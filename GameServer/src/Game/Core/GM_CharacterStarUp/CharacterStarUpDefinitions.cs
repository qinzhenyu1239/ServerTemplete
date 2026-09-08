using System.Collections.Generic;

namespace Game.Core.GM_CharacterStarUp
{
    public enum CharacterStarUpStatus
    {
        Success,
        NotOwned,
        FragmentNotEnough,
        TargetStarInvalid,
        Failed,
        OperationConflict
    }

    public sealed class CharacterStarUpDefinition
    {
        public int CharacterId { get; init; }
        public int PieceItemId { get; init; }
        public int MaxStar { get; init; }
        public IReadOnlyDictionary<int, int> PieceCounts { get; init; }
    }

    public sealed class CharacterStarUpInfoResult
    {
        public CharacterStarUpStatus Status { get; init; }
        public int CharacterId { get; init; }
        public int Star { get; init; }
        public int PieceItemId { get; init; }
        public long PieceQuantity { get; init; }
        public int NextStar { get; init; }
        public int NextPieceCount { get; init; }
        public bool IsMaxStar { get; init; }
    }

    public sealed class CharacterStarUpResult
    {
        public string OperationId { get; init; }
        public CharacterStarUpStatus Status { get; init; }
        public int CharacterId { get; init; }
        public int RequestedStar { get; init; }
        public int PreviousStar { get; init; }
        public int CurrentStar { get; init; }
        public bool DidUpgrade { get; init; }
        public int ConsumedPieceCount { get; init; }
        public int PieceItemId { get; init; }
        public long RemainingPieceQuantity { get; init; }
        public bool IsMaxStar { get; init; }
        public int NextStar { get; init; }
        public int NextPieceCount { get; init; }
    }
}
