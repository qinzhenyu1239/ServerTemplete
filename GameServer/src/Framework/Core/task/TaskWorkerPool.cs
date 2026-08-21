using System;
using System.Collections.Generic;
using System.Threading;

namespace Framework.Core.task {

    class TaskWorker {
        // 任务队列Task, 操作对垒，都是线程安全的;
        public Queue<AbstractDistributeTask> taskQueue = new Queue<AbstractDistributeTask>();
        public AutoResetEvent taskEvent = new AutoResetEvent(true);


    }

    public class TaskWorkerPool {
        public static TaskWorkerPool Instance = new TaskWorkerPool();

        private List<TaskWorker> workerPool = new List<TaskWorker>();
        private bool Running = false;

        

        public int ThreadCount = 0; // 我们一供有多少线程;
        public int ActiveThreadCount = 0; // 多少个线程正在调度我们的任务


        public bool ThrowException = false;

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public void Start(int ThreadNum) {
            this.ThreadCount = ThreadNum;
            this.ThreadCount = (this.ThreadCount < 1) ? 1 : this.ThreadCount;
            this.ThreadCount = (this.ThreadCount > 1000) ? 1000 : this.ThreadCount;


            Running = true;

            for (int i = 0; i < this.ThreadCount; i++) {
                var workerData = new TaskWorker();
                this.workerPool.Add(workerData);

                ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadRun), workerData);
            }
        }

        private void ExceTask(AbstractDistributeTask task) {
            try
            {
                task.DoAction();
            }
            catch (Exception ex) {
                this.logger.Error("Message handler exception:{0}, {1}, {2}, {3}", ex.InnerException, ex.Message, ex.Source, ex.StackTrace);
            }
        }

        private void ThreadRun(Object stateInfo) {
            TaskWorker worker = (TaskWorker)stateInfo;
            try
            {
                ActiveThreadCount = Interlocked.Increment(ref ActiveThreadCount);

                while (Running) {
                    if (worker.taskQueue.Count == 0) { // 等待任务队列里面有任务
                        worker.taskEvent.WaitOne();
                        continue;
                    }

                    AbstractDistributeTask task = worker.taskQueue.Dequeue();
                    TaskWorkerPool.Instance.ExceTask(task);
                }
            }
            catch
            {
            }
            finally
            {
                ActiveThreadCount = Interlocked.Decrement(ref ActiveThreadCount);
            }
        }

        public void Stop() {
            Running = false;

            
            while (ActiveThreadCount > 0) {
                this.logger.Info("广播消息处理线程退出...");
                for (int i = 0; i < this.workerPool.Count; i++) {
                    this.workerPool[i].taskQueue.Clear();
                    this.workerPool[i].taskEvent.Set();
                }
                Thread.Sleep(1000);
            }

            
        }

        public void AddTask(AbstractDistributeTask t) {
            if (this.Running == false) {
                return;
            }

            int index = (int)(t.distributeKey % this.workerPool.Count);

            this.workerPool[index].taskQueue.Enqueue(t);
            this.workerPool[index].taskEvent.Set();
        }
    }

}

