# TCP 主动登出：逐行修改说明

这份说明按当前代码的真实行号，列出服务端本次 diff、新增文件，以及客户端本次登出修改。每个表格一行对应一行代码，包括空行和括号。代码没有被再次修改；本文件仅解释实现。

客户端 `Assets/Scripts` 是当前使用的源代码目录。Git 中的 `Assets/Rogue/Scripts` 与当前代码之间还存在本次任务前的修改；这里根据本次编辑记录逐处识别，未将那些预存修改归入登出功能。

## 先读调用顺序

1. 客户端按钮 → `UserLoginMgr.LogoutAsync` → 取消旧登录生命周期 → `NetworkMgr.DisconnectAsync` → HTTP `/ReqUserLogout`。
2. 登录服 `HttpAuthControll` → `AuthModule` → `GM_AuthSessionMgr.LogoutAsync`：账号锁内撤销指定 token、生成或复用版本标记，释放账号锁后通知游戏服。
3. 内部通知仍是 HTTP POST，正文是标准业务包的 Base64；包内 AUTH 101、命令 1007。游戏服沿用已有签名校验、会话版本屏障和关闭旧连接代码。
4. 通知未全部确认时，响应 SystemErr 并后台重试；客户端仍清空本地会话、关闭 UI、返回登录页。
5. 普通 TCP 断线和空闲超时只清理连接，不撤销 accessToken，所以原有重新申请票据的重连流程仍能执行。

## 阅读时需要区分的实际行为

- `Pending` 为空就不再重试；完成记录仍保留到原 token 到期，供重复登出使用。因此“通知完成”和“删除幂等记录”不是同一时刻。
- HTTP 请求的十秒上限只包围 HTTP 请求；前面的 TCP 断开和后面的场景加载不在这个十秒计时器中。
- 无 token 的客户端分支直接按本地成功处理，`ServerConfirmed=true` 在该分支不是一次真实的远端确认。
- 游戏服处理 1007 的校验与版本屏障是已有实现，本次主要复用它；没有重写那段代码。
- `ComputeSizeReqUserLogout` / `ComputeSizeResUserLogout` 是配套长度辅助方法，当前顶层编解码入口直接调用 Write/Read。
- `ReturnToLoginAsync` 中的场景加载/显示如果抛异常，内层 finally 仍恢复登出状态并发布完成事件，但 `LogoutAsync` 会向调用者传播这个 UI 异常。

## App.config

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/App.config](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/App.config)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 14 | <code>&lt;add key="GameConnectionIdleTimeoutSeconds" value="60"/&gt;</code> | 新增已鉴权连接空闲阈值配置，默认 60 秒。 |
| 15 | <code>&lt;add key="GameConnectionCheckIntervalSeconds" value="5"/&gt;</code> | 新增统一扫描间隔配置，默认 5 秒。 |

## IdSession.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/net/IdSession.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/net/IdSession.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 10 | <code>private long lastValidActivity = System.Diagnostics.Stopwatch.GetTimestamp();</code> | 保存最后有效接收的单调时钟 tick；不受系统墙上时间调整影响。 |
| 11 | <code>public void MarkValidActivity() =&gt; System.Threading.Interlocked.Exchange(</code> | 增加刷新有效活动时间的方法。 |
| 12 | <code>ref lastValidActivity, System.Diagnostics.Stopwatch.GetTimestamp());</code> | 原子写入最新单调时钟 tick，保证接收线程与扫描线程可见。 |
| 13 | <code>public double IdleSeconds =&gt; (System.Diagnostics.Stopwatch.GetTimestamp() -</code> | 取得当前单调时钟 tick，准备计算空闲秒数。 |
| 14 | <code>System.Threading.Interlocked.Read(ref lastValidActivity)) / (double)System.Diagnostics.Stopwatch.Frequency;</code> | 原子读取最后接收 tick，再除以 Stopwatch.Frequency 转换为秒。 |
| 15 | <code>public bool IsIdle(System.TimeSpan timeout) =&gt; IdleSeconds &gt;= timeout.TotalSeconds;</code> | 比较空闲秒数是否达到指定阈值。 |
| 16 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |

## MessageDispatcher.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/net/MessageDispatcher.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/net/MessageDispatcher.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 116 | <code>if (!s.isWebSocket && s.logicServerId == -1)</code> | 直连 TCP 未进入逻辑服时，也需要清掉实体上的旧连接引用。 |
| 117 | <code>Game.Core.EntityMgr.GM_EntityMgr.Instance.ClearDisconnectedSession(s);</code> | 按连接身份清理实体引用，保留实体本身。 |
| 284 | <code>if (msg == null) return;</code> | 解码失败时直接返回，非法消息不能刷新空闲时间。 |
| 285 | <code>s.MarkValidActivity();</code> | 在协议、鉴权、限流检查并成功解码后刷新接收时间。 |
| 286 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |

