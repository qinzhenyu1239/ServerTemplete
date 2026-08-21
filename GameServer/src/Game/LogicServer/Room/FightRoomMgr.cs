using Enyim.Caching.Memcached.Results.StatusCodes;
using Framework.Core.Serializer;
using Framework.Core.task;
using Game.Core.EntityMgr;
using Game.Core.GM_Backpack;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NLog.Time;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.LogicServer
{
    public class FightRoomMgr : RoomMgr
    {

        public override void Init(int stype, int zid, BaseLogicServer logicServer = null)
        {
            // 根据配置初始化玩家的数目等等参数;
            this.MAX_PLAYER_NUM = 2;
            this.INVIEW_NUM = 8;
            this.MIN_PLAYER_NUM = 2;
            this.ROUND_TIME = -1;
            // end
            base.Init(stype, zid, logicServer);

            
        }

        private void OnSkillAndBuffUpdate(float dt) {
            for (int i = 0; i < this.seats.Length; i++) {
                if (this.seats[i] == -1) {
                    continue;
                }

                GM_PlayerEntity e = GM_EntityMgr.Instance.Get(this.seats[i]);
                SkillAndBuffSystem.Update(this, e, dt);
            }
        }

        protected override void OnRoundGameUpdate(float dt)
        {
            this.OnSkillAndBuffUpdate(dt);
           
            // 房间战斗模式的游戏处理
            // end

            /*// 结算条件检查
            this.roundTime += dt;
            if (this.roundTime < ROUND_TIME)
            {
                return;
            }
            // end

            // 游戏结束，进入结算
            this.RoundGameEnterCheckout();*/
        }

        protected override void CheckoutRoundResult()
        {
            // 对游戏进行结算,测试代码，具体可以根据自己的游戏来定制;
            int winnerId = -1;

            for (int i = 0; i < this.seats.Length; i++) {
                if (this.seats[i] == -1) {
                    continue;
                }

                GM_PlayerEntity e = GM_EntityMgr.Instance.Get(this.seats[i]);
                if (e == null) {
                    continue;
                }

                if (e.uStatus.status != (int)CharactorStatus.Died) {
                    winnerId = e.uRoomGame.seatId;
                }
            }
            // end

            // 把游戏的结果和游戏的数据结算出来，发往客户端
            ResRoundCheckOut res = new ResRoundCheckOut();
            res.reserve = winnerId; // -1 为平局，
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

        protected override void SendReconnDataToPlayers()
        {
            throw new System.NotImplementedException();
        }

        protected override int OnPlayerOperateInRoom(GM_PlayerEntity e, int seatId, ReqPlayerOpt req)
        {
            return (int)Respones.OK;
        }

        public override int PlayerExitFromRoom(GM_PlayerEntity e, long playerId, int reason) {
            if (reason == (int)QuitReason.DisconnectQuit) {
                reason = (int)QuitReason.ForceQuit;
            }

            return base.PlayerExitFromRoom(e,playerId, reason);
        }

        public override void PlayerEnterToRoom(GM_PlayerEntity e, long playerId) {
            
            base.PlayerEnterToRoom(e, playerId);

            // 自动坐下;
            int status = base.OnPlayerSitdown(e, playerId);
            if (status == (int)Respones.OK) {
                ResSitdown res = new ResSitdown();
                res.status = status;
                res.seatId = e.uRoomGame.seatId;
                this.SendMsg(e, res);
            }
            // end

            // 模板测试代码，后期用配置封装
            e.uProps.hp = 500;
            e.uStatus.status = (int)CharactorStatus.Idle; 
            // end

            // 自动准备好
            base.OnPlayerIsReady(e, playerId);
            // end
        }

        protected override int OnPlayerStartSkillInRoom(GM_PlayerEntity e, ReqStartSkill req)
        {
            if (this.state != (int)RoomState.Started || 
                e.uStatus.status == (int)CharactorStatus.Invalid ||
                e.uStatus.status == (int)CharactorStatus.Died) {
                return (int)Respones.InvalidOpt;
            }

            if (!e.uSkillAndBuff.skillTimeLine.StartSkill(e, req.skillId, null)) {
                return (int)Respones.InvalidOpt;
            }

            return (int)Respones.OK;
            // return (int)Respones.InvalidOpt;
        }

        protected override int OnPlayerStartBuffInRoom(GM_PlayerEntity e, ReqStartBuff req)
        {
            if (this.state != (int)RoomState.Started)
            {
                return (int)Respones.InvalidOpt;
            }

            if (!e.uSkillAndBuff.buffTimeLine.StartBuff(req.buffId))
            {
                return (int)Respones.InvalidOpt;
            }

            return (int)Respones.OK;
        }

        // 暂时测试
        public GM_PlayerEntity[] FindEntitiesInArea(GM_PlayerEntity e, float attackR)
        {
            return null;
        }

        public GM_PlayerEntity FindNearestInArea(GM_PlayerEntity e, float attackR)
        {
            if (e.uRoomGame.seatId == 0) {
                return GM_EntityMgr.Instance.Get(this.seats[1]);
            }
            return GM_EntityMgr.Instance.Get(this.seats[0]);
        }

        public void BroadMsgToAOI(GM_PlayerEntity e, Message s, long ignorePlayerId = -1)
        {
            for(int i = 0; i < this.seats.Length; i ++)  {
                long playerId = this.seats[i];
                if (playerId == -1 || playerId == ignorePlayerId) {
                    continue;
                }

                this.SendMsg(playerId, s);
            }
        }

        public void OnPlayerEntityLostHp(GM_PlayerEntity e, int lost) {
            e.uProps.hp -= lost;

            ResLostHp res = new ResLostHp();
            res.lostHp = lost;
            res.seatOrWorldId = e.uRoomGame.seatId;
            this.BroadMsgToAOI(e, res);

            if (e.uProps.hp <= 0) {
                this.OnPlayerEntityDied(e);
            }
        }


        private void OnPlayerEntityDied(GM_PlayerEntity e) {
            e.uStatus.status = (int)CharactorStatus.Died;
            ResSyncCharactorStatus res = EntityWrapper.GetSyncCharactorStatus(e);
            res.worldId = e.uRoomGame.seatId; // 注意一下，房间模式的id为seatId;
            this.BroadMsgToAOI(e, res);

            // 游戏直接结束，进入结算
            this.RoundGameEnterCheckout();
            // end
        }
    }
}


