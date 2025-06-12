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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System; // Required for Exception
using Microsoft.AspNetCore.Http; // Required for StatusCodes

namespace ShadowverseBeyond.Server.Controllers // Corrected namespace
{
    [ApiController]
    [Route("api/[controller]")] // Route will be /api/cards
    public class CardsController : ControllerBase
    {
        // Change dependency from ICardService to ICardRepository
        private readonly ICardRepository _cardRepository;
        private readonly ILogger<CardsController> _logger;

        public CardsController(ICardRepository cardRepository, ILogger<CardsController> logger) // Inject Repository
        {
            _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository)); // Assign Repository
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

                // Map CardDefinition (Domain Entity) to CardDefinitionDto
                var dtos = definitions.Select(def => new CardDefinitionDto
                {
                    CardId = def.Id, // int to int mapping
                    Name = def.Name,
                    Type = def.Type,
                    Rarity = def.Rarity, // Use actual property from entity
                    CardSet = def.CardSet, // Use actual property from entity
                    Cost = def.Cost,
                    Attack = def.Attack,
                    Health = def.Health,
                    Description = def.Description,
                    CanEvolve = def.EvolveId.HasValue, // Derive CanEvolve from EvolveId
                    Keywords = def.Keywords ?? new List<string>(),
                });

                _logger.LogInformation($"Successfully retrieved {dtos.Count()} card definitions.");
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting all card definitions.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal server error occurred while retrieving card definitions.");
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

                // Map CardDefinition (Domain Entity) to CardDefinitionDto
                var dto = new CardDefinitionDto
                {
                    CardId = definition.Id,
                    Name = definition.Name,
                    Type = definition.Type,
                    Rarity = definition.Rarity,
                    CardSet = definition.CardSet,
                    Cost = definition.Cost,
                    Attack = definition.Attack,
                    Health = definition.Health,
                    Description = definition.Description,
                    CanEvolve = definition.EvolveId.HasValue,
                    Keywords = definition.Keywords ?? new List<string>(),
                };

                _logger.LogInformation("Successfully retrieved card definition for ID: {CardId}", id);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving card definition with ID: {CardId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving card definition.");
            }
        }
    }
}