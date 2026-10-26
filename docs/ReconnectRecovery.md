# 模块化重连恢复

客户端根目录：D:\iaowu\MiaoWu。
通用恢复代码在Assets/Scripts/HotUpdate/Game/Shared/Network。

SessionNetwork鉴权成功后进入Recovering，等待协调器完成恢复，再发布NetworkRecoveryCompletedEvent和NetworkConnectedEvent。
协调器只管理会话、取消和调度，不引用具体业务Manager。

## 扩展模块

在自己的模块目录实现无参数、非MonoBehaviour的IReconnectRecoveryHandler。
注册表首次使用时扫描接口所在的热更新程序集，自动发现实现；ModuleId必须非空且唯一。
实现类添加UnityEngine.Scripting.Preserve，保留反射构造能力。
不需要修改协调器、SessionNetwork或手动注册清单。

```csharp
[UnityEngine.Scripting.Preserve]
public sealed class ExampleRecoveryHandler : IReconnectRecoveryHandler
{
    public string ModuleId => "example";
    public IEnumerable<RecoveryTask> CollectTasks(RecoveryContext context)
    {
        // 先捕获本轮所需的操作和ID；不要在执行中重新读取待确认任务列表。
        yield return new RecoveryTask(RecoveryPhase.Refresh, 0, "overview", async token =>
        {
            var result = await ExampleManager.Singleton.LoadAsync(token);
            if (!result.Success) throw new NetworkRequestException(result.Error);
        });
    }
}
```

## 执行规则

所有模块的任务先收集为固定快照，之后按阶段、SortKey、ModuleId、OperationId排序串行执行。
三个阶段依次为EconomicOperation、StateSave、Refresh。
经济操作SortKey使用原创建时间戳，确保抽奖和升星跨模块保持时间顺序；同时间按模块标识和操作ID稳定排序。
任务必须返回可等待的UniTask，并明确检查业务结果；不通过普通事件广播或Forget发起恢复。

RecoveryContext固定账号、区服、取消令牌；每个任务前后校验会话。
注销、切换连接或断线取消当前恢复，拒绝并发启动第二轮恢复。
任务抛错立即停止后续任务，不发布恢复完成，由SessionNetwork现有重连失败流程处理。
新模块不能依赖打开UI才注册，待确认操作必须能直接从持久化记录加载。

## 当前模块

| 模块 | 恢复行为 |
| --- | --- |
| Gacha | 恢复待确认抽奖，再查询恢复后的最后卡池；无待确认操作则查询最后查看卡池 |
| Characters | 恢复待确认升星，再查询恢复后的最后角色；无待确认操作则查询最后查看角色 |
| Formation | 使用本轮快照恢复待确认保存；曾加载或有待确认保存时刷新全部编队 |
| PlayerProfile | 每次鉴权恢复重新查询资料，成功前禁止修改 |
| Inventory | 已经加载过时重新查询；尚未使用则保持按需加载，注销清空加载标记 |

新增恢复调度测试位于Assets/Tests/Editor/ReconnectRecoveryTests.cs，覆盖自动发现、重复标识、快照、
跨模块排序、取消、会话变化、失败传播以及无需修改协调器的新模块接入。
