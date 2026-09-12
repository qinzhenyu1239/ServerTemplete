using Game.Core.Db;
using Game.Core.GM_CharacterStarUp;
using Game.Core.GM_Gacha;
using SqlSugar;
using System;
using System.Linq;
using System.Threading;

namespace Game.Core.Maintenance
{
    public sealed class EconomicOperationCleanupService
    {
        public static readonly EconomicOperationCleanupService Instance = new();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private Timer timer;
        private int running;

        private EconomicOperationCleanupService() { }

        public void Start()
        {
            timer ??= new Timer(
                _ => Cleanup(), null, TimeSpan.FromMinutes(5), TimeSpan.FromHours(6));
        }

        public void Stop()
        {
            timer?.Dispose();
            timer = null;
        }

        private void Cleanup()
        {
            if (Interlocked.Exchange(ref running, 1) != 0) return;
            try
            {
                CleanupGacha();
                CleanupCharacterStarUp();
            }
            finally { Volatile.Write(ref running, 0); }
        }

        private void CleanupGacha()
        {
            try
            {
                using SqlSugarClient db = DBService.Instance.GetGameInstance();
                long backlog = db.Ado.SqlQuery<long>(
                    "SELECT COUNT(*) FROM player_gacha_operation WHERE expires_at < UTC_TIMESTAMP(6)")
                    .Single();
                int deleted = GachaOperationRepository.Instance.DeleteExpired(db);
                logger.Info($"idempotency_cleanup operation=gacha expiredBacklog={backlog} deleted={deleted}");
            }
            catch (Exception exception) { logger.Error(exception, "Gacha operation cleanup failed."); }
        }

        private void CleanupCharacterStarUp()
        {
            try
            {
                using SqlSugarClient db = DBService.Instance.GetGameInstance();
                long backlog = db.Ado.SqlQuery<long>(
                    "SELECT COUNT(*) FROM player_character_starup_operation WHERE expires_at < UTC_TIMESTAMP(6)")
                    .Single();
                int deleted = CharacterStarUpOperationRepository.Instance.DeleteExpired(db);
                logger.Info($"idempotency_cleanup operation=character_starup expiredBacklog={backlog} deleted={deleted}");
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Character star-up operation cleanup failed.");
            }
        }
    }
}
