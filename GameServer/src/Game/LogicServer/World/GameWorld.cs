using Framework.Core.Serializer;
using Framework.Core.task;
using Game.Core.EntityMgr;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.Utils;
using LitJson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    enum PlayerInWorldState
    {
        Invalid = -1,
        InView = 0, // 玩家正在旁观
        Playing, // 玩家正在游戏;
    }

    public class GameWorld
    {
        int worldGenId = 1;

        // private List<long> worldPlayers = null;
        NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private TimerTaskMgr timer = null;
        private AOIMgr aoiMgr = null;
        private int zidOrMapId = -1;

        private IMapWrapper mapWrapper = null;

        private RVO.Simulator simulator = null;

        public void LoadAndCreateGameMap() {

            if (this.zidOrMapId == 20001)
            {
                this.mapWrapper = new RectMapWrapper();
                this.mapWrapper.LoadMapData(this.zidOrMapId);
                this.aoiMgr = new GlobalAOIMgr(); // 全局AOI
                this.aoiMgr.Init(this.mapWrapper);
            }
            else if (this.zidOrMapId == 20002) // Astar 2D 与副本
            {
                this.mapWrapper = new AStarMapWrapper();
                this.mapWrapper.LoadMapData(this.zidOrMapId);
                // 九宫格的AOI
                this.aoiMgr = new NineGridAOIMgr(); // 九宫格AOI
                this.aoiMgr.Init(this.mapWrapper, 200 * 3);
                // end
            }
            else if (this.zidOrMapId == 20003) { // RVO + 3d
                this.simulator = new RVO.Simulator();
                RvoNavSystem.InitRVO(this.simulator);

                this.mapWrapper = new RVOMapWrapper(this.simulator);
                this.mapWrapper.LoadMapData(this.zidOrMapId);
                this.aoiMgr = new GlobalAOIMgr(); // 全局AOI
                this.aoiMgr.Init(this.mapWrapper);

            }
        }

        public void Init(int zidOrMapId, TimerTaskMgr timer) {
            this.timer = timer;
            this.zidOrMapId = zidOrMapId;

            this.LoadAndCreateGameMap();
        }

        public int PlayerEnterToWorld(GM_PlayerEntity e, long playerId) {
            e.uWorldGame.logicServerId = e.uPlayer.s.logicServerId;
            e.uWorldGame.worldId = this.worldGenId;
            e.uSkillAndBuff.worldOrRoom = this.aoiMgr;
            e.uWorldGame.playInWorldState = (int)PlayerInWorldState.InView;
            this.worldGenId++;

            // 从配置文件里面读取相关属性
            e.uProps.speed = 5;
            if (this.zidOrMapId == 20002) {
                e.uProps.speed = 100; // 2D的速度
            }
            else if (this.zidOrMapId == 20003) {
                e.uProps.speed = 5;
            }

            // this.SpawnPlayerInMap(e, req.SpawnPoint); // 自动玩家出生
            // end

            return (int)Respones.OK;
        }

        public int PlayerExitToWorld(GM_PlayerEntity e, long playerId, int reason)
        {
            /*if (reason == (int)QuitReason.DisconnectQuit && e.uWorldGame.playInWorldState == (int)PlayerInWorldState.Playing) {
                e.uWorldGame.reconnectState = 1; // 我们断线了，等待重连
                return (int)Respones.UserIsPlaying;
            }*/

            this.aoiMgr.LeaveFromAOI(e);
            
            e.uWorldGame.playInWorldState = (int)PlayerInWorldState.Invalid;
            e.uSkillAndBuff.worldOrRoom = null;
            e.uWorldGame.worldId = -1;

            if (e.uRvo.agentId != -1) {
                RvoNavSystem.DestroyAgent(this.simulator, e.uRvo.agentId);
                e.uRvo.agentId = -1;
            }
            
            e.uWorldGame.reconnectState = -1;

            return (int)Respones.OK;
        }

        public void OnUpdate(float dt) {
            this.aoiMgr.OnUpdate(dt);

            if (this.simulator != null) {
                this.simulator.setTimeStep(dt);
                this.simulator.doStep();
            }
            
            this.aoiMgr.OnLateUpdate(dt);
        }

        private void OnPlayerEnterAOI(GM_PlayerEntity e) {
            this.aoiMgr.EnterToAOI(e);
        }

        private void SpawnPlayerInMap(GM_PlayerEntity e, int spawnPointIndex) {

            
            this.mapWrapper.PlayerSpwanAtMap(e, spawnPointIndex);
            
            e.uWorldGame.playInWorldState = (int)PlayerInWorldState.Playing;
            e.uStatus.status = (int)CharactorStatus.Idle;


            this.OnPlayerEnterAOI(e);
#if false
            this.timer.ScheduleOnce((object param) => { // 断线重连时，要考虑
                this.OnPlayerEnterAOI(e);
            }, 0.01f);
#endif
        }

        public void OnPlayerNavInDir(GM_PlayerEntity e, long playerId, ReqNavInDir req) {

            // 服务端，你要操作服务端上的游戏对象来做摇杆移动;
            this.mapWrapper.WalkInDir(this.aoiMgr, e, req.dirx, req.diry);
            // end

            ResNavInDir res = new ResNavInDir();
            res.worldId = e.uWorldGame.worldId;

            res.dirx = req.dirx;
            res.diry = req.diry;

            res.speed = e.uProps.speed; // 由这些Buff叠加计算出来的，所以这个地方你要加个速度;

            // 广播出来;
            this.aoiMgr.BroadMsgToAOI(e, res);
        }

        public void OnPlayerNavTo(GM_PlayerEntity e, long playerId, ReqNavToDst req) {
            
            this.mapWrapper.WalkToDst(this.aoiMgr, e, req.x, req.y, req.z);

            e.uAoi.xDst = req.x;
            e.uAoi.yDst = req.y;
            e.uAoi.zDst = req.z;

            ResNavToDst res = new ResNavToDst();
            res.worldId = e.uWorldGame.worldId;

            res.x = req.x;
            res.y = req.y;
            res.z = req.z;

            res.speed = e.uProps.speed;

            // 广播出来;
            this.aoiMgr.BroadMsgToAOI(e, res);
        }

        public ResPlayerSpawn OnPlayerSpawnAt(GM_PlayerEntity e, long playerId, ReqPlayerSpawn req) {
            ResPlayerSpawn res = new ResPlayerSpawn();
            res.status = (int)Respones.OK;
            res.worldId = e.uWorldGame.worldId;
            this.aoiMgr.SendMsg(e, res); // 考虑一下

            this.SpawnPlayerInMap(e, req.SpawnPoint);
            
            return null;
        }
    }
}
