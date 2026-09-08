using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///玩家各卡池的抽奖保底状态
    ///</summary>
    public partial class Player_gacha_state
    {
           public Player_gacha_state(){


           }
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
           /// Desc:距离上次获得小保底稀有度角色后的抽取次数
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int small_pity_count {get;set;}

           /// <summary>
           /// Desc:距离上次获得大保底稀有度角色后的抽取次数
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int big_pity_count {get;set;}

           /// <summary>
           /// Desc:该玩家在该卡池的累计抽取次数
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public long total_draw_count {get;set;}

    }
}
