

using Enyim.Caching.Memcached.Results.StatusCodes;
using Framework.Core.Serializer;
using Framework.Core.task;
using Game.Core.EntityMgr;
using Game.Core.GM_Backpack;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NLog.Time;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Runtime.CompilerServices;

namespace Game.LogicServer {
    enum RoomState { 
        Invalid = -1,
        Waiting = 0, // 等待玩家加入，等待玩家游戏Ready准备;
        Ready, // 已经达到可以开局的游戏要求，所有玩家都已经Ready; 随时可以开始;  
        Started, // 游戏正式开始了，大家进入到了游戏环节;
        CheckOut, // 游戏进入结算状态;
        RoundClear, // 结算完成，进入清理状态;
    }

    enum PlayerInRoomState {
        Invalid = -1, 
        InView = 0, // 玩家正在旁观
        Ready,  //  玩家做小已经准备好了;
        Playing, // 玩家正在游戏;
        CheckOut, // 玩家目前处于结算状态;
        RoundClear, // 玩家目前处于清理状态;
    }

    abstract public class RoomMgr {
        private static int autoId = 1;

        protected int MAX_PLAYER_NUM = 4;
        protected int INVIEW_NUM = 8;  // 8人里面，包含正在游戏的4人;
        protected int MIN_PLAYER_NUM = 2; // 最少多少玩家可以开局
        protected float READY_TIME = 3; //  玩家准备好的时间;
        protected int ROUND_TIME = 30 * 60; // 每一局游戏的时间
        protected float CHECKOUT_TIME = 5; // 每一局的结算时间;

        public int zid;
        public int roomId;

        public int state = (int)RoomState.Invalid;

        protected int stype = -1;

        protected bool isCheckGameStarted = false;

        protected NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // 四个玩家,5个玩家;
        protected long[] seats = null;

        // 玩家的旁观列表 ====> 先是进入旁观列表 ====>  就在给你找一个seat坐下;
        protected long[] inViews = null;

        protected float curTime = 0.0f; // 用来记录当前阶段过去的时间;

        protected float roundTime = 0.0f; // 本局游戏时间累积;
        protected bool isCheckCaled = false; // 是否已经计算了;
        protected bool hasReconnPlayer = false; // 是否有重新断线进来的玩家;

        protected TimerTaskMgr timerTaskMgr = null;

        public void SendMsg(long playerId, Message m) {
            GM_PlayerEntity e = GM_EntityMgr.Instance.Get(playerId);
            if (e == null || e.uPlayer.s == null || e.uRoomGame.reconnectState != -1) {
                return;
            }

            MessagePusher.PushMessage(e.uPlayer.s, m, playerId);
        }

        public void SendMsg(GM_PlayerEntity e, Message m)
        {
            if (e.uPlayer.s == null || e.uRoomGame.reconnectState != -1) {
                return;
            }

            MessagePusher.PushMessage(e.uPlayer.s, m, e.uPlayer.playerInfo.id);
        }

        public void BroadcastInView(Message msg, long exceptPlayerId = -1) {
            for (int i = 0; i < this.inViews.Length; i++) {
                if (this.inViews[i] == -1 || this.inViews[i] == exceptPlayerId) {
                    continue;
                }

                this.SendMsg(this.inViews[i], msg);
            }
        }

        public void BroadcastInSeat(Message msg, long exceptPlayerId = -1)
        {
            for (int i = 0; i < this.seats.Length; i++)
            {
                if (this.seats[i] == -1 || this.seats[i] == exceptPlayerId)
                {
                    continue;
                }

                this.SendMsg(this.seats[i], msg);
            }
        }

        public bool IsFull() {
            for (int i = 0; i < this.inViews.Length; i++) {
                if (this.inViews[i] == -1) {
                    return false;
                }
            }
            return true;
        }

        protected ResUserArrivedSeat GetUserArrivedSeatData(GM_PlayerEntity entity, int seatId) {
            ResUserArrivedSeat userData = new ResUserArrivedSeat();
            userData.unick = entity.uPlayer.playerInfo.name;
            userData.usex = entity.uPlayer.playerInfo.usex;
            userData.uface = entity.uPlayer.accountInfo.uface;

            userData.seatId = seatId;
            userData.playInRoomState = entity.uRoomGame.playInRoomState;
            return userData;
        }

