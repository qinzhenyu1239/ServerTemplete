using Enyim.Caching.Configuration;
using Framework.Core.Net;
using Framework.Core.Utils;
using Game;
using Game.Core.EntityMgr;
using Game.Datas.GMEntities;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Framework.Core.task
{

    class LogicWorker
    {
        // 任务队列Task, 操作对垒，都是线程安全的;
        public Queue<AbstractDistributeTask> taskQueue = new Queue<AbstractDistributeTask>();
        public AutoResetEvent taskEvent = new AutoResetEvent(true);


    }

    public class LogicWorkerPool
    {
        public static LogicWorkerPool Instance = new LogicWorkerPool();

        private List<LogicWorker> workerPool = new List<LogicWorker>();
        public bool Running = false;

        // private Dictionary<int, Dictionary<int ,LogicTask>> logicServers = new Dictionary<int, Dictionary<int, LogicTask>>();
        private Dictionary<int, LogicTask> logicServers = new Dictionary<int, LogicTask>();

        private Dictionary<int, Type> typeofLogicServers = new Dictionary<int, Type>();

        public int ThreadCount = 0; // 我们一供有多少线程;
        public int ActiveThreadCount = 0; // 多少个线程正在调度我们的任务


        public bool ThrowException = false;

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public void Start()
        {
            IEnumerable<Type> servers = TypeScanner.ListTypesWithAttribute(typeof(LogicServerMeta));

            foreach (Type server in servers) {
                LogicServerMeta meta = server.GetCustomAttribute<LogicServerMeta>();
                if (this.typeofLogicServers.ContainsKey(meta.stype)) {
                    this.logger.Error($"重复注册的LogicServer类型: {meta.stype}");
                    continue;
                }

                this.typeofLogicServers.Add(meta.stype, server);
            }
            Running = true;
        }

        public BaseLogicServer AddLogicServerInstance(int stype, int instId, object instConfig) {
            if (!this.typeofLogicServers.ContainsKey(stype)) {
                this.logger.Warn($"启动LogicServer实例失败,找不到服务类型: {stype}");
                return null;
            }

            Type t = this.typeofLogicServers[stype];
            BaseLogicServer handler = (BaseLogicServer)Activator.CreateInstance(t);
            if (handler == null) {
                this.logger.Warn("创建LogicServer实例失败");
                return null;
            }
            handler.instanceId = instId;
            handler.stype = stype;
            int index = this.ThreadCount;

            MethodInfo method = t.GetMethod("MainLoop");
            LogicServerMeta meta = t.GetCustomAttribute<LogicServerMeta>();
            LogicTask task = LogicTask.Create(index, meta.stype, handler, method);
            handler.Init(task.eventQueue, instConfig);

            
            this.logicServers.Add(handler.instanceId, task);

            var workerData = new LogicWorker();
            this.workerPool.Add(workerData);

            ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadRun), workerData);
            this.ThreadCount = this.workerPool.Count;
            this.AddTask(task);

            return handler;
        }

        private void ExceTask(AbstractDistributeTask task)
        {
            try
            {
                task.DoAction();
            }
            catch (Exception ex)
            {
                this.logger.Error("Message handler exception:{0}, {1}, {2}, {3}", ex.InnerException, ex.Message, ex.Source, ex.StackTrace);
            }
        }

        private void ThreadRun(Object stateInfo)
        {
            LogicWorker worker = (LogicWorker)stateInfo;
            try
            {
                ActiveThreadCount = Interlocked.Increment(ref ActiveThreadCount);

                while (Running)
                {
                    if (worker.taskQueue.Count == 0)
                    { // 等待任务队列里面有任务
                        worker.taskEvent.WaitOne();
                        continue;
                    }

                    AbstractDistributeTask task = worker.taskQueue.Dequeue();
                    LogicWorkerPool.Instance.ExceTask(task);
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

        public void Stop()
        {
            Running = false;


            while (ActiveThreadCount > 0)
            {
                this.logger.Info("广播逻辑服线程退出...");
                for (int i = 0; i < this.workerPool.Count; i++)
                {
                    this.workerPool[i].taskQueue.Clear();
                    this.workerPool[i].taskEvent.Set();
                }
                Thread.Sleep(1000);
            }


        }

        public void AddTask(AbstractDistributeTask t)
        {
            if (this.Running == false)
            {
                return;
            }

            int index = (int)(t.distributeKey % this.workerPool.Count);

            this.workerPool[index].taskQueue.Enqueue(t);
            this.workerPool[index].taskEvent.Set();
        }

        public void PushMsgToLogicServer(IdSession s, short module, short cmd, object msg) {

            
            if (/*s.accountId <= 0 || */s.playerId <= 0 || s.logicServerId == -1) {
                return;
            }

            int instId = s.logicServerId;
            
            if (this.logicServers.ContainsKey(instId)) {
                LogicTask task = this.logicServers[instId];
                task.PushEvent(s, s.playerId, module, cmd, msg);
            }
        }

        public void GWPushMsgToLogicServer(long playerId, short logicServerId, short module, short cmd, object msg)
        {


            if (playerId <= 0 || logicServerId == -1)
            {
                return;
            }

            int instId = logicServerId;

            if (this.logicServers.ContainsKey(instId))
            {
                LogicTask task = this.logicServers[instId];
                task.PushEvent(SessionMgr.Instance.GatewaySession, playerId, module, cmd, msg);
            }
        }
    }

}

