

using System.Runtime.CompilerServices;

/*
 * 通用的RoomMgr ===> 通用房间模式的通用逻辑;
 * 如果你有特别的游戏玩法，可以继承扩展自RoomMgr;  BossRoomMgr ===>RoomMgr;
 * stype: 哪种玩法===》某个分区的玩法; zid;
 */
namespace Game.LogicServer {
    public class RoomFactory {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static RoomMgr CreateRoom(int stype, int zid) {
            switch (stype) {
                case 1:
                    if (zid == 10001)
                    {
                        return new RoundRoomMgr();
                    }
                    else if (zid == 10002)
                    {
                        return new FightRoomMgr();
                    }
                    else if (zid == 10004) { // 帧同步的房间处理
                        return new FrameSyncRoomMgr();
                    }
                    return new RoundRoomMgr();
                    // break;
                case 2:
                    return new RoundRoomMgr();
                // break;
                case 3:
                    if (zid == 200020) { // 20002地图的副本
                        return new WorldIndepRoomMgr();
                    }
                    return new WorldIndepRoomMgr();
                // break;

            }

            logger.Error($"CreateRoom with unknow Type: {stype}");
            return null;
        }
    }
}

