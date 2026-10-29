using System.Reflection;
using System.Text.RegularExpressions;
using DotNetty.Transport.Channels.Embedded;
using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Core.GM_MailMessage;
using Game.Core.Maintenance;
using Game.Datas.GMEntities;
using Game.Datas.Excels;
using Game.Datas.Messages;
using Game.Entries.Modules;
using GameServerTests.Gacha;
using MySqlConnector;

namespace GameServerTests.Mail;
public sealed class MailMySqlTests
{
    [DatabaseFact, Trait("Category","Database")]
    public async Task ClaimAllMarksOrdinaryUnreadMailAcrossPagesEvenWithoutRewards() => await Run(async e=>
    {
        for (int i = 0; i < 13; i++) e.Send("ordinary-page-" + i);
        var expired = e.Send("excluded-expired");
        var deleted = e.Send("excluded-deleted");
        var foreign = e.Send("excluded-other-player");
        await e.Sql($"UPDATE mailmessage SET expireTime=UNIX_TIMESTAMP()-1 WHERE id={expired.MailId};" +
            $"UPDATE mailmessage SET deleteTime=UNIX_TIMESTAMP(),deleteReason=1 WHERE id={deleted.MailId};" +
            $"UPDATE mailmessage SET toPlayerId={e.PlayerId+1} WHERE id={foreign.MailId}");

        var response = MailModule.Instance.HandlerClaimAllMail(e.Session, new ReqMailClaimAll(), e.PlayerId);

        Assert.Equal((int)Game.Respones.MailNoRewards, response.status);
        Assert.Equal(13, response.count);
        Assert.Equal(0, response.unreadCount);
        Assert.Equal(10, response.items.Count);
        Assert.True(response.hasMore);
        Assert.All(response.items, mail => Assert.True(mail.readTime > 0));
        Assert.Empty(response.itemQuantities);
        Assert.Equal(13, await e.Scalar($"SELECT COUNT(*) FROM mailmessage WHERE toPlayerId={e.PlayerId} " +
            "AND title LIKE 'ordinary-page-%' AND readTime>0"));
        Assert.Equal(0, await e.Scalar($"SELECT readTime FROM mailmessage WHERE id={expired.MailId}"));
        Assert.Equal(0, await e.Scalar($"SELECT readTime FROM mailmessage WHERE id={deleted.MailId}"));
        Assert.Equal(0, await e.Scalar($"SELECT readTime FROM mailmessage WHERE id={foreign.MailId}"));

        var page = MailModule.Instance.HandlerGetMailList(e.Session, new ReqMailList { offset = 10, pageSize = 50 }, e.PlayerId);
        Assert.Equal(3, page.items.Count);
        Assert.All(page.items, mail => Assert.True(mail.readTime > 0));
        Assert.Equal(0, page.unreadCount);
        Assert.Equal(0, await e.Scalar($"SELECT COUNT(*) FROM player_item WHERE player_id={e.PlayerId}"));
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task ClaimAllMarksOrdinaryAndPreviouslyClaimedUnreadMailWithoutRegranting() => await Run(async e=>
    {
        var ordinary = e.Send("ordinary-unread");
        var reward = e.Send("reward-unread", true);
        var claimed = e.Send("previously-claimed", true);
        Assert.Equal(MailStatus.Success, GM_MailMsgMgr.Instance.Execute(e.PlayerId, MailAction.Claim, claimed.MailId).Status);
        // Legacy data can contain claimed rewards whose read flag was not populated.
        await e.Sql($"UPDATE mailmessage SET readTime=0 WHERE id={claimed.MailId}");

        var response = MailModule.Instance.HandlerClaimAllMail(e.Session, new ReqMailClaimAll(), e.PlayerId);

        Assert.Equal((int)Game.Respones.OK, response.status);
        Assert.Equal(0, response.unreadCount);
        Assert.Equal(3, response.items.Count);
        Assert.All(response.items, mail => Assert.True(mail.readTime > 0));
        Assert.Equal(0, await e.Scalar($"SELECT claimTime FROM mailmessage WHERE id={ordinary.MailId}"));
        Assert.True(await e.Scalar($"SELECT claimTime FROM mailmessage WHERE id={reward.MailId}") > 0);
        Assert.Equal(10, await e.Scalar($"SELECT quantity FROM player_item WHERE player_id={e.PlayerId} AND item_id=1"));
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task MailChangesReturnStateThroughResponsesWithoutPushing() => await Run(async e=>
    {
        var channel = Assert.IsType<EmbeddedChannel>(e.Session.client);
        void NoPush()
        {
            channel.RunPendingTasks();
            Assert.Null(channel.ReadOutbound<object>());
        }

        var initial = MailModule.Instance.HandlerGetMailSummary(e.Session, new ReqMailSummary(), e.PlayerId);
        var sent = e.Send("manual-refresh-reward", true);
        Assert.Equal(MailStatus.Success, sent.Status);
        NoPush();

        var summary = MailModule.Instance.HandlerGetMailSummary(e.Session, new ReqMailSummary(), e.PlayerId);
        Assert.Equal(1, summary.count);
        Assert.Equal(1, summary.unreadCount);
        Assert.True(summary.mailboxVersion > initial.mailboxVersion);
        NoPush();

        var read = MailModule.Instance.HandlerReadMail(e.Session, new ReqMailRead { mailId = sent.MailId }, e.PlayerId);
        Assert.Equal(0, read.unreadCount);
        Assert.True(read.mailboxVersion > summary.mailboxVersion);
        Assert.Equal(5, Assert.Single(read.detail.attachments).quantity);
        NoPush();

        var claim = MailModule.Instance.HandlerClaimMail(e.Session, new ReqMailClaim { mailId = sent.MailId }, e.PlayerId);
        Assert.Equal((int)Game.Respones.OK, claim.status);
        Assert.True(claim.mailboxVersion > read.mailboxVersion);
        Assert.Equal(5, Assert.Single(claim.itemQuantities).quantity);
        NoPush();

        var repeated = MailModule.Instance.HandlerClaimMail(e.Session, new ReqMailClaim { mailId = sent.MailId }, e.PlayerId);
        Assert.Equal(claim.mailboxVersion, repeated.mailboxVersion);
        Assert.Equal(5, await e.Scalar($"SELECT quantity FROM player_item WHERE player_id={e.PlayerId} AND item_id=1"));
        NoPush();

        var deleted = MailModule.Instance.HandlerDeleteMail(e.Session, new ReqMailDelete { mailId = sent.MailId }, e.PlayerId);
        Assert.Equal(0, deleted.count);
        Assert.True(deleted.mailboxVersion > claim.mailboxVersion);
        NoPush();

        var expired = e.Send("manual-refresh-expired", true);
        await e.Sql($"UPDATE mailmessage SET expireTime=UNIX_TIMESTAMP()-1 WHERE id={expired.MailId}");
        GM_MailMsgMgr.Instance.ExpireDue();
        Assert.Equal(2, await e.Scalar($"SELECT deleteReason FROM mailmessage WHERE id={expired.MailId}"));
        Assert.Equal(0, MailModule.Instance.HandlerGetMailSummary(e.Session, new ReqMailSummary(), e.PlayerId).count);
        NoPush();
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task NewPlayerTestMailsIncludeClaimableRewardsAndDoNotDuplicateOnRetry() => await Run(async e=>
    {
        e.ItemConfigs["10001"] = new Item { id = 10001 };
        e.ItemConfigs["10002"] = new Item { id = 10002 };
        Assert.Equal(10, Game.Debug.MailDebugTool.SendNewPlayerMails(e.PlayerId));
        Assert.Equal(10, await e.Scalar($"SELECT COUNT(*) FROM mailmessage WHERE toPlayerId={e.PlayerId}"));
        Assert.Equal(10, await e.Scalar($"SELECT COUNT(*) FROM mailmessage WHERE toPlayerId={e.PlayerId} " +
            "AND readTime=0 AND claimTime=0 AND deleteTime=0 AND expireTime-sendTime=86400"));
        Assert.Equal(7, await e.Scalar($"SELECT COUNT(*) FROM mailmessage WHERE toPlayerId={e.PlayerId} AND mailType=0"));
        Assert.Equal(3, await e.Scalar($"SELECT COUNT(*) FROM mailmessage WHERE toPlayerId={e.PlayerId} AND mailType=1"));
        Assert.Equal(4, await e.Scalar("SELECT COUNT(*) FROM mailattachment"));
        Assert.Equal(16, await e.Scalar("SELECT SUM(quantity) FROM mailattachment WHERE itemId=10001"));
        Assert.Equal(20, await e.Scalar("SELECT SUM(quantity) FROM mailattachment WHERE itemId=10002"));
        Assert.Equal(0, await e.Scalar($"SELECT COUNT(*) FROM mailmessage WHERE toPlayerId={e.PlayerId+1}"));
        Assert.Equal(0, await e.Scalar($"SELECT COUNT(*) FROM player_item WHERE player_id={e.PlayerId}"));

        Assert.Equal(10, Game.Debug.MailDebugTool.SendNewPlayerMails(e.PlayerId));
        Assert.Equal(10, await e.Scalar($"SELECT COUNT(*) FROM mailmessage WHERE toPlayerId={e.PlayerId}"));
        Assert.Equal(4, await e.Scalar("SELECT COUNT(*) FROM mailattachment"));
        Assert.Equal(10, GM_MailMsgMgr.Instance.Execute(e.PlayerId, MailAction.Summary).UnreadCount);

        long singleMailId = await e.Scalar($"SELECT id FROM mailmessage WHERE toPlayerId={e.PlayerId} " +
            "AND sourceKey='debug:new-player-reward-mail:v1:8'");
        var detail = GM_MailMsgMgr.Instance.Execute(e.PlayerId, MailAction.Read, singleMailId);
        var attachment = Assert.Single(detail.Items.Single(item => item.Mail.id == singleMailId).Attachments);
        Assert.Equal(10001, attachment.itemId);
        Assert.Equal(1, attachment.quantity);
        Assert.Equal(MailStatus.Success, GM_MailMsgMgr.Instance.Execute(e.PlayerId, MailAction.Claim, singleMailId).Status);
        Assert.Equal(MailStatus.Success, GM_MailMsgMgr.Instance.Execute(e.PlayerId, MailAction.Claim, singleMailId).Status);
        Assert.Equal(1, await e.Scalar($"SELECT quantity FROM player_item WHERE player_id={e.PlayerId} AND item_id=10001"));

        Assert.Equal(MailStatus.Success, GM_MailMsgMgr.Instance.Execute(e.PlayerId, MailAction.ClaimAll).Status);
        Assert.Equal(MailStatus.NoRewards, GM_MailMsgMgr.Instance.Execute(e.PlayerId, MailAction.ClaimAll).Status);
        Assert.Equal(16, await e.Scalar($"SELECT quantity FROM player_item WHERE player_id={e.PlayerId} AND item_id=10001"));
        Assert.Equal(20, await e.Scalar($"SELECT quantity FROM player_item WHERE player_id={e.PlayerId} AND item_id=10002"));
        Assert.Equal(3, await e.Scalar($"SELECT COUNT(*) FROM mailmessage WHERE toPlayerId={e.PlayerId} AND claimTime>0"));
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task ExpiryWorkerCleansExpiredMailWithoutManualScan() => await Run(async e=>
    {
        var expired = e.Send("worker-expired", true);
        var valid = e.Send("worker-valid");
        await e.Sql($"UPDATE mailmessage SET expireTime=UNIX_TIMESTAMP()-1 WHERE id={expired.MailId}");
        MailCleanupService.Instance.Start();
        try
        {
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (await e.Scalar($"SELECT deleteTime FROM mailmessage WHERE id={expired.MailId}") == 0 &&
                deadline.Elapsed < TimeSpan.FromSeconds(10))
                await Task.Delay(100);
        }
        finally { MailCleanupService.Instance.Stop(); }
        Assert.Equal(2, await e.Scalar($"SELECT deleteReason FROM mailmessage WHERE id={expired.MailId}"));
        Assert.Equal(0, await e.Scalar($"SELECT deleteTime FROM mailmessage WHERE id={valid.MailId}"));
        Assert.Equal(0, await e.Scalar($"SELECT claimTime FROM mailmessage WHERE id={expired.MailId}"));
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task SummaryCountsOnlyValidMailWithoutLoadingAttachments() => await Run(async e=>
    {
        var empty=GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Summary);
        Assert.Equal(MailStatus.Success,empty.Status);
        Assert.Equal(0,empty.Count);
        Assert.Equal(0,empty.UnreadCount);

        var unread=e.Send("legacy-unread",true);
        var read=e.Send("read-unclaimed",true);
        var expired=e.Send("expired");
        var deleted=e.Send("deleted");
        var foreign=e.Send("other-player");
        GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Read,read.MailId);
        await e.Sql($"UPDATE mailmessage SET expireTime=0 WHERE id={unread.MailId};"+
            $"UPDATE mailmessage SET expireTime=UNIX_TIMESTAMP() WHERE id={expired.MailId};"+
            $"UPDATE mailmessage SET deleteTime=UNIX_TIMESTAMP(),deleteReason=1 WHERE id={deleted.MailId};"+
            $"UPDATE mailmessage SET toPlayerId={e.PlayerId+1} WHERE id={foreign.MailId}");
        // This fixture owns an isolated temporary database. Summary must not access the attachment table.
        await e.Sql("DROP TABLE mailattachment");

        var summary=GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Summary);
        Assert.Equal(MailStatus.Success,summary.Status);
        Assert.Equal(2,summary.Count);
        Assert.Equal(1,summary.UnreadCount);
        Assert.Equal(20,summary.Capacity);
        Assert.True(summary.ServerTime>0);
        Assert.Empty(summary.Items);
        Assert.Equal(0,await e.Scalar($"SELECT readTime FROM mailmessage WHERE id={unread.MailId}"));
        Assert.Equal(0,await e.Scalar($"SELECT deleteTime FROM mailmessage WHERE id={expired.MailId}"));
        Assert.Equal(0,await e.Scalar($"SELECT claimTime FROM mailmessage WHERE id={read.MailId}"));

        var response=MailModule.Instance.HandlerGetMailSummary(e.Session,new ReqMailSummary(),e.PlayerId);
        Assert.Equal((int)Game.Respones.OK,response.status);
        Assert.Equal(2,response.count);
        Assert.Equal(1,response.unreadCount);
        Assert.Empty(response.items);
        Assert.Null(response.detail);
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task ModuleValidationRejectsInvalidRequestsAndStaleSessions() => await Run(async e=>
    {
        var module=MailModule.Instance;
        Assert.All(new[]
        {
            module.HandlerGetMailSummary(e.Session,null,e.PlayerId).status,
            module.HandlerGetMailList(e.Session,new ReqMailList{offset=-1},e.PlayerId).status,
            module.HandlerGetMailList(e.Session,new ReqMailList{pageSize=51},e.PlayerId).status,
            module.HandlerGetMailList(e.Session,new ReqMailList{offset=int.MaxValue},e.PlayerId).status,
            module.HandlerReadMail(e.Session,new ReqMailRead{mailId=0},e.PlayerId).status,
            module.HandlerClaimMail(e.Session,null,e.PlayerId).status,
            module.HandlerClaimAllMail(e.Session,null,e.PlayerId).status,
            module.HandlerDeleteMail(e.Session,new ReqMailDelete{mailId=-1},e.PlayerId).status,
            module.HandlerDeleteReadMail(e.Session,null,e.PlayerId).status,
            module.HandlerReqPullingMailMsg(e.Session,null,e.PlayerId).status,
            module.HandlerReqUpdateMailMsg(e.Session,new ReqUpdateMailMsg{mailMsgId=1,status=0},e.PlayerId).status
        },status=>Assert.Equal((int)Game.Respones.InvalidParams,status));
        var stale=new IdSession{playerId=e.PlayerId};
        Assert.Equal((int)Game.Respones.AccountIsNotLogin,module.HandlerGetMailList(stale,new ReqMailList(),e.PlayerId).status);
        Assert.Equal((int)Game.Respones.AccountIsNotLogin,module.HandlerReqUpdateMailMsg(stale,null,e.PlayerId).status);
        Assert.Equal(0,await e.Scalar("SELECT COUNT(*) FROM mailmessage"));
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task MigrationIsRepeatableAndKeepsOldMailContentAndStatus() => await Run(async e=>
    {
        Assert.Equal(3,await e.Scalar("SELECT COUNT(*) FROM mailmessage WHERE title='历史邮件' AND expireTime=0"));
        Assert.Equal(0,await e.Scalar("SELECT readTime FROM mailmessage WHERE id=1"));
        Assert.Equal(123,await e.Scalar("SELECT readTime FROM mailmessage WHERE id=2"));
        Assert.True(await e.Scalar("SELECT deleteTime FROM mailmessage WHERE id=3")>0);
        Assert.Equal(2,GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Summary).Count);
        Assert.Equal(0,await e.Scalar("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='playerreddot'"));
    },legacy:true);

    [DatabaseFact, Trait("Category","Database")]
    public async Task SendDedupeOrderingOwnershipAndConcurrentClaim() => await Run(async e=>
    {
        var first=e.Send("reward",true,"event-1",new[]{new MailReward{ItemId=1,Quantity=3},new MailReward{ItemId=1,Quantity=2},new MailReward{ItemId=2,Quantity=7}});
        var duplicate=e.Send("reward",true,"event-1",new[]{new MailReward{ItemId=1,Quantity=3},new MailReward{ItemId=1,Quantity=2},new MailReward{ItemId=2,Quantity=7}});
        Assert.Equal(first.MailId,duplicate.MailId);Assert.Equal(1,await e.Scalar("SELECT COUNT(*) FROM mailmessage"));
        Assert.Equal(MailStatus.SourceConflict,e.Send("different",true,"event-1").Status);
        Assert.Equal(MailStatus.NotFound,GM_MailMsgMgr.Instance.Execute(e.PlayerId+1,MailAction.Read,first.MailId).Status);
        Assert.Equal(MailStatus.NotAllowed,GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Delete,first.MailId).Status);
        var detail=MailModule.Instance.HandlerReadMail(e.Session,new ReqMailRead{mailId=first.MailId},e.PlayerId);
        Assert.Equal(1,detail.status);Assert.Equal(new[]{1,1,2},detail.detail.attachments.Select(a=>a.itemId));
        Assert.Equal(0,detail.unreadCount);
        var claims=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Task.Run(()=>GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Claim,first.MailId))));
        Assert.All(claims,r=>Assert.Equal(MailStatus.Success,r.Status));
        Assert.Equal(5,await e.Scalar("SELECT quantity FROM player_item WHERE item_id=1"));
        Assert.Equal(7,await e.Scalar("SELECT quantity FROM player_item WHERE item_id=2"));
        Assert.Equal(5,e.Entity.uItem.quantities[1]);
        Assert.Equal(MailStatus.Success,GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Delete,first.MailId).Status);
        Assert.Equal(5,await e.Scalar("SELECT quantity FROM player_item WHERE item_id=1"));
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task ClaimAllValidationFailureMarksAllReadButGrantsNothing() => await Run(async e=>
    {
        e.Send("ordinary");e.Send("valid",true);e.Send("invalid",true,null,new[]{new MailReward{ItemId=2,Quantity=9}});
        e.ItemConfigs.Remove("2");
        var failed=GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.ClaimAll);
        Assert.Equal(MailStatus.InvalidReward,failed.Status);Assert.Equal(0,failed.UnreadCount);
        Assert.Equal(0,await e.Scalar("SELECT COUNT(*) FROM player_item WHERE quantity>0"));
        Assert.Equal(0,await e.Scalar("SELECT COUNT(*) FROM mailmessage WHERE claimTime>0"));
        var deletion=GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.DeleteRead);
        Assert.Equal(2,deletion.Count);
        e.ItemConfigs.Add("2",new Item { id=2 });
        var claimed=GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.ClaimAll);
        Assert.Equal(MailStatus.Success,claimed.Status);Assert.Equal(2,await e.Scalar("SELECT COUNT(*) FROM mailmessage WHERE claimTime>0"));
        Assert.Equal(MailStatus.NoRewards,GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.ClaimAll).Status);
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task OverflowAndDatabaseFailureNeverPartiallyGrantOrUpdateComponents() => await Run(async e=>
    {
        e.Send("one",true);e.Send("two",true,null,new[]{new MailReward{ItemId=2,Quantity=9}});
        await e.Sql($"INSERT INTO player_item(player_id,item_id,quantity) VALUES({e.PlayerId},1,{long.MaxValue})");
        var overflow=GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.ClaimAll);
        Assert.Equal(MailStatus.InvalidReward,overflow.Status);Assert.Equal(0,overflow.UnreadCount);
        Assert.Equal(0,await e.Scalar("SELECT COUNT(*) FROM mailmessage WHERE claimTime>0"));
        await e.Sql("UPDATE player_item SET quantity=0");
        e.Send("db-failure-unread");
        await e.Sql("CREATE TRIGGER reject_mail_claim BEFORE UPDATE ON mailmessage FOR EACH ROW BEGIN " +
            "IF NEW.claimTime>OLD.claimTime THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='rollback claim'; END IF; END");
        var failure=GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.ClaimAll);
        Assert.Equal(MailStatus.Failed,failure.Status);
        Assert.Equal(1,await e.Scalar("SELECT COUNT(*) FROM mailmessage WHERE readTime=0"));
        Assert.Equal(0,await e.Scalar("SELECT SUM(quantity) FROM player_item"));
        Assert.Empty(e.Entity.uItem.quantities);
        await e.Sql("DROP TRIGGER reject_mail_claim");
        Assert.Equal(MailStatus.Success,GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.ClaimAll).Status);
    });

    [DatabaseFact, Trait("Category","Database")]
    public async Task ExpiryAndCapacityCleanUnreadUnclaimedMailAndReadsStayIdempotent() => await Run(async e=>
    {
        GM_MailMsgMgr.Instance.Configure(2);
        var a=e.Send("old",true);var b=e.Send("new");var c=e.Send("newest");
        Assert.Equal(3,await e.Scalar($"SELECT deleteReason FROM mailmessage WHERE id={a.MailId}"));
        var list=GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.List);
        Assert.Equal(new[]{c.MailId,b.MailId},list.Items.Select(d=>d.Mail.id));
        GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Read,b.MailId);
        long read=await e.Scalar($"SELECT readTime FROM mailmessage WHERE id={b.MailId}");
        GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Read,b.MailId);
        Assert.Equal(read,await e.Scalar($"SELECT readTime FROM mailmessage WHERE id={b.MailId}"));
        await e.Sql($"UPDATE mailmessage SET expireTime=UNIX_TIMESTAMP()-1 WHERE id={c.MailId}");
        Assert.Equal(0,GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Summary).UnreadCount);
        GM_MailMsgMgr.Instance.ExpireDue();
        Assert.Equal(2,await e.Scalar($"SELECT deleteReason FROM mailmessage WHERE id={c.MailId}"));
        Assert.Equal(MailStatus.NotFound,GM_MailMsgMgr.Instance.Execute(e.PlayerId,MailAction.Claim,a.MailId).Status);
        var legacy=MailModule.Instance.HandlerReqPullingMailMsg(e.Session,new ReqPullingMailMsg(),e.PlayerId);
        Assert.Equal(1,legacy.status);Assert.Equal(b.MailId,Assert.Single(legacy.mailMessages).msgId);
        Assert.Equal(-103,MailModule.Instance.HandlerReqUpdateMailMsg(e.Session,new ReqUpdateMailMsg{mailMsgId=b.MailId,status=0},e.PlayerId).status);
    });

    private static async Task Run(Func<Environment,Task> test,bool legacy=false)
    {
        string name="codex_mail_"+Guid.NewGuid().ToString("N");
        var builder=new MySqlConnectionStringBuilder(GachaMySqlTestEnvironment.ReadConnectionString()){Database=""};
        await using var admin=new MySqlConnection(builder.ConnectionString);await admin.OpenAsync();
        await Command(admin,$"CREATE DATABASE `{name}` CHARACTER SET utf8mb4");
        builder.Database=name;
        var field=typeof(DBService).GetField("connGameDataStr",BindingFlags.Instance|BindingFlags.NonPublic);
        var old=field.GetValue(DBService.Instance);var channel=new EmbeddedChannel();
        var configs=(Dictionary<string,Dictionary<string,object>>)typeof(ExcelUtils)
            .GetField("dataDic",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        bool hadItems=configs.TryGetValue(nameof(Item),out var previousItems);
        try
        {
            await using var db=new MySqlConnection(builder.ConnectionString);await db.OpenAsync();
            var root=new DirectoryInfo(AppContext.BaseDirectory);
            while(root!=null && !File.Exists(Path.Combine(root.FullName,"sql/ugame_10001.sql")))root=root.Parent;
            string master=await File.ReadAllTextAsync(Path.Combine(root.FullName,"sql/ugame_10001.sql"));
            await Command(db,"CREATE TABLE player(id BIGINT PRIMARY KEY);"+
                "CREATE TABLE mailmessage(id BIGINT AUTO_INCREMENT PRIMARY KEY,fromPlayerId BIGINT NOT NULL DEFAULT -1,"+
                "toPlayerId BIGINT NOT NULL,msgBody VARCHAR(256) NOT NULL,status INT NOT NULL DEFAULT 0,"+
                "sendTime INT NOT NULL DEFAULT 0,userData BIGINT NOT NULL DEFAULT 0,readTime INT NOT NULL DEFAULT 0);");
            if(legacy)await Command(db,"INSERT INTO mailmessage(fromPlayerId,toPlayerId,msgBody,status,sendTime,readTime) VALUES"+
                "(-1,910001,'old-unread',0,100,-1),(-1,910001,'old-read',1,100,123),(-1,910001,'old-deleted',2,100,-1)");
            string migration=await File.ReadAllTextAsync(Path.Combine(root.FullName,"sql/migrations/20261026_001_mail.sql"));
            await Command(db,migration);await Command(db,migration);
            await Command(db,Regex.Match(master,@"CREATE TABLE IF NOT EXISTS `player_item`[\s\S]*?\) ENGINE=[^;]+;").Value);
            await Command(db,"INSERT INTO player(id) VALUES(910001),(910002)");
            GachaMySqlTestEnvironment.InitializeServerServices(builder.ConnectionString);
            var entity=GachaMySqlTestEnvironment.AddOnlinePlayer(910001,new Dictionary<int,long>());
            var session=new IdSession{playerId=910001,accountId=910001,client=channel};entity.uPlayer.s=session;
            var env=new Environment(db,entity,session);
            configs[nameof(Item)]=env.ItemConfigs;
            GM_MailMsgMgr.Instance.Configure(20);
            await test(env);
        }
        finally
        {
            MailCleanupService.Instance.Stop();GM_EntityMgr.Instance.RemovePlayer(910001);
            field.SetValue(DBService.Instance,old);channel.FinishAndReleaseAll();
            if(hadItems)configs[nameof(Item)]=previousItems;else configs.Remove(nameof(Item));
            await Command(admin,$"DROP DATABASE `{name}`");
        }
    }
    private sealed class Environment
    {
        public readonly MySqlConnection Db;
        public readonly GM_PlayerEntity Entity;
        public readonly IdSession Session;
        public long PlayerId=>910001;
        public readonly Dictionary<string,object> ItemConfigs=new()
        { ["1"]=new Item{id=1},["2"]=new Item{id=2},["3"]=new Item{id=3} };
        public Environment(MySqlConnection db,GM_PlayerEntity entity,IdSession session){Db=db;Entity=entity;Session=session;}
        public MailResult Send(string title,bool reward=false,string source=null,MailReward[] rewards=null)=>GM_MailMsgMgr.Instance.Send(
            new MailSendCommand{PlayerId=PlayerId,Title=title,Body="body-"+title,LifetimeSeconds=3600,
                MailType=reward?1:0,SourceType=1,SourceKey=source,
                Rewards=rewards ?? (reward?new[]{new MailReward{ItemId=1,Quantity=5}}:Array.Empty<MailReward>())});
        public Task Sql(string sql)=>Command(Db,sql);
        public async Task<long> Scalar(string sql){using var c=new MySqlCommand(sql,Db);return Convert.ToInt64(await c.ExecuteScalarAsync());}
    }
    private static async Task Command(MySqlConnection db,string sql){using var c=new MySqlCommand(sql,db);await c.ExecuteNonQueryAsync();}
}
