using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///存放我们的玩家信息
    ///</summary>
    public partial class Account
    {
           public Account(){


           }
           /// <summary>
           /// Desc:玩家唯一的UID号
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long uid {get;set;}

           /// <summary>
           /// Desc:玩家的昵称
           /// Default:
           /// Nullable:False
           /// </summary>           
           public string unick {get;set;}

           /// <summary>
           /// Desc:0:男, 1:女的
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int usex {get;set;}

           /// <summary>
           /// Desc:系统默认图像，自定义图像后面再加上
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int uface {get;set;}

           /// <summary>
           /// Desc:玩家的账号名称
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string uname {get;set;}

           /// <summary>
           /// Desc:玩家密码的MD5值
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string upwd {get;set;}

           /// <summary>
           /// Desc:玩家的电话
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string phone {get;set;}

           /// <summary>
           /// Desc:游客账号的唯一的key
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public string guest_key {get;set;}

           /// <summary>
           /// Desc:玩家的email
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string email {get;set;}

           /// <summary>
           /// Desc:玩家的地址
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string address {get;set;}

           /// <summary>
           /// Desc:玩家VIP的等级，这个是最普通的
           /// Default:00000000
           /// Nullable:False
           /// </summary>           
           public int uvip {get;set;}

           /// <summary>
           /// Desc:玩家VIP到期的时间撮
           /// Default:00000000000000000000000000000000
           /// Nullable:False
           /// </summary>           
           public int vip_end_time {get;set;}

           /// <summary>
           /// Desc:标志改账号是否为游客账号
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int is_guest {get;set;}

           /// <summary>
           /// Desc:0正常，其他的根据需求来定
           /// Default:00000000
           /// Nullable:False
           /// </summary>           
           public int status {get;set;}

           /// <summary>
           /// Desc:玩家注册对应的渠道，微信，抖音等
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int uchannel {get;set;}

    }
}
