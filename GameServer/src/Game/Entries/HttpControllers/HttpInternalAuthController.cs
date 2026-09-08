using DotNetty.Codecs.Http;
using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;
using System;
using System.Text;

namespace Game.Entries.HttpControllers
{
    [HttpController]
    internal sealed class HttpInternalAuthController
    {
        [HttpRequestMapping("/Internal/InvalidateAccountSession")]
        public object InvalidateAccountSession(IFullHttpRequest request)
            => GameAuthModule.Instance.HandlerSessionInvalidation(
                ReadRequest(request) as ReqInvalidateAccountSession);

        private static Message DecodeMessage(byte[] data)
        {
            string base64 = Encoding.UTF8.GetString(data);
            byte[] packet = Convert.FromBase64String(base64);
            if (packet.Length < ProtocolPacketLayout.HeaderLength) return null;
            short module = (short)(UtilsHelper.ReadShortLE(packet, ProtocolPacketLayout.ModuleOffset) & 0x00ff);
            short cmd = UtilsHelper.ReadShortLE(packet, ProtocolPacketLayout.CommandOffset);
            return SerializerHelper.PbDecode(module, cmd, packet, ProtocolPacketLayout.HeaderLength,
                packet.Length - ProtocolPacketLayout.HeaderLength);
        }

        private static Message ReadRequest(IFullHttpRequest request)
        {
            if (request?.Content == null || request.Content.ReadableBytes <= 0 ||
                request.Content.ReadableBytes > 4096) return null;
            byte[] body = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(body);
            try { return DecodeMessage(body); }
            catch { return null; }
        }
    }
}
