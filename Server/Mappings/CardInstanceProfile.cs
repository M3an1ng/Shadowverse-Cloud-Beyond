using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum
using System;
using System.Collections.Generic; // Required for List

namespace ShadowverseBeyond.Server.Mappings
{
    /// <summary>
    /// Configures AutoMapper profiles for mapping between CardInstance entities and DTOs.
    /// This profile focuses on mapping the *current state* of a card instance.
    /// Definition-based properties (Name, base Cost, base Attack/Health, Description)
    /// are intentionally NOT mapped here, as they should come from CardDefinitionDto
    /// when constructing the full view model client-side or in a higher service layer.
    /// </summary>
    public class CardInstanceProfile : Profile
    {
        public CardInstanceProfile()
        {
            CreateMap<CardInstance, CardInstanceDto>()
                // InstanceId now maps directly (Guid to Guid)
                .ForMember(dest => dest.InstanceId, opt => opt.MapFrom(src => src.InstanceId))
                // Map DefinitionId to CardId
                .ForMember(dest => dest.CardId, opt => opt.MapFrom(src => src.DefinitionId))
                // Map CurrentZone to Zone
                .ForMember(dest => dest.Zone, opt => opt.MapFrom(src => src.CurrentZone))
                // Map CardType to Type
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.CardType))
                // Map Countdown to CurrentCountdown
                .ForMember(dest => dest.CurrentCountdown, opt => opt.MapFrom(src => src.Countdown))
                // Ensure Keywords list is initialized even if source is null (though unlikely here)
                .ForMember(dest => dest.Keywords, opt => opt.NullSubstitute(new List<CardKeyword>()));

            // Properties with matching names and types are mapped automatically:
            // CurrentCost, CurrentAttack, CurrentHealth, MaxHealth, IsEvolved,
            // FieldPosition, CanAttack, CanEvolve

            // DTO properties representing definition data (Name, Cost, Attack, Health, Description,
            // EffectDescriptions, MaxAttacksPerTurn, MaxAttack) are ignored as they don't exist in CardInstance.

            // Entity properties not in DTO (BaseKeywords, TemporaryKeywords, ActiveEffects, AbilityState)
            // are ignored automatically.
        }
    }
}