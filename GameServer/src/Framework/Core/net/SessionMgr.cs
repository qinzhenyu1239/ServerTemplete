using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using System.Net;
using System.Threading;

namespace Framework.Core.Net
{
    public class SessionMgr {
        public static SessionMgr Instance = new SessionMgr();

        
        private AttributeKey<IdSession> SESSION_KEY = AttributeKey<IdSession>.ValueOf("session");
        private long autoId = 0;

        public IdSession GatewaySession = null;

        public void Init() {
            this.autoId = 0;
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
            session.playerId = 0;

            long id = Interlocked.Increment(ref autoId);
            session.distributeKey = id;

            session.client = channel;
            session.isWebSocket = isWebSocket;

            // channel --->±£¥Ê¡À session;
            if (channel != null) {
                IAttribute<IdSession> sessionAttr = channel.GetAttribute(SESSION_KEY);
                sessionAttr.CompareAndSet(null, session);
            }
            
            return session;
        }

        public IdSession GetSessionBy(IChannel channel)
        {
            IAttribute<IdSession> sessionAttr = channel.GetAttribute(SESSION_KEY);
            return sessionAttr.Get();
        }

    }
}

