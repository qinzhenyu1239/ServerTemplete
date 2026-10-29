using Framework.Core.Net;
using Game.Core.EntityMgr;
using Game.Core.GM_MailMessage;
using Game.Datas.Messages;
using Game.Datas.GMEntities;
using System;
using System.Configuration;
using System.Linq;
namespace Game.Entries.Modules
{
    public sealed class MailModule
    {
        public static MailModule Instance { get; } = new();
        public void Init()
        {
            if (!int.TryParse(ConfigurationManager.AppSettings["MailboxCapacity"], out int capacity) || capacity <= 0)
                throw new ConfigurationErrorsException("Explicit positive MailboxCapacity is required for the game server.");
            GM_MailMsgMgr.Instance.Configure(capacity);
        }
        private static (Respones Status, string ErrorMessage) ValidateRequest(
            IdSession session,
            long playerId,
            bool parametersValid)
        {
            GM_PlayerEntity player = playerId > 0 ? GM_EntityMgr.Instance.Get(playerId) : null;
            if (player == null || session == null || session.playerId != playerId ||
                !ReferenceEquals(player.uPlayer.s, session))
            {
                return (Respones.AccountIsNotLogin, "Player is not logged in.");
            }
            if (!parametersValid)
            {
                return (Respones.InvalidParams, "Invalid mail request.");
            }
            if (GM_MailMsgMgr.Instance.Capacity <= 0)
            {
                return (Respones.SystemErr, "Mail module is not initialized.");
            }

            return (Respones.OK, null);
        }

        public MailResult SendMail(MailSendCommand command)
        {
            if (!MailRules.ValidSend(command))
            {
                return new MailResult { Status = MailStatus.Invalid };
            }

            return GM_MailMsgMgr.Instance.Send(command);
        }

