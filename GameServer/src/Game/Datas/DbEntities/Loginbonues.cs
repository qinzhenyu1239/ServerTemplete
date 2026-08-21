using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///登陆奖励管理
    ///</summary>
    public partial class Loginbonues
    {
           public Loginbonues(){


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
           /// Desc:奖励的数目
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int bonues {get;set;}

           /// <summary>
           /// Desc:是否已经领取, 0未领取，1已领取
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int status {get;set;}

           /// <summary>
           /// Desc:发放奖励的时间
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int bonues_time {get;set;}

           /// <summary>
           /// Desc:连续登陆天数
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int days {get;set;}

    }
}
