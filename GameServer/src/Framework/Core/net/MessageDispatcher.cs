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

        private bool IsValidPacket(byte[] data, int offset, int count)
        {
            bool valid = data != null && offset >= 0 && count >= ProtocolPacketLayout.HeaderLength && offset <= data.Length - count;
            if (!valid) logger.Warn($"Invalid business packet: offset={offset}, count={count}, dataLength={data?.Length ?? 0}");
            return valid;
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
            SessionMgr.Instance.UnbindAuthenticatedAccount(s);
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
            if (!IsValidPacket(data, offset, count)) return;
            short module = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.ModuleOffset); // LE的方式
            short cmd = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.CommandOffset);

            // 网关,可能要用到utag, 暂时保留;
            long routeTag = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RouteTagOffset);
            short logicServerId = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.LogicServerIdOffset);
            long requestId = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RequestIdOffset);

            Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + ProtocolPacketLayout.HeaderLength, count - ProtocolPacketLayout.HeaderLength);

            string key = BuildKey(module, cmd);
            MODULE_CMD_HANDLERS.TryGetValue(key, out CmdExecutor cmdExecutor);
            if (cmdExecutor != null)
            {
                // 处理函数反射时候需要用到的参数;
                object[] @params = GWConvertToMethodParams(s, cmdExecutor.@params, msg, routeTag);
                TaskWorkerPool.Instance.AddTask(MessageTask.Create(s.distributeKey, cmdExecutor.handler, cmdExecutor.method, @params, s, routeTag, requestId));
                return;
            }
            else
            {  // 查看一下是否为Logic服务事件,然后直接投递到逻辑服线程处理
                LogicWorkerPool.Instance.GWPushMsgToLogicServer(routeTag, logicServerId, module, cmd, msg, requestId);
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
            if (!IsValidPacket(data, offset, count)) return;
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
            if (!IsValidPacket(data, offset, count)) return;
            // this.logger.Debug($"On Client Recv Cmd: {s.GetRemoteAddress()}");

            short module = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.ModuleOffset); // LE的方式
            module = (short)(module & 0x00ff);
            short cmd = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.CommandOffset);

            if (s.accountId <= 0 &&
                !(module == (short)Game.Module.AUTH && cmd == (short)Cmd.eGameAuthenticateReq))
            {
                logger.Warn($"Unauthenticated client attempted module[{module}] cmd[{cmd}]");
                s.client?.CloseAsync();
                return;
            }

            long routeTag = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RouteTagOffset);
            long requestId = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RequestIdOffset);

            Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + ProtocolPacketLayout.HeaderLength, count - ProtocolPacketLayout.HeaderLength);

            string key = BuildKey(module, cmd);
            MODULE_CMD_HANDLERS.TryGetValue(key, out CmdExecutor cmdExecutor);
            if (cmdExecutor != null)
            {
                // 处理函数反射时候需要用到的参数;
                // 这里有Bug,要根据命令号，模块号，来区分一下;
                object[] @params = ConvertToMethodParams(s, cmdExecutor.@params, msg);
                TaskWorkerPool.Instance.AddTask(MessageTask.Create(s.distributeKey, cmdExecutor.handler, cmdExecutor.method, @params, s, routeTag, requestId));
                return;
            }
            else {  // 查看一下是否为Logic服务事件,然后直接投递到逻辑服线程处理
                LogicWorkerPool.Instance.PushMsgToLogicServer(s, module, cmd, msg, requestId);
            }
        }

        public CmdExecutor GetCmdExecutor(short module, short cmd) {
            string key = BuildKey(module, cmd);
            
            MODULE_CMD_HANDLERS.TryGetValue(key, out CmdExecutor cmdExecutor);
            return cmdExecutor;

        }
    }

}


