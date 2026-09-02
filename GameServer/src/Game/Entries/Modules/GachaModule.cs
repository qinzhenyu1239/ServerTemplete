using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Core.EntityMgr;
using Game.Core.GM_Gacha;
using Game.Datas.DBEntities;
using Game.Datas.Excels;
using Game.Datas.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entries.Modules
{
    /// <summary>
    /// Validates gacha requests and prepares configuration used by the domain manager.
    /// </summary>
    public sealed class GachaModule
    {
        public static readonly GachaModule Instance = new GachaModule();
        private IReadOnlyDictionary<int, GachaPoolDefinition> pools;

        private GachaModule() { }

        public void Init()
        {
            List<GachaPoolConfig> poolConfigs =
                ExcelUtils.GetConfigDatas<GachaPoolConfig>(nameof(GachaPoolConfig));
            List<GachaPoolRewardConfig> rewardConfigs =
                ExcelUtils.GetConfigDatas<GachaPoolRewardConfig>(nameof(GachaPoolRewardConfig));
            var characterIds = new HashSet<int>(
                ExcelUtils.GetConfigDatas<CharactorConfig>(nameof(CharactorConfig)).Select(item => item.ID));
            var itemIds = new HashSet<int>(
                ExcelUtils.GetConfigDatas<Item>(nameof(Item)).Select(item => item.id));

            pools = BuildPools(poolConfigs, rewardConfigs, characterIds, itemIds);
        }

        public ResGetGachaPoolInfo HandlerGetPoolInfo(
            IdSession session,
            ReqGetGachaPoolInfo request,
            long playerId)
        {
            Player player = playerId > 0 ? GM_AccountEntityMgr.Instance.GetPlayerById(playerId) : null;
            if (player == null)
            {
                return new ResGetGachaPoolInfo
                {
                    status = (int)Respones.AccountIsNotLogin,
                    errorMessage = "Player is not logged in."
                };
            }
            if (request == null || request.poolId <= 0)
            {
                return new ResGetGachaPoolInfo
                {
                    status = (int)Respones.InvalidParams,
                    errorMessage = "Invalid gacha pool request."
                };
            }
            if (pools == null)
            {
                return new ResGetGachaPoolInfo
                {
                    status = (int)Respones.GachaConfigError,
                    errorMessage = "Gacha module is not initialized."
                };
            }
            if (!pools.TryGetValue(request.poolId, out GachaPoolDefinition pool))
            {
                return new ResGetGachaPoolInfo
                {
                    status = (int)Respones.GachaPoolNotFound,
                    errorMessage = "Gacha pool does not exist."
                };
            }

            return GM_GachaMgr.Instance.GetPoolInfo(playerId, pool);
        }

        public ResGachaDraw HandlerDraw(IdSession session, ReqGachaDraw request, long playerId)
        {
            Player player = playerId > 0 ? GM_AccountEntityMgr.Instance.GetPlayerById(playerId) : null;
            if (player == null)
            {
                return new ResGachaDraw
                {
                    status = (int)Respones.AccountIsNotLogin,
                    errorMessage = "Player is not logged in."
                };
            }
            if (request == null || request.poolId <= 0)
            {
                return new ResGachaDraw
                {
                    status = (int)Respones.InvalidParams,
                    errorMessage = "Invalid gacha draw request."
                };
            }
            if (request.drawCount != 1 && request.drawCount != 10)
            {
                return new ResGachaDraw
                {
                    status = (int)Respones.GachaDrawCountInvalid,
                    errorMessage = "Draw count must be 1 or 10."
                };
            }
            if (pools == null)
            {
                return new ResGachaDraw
                {
                    status = (int)Respones.GachaConfigError,
                    errorMessage = "Gacha module is not initialized."
                };
            }
            if (!pools.TryGetValue(request.poolId, out GachaPoolDefinition pool))
            {
                return new ResGachaDraw
                {
                    status = (int)Respones.GachaPoolNotFound,
                    errorMessage = "Gacha pool does not exist."
                };
            }

            return GM_GachaMgr.Instance.Draw(playerId, pool, request.drawCount);
        }

        private static IReadOnlyDictionary<int, GachaPoolDefinition> BuildPools(
            IReadOnlyCollection<GachaPoolConfig> poolConfigs,
            IReadOnlyCollection<GachaPoolRewardConfig> rewardConfigs,
            IReadOnlySet<int> characterIds,
            IReadOnlySet<int> itemIds)
        {
            var result = new Dictionary<int, GachaPoolDefinition>();
            foreach (GachaPoolConfig config in poolConfigs)
            {
                if (config.poolID <= 0 || result.ContainsKey(config.poolID))
                    throw new InvalidOperationException($"Invalid or duplicate gacha pool id: {config.poolID}.");
                if (config.smallPityCount <= 0 || config.bigPityCount <= 0 ||
                    config.ticketItemID <= 0 || config.diamondItemID <= 0 ||
                    config.diamondCostPerDraw <= 0)
                    throw new InvalidOperationException($"Gacha pool {config.poolID} has invalid pity or cost settings.");
                if (!itemIds.Contains(config.ticketItemID) || !itemIds.Contains(config.diamondItemID))
                    throw new InvalidOperationException($"Gacha pool {config.poolID} references an unknown payment item.");

                List<int> rarities = ParsePositiveIntegers(config.rarities, "rarities", config.poolID);
                List<int> rarityWeights = ParsePositiveIntegers(config.rarityWeights, "rarityWeights", config.poolID);
                if (rarities.Count != rarityWeights.Count || rarities.Distinct().Count() != rarities.Count)
                    throw new InvalidOperationException($"Gacha pool {config.poolID} rarity and weight settings do not match.");
                if (!rarities.Contains(config.smallPityRarity) || !rarities.Contains(config.bigPityRarity))
                    throw new InvalidOperationException($"Gacha pool {config.poolID} pity rarity is not present in rarities.");

                Dictionary<int, IReadOnlyList<GachaRewardDefinition>> rewardsByRarity = rewardConfigs
                    .Where(item => item.poolID == config.poolID)
                    .GroupBy(item => item.rarity)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<GachaRewardDefinition>)group.Select(item =>
                        {
                            if (item.weight <= 0 || item.fragmentNum <= 0 ||
                                !characterIds.Contains(item.cardID) || !itemIds.Contains(item.fragmentItemID))
                                throw new InvalidOperationException(
                                    $"Gacha pool {config.poolID} has an invalid reward row {item.ID}.");
                            return new GachaRewardDefinition
                            {
                                CharacterId = item.cardID,
                                FragmentItemId = item.fragmentItemID,
                                FragmentNum = item.fragmentNum,
                                Rarity = item.rarity,
                                Weight = item.weight
                            };
                        }).ToList());

                foreach (int rarity in rarities)
                {
                    if (!rewardsByRarity.TryGetValue(rarity, out IReadOnlyList<GachaRewardDefinition> rewards) ||
                        rewards.Count == 0)
                        throw new InvalidOperationException(
                            $"Gacha pool {config.poolID} has no rewards for rarity {rarity}.");
                }

                result.Add(config.poolID, new GachaPoolDefinition
                {
                    PoolId = config.poolID,
                    SmallPityRarity = config.smallPityRarity,
                    SmallPityCount = config.smallPityCount,
                    BigPityRarity = config.bigPityRarity,
                    BigPityCount = config.bigPityCount,
                    TicketItemId = config.ticketItemID,
                    DiamondItemId = config.diamondItemID,
                    DiamondCostPerDraw = config.diamondCostPerDraw,
                    Rarities = rarities,
                    RarityWeights = rarityWeights,
                    RewardsByRarity = rewardsByRarity
                });
            }
            return result;
        }

        private static List<int> ParsePositiveIntegers(string value, string field, int poolId)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Gacha pool {poolId} has an empty {field} setting.");

            var result = new List<int>();
            foreach (string part in value.Split(','))
            {
                if (!int.TryParse(part.Trim(), out int parsed) || parsed <= 0)
                    throw new InvalidOperationException($"Gacha pool {poolId} has an invalid {field} setting.");
                result.Add(parsed);
            }
            return result;
        }
    }
}
