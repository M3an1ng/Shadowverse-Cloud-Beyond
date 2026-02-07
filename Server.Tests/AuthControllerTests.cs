using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.Auth;

public class AuthControllerTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class StubGuestDeckMigrationService : IGuestDeckMigrationService
    {
        public Guid? LastGuestId { get; private set; }
        public Guid? LastUserId { get; private set; }
        public GuestDeckMigrationResult Result { get; set; } = new(0, 0);

        public Task<GuestDeckMigrationResult> MigrateAsync(Guid guestId, Guid userId, CancellationToken cancellationToken)
        {
            LastGuestId = guestId;
            LastUserId = userId;
            return Task.FromResult(Result);
        }
    }

    [Fact]
    public async Task Register_CreatesUserAndMigratesGuestDecks()
    {
        var dbName = $"auth-register-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var hasher = new Pbkdf2PasswordHasher();
        var migration = new StubGuestDeckMigrationService
        {
            Result = new GuestDeckMigrationResult(2, 0)
        };
        var controller = new AuthController(db, hasher, migration, NullLogger<AuthController>.Instance);

        var guestId = Guid.NewGuid();
        var request = new AuthController.RegisterRequest
        {
            Username = "player-one",
            Password = "secret123",
            GuestId = guestId
        };

        var result = await controller.Register(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AuthController.AuthResponse>(ok.Value);

        Assert.NotEqual(Guid.Empty, payload.UserId);
        Assert.Equal("player-one", payload.Username);
        Assert.Equal(2, payload.MigratedDecks);
        Assert.Equal(guestId, migration.LastGuestId);
        Assert.Equal(payload.UserId, migration.LastUserId);

        var stored = await db.Users.FindAsync(payload.UserId);
        Assert.NotNull(stored);
        Assert.True(hasher.VerifyPassword("secret123", stored!.PasswordHash));
    }

    [Fact]
    public async Task Login_ReturnsUserWhenPasswordValid()
    {
        var dbName = $"auth-login-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var hasher = new Pbkdf2PasswordHasher();
        var migration = new StubGuestDeckMigrationService();
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "tester",
            Email = "user-tester@local.invalid",
            PasswordHash = hasher.HashPassword("secret123"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = new AuthController(db, hasher, migration, NullLogger<AuthController>.Instance);

        var request = new AuthController.LoginRequest
        {
            Username = "tester",
            Password = "secret123"
        };

        var result = await controller.Login(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AuthController.AuthResponse>(ok.Value);

        Assert.Equal(userId, payload.UserId);
        Assert.Equal("tester", payload.Username);
    }
}
