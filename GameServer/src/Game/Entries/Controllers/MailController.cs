using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace Game.Entries.Controllers
{
    [Controller]
    public sealed class MailController
    {
        [RequestMapping]
        public object DoGetMailSummary(IdSession session, ReqMailSummary request, long playerId) =>
            MailModule.Instance.HandlerGetMailSummary(session, request, playerId);

        [RequestMapping]
        public object DoGetMailList(IdSession session, ReqMailList request, long playerId) =>
            MailModule.Instance.HandlerGetMailList(session, request, playerId);

        [RequestMapping]
        public object DoReadMail(IdSession session, ReqMailRead request, long playerId) =>
            MailModule.Instance.HandlerReadMail(session, request, playerId);

        [RequestMapping]
        public object DoClaimMail(IdSession session, ReqMailClaim request, long playerId) =>
            MailModule.Instance.HandlerClaimMail(session, request, playerId);

        [RequestMapping]
        public object DoClaimAllMail(IdSession session, ReqMailClaimAll request, long playerId) =>
            MailModule.Instance.HandlerClaimAllMail(session, request, playerId);

        [RequestMapping]
        public object DoDeleteMail(IdSession session, ReqMailDelete request, long playerId) =>
            MailModule.Instance.HandlerDeleteMail(session, request, playerId);

        [RequestMapping]
        public object DoDeleteReadMail(IdSession session, ReqMailDeleteRead request, long playerId) =>
            MailModule.Instance.HandlerDeleteReadMail(session, request, playerId);
    }
}
