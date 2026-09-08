using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace Game.Entries.Controllers
{
    [Controller]
    public sealed class FormationController
    {
        [RequestMapping]
        public object DoGetPlayerFormations(IdSession session, ReqGetPlayerFormations request, long playerId) =>
            FormationModule.Instance.HandlerGet(session, request, playerId);

        [RequestMapping]
        public object DoSavePlayerFormation(IdSession session, ReqSavePlayerFormation request, long playerId) =>
            FormationModule.Instance.HandlerSave(session, request, playerId);
    }
}
