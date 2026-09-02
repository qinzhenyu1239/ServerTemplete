using Framework.Core.Utils;
using Game.Core.Db;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Game.Utils;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Game.Core.EntityMgr
{
    /// <summary>
    /// Owns account-stage entities and replaces the former BaseCacheSerivce
    /// account/player/login-bonus caches with one coherent object graph.
    /// </summary>
    public sealed class GM_AccountEntityMgr
    {
        public static GM_AccountEntityMgr Instance { get; } = new GM_AccountEntityMgr();

        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private ConcurrentDictionary<long, GM_AccountEntity> accounts;
        private ConcurrentDictionary<string, long> accountIdsByUsername;
        private ConcurrentDictionary<string, long> accountIdsByGuestKey;
        private ConcurrentDictionary<long, long> accountIdsByPlayerId;
        private ConcurrentDictionary<string, object> creationLocks;

        private GM_AccountEntityMgr() => Init();

        public void Init()
        {
            accounts = new ConcurrentDictionary<long, GM_AccountEntity>();
            accountIdsByUsername = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
            accountIdsByGuestKey = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
            accountIdsByPlayerId = new ConcurrentDictionary<long, long>();
            creationLocks = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        }

        public GM_AccountEntity Get(long accountId)
        {
            if (accountId <= 0) return null;
            if (accounts.TryGetValue(accountId, out GM_AccountEntity entity) &&
                entity.uAccount.accountInfo != null)
            {
                return entity;
            }

            Account account = LoadAccountById(accountId);
            return account == null ? null : AttachAccount(account);
        }

        public Account GetAccountById(long accountId) => Get(accountId)?.uAccount.accountInfo;

        public Account GetAccountByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            if (accountIdsByUsername.TryGetValue(username, out long accountId))
            {
                return GetAccountById(accountId);
            }

            Account account = LoadAccountByUsername(username);
            return account == null ? null : AttachAccount(account).uAccount.accountInfo;
        }

        public async Task<Account> GetAccountByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            if (accountIdsByUsername.TryGetValue(username, out long accountId))
            {
                return GetAccountById(accountId);
            }

            Account account = await LoadAccountByUsernameAsync(username);
            return account == null ? null : AttachAccount(account).uAccount.accountInfo;
        }

        public Account GetAccountByGuestKey(string guestKey)
        {
            if (string.IsNullOrWhiteSpace(guestKey)) return null;
            if (accountIdsByGuestKey.TryGetValue(guestKey, out long accountId))
            {
                return GetAccountById(accountId);
            }

            Account account = LoadAccountByGuestKey(guestKey);
            return account == null ? null : AttachAccount(account).uAccount.accountInfo;
        }

        public Account GetOrCreateGuest(string guestKey, int channel)
        {
            Account existing = GetAccountByGuestKey(guestKey);
            if (existing != null) return existing;

            object creationLock = creationLocks.GetOrAdd("guest:" + guestKey, _ => new object());
            try
            {
                lock (creationLock)
                {
                    existing = GetAccountByGuestKey(guestKey);
                    if (existing != null) return existing;

                    Account account = BuildGuestAccount(guestKey, channel);
                    if (InsertAccount(account) != 1) return null;
                    return AttachAccount(account).uAccount.accountInfo;
                }
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Create guest account failed: guestKey[{guestKey}]");
                return null;
            }
            finally
            {
                creationLocks.TryRemove("guest:" + guestKey, out _);
            }
        }

        public Account CreateAccount(ReqRegisterUser request)
        {
            try
            {
                Account account = BuildAccount(request);
                if (InsertAccount(account) != 1) return null;
                return AttachAccount(account).uAccount.accountInfo;
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Create account failed: username[{request?.uname}]");
                return null;
            }
        }

        public async Task<Account> CreateAccountAsync(ReqRegisterUser request)
        {
            try
            {
                Account account = BuildAccount(request);
                if (await InsertAccountAsync(account) != 1) return null;
                return AttachAccount(account).uAccount.accountInfo;
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Create account failed: username[{request?.uname}]");
                return null;
            }
        }

        public bool UpgradeGuestAccount(
            long accountId,
            string username,
            string passwordHash,
            string nickname,
            out Account account)
        {
            account = GetAccountById(accountId);
            if (account == null || account.is_guest != 1) return false;

            GM_AccountEntity entity = accounts[accountId];
            lock (entity.syncRoot)
            {
                string oldGuestKey = account.guest_key;
                try
                {
                    if (UpdateGuestAccountInDatabase(
                            accountId, username, passwordHash, nickname) != 1)
                    {
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    logger.Error(exception, $"Upgrade guest account failed: accountId[{accountId}]");
                    return false;
                }

                account.is_guest = 0;
                account.guest_key = string.Empty;
                account.uname = username;
                account.upwd = passwordHash;
                account.unick = nickname;
                if (!string.IsNullOrEmpty(oldGuestKey)) accountIdsByGuestKey.TryRemove(oldGuestKey, out _);
                accountIdsByUsername[username] = accountId;
                return true;
            }
        }

        public Player GetPlayerById(long playerId)
        {
            if (playerId <= 0) return null;
            if (accountIdsByPlayerId.TryGetValue(playerId, out long accountId) &&
                accounts.TryGetValue(accountId, out GM_AccountEntity entity) &&
                entity.uPlayer.playerInfo?.id == playerId)
            {
                return entity.uPlayer.playerInfo;
            }

            Player player = LoadPlayerById(playerId);
            return player == null ? null : AttachPlayer(player);
        }

        public Player GetPlayerByAccountId(long accountId, int job)
        {
            if (accountId <= 0) return null;
            GM_AccountEntity entity = GetOrAddEntity(accountId);
            if (entity.uPlayer.playerInfo != null) return entity.uPlayer.playerInfo;

            Player player = LoadPlayerByAccountId(accountId);
            return player == null ? null : AttachPlayer(player);
        }

        public Player GetOrCreatePlayer(long accountId, ReqSelectPlayer request)
        {
            Player player = GetPlayerByAccountId(accountId, request.job);
            if (player != null) return player;

            GM_AccountEntity entity = GetOrAddEntity(accountId);
            lock (entity.syncRoot)
            {
                if (entity.uPlayer.playerInfo != null) return entity.uPlayer.playerInfo;
                player = LoadPlayerByAccountId(accountId);
                if (player != null) return AttachPlayer(player);

                player = BuildPlayer(accountId, request);
                try
                {
                    if (InsertPlayer(player) != 1) return null;
                }
                catch (Exception exception)
                {
                    logger.Error(exception, $"Create player failed: accountId[{accountId}]");
                    return null;
                }
                return AttachPlayer(player);
            }
        }

        public bool UpdatePlayer(Player player)
        {
            if (player == null) return false;
            int affectedRows = UpdatePlayerInDatabase(player);
            if (affectedRows != 1) return false;
            AttachPlayer(player);
            return true;
        }

        public Loginbonues GetLoginBonues(long playerId, bool createIfMissing = false)
        {
            Player player = GetPlayerById(playerId);
            if (player == null) return null;
            GM_AccountEntity entity = GetOrAddEntity(player.accountId);
            lock (entity.syncRoot)
            {
                if (entity.uLoginBonues.isLoaded &&
                    (entity.uLoginBonues.loginBonues != null || !createIfMissing))
                {
                    return entity.uLoginBonues.loginBonues;
                }

                Loginbonues loginBonues = LoadLoginBonues(playerId);
                if (loginBonues == null && createIfMissing)
                {
                    loginBonues = new Loginbonues
                    {
                        uid = playerId,
                        bonues = 0,
                        status = 1,
                        bonues_time = (int)UtilsHelper.Timestamp(),
                        days = 0
                    };
                    if (InsertLoginBonues(loginBonues) != 1) return null;
                }

                entity.uLoginBonues.loginBonues = loginBonues;
                entity.uLoginBonues.isLoaded = true;
                return loginBonues;
            }
        }

        public bool UpdateLoginBonues(Loginbonues loginBonues)
        {
            if (loginBonues == null || UpdateLoginBonuesInDatabase(loginBonues) != 1)
            {
                return false;
            }

            Player player = GetPlayerById(loginBonues.uid);
            if (player != null)
            {
                GM_AccountEntity entity = GetOrAddEntity(player.accountId);
                entity.uLoginBonues.loginBonues = loginBonues;
                entity.uLoginBonues.isLoaded = true;
            }
            return true;
        }

        public long PlayerKey(long accountId, int job) => accountId << 3;

        private GM_AccountEntity AttachAccount(Account account)
        {
            GM_AccountEntity entity = GetOrAddEntity(account.uid);
            lock (entity.syncRoot)
            {
                entity.uAccount.accountId = account.uid;
                entity.uAccount.accountInfo = account;
                if (!string.IsNullOrEmpty(account.uname)) accountIdsByUsername[account.uname] = account.uid;
                if (!string.IsNullOrEmpty(account.guest_key)) accountIdsByGuestKey[account.guest_key] = account.uid;
            }
            return entity;
        }

        private Player AttachPlayer(Player player)
        {
            GM_AccountEntity entity = GetOrAddEntity(player.accountId);
            lock (entity.syncRoot)
            {
                entity.uPlayer.playerInfo = player;
                accountIdsByPlayerId[player.id] = player.accountId;
            }
            return player;
        }

        private GM_AccountEntity GetOrAddEntity(long accountId) =>
            accounts.GetOrAdd(accountId, id => new GM_AccountEntity(id));

        private static Account BuildGuestAccount(string guestKey, int channel)
        {
            int face = GameUtils.Random(0, 6);
            return new Account
            {
                uid = IdGenerator.GetNextId(),
                guest_key = guestKey,
                is_guest = 1,
                uchannel = channel,
                unick = "游客" + GameUtils.Random(1000, 10000),
                uface = face,
                usex = face < 3 ? 1 : 0,
                status = 0
            };
        }

        private static Account BuildAccount(ReqRegisterUser request)
        {
            int face = GameUtils.Random(0, 6);
            return new Account
            {
                uid = IdGenerator.GetNextId(),
                guest_key = string.Empty,
                is_guest = 0,
                uchannel = request.channal,
                unick = "用户" + GameUtils.Random(1000, 10000),
                uface = face,
                usex = face < 3 ? 1 : 0,
                status = 0,
                uname = request.uname,
                upwd = UtilsHelper.Md5(request.upwd)
            };
        }

        private static Player BuildPlayer(long accountId, ReqSelectPlayer request) => new Player
        {
            id = IdGenerator.GetNextId(),
            accountId = accountId,
            job = request.job,
            name = request.uname,
            HP = 100,
            MP = 0,
            status = 0,
            lastDailyReset = 0,
            level = 1,
            ucoin = 100,
            umoney = 0,
            usex = request.usex,
            vipRightJson = string.Empty
        };

        private static Account LoadAccountById(long accountId) =>
            DBService.Instance.GetAuthInstance().Queryable<Account>()
                .First(it => it.uid == accountId);

        private static Account LoadAccountByUsername(string username) =>
            DBService.Instance.GetAuthInstance().Queryable<Account>()
                .First(it => it.uname == username);

        private static Task<Account> LoadAccountByUsernameAsync(string username) =>
            DBService.Instance.GetAuthInstance().Queryable<Account>()
                .FirstAsync(it => it.uname == username);

        private static Account LoadAccountByGuestKey(string guestKey) =>
            DBService.Instance.GetAuthInstance().Queryable<Account>()
                .First(it => it.guest_key == guestKey);

        private static int InsertAccount(Account account) =>
            DBService.Instance.GetAuthInstance().Insertable(account).ExecuteCommand();

        private static Task<int> InsertAccountAsync(Account account) =>
            DBService.Instance.GetAuthInstance().Insertable(account).ExecuteCommandAsync();

        private static int UpdateGuestAccountInDatabase(
            long accountId,
            string username,
            string passwordHash,
            string nickname) =>
            DBService.Instance.GetAuthInstance().Updateable<Account>()
                .SetColumns(it => new Account
                {
                    is_guest = 0,
                    guest_key = string.Empty,
                    uname = username,
                    upwd = passwordHash,
                    unick = nickname
                })
                .Where(it => it.uid == accountId && it.is_guest == 1)
                .ExecuteCommand();

        private static Player LoadPlayerById(long playerId) =>
            DBService.Instance.GetGameInstance().Queryable<Player>()
                .First(it => it.id == playerId);

        private static Player LoadPlayerByAccountId(long accountId) =>
            DBService.Instance.GetGameInstance().Queryable<Player>()
                .First(it => it.accountId == accountId);

        private static int InsertPlayer(Player player) =>
            DBService.Instance.GetGameInstance().Insertable(player).ExecuteCommand();

        private static int UpdatePlayerInDatabase(Player player) =>
            DBService.Instance.GetGameInstance().Updateable(player)
                .Where(it => it.id == player.id)
                .ExecuteCommand();

        private static Loginbonues LoadLoginBonues(long playerId) =>
            DBService.Instance.GetGameInstance().Queryable<Loginbonues>()
                .First(it => it.uid == playerId);

        private static int InsertLoginBonues(Loginbonues loginBonues) =>
            DBService.Instance.GetGameInstance().Insertable(loginBonues).ExecuteCommand();

        private static int UpdateLoginBonuesInDatabase(Loginbonues loginBonues) =>
            DBService.Instance.GetGameInstance().Updateable(loginBonues)
                .Where(it => it.uid == loginBonues.uid)
                .ExecuteCommand();
    }
}
