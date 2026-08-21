using Game.Datas.GMEntities;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.GM_Backpack
{
    [RuleModule((int)RuleType.Backpack, 10000)]
    public class MedicinerRules
    {
        [RuleProcesser("SellOut", -1)]
        static bool DefaultGoodsSellOut(GM_PlayerEntity player, PackItem item, object param) {
            return false;
        }

        [RuleProcesser("SellOut", 10004)] // 10004
        static bool BigBlueGoodsSellOut(GM_PlayerEntity player, PackItem item, object param)
        {
            return false;
        }

        [RuleProcesser("BuyIn", -1)]
        static bool DefaultGoodsBuyIn(GM_PlayerEntity player, PackItem item, object param)
        {
            return false;
        }

        [RuleProcesser("PutOn", -1)]
        static bool DefaultGoodsPutOn(GM_PlayerEntity player, PackItem item, object param)
        {
            return false;
        }
    }
}
