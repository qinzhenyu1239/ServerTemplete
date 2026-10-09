using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Framework.Core.Net;
using Framework.Core.Serializer;
using Game.Datas.Messages;

namespace Game.Core.GM_Auth
{
    public sealed partial class GM_AuthSessionMgr
    {
        private sealed class LogoutNotification
        {
            public LoginSession Marker;
            public long TokenExpiresAt;
            public long Deadline;
            public long NextAttempt;
            public int Failures;
            public HashSet<int> Pending;
            public readonly SemaphoreSlim Gate = new(1, 1);
        }
        private readonly ConcurrentDictionary<string, LogoutNotification> logoutRecords = new();
        private CancellationTokenSource logoutRetryCancellation;
        private Task logoutRetryTask;

        public async Task<AuthOperationStatus> LogoutAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 512)
                return AuthOperationStatus.InvalidAccessToken;
            string hash = HashToken(accessToken);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!logoutRecords.TryGetValue(hash, out LogoutNotification record))
            {
                if (!accessSessions.TryGetValue(hash, out LoginSession login) || login.ExpiresAtUnixSeconds <= now)
                    return AuthOperationStatus.InvalidAccessToken;
                LoginSessionCreationLock accountLock = RetainLoginSessionLock(login.AccountId);
                try
                {
                    lock (accountLock.Sync)
                    {
                        if (!logoutRecords.TryGetValue(hash, out record))
                        {
                            if (login.ExpiresAtUnixSeconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                                return AuthOperationStatus.InvalidAccessToken;
                            bool isCurrent = currentSessions.TryGetValue(login.AccountId, out LoginSession current) &&
                                ReferenceEquals(current, login);
                            // A replaced login must never mint a barrier above the new login.
                            LoginSession marker = current;
                            if (isCurrent)
                            {
                                marker = new LoginSession
                                {
                                    AccountId = login.AccountId, SessionId = CreateRandomToken(),
                                    SessionVersion = sessionVersions.AddOrUpdate(login.AccountId,
                                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                        (_, previous) => Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), previous + 1)),
                                    ExpiresAtUnixSeconds = login.ExpiresAtUnixSeconds
                                };
                                // Keep the version barrier; this marker has no access token.
                                currentSessions[login.AccountId] = marker;
                            }
                            accessSessions.TryRemove(hash, out _);
                            record = new LogoutNotification
                            {
                                Marker = marker, TokenExpiresAt = login.ExpiresAtUnixSeconds,
                                Deadline = login.ExpiresAtUnixSeconds + (long)ticketLifetime.TotalSeconds,
                                NextAttempt = now,
                                Pending = marker == null ? new HashSet<int>() : new HashSet<int>(login.GameServerIds)
                            };
                            logoutRecords[hash] = record;
                        }
                    }
                }
                finally { ReleaseLoginSessionLock(login.AccountId, accountLock); }
            }
            if (record.TokenExpiresAt <= now) return AuthOperationStatus.InvalidAccessToken;
            await record.Gate.WaitAsync();
            try
            {
                await NotifyPendingAsync(record, CancellationToken.None);
                return record.Pending.Count == 0 ? AuthOperationStatus.Success : AuthOperationStatus.NotificationPending;
            }
            finally { record.Gate.Release(); }
        }

        private async Task NotifyPendingAsync(LogoutNotification record, CancellationToken cancellation)
        {
            int[] targets = record.Pending.ToArray();
            bool[] results = await Task.WhenAll(targets.Select(id => SendLogoutNotificationAsync(record.Marker, id, cancellation)));
            for (int i = 0; i < targets.Length; i++)
                if (results[i]) record.Pending.Remove(targets[i]);
            if (record.Pending.Count > 0)
            {
                int delay = record.Failures++ switch { 0 => 1, 1 => 2, 2 => 5, _ => 10 };
                record.NextAttempt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + delay;
            }
        }

        private async Task<bool> SendLogoutNotificationAsync(LoginSession marker, int serverId, CancellationToken cancellation)
        {
            if (!gameSessionNotificationUrls.TryGetValue(serverId, out string url)) return false;
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string nonce = CreateRandomToken();
            var request = new ReqInvalidateAccountSession
            {
                accountId = marker.AccountId, sessionId = marker.SessionId, sessionVersion = marker.SessionVersion,
                timestamp = timestamp, nonce = nonce,
                signature = Convert.ToBase64String(ComputeHmac(InvalidationPurpose +
                    BuildInvalidationUnsignedBody(marker.AccountId, marker.SessionVersion, marker.SessionId, timestamp, nonce)))
            };
            byte[] packet = MessagePusher.PackMessage(request, 0);
            if (packet == null) return false;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                using var content = new StringContent(Convert.ToBase64String(packet), Encoding.UTF8, "text/plain");
                using HttpResponseMessage response = await InternalHttpClient.PostAsync(url, content, timeout.Token);
                return response.IsSuccessStatusCode && await response.Content.ReadAsStringAsync(timeout.Token) == "OK";
            }
            catch (Exception exception)
            {
                logger.Warn(exception, $"Logout notification pending: account[{marker.AccountId}] server[{serverId}]");
                return false;
            }
        }

        private void StartLogoutNotificationRetryLoop()
        {
            StopLogoutRetries();
            logoutRetryCancellation = new CancellationTokenSource();
            logoutRetryTask = RetryLogoutsAsync(logoutRetryCancellation.Token);
        }
        public void StopLogoutRetries() => logoutRetryCancellation?.Cancel();
        public async Task StopLogoutRetriesAsync()
        {
            StopLogoutRetries();
            if (logoutRetryTask != null) await logoutRetryTask;
        }

        private async Task RetryLogoutsAsync(CancellationToken cancellation)
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (await timer.WaitForNextTickAsync(cancellation))
                    await Task.WhenAll(logoutRecords.Select(pair => RetryLogoutAsync(pair.Key, pair.Value, cancellation)));
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        private async Task RetryLogoutAsync(string hash, LogoutNotification record, CancellationToken cancellation)
        {
            if (!await record.Gate.WaitAsync(0, cancellation)) return;
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now >= record.Deadline)
                {
                    if (record.Pending.Count > 0)
                        logger.Error($"Logout cleanup deadline exceeded: account[{record.Marker.AccountId}] servers[{string.Join(",", record.Pending)}]");
                    logoutRecords.TryRemove(hash, out _);
                }
                else if (record.Pending.Count == 0 && now >= record.TokenExpiresAt)
                    logoutRecords.TryRemove(hash, out _);
                else if (record.Pending.Count > 0 && now >= record.NextAttempt)
                    await NotifyPendingAsync(record, cancellation);
            }
            finally { record.Gate.Release(); }
        }
    }
}
