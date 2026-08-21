using Game.Datas.GMEntities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.LogicServer.RVOMapEditor
{

    [Serializable]
    public class RoleConfig
    {
        public int typeId;

        public Vector3 pos;
        public Vector3 rot;

        public FightConfig fConfig = null;
        // ...
    }
}




