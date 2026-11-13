# 用户信息精简及客户端事件模板交付清单

> 本页为精简资料阶段的历史记录。后续已新增基地等级、主线关卡、共鸣等级、爬塔关卡，当前player为10列，响应末尾追加14～17号字段。最新说明和差异见 [ZombiePlayerProgressChanges.md](ZombiePlayerProgressChanges.md)。

## 结果

player仅保留 id、accountId、name、avatarId、createTime、status。查询91／92格式保持不变，废弃字段返回兼容值；头像更新93／94正常。
TowerDefence新增用户信息面板和MainInterface按钮，UI与Manager双向通过事件交互，Network复用SessionNetwork。新项目Skill已写入两项架构要求。未修改喵呜项目及其技能。

## 验证结果

- 服务端全套：109通过，45项依赖数据库开关的测试跳过，总计154项；本次额外启用相关数据库测试：18/18通过。
- 客户端网络协议／传输测试：16/16；恢复协调测试：8/8。
- 完整HotUpdate编译：0错误，461项既有工程警告。隔离Unity构建与实际面板联调通过。
- Unity联调：真实本机注册、登录、首次创建角色、查询、头像修改、刷新、断线、注销、再次登录；受控端点验证重复请求、请求标识过滤、关闭取消、延迟响应丢弃及10秒超时。预制体全部引用已绑定，渲染截图已检查。
- MainInterface按钮：场景序列化引用与点击监听已核对，实际MainInterface源码通过完整客户端编译。未在用户当前打开的完整游戏主场景内手动点击或验证引导动画；Unity运行验证使用隔离工程和实际资料面板，未替换用户打开的场景。
- 新Skill通过quick_validate.py校验；参考路径已按当前喵呜真实目录核对。保留MainInterface和RoomMgr原始编码。
- 迁移重复执行通过、原有保留字段数据一致。三次联调生成的三个测试账号及其角色、初始道具、任务和邮件已按确切账号ID清理。
- 本机ServerZone/Login/Game角色已启动，监听16090、16091、7080、7082、6090。主客户端原有服务器地址配置保持原样，隔离测试明确连接127.0.0.1；本次没有更新远端部署。
- 完整回归曾暴露旧测试的泛型字典强转问题，已仅修正测试快照枚举；真实鉴权发现旧职业校验，已移除。没有留下已知失败项。

GenDbClass/Program.cs 的本次特殊生成分支已撤回，文件与操作开始前快照逐字节一致；原有统一导表流程及用户先前修改均保留。

## 全部项目变更

以下差异以本次操作开始时的文件快照为基准，不将原有未提交修改算成本次改动。完整代码／SQL／Skill／场景／预制体差异见 [ZombiePlayerBasicInfo.patch](ZombiePlayerBasicInfo.patch)。差异面板打开请求返回queued，不能据此视为已打开。

