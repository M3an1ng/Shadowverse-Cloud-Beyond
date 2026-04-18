using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/cards/{cardId:int}/community")]
public class CardCommunityController : ControllerBase
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;
    private const int MaxCommentLength = 500;

    private readonly ApplicationDbContext _db;
    private readonly ICardCommunityService _cardCommunityService;

    public CardCommunityController(ApplicationDbContext db, ICardCommunityService cardCommunityService)
    {
        _db = db;
        _cardCommunityService = cardCommunityService;
    }

    [HttpGet]
    public async Task<ActionResult<CardCommunityResponse>> Get(
        int cardId,
        [FromQuery] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(new { error = "userId is required." });
        }

        if (!await ValidateUserAsync(userId, cancellationToken))
        {
            return NotFound(new { error = $"User {userId} not found." });
        }

        if (!await ValidateCardAsync(cardId, cancellationToken))
        {
            return NotFound(new { error = $"Card {cardId} not found." });
        }

        var normalizedPage = Math.Max(1, page);
        var normalizedLimit = Math.Clamp(limit <= 0 ? DefaultLimit : limit, 1, MaxLimit);
        var skip = (normalizedPage - 1) * normalizedLimit;

        var commentQuery = _db.CardComments
            .AsNoTracking()
            .Where(comment => comment.CardDefinitionId == cardId);

        var totalComments = await commentQuery.CountAsync(cancellationToken);
        var comments = await commentQuery
            .OrderByDescending(comment => comment.CreatedAt)
            .Skip(skip)
            .Take(normalizedLimit)
            .Select(comment => new CardCommentDto
            {
                Id = comment.Id,
                CardDefinitionId = comment.CardDefinitionId,
                UserId = comment.UserId,
                Username = comment.User.Username,
                AvatarUrl = comment.User.AvatarUrl,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                CanDelete = comment.UserId == userId
            })
            .ToListAsync(cancellationToken);

        var voteStats = await BuildVoteStatsAsync(cardId, userId, cancellationToken);
        var voteActivities = await _db.CardVotes
            .AsNoTracking()
            .Where(vote => vote.CardDefinitionId == cardId)
            .OrderByDescending(vote => vote.UpdatedAt)
            .Take(100)
            .Select(vote => new CardVoteActivityDto
            {
                UserId = vote.UserId,
                Username = vote.User.Username,
                VoteType = vote.VoteType == CardVoteType.Upvote ? "up" : "down",
                UpdatedAt = vote.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new CardCommunityResponse
        {
            CardDefinitionId = cardId,
            UpvoteCount = voteStats.UpvoteCount,
            DownvoteCount = voteStats.DownvoteCount,
            UserVote = voteStats.UserVote,
            TotalComments = totalComments,
            Page = normalizedPage,
            Limit = normalizedLimit,
            Comments = comments,
            VoteActivities = voteActivities
        });
    }

    [HttpPost("comments")]
    public async Task<ActionResult<CardCommentDto>> CreateComment(
        int cardId,
        [FromBody] CreateCardCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        if (request.UserId == Guid.Empty)
        {
            return BadRequest(new { error = "userId is required." });
        }

        var content = (request.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new { error = "content is required." });
        }

        if (content.Length > MaxCommentLength)
        {
            return BadRequest(new { error = $"content must be <= {MaxCommentLength} characters." });
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return NotFound(new { error = $"User {request.UserId} not found." });
        }

        if (!await ValidateCardAsync(cardId, cancellationToken))
        {
            return NotFound(new { error = $"Card {cardId} not found." });
        }

        var dto = await _cardCommunityService.CreateCommentAsync(cardId, user, content, cancellationToken);
        return CreatedAtAction(nameof(Get), new { cardId, userId = request.UserId }, dto);
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(
        int cardId,
        Guid commentId,
        [FromQuery] Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(new { error = "userId is required." });
        }

        if (!await ValidateUserAsync(userId, cancellationToken))
        {
            return NotFound(new { error = $"User {userId} not found." });
        }

        var entity = await _db.CardComments
            .FirstOrDefaultAsync(comment =>
                comment.Id == commentId &&
                comment.CardDefinitionId == cardId,
                cancellationToken);
        if (entity == null)
        {
            return NotFound(new { error = $"Comment {commentId} not found." });
        }

        if (entity.UserId != userId)
        {
            return Forbid();
        }

        _db.CardComments.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("vote")]
    public async Task<ActionResult<CardVoteSummaryDto>> Vote(
        int cardId,
        [FromBody] VoteCardRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        if (request.UserId == Guid.Empty)
        {
            return BadRequest(new { error = "userId is required." });
        }

        if (!TryParseVote(request.VoteType, out var voteType))
        {
            return BadRequest(new { error = "voteType must be 'up' or 'down'." });
        }

        if (!await ValidateUserAsync(request.UserId, cancellationToken))
        {
            return NotFound(new { error = $"User {request.UserId} not found." });
        }

        if (!await ValidateCardAsync(cardId, cancellationToken))
        {
            return NotFound(new { error = $"Card {cardId} not found." });
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return NotFound(new { error = $"User {request.UserId} not found." });
        }

        var result = await _cardCommunityService.VoteAsync(cardId, user, voteType, cancellationToken);
        return Ok(result);
    }

    private async Task<bool> ValidateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.Users.AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
    }

    private async Task<bool> ValidateCardAsync(int cardId, CancellationToken cancellationToken)
    {
        return await _db.CardDefinitions.AnyAsync(card => card.Id == cardId, cancellationToken);
    }

    private async Task<(int UpvoteCount, int DownvoteCount, string? UserVote)> BuildVoteStatsAsync(
        int cardId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var upvoteCount = await _db.CardVotes.CountAsync(
            vote => vote.CardDefinitionId == cardId && vote.VoteType == CardVoteType.Upvote,
            cancellationToken);
        var downvoteCount = await _db.CardVotes.CountAsync(
            vote => vote.CardDefinitionId == cardId && vote.VoteType == CardVoteType.Downvote,
            cancellationToken);
        var userVoteType = await _db.CardVotes
            .Where(vote => vote.CardDefinitionId == cardId && vote.UserId == userId)
            .Select(vote => (CardVoteType?)vote.VoteType)
            .FirstOrDefaultAsync(cancellationToken);

        return (upvoteCount, downvoteCount, ToVoteLabel(userVoteType));
    }

    private static bool TryParseVote(string? raw, out CardVoteType voteType)
    {
        voteType = CardVoteType.Upvote;
        var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized == "up")
        {
            voteType = CardVoteType.Upvote;
            return true;
        }

        if (normalized == "down")
        {
            voteType = CardVoteType.Downvote;
            return true;
        }

        return false;
    }

    private static string? ToVoteLabel(CardVoteType? voteType)
    {
        return voteType switch
        {
            CardVoteType.Upvote => "up",
            CardVoteType.Downvote => "down",
            _ => null
        };
    }

    public class CreateCardCommentRequest
    {
        public Guid UserId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class VoteCardRequest
    {
        public Guid UserId { get; set; }
        public string VoteType { get; set; } = string.Empty;
    }

    public class CardVoteSummaryDto
    {
        public int CardDefinitionId { get; set; }
        public int UpvoteCount { get; set; }
        public int DownvoteCount { get; set; }
        public string? UserVote { get; set; }
        public bool RewardGranted { get; set; }
        public int RewardCoins { get; set; }
        public int? CurrentCoins { get; set; }
    }

    public class CardCommunityResponse : CardVoteSummaryDto
    {
        public int TotalComments { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public List<CardCommentDto> Comments { get; set; } = new();
        public List<CardVoteActivityDto> VoteActivities { get; set; } = new();
    }

    public class CardVoteActivityDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string VoteType { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public class CardCommentDto
    {
        public Guid Id { get; set; }
        public int CardDefinitionId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool CanDelete { get; set; }
        public bool RewardGranted { get; set; }
        public int RewardCoins { get; set; }
        public int? CurrentCoins { get; set; }
    }
}
