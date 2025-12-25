using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Domain.Entities;
using System.Text.Json;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AbilitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AbilitiesController> _logger;

        public AbilitiesController(ApplicationDbContext context, ILogger<AbilitiesController> logger)
        {
            _context = context;
            _logger = logger;
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

                var abilities = await _context.CardAbilities
                    .Where(ca => ca.IsActive)
                    .Select(ca => new
                    {
                        id = ca.Id,
                        name = ca.Name ?? $"Ability {ca.Id}",
                        description = ca.Description ?? "No description available",
                        abilityTag = ca.AbilityTag ?? "Unknown",
                        abilityType = ca.AbilityType,
                        priority = ca.Priority,
                        isActive = ca.IsActive,
                        cardId = ca.CardId,
                        jsonConfig = ca.JsonConfig
                    })
                    .ToListAsync();

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
                    .FirstOrDefaultAsync(ca => ca.Id == id && ca.IsActive);

                if (cardAbility == null)
                {
                    _logger.LogWarning("Ability with ID {AbilityId} not found", id);
                    return NotFound($"Ability with ID {id} not found");
                }

                // 解析JsonConfig以获取详细的能力配置
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
                            abilityTag = new[] { cardAbility.AbilityTag ?? "Unknown" },
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
                        abilityTag = new[] { cardAbility.AbilityTag ?? "Unknown" },
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
                    abilityTag = cardAbility.AbilityTag ?? "Unknown",
                    abilityType = cardAbility.AbilityType,
                    priority = cardAbility.Priority,
                    isActive = cardAbility.IsActive,
                    cardId = cardAbility.CardId,
                    jsonConfig = jsonConfig
                };

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
                if (abilityData.TryGetProperty("abilityTag", out var tagProp))
                {
                    cardAbility.AbilityTag = tagProp.ValueKind == JsonValueKind.Array
                        ? string.Join(",", tagProp.EnumerateArray().Select(e => e.ToString()))
                        : tagProp.GetString();
                }

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

                _logger.LogInformation("Ability {AbilityId} updated successfully", id);
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

                var abilityTag = "Unknown";
                if (abilityData.TryGetProperty("abilityTag", out var tagProp))
                {
                    abilityTag = tagProp.ValueKind switch
                    {
                        JsonValueKind.Array => tagProp.EnumerateArray().Select(e => e.ToString()).FirstOrDefault() ?? "Unknown",
                        JsonValueKind.String => tagProp.GetString() ?? "Unknown",
                        _ => tagProp.ToString()
                    };
                }

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
                    AbilityTag = abilityTag,
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

                var created = new
                {
                    id = entity.Id,
                    name = entity.Name,
                    description = entity.Description,
                    abilityTag = entity.AbilityTag,
                    abilityType = entity.AbilityType,
                    priority = entity.Priority,
                    isActive = entity.IsActive,
                    cardId = entity.CardId,
                    jsonConfig = entity.JsonConfig
                };

                _logger.LogInformation("New ability created with ID: {AbilityId}", entity.Id);
                return CreatedAtAction(nameof(GetAbilityById), new { id = entity.Id }, created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new ability");
                return StatusCode(500, new { error = "Failed to create ability" });
            }
        }
    }
}