## SessionMgr.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/net/SessionMgr.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/net/SessionMgr.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 12 | <code>public partial class SessionMgr {</code> | 把 SessionMgr 拆成部分类型；主文件管理连接，Idle 文件管理扫描。 |
| 18 | <code>public object BindingSync { get; } = new object();</code> | 新增统一同步锁，协调账号当前连接映射和玩家连接引用。 |
| 27 | <code>StartIdleChecks();</code> | SessionMgr 初始化时启动符合角色条件的空闲扫描。 |
| 83 | <code>lock (BindingSync)</code> | 所有账号绑定、解绑、踢连接判断都在同一同步锁中执行。 |
| 85 | <code>session.accountId = accountId;</code> | 写入已鉴权账号 ID，操作移动到统一锁内。 |
| 86 | <code>session.authSessionId = authSessionId;</code> | 写入凭证对应会话 ID，供撤销通知比较。 |
| 87 | <code>session.MarkValidActivity();</code> | 鉴权绑定时初始化最后有效活动时间。 |
| 88 | <code>if (accountSessions.TryGetValue(accountId, out IdSession previous) && !ReferenceEquals(previous, session))</code> | 查出同账号旧连接；仅在不是同一对象时关闭。 |
| 89 | <code>previous.client?.CloseAsync();</code> | 请求关闭旧通道；迟到的断线事件受身份校验保护。 |
| 90 | <code>accountSessions[accountId] = session;</code> | 在锁内发布最新账号连接，替代原 AddOrUpdate 回调。 |
| 91 | <code>}</code> | 结束当前代码作用域。 |
| 92 | <code>}</code> | 结束当前代码作用域。 |
| 93 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 94 | <code>public bool IsCurrentConnection(IdSession session)</code> | 提供检查 IdSession 是否仍为账号当前连接的方法。 |
| 95 | <code>{</code> | 开始代码作用域。 |
| 96 | <code>lock (BindingSync)</code> | 所有账号绑定、解绑、踢连接判断都在同一同步锁中执行。 |
| 97 | <code>return session != null && accountSessions.TryGetValue(session.accountId, out IdSession current) && ReferenceEquals(current, session);</code> | 同时检查非空、映射存在和对象身份相同。 |
| 104 | <code>lock (BindingSync)</code> | 所有账号绑定、解绑、踢连接判断都在同一同步锁中执行。 |
| 105 | <code>{</code> | 开始代码作用域。 |
| 106 | <code>if (accountSessions.TryGetValue(session.accountId, out IdSession current) && ReferenceEquals(current, session))</code> | 先比较对象身份，再删除账号映射；检查与删除同处一个锁。 |
| 107 | <code>accountSessions.TryRemove(session.accountId, out _);</code> | 只删除刚刚确认仍属于本连接的账号映射。 |
| 108 | <code>}</code> | 结束当前代码作用域。 |
| 146 | <code>lock (BindingSync)</code> | 所有账号绑定、解绑、踢连接判断都在同一同步锁中执行。 |
| 147 | <code>if (accountSessions.TryGetValue(accountId, out IdSession session) &&</code> | 在锁内读取需要检查会话 ID 的账号连接。 |
| 148 | <code>!string.Equals(session.authSessionId, currentAuthSessionId, System.StringComparison.Ordinal))</code> | 会话 ID 不等于有效标记/新登录 ID 时才踢连接。 |
| 149 | <code>session.client?.CloseAsync();</code> | 关闭确认已经被替代的连接，复用原断线回调。 |

原代码删除/替换原因：

- `public class SessionMgr {`：原类声明增加 partial，允许在新文件中扩展同一类型。
- `session.accountId = accountId;`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `session.authSessionId = authSessionId;`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `accountSessions.AddOrUpdate(accountId, session, (_, previous) =>`：改为统一 BindingSync 锁中的显式查询与赋值，避免字典回调与解绑不共享同步边界。
- `if (!ReferenceEquals(previous, session)) previous.client?.CloseAsync();`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `return session;`：改为统一 BindingSync 锁中的显式查询与赋值，避免字典回调与解绑不共享同步边界。
- `});`：旧结构的边界随对应代码移动或替换，本身无独立业务行为。
- `if (session.accountId <= 0) return;`：解绑统一移入锁内并按实际对象身份判断；无匹配映射时自然不删除。
- `if (accountSessions.TryGetValue(session.accountId, out IdSession current) && ReferenceEquals(current, session))`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `accountSessions.TryRemove(session.accountId, out _);`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `if (accountSessions.TryGetValue(accountId, out IdSession session) &&`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `!string.Equals(session.authSessionId, currentAuthSessionId, System.StringComparison.Ordinal))`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `session.client?.CloseAsync();`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。

## GoogleProtocolCodec.Generated.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/serializer/GoogleProtocolCodec.Generated.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/serializer/GoogleProtocolCodec.Generated.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 18 | <code>if (source is ReqUserLogout logoutRequest) WriteReqUserLogout(output, logoutRequest);</code> | 序列化入口新增登出请求类型分支，调用其写入函数。 |
| 19 | <code>else if (source is ResUserLogout logoutResponse) WriteResUserLogout(output, logoutResponse);</code> | 序列化入口新增登出响应类型分支。 |
| 20 | <code>else if (source is ReqGuestLogin value0)</code> | 原首个消息分支改为 else if，接在新增登出分支之后。 |
| 378 | <code>if (messageType == typeof(ReqUserLogout)) message = ReadReqUserLogout(input);</code> | 反序列化入口新增登出请求分支。 |
| 379 | <code>else if (messageType == typeof(ResUserLogout)) message = ReadResUserLogout(input);</code> | 反序列化入口新增登出响应分支。 |
| 380 | <code>else if (messageType == typeof(ReqGuestLogin)) message = ReadReqGuestLogin(input);</code> | 原首个解码分支改为 else if，接在新增登出分支之后。 |
| 5204 | <code>private static void WriteReqUserLogout(CodedOutputStream output, ReqUserLogout message)</code> | 新增请求 protobuf 写入函数。 |
| 5205 | <code>{</code> | 开始代码作用域。 |
| 5206 | <code>if (message.accessToken != null)</code> | token 非 null 时才写入或计算长度。 |
| 5207 | <code>{</code> | 开始代码作用域。 |
| 5208 | <code>output.WriteTag(1, WireFormat.WireType.LengthDelimited);</code> | 写字段 1 的长度分隔类型标签，后续数据是 token 字符串。 |
| 5209 | <code>output.WriteString(message.accessToken);</code> | 写入 token 字符串。 |
| 5210 | <code>}</code> | 结束当前代码作用域。 |
| 5211 | <code>}</code> | 结束当前代码作用域。 |
| 5212 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 5213 | <code>private static ReqUserLogout ReadReqUserLogout(CodedInputStream input)</code> | 新增请求 protobuf 读取函数。 |
| 5214 | <code>{</code> | 开始代码作用域。 |
| 5215 | <code>var message = new ReqUserLogout();</code> | 创建待填充的请求对象。 |
| 5216 | <code>uint tag;</code> | 保存当前 protobuf 标签。 |
| 5217 | <code>while ((tag = input.ReadTag()) != 0)</code> | 逐字段读取，标签为零表示消息结束。 |
| 5218 | <code>{</code> | 开始代码作用域。 |
| 5219 | <code>switch (WireFormat.GetTagFieldNumber(tag))</code> | 根据字段号选择对应数据读取方式。 |
| 5220 | <code>{</code> | 开始代码作用域。 |
| 5221 | <code>case 1:</code> | 处理字段 1：请求中是 token，响应中是 status。 |
| 5222 | <code>message.accessToken = input.ReadString();</code> | 读取字段 1 字符串，写到请求 token。 |
| 5223 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 5224 | <code>default:</code> | 遇到未定义的 protobuf 字段时走兼容分支。 |
| 5225 | <code>input.SkipLastField();</code> | 跳过未知字段，保持 protobuf 向前兼容。 |
| 5226 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 5227 | <code>}</code> | 结束当前代码作用域。 |
| 5228 | <code>}</code> | 结束当前代码作用域。 |
| 5229 | <code>return message;</code> | 返回已经读取的消息对象。 |
| 5230 | <code>}</code> | 结束当前代码作用域。 |
| 5231 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 5232 | <code>private static int ComputeSizeReqUserLogout(ReqUserLogout message)</code> | 新增请求编码长度辅助函数；当前顶层分支直接使用 Write/Read。 |
| 5233 | <code>{</code> | 开始代码作用域。 |
| 5234 | <code>if (message == null) return 0;</code> | 空消息的编码长度为零。 |
| 5235 | <code>int size = 0;</code> | 初始化长度累加器。 |
| 5236 | <code>if (message.accessToken != null)</code> | token 非 null 时才写入或计算长度。 |
| 5237 | <code>size += CodedOutputStream.ComputeTagSize(1) + CodedOutputStream.ComputeStringSize(message.accessToken);</code> | 累加 token 字段标签和字符串编码长度。 |
| 5238 | <code>return size;</code> | 返回累积的 protobuf 正文长度。 |
| 5239 | <code>}</code> | 结束当前代码作用域。 |
| 5240 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 5241 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 5242 | <code>private static void WriteResUserLogout(CodedOutputStream output, ResUserLogout message)</code> | 新增响应 protobuf 写入函数。 |
| 5243 | <code>{</code> | 开始代码作用域。 |
| 5244 | <code>output.WriteTag(1, WireFormat.WireType.Varint);</code> | 响应字段 1 的 status 使用 Varint 标签。 |
| 5245 | <code>output.WriteInt32(message.status);</code> | 写入整数状态码，即使值为零也写入。 |
| 5246 | <code>if (message.errorMessage != null)</code> | 错误文案非 null 时写入或计算长度。 |
| 5247 | <code>{</code> | 开始代码作用域。 |
| 5248 | <code>output.WriteTag(2, WireFormat.WireType.LengthDelimited);</code> | 响应字段 2 使用长度分隔标签。 |
| 5249 | <code>output.WriteString(message.errorMessage);</code> | 写入错误文案。 |
| 5250 | <code>}</code> | 结束当前代码作用域。 |
| 5251 | <code>}</code> | 结束当前代码作用域。 |
| 5252 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 5253 | <code>private static ResUserLogout ReadResUserLogout(CodedInputStream input)</code> | 新增响应 protobuf 读取函数。 |
| 5254 | <code>{</code> | 开始代码作用域。 |
| 5255 | <code>var message = new ResUserLogout();</code> | 创建待填充的响应对象。 |
| 5256 | <code>uint tag;</code> | 保存当前 protobuf 标签。 |
| 5257 | <code>while ((tag = input.ReadTag()) != 0)</code> | 逐字段读取，标签为零表示消息结束。 |
| 5258 | <code>{</code> | 开始代码作用域。 |
| 5259 | <code>switch (WireFormat.GetTagFieldNumber(tag))</code> | 根据字段号选择对应数据读取方式。 |
| 5260 | <code>{</code> | 开始代码作用域。 |
| 5261 | <code>case 1:</code> | 处理字段 1：请求中是 token，响应中是 status。 |
| 5262 | <code>message.status = input.ReadInt32();</code> | 读取整数状态码。 |
| 5263 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 5264 | <code>case 2:</code> | 处理响应的字段 2 错误文案。 |
| 5265 | <code>message.errorMessage = input.ReadString();</code> | 读取错误文案字符串。 |
| 5266 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 5267 | <code>default:</code> | 遇到未定义的 protobuf 字段时走兼容分支。 |
| 5268 | <code>input.SkipLastField();</code> | 跳过未知字段，保持 protobuf 向前兼容。 |
| 5269 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 5270 | <code>}</code> | 结束当前代码作用域。 |
| 5271 | <code>}</code> | 结束当前代码作用域。 |
| 5272 | <code>return message;</code> | 返回已经读取的消息对象。 |
| 5273 | <code>}</code> | 结束当前代码作用域。 |
| 5274 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 5275 | <code>private static int ComputeSizeResUserLogout(ResUserLogout message)</code> | 新增响应编码长度辅助函数；不会发送额外协议数据。 |
| 5276 | <code>{</code> | 开始代码作用域。 |
| 5277 | <code>if (message == null) return 0;</code> | 空消息的编码长度为零。 |
| 5278 | <code>int size = 0;</code> | 初始化长度累加器。 |
| 5279 | <code>size += CodedOutputStream.ComputeTagSize(1) + CodedOutputStream.ComputeInt32Size(message.status);</code> | 累加状态标签与整数编码长度。 |
| 5280 | <code>if (message.errorMessage != null)</code> | 错误文案非 null 时写入或计算长度。 |
| 5281 | <code>size += CodedOutputStream.ComputeTagSize(2) + CodedOutputStream.ComputeStringSize(message.errorMessage);</code> | 累加错误文案标签与字符串编码长度。 |
| 5282 | <code>return size;</code> | 返回累积的 protobuf 正文长度。 |
| 5283 | <code>}</code> | 结束当前代码作用域。 |
| 5284 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 5285 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |

原代码删除/替换原因：

- `if (source is ReqGuestLogin value0)`：原首分支改为 else if，为新增登出编解码分支留出位置。
- `if (messageType == typeof(ReqGuestLogin)) message = ReadReqGuestLogin(input);`：原首分支改为 else if，为新增登出编解码分支留出位置。

## Constants.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Constants.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Constants.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 36 | <code>eUserLogoutReq = 1008,</code> | 定义登出请求命令号 1008；客户端与服务端保持一致。 |
| 37 | <code>eUserLogoutRes = 1009,</code> | 定义登出响应命令号 1009；客户端与服务端保持一致。 |

## GM_EntityMgr.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/EntityMgr/GM_EntityMgr.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/EntityMgr/GM_EntityMgr.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 33 | <code>lock (SessionMgr.Instance.BindingSync)</code> | 实体连接绑定、清理也使用网络层同一把锁，避免跨对象竞态。 |
| 34 | <code>{</code> | 开始代码作用域。 |
| 35 | <code>if (!SessionMgr.Instance.IsCurrentConnection(session) &#124;&#124; session.client != null && !session.client.Active)</code> | 绑定玩家前确认账号仍绑定该连接，而且通道没有被关闭。 |
| 36 | <code>return false;</code> | 返回失败。 |
| 37 | <code>session.playerId = playerId;</code> | 通过校验后写玩家 ID。 |
| 38 | <code>session.accountIdAndJob = GM_AccountEntityMgr.Instance.PlayerKey(accountId, job);</code> | 计算并写入现有账号和职业组合键。 |
| 39 | <code>AddPlayer(session, playerId);</code> | 复用现有实体创建/缓存与连接绑定。 |
| 40 | <code>return true;</code> | 返回成功。 |
| 41 | <code>}</code> | 结束当前代码作用域。 |
| 42 | <code>}</code> | 结束当前代码作用域。 |
| 43 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 44 | <code>public int? ExitCurrentSession(IdSession session, Func&lt;int&gt; exit)</code> | 增加受连接身份保护的退出入口；null 表示过时事件被忽略。 |
| 45 | <code>{</code> | 开始代码作用域。 |
| 46 | <code>lock (SessionMgr.Instance.BindingSync)</code> | 实体连接绑定、清理也使用网络层同一把锁，避免跨对象竞态。 |
| 47 | <code>{</code> | 开始代码作用域。 |
| 48 | <code>GM_PlayerEntity player = Get(session.playerId);</code> | 读取缓存中的玩家实体，不新增实体删除策略。 |
| 49 | <code>if (player == null &#124;&#124; !ReferenceEquals(player.uPlayer.s, session)) return null;</code> | 只有实体仍绑定触发断线的连接时才允许退出；旧事件直接返回 null。 |
| 50 | <code>return exit();</code> | 在同一锁中执行原有退出逻辑，使判断与退出成为不可插入的操作。 |
| 51 | <code>}</code> | 结束当前代码作用域。 |
| 52 | <code>}</code> | 结束当前代码作用域。 |
| 53 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 54 | <code>public void ClearDisconnectedSession(IdSession session)</code> | 为没有逻辑服的断线增加安全引用清理方法。 |
| 55 | <code>{</code> | 开始代码作用域。 |
| 56 | <code>lock (SessionMgr.Instance.BindingSync)</code> | 实体连接绑定、清理也使用网络层同一把锁，避免跨对象竞态。 |
| 57 | <code>{</code> | 开始代码作用域。 |
| 58 | <code>GM_PlayerEntity player = Get(session.playerId);</code> | 读取缓存中的玩家实体，不新增实体删除策略。 |
| 59 | <code>if (player != null && ReferenceEquals(player.uPlayer.s, session)) player.uPlayer.s = null;</code> | 只有实体连接引用等于旧连接时才置空；新登录引用保持。 |
| 60 | <code>}</code> | 结束当前代码作用域。 |

原代码删除/替换原因：

- `session.playerId = playerId;`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `session.accountIdAndJob = GM_AccountEntityMgr.Instance.PlayerKey(accountId, job);`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `AddPlayer(session, playerId);`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。
- `return true;`：此语句在当前版本移动到同步锁或条件保护内；原功能由上面的新代码保留，并增加并发身份检查。

## AuthDefinitions.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/GM_Auth/AuthDefinitions.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/GM_Auth/AuthDefinitions.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 9 | <code>Failed,</code> | 原 Failed 项仅补逗号，以便在末尾添加新状态；数值不变。 |
| 10 | <code>NotificationPending</code> | 新增凭证已撤销但远端通知未全部确认的领域状态；放在枚举末尾保持旧值。 |

原代码删除/替换原因：

- `Failed`：仅补逗号以便末尾添加 NotificationPending。

## GM_AuthSessionMgr.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/GM_Auth/GM_AuthSessionMgr.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/GM_Auth/GM_AuthSessionMgr.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 17 | <code>public sealed partial class GM_AuthSessionMgr</code> | 把认证 Manager 拆成部分类型；Logout 文件仍是同一个 Manager。 |
| 50 | <code>public HashSet&lt;int&gt; GameServerIds { get; } = new();</code> | 为登录会话增加全部已签发票据目标集合，避免只通知最后一个服务器。 |
| 87 | <code>StartLogoutNotificationRetryLoop();</code> | 认证 Manager 初始化时启动内存通知重试循环。 |
| 184 | <code>session.GameServerIds.Add(serverId);</code> | 签发票据时记录目标服务器；HashSet 自动去重。 |

原代码删除/替换原因：

- `public sealed class GM_AuthSessionMgr`：原类声明增加 partial，允许在新文件中扩展同一类型。

## AuthMessage.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Datas/Messages/AuthMessage.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Datas/Messages/AuthMessage.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 253 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 254 | <code>[ProtoContract]</code> | 声明这是 protobuf 消息类型。 |
| 255 | <code>[MessageMeta((short)Module.AUTH, (short)Cmd.eUserLogoutReq)]</code> | 注册 AUTH/1008 请求类型，消息工厂据此解码。 |
| 256 | <code>public sealed class ReqUserLogout : Message</code> | 新增登出请求消息，继承已有 Message。 |
| 257 | <code>{</code> | 开始代码作用域。 |
| 258 | <code>[ProtoMember(1, IsRequired = true)] public string accessToken;</code> | token 使用 protobuf 字段 1，与客户端一致。 |
| 259 | <code>}</code> | 结束当前代码作用域。 |
| 260 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 261 | <code>[ProtoContract]</code> | 声明这是 protobuf 消息类型。 |
| 262 | <code>[MessageMeta((short)Module.AUTH, (short)Cmd.eUserLogoutRes)]</code> | 注册 AUTH/1009 响应类型。 |
| 263 | <code>public sealed class ResUserLogout : Message</code> | 新增登出响应消息，继承已有 Message。 |
| 264 | <code>{</code> | 开始代码作用域。 |
| 265 | <code>[ProtoMember(1, IsRequired = true)] public int status;</code> | 先声明退出状态，供受保护和原有两个分支赋值。 |
| 266 | <code>[ProtoMember(2)] public string errorMessage;</code> | 可选错误文案使用字段 2。 |
| 267 | <code>}</code> | 结束当前代码作用域。 |

## HttpAuthControll.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Entries/HttpControllers/HttpAuthControll.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Entries/HttpControllers/HttpAuthControll.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 48 | <code>[HttpRequestMapping("/ReqUserLogout")]</code> | 注册登出 HTTP 路由，框架据此找到 Controller 方法。 |
| 49 | <code>public async Task&lt;object&gt; DoUserLogout(IFullHttpRequest request) =&gt;</code> | Controller 异步入口，接收 HTTP 请求并返回响应对象。 |
| 50 | <code>await AuthModule.Instance.HandlerReqUserLogoutAsync(ReadRequest(request) as ReqUserLogout);</code> | 沿用现有 Base64/业务包解码，再交给 AuthModule；Controller 不执行会话逻辑。 |
| 51 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |

## AuthModule.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Entries/Modules/AuthModule.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Entries/Modules/AuthModule.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 137 | <code>public async Task&lt;ResUserLogout&gt; HandlerReqUserLogoutAsync(ReqUserLogout request)</code> | Module 登出方法，负责参数检查及领域状态到协议状态的映射。 |
| 138 | <code>{</code> | 开始代码作用域。 |
| 139 | <code>if (request == null &#124;&#124; string.IsNullOrWhiteSpace(request.accessToken) &#124;&#124; request.accessToken.Length &gt; 512)</code> | 检查解码结果、空 token 和 512 字符长度上限。 |
| 140 | <code>return new ResUserLogout { status = (int)Respones.InvalidParams };</code> | 非法请求返回 InvalidParams，不进入 Manager。 |
| 141 | <code>AuthOperationStatus result = await GM_AuthSessionMgr.Instance.LogoutAsync(request.accessToken);</code> | 等待 Manager 完成凭证撤销和首次通知。 |
| 142 | <code>return new ResUserLogout</code> | 构造符合协议的登出响应。 |
| 143 | <code>{</code> | 开始代码作用域。 |
| 144 | <code>status = result == AuthOperationStatus.Success ? (int)Respones.OK :</code> | Manager 成功映射为 OK。 |
| 145 | <code>result == AuthOperationStatus.InvalidAccessToken ? (int)Respones.AccountIsNotLogin : (int)Respones.SystemErr,</code> | 未知或过期 token 映射 AccountIsNotLogin，其他失败映射 SystemErr。 |
| 146 | <code>errorMessage = result == AuthOperationStatus.NotificationPending</code> | 新增凭证已撤销但远端通知未全部确认的领域状态；放在枚举末尾保持旧值。 |
| 147 | <code>? "Login credentials revoked; game server cleanup is pending and will be retried." : null</code> | 文案明确凭证已撤销，游戏服清理尚未完成且后台会重试。 |
| 148 | <code>};</code> | 结束对象初始化/调用表达式。 |
| 149 | <code>}</code> | 结束当前代码作用域。 |
| 150 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |

## GameServer.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/GameServer.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/GameServer.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 98 | <code>"/ReqGuestLogin", "/ReqRegisterUser", "/ReqUserLogin", "/ReqUserLogout",</code> | Login 角色白名单加入公共登出路由。 |
| 107 | <code>"/ReqUserLogin", "/ReqUserLogout", "/ReqGuestUpgrade", "/ReqIssueGameTicket");</code> | 非 Login 角色移除登出路由，游戏服不接受公共登出。 |
| 169 | <code>await SessionMgr.Instance.StopAsync();</code> | 服务器关机先取消并等待空闲检测结束。 |
| 170 | <code>await GM_AuthSessionMgr.Instance.StopLogoutRetriesAsync();</code> | 服务器关机再取消并等待后台通知重试结束。 |

原代码删除/替换原因：

- `"/ReqGuestLogin", "/ReqRegisterUser", "/ReqUserLogin",`：原角色路由清单增加 ReqUserLogout，其他路由不变。
- `"/ReqUserLogin", "/ReqGuestUpgrade", "/ReqIssueGameTicket");`：原角色路由清单增加 ReqUserLogout，其他路由不变。

## CommonLogicServer.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/LogicServer/CommonLogicServer.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/LogicServer/CommonLogicServer.cs)

以下列出此文件本次 diff 的全部新增/替换行。删除行在表后说明。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 59 | <code>int status;</code> | 先声明退出状态，供受保护和原有两个分支赋值。 |
| 60 | <code>bool gatewayMode = bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["GatewayMode"], out bool enabled) && enabled;</code> | 读取 GatewayMode，以限制身份保护分支的适用范围。 |
| 61 | <code>if (req.quitReason == (int)QuitReason.DisconnectQuit && !s.isWebSocket && !gatewayMode)</code> | 只对直连 TCP 的被动断线退出应用旧连接保护。 |
| 62 | <code>{</code> | 开始代码作用域。 |
| 63 | <code>int? result = Game.Core.EntityMgr.GM_EntityMgr.Instance.ExitCurrentSession(</code> | 调用实体 Manager 的原子身份检查与退出方法。 |
| 64 | <code>s, () =&gt; this.ExitLogicServer(playerId, req.quitReason));</code> | 以委托传入原有 ExitLogicServer，不修改具体退出策略。 |
| 65 | <code>if (!result.HasValue) return null;</code> | 旧连接事件返回 null 时终止，避免影响新连接。 |
| 66 | <code>status = result.Value;</code> | 取原有退出逻辑的状态用于后续响应流程。 |
| 67 | <code>}</code> | 结束当前代码作用域。 |
| 68 | <code>else status = this.ExitLogicServer(playerId, req.quitReason);</code> | 其他类型退出仍按原逻辑执行。 |

原代码删除/替换原因：

- `int status = this.ExitLogicServer(playerId, req.quitReason);`：退出状态改为按直连 TCP 断线身份保护分支赋值；其他退出保留原方法。

## GM_AuthLogoutSessionMgr.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/GM_Auth/GM_AuthLogoutSessionMgr.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Game/Core/GM_Auth/GM_AuthLogoutSessionMgr.cs)

本次新增文件，因此下面包含整个文件的每一行。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 1 | <code>using System;</code> | 引入 System 命名空间，供此文件使用相关类型。 |
| 2 | <code>using System.Collections.Concurrent;</code> | 引入 System.Collections.Concurrent 命名空间，供此文件使用相关类型。 |
| 3 | <code>using System.Collections.Generic;</code> | 引入 System.Collections.Generic 命名空间，供此文件使用相关类型。 |
| 4 | <code>using System.Linq;</code> | 引入 System.Linq 命名空间，供此文件使用相关类型。 |
| 5 | <code>using System.Net.Http;</code> | 引入 System.Net.Http 命名空间，供此文件使用相关类型。 |
| 6 | <code>using System.Text;</code> | 引入 System.Text 命名空间，供此文件使用相关类型。 |
| 7 | <code>using System.Threading;</code> | 引入 System.Threading 命名空间，供此文件使用相关类型。 |
| 8 | <code>using System.Threading.Tasks;</code> | 引入 System.Threading.Tasks 命名空间，供此文件使用相关类型。 |
| 9 | <code>using Framework.Core.Net;</code> | 引入 Framework.Core.Net 命名空间，供此文件使用相关类型。 |
| 10 | <code>using Framework.Core.Serializer;</code> | 引入 Framework.Core.Serializer 命名空间，供此文件使用相关类型。 |
| 11 | <code>using Game.Datas.Messages;</code> | 引入 Game.Datas.Messages 命名空间，供此文件使用相关类型。 |
| 12 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 13 | <code>namespace Game.Core.GM_Auth</code> | 把类型放入 Game.Core.GM_Auth 命名空间。 |
| 14 | <code>{</code> | 开始代码作用域。 |
| 15 | <code>public sealed partial class GM_AuthSessionMgr</code> | 把认证 Manager 拆成部分类型；Logout 文件仍是同一个 Manager。 |
| 16 | <code>{</code> | 开始代码作用域。 |
| 17 | <code>private sealed class LogoutNotification</code> | 声明单次登出的内存通知状态对象；它同时承担幂等记录和重试任务的作用。 |
| 18 | <code>{</code> | 开始代码作用域。 |
| 19 | <code>public LoginSession Marker;</code> | 保存固定的撤销标记，包括账号、会话 ID、会话版本；重试不能改变它。 |
| 20 | <code>public long TokenExpiresAt;</code> | 原 token 的过期时间，用于限制重复登出的有效期。 |
| 21 | <code>public long Deadline;</code> | 通知任务截止时间；超过此时间放弃重试并记录错误。 |
| 22 | <code>public long NextAttempt;</code> | 下一次允许重试的 Unix 秒数。 |
| 23 | <code>public int Failures;</code> | 累计失败轮数，用于选择退避间隔。 |
| 24 | <code>public HashSet&lt;int&gt; Pending;</code> | 尚未确认的游戏服 ID 集合；确认一个就删一个。 |
| 25 | <code>public readonly SemaphoreSlim Gate = new(1, 1);</code> | 每条登出记录独立的异步互斥锁；HTTP 重复请求和后台重试共享它。 |
| 26 | <code>}</code> | 结束当前代码作用域。 |
| 27 | <code>private readonly ConcurrentDictionary&lt;string, LogoutNotification&gt; logoutRecords = new();</code> | 以 token 哈希为键保存撤销与通知记录；并发字典允许不同账号并行操作。 |
| 28 | <code>private CancellationTokenSource logoutRetryCancellation;</code> | 后台重试的取消源，服务器停止时取消。 |
| 29 | <code>private Task logoutRetryTask;</code> | 保存后台任务，停止时可以等待它退出。 |
| 30 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 31 | <code>public async Task&lt;AuthOperationStatus&gt; LogoutAsync(string accessToken)</code> | Manager 登出入口，异步返回领域状态，不直接返回 HTTP 协议对象。 |
| 32 | <code>{</code> | 开始代码作用域。 |
| 33 | <code>if (string.IsNullOrWhiteSpace(accessToken) &#124;&#124; accessToken.Length &gt; 512)</code> | 拒绝空 token、全空白 token 和超过 512 字符的 token。 |
| 34 | <code>return AuthOperationStatus.InvalidAccessToken;</code> | 参数不符合 token 格式时返回无效凭证。 |
| 35 | <code>string hash = HashToken(accessToken);</code> | 使用现有 HashToken；记录使用哈希键而不是明文 token。 |
| 36 | <code>long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();</code> | 取得当前 UTC Unix 秒数，用于过期判断和首次通知时间。 |
| 37 | <code>if (!logoutRecords.TryGetValue(hash, out LogoutNotification record))</code> | 先查已有登出记录；找到时直接复用，保证幂等。 |
| 38 | <code>{</code> | 开始代码作用域。 |
| 39 | <code>if (!accessSessions.TryGetValue(hash, out LoginSession login) &#124;&#124; login.ExpiresAtUnixSeconds &lt;= now)</code> | 没有登出记录时，必须找到仍未过期的登录会话。 |
| 40 | <code>return AuthOperationStatus.InvalidAccessToken;</code> | 未知或过期 token 不撤销任何会话。 |
| 41 | <code>LoginSessionCreationLock accountLock = RetainLoginSessionLock(login.AccountId);</code> | 取得并引用计数保留该账号的登录锁，与新登录、签发票据使用同一同步机制。 |
| 42 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 43 | <code>{</code> | 开始代码作用域。 |
| 44 | <code>lock (accountLock.Sync)</code> | 进入账号锁，保证查当前会话、生成版本、撤销 token 是一个串行操作。 |
| 45 | <code>{</code> | 开始代码作用域。 |
| 46 | <code>if (!logoutRecords.TryGetValue(hash, out record))</code> | 进入锁后再次查登出记录，防止两个并发请求重复生成标记。 |
| 47 | <code>{</code> | 开始代码作用域。 |
| 48 | <code>if (login.ExpiresAtUnixSeconds &lt;= DateTimeOffset.UtcNow.ToUnixTimeSeconds())</code> | 等待锁期间可能过期，因此重新读取时间检查。 |
| 49 | <code>return AuthOperationStatus.InvalidAccessToken;</code> | 锁内检查已过期时立即返回无效凭证。 |
| 50 | <code>bool isCurrent = currentSessions.TryGetValue(login.AccountId, out LoginSession current) &&</code> | 查账号当前会话，准备判断请求是否来自最新登录。 |
| 51 | <code>ReferenceEquals(current, login);</code> | 用对象身份判断当前会话是否就是该 token 对应的登录。 |
| 52 | <code>// A replaced login must never mint a barrier above the new login.</code> | 说明旧 token 不允许生成比新登录更高的版本屏障。 |
| 53 | <code>LoginSession marker = current;</code> | 默认复用当前会话；被新登录替代的旧 token 走这个分支。 |
| 54 | <code>if (isCurrent)</code> | 只有 token 仍对应当前登录时才创建新的撤销标记。 |
| 55 | <code>{</code> | 开始代码作用域。 |
| 56 | <code>marker = new LoginSession</code> | 创建仅用于版本屏障的 LoginSession 对象。 |
| 57 | <code>{</code> | 开始代码作用域。 |
| 58 | <code>AccountId = login.AccountId, SessionId = CreateRandomToken(),</code> | 继承账号 ID，并生成新的随机会话 ID，使旧连接的会话 ID 不再匹配。 |
| 59 | <code>SessionVersion = sessionVersions.AddOrUpdate(login.AccountId,</code> | 原子更新账号会话版本，创建或递增版本屏障。 |
| 60 | <code>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),</code> | 账号尚无版本时，以当前 UTC 毫秒作为初始版本。 |
| 61 | <code>(_, previous) =&gt; Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), previous + 1)),</code> | 已有版本时取当前毫秒和旧版本加一的较大值，保证严格递增。 |
| 62 | <code>ExpiresAtUnixSeconds = login.ExpiresAtUnixSeconds</code> | 标记沿用原 token 过期时间。 |
| 63 | <code>};</code> | 结束对象初始化/调用表达式。 |
| 64 | <code>// Keep the version barrier; this marker has no access token.</code> | 说明该标记仅保留版本屏障，不会获得 accessToken。 |
| 65 | <code>currentSessions[login.AccountId] = marker;</code> | 把账号当前会话换成撤销标记；旧登录不再是当前会话。 |
| 66 | <code>}</code> | 结束当前代码作用域。 |
| 67 | <code>accessSessions.TryRemove(hash, out _);</code> | 删除指定 token 的访问凭证映射；此后不能验证它或签发新票据。 |
| 68 | <code>record = new LogoutNotification</code> | 创建通知记录，供首次通知、重复请求及后台重试共同使用。 |
| 69 | <code>{</code> | 开始代码作用域。 |
| 70 | <code>Marker = marker, TokenExpiresAt = login.ExpiresAtUnixSeconds,</code> | 保存固定标记与原 token 过期时间。 |
| 71 | <code>Deadline = login.ExpiresAtUnixSeconds + (long)ticketLifetime.TotalSeconds,</code> | 截止时间等于 token 到期时间加票据有效期，覆盖可能尚有效的旧票据。 |
| 72 | <code>NextAttempt = now,</code> | 首次通知立即可执行。 |
| 73 | <code>Pending = marker == null ? new HashSet&lt;int&gt;() : new HashSet&lt;int&gt;(login.GameServerIds)</code> | 复制该登录申请过票据的全部游戏服；没有标记时不创建目标。 |
| 74 | <code>};</code> | 结束对象初始化/调用表达式。 |
| 75 | <code>logoutRecords[hash] = record;</code> | 在账号锁内发布记录，后续重复登出拿到同一份状态。 |
| 76 | <code>}</code> | 结束当前代码作用域。 |
| 77 | <code>}</code> | 结束当前代码作用域。 |
| 78 | <code>}</code> | 结束当前代码作用域。 |
| 79 | <code>finally { ReleaseLoginSessionLock(login.AccountId, accountLock); }</code> | 无论成功、异常或提前返回，都释放账号锁的引用计数。网络请求在此后执行。 |
| 80 | <code>}</code> | 结束当前代码作用域。 |
| 81 | <code>if (record.TokenExpiresAt &lt;= now) return AuthOperationStatus.InvalidAccessToken;</code> | 已有登出记录也受原 token 有效期限制；过期后不返回成功。 |
| 82 | <code>await record.Gate.WaitAsync();</code> | 等待该任务的异步锁，避免同一记录同时发送两轮通知。 |
| 83 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 84 | <code>{</code> | 开始代码作用域。 |
| 85 | <code>await NotifyPendingAsync(record, CancellationToken.None);</code> | 立即通知未确认目标；首次 HTTP 操作不使用后台停止令牌。 |
| 86 | <code>return record.Pending.Count == 0 ? AuthOperationStatus.Success : AuthOperationStatus.NotificationPending;</code> | 目标集合为空表示全部确认，返回 Success；否则返回 NotificationPending。 |
| 87 | <code>}</code> | 结束当前代码作用域。 |
| 88 | <code>finally { record.Gate.Release(); }</code> | 始终释放记录锁，允许后台或后续请求继续处理。 |
| 89 | <code>}</code> | 结束当前代码作用域。 |
| 90 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 91 | <code>private async Task NotifyPendingAsync(LogoutNotification record, CancellationToken cancellation)</code> | 执行一次通知轮次；此方法要求调用者已经持有记录 Gate。 |
| 92 | <code>{</code> | 开始代码作用域。 |
| 93 | <code>int[] targets = record.Pending.ToArray();</code> | 复制目标快照，避免边遍历边删除 Pending。 |
| 94 | <code>bool[] results = await Task.WhenAll(targets.Select(id =&gt; SendLogoutNotificationAsync(record.Marker, id, cancellation)));</code> | 对所有目标并行发送；Task.WhenAll 的结果顺序与 targets 一致。 |
| 95 | <code>for (int i = 0; i &lt; targets.Length; i++)</code> | 按数组索引把目标与确认结果对应起来。 |
| 96 | <code>if (results[i]) record.Pending.Remove(targets[i]);</code> | 只删除明确返回成功的目标，下轮不再通知已确认目标。 |
| 97 | <code>if (record.Pending.Count &gt; 0)</code> | 还有未确认目标时安排重试。 |
| 98 | <code>{</code> | 开始代码作用域。 |
| 99 | <code>int delay = record.Failures++ switch { 0 =&gt; 1, 1 =&gt; 2, 2 =&gt; 5, _ =&gt; 10 };</code> | 按失败轮数选择 1、2、5、10 秒；第四次起固定 10 秒，并递增失败轮数。 |
| 100 | <code>record.NextAttempt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + delay;</code> | 退避时间从本轮结束时开始计算。 |
| 101 | <code>}</code> | 结束当前代码作用域。 |
| 102 | <code>}</code> | 结束当前代码作用域。 |
| 103 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 104 | <code>private async Task&lt;bool&gt; SendLogoutNotificationAsync(LoginSession marker, int serverId, CancellationToken cancellation)</code> | 发送一个游戏服的内部失效通知，返回是否得到完整确认。 |
| 105 | <code>{</code> | 开始代码作用域。 |
| 106 | <code>if (!gameSessionNotificationUrls.TryGetValue(serverId, out string url)) return false;</code> | 缺少目标通知 URL 时视为失败，仍保留待重试目标。 |
| 107 | <code>long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();</code> | 每次发送重新生成当前时间戳。 |
| 108 | <code>string nonce = CreateRandomToken();</code> | 每次发送重新生成随机 nonce，避免重放校验拒绝重试。 |
| 109 | <code>var request = new ReqInvalidateAccountSession</code> | 复用现有 ReqInvalidateAccountSession，命令号是 1007。 |
| 110 | <code>{</code> | 开始代码作用域。 |
| 111 | <code>accountId = marker.AccountId, sessionId = marker.SessionId, sessionVersion = marker.SessionVersion,</code> | 发送固定账号、标记 ID 和版本；这些值在重试中不变化。 |
| 112 | <code>timestamp = timestamp, nonce = nonce,</code> | 写入本次发送的新时间戳和 nonce。 |
| 113 | <code>signature = Convert.ToBase64String(ComputeHmac(InvalidationPurpose +</code> | 为失效通知计算 HMAC，再转成 Base64 字符串。 |
| 114 | <code>BuildInvalidationUnsignedBody(marker.AccountId, marker.SessionVersion, marker.SessionId, timestamp, nonce)))</code> | 按照现有规范拼接账号、版本、会话 ID、时间戳及 nonce，作为签名内容。 |
| 115 | <code>};</code> | 结束对象初始化/调用表达式。 |
| 116 | <code>byte[] packet = MessagePusher.PackMessage(request, 0);</code> | 复用标准业务包打包，包含协议头和 protobuf 消息。 |
| 117 | <code>if (packet == null) return false;</code> | 打包失败时不发送，返回未确认。 |
| 118 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 119 | <code>{</code> | 开始代码作用域。 |
| 120 | <code>using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);</code> | 创建关联后台取消令牌的通知超时源；using 在结束时释放。 |
| 121 | <code>timeout.CancelAfter(TimeSpan.FromSeconds(2));</code> | 一个目标的 HTTP 通知最长等待两秒。 |
| 122 | <code>using var content = new StringContent(Convert.ToBase64String(packet), Encoding.UTF8, "text/plain");</code> | 将整个业务包转 Base64，用 UTF-8 text/plain 作为 HTTP 正文。 |
| 123 | <code>using HttpResponseMessage response = await InternalHttpClient.PostAsync(url, content, timeout.Token);</code> | 使用复用的 InternalHttpClient 发送 POST；响应对象在结束时释放。 |
| 124 | <code>return response.IsSuccessStatusCode && await response.Content.ReadAsStringAsync(timeout.Token) == "OK";</code> | 同时要求 HTTP 成功和正文恰为 OK；读取正文也受两秒令牌限制。 |
| 125 | <code>}</code> | 结束当前代码作用域。 |
| 126 | <code>catch (Exception exception)</code> | 捕获超时、网络异常等发送失败。 |
| 127 | <code>{</code> | 开始代码作用域。 |
| 128 | <code>logger.Warn(exception, $"Logout notification pending: account[{marker.AccountId}] server[{serverId}]");</code> | 记录账号、服务器和异常，便于定位未确认目标。 |
| 129 | <code>return false;</code> | 异常转为 false，不让一个目标失败中断所有目标。 |
| 130 | <code>}</code> | 结束当前代码作用域。 |
| 131 | <code>}</code> | 结束当前代码作用域。 |
| 132 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 133 | <code>private void StartLogoutNotificationRetryLoop()</code> | 初始化后台重试任务。 |
| 134 | <code>{</code> | 开始代码作用域。 |
| 135 | <code>StopLogoutRetries();</code> | 先取消旧循环，避免重复初始化后旧循环持续运行。 |
| 136 | <code>logoutRetryCancellation = new CancellationTokenSource();</code> | 给新循环创建独立取消源。 |
| 137 | <code>logoutRetryTask = RetryLogoutsAsync(logoutRetryCancellation.Token);</code> | 启动循环并保存 Task；无需新建专用线程。 |
| 138 | <code>}</code> | 结束当前代码作用域。 |
| 139 | <code>public void StopLogoutRetries() =&gt; logoutRetryCancellation?.Cancel();</code> | 同步发出停止请求；空取消源时不做操作。 |
| 140 | <code>public async Task StopLogoutRetriesAsync()</code> | 异步停止入口，用于服务器关机和测试清理。 |
| 141 | <code>{</code> | 开始代码作用域。 |
| 142 | <code>StopLogoutRetries();</code> | 发出取消信号。 |
| 143 | <code>if (logoutRetryTask != null) await logoutRetryTask;</code> | 等待已有后台任务结束，避免停止后仍在修改记录。 |
| 144 | <code>}</code> | 结束当前代码作用域。 |
| 145 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 146 | <code>private async Task RetryLogoutsAsync(CancellationToken cancellation)</code> | 后台循环，每一秒检查任务是否已经到重试时间。 |
| 147 | <code>{</code> | 开始代码作用域。 |
| 148 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 149 | <code>{</code> | 开始代码作用域。 |
| 150 | <code>using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));</code> | 创建一秒周期定时器，离开作用域自动释放。 |
| 151 | <code>while (await timer.WaitForNextTickAsync(cancellation))</code> | 等待每次时钟 tick；取消会使等待抛取消异常。 |
| 152 | <code>await Task.WhenAll(logoutRecords.Select(pair =&gt; RetryLogoutAsync(pair.Key, pair.Value, cancellation)));</code> | 不同记录并行检查或重试；每条记录仍由自己的 Gate 防止并发。 |
| 153 | <code>}</code> | 结束当前代码作用域。 |
| 154 | <code>catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }</code> | 只有本循环的停止令牌被取消时吞掉取消异常，正常结束。 |
| 155 | <code>}</code> | 结束当前代码作用域。 |
| 156 | <code>private async Task RetryLogoutAsync(string hash, LogoutNotification record, CancellationToken cancellation)</code> | 检查并处理一条登出记录。 |
| 157 | <code>{</code> | 开始代码作用域。 |
| 158 | <code>if (!await record.Gate.WaitAsync(0, cancellation)) return;</code> | 零等待尝试取得记录锁；已有 HTTP 或重试正在处理时跳过本轮。 |
| 159 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 160 | <code>{</code> | 开始代码作用域。 |
| 161 | <code>long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();</code> | 读取本轮检查时间。 |
| 162 | <code>if (now &gt;= record.Deadline)</code> | 先判断绝对截止时间，避免无限重试。 |
| 163 | <code>{</code> | 开始代码作用域。 |
| 164 | <code>if (record.Pending.Count &gt; 0)</code> | 截止时仍有失败目标才记录清理未完成。 |
| 165 | <code>logger.Error($"Logout cleanup deadline exceeded: account[{record.Marker.AccountId}] servers[{string.Join(",", record.Pending)}]");</code> | 错误日志列出账号和全部未确认游戏服，不宣称远端退出成功。 |
| 166 | <code>logoutRecords.TryRemove(hash, out _);</code> | 达到截止时间后删除内存记录。 |
| 167 | <code>}</code> | 结束当前代码作用域。 |
| 168 | <code>else if (record.Pending.Count == 0 && now &gt;= record.TokenExpiresAt)</code> | 已全部确认的记录保留到原 token 到期，支持有效期内重复登出。 |
| 169 | <code>logoutRecords.TryRemove(hash, out _);</code> | 幂等保留期结束后释放记录。 |
| 170 | <code>else if (record.Pending.Count &gt; 0 && now &gt;= record.NextAttempt)</code> | 未确认目标存在且退避已结束时才重新发送。 |
| 171 | <code>await NotifyPendingAsync(record, cancellation);</code> | 使用相同 Marker 发起下一轮，但发送函数会更新签名防重放字段。 |
| 172 | <code>}</code> | 结束当前代码作用域。 |
| 173 | <code>finally { record.Gate.Release(); }</code> | 始终释放记录锁。 |
| 174 | <code>}</code> | 结束当前代码作用域。 |
| 175 | <code>}</code> | 结束当前代码作用域。 |
| 176 | <code>}</code> | 结束当前代码作用域。 |

