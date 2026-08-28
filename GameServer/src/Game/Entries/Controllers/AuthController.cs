using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace Game.Entries.Controllers
{

    public class AuthController
    {
        static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [RequestMapping]
        public object DoReqGuestLogin(IdSession s, ReqGuestLogin req, long utag) {
            return AuthModule.Instance.HandlerReqGuestLogin(s, req);
        }

        [RequestMapping]
        public object DoReqRegisterUser(IdSession s, ReqRegisterUser req, long utag)
        {
            return AuthModule.Instance.HandlerReqRegisterUser(s, req);
        }

        [RequestMapping]
        public object DoReqUserLogin(IdSession s, ReqUserLogin req, long utag)
        {
            return AuthModule.Instance.HandlerReqUserLogin(s, req);
        }

        [RequestMapping]
        public object DoReqGuestUpgrade(IdSession s, ReqGuestUpgrade req, long playerId)
        {
            return AuthModule.Instance.HandlerReqGuestUpgrade(s, req, playerId);
        }
    }

}


