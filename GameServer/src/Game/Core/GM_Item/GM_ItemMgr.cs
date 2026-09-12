using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Framework.Core.Concurrency;
using SqlSugar;
using System;
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
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

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

        public ItemInventoryResult GetAllItems(long playerId)
        {
            using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
            lock (playerLock.SyncRoot)
            {
                return GetAllItemsLocked(playerId);
            }
        }

        private ItemInventoryResult GetAllItemsLocked(long playerId)
        {
            try
            {
                IReadOnlyDictionary<int, long> quantities = ReadAllQuantities(playerId);
                return new ItemInventoryResult
                {
                    Status = ItemInventoryStatus.Success,
                    Items = quantities
                        .Where(item => item.Value > 0)
                        .OrderBy(item => item.Key)
                        .Select(item => new PlayerItemSnapshot
                        {
                            ItemId = item.Key,
                            Quantity = item.Value
                        })
                        .ToList()
                };
            }
            catch (Exception exception)
            {
                InvalidateItemComponent(playerId);
                logger.Error(exception, $"Failed to load inventory for player {playerId}.");
                return new ItemInventoryResult { Status = ItemInventoryStatus.Failed };
            }
        }

        private IReadOnlyDictionary<int, long> ReadAllQuantities(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                EnsureLoaded(entity);
                return new Dictionary<int, long>(entity.uItem.quantities);
            }

            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            return db.Queryable<Player_item>()
                .Where(item => item.player_id == playerId)
                .ToList()
                .ToDictionary(item => item.item_id, item => item.quantity);
        }

        private static void InvalidateItemComponent(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null) entity.uItem.quantities = null;
        }
    }
}
