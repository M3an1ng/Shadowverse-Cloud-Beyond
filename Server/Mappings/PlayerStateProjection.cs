using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;

using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Mappings
{
    public static class PlayerStateProjection
    {

        public static PlayerStateDto Create(PlayerState source, GameContext? context)
        {
            return new PlayerStateDto
            {
                MaxPP = source.MaxPlayPoints,
                CurrentPP = source.CurrentPlayPoints,
                PpBoostUsesAvailable = source.PpBoostUsesAvailable,
                IsPpBoostArmed = source.IsPpBoostArmed,
                MaxEP = source.MaxEvolutionPoints,
                CurrentEP = source.CurrentEvolutionPoints,
                IsEvolutionUnlocked = source.IsEvolutionUnlocked,
                CanEvolve = source.CanEvolve,
                MaxSEP = source.MaxSuperEvolutionPoints,
                CurrentSEP = source.CurrentSuperEvolutionPoints,
                IsSuperEvolutionUnlocked = source.IsSuperEvolutionUnlocked,
                CanSuperEvolve = source.CanSuperEvolve,
                Class = source.Class,
                DisplayName = source.DisplayName,
                AvatarUrl = source.AvatarUrl,
                GraveyardCount = source.GraveyardCount,
                BloodPool = source.BloodPool,
                Counters = BuildCounterSnapshot(source),
                CounterDisplayNames = BuildCounterDisplayNameSnapshot(source),
                CounterSummaryEntries = BuildCounterSummaryEntries(source),
                LeaderCard = source.Leader == null ? null : CardInstanceProjection.Create(source.Leader, context),
                Field = source.Field.Where(card => !card.IsLeader).Select(card => CardInstanceProjection.Create(card, context)).ToList(),
                Emblem = source.Emblem.Select(card => CardInstanceProjection.Create(card, context)).ToList(),
                Hand = source.Hand.Select(card => CardInstanceProjection.Create(card, context)).ToList(),
                Deck = source.Deck.Select(card => CardInstanceProjection.Create(card, context)).ToList(),
                Graveyard = source.Graveyard.Select(card => CardInstanceProjection.Create(card, context)).ToList(),
                Extra = source.Extra.Select(card => CardInstanceProjection.Create(card, context)).ToList(),
                PlayerId = source.PlayerId,
                IsAI = source.IsAI,
                IsFirst = source.IsFirst,
                DeckCount = source.DeckCount,
                PlayedCardCountThisTurn = source.PlayedCardCountThisTurn,
                HasCompletedMulligan = source.HasCompletedMulligan,
                HasTakenFirstTurn = source.HasTakenFirstTurn,
                TurnNumber = source.TurnNumber,
            };
        }

        private static Dictionary<string, int> BuildCounterSnapshot(PlayerState playerState)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in playerState.CounterMap)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                result[pair.Key] = Math.Max(0, pair.Value);
            }

            return result;
        }

        private static Dictionary<string, string> BuildCounterDisplayNameSnapshot(PlayerState playerState)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PlayerState.ComboCounterKey] = "连击数",
                [PlayerState.GraveyardCounterKey] = "墓场数",
                [PlayerState.BloodPoolCounterKey] = "血池",
                [PlayerState.RallyCounterKey] = "连携数"
            };

            foreach (var card in EnumeratePlayerCards(playerState))
            {
                var abilities = card?.Abilities;
                if (abilities == null || abilities.Count == 0)
                {
                    continue;
                }

                foreach (var ability in abilities)
                {
                    var registrations = ability?.Config?.PlayerCounterRegistrations;
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

                        result[name] = string.IsNullOrWhiteSpace(registration.DisplayName)
                            ? name
                            : registration.DisplayName.Trim();
                    }
                }
            }

            foreach (var key in playerState.CounterMap.Keys)
            {
                if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key))
                {
                    continue;
                }

                result[key] = key.Trim();
            }

            return result;
        }

        private static List<CounterSummaryEntryDto> BuildCounterSummaryEntries(PlayerState playerState)
        {
            var entries = new List<CounterSummaryEntryDto>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddCounterSummaryEntry(entries, seenKeys, PlayerState.ComboCounterKey, "连击数", playerState.Combo);
            AddCounterSummaryEntry(entries, seenKeys, PlayerState.GraveyardCounterKey, "墓场数", playerState.GraveyardCount);
            AddCounterSummaryEntry(entries, seenKeys, PlayerState.BloodPoolCounterKey, "血池", playerState.BloodPool);
            AddCounterSummaryEntry(entries, seenKeys, PlayerState.RallyCounterKey, "连携数", playerState.Rally);

            foreach (var card in EnumeratePlayerCards(playerState))
            {
                var abilities = card?.Abilities;
                if (abilities == null || abilities.Count == 0)
                {
                    continue;
                }

                foreach (var ability in abilities)
                {
                    var registrations = ability?.Config?.PlayerCounterRegistrations;
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
                            playerState.GetCounter(registration.Name));
                    }
                }
            }

            foreach (var pair in playerState.CounterMap)
            {
                AddCounterSummaryEntry(entries, seenKeys, pair.Key, pair.Key, pair.Value);
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

            entries.Add(new CounterSummaryEntryDto
            {
                Key = key,
                Label = string.IsNullOrWhiteSpace(rawLabel) ? key : rawLabel.Trim(),
                Value = Math.Max(0, rawValue)
            });
        }

        private static IEnumerable<CardInstance> EnumeratePlayerCards(PlayerState playerState)
        {
            if (playerState?.Leader != null)
            {
                yield return playerState.Leader;
            }

            var seen = new HashSet<Guid>();
            if (playerState?.Leader != null)
            {
                seen.Add(playerState.Leader.InstanceId);
            }

            foreach (var zone in new[]
            {
                playerState?.Hand,
                playerState?.Deck,
                playerState?.Field,
                playerState?.Graveyard,
                playerState?.Extra,
                playerState?.Emblem
            })
            {
                if (zone == null)
                {
                    continue;
                }

                foreach (var card in zone)
                {
                    if (card == null || !seen.Add(card.InstanceId))
                    {
                        continue;
                    }

                    yield return card;
                }
            }
        }
    }
}
