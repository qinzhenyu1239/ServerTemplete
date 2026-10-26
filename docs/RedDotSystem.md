# 通用红点系统

实现范围：服务端持久化业务叶子节点、完整配置同步、查看记录、增量推送、客户端父子汇总，当前没有接入任何实际业务。邮箱、任务和活动仅保留禁用的示例节点。

## 配置与数据

- 编辑 `D:/iaowu/MiaoWu/Assets/Luban/Template/Datas/RedDotConfig.xlsx`，使用现有 `tools/LubanExcelToCsv/Export-LubanExcelToCsv.ps1` 导出。工作簿副本位于 `Excels/RedDotConfig.xlsx`。
- RedDotConfig 的 Excel 格式与 ResonanceLv 一致：三行表头依次为 `##var`、`##type`、`##`，第四行开始为数据；字体、表头填色和边框沿用参考表。红点字段类型保留 int/string/bool/long。服务端 CSV 继续使用“中文说明、类型、字段名”三行表头。导出脚本兼容上述三行表头及原来的四行表头，不漏掉首条数据。
- 字段：`id:int`、`name:string`、`parentId:int`、`clearType:int`、`enabled:bool`、`tipVersion:long`。
- 类型 0 为汇总，1 为业务条件，2 为查看消除。业务节点只能是叶子；节点 ID 必须唯一、正数；父节点必须存在且为汇总节点；禁止循环。
- 全部示例节点默认禁用，包括 1000 邮箱入口和 1001 邮件未读；仅开启配置不能替代业务接入。业务条件节点可以直接调用 WriteBusinessState 更新，不强制注册判断方法。只有调用 RefreshBusinessState 时，才要求已通过 RegisterBusiness 注册对应方法，否则该次刷新报错。
- 查看型节点的 `tipVersion` 从 1 起递增，其他节点为 0。发布后不要复用 ID 或把已有节点改作不相关业务。
- `configVersion` 为按 ID 排序后的全部配置字段的 SHA-256，包括禁用节点。本期在启动时加载配置，不提供运行时修改入口。

数据库 `playerreddot` 使用 `(playerId,nodeId)` 联合主键，仅保存叶子，字段为 `isActive`、`seenVersion`、`updateTime`。父节点不保存到数据库，客户端从有效子节点汇总。禁用的父节点会禁用其整棵子树。

## 安装数据库

新建环境使用 `sql/ugame_10001.sql`，其中已同步加入红点表。已有环境在对应游戏数据库执行 `sql/migrations/20261023_001_reddot.sql` 后再启动更新后的游戏服。迁移兼容 MySQL 5.7，可重复执行，不会删除现有数据。

本次已撤回所有邮件接入：GM_MailMsgMgr、邮件实体及 PlayerModule 的邮件处理恢复为本功能实施前的代码。没有新增邮件有效期、容量清理、定时扫描或邮件红点写入。迁移脚本只创建 playerreddot，不依赖或修改 mailmessage。

本次没有对现有业务数据库执行迁移，数据库测试和启动验证均使用独立临时数据库。

## 协议与状态同步

沿用 PLAYER 模块 102，使用新命令 95–99：

| 命令 | 协议 | 作用 |
|---|---|---|
|95|ReqRedDotInit|登录、重连或发现版本不连续时请求初始化|
|96|ResRedDotInit|status、configVersion、stateVersion、完整 nodes、有效叶子 states|
|97|ReqRedDotView|nodeId、实际展示的 tipVersion|
|98|ResRedDotView|status、configVersion、stateVersion、完整有效叶子 states|
|99|NotifyRedDotChanged|configVersion、stateVersion、变化叶子 states；requestId=0|

节点消息包含配置六个字段，状态消息包含 nodeId 和 isActive。初始化发送禁用节点，无须客户端维护本地 Excel。stateVersion 随当前连接的叶子亮灭变化递增，重连后以新快照为准。

初始化接口只读取配置和已有玩家状态，不插入、更新记录，不锁数据库 player 行，也不调用业务判断方法。缺少记录的有效叶子按隐藏返回；重复请求优先读取当前连接的在线组件，首次请求或连接/配置版本变化时从数据库加载。修改配置提示版本本身不会自动点亮红点，必须由业务事件显式触发。

客户端在临时树上建立配置并应用快照，初始化期间暂存推送，按版本回放成功后一次刷新 UI。旧推送不覆盖新状态；缺失版本、配置不匹配触发全量同步。全量查看响应也按版本应用，防止旧响应恢复旧红点。登出、断线、重新鉴权时隐藏旧状态。

查看请求只接受已启用的查看型叶子，版本必须大于 0 且不超过当前版本。seenVersion 单调递增，旧查看请求不能清掉新版本。客户端须在实际展示内容后调用查看接口，不得通过打开父入口清除所有节点。

## 开发接入

