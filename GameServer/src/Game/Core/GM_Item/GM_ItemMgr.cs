using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using SqlSugar;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.GM_Item
{
    /// <summary>
    /// Owns the persistence-backed loading of the player's shared item component.
    /// Gameplay managers remain responsible for their own transactional item mutations.
    /// </summary>
    public sealed class GM_ItemMgr
    {
        public static readonly GM_ItemMgr Instance = new GM_ItemMgr();

        private GM_ItemMgr() { }

        public void EnsureLoaded(GM_PlayerEntity entity)
        {
            if (entity.uItem.quantities == null)
                ReloadItemComponent(entity);
        }

        public void ReloadItemComponent(GM_PlayerEntity entity)
        {
            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            entity.uItem.quantities = db.Queryable<Player_item>()
                .Where(item => item.player_id == entity.uPlayer.playerInfo.id)
                .ToList()
                .ToDictionary(item => item.item_id, item => item.quantity);
        }

        public Dictionary<int, long> GetQuantities(long playerId, params int[] itemIds)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                EnsureLoaded(entity);
                return itemIds.ToDictionary(
                    itemId => itemId,
                    itemId => entity.uItem.quantities.TryGetValue(itemId, out long value) ? value : 0);
            }

            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            Dictionary<int, long> stored = db.Queryable<Player_item>()
                .Where(item => item.player_id == playerId && itemIds.Contains(item.item_id))
                .ToList()
                .ToDictionary(item => item.item_id, item => item.quantity);
            return itemIds.ToDictionary(
                itemId => itemId,
                itemId => stored.TryGetValue(itemId, out long value) ? value : 0);
        }

        public long GetQuantity(long playerId, int itemId) =>
            GetQuantities(playerId, itemId)[itemId];
    }
}