## SessionMgrConnectionTimeout.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/net/SessionMgrConnectionTimeout.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServer/src/Framework/Core/net/SessionMgrConnectionTimeout.cs)

本次新增文件，因此下面包含整个文件的每一行。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 1 | <code>using System;</code> | 引入 System 命名空间，供此文件使用相关类型。 |
| 2 | <code>using System.Configuration;</code> | 引入 System.Configuration 命名空间，供此文件使用相关类型。 |
| 3 | <code>using System.Threading;</code> | 引入 System.Threading 命名空间，供此文件使用相关类型。 |
| 4 | <code>using System.Threading.Tasks;</code> | 引入 System.Threading.Tasks 命名空间，供此文件使用相关类型。 |
| 5 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 6 | <code>namespace Framework.Core.Net</code> | 把类型放入 Framework.Core.Net 命名空间。 |
| 7 | <code>{</code> | 开始代码作用域。 |
| 8 | <code>public partial class SessionMgr</code> | 把 SessionMgr 拆成部分类型；主文件管理连接，Idle 文件管理扫描。 |
| 9 | <code>{</code> | 开始代码作用域。 |
| 10 | <code>private CancellationTokenSource idleCancellation;</code> | 保存统一空闲检测任务的取消源。 |
| 11 | <code>private Task idleTask;</code> | 保存扫描任务，关机时等待结束。 |
| 12 | <code>private TimeSpan idleTimeout;</code> | 保存空闲超时阈值。 |
| 13 | <code>private void StartIdleChecks()</code> | 读取配置并启动扫描。 |
| 14 | <code>{</code> | 开始代码作用域。 |
| 15 | <code>Stop();</code> | 先取消之前启动的扫描。 |
| 16 | <code>if (!string.Equals(ConfigurationManager.AppSettings["ServerRole"], "Game", StringComparison.OrdinalIgnoreCase) &#124;&#124;</code> | 服务器角色必须是 Game，角色比较忽略大小写。 |
| 17 | <code>bool.TryParse(ConfigurationManager.AppSettings["GatewayMode"], out bool gateway) && gateway) return;</code> | 启用了 GatewayMode 时也不启动；本次只处理直连游戏服 TCP。 |
| 18 | <code>idleTimeout = TimeSpan.FromSeconds(ReadIdleSetting("GameConnectionIdleTimeoutSeconds", 60));</code> | 读取空闲阈值，未配置时默认 60 秒。 |
| 19 | <code>int interval = ReadIdleSetting("GameConnectionCheckIntervalSeconds", 5);</code> | 读取扫描间隔，未配置时默认 5 秒。 |
| 20 | <code>idleCancellation = new CancellationTokenSource();</code> | 创建本轮扫描的停止令牌。 |
| 21 | <code>idleTask = RunIdleChecksAsync(interval, idleCancellation.Token);</code> | 启动单个扫描循环并保存任务。 |
| 22 | <code>}</code> | 结束当前代码作用域。 |
| 23 | <code>private static int ReadIdleSetting(string key, int fallback)</code> | 读取并校验一个秒数配置。 |
| 24 | <code>{</code> | 开始代码作用域。 |
| 25 | <code>string text = ConfigurationManager.AppSettings[key];</code> | 从 AppSettings 读取原始文本。 |
| 26 | <code>if (text == null) return fallback;</code> | 配置缺失时用指定默认值。 |
| 27 | <code>if (!int.TryParse(text, out int seconds) &#124;&#124; seconds &lt;= 0)</code> | 要求能解析为正整数，零、负数和非法文本均不接受。 |
| 28 | <code>throw new ConfigurationErrorsException(key + " must be a positive integer.");</code> | 启动时直接抛出配置错误，避免错误阈值悄悄运行。 |
| 29 | <code>return seconds;</code> | 返回合法秒数。 |
| 30 | <code>}</code> | 结束当前代码作用域。 |
| 31 | <code>public void Stop() =&gt; idleCancellation?.Cancel();</code> | 发出取消扫描请求。 |
| 32 | <code>public async Task StopAsync()</code> | 提供可以等待扫描结束的关机方法。 |
| 33 | <code>{</code> | 开始代码作用域。 |
| 34 | <code>Stop();</code> | 取消定时器等待。 |
| 35 | <code>if (idleTask != null) await idleTask;</code> | 已有任务时等待它退出。 |
| 36 | <code>}</code> | 结束当前代码作用域。 |
| 37 | <code>private async Task RunIdleChecksAsync(int interval, CancellationToken cancellation)</code> | 空闲检测的统一异步循环。 |
| 38 | <code>{</code> | 开始代码作用域。 |
| 39 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 40 | <code>{</code> | 开始代码作用域。 |
| 41 | <code>using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));</code> | 创建配置间隔的 PeriodicTimer。 |
| 42 | <code>while (await timer.WaitForNextTickAsync(cancellation))</code> | 等待每次扫描时刻并支持取消。 |
| 43 | <code>CheckIdleConnections(cancellation);</code> | 扫描当前已鉴权账号连接。 |
| 44 | <code>}</code> | 结束当前代码作用域。 |
| 45 | <code>catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }</code> | 服务器正常停止导致的取消不记为任务故障。 |
| 46 | <code>}</code> | 结束当前代码作用域。 |
| 47 | <code>private void CheckIdleConnections(CancellationToken cancellation)</code> | 查找可能超时的连接，并把最终关闭判断交给连接事件线程。 |
| 48 | <code>{</code> | 开始代码作用域。 |
| 49 | <code>foreach (IdSession session in accountSessions.Values)</code> | 只遍历 accountSessions，即已完成账号鉴权的当前连接。 |
| 50 | <code>{</code> | 开始代码作用域。 |
| 51 | <code>if (session.isWebSocket &#124;&#124; session.client == null &#124;&#124; !session.IsIdle(idleTimeout)) continue;</code> | 跳过 WebSocket、无通道、以及尚未超时的连接。 |
| 52 | <code>var channel = session.client;</code> | 保存这次扫描看到的通道对象，后面必须仍是同一个通道。 |
| 53 | <code>if (!channel.Active) continue;</code> | 已经关闭的通道无需再次关闭。 |
| 54 | <code>channel.EventLoop.Execute(() =&gt;</code> | 将关闭判断投递到该通道 EventLoop，和网络事件按线程顺序执行。 |
| 55 | <code>{</code> | 开始代码作用域。 |
| 56 | <code>if (cancellation.IsCancellationRequested) return;</code> | 如果服务正在停止，丢弃已排队的关闭判断。 |
| 57 | <code>lock (BindingSync)</code> | 与账号绑定、解绑、玩家连接引用清理使用同一把锁。 |
| 58 | <code>{</code> | 开始代码作用域。 |
| 59 | <code>if (!IsCurrentConnection(session) &#124;&#124; !ReferenceEquals(session.client, channel) &#124;&#124;</code> | 重新检查它仍是账号当前连接，且 IdSession 仍指向扫描时那个通道。 |
| 60 | <code>!channel.Active &#124;&#124; !session.IsIdle(idleTimeout)) return;</code> | 重新检查通道存活和最后有效接收时间，避免扫描后已收到消息仍被关闭。 |
| 61 | <code>NLog.LogManager.GetCurrentClassLogger().Info(</code> | 创建并调用日志记录器。 |
| 62 | <code>$"Closing idle TCP session: account[{session.accountId}] player[{session.playerId}] idleSeconds[{session.IdleSeconds:F1}]");</code> | 日志包括账号、玩家和空闲秒数，保留一位小数。 |
| 63 | <code>channel.CloseAsync();</code> | 仅关闭网络通道，由原有 ChannelInactive → OnClientExit 继续清理，不撤销 token。 |
| 64 | <code>}</code> | 结束当前代码作用域。 |
| 65 | <code>});</code> | 结束对象初始化/调用表达式。 |
| 66 | <code>}</code> | 结束当前代码作用域。 |
| 67 | <code>}</code> | 结束当前代码作用域。 |
| 68 | <code>}</code> | 结束当前代码作用域。 |
| 69 | <code>}</code> | 结束当前代码作用域。 |

