using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Core.EntityMgr;
using Game.Core.GM_CharacterStarUp;
using Game.Datas.DBEntities;
using Game.Datas.Excels;
using Game.Datas.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Entries.Modules
{
    /// <summary>
    /// Validates character requests and owns the immutable character/star configuration.
    /// </summary>
    public sealed class CharacterStarUpModule
    {
        public static readonly CharacterStarUpModule Instance = new CharacterStarUpModule();
        private IReadOnlyDictionary<int, CharacterStarUpDefinition> definitions;

        private CharacterStarUpModule() { }

        public void Init()
        {
            definitions = BuildDefinitions(
                ExcelUtils.GetConfigDatas<Character>(nameof(Character)),
                ExcelUtils.GetConfigDatas<StarLevel>(nameof(StarLevel)),
                ExcelUtils.GetConfigDatas<Item>(nameof(Item)));
        }

        public ResGetCharacterInfo HandlerGetCharacterInfo(
            IdSession session, ReqGetCharacterInfo request, long playerId)
        {
            if (!IsLoggedIn(playerId))
                return new ResGetCharacterInfo
                {
                    status = (int)Respones.AccountIsNotLogin,
                    errorMessage = "Player is not logged in."
                };
            if (request == null || request.characterId <= 0)
                return new ResGetCharacterInfo
                {
                    status = (int)Respones.InvalidParams,
                    errorMessage = "Invalid character query."
                };
            if (definitions == null)
                return new ResGetCharacterInfo
                {
                    status = (int)Respones.CharacterConfigError,
                    errorMessage = "Character module is not initialized."
                };
            if (!definitions.TryGetValue(request.characterId, out CharacterStarUpDefinition definition))
                return new ResGetCharacterInfo
                {
                    status = (int)Respones.CharacterConfigError,
                    errorMessage = "Character configuration does not exist.",
                    characterId = request.characterId
                };

            return MapInfo(GM_CharacterStarUpMgr.Instance.GetCharacterInfo(playerId, definition));
        }

        public ResCharacterStarUp HandlerCharacterStarUp(
            IdSession session, ReqCharacterStarUp request, long playerId)
        {
            if (!IsLoggedIn(playerId))
                return ErrorStarUp(Respones.AccountIsNotLogin, "Player is not logged in.", request);
            if (request == null || request.characterId <= 0 || request.targetStar <= 0)
                return ErrorStarUp(Respones.InvalidParams, "Invalid character star-up request.", request);
            if (definitions == null)
                return ErrorStarUp(Respones.CharacterConfigError,
                    "Character module is not initialized.", request);
            if (!definitions.TryGetValue(request.characterId, out CharacterStarUpDefinition definition))
                return ErrorStarUp(Respones.CharacterConfigError,
                    "Character configuration does not exist.", request);
            if (request.targetStar > definition.MaxStar)
                return ErrorStarUp(Respones.InvalidParams,
                    "Target star exceeds the configured maximum.", request);

            return MapStarUp(GM_CharacterStarUpMgr.Instance.StarUp(
                playerId, definition, request.targetStar));
        }

        internal static IReadOnlyDictionary<int, CharacterStarUpDefinition> BuildDefinitions(
            IReadOnlyCollection<Character> characters,
            IReadOnlyCollection<StarLevel> starLevels,
            IReadOnlyCollection<Item> items,
            int expectedMaxStar = 61)
        {
            if (characters == null || characters.Count == 0)
                throw new InvalidOperationException("Character configuration is empty.");
            if (starLevels == null || starLevels.Count == 0)
                throw new InvalidOperationException("StarLevel configuration is empty.");

            var pieceCounts = new Dictionary<int, int>();
            foreach (StarLevel config in starLevels)
            {
                if (!int.TryParse(config?.Star?.Trim(), out int star) || star < 0)
                {
                    throw new InvalidOperationException(
                        $"StarLevel row {config?.ID ?? "<unknown>"} is invalid or duplicated.");
                }

                string pieceCountText = config?.PieceCount?.Trim();
                int cost = string.IsNullOrEmpty(pieceCountText)
                    ? 0
                    : int.TryParse(pieceCountText, out int parsedCost) ? parsedCost : -1;
                if (cost < 0 || !pieceCounts.TryAdd(star, cost))
                    throw new InvalidOperationException(
                        $"StarLevel row {config?.ID ?? "<unknown>"} is invalid or duplicated.");
            }

            int maxStar = pieceCounts.Keys.Max();
            if (maxStar != expectedMaxStar || pieceCounts.Count != expectedMaxStar + 1 ||
                Enumerable.Range(0, maxStar + 1).Any(star => !pieceCounts.ContainsKey(star)))
                throw new InvalidOperationException(
                    $"StarLevel must be continuous from star 0 through {expectedMaxStar}.");
            if (pieceCounts[0] != 1)
                throw new InvalidOperationException("StarLevel star 0 must cost exactly 1 piece.");
            if (pieceCounts.Any(pair => pair.Key > 0 && pair.Key < maxStar && pair.Value <= 0))
                throw new InvalidOperationException(
                    "Every non-maximum StarLevel row must have a positive next-star cost.");
            if (pieceCounts[maxStar] != 0)
                throw new InvalidOperationException(
                    "The maximum StarLevel row must have an empty or zero cost.");

            var itemIds = new HashSet<int>(items?.Select(item => item.id) ?? Enumerable.Empty<int>());
            var result = new Dictionary<int, CharacterStarUpDefinition>();
            foreach (Character config in characters)
            {
                if (!int.TryParse(config?.CardID?.Trim(), out int characterId) || characterId <= 0 ||
                    !int.TryParse(config?.PieceId?.Trim(), out int pieceItemId) || pieceItemId <= 0 ||
                    !itemIds.Contains(pieceItemId) || result.ContainsKey(characterId))
                {
                    throw new InvalidOperationException(
                        $"Character row {config?.ID ?? "<unknown>"} has an invalid CardID or PieceId.");
                }

                result.Add(characterId, new CharacterStarUpDefinition
                {
                    CharacterId = characterId,
                    PieceItemId = pieceItemId,
                    MaxStar = maxStar,
                    PieceCounts = pieceCounts
                });
            }
            return result;
        }

        private static bool IsLoggedIn(long playerId) =>
            playerId > 0 && GM_AccountEntityMgr.Instance.GetPlayerById(playerId) != null;

        private static ResGetCharacterInfo MapInfo(CharacterStarUpInfoResult result)
        {
            Respones response = result.Status switch
            {
                CharacterStarUpStatus.NotOwned => Respones.CharacterNotOwned,
                CharacterStarUpStatus.Failed => Respones.CharacterStarUpFailed,
                _ => Respones.OK
            };
            return new ResGetCharacterInfo
            {
                status = (int)response,
                errorMessage = response == Respones.OK ? null :
                    (response == Respones.CharacterNotOwned
                        ? "Player does not own this character."
                        : "Failed to query character information."),
                characterId = result.CharacterId,
                star = result.Star,
                pieceItemId = result.PieceItemId,
                pieceQuantity = result.PieceQuantity,
                nextStar = result.NextStar,
                nextPieceCount = result.NextPieceCount,
                isMaxStar = result.IsMaxStar
            };
        }

        private static ResCharacterStarUp MapStarUp(CharacterStarUpResult result)
        {
            Respones response = result.Status switch
            {
                CharacterStarUpStatus.NotOwned => Respones.CharacterNotOwned,
                CharacterStarUpStatus.FragmentNotEnough => Respones.CharacterFragmentNotEnough,
                CharacterStarUpStatus.TargetStarInvalid => Respones.CharacterTargetStarInvalid,
                CharacterStarUpStatus.Failed => Respones.CharacterStarUpFailed,
                _ => Respones.OK
            };
            string error = response switch
            {
                Respones.CharacterNotOwned => "Player does not own this character.",
                Respones.CharacterFragmentNotEnough => "Character fragments are not enough.",
                Respones.CharacterTargetStarInvalid => "Only the next configured star can be upgraded.",
                Respones.CharacterStarUpFailed => "Character star-up failed and was rolled back.",
                _ => null
            };
            return new ResCharacterStarUp
            {
                status = (int)response,
                errorMessage = error,
                characterId = result.CharacterId,
                requestedStar = result.RequestedStar,
                previousStar = result.PreviousStar,
                currentStar = result.CurrentStar,
                didUpgrade = result.DidUpgrade,
                consumedPieceCount = result.ConsumedPieceCount,
                pieceItemId = result.PieceItemId,
                remainingPieceQuantity = result.RemainingPieceQuantity,
                isMaxStar = result.IsMaxStar
            };
        }

        private static ResCharacterStarUp ErrorStarUp(
            Respones response, string message, ReqCharacterStarUp request) => new()
        {
            status = (int)response,
            errorMessage = message,
            characterId = request?.characterId ?? 0,
            requestedStar = request?.targetStar ?? 0
        };
    }
}
