//
// 相关文档:
// - API文档: 文档/06-API文档/01-端点总览.md
// - 卡牌出牌系统: 文档/05-游戏系统/04-卡牌出牌系统.md
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
                    CardId = def.Id, // string to string mapping
                    Name = def.Name,
                    Type = def.Type,
                    Rarity = "Unknown", // Added default value for required property
                    CardSet = "Unknown", // Added default value for required property
                    // Class removed - not in entity
                    Cost = def.Cost,
                    Attack = def.Attack,
                    Health = def.Health,
                    Description = def.Description,
                    // FlavorText removed - not in entity
                    CanEvolve = def.EvolveId.HasValue, // Check if the int? has a value
                    Keywords = def.Keywords ?? new List<string>(),
                    // EffectDescriptions removed - not in entity
                    // ImageUrl removed - not in entity
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
    }
}