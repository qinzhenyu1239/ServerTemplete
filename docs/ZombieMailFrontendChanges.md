# 僵尸项目邮件前端交付清单

## 已完成行为

MainInterface用户信息按钮下方新增邮件按钮及未读数，打开UIMail。前端参考MiaoWu邮件操作和现有用户资料的UI → Manager → Network分层，UI／Manager双向通过事件交互。
接入既有摘要、全分页列表、阅读、单封领取、一键领取、单封删除、删除已读邮件，支持正文／附件滚动、已读与已领取灰显、确认、空状态、过期和错误提示。
Manager保存不可变快照和服务端道具绝对数量；领取结果通过MailRewardsUpdatedEvent通知其他模块。关闭面板不会取消已接受的写请求；写响应丢失只读取邮件和资产核对，不自动重发写请求。核对未完成时阻止继续写。
首次鉴权及重连接入既有恢复协调器：关闭面板时刷新摘要和资产；打开时刷新完整列表。在线每30秒刷新，过期时刷新。注销及断线清空，代次和账号校验丢弃迟到响应。
后端业务、SQL、协议定义、MiaoWu代码和统一导表Program.cs未修改。

## 验证结果

- 隔离Unity API编译及完整HotUpdate编译通过，0错误；完整编译461项既有工程警告。
- 网络协议／传输便携测试16/16，恢复协调器测试8/8通过。
- Unity预制体生成与引用核对通过，实际面板渲染预览已目视检查。
- 真实本机后端：注册独立测试账号、首次鉴权自动恢复、新角色10封测试邮件；列表／阅读／正文及附件／未读数／单封领取／关闭面板后领取完成／一键领取／取消删除／确认删除已读／空状态／注销通过。
- 受控端点：55封邮件跨页加载、分页版本变化后整体重读、无重复ID；单封删除取消及确认；未领取附件不能删除；重复点击只提交一次；接受写请求后关闭仍应用服务器结果；过期邮件移除；活动和隐藏面板自动重连刷新通过。
- 丢失一键领取响应：15秒超时后收到不确定结果，随后只读邮箱和道具完成核对，服务端一键领取计数保持1、资产数量仅增加一次。
- 延迟阅读响应与注销隔离、邮件和资产快照清空通过。
- UI和未读提示组件静态检查均无直接Manager／Network业务调用；MainInterface监听成对绑定，场景新增ID无重复，按钮引用有效。
- 最终完整真实验证脚本成功退出；所有zmail_测试账号（含首次注册长度不符而未创建的账号记录）已按确切审计名称定向清理，临时ServerZone/Login/Game三个进程全部停止，7080、7082、6090、16090、16091均无监听。

## 实际边界

附件当前显示服务端道具ID和数量，正文按纯文本显示；未复制MiaoWu的美术图标及超链接组件。未新增完整背包界面或覆盖原本地金币／体力存档。
运行联调使用隔离Unity工程中的实际邮件面板、Manager、Network和真实本机服务；用户当前打开的完整游戏主场景未手动点击，入口代码已完整编译、场景序列化引用已核对。

## 全部项目变更

以下以本次邮件任务开始时的快照为基准，保留用户原有修改及此前资料功能。完整差异：[ZombieMailFrontend.patch](ZombieMailFrontend.patch)。

