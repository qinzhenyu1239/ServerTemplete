using Framework.Core.Cache;
using Framework.Core.Utils;
using Game.Datas.DBEntities;
using Game.Core.Db;
using Game.Utils;


namespace Game.Core.Caches {
    public class AccountGuestCache : BaseCacheSerivce<string, Account>
    {
        public static AccountGuestCache Instance = new AccountGuestCache();
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public void Init() { 
        }

        public override Account Load(string guestKey)
        {
            return DBService.Instance.GetAuthInstance().Queryable<Account>().First(it => it.guest_key == guestKey);
        }

        private string RandGuestNick()
        { // [1000, 10000)
            return "游客" + GameUtils.Random(1000, 10000);
        }

        public Account GetOrCreate(string guestKey, int channal) {
            Account dbAccount = Get(guestKey);
            if (dbAccount != null) {
                return dbAccount;
            }

            dbAccount = new Account();
            dbAccount.uid = IdGenerator.GetNextId();
            dbAccount.guest_key = guestKey;
            dbAccount.is_guest = 1;
            dbAccount.uchannel = channal;

            dbAccount.unick = this.RandGuestNick();
            dbAccount.uface = GameUtils.Random(0, 6);
            dbAccount.usex = (dbAccount.uface < 3) ? 1 : 0;
            dbAccount.status = 0;
            /*dbAccount.phone = "";
            dbAccount.uname = "";
            dbAccount.upwd = "";
            dbAccount.vip_end_time = 0;
            dbAccount.uvip = 0;
            dbAccount.email = "";
            dbAccount.address = "";*/
            // 写入数据库
            DBService.Instance.GetAuthInstance().Insertable(dbAccount).ExecuteCommandAsync();
            //end

            this.Put(guestKey, dbAccount);

            // 添加uid--->account的映射
            AccountIDCache.Instance.Put(dbAccount.uid, dbAccount);

            return dbAccount;
        }

    }
}
