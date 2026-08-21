using Enyim.Caching.Memcached.Protocol.Binary;
using Framework.Core.Net;
using Framework.Core.task;
using Framework.Core.Utils;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.Excels;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.Entries.Modules;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    [LogicServerMeta((int)ServerType.RoomWithRule)]
    public partial class RoomLogicServer : CommonLogicServer
    {
        // 分等级的等待列表;  zid ===> 新手场，高手场, ...， 熟人场
        protected Dictionary<int, List<long>> waitList = null;

        // 管理所有的游戏房间;  
        protected Dictionary<int, Dictionary<int, RoomMgr>> rooms = null;

        

        protected Dictionary<int, RoomMgr> roomIdMap = new Dictionary<int, RoomMgr>();


        

        protected RoomMgr GetRoomBy(int roomId)
        {
            if (this.roomIdMap.ContainsKey(roomId))
            {
                return this.roomIdMap[roomId];
            }

            return null;
        }

        protected RoomMgr GetOrCreateWaitingRoom(int zid)
        {
            Dictionary<int, RoomMgr> zidRooms = null;

            if (!this.rooms.ContainsKey(zid))
            {
                zidRooms = new Dictionary<int, RoomMgr>();
                this.rooms.Add(zid, zidRooms);
            }
            else {
                zidRooms = this.rooms[zid];
            }

            foreach (var room in zidRooms.Values)
            {
                if (room.state == (int)RoomState.Waiting && !room.IsFull())
                {
                    return room;
                }
            }

            // 创建一个房间
            RoomMgr r = RoomFactory.CreateRoom(this.stype, zid);
            r.Init(this.stype, zid, this);
            zidRooms.Add(r.roomId, r);// zidRooms.Add(zid, r);

            this.roomIdMap.Add(r.roomId, r);
            return r;
        }

        public override int EnterLogicServer(long playerId, int zid/*, int logicServerId*/)
        {
            if (!this.zoneSet.ContainsKey(zid)) {
                return (int)Respones.InvalidParams;
            }

            LogicServerInst config = this.instConfig as LogicServerInst;
            if (config != null) {
                if (this.onlinePlayers >= config.maxUser) {
                    return (int)Respones.LogicServerIsBusy;
                }
            }

            this.onlinePlayers ++;

            // 加入逻辑服管理列表
            ZoneConfig zConfig = (ZoneConfig)ExcelUtils.GetConfigData<ZoneConfig>(zid.ToString());
            if (zConfig == null) {
                return (int)Respones.InvalidParams;
            }
            // end

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null) {
                return (int)Respones.InvalidOpt;
            }
            if (player.uRoomGame.zid >= 0 || player.uRoomGame.roomId >= 0) {
                return (int)Respones.InvalidOpt;
            }

            // 根据配置文件，来检查玩家是否能进分区, condString;
            int status = ZoneRule.PlayerCanEnterZone(zConfig.condString, player);
            if (status != (int)Respones.OK) {
                return status;
            }
            // end 

            player.uRoomGame.zid = zid; // 表示你已经在等待列表or房间里面里面;
            player.uRoomGame.roomId = -1; // zid > 0 roomId = -1 等待列表, zid > 0, roomId >= 0 表示在房间里面;
            List<long> waitList = this.waitList[zid];
            waitList.Add(playerId);

            
            
            return (int)Respones.OK;
        }

        public override int ExitLogicServer(long playerId, int reason)
        {
            // 根据离开理由，判断玩家是否能离开
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null)
            {
                return (int)Respones.InvalidOpt;
            }
            // end 

            if (reason == (int)QuitReason.DisconnectQuit) { // 断线了
                player.uPlayer.s = null;
            }

            int status;

            if (player.uRoomGame.zid != -1 && player.uRoomGame.roomId != -1) {
                RoomMgr room = this.GetRoomBy(player.uRoomGame.roomId);
                if (room != null) {
                    status = room.PlayerExitFromRoom(player, playerId, reason);
                    if (status != (int)Respones.OK) {
                        return status;
                    }
                }
            }

            player.uRoomGame.zid = -1; // 表示你已经在等待列表or房间里面里面;
            player.uRoomGame.roomId = -1; // zid > 0 roomId = -1 等待列表, zid > 0, roomId >= 0 表示在房间里面;

            this.onlinePlayers --;
            return (int)Respones.OK;
        }

        public override void OnStart() {
            base.OnStart();

            LogicServerInst config = this.instConfig as LogicServerInst;
            this.logger.Info($"\n逻辑服实例{this.instanceId}:{config.desic}创建出来了");

            this.waitList = new Dictionary<int, List<long>>();
            this.rooms = new Dictionary<int, Dictionary<int, RoomMgr>>();
            
            foreach (var zid in this.zoneSet.Keys) {
                List<long> waitList = new List<long>();
                this.waitList.Add(zid, waitList);
            }
        }

        private void MatchWaitingPlayerToRoom() {
            // step1: 遍历所有的等待列表里面的所有玩家;
            foreach (var zid in this.waitList.Keys) {
                var zoneWaitList = this.waitList[zid];
                for (int i = 0; i < zoneWaitList.Count; i++) {
                    RoomMgr room = this.GetOrCreateWaitingRoom(zid);
                    if (room == null) {
                        this.logger.Error($"GetOrCreateRoom null: {zid}");
                        return;
                    }

                    long playerId = zoneWaitList[i];
                    GM_PlayerEntity e = GM_EntityMgr.Instance.Get(playerId);
                    if (e == null) {
                        continue;
                    }
                    room.PlayerEnterToRoom(e, playerId);
                }

                zoneWaitList.Clear();
            }
            // end


        }

        public void OnUpdate(float dt) {

            // 更新将我们的等待列表中的玩家，放入到房间中，如果没有房间，就创建;
            this.MatchWaitingPlayerToRoom();
            // end

            // 遍历所有的房间, 来迭代里面的游戏逻辑;
            foreach (var roomSet in this.rooms.Values) {
                foreach (var room in roomSet.Values) {
                    room.OnUpdate(dt);
                }
            }
            // end
        }

        [RequestMapping]
        public object DoPlayerStartSkill(IdSession s, ReqStartSkill req, long playerId) {
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uRoomGame.roomId == -1) {
                return null;
            }

            RoomMgr room = this.GetRoomBy(player.uRoomGame.roomId);
            if (room == null) {
                return null;
            }

            room.DoPlayerStartSkill(player, playerId, req);
            
            return null;
        }

        [RequestMapping]
        public object DoPlayerStartBuff(IdSession s, ReqStartBuff req, long playerId)
        {
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uRoomGame.roomId == -1)
            {
                return null;
            }

            RoomMgr room = this.GetRoomBy(player.uRoomGame.roomId);
            if (room == null) {
                return null;
            }

            room.DoPlayerStartBuff(player, playerId, req);

            return null;
        }

        [RequestMapping]
        public object DoPlayerIsReady(IdSession s, ReqPlayerIsReady req, long playerId)
        {
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uRoomGame.roomId == -1)
            {
                return (int)Respones.InvalidOpt;
            }

            RoomMgr room = this.GetRoomBy(player.uRoomGame.roomId);
            if (room == null)
            {
                return (int)Respones.InvalidOpt;
            }

            int status = room.OnPlayerIsReady(player, playerId);

            if (status != (int)Respones.OK) {
                ResPlayerIsReady res = new ResPlayerIsReady();
                res.status = status;
                res.seatId = -1;
                return res;
            }
            
            return null;
        }

        [RequestMapping]
        public object DoPlayerSendMsg(IdSession s, ReqTalkMsg req, long playerId) {
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uRoomGame.roomId == -1) {
                return (int)Respones.InvalidOpt;
            }

            RoomMgr room = this.GetRoomBy(player.uRoomGame.roomId);
            if (room == null) {
                return (int)Respones.InvalidOpt;
            }

            room.OnPlayerTalkMsg(playerId, req);

            return null;
        }

        [RequestMapping]
        public object DoReqSitdown(IdSession s, ReqSitdown req, long playerId)
        {
            ResSitdown res = new ResSitdown();
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uRoomGame.roomId == -1) {
                return (int)Respones.InvalidOpt;
            }
            // end 

            res.status = this.OnPlayerSitdown(player.uRoomGame.roomId, playerId, req.seatId);
            res.seatId = player.uRoomGame.seatId;

            return res;
        }


        public int OnPlayerSitdown(int roomId, long playerId, int seatId = -1)
        {
            RoomMgr room = this.GetRoomBy(roomId);
            if (room == null) {
                return (int)Respones.InvalidOpt;
            }

            GM_PlayerEntity e = GM_EntityMgr.Instance.Get(playerId);
            if (e == null)
            {
                return (int)Respones.InvalidOpt;
            }

            int status = room.OnPlayerSitdown(e, playerId, seatId);
            
            return status;
        }

        [RequestMapping]
        public object DoReqStandup(IdSession s, ReqStandup req, long playerId)
        {
            ResStandup res = new ResStandup();
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uRoomGame.roomId == -1)
            {
                res.status = (int)Respones.InvalidOpt;
                return res;
            }
            // end 

            res.status = this.OnPlayerStandup(player.uRoomGame.roomId, playerId);

            return res;
        }

        [RequestMapping]
        public object DoReqPlayerOpt(IdSession s, ReqPlayerOpt req, long playerId) {
            
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uRoomGame.roomId == -1) {
                ResPlayerOpt res = new ResPlayerOpt();
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            RoomMgr room = this.GetRoomBy(player.uRoomGame.roomId);
            if (room == null) {
                ResPlayerOpt res = new ResPlayerOpt();
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            return room.DoPlayerOptInRoom(player, playerId, req);
        }

        public int OnPlayerStandup(int roomId, long playerId)
        {
           
            RoomMgr room = this.GetRoomBy(roomId);
            if (room == null)
            {
                return (int)Respones.InvalidOpt;
            }

            return room.OnPlayerStandUp(playerId);
            
        }

        public override int DoReconnectToGameInLogicServer(long playerId)
        {
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uRoomGame.roomId == -1)
            {
                return (int)Respones.InvalidOpt;
            }
            RoomMgr room = this.GetRoomBy(player.uRoomGame.roomId);
            if (room == null)
            {
                return (int)Respones.InvalidOpt;
            }

            return room.PlayerReconnectToRoom(player, playerId);
        }
    }
}
