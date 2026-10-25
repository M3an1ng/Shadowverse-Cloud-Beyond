using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum
using System;
using System.Collections.Generic; // Required for List
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Services;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Events;


namespace ShadowverseBeyond.Server.Mappings
{
    public static class CardInstanceProjection
    {
        private const string FusionCountCounterKey = "fusion_count";
        private const string FusionCountDisplayName = "融合数";

        public static CardInstanceDto Create(CardInstance source, GameContext? context)
        {
            var definition = source.CurrentDefinition;
            var maxHealth = source.GetMaxHealth(context);
            return new CardInstanceDto
            {
                InstanceId = source.InstanceId,
                CardId = source.CurrentDefinitionId,
                Name = source.Name,
                CardNames = source.CardNames,
                Description = source.Description,
                ArtPreview = definition?.ArtPreview,
                ArtDetail = definition?.ArtDetail,
                Rarity = source.Rarity,
                Type = source.CardType,
                Class = source.Class,
                Tribe = source.Tribe,
                Earthsigil = source.Earthsigil,
                Counters = BuildCounterSnapshot(source),
                CounterDisplayNames = BuildCounterDisplayNameSnapshot(source),
                CounterSummaryEntries = BuildCounterSummaryEntries(source),
                Attack = source.BaseAttack,
                Health = source.BaseHealth,
                Cost = source.BaseCost,
                CurrentCost = source.GetCurrentCost(context),
                CurrentAttack = source.GetCurrentAttack(context),
                CurrentHealth = maxHealth - source.Damage,
                MaxHealth = maxHealth,
                IsEvolved = source.IsEvolved,
                IsSuperEvolved = source.IsSuperEvolved,
                CanAttack = source.CanAttackIn(context),
                ActAbilities = BuildActionSummaries(source, AbilityKeyword.Act, context),
                ActsThisTurn = source.ActsThisTurn,
                MaxActsPerTurn = source.GetActionUsageLimit(CardActionUsageType.Act, context),
                FusionAbilities = BuildActionSummaries(source, AbilityKeyword.Fusion, context),
                FusedCards = source.FusedCards.Select(card => Create(card, context)).ToList(),
                FusionsThisTurn = source.FusionsThisTurn,
                MaxFusionsPerTurn = source.GetActionUsageLimit(CardActionUsageType.Fusion, context),
                AttacksThisTurn = source.AttacksThisTurn,
                ZoneStayTurn = source.ZoneStayTurn,
                CurrentCountdown = source.CurrentCountdown,
                MaxAttacksPerTurn = source.GetActionUsageLimit(CardActionUsageType.Attack, context),
                Zone = source.CurrentZone,
                OwnerPlayerId = source.OwnerPlayerId,
                Keywords = source.GetKeywords(context).ToList(),
                BuffSummaries = BuildBuffSummaries(source, context),
                BuffStatSummary = BuildBuffStatSummary(source, context),
                CanEvolve = source.CanEvolve(),
                ZoneIndex = source.ZoneIndex,
            };
        }

