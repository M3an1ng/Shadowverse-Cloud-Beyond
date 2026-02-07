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

public class UserAvatarUploadTests
{
    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "TestApp";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task UploadAvatar_SavesFileAndUpdatesUser()
    {
        var dbName = $"user-avatar-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "avatar-user",
            Email = "avatar-user@local.invalid",
            PasswordHash = "hash",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var root = Path.Combine(Path.GetTempPath(), $"svb-avatar-{Guid.NewGuid():N}");
        var contentRoot = Path.Combine(root, "Server");
        Directory.CreateDirectory(contentRoot);

        try
        {
            var environment = new TestWebHostEnvironment { ContentRootPath = contentRoot };
            var controller = new UsersController(db, environment, NullLogger<UsersController>.Instance);

            var bytes = new byte[] { 1, 2, 3, 4 };
            await using var stream = new MemoryStream(bytes);
            var formFile = new FormFile(stream, 0, bytes.Length, "file", "avatar.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };

            var result = await controller.UploadAvatar(userId, formFile, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<UsersController.AvatarUploadResponse>(ok.Value);

            Assert.Equal($"/assets/avatars/{userId}/avatar.png", payload.AvatarUrl);

            var user = await db.Users.FindAsync(userId);
            Assert.NotNull(user);
            Assert.Equal(payload.AvatarUrl, user!.AvatarUrl);

            var expectedPath = Path.Combine(root, "assets", "avatars", userId.ToString(), "avatar.png");
            Assert.True(File.Exists(expectedPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
