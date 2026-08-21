using Game.Datas.GMEntities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.LogicServer.RVOMapEditor
{
    public class LevelData : Attribute
    {
        public int levelId;

        public LevelData(int id)
        {
            this.levelId = id;
        }
    }

    [Serializable]
    public class LevelConfig
    {
        public MapConfig[] maps = null;
        public ObsConfig[] obses = null;
        public NpcConfig[] npcs = null;
        public EnmeyConfig[] enmeies = null;
        public RoleConfig[] roles = null;

        // rvo地图边界数据
        public List<Vector3[]> rvoObstacles = null;
        // ...
    }
}


