# CSharpGameServer Controller–Module–Manager architecture

## Required flow

```text
Wire request
  → Controller
    → FeatureModule
      → GM_FeatureMgr
        → Entity/Component and SqlSugar/MySQL, Redis, crypto, or external service
```

The only exception is a presentation-only, read-only configuration endpoint:

```text
HTTP Controller → FeatureModule → ExcelUtils
```

Do not create an empty Manager solely to satisfy naming symmetry.

## Responsibility matrix

| Layer | Owns | Must not own |
|---|---|---|
| Controller | HTTP/TCP route, raw-body/header/protobuf decoding, one Module call | Business validation, SQL, token decisions, state changes |
| Module | Request/session/player/config validation, orchestration, response mapping | Raw wire parsing, SqlSugar, locks, transaction mechanics, durable state |
| `GM_*Mgr` | Domain rules, concurrency, transactions, SQL/Redis, crypto/session state, external effects | HTTP/TCP parsing, protocol response formatting when a domain result suffices |
| Entity/Component | Bounded in-memory account/online-player state | SQL, unbounded history, independent mutable copies |

## Naming and placement

```text
Game/Datas/Messages/FeatureMessage.cs
Game/Entries/Controllers/FeatureController.cs
Game/Entries/HttpControllers/FeatureHttpController.cs
Game/Entries/Modules/FeatureModule.cs
Game/Core/GM_Feature/GM_FeatureMgr.cs
Game/Core/GM_Feature/FeatureDefinitions.cs
Game/Datas/GMEntities/Components/FeatureComponent.cs
Game/Datas/DbEntities/FeatureRecord.cs
Game/Datas/Excels/FeatureConfig.cs
```

- Do not prefix a business Module with `Http`; transport belongs to the Controller.
- Prefer `public sealed class FeatureModule` and `public static FeatureModule Instance { get; }`.
- Name handler parameters `accountId` or `playerId` exactly when dispatcher injection depends on the name.
- Define domain commands/results beside the owning Manager when an operation needs request-shaped input or returns several values. Modules own `Req*` → command and result → `Res*` mapping.

## Domain command and result template

```csharp
public sealed class FeatureCommand
{
    public int ConfigId { get; init; }
    public int Count { get; init; }
}

public sealed class FeatureResult
{
    public FeatureStatus Status { get; init; }
    public long RecordId { get; init; }
    public IReadOnlyList<FeatureReward> Rewards { get; init; }
}
```

Do not make a Manager accept `ReqFeature`, return `ResFeature`, or expose `bool` with multiple `out` values when a domain result expresses the operation clearly. Simple entity or scalar returns remain valid.

## TCP/protobuf template

```csharp
[ProtoContract]
[MessageMeta((short)Module.FEATURE, (short)Cmd.eFeatureReq)]
public sealed class ReqFeature : Message
{
    [ProtoMember(1, IsRequired = true)]
    public int value;

    [ProtoMember(2, IsRequired = true)]
    public int configId;
}

[Controller]
public sealed class FeatureController
{
    [RequestMapping]
    public object DoFeature(IdSession session, ReqFeature request, long playerId) =>
        FeatureModule.Instance.HandlerFeature(session, request, playerId);
}
```

```csharp
public ResFeature HandlerFeature(IdSession session, ReqFeature request, long playerId)
{
    if (playerId <= 0) return Error(Respones.AccountIsNotLogin);
    if (request == null || request.value <= 0) return Error(Respones.InvalidParams);
    if (!configs.TryGetValue(request.configId, out FeatureConfig config))
        return Error(Respones.InvalidParams);

    FeatureResult result = GM_FeatureMgr.Instance.Execute(playerId, config, request.value);
    return MapResponse(result);
}
```

## HTTP/protobuf template

Decode HTTP wire data only in the Controller, following `HttpAuthController`:

```csharp
private static Message DecodeMessage(byte[] data)
{
    string base64 = Encoding.UTF8.GetString(data);
    byte[] packet = Convert.FromBase64String(base64);
    if (packet.Length < ProtocolPacketLayout.HeaderLength) return null;
    short module = (short)(UtilsHelper.ReadShortLE(packet, ProtocolPacketLayout.ModuleOffset) & 0x00ff);
    short cmd = UtilsHelper.ReadShortLE(packet, ProtocolPacketLayout.CommandOffset);
    return SerializerHelper.PbDecode(module, cmd, packet, ProtocolPacketLayout.HeaderLength,
        packet.Length - ProtocolPacketLayout.HeaderLength);
}

[HttpRequestMapping("/ReqFeature")]
public object DoFeature(IFullHttpRequest request) =>
    FeatureModule.Instance.HandlerFeature(ReadRequest(request) as ReqFeature);
```

