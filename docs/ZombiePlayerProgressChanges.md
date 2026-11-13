# 用户资料新增四项进度

## 当前行为

新增四项服务端player数据，并在MainInterface用户信息面板展示。采用现有UI／Manager双向事件及Manager → Network模式。

| 数据库／协议字段 | 含义 | 类型 | 初始值 | 响应Proto编号 |
|---|---|---|---|---|
| baseLevel | 基地等级 | int | 1 | 14 |
| mainStage | 主线关卡（数字章节编号） | int | 1 | 15 |
| resonanceLevel | 共鸣等级 | int | 0 | 16 |
| towerStage | 爬塔关卡 | int | 0 | 17 |

player现有6列保持，新增4列后共10列。请求91／93、响应92／94及原字段1～13不变。响应末尾增加14～17；旧客户端可跳过新增字段。头像修改仍只允许头像，四项进度本次仅存储／查询／展示，不上传或覆盖本地存档，不新增进度写接口。
统一导表tools/GenDbClass/Program.cs与本次开始前逐字节一致，未增加特判或单表生成参数。本次仅同步实体属性。

## 验证

- 迁移备份后执行两次成功，原有六字段数据一致；四项新增默认值经真实数据库及新角色创建验证。
- 相关服务端／数据库测试18/18通过；服务端全套109通过、45项数据库开关测试跳过，共154项。
- 客户端完整HotUpdate编译0错误，461项既有工程警告；网络传输测试16/16通过。
- 协议导出工具10项测试通过；143个合同、120个消息ID、双向protobuf-net兼容及111个服务端Google编解码检查通过。新增字段值包含非零值、旧字段均保留。
- 隔离Unity生成九行面板并核对引用；真实本机注册／登录／新角色查询／头像修改／再次查询／断线／注销／重新登录通过；新角色显示1、1、0、0。
- 受控端点返回12、23、34、45，经Network → Manager → Loaded事件 → UI正确显示。原取消、重复请求、请求标识过滤、延迟响应、10秒超时测试继续通过。
- 渲染预览已目视检查，九行数据与刷新、关闭按钮完整显示。完整游戏主场景的手动点击未重测；主界面入口代码本次没有变化。
- 本次生成的1个测试账号及其关联角色、初始道具、任务和邮件已按确切账号名清理，用户原记录保留。
- 本机ServerZone/Login/Game已重新启动；主客户端服务器地址未修改。本次验证明确连接本机127.0.0.1，不更新远端部署。

## 全部变更文件

差异相对本次新增四项数据之前的文件快照，保留此前用户和本会话已有改动。完整差异：[ZombiePlayerProgress.patch](ZombiePlayerProgress.patch)。

