using Game.Core.GM_Task;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Game.Datas.GMEntities
{
    public struct RGameComponent
    {

        public int zid; // 玩家所在的分区号;
        public int roomId; // 玩家所在的房间号
        public int inViewId; // 玩家所在旁观列表中的Id;
        public int seatId; // 玩家的所在座位号，没有桌下为-1,
        public int playInRoomState; // 玩家在房间中的状态
        public int logicServerId; // 玩家所在的逻辑服Id;
        public int reconnectState; // 玩家重连的状态;  -1, 没有断线重连, 1为玩家断线了，但没有连进来，2，玩家连进来了，但是还没有发送数据


        public static void Init(GM_PlayerEntity entity)
        {
            entity.uRoomGame.zid = -1;
            entity.uRoomGame.roomId = -1;
            entity.uRoomGame.playInRoomState = -1;
            entity.uRoomGame.seatId = -1;
            entity.uRoomGame.inViewId = -1;
            entity.uRoomGame.logicServerId = -1;
            entity.uRoomGame.reconnectState = -1;
        }

    }

}