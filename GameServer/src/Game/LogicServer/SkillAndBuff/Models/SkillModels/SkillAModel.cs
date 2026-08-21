using Framework.Core.Utils;
using Game.Datas.Excels;
using Game.Datas.GMEntities;
using NLog;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
namespace Game.LogicServer {

    [SkillModel(1000000)]
    public class SkillAModel
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /*[SkillProcesser("MyCustom", 1)]
        public static void MyCostomProcesser_1000001(GM_PlayerEntity sender, int skillId, object udata)
        {
            Debug.Log("MyCostomProcesser_1000001!");
        }*/

        [SkillProcesser("Init", -1)] // default;
        public static void DefaultInitProcesser(GM_PlayerEntity sender, int skillId, object udata)
        {
            SkillAConfig config = (SkillAConfig)ExcelUtils.GetConfigData<SkillAConfig>(skillId.ToString());
            if (!config.SkillEffectName.Equals("default"))
            {
                // GM_EffectMgr.Instance.PlayerSkillEffectAt(config.SkillEffectName, sender.transform.parent, sender.transform.position);
            }
        }

        [SkillProcesser("Begin", -1)]
        public static void DefaultBeginProcesser(GM_PlayerEntity sender, int skillId, object udata)
        {
            // Debug.Log($"DefaultBeginProcesser Skill ID: {skillId}");
        }

        [SkillProcesser("Calc", -1)]
        public static void DefaultCalcProcesser(GM_PlayerEntity e, int skillId, object udata)
        {
            // logger.Info($"DefaultCalcProcesser Skill ID: {skillId}");
            SkillAConfig config = (SkillAConfig)ExcelUtils.GetConfigData<SkillAConfig>(skillId.ToString());
            FightCalcResult calcResult = new FightCalcResult();
            calcResult.attackR = config.AttackR;
            SkillWrapper.CalcBuffsWithProp(e, "AttackR", calcResult); // sender 如果带N个buff,每个buff都有attackR,那么就可以都累加;
            /*GM_PlayerEntity[] targets = SkillWrapper.FindEntitiesInArea(e, calcResult.attackR);
            if (targets == null || targets.Length <= 0) {
                return;
            }          
            
            int count = targets.Length;
            if (config.TargetMax > 0) {
                count = (count > config.TargetMax) ? config.TargetMax : count;
            }
            // 遍历所有的目标
            for (int i = 0; i < count; i++)
            {
                calcResult.defense = targets[i].uProps.defense;
                targets[i].uSkillAndBuff.buffTimeLine.CalcAllBuffsWithProp("Defense", calcResult);

                if (calcResult.attack > calcResult.defense) {
                    SkillWrapper.EntityLostHp(targets[i], (int)(calcResult.attack - calcResult.defense));
                }
            }
            */

            GM_PlayerEntity target = SkillWrapper.FindNearestInArea(e, calcResult.attackR);
            float attack = (float)e.uProps.attack;
            attack = attack * config.DamageRate + config.FixDamage; // A类技能的模板;

            calcResult.attack = attack;
            // 叠加发送这所有Buff的Attack技能，不只是一个;
            SkillWrapper.CalcBuffsWithProp(e, "Attack", calcResult);


            calcResult.defense = target.uProps.defense;
            SkillWrapper.CalcBuffsWithProp(target, "Defense", calcResult);
            SkillWrapper.EntityLostHp(target, (int)(calcResult.attack - calcResult.defense));
        }

        [SkillProcesser("End", -1)]
        public static void DefaultEndProcesser(GM_PlayerEntity sender, int skillId, object udata)
        {
            logger.Info($"DefaultEndProcesser Skill ID: {skillId}");
        }

        [SkillProcesser("TimeLine", -1)]
        public static ParseTimeLineRet DefaultTimeLineStr(int skillId)
        {
            ParseTimeLineRet ret = new ParseTimeLineRet();

            SkillAConfig config = (SkillAConfig)ExcelUtils.GetConfigData<SkillAConfig>(skillId.ToString());
            if (config == null)
            {
                return null;
            }

            ret.SkillDuration = config.SkillDuration;
            ret.timeLineStr = config.TimeLine;

            return ret;
        }
    }

}

