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
        public object DoGuestLoginAction(IFullHttpRequest request)
        {
            if (ReadRequest(request) is not ReqGuestLogin req)
                return new ResGuestLogin { status = (int)Respones.InvalidParams };
            ResGuestLogin response = AuthModule.Instance.HandlerReqGuestLogin(null, req);
            if (response.status == (int)Respones.OK)
                response.accessToken = AuthSessionModule.Instance.CreateLoginSession(response.accountIdOrOpenId);
            return response;
        }

        [HttpRequestMapping("/ReqRegisterUser")]
        public object DoReqRegisterUserAction(IFullHttpRequest request)
        {
            return ReadRequest(request) is ReqRegisterUser req
                ? AuthModule.Instance.HandlerReqRegisterUser(null, req)
                : new ResRegisterUser { status = (int)Respones.InvalidParams };
        }

        [HttpRequestMapping("/ReqUserLogin")]
        public async Task<object> DoUserLoginAction(IFullHttpRequest request)
        {
            if (ReadRequest(request) is not ReqUserLogin req)
                return new ResUserLogin { status = (int)Respones.InvalidParams };
            ResUserLogin response = await AutoLoginModule.Instance.LoginOrRegisterAsync(req);
            if (response.status == (int)Respones.OK)
                response.accessToken = AuthSessionModule.Instance.CreateLoginSession(response.accountIdOrOpenId);
            return response;
        }

        [HttpRequestMapping("/ReqGuestUpgrade")]
        public object DoReqGuestUpgrade(IFullHttpRequest request)
        {
            if (ReadRequest(request) is not ReqGuestUpgrade req)
                return new ResGuestUpgrade { status = (int)Respones.InvalidParams };
            if (!AuthSessionModule.Instance.TryValidateAccessToken(req.accessToken, out long accountId, out _))
                return new ResGuestUpgrade { status = (int)Respones.AccountIsNotLogin };
            return AuthAccountUpgradeModule.Instance.Upgrade(req, accountId);
        }

        [HttpRequestMapping("/ReqIssueGameTicket")]
        public object DoReqIssueGameTicket(IFullHttpRequest request)
        {
            if (ReadRequest(request) is not ReqIssueGameTicket req || req.serverId <= 0)
                return new ResIssueGameTicket { status = (int)Respones.InvalidParams };
            if (!AuthSessionModule.Instance.TryIssueGameTicket(
                    req.accessToken, req.serverId, out string ticket, out long expiresAt))
                return new ResIssueGameTicket { status = (int)Respones.AccountIsNotLogin };
            return new ResIssueGameTicket
            {
                status = (int)Respones.OK,
                gameTicket = ticket,
                expiresAtUnixSeconds = expiresAt
            };
        }
    }
}
