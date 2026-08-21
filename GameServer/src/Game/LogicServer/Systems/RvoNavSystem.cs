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
    public class RvoNavSystem
    {
        public static void RVOAddObstacles(RVO.Simulator s, List<Vector3[]> obsSet)
        {
            for (int i = 0; i < obsSet.Count; i++)
            {
                List<RVO.Vector2> obsData = new List<RVO.Vector2>();
                for (int j = 0; j < obsSet[i].Length; j++)
                {
                    obsData.Add(new RVO.Vector2(obsSet[i][j].x, obsSet[i][j].z));
                }

                s.addObstacle(obsData);
            }

            s.processObstacles();
        }

        public static int CreateAgent(RVO.Simulator s, Vector3 pos, float r)
        {
            int agentId = s.addAgent(new RVO.Vector2(pos.x, pos.z));
            s.setAgentRadius(agentId, r);
            return agentId;
        }

        public static void SetAgentPrefVelocity(RVO.Simulator s, int agentId, Vector3 v)
        {
            s.setAgentPrefVelocity(agentId, new RVO.Vector2(v.x, v.z));
        }

        public static void SetAgentPosition(RVO.Simulator s, int agentId, Vector3 v)
        {
            s.setAgentPosition(agentId, new RVO.Vector2(v.x, v.z));
        }

        public static Vector3 GetAgentPosition(RVO.Simulator s, int agentId)
        {
            RVO.Vector2 pos = s.getAgentPosition(agentId);

            return new Vector3(pos.x(), 0, pos.y());
        }

        public static float GetAgentRaduis(RVO.Simulator s, int agentId)
        {
            return s.getAgentRadius(agentId);
        }

        public static void DestroyAgent(RVO.Simulator s, int agentId)
        {
            s.delAgent(agentId);
        }

        public static void InitRVO(RVO.Simulator s) {
            s.setTimeStep(0.25f);
            s.setAgentDefaults(15.0f, 10, 5.0f, 5.0f, 2.0f, 2.0f, new RVO.Vector2(0.0f, 0.0f));
        }

        public static void ClearRVO(RVO.Simulator s)
        {
            s.Clear();

        }

        public static void StartRvoNavAction(BaseEntity e, float x, float y, float z) {
            e.uRvo.targetPos.x = x;
            e.uRvo.targetPos.y = y;
            e.uRvo.targetPos.z = z;

            e.uRvo.type = 0; // 目的地驱动;
            e.uStatus.status = (int)CharactorStatus.Run; // 不掉用EntityWrapper的接口，开始的时候不需要同步状态;
        }

        public static void StartRvoNavAction(Simulator s, AOIMgr aoi, BaseEntity e, int dirx, int diry)
        {
            if (dirx == 0 && diry == 0) { // 摇杆弹起
                e.uRvo.type = -1;
                SetAgentPrefVelocity(s, e.uRvo.agentId, new Vector3(0, 0, 0));
                EntityWrapper.SetEntityStatus(e, (int)CharactorStatus.Idle, aoi);
                return;
            }

            e.uStatus.status = (int)CharactorStatus.Run; // 不掉用EntityWrapper的接口，开始的时候不需要同步状态;
            e.uRvo.type = 1;

            e.uRvo.dirx = ((float)(dirx)) / (1 << 16);
            e.uRvo.diry = ((float)(diry)) / (1 << 16);

            float len = MathF.Sqrt(e.uRvo.dirx * e.uRvo.dirx + e.uRvo.diry * e.uRvo.diry);

            e.uRvo.dirx = e.uRvo.dirx / len;
            e.uRvo.diry = e.uRvo.diry / len;
        }

        public static void NavRoadLateUpdate(RVO.Simulator s, AOIMgr aoi, BaseEntity e, float dt) {
            // 同步Transform组件上的位置和旋转
            RVO.Vector2 pos = s.getAgentPosition(e.uRvo.agentId);
            RVO.Vector2 vel = s.getAgentVelocity(e.uRvo.agentId);
            e.uTransform.pos = new Vector3(pos.x(), 0, pos.y());

            if (Math.Abs(vel.x()) > 0.0005f && Math.Abs(vel.y()) > 0.0005f)
            {
                e.uTransform.eulerAngles.y = (MathF.Atan2(vel.y(), vel.x()) * 180) / MathF.PI;
            }
        }

        public static void NavRoadUpdate(RVO.Simulator s, AOIMgr aoi, BaseEntity e, float dt) {
            Vector3 v;

            if (e.uRvo.type == 1) { // 游戏摇杆控制
                
                v.x = e.uRvo.dirx * e.uProps.speed;
                v.z = e.uRvo.diry * e.uProps.speed;
                v.y = 0;
                SetAgentPrefVelocity(s, e.uRvo.agentId, v);
                return;
            }

            // 游戏导航控制
            Vector3 pos = GetAgentPosition(s, e.uRvo.agentId);
            Vector3 dir = Vector3.Sub(ref e.uRvo.targetPos, ref pos);
            float len = Vector3.Len(ref dir);

            // NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
            // logger.Info($"len {len}");
            if (len <= 0.3f) { // 这里是根据游戏效果来调整就可以了;
                
                SetAgentPosition(s, e.uRvo.agentId, e.uRvo.targetPos);
                SetAgentPrefVelocity(s, e.uRvo.agentId, new Vector3(0, 0, 0));
                EntityWrapper.SetEntityStatus(e, (int)CharactorStatus.Idle, aoi);
                return;
            }

            
            v.x = dir.x * e.uProps.speed / len;
            v.z = dir.z * e.uProps.speed / len;
            v.y = 0;
            SetAgentPrefVelocity(s, e.uRvo.agentId, v);
        }
    }
}
