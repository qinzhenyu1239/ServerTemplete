using Game.Core.GM_Gacha;
using Game.Datas.DBEntities;

namespace GameServerTests.Gacha;

public sealed class GachaProbabilityTests
{
    [Fact]
    [Trait("Category", "Statistical")]
    public void RarityDistribution_WithFixedSeed_MatchesConfiguredWeights()
    {
        const int sampleCount = 200_000;
        const double tolerance = 0.005;
        GachaPoolDefinition pool = GachaTestData.CreatePool();
        var state = new Player_gacha_state();
        var random = new Random(20260905);
        var counts = pool.Rarities.ToDictionary(rarity => rarity, _ => 0);

        for (int index = 0; index < sampleCount; index++)
        {
            long roll = random.NextInt64(100);
            int rarity = GM_GachaMgr.SelectRarityAtRoll(pool, state, roll);
            counts[rarity]++;
        }

        for (int index = 0; index < pool.Rarities.Count; index++)
        {
            int rarity = pool.Rarities[index];
            double expectedRate = pool.RarityWeights[index] / 100.0;
            double actualRate = counts[rarity] / (double)sampleCount;
            Assert.InRange(actualRate, expectedRate - tolerance, expectedRate + tolerance);
        }
    }
}