| 操作 | 文件 | 改动与原因 |
|---|---|---|
| 新增 | [server/.codex/skills/zombie-ui-network-template/SKILL.md](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/.codex/skills/zombie-ui-network-template/SKILL.md) | 固化僵尸项目 UI → Manager → Network 分层和 UI／Manager 双向事件交互、请求关联及生命周期规范。 |
| 修改 | [server/sql/zombie_init.sql](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/sql/zombie_init.sql) | 将新环境 player 建表精简为六列，移除重新添加已删除资料字段的旧兼容段。 |
| 新增 | [server/sql/migrations/20261112_001_zombie_player_minimal.sql](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/sql/migrations/20261112_001_zombie_player_minimal.sql) | 为 zombie_game 提供可重复执行的 MySQL 5.7 删列迁移。 |
| 修改 | [server/GameServer/src/Game/Datas/DbEntities/Player.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Datas/DbEntities/Player.cs) | 从实际 player 表仅重新生成六字段实体，保留小写表名映射。 |
| 修改 | [server/GameServer/src/Game/Core/EntityMgr/GM_AccountEntityMgr.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/EntityMgr/GM_AccountEntityMgr.cs) | 创建角色不再依赖职业或写入删除字段；头像提交后只同步保留的头像字段。 |
| 修改 | [server/GameServer/src/Game/Entries/Modules/GameAuthModule.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Entries/Modules/GameAuthModule.cs) | 默认角色鉴权绑定不再读取已删除职业字段。 |
| 修改 | [server/GameServer/src/Game/Entries/Modules/PlayerModule.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Entries/Modules/PlayerModule.cs) | 维持查询及修改响应格式，废弃字段返回兼容值；旧 PlayerInfo 数值归零，停用旧资产功能。 |
| 修改 | [server/GameServer/src/Game/Core/GM_PlayerProfile/GM_PlayerProfileMgr.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/GM_PlayerProfile/GM_PlayerProfileMgr.cs) | 精简资料读取；返回兼容占位值；仅头像事务更新并验证账号归属，提交后同步缓存。 |
| 修改 | [server/GameServer/src/Game/Core/GM_PlayerProfile/PlayerProfileDefinitions.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/GM_PlayerProfile/PlayerProfileDefinitions.cs) | 领域更新命令仅允许单独修改合法头像ID。 |
| 修改 | [server/GameServer/src/Game/Core/EntityMgr/GM_EntityMgr.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/EntityMgr/GM_EntityMgr.cs) | 会话绑定不再要求职业，停止旧金币排行写入。 |
| 修改 | [server/GameServer/src/Game/LogicServer/Room/RoomMgr.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/LogicServer/Room/RoomMgr.cs) | 旧房间性别协议字段返回0；保留文件原始编码。 |
| 修改 | [server/GameServer/src/Game/LogicServer/ZoneRule.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/LogicServer/ZoneRule.cs) | 旧等级／金币入场条件返回 InvalidOpt。 |
| 修改 | [server/GameServer/src/Game/Core/GM_Bonues/Rules/BonuesRuleA.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/GM_Bonues/Rules/BonuesRuleA.cs) | 旧金币元宝奖励规则禁止执行，避免引用删除的资产字段。 |
| 修改 | [server/GameServer/src/Game/Core/GM_Trading/Rules/ExchangeTradingRules.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/GM_Trading/Rules/ExchangeTradingRules.cs) | 旧元宝兑换规则拒绝交易，不发放商品。 |
| 修改 | [server/GameServer/src/Game/Core/GM_Bonues/GM_BonuesMgr.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/GM_Bonues/GM_BonuesMgr.cs) | 旧资产奖励在任何领取状态更新之前拒绝，直接领取入口也不查库或标记领取。 |
| 新增 | [server/tools/Update-ZombiePlayer.ps1](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/tools/Update-ZombiePlayer.ps1) | 限定目标库，停止本项目服务、备份、迁移两次并核对列和保留数据。 |
| 修改 | [server/GameServer/src/Game/LogicServer/World/AOI/AOIMgr.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/LogicServer/World/AOI/AOIMgr.cs) | 旧AOI职业与性别协议字段返回0。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Models/PlayerBasicInfoModel.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Models/PlayerBasicInfoModel.cs) | 保存不可变的五项客户端展示资料，账号ID来自当前登录会话。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Events/PlayerBasicInfoEvents.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Events/PlayerBasicInfoEvents.cs) | 定义带请求标识和取消令牌的请求事件，以及加载／成功／失败／全局清空事件。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network/PlayerBasicInfoNetwork.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network/PlayerBasicInfoNetwork.cs) | 只负责91／92协议请求、10秒超时、重复请求策略及取消。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager/PlayerBasicInfoManager.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager/PlayerBasicInfoManager.cs) | 监听请求事件，协调网络和会话取消，维护快照并通过结果事件反馈UI。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.cs) | 纯事件交互面板，展示资料和状态，自动查询／刷新／关闭，过滤不匹配与过期结果。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Shared/Network/SessionNetwork.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Shared/Network/SessionNetwork.cs) | 网络功能初始化时幂等创建资料Manager，确保UI发事件前监听已注册。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/MainInterface.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/MainInterface.cs) | 增加用户信息按钮字段、打开面板方法和成对监听绑定；保留文件原始编码。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Models.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Models.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Events.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Events.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Models/PlayerBasicInfoModel.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Models/PlayerBasicInfoModel.cs.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Events/PlayerBasicInfoEvents.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Events/PlayerBasicInfoEvents.cs.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network/PlayerBasicInfoNetwork.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Network/PlayerBasicInfoNetwork.cs.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager/PlayerBasicInfoManager.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager/PlayerBasicInfoManager.cs.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.cs.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 修改 | [client/Tools/NetworkVerification/UnityCompile.csproj](D:/apk/TowerDefence/Tools/NetworkVerification/UnityCompile.csproj) | 将新功能实际源码和面板纳入隔离Unity API编译。 |
| 修改 | [client/Tools/SessionVerification/UnityUIHarness.cs](D:/apk/TowerDefence/Tools/SessionVerification/UnityUIHarness.cs) | 隔离测试UI加载器补充关闭回调，用于实际关闭按钮验证；不进入游戏。 |
| 修改 | [server/GameServerTests/PlayerProfile/PlayerBasicInfoTests.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServerTests/PlayerProfile/PlayerBasicInfoTests.cs) | 更新精简资料、缓存回填、协议兼容及真实数据库回退测试。 |
| 修改 | [server/GameServerTests/PlayerProfile/PlayerProfileUpdateTests.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServerTests/PlayerProfile/PlayerProfileUpdateTests.cs) | 验证头像重复置零、账号归属、混合修改拒绝和兼容数组快照隔离。 |
| 修改 | [client/Assets/Scenes/Start.unity](D:/apk/TowerDefence/Assets/Scenes/Start.unity) | 在设置按钮下方增加已绑定的用户信息按钮，仅追加节点与对应引用。 |
| 新增 | [client/Assets/Prefabs/UI/Panel/PlayerBasicInfo.meta](D:/apk/TowerDefence/Assets/Prefabs/UI/Panel/PlayerBasicInfo.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Prefabs/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.prefab](D:/apk/TowerDefence/Assets/Prefabs/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.prefab) | Unity生成完整UGUI面板，按钮、文本和脚本引用全部序列化绑定。 |
| 新增 | [client/Assets/Prefabs/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.prefab.meta](D:/apk/TowerDefence/Assets/Prefabs/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.prefab.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/Editor/IntegrationUI/PlayerBasicInfoPrefabBuilder.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/Editor/IntegrationUI/PlayerBasicInfoPrefabBuilder.cs) | 增加可重复使用的Unity预制体及主界面入口构建菜单，保护未保存场景。 |
| 新增 | [client/Assets/Rogue/Scripts/Editor/IntegrationUI/PlayerBasicInfoPrefabBuilder.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/Editor/IntegrationUI/PlayerBasicInfoPrefabBuilder.cs.meta) | 新增Unity脚本／目录／预制体的稳定GUID及导入元数据。 |
| 修改 | [server/GameServer/src/Framework/Core/serializer/GoogleProtocolCodec.Generated.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Framework/Core/serializer/GoogleProtocolCodec.Generated.cs) | 补齐原先缺失的93号请求编码／解码；保留所有已有协议和响应编码。 |
| 修改 | [client/Tools/NetworkVerification/HotUpdate.Network.targets](D:/apk/TowerDefence/Tools/NetworkVerification/HotUpdate.Network.targets) | 将新面板纳入完整HotUpdate CLI编译，避免Unity项目尚未刷新时遗漏源码。 |
| 新增 | [client/Tools/PlayerBasicInfoVerification/Run-Verification.ps1](D:/apk/TowerDefence/Tools/PlayerBasicInfoVerification/Run-Verification.ps1) | 准备隔离Unity工程并执行预制体生成或联调，不更改主客户端网络地址。 |
| 修改 | [client/Tools/SessionVerification/LoopbackGameServer.cs](D:/apk/TowerDefence/Tools/SessionVerification/LoopbackGameServer.cs) | 测试协议端点支持资料查询、延迟和不响应，验证取消与10秒超时。 |
| 新增 | [client/Tools/PlayerBasicInfoVerification/PlayerBasicInfoSmoke.cs](D:/apk/TowerDefence/Tools/PlayerBasicInfoVerification/PlayerBasicInfoSmoke.cs) | 新增隔离Unity真实服务端和受控网络端点联调，包含事件过滤、取消、超时及截图。 |
| 修改 | [server/GameServerTests/LogoutAndTcpLifecycleTests.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServerTests/LogoutAndTcpLifecycleTests.cs) | 修复全套运行才暴露的既有测试快照枚举问题：通过字典键取值，不将泛型 KeyValuePair 强转为 DictionaryEntry。 |
| 新增 | [server/GameServerTests/PlayerProfile/PlayerMinimalSchemaTests.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServerTests/PlayerProfile/PlayerMinimalSchemaTests.cs) | 新增六字段实体和旧功能无领取副作用的回归检查。 |
| 修改 | [server/docs/PlayerBasicInfo.md](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/docs/PlayerBasicInfo.md) | 更新当前僵尸项目字段、协议兼容、客户端事件结构、数据库操作和测试说明。 |
| 新增 | [server/docs/ZombiePlayerBasicInfo.png](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/docs/ZombiePlayerBasicInfo.png) | 联调时真实资料面板的渲染预览（交付图片，不是游戏资源）。 |

