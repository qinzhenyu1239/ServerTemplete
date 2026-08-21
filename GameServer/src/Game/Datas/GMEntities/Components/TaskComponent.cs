using Game.Core.GM_Task;
using System.Collections.Concurrent;

namespace Game.Datas.GMEntities {
    public struct TaskComponent {

        public ConcurrentDictionary<long, GM_TaskLine> taskLineSet;

        public static void Init(GM_PlayerEntity entity) {
            entity.uTask.taskLineSet = new ConcurrentDictionary<long, GM_TaskLine>();
            GM_TaskMgr.Instance.LoadTasksFormDb(entity);            
        }

    }

}