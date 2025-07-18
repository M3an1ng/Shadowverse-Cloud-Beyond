using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum
using System;
using System.Collections.Generic; // Required for List
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions; // For IGameDataProvider

namespace ShadowverseBeyond.Server.Mappings
{
    /// <summary>
    /// Configures AutoMapper profiles for mapping between CardInstance entities and DTOs.
    /// 【修复】现在包含定义属性的映射，确保前端能正确显示卡牌信息
    /// </summary>
    public class CardInstanceProfile : Profile
    {
        public CardInstanceProfile(IGameDataProvider gameDataProvider)
        {
            CreateMap<CardInstance, CardInstanceDto>()
                // InstanceId now maps directly (Guid to Guid)
                .ForMember(dest => dest.InstanceId, opt => opt.MapFrom(src => src.InstanceId))
                // 【BUG-023修复】显式映射OwnerPlayerId以确保数据完整性
                .ForMember(dest => dest.OwnerPlayerId, opt => opt.MapFrom(src => src.OwnerPlayerId))
                // Map DefinitionId to CardId
                .ForMember(dest => dest.CardId, opt => opt.MapFrom(src => src.DefinitionId))
                // Map CurrentZone to Zone
                .ForMember(dest => dest.Zone, opt => opt.MapFrom(src => src.CurrentZone))
                // Map CardType to Type
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.CardType))
                // Map Countdown to CurrentCountdown
                .ForMember(dest => dest.CurrentCountdown, opt => opt.MapFrom(src => src.Countdown))
                // Ensure Keywords list is initialized even if source is null (though unlikely here)
                .ForMember(dest => dest.Keywords, opt => opt.NullSubstitute(new List<CardKeyword>()))
                // 【修复】映射当前攻击力和生命值到显示属性（前端显示当前值）
                .ForMember(dest => dest.Attack, opt => opt.MapFrom(src => src.CurrentAttack))
                .ForMember(dest => dest.Health, opt => opt.MapFrom(src => src.CurrentHealth))
                .ForMember(dest => dest.MaxAttack, opt => opt.MapFrom(src => src.BaseAttack))  // MaxAttack使用基础值
                .ForMember(dest => dest.Cost, opt => opt.MapFrom(src => src.CurrentCost))      // Cost使用当前值
                                                                                               // 【修复】映射Name和Description从CardInstance的属性
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .AfterMap((src, dest, context) =>
                {
                    if (src.EvolveDefinitionId.HasValue)
                    {
                        var evolvedDef = gameDataProvider.GetCardDefinition(src.EvolveDefinitionId.Value);
                        if (evolvedDef != null)
                        {
                            dest.EvolvedState = context.Mapper.Map<CardDefinitionDto>(evolvedDef);
                        }
                    }
                });

            // Properties with matching names and types are mapped automatically:
            // CurrentCost, CurrentAttack, CurrentHealth, MaxHealth, IsEvolved,
            // FieldPosition, CanAttack, CanEvolve

            // Entity properties not in DTO (BaseKeywords, TemporaryKeywords, ActiveEffects, AbilityState)
            // are ignored automatically.
        }
    }
}