namespace Game.Core.GM_Auth
{
    public enum AuthOperationStatus
    {
        Success,
        InvalidAccessToken,
        InvalidGameTicket,
        SupersededSession,
        Failed
    }

    public sealed class AccessTokenValidationResult
    {
        public AuthOperationStatus Status { get; init; }
        public long AccountId { get; init; }
        public string SessionId { get; init; }
        public long SessionVersion { get; init; }

        public bool IsSuccess => Status == AuthOperationStatus.Success;
    }

    public sealed class IssueGameTicketResult
    {
        public AuthOperationStatus Status { get; init; }
        public string Ticket { get; init; }
        public long ExpiresAtUnixSeconds { get; init; }

        public bool IsSuccess => Status == AuthOperationStatus.Success;
    }

    public sealed class GameAuthenticationResult
    {
        public AuthOperationStatus Status { get; init; }
        public long AccountId { get; init; }
    }

    public sealed class AccountRegistrationCommand
    {
        public string Username { get; init; }
        public string Password { get; init; }
        public int Channel { get; init; }
    }

    public sealed class GuestAccountUpgradeCommand
    {
        public long AccountId { get; init; }
        public string Username { get; init; }
        public string Password { get; init; }
        public string Nickname { get; init; }
    }

    public sealed class SessionInvalidationCommand
    {
        public long AccountId { get; init; }
        public long SessionVersion { get; init; }
        public string SessionId { get; init; }
        public long Timestamp { get; init; }
        public string Nonce { get; init; }
        public byte[] Signature { get; init; }
    }
}
