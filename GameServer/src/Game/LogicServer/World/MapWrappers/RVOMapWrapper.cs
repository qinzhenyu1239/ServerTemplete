using Framework.Core.Utils;
using Game.Datas.Excels;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.LogicServer.RVOMapEditor;
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
    public class RVOMapWrapper : IMapWrapper
    {
        private List<Vector3[]> obsSet = null;

        private int mapId = -1;



        private LevelConfig mapData = null;

        private RVO.Simulator simulator = null;

        public RVOMapWrapper(RVO.Simulator simulator) { 
            this.simulator = simulator; 
        }

        public void LoadMapData(int mapId)
        {
            this.mapId = mapId;

            this.obsSet = null;

            
            // 根据mapId,加载我们的地图数据，配置好RVO的障碍物
            string path = "Configs/Jsons/MapDatas/" + this.mapId + ".json";
            string mapJsonText = File.ReadAllText(path);
            this.mapData = JsonMapper.ToObject<LevelConfig>(mapJsonText);
            // 关联好我们的地图障碍物上的数据;
            RvoNavSystem.RVOAddObstacles(this.simulator, this.mapData.rvoObstacles);
            this.obsSet = this.mapData.rvoObstacles;
            // end
        }

       

        public int MapHeight()
        {
            throw new NotImplementedException();
        }

        public int MapWidth()
        {
            throw new NotImplementedException();
        }

        public void OnNavLateUpdate(AOIMgr aoi, BaseEntity e, float dt)
        {
            RvoNavSystem.NavRoadLateUpdate(this.simulator, aoi, e, dt);
            // 测试,客户端不跑，服务端直接同步过去，可视化查看服务端上的行走的真实情况
            // ResSyncCharactorStatus res = EntityWrapper.GetSyncCharactorStatus(e);
            // aoi.BroadMsgToAOI(e, res);
        }

        public void OnNavUpdate(AOIMgr aoi, BaseEntity e, float dt)
        {
            RvoNavSystem.NavRoadUpdate(this.simulator, aoi, e, dt);
        }

        public void PlayerSpwanAtMap(BaseEntity e, int spawnIndex = -1)
        {
            if (this.mapData.roles.Length <= 0) {
                return;
            }

            spawnIndex = (spawnIndex == -1) ? 0 : (spawnIndex % this.mapData.roles.Length);
            RoleConfig roleConfig = this.mapData.roles[spawnIndex];

            CharactorConfig config = (CharactorConfig)ExcelUtils.GetConfigData<CharactorConfig>(roleConfig.typeId.ToString());
            // 在RVO世界里面，我们要创建一个Atent;
            e.uRvo.agentId = RvoNavSystem.CreateAgent(this.simulator, roleConfig.pos, config.raduis);
            
            e.uTransform.pos = roleConfig.pos;
            e.uTransform.eulerAngles = roleConfig.rot;
            // 地图编辑器中的角色的0度是 +z，而Tranform是数学的0度，所以要 - 90
            e.uTransform.eulerAngles.y = roleConfig.rot.y - 90;
        }

        public void WalkInDir(AOIMgr aoi, BaseEntity e, int dirx, int diry)
        {
            RvoNavSystem.StartRvoNavAction(this.simulator, aoi, e, dirx, diry);

        }

        public void WalkToDst(AOIMgr aoi, BaseEntity e, float x, float y, float z)
        {
            e.uStatus.status = (int)CharactorStatus.Run; // 不掉用EntityWrapper的接口，开始的时候不需要同步状态;
            RvoNavSystem.StartRvoNavAction(e, x, y, z);

        }
    }
}
