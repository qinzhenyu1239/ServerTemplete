using Framework.Core.Utils;
using Game.Core.Caches;
using Game.Datas.DBEntities;
using Game.Datas.Messages;

namespace Game.Entries.Modules
{
    public sealed class AuthAccountUpgradeModule
    {
        public static AuthAccountUpgradeModule Instance { get; } = new AuthAccountUpgradeModule();

        public ResGuestUpgrade Upgrade(ReqGuestUpgrade req, long accountId)
        {
            var response = new ResGuestUpgrade();
            if (accountId <= 0)
            {
                response.status = (int)Respones.AccountIsNotLogin;
                return response;
            }
            if (req == null || string.IsNullOrWhiteSpace(req.uname) ||
                string.IsNullOrWhiteSpace(req.upwd) || string.IsNullOrWhiteSpace(req.unick))
            {
                response.status = (int)Respones.InvalidParams;
                return response;
            }
            if (AccountUnameCache.Instance.Get(req.uname) != null)
            {
                response.status = (int)Respones.UnameIsExist;
                return response;
            }

            Account account = AccountIDCache.Instance.Get(accountId);
            if (account == null)
            {
                response.status = (int)Respones.AccountIsNotExist;
                return response;
            }
            if (account.is_guest == 0)
            {
                response.status = (int)Respones.UserIsNotGuest;
                return response;
            }

            AccountGuestCache.Instance.Remove(account.guest_key);
            account.is_guest = 0;
            account.guest_key = string.Empty;
            account.uname = req.uname;
            account.upwd = UtilsHelper.Md5(req.upwd);
            account.unick = req.unick;
            AccountIDCache.Instance.UpdateAccountToDatabase(account);
            AccountUnameCache.Instance.Put(account.uname, account);

            response.status = (int)Respones.OK;
            response.uinfo = new AccountInfo
            {
                uface = account.uface,
                unick = account.unick,
                isGuest = account.is_guest,
                uvip = account.uvip
            };
            return response;
        }
    }
}
