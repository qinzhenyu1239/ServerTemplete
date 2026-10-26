using Framework.Core.Concurrency;
using Framework.Core.Serializer;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.GM_RedDot
{
    public sealed class GM_RedDotMgr
    {
        public static GM_RedDotMgr Instance { get; } = new();
        private readonly Dictionary<int, Func<SqlSugarClient, long, bool>> providers = new();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private Func<RedDotSnapshot, Message> createNotification;
        public RedDotCatalog Catalog { get; private set; }
        private GM_RedDotMgr() { }

        public void Configure(RedDotCatalog catalog, Func<RedDotSnapshot, Message> notificationFactory)
        {
            Catalog = catalog;
            createNotification = notificationFactory;
        }

        // Register only at server startup. The owning business manager evaluates its own data.
        public void RegisterBusiness(int nodeId, Func<SqlSugarClient, long, bool> evaluate) =>
            providers.Add(nodeId, evaluate ?? throw new ArgumentNullException(nameof(evaluate)));

        public RedDotSnapshot Initialize(long playerId)
        {
            using var lease = PlayerOperationLock.Acquire(playerId);
            lock (lease.SyncRoot)
            {
                return InitializeLocked(playerId);
            }
        }

        private RedDotSnapshot InitializeLocked(long playerId)
        {
            var component = GetState(playerId);
            component.IsInitialized = true;
            return Snapshot(component);
        }

        // Initialization reads existing state only; it never triggers a business condition.
        private RedDotComponent GetState(long playerId)
        {
            var entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                var component = entity.uRedDot;
                if (component == null || !ReferenceEquals(component.Session, entity.uPlayer.s))
                    entity.uRedDot = component = new RedDotComponent { Session = entity.uPlayer.s };
                if (!component.IsLoaded || component.ConfigVersion != Catalog.Version)
                {
                    using var db = DBService.Instance.GetGameInstance();
                    component = ApplyCommitted(playerId, ReadPersisted(db, playerId), false);
                }
                return component;
            }
            using var offlineDb = DBService.Instance.GetGameInstance();
            return new RedDotComponent { IsLoaded = true, ConfigVersion = Catalog.Version,
                States = ReadPersisted(offlineDb, playerId) };
        }

        public RedDotSnapshot MarkViewed(long playerId, int nodeId, long version)
        {
            using var lease = PlayerOperationLock.Acquire(playerId);
            lock (lease.SyncRoot)
            {
                if (!Catalog.CanView(nodeId, version)) throw new ArgumentException("Invalid viewed red-dot node/version.");
                using var db = DBService.Instance.GetGameInstance();
                try
                {
                    db.Ado.BeginTran();
                    LockPlayer(db, playerId);
                    var node = Catalog.ById[nodeId];
                    db.Ado.ExecuteCommand(
                        "INSERT INTO playerreddot(playerId,nodeId,isActive,seenVersion,updateTime) " +
                        "VALUES(@pid,@nid,@active,@seen,@now) ON DUPLICATE KEY UPDATE " +
                        "seenVersion=GREATEST(seenVersion,@seen),isActive=(seenVersion<@current),updateTime=@now",
                        new { pid = playerId, nid = nodeId, active = version < node.tipVersion,
                            seen = version, current = node.tipVersion, now = Now() });
                    var states = ReadPersisted(db, playerId);
                    db.Ado.CommitTran();
                    return Snapshot(ApplyCommitted(playerId, states, true));
                }
                catch { db.Ado.RollbackTran(); throw; }
            }
        }

        public static void LockPlayer(SqlSugarClient db, long playerId)
        {
            if (db.Ado.SqlQuery<long>("SELECT id FROM player WHERE id=@pid FOR UPDATE", new { pid = playerId }).Count != 1)
                throw new InvalidOperationException("Player does not exist.");
        }

        // Caller owns the player lock and transaction. PublishCommitted must run after commit.
        public void WriteBusinessState(SqlSugarClient db, long playerId, int nodeId, bool active)
        {
            if (Catalog == null) throw new InvalidOperationException("Red-dot configuration is not initialized.");
            if (!Catalog.ById.TryGetValue(nodeId, out var node) || node.clearType != 1)
                throw new ArgumentException("Not a business leaf node.");
            if (!Catalog.IsEnabled(nodeId)) return;
            db.Ado.ExecuteCommand(
                "INSERT INTO playerreddot(playerId,nodeId,isActive,seenVersion,updateTime) " +
                "VALUES(@pid,@nid,@active,0,@now) ON DUPLICATE KEY UPDATE isActive=@active,updateTime=@now",
                new { pid = playerId, nid = nodeId, active, now = Now() });
        }

        // Explicit business event entry points. Caller owns the lock/transaction and publishes after commit.
        public void RefreshBusinessState(SqlSugarClient db, long playerId, int nodeId)
        {
            if (!providers.TryGetValue(nodeId, out var evaluate))
                throw new ArgumentException("No business provider for this red-dot node.");
            WriteBusinessState(db, playerId, nodeId, evaluate(db, playerId));
        }

        public void WriteViewedState(SqlSugarClient db, long playerId, int nodeId)
        {
            if (!Catalog.ById.TryGetValue(nodeId, out var node) || node.clearType != 2)
                throw new ArgumentException("Not a viewed leaf node.");
            if (!Catalog.IsEnabled(nodeId)) return;
            db.Ado.ExecuteCommand(
                "INSERT INTO playerreddot(playerId,nodeId,isActive,seenVersion,updateTime) " +
                "VALUES(@pid,@nid,1,0,@now) ON DUPLICATE KEY UPDATE " +
                "isActive=(seenVersion<@version),updateTime=@now",
                new { pid = playerId, nid = node.id, now = Now(), version = node.tipVersion });
        }

        public void PublishCommitted(long playerId)
        {
            // A push failure must never turn a committed business operation into a failure/retry.
            try
            {
                using var lease = PlayerOperationLock.Acquire(playerId);
                lock (lease.SyncRoot)
                {
                    if (GM_EntityMgr.Instance.Get(playerId) == null) return;
                    using var db = DBService.Instance.GetGameInstance();
                    ApplyCommitted(playerId, ReadPersisted(db, playerId), true);
                }
            }
            catch (Exception ex) { logger.Error(ex, "Red-dot post-commit synchronization failed."); }
        }

        private Dictionary<int, Playerreddot> ReadPersisted(SqlSugarClient db, long playerId) =>
            db.Queryable<Playerreddot>().Where(r => r.playerId == playerId).ToList()
                .Where(r => Catalog.ById.TryGetValue(r.nodeId, out var n) && n.clearType != 0 && Catalog.IsEnabled(r.nodeId))
                .ToDictionary(r => r.nodeId);

        private RedDotComponent ApplyCommitted(long playerId, Dictionary<int, Playerreddot> states, bool notify)
        {
            var entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null) throw new InvalidOperationException("Player is offline.");
            var c = entity.uRedDot;
            if (c == null || !ReferenceEquals(c.Session, entity.uPlayer.s))
                entity.uRedDot = c = new RedDotComponent { Session = entity.uPlayer.s };
            var changed = states.Values.Where(r => !c.States.TryGetValue(r.nodeId, out var old)
                ? r.isActive : old.isActive != r.isActive)
                .Select(r => new RedDotValue { NodeId = r.nodeId, IsActive = r.isActive }).ToArray();
            c.States = states;
            c.IsLoaded = true;
            c.ConfigVersion = Catalog.Version;
            if (changed.Length > 0) c.StateVersion++;
            if (notify && c.IsInitialized && changed.Length > 0 && c.Session != null)
            {
                try
                {
                    MessagePusher.PushMessage(c.Session, createNotification(new RedDotSnapshot
                    { ConfigVersion = Catalog.Version, StateVersion = c.StateVersion, States = changed }), playerId);
                }
                catch (Exception ex) { logger.Error(ex, "Red-dot push failed; client will resynchronize."); }
            }
            return c;
        }

        private RedDotSnapshot Snapshot(RedDotComponent c) => new()
        {
            ConfigVersion = Catalog.Version, StateVersion = c.StateVersion,
            States = Catalog.Nodes.Where(n => n.clearType != 0 && Catalog.IsEnabled(n.id))
                .Select(n => new RedDotValue { NodeId = n.id,
                    IsActive = c.States.TryGetValue(n.id, out var r) && r.isActive }).ToArray()
        };
        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
