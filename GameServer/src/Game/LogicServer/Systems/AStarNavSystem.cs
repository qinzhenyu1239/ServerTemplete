using Framework.Core.Serializer;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    public class AStarNavSystem
    {
        public static void StartRoadNavAction(BaseEntity e, List<RoadNode> roadArray)
        {
            ref AStarComponent nav = ref e.uAStarNav;
            // nav.isWalking = true;
            nav.walkTime = nav.passedTime = 0.0f;
            nav.roadNodeArr = roadArray;
            nav.vx = nav.vz = 0;
            nav.nextIndex = 0;// 一开始你已经在第一个点; // [start, ..., end]


        }

        private static void WalkToNext(BaseEntity e)
        {
            ref AStarComponent nav = ref e.uAStarNav;


            Vector2 startPos = new Vector2(e.uTransform.pos.x, e.uTransform.pos.z);
            Vector2 endPos = new Vector2(nav.roadNodeArr[nav.nextIndex].px, nav.roadNodeArr[nav.nextIndex].py);

            Vector2 dir = Vector2.Sub(ref endPos, ref startPos);
            float len = Vector2.Len(ref dir);
            nav.walkTime = len / e.uProps.speed; // 可能后续要叠加我们的buff, 结合当前的Buff系统，来计算玩家的最终移动速度;
            nav.passedTime = 0;

            nav.vx = dir.x * e.uProps.speed / len;
            nav.vz = dir.y * e.uProps.speed / len;

            // 同步一下我们角色的方向, 角度为"度";
            e.uTransform.eulerAngles.y = (MathF.Atan2(dir.y, dir.x) * 180) / MathF.PI;

        }

        public static void StopNavAction(AOIMgr aoi, BaseEntity e)
        {
            ref AStarComponent nav = ref e.uAStarNav;
            // nav.isWalking = false;
            nav.roadNodeArr = null;

            EntityWrapper.SetEntityStatus(e, (int)CharactorStatus.Idle, aoi);
        }

        public static void NavRoadUpdate(AOIMgr aoi, BaseEntity e, float dt)
        {
            ref AStarComponent nav = ref e.uAStarNav;
            /*if (nav.isWalking == false) {
                return;
            }*/

            if (nav.passedTime >= nav.walkTime) {
                nav.nextIndex++;
                if (nav.nextIndex >= nav.roadNodeArr.Count)
                { //  行走到了目的地
                    AStarNavSystem.StopNavAction(aoi, e);
                    return;
                }

                AStarNavSystem.WalkToNext(e);
            }

            nav.passedTime += dt;
            if (nav.passedTime > nav.walkTime) {
                dt -= (nav.passedTime - nav.walkTime);
            }

            e.uTransform.pos.x += (nav.vx * dt);
            e.uTransform.pos.z += (nav.vz * dt);


        }
    }
}
