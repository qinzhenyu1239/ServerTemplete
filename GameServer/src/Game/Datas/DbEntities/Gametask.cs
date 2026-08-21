using System;
using System.Linq;
using System.Text;

namespace Game.Datas.DBEntities
{
    ///<summary>
    ///玩家当前的任务实例列表
    ///</summary>
    public partial class Gametask
    {
           public Gametask(){


           }
           /// <summary>
           /// Desc:任务实例ID号
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
           /// Desc:任务类型ID号
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int tid {get;set;}

           /// <summary>
           /// Desc:当前任务的状态,0未开启,1进行中，2任务完成,3任务取消等
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int status {get;set;}

           /// <summary>
           /// Desc:开始任务的时间戳
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int startTime {get;set;}

           /// <summary>
           /// Desc:任务结束的时间戳，-1长期有效
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public int endTime {get;set;}

           /// <summary>
           /// Desc:保存当前任务的数据进度，Protobuf编码
           /// Default:0
           /// Nullable:False
           /// </summary>           
           public byte[] TaskData {get;set;}

    }
}
