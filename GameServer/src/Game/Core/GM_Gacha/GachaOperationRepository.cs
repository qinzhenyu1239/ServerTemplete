using Framework.Core.Utils;
using Framework.Core.Idempotency;
using Game.Datas.DBEntities;
using SqlSugar;
using System;
using System.Linq;

namespace Game.Core.GM_Gacha
{
    internal sealed class GachaOperationRepository
    {
        public static readonly GachaOperationRepository Instance = new();
        internal static readonly TimeSpan Retention = TimeSpan.FromDays(90);
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private GachaOperationRepository() { }

        public GachaDrawResult TryBegin(SqlSugarClient db, long playerId, string operationId,
            int poolId, int drawCount, string requestHash)
        {
            DateTime now = DateTime.UtcNow;
            int inserted = db.Ado.ExecuteCommand(
                "INSERT IGNORE INTO player_gacha_operation " +
                "(id,player_id,operation_id,pool_id,draw_count,request_hash,result_status,batch_id,response_json,created_at,completed_at,expires_at) " +
                "VALUES (@id,@playerId,@operationId,@poolId,@drawCount,@requestHash,-1,0,'',@now,@now,@expiresAt)",
                new SugarParameter("@id", IdGenerator.GetNextId()),
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@operationId", operationId),
                new SugarParameter("@poolId", poolId),
                new SugarParameter("@drawCount", drawCount),
                new SugarParameter("@requestHash", requestHash),
                new SugarParameter("@now", now),
                new SugarParameter("@expiresAt", now.Add(Retention)));
            if (inserted == 1) return null;

            Player_gacha_operation stored = db.Ado.SqlQuery<Player_gacha_operation>(
                    "SELECT * FROM player_gacha_operation WHERE player_id=@playerId AND operation_id=@operationId FOR UPDATE",
                    new SugarParameter("@playerId", playerId),
                    new SugarParameter("@operationId", operationId))
                .SingleOrDefault() ?? throw new InvalidOperationException("Failed to load duplicate gacha operation.");
            if (stored.pool_id != poolId || stored.draw_count != drawCount ||
                !string.Equals(stored.request_hash, requestHash, StringComparison.Ordinal))
            {
                logger.Warn($"idempotency_conflict operation=gacha playerId={playerId} operationId={operationId}");
                throw new IdempotentOperationConflictException(operationId);
            }
            logger.Info($"idempotency_hit operation=gacha playerId={playerId} operationId={operationId}");
            return EconomicOperationPersistence.Deserialize<GachaDrawResult>(stored.response_json);
        }

        public void Complete(SqlSugarClient db, long playerId, string operationId, GachaDrawResult result)
        {
            if (result?.Status != GachaOperationStatus.Success)
                throw new ArgumentException(
                    "Only successful gacha operations may be persisted.", nameof(result));

            int affected = db.Ado.ExecuteCommand(
                "UPDATE player_gacha_operation SET result_status=@status,batch_id=@batchId,response_json=@responseJson,completed_at=@completedAt " +
                "WHERE player_id=@playerId AND operation_id=@operationId AND result_status=-1",
                new SugarParameter("@status", (int)result.Status),
                new SugarParameter("@batchId", result.BatchId),
                new SugarParameter("@responseJson", EconomicOperationPersistence.Serialize(result)),
                new SugarParameter("@completedAt", DateTime.UtcNow),
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@operationId", operationId));
            if (affected != 1) throw new InvalidOperationException("Failed to complete gacha operation.");
        }

        public int DeleteExpired(SqlSugarClient db, int batchSize = 10000) =>
            db.Ado.ExecuteCommand(
                $"DELETE FROM player_gacha_operation WHERE expires_at < UTC_TIMESTAMP(6) LIMIT {Math.Max(1, batchSize)}");
    }
}
