using Framework.Core.Utils;
using Game.Core.Caches;
using Game.Core.Db;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Game.Core.GM_Gacha
{
    /// <summary>
    /// Reusable gacha domain service. Owns payment, reward, pity and transaction rules.
    /// Cache services own database statements and in-memory synchronization.
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
                Player_gacha_state state = PlayerGachaStateCache.Instance.Get(playerId, pool.PoolId) ??
                                           NewState(playerId, pool.PoolId);
                Dictionary<int, long> quantities = PlayerItemCache.Instance.GetQuantities(
                    playerId, pool.TicketItemId, pool.DiamondItemId);
                long ticketQuantity = GetQuantity(quantities, pool.TicketItemId);
                long diamondQuantity = GetQuantity(quantities, pool.DiamondItemId);
                List<int> ownedCharacterIds = PlayerCharacterCache.Instance.GetOwnedCharacterIds(playerId);

                PlayerItemCache.Instance.ApplyCommitted(
                    playerId, pool.TicketItemId, ticketQuantity,
                    pool.DiamondItemId, diamondQuantity, new Dictionary<int, long>());
                PlayerGachaStateCache.Instance.Put(playerId, state);
                PlayerCharacterCache.Instance.Replace(playerId, ownedCharacterIds);

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
                InvalidateCache(playerId);
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
                Player_gacha_state state = PlayerGachaStateCache.Instance.Lock(db, playerId, pool.PoolId);
                Dictionary<int, long> quantities = PlayerItemCache.Instance.LockPaymentItems(
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

                PlayerItemCache.Instance.Deduct(db, playerId, pool.TicketItemId, usedTickets);
                PlayerItemCache.Instance.Deduct(db, playerId, pool.DiamondItemId, usedDiamonds);

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
                    bool isNewCharacter = PlayerCharacterCache.Instance.InsertIfNew(
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
                        PlayerItemCache.Instance.Add(db, playerId, fragmentItemId, fragmentNum);
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

                    PlayerGachaRecordCache.Instance.Insert(db, new Player_gacha_record
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

                PlayerGachaStateCache.Instance.Update(db, state);
                db.Ado.CommitTran();

                long remainingTickets = ticketQuantity - usedTickets;
                long remainingDiamonds = diamondQuantity - usedDiamonds;
                SyncCommittedCache(
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
                InvalidateCache(playerId);
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

        private void SyncCommittedCache(
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
                PlayerItemCache.Instance.ApplyCommitted(
                    playerId, pool.TicketItemId, tickets,
                    pool.DiamondItemId, diamonds, fragmentChanges);
                PlayerGachaStateCache.Instance.Put(playerId, state);
                PlayerCharacterCache.Instance.ApplyCommitted(playerId, newCharacters);
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Gacha cache synchronization failed for player {playerId}.");
                InvalidateCache(playerId);
            }
        }

        private static Player_gacha_state NewState(long playerId, int poolId) => new Player_gacha_state
        {
            player_id = playerId,
            pool_id = poolId
        };

        private static long GetQuantity(IReadOnlyDictionary<int, long> quantities, int itemId) =>
            quantities != null && quantities.TryGetValue(itemId, out long value) ? value : 0;

        private static void InvalidateCache(long playerId)
        {
            PlayerItemCache.Instance.Invalidate(playerId);
            PlayerCharacterCache.Instance.Invalidate(playerId);
            PlayerGachaStateCache.Instance.Invalidate(playerId);
        }
    }
}
