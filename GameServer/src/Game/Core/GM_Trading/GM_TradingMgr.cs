using Game.Core.Db;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.GM_Trading {
    public class GM_TradingMgr {
        public static GM_TradingMgr Instance = new GM_TradingMgr();

        private NLog.Logger logger = null;

        public void Init() {
            this.logger = NLog.LogManager.GetCurrentClassLogger();

            
        }

        private int MainType(int tid)
        {
            return (tid / 1000000) * 1000000;
        }


        private MethodInfo GetProcesserFunc(string funcName, Dictionary<string, MethodInfo> funMap, Dictionary<string, MethodInfo> defaultFunMap)
        {
            if (funMap.ContainsKey(funcName))
            {
                return funMap[funcName];
            }
            if (defaultFunMap.ContainsKey(funcName))
            {
                return defaultFunMap[funcName];
            }

            return null;
        }

        public int CanExchangeProduct(GM_PlayerEntity player, int productId) {

            MethodInfo m = RuleProcesserFactory.GetProcesser((int)RuleType.Trading, "CanExchange", productId, this.MainType(productId));
            if (m == null) {
                this.logger.Error($"tid:{productId} has action: CanExchange func is null");
                return (int)Respones.InvalidParams;
            }

            int ret = (int)(m.Invoke(null, new object[] { player, productId }));

            return ret;
        }

        public void DoExchangeProduct(GM_PlayerEntity player, int productId) {
            MethodInfo m = RuleProcesserFactory.GetProcesser((int)RuleType.Trading, "DoExchange", productId, this.MainType(productId));

            if (m == null)
            {
                this.logger.Error($"tid:{productId} has action: DoExchange func is null");
                return;
            }

            bool ret = (bool)(m.Invoke(null, new object[] { player, productId }));
            if (!ret) {
                this.logger.Error($"tid:{productId} do action: DoExchange return false !");
            }
        }

        public long GenProductOrder(GM_PlayerEntity player, int productId, int payType) {
            MethodInfo m = RuleProcesserFactory.GetProcesser((int)RuleType.Trading, "GenOrder", productId, this.MainType(productId));

            if (m == null)
            {
                this.logger.Error($"tid:{productId} has action: GenOrder func is null");
                return 0;
            }

            return (long)(m.Invoke(null, new object[] { player, productId, payType }));
        }

        public void UserOrderDelivery(GM_PlayerEntity player, int productId, long orderId) {
            MethodInfo m = RuleProcesserFactory.GetProcesser((int)RuleType.Trading, "OrderDelivery", productId, this.MainType(productId));

            if (m == null)
            {
                this.logger.Error($"tid:{productId} has action: GenOrder func is null");
                return;
            }

            bool ret =  (bool)(m.Invoke(null, new object[] { player, productId, orderId }));
            if (!ret) {
                this.logger.Error($"orderId:{orderId} OrderDelivery: Failed !");
            }
        }
    }

}