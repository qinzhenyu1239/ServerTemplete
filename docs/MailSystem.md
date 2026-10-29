# 邮件系统接入与验证

服务端调用链为 `MailController → MailModule → GM_MailMsgMgr → MySQL / GM_ItemMgr`。客户端位于 `D:/iaowu/MiaoWu/Assets/Scripts`。本次交付网络与可绑定 UI 模板，没有创建 prefab、场景、运营页面。邮件不访问 `playerreddot`，入口提示仅使用邮件未读数。登录/重连读取摘要，打开邮件界面或点击刷新时读取列表；阅读、领取、删除使用本次操作响应更新本地状态。后台新邮件不立即更新客户端红点。

## 上线配置和数据库

1. 先备份目标库，在维护窗口选择目标游戏库，执行 `sql/migrations/20261026_001_mail.sql`。该脚本不切换数据库，支持重复执行；MySQL DDL 会隐式提交，不能用事务回滚整个迁移。
2. 新建库使用已同步的 `sql/ugame_10001.sql`。不要对已有库用整份建库 SQL 代替迁移。
3. 在游戏服实际运行的配置文件 `appSettings` 中配置 `MailboxCapacity` 正整数。当前按确认的默认容量配置为 100 封；配置缺失或非法时游戏服启动失败。临时协议验证使用容量 60，不是默认配置。
4. 所有发送调用必须传入正数 `LifetimeSeconds`。没有默认有效期。普通邮件不能带附件，资源邮件必须带有效附件，数量必须为正数且物品配置存在。

`mailmessage` 保存收发玩家、发送时标题/正文、类型、来源、发送/到期/阅读/领取/删除时间及删除原因。`mailattachment` 按 `(mailId, sortOrder)` 保存实际附件快照，数量为 bigint。时间统一 Unix 秒。正文与附件不进入在线组件。

旧数据保留 ID、正文、收发玩家、发送时间，标题补为“历史邮件”，类型为普通邮件、到期时间为 0、附件为空。旧 status=1 优先保留有效 readTime，缺失时用发送时间兜底；旧负数 readTime 归零。status=2 补逻辑删除，无法知道的删除时间取迁移时刻，表示“迁移时确认已删除”，不是原始删除时刻。旧 status/userData 列保留，但新业务不读写它们。

发送来源唯一索引为 `(toPlayerId, sourceType, sourceKey)`。不去重时用 NULL；有去重需求时传稳定的业务标识，相同来源、相同内容返回原邮件 ID，不重发；内容不同返回 SourceConflict。来源标识区分大小写，删除的邮件也保留来源标识防止重发。

## 服务端发送示例

```csharp
var result = MailModule.Instance.SendMail(new MailSendCommand
{
    PlayerId = playerId,
    SenderId = -1,
    MailType = 1,
    Title = "任务奖励",
    Body = "奖励已发放至附件。<link=task>前往任务</link>",
    LifetimeSeconds = configuredLifetimeSeconds, // 发送方明确提供
    SourceType = 1, // 各业务统一约定来源编号
    SourceKey = $"quest:{questId}:reward:{rewardRevision}",
    Rewards = new[] { new MailReward { ItemId = 10001, Quantity = 3 } }
});
if (result.Status != MailStatus.Success)
{
    // 记录并按确定的失败类型处理；SourceConflict 不能当作成功重试。
}
```

示例所需命名空间：`Game.Core.GM_MailMessage` 和 `Game.Entries.Modules`。普通邮件设置 MailType=0，Rewards 留空。该入口支持离线收件；普通玩家没有发信网络接口。

## 行为和一致性

