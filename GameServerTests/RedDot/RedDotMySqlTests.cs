using DotNetty.Transport.Channels.Embedded;
using Framework.Core.Concurrency;
using Framework.Core.Net;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Core.GM_RedDot;
using Game.Datas.Excels;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.Entries.Modules;
using GameServerTests.Gacha;
using MySqlConnector;
using System.Reflection;

namespace GameServerTests.RedDot;

public sealed class RedDotMySqlTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task IsolatedDatabase_GenericBusinessRollbackViewedPersistenceAndMigration()
    {
        string database = "codex_reddot_" + Guid.NewGuid().ToString("N");
        var builder = new MySqlConnectionStringBuilder(GachaMySqlTestEnvironment.ReadConnectionString()) { Database = "" };
        await using var admin = new MySqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await Execute(admin, $"CREATE DATABASE `{database}` CHARACTER SET utf8mb4");
        builder.Database = database;
        var channel = new EmbeddedChannel();
        const long playerId = 910001;
        var connectionField = typeof(DBService).GetField("connGameDataStr", BindingFlags.Instance | BindingFlags.NonPublic);
        object previousConnection = connectionField.GetValue(DBService.Instance);
        try
        {
            await using var db = new MySqlConnection(builder.ConnectionString);
            await db.OpenAsync();
            await Execute(db, "CREATE TABLE player(id BIGINT PRIMARY KEY);" +
                "CREATE TABLE testcondition(playerId BIGINT PRIMARY KEY,active TINYINT NOT NULL);");
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null && !File.Exists(Path.Combine(current.FullName, "sql/ugame_10001.sql"))) current = current.Parent;
            string migration = await File.ReadAllTextAsync(Path.Combine(current.FullName, "sql/migrations/20261023_001_reddot.sql"));
            await Execute(db, migration); await Execute(db, migration);
            // The generic migration must not require a mail table.
            Assert.Equal(0, await Scalar(db, "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage'"));
            await Execute(db, $"INSERT INTO player(id) VALUES({playerId}); INSERT INTO testcondition VALUES({playerId},0)");
            GachaMySqlTestEnvironment.InitializeServerServices(builder.ConnectionString);
            var entity = GachaMySqlTestEnvironment.AddOnlinePlayer(playerId, new Dictionary<int, long>());
            var session = new IdSession { playerId = playerId, accountId = playerId, client = channel };
            entity.uPlayer.s = session;
            var catalog = new RedDotCatalog(new[]
            {
                new RedDotConfig { id = 9000, name = "测试业务", enabled = true },
                new RedDotConfig { id = 9001, name = "测试条件", parentId = 9000, clearType = 1, enabled = true },
                new RedDotConfig { id = 3000, name = "测试查看", enabled = true },
                new RedDotConfig { id = 3001, name = "提示", parentId = 3000, clearType = 2, enabled = true, tipVersion = 5 }
            });
            Configure(catalog);
            int evaluations = 0;
            GM_RedDotMgr.Instance.RegisterBusiness(9001, (transaction, id) =>
            {
                evaluations++;
                return transaction.Ado.SqlQuery<int>("SELECT active FROM testcondition WHERE playerId=@pid", new { pid = id }).Single() == 1;
            });
            var initial = RedDotModule.Instance.Initialize(session, new ReqRedDotInit(), playerId);
            Assert.Equal(1, initial.status);
            Assert.False(initial.states.Single(s => s.nodeId == 9001).isActive);
            Assert.False(initial.states.Single(s => s.nodeId == 3001).isActive);
            Assert.Equal(0, evaluations);
            Assert.Equal(0, await Scalar(db, "SELECT COUNT(*) FROM playerreddot"));
            // Even when the DB rejects writes, initialization may load and return existing records unchanged.
            await Execute(db, $"INSERT INTO playerreddot VALUES({playerId},3001,0,3,123)");
            await Execute(db, "CREATE TRIGGER reject_init_insert BEFORE INSERT ON playerreddot FOR EACH ROW " +
                "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='initialization must not insert'");
            await Execute(db, "CREATE TRIGGER reject_init_update BEFORE UPDATE ON playerreddot FOR EACH ROW " +
                "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='initialization must not update'");
            RedDotComponent.Reset(entity);
            var readOnly = RedDotModule.Instance.Initialize(session, new ReqRedDotInit(), playerId);
            Assert.Equal(1, readOnly.status);
            Assert.False(readOnly.states.Single(s => s.nodeId == 3001).isActive);
            Assert.Equal(3, await Scalar(db, "SELECT seenVersion FROM playerreddot WHERE nodeId=3001"));
            Assert.Equal(123, await Scalar(db, "SELECT updateTime FROM playerreddot WHERE nodeId=3001"));
            Assert.Equal(0, evaluations);
            Assert.Equal(1, await Scalar(db, "SELECT COUNT(*) FROM playerreddot"));
            await Execute(db, "DROP TRIGGER reject_init_insert; DROP TRIGGER reject_init_update");
            Assert.Equal(0, await Scalar(db, "SELECT COUNT(*) FROM playerreddot WHERE nodeId IN(9000,3000)"));
            SetBusiness(playerId, true);
            Assert.Equal(1, evaluations);
            Assert.Equal(1, await Scalar(db, "SELECT isActive FROM playerreddot WHERE nodeId=9001"));
            Assert.True(entity.uRedDot.StateVersion > initial.stateVersion);
            await Execute(db, "CREATE TRIGGER reject_reddot BEFORE UPDATE ON playerreddot FOR EACH ROW " +
                "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='test rollback'");
            Assert.ThrowsAny<Exception>(() => SetBusiness(playerId, false));
            Assert.Equal(1, await Scalar(db, "SELECT active FROM testcondition"));
            Assert.Equal(1, await Scalar(db, "SELECT isActive FROM playerreddot WHERE nodeId=9001"));
            await Execute(db, "DROP TRIGGER reject_reddot");
            SetBusiness(playerId, false);
            Assert.Equal(0, await Scalar(db, "SELECT isActive FROM playerreddot WHERE nodeId=9001"));
            Assert.Equal(-103, RedDotModule.Instance.MarkViewed(session,
                new ReqRedDotView { nodeId = 9001, tipVersion = 1 }, playerId).status);
            var stale = RedDotModule.Instance.MarkViewed(session, new ReqRedDotView { nodeId = 3001, tipVersion = 3 }, playerId);
            Assert.True(stale.states.Single(s => s.nodeId == 3001).isActive);
            Assert.Equal(-103, RedDotModule.Instance.MarkViewed(session,
                new ReqRedDotView { nodeId = 3001, tipVersion = 6 }, playerId).status);
            var viewed = RedDotModule.Instance.MarkViewed(session, new ReqRedDotView { nodeId = 3001, tipVersion = 5 }, playerId);
            Assert.False(viewed.states.Single(s => s.nodeId == 3001).isActive);
            RedDotComponent.Reset(entity);
            Assert.False(RedDotModule.Instance.Initialize(session, new ReqRedDotInit(), playerId)
                .states.Single(s => s.nodeId == 3001).isActive);
            catalog.ById[3001].tipVersion = 6;
            Configure(new RedDotCatalog(catalog.Nodes));
            Assert.False(RedDotModule.Instance.Initialize(session, new ReqRedDotInit(), playerId)
                .states.Single(s => s.nodeId == 3001).isActive);
            // A new configuration version alone does not activate a red dot; the business event does.
            using (var transaction = DBService.Instance.GetGameInstance())
            {
                transaction.Ado.BeginTran();
                GM_RedDotMgr.LockPlayer(transaction, playerId);
                GM_RedDotMgr.Instance.WriteViewedState(transaction, playerId, 3001);
                transaction.Ado.CommitTran();
            }
            GM_RedDotMgr.Instance.PublishCommitted(playerId);
            Assert.True(RedDotModule.Instance.Initialize(session, new ReqRedDotInit(), playerId)
                .states.Single(s => s.nodeId == 3001).isActive);
        }
        finally
        {
            GM_EntityMgr.Instance.RemovePlayer(playerId);
            connectionField.SetValue(DBService.Instance, previousConnection);
            await Execute(admin, $"DROP DATABASE `{database}`");
            channel.FinishAndReleaseAll();
        }
    }
    private static void Configure(RedDotCatalog catalog) => GM_RedDotMgr.Instance.Configure(catalog, snapshot =>
        new NotifyRedDotChanged { configVersion = snapshot.ConfigVersion, stateVersion = snapshot.StateVersion,
            states = snapshot.States.Select(s => new RedDotStateMessage { nodeId = s.NodeId, isActive = s.IsActive }).ToList() });
    private static void SetBusiness(long playerId, bool active)
    {
        using var lease = PlayerOperationLock.Acquire(playerId);
        lock (lease.SyncRoot)
        {
            using var db = DBService.Instance.GetGameInstance();
            try
            {
                db.Ado.BeginTran(); GM_RedDotMgr.LockPlayer(db, playerId);
                db.Ado.ExecuteCommand("UPDATE testcondition SET active=@active WHERE playerId=@pid", new { active, pid = playerId });
                GM_RedDotMgr.Instance.RefreshBusinessState(db, playerId, 9001);
                db.Ado.CommitTran();
            }
            catch { db.Ado.RollbackTran(); throw; }
            GM_RedDotMgr.Instance.PublishCommitted(playerId);
        }
    }
    private static async Task Execute(MySqlConnection db, string sql)
    { using var command = new MySqlCommand(sql, db); await command.ExecuteNonQueryAsync(); }
    private static async Task<long> Scalar(MySqlConnection db, string sql)
    { using var command = new MySqlCommand(sql, db); return Convert.ToInt64(await command.ExecuteScalarAsync()); }
}
