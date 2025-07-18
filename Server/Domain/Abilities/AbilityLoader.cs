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
using ShadowverseBeyond.Server.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// 负责从数据库或缓存加载能力配置并构建运行时AbilityConfig对象
    /// 支持DSL、JSON和混合格式的能力配置
    /// 支持从GameDataProvider缓存或直接数据库访问两种模式
    /// </summary>
    public class AbilityLoader
    {
        private readonly ApplicationDbContext? _context;
        private readonly IGameDataProvider? _gameDataProvider;
        private readonly ILogger<AbilityLoader> _logger;

        /// <summary>
        /// 构造函数：使用数据库直接访问模式
        /// </summary>
        public AbilityLoader(
            ApplicationDbContext context,
            ILogger<AbilityLoader> logger)
        {
            _context = context;
            _gameDataProvider = null;
            _logger = logger;
        }

        /// <summary>
        /// 构造函数：使用GameDataProvider缓存模式（推荐）
        /// </summary>
        public AbilityLoader(
            IGameDataProvider gameDataProvider,
            ILogger<AbilityLoader> logger)
        {
            _context = null;
            _gameDataProvider = gameDataProvider;
            _logger = logger;
        }

        /// <summary>
        /// 从缓存加载指定卡牌的所有能力配置（推荐方法）
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        /// <returns>能力配置列表</returns>
        public List<AbilityConfig> LoadAbilitiesFromCache(int cardId)
        {
            if (_gameDataProvider == null)
            {
                _logger.LogError("GameDataProvider is not available. Use LoadAbilitiesForCardAsync for database access.");
                return [];
            }

            try
            {
                _logger.LogDebug("Loading abilities for card {CardId} from cache", cardId);

                var configs = _gameDataProvider.GetAbilityConfigs(cardId);

                _logger.LogDebug("Loaded {Count} abilities for card {CardId} from cache", configs.Count, cardId);
                return configs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load abilities for card {CardId} from cache", cardId);
                return [];
            }
        }

        /// <summary>
        /// 加载指定卡牌的所有能力配置（数据库访问模式）
        /// </summary>
        /// <param name="cardId">卡牌ID</param>
        /// <returns>能力配置列表</returns>
        public async Task<List<AbilityConfig>> LoadAbilitiesForCardAsync(int cardId)
        {
            // 优先使用缓存模式
            if (_gameDataProvider != null)
            {
                return LoadAbilitiesFromCache(cardId);
            }

            if (_context == null)
            {
                _logger.LogError("Neither GameDataProvider nor ApplicationDbContext is available.");
                return [];
            }

            try
            {
                _logger.LogDebug("Loading abilities for card {CardId} from database", cardId);

                var cardAbilities = await _context.Set<AbilityDefinition>()
                    .Where(ca => ca.CardId == cardId && ca.IsActive)
                    .OrderBy(ca => ca.Priority)
                    .ToListAsync();

                var configs = new List<AbilityConfig>();

                ProcessCardAbilities(cardAbilities, configs);

                _logger.LogDebug("Loaded {Count} abilities for card {CardId} from database", configs.Count, cardId);
                return configs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load abilities for card {CardId} from database", cardId);
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
            // 优先使用缓存模式
            if (_gameDataProvider != null)
            {
                try
                {
                    _logger.LogDebug("Loading ability {AbilityId} from cache", abilityId);

                    // 从缓存中查找能力配置
                    var allConfigs = _gameDataProvider.GetAllAbilityConfigs();
                    var config = allConfigs.Values.SelectMany(configs => configs)
                        .FirstOrDefault(c => c.Id == abilityId);

                    if (config == null)
                    {
                        _logger.LogWarning("Ability {AbilityId} not found in cache", abilityId);
                    }

                    return config;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load ability {AbilityId} from cache", abilityId);
                    return null;
                }
            }

            if (_context == null)
            {
                _logger.LogError("Neither GameDataProvider nor ApplicationDbContext is available.");
                return null;
            }

            try
            {
                _logger.LogDebug("Loading ability {AbilityId} from database", abilityId);

                var cardAbility = await _context.Set<AbilityDefinition>()
                    .FirstOrDefaultAsync(ca => ca.Id == abilityId && ca.IsActive);

                if (cardAbility == null)
                {
                    _logger.LogWarning("Ability {AbilityId} not found or inactive in database", abilityId);
                    return null;
                }

                return ConvertToAbilityConfig(cardAbility);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load ability {AbilityId} from database", abilityId);
                return null;
            }
        }

        /// <summary>
        /// 加载所有激活的能力配置
        /// </summary>
        /// <returns>所有能力配置列表</returns>
        public async Task<List<AbilityConfig>> LoadAllAbilitiesAsync()
        {
            // 优先使用缓存模式
            if (_gameDataProvider != null)
            {
                try
                {
                    _logger.LogDebug("Loading all active abilities from cache");

                    var allConfigs = _gameDataProvider.GetAllAbilityConfigs();
                    var configs = allConfigs.Values.SelectMany(configs => configs).ToList();

                    _logger.LogDebug("Loaded {Count} abilities total from cache", configs.Count);
                    return configs;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load all abilities from cache");
                    return [];
                }
            }

            if (_context == null)
            {
                _logger.LogError("Neither GameDataProvider nor ApplicationDbContext is available.");
                return [];
            }

            try
            {
                _logger.LogDebug("Loading all active abilities from database");

                var cardAbilities = await _context.Set<AbilityDefinition>()
                    .Where(ca => ca.IsActive)
                    .OrderBy(ca => ca.CardId)
                    .ThenBy(ca => ca.Priority)
                    .ToListAsync();

                var configs = new List<AbilityConfig>();

                ProcessCardAbilities(cardAbilities, configs);

                _logger.LogDebug("Loaded {Count} abilities total from database", configs.Count);
                return configs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load all abilities from database");
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
                    _logger.LogError(ex, "Failed to convert CardAbility {AbilityId} to AbilityConfig", cardAbility.Id);
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
                if (string.IsNullOrEmpty(cardAbility.JsonConfig))
                {
                    _logger.LogWarning("JSON config is empty for ability {AbilityId}", cardAbility.Id);
                    return null;
                }

                var config = JsonSerializer.Deserialize<AbilityConfig>(cardAbility.JsonConfig);
                if (config == null)
                {
                    _logger.LogWarning("Failed to deserialize JSON config for ability {AbilityId}", cardAbility.Id);
                    return null;
                }

                // 确保数据库中的基本信息优先
                config.Id = cardAbility.Id;
                config.CardId = cardAbility.CardId; // Populate CardId
                config.Name = cardAbility.Name;
                config.Description = cardAbility.Description;
                config.AbilityTag = cardAbility.AbilityTag;
                config.IsActive = cardAbility.IsActive;

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert CardAbility {AbilityId} to AbilityConfig", cardAbility.Id);
                return null;
            }
        }




    }
}
