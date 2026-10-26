# 红点初始化只读调整

初始化只返回配置和已有状态；缺少记录按隐藏处理。协议及数据库表结构不变，邮件未接入。

| 文件 | 本次修改 |
|---|---|
| `GameServer/src/Game/Core/GM_RedDot/GM_RedDotMgr.cs` | 删除初始化事务、player 行锁、业务计算和 upsert。按加锁入口 → Locked 编排 → 缓存优先读取组织查询。业务事件分别使用 RefreshBusinessState / WriteViewedState，提交后推送。 |
| `GameServer/src/Game/Datas/GMEntities/Components/RedDotComponent.cs` | 保存已加载配置版本，防止节点配置变化后继续使用旧过滤结果。 |
| `GameServerTests/RedDot/RedDotMySqlTests.cs` | 验证空表不插入、现有记录不更新、初始化不计算业务、配置升级不点亮，以及显式业务触发、事务回滚和查看记录。 |
| `docs/RedDotSystem.md` | 更新只读初始化及业务触发接入说明。 |
| `docs/RedDotChanges.md` / `docs/RedDotSystem.patch` | 更新整个功能的最终文件清单及实际差异。 |
| 本文件 / `docs/RedDotReadonlyInit.patch` | 本次说明和修改前后完整差异。 |

验证：服务端构建通过；红点相关 7 项测试（含隔离 MySQL）通过；Game 角色启动、真实 TCP 鉴权及初始化接口验证通过。临时数据库已删除，没有修改业务数据库。客户端和邮件代码未改动，本次未重跑无关全量测试；此前已知测试限制见 RedDotSystem.md。

临时验证配置、日志及修改前快照位于 `C:/Users/Windows/AppData/Local/Temp/codex-reddot-20261023/`，含连接信息的临时 smoke.config 在测试后清理。编译器照常更新 bin/obj 构建产物。
