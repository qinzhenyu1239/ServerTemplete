using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game.Datas.Messages;
using NLog;

namespace Game.Core.GM_Auth
{
    public sealed partial class GM_AuthSessionMgr
    {
        private const string TicketVersion = "v2";
        private const string InvalidationPurpose = "session-invalidate|";

        public static GM_AuthSessionMgr Instance { get; } = new GM_AuthSessionMgr();
        private static readonly HttpClient InternalHttpClient = new() { Timeout = TimeSpan.FromSeconds(2) };

        private readonly ConcurrentDictionary<string, LoginSession> accessSessions = new();
        private readonly ConcurrentDictionary<long, LoginSession> currentSessions = new();
        private readonly ConcurrentDictionary<string, long> consumedNonces = new();
        private readonly ConcurrentDictionary<long, long> sessionVersions = new();
        private readonly ConcurrentDictionary<long, AcceptedGameSession> latestGameSessions = new();
        private readonly object gameSessionLock = new();
        private readonly object loginSessionLocksSync = new();
        private readonly Dictionary<long, LoginSessionCreationLock> loginSessionLocks = new();
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private TimeSpan accessLifetime;
        private TimeSpan ticketLifetime;
        private byte[] ticketHmacKey;
        private long processStartedAtUnixSeconds;
        private long nextCleanupAtUnixSeconds;
        private IReadOnlyDictionary<int, string> gameSessionNotificationUrls =
            new Dictionary<int, string>();

        private sealed class LoginSession
        {
            public long AccountId { get; init; }
            public string SessionId { get; init; }
            public long SessionVersion { get; init; }
            public long ExpiresAtUnixSeconds { get; init; }
            public int GameServerId { get; set; }
            public HashSet<int> GameServerIds { get; } = new();
        }

        private sealed class AcceptedGameSession
        {
            public string SessionId { get; init; }
            public long SessionVersion { get; init; }
        }

        private sealed class LoginSessionCreationLock
        {
            public readonly object Sync = new();
            public int ReferenceCount;
        }

        public void Init(IReadOnlyDictionary<int, string> notificationUrls = null)
        {
            accessLifetime = TimeSpan.FromMinutes(ReadPositiveInt("AuthAccessTokenMinutes", 30));
            ticketLifetime = TimeSpan.FromSeconds(ReadPositiveInt("GameTicketSeconds", 60));

            string secret = Environment.GetEnvironmentVariable("GAME_TICKET_HMAC_SECRET");
            if (string.IsNullOrWhiteSpace(secret))
            {
                secret = ConfigurationManager.AppSettings["GameTicketHmacSecret"];
            }
            if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            {
                throw new ConfigurationErrorsException(
                    "GameTicketHmacSecret (or GAME_TICKET_HMAC_SECRET) must contain at least 32 UTF-8 bytes.");
            }

            ticketHmacKey = Encoding.UTF8.GetBytes(secret);
            gameSessionNotificationUrls = notificationUrls == null
                ? new Dictionary<int, string>()
                : new Dictionary<int, string>(notificationUrls);
            processStartedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            nextCleanupAtUnixSeconds = processStartedAtUnixSeconds + 60;
            StartLogoutNotificationRetryLoop();
        }

        public string CreateLoginSession(long accountId)
        {
            if (accountId <= 0) throw new ArgumentOutOfRangeException(nameof(accountId));

            LoginSessionCreationLock accountLock = RetainLoginSessionLock(accountId);
            LoginSession session;
            LoginSession previousSession;
            string accessToken;
            try
            {
                lock (accountLock.Sync)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    CleanupExpiredEntries(now);

                    string sessionId = CreateRandomToken();
                    accessToken = CreateRandomToken();
                    long nowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long sessionVersion = sessionVersions.AddOrUpdate(
                        accountId, nowMilliseconds, (_, previous) => Math.Max(nowMilliseconds, previous + 1));
                    session = new LoginSession
                    {
                        AccountId = accountId,
                        SessionId = sessionId,
                        SessionVersion = sessionVersion,
                        ExpiresAtUnixSeconds = now + (long)accessLifetime.TotalSeconds
                    };

                    accessSessions[HashToken(accessToken)] = session;
                    currentSessions.TryGetValue(accountId, out previousSession);
                    currentSessions[accountId] = session;
                }
            }
            finally
            {
                ReleaseLoginSessionLock(accountId, accountLock);
            }

