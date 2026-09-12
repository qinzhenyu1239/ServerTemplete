using Game.Core.GM_Gacha;
using Game.Datas.GMEntities;
using MySqlConnector;

namespace GameServerTests.Gacha;

public sealed class GachaManagerMySqlIntegrationTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task SameOperation_ReturnsOriginalResultWithoutSecondDebit()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(1);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910000);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 2, diamonds: 0);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            string operationId = Guid.NewGuid().ToString("N");

            GachaDrawResult first = GM_GachaMgr.Instance.Draw(playerId, pool, 1, operationId);
            GachaDrawResult duplicate = GM_GachaMgr.Instance.Draw(playerId, pool, 1, operationId);

            Assert.Equal(GachaOperationStatus.Success, first.Status);
            Assert.Equal(first.BatchId, duplicate.BatchId);
            Assert.Equal(first.Rewards.Single().CharacterId, duplicate.Rewards.Single().CharacterId);
            Assert.Equal(1, await ItemQuantityAsync(connection, playerId, pool.TicketItemId));
            Assert.Equal(1, await PlayerRowCountAsync(connection, "player_gacha_record", playerId));
            Assert.Equal(1, await PlayerRowCountAsync(connection, "player_gacha_operation", playerId));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ConcurrentSameOperation_ReturnsOneCommittedDrawTwice()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(4);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910030);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 2, diamonds: 0);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            string operationId = Guid.NewGuid().ToString("N");
            GachaDrawResult[] results = await Task.WhenAll(
                Task.Run(() => GM_GachaMgr.Instance.Draw(playerId, pool, 1, operationId)),
                Task.Run(() => GM_GachaMgr.Instance.Draw(playerId, pool, 1, operationId)));
            Assert.All(results, result => Assert.Equal(GachaOperationStatus.Success, result.Status));
            Assert.Equal(results[0].BatchId, results[1].BatchId);
            Assert.Equal(1, await ItemQuantityAsync(connection, playerId, pool.TicketItemId));
            Assert.Equal(1, await PlayerRowCountAsync(connection, "player_gacha_record", playerId));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task SameOperationId_IsIndependentAcrossPlayers()
    {
        long firstPlayerId = GachaMySqlTestEnvironment.PlayerId(5);
        long secondPlayerId = GachaMySqlTestEnvironment.PlayerId(6);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910040);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, firstPlayerId);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, secondPlayerId);
        try
        {
            await SeedItemsAsync(connection, firstPlayerId, pool, tickets: 1, diamonds: 0);
            await SeedItemsAsync(connection, secondPlayerId, pool, tickets: 1, diamonds: 0);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            string operationId = Guid.NewGuid().ToString("N");
            Assert.Equal(GachaOperationStatus.Success,
                GM_GachaMgr.Instance.Draw(firstPlayerId, pool, 1, operationId).Status);
            Assert.Equal(GachaOperationStatus.Success,
                GM_GachaMgr.Instance.Draw(secondPlayerId, pool, 1, operationId).Status);
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
            {
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, firstPlayerId);
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, secondPlayerId);
            }
        }
    }


    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ReusedOperationWithDifferentParameters_ReturnsConflict()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(2);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910010);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 10, diamonds: 0);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            string operationId = Guid.NewGuid().ToString("N");
            Assert.Equal(GachaOperationStatus.Success,
                GM_GachaMgr.Instance.Draw(playerId, pool, 1, operationId).Status);
            Assert.Equal(GachaOperationStatus.OperationConflict,
                GM_GachaMgr.Instance.Draw(playerId, pool, 10, operationId).Status);
            Assert.Equal(9, await ItemQuantityAsync(connection, playerId, pool.TicketItemId));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task InsufficientResult_IsNotStoredAndCanSucceedAfterResourcesIncrease()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(3);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910020);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 0, diamonds: 0);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            string operationId = Guid.NewGuid().ToString("N");
            Assert.Equal(GachaOperationStatus.ResourceNotEnough,
                GM_GachaMgr.Instance.Draw(playerId, pool, 1, operationId).Status);
            Assert.Equal(0, await PlayerRowCountAsync(
                connection, "player_gacha_operation", playerId));
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
                $"UPDATE player_item SET quantity=5 WHERE player_id={playerId} AND item_id={pool.TicketItemId};");
            Assert.Equal(GachaOperationStatus.Success,
                GM_GachaMgr.Instance.Draw(playerId, pool, 1, operationId).Status);
            Assert.Equal(4, await ItemQuantityAsync(connection, playerId, pool.TicketItemId));
            Assert.Equal(1, await PlayerRowCountAsync(
                connection, "player_gacha_operation", playerId));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task SingleDraw_KeepsResponseDatabaseAndOnlineComponentsConsistent()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(10);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910001);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 1, diamonds: 100);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            GM_PlayerEntity entity = GachaMySqlTestEnvironment.AddOnlinePlayer(
                playerId,
                new Dictionary<int, long>
                {
                    [pool.TicketItemId] = 1,
                    [pool.DiamondItemId] = 100
                });

            GachaDrawResult result = GM_GachaMgr.Instance.Draw(playerId, pool, 1);

            Assert.Equal(GachaOperationStatus.Success, result.Status);
            GachaDrawRewardResult reward = Assert.Single(result.Rewards);
            Assert.True(reward.IsNewCharacter);
            Assert.Equal(0, reward.FragmentItemId);
            Assert.Equal(1, result.UsedTicketCount);
            Assert.Equal(0, result.UsedDiamondCount);
            Assert.Equal(1, result.TotalDrawCount);
            Assert.Equal(
                result.BatchId,
                await GachaMySqlTestEnvironment.ReadScalarAsync(
                    connection,
                    $"SELECT batch_id FROM player_gacha_record WHERE player_id={playerId};"));
            Assert.Equal(
                reward.CharacterId,
                await GachaMySqlTestEnvironment.ReadScalarAsync(
                    connection,
                    $"SELECT character_id FROM player_gacha_record WHERE player_id={playerId};"));
            Assert.Equal(
                0,
                await ItemQuantityAsync(connection, playerId, pool.TicketItemId));
            Assert.Equal(0, entity.uItem.quantities[pool.TicketItemId]);
            Assert.Equal(100, entity.uItem.quantities[pool.DiamondItemId]);
            Assert.Contains(reward.CharacterId, entity.uCharacter.characterIds);
            Assert.Equal(0, entity.uCharacter.characterStars[reward.CharacterId]);
            Assert.Equal(1, entity.uGacha.poolStates[pool.PoolId].total_draw_count);
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task TwoDraws_ConvertDuplicateCharacterToFragments()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(20);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910002);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 2, diamonds: 0);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

            GachaDrawResult first = GM_GachaMgr.Instance.Draw(playerId, pool, 1);
            GachaDrawResult second = GM_GachaMgr.Instance.Draw(playerId, pool, 1);

            Assert.True(Assert.Single(first.Rewards).IsNewCharacter);
            GachaDrawRewardResult duplicate = Assert.Single(second.Rewards);
            Assert.False(duplicate.IsNewCharacter);
            Assert.Equal(930001, duplicate.FragmentItemId);
            Assert.Equal(7, duplicate.FragmentNum);
            Assert.Equal(
                1,
                await GachaMySqlTestEnvironment.ReadScalarAsync(
                    connection,
                    $"SELECT COUNT(*) FROM player_character WHERE player_id={playerId};"));
            Assert.Equal(7, await ItemQuantityAsync(connection, playerId, 930001));
            Assert.Equal(
                2,
                await GachaMySqlTestEnvironment.ReadScalarAsync(
                    connection,
                    $"SELECT COUNT(*) FROM player_gacha_record WHERE player_id={playerId};"));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task TenDraw_UsesTicketsThenDiamonds_AndPersistsEveryReward()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(30);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910003);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 3, diamonds: 35);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

            GachaDrawResult result = GM_GachaMgr.Instance.Draw(playerId, pool, 10);

            Assert.Equal(GachaOperationStatus.Success, result.Status);
            Assert.Equal(3, result.UsedTicketCount);
            Assert.Equal(35, result.UsedDiamondCount);
            Assert.Equal(10, result.Rewards.Count);
            Assert.Equal(10, result.TotalDrawCount);
            Assert.Equal(0, result.TicketQuantity);
            Assert.Equal(0, result.DiamondQuantity);
            Assert.Equal(63, await ItemQuantityAsync(connection, playerId, 930001));
            Assert.Equal(
                10,
                await GachaMySqlTestEnvironment.ReadScalarAsync(
                    connection,
                    $"SELECT COUNT(*) FROM player_gacha_record WHERE player_id={playerId} " +
                    $"AND batch_id={result.BatchId} AND draw_index BETWEEN 1 AND 10;"));
            Assert.Equal(
                10,
                await GachaMySqlTestEnvironment.ReadScalarAsync(
                    connection,
                    $"SELECT total_draw_count FROM player_gacha_state WHERE player_id={playerId} " +
                    $"AND pool_id={pool.PoolId};"));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task InsufficientResources_DoNotCreateRewardsOrPityState()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(40);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910004);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 0, diamonds: 4);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

            GachaDrawResult result = GM_GachaMgr.Instance.Draw(playerId, pool, 1);

            Assert.Equal(GachaOperationStatus.ResourceNotEnough, result.Status);
            Assert.Equal(4, await ItemQuantityAsync(connection, playerId, pool.DiamondItemId));
            Assert.Equal(0, await PlayerRowCountAsync(connection, "player_character", playerId));
            Assert.Equal(0, await PlayerRowCountAsync(connection, "player_gacha_state", playerId));
            Assert.Equal(0, await PlayerRowCountAsync(connection, "player_gacha_record", playerId));
            Assert.Equal(0, await PlayerRowCountAsync(connection, "player_gacha_operation", playerId));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task FailureAfterPayment_RollsBackPaymentCharacterStateAndRecord()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(50);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910005, rarity: 300);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 1, diamonds: 0);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

            GachaDrawResult result = GM_GachaMgr.Instance.Draw(
                playerId, pool, 1, Guid.NewGuid().ToString("N"));

            Assert.Equal(GachaOperationStatus.Failed, result.Status);
            Assert.Equal(1, await ItemQuantityAsync(connection, playerId, pool.TicketItemId));
            Assert.Equal(0, await PlayerRowCountAsync(connection, "player_character", playerId));
            Assert.Equal(0, await PlayerRowCountAsync(connection, "player_gacha_state", playerId));
            Assert.Equal(0, await PlayerRowCountAsync(connection, "player_gacha_record", playerId));
            Assert.Equal(0, await PlayerRowCountAsync(connection, "player_gacha_operation", playerId));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ConcurrentDraws_WithOneTicket_OnlyOneSucceeds()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(60);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreateDeterministicPool(910006);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 1, diamonds: 0);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

            Task<GachaDrawResult> firstTask = Task.Run(() =>
                GM_GachaMgr.Instance.Draw(playerId, pool, 1));
            Task<GachaDrawResult> secondTask = Task.Run(() =>
                GM_GachaMgr.Instance.Draw(playerId, pool, 1));
            GachaDrawResult[] results = await Task.WhenAll(firstTask, secondTask);

            Assert.Single(results, result => result.Status == GachaOperationStatus.Success);
            Assert.Single(results, result => result.Status == GachaOperationStatus.ResourceNotEnough);
            Assert.Equal(0, await ItemQuantityAsync(connection, playerId, pool.TicketItemId));
            Assert.Equal(1, await PlayerRowCountAsync(connection, "player_character", playerId));
            Assert.Equal(1, await PlayerRowCountAsync(connection, "player_gacha_record", playerId));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task PityDraws_ResetAndPersistConfiguredCounters()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(70);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        GachaPoolDefinition pool = CreatePityPool(910007);
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        try
        {
            await SeedItemsAsync(connection, playerId, pool, tickets: 1, diamonds: 0);
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(
                connection,
                "INSERT INTO player_gacha_state" +
                "(player_id,pool_id,small_pity_count,big_pity_count,total_draw_count) " +
                $"VALUES ({playerId},{pool.PoolId},{pool.SmallPityCount - 1},0,29);");
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

            GachaDrawResult result = GM_GachaMgr.Instance.Draw(playerId, pool, 1);

            Assert.Equal(GachaOperationStatus.Success, result.Status);
            Assert.Equal(pool.SmallPityRarity, Assert.Single(result.Rewards).Rarity);
            Assert.Equal(0, result.SmallPityCount);
            Assert.Equal(1, result.BigPityCount);
            Assert.Equal(30, result.TotalDrawCount);
            Assert.Equal(
                0,
                await GachaMySqlTestEnvironment.ReadScalarAsync(
                    connection,
                    $"SELECT small_pity_count FROM player_gacha_state WHERE player_id={playerId} " +
                    $"AND pool_id={pool.PoolId};"));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        }
    }

    private static GachaPoolDefinition CreateDeterministicPool(int poolId, int rarity = 1)
    {
        GachaRewardDefinition reward = GachaTestData.Reward(920001, 930001, 7, rarity, 1);
        return new GachaPoolDefinition
        {
            PoolId = poolId,
            SmallPityRarity = rarity,
            SmallPityCount = 1000,
            BigPityRarity = rarity,
            BigPityCount = 2000,
            TicketItemId = 940001,
            DiamondItemId = 940002,
            DiamondCostPerDraw = 5,
            Rarities = new[] { rarity },
            RarityWeights = new[] { 1 },
            RewardsByRarity = new Dictionary<int, IReadOnlyList<GachaRewardDefinition>>
            {
                [rarity] = new[] { reward }
            }
        };
    }

    private static GachaPoolDefinition CreatePityPool(int poolId)
    {
        IReadOnlyList<GachaRewardDefinition> rarity1 =
            new[] { GachaTestData.Reward(920001, 930001, 7, 1, 100) };
        IReadOnlyList<GachaRewardDefinition> rarity3 =
            new[] { GachaTestData.Reward(920003, 930003, 7, 3, 1) };
        IReadOnlyList<GachaRewardDefinition> rarity4 =
            new[] { GachaTestData.Reward(920004, 930004, 7, 4, 1) };
        return new GachaPoolDefinition
        {
            PoolId = poolId,
            SmallPityRarity = 3,
            SmallPityCount = 30,
            BigPityRarity = 4,
            BigPityCount = 50,
            TicketItemId = 940001,
            DiamondItemId = 940002,
            DiamondCostPerDraw = 5,
            Rarities = new[] { 1, 3, 4 },
            RarityWeights = new[] { 100, 1, 1 },
            RewardsByRarity = new Dictionary<int, IReadOnlyList<GachaRewardDefinition>>
            {
                [1] = rarity1,
                [3] = rarity3,
                [4] = rarity4
            }
        };
    }

    private static Task SeedItemsAsync(
        MySqlConnection connection,
        long playerId,
        GachaPoolDefinition pool,
        long tickets,
        long diamonds) => GachaMySqlTestEnvironment.ExecuteNonQueryAsync(
        connection,
        $"INSERT INTO player_item(player_id,item_id,quantity) VALUES " +
        $"({playerId},{pool.TicketItemId},{tickets})," +
        $"({playerId},{pool.DiamondItemId},{diamonds});");

    private static Task<long> ItemQuantityAsync(
        MySqlConnection connection,
        long playerId,
        int itemId) => GachaMySqlTestEnvironment.ReadScalarAsync(
        connection,
        $"SELECT quantity FROM player_item WHERE player_id={playerId} AND item_id={itemId};");

    private static Task<long> PlayerRowCountAsync(
        MySqlConnection connection,
        string table,
        long playerId) => GachaMySqlTestEnvironment.ReadScalarAsync(
        connection,
        $"SELECT COUNT(*) FROM {table} WHERE player_id={playerId};");
}
