using Framework.Core.Net;
using Game.Core.GM_Auth;
using Game.Datas.Messages;
using System;
using System.Configuration;

namespace Game.Entries.Modules
{
    /// <summary>
    /// Validates the game-authentication request and assembles its protocol response.
    /// Token and ticket state is owned by GM_AuthSessionMgr.
    /// </summary>
    public sealed class GameAuthModule
    {
        public static GameAuthModule Instance { get; } = new GameAuthModule();

        public ResGameAuthenticate HandlerReqGameAuthenticate(
            IdSession session,
            ReqGameAuthenticate request)
        {
            int configuredServerId = int.TryParse(ConfigurationManager.AppSettings["ServerId"], out int value)
                ? value
                : 1;
            var response = new ResGameAuthenticate();
            if (request == null || request.serverId != configuredServerId ||
                string.IsNullOrWhiteSpace(request.gameTicket))
            {
                response.status = (int)Respones.InvalidParams;
                response.errorMessage = "Invalid game authentication request.";
                SessionMgr.Instance.CloseAfterNextResponse(session);
                return response;
            }

            GameAuthenticationResult result = GM_AuthSessionMgr.Instance.AuthenticateGameSession(
                session,
                request.gameTicket,
                request.serverId);
            if (result.Status == AuthOperationStatus.InvalidGameTicket)
            {
                response.status = (int)Respones.AccountIsNotLogin;
                response.errorMessage = "The game ticket is invalid, expired, or already used.";
                SessionMgr.Instance.CloseAfterNextResponse(session);
                return response;
            }

            if (result.Status == AuthOperationStatus.SupersededSession)
            {
                response.status = (int)Respones.AccountIsNotLogin;
                response.errorMessage = "The game ticket belongs to a superseded login session.";
                SessionMgr.Instance.CloseAfterNextResponse(session);
                return response;
            }
            if (result.Status != AuthOperationStatus.Success)
            {
                response.status = (int)Respones.AccountIsNotLogin;
                response.errorMessage = "Game authentication failed.";
                SessionMgr.Instance.CloseAfterNextResponse(session);
                return response;
            }

            response.status = (int)Respones.OK;
            response.accountId = result.AccountId;
            return response;
        }

        public string HandlerSessionInvalidation(ReqInvalidateAccountSession request)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (request == null || request.accountId <= 0 || request.sessionVersion <= 0 ||
                string.IsNullOrWhiteSpace(request.sessionId) || string.IsNullOrWhiteSpace(request.nonce) ||
                request.signature == null || request.signature.Length == 0 ||
                request.timestamp < now - 30 || request.timestamp > now + 30)
            {
                return "INVALID";
            }

            var command = new SessionInvalidationCommand
            {
                AccountId = request.accountId,
                SessionVersion = request.sessionVersion,
                SessionId = request.sessionId,
                Timestamp = request.timestamp,
                Nonce = request.nonce,
                Signature = request.signature
            };
            return GM_AuthSessionMgr.Instance.ApplySessionInvalidation(command) ? "OK" : "INVALID";
        }
    }
}
