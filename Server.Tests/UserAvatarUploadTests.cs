using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
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

    private static ApplicationDbContext CreateDbContext(string dbName, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptors)
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
            var controller = new UsersController(db, environment, new Pbkdf2PasswordHasher(), NullLogger<UsersController>.Instance);

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

            Assert.StartsWith($"/assets/avatars/{userId}/", payload.AvatarUrl);
            Assert.EndsWith(".png", payload.AvatarUrl);

            var user = await db.Users.FindAsync(userId);
            Assert.NotNull(user);
            Assert.Equal(payload.AvatarUrl, user!.AvatarUrl);

            var expectedPath = Path.Combine(root, payload.AvatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(expectedPath));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(expectedPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Theory]
    [InlineData("replacement.png")]
    [InlineData("replacement.webp")]
    public async Task UploadAvatar_CopyFailureKeepsPublishedAvatar(string fileName)
    {
        await using var replacement = await AvatarReplacement.CreateAsync();
        var failure = new IOException("Injected avatar copy failure.");
        var upload = new Mock<IFormFile>(MockBehavior.Strict);
        upload.SetupGet(file => file.Length).Returns(4);
        upload.SetupGet(file => file.FileName).Returns(fileName);
        upload.Setup(file => file.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream stream, CancellationToken cancellationToken) =>
            {
                await stream.WriteAsync(new byte[] { 9, 9 }, cancellationToken);
                throw failure;
            });

        var observed = await Assert.ThrowsAsync<IOException>(() =>
            replacement.Controller.UploadAvatar(replacement.UserId, upload.Object, CancellationToken.None));

        Assert.Same(failure, observed);
        await replacement.AssertPreviousAvatarAsync();
    }

    [Theory]
    [InlineData("replacement.png")]
    [InlineData("replacement.webp")]
    public async Task UploadAvatar_SaveFailureRestoresPreviousAvatarAndRemovesNewFile(string fileName)
    {
        var failure = new DbUpdateException("Injected avatar save failure.");
        await using var replacement = await AvatarReplacement.CreateAsync(
            new AvatarSaveInterceptor(_ => throw failure));

        var observed = await Assert.ThrowsAsync<DbUpdateException>(() => replacement.UploadAsync(fileName));

        Assert.Same(failure, observed);
        await replacement.AssertPreviousAvatarAsync();
    }

    [Theory]
    [InlineData("replacement.png")]
    [InlineData("replacement.webp")]
    public async Task UploadAvatar_CancelledCopyPreservesAvatarAndCancellation(string fileName)
    {
        await using var replacement = await AvatarReplacement.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var upload = new Mock<IFormFile>(MockBehavior.Strict);
        upload.SetupGet(file => file.Length).Returns(4);
        upload.SetupGet(file => file.FileName).Returns(fileName);
        upload.Setup(file => file.CopyToAsync(It.IsAny<Stream>(), cancellation.Token))
            .Returns(async (Stream stream, CancellationToken cancellationToken) =>
            {
                await stream.WriteAsync(new byte[] { 9, 9 }, cancellationToken);
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            });

        var observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            replacement.Controller.UploadAvatar(replacement.UserId, upload.Object, cancellation.Token));

        Assert.Equal(cancellation.Token, observed.CancellationToken);
        await replacement.AssertPreviousAvatarAsync();
    }

    [Theory]
    [InlineData("replacement.png")]
    [InlineData("replacement.webp")]
    public async Task UploadAvatar_ReplacementPublishesReturnedUrlAndRemovesUnusedFiles(string fileName)
    {
        await using var replacement = await AvatarReplacement.CreateAsync();

        var response = await replacement.UploadAsync(fileName);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<UsersController.AvatarUploadResponse>(ok.Value);
        Assert.Equal(replacement.UserId, payload.UserId);
        Assert.StartsWith($"/assets/avatars/{replacement.UserId}/", payload.AvatarUrl);
        Assert.EndsWith(Path.GetExtension(fileName), payload.AvatarUrl);
        var publishedFile = Assert.Single(Directory.GetFiles(replacement.AvatarFolder));
        Assert.Equal(replacement.GetDiskPath(payload.AvatarUrl), publishedFile);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, await File.ReadAllBytesAsync(publishedFile));
        var stored = await replacement.Db.Users.AsNoTracking().SingleAsync();
        Assert.Equal(payload.AvatarUrl, stored.AvatarUrl);
    }

    [Fact]
    public async Task UploadAvatar_UnsupportedExtensionDoesNotChangePublishedAvatar()
    {
        await using var replacement = await AvatarReplacement.CreateAsync();

        var response = await replacement.UploadAsync("replacement.gif");

        var rejected = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("Unsupported image type.", rejected.Value);
        await replacement.AssertPreviousAvatarAsync();
    }

    private sealed class AvatarSaveInterceptor(Action<User> onSave) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries<User>())
            {
                if (entry.State == EntityState.Modified)
                {
                    onSave(entry.Entity);
                }
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AvatarReplacement : IAsyncDisposable
    {
        private static readonly DateTime PreviousUpdatedAt = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly string _workspaceRoot;
        private readonly byte[] _previousBytes = { 1, 2, 3, 4 };
        public Guid UserId { get; } = Guid.NewGuid();
        public ApplicationDbContext Db { get; }
        public UsersController Controller { get; }
        public string PreviousPath => $"/assets/avatars/{UserId}/avatar.png";
        public string PreviousFile => GetDiskPath(PreviousPath);
        public string AvatarFolder => Path.Combine(_workspaceRoot, "assets", "avatars", UserId.ToString());

        private AvatarReplacement(ApplicationDbContext db)
        {
            Db = db;
            _workspaceRoot = Path.Combine(Path.GetTempPath(), $"svb-avatar-replacement-{Guid.NewGuid():N}");
            var contentRoot = Path.Combine(_workspaceRoot, "Server");
            Directory.CreateDirectory(contentRoot);
            Directory.CreateDirectory(AvatarFolder);
            File.WriteAllBytes(PreviousFile, _previousBytes);
            var environment = new TestWebHostEnvironment { ContentRootPath = contentRoot };
            Controller = new UsersController(db, environment, new Pbkdf2PasswordHasher(), NullLogger<UsersController>.Instance);
        }

        public static async Task<AvatarReplacement> CreateAsync(params IInterceptor[] interceptors)
        {
            var replacement = new AvatarReplacement(CreateDbContext($"avatar-replacement-{Guid.NewGuid():N}", interceptors));
            replacement.Db.Users.Add(new User
            {
                Id = replacement.UserId,
                Username = "avatar-replacement-user",
                Email = "avatar-replacement@local.invalid",
                PasswordHash = "hash",
                IsActive = true,
                AvatarUrl = replacement.PreviousPath,
                UpdatedAt = PreviousUpdatedAt
            });
            await replacement.Db.SaveChangesAsync();
            return replacement;
        }

        public async Task<ActionResult<UsersController.AvatarUploadResponse>> UploadAsync(string fileName)
        {
            var bytes = new byte[] { 5, 6, 7, 8 };
            await using var stream = new MemoryStream(bytes);
            var upload = new FormFile(stream, 0, bytes.Length, "file", fileName);
            return await Controller.UploadAvatar(UserId, upload, CancellationToken.None);
        }

        public string GetDiskPath(string avatarPath) =>
            Path.Combine(_workspaceRoot, avatarPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        public async Task AssertPreviousAvatarAsync()
        {
            Assert.True(File.Exists(PreviousFile), "The published avatar must survive a failed replacement.");
            Assert.Equal(_previousBytes, await File.ReadAllBytesAsync(PreviousFile));
            Assert.Equal(PreviousFile, Assert.Single(Directory.GetFiles(AvatarFolder)));
            var tracked = await Db.Users.SingleAsync();
            var stored = await Db.Users.AsNoTracking().SingleAsync();
            foreach (var user in new[] { tracked, stored })
            {
                Assert.Equal(PreviousPath, user.AvatarUrl);
                Assert.Equal(PreviousUpdatedAt, user.UpdatedAt);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            Directory.Delete(_workspaceRoot, true);
        }
    }
}
