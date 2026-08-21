using Framework.Core.Utils;
using Game.Core.Caches;
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

            if (player.uPlayer.playerInfo.umoney < config.Price) {
                return (int)Respones.MoneyIsNotEnough;
            }

            return (int)Respones.OK;
        }


        [RuleProcesser("DoExchange", -1)]
        static bool DefaultDoExchange(GM_PlayerEntity player, int productId)
        {
            ExchangeTrading config = (ExchangeTrading)ExcelUtils.GetConfigData<ExchangeTrading>(productId.ToString());
            if (config == null) {
                return false;
            }
            Dictionary<string, int> products = GameUtils.ParseStringWithKeyIntValue(config.Product);
            if (products.Count <= 0) {
                return false;
            }

            player.uPlayer.playerInfo.umoney -= config.Price;
            PlayerIDCache.Instance.UpdateDataToDb(player.uPlayer.playerInfo);

            foreach (var key in products.Keys) {
                int value = products[key];
                int typeId = int.Parse(key);

                
                // 注意多线程
                GM_BackpackMgr.Instance.UpdateGoodsWithTid(player, typeId, value);
            }

            return true;
        }


    }
}
