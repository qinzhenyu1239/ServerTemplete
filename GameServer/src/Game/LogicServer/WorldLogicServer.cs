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
    [LogicServerMeta((int)ServerType.OpenWithMapWorld)]
    public class WorldLogicServer : CommonLogicServer
    {
        GameWorld gameWorld = null;
        

        public override int DoReconnectToGameInLogicServer(long playerId)
        {
            return (int)Respones.OK;
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

            ZoneConfig zConfig = (ZoneConfig)ExcelUtils.GetConfigData<ZoneConfig>(zid.ToString());
            if (zConfig == null) {
                return (int)Respones.InvalidParams;
            }
            // end

            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null) {
                return (int)Respones.InvalidOpt;
            }
            
            // 根据配置文件，来检查玩家是否能进地图分区, condString;
            int status = ZoneRule.PlayerCanEnterZone(zConfig.condString, player);
            if (status != (int)Respones.OK) {
                return status;
            }
            // end 

            status = this.gameWorld.PlayerEnterToWorld(player, playerId);
            if (status != (int)Respones.OK) {
                return status;
            }

            player.uWorldGame.zid = zid;
            player.uWorldGame.playInWorldState = (int)PlayerInWorldState.InView;

            this.onlinePlayers++;
            return status;
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
            
            int status = this.gameWorld.PlayerExitToWorld(player, playerId, reason);
            if (status != (int)Respones.OK) {
                return status;
            }

            player.uWorldGame.zid = -1;
            this.onlinePlayers--;

            return status;
        }

        public override void OnStart() {
            base.OnStart();

            LogicServerInst config = this.instConfig as LogicServerInst;

            this.logger.Info($"\n逻辑服实例{this.instanceId}:{config.desic}创建出来了");
            this.gameWorld = new GameWorld();

            List<int> zones = GameUtils.ParseStringWithIntValues(config.zones);
            if (zones.Count > 1) {
                this.logger.Warn($"开放世界，每个逻辑服实例只能带一个地图!");
            }

            this.gameWorld.Init(zones[0], this.timerMgr);
        }

       

        public void OnUpdate(float dt) {
            this.gameWorld.OnUpdate(dt);
          
        }


        [RequestMapping]
        public object DoPlayerSpawnAtWorld(IdSession s, ReqPlayerSpawn req, long playerId) {
            ResPlayerSpawn res = null;
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uWorldGame.worldId == -1 || 
                player.uWorldGame.playInWorldState != (int)PlayerInWorldState.InView) {
                res = new ResPlayerSpawn();
                res.status = (int)Respones.InvalidOpt;
                return res;
            }


            res = this.gameWorld.OnPlayerSpawnAt(player, playerId, req);
            return res;
        }

        [RequestMapping]
        public object DoPlayerNavToDst(IdSession s, ReqNavToDst req, long playerId) {
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uWorldGame.worldId == -1 ||
                player.uWorldGame.playInWorldState != (int)PlayerInWorldState.Playing)
            {
                return null;
            }

            this.gameWorld.OnPlayerNavTo(player, playerId, req);

            return null;
        }

        [RequestMapping]
        public object DoPlayerNavInDir(IdSession s, ReqNavInDir req, long playerId) {
            
            GM_PlayerEntity player = GM_EntityMgr.Instance.Get(playerId);
            if (player == null || player.uWorldGame.worldId == -1 ||
                player.uWorldGame.playInWorldState != (int)PlayerInWorldState.Playing)
            {
                return null;
            }

            this.gameWorld.OnPlayerNavInDir(player, playerId, req);
            

            // 如果是成功，我们需要在AOI范围内广播摇杆，所以这里直接返回为null;
            return null;
        }
    }
}
