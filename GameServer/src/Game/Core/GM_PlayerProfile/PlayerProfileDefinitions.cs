using System;
using System.Linq;
using System.Text;

namespace Game.Core.GM_PlayerProfile
{
    public enum PlayerProfileStatus { Success, NotFound, Failed, InvalidInput }

    public sealed class PlayerBasicInfoSnapshot
    {
        public int BaseLevel { get; init; }
        public int MainStage { get; init; }
        public int ResonanceLevel { get; init; }
        public int TowerStage { get; init; }
        public long PlayerId { get; init; }
        public int AvatarId { get; init; }
        public string Name { get; init; }
        public long GuildId { get; init; }
        public long CreateTime { get; init; }
        public int TitleId { get; init; }
        public string Signature { get; init; }
        public int AvatarFrameId { get; init; }
        public int BackgroundId { get; init; }
        public int[] DisplayAchievementIds { get; init; }
        public long[] DisplayCharacterIds { get; init; }
    }

    public enum ProfileField { Avatar = 1, AvatarFrame = 2, Title = 3, Background = 4, Achievements = 5, Characters = 6, Signature = 7 }

    public sealed class PlayerProfileUpdate
    {
        public ProfileField[] Fields { get; init; }
        public int? AvatarId { get; init; }
        public int? AvatarFrameId { get; init; }
        public int? TitleId { get; init; }
        public int? BackgroundId { get; init; }
        public int[] DisplayAchievementIds { get; init; }
        public long[] DisplayCharacterIds { get; init; }
        public string Signature { get; init; }
        public bool Includes(ProfileField field) => Fields?.Contains(field) == true;
        public bool IsValid()
        {
            return Fields != null && Fields.Length == 1 && Fields[0] == ProfileField.Avatar && AvatarId.HasValue && AvatarId.Value >= 0;
        }
        public static bool ValidSlots(long[] ids, int count) => ids != null && ids.Length == count &&
            ids.All(x => x >= 0) && ids.Where(x => x != 0).Distinct().Count() == ids.Count(x => x != 0);
        public static bool ValidSignature(string value)
        {
            if (value == null) return false;
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsHighSurrogate(value[i]))
                {
                    if (++i >= value.Length || !char.IsLowSurrogate(value[i])) return false;
                }
                else if (char.IsLowSurrogate(value[i])) return false;
                if (++count > 255) return false;
            }
            return true;
        }
    }

    public sealed class PlayerBasicInfoResult
    {
        public PlayerProfileStatus Status { get; init; }
        public PlayerBasicInfoSnapshot Info { get; init; }
    }
}
