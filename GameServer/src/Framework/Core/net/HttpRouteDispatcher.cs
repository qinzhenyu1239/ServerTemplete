using DotNetty.Buffers;
using DotNetty.Codecs.Http;
using DotNetty.Common;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using Framework.Core.Serializer;
using Framework.Core.task;
using Framework.Core.Utils;
using Framework.Core.RateLimiting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Core.Net
{
    class HttpRouteDispatcher {
        static readonly ThreadLocalCache Cache = new ThreadLocalCache();

        sealed class ThreadLocalCache : FastThreadLocal<AsciiString>
        {
            protected override AsciiString GetInitialValue()
            {
                DateTime dateTime = DateTime.UtcNow;
                return AsciiString.Cached($"{dateTime.DayOfWeek}, {dateTime:dd MMM yyyy HH:mm:ss z}");
            }
        }

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static HttpRouteDispatcher Instance = new HttpRouteDispatcher();

        /** [uri, CmdExecutor] */
        private static Dictionary<string, CmdExecutor> HTTP_ROUTE_CMD_HANDLERS = new();


        // 头
        static readonly AsciiString ContentTypeEntity = HttpHeaderNames.ContentType;
        static readonly AsciiString ServerEntity = HttpHeaderNames.Server;
        static readonly AsciiString DateEntity = HttpHeaderNames.Date;
        static readonly AsciiString ContentLengthEntity = HttpHeaderNames.ContentLength;
        static readonly AsciiString TypePlain = AsciiString.Cached("text/plain");
        static readonly AsciiString TypeJson = AsciiString.Cached("application/json");
        static readonly AsciiString TypeBinary = AsciiString.Cached("application/octet-stream");
        // end

        // value
        volatile ICharSequence date = Cache.Value;
        static readonly AsciiString ServerName = AsciiString.Cached("Bycw");
        // end

        public void Init(params string[] allowedUris)
        {
            HashSet<string> allowedUriSet = allowedUris != null && allowedUris.Length > 0
                ? new HashSet<string>(allowedUris, StringComparer.Ordinal)
                : null;

            IEnumerable<Type> controllers = TypeScanner.ListTypesWithAttribute(typeof(HttpController));
            foreach (Type controller in controllers)
            {
                try
                {
                    MethodInfo[] methods = controller.GetMethods();
                    MethodInfo[] mappedMethods = methods
                        .Where(method =>
                        {
                            HttpRequestMapping mapping = method.GetCustomAttribute<HttpRequestMapping>();
                            return mapping != null &&
                                   (allowedUriSet == null || allowedUriSet.Contains(mapping.uri));
                        })
                        .ToArray();

                    if (mappedMethods.Length == 0)
                    {
                        continue;
                    }

                    object handler = Activator.CreateInstance(controller);

                    foreach (MethodInfo method in mappedMethods)
                    {
                        HttpRequestMapping mapperAttribute = method.GetCustomAttribute<HttpRequestMapping>();

                        string key = mapperAttribute.uri;
                        HTTP_ROUTE_CMD_HANDLERS.TryGetValue(key, out CmdExecutor cmdExecutor);
                        if (cmdExecutor != null)
                        {
                            logger.Warn($"Http Route[{key}] 重复注册处理器");
                            return;
                        }

                        cmdExecutor = CmdExecutor.Create(method, method.GetParameters().ToArray(), handler);
                        HTTP_ROUTE_CMD_HANDLERS.Add(key, cmdExecutor);
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e.Message);
                }
            }
        }

        private object[] ConvertToMethodParams(IChannelHandlerContext ctx, IHttpRequest request, ParameterInfo[] methodParams)
        {
            object[] result = new object[methodParams == null ? 0 : methodParams.Length];

            for (int i = 0; i < result.Length; i++)
            {
                Type param = methodParams[i].ParameterType;
                if (param.IsAssignableTo(typeof(IHttpRequest)))
                {
                    result[i] = request;
                }
                if (param.IsAssignableTo(typeof(IChannelHandlerContext)))
                {
                    result[i] = ctx;
                }
            }

            return result;
        }

        private object[] ConvertToMethodParams(IChannelHandlerContext ctx, IHttpRequest request, ParameterInfo[] methodParams, List<object> allContent)
        {
            object[] result = new object[methodParams == null ? 0 : methodParams.Length];

            for (int i = 0; i < result.Length; i++)
            {
                Type param = methodParams[i].ParameterType;
                if (param.IsAssignableTo(typeof(IHttpRequest)))
                {
                    result[i] = request;
                }
                if (param.IsAssignableTo(typeof(List<object>))) {
                    result[i] = allContent;
                }
                if (param.IsAssignableTo(typeof(IChannelHandlerContext)))
                {
                    result[i] = ctx;
                }
            }

            return result;
        }

        void WriteResponse(IChannelHandlerContext ctx, IByteBuffer buf, ICharSequence contentType, ICharSequence contentLength)
        {

            // Build the response object.
            var response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, buf, false);
            HttpHeaders headers = response.Headers;

            // 设置允许跨域 CORS 响应头
            response.Headers.Set(HttpHeaderNames.AccessControlAllowOrigin, "*");
            response.Headers.Set(HttpHeaderNames.AccessControlAllowMethods, "GET, POST, PUT, DELETE, OPTIONS");
            response.Headers.Set(HttpHeaderNames.AccessControlAllowHeaders, "Content-Type, Accept");
            response.Headers.Set(HttpHeaderNames.AccessControlMaxAge, "3600");
            // end

            headers.Set(ContentTypeEntity, contentType); // header ---> value;
            headers.Set(ServerEntity, ServerName);
            headers.Set(DateEntity, this.date);
            headers.Set(ContentLengthEntity, contentLength);

            // Close the non-keep-alive connection after the write operation is done.
            MessageTrafficLogger.HttpSent(
                ctx.Channel.RemoteAddress?.ToString() ?? "unknown",
                HttpResponseStatus.OK.Code, CopyReadableBytes(buf), 0, buf.ReadableBytes);
            ctx.WriteAndFlushAsync(response);
            ctx.CloseAsync();
        }

        public void SendHttpRespones(IChannelHandlerContext ctx, object response) {
            if (response != null)
            {
                if (response is string)
                {
                    byte[] jsonBody = Encoding.UTF8.GetBytes(response as string);
                    this.WriteResponse(ctx, Unpooled.WrappedBuffer(jsonBody), TypeJson, AsciiString.Cached($"{jsonBody.Length}"));
                    return;
                }
                else if (response is Message) // 消息命令，需要[module, cmd, utag, logicServerId, body]
                {
                    byte[] body = MessagePusher.PackMessage((Message)response, 0, 0);
                    if (body != null)
                    {
                        this.WriteResponse(ctx, Unpooled.WrappedBuffer(body), TypeBinary, AsciiString.Cached($"{body.Length}"));
                        return;
                    }
                }
                else if (response is byte[])
                {
                    byte[] body = response as byte[];
                    if (body != null)
                    {
                        this.WriteResponse(ctx, Unpooled.WrappedBuffer(body), TypeBinary, AsciiString.Cached($"{body.Length}"));
                        return;
                    }
                }
            }

            // 不回应，关闭
            ctx.CloseAsync();
        }

        private static async Task<object> AwaitInvocationResult(object invocationResult)
        {
            if (invocationResult is not Task task)
            {
                return invocationResult;
            }

            await task.ConfigureAwait(false);
            PropertyInfo resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task);
        }

        public void RemoveRoutes(params string[] uris)
        {
            if (uris == null) return;
            foreach (string uri in uris)
            {
                if (!string.IsNullOrWhiteSpace(uri)) HTTP_ROUTE_CMD_HANDLERS.Remove(uri);
            }
        }

        private bool TryHandleBuiltInRequest(IChannelHandlerContext ctx, string uri)
        {
            if (!string.Equals(uri, "/favicon.ico", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var response = new DefaultFullHttpResponse(
                HttpVersion.Http11,
                HttpResponseStatus.NoContent,
                Unpooled.Empty,
                false);
            response.Headers.Set(ContentLengthEntity, AsciiString.Cached("0"));

            MessageTrafficLogger.HttpSent(
                ctx.Channel.RemoteAddress?.ToString() ?? "unknown",
                HttpResponseStatus.NoContent.Code);
            ctx.WriteAndFlushAsync(response);
            ctx.CloseAsync();
            return true;
        }

        private bool TryHandleRateLimitedRequest(IChannelHandlerContext ctx, string uri)
        {
            string ip = RequestRateLimiter.GetIp(ctx.Channel);
            if (HttpRequestRateLimiter.Instance.TryAcquire(ip, GetRateLimitKind(uri))) return false;

            var response = new DefaultFullHttpResponse(
                HttpVersion.Http11, HttpResponseStatus.TooManyRequests, Unpooled.Empty, false);
            response.Headers.Set(ContentLengthEntity, AsciiString.Cached("0"));
            response.Headers.Set(HttpHeaderNames.RetryAfter, AsciiString.Cached("1"));
            MessageTrafficLogger.HttpSent(
                ctx.Channel.RemoteAddress?.ToString() ?? "unknown",
                HttpResponseStatus.TooManyRequests.Code);
            ctx.WriteAndFlushAsync(response);
            ctx.CloseAsync();
            return true;
        }

        internal static HttpRequestRateLimitKind GetRateLimitKind(string uri)
        {
            if (string.Equals(uri, "/ReqIssueGameTicket", StringComparison.Ordinal))
            {
                return HttpRequestRateLimitKind.Ticket;
            }

            return uri switch
            {
                "/ReqGuestLogin" or
                "/ReqRegisterUser" or
                "/ReqUserLogin" or
                "/ReqGuestUpgrade" => HttpRequestRateLimitKind.Credential,
                _ => HttpRequestRateLimitKind.Default
            };
        }

        public async Task OnHttpRequrestProcess(IChannelHandlerContext ctx, IFullHttpRequest request)
        {
            string uri = request.Uri;
            if (TryHandleBuiltInRequest(ctx, uri))
            {
                return;
            }
            if (TryHandleRateLimitedRequest(ctx, uri)) return;

            HTTP_ROUTE_CMD_HANDLERS.TryGetValue(uri, out CmdExecutor cmdExecutor);
            if (cmdExecutor == null)
            {
                logger.Warn($"Http uri executor missed, {uri}");

                var response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.NotFound, Unpooled.Empty, false);
                MessageTrafficLogger.HttpSent(
                    ctx.Channel.RemoteAddress?.ToString() ?? "unknown",
                    HttpResponseStatus.NotFound.Code);
                await ctx.WriteAndFlushAsync(response);
                await ctx.CloseAsync();

                return;
            }
            
            if (cmdExecutor != null)
            {
                object[] @params = ConvertToMethodParams(ctx, request, cmdExecutor.@params);
                object response = await AwaitInvocationResult(cmdExecutor.method.Invoke(cmdExecutor.handler, @params));
                if (response != null) {
                    this.SendHttpRespones(ctx, response);
                }
                
                // 优化走线程池处理
                // TaskWorkerPool.Instance.AddTask(HttpMessageTask.Create(cmdExecutor.handler, cmdExecutor.method, @params, ctx));
            }
           
        }

        public async Task OnHttpRequrestProcess(IChannelHandlerContext ctx, IHttpRequest request, List<object> allContent) {
            string uri = request.Uri;
            if (TryHandleBuiltInRequest(ctx, uri))
            {
                return;
            }
            if (TryHandleRateLimitedRequest(ctx, uri)) return;

            HTTP_ROUTE_CMD_HANDLERS.TryGetValue(uri, out CmdExecutor cmdExecutor);
            if (cmdExecutor == null)
            {
                logger.Warn($"Http uri executor missed, {uri}");

                var response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.NotFound, Unpooled.Empty, false);
                MessageTrafficLogger.HttpSent(
                    ctx.Channel.RemoteAddress?.ToString() ?? "unknown",
                    HttpResponseStatus.NotFound.Code);
                await ctx.WriteAndFlushAsync(response);
                await ctx.CloseAsync();

                return;
            }

            if (cmdExecutor != null) {
                object[] @params = ConvertToMethodParams(ctx,request, cmdExecutor.@params);
                object response = await AwaitInvocationResult(cmdExecutor.method.Invoke(cmdExecutor.handler, @params));
                if (response != null) {
                    this.SendHttpRespones(ctx, response);
                }
                // 优化走线程池处理
                // TaskWorkerPool.Instance.AddTask(HttpMessageTask.Create(cmdExecutor.handler, cmdExecutor.method, @params, ctx));
            }
        }

        private static byte[] CopyReadableBytes(IByteBuffer buffer)
        {
            byte[] bytes = new byte[buffer.ReadableBytes];
            buffer.GetBytes(buffer.ReaderIndex, bytes);
            return bytes;
        }
    }
}
