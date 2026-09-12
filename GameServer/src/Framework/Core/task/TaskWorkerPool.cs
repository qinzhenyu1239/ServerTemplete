using System;
using System.Threading;
using System.Threading.Channels;

namespace Framework.Core.task
{
    internal sealed class TaskWorker : IDisposable
    {
        internal readonly Channel<AbstractDistributeTask> TaskQueue;
        internal readonly CancellationTokenSource Shutdown = new();
        internal readonly ManualResetEventSlim Stopped = new(false);

        internal TaskWorker(int capacity)
        {
            TaskQueue = Channel.CreateBounded<AbstractDistributeTask>(
                new BoundedChannelOptions(capacity)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait,
                    AllowSynchronousContinuations = false
                });
        }

        public void Dispose()
        {
            Shutdown.Dispose();
            Stopped.Dispose();
        }
    }

    public class TaskWorkerPool
    {
        private const int DefaultQueueCapacityPerWorker = 256;
        public static readonly TaskWorkerPool Instance = new();

        private readonly object lifecycleGate = new();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private TaskWorker[] workers = Array.Empty<TaskWorker>();
        private int running;
        private long rejectedTaskCount;

        public int ThreadCount { get; private set; }
        public int ActiveThreadCount;
        public long RejectedTaskCount => Interlocked.Read(ref rejectedTaskCount);
        public bool ThrowException;

        public void Start(int threadNum, int queueCapacityPerWorker = DefaultQueueCapacityPerWorker)
        {
            threadNum = Math.Clamp(threadNum, 1, 1000);
            if (queueCapacityPerWorker <= 0)
                throw new ArgumentOutOfRangeException(nameof(queueCapacityPerWorker));

            lock (lifecycleGate)
            {
                if (Volatile.Read(ref running) != 0)
                    throw new InvalidOperationException("Task worker pool is already running.");

                var newWorkers = new TaskWorker[threadNum];
                for (int index = 0; index < newWorkers.Length; index++)
                    newWorkers[index] = new TaskWorker(queueCapacityPerWorker);

                workers = newWorkers;
                ThreadCount = threadNum;
                Interlocked.Exchange(ref rejectedTaskCount, 0);
                Volatile.Write(ref running, 1);

                foreach (TaskWorker worker in newWorkers)
                    ThreadPool.QueueUserWorkItem(ThreadRun, worker);
            }
        }

        private void ExecuteTask(AbstractDistributeTask task)
        {
            try
            {
                task.DoAction();
            }
            catch (Exception exception)
            {
                logger.Error(
                    "Message handler exception:{0}, {1}, {2}, {3}",
                    exception.InnerException,
                    exception.Message,
                    exception.Source,
                    exception.StackTrace);
            }
        }

        private void ThreadRun(object state)
        {
            var worker = (TaskWorker)state;
            Interlocked.Increment(ref ActiveThreadCount);
            try
            {
                while (true)
                {
                    AbstractDistributeTask task;
                    try
                    {
                        task = worker.TaskQueue.Reader
                            .ReadAsync(worker.Shutdown.Token)
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch (OperationCanceledException) when (worker.Shutdown.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (ChannelClosedException)
                    {
                        break;
                    }

                    ExecuteTask(task);
                }
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Task worker stopped unexpectedly.");
            }
            finally
            {
                Interlocked.Decrement(ref ActiveThreadCount);
                worker.Stopped.Set();
            }
        }

        public void Stop()
        {
            TaskWorker[] snapshot;
            lock (lifecycleGate)
            {
                if (Interlocked.Exchange(ref running, 0) == 0) return;
                snapshot = workers;
                workers = Array.Empty<TaskWorker>();
                ThreadCount = 0;
            }

            foreach (TaskWorker worker in snapshot)
            {
                worker.TaskQueue.Writer.TryComplete();
                worker.Shutdown.Cancel();
            }

            foreach (TaskWorker worker in snapshot)
            {
                if (!worker.Stopped.Wait(TimeSpan.FromSeconds(5)))
                {
                    logger.Warn("Timed out waiting for a task worker to stop.");
                    continue;
                }
                worker.Dispose();
            }
        }

        public bool AddTask(AbstractDistributeTask task)
        {
            if (task == null || Volatile.Read(ref running) == 0) return false;

            TaskWorker[] snapshot = workers;
            if (snapshot.Length == 0) return false;
            int index = (int)Math.Abs(task.distributeKey % snapshot.Length);
            if (snapshot[index].TaskQueue.Writer.TryWrite(task)) return true;

            long rejected = Interlocked.Increment(ref rejectedTaskCount);
            if (rejected == 1 || rejected % 100 == 0)
                logger.Warn($"Task queue is full; rejectedTasks={rejected}.");
            return false;
        }
    }
}
