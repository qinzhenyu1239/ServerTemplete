# 用户信息接入重连恢复

## 当前行为

首次游戏服鉴权和自动重连鉴权后，由现有ReconnectRecoveryRegistry执行PlayerBasicInfoRecoveryHandler，顺序为Refresh阶段／SessionSyncOrder.PlayerProfile。读取成功后才完成恢复并进入Online；不额外监听NetworkConnected重复查询。
Manager保存全部用户资料快照，包括基地等级、主线关卡、共鸣等级和爬塔关卡，继续通过事件通知UI。
恢复事件带IsSessionRefresh=true，已打开面板自动更新；普通手动请求保留请求编号匹配。面板在恢复期间关闭或重新打开不会取消账号级恢复，也不会重复发包。隐藏面板时，Manager仍更新缓存。
取消令牌关联恢复上下文、账号和连接；响应写入前重新核对上下文和代次。恢复失败或超时向协调器传播，沿用既有自动重连重试策略；注销取消恢复，迟到响应不更新旧账号资料。
没有修改数据库结构、服务端协议、统一导表Program.cs或MainInterface／面板预制体。

## 验证

- 客户端隔离Unity API编译0错误；完整HotUpdate编译0错误，461项既有工程警告。
- 现有恢复协调器便携测试8/8通过；网络协议／传输便携测试16/16通过。
- 隔离Unity真实本机服务联调通过：首次鉴权不手动查询就填充缓存及活动面板；重新连接和重新登录自动更新；原头像更新、四项进度、请求编号过滤、刷新和取消继续通过。
- 受控TCP端点主动断开连接后，客户端自动重连、重新鉴权、只发起一次资料查询，并自动显示修改后的名称。
- 恢复查询期间关闭再打开面板：不重复发包，恢复结果仍可显示；面板隐藏期间断线重连：资料缓存仍更新。
- 手动查询10秒超时、恢复查询10秒超时分别验证；恢复失败不能进入Online且释放忙状态，后续鉴权可再次成功恢复。
- 注销发生在恢复查询进行中：任务取消，延迟响应不能重建旧资料。
- UI保持纯事件交互，Program.cs与本次操作开始前逐字节一致。
- 本次生成的1个真实联调账号及其角色、道具、任务和邮件已按确切账号名清理。没有为此重启现有本机服务。
- 未在用户打开的完整游戏主场景内手动点击；运行验证使用实际资料面板／Manager／Network和隔离Unity工程。

## 全部项目变更

以下差异以本次“接入重连恢复”开始前的快照为基准，不将先前进度字段等改动计入本次。
完整差异：[ZombiePlayerRecovery.patch](ZombiePlayerRecovery.patch)。

| 操作 | 文件 | 改动与原因 |
|---|---|---|
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Events/PlayerBasicInfoEvents.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Events/PlayerBasicInfoEvents.cs) | 加载／成功／失败事件增加IsSessionRefresh标记；默认false，保留手动请求编号匹配行为。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager/PlayerBasicInfoManager.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager/PlayerBasicInfoManager.cs) | 新增RecoverAsync并与手动查询共用读取逻辑；恢复上下文及代次校验；恢复期间拒绝重复手动请求；恢复错误／取消向协调器传播。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network/PlayerBasicInfoRecoveryHandler.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network/PlayerBasicInfoRecoveryHandler.cs) | 新增注册表自动发现的恢复处理器，在Refresh/PlayerProfile顺序执行账号资料查询；CollectTasks不产生副作用，Preserve保留反射入口。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network/PlayerBasicInfoRecoveryHandler.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network/PlayerBasicInfoRecoveryHandler.cs.meta) | 新增脚本稳定GUID和Unity导入元数据。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.cs) | 监听账号级恢复事件自动更新面板；普通查询仍按请求编号过滤；恢复期间重开面板也能接收最终结果。 |
| 修改 | [client/Tools/SessionVerification/LoopbackGameServer.cs](D:/apk/TowerDefence/Tools/SessionVerification/LoopbackGameServer.cs) | 测试端点支持切换资料名称，验证断线后获得新资料而非残留快照。 |
| 修改 | [client/Tools/PlayerBasicInfoVerification/PlayerBasicInfoSmoke.cs](D:/apk/TowerDefence/Tools/PlayerBasicInfoVerification/PlayerBasicInfoSmoke.cs) | 验证首次鉴权、自动重连、隐藏／重开面板、单次恢复查询、超时失败传播、重试成功和注销取消；调整关闭面板断言以允许保留已恢复的快照。 |
| 修改 | [server/docs/PlayerBasicInfo.md](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/docs/PlayerBasicInfo.md) | 记录首次登录及重连自动拉取、事件标记、取消与失败语义。 |

## 单独列出的交付与临时产物

- 本交付清单：docs/ZombiePlayerRecoveryChanges.md；完整实际差异：docs/ZombiePlayerRecovery.patch。两份交付文件不递归纳入差异。
- 本次文件快照／manifest、Program未变基线、辅助脚本、client-build.log、network-tests.log、smoke.log、精确账号清理脚本：[任务临时目录](C:/Users/Windows/AppData/Local/Temp/zombie-profile-recovery-20261112)。
- Unity隔离工程和运行日志：[Temp/PlayerBasicInfoVerification](D:/apk/TowerDefence/Temp/PlayerBasicInfoVerification)。profile-test-accounts-recovery.txt对应本次已清理账号。
- 客户端HotUpdate.dll、UnityCompile.dll及隔离Unity程序集为重新编译产物；未手工修改二进制或游戏美术资源。
