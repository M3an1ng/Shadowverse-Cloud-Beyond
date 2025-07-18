using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Extensions
{
    /// <summary>
    /// 扩展方法，用于CardInstance到CardInstanceDto的完整映射
    /// 包含从CardDefinition获取的定义信息
    /// </summary>
    public static class CardInstanceExtensions
    {
        /// <summary>
        /// 将CardInstance转换为完整的CardInstanceDto，包含定义信息
        /// </summary>
        /// <param name="cardInstance">卡牌实例</param>
        /// <param name="gameDataProvider">游戏数据提供者</param>
        /// <param name="mapper">AutoMapper实例</param>
        /// <returns>完整的CardInstanceDto</returns>
        public static CardInstanceDto ToCompleteDto(this CardInstance cardInstance,
            IGameDataProvider gameDataProvider, IMapper mapper)
        {
            // 首先使用AutoMapper进行基础映射
            var dto = mapper.Map<CardInstanceDto>(cardInstance);

            // 获取卡牌定义信息
            var cardDefinition = gameDataProvider.GetCardDefinition(cardInstance.DefinitionId);
            if (cardDefinition != null)
            {
                // 【BUG-023修复】使用with表达式填充定义信息，并确保动态状态（所有者和区域）也被正确映射
                dto = dto with
                {
                    Name = cardDefinition.Name,
                    Description = cardDefinition.Description ?? "",
                    // 确保基础属性正确设置
                    Attack = cardDefinition.Attack ?? 0,
                    Health = cardDefinition.Health ?? 0,
                    Cost = cardDefinition.Cost,
                    MaxAttack = cardDefinition.Attack ?? 0,
                    // 显式地从实例中映射动态数据，防止被AutoMapper的默认值或不正确的映射覆盖
                    OwnerPlayerId = cardInstance.OwnerPlayerId,
                    Zone = (CardZone)cardInstance.CurrentZone
                };
            }

            return dto;
        }

        /// <summary>
        /// 批量转换CardInstance列表为完整的CardInstanceDto列表
        /// </summary>
        /// <param name="cardInstances">卡牌实例列表</param>
        /// <param name="gameDataProvider">游戏数据提供者</param>
        /// <param name="mapper">AutoMapper实例</param>
        /// <returns>完整的CardInstanceDto列表</returns>
        public static List<CardInstanceDto> ToCompleteDtos(this IEnumerable<CardInstance> cardInstances,
            IGameDataProvider gameDataProvider, IMapper mapper)
        {
            return cardInstances.Select(instance => instance.ToCompleteDto(gameDataProvider, mapper)).ToList();
        }
    }
}
