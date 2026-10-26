# 本次红点系统变更清单

下列差异以本次修改前的工作区快照为基准；不包含用户此前已有的代码修改。没有删除或重命名项目源文件。

完整实际差异：[RedDotSystem.patch](RedDotSystem.patch)。安装、接口和验证说明：[RedDotSystem.md](RedDotSystem.md)。

## 项目文件

| 操作 | 文件 | 内容及原因 |
|---|---|---|
| 新增 | [RedDotConfig.xlsx](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/Excels/RedDotConfig.xlsx) | Luban 三行表头及样式与 ResonanceLv 一致，七个预留节点全部禁用；两处副本内容一致。 |
| 新增 | [RedDotMySqlTests.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServerTests/RedDot/RedDotMySqlTests.cs) | 规则、协议、临时 MySQL 事务或实际客户端红点树测试。 |
| 新增 | [RedDotTests.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServerTests/RedDot/RedDotTests.cs) | 规则、协议、临时 MySQL 事务或实际客户端红点树测试。 |
| 新增 | [RedDotConfig.csv](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/Configs/Csvs/RedDotConfig.csv) | 七节点预留配置，全部禁用。 |
| 修改 | [GoogleProtocolCodec.Generated.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/serializer/GoogleProtocolCodec.Generated.cs) | 生成红点协议编解码；不移除原有已支持合同。 |
| 修改 | [ExcelUtils.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/utils/ExcelUtils.cs) | 泛型与非泛型配置读取入口支持 bool 字段。 |
| 修改 | [Constants.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Constants.cs) | 追加 PLAYER 红点命令 95–99，保留原命令号。 |
| 修改 | [ConfigMgr.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/Configs/ConfigMgr.cs) | 启动预加载 RedDotConfig。 |
| 新增 | [GM_RedDotMgr.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/GM_RedDot/GM_RedDotMgr.cs) | 叶子持久化、查看记录、事务同步和提交后推送。 |
| 新增 | [RedDotDefinitions.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/GM_RedDot/RedDotDefinitions.cs) | 领域快照、配置图校验与稳定配置版本。 |
| 新增 | [Playerreddot.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Datas/DbEntities/Playerreddot.cs) | 新增红点数据库实体和联合主键映射。 |
| 新增 | [RedDotConfig.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Datas/Excels/RedDotConfig.cs) | 与配置字段一致的类型定义。 |
| 新增 | [RedDotComponent.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Datas/GMEntities/Components/RedDotComponent.cs) | 保存当前连接的有界红点状态及同步版本。 |
| 修改 | [GM_Entity.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Datas/GMEntities/GM_Entity.cs) | 在线玩家持有 RedDotComponent。 |
| 新增 | [RedDotMessage.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Datas/Messages/RedDotMessage.cs) | 两端一致的节点、状态及五种协议定义。 |
| 新增 | [RedDotController.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Entries/Controllers/RedDotController.cs) | 初始化及查看请求路由。 |
| 新增 | [RedDotModule.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Entries/Modules/RedDotModule.cs) | 登录/参数校验和协议响应、通知映射。 |
| 修改 | [GameServer.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/GameServer.cs) | 初始化通用红点模块；不再强制校验业务判断方法注册。 |
| 新增 | [RedDotChanges.md](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/docs/RedDotChanges.md) | 接入、安装、验证及本次完整变更说明。 |
| 新增 | [RedDotSystem.md](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/docs/RedDotSystem.md) | 接入、安装、验证及本次完整变更说明。 |
| 新增 | [20261023_001_reddot.sql](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/sql/migrations/20261023_001_reddot.sql) | 已有库的可重复迁移，保留旧邮件数据。 |
| 修改 | [ugame_10001.sql](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/sql/ugame_10001.sql) | 完整建库结构同步红点表；邮件结构恢复原状，保留原有玩家资料改动。 |
| 修改 | [Export-LubanExcelToCsv.ps1](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/tools/LubanExcelToCsv/Export-LubanExcelToCsv.ps1) | 兼容三行和四行 Excel 表头，正确读取说明及首条数据。 |
| 修改 | [LubanExcelToCsv.json](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/tools/LubanExcelToCsv/LubanExcelToCsv.json) | 现有 Excel → CSV 导出流程登记 RedDotConfig。 |
| 新增 | [RedDotConfig.xlsx](D:/iaowu/MiaoWu/Assets/Luban/Template/Datas/RedDotConfig.xlsx) | Luban 三行表头及样式与 ResonanceLv 一致，七个预留节点全部禁用；两处副本内容一致。 |
| 新增 | [RedDotConfig.xlsx.meta](D:/iaowu/MiaoWu/Assets/Luban/Template/Datas/RedDotConfig.xlsx.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 修改 | [Cmd.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/Protocol/Definitions/Cmd.cs) | 追加 PLAYER 红点命令 95–99，保留原命令号。 |
| 新增 | [RedDotMessage.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/Protocol/Messages/RedDotMessage.cs) | 两端一致的节点、状态及五种协议定义。 |
| 新增 | [RedDotMessage.cs.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/Protocol/Messages/RedDotMessage.cs.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 修改 | [GoogleProtocolCodecNetwork.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/Protocol/Network/GoogleProtocolCodecNetwork.cs) | 生成红点协议编解码；不移除原有已支持合同。 |
| 新增 | [RedDot.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [Manager.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Manager.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [RedDotManager.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Manager/RedDotManager.cs) | 网络与会话流程调用树 Manager，对 UI 提供查询和通知。 |
| 新增 | [RedDotManager.cs.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Manager/RedDotManager.cs.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [RedDotTreeManager.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Manager/RedDotTreeManager.cs) | 接管配置校验、版本判断、状态应用、父子汇总和通知。 |
| 新增 | [RedDotTreeManager.cs.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Manager/RedDotTreeManager.cs.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [Models.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Models.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [RedDotNodeModel.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Models/RedDotNodeModel.cs) | 独立的单节点数据模型。 |
| 新增 | [RedDotNodeModel.cs.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Models/RedDotNodeModel.cs.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [RedDotTreeModel.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Models/RedDotTreeModel.cs) | 纯数据模型，保存节点/状态集合及版本，不含操作和事件。 |
| 新增 | [RedDotTreeModel.cs.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Models/RedDotTreeModel.cs.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [Network.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Network.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [RedDotNetwork.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Network/RedDotNetwork.cs) | 请求封装及超时、重复请求策略。 |
| 新增 | [RedDotNetwork.cs.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Network/RedDotNetwork.cs.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [RedDotRecoveryHandler.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Network/RedDotRecoveryHandler.cs) | 登录及重连后自动初始化红点。 |
| 新增 | [RedDotRecoveryHandler.cs.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Game/RedDot/Network/RedDotRecoveryHandler.cs.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [RedDot.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/UI/Shared/RedDot.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 新增 | [RedDotBinding.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/UI/Shared/RedDot/RedDotBinding.cs) | UI 根据节点 ID 自动订阅、刷新和解绑。 |
| 新增 | [RedDotBinding.cs.meta](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/UI/Shared/RedDot/RedDotBinding.cs.meta) | Unity 资源 GUID / 文件夹元数据，确保新增资源可识别。 |
| 修改 | [HotUpdate.csproj](D:/iaowu/MiaoWu/HotUpdate.csproj) | 将新增客户端脚本加入当前编译项目。 |
| 新增 | [RedDot.Tests.csproj](D:/iaowu/MiaoWu/Tools/RedDotTests/RedDot.Tests.csproj) | 规则、协议、临时 MySQL 事务或实际客户端红点树测试。 |
| 新增 | [RedDotTreeTests.cs](D:/iaowu/MiaoWu/Tools/RedDotTests/RedDotTreeTests.cs) | 规则、协议、临时 MySQL 事务或实际客户端红点树测试。 |

## 验证与限制

- 服务端编译通过，客户端 HotUpdate 编译通过；红点服务端测试 6 项、客户端测试 9 项和隔离 MySQL 测试 1 项通过。
- Game 角色真实启动、TCP 鉴权、完整禁用配置、空状态快照和非法查看拒绝均通过。未迁移用户业务数据库。
- 全量非数据库测试 93 通过、1 失败；已用修改前 codec 单独编译验证，失败来自已有玩家资料更新请求未生成编解码。
- 原客户端网络测试项目引用的 7 个源文件不存在，无法完整编译；未恢复用户移除的文件。Unity 场景/视觉验收未运行。

## 临时与构建文件

- 修改前快照、工作簿生成器与预览、隔离游戏服 smoke 工程、原 codec 验证工程及日志位于 `C:/Users/Windows/AppData/Local/Temp/codex-reddot-20261023/`；不作为游戏资源交付。
- 验证日志另外保存为同级 `codex-reddot-*.log`。临时数据库均已删除，临时含连接信息的 smoke.config 已清理。
- 工作簿自动产生的 inspect.ndjson 已移入上述临时目录。
- dotnet 编译产生/更新服务端、服务端测试、客户端及独立红点测试的 bin/obj 等构建产物；这些目录中部分文件在本任务前已修改，不纳入源代码差异。
- Unity 新增的全部 .meta 已在上表逐项列出。

## 本次撤回邮件接入

- GM_MailMsgMgr、Mailmessage、PlayerModule 的邮件处理已恢复至本功能实施前，取消默认期限、容量清理、定时扫描及邮件红点事务。
- 1000/1001 节点在 Excel 和 CSV 中禁用；SQL 和迁移脚本不再增加邮件字段。
- MySQL 测试改用独立测试业务表，不依赖邮件。
- 撤回前后完整差异：[RedDotMailDetach.patch](RedDotMailDetach.patch)。

## 初始化只读修订

- 初始化只读取已有状态，缺少记录按隐藏处理；不执行 upsert、不校准业务条件。
- 业务刷新和查看提示激活由显式业务事件触发；详情见 [RedDotReadonlyInitChanges.md](RedDotReadonlyInitChanges.md)，本次完整差异见 [RedDotReadonlyInit.patch](RedDotReadonlyInit.patch)。

## 客户端 Model 分层修订

- Models 只保存数据；RedDotTreeManager 承担树规则与事件，UI 改为通过 RedDotManager 查询和订阅。
- 完整文件说明见 [RedDotModelRefactorChanges.md](RedDotModelRefactorChanges.md)，本轮差异见 [RedDotModelRefactor.patch](RedDotModelRefactor.patch)。