## LogoutAndTcpLifecycleTests.cs

文件：[D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServerTests/LogoutAndTcpLifecycleTests.cs](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/GameServerTests/LogoutAndTcpLifecycleTests.cs)

本次新增文件，因此下面包含整个文件的每一行。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 1 | <code>using System.Diagnostics;</code> | 引入 System.Diagnostics 命名空间，供此文件使用相关类型。 |
| 2 | <code>using System.Net;</code> | 引入 System.Net 命名空间，供此文件使用相关类型。 |
| 3 | <code>using System.Net.Sockets;</code> | 引入 System.Net.Sockets 命名空间，供此文件使用相关类型。 |
| 4 | <code>using System.Reflection;</code> | 引入 System.Reflection 命名空间，供此文件使用相关类型。 |
| 5 | <code>using System.Text;</code> | 引入 System.Text 命名空间，供此文件使用相关类型。 |
| 6 | <code>using DotNetty.Transport.Channels.Embedded;</code> | 引入 DotNetty.Transport.Channels.Embedded 命名空间，供此文件使用相关类型。 |
| 7 | <code>using Framework.Core.Net;</code> | 引入 Framework.Core.Net 命名空间，供此文件使用相关类型。 |
| 8 | <code>using Framework.Core.Serializer;</code> | 引入 Framework.Core.Serializer 命名空间，供此文件使用相关类型。 |
| 9 | <code>using Game.Core.EntityMgr;</code> | 引入 Game.Core.EntityMgr 命名空间，供此文件使用相关类型。 |
| 10 | <code>using Game.Core.GM_Auth;</code> | 引入 Game.Core.GM_Auth 命名空间，供此文件使用相关类型。 |
| 11 | <code>using Game.Datas.GMEntities;</code> | 引入 Game.Datas.GMEntities 命名空间，供此文件使用相关类型。 |
| 12 | <code>using Game.Datas.Messages;</code> | 引入 Game.Datas.Messages 命名空间，供此文件使用相关类型。 |
| 13 | <code>using Game.Entries.Modules;</code> | 引入 Game.Entries.Modules 命名空间，供此文件使用相关类型。 |
| 14 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 15 | <code>namespace GameServerTests;</code> | 把类型放入 GameServerTests 命名空间。 |
| 16 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 17 | <code>[CollectionDefinition("Logout lifecycle", DisableParallelization = true)]</code> | 声明不并行执行的测试集合，避免全局单例与环境变量互相干扰。 |
| 18 | <code>public class LogoutLifecycleCollection { }</code> | 提供测试集合定义承载类型，不包含业务逻辑。 |
| 19 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 20 | <code>[Collection("Logout lifecycle")]</code> | 把本测试类加入上述串行测试集合。 |
| 21 | <code>public class LogoutAndTcpLifecycleTests</code> | 声明登出及 TCP 生命周期测试类。 |
| 22 | <code>{</code> | 开始代码作用域。 |
| 23 | <code>private const string Secret = "logout-tests-only-secret-with-at-least-32-bytes";</code> | 测试专用 HMAC 密钥，不用于生产配置。 |
| 24 | <code>private static GM_AuthSessionMgr Manager(IReadOnlyDictionary&lt;int, string&gt; urls = null)</code> | 定义测试辅助方法：private static GM_AuthSessionMgr Manager(IReadOnlyDictionary<int, string> urls = null)。 |
| 25 | <code>{</code> | 开始代码作用域。 |
| 26 | <code>var manager = new GM_AuthSessionMgr();</code> | 创建独立 Manager，避免业务单例状态影响测试。 |
| 27 | <code>string previous = Environment.GetEnvironmentVariable("GAME_TICKET_HMAC_SECRET");</code> | 保存测试前的 HMAC 环境变量，结束后恢复。 |
| 28 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 29 | <code>{</code> | 开始代码作用域。 |
| 30 | <code>Environment.SetEnvironmentVariable("GAME_TICKET_HMAC_SECRET", Secret);</code> | 设置测试密钥或在 finally 中恢复原环境变量，避免污染其他测试。 |
| 31 | <code>manager.Init(urls);</code> | 初始化测试 Manager，启动其后台任务并加载目标通知地址。 |
| 32 | <code>}</code> | 结束当前代码作用域。 |
| 33 | <code>finally { Environment.SetEnvironmentVariable("GAME_TICKET_HMAC_SECRET", previous); }</code> | 设置测试密钥或在 finally 中恢复原环境变量，避免污染其他测试。 |
| 34 | <code>return manager;</code> | 返回初始化后的测试 Manager。 |
| 35 | <code>}</code> | 结束当前代码作用域。 |
| 36 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 37 | <code>private static void Set(object target, string name, object value) =&gt;</code> | 定义测试辅助方法：private static void Set(object target, string name, object value) =>。 |
| 38 | <code>target.GetType().GetField(name, BindingFlags.Instance &#124; BindingFlags.Public &#124; BindingFlags.NonPublic).SetValue(target, value);</code> | 使用反射访问内部测试状态或方法：target.GetType().GetField(name, BindingFlags.Instance &#124; BindingFlags.Public &#124; BindingFlags.NonPublic).SetValue(target, value)。 |
| 39 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 40 | <code>[Fact]</code> | 标记下面方法为 xUnit 测试。 |
| 41 | <code>public void MalformedPacketsDoNotRefreshActivityButHeartbeatDoes()</code> | 定义 MalformedPacketsDoNotRefreshActivityButHeartbeatDoes 测试入口；下面逐行安排、执行和断言此场景。 |
| 42 | <code>{</code> | 开始代码作用域。 |
| 43 | <code>var assembly = typeof(IdSession).Assembly;</code> | 获取服务端网络类型所在程序集，供反射访问内部消息工厂和分发器。 |
| 44 | <code>Type factoryType = assembly.GetType("Framework.Core.Serializer.MessageFactory");</code> | 使用反射访问内部测试状态或方法：Type factoryType = assembly.GetType("Framework.Core.Serializer.MessageFactory")。 |
| 45 | <code>object factory = factoryType.GetField("Instance").GetValue(null);</code> | 使用反射访问内部测试状态或方法：object factory = factoryType.GetField("Instance").GetValue(null)。 |
| 46 | <code>var mappings = (System.Collections.IDictionary)factoryType.GetField("keyTypeDic", BindingFlags.Instance &#124; BindingFlags.NonPublic).GetValue(factory);</code> | 使用反射访问内部测试状态或方法：var mappings = (System.Collections.IDictionary)factoryType.GetField("keyTypeDic", BindingFlags.Instance &#124; BindingFlags.NonPublic).GetValue(factory)。 |
| 47 | <code>var previousMappings = mappings.Cast&lt;System.Collections.DictionaryEntry&gt;().ToArray();</code> | 保存或恢复协议工厂原映射，避免重复注册污染后续测试。 |
| 48 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 49 | <code>{</code> | 开始代码作用域。 |
| 50 | <code>if (mappings.Count == 0) factoryType.GetMethod("InitMeesagePool").Invoke(factory, null);</code> | 使用反射访问内部测试状态或方法：if (mappings.Count == 0) factoryType.GetMethod("InitMeesagePool").Invoke(factory, null)。 |
| 51 | <code>Type dispatcherType = assembly.GetType("Framework.Core.Net.MessageDispatcher");</code> | 使用反射访问内部测试状态或方法：Type dispatcherType = assembly.GetType("Framework.Core.Net.MessageDispatcher")。 |
| 52 | <code>object dispatcher = dispatcherType.GetField("Instance").GetValue(null);</code> | 使用反射访问内部测试状态或方法：object dispatcher = dispatcherType.GetField("Instance").GetValue(null)。 |
| 53 | <code>var session = new IdSession { accountId = 220, playerId = 220 };</code> | 创建此测试使用的连接会话对象：var session = new IdSession { accountId = 220, playerId = 220 }。 |
| 54 | <code>long stale = Stopwatch.GetTimestamp() - 61 * Stopwatch.Frequency;</code> | 取得单调计时 tick，并按此行偏移构造已经空闲 61 秒的连接。 |
| 55 | <code>Set(session, "lastValidActivity", stale);</code> | 测试直接设置内部时间/阈值，构造超时或截止情形，避免实际等待一分钟。 |
| 56 | <code>byte[] packet = MessagePusher.PackMessage(new ReqHeartbeat(), 0);</code> | 打包真实心跳消息，供有效/无效数据测试使用。 |
| 57 | <code>var receive = dispatcherType.GetMethod("OnClientMsg");</code> | 使用反射访问内部测试状态或方法：var receive = dispatcherType.GetMethod("OnClientMsg")。 |
| 58 | <code>receive.Invoke(dispatcher, new object[] { session, new byte[3], 0, 3 });</code> | 调用实际消息分发入口，输入本行指定的短包、非法 protobuf 或合法心跳。 |
| 59 | <code>Assert.True(session.IsIdle(TimeSpan.FromSeconds(60)));</code> | 在 MalformedPacketsDoNotRefreshActivityButHeartbeatDoes 测试中验证：Assert.True(session.IsIdle(TimeSpan.FromSeconds(60)))；不满足条件时测试失败。 |
| 60 | <code>byte[] malformed = packet.Take(ProtocolPacketLayout.HeaderLength).Concat(new byte[] { 0x80 }).ToArray();</code> | 跳过/使用标准协议头长度，只处理 protobuf 正文。 |
| 61 | <code>receive.Invoke(dispatcher, new object[] { session, malformed, 0, malformed.Length });</code> | 调用实际消息分发入口，输入本行指定的短包、非法 protobuf 或合法心跳。 |
| 62 | <code>Assert.True(session.IsIdle(TimeSpan.FromSeconds(60)));</code> | 在 MalformedPacketsDoNotRefreshActivityButHeartbeatDoes 测试中验证：Assert.True(session.IsIdle(TimeSpan.FromSeconds(60)))；不满足条件时测试失败。 |
| 63 | <code>receive.Invoke(dispatcher, new object[] { session, packet, 0, packet.Length });</code> | 调用实际消息分发入口，输入本行指定的短包、非法 protobuf 或合法心跳。 |
| 64 | <code>Assert.False(session.IsIdle(TimeSpan.FromSeconds(60)));</code> | 在 MalformedPacketsDoNotRefreshActivityButHeartbeatDoes 测试中验证：Assert.False(session.IsIdle(TimeSpan.FromSeconds(60)))；不满足条件时测试失败。 |
| 65 | <code>}</code> | 结束当前代码作用域。 |
| 66 | <code>finally</code> | 无论正常返回、取消或异常都执行下面的清理。 |
| 67 | <code>{</code> | 开始代码作用域。 |
| 68 | <code>mappings.Clear();</code> | 恢复协议工厂状态前先清空测试产生的映射。 |
| 69 | <code>foreach (var entry in previousMappings) mappings.Add(entry.Key, entry.Value);</code> | 保存或恢复协议工厂原映射，避免重复注册污染后续测试。 |
| 70 | <code>}</code> | 结束当前代码作用域。 |
| 71 | <code>}</code> | 结束当前代码作用域。 |
| 72 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 73 | <code>[Fact]</code> | 标记下面方法为 xUnit 测试。 |
| 74 | <code>public async Task NotificationTimeoutIsBoundedAndDeadlineRemovesPendingWork()</code> | 定义 NotificationTimeoutIsBoundedAndDeadlineRemovesPendingWork 测试入口；下面逐行安排、执行和断言此场景。 |
| 75 | <code>{</code> | 开始代码作用域。 |
| 76 | <code>int port = FreePort();</code> | 取得本机临时可用端口，减少测试端口冲突。 |
| 77 | <code>using var listener = new HttpListener();</code> | 引入 var listener = new HttpListener() 命名空间，供此文件使用相关类型。 |
| 78 | <code>listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();</code> | 监听临时本机端口并启动服务，供内部通知请求使用。 |
| 79 | <code>var manager = Manager(new Dictionary&lt;int, string&gt; { [1] = $"http://127.0.0.1:{port}/silent" });</code> | 创建独立认证 Manager，并使用此行给定的内部通知 URL。 |
| 80 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 81 | <code>{</code> | 开始代码作用域。 |
| 82 | <code>string token = manager.CreateLoginSession(221);</code> | 创建测试登录 token；重复创建同账号模拟新登录替代旧登录。 |
| 83 | <code>manager.IssueGameTicket(token, 1);</code> | 为该测试 token 签发票据，同时记录目标游戏服。 |
| 84 | <code>Task&lt;HttpListenerContext&gt; accepted = listener.GetContextAsync();</code> | 接收测试 HTTP 请求；模拟实际内部通知链路。 |
| 85 | <code>var watch = Stopwatch.StartNew();</code> | 开始实测通知耗时，用于断言两秒通知超时。 |
| 86 | <code>Assert.Equal(AuthOperationStatus.NotificationPending, await manager.LogoutAsync(token));</code> | 新增凭证已撤销但远端通知未全部确认的领域状态；放在枚举末尾保持旧值。 |
| 87 | <code>Assert.InRange(watch.Elapsed.TotalSeconds, 1.5, 6);</code> | 在 NotificationTimeoutIsBoundedAndDeadlineRemovesPendingWork 测试中验证：Assert.InRange(watch.Elapsed.TotalSeconds, 1.5, 6)；不满足条件时测试失败。 |
| 88 | <code>(await accepted).Response.Close();</code> | 关闭已经接收的测试响应，释放连接。 |
| 89 | <code>await manager.StopLogoutRetriesAsync();</code> | 取消并等待测试 Manager 的后台重试任务结束，防止跨测试泄漏。 |
| 90 | <code>var records = (System.Collections.IDictionary)typeof(GM_AuthSessionMgr)</code> | 取得 Manager 内部通知记录以验证截止删除。 |
| 91 | <code>.GetField("logoutRecords", BindingFlags.Instance &#124; BindingFlags.NonPublic).GetValue(manager);</code> | 使用反射访问内部测试状态或方法：.GetField("logoutRecords", BindingFlags.Instance &#124; BindingFlags.NonPublic).GetValue(manager)。 |
| 92 | <code>object record = records.Values.Cast&lt;object&gt;().Single();</code> | 读取本测试唯一的待通知记录。 |
| 93 | <code>Set(record, "Deadline", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1);</code> | 测试直接设置内部时间/阈值，构造超时或截止情形，避免实际等待一分钟。 |
| 94 | <code>string hash = records.Keys.Cast&lt;string&gt;().Single();</code> | 读取对应 token 哈希键。 |
| 95 | <code>var retry = (Task)typeof(GM_AuthSessionMgr).GetMethod("RetryLogoutAsync", BindingFlags.Instance &#124; BindingFlags.NonPublic)</code> | 使用反射访问内部测试状态或方法：var retry = (Task)typeof(GM_AuthSessionMgr).GetMethod("RetryLogoutAsync", BindingFlags.Instance &#124; BindingFlags.NonPublic)。 |
| 96 | <code>.Invoke(manager, new object[] { hash, record, CancellationToken.None });</code> | 传入 token 哈希、记录和不取消令牌，反射调用单记录截止检查。 |
| 97 | <code>await retry;</code> | 等待截止检查执行完毕后再断言记录已删除。 |
| 98 | <code>Assert.Empty(records.Keys.Cast&lt;string&gt;());</code> | 在 NotificationTimeoutIsBoundedAndDeadlineRemovesPendingWork 测试中验证：Assert.Empty(records.Keys.Cast<string>())；不满足条件时测试失败。 |
| 99 | <code>Assert.False(manager.ValidateAccessToken(token).IsSuccess);</code> | 在 NotificationTimeoutIsBoundedAndDeadlineRemovesPendingWork 测试中验证：Assert.False(manager.ValidateAccessToken(token).IsSuccess)；不满足条件时测试失败。 |
| 100 | <code>}</code> | 结束当前代码作用域。 |
| 101 | <code>finally { await manager.StopLogoutRetriesAsync(); }</code> | 取消并等待测试 Manager 的后台重试任务结束，防止跨测试泄漏。 |
| 102 | <code>}</code> | 结束当前代码作用域。 |
| 103 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 104 | <code>[Fact]</code> | 标记下面方法为 xUnit 测试。 |
| 105 | <code>public void LogoutProtocolRoundTrips()</code> | 定义 LogoutProtocolRoundTrips 测试入口；下面逐行安排、执行和断言此场景。 |
| 106 | <code>{</code> | 开始代码作用域。 |
| 107 | <code>Message[] messages = { new ReqUserLogout { accessToken = "token" },</code> | 准备请求和响应两个协议样本。 |
| 108 | <code>new ResUserLogout { status = -1, errorMessage = "pending" } };</code> | 用负数状态码和文案检验 Varint 与字符串编解码。 |
| 109 | <code>foreach (Message message in messages)</code> | 分别检查请求与响应的协议往返。 |
| 110 | <code>{</code> | 开始代码作用域。 |
| 111 | <code>using var stream = new MemoryStream();</code> | 引入 var stream = new MemoryStream() 命名空间，供此文件使用相关类型。 |
| 112 | <code>Assert.True(GoogleProtocolCodec.TrySerialize(message, stream, out string error), error);</code> | 在 LogoutProtocolRoundTrips 测试中验证：Assert.True(GoogleProtocolCodec.TrySerialize(message, stream, out string error), error)；不满足条件时测试失败。 |
| 113 | <code>stream.Position = 0;</code> | 序列化后把流位置重置，允许反序列化读取刚写入的内容。 |
| 114 | <code>Assert.True(GoogleProtocolCodec.TryDeserialize(message.GetType(), stream, out Message result, out error), error);</code> | 在 LogoutProtocolRoundTrips 测试中验证：Assert.True(GoogleProtocolCodec.TryDeserialize(message.GetType(), stream, out Message result, out error), error)；不满足条件时测试失败。 |
| 115 | <code>Assert.Equal(message.GetCmd(), result.GetCmd());</code> | 在 LogoutProtocolRoundTrips 测试中验证：Assert.Equal(message.GetCmd(), result.GetCmd())；不满足条件时测试失败。 |
| 116 | <code>if (result is ReqUserLogout request) { Assert.Equal(1008, request.GetCmd()); Assert.Equal("token", request.accessToken); }</code> | 如果解码成请求，分别检查命令号等于 1008 和 token 字符串保持不变。 |
| 117 | <code>if (result is ResUserLogout response) { Assert.Equal(1009, response.GetCmd()); Assert.Equal(-1, response.status); Assert.Equal("pending", response.errorMessage); }</code> | 如果解码成响应，分别检查命令号 1009、负数状态码 -1 和错误文案 pending 完整保留。 |
| 118 | <code>}</code> | 结束当前代码作用域。 |
| 119 | <code>}</code> | 结束当前代码作用域。 |
| 120 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 121 | <code>[Fact]</code> | 标记下面方法为 xUnit 测试。 |
| 122 | <code>public async Task LogoutWithoutGameTicketIsIdempotentAndNewLoginSurvives()</code> | 定义 LogoutWithoutGameTicketIsIdempotentAndNewLoginSurvives 测试入口；下面逐行安排、执行和断言此场景。 |
| 123 | <code>{</code> | 开始代码作用域。 |
| 124 | <code>var manager = Manager();</code> | 创建独立认证 Manager，并使用此行给定的内部通知 URL。 |
| 125 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 126 | <code>{</code> | 开始代码作用域。 |
| 127 | <code>string oldToken = manager.CreateLoginSession(123);</code> | 创建测试登录 token；重复创建同账号模拟新登录替代旧登录。 |
| 128 | <code>Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(oldToken));</code> | 在 LogoutWithoutGameTicketIsIdempotentAndNewLoginSurvives 测试中验证：Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(oldToken))；不满足条件时测试失败。 |
| 129 | <code>Assert.False(manager.ValidateAccessToken(oldToken).IsSuccess);</code> | 在 LogoutWithoutGameTicketIsIdempotentAndNewLoginSurvives 测试中验证：Assert.False(manager.ValidateAccessToken(oldToken).IsSuccess)；不满足条件时测试失败。 |
| 130 | <code>Assert.Equal(AuthOperationStatus.InvalidAccessToken, manager.IssueGameTicket(oldToken, 1).Status);</code> | 在 LogoutWithoutGameTicketIsIdempotentAndNewLoginSurvives 测试中验证：Assert.Equal(AuthOperationStatus.InvalidAccessToken, manager.IssueGameTicket(oldToken, 1).Status)；不满足条件时测试失败。 |
| 131 | <code>string newToken = manager.CreateLoginSession(123);</code> | 创建测试登录 token；重复创建同账号模拟新登录替代旧登录。 |
| 132 | <code>Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(oldToken));</code> | 在 LogoutWithoutGameTicketIsIdempotentAndNewLoginSurvives 测试中验证：Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(oldToken))；不满足条件时测试失败。 |
| 133 | <code>Assert.True(manager.ValidateAccessToken(newToken).IsSuccess);</code> | 在 LogoutWithoutGameTicketIsIdempotentAndNewLoginSurvives 测试中验证：Assert.True(manager.ValidateAccessToken(newToken).IsSuccess)；不满足条件时测试失败。 |
| 134 | <code>Assert.Equal(AuthOperationStatus.InvalidAccessToken, await manager.LogoutAsync("unknown"));</code> | 在 LogoutWithoutGameTicketIsIdempotentAndNewLoginSurvives 测试中验证：Assert.Equal(AuthOperationStatus.InvalidAccessToken, await manager.LogoutAsync("unknown"))；不满足条件时测试失败。 |
| 135 | <code>}</code> | 结束当前代码作用域。 |
| 136 | <code>finally { await manager.StopLogoutRetriesAsync(); }</code> | 取消并等待测试 Manager 的后台重试任务结束，防止跨测试泄漏。 |
| 137 | <code>}</code> | 结束当前代码作用域。 |
| 138 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 139 | <code>[Fact]</code> | 标记下面方法为 xUnit 测试。 |
| 140 | <code>public async Task ReplacedTokenLogoutCannotRevokeNewLogin()</code> | 定义 ReplacedTokenLogoutCannotRevokeNewLogin 测试入口；下面逐行安排、执行和断言此场景。 |
| 141 | <code>{</code> | 开始代码作用域。 |
| 142 | <code>var manager = Manager();</code> | 创建独立认证 Manager，并使用此行给定的内部通知 URL。 |
| 143 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 144 | <code>{</code> | 开始代码作用域。 |
| 145 | <code>string oldToken = manager.CreateLoginSession(124);</code> | 创建测试登录 token；重复创建同账号模拟新登录替代旧登录。 |
| 146 | <code>string newToken = manager.CreateLoginSession(124);</code> | 创建测试登录 token；重复创建同账号模拟新登录替代旧登录。 |
| 147 | <code>Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(oldToken));</code> | 在 ReplacedTokenLogoutCannotRevokeNewLogin 测试中验证：Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(oldToken))；不满足条件时测试失败。 |
| 148 | <code>Assert.True(manager.ValidateAccessToken(newToken).IsSuccess);</code> | 在 ReplacedTokenLogoutCannotRevokeNewLogin 测试中验证：Assert.True(manager.ValidateAccessToken(newToken).IsSuccess)；不满足条件时测试失败。 |
| 149 | <code>}</code> | 结束当前代码作用域。 |
| 150 | <code>finally { await manager.StopLogoutRetriesAsync(); }</code> | 取消并等待测试 Manager 的后台重试任务结束，防止跨测试泄漏。 |
| 151 | <code>}</code> | 结束当前代码作用域。 |
| 152 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 153 | <code>[Fact]</code> | 标记下面方法为 xUnit 测试。 |
| 154 | <code>public async Task NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession()</code> | 定义 NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession 测试入口；下面逐行安排、执行和断言此场景。 |
| 155 | <code>{</code> | 开始代码作用域。 |
| 156 | <code>var game = Manager();</code> | 创建独立认证 Manager，并使用此行给定的内部通知 URL。 |
| 157 | <code>var requests = new List&lt;ReqInvalidateAccountSession&gt;();</code> | 创建或接收真实 AUTH 1007 失效通知对象。 |
| 158 | <code>int port = FreePort();</code> | 取得本机临时可用端口，减少测试端口冲突。 |
| 159 | <code>using var listener = new HttpListener();</code> | 引入 var listener = new HttpListener() 命名空间，供此文件使用相关类型。 |
| 160 | <code>listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();</code> | 监听临时本机端口并启动服务，供内部通知请求使用。 |
| 161 | <code>var login = Manager(new Dictionary&lt;int, string&gt; { [1] = $"http://127.0.0.1:{port}/invalidate" });</code> | 创建独立认证 Manager，并使用此行给定的内部通知 URL。 |
| 162 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 163 | <code>{</code> | 开始代码作用域。 |
| 164 | <code>string token = login.CreateLoginSession(125);</code> | 创建测试登录 token；重复创建同账号模拟新登录替代旧登录。 |
| 165 | <code>string oldTicket = login.IssueGameTicket(token, 1).Ticket;</code> | 为该测试 token 签发票据，同时记录目标游戏服。 |
| 166 | <code>Task server = Task.Run(async () =&gt;</code> | 启动测试监听服务任务，和登出通知发送并行运行。 |
| 167 | <code>{</code> | 开始代码作用域。 |
| 168 | <code>var context = await listener.GetContextAsync();</code> | 接收测试 HTTP 请求；模拟实际内部通知链路。 |
| 169 | <code>using var reader = new StreamReader(context.Request.InputStream);</code> | 引入 var reader = new StreamReader(context.Request.InputStream) 命名空间，供此文件使用相关类型。 |
| 170 | <code>byte[] packet = Convert.FromBase64String(await reader.ReadToEndAsync());</code> | 把测试收到的 Base64 正文还原为标准业务包。 |
| 171 | <code>using var stream = new MemoryStream(packet, ProtocolPacketLayout.HeaderLength,</code> | 引入 var stream = new MemoryStream(packet, ProtocolPacketLayout.HeaderLength, 命名空间，供此文件使用相关类型。 |
| 172 | <code>packet.Length - ProtocolPacketLayout.HeaderLength);</code> | 跳过/使用标准协议头长度，只处理 protobuf 正文。 |
| 173 | <code>Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ReqInvalidateAccountSession), stream, out Message decoded, out string error), error);</code> | 在 NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession 测试中验证：Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ReqInvalidateAccountSession), stream, out Message decoded, out string error), error)；不满足条件时测试失败。 |
| 174 | <code>var request = (ReqInvalidateAccountSession)decoded; requests.Add(request);</code> | 创建或接收真实 AUTH 1007 失效通知对象。 |
| 175 | <code>Assert.True(game.ApplySessionInvalidation(Command(request)));</code> | 在 NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession 测试中验证：Assert.True(game.ApplySessionInvalidation(Command(request)))；不满足条件时测试失败。 |
| 176 | <code>byte[] reply = Encoding.UTF8.GetBytes("OK");</code> | 将测试响应文本编码为 UTF-8 字节。 |
| 177 | <code>await context.Response.OutputStream.WriteAsync(reply); context.Response.Close();</code> | 向通知发送者写回测试 HTTP 响应，并按此行关闭响应。 |
| 178 | <code>});</code> | 结束对象初始化/调用表达式。 |
| 179 | <code>Assert.Equal(AuthOperationStatus.Success, await login.LogoutAsync(token));</code> | 在 NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession 测试中验证：Assert.Equal(AuthOperationStatus.Success, await login.LogoutAsync(token))；不满足条件时测试失败。 |
| 180 | <code>await server.WaitAsync(TimeSpan.FromSeconds(5));</code> | 限制测试等待时间，避免失败时无限挂起。 |
| 181 | <code>Assert.Equal(AuthOperationStatus.SupersededSession,</code> | 在 NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession 测试中验证：Assert.Equal(AuthOperationStatus.SupersededSession,；不满足条件时测试失败。 |
| 182 | <code>game.AuthenticateGameSession(new IdSession(), oldTicket, 1).Status);</code> | 创建此测试使用的连接会话对象：game.AuthenticateGameSession(new IdSession(), oldTicket, 1).Status)。 |
| 183 | <code>string fresh = login.CreateLoginSession(125);</code> | 创建测试登录 token；重复创建同账号模拟新登录替代旧登录。 |
| 184 | <code>string ticket = login.IssueGameTicket(fresh, 1).Ticket;</code> | 为该测试 token 签发票据，同时记录目标游戏服。 |
| 185 | <code>var session = new IdSession();</code> | 创建此测试使用的连接会话对象：var session = new IdSession()。 |
| 186 | <code>Assert.Equal(AuthOperationStatus.Success, game.AuthenticateGameSession(session, ticket, 1).Status);</code> | 在 NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession 测试中验证：Assert.Equal(AuthOperationStatus.Success, game.AuthenticateGameSession(session, ticket, 1).Status)；不满足条件时测试失败。 |
| 187 | <code>Assert.True(game.ApplySessionInvalidation(Command(requests[0])));</code> | 在 NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession 测试中验证：Assert.True(game.ApplySessionInvalidation(Command(requests[0])))；不满足条件时测试失败。 |
| 188 | <code>Assert.True(SessionMgr.Instance.IsCurrentConnection(session));</code> | 在 NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession 测试中验证：Assert.True(SessionMgr.Instance.IsCurrentConnection(session))；不满足条件时测试失败。 |
| 189 | <code>Assert.Equal(AuthOperationStatus.Success, await login.LogoutAsync(token));</code> | 在 NotificationRejectsOldTicketsAndDelayedRetryPreservesNewSession 测试中验证：Assert.Equal(AuthOperationStatus.Success, await login.LogoutAsync(token))；不满足条件时测试失败。 |
| 190 | <code>}</code> | 结束当前代码作用域。 |
| 191 | <code>finally { await login.StopLogoutRetriesAsync(); await game.StopLogoutRetriesAsync(); }</code> | 取消并等待测试 Manager 的后台重试任务结束，防止跨测试泄漏。 |
| 192 | <code>}</code> | 结束当前代码作用域。 |
| 193 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 194 | <code>[Fact]</code> | 标记下面方法为 xUnit 测试。 |
| 195 | <code>public async Task FailedNotificationRetriesAndAcknowledgedTargetsAreNotRetried()</code> | 定义 FailedNotificationRetriesAndAcknowledgedTargetsAreNotRetried 测试入口；下面逐行安排、执行和断言此场景。 |
| 196 | <code>{</code> | 开始代码作用域。 |
| 197 | <code>int port = FreePort();</code> | 取得本机临时可用端口，减少测试端口冲突。 |
| 198 | <code>using var listener = new HttpListener();</code> | 引入 var listener = new HttpListener() 命名空间，供此文件使用相关类型。 |
| 199 | <code>listener.Prefixes.Add($"http://127.0.0.1:{port}/"); listener.Start();</code> | 监听临时本机端口并启动服务，供内部通知请求使用。 |
| 200 | <code>var manager = Manager(new Dictionary&lt;int, string&gt; {</code> | 创建独立认证 Manager，并使用此行给定的内部通知 URL。 |
| 201 | <code>[1] = $"http://127.0.0.1:{port}/one", [2] = $"http://127.0.0.1:{port}/two" });</code> | 配置两个不同通知目标路径，分别模拟立即确认和首次失败后恢复的游戏服。 |
| 202 | <code>var count = new System.Collections.Concurrent.ConcurrentDictionary&lt;string, int&gt;();</code> | 按 URL 统计每个目标通知次数。 |
| 203 | <code>var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);</code> | 创建测试完成信号，并异步调度续执行，避免在 HTTP 回调内同步重入。 |
| 204 | <code>using var cancellation = new CancellationTokenSource();</code> | 引入 var cancellation = new CancellationTokenSource() 命名空间，供此文件使用相关类型。 |
| 205 | <code>Task server = Task.Run(async () =&gt;</code> | 启动测试监听服务任务，和登出通知发送并行运行。 |
| 206 | <code>{</code> | 开始代码作用域。 |
| 207 | <code>while (!cancellation.IsCancellationRequested)</code> | 持续接受模拟远端请求，直到测试清理取消。 |
| 208 | <code>{</code> | 开始代码作用域。 |
| 209 | <code>var context = await listener.GetContextAsync().WaitAsync(cancellation.Token);</code> | 接收测试 HTTP 请求；模拟实际内部通知链路。 |
| 210 | <code>string path = context.Request.Url.AbsolutePath;</code> | 通过 URL 路径区分两个游戏服目标。 |
| 211 | <code>int attempt = count.AddOrUpdate(path, 1, (_, n) =&gt; n + 1);</code> | 线程安全地递增该目标收到的通知次数。 |
| 212 | <code>bool ok = path == "/one" &#124;&#124; attempt &gt; 1;</code> | 第一个目标始终成功，第二个目标首次失败、重试成功。 |
| 213 | <code>byte[] reply = Encoding.UTF8.GetBytes(ok ? "OK" : "INVALID");</code> | 将测试响应文本编码为 UTF-8 字节。 |
| 214 | <code>await context.Response.OutputStream.WriteAsync(reply); context.Response.Close();</code> | 向通知发送者写回测试 HTTP 响应，并按此行关闭响应。 |
| 215 | <code>if (path == "/two" && ok) recovered.TrySetResult();</code> | 通知测试等待方：模拟远端已恢复并确认。 |
| 216 | <code>}</code> | 结束当前代码作用域。 |
| 217 | <code>});</code> | 结束对象初始化/调用表达式。 |
| 218 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 219 | <code>{</code> | 开始代码作用域。 |
| 220 | <code>string token = manager.CreateLoginSession(126);</code> | 创建测试登录 token；重复创建同账号模拟新登录替代旧登录。 |
| 221 | <code>manager.IssueGameTicket(token, 1); manager.IssueGameTicket(token, 2);</code> | 为该测试 token 签发票据，同时记录目标游戏服。 |
| 222 | <code>Assert.Equal(AuthOperationStatus.NotificationPending, await manager.LogoutAsync(token));</code> | 新增凭证已撤销但远端通知未全部确认的领域状态；放在枚举末尾保持旧值。 |
| 223 | <code>Assert.False(manager.ValidateAccessToken(token).IsSuccess);</code> | 在 FailedNotificationRetriesAndAcknowledgedTargetsAreNotRetried 测试中验证：Assert.False(manager.ValidateAccessToken(token).IsSuccess)；不满足条件时测试失败。 |
| 224 | <code>await recovered.Task.WaitAsync(TimeSpan.FromSeconds(8));</code> | 限制测试等待时间，避免失败时无限挂起。 |
| 225 | <code>Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(token));</code> | 在 FailedNotificationRetriesAndAcknowledgedTargetsAreNotRetried 测试中验证：Assert.Equal(AuthOperationStatus.Success, await manager.LogoutAsync(token))；不满足条件时测试失败。 |
| 226 | <code>Assert.Equal(1, count["/one"]); Assert.True(count["/two"] &gt;= 2);</code> | 在 FailedNotificationRetriesAndAcknowledgedTargetsAreNotRetried 测试中验证：Assert.Equal(1, count["/one"]); Assert.True(count["/two"] >= 2)；不满足条件时测试失败。 |
| 227 | <code>}</code> | 结束当前代码作用域。 |
| 228 | <code>finally</code> | 无论正常返回、取消或异常都执行下面的清理。 |
| 229 | <code>{</code> | 开始代码作用域。 |
| 230 | <code>await manager.StopLogoutRetriesAsync(); cancellation.Cancel();</code> | 取消并等待测试 Manager 的后台重试任务结束，防止跨测试泄漏。 |
| 231 | <code>try { await server; } catch (OperationCanceledException) { }</code> | 等待模拟服务器退出，将主动停止导致的取消视为正常。 |
| 232 | <code>}</code> | 结束当前代码作用域。 |
| 233 | <code>}</code> | 结束当前代码作用域。 |
| 234 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 235 | <code>[Fact]</code> | 标记下面方法为 xUnit 测试。 |
| 236 | <code>public void OldUnbindAndIdleClosureCannotRemoveNewSession()</code> | 定义 OldUnbindAndIdleClosureCannotRemoveNewSession 测试入口；下面逐行安排、执行和断言此场景。 |
| 237 | <code>{</code> | 开始代码作用域。 |
| 238 | <code>var manager = new SessionMgr();</code> | 创建独立 SessionMgr，隔离本测试的账号连接映射。 |
| 239 | <code>var oldChannel = new EmbeddedChannel(); var newChannel = new EmbeddedChannel();</code> | 创建 DotNetty 内存通道，无需外部 TCP 服务即可测试事件循环与关闭。 |
| 240 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 241 | <code>{</code> | 开始代码作用域。 |
| 242 | <code>var old = new IdSession { client = oldChannel };</code> | 创建此测试使用的连接会话对象：var old = new IdSession { client = oldChannel }。 |
| 243 | <code>var current = new IdSession { client = newChannel };</code> | 创建此测试使用的连接会话对象：var current = new IdSession { client = newChannel }。 |
| 244 | <code>manager.BindAuthenticatedAccount(old, 200, "old");</code> | 执行账号鉴权绑定；第二次绑定模拟新连接替代旧连接。 |
| 245 | <code>manager.BindAuthenticatedAccount(current, 200, "new");</code> | 执行账号鉴权绑定；第二次绑定模拟新连接替代旧连接。 |
| 246 | <code>manager.UnbindAuthenticatedAccount(old);</code> | 模拟迟到的旧连接退出解绑事件。 |
| 247 | <code>Assert.True(manager.IsCurrentConnection(current));</code> | 在 OldUnbindAndIdleClosureCannotRemoveNewSession 测试中验证：Assert.True(manager.IsCurrentConnection(current))；不满足条件时测试失败。 |
| 248 | <code>Set(manager, "idleTimeout", TimeSpan.FromSeconds(60));</code> | 测试直接设置内部时间/阈值，构造超时或截止情形，避免实际等待一分钟。 |
| 249 | <code>Set(current, "lastValidActivity", Stopwatch.GetTimestamp() - 61 * Stopwatch.Frequency);</code> | 测试直接设置内部时间/阈值，构造超时或截止情形，避免实际等待一分钟。 |
| 250 | <code>Scan(manager);</code> | 触发实际空闲扫描，把最终关闭判断排入通道事件队列。 |
| 251 | <code>current.MarkValidActivity(); // Fresh data wins over a queued timeout decision.</code> | 在扫描后、关闭判断前模拟收到新数据，验证二次判断会保留连接。 |
| 252 | <code>newChannel.RunPendingTasks();</code> | 运行内存通道排队的网络任务，执行最终超时判断。 |
| 253 | <code>Assert.True(newChannel.Active);</code> | 在 OldUnbindAndIdleClosureCannotRemoveNewSession 测试中验证：Assert.True(newChannel.Active)；不满足条件时测试失败。 |
| 254 | <code>Set(current, "lastValidActivity", Stopwatch.GetTimestamp() - 61 * Stopwatch.Frequency);</code> | 测试直接设置内部时间/阈值，构造超时或截止情形，避免实际等待一分钟。 |
| 255 | <code>Scan(manager); newChannel.RunPendingTasks();</code> | 运行内存通道排队的网络任务，执行最终超时判断。 |
| 256 | <code>Assert.False(newChannel.Active);</code> | 在 OldUnbindAndIdleClosureCannotRemoveNewSession 测试中验证：Assert.False(newChannel.Active)；不满足条件时测试失败。 |
| 257 | <code>}</code> | 结束当前代码作用域。 |
| 258 | <code>finally { oldChannel.FinishAndReleaseAll(); newChannel.FinishAndReleaseAll(); }</code> | 结束测试通道并释放缓冲区资源。 |
| 259 | <code>}</code> | 结束当前代码作用域。 |
| 260 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 261 | <code>[Fact]</code> | 标记下面方法为 xUnit 测试。 |
| 262 | <code>public void DelayedOldLogicExitDoesNotClearNewPlayerConnection()</code> | 定义 DelayedOldLogicExitDoesNotClearNewPlayerConnection 测试入口；下面逐行安排、执行和断言此场景。 |
| 263 | <code>{</code> | 开始代码作用域。 |
| 264 | <code>GM_EntityMgr.Instance.Init();</code> | 初始化测试实体管理器。 |
| 265 | <code>var players = (System.Collections.Concurrent.ConcurrentDictionary&lt;long, GM_PlayerEntity&gt;)</code> | 准备将反射读取到的玩家缓存转换为实际的并发字典类型。 |
| 266 | <code>typeof(GM_EntityMgr).GetField("players", BindingFlags.Instance &#124; BindingFlags.NonPublic).GetValue(GM_EntityMgr.Instance);</code> | 使用反射访问内部测试状态或方法：typeof(GM_EntityMgr).GetField("players", BindingFlags.Instance &#124; BindingFlags.NonPublic).GetValue(GM_EntityMgr.Instance)。 |
| 267 | <code>var old = new IdSession { playerId = 201, logicServerId = 1 };</code> | 创建此测试使用的连接会话对象：var old = new IdSession { playerId = 201, logicServerId = 1 }。 |
| 268 | <code>var current = new IdSession { playerId = 201 };</code> | 创建此测试使用的连接会话对象：var current = new IdSession { playerId = 201 }。 |
| 269 | <code>var player = new GM_PlayerEntity(); player.uPlayer.s = current; players[201] = player;</code> | 让缓存玩家指向新连接，以便检验旧连接退出不会误清理。 |
| 270 | <code>var logic = new Game.LogicServer.WorldLogicServer();</code> | 创建真实逻辑服实现，调用原退出入口。 |
| 271 | <code>Assert.Null(logic.DoReqExitLogicServer(old, new ReqExitLogicServer { quitReason = (int)Game.QuitReason.DisconnectQuit }, 201));</code> | 在 DelayedOldLogicExitDoesNotClearNewPlayerConnection 测试中验证：Assert.Null(logic.DoReqExitLogicServer(old, new ReqExitLogicServer { quitReason = (int)Game.QuitReason.DisconnectQuit }, 201))；不满足条件时测试失败。 |
| 272 | <code>Assert.Same(current, player.uPlayer.s);</code> | 在 DelayedOldLogicExitDoesNotClearNewPlayerConnection 测试中验证：Assert.Same(current, player.uPlayer.s)；不满足条件时测试失败。 |
| 273 | <code>}</code> | 结束当前代码作用域。 |
| 274 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 275 | <code>private static void Scan(SessionMgr manager) =&gt; typeof(SessionMgr)</code> | 定义测试辅助方法：private static void Scan(SessionMgr manager) => typeof(SessionMgr)。 |
| 276 | <code>.GetMethod("CheckIdleConnections", BindingFlags.Instance &#124; BindingFlags.NonPublic)</code> | 使用反射访问内部测试状态或方法：.GetMethod("CheckIdleConnections", BindingFlags.Instance &#124; BindingFlags.NonPublic)。 |
| 277 | <code>.Invoke(manager, new object[] { CancellationToken.None });</code> | 用不取消令牌调用实际空闲扫描函数，让内存通道排队最终关闭判断。 |
| 278 | <code>private static int FreePort()</code> | 定义测试辅助方法：private static int FreePort()。 |
| 279 | <code>{</code> | 开始代码作用域。 |
| 280 | <code>var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();</code> | 让系统分配临时 TCP 端口。 |
| 281 | <code>int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;</code> | 取得系统分配的端口并停止探测监听。 |
| 282 | <code>}</code> | 结束当前代码作用域。 |
| 283 | <code>private static SessionInvalidationCommand Command(ReqInvalidateAccountSession request) =&gt; new()</code> | 定义测试辅助方法：private static SessionInvalidationCommand Command(ReqInvalidateAccountSession request) => new()。 |
| 284 | <code>{</code> | 开始代码作用域。 |
| 285 | <code>AccountId = request.accountId, SessionVersion = request.sessionVersion, SessionId = request.sessionId,</code> | 将通知中的账号、会话版本、会话 ID 转为领域命令。 |
| 286 | <code>Timestamp = request.timestamp, Nonce = request.nonce, Signature = Convert.FromBase64String(request.signature)</code> | 把测试收到的 Base64 正文还原为标准业务包。 |
| 287 | <code>};</code> | 结束对象初始化/调用表达式。 |
| 288 | <code>}</code> | 结束当前代码作用域。 |
| 289 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |

## UserLoginMgr.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Auth/UserLoginMgr.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Auth/UserLoginMgr.cs)

只解释本次主动登出修改；不把当前目录迁移、已有心跳/恢复功能和其他预存改动当作本次新增。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 13 | <code>public bool IsLoggingOut { get; private set; }</code> | 新增登出进行中状态，用于拦截连接、登录、心跳和重连回调。 |
| 14 | <code>private CancellationTokenSource sessionCancellation = new CancellationTokenSource();</code> | 创建当前登录生命周期取消源；主动登出取消它。 |
| 15 | <code>public CancellationToken SessionCancellation =&gt; sessionCancellation.Token;</code> | 向网络和选服流程暴露当前生命周期令牌。 |
| 16 | <code>private UniTask&lt;UserLogoutResult&gt; logoutOperation;</code> | 缓存当前登出任务，重复点击复用同一操作。 |
| 17 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 36 | <code>if (IsLoggingOut)</code> | 账号登录入口先检查登出状态。 |
| 37 | <code>return PublishImmediateFailure(UserLoginFailureReason.AlreadyInProgress, "Logout is in progress.");</code> | 通知调用方当前登出正在执行，禁止并行登录。 |
| 38 | <code>using var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, SessionCancellation);</code> | 引入 var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, SessionCancellation) 命名空间，供此文件使用相关类型。 |
| 39 | <code>cancellationToken = sessionScope.Token;</code> | 后续 await 使用组合令牌。 |
| 70 | <code>cancellationToken.ThrowIfCancellationRequested();</code> | 异步结果回来后检查取消，阻止迟到结果恢复旧登录或连接状态。 |
| 108 | <code>if (cancellationToken.IsCancellationRequested)</code> | 如果账号生命周期已经取消，丢弃旧操作的成功结果。 |
| 109 | <code>result = UserLoginResult.Failed(UserLoginFailureReason.Cancelled, "The login was cancelled.");</code> | 将取消的登录结果改成 Cancelled，避免保存成功状态。 |
| 113 | <code>return cancellationToken.IsCancellationRequested ? result : PublishResult(result);</code> | 取消后只返回结果，不广播可能恢复 UI/状态的迟到事件。 |
| 126 | <code>public UniTask&lt;UserLogoutResult&gt; LogoutAsync(CancellationToken cancellationToken = default)</code> | 提供登录 UI、游戏内 UI 均可调用的统一登出入口。 |
| 127 | <code>{</code> | 开始代码作用域。 |
| 128 | <code>if (IsLoggingOut) return logoutOperation;</code> | 已有登出正在执行时返回同一个任务。 |
| 129 | <code>IsLoggingOut = true;</code> | 先标记登出，立刻阻止后续登录和重连动作。 |
| 130 | <code>logoutOperation = LogoutCoreAsync(cancellationToken).Preserve();</code> | 开始异步登出，并用 Preserve 允许同一 UniTask 被多个调用者等待。 |
| 131 | <code>return logoutOperation;</code> | 返回缓存的操作结果任务。 |
| 132 | <code>}</code> | 结束当前代码作用域。 |
| 133 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 134 | <code>private async UniTask&lt;UserLogoutResult&gt; LogoutCoreAsync(CancellationToken cancellationToken)</code> | 具体实现断开、HTTP 登出、本地清理和页面切换。 |
| 135 | <code>{</code> | 开始代码作用域。 |
| 136 | <code>string token = AccessToken;</code> | 先保存凭证，确保后面清空本地状态不丢失 HTTP 所需 token。 |
| 137 | <code>var previousScope = sessionCancellation;</code> | 保存本次要取消的旧登录生命周期对象。 |
| 138 | <code>previousScope.Cancel();</code> | 取消登录、选服、鉴权、连接和恢复等关联任务。 |
| 139 | <code>UniEvent.SendMessage(new UserLogoutStartedEvent());</code> | 发送登出开始事件。 |
| 140 | <code>UserLogoutResult result = new UserLogoutResult(false, 0, "连接已断开，服务端登出未确认");</code> | 默认结果设为服务端未确认，异常时保留此结果。 |
| 141 | <code>try</code> | 开始受异常处理或 finally 清理保护的操作。 |
| 142 | <code>{</code> | 开始代码作用域。 |
| 143 | <code>await NetworkMgr.Singleton.DisconnectAsync();</code> | 先主动断开连接；finally 再做一次兜底清理。 |
| 144 | <code>if (!string.IsNullOrWhiteSpace(token))</code> | 有 token 才请求服务端撤销。 |
| 145 | <code>{</code> | 开始代码作用域。 |
| 146 | <code>using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);</code> | 引入 var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken) 命名空间，供此文件使用相关类型。 |
| 147 | <code>timeout.CancelAfter(TimeSpan.FromSeconds(10));</code> | 登出 HTTP 最多等待十秒，不包含页面加载时间。 |
| 148 | <code>ResUserLogout response = await LoginHttpClient.RequestAsync&lt;ResUserLogout&gt;(</code> | 沿用登录 HTTP 客户端发起 protobuf 请求。 |
| 149 | <code>"/ReqUserLogout", new ReqUserLogout { accessToken = token }, timeout.Token);</code> | 向登录服发送保存的 token，并传入十秒超时令牌。 |
| 150 | <code>bool confirmed = response.status == (int)Respones.OK;</code> | 只有 OK 才认为全部游戏服清理已经确认。 |
| 151 | <code>result = new UserLogoutResult(confirmed, response.status,</code> | 保存完整确认标志及服务端状态码。 |
| 152 | <code>confirmed ? null : "连接已断开，服务端登出未确认");</code> | 确认成功时不提示，其他状态提示服务端登出未确认。 |
| 153 | <code>}</code> | 结束当前代码作用域。 |
| 154 | <code>else result = new UserLogoutResult(true, (int)Respones.OK, null);</code> | 没有本地 token 时只需本地退出，结果按成功处理；并未进行远端确认。 |
| 155 | <code>}</code> | 结束当前代码作用域。 |
| 156 | <code>catch (Exception exception) { Debug.LogWarning($"Logout was not confirmed: {exception.Message}"); }</code> | HTTP 失败或取消时记录日志，继续 finally 本地退出。 |
| 157 | <code>finally</code> | 无论正常返回、取消或异常都执行下面的清理。 |
| 158 | <code>{</code> | 开始代码作用域。 |
| 159 | <code>// Always complete local logout, even if the HTTP request never reached the server.</code> | 注释强调 HTTP 根本未到达服务端也必须清空客户端。 |
| 160 | <code>try { await NetworkMgr.Singleton.DisconnectAsync(); }</code> | 先主动断开连接；finally 再做一次兜底清理。 |
| 161 | <code>catch (Exception exception) { Debug.LogWarning(exception.Message); }</code> | 兜底断开异常仅记录，不阻断账号清理。 |
| 162 | <code>LogoutLocal();</code> | 清空本地当前账号、账号 ID 和 token；不删除记住的账号密码。 |
| 163 | <code>IsLoggingIn = false;</code> | 重置账号登录进行中状态。 |
| 164 | <code>try { await GameMgr.Singleton.ReturnToLoginAsync(result.Error); }</code> | 等待关闭游戏 UI、必要时加载 Start 场景、显示登录页及提示。 |
| 165 | <code>finally</code> | 无论正常返回、取消或异常都执行下面的清理。 |
| 166 | <code>{</code> | 开始代码作用域。 |
| 167 | <code>sessionCancellation = new CancellationTokenSource();</code> | 给下一次登录创建新的生命周期，旧任务仍持有已经取消的旧令牌。 |
| 168 | <code>IsLoggingOut = false;</code> | 页面切换结束或抛出后解除登出状态。 |
| 169 | <code>UniEvent.SendMessage(new UserLogoutCompletedEvent(result));</code> | 发布最终登出结果，供 UI 或其他监听者处理。 |
| 170 | <code>}</code> | 结束当前代码作用域。 |
| 171 | <code>}</code> | 结束当前代码作用域。 |
| 172 | <code>return result;</code> | 返回本次操作结果给调用方。 |
| 173 | <code>}</code> | 结束当前代码作用域。 |
| 174 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 175 | <code>public void LogoutLocal()</code> | 定义 LogoutLocal 测试入口；下面逐行安排、执行和断言此场景。 |
| 176 | <code>{</code> | 开始代码作用域。 |
| 186 | <code>if (IsLoggingOut) throw new OperationCanceledException("Logout is in progress.");</code> | 登出期间禁止发起票据申请或游戏服鉴权。 |
| 187 | <code>using var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, SessionCancellation);</code> | 引入 var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, SessionCancellation) 命名空间，供此文件使用相关类型。 |
| 188 | <code>cancellationToken = sessionScope.Token;</code> | 后续 await 使用组合令牌。 |
| 197 | <code>cancellationToken.ThrowIfCancellationRequested();</code> | 异步结果回来后检查取消，阻止迟到结果恢复旧登录或连接状态。 |
| 212 | <code>if (IsLoggingOut) throw new OperationCanceledException("Logout is in progress.");</code> | 登出期间禁止发起票据申请或游戏服鉴权。 |
| 213 | <code>using var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, SessionCancellation);</code> | 引入 var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, SessionCancellation) 命名空间，供此文件使用相关类型。 |
| 214 | <code>cancellationToken = sessionScope.Token;</code> | 后续 await 使用组合令牌。 |
| 236 | <code>cancellationToken.ThrowIfCancellationRequested();</code> | 异步结果回来后检查取消，阻止迟到结果恢复旧登录或连接状态。 |
| 289 | <code>sessionCancellation.Cancel();</code> | 对象销毁时取消仍关联此登录对象的任务。 |

