using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.LogicServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game
{
    public class EntityWrapper
    {
        public static ResSyncCharactorStatus GetSyncCharactorStatus(BaseEntity e) {
            ResSyncCharactorStatus res = new ResSyncCharactorStatus();

            res.worldId = e.uWorldGame.worldId;
            res.transform = new CharactorTransform();
            res.transform.pos = new float[3];
            res.transform.pos[0] = e.uTransform.pos.x;
            res.transform.pos[1] = e.uTransform.pos.y;
            res.transform.pos[2] = e.uTransform.pos.z;

            res.transform.eulerAngles = new float[3];
            res.transform.eulerAngles[0] = e.uTransform.eulerAngles.x;
            res.transform.eulerAngles[1] = e.uTransform.eulerAngles.y;
            res.transform.eulerAngles[2] = e.uTransform.eulerAngles.z;

            res.statusData = new SyncStatusData();
            res.statusData.status = e.uStatus.status;

            return res;
        }

        public static void SetEntityStatus(BaseEntity e, int status, AOIMgr aoi) {
            if (e.uStatus.status == status) {
                return;
            }

            e.uStatus.status = status;

            // 同步Entity的状态给到AOI范围内的所有我们的客户端;
            ResSyncCharactorStatus res = GetSyncCharactorStatus(e);
            aoi.BroadMsgToAOI(e, res);
        }
    }
}
