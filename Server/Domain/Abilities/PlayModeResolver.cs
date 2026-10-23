using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    public sealed class PlayModeResolution
    {
        public IReadOnlyList<PlayModeAvailabilityDto> Modes { get; }
        public PlayMode? SelectedMode { get; }
        public int? SelectedCost { get; }
        public Guid? SelectedAbilityRuntimeId { get; }
        public int? SelectedAlternateCardDefinitionId { get; }
        public string? DefaultFailureReason { get; }

        public bool HasAvailableMode => Modes.Any(m => m.IsAvailable);

        public PlayModeResolution(
            IReadOnlyList<PlayModeAvailabilityDto> modes,
            PlayMode? selectedMode,
            int? selectedCost,
            Guid? selectedAbilityRuntimeId,
            int? selectedAlternateCardDefinitionId,
            string? defaultFailureReason)
        {
            Modes = modes;
            SelectedMode = selectedMode;
            SelectedCost = selectedCost;
            SelectedAbilityRuntimeId = selectedAbilityRuntimeId;
            SelectedAlternateCardDefinitionId = selectedAlternateCardDefinitionId;
            DefaultFailureReason = defaultFailureReason;
        }
    }

    public static class PlayModeResolver
    {
        public const string EnhanceDisabledKey = "enhance.disabled";

        private static readonly PlayMode[] PriorityOrder =
        {
            PlayMode.Enhance,
            PlayMode.Base
        };

        public static PlayModeResolution Resolve(
            CardInstance card,
            PlayerState player,
            GameContext context)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var modes = new List<PlayModeAvailabilityDto>();
            var currentPp = player.GetSpendablePlayPoints();

            var baseCost = Math.Max(0, card.CurrentCost);
            modes.Add(BuildAvailability(PlayMode.Base, baseCost, currentPp));

            var (selectedEnhanceCost, minEnhanceCost, selectedEnhanceAbilityRuntimeId) = ResolveEnhanceCost(card, context, currentPp);
            if (minEnhanceCost.HasValue)
            {
                var isDisabled = IsEnhanceDisabled(card);
                var available = !isDisabled && selectedEnhanceCost.HasValue;
                var effectiveCost = selectedEnhanceCost ?? minEnhanceCost.Value;
                var reason = string.Empty;

                if (isDisabled)
                {
                    reason = "Enhance disabled.";
                }
                else if (!available)
                {
                    reason = $"Not enough PP for Enhance (need {minEnhanceCost.Value}, have {currentPp}).";
                }

                modes.Add(new PlayModeAvailabilityDto
                {
                    Mode = PlayMode.Enhance,
                    EffectiveCost = effectiveCost,
                    IsAvailable = available,
                    Reason = reason
                });
            }

            Guid? selectedAlternateAbilityRuntimeId = null;
            PlayMode? selectedAlternateMode = null;
            int? selectedAlternateCost = null;
            int? selectedAlternateCardDefinitionId = null;
            if (currentPp < baseCost)
            {
                var alternateResolution = ResolveAlternatePlay(card, context, currentPp);
                modes.AddRange(alternateResolution.Modes);
                selectedAlternateAbilityRuntimeId = alternateResolution.SelectedAbilityRuntimeId;
                selectedAlternateMode = alternateResolution.SelectedMode;
                selectedAlternateCost = alternateResolution.SelectedCost;
                selectedAlternateCardDefinitionId = alternateResolution.SelectedAlternateCardDefinitionId;
            }

            var selectedMode = selectedAlternateMode ?? SelectMode(modes, currentPp < baseCost);
            var selectedCost = selectedAlternateCost ?? (selectedMode.HasValue
                ? modes.First(m => m.Mode == selectedMode.Value).EffectiveCost
                : (int?)null);
            var selectedAbilityRuntimeId = selectedMode == PlayMode.Enhance
                ? selectedEnhanceAbilityRuntimeId
                : selectedMode is PlayMode.Accelerate or PlayMode.Crystalize
                    ? selectedAlternateAbilityRuntimeId
                    : null;
            var selectedAlternateDefinitionId = selectedMode is PlayMode.Accelerate or PlayMode.Crystalize
                ? selectedAlternateCardDefinitionId
                : (int?)null;

            var defaultFailureReason = selectedMode == null ? SelectFailureReason(modes) : null;

            return new PlayModeResolution(
                modes,
                selectedMode,
                selectedCost,
                selectedAbilityRuntimeId,
                selectedAlternateDefinitionId,
                defaultFailureReason);
        }

        private sealed class AlternatePlayResolution
        {
            public List<PlayModeAvailabilityDto> Modes { get; } = new();
            public PlayMode? SelectedMode { get; set; }
            public int? SelectedCost { get; set; }
            public Guid? SelectedAbilityRuntimeId { get; set; }
            public int? SelectedAlternateCardDefinitionId { get; set; }
        }

        private sealed record AlternatePlayCandidate(
            PlayMode Mode,
            AbilityKeyword Keyword,
            int Cost,
            Guid RuntimeId,
            int PlayAsCardDefinitionId,
            int Order);

        private static AlternatePlayResolution ResolveAlternatePlay(
            CardInstance card,
            GameContext context,
            int currentPp)
        {
            var resolution = new AlternatePlayResolution();
            var abilities = context.AbilityManager?
                .GetEffectiveAbilitiesForCard(card.InstanceId, context)
                .ToList() ?? new List<IAbility>();

            if (abilities.Count == 0)
            {
                return resolution;
            }

            var candidates = new List<AlternatePlayCandidate>();
            for (var index = 0; index < abilities.Count; index++)
            {
                var ability = abilities[index];
                if (ability == null)
                {
                    continue;
                }

                foreach (var keywordConfig in ability.AbilityKeywords)
                {
                    var keyword = keywordConfig.Keyword;
                    if (!ability.HasKeyword(keyword))
                    {
                        continue;
                    }

                    if (keyword is not (AbilityKeyword.Accelerate or AbilityKeyword.Crystalize))
                    {
                        continue;
                    }

                    var playAsCardDefinitionId = ability.AlternatePlay?.PlayAsCardDefinitionId ?? 0;
                    if (playAsCardDefinitionId <= 0)
                    {
                        continue;
                    }

                    var cost = GetAlternatePlayCostForAbility(card, keywordConfig, keyword, context);
                    if (cost <= 0)
                    {
                        continue;
                    }

                    candidates.Add(new AlternatePlayCandidate(
                        ToPlayMode(keyword),
                        keyword,
                        cost,
                        ability.RuntimeId,
                        playAsCardDefinitionId,
                        index));
                }
            }

            if (candidates.Count == 0)
            {
                return resolution;
            }

            foreach (var group in candidates.GroupBy(c => c.Mode))
            {
                var minCost = group.Min(c => c.Cost);
                var selectedForMode = group
                    .Where(c => c.Cost <= currentPp)
                    .OrderByDescending(c => c.Cost)
                    .ThenBy(c => c.Order)
                    .FirstOrDefault();

                var available = selectedForMode != null;
                var effectiveCost = selectedForMode?.Cost ?? minCost;
                var reason = available
                    ? string.Empty
                    : $"Not enough PP for {group.Key} (need {minCost}, have {currentPp}).";

                resolution.Modes.Add(new PlayModeAvailabilityDto
                {
                    Mode = group.Key,
                    EffectiveCost = effectiveCost,
                    IsAvailable = available,
                    Reason = reason
                });
            }

            var selected = candidates
                .Where(c => c.Cost <= currentPp)
                .OrderByDescending(c => c.Cost)
                .ThenBy(c => c.Order)
                .FirstOrDefault();

            if (selected != null)
            {
                resolution.SelectedMode = selected.Mode;
                resolution.SelectedCost = selected.Cost;
                resolution.SelectedAbilityRuntimeId = selected.RuntimeId;
                resolution.SelectedAlternateCardDefinitionId = selected.PlayAsCardDefinitionId;
            }

            return resolution;
        }

        private static (int? SelectedCost, int? MinCost, Guid? SelectedAbilityRuntimeId) ResolveEnhanceCost(
            CardInstance card,
            GameContext context,
            int currentPp)
        {
            var enhanceAbilities = context.AbilityManager?
                .GetEffectiveAbilitiesForCard(card.InstanceId, context)
                .Where(a => a.HasKeyword(AbilityKeyword.Enhance))
                .ToList() ?? new List<IAbility>();

            if (enhanceAbilities.Count == 0)
            {
                return (null, null, null);
            }

            var runtimeCandidates = new List<(int Cost, Guid RuntimeId)>();
            foreach (var ability in enhanceAbilities)
            {
                if (ability == null)
                {
                    continue;
                }

                var cost = card.GetEnhanceCostForAbility(ability, context);
                if (cost > 0)
                {
                    runtimeCandidates.Add((cost, ability.RuntimeId));
                }
            }

            if (runtimeCandidates.Count == 0)
            {
                return (null, null, null);
            }

            var minCost = runtimeCandidates.Min(c => c.Cost);
            var selected = runtimeCandidates
                .Where(c => c.Cost <= currentPp)
                .OrderByDescending(c => c.Cost)
                .FirstOrDefault();

            if (selected.Cost > 0)
            {
                return (selected.Cost, minCost, selected.RuntimeId);
            }

            return (null, minCost, null);
        }

        private static PlayMode? SelectMode(IReadOnlyList<PlayModeAvailabilityDto> modes, bool baseUnavailable)
        {
            if (baseUnavailable)
            {
                var selectedAlternate = modes
                    .Where(m => (m.Mode == PlayMode.Accelerate || m.Mode == PlayMode.Crystalize) && m.IsAvailable)
                    .OrderByDescending(m => m.EffectiveCost)
                    .FirstOrDefault();

                if (selectedAlternate != null)
                {
                    return selectedAlternate.Mode;
                }
            }

            foreach (var mode in PriorityOrder)
            {
                var availability = modes.FirstOrDefault(m => m.Mode == mode);
                if (availability != null && availability.IsAvailable)
                {
                    return mode;
                }
            }

            return null;
        }

        private static PlayMode ToPlayMode(AbilityKeyword keyword)
            => keyword == AbilityKeyword.Accelerate ? PlayMode.Accelerate : PlayMode.Crystalize;

        private static int GetAlternatePlayCostForAbility(
            CardInstance card,
            AbilityKeywordConfig keywordConfig,
            AbilityKeyword keyword,
            GameContext context)
        {
            var baseValue = keywordConfig.Value ?? 0;
            return context.BuffService.GetEffectiveAbilityKeywordValue(card, keyword, baseValue, context);
        }

        private static string? SelectFailureReason(IReadOnlyList<PlayModeAvailabilityDto> modes)
        {
            foreach (var mode in PriorityOrder)
            {
                var availability = modes.FirstOrDefault(m => m.Mode == mode);
                if (availability != null && !availability.IsAvailable && !string.IsNullOrWhiteSpace(availability.Reason))
                {
                    return availability.Reason;
                }
            }

            return null;
        }

        private static PlayModeAvailabilityDto BuildAvailability(PlayMode mode, int cost, int currentPp)
        {
            var available = currentPp >= cost;
            var reason = available ? string.Empty : $"Not enough PP (need {cost}, have {currentPp}).";

            return new PlayModeAvailabilityDto
            {
                Mode = mode,
                EffectiveCost = cost,
                IsAvailable = available,
                Reason = reason
            };
        }

        private static bool IsEnhanceDisabled(CardInstance card)
        {
            if (!card.AbilityState.TryGetValue(EnhanceDisabledKey, out var value) || value == null)
            {
                return false;
            }

            return value switch
            {
                bool b => b,
                int i => i != 0,
                long l => l != 0,
                string s => bool.TryParse(s, out var parsed) && parsed,
                _ => false
            };
        }
    }
}
