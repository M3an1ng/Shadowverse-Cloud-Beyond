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
                .ForMember(dest => dest.MaxEP, opt => opt.MapFrom(src => src.MaxEvolutionPoints))
                .ForMember(dest => dest.CurrentEP, opt => opt.MapFrom(src => src.CurrentEvolutionPoints))
                .ForMember(dest => dest.MaxSEP, opt => opt.MapFrom(src => src.MaxSuperEvolutionPoints))
                .ForMember(dest => dest.CurrentSEP, opt => opt.MapFrom(src => src.CurrentSuperEvolutionPoints))
                .ForMember(dest => dest.CanSuperEvolve, opt => opt.MapFrom(src => src.CanSuperEvolve))
                .ForMember(dest => dest.LeaderCard, opt => opt.MapFrom(src => src.Leader))
                // 【BUG修复】在将领域实体映射到DTO时，从Field列表中过滤掉主战者。
                // 这确保了前端渲染时不会将主战者错误地当作场上的卡牌。
                // 后端的领域模型中，主战者仍然在Field列表中，以确保目标选择逻辑的正确性。
                .ForMember(dest => dest.Field, opt => opt.MapFrom(src => src.Field.Where(card => src.Leader != null && card.InstanceId != src.Leader.InstanceId)));
        }
    }
}
