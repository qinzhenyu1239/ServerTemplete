using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;
using NLog.LayoutRenderers;

namespace Game.Entries.Controllers
{

    [Controller]
    public class PlayerController
    {
        static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [RequestMapping]
        public object DoReqPullingPlayerData(IdSession s, ReqPullingPlayerData req, long accountId) {
            
            PlayerModule.Instance.HandlerReqPullingPlayerData(s, req, accountId);
            return null;
        }

        [RequestMapping]
        public object DoReqSelectPlayer(IdSession s, ReqSelectPlayer req, long accountId)
        {

            PlayerModule.Instance.HandlerReqSelectPlayer(s, req, accountId);
            return null;
        }

        [RequestMapping]
        public object DoReqRecvLoginBonues(IdSession s, ReqRecvLoginBonues req, long playerId)
        {

            return PlayerModule.Instance.HandlerRecvLoginBonues(s, req, playerId);
        }

        [RequestMapping]
        public object DoReqPullingBonuesList(IdSession s, ReqPullingBonuesList req, long playerId)
        {
            return PlayerModule.Instance.HandlerReqPullingBonuesList(s, req, playerId);
        }

        [RequestMapping]
        public object DoReqRecvBonues(IdSession s, ReqRecvBonues req, long playerId)
        {
            return PlayerModule.Instance.HandlerReqRecvBonues(s, req, playerId);
        }


        [RequestMapping]
        public object DoReqPullingTaskList(IdSession s, ReqPullingTaskList req, long playerId)
        {
            return PlayerModule.Instance.HandlerReqPullingTaskList(s, req, playerId);
        }

        [RequestMapping]
        public object DoReqTestGetBonuesGoods(IdSession s, ReqTestGetGoods req, long playerId) {
            return PlayerModule.Instance.HandlerReqTestGetGoods(s, req, playerId);
        }

        [RequestMapping]
        public object DoReqTestUpdateGoods(IdSession s, ReqTestUpdateGooods req, long playerId)
        {
            return PlayerModule.Instance.HandlerReqTestUpdatesGoods(s, req, playerId);
        }

        [RequestMapping]
        public object DoReqPullingMailMsg(IdSession s, ReqPullingMailMsg req, long playerId) {
            return PlayerModule.Instance.HandlerReqPullingMailMsg(s, req, playerId);
        }

        [RequestMapping]
        public object DoReqUpdateMailMsg(IdSession s, ReqUpdateMailMsg req, long playerId)
        {
            return PlayerModule.Instance.HandlerReqUpdateMailMsg(s, req, playerId);
        }


        [RequestMapping]
        public object DoReqPullingRank(IdSession s, ReqPullingRank req, long playerId) {
            return PlayerModule.Instance.HandlerReqPullingRank(s, req, playerId);
        }

        [RequestMapping]
        public object DoReqPullingPackData(IdSession s, ReqPullingPackData req, long playerId) {
            return PlayerModule.Instance.HandlerReqPullingPackData(s, req, playerId);
        }

        [RequestMapping]
        public object DoReqExchangeProduct(IdSession s, ReqExchangeProduct req, long playerId)
        {
            return PlayerModule.Instance.HandlerReqExchangeProduct(s, req, playerId);
        }

        [RequestMapping]
        public object DoReqEnterLogicServer(IdSession s, ReqEnterLogicServer req, long playerId)
        {
            PlayerModule.Instance.HandlerReqEnterLogicServer(s, req, playerId);
            return null;
        }
    }

}


