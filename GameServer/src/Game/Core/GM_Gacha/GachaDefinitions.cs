using System.Collections.Generic;

namespace Game.Core.GM_Gacha
{
    public enum GachaOperationStatus
    {
        Success,
        ResourceNotEnough,
        Failed
    }

    public sealed class GachaRewardDefinition
    {
        public int CharacterId { get; init; }
        public int FragmentItemId { get; init; }
        public int FragmentNum { get; init; }
        public int Rarity { get; init; }
        public int Weight { get; init; }
    }

    public sealed class GachaPoolDefinition
    {
        public int PoolId { get; init; }
        public int SmallPityRarity { get; init; }
        public int SmallPityCount { get; init; }
        public int BigPityRarity { get; init; }
        public int BigPityCount { get; init; }
        public int TicketItemId { get; init; }
        public int DiamondItemId { get; init; }
        public int DiamondCostPerDraw { get; init; }
        public IReadOnlyList<int> Rarities { get; init; }
        public IReadOnlyList<int> RarityWeights { get; init; }
        public IReadOnlyDictionary<int, IReadOnlyList<GachaRewardDefinition>> RewardsByRarity { get; init; }
    }

    public sealed class GachaPoolInfoResult
    {
        public GachaOperationStatus Status { get; init; }
        public int PoolId { get; init; }
        public int SmallPityCount { get; init; }
        public int SmallPityMax { get; init; }
        public int BigPityCount { get; init; }
        public int BigPityMax { get; init; }
        public long TotalDrawCount { get; init; }
        public int TicketItemId { get; init; }
        public long TicketQuantity { get; init; }
        public int DiamondItemId { get; init; }
        public long DiamondQuantity { get; init; }
        public int DiamondCostPerDraw { get; init; }
        public IReadOnlyList<int> OwnedCharacterIds { get; init; }
    }

    public sealed class GachaDrawRewardResult
    {
        public int DrawIndex { get; init; }
        public int CharacterId { get; init; }
        public int Rarity { get; init; }
        public bool IsNewCharacter { get; init; }
        public int FragmentItemId { get; init; }
        public int FragmentNum { get; init; }
    }

    public sealed class GachaDrawResult
    {
        public GachaOperationStatus Status { get; init; }
        public long BatchId { get; init; }
        public int UsedTicketCount { get; init; }
        public int UsedDiamondCount { get; init; }
        public long TicketQuantity { get; init; }
        public long DiamondQuantity { get; init; }
        public int SmallPityCount { get; init; }
        public int BigPityCount { get; init; }
        public long TotalDrawCount { get; init; }
        public IReadOnlyList<GachaDrawRewardResult> Rewards { get; init; }
    }
}
