using System;
using System.Collections.Generic;

namespace Game.Core.GM_Item
{
    public enum ItemInventoryStatus
    {
        Success,
        Failed
    }

    public sealed class PlayerItemSnapshot
    {
        public int ItemId { get; init; }
        public long Quantity { get; init; }
    }

    public sealed class ItemInventoryResult
    {
        public ItemInventoryStatus Status { get; init; }
        public IReadOnlyList<PlayerItemSnapshot> Items { get; init; } =
            Array.Empty<PlayerItemSnapshot>();
    }
}
