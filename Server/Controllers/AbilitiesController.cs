using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Services.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AbilitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AbilitiesController> _logger;
        private readonly IGameDataProvider _gameDataProvider;
        private const string DataRevisionHeader = "X-Data-Revision";

        public AbilitiesController(
            ApplicationDbContext context,
            ILogger<AbilitiesController> logger,
            IGameDataProvider gameDataProvider)
        {
            _context = context;
            _logger = logger;
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        }

        private void AppendDataRevisionHeader()
        {
            Response.Headers[DataRevisionHeader] = _gameDataProvider.DataRevision.ToString();
        }

        public sealed class AbilityCountRequest
        {
            public List<int> CardIds { get; set; } = new();
        }

        /// <summary>
        /// 获取所有能力
        /// </summary>
        /// <returns>所有能力列表</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetAllAbilities()
        {
            try
            {
                _logger.LogInformation("Attempting to get all abilities");

                var abilityEntities = await _context.CardAbilities
                    .AsNoTracking()
                    .Where(ca => ca.IsActive)
                    .ToListAsync();

                var abilities = abilityEntities.Select(ca =>
                {
                    var keywords = ExtractAbilityKeywords(ca.JsonConfig);
                    return new
                    {
                        id = ca.Id,
                        name = ca.Name ?? $"Ability {ca.Id}",
                        description = ca.Description ?? "No description available",
                        abilityKeywords = BuildAbilityKeywordDtos(keywords),
                        abilityType = ca.AbilityType,
                        priority = ca.Priority,
                        isActive = ca.IsActive,
                        cardId = ca.CardId,
                        jsonConfig = ca.JsonConfig
                    };
                }).ToList();

                AppendDataRevisionHeader();
                _logger.LogInformation("Successfully retrieved {Count} abilities", abilities.Count);
                return Ok(abilities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all abilities");
                return StatusCode(500, new { error = "Failed to retrieve abilities" });
            }
        }

        /// <summary>
        /// 根据ID获取特定能力
        /// </summary>
        /// <param name="id">能力ID</param>
        /// <returns>能力详细信息</returns>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetAbilityById(long id)
        {
            try
            {
                _logger.LogInformation("Attempting to get ability with ID: {AbilityId}", id);

                var cardAbility = await _context.CardAbilities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ca => ca.Id == id && ca.IsActive);

                if (cardAbility == null)
                {
                    _logger.LogWarning("Ability with ID {AbilityId} not found", id);
                    return NotFound($"Ability with ID {id} not found");
                }

                // 解析JsonConfig以获取详细的能力配置
                var keywords = ExtractAbilityKeywords(cardAbility.JsonConfig);
                object? jsonConfig = null;
                if (!string.IsNullOrEmpty(cardAbility.JsonConfig))
                {
                    try
                    {
                        jsonConfig = System.Text.Json.JsonSerializer.Deserialize<object>(cardAbility.JsonConfig);
                    }
                    catch (Exception jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "Failed to parse JsonConfig for ability {AbilityId}", id);
                        // 如果解析失败，使用默认结构
                        jsonConfig = new
                        {
                            id = $"ability_{cardAbility.Id}",
                            name = cardAbility.Name ?? $"Ability {cardAbility.Id}",
                            type = "Triggered",
                            abilityKeywords = BuildAbilityKeywordDtos(keywords),
                            trigger = new { },
                            conditions = new object[] { },
                            targets = new object[] { },
                            effects = new object[] { }
                        };
                    }
                }
                else
                {
                    // 如果没有JsonConfig，创建默认结构
                    jsonConfig = new
                    {
                        id = $"ability_{cardAbility.Id}",
                        name = cardAbility.Name ?? $"Ability {cardAbility.Id}",
                        type = "Triggered",
                        abilityKeywords = BuildAbilityKeywordDtos(keywords),
                        trigger = new { },
                        conditions = new object[] { },
                        targets = new object[] { },
                        effects = new object[] { }
                    };
                }

                var ability = new
                {
                    id = cardAbility.Id,
                    name = cardAbility.Name ?? $"Ability {cardAbility.Id}",
                    description = cardAbility.Description ?? "No description available",
                    abilityKeywords = BuildAbilityKeywordDtos(keywords),
                    abilityType = cardAbility.AbilityType,
                    priority = cardAbility.Priority,
                    isActive = cardAbility.IsActive,
                    cardId = cardAbility.CardId,
                    jsonConfig = jsonConfig
                };

                AppendDataRevisionHeader();
                _logger.LogInformation("Successfully retrieved ability {AbilityId}", id);
                return Ok(ability);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ability {AbilityId}", id);
                return StatusCode(500, new { error = "Failed to retrieve ability" });
            }
        }

        /// <summary>
        /// 更新能力
        /// </summary>
        /// <param name="id">能力ID</param>
        /// <param name="abilityData">能力数据</param>
        /// <returns>更新结果</returns>
        [HttpPut("{id:long}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateAbility(long id, [FromBody] JsonElement abilityData)
        {
            try
            {
                _logger.LogInformation("Attempting to update ability with ID: {AbilityId}", id);

                var cardAbility = await _context.CardAbilities.FirstOrDefaultAsync(ca => ca.Id == id);
                if (cardAbility == null)
                {
                    _logger.LogWarning("Ability with ID {AbilityId} not found", id);
                    return NotFound(new { error = $"Ability {id} not found" });
                }

                // 更新常用字段（可选）
                if (abilityData.TryGetProperty("name", out var nameProp))
                {
                    cardAbility.Name = nameProp.GetString();
                }
                if (abilityData.TryGetProperty("description", out var descProp))
                {
                    cardAbility.Description = descProp.GetString();
                }
                if (abilityData.TryGetProperty("priority", out var priorityProp) && priorityProp.ValueKind == JsonValueKind.Number)
                {
                    cardAbility.Priority = priorityProp.GetInt32();
                }
                if (abilityData.TryGetProperty("isActive", out var activeProp))
                {
                    if (activeProp.ValueKind == JsonValueKind.True || activeProp.ValueKind == JsonValueKind.False)
                    {
                        cardAbility.IsActive = activeProp.GetBoolean();
                    }
                    else if (activeProp.ValueKind == JsonValueKind.String
                             && bool.TryParse(activeProp.GetString(), out var parsedActive))
                    {
                        cardAbility.IsActive = parsedActive;
                    }
                }
                var incomingKeywords = ExtractAbilityKeywords(abilityData);

                // 将前端完整配置保存为 JsonConfig（优先使用 jsonConfig 字段）
                if (abilityData.TryGetProperty("jsonConfig", out var jsonConfigProp))
                {
                    cardAbility.JsonConfig = jsonConfigProp.ValueKind == JsonValueKind.String
                        ? jsonConfigProp.GetString()
                        : jsonConfigProp.GetRawText();
                }
                else
                {
                    cardAbility.JsonConfig = abilityData.GetRawText();
                }

                cardAbility.UpdateTimestamp();

                await _context.SaveChangesAsync();
                await _gameDataProvider.RefreshCacheAsync();

                _logger.LogInformation("Ability {AbilityId} updated successfully", id);
                AppendDataRevisionHeader();
                return Ok(new { message = "Ability updated", id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ability {AbilityId}", id);
                return StatusCode(500, new { error = "Failed to update ability" });
            }
        }

        /// <summary>
        /// 创建新能力
        /// </summary>
        /// <param name="abilityData">能力数据</param>
        /// <returns>创建的能力</returns>
        [HttpPost]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CreateAbility([FromBody] JsonElement abilityData)
        {
            try
            {
                _logger.LogInformation("Attempting to create new ability");

                if (!abilityData.TryGetProperty("cardId", out var cardIdProp) || cardIdProp.ValueKind != JsonValueKind.Number)
                {
                    return BadRequest(new { error = "cardId is required" });
                }

                var cardId = cardIdProp.GetInt32();
                var cardDefinition = await _context.CardDefinitions.FindAsync(cardId);
                if (cardDefinition == null)
                {
                    return NotFound(new { error = $"Card {cardId} not found" });
                }

                var name = abilityData.TryGetProperty("name", out var nameProp) ? nameProp.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { error = "name is required" });
                }

                var description = abilityData.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

                var incomingKeywords = ExtractAbilityKeywords(abilityData);

                var abilityType = abilityData.TryGetProperty("abilityType", out var typeProp) && typeProp.ValueKind == JsonValueKind.Number
                    ? typeProp.GetInt32()
                    : 1;

                var priority = abilityData.TryGetProperty("priority", out var priorityProp) && priorityProp.ValueKind == JsonValueKind.Number
                    ? priorityProp.GetInt32()
                    : 0;

                var isActive = !abilityData.TryGetProperty("isActive", out var activeProp) || activeProp.ValueKind != JsonValueKind.False;

                string? jsonConfig = null;
                if (abilityData.TryGetProperty("jsonConfig", out var jsonConfigProp))
                {
                    jsonConfig = jsonConfigProp.ValueKind == JsonValueKind.String
                        ? jsonConfigProp.GetString()
                        : jsonConfigProp.GetRawText();
                }

                var entity = new AbilityDefinition
                {
                    CardId = cardId,
                    Name = name,
                    Description = description,
                    AbilityType = abilityType,
                    Priority = priority,
                    IsActive = isActive,
                    JsonConfig = jsonConfig,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.CardAbilities.Add(entity);
                await _context.SaveChangesAsync();

                if (!cardDefinition.AbilityIds.Contains(entity.Id))
                {
                    cardDefinition.AbilityIds.Add(entity.Id);
                    await _context.SaveChangesAsync();
                }
                await _gameDataProvider.RefreshCacheAsync();

                var createdKeywords = ExtractAbilityKeywords(entity.JsonConfig);
                var created = new
                {
                    id = entity.Id,
                    name = entity.Name,
                    description = entity.Description,
                    abilityKeywords = BuildAbilityKeywordDtos(createdKeywords),
                    abilityType = entity.AbilityType,
                    priority = entity.Priority,
                    isActive = entity.IsActive,
                    cardId = entity.CardId,
                    jsonConfig = entity.JsonConfig
                };

                _logger.LogInformation("New ability created with ID: {AbilityId}", entity.Id);
                AppendDataRevisionHeader();
                return CreatedAtAction(nameof(GetAbilityById), new { id = entity.Id }, created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new ability");
                return StatusCode(500, new { error = "Failed to create ability" });
            }
        }

        /// <summary>
        /// 获取指定卡牌的能力数量快照
        /// </summary>
        [HttpPost("counts")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetAbilityCounts([FromBody] AbilityCountRequest request)
        {
            try
            {
                var cardIds = request?.CardIds?
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList() ?? new List<int>();

                if (cardIds.Count == 0)
                {
                    AppendDataRevisionHeader();
                    return Ok(new List<object>());
                }

                var cards = await _context.CardDefinitions
                    .AsNoTracking()
                    .Where(card => cardIds.Contains(card.Id))
                    .Select(card => new { card.Id, card.AbilityIds })
                    .ToListAsync();

                var abilityIdsByCard = cards.ToDictionary(
                    card => card.Id,
                    card => card.AbilityIds ?? new List<long>());

                var abilityIds = abilityIdsByCard.Values
                    .SelectMany(ids => ids)
                    .Distinct()
                    .ToList();

                var abilitiesByCard = await _context.CardAbilities
                    .AsNoTracking()
                    .Where(ability => ability.IsActive && cardIds.Contains(ability.CardId))
                    .Select(ability => new { ability.Id, ability.CardId })
                    .ToListAsync();

                var activeAbilityIds = abilityIds.Count == 0
                    ? new HashSet<long>()
                    : new HashSet<long>(await _context.CardAbilities
                        .AsNoTracking()
                        .Where(ability => ability.IsActive && abilityIds.Contains(ability.Id))
                        .Select(ability => ability.Id)
                        .ToListAsync());

                var abilitiesByCardId = abilitiesByCard
                    .GroupBy(ability => ability.CardId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(ability => ability.Id).ToHashSet());

                var results = new List<object>(cardIds.Count);
                foreach (var cardId in cardIds)
                {
                    var counted = abilitiesByCardId.TryGetValue(cardId, out var ids)
                        ? new HashSet<long>(ids)
                        : new HashSet<long>();

                    if (abilityIdsByCard.TryGetValue(cardId, out var abilityList))
                    {
                        foreach (var abilityId in abilityList)
                        {
                            if (activeAbilityIds.Contains(abilityId))
                            {
                                counted.Add(abilityId);
                            }
                        }
                    }

                    results.Add(new { cardId, count = counted.Count });
                }

                AppendDataRevisionHeader();
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ability counts snapshot");
                return StatusCode(500, new { error = "Failed to retrieve ability counts" });
            }
        }

        private static List<AbilityKeywordConfig> ExtractAbilityKeywords(JsonElement abilityData)
        {
            if (abilityData.ValueKind == JsonValueKind.Object &&
                abilityData.TryGetProperty("abilityKeywords", out var keywordsProp))
            {
                var parsed = DeserializeAbilityKeywords(keywordsProp);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }

            if (abilityData.ValueKind == JsonValueKind.Object &&
                abilityData.TryGetProperty("jsonConfig", out var jsonConfigProp))
            {
                var jsonConfigText = jsonConfigProp.ValueKind == JsonValueKind.String
                    ? jsonConfigProp.GetString()
                    : jsonConfigProp.GetRawText();
                var config = TryDeserializeAbilityConfig(jsonConfigText);
                if (config != null)
                {
                    config.NormalizeKeywords();
                    return config.AbilityKeywords;
                }
            }

            return new List<AbilityKeywordConfig>();
        }

        private static List<AbilityKeywordConfig> ExtractAbilityKeywords(string? jsonConfig)
        {
            var config = TryDeserializeAbilityConfig(jsonConfig);
            if (config == null)
            {
                return new List<AbilityKeywordConfig>();
            }

            config.NormalizeKeywords();
            return config.AbilityKeywords;
        }

        private static AbilityConfig? TryDeserializeAbilityConfig(string? jsonConfig)
        {
            if (string.IsNullOrWhiteSpace(jsonConfig))
            {
                return null;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new LenientEffectKeywordConverter(), new JsonStringEnumConverter() }
                };
                return JsonSerializer.Deserialize<AbilityConfig>(jsonConfig, options);
            }
            catch
            {
                return null;
            }
        }

        private static List<AbilityKeywordConfig> DeserializeAbilityKeywords(JsonElement element)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters =
                    {
                        new AbilityKeywordConfigListConverter(),
                        new LenientEffectKeywordConverter(),
                        new JsonStringEnumConverter()
                    }
                };

                return JsonSerializer.Deserialize<List<AbilityKeywordConfig>>(element.GetRawText(), options)
                    ?? new List<AbilityKeywordConfig>();
            }
            catch
            {
                return new List<AbilityKeywordConfig>();
            }
        }

        private static List<object> BuildAbilityKeywordDtos(IEnumerable<AbilityKeywordConfig> keywords)
        {
            return keywords
                .Select(k => new { keyword = k.Keyword.ToString(), value = k.Value })
                .Cast<object>()
                .ToList();
        }

    }
}
