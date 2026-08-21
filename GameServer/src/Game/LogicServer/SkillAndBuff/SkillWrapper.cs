using Framework.Core.Serializer;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    public class SkillWrapper
    {
        public static GM_PlayerEntity[] FindEntitiesInArea(GM_PlayerEntity e, float attacR) {
            if (e.uSkillAndBuff.worldOrRoom == null) {
                return null;
            }

            // 用反射来获取方法
            Type t = e.uSkillAndBuff.worldOrRoom.GetType();
            MethodInfo m = t.GetMethod("FindEntitiesInArea", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) {
                return null;
            }
            // end

            return (GM_PlayerEntity[])m.Invoke(e.uSkillAndBuff.worldOrRoom, new object[] { e, attacR });
        }

        public static GM_PlayerEntity FindNearestInArea(GM_PlayerEntity e, float attacR)
        {
            if (e.uSkillAndBuff.worldOrRoom == null) {
                return null;
            }

            // 用反射来获取方法
            Type t = e.uSkillAndBuff.worldOrRoom.GetType();
            MethodInfo m = t.GetMethod("FindNearestInArea", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) {
                return null;
            }
            // end

            return (GM_PlayerEntity) m.Invoke(e.uSkillAndBuff.worldOrRoom, new object[] { e, attacR });
            
        }

        public static void EntityLostHp(GM_PlayerEntity e, int lost)
        {
            // 
            if (e.uSkillAndBuff.worldOrRoom == null) {
                return;
            }

            // 用反射来获取方法
            Type t = e.uSkillAndBuff.worldOrRoom.GetType();
            MethodInfo m = t.GetMethod("OnPlayerEntityLostHp", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null)
            {
                return;
            }
            // end

            m.Invoke(e.uSkillAndBuff.worldOrRoom, new object[] { e, lost });

        }

        public static void CalcBuffsWithProp(GM_PlayerEntity e, string propName, FightCalcResult calcResult) {
            e.uSkillAndBuff.buffTimeLine.CalcAllBuffsWithProp(propName, calcResult);
        }

        public static void BroadMsgToAOI(GM_PlayerEntity e, Message msg, long ignorePlayerId = -1) {
            if (e.uSkillAndBuff.worldOrRoom == null) {
                return;
            }

            Type t = e.uSkillAndBuff.worldOrRoom.GetType();
            MethodInfo m = t.GetMethod("BroadMsgToAOI");
            if (m == null) {
                return;
            }

            m.Invoke(e.uSkillAndBuff.worldOrRoom, new object[] { e, msg, ignorePlayerId });
        }
    }
}