        private static int Code(MailResult r) => r.Status switch
        {
            MailStatus.Success=>(int)Respones.OK, MailStatus.Invalid=>(int)Respones.InvalidParams,
            MailStatus.NotFound=>(int)Respones.MailNotFound, MailStatus.NotAllowed=>(int)Respones.MailNotAllowed,
            MailStatus.NoRewards=>(int)Respones.MailNoRewards, MailStatus.InvalidReward=>(int)Respones.MailInvalidReward,
            MailStatus.SourceConflict=>(int)Respones.MailSourceConflict, _=>(int)Respones.SystemErr
        };
        private static MailEntryMessage Entry(MailData d,bool detail) => new MailEntryMessage
        { id=d.Mail.id,mailType=d.Mail.mailType,title=d.Mail.title,body=detail?d.Mail.msgBody:null,
            sendTime=d.Mail.sendTime,expireTime=d.Mail.expireTime,readTime=d.Mail.readTime,claimTime=d.Mail.claimTime,
            attachments=detail?d.Attachments.Select(a=>new MailAttachmentMessage {sortOrder=a.sortOrder,itemId=a.itemId,quantity=a.quantity}).ToList()
                :new System.Collections.Generic.List<MailAttachmentMessage>() };
        public ResMailSummary HandlerGetMailSummary(
            IdSession session,
            ReqMailSummary request,
            long playerId)
        {
            var validation = ValidateRequest(session, playerId,
                request != null);
            if (validation.Status != Respones.OK)
            {
                return new ResMailSummary
                {
                    status = (int)validation.Status,
                    errorMessage = validation.ErrorMessage
                };
            }

            MailResult result = GM_MailMsgMgr.Instance.Execute(playerId, MailAction.Summary, 0);
            return new ResMailSummary {status=Code(result),errorMessage=result.Error ?? result.Status.ToString(),mailboxVersion=result.MailboxVersion,
                serverTime=result.ServerTime,count=result.Count,unreadCount=result.UnreadCount,capacity=result.Capacity,mailId=result.MailId,
                items=new System.Collections.Generic.List<MailEntryMessage>(),
                detail=result.Items.Where(d=>d.Mail.id==result.MailId).Select(d=>Entry(d,true)).FirstOrDefault(),
                itemQuantities=result.ItemQuantities.Select(p=>new PlayerItemMessage {itemId=p.Key,quantity=p.Value}).ToList() };
        }
        public ResMailList HandlerGetMailList(
            IdSession session,
            ReqMailList request,
            long playerId)
        {
            var validation = ValidateRequest(session, playerId,
                request != null && request.offset >= 0 && request.pageSize > 0 &&
                request.pageSize <= 50 && request.offset <= int.MaxValue - request.pageSize);
            if (validation.Status != Respones.OK)
            {
                return new ResMailList
                {
                    status = (int)validation.Status,
                    errorMessage = validation.ErrorMessage
                };
            }

            int offset = request.offset;
            int size = request.pageSize;
            MailResult result = GM_MailMsgMgr.Instance.Execute(playerId, MailAction.List, 0);
            return new ResMailList {status=Code(result),errorMessage=result.Error ?? result.Status.ToString(),mailboxVersion=result.MailboxVersion,
                serverTime=result.ServerTime,count=result.Count,unreadCount=result.UnreadCount,capacity=result.Capacity,mailId=result.MailId,
                items=result.Items.Skip(offset).Take(size).Select(d=>Entry(d,false)).ToList(),hasMore=result.Items.Count>offset+size,nextOffset=offset+size,
                detail=result.Items.Where(d=>d.Mail.id==result.MailId).Select(d=>Entry(d,true)).FirstOrDefault(),
                itemQuantities=result.ItemQuantities.Select(p=>new PlayerItemMessage {itemId=p.Key,quantity=p.Value}).ToList() };
        }
        public ResMailRead HandlerReadMail(
            IdSession session,
            ReqMailRead request,
            long playerId)
        {
            var validation = ValidateRequest(session, playerId,
                request != null && request.mailId > 0);
            if (validation.Status != Respones.OK)
            {
                return new ResMailRead
                {
                    status = (int)validation.Status,
                    errorMessage = validation.ErrorMessage
                };
            }

            MailResult result = GM_MailMsgMgr.Instance.Execute(playerId, MailAction.Read, request.mailId);
            return new ResMailRead {status=Code(result),errorMessage=result.Error ?? result.Status.ToString(),mailboxVersion=result.MailboxVersion,
                serverTime=result.ServerTime,count=result.Count,unreadCount=result.UnreadCount,capacity=result.Capacity,mailId=result.MailId,
                items=result.Items.Take(10).Select(d=>Entry(d,false)).ToList(),hasMore=result.Items.Count>10,nextOffset=10,
                detail=result.Items.Where(d=>d.Mail.id==result.MailId).Select(d=>Entry(d,true)).FirstOrDefault(),
                itemQuantities=result.ItemQuantities.Select(p=>new PlayerItemMessage {itemId=p.Key,quantity=p.Value}).ToList() };
        }
        public ResMailClaim HandlerClaimMail(
            IdSession session,
            ReqMailClaim request,
            long playerId)
        {
            var validation = ValidateRequest(session, playerId,
                request != null && request.mailId > 0);
            if (validation.Status != Respones.OK)
            {
                return new ResMailClaim
                {
                    status = (int)validation.Status,
                    errorMessage = validation.ErrorMessage
                };
            }

            MailResult result = GM_MailMsgMgr.Instance.Execute(playerId, MailAction.Claim, request.mailId);
            return new ResMailClaim {status=Code(result),errorMessage=result.Error ?? result.Status.ToString(),mailboxVersion=result.MailboxVersion,
                serverTime=result.ServerTime,count=result.Count,unreadCount=result.UnreadCount,capacity=result.Capacity,mailId=result.MailId,
                items=result.Items.Take(10).Select(d=>Entry(d,false)).ToList(),hasMore=result.Items.Count>10,nextOffset=10,
                detail=result.Items.Where(d=>d.Mail.id==result.MailId).Select(d=>Entry(d,true)).FirstOrDefault(),
                itemQuantities=result.ItemQuantities.Select(p=>new PlayerItemMessage {itemId=p.Key,quantity=p.Value}).ToList() };
        }
        public ResMailClaimAll HandlerClaimAllMail(
            IdSession session,
            ReqMailClaimAll request,
            long playerId)
        {
            var validation = ValidateRequest(session, playerId,
                request != null);
            if (validation.Status != Respones.OK)
            {
                return new ResMailClaimAll
                {
                    status = (int)validation.Status,
                    errorMessage = validation.ErrorMessage
                };
            }

            MailResult result = GM_MailMsgMgr.Instance.Execute(playerId, MailAction.ClaimAll, 0);
            return new ResMailClaimAll {status=Code(result),errorMessage=result.Error ?? result.Status.ToString(),mailboxVersion=result.MailboxVersion,
                serverTime=result.ServerTime,count=result.Count,unreadCount=result.UnreadCount,capacity=result.Capacity,mailId=result.MailId,
                items=result.Items.Take(10).Select(d=>Entry(d,false)).ToList(),hasMore=result.Items.Count>10,nextOffset=10,
                detail=result.Items.Where(d=>d.Mail.id==result.MailId).Select(d=>Entry(d,true)).FirstOrDefault(),
                itemQuantities=result.ItemQuantities.Select(p=>new PlayerItemMessage {itemId=p.Key,quantity=p.Value}).ToList() };
        }
        public ResMailDelete HandlerDeleteMail(
            IdSession session,
            ReqMailDelete request,
            long playerId)
        {
            var validation = ValidateRequest(session, playerId,
                request != null && request.mailId > 0);
            if (validation.Status != Respones.OK)
            {
                return new ResMailDelete
                {
                    status = (int)validation.Status,
                    errorMessage = validation.ErrorMessage
                };
            }

            MailResult result = GM_MailMsgMgr.Instance.Execute(playerId, MailAction.Delete, request.mailId);
            return new ResMailDelete {status=Code(result),errorMessage=result.Error ?? result.Status.ToString(),mailboxVersion=result.MailboxVersion,
                serverTime=result.ServerTime,count=result.Count,unreadCount=result.UnreadCount,capacity=result.Capacity,mailId=result.MailId,
                items=result.Items.Take(10).Select(d=>Entry(d,false)).ToList(),hasMore=result.Items.Count>10,nextOffset=10,
                detail=result.Items.Where(d=>d.Mail.id==result.MailId).Select(d=>Entry(d,true)).FirstOrDefault(),
                itemQuantities=result.ItemQuantities.Select(p=>new PlayerItemMessage {itemId=p.Key,quantity=p.Value}).ToList() };
        }
        public ResMailDeleteRead HandlerDeleteReadMail(
            IdSession session,
            ReqMailDeleteRead request,
            long playerId)
        {
            var validation = ValidateRequest(session, playerId,
                request != null);
            if (validation.Status != Respones.OK)
            {
                return new ResMailDeleteRead
                {
                    status = (int)validation.Status,
                    errorMessage = validation.ErrorMessage
                };
            }

            MailResult result = GM_MailMsgMgr.Instance.Execute(playerId, MailAction.DeleteRead, 0);
            return new ResMailDeleteRead {status=Code(result),errorMessage=result.Error ?? result.Status.ToString(),mailboxVersion=result.MailboxVersion,
                serverTime=result.ServerTime,count=result.Count,unreadCount=result.UnreadCount,capacity=result.Capacity,mailId=result.MailId,
                items=result.Items.Take(10).Select(d=>Entry(d,false)).ToList(),hasMore=result.Items.Count>10,nextOffset=10,
                detail=result.Items.Where(d=>d.Mail.id==result.MailId).Select(d=>Entry(d,true)).FirstOrDefault(),
                itemQuantities=result.ItemQuantities.Select(p=>new PlayerItemMessage {itemId=p.Key,quantity=p.Value}).ToList() };
        }
        public ResPullingMailMsg HandlerReqPullingMailMsg(
            IdSession session,
            ReqPullingMailMsg request,
            long playerId)
        {
            var validation = ValidateRequest(session, playerId,
                request != null);
            if (validation.Status != Respones.OK)
            {
                return new ResPullingMailMsg
                {
                    status = (int)validation.Status
                };
            }

            MailResult result = GM_MailMsgMgr.Instance.Execute(playerId, MailAction.List, 0);
            return new ResPullingMailMsg { status=Code(result),mailMessages=result.Items.Select(d=>new MailMsgItem
                {msgId=d.Mail.id,msgBody=d.Mail.msgBody,sendTime=checked((int)d.Mail.sendTime),status=d.Mail.readTime>0?1:0}).ToList() };
        }
        public ResUpdateMailMsg HandlerReqUpdateMailMsg(
            IdSession session,
            ReqUpdateMailMsg request,
            long playerId)
        {
            var validation = ValidateRequest(session, playerId,
                request != null && request.mailMsgId > 0 && request.status is (1 or 2));
            if (validation.Status != Respones.OK)
            {
                return new ResUpdateMailMsg
                {
                    status = (int)validation.Status
                };
            }

            MailResult result = GM_MailMsgMgr.Instance.Execute(
                playerId, request.status == 1 ? MailAction.Read : MailAction.Delete, request.mailMsgId);
            return new ResUpdateMailMsg {status=Code(result)};
        }
    }
}

