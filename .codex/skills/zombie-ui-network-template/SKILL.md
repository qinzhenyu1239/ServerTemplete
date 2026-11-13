---
name: zombie-ui-network-template
description: Build or refactor TowerDefence client network features using MiaoWu-style UI, Manager, Network layers and bidirectional UniFramework events. Use for zombie project protocol-driven UI and MainInterface feature panels.
---

# 僵尸项目客户端事件模板

客户端为 D:/apk/TowerDefence，服务端为本技能所在仓库。MiaoWu（D:/iaowu/MiaoWu）仅作只读参考，不修改其代码或技能。

## 两项必需模式

1. 使用 UI → Manager → Network 分层。Network 复用 TowerDefence 的 SessionNetwork；不新增传输层。
2. UI／Manager 双向事件交互：UI 发布请求事件，Manager 监听后调用 Network，再发布加载、成功、失败或清空事件，UI 监听结果更新展示。UI 不直接调用 Manager 业务方法或 Network；Manager 不持有 UI 组件引用。

## 实施基准

先读取目标项目当前调用链与协议。喵呜参考 Assets/Scripts/HotUpdate/UI/Gacha/UIGacha.cs，以及 Game/Gacha 下 Manager/GachaManager.cs、Network/GachaNetwork.cs、Events；路径失效时用 rg 定位，不能照抄旧路径。
TowerDefence 现有基础设施在 Assets/Rogue/Scripts/HotUpdate：Game/Shared/Network/SessionNetwork.cs、UI/UIMgr.cs、UI/IUIPanel.cs。完整本项目示例为 Game/PlayerBasicInfo 与 UI/Panel/PlayerBasicInfo。

## 职责和生命周期

- UI 在 Awake 绑定按钮、OnDestroy 解绑，OnEnable 订阅、OnDisable 退订；OnSHow 初始化并发布首个查询事件。UI 仅保存展示和请求匹配所需的短期状态。
- Manager 在功能初始化时注册请求及会话事件，销毁时退订；初始化必须幂等，确保 UI 发事件前已就绪。Manager 保存权威资料快照及请求状态。
- 使用 UniFramework.Event，不创建事件总线。请求带唯一请求标识和取消令牌；结果带同一标识。会话级清空事件广播所有面板；单次结果只能更新匹配请求的面板。
- Network 只构造协议、调用 SessionNetwork、接收响应并设置超时与取消，不保存 UI 或全局业务状态。
- 关联面板、账号、连接和功能生命周期取消令牌。隐藏面板取消自己的读请求；注销、断线、切换连接清空快照。检查账号及连接代次，旧响应不得写入新会话。
- 加载期间禁用相关按钮，Manager 拒绝重复请求；错误必须通过结果事件恢复 UI。写请求的取消及重试规则以服务端幂等契约为准，不能机械套用读请求。

## 验证

核对协议号、字段类型、服务端语义；编译实际 TowerDefence 项目及相关测试，不编译 MiaoWu 作为替代。验证重复点击、关闭、超时、注销、断线、重连及过期响应；确保没有重复订阅、悬空监听或 UI 直接调用 Manager 业务方法。按 show-code-diffs 要求保留已有改动，提供完整实际差异及验证限制。
