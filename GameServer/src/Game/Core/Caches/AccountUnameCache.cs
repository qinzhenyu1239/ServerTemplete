using Framework.Core.Cache;
using Framework.Core.Utils;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using Game.Core.Db;
using Game.Utils;

namespace Game.Core.Caches
{
    public class AccountUnameCache : BaseCacheSerivce<string, Account>
    {
        public static AccountUnameCache Instance = new AccountUnameCache();
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public void Init()
        {
        }

        public override Account Load(string uname)
        {
            return DBService.Instance.GetAuthInstance().Queryable<Account>().First(it => it.uname == uname);
        }

        private string RandUnameNick()
        { // [1000, 10000)
            return "用户" + GameUtils.Random(1000, 10000);
        }


        public Account GetOrCreate(ReqRegisterUser req)
        {
            Account dbAccount = Get(req.uname);
            if (dbAccount != null) {
                return dbAccount;
            }

            dbAccount = new Account();
            dbAccount.uid = IdGenerator.GetNextId();
            dbAccount.guest_key = "";
            dbAccount.is_guest = 0;
            dbAccount.uchannel = req.channal;

            dbAccount.unick = this.RandUnameNick(); 
            dbAccount.uface = GameUtils.Random(0, 6);
            dbAccount.usex = (dbAccount.uface < 3) ? 1 : 0;
            dbAccount.status = 0;
            dbAccount.uname = req.uname;
            dbAccount.upwd = UtilsHelper.Md5(req.upwd);
            /*dbAccount.phone = "";
            
            dbAccount.upwd = "";
            dbAccount.vip_end_time = 0;
            dbAccount.uvip = 0;
            dbAccount.email = "";
            dbAccount.address = "";*/

            // 写入数据库
            DBService.Instance.GetAuthInstance().Insertable(dbAccount).ExecuteCommandAsync();
            //end

            this.Put(req.uname, dbAccount);

            // 添加uid--->account的映射
            AccountIDCache.Instance.Put(dbAccount.uid, dbAccount);

            return dbAccount;
        }
    }
}
