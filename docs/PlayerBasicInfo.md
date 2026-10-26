# 玩家基础信息

## 消息与服务端读取

游戏服消息 `Module.PLAYER = 102`：请求 `ReqGetPlayerBasicInfo = 91`，响应 `ResGetPlayerBasicInfo = 92`。
空请求体，不接受客户端指定玩家ID；查询当前已鉴权连接绑定的角色。

响应字段：1 status、2 errorMessage、3 playerId(long)、4 avatarId(int)、5 name(string)、
6 guildId(long)、7 createTime(long，Unix秒)、8 titleId(int)、9 signature(string)。

调用链：PlayerController → PlayerModule → GM_PlayerProfileMgr。
读取顺序：在线玩家组件 → GM_AccountEntityMgr账号角色缓存 → MySQL player表。
查库结果回填账号角色缓存；在线组件缺失时补入同一个Player对象。接口不会创建角色。
返回已有状态码OK、AccountIsNotLogin、InvalidParams、PlayerIsNotExist或SystemErr。
先确认player表已有avatarId、guildId、titleId、signature、createTime五列。

## MiaoWu客户端

客户端根目录：`D:\iaowu\MiaoWu`。

`PlayerBasicInfoNetwork.LoadAsync()`使用SessionNetwork，10秒超时，拒绝重复操作。
`PlayerBasicInfoManager.Singleton.LoadAsync(token)`更新只读CurrentInfo快照并发布
PlayerBasicInfoLoadedEvent / PlayerBasicInfoFailedEvent。注销清空快照并发布PlayerBasicInfoClearedEvent。
调用方取消或登录会话取消后，响应不能再更新快照或发布过时结果。

## Unity面板绑定

脚本：`Assets/Scripts/HotUpdate/UI/PlayerProfile/UIPlayerBasicInfo.cs`。
预制体：`Assets/ResOld/Prefabs/UI/Panel/PlayerBasicInfo/UIPlayerBasicInfo.prefab`。
已挂载UIPlayerBasicInfo，绑定两个按钮和八个文本；无需手动补引用。
使用`Assets/Res/Art/UI/UI_Info_Assets`背景、猫咪、面板、按钮及徽章切图，
以及现有荆南缘默体字体。头像图案和徽章属于静态装饰，动态资料仍由七个文本展示。
签名区域支持纵向滚动；布局按项目现有UIRoot的1200参考高度制作，适配横屏16:9及更宽比例。
现有YooAsset的UI目录采集规则已覆盖新预制体，可按现有资源流程重新采集/构建后打开：

```csharp
await R.UI.UIMgr.Singleton.ShowUI(nameof(R.UI.UIPlayerBasicInfo));
```

Start场景的MainInterface下已添加PlayerBasicInfoButton（玩家资料），点击调用UIMainInterface.OpenPlayerBasicInfo()。
打开面板时UIMgr会调用IUIPanel.OnSHow()自动获取资料。

Inspector引用已绑定，以下为节点用途：

| 字段 | 组件/用途 |
| --- | --- |
| closeButton | Button，关闭面板 |
| refreshButton | Button，重新查询 |
| playerIdText | UnityEngine.UI.Text，玩家ID |
| avatarIdText | Text，头像ID |
| nameText | Text，角色名称 |
| guildIdText | Text，公会ID；0显示未加入公会 |
| createTimeText | Text，本地时间；0或越界显示未知 |
| titleIdText | Text，称号ID；0显示未佩戴称号 |
| signatureText | Text，空白显示暂无签名 |
| statusText | Text，请求/失败/注销状态 |

打开自动查询；刷新中禁用刷新按钮；隐藏面板会取消它发起的请求。
头像和称号仅展示ID，不加载图片、名称配置或公会详情。

## 验证

服务端：PlayerBasicInfoTests覆盖缓存顺序、回填、快照、鉴权、协议与数据库异常。
数据库测试沿用GACHA_RUN_DATABASE_TESTS/GACHA_DB_CONFIGURATION_PATH/GACHA_TEST_PLAYER_ID开关，
只插入并清理自己生成的测试角色记录。
客户端：Assets/Tests/Editor/PlayerBasicInfoTests.cs，Unity EditMode运行，覆盖协议、重复请求、
取消、超时、断线、注销清空和延迟响应。


## 可编辑资料（2026-10-22）

