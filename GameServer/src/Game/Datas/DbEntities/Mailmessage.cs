using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///邮件消息管理
    ///</summary>
    public partial class Mailmessage
    {
           public Mailmessage(){


           }
           /// <summary>
           /// Desc:邮件消息的唯一ID号
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long id {get;set;}

           /// <summary>
           /// Desc:来自与哪个玩家Id发送的邮件
           /// Default:-1
           /// Nullable:False
           /// </summary>           
           public long fromPlayerId {get;set;}

           /// <summary>
           /// Desc:发送给哪个玩家Id的邮件
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public long toPlayerId {get;set;}

           /// <summary>
           /// Desc:邮件消息主题内容
           /// Default:
           /// Nullable:False
           /// </summary>           
           public string msgBody {get;set;}

           /// <summary>
           /// Desc:当前邮件消息的状态，0，表示未读， 1表示已读，2表示已删除;
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int status {get;set;}

           /// <summary>
           /// Desc:邮件消息发送的时间
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int sendTime {get;set;}

           /// <summary>
           /// Desc:消息邮件要关联的一些用户数据，具体可以根据游戏项目来定,暂定用BigInt,带任务ID，奖励ID等
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public long userData {get;set;}

           /// <summary>
           /// Desc:邮件消息被玩家阅读的时间
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int readTime {get;set;}

    }
}