## UserLoginEvents.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Auth/UserLoginEvents.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Auth/UserLoginEvents.cs)

只解释本次主动登出修改；不把当前目录迁移、已有心跳/恢复功能和其他预存改动当作本次新增。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 69 | <code>public sealed class UserLogoutResult</code> | 新增登出结果类型，独立于登录结果。 |
| 70 | <code>{</code> | 开始代码作用域。 |
| 71 | <code>public bool ServerConfirmed { get; }</code> | 保存是否得到完整服务端确认。 |
| 72 | <code>public int Status { get; }</code> | 保存服务端协议状态码。 |
| 73 | <code>public string Error { get; }</code> | 保存客户端展示的错误或未确认提示。 |
| 74 | <code>public UserLogoutResult(bool confirmed, int status, string error)</code> | 结果构造函数接收确认状态、协议状态与提示。 |
| 75 | <code>{ ServerConfirmed = confirmed; Status = status; Error = error; }</code> | 构造时一次性赋值三个只读属性。 |
| 76 | <code>}</code> | 结束当前代码作用域。 |
| 77 | <code>public sealed class UserLogoutStartedEvent : IEventMessage { }</code> | 定义不带参数的登出开始事件。 |
| 78 | <code>public sealed class UserLogoutCompletedEvent : IEventMessage</code> | 定义登出完成事件，包含成功或未确认结果。 |
| 79 | <code>{</code> | 开始代码作用域。 |
| 80 | <code>public readonly UserLogoutResult Result;</code> | 完成事件携带只读结果引用。 |
| 81 | <code>public UserLogoutCompletedEvent(UserLogoutResult result) =&gt; Result = result;</code> | 构造完成事件并保存结果。 |
| 82 | <code>}</code> | 结束当前代码作用域。 |
| 83 | <code>}</code> | 结束当前代码作用域。 |

