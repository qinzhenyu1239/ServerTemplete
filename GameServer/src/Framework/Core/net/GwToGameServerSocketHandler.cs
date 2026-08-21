using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using System;
using System.Configuration;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Framework.Core.Net{

    public class GwToGameServerSocketHandler : ChannelHandlerAdapter {

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private bool AsAuthServer = false;

        public GwToGameServerSocketHandler() : base() {
            this.AsAuthServer = bool.Parse(ConfigurationManager.AppSettings["AsAuthServer"]);
        }

        public override void ChannelActive(IChannelHandlerContext context) { // 网关连接进来的
            IChannel channel = context.Channel;

            this.logger.Debug($"Gateway Enter To GameServer: {channel.RemoteAddress.ToString()}");
            if (this.AsAuthServer) {
                IdSession s = SessionMgr.Instance.CreateIdSession(channel);
                MessageDispatcher.Instance.OnClientEnter(s);
            }
            else {
                SessionMgr.Instance.GatewayConnectToServer(channel);
            }
                
        }

        public override void ChannelRead(IChannelHandlerContext context, object message) { // 有数据读的时候 
            var msg = message as IByteBuffer;
            IChannel channel = context.Channel;
            IdSession s = null;

            if (this.AsAuthServer) {
                s = SessionMgr.Instance.GetSessionBy(channel);
            }
            else {
                s = SessionMgr.Instance.GatewaySession; // 网关的Session
            }
            MessageDispatcher.Instance.OnGatewayToGameServerMsg(s, msg.Array, msg.ArrayOffset, msg.ReadableBytes);
            // this.logger.Debug($"{msg.ReadableBytes} : {msg.ArrayOffset}"); // ReadableBytes = 12 : 2 Array 包含了头;
            // byte[] cmdData = new byte[msg.ReadableBytes]; // 内存池来处理,后续考虑优化;
            // Array.Copy(msg.Array, msg.ArrayOffset, cmdData, 0, msg.ReadableBytes);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush(); // 数据读取完的一个处理

        public override void ChannelInactive(IChannelHandlerContext context) { // 网关断线
            IChannel channel = context.Channel;
            if (this.AsAuthServer)
            {
                IdSession s = SessionMgr.Instance.GetSessionBy(channel);
                MessageDispatcher.Instance.OnClientExit(s);
            }
            else {
                SessionMgr.Instance.GatewayDisconnectFromServer();
            }
            
            this.logger.Debug($"Gateway Exit From GameServer: {channel.RemoteAddress.ToString()}");
            channel.DisconnectAsync();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception e) { // 有异常的时候
            IChannel channel = context.Channel;
            if (channel.Active || channel.Open) {
                context.CloseAsync();
            }

            if (!(e is IOException))
            {
                this.logger.Debug("remote:" + channel.RemoteAddress, e.Message);
            }
        }
    }

}