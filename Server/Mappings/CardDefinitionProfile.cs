using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic; // Required for List
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
                // Ensure Keywords list is initialized even if source is null
                .ForMember(dest => dest.Keywords, opt => opt.NullSubstitute(new List<string>()))
                .ForMember(dest => dest.IsCollectible, opt => opt.MapFrom(src => src.IsCollectible))
                .ForMember(dest => dest.Rarity, opt => opt.MapFrom(src => src.Rarity.ToString()))
                .ForMember(dest => dest.Class, opt => opt.MapFrom(src => src.Class.ToString()))
                .ForMember(dest => dest.CardSet, opt => opt.MapFrom(src => src.CardSetDefinition != null ? src.CardSetDefinition.Name : src.CardSet))
                .ForMember(dest => dest.CardSetDefinitionId, opt => opt.MapFrom(src => src.CardSetDefinitionId))
                .ForMember(dest => dest.CardSetCode, opt => opt.MapFrom(src => src.CardSetDefinition != null ? src.CardSetDefinition.SetCode : null))
                .ForMember(dest => dest.CardSetAbbr, opt => opt.MapFrom(src => src.CardSetDefinition != null ? src.CardSetDefinition.Abbreviation : null))
                .ForMember(dest => dest.CardSetEnvironment, opt => opt.MapFrom(src => src.CardSetDefinition != null ? src.CardSetDefinition.Environments : null));

            // Properties with matching names and types are mapped automatically by convention:
            // Name, Type, Rarity, CardSet, Cost, Attack, Health, Description

            // Properties only in Entity (EffectsJson, IsEvolved, AbilityId, Effects)
            // are ignored by default as they don't exist in the DTO.

            CreateMap<CardUpsertDto, CardDefinition>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id ?? 0))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => ParseEnum<CardType>(src.Type, CardType.Follower)))
                .ForMember(dest => dest.Rarity, opt => opt.MapFrom(src => ParseEnum<CardRarity>(src.Rarity, CardRarity.Bronze)))
                .ForMember(dest => dest.Class, opt => opt.MapFrom(src => ParseEnum<CardClass>(src.Class, CardClass.Neutral)))
                .ForMember(dest => dest.CardSet, opt => opt.MapFrom(src => src.CardSet ?? "Legacy"))
                .ForMember(dest => dest.CardSetDefinitionId, opt => opt.MapFrom(src => src.CardSetDefinitionId))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description ?? string.Empty))
                .ForMember(dest => dest.Keywords, opt => opt.MapFrom(src => src.Keywords ?? new List<string>()))
                .ForMember(dest => dest.Tribe, opt => opt.MapFrom(src => src.Tribe))
                .ForMember(dest => dest.AbilityIds, opt => opt.MapFrom(src => src.AbilityIds ?? new List<long>()))
                .ForMember(dest => dest.IsCollectible, opt => opt.MapFrom(src => src.IsCollectible ?? true));
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
        }
    }
}
