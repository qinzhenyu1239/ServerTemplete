using Framework.Core.Cache;
using Game.Datas.DBEntities;
using Game.Core.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.Caches
{
    class AccountIDCache : BaseCacheSerivce<long, Account>
    {
        public static AccountIDCache Instance = new AccountIDCache();
        public void Init() { 
        }

        public override Account Load(long accountID)
        {
            return DBService.Instance.GetAuthInstance().Queryable<Account>().First(it => it.uid == accountID);//查询单条
        }

        public void UpdateAccountToDatabase(Account dbAccount) {
            DBService.Instance.GetAuthInstance().Updateable(dbAccount).Where(it => it.uid == dbAccount.uid).ExecuteCommandAsync();
        }
    }
}