- 所有事务内邮件修改取得玩家操作锁与玩家数据库行锁；附件发奖还锁定道具行。道具方法接收同一个 SqlSugarClient，发奖与 claimTime 原子提交。提交后才同步已加载的背包组件和连接摘要版本；通过本次请求响应返回状态，不主动推送邮件变化。
- 单封资源邮件整体领取。重复领取已领取邮件返回成功但不再次入账。校验失败不发任何附件；没有背包容量限制，本期只校验配置、正数、long 溢出。
- 一键领取固定开始时的有效邮件集合，先将全部标记已读，然后校验全部待领取奖励。已知业务校验失败只提交已读，整批不发奖；没有奖励也提交全部已读并返回 NoRewards。数据库异常回滚整次事务，包括已读修改。
- 全部已读包含普通邮件和已领取附件但仍未读的历史邮件，不受响应只附带前 10 条摘要的限制；已删除、已到期及其他玩家的邮件不在范围内。没有奖励时客户端仍应用已读状态并提示“邮件已全部标为已读，暂无可领取奖励”。
- 排序为未读、已读未领取资源邮件、其他已读；每组 sendTime DESC、id DESC。新邮件和打开列表时执行容量清理，从排序底部删除多余邮件，允许删除未领取附件。
- 逻辑有效性始终排除已删除、已到期邮件，不依赖定时器。每秒扫描最多 100 位到期收件玩家，分批补记删除并更新在线摘要版本；客户端下次查询时获取变化。删除不回收已入账奖励。
- 在线组件只保存 Session、Version、Count、UnreadCount；连接变化即重新开始版本。状态只通过请求响应返回，登录和重连从服务端重新刷新。

## 协议

命令均属于 PLAYER(102)，请求/响应为 110/111 摘要、112/113 列表、114/115 详情并阅读、116/117 单封领取、118/119 一键领取、120/121 删除单封、122/123 删除已读。124 号邮件变化推送已移除，编号保留不用。旧 21～24 转入同一个 MailModule，只有阅读和合法删除可用，不允许改回未读或跨玩家操作。旧接口不提供附件领取能力，应迁移至新接口；旧邮件 HTTP 路径没有当前游戏连接校验条件时会拒绝访问。

每个响应有 status、mailboxVersion、serverTime、总数/未读/容量、邮件摘要集合、可选详情和变更物品数量。列表不返回正文与附件，详情单独返回。新列表 offset 从 0 开始，pageSize 默认 50、最大 50；hasMore/nextOffset 表示继续取页。分页期间版本变化，客户端重新读取列表，最多重取三轮；不拼接不同版本的数据。

为适配现有 TCP 16 位帧长度，新邮件正文限制为 UTF-8 49152 字节、最多 256 项附件；这是传输长度限制，与背包持有量无关。标题最多 128 字符。详情及写操作最多附带 10 条列表摘要；hasMore 时客户端只补读列表，不重放写操作。旧协议无分页能力，继续保留历史全量格式，仅用于兼容。

错误码：-140 邮件不存在或不可见，-141 删除条件不满足，-142 没有可领取奖励，-143 奖励配置/数量异常，-144 来源冲突；登录、参数和系统异常沿用项目错误码。

## 客户端与 UI 绑定

`MailNetwork` 处理请求、超时和列表分页；`MailManager` 串行协调网络、重连、版本和事件；`MailboxStateManager` 处理显示快照，Models 只含数据。所有写请求禁止自动重试。领取结果不明时只刷新邮箱与背包，禁止再次自动发起领取。

`MailManager.Singleton.LoadListAsync(true)` 打开邮箱，`CloseView()` 关闭；LoadSummaryAsync/ReadAsync/ClaimAsync/ClaimAllAsync/DeleteAsync/DeleteReadAsync 对应接口。订阅 `MailUpdatedEvent`，Result.Operation 区分 Busy、Idle、Notice、Expiry、Reset 和业务结果。读取 `MailManager.Singleton.State.Summary.UnreadCount` 绑定入口；`UIMailUnreadBadge` 提供现成亮灭组件，不接入通用红点节点。

UI 按 UIGacha 的 Awake 绑定按钮、OnEnable 订阅、OnDisable 退订、OnSHow 首查的结构提供：

| 脚本 | Inspector / 接入点 |
|---|---|
| UIMail | 绑定关闭、刷新、领取、一键领取、删除、删除已读按钮；列表槽位、附件槽位；标题/正文/时间/计数/状态文本；详情/奖励面板；确认面板和确认/取消按钮 |
| UIMailRow | 绑定选择按钮、标题、倒计时、图标、四种信件/礼物 Sprite、新标识、资源标识、选中背景和 CanvasGroup |
| UIMailAttachment | 绑定物品 Image、数量文本、点击按钮、领取置灰 CanvasGroup。领取后仍可点击物品详情 |
| UIMailLinkHandler | 挂到正文 TMP 对象并绑定正文文本；事件相机由 PointerEventData 提供，Canvas/EventSystem 需正确配置；链接点击交给 UIMail.LinkRequested |
| UIMailUnreadBadge | 绑定入口红点 GameObject；监听邮件状态事件，未读数大于零时显示 |

