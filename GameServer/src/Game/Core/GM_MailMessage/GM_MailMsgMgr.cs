using Framework.Core.Concurrency;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Core.GM_Item;
using Game.Datas.DBEntities;
using Game.Datas.Excels;
using Game.Datas.GMEntities;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Game.Core.GM_MailMessage
{
    public sealed class GM_MailMsgMgr
    {
        public static GM_MailMsgMgr Instance { get; } = new();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private Func<MailResult, Message> notificationFactory;
        private int scanning;
        public int Capacity { get; private set; }
        private const string ValidWhere = "toPlayerId=@pid AND deleteTime=0 AND (expireTime=0 OR expireTime>@now)";

        private GM_MailMsgMgr() { }

        public void Configure(int capacity, Func<MailResult, Message> notifications)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "MailboxCapacity must be explicitly configured.");
            Capacity = capacity;
            notificationFactory = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }
        public MailResult Execute(long playerId, MailAction action, long mailId = 0)
        {
            using var lease = PlayerOperationLock.Acquire(playerId);
            lock (lease.SyncRoot) return ExecuteLocked(playerId, action, mailId);
        }
        private MailResult ExecuteLocked(long playerId, MailAction action, long mailId)
        {
            try
            {
                if (playerId <= 0 || Capacity <= 0 || !Enum.IsDefined(typeof(MailAction), action))
                    return new MailResult { Status = MailStatus.Invalid };
                var result = action == MailAction.Summary ? ReadSummary(playerId) : RunTransaction(playerId, action, mailId);
                Synchronize(playerId, result, action is MailAction.Summary or MailAction.List);
                return result;
            }
            catch (Exception ex) { logger.Error(ex, "Mail operation failed."); return new MailResult { Status = MailStatus.Failed }; }
        }

        public MailResult Send(MailSendCommand command)
        {
            if (Capacity <= 0) return new MailResult { Status = MailStatus.Failed };
            if (!MailRules.ValidSend(command)) return new MailResult { Status = MailStatus.Invalid };
            if (command.Rewards.Any(r => ExcelUtils.GetConfigData<Item>(r.ItemId.ToString()) == null))
                return new MailResult { Status = MailStatus.InvalidReward };
            using var lease = PlayerOperationLock.Acquire(command.PlayerId);
            lock (lease.SyncRoot)
            {
                using var db = DBService.Instance.GetGameInstance();
                try
                {
                    db.Ado.BeginTran(); LockPlayer(db, command.PlayerId);
                    var result = new MailResult { Status = MailStatus.Success };
                    Mailmessage previous = command.SourceKey == null ? null : db.Queryable<Mailmessage>().First(m =>
                        m.toPlayerId == command.PlayerId && m.sourceType == command.SourceType && m.sourceKey == command.SourceKey);
                    if (previous != null)
                    {
                        result.MailId = previous.id;
                        if (!SameSend(db, previous, command)) result.Status = MailStatus.SourceConflict;
                    }
                    else
                    {
                        long now = Now();
                        var mail = new Mailmessage { toPlayerId = command.PlayerId, fromPlayerId = command.SenderId,
                            mailType = command.MailType, title = command.Title, msgBody = command.Body,
                            sourceType = command.SourceType, sourceKey = command.SourceKey,
                            sendTime = now, expireTime = checked(now + command.LifetimeSeconds) };
                        result.MailId = db.Insertable(mail).ExecuteReturnBigIdentity();
                        if (result.MailId <= 0) throw new InvalidOperationException("Mail insertion failed.");
                        for (int i = 0; i < command.Rewards.Count; i++)
                            RequireOne(db.Insertable(new Mailattachment { mailId = result.MailId, sortOrder = i + 1,
                                itemId = command.Rewards[i].ItemId, quantity = command.Rewards[i].Quantity }).ExecuteCommand());
                        result.AddedIds.Add(result.MailId);
                        Cleanup(db, command.PlayerId, now, result, true);
                    }
                    FillSnapshot(db, command.PlayerId, result);
                    db.Ado.CommitTran();
                    Synchronize(command.PlayerId, result, false);
                    return result;
                }
                catch (Exception ex) { db.Ado.RollbackTran(); logger.Error(ex, "Mail send failed."); return new MailResult { Status = MailStatus.Failed }; }
            }
        }

        private MailResult ReadSummary(long playerId)
        {
            using var db = DBService.Instance.GetGameInstance();
            long now = Now();
            var result = db.Ado.SqlQuery<MailResult>(
                "SELECT COUNT(*) AS Count, " +
                "COALESCE(SUM(CASE WHEN readTime=0 THEN 1 ELSE 0 END),0) AS UnreadCount " +
                "FROM mailmessage WHERE " + ValidWhere,
                new { pid = playerId, now }).Single();
            result.Status = MailStatus.Success;
            result.ServerTime = now;
            result.Capacity = Capacity;
            return result;
        }
        private MailResult RunTransaction(long playerId, MailAction action, long mailId)
        {
            using var db = DBService.Instance.GetGameInstance();
            try
            {
                db.Ado.BeginTran(); LockPlayer(db, playerId);
                var result = new MailResult { Status = MailStatus.Success, MailId = mailId };
                long now = Now();
                Cleanup(db, playerId, now, result, action == MailAction.List);
                var mails = ReadValid(db, playerId, now, true);
                var target = mails.FirstOrDefault(m => m.id == mailId);
                if (action is MailAction.Read or MailAction.Claim or MailAction.Delete && target == null)
                {
                    var removed = db.Queryable<Mailmessage>().First(m => m.id == mailId && m.toPlayerId == playerId);
                    result.Status = action == MailAction.Delete && removed?.deleteTime > 0 ? MailStatus.Success : MailStatus.NotFound;
                }
                else if (action == MailAction.Read) MarkRead(db, target, now, result);
                else if (action == MailAction.Delete)
                {
                    if (!MailRules.CanDelete(target)) result.Status = MailStatus.NotAllowed;
                    else DeleteMail(db, target, now, 1, result);
                }
                else if (action == MailAction.DeleteRead)
                    foreach (var mail in mails.Where(MailRules.CanDelete)) DeleteMail(db, mail, now, 1, result);
                else if (action is MailAction.Claim or MailAction.ClaimAll)
                {
                    if (action == MailAction.ClaimAll)
                        foreach (var mail in mails) MarkRead(db, mail, now, result);
                    var selected = action == MailAction.ClaimAll ? mails.Where(m => m.mailType == 1 && m.claimTime == 0).ToArray()
                        : target.mailType == 1 && target.claimTime == 0 ? new[] { target } : Array.Empty<Mailmessage>();
                    if (selected.Length == 0)
                        result.Status = action == MailAction.Claim && target.mailType == 1 && target.claimTime > 0
                            ? MailStatus.Success : MailStatus.NoRewards;
                    else ClaimRewards(db, playerId, selected, now, result);
                }
                FillSnapshot(db, playerId, result);
                db.Ado.CommitTran();
                return result;
            }
            catch { db.Ado.RollbackTran(); throw; }
        }

        private void ClaimRewards(SqlSugarClient db, long playerId, Mailmessage[] mails, long now, MailResult result)
        {
            var additions = new Dictionary<int, long>();
            try
            {
                foreach (var mail in mails)
                {
                    var attachments = db.Queryable<Mailattachment>().Where(a => a.mailId == mail.id).OrderBy(a => a.sortOrder).ToList();
                    if (attachments.Count == 0) { result.Status = MailStatus.InvalidReward; return; }
                    foreach (var a in attachments)
                    {
                        if (a.quantity <= 0 || ExcelUtils.GetConfigData<Item>(a.itemId.ToString()) == null)
                        { result.Status = MailStatus.InvalidReward; return; }
                        additions.TryGetValue(a.itemId, out long existing);
                        additions[a.itemId] = checked(existing + a.quantity);
                    }
                }
                var stored = GM_ItemMgr.Instance.LockRewardQuantities(db, playerId, additions.Keys);
                foreach (var pair in additions)
                {
                    if (stored[pair.Key] < 0) { result.Status = MailStatus.InvalidReward; return; }
                    stored[pair.Key] = checked(stored[pair.Key] + pair.Value);
                }
                // No gameplay upper limit in this release; all validation occurs before any grant.
                GM_ItemMgr.Instance.GrantLocked(db, playerId, additions);
                foreach (var mail in mails)
                {
                    RequireOne(db.Ado.ExecuteCommand("UPDATE mailmessage SET claimTime=@now,readTime=IF(readTime=0,@now,readTime) " +
                        "WHERE id=@id AND toPlayerId=@pid AND claimTime=0 AND deleteTime=0 AND (expireTime=0 OR expireTime>@now)",
                        new { now, id = mail.id, pid = playerId }));
                    result.ChangedIds.Add(mail.id);
                }
                result.ItemQuantities = stored;
            }
            catch (OverflowException) { result.Status = MailStatus.InvalidReward; }
        }

        private static void LockPlayer(SqlSugarClient db, long playerId)
        {
            if (db.Ado.SqlQuery<long>("SELECT id FROM player WHERE id=@pid FOR UPDATE", new { pid = playerId }).Count != 1)
                throw new InvalidOperationException("Player does not exist.");
        }
        private static Mailmessage[] ReadValid(SqlSugarClient db, long playerId, long now, bool lockRows = false) => MailRules.Sort(
            db.Ado.SqlQuery<Mailmessage>("SELECT * FROM mailmessage WHERE " + ValidWhere + (lockRows ? " FOR UPDATE" : ""),
                new { pid = playerId, now }));
        private void FillSnapshot(SqlSugarClient db, long playerId, MailResult result)
        {
            long now = Now();
            var mails = ReadValid(db, playerId, now);
            long[] ids = mails.Select(m => m.id).ToArray();
            var rewards = ids.Length == 0 ? new List<Mailattachment>() : db.Queryable<Mailattachment>()
                .Where(a => ids.Contains(a.mailId)).OrderBy(a => a.sortOrder).ToList();
            result.Items = mails.Select(m => new MailData { Mail = m,
                Attachments = rewards.Where(a => a.mailId == m.id).ToArray() }).ToArray();
            result.Count = mails.Length; result.UnreadCount = mails.Count(m => m.readTime == 0);
            result.ServerTime = now; result.Capacity = Capacity;
        }
        private static void MarkRead(SqlSugarClient db, Mailmessage mail, long now, MailResult result)
        {
            if (mail.readTime > 0) return;
            RequireOne(db.Ado.ExecuteCommand("UPDATE mailmessage SET readTime=@now WHERE id=@id AND toPlayerId=@pid AND readTime=0",
                new { now, id = mail.id, pid = mail.toPlayerId }));
            mail.readTime = now; result.ChangedIds.Add(mail.id);
        }
        private static void DeleteMail(SqlSugarClient db, Mailmessage mail, long now, int reason, MailResult result)
        {
            RequireOne(db.Ado.ExecuteCommand("UPDATE mailmessage SET deleteTime=@now,deleteReason=@reason " +
                "WHERE id=@id AND toPlayerId=@pid AND deleteTime=0", new { now, reason, id = mail.id, pid = mail.toPlayerId }));
            result.DeletedIds.Add(mail.id);
        }
        private void Cleanup(SqlSugarClient db, long playerId, long now, MailResult result, bool capacity)
        {
            foreach (var mail in db.Queryable<Mailmessage>().Where(m => m.toPlayerId == playerId && m.deleteTime == 0 &&
                m.expireTime > 0 && m.expireTime <= now).ToList()) DeleteMail(db, mail, now, 2, result);
            if (capacity)
                foreach (var mail in ReadValid(db, playerId, now, true).Skip(Capacity)) DeleteMail(db, mail, now, 3, result);
        }
        private static bool SameSend(SqlSugarClient db, Mailmessage m, MailSendCommand c)
        {
            var rewards = db.Queryable<Mailattachment>().Where(a => a.mailId == m.id).OrderBy(a => a.sortOrder).ToList();
            return m.fromPlayerId == c.SenderId && m.mailType == c.MailType && m.title == c.Title && m.msgBody == c.Body &&
                m.expireTime - m.sendTime == c.LifetimeSeconds && rewards.Count == c.Rewards.Count &&
                rewards.Select((a, i) => a.itemId == c.Rewards[i].ItemId && a.quantity == c.Rewards[i].Quantity).All(v => v);
        }
        private void Synchronize(long playerId, MailResult result, bool initialize)
        {
            // A notification or cache failure cannot undo a committed transaction.
            try
            {
                GM_ItemMgr.Instance.SyncCommittedQuantities(playerId, result.ItemQuantities);
                var entity = GM_EntityMgr.Instance.Get(playerId);
                if (entity == null) return;
                var c = entity.uMail;
                if (c == null || !ReferenceEquals(c.Session, entity.uPlayer.s))
                    entity.uMail = c = new MailComponent { Session = entity.uPlayer.s };
                bool changed = result.AddedIds.Count + result.ChangedIds.Count + result.DeletedIds.Count > 0 ||
                    c.Count != result.Count || c.UnreadCount != result.UnreadCount;
                if (changed) c.Version++;
                c.Count = result.Count; c.UnreadCount = result.UnreadCount; result.MailboxVersion = c.Version;
                if (changed && c.Initialized && c.Session != null)
                    MessagePusher.PushMessage(c.Session, notificationFactory(result), playerId);
                if (initialize) c.Initialized = true;
            }
            catch (Exception ex) { logger.Error(ex, "Mail committed; notification/cache synchronization failed."); }
        }
        public void ExpireDue()
        {
            if (Interlocked.Exchange(ref scanning, 1) != 0) return;
            try
            {
                using var db = DBService.Instance.GetGameInstance();
                var players = db.Ado.SqlQuery<long>("SELECT DISTINCT m.toPlayerId FROM mailmessage m INNER JOIN player p ON p.id=m.toPlayerId " +
                    "WHERE m.deleteTime=0 AND m.expireTime>0 AND m.expireTime<=@now LIMIT 100", new { now = Now() });
                foreach (long id in players) Execute(id, MailAction.Cleanup);
            }
            catch (Exception ex) { logger.Error(ex, "Mail expiry scan failed."); }
            finally { Volatile.Write(ref scanning, 0); }
        }
        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private static void RequireOne(int affected)
        { if (affected != 1) throw new InvalidOperationException("Concurrent mail update."); }
    }
}
