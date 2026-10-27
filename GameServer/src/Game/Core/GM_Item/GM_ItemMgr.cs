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

        // Caller owns the transaction. Lock even missing rows before checking additions.
        public Dictionary<int, long> LockRewardQuantities(SqlSugarClient db, long playerId, IEnumerable<int> itemIds)
        {
            var result = new Dictionary<int, long>();
            foreach (int id in itemIds.Distinct().OrderBy(id => id))
            {
                db.Ado.ExecuteCommand("INSERT IGNORE INTO player_item(player_id,item_id,quantity) VALUES(@pid,@id,0)",
                    new { pid = playerId, id });
                result[id] = db.Ado.SqlQuery<long>("SELECT quantity FROM player_item WHERE player_id=@pid AND item_id=@id FOR UPDATE",
                    new { pid = playerId, id }).Single();
            }
            return result;
        }

        public void GrantLocked(SqlSugarClient db, long playerId, IReadOnlyDictionary<int, long> additions)
        {
            foreach (var pair in additions.OrderBy(p => p.Key))
            {
                if (pair.Value <= 0) throw new ArgumentOutOfRangeException(nameof(additions));
                int affected = db.Ado.ExecuteCommand("UPDATE player_item SET quantity=quantity+@amount " +
                    "WHERE player_id=@pid AND item_id=@id AND quantity>=0 AND quantity<=@maximum",
                    new { pid = playerId, id = pair.Key, amount = pair.Value, maximum = long.MaxValue - pair.Value });
                if (affected != 1) throw new InvalidOperationException("Reward quantity changed or overflowed.");
            }
        }

        public void SyncCommittedQuantities(long playerId, IReadOnlyDictionary<int, long> quantities)
        {
            var entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity?.uItem.quantities == null) return;
            foreach (var pair in quantities) entity.uItem.quantities[pair.Key] = pair.Value;
        }
    }
}
