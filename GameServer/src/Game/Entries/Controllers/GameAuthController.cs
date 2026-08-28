using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;
using System.Configuration;

namespace Game.Entries.Controllers
{
    [Controller]
    public sealed class GameAuthController
    {
        [RequestMapping]
        public object DoReqGameAuthenticate(IdSession session, ReqGameAuthenticate req)
        {
            int configuredServerId = int.TryParse(ConfigurationManager.AppSettings["ServerId"], out int value)
                ? value : 1;
            var response = new ResGameAuthenticate();
            if (req == null || req.serverId != configuredServerId || string.IsNullOrWhiteSpace(req.gameTicket))
            {
                response.status = (int)Respones.InvalidParams;
                response.errorMessage = "Invalid game authentication request.";
                return response;
            }
            if (!AuthSessionModule.Instance.TryConsumeGameTicket(
                    req.gameTicket, req.serverId, out long accountId, out string authSessionId,
                    out long sessionVersion))
            {
                response.status = (int)Respones.AccountIsNotLogin;
                response.errorMessage = "The game ticket is invalid, expired, or already used.";
                return response;
            }
            if (!AuthSessionModule.Instance.TryBindAuthenticatedGameSession(
                    session, accountId, authSessionId, sessionVersion))
            {
                response.status = (int)Respones.AccountIsNotLogin;
                response.errorMessage = "The game ticket belongs to a superseded login session.";
                return response;
            }
            response.status = (int)Respones.OK;
            response.accountId = accountId;
            return response;
        }
    }
}
