using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Framework.Core.Net;
using NLog;

namespace Game.Entries.Modules
{
    public sealed class AuthSessionModule
    {
        private const string TicketVersion = "v2";
        private const string InvalidationPurpose = "session-invalidate|";

        public static AuthSessionModule Instance { get; } = new AuthSessionModule();
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
        private string gameSessionNotifyUrl;

        private sealed class LoginSession
        {
            public long AccountId { get; init; }
            public string SessionId { get; init; }
            public long SessionVersion { get; init; }
            public long ExpiresAtUnixSeconds { get; init; }
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

        public void Init()
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
            gameSessionNotifyUrl = ConfigurationManager.AppSettings["GameSessionNotifyUrl"];
            processStartedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            nextCleanupAtUnixSeconds = processStartedAtUnixSeconds + 60;
        }

        public string CreateLoginSession(long accountId)
        {
            if (accountId <= 0) throw new ArgumentOutOfRangeException(nameof(accountId));

            LoginSessionCreationLock accountLock = RetainLoginSessionLock(accountId);
            try
            {
                lock (accountLock.Sync)
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    CleanupExpiredEntries(now);

                    string sessionId = CreateRandomToken();
                    string accessToken = CreateRandomToken();
                    long nowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long sessionVersion = sessionVersions.AddOrUpdate(
                        accountId, nowMilliseconds, (_, previous) => Math.Max(nowMilliseconds, previous + 1));
                    var session = new LoginSession
                    {
                        AccountId = accountId,
                        SessionId = sessionId,
                        SessionVersion = sessionVersion,
                        ExpiresAtUnixSeconds = now + (long)accessLifetime.TotalSeconds
                    };

                    accessSessions[HashToken(accessToken)] = session;
                    currentSessions[accountId] = session;
                    NotifyGameServerSessionChanged(session);
                    return accessToken;
                }
            }
            finally
            {
                ReleaseLoginSessionLock(accountId, accountLock);
            }
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

        public bool TryIssueGameTicket(string accessToken, int serverId, out string ticket, out long expiresAt)
        {
            ticket = null;
            expiresAt = 0;
            if (serverId <= 0 ||
                !TryValidateAccessToken(accessToken, out long accountId, out string sessionId,
                    out long sessionVersion))
            {
                return false;
            }

            long issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            expiresAt = issuedAt + (long)ticketLifetime.TotalSeconds;
            string nonce = CreateRandomToken();
            string payload = string.Join("|",
                accountId.ToString(CultureInfo.InvariantCulture),
                sessionId,
                sessionVersion.ToString(CultureInfo.InvariantCulture),
                serverId.ToString(CultureInfo.InvariantCulture),
                issuedAt.ToString(CultureInfo.InvariantCulture),
                expiresAt.ToString(CultureInfo.InvariantCulture),
                nonce);

            string encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
            byte[] signature = HMACSHA256.HashData(ticketHmacKey, Encoding.ASCII.GetBytes(encodedPayload));
            ticket = TicketVersion + "." + encodedPayload + "." + Base64UrlEncode(signature);
            return true;
        }

