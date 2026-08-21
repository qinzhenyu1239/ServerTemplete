using Framework.Core.Utils;
using Game.Core.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.GM_MailMessage
{
    enum MailMsgStatus { 
        Unreaded = 0,
        Readed,
        Deleted,
    }

    public class GM_MailMsgMgr
    {
        public static GM_MailMsgMgr Instance = new GM_MailMsgMgr();

        public void Init() {

        }

        public void SendMailMsg(long fromPlayerId, long toPlayerId, string msgBody, long udata = 0) {
            Game.Datas.DBEntities.Mailmessage msg = new Game.Datas.DBEntities.Mailmessage();
            msg.fromPlayerId = fromPlayerId;
            msg.toPlayerId = toPlayerId;
            msg.msgBody = msgBody;
            msg.userData = udata;
            msg.status = (int)MailMsgStatus.Unreaded;
            msg.sendTime = (int)UtilsHelper.Timestamp();
            msg.readTime = -1;

            DBService.Instance.GetGameInstance().Insertable(msg).ExecuteCommandAsync();
        }

        public bool UpdateMailMsgStatus(long msgId, int status) {
            if (status < (int)MailMsgStatus.Unreaded ||
                status > (int)MailMsgStatus.Deleted) {
                return false;
            }

            Game.Datas.DBEntities.Mailmessage mailMsg = null;
            mailMsg = DBService.Instance.GetGameInstance().Queryable<Game.Datas.DBEntities.Mailmessage>().First(it => it.id == msgId);//查询单条
            if (mailMsg.status == status) {
                return true;
            }


            mailMsg.status = status;
            if (status == (int)MailMsgStatus.Readed) {
                mailMsg.readTime = (int)UtilsHelper.Timestamp();
            }
            DBService.Instance.GetGameInstance().Updateable<Game.Datas.DBEntities.Mailmessage>(mailMsg).Where(it => it.id == msgId).ExecuteCommandAsync();
            return true;
        }

        // statusMask 0: 拉取这个未读的邮件消息, 1: 拉取未读+已读的邮件 2: 拉取未读+已读+删除的邮件
        public Game.Datas.DBEntities.Mailmessage[] PullingMailMsg(long playerId, int statusMask) {
            Game.Datas.DBEntities.Mailmessage[] mailMsg = null;
            if (statusMask == 2)
            { // 所有邮件消息
                mailMsg = DBService.Instance.GetGameInstance().Queryable<Game.Datas.DBEntities.Mailmessage>().Where(it => it.toPlayerId == playerId).ToArray();//查询单条
            }
            else if (statusMask == 1)
            { // 拉取未读+已读的邮件
                mailMsg = DBService.Instance.GetGameInstance().Queryable<Game.Datas.DBEntities.Mailmessage>().Where(it => ((it.toPlayerId == playerId) && (it.status < 2))).ToArray();//查询单条
            }
            else if (statusMask == 0)
            {// 未读的邮件消息
                mailMsg = DBService.Instance.GetGameInstance().Queryable<Game.Datas.DBEntities.Mailmessage>().Where(it => ((it.toPlayerId == playerId) && (it.status < 1))).ToArray();//查询单条
            }
            return mailMsg;         
        }
    }
}
