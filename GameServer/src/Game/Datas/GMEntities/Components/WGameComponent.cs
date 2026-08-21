using Game.Core.GM_Task;
using Game.LogicServer;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Game.Datas.GMEntities
{
    public struct WGameComponent
    {

        public int zid; // 玩家所在的分区号;
        public int worldId; // 玩家所在世界的索引;
        public int playInWorldState; // 玩家在世界中的状态(旁观，Playing)
        public int logicServerId; // 玩家所在的逻辑服Id;
        public int reconnectState; // 玩家重连的状态;  -1, 没有断线重连, 1为玩家断线了，但没有连进来，2，玩家连进来了，但是还没有发送数据


        public static void Init(GM_PlayerEntity entity)
        {
            entity.uWorldGame.zid = -1;
            entity.uWorldGame.worldId = -1;
            entity.uWorldGame.logicServerId = -1;
            entity.uWorldGame.reconnectState = -1;
            entity.uWorldGame.playInWorldState = (int)PlayerInWorldState.Invalid;
        }

    }

}