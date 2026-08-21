using Framework.Core.Utils;
using Game.Datas.Excels;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Game.LogicServer {
    [BuffModel(100000)]
    public class BuffAModel
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();   

        [BuffProcesser("BuffTime", -1)]
        public static void DefaultBuffTime(BuffNode node)
        {
            BuffAConfig config = (BuffAConfig) ExcelUtils.GetConfigData<BuffAConfig>(node.buffId.ToString());
            if (config == null)
            {
                return;
            }

            node.freezeTime = config.BuffFrezzeTime;
            node.durationTime = config.BuffTime;
        }

        [BuffProcesser("Defense", -1)]
        public static void DefaultCalcPropDefense(int buffId, FightCalcResult ret)
        {
            BuffAConfig config = (BuffAConfig)ExcelUtils.GetConfigData<BuffAConfig>(buffId.ToString());
            if (config == null)
            {
                return;
            }

            ret.defense = ret.defense * config.BuffDefense;
        }

        [BuffProcesser("Attack", -1)]
        public static void DefaultCalcPropAttack(int buffId, FightCalcResult ret)
        {
            BuffAConfig config = (BuffAConfig) ExcelUtils.GetConfigData<BuffAConfig>(buffId.ToString());
            if (config == null)
            {
                return;
            }

            ret.attack = ret.attack * config.BuffAttack;
        }

        [BuffProcesser("AttackR", -1)]
        public static void DefaultCalcPropAttackR(int buffId, FightCalcResult ret)
        {
            logger.Info("DefaultCalcPropAttackR Buff Cale !!!!!###");
        }
    }

}

