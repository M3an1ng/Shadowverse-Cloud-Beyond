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
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Dtos.Enums;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
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
        private readonly IConfiguration _configuration;
        private const string DataRevisionHeader = "X-Data-Revision";
        private const string EditorUserIdHeader = "X-Editor-UserId";
        private static readonly HashSet<string> AllowedArtMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
            "image/webp"
        };
        public CardsController(
            ICardRepository cardRepository,
            ILogger<CardsController> logger,
            IMapper mapper,
            ApplicationDbContext db,
            IWebHostEnvironment environment,
            IGameService gameService,
            IGameDataProvider gameDataProvider,
            ITribeRegistry tribeRegistry,
            IConfiguration configuration) // Inject Repository
        {
            _cardRepository = cardRepository ?? throw new ArgumentNullException(nameof(cardRepository)); // Assign Repository
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
            _tribeRegistry = tribeRegistry ?? throw new ArgumentNullException(nameof(tribeRegistry));
            _configuration = configuration;
        }

        private void AppendDataRevisionHeader()
        {
            Response.Headers[DataRevisionHeader] = _gameDataProvider.DataRevision.ToString();
        }

        private async Task<string?> ResolveEditorUsernameAsync(CancellationToken cancellationToken = default)
        {
            if (!Request.Headers.TryGetValue(EditorUserIdHeader, out var values))
            {
                return null;
            }

            var raw = values.FirstOrDefault();
            if (!Guid.TryParse(raw, out var userId) || userId == Guid.Empty)
            {
                return null;
            }

            return await _db.Users
                .AsNoTracking()
                .Where(user => user.Id == userId && user.IsActive)
                .Select(user => user.Username)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task TouchCardMetadataAsync(CardDefinition card, CancellationToken cancellationToken = default)
        {
            card.UpdatedAt = DateTime.UtcNow;
            card.UpdatedBy = await ResolveEditorUsernameAsync(cancellationToken);
        }

        private object BuildCardMetadataResponse(CardDefinition card)
        {
            return new
            {
                cardId = card.Id,
                cardUpdatedAt = card.UpdatedAt,
                cardUpdatedBy = card.UpdatedBy
            };
        }

        private async Task RefreshCardCacheAsync(params int[] cardIds)
        {
            if (_gameDataProvider is IIncrementalGameDataProvider incrementalProvider)
            {
                foreach (var cardId in cardIds.Where(id => id >= GameConstants.Cards.MirageLeaderCardId).Distinct())
                {
                    await incrementalProvider.RefreshCardAsync(cardId);
                }
                return;
            }

            await _gameDataProvider.RefreshCacheAsync();
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CardDefinitionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<CardDefinitionDto>>> GetAllCards([FromQuery] string? scope = "standard")
        {
            try
            {
                _logger.LogInformation("Attempting to get card definitions for scope={Scope}.", scope ?? "standard");
                var definitions = await _cardRepository.GetAllCardDefinitionsAsync();

                if (definitions == null)
                {
                    _logger.LogWarning("Card service returned null for all card definitions.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve card definitions (service returned null).");
                }

                var isMirageScope = MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var parsedMirage) && parsedMirage;
                var scopedDefinitions = definitions.Where(d => d != null && d.IsMirageCard == isMirageScope);
                var dtos = _mapper.Map<IEnumerable<CardDefinitionDto>>(scopedDefinitions);

                AppendDataRevisionHeader();
                _logger.LogInformation("Successfully retrieved {Count} card definitions (scope={Scope}).", dtos.Count(), isMirageScope ? "mirage" : "standard");
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
        public async Task<ActionResult<CardDefinitionDto>> CreateCard([FromBody] CardUpsertDto dto, [FromQuery] string? scope = null)
        {
            try
            {
                bool isMirage;
                if (MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var scopedMirage))
                {
                    if (dto.IsMirageCard.HasValue && dto.IsMirageCard.Value != scopedMirage)
                    {
                        return BadRequest(new { error = "卡牌归属（isMirageCard）与当前录卡入口环境不一致。" });
                    }
                    isMirage = scopedMirage;
                }
                else
                {
                    isMirage = dto.IsMirageCard ?? false;
                }

                var abilityScopeCheck = await ValidateCardAbilityIdsScopeAsync(dto.AbilityIds, isMirage);
                if (!abilityScopeCheck.Valid)
                {
                    return BadRequest(new { error = abilityScopeCheck.ErrorMessage });
                }

                if (dto.Id == null)
                {
                    var maxId = await _db.CardDefinitions.MaxAsync(c => (int?)c.Id) ?? 0;
                    dto = dto with { Id = maxId + 1 };
                }

                CardSetDefinition? setDefinition;
                if (isMirage)
                {
                    if (dto.CardSetDefinitionId.HasValue)
                    {
                        var requestedSet = await _db.CardSetDefinitions.FindAsync(dto.CardSetDefinitionId.Value);
                        if (requestedSet == null || !string.Equals(requestedSet.Name, GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase))
                        {
                            return BadRequest(new { error = "幻境卡牌只能绑定幻境专属卡集，禁止绑定经典对战卡集。" });
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(dto.CardSet)
                        && !string.Equals(dto.CardSet.Trim(), GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(dto.CardSet.Trim(), "Legacy", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(dto.CardSet.Trim(), "特殊", StringComparison.OrdinalIgnoreCase))
                    {
                        var existingClassicSet = await _db.CardSetDefinitions
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.Name.ToLower() == dto.CardSet.Trim().ToLower());
                        if (existingClassicSet != null)
                        {
                            return BadRequest(new { error = "幻境卡牌禁止使用已存在的经典卡集。" });
                        }
                    }

                    setDefinition = await EnsureMirageCardSetDefinitionAsync();
                }
                else
                {
                    if (string.Equals(dto.CardSet?.Trim(), GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new { error = "经典对战卡牌禁止归入幻境专属卡集。" });
                    }

                    setDefinition = await ResolveCardSetDefinitionAsync(dto);
                    if (setDefinition != null && string.Equals(setDefinition.Name, GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new { error = "经典对战卡牌禁止绑定幻境专属卡集。" });
                    }

                    if (setDefinition == null && ShouldAutoRegisterSet(dto.CardSet))
                    {
                        setDefinition = await EnsureCardSetDefinitionAsync(dto.CardSet!);
                    }
                    setDefinition ??= await ResolveLegacyCardSetDefinitionAsync();
                }

                var tavernTier = isMirage && !string.Equals(dto.Type, nameof(CardType.Leader), StringComparison.OrdinalIgnoreCase)
                    ? Math.Clamp(dto.MirageTavernTier ?? (dto.Cost > 0 ? dto.Cost : 1), 1, MirageRuleConfig.DefaultMaxTavernTier)
                    : (int?)null;

                var normalizedDto = dto with
                {
                    IsMirageCard = isMirage,
                    MirageTavernTier = tavernTier,
                    Cost = tavernTier ?? dto.Cost,
                    CardSet = isMirage
                        ? GameConstants.Cards.MirageCardSetName
                        : NormalizeCardSet(setDefinition?.Name ?? dto.CardSet),
                    Tribe = await NormalizeTribesAsync(dto.Tribe, isMirage)
                };

                var entity = _mapper.Map<CardDefinition>(normalizedDto);
                entity.IsMirageCard = isMirage;
                entity.MirageTavernTier = tavernTier;
                if (setDefinition != null)
                {
                    entity.CardSetDefinitionId = setDefinition.Id;
                    entity.CardSetDefinition = setDefinition;
                }
                await TouchCardMetadataAsync(entity);
                await _cardRepository.AddAsync(entity);
                await SaveMseCardMetadataAsync(entity.Id, dto);
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
        public async Task<ActionResult<CardDefinitionDto>> UpdateCard(int id, [FromBody] CardUpsertDto dto, [FromQuery] string? scope = null)
        {
            try
            {
                var existing = await _cardRepository.GetCardDefinitionByIdAsync(id);
                if (existing == null)
                {
                    return NotFound($"Card with ID {id} not found.");
                }

                if (dto.IsMirageCard.HasValue && dto.IsMirageCard.Value != existing.IsMirageCard)
                {
                    return BadRequest(new { error = "卡牌的环境归属（isMirageCard）在创建后不可修改。" });
                }

                if (MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var scopedMirage)
                    && existing.IsMirageCard != scopedMirage)
                {
                    return BadRequest(new { error = "当前录卡入口无权修改另一环境的卡牌。" });
                }

                var effectiveAbilityIds = dto.AbilityIds ?? existing.AbilityIds;
                var abilityScopeCheck = await ValidateCardAbilityIdsScopeAsync(effectiveAbilityIds, existing.IsMirageCard);
                if (!abilityScopeCheck.Valid)
                {
                    return BadRequest(new { error = abilityScopeCheck.ErrorMessage });
                }

                CardSetDefinition? setDefinition;
                string normalizedCardSet;
                if (existing.IsMirageCard)
                {
                    if (dto.CardSetDefinitionId.HasValue)
                    {
                        var requestedSet = await _db.CardSetDefinitions.FindAsync(dto.CardSetDefinitionId.Value);
                        if (requestedSet == null || !string.Equals(requestedSet.Name, GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase))
                        {
                            return BadRequest(new { error = "幻境卡牌只能绑定幻境专属卡集，禁止绑定经典对战卡集。" });
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(dto.CardSet)
                        && !string.Equals(dto.CardSet.Trim(), GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(dto.CardSet.Trim(), existing.CardSet, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new { error = "幻境卡牌禁止修改为经典对战卡集。" });
                    }

                    setDefinition = await EnsureMirageCardSetDefinitionAsync();
                    normalizedCardSet = GameConstants.Cards.MirageCardSetName;
                }
                else
                {
                    if (string.Equals(dto.CardSet?.Trim(), GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new { error = "经典对战卡牌禁止归入幻境专属卡集。" });
                    }

                    setDefinition = await ResolveCardSetDefinitionAsync(dto);
                    if (setDefinition != null && string.Equals(setDefinition.Name, GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new { error = "经典对战卡牌禁止绑定幻境专属卡集。" });
                    }

                    if (setDefinition == null && ShouldAutoRegisterSet(dto.CardSet))
                    {
                        setDefinition = await EnsureCardSetDefinitionAsync(dto.CardSet!);
                    }
                    if (setDefinition == null && existing.CardSetDefinitionId.HasValue)
                    {
                        setDefinition = await _db.CardSetDefinitions.FindAsync(existing.CardSetDefinitionId.Value);
                    }

                    normalizedCardSet = setDefinition?.Name
                        ?? (string.IsNullOrWhiteSpace(dto.CardSet) ? existing.CardSet : NormalizeCardSet(dto.CardSet));
                }

                var isLeader = existing.Type == CardType.Leader || string.Equals(dto.Type, nameof(CardType.Leader), StringComparison.OrdinalIgnoreCase);
                var tavernTier = existing.IsMirageCard && !isLeader
                    ? Math.Clamp(dto.MirageTavernTier ?? (dto.Cost > 0 ? dto.Cost : (existing.MirageTavernTier ?? 1)), 1, MirageRuleConfig.DefaultMaxTavernTier)
                    : (int?)null;

                var mergedDto = dto with
                {
                    Id = id,
                    IsMirageCard = existing.IsMirageCard,
                    MirageTavernTier = tavernTier,
                    Cost = tavernTier ?? dto.Cost,
                    CardSet = normalizedCardSet,
                    AbilityIds = effectiveAbilityIds,
                    Keywords = dto.Keywords ?? existing.Keywords,
                    Tribe = dto.Tribe != null ? await NormalizeTribesAsync(dto.Tribe, existing.IsMirageCard) : existing.Tribe,
                    Rarity = string.IsNullOrWhiteSpace(dto.Rarity) ? existing.Rarity.ToString() : dto.Rarity,
                    Class = string.IsNullOrWhiteSpace(dto.Class) ? existing.Class.ToString() : dto.Class,
                    ArtPreview = dto.ArtPreview ?? existing.ArtPreview,
                    ArtDetail = dto.ArtDetail ?? existing.ArtDetail,
                    IsCollectible = dto.IsCollectible ?? existing.IsCollectible
                };

                var updatedEntity = _mapper.Map(mergedDto, existing);
                updatedEntity.IsMirageCard = existing.IsMirageCard;
                updatedEntity.MirageTavernTier = tavernTier;
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
                await TouchCardMetadataAsync(updatedEntity);
                await _cardRepository.UpdateAsync(updatedEntity);

                await SaveMseCardMetadataAsync(updatedEntity.Id, dto);
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
        public async Task<ActionResult<CardDefinitionDto>> GetCardById(int id, [FromQuery] string? scope = null)
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

                if (MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var scopedMirage)
                    && definition.IsMirageCard != scopedMirage)
                {
                    return BadRequest(new { error = $"卡牌 #{id} 不属于当前编辑环境（{scope}）。" });
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
        public Task<ActionResult<CardDefinitionDto>> UploadPreviewArt(int id, [FromForm] IFormFile file, [FromQuery] string? scope = null)
        {
            return SaveCardArtAsync(id, file, "preview", scope);
        }

        [HttpPost("{id:int}/art/detail")]
        [ProducesResponseType(typeof(CardDefinitionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<ActionResult<CardDefinitionDto>> UploadDetailArt(int id, [FromForm] IFormFile file, [FromQuery] string? scope = null)
        {
            return SaveCardArtAsync(id, file, "detail", scope);
        }

        private static readonly HttpClient SharedOcrHttpClient = new() { Timeout = TimeSpan.FromSeconds(45) };
        private static readonly object OcrProcessStartLock = new();
        private static Process? _ocrDaemonProcess;

        private void EnsureLocalOcrDaemonStarted(Uri ocrAddress)
        {
            lock (OcrProcessStartLock)
            {
                if (_ocrDaemonProcess != null && !_ocrDaemonProcess.HasExited)
                {
                    return;
                }

                var repoRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, ".."));
                var scriptPath = Path.Combine(repoRoot, "tools", "ocr-lab", "server.py");
                if (!System.IO.File.Exists(scriptPath))
                {
                    throw new FileNotFoundException("OCR 服务脚本未安装。", scriptPath);
                }

                var configuredPython = Environment.GetEnvironmentVariable("OCR_LAB_PYTHON");
                const string sharedPython = "/opt/svnebula-native/shared/ocr-lab/venv/bin/python";
                var winVenvPython = Path.Combine(repoRoot, ".local", "ocr-lab", "venv", "Scripts", "python.exe");
                var unixVenvPython = Path.Combine(repoRoot, ".local", "ocr-lab", "venv", "bin", "python");
                var pythonExe = new[] { configuredPython, sharedPython, winVenvPython, unixVenvPython }
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                    ?? (OperatingSystem.IsWindows() ? "python" : "python3");

                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };
                psi.ArgumentList.Add(scriptPath);
                psi.Environment["OCR_LAB_HOST"] = ocrAddress.Host;
                psi.Environment["OCR_LAB_PORT"] = ocrAddress.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
                psi.Environment["PYTHONUNBUFFERED"] = "1";
                _logger.LogInformation("Starting OCR service with Python {PythonPath}, script {ScriptPath}, address {Address}",
                    pythonExe, scriptPath, ocrAddress);
                _ocrDaemonProcess?.Dispose();
                _ocrDaemonProcess = Process.Start(psi)
                    ?? throw new InvalidOperationException("无法启动 OCR 服务进程。");
            }
        }

        private static async Task<bool> IsOcrReadyAsync(Uri healthUrl, CancellationToken cancellationToken)
        {
            using var pingTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pingTimeout.CancelAfter(TimeSpan.FromSeconds(1.5));
            try
            {
                using var ping = await SharedOcrHttpClient.GetAsync(healthUrl, pingTimeout.Token);
                return ping.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        [HttpPost("ocr-import")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> OcrImportCard([FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "请上传需要识别的卡牌图片。" });
            }

            if (!AllowedArtMimeTypes.Contains(file.ContentType))
            {
                return BadRequest(new { error = "仅支持 PNG、JPEG 或 WebP 格式的卡牌图片。" });
            }

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            var imageBytes = ms.ToArray();

            try
            {
                var host = Environment.GetEnvironmentVariable("OCR_LAB_HOST") ?? _configuration["OcrLab:Host"] ?? "127.0.0.1";
                var port = int.Parse(Environment.GetEnvironmentVariable("OCR_LAB_PORT") ?? _configuration["OcrLab:Port"] ?? "8786",
                    System.Globalization.CultureInfo.InvariantCulture);
                var ocrAddress = new UriBuilder("http", host, port).Uri;
                var healthUrl = new Uri(ocrAddress, "health");
                var ocrUrl = new Uri(ocrAddress, "recognize?model=mobile&layout=beyond");
                using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                startupTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                bool ready = await IsOcrReadyAsync(healthUrl, startupTimeout.Token);

                if (!ready)
                {
                    EnsureLocalOcrDaemonStarted(ocrAddress);
                    while (!startupTimeout.IsCancellationRequested)
                    {
                        if (_ocrDaemonProcess!.HasExited)
                        {
                            _logger.LogError("OCR service exited during startup with exit code {ExitCode}", _ocrDaemonProcess.ExitCode);
                            throw new InvalidOperationException($"OCR 服务启动失败，Python 退出码：{_ocrDaemonProcess.ExitCode}。请查看服务日志。");
                        }
                        await Task.Delay(250, startupTimeout.Token);
                        if (await IsOcrReadyAsync(healthUrl, startupTimeout.Token))
                        {
                            ready = true;
                            break;
                        }
                    }
                }

                if (!ready)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        error = "本地 OCR 识别服务未启动或初始化超时，请确认 tools/ocr-lab 环境可用。"
                    });
                }

                using var content = new ByteArrayContent(imageBytes);
                using var response = await SharedOcrHttpClient.PostAsync(ocrUrl, content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, body);
                }

                return Content(body, "application/json; charset=utf-8");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "OCR 服务启动或识别超时，请查看服务日志。" });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR card import failed.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"OCR 识别异常：{ex.Message}" });
            }
        }

        public sealed record MirageCardImportRequest(
            [property: System.Text.Json.Serialization.JsonPropertyName("sourceCardId")] int SourceCardId,
            [property: System.Text.Json.Serialization.JsonPropertyName("tavernTier")] int? TavernTier = null,
            [property: System.Text.Json.Serialization.JsonPropertyName("mirageTavernTier")] int? MirageTavernTier = null,
            [property: System.Text.Json.Serialization.JsonPropertyName("includeAbilities")] bool IncludeAbilities = true,
            [property: System.Text.Json.Serialization.JsonPropertyName("nameOverride")] string? NameOverride = null);

        [HttpPost("mirage/import")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> ImportClassicCardToMirage([FromBody] MirageCardImportRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || request.SourceCardId <= 0)
            {
                return BadRequest(new { error = "请提供有效的普通卡牌 ID（sourceCardId > 0）。" });
            }

            try
            {
                var sourceRoot = await _db.CardDefinitions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.SourceCardId, cancellationToken);
                if (sourceRoot == null)
                {
                    return NotFound(new { error = $"未找到源卡牌 #{request.SourceCardId}。" });
                }

                if (sourceRoot.IsMirageCard || sourceRoot.Type == CardType.Leader)
                {
                    return BadRequest(new { error = "只能从经典对战普通卡牌导入至幻境卡池，禁止导入主战者或已是幻境的卡牌。" });
                }

                // BFS discover transitive closure of referenced classic cards & abilities
                var orderedSourceCardIds = new List<int> { sourceRoot.Id };
                var visitedCardIds = new HashSet<int> { sourceRoot.Id };
                var sourceCardsById = new Dictionary<int, CardDefinition> { [sourceRoot.Id] = sourceRoot };
                var sourceAbilitiesByCardId = new Dictionary<int, List<AbilityDefinition>>();
                var unsupportedReferences = new List<string>();
                var queue = new Queue<int>();
                queue.Enqueue(sourceRoot.Id);

                while (queue.Count > 0)
                {
                    var currentId = queue.Dequeue();
                    var currentCard = sourceCardsById[currentId];
                    var abilityIdFilter = currentCard.AbilityIds ?? new List<long>();
                    var abilities = request.IncludeAbilities
                        ? await _db.CardAbilities
                            .AsNoTracking()
                            .Where(a => a.IsActive && (a.CardId == currentId || abilityIdFilter.Contains(a.Id)))
                            .OrderBy(a => a.Priority)
                            .ThenBy(a => a.Id)
                            .ToListAsync(cancellationToken)
                        : new List<AbilityDefinition>();

                    sourceAbilitiesByCardId[currentId] = abilities;

                    foreach (var ability in abilities)
                    {
                        var analysis = MirageCardPoolIsolationRules.ExtractAbilityConfigReferences(ability.JsonConfig);
                        foreach (var refCardId in analysis.ReferencedCardIds)
                        {
                            if (visitedCardIds.Contains(refCardId))
                            {
                                continue;
                            }

                            var refCard = await _db.CardDefinitions
                                .AsNoTracking()
                                .FirstOrDefaultAsync(c => c.Id == refCardId, cancellationToken);
                            if (refCard == null || refCard.Id <= 0 || refCard.Type == CardType.Leader || refCard.IsMirageCard)
                            {
                                unsupportedReferences.Add($"卡牌 #{currentId} 的能力 #{ability.Id} 引用了无法转换的卡牌定义 #{refCardId}");
                                continue;
                            }

                            visitedCardIds.Add(refCardId);
                            orderedSourceCardIds.Add(refCardId);
                            sourceCardsById[refCardId] = refCard;
                            queue.Enqueue(refCardId);
                        }

                        foreach (var refAbilityId in analysis.ReferencedAbilityIds)
                        {
                            var refAbility = await _db.CardAbilities
                                .AsNoTracking()
                                .FirstOrDefaultAsync(a => a.Id == refAbilityId && a.IsActive, cancellationToken);
                            if (refAbility == null)
                            {
                                unsupportedReferences.Add($"卡牌 #{currentId} 的能力 #{ability.Id} 引用了不存在的能力 #{refAbilityId}");
                                continue;
                            }

                            if (!visitedCardIds.Contains(refAbility.CardId))
                            {
                                var refOwnerCard = await _db.CardDefinitions
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(c => c.Id == refAbility.CardId, cancellationToken);
                                if (refOwnerCard == null || refOwnerCard.Id <= 0 || refOwnerCard.Type == CardType.Leader || refOwnerCard.IsMirageCard)
                                {
                                    unsupportedReferences.Add($"卡牌 #{currentId} 的能力 #{ability.Id} 引用了跨环境或无效卡牌 #{refAbility.CardId} 的能力 #{refAbilityId}");
                                    continue;
                                }

                                visitedCardIds.Add(refOwnerCard.Id);
                                orderedSourceCardIds.Add(refOwnerCard.Id);
                                sourceCardsById[refOwnerCard.Id] = refOwnerCard;
                                queue.Enqueue(refOwnerCard.Id);
                            }
                        }
                    }
                }

                if (unsupportedReferences.Count > 0)
                {
                    return BadRequest(new
                    {
                        error = "导入失败：源卡牌包含无法转换为独立幻境对象的引用。",
                        unsupportedReferences
                    });
                }

                var mirageSet = await EnsureMirageCardSetDefinitionAsync();
                var workspaceRoot = GetWorkspaceRoot();
                var editorUsername = await ResolveEditorUsernameAsync(cancellationToken);
                var now = DateTime.UtcNow;

                var cardIdMap = new Dictionary<int, int>();
                var abilityIdMap = new Dictionary<long, long>();
                var createdCardsBySourceId = new Dictionary<int, CardDefinition>();
                var clonedAbilities = new List<(AbilityDefinition ClonedEntity, long SourceAbilityId, int SourceCardId, string? OriginalJson)>();

                await using (var tx = await _db.Database.BeginTransactionAsync(cancellationToken))
                {
                    var nextCardId = (await _db.CardDefinitions.MaxAsync(c => (int?)c.Id, cancellationToken) ?? 0) + 1;

                    foreach (var srcCardId in orderedSourceCardIds)
                    {
                        var srcCard = sourceCardsById[srcCardId];
                        var newCardId = nextCardId++;
                        cardIdMap[srcCardId] = newCardId;

                        var isRoot = srcCardId == sourceRoot.Id;
                        var defaultTier = ComputeDefaultMirageTier(srcCard.Cost);
                        var requestedTier = request.MirageTavernTier ?? request.TavernTier;
                        var resolvedTier = isRoot && requestedTier.HasValue
                            ? Math.Clamp(requestedTier.Value, 1, MirageRuleConfig.DefaultMaxTavernTier)
                            : defaultTier;

                        var newPreviewPath = MirageCardPoolIsolationRules.CopyCardArtFileForImport(
                            workspaceRoot,
                            srcCard.ArtPreview,
                            newCardId,
                            "preview");
                        var newDetailPath = MirageCardPoolIsolationRules.CopyCardArtFileForImport(
                            workspaceRoot,
                            srcCard.ArtDetail,
                            newCardId,
                            "detail");

                        var normalizedTribes = await NormalizeTribesAsync(srcCard.Tribe, isMirageScope: true);

                        var clonedCard = new CardDefinition
                        {
                            Id = newCardId,
                            Name = isRoot && !string.IsNullOrWhiteSpace(request.NameOverride)
                                ? request.NameOverride.Trim()
                                : srcCard.Name,
                            Type = srcCard.Type,
                            Rarity = srcCard.Rarity,
                            CardSet = GameConstants.Cards.MirageCardSetName,
                            CardSetDefinitionId = mirageSet?.Id,
                            Class = srcCard.Class,
                            Cost = resolvedTier,
                            MirageTavernTier = resolvedTier,
                            Attack = srcCard.Attack,
                            Health = srcCard.Health,
                            Countdown = srcCard.Countdown,
                            Keywords = srcCard.Keywords != null ? new List<string>(srcCard.Keywords) : new List<string>(),
                            Description = srcCard.Description ?? string.Empty,
                            ArtPreview = newPreviewPath,
                            ArtDetail = newDetailPath,
                            UpdatedAt = now,
                            UpdatedBy = editorUsername,
                            IsCollectible = srcCard.IsCollectible,
                            IsMirageCard = true,
                            Tribe = normalizedTribes,
                            AbilityIds = new List<long>()
                        };

                        _db.CardDefinitions.Add(clonedCard);
                        createdCardsBySourceId[srcCardId] = clonedCard;
                    }

                    await _db.SaveChangesAsync(cancellationToken);

                    foreach (var srcCardId in orderedSourceCardIds)
                    {
                        var newCardId = cardIdMap[srcCardId];
                        var abilities = sourceAbilitiesByCardId.GetValueOrDefault(srcCardId) ?? new List<AbilityDefinition>();
                        foreach (var srcAbility in abilities)
                        {
                            if (abilityIdMap.ContainsKey(srcAbility.Id))
                            {
                                continue;
                            }

                            var clonedAbility = new AbilityDefinition
                            {
                                CardId = newCardId,
                                Name = srcAbility.Name,
                                Description = srcAbility.Description,
                                AbilityType = srcAbility.AbilityType,
                                Priority = srcAbility.Priority,
                                IsActive = srcAbility.IsActive,
                                JsonConfig = srcAbility.JsonConfig,
                                CreatedAt = now,
                                UpdatedAt = now
                            };

                            _db.CardAbilities.Add(clonedAbility);
                            clonedAbilities.Add((clonedAbility, srcAbility.Id, srcCardId, srcAbility.JsonConfig));
                        }
                    }

                    await _db.SaveChangesAsync(cancellationToken);

                    foreach (var item in clonedAbilities)
                    {
                        abilityIdMap[item.SourceAbilityId] = item.ClonedEntity.Id;
                    }

                    foreach (var item in clonedAbilities)
                    {
                        var newOwnerCardId = cardIdMap[item.SourceCardId];
                        item.ClonedEntity.JsonConfig = MirageCardPoolIsolationRules.RewriteAbilityConfigForMirageImport(
                            item.OriginalJson,
                            item.ClonedEntity.Id,
                            newOwnerCardId,
                            cardIdMap,
                            abilityIdMap);

                        if (createdCardsBySourceId.TryGetValue(item.SourceCardId, out var ownerCard)
                            && !ownerCard.AbilityIds.Contains(item.ClonedEntity.Id))
                        {
                            ownerCard.AbilityIds.Add(item.ClonedEntity.Id);
                        }
                    }

                    await _db.SaveChangesAsync(cancellationToken);

                    foreach (var item in clonedAbilities)
                    {
                        var postCheck = await MirageCardPoolIsolationRules.ValidateAbilityJsonScopeAsync(
                            _db,
                            item.ClonedEntity.JsonConfig,
                            ownerIsMirageCard: true,
                            cancellationToken);
                        if (!postCheck.Valid)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return BadRequest(new { error = $"导入后能力校验未通过：{postCheck.ErrorMessage}" });
                        }
                    }

                    await tx.CommitAsync(cancellationToken);
                }

                var createdIds = cardIdMap.Values.ToArray();
                await RefreshCardCacheAsync(createdIds);
                foreach (var createdId in createdIds)
                {
                    await _gameService.NotifyCardDefinitionUpdatedAsync(createdId);
                }

                var rootCreated = createdCardsBySourceId[sourceRoot.Id];
                rootCreated.CardSetDefinition = mirageSet;
                var resultDto = _mapper.Map<CardDefinitionDto>(rootCreated);
                AppendDataRevisionHeader();

                return CreatedAtAction(
                    nameof(GetCardById),
                    new { id = rootCreated.Id, scope = "mirage" },
                    new
                    {
                        card = resultDto,
                        importedCardIdMap = cardIdMap,
                        importedAbilityIdMap = abilityIdMap,
                        importedDerivedCardIds = cardIdMap
                            .Where(kvp => kvp.Key != sourceRoot.Id)
                            .Select(kvp => kvp.Value)
                            .ToArray(),
                        importedAbilityCount = abilityIdMap.Count
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing classic card {SourceCardId} into Mirage pool", request.SourceCardId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "导入幻境卡牌失败" });
            }
        }

        [HttpGet("{cardId:int}/abilities")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetCardAbilities(int cardId, [FromQuery] string? scope = null)
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

                if (MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var scopedMirage)
                    && cardDefinition.IsMirageCard != scopedMirage)
                {
                    return BadRequest(new { error = $"卡牌 #{cardId} 不属于当前编辑环境（{scope}）。" });
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

                    return JsonSerializer.Deserialize<object>(jsonConfig);
                }

                var abilities = abilityEntities.Select(a =>
                {
                    var keywords = AbilityConfigJson.ReadKeywords(a.JsonConfig);
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
        public async Task<ActionResult<object>> LinkAbilityToCard(int cardId, long abilityId, [FromQuery] string? scope = null)
        {
            try
            {
                var card = await _db.CardDefinitions.FindAsync(cardId);
                if (card == null)
                {
                    return NotFound(new { error = $"Card {cardId} not found" });
                }

                if (MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var scopedMirage)
                    && card.IsMirageCard != scopedMirage)
                {
                    return BadRequest(new { error = "目标卡牌与当前编辑环境不一致。" });
                }

                var ability = await _db.CardAbilities.FindAsync(abilityId);
                if (ability == null)
                {
                    return NotFound(new { error = $"Ability {abilityId} not found" });
                }

                var oldCardId = ability.CardId;
                var oldCard = await _db.CardDefinitions.FindAsync(oldCardId);
                if (oldCard != null && oldCard.IsMirageCard != card.IsMirageCard)
                {
                    return BadRequest(new { error = "严禁跨环境关联能力（经典卡牌与幻境卡牌能力不可互相绑定或转移）。" });
                }

                var jsonValidation = await MirageCardPoolIsolationRules.ValidateAbilityJsonScopeAsync(_db, ability.JsonConfig, card.IsMirageCard);
                if (!jsonValidation.Valid)
                {
                    return BadRequest(new { error = jsonValidation.ErrorMessage });
                }

                if (oldCardId != cardId)
                {
                    // 若能力之前属于别的卡牌，尽量保持 AbilityIds 列表同步
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

                await TouchCardMetadataAsync(card);

                await _db.SaveChangesAsync();
                try
                {
                    await RefreshCardCacheAsync(oldCardId, cardId);
                }
                catch (Exception ex)
                {
                    return AbilityCacheRefreshFailed(card, abilityId, "link", ex);
                }

                AppendDataRevisionHeader();
                return Ok(new
                {
                    message = "Ability linked",
                    cardId,
                    abilityId,
                    cardUpdatedAt = card.UpdatedAt,
                    cardUpdatedBy = card.UpdatedBy
                });
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
        public async Task<IActionResult> UnlinkAbilityFromCard(int cardId, long abilityId, [FromQuery] string? scope = null)
        {
            try
            {
                var card = await _db.CardDefinitions.FindAsync(cardId);
                if (card == null)
                {
                    return NotFound(new { error = $"Card {cardId} not found" });
                }

                if (MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var scopedMirage)
                    && card.IsMirageCard != scopedMirage)
                {
                    return BadRequest(new { error = "目标卡牌与当前编辑环境不一致。" });
                }

                var ability = await _db.CardAbilities.FirstOrDefaultAsync(a => a.Id == abilityId);

                card.AbilityIds.Remove(abilityId);

                if (ability != null && ability.CardId == cardId)
                {
                    _db.CardAbilities.Remove(ability);
                }
                await TouchCardMetadataAsync(card);
                await _db.SaveChangesAsync();
                try
                {
                    await RefreshCardCacheAsync(cardId);
                }
                catch (Exception ex)
                {
                    return AbilityCacheRefreshFailed(card, abilityId, "unlink", ex);
                }

                AppendDataRevisionHeader();
                return Ok(new
                {
                    message = "Ability unlinked",
                    cardId,
                    abilityId,
                    cardUpdatedAt = card.UpdatedAt,
                    cardUpdatedBy = card.UpdatedBy
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlinking ability {AbilityId} from card {CardId}", abilityId, cardId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to unlink ability" });
            }
        }

        private ObjectResult AbilityCacheRefreshFailed(CardDefinition card, long abilityId, string operation, Exception exception)
        {
            _logger.LogError(exception, "Ability {AbilityId} {Operation} was saved for card {CardId}, but its cache refresh failed", abilityId, operation, card.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Ability change saved, but game data cache refresh failed",
                code = "ABILITY_CACHE_REFRESH_FAILED",
                saved = true,
                operation,
                id = abilityId,
                cardId = card.Id,
                cardUpdatedAt = card.UpdatedAt,
                cardUpdatedBy = card.UpdatedBy
            });
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteCard(int id, [FromQuery] string? scope = null)
        {
            try
            {
                if (id == GameConstants.Cards.LeaderCardId || id == GameConstants.Cards.MirageLeaderCardId)
                {
                    return BadRequest(new { error = "系统主战者卡牌不可删除。" });
                }

                var card = await _db.CardDefinitions.FindAsync(id);
                if (card == null)
                {
                    return NotFound(new { error = $"Card {id} not found" });
                }

                if (MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var scopedMirage)
                    && card.IsMirageCard != scopedMirage)
                {
                    return BadRequest(new { error = "目标卡牌与当前编辑环境不一致，无权删除。" });
                }

                var ownedAbilities = await _db.CardAbilities
                    .Where(a => a.CardId == id)
                    .ToListAsync();
                if (ownedAbilities.Count > 0)
                {
                    _db.CardAbilities.RemoveRange(ownedAbilities);
                }

                var mseDoc = await _db.FeatureDocuments.FindAsync($"mse-card:{id}");
                if (mseDoc != null) _db.FeatureDocuments.Remove(mseDoc);
                _db.CardDefinitions.Remove(card);
                await _db.SaveChangesAsync();
                CardStyleCatalog.RemoveMseCardMeta(id);
                await RefreshCardCacheAsync(id);

                AppendDataRevisionHeader();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting card {CardId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to delete card" });
            }
        }

        private async Task SaveMseCardMetadataAsync(int cardId, CardUpsertDto dto)
        {
            if (!dto.IsImplemented.HasValue && !dto.Presentation.HasValue) return;
            var existing = CardStyleCatalog.GetMseCardMeta(cardId);
            var meta = new MseCardMetadata(cardId, dto.IsImplemented ?? existing?.IsImplemented ?? true, dto.Presentation ?? existing?.Presentation);
            var key = $"mse-card:{cardId}";
            var doc = await _db.FeatureDocuments.FindAsync(key);
            if (doc == null)
            {
                doc = new FeatureDocument { Id = key };
                _db.FeatureDocuments.Add(doc);
            }
            doc.Json = System.Text.Json.JsonSerializer.Serialize(meta, CardStyleCatalog.JsonOptions);
            doc.Revision++;
            doc.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            CardStyleCatalog.Apply(doc);
        }

        private async Task<ActionResult<CardDefinitionDto>> SaveCardArtAsync(int id, IFormFile file, string kind, string? scope = null)
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

            if (MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var scopedMirage)
                && card.IsMirageCard != scopedMirage)
            {
                return BadRequest(new { error = "目标卡牌与当前编辑环境不一致，无权修改卡图。" });
            }

            var workspaceRoot = GetWorkspaceRoot();
            var cardFolder = Path.Combine(workspaceRoot, "assets", "cards", id.ToString());
            Directory.CreateDirectory(cardFolder);

            var versionToken = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var targetExtension = ".webp";
            var fileName = $"{kind}-{versionToken}-{Guid.NewGuid():N}{targetExtension}";
            var filePath = Path.Combine(cardFolder, fileName);
            var newPath = $"/assets/cards/{id}/{fileName}";

            var existingPath = kind == "preview" ? card.ArtPreview : card.ArtDetail;

            try
            {
                var converted = await TryConvertToWebpAsync(file, filePath);
                if (!converted)
                {
                    return BadRequest("Unsupported image content.");
                }
            }
            catch (UnknownImageFormatException)
            {
                System.IO.File.Delete(filePath);
                return BadRequest("Unsupported image content.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert {ArtKind} art to WebP for card {CardId}.", kind, id);
                System.IO.File.Delete(filePath);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to process card art.");
            }

            var previousUpdatedAt = card.UpdatedAt;
            var previousUpdatedBy = card.UpdatedBy;
            // Keep the previous file valid until the new reference has been persisted.
            try
            {
                if (kind == "preview")
                {
                    card.ArtPreview = newPath;
                }
                else
                {
                    card.ArtDetail = newPath;
                }

                await TouchCardMetadataAsync(card);
                await _cardRepository.UpdateAsync(card);
            }
            catch
            {
                if (kind == "preview")
                {
                    card.ArtPreview = existingPath;
                }
                else
                {
                    card.ArtDetail = existingPath;
                }
                card.UpdatedAt = previousUpdatedAt;
                card.UpdatedBy = previousUpdatedBy;
                System.IO.File.Delete(filePath);
                throw;
            }

            // Notification errors are post-commit failures, not a reason to remove the saved image.
            await TryDeleteExistingArtAsync(workspaceRoot, id, existingPath, newPath);
            await _gameService.NotifyCardDefinitionUpdatedAsync(id);
            var dto = _mapper.Map<CardDefinitionDto>(card);
            AppendDataRevisionHeader();
            return Ok(dto);
        }

        private static async Task<bool> TryConvertToWebpAsync(IFormFile file, string outputPath)
        {
            await using var inputStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);
            var mimeType = image.Metadata.DecodedImageFormat?.DefaultMimeType;
            if (string.IsNullOrWhiteSpace(mimeType) || !AllowedArtMimeTypes.Contains(mimeType))
            {
                return false;
            }

            var encoder = new WebpEncoder
            {
                Quality = 82
            };
            await image.SaveAsWebpAsync(outputPath, encoder);
            return true;
        }

        private string GetWorkspaceRoot()
        {
            return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, ".."));
        }

        private async Task TryDeleteExistingArtAsync(string workspaceRoot, int cardId, string? existingPath, string newPath)
        {
            if (string.IsNullOrWhiteSpace(existingPath))
            {
                return;
            }

            var normalized = existingPath.Replace('\\', '/').Trim();
            var ownedPrefix = $"/assets/cards/{cardId}/";
            if (!normalized.StartsWith(ownedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Never delete art files outside the current card's owned directory.
                return;
            }

            if (normalized.Equals(newPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var stillReferencedByOtherCards = await _db.CardDefinitions
                .AsNoTracking()
                .AnyAsync(c => c.Id != cardId && (c.ArtPreview == existingPath || c.ArtDetail == existingPath));
            if (stillReferencedByOtherCards)
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

        private async Task<List<string>?> NormalizeTribesAsync(List<string>? tribes, bool isMirageScope = false)
        {
            if (tribes == null)
            {
                return null;
            }

            var normalized = await _tribeRegistry.NormalizeAndRegisterAsync(tribes, isMirageScope: isMirageScope);
            return normalized.Count == 0 ? null : normalized.ToList();
        }

        private async Task<(bool Valid, string? ErrorMessage)> ValidateCardAbilityIdsScopeAsync(
            IReadOnlyCollection<long>? abilityIds,
            bool isMirageCard)
        {
            if (abilityIds == null || abilityIds.Count == 0)
            {
                return (true, null);
            }

            var distinctIds = abilityIds.Where(id => id > 0).Distinct().ToList();
            if (distinctIds.Count == 0)
            {
                return (true, null);
            }

            var ownerScopes = await (
                from a in _db.CardAbilities.AsNoTracking()
                join c in _db.CardDefinitions.AsNoTracking() on a.CardId equals c.Id
                where distinctIds.Contains(a.Id)
                select new { a.Id, c.IsMirageCard })
                .ToListAsync();

            var map = ownerScopes.ToDictionary(x => x.Id, x => x.IsMirageCard);
            foreach (var abilityId in distinctIds)
            {
                if (!map.TryGetValue(abilityId, out var ownerIsMirage) || ownerIsMirage != isMirageCard)
                {
                    return (false, $"关联的能力 #{abilityId} 不存在或不属于当前环境（{(isMirageCard ? "幻境" : "经典")}）。");
                }
            }

            return (true, null);
        }

        private async Task<CardSetDefinition?> EnsureMirageCardSetDefinitionAsync()
        {
            var existing = await _db.CardSetDefinitions
                .FirstOrDefaultAsync(set => set.Name == GameConstants.Cards.MirageCardSetName);
            if (existing != null)
            {
                if (existing.Environments != null && existing.Environments.Count > 0)
                {
                    existing.Environments = new List<DeckEnvironment>();
                    await _db.SaveChangesAsync();
                }
                return existing;
            }

            var usedSequences = await _db.CardSetDefinitions
                .Where(set => set.Region == 9)
                .Select(set => set.Sequence)
                .ToListAsync();
            var sequence = 99;
            while (usedSequences.Contains(sequence) && sequence > 0)
            {
                sequence--;
            }

            var definition = new CardSetDefinition
            {
                Name = GameConstants.Cards.MirageCardSetName,
                Abbreviation = "MRG",
                Region = 9,
                Sequence = sequence,
                Environments = new List<DeckEnvironment>(),
                CreatedAt = DateTime.UtcNow
            };

            _db.CardSetDefinitions.Add(definition);
            await _db.SaveChangesAsync();
            return definition;
        }

        private static int ComputeDefaultMirageTier(int cost)
        {
            if (cost <= 1) return 1;
            if (cost == 2) return 2;
            if (cost == 3) return 3;
            if (cost == 4) return 4;
            if (cost <= 6) return 5;
            return 6;
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

