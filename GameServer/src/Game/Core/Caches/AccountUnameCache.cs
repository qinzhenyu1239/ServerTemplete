using Framework.Core.Cache;
using Framework.Core.Utils;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using Game.Core.Db;
using Game.Utils;
using System.Threading.Tasks;

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

        public async Task<Account> GetAsync(string uname)
        {
            Account account = GetIfPresent(uname);
            if (account != null)
            {
                return account;
            }

            account = await DBService.Instance.GetAuthInstance()
                .Queryable<Account>()
                .FirstAsync(it => it.uname == uname);
            if (account != null)
            {
                Put(uname, account);
            }

            return account;
        }

        public async Task<Account> CreateAsync(ReqRegisterUser req)
        {
            Account dbAccount = BuildAccount(req);
            try
            {
                int affectedRows = await DBService.Instance.GetAuthInstance()
                    .Insertable(dbAccount)
                    .ExecuteCommandAsync();
                if (affectedRows <= 0)
                {
                    logger.Error($"Create account failed, database inserted no rows: uname[{req.uname}]");
                    return null;
                }
            }
            catch (System.Exception exception)
            {
                logger.Error(exception, $"Create account failed: uname[{req.uname}]");
                return null;
            }

            Put(req.uname, dbAccount);
            AccountIDCache.Instance.Put(dbAccount.uid, dbAccount);
            return dbAccount;
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

            dbAccount = BuildAccount(req);

            // 当前同步注册接口仍使用同步写入；登录接口使用上面的 CreateAsync。
            try
            {
                int affectedRows = DBService.Instance.GetAuthInstance()
                    .Insertable(dbAccount)
                    .ExecuteCommand();
                if (affectedRows <= 0)
                {
                    logger.Error($"Create account failed, database inserted no rows: uname[{req.uname}]");
                    return null;
                }
            }
            catch (System.Exception exception)
            {
                logger.Error(exception, $"Create account failed: uname[{req.uname}]");
                return null;
            }

            this.Put(req.uname, dbAccount);
            AccountIDCache.Instance.Put(dbAccount.uid, dbAccount);
            return dbAccount;
        }

        private Account BuildAccount(ReqRegisterUser req)
        {
            Account dbAccount = new Account();
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

            return dbAccount;
        }
    }
}
