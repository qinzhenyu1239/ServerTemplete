using Framework.Core.Utils;
using Game.Datas.Excels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.Configs
{
    public class ConfigMgr
    {
        public static ConfigMgr Instance = new ConfigMgr();

        public bool useRedis = false;
        public void Init() {


            this.useRedis = bool.Parse(ConfigurationManager.AppSettings["useRedis"]);

            // 提前加载读出来所有的Excel表;
            ExcelUtils.ReadConfigData<BonuesRuleA>();
            ExcelUtils.ReadConfigData<CollectTask>();
            ExcelUtils.ReadConfigData<ExchangeTrading>();
            ExcelUtils.ReadConfigData<OrderTrading>();
            ExcelUtils.ReadConfigData<LogicServerInst>();


            ExcelUtils.ReadConfigData<BuffAConfig>();
            ExcelUtils.ReadConfigData<CharactorConfig>();
            ExcelUtils.ReadConfigData<MapConfig>();
            ExcelUtils.ReadConfigData<MedicineGoods>();

            ExcelUtils.ReadConfigData<SkillAConfig>();
            ExcelUtils.ReadConfigData<SkillBConfig>();
            ExcelUtils.ReadConfigData<WeaponGoods>();
            ExcelUtils.ReadConfigData<WearGoods>();
            ExcelUtils.ReadConfigData<ZoneConfig>();

        }
    }
}
