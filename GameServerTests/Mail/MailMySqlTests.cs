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
        GM_MailMsgMgr.Instance.Configure(2,_=>new ResMailChanged());
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
            GM_MailMsgMgr.Instance.Configure(20,r=>new ResMailChanged{mailboxVersion=r.MailboxVersion});
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
