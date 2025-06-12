// File: Server/Services/CardStatsService.cs
//
// 相关文档:
// - Buff系统: 文档/05-游戏系统/10-动态效果与Buff系统.md
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Buffs;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// 提供考虑Buff效果的卡牌属性计算服务
    /// </summary>
    public interface ICardStatsService
    {
        /// <summary>
        /// 获取卡牌的有效攻击力（考虑Buff效果）
        /// </summary>
        Task<int> GetEffectiveAttackAsync(CardInstance card);

        /// <summary>
        /// 获取卡牌的有效最大生命值（考虑Buff效果）
        /// </summary>
        Task<int> GetEffectiveMaxHealthAsync(CardInstance card);

        /// <summary>
        /// 获取卡牌的有效费用（考虑Buff效果）
        /// </summary>
        Task<int> GetEffectiveCostAsync(CardInstance card);

        /// <summary>
        /// 获取卡牌的有效关键字列表（考虑Buff效果）
        /// </summary>
        Task<List<string>> GetEffectiveKeywordsAsync(CardInstance card);

        /// <summary>
        /// 获取卡牌的有效当前生命值（最大生命值 - 伤害）
        /// </summary>
        Task<int> GetEffectiveCurrentHealthAsync(CardInstance card);
    }

    /// <summary>
    /// 卡牌属性计算服务实现
    /// </summary>
    public class CardStatsService : ICardStatsService
    {
        private readonly IBuffManager _buffManager;
        private readonly ILogger<CardStatsService> _logger;

        public CardStatsService(IBuffManager buffManager, ILogger<CardStatsService> logger)
        {
            _buffManager = buffManager ?? throw new ArgumentNullException(nameof(buffManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<int> GetEffectiveAttackAsync(CardInstance card)
        {
            if (card == null)
            {
                _logger.LogWarning("GetEffectiveAttackAsync called with null card");
                return 0;
            }

            try
            {
                return await card.GetEffectiveAttackAsync(_buffManager);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating effective attack for card {CardInstanceId}", card.InstanceId);
                return card.CurrentAttack; // Fallback to stored value
            }
        }

        /// <inheritdoc />
        public async Task<int> GetEffectiveMaxHealthAsync(CardInstance card)
        {
            if (card == null)
            {
                _logger.LogWarning("GetEffectiveMaxHealthAsync called with null card");
                return 0;
            }

            try
            {
                return await card.GetEffectiveMaxHealthAsync(_buffManager);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating effective max health for card {CardInstanceId}", card.InstanceId);
                return card.MaxHealth; // Fallback to stored value
            }
        }

        /// <inheritdoc />
        public async Task<int> GetEffectiveCostAsync(CardInstance card)
        {
            if (card == null)
            {
                _logger.LogWarning("GetEffectiveCostAsync called with null card");
                return 0;
            }

            try
            {
                return await card.GetEffectiveCostAsync(_buffManager);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating effective cost for card {CardInstanceId}", card.InstanceId);
                return card.CurrentCost; // Fallback to stored value
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> GetEffectiveKeywordsAsync(CardInstance card)
        {
            if (card == null)
            {
                _logger.LogWarning("GetEffectiveKeywordsAsync called with null card");
                return new List<string>();
            }

            try
            {
                var keywordEnums = await card.GetEffectiveKeywordsAsync(_buffManager);
                return keywordEnums.Select(k => k.ToString()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating effective keywords for card {CardInstanceId}", card.InstanceId);
                return card.Keywords.Select(k => k.ToString()).ToList(); // Fallback to stored keywords
            }
        }

        /// <inheritdoc />
        public async Task<int> GetEffectiveCurrentHealthAsync(CardInstance card)
        {
            if (card == null)
            {
                _logger.LogWarning("GetEffectiveCurrentHealthAsync called with null card");
                return 0;
            }

            try
            {
                var effectiveMaxHealth = await GetEffectiveMaxHealthAsync(card);
                return Math.Max(0, effectiveMaxHealth - card.DamageTaken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating effective current health for card {CardInstanceId}", card.InstanceId);
                return card.CurrentHealth; // Fallback to stored value
            }
        }
    }
}
