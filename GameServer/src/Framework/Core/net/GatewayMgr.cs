using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Common.Utilities;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game;
using Game.Datas.Excels;
using Game.Datas.Messages;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Core.Net
{
    public class GameServerClientSocketHandler : ChannelHandlerAdapter
    {

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public GameServerClientSocketHandler() : base()
        {
        }

        // 网关连接上了GameServer
        public override void ChannelActive(IChannelHandlerContext context)
        { // 连接进来的
            IChannel channel = context.Channel;

            
        }

        
        public void OnGameServerToGatewayMsg(IdSession s, byte[] data, int offset, int count)
        {
            if (data == null || offset < 0 || count < ProtocolPacketLayout.HeaderLength || offset > data.Length - count)
            {
                logger.Warn($"Invalid game-server packet: offset={offset}, count={count}, dataLength={data?.Length ?? 0}");
                return;
            }
            // this.logger.Debug($"On Client Recv Cmd: {s.GetRemoteAddress()}");
            short module = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.ModuleOffset); // LE的方式
            short cmd = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.CommandOffset);
            long utag = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RouteTagOffset);
            long requestId = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RequestIdOffset);
            // utag ===>clientSession;
            IdSession clientSession = GatewayMgr.Instance.GetClientSession(utag/*, module, cmd*/);
            if (clientSession == null)  {
                this.logger.Error("utag Find ClientSession Error!");
                return;
            }

            if (cmd == (short)Cmd.eGuestLoginRes)
            {
                Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + ProtocolPacketLayout.HeaderLength, count - ProtocolPacketLayout.HeaderLength);
                ResGuestLogin res = msg as ResGuestLogin;
                clientSession.accountId = res.accountIdOrOpenId;
                res.accountIdOrOpenId = 0;
                MessagePusher.PushMessage(clientSession, msg, 0, 0, requestId);
                return;
            }
            else if (cmd == (short)Cmd.eUserLoginRes)
            {
                Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + ProtocolPacketLayout.HeaderLength, count - ProtocolPacketLayout.HeaderLength);
                ResUserLogin res = msg as ResUserLogin;
                clientSession.accountId = res.accountIdOrOpenId;
                res.accountIdOrOpenId = 0;
                MessagePusher.PushMessage(clientSession, msg, 0, 0, requestId);
                return;
            }
            else if (cmd == (short)Cmd.eSelectPlayerRes)
            {
                Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + ProtocolPacketLayout.HeaderLength, count - ProtocolPacketLayout.HeaderLength);
                ResSelectPlayer res = msg as ResSelectPlayer;
                clientSession.playerId = res.playerId;
                res.playerId = 0;
                MessagePusher.PushMessage(clientSession, msg, 0, 0, requestId);
                return;
            }
            else if (cmd == (short)Cmd.ePullingPlayerDataRes)
            {
                Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + ProtocolPacketLayout.HeaderLength, count - ProtocolPacketLayout.HeaderLength);
                ResPullingPlayerData res = msg as ResPullingPlayerData;
                clientSession.playerId = res.playerId;
                res.playerId = 0;
                MessagePusher.PushMessage(clientSession, msg, 0, 0, requestId);
                return;
            }
            else if (cmd == (short)Cmd.eEnterLogicServerRes)
            {
                short logicServerId = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.LogicServerIdOffset);
                clientSession.logicServerId = logicServerId;
            }
            else if (cmd == (short)Cmd.eExitLogicServerRes)
            { // 这里要判断是否正常离开,所以还是从GameServer传递过来;
                Message msg = SerializerHelper.PbDecode(module, cmd, data, offset + ProtocolPacketLayout.HeaderLength, count - ProtocolPacketLayout.HeaderLength);
                ResExitLogicServer res = msg as ResExitLogicServer;
                if (res.status == (int)Respones.OK) {
                    clientSession.logicServerId = -1;
                }
            }

            // 客户端不需要utag, 我们清理掉;
            UtilsHelper.WriteULongLE(data, offset + ProtocolPacketLayout.RouteTagOffset, 0);
            UtilsHelper.WriteShortLE(data, offset + ProtocolPacketLayout.LogicServerIdOffset, 0);

            clientSession.Send(data, offset, count);
        }

        // 网关收到GameServer 返回给我们的消息;
        public override void ChannelRead(IChannelHandlerContext context, object message)
        { // 有数据读的时候 
            var msg = message as IByteBuffer;
            IChannel channel = context.Channel;
            IdSession s = SessionMgr.Instance.GetSessionBy(channel);

            // 游戏服务器发送数据过来到网关;
            MessageTrafficLogger.Received(s, msg.Array, msg.ArrayOffset, msg.ReadableBytes);
            this.OnGameServerToGatewayMsg(s, msg.Array, msg.ArrayOffset, msg.ReadableBytes);
            // end
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush(); // 数据读取完的一个处理

        // 网关与游戏服务器断开;
        public override void ChannelInactive(IChannelHandlerContext context)
        { 
            IChannel channel = context.Channel;
            IdSession s = SessionMgr.Instance.GetSessionBy(channel);
            
            channel.DisconnectAsync();

            s.client = null;
            GatewayMgr.Instance.taskEvent.Set();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception e)
        { // 有异常的时候
            IChannel channel = context.Channel;
            if (channel.Active || channel.Open)
            {
                context.CloseAsync();
            }

            if (!(e is IOException))
            {
                this.logger.Debug("remote:" + channel.RemoteAddress, e.Message);
            }
        }
    }

    public class GatewayMgr
    {
        private string httpPlayerServerUrl = null;

        public static GatewayMgr Instance = new GatewayMgr();
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private long autoId = 0;


        private Dictionary<int, IdSession> machineSet = null; // 机器码===>GameServer Socket
        private AttributeKey<object> MACHINE_ID = AttributeKey<object>.ValueOf("MACHINE_ID");
        

        IEventLoopGroup gatewayGroup = null;
        Bootstrap bootStrap = null;
        private List<GatewayConfig> configs = null;

        

        public AutoResetEvent taskEvent = new AutoResetEvent(true);
        private bool isRunning = false;

        // 网关缓存客户端的Sesssion表;
        private Dictionary<long, IdSession> distributeKeyMap = null;
        private Dictionary<long, IdSession> accountIdMap = null;
        private Dictionary<long, IdSession> playerIdMap = null;

        public void Init() {
            this.autoId = 0;
            this.httpPlayerServerUrl = $"{ConfigurationManager.AppSettings["HttpAuthAndPlayerWebsite"]}GetPlayerId";

            this.distributeKeyMap = new Dictionary<long, IdSession>();
            this.accountIdMap = new Dictionary<long, IdSession>();
            this.playerIdMap = new Dictionary<long, IdSession>();

            this.machineSet = new Dictionary<int, IdSession>();
            this.configs = ExcelUtils.GetConfigDatas<GatewayConfig>(null);
            // 加载机器配置表, 连接到其他的GameServer
            for (int i = 0; i < this.configs.Count; i++) {
                GatewayConfig config = this.configs[i];
                if (config.Enabled == 0) {
                    continue;
                }
                // 每个机器，创建一个空的IdSession对象;
                IdSession client = SessionMgr.Instance.CreateIdSession(null);
                // 机器码===》IdSession 映射;
                this.machineSet.Add(config.ID, client);
            }
            // end

            this.gatewayGroup = new MultithreadEventLoopGroup(4);
            this.bootStrap = new Bootstrap();
            this.bootStrap.Group(this.gatewayGroup)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    IChannelPipeline pipeline = channel.Pipeline;
                    pipeline.AddLast("framing-enc", new LengthFieldPrepender(ByteOrder.LittleEndian, 2, 0, true));
                    pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ByteOrder.LittleEndian, ushort.MaxValue, 0, 2, -2, 2, true));

                    pipeline.AddLast("IoEventHandler", new GameServerClientSocketHandler());
                }));


            this.taskEvent.Reset();
            this.isRunning = true;
            ThreadPool.QueueUserWorkItem(new WaitCallback(GatewayReConnectThread), this);
        }

        private void GatewayReConnectThread(Object stateInfo) {
            
            while (this.isRunning) {
                this.logger.Info("Waiting for Scan ReConnect ...");
                this.taskEvent.WaitOne();

                if (this.isRunning == false) { // 唤醒退出;
                    break;
                }
                this.logger.Info("Begin Scan ReConnect ...");
                this.ScaneReconnectToServer().Wait();
                this.logger.Info("End Scan ReConnect ...");
            }

            this.logger.Info("网关重连接扫描线程退出 ...");
        }

        public void Stop() {
            this.isRunning = false;
            this.taskEvent.Set();
        }

        private async Task ScaneReconnectToServer() {
            for(int i = 0; i < this.configs.Count; i ++){
                GatewayConfig config = this.configs[i];
                if (config.Enabled == 0 || this.machineSet[config.ID].client != null) {
                    continue;
                }

                await this.ConnectToServer(config);
            }
        }

        private async Task ConnectToServer(GatewayConfig config) {
            try
            {
                var remoteAddress = new IPEndPoint(IPAddress.Parse(config.IpAddr), config.GwConnPort);
                this.logger.Info($"Gateway Connect To GameServer:{config.ID}-{config.IpAddr}:{config.GwConnPort}");
                IChannel channel = await this.bootStrap.ConnectAsync(remoteAddress);
                SessionMgr.Instance.SetSessionChannel(this.machineSet[config.ID], channel);
            }
            catch (Exception e) {
                this.logger.Warn(e.ToString());
                this.taskEvent.Set();
            }
            
        }

        public async Task GatewayConnectToGameServer() {
            for (int i = 0; i < this.configs.Count; i++) {
                if (this.configs[i].Enabled == 0) {
                    continue;
                }
                await this.ConnectToServer(this.configs[i]);
            }
        }

        public IdSession GetClientSession(long utag/*, short module, short cmd*/) {
            if (this.playerIdMap.ContainsKey(utag)) {
                return this.playerIdMap[utag];
            }

            if (this.accountIdMap.ContainsKey(utag)) {
                return this.accountIdMap[utag];
            }

            if (this.distributeKeyMap.ContainsKey(utag)) {
                return this.distributeKeyMap[utag];
            }

            return null;
        }

        private long GetClientUtag(IdSession clientSession, short module, short cmd) {
            CmdExecutor cmdExecutor = MessageDispatcher.Instance.GetCmdExecutor(module, cmd);
            long utag = clientSession.distributeKey; // 没有登录，不需要使用账号Id, playerId;

            if (cmdExecutor == null) { // LogicServer,命令都是用playerId
                utag = clientSession.playerId;
                this.playerIdMap.TryAdd(utag, clientSession);
                return utag;
            }
            else {
                for (int i = 0; i < cmdExecutor.@params.Length; i++) {
                    if (cmdExecutor.@params[i].ParameterType == typeof(long)) {
                        if (cmdExecutor.@params[i].Name.Equals("accountId")) {
                            utag = clientSession.accountId;
                            this.accountIdMap.TryAdd(utag, clientSession);
                            return utag;
                        }
                        else if (cmdExecutor.@params[i].Name.Equals("playerId")) {
                            utag = clientSession.playerId;
                            this.playerIdMap.TryAdd(utag, clientSession);
                            return utag;
                        }
                    }
                }
            }

            this.distributeKeyMap.TryAdd(utag, clientSession);
            return utag;
        }

        public async void SendMsgToGameServer(IdSession clientSession, byte[] data, int offset, int count) {
            short module = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.ModuleOffset); // LE的方式
            short cmd = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.CommandOffset);
            long requestId = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RequestIdOffset);



            int macheineId = module;
            macheineId = (macheineId & 0x0000ff00) >> 8;
            clientSession.machineId = (short)macheineId; // 网关模式断线后能知道在那个机器上游戏并断线
            module = (short)(module & 0x00ff);
            UtilsHelper.WriteShortLE(data, offset + ProtocolPacketLayout.ModuleOffset, module);

            if (cmd == (short)Cmd.eEnterLogicServerReq) {
                if (clientSession.playerId == 0) {
                    long openId = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RouteTagOffset);
                    clientSession.playerId = await HttpUtils.HttpPost(httpPlayerServerUrl, openId);
                }
            }
            long utag = GetClientUtag(clientSession, module, cmd);
            UtilsHelper.WriteULongLE(data, offset + ProtocolPacketLayout.RouteTagOffset, (ulong)utag);
            // utag = UtilsHelper.ReadLongLE(data, offset + 4);
            short logicServerId = (short)clientSession.logicServerId;
            UtilsHelper.WriteShortLE(data, offset + ProtocolPacketLayout.LogicServerIdOffset, (short)logicServerId);

            if (cmd == (short)Cmd.eEnterLogicServerReq) {
                if (logicServerId != -1) {
                    ResEnterLogicServer res = new ResEnterLogicServer();
                    res.status = (int)Respones.InOtherLogicServerInst;
                    MessagePusher.PushMessage(clientSession, res, 0, 0, requestId);
                    return;
                }
            }

            IdSession s = this.machineSet[macheineId];
            if (s == null) {
                this.logger.Error($"[机器码错误: {macheineId}]");
                return;
            }

            /*if (s.client == null) {
                this.logger.Warn($"[网关与机器: {macheineId}断开了!]");
            }*/

            s.Send(data, offset, count);
        }

        public void OnClientExitFromGateway(IdSession clientSession) {
            if (clientSession.logicServerId != -1) { // 说明玩家正在游戏
                ReqExitLogicServer req = new ReqExitLogicServer();
                req.quitReason = (int)QuitReason.DisconnectQuit;
                long utag = clientSession.playerId;
                short logicServerId = (short)clientSession.logicServerId;
                IdSession s = this.machineSet[clientSession.machineId];
                if (s != null) {
                    MessagePusher.PushMessage(s, req, s.playerId, logicServerId);
                }
            }

            if (this.distributeKeyMap.ContainsKey(clientSession.distributeKey)) {
                this.distributeKeyMap.Remove(clientSession.distributeKey);
            }

            if (this.accountIdMap.ContainsKey(clientSession.accountId)) {
                this.accountIdMap.Remove(clientSession.accountId);
            }

            if (this.playerIdMap.ContainsKey(clientSession.playerId)) {
                this.playerIdMap.Remove(clientSession.playerId);
            }
        }
    }
}
