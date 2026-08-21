using Game.Datas.GMEntities;
using Game.Datas.Messages;
using LitJson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    public class MapSpawnPoint
    {
        public float x; // 出生点的位置
        public float y; // 出生点的位置

        public int spawnId; // 当前我们对应的spawnId;
        public bool defaultSpawn; // 是否为默认的Spawn;
    }

    public class AStarMapWrapper : IMapWrapper
    {
        private int mapId;

        protected MapData mapData = null;

        private Dictionary<int, MapSpawnPoint> spawnPoints = null;

        private PathFindingAgent pathFindingAgent = null;
        private void InitSpawnPointInMap(JsonData item)
        {
            string itemType = (string)item["type"];

            if (itemType.Equals("spawnPoint"))
            { // 出生点,不是entity,所以这里到时候特殊处理一下
                MapSpawnPoint entity = new MapSpawnPoint();
                entity.spawnId = (int)((item["spawnId"].IsInt) ? (int)item["spawnId"] : (double)(item["spawnId"]));
                entity.x = (float)((item["x"].IsInt) ? (int)item["x"] : (double)(item["x"]));
                entity.y = (float)((item["y"].IsInt) ? (int)item["y"] : (double)(item["y"]));
                entity.defaultSpawn = (bool)item["defaultSpawn"];
                if (entity != null) {
                    this.spawnPoints.Add(entity.spawnId, entity);
                }
            }
        }

        private void InitMapSpwanPoint(JsonData mapItems)
        {
            this.spawnPoints = new Dictionary<int, MapSpawnPoint>();
            for (int i = 0; i < mapItems.Count; i++)
            {
                JsonData item = mapItems[i];
                this.InitSpawnPointInMap(item);
            }
        }

        public void LoadMapData(int mapId)
        {
            this.mapId = mapId;
            string path = "Configs/Jsons/MapDatas/" + this.mapId + ".json";
            string mapJsonText = File.ReadAllText(path);

            this.mapData = JsonMapper.ToObject<MapData>(mapJsonText);
            JsonData mapDataJson = JsonMapper.ToObject(mapJsonText);

            // 读取地图里面所有玩家的出生点，到我们的内存里面来;
            this.InitMapSpwanPoint(mapDataJson["mapItems"]);
            // end

            // 读取其它的;
            // end

            // 初始化寻路系统，把寻路关联地图数据;  [0, 0, 0, ....]
            this.pathFindingAgent = new PathFindingAgent(); 
            this.pathFindingAgent.init(this.mapData);
        }

        private Vector2 GetSpawnPointPos(int spawnId)
        {
            Vector2 pos;
            pos.x = pos.y = 0.0f;

            MapSpawnPoint first = null;

            if (this.spawnPoints.ContainsKey(spawnId))
            {
                pos.x = this.spawnPoints[spawnId].x;
                pos.y = this.spawnPoints[spawnId].y;
            }
            else
            {
                // 找默认的，如果默认的也没有，我就找第一个;
                foreach (var key in this.spawnPoints.Keys)
                {
                    if (this.spawnPoints[key] != null && first == null)
                    {
                        first = this.spawnPoints[key];
                    }

                    if (this.spawnPoints[key].defaultSpawn)
                    {
                        first = this.spawnPoints[key];
                        break;
                    }
                }
            }

            if (first != null)
            {
                pos.x = first.x;
                pos.y = first.y;
            }

            return pos;
        }

        public void PlayerSpwanAtMap(BaseEntity e, int spawnIndex = -1)
        {
            Vector2 pos = this.GetSpawnPointPos(spawnIndex);
            e.uTransform.pos = new Vector3(pos.x, 0, pos.y);
            e.uTransform.eulerAngles = new Vector3(0, 0, 0);
        }

        // 寻路,  [x, 0, z]
        public void WalkToDst(AOIMgr aoi, BaseEntity e, float x, float y, float z)
        {
            List<RoadNode> roadNodeArr = this.pathFindingAgent.seekPath2(e.uTransform.pos.x, e.uTransform.pos.z, x, z);
            if (roadNodeArr == null || roadNodeArr.Count < 2) {
                // e.uStatus.status = (int)CharactorStatus.Idle;
                return;
            }

            e.uStatus.status = (int)CharactorStatus.Run; // 不掉用EntityWrapper的接口，开始的时候不需要同步状态;


            // 初始化一些与导航相关的AStarNavSystem;
            AStarNavSystem.StartRoadNavAction(e, roadNodeArr);
        }

        public void OnNavUpdate(AOIMgr aoi, BaseEntity e, float dt)
        {
            AStarNavSystem.NavRoadUpdate(aoi, e, dt);

            // 测试,客户端不跑，服务端直接同步过去，可视化查看服务端上的行走的真实情况
            // ResSyncCharactorStatus res = EntityWrapper.GetSyncCharactorStatus(e);
            // aoi.BroadMsgToAOI(e, res);
        }

        public int MapWidth()
        {
            return this.mapData.mapWidth;
        }

        public int MapHeight()
        {
            return this.mapData.mapHeight;
        }

        public void OnNavLateUpdate(AOIMgr aoi, BaseEntity e, float dt)
        {
            
        }

        public void WalkInDir(AOIMgr aoi, BaseEntity e, int dirx, int diry)
        {
            throw new NotImplementedException();
        }
    }
}
