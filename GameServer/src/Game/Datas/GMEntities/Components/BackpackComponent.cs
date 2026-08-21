using Game.Core.Db;
using System.Collections.Generic;

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


        private static Game.Datas.DBEntities.Gamegoods[]  LoadDataFromDb(long playerId) {
            return DBService.Instance.GetGameInstance().Queryable<Game.Datas.DBEntities.Gamegoods>().Where(it => it.uid == playerId).ToArray();
        }

        public static void Init(GM_PlayerEntity entity)
        {
            NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

            entity.uBackpack.packItems = new Dictionary<int, List<PackItem>>();

            Game.Datas.DBEntities.Gamegoods[] gameGoods = LoadDataFromDb(entity.uPlayer.playerInfo.id);
            if (gameGoods == null || gameGoods.Length <= 0) {
                return;
            }

            for (int i = 0; i < gameGoods.Length; i++) {
                PackItem item = new PackItem();
                item.goods = gameGoods[i];
                item.strengData = null; // 通过规则模块(StrenRules)来编码解码我们的强化数据，参考任务系统
                List<PackItem> goodsSet = null;
                // 如果有强化数据，你可能还要考虑强化数据是否要作为key;
                if (entity.uBackpack.packItems.ContainsKey(gameGoods[i].tid)) {
                    goodsSet = entity.uBackpack.packItems[gameGoods[i].tid];
                    goodsSet.Add(item);
                }
                else {
                    goodsSet = new List<PackItem>();
                    entity.uBackpack.packItems.Add(gameGoods[i].tid, goodsSet);
                    goodsSet.Add(item);
                }
            }
        }

    }

}