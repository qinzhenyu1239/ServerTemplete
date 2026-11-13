using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///
    ///</summary>
    [SqlSugar.SugarTable("player")]
    public partial class Player
    {
           public Player(){


           }
           /// <summary>
           /// Desc:playerId
           /// Default:10000
           /// Nullable:False
           /// </summary>           
           public long id {get;set;}

           /// <summary>
           /// Desc:账号ID
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public long accountId {get;set;}

           /// <summary>
           /// Desc:用户名字
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string name {get;set;}

           /// <summary>
           /// Desc:头像ID，0表示未设置
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int avatarId {get;set;}

           /// <summary>
           /// Desc:创建角色Unix秒时间戳，0表示未知
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public long createTime {get;set;}

           /// <summary>
           /// Desc:账号的状态
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int status {get;set;}

           /// <summary>基地等级。</summary>
           public int baseLevel {get;set;} = 1;

           /// <summary>主线关卡章节编号。</summary>
           public int mainStage {get;set;} = 1;

           /// <summary>共鸣等级。</summary>
           public int resonanceLevel {get;set;} = 0;

           /// <summary>爬塔关卡。</summary>
           public int towerStage {get;set;} = 0;

    }
}
