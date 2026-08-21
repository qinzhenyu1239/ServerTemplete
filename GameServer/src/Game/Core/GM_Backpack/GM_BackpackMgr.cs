using Game.Core.Db;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.GM_Backpack
{
    public class GM_BackpackMgr
    {
        public static GM_BackpackMgr Instance = new GM_BackpackMgr();

        private NLog.Logger logger = null;

        
        public void Init() {
            this.logger = NLog.LogManager.GetCurrentClassLogger();
        }

        private int MainType(int tid) {
            return (tid / 10000) * 10000;
        }

        public Dictionary<int, List<GoodsItem>> GetBackpackData(ref BackpackComponent backpack) {
            // 药水类: 30000  ===> [物品1， 物品2， 物品3]
            var ret = new Dictionary<int, List<GoodsItem>>();
            foreach (var k in backpack.packItems.Keys) {
                List<PackItem> items = backpack.packItems[k];
                for (int i = 0; i < items.Count; i++) {
                    PackItem item = items[i];

                    int mainType = this.MainType(item.goods.tid);
                    GoodsItem goods = new GoodsItem();
                    goods.num = item.goods.num;
                    goods.typeId = item.goods.tid;
                    goods.strengData = item.goods.strengData;

                    List<GoodsItem> goodsSet = null;
                    if (ret.ContainsKey(mainType)) {
                        goodsSet = ret[mainType];
                    }
                    else {
                        goodsSet = new List<GoodsItem>();
                        ret.Add(mainType, goodsSet);
                    }
                    goodsSet.Add(goods);
                }
            }
            return ret;
        }

        private bool AddGoodsWithTid(ref BackpackComponent backpack, int tid, int num, long playerId) {
            if (backpack.packItems.ContainsKey(tid)) {
                // 
                List<PackItem> items = backpack.packItems[tid];
                if (items.Count > 1)
                {
                    this.logger.Error($"tid:{tid} has multi PackItem");
                    return false;
                }
                // end

                items[0].goods.num += num; // 线程安全

                // 更新到数据库
                DBService.Instance.GetGameInstance().Updateable<Game.Datas.DBEntities.Gamegoods>(items[0].goods).Where(it => it.id == items[0].goods.id).ExecuteCommandAsync();
                // end
            }
            else { // tid不存在;
                List<PackItem> items = new List<PackItem>();

                backpack.packItems.Add(tid, items);
                PackItem item = new PackItem();
                item.goods = new Game.Datas.DBEntities.Gamegoods();
                item.goods.tid = tid;
                item.goods.num = num;
                item.goods.uid = playerId;
                item.strengData = null;
                items.Add(item);

                // 更新到数据库
                DBService.Instance.GetGameInstance().Insertable<Game.Datas.DBEntities.Gamegoods>(item.goods).ExecuteCommandAsync();
                // end
            }
            return true;
        }

        private bool DecGoodsWithTid(ref BackpackComponent backpack, int tid, int num, long playerId) {
            if (backpack.packItems.ContainsKey(tid))
            {
                // 
                List<PackItem> items = backpack.packItems[tid];
                if (items.Count > 1)
                {
                    this.logger.Error($"tid:{tid} has multi PackItem");
                    return false;
                }
                // end

                items[0].goods.num -= num; // 线程安全

                // 更新到数据库
                DBService.Instance.GetGameInstance().Updateable<Game.Datas.DBEntities.Gamegoods>(items[0].goods).Where(it => it.id == items[0].goods.id).ExecuteCommandAsync();
                // end
            }

            return false;
        }

        public bool UpdateGoodsWithTid(GM_PlayerEntity entity, int tid, int num) {
            // 验证tid的合法性
            // 验证我们num的合法性;
            if (num > 0) { // 增加物品;
                return this.AddGoodsWithTid(ref entity.uBackpack, tid, num, entity.uPlayer.playerInfo.id);
            }
            else { // 消耗物品 
                return DecGoodsWithTid(ref entity.uBackpack, tid, -num, entity.uPlayer.playerInfo.id);
            }
        }

        public bool DoGoodsActionWithTid(GM_PlayerEntity entity, int tid, string actionName, object param) {
            if (entity.uBackpack.packItems.ContainsKey(tid))
            {
                // 
                List<PackItem> items = entity.uBackpack.packItems[tid];
                if (items.Count > 1)
                {
                    this.logger.Error($"tid:{tid} has multi PackItem");
                    return false;
                }
                // end

                MethodInfo m = RuleProcesserFactory.GetProcesser((int)RuleType.Backpack, actionName, tid, this.MainType(tid));
                if (m == null) {
                    this.logger.Error($"tid:{tid} has action: {actionName} func is null");
                    return false;
                }

                return (bool)(m.Invoke(null, new object[] { entity, items[0], param }));

            }

            return false;
        }
    }
}
