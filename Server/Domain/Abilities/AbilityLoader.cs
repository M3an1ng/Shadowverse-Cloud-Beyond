//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
//
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 负责从数据库加载能力配置并构建运行时AbilityConfig对象
    /// </summary>
    public class AbilityLoader
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AbilityLoader> _logger;

        /// <summary>
        /// 构造函数：使用数据库直接访问模式
        /// </summary>
        public AbilityLoader(
            ApplicationDbContext context,
            ILogger<AbilityLoader> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 加载指定卡牌的所有能力配置（数据库访问模式）
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        /// <returns>能力配置列表</returns>
        public async Task<List<AbilityConfig>> LoadAbilitiesForCardAsync(int cardId)
        {
            if (_context == null)
            {
                // In a real application, you'd want more robust error handling.
                return [];
            }

            try
            {
                var cardAbilities = await _context.Set<AbilityDefinition>()
                    .Where(ca => ca.CardId == cardId && ca.IsActive)
                    .OrderBy(ca => ca.Priority)
                    .ToListAsync();

                var configs = new List<AbilityConfig>();

                ProcessCardAbilities(cardAbilities, configs);

                return configs;
            }
            catch (Exception)
            {
                // In a real application, you'd want more robust error handling.
                return [];
            }
        }

        /// <summary>
        /// 加载指定ID的能力配置
        /// </summary>
        /// <param name="abilityId">能力ID</param>
        /// <returns>能力配置，如果不存在则返回null</returns>
        public async Task<AbilityConfig?> LoadAbilityByIdAsync(long abilityId)
        {
            if (_context == null)
            {
                // In a real application, you'd want more robust error handling.
                return null;
            }

            try
            {
                var cardAbility = await _context.Set<AbilityDefinition>()
                    .FirstOrDefaultAsync(ca => ca.Id == abilityId && ca.IsActive);

                if (cardAbility == null)
                {
                    return null;
                }

                return ConvertToAbilityConfig(cardAbility);
            }
            catch (Exception)
            {
                // In a real application, you'd want more robust error handling.
                return null;
            }
        }

        /// <summary>
        /// 加载所有激活的能力配置
        /// </summary>
        /// <returns>所有能力配置列表</returns>
        public async Task<List<AbilityConfig>> LoadAllAbilitiesAsync()
        {
            if (_context == null)
            {
                // In a real application, you'd want more robust error handling.
                return [];
            }

            try
            {
                var cardAbilities = await _context.Set<AbilityDefinition>()
                    .Where(ca => ca.IsActive)
                    .OrderBy(ca => ca.CardId)
                    .ThenBy(ca => ca.Priority)
                    .ToListAsync();

                var configs = new List<AbilityConfig>();

                ProcessCardAbilities(cardAbilities, configs);

                return configs;
            }
            catch (Exception)
            {
                // In a real application, you'd want more robust error handling.
                return [];
            }
        }

        /// <summary>
        /// 处理CardAbility列表，转换为AbilityConfig并添加到结果列表中
        /// </summary>
        /// <param name="cardAbilities">CardAbility列表</param>
        /// <param name="configs">结果配置列表</param>
        private void ProcessCardAbilities(List<AbilityDefinition> cardAbilities, List<AbilityConfig> configs)
        {
            foreach (var cardAbility in cardAbilities)
            {
                try
                {
                    var config = ConvertToAbilityConfig(cardAbility);
                    if (config != null)
                    {
                        configs.Add(config);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to convert AbilityDefinition with ID {AbilityId} for CardId {CardId}.", cardAbility.Id, cardAbility.CardId);
                }
            }
        }

        /// <summary>
        /// 将CardAbility实体转换为AbilityConfig对象
        /// </summary>
        /// <param name="cardAbility">数据库实体</param>
        /// <returns>能力配置对象</returns>
        private AbilityConfig? ConvertToAbilityConfig(AbilityDefinition cardAbility)
        {
            try
            {
                // 目前只支持JSON配置格式
                var isJsonConfigNullOrEmpty = string.IsNullOrEmpty(cardAbility.JsonConfig);
                _logger.LogTrace("Is JsonConfig null or empty for AbilityDefinition Id {AbilityId}? {Result}", cardAbility.Id, isJsonConfigNullOrEmpty);

                if (isJsonConfigNullOrEmpty)
                {
                    _logger.LogWarning("JsonConfig is null or empty for AbilityDefinition Id: {AbilityId}. Skipping conversion.", cardAbility.Id);
                    return null;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Handle camelCase from JSON
                    // Allow case-insensitive matching for enums by not specifying a naming policy for the converter.
                    // This will correctly map "Fixed" to ValueType.Fixed and "Passive" to AbilityType.Passive.
                    Converters = { new LenientEffectKeywordConverter(), new JsonStringEnumConverter() }
                };

                var config = JsonSerializer.Deserialize<AbilityConfig>(cardAbility.JsonConfig!, options);
                if (config == null)
                {
                    return null;
                }

                // 确保数据库中的基本信息优先
                config.Id = cardAbility.Id;
                config.CardId = cardAbility.CardId; // Populate CardId
                config.Name = cardAbility.Name;
                config.Description = cardAbility.Description ?? string.Empty;
                config.IsEnabled = cardAbility.IsActive;
                config.NormalizeKeywords();

                return config;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization failed for AbilityDefinition ID {AbilityId}. JsonConfig: {JsonConfig}", cardAbility.Id, cardAbility.JsonConfig);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while converting AbilityDefinition ID {AbilityId}.", cardAbility.Id);
                return null;
            }
        }
    }
}
