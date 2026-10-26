using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///
    ///</summary>
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
           /// Desc:等级
           /// Default:1
           /// Nullable:False
           /// </summary>           
           public int level {get;set;}

           /// <summary>
           /// Desc:用户名字
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string name {get;set;}

           /// <summary>头像ID，0表示未设置。</summary>
           public int avatarId {get;set;}

           /// <summary>公会ID，0表示未加入公会。</summary>
           public long guildId {get;set;}

           /// <summary>称号ID，0表示未佩戴称号。</summary>
           public int titleId {get;set;}

           /// <summary>个性签名，最多255个字符。</summary>
           public string signature {get;set;} = string.Empty;

           /// <summary>创建角色的Unix秒时间戳，0表示历史创建时间未知。</summary>
           public long createTime {get;set;}

           public int avatarFrameId {get;set;}
           public int backgroundId {get;set;}
           public string displayAchievementIds {get;set;} = "[0,0,0,0,0,0,0]";
           public string displayCharacterIds {get;set;} = "[0,0,0,0,0,0]";

           /// <summary>
           /// Desc:
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int usex {get;set;}

           /// <summary>
           /// Desc:职业
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int job {get;set;}

           /// <summary>
           /// Desc:经验
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int exp {get;set;}

           /// <summary>
           /// Desc:玩家当前的HP
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int HP {get;set;}

           /// <summary>
           /// Desc:玩家当前的MP
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int MP {get;set;}

           /// <summary>
           /// Desc:装备情况
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string equipment {get;set;}

           /// <summary>
           /// Desc:玩家的金币,游戏，奖励，卖装备获取, 购买消耗品
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int ucoin {get;set;}

           /// <summary>
           /// Desc:玩家的元宝, 充值可以获得
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int umoney {get;set;}

           /// <summary>
           /// Desc:
           /// Default:
           /// Nullable:True
           /// </summary>           
           public long? lastDailyReset {get;set;}

           /// <summary>
           /// Desc:VIP特权
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string vipRightJson {get;set;}

           /// <summary>
           /// Desc:用户在哪个平台
           /// Default:
           /// Nullable:True
           /// </summary>           
           public string platform {get;set;}

           /// <summary>
           /// Desc:账号的状态
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int status {get;set;}

    }
}
