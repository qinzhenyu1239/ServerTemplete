using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Framework.Core.Concurrency;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Core.GM_Character;
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
            using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
            lock (playerLock.SyncRoot) return GetBasicInfoLocked(playerId);
        }

        private PlayerBasicInfoResult GetBasicInfoLocked(long playerId)
        {
            try
            {
                Player player = ReadPlayer(playerId);
                return player == null ? Result(PlayerProfileStatus.NotFound) : Success(player);
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Failed to load basic info for player[{playerId}].");
                return Result(PlayerProfileStatus.Failed);
            }
        }

        public PlayerBasicInfoResult UpdateBasicInfo(long playerId, long accountId, PlayerProfileUpdate update)
        {
            if (playerId <= 0 || accountId <= 0 || update?.IsValid() != true)
                return Result(PlayerProfileStatus.InvalidInput);
            using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
            lock (playerLock.SyncRoot)
            {
                SqlSugarClient db = null;
                bool transaction = false;
                try
                {
                    db = DBService.Instance.GetGameInstance();
                    db.Ado.BeginTran();
                    transaction = true;
                    Player player = db.Ado.SqlQuery<Player>(
                        "SELECT * FROM player WHERE id=@id AND accountId=@accountId FOR UPDATE",
                        new SugarParameter("@id", playerId), new SugarParameter("@accountId", accountId)).SingleOrDefault();
                    if (player == null)
                    {
                        db.Ado.RollbackTran(); transaction = false;
                        return Result(PlayerProfileStatus.NotFound);
                    }
                    if (update.Includes(ProfileField.Characters) &&
                        !GM_CharacterMgr.Instance.OwnsCharacterInstances(db, playerId, update.DisplayCharacterIds))
                    {
                        db.Ado.RollbackTran(); transaction = false;
                        return Result(PlayerProfileStatus.InvalidInput);
                    }
                    var columns = new List<string>();
                    var parameters = new List<SugarParameter> { new SugarParameter("@id", playerId) };
                    void Set(string column, object value)
                    {
                        columns.Add("`" + column + "`=@" + column);
                        parameters.Add(new SugarParameter("@" + column, value));
                    }
                    foreach (ProfileField field in update.Fields)
                    {
                        switch (field)
                        {
                            case ProfileField.Avatar: player.avatarId = update.AvatarId.Value; Set("avatarId", player.avatarId); break;
                            case ProfileField.AvatarFrame: player.avatarFrameId = update.AvatarFrameId.Value; Set("avatarFrameId", player.avatarFrameId); break;
                            case ProfileField.Title: player.titleId = update.TitleId.Value; Set("titleId", player.titleId); break;
                            case ProfileField.Background: player.backgroundId = update.BackgroundId.Value; Set("backgroundId", player.backgroundId); break;
                            case ProfileField.Achievements: player.displayAchievementIds = JsonSerializer.Serialize(update.DisplayAchievementIds); Set("displayAchievementIds", player.displayAchievementIds); break;
                            case ProfileField.Characters: player.displayCharacterIds = JsonSerializer.Serialize(update.DisplayCharacterIds); Set("displayCharacterIds", player.displayCharacterIds); break;
                            case ProfileField.Signature: player.signature = update.Signature; Set("signature", player.signature); break;
                        }
                    }
                    // Validate the complete response before committing; malformed stored slots are never silently replaced.
                    PlayerBasicInfoResult result = Success(player);
                    int affected = db.Ado.ExecuteCommand("UPDATE player SET " + string.Join(",", columns) + " WHERE id=@id", parameters.ToArray());
                    // MySQL may report either matched rows or changed rows. The row was locked and checked above.
                    if (affected < 0 || affected > 1) throw new InvalidOperationException("Unexpected player update count.");
                    db.Ado.CommitTran(); transaction = false;
                    GM_AccountEntityMgr.Instance.ApplyCommittedPlayer(player);
                    return result;
                }
                catch (Exception exception)
                {
                    if (transaction)
                    {
                        try { db.Ado.RollbackTran(); }
                        catch (Exception rollback) { logger.Error(rollback, "Profile rollback failed."); }
                    }
                    logger.Error(exception, $"Failed to update basic info for player[{playerId}].");
                    return Result(PlayerProfileStatus.Failed);
                }
                finally { db?.Dispose(); }
            }
        }

        private static PlayerBasicInfoResult Result(PlayerProfileStatus status) => new PlayerBasicInfoResult { Status = status };
        private static PlayerBasicInfoResult Success(Player player)
        {
            int[] achievements = JsonSerializer.Deserialize<int[]>(player.displayAchievementIds);
            long[] characters = JsonSerializer.Deserialize<long[]>(player.displayCharacterIds);
            if (!PlayerProfileUpdate.ValidSlots(achievements?.Select(x => (long)x).ToArray(), 7) ||
                !PlayerProfileUpdate.ValidSlots(characters, 6)) throw new InvalidOperationException("Invalid stored profile slots.");
            return new PlayerBasicInfoResult
            {
                Status = PlayerProfileStatus.Success,
                Info = new PlayerBasicInfoSnapshot
                {
                    PlayerId = player.id, AvatarId = player.avatarId, Name = player.name ?? string.Empty,
                    GuildId = player.guildId, CreateTime = player.createTime, TitleId = player.titleId,
                    Signature = player.signature ?? string.Empty, AvatarFrameId = player.avatarFrameId,
                    BackgroundId = player.backgroundId, DisplayAchievementIds = achievements, DisplayCharacterIds = characters
                }
            };
        }

        private static Player ReadPlayer(long playerId)
        {
            if (playerId <= 0) return null;
            var entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity?.uPlayer.playerInfo?.id == playerId) return entity.uPlayer.playerInfo;
            Player player = GM_AccountEntityMgr.Instance.GetPlayerById(playerId);
            if (player != null && entity != null) entity.uPlayer.playerInfo = player;
            return player;
        }
    }
}
