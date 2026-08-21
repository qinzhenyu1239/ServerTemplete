using Game.Datas.GMEntities;
using Game.Utils;
using LitJson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer {
    public class RectMapWrapper : IMapWrapper
    {
        private int mapId = -1;

        private int width;

        private int height;


        public void LoadMapData(int mapId)
        {
            this.mapId = mapId;

            // 正式项目可以通过mapId,从地图配置里面读取的;
            // 没有正式项目的时候，我们先写死;
            this.width = 40;
            this.height = 40;
        }

        public int MapHeight()
        {
            return this.height;
        }

        public int MapWidth()
        {
            return this.width;
        }

        public void OnNavLateUpdate(AOIMgr aoi, BaseEntity e, float dt)
        {
            // throw new NotImplementedException();
        }

        public void OnNavUpdate(AOIMgr aoi, BaseEntity e, float dt)
        {
            PointToPointNavSystem.Update(aoi, e, dt);
        }

        public void PlayerSpwanAtMap(BaseEntity e, int spawnIndex = -1)
        {
            // 这里是以中心为原点，[-20, 20) 之间来随机,正式项目，根据需求来决定出生;
            e.uTransform.pos = new Vector3(GameUtils.Random(-this.width / 2, this.width / 2), 0, GameUtils.Random(-this.height / 2, this.height / 2));
            e.uTransform.eulerAngles = new Vector3(0, 0, 0);
        }

        public void WalkInDir(AOIMgr aoi, BaseEntity e, int dirx, int diry)
        {
            PointToPointNavSystem.StartDirNavAction(aoi, e, dirx, diry);
        }

        public void WalkToDst(AOIMgr aoi, BaseEntity e, float x, float y, float z)
        {
            PointToPointNavSystem.StartDstNavAction(aoi, e, x, y, z);
        }

    }


}


