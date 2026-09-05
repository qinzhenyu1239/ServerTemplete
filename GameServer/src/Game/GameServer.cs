using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.task;
using Game.Core.Configs;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Core.GM_Auth;
using Game.Core.GM_Backpack;
using Game.Core.GM_Bonues;
using Game.Core.GM_MailMessage;
using Game.Core.GM_Rank;
using Game.Core.GM_Task;
using Game.Core.GM_Trading;
using Game.Entries.Modules;
using Game.LogicServer;
using Game.Utils;
using NLog;
using NLog.Config;
using System.Configuration;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Game
{
    public class GameServer
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        public static GameServer Instance = new GameServer();

        private NettySocketServer server;
        private ServerRole role;

        private void StartNetSocketServer()
        {
            server = new NettySocketServer();
            bool isSsl = bool.Parse(ConfigurationManager.AppSettings["IsSsl"]);

            if (role == ServerRole.ServerZone)
            {
                int port = int.Parse(ConfigurationManager.AppSettings["HttpServerZonePort"]);
                server.StartHttpServer(port, isSsl).Wait();
                return;
            }

            if (role == ServerRole.Login)
            {
                int port = int.Parse(ConfigurationManager.AppSettings["LoginHttpPort"]);
                server.StartHttpServer(port, isSsl).Wait();
                return;
            }

            bool gatewayMode = role == ServerRole.Gateway ||
                               bool.Parse(ConfigurationManager.AppSettings["GatewayMode"]);
            bool isGateway = role == ServerRole.Gateway;
            bool isGameServer = role == ServerRole.Game;

            int tcpPort = int.Parse(ConfigurationManager.AppSettings["port"]);
            int wsPort = int.Parse(ConfigurationManager.AppSettings["wsPort"]);
            int httpPort = int.Parse(ConfigurationManager.AppSettings["httpPort"]);
            int gatewayConnectPort = int.Parse(ConfigurationManager.AppSettings["GwConnectPort"]);
            int gatewayTcpPort = int.Parse(ConfigurationManager.AppSettings["GatewayTcpPort"]);
            int gatewayWsPort = int.Parse(ConfigurationManager.AppSettings["GatewayWsPort"]);
            int gatewayHttpPort = int.Parse(ConfigurationManager.AppSettings["GatewayHttpPort"]);

            logger.Info($"\n[ServerRole = {role}, GatewayMode = {gatewayMode}]");
            if (!gatewayMode)
            {
                server.StartSterverAt(tcpPort, wsPort, isSsl).Wait();
                server.StartHttpServer(httpPort, isSsl).Wait();
                return;
            }

            if (isGateway)
            {
                server.StartGatewayServerAt(gatewayTcpPort, gatewayWsPort, isSsl).Wait();
                server.StartHttpServer(gatewayHttpPort, isSsl).Wait();
            }
            if (isGameServer) server.StartGwToServerSocketAt(gatewayConnectPort).Wait();
            if (isGateway) GatewayMgr.Instance.GatewayConnectToGameServer().Wait();
        }

        private void FrameworkInit()
        {
            LogManager.Configuration = new XmlLoggingConfiguration("nlog.config");
            role = ServerRoleConfig.Current;

            if (role == ServerRole.ServerZone)
            {
                HttpRouteDispatcher.Instance.Init("/PullingServerZone");
                return;
            }

            MessageFactory.Instance.InitMeesagePool();
            if (role == ServerRole.Login)
            {
                HttpRouteDispatcher.Instance.Init(
                    "/ReqGuestLogin", "/ReqRegisterUser", "/ReqUserLogin",
                    "/ReqGuestUpgrade", "/ReqIssueGameTicket");
                return;
            }

            MessageDispatcher.Instance.Init();
            HttpRouteDispatcher.Instance.Init();
            HttpRouteDispatcher.Instance.RemoveRoutes(
                "/GetAccountId", "/ReqGuestLogin", "/ReqRegisterUser",
                "/ReqUserLogin", "/ReqGuestUpgrade", "/ReqIssueGameTicket");
            SessionMgr.Instance.Init();

            bool gatewayMode = role == ServerRole.Gateway ||
                               bool.Parse(ConfigurationManager.AppSettings["GatewayMode"]);
            bool isGateway = role == ServerRole.Gateway;
            if (gatewayMode && isGateway) GatewayMgr.Instance.Init();
            TaskWorkerPool.Instance.Start(5);
        }

        private void GameLogicInit()
        {
            if (role == ServerRole.ServerZone)
            {
                ServerZoneModule.Instance.Init();
                return;
            }

            DBService.Instance.Init();
            RedisService.Instance.Init();
            GM_AccountEntityMgr.Instance.Init();
            if (role == ServerRole.Login)
            {
                AuthModule.Instance.Init();
                return;
            }

            ConfigMgr.Instance.Init();
            GachaModule.Instance.Init();
            ServerZoneModule.Instance.Init();
            ServerDirectConnModule.Instance.Init();
            RuleProcesserFactory.Init();
            GM_EntityMgr.Instance.Init();
            GM_SkillMgr.Instance.Init();
            GM_BuffMgr.Instance.Init();
            GM_MailMsgMgr.Instance.Init();
            GM_BonuesMgr.Instance.Init();
            GM_TaskMgr.Instance.Init();
            GM_RankMgr.Instance.Init();
            GM_BackpackMgr.Instance.Init();
            GM_TradingMgr.Instance.Init();
            PlayerModule.Instance.Init();
            GM_AuthSessionMgr.Instance.Init();
            LogicWorkerPool.Instance.Start();
            LogicServerFactory.Instance.Init();
        }

        public void Start()
        {
            var stopWatch = Stopwatch.StartNew();
            logger.Info($"服务器:[{ConfigurationManager.AppSettings["ServerName"]}] 开始启动...");
            FrameworkInit();
            GameLogicInit();
            StartNetSocketServer();
            stopWatch.Stop();
            logger.Info($"\n服务器启动完成,耗时[{stopWatch.ElapsedMilliseconds}]毫秒");
        }

        public async Task Shutdown()
        {
            if (server != null) await server.Shutdown();
            if (role == ServerRole.ServerZone) return;

            RedisService.Instance.Shutdown();
            if (role == ServerRole.Login) return;

            LogicWorkerPool.Instance.Stop();
            TaskWorkerPool.Instance.Stop();
            bool isGateway = role == ServerRole.Gateway;
            if (isGateway) GatewayMgr.Instance.Stop();
        }
    }
}
