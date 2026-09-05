using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace Game.Entries.Controllers
{
    [Controller]
    public sealed class CharacterStarUpController
    {
        [RequestMapping]
        public object DoGetCharacterInfo(IdSession session, ReqGetCharacterInfo request, long playerId) =>
            CharacterStarUpModule.Instance.HandlerGetCharacterInfo(session, request, playerId);

        [RequestMapping]
        public object DoCharacterStarUp(IdSession session, ReqCharacterStarUp request, long playerId) =>
            CharacterStarUpModule.Instance.HandlerCharacterStarUp(session, request, playerId);
    }
}
