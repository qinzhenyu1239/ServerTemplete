using Framework.Core.task;
using Game.Core.GM_MailMessage;
using System;

namespace Game.Core.Maintenance
{
    public sealed class MailCleanupService
    {
        public static readonly MailCleanupService Instance = new();
        private readonly TimerTaskWorker cleanupWorker;

        private MailCleanupService()
        {
            cleanupWorker = new TimerTaskWorker("MailExpiryWorker",
                GM_MailMsgMgr.Instance.ExpireDue, interval: 1f, delay: 1f);
        }

        public void Start()
        {
            if (GM_MailMsgMgr.Instance.Capacity <= 0)
                throw new InvalidOperationException("MailboxCapacity is required.");
            cleanupWorker.Start();
        }

        public void Stop()
        {
            cleanupWorker.Stop();
        }
    }
}
