using Framework.Core.Utils;
using Game.Core.EntityMgr;
using Game.Core.GM_Backpack;
using Game.Datas.Excels;
using Game.Datas.GMEntities;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.GM_Trading
{
    [RuleModule((int)RuleType.Trading, 1000000)]
    public class ExchangeTradingRules
    {
        [RuleProcesser("CanExchange", -1)]
        static int DefaultCanExchange(GM_PlayerEntity player, int productId)
        {
            ExchangeTrading config = (ExchangeTrading)ExcelUtils.GetConfigData<ExchangeTrading>(productId.ToString());
            if (config == null) {
                return (int)Respones.InvalidParams;
            }

            return (int)Respones.InvalidOpt;
        }


        [RuleProcesser("DoExchange", -1)]
        static bool DefaultDoExchange(GM_PlayerEntity player, int productId)
        {
            return false; // Legacy yuanbao exchange is disabled.
        }


    }
}
