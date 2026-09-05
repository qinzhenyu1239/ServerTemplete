using Game.Core.GM_Gacha;
using System.Collections.Generic;

namespace Game.Datas.GMEntities
{
    public struct ItemComponent
    {
        public Dictionary<int, long> quantities;

        public static void Init(GM_PlayerEntity entity) => Reload(entity);
        public static void Reset(GM_PlayerEntity entity) => Reload(entity);

        public static void Reload(GM_PlayerEntity entity)
        {
            GM_GachaMgr.Instance.ReloadItemComponent(entity);
        }
    }
}
