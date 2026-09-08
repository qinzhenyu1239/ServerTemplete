namespace Framework.Core.Net {
    
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Microsoft.IdentityModel.Tokens;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection.Metadata;
    using System.Threading.Tasks;

    sealed class HttpServerHandler : ChannelHandlerAdapter {

        private static readonly AttributeKey<List<object>> RequestKey = AttributeKey<List<object>>.ValueOf("Request");
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public override void ChannelRead(IChannelHandlerContext ctx, object message) {
            if (message is IFullHttpRequest) {
                _ = this.ProcessAndReleaseAsync(ctx, message as IFullHttpRequest, message).ContinueWith(
                    task => this.HandleAsyncFailure(ctx, task), TaskScheduler.Default);
            }
            else if (message is IHttpRequest request)
            {
                List<object> allConent = new List<object>();
                ctx.Channel.GetAttribute(RequestKey).Set(allConent);
                allConent.Add(request);
                /*try
                {
                    this.Process(ctx, request);
                }
                finally
                {
                    ReferenceCountUtil.Release(message);
                }*/

            }
            else if (message is IHttpContent)
            {
                List<object> allConent = ctx.Channel.GetAttribute(RequestKey).Get();
                allConent.Add(message);

                if (message is ILastHttpContent lastContent)
                {
                    ctx.Channel.GetAttribute(RequestKey).Set(null);
                    _ = this.ProcessAndReleaseAsync(ctx, allConent[0] as IHttpRequest, allConent).ContinueWith(
                        task => this.HandleAsyncFailure(ctx, task), TaskScheduler.Default);
                }
            }
            else
            {
                ctx.FireChannelRead(message);
            }
        }

        private async Task ProcessAndReleaseAsync(IChannelHandlerContext ctx, IFullHttpRequest request, object message) {
            try
            {
                MessageTrafficLogger.HttpReceived(
                    ctx.Channel.RemoteAddress?.ToString() ?? "unknown",
                    request.Method.ToString(), request.Uri, request.Content.ReadableBytes);
                await HttpRouteDispatcher.Instance.OnHttpRequrestProcess(ctx, request);
            }
            finally
            {
                ReferenceCountUtil.Release(message);
            }
        }

        private async Task ProcessAndReleaseAsync(IChannelHandlerContext ctx, IHttpRequest request, List<object> allContent) {
            try
            {
                int contentLength = 0;
                foreach (object item in allContent)
                {
                    if (item is IHttpContent content) contentLength += content.Content.ReadableBytes;
                }
                MessageTrafficLogger.HttpReceived(
                    ctx.Channel.RemoteAddress?.ToString() ?? "unknown",
                    request.Method.ToString(), request.Uri, contentLength);
                await HttpRouteDispatcher.Instance.OnHttpRequrestProcess(ctx, request, allContent);
            }
            finally
            {
                foreach (object item in allContent) ReferenceCountUtil.Release(item);
            }
        }

        private void HandleAsyncFailure(IChannelHandlerContext ctx, Task task)
        {
            if (!task.IsFaulted) return;
            logger.Error(task.Exception?.GetBaseException(), "HTTP request processing failed");
            ctx.CloseAsync();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => context.CloseAsync();

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();
    }
}