| 操作 | 文件 | 改动和原因 |
|---|---|---|
| 修改 | [server/sql/zombie_init.sql](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/sql/zombie_init.sql) | player新增4个int列及默认值，保留原有6列；记录两段迁移的执行顺序。 |
| 修改 | [server/GameServer/src/Game/Datas/DbEntities/Player.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Datas/DbEntities/Player.cs) | 同步数据库新增的四项进度属性及初始默认值。 |
| 修改 | [server/GameServer/src/Game/Datas/Messages/PlayerBasicInfoMessage.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Datas/Messages/PlayerBasicInfoMessage.cs) | 查询92和修改94响应末尾追加14～17号int字段，原编号及请求不变。 |
| 修改 | [server/GameServer/src/Game/Core/GM_PlayerProfile/PlayerProfileDefinitions.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/GM_PlayerProfile/PlayerProfileDefinitions.cs) | 领域资料快照增加四项进度。 |
| 修改 | [server/GameServer/src/Game/Core/GM_PlayerProfile/GM_PlayerProfileMgr.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/GM_PlayerProfile/GM_PlayerProfileMgr.cs) | 从Player映射四项进度到查询／更新结果，维持原缓存读取与事务写入。 |
| 修改 | [server/GameServer/src/Game/Core/EntityMgr/GM_AccountEntityMgr.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Core/EntityMgr/GM_AccountEntityMgr.cs) | 首次创建角色显式初始化基地1、主线1、共鸣0、爬塔0，保证统一导表重生成后也不依赖属性初始值。 |
| 修改 | [server/GameServer/src/Game/Entries/Modules/PlayerModule.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Game/Entries/Modules/PlayerModule.cs) | 查询及头像修改响应均返回四项进度；不开放客户端修改进度。 |
| 修改 | [server/GameServer/src/Framework/Core/serializer/GoogleProtocolCodec.Generated.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServer/src/Framework/Core/serializer/GoogleProtocolCodec.Generated.cs) | 同步两种响应的Google protobuf读写，保留原有合同和消息处理分支。 |
| 修改 | [server/GameServerTests/PlayerProfile/PlayerMinimalSchemaTests.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServerTests/PlayerProfile/PlayerMinimalSchemaTests.cs) | 核对实体10个字段，继续验证旧功能无领取副作用。 |
| 修改 | [server/GameServerTests/PlayerProfile/PlayerBasicInfoTests.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServerTests/PlayerProfile/PlayerBasicInfoTests.cs) | 验证非默认进度经缓存、Module、Google/protobuf-net编解码传递正确，并验证数据库默认值。 |
| 修改 | [server/GameServerTests/PlayerProfile/PlayerProfileUpdateTests.cs](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/GameServerTests/PlayerProfile/PlayerProfileUpdateTests.cs) | 真实数据库中使用非默认进度，验证头像重复修改和缓存同步不改变四项进度。 |
| 修改 | [server/docs/PlayerBasicInfo.md](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/docs/PlayerBasicInfo.md) | 文档更新为10列、响应追加字段、默认值和仅服务端存储展示的边界。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Protocol/Messages/GeneratedProtocol.Generated.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Protocol/Messages/GeneratedProtocol.Generated.cs) | 协议工具生成客户端四项进度的编码／解码。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Protocol/Messages/PlayerBasicInfoMessage.Generated.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Protocol/Messages/PlayerBasicInfoMessage.Generated.cs) | 使用现有统一协议导出工具生成新增响应字段。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Protocol/Messages/protocol-manifest.json](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Protocol/Messages/protocol-manifest.json) | 协议导出工具更新源合同哈希与字段清单。 |
| 新增 | [server/sql/migrations/20261112_002_zombie_player_progress.sql](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/sql/migrations/20261112_002_zombie_player_progress.sql) | 新增仅针对zombie_game的MySQL 5.7幂等加列迁移，不重置已有进度。 |
| 修改 | [server/tools/Update-ZombiePlayer.ps1](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/tools/Update-ZombiePlayer.ps1) | 备份后按顺序执行两段迁移两次，核对保留数据及最终10列；不改统一导表入口。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Models/PlayerBasicInfoModel.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Models/PlayerBasicInfoModel.cs) | 只读客户端资料快照增加四项进度。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager/PlayerBasicInfoManager.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/PlayerBasicInfo/Manager/PlayerBasicInfoManager.cs) | 把服务端响应四项进度放入快照，通过原Loaded事件交给UI。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.cs) | 新增四个显示引用，成功时展示进度，清空时同时清除；保持双向事件交互。 |
| 修改 | [client/Assets/Rogue/Scripts/Editor/IntegrationUI/PlayerBasicInfoPrefabBuilder.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/Editor/IntegrationUI/PlayerBasicInfoPrefabBuilder.cs) | 面板由5行扩展为9行、增加高度并绑定四个进度文本。 |
| 修改 | [client/Assets/Prefabs/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.prefab](D:/apk/TowerDefence/Assets/Prefabs/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.prefab) | Unity重新生成九行面板，序列化四个文本引用，保持脚本与资源GUID。 |
| 修改 | [client/Tools/SessionVerification/LoopbackGameServer.cs](D:/apk/TowerDefence/Tools/SessionVerification/LoopbackGameServer.cs) | 测试端点返回12、23、34、45，验证非默认进度跨协议／事件传递。 |
| 修改 | [client/Tools/PlayerBasicInfoVerification/PlayerBasicInfoSmoke.cs](D:/apk/TowerDefence/Tools/PlayerBasicInfoVerification/PlayerBasicInfoSmoke.cs) | 验证真实角色默认值、头像修改后进度保持、四项非默认值展示，并记录精确测试账号供清理。 |
| 修改 | [server/docs/ZombiePlayerBasicInfoChanges.md](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/docs/ZombiePlayerBasicInfoChanges.md) | 标明原交付清单为历史记录，并指向本次进度扩展清单。 |
| 新增 | [server/docs/ZombiePlayerProgress.png](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/docs/ZombiePlayerProgress.png) | 新增九行资料面板的真实Unity渲染预览；交付产物，不是游戏资源。 |

## 备份与产物

- 数据库变更前完整备份：[zombie_game.player.sql](C:/Users/Windows/AppData/Local/Temp/zombie-player-backup-20261112-140710/zombie_game.player.sql)。
- 新增交付清单：docs/ZombiePlayerProgressChanges.md；新增完整差异：docs/ZombiePlayerProgress.patch；清单和差异本身不递归纳入差异。
- 新增二进制预览图：[ZombiePlayerProgress.png](ZombiePlayerProgress.png)，不是游戏运行资源。
- 临时快照、辅助脚本、Program不变基线、profile-tests.log、server-tests.log、client-build.log、network-tests.log、protocol-tests.log、servers.log：[本次临时目录](C:/Users/Windows/AppData/Local/Temp/zombie-profile-progress-20261112)。
- Unity隔离工程／build.log／smoke.log／测试账号审计记录／原始预览图：[Temp/PlayerBasicInfoVerification](D:/apk/TowerDefence/Temp/PlayerBasicInfoVerification)。审计记录对应账号已删除。
- 协议工具测试输出仅位于客户端Temp/ProtocolToolTests；统一协议工具源码未修改。
- 服务端GameServer.dll／exe／pdb、本机部署副本及客户端HotUpdate.dll等为重新编译产物；原始源码快照不把既有bin／obj改动当作本次源码改动。

本次Git可见派生构建产物完整路径清单：[build-artifacts.txt](C:/Users/Windows/AppData/Local/Temp/zombie-profile-progress-20261112/build-artifacts.txt)。
