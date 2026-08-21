using Game.Core.Db;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Datas.GMEntities
{
    public struct NavComponent
    {
        public float totalTime; // 移动的总时间

        public float vx;
        public float vz;
        public float passedTime;

        public static void Init(GM_PlayerEntity e) {
            e.uNav.totalTime = 0.0f;
            e.uNav.vx = 0.0f; 
            e.uNav.vz = 0.0f;
        }
    }
}
