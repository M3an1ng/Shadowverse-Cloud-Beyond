using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/cardsets")]
public class CardSetsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CardSetsController> _logger;

    public CardSetsController(ApplicationDbContext db, ILogger<CardSetsController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CardSetDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CardSetDefinitionDto>>> GetCardSets()
    {
        try
        {
            var definitions = await _db.CardSetDefinitions
                .AsNoTracking()
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
                .OrderBy(set => set.Region)
                .ThenBy(set => set.Sequence)
                .Select(set => new CardSetEnvironmentMembershipItem
                {
                    CardSetDefinitionId = set.Id,
                    Name = set.Name,
                    Abbreviation = set.Abbreviation,
                    SetCode = set.SetCode,
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
            .Where(set => includedIds.Contains(set.Id))
            .Select(set => set.Id)
            .ToListAsync(cancellationToken);
        var missing = includedIds.Except(existingIds).OrderBy(id => id).ToList();
        if (missing.Count > 0)
        {
            return BadRequest($"Unknown cardSetDefinitionId: {string.Join(", ", missing)}.");
        }

        try
        {
            var includedSet = includedIds.ToHashSet();
            var cardSets = await _db.CardSetDefinitions
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
        public bool Included { get; set; }
    }
}

