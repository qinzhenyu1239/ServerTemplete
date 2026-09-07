using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Core.EntityMgr;
using Game.Core.GM_Gacha;
using Game.Core.Idempotency;
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
            List<GachaPoolRewardConfig> poolConfigs =
                ExcelUtils.GetConfigDatas<GachaPoolRewardConfig>(nameof(GachaPoolRewardConfig));
            List<GachaPoolConfig> rewardConfigs =
                ExcelUtils.GetConfigDatas<GachaPoolConfig>(nameof(GachaPoolConfig));
            List<KeyValuePair<int, int>> characterPieceValues =
                ExcelUtils.GetConfigDatas<Character>(nameof(Character))
                    .Select(ParseCharacterPiece)
                    .ToList();
            if (characterPieceValues.Select(pair => pair.Key).Distinct().Count() != characterPieceValues.Count)
                throw new InvalidOperationException("Character config contains duplicate CardID values.");
            Dictionary<int, int> characterPieces = characterPieceValues.ToDictionary(
                pair => pair.Key, pair => pair.Value);
            var itemIds = new HashSet<int>(
                ExcelUtils.GetConfigDatas<Item>(nameof(Item)).Select(item => item.id));

            pools = BuildPools(poolConfigs, rewardConfigs, characterPieces, itemIds);
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

            GachaPoolInfoResult result = GM_GachaMgr.Instance.GetPoolInfo(playerId, pool);
            return MapPoolInfoResponse(result);
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
            if (!OperationIdValidator.IsValid(request.operationId))
            {
                return new ResGachaDraw
                {
                    status = (int)Respones.InvalidParams,
                    errorMessage = "operationId is required and must be a 32-character lowercase UUID.",
                    operationId = request.operationId
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

            GachaDrawResult result = GM_GachaMgr.Instance.Draw(
                playerId, pool, request.drawCount, request.operationId);
            return MapDrawResponse(result);
        }

        private static ResGetGachaPoolInfo MapPoolInfoResponse(GachaPoolInfoResult result)
        {
            if (result.Status != GachaOperationStatus.Success)
            {
                return new ResGetGachaPoolInfo
                {
                    status = (int)Respones.GachaFailed,
                    errorMessage = "Failed to query gacha pool information."
                };
            }

            return new ResGetGachaPoolInfo
            {
                status = (int)Respones.OK,
                poolId = result.PoolId,
                smallPityCount = result.SmallPityCount,
                smallPityMax = result.SmallPityMax,
                bigPityCount = result.BigPityCount,
                bigPityMax = result.BigPityMax,
                totalDrawCount = result.TotalDrawCount,
                ticketItemId = result.TicketItemId,
                ticketQuantity = result.TicketQuantity,
                diamondItemId = result.DiamondItemId,
                diamondQuantity = result.DiamondQuantity,
                diamondCostPerDraw = result.DiamondCostPerDraw,
                ownedCharacterIds = result.OwnedCharacterIds?.ToList() ?? new List<int>()
            };
        }

        private static ResGachaDraw MapDrawResponse(GachaDrawResult result)
        {
            if (result.Status == GachaOperationStatus.ResourceNotEnough)
            {
                return new ResGachaDraw
                {
                    status = (int)Respones.GachaResourceNotEnough,
                    errorMessage = "Gacha currency is not enough.",
                    operationId = result.OperationId
                };
            }
            if (result.Status == GachaOperationStatus.OperationConflict)
            {
                return new ResGachaDraw
                {
                    status = (int)Respones.EconomicOperationConflict,
                    errorMessage = "operationId was already used with different parameters.",
                    operationId = result.OperationId
                };
            }
            if (result.Status != GachaOperationStatus.Success)
            {
                return new ResGachaDraw
                {
                    status = (int)Respones.GachaFailed,
                    errorMessage = "Gacha draw failed and was rolled back.",
                    operationId = result.OperationId
                };
            }

            return new ResGachaDraw
            {
                status = (int)Respones.OK,
                operationId = result.OperationId,
                batchId = result.BatchId,
                usedTicketCount = result.UsedTicketCount,
                usedDiamondCount = result.UsedDiamondCount,
                ticketQuantity = result.TicketQuantity,
                diamondQuantity = result.DiamondQuantity,
                smallPityCount = result.SmallPityCount,
                bigPityCount = result.BigPityCount,
                totalDrawCount = result.TotalDrawCount,
                results = result.Rewards?.Select(reward => new GachaResultItem
                {
                    drawIndex = reward.DrawIndex,
                    characterId = reward.CharacterId,
                    rarity = reward.Rarity,
                    isNewCharacter = reward.IsNewCharacter,
                    fragmentItemId = reward.FragmentItemId,
                    fragmentNum = reward.FragmentNum
                }).ToList() ?? new List<GachaResultItem>()
            };
        }

        internal static IReadOnlyDictionary<int, GachaPoolDefinition> BuildPools(
            IReadOnlyCollection<GachaPoolRewardConfig> poolConfigs,
            IReadOnlyCollection<GachaPoolConfig> rewardConfigs,
            IReadOnlyDictionary<int, int> characterPieces,
            IReadOnlySet<int> itemIds)
        {
            var result = new Dictionary<int, GachaPoolDefinition>();
            foreach (GachaPoolRewardConfig config in poolConfigs)
            {
                if (config.poolID <= 0 || result.ContainsKey(config.poolID))
                    throw new InvalidOperationException($"Invalid or duplicate gacha pool id: {config.poolID}.");
                int smallPityRarity = ParsePositiveInteger(config.smallPityRarity, "smallPityRarity", config.poolID);
                int smallPityCount = ParsePositiveInteger(config.smallPityCount, "smallPityCount", config.poolID);
                int bigPityRarity = ParsePositiveInteger(config.bigPityRarity, "bigPityRarity", config.poolID);
                int bigPityCount = ParsePositiveInteger(config.bigPityCount, "bigPityCount", config.poolID);
                int ticketItemId = ParsePositiveInteger(config.ticketItemID, "ticketItemID", config.poolID);
                int diamondItemId = ParsePositiveInteger(config.diamondItemID, "diamondItemID", config.poolID);
                int diamondCostPerDraw = ParsePositiveInteger(config.diamondCostPerDraw, "diamondCostPerDraw", config.poolID);
                if (!itemIds.Contains(ticketItemId) || !itemIds.Contains(diamondItemId))
                    throw new InvalidOperationException($"Gacha pool {config.poolID} references an unknown payment item.");

                List<int> rarities = ParsePositiveIntegers(config.rarities, "rarities", config.poolID);
                List<int> rarityWeights = ParsePositiveIntegers(config.rarityWeights, "rarityWeights", config.poolID);
                if (rarities.Count != rarityWeights.Count || rarities.Distinct().Count() != rarities.Count)
                    throw new InvalidOperationException($"Gacha pool {config.poolID} rarity and weight settings do not match.");
                if (!rarities.Contains(smallPityRarity) || !rarities.Contains(bigPityRarity))
                    throw new InvalidOperationException($"Gacha pool {config.poolID} pity rarity is not present in rarities.");

                var mutableRewards = new Dictionary<int, List<GachaRewardDefinition>>();
                foreach (GachaPoolConfig item in rewardConfigs.Where(item => item.poolID == config.poolID))
                {
                    int characterId = ParsePositiveInteger(item.cardID, "cardID", config.poolID, item.ID);
                    int fragmentItemId = ParsePositiveInteger(item.fragmentItemID, "fragmentItemID", config.poolID, item.ID);
                    int rarity = ParsePositiveInteger(item.rarity, "rarity", config.poolID, item.ID);
                    int weight = ParsePositiveInteger(item.weight, "weight", config.poolID, item.ID);
                    int fragmentNum = ParsePositiveInteger(item.fragmentNum, "fragmentNum", config.poolID, item.ID);
                    if (!characterPieces.TryGetValue(characterId, out int configuredPieceItemId) ||
                        !itemIds.Contains(fragmentItemId) || fragmentItemId != configuredPieceItemId)
                        throw new InvalidOperationException(
                            $"Gacha pool {config.poolID} has an invalid reward row {item.ID}.");

                    if (!mutableRewards.TryGetValue(rarity, out List<GachaRewardDefinition> rewards))
                    {
                        rewards = new List<GachaRewardDefinition>();
                        mutableRewards.Add(rarity, rewards);
                    }
                    rewards.Add(new GachaRewardDefinition
                    {
                        CharacterId = characterId,
                        FragmentItemId = fragmentItemId,
                        FragmentNum = fragmentNum,
                        Rarity = rarity,
                        Weight = weight
                    });
                }
                Dictionary<int, IReadOnlyList<GachaRewardDefinition>> rewardsByRarity = mutableRewards
                    .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<GachaRewardDefinition>)pair.Value);

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
                    SmallPityRarity = smallPityRarity,
                    SmallPityCount = smallPityCount,
                    BigPityRarity = bigPityRarity,
                    BigPityCount = bigPityCount,
                    TicketItemId = ticketItemId,
                    DiamondItemId = diamondItemId,
                    DiamondCostPerDraw = diamondCostPerDraw,
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

        private static KeyValuePair<int, int> ParseCharacterPiece(Character config)
        {
            if (config != null && int.TryParse(config.CardID?.Trim(), out int characterId) &&
                characterId > 0 && int.TryParse(config.PieceId?.Trim(), out int pieceItemId) &&
                pieceItemId > 0)
            {
                return new KeyValuePair<int, int>(characterId, pieceItemId);
            }

            throw new InvalidOperationException(
                $"Character config row {config?.ID ?? "<unknown>"} has an invalid CardID or PieceId.");
        }

        private static int ParsePositiveInteger(string value, string field, int poolId, int rowId = 0)
        {
            if (int.TryParse(value?.Trim(), out int parsed) && parsed > 0)
                return parsed;

            string row = rowId > 0 ? $" reward row {rowId}" : string.Empty;
            throw new InvalidOperationException(
                $"Gacha pool {poolId}{row} has an invalid {field} setting.");
        }
    }
}
