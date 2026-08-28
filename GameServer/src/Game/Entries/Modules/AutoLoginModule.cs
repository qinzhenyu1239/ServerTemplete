using Framework.Core.Utils;
using Game.Core.Caches;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Entries.Modules
{
    /// <summary>
    /// Logs an existing account in, or creates it from the supplied credentials on first login.
    /// </summary>
    public sealed class AutoLoginModule
    {
        public static AutoLoginModule Instance { get; } = new AutoLoginModule();

        private sealed class AccountCreationLock
        {
            public readonly SemaphoreSlim Semaphore = new(1, 1);
            public int ReferenceCount;
        }

        private readonly object accountCreationLocksSync = new();
        private readonly Dictionary<string, AccountCreationLock> accountCreationLocks =
            new(System.StringComparer.OrdinalIgnoreCase);

        public async Task<ResUserLogin> LoginOrRegisterAsync(ReqUserLogin req)
        {
            var response = new ResUserLogin();
            if (req == null || string.IsNullOrWhiteSpace(req.uname) || string.IsNullOrWhiteSpace(req.upwd))
            {
                response.status = (int)Respones.InvalidParams;
                return response;
            }

            Account account = await AccountUnameCache.Instance.GetAsync(req.uname);
            if (account == null)
            {
                AccountCreationLock accountLock = RetainAccountCreationLock(req.uname);
                await accountLock.Semaphore.WaitAsync();
                try
                {
                    // Another request may have created the account while this request was waiting.
                    account = await AccountUnameCache.Instance.GetAsync(req.uname);
                    if (account == null)
                    {
                        account = await AccountUnameCache.Instance.CreateAsync(new ReqRegisterUser
                        {
                            uname = req.uname,
                            upwd = req.upwd,
                            channal = (int)Channal.SelfChannal
                        });
                    }
                }
                finally
                {
                    accountLock.Semaphore.Release();
                    ReleaseAccountCreationLock(req.uname, accountLock);
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
            if (!string.Equals(account.upwd, UtilsHelper.Md5(req.upwd), System.StringComparison.Ordinal))
            {
                response.status = (int)Respones.UnameOrUpwdError;
                return response;
            }

            response.status = (int)Respones.OK;
            response.accountIdOrOpenId = account.uid;
            response.uinfo = new AccountInfo
            {
                unick = account.unick,
                uface = account.uface,
                isGuest = account.is_guest,
                uvip = account.uvip
            };
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