        private bool CheckGameIsStarted() {
            if (this.state != (int)RoomState.Waiting) {
                return false;
            }

            // 条件1: 准备好的玩家的人数 >= 最少游戏开局的人数;
            // 条件2: 所有玩家都准备好了;

            int readyNum = 0;
            for (int i = 0; i < this.seats.Length; i++) {
                if (this.seats[i] == -1) {
                    continue;
                }

                GM_PlayerEntity e = GM_EntityMgr.Instance.Get(this.seats[i]);
                if (e == null) {
                    this.logger.Error($"Seat Sitdown a invalid PlayerEntity: {this.seats[i]}");
                    continue;
                }

                if (e.uRoomGame.playInRoomState != (int)PlayerInRoomState.Ready) {
                    return false; 
                }

                readyNum ++;
            }

            if (readyNum < MIN_PLAYER_NUM) {
                return false;
            }

            return true;
        }

        protected abstract int OnPlayerStartBuffInRoom(GM_PlayerEntity e, ReqStartBuff req);

        public int DoPlayerStartBuff(GM_PlayerEntity e, long playerId, ReqStartBuff req) {
            int status = this.OnPlayerStartBuffInRoom(e, req);
            if (status != (int)Respones.OK) {
                return status;
            }

            
            // 广播给房间里面所有的人，它释放了一个技能;
            ResStartBuff res = new ResStartBuff();
            res.buffId = req.buffId;
            res.seatOrWorldId = e.uRoomGame.seatId;
            this.BroadcastInView(res);

            return (int)Respones.OK;
        }

        protected abstract int OnPlayerStartSkillInRoom(GM_PlayerEntity e, ReqStartSkill req);

        public int DoPlayerStartSkill(GM_PlayerEntity e, long playerId, ReqStartSkill req) {
            int status = this.OnPlayerStartSkillInRoom(e, req);
            if (status != (int)Respones.OK) {
                return status;
            }

            // 广播给房间里面所有的人，它释放了一个技能;
            ResStartSkill res = new ResStartSkill();
            res.skillId = req.skillId;
            res.seatOrWorldId = e.uRoomGame.seatId;
            this.BroadcastInView(res);

            return (int)Respones.OK;
        }

        public int OnPlayerIsReady(GM_PlayerEntity e, long playerId) {
            if (this.state != (int)RoomState.Waiting ||
                e.uRoomGame.seatId == -1 ||
                e.uRoomGame.playInRoomState != (int)PlayerInRoomState.InView) {
                return (int)Respones.InvalidOpt;
            }

            e.uRoomGame.playInRoomState = (int)PlayerInRoomState.Ready;

            this.isCheckGameStarted = true;

            
            ResPlayerIsReady res = new ResPlayerIsReady();
            res.status = (int)Respones.OK;
            res.seatId = e.uRoomGame.seatId;

            this.BroadcastInView(res);

            return (int)Respones.OK;
        }

        public void OnPlayerTalkMsg(long playerId, ReqTalkMsg req) {
            if (req.talkType == 0) // 文字
            {
                if (req.msgBodyOrAudioUrl.Length <= 0 /*有非法的字符,不能说的*/)
                {
                    return;
                }
            }
            else if (req.talkType == 1) { // 语音
                /*if(checkAudioUrl(req.msgBodyOrAudioUrl))  不存在 return*/
                return;
            }

            GM_PlayerEntity e = GM_EntityMgr.Instance.Get(playerId);

            ResTalkMsg talkData = new ResTalkMsg();
            talkData.roomInViewId = e.uRoomGame.inViewId;
            talkData.talkType = req.talkType;
            talkData.msgBodyOrAudioUrl = req.msgBodyOrAudioUrl;
            

            this.BroadcastInView(talkData);
        }

