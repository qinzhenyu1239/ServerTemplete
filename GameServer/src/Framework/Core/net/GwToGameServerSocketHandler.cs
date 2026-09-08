using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using System;
using System.IO;

namespace Framework.Core.Net
{
    public class GwToGameServerSocketHandler : ChannelHandlerAdapter
    {
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public override void ChannelActive(IChannelHandlerContext context)
        {
            IChannel channel = context.Channel;
            logger.Debug($"Gateway Enter To GameServer: {channel.RemoteAddress}");
            SessionMgr.Instance.GatewayConnectToServer(channel);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var buffer = message as IByteBuffer;
            IdSession session = SessionMgr.Instance.GatewaySession;
            MessageTrafficLogger.Received(session, buffer.Array, buffer.ArrayOffset, buffer.ReadableBytes);
            MessageDispatcher.Instance.OnGatewayToGameServerMsg(
                session, buffer.Array, buffer.ArrayOffset, buffer.ReadableBytes);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            IChannel channel = context.Channel;
            SessionMgr.Instance.GatewayDisconnectFromServer();
            logger.Debug($"Gateway Exit From GameServer: {channel.RemoteAddress}");
            channel.DisconnectAsync();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            IChannel channel = context.Channel;
            if (channel.Active || channel.Open) context.CloseAsync();
            if (exception is not IOException) logger.Debug("remote:" + channel.RemoteAddress, exception.Message);
        }
    }
}
