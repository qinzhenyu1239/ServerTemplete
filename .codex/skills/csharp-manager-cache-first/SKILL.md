---
name: csharp-manager-cache-first
description: Standardize cache-first read methods in CSharpGameServer GM Managers. Use when implementing or refactoring GetInfo, query, detail, state, inventory, character, gacha, or other read paths that acquire PlayerOperationLock, read GM_PlayerEntity components, fall back to MySQL, or risk duplicating database access across gameplay managers.
---

# CSharpGameServer Manager Cache-First Reads

Use this skill together with `csharp-game-server-feature`. Preserve its Controller → Module → GM Manager → Component/database boundaries.

## Required shape

Follow the established `GetPoolInfo` → `GetPoolInfoLocked` pattern:

1. Let the public Manager method acquire `PlayerOperationLock` and immediately delegate to a private `*Locked` method.
2. Let the `*Locked` method orchestrate domain reads, handle errors, and assemble a domain result.
3. Keep `DBService`, `SqlSugarClient`, `Queryable`, SQL strings, and online/offline branching out of the `*Locked` method.
4. Give each owning Manager one reusable read entry point that implements cache-first behavior.
5. Read another domain through its Manager instead of duplicating that domain's component or database logic.

```csharp
public FeatureInfoResult GetInfo(long playerId, FeatureDefinition definition)
{
    using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
    lock (playerLock.SyncRoot)
    {
        return GetInfoLocked(playerId, definition);
    }
}

private FeatureInfoResult GetInfoLocked(long playerId, FeatureDefinition definition)
{
    try
    {
        FeatureState state = GetFeatureState(playerId, definition.FeatureId);
        if (state == null) return CreateNotOwned(definition);

        long quantity = GM_ItemMgr.Instance.GetQuantity(playerId, definition.ItemId);
        return CreateInfo(definition, state, quantity);
    }
    catch (Exception exception)
    {
        InvalidateComponents(playerId);
        return CreateFailed(definition);
    }
}
```

## Cache-first entry points

Implement an owning Manager read entry point as follows:

- If `GM_EntityMgr` contains the online player, ensure the relevant component is loaded and return component state.
- If the player is offline, query only the owning Manager's table and return the same domain shape.
- Put full component reload SQL in the owning Manager. Let the Component's `Reload` or `EnsureLoaded` delegate to that Manager.
- Put generic `player_item` reads in `GM_ItemMgr`; do not reproduce them in gacha, character star-up, rewards, or other gameplay managers.
- Do not add a cross-domain join inside a feature Manager merely to conceal duplicated ownership. Call the other owning Manager instead.

## Write paths

Do not apply the read pattern mechanically to mutations. Keep an atomic gameplay mutation in its owning Manager with one transaction, row locks, conditional updates, affected-row checks, commit-before-component-sync, and rollback on failure.

## Review checklist

Before completing a change, verify:

- The public read method only acquires the player lock and calls `*Locked`.
- The `*Locked` method contains no persistence API or SQL.
- Online reads use loaded components without querying MySQL again.
- Offline fallback exists only in the owning Manager's read entry point.
- Shared item reads go through `GM_ItemMgr`.
- There is no second implementation of the same table read in the calling feature method.
- Existing response fields, command numbers, status behavior, and cache invalidation remain compatible.
- Relevant unit and MySQL integration tests pass, and the .NET 8 server builds.