        protected  ResEnterRoom GetEnerRoomData(GM_PlayerEntity e) {
            ResEnterRoom res = new ResEnterRoom();
            res.roomId = this.roomId;
            res.roomState = this.state;
            res.roomInViewId = e.uRoomGame.inViewId;

            return res;
        }
        public virtual void PlayerEnterToRoom(GM_PlayerEntity e, long playerId) {
            // GM_PlayerEntity e = GM_EntityMgr.Instance.Get(playerId);
            e.uRoomGame.roomId = this.roomId;
            e.uSkillAndBuff.worldOrRoom = this;

            e.uRoomGame.logicServerId = e.uPlayer.s.logicServerId;

            for (int i = 0; i < this.inViews.Length; i++) {
                if (this.inViews[i] == -1) {
                    this.inViews[i] = playerId;
                    e.uRoomGame.playInRoomState = (int)PlayerInRoomState.InView;
                    e.uRoomGame.inViewId = i;
                    break;
                }
            }

            // 返回给客户端，已经分配到一个房间，
            ResEnterRoom res = GetEnerRoomData(e);
            
            this.SendMsg(e, res);

            // 讲其他座位上的玩家的信息，发送给对应的客户端;
            for (int i = 0; i < this.seats.Length; i++) {
                if (this.seats[i] == -1) {
                    continue;
                }

                GM_PlayerEntity otherPlayer = GM_EntityMgr.Instance.Get(this.seats[i]);
                if (otherPlayer == null) {
                    continue;
                }

                
                // 把座位上每个玩家的最新状态发送给刚进入房间客户端;
                this.SendMsg(e, this.GetUserArrivedSeatData(otherPlayer, i));
            }

            // 如果你是自动模式，就可以直接在这里调用Sitdown,否则就主动让玩家来通过消息来调用Sitdown

        }

        private int FindEmptySeat() {
            for (int i = 0; i < this.seats.Length; i++) {
                if (this.seats[i] == -1) {
                    return i;
                }
            }

            return -1;
        }

        public int OnPlayerSitdown(GM_PlayerEntity e, long playerId, int seatId = -1) {

            if (this.state != (int)RoomState.Waiting || e.uRoomGame.seatId != -1) {
                return (int)Respones.InvalidOpt;
            }

           
            /*GM_PlayerEntity e = GM_EntityMgr.Instance.Get(playerId);
            if (e == null)
            {
                return (int)Respones.InvalidOpt;
            }*/

            // 自动找一个座位，一种是主动下这个座位;
            if (seatId == -1) {
                seatId = this.FindEmptySeat();

            }

            if (seatId < 0 || seatId >= this.seats.Length || this.seats[seatId] != -1) {
                return (int)Respones.InvalidParams;
            }

            this.seats[seatId] = playerId;
            e.uRoomGame.seatId = seatId;


            // 发送当前的玩家的状态给其他房间内所有玩家，让其他玩家看到有人坐下了;
            // 其它人就能看到这个玩家进来了;
            ResUserArrivedSeat res = this.GetUserArrivedSeatData(e, seatId);
            if (res != null) {
                this.BroadcastInView(res, playerId);
            }

            return (int)Respones.OK;
        }

        protected abstract int OnPlayerOperateInRoom(GM_PlayerEntity e, int seatId, ReqPlayerOpt req);

