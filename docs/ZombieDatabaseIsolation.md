# 僵尸项目独立数据库

本项目的默认 MySQL 库已调整为：

| 连接配置 | 数据库 | 内容 |
| --- | --- | --- |
| `connAuthUserStr` | `zombie_auth` | 独立注册账号 |
| `connGameDataStr` | `zombie_game` | 独立角色及游戏数据 |

MySQL 地址及登录凭据沿用本项目 `GameServer/App.config` 的连接设置。只复用 MySQL 服务，不复用原数据库。没有迁移原账号或角色；原 `auth_10001`、`ugame_10001` 保持不变。

## 初始化与检查

在本仓库根目录使用 PowerShell 7：

```powershell
pwsh -NoProfile -File tools/Initialize-ZombieDatabases.ps1
pwsh -NoProfile -File tools/Initialize-ZombieDatabases.ps1 -VerifyOnly
```

初始化使用 `sql/zombie_init.sql`，创建账号库 1 张表、游戏库 16 张表，不导入账号和游戏记录。脚本只接受这两个目标库名，不会执行原库初始化文件。再次执行不会清空数据。没有安装默认路径的 MySQL 客户端时，通过 `-MySqlPath` 指定 `mysql.exe`。

历史 `sql/ugame_10001.sql` 及其迁移保留作结构参考；僵尸项目新环境请使用上述独立初始化入口。

## 本机独立服务

```powershell
pwsh -NoProfile -File tools/Start-ZombieServers.ps1
```

脚本编译并为 `ServerZone`、`Login`、`Game` 各准备一份运行目录及配置，位于 `GameServer/bin/ZombieLocal`。窗口保持运行即可；按 Ctrl+C 结束时会关闭由此脚本启动的服务。已有端口占用时会报错，不会结束其他项目的服务。停止本项目三个本地角色也可以执行：

```powershell
pwsh -NoProfile -File tools/Start-ZombieServers.ps1 -Stop
```

`-PrepareOnly` 只编译和准备配置；`-SkipBuild` 复用已编译的本地版本。`processes.json` 记录运行进程，日志在各角色的 `Logs` 目录。

当前本机入口：

- 登录服：`http://192.168.1.5:16091`
- 区服列表：`http://192.168.1.5:16090/PullingServerZone`
- 僵尸游戏服：区服 ID `4`，TCP `192.168.1.5:7080`，HTTP `6090`

区服列表只发布本项目的区服 4。登录服和游戏服使用本项目独立的票据签名配置，不接受共享登录服签发的票据。

TowerDefence 的 `Assets/Resources/ServerIntegrationConfig.json` 已指向以上独立服务。Unity 停止播放后重新运行使配置生效，使用新注册账号登录。原账号不会自动带入新库。

## 本次验证

- 服务端编译：0 错误，1 项已有依赖警告。
- 数据库表名映射测试：5/5 通过。
- 使用真实客户端协议编解码及 TCP 基础层，完成注册、登录、区服列表、票据、游戏服鉴权、心跳、登出。
- 新账号在 `zombie_auth`，鉴权创建的角色在 `zombie_game`；原账号库不存在该联调账号。
- 联调账号及其角色、初始道具、任务、邮件均已定向清理，最终两个新库无业务记录。
- 联调前后原账号库和游戏库各表校验和一致。
- 本次没有进行 Unity 完整主场景的画面和业务流程验证；新数据库不代表客户端游戏存档已完成服务端对接。

完整差异、文件清单和联调记录位于客户端 `D:/apk/TowerDefence/Docs/ZombieDatabaseIsolation/`。
