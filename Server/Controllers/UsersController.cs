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
}
