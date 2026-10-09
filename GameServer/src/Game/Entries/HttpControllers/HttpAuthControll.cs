using DotNetty.Codecs.Http;
using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Game.Entries.HttpControllers
{
    [HttpController]
    class HttpAuthController
    {
        private static Message DecodeMessage(byte[] data)
        {
            string base64 = Encoding.UTF8.GetString(data);
            byte[] packet = Convert.FromBase64String(base64);
            if (packet.Length < ProtocolPacketLayout.HeaderLength) return null;
            short module = (short)(UtilsHelper.ReadShortLE(packet, 0) & 0x00ff);
            short cmd = UtilsHelper.ReadShortLE(packet, 2);
            return SerializerHelper.PbDecode(module, cmd, packet, ProtocolPacketLayout.HeaderLength,
                packet.Length - ProtocolPacketLayout.HeaderLength);
        }

        private static Message ReadRequest(IFullHttpRequest request)
        {
            if (request?.Content == null || request.Content.ReadableBytes <= 0) return null;
            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);
            try { return DecodeMessage(data); }
            catch { return null; }
        }

        [HttpRequestMapping("/ReqGuestLogin")]
        public object DoGuestLoginAction(IFullHttpRequest request) =>
            AuthModule.Instance.HandlerReqGuestLogin(ReadRequest(request) as ReqGuestLogin);

        [HttpRequestMapping("/ReqRegisterUser")]
        public object DoReqRegisterUserAction(IFullHttpRequest request) =>
            AuthModule.Instance.HandlerReqRegisterUser(ReadRequest(request) as ReqRegisterUser);

        [HttpRequestMapping("/ReqUserLogin")]
        public async Task<object> DoUserLoginAction(IFullHttpRequest request)
            => await AuthModule.Instance.HandlerReqUserLoginAsync(ReadRequest(request) as ReqUserLogin);

        [HttpRequestMapping("/ReqUserLogout")]
        public async Task<object> DoUserLogout(IFullHttpRequest request) =>
            await AuthModule.Instance.HandlerReqUserLogoutAsync(ReadRequest(request) as ReqUserLogout);

        [HttpRequestMapping("/ReqGuestUpgrade")]
        public object DoReqGuestUpgrade(IFullHttpRequest request) =>
            AuthModule.Instance.HandlerReqGuestUpgrade(ReadRequest(request) as ReqGuestUpgrade);

        [HttpRequestMapping("/ReqIssueGameTicket")]
        public object DoReqIssueGameTicket(IFullHttpRequest request) =>
            AuthModule.Instance.HandlerReqIssueGameTicket(ReadRequest(request) as ReqIssueGameTicket);
    }
}
