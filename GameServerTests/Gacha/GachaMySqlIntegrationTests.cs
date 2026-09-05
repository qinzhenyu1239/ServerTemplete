using System.Xml.Linq;
using MySqlConnector;

namespace GameServerTests.Gacha;

public sealed class GachaMySqlIntegrationTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Transactions_RollBackAtomically_AndConcurrentDebitSucceedsOnlyOnce()
    {
        string connectionString = ReadConnectionString();
        long playerId = ReadPlayerId();
        bool keepDatabaseData = string.Equals(
            Environment.GetEnvironmentVariable("GACHA_KEEP_DATABASE_DATA"),
            "1",
            StringComparison.Ordinal);
        const int characterId = 10001;
        const int poolId = 1;

        await using var setupConnection = new MySqlConnection(connectionString);
        await setupConnection.OpenAsync();
        try
        {
            await VerifyRollbackAsync(setupConnection, playerId, characterId, poolId);
            await VerifyConcurrentDebitAsync(setupConnection, connectionString, playerId);
            await CreateInspectableSnapshotAsync(setupConnection, playerId, poolId);
        }
        finally
        {
            if (!keepDatabaseData)
            {
                await ExecuteNonQueryAsync(
                    setupConnection,
                    null,
                    $"DELETE FROM player_gacha_record WHERE player_id={playerId};" +
                    $"DELETE FROM player_character WHERE player_id={playerId};" +
                    $"DELETE FROM player_gacha_state WHERE player_id={playerId};" +
                    $"DELETE FROM player_item WHERE player_id={playerId};");
            }
        }
    }

    private static async Task VerifyRollbackAsync(
        MySqlConnection connection,
        long playerId,
        int characterId,
        int poolId)
    {
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"INSERT INTO player_item(player_id,item_id,quantity) VALUES ({playerId},90001,10),({playerId},91001,0);");
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"INSERT INTO player_gacha_state(player_id,pool_id) VALUES ({playerId},{poolId});");
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"UPDATE player_item SET quantity=quantity-1 WHERE player_id={playerId} AND item_id=90001 AND quantity>=1;");
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"INSERT INTO player_character(player_id,character_id,star) VALUES ({playerId},{characterId},0);");
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"UPDATE player_item SET quantity=quantity+1 WHERE player_id={playerId} AND item_id=91001;");
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"UPDATE player_gacha_state SET small_pity_count=1,big_pity_count=1,total_draw_count=1 " +
            $"WHERE player_id={playerId} AND pool_id={poolId};");
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO player_gacha_record" +
            "(id,batch_id,draw_index,player_id,pool_id,character_id,rarity,is_new_character) " +
            $"VALUES ({Math.Abs(playerId)},{Math.Abs(playerId)},1,{playerId},{poolId},{characterId},1,1);");
        await transaction.RollbackAsync();

        foreach (string table in new[]
                 {
                     "player_item", "player_character", "player_gacha_state", "player_gacha_record"
                 })
        {
            long count = await ReadScalarAsync(
                connection,
                $"SELECT COUNT(*) FROM {table} WHERE player_id={playerId};");
            Assert.Equal(0, count);
        }
    }

    private static async Task VerifyConcurrentDebitAsync(
        MySqlConnection setupConnection,
        string connectionString,
        long playerId)
    {
        await ExecuteNonQueryAsync(
            setupConnection,
            null,
            $"INSERT INTO player_item(player_id,item_id,quantity) VALUES ({playerId},90001,1);");

        await using var firstConnection = new MySqlConnection(connectionString);
        await using var secondConnection = new MySqlConnection(connectionString);
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();
        await using MySqlTransaction firstTransaction = await firstConnection.BeginTransactionAsync();
        await using MySqlTransaction secondTransaction = await secondConnection.BeginTransactionAsync();

        int firstAffected = await ExecuteNonQueryAsync(
            firstConnection,
            firstTransaction,
            $"UPDATE player_item SET quantity=quantity-1 WHERE player_id={playerId} " +
            "AND item_id=90001 AND quantity>=1;");

        await using MySqlCommand secondCommand = secondConnection.CreateCommand();
        secondCommand.Transaction = secondTransaction;
        secondCommand.CommandText =
            $"UPDATE player_item SET quantity=quantity-1 WHERE player_id={playerId} " +
            "AND item_id=90001 AND quantity>=1;";
        Task<int> secondTask = secondCommand.ExecuteNonQueryAsync();

        await Task.Delay(100);
        await firstTransaction.CommitAsync();
        int secondAffected = await secondTask;
        await secondTransaction.CommitAsync();

        Assert.Equal(1, firstAffected + secondAffected);
        Assert.Equal(
            0,
            await ReadScalarAsync(
                setupConnection,
                $"SELECT quantity FROM player_item WHERE player_id={playerId} AND item_id=90001;"));
    }

    private static async Task CreateInspectableSnapshotAsync(
        MySqlConnection connection,
        long playerId,
        int poolId)
    {
        long recordId = Math.Abs(playerId);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"INSERT INTO player_item(player_id,item_id,quantity) VALUES " +
            $"({playerId},10001,9),({playerId},10002,100),({playerId},30001,0);");
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"INSERT INTO player_character(player_id,character_id,star) VALUES ({playerId},20001,0);");
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO player_gacha_state" +
            "(player_id,pool_id,small_pity_count,big_pity_count,total_draw_count) " +
            $"VALUES ({playerId},{poolId},1,1,1);");
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO player_gacha_record" +
            "(id,batch_id,draw_index,player_id,pool_id,character_id,rarity,is_new_character," +
            "fragment_item_id,fragment_num,small_pity_count,big_pity_count,created_at) " +
            $"VALUES ({recordId},{recordId},1,{playerId},{poolId},20001,1,1,0,0,1,1,UTC_TIMESTAMP());");
        await transaction.CommitAsync();

        Assert.Equal(
            1,
            await ReadScalarAsync(
                connection,
                $"SELECT COUNT(*) FROM player_gacha_record WHERE player_id={playerId};"));
    }

    private static string ReadConnectionString()
    {
        string configurationPath =
            Environment.GetEnvironmentVariable("GACHA_DB_CONFIGURATION_PATH");
        if (string.IsNullOrWhiteSpace(configurationPath))
            throw new InvalidOperationException("GACHA_DB_CONFIGURATION_PATH is not set.");

        XDocument document = XDocument.Load(configurationPath);
        string connectionString = document
            .Descendants("add")
            .FirstOrDefault(element =>
                string.Equals(
                    (string)element.Attribute("name"),
                    "connGameDataStr",
                    StringComparison.Ordinal))
            ?.Attribute("connectionString")
            ?.Value;
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException("connGameDataStr is missing from the configuration file.")
            : connectionString;
    }

    private static long ReadPlayerId()
    {
        string value = Environment.GetEnvironmentVariable("GACHA_TEST_PLAYER_ID");
        if (!long.TryParse(value, out long playerId) || playerId >= 0)
            throw new InvalidOperationException("GACHA_TEST_PLAYER_ID must be a negative integer.");
        return playerId;
    }

    private static async Task<int> ExecuteNonQueryAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string sql)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ReadScalarAsync(MySqlConnection connection, string sql)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object value = await command.ExecuteScalarAsync();
        return Convert.ToInt64(value);
    }
}