## NetworkMgr.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/NetworkMgr.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/NetworkMgr.cs)

只解释本次主动登出修改；不把当前目录迁移、已有心跳/恢复功能和其他预存改动当作本次新增。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 141 | <code>if (UserLoginMgr.Singleton.IsLoggingOut) return false;</code> | 主动登出期间拒绝新建网络连接。 |
| 162 | <code>await ReleaseTransportAsync();</code> | 复用已有传输释放函数：取消订阅、断开并释放对象，避免直接断开残留回调。 |
| 381 | <code>using var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(</code> | 引入 var sessionScope = CancellationTokenSource.CreateLinkedTokenSource( 命名空间，供此文件使用相关类型。 |
| 382 | <code>cancellationToken, UserLoginMgr.Singleton.SessionCancellation);</code> | 网络/选服请求关联账号生命周期，登出可立即取消。 |
| 383 | <code>cancellationToken = sessionScope.Token;</code> | 后续 await 使用组合令牌。 |
| 400 | <code>timeout.Token.ThrowIfCancellationRequested();</code> | 连接返回后检查包含连接超时的完整令牌，不能只检查调用者令牌。 |
| 535 | <code>if (_manualDisconnect &#124;&#124; UserLoginMgr.Singleton.IsLoggingOut) return;</code> | 主动断开或正在登出时忽略断线回调，不启动自动重连。 |
| 572 | <code>if (UserLoginMgr.Singleton.IsLoggingOut &#124;&#124; State != NetworkState.Connected &#124;&#124; !IsGameAuthenticated &#124;&#124; _heartbeatTimedOut) return;</code> | 心跳调度新增登出判断，登出开始后不发新心跳。 |

## ServerSelectMgr.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/ServerSelection/ServerSelectMgr.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/ServerSelection/ServerSelectMgr.cs)

只解释本次主动登出修改；不把当前目录迁移、已有心跳/恢复功能和其他预存改动当作本次新增。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 47 | <code>using var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, UserLoginMgr.Singleton.SessionCancellation);</code> | 引入 var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, UserLoginMgr.Singleton.SessionCancellation) 命名空间，供此文件使用相关类型。 |
| 48 | <code>cancellationToken = sessionScope.Token;</code> | 后续 await 使用组合令牌。 |
| 89 | <code>return cancellationToken.IsCancellationRequested ? result : PublishLoadResult(result);</code> | 取消后只返回结果，不广播可能恢复 UI/状态的迟到事件。 |
| 108 | <code>using var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, UserLoginMgr.Singleton.SessionCancellation);</code> | 引入 var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, UserLoginMgr.Singleton.SessionCancellation) 命名空间，供此文件使用相关类型。 |
| 109 | <code>cancellationToken = sessionScope.Token;</code> | 后续 await 使用组合令牌。 |
| 144 | <code>cancellationToken.ThrowIfCancellationRequested();</code> | 异步结果回来后检查取消，阻止迟到结果恢复旧登录或连接状态。 |
| 162 | <code>return cancellationToken.IsCancellationRequested ? result : PublishConnectResult(result);</code> | 取消后只返回结果，不广播可能恢复 UI/状态的迟到事件。 |
| 169 | <code>using var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(</code> | 引入 var sessionScope = CancellationTokenSource.CreateLinkedTokenSource( 命名空间，供此文件使用相关类型。 |
| 170 | <code>cancellationToken, UserLoginMgr.Singleton.SessionCancellation);</code> | 网络/选服请求关联账号生命周期，登出可立即取消。 |
| 171 | <code>cancellationToken = sessionScope.Token;</code> | 后续 await 使用组合令牌。 |
| 172 | <code>if (UserLoginMgr.Singleton.IsLoggingOut &#124;&#124; IsBusy)</code> | 选服登录入口新增登出状态检查。 |
| 192 | <code>return cancellationToken.IsCancellationRequested ? result : PublishLoginResult(serverIndex, result);</code> | 取消后只返回结果，不广播可能恢复 UI/状态的迟到事件。 |
| 198 | <code>using var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, UserLoginMgr.Singleton.SessionCancellation);</code> | 引入 var sessionScope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, UserLoginMgr.Singleton.SessionCancellation) 命名空间，供此文件使用相关类型。 |
| 199 | <code>cancellationToken = sessionScope.Token;</code> | 后续 await 使用组合令牌。 |
| 239 | <code>if (!authenticated && !cancellationToken.IsCancellationRequested)</code> | 失败清理只对未取消的当前操作执行；旧取消任务不能清空下一次登录。 |

