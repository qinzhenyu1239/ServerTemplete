using Enyim.Caching.Configuration;
using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.task;
using Framework.Core.Utils;
using Game;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.LogicServer;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Core.task
{
    public abstract class BaseLogicServer
    {
        protected NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        protected Queue<LogicEvent> eventQueue = null;
        MethodInfo OnUpdate = null;

        protected int fps = 20;
        protected long timePerFrame = 0;

        private long lastFrameMillis;

        public int stype; // 逻辑服务对应的类型;
        public int instanceId; // 逻辑服对应的实例Id;

        private Dictionary<int, MethodInfo> keyMethodDic;


        public int onlinePlayers = 0;
        protected object instConfig = null;

        protected TimerTaskMgr timerMgr = null;
        public TimerTaskMgr TimerTaskMgr { get { return timerMgr; } }

        public void SetTargetFPS(int fps) {
            this.fps = fps;
            this.timePerFrame = 1000 / (fps);
        }

        public virtual void Init(Queue<LogicEvent> eventQueue, object instConfig = null)
        {
            this.instConfig = instConfig;
            this.eventQueue = eventQueue;
            this.onlinePlayers = 0;
            this.timerMgr = new TimerTaskMgr();
            this.timerMgr.Init();

            this.SetTargetFPS(20); // 可以从配置表里面来获取这个instConfig的FPS配置;

            Type t = this.GetType();
            this.OnUpdate = t.GetMethod("OnUpdate");

            MethodInfo OnStart = t.GetMethod("OnStart");
            if (OnStart != null) {
                OnStart.Invoke(this, null);
            }


            this.keyMethodDic = new Dictionary<int, MethodInfo>();
            MethodInfo[] methods = t.GetMethods();
            foreach (MethodInfo method in methods)
            {
                RequestMapping mapperAttribute = method.GetCustomAttribute<RequestMapping>();
                if (mapperAttribute == null)
                {
                    continue;
                }


                short[] meta = MessageDispatcher.Instance.GetMessageMeta(method);
                short cmd = meta[1]; // LogicServer的网络消息，我们只用cmd来做key; 

         
                if (keyMethodDic.ContainsKey(cmd))
                {
                    throw new RuntimeBinderException($"[致命错误]:[{cmd}]重复注册");
                }

                keyMethodDic.Add(cmd, method);
            }


            this.lastFrameMillis = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            
        }

        public bool IsAppRunning() {
            return (LogicWorkerPool.Instance.Running);
        }

        private void ProcessNetEvents() {
            
            lock (this.eventQueue) {
                while (this.eventQueue.Count > 0) {
                    LogicEvent e = this.eventQueue.Dequeue();

                    
                    if (this.keyMethodDic.ContainsKey(e.cmd)) {
                        var ret = this.keyMethodDic[e.cmd].Invoke(this, new object[] { e.s, e.msg, e.playerId});
                        if (ret != null) {
                            MessagePusher.PushMessage(e.s, ret as Message, e.playerId, requestId: e.requestId);
                        }
                    }
                }
            }
        }


        // 多线程环境下调用EnterLogicServer / DoReconnectToGameInLogicServer
        // 后续考虑线程安全的问题;
        public abstract int EnterLogicServer(long playerId, int zid/*, int logicServerId*/);
        public abstract int DoReconnectToGameInLogicServer(long playerId);

        public abstract int ExitLogicServer(long playerId, int reason);

        

        public abstract bool HasZone(int zid);

        public void MainLoop() {

            while (this.IsAppRunning()) {
                // Wait For Target FPS
                long nowMillis = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

                if (nowMillis - this.lastFrameMillis < this.timePerFrame)
                {
                    int sleepTime = (int)(this.timePerFrame - (nowMillis - this.lastFrameMillis));
                    Thread.Sleep(sleepTime);
                    nowMillis = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                }

                long dt = (nowMillis - this.lastFrameMillis);
                this.lastFrameMillis = nowMillis;

                // 处理所有的事件
                this.ProcessNetEvents();
                // end

                // 迭代游戏逻辑
                float duration = ((float)(dt)) / 1000.0f;
                this.timerMgr.Update(duration);

                if (this.OnUpdate != null) {
                    this.OnUpdate.Invoke(this, new object[] { duration });
                }
            }
        }
    }
}
