using Framework.Core.Serializer;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using RVO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    public class PointToPointNavSystem
    {
        public static void StartDirNavAction(AOIMgr aoi, BaseEntity e, int dirx, int diry)
        {
            if (dirx == 0 && diry == 0) { 
                EntityWrapper.SetEntityStatus(e, (int)CharactorStatus.Idle, aoi);
                return;
            }

            e.uNav.vx = (((float)(dirx)) / (1 << 16)) * e.uProps.speed;
            e.uNav.vz = (((float)(diry)) / (1 << 16)) * e.uProps.speed;
            e.uTransform.eulerAngles.y = (MathF.Atan2(e.uNav.vz, e.uNav.vx) * 180) / MathF.PI;

            e.uNav.totalTime = float.MaxValue; // 移动的时候,向前预测10个帧;
            e.uNav.passedTime = 0;
            e.uStatus.status = (int)CharactorStatus.Run;
        }

        public static void StartDstNavAction(AOIMgr aoi, BaseEntity e, float x, float y, float z)
        {
            Vector3 dst = new Vector3(x, y, z);
            Vector3 dir;
            dir.x = dst.x - e.uTransform.pos.x;
            dir.y = dst.y - e.uTransform.pos.y;
            dir.z = dst.z - e.uTransform.pos.z;

            e.uTransform.eulerAngles.y = (MathF.Atan2(dir.z, dir.x) * 180) / MathF.PI;

            float len = Vector3.Distance(ref e.uTransform.pos, ref dst);
            e.uNav.totalTime = len / e.uProps.speed;

            e.uNav.vx = (dir.x / len) * e.uProps.speed;
            e.uNav.vz = (dir.z / len) * e.uProps.speed;

            e.uNav.passedTime = 0;

            e.uStatus.status = (int)CharactorStatus.Run;
        }

        public static void Update(AOIMgr aoi, BaseEntity e, float dt) {
            e.uNav.passedTime += dt;
            if (e.uNav.passedTime > e.uNav.totalTime) {
                dt -= (e.uNav.passedTime - e.uNav.totalTime);
            }

            e.uTransform.pos.x += (e.uNav.vx * dt);
            e.uTransform.pos.z += (e.uNav.vz * dt);

            if (e.uNav.passedTime >= e.uNav.totalTime) {
                EntityWrapper.SetEntityStatus(e, (int)CharactorStatus.Idle, aoi);
                // e.uStatus.status = (int)CharactorStatus.Idle;
            }
        }
    }
}
