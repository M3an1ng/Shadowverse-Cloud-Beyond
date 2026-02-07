using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
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
                .ForMember(dest => dest.MaxEP, opt => opt.MapFrom(src => src.MaxEvolutionPoints))
                .ForMember(dest => dest.CurrentEP, opt => opt.MapFrom(src => src.CurrentEvolutionPoints))
                .ForMember(dest => dest.MaxSEP, opt => opt.MapFrom(src => src.MaxSuperEvolutionPoints))
                .ForMember(dest => dest.CurrentSEP, opt => opt.MapFrom(src => src.CurrentSuperEvolutionPoints))
                .ForMember(dest => dest.CanSuperEvolve, opt => opt.MapFrom(src => src.CanSuperEvolve))
                .ForMember(dest => dest.GraveyardCount, opt => opt.MapFrom(src => src.GraveyardCount))
                .ForMember(dest => dest.BloodPool, opt => opt.MapFrom(src => src.BloodPool))
                .ForMember(dest => dest.LeaderCard, opt => opt.MapFrom(src => src.Leader))
                // Field 已不再包含主战者；这里保留防御性过滤，避免遗留状态误入前端。
                .ForMember(dest => dest.Field, opt => opt.MapFrom(src => src.Field.Where(card => card == null || !card.IsLeader)))
                .ForMember(dest => dest.Emblem, opt => opt.MapFrom(src => src.Emblem));
        }
    }
}
