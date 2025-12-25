using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic; // Required for List
using System.Linq; // Required for .Any() if used, though not strictly needed here
using ShadowverseBeyond.Server.Dtos.Enums;
using System;

namespace ShadowverseBeyond.Server.Mappings
{
    /// <summary>
    /// Configures AutoMapper profiles for mapping between CardDefinition entities and DTOs.
    /// </summary>
    public class CardDefinitionProfile : Profile
    {
        public CardDefinitionProfile()
        {
            CreateMap<CardDefinition, CardDefinitionDto>()
                // Map Id (Entity) to CardId (DTO)
                .ForMember(dest => dest.CardId, opt => opt.MapFrom(src => src.Id))
                // Map CanEvolve (DTO) based on whether EvolveId (Entity) has a value
                .ForMember(dest => dest.CanEvolve, opt => opt.MapFrom(src => src.EvolveId.HasValue))
                // Ensure Keywords list is initialized even if source is null
                .ForMember(dest => dest.Keywords, opt => opt.NullSubstitute(new List<string>()))
                .ForMember(dest => dest.Rarity, opt => opt.MapFrom(src => src.Rarity.ToString()))
                .ForMember(dest => dest.Class, opt => opt.MapFrom(src => src.Class.ToString()));

            // Properties with matching names and types are mapped automatically by convention:
            // Name, Type, Rarity, CardSet, Cost, Attack, Health, Description

            // Properties only in Entity (EffectsJson, IsCollectible, IsEvolved, AbilityId, Effects)
            // are ignored by default as they don't exist in the DTO.

            CreateMap<CardUpsertDto, CardDefinition>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id ?? 0))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => ParseEnum<CardType>(src.Type, CardType.Follower)))
                .ForMember(dest => dest.Rarity, opt => opt.MapFrom(src => ParseEnum<CardRarity>(src.Rarity, CardRarity.Bronze)))
                .ForMember(dest => dest.Class, opt => opt.MapFrom(src => ParseEnum<CardClass>(src.Class, CardClass.Neutral)))
                .ForMember(dest => dest.CardSet, opt => opt.MapFrom(src => src.CardSet ?? "Basic"))
                .ForMember(dest => dest.EvolveId, opt => opt.MapFrom(src => src.CanEvolve == true ? src.EvolveId : null))
                .ForMember(dest => dest.Keywords, opt => opt.MapFrom(src => src.Keywords ?? new List<string>()))
                .ForMember(dest => dest.Tribe, opt => opt.MapFrom(src => src.Tribe))
                .ForMember(dest => dest.AbilityIds, opt => opt.MapFrom(src => src.AbilityIds ?? new List<long>()));
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
        }
    }
}
