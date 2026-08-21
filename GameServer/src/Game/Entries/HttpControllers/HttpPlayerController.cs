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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Entries.HttpControllers
{
    [HttpController]
    class HttpPlayerController
    {
        private const int HEAD_SIZE = 14; // [Module2字节, 2字节Cmd, utag8字节, 2字节LogicServerId]
        private string httpAuthServerUrl = "http://127.0.0.1:6090/GetAccountId";

        private Dictionary<long, long> playerIdToOpenId = new Dictionary<long, long>();
        private Dictionary<long, long> openIdToPlayerId = new Dictionary<long, long>();


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


        [HttpRequestMapping("/GetPlayerId")]
        public object DoGetPlayerIdAction(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes <= 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = UtilsHelper.ReadLongLE(data, 0);
            long playerId = 0;
            if (this.openIdToPlayerId.ContainsKey(openId))
            {
                playerId = this.openIdToPlayerId[openId];
            }

            byte[] body = new byte[8];
            UtilsHelper.WriteULongLE(body, 0, (ulong)playerId);
            return body;
        }


        [HttpRequestMapping("/ReqSelectPlayer")]
        public async void DoReqSelectPlayerAction(IChannelHandlerContext ctx, IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            ResSelectPlayer res = null;
            if (msg is ReqSelectPlayer)
            {
                long accountId = await HttpUtils.HttpPost(httpAuthServerUrl, openId);
                if (accountId <= 0) {
                    res = new ResSelectPlayer();
                    res.status = (int)Respones.InvalidOpt;
                    HttpRouteDispatcher.Instance.SendHttpRespones(ctx, res);
                    return;
                }

                res = PlayerModule.Instance.HandlerHttpReqSelectPlayer((ReqSelectPlayer)msg, accountId);
                if (res.status == (int)Respones.OK) {
                    if (!this.openIdToPlayerId.ContainsKey(openId)) {
                        this.openIdToPlayerId[openId] = res.playerId;
                    }

                    if (!this.playerIdToOpenId.ContainsKey(res.playerId)) {
                        this.playerIdToOpenId[res.playerId] = openId;
                    }
                }
                HttpRouteDispatcher.Instance.SendHttpRespones(ctx, res);
            }
        }

      

        [HttpRequestMapping("/ReqPullingPlayerData")]
        public async void DoReqPullingPlayerDataAction(IChannelHandlerContext ctx, IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);

            


            ResPullingPlayerData res = null;
            if (msg is ReqPullingPlayerData)
            {

                long accountId = await HttpUtils.HttpPost(httpAuthServerUrl, openId);
                if (accountId <= 0)
                {
                    res = new ResPullingPlayerData();
                    res.status = (int)Respones.InvalidOpt;
                    HttpRouteDispatcher.Instance.SendHttpRespones(ctx, res);
                    return;
                }
                res = PlayerModule.Instance.HandlerHttpReqPullingPlayerData(msg as ReqPullingPlayerData, accountId);
                if (res.status == (int)Respones.OK)
                {
                    if (!this.openIdToPlayerId.ContainsKey(openId))
                    {
                        this.openIdToPlayerId[openId] = res.playerId;
                    }

                    if (!this.playerIdToOpenId.ContainsKey(res.playerId))
                    {
                        this.playerIdToOpenId[res.playerId] = openId;
                    }
                }
                HttpRouteDispatcher.Instance.SendHttpRespones(ctx, res);
            }       
        }

        [HttpRequestMapping("/ReqRecvLoginBonues")]
        public object DoReqRecvLoginBonues(IFullHttpRequest request) {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId)) {
                return null;
            }

            if (msg is ReqRecvLoginBonues) {
                return PlayerModule.Instance.HandlerRecvLoginBonues(null, (ReqRecvLoginBonues)msg, this.openIdToPlayerId[openId]);
            }
            
            return null;
        }

        [HttpRequestMapping("/ReqPullingBonuesList")]
        public object DoReqPullingBonuesList(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }

            if (msg is ReqPullingBonuesList) {
                return PlayerModule.Instance.HandlerReqPullingBonuesList(null, (ReqPullingBonuesList)msg, this.openIdToPlayerId[openId]);
            }
            return null;
        }

        [HttpRequestMapping("/ReqRecvBonues")]
        public object DoReqRecvBonues(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }
            
            if (msg is ReqRecvBonues)
            {
                return PlayerModule.Instance.HandlerReqRecvBonues(null, (ReqRecvBonues)msg, this.openIdToPlayerId[openId]);
            }
            return null;
        }

        [HttpRequestMapping("/ReqPullingTaskList")]
        public object DoReqPullingTaskList(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }

            if (msg is ReqPullingTaskList) {
                return PlayerModule.Instance.HandlerReqPullingTaskList(null, (ReqPullingTaskList)msg, this.openIdToPlayerId[openId]);
            }

            return null;
        }

        [HttpRequestMapping("/ReqTestGetGoods")]
        public object DoReqTestGetBonuesGoods(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }

            if (msg is ReqTestGetGoods)
            {
                return PlayerModule.Instance.HandlerReqTestGetGoods(null, (ReqTestGetGoods)msg, this.openIdToPlayerId[openId]);
            }

            return null;
        }

        [HttpRequestMapping("/ReqTestUpdateGooods")]
        public object DoReqTestUpdateGoods(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }
            
            if (msg is ReqTestUpdateGooods)
            {
                return PlayerModule.Instance.HandlerReqTestUpdatesGoods(null, (ReqTestUpdateGooods)msg, this.openIdToPlayerId[openId]);
            }

            return null;
        }

        [HttpRequestMapping("/ReqPullingMailMsg")]
        public object DoReqPullingMailMsg(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }
            
            if (msg is ReqPullingMailMsg)
            {
                return PlayerModule.Instance.HandlerReqPullingMailMsg(null, (ReqPullingMailMsg)msg, this.openIdToPlayerId[openId]);
            }

            return null;
        }

        [HttpRequestMapping("/ReqUpdateMailMsg")]
        public object DoReqUpdateMailMsg(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }
            
            if (msg is ReqUpdateMailMsg)
            {
                return PlayerModule.Instance.HandlerReqUpdateMailMsg(null, (ReqUpdateMailMsg)msg, this.openIdToPlayerId[openId]);
            }

            return null;
        }

        [HttpRequestMapping("/ReqPullingRank")]
        public object DoReqPullingRank(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }
            
            if (msg is ReqPullingRank)
            {
                return PlayerModule.Instance.HandlerReqPullingRank(null, (ReqPullingRank)msg, this.openIdToPlayerId[openId]);
            }

            return null;
        }

        [HttpRequestMapping("/ReqPullingPackData")]
        public object DoReqPullingPackData(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }
            
            if (msg is ReqPullingPackData)
            {
                return PlayerModule.Instance.HandlerReqPullingPackData(null, (ReqPullingPackData)msg, this.openIdToPlayerId[openId]);
            }

            return null;
        }

        [HttpRequestMapping("/ReqExchangeProduct")]
        public object DoReqExchangeProduct(IFullHttpRequest request)
        {
            if (request.Content.ReadableBytes == 0)
            {
                return null;
            }

            byte[] data = new byte[request.Content.ReadableBytes];
            request.Content.ReadBytes(data);

            long openId = 0;
            Message msg = this.DecodeMessage(data, ref openId);
            if (!this.openIdToPlayerId.ContainsKey(openId))
            {
                return null;
            }
            
            if (msg is ReqExchangeProduct)
            {
                return PlayerModule.Instance.HandlerReqExchangeProduct(null, (ReqExchangeProduct)msg, this.openIdToPlayerId[openId]);
            }

            return null;
        }
    }
}
