using Game.Core.EntityMgr;
using Game.Core.GM_Auth;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using System;
using System.Threading.Tasks;

namespace Game.Entries.Modules
{
    /// <summary>
    /// Handles account-stage authentication operations. Access tokens, game tickets,
    /// and authenticated game connections remain the responsibility of GM_AuthSessionMgr.
    /// </summary>
    public sealed class AuthModule
    {
        public static AuthModule Instance { get; } = new AuthModule();

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
            response.accessToken = GM_AuthSessionMgr.Instance.CreateLoginSession(account.uid);
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

            Account account = await GM_AccountEntityMgr.Instance.GetOrCreateAccountAsync(
                request.uname, request.upwd, (int)Channal.SelfChannal);

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
            if (!GM_AccountEntityMgr.Instance.VerifyPassword(account, request.upwd))
            {
                response.status = (int)Respones.UnameOrUpwdError;
                return response;
            }

            response.status = (int)Respones.OK;
            response.accountIdOrOpenId = account.uid;
            response.uinfo = new AccountInfo();
            CopyAccountInfo(account, response.uinfo);
            response.accessToken = GM_AuthSessionMgr.Instance.CreateLoginSession(account.uid);
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

            var command = new AccountRegistrationCommand
            {
                Username = request.uname,
                Password = request.upwd,
                Channel = request.channal
            };
            response.status = GM_AccountEntityMgr.Instance.CreateAccount(command) == null
                ? (int)Respones.SystemErr
                : (int)Respones.OK;
            return response;
        }

        public ResGuestUpgrade HandlerReqGuestUpgrade(ReqGuestUpgrade request)
        {
            var response = new ResGuestUpgrade();
            if (request == null || string.IsNullOrWhiteSpace(request.uname) ||
                string.IsNullOrWhiteSpace(request.upwd) || string.IsNullOrWhiteSpace(request.unick))
            {
                response.status = (int)Respones.InvalidParams;
                return response;
            }
            AccessTokenValidationResult validation =
                GM_AuthSessionMgr.Instance.ValidateAccessToken(request.accessToken);
            if (!validation.IsSuccess)
            {
                response.status = (int)Respones.AccountIsNotLogin;
                return response;
            }
            if (GM_AccountEntityMgr.Instance.GetAccountByUsername(request.uname) != null)
            {
                response.status = (int)Respones.UnameIsExist;
                return response;
            }

            Account account = GM_AccountEntityMgr.Instance.GetAccountById(validation.AccountId);
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

            account = GM_AccountEntityMgr.Instance.UpgradeGuestAccount(new GuestAccountUpgradeCommand
            {
                AccountId = validation.AccountId,
                Username = request.uname,
                Password = request.upwd,
                Nickname = request.unick
            });
            if (account == null)
            {
                response.status = (int)Respones.SystemErr;
                return response;
            }

            response.status = (int)Respones.OK;
            response.uinfo = new AccountInfo();
            CopyAccountInfo(account, response.uinfo);
            return response;
        }

        public ResIssueGameTicket HandlerReqIssueGameTicket(ReqIssueGameTicket request)
        {
            if (request == null || request.serverId <= 0)
            {
                return new ResIssueGameTicket { status = (int)Respones.InvalidParams };
            }
            IssueGameTicketResult result =
                GM_AuthSessionMgr.Instance.IssueGameTicket(request.accessToken, request.serverId);
            if (!result.IsSuccess)
            {
                return new ResIssueGameTicket { status = (int)Respones.AccountIsNotLogin };
            }

            return new ResIssueGameTicket
            {
                status = (int)Respones.OK,
                gameTicket = result.Ticket,
                expiresAtUnixSeconds = result.ExpiresAtUnixSeconds
            };
        }
    }
}