查询仍为 PLAYER=102、请求91／响应92。原字段1～9保持不变，新增：

| Proto字段 | 名称 | 类型／含义 |
| --- | --- | --- |
| 10 | avatarFrameId | int，头像框ID，0表示未设置 |
| 11 | backgroundId | int，背景ID，0表示默认 |
| 12 | displayAchievementIds | JSON string，固定7项成就ID，例如 `[0,0,0,0,0,0,0]` |
| 13 | displayCharacterIds | JSON string，固定6项，关联player_character.id，例如 `[0,0,0,0,0,0]` |

修改使用请求93 `ReqUpdatePlayerBasicInfo`、响应94 `ResUpdatePlayerBasicInfo`。
响应与查询布局一致，成功返回完整的最新资料。

请求字段：1 updateFields（repeated PlayerProfileField）、2 avatarId（optional int）、
3 avatarFrameId（optional int）、4 titleId（optional int）、5 backgroundId（optional int）、
6 displayAchievementIds（JSON string）、7 displayCharacterIds（JSON string）、8 signature（string）。
枚举：Avatar=1、AvatarFrame=2、Title=3、Background=4、Achievements=5、Characters=6、Signature=7。

只修改updateFields明确选中的字段；空列表、重复字段、未知字段、缺少所选值均拒绝。
标量0表示取消设置，空字符串清空签名，数组中的0表示空位；数组须完整提交7／6项，非零ID不能重复。
签名最多255个Unicode标量，拒绝无效UTF-16。ID不能为负。
角色实例必须属于当前玩家；装饰和成就当前仅基础校验，尚未实现配置及解锁校验。
仅允许操作已鉴权连接的玩家，姓名、公会、创建时间等不能通过此接口修改。

持久化使用玩家锁和数据库事务，锁定玩家行，只更新所选列，提交后同步账号缓存和在线Player引用。
重复保存同样的值返回成功，无扣费／奖励副作用，不额外建立幂等记录。
数据库数组存为JSON文本，格式异常返回SystemErr并记录日志，不自动覆盖损坏的数据。

### MiaoWu调用

```csharp
var manager = PlayerBasicInfoManager.Singleton;
var loaded = await manager.LoadAsync(token);
if (!loaded.Success) return;
var result = await manager.SetSignatureAsync("新的签名", token);

// 多个字段一次保存；不在UpdateFields中的内容保持原值。
result = await manager.UpdateAsync(new PlayerBasicInfoUpdate
{
    UpdateFields = new[] { PlayerProfileField.AvatarFrame, PlayerProfileField.Characters },
    AvatarFrameId = 12,
    DisplayCharacterIds = new long[] { 9000000001, 0, 0, 0, 0, 0 }
}, token);
if (result.Success)
{
    var latest = manager.CurrentInfo;
}

// 事件来自MiaoWu.Networking.PlayerProfile，协议枚举来自Game.Datas.Messages。
UniEvent.AddListener<PlayerBasicInfoUpdatedEvent>(message =>
{
    var info = ((PlayerBasicInfoUpdatedEvent)message).Result.Info;
    // 用info刷新展示；失败事件为PlayerBasicInfoUpdateFailedEvent。
});
```

便捷方法：SetAvatarAsync、SetAvatarFrameAsync、SetTitleAsync、SetBackgroundAsync、
SetDisplayAchievementsAsync、SetDisplayCharactersAsync、SetSignatureAsync。
CurrentInfo展示数组为只读、独立快照。查询和修改共用互斥及10秒超时。
修改成功直接应用服务端完整响应；失败不替换快照。
修改取消、超时或断线后结果可能不确定，NeedsRefresh=true，先查询确认，不自动重试写入。
注销清空资料；断线保留展示快照但标记待刷新；每次鉴权恢复自动重新查询，恢复完成前禁止编辑。
UIPlayerBasicInfo已监听修改成功事件；本次没有新增编辑按钮或修改预制体。

部署顺序：执行ugame_10001.sql中新增四列的兼容语句 → 部署服务端 → 更新客户端。
展示字段为MySQL 5.7兼容的varchar(255)，保存JSON数组文本和固定长度全零默认值。
新增协议采用非打包格式写数组，同时接受打包格式读取；旧客户端可以忽略追加字段。
新客户端需要新服务端支持修改及重连恢复资料查询。
