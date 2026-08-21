using Game.Core.GM_Task;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Game.Datas.GMEntities
{
    // 玩家的当前的状态 Idle, Run, Diead... ===>游戏设定有很大的关系;
    public enum CharactorStatus { 
        Invalid = -1,
        Idle = 0,
        Run,
        Dance,
        Attack,
        Died,
    }

    public struct StatusComponent
    {
        public int status;

        public static void Init(GM_PlayerEntity entity) {
            entity.uStatus.status = (int)CharactorStatus.Invalid;
        }
    }
}
