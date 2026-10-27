# 邮件系统本次文件变更清单
此清单按工作开始时的文件快照比较，不包含用户先前改动。无删除或重命名源码；新增/修改逐项如下。完整真实代码差异见 [MailSystem.patch](MailSystem.patch)，接入与测试说明见 [MailSystem.md](MailSystem.md)。
共 57 个源码、配置、测试、Unity 元数据及接入说明文件。根目录：server=`D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer`；client=`D:/iaowu/MiaoWu`。
| 范围 | 操作 | 文件 | 改动与目的 |
|---|---|---|---|
| server | 修改 | `GameServer/App.config` | 注明必填容量配置，不给出生产默认值。 |
| server | 修改 | `GameServer/src/Framework/Core/serializer/GoogleProtocolCodec.Generated.cs` | 重新生成邮件 Google 编解码器；保留原有已支持契约。 |
| server | 修改 | `GameServer/src/Game/Constants.cs` | 两端同步邮件命令号和业务错误码。 |
| server | 修改 | `GameServer/src/Game/Core/GM_Item/GM_ItemMgr.cs` | 复用道具模块完成事务内锁定、条件入账和提交后组件同步。 |
| server | 修改 | `GameServer/src/Game/Core/GM_MailMessage/GM_MailMsgMgr.cs` | 邮件生命周期、SQL 事务、去重、发奖、清理与提交后通知。 |
| server | 新增 | `GameServer/src/Game/Core/GM_MailMessage/MailDefinitions.cs` | 领域命令、返回值、排序和发送校验。 |
| server | 新增 | `GameServer/src/Game/Datas/DbEntities/Mailattachment.cs` | 邮件主表独立时间状态与有序附件实体。 |
| server | 修改 | `GameServer/src/Game/Datas/DbEntities/Mailmessage.cs` | 邮件主表独立时间状态与有序附件实体。 |
| server | 新增 | `GameServer/src/Game/Datas/GMEntities/Components/MailComponent.cs` | 有界连接摘要和版本组件。 |
| server | 修改 | `GameServer/src/Game/Datas/GMEntities/GM_Entity.cs` | 添加 uMail 在线组件字段。 |
| server | 新增 | `GameServer/src/Game/Datas/Messages/MailMessage.cs` | 七组邮件协议、变化通知、摘要/详情/附件与分页字段。 |
| server | 修改 | `GameServer/src/Game/Datas/Messages/PlayerMessage.cs` | 旧邮件列表改成生成器已支持的 List，保持 protobuf 标签。 |
| server | 新增 | `GameServer/src/Game/Entries/Controllers/MailController.cs` | 新增邮件请求路由。 |
| server | 新增 | `GameServer/src/Game/Entries/Modules/MailModule.cs` | 登录/参数校验、配置加载、协议组装与旧协议适配。 |
| server | 修改 | `GameServer/src/Game/Entries/Modules/PlayerModule.cs` | 旧邮件请求统一转发 MailModule，移除旧 Manager 绕过路径。 |
| server | 修改 | `GameServer/src/Game/GameServer.cs` | 游戏服注册邮件配置加载、定时器及关闭清理。 |
| server | 新增 | `GameServerTests/Mail/MailMySqlTests.cs` | 隔离 MySQL 的迁移、去重、并发、回滚和容量/过期测试。 |
| server | 新增 | `GameServerTests/Mail/MailRulesTests.cs` | 邮件规则、登录校验、协议长整数与旧邮件契约测试。 |
| server | 新增 | `docs/MailSystem.md` | 上线配置、迁移、发送示例、协议、UI 绑定和验证说明。 |
| server | 新增 | `sql/migrations/20261026_001_mail.sql` | 可重复执行的非破坏性增量迁移，保留旧字段与数据。 |
| server | 修改 | `sql/ugame_10001.sql` | 同步完整建库邮件表和索引、附件表。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Events.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Events/MailUpdatedEvent.cs` | 邮件状态和操作结果事件。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Events/MailUpdatedEvent.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Manager.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Manager/MailManager.cs` | 客户端操作协调、推送、账号代际、只读恢复及背包刷新。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Manager/MailManager.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Manager/MailboxStateManager.cs` | 版本检查、冻结排序/置灰快照、到期移除和详情同步。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Manager/MailboxStateManager.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Models.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Models/MailModels.cs` | 纯数据模型，不包含业务方法。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Models/MailModels.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Network.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Network/MailNetwork.cs` | 请求、超时、重复请求策略、同版本分页及写后只读补齐。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Network/MailNetwork.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Network/MailRecoveryHandler.cs` | 注册登录和重连后的邮箱摘要/列表恢复。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Mail/Network/MailRecoveryHandler.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 修改 | `Assets/Scripts/HotUpdate/Game/Protocol/Definitions/Cmd.cs` | 两端同步邮件命令号和业务错误码。 |
| client | 修改 | `Assets/Scripts/HotUpdate/Game/Protocol/Definitions/Respones.cs` | 两端同步邮件命令号和业务错误码。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Protocol/Messages/MailMessage.cs` | 七组邮件协议、变化通知、摘要/详情/附件与分页字段。 |
| client | 新增 | `Assets/Scripts/HotUpdate/Game/Protocol/Messages/MailMessage.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 修改 | `Assets/Scripts/HotUpdate/Game/Protocol/Network/GoogleProtocolCodecNetwork.cs` | 重新生成邮件 Google 编解码器；保留原有已支持契约。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMail.cs` | 可绑定邮件 UI/条目/奖励/链接/入口提示模板。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMail.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMailAttachment.cs` | 可绑定邮件 UI/条目/奖励/链接/入口提示模板。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMailAttachment.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMailLinkHandler.cs` | 可绑定邮件 UI/条目/奖励/链接/入口提示模板。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMailLinkHandler.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMailRow.cs` | 可绑定邮件 UI/条目/奖励/链接/入口提示模板。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMailRow.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMailUnreadBadge.cs` | 可绑定邮件 UI/条目/奖励/链接/入口提示模板。 |
| client | 新增 | `Assets/Scripts/HotUpdate/UI/Mail/UIMailUnreadBadge.cs.meta` | Unity 资源 GUID 和导入元数据，供新增脚本/目录识别。 |
| client | 修改 | `HotUpdate.csproj` | 仅把新增邮件脚本加入编译，保留开始时已有项目改动。 |
| client | 新增 | `Tools/MailTests/Mail.Tests.csproj` | 可独立执行的客户端状态测试工程。 |
| client | 新增 | `Tools/MailTests/MailboxTests.cs` | 客户端显示快照、版本、重连、到期与数据模型测试。 |

## 差异和清单文件

- `docs/MailSystem.patch`：以上源码/配置/测试/说明的完整新增、删除行，不把补丁文件自身嵌入补丁。
- `docs/MailChanges.md`：本清单，作为生成报告单列。

## 验证与辅助文件

临时目录：`C:/Users/Windows/AppData/Local/Temp/codex-mail-20261026`。其中 `before/` 与 `manifest.json` 是任务开始快照；`generate_protocol.py`、`schema.py`、`client_protocol.py` 是一次性生成辅助脚本，不能重复运行覆盖最终调整；`report.py` 生成本次差异和清单。

- `all-unit-tests.log`：1905 字节验证日志。
- `build.log`：5477 字节验证日志。
- `client-build.log`：300497 字节验证日志。
- `client-codegen.log`：245 字节验证日志。
- `client-tests.log`：770 字节验证日志。
- `network-build.log`：162659 字节验证日志。
- `network-tests.log`：0 字节验证日志。
- `server-codegen.log`：677 字节验证日志。
- `smoke.log`：17724 字节验证日志。
- `tests.log`：3283 字节验证日志。
- `smoke/Program.cs`、`smoke/Smoke.csproj`：临时 Game 角色 TCP/WebSocket 协议测试；Configs/nlog.config 为运行所需副本，bin/obj 为编译产物。验证后销毁临时数据库和包含凭据的 smoke.config。
- 两个工程构建更新了 bin/obj/Temp 下的 DLL、PDB、缓存等派生产物；不是手写源码交付，不纳入源码补丁。已有被 Git 跟踪的构建产物仍可能显示修改，没有回滚用户原有工作。
- Unity 导入生成/更新项目编译缓存和 .meta；上表列出了邮件新增元数据。

## 结果与边界

服务端邮件 9 项、客户端邮件 8 项通过；真实 TCP/WebSocket 协议通过；服务端与 HotUpdate 构建通过。完整非数据库测试 97 通过、1 既有玩家资料 codec 失败、1 跳过；原客户端网络测试工程缺少 7 个已有源文件，编译未通过。详见 MailSystem.md。没有迁移现有业务库，没有配置生产邮箱容量，没有制作 prefab/场景或执行 Unity 视觉验收。

## 工作区派生产物逐项清单

下列服务端已被 Git 跟踪的构建产物在构建后显示变更。任务开始未保存二进制快照，因此不把与 Git 提交之间的全部差异归因于本次修改，也不回滚它们。DLL/EXE/PDB 为重新编译的二进制，其余为生成配置/缓存。

- `GameServer/bin/Debug/net8.0/GameServer.dll`：构建派生产物。
- `GameServer/bin/Debug/net8.0/GameServer.dll.config`：构建派生产物。
- `GameServer/bin/Debug/net8.0/GameServer.exe`：构建派生产物。
- `GameServer/bin/Debug/net8.0/GameServer.pdb`：构建派生产物。
- `GameServer/obj/Debug/net8.0/GameServer.AssemblyInfo.cs`：构建派生产物。
- `GameServer/obj/Debug/net8.0/GameServer.AssemblyInfoInputs.cache`：构建派生产物。
- `GameServer/obj/Debug/net8.0/GameServer.assets.cache`：构建派生产物。
- `GameServer/obj/Debug/net8.0/GameServer.csproj.CoreCompileInputs.cache`：构建派生产物。
- `GameServer/obj/Debug/net8.0/GameServer.dll`：构建派生产物。
- `GameServer/obj/Debug/net8.0/GameServer.pdb`：构建派生产物。
- `GameServer/obj/Debug/net8.0/apphost.exe`：构建派生产物。
- `GameServer/obj/Debug/net8.0/ref/GameServer.dll`：构建派生产物。
- `GameServer/obj/Debug/net8.0/refint/GameServer.dll`：构建派生产物。
- `GameServer/obj/GameServer.csproj.nuget.dgspec.json`：构建派生产物。
- `GameServer/obj/GameServer.csproj.nuget.g.props`：构建派生产物。
- `GameServer/obj/project.assets.json`：构建派生产物。
- `GameServer/obj/project.nuget.cache`：构建派生产物。
