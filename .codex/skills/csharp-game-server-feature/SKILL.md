---
name: csharp-game-server-feature
description: Implement or refactor CSharpGameServer backend features with the mandatory Controller → Module → GM Manager → Entity/Component or database flow. Use for any new server protocol, HTTP/TCP endpoint, gameplay feature, authentication flow, SqlSugar/MySQL operation, configuration table, reward, inventory, gacha, account, player, or server-discovery change in D:\BaiduNetdiskDownload\CSharpGame\CSharpGameServer.
---

# CSharpGameServer Feature Development

Use `D:\BaiduNetdiskDownload\CSharpGame\CSharpGameServer` unless the user overrides it.

Before designing or editing, read [references/architecture.md](references/architecture.md) completely.

## Mandatory workflow

1. Trace one analogous feature from message/HTTP route through Controller, Module, `GM_*Mgr`, Entity/Component, persistence/configuration, initialization, and tests.
2. Write down ownership before editing:
   - Controller: decode transport/protocol input and call exactly one Module method.
   - Module: validate request/session/player/configuration, orchestrate managers, and assemble protocol/HTTP responses.
   - `GM_*Mgr`: own reusable domain behavior, concurrency, mutable state, persistence, transactions, cryptography, and external side effects.
   - Entity/Component: own bounded in-memory account or online-player state.
3. Implement the complete call chain. Do not skip a layer by putting its work in another layer.
4. Register protocol metadata, HTTP routes, configuration preload, and Module/Manager initialization in the correct server role.
5. Preserve command numbers, request IDs, route tags, response formats, and client compatibility unless explicitly changed.
6. Build with `.NET 8`, start the affected server role, and exercise the changed endpoint or protocol.

## Non-negotiable boundaries

- Never put parameter/business validation, token decisions, SQL, configuration transformation, or state changes in a Controller.
- Never parse raw HTTP/TCP wire formats in a Module or Manager. HTTP Controllers decode standard message headers/protobuf; TCP decoding stays in the framework dispatcher.
- Never put SqlSugar queries, inserts, updates, locks, transactions, HMAC/session state, or external notifications in a Module.
- Never return transport concerns from a Manager when a domain result lets the Module assemble the response.
- Never pass `Req*`/`Res*` protocol messages across a business Manager boundary. Map requests to primitives or domain commands in the Module, and map domain results to responses in the Module.
- Put reusable commands, status enums, and multi-field Manager results in `Game/Core/GM_Feature/FeatureDefinitions.cs`. Prefer one domain result over `bool` plus multiple `out` parameters.
- Keep `Game/Core/Caches` empty. Do not create Cache, Repository, DAO, Storage, or persistence-gateway layers.
- Do not reintroduce `BaseCacheSerivce`.
- Put account-stage memory in `GM_AccountEntity`; put online gameplay memory in `GM_PlayerEntity` components.
- Keep history/log/stream data in the database unless bounded online state is explicitly required.
- Use one `SqlSugarClient` and one transaction for an atomic workflow; check affected-row counts and update components only after commit.
- Put process-local and database concurrency guards in the owning `GM_*Mgr`; database uniqueness/conditional updates remain the cross-process authority.
- Use `ExcelUtils` for CSV data. Do not introduce a second configuration provider/cache.
- Do not create an empty `GM_*Mgr` for a presentation-only, read-only configuration endpoint; use Controller → Module → `ExcelUtils`. Stateful or reusable domain features must use the full chain.

## Completion gate

Do not report completion until:

- Controllers only decode and route.
- Modules contain request-facing validation and response assembly, but no SQL or raw-wire parsing.
- Managers contain domain state, concurrency, persistence, transactions, and external side effects.
- Manager public business methods accept domain inputs and return entities, scalars, or domain results rather than protocol messages or bundles of `out` parameters.
- Entity/component synchronization occurs after successful persistence.
- No legacy bypass path reaches the same feature outside the new chain.
- `dotnet build GameServer/GameServer.csproj --no-restore` succeeds.
- The affected role starts and the changed route/protocol is exercised.

If client work is required, also use `miaowu-client` and treat `D:\iaowu\MiaoWu` as authoritative.