职责链：Controller 解码和路由 → RedDotModule 校验及组装协议 → GM_RedDotMgr 管理事务、状态和推送 → 玩家 RedDotComponent / MySQL。各业务 Manager 提供自身条件；红点 Manager 不查询其他业务表。当前没有注册邮件业务提供方。

注册业务叶子示例：

```csharp
GM_RedDotMgr.Instance.RegisterBusiness(nodeId, (db, playerId) => EvaluateOwnBusiness(db, playerId));
```

业务变化时使用 PlayerOperationLock，同一事务先锁定 player 行，再修改业务状态、调用 `WriteBusinessState(db,playerId,nodeId,active)`，提交后调用 `PublishCommitted(playerId)`。不要在提交前改在线组件或发推送。幂等 upsert 允许无变化时影响行数为 0；业务条件更新需要检查对应影响行数。

如果已注册业务判断方法，可在业务事件事务中调用 `RefreshBusinessState(db,playerId,nodeId)` 重新判断并写入。查看型提示在业务生效时调用 `WriteViewedState(db,playerId,nodeId)`，再于提交后发布变化；该方法保留历史 seenVersion，只在玩家尚未看过当前版本时点亮。以上写入入口均不会在初始化查询中调用。

客户端实现位于 `Assets/Scripts/HotUpdate/Game/RedDot`。使用 `MiaoWu.Networking.RedDot.RedDotManager.Singleton.IsActive(nodeId)` 查询，查看型节点调用 `MarkViewedAsync(nodeId, displayedVersion)`。在内容展示时记录实际版本，提交时不要改用刚同步到的新版本。重连注册器自动在每次鉴权后初始化，管理器跨场景保留。

客户端 Models 只保存数据：RedDotNodeModel 保存节点字段，RedDotTreeModel 保存节点、亮灭状态、有效启用状态和版本。Models 不包含校验、版本处理、父子汇总、状态操作或 Changed 事件。RedDotTreeManager 是可独立测试的普通 Manager，负责这些树规则；RedDotManager 负责网络流程、会话生命周期和对外查询/通知。UI 订阅 RedDotManager.Changed，通过 Manager 查询状态，不再调用 Model 方法或订阅 Model 事件。

UI 入口挂载 `RedDotBinding`，设置 nodeId 与独立子对象 indicator；组件启用时订阅、禁用时解除订阅。不要把 indicator 指向挂载组件的对象本身，以免隐藏后无法重新订阅。现有 `Game/Presentation` 下的本地技能红点及已有界面引用保留；本期没有新增邮箱 UI 或绑定邮箱入口；1000/1001 仅是禁用的配置预留。

两端协议 DTO 一致，Google protobuf codec 使用客户端现有 `Tools/Protocol/GenerateGoogleProtocolCodec.py` 生成。此次未移除旧 codec 中已支持的任何合同。

## 验证结果

- .NET 8 服务端编译通过；MiaoWu `HotUpdate.csproj` 编译通过。
- 服务端红点规则与协议测试 6 项通过；客户端重构后的独立红点测试 9 项通过，包括 Models 只包含数据、Manager 在汇总后通知及原父子联动/版本处理场景。
- MySQL 隔离测试通过：红点迁移两次、独立测试业务表与红点状态的事务回滚、通用状态变化、查看版本与重连持久化；测试数据库没有邮件表，随机临时库在结束后删除。
- 初始化只读回归通过：空表不新增记录；禁止 INSERT/UPDATE 的触发器下仍能初始化；原 isActive、seenVersion、updateTime 保持不变；不调用业务提供方；提示版本提高后仍不自动点亮，直到业务显式触发。
- 真实 Game 角色启动及 TCP 验证通过：HMAC 鉴权、完整七个禁用节点配置、空叶子状态、拒绝清除禁用节点。此前邮件推送验证属于已撤回的接入，不计入当前业务能力。使用独立临时游戏库、账号库与空闲端口，结束后关闭并删除临时库。
- 服务端全部非数据库测试：93 项通过，1 项失败。失败项为已有 `PlayerProfileUpdateTests.BothCodecsPreserveNullableZeroJsonStringsAndLongIds`；修改前 codec 同样不包含 `ReqUpdatePlayerBasicInfo` 的编解码分支，原生成器不支持其枚举数组和 nullable 字段，本次未扩大处理范围。
- `MiaoWu.NetworkAuth.Tests.csproj` 因已有 7 个测试源文件路径不存在而无法完成编译；其引用的 HotUpdate 业务程序集可编译。本次另建 `Tools/RedDotTests` 独立测试工程验证实际红点树代码。未运行 Unity Editor 场景及 prefab 视觉验收。

运行红点核心测试：`dotnet test GameServerTests/GameServerTests.csproj --filter FullyQualifiedName~RedDotTests`；客户端：`dotnet test D:/iaowu/MiaoWu/Tools/RedDotTests/RedDot.Tests.csproj`。MySQL 测试沿用 GACHA_RUN_DATABASE_TESTS 和 GACHA_DB_CONFIGURATION_PATH 环境变量，并仅创建/删除随机命名的私有测试库。
