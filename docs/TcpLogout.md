# TCP 登出与连接超时

客户端调用 `await UserLoginMgr.Singleton.LogoutAsync()`。重复调用复用当前登出操作。
该入口取消登录、选服、鉴权及恢复任务，停止心跳和重连，先断开 TCP，再请求登录服，
最后清空账号/token 并返回 `UIPanel_Login`。记住的账号密码和按账号保存的未确认经济操作保留。
现有 `LoginOutBTN` 已绑定此入口；游戏内按钮可直接调用同一个方法。

## HTTP 协议

- 路由：登录服 `POST /ReqUserLogout`，游戏服公共路由不开放。
- 请求：AUTH 101 / cmd 1008，`ReqUserLogout.accessToken` 为 protobuf 字段 1。
- 响应：AUTH 101 / cmd 1009，`ResUserLogout.status` 为字段 1，`errorMessage` 为字段 2。
- 编码：沿用登录的标准 22 字节业务头与 protobuf，整个请求包转换为 Base64 文本。
- `OK` 表示所有游戏服已确认失效通知；`SystemErr` 加 pending 文案表示凭证已撤销，通知仍在重试。
- `AccountIsNotLogin` 表示 token 未知或已过期。客户端仍完成本地退出。

登录服复用 cmd 1007 签名失效通知，以更高版本的撤销标记阻止旧票据鉴权。
旧会话的重复通知不会关闭更新版本的新连接。重试只更新时间戳、nonce 和签名。

## 时间与清理

- 单个通知 HTTP 超时 2 秒，多目标并行通知。
- 首次失败后等待 1、2、5 秒，之后每 10 秒重试，仅处理未确认目标。
- 截止时间为原 token 过期时间加票据有效期；截止仍失败时记录错误并删除任务。
- 已完成记录保留至原 token 过期，供重复登出返回同一结果。
- 客户端 HTTP 等待上限 10 秒，失败时显示“连接已断开，服务端登出未确认”。
- `GameConnectionIdleTimeoutSeconds` 默认 60，`GameConnectionCheckIntervalSeconds` 默认 5，必须是正整数。
- 已鉴权直连 TCP 连续没有有效请求达到阈值后关闭，默认正常运行下约 60～65 秒。
- 心跳和正常业务请求均刷新单调计时；非法包不刷新。关闭前再次检查活动时间和连接身份。
- 原有未鉴权连接 10 秒超时继续保留。超时只清理连接，不撤销 accessToken。

通知任务仅保存在内存中，登录服重启会丢失；通知请求根本未到登录服时，服务端不知道用户主动登出。
这两种情况下，TCP 超时负责回收失联连接，token 仍依靠过期或新登录替代。
WebSocket、网关和对局规则未纳入此次改动。

## 验证

运行 `dotnet test GameServerTests/GameServerTests.csproj --filter "Category!=Database&Category!=Performance"`。
`LogoutAndTcpLifecycleTests` 覆盖撤销、幂等、多目标重试、通知超时、任务截止、旧票据拒绝、
旧退出事件保护新连接，以及有效/无效数据对空闲计时的影响。

Unity 运行时验收：正常登录后调用登出、重复点击、断网后登出、选服期间登出、重新登录。
确认返回登录入口、异常提示显示、不会自动重连，普通断线仍可重连。
