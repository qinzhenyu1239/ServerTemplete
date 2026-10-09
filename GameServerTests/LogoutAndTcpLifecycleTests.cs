using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using DotNetty.Transport.Channels.Embedded;
using Framework.Core.Net;
using Framework.Core.Serializer;
using Game.Core.EntityMgr;
using Game.Core.GM_Auth;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace GameServerTests;

[CollectionDefinition("Logout lifecycle", DisableParallelization = true)]
public class LogoutLifecycleCollection { }

[Collection("Logout lifecycle")]
public class LogoutAndTcpLifecycleTests
{
    private const string Secret = "logout-tests-only-secret-with-at-least-32-bytes";
    private static GM_AuthSessionMgr Manager(IReadOnlyDictionary<int, string> urls = null)
    {
        var manager = new GM_AuthSessionMgr();
        string previous = Environment.GetEnvironmentVariable("GAME_TICKET_HMAC_SECRET");
        try
        {
            Environment.SetEnvironmentVariable("GAME_TICKET_HMAC_SECRET", Secret);
            manager.Init(urls);
        }
        finally { Environment.SetEnvironmentVariable("GAME_TICKET_HMAC_SECRET", previous); }
        return manager;
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);

    [Fact]
    public void MalformedPacketsDoNotRefreshActivityButHeartbeatDoes()
    {
        var assembly = typeof(IdSession).Assembly;
        Type factoryType = assembly.GetType("Framework.Core.Serializer.MessageFactory");
        object factory = factoryType.GetField("Instance").GetValue(null);
        var mappings = (System.Collections.IDictionary)factoryType.GetField("keyTypeDic", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(factory);
        var previousMappings = mappings.Cast<System.Collections.DictionaryEntry>().ToArray();
        try
        {
        if (mappings.Count == 0) factoryType.GetMethod("InitMeesagePool").Invoke(factory, null);
        Type dispatcherType = assembly.GetType("Framework.Core.Net.MessageDispatcher");
        object dispatcher = dispatcherType.GetField("Instance").GetValue(null);
        var session = new IdSession { accountId = 220, playerId = 220 };
        long stale = Stopwatch.GetTimestamp() - 61 * Stopwatch.Frequency;
        Set(session, "lastValidActivity", stale);
        byte[] packet = MessagePusher.PackMessage(new ReqHeartbeat(), 0);
        var receive = dispatcherType.GetMethod("OnClientMsg");
        receive.Invoke(dispatcher, new object[] { session, new byte[3], 0, 3 });
        Assert.True(session.IsIdle(TimeSpan.FromSeconds(60)));
        byte[] malformed = packet.Take(ProtocolPacketLayout.HeaderLength).Concat(new byte[] { 0x80 }).ToArray();
        receive.Invoke(dispatcher, new object[] { session, malformed, 0, malformed.Length });
        Assert.True(session.IsIdle(TimeSpan.FromSeconds(60)));
        receive.Invoke(dispatcher, new object[] { session, packet, 0, packet.Length });
        Assert.False(session.IsIdle(TimeSpan.FromSeconds(60)));
        }
        finally
        {
            mappings.Clear();
            foreach (var entry in previousMappings) mappings.Add(entry.Key, entry.Value);
        }
    }

    [Fact]
    public async Task NotificationTimeoutIsBoundedAndDeadlineRemovesPendingWork()
    {
        int port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var manager = Manager(new Dictionary<int, string> { [1] = $"http://127.0.0.1:{port}/silent" });
        try
        {
            string token = manager.CreateLoginSession(221);
            manager.IssueGameTicket(token, 1);
            Task<HttpListenerContext> accepted = listener.GetContextAsync();
            var watch = Stopwatch.StartNew();
            Assert.Equal(AuthOperationStatus.NotificationPending, await manager.LogoutAsync(token));
            Assert.InRange(watch.Elapsed.TotalSeconds, 1.5, 6);
            (await accepted).Response.Close();
            await manager.StopLogoutRetriesAsync();
            var records = (System.Collections.IDictionary)typeof(GM_AuthSessionMgr)
                .GetField("logoutRecords", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            object record = records.Values.Cast<object>().Single();
            Set(record, "Deadline", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1);
            string hash = records.Keys.Cast<string>().Single();
            var retry = (Task)typeof(GM_AuthSessionMgr).GetMethod("RetryLogoutAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, new object[] { hash, record, CancellationToken.None });
            await retry;
            Assert.Empty(records.Keys.Cast<string>());
            Assert.False(manager.ValidateAccessToken(token).IsSuccess);
        }
        finally { await manager.StopLogoutRetriesAsync(); }
    }

    [Fact]
    public void LogoutProtocolRoundTrips()
    {
        Message[] messages = { new ReqUserLogout { accessToken = "token" },
            new ResUserLogout { status = -1, errorMessage = "pending" } };
        foreach (Message message in messages)
        {
            using var stream = new MemoryStream();
            Assert.True(GoogleProtocolCodec.TrySerialize(message, stream, out string error), error);
            stream.Position = 0;
            Assert.True(GoogleProtocolCodec.TryDeserialize(message.GetType(), stream, out Message result, out error), error);
            Assert.Equal(message.GetCmd(), result.GetCmd());
            if (result is ReqUserLogout request) { Assert.Equal(1008, request.GetCmd()); Assert.Equal("token", request.accessToken); }
            if (result is ResUserLogout response) { Assert.Equal(1009, response.GetCmd()); Assert.Equal(-1, response.status); Assert.Equal("pending", response.errorMessage); }
        }
    }

    [Fact]
    public async Task LogoutWithoutGameTicketIsIdempotentAndNewLoginSurvives()
    {
        var manager = Manager();
        try
        {
            string oldToken = manager.CreateLoginSession(123);
            Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(oldToken));
            Assert.False(manager.ValidateAccessToken(oldToken).IsSuccess);
            Assert.Equal(AuthOperationStatus.InvalidAccessToken, manager.IssueGameTicket(oldToken, 1).Status);
            string newToken = manager.CreateLoginSession(123);
            Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(oldToken));
            Assert.True(manager.ValidateAccessToken(newToken).IsSuccess);
            Assert.Equal(AuthOperationStatus.InvalidAccessToken, await manager.LogoutAsync("unknown"));
        }
        finally { await manager.StopLogoutRetriesAsync(); }
    }

    [Fact]
    public async Task ReplacedTokenLogoutCannotRevokeNewLogin()
    {
        var manager = Manager();
        try
        {
            string oldToken = manager.CreateLoginSession(124);
            string newToken = manager.CreateLoginSession(124);
            Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(oldToken));
            Assert.True(manager.ValidateAccessToken(newToken).IsSuccess);
        }
        finally { await manager.StopLogoutRetriesAsync(); }
    }

