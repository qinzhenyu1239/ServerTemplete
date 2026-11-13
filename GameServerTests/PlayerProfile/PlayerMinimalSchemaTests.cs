using Game;
using Framework.Core.Utils;
using Game.Core.GM_Bonues;
using Game.Core.GM_PlayerProfile;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using Game.Entries.Modules;
using Game.LogicServer;

namespace GameServerTests.PlayerProfile;

public sealed class PlayerMinimalSchemaTests
{
    [Fact]
    public void PlayerEntityHasTheTenProfileAndProgressColumns()
    {
        Assert.Equal(new[] { "accountId", "avatarId", "baseLevel", "createTime", "id", "mainStage", "name", "resonanceLevel", "status", "towerStage" },
            typeof(Player).GetProperties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void RetiredFeaturesRejectWithoutDatabaseOrClaimStateChanges()
    {
        var module = PlayerModule.Instance;
        Assert.Equal((int)Respones.InvalidOpt, module.HandlerRecvLoginBonues(null, new ReqRecvLoginBonues(), 1).status);
        Assert.Equal((int)Respones.InvalidOpt, module.HandlerReqRecvBonues(null, new ReqRecvBonues(), 1).status);
        Assert.Equal((int)Respones.InvalidOpt, module.HandlerReqExchangeProduct(null, new ReqExchangeProduct(), 1).status);
        Assert.Equal((int)Respones.InvalidOpt, module.HandlerReqPullingRank(null, new ReqPullingRank(), 1).status);
        Assert.Equal((int)Respones.InvalidOpt, ZoneRule.ParseCondGE(null, "ulevel", "1"));
        Assert.Equal((int)Respones.InvalidOpt, ZoneRule.ParseCondGE(null, "uchip", "1"));
        var reward = new Bonues { uid = 1, tid = 100001, status = 0, bonues1 = 100 };
        Assert.False(GM_BonuesMgr.Instance.RecvBonues(1, reward));
        Assert.Equal(0, reward.status);
        Assert.False(new PlayerProfileUpdate { Fields = new[] { ProfileField.Avatar, ProfileField.Signature }, AvatarId = 9, Signature = "x" }.IsValid());
    }
}
