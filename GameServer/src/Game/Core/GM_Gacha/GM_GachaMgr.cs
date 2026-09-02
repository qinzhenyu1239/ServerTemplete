using Framework.Core.Utils;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.GM_Gacha
{
    /// <summary>
    /// Reusable gacha domain service. Owns payment, reward, pity and transaction rules.
    /// Owns its database statements and synchronizes committed data to player components.
    /// </summary>
    public sealed class GM_GachaMgr
    {
        public static readonly GM_GachaMgr Instance = new GM_GachaMgr();
        private readonly ConcurrentDictionary<long, object> playerLocks = new ConcurrentDictionary<long, object>();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private GM_GachaMgr() { }

        public ResGetGachaPoolInfo GetPoolInfo(long playerId, GachaPoolDefinition pool)
        {
            try
            {
                Player_gacha_state state = GetGachaState(playerId, pool.PoolId) ??
                                           NewState(playerId, pool.PoolId);
                Dictionary<int, long> quantities = GetQuantities(
                    playerId, pool.TicketItemId, pool.DiamondItemId);
                long ticketQuantity = GetQuantity(quantities, pool.TicketItemId);
                long diamondQuantity = GetQuantity(quantities, pool.DiamondItemId);
                List<int> ownedCharacterIds = GetOwnedCharacterIds(playerId);

                ApplyCommittedItems(
                    playerId, pool.TicketItemId, ticketQuantity,
                    pool.DiamondItemId, diamondQuantity, new Dictionary<int, long>());
                PutGachaState(playerId, state);
                ReplaceCharacters(playerId, ownedCharacterIds);

                return new ResGetGachaPoolInfo
                {
                    status = (int)Respones.OK,
                    poolId = pool.PoolId,
                    smallPityCount = state.small_pity_count,
                    smallPityMax = pool.SmallPityCount,
                    bigPityCount = state.big_pity_count,
                    bigPityMax = pool.BigPityCount,
                    totalDrawCount = state.total_draw_count,
                    ticketItemId = pool.TicketItemId,
                    ticketQuantity = ticketQuantity,
                    diamondItemId = pool.DiamondItemId,
                    diamondQuantity = diamondQuantity,
                    diamondCostPerDraw = pool.DiamondCostPerDraw,
                    ownedCharacterIds = ownedCharacterIds
                };
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Failed to query gacha pool {pool.PoolId} for player {playerId}.");
                InvalidateComponents(playerId);
                return new ResGetGachaPoolInfo
                {
                    status = (int)Respones.GachaFailed,
                    errorMessage = "Failed to query gacha pool information."
                };
            }
        }

        public ResGachaDraw Draw(long playerId, GachaPoolDefinition pool, int drawCount)
        {
            lock (playerLocks.GetOrAdd(playerId, _ => new object()))
            {
                return DrawLocked(playerId, pool, drawCount);
            }
        }

        private ResGachaDraw DrawLocked(long playerId, GachaPoolDefinition pool, int drawCount)
        {
            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            try
            {
                db.Ado.BeginTran();
                Player_gacha_state state = LockGachaState(db, playerId, pool.PoolId);
                Dictionary<int, long> quantities = LockPaymentItems(
                    db, playerId, pool.TicketItemId, pool.DiamondItemId);

                long ticketQuantity = Math.Max(0, GetQuantity(quantities, pool.TicketItemId));
                long diamondQuantity = Math.Max(0, GetQuantity(quantities, pool.DiamondItemId));
                int usedTickets = (int)Math.Min(ticketQuantity, drawCount);
                int usedDiamonds = checked((drawCount - usedTickets) * pool.DiamondCostPerDraw);
                if (diamondQuantity < usedDiamonds)
                {
                    db.Ado.RollbackTran();
                    return new ResGachaDraw
                    {
                        status = (int)Respones.GachaResourceNotEnough,
                        errorMessage = "Gacha currency is not enough."
                    };
                }

                DeductItem(db, playerId, pool.TicketItemId, usedTickets);
                DeductItem(db, playerId, pool.DiamondItemId, usedDiamonds);

                long batchId = IdGenerator.GetNextId();
                var results = new List<GachaResultItem>(drawCount);
                var fragmentChanges = new Dictionary<int, long>();
                var newCharacters = new HashSet<int>();

                for (int index = 1; index <= drawCount; index++)
                {
                    state.small_pity_count = checked(state.small_pity_count + 1);
                    state.big_pity_count = checked(state.big_pity_count + 1);
                    state.total_draw_count = checked(state.total_draw_count + 1);

                    int rarity = SelectRarity(pool, state);
                    GachaRewardDefinition reward = SelectReward(pool.RewardsByRarity[rarity]);
                    bool isNewCharacter = InsertCharacterIfNew(
                        db, IdGenerator.GetNextId(), playerId, reward.CharacterId);

                    int fragmentItemId = 0;
                    int fragmentNum = 0;
                    if (isNewCharacter)
                    {
                        newCharacters.Add(reward.CharacterId);
                    }
                    else
                    {
                        fragmentItemId = reward.FragmentItemId;
                        fragmentNum = reward.FragmentNum;
                        AddItem(db, playerId, fragmentItemId, fragmentNum);
                        fragmentChanges[fragmentItemId] =
                            GetQuantity(fragmentChanges, fragmentItemId) + fragmentNum;
                    }

                    if (rarity == pool.SmallPityRarity) state.small_pity_count = 0;
                    if (rarity == pool.BigPityRarity) state.big_pity_count = 0;

                    results.Add(new GachaResultItem
                    {
                        drawIndex = index,
                        characterId = reward.CharacterId,
                        rarity = rarity,
                        isNewCharacter = isNewCharacter,
                        fragmentItemId = fragmentItemId,
                        fragmentNum = fragmentNum
                    });

                    InsertGachaRecord(db, new Player_gacha_record
                    {
                        id = IdGenerator.GetNextId(),
                        batch_id = batchId,
                        draw_index = checked((byte)index),
                        player_id = playerId,
                        pool_id = pool.PoolId,
                        character_id = reward.CharacterId,
                        rarity = checked((byte)rarity),
                        is_new_character = isNewCharacter,
                        fragment_item_id = fragmentItemId,
                        fragment_num = fragmentNum,
                        small_pity_count = state.small_pity_count,
                        big_pity_count = state.big_pity_count,
                        created_at = DateTime.UtcNow
                    });
                }

                UpdateGachaState(db, state);
                db.Ado.CommitTran();

                long remainingTickets = ticketQuantity - usedTickets;
                long remainingDiamonds = diamondQuantity - usedDiamonds;
                SyncCommittedComponents(
                    playerId, state, pool, remainingTickets, remainingDiamonds,
                    fragmentChanges, newCharacters);

                return new ResGachaDraw
                {
                    status = (int)Respones.OK,
                    batchId = batchId,
                    usedTicketCount = usedTickets,
                    usedDiamondCount = usedDiamonds,
                    ticketQuantity = remainingTickets,
                    diamondQuantity = remainingDiamonds,
                    smallPityCount = state.small_pity_count,
                    bigPityCount = state.big_pity_count,
                    totalDrawCount = state.total_draw_count,
                    results = results
                };
            }
            catch (Exception exception)
            {
                try { db.Ado.RollbackTran(); }
                catch (Exception rollbackException) { logger.Error(rollbackException); }
                logger.Error(exception, $"Gacha draw failed for player {playerId}, pool {pool.PoolId}.");
                InvalidateComponents(playerId);
                return new ResGachaDraw
                {
                    status = (int)Respones.GachaFailed,
                    errorMessage = "Gacha draw failed and was rolled back."
                };
            }
        }

        private static int SelectRarity(GachaPoolDefinition pool, Player_gacha_state state)
        {
            if (state.big_pity_count >= pool.BigPityCount) return pool.BigPityRarity;
            if (state.small_pity_count >= pool.SmallPityCount) return pool.SmallPityRarity;

            long totalWeight = 0;
            foreach (int weight in pool.RarityWeights) totalWeight = checked(totalWeight + weight);
            long roll = Random.Shared.NextInt64(totalWeight);
            for (int index = 0; index < pool.Rarities.Count; index++)
            {
                if (roll < pool.RarityWeights[index]) return pool.Rarities[index];
                roll -= pool.RarityWeights[index];
            }
            throw new InvalidOperationException("Failed to select gacha rarity.");
        }

        private static GachaRewardDefinition SelectReward(IReadOnlyList<GachaRewardDefinition> rewards)
        {
            long totalWeight = 0;
            foreach (GachaRewardDefinition reward in rewards)
                totalWeight = checked(totalWeight + reward.Weight);
            long roll = Random.Shared.NextInt64(totalWeight);
            foreach (GachaRewardDefinition reward in rewards)
            {
                if (roll < reward.Weight) return reward;
                roll -= reward.Weight;
            }
            throw new InvalidOperationException("Failed to select gacha reward.");
        }

        private void SyncCommittedComponents(
            long playerId,
            Player_gacha_state state,
            GachaPoolDefinition pool,
            long tickets,
            long diamonds,
            IReadOnlyDictionary<int, long> fragmentChanges,
            IReadOnlyCollection<int> newCharacters)
        {
            try
            {
                ApplyCommittedItems(
                    playerId, pool.TicketItemId, tickets,
                    pool.DiamondItemId, diamonds, fragmentChanges);
                PutGachaState(playerId, state);
                ApplyCommittedCharacters(playerId, newCharacters);
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Gacha component synchronization failed for player {playerId}.");
                InvalidateComponents(playerId);
            }
        }

        private static Player_gacha_state NewState(long playerId, int poolId) => new Player_gacha_state
        {
            player_id = playerId,
            pool_id = poolId
        };

        private static long GetQuantity(IReadOnlyDictionary<int, long> quantities, int itemId) =>
            quantities != null && quantities.TryGetValue(itemId, out long value) ? value : 0;

        internal void ReloadItemComponent(GM_PlayerEntity entity)
        {
            entity.uItem.quantities = DBService.Instance.GetGameInstance()
                .Queryable<Player_item>()
                .Where(item => item.player_id == entity.uPlayer.playerInfo.id)
                .ToList()
                .ToDictionary(item => item.item_id, item => item.quantity);
        }

        internal void ReloadCharacterComponent(GM_PlayerEntity entity)
        {
            entity.uCharacter.characterIds = new HashSet<int>(DBService.Instance.GetGameInstance()
                .Queryable<Player_character>()
                .Where(character => character.player_id == entity.uPlayer.playerInfo.id)
                .Select(character => character.character_id)
                .ToList());
        }

        internal void ReloadGachaComponent(GM_PlayerEntity entity)
        {
            entity.uGacha.poolStates = DBService.Instance.GetGameInstance()
                .Queryable<Player_gacha_state>()
                .Where(state => state.player_id == entity.uPlayer.playerInfo.id)
                .ToList()
                .ToDictionary(state => state.pool_id, state => state);
            entity.uGacha.isLoaded = true;
        }

        private static Dictionary<int, long> GetQuantities(long playerId, params int[] itemIds)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                if (entity.uItem.quantities == null) Instance.ReloadItemComponent(entity);
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

        private static Dictionary<int, long> LockPaymentItems(
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

        private static void DeductItem(SqlSugarClient db, long playerId, int itemId, long quantity)
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

        private static void AddItem(SqlSugarClient db, long playerId, int itemId, int quantity)
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

        private static void ApplyCommittedItems(
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
            foreach (KeyValuePair<int, long> change in fragmentChanges)
            {
                entity.uItem.quantities.TryGetValue(change.Key, out long current);
                entity.uItem.quantities[change.Key] = current + change.Value;
            }
        }

        private static List<int> GetOwnedCharacterIds(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                if (entity.uCharacter.characterIds == null) Instance.ReloadCharacterComponent(entity);
                return entity.uCharacter.characterIds.ToList();
            }

            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            return db.Queryable<Player_character>()
                .Where(character => character.player_id == playerId)
                .Select(character => character.character_id)
                .ToList();
        }

        private static bool InsertCharacterIfNew(
            SqlSugarClient db, long instanceId, long playerId, int characterId)
        {
            int affected = db.Ado.ExecuteCommand(
                "INSERT IGNORE INTO player_character (id, player_id, character_id, star) " +
                "VALUES (@id, @playerId, @characterId, 0)",
                new SugarParameter("@id", instanceId),
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@characterId", characterId));
            return affected == 1;
        }

        private static void ApplyCommittedCharacters(long playerId, IReadOnlyCollection<int> characterIds)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null) return;
            entity.uCharacter.characterIds ??= new HashSet<int>();
            foreach (int characterId in characterIds) entity.uCharacter.characterIds.Add(characterId);
        }

        private static void ReplaceCharacters(long playerId, IReadOnlyCollection<int> characterIds)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null) entity.uCharacter.characterIds = new HashSet<int>(characterIds);
        }

        private static Player_gacha_state GetGachaState(long playerId, int poolId)
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

        private static Player_gacha_state LockGachaState(SqlSugarClient db, long playerId, int poolId)
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

        private static void UpdateGachaState(SqlSugarClient db, Player_gacha_state state)
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

        private static void PutGachaState(long playerId, Player_gacha_state state)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null) return;
            entity.uGacha.poolStates ??= new Dictionary<int, Player_gacha_state>();
            entity.uGacha.poolStates[state.pool_id] = state;
            entity.uGacha.isLoaded = true;
        }

        private static void InsertGachaRecord(SqlSugarClient db, Player_gacha_record record)
        {
            int affected = db.Insertable(record).ExecuteCommand();
            if (affected != 1) throw new InvalidOperationException("Failed to insert gacha record.");
        }

        private static void InvalidateComponents(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null) return;
            entity.uItem.quantities = null;
            entity.uCharacter.characterIds = null;
            GachaComponent.Invalidate(entity);
        }

    }
}
