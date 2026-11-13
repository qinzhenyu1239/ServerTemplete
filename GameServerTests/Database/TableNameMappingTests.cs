using System.Configuration;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Core.Db;
using Game.Datas.DBEntities;
using SqlSugar;

namespace GameServerTests.Database
{
    public sealed class TableNameMappingTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BothConnectionsMapEveryDatabaseEntityToAnExistingSchemaTable(bool auth)
        {
            using var db = CreateClient(auth);
            string schema = File.ReadAllText(FindSchema());
            var tables = Regex.Matches(schema, @"CREATE TABLE IF NOT EXISTS `([^`]+)`")
                .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
            var entities = typeof(Player).Assembly.GetTypes().Where(type =>
                type.IsClass && type.Namespace == typeof(Player).Namespace).ToArray();
            Assert.NotEmpty(entities);
            foreach (var type in entities)
            {
                string table = db.EntityMaintenance.GetEntityInfo(type).DbTableName;
                Assert.Equal(table.ToLowerInvariant(), table);
                Assert.Contains(table, tables);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GeneratedReadAndWriteSqlUsesLowercaseTables(bool auth)
        {
            using var db = CreateClient(auth);
            Assert.Contains("FROM `account`", db.Queryable<Account>().ToSql().Key);
            Assert.Contains("FROM `player`", db.Queryable<Player>().Where(p => p.accountId == 123).ToSql().Key);
            Assert.Contains("FROM `player_item`", db.Queryable<Player_item>().ToSql().Key);
            Assert.Contains("INSERT INTO `player`", db.Insertable(new Player { id = 123, name = "test" }).ToSql().Key);
            Assert.Contains("UPDATE `player`", db.Updateable(new Player { id = 123, name = "test" })
                .UpdateColumns(p => p.name).Where(p => p.id == 123).ToSql().Key);
            Assert.Contains("DELETE FROM `player`", db.Deleteable<Player>().Where(p => p.id == 123).ToSql().Key);
        }

        [Fact]
        public void ExplicitMappingsAndNonDatabaseTypesArePreserved()
        {
            using var db = CreateClient(false);
            Assert.Equal("ExplicitMixedTable", db.EntityMaintenance.GetEntityInfo<ExplicitMappingProbe>().DbTableName);
            Assert.Equal("unannotatedmappingprobe", db.EntityMaintenance.GetEntityInfo<UnannotatedMappingProbe>().DbTableName);
            Assert.Equal(nameof(UnmappedProjection), db.EntityMaintenance.GetEntityInfo<UnmappedProjection>().DbTableName);
            Assert.Equal("mailmessage", db.EntityMaintenance.GetEntityInfo<Mailmessage>().DbTableName);
        }

        private static SqlSugarClient CreateClient(bool auth)
        {
            var service = new DBService();
            const string connection = "Server=127.0.0.1;Port=1;Database=unused;User ID=unused;Password=unused";
            foreach (string field in new[] { "connAuthUserStr", "connGameDataStr" })
                typeof(DBService).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(service, new ConnectionStringSettings(field, connection));
            // Metadata and ToSql never connect; no database installation is required.
            return auth ? service.GetAuthInstance() : service.GetGameInstance();
        }
        private static string FindSchema()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, "sql", "ugame_10001.sql");
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException("Could not locate sql/ugame_10001.sql.");
        }
        private sealed class UnmappedProjection { public long Id { get; set; } }
    }
}

namespace Game.Datas.DBEntities
{
    [SugarTable("ExplicitMixedTable")]
    public sealed class ExplicitMappingProbe { public long Id { get; set; } }
    public sealed class UnannotatedMappingProbe { public long Id { get; set; } }
}
