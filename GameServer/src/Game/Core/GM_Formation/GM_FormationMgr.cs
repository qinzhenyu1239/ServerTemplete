using Framework.Core.Concurrency;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.GM_Formation
{
    public sealed class GM_FormationMgr
    {
        public static readonly GM_FormationMgr Instance = new GM_FormationMgr();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private GM_FormationMgr() { }

        public FormationOverviewResult GetOverview(long playerId)
        {
            using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
            lock (playerLock.SyncRoot)
            {
                return GetOverviewLocked(playerId);
            }
        }

        private FormationOverviewResult GetOverviewLocked(long playerId)
        {
            try
            {
                return new FormationOverviewResult
                {
                    Status = FormationStatus.Success,
                    ModuleRules = FormationRules.All,
                    Formations = GetFormations(playerId),
                    Characters = GetOwnedCharacters(playerId)
                };
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Failed to query formations for player {playerId}.");
                InvalidateFormation(playerId);
                return new FormationOverviewResult { Status = FormationStatus.Failed };
            }
        }

        private static List<OwnedCharacterBrief> GetOwnedCharacters(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                if (entity.uCharacter.characterStars == null)
                    ReloadOwnedCharacters(entity);
                return entity.uCharacter.characterStars
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new OwnedCharacterBrief
                    {
                        CharacterId = pair.Key,
                        Star = pair.Value
                    }).ToList();
            }

            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            return LoadOwnedCharacters(db, playerId);
        }

        private static void ReloadOwnedCharacters(GM_PlayerEntity entity)
        {
            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            List<Player_character> characters = db.Queryable<Player_character>()
                .Where(row => row.player_id == entity.uPlayer.playerInfo.id)
                .OrderBy(row => row.character_id)
                .ToList();
            entity.uCharacter.characterIds = characters
                .Select(row => row.character_id)
                .ToHashSet();
            entity.uCharacter.characterStars = characters
                .ToDictionary(row => row.character_id, row => row.star);
        }

        private static List<OwnedCharacterBrief> LoadOwnedCharacters(
            SqlSugarClient db, long playerId) =>
            db.Queryable<Player_character>()
                .Where(row => row.player_id == playerId)
                .OrderBy(row => row.character_id)
                .ToList()
                .Select(row => new OwnedCharacterBrief
                {
                    CharacterId = row.character_id,
                    Star = row.star
                }).ToList();

        private static List<PlayerFormationSnapshot> GetFormations(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null)
            {
                FormationComponent.EnsureLoaded(entity);
                return OrderedFormations(entity.uFormation.formations);
            }

            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            return OrderedFormations(LoadFormations(db, playerId));
        }

        internal void ReloadFormationComponent(GM_PlayerEntity entity)
        {
            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            entity.uFormation.formations = LoadFormations(
                db, entity.uPlayer.playerInfo.id);
            entity.uFormation.isLoaded = true;
        }

        private static Dictionary<(int ModuleId, int FormationId), PlayerFormationSnapshot>
            LoadFormations(SqlSugarClient db, long playerId)
        {
            List<FormationHeaderRow> headers = db.Ado.SqlQuery<FormationHeaderRow>(
                "SELECT player_id, module_id, formation_id, version FROM player_formation " +
                "WHERE player_id=@playerId",
                new SugarParameter("@playerId", playerId));
            List<FormationSlotRow> slots = db.Ado.SqlQuery<FormationSlotRow>(
                "SELECT player_id, module_id, formation_id, slot_index, character_id " +
                "FROM player_formation_slot WHERE player_id=@playerId " +
                "ORDER BY module_id, formation_id, slot_index",
                new SugarParameter("@playerId", playerId));
            Dictionary<(int module_id, int formation_id), long> headerMap = headers.ToDictionary(
                row => (row.module_id, row.formation_id), row => row.version);
            Dictionary<(int module_id, int formation_id), IReadOnlyList<FormationSlotValue>> slotMap =
                slots.GroupBy(row => (row.module_id, row.formation_id))
                    .ToDictionary(group => group.Key, group =>
                        (IReadOnlyList<FormationSlotValue>)group.Select(MapSlot).ToList());
            var formations =
                new Dictionary<(int ModuleId, int FormationId), PlayerFormationSnapshot>();
            foreach (FormationModuleRule rule in FormationRules.All)
            {
                for (int formationId = 0; formationId < FormationRules.FormationCount; formationId++)
                {
                    var key = (rule.ModuleId, formationId);
                    formations[key] = Snapshot(
                        rule.ModuleId,
                        formationId,
                        headerMap.TryGetValue(key, out long version) ? version : 0,
                        slotMap.TryGetValue(key, out IReadOnlyList<FormationSlotValue> values)
                            ? values
                            : Array.Empty<FormationSlotValue>());
                }
            }
            return formations;
        }

        private static List<PlayerFormationSnapshot> OrderedFormations(
            IReadOnlyDictionary<(int ModuleId, int FormationId), PlayerFormationSnapshot> formations)
        {
            var result = new List<PlayerFormationSnapshot>();
            foreach (FormationModuleRule rule in FormationRules.All)
                for (int formationId = 0; formationId < FormationRules.FormationCount; formationId++)
                    result.Add(formations[(rule.ModuleId, formationId)]);
            return result;
        }

        public SaveFormationResult Save(long playerId, int moduleId, int formationId,
            long expectedVersion, IReadOnlyCollection<FormationSlotValue> requestedSlots)
            => SaveInternal(playerId, moduleId, formationId, expectedVersion, requestedSlots, null);

        internal SaveFormationResult SaveForTest(long playerId, int moduleId, int formationId,
            long expectedVersion, IReadOnlyCollection<FormationSlotValue> requestedSlots,
            Action afterSlotsWritten)
            => SaveInternal(playerId, moduleId, formationId, expectedVersion,
                requestedSlots, afterSlotsWritten);

        private SaveFormationResult SaveInternal(long playerId, int moduleId, int formationId,
            long expectedVersion, IReadOnlyCollection<FormationSlotValue> requestedSlots,
            Action afterSlotsWritten)
        {
            if (!Validate(moduleId, formationId, expectedVersion, requestedSlots))
                return new SaveFormationResult { Status = FormationStatus.Invalid };

            List<FormationSlotValue> canonicalSlots = requestedSlots
                .OrderBy(slot => slot.SlotIndex)
                .ToList();
            using PlayerOperationLock.Lease playerLock = PlayerOperationLock.Acquire(playerId);
            lock (playerLock.SyncRoot)
            {
                return SaveLocked(playerId, moduleId, formationId, expectedVersion,
                    canonicalSlots, afterSlotsWritten);
            }
        }

        private SaveFormationResult SaveLocked(long playerId, int moduleId, int formationId,
            long expectedVersion, List<FormationSlotValue> requestedSlots, Action afterSlotsWritten)
        {
            using SqlSugarClient db = DBService.Instance.GetGameInstance();
            try
            {
                db.Ado.BeginTran();
                db.Ado.ExecuteCommand(
                    "INSERT IGNORE INTO player_formation " +
                    "(player_id, module_id, formation_id, version, created_at, updated_at) " +
                    "VALUES (@playerId, @moduleId, @formationId, 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))",
                    KeyParameters(playerId, moduleId, formationId));

                FormationHeaderRow header = db.Ado.SqlQuery<FormationHeaderRow>(
                    "SELECT player_id, module_id, formation_id, version FROM player_formation " +
                    "WHERE player_id=@playerId AND module_id=@moduleId AND formation_id=@formationId FOR UPDATE",
                    KeyParameters(playerId, moduleId, formationId)).Single();
                List<FormationSlotValue> currentSlots = ReadSlots(
                    db, playerId, moduleId, formationId, forUpdate: true);

                if (header.version != expectedVersion)
                {
                    PlayerFormationSnapshot current = Snapshot(
                        moduleId, formationId, header.version, currentSlots);
                    db.Ado.RollbackTran();
                    PutFormation(playerId, current);
                    return new SaveFormationResult
                    {
                        Status = SameSlots(currentSlots, requestedSlots)
                            ? FormationStatus.Success
                            : FormationStatus.VersionConflict,
                        Formation = current
                    };
                }

                HashSet<int> ownedCharacters = db.Queryable<Player_character>()
                    .Where(row => row.player_id == playerId)
                    .Select(row => row.character_id)
                    .ToList()
                    .ToHashSet();
                if (requestedSlots.Any(slot => !ownedCharacters.Contains(slot.CharacterId)))
                {
                    db.Ado.RollbackTran();
                    return new SaveFormationResult { Status = FormationStatus.CharacterNotOwned };
                }

                db.Ado.ExecuteCommand(
                    "DELETE FROM player_formation_slot " +
                    "WHERE player_id=@playerId AND module_id=@moduleId AND formation_id=@formationId",
                    KeyParameters(playerId, moduleId, formationId));
                foreach (FormationSlotValue slot in requestedSlots)
                {
                    db.Ado.ExecuteCommand(
                        "INSERT INTO player_formation_slot " +
                        "(player_id, module_id, formation_id, slot_index, character_id) " +
                        "VALUES (@playerId, @moduleId, @formationId, @slotIndex, @characterId)",
                        new SugarParameter("@playerId", playerId),
                        new SugarParameter("@moduleId", moduleId),
                        new SugarParameter("@formationId", formationId),
                        new SugarParameter("@slotIndex", slot.SlotIndex),
                        new SugarParameter("@characterId", slot.CharacterId));
                }
                afterSlotsWritten?.Invoke();

                long newVersion = checked(header.version + 1);
                int updated = db.Ado.ExecuteCommand(
                    "UPDATE player_formation SET version=@newVersion, updated_at=UTC_TIMESTAMP(6) " +
                    "WHERE player_id=@playerId AND module_id=@moduleId AND formation_id=@formationId " +
                    "AND version=@expectedVersion",
                    new SugarParameter("@newVersion", newVersion),
                    new SugarParameter("@playerId", playerId),
                    new SugarParameter("@moduleId", moduleId),
                    new SugarParameter("@formationId", formationId),
                    new SugarParameter("@expectedVersion", expectedVersion));
                if (updated != 1) throw new InvalidOperationException("Formation version update failed.");

                db.Ado.CommitTran();
                PlayerFormationSnapshot committed = Snapshot(
                    moduleId, formationId, newVersion, requestedSlots);
                PutFormation(playerId, committed);
                return new SaveFormationResult
                {
                    Status = FormationStatus.Success,
                    Formation = committed
                };
            }
            catch (Exception exception)
            {
                try { db.Ado.RollbackTran(); } catch (Exception rollbackException) { logger.Error(rollbackException); }
                logger.Error(exception,
                    $"Failed to save formation {moduleId}/{formationId} for player {playerId}.");
                InvalidateFormation(playerId);
                return new SaveFormationResult { Status = FormationStatus.Failed };
            }
        }

        private static void PutFormation(long playerId, PlayerFormationSnapshot formation)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity == null || !entity.uFormation.isLoaded) return;
            entity.uFormation.formations[(formation.ModuleId, formation.FormationId)] = formation;
        }

        private static void InvalidateFormation(long playerId)
        {
            GM_PlayerEntity entity = GM_EntityMgr.Instance.Get(playerId);
            if (entity != null) FormationComponent.Invalidate(entity);
        }

        public static bool Validate(int moduleId, int formationId, long expectedVersion,
            IReadOnlyCollection<FormationSlotValue> slots)
        {
            if (!FormationRules.TryGet(moduleId, out FormationModuleRule rule) ||
                formationId < 0 || formationId >= FormationRules.FormationCount ||
                expectedVersion < 0 || slots == null ||
                slots.Count < rule.MinDeployCount || slots.Count > rule.MaxDeployCount)
                return false;
            return slots.All(slot => slot != null && slot.CharacterId > 0 &&
                                      slot.SlotIndex >= 0 && slot.SlotIndex < rule.SlotCount) &&
                   slots.Select(slot => slot.SlotIndex).Distinct().Count() == slots.Count &&
                   slots.Select(slot => slot.CharacterId).Distinct().Count() == slots.Count;
        }

        private static List<FormationSlotValue> ReadSlots(SqlSugarClient db, long playerId,
            int moduleId, int formationId, bool forUpdate)
        {
            string sql = "SELECT player_id, module_id, formation_id, slot_index, character_id " +
                         "FROM player_formation_slot WHERE player_id=@playerId " +
                         "AND module_id=@moduleId AND formation_id=@formationId ORDER BY slot_index" +
                         (forUpdate ? " FOR UPDATE" : string.Empty);
            return db.Ado.SqlQuery<FormationSlotRow>(
                    sql, KeyParameters(playerId, moduleId, formationId))
                .Select(MapSlot).ToList();
        }

        private static PlayerFormationSnapshot Snapshot(int moduleId, int formationId,
            long version, IReadOnlyList<FormationSlotValue> slots) => new()
        {
            ModuleId = moduleId,
            FormationId = formationId,
            Version = version,
            Slots = slots.ToList()
        };

        private static FormationSlotValue MapSlot(FormationSlotRow row) => new()
        {
            SlotIndex = row.slot_index,
            CharacterId = row.character_id
        };

        private static bool SameSlots(IReadOnlyList<FormationSlotValue> left,
            IReadOnlyList<FormationSlotValue> right) =>
            left.Count == right.Count && left.Zip(right,
                (a, b) => a.SlotIndex == b.SlotIndex && a.CharacterId == b.CharacterId).All(equal => equal);

        private static SugarParameter[] KeyParameters(long playerId, int moduleId, int formationId) =>
            new[]
            {
                new SugarParameter("@playerId", playerId),
                new SugarParameter("@moduleId", moduleId),
                new SugarParameter("@formationId", formationId)
            };

        public sealed class FormationHeaderRow
        {
            public long player_id { get; set; }
            public int module_id { get; set; }
            public int formation_id { get; set; }
            public long version { get; set; }
        }

        public sealed class FormationSlotRow
        {
            public long player_id { get; set; }
            public int module_id { get; set; }
            public int formation_id { get; set; }
            public int slot_index { get; set; }
            public int character_id { get; set; }
        }
    }
}
