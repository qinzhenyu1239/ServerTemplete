using Framework.Core.Utils;
using Game.Core.EntityMgr;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
 * 如果你是要根据奖励的Typeid来奖励的规则计算，你可以将计算规则，放到这里;
 * 标记BonuesCalc,否则的话，你就可以放到外面去计算;
 * 给玩家 playerId ----> 一个typeId的奖励;  ===>根据玩家的等级,  金币，魔法,...
 * calcBonoues;
 */

/*
 * A类奖励，基本上就是直接奖励一些奖品,
 * 不是所有的奖励，都是奖励金币;
 */ 
namespace Game.Core.GM_Bonues.Rules
{
    
    [RuleModule((int)RuleType.Bonues, 100000)]
    public class BonuesRuleA
    {
        static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [RuleProcesser("Applay", -1)]
        public static void DefaultBonuesApplayToPlayer(Game.Datas.DBEntities.Bonues bonues) {
            
            logger.Info("DefaultBonuesApplayToPlayer");
            // 给玩家计算出来，它这次账号升级可以获得的奖励
            Game.Datas.DBEntities.Player playerInfo = GM_AccountEntityMgr.Instance.GetPlayerById(bonues.uid);
            playerInfo.ucoin += bonues.bonues1; 
            GM_AccountEntityMgr.Instance.UpdatePlayer(playerInfo);
            // end


            return;
        }

        [RuleProcesser("Calc", -1)]
        public static Game.Datas.DBEntities.Bonues DefaultCaleBonuesToPlayer(long playerId, int typeId, object param = null)
        {
            Game.Datas.DBEntities.Bonues bonues = null;

            
            bonues = new Game.Datas.DBEntities.Bonues();
            bonues.uid = playerId;
            bonues.status = 0;
            bonues.tid = typeId;
            bonues.time = (int)UtilsHelper.Timestamp();
            bonues.endTime = -1;

            Game.Datas.Excels.BonuesRuleA configItem = (Game.Datas.Excels.BonuesRuleA)ExcelUtils.GetConfigData<Game.Datas.Excels.BonuesRuleA>(typeId.ToString());
            // 来自于这个配置文件,  typeId  {获取到了金币的奖励}
            bonues.bonues1 = (int)param; // typeId 100001:  170,    100002 = 10个
            bonues.bonuesDesic = String.Format(configItem.desicFormat, bonues.bonues1); 

            return bonues;
        }

        [RuleProcesser("Calc", 100002)]
        public static Game.Datas.DBEntities.Bonues MoneyCaleBonuesToPlayer(long playerId, int typeId, object param = null)
        {
            Game.Datas.DBEntities.Bonues bonues = null;

            bonues = new Game.Datas.DBEntities.Bonues();
            bonues.uid = playerId;
            bonues.status = 0;
            bonues.tid = typeId;
            bonues.time = (int)UtilsHelper.Timestamp();
            bonues.endTime = -1;

            // 来自于这个配置文件,  typeId  {获取到了金币的奖励}
            bonues.bonues1 = (int)param; // typeId 100001:  170,    100002 = 10个

            Game.Datas.Excels.BonuesRuleA configItem = (Game.Datas.Excels.BonuesRuleA)ExcelUtils.GetConfigData<Game.Datas.Excels.BonuesRuleA>(typeId.ToString());
            bonues.bonuesDesic = String.Format(configItem.desicFormat, bonues.bonues1);

            return bonues;
        }

        [RuleProcesser("Applay", 100002)]
        public static void MonneyBonuesApplayToPlayer(Game.Datas.DBEntities.Bonues bonues)
        {

            logger.Info("MonneyBonuesApplayToPlayer");
            // 给玩家计算出来，它这次账号升级可以获得的奖励
            Game.Datas.DBEntities.Player playerInfo = GM_AccountEntityMgr.Instance.GetPlayerById(bonues.uid);
            playerInfo.umoney += bonues.bonues1; // 配置表里面读;
            GM_AccountEntityMgr.Instance.UpdatePlayer(playerInfo);
            // end


            return;
        }
    }
}
