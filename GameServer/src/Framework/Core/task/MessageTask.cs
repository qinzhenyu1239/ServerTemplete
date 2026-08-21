using DotNetty.Transport.Channels;
using Framework.Core.Net;
using Framework.Core.Serializer;
using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Core.task {
    public class MessageTask : AbstractDistributeTask
    {
        /** io session context */
        private IdSession session;
        /** message controller */
        private object handler;
        /** target method of the controller */
        private MethodInfo method;
        /**arguments passed to the method */
        private object[] @params;



        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static MessageTask Create(long distributeKey, object handler, MethodInfo method, object[] @params, IdSession s) {
            MessageTask t = new MessageTask();
            t.distributeKey = distributeKey;

            t.handler = handler;
            t.method = method;
            t.@params = @params;
            t.session = s;

            return t;
        }

        public override void DoAction() {
            try
            {
                object response = method.Invoke(handler, @params);
                if (response != null) {
                    long utag = (long)(@params[2]);
                    // MessagePusher.PushMessage(playerId, (Message)response);
                    MessagePusher.PushMessage(this.session, (Message)response, utag);
                }
            }
            catch (Exception e)
            {
                logger.Warn("message task execute failed" + e.Message);
            }
        }
    }

    public class HttpMessageTask : AbstractDistributeTask
    {
        /** io session context */
        private IChannelHandlerContext context;
        /** message controller */
        private object handler;
        /** target method of the controller */
        private MethodInfo method;
        /**arguments passed to the method */
        private object[] @params;

        private static int autoId = 0;

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static HttpMessageTask Create(object handler, MethodInfo method, object[] @params, IChannelHandlerContext s)
        {
            HttpMessageTask t = new HttpMessageTask();
            t.distributeKey = HttpMessageTask.autoId++;

            t.handler = handler;
            t.method = method;
            t.@params = @params;
            t.context = s;

            return t;
        }

        public override void DoAction()
        {
            try
            {
                object response = method.Invoke(handler, @params);
                if (response != null)
                {
                    HttpRouteDispatcher.Instance.SendHttpRespones(this.context, response);
                }
            }
            catch (Exception e)
            {
                logger.Warn("message task execute failed" + e.Message);
            }
        }
    }
}
