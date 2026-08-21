using Game.Datas.GMEntities;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.GM_Trading
{
    [RuleModule((int)RuleType.Trading, 2000000)]
    public class OrderTradingRules
    {
        [RuleProcesser("GenOrder", -1)]
        static long DefaultGenProductOrder(GM_PlayerEntity player, int productId, int payType)
        {
            return 0;
        }


        [RuleProcesser("OrderDelivery", -1)]
        static bool DefaultDoOrderDelivery(GM_PlayerEntity player, long orderId)
        {
            return false;
        }


    }
}
