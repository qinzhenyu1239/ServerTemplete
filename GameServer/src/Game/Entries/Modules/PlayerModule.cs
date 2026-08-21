
using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using Game.Core.GM_Bonues;
using Game.Core.Caches;
using Game.Core.EntityMgr;
using Game.Datas.GMEntities;
using Game.Core.GM_Task;
using System.Collections.Generic;
using Game.Core.GM_MailMessage;
using Game.Core.GM_Rank;
using Game.Core.GM_Backpack;
using Game.Core.GM_Trading;
using Framework.Core.task;
using System.Threading.Tasks.Sources;
using Game.LogicServer;
using Framework.Core.Serializer;
using System.Configuration;
using System.Threading.Tasks;

namespace Game.Entries.Modules
{
    public class PlayerModule
    {
        public static PlayerModule Instance = new PlayerModule();

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private bool GatewayMode = false;
        private string httpAuthServerUrl = "http://127.0.0.1:6090/GetAccountId";
        private string httpPlayerServerUrl = "http://127.0.0.1:6090/GetPlayerId";
        public void Init()
        {
            this.GatewayMode = bool.Parse(ConfigurationManager.AppSettings["GatewayMode"]);
            this.httpAuthServerUrl = $"{ConfigurationManager.AppSettings["HttpAuthAndPlayerWebsite"]}GetAccountId";
            this.httpPlayerServerUrl = $"{ConfigurationManager.AppSettings["HttpAuthAndPlayerWebsite"]}GetPlayerId";
            PlayerIDCache.Instance.Init();
            PlayerAcountIDCache.Instance.Init();
            PlayerIdLoginBonuesCache.Instance.Init();
        }

        private void CopyDbPlayerToResponesData(Game.Datas.DBEntities.Player p, PlayerInfo pInfo)
        {
            pInfo.exp = p.exp;
            pInfo.hp = p.HP;
            pInfo.mp = p.MP;
            pInfo.umoney = p.umoney;
            pInfo.unick = p.name;
            pInfo.ucion = p.ucoin;
            pInfo.usex = p.usex;
        }

        public ResRecvLoginBonues HandlerRecvLoginBonues(IdSession s, ReqRecvLoginBonues req, long playerId)
        {
            ResRecvLoginBonues res = new ResRecvLoginBonues();

            // 验证accountId, playerID账号;
            if (playerId <= 0)
            {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }


            Loginbonues data = PlayerIdLoginBonuesCache.Instance.Get(playerId);
            if (data == null)
            {
                res.status = (int)Respones.SystemErr;
                return res;
            }

            if (data.days == 0 || data.status != 0)
            { // 奖励不能领取，非法操作
                res.status = (int)Respones.InvalidOpt;
                return res;
            }


            res.status = (int)Respones.OK;
            res.num = data.bonues;

            Game.Datas.DBEntities.Player p = PlayerIDCache.Instance.Get((playerId));
            if (p != null)
            { // 至于奖励是什么，根据游戏需求来处理即可;
                // logger.Info($"{p.GetHashCode()}");
                p.ucoin += data.bonues;
            }

            if (p != null)
            {
                PlayerIDCache.Instance.UpdateDataToDb(p);
            }

            // Game.Datas.DBEntities.Player p = PlayerAcountIDCache.Instance.Get(s.accountIdAndJob);
            /* 
             * 这里可能有Bug,要测试一下，看如何处理，如果不行一个Player 只能是一个职业;
            p = PlayerAcountIDCache.Instance.Get(accountIdAndJob);
            if (p != null)
            {
                // logger.Info($"{p.GetHashCode()}");
                p.ucoin += data.bonues;
            }*/

            data.bonues = 0;
            data.status = 1;
            PlayerIdLoginBonuesCache.Instance.UpdateDataToDb(data);



            return res;
        }

