using Game.Core.GM_CharacterStarUp;
using Game.Core.GM_Gacha;
using GameServerTests.Gacha;
using MySqlConnector;

namespace GameServerTests.CharacterStarUp;

public sealed class CharacterStarUpManagerMySqlIntegrationTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Query_ReturnsNotOwnedZeroStarNormalAndMaxState()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(100);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        CharacterStarUpDefinition definition = Definition();
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            Assert.Equal(CharacterStarUpStatus.NotOwned,
                GM_CharacterStarUpMgr.Instance.GetCharacterInfo(playerId, definition).Status);

            await SeedAsync(connection, playerId, star: 0, pieces: 7);
            CharacterStarUpInfoResult zero = GM_CharacterStarUpMgr.Instance.GetCharacterInfo(playerId, definition);
            Assert.Equal(0, zero.Star);
            Assert.Equal(1, zero.NextStar);
            Assert.Equal(2, zero.NextPieceCount);
            Assert.Equal(7, zero.PieceQuantity);

            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
                $"UPDATE player_character SET star=2 WHERE player_id={playerId};");
            CharacterStarUpInfoResult max = GM_CharacterStarUpMgr.Instance.GetCharacterInfo(playerId, definition);
            Assert.True(max.IsMaxStar);
            Assert.Equal(2, max.Star);
            Assert.Equal(0, max.NextPieceCount);
        }
        finally { await CleanupAsync(connection, playerId); }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Upgrade_IsOneLevelOnly_AndRepeatedOrOldTargetsDoNotDeductAgain()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(110);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        CharacterStarUpDefinition definition = Definition();
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedAsync(connection, playerId, star: 0, pieces: 10);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

            CharacterStarUpResult cross = GM_CharacterStarUpMgr.Instance.StarUp(playerId, definition, 2);
            Assert.Equal(CharacterStarUpStatus.TargetStarInvalid, cross.Status);
            Assert.Equal(10, await PiecesAsync(connection, playerId));

            CharacterStarUpResult upgraded = GM_CharacterStarUpMgr.Instance.StarUp(playerId, definition, 1);
            Assert.True(upgraded.DidUpgrade);
            Assert.Equal(2, upgraded.ConsumedPieceCount);
            Assert.Equal(8, upgraded.RemainingPieceQuantity);

            CharacterStarUpResult duplicate = GM_CharacterStarUpMgr.Instance.StarUp(playerId, definition, 1);
            Assert.False(duplicate.DidUpgrade);
            Assert.Equal(8, await PiecesAsync(connection, playerId));

            CharacterStarUpResult second = GM_CharacterStarUpMgr.Instance.StarUp(playerId, definition, 2);
            Assert.True(second.DidUpgrade);
            CharacterStarUpResult old = GM_CharacterStarUpMgr.Instance.StarUp(playerId, definition, 1);
            Assert.False(old.DidUpgrade);
            Assert.Equal(5, await PiecesAsync(connection, playerId));
            Assert.Equal(2, await StarAsync(connection, playerId));
        }
        finally { await CleanupAsync(connection, playerId); }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task InsufficientFragments_DoNotChangeStarOrQuantity()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(120);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        CharacterStarUpDefinition definition = Definition();
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedAsync(connection, playerId, star: 0, pieces: 1);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            CharacterStarUpResult result = GM_CharacterStarUpMgr.Instance.StarUp(playerId, definition, 1);
            Assert.Equal(CharacterStarUpStatus.FragmentNotEnough, result.Status);
            Assert.Equal(0, await StarAsync(connection, playerId));
            Assert.Equal(1, await PiecesAsync(connection, playerId));
        }
        finally { await CleanupAsync(connection, playerId); }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task FailureAfterDeduction_RollsBackFragmentsAndStar()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(130);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        CharacterStarUpDefinition definition = Definition();
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedAsync(connection, playerId, star: 0, pieces: 10);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            CharacterStarUpResult result = GM_CharacterStarUpMgr.Instance.StarUpForTest(
                playerId, definition, 1, () => throw new InvalidOperationException("forced"));
            Assert.Equal(CharacterStarUpStatus.Failed, result.Status);
            Assert.Equal(0, await StarAsync(connection, playerId));
            Assert.Equal(10, await PiecesAsync(connection, playerId));
        }
        finally { await CleanupAsync(connection, playerId); }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ConcurrentSameTarget_UpgradesAndDeductsExactlyOnce()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(140);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        CharacterStarUpDefinition definition = Definition();
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedAsync(connection, playerId, star: 0, pieces: 10);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            CharacterStarUpResult[] results = await Task.WhenAll(
                Task.Run(() => GM_CharacterStarUpMgr.Instance.StarUp(playerId, definition, 1)),
                Task.Run(() => GM_CharacterStarUpMgr.Instance.StarUp(playerId, definition, 1)));
            Assert.All(results, result => Assert.Equal(CharacterStarUpStatus.Success, result.Status));
            Assert.Single(results, result => result.DidUpgrade);
            Assert.Single(results, result => !result.DidUpgrade);
            Assert.Equal(1, await StarAsync(connection, playerId));
            Assert.Equal(8, await PiecesAsync(connection, playerId));
        }
        finally { await CleanupAsync(connection, playerId); }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task DuplicateGachaFragments_CanUpgradeTheSameCharacter()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(150);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        CharacterStarUpDefinition definition = Definition();
        GachaPoolDefinition pool = Pool();
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
                $"INSERT INTO player_item(player_id,item_id,quantity) VALUES " +
                $"({playerId},{pool.TicketItemId},2),({playerId},{pool.DiamondItemId},0);");
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            Assert.True(Assert.Single(GM_GachaMgr.Instance.Draw(playerId, pool, 1).Rewards).IsNewCharacter);
            Assert.False(Assert.Single(GM_GachaMgr.Instance.Draw(playerId, pool, 1).Rewards).IsNewCharacter);
            CharacterStarUpResult upgraded = GM_CharacterStarUpMgr.Instance.StarUp(playerId, definition, 1);
            Assert.True(upgraded.DidUpgrade);
            Assert.Equal(0, upgraded.RemainingPieceQuantity);
        }
        finally { await CleanupAsync(connection, playerId); }
    }

    private static CharacterStarUpDefinition Definition() => new()
    {
        CharacterId = 920001,
        PieceItemId = 930001,
        MaxStar = 2,
        PieceCounts = new Dictionary<int, int> { [0] = 2, [1] = 3, [2] = 0 }
    };

    private static GachaPoolDefinition Pool() => new()
    {
        PoolId = 950001,
        SmallPityRarity = 1,
        SmallPityCount = 100,
        BigPityRarity = 1,
        BigPityCount = 200,
        TicketItemId = 940001,
        DiamondItemId = 940002,
        DiamondCostPerDraw = 5,
        Rarities = new[] { 1 },
        RarityWeights = new[] { 1 },
        RewardsByRarity = new Dictionary<int, IReadOnlyList<GachaRewardDefinition>>
        {
            [1] = new[] { new GachaRewardDefinition
                { CharacterId = 920001, FragmentItemId = 930001, FragmentNum = 2, Rarity = 1, Weight = 1 } }
        }
    };

    private static Task SeedAsync(MySqlConnection connection, long playerId, int star, long pieces) =>
        GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
            $"INSERT INTO player_character(player_id,character_id,star) VALUES ({playerId},920001,{star});" +
            $"INSERT INTO player_item(player_id,item_id,quantity) VALUES ({playerId},930001,{pieces});");

    private static Task<long> StarAsync(MySqlConnection connection, long playerId) =>
        GachaMySqlTestEnvironment.ReadScalarAsync(connection,
            $"SELECT star FROM player_character WHERE player_id={playerId} AND character_id=920001;");

    private static Task<long> PiecesAsync(MySqlConnection connection, long playerId) =>
        GachaMySqlTestEnvironment.ReadScalarAsync(connection,
            $"SELECT quantity FROM player_item WHERE player_id={playerId} AND item_id=930001;");

    private static async Task CleanupAsync(MySqlConnection connection, long playerId)
    {
        if (!GachaMySqlTestEnvironment.KeepDatabaseData)
            await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
    }
}
