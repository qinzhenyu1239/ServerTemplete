# 客户端红点 Model / Manager 分层调整

Models 只保存数据，处理逻辑移入 Manager。服务端、协议、数据库及邮件代码未改动。

| 操作 | 文件（客户端根目录 D:/iaowu/MiaoWu） | 内容 |
|---|---|---|
|修改|Assets/Scripts/HotUpdate/Game/RedDot/Models/RedDotTreeModel.cs|只保留只读集合视图和状态/版本属性；删除操作方法、校验、汇总及事件。|
|新增|Assets/Scripts/HotUpdate/Game/RedDot/Models/RedDotNodeModel.cs|从原树文件拆出独立的节点数据类型。|
|新增|Assets/Scripts/HotUpdate/Game/RedDot/Manager/RedDotTreeManager.cs|接管原树算法、状态修改、版本检查、查询和 Changed 通知；普通 C# 类，方便独立测试。|
|修改|Assets/Scripts/HotUpdate/Game/RedDot/Manager/RedDotManager.cs|网络流程调用树 Manager；对 UI 提供 IsActive、GetNode 和 Changed，Tree 仅供读取数据。|
|修改|Assets/Scripts/HotUpdate/UI/Shared/RedDot/RedDotBinding.cs|改为订阅 Manager 和通过 Manager 查询状态，保持启用订阅/禁用解绑。|
|修改|Tools/RedDotTests/RedDot.Tests.csproj|编译实际数据模型和树 Manager，测试继续脱离 Unity 运行。|
|修改|Tools/RedDotTests/RedDotTreeTests.cs|原七项测试转测 Manager，新增纯数据分层检查和汇总后通知测试。|
|修改|HotUpdate.csproj|Unity 自动登记新脚本；保留 Unity 本次重生成的内容。|
|新增|上述 RedDotNodeModel.cs.meta、RedDotTreeManager.cs.meta|Unity 自动生成资源 GUID；保留已有 TreeModel 和 UI 脚本 GUID。|

服务端仓库文档变化：更新 docs/RedDotSystem.md 的查询接口、职责和测试说明；更新 docs/RedDotChanges.md 与 docs/RedDotSystem.patch 的最终功能差异；新增本说明及 docs/RedDotModelRefactor.patch。

验证：HotUpdate.csproj 编译通过；独立红点测试 9 项通过。原 MiaoWu.NetworkAuth.Tests.csproj 仍因 7 个已不存在的 Assets/Tests/Editor 源文件报错，本次未恢复这些文件。未执行 Unity Editor 场景视觉验收。

完整本次差异见 RedDotModelRefactor.patch，基准为本轮修改前快照，不混入此前红点开发或其他已有改动。快照和验证日志位于 C:/Users/Windows/AppData/Local/Temp/codex-reddot-20261023/；构建过程中更新了客户端和独立测试的 bin/obj 产物。