        private static Dictionary<string, int> BuildCounterSnapshot(CardInstance cardInstance)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in cardInstance.CounterMap)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }
                result[pair.Key] = Math.Max(0, pair.Value);
            }

            var fusedCount = cardInstance.FusedCards.Count;
            if (fusedCount > 0)
            {
                result[FusionCountCounterKey] = fusedCount;
            }

            return result;
        }

        private static Dictionary<string, string> BuildCounterDisplayNameSnapshot(CardInstance cardInstance)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var abilities = cardInstance.Abilities;
            foreach (var ability in abilities)
            {
                var registrations = ability?.Config?.CounterRegistrations;
                if (registrations == null || registrations.Count == 0)
                {
                    registrations = new List<CounterRegistrationConfig>();
                }

                foreach (var registration in registrations)
                {
                    if (registration == null || string.IsNullOrWhiteSpace(registration.Name))
                    {
                        continue;
                    }

                    var name = registration.Name.Trim();
                    if (result.ContainsKey(name))
                    {
                        continue;
                    }

                    var displayName = string.IsNullOrWhiteSpace(registration.DisplayName)
                        ? name
                        : registration.DisplayName.Trim();
                    result[name] = displayName;
                }

                var playerRegistrations = ability?.Config?.PlayerCounterRegistrations;
                if (playerRegistrations == null || playerRegistrations.Count == 0)
                {
                    continue;
                }

                foreach (var registration in playerRegistrations)
                {
                    if (registration == null || string.IsNullOrWhiteSpace(registration.Name))
                    {
                        continue;
                    }

                    var name = registration.Name.Trim();
                    if (result.ContainsKey(name))
                    {
                        continue;
                    }

                    var displayName = string.IsNullOrWhiteSpace(registration.DisplayName)
                        ? name
                        : registration.DisplayName.Trim();
                    result[name] = displayName;
                }
            }

            var fusedCount = cardInstance.FusedCards.Count;
            if (fusedCount > 0 && !result.ContainsKey(FusionCountCounterKey))
            {
                result[FusionCountCounterKey] = FusionCountDisplayName;
            }

            return result;
        }

        private static List<CounterSummaryEntryDto> BuildCounterSummaryEntries(CardInstance cardInstance)
        {
            var entries = new List<CounterSummaryEntryDto>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var abilities = cardInstance.Abilities;

            foreach (var ability in abilities)
            {
                var registrations = ability?.Config?.CounterRegistrations;
                if (registrations == null || registrations.Count == 0)
                {
                    continue;
                }

                foreach (var registration in registrations)
                {
                    if (registration == null || string.IsNullOrWhiteSpace(registration.Name))
                    {
                        continue;
                    }

                    AddCounterSummaryEntry(
                        entries,
                        seenKeys,
                        registration.Name,
                        registration.DisplayName,
                        cardInstance.GetCounter(registration.Name));
                }
            }

            foreach (var pair in cardInstance.CounterMap)
            {
                AddCounterSummaryEntry(entries, seenKeys, pair.Key, pair.Key, pair.Value);
            }

            var fusedCount = cardInstance.FusedCards.Count;
            if (fusedCount > 0)
            {
                AddCounterSummaryEntry(entries, seenKeys, FusionCountCounterKey, FusionCountDisplayName, fusedCount);
            }

            return entries;
        }

        private static void AddCounterSummaryEntry(
            ICollection<CounterSummaryEntryDto> entries,
            ISet<string> seenKeys,
            string? rawKey,
            string? rawLabel,
            int rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                return;
            }

            var key = rawKey.Trim();
            if (!seenKeys.Add(key))
            {
                return;
            }

            var label = string.IsNullOrWhiteSpace(rawLabel)
                ? key
                : rawLabel.Trim();
            var value = Math.Max(0, rawValue);
            if (value == 0)
            {
                return;
            }

            entries.Add(new CounterSummaryEntryDto
            {
                Key = key,
                Label = label,
                Value = value
            });
        }

        private static List<AbilityActionSummaryDto> BuildActionSummaries(CardInstance cardInstance, AbilityKeyword keyword, GameContext? context)
        {
            IEnumerable<IAbility> abilities = context == null
                ? cardInstance.Abilities
                : context.AbilityManager.GetEffectiveAbilitiesForCard(cardInstance.InstanceId, context);

            return abilities
                .Where(ability => ability.HasKeyword(keyword))
                .Select(ability => new AbilityActionSummaryDto
                {
                    RuntimeId = ability.RuntimeId,
                    Name = ability.Name ?? string.Empty,
                    Description = (ability as Ability)?.Description ?? string.Empty
                })
                .ToList();
        }

        private static List<BuffSummaryDto> BuildBuffSummaries(CardInstance cardInstance, GameContext? context)
        {
            if (context == null)
            {
                return new List<BuffSummaryDto>();
            }

            var buffs = GetVisibleBuffs(cardInstance, context, context.BuffManager);
            if (buffs.Count == 0)
            {
                return new List<BuffSummaryDto>();
            }

            var logger = GameServiceProvider.Logger;

            static string BuildDisplayGroupKey(Buff buff)
            {
                if (buff.SummaryRootAbilityId.HasValue && buff.SummaryRootAbilityId.Value != Guid.Empty)
                {
                    return $"ability:{buff.SummaryRootAbilityId.Value:N}";
                }

                if (buff.SourceAbilityId != Guid.Empty)
                {
                    return $"ability:{buff.SourceAbilityId:N}";
                }

                if (!string.IsNullOrWhiteSpace(buff.EffectKey))
                {
                    return $"effect:{buff.EffectKey}";
                }

                return $"buff:{buff.Id:N}";
            }

            static string FormatBuff(Buff buff)
            {
                return $"BuffId={buff.Id} EffectKey={buff.EffectKey ?? "<null>"} SourceAbilityId={buff.SourceAbilityId} " +
                    $"SummaryRootAbilityId={buff.SummaryRootAbilityId?.ToString() ?? "<null>"} " +
                    $"SourceEffectIndex={buff.SourceEffectIndex?.ToString() ?? "<null>"} SourceCardId={buff.SourceCardInstanceId?.ToString() ?? "<null>"} " +
                    $"SourceCardName={buff.SourceCardName ?? "<null>"} SourceAbilityName={buff.SourceAbilityName ?? "<null>"} " +
                    $"DefinitionId={buff.DefinitionId ?? "<null>"} Duration={buff.DurationType} Active={buff.IsActive}";
            }

            var abilityManager = context.AbilityManager;
            var gameState = context.CurrentGameState;

            var summaries = new List<BuffSummaryDto>();

            var groupedBuffs = buffs
                .GroupBy(BuildDisplayGroupKey, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToList();

            foreach (var group in groupedBuffs)
            {
                var sample = group.First();

                var preferredAbilityId = sample.SummaryRootAbilityId.HasValue && sample.SummaryRootAbilityId.Value != Guid.Empty
                    ? sample.SummaryRootAbilityId.Value
                    : sample.SourceAbilityId;

                IAbility? ability = null;
                AbilitySnapshot? abilitySnapshot = null;
                if (preferredAbilityId != Guid.Empty)
                {
                    ability = abilityManager.GetAbilityByRuntimeId(preferredAbilityId);
                    if (ability == null && abilityManager.TryGetAbilitySnapshot(preferredAbilityId, out var preferredSnapshot))
                    {
                        abilitySnapshot = preferredSnapshot;
                    }
                }

                if (ability == null && abilitySnapshot == null && sample.SourceAbilityId != Guid.Empty && sample.SourceAbilityId != preferredAbilityId)
                {
                    ability = abilityManager.GetAbilityByRuntimeId(sample.SourceAbilityId);
                    if (ability == null && abilityManager.TryGetAbilitySnapshot(sample.SourceAbilityId, out var sourceSnapshot))
                    {
                        abilitySnapshot = sourceSnapshot;
                    }
                }

                if (ability == null)
                {
                    logger?.LogInformation(
                        "[BuffSummary] Ability not found for buff {BuffId} (DisplayKey {DisplayKey}) on card {CardId}. PreferredAbilityId={PreferredAbilityId} SourceAbilityId={SourceAbilityId} CachedSourceCardName={CachedSourceCardName} CachedSourceAbilityName={CachedSourceAbilityName} TombstoneAbilityName={TombstoneAbilityName} TombstoneCardName={TombstoneCardName}",
                        sample.Id,
                        group.Key,
                        cardInstance.InstanceId,
                        preferredAbilityId == Guid.Empty ? "<empty>" : preferredAbilityId.ToString(),
                        sample.SourceAbilityId,
                        sample.SourceCardName ?? "<null>",
                        sample.SourceAbilityName ?? "<null>",
                        abilitySnapshot?.AbilityName ?? "<null>",
                        abilitySnapshot?.SourceCardName ?? "<null>");
                }
                var sourceCardName = ability?.SourceCard?.Name;
                if (string.IsNullOrWhiteSpace(sourceCardName) && ability?.SourceCardId is Guid sourceCardId)
                {
                    sourceCardName = gameState.FindCardInstance(sourceCardId)?.Name;
                }
                if (string.IsNullOrWhiteSpace(sourceCardName))
                {
                    sourceCardName = abilitySnapshot?.SourceCardName;
                }
                if (string.IsNullOrWhiteSpace(sourceCardName) && abilitySnapshot?.SourceCardId is Guid snapshotCardId)
                {
                    sourceCardName = gameState.FindCardInstance(snapshotCardId)?.Name;
                }
                if (string.IsNullOrWhiteSpace(sourceCardName))
                {
                    sourceCardName = sample.SourceCardName;
                }
                if (string.IsNullOrWhiteSpace(sourceCardName) && sample.SourceCardInstanceId is Guid cachedSourceCardId)
                {
                    sourceCardName = gameState.FindCardInstance(cachedSourceCardId)?.Name;
                }

                var sourceCardArtPreview = ability?.SourceCard?.CurrentDefinition?.ArtPreview;
                if (string.IsNullOrWhiteSpace(sourceCardArtPreview) && ability?.SourceCardId is Guid artSourceCardId)
                {
                    sourceCardArtPreview = gameState.FindCardInstance(artSourceCardId)?.CurrentDefinition?.ArtPreview;
                }
                if (string.IsNullOrWhiteSpace(sourceCardArtPreview) && abilitySnapshot?.SourceCardId is Guid snapshotArtCardId)
                {
                    sourceCardArtPreview = gameState.FindCardInstance(snapshotArtCardId)?.CurrentDefinition?.ArtPreview;
                }
                if (string.IsNullOrWhiteSpace(sourceCardArtPreview) && sample.SourceCardInstanceId is Guid cachedArtSourceCardId)
                {
                    sourceCardArtPreview = gameState.FindCardInstance(cachedArtSourceCardId)?.CurrentDefinition?.ArtPreview;
                }

                var sourceAbilityName = ability?.Name ?? abilitySnapshot?.AbilityName ?? sample.SourceAbilityName;

                if (string.IsNullOrWhiteSpace(sourceCardName) || string.IsNullOrWhiteSpace(sourceAbilityName))
                {
                    logger?.LogInformation(
                        "[BuffSummary] Missing source names for buff {BuffId} (DisplayKey {DisplayKey}) on card {CardId}. SourceCardName={SourceCardName} SourceAbilityName={SourceAbilityName} CachedSourceCardName={CachedSourceCardName} CachedSourceAbilityName={CachedSourceAbilityName}. Details: {BuffDetails}",
                        sample.Id,
                        group.Key,
                        cardInstance.InstanceId,
                        sourceCardName ?? "<null>",
                        sourceAbilityName ?? "<null>",
                        sample.SourceCardName ?? "<null>",
                        sample.SourceAbilityName ?? "<null>",
                        string.Join(" | ", group.Select(FormatBuff)));
                    continue;
                }

                summaries.Add(new BuffSummaryDto
                {
                    EffectKey = group.Key,
                    SourceCardName = sourceCardName,
                    SourceCardArtPreview = sourceCardArtPreview ?? string.Empty,
                    SourceAbilityName = sourceAbilityName
                });

                logger?.LogDebug(
                    "BuffSummary built: Card {CardId} DisplayKey {DisplayKey} SourceCard {SourceCardName} SourceAbility {SourceAbilityName}.",
                    cardInstance.InstanceId,
                    group.Key,
                    sourceCardName,
                    sourceAbilityName);
            }

            if (summaries.Count == 0)
            {
                logger?.LogInformation(
                    "[BuffSummary] No summaries built for card {CardId}. BuffCount={BuffCount} GroupCount={GroupCount}.",
                    cardInstance.InstanceId,
                    buffs.Count,
                    groupedBuffs.Count);
            }
            else if (summaries.Count < groupedBuffs.Count)
            {
                logger?.LogInformation(
                    "[BuffSummary] Partial summaries for card {CardId}. Built={BuiltCount} GroupCount={GroupCount}.",
                    cardInstance.InstanceId,
                    summaries.Count,
                    groupedBuffs.Count);
            }

            return summaries;
        }

        private static BuffStatSummaryDto? BuildBuffStatSummary(CardInstance cardInstance, GameContext? context)
        {
            if (context == null)
            {
                return null;
            }

            var buffs = GetVisibleBuffs(cardInstance, context, context.BuffManager);
            if (buffs.Count == 0)
            {
                return null;
            }

            var hasStatMods = buffs.Any(buff => buff.GetStatModifications().Any());
            if (!hasStatMods)
            {
                return null;
            }

            var attackDelta = ComputeVisibleStatValue(cardInstance.BaseAttack, buffs, StatType.Attack) - cardInstance.BaseAttack;
            var healthDelta = ComputeVisibleStatValue(cardInstance.BaseHealth, buffs, StatType.Health) - cardInstance.BaseHealth;
            var costDelta = ComputeVisibleStatValue(cardInstance.BaseCost, buffs, StatType.Cost) - cardInstance.BaseCost;

            if (attackDelta == 0 && healthDelta == 0 && costDelta == 0)
            {
                return null;
            }

            return new BuffStatSummaryDto
            {
                AttackDelta = attackDelta,
                HealthDelta = healthDelta,
                CostDelta = costDelta
            };
        }

        private static List<Buff> GetVisibleBuffs(CardInstance cardInstance, GameContext context, IBuffManager buffManager)
        {
            var buffs = buffManager.GetBuffsForCard(cardInstance.InstanceId);
            if (buffs.Count == 0)
            {
                return new List<Buff>();
            }

            return buffs
                .Where(buff => ShouldDisplayBuff(buff, context))
                .ToList();
        }

        private static bool ShouldDisplayBuff(Buff buff, GameContext context)
        {
            ResolveSourceAbility(buff, context.AbilityManager, out var ability, out var snapshot);
            return (ability as Ability)?.BuffDisplay != false
                && snapshot?.BuffDisplay != false;
        }

        private static void ResolveSourceAbility(
            Buff buff,
            IAbilityManager abilityManager,
            out IAbility? ability,
            out AbilitySnapshot? snapshot)
        {
            ability = null;
            snapshot = null;

            var preferredAbilityId = buff.SummaryRootAbilityId.HasValue && buff.SummaryRootAbilityId.Value != Guid.Empty
                ? buff.SummaryRootAbilityId.Value
                : buff.SourceAbilityId;

            if (preferredAbilityId != Guid.Empty)
            {
                ability = abilityManager.GetAbilityByRuntimeId(preferredAbilityId);
                if (ability == null)
                {
                    abilityManager.TryGetAbilitySnapshot(preferredAbilityId, out snapshot);
                }
            }

            if (ability == null && snapshot == null && buff.SourceAbilityId != Guid.Empty && buff.SourceAbilityId != preferredAbilityId)
            {
                ability = abilityManager.GetAbilityByRuntimeId(buff.SourceAbilityId);
                if (ability == null)
                {
                    abilityManager.TryGetAbilitySnapshot(buff.SourceAbilityId, out snapshot);
                }
            }
        }

        private static int ComputeVisibleStatValue(int baseValue, IEnumerable<Buff> buffs, StatType stat)
        {
            var currentValue = baseValue;
            foreach (var buff in buffs)
            {
                if (!buff.IsActive)
                {
                    continue;
                }

                var modifiers = buff.GetStatModifications()
                    .Where(modification => modification.Stat == stat)
                    .OrderBy(modification => modification.Layer);

                foreach (var modification in modifiers)
                {
                    currentValue = BuffValueCalculation.Apply(currentValue, modification.Layer, modification.Value);
                }
            }

            return currentValue;
        }

    }
}
