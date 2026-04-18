using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/tribes")]
public class TribesController : ControllerBase
{
    private const int DefaultSuggestionLimit = 20;
    private const int MaxSuggestionLimit = 100;

    private readonly ApplicationDbContext _db;
    private readonly ITribeRegistry _tribeRegistry;
    private readonly ILogger<TribesController> _logger;

    public TribesController(
        ApplicationDbContext db,
        ITribeRegistry tribeRegistry,
        ILogger<TribesController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tribeRegistry = tribeRegistry ?? throw new ArgumentNullException(nameof(tribeRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<TribeDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TribeDefinitionDto>>> GetAll(CancellationToken cancellationToken = default)
    {
        await _tribeRegistry.RefreshAsync(cancellationToken);

        var definitions = await _db.TribeDefinitions
            .AsNoTracking()
            .OrderByDescending(item => item.IsReserved)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        if (definitions.All(item => !string.Equals(item.Name, GameConstants.Tribes.MatchAll, StringComparison.OrdinalIgnoreCase)))
        {
            definitions.Insert(0, new TribeDefinition
            {
                Name = GameConstants.Tribes.MatchAll,
                Behavior = TribeBehavior.MatchAllKnownTribes,
                IsReserved = true,
                IsEnabled = true
            });
        }

        var usageCounts = await BuildUsageCountMapAsync(cancellationToken);
        var response = definitions
            .Select(definition => ToDto(definition, usageCounts))
            .OrderByDescending(item => item.IsReserved)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(response);
    }

    [HttpGet("suggest")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> Suggest(
        [FromQuery] string? query = null,
        [FromQuery] int limit = DefaultSuggestionLimit,
        CancellationToken cancellationToken = default)
    {
        await _tribeRegistry.RefreshAsync(cancellationToken);

        var normalizedQuery = NormalizeValue(query);
        var normalizedLimit = Math.Clamp(limit, 1, MaxSuggestionLimit);
        var definitions = await _db.TribeDefinitions
            .AsNoTracking()
            .Where(item => item.IsEnabled)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            if (!string.IsNullOrWhiteSpace(definition.Name))
            {
                known.Add(definition.Name.Trim());
            }
        }

        foreach (var cardTribes in await _db.CardDefinitions
                     .AsNoTracking()
                     .Select(item => item.Tribe)
                     .ToListAsync(cancellationToken))
        {
            foreach (var tribe in _tribeRegistry.NormalizeMany(cardTribes))
            {
                known.Add(tribe);
            }
        }

        var suggestions = known
            .Where(item => string.IsNullOrWhiteSpace(normalizedQuery)
                || item.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => string.Equals(item, normalizedQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(item => item.Length)
            .ThenBy(item => item, StringComparer.OrdinalIgnoreCase)
            .Take(normalizedLimit)
            .ToList();

        return Ok(suggestions);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TribeDefinitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TribeDefinitionDto>> Create(
        [FromBody] UpsertTribeDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        var normalizedName = NormalizeValue(request.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest("Name is required.");
        }

        var existing = await _db.TribeDefinitions.FindAsync([normalizedName], cancellationToken);
        if (existing != null)
        {
            return Conflict($"Tribe \"{normalizedName}\" already exists.");
        }

        var behaviorResult = ResolveBehavior(normalizedName, request.Behavior);
        if (!behaviorResult.Success)
        {
            return BadRequest(behaviorResult.ErrorMessage);
        }

        var definition = new TribeDefinition
        {
            Name = normalizedName,
            Aliases = NormalizeAliases(request.Aliases, normalizedName),
            Behavior = behaviorResult.Value,
            IsReserved = string.Equals(normalizedName, GameConstants.Tribes.MatchAll, StringComparison.OrdinalIgnoreCase),
            IsEnabled = request.IsEnabled ?? true
        };

        _db.TribeDefinitions.Add(definition);
        await _db.SaveChangesAsync(cancellationToken);
        await _tribeRegistry.RefreshAsync(cancellationToken);

        _logger.LogInformation("Created tribe definition {TribeName}.", definition.Name);

        var usageCounts = await BuildUsageCountMapAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ToDto(definition, usageCounts));
    }

    [HttpPut("{name}")]
    [ProducesResponseType(typeof(TribeDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TribeDefinitionDto>> Update(
        [FromRoute] string name,
        [FromBody] UpdateTribeDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeValue(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest("Name is required.");
        }

        var definition = await _db.TribeDefinitions.FindAsync([normalizedName], cancellationToken);
        if (definition == null)
        {
            if (!string.Equals(normalizedName, GameConstants.Tribes.MatchAll, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            definition = new TribeDefinition
            {
                Name = GameConstants.Tribes.MatchAll,
                IsReserved = true,
                IsEnabled = true,
                Behavior = TribeBehavior.MatchAllKnownTribes
            };
            _db.TribeDefinitions.Add(definition);
        }

        if (definition.IsReserved)
        {
            definition.Aliases = NormalizeAliases(request.Aliases, definition.Name);
            definition.IsEnabled = true;
            definition.Behavior = TribeBehavior.MatchAllKnownTribes;
        }
        else
        {
            var behaviorResult = ResolveBehavior(definition.Name, request.Behavior);
            if (!behaviorResult.Success)
            {
                return BadRequest(behaviorResult.ErrorMessage);
            }

            definition.Aliases = NormalizeAliases(request.Aliases, definition.Name);
            definition.Behavior = behaviorResult.Value;
            definition.IsEnabled = request.IsEnabled ?? definition.IsEnabled;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _tribeRegistry.RefreshAsync(cancellationToken);

        var usageCounts = await BuildUsageCountMapAsync(cancellationToken);
        return Ok(ToDto(definition, usageCounts));
    }

    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeValue(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest("Name is required.");
        }

        var definition = await _db.TribeDefinitions.FindAsync([normalizedName], cancellationToken);
        if (definition == null)
        {
            return NotFound();
        }

        if (definition.IsReserved)
        {
            return BadRequest("Reserved tribe definitions cannot be deleted.");
        }

        var usageCounts = await BuildUsageCountMapAsync(cancellationToken);
        if (usageCounts.TryGetValue(definition.Name, out var usageCount) && usageCount > 0)
        {
            return BadRequest($"Tribe \"{definition.Name}\" is still used by {usageCount} card(s) and cannot be deleted.");
        }

        _db.TribeDefinitions.Remove(definition);
        await _db.SaveChangesAsync(cancellationToken);
        await _tribeRegistry.RefreshAsync(cancellationToken);

        return NoContent();
    }

    private async Task<Dictionary<string, int>> BuildUsageCountMapAsync(CancellationToken cancellationToken)
    {
        var usage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cardTribes = await _db.CardDefinitions
            .AsNoTracking()
            .Select(item => item.Tribe)
            .ToListAsync(cancellationToken);

        foreach (var tribes in cardTribes)
        {
            foreach (var tribe in _tribeRegistry.NormalizeMany(tribes))
            {
                usage[tribe] = usage.TryGetValue(tribe, out var count) ? count + 1 : 1;
            }
        }

        return usage;
    }

    private static TribeDefinitionDto ToDto(TribeDefinition definition, IReadOnlyDictionary<string, int> usageCounts)
    {
        return new TribeDefinitionDto
        {
            Name = definition.Name,
            Aliases = (definition.Aliases ?? new List<string>()).ToList(),
            Behavior = definition.Behavior.ToString(),
            IsReserved = definition.IsReserved,
            IsEnabled = definition.IsEnabled,
            UsageCount = usageCounts.TryGetValue(definition.Name, out var usageCount) ? usageCount : 0
        };
    }

    private static string NormalizeValue(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
    }

    private static List<string> NormalizeAliases(IEnumerable<string>? aliases, string tribeName)
    {
        if (aliases == null)
        {
            return new List<string>();
        }

        return aliases
            .Select(NormalizeValue)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Where(item => !string.Equals(item, tribeName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static BehaviorResult ResolveBehavior(string tribeName, string? rawBehavior)
    {
        var isMatchAllName = string.Equals(tribeName, GameConstants.Tribes.MatchAll, StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawBehavior))
        {
            return BehaviorResult.Ok(isMatchAllName ? TribeBehavior.MatchAllKnownTribes : TribeBehavior.Normal);
        }

        if (!Enum.TryParse<TribeBehavior>(rawBehavior.Trim(), true, out var behavior))
        {
            return BehaviorResult.Fail($"Unknown behavior \"{rawBehavior}\".");
        }

        if (behavior == TribeBehavior.MatchAllKnownTribes && !isMatchAllName)
        {
            return BehaviorResult.Fail("Only the reserved tribe \"全部\" can use MatchAllKnownTribes behavior.");
        }

        if (isMatchAllName)
        {
            behavior = TribeBehavior.MatchAllKnownTribes;
        }

        return BehaviorResult.Ok(behavior);
    }

    private readonly record struct BehaviorResult(bool Success, TribeBehavior Value, string ErrorMessage)
    {
        public static BehaviorResult Ok(TribeBehavior value) => new(true, value, string.Empty);
        public static BehaviorResult Fail(string errorMessage) => new(false, TribeBehavior.Normal, errorMessage);
    }

    public sealed class TribeDefinitionDto
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Aliases { get; set; } = new();
        public string Behavior { get; set; } = TribeBehavior.Normal.ToString();
        public bool IsReserved { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int UsageCount { get; set; }
    }

    public sealed class UpsertTribeDefinitionRequest
    {
        public string Name { get; set; } = string.Empty;
        public List<string>? Aliases { get; set; }
        public string? Behavior { get; set; }
        public bool? IsEnabled { get; set; }
    }

    public sealed class UpdateTribeDefinitionRequest
    {
        public List<string>? Aliases { get; set; }
        public string? Behavior { get; set; }
        public bool? IsEnabled { get; set; }
    }
}
