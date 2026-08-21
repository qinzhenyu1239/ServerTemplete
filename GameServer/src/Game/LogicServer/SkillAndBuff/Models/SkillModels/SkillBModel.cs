using Framework.Core.Utils;
using Game.Datas.Excels;
using Game.Datas.GMEntities;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Game.LogicServer {
    [SkillModel(2000000)]
    public class SkillBModel
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();   
        [SkillProcesser("Init", -1)] // default;
        public static void DefaultInitProcesser(GM_PlayerEntity sender, int skillId, object udata)
        {
            logger.Info($"B  DefaultInitProcesser Skill ID: {skillId}");
        }

        [SkillProcesser("Begin", -1)]
        public static void DefaultBeginProcesser(GM_PlayerEntity sender, int skillId, object udata)
        {
            logger.Info($"B: DefaultBeginProcesser Skill ID: {skillId}");
        }

        [SkillProcesser("Calc", -1)]
        public static void DefaultCalcProcesser(GM_PlayerEntity sender, int skillId, object udata)
        {
            logger.Info($"B: DefaultCalcProcesser Skill ID: {skillId}");
        }

        [SkillProcesser("End", -1)]
        public static void DefaultEndProcesser(GM_PlayerEntity sender, int skillId, object udata)
        {
            logger.Info($"B: DefaultEndProcesser Skill ID: {skillId}");
        }

        [SkillProcesser("TimeLine", -1)]
        public static ParseTimeLineRet DefaultTimeLineStr(int skillId)
        {
            ParseTimeLineRet ret = new ParseTimeLineRet();

            SkillBConfig config = (SkillBConfig) ExcelUtils.GetConfigData<SkillBConfig>(skillId.ToString());
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

