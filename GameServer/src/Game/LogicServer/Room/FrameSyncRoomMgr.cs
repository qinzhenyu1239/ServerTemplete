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
using System.Runtime.CompilerServices;

namespace Game.LogicServer
{
    public class FrameSyncRoomMgr : RoomMgr
    {
        private int frameId = -1;

        private List<ResFrameSyncData> historyFrameData = null;
        // 是否用来保存到文件，这个你自己决定; 
        private List<FrameOpt> nextFrameOpt = null;

        public override void Init(int stype, int zid, BaseLogicServer logicServer = null)
        {
            // 根据配置初始化玩家的数目;
            this.MAX_PLAYER_NUM = 2;
            this.MIN_PLAYER_NUM = 2;
            this.ROUND_TIME = 30 * 60; // 一局游戏的时长;
            this.READY_TIME = 3; // 准备3秒后，正式开始;
            this.frameId = -1;
            this.nextFrameOpt = new List<FrameOpt>();
            this.historyFrameData = new List<ResFrameSyncData>();
            // end

            base.Init(stype, zid, logicServer);
        }

        public override void Reset() {
            base.Reset();

            this.frameId = -1;
            this.nextFrameOpt.Clear();
        }
        protected override void OnRoundGameUpdate(float dt)
        {

            // 帧同步房间模式的游戏处理
            this.frameId ++;
            ResFrameSyncData res = new ResFrameSyncData(); // 优化建议：后续正式项目可以采用对象池的方式;
            res.frameId = this.frameId;
            res.playersOptSet = this.nextFrameOpt.ToArray();
            this.BroadcastInView(res);
            // end

            // 清理一下当前的所有操作;
            this.nextFrameOpt.Clear();
            // this.historyFrameData.Add(res);  // 这个历史操作的机制，你正式项目的时候，自己去写;
            // end

            // 结算条件检查
            this.roundTime += dt;
            if (this.roundTime < ROUND_TIME) {
                return;
            }
            // end

            // 游戏结束，进入结算
            this.RoundGameEnterCheckout();
        }

        protected override void CheckoutRoundResult()
        {
            // 对游戏进行结算
            // end

            // 把游戏的结果和游戏的数据结算出来，发往客户端
            ResRoundCheckOut res = new ResRoundCheckOut();
            this.BroadcastInView(res);
            // this.BroadcastInSeat(res);
        }

        protected override void CheckOutRoundGameWithEscape(GM_PlayerEntity e, long escapePlayer)
        {

            // 
            ResPlayerEscape res = new ResPlayerEscape();
            res.seatId = e.uRoomGame.seatId;
            this.BroadcastInView(res, escapePlayer);
            // end

            // 发送一个消息,玩家逃跑;
            this.RoundGameEnterCheckout();
            // 对逃跑的玩家进行惩罚
            // end


        }

        public override int PlayerExitFromRoom(GM_PlayerEntity e, long playerId, int reason)
        {
            
            if (reason == (int)QuitReason.DisconnectQuit || reason == (int)QuitReason.PlayerQuit)
            {
                reason = (int)QuitReason.ForceQuit;
            }

            return base.PlayerExitFromRoom(e, playerId, reason);
        }

        // 生成断线重连的发往客户端信息
        private ResReconnRoom GetRoomReconnData(GM_PlayerEntity e)
        {
            if (this.state != (int)(PlayerInRoomState.Playing) && this.state != (int)(PlayerInRoomState.Ready))
            {
                return null;
            }


            ResReconnRoom res = new ResReconnRoom();

            // 把当前的游戏状态都发送给对应的人;
            // step1: 发送在座的信息;
            List<ResUserArrivedSeat> seatsData = new List<ResUserArrivedSeat>();
            for (int i = 0; i < this.seats.Length; i++)
            {
                if (this.seats[i] == -1 || i == e.uRoomGame.seatId)
                {
                    continue;
                }

                GM_PlayerEntity otherPlayer = GM_EntityMgr.Instance.Get(this.seats[i]);
                ResUserArrivedSeat otherData = this.GetUserArrivedSeatData(otherPlayer, i);
                seatsData.Add(otherData);
            }
            // 
            // step2: 关联房间信息;
            res.room = this.GetEnerRoomData(e);
            // step3: 关联玩家自己信息
            res.selfSitdown = new ResSitdown();
            res.selfSitdown.status = (int)Respones.OK;
            res.selfSitdown.seatId = e.uRoomGame.seatId;

            res.seats = seatsData.ToArray();
            return res;
        }

        protected override void SendReconnDataToPlayers()
        {
            // 考虑一下，如果断线重连进来的时候，是这个情况，阁下如何应对;
            if (this.state != (int)RoomState.Ready && this.state != (int)RoomState.Started)
            {
                return;
            }

            for (int i = 0; i < this.seats.Length; i++)
            {
                if (this.seats[i] == -1)
                {
                    continue;
                }

                GM_PlayerEntity e = GM_EntityMgr.Instance.Get(this.seats[i]);
                if (e == null)
                {
                    continue;
                }


                if (e.uRoomGame.reconnectState == 2)
                {
                    e.uRoomGame.reconnectState = -1;
                    ResReconnRoom data = this.GetRoomReconnData(e);
                    if (data != null)
                    {
                        this.SendMsg(e, data);
                    }

                }
            }
        }

        protected override int OnPlayerOperateInRoom(GM_PlayerEntity e, int seatId, ReqPlayerOpt req)
        {
            // 检查我们的上报的帧id是不是正确的; 假设收到的是以前的frameId, 100,  50的frameId;
            int frameId = req.v3; // 帧同步，v3为客户端传过来的帧id;
            if (frameId != this.frameId + 1) { // 丢弃
                return (int)Respones.InvalidParams;
            }
            // end

            FrameOpt opt = new FrameOpt();
            opt.seatId = seatId;
            opt.optType = req.optType;
            opt.v1 = req.v1;
            opt.v2 = req.v2;

            this.nextFrameOpt.Add(opt);


            return (int)Respones.FrameSyncCached; // 帧同步，不需要马上把操作广播给客户端;
        }

        protected override int OnPlayerStartSkillInRoom(GM_PlayerEntity e, ReqStartSkill req)
        {
            return (int)Respones.InvalidOpt;
        }

        protected override int OnPlayerStartBuffInRoom(GM_PlayerEntity e, ReqStartBuff req)
        {
            return (int)Respones.InvalidOpt;
        }
    }
}


