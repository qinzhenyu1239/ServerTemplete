using Game.Core.Db;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Datas.GMEntities
{
    public struct RVOComponent
    {
        public int agentId;

        public Vector3 targetPos;

        public float dirx;
        public float diry;

        public int type; // -1, 0,表示是目的地导航， 1, 表示是摇杆方向导航;

        public static void Init(GM_PlayerEntity e) {
            e.uRvo.agentId = -1;
            e.uRvo.diry = 0;
            e.uRvo.dirx = 0;
            e.uRvo.type = -1;
        }
    }
}