        public ResSelectPlayer HandlerHttpReqSelectPlayer(ReqSelectPlayer req, long accountId)
        {
            ResSelectPlayer res = new ResSelectPlayer();
            long utag = accountId;

            // 验证accountId账号;
            if (accountId <= 0) {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            // 检查req参数,如果有非法的，那么就返回参数错误
            if (req.job <= 0)
            {  // 根据自己的实际情况，检查参数
                res.status = (int)Respones.InvalidParams;
                return res;
            }

            Game.Datas.DBEntities.Player p = PlayerAcountIDCache.Instance.GetOrCreate(accountId, req);
            if (p == null)
            {
                res.status = (int)Respones.SystemErr;
                return res;
            }

            if (p.status != 0)
            { // 判断玩家是否被冻结;
                res.status = (int)Respones.PlayerIsFreeze;
                return res;
            }

            res.status = (int)Respones.OK;

            res.pInfo = new PlayerInfo(); // 
            this.CopyDbPlayerToResponesData(p, res.pInfo);


            // http 模式下，都要返回playerId
            res.playerId = p.id;
            
            // 检查一下每日登录奖励
            this.CheckLoginBonues(p.id, res.pInfo);
            // end

            // 创建玩家Entity出来
            int isReConnectGame = GM_EntityMgr.Instance.AddPlayer(null, p.id);
            res.isReConnectGame = isReConnectGame;
            // end

            return res;
        }

        public async void HandlerReqSelectPlayer(IdSession s, ReqSelectPlayer req, long accountId)
        {
            ResSelectPlayer res = new ResSelectPlayer();
            long utag = accountId;

            // 验证accountId账号;
            if (accountId <= 0)
            {
                if (req.openId != 0)
                {
                    // 像服务器发送请求，获取openId对应的accountId;
                    accountId = await HttpUtils.HttpPost(httpAuthServerUrl, req.openId);
                    if (accountId <= 0)
                    {
                        res.status = (int)Respones.AccountIsNotLogin;
                        MessagePusher.PushMessage(s, (Message)res, utag); // 网关模式的utag,这里要再仔细检查下;
                        return;
                    }
                }
                else
                {
                    res.status = (int)Respones.AccountIsNotLogin;
                    MessagePusher.PushMessage(s, (Message)res, utag); // 网关模式的utag,这里要再仔细检查下;
                    return;
                }
            }

            // 检查req参数,如果有非法的，那么就返回参数错误
            if (req.job <= 0)
            {  // 根据自己的实际情况，检查参数
                res.status = (int)Respones.InvalidParams;
                MessagePusher.PushMessage(s, (Message)res, utag);
            }

            Game.Datas.DBEntities.Player p = PlayerAcountIDCache.Instance.GetOrCreate(accountId, req);
            if (p == null)
            {
                res.status = (int)Respones.SystemErr;
                MessagePusher.PushMessage(s, (Message)res, utag);
            }

            if (p.status != 0)
            { // 判断玩家是否被冻结;
                res.status = (int)Respones.PlayerIsFreeze;
                MessagePusher.PushMessage(s, (Message)res, utag);
            }

            res.status = (int)Respones.OK;

            res.pInfo = new PlayerInfo(); // 
            this.CopyDbPlayerToResponesData(p, res.pInfo);

            
            if (this.GatewayMode) {
                res.playerId = p.id;
            }
            s.playerId = p.id;
            s.accountIdAndJob = PlayerAcountIDCache.Instance.Key(accountId, req.job);

            // 检查一下每日登录奖励
            this.CheckLoginBonues(p.id, res.pInfo);
            // end

            // 创建玩家Entity出来
            int isReConnectGame = GM_EntityMgr.Instance.AddPlayer(s, p.id);
            res.isReConnectGame = isReConnectGame;
            // end

            MessagePusher.PushMessage(s, (Message)res, utag);
        }

        public ResPullingPlayerData HandlerHttpReqPullingPlayerData(ReqPullingPlayerData req, long accountId)
        {
            ResPullingPlayerData res = new ResPullingPlayerData();
            long utag = accountId;
            // 验证accountId账号;
            if (accountId <= 0) {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            int job = req.job; // 如果一个账号只允许一个职业，那么job = 0;
            // 判断职业的类型，1,(战士), 2(法师),
            // 做好job的参数判断，具体游戏，具体来定;

            // 到数据库中查找，看下有没有对应的Player 数据;
            // accountId 与 job --->来查询;
            Game.Datas.DBEntities.Player p = PlayerAcountIDCache.Instance.TryGetPlayer(accountId, req.job);
            if (p == null)
            {
                res.status = (int)Respones.PlayerIsNotExist;
                return res;
            }

            if (p.status != 0)
            { // 判断玩家是否被冻结;
                res.status = (int)Respones.PlayerIsFreeze;
                return res;
            }


            res.status = (int)Respones.OK; // 暂时返回系统错误;
            res.pInfo = new PlayerInfo(); // 
            this.CopyDbPlayerToResponesData(p, res.pInfo);

            res.playerId = p.id;
            
            // 检查一下每日登录奖励
            this.CheckLoginBonues(p.id, res.pInfo);
            // end


            // 测试, 每次登录，给玩家奖励180个金币
            // Game.Datas.Excels.BonuesRuleA configItem = (Game.Datas.Excels.BonuesRuleA)ExcelUtils.GetConfigData<Game.Datas.Excels.BonuesRuleA>("100002");
            // GM_BonuesMgr.Instance.GenBonuesToPlayer(s.playerId, configItem.ID, configItem.value);
            // end

            // 创建玩家Entity出来
            int isReConnectGame = GM_EntityMgr.Instance.AddPlayer(null, p.id);
            res.isReConnectGame = isReConnectGame;
            // end

            return res;
        }

        public async void HandlerReqPullingPlayerData(IdSession s, ReqPullingPlayerData req, long accountId)
        {
            ResPullingPlayerData res = new ResPullingPlayerData();
            long utag = accountId;
            // 验证accountId账号;
            if (accountId <= 0)
            {
                if (req.openId != 0)
                {
                    // 像服务器发送请求，获取openId对应的accountId;
                    accountId = await HttpUtils.HttpPost(httpAuthServerUrl, req.openId);
                    if (accountId <= 0)
                    {
                        res.status = (int)Respones.AccountIsNotLogin;
                        MessagePusher.PushMessage(s, (Message)res, utag); // 网关模式的utag,这里要再仔细检查下;
                        return;
                    }
                }
                else {
                    res.status = (int)Respones.AccountIsNotLogin;
                    MessagePusher.PushMessage(s, (Message)res, utag); // 网关模式的utag,这里要再仔细检查下;
                    return;
                }
                
            }

            int job = req.job; // 如果一个账号只允许一个职业，那么job = 0;
            // 判断职业的类型，1,(战士), 2(法师),
            // 做好job的参数判断，具体游戏，具体来定;

            // 到数据库中查找，看下有没有对应的Player 数据;
            // accountId 与 job --->来查询;
            Game.Datas.DBEntities.Player p = PlayerAcountIDCache.Instance.TryGetPlayer(accountId, req.job);
            if (p == null)
            {
                res.status = (int)Respones.PlayerIsNotExist;
                MessagePusher.PushMessage(s, (Message)res, utag);
                return;
            }

            if (p.status != 0)
            { // 判断玩家是否被冻结;
                res.status = (int)Respones.PlayerIsFreeze;
                MessagePusher.PushMessage(s, (Message)res, utag);
                return;
            }


            res.status = (int)Respones.OK; // 暂时返回系统错误;
            res.pInfo = new PlayerInfo(); // 
            this.CopyDbPlayerToResponesData(p, res.pInfo);

            s.playerId = p.id;
            if (this.GatewayMode) {
                res.playerId = p.id;
            }
            s.accountIdAndJob = PlayerAcountIDCache.Instance.Key(accountId, req.job);

            // 检查一下每日登录奖励
            this.CheckLoginBonues(p.id, res.pInfo);
            // end


            // 测试, 每次登录，给玩家奖励180个金币
            // Game.Datas.Excels.BonuesRuleA configItem = (Game.Datas.Excels.BonuesRuleA)ExcelUtils.GetConfigData<Game.Datas.Excels.BonuesRuleA>("100002");
            // GM_BonuesMgr.Instance.GenBonuesToPlayer(s.playerId, configItem.ID, configItem.value);
            // end

            // 创建玩家Entity出来
            int isReConnectGame = GM_EntityMgr.Instance.AddPlayer(s, p.id);
            res.isReConnectGame = isReConnectGame;
            // end
            MessagePusher.PushMessage(s, (Message)res, utag);

        }

        private void CheckLoginBonues(long playerId, PlayerInfo res)
        {
            res.hasBonues = 0;
            Loginbonues data = PlayerIdLoginBonuesCache.Instance.GetOrCreate(playerId);
            if (data == null)
            {
                logger.Error($"每日登录奖励获取为null: {playerId}");
                return;
            }

            if (data.status == 0)
            {
                res.hasBonues = 1;
                res.days = data.days;
                res.loginBonues = data.bonues;
            }

            bool hasLoginBonues = (data.bonues_time < UtilsHelper.TimestampToday());
            if (!hasLoginBonues)
            {
                return;
            }

            bool isSustain = (data.bonues_time < UtilsHelper.TimestampToday()) &&
                (data.bonues_time >= UtilsHelper.TimestampYesterday());

            data.days = (isSustain) ? (data.days + 1) : 1;
            data.bonues = 100; // 要发放奖励，你可以根据配置来;
            data.bonues_time = (int)(UtilsHelper.Timestamp());
            data.status = 0; // 未领取状态;

            res.hasBonues = 1;
            res.days = data.days;
            res.loginBonues = data.bonues;

            PlayerIdLoginBonuesCache.Instance.UpdateDataToDb(data);
        }


        public ResPullingBonuesList HandlerReqPullingBonuesList(IdSession s, ReqPullingBonuesList req, long playerId)
        {
            ResPullingBonuesList res = new ResPullingBonuesList();
            // 验证accountId, playerID账号;
            if (playerId <= 0) {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            int typeId = req.typeId; // 如果你有这个id限制到时候，你就自己再加sql找出来;

            Bonues[] datas = GM_BonuesMgr.Instance.PullingBonuesData(playerId, typeId);
            if (datas != null && datas.Length > 0)
            {
                res.bonues = new BonuesItem[datas.Length];
                for (int i = 0; i < datas.Length; i++)
                {
                    /*if (datas[i].status != 0) { // 根据需求来决定
                        continue;
                    }*/
                    res.bonues[i] = new BonuesItem();
                    res.bonues[i].bonuesId = datas[i].id;
                    res.bonues[i].bonuesDesic = datas[i].bonuesDesic;
                    res.bonues[i].status = datas[i].status;
                    res.bonues[i].typeId = datas[i].tid;
                }
            }


            res.status = (int)Respones.OK;

            return res;
        }

        public ResRecvBonues HandlerReqRecvBonues(IdSession s, ReqRecvBonues req, long playerId) {
            ResRecvBonues res = new ResRecvBonues();
            // 验证accountId, playerID账号;
            if (playerId <= 0) {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            Bonues data = GM_BonuesMgr.Instance.GetBonuesBy(req.bonuesId);
            if (data == null || data.uid != playerId || data.status != 0) {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            GM_BonuesMgr.Instance.RecvBonues(req.bonuesId, data);
            res.status = (int)Respones.OK;
            res.typeId = data.tid;
            res.b1 = data.bonues1;
            res.b2 = data.bonues2;
            res.b3 = data.bonues3;
            res.b4 = data.bonues4;
            res.b5 = data.bonues5;

            return res;
        }

        public ResPullingTaskList HandlerReqPullingTaskList(IdSession s, ReqPullingTaskList req, long playerId) {
            ResPullingTaskList res = new ResPullingTaskList();
            // 验证accountId, playerID账号;
            if (playerId <= 0)
            {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            int typeId = req.typeId;

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null) {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            List<GM_Task> tasks = GM_TaskMgr.Instance.PullingTaskList(player, typeId);

            if (tasks != null && tasks.Count > 0)
            {
                res.tasks = new TaskItem[tasks.Count];
                for (int i = 0; i < tasks.Count; i++)
                {
                    /*if (datas[i].status != 0) { // 根据需求来决定
                        continue;
                    }*/
                    res.tasks[i] = new TaskItem();
                    res.tasks[i].taskDesic = tasks[i].taskDesic;
                    res.tasks[i].status = tasks[i].dbTaskInst.status;
                    res.tasks[i].typeId = tasks[i].dbTaskInst.tid;
                }
            }


            res.status = (int)Respones.OK;
            return res;
        }

        public ResUpdateMailMsg HandlerReqUpdateMailMsg(IdSession s, ReqUpdateMailMsg req, long playerId) {
            ResUpdateMailMsg res = new ResUpdateMailMsg();
            // 验证accountId, playerID账号;
            if (playerId <= 0)
            {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null) {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            GM_MailMsgMgr.Instance.UpdateMailMsgStatus(req.mailMsgId, req.status);
            res.status = (int)Respones.OK;
            return res;
        }

        public ResPullingMailMsg HandlerReqPullingMailMsg(IdSession s, ReqPullingMailMsg req, long playerId) {
            ResPullingMailMsg res = new ResPullingMailMsg();
            // 验证accountId, playerID账号;
            if (playerId <= 0)
            {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null) {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            res.status = (int)Respones.OK;

            Game.Datas.DBEntities.Mailmessage[] mailMessages = GM_MailMsgMgr.Instance.PullingMailMsg(playerId, 0);
            if (mailMessages != null && mailMessages.Length > 0) {
                res.mailMessages = new MailMsgItem[mailMessages.Length];
                for (int i = 0; i < mailMessages.Length; i++) {
                    res.mailMessages[i] = new MailMsgItem();
                    res.mailMessages[i].msgBody = mailMessages[i].msgBody;
                    res.mailMessages[i].status = mailMessages[i].status;
                    res.mailMessages[i].sendTime = mailMessages[i].sendTime;
                    res.mailMessages[i].msgId = mailMessages[i].id;
                }
            }


            return res;
        }

        public ResPullingRank HandlerReqPullingRank(IdSession s, ReqPullingRank req, long playerId) {
            ResPullingRank res = new ResPullingRank();
            // 验证accountId, playerID账号;
            if (playerId <= 0)
            {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null)
            {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }
            res.status = (int)Respones.OK;
            res.selfIndex = -1;

            // int rankType = req.typeId;
            int rankType = (int)RankType.WorldCoin;
            RankData[] rankData = GM_RankMgr.Instance.GetRankData(rankType, 30);
            if (rankData == null || rankData.Length <= 0) {
                return res;
            }

            res.ranks = new RankItem[rankData.Length];
            for (int i = 0; i < rankData.Length; i++) {
                RankItem item = new RankItem(); // uid 不要放給客户端;

                Player p = PlayerIDCache.Instance.Get(rankData[i].uid);
                if (p == null) {
                    this.logger.Error($"rankdata Error uid {rankData[i].uid}");
                    continue;
                }
                Account a = AccountIDCache.Instance.Get(p.accountId);
                if (a == null) {
                    this.logger.Error($"rankdata Error uid {rankData[i].uid}");
                    continue;
                }
                item.uface = a.uface;
                item.unick = a.unick;
                item.value = rankData[i].value;

                if (rankData[i].uid == playerId) {
                    res.selfIndex = i;
                }

                res.ranks[i] = item;
            }


            return res;
        }

        public async void HandlerReqEnterLogicServer(IdSession s, ReqEnterLogicServer req, long playerId)
        {
            ResEnterLogicServer res = new ResEnterLogicServer();
            // 验证accountId, playerID账号;
            if (playerId <= 0)
            {
                if (req.openId > 0) {
                    // 像服务器发送请求，获取openId对应的accountId;
                    playerId = await HttpUtils.HttpPost(httpPlayerServerUrl, req.openId);
                    if (playerId <= 0) {
                        res.status = (int)Respones.AccountIsNotLogin;
                        MessagePusher.PushMessage(s, (Message)res, playerId); // 网关模式的utag,这里要再仔细检查下;
                        return;
                    }

                    // 走openid, 非网关模式,Session,保存PlayerId;
                    if (this.GatewayMode == false) {
                        s.playerId = playerId;
                    }
                    
                }
            }

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null)
            {
                // 
                // res.status = (int)Respones.InvalidOpt;
                // return res;
                GM_EntityMgr.Instance.AddPlayer(s, playerId);
                player = GM_EntityMgr.Instance.Get(playerId);
                if (player == null)
                {
                    res.status = (int)Respones.InvalidOpt;
                    MessagePusher.PushMessage(s, (Message)res, playerId);
                }
            }
            else {
                // 重新赋值一下session, http 模式下，有可能还没有session;
                player.uPlayer.s = s;
            }

            // 不在网关模式下,如果是网关模式，我直接在网关就处理了;
            if (this.GatewayMode == false && s.logicServerId != -1)
            {
                res.status = (int)Respones.InOtherLogicServerInst;
                MessagePusher.PushMessage(s, (Message)res, playerId);
            }

            bool isReconnGame = false;
            if (player.uRoomGame.logicServerId != -1 && 
                player.uRoomGame.playInRoomState == (int)PlayerInRoomState.Playing) {
                req.instId = player.uRoomGame.logicServerId;
                isReconnGame = true;
            }

            if (req.instId == -1) { // 自动寻找;
                req.instId = LogicServerFactory.Instance.FindLogicServerInst(req.typeId, req.zoneId);
                if (req.instId == -1) {
                    this.logger.Warn($"Can not find {req.typeId}:{req.zoneId} LogicServer");
                    res.status = (int)Respones.LogicServerIsBusy;
                    MessagePusher.PushMessage(s, (Message)res, playerId);
                }
            }

            res.status = this.ProcEnterLogicServer(playerId, s, req.instId, req.zoneId, isReconnGame);

            if (res.status == (int)Respones.OK) { // 为了网关来特殊调整;
                // 网关[2, 2, 8, 2, body]
                MessagePusher.PushMessage(s, res, playerId, (short)req.instId);
            }
        }

        private int ProcEnterLogicServer(long playerId, IdSession s, int instId, int zid, bool isReconnGame) {

            BaseLogicServer server = LogicServerFactory.Instance.GetLogciServer(instId);
            if (server == null) {
                return (int)Respones.LogicServerInstIsNotExist;
            }

            int status = (int)Respones.OK;
            if (!isReconnGame)
            {
                status = server.EnterLogicServer(playerId, zid/*, instId*/);
            }
            else {
                status = server.DoReconnectToGameInLogicServer(playerId);
            }
            
            if (status != (int)Respones.OK) {
                return (int)status;
            }

            s.logicServerId = instId;

            return (int)Respones.OK;
        }

        public ResExchangeProduct HandlerReqExchangeProduct(IdSession s, ReqExchangeProduct req, long playerId) {
            ResExchangeProduct res = new ResExchangeProduct();
            // 验证accountId, playerID账号;
            if (/*s.accountId <= 0 || */playerId <= 0)
            {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null)
            {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            int status = GM_TradingMgr.Instance.CanExchangeProduct(player, req.productId);
            if (status != (int)Respones.OK) {
                res.status = status;
                return res;
            }

            GM_TradingMgr.Instance.DoExchangeProduct(player, req.productId);

            res.status = (int)Respones.OK;
            return res;
        }

        public ResPullingPackData HandlerReqPullingPackData(IdSession s, ReqPullingPackData req, long playerId) {

            ResPullingPackData res = new ResPullingPackData();
            // 验证accountId, playerID账号;
            if (playerId <= 0)
            {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null) {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            Dictionary<int, List<GoodsItem>> ret = GM_BackpackMgr.Instance.GetBackpackData(ref player.uBackpack); ;
            // res.packGoods = new List<DicGoodsItem>();
            var packGoods = new List<DicGoodsItem>();
            foreach (var key in ret.Keys) {
                DicGoodsItem dic = new DicGoodsItem();
                dic.mainType = key;
                dic.Value = (ret[key]).ToArray();
                packGoods.Add(dic);
            }
            res.packGoods = packGoods.ToArray();

            
            res.status = (int)Respones.OK;
            return res;
        }

        public ResTestUpdateGoods HandlerReqTestUpdatesGoods(IdSession s, ReqTestUpdateGooods req, long playerId) {
            ResTestUpdateGoods res = new ResTestUpdateGoods();
            // 验证accountId, playerID账号;
            if (playerId <= 0)
            {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null)
            {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            GM_BackpackMgr.Instance.UpdateGoodsWithTid(player, req.typeId, req.num);

            res.status = (int)Respones.OK;
            return res;
        }
        public ResTestGetGoods HandlerReqTestGetGoods(IdSession s, ReqTestGetGoods req, long playerId) {
            ResTestGetGoods res = new ResTestGetGoods();
            // 验证accountId, playerID账号;
            if (playerId <= 0)
            {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null) {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            // 测试代码
            if (req.typeId == 1) {  // 钻石
                // 更新玩家身上的属性，更新任务进度;
                GM_Task collectTask = GM_TaskMgr.Instance.GetCurrectTaskData(player, 100000);
                if (collectTask != null) {
                    GM_TaskMgr.Instance.UpdateTaskProgress(player, collectTask, "damond", req.num);
                }
            }
            else if (req.typeId == 2) {  // 天书
                GM_Task collectTask = GM_TaskMgr.Instance.GetCurrectTaskData(player, 100000);
                if (collectTask != null) {
                    GM_TaskMgr.Instance.UpdateTaskProgress(player, collectTask, "book", req.num);
                }
            }
            // end

            res.status = (int)Respones.OK;
            return res;
        }
    }
}

