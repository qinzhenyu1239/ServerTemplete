using Game.Datas.DBEntities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.GM_MailMessage
{
    public enum MailAction { Summary, List, Read, Claim, ClaimAll, Delete, DeleteRead, Cleanup }
    public enum MailStatus { Success, Invalid, NotFound, NotAllowed, NoRewards, InvalidReward, SourceConflict, Failed }
    public sealed class MailReward
    {
        public int ItemId { get; init; }
        public long Quantity { get; init; }
    }
    public sealed class MailSendCommand
    {
        public long PlayerId { get; init; }
        public long SenderId { get; init; } = -1;
        public int MailType { get; init; }
        public string Title { get; init; }
        public string Body { get; init; }
        public long LifetimeSeconds { get; init; }
        public int SourceType { get; init; }
        public string SourceKey { get; init; }
        public IReadOnlyList<MailReward> Rewards { get; init; } = Array.Empty<MailReward>();
    }
    public sealed class MailData
    {
        public Mailmessage Mail { get; init; }
        public IReadOnlyList<Mailattachment> Attachments { get; init; } = Array.Empty<Mailattachment>();
    }
    public sealed class MailResult
    {
        public MailStatus Status { get; set; }
        public string Error { get; set; }
        public long MailId { get; set; }
        public int Count { get; set; }
        public int UnreadCount { get; set; }
        public int Capacity { get; set; }
        public long ServerTime { get; set; }
        public long MailboxVersion { get; set; }
        public IReadOnlyList<MailData> Items { get; set; } = Array.Empty<MailData>();
        public List<long> AddedIds { get; } = new();
        public List<long> ChangedIds { get; } = new();
        public List<long> DeletedIds { get; } = new();
        public Dictionary<int, long> ItemQuantities { get; set; } = new();
    }
    public static class MailRules
    {
        public static bool IsValid(Mailmessage m, long now) => m.deleteTime == 0 && (m.expireTime == 0 || m.expireTime > now);
        public static bool CanDelete(Mailmessage m) => m.readTime > 0 && (m.mailType == 0 || m.claimTime > 0);
        public static int Group(Mailmessage m) => m.readTime == 0 ? 0 : m.mailType == 1 && m.claimTime == 0 ? 1 : 2;
        public static Mailmessage[] Sort(IEnumerable<Mailmessage> mails) => mails.OrderBy(Group)
            .ThenByDescending(m => m.sendTime).ThenByDescending(m => m.id).ToArray();
        public static bool ValidSend(MailSendCommand c) => c != null && c.PlayerId > 0 && c.LifetimeSeconds > 0 &&
            c.LifetimeSeconds <= long.MaxValue - DateTimeOffset.UtcNow.ToUnixTimeSeconds() &&
            !string.IsNullOrWhiteSpace(c.Title) && c.Title.Length <= 128 && c.Body != null &&
            System.Text.Encoding.UTF8.GetByteCount(c.Body) <= 49152 && c.SourceType >= 0 &&
            (c.SourceKey == null || !string.IsNullOrWhiteSpace(c.SourceKey) && c.SourceKey.Length <= 128) &&
            c.Rewards != null && c.Rewards.Count <= 256 && c.Rewards.All(r => r != null && r.ItemId > 0 && r.Quantity > 0) &&
            (c.MailType == 0 && c.Rewards.Count == 0 || c.MailType == 1 && c.Rewards.Count > 0);
    }
}
