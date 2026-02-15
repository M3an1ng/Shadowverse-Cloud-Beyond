using AutoMapper;
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

namespace ShadowverseBeyond.Server.Mappings
{
    /// <summary>
    /// Configures AutoMapper profiles for mapping between CardInstance entities and DTOs.
    /// 【修复】现在包含定义属性的映射，确保前端能正确显示卡牌信息
    /// </summary>
    public class CardInstanceProfile : Profile
    {
        private const string FusionCountCounterKey = "fusion_count";
        private const string FusionCountDisplayName = "融合数";

        public CardInstanceProfile()
        {
            CreateMap<CardInstance, CardInstanceDto>()
                // --- Instance & Definition Identifiers ---
                .ForMember(dest => dest.InstanceId, opt => opt.MapFrom(src => src.InstanceId))
                .ForMember(dest => dest.CardId, opt => opt.MapFrom(src => src.CurrentDefinitionId))

                // --- Static Data (from computed properties in CardInstance that need explicit mapping) ---
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.CardNames, opt => opt.MapFrom(src => src.CardNames))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.ArtPreview, opt => opt.MapFrom(src => src.CurrentDefinition != null ? src.CurrentDefinition.ArtPreview : null))
                .ForMember(dest => dest.ArtDetail, opt => opt.MapFrom(src => src.CurrentDefinition != null ? src.CurrentDefinition.ArtDetail : null))
                .ForMember(dest => dest.Rarity, opt => opt.MapFrom(src => src.Rarity))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.CardType))
                .ForMember(dest => dest.Class, opt => opt.MapFrom(src => src.Class))
                .ForMember(dest => dest.Tribe, opt => opt.MapFrom(src => src.Tribe))
                .ForMember(dest => dest.Earthsigil, opt => opt.MapFrom(src => src.Earthsigil))
                .ForMember(dest => dest.Counters, opt => opt.MapFrom(src => BuildCounterSnapshot(src)))
                .ForMember(dest => dest.CounterDisplayNames, opt => opt.MapFrom(src => BuildCounterDisplayNameSnapshot(src)))

                // --- Base Stats (Mapping from direct properties) ---
                .ForMember(dest => dest.Attack, opt => opt.MapFrom(src => src.BaseAttack))
                .ForMember(dest => dest.Health, opt => opt.MapFrom(src => src.BaseHealth))
                .ForMember(dest => dest.Cost, opt => opt.MapFrom(src => src.BaseCost))

                // --- Dynamic State (Mapping properties with same name but for clarity) ---
                .ForMember(dest => dest.CurrentCost, opt => opt.MapFrom(src => src.CurrentCost))
                .ForMember(dest => dest.CurrentAttack, opt => opt.MapFrom(src => src.CurrentAttack))
                .ForMember(dest => dest.CurrentHealth, opt => opt.MapFrom(src => src.CurrentHealth))
                .ForMember(dest => dest.MaxHealth, opt => opt.MapFrom(src => src.MaxHealth))
                .ForMember(dest => dest.IsEvolved, opt => opt.MapFrom(src => src.IsEvolved))
                .ForMember(dest => dest.IsSuperEvolved, opt => opt.MapFrom(src => src.IsSuperEvolved))
                .ForMember(dest => dest.CanAttack, opt => opt.MapFrom(src => src.CanAttack))
                .ForMember(dest => dest.HasActAbility, opt => opt.MapFrom(src => src.HasActAbility))
                .ForMember(dest => dest.ActAbilities, opt => opt.MapFrom(src => BuildActionSummaries(src, AbilityKeyword.Act)))
                .ForMember(dest => dest.ActLastUsedTurnNumber, opt => opt.MapFrom(src => src.ActLastUsedTurnNumber))
                .ForMember(dest => dest.FusionAbilities, opt => opt.MapFrom(src => BuildActionSummaries(src, AbilityKeyword.Fusion)))
                .ForMember(dest => dest.FusedCards, opt => opt.MapFrom(src => src.FusedCards))
                .ForMember(dest => dest.AttacksThisTurn, opt => opt.MapFrom(src => src.AttacksThisTurn))
                .ForMember(dest => dest.FieldTurn, opt => opt.MapFrom(src => src.FieldTurn)) // BUG修复：添加缺失的FieldTurn映射
                .ForMember(dest => dest.CurrentCountdown, opt => opt.MapFrom(src => src.CurrentCountdown))
                .ForMember(dest => dest.MaxAttacksPerTurn, opt => opt.MapFrom(src => src.MaxAttacksPerTurn))
                .ForMember(dest => dest.Zone, opt => opt.MapFrom(src => src.CurrentZone))
                .ForMember(dest => dest.OwnerPlayerId, opt => opt.MapFrom(src => src.OwnerPlayerId))
                .ForMember(dest => dest.Keywords, opt => opt.MapFrom(src => src.Keywords))
                .ForMember(dest => dest.BuffSummaries, opt => opt.MapFrom(src => BuildBuffSummaries(src)))
                .ForMember(dest => dest.BuffStatSummary, opt => opt.MapFrom(src => BuildBuffStatSummary(src)))

                // --- Evolution Info ---
                .ForMember(dest => dest.CanEvolve, opt => opt.MapFrom(src => src.CanEvolve()));
        }

        private static Dictionary<string, int> BuildCounterSnapshot(CardInstance cardInstance)
        {
            if (cardInstance == null)
            {
                return new Dictionary<string, int>();
            }

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in cardInstance.CounterMap)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }
                result[pair.Key] = Math.Max(0, pair.Value);
            }

            var fusedCount = Math.Max(0, cardInstance.FusedCards?.Count ?? 0);
            if (fusedCount > 0)
            {
                result[FusionCountCounterKey] = fusedCount;
            }

            return result;
        }

        private static Dictionary<string, string> BuildCounterDisplayNameSnapshot(CardInstance cardInstance)
        {
            if (cardInstance == null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var abilities = cardInstance.Abilities ?? new List<Ability>();
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

            var fusedCount = Math.Max(0, cardInstance.FusedCards?.Count ?? 0);
            if (fusedCount > 0 && !result.ContainsKey(FusionCountCounterKey))
            {
                result[FusionCountCounterKey] = FusionCountDisplayName;
            }

            return result;
        }

        private static List<AbilityActionSummaryDto> BuildActionSummaries(CardInstance cardInstance, AbilityKeyword keyword)
        {
            if (cardInstance == null)
            {
                return new List<AbilityActionSummaryDto>();
            }

            var context = GameServiceProvider.CurrentGameContext;
            var effectiveAbilities = context?.AbilityManager?
                .GetEffectiveAbilitiesForCard(cardInstance.InstanceId, context);
            IEnumerable<IAbility> abilities = effectiveAbilities ?? cardInstance.Abilities.Cast<IAbility>();

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

        private static List<BuffSummaryDto> BuildBuffSummaries(CardInstance cardInstance)
        {
            var context = GameServiceProvider.CurrentGameContext;
            var buffManager = context?.BuffManager;
            if (context == null || buffManager == null)
            {
                return new List<BuffSummaryDto>();
            }

            var buffs = buffManager.GetBuffsForCard(cardInstance.InstanceId);
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
                var sample = group.FirstOrDefault();
                if (sample == null)
                {
                    continue;
                }

                var preferredAbilityId = sample.SummaryRootAbilityId.HasValue && sample.SummaryRootAbilityId.Value != Guid.Empty
                    ? sample.SummaryRootAbilityId.Value
                    : sample.SourceAbilityId;

                IAbility? ability = null;
                AbilitySnapshot? abilitySnapshot = null;
                if (preferredAbilityId != Guid.Empty)
                {
                    ability = abilityManager?.GetAbilityByRuntimeId(preferredAbilityId);
                    if (ability == null && abilityManager != null && abilityManager.TryGetAbilitySnapshot(preferredAbilityId, out var preferredSnapshot))
                    {
                        abilitySnapshot = preferredSnapshot;
                    }
                }

                if (ability == null && abilitySnapshot == null && sample.SourceAbilityId != Guid.Empty && sample.SourceAbilityId != preferredAbilityId)
                {
                    ability = abilityManager?.GetAbilityByRuntimeId(sample.SourceAbilityId);
                    if (ability == null && abilityManager != null && abilityManager.TryGetAbilitySnapshot(sample.SourceAbilityId, out var sourceSnapshot))
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

        private static BuffStatSummaryDto? BuildBuffStatSummary(CardInstance cardInstance)
        {
            var context = GameServiceProvider.CurrentGameContext;
            var buffManager = context?.BuffManager;
            var buffService = context?.BuffService;
            if (buffManager == null || buffService == null)
            {
                return null;
            }

            var buffs = buffManager.GetBuffsForCard(cardInstance.InstanceId);
            if (buffs.Count == 0)
            {
                return null;
            }

            var hasStatMods = buffs.Any(buff => buff.GetStatModifications().Any());
            if (!hasStatMods)
            {
                return null;
            }

            var attackDelta = buffService.GetEffectiveAttack(cardInstance, context) - cardInstance.BaseAttack;
            var healthDelta = buffService.GetEffectiveMaxHealth(cardInstance, context) - cardInstance.BaseHealth;
            var costDelta = buffService.GetEffectiveCost(cardInstance, context) - cardInstance.BaseCost;

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
    }
}
