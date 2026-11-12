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
            var appearance = ShadowverseBeyond.Server.Infrastructure.Data.CardStyleCatalog.Resolve(source.OwnerPlayerId, source.CurrentDefinitionId);
            var artAppearance=appearance ?? (source.AlternateArtDefinitionId is int parentId ? ShadowverseBeyond.Server.Infrastructure.Data.CardStyleCatalog.Resolve(source.OwnerPlayerId,parentId) : null);
            return new CardInstanceDto
            {
                InstanceId = source.InstanceId,
                CardId = source.CurrentDefinitionId,
                Name = appearance?.Name ?? source.Name,
                CardNames = source.CardNames,
                Description = appearance?.Description ?? source.Description,
                ArtPreview = artAppearance?.ArtPreview ?? (source.AlternateArtDefinitionId is int artId ? GameDataProvider.Instance.GetCardDefinition(artId) : definition)?.ArtPreview,
                ArtDetail = artAppearance?.ArtDetail ?? (source.AlternateArtDefinitionId is int detailArtId ? GameDataProvider.Instance.GetCardDefinition(detailArtId) : definition)?.ArtDetail,
                Rarity = source.Rarity,
                Type = source.CardType,
                Class = source.Class,
                Tribe = source.Tribe,
                Earthsigil = source.Earthsigil,
                Evidence = source.Evidence,
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
                IsMirageRecruited = source.IsMirageRecruited,
                IsMirageTriple = source.IsMirageTriple,
                OriginInstanceId = source.OriginInstanceId,
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
                IsIntromezzoActive = context?.CurrentGameState.GetPlayerState(source.OwnerPlayerId)?.IsIntromezzoActive ?? false,
                Keywords = source.GetKeywords(context).ToList(),
                BuffSummaries = BuildBuffSummaries(source, context),
                AlternatePlayReferences = BuildAlternatePlayReferences(source, context),
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
            // Sequence cursors are engine state, not player-facing counters.
            if (key.StartsWith("sequence.cursor.", StringComparison.OrdinalIgnoreCase)) return;
            if (!seenKeys.Add(key))
            {
                return;
            }

            var label = string.IsNullOrWhiteSpace(rawLabel)
                ? key
                : rawLabel.Trim();
            label = key.ToLowerInvariant() switch { "spellboost" => "魔力增幅数", "earthsigil" => "土之印数", _ => label };
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

        private static List<AlternatePlayReferenceDto> BuildAlternatePlayReferences(CardInstance card, GameContext? context)
        {
            var abilities = context != null
                ? context.AbilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context)
                    .Where(ability => ability.HasKeyword(AbilityKeyword.Accelerate) || ability.HasKeyword(AbilityKeyword.Crystalize))
                    .Select(ability => (Config: ability.AlternatePlay, Label: ability.HasKeyword(AbilityKeyword.Accelerate) ? "激奏" : "结晶"))
                : GameDataProvider.Instance.GetAbilityConfigs(card.CurrentDefinitionId)
                    .Where(ability => ability.HasKeyword(AbilityKeyword.Accelerate) || ability.HasKeyword(AbilityKeyword.Crystalize))
                    .Select(ability => (Config: ability.AlternatePlay, Label: ability.HasKeyword(AbilityKeyword.Accelerate) ? "激奏" : "结晶"));
            return abilities.Where(item => item.Config?.PlayAsCardDefinitionId > 0)
                .Select(item => (Definition: GameDataProvider.Instance.GetCardDefinition(item.Config!.PlayAsCardDefinitionId), item.Label))
                .Where(item => item.Definition != null)
                .DistinctBy(item => item.Definition!.Id)
                .Select(item => new AlternatePlayReferenceDto {
                    Id = item.Definition!.Id, Label = item.Label, Name = item.Definition.Name,
                    Description = item.Definition.Description, Cost = item.Definition.Cost,
                    Attack = item.Definition.Attack, Health = item.Definition.Health,
                    ArtPreview = GameDataProvider.Instance.GetCardDefinition(card.AlternateArtDefinitionId ?? card.CurrentDefinitionId)?.ArtPreview ?? string.Empty
                }).ToList();
        }

        private static IEnumerable<AttachedNumericEffectDto> BuildNumericEffects(Buff buff, GameContext context)
        {
            // These are already-applied buffs. Their originating ability's trigger/target IDs
            // do not change applicability; group by the buff's actual remaining lifetime instead.
            var policy = global::System.Text.Json.JsonSerializer.Serialize(new {
                buff.DurationType, buff.DurationParameter, buff.RemoveAfterUse,
                AppliedTurn = buff.DurationType == DurationType.Permanent ? 0 : buff.AppliedTurn,
                RemovalExpression = buff.RemovalCondition?.Expression,
                RemovalEnabled = buff.RemovalCondition?.IsEnabled,
                buff.RemovalTrigger,
                RemovalSource = buff.RemovalCondition != null ? buff.SourceCardInstanceId : null
            });
            var key = Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(policy)));
            var texts = DescribeBuff(buff).ToList();
            var index = 0;
            foreach (var stat in buff.GetStatModifications())
                yield return new AttachedNumericEffectDto { Field = stat.Stat.ToString(), Layer = stat.Layer.ToString(), Value = stat.Value, PolicyKey = key, RemoveAfterUse = buff.RemoveAfterUse, Duration = buff.DurationType.ToString(), DisplayText = texts[index++] };
            foreach (var effect in buff.Effects.OfType<AbilityKeywordBuffEffect>())
                foreach (var modifier in effect.Modifications)
                    yield return new AttachedNumericEffectDto { Field = modifier.Keyword.ToString(), Layer = modifier.Layer.ToString(), Value = modifier.Value, PolicyKey = key, RemoveAfterUse = buff.RemoveAfterUse, Duration = buff.DurationType.ToString(), DisplayText = texts[index++] };
        }

        private static IEnumerable<string> DescribeBuff(Buff buff, IEnumerable<AbilityConfig>? grantedAbilities = null)
        {
            static string Number(double value, ModifierLayer layer) => layer switch
            {
                ModifierLayer.Set => $"变为 {value:0.##}",
                ModifierLayer.Base => $"基础值 {value:0.##}",
                ModifierLayer.Multiplicative => $"×{value:0.##}",
                _ => value >= 0 ? $"+{value:0.##}" : $"{value:0.##}"
            };
            var duration = buff.DurationType switch
            {
                DurationType.Permanent => "",
                DurationType.ConditionalRemoval => " · ConditionalRemoval",
                _ => $" · {buff.DurationType}（{Math.Max(1, buff.DurationParameter ?? 1)}次）"
            };
            foreach (var stat in buff.GetStatModifications())
                yield return $"{stat.Stat} {Number(stat.Value, stat.Layer)}{duration}";
            foreach (var effect in buff.Effects.OfType<AbilityKeywordBuffEffect>())
                foreach (var modifier in effect.Modifications)
                    yield return $"{modifier.Keyword} {Number(modifier.Value, modifier.Layer)}{duration}";
            foreach (var keyword in buff.GetGrantedKeywords()) yield return $"获得 {keyword}{duration}";
            foreach (var granted in grantedAbilities ?? buff.GetGrantedAbilityConfigs()) yield return $"{(string.IsNullOrWhiteSpace(granted.Description) ? granted.Name : granted.Description)}{duration}";
            foreach (var tribe in buff.GetAddedTribes()) yield return $"种类：{tribe}{duration}";
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

            // A grant buff remains as historical runtime state after RemoveAbility suppresses it.
            // Display only abilities that are currently effective, matched by the exact origin buff
            // so removing one grant does not hide a later grant of the same ability definition.
            var visibleGrantedAbilities = context.AbilityManager
                .GetEffectiveAbilitiesForCard(cardInstance.InstanceId, context)
                .OfType<Ability>()
                .Where(ability => ability.SourceBuffId.HasValue && ability.Config.IsEnabled)
                .GroupBy(ability => ability.SourceBuffId!.Value)
                .ToDictionary(group => group.Key, group => group.Select(ability => ability.Config).ToList());
            List<AbilityConfig> GetVisibleGrants(Buff buff) => visibleGrantedAbilities.TryGetValue(buff.Id, out var granted)
                ? granted : new List<AbilityConfig>();
            buffs = buffs.Where(buff => !buff.GetGrantedAbilityConfigs().Any()
                || GetVisibleGrants(buff).Count > 0
                || buff.Effects.Any(effect => effect is not AbilityBuffEffect)).ToList();

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

                var sourceAbilityName = abilityManager.GetAbilityByRuntimeId(sample.SourceAbilityId)?.Name ?? sample.SourceAbilityName ?? ability?.Name ?? abilitySnapshot?.AbilityName;
                if (string.IsNullOrWhiteSpace(sourceCardName)) sourceCardName = "附加效果";
                if (string.IsNullOrWhiteSpace(sourceAbilityName)) sourceAbilityName = "附加效果";

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
                    SourceCardDefinitionId = (ability?.SourceCard ?? gameState.FindCardInstance(sample.SourceCardInstanceId ?? Guid.Empty))?.CurrentDefinition.Id,
                    SourceCardName = sourceCardName,
                    SourceCardArtPreview = sourceCardArtPreview ?? string.Empty,
                    SourceAbilityName = sourceAbilityName,
                    GrantedAbilities = group.SelectMany(GetVisibleGrants).Select(config => new GrantedAbilityDetailDto { Name = config.Name ?? "", Description = config.Description ?? "" }).ToList(),
                    Description = (ability as Ability)?.Description ?? string.Empty,
                    Details = group.SelectMany(buff => DescribeBuff(buff, GetVisibleGrants(buff))).ToList(),
                    NumericEffects = group.SelectMany(buff => BuildNumericEffects(buff, context)).ToList()
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
                .Where(buff => buff.IsActive && ShouldDisplayBuff(buff, context))
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
