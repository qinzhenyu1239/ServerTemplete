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
    [LogicServerMeta((int)ServerType.IndepOrRoomWithMapWorld)]
    public class WorldIndepLogicServer : RoomLogicServer
    {
        [RequestMapping]
        public object DoPlayerSpawnAtWorld(IdSession s, ReqPlayerSpawn req, long playerId)
        {
            ResPlayerSpawn res = null;
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uWorldGame.worldId == -1 ||
                player.uWorldGame.playInWorldState != (int)PlayerInWorldState.InView)
            {
                res = new ResPlayerSpawn();
                res.status = (int)Respones.InvalidOpt;
                return res;
            }

            // 1: 获取玩家所在房间;
            WorldIndepRoomMgr worldRoom = (WorldIndepRoomMgr) this.GetRoomBy(player.uRoomGame.roomId);
            // 2: 由房间来获得我们的gameWorld对象;

            res = worldRoom.gameWorld.OnPlayerSpawnAt(player, playerId, req);
            return res;
        }

        [RequestMapping]
        public object DoPlayerNavToDst(IdSession s, ReqNavToDst req, long playerId)
        {
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uWorldGame.worldId == -1 ||
                player.uWorldGame.playInWorldState != (int)PlayerInWorldState.Playing)
            {
                return null;
            }
            // 1: 获取玩家所在房间;
            WorldIndepRoomMgr worldRoom = (WorldIndepRoomMgr)this.GetRoomBy(player.uRoomGame.roomId);
            // 2: 由房间来获得我们的gameWorld对象;
            worldRoom.gameWorld.OnPlayerNavTo(player, playerId, req);

            return null;
        }

        [RequestMapping]
        public object DoPlayerNavToDir(IdSession s, ReqNavInDir req, long playerId)
        {
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uWorldGame.worldId == -1 ||
                player.uWorldGame.playInWorldState != (int)PlayerInWorldState.Playing)
            {
                return null;
                
            }
            // 1: 获取玩家所在房间;
            WorldIndepRoomMgr worldRoom = (WorldIndepRoomMgr)this.GetRoomBy(player.uRoomGame.roomId);
            // 2: 由房间来获得我们的gameWorld对象;
            worldRoom.gameWorld.OnPlayerNavInDir(player, playerId, req);
            
            return null;
        }
    }
}
