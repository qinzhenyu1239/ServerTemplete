using DotNetty.Codecs.Base64;
using DotNetty.Codecs.Http;
using DotNetty.Transport.Channels;
using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Entries.HttpControllers
{
    [HttpController]
    class HttpAuthController
    {
        private const int HEAD_SIZE = 14; // [Module2字节, 2字节Cmd, utag8字节, 2字节LogicServerId]

        private Dictionary<long, long> accountIdToOpenId = new Dictionary<long, long>();
        private Dictionary<long, long> openIdToAccountId = new Dictionary<long, long>();
        private string httpPlayerServerUrl = "http://127.0.0.1:6090/GetPlayerId";


        public HttpAuthController() {
            this.httpPlayerServerUrl = $"{ConfigurationManager.AppSettings["HttpAuthAndPlayerWebsite"]}GetPlayerId";
        }

        private Message DecodeMessage(byte[] data, ref long openId)
        {
            String base64 = Encoding.UTF8.GetString(data); // 将字节数组转换为字符串
            byte[] decodedBytes = Convert.FromBase64String(base64);
            data = decodedBytes;
            short module = UtilsHelper.ReadShortLE(data, 0); // LE的方式
            module = (short)(module & 0x00ff);
            short cmd = UtilsHelper.ReadShortLE(data, 2);
            openId = UtilsHelper.ReadLongLE(data, 4);
            int count = data.Length;
            Message msg = SerializerHelper.PbDecode(module, cmd, data, HEAD_SIZE, count - HEAD_SIZE);

            return msg;
        }

        private Message DecodeMessage(byte[] data) {
            String base64 = Encoding.UTF8.GetString(data); // 将字节数组转换为字符串
            byte[] decodedBytes = Convert.FromBase64String(base64);
            data = decodedBytes;
            short module = UtilsHelper.ReadShortLE(data, 0); // LE的方式
            module = (short)(module & 0x00ff);
            short cmd = UtilsHelper.ReadShortLE(data, 2);

            int count = data.Length;
            Message msg = SerializerHelper.PbDecode(module, cmd, data, HEAD_SIZE, count - HEAD_SIZE);

            return msg;
        }


        [HttpRequestMapping("/GetAccountId")]
        public object DoGetAccountIdAction(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes <= 0) {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = UtilsHelper.ReadLongLE(data, 0);
            long accountId = 0;
            if (this.openIdToAccountId.ContainsKey(openId)) {
                accountId = this.openIdToAccountId[openId];
            }
            
            byte[] body = new byte[8];
            UtilsHelper.WriteULongLE(body, 0, (ulong)accountId);
            return body;
        }


        [HttpRequestMapping("/ReqGuestLogin")]
        public object DoGuestLoginAction(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0) {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);
           
            Message msg = this.DecodeMessage(data);
            ResGuestLogin res = null;
            if (msg is ReqGuestLogin) {
                res = AuthModule.Instance.HandlerReqGuestLogin(null, msg as ReqGuestLogin);
                if (res.status == (int)Respones.OK) {
                    if (!this.accountIdToOpenId.ContainsKey(res.accountIdOrOpenId)) {
                        this.accountIdToOpenId.Add(res.accountIdOrOpenId, IdGenerator.GetNextId(1002));
                    }
                    
                    long openId = this.accountIdToOpenId[res.accountIdOrOpenId]; // 返回openId,通过accountId带回客户端;
                    if (!this.openIdToAccountId.ContainsKey(openId)) {
                        this.openIdToAccountId.Add(openId, res.accountIdOrOpenId);
                    }
                    
                    res.accountIdOrOpenId = openId;
                }
                
                return res;
            }

            res = new ResGuestLogin();
            res.status = (int)Respones.InvalidOpt;

            return res;
            // return HttpServerZoneModule.Instance.HandlePullServerZoneAction();
        }

        [HttpRequestMapping("/RegisterUser")]
        public object DoReqRegisterUserAction(IFullHttpRequest request) {
            if (request.Content.ReadableBytes == 0) {
                return null;
            }
            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);
            Message msg = this.DecodeMessage(data);
            if (msg is ReqRegisterUser) {
                ResRegisterUser res = AuthModule.Instance.HandlerReqRegisterUser(null, msg as ReqRegisterUser);
                return res;
            }
            return null;
        }

        [HttpRequestMapping("/ReqUserLogin")]
        public object DoUserLoginAction(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            Message msg = this.DecodeMessage(data);
            ResUserLogin res = null;
            if (msg is ReqUserLogin)
            {
                res = AuthModule.Instance.HandlerReqUserLogin(null, msg as ReqUserLogin);
                if (res.status == (int)Respones.OK)
                {
                    if (!this.accountIdToOpenId.ContainsKey(res.accountIdOrOpenId))
                    {
                        this.accountIdToOpenId.Add(res.accountIdOrOpenId, IdGenerator.GetNextId(1002));
                    }

                    long openId = this.accountIdToOpenId[res.accountIdOrOpenId]; // 返回openId,通过accountId带回客户端;
                    if (!this.openIdToAccountId.ContainsKey(openId))
                    {
                        this.openIdToAccountId.Add(openId, res.accountIdOrOpenId);
                    }

                    res.accountIdOrOpenId = openId;
                }

                return res;
            }

            res = new ResUserLogin();
            res.status = (int)Respones.InvalidOpt;

            return res;
        }

        [HttpRequestMapping("/ReqGuestUpgrade")]
        public async void DoReqGuestUpgrade(IChannelHandlerContext ctx, IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return;
            }
            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);
            
            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (msg is ReqGuestUpgrade)
            {
                long playerId = await HttpUtils.HttpPost(httpPlayerServerUrl, openId);
                if (playerId > 0) {
                    ResGuestUpgrade res = AuthModule.Instance.HandlerReqGuestUpgrade(null, msg as ReqGuestUpgrade, playerId);
                    HttpRouteDispatcher.Instance.SendHttpRespones(ctx, res);
                }
            }
            return;
        }
    }
}
