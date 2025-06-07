//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 负责从数据库加载能力配置并构建运行时AbilityConfig对象
    /// 支持DSL、JSON和混合格式的能力配置
    /// </summary>
    public class AbilityLoader
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AbilityLoader> _logger;

        public AbilityLoader(
            ApplicationDbContext context,
            ILogger<AbilityLoader> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 加载指定卡牌的所有能力配置
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        /// <returns>能力配置列表</returns>
        public async Task<List<AbilityConfig>> LoadAbilitiesForCardAsync(int cardId)
        {
            try
            {
                _logger.LogDebug("Loading abilities for card {CardId}", cardId);

                var cardAbilities = await _context.Set<CardAbility>()
                    .Where(ca => ca.CardId == cardId && ca.IsActive)
                    .OrderBy(ca => ca.Priority)
                    .ToListAsync();

                var configs = new List<AbilityConfig>();

                await ProcessCardAbilitiesAsync(cardAbilities, configs);

                _logger.LogDebug("Loaded {Count} abilities for card {CardId}", configs.Count, cardId);
                return configs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load abilities for card {CardId}", cardId);
                return new List<AbilityConfig>();
            }
        }

        /// <summary>
        /// 加载指定ID的能力配置
        /// </summary>
        /// <param name="abilityId">能力ID</param>
        /// <returns>能力配置，如果不存在则返回null</returns>
        public async Task<AbilityConfig?> LoadAbilityByIdAsync(long abilityId)
        {
            try
            {
                _logger.LogDebug("Loading ability {AbilityId}", abilityId);

                var cardAbility = await _context.Set<CardAbility>()
                    .FirstOrDefaultAsync(ca => ca.Id == abilityId && ca.IsActive);

                if (cardAbility == null)
                {
                    _logger.LogWarning("Ability {AbilityId} not found or inactive", abilityId);
                    return null;
                }

                return await ConvertToAbilityConfigAsync(cardAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load ability {AbilityId}", abilityId);
                return null;
            }
        }

        /// <summary>
        /// 加载所有激活的能力配置
        /// </summary>
        /// <returns>所有能力配置列表</returns>
        public async Task<List<AbilityConfig>> LoadAllAbilitiesAsync()
        {
            try
            {
                _logger.LogDebug("Loading all active abilities");

                var cardAbilities = await _context.Set<CardAbility>()
                    .Where(ca => ca.IsActive)
                    .OrderBy(ca => ca.CardId)
                    .ThenBy(ca => ca.Priority)
                    .ToListAsync();

                var configs = new List<AbilityConfig>();

                await ProcessCardAbilitiesAsync(cardAbilities, configs);

                _logger.LogDebug("Loaded {Count} abilities total", configs.Count);
                return configs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load all abilities");
                return new List<AbilityConfig>();
            }
        }

        /// <summary>
        /// 处理CardAbility列表，转换为AbilityConfig并添加到结果列表中
        /// </summary>
        /// <param name="cardAbilities">CardAbility列表</param>
        /// <param name="configs">结果配置列表</param>
        private async Task ProcessCardAbilitiesAsync(List<CardAbility> cardAbilities, List<AbilityConfig> configs)
        {
            foreach (var cardAbility in cardAbilities)
            {
                try
                {
                    var config = await ConvertToAbilityConfigAsync(cardAbility);
                    if (config != null)
                    {
                        configs.Add(config);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to convert CardAbility {AbilityId} to AbilityConfig", cardAbility.Id);
                }
            }
        }

        /// <summary>
        /// 将CardAbility实体转换为AbilityConfig对象
        /// </summary>
        /// <param name="cardAbility">数据库实体</param>
        /// <returns>能力配置对象</returns>
        private async Task<AbilityConfig?> ConvertToAbilityConfigAsync(CardAbility cardAbility)
        {
            try
            {
                return ParseFromJson(cardAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert CardAbility {AbilityId} with format {Format}",
                    cardAbility.Id, cardAbility.ConfigFormat);
                return null;
            }
        }

        /// <summary>
        /// 从DSL源码解析能力配置
        /// </summary>
        private AbilityConfig? ParseFromDsl(CardAbility cardAbility)
        {
            if (string.IsNullOrEmpty(cardAbility.DslSource))
            {
                _logger.LogWarning("DSL source is empty for ability {AbilityId}", cardAbility.Id);
                return null;
            }

            try
            {
                // 使用现有的DSL解析功能
                var config = AbilityConfig.FromDsl(cardAbility.DslSource);

                // 设置数据库中的基本信息
                config.Id = cardAbility.Id.ToString();
                config.CardId = cardAbility.CardId;
                config.Name = cardAbility.Name;
                config.Description = cardAbility.Description;
                config.AbilityTag = cardAbility.AbilityTag;
                config.IsActive = cardAbility.IsActive;

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse DSL for ability {AbilityId}: {DslSource}",
                    cardAbility.Id, cardAbility.DslSource);
                return null;
            }
        }

        /// <summary>
        /// 从JSON配置解析能力配置
        /// </summary>
        private AbilityConfig? ParseFromJson(CardAbility cardAbility)
        {
            if (string.IsNullOrEmpty(cardAbility.JsonConfig))
            {
                _logger.LogWarning("JSON config is empty for ability {AbilityId}", cardAbility.Id);
                return null;
            }

            try
            {
                var config = JsonSerializer.Deserialize<AbilityConfig>(cardAbility.JsonConfig);
                if (config == null)
                {
                    _logger.LogWarning("Failed to deserialize JSON config for ability {AbilityId}", cardAbility.Id);
                    return null;
                }

                // 确保数据库中的基本信息优先
                config.Id = cardAbility.Id.ToString();
                config.CardId = cardAbility.CardId;
                config.Name = cardAbility.Name;
                config.Description = cardAbility.Description;
                config.AbilityTag = cardAbility.AbilityTag;
                config.IsActive = cardAbility.IsActive;

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse JSON for ability {AbilityId}: {JsonConfig}",
                    cardAbility.Id, cardAbility.JsonConfig);
                return null;
            }
        }

        /// <summary>
        /// 从混合格式解析能力配置（优先使用DSL，回退到JSON）
        /// </summary>
        private Task<AbilityConfig?> ParseFromMixedAsync(CardAbility cardAbility)
        {
            // 优先尝试DSL
            if (!string.IsNullOrEmpty(cardAbility.DslSource))
            {
                var dslConfig = ParseFromDsl(cardAbility);
                if (dslConfig != null)
                {
                    return Task.FromResult<AbilityConfig?>(dslConfig);
                }
            }

            // 回退到JSON
            if (!string.IsNullOrEmpty(cardAbility.JsonConfig))
            {
                var jsonConfig = ParseFromJson(cardAbility);
                return Task.FromResult(jsonConfig);
            }

            _logger.LogWarning("Both DSL and JSON are empty for mixed format ability {AbilityId}", cardAbility.Id);
            return Task.FromResult<AbilityConfig?>(null);
        }
    }
}
