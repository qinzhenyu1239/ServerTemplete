# 抽卡自动化测试

测试项目名为 `GameServerTests`，不会连接正式服。默认命令只运行内存中的规则、概率、保底和配置测试。

## 直接运行

在仓库根目录执行：

```powershell
.\GameServerTests\run-tests.ps1
```

如果 PowerShell 阻止本地脚本执行，可以使用：

```powershell
powershell -ExecutionPolicy Bypass -File .\GameServerTests\run-tests.ps1
```

也可以使用标准 .NET 命令：

```powershell
dotnet test .\GameServerTests\GameServerTests.csproj
```

## 包含 MySQL 集成测试

先确认测试数据库已经应用 `sql/migrations/20260831_001_gacha.sql`，并检查配置文件中的 `connGameDataStr` 指向测试库，然后执行：

```powershell
.\GameServerTests\run-tests.ps1 -IncludeDatabase
```

需要保留测试数据用于人工检查时：

```powershell
.\GameServerTests\run-tests.ps1 -IncludeDatabase -KeepDatabaseData
```

脚本结束会打印本次使用的负数 `player_id` 范围和四条查询 SQL。每个真实抽卡场景使用独立玩家，保留的数据包括支付道具、角色、碎片、保底状态和抽卡记录。

默认读取 `GameServer/App.config`。使用单独的测试配置文件时：

```powershell
.\GameServerTests\run-tests.ps1 `
  -IncludeDatabase `
  -DatabaseConfigurationPath "D:\配置\GameServer.Test.config"
```

MySQL 测试由 .NET 8 测试进程执行，不依赖 PowerShell 的 `Add-Type`。它使用负数临时玩家 ID，结束时会清理创建的数据。仍然建议只对本机或隔离的测试数据库运行。

## 当前覆盖

- 稀有度权重的首尾边界
- 奖励权重的首尾边界
- 小保底、大保底及优先级
- 固定随机种子的 20 万次概率分布
- 当前 CSV 配置能否成功加载
- 重复卡池、错误权重和无效奖励引用
- 底层 MySQL 事务回滚与同一余额的并发扣费
- 真实 `GM_GachaMgr.Draw()` 单抽及响应、数据库、在线组件一致性
- 连续抽取后重复角色转碎片
- 十连先消耗抽卡券、再消耗钻石，并生成完整批次记录
- 资源不足时不产生角色、保底状态或抽卡记录
- 扣费后发生异常时，支付、角色、保底和记录全部回滚
- 同一玩家并发抽卡时只有一个请求成功
- 真实数据库中的小保底触发、重置和持久化

## 当前系统边界

传输包中的 `requestId` 当前只用于关联响应，没有传入抽卡 Controller、Module 或 Manager，数据库也没有抽卡幂等键。因此现阶段无法验证“相同 requestId 重试只执行一次”。这属于待实现的业务能力，而不是测试脚本可以单独补出的行为。

## 容量与性能测试

性能测试与日常正确性测试分开，默认生成 10 万条历史记录，并执行不同玩家并发抽卡、同一玩家并发抽卡、最近记录查询、游标分页和深度 OFFSET 查询：

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\GameServerTests\run-performance-tests.ps1
```

小规模快速验证：

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\GameServerTests\run-performance-tests.ps1 `
  -RecordCount 1000 `
  -ConcurrentPlayers 8 `
  -DrawsPerPlayer 5
```

百万条并保留数据：

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\GameServerTests\run-performance-tests.ps1 `
  -RecordCount 1000000 `
  -ConcurrentPlayers 64 `
  -DrawsPerPlayer 20 `
  -KeepDatabaseData
```

超过 100 万条时必须额外传入 `-AllowLargeDataSet`。每次执行会在 `GameServerTests/TestResults` 生成 JSON 报告；默认自动清理测试数据。

在明确要修改当前测试数据库结构时，可以应用推荐的抽卡历史组合索引并立即压测：

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\GameServerTests\run-performance-tests.ps1 `
  -ApplyHistoryIndex
```

该参数执行 `sql/migrations/20260905_001_gacha_history_index.sql`，新增 `(player_id, created_at, id)` 索引。迁移兼容 MySQL 5.7，可以重复执行，不会删除旧索引或业务数据。

本机测试库的 10 万条基准中，增加索引后最近一页查询从约 `273.59ms` 降至 `0.62ms`，游标分页从约 `282.03ms` 降至 `0.72ms`。深度 OFFSET 仍明显慢于游标分页，因此业务查询仍应优先使用游标分页。
