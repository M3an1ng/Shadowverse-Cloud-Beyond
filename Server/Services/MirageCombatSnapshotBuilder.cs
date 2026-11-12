using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services
{
    public sealed record MirageCardSnapshot
    {
        public Guid OriginalInstanceId { get; init; }
        public int BaseDefinitionId { get; init; }
        public CardZone CurrentZone { get; init; }
        public int? ZoneIndex { get; init; }
        public CardMoveReason LastMoveReason { get; init; }
        public int HealthFloorFromBuffRemoval { get; init; }
        public long CombatContinuityVersion { get; init; }
        public bool IsLeader { get; init; }
        public CardInstance.CardRuntimeStateSnapshot RuntimeState { get; init; } = null!;
        public List<Guid> FusedCardIds { get; init; } = new();
    }

    public sealed record MirageAbilitySnapshot
    {
        public int Order { get; init; }
        public Guid OriginalRuntimeId { get; init; }
        public Guid? OriginalSourceCardId { get; init; }
        public Guid? OriginalSourceBuffId { get; init; }
        public JsonElement ResumeState { get; init; }
    }

    public sealed record MirageBuffSnapshot
    {
        public Guid OriginalBuffId { get; init; }
        public JsonElement SerializedBuff { get; init; }
    }

    public sealed record MirageCombatSideSnapshot
    {
        public Guid PlayerId { get; init; }
        public string? Name { get; init; }
        public CardClass Class { get; init; }
        public DeckEnvironment Environment { get; init; }
        public bool IsAI { get; init; }
        public int TurnNumber { get; init; }
        public int TavernTier { get; init; } = 1;
        public Dictionary<string, int> Counters { get; init; } = new();
        public Guid LeaderOriginalInstanceId { get; init; }
        public Dictionary<string, List<Guid>> ZoneCardIds { get; init; } = new();
        public List<MirageCardSnapshot> Cards { get; init; } = new();
        public List<MirageAbilitySnapshot> Abilities { get; init; } = new();
        public List<MirageBuffSnapshot> Buffs { get; init; } = new();
    }

    public interface IMirageCombatSnapshotBuilder
    {
        MirageCombatSideSnapshot CaptureSideSnapshot(GameState gameState, Guid playerId);

        (GameState SandboxGameState, HashSet<Guid> InitialSandboxBuffIds) PopulateSandboxState(
            MirageCombatSideSnapshot sideA,
            MirageCombatSideSnapshot sideB,
            int roundNumber,
            Guid firstAttackerPlayerId,
            IServiceProvider sandboxServices,
            AbilityManager sandboxAbilityManager,
            BuffManager sandboxBuffManager,
            AuraManager sandboxAuraManager);
    }

    public sealed class MirageCombatSnapshotBuilder : IMirageCombatSnapshotBuilder
    {
        private static readonly string[] SnapshotZones = ["Field", "Hand", "Emblem", "Graveyard", "Extra"];

        public static bool IsAuraGeneratedBuff(Buff buff, IAbilityManager? abilityManager)
        {
            if (buff == null)
            {
                return false;
            }

            if (buff.IsAuraGenerated)
            {
                return true;
            }

            if (buff.SourceAbilityId == Guid.Empty || abilityManager == null)
            {
                return false;
            }

            var sourceAbility = abilityManager.GetAbilityByRuntimeId(buff.SourceAbilityId);
            if (sourceAbility is Ability concreteAbility)
            {
                return concreteAbility.Type == AbilityType.Continuous
                    || concreteAbility.Config.Auras.Any(a => a != null && a.IsEnabled);
            }

            if (abilityManager.TryGetAbilitySnapshot(buff.SourceAbilityId, out var tombstone))
            {
                return tombstone.RemovalReason == "aura_unregistered";
            }

            return false;
        }

        public MirageCombatSideSnapshot CaptureSideSnapshot(GameState gameState, Guid playerId)
        {
            if (gameState == null) throw new ArgumentNullException(nameof(gameState));
            var player = gameState.GetPlayerState(playerId)
                ?? throw new ArgumentException($"Player {playerId} not found in game {gameState.GameId}.", nameof(playerId));

            var cardsById = new Dictionary<Guid, CardInstance>();
            void CollectCard(CardInstance? card)
            {
                if (card == null || !cardsById.TryAdd(card.InstanceId, card))
                {
                    return;
                }
                foreach (var fused in card.FusedCards)
                {
                    CollectCard(fused);
                }
            }

            CollectCard(player.Leader);
            var zoneCardIds = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);
            foreach (var zoneName in SnapshotZones)
            {
                var list = (List<CardInstance>)typeof(PlayerState).GetProperty(zoneName)!.GetValue(player)!;
                var ids = new List<Guid>(list.Count);
                foreach (var card in list)
                {
                    if (card == null) continue;
                    CollectCard(card);
                    ids.Add(card.InstanceId);
                }
                zoneCardIds[zoneName] = ids;
            }

            var cardSnapshots = cardsById.Values.Select(card =>
            {
                var rawSnapshot = card.CaptureRuntimeStateSnapshot() with { FusedCards = [] };
                return new MirageCardSnapshot
                {
                    OriginalInstanceId = card.InstanceId,
                    BaseDefinitionId = card.BaseDefinitionId,
                    CurrentZone = card.CurrentZone,
                    ZoneIndex = card.ZoneIndex,
                    LastMoveReason = card.LastMoveReason,
                    HealthFloorFromBuffRemoval = card.HealthFloorFromBuffRemoval,
                    CombatContinuityVersion = card.CombatContinuityVersion,
                    IsLeader = card.IsLeader || card.InstanceId == player.Leader.InstanceId,
                    RuntimeState = RemapRuntimeStateSnapshot(rawSnapshot, null, rawSnapshot.OriginInstanceId),
                    FusedCardIds = card.FusedCards.Select(c => c.InstanceId).ToList()
                };
            }).ToList();

            var auraBuffIds = new HashSet<Guid>();
            var buffSnapshots = new List<MirageBuffSnapshot>();
            var seenBuffIds = new HashSet<Guid>();

            foreach (var cardId in cardsById.Keys)
            {
                var buffs = gameState.BuffManager.GetBuffsForCard(cardId);
                foreach (var buff in buffs)
                {
                    if (buff == null || !seenBuffIds.Add(buff.Id))
                    {
                        continue;
                    }

                    if (IsAuraGeneratedBuff(buff, gameState.AbilityManager))
                    {
                        auraBuffIds.Add(buff.Id);
                        continue;
                    }

                    buffSnapshots.Add(new MirageBuffSnapshot
                    {
                        OriginalBuffId = buff.Id,
                        SerializedBuff = JsonSerializer.SerializeToElement(buff, ReplayExecutionState.Json)
                    });
                }
            }

            var abilitySnapshots = new List<MirageAbilitySnapshot>();
            var activeAbilities = ((AbilityManager)gameState.AbilityManager).GetActiveAbilities().OfType<Ability>().ToList();
            for (int i = 0; i < activeAbilities.Count; i++)
            {
                var ability = activeAbilities[i];
                if (!ability.SourceCardId.HasValue || !cardsById.ContainsKey(ability.SourceCardId.Value))
                {
                    continue;
                }

                if (ability.SourceBuffId.HasValue && auraBuffIds.Contains(ability.SourceBuffId.Value))
                {
                    continue;
                }

                abilitySnapshots.Add(new MirageAbilitySnapshot
                {
                    Order = i,
                    OriginalRuntimeId = ability.RuntimeId,
                    OriginalSourceCardId = ability.SourceCardId,
                    OriginalSourceBuffId = ability.SourceBuffId,
                    ResumeState = ability.CaptureResumeState()
                });
            }

            return new MirageCombatSideSnapshot
            {
                PlayerId = player.PlayerId,
                Name = player.DisplayName,
                Class = player.Class,
                Environment = player.Environment,
                IsAI = player.IsAI,
                TurnNumber = player.TurnNumber,
                TavernTier = player.Mirage?.TavernTier ?? 1,
                Counters = new Dictionary<string, int>(player.CounterMap, StringComparer.OrdinalIgnoreCase),
                LeaderOriginalInstanceId = player.Leader.InstanceId,
                ZoneCardIds = zoneCardIds,
                Cards = cardSnapshots,
                Abilities = abilitySnapshots,
                Buffs = buffSnapshots
            };
        }

        public (GameState SandboxGameState, HashSet<Guid> InitialSandboxBuffIds) PopulateSandboxState(
            MirageCombatSideSnapshot sideA,
            MirageCombatSideSnapshot sideB,
            int roundNumber,
            Guid firstAttackerPlayerId,
            IServiceProvider sandboxServices,
            AbilityManager sandboxAbilityManager,
            BuffManager sandboxBuffManager,
            AuraManager sandboxAuraManager)
        {
            if (sideA == null) throw new ArgumentNullException(nameof(sideA));
            if (sideB == null) throw new ArgumentNullException(nameof(sideB));

            // Build per-side ID maps so that even if sideB is a Ghost snapshot sharing original IDs with sideA,
            // both sides receive completely disjoint sandbox IDs while preserving cross-side references when distinct.
            var (cardIdMapA, allIdMapA, cardIdMapB, allIdMapB) = BuildPerSideIdMaps(sideA, sideB);

            var sandboxCards = new Dictionary<Guid, CardInstance>();
            var playerA = CreateSandboxPlayer(sideA, cardIdMapA, allIdMapA, sandboxCards);
            var playerB = CreateSandboxPlayer(sideB, cardIdMapB, allIdMapB, sandboxCards);

            var sandboxGameId = $"mirage-sandbox-{Guid.NewGuid():N}";
            var sandboxGame = new GameState(sandboxGameId, playerA, playerB, firstAttackerPlayerId)
            {
                Status = GameStatus.InProgress,
                Phase = GamePhase.AutoCombat,
                Mode = GameMode.Mirage,
                IsCombatSandbox = true,
                CurrentPlayerId = firstAttackerPlayerId,
                Mirage = new MirageMatchState
                {
                    RoundNumber = Math.Max(1, roundNumber)
                },
                AbilityManager = sandboxAbilityManager,
                BuffManager = sandboxBuffManager,
                AuraManager = sandboxAuraManager
            };

            sandboxGame.InGameIds.GetOrCreate(InGameIdKind.Player, playerA.PlayerId);
            sandboxGame.InGameIds.GetOrCreate(InGameIdKind.Player, playerB.PlayerId);

            foreach (var card in sandboxCards.Values)
            {
                sandboxGame.RegisterCardInstance(card);
                sandboxGame.InGameIds.GetOrCreate(InGameIdKind.Card, card.InstanceId);
            }

            foreach (var (side, sideAllIdMap) in new[] { (sideA, allIdMapA), (sideB, allIdMapB) })
            {
                foreach (var abilitySnap in side.Abilities.OrderBy(a => a.Order))
                {
                    var remappedNode = ReplayExecutionState.Remap(JsonNode.Parse(abilitySnap.ResumeState.GetRawText())!, sideAllIdMap);
                    var remappedElement = JsonSerializer.SerializeToElement(remappedNode, ReplayExecutionState.Json);
                    var restoredAbility = ReplayExecutionState.RestoreAbility(remappedElement, sandboxCards, sandboxServices);
                    sandboxGame.AbilityManager.RegisterAbility(restoredAbility);
                    sandboxGame.InGameIds.GetOrCreate(InGameIdKind.Ability, restoredAbility.RuntimeId);
                }
            }

            var initialSandboxBuffIds = new HashSet<Guid>();
            foreach (var (side, sideAllIdMap) in new[] { (sideA, allIdMapA), (sideB, allIdMapB) })
            {
                foreach (var buffSnap in side.Buffs)
                {
                    var remappedNode = ReplayExecutionState.Remap(JsonNode.Parse(buffSnap.SerializedBuff.GetRawText())!, sideAllIdMap);
                    var remappedElement = JsonSerializer.SerializeToElement(remappedNode, ReplayExecutionState.Json);
                    var restoredBuff = ReplayExecutionState.RestoreBuff(remappedElement);
                    sandboxBuffManager.RestoreBuffInstanceWithoutCreatingAbilities(restoredBuff, sandboxGame);
                    initialSandboxBuffIds.Add(restoredBuff.Id);
                }
            }

            return (sandboxGame, initialSandboxBuffIds);
        }

        private static (
            Dictionary<Guid, Guid> CardIdMapA,
            Dictionary<Guid, Guid> AllIdMapA,
            Dictionary<Guid, Guid> CardIdMapB,
            Dictionary<Guid, Guid> AllIdMapB) BuildPerSideIdMaps(
            MirageCombatSideSnapshot sideA,
            MirageCombatSideSnapshot sideB)
        {
            static (Dictionary<Guid, Guid> CardMap, Dictionary<Guid, Guid> OwnMap, List<Guid> ExternalRefs) BuildOwnMaps(MirageCombatSideSnapshot side)
            {
                var cardMap = new Dictionary<Guid, Guid>();
                var ownMap = new Dictionary<Guid, Guid>();
                var externalRefs = new List<Guid>();

                void MapOwned(Dictionary<Guid, Guid>? specific, Guid id)
                {
                    if (id == Guid.Empty) return;
                    if (!ownMap.TryGetValue(id, out var mapped))
                    {
                        mapped = Guid.NewGuid();
                        ownMap[id] = mapped;
                    }
                    specific?.TryAdd(id, mapped);
                }

                MapOwned(cardMap, side.LeaderOriginalInstanceId);
                foreach (var card in side.Cards)
                {
                    MapOwned(cardMap, card.OriginalInstanceId);
                    foreach (var fusedId in card.FusedCardIds)
                    {
                        MapOwned(cardMap, fusedId);
                    }
                }
                foreach (var ability in side.Abilities)
                {
                    MapOwned(null, ability.OriginalRuntimeId);
                    if (ability.OriginalSourceCardId.HasValue) externalRefs.Add(ability.OriginalSourceCardId.Value);
                    if (ability.OriginalSourceBuffId.HasValue) externalRefs.Add(ability.OriginalSourceBuffId.Value);
                    if (ability.ResumeState.TryGetProperty("summaryRootAbilityId", out var rootProp)
                        && rootProp.ValueKind == JsonValueKind.String
                        && Guid.TryParse(rootProp.GetString(), out var rootId) && rootId != Guid.Empty)
                    {
                        externalRefs.Add(rootId);
                    }
                }
                foreach (var buff in side.Buffs)
                {
                    MapOwned(null, buff.OriginalBuffId);
                    if (buff.SerializedBuff.TryGetProperty("sourceAbilityId", out var saProp)
                        && saProp.ValueKind == JsonValueKind.String
                        && Guid.TryParse(saProp.GetString(), out var saId) && saId != Guid.Empty)
                    {
                        externalRefs.Add(saId);
                    }
                    if (buff.SerializedBuff.TryGetProperty("summaryRootAbilityId", out var srProp)
                        && srProp.ValueKind == JsonValueKind.String
                        && Guid.TryParse(srProp.GetString(), out var srId) && srId != Guid.Empty)
                    {
                        externalRefs.Add(srId);
                    }
                    if (buff.SerializedBuff.TryGetProperty("sourceCardInstanceId", out var scProp)
                        && scProp.ValueKind == JsonValueKind.String
                        && Guid.TryParse(scProp.GetString(), out var scId) && scId != Guid.Empty)
                    {
                        externalRefs.Add(scId);
                    }
                }

                return (cardMap, ownMap, externalRefs);
            }

            var (cardMapA, ownMapA, refsA) = BuildOwnMaps(sideA);
            var (cardMapB, ownMapB, refsB) = BuildOwnMaps(sideB);

            var allMapA = new Dictionary<Guid, Guid>(ownMapA);
            var allMapB = new Dictionary<Guid, Guid>(ownMapB);

            foreach (var extId in refsA)
            {
                if (allMapA.ContainsKey(extId)) continue;
                allMapA[extId] = ownMapB.TryGetValue(extId, out var fromB) ? fromB : Guid.NewGuid();
            }

            foreach (var extId in refsB)
            {
                if (allMapB.ContainsKey(extId)) continue;
                allMapB[extId] = ownMapA.TryGetValue(extId, out var fromA) ? fromA : Guid.NewGuid();
            }

            return (cardMapA, allMapA, cardMapB, allMapB);
        }

        private static PlayerState CreateSandboxPlayer(
            MirageCombatSideSnapshot side,
            IReadOnlyDictionary<Guid, Guid> cardIdMap,
            IReadOnlyDictionary<Guid, Guid> allIdMap,
            Dictionary<Guid, CardInstance> sandboxCards)
        {
            var localCardsByOldId = new Dictionary<Guid, CardInstance>();
            foreach (var cardSnap in side.Cards)
            {
                if (GameDataProvider.Instance is { IsInitialized: true } provider)
                {
                    var baseDef = provider.GetCardDefinition(cardSnap.BaseDefinitionId);
                    if (baseDef == null || !baseDef.IsMirageCard)
                    {
                        throw new InvalidOperationException(
                            $"Mirage combat sandbox cannot restore non-Mirage card definition {cardSnap.BaseDefinitionId}.");
                    }
                }

                var newCardId = cardIdMap[cardSnap.OriginalInstanceId];
                var card = new CardInstance(newCardId, cardSnap.BaseDefinitionId, side.PlayerId)
                {
                    CurrentZone = cardSnap.CurrentZone,
                    ZoneIndex = cardSnap.ZoneIndex
                };

                var originId = cardSnap.IsLeader
                    ? (Guid?)null
                    : (cardSnap.RuntimeState.OriginInstanceId ?? cardSnap.OriginalInstanceId);
                var remappedRuntimeState = RemapRuntimeStateSnapshot(cardSnap.RuntimeState, allIdMap, originId);
                card.RestoreResumeState(remappedRuntimeState, cardSnap.HealthFloorFromBuffRemoval, cardSnap.CombatContinuityVersion);
                card.SetLastMoveReason(cardSnap.LastMoveReason);

                if (cardSnap.IsLeader)
                {
                    card.BaseHealthOverride = 99999;
                    card.Damage = 0;
                    card.IsCombatSandboxLeader = true;
                }

                localCardsByOldId[cardSnap.OriginalInstanceId] = card;
                sandboxCards[newCardId] = card;
            }

            foreach (var cardSnap in side.Cards)
            {
                var parent = localCardsByOldId[cardSnap.OriginalInstanceId];
                foreach (var fusedOldId in cardSnap.FusedCardIds)
                {
                    if (localCardsByOldId.TryGetValue(fusedOldId, out var fusedChild))
                    {
                        parent.AddFusedCard(fusedChild);
                    }
                }
            }

            var leader = localCardsByOldId[side.LeaderOriginalInstanceId];
            var player = new PlayerState(side.PlayerId)
            {
                Leader = leader,
                IsAI = side.IsAI,
                DisplayName = side.Name ?? string.Empty,
                Class = side.Class,
                Environment = side.Environment,
                TurnNumber = Math.Max(1, side.TurnNumber),
                Mirage = new MiragePlayerState
                {
                    TavernTier = Math.Max(1, side.TavernTier)
                }
            };

            foreach (var counter in side.Counters)
            {
                var remappedKey = RemapStringText(counter.Key, allIdMap);
                player.Counters.Set(remappedKey, counter.Value);
            }

            foreach (var zoneName in SnapshotZones)
            {
                if (!side.ZoneCardIds.TryGetValue(zoneName, out var oldIds))
                {
                    continue;
                }

                var restoredZone = new List<CardInstance>(oldIds.Count);
                for (int i = 0; i < oldIds.Count; i++)
                {
                    if (localCardsByOldId.TryGetValue(oldIds[i], out var c))
                    {
                        c.ZoneIndex = i;
                        restoredZone.Add(c);
                    }
                }
                typeof(PlayerState).GetProperty(zoneName)!.SetValue(player, restoredZone);
            }

            return player;
        }

        private static CardInstance.CardRuntimeStateSnapshot RemapRuntimeStateSnapshot(
            CardInstance.CardRuntimeStateSnapshot source,
            IReadOnlyDictionary<Guid, Guid>? idMap,
            Guid? originInstanceId)
        {
            var remappedAbilityState = new Dictionary<string, object>(StringComparer.Ordinal);
            if (source.AbilityState != null)
            {
                foreach (var pair in source.AbilityState)
                {
                    var key = RemapStringText(pair.Key, idMap);
                    remappedAbilityState[key] = DeepCloneAndRemapStateValue(pair.Value, idMap)!;
                }
            }

            var remappedCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (source.Counters != null)
            {
                foreach (var pair in source.Counters)
                {
                    remappedCounters[RemapStringText(pair.Key, idMap)] = pair.Value;
                }
            }

            return source with
            {
                OriginInstanceId = originInstanceId,
                ActionUsageCounts = source.ActionUsageCounts != null
                    ? new Dictionary<CardActionUsageType, int>(source.ActionUsageCounts)
                    : new Dictionary<CardActionUsageType, int>(),
                ActionUsageLimits = source.ActionUsageLimits != null
                    ? new Dictionary<CardActionUsageType, int>(source.ActionUsageLimits)
                    : new Dictionary<CardActionUsageType, int>(),
                AbilityState = remappedAbilityState,
                AdditionalNames = source.AdditionalNames != null
                    ? new List<string>(source.AdditionalNames)
                    : new List<string>(),
                AdditionalTribes = source.AdditionalTribes != null
                    ? new List<string>(source.AdditionalTribes)
                    : new List<string>(),
                FusedCards = new List<CardInstance>(),
                Counters = remappedCounters
            };
        }

        private static object? DeepCloneAndRemapStateValue(object? value, IReadOnlyDictionary<Guid, Guid>? idMap)
        {
            switch (value)
            {
                case null:
                    return null;
                case Guid guid:
                    return idMap != null && idMap.TryGetValue(guid, out var mappedGuid) ? mappedGuid : guid;
                case string text:
                    return RemapStringText(text, idMap);
                case bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or DateTime or DateTimeOffset or Enum:
                    return value;
                case JsonElement jsonElement:
                    switch (jsonElement.ValueKind)
                    {
                        case JsonValueKind.True:
                            return true;
                        case JsonValueKind.False:
                            return false;
                        case JsonValueKind.Null:
                        case JsonValueKind.Undefined:
                            return null;
                        case JsonValueKind.Number:
                            if (jsonElement.TryGetInt32(out var intVal)) return intVal;
                            if (jsonElement.TryGetInt64(out var longVal)) return longVal;
                            if (jsonElement.TryGetDouble(out var doubleVal)) return doubleVal;
                            break;
                        case JsonValueKind.String:
                            return RemapStringText(jsonElement.GetString() ?? string.Empty, idMap);
                    }
                    if (idMap == null || idMap.Count == 0)
                    {
                        return jsonElement.Clone();
                    }
                    var parsedNode = JsonNode.Parse(jsonElement.GetRawText());
                    if (parsedNode == null)
                    {
                        return jsonElement.Clone();
                    }
                    var remappedNode = ReplayExecutionState.Remap(parsedNode, idMap);
                    return JsonSerializer.SerializeToElement(remappedNode, ReplayExecutionState.Json);
                case JsonNode jsonNode:
                    return idMap == null || idMap.Count == 0
                        ? jsonNode.DeepClone()
                        : ReplayExecutionState.Remap(jsonNode, idMap);
                case IReadOnlyDictionary<string, object> readOnlyStringDict:
                {
                    var copy = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var pair in readOnlyStringDict)
                    {
                        copy[RemapStringText(pair.Key, idMap)] = DeepCloneAndRemapStateValue(pair.Value, idMap)!;
                    }
                    return copy;
                }
                case IDictionary untypedDict:
                {
                    var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (DictionaryEntry entry in untypedDict)
                    {
                        var key = RemapStringText(entry.Key?.ToString() ?? string.Empty, idMap);
                        copy[key] = DeepCloneAndRemapStateValue(entry.Value, idMap);
                    }
                    return copy;
                }
                case HashSet<Guid> guidSet:
                    return guidSet.Select(g => idMap != null && idMap.TryGetValue(g, out var mg) ? mg : g).ToHashSet();
                case HashSet<string> stringSet:
                    return new HashSet<string>(stringSet.Select(s => RemapStringText(s, idMap)), stringSet.Comparer);
                case HashSet<int> intSet:
                    return new HashSet<int>(intSet);
                case List<Guid> guidList:
                    return guidList.Select(g => idMap != null && idMap.TryGetValue(g, out var mg) ? mg : g).ToList();
                case List<string> stringList:
                    return stringList.Select(s => RemapStringText(s, idMap)).ToList();
                case List<int> intList:
                    return new List<int>(intList);
                case IEnumerable enumerable:
                    return enumerable.Cast<object?>().Select(item => DeepCloneAndRemapStateValue(item, idMap)).ToList();
                case ICloneable cloneable:
                    return cloneable.Clone();
                default:
                    return value;
            }
        }

        private static string RemapStringText(string text, IReadOnlyDictionary<Guid, Guid>? idMap)
        {
            if (string.IsNullOrEmpty(text) || idMap == null || idMap.Count == 0)
            {
                return text;
            }

            if (Guid.TryParse(text, out var parsed) && idMap.TryGetValue(parsed, out var directMapped))
            {
                return directMapped.ToString();
            }

            var current = text;
            foreach (var pair in idMap)
            {
                current = current
                    .Replace(pair.Key.ToString("D"), pair.Value.ToString("D"), StringComparison.OrdinalIgnoreCase)
                    .Replace(pair.Key.ToString("N"), pair.Value.ToString("N"), StringComparison.OrdinalIgnoreCase);
            }
            return current;
        }
    }
}
