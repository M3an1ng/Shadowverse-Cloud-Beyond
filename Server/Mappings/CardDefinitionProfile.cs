using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic; // Required for List
using System.Linq; // Required for .Any() if used, though not strictly needed here

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
                .ForMember(dest => dest.Keywords, opt => opt.NullSubstitute(new List<string>()));

            // Properties with matching names and types are mapped automatically by convention:
            // Name, Type, Rarity, CardSet, Cost, Attack, Health, Description

            // Properties only in Entity (EffectsJson, IsCollectible, IsEvolved, AbilityId, Effects)
            // are ignored by default as they don't exist in the DTO.
        }
    }
}