Do not use ad-hoc `Split('|')`, JSON parsing, Base64 decoding, or header parsing in a Module/Manager. For server-to-server protocols, define a `MessageMeta` protobuf message and use `MessagePusher.PackMessage`.

For a bodyless JSON configuration endpoint, the Controller routes directly:

```csharp
[HttpRequestMapping("/PullFeatureConfig")]
public object DoPull(IFullHttpRequest request) =>
    FeatureModule.Instance.HandlerPullConfig();
```

The Module may load `ExcelUtils` rows during `Init()` and cache the derived response string/index.

## Manager and transaction template

```csharp
public sealed class GM_FeatureMgr
{
    public static GM_FeatureMgr Instance { get; } = new GM_FeatureMgr();

    public FeatureResult Execute(long playerId, FeatureConfig config, int count)
    {
        using SqlSugarClient db = DBService.Instance.GetGameInstance();
        try
        {
            db.Ado.BeginTran();
            State state = LockState(db, playerId);
            int affected = ConditionalUpdate(db, playerId, count);
            if (affected != 1) throw new DomainException();
            InsertResult(db, state);
            db.Ado.CommitTran();
            SyncCommittedComponents(playerId);
            return FeatureResult.Success(...);
        }
        catch
        {
            db.Ado.RollbackTran();
            InvalidateComponents(playerId);
            return FeatureResult.Failure(...);
        }
    }
}
```

- Pass the same `SqlSugarClient` to every transaction-bound helper.
- Lock required rows inside the transaction.
- Use conditional deductions such as `quantity >= cost` and verify affected rows.
- Commit before changing component state or returning success.
- Put username/item/request locks in the Manager. Use reference-counted per-key locks when removing lock objects.

## Entity ownership

- `GM_AccountEntity`: account-stage `Account`, associated `Player`, and login-bonus components.
- `GM_PlayerEntity`: online session and gameplay components such as backpack, task, character, gacha, room, movement, and combat.
- Managers may attach the same `Account`/`Player` instance to related entities; do not create unrelated mutable copies.
- Keep mail history, draw history, audit logs, and other unbounded streams in MySQL.

## Configuration initialization

- Preload raw tables with `ExcelUtils.ReadConfigData<T>()` in `ConfigMgr.Init()` when the role uses `ConfigMgr`.
- Use `ExcelUtils.GetConfigData<T>(id)` for direct lookup.
- Build joins, groups, parsed weights, response JSON, and derived indexes in `FeatureModule.Init()`.
- Initialize the Module after raw configuration loading.
- For roles that do not initialize `ConfigMgr`, explicitly initialize the required read-only Module; `ExcelUtils` may lazy-load its table.
- Fail startup for duplicate IDs, invalid references, malformed weights, or missing mandatory rows.

## Reference implementations

Authentication:

```text
HttpAuthController → AuthModule → AuthDefinitions command/result → GM_AccountEntityMgr / GM_AuthSessionMgr
GameAuthController → GameAuthModule → AuthDefinitions result → GM_AuthSessionMgr → SessionMgr
```

Read-only server configuration:

```text
ServerZoneController → ServerZoneModule → ExcelUtils
ServerDirectConnController → ServerDirectConnModule → ExcelUtils
```

## Verification checklist

1. Search Controllers for direct `GM_*Mgr`, SQL, token, HMAC, or configuration logic.
2. Search Modules for `DBService`, `SqlSugarClient`, raw-body parsing, `Split('|')`, locks, and transaction calls.
3. Search Managers for `IFullHttpRequest`, route attributes, and request-wire parsing.
4. Search Manager public methods for `Req*`, `Res*`, and multiple `out` parameters; replace business boundary leakage with domain commands/results.
5. Confirm no legacy Controller/Module path bypasses the new flow.
6. Build with `dotnet build GameServer/GameServer.csproj --no-restore`.
7. Start each affected `ServerRole`.
8. Exercise success, invalid parameters, unauthenticated access, persistence failure, rollback, concurrency, reconnect, and response-loss cases as applicable.
