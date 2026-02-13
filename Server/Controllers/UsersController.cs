using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using System.IO;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private const int DefaultListLimit = 50;
    private const int MaxListLimit = 200;

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UsersController> _logger;
    private static readonly HashSet<string> AllowedAvatarExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    public UsersController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        ILogger<UsersController> logger)
    {
        _db = db;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(UserListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserListResponse>> GetUsers(
        [FromQuery] Guid actorUserId,
        [FromQuery] string? keyword = null,
        [FromQuery] int limit = DefaultListLimit,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty)
        {
            return BadRequest("actorUserId is required.");
        }

        var actor = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == actorUserId, cancellationToken);
        if (!CanManageUsers(actor))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only admin can access user management.");
        }

        var take = Math.Clamp(limit, 1, MaxListLimit);
        var normalizedKeyword = (keyword ?? string.Empty).Trim();

        var query = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            var lowered = normalizedKeyword.ToLowerInvariant();
            query = query.Where(u => u.Username.ToLower().Contains(lowered));
        }

        var entities = await query
            .OrderByDescending(u => u.UpdatedAt)
            .ThenBy(u => u.Username)
            .Take(take)
            .ToListAsync(cancellationToken);
        var users = entities.Select(ToUserSummary).ToList();

        return Ok(new UserListResponse
        {
            Count = users.Count,
            Items = users
        });
    }

    [HttpPut("{userId:guid}/role")]
    [ProducesResponseType(typeof(UserSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserSummaryResponse>> UpdateUserRole(
        Guid userId,
        [FromBody] UpdateUserRoleRequest request,
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

        var actor = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);
        if (!CanManageUsers(actor))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Only admin can update user roles.");
        }

        if (!TryParseManageableRole(request.Role, out var nextRole, out var errorMessage))
        {
            return BadRequest(errorMessage);
        }

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (target == null)
        {
            return NotFound("User not found.");
        }

        if (target.Role == UserRole.Admin && nextRole != UserRole.Admin)
        {
            var activeAdminCount = await _db.Users.CountAsync(
                u => u.IsActive && u.Role == UserRole.Admin,
                cancellationToken);
            if (activeAdminCount <= 1)
            {
                return BadRequest("系统至少需要保留一个管理员账号。");
            }
        }

        target.Role = nextRole;
        target.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToUserSummary(target));
    }

    [HttpPost("{userId:guid}/avatar")]
    [ProducesResponseType(typeof(AvatarUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvatarUploadResponse>> UploadAvatar(Guid userId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0)
        {
            return BadRequest("No file uploaded.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedAvatarExtensions.Contains(extension))
        {
            return BadRequest("Unsupported image type.");
        }

        var workspaceRoot = GetWorkspaceRoot();
        var avatarFolder = Path.Combine(workspaceRoot, "assets", "avatars", userId.ToString());
        Directory.CreateDirectory(avatarFolder);

        var fileName = $"avatar{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(avatarFolder, fileName);
        var newPath = $"/assets/avatars/{userId}/{fileName}";

        TryDeleteExistingAvatar(workspaceRoot, user.AvatarUrl, newPath);

        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        user.AvatarUrl = newPath;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AvatarUploadResponse
        {
            UserId = userId,
            AvatarUrl = newPath
        });
    }

    private static bool CanManageUsers(User? user)
    {
        return user != null && user.IsActive && user.Role == UserRole.Admin;
    }

    private static int ResolvePermissionLevel(UserRole role)
    {
        return role switch
        {
            UserRole.Admin => 100,
            UserRole.Player => 10,
            _ => 0
        };
    }

    private static bool TryParseManageableRole(string? rawRole, out UserRole role, out string? errorMessage)
    {
        role = UserRole.Player;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rawRole))
        {
            errorMessage = "role is required.";
            return false;
        }

        if (!Enum.TryParse<UserRole>(rawRole.Trim(), ignoreCase: true, out role))
        {
            errorMessage = "Unsupported role.";
            return false;
        }

        if (role == UserRole.Guest)
        {
            errorMessage = "Guest role cannot be assigned to persisted users.";
            return false;
        }

        return true;
    }

    private static UserSummaryResponse ToUserSummary(User user)
    {
        return new UserSummaryResponse
        {
            UserId = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
            PermissionLevel = ResolvePermissionLevel(user.Role),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginAt = user.LastLoginAt,
            AvatarUrl = user.AvatarUrl
        };
    }

    private string GetWorkspaceRoot()
    {
        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, ".."));
    }

    private static void TryDeleteExistingAvatar(string workspaceRoot, string? existingPath, string newPath)
    {
        if (string.IsNullOrWhiteSpace(existingPath))
        {
            return;
        }

        var normalized = existingPath.Replace('\\', '/').Trim();
        if (!normalized.StartsWith("/assets/avatars/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (normalized.Equals(newPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var diskPath = GetDiskPathFromAssetPath(workspaceRoot, normalized);
        if (diskPath != null && System.IO.File.Exists(diskPath))
        {
            System.IO.File.Delete(diskPath);
        }
    }

    private static string? GetDiskPathFromAssetPath(string workspaceRoot, string assetPath)
    {
        var trimmed = assetPath.TrimStart('/');
        var combined = Path.GetFullPath(Path.Combine(workspaceRoot, trimmed));
        return combined.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase) ? combined : null;
    }

    public class AvatarUploadResponse
    {
        public Guid UserId { get; set; }
        public string AvatarUrl { get; set; } = string.Empty;
    }

    public class UpdateUserRoleRequest
    {
        public Guid ActorUserId { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class UserListResponse
    {
        public int Count { get; set; }
        public List<UserSummaryResponse> Items { get; set; } = new();
    }

    public class UserSummaryResponse
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = UserRole.Player.ToString();
        public int PermissionLevel { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
