# 僵尸项目玩家基础信息

## 数据与协议

服务端为本仓库，客户端为 D:/apk/TowerDefence。player 表保留 id、accountId、name、avatarId、createTime、status，新增四项进度：baseLevel（基地等级，默认1）、mainStage（主线章节编号，默认1）、resonanceLevel（共鸣等级，默认0）、towerStage（爬塔关卡，默认0），共10列。
查询保持 PLAYER=102、请求91／响应92，空请求查询当前已鉴权会话角色。响应 status 表示接口结果，不是 player.status。
原响应字段1～13及类型、编号不变；playerId/name/avatarId/createTime 为真实值，guildId/titleId/avatarFrameId/backgroundId 为0，signature 为空字符串，displayAchievementIds 为 [0,0,0,0,0,0,0]，displayCharacterIds 为 [0,0,0,0,0,0]。
查询92与修改94响应末尾追加 int 字段：14 baseLevel、15 mainStage、16 resonanceLevel、17 towerStage；请求和消息号不变，旧客户端可跳过新字段。
四项数据本次仅服务端存储和资料展示，不上传或覆盖客户端本地存档，资料修改请求不开放修改这些进度。
修改仍为93／94，仅接受 updateFields=[Avatar] 且 avatarId>=0，混入已移除字段整体拒绝，响应布局与查询一致。

读取仍为 PlayerController → PlayerModule → GM_PlayerProfileMgr → 在线组件 → 账号角色缓存 → MySQL。
头像写入在 Manager 内加玩家锁、核对账号归属、事务提交后更新共享缓存。创建和选择角色按账号关联，旧职业及性别参数不再落库或影响选择。
旧 PlayerInfo 保留布局，移除属性的数值返回0。旧签到领奖、奖励领取、元宝兑换、金币排行查询和等级/金币入场条件返回 InvalidOpt；不扣款、不发奖、不改变领取状态。独立道具与邮件资产不受影响。

## 客户端事件模板

MainInterface 的“用户信息”按钮通过 UIMgr 打开 UIPlayerBasicInfo。
UI 发布 PlayerBasicInfoRequestedEvent，Manager 监听并调用 PlayerBasicInfoNetwork，再通过 SessionNetwork 发包；Manager 发布 Loading/Loaded/Failed/Cleared 事件，UI 订阅更新显示。
UI 不直接调用 Manager 业务方法或 Network；Manager 不持有界面引用。

请求及单次结果携带 Guid 请求标识，请求还携带取消令牌。Guid.Empty 的清空事件表示会话整体失效。
Manager 保存只读资料快照并拒绝重复查询；UI 仅保存当前请求标识、取消源和加载状态。Network 超时10秒。
UI 打开自动查询，支持刷新和关闭。隐藏取消本次请求；注销、断线和连接代次变化清空缓存。过期结果不会更新新会话。
显示玩家ID、当前会话账号ID、名称、头像ID、本地创建时间、基地等级、主线关卡、共鸣等级和爬塔关卡；无效时间显示“未知”。本次不提供头像编辑界面。

项目技能见 ../.codex/skills/zombie-ui-network-template/SKILL.md，固定 UI → Manager → Network 与 UI／Manager 双向事件交互两项规范。

## 登录与重连恢复

PlayerBasicInfoRecoveryHandler 实现 IReconnectRecoveryHandler，由现有注册表自动发现，在 Refresh 阶段按 SessionSyncOrder.PlayerProfile 顺序执行。首次游戏服鉴权和每次重连鉴权成功后，协调器都会调用 Manager.RecoverAsync；不是通过 NetworkConnected 再追加查询。
恢复查询复用91／92和原Network层，成功更新Manager快照并发布带 IsSessionRefresh=true 的加载／成功事件，所有已打开面板都可接收；普通手动查询继续按请求编号匹配。
恢复期间打开或刷新面板不会重复发包。面板关闭仅取消它自己的手动查询，不取消账号级恢复；即使面板没有打开，恢复仍会更新资料快照。
关联恢复、账号和连接取消令牌，写入快照及发事件前校验当前上下文。断线／注销会清空资料，旧连接的迟到响应不能覆盖新会话。恢复查询错误和超时向协调器传播，不能在缺少资料时报告Online；自动重连沿用现有重试策略。

## 数据库更新与导表

新环境使用 sql/zombie_init.sql。现有本机环境执行 tools/Update-ZombiePlayer.ps1：仅停止本项目服务，备份 player 后依次应用精简迁移和新增进度迁移（20261112_002_zombie_player_progress.sql），整组重复执行两次，核对保留字段与最终列清单。备份默认保存在本机临时目录。
迁移包含删列，MySQL DDL 自动提交。失败时停止后续操作，使用输出的完整备份恢复，不重建账号或其他业务表。
导表继续使用 tools/GenDbClass 原有的统一生成流程。Program.cs 保持本次任务开始前的内容，不增加单表生成参数或针对 Player 的特殊处理。本次已生成的精简 Player 实体保留。

完成后用 tools/Start-ZombieServers.ps1 启动本项目服务。历史原项目 SQL 和原数据库不改。

## 验证

服务端 PlayerProfile 测试覆盖协议、缓存顺序、兼容值、十字段实体、停用入口、头像更新归属及重复更新；数据库测试沿用 GACHA_RUN_DATABASE_TESTS / GACHA_DB_CONFIGURATION_PATH / GACHA_TEST_PLAYER_ID。
客户端 Tools/PlayerBasicInfoVerification/Run-Verification.ps1 在隔离 Unity 工程生成预制体，-Smoke 使用真实本机服务与受控协议端点验证事件链、取消、超时、断线及重新登录。它不会改变主客户端服务器地址；游戏主场景仍使用用户自己的配置。
精简资料的历史差异见 ZombiePlayerBasicInfoChanges.md；本次新增四项进度的差异与清单见 ZombiePlayerProgressChanges.md 和 ZombiePlayerProgress.patch。