## UIMgr.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/UI/UIMgr.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/UI/UIMgr.cs)

只解释本次主动登出修改；不把当前目录迁移、已有心跳/恢复功能和其他预存改动当作本次新增。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 135 | <code>public void ClearPanelsForLogout()</code> | 新增 UI 管理器登出清理入口。 |
| 136 | <code>{</code> | 开始代码作用域。 |
| 137 | <code>foreach (var panel in panelDict.Values)</code> | 遍历管理器缓存的全部面板。 |
| 138 | <code>if (panel?.GameObject != null) GameObject.Destroy(panel.GameObject);</code> | 销毁存在的面板对象，避免旧游戏界面仍显示。 |
| 139 | <code>panelDict.Clear();</code> | 清空面板缓存，下次 ShowUI 创建新的登录面板。 |
| 140 | <code>}</code> | 结束当前代码作用域。 |

## UIPanel_Login.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/UI/Panel/UIPanel_Login.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/UI/Panel/UIPanel_Login.cs)

只解释本次主动登出修改；不把当前目录迁移、已有心跳/恢复功能和其他预存改动当作本次新增。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 125 | <code>private void LogoutLocal() =&gt; UserLoginMgr.Singleton.LogoutAsync().Forget();</code> | 原按钮回调改为调用完整登出；Forget 用于按钮不能 await 的场景。 |

