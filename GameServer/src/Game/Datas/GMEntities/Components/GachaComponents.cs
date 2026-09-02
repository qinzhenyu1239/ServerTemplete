using Game.Core.GM_Gacha;
using Game.Datas.DBEntities;
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

    public struct CharacterComponent
    {
        public HashSet<int> characterIds;

        public static void Init(GM_PlayerEntity entity) => Reload(entity);
        public static void Reset(GM_PlayerEntity entity) => Reload(entity);

        public static void Reload(GM_PlayerEntity entity)
        {
            GM_GachaMgr.Instance.ReloadCharacterComponent(entity);
        }
    }

    public struct GachaComponent
    {
        public bool isLoaded;
        public Dictionary<int, Player_gacha_state> poolStates;

        public static void Init(GM_PlayerEntity entity) => Invalidate(entity);
        public static void Reset(GM_PlayerEntity entity) => Invalidate(entity);

        public static void Invalidate(GM_PlayerEntity entity)
        {
            entity.uGacha.isLoaded = false;
            entity.uGacha.poolStates = new Dictionary<int, Player_gacha_state>();
        }

        public static void EnsureLoaded(GM_PlayerEntity entity)
        {
            if (entity.uGacha.isLoaded) return;
            GM_GachaMgr.Instance.ReloadGachaComponent(entity);
        }
    }
}
