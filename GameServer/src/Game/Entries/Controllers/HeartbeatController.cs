using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.Messages;
using System;

namespace Game.Entries.Controllers
{
    [Controller]
    public sealed class HeartbeatController
    {
        [RequestMapping]
        public object DoHeartbeat(IdSession session, ReqHeartbeat request, long playerId) =>
            new ResHeartbeat
            {
                status = playerId > 0 ? (int)Respones.OK : (int)Respones.AccountIsNotLogin,
                clientUnixMilliseconds = request?.clientUnixMilliseconds ?? 0,
                serverUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
    }
}