## GameMgr.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/GameMgr.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/GameMgr.cs)

只解释本次主动登出修改；不把当前目录迁移、已有心跳/恢复功能和其他预存改动当作本次新增。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 256 | <code>public async UniTask ReturnToLoginAsync(string message = null)</code> | 新增可以等待完成的返回登录页入口，可选传入提示。 |
| 257 | <code>{</code> | 开始代码作用域。 |
| 258 | <code>UIMgr.Singleton.ClearPanelsForLogout();</code> | 先销毁旧面板并清除缓存。 |
| 259 | <code>Singleton.SetTimePause(false);</code> | 解除暂停，避免回到登录页面仍受游戏暂停状态影响。 |
| 260 | <code>if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Start")</code> | 仅当当前不是 Start 场景时重新加载入口场景。 |
| 261 | <code>{</code> | 开始代码作用域。 |
| 262 | <code>var loaded = new UniTaskCompletionSource();</code> | 用异步完成源等待现有场景加载回调。 |
| 263 | <code>new EventMessage_GameEnd();</code> | 沿用现有事件类型构造即发送的方式通知游戏结束流程。 |
| 264 | <code>ResourcesMgr.LoadScene("Start", scene =&gt; loaded.TrySetResult());</code> | 调用已有 Start 场景加载，回调时标记等待完成。 |
| 265 | <code>await loaded.Task;</code> | 等待入口场景加载完成，之后再创建登录面板。 |
| 266 | <code>}</code> | 结束当前代码作用域。 |
| 267 | <code>await UIMgr.Singleton.ShowUI(nameof(UIPanel_Login), UIMgr.EPanelLoadMode.Single);</code> | 异步显示登录页面，沿用 Single 加载模式。 |
| 268 | <code>if (!string.IsNullOrWhiteSpace(message))</code> | 只有不为空的提示才显示弹窗。 |
| 269 | <code>{</code> | 开始代码作用域。 |
| 270 | <code>UIMgr.Singleton.ShowMessageBox(new R.UI.Component.MessageBoxData("登出提示", message));</code> | 调用已有消息框，标题为登出提示，正文为未确认说明。 |
| 271 | <code>Debug.LogWarning(message);</code> | 同时写日志，便于检查登出失败情况。 |
| 272 | <code>}</code> | 结束当前代码作用域。 |
| 273 | <code>}</code> | 结束当前代码作用域。 |

## ProtocolConstants.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Protocol/ProtocolConstants.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Protocol/ProtocolConstants.cs)

客户端协议与服务器完全相同；下面仍逐行列出，便于核对字段号和编码类型。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 18 | <code>eUserLogoutReq = 1008,</code> | 定义登出请求命令号 1008；客户端与服务端保持一致。 |
| 19 | <code>eUserLogoutRes = 1009,</code> | 定义登出响应命令号 1009；客户端与服务端保持一致。 |

## AuthMessage.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Messages/AuthMessage.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Messages/AuthMessage.cs)

客户端协议与服务器完全相同；下面仍逐行列出，便于核对字段号和编码类型。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 225 | <code>[ProtoContract]</code> | 声明这是 protobuf 消息类型。 |
| 226 | <code>[MessageMeta((short)Module.AUTH, (short)Cmd.eUserLogoutReq)]</code> | 注册 AUTH/1008 请求类型，消息工厂据此解码。 |
| 227 | <code>public sealed class ReqUserLogout : Message</code> | 新增登出请求消息，继承已有 Message。 |
| 228 | <code>{</code> | 开始代码作用域。 |
| 229 | <code>[ProtoMember(1, IsRequired = true)] public string accessToken;</code> | token 使用 protobuf 字段 1，与客户端一致。 |
| 230 | <code>}</code> | 结束当前代码作用域。 |
| 231 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 232 | <code>[ProtoContract]</code> | 声明这是 protobuf 消息类型。 |
| 233 | <code>[MessageMeta((short)Module.AUTH, (short)Cmd.eUserLogoutRes)]</code> | 注册 AUTH/1009 响应类型。 |
| 234 | <code>public sealed class ResUserLogout : Message</code> | 新增登出响应消息，继承已有 Message。 |
| 235 | <code>{</code> | 开始代码作用域。 |
| 236 | <code>[ProtoMember(1, IsRequired = true)] public int status;</code> | 先声明退出状态，供受保护和原有两个分支赋值。 |
| 237 | <code>[ProtoMember(2)] public string errorMessage;</code> | 可选错误文案使用字段 2。 |
| 238 | <code>}</code> | 结束当前代码作用域。 |

## GoogleProtocolCodec.Generated.cs

文件：[D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Protocol/GoogleProtocolCodec.Generated.cs](D:/iaowu/MiaoWu/Assets/Scripts/HotUpdate/Network/Protocol/GoogleProtocolCodec.Generated.cs)

客户端协议与服务器完全相同；下面仍逐行列出，便于核对字段号和编码类型。

| 当前行 | 代码 | 逐行说明 |
| --- | --- | --- |
| 18 | <code>if (source is ReqUserLogout logoutRequest) WriteReqUserLogout(output, logoutRequest);</code> | 序列化入口新增登出请求类型分支，调用其写入函数。 |
| 19 | <code>else if (source is ResUserLogout logoutResponse) WriteResUserLogout(output, logoutResponse);</code> | 序列化入口新增登出响应类型分支。 |
| 20 | <code>else if (source is ReqUserLogin value0)</code> | 原首个消息分支改为 else if，接在新增登出分支之后。 |
| 126 | <code>if (messageType == typeof(ReqUserLogout)) message = ReadReqUserLogout(input);</code> | 反序列化入口新增登出请求分支。 |
| 127 | <code>else if (messageType == typeof(ResUserLogout)) message = ReadResUserLogout(input);</code> | 反序列化入口新增登出响应分支。 |
| 128 | <code>else if (messageType == typeof(ReqUserLogin)) message = ReadReqUserLogin(input);</code> | 原首个解码分支改为 else if，接在新增登出分支之后。 |
| 1850 | <code>private static void WriteReqUserLogout(CodedOutputStream output, ReqUserLogout message)</code> | 新增请求 protobuf 写入函数。 |
| 1851 | <code>{</code> | 开始代码作用域。 |
| 1852 | <code>if (message.accessToken != null)</code> | token 非 null 时才写入或计算长度。 |
| 1853 | <code>{</code> | 开始代码作用域。 |
| 1854 | <code>output.WriteTag(1, WireFormat.WireType.LengthDelimited);</code> | 写字段 1 的长度分隔类型标签，后续数据是 token 字符串。 |
| 1855 | <code>output.WriteString(message.accessToken);</code> | 写入 token 字符串。 |
| 1856 | <code>}</code> | 结束当前代码作用域。 |
| 1857 | <code>}</code> | 结束当前代码作用域。 |
| 1858 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 1859 | <code>private static ReqUserLogout ReadReqUserLogout(CodedInputStream input)</code> | 新增请求 protobuf 读取函数。 |
| 1860 | <code>{</code> | 开始代码作用域。 |
| 1861 | <code>var message = new ReqUserLogout();</code> | 创建待填充的请求对象。 |
| 1862 | <code>uint tag;</code> | 保存当前 protobuf 标签。 |
| 1863 | <code>while ((tag = input.ReadTag()) != 0)</code> | 逐字段读取，标签为零表示消息结束。 |
| 1864 | <code>{</code> | 开始代码作用域。 |
| 1865 | <code>switch (WireFormat.GetTagFieldNumber(tag))</code> | 根据字段号选择对应数据读取方式。 |
| 1866 | <code>{</code> | 开始代码作用域。 |
| 1867 | <code>case 1:</code> | 处理字段 1：请求中是 token，响应中是 status。 |
| 1868 | <code>message.accessToken = input.ReadString();</code> | 读取字段 1 字符串，写到请求 token。 |
| 1869 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 1870 | <code>default:</code> | 遇到未定义的 protobuf 字段时走兼容分支。 |
| 1871 | <code>input.SkipLastField();</code> | 跳过未知字段，保持 protobuf 向前兼容。 |
| 1872 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 1873 | <code>}</code> | 结束当前代码作用域。 |
| 1874 | <code>}</code> | 结束当前代码作用域。 |
| 1875 | <code>return message;</code> | 返回已经读取的消息对象。 |
| 1876 | <code>}</code> | 结束当前代码作用域。 |
| 1877 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 1878 | <code>private static int ComputeSizeReqUserLogout(ReqUserLogout message)</code> | 新增请求编码长度辅助函数；当前顶层分支直接使用 Write/Read。 |
| 1879 | <code>{</code> | 开始代码作用域。 |
| 1880 | <code>if (message == null) return 0;</code> | 空消息的编码长度为零。 |
| 1881 | <code>int size = 0;</code> | 初始化长度累加器。 |
| 1882 | <code>if (message.accessToken != null)</code> | token 非 null 时才写入或计算长度。 |
| 1883 | <code>size += CodedOutputStream.ComputeTagSize(1) + CodedOutputStream.ComputeStringSize(message.accessToken);</code> | 累加 token 字段标签和字符串编码长度。 |
| 1884 | <code>return size;</code> | 返回累积的 protobuf 正文长度。 |
| 1885 | <code>}</code> | 结束当前代码作用域。 |
| 1886 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 1887 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 1888 | <code>private static void WriteResUserLogout(CodedOutputStream output, ResUserLogout message)</code> | 新增响应 protobuf 写入函数。 |
| 1889 | <code>{</code> | 开始代码作用域。 |
| 1890 | <code>output.WriteTag(1, WireFormat.WireType.Varint);</code> | 响应字段 1 的 status 使用 Varint 标签。 |
| 1891 | <code>output.WriteInt32(message.status);</code> | 写入整数状态码，即使值为零也写入。 |
| 1892 | <code>if (message.errorMessage != null)</code> | 错误文案非 null 时写入或计算长度。 |
| 1893 | <code>{</code> | 开始代码作用域。 |
| 1894 | <code>output.WriteTag(2, WireFormat.WireType.LengthDelimited);</code> | 响应字段 2 使用长度分隔标签。 |
| 1895 | <code>output.WriteString(message.errorMessage);</code> | 写入错误文案。 |
| 1896 | <code>}</code> | 结束当前代码作用域。 |
| 1897 | <code>}</code> | 结束当前代码作用域。 |
| 1898 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 1899 | <code>private static ResUserLogout ReadResUserLogout(CodedInputStream input)</code> | 新增响应 protobuf 读取函数。 |
| 1900 | <code>{</code> | 开始代码作用域。 |
| 1901 | <code>var message = new ResUserLogout();</code> | 创建待填充的响应对象。 |
| 1902 | <code>uint tag;</code> | 保存当前 protobuf 标签。 |
| 1903 | <code>while ((tag = input.ReadTag()) != 0)</code> | 逐字段读取，标签为零表示消息结束。 |
| 1904 | <code>{</code> | 开始代码作用域。 |
| 1905 | <code>switch (WireFormat.GetTagFieldNumber(tag))</code> | 根据字段号选择对应数据读取方式。 |
| 1906 | <code>{</code> | 开始代码作用域。 |
| 1907 | <code>case 1:</code> | 处理字段 1：请求中是 token，响应中是 status。 |
| 1908 | <code>message.status = input.ReadInt32();</code> | 读取整数状态码。 |
| 1909 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 1910 | <code>case 2:</code> | 处理响应的字段 2 错误文案。 |
| 1911 | <code>message.errorMessage = input.ReadString();</code> | 读取错误文案字符串。 |
| 1912 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 1913 | <code>default:</code> | 遇到未定义的 protobuf 字段时走兼容分支。 |
| 1914 | <code>input.SkipLastField();</code> | 跳过未知字段，保持 protobuf 向前兼容。 |
| 1915 | <code>break;</code> | 结束当前 switch 分支，不落入其他字段处理。 |
| 1916 | <code>}</code> | 结束当前代码作用域。 |
| 1917 | <code>}</code> | 结束当前代码作用域。 |
| 1918 | <code>return message;</code> | 返回已经读取的消息对象。 |
| 1919 | <code>}</code> | 结束当前代码作用域。 |
| 1920 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 1921 | <code>private static int ComputeSizeResUserLogout(ResUserLogout message)</code> | 新增响应编码长度辅助函数；不会发送额外协议数据。 |
| 1922 | <code>{</code> | 开始代码作用域。 |
| 1923 | <code>if (message == null) return 0;</code> | 空消息的编码长度为零。 |
| 1924 | <code>int size = 0;</code> | 初始化长度累加器。 |
| 1925 | <code>size += CodedOutputStream.ComputeTagSize(1) + CodedOutputStream.ComputeInt32Size(message.status);</code> | 累加状态标签与整数编码长度。 |
| 1926 | <code>if (message.errorMessage != null)</code> | 错误文案非 null 时写入或计算长度。 |
| 1927 | <code>size += CodedOutputStream.ComputeTagSize(2) + CodedOutputStream.ComputeStringSize(message.errorMessage);</code> | 累加错误文案标签与字符串编码长度。 |
| 1928 | <code>return size;</code> | 返回累积的 protobuf 正文长度。 |
| 1929 | <code>}</code> | 结束当前代码作用域。 |
| 1930 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |
| 1931 | <code>（空行）</code> | 空行，仅用于分隔代码，不改变执行行为。 |

## TcpLogout.md 说明文档

[TcpLogout.md](D:/BaiduNetdiskDownload/CSharpGame/CSharpGameServer/docs/TcpLogout.md) 是新增的调用与验收说明，不参与编译或运行。它依次记录客户端入口、HTTP 字段和状态、签名通知、超时/重试规则、内存任务限制以及测试和 Unity 验收步骤。
