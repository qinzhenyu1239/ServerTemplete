using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace Game.Entries.Controllers
{
    [Controller]
    public sealed class RedDotController
    {
        [RequestMapping]
        public object Initialize(IdSession session, ReqRedDotInit request, long playerId) =>
            RedDotModule.Instance.Initialize(session, request, playerId);
        [RequestMapping]
        public object MarkViewed(IdSession session, ReqRedDotView request, long playerId) =>
            RedDotModule.Instance.MarkViewed(session, request, playerId);
    }
}