            NotifyGameServerSessionChanged(session, previousSession?.GameServerId ?? 0);
            return accessToken;
        }

        private LoginSessionCreationLock RetainLoginSessionLock(long accountId)
        {
            lock (loginSessionLocksSync)
            {
                if (!loginSessionLocks.TryGetValue(accountId, out LoginSessionCreationLock accountLock))
                {
                    accountLock = new LoginSessionCreationLock();
                    loginSessionLocks.Add(accountId, accountLock);
                }

                accountLock.ReferenceCount++;
                return accountLock;
            }
        }

        private void ReleaseLoginSessionLock(long accountId, LoginSessionCreationLock accountLock)
        {
            lock (loginSessionLocksSync)
            {
                accountLock.ReferenceCount--;
                if (accountLock.ReferenceCount == 0 &&
                    loginSessionLocks.TryGetValue(accountId, out LoginSessionCreationLock current) &&
                    ReferenceEquals(current, accountLock))
                {
                    loginSessionLocks.Remove(accountId);
                }
            }
        }

        public IssueGameTicketResult IssueGameTicket(string accessToken, int serverId)
        {
            AccessTokenValidationResult validation = ValidateAccessToken(accessToken);
            if (serverId <= 0 || !validation.IsSuccess)
            {
                return new IssueGameTicketResult { Status = AuthOperationStatus.InvalidAccessToken };
            }

            long accountId = validation.AccountId;
            LoginSessionCreationLock accountLock = RetainLoginSessionLock(accountId);
            try
            {
                lock (accountLock.Sync)
                {
                    validation = ValidateAccessToken(accessToken);
                    if (!validation.IsSuccess ||
                        !currentSessions.TryGetValue(validation.AccountId, out LoginSession session) ||
                        !string.Equals(session.SessionId, validation.SessionId, StringComparison.Ordinal))
                    {
                        return new IssueGameTicketResult { Status = AuthOperationStatus.InvalidAccessToken };
                    }

                    session.GameServerId = serverId;
                    session.GameServerIds.Add(serverId);
                    long issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    long expiresAt = issuedAt + (long)ticketLifetime.TotalSeconds;
                    string nonce = CreateRandomToken();
                    string payload = string.Join("|",
                        validation.AccountId.ToString(CultureInfo.InvariantCulture),
                        validation.SessionId,
                        validation.SessionVersion.ToString(CultureInfo.InvariantCulture),
                        serverId.ToString(CultureInfo.InvariantCulture),
                        issuedAt.ToString(CultureInfo.InvariantCulture),
                        expiresAt.ToString(CultureInfo.InvariantCulture),
                        nonce);

                    string encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
                    byte[] signature = HMACSHA256.HashData(ticketHmacKey, Encoding.ASCII.GetBytes(encodedPayload));
                    return new IssueGameTicketResult
                    {
                        Status = AuthOperationStatus.Success,
                        Ticket = TicketVersion + "." + encodedPayload + "." + Base64UrlEncode(signature),
                        ExpiresAtUnixSeconds = expiresAt
                    };
                }
            }
            finally
            {
                ReleaseLoginSessionLock(accountId, accountLock);
            }
        }

        public GameAuthenticationResult AuthenticateGameSession(
            IdSession connection,
            string ticket,
            int serverId)
        {
            AccessTokenValidationResult ticketResult = ConsumeGameTicket(ticket, serverId);
            if (!ticketResult.IsSuccess)
                return new GameAuthenticationResult { Status = AuthOperationStatus.InvalidGameTicket };

            if (!TryBindAuthenticatedGameSession(
                    connection,
                    ticketResult.AccountId,
                    ticketResult.SessionId,
                    ticketResult.SessionVersion))
            {
                return new GameAuthenticationResult { Status = AuthOperationStatus.SupersededSession };
            }

            return new GameAuthenticationResult
            {
                Status = AuthOperationStatus.Success,
                AccountId = ticketResult.AccountId
            };
        }

