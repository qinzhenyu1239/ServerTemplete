using Framework.Core.task;
using Game.Core.GM_CharacterStarUp;
using Game.Core.GM_Gacha;
using System;

namespace Game.Core.Maintenance
{
    public sealed class EconomicOperationCleanupService
    {
        public static readonly EconomicOperationCleanupService Instance = new();
        private readonly TimerTaskWorker cleanupWorker;

        private EconomicOperationCleanupService()
        {
            cleanupWorker = new TimerTaskWorker("EconomicCleanupWorker", Cleanup,
                interval: (float)TimeSpan.FromHours(6).TotalSeconds,
                delay: (float)TimeSpan.FromMinutes(5).TotalSeconds);
        }

        public void Start()
        {
            cleanupWorker.Start();
        }

        public void Stop()
        {
            cleanupWorker.Stop();
        }

        private void Cleanup()
        {
            GM_GachaMgr.Instance.CleanupExpiredOperations();
            GM_CharacterStarUpMgr.Instance.CleanupExpiredOperations();
        }
    }
}
