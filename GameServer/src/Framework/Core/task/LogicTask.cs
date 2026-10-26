using Framework.Core.Net;
using Framework.Core.Serializer;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Framework.Core.task
{
    public class LogicEvent
    {
        public IdSession s;
        public short module;
        public short cmd;
        public object msg;
        public long playerId;
        public long requestId;
    }

    public class LogicTask : AbstractDistributeTask
    {
       
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private object handler;
        /** target method of the controller */
        private MethodInfo method;

        private int stype;

        public Queue<LogicEvent> eventQueue;


        public void PushEvent(IdSession s, long playerId, short module, short cmd, object msg, long requestId = 0)
        {
            LogicEvent e = new LogicEvent();
            e.module = module;
            e.cmd = cmd;
            e.msg = msg;
            e.s = s;
            e.playerId = playerId;
            e.requestId = requestId;

            lock (this.eventQueue)
            {
                this.eventQueue.Enqueue(e);
            }
        }

        public static LogicTask Create(long distributeKey, int stype, object handler, MethodInfo method)
        {
            LogicTask t = new LogicTask();
            t.distributeKey = distributeKey;
            t.stype = stype;
            t.handler = handler;
            t.method = method;
            t.eventQueue = new Queue<LogicEvent>();

            
            return t;
        }

        public override void DoAction()
        {
            try
            {
                method.Invoke(handler, null);
            }
            catch (Exception e)
            {
                logger.Error(e,
                    $"logic task execute failed: handler={method?.DeclaringType?.FullName}.{method?.Name}, " +
                    $"serverType={stype}, distributeKey={distributeKey}{Environment.NewLine}{e}");
            }
        }
    }
}