        private AccessTokenValidationResult ConsumeGameTicket(string ticket, int serverId)
        {
            if (serverId <= 0 || string.IsNullOrWhiteSpace(ticket) || ticket.Length > 2048 || ticketHmacKey == null)
            {
                return InvalidTicket();
            }

            string[] ticketParts = ticket.Split('.');
            if (ticketParts.Length != 3 || ticketParts[0] != TicketVersion ||
                !TryBase64UrlDecode(ticketParts[2], out byte[] suppliedSignature))
            {
                return InvalidTicket();
            }

            byte[] expectedSignature = HMACSHA256.HashData(
                ticketHmacKey, Encoding.ASCII.GetBytes(ticketParts[1]));
            if (suppliedSignature.Length != expectedSignature.Length ||
                !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            {
                return InvalidTicket();
            }

            if (!TryBase64UrlDecode(ticketParts[1], out byte[] payloadBytes) ||
                !TryDecodeTicket(Encoding.UTF8.GetString(payloadBytes), out long accountId, out string sessionId,
                    out long sessionVersion, out int ticketServerId, out long issuedAt, out long expiresAt,
                    out string nonce))
            {
                return InvalidTicket();
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long maximumLifetime = (long)ticketLifetime.TotalSeconds;
            if (ticketServerId != serverId || issuedAt > now + 5 || expiresAt <= now ||
                expiresAt - issuedAt != maximumLifetime || issuedAt < processStartedAtUnixSeconds)
            {
                return InvalidTicket();
            }

            CleanupExpiredEntries(now);
            if (!consumedNonces.TryAdd(nonce, expiresAt))
            {
                return InvalidTicket();
            }

            return new AccessTokenValidationResult
            {
                Status = AuthOperationStatus.Success,
                AccountId = accountId,
                SessionId = sessionId,
                SessionVersion = sessionVersion
            };
        }

        private static AccessTokenValidationResult InvalidTicket()
        {
            return new AccessTokenValidationResult { Status = AuthOperationStatus.InvalidGameTicket };
        }

        public AccessTokenValidationResult ValidateAccessToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 512)
                return InvalidAccessToken();

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            CleanupExpiredEntries(now);
            string tokenHash = HashToken(accessToken);
            if (!accessSessions.TryGetValue(tokenHash, out LoginSession loginSession))
                return InvalidAccessToken();
            if (loginSession.ExpiresAtUnixSeconds <= now)
            {
                accessSessions.TryRemove(tokenHash, out _);
                return InvalidAccessToken();
            }
            if (!currentSessions.TryGetValue(loginSession.AccountId, out LoginSession currentSession) ||
                !string.Equals(currentSession.SessionId, loginSession.SessionId, StringComparison.Ordinal) ||
                currentSession.SessionVersion != loginSession.SessionVersion)
            {
                return InvalidAccessToken();
            }

            return new AccessTokenValidationResult
            {
                Status = AuthOperationStatus.Success,
                AccountId = loginSession.AccountId,
                SessionId = loginSession.SessionId,
                SessionVersion = loginSession.SessionVersion
            };
        }

        private static AccessTokenValidationResult InvalidAccessToken()
        {
            return new AccessTokenValidationResult { Status = AuthOperationStatus.InvalidAccessToken };
        }

        private void CleanupExpiredEntries(long now)
        {
            if (now < nextCleanupAtUnixSeconds) return;
            nextCleanupAtUnixSeconds = now + 60;

            foreach (var pair in accessSessions)
            {
                if (pair.Value.ExpiresAtUnixSeconds <= now)
                {
                    accessSessions.TryRemove(pair.Key, out _);
                }
            }

            foreach (var pair in consumedNonces)
            {
                if (pair.Value <= now) consumedNonces.TryRemove(pair.Key, out _);
            }
        }

        private static string CreateRandomToken()
        {
            return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        }

