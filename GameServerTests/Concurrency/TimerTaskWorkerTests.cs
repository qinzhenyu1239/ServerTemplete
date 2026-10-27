using Framework.Core.task;

namespace GameServerTests.Concurrency;

public sealed class TimerTaskWorkerTests
{
    [Fact]
    public void SeparateWorkersUseDedicatedThreadsAndDoNotBlockEachOther()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var completed = new ManualResetEventSlim();
        Thread firstThread = null;
        Thread secondThread = null;
        var first = new TimerTaskWorker("BlockedCleanup", () =>
        {
            firstThread = Thread.CurrentThread;
            started.Set();
            release.Wait();
        }, 1f, 0f);
        var second = new TimerTaskWorker("IndependentCleanup", () =>
        {
            secondThread = Thread.CurrentThread;
            completed.Set();
        }, 1f, 0f);
        try
        {
            first.Start();
            first.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            second.Start();
            Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
            Assert.NotSame(firstThread, secondThread);
            Assert.False(firstThread.IsThreadPoolThread);
            Assert.False(secondThread.IsThreadPoolThread);
            Assert.True(firstThread.IsBackground);
            Assert.Equal("BlockedCleanup", firstThread.Name);
            Assert.Equal("IndependentCleanup", secondThread.Name);
        }
        finally
        {
            release.Set();
            first.Stop();
            second.Stop();
        }
    }

    [Fact]
    public void CallbackExceptionDoesNotStopFollowingTicks()
    {
        using var completed = new ManualResetEventSlim();
        int calls = 0;
        var worker = new TimerTaskWorker("FaultingCleanup", () =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException("forced");
            completed.Set();
        }, 0.1f, 0f);
        try
        {
            worker.Start();
            Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(Volatile.Read(ref calls) >= 2);
        }
        finally { worker.Stop(); }
    }

    [Fact]
    public async Task StopWaitsForRunningCallbackAndWorkerCanRestart()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var stopping = new ManualResetEventSlim();
        int calls = 0;
        var worker = new TimerTaskWorker("StoppingCleanup", () =>
        {
            Interlocked.Increment(ref calls);
            started.Set();
            release.Wait();
        }, 0.1f, 0f);
        Task stop = null;
        try
        {
            worker.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            stop = Task.Run(() => { stopping.Set(); worker.Stop(); });
            Assert.True(stopping.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(150);
            Assert.False(stop.IsCompleted);
            Assert.Equal(1, Volatile.Read(ref calls));
            release.Set();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            int stoppedCalls = Volatile.Read(ref calls);
            await Task.Delay(200);
            Assert.Equal(stoppedCalls, Volatile.Read(ref calls));
            worker.Stop();

            started.Reset();
            worker.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(Volatile.Read(ref calls) > stoppedCalls);
        }
        finally
        {
            release.Set();
            if (stop != null) await stop.WaitAsync(TimeSpan.FromSeconds(5));
            worker.Stop();
        }
    }

    [Fact]
    public void StopWakesWorkerDuringLongInitialDelay()
    {
        using var called = new ManualResetEventSlim();
        var worker = new TimerTaskWorker("DelayedCleanup", called.Set, 21600f, 300f);
        worker.Stop();
        worker.Start();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        worker.Stop();
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2));
        Assert.False(called.IsSet);
    }
}
