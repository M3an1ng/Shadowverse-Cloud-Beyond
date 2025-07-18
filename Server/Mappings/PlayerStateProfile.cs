using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic; // Required for List

namespace ShadowverseBeyond.Server.Mappings
{
    /// <summary>
    /// Configures AutoMapper profiles for mapping between PlayerState entities and DTOs.
    /// </summary>
    public class PlayerStateProfile : Profile
    {
        public PlayerStateProfile()
        {
            CreateMap<PlayerState, PlayerStateDto>()
                // Map properties with different names
                .ForMember(dest => dest.MaxPP, opt => opt.MapFrom(src => src.MaxPlayPoints))
                .ForMember(dest => dest.CurrentPP, opt => opt.MapFrom(src => src.CurrentPlayPoints))
                .ForMember(dest => dest.MaxEP, opt => opt.MapFrom(src => src.MaxEvolutionPoints))
                .ForMember(dest => dest.CurrentEP, opt => opt.MapFrom(src => src.CurrentEvolutionPoints))
                // 【临时】回到自动映射，依赖修复后的CardInstanceProfile
                .ForMember(dest => dest.LeaderCard, opt => opt.MapFrom(src => src.Leader));

            // Properties with matching names and types are mapped automatically:
            // PlayerId, IsAI, IsFirst, CanEvolve, TurnNumber, PlayedCardCountThisTurn,
            // HasCompletedMulligan, HasTakenFirstTurn, DeckCount

            // 【修复】Collections现在使用自定义解析器，确保包含完整的卡牌定义信息
            // Note: The DTO's Deck property is intentionally not mapped from the Entity's Deck list.
        }
    }
}