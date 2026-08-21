using Game.Datas.GMEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    public interface IMapWrapper
    {
        public void LoadMapData(int mapId);

        public int MapWidth();

        public int MapHeight();

        public void PlayerSpwanAtMap(BaseEntity e, int spawnIndex = -1);

        public void WalkToDst(AOIMgr aoi, BaseEntity e, float x, float y, float z);

        public void WalkInDir(AOIMgr aoi, BaseEntity e, int dirx, int diry);

        public void OnNavUpdate(AOIMgr aoi, BaseEntity e, float dt);

        public void OnNavLateUpdate(AOIMgr aoi, BaseEntity e, float dt);

        // ....
    }
}
