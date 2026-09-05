using Game.Core.GM_Gacha;
using Game.Datas.Excels;
using Game.Entries.Modules;

namespace GameServerTests.Gacha;

public sealed class GachaConfigurationTests
{
    [Fact]
    public void CurrentCsvConfiguration_LoadsSuccessfully()
    {
        string previousDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = AppContext.BaseDirectory;
            GachaModule.Instance.Init();
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
        }
    }

    [Fact]
    public void BuildPools_CreatesPoolFromValidConfiguration()
    {
        GachaPoolRewardConfig poolConfig = ValidPoolConfig();
        List<GachaPoolConfig> rewards = ValidRewards();

        IReadOnlyDictionary<int, GachaPoolDefinition> pools = GachaModule.BuildPools(
            new[] { poolConfig }, rewards,
            new Dictionary<int, int> { [20001] = 30001, [20002] = 30002 },
            new HashSet<int> { 10001, 10002, 30001, 30002 });

        GachaPoolDefinition pool = Assert.Single(pools).Value;
        Assert.Equal(30, pool.SmallPityCount);
        Assert.Equal(50, pool.BigPityCount);
        Assert.Equal(new[] { 1, 4 }, pool.Rarities);
    }

    [Fact]
    public void BuildPools_RejectsDuplicatePoolIds()
    {
        GachaPoolRewardConfig first = ValidPoolConfig();
        GachaPoolRewardConfig second = ValidPoolConfig();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            GachaModule.BuildPools(
                new[] { first, second }, ValidRewards(),
                new Dictionary<int, int> { [20001] = 30001, [20002] = 30002 },
                new HashSet<int> { 10001, 10002, 30001, 30002 }));

        Assert.Contains("duplicate gacha pool id", exception.Message);
    }

    [Fact]
    public void BuildPools_RejectsUnknownFragmentItem()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            GachaModule.BuildPools(
                new[] { ValidPoolConfig() }, ValidRewards(),
                new Dictionary<int, int> { [20001] = 30001, [20002] = 30002 },
                new HashSet<int> { 10001, 10002, 30001 }));

        Assert.Contains("invalid reward row", exception.Message);
    }

    [Fact]
    public void BuildPools_RejectsFragmentThatDoesNotMatchCharacterPieceId()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            GachaModule.BuildPools(
                new[] { ValidPoolConfig() }, ValidRewards(),
                new Dictionary<int, int> { [20001] = 30002, [20002] = 30001 },
                new HashSet<int> { 10001, 10002, 30001, 30002 }));

        Assert.Contains("invalid reward row", exception.Message);
    }

    [Fact]
    public void BuildPools_RejectsMismatchedRaritiesAndWeights()
    {
        GachaPoolRewardConfig poolConfig = ValidPoolConfig();
        poolConfig.rarityWeights = "90";

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            GachaModule.BuildPools(
                new[] { poolConfig }, ValidRewards(),
                new Dictionary<int, int> { [20001] = 30001, [20002] = 30002 },
                new HashSet<int> { 10001, 10002, 30001, 30002 }));

        Assert.Contains("rarity and weight settings do not match", exception.Message);
    }

    private static GachaPoolRewardConfig ValidPoolConfig() => new()
    {
        ID = 1,
        poolID = 1,
        smallPityRarity = "1",
        smallPityCount = "30",
        bigPityRarity = "4",
        bigPityCount = "50",
        rarities = "1,4",
        rarityWeights = "90,10",
        ticketItemID = "10001",
        diamondItemID = "10002",
        diamondCostPerDraw = "1"
    };

    private static List<GachaPoolConfig> ValidRewards() => new()
    {
        new GachaPoolConfig
        {
            ID = 1,
            poolID = 1,
            cardID = "20001",
            fragmentItemID = "30001",
            rarity = "1",
            weight = "90",
            fragmentNum = "1"
        },
        new GachaPoolConfig
        {
            ID = 2,
            poolID = 1,
            cardID = "20002",
            fragmentItemID = "30002",
            rarity = "4",
            weight = "10",
            fragmentNum = "1"
        }
    };
}
