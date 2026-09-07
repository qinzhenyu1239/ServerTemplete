using System.Collections.Concurrent;
using System.Configuration;
using System.Reflection;
using System.Xml.Linq;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Datas.GMEntities;
using MySqlConnector;

namespace GameServerTests.Gacha;

internal static class GachaMySqlTestEnvironment
{
    internal static bool KeepDatabaseData => string.Equals(
        Environment.GetEnvironmentVariable("GACHA_KEEP_DATABASE_DATA"),
        "1",
        StringComparison.Ordinal);

    internal static long PlayerId(int offset)
    {
        string value = Environment.GetEnvironmentVariable("GACHA_TEST_PLAYER_ID");
        if (!long.TryParse(value, out long basePlayerId) || basePlayerId >= 0)
            throw new InvalidOperationException("GACHA_TEST_PLAYER_ID must be a negative integer.");
        return checked(basePlayerId - offset);
    }

    internal static string ReadConnectionString()
    {
        string configurationPath = ReadConfigurationPath();
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
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("connGameDataStr is missing from the configuration file.");

        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowUserVariables = true
        };
        return builder.ConnectionString;
    }

    internal static void InitializeServerServices(string connectionString)
    {
        AppContext.SetData("APP_CONFIG_FILE", ReadConfigurationPath());
        ConfigurationManager.RefreshSection("appSettings");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo connectionField = typeof(DBService).GetField("connGameDataStr", flags)
            ?? throw new MissingFieldException(typeof(DBService).FullName, "connGameDataStr");
        connectionField.SetValue(
            DBService.Instance,
            new ConnectionStringSettings("connGameDataStr", connectionString));
        GM_EntityMgr.Instance.Init();
    }

    internal static GM_PlayerEntity AddOnlinePlayer(
        long playerId,
        IReadOnlyDictionary<int, long> quantities)
    {
        var entity = new GM_PlayerEntity();
        entity.uItem.quantities = new Dictionary<int, long>(quantities);
        entity.uCharacter.characterIds = new HashSet<int>();
        entity.uCharacter.characterStars = new Dictionary<int, byte>();
        entity.uGacha.isLoaded = true;
        entity.uGacha.poolStates = new Dictionary<int, Game.Datas.DBEntities.Player_gacha_state>();

        FieldInfo playersField = typeof(GM_EntityMgr).GetField(
            "players",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(GM_EntityMgr).FullName, "players");
        var players = (ConcurrentDictionary<long, GM_PlayerEntity>)playersField.GetValue(
            GM_EntityMgr.Instance);
        Assert.True(players.TryAdd(playerId, entity));
        return entity;
    }

    internal static async Task<MySqlConnection> OpenConnectionAsync(string connectionString)
    {
        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    internal static async Task ResetPlayerAsync(MySqlConnection connection, long playerId)
    {
        await ExecuteNonQueryAsync(
            connection,
            $"DELETE FROM player_gacha_operation WHERE player_id={playerId};" +
            $"DELETE FROM player_character_starup_operation WHERE player_id={playerId};" +
            $"DELETE FROM player_gacha_record WHERE player_id={playerId};" +
            $"DELETE FROM player_character WHERE player_id={playerId};" +
            $"DELETE FROM player_gacha_state WHERE player_id={playerId};" +
            $"DELETE FROM player_item WHERE player_id={playerId};");
    }

    internal static async Task ExecuteNonQueryAsync(MySqlConnection connection, string sql)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task<long> ReadScalarAsync(MySqlConnection connection, string sql)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object value = await command.ExecuteScalarAsync();
        return Convert.ToInt64(value);
    }

    private static string ReadConfigurationPath()
    {
        string configurationPath =
            Environment.GetEnvironmentVariable("GACHA_DB_CONFIGURATION_PATH");
        return string.IsNullOrWhiteSpace(configurationPath)
            ? throw new InvalidOperationException("GACHA_DB_CONFIGURATION_PATH is not set.")
            : configurationPath;
    }
}
