using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///玩家当前的任务实例列表
    ///</summary>
    public partial class Gamegoods
    {
           public Gamegoods(){


           }
           /// <summary>
           /// Desc:物品实例ID号
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
           /// Desc:物品的类型ID，关联背包物品的配置表
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int tid {get;set;}

           /// <summary>
           /// Desc:背包物品的数目
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int num {get;set;}

           /// <summary>
           /// Desc:可选，不同游戏，可能不一样,保存当前装备的强化数据,可以考虑使用protobuf序列化
           /// Default:0
           /// Nullable:True
           /// </summary>           
           public byte[] strengData {get;set;}

    }
}
