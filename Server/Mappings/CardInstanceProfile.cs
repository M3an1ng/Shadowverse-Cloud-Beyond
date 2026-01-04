using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Domain.Constants; // For CardKeyword enum
using System;
using System.Collections.Generic; // Required for List
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Mappings
{
    /// <summary>
    /// Configures AutoMapper profiles for mapping between CardInstance entities and DTOs.
    /// 【修复】现在包含定义属性的映射，确保前端能正确显示卡牌信息
    /// </summary>
    public class CardInstanceProfile : Profile
    {
        public CardInstanceProfile()
        {
            CreateMap<CardInstance, CardInstanceDto>()
                // --- Instance & Definition Identifiers ---
                .ForMember(dest => dest.InstanceId, opt => opt.MapFrom(src => src.InstanceId))
                .ForMember(dest => dest.CardId, opt => opt.MapFrom(src => src.CurrentDefinitionId))

                // --- Static Data (from computed properties in CardInstance that need explicit mapping) ---
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.Rarity, opt => opt.MapFrom(src => src.Rarity))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.CardType))
                .ForMember(dest => dest.Class, opt => opt.MapFrom(src => src.Class))
                .ForMember(dest => dest.Tribe, opt => opt.MapFrom(src => src.Tribe))

                // --- Base Stats (Mapping from direct properties) ---
                .ForMember(dest => dest.Attack, opt => opt.MapFrom(src => src.BaseAttack))
                .ForMember(dest => dest.Health, opt => opt.MapFrom(src => src.BaseHealth))
                .ForMember(dest => dest.Cost, opt => opt.MapFrom(src => src.BaseCost))

                // --- Dynamic State (Mapping properties with same name but for clarity) ---
                .ForMember(dest => dest.CurrentCost, opt => opt.MapFrom(src => src.CurrentCost))
                .ForMember(dest => dest.CurrentAttack, opt => opt.MapFrom(src => src.CurrentAttack))
                .ForMember(dest => dest.CurrentHealth, opt => opt.MapFrom(src => src.CurrentHealth))
                .ForMember(dest => dest.MaxHealth, opt => opt.MapFrom(src => src.MaxHealth))
                .ForMember(dest => dest.IsEvolved, opt => opt.MapFrom(src => src.IsEvolved))
                .ForMember(dest => dest.CanAttack, opt => opt.MapFrom(src => src.CanAttack))
                .ForMember(dest => dest.HasActAbility, opt => opt.MapFrom(src => src.HasActAbility))
                .ForMember(dest => dest.ActLastUsedTurnNumber, opt => opt.MapFrom(src => src.ActLastUsedTurnNumber))
                .ForMember(dest => dest.AttacksThisTurn, opt => opt.MapFrom(src => src.AttacksThisTurn))
                .ForMember(dest => dest.FieldTurn, opt => opt.MapFrom(src => src.FieldTurn)) // BUG修复：添加缺失的FieldTurn映射
                .ForMember(dest => dest.CurrentCountdown, opt => opt.MapFrom(src => src.CurrentCountdown))
                .ForMember(dest => dest.MaxAttacksPerTurn, opt => opt.MapFrom(src => src.MaxAttacksPerTurn))
                .ForMember(dest => dest.Zone, opt => opt.MapFrom(src => src.CurrentZone))
                .ForMember(dest => dest.OwnerPlayerId, opt => opt.MapFrom(src => src.OwnerPlayerId))
                .ForMember(dest => dest.Keywords, opt => opt.MapFrom(src => src.Keywords))

                // --- Evolution Info ---
                .ForMember(dest => dest.CanEvolve, opt => opt.MapFrom(src => src.CanEvolve()));
        }
    }
}
