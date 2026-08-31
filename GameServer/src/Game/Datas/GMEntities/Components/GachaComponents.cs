using Game.Core.Db;
using Game.Datas.DBEntities;
using System.Collections.Generic;
using System.Linq;

namespace Game.Datas.GMEntities
{
    public struct ItemComponent
    {
        public Dictionary<int, long> quantities;

        public static void Init(GM_PlayerEntity entity) => Reload(entity);
        public static void Reset(GM_PlayerEntity entity) => Reload(entity);

        public static void Reload(GM_PlayerEntity entity)
        {
            entity.uItem.quantities = DBService.Instance.GetGameInstance()
                .Queryable<Player_item>()
                .Where(item => item.player_id == entity.uPlayer.playerInfo.id)
                .ToList()
                .ToDictionary(item => item.item_id, item => item.quantity);
        }
    }

    public struct CharacterComponent
    {
        public HashSet<int> characterIds;

        public static void Init(GM_PlayerEntity entity) => Reload(entity);
        public static void Reset(GM_PlayerEntity entity) => Reload(entity);

        public static void Reload(GM_PlayerEntity entity)
        {
            entity.uCharacter.characterIds = new HashSet<int>(DBService.Instance.GetGameInstance()
                .Queryable<Player_character>()
                .Where(character => character.player_id == entity.uPlayer.playerInfo.id)
                .Select(character => character.character_id)
                .ToList());
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
            entity.uGacha.poolStates = DBService.Instance.GetGameInstance()
                .Queryable<Player_gacha_state>()
                .Where(state => state.player_id == entity.uPlayer.playerInfo.id)
                .ToList()
                .ToDictionary(state => state.pool_id, state => state);
            entity.uGacha.isLoaded = true;
        }
    }
}
