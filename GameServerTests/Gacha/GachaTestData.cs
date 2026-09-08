using Game.Core.GM_Gacha;

namespace GameServerTests.Gacha;

internal static class GachaTestData
{
    internal static GachaPoolDefinition CreatePool() => new()
    {
        PoolId = 1,
        SmallPityRarity = 3,
        SmallPityCount = 30,
        BigPityRarity = 4,
        BigPityCount = 50,
        TicketItemId = 10001,
        DiamondItemId = 10002,
        DiamondCostPerDraw = 1,
        Rarities = new[] { 1, 2, 3, 4 },
        RarityWeights = new[] { 40, 30, 20, 10 },
        RewardsByRarity = new Dictionary<int, IReadOnlyList<GachaRewardDefinition>>
        {
            [1] = new[] { Reward(20001, 30001, 1, 1, 40), Reward(20006, 30006, 1, 1, 60) },
            [2] = new[] { Reward(20002, 30002, 1, 2, 30) },
            [3] = new[] { Reward(20003, 30003, 1, 3, 20) },
            [4] = new[] { Reward(20005, 30005, 1, 4, 10) }
        }
    };

    internal static GachaRewardDefinition Reward(
        int characterId,
        int fragmentItemId,
        int fragmentNum,
        int rarity,
        int weight) => new()
    {
        CharacterId = characterId,
        FragmentItemId = fragmentItemId,
        FragmentNum = fragmentNum,
        Rarity = rarity,
        Weight = weight
    };
}
