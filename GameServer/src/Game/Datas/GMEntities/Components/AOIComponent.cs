using Game.Core.Db;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Datas.GMEntities
{
    public struct AOIComponent
    {
        public int blockX;
        public int blockZ;

        public float xDst;
        public float yDst;
        public float zDst;

        public static void Init(GM_PlayerEntity e)
        {
            e.uAoi.blockX = -1;
            e.uAoi.blockZ = -1;
            e.uAoi.xDst = -1;
            e.uAoi.zDst = -1;
        }
    }

}
