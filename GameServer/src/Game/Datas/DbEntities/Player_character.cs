using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///玩家拥有的角色
    ///</summary>
    public partial class Player_character
    {
           public Player_character(){


           }
           /// <summary>
           /// Desc:角色实例ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long id {get;set;}

           /// <summary>
           /// Desc:玩家ID，对应player.id
           /// Default:
           /// Nullable:False
           /// </summary>           
           public long player_id {get;set;}

           /// <summary>
           /// Desc:角色配置ID
           /// Default:
           /// Nullable:False
           /// </summary>           
           public int character_id {get;set;}

           /// <summary>
           /// Desc:角色星级
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public byte star {get;set;}

    }
}
