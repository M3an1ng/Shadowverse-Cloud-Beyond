//
// 相关文档:
// - API文档: 文档/06-API文档/01-端点总览.md
// - 游戏事件系统: 文档/05-游戏系统/04-游戏事件系统.md
// - 数据库设计: 文档/04-数据库设计/01-实体关系图.md
//
using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Domain.Interfaces;    // Inject Repository Interface
using ShadowverseBeyond.Server.Domain.Entities;      // Corrected namespace
using ShadowverseBeyond.Server.Dtos; // Correct DTO namespace
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System; // Required for Exception
using Microsoft.AspNetCore.Http; // Required for StatusCodes
using AutoMapper;
using ShadowverseBeyond.Server.Infrastructure.Data;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;
using ShadowverseBeyond.Server.Domain.Constants;
using System.Text.Json.Serialization;

namespace ShadowverseBeyond.Server.Controllers // Corrected namespace
{
    [ApiController]
    [Route("api/[controller]")] // Route will be /api/cards
    public class CardsController : ControllerBase
    {
        // Change dependency from ICardService to ICardRepository
        private readonly ICardRepository _cardRepository;
        private readonly ILogger<CardsController> _logger;
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _db;

        public CardsController(ICardRepository cardRepository, ILogger<CardsController> logger, IMapper mapper, ApplicationDbContext db) // Inject Repository
        {
            _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository)); // Assign Repository
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CardDefinitionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<CardDefinitionDto>>> GetAllCards()
        {
            try
            {
                _logger.LogInformation("Attempting to get all card definitions.");
                // Use the injected repository instead of the service
                var definitions = await _cardRepository.GetAllCardDefinitionsAsync(); // Corrected method name

                if (definitions == null)
                {
                    _logger.LogWarning("Card service returned null for all card definitions.");
                    // Depending on requirements, maybe return Ok(new List<CardDto>())?
                    // Or handle it as an internal error if null is unexpected.
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve card definitions (service returned null).");
                }

                var dtos = _mapper.Map<IEnumerable<CardDefinitionDto>>(definitions);

                _logger.LogInformation($"Successfully retrieved {dtos.Count()} card definitions.");
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting all card definitions.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred while retrieving card definitions.");
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(CardDefinitionDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CardDefinitionDto>> CreateCard([FromBody] CardUpsertDto dto)
        {
            try
            {
                if (dto.Id == null)
                {
                    // 简单生成一个新ID（现有模型禁用自动递增），实际可改为序列/雪花
                    var maxId = await _db.CardDefinitions.MaxAsync(c => (int?)c.Id) ?? 0;
                    dto = dto with { Id = maxId + 1 };
                }

                var entity = _mapper.Map<CardDefinition>(dto);
                await _cardRepository.AddAsync(entity);
                var resultDto = _mapper.Map<CardDefinitionDto>(entity);
                return CreatedAtAction(nameof(GetCardById), new { id = entity.Id }, resultDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating card");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create card");
            }
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(CardDefinitionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CardDefinitionDto>> UpdateCard(int id, [FromBody] CardUpsertDto dto)
        {
            try
            {
                var existing = await _cardRepository.GetCardDefinitionByIdAsync(id);
                if (existing == null)
                {
                    return NotFound($"Card with ID {id} not found.");
                }

                var mergedDto = dto with
                {
                    Id = id,
                    // Avoid accidental data loss when the client doesn't send optional fields.
                    AbilityIds = dto.AbilityIds ?? existing.AbilityIds,
                    Keywords = dto.Keywords ?? existing.Keywords,
                    Tribe = dto.Tribe ?? existing.Tribe,
                    CardSet = string.IsNullOrWhiteSpace(dto.CardSet) ? existing.CardSet : dto.CardSet,
                    Rarity = string.IsNullOrWhiteSpace(dto.Rarity) ? existing.Rarity.ToString() : dto.Rarity,
                    Class = string.IsNullOrWhiteSpace(dto.Class) ? existing.Class.ToString() : dto.Class
                };

                var updatedEntity = _mapper.Map(mergedDto, existing);
                await _cardRepository.UpdateAsync(updatedEntity);

                var resultDto = _mapper.Map<CardDefinitionDto>(updatedEntity);
                return Ok(resultDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating card {CardId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update card");
            }
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(CardDefinitionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CardDefinitionDto>> GetCardById(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to get card definition with ID: {CardId}", id);

                // Get card definition by ID
                var definition = await _cardRepository.GetCardDefinitionByIdAsync(id);

                if (definition == null)
                {
                    _logger.LogWarning("Card definition with ID {CardId} not found.", id);
                    return NotFound($"Card with ID {id} not found.");
                }

                var dto = _mapper.Map<CardDefinitionDto>(definition);

                _logger.LogInformation("Successfully retrieved card definition for ID: {CardId}", id);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving card definition with ID: {CardId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving card definition.");
            }
        }

        [HttpGet("{cardId:int}/abilities")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetCardAbilities(int cardId)
        {
            try
            {
                _logger.LogInformation("Attempting to get abilities for card ID: {CardId}", cardId);

                // 首先验证卡牌是否存在
                var cardDefinition = await _cardRepository.GetCardDefinitionByIdAsync(cardId);
                if (cardDefinition == null)
                {
                    _logger.LogWarning("Card with ID {CardId} not found.", cardId);
                    return NotFound($"Card with ID {cardId} not found.");
                }

                var abilityIds = cardDefinition.AbilityIds ?? new List<long>();

                // 优先按 AbilityDefinition.CardId 获取（与运行时 AbilityLoader 一致），并兼容 AbilityIds 列表（历史/编辑器用途）
                var abilityEntities = await _db.CardAbilities
                    .Where(a => a.IsActive && (a.CardId == cardId || abilityIds.Contains(a.Id)))
                    .OrderBy(a => a.Priority)
                    .ToListAsync();

                if (!abilityEntities.Any())
                {
                    _logger.LogInformation("Card {CardId} has no active abilities.", cardId);
                    return Ok(new List<object>());
                }

                object? ParseJsonConfig(string? jsonConfig)
                {
                    if (string.IsNullOrWhiteSpace(jsonConfig))
                    {
                        return null;
                    }

                    try
                    {
                        return JsonSerializer.Deserialize<object>(jsonConfig);
                    }
                    catch
                    {
                        return jsonConfig;
                    }
                }

                var abilities = abilityEntities.Select(a =>
                {
                    var keywords = ExtractAbilityKeywords(a.JsonConfig);
                    return new
                    {
                        id = a.Id,
                        name = a.Name,
                        description = a.Description,
                        abilityKeywords = BuildAbilityKeywordDtos(keywords),
                        abilityType = a.AbilityType,
                        priority = a.Priority,
                        isActive = a.IsActive,
                        cardId = a.CardId,
                        jsonConfig = ParseJsonConfig(a.JsonConfig)
                    };
                }).ToList();

                _logger.LogInformation("Successfully retrieved {Count} abilities for card ID: {CardId}", abilities.Count, cardId);
                return Ok(abilities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving abilities for card ID: {CardId}", cardId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving card abilities.");
            }
        }

        [HttpPost("{cardId:int}/abilities/{abilityId:long}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> LinkAbilityToCard(int cardId, long abilityId)
        {
            try
            {
                var card = await _db.CardDefinitions.FindAsync(cardId);
                if (card == null)
                {
                    return NotFound(new { error = $"Card {cardId} not found" });
                }

                var ability = await _db.CardAbilities.FindAsync(abilityId);
                if (ability == null)
                {
                    return NotFound(new { error = $"Ability {abilityId} not found" });
                }

                var oldCardId = ability.CardId;
                if (oldCardId != cardId)
                {
                    // 若能力之前属于别的卡牌，尽量保持 AbilityIds 列表同步
                    var oldCard = await _db.CardDefinitions.FindAsync(oldCardId);
                    if (oldCard != null && oldCard.AbilityIds.Contains(abilityId))
                    {
                        oldCard.AbilityIds.Remove(abilityId);
                    }

                    ability.CardId = cardId;
                    ability.UpdateTimestamp();
                }

                if (!card.AbilityIds.Contains(abilityId))
                {
                    card.AbilityIds.Add(abilityId);
                }

                await _db.SaveChangesAsync();

                return Ok(new { message = "Ability linked", cardId, abilityId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking ability {AbilityId} to card {CardId}", abilityId, cardId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to link ability" });
            }
        }

        [HttpDelete("{cardId:int}/abilities/{abilityId:long}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UnlinkAbilityFromCard(int cardId, long abilityId)
        {
            try
            {
                var card = await _db.CardDefinitions.FindAsync(cardId);
                if (card == null)
                {
                    return NotFound(new { error = $"Card {cardId} not found" });
                }

                card.AbilityIds.Remove(abilityId);
                await _db.SaveChangesAsync();

                return Ok(new { message = "Ability unlinked", cardId, abilityId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlinking ability {AbilityId} from card {CardId}", abilityId, cardId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to unlink ability" });
            }
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteCard(int id)
        {
            try
            {
                var card = await _db.CardDefinitions.FindAsync(id);
                if (card == null)
                {
                    return NotFound(new { error = $"Card {id} not found" });
                }

                _db.CardDefinitions.Remove(card);
                await _db.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting card {CardId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to delete card" });
            }
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

        private static List<object> BuildAbilityKeywordDtos(IEnumerable<AbilityKeywordConfig> keywords)
        {
            return keywords
                .Select(k => new { keyword = k.Keyword.ToString(), value = k.Value })
                .Cast<object>()
                .ToList();
        }

    }
}
