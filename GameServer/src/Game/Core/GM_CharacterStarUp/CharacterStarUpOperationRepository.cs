using Framework.Core.Utils;
using Framework.Core.Idempotency;
using Game.Datas.DBEntities;
using SqlSugar;
using System;
using System.Linq;

namespace Game.Core.GM_CharacterStarUp
{
    internal sealed class CharacterStarUpOperationRepository
    {
        public static readonly CharacterStarUpOperationRepository Instance = new();
        internal static readonly TimeSpan Retention = TimeSpan.FromDays(90);
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private CharacterStarUpOperationRepository() { }

        public CharacterStarUpResult TryBegin(SqlSugarClient db, long playerId, string operationId,
            int characterId, int targetStar, string requestHash)
        {
            DateTime now = DateTime.UtcNow;
            int inserted = db.Ado.ExecuteCommand(
                "INSERT IGNORE INTO player_character_starup_operation " +
                "(id,player_id,operation_id,character_id,target_star,request_hash,result_status,previous_star,current_star,did_upgrade,used_fragment_num,remaining_fragment_num,response_json,created_at,completed_at,expires_at) " +
                "VALUES (@id,@playerId,@operationId,@characterId,@targetStar,@requestHash,-1,0,0,0,0,0,'',@now,@now,@expiresAt)",
                new SugarParameter("@id", IdGenerator.GetNextId()),
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@operationId", operationId),
                new SugarParameter("@characterId", characterId),
                new SugarParameter("@targetStar", targetStar),
                new SugarParameter("@requestHash", requestHash),
                new SugarParameter("@now", now),
                new SugarParameter("@expiresAt", now.Add(Retention)));
            if (inserted == 1) return null;

            Player_character_starup_operation stored = db.Ado.SqlQuery<Player_character_starup_operation>(
                    "SELECT * FROM player_character_starup_operation WHERE player_id=@playerId AND operation_id=@operationId FOR UPDATE",
                    new SugarParameter("@playerId", playerId),
                    new SugarParameter("@operationId", operationId))
                .SingleOrDefault() ?? throw new InvalidOperationException("Failed to load duplicate star-up operation.");
            if (stored.character_id != characterId || stored.target_star != targetStar ||
                !string.Equals(stored.request_hash, requestHash, StringComparison.Ordinal))
            {
                logger.Warn($"idempotency_conflict operation=character_starup playerId={playerId} operationId={operationId}");
                throw new IdempotentOperationConflictException(operationId);
            }
            logger.Info($"idempotency_hit operation=character_starup playerId={playerId} operationId={operationId}");
            return EconomicOperationPersistence.Deserialize<CharacterStarUpResult>(stored.response_json);
        }

        public void Complete(SqlSugarClient db, long playerId, string operationId, CharacterStarUpResult result)
        {
            if (result?.Status != CharacterStarUpStatus.Success)
                throw new ArgumentException(
                    "Only successful character star-up operations may be persisted.", nameof(result));

            int affected = db.Ado.ExecuteCommand(
                "UPDATE player_character_starup_operation SET result_status=@status,previous_star=@previousStar,current_star=@currentStar," +
                "did_upgrade=@didUpgrade,used_fragment_num=@usedFragments,remaining_fragment_num=@remainingFragments," +
                "response_json=@responseJson,completed_at=@completedAt " +
                "WHERE player_id=@playerId AND operation_id=@operationId AND result_status=-1",
                new SugarParameter("@status", (int)result.Status),
                new SugarParameter("@previousStar", result.PreviousStar),
                new SugarParameter("@currentStar", result.CurrentStar),
                new SugarParameter("@didUpgrade", result.DidUpgrade),
                new SugarParameter("@usedFragments", result.ConsumedPieceCount),
                new SugarParameter("@remainingFragments", result.RemainingPieceQuantity),
                new SugarParameter("@responseJson", EconomicOperationPersistence.Serialize(result)),
                new SugarParameter("@completedAt", DateTime.UtcNow),
                new SugarParameter("@playerId", playerId), 
                new SugarParameter("@operationId", operationId));
            if (affected != 1) throw new InvalidOperationException("Failed to complete character star-up operation.");
        }

        public int DeleteExpired(SqlSugarClient db, int batchSize = 10000) =>
            db.Ado.ExecuteCommand(
                $"DELETE FROM player_character_starup_operation WHERE expires_at < UTC_TIMESTAMP(6) LIMIT {Math.Max(1, batchSize)}");
    }
}