        private static string HashToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }

        public bool TryBindAuthenticatedGameSession(IdSession connection, long accountId, string sessionId,
            long sessionVersion)
        {
            if (connection == null || accountId <= 0 || string.IsNullOrWhiteSpace(sessionId) || sessionVersion <= 0)
                return false;

            lock (gameSessionLock)
            {
                if (!TryAcceptGameSession(accountId, sessionId, sessionVersion)) return false;
                SessionMgr.Instance.BindAuthenticatedAccount(connection, accountId, sessionId);
                return true;
            }
        }

        public bool ApplySessionInvalidation(SessionInvalidationCommand command)
        {
            string unsignedBody = BuildInvalidationUnsignedBody(
                command.AccountId,
                command.SessionVersion,
                command.SessionId,
                command.Timestamp,
                command.Nonce);
            byte[] expectedSignature = ComputeHmac(InvalidationPurpose + unsignedBody);
            if (command.Signature.Length != expectedSignature.Length ||
                !CryptographicOperations.FixedTimeEquals(command.Signature, expectedSignature))
            {
                return false;
            }

            lock (gameSessionLock)
            {
                if (!TryAcceptGameSession(command.AccountId, command.SessionId, command.SessionVersion)) return true;
                SessionMgr.Instance.KickSupersededSession(command.AccountId, command.SessionId);
                return true;
            }
        }

        private bool TryAcceptGameSession(long accountId, string sessionId, long sessionVersion)
        {
            if (latestGameSessions.TryGetValue(accountId, out AcceptedGameSession current))
            {
                if (sessionVersion < current.SessionVersion) return false;
                if (sessionVersion == current.SessionVersion)
                    return string.Equals(sessionId, current.SessionId, StringComparison.Ordinal);
            }

            latestGameSessions[accountId] = new AcceptedGameSession
            {
                SessionId = sessionId,
                SessionVersion = sessionVersion
            };
            return true;
        }

        private void NotifyGameServerSessionChanged(LoginSession session, int previousGameServerId)
        {
            if (previousGameServerId <= 0) return;
            if (!gameSessionNotificationUrls.TryGetValue(previousGameServerId, out string notificationUrl))
            {
                logger.Warn(
                    $"Cannot invalidate account[{session.AccountId}] session: game server[{previousGameServerId}] is not configured.");
                return;
            }

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string nonce = CreateRandomToken();
            string unsignedBody = BuildInvalidationUnsignedBody(
                session.AccountId, session.SessionVersion, session.SessionId, timestamp, nonce);
            var request = new ReqInvalidateAccountSession
            {
                accountId = session.AccountId,
                sessionVersion = session.SessionVersion,
                sessionId = session.SessionId,
                timestamp = timestamp,
                nonce = nonce,
                signature = Convert.ToBase64String(ComputeHmac(InvalidationPurpose + unsignedBody))
            };
            byte[] packet = MessagePusher.PackMessage(request, 0);
            if (packet == null) return;
            string requestBody = Convert.ToBase64String(packet);

            try
            {
                using var content = new StringContent(requestBody, Encoding.UTF8, "text/plain");
                using HttpResponseMessage response = InternalHttpClient.PostAsync(notificationUrl, content)
                    .GetAwaiter().GetResult();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode || !string.Equals(responseBody, "OK", StringComparison.Ordinal))
                    logger.Warn($"Game session invalidation failed for account[{session.AccountId}]: {response.StatusCode}");
            }
            catch (Exception exception)
            {
                logger.Warn(exception, $"Game session invalidation failed for account[{session.AccountId}]");
            }
        }

        private byte[] ComputeHmac(string value)
        {
            return HMACSHA256.HashData(ticketHmacKey, Encoding.UTF8.GetBytes(value));
        }

        private static string BuildInvalidationUnsignedBody(
            long accountId, long sessionVersion, string sessionId, long timestamp, string nonce) =>
            string.Join("|",
                accountId.ToString(CultureInfo.InvariantCulture),
                sessionVersion.ToString(CultureInfo.InvariantCulture),
                sessionId,
                timestamp.ToString(CultureInfo.InvariantCulture),
                nonce);

        private static bool TryDecodeTicket(string value, out long accountId, out string sessionId,
            out long sessionVersion, out int serverId, out long issuedAt, out long expiresAt, out string nonce)
        {
            accountId = 0;
            sessionId = null;
            sessionVersion = 0;
            serverId = 0;
            issuedAt = 0;
            expiresAt = 0;
            nonce = null;
            string[] parts = value?.Split('|');
            if (parts == null || parts.Length != 7 ||
                !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out accountId) ||
                !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out sessionVersion) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out serverId) ||
                !long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out issuedAt) ||
                !long.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out expiresAt))
            {
                return false;
            }

            sessionId = parts[1];
            nonce = parts[6];
            return accountId > 0 && !string.IsNullOrWhiteSpace(sessionId) &&
                   sessionVersion > 0 && serverId > 0 && !string.IsNullOrWhiteSpace(nonce);
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static bool TryBase64UrlDecode(string value, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string padded = value.Replace('-', '+').Replace('_', '/');
            int remainder = padded.Length % 4;
            if (remainder == 1) return false;
            padded += remainder switch { 0 => string.Empty, 2 => "==", 3 => "=", _ => string.Empty };
            try
            {
                bytes = Convert.FromBase64String(padded);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static int ReadPositiveInt(string key, int fallback)
        {
            return int.TryParse(ConfigurationManager.AppSettings[key], out int value) && value > 0 ? value : fallback;
        }
    }
}
