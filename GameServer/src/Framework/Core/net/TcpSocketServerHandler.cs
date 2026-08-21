using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using System;
using System.IO;
using System.Text;

namespace Framework.Core.Net{

    public class TcpSocketServerHandler : ChannelHandlerAdapter {

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private bool isGateway = false;

        public TcpSocketServerHandler(bool isGateway = false) : base() {
            this.isGateway = isGateway;
        }

        public override void ChannelActive(IChannelHandlerContext context) { // 连接进来的
            IChannel channel = context.Channel;

            IdSession s = SessionMgr.Instance.CreateIdSession(channel);

            // MessageDispatcher.Instance.OnClientEnter(s);
            if (this.isGateway) {
                MessageDispatcher.Instance.OnClientEnterToGateway(s, false);
            }
            else {
                MessageDispatcher.Instance.OnClientEnter(s, false);
            }
        }

        public override void ChannelRead(IChannelHandlerContext context, object message) { // 有数据读的时候 
            var msg = message as IByteBuffer;
            IChannel channel = context.Channel;
            IdSession s = SessionMgr.Instance.GetSessionBy(channel);
            // this.logger.Debug($"{msg.ReadableBytes} : {msg.ArrayOffset}"); // ReadableBytes = 12 : 2 Array 包含了头;
            // byte[] cmdData = new byte[msg.ReadableBytes]; // 内存池来处理,后续考虑优化;
            // Array.Copy(msg.Array, msg.ArrayOffset, cmdData, 0, msg.ReadableBytes);
            // MessageDispatcher.Instance.OnClientMsg(s, msg.Array, msg.ArrayOffset, msg.ReadableBytes);
            if (this.isGateway) {
                MessageDispatcher.Instance.OnClientMsgToGateway(s, msg.Array, msg.ArrayOffset, msg.ReadableBytes);
            }
            else {
                MessageDispatcher.Instance.OnClientMsg(s, msg.Array, msg.ArrayOffset, msg.ReadableBytes);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush(); // 数据读取完的一个处理

        public override void ChannelInactive(IChannelHandlerContext context) { // 关闭
            IChannel channel = context.Channel;
            IdSession s = SessionMgr.Instance.GetSessionBy(channel);
            // MessageDispatcher.Instance.OnClientExit(s);
            if (this.isGateway) {
                MessageDispatcher.Instance.OnClientExitFromGateway(s);
            }
            else {
                MessageDispatcher.Instance.OnClientExit(s);
            }
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