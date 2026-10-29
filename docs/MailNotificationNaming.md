# 邮件变化通知命名统一（历史记录）

2026-10-28：124 号邮件变化推送及两端 ResMailChanged 已移除。当前邮件使用请求响应与手动刷新；以下保留当时的命名变更记录。

按项目主动下发消息的 `Res…` / `e…Res` 命名，将 NotifyMailChanged / eMailChanged 统一为 ResMailChanged / eMailChangedRes。编号 124、字段 1～8、类型、推送时机和业务行为保持不变。红点协议不在此次范围。

服务端根目录：`D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer`；客户端根目录：`D:/iaowu/MiaoWu`。

| 范围 | 修改文件 | 目的 |
|---|---|---|
| server | `GameServer/src/Game/Constants.cs` | 命令改为 eMailChangedRes，值保持 124。 |
| server | `GameServer/src/Game/Datas/Messages/MailMessage.cs` | 协议改名 ResMailChanged，并按现有协议格式分行定义字段。 |
| server | `GameServer/src/Game/Entries/Modules/MailModule.cs` | 同步 ResMailChanged 命名与调用。 |
| server | `GameServer/src/Framework/Core/serializer/GoogleProtocolCodec.Generated.cs` | 重新生成编解码器，仅替换对应协议类型名。 |
| server | `GameServerTests/Mail/MailRulesTests.cs` | 更新类型引用；服务端增加 124 编号及原字段编码的兼容断言。 |
| server | `GameServerTests/Mail/MailMySqlTests.cs` | 更新通知类型引用，保留原测试行为。 |
| client | `Assets/Scripts/HotUpdate/Game/Protocol/Definitions/Cmd.cs` | 命令改为 eMailChangedRes，值保持 124。 |
| client | `Assets/Scripts/HotUpdate/Game/Protocol/Messages/MailMessage.cs` | 协议改名 ResMailChanged，并按现有协议格式分行定义字段。 |
| client | `Assets/Scripts/HotUpdate/Game/Protocol/Network/GoogleProtocolCodecNetwork.cs` | 重新生成编解码器，仅替换对应协议类型名。 |
| client | `Assets/Scripts/HotUpdate/Game/Mail/Manager/MailManager.cs` | 同步 ResMailChanged 命名与调用。 |
| client | `Assets/Scripts/HotUpdate/Game/Mail/Manager/MailboxStateManager.cs` | 同步 ResMailChanged 命名与调用。 |
| client | `Tools/MailTests/MailboxTests.cs` | 更新通知类型引用，保留原测试行为。 |

新增交付文件：`docs/MailNotificationNaming.patch`（上述全部真实差异）、`docs/MailNotificationNaming.md`（本清单）。没有删除或重命名物理文件。新增元数据不需要生成，原 MailMessage.cs 的 GUID 保持不变。

验证：服务端构建、11 项邮件测试、客户端 HotUpdate 构建、8 项客户端邮件测试、真实 TCP/WebSocket 游戏服协议验证通过。原 MiaoWu.NetworkAuth.Tests 工程仍缺少 7 个已有测试源文件，编译未通过。

临时快照、一次性替换脚本 refactor.py、差异生成 report.py、manifest.json、编解码器与构建/测试日志位于 `C:/Users/Windows/AppData/Local/Temp/mail-notification-naming`。真实协议验证复用临时 smoke 工程，只操作随机命名的验证数据库，验证后销毁。构建产生的 DLL、PDB、缓存及 Temp 输出属于派生产物，不纳入源码补丁；保留已有工作区改动。