        public bool TryConsumeGameTicket(string ticket, int serverId, out long accountId, out string sessionId,
            out long sessionVersion)
        {
            accountId = 0;
            sessionId = null;
            sessionVersion = 0;
            if (serverId <= 0 || string.IsNullOrWhiteSpace(ticket) || ticket.Length > 2048 || ticketHmacKey == null)
            {
                return false;
            }

            string[] ticketParts = ticket.Split('.');
            if (ticketParts.Length != 3 || ticketParts[0] != TicketVersion ||
                !TryBase64UrlDecode(ticketParts[2], out byte[] suppliedSignature))
            {
                return false;
            }

            byte[] expectedSignature = HMACSHA256.HashData(
                ticketHmacKey, Encoding.ASCII.GetBytes(ticketParts[1]));
            if (suppliedSignature.Length != expectedSignature.Length ||
                !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            {
                return false;
            }

            if (!TryBase64UrlDecode(ticketParts[1], out byte[] payloadBytes) ||
                !TryDecodeTicket(Encoding.UTF8.GetString(payloadBytes), out accountId, out sessionId,
                    out sessionVersion, out int ticketServerId, out long issuedAt, out long expiresAt,
                    out string nonce))
            {
                accountId = 0;
                sessionId = null;
                sessionVersion = 0;
                return false;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long maximumLifetime = (long)ticketLifetime.TotalSeconds;
            if (ticketServerId != serverId || issuedAt > now + 5 || expiresAt <= now ||
                expiresAt - issuedAt != maximumLifetime || issuedAt < processStartedAtUnixSeconds)
            {
                accountId = 0;
                sessionId = null;
                sessionVersion = 0;
                return false;
            }

            CleanupExpiredEntries(now);
            if (!consumedNonces.TryAdd(nonce, expiresAt))
            {
                accountId = 0;
                sessionId = null;
                sessionVersion = 0;
                return false;
            }

            return true;
        }

        public bool TryValidateAccessToken(string accessToken, out long accountId, out string sessionId)
        {
            return TryValidateAccessToken(accessToken, out accountId, out sessionId, out _);
        }

        private bool TryValidateAccessToken(string accessToken, out long accountId, out string sessionId,
            out long sessionVersion)
        {
            accountId = 0;
            sessionId = null;
            sessionVersion = 0;
            if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 512) return false;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            CleanupExpiredEntries(now);
            string tokenHash = HashToken(accessToken);
            if (!accessSessions.TryGetValue(tokenHash, out LoginSession loginSession)) return false;
            if (loginSession.ExpiresAtUnixSeconds <= now)
            {
                accessSessions.TryRemove(tokenHash, out _);
                return false;
            }
            if (!currentSessions.TryGetValue(loginSession.AccountId, out LoginSession currentSession) ||
                !string.Equals(currentSession.SessionId, loginSession.SessionId, StringComparison.Ordinal) ||
                currentSession.SessionVersion != loginSession.SessionVersion)
            {
                return false;
            }

            accountId = loginSession.AccountId;
            sessionId = loginSession.SessionId;
            sessionVersion = loginSession.SessionVersion;
            return true;
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

        public bool TryApplySessionInvalidation(string requestBody)
        {
            if (string.IsNullOrWhiteSpace(requestBody) || requestBody.Length > 2048) return false;
            string[] parts = requestBody.Split('|');
            if (parts.Length != 6 ||
                !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long accountId) ||
                !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long sessionVersion) ||
                !long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestamp) ||
                accountId <= 0 || sessionVersion <= 0 || string.IsNullOrWhiteSpace(parts[2]) ||
                string.IsNullOrWhiteSpace(parts[4]) || !TryBase64UrlDecode(parts[5], out byte[] suppliedSignature))
            {
                return false;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (timestamp < now - 30 || timestamp > now + 30) return false;

            string unsignedBody = string.Join("|", parts[0], parts[1], parts[2], parts[3], parts[4]);
            byte[] expectedSignature = ComputeHmac(InvalidationPurpose + unsignedBody);
            if (suppliedSignature.Length != expectedSignature.Length ||
                !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            {
                return false;
            }

            string sessionId = parts[2];
            lock (gameSessionLock)
            {
                if (!TryAcceptGameSession(accountId, sessionId, sessionVersion)) return true;
                SessionMgr.Instance.KickSupersededSession(accountId, sessionId);
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

        private void NotifyGameServerSessionChanged(LoginSession session)
        {
            if (string.IsNullOrWhiteSpace(gameSessionNotifyUrl)) return;

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string unsignedBody = string.Join("|",
                session.AccountId.ToString(CultureInfo.InvariantCulture),
                session.SessionVersion.ToString(CultureInfo.InvariantCulture),
                session.SessionId,
                timestamp.ToString(CultureInfo.InvariantCulture),
                CreateRandomToken());
            string requestBody = unsignedBody + "|" + Base64UrlEncode(ComputeHmac(InvalidationPurpose + unsignedBody));

            try
            {
                using var content = new StringContent(requestBody, Encoding.UTF8, "text/plain");
                using HttpResponseMessage response = InternalHttpClient.PostAsync(gameSessionNotifyUrl, content)
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
