using Framework.Core.Utils;
using Game.Datas.DBEntities;
using Game.Datas.Excels;
using Game.Datas.GMEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using System.IO;
using System.Reflection;
using Game.Utils;
using Game.Core.Caches;

namespace Game.Core.GM_Task
{
    [ProtoContract]
    class CollectTaskProgressData {
        [ProtoMember(1, IsRequired = true)]
        public int damond = 0;

        [ProtoMember(2, IsRequired = true)]
        public int book = 0;

        // 多个条件的key, value;
        public Dictionary<string, int> conds = new Dictionary<string, int>();
        public Dictionary<string, int> completeBonues = new Dictionary<string, int>();
    }

    [TaskType(100000)]
    [RuleModule((int)RuleType.Task, 100000)]  //  任务线的ID号; 
    public class CollectTaskRule 
    {
        
        [RuleProcesser("TaskDataEncoder", -1)]
        public static byte[] DefaultEncodeTaskData(object taskData) {
            return GameUtils.PbObjToBytes<CollectTaskProgressData>(taskData as CollectTaskProgressData);
        }


        [RuleProcesser("TaskDataDecoder", -1)]
        public static void DefaultDecoderTaskData(GM_Task task) {
            
            task.taskProgressData = GameUtils.BytesToPbObj<CollectTaskProgressData>(task.dbTaskInst.TaskData);
            
            CollectTask config = (CollectTask) ExcelUtils.GetConfigData<CollectTask>(task.dbTaskInst.tid.ToString());
            if (config != null) {
                task.taskDesic = config.desicFormat;
            }

            CollectTaskProgressData data = (CollectTaskProgressData)task.taskProgressData;
            data.conds = GameUtils.ParseStringWithKeyIntValue(config.DoneCond);
            data.completeBonues = GameUtils.ParseStringWithKeyIntValue(config.Bonues);
        }

        [RuleProcesser("UpdateProgress", -1)]
        public static void DefaultTaskUpdateProgress(GM_Task curTask, string key, object value) {
            CollectTaskProgressData item = (CollectTaskProgressData) curTask.taskProgressData;
            int lastValue = (int)GameUtils.GetFiled(item, key, 0);
            lastValue += ((int)value);
            GameUtils.SetFiled(item, key, lastValue);
        }

        [RuleProcesser("TaskIsStarted", -1)]
        public static GM_Task DefaultCheckIfStartTask(GM_PlayerEntity entity, int taskLineType, int nextTaskId) {
            CollectTask config = (CollectTask)ExcelUtils.GetConfigData<CollectTask>(nextTaskId.ToString());
            if (config == null) {
                return null;
            }

            // 因为我们是自动开启auto
            // end

            GM_Task nextTask = GM_Task.Create(entity.uPlayer.playerInfo.id, nextTaskId);
            
            nextTask.taskDesic = config.desicFormat;
            CollectTaskProgressData item = new CollectTaskProgressData();
            item.conds = GameUtils.ParseStringWithKeyIntValue(config.DoneCond);
            item.completeBonues = GameUtils.ParseStringWithKeyIntValue(config.Bonues);
            nextTask.taskProgressData = item;

            return nextTask;
        }

        [RuleProcesser("TaskIsCompleted", -1)]
        public static bool DefaultCheckIsCompleted(GM_PlayerEntity entity, GM_Task curTask) {
            CollectTask config = (CollectTask)ExcelUtils.GetConfigData<CollectTask>(curTask.dbTaskInst.tid.ToString());
            if (config == null) {
                return false;
            }

            CollectTaskProgressData item = (CollectTaskProgressData)curTask.taskProgressData;

            foreach (var key in item.conds.Keys) {
                int value = (int) GameUtils.GetFiled(item, key, 0);
                if (value < item.conds[key]) {
                    return false;
                }
            }
            
            return true;
        }

        [RuleProcesser("TaskOnCompleted", -1)]
        public static void DefaultDoTaskCompleted(GM_PlayerEntity entity, GM_Task task) {
            CollectTaskProgressData item = (CollectTaskProgressData)task.taskProgressData;
            // 你可以根据自己的游戏来写规则，奖励
            foreach(var key in item.completeBonues.Keys)
            {
                int value = (int)GameUtils.GetProp(entity.uPlayer.playerInfo, key, 0);
                value += item.completeBonues[key];
                GameUtils.SetProp(entity.uPlayer.playerInfo, key, value);
            }

            PlayerIDCache.Instance.UpdateDataToDb(entity.uPlayer.playerInfo);
            // end
        }
    }
}