| 操作 | 文件 | 内容和原因 |
|---|---|---|
| 修改 | [client/Assets/Scenes/Start.unity](D:/apk/TowerDefence/Assets/Scenes/Start.unity) | 在用户信息入口下方新增邮件按钮及未读提示组件，绑定MainInterface.MailButton并保留原有场景内容。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/MainInterface.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/MainInterface.cs) | 新增MailButton、OpenMail及成对按钮监听，防止并发重复打开；保留文件原始编码。 |
| 修改 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Shared/Network/SessionNetwork.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Shared/Network/SessionNetwork.cs) | 初始化邮件Manager，保证面板发事件前已注册监听。 |
| 修改 | [client/Tools/NetworkVerification/UnityCompile.csproj](D:/apk/TowerDefence/Tools/NetworkVerification/UnityCompile.csproj) | 把邮件模块及UI源码纳入隔离Unity API编译。 |
| 修改 | [client/Tools/NetworkVerification/HotUpdate.Network.targets](D:/apk/TowerDefence/Tools/NetworkVerification/HotUpdate.Network.targets) | 完整客户端CLI编译纳入新邮件UI，Game目录仍用已有通配发现。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Models/MailModels.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Models/MailModels.cs) | 新增不可变邮件、附件、摘要、邮箱快照、网络响应及操作结果模型。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Events/MailEvents.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Events/MailEvents.cs) | 新增带请求编号／取消令牌的操作事件、视图生命周期与快照请求、会话状态广播、服务端资产绝对数量事件。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network/MailNetwork.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network/MailNetwork.cs) | 接入七组既有邮件协议及道具数量核对请求，设置15秒超时，不在Network保存业务状态。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Manager/MailManager.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Manager/MailManager.cs) | 管理快照、分页及版本重读、单操作并发、确认结果应用、写请求与面板取消隔离、只读核对、过期与30秒刷新、注销断线清空。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network/MailRecoveryHandler.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network/MailRecoveryHandler.cs) | 按SessionSyncOrder.Mail接入首次鉴权和重连恢复，刷新摘要／活动列表及道具数量；错误向协调器传播。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMailRow.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMailRow.cs) | 新增可复用列表行，绑定邮件ID、未读／已读／附件／领取状态和灰显，通过UI回调发布阅读请求。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMail.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMail.cs) | 新增事件驱动邮件面板，提供列表／正文／附件、刷新、领取、批量领取、删除、删除已读、确认与错误／不确定状态显示。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMailUnreadBadge.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMailUnreadBadge.cs) | 邮件入口只通过事件显示未读数和会话清空状态。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Models.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Models.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Models/MailModels.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Models/MailModels.cs.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Events.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Events.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Events/MailEvents.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Events/MailEvents.cs.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Manager.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Manager.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Manager/MailManager.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Manager/MailManager.cs.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network/MailNetwork.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network/MailNetwork.cs.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network/MailRecoveryHandler.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/Game/Mail/Network/MailRecoveryHandler.cs.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMail.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMail.cs.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMailRow.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMailRow.cs.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMailUnreadBadge.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/HotUpdate/UI/Panel/Mail/UIMailUnreadBadge.cs.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Rogue/Scripts/Editor/IntegrationUI/MailPrefabBuilder.cs](D:/apk/TowerDefence/Assets/Rogue/Scripts/Editor/IntegrationUI/MailPrefabBuilder.cs) | 新增可复用Unity面板生成工具，复用现有中文字体及资料面板视觉，并生成主界面按钮模板。 |
| 新增 | [client/Assets/Rogue/Scripts/Editor/IntegrationUI/MailPrefabBuilder.cs.meta](D:/apk/TowerDefence/Assets/Rogue/Scripts/Editor/IntegrationUI/MailPrefabBuilder.cs.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Prefabs/UI/Panel/Mail.meta](D:/apk/TowerDefence/Assets/Prefabs/UI/Panel/Mail.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 新增 | [client/Assets/Prefabs/UI/Panel/Mail/UIMail.prefab](D:/apk/TowerDefence/Assets/Prefabs/UI/Panel/Mail/UIMail.prefab) | Unity生成滚动列表／详情／附件／六个操作按钮及确认窗口，全部脚本和文本引用序列化绑定。 |
| 新增 | [client/Assets/Prefabs/UI/Panel/Mail/UIMail.prefab.meta](D:/apk/TowerDefence/Assets/Prefabs/UI/Panel/Mail/UIMail.prefab.meta) | 新增Unity目录／脚本／预制体稳定GUID与导入元数据。 |
| 修改 | [client/Tools/SessionVerification/LoopbackGameServer.cs](D:/apk/TowerDefence/Tools/SessionVerification/LoopbackGameServer.cs) | 增加可注入业务响应及丢响应测试入口；为其他恢复测试提供默认邮件摘要／道具响应，不改变生产传输。 |
| 新增 | [client/Tools/MailVerification/Run-Verification.ps1](D:/apk/TowerDefence/Tools/MailVerification/Run-Verification.ps1) | 新增隔离Unity生成／测试入口；真实模式只启动自有临时服务、检查端口占用，并在finally清理账号和停止所有自有服务。 |
| 新增 | [client/Tools/MailVerification/Cleanup-TestAccount.ps1](D:/apk/TowerDefence/Tools/MailVerification/Cleanup-TestAccount.ps1) | 只接受审计文件中严格zmail_测试用户名，在本机zombie库定向清理相关角色、邮件、道具与账号。 |
| 新增 | [client/Tools/MailVerification/MailProtocolFixture.cs](D:/apk/TowerDefence/Tools/MailVerification/MailProtocolFixture.cs) | 新增邮件协议端点模型，覆盖55封分页、版本变化、权限、奖励和写响应丢失场景。 |
| 新增 | [client/Tools/MailVerification/MailSmoke.cs](D:/apk/TowerDefence/Tools/MailVerification/MailSmoke.cs) | 通过实际预制体按钮／事件／Manager／Network联调真实后端和受控端点，覆盖全套操作、重连、取消和只读核对，渲染截图。 |
| 新增 | [server/docs/ZombieMailFrontend.png](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/docs/ZombieMailFrontend.png) | 新增真实后端联调后的面板渲染预览，交付产物，非游戏资源。 |
| 新增 | [server/docs/ZombieMailFrontend.md](D:/BaiduNetdiskDownload/CSharpGame_js/CSharpGameServer/docs/ZombieMailFrontend.md) | 记录客户端架构、协议、操作／资产语义、未读刷新、重连恢复和验证运行方式。 |

## 单独列出的产物与临时文件

- 本清单：docs/ZombieMailFrontendChanges.md；完整差异：docs/ZombieMailFrontend.patch。两份生成交付文件不递归加入差异。
- 新增二进制渲染预览：[ZombieMailFrontend.png](ZombieMailFrontend.png)，不是游戏运行资源。
- 临时源文件快照、manifest、Program未变基线、完整编译／网络／恢复测试日志、integration-final.log和smoke-final.log：[任务临时目录](C:/Users/Windows/AppData/Local/Temp/zombie-mail-20261112)。
- 隔离Unity工程、生成按钮模板、build.log／smoke.log、真实服务启动日志、测试账号审计文件和截图：[Temp/MailVerification](D:/apk/TowerDefence/Temp/MailVerification)。审计账号均已清理。
- 临时服务的配置及二进制位于上述隔离目录ServerBuild、Server/ServerZone、Server/Login、Server/Game；进程均已关闭，不会继续占用端口。
- 本次编译生成／使用的隔离程序集和临时服务二进制清单：[build-artifacts.txt](C:/Users/Windows/AppData/Local/Temp/zombie-mail-20261112/build-artifacts.txt)。这些属于派生构建产物，未手工编辑DLL或EXE。
