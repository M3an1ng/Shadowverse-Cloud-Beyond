using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const int MinPasswordLength = 6;
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IGuestDeckMigrationService _guestDeckMigration;
    private readonly IQuestProgressService _questProgressService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IGuestDeckMigrationService guestDeckMigration,
        IQuestProgressService questProgressService,
        ILogger<AuthController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _guestDeckMigration = guestDeckMigration;
        _questProgressService = questProgressService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("请求体不能为空。");
        }

        var username = NormalizeUsername(request.Username);
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("用户名不能为空。");
        }

        if (username.Length > 50)
        {
            return BadRequest("用户名长度不能超过 50。");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < MinPasswordLength)
        {
            return BadRequest($"密码长度至少为 {MinPasswordLength}。");
        }

        var normalized = username.ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(
            user => user.Username.ToLower() == normalized,
            cancellationToken);
        if (exists)
        {
            return Conflict("用户名已存在。");
        }

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = userId,
            Username = username,
            Email = $"user-{userId:N}@local.invalid",
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            CreatedAt = now,
            UpdatedAt = now,
            LastLoginAt = now,
            Role = UserRole.Player,
            IsActive = true,
            IsEmailVerified = false
        };

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Failed to register user {Username}", username);
            return Conflict("用户名已存在。");
        }

        var migrated = 0;
        var failed = 0;
        if (request.GuestId.HasValue && request.GuestId.Value != Guid.Empty)
        {
            var migration = await _guestDeckMigration.MigrateAsync(request.GuestId.Value, userId, cancellationToken);
            migrated = migration.MigratedCount;
            failed = migration.FailedCount;
        }

        return Ok(new AuthResponse
        {
            UserId = userId,
            Username = username,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            PermissionLevel = ResolvePermissionLevel(user.Role),
            MigratedDecks = migrated,
            FailedDecks = failed
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("请求体不能为空。");
        }

        var username = NormalizeUsername(request.Username);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("用户名和密码不能为空。");
        }

        var normalized = username.ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == normalized,
            cancellationToken);
        if (user == null || !user.IsActive)
        {
            return Unauthorized("用户名或密码错误。");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized("用户名或密码错误。");
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _questProgressService.RecordActivityAsync(
            user.Id,
            QuestActivityType.Login,
            "Login",
            "login",
            occurredAtUtc: user.LastLoginAt,
            cancellationToken: cancellationToken);

        return Ok(new AuthResponse
        {
            UserId = user.Id,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            PermissionLevel = ResolvePermissionLevel(user.Role),
            MigratedDecks = 0,
            FailedDecks = 0
        });
    }

    private static string NormalizeUsername(string? username)
    {
        return string.IsNullOrWhiteSpace(username) ? string.Empty : username.Trim();
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

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public Guid? GuestId { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = UserRole.Player.ToString();
        public int PermissionLevel { get; set; }
        public int MigratedDecks { get; set; }
        public int FailedDecks { get; set; }
    }
}
