using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/deck-copy-limit-policies")]
public class DeckCopyLimitPoliciesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DeckCopyLimitPoliciesController> _logger;

    public DeckCopyLimitPoliciesController(ApplicationDbContext db, ILogger<DeckCopyLimitPoliciesController> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(typeof(DeckCopyLimitPolicyListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeckCopyLimitPolicyListResponse>> GetByFormat(
        [FromQuery] DeckEnvironment? environment = null,
        [FromQuery] DeckRule? rule = null,
        CancellationToken cancellationToken = default)
    {
        var targetEnvironment = environment ?? DeckEnvironment.Rotation;
        var targetRule = rule ?? DeckRule.Standard;

        var rows = await _db.DeckCardCopyLimitPolicies
            .AsNoTracking()
            .Where(item => item.Environment == targetEnvironment && item.Rule == targetRule)
            .OrderBy(item => item.CardDefinitionId)
            .Select(item => new DeckCopyLimitPolicyItemResponse
            {
                CardDefinitionId = item.CardDefinitionId,
                MaxCopies = item.MaxCopies,
                UpdatedAtUtc = item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new DeckCopyLimitPolicyListResponse
        {
            Environment = targetEnvironment,
            Rule = targetRule,
            DefaultMaxCopies = GameConstants.Cards.MaxCopiesPerCard,
            Items = rows
        });
    }

    [HttpPut]
    [ProducesResponseType(typeof(DeckCopyLimitPolicyListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeckCopyLimitPolicyListResponse>> BatchUpsert(
        [FromBody] BatchUpsertDeckCopyLimitPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        if (request.ActorUserId == Guid.Empty)
        {
            return BadRequest("actorUserId is required.");
        }

        if (!await CanManagePoliciesAsync(request.ActorUserId, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only admin can update deck copy limit policies.");
        }

        var targetEnvironment = request.Environment ?? DeckEnvironment.Rotation;
        var targetRule = request.Rule ?? DeckRule.Standard;

        var items = request.Items ?? new List<BatchUpsertDeckCopyLimitPolicyItem>();
        if (items.Count == 0)
        {
            return Ok(await BuildResponseAsync(targetEnvironment, targetRule, cancellationToken));
        }

        var duplicateCardId = items
            .GroupBy(item => item.CardDefinitionId)
            .FirstOrDefault(group => group.Count() > 1)?
            .Key;
        if (duplicateCardId.HasValue)
        {
            return BadRequest($"Duplicate cardDefinitionId in request: {duplicateCardId.Value}.");
        }

        foreach (var item in items)
        {
            if (item.CardDefinitionId <= 0)
            {
                return BadRequest("cardDefinitionId must be positive.");
            }

            if (item.MaxCopies < 0 || item.MaxCopies > GameConstants.Cards.MaxCopiesPerCard)
            {
                return BadRequest($"maxCopies must be between 0 and {GameConstants.Cards.MaxCopiesPerCard}.");
            }
        }

        var cardIds = items.Select(item => item.CardDefinitionId).Distinct().ToList();
        var existingCardIds = await _db.CardDefinitions
            .AsNoTracking()
            .Where(card => cardIds.Contains(card.Id))
            .Select(card => card.Id)
            .ToListAsync(cancellationToken);
        var missingCardIds = cardIds.Except(existingCardIds).OrderBy(id => id).ToList();
        if (missingCardIds.Count > 0)
        {
            return BadRequest($"Unknown cardDefinitionId: {string.Join(", ", missingCardIds)}.");
        }

        var existingRows = await _db.DeckCardCopyLimitPolicies
            .Where(item =>
                item.Environment == targetEnvironment &&
                item.Rule == targetRule &&
                cardIds.Contains(item.CardDefinitionId))
            .ToListAsync(cancellationToken);
        var existingByCardId = existingRows.ToDictionary(item => item.CardDefinitionId, item => item);

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            if (existingByCardId.TryGetValue(item.CardDefinitionId, out var row))
            {
                row.MaxCopies = item.MaxCopies;
                row.UpdatedAtUtc = now;
            }
            else
            {
                _db.DeckCardCopyLimitPolicies.Add(new DeckCardCopyLimitPolicy
                {
                    Id = Guid.NewGuid(),
                    Environment = targetEnvironment,
                    Rule = targetRule,
                    CardDefinitionId = item.CardDefinitionId,
                    MaxCopies = item.MaxCopies,
                    UpdatedAtUtc = now
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deck copy limit policies updated by {ActorUserId}. Environment={Environment} Rule={Rule} Count={Count}",
            request.ActorUserId,
            targetEnvironment,
            targetRule,
            items.Count);

        return Ok(await BuildResponseAsync(targetEnvironment, targetRule, cancellationToken));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(
        [FromQuery] Guid actorUserId,
        [FromQuery] int cardDefinitionId,
        [FromQuery] DeckEnvironment? environment = null,
        [FromQuery] DeckRule? rule = null,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty)
        {
            return BadRequest("actorUserId is required.");
        }

        if (!await CanManagePoliciesAsync(actorUserId, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only admin can delete deck copy limit policies.");
        }

        if (cardDefinitionId <= 0)
        {
            return BadRequest("cardDefinitionId must be positive.");
        }

        var targetEnvironment = environment ?? DeckEnvironment.Rotation;
        var targetRule = rule ?? DeckRule.Standard;

        var row = await _db.DeckCardCopyLimitPolicies.FirstOrDefaultAsync(
            item => item.Environment == targetEnvironment &&
                    item.Rule == targetRule &&
                    item.CardDefinitionId == cardDefinitionId,
            cancellationToken);
        if (row != null)
        {
            _db.DeckCardCopyLimitPolicies.Remove(row);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private async Task<DeckCopyLimitPolicyListResponse> BuildResponseAsync(
        DeckEnvironment environment,
        DeckRule rule,
        CancellationToken cancellationToken)
    {
        var rows = await _db.DeckCardCopyLimitPolicies
            .AsNoTracking()
            .Where(item => item.Environment == environment && item.Rule == rule)
            .OrderBy(item => item.CardDefinitionId)
            .Select(item => new DeckCopyLimitPolicyItemResponse
            {
                CardDefinitionId = item.CardDefinitionId,
                MaxCopies = item.MaxCopies,
                UpdatedAtUtc = item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new DeckCopyLimitPolicyListResponse
        {
            Environment = environment,
            Rule = rule,
            DefaultMaxCopies = GameConstants.Cards.MaxCopiesPerCard,
            Items = rows
        };
    }

    private async Task<bool> CanManagePoliciesAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var actor = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == actorUserId, cancellationToken);
        return actor != null && actor.IsActive && actor.Role == UserRole.Admin;
    }

    public sealed class BatchUpsertDeckCopyLimitPolicyRequest
    {
        public Guid ActorUserId { get; set; }
        public DeckEnvironment? Environment { get; set; }
        public DeckRule? Rule { get; set; }
        public List<BatchUpsertDeckCopyLimitPolicyItem>? Items { get; set; }
    }

    public sealed class BatchUpsertDeckCopyLimitPolicyItem
    {
        public int CardDefinitionId { get; set; }
        public int MaxCopies { get; set; }
    }

    public sealed class DeckCopyLimitPolicyListResponse
    {
        public DeckEnvironment Environment { get; set; } = DeckEnvironment.Rotation;
        public DeckRule Rule { get; set; } = DeckRule.Standard;
        public int DefaultMaxCopies { get; set; } = GameConstants.Cards.MaxCopiesPerCard;
        public List<DeckCopyLimitPolicyItemResponse> Items { get; set; } = new();
    }

    public sealed class DeckCopyLimitPolicyItemResponse
    {
        public int CardDefinitionId { get; set; }
        public int MaxCopies { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}

