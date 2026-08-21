// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Framework.Core.Net
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    

    using static DotNetty.Codecs.Http.HttpVersion;
    using static DotNetty.Codecs.Http.HttpResponseStatus;
    using System.IO;

    public sealed class WebSocketServerHandler : SimpleChannelInboundHandler<object>
    {
        const string WebsocketPath = "/websocket";

        WebSocketServerHandshaker handshaker;

        NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private bool isGateway = false;
        public WebSocketServerHandler(bool isGateway = false) : base() {
            this.isGateway = isGateway;
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
        {
            if (msg is IFullHttpRequest request)
            {
                this.HandleHttpRequest(ctx, request);
            }
            else if (msg is WebSocketFrame frame)
            {
                this.HandleWebSocketFrame(ctx, frame);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        private void HandleHttpRequest(IChannelHandlerContext ctx, IFullHttpRequest req)
        {
            // Handle a bad request.
            if (!req.Result.IsSuccess)
            {
                SendHttpResponse(ctx, req, new DefaultFullHttpResponse(Http11, BadRequest));
                return;
            }

            // Allow only GET methods.
            if (!Equals(req.Method, HttpMethod.Get))
            {
                SendHttpResponse(ctx, req, new DefaultFullHttpResponse(Http11, Forbidden));
                return;
            }

            // Send the demo page and favicon.ico
            if ("/".Equals(req.Uri))
            {
                IByteBuffer content = WebSocketServerBenchmarkPage.GetContent(GetWebSocketLocation(req));
                var res = new DefaultFullHttpResponse(Http11, OK, content);

                res.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
                HttpUtil.SetContentLength(res, content.ReadableBytes);

                SendHttpResponse(ctx, req, res);
                return;
            }
            if ("/favicon.ico".Equals(req.Uri))
            {
                var res = new DefaultFullHttpResponse(Http11, NotFound);
                SendHttpResponse(ctx, req, res);
                return;
            }

            // Handshake
            var wsFactory = new WebSocketServerHandshakerFactory(
                GetWebSocketLocation(req), null, true, 5 * 1024 * 1024);
            this.handshaker = wsFactory.NewHandshaker(req);
            if (this.handshaker == null)
            {
                WebSocketServerHandshakerFactory.SendUnsupportedVersionResponse(ctx.Channel);
            }
            else
            {
                this.handshaker.HandshakeAsync(ctx.Channel, req);
            }

            // 接入新用户进来
            IdSession s = SessionMgr.Instance.CreateIdSession(ctx.Channel, true);
            // MessageDispatcher.Instance.OnClientEnter(s, true);
            if (this.isGateway) {
                MessageDispatcher.Instance.OnClientEnterToGateway(s, true);
            }
            else {
                MessageDispatcher.Instance.OnClientEnter(s, true);
            }
        }

        void HandleWebSocketFrame(IChannelHandlerContext ctx, WebSocketFrame frame)
        {
            // Check for closing frame
            if (frame is CloseWebSocketFrame)
            {
                IdSession s = SessionMgr.Instance.GetSessionBy(ctx.Channel);
                // MessageDispatcher.Instance.OnClientExit(s);
                if (this.isGateway) {
                    MessageDispatcher.Instance.OnClientExitFromGateway(s);
                }
                else {
                    MessageDispatcher.Instance.OnClientExit(s);
                }
                this.handshaker.CloseAsync(ctx.Channel, (CloseWebSocketFrame)frame.Retain());
                return;
            }

            if (frame is PingWebSocketFrame)
            {
                ctx.WriteAsync(new PongWebSocketFrame((IByteBuffer)frame.Content.Retain()));
                return;
            }

            if (frame is TextWebSocketFrame) // Text模式
            {
                // 将数据包丢给我们的上层来进行处理;
                // IdSession s = SessionMgr.Instance.GetSessionBy(ctx.Channel);
                // MessageDispatcher.Instance.OnClientMsg(s, Encoding.UTF8.GetBytes((frame as TextWebSocketFrame).Text()));
                string warningMsg = "Server WebSocket: Use arraybuffer Model";
                this.logger.Warn(warningMsg);

                TextWebSocketFrame f = new TextWebSocketFrame(warningMsg);
                ctx.WriteAsync(f);
                // end

                return;
            }

            if (frame is BinaryWebSocketFrame) // arraybuffer;
            {
                // 将数据包丢给我们的上层来进行处理;
                IdSession s = SessionMgr.Instance.GetSessionBy(ctx.Channel);
                IByteBuffer msg = frame.Content;
                // byte[] cmdData = new byte[msg.ReadableBytes]; // 内存池来处理,后续考虑优化;
                // Array.Copy(msg.Array, msg.ArrayOffset, cmdData, 0, msg.ReadableBytes);
                // MessageDispatcher.Instance.OnClientMsg(s, msg.Array, msg.ArrayOffset, msg.ReadableBytes);
                if (this.isGateway) {
                    MessageDispatcher.Instance.OnClientMsgToGateway(s, msg.Array, msg.ArrayOffset, msg.ReadableBytes);
                }
                else {
                    MessageDispatcher.Instance.OnClientMsg(s, msg.Array, msg.ArrayOffset, msg.ReadableBytes);
                }
                // end

                // ctx.WriteAsync(frame.Retain());
            }
        }

        static void SendHttpResponse(IChannelHandlerContext ctx, IFullHttpRequest req, IFullHttpResponse res)
        {
            // Generate an error page if response getStatus code is not OK (200).
            if (res.Status.Code != 200)
            {
                IByteBuffer buf = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(res.Status.ToString()));
                res.Content.WriteBytes(buf);
                buf.Release();
                HttpUtil.SetContentLength(res, res.Content.ReadableBytes);
            }

            // Send the response and close the connection if necessary.
            Task task = ctx.Channel.WriteAndFlushAsync(res);
            if (!HttpUtil.IsKeepAlive(req) || res.Status.Code != 200)
            {
                task.ContinueWith((t, c) => ((IChannelHandlerContext)c).CloseAsync(), 
                    ctx, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception e)
        {
            IChannel channel = context.Channel;
            if (channel.Active || channel.Open)
            {
                context.CloseAsync();
            }

            if (!(e is IOException))
            {
                this.logger.Debug("remote:" + channel.RemoteAddress, e.Message);
            }
        }

        static string GetWebSocketLocation(IFullHttpRequest req, bool IsSsl = false)
        {
            bool result = req.Headers.TryGet(HttpHeaderNames.Host, out ICharSequence value);
            // this.logger.Debug("Host header does not exist.");

            string location= value.ToString() + WebsocketPath;

            if (IsSsl) {
                return "wss://" + location;
            }
            else
            {
                return "ws://" + location;
            }
        }
    }
}
