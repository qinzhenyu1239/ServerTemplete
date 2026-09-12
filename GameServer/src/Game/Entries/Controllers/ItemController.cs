using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace Game.Entries.Controllers
{
    [Controller]
    public sealed class ItemController
    {
        [RequestMapping]
        public object DoGetPlayerItems(
            IdSession session, ReqGetPlayerItems request, long playerId) =>
            ItemModule.Instance.HandlerGetAll(session, request, playerId);
    }
}
