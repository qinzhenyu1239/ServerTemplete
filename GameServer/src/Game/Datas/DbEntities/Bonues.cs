using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///玩家奖励管理
    ///</summary>
    public partial class Bonues
    {
           public Bonues(){


           }
           /// <summary>
           /// Desc:领取奖励的唯一ID号
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long id {get;set;}

           /// <summary>
           /// Desc:用户的UID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long uid {get;set;}

           /// <summary>
           /// Desc:奖励对应的类型
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int tid {get;set;}

           /// <summary>
           /// Desc:是否已经领取, 0未领取，1已领取,2已过期
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int status {get;set;}

           /// <summary>
           /// Desc:发放奖励的时间戳
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int time {get;set;}

           /// <summary>
           /// Desc:endTime: 奖励结束领取的时间;
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int endTime {get;set;}

           /// <summary>
           /// Desc:发生奖励的原因
           /// Default:  
           /// Nullable:False
           /// </summary>           
           public string bonuesDesic {get;set;}

           /// <summary>
           /// Desc:第1个奖励数据
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int bonues1 {get;set;}

           /// <summary>
           /// Desc:第2个奖励数据
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int bonues2 {get;set;}

           /// <summary>
           /// Desc:第3个奖励数据
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int bonues3 {get;set;}

           /// <summary>
           /// Desc:第4个奖励数据
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int bonues4 {get;set;}

           /// <summary>
           /// Desc:第5个奖励数据
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int bonues5 {get;set;}

    }
}
