using Game.Core.GM_Gacha;
using Game.Datas.DBEntities;

namespace GameServerTests.Gacha;

public sealed class GachaSelectionTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(39, 1)]
    [InlineData(40, 2)]
    [InlineData(69, 2)]
    [InlineData(70, 3)]
    [InlineData(89, 3)]
    [InlineData(90, 4)]
    [InlineData(99, 4)]
    public void SelectRarityAtRoll_UsesConfiguredWeightBoundaries(long roll, int expectedRarity)
    {
        GachaPoolDefinition pool = GachaTestData.CreatePool();
        var state = new Player_gacha_state();

        int rarity = GM_GachaMgr.SelectRarityAtRoll(pool, state, roll);

        Assert.Equal(expectedRarity, rarity);
    }

    [Fact]
    public void SelectRarityAtRoll_TriggersSmallPityAtConfiguredCount()
    {
        GachaPoolDefinition pool = GachaTestData.CreatePool();
        var state = new Player_gacha_state
        {
            small_pity_count = pool.SmallPityCount,
            big_pity_count = pool.BigPityCount - 1
        };

        int rarity = GM_GachaMgr.SelectRarityAtRoll(pool, state, 0);

        Assert.Equal(pool.SmallPityRarity, rarity);
    }

    [Fact]
    public void SelectRarityAtRoll_TriggersBigPityBeforeSmallPity()
    {
        GachaPoolDefinition pool = GachaTestData.CreatePool();
        var state = new Player_gacha_state
        {
            small_pity_count = pool.SmallPityCount,
            big_pity_count = pool.BigPityCount
        };

        int rarity = GM_GachaMgr.SelectRarityAtRoll(pool, state, 0);

        Assert.Equal(pool.BigPityRarity, rarity);
    }

    [Fact]
    public void SelectRarityAtRoll_DoesNotTriggerPityOneDrawEarly()
    {
        GachaPoolDefinition pool = GachaTestData.CreatePool();
        var state = new Player_gacha_state
        {
            small_pity_count = pool.SmallPityCount - 1,
            big_pity_count = pool.BigPityCount - 1
        };

        int rarity = GM_GachaMgr.SelectRarityAtRoll(pool, state, 0);

        Assert.Equal(1, rarity);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void SelectRarityAtRoll_RejectsOutOfRangeRoll(long roll)
    {
        GachaPoolDefinition pool = GachaTestData.CreatePool();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GM_GachaMgr.SelectRarityAtRoll(pool, new Player_gacha_state(), roll));
    }

    [Theory]
    [InlineData(0, 20001)]
    [InlineData(39, 20001)]
    [InlineData(40, 20006)]
    [InlineData(99, 20006)]
    public void SelectRewardAtRoll_UsesRewardWeightBoundaries(long roll, int expectedCharacterId)
    {
        IReadOnlyList<GachaRewardDefinition> rewards =
            GachaTestData.CreatePool().RewardsByRarity[1];

        GachaRewardDefinition reward = GM_GachaMgr.SelectRewardAtRoll(rewards, roll);

        Assert.Equal(expectedCharacterId, reward.CharacterId);
    }
}
