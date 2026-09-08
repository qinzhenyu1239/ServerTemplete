using Game.Core.GM_Formation;
using GameServerTests.Gacha;
using MySqlConnector;

namespace GameServerTests.Formation;

public sealed class FormationManagerMySqlIntegrationTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task SaveDuplicateConflictAndOverview_AreConsistent()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(301);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await CleanupAsync(connection, playerId);
        try
        {
            await SeedCharactersAsync(connection, playerId, 1001, 1002);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            var requested = new[]
            {
                new FormationSlotValue { SlotIndex = 0, CharacterId = 1001 },
                new FormationSlotValue { SlotIndex = 8, CharacterId = 1002 }
            };

            SaveFormationResult first = GM_FormationMgr.Instance.Save(playerId, 0, 4, 0, requested);
            Assert.Equal(FormationStatus.Success, first.Status);
            Assert.Equal(1, first.Formation.Version);

            SaveFormationResult duplicate = GM_FormationMgr.Instance.Save(playerId, 0, 4, 0, requested);
            Assert.Equal(FormationStatus.Success, duplicate.Status);
            Assert.Equal(1, duplicate.Formation.Version);

            SaveFormationResult conflict = GM_FormationMgr.Instance.Save(playerId, 0, 4, 0,
                new[] { new FormationSlotValue { SlotIndex = 1, CharacterId = 1001 } });
            Assert.Equal(FormationStatus.VersionConflict, conflict.Status);
            Assert.Equal(1, conflict.Formation.Version);
            Assert.Equal(2, conflict.Formation.Slots.Count);

            FormationOverviewResult overview = GM_FormationMgr.Instance.GetOverview(playerId);
            Assert.Equal(FormationStatus.Success, overview.Status);
            Assert.Equal(15, overview.Formations.Count);
            Assert.Equal(2, overview.Characters.Count);
            PlayerFormationSnapshot stored = overview.Formations.Single(
                formation => formation.ModuleId == 0 && formation.FormationId == 4);
            Assert.Equal(1, stored.Version);
            Assert.Equal(new[] { 0, 8 }, stored.Slots.Select(slot => slot.SlotIndex));
        }
        finally { await CleanupAsync(connection, playerId); }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ConcurrentDifferentSnapshots_OnlyOneWinsExpectedVersion()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(302);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await CleanupAsync(connection, playerId);
        try
        {
            await SeedCharactersAsync(connection, playerId, 1001, 1002);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            SaveFormationResult[] results = await Task.WhenAll(
                Task.Run(() => GM_FormationMgr.Instance.Save(playerId, 1, 0, 0,
                    new[] { new FormationSlotValue { SlotIndex = 0, CharacterId = 1001 } })),
                Task.Run(() => GM_FormationMgr.Instance.Save(playerId, 1, 0, 0,
                    new[] { new FormationSlotValue { SlotIndex = 1, CharacterId = 1002 } })));
            Assert.Single(results.Where(result => result.Status == FormationStatus.Success));
            Assert.Single(results.Where(result => result.Status == FormationStatus.VersionConflict));
        }
        finally { await CleanupAsync(connection, playerId); }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task FailureAfterSlotReplacement_RollsBackSlotsAndVersion()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(303);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await CleanupAsync(connection, playerId);
        try
        {
            await SeedCharactersAsync(connection, playerId, 1001, 1002);
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
            Assert.Equal(FormationStatus.Success, GM_FormationMgr.Instance.Save(playerId, 0, 0, 0,
                new[] { new FormationSlotValue { SlotIndex = 0, CharacterId = 1001 } }).Status);

            SaveFormationResult failed = GM_FormationMgr.Instance.SaveForTest(playerId, 0, 0, 1,
                new[] { new FormationSlotValue { SlotIndex = 1, CharacterId = 1002 } },
                () => throw new InvalidOperationException("forced"));
            Assert.Equal(FormationStatus.Failed, failed.Status);

            PlayerFormationSnapshot stored = GM_FormationMgr.Instance.GetOverview(playerId).Formations
                .Single(formation => formation.ModuleId == 0 && formation.FormationId == 0);
            Assert.Equal(1, stored.Version);
            Assert.Single(stored.Slots);
            Assert.Equal(0, stored.Slots[0].SlotIndex);
            Assert.Equal(1001, stored.Slots[0].CharacterId);
        }
        finally { await CleanupAsync(connection, playerId); }
    }

    private static async Task SeedCharactersAsync(MySqlConnection connection, long playerId,
        params int[] characterIds)
    {
        foreach (int characterId in characterIds)
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
                $"INSERT INTO player_character (player_id, character_id, star) " +
                $"VALUES ({playerId}, {characterId}, 0);");
    }

    private static Task CleanupAsync(MySqlConnection connection, long playerId) =>
        GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
            $"DELETE FROM player_formation_slot WHERE player_id={playerId};" +
            $"DELETE FROM player_formation WHERE player_id={playerId};" +
            $"DELETE FROM player_character WHERE player_id={playerId};");
}
