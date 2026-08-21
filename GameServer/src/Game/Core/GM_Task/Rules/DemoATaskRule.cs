using Game.Datas.GMEntities;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.GM_Task
{

    [TaskType(200000)]
    [RuleModule((int)RuleType.Task, 200000)]
    class DemoATaskRule
    {
        [RuleProcesser("TaskDataEncoder", -1)]
        public static byte[] DefaultEncodeTaskData(object taskData)
        {
            return null;
        }


        [RuleProcesser("TaskDataDecoder", -1)]
        public static void DefaultDecoderTaskData(GM_Task task)
        {
        }

        [RuleProcesser("TaskIsStarted", -1)]
        public static GM_Task DefaultCheckIfStartTask(GM_PlayerEntity entity, int taskLineType, int nextTaskId)
        {
            return null;
        }

        [RuleProcesser("TaskIsCompleted", -1)]
        public static bool DefaultCheckIsCompleted(GM_PlayerEntity entity, GM_Task task)
        {


            return false;
        }

        [RuleProcesser("TaskOnCompleted", -1)]
        public static void DefaultDoTaskCompleted(GM_PlayerEntity entity, GM_Task task)
        {


        }

        [RuleProcesser("UpdateProgress", -1)]
        public static void DefaultTaskUpdateProgress(GM_Task curTask, string key, object value)
        {

        }
    }
}
