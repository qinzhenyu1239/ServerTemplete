using Game.Core.Db;
using Framework.Core.Concurrency;
using Game.Core.EntityMgr;
using Game.Core.GM_Item;
using Framework.Core.Idempotency;
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

        public void CleanupExpiredOperations()
        {
            try
            {
                using SqlSugarClient db = DBService.Instance.GetGameInstance();
                long backlog = db.Ado.SqlQuery<long>(
                    "SELECT COUNT(*) FROM player_character_starup_operation WHERE expires_at < UTC_TIMESTAMP(6)")
                    .Single();
                int deleted = CharacterStarUpOperationRepository.Instance.DeleteExpired(db);
                logger.Info($"idempotency_cleanup operation=character_starup expiredBacklog={backlog} deleted={deleted}");
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Character star-up operation cleanup failed.");
            }
        }

        public CharacterStarUpInfoResult GetCharacterInfo(long playerId, CharacterStarUpDefinition definition)
        {
            using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
            lock (playerLock.SyncRoot)
            {
                return GetCharacterInfoLocked(playerId, definition);
            }
        }

        private CharacterStarUpInfoResult GetCharacterInfoLocked(
            long playerId, CharacterStarUpDefinition definition)
        {
            try
            {
                CharacterInfoSnapshot character = GetCharacterState(playerId, definition.CharacterId);
                if (character == null)
                    return CreateInfoError(CharacterStarUpStatus.NotOwned, definition);

                long pieceQuantity = GM_ItemMgr.Instance.GetQuantity(
                    playerId, definition.PieceItemId);
                return CreateInfo(definition, character.Star, pieceQuantity);
            }
            catch (Exception exception)
            {
                logger.Error(exception,
                    $"Failed to query character {definition.CharacterId} for player {playerId}.");
                InvalidateComponents(playerId);
                return CreateInfoError(CharacterStarUpStatus.Failed, definition);
            }
        }

        public CharacterStarUpResult StarUp(
            long playerId, CharacterStarUpDefinition definition, int targetStar) =>
            StarUpCore(playerId, definition, targetStar, null, null);

        public CharacterStarUpResult StarUp(
            long playerId, CharacterStarUpDefinition definition, int targetStar, string operationId) =>
            StarUpCore(playerId, definition, targetStar, operationId, null);

        internal CharacterStarUpResult StarUpForTest(
            long playerId, CharacterStarUpDefinition definition, int targetStar, Action afterDeduction) =>
            StarUpCore(playerId, definition, targetStar, null, afterDeduction);

        internal CharacterStarUpResult StarUpForTest(
            long playerId, CharacterStarUpDefinition definition, int targetStar,
            string operationId, Action afterDeduction) =>
            StarUpCore(playerId, definition, targetStar, operationId, afterDeduction);

        private CharacterStarUpResult StarUpCore(
            long playerId, CharacterStarUpDefinition definition, int targetStar,
            string operationId, Action afterDeduction)
        {
            using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
            lock (playerLock.SyncRoot)
            {
                using SqlSugarClient db = DBService.Instance.GetGameInstance();
                try
                {
                    db.Ado.BeginTran();
                    if (operationId != null)
                    {
                        string requestHash = OperationRequestHasher.Compute(
                            "character-star-up", definition.CharacterId, targetStar);
                        CharacterStarUpResult stored = CharacterStarUpOperationRepository.Instance.TryBegin(
                            db, playerId, operationId, definition.CharacterId, targetStar, requestHash);
                        if (stored != null)
                        {
                            db.Ado.CommitTran();
                            return stored;
                        }
                    }
                    Player_character character = db.Ado.SqlQuery<Player_character>(
                            "SELECT id, player_id, character_id, star FROM player_character " +
                            "WHERE player_id=@playerId AND character_id=@characterId FOR UPDATE",
                            new SugarParameter("@playerId", playerId),
                            new SugarParameter("@characterId", definition.CharacterId))
                        .SingleOrDefault();
                    if (character == null)
                    {
                        CharacterStarUpResult result = CreateStarUpResult(CharacterStarUpStatus.NotOwned,
                            definition, targetStar, 0, 0, false, 0, 0, operationId);
                        FinishBusinessResult(db, playerId, operationId, result, false);
                        return result;
                    }

                    int previousStar = character.star;
                    if (targetStar <= previousStar)
                    {
                        long unchangedQuantity = GetItemQuantity(db, playerId, definition.PieceItemId);
                        CharacterStarUpResult result = CreateStarUpResult(CharacterStarUpStatus.TargetStarInvalid,
                            definition, targetStar, previousStar, previousStar, false, 0,
                            unchangedQuantity, operationId);
                        FinishBusinessResult(db, playerId, operationId, result, false);
                        return result;
                    }

                    if (targetStar != previousStar + 1 || targetStar > definition.MaxStar ||
                        !definition.PieceCounts.TryGetValue(previousStar, out int cost) || cost <= 0)
                    {
                        CharacterStarUpResult result = CreateStarUpResult(CharacterStarUpStatus.TargetStarInvalid, definition,
                            targetStar, previousStar, previousStar, false, 0,
                            GetItemQuantity(db, playerId, definition.PieceItemId), operationId);
                        FinishBusinessResult(db, playerId, operationId, result, false);
                        return result;
                    }

                    Player_item item = LockItem(db, playerId, definition.PieceItemId);
                    if (item.quantity < cost)
                    {
                        CharacterStarUpResult result = CreateStarUpResult(CharacterStarUpStatus.FragmentNotEnough,
                            definition, targetStar, previousStar, previousStar, false, 0,
                            item.quantity, operationId);
                        FinishBusinessResult(db, playerId, operationId, result, false);
                        return result;
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

                    long remaining = item.quantity - cost;
                    CharacterStarUpResult success = CreateStarUpResult(CharacterStarUpStatus.Success,
                        definition, targetStar, previousStar, targetStar, true, cost, remaining, operationId);
                    FinishBusinessResult(db, playerId, operationId, success, true);
                    SyncCommittedComponents(playerId, definition.CharacterId, checked((byte)targetStar),
                        definition.PieceItemId, remaining);
                    return success;
                }
                catch (IdempotentOperationConflictException)
                {
                    try { db.Ado.RollbackTran(); }
                    catch (Exception rollbackException) { logger.Error(rollbackException); }
                    return CreateStarUpResult(CharacterStarUpStatus.OperationConflict, definition,
                        targetStar, 0, 0, false, 0, 0, operationId);
                }
                catch (Exception exception)
                {
                    try { db.Ado.RollbackTran(); }
                    catch (Exception rollbackException) { logger.Error(rollbackException); }
                    logger.Error(exception,
                        $"Character star-up failed for player {playerId}, character {definition.CharacterId}.");
                    InvalidateComponents(playerId);
                    return CreateStarUpResult(CharacterStarUpStatus.Failed, definition,
                        targetStar, 0, 0, false, 0, 0, operationId);
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

        private CharacterInfoSnapshot GetCharacterState(long playerId, int characterId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                if (entity.uCharacter.characterStars == null)
                    ReloadCharacterComponent(entity);
                if (!entity.uCharacter.characterStars.TryGetValue(
                        characterId, out byte star))
                    return null;

                return new CharacterInfoSnapshot { Star = star };
            }

            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            Player_character character = db.Queryable<Player_character>()
                .Where(row => row.player_id == playerId && row.character_id == characterId)
                .First();
            return character == null
                ? null
                : new CharacterInfoSnapshot { Star = character.star };
        }

        private static CharacterStarUpInfoResult CreateInfoError(
            CharacterStarUpStatus status, CharacterStarUpDefinition definition) => new()
        {
            Status = status,
            CharacterId = definition.CharacterId,
            PieceItemId = definition.PieceItemId
        };

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
            int previousStar, int currentStar, bool didUpgrade, int consumed, long remaining,
            string operationId = null)
        {
            bool hasAuthoritativeState = status != CharacterStarUpStatus.NotOwned &&
                                         status != CharacterStarUpStatus.Failed;
            bool isMaxStar = hasAuthoritativeState && currentStar >= definition.MaxStar;
            int nextStar = hasAuthoritativeState
                ? (isMaxStar ? currentStar : currentStar + 1)
                : 0;
            int nextCost = hasAuthoritativeState && !isMaxStar &&
                           definition.PieceCounts.TryGetValue(currentStar, out int cost)
                ? cost
                : 0;

            return new CharacterStarUpResult
            {
                OperationId = operationId,
                Status = status,
                CharacterId = definition.CharacterId,
                RequestedStar = requestedStar,
                PreviousStar = previousStar,
                CurrentStar = currentStar,
                DidUpgrade = didUpgrade,
                ConsumedPieceCount = consumed,
                PieceItemId = definition.PieceItemId,
                RemainingPieceQuantity = remaining,
                IsMaxStar = isMaxStar,
                NextStar = nextStar,
                NextPieceCount = nextCost
            };
        }

        private static void FinishBusinessResult(
            SqlSugarClient db, long playerId, string operationId,
            CharacterStarUpResult result, bool successful)
        {
            if (!successful)
            {
                db.Ado.RollbackTran();
                return;
            }

            if (operationId != null)
            {
                CharacterStarUpOperationRepository.Instance.Complete(
                    db, playerId, operationId, result);
            }
            db.Ado.CommitTran();
        }

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
            if (entity.uCharacter.characterIds != null &&
                entity.uCharacter.characterStars != null)
            {
                entity.uCharacter.characterIds.Add(characterId);
                entity.uCharacter.characterStars[characterId] = star;
            }
            if (entity.uItem.quantities != null)
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

        private sealed class CharacterInfoSnapshot
        {
            public byte Star { get; set; }
        }

    }
}
