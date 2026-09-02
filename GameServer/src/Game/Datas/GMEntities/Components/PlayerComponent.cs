using Framework.Core.Net;

namespace Game.Datas.GMEntities {
    public struct PlayerComponent {
        public Game.Datas.DBEntities.Account accountInfo;
        public Game.Datas.DBEntities.Player playerInfo;
        public IdSession s;
    }

}