正文放入纵向 ScrollRect，TMP 自动换行与父级 RectMask2D 负责边界；奖励容器放入横向 ScrollRect。模板本身不创建这些对象。文本使用 TMP `<link=目标>文案</link>`，高亮由模板处理，LinkRequested 接现有导航。ItemDetailsRequested 接物品介绍/获取途径，AttachmentIconRequested 接现有图标加载器。

固定槽位数组仅用于轻量绑定。生产邮箱列表应通过 RowsChanged 绑定完整集合与 Select(mailId)，附件通过 AttachmentsChanged 绑定完整有序集合；自行接已有列表/虚拟列表容器，不能把固定槽位数量当作邮箱容量。模板不自动实例化节点。请在启用/打开界面前绑定回调，并由 IUIPanel/UIMgr 调用 OnSHow。

打开时固定排序与置灰快照；单封读信、领附件更新图标、新标识和删除按钮，保持本次顺序及原置灰结果。一键领取响应返回后立即按最新已读/已领取状态刷新灰显，包括之前看过的邮件，列表顺序不变；仍未领取成功的奖励和操作后新到的未读邮件不误置灰。关闭后再打开按最新状态重建。到期本地移除并等待服务端确认。操作期间禁用按钮，删除缺少确认面板绑定时不会执行，确认文案按需求提供。“7 日内不再提示”不在本期范围。

## 验证记录（2026-10-28，移除 124）

- 服务端与客户端 HotUpdate 编译通过；邮件服务端规则/数据库测试 14 项、客户端状态测试 9 项通过。
- 隔离 Game 角色通过真实 TCP 验证：在线发新邮件不推送，手动摘要/列表能获取；阅读、单封领取、删除、一键领取和过期清理正常，整个流程未收到 124。
- 原网络测试工程仍缺少 7 个已有源文件。未执行 Unity 场景/真机交互验证。本次未修改数据库结构或运行中的游戏服。

## 历史验证记录（2026-10-26）

- .NET 8 服务端构建、客户端 HotUpdate 业务程序集构建通过；保留项目既有警告。
- 邮件服务端测试 9 项通过：排序、有效期校验、鉴权、Google 编解码、重复迁移、旧邮件状态、重复来源、附件顺序、并发领取、归属、整批失败全读、溢出、事务回滚、提交后组件、容量/过期和旧接口适配。
- 客户端可独立运行的 Mail.Tests 覆盖冻结展示、旧版本响应抑制、摘要/列表响应更新、领取详情、到期、重连重置、删除条件、数据模型边界。
- 随机命名临时 MySQL 库启动真实 Game 角色：TCP 验证鉴权、摘要、收信、详情、重复领取、删除、52 封分页、无奖励一键领取仍全读、旧 21～24；WebSocket 验证鉴权、摘要、整批领取和删除已读。验证完毕关闭游戏服并删除临时库及包含凭据的临时配置。没有迁移现有业务库。
- 完整非数据库测试：97 通过、1 失败、1 跳过。失败为已有 PlayerProfileUpdateTests.BothCodecsPreserveNullableZeroJsonStringsAndLongIds；修改前的生成器已不支持该可空字段契约，本次未修复该独立问题。
- 原 MiaoWu.NetworkAuth.Tests 工程编译失败：7 个 Assets/Tests/Editor 源文件缺失。dotnet test 对该旧式工程不执行实际测试，因此另行执行 build 并记录失败，没有用它声称测试通过。
- 未执行 Unity 场景/真机视觉验收（本期没有 prefab）；模板已编译，需在实际 UI 绑定后验收滚动、字体、图标与导航。

完整文件清单、验证文件位置见 `MailChanges.md`；真实本次差异见 `MailSystem.patch`，基于开始工作时的文件快照生成，保留用户原有修改。
