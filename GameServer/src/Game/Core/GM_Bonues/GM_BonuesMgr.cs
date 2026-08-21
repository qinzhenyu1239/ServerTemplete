using Framework.Core.Utils;
using Game.Datas.DBEntities;
using Game.Core.Db;
using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Utils;


/*
 * 奖励类型的ID编码:  100000类 100001  ---> 100000类的第1种,  ....;
 * 200000类200001---->200000的第1种，依此类推;
 */

namespace Game.Core.GM_Bonues
{
    public class GM_BonuesMgr {

        public static GM_BonuesMgr Instance = new GM_BonuesMgr();

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public void Init() {
           
        }

        public bool GenBonues(long playerId, int typeId, string desic, int duration, 
            int b1 = 0, int b2 = 0, int b3 = 0, int b4 = 0, int b5 = 0) {

            Bonues dbBonues = new Bonues();
            dbBonues.uid = playerId;
            dbBonues.tid = typeId;
            dbBonues.bonuesDesic = desic;
            dbBonues.status = 0;
            dbBonues.time = (int)UtilsHelper.Timestamp();
            dbBonues.endTime = (duration <= 0) ? -1 : dbBonues.time + duration;

            dbBonues.bonues1 = b1;
            dbBonues.bonues2 = b2;
            dbBonues.bonues3 = b3;
            dbBonues.bonues4 = b4;
            dbBonues.bonues5 = b5;

            DBService.Instance.GetGameInstance().Insertable(dbBonues).ExecuteCommandAsync();

            return true;
        }

        private bool ApplayBonuesToPlayer(Bonues dbBonues) {
            if (dbBonues == null || dbBonues.status != 0)
            {
                return false;
            }

            MethodInfo method = null;
            int mainKey = ((dbBonues.tid) / 100000) * 100000;
            method = RuleProcesserFactory.GetProcesser((int)RuleType.Bonues, "Applay", dbBonues.tid, mainKey);
            
            if (method == null)
            {
                logger.Warn($"unkonw BonuesType: {dbBonues.tid}");
                return false;
            }

            method.Invoke(null, new object[] { dbBonues });

            return true;
        }

        public bool RecvBonues(long bonuesId, Bonues dbBonues = null) {

            if (dbBonues != null) {
                dbBonues = DBService.Instance.GetGameInstance().Queryable<Game.Datas.DBEntities.Bonues>().First(it => it.id == bonuesId);//查询单条
            } 

            bool ret = this.ApplayBonuesToPlayer(dbBonues);
            if (!ret) {
                return false;
            }

            dbBonues.status = 1;
            DBService.Instance.GetGameInstance().Updateable(dbBonues).Where(it => it.id == dbBonues.id).ExecuteCommandAsync();

            return true;
        }

        public Bonues GenAndRecvBonues(long playerId, int typeId, string desic, int duration,
            int b1 = 0, int b2 = 0, int b3 = 0, int b4 = 0, int b5 = 0)
        {
            Bonues dbBonues = new Bonues();
            dbBonues.uid = playerId;
            dbBonues.tid = typeId;
            dbBonues.bonuesDesic = desic;
            dbBonues.status = 0;
            dbBonues.time = (int)UtilsHelper.Timestamp();
            dbBonues.endTime = (duration <= 0) ? -1 : dbBonues.time + duration;

            dbBonues.bonues1 = b1;
            dbBonues.bonues2 = b2;
            dbBonues.bonues3 = b3;
            dbBonues.bonues4 = b4;
            dbBonues.bonues5 = b5;

            this.ApplayBonuesToPlayer(dbBonues);

            dbBonues.status = 1;
            DBService.Instance.GetGameInstance().Insertable(dbBonues).ExecuteCommandAsync();

            return dbBonues;
        }


        private Bonues CalcBonuesToPlayer(long playerId, int typeId, object param) {
            MethodInfo method = null;
            int mainKey = ((typeId) / 100000) * 100000;
            method = RuleProcesserFactory.GetProcesser((int)RuleType.Bonues, "Calc", typeId, mainKey);

            if (method == null)
            {
                logger.Warn($"unkonw BonuesType: {typeId}");
                return null;
            }

            Game.Datas.DBEntities.Bonues dbBonues = (Game.Datas.DBEntities.Bonues)method.Invoke(null, new object[] { playerId, typeId, param });
            return dbBonues;
        }

        // 将计算奖励的放到奖励计算的规则里面
        public void GenBonuesToPlayer(long playerId, int typeId, object param = null) {
           

            Bonues dbBonues = this.CalcBonuesToPlayer(playerId, typeId, param);
            if (dbBonues == null) {
                return;
            }

            DBService.Instance.GetGameInstance().Insertable(dbBonues).ExecuteCommandAsync();
        }

        public void GenAndRecvBonuesToPlayer(long playerId, int typeId, object param = null)
        {
            Bonues dbBonues = this.CalcBonuesToPlayer(playerId, typeId, param);
            if (dbBonues == null) {
                return;
            }


            this.ApplayBonuesToPlayer(dbBonues);
            dbBonues.status = 1;

            DBService.Instance.GetGameInstance().Insertable(dbBonues).ExecuteCommandAsync();
        }

        public Bonues[] PullingBonuesData(long playerId, int typeId) {

            return DBService.Instance.GetGameInstance().Queryable<Bonues>().Where(it => it.uid == playerId).ToArray();
        }

        public Bonues GetBonuesBy(long bonuesId) {
            return DBService.Instance.GetGameInstance().Queryable<Bonues>().First(it => it.id == bonuesId);
        }
    }
}
