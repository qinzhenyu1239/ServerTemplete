using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Game.Core.GM_Gacha;
using GameServerTests.Gacha;
using MySqlConnector;

namespace GameServerTests.Performance;

public sealed class GachaPerformanceTests
{
    [PerformanceFact]
    [Trait("Category", "Performance")]
    public async Task RunConfiguredCapacityAndLoadScenario()
    {
        PerformanceOptions options = PerformanceOptions.FromEnvironment();
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        long historyPlayerId = GachaMySqlTestEnvironment.PlayerId(1000);
        long samePlayerId = GachaMySqlTestEnvironment.PlayerId(3000);
        long[] loadPlayerIds = Enumerable.Range(0, options.ConcurrentPlayers)
            .Select(index => GachaMySqlTestEnvironment.PlayerId(2000 + index))
            .ToArray();
        long[] allPlayerIds = new[] { historyPlayerId, samePlayerId }
            .Concat(loadPlayerIds)
            .ToArray();

        await using MySqlConnection connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await CleanupPlayersAsync(connection, allPlayerIds);
        try
        {
            Stopwatch indexTimer = Stopwatch.StartNew();
            bool historyIndexApplied = await ApplyHistoryIndexWhenRequestedAsync(connection);
            indexTimer.Stop();

            Stopwatch seedTimer = Stopwatch.StartNew();
            await SeedHistoryAsync(
                connection,
                historyPlayerId,
                options.RecordCount,
                options.InsertBatchSize);
            seedTimer.Stop();

            HistoryMeasurements history = await MeasureHistoryQueriesAsync(
                connection,
                historyPlayerId,
                options.RecordCount);
            LoadMeasurements distinctPlayers = await MeasureDistinctPlayerDrawsAsync(
                connection,
                connectionString,
                loadPlayerIds,
                options.DrawsPerPlayer);
            LoadMeasurements samePlayer = await MeasureSamePlayerDrawsAsync(
                connection,
                connectionString,
                samePlayerId,
                options.ConcurrentPlayers);

            var report = new
            {
                generatedAtUtc = DateTime.UtcNow,
                database = new MySqlConnectionStringBuilder(connectionString).Database,
                options.RecordCount,
                options.ConcurrentPlayers,
                options.DrawsPerPlayer,
                historyIndexApplied,
                indexMigrationMilliseconds = indexTimer.Elapsed.TotalMilliseconds,
                historySeedMilliseconds = seedTimer.Elapsed.TotalMilliseconds,
                history,
                distinctPlayers,
                samePlayer,
                retainedData = GachaMySqlTestEnvironment.KeepDatabaseData,
                playerIds = new
                {
                    historyPlayerId,
                    distinctPlayerIdStart = loadPlayerIds.Min(),
                    distinctPlayerIdEnd = loadPlayerIds.Max(),
                    samePlayerId
                }
            };
            await WriteReportAsync(options.ReportPath, report);

            Assert.Equal(options.RecordCount, history.RowCount);
            if (historyIndexApplied)
            {
                Assert.Equal(
                    "player_id,created_at,id",
                    history.IndexColumns,
                    ignoreCase: true,
                    ignoreLineEndingDifferences: false,
                    ignoreWhiteSpaceDifferences: true);
            }
            Assert.Equal(
                options.ConcurrentPlayers * options.DrawsPerPlayer,
                distinctPlayers.SuccessCount);
            Assert.Equal(options.ConcurrentPlayers, samePlayer.SuccessCount);
            if (options.MaxP95Milliseconds > 0)
            {
                Assert.True(
                    distinctPlayers.P95Milliseconds <= options.MaxP95Milliseconds,
                    $"Distinct-player P95 {distinctPlayers.P95Milliseconds:F2}ms exceeded " +
                    $"{options.MaxP95Milliseconds:F2}ms.");
            }
            if (options.MaxRecentQueryMilliseconds > 0)
            {
                Assert.True(
                    history.RecentPageAverageMilliseconds <= options.MaxRecentQueryMilliseconds,
                    $"Recent-page average {history.RecentPageAverageMilliseconds:F2}ms exceeded " +
                    $"{options.MaxRecentQueryMilliseconds:F2}ms.");
            }
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
            {
                await CleanupPlayersAsync(connection, allPlayerIds);
                Assert.Equal(0, await CountPlayerRowsAsync(connection, allPlayerIds));
            }
        }
    }

