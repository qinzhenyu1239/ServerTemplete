using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace Game.Entries.Controllers
{
    [Controller]
    public sealed class GameAuthController
    {
        [RequestMapping]
        public object DoReqGameAuthenticate(IdSession session, ReqGameAuthenticate request) =>
            AuthSessionModule.Instance.HandlerReqGameAuthenticate(session, request);
    }
}
