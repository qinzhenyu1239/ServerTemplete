

using Framework.Core.Utils;
using Game.Core.Db;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

/*
 * Gametask status的说明: // 0, 未开启, 1,正在进行中, 2结束完成了，3,结束已经处理了结束流程
 */
namespace Game.Core.GM_Task {

    public enum TaskStatus { 
        Invalid = -1,
        UnOpended = 0,
        Starting = 1,
        Complete = 2,
        EndComplete = 3,
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class TaskType : Attribute
    {
        public int mainType;

        public TaskType(int mainType)
        {
            this.mainType = mainType;
        }
    }

    public class GM_Task {
        public Gametask dbTaskInst;
        public string taskDesic = null;
        public object taskProgressData = null; // 不同的规则，对象是不一样的

        public static GM_Task Create(long uid, int tid) {
            GM_Task task = new GM_Task();
            task.dbTaskInst = new Gametask();
            task.dbTaskInst.id = IdGenerator.GetNextId();
            task.dbTaskInst.endTime = -1;
            task.dbTaskInst.startTime = (int)UtilsHelper.Timestamp();
            task.dbTaskInst.tid = tid;
            task.dbTaskInst.uid = uid;
            task.dbTaskInst.status = (int)TaskStatus.Starting;


            return task;
        }
    }

    public class GM_TaskLine {
        public int taskLineType; // 这里就是我们的任务线的ID号;

        // GM_Task curTask; // 一个任务线只允许一个任务实例;
        public List<GM_Task> taskSet = new List<GM_Task>(); // 当前任务线上的所有任务;
    }

    public class GM_TaskMgr {
        public static GM_TaskMgr Instance = new GM_TaskMgr();

        private List<int> taskTypes = new List<int>();

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public void Init() {
            IEnumerable<Type> taskTypes = TypeScanner.ListTypesWithAttribute(typeof(TaskType));
            foreach (Type taskType in taskTypes)
            {
                TaskType taskTypeAttr = taskType.GetCustomAttribute<TaskType>();
                this.taskTypes.Add(taskTypeAttr.mainType);
            }

        }

        private void LoadTaskFromDb(GM_PlayerEntity entity, int taskLineType, Gametask[] taskSet) {
           
            if (taskSet == null || taskSet.Length <= 0) {
                return;
            }


            GM_TaskLine taskLine = new GM_TaskLine();
            taskLine.taskLineType = taskLineType;
            taskLine.taskSet = new List<GM_Task>();

            for (int i = 0; i < taskSet.Length; i++) {
                if (taskSet[i].tid < taskLineType || taskSet[i].tid > taskLineType + 99999) {
                    continue;
                }

                // 正在进行中的，还有未开启的;
                if (taskSet[i].status != (int)TaskStatus.UnOpended && 
                    taskSet[i].status != (int)TaskStatus.Starting) {
                    continue;
                }

                GM_Task task = new GM_Task();
                task.dbTaskInst = taskSet[i];
                task.taskProgressData = null; // 实际是要根据每个类型的解析函数来做;
                task.taskDesic = "";

                
                MethodInfo method = RuleProcesserFactory.GetProcesser((int)RuleType.Task, "TaskDataDecoder", taskSet[i].tid, (taskSet[i].tid / 100000) * 100000);
                if (method != null) {
                    method.Invoke(null, new object[] { task });
                }

                taskLine.taskSet.Add(task);
            }

            entity.uTask.taskLineSet.TryAdd(taskLineType, taskLine);
        }

        private void LoadNextTask(GM_PlayerEntity entity, int taskLineType, int nextTaskId) {
            GM_TaskLine taskLine = null;


            entity.uTask.taskLineSet.TryGetValue((long)taskLineType, out taskLine);
            // end
            
            MethodInfo method = null;
            method = RuleProcesserFactory.GetProcesser((int)RuleType.Task, "TaskIsStarted", taskLineType, (taskLineType / 100000) * 100000);
            if (method == null)
            {
                this.logger.Error($"TaskIsStartedFuncs key is null: {taskLineType}");
                return;
            }

            GM_Task nextTask = (GM_Task)method.Invoke(null, new object[] { entity, taskLineType, nextTaskId });
            if (nextTask != null)
            {
                if (taskLine == null)
                {
                    taskLine = new GM_TaskLine();
                    taskLine.taskLineType = taskLineType;
                    entity.uTask.taskLineSet.TryAdd(taskLineType, taskLine);
                }

                taskLine.taskSet.Add(nextTask);

                // 更新到数据库
                this.UpdateOrInsertTaskToDb(entity, nextTask, true);
                // end
            }
        }

        public void CheckAndStartTask(GM_PlayerEntity entity, int taskLineType, Gametask[] taskSet) {
            // 判断一下当前是否有正在开启的任务，如果有，我们直接返回;  一个任务线只允许一个任务,具体你可以根据业务逻辑来修改;
            
            int nextTaskId = taskLineType; // 从baseId开始

            for (int i = 0; i < taskSet.Length; i++)
            {
                if (taskSet[i].tid < taskLineType || taskSet[i].tid > (taskLineType + 99999)) {
                    continue;
                }

                if (taskSet[i].status == (int)TaskStatus.Starting)
                {
                    return;
                }
                else if (taskSet[i].status > (int)TaskStatus.Starting) {
                    nextTaskId = taskSet[i].tid;
                }
                
            }
            // end
            nextTaskId ++;

            this.LoadNextTask(entity, taskLineType, nextTaskId);
        }

