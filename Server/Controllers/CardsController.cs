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
using Microsoft.AspNetCore.Hosting;
using AutoMapper;
using ShadowverseBeyond.Server.Infrastructure.Data;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.JsonConverters;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;
using System.Text.Json.Serialization;
using System.IO;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;

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
        private readonly IWebHostEnvironment _environment;
        private readonly IGameService _gameService;
        private readonly IGameDataProvider _gameDataProvider;
        private readonly ITribeRegistry _tribeRegistry;
        private const string DataRevisionHeader = "X-Data-Revision";
        private static readonly HashSet<string> AllowedArtExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp"
        };

        public CardsController(
            ICardRepository cardRepository,
            ILogger<CardsController> logger,
            IMapper mapper,
            ApplicationDbContext db,
            IWebHostEnvironment environment,
            IGameService gameService,
            IGameDataProvider gameDataProvider,
            ITribeRegistry tribeRegistry) // Inject Repository
        {
            _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository)); // Assign Repository
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
            _tribeRegistry = tribeRegistry ?? throw new ArgumentNullException(nameof(tribeRegistry));
        }

        private void AppendDataRevisionHeader()
        {
            Response.Headers[DataRevisionHeader] = _gameDataProvider.DataRevision.ToString();
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

                AppendDataRevisionHeader();
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

                var setDefinition = await ResolveCardSetDefinitionAsync(dto);
                if (setDefinition == null && ShouldAutoRegisterSet(dto.CardSet))
                {
                    setDefinition = await EnsureCardSetDefinitionAsync(dto.CardSet!);
                }
                setDefinition ??= await ResolveLegacyCardSetDefinitionAsync();
                var normalizedDto = dto with
                {
                    CardSet = NormalizeCardSet(setDefinition?.Name ?? dto.CardSet),
                    Tribe = await NormalizeTribesAsync(dto.Tribe)
                };

                var entity = _mapper.Map<CardDefinition>(normalizedDto);
                if (setDefinition != null)
                {
                    entity.CardSetDefinitionId = setDefinition.Id;
                    entity.CardSetDefinition = setDefinition;

                }
                await _cardRepository.AddAsync(entity);
                await _gameService.NotifyCardDefinitionUpdatedAsync(entity.Id);
                var resultDto = _mapper.Map<CardDefinitionDto>(entity);
                AppendDataRevisionHeader();
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

                var setDefinition = await ResolveCardSetDefinitionAsync(dto);
                if (setDefinition == null && ShouldAutoRegisterSet(dto.CardSet))
                {
                    setDefinition = await EnsureCardSetDefinitionAsync(dto.CardSet!);
                }
                if (setDefinition == null && existing.CardSetDefinitionId.HasValue)
                {
                    setDefinition = await _db.CardSetDefinitions.FindAsync(existing.CardSetDefinitionId.Value);
                }

                var normalizedCardSet = setDefinition?.Name
                    ?? (string.IsNullOrWhiteSpace(dto.CardSet) ? existing.CardSet : NormalizeCardSet(dto.CardSet));

                var mergedDto = dto with
                {
                    Id = id,
                    CardSet = normalizedCardSet,
                    // Avoid accidental data loss when the client doesn't send optional fields.
                    AbilityIds = dto.AbilityIds ?? existing.AbilityIds,
                    Keywords = dto.Keywords ?? existing.Keywords,
                    Tribe = dto.Tribe != null ? await NormalizeTribesAsync(dto.Tribe) : existing.Tribe,
                    Rarity = string.IsNullOrWhiteSpace(dto.Rarity) ? existing.Rarity.ToString() : dto.Rarity,
                    Class = string.IsNullOrWhiteSpace(dto.Class) ? existing.Class.ToString() : dto.Class,
                    ArtPreview = dto.ArtPreview ?? existing.ArtPreview,
                    ArtDetail = dto.ArtDetail ?? existing.ArtDetail,
                    IsCollectible = dto.IsCollectible ?? existing.IsCollectible
                };

                var updatedEntity = _mapper.Map(mergedDto, existing);
                if (setDefinition != null)
                {
                    updatedEntity.CardSetDefinitionId = setDefinition.Id;
                    updatedEntity.CardSetDefinition = setDefinition;

                }
                else
                {
                    updatedEntity.CardSetDefinitionId = existing.CardSetDefinitionId;
                    updatedEntity.CardSetDefinition = existing.CardSetDefinition;
                }
                await _cardRepository.UpdateAsync(updatedEntity);

                await _gameService.NotifyCardDefinitionUpdatedAsync(id);

                var resultDto = _mapper.Map<CardDefinitionDto>(updatedEntity);
                AppendDataRevisionHeader();
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

                AppendDataRevisionHeader();
                _logger.LogInformation("Successfully retrieved card definition for ID: {CardId}", id);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving card definition with ID: {CardId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving card definition.");
            }
        }

        [HttpPost("{id:int}/art/preview")]
        [ProducesResponseType(typeof(CardDefinitionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<ActionResult<CardDefinitionDto>> UploadPreviewArt(int id, [FromForm] IFormFile file)
        {
            return SaveCardArtAsync(id, file, "preview");
        }

        [HttpPost("{id:int}/art/detail")]
        [ProducesResponseType(typeof(CardDefinitionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<ActionResult<CardDefinitionDto>> UploadDetailArt(int id, [FromForm] IFormFile file)
        {
            return SaveCardArtAsync(id, file, "detail");
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
                    .AsNoTracking()
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

                AppendDataRevisionHeader();
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
                await _gameDataProvider.RefreshCacheAsync();

                AppendDataRevisionHeader();
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

                var ability = await _db.CardAbilities.FirstOrDefaultAsync(a => a.Id == abilityId);

                card.AbilityIds.Remove(abilityId);

                if (ability != null && ability.CardId == cardId)
                {
                    _db.CardAbilities.Remove(ability);
                }
                await _db.SaveChangesAsync();
                await _gameDataProvider.RefreshCacheAsync();

                AppendDataRevisionHeader();
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
                await _gameDataProvider.RefreshCacheAsync();

                AppendDataRevisionHeader();
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

        private async Task<ActionResult<CardDefinitionDto>> SaveCardArtAsync(int id, IFormFile file, string kind)
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest("No file uploaded.");
            }

            var card = await _cardRepository.GetCardDefinitionByIdAsync(id);
            if (card == null)
            {
                return NotFound($"Card with ID {id} not found.");
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedArtExtensions.Contains(extension))
            {
                return BadRequest("Unsupported image type.");
            }

            var workspaceRoot = GetWorkspaceRoot();
            var cardFolder = Path.Combine(workspaceRoot, "assets", "cards", id.ToString());
            Directory.CreateDirectory(cardFolder);

            var versionToken = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var targetExtension = ".webp";
            var fileName = $"{kind}-{versionToken}{targetExtension}";
            var filePath = Path.Combine(cardFolder, fileName);
            var newPath = $"/assets/cards/{id}/{fileName}";

            var existingPath = kind == "preview" ? card.ArtPreview : card.ArtDetail;
            TryDeleteExistingArt(workspaceRoot, existingPath, newPath);

            try
            {
                await ConvertToWebpAsync(file, filePath);
            }
            catch (UnknownImageFormatException)
            {
                return BadRequest("Unsupported image content.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert {ArtKind} art to WebP for card {CardId}.", kind, id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to process card art.");
            }

            if (kind == "preview")
            {
                card.ArtPreview = newPath;
            }
            else
            {
                card.ArtDetail = newPath;
            }

            await _cardRepository.UpdateAsync(card);
            await _gameService.NotifyCardDefinitionUpdatedAsync(id);
            var dto = _mapper.Map<CardDefinitionDto>(card);
            AppendDataRevisionHeader();
            return Ok(dto);
        }

        private static async Task ConvertToWebpAsync(IFormFile file, string outputPath)
        {
            await using var inputStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);
            var encoder = new WebpEncoder
            {
                Quality = 82
            };
            await image.SaveAsWebpAsync(outputPath, encoder);
        }

        private string GetWorkspaceRoot()
        {
            return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, ".."));
        }

        private static void TryDeleteExistingArt(string workspaceRoot, string? existingPath, string newPath)
        {
            if (string.IsNullOrWhiteSpace(existingPath))
            {
                return;
            }

            var normalized = existingPath.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("/assets/cards/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (normalized.Equals(newPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var diskPath = GetDiskPathFromArtPath(workspaceRoot, normalized);
            if (diskPath != null && System.IO.File.Exists(diskPath))
            {
                System.IO.File.Delete(diskPath);
            }
        }

        private static string NormalizeCardSet(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Legacy" : value.Trim();
        }

        private async Task<List<string>?> NormalizeTribesAsync(List<string>? tribes)
        {
            if (tribes == null)
            {
                return null;
            }

            var normalized = await _tribeRegistry.NormalizeAndRegisterAsync(tribes);
            return normalized.Count == 0 ? null : normalized.ToList();
        }

        private static bool TryParsePackCode(string? value, out int region, out int sequence)
        {
            region = 0;
            sequence = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var trimmed = value.Trim();
            if (trimmed.Length != 3 || !trimmed.All(char.IsDigit)) return false;

            region = int.Parse(trimmed.Substring(0, 1));
            sequence = int.Parse(trimmed.Substring(1, 2));
            return true;
        }

        private async Task<CardSetDefinition?> ResolveCardSetDefinitionAsync(CardUpsertDto dto)
        {
            if (dto.CardSetDefinitionId.HasValue)
            {
                return await _db.CardSetDefinitions.FindAsync(dto.CardSetDefinitionId.Value);
            }

            if (dto.CardPackDefinitionId.HasValue)
            {
                var pack = await _db.CardPackDefinitions
                    .Include(p => p.CardSetDefinition)
                    .FirstOrDefaultAsync(p => p.Id == dto.CardPackDefinitionId.Value);
                if (pack != null)
                {
                    return pack.CardSetDefinition;
                }
            }

            if (TryParsePackCode(dto.CardPackCode, out var region, out var sequence))
            {
                var pack = await _db.CardPackDefinitions
                    .Include(p => p.CardSetDefinition)
                    .FirstOrDefaultAsync(p => p.Region == region && p.Sequence == sequence);
                if (pack != null)
                {
                    return pack.CardSetDefinition;
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.CardSet))
            {
                var normalized = dto.CardSet.Trim().ToLower();
                return await _db.CardSetDefinitions
                    .FirstOrDefaultAsync(set => set.Name.ToLower() == normalized);
            }

            return null;
        }

        private async Task<CardSetDefinition?> ResolveLegacyCardSetDefinitionAsync()
        {
            return await _db.CardSetDefinitions
                .FirstOrDefaultAsync(set => set.Name.ToLower() == "legacy");
        }

        private static bool ShouldAutoRegisterSet(string? cardSet)
        {
            return !string.IsNullOrWhiteSpace(cardSet);
        }

        private async Task<CardSetDefinition?> EnsureCardSetDefinitionAsync(string cardSetName)
        {
            var normalized = cardSetName.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var existing = await _db.CardSetDefinitions
                .FirstOrDefaultAsync(set => set.Name.ToLower() == normalized.ToLower());
            if (existing != null)
            {
                return existing;
            }

            for (var attempt = 0; attempt < 3; attempt++)
            {
                var sequence = await FindNextSetSequenceAsync(region: 0);
                var definition = new CardSetDefinition
                {
                    Name = normalized,
                    Abbreviation = string.Empty,
                    Region = 0,
                    Sequence = sequence,
                    Environments = new List<DeckEnvironment> { DeckEnvironment.Open },
                    CreatedAt = DateTime.UtcNow
                };

                _db.CardSetDefinitions.Add(definition);
                try
                {
                    await _db.SaveChangesAsync();
                    return definition;
                }
                catch (DbUpdateException)
                {
                    _db.Entry(definition).State = EntityState.Detached;
                }
            }

            return null;
        }

        private async Task<int> FindNextSetSequenceAsync(int region)
        {
            var used = await _db.CardSetDefinitions
                .Where(set => set.Region == region)
                .Select(set => set.Sequence)
                .ToListAsync();

            for (var sequence = 2; sequence <= 99; sequence++)
            {
                if (!used.Contains(sequence))
                {
                    return sequence;
                }
            }

            throw new InvalidOperationException($"No available set sequence for region {region}.");
        }

        private static string? GetDiskPathFromArtPath(string workspaceRoot, string artPath)
        {
            var trimmed = artPath.TrimStart('/');
            var combined = Path.GetFullPath(Path.Combine(workspaceRoot, trimmed));
            return combined.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase) ? combined : null;
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

