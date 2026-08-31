using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///玩家角色抽奖记录
    ///</summary>
    public partial class Player_gacha_record
    {
           public Player_gacha_record(){


           }
           /// <summary>
           /// Desc:抽奖记录ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long id {get;set;}

           /// <summary>
           /// Desc:抽奖批次ID，十连抽共用同一个批次ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long batch_id {get;set;}

           /// <summary>
           /// Desc:本批次中的第几抽，从1开始
           /// Default:1
           /// Nullable:False
           /// </summary>           
           public byte draw_index {get;set;}

           /// <summary>
           /// Desc:玩家ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long player_id {get;set;}

           /// <summary>
           /// Desc:卡池ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public int pool_id {get;set;}

           /// <summary>
           /// Desc:抽到的角色配置ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public int character_id {get;set;}

           /// <summary>
           /// Desc:抽取时的角色稀有度
           /// Default:1
           /// Nullable:False
           /// </summary>           
           public byte rarity {get;set;}

           /// <summary>
           /// Desc:是否首次获得角色：0否，1是
           /// Default:true
           /// Nullable:False
           /// </summary>           
           public bool is_new_character {get;set;}

           /// <summary>
           /// Desc:重复角色转换的碎片道具ID，首次获得填0
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int fragment_item_id {get;set;}

           /// <summary>
           /// Desc:转换得到的碎片数量
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int fragment_num {get;set;}

           /// <summary>
           /// Desc:本次抽取后的小保底累计次数
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int small_pity_count {get;set;}

           /// <summary>
           /// Desc:本次抽取后的大保底累计次数
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int big_pity_count {get;set;}

           /// <summary>
           /// Desc:抽奖时间
           /// Default:CURRENT_TIMESTAMP
           /// Nullable:False
           /// </summary>           
           public DateTime created_at {get;set;}

    }
}
