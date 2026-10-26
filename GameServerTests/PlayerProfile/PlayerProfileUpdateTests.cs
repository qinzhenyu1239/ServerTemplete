using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game;
using Game.Core.EntityMgr;
using Game.Core.GM_PlayerProfile;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.Entries.Modules;
using GameServerTests.Gacha;

namespace GameServerTests.PlayerProfile;

public sealed class PlayerProfileUpdateTests
{
    [Fact]
    public void PartialUpdateValidationPreservesZeroAndClearSemantics()
    {
        Assert.True(new PlayerProfileUpdate { Fields = new[] { ProfileField.Avatar }, AvatarId = 0 }.IsValid());
        Assert.True(new PlayerProfileUpdate { Fields = new[] { ProfileField.Signature }, Signature = "" }.IsValid());
        Assert.False(new PlayerProfileUpdate { Fields = new[] { ProfileField.Avatar } }.IsValid());
        Assert.False(new PlayerProfileUpdate { Fields = new[] { ProfileField.Avatar, ProfileField.Avatar }, AvatarId = 1 }.IsValid());
        Assert.False(new PlayerProfileUpdate { Fields = new[] { (ProfileField)99 } }.IsValid());
        Assert.False(new PlayerProfileUpdate { Fields = Array.Empty<ProfileField>() }.IsValid());
        Assert.False(new PlayerProfileUpdate { Fields = new[] { ProfileField.Avatar }, AvatarId = -1 }.IsValid());
        Assert.True(PlayerProfileUpdate.ValidSignature(string.Concat(Enumerable.Repeat("🌟", 255))));
        Assert.False(PlayerProfileUpdate.ValidSignature(new string('a', 256)));
        Assert.False(PlayerProfileUpdate.ValidSignature("\ud800"));
        Assert.True(PlayerProfileUpdate.ValidSlots(new long[] { 9000000001, 0, 0, 0, 0, 0 }, 6));
        Assert.False(PlayerProfileUpdate.ValidSlots(new long[] { 1, 1, 0, 0, 0, 0 }, 6));
        Assert.False(PlayerProfileUpdate.ValidSlots(new long[] { -1, 0, 0, 0, 0, 0 }, 6));
        Assert.False(PlayerProfileUpdate.ValidSlots(new long[7], 6));
    }

    [Fact]
    public void ControllerModuleRejectsUnauthenticatedAndMalformedWrites()
    {
        Assert.Equal((int)Respones.AccountIsNotLogin,
            PlayerModule.Instance.HandlerUpdatePlayerBasicInfo(null, new ReqUpdatePlayerBasicInfo(), 1).status);
        var session = new IdSession { accountId = 1, playerId = 2 };
        Assert.Equal((int)Respones.InvalidParams, PlayerModule.Instance.HandlerUpdatePlayerBasicInfo(session,
            new ReqUpdatePlayerBasicInfo { updateFields = new[] { PlayerProfileField.Characters }, displayCharacterIds = "[0,0,0,0,0]" }, 2).status);
        Assert.Equal((int)Respones.InvalidParams, PlayerModule.Instance.HandlerUpdatePlayerBasicInfo(session, null, 2).status);
        foreach (string invalid in new[] { "bad-json", "{}", "[\"bad\"]", "null" })
            Assert.Equal((int)Respones.InvalidParams, PlayerModule.Instance.HandlerUpdatePlayerBasicInfo(session,
                new ReqUpdatePlayerBasicInfo { updateFields = new[] { PlayerProfileField.Characters }, displayCharacterIds = invalid }, 2).status);
    }