    private static async Task SeedHistoryAsync(
        MySqlConnection connection,
        long playerId,
        int recordCount,
        int batchSize)
    {
        long firstId = checked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100_000L);
        for (int start = 0; start < recordCount; start += batchSize)
        {
            int count = Math.Min(batchSize, recordCount - start);
            var sql = new StringBuilder(
                "INSERT INTO player_gacha_record" +
                "(id,batch_id,draw_index,player_id,pool_id,character_id,rarity,is_new_character," +
                "fragment_item_id,fragment_num,small_pity_count,big_pity_count,created_at) VALUES ");
            for (int offset = 0; offset < count; offset++)
            {
                if (offset > 0) sql.Append(',');
                int index = start + offset;
                long id = firstId + index;
                sql.Append('(')
                    .Append(id).Append(',')
                    .Append(id).Append(",1,")
                    .Append(playerId)
                    .Append(",920000,920001,1,0,930001,7,")
                    .Append(index % 30).Append(',')
                    .Append(index % 50)
                    .Append(",DATE_ADD('2026-01-01 00:00:00', INTERVAL ")
                    .Append(index % 2_000_000)
                    .Append(" SECOND))");
            }
            sql.Append(';');
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection, sql.ToString());
        }
    }

    private static async Task<bool> ApplyHistoryIndexWhenRequestedAsync(
        MySqlConnection connection)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GACHA_PERF_APPLY_HISTORY_INDEX"),
                "1",
                StringComparison.Ordinal))
        {
            return false;
        }

        string migrationPath = Environment.GetEnvironmentVariable(
            "GACHA_PERF_HISTORY_INDEX_MIGRATION_PATH");
        if (string.IsNullOrWhiteSpace(migrationPath))
            throw new InvalidOperationException("History index migration path is not set.");

        string sql = await File.ReadAllTextAsync(migrationPath);
        await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection, sql);
        await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(
            connection,
            "ANALYZE TABLE player_gacha_record;");
        return true;
    }

    private static async Task<HistoryMeasurements> MeasureHistoryQueriesAsync(
        MySqlConnection connection,
        long playerId,
        int expectedRows)
    {
        long rowCount = await GachaMySqlTestEnvironment.ReadScalarAsync(
            connection,
            $"SELECT COUNT(*) FROM player_gacha_record WHERE player_id={playerId};");

        const int repetitions = 20;
        Stopwatch recentTimer = Stopwatch.StartNew();
        for (int index = 0; index < repetitions; index++)
        {
            await ReadPageAsync(
                connection,
                $"SELECT id,created_at FROM player_gacha_record WHERE player_id={playerId} " +
                "ORDER BY created_at DESC,id DESC LIMIT 20;");
        }
        recentTimer.Stop();

        (long lastId, DateTime lastTime) = await ReadLastCursorAsync(connection, playerId);
        Stopwatch keysetTimer = Stopwatch.StartNew();
        for (int index = 0; index < repetitions; index++)
        {
            await ReadPageAsync(
                connection,
                $"SELECT id,created_at FROM player_gacha_record WHERE player_id={playerId} " +
                $"AND (created_at<'{lastTime:yyyy-MM-dd HH:mm:ss}' OR " +
                $"(created_at='{lastTime:yyyy-MM-dd HH:mm:ss}' AND id<{lastId})) " +
                "ORDER BY created_at DESC,id DESC LIMIT 20;");
        }
        keysetTimer.Stop();

        int offset = Math.Max(0, expectedRows - 20);
        Stopwatch offsetTimer = Stopwatch.StartNew();
        await ReadPageAsync(
            connection,
            $"SELECT id,created_at FROM player_gacha_record WHERE player_id={playerId} " +
            $"ORDER BY created_at DESC,id DESC LIMIT 20 OFFSET {offset};");
        offsetTimer.Stop();

        string selectedIndex = await ReadExplainIndexAsync(connection, playerId);
        string indexColumns = await ReadIndexColumnsAsync(connection, selectedIndex);
        return new HistoryMeasurements
        {
            RowCount = rowCount,
            RecentPageAverageMilliseconds = recentTimer.Elapsed.TotalMilliseconds / repetitions,
            KeysetPageAverageMilliseconds = keysetTimer.Elapsed.TotalMilliseconds / repetitions,
            DeepOffsetPageMilliseconds = offsetTimer.Elapsed.TotalMilliseconds,
            SelectedIndex = selectedIndex,
            IndexColumns = indexColumns
        };
    }

    private static async Task<LoadMeasurements> MeasureDistinctPlayerDrawsAsync(
        MySqlConnection setupConnection,
        string connectionString,
        IReadOnlyList<long> playerIds,
        int drawsPerPlayer)
    {
        GachaPoolDefinition pool = CreateLoadPool(920001);
        foreach (long playerId in playerIds)
        {
            await SeedTicketsAsync(setupConnection, playerId, pool.TicketItemId, drawsPerPlayer);
        }
        GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

        var latencies = new ConcurrentBag<double>();
        int successCount = 0;
        Stopwatch total = Stopwatch.StartNew();
        await Task.WhenAll(playerIds.Select(playerId => Task.Run(() =>
        {
            for (int draw = 0; draw < drawsPerPlayer; draw++)
            {
                Stopwatch request = Stopwatch.StartNew();
                GachaDrawResult result = GM_GachaMgr.Instance.Draw(playerId, pool, 1);
                request.Stop();
                latencies.Add(request.Elapsed.TotalMilliseconds);
                if (result.Status == GachaOperationStatus.Success)
                    Interlocked.Increment(ref successCount);
            }
        })));
        total.Stop();
        return BuildLoadMeasurements(successCount, latencies, total.Elapsed);
    }

    private static async Task<LoadMeasurements> MeasureSamePlayerDrawsAsync(
        MySqlConnection setupConnection,
        string connectionString,
        long playerId,
        int concurrentRequests)
    {
        GachaPoolDefinition pool = CreateLoadPool(920002);
        await SeedTicketsAsync(setupConnection, playerId, pool.TicketItemId, concurrentRequests);
        GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

        var latencies = new ConcurrentBag<double>();
        int successCount = 0;
        Stopwatch total = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, concurrentRequests).Select(_ => Task.Run(() =>
        {
            Stopwatch request = Stopwatch.StartNew();
            GachaDrawResult result = GM_GachaMgr.Instance.Draw(playerId, pool, 1);
            request.Stop();
            latencies.Add(request.Elapsed.TotalMilliseconds);
            if (result.Status == GachaOperationStatus.Success)
                Interlocked.Increment(ref successCount);
        })));
        total.Stop();
        return BuildLoadMeasurements(successCount, latencies, total.Elapsed);
    }

    private static LoadMeasurements BuildLoadMeasurements(
        int successCount,
        IEnumerable<double> samples,
        TimeSpan elapsed)
    {
        double[] sorted = samples.OrderBy(value => value).ToArray();
        double percentile(double fraction) => sorted.Length == 0
            ? 0
            : sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * fraction) - 1)];
        return new LoadMeasurements
        {
            SuccessCount = successCount,
            TotalMilliseconds = elapsed.TotalMilliseconds,
            RequestsPerSecond = elapsed.TotalSeconds <= 0 ? 0 : successCount / elapsed.TotalSeconds,
            AverageMilliseconds = sorted.Length == 0 ? 0 : sorted.Average(),
            P95Milliseconds = percentile(0.95),
            P99Milliseconds = percentile(0.99),
            MaximumMilliseconds = sorted.Length == 0 ? 0 : sorted[^1]
        };
    }

    private static GachaPoolDefinition CreateLoadPool(int poolId)
    {
        GachaRewardDefinition reward = GachaTestData.Reward(950001, 960001, 1, 1, 1);
        return new GachaPoolDefinition
        {
            PoolId = poolId,
            SmallPityRarity = 1,
            SmallPityCount = 1000,
            BigPityRarity = 1,
            BigPityCount = 2000,
            TicketItemId = 970001,
            DiamondItemId = 970002,
            DiamondCostPerDraw = 1,
            Rarities = new[] { 1 },
            RarityWeights = new[] { 1 },
            RewardsByRarity = new Dictionary<int, IReadOnlyList<GachaRewardDefinition>>
            {
                [1] = new[] { reward }
            }
        };
    }

    private static Task SeedTicketsAsync(
        MySqlConnection connection,
        long playerId,
        int ticketItemId,
        int quantity) => GachaMySqlTestEnvironment.ExecuteNonQueryAsync(
        connection,
        $"INSERT INTO player_item(player_id,item_id,quantity) VALUES " +
        $"({playerId},{ticketItemId},{quantity});");

    private static async Task CleanupPlayersAsync(
        MySqlConnection connection,
        IReadOnlyList<long> playerIds)
    {
        foreach (long[] chunk in playerIds.Distinct().Chunk(200))
        {
            string ids = string.Join(',', chunk);
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(
                connection,
                $"DELETE FROM player_gacha_record WHERE player_id IN ({ids});" +
                $"DELETE FROM player_character WHERE player_id IN ({ids});" +
                $"DELETE FROM player_gacha_state WHERE player_id IN ({ids});" +
                $"DELETE FROM player_item WHERE player_id IN ({ids});");
        }
    }

    private static async Task ReadPageAsync(MySqlConnection connection, string sql)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using MySqlDataReader reader = await command.ExecuteReaderAsync();
        int count = 0;
        while (await reader.ReadAsync()) count++;
        Assert.InRange(count, 1, 20);
    }

    private static async Task<(long Id, DateTime CreatedAt)> ReadLastCursorAsync(
        MySqlConnection connection,
        long playerId)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT id,created_at FROM player_gacha_record WHERE player_id={playerId} " +
            "ORDER BY created_at DESC,id DESC LIMIT 20;";
        await using MySqlDataReader reader = await command.ExecuteReaderAsync();
        long id = 0;
        DateTime createdAt = default;
        while (await reader.ReadAsync())
        {
            id = reader.GetInt64(0);
            createdAt = reader.GetDateTime(1);
        }
        return (id, createdAt);
    }

    private static async Task<string> ReadExplainIndexAsync(
        MySqlConnection connection,
        long playerId)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText =
            $"EXPLAIN SELECT id,created_at FROM player_gacha_record WHERE player_id={playerId} " +
            "ORDER BY created_at DESC,id DESC LIMIT 20;";
        await using MySqlDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return string.Empty;
        int keyOrdinal = reader.GetOrdinal("key");
        return reader.IsDBNull(keyOrdinal) ? string.Empty : reader.GetString(keyOrdinal);
    }

    private static async Task<string> ReadIndexColumnsAsync(
        MySqlConnection connection,
        string indexName)
    {
        if (string.IsNullOrWhiteSpace(indexName)) return string.Empty;
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') " +
            "FROM information_schema.statistics WHERE table_schema=DATABASE() " +
            "AND table_name='player_gacha_record' AND index_name=@indexName;";
        command.Parameters.AddWithValue("@indexName", indexName);
        object value = await command.ExecuteScalarAsync();
        return value == null || value == DBNull.Value ? string.Empty : Convert.ToString(value);
    }

    private static async Task WriteReportAsync(string path, object report)
    {
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Performance report path has no directory.");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
    }

    private static async Task<long> CountPlayerRowsAsync(
        MySqlConnection connection,
        IReadOnlyList<long> playerIds)
    {
        long count = 0;
        foreach (long[] chunk in playerIds.Distinct().Chunk(200))
        {
            string ids = string.Join(',', chunk);
            foreach (string table in new[]
                     {
                         "player_item", "player_character", "player_gacha_state", "player_gacha_record"
                     })
            {
                count += await GachaMySqlTestEnvironment.ReadScalarAsync(
                    connection,
                    $"SELECT COUNT(*) FROM {table} WHERE player_id IN ({ids});");
            }
        }
        return count;
    }

    private sealed class PerformanceOptions
    {
        public int RecordCount { get; init; }
        public int ConcurrentPlayers { get; init; }
        public int DrawsPerPlayer { get; init; }
        public int InsertBatchSize { get; init; }
        public double MaxP95Milliseconds { get; init; }
        public double MaxRecentQueryMilliseconds { get; init; }
        public string ReportPath { get; init; }

        public static PerformanceOptions FromEnvironment() => new()
        {
            RecordCount = ReadInt("GACHA_PERF_RECORD_COUNT", 100_000),
            ConcurrentPlayers = ReadInt("GACHA_PERF_CONCURRENT_PLAYERS", 32),
            DrawsPerPlayer = ReadInt("GACHA_PERF_DRAWS_PER_PLAYER", 10),
            InsertBatchSize = ReadInt("GACHA_PERF_INSERT_BATCH_SIZE", 500),
            MaxP95Milliseconds = ReadDouble("GACHA_PERF_MAX_P95_MS"),
            MaxRecentQueryMilliseconds = ReadDouble("GACHA_PERF_MAX_RECENT_QUERY_MS"),
            ReportPath = Environment.GetEnvironmentVariable("GACHA_PERF_REPORT_PATH")
                ?? throw new InvalidOperationException("GACHA_PERF_REPORT_PATH is not set.")
        };

        private static int ReadInt(string name, int defaultValue) =>
            int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0
                ? value
                : defaultValue;

        private static double ReadDouble(string name) =>
            double.TryParse(Environment.GetEnvironmentVariable(name), out double value) && value > 0
                ? value
                : 0;
    }

    private sealed class HistoryMeasurements
    {
        public long RowCount { get; init; }
        public double RecentPageAverageMilliseconds { get; init; }
        public double KeysetPageAverageMilliseconds { get; init; }
        public double DeepOffsetPageMilliseconds { get; init; }
        public string SelectedIndex { get; init; }
        public string IndexColumns { get; init; }
    }

    private sealed class LoadMeasurements
    {
        public int SuccessCount { get; init; }
        public double TotalMilliseconds { get; init; }
        public double RequestsPerSecond { get; init; }
        public double AverageMilliseconds { get; init; }
        public double P95Milliseconds { get; init; }
        public double P99Milliseconds { get; init; }
        public double MaximumMilliseconds { get; init; }
    }
}
