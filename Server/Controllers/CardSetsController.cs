using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/cardsets")]
public class CardSetsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CardSetsController> _logger;
    private readonly IWebHostEnvironment _environment;

    public CardSetsController(ApplicationDbContext db, ILogger<CardSetsController> logger, IWebHostEnvironment environment)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment;
    }

    [HttpPost("{id:int}/logo"), RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<ActionResult<CardSetDefinitionDto>> UploadLogo(int id, [FromForm] IFormFile file, CancellationToken ct, [FromForm] Guid? actorUserId = null)
    {
        var effectiveActorUserId = actorUserId ?? Guid.Empty;
        if (effectiveActorUserId == Guid.Empty || !await CanManageEnvironmentMembershipAsync(effectiveActorUserId, ct))
            return StatusCode(StatusCodes.Status403Forbidden, "仅管理员可上传或修改卡包 Logo。");

        var setDefinition = await _db.CardSetDefinitions.FindAsync(new object[] { id }, ct);
        if (setDefinition == null) return NotFound();
        if (file == null || file.Length == 0 || file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "图片大小应在10MB以内" });

        var bytes = new byte[12];
        await using (var stream = file.OpenReadStream())
        {
            if (await stream.ReadAsync(bytes, ct) < 12) return BadRequest();
        }
        string? extension = bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 ? ".png"
            : bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? ".jpg"
            : System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" ? ".webp" : null;
        if (extension == null) return BadRequest(new { error = "只支持PNG、JPEG、WebP图片" });

        var folder = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "assets", "card-sets"));
        Directory.CreateDirectory(folder);
        var filename = $"set-{id}-{Guid.NewGuid():N}{extension}";
        await using (var output = System.IO.File.Create(Path.Combine(folder, filename)))
            await file.CopyToAsync(output, ct);

        var key = $"cardset-logo:{id}";
        var doc = await _db.FeatureDocuments.FindAsync(new object[] { key }, ct);
        if (doc == null)
        {
            doc = new FeatureDocument { Id = key };
            _db.FeatureDocuments.Add(doc);
        }
        doc.Json = JsonSerializer.Serialize(new CardSetLogo(id, $"/assets/card-sets/{filename}"), CardStyleCatalog.JsonOptions);
        doc.Revision++;
        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        CardStyleCatalog.Apply(doc);
        return Ok(CardSetDefinitionDto.FromEntity(setDefinition));
    }

    [HttpPost]
    public async Task<ActionResult<CardSetDefinitionDto>> CreateCardSet([FromBody] CreateCardSetRequest request, CancellationToken ct)
    {
        if (request.ActorUserId == Guid.Empty || !await CanManageEnvironmentMembershipAsync(request.ActorUserId, ct))
            return StatusCode(StatusCodes.Status403Forbidden, "仅管理员可新建卡包。");
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("请填写卡包名称。");

        var name = request.Name.Trim();
        if (string.Equals(name, GameConstants.Cards.MirageCardSetName, StringComparison.OrdinalIgnoreCase))
            return BadRequest("不能使用幻境专属卡包名称。");
        if (await _db.CardSetDefinitions.AnyAsync(set => set.Name.ToLower() == name.ToLower(), ct))
            return Conflict("同名卡包已存在。");

        var usedSequences = await _db.CardSetDefinitions
            .Where(set => set.Region == 0)
            .Select(set => set.Sequence)
            .ToListAsync(ct);
        var sequence = 2;
        while (sequence <= 99 && usedSequences.Contains(sequence)) sequence++;
        if (sequence > 99) return BadRequest("当前区域的卡包序号已用尽（002–099）。");

        var definition = new CardSetDefinition
        {
            Name = name,
            Abbreviation = (request.Abbreviation ?? "").Trim(),
            Region = 0,
            Sequence = sequence,
            Environments = new List<DeckEnvironment> { DeckEnvironment.Open },
            CreatedAt = DateTime.UtcNow
        };
        _db.CardSetDefinitions.Add(definition);
        await _db.SaveChangesAsync(ct);
        return Ok(CardSetDefinitionDto.FromEntity(definition));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CardSetDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CardSetDefinitionDto>>> GetCardSets([FromQuery] string? scope = "standard")
    {
        try
        {
            var isMirageScope = MirageCardPoolIsolationRules.TryParseEditorScope(scope, out var parsedMirage) && parsedMirage;
            var definitions = await _db.CardSetDefinitions
                .AsNoTracking()
                .Where(set => isMirageScope
                    ? set.Name == GameConstants.Cards.MirageCardSetName
                    : set.Name != GameConstants.Cards.MirageCardSetName)
                .OrderBy(set => set.Region)
                .ThenBy(set => set.Sequence)
                .ToListAsync();

            var dtos = definitions.Select(CardSetDefinitionDto.FromEntity).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load card set definitions.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to load card set definitions.");
        }
    }

    [HttpGet("environment-membership")]
    [ProducesResponseType(typeof(CardSetEnvironmentMembershipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CardSetEnvironmentMembershipResponse>> GetEnvironmentMembership(
        [FromQuery] DeckEnvironment? environment = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetEnvironment = environment ?? DeckEnvironment.Rotation;
            var rows = await _db.CardSetDefinitions
                .AsNoTracking()
                .Where(set => set.Name != GameConstants.Cards.MirageCardSetName)
                .OrderBy(set => set.Region)
                .ThenBy(set => set.Sequence)
                .Select(set => new CardSetEnvironmentMembershipItem
                {
                    CardSetDefinitionId = set.Id,
                    Name = set.Name,
                    Abbreviation = set.Abbreviation,
                    SetCode = set.SetCode,
                    LogoUrl = CardStyleCatalog.GetCardSetLogo(set.Id),
                    Included = (set.Environments ?? new List<DeckEnvironment>()).Contains(targetEnvironment)
                })
                .ToListAsync(cancellationToken);

            return Ok(new CardSetEnvironmentMembershipResponse
            {
                Environment = targetEnvironment,
                IncludedCardSetDefinitionIds = rows.Where(item => item.Included).Select(item => item.CardSetDefinitionId).ToList(),
                Items = rows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load card set environment membership.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to load card set environment membership.");
        }
    }

    [HttpPut("environment-membership")]
    [ProducesResponseType(typeof(CardSetEnvironmentMembershipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CardSetEnvironmentMembershipResponse>> UpdateEnvironmentMembership(
        [FromBody] UpdateCardSetEnvironmentMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        if (request.ActorUserId == Guid.Empty)
        {
            return BadRequest("actorUserId is required.");
        }

        if (!await CanManageEnvironmentMembershipAsync(request.ActorUserId, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only admin can update card set environment membership.");
        }

        var targetEnvironment = request.Environment ?? DeckEnvironment.Rotation;
        var includedIds = (request.IncludedCardSetDefinitionIds ?? new List<int>())
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (includedIds.Any(id => id <= 0))
        {
            return BadRequest("includedCardSetDefinitionIds must contain positive integers.");
        }

        var existingIds = await _db.CardSetDefinitions
            .AsNoTracking()
            .Where(set => includedIds.Contains(set.Id) && set.Name != GameConstants.Cards.MirageCardSetName)
            .Select(set => set.Id)
            .ToListAsync(cancellationToken);
        var missing = includedIds.Except(existingIds).OrderBy(id => id).ToList();
        if (missing.Count > 0)
        {
            return BadRequest($"Unknown or restricted cardSetDefinitionId: {string.Join(", ", missing)}.");
        }

        try
        {
            var includedSet = includedIds.ToHashSet();
            var cardSets = await _db.CardSetDefinitions
                .Where(set => set.Name != GameConstants.Cards.MirageCardSetName)
                .OrderBy(set => set.Region)
                .ThenBy(set => set.Sequence)
                .ToListAsync(cancellationToken);

            foreach (var set in cardSets)
            {
                var envs = (set.Environments ?? new List<DeckEnvironment>()).Distinct().ToHashSet();
                if (includedSet.Contains(set.Id))
                {
                    envs.Add(targetEnvironment);
                }
                else
                {
                    envs.Remove(targetEnvironment);
                }

                set.Environments = envs.OrderBy(value => (int)value).ToList();
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Card set environment membership updated by {ActorUserId}. Environment={Environment} IncludedCount={IncludedCount}",
                request.ActorUserId,
                targetEnvironment,
                includedIds.Count);

            var rows = cardSets.Select(set => new CardSetEnvironmentMembershipItem
            {
                CardSetDefinitionId = set.Id,
                Name = set.Name,
                Abbreviation = set.Abbreviation,
                SetCode = set.SetCode,
                LogoUrl = CardStyleCatalog.GetCardSetLogo(set.Id),
                Included = (set.Environments ?? new List<DeckEnvironment>()).Contains(targetEnvironment)
            }).ToList();

            return Ok(new CardSetEnvironmentMembershipResponse
            {
                Environment = targetEnvironment,
                IncludedCardSetDefinitionIds = rows.Where(item => item.Included).Select(item => item.CardSetDefinitionId).ToList(),
                Items = rows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update card set environment membership.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update card set environment membership.");
        }
    }

    private async Task<bool> CanManageEnvironmentMembershipAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var actor = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == actorUserId, cancellationToken);
        return actor != null && actor.IsActive && actor.Role == UserRole.Admin;
    }

    public sealed class CreateCardSetRequest
    {
        public Guid ActorUserId { get; set; }
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Name { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string? Abbreviation { get; set; }
    }

    public sealed class UpdateCardSetEnvironmentMembershipRequest
    {
        public Guid ActorUserId { get; set; }
        public DeckEnvironment? Environment { get; set; }
        public List<int>? IncludedCardSetDefinitionIds { get; set; }
    }

    public sealed class CardSetEnvironmentMembershipResponse
    {
        public DeckEnvironment Environment { get; set; } = DeckEnvironment.Rotation;
        public List<int> IncludedCardSetDefinitionIds { get; set; } = new();
        public List<CardSetEnvironmentMembershipItem> Items { get; set; } = new();
    }

    public sealed class CardSetEnvironmentMembershipItem
    {
        public int CardSetDefinitionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string SetCode { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public bool Included { get; set; }
    }
}

