using System;
using Framework.Core.Concurrency;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using SqlSugar;

namespace Game.Core.GM_PlayerProfile
{
    public sealed class GM_PlayerProfileMgr
    {
        public static GM_PlayerProfileMgr Instance { get; } = new GM_PlayerProfileMgr();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private GM_PlayerProfileMgr() { }
        public PlayerBasicInfoResult GetBasicInfo(long playerId)
        {
            using var lease = PlayerOperationLock.Acquire(playerId);
            lock (lease.SyncRoot) return GetBasicInfoLocked(playerId);
        }
        private PlayerBasicInfoResult GetBasicInfoLocked(long playerId)
        {
            try { var player = ReadPlayer(playerId); return player == null ? Result(PlayerProfileStatus.NotFound) : Success(player); }
            catch (Exception error) { logger.Error(error, "Failed to load player basic info."); return Result(PlayerProfileStatus.Failed); }
        }
        public PlayerBasicInfoResult UpdateBasicInfo(long playerId, long accountId, PlayerProfileUpdate update)
        {
            if (playerId <= 0 || accountId <= 0 || update?.IsValid() != true) return Result(PlayerProfileStatus.InvalidInput);
            using var lease = PlayerOperationLock.Acquire(playerId);
            lock (lease.SyncRoot)
            {
                SqlSugarClient db = null;
                try
                {
                    db = DBService.Instance.GetGameInstance();
                    db.Ado.BeginTran();
                    var player = db.Ado.SqlQuerySingle<Player>("SELECT * FROM player WHERE id=@id AND accountId=@accountId FOR UPDATE",
                        new SugarParameter("@id", playerId), new SugarParameter("@accountId", accountId));
                    if (player == null) { db.Ado.RollbackTran(); return Result(PlayerProfileStatus.NotFound); }
                    int affected = db.Updateable<Player>().SetColumns(p => p.avatarId == update.AvatarId.Value).Where(p => p.id == playerId && p.accountId == accountId).ExecuteCommand();
                    if (affected < 0 || affected > 1) throw new InvalidOperationException("Unexpected player update count.");
                    player.avatarId = update.AvatarId.Value;
                    db.Ado.CommitTran();
                    GM_AccountEntityMgr.Instance.ApplyCommittedPlayer(player);
                    return Success(player);
                }
                catch (Exception error)
                {
                    try { db?.Ado.RollbackTran(); } catch (Exception rollback) { logger.Error(rollback, "Profile rollback failed."); }
                    logger.Error(error, "Failed to update player avatar."); return Result(PlayerProfileStatus.Failed);
                }
                finally { db?.Dispose(); }
            }
        }
        private static PlayerBasicInfoResult Result(PlayerProfileStatus status) => new PlayerBasicInfoResult { Status = status };
        private static PlayerBasicInfoResult Success(Player player) => new PlayerBasicInfoResult
        {
            Status = PlayerProfileStatus.Success,
            Info = new PlayerBasicInfoSnapshot
            {
                PlayerId = player.id, Name = player.name ?? string.Empty, AvatarId = player.avatarId, CreateTime = player.createTime,
                BaseLevel = player.baseLevel, MainStage = player.mainStage, ResonanceLevel = player.resonanceLevel, TowerStage = player.towerStage,
                Signature = string.Empty, DisplayAchievementIds = new int[7], DisplayCharacterIds = new long[6]
            }
        };
        private static Player ReadPlayer(long playerId)
        {
            if (playerId <= 0) return null;
            var entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity?.uPlayer.playerInfo?.id == playerId) return entity.uPlayer.playerInfo;
            var player = GM_AccountEntityMgr.Instance.GetPlayerById(playerId);
            if (player != null && entity != null) entity.uPlayer.playerInfo = player;
            return player;
        }
    }
}
