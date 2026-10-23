using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Mappings
{
    public class PlayerStateProfile : Profile
    {
        public PlayerStateProfile()
        {
            // Use the custom type converter to handle the complex mapping
            // that requires dependency injection.
            CreateMap<PlayerState, PlayerStateDto>()
                .ForMember(dest => dest.MaxPP, opt => opt.MapFrom(src => src.MaxPlayPoints))
                .ForMember(dest => dest.CurrentPP, opt => opt.MapFrom(src => src.CurrentPlayPoints))
                .ForMember(dest => dest.PpBoostUsesAvailable, opt => opt.MapFrom(src => src.PpBoostUsesAvailable))
                .ForMember(dest => dest.IsPpBoostArmed, opt => opt.MapFrom(src => src.IsPpBoostArmed))
                .ForMember(dest => dest.MaxEP, opt => opt.MapFrom(src => src.MaxEvolutionPoints))
                .ForMember(dest => dest.CurrentEP, opt => opt.MapFrom(src => src.CurrentEvolutionPoints))
                .ForMember(dest => dest.IsEvolutionUnlocked, opt => opt.MapFrom(src => src.IsEvolutionUnlocked))
                .ForMember(dest => dest.CanEvolve, opt => opt.MapFrom(src => src.CanEvolve))
                .ForMember(dest => dest.MaxSEP, opt => opt.MapFrom(src => src.MaxSuperEvolutionPoints))
                .ForMember(dest => dest.CurrentSEP, opt => opt.MapFrom(src => src.CurrentSuperEvolutionPoints))
                .ForMember(dest => dest.IsSuperEvolutionUnlocked, opt => opt.MapFrom(src => src.IsSuperEvolutionUnlocked))
                .ForMember(dest => dest.CanSuperEvolve, opt => opt.MapFrom(src => src.CanSuperEvolve))
                .ForMember(dest => dest.Class, opt => opt.MapFrom(src => src.Class))
                .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName))
                .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.AvatarUrl))
                .ForMember(dest => dest.GraveyardCount, opt => opt.MapFrom(src => src.GraveyardCount))
                .ForMember(dest => dest.BloodPool, opt => opt.MapFrom(src => src.BloodPool))
                .ForMember(dest => dest.Counters, opt => opt.MapFrom(src => BuildCounterSnapshot(src)))
                .ForMember(dest => dest.CounterDisplayNames, opt => opt.MapFrom(src => BuildCounterDisplayNameSnapshot(src)))
                .ForMember(dest => dest.CounterSummaryEntries, opt => opt.MapFrom(src => BuildCounterSummaryEntries(src)))
                .ForMember(dest => dest.LeaderCard, opt => opt.MapFrom(src => src.Leader))
                // Field 已不再包含主战者；这里保留防御性过滤，避免遗留状态误入前端。
                .ForMember(dest => dest.Field, opt => opt.MapFrom(src => src.Field.Where(card => card == null || !card.IsLeader)))
                .ForMember(dest => dest.Emblem, opt => opt.MapFrom(src => src.Emblem));
        }

        private static Dictionary<string, int> BuildCounterSnapshot(PlayerState playerState)
        {
            if (playerState == null)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

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

            if (playerState == null)
            {
                return result;
            }

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

            if (playerState == null)
            {
                return entries;
            }

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
