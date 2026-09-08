using System.Collections.Generic;

namespace Game.Core.GM_Formation
{
    public enum FormationStatus
    {
        Success,
        Invalid,
        CharacterNotOwned,
        VersionConflict,
        Failed
    }

    public sealed class FormationModuleRule
    {
        public int ModuleId { get; init; }
        public int SlotCount { get; init; }
        public int MinDeployCount { get; init; }
        public int MaxDeployCount { get; init; }
    }

    public sealed class FormationSlotValue
    {
        public int SlotIndex { get; init; }
        public int CharacterId { get; init; }
    }

    public sealed class PlayerFormationSnapshot
    {
        public int ModuleId { get; init; }
        public int FormationId { get; init; }
        public long Version { get; init; }
        public IReadOnlyList<FormationSlotValue> Slots { get; init; }
    }

    public sealed class OwnedCharacterBrief
    {
        public int CharacterId { get; init; }
        public int Star { get; init; }
    }

    public sealed class FormationOverviewResult
    {
        public FormationStatus Status { get; init; }
        public IReadOnlyList<FormationModuleRule> ModuleRules { get; init; }
        public IReadOnlyList<PlayerFormationSnapshot> Formations { get; init; }
        public IReadOnlyList<OwnedCharacterBrief> Characters { get; init; }
    }

    public sealed class SaveFormationResult
    {
        public FormationStatus Status { get; init; }
        public PlayerFormationSnapshot Formation { get; init; }
    }

    public static class FormationRules
    {
        public const int FormationCount = 5;

        public static readonly IReadOnlyList<FormationModuleRule> All = new[]
        {
            new FormationModuleRule { ModuleId = 0, SlotCount = 9, MinDeployCount = 1, MaxDeployCount = 6 },
            new FormationModuleRule { ModuleId = 1, SlotCount = 9, MinDeployCount = 1, MaxDeployCount = 6 },
            new FormationModuleRule { ModuleId = 2, SlotCount = 12, MinDeployCount = 12, MaxDeployCount = 12 }
        };

        public static bool TryGet(int moduleId, out FormationModuleRule rule)
        {
            foreach (FormationModuleRule candidate in All)
            {
                if (candidate.ModuleId != moduleId) continue;
                rule = candidate;
                return true;
            }
            rule = null;
            return false;
        }
    }
}