        public ResPlayerOpt DoPlayerOptInRoom(GM_PlayerEntity e, long playerId, ReqPlayerOpt req) {
            ResPlayerOpt res = new ResPlayerOpt();
            if (this.state != (int)RoomState.Started) {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            int seatId = e.uRoomGame.seatId;
            if (seatId == -1 /*|| 没有轮到你了*/) {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            // 给他接入具体的处理逻辑;
            int status = this.OnPlayerOperateInRoom(e, seatId, req);
            if (status == (int)Respones.FrameSyncCached) {
                return null;
            }
            else if (status != (int)Respones.OK) {
                res.status = status;
                return res;
            }
            // end 

            res.status = (int)Respones.OK;
            res.optType = req.optType;
            res.seatId = seatId;
            res.v1 = req.v1;
            res.v2 = req.v2;
            res.v3 = req.v3;

            this.BroadcastInView(res);

           
            return null;
        }

        public int OnPlayerStandUp(long playerId, bool forceQuit = false) {
            if (!forceQuit && (this.state == (int)RoomState.Ready || this.state == (int)RoomState.Started))
            {
                return (int)Respones.InvalidOpt;
            }

            GM_PlayerEntity e = GM_EntityMgr.Instance.Get(playerId);
            if (e == null || e.uRoomGame.seatId == -1) {
                return (int)Respones.InvalidOpt;
            }

            this.seats[e.uRoomGame.seatId] = -1;
            
            e.uRoomGame.playInRoomState = (int)PlayerInRoomState.InView;

            ResUserExitSeat res = new ResUserExitSeat();
            res.seatId = e.uRoomGame.seatId;
            e.uRoomGame.seatId = -1;


            this.BroadcastInView(res, playerId);
            return (int)Respones.OK;
        }

        public int PlayerReconnectToRoom(GM_PlayerEntity e, long playerId) {
            e.uPlayer.s.logicServerId = e.uRoomGame.logicServerId;
            e.uRoomGame.reconnectState = 2; // 重连进来了;
            this.hasReconnPlayer = true;
            return (int)Respones.OK;
        }

        public virtual int PlayerExitFromRoom(GM_PlayerEntity e, long playerId, int reason) {
            

            // GM_PlayerEntity e = GM_EntityMgr.Instance.Get(playerId);
            if (e.uRoomGame.playInRoomState == (int)PlayerInRoomState.Playing) {
                if (reason != (int)QuitReason.ForceQuit) {
                    if (reason == (int)QuitReason.DisconnectQuit) {
                        e.uRoomGame.reconnectState = 1; // 我们断线了，等待重连
                    }
                    return (int)Respones.UserIsPlaying;
                }

                // 对于逃跑玩家而言，还是回先收到,强退状态,不会收到逃跑消息以及其它消息;
                this.CheckOutRoundGameWithEscape(e, playerId);
            }

            if (e.uRoomGame.seatId != -1) {
                this.OnPlayerStandUp(playerId, reason == (int)QuitReason.ForceQuit);
            }

            for (int i = 0; i < this.inViews.Length; i++) {
                if (this.inViews[i] == playerId) {
                    this.inViews[i] = -1;
                    e.uRoomGame.inViewId = -1;
                    break;
                }
            }

            e.uRoomGame.reconnectState = -1;
            e.uRoomGame.roomId = -1;
            e.uSkillAndBuff.worldOrRoom = null;
            e.uRoomGame.playInRoomState = (int)PlayerInRoomState.Invalid;
            return (int)Respones.OK;
        }

        public virtual void Init(int stype, int zid, BaseLogicServer logicServer = null) {
            this.stype = stype;
            this.zid = zid;

            this.roomId = RoomMgr.autoId++ ;

            this.state = (int)RoomState.Waiting;
            this.isCheckGameStarted = false;
            this.seats = new long[MAX_PLAYER_NUM];
            this.inViews = new long[INVIEW_NUM];

            for (int i = 0; i < MAX_PLAYER_NUM; i++) {
                this.seats[i] = -1;
            }

            for (int i = 0; i < INVIEW_NUM; i++) {
                this.inViews[i] = -1;
            }

            this.curTime = 0.0f;
            this.roundTime = 0.0f;
            this.isCheckCaled = false;
            this.hasReconnPlayer = false;

            this.timerTaskMgr = logicServer.TimerTaskMgr;

            /*
            // 测试定时器
            this.timerTaskMgr.ScheduleOnce((object param) => {
                this.logger.Info("Timer Task Test: ########");
            }, 5.0f);
            // end
            */
        }

        public virtual void Reset() { // 下一局游戏开始的重置
            this.curTime = 0.0f;
            this.roundTime = 0.0f;
            this.state = (int)RoomState.Waiting;
            this.hasReconnPlayer = false;

            // 清理一些断线的玩家
            for (int i = 0; i < this.seats.Length; i++) {
                if (this.seats[i] != -1) {
                    GM_PlayerEntity e = GM_EntityMgr.Instance.Get(this.seats[i]);
                    if (e.uPlayer.s == null || e.uRoomGame.reconnectState != -1) { // 踢掉断线的玩家出房间
                        this.PlayerExitFromRoom(e, this.seats[i], (int)QuitReason.DisconnectQuit);
                        continue;
                    }

                    e.uRoomGame.playInRoomState = (int)PlayerInRoomState.InView;
                }
            }
        }

        private void DoRoundReady() {
            this.state = (int)RoomState.Ready;

            for (int i = 0; i < this.seats.Length; i++)
            {
                if (this.seats[i] != -1)
                {
                    GM_PlayerEntity e = GM_EntityMgr.Instance.Get(this.seats[i]);
                    e.uRoomGame.playInRoomState = (int)PlayerInRoomState.Playing;
                }
            }

            ResRoundReady res = new ResRoundReady();
            // 生成一些游戏数据
            res.roundTime = this.ROUND_TIME;
            // end
            this.BroadcastInView(res);

            this.curTime = 0.0f;
        }

        private void OnWatingStateUpdate(float dt) {
            if (this.isCheckGameStarted)
            {
                this.isCheckGameStarted = false;
                bool ret = this.CheckGameIsStarted();
                if (ret)
                {
                    this.DoRoundReady();
                }
            }
        }

        private void OnReadyStateUpdate(float dt) {
            this.curTime += dt;
            if (this.curTime >= READY_TIME) {
                this.state = (int)RoomState.Started;
                this.roundTime = 0;
                this.curTime = 0;

                ResRoundStarted res = new ResRoundStarted();
                this.BroadcastInView(res);
            }
        }

        

        protected void RoundGameEnterCheckout() {
            
            this.state = (int)RoomState.CheckOut;
            this.curTime = 0.0f;

            for (int i = 0; i < this.seats.Length; i++) {
                if (this.seats[i] == -1) {
                    continue;
                }

                GM_PlayerEntity e = GM_EntityMgr.Instance.Get(this.seats[i]);
                if (e == null) {
                    this.logger.Error($"Seat Sitdown a invalid PlayerEntity: {this.seats[i]}");
                    continue;
                }

                e.uRoomGame.playInRoomState = (int)PlayerInRoomState.CheckOut;
            }

            this.isCheckCaled = true;
            
        }

        protected abstract void CheckoutRoundResult();

        protected abstract void CheckOutRoundGameWithEscape(GM_PlayerEntity e, long escapePlayer);

        private void OnCheckOutUpdate(float dt) {
            if (this.isCheckCaled) {
                this.CheckoutRoundResult();
                this.isCheckCaled = false;
            }

            this.curTime += dt;
            if (this.curTime >= CHECKOUT_TIME) {
                this.curTime = 0;
                this.state = (int)RoomState.RoundClear;

                this.OnRoundClear();
            }
        }

        private void OnRoundClear() {
            this.state = (int)RoomState.Invalid;
            

            this.Reset();

            // 发送一个清理事件的消息给客户端
            ResRoundClear res = new ResRoundClear();
            res.reserve = -1;
            this.BroadcastInView(res);
        }

        protected abstract void SendReconnDataToPlayers();

        protected abstract void OnRoundGameUpdate(float dt);

        public void OnUpdate(float dt) {
            // 处理断线重连的相关检查,给重连进来的客户端发送房间数据;
            if (this.hasReconnPlayer) {
                this.SendReconnDataToPlayers();
                this.hasReconnPlayer = false;
            }
            // end

            if (this.state == (int)RoomState.Waiting) {
                this.OnWatingStateUpdate(dt);
            }
            else if (this.state == (int)RoomState.Ready) {
                this.OnReadyStateUpdate(dt);
            }
            else if (this.state == (int)RoomState.Started) {
                this.OnRoundGameUpdate(dt);
            }
            else if (this.state == (int)RoomState.CheckOut) {
                this.OnCheckOutUpdate(dt);
            }
        }
    }

}

