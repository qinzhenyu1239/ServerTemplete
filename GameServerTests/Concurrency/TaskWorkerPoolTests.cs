using Framework.Core.task;
using System.Threading;

namespace GameServerTests.Concurrency;

public sealed class TaskWorkerPoolTests
{
    [Fact]
    public void AddTask_RejectsWhenTheSelectedWorkerQueueIsFull()
    {
        var pool = new TaskWorkerPool();
        using var started = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var queuedCompleted = new ManualResetEventSlim(false);

        try
        {
            pool.Start(1, 1);
            Assert.True(pool.AddTask(new ActionTask(1, () =>
            {
                started.Set();
                release.Wait();
            })));
            Assert.True(started.Wait(TimeSpan.FromSeconds(2)));

            Assert.True(pool.AddTask(new ActionTask(1, queuedCompleted.Set)));
            Assert.False(pool.AddTask(new ActionTask(1, () => { })));
            Assert.Equal(1, pool.RejectedTaskCount);

            release.Set();
            Assert.True(queuedCompleted.Wait(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            release.Set();
            pool.Stop();
        }
    }

    [Fact]
    public void Worker_ContinuesAfterAHandlerThrows()
    {
        var pool = new TaskWorkerPool();
        using var completed = new ManualResetEventSlim(false);

        try
        {
            pool.Start(1, 2);
            Assert.True(pool.AddTask(new ActionTask(1,
                () => throw new InvalidOperationException("forced"))));
            Assert.True(pool.AddTask(new ActionTask(1, completed.Set)));

            Assert.True(completed.Wait(TimeSpan.FromSeconds(2)));
            Assert.Equal(1, pool.ActiveThreadCount);
        }
        finally
        {
            pool.Stop();
        }
    }

    private sealed class ActionTask : AbstractDistributeTask
    {
        private readonly Action action;

        internal ActionTask(long key, Action action)
        {
            distributeKey = key;
            this.action = action;
        }

        public override void DoAction() => action();
    }
}
