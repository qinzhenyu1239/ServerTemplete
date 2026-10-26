using Framework.Core.Net;
using Game.Datas.DBEntities;
using System.Collections.Generic;

namespace Game.Datas.GMEntities
{
    public sealed class RedDotComponent
    {
        public bool IsLoaded;
        public bool IsInitialized;
        public long StateVersion;
        public string ConfigVersion;
        public IdSession Session;
        public Dictionary<int, Playerreddot> States = new();
        public static void Init(GM_PlayerEntity entity) => Reset(entity);
        public static void Reset(GM_PlayerEntity entity) => entity.uRedDot = new RedDotComponent();
    }
}
