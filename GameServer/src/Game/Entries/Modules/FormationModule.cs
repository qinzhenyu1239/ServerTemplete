using Framework.Core.Net;
using Game.Core.EntityMgr;
using Game.Core.GM_Formation;
using Framework.Core.RateLimiting;
using Game.Datas.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entries.Modules
{
    public sealed class FormationModule
    {
        public static readonly FormationModule Instance = new FormationModule();
        private readonly TokenBucketRateLimiter<long> playerSaveLimiter =
            new(10, 3, TimeSpan.FromMinutes(10));
        private readonly TokenBucketRateLimiter<string> ipSaveLimiter =
            new(100, 30, TimeSpan.FromMinutes(10), StringComparer.Ordinal);
        private FormationModule() { }

        public ResGetPlayerFormations HandlerGet(IdSession session, ReqGetPlayerFormations request, long playerId)
        {
            if (!IsLoggedIn(playerId))
            {
                return new ResGetPlayerFormations
                {
                    status = (int)Respones.AccountIsNotLogin,
                    errorMessage = "Player is not logged in."
                };
            }
            FormationOverviewResult result = GM_FormationMgr.Instance.GetOverview(playerId);
            if (result.Status != FormationStatus.Success)
            {
                return new ResGetPlayerFormations
                {
                    status = (int)Respones.SystemErr,
                    errorMessage = "Failed to query player formations."
                };
            }
            var response = new ResGetPlayerFormations
            {
                status = (int)Respones.OK,
                moduleRules = result.ModuleRules.Select(MapRule).ToList(),
                formations = result.Formations.Select(MapFormation).ToList(),
                characters = result.Characters.Select(character => new OwnedCharacterBriefMessage
                {
                    characterId = character.CharacterId,
                    star = character.Star
                }).ToList()
            };
            return response;
        }
 
        public ResSavePlayerFormation HandlerSave(IdSession session, ReqSavePlayerFormation request, long playerId)
        {
            if (!IsLoggedIn(playerId))
                return Error(Respones.AccountIsNotLogin, "Player is not logged in.");
            if (request == null)    
                return Error(Respones.InvalidParams, "Invalid formation save request.");

            string clientIp = RequestRateLimiter.GetIp(session?.client);
            if ((!string.IsNullOrWhiteSpace(clientIp) && !ipSaveLimiter.TryAcquire(clientIp)) ||
                !playerSaveLimiter.TryAcquire(playerId))
                return Error(Respones.RequestRateLimited,
                    "Formation save requests are too frequent. Please retry shortly.");

            List<FormationSlotValue> slots = request.slots?.Select(slot => new FormationSlotValue
            {
                SlotIndex = slot?.slotIndex ?? -1,
                CharacterId = slot?.characterId ?? 0
            }).ToList();
            if (!GM_FormationMgr.Validate(
                    request.moduleId, request.formationId, request.expectedVersion, slots))
                return Error(Respones.InvalidParams, "Invalid formation save request.");

            SaveFormationResult result = GM_FormationMgr.Instance.Save(
                playerId, request.moduleId, request.formationId, request.expectedVersion, slots);
            ResSavePlayerFormation response = result.Status switch
            {
                FormationStatus.Success => new ResSavePlayerFormation
                {
                    status = (int)Respones.OK,
                    formation = MapFormation(result.Formation)
                },
                FormationStatus.CharacterNotOwned => Error(
                    Respones.CharacterNotOwned, "The formation contains a character the player does not own."),
                FormationStatus.VersionConflict => new ResSavePlayerFormation
                {
                    status = (int)Respones.FormationVersionConflict,
                    errorMessage = "The formation was changed by another request.",
                    formation = MapFormation(result.Formation)
                },
                FormationStatus.Invalid => Error(Respones.InvalidParams, "Invalid formation save request."),
                _ => Error(Respones.FormationSaveFailed, "Formation save failed and was rolled back.")
            };
            return response;
        }

        private static bool IsLoggedIn(long playerId) =>
            playerId > 0 && GM_AccountEntityMgr.Instance.GetPlayerById(playerId) != null;

        private static FormationModuleRuleMessage MapRule(FormationModuleRule rule) => new()
        {
            moduleId = rule.ModuleId,
            slotCount = rule.SlotCount,
            minDeployCount = rule.MinDeployCount,
            maxDeployCount = rule.MaxDeployCount
        };

        private static PlayerFormationMessage MapFormation(PlayerFormationSnapshot formation) =>
            formation == null ? null : new PlayerFormationMessage
            {
                moduleId = formation.ModuleId,
                formationId = formation.FormationId,
                version = formation.Version,
                slots = formation.Slots.Select(slot => new FormationSlotMessage
                {
                    slotIndex = slot.SlotIndex,
                    characterId = slot.CharacterId
                }).ToList()
            };

        private static ResSavePlayerFormation Error(Respones status, string message) => new()
        {
            status = (int)status,
            errorMessage = message
        };

    }
}
