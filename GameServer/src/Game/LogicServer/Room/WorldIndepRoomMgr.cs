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
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Game.LogicServer
{
    public class WorldIndepRoomMgr : RoomMgr
    {
        public GameWorld gameWorld = null;

        // 玩家在独立空间(房间)来在地图里面做相关的任务触发;
        public override void Init(int stype, int zid, BaseLogicServer logicServer = null) {
            // 根据配置初始化玩家的数目等等参数;
            this.MAX_PLAYER_NUM = 2;
            this.INVIEW_NUM = 8;
            this.MIN_PLAYER_NUM = 2;
            this.ROUND_TIME = -1;
            // end

            base.Init(stype, zid, logicServer);

            // 把地图副本对应的世界初始化;
            this.gameWorld = new GameWorld();

            // zid 地图副本的zid  ---> gameWord初始化的zid 地图的zid;
            // 地图副本的zid  =  gameWorld匹配的地图id * 10;
            this.gameWorld.Init(zid / 10, this.timerTaskMgr);
            // end
        }

        public override void PlayerEnterToRoom(GM_PlayerEntity e, long playerId) {
            base.PlayerEnterToRoom(e, playerId); // 玩家进入房间

            // 带一些地图副本的数据, 根据地图副本的任务，来初始化这个世界里面的内容;
            // end

            this.state = (int)RoomState.Started; // 副本开始了;

            this.gameWorld.PlayerEnterToWorld(e, playerId); // 玩家进入到游戏世界; 
        }

        public override int PlayerExitFromRoom(GM_PlayerEntity e, long playerId, int reason)
        {
            // 清理地图副本的数据, 根据地图副本的任务，来初始化这个世界里面的内容;
            // end

            // 离开房间之前，我们要先离开世界
            int status = this.gameWorld.PlayerExitToWorld(e, playerId, reason);
            // end 

            this.state = (int)RoomState.Waiting; // 下一次，这个房间可以继续派送回来;

            if (reason == (int)QuitReason.DisconnectQuit)
            {
                reason = (int)QuitReason.ForceQuit;
            }

            return base.PlayerExitFromRoom(e, playerId, reason);
        }

        protected override void CheckOutRoundGameWithEscape(GM_PlayerEntity e, long escapePlayer)
        {
            // throw new System.NotImplementedException();
        }

        protected override void CheckoutRoundResult()
        {
            throw new System.NotImplementedException();
        }

        protected override int OnPlayerOperateInRoom(GM_PlayerEntity e, int seatId, ReqPlayerOpt req)
        {
            throw new System.NotImplementedException();
        }

        protected override int OnPlayerStartBuffInRoom(GM_PlayerEntity e, ReqStartBuff req)
        {
            throw new System.NotImplementedException();
        }

        protected override int OnPlayerStartSkillInRoom(GM_PlayerEntity e, ReqStartSkill req)
        {
            throw new System.NotImplementedException();
        }

        protected override void OnRoundGameUpdate(float dt)
        {
            this.gameWorld.OnUpdate(dt);
        }

        protected override void SendReconnDataToPlayers()
        {
            throw new System.NotImplementedException();
        }
    }
}
