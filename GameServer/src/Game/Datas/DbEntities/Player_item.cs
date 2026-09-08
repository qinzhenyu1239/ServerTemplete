using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///玩家道具背包
    ///</summary>
    public partial class Player_item
    {
           public Player_item(){


           }
           /// <summary>
           /// Desc:记录ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long id {get;set;}

           /// <summary>
           /// Desc:玩家ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long player_id {get;set;}

           /// <summary>
           /// Desc:Item配置表ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public int item_id {get;set;}

           /// <summary>
           /// Desc:拥有数量
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public long quantity {get;set;}

    }
}
