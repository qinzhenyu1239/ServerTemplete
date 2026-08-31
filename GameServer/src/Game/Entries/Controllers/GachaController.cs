using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace Game.Entries.Controllers
{
    [Controller]
    public sealed class GachaController
    {
        [RequestMapping]
        public object DoGetPoolInfo(IdSession session, ReqGetGachaPoolInfo request, long playerId) =>
            GachaModule.Instance.HandlerGetPoolInfo(session, request, playerId);

        [RequestMapping]
        public object DoDraw(IdSession session, ReqGachaDraw request, long playerId) =>
            GachaModule.Instance.HandlerDraw(session, request, playerId);
    }
}
