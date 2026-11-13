using System.Collections.Concurrent;
using System.Reflection;
using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Core.GM_PlayerProfile;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.Entries.Modules;
using GameServerTests.Gacha;

namespace GameServerTests.PlayerProfile;

public sealed class PlayerBasicInfoTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Player Player(long id) => new()
    {
        id = id, accountId = id + 1, avatarId = 4, name = "测试玩家", createTime = 1_792_000_000,
        baseLevel = 12, mainStage = 23, resonanceLevel = 34, towerStage = 45
    };

    private static GM_PlayerEntity Online(Player player)
    {
        var entity = new GM_PlayerEntity();
        entity.uPlayer.playerInfo = player;
        var players = (ConcurrentDictionary<long, GM_PlayerEntity>)typeof(GM_EntityMgr)
            .GetField("players", Private).GetValue(GM_EntityMgr.Instance);
        players[player.id] = entity;
        return entity;
    }

    private static void Attach(Player player) => typeof(GM_AccountEntityMgr)
        .GetMethod("AttachPlayer", Private).Invoke(GM_AccountEntityMgr.Instance, new object[] { player });

    [Fact]
    public void ModuleRejectsUnauthenticatedAndInvalidRequests()
    {
        Assert.Equal((int)Respones.AccountIsNotLogin,
            PlayerModule.Instance.HandlerGetPlayerBasicInfo(null, new ReqGetPlayerBasicInfo(), 0).status);
        var session = new IdSession { accountId = 1, playerId = 2 };
        Assert.Equal((int)Respones.InvalidParams,
            PlayerModule.Instance.HandlerGetPlayerBasicInfo(session, null, 2).status);
        Assert.Equal((int)Respones.AccountIsNotLogin,
            PlayerModule.Instance.HandlerGetPlayerBasicInfo(session, new ReqGetPlayerBasicInfo(), 3).status);
    }

    [Fact]
    public void OnlineCacheWinsAndResponseIsAnIndependentSnapshot()
    {
        GM_EntityMgr.Instance.Init();
        GM_AccountEntityMgr.Instance.Init();
        var player = Player(903001);
        Online(player);
        var stale = Player(player.id); stale.name = "stale"; Attach(stale);
        try
        {
            var result = GM_PlayerProfileMgr.Instance.GetBasicInfo(player.id);
            Assert.Equal(PlayerProfileStatus.Success, result.Status);
            Assert.Equal(player.name, result.Info.Name);
            var session = new IdSession { accountId = player.accountId, playerId = player.id };
            var response = PlayerModule.Instance.HandlerGetPlayerBasicInfo(session, new ReqGetPlayerBasicInfo(), player.id);
            Assert.Equal((int)Respones.OK, response.status);
            Assert.Equal(0, response.guildId);
            Assert.Equal(player.avatarId, response.avatarId);
            Assert.Equal(player.baseLevel, response.baseLevel);
            Assert.Equal(player.mainStage, response.mainStage);
            Assert.Equal(player.resonanceLevel, response.resonanceLevel);
            Assert.Equal(player.towerStage, response.towerStage);
            Assert.Equal(0, response.titleId);
            Assert.Equal(player.createTime, response.createTime);
            Assert.Equal("", response.signature);
            Assert.Equal("[0,0,0,0,0,0,0]", response.displayAchievementIds);
            Assert.Equal("[0,0,0,0,0,0]", response.displayCharacterIds);
            player.name = "changed";
            Assert.Equal("测试玩家", result.Info.Name);
        }
        finally { GM_EntityMgr.Instance.RemovePlayer(player.id); GM_AccountEntityMgr.Instance.Init(); }
    }

    [Fact]
    public void AccountCacheFillsMissingOnlineComponentWithoutDatabase()
    {
        GM_EntityMgr.Instance.Init(); GM_AccountEntityMgr.Instance.Init();
        var player = Player(903002);
        var entity = Online(player); entity.uPlayer.playerInfo = null;
        Attach(player);
        try
        {
            var result = GM_PlayerProfileMgr.Instance.GetBasicInfo(player.id);
            Assert.Equal(PlayerProfileStatus.Success, result.Status);
            Assert.Same(player, entity.uPlayer.playerInfo);
            GM_EntityMgr.Instance.RemovePlayer(player.id);
            Assert.Equal(player.name, GM_PlayerProfileMgr.Instance.GetBasicInfo(player.id).Info.Name);
        }
        finally { GM_EntityMgr.Instance.RemovePlayer(player.id); GM_AccountEntityMgr.Instance.Init(); }
    }

    [Fact]
    public void MissingDatabaseReturnsFailureInsteadOfCreatingPlayer()
    {
        GM_EntityMgr.Instance.Init(); GM_AccountEntityMgr.Instance.Init();
        var field = typeof(DBService).GetField("connGameDataStr", Private);
        object previous = field.GetValue(DBService.Instance);
        try
        {
            field.SetValue(DBService.Instance, null);
            Assert.Equal(PlayerProfileStatus.Failed, GM_PlayerProfileMgr.Instance.GetBasicInfo(903003).Status);
            Assert.Equal(PlayerProfileStatus.NotFound, GM_PlayerProfileMgr.Instance.GetBasicInfo(0).Status);
        }
        finally { field.SetValue(DBService.Instance, previous); }
    }

    [Fact]
    public void ProtocolPreservesLongIdsUnicodeAndRequestIdAndSkipsUnknownFields()
    {
        Assert.Equal(91, (int)Cmd.eGetPlayerBasicInfoReq);
        Assert.Equal(92, (int)Cmd.eGetPlayerBasicInfoRes);
        Assert.Equal(102, new ReqGetPlayerBasicInfo().GetModule());
        var response = new ResGetPlayerBasicInfo
        {
            status = 1, playerId = 9_000_000_001, avatarId = 4, name = "测试玩家",
            guildId = 8_000_000_001, createTime = 3_000_000_000, titleId = 7, signature = "你好 🌟", baseLevel = 12, mainStage = 23, resonanceLevel = 34, towerStage = 45
        };
        byte[] packet = MessagePusher.PackMessage(response, 0, requestId: 9_000_000_002);
        Assert.Equal(9_000_000_002, UtilsHelper.ReadLongLE(packet, ProtocolPacketLayout.RequestIdOffset));
        using var body = new MemoryStream(packet, ProtocolPacketLayout.HeaderLength,
            packet.Length - ProtocolPacketLayout.HeaderLength);
        var protobuf = ProtoBuf.Serializer.Deserialize<ResGetPlayerBasicInfo>(body);
        Assert.Equal(response.guildId, protobuf.guildId);
        Assert.Equal(response.signature, protobuf.signature);
        using var encoded = new MemoryStream();
        ProtoBuf.Serializer.Serialize(encoded, response);
        encoded.Write(new byte[] { 0xA0, 0x06, 0x01 }); // Unknown field 100.
        encoded.Position = 0;
        Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ResGetPlayerBasicInfo), encoded, out var decoded, out var error), error);
        var actual = Assert.IsType<ResGetPlayerBasicInfo>(decoded);
        Assert.Equal(response.playerId, actual.playerId);
        Assert.Equal(response.createTime, actual.createTime);
        Assert.Equal(response.name, actual.name);
        Assert.Equal(response.baseLevel, actual.baseLevel);
        Assert.Equal(response.mainStage, actual.mainStage);
        Assert.Equal(response.resonanceLevel, actual.resonanceLevel);
        Assert.Equal(response.towerStage, actual.towerStage);
        Assert.Equal(response.signature, actual.signature);
        using var empty = new MemoryStream();
        Assert.True(GoogleProtocolCodec.TrySerialize(new ReqGetPlayerBasicInfo(), empty, out error), error);
        empty.Position = 0;
        Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ReqGetPlayerBasicInfo), empty, out decoded, out error), error);
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task DatabaseFallbackFillsCacheAndMissingPlayerReturnsNotFound()
    {
        long playerId = Math.Abs(GachaMySqlTestEnvironment.PlayerId(903));
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        await using var connection = await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
        GM_AccountEntityMgr.Instance.Init();
        try
        {
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
                $"INSERT INTO player(id,accountId,name,avatarId,createTime) " +
                $"VALUES ({playerId},{playerId},'profile-test',4,1792000000)");
            var result = GM_PlayerProfileMgr.Instance.GetBasicInfo(playerId);
            Assert.Equal(PlayerProfileStatus.Success, result.Status);
            Assert.Equal(0, result.Info.GuildId);
            Assert.Equal("", result.Info.Signature);
            Assert.Equal(1, result.Info.BaseLevel); Assert.Equal(1, result.Info.MainStage);
            Assert.Equal(0, result.Info.ResonanceLevel); Assert.Equal(0, result.Info.TowerStage);
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection, $"DELETE FROM player WHERE id={playerId}");
            Assert.Equal(PlayerProfileStatus.Success, GM_PlayerProfileMgr.Instance.GetBasicInfo(playerId).Status);
            GM_AccountEntityMgr.Instance.Init();
            Assert.Equal(PlayerProfileStatus.NotFound, GM_PlayerProfileMgr.Instance.GetBasicInfo(playerId).Status);
        }
        finally
        {
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection, $"DELETE FROM player WHERE id={playerId}");
            GM_AccountEntityMgr.Instance.Init();
        }
    }
}