    [Fact]
    public void BothCodecsPreserveNullableZeroJsonStringsAndLongIds()
    {
        Assert.Equal(93, (int)Cmd.eUpdatePlayerBasicInfoReq);
        Assert.Equal(94, (int)Cmd.eUpdatePlayerBasicInfoRes);
        var expected = new ReqUpdatePlayerBasicInfo
        {
            updateFields = new[] { PlayerProfileField.Avatar, PlayerProfileField.Characters, PlayerProfileField.Signature },
            avatarId = 0, signature = "", displayCharacterIds = "[9000000001,0,0,0,0,0]"
        };
        using var stream = new MemoryStream();
        Assert.True(GoogleProtocolCodec.TrySerialize(expected, stream, out var error), error);
        stream.Position = 0;
        var protobuf = ProtoBuf.Serializer.Deserialize<ReqUpdatePlayerBasicInfo>(stream);
        Assert.Equal(0, protobuf.avatarId); Assert.Null(protobuf.titleId);
        Assert.Equal(expected.displayCharacterIds, protobuf.displayCharacterIds);
        Assert.Equal(expected.updateFields, protobuf.updateFields); Assert.Equal("", protobuf.signature);
        using var packed = new MemoryStream();
        ProtoBuf.Serializer.Serialize(packed, expected);
        packed.Position = 0;
        Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ReqUpdatePlayerBasicInfo), packed, out var decoded, out error), error);
        var actual = Assert.IsType<ReqUpdatePlayerBasicInfo>(decoded);
        Assert.Equal(expected.updateFields, actual.updateFields); Assert.Equal(expected.displayCharacterIds, actual.displayCharacterIds);
        Assert.Equal(0, actual.avatarId); Assert.Null(actual.titleId);
        using var response = new MemoryStream();
        Assert.True(GoogleProtocolCodec.TrySerialize(new ResUpdatePlayerBasicInfo
        {
            status = (int)Respones.OK, playerId = 9000000002, avatarFrameId = 4, backgroundId = 5,
            displayAchievementIds = "[1,2,0,0,0,0,0]", displayCharacterIds = expected.displayCharacterIds
        }, response, out error), error);
        response.Position = 0;
        var updated = ProtoBuf.Serializer.Deserialize<ResUpdatePlayerBasicInfo>(response);
        Assert.Equal(9000000002, updated.playerId); Assert.Equal(4, updated.avatarFrameId);
        Assert.Equal(expected.displayCharacterIds, updated.displayCharacterIds);
    }

    [Fact]
    public void QueryCopiesSlotsAndRejectsCorruptStoredJson()
    {
        GM_EntityMgr.Instance.Init();
        long id = 909123;
        var player = new Player { id = id, accountId = 1, avatarFrameId = 3, backgroundId = 4,
            displayAchievementIds = "[1,2,0,0,0,0,0]", displayCharacterIds = "[9000000001,0,0,0,0,0]" };
        var entity = new GM_PlayerEntity(); entity.uPlayer.playerInfo = player;
        var players = (ConcurrentDictionary<long, GM_PlayerEntity>)typeof(GM_EntityMgr)
            .GetField("players", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GM_EntityMgr.Instance);
        players[id] = entity;
        try
        {
            var first = GM_PlayerProfileMgr.Instance.GetBasicInfo(id);
            Assert.Equal(3, first.Info.AvatarFrameId); Assert.Equal(9000000001, first.Info.DisplayCharacterIds[0]);
            first.Info.DisplayAchievementIds[0] = 99;
            Assert.Equal(1, GM_PlayerProfileMgr.Instance.GetBasicInfo(id).Info.DisplayAchievementIds[0]);
            player.displayCharacterIds = "[1]";
            Assert.Equal(PlayerProfileStatus.Failed, GM_PlayerProfileMgr.Instance.GetBasicInfo(id).Status);
        }
        finally { players.TryRemove(id, out _); }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task DatabaseUpdateIsPartialRepeatableAndValidatesOwnershipBeforeWriting()
    {
        long id = -GachaMySqlTestEnvironment.PlayerId(990);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        await using var connection = await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        GachaMySqlTestEnvironment.InitializeServerServices(connectionString);
        GM_AccountEntityMgr.Instance.Init();
        try
        {
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
                $"INSERT INTO player(id,accountId,name,avatarId,titleId,signature) VALUES({id},{id},'profile-write-test',3,4,'before')");
            Player cached = GM_AccountEntityMgr.Instance.GetPlayerById(id);
            var command = new PlayerProfileUpdate { Fields = new[] { ProfileField.Avatar, ProfileField.Signature }, AvatarId = 0, Signature = "" };
            Assert.Equal(PlayerProfileStatus.Success, GM_PlayerProfileMgr.Instance.UpdateBasicInfo(id, id, command).Status);
            Assert.Equal(PlayerProfileStatus.Success, GM_PlayerProfileMgr.Instance.UpdateBasicInfo(id, id, command).Status);
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
                $"INSERT INTO player_character(player_id,character_id,star) VALUES({id},123456,0)");
            long instanceId = await GachaMySqlTestEnvironment.ReadScalarAsync(connection,
                $"SELECT id FROM player_character WHERE player_id={id}");
            Assert.Same(cached, GM_AccountEntityMgr.Instance.GetPlayerById(id));
            Assert.Equal(0, cached.avatarId); Assert.Equal("", cached.signature);
            var slots = new PlayerProfileUpdate
            {
                Fields = new[] { ProfileField.Characters, ProfileField.Achievements, ProfileField.AvatarFrame, ProfileField.Background },
                DisplayCharacterIds = new long[] { instanceId, 0, 0, 0, 0, 0 },
                DisplayAchievementIds = new[] { 11, 22, 0, 0, 0, 0, 0 }, AvatarFrameId = 5, BackgroundId = 6
            };
            Assert.Equal(PlayerProfileStatus.Success, GM_PlayerProfileMgr.Instance.UpdateBasicInfo(id, id, slots).Status);
            var saved = GM_PlayerProfileMgr.Instance.GetBasicInfo(id).Info;
            Assert.Equal(instanceId, saved.DisplayCharacterIds[0]); Assert.Equal(22, saved.DisplayAchievementIds[1]);
            Assert.Equal(5, saved.AvatarFrameId); Assert.Equal(6, saved.BackgroundId);
            Assert.Equal(4, GM_PlayerProfileMgr.Instance.GetBasicInfo(id).Info.TitleId);
            Assert.Equal(0, GM_PlayerProfileMgr.Instance.GetBasicInfo(id).Info.AvatarId);
            Assert.Equal("", GM_PlayerProfileMgr.Instance.GetBasicInfo(id).Info.Signature);
            Assert.Equal(PlayerProfileStatus.InvalidInput, GM_PlayerProfileMgr.Instance.UpdateBasicInfo(id, id,
                new PlayerProfileUpdate { Fields = new[] { ProfileField.Avatar, ProfileField.Characters }, AvatarId = 99,
                    DisplayCharacterIds = new long[] { long.MaxValue, 0, 0, 0, 0, 0 } }).Status);
            Assert.Equal(0, await GachaMySqlTestEnvironment.ReadScalarAsync(connection, $"SELECT avatarId FROM player WHERE id={id}"));
            Assert.Equal(0, GM_PlayerProfileMgr.Instance.GetBasicInfo(id).Info.AvatarId);
            Assert.Equal(PlayerProfileStatus.NotFound, GM_PlayerProfileMgr.Instance.UpdateBasicInfo(id, id + 1, command).Status);
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection, $"UPDATE player SET displayAchievementIds='bad-json' WHERE id={id}");
            Assert.Equal(PlayerProfileStatus.Failed, GM_PlayerProfileMgr.Instance.UpdateBasicInfo(id, id,
                new PlayerProfileUpdate { Fields = new[] { ProfileField.Avatar }, AvatarId = 99 }).Status);
            Assert.Equal(0, await GachaMySqlTestEnvironment.ReadScalarAsync(connection, $"SELECT avatarId FROM player WHERE id={id}"));
        }
        finally
        {
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection, $"DELETE FROM player_character WHERE player_id={id}; DELETE FROM player WHERE id={id}");
            GM_AccountEntityMgr.Instance.Init();
        }
    }
}