    [Fact]
    public async Task NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession()
    {
        var game = Manager();
        var requests = new List<ReqInvalidateAccountSession>();
        int port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var login = Manager(new Dictionary<int, string> { [1] = $"http://127.0.0.1:{port}/invalidate" });
        try
        {
            string token = login.CreateLoginSession(125);
            string oldTicket = login.IssueGameTicket(token, 1).Ticket;
            Task server = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync();
                using var reader = new StreamReader(context.Request.InputStream);
                byte[] packet = Convert.FromBase64String(await reader.ReadToEndAsync());
                using var stream = new MemoryStream(packet, ProtocolPacketLayout.HeaderLength,
                    packet.Length - ProtocolPacketLayout.HeaderLength);
                Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ReqInvalidateAccountSession), stream, out Message decoded, out string error), error);
                var request = (ReqInvalidateAccountSession)decoded; requests.Add(request);
                Assert.True(game.ApplySessionInvalidation(Command(request)));
                byte[] reply = Encoding.UTF8.GetBytes("OK");
                await context.Response.OutputStream.WriteAsync(reply); context.Response.Close();
            });
            Assert.Equal(AuthOperationStatus.Success, await login.LogoutAsync(token));
            await server.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AuthOperationStatus.SupersededSession,
                game.AuthenticateGameSession(new IdSession(), oldTicket, 1).Status);
            string fresh = login.CreateLoginSession(125);
            string ticket = login.IssueGameTicket(fresh, 1).Ticket;
            var session = new IdSession();
            Assert.Equal(AuthOperationStatus.Success, game.AuthenticateGameSession(session, ticket, 1).Status);
            Assert.True(game.ApplySessionInvalidation(Command(requests[0])));
            Assert.True(SessionMgr.Instance.IsCurrentConnection(session));
            Assert.Equal(AuthOperationStatus.Success, await login.LogoutAsync(token));
        }
        finally { await login.StopLogoutRetriesAsync(); await game.StopLogoutRetriesAsync(); }
    }

    [Fact]
    public async Task FailedNotificationRetriesAndAcknowledgedTargetsAreNotRetried()
    {
        int port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();
        var manager = Manager(new Dictionary<int, string> {
            [1] = $"http://127.0.0.1:{port}/one", [2] = $"http://127.0.0.1:{port}/two" });
        var count = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        Task server = Task.Run(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cancellation.Token);
                string path = context.Request.Url.AbsolutePath;
                int attempt = count.AddOrUpdate(path, 1, (_, n) => n + 1);
                bool ok = path == "/one" || attempt > 1;
                byte[] reply = Encoding.UTF8.GetBytes(ok ? "OK" : "INVALID");
                await context.Response.OutputStream.WriteAsync(reply); context.Response.Close();
                if (path == "/two" && ok) recovered.TrySetResult();
            }
        });
        try
        {
            string token = manager.CreateLoginSession(126);
            manager.IssueGameTicket(token, 1); manager.IssueGameTicket(token, 2);
            Assert.Equal(AuthOperationStatus.NotificationPending, await manager.LogoutAsync(token));
            Assert.False(manager.ValidateAccessToken(token).IsSuccess);
            await recovered.Task.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(token));
            Assert.Equal(1, count["/one"]); Assert.True(count["/two"] >= 2);
        }
        finally
        {
            await manager.StopLogoutRetriesAsync(); cancellation.Cancel();
            try { await server; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public void OldUnbindAndIdleClosureCannotRemoveNewSession()
    {
        var manager = new SessionMgr();
        var oldChannel = new EmbeddedChannel(); var newChannel = new EmbeddedChannel();
        try
        {
            var old = new IdSession { client = oldChannel };
            var current = new IdSession { client = newChannel };
            manager.BindAuthenticatedAccount(old, 200, "old");
            manager.BindAuthenticatedAccount(current, 200, "new");
            manager.UnbindAuthenticatedAccount(old);
            Assert.True(manager.IsCurrentConnection(current));
            Set(manager, "idleTimeout", TimeSpan.FromSeconds(60));
            Set(current, "lastValidActivity", Stopwatch.GetTimestamp() - 61 * Stopwatch.Frequency);
            Scan(manager);
            current.MarkValidActivity(); // Fresh data wins over a queued timeout decision.
            newChannel.RunPendingTasks();
            Assert.True(newChannel.Active);
            Set(current, "lastValidActivity", Stopwatch.GetTimestamp() - 61 * Stopwatch.Frequency);
            Scan(manager); newChannel.RunPendingTasks();
            Assert.False(newChannel.Active);
        }
        finally { oldChannel.FinishAndReleaseAll(); newChannel.FinishAndReleaseAll(); }
    }

    [Fact]
    public void DelayedOldLogicExitDoesNotClearNewPlayerConnection()
    {
        GM_EntityMgr.Instance.Init();
        var players = (System.Collections.Concurrent.ConcurrentDictionary<long, GM_PlayerEntity>)
            typeof(GM_EntityMgr).GetField("players", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GM_EntityMgr.Instance);
        var old = new IdSession { playerId = 201, logicServerId = 1 };
        var current = new IdSession { playerId = 201 };
        var player = new GM_PlayerEntity(); player.uPlayer.s = current; players[201] = player;
        var logic = new Game.LogicServer.WorldLogicServer();
        Assert.Null(logic.DoReqExitLogicServer(old, new ReqExitLogicServer { quitReason = (int)Game.QuitReason.DisconnectQuit }, 201));
        Assert.Same(current, player.uPlayer.s);
    }

    private static void Scan(SessionMgr manager) => typeof(SessionMgr)
        .GetMethod("CheckIdleConnections", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(manager, new object[] { CancellationToken.None });
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
    }
    private static SessionInvalidationCommand Command(ReqInvalidateAccountSession request) => new()
    {
        AccountId = request.accountId, SessionVersion = request.sessionVersion, SessionId = request.sessionId,
        Timestamp = request.timestamp, Nonce = request.nonce, Signature = Convert.FromBase64String(request.signature)
    };
}

