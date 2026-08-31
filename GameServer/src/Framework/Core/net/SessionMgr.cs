using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using System;
using System.Configuration;
using System.Net;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Framework.Core.Net
{
    public class SessionMgr {
        public static SessionMgr Instance = new SessionMgr();

        
        private AttributeKey<IdSession> SESSION_KEY = AttributeKey<IdSession>.ValueOf("session");
        private long autoId = 0;
        private readonly ConcurrentDictionary<long, IdSession> accountSessions = new();
        private readonly ConcurrentDictionary<IdSession, byte> closeAfterResponseSessions = new();

        public IdSession GatewaySession = null;

        public void Init() {
            this.autoId = 0;
            this.accountSessions.Clear();
        }

        public void SetSessionChannel(IdSession s, IChannel channel) {
            s.client = channel;
            IAttribute<IdSession> sessionAttr = channel.GetAttribute(SESSION_KEY);
            sessionAttr.CompareAndSet(null, s);
        }

        public void GatewayConnectToServer(IChannel channel) {
            if (this.GatewaySession == null) {
                this.GatewaySession = this.CreateIdSession(channel, false);
            }
            else {
                this.SetSessionChannel(this.GatewaySession, channel);
            }
        }

        public void GatewayDisconnectFromServer() {
            this.GatewaySession.client = null;
        }

        public IdSession CreateIdSession(IChannel channel, bool isWebSocket = false) {
            IdSession session = new IdSession();
            session.accountId = 0;
            session.authSessionId = null;
            session.playerId = 0;

            long id = Interlocked.Increment(ref autoId);
            session.distributeKey = id;

            session.client = channel;
            session.isWebSocket = isWebSocket;

            // channel --->������ session;
            if (channel != null) {
                IAttribute<IdSession> sessionAttr = channel.GetAttribute(SESSION_KEY);
                sessionAttr.CompareAndSet(null, session);
            }

            if (channel != null && string.Equals(
                    ConfigurationManager.AppSettings["ServerRole"], "Game", StringComparison.OrdinalIgnoreCase))
                CloseUnauthenticatedClientAfterTimeoutAsync(session);
            
            return session;
        }

        public IdSession GetSessionBy(IChannel channel)
        {
            IAttribute<IdSession> sessionAttr = channel.GetAttribute(SESSION_KEY);
            return sessionAttr.Get();
        }

        public void BindAuthenticatedAccount(IdSession session, long accountId, string authSessionId)
        {
            if (session == null || accountId <= 0 || string.IsNullOrWhiteSpace(authSessionId)) return;
            session.accountId = accountId;
            session.authSessionId = authSessionId;
            accountSessions.AddOrUpdate(accountId, session, (_, previous) =>
            {
                if (!ReferenceEquals(previous, session)) previous.client?.CloseAsync();
                return session;
            });
        }

        public void UnbindAuthenticatedAccount(IdSession session)
        {
            if (session == null) return;
            closeAfterResponseSessions.TryRemove(session, out _);
            if (session.accountId <= 0) return;
            if (accountSessions.TryGetValue(session.accountId, out IdSession current) && ReferenceEquals(current, session))
                accountSessions.TryRemove(session.accountId, out _);
        }

        public void CloseAfterNextResponse(IdSession session)
        {
            if (session != null) closeAfterResponseSessions[session] = 0;
        }

        public bool ConsumeCloseAfterNextResponse(IdSession session)
        {
            return session != null && closeAfterResponseSessions.TryRemove(session, out _);
        }

        private async void CloseUnauthenticatedClientAfterTimeoutAsync(IdSession session)
        {
            int timeoutSeconds = int.TryParse(
                ConfigurationManager.AppSettings["GameAuthenticationTimeoutSeconds"],
                out int configuredSeconds)
                ? Math.Max(1, configuredSeconds)
                : 10;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                if (session?.accountId > 0) return;

                IChannel channel = session?.client;
                if (channel != null && (channel.Active || channel.Open))
                    await channel.CloseAsync();
            }
            catch
            {
                // The channel may already have been closed and disposed.
            }
        }

        public void KickSupersededSession(long accountId, string currentAuthSessionId)
        {
            if (accountSessions.TryGetValue(accountId, out IdSession session) &&
                !string.Equals(session.authSessionId, currentAuthSessionId, System.StringComparison.Ordinal))
                session.client?.CloseAsync();
        }

    }
}

