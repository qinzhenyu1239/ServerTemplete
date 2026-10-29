using Framework.Core.Utils;
using Game.Core.Db;
using Game.Core.GM_Auth;
using Game.Debug;
using Game.Datas.DBEntities;
using Game.Datas.Excels;
using Game.Datas.GMEntities;
using Game.Utils;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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
        private readonly object creationLocksSync = new();
        private readonly Dictionary<string, CreationLock> creationLocks =
            new(StringComparer.Ordinal);

        private sealed class CreationLock
        {
            public readonly SemaphoreSlim Semaphore = new(1, 1);
            public int ReferenceCount;
        }

        private GM_AccountEntityMgr() => Init();

        public void Init()
        {
            accounts = new ConcurrentDictionary<long, GM_AccountEntity>();
            accountIdsByUsername = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            accountIdsByGuestKey = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
            accountIdsByPlayerId = new ConcurrentDictionary<long, long>();
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

        public async Task<Account> GetOrCreateAccountAsync(string username, string password, int channel)
        {
            Account account = await GetAccountByUsernameAsync(username);
            if (account != null) return account;

            string lockKey = "account:" + username.ToUpperInvariant();
            CreationLock creationLock = RetainCreationLock(lockKey);
            await creationLock.Semaphore.WaitAsync();
            try
            {
                account = await GetAccountByUsernameAsync(username);
                if (account != null) return account;

                account = await CreateAccountAsync(new AccountRegistrationCommand
                {
                    Username = username,
                    Password = password,
                    Channel = channel
                });
                if (account != null) return account;

                // A database unique key remains the final cross-process guard. If another
                // login-server instance won the insert race, attach the committed row.
                return await GetAccountByUsernameAsync(username);
            }
            finally
            {
                creationLock.Semaphore.Release();
                ReleaseCreationLock(lockKey, creationLock);
            }
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

            string lockKey = "guest:" + guestKey;
            CreationLock creationLock = RetainCreationLock(lockKey);
            creationLock.Semaphore.Wait();
            try
            {
                existing = GetAccountByGuestKey(guestKey);
                if (existing != null) return existing;

                Account account = BuildGuestAccount(guestKey, channel);
                if (InsertAccount(account) != 1) return null;
                return AttachAccount(account).uAccount.accountInfo;
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Create guest account failed: guestKey[{guestKey}]");
                return null;
            }
            finally
            {
                creationLock.Semaphore.Release();
                ReleaseCreationLock(lockKey, creationLock);
            }
        }

        private CreationLock RetainCreationLock(string key)
        {
            lock (creationLocksSync)
            {
                if (!creationLocks.TryGetValue(key, out CreationLock creationLock))
                {
                    creationLock = new CreationLock();
                    creationLocks.Add(key, creationLock);
                }

                creationLock.ReferenceCount++;
                return creationLock;
            }
        }

        private void ReleaseCreationLock(string key, CreationLock creationLock)
        {
            lock (creationLocksSync)
            {
                creationLock.ReferenceCount--;
                if (creationLock.ReferenceCount == 0 &&
                    creationLocks.TryGetValue(key, out CreationLock current) &&
                    ReferenceEquals(current, creationLock))
                {
                    creationLocks.Remove(key);
                    creationLock.Semaphore.Dispose();
                }
            }
        }

        public Account CreateAccount(AccountRegistrationCommand command)
        {
            try
            {
                Account account = BuildAccount(command);
                if (InsertAccount(account) != 1) return null;
                return AttachAccount(account).uAccount.accountInfo;
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Create account failed: username[{command?.Username}]");
                return null;
            }
        }

        public async Task<Account> CreateAccountAsync(AccountRegistrationCommand command)
        {
            try
            {
                Account account = BuildAccount(command);
                if (await InsertAccountAsync(account) != 1) return null;
                return AttachAccount(account).uAccount.accountInfo;
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Create account failed: username[{command?.Username}]");
                return null;
            }
        }

        public Account UpgradeGuestAccount(GuestAccountUpgradeCommand command)
        {
            Account account = GetAccountById(command.AccountId);
            if (account == null || account.is_guest != 1) return null;

            GM_AccountEntity entity = accounts[command.AccountId];
            lock (entity.syncRoot)
            {
                string oldGuestKey = account.guest_key;
                string passwordHash = UtilsHelper.Md5(command.Password);
                try
                {
                    if (UpdateGuestAccountInDatabase(
                            command.AccountId,
                            command.Username,
                            passwordHash,
                            command.Nickname) != 1)
                    {
                        return null;
                    }
                }
                catch (Exception exception)
                {
                    logger.Error(exception, $"Upgrade guest account failed: accountId[{command.AccountId}]");
                    return null;
                }

                account.is_guest = 0;
                account.guest_key = string.Empty;
                account.uname = command.Username;
                account.upwd = passwordHash;
                account.unick = command.Nickname;
                if (!string.IsNullOrEmpty(oldGuestKey)) accountIdsByGuestKey.TryRemove(oldGuestKey, out _);
                accountIdsByUsername[command.Username] = command.AccountId;
                return account;
            }
        }

        public bool VerifyPassword(Account account, string password)
        {
            return account != null && !string.IsNullOrEmpty(password) &&
                   string.Equals(account.upwd, UtilsHelper.Md5(password), StringComparison.Ordinal);
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

        public Player GetOrCreatePlayer(long accountId, PlayerCreationCommand command)
        {
            if (accountId <= 0 || command == null || command.Job <= 0) return null;

            Player player = GetPlayerByAccountId(accountId, command.Job);
            if (player != null) return player;

            GM_AccountEntity entity = GetOrAddEntity(accountId);
            lock (entity.syncRoot)
            {
                if (entity.uPlayer.playerInfo != null) return entity.uPlayer.playerInfo;
                player = LoadPlayerByAccountId(accountId);
                if (player != null) return AttachPlayer(player);

                player = BuildPlayer(accountId, command);
                using SqlSugarClient db = DBService.Instance.GetGameInstance();
                bool transactionStarted = false;
                try
                {
                    IReadOnlyList<InitialPlayerItem> initialItems = LoadInitialPlayerItems();
                    db.Ado.BeginTran();
                    transactionStarted = true;
                    if (db.Insertable(player).ExecuteCommand() != 1)
                        throw new InvalidOperationException("Insert player failed.");

                    foreach (InitialPlayerItem item in initialItems)
                    {
                        int insertedItems = db.Ado.ExecuteCommand(
                            "INSERT INTO player_item (player_id, item_id, quantity) " +
                            "VALUES (@playerId, @itemId, @quantity)",
                            new SugarParameter("@playerId", player.id),
                            new SugarParameter("@itemId", item.ItemId),
                            new SugarParameter("@quantity", item.Quantity));
                        if (insertedItems != 1)
                            throw new InvalidOperationException(
                                $"Insert initial player item {item.ItemId} failed.");
                    }

                    db.Ado.CommitTran();
                    transactionStarted = false;
                }
                catch (Exception exception)
                {
                    if (transactionStarted)
                    {
                        try { db.Ado.RollbackTran(); }
                        catch (Exception rollbackException)
                        {
                            logger.Error(rollbackException,
                                $"Rollback player creation failed: accountId[{accountId}]");
                        }
                    }
                    logger.Error(exception, $"Create player failed: accountId[{accountId}]");
                    return null;
                }
                player = AttachPlayer(player);
            }

            // Only this branch created and committed a new character.
            MailDebugTool.SendNewPlayerMails(player.id);
            return player;
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

        public void ApplyCommittedPlayer(Player player)
        {
            GM_AccountEntity account = GetOrAddEntity(player.accountId);
            lock (account.syncRoot)
            {
                var online = GM_EntityMgr.Instance.Get(player.id);
                // Keep the shared Player reference and unrelated cached gameplay state intact.
                Player target = online?.uPlayer.playerInfo ?? account.uPlayer.playerInfo;
                if (target == null || target.id != player.id) target = player;
                target.avatarId = player.avatarId;
                target.avatarFrameId = player.avatarFrameId;
                target.titleId = player.titleId;
                target.backgroundId = player.backgroundId;
                target.signature = player.signature;
                target.displayAchievementIds = player.displayAchievementIds;
                target.displayCharacterIds = player.displayCharacterIds;
                AttachPlayer(target);
                if (online != null) online.uPlayer.playerInfo = target;
            }
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

        private static Account BuildAccount(AccountRegistrationCommand command)
        {
            int face = GameUtils.Random(0, 6);
            return new Account
            {
                uid = IdGenerator.GetNextId(),
                guest_key = string.Empty,
                is_guest = 0,
                uchannel = command.Channel,
                unick = "用户" + GameUtils.Random(1000, 10000),
                uface = face,
                usex = face < 3 ? 1 : 0,
                status = 0,
                uname = command.Username,
                upwd = UtilsHelper.Md5(command.Password)
            };
        }

        private static Player BuildPlayer(long accountId, PlayerCreationCommand command) => new Player
        {
            id = IdGenerator.GetNextId(),
            accountId = accountId,
            job = command.Job,
            name = command.Name,
            avatarId = 0,
            avatarFrameId = 0,
            backgroundId = 0,
            displayAchievementIds = "[0,0,0,0,0,0,0]",
            displayCharacterIds = "[0,0,0,0,0,0]",
            guildId = 0,
            titleId = 0,
            signature = string.Empty,
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            HP = 100,
            MP = 0,
            status = 0,
            lastDailyReset = 0,
            level = 1,
            ucoin = 100,
            umoney = 0,
            usex = command.Sex,
            vipRightJson = string.Empty
        };

        private static IReadOnlyList<InitialPlayerItem> LoadInitialPlayerItems()
        {
            List<BaseInventory> configs =
                ExcelUtils.GetConfigDatas<BaseInventory>(nameof(BaseInventory));
            if (configs.Count == 0)
                throw new InvalidOperationException("BaseInventory config is empty.");

            var itemIds = new HashSet<int>();
            var result = new List<InitialPlayerItem>(configs.Count);
            foreach (BaseInventory config in configs)
            {
                if (config == null ||
                    !int.TryParse(config.ItemID?.Trim(), out int itemId) || itemId <= 0 ||
                    !long.TryParse(config.ItemCount?.Trim(), out long quantity) || quantity <= 0)
                {
                    throw new InvalidOperationException(
                        $"BaseInventory row {config?.ID ?? "<unknown>"} is invalid.");
                }
                if (!itemIds.Add(itemId))
                    throw new InvalidOperationException(
                        $"BaseInventory contains duplicate ItemID {itemId}.");

                result.Add(new InitialPlayerItem(itemId, quantity));
            }
            return result;
        }

        private sealed class InitialPlayerItem
        {
            public int ItemId { get; }
            public long Quantity { get; }

            public InitialPlayerItem(int itemId, long quantity)
            {
                ItemId = itemId;
                Quantity = quantity;
            }
        }

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
