using Game.Datas.GMEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    public class NavMeshMapWrapper : IMapWrapper
    {
        public void LoadMapData(int mapId)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public void OnNavUpdate(AOIMgr aoi, BaseEntity e, float dt)
        {
            throw new NotImplementedException();
        }

        public void PlayerSpwanAtMap(BaseEntity e, int spawnIndex = -1)
        {
            throw new NotImplementedException();
        }

        public void WalkInDir(AOIMgr aoi, BaseEntity e, int dirx, int diry)
        {
            throw new NotImplementedException();
        }

        public void WalkToDst(AOIMgr aoi, BaseEntity e, float x, float y, float z)
        {
            throw new NotImplementedException();
        }
    }
}
