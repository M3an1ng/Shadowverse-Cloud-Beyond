using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.Users;

public class UserManagementControllerTests
{
    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "TestApp";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static UsersController CreateController(ApplicationDbContext db)
    {
        var environment = new TestWebHostEnvironment();
        return new UsersController(db, environment, NullLogger<UsersController>.Instance);
    }

    [Fact]
    public async Task GetUsers_AdminCanListUsers()
    {
        var dbName = $"user-list-admin-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var adminId = Guid.NewGuid();
        db.Users.AddRange(
            new User
            {
                Id = adminId,
                Username = "admin",
                Email = "admin@local.invalid",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                IsActive = true
            },
            new User
            {
                Id = Guid.NewGuid(),
                Username = "player-a",
                Email = "player-a@local.invalid",
                PasswordHash = "hash",
                Role = UserRole.Player,
                IsActive = true
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetUsers(adminId, null, 50, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<UsersController.UserListResponse>(ok.Value);

        Assert.Equal(2, payload.Count);
        Assert.Contains(payload.Items, item => item.UserId == adminId && item.Role == "Admin" && item.PermissionLevel == 100);
        Assert.Contains(payload.Items, item => item.Role == "Player" && item.PermissionLevel == 10);
    }

    [Fact]
    public async Task GetUsers_PlayerIsForbidden()
    {
        var dbName = $"user-list-player-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var playerId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = playerId,
            Username = "player",
            Email = "player@local.invalid",
            PasswordHash = "hash",
            Role = UserRole.Player,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetUsers(playerId, null, 50, CancellationToken.None);
        var forbidden = Assert.IsType<ObjectResult>(result.Result);

        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task UpdateUserRole_AdminCanPromoteUser()
    {
        var dbName = $"user-role-update-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        db.Users.AddRange(
            new User
            {
                Id = adminId,
                Username = "admin",
                Email = "admin@local.invalid",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                IsActive = true
            },
            new User
            {
                Id = targetId,
                Username = "target",
                Email = "target@local.invalid",
                PasswordHash = "hash",
                Role = UserRole.Player,
                IsActive = true
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = new UsersController.UpdateUserRoleRequest
        {
            ActorUserId = adminId,
            Role = "Admin"
        };

        var result = await controller.UpdateUserRole(targetId, request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<UsersController.UserSummaryResponse>(ok.Value);

        Assert.Equal("Admin", payload.Role);
        Assert.Equal(100, payload.PermissionLevel);

        var targetUser = await db.Users.FindAsync(targetId);
        Assert.NotNull(targetUser);
        Assert.Equal(UserRole.Admin, targetUser!.Role);
    }

    [Fact]
    public async Task UpdateUserRole_CannotDemoteLastAdmin()
    {
        var dbName = $"user-role-last-admin-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var adminId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = adminId,
            Username = "admin",
            Email = "admin@local.invalid",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = new UsersController.UpdateUserRoleRequest
        {
            ActorUserId = adminId,
            Role = "Player"
        };

        var result = await controller.UpdateUserRole(adminId, request, CancellationToken.None);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("系统至少需要保留一个管理员账号。", badRequest.Value);
    }
}