        public void LoadTasksFormDb(GM_PlayerEntity entity) {
            Gametask[] taskSet = DBService.Instance.GetGameInstance().Queryable<Gametask>().Where(it => (it.uid == entity.uPlayer.playerInfo.id)).ToArray();
            for (int i = 0; i < this.taskTypes.Count; i++) {
                this.LoadTaskFromDb(entity, this.taskTypes[i], taskSet);
                this.CheckAndStartTask(entity, this.taskTypes[i], taskSet);
            }
        }

        public GM_Task GetCurrectTaskData(GM_PlayerEntity entity, int taskLineType) {
            GM_TaskLine taskLine = null;
            entity.uTask.taskLineSet.TryGetValue(taskLineType, out taskLine);
            if (taskLine == null) {
                return null;
            }

            for (int i = 0; i < taskLine.taskSet.Count; i++) {
                if (taskLine.taskSet[i].dbTaskInst.status == (int)TaskStatus.Starting) {
                    return taskLine.taskSet[i];
                }
            }

            return null;
        }

        public void UpdateTaskProgress(GM_PlayerEntity entity, GM_Task curTask, string key, object value) {
            MethodInfo method = null; 
            method = RuleProcesserFactory.GetProcesser((int)RuleType.Task, "UpdateProgress", curTask.dbTaskInst.tid, (curTask.dbTaskInst.tid / 100000) * 100000);
            if (method != null) {
                method.Invoke(null, new object[] { curTask, key, value});
            }

            // 判断任务是否达到完成条件
            if (!this.CheckTaskIsCompleted(entity, curTask)) {
                GM_TaskMgr.Instance.UpdateOrInsertTaskToDb(entity, curTask);
            }
            // end
        }

        public void UpdateOrInsertTaskToDb(GM_PlayerEntity entity, GM_Task curTask, bool isInsert = false) {

            MethodInfo method = null;
            method = RuleProcesserFactory.GetProcesser((int)RuleType.Task, "TaskDataEncoder", curTask.dbTaskInst.tid, (curTask.dbTaskInst.tid / 100000) * 100000);
            
            if (method != null)
            {
                curTask.dbTaskInst.TaskData = (byte[])method.Invoke(null, new object[] { curTask.taskProgressData });
                if (isInsert) {
                    InsertToDb(curTask.dbTaskInst);
                }
                else {
                    UpdateToDb(curTask.dbTaskInst);
                }
                
            }
            else {
                this.logger.Error($"TaskMgr: Unknow Encoder Type: {curTask.dbTaskInst.tid}");
            }
        }

        private void UpdateToDb(Gametask dbInst) {
            DBService.Instance.GetGameInstance().Updateable(dbInst).Where(it => it.id == dbInst.id).ExecuteCommandAsync();
        }

        private void InsertToDb(Gametask dbInst)
        {
            DBService.Instance.GetGameInstance().Insertable(dbInst).ExecuteCommandAsync();
        }

        public bool CheckTaskIsCompleted(GM_PlayerEntity player, GM_Task curTask) {

            MethodInfo method = null;
            method = RuleProcesserFactory.GetProcesser((int)RuleType.Task, "TaskIsCompleted", curTask.dbTaskInst.tid, (curTask.dbTaskInst.tid / 100000) * 100000);
            if (method != null) {
                if ((bool)(method.Invoke(null, new object[] { player, curTask })))
                {
                    // 有任务完成了;
                    curTask.dbTaskInst.status = (int)TaskStatus.Complete;
                    this.DoTaskCompletedAction(player, curTask);
                    // end

                    return true;
                }
            }
            else
            {
                this.logger.Error($"TaskIsCompletedFuncs key is null: {curTask.dbTaskInst.tid}");
            }
            // end
            return false;
        }

        private void DoTaskCompletedAction(GM_PlayerEntity player, GM_Task curTask) {
            MethodInfo method = null;
            
            method = RuleProcesserFactory.GetProcesser((int)RuleType.Task, "TaskOnCompleted", curTask.dbTaskInst.tid, (curTask.dbTaskInst.tid / 100000) * 100000);
            if (method != null) {
                method.Invoke(null, new object[] { player, curTask });
            }
            else {
                this.logger.Error($"DoTaskCompletedAction key is null: {curTask.dbTaskInst.tid}");
            }

            // 更新数据库的状态;
            curTask.dbTaskInst.status = (int)TaskStatus.EndComplete;
            this.UpdateToDb(curTask.dbTaskInst);

            // 尝试加载下一个任务
            int defaultKey = (curTask.dbTaskInst.tid / 100000) * 100000;
            this.LoadNextTask(player, defaultKey, curTask.dbTaskInst.tid + 1);
            // end
        }

        public List<GM_Task> PullingTaskList(GM_PlayerEntity entity, int maskTypeId)
        {
            List<GM_Task> tasks = new List<GM_Task>();
            for (int i = 0; i < this.taskTypes.Count; i++) {
                GM_TaskLine taskLine = null;
                entity.uTask.taskLineSet.TryGetValue(this.taskTypes[i], out taskLine);
                if (taskLine == null) {
                    continue;
                }

                for (int j = 0; j < taskLine.taskSet.Count; j++) {
                    if (taskLine.taskSet[j].dbTaskInst.status != (int)TaskStatus.Starting &&
                        taskLine.taskSet[j].dbTaskInst.status != (int)TaskStatus.UnOpended) {
                        continue;
                    }

                    tasks.Add(taskLine.taskSet[j]);
                }
            }

            return tasks;
        }
    }
}

