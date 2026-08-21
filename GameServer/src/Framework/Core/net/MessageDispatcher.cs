using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Framework.Core.Serializer;
using Framework.Core.task;
using Framework.Core.Utils;
using Game;
using Game.Datas.Messages;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Framework.Core.Net {
    class MessageDispatcher {

        private const int HEAD_SIZE = 14; // [Module2字节, 2字节Cmd, utag8字节, 2字节LogicServerId]

        public static MessageDispatcher Instance = new MessageDispatcher();

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /** [module_cmd, CmdExecutor] */
        private static Dictionary<string, CmdExecutor> MODULE_CMD_HANDLERS = new();

        public short[] GetMessageMeta(MethodInfo method) {
            foreach (ParameterInfo parameter in method.GetParameters()) {
                if (parameter.ParameterType.IsAssignableTo(typeof(Message))) {
                    MessageMeta msgMeta = parameter.ParameterType.GetCustomAttribute<MessageMeta>();
                    if (msgMeta != null) {
                        short[] meta = { msgMeta.module, msgMeta.cmd };
                        return meta;
                    }
                }
            }

            return null;
        }

        public string BuildKey(short module, short cmd) {
            return module + "_" + cmd;
        }

        public void Init() {  // 初始化
            IEnumerable<Type> controllers = TypeScanner.ListTypesWithAttribute(typeof(Controller));
            foreach (Type controller in controllers) {
                try
                {
                    object handler = Activator.CreateInstance(controller);
                    MethodInfo[] methods = controller.GetMethods();

                    foreach (MethodInfo method in methods) {
                        RequestMapping mapperAttribute = method.GetCustomAttribute<RequestMapping>();
                        if (mapperAttribute == null) {
                            continue;
                        }

                        short[] meta = this.GetMessageMeta(method);
                        short module = meta[0];
                        short cmd = meta[1];

                        string key = BuildKey(module, cmd);
                        MODULE_CMD_HANDLERS.TryGetValue(key, out CmdExecutor cmdExecutor);
                        
                        if (cmdExecutor != null) {
                            logger.Warn($"module[{module}] cmd[{cmd}] 重复注册处理器");
                            return;
                        }

                        // cmdExecutor = CmdExecutor.Create(method, method.GetParameters().Select(parameterInfo => parameterInfo.ParameterType).ToArray(), handler);
                        cmdExecutor = CmdExecutor.Create(method, method.GetParameters(), handler);

                        MODULE_CMD_HANDLERS.Add(key, cmdExecutor);
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e.Message);
                }
            }
        }

        // 有新的客户端进来
        public void OnClientEnter(IdSession s, bool isWebSocket = false) {
            string socketType = (isWebSocket) ? "WebSocket" : "TcpSocket";
            this.logger.Debug($"On Client Enter[({socketType})]: {s.GetRemoteAddress()}");
        }

        public void OnClientEnterToGateway(IdSession s, bool isWebSocket = false)
        {
            string socketType = (isWebSocket) ? "WebSocket" : "TcpSocket";
            this.logger.Debug($"On Client Enter[({socketType})]: {s.GetRemoteAddress()}");
        }

        // 有客户端离开;
        public void OnClientExit(IdSession s) {
            this.logger.Debug($"On Client Exit: {s.GetRemoteAddress()}");
            if (s.logicServerId != -1) { // 说明玩家正在游戏
                short module = (int)Game.Module.SCENE;
                short cmd = (int)Cmd.eExitLogicServerReq;
                ReqExitLogicServer req = new ReqExitLogicServer();
                req.quitReason = (int)QuitReason.DisconnectQuit;
                LogicWorkerPool.Instance.PushMsgToLogicServer(s, module, cmd, req);
            }
        }

        public void OnClientExitFromGateway(IdSession s)
        {
            this.logger.Debug($"On Client Exit: {s.GetRemoteAddress()}");
            GatewayMgr.Instance.OnClientExitFromGateway(s);
        }

        private object[] GWConvertToMethodParams(IdSession session, ParameterInfo[] methodParams, Message message, long utag) {
            object[] result = new object[methodParams == null ? 0 : methodParams.Length];
            
            for (int i = 0; i < result.Length; i++) {
                Type param = methodParams[i].ParameterType;
                if (param.IsAssignableTo(typeof(IdSession)))
                {
                    result[i] = session;
                }
                else if (param.IsAssignableTo(typeof(long)))
                {
                    result[i] = utag;
                }
                else if (param.IsAssignableTo(typeof(Message))) {
                    result[i] = message;
                }
            }

            return result;
        }

        // 收到网关的消息
        public void OnGatewayToGameServerMsg(IdSession s, byte[] data, int offset, int count)
        {
            short module = UtilsHelper.ReadShortLE(data, offset + 0); // LE的方式
            short cmd = UtilsHelper.ReadShortLE(data, offset + 2);

            // 网关,可能要用到utag, 暂时保留;
            long utag = UtilsHelper.ReadLongLE(data, offset + 4);
            short logicServerId = UtilsHelper.ReadShortLE(data, offset + 12);

            Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + HEAD_SIZE, count - HEAD_SIZE);

            string key = BuildKey(module, cmd);
            MODULE_CMD_HANDLERS.TryGetValue(key, out CmdExecutor cmdExecutor);
            if (cmdExecutor != null)
            {
                // 处理函数反射时候需要用到的参数;
                object[] @params = GWConvertToMethodParams(s, cmdExecutor.@params, msg, utag);
                TaskWorkerPool.Instance.AddTask(MessageTask.Create(s.distributeKey, cmdExecutor.handler, cmdExecutor.method, @params, s));
                return;
            }
            else
            {  // 查看一下是否为Logic服务事件,然后直接投递到逻辑服线程处理
                LogicWorkerPool.Instance.GWPushMsgToLogicServer(utag, logicServerId, module, cmd, msg);
            }

        }

        private object[] ConvertToMethodParams(IdSession session, ParameterInfo[] methodParams, Message message)
        {
            object[] result = new object[methodParams == null ? 0 : methodParams.Length];

            for (int i = 0; i < result.Length; i++)
            {
                Type param = methodParams[i].ParameterType;
                if (param.IsAssignableTo(typeof(IdSession)))
                {
                    result[i] = session;
                }
                else if (param.IsAssignableTo(typeof(long)))
                {
                    if (methodParams[i].Name.Equals("accountId"))
                    {
                        result[i] = session.accountId;
                    }
                    else {
                        result[i] = session.playerId;
                    }
                }
                else if (param.IsAssignableTo(typeof(Message)))
                {
                    result[i] = message;
                }
            }

            return result;
        }

        public void OnClientMsgToGateway(IdSession s, byte[] data, int offset, int count)
        {
            // this.logger.Debug($"On Client Recv Cmd: {s.GetRemoteAddress()}");
            GatewayMgr.Instance.SendMsgToGameServer(s, data, offset, count);
            // 网关,可能要用到utag, 暂时保留;
            // long utag = UtilsHelper.ReadLongLE(data, offset + 4);
            /*
            Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + HEAD_SIZE, count - HEAD_SIZE);

            string key = BuildKey(module, cmd);
            MODULE_CMD_HANDLERS.TryGetValue(key, out CmdExecutor cmdExecutor);
            if (cmdExecutor != null)
            {
                // 处理函数反射时候需要用到的参数;
                // 这里有Bug,要根据命令号，模块号，来区分一下;
                object[] @params = ConvertToMethodParams(s, cmdExecutor.@params, msg);
                TaskWorkerPool.Instance.AddTask(MessageTask.Create(s.distributeKey, cmdExecutor.handler, cmdExecutor.method, @params, s));
                return;
            }
            else
            {  // 查看一下是否为Logic服务事件,然后直接投递到逻辑服线程处理
                LogicWorkerPool.Instance.PushMsgToLogicServer(s, module, cmd, msg);
            }*/

        }

        // 收到某个客户端的消息;
        public void OnClientMsg(IdSession s, byte[] data, int offset, int count) {
            // this.logger.Debug($"On Client Recv Cmd: {s.GetRemoteAddress()}");

            short module = UtilsHelper.ReadShortLE(data, offset + 0); // LE的方式
            module = (short)(module & 0x00ff);
            short cmd = UtilsHelper.ReadShortLE(data, offset + 2);

            // 网关,可能要用到utag, 暂时保留;
            // long utag = UtilsHelper.ReadLongLE(data, offset + 4);

            Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + HEAD_SIZE, count - HEAD_SIZE);

            string key = BuildKey(module, cmd);
            MODULE_CMD_HANDLERS.TryGetValue(key, out CmdExecutor cmdExecutor);
            if (cmdExecutor != null)
            {
                // 处理函数反射时候需要用到的参数;
                // 这里有Bug,要根据命令号，模块号，来区分一下;
                object[] @params = ConvertToMethodParams(s, cmdExecutor.@params, msg);
                TaskWorkerPool.Instance.AddTask(MessageTask.Create(s.distributeKey, cmdExecutor.handler, cmdExecutor.method, @params, s));
                return;
            }
            else {  // 查看一下是否为Logic服务事件,然后直接投递到逻辑服线程处理
                LogicWorkerPool.Instance.PushMsgToLogicServer(s, module, cmd, msg);
            }
        }

        public CmdExecutor GetCmdExecutor(short module, short cmd) {
            string key = BuildKey(module, cmd);
            
            MODULE_CMD_HANDLERS.TryGetValue(key, out CmdExecutor cmdExecutor);
            return cmdExecutor;

        }
    }

}


