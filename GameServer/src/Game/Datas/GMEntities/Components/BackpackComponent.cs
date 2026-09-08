using System.Collections.Generic;
using Game.Core.GM_Backpack;

namespace Game.Datas.GMEntities
{

    public class PackItem {
        public Game.Datas.DBEntities.Gamegoods goods = null;
        public object strengData = null; // 如果有强化数据 protobuf 解码以后的对象，可以参考任务系统;
    }

    public struct BackpackComponent
    {
        // 注意线程安全, 推到后期;
        public Dictionary<int, List<PackItem>> packItems; // 物品ID===》物品数据对象  20001===>[20001实例1(强化数据1), 20001实例2(强化数据2)];


        public static void Init(GM_PlayerEntity entity)
        {
            GM_BackpackMgr.Instance.LoadBackpackComponent(entity);
        }

    }

}
