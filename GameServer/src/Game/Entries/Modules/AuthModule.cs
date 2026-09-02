using Framework.Core.Utils;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Entries.Modules
{
    /// <summary>
    /// Handles account-stage authentication operations. Access tokens, game tickets,
    /// and authenticated game connections remain the responsibility of AuthSessionModule.
    /// </summary>
    public sealed class AuthModule
    {
        public static AuthModule Instance { get; } = new AuthModule();

        private sealed class AccountCreationLock
        {
            public readonly SemaphoreSlim Semaphore = new(1, 1);
            public int ReferenceCount;
        }

        private readonly object accountCreationLocksSync = new();
        private readonly Dictionary<string, AccountCreationLock> accountCreationLocks =
            new(StringComparer.OrdinalIgnoreCase);

        private static void CopyAccountInfo(Account account, AccountInfo accountInfo)
        {
            accountInfo.uface = account.uface;
            accountInfo.unick = account.unick;
            accountInfo.isGuest = account.is_guest;
            accountInfo.uvip = account.uvip;
        }

        public ResGuestLogin HandlerReqGuestLogin(ReqGuestLogin request)
        {
            var response = new ResGuestLogin();
            if (request == null || string.IsNullOrWhiteSpace(request.guestKey) ||
                request.channal <= (int)Channal.InvalidChannal)
            {
                response.status = (int)Respones.InvalidParams;
                return response;
            }

            Account account = GM_AccountEntityMgr.Instance.GetOrCreateGuest(request.guestKey, request.channal);
            if (account == null)
            {
                response.status = (int)Respones.SystemErr;
                return response;
            }
            if (account.is_guest != 1)
            {
                response.status = (int)Respones.UserIsNotGuest;
                return response;
            }
            if (account.status != 0)
            {
                response.status = (int)Respones.UserIsFreeze;
                return response;
            }

            response.status = (int)Respones.OK;
            response.accountIdOrOpenId = account.uid;
            response.uinfo = new AccountInfo();
            CopyAccountInfo(account, response.uinfo);
            return response;
        }

        public async Task<ResUserLogin> HandlerReqUserLoginAsync(ReqUserLogin request)
        {
            var response = new ResUserLogin();
            if (request == null || string.IsNullOrWhiteSpace(request.uname) ||
                string.IsNullOrWhiteSpace(request.upwd))
            {
                response.status = (int)Respones.InvalidParams;
                return response;
            }

            Account account = await GM_AccountEntityMgr.Instance.GetAccountByUsernameAsync(request.uname);
            if (account == null)
            {
                AccountCreationLock accountLock = RetainAccountCreationLock(request.uname);
                await accountLock.Semaphore.WaitAsync();
                try
                {
                    account = await GM_AccountEntityMgr.Instance.GetAccountByUsernameAsync(request.uname);
                    if (account == null)
                    {
                        account = await GM_AccountEntityMgr.Instance.CreateAccountAsync(new ReqRegisterUser
                        {
                            uname = request.uname,
                            upwd = request.upwd,
                            channal = (int)Channal.SelfChannal
                        });
                    }
                }
                finally
                {
                    accountLock.Semaphore.Release();
                    ReleaseAccountCreationLock(request.uname, accountLock);
                }
            }

            if (account == null)
            {
                response.status = (int)Respones.SystemErr;
                return response;
            }
            if (account.status != 0)
            {
                response.status = (int)Respones.UserIsFreeze;
                return response;
            }
            if (!string.Equals(account.upwd, UtilsHelper.Md5(request.upwd), StringComparison.Ordinal))
            {
                response.status = (int)Respones.UnameOrUpwdError;
                return response;
            }

            response.status = (int)Respones.OK;
            response.accountIdOrOpenId = account.uid;
            response.uinfo = new AccountInfo();
            CopyAccountInfo(account, response.uinfo);
            return response;
        }

        public ResRegisterUser HandlerReqRegisterUser(ReqRegisterUser request)
        {
            var response = new ResRegisterUser();
            if (request == null || string.IsNullOrWhiteSpace(request.uname) ||
                string.IsNullOrWhiteSpace(request.upwd) ||
                request.channal <= (int)Channal.InvalidChannal)
            {
                response.status = (int)Respones.InvalidParams;
                return response;
            }

            if (GM_AccountEntityMgr.Instance.GetAccountByUsername(request.uname) != null)
            {
                response.status = (int)Respones.UnameIsExist;
                return response;
            }

            response.status = GM_AccountEntityMgr.Instance.CreateAccount(request) == null
                ? (int)Respones.SystemErr
                : (int)Respones.OK;
            return response;
        }

        public ResGuestUpgrade HandlerReqGuestUpgrade(ReqGuestUpgrade request, long accountId)
        {
            var response = new ResGuestUpgrade();
            if (accountId <= 0)
            {
                response.status = (int)Respones.AccountIsNotLogin;
                return response;
            }
            if (request == null || string.IsNullOrWhiteSpace(request.uname) ||
                string.IsNullOrWhiteSpace(request.upwd) || string.IsNullOrWhiteSpace(request.unick))
            {
                response.status = (int)Respones.InvalidParams;
                return response;
            }
            if (GM_AccountEntityMgr.Instance.GetAccountByUsername(request.uname) != null)
            {
                response.status = (int)Respones.UnameIsExist;
                return response;
            }

            Account account = GM_AccountEntityMgr.Instance.GetAccountById(accountId);
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

            if (!GM_AccountEntityMgr.Instance.UpgradeGuestAccount(
                    accountId, request.uname, UtilsHelper.Md5(request.upwd), request.unick, out account))
            {
                response.status = (int)Respones.SystemErr;
                return response;
            }

            response.status = (int)Respones.OK;
            response.uinfo = new AccountInfo();
            CopyAccountInfo(account, response.uinfo);
            return response;
        }

        private AccountCreationLock RetainAccountCreationLock(string username)
        {
            lock (accountCreationLocksSync)
            {
                if (!accountCreationLocks.TryGetValue(username, out AccountCreationLock accountLock))
                {
                    accountLock = new AccountCreationLock();
                    accountCreationLocks.Add(username, accountLock);
                }

                accountLock.ReferenceCount++;
                return accountLock;
            }
        }

        private void ReleaseAccountCreationLock(string username, AccountCreationLock accountLock)
        {
            lock (accountCreationLocksSync)
            {
                accountLock.ReferenceCount--;
                if (accountLock.ReferenceCount == 0 &&
                    accountCreationLocks.TryGetValue(username, out AccountCreationLock current) &&
                    ReferenceEquals(current, accountLock))
                {
                    accountCreationLocks.Remove(username);
                    accountLock.Semaphore.Dispose();
                }
            }
        }
    }
}
