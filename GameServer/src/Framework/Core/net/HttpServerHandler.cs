namespace Framework.Core.Net {
    
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Microsoft.IdentityModel.Tokens;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection.Metadata;

    sealed class HttpServerHandler : ChannelHandlerAdapter {

        private static readonly AttributeKey<List<object>> RequestKey = AttributeKey<List<object>>.ValueOf("Request");
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public override void ChannelRead(IChannelHandlerContext ctx, object message) {
            if (message is IFullHttpRequest) {
                try
                {
                    this.Process(ctx, message as IFullHttpRequest);
                }
                finally
                {
                    ReferenceCountUtil.Release(message);
                }
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
                    try
                    {
                        this.Process(ctx, allConent[0] as IHttpRequest, allConent);
                    }
                    finally
                    {
                        for (int i = 0; i < allConent.Count; i++)
                        {
                            ReferenceCountUtil.Release(allConent[i]);
                        }
                    }
                    ctx.Channel.GetAttribute(RequestKey).Set(null);
                }
            }
            else
            {
                ctx.FireChannelRead(message);
            }
        }

        private void Process(IChannelHandlerContext ctx, IFullHttpRequest request) {
            HttpRouteDispatcher.Instance.OnHttpRequrestProcess(ctx, request);
        }

        void Process(IChannelHandlerContext ctx, IHttpRequest request, List<object> allContent) {
            HttpRouteDispatcher.Instance.OnHttpRequrestProcess(ctx, request, allContent);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => context.CloseAsync();

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();
    }
}

