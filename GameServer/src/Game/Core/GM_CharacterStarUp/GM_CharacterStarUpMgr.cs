using Game.Core.Db;
using Game.Core.Concurrency;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.GM_CharacterStarUp
{
    /// <summary>
    /// Owns character queries, star-up transactions and committed component synchronization.
    /// </summary>
    public sealed class GM_CharacterStarUpMgr
    {
        public static readonly GM_CharacterStarUpMgr Instance = new GM_CharacterStarUpMgr();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private GM_CharacterStarUpMgr() { }

        public CharacterStarUpInfoResult GetCharacterInfo(long playerId, CharacterStarUpDefinition definition)
        {
            using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
            lock (playerLock.SyncRoot)
            {
                try
                {
                    using SqlSugarClient db = DBService.Instance.GetGameInstance();
                    Player_character character = db.Queryable<Player_character>()
                        .Where(row => row.player_id == playerId && row.character_id == definition.CharacterId)
                        .First();
                    if (character == null)
                        return new CharacterStarUpInfoResult
                        {
                            Status = CharacterStarUpStatus.NotOwned,
                            CharacterId = definition.CharacterId,
                            PieceItemId = definition.PieceItemId
                        };

                    long quantity = GetItemQuantity(db, playerId, definition.PieceItemId);
                    SyncCommittedComponents(playerId, definition.CharacterId, character.star,
                        definition.PieceItemId, quantity);
                    return CreateInfo(definition, character.star, quantity);
                }
                catch (Exception exception)
                {
                    logger.Error(exception,
                        $"Failed to query character {definition.CharacterId} for player {playerId}.");
                    InvalidateComponents(playerId);
                    return new CharacterStarUpInfoResult
                    {
                        Status = CharacterStarUpStatus.Failed,
                        CharacterId = definition.CharacterId,
                        PieceItemId = definition.PieceItemId
                    };
                }
            }
        }

        public CharacterStarUpResult StarUp(
            long playerId, CharacterStarUpDefinition definition, int targetStar) =>
            StarUpCore(playerId, definition, targetStar, null);

        internal CharacterStarUpResult StarUpForTest(
            long playerId, CharacterStarUpDefinition definition, int targetStar, Action afterDeduction) =>
            StarUpCore(playerId, definition, targetStar, afterDeduction);

        private CharacterStarUpResult StarUpCore(
            long playerId, CharacterStarUpDefinition definition, int targetStar, Action afterDeduction)
        {
            using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
            lock (playerLock.SyncRoot)
            {
                using SqlSugarClient db = DBService.Instance.GetGameInstance();
                try
                {
                    db.Ado.BeginTran();
                    Player_character character = db.Ado.SqlQuery<Player_character>(
                            "SELECT id, player_id, character_id, star FROM player_character " +
                            "WHERE player_id=@playerId AND character_id=@characterId FOR UPDATE",
                            new SugarParameter("@playerId", playerId),
                            new SugarParameter("@characterId", definition.CharacterId))
                        .SingleOrDefault();
                    if (character == null)
                    {
                        db.Ado.RollbackTran();
                        return CreateStarUpResult(CharacterStarUpStatus.NotOwned, definition,
                            targetStar, 0, 0, false, 0, 0);
                    }

                    int previousStar = character.star;
                    if (targetStar <= previousStar)
                    {
                        long unchangedQuantity = GetItemQuantity(db, playerId, definition.PieceItemId);
                        db.Ado.CommitTran();
                        SyncCommittedComponents(playerId, definition.CharacterId, character.star,
                            definition.PieceItemId, unchangedQuantity);
                        return CreateStarUpResult(CharacterStarUpStatus.Success, definition,
                            targetStar, previousStar, previousStar, false, 0, unchangedQuantity);
                    }

                    if (targetStar != previousStar + 1 || targetStar > definition.MaxStar ||
                        !definition.PieceCounts.TryGetValue(previousStar, out int cost) || cost <= 0)
                    {
                        db.Ado.RollbackTran();
                        return CreateStarUpResult(CharacterStarUpStatus.TargetStarInvalid, definition,
                            targetStar, previousStar, previousStar, false, 0,
                            GetItemQuantity(db, playerId, definition.PieceItemId));
                    }

                    Player_item item = LockItem(db, playerId, definition.PieceItemId);
                    if (item.quantity < cost)
                    {
                        db.Ado.RollbackTran();
                        return CreateStarUpResult(CharacterStarUpStatus.FragmentNotEnough, definition,
                            targetStar, previousStar, previousStar, false, 0, item.quantity);
                    }

                    int itemAffected = db.Ado.ExecuteCommand(
                        "UPDATE player_item SET quantity=quantity-@cost " +
                        "WHERE player_id=@playerId AND item_id=@itemId AND quantity>=@cost",
                        new SugarParameter("@cost", cost),
                        new SugarParameter("@playerId", playerId),
                        new SugarParameter("@itemId", definition.PieceItemId));
                    if (itemAffected != 1)
                        throw new InvalidOperationException("Failed to deduct character fragments.");

                    afterDeduction?.Invoke();

                    int characterAffected = db.Ado.ExecuteCommand(
                        "UPDATE player_character SET star=@targetStar " +
                        "WHERE player_id=@playerId AND character_id=@characterId AND star=@previousStar",
                        new SugarParameter("@targetStar", targetStar),
                        new SugarParameter("@playerId", playerId),
                        new SugarParameter("@characterId", definition.CharacterId),
                        new SugarParameter("@previousStar", previousStar));
                    if (characterAffected != 1)
                        throw new InvalidOperationException("Failed to update character star.");

                    db.Ado.CommitTran();
                    long remaining = item.quantity - cost;
                    SyncCommittedComponents(playerId, definition.CharacterId, checked((byte)targetStar),
                        definition.PieceItemId, remaining);
                    return CreateStarUpResult(CharacterStarUpStatus.Success, definition,
                        targetStar, previousStar, targetStar, true, cost, remaining);
                }
                catch (Exception exception)
                {
                    try { db.Ado.RollbackTran(); }
                    catch (Exception rollbackException) { logger.Error(rollbackException); }
                    logger.Error(exception,
                        $"Character star-up failed for player {playerId}, character {definition.CharacterId}.");
                    InvalidateComponents(playerId);
                    return CreateStarUpResult(CharacterStarUpStatus.Failed, definition,
                        targetStar, 0, 0, false, 0, 0);
                }
            }
        }

        internal void ReloadCharacterComponent(GM_PlayerEntity entity)
        {
            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            List<Player_character> characters = db.Queryable<Player_character>()
                .Where(character => character.player_id == entity.uPlayer.playerInfo.id)
                .ToList();
            entity.uCharacter.characterIds = characters.Select(row => row.character_id).ToHashSet();
            entity.uCharacter.characterStars = characters.ToDictionary(row => row.character_id, row => row.star);
        }

        private static CharacterStarUpInfoResult CreateInfo(
            CharacterStarUpDefinition definition, int star, long quantity)
        {
            bool isMaxStar = star >= definition.MaxStar;
            int nextStar = isMaxStar ? star : star + 1;
            int nextCost = isMaxStar ? 0 : definition.PieceCounts[star];
            return new CharacterStarUpInfoResult
            {
                Status = CharacterStarUpStatus.Success,
                CharacterId = definition.CharacterId,
                Star = star,
                PieceItemId = definition.PieceItemId,
                PieceQuantity = quantity,
                NextStar = nextStar,
                NextPieceCount = nextCost,
                IsMaxStar = isMaxStar
            };
        }

        private static CharacterStarUpResult CreateStarUpResult(
            CharacterStarUpStatus status, CharacterStarUpDefinition definition, int requestedStar,
            int previousStar, int currentStar, bool didUpgrade, int consumed, long remaining) => new()
        {
            Status = status,
            CharacterId = definition.CharacterId,
            RequestedStar = requestedStar,
            PreviousStar = previousStar,
            CurrentStar = currentStar,
            DidUpgrade = didUpgrade,
            ConsumedPieceCount = consumed,
            PieceItemId = definition.PieceItemId,
            RemainingPieceQuantity = remaining,
            IsMaxStar = currentStar >= definition.MaxStar
        };

        private static Player_item LockItem(SqlSugarClient db, long playerId, int itemId)
        {
            db.Ado.ExecuteCommand(
                "INSERT IGNORE INTO player_item (player_id, item_id, quantity) VALUES (@playerId, @itemId, 0)",
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@itemId", itemId));
            return db.Ado.SqlQuery<Player_item>(
                    "SELECT id, player_id, item_id, quantity FROM player_item " +
                    "WHERE player_id=@playerId AND item_id=@itemId FOR UPDATE",
                    new SugarParameter("@playerId", playerId),
                    new SugarParameter("@itemId", itemId))
                .Single();
        }

        private static long GetItemQuantity(SqlSugarClient db, long playerId, int itemId)
        {
            Player_item item = db.Queryable<Player_item>()
                .Where(item => item.player_id == playerId && item.item_id == itemId)
                .First();
            return item?.quantity ?? 0;
        }

        private static void SyncCommittedComponents(
            long playerId, int characterId, byte star, int itemId, long quantity)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null) return;
            entity.uCharacter.characterIds ??= new HashSet<int>();
            entity.uCharacter.characterStars ??= new Dictionary<int, byte>();
            entity.uCharacter.characterIds.Add(characterId);
            entity.uCharacter.characterStars[characterId] = star;
            entity.uItem.quantities ??= new Dictionary<int, long>();
            entity.uItem.quantities[itemId] = quantity;
        }

        private static void InvalidateComponents(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null) return;
            entity.uCharacter.characterIds = null;
            entity.uCharacter.characterStars = null;
            entity.uItem.quantities = null;
        }

    }
}
