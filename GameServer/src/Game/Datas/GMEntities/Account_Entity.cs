using Game.Datas.DBEntities;

namespace Game.Datas.GMEntities
{
    /// <summary>
    /// Account-stage entity. It exists before a player enters the game and may
    /// continue to serve offline account/player lookups afterwards.
    /// </summary>
    public sealed class GM_AccountEntity
    {
        public readonly object syncRoot = new object();

        public AccountComponent uAccount;
        public AccountPlayerComponent uPlayer;
        public AccountLoginBonuesComponent uLoginBonues;

        public GM_AccountEntity(long accountId)
        {
            uAccount.accountId = accountId;
        }
    }

    public struct AccountComponent
    {
        public long accountId;
        public Account accountInfo;
    }

    public struct AccountPlayerComponent
    {
        // The current project allows one player per account. Keep this as a
        // component so it can be expanded to a collection when multi-role is enabled.
        public Player playerInfo;
    }

    public struct AccountLoginBonuesComponent
    {
        public bool isLoaded;
        public Loginbonues loginBonues;
    }
}