## 交付与验证产物（单独列出）

- 本清单：docs/ZombiePlayerBasicInfoChanges.md。
- 完整差异：docs/ZombiePlayerBasicInfo.patch；包含本次所有源码、Skill、SQL、场景、预制体和元数据变更。交付清单和差异文件本身不递归纳入差异。
- 预览图：docs/ZombiePlayerBasicInfo.png（新增二进制渲染产物，非游戏资源）。
- 数据库完整备份：[zombie_game.player.sql](C:/Users/Windows/AppData/Local/Temp/zombie-player-backup-20261112-120120/zombie_game.player.sql)。恢复方式：停止本项目服务，在zombie_game中导入该备份，再恢复对应旧版服务端；不能让旧表和新实体混用。
- 修改前文件快照及manifest、编辑辅助脚本、测试日志、定向清理脚本：[任务临时目录](C:/Users/Windows/AppData/Local/Temp/zombie-profile-20261112)。这些不加入项目源码。
- 关键日志：任务临时目录的server-tests-final.log、profile-tests-final.log、client-full-build.log、servers.log。
- Unity隔离工程／构建日志／联调日志／测试账号记录／原始截图：[Temp/PlayerBasicInfoVerification](D:/apk/TowerDefence/Temp/PlayerBasicInfoVerification)。测试账号记录仅供审计，对应账号已清理。
- 本次构建生成或重新写入的Git可见bin／obj等产物路径清单：[build-artifacts.txt](C:/Users/Windows/AppData/Local/Temp/zombie-profile-20261112/build-artifacts.txt)。它们是派生编译产物；已有修改状态不等同于本次源码变更。核心GameServer.dll／exe／pdb与HotUpdate.dll为重新编译结果，未手工修改二进制。
- Skill校验所需PyYAML安装在已有Codex捆绑Python中，没有新增项目依赖。
