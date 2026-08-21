
using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using Game.Core.GM_Bonues;
using Game.Core.Caches;
using System.Configuration;

namespace Game.Entries.Modules
{
    public class AuthModule {
        public static AuthModule Instance = new AuthModule();

        private bool GatewayMode = false;
        public void Init() {
            AccountGuestCache.Instance.Init();
            AccountIDCache.Instance.Init();

            this.GatewayMode = bool.Parse(ConfigurationManager.AppSettings["GatewayMode"]);
        }

        private void CopyDbAccountToResponesData(Account dbAccount, AccountInfo aInfo)
        {
            aInfo.uface = dbAccount.uface;
            aInfo.unick = dbAccount.unick;
            aInfo.isGuest = dbAccount.is_guest;
            aInfo.uvip = dbAccount.uvip;
        }

        public ResGuestLogin HandlerReqGuestLogin(IdSession s, ReqGuestLogin req) {
            ResGuestLogin res = new ResGuestLogin();
            res.uinfo = null;

            // 检查参数
            if (req == null || 
                req.guestKey == null || 
                req.guestKey.Equals("") ||
                req.channal <= (int)Channal.InvalidChannal) {
                res.status = (int)Respones.InvalidParams;
                res.uinfo = null;

                return res;
            }
            // end


            Account dbAccount = AccountGuestCache.Instance.GetOrCreate(req.guestKey, req.channal);
            if (dbAccount.is_guest != 1) { // 这个账号是已经升级过的正式账号，不使用游客登录了;
                res.status = (int)Respones.UserIsNotGuest;
                return res;
            }

            if (dbAccount.status != 0) // 账号被冻结了
            { 
                res.status = (int)Respones.UserIsFreeze;
                return res;
            }

            res.status = (int)Respones.OK;
            res.uinfo = new AccountInfo();
            this.CopyDbAccountToResponesData(dbAccount, res.uinfo);

            // 将我们的uid与session关联起来，这样当我们的session收到数据的时候，知道是哪个UID;
            if (s != null) { // http login的时候，会为null
                s.accountId = dbAccount.uid;
            }
            
            if (this.GatewayMode) {
                res.accountIdOrOpenId = dbAccount.uid;
            }

            if (s == null) {  // http模式，将accountId带出去;
                res.accountIdOrOpenId = dbAccount.uid;
            }
            // end

            return res;
        }

        public ResGuestUpgrade HandlerReqGuestUpgrade(IdSession s, ReqGuestUpgrade req, long playerId) {

            ResGuestUpgrade res = new ResGuestUpgrade();

            Player p = PlayerIDCache.Instance.Get(playerId);
            // 验证accountId账号;
            if (p == null) {
                res.status = (int)Respones.AccountIsNotLogin;
                return res;
            }

            if (req == null ||
                req.uname == null ||
                req.uname.Equals("") ||
                req.upwd == null ||
                req.upwd.Equals("") ||
                req.unick == null ||
                req.unick.Equals(""))
            {
                res.status = (int)Respones.InvalidParams;
                return res;
            }

            // 重复的用户名，要返回错误;
            Account dbAccount = AccountUnameCache.Instance.Get(req.uname);
            if (dbAccount != null) {
                res.status = (int)Respones.UnameIsExist;
                return res;
            }
            // end

            // 检查是否为游客账号
            dbAccount = AccountIDCache.Instance.Get(p.accountId);
            if (dbAccount.is_guest == 0) {
                res.status = (int)Respones.UserIsNotGuest;
                return res;
            }

            // 从缓存里面移除;
            AccountGuestCache.Instance.Remove(dbAccount.guest_key);

            // 更新缓存里面的内容; AccountIDCache, 删除掉AccountGuestCache 里面，添加 AccountUnameCache;
            dbAccount.is_guest = 0;
            dbAccount.guest_key = "";
            dbAccount.uname = req.uname;
            dbAccount.upwd = UtilsHelper.Md5(req.upwd);
            dbAccount.unick = req.unick;

            AccountIDCache.Instance.UpdateAccountToDatabase(dbAccount);

            AccountUnameCache.Instance.Put(dbAccount.uname, dbAccount);
            // end

            res.status = (int)Respones.OK;
            res.uinfo = new AccountInfo();
            this.CopyDbAccountToResponesData(dbAccount, res.uinfo);


            // 系统给当前玩家生成一个游客账号升级的奖励
            Game.Datas.Excels.BonuesRuleA configItem = (Game.Datas.Excels.BonuesRuleA)ExcelUtils.GetConfigData<Game.Datas.Excels.BonuesRuleA>("100001");
            GM_BonuesMgr.Instance.GenBonuesToPlayer(p.id, configItem.ID, configItem.value);
            // end 

            return res;
        }

        public ResUserLogin HandlerReqUserLogin(IdSession s, ReqUserLogin req)
        {
            ResUserLogin res = new ResUserLogin();
            if (req == null ||
                req.uname == null ||
                req.uname.Equals("") ||
                req.upwd == null ||
                req.upwd.Equals("")) {
                res.status = (int)Respones.InvalidParams;
                return res;
            }

            Account dbAccount = AccountUnameCache.Instance.Get(req.uname);
            if (dbAccount == null) {
                res.status = (int)Respones.AccountIsNotExist;
                return res;
            }

            string md5Password = UtilsHelper.Md5(req.upwd);
            if (!md5Password.Equals(dbAccount.upwd)) {
                res.status = (int)Respones.UnameOrUpwdError;
                return res;
            }

            res.status = (int)Respones.OK;
            res.uinfo = new AccountInfo();
            this.CopyDbAccountToResponesData(dbAccount, res.uinfo);

            if (s != null) { // http模式，将accountId带出去;
                s.accountId = dbAccount.uid;
            }
            else {
                res.accountIdOrOpenId = dbAccount.uid;
            }

            if (this.GatewayMode)
            {
                res.accountIdOrOpenId = dbAccount.uid;
            }
            // end

            return res;
        }

        public ResRegisterUser HandlerReqRegisterUser(IdSession s, ReqRegisterUser req)
        {
            ResRegisterUser res = new ResRegisterUser();
            res.errorStr = null;

            // 检查参数
            if (req == null ||
                req.uname == null ||
                req.uname.Equals("") ||
                req.upwd == null ||
                req.upwd.Equals("") ||
                req.channal <= (int)Channal.InvalidChannal)
            {
                res.status = (int)Respones.InvalidParams;
                // res.errorStr = "用户名or密码不符合要求";
                return res;
            }
            // end

            // 重复的用户名，要返回错误;
            Account dbAccount = AccountUnameCache.Instance.Get(req.uname);
            if (dbAccount != null)
            {
                res.status = (int)Respones.UnameIsExist;
                // res.errorStr = "用户名已经存在";
                return res;
            }
            // end


            dbAccount = AccountUnameCache.Instance.GetOrCreate(req);
            if (dbAccount == null) {
                res.status = (int)Respones.SystemErr;
                // res.errorStr = "系统出错了,稍后再试";
                return res;
            }

            res.status = (int)Respones.OK;
            return res;
        }

    }

}


