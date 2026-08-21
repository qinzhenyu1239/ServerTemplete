using System;

namespace Framework.Core.task {
    public abstract class AbstractDistributeTask {

        public long distributeKey; // Session 哪里的唯一的key;
        private long startMillis; // 每个任务的起始时间戳;
        private long endMillis; // 每个任务结束的时间戳;

        public abstract void DoAction();

        public string getName() => GetType().Name;

        public long getStartMillis() => startMillis;

        public long getEndMillis() => endMillis;

        public void markStartMillis() => startMillis = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        public void markEndMillis() => endMillis = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

    }
}

