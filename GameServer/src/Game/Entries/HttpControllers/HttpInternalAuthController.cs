using DotNetty.Codecs.Http;
using Framework.Core.Utils;
using Game.Entries.Modules;
using System.Text;

namespace Game.Entries.HttpControllers
{
    [HttpController]
    internal sealed class HttpInternalAuthController
    {
        [HttpRequestMapping("/Internal/InvalidateAccountSession")]
        public object InvalidateAccountSession(IFullHttpRequest request)
        {
            if (request?.Content == null || request.Content.ReadableBytes <= 0 ||
                request.Content.ReadableBytes > 2048)
            {
                return "INVALID";
            }

            byte[] body = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(body);
            string requestBody = Encoding.UTF8.GetString(body);
            return AuthSessionModule.Instance.TryApplySessionInvalidation(requestBody) ? "OK" : "INVALID";
        }
    }
}
