using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Caches
{
    internal sealed class PlayerItemCache
    {
        public static readonly PlayerItemCache Instance = new PlayerItemCache();
        private PlayerItemCache() { }

        public Dictionary<int, long> GetQuantities(long playerId, params int[] itemIds)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                if (entity.uItem.quantities == null) ItemComponent.Reload(entity);
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

        public Dictionary<int, long> LockPaymentItems(
            SqlSugarClient db, long playerId, int ticketItemId, int diamondItemId)
        {
            db.Ado.ExecuteCommand(
                "INSERT IGNORE INTO player_item (player_id, item_id, quantity) VALUES " +
                "(@playerId, @ticketItemId, 0), (@playerId, @diamondItemId, 0)",
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@ticketItemId", ticketItemId),
                new SugarParameter("@diamondItemId", diamondItemId));

            return db.Ado.SqlQuery<Player_item>(
                    "SELECT id, player_id, item_id, quantity FROM player_item " +
                    "WHERE player_id=@playerId AND item_id IN (@ticketItemId, @diamondItemId) " +
                    "ORDER BY item_id FOR UPDATE",
                    new SugarParameter("@playerId", playerId),
                    new SugarParameter("@ticketItemId", ticketItemId),
                    new SugarParameter("@diamondItemId", diamondItemId))
                .ToDictionary(item => item.item_id, item => item.quantity);
        }

        public void Deduct(SqlSugarClient db, long playerId, int itemId, long quantity)
        {
            if (quantity <= 0) return;
            int affected = db.Ado.ExecuteCommand(
                "UPDATE player_item SET quantity=quantity-@quantity " +
                "WHERE player_id=@playerId AND item_id=@itemId AND quantity>=@quantity",
                new SugarParameter("@quantity", quantity),
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@itemId", itemId));
            if (affected != 1)
                throw new InvalidOperationException($"Failed to deduct item {itemId} for player {playerId}.");
        }

        public void Add(SqlSugarClient db, long playerId, int itemId, int quantity)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            int affected = db.Ado.ExecuteCommand(
                "INSERT INTO player_item (player_id, item_id, quantity) VALUES (@playerId, @itemId, @quantity) " +
                "ON DUPLICATE KEY UPDATE quantity=quantity+VALUES(quantity)",
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@itemId", itemId),
                new SugarParameter("@quantity", quantity));
            if (affected <= 0)
                throw new InvalidOperationException($"Failed to add item {itemId} for player {playerId}.");
        }

        public void ApplyCommitted(
            long playerId,
            int ticketItemId,
            long tickets,
            int diamondItemId,
            long diamonds,
            IReadOnlyDictionary<int, long> fragmentChanges)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null) return;
            entity.uItem.quantities ??= new Dictionary<int, long>();
            entity.uItem.quantities[ticketItemId] = tickets;
            entity.uItem.quantities[diamondItemId] = diamonds;
            foreach (var change in fragmentChanges)
            {
                entity.uItem.quantities.TryGetValue(change.Key, out long current);
                entity.uItem.quantities[change.Key] = current + change.Value;
            }
        }

        public void Invalidate(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null) entity.uItem.quantities = null;
        }
    }

    internal sealed class PlayerCharacterCache
    {
        public static readonly PlayerCharacterCache Instance = new PlayerCharacterCache();
        private PlayerCharacterCache() { }

        public List<int> GetOwnedCharacterIds(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                if (entity.uCharacter.characterIds == null) CharacterComponent.Reload(entity);
                return entity.uCharacter.characterIds.ToList();
            }

            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            return db.Queryable<Player_character>()
                .Where(character => character.player_id == playerId)
                .Select(character => character.character_id)
                .ToList();
        }

        public bool InsertIfNew(SqlSugarClient db, long instanceId, long playerId, int characterId)
        {
            int affected = db.Ado.ExecuteCommand(
                "INSERT IGNORE INTO player_character (id, player_id, character_id, star) " +
                "VALUES (@id, @playerId, @characterId, 0)",
                new SugarParameter("@id", instanceId),
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@characterId", characterId));
            return affected == 1;
        }

        public void ApplyCommitted(long playerId, IReadOnlyCollection<int> characterIds)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null) return;
            entity.uCharacter.characterIds ??= new HashSet<int>();
            foreach (int characterId in characterIds) entity.uCharacter.characterIds.Add(characterId);
        }

        public void Replace(long playerId, IReadOnlyCollection<int> characterIds)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null) entity.uCharacter.characterIds = new HashSet<int>(characterIds);
        }

        public void Invalidate(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null) entity.uCharacter.characterIds = null;
        }
    }

    internal sealed class PlayerGachaStateCache
    {
        public static readonly PlayerGachaStateCache Instance = new PlayerGachaStateCache();
        private PlayerGachaStateCache() { }

        public Player_gacha_state Get(long playerId, int poolId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                GachaComponent.EnsureLoaded(entity);
                return entity.uGacha.poolStates.TryGetValue(poolId, out Player_gacha_state state)
                    ? state
                    : null;
            }

            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            return db.Queryable<Player_gacha_state>()
                .Where(state => state.player_id == playerId && state.pool_id == poolId)
                .First();
        }

        public Player_gacha_state Lock(SqlSugarClient db, long playerId, int poolId)
        {
            db.Ado.ExecuteCommand(
                "INSERT IGNORE INTO player_gacha_state " +
                "(player_id, pool_id, small_pity_count, big_pity_count, total_draw_count) " +
                "VALUES (@playerId, @poolId, 0, 0, 0)",
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@poolId", poolId));

            Player_gacha_state state = db.Ado.SqlQuery<Player_gacha_state>(
                    "SELECT player_id, pool_id, small_pity_count, big_pity_count, total_draw_count " +
                    "FROM player_gacha_state WHERE player_id=@playerId AND pool_id=@poolId FOR UPDATE",
                    new SugarParameter("@playerId", playerId),
                    new SugarParameter("@poolId", poolId))
                .SingleOrDefault();
            return state ?? throw new InvalidOperationException("Failed to initialize gacha state.");
        }

        public void Update(SqlSugarClient db, Player_gacha_state state)
        {
            int affected = db.Ado.ExecuteCommand(
                "UPDATE player_gacha_state SET small_pity_count=@smallPity, " +
                "big_pity_count=@bigPity, total_draw_count=@totalDraws " +
                "WHERE player_id=@playerId AND pool_id=@poolId",
                new SugarParameter("@smallPity", state.small_pity_count),
                new SugarParameter("@bigPity", state.big_pity_count),
                new SugarParameter("@totalDraws", state.total_draw_count),
                new SugarParameter("@playerId", state.player_id),
                new SugarParameter("@poolId", state.pool_id));
            if (affected != 1) throw new InvalidOperationException("Failed to update gacha state.");
        }

        public void Put(long playerId, Player_gacha_state state)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null) return;
            entity.uGacha.poolStates ??= new Dictionary<int, Player_gacha_state>();
            entity.uGacha.poolStates[state.pool_id] = state;
            entity.uGacha.isLoaded = true;
        }

        public void Invalidate(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null) GachaComponent.Invalidate(entity);
        }
    }

    internal sealed class PlayerGachaRecordCache
    {
        public static readonly PlayerGachaRecordCache Instance = new PlayerGachaRecordCache();
        private PlayerGachaRecordCache() { }

        public void Insert(SqlSugarClient db, Player_gacha_record record)
        {
            int affected = db.Insertable(record).ExecuteCommand();
            if (affected != 1) throw new InvalidOperationException("Failed to insert gacha record.");
        }
    }
}
