using System;
using System.Diagnostics;
using System.Threading;

namespace Framework.Core.task
{
    /// <summary>Runs one recurring task on its own thread using a thread-confined TimerTaskMgr.</summary>
    public sealed class TimerTaskWorker
    {
        private readonly object lifecycleGate = new();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly string name;
        private readonly Action action;
        private readonly float interval;
        private readonly float delay;
        private Thread thread;
        private ManualResetEventSlim stopSignal;

        public TimerTaskWorker(string name, Action action, float interval, float delay)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Worker name is required.", nameof(name));
            if (!float.IsFinite(interval) || interval <= 0) throw new ArgumentOutOfRangeException(nameof(interval));
            if (!float.IsFinite(delay) || delay < 0) throw new ArgumentOutOfRangeException(nameof(delay));
            this.name = name;
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            this.interval = interval;
            this.delay = delay;
        }

        public void Start()
        {
            lock (lifecycleGate)
            {
                if (thread != null)
                {
                    if (stopSignal.IsSet) throw new InvalidOperationException($"Worker {name} is stopping.");
                    return;
                }
                var signal = new ManualResetEventSlim(false);
                var worker = new Thread(() => Run(signal)) { IsBackground = true, Name = name };
                stopSignal = signal;
                thread = worker;
                try { worker.Start(); }
                catch
                {
                    thread = null;
                    stopSignal = null;
                    signal.Dispose();
                    throw;
                }
            }
        }

        public void Stop()
        {
            Thread worker;
            ManualResetEventSlim signal;
            lock (lifecycleGate)
            {
                worker = thread;
                if (worker == null) return;
                if (Thread.CurrentThread == worker)
                    throw new InvalidOperationException("A timer worker cannot join itself.");
                signal = stopSignal;
                signal.Set();
            }

            // Keep dependencies alive if a callback is still running after the shutdown deadline.
            if (!worker.Join(TimeSpan.FromSeconds(30)))
                throw new TimeoutException($"Timed out waiting for worker {name} to stop.");

            lock (lifecycleGate)
            {
                if (thread != worker) return;
                thread = null;
                stopSignal = null;
                signal.Dispose();
            }
        }

        private void Run(ManualResetEventSlim signal)
        {
            try
            {
                var timers = new TimerTaskMgr();
                timers.Init();
                timers.Schedule(_ =>
                {
                    if (signal.IsSet) return;
                    try { action(); }
                    catch (Exception exception) { logger.Error(exception, $"Timer worker {name} task failed."); }
                }, repeat: -1, interval: interval, delay: delay);

                var clock = Stopwatch.StartNew();
                while (!signal.Wait(100))
                {
                    float elapsed = (float)clock.Elapsed.TotalSeconds;
                    clock.Restart();
                    // TimerTaskMgr fires at most once per update, so missed ticks do not build a queue.
                    timers.Update(elapsed);
                }
            }
            catch (Exception exception) { logger.Error(exception, $"Timer worker {name} stopped unexpectedly."); }
        }
    }
}
