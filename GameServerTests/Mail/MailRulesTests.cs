using Framework.Core.Serializer;
using Game;
using Game.Core.GM_MailMessage;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace GameServerTests.Mail;
public sealed class MailRulesTests
{
    [Fact]
    public void SortUsesUnreadThenUnclaimedThenOtherAndTimeIdDescending()
    {
        var mails = new[] {
            new Mailmessage { id=1,readTime=1,mailType=0,sendTime=100 },
            new Mailmessage { id=2,readTime=1,mailType=1,sendTime=50 },
            new Mailmessage { id=3,mailType=0,sendTime=10 },
            new Mailmessage { id=4,mailType=1,sendTime=10 },
            new Mailmessage { id=5,readTime=1,mailType=1,claimTime=2,sendTime=200 } };
        Assert.Equal(new long[]{4,3,2,5,1},MailRules.Sort(mails).Select(m=>m.id));
        Assert.False(MailRules.CanDelete(mails[1]));Assert.True(MailRules.CanDelete(mails[0]));
        Assert.False(MailRules.IsValid(new Mailmessage {expireTime=100},100));
    }
    [Fact]
    public void SendRequiresExplicitLifetimeAndCorrectAttachmentType()
    {
        Assert.False(MailRules.ValidSend(new MailSendCommand {PlayerId=1,Title="t",Body="b"}));
        Assert.False(MailRules.ValidSend(new MailSendCommand {PlayerId=1,Title="t",Body="b",LifetimeSeconds=1,MailType=1}));
        Assert.True(MailRules.ValidSend(new MailSendCommand {PlayerId=1,Title="t",Body="b",LifetimeSeconds=1}));
        Assert.Throws<ArgumentOutOfRangeException>(()=>GM_MailMsgMgr.Instance.Configure(0));
    }
    [Fact]
    public void ModuleRejectsUnauthenticatedRequestsBeforeAccessingDatabase()
    {
        Assert.Equal((int)Respones.AccountIsNotLogin,MailModule.Instance.HandlerGetMailList(null,new ReqMailList(),0).status);
        Assert.Equal((int)Respones.AccountIsNotLogin,MailModule.Instance.HandlerClaimAllMail(null,new ReqMailClaimAll(),0).status);
    }
    [Fact]
    public void NewAndLegacyMailContractsUseGoogleCodecAndPreserveLongValues()
    {
        var response=new ResMailRead {status=1,mailboxVersion=5_000_000_000,serverTime=2_200_000_000,
            detail=new MailEntryMessage {id=6_000_000_000,title="邮件🌟",body="<link=task>任务</link>",
                attachments=new(){new(){sortOrder=1,itemId=1,quantity=long.MaxValue-1}}}};
        using var stream=new MemoryStream();
        Assert.True(GoogleProtocolCodec.TrySerialize(response,stream,out var error),error);stream.Position=0;
        Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ResMailRead),stream,out var value,out error),error);
        var actual=Assert.IsType<ResMailRead>(value);Assert.Equal(response.detail.body,actual.detail.body);
        Assert.Equal(long.MaxValue-1,Assert.Single(actual.detail.attachments).quantity);
        var legacy=new ResPullingMailMsg {status=1,mailMessages=new(){new(){msgId=7,msgBody="old",status=1,sendTime=123}}};
        using var old=new MemoryStream();Assert.True(GoogleProtocolCodec.TrySerialize(legacy,old,out error),error);old.Position=0;
        Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ResPullingMailMsg),old,out value,out error),error);
        Assert.Equal(7,Assert.Single(Assert.IsType<ResPullingMailMsg>(value).mailMessages).msgId);

        Assert.Equal(110, (int)Cmd.eMailSummaryReq);
        Assert.Equal(111, (int)Cmd.eMailSummaryRes);
        Assert.Equal(122, (int)Cmd.eMailDeleteReadReq);
        Assert.Equal(123, (int)Cmd.eMailDeleteReadRes);
        Assert.DoesNotContain(Enum.GetValues<Cmd>(), command => (int)command == 124);
        Assert.Null(typeof(MailEntryMessage).Assembly.GetType("Game.Datas.Messages.ResMailChanged"));
    }
}
