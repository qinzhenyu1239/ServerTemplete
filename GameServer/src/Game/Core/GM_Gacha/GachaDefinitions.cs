using System.Collections.Generic;

namespace Game.Core.GM_Gacha
{
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
}
