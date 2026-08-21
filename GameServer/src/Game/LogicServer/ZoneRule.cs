using Game.Datas.GMEntities;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

/*
 * ulevel>=10|uchip>=100
 */
namespace Game.LogicServer
{
    public class ZoneRule
    {
        // 可以使用特定的反射来做;
        public static int ParseCondLE(GM_PlayerEntity e, string lhs, string rhs) {
            int status = (int)Respones.InvalidOpt;
            
            return status; 
        }

        public static int ParseCondEqual(GM_PlayerEntity e, string lhs, string rhs)
        {
            return (int)Respones.OK;
        }

        public static int ParseCondGE(GM_PlayerEntity e, string lhs, string rhs)
        {
            int status = (int)Respones.InvalidOpt;
            int value = 0;
            if (lhs.Equals("ulevel"))
            {
                value = int.Parse(rhs);
                if (e.uPlayer.playerInfo.level >= value)
                {
                    return (int)Respones.OK;
                }

                status = (int)Respones.LevelIsNotEnough;
                // status = (int)Respones.OK;
            }
            else if (lhs.Equals("uchip"))
            {
                value = int.Parse(rhs);
                if (e.uPlayer.playerInfo.ucoin >= value)
                {
                    return (int)Respones.OK;
                }

                status = (int)Respones.ChipIsNotEnough;
            }
            return status;
        }



        public static int PlayerCanEnterZone(string enterCondStr, GM_PlayerEntity e) {
            if (enterCondStr == null || enterCondStr.Length <= 0 ||
                enterCondStr.Equals("none") ||
                enterCondStr.Equals("null")) {
                return (int)Respones.OK;
            }
            // 
            string[] conds = enterCondStr.Split('|');
            if (conds.Length <= 0) {
                return (int)Respones.OK;
            }
            // end

            int status = (int)Respones.InvalidOpt;
            for (int i = 0; i < conds.Length; i++) {
                string cond = conds[i];
                int index = cond.IndexOf(">=");
                if (index > 0) {
                    status = ParseCondGE(e, cond.Substring(0, index), cond.Substring(index + 2));
                    if (status != (int)Respones.OK) {
                        return status;
                    }
                    continue;
                }

                index = cond.IndexOf("==");
                if (index > 0) {
                    status = ParseCondEqual(e, cond.Substring(0, index), cond.Substring(index + 2));
                    if (status != (int)Respones.OK) {
                        return status;
                    }
                    continue;
                }

                index = cond.IndexOf("<=");
                if (index > 0) {
                    status = ParseCondLE(e, cond.Substring(0, index), cond.Substring(index + 2));
                    if (status != (int)Respones.OK) {
                        return status;
                    }
                    continue;
                }
            }

            return (int)status;
        }
    }
}
