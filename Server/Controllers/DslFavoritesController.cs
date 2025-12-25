using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Dsl;
using ShadowverseBeyond.Server.Infrastructure.Data;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/dsl-favorites")]
    public class DslFavoritesController : ControllerBase
    {
        private const int DefaultLimit = 20;
        private const int MaxLimit = 100;

        private readonly ApplicationDbContext _db;
        private readonly ILogger<DslFavoritesController> _logger;

        public DslFavoritesController(ApplicationDbContext db, ILogger<DslFavoritesController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<DslFavoriteSearchResultDto>> Search(
            [FromQuery] Guid userId,
            [FromQuery] string? query,
            [FromQuery] string? expectedType,
            [FromQuery] int page = 1,
            [FromQuery] int limit = DefaultLimit,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest(new { error = "userId is required." });
            }

            var normalizedLimit = Math.Clamp(limit <= 0 ? DefaultLimit : limit, 1, MaxLimit);
            var normalizedPage = Math.Max(1, page);

            var favorites = _db.AbilityDslFavorites
                .AsNoTracking()
                .Where(f => f.UserId == userId);

            if (!string.IsNullOrWhiteSpace(expectedType))
            {
                var expected = expectedType.Trim();
                favorites = favorites.Where(f => f.ExpectedType != null && f.ExpectedType == expected);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var keyword = query.Trim();
                var like = $"%{keyword}%";
                favorites = favorites.Where(f =>
                    EF.Functions.Like(f.Label, like) ||
                    EF.Functions.Like(f.Expression, like) ||
                    EF.Functions.Like(f.Tags, like));
            }

            var total = await favorites.CountAsync(cancellationToken);

            var items = await favorites
                .OrderByDescending(f => f.LastUsedAt ?? f.UpdatedAt)
                .ThenByDescending(f => f.UsageCount)
                .ThenByDescending(f => f.UpdatedAt)
                .Skip((normalizedPage - 1) * normalizedLimit)
                .Take(normalizedLimit)
                .Select(f => new AbilityDslFavoriteDto
                {
                    Id = f.Id,
                    UserId = f.UserId,
                    Label = f.Label,
                    Expression = f.Expression,
                    ExpectedType = f.ExpectedType,
                    Tags = f.GetTagList(),
                    UsageCount = f.UsageCount,
                    LastUsedAt = f.LastUsedAt,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return Ok(new DslFavoriteSearchResultDto
            {
                Total = total,
                Page = normalizedPage,
                Limit = normalizedLimit,
                Items = items
            });
        }

        [HttpPost]
        public async Task<ActionResult<AbilityDslFavoriteDto>> Create(
            [FromBody] CreateAbilityDslFavoriteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.UserId == Guid.Empty)
            {
                return BadRequest(new { error = "userId is required." });
            }

            var label = request.Label?.Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                return BadRequest(new { error = "label is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Expression))
            {
                return BadRequest(new { error = "expression is required." });
            }

            var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
            if (!userExists)
            {
                return NotFound(new { error = $"User {request.UserId} not found." });
            }

            var entity = new AbilityDslFavorite
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Label = label,
                Expression = request.Expression,
                ExpectedType = string.IsNullOrWhiteSpace(request.ExpectedType) ? null : request.ExpectedType.Trim(),
                UsageCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            entity.SetTagList(request.Tags);

            _db.AbilityDslFavorites.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            var dto = new AbilityDslFavoriteDto
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Label = entity.Label,
                Expression = entity.Expression,
                ExpectedType = entity.ExpectedType,
                Tags = entity.GetTagList(),
                UsageCount = entity.UsageCount,
                LastUsedAt = entity.LastUsedAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };

            return CreatedAtAction(nameof(Search), new { userId = entity.UserId }, dto);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AbilityDslFavoriteDto>> Update(
            Guid id,
            [FromBody] UpdateAbilityDslFavoriteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.UserId == Guid.Empty)
            {
                return BadRequest(new { error = "userId is required." });
            }

            var entity = await _db.AbilityDslFavorites.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound(new { error = $"Favorite {id} not found." });
            }

            if (entity.UserId != request.UserId)
            {
                return Forbid();
            }

            var hasUpdate = false;

            if (request.Label != null)
            {
                var label = request.Label.Trim();
                if (string.IsNullOrWhiteSpace(label))
                {
                    return BadRequest(new { error = "label cannot be empty." });
                }

                entity.Rename(label);
                hasUpdate = true;
            }

            if (request.Tags != null)
            {
                entity.SetTagList(request.Tags);
                hasUpdate = true;
            }

            if (request.ExpectedType != null)
            {
                entity.ExpectedType = string.IsNullOrWhiteSpace(request.ExpectedType) ? null : request.ExpectedType.Trim();
                entity.UpdatedAt = DateTime.UtcNow;
                hasUpdate = true;
            }

            if (!hasUpdate)
            {
                return BadRequest(new { error = "No fields to update." });
            }

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new AbilityDslFavoriteDto
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Label = entity.Label,
                Expression = entity.Expression,
                ExpectedType = entity.ExpectedType,
                Tags = entity.GetTagList(),
                UsageCount = entity.UsageCount,
                LastUsedAt = entity.LastUsedAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(
            Guid id,
            [FromQuery] Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest(new { error = "userId is required." });
            }

            var entity = await _db.AbilityDslFavorites.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound(new { error = $"Favorite {id} not found." });
            }

            if (entity.UserId != userId)
            {
                return Forbid();
            }

            _db.AbilityDslFavorites.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [HttpPost("{id:guid}/use")]
        public async Task<ActionResult<AbilityDslFavoriteDto>> MarkUsed(
            Guid id,
            [FromQuery] Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest(new { error = "userId is required." });
            }

            var entity = await _db.AbilityDslFavorites.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound(new { error = $"Favorite {id} not found." });
            }

            if (entity.UserId != userId)
            {
                return Forbid();
            }

            entity.UpdateUsage();
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new AbilityDslFavoriteDto
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Label = entity.Label,
                Expression = entity.Expression,
                ExpectedType = entity.ExpectedType,
                Tags = entity.GetTagList(),
                UsageCount = entity.UsageCount,
                LastUsedAt = entity.LastUsedAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            });
        }
    }
}
