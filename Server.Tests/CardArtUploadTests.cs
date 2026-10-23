using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Infrastructure.Persistence;
using ShadowverseBeyond.Server.Mappings;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.Cards;

public class CardArtUploadTests
{
    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ShadowverseBeyond.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
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

    private static byte[] CreatePngBytes()
    {
        using var image = new Image<Rgba32>(2, 2);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static byte[] CreateWebpBytes()
    {
        using var image = new Image<Rgba32>(2, 2);
        using var stream = new MemoryStream();
        image.SaveAsWebp(stream);
        return stream.ToArray();
    }

    private static byte[] CreateGifBytes()
    {
        using var image = new Image<Rgba32>(2, 2);
        using var stream = new MemoryStream();
        image.SaveAsGif(stream);
        return stream.ToArray();
    }

    private static CardsController CreateController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        int expectedUpdatedCardId,
        out Mock<IGameService> gameServiceMock)
    {
        var repository = new EFCardRepository(db);
        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<CardDefinitionProfile>());
        var mapper = mapperConfig.CreateMapper();

        gameServiceMock = new Mock<IGameService>(MockBehavior.Strict);
        gameServiceMock
            .Setup(service => service.NotifyCardDefinitionUpdatedAsync(expectedUpdatedCardId))
            .Returns(() => Task.CompletedTask);

        var gameDataProviderMock = new Mock<IGameDataProvider>(MockBehavior.Loose);
        var tribeRegistryMock = new Mock<ITribeRegistry>(MockBehavior.Loose);

        return new CardsController(
            repository,
            NullLogger<CardsController>.Instance,
            mapper,
            db,
            environment,
            gameServiceMock.Object,
            gameDataProviderMock.Object,
            tribeRegistryMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task UploadDetailArt_ConvertsToWebpAndUpdatesCardDefinition()
    {
        var dbName = $"card-art-upload-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        const int cardId = 777001;
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = cardId,
            Name = "Detail Art Test",
            Type = CardType.Follower,
            Cost = 1,
            Attack = 1,
            Health = 1,
            Class = CardClass.Neutral,
            Description = "test"
        });
        await db.SaveChangesAsync();

        var root = Path.Combine(Path.GetTempPath(), $"svb-card-art-{Guid.NewGuid():N}");
        var contentRoot = Path.Combine(root, "Server");
        Directory.CreateDirectory(contentRoot);

        try
        {
            var environment = new TestWebHostEnvironment { ContentRootPath = contentRoot };
            var controller = CreateController(db, environment, cardId, out var gameServiceMock);

            var pngBytes = CreatePngBytes();
            await using var stream = new MemoryStream(pngBytes);
            var formFile = new FormFile(stream, 0, pngBytes.Length, "file", "detail.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };

            var actionResult = await controller.UploadDetailArt(cardId, formFile);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var payload = Assert.IsType<CardDefinitionDto>(okResult.Value);

            Assert.NotNull(payload.ArtDetail);
            Assert.EndsWith(".webp", payload.ArtDetail, StringComparison.OrdinalIgnoreCase);

            var updatedCard = await db.CardDefinitions.FindAsync(cardId);
            Assert.NotNull(updatedCard);
            Assert.NotNull(updatedCard!.ArtDetail);
            Assert.EndsWith(".webp", updatedCard.ArtDetail, StringComparison.OrdinalIgnoreCase);

            var relativePath = updatedCard.ArtDetail!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var expectedOutputPath = Path.Combine(root, relativePath);
            Assert.True(File.Exists(expectedOutputPath));

            gameServiceMock.Verify(service => service.NotifyCardDefinitionUpdatedAsync(cardId), Times.Once);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task UploadDetailArt_AcceptsBrowserGeneratedWebp()
    {
        var dbName = $"card-art-upload-webp-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        const int cardId = 777002;
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = cardId,
            Name = "Browser WebP Test",
            Type = CardType.Follower,
            Cost = 1,
            Attack = 1,
            Health = 1,
            Class = CardClass.Neutral,
            Description = "test"
        });
        await db.SaveChangesAsync();

        var root = Path.Combine(Path.GetTempPath(), $"svb-card-art-{Guid.NewGuid():N}");
        var contentRoot = Path.Combine(root, "Server");
        Directory.CreateDirectory(contentRoot);

        try
        {
            var environment = new TestWebHostEnvironment { ContentRootPath = contentRoot };
            var controller = CreateController(db, environment, cardId, out var gameServiceMock);

            var webpBytes = CreateWebpBytes();
            await using var stream = new MemoryStream(webpBytes);
            var formFile = new FormFile(stream, 0, webpBytes.Length, "file", "detail.webp")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/webp"
            };

            var actionResult = await controller.UploadDetailArt(cardId, formFile);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var payload = Assert.IsType<CardDefinitionDto>(okResult.Value);
            Assert.NotNull(payload.ArtDetail);
            Assert.EndsWith(".webp", payload.ArtDetail, StringComparison.OrdinalIgnoreCase);
            gameServiceMock.Verify(service => service.NotifyCardDefinitionUpdatedAsync(cardId), Times.Once);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task UploadDetailArt_AcceptsValidImageWhenFilenameHasNoExtension()
    {
        var dbName = $"card-art-upload-no-extension-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        const int cardId = 777003;
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = cardId,
            Name = "No Extension Upload Test",
            Type = CardType.Follower,
            Cost = 1,
            Attack = 1,
            Health = 1,
            Class = CardClass.Neutral,
            Description = "test"
        });
        await db.SaveChangesAsync();

        var root = Path.Combine(Path.GetTempPath(), $"svb-card-art-{Guid.NewGuid():N}");
        var contentRoot = Path.Combine(root, "Server");
        Directory.CreateDirectory(contentRoot);

        try
        {
            var environment = new TestWebHostEnvironment { ContentRootPath = contentRoot };
            var controller = CreateController(db, environment, cardId, out var gameServiceMock);

            var webpBytes = CreateWebpBytes();
            await using var stream = new MemoryStream(webpBytes);
            var formFile = new FormFile(stream, 0, webpBytes.Length, "file", "blob")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/webp"
            };

            var actionResult = await controller.UploadDetailArt(cardId, formFile);

            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var payload = Assert.IsType<CardDefinitionDto>(okResult.Value);
            Assert.NotNull(payload.ArtDetail);
            Assert.EndsWith(".webp", payload.ArtDetail, StringComparison.OrdinalIgnoreCase);
            gameServiceMock.Verify(service => service.NotifyCardDefinitionUpdatedAsync(cardId), Times.Once);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task UploadDetailArt_RejectsUnsupportedDecodedImageFormat()
    {
        var dbName = $"card-art-upload-gif-{Guid.NewGuid()}";
        await using var db = CreateDbContext(dbName);
        const int cardId = 777004;
        db.CardDefinitions.Add(new CardDefinition
        {
            Id = cardId,
            Name = "Unsupported Format Test",
            Type = CardType.Follower,
            Cost = 1,
            Attack = 1,
            Health = 1,
            Class = CardClass.Neutral,
            Description = "test"
        });
        await db.SaveChangesAsync();

        var root = Path.Combine(Path.GetTempPath(), $"svb-card-art-{Guid.NewGuid():N}");
        var contentRoot = Path.Combine(root, "Server");
        Directory.CreateDirectory(contentRoot);

        try
        {
            var environment = new TestWebHostEnvironment { ContentRootPath = contentRoot };
            var controller = CreateController(db, environment, cardId, out var gameServiceMock);

            var gifBytes = CreateGifBytes();
            await using var stream = new MemoryStream(gifBytes);
            var formFile = new FormFile(stream, 0, gifBytes.Length, "file", "detail.gif")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/gif"
            };

            var actionResult = await controller.UploadDetailArt(cardId, formFile);

            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            Assert.Equal("Unsupported image content.", badRequest.Value);
            gameServiceMock.Verify(service => service.NotifyCardDefinitionUpdatedAsync(cardId), Times.Never);
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
    [InlineData("preview")]
    [InlineData("detail")]
    public async Task ReplaceArt_UnsupportedFormatPreservesPreviousArt(string kind)
    {
        await using var replacement = await ArtReplacement.CreateAsync(kind);

        var response = await replacement.UploadAsync(CreateGifBytes());

        Assert.IsType<BadRequestObjectResult>(response.Result);
        await replacement.AssertPreviousArtAsync();
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("detail")]
    public async Task ReplaceArt_InvalidImagePreservesPreviousArt(string kind)
    {
        await using var replacement = await ArtReplacement.CreateAsync(kind);

        var response = await replacement.UploadAsync(new byte[] { 1, 2, 3, 4 });

        Assert.IsType<BadRequestObjectResult>(response.Result);
        await replacement.AssertPreviousArtAsync();
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("detail")]
    public async Task ReplaceArt_ConversionFailureRemovesUncommittedOutput(string kind)
    {
        await using var replacement = await ArtReplacement.CreateAsync(kind);
        var bytes = CreatePngBytes();
        using var stream = new FailingDisposeStream(bytes);
        var upload = new Mock<IFormFile>(MockBehavior.Strict);
        upload.SetupGet(file => file.Length).Returns(bytes.Length);
        upload.Setup(file => file.OpenReadStream()).Returns(stream);

        var response = await replacement.UploadAsync(upload.Object);

        var error = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
        await replacement.AssertPreviousArtAsync();
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("detail")]
    public async Task ReplaceArt_SaveFailurePreservesPreviousArtAndMetadata(string kind)
    {
        var failure = new DbUpdateException("Injected art save failure.");
        var interceptor = new RejectArtSaveInterceptor(failure);
        await using var replacement = await ArtReplacement.CreateAsync(kind, interceptor);

        var observed = await Assert.ThrowsAsync<DbUpdateException>(
            () => replacement.UploadAsync(CreatePngBytes()));

        Assert.Same(failure, observed);
        await replacement.AssertPreviousArtAsync();
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("detail")]
    public async Task ReplaceArt_SuccessPersistsNewWebpBeforeRemovingPreviousArt(string kind)
    {
        await using var replacement = await ArtReplacement.CreateAsync(kind);

        var response = await replacement.UploadAsync(CreatePngBytes());

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<CardDefinitionDto>(ok.Value);
        var stored = await replacement.Db.CardDefinitions.AsNoTracking().SingleAsync();
        var newPath = ArtReplacement.GetArtPath(stored, kind);
        Assert.NotEqual(replacement.PreviousPath, newPath);
        Assert.Equal(newPath, kind == "preview" ? dto.ArtPreview : dto.ArtDetail);
        Assert.False(File.Exists(replacement.PreviousFile));
        var newFile = Assert.Single(Directory.GetFiles(replacement.CardFolder));
        Assert.Equal(replacement.GetDiskPath(newPath!), newFile);
        using var image = await Image.LoadAsync(newFile);
        Assert.Equal("image/webp", image.Metadata.DecodedImageFormat?.DefaultMimeType);
        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        replacement.GameService.Verify(
            service => service.NotifyCardDefinitionUpdatedAsync(ArtReplacement.CardId), Times.Once);
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("detail")]
    public async Task ReplaceArt_NotificationFailureDoesNotUndoPersistedArt(string kind)
    {
        await using var replacement = await ArtReplacement.CreateAsync(kind);
        var failure = new IOException("Injected notification failure.");
        replacement.GameService
            .Setup(service => service.NotifyCardDefinitionUpdatedAsync(ArtReplacement.CardId))
            .ThrowsAsync(failure);

        var observed = await Assert.ThrowsAsync<IOException>(
            () => replacement.UploadAsync(CreatePngBytes()));

        Assert.Same(failure, observed);
        var stored = await replacement.Db.CardDefinitions.AsNoTracking().SingleAsync();
        var newPath = ArtReplacement.GetArtPath(stored, kind);
        Assert.NotEqual(replacement.PreviousPath, newPath);
        Assert.True(File.Exists(replacement.GetDiskPath(newPath!)));
        Assert.False(File.Exists(replacement.PreviousFile));
        replacement.GameService.Verify(
            service => service.NotifyCardDefinitionUpdatedAsync(ArtReplacement.CardId), Times.Once);
    }

    private sealed class FailingDisposeStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask DisposeAsync()
        {
            base.Dispose();
            return ValueTask.FromException(new IOException("Injected input disposal failure after encoding."));
        }
    }

    private sealed class RejectArtSaveInterceptor(DbUpdateException failure) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var entries = eventData.Context!.ChangeTracker.Entries<CardDefinition>();
            if (entries.Any(entry => entry.State != EntityState.Added &&
                (entry.Entity.ArtPreview != entry.Property(card => card.ArtPreview).OriginalValue ||
                 entry.Entity.ArtDetail != entry.Property(card => card.ArtDetail).OriginalValue)))
            {
                throw failure;
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class ArtReplacement : IAsyncDisposable
    {
        public const int CardId = 777010;
        private static readonly DateTime PreviousUpdatedAt = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const string PreviousUpdatedBy = "original-editor";
        private readonly string _kind;
        private readonly string _workspaceRoot;
        private readonly byte[] _previousBytes;
        private readonly CardsController _controller;
        public ApplicationDbContext Db { get; }
        public Mock<IGameService> GameService { get; }
        public string PreviousPath { get; }
        public string PreviousFile => GetDiskPath(PreviousPath);
        public string CardFolder => Path.Combine(_workspaceRoot, "assets", "cards", CardId.ToString());

        private ArtReplacement(string kind, ApplicationDbContext db)
        {
            _kind = kind;
            Db = db;
            _workspaceRoot = Path.Combine(Path.GetTempPath(), $"svb-art-replacement-{Guid.NewGuid():N}");
            PreviousPath = $"/assets/cards/{CardId}/{kind}-original.webp";
            _previousBytes = CreateWebpBytes();
            var contentRoot = Path.Combine(_workspaceRoot, "Server");
            Directory.CreateDirectory(contentRoot);
            Directory.CreateDirectory(CardFolder);
            File.WriteAllBytes(PreviousFile, _previousBytes);
            var environment = new TestWebHostEnvironment { ContentRootPath = contentRoot };
            _controller = CreateController(db, environment, CardId, out var gameService);
            GameService = gameService;
        }

        public static async Task<ArtReplacement> CreateAsync(string kind, params IInterceptor[] interceptors)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"art-replacement-{Guid.NewGuid():N}")
                .AddInterceptors(interceptors)
                .Options;
            var replacement = new ArtReplacement(kind, new ApplicationDbContext(options));
            replacement.Db.CardDefinitions.Add(new CardDefinition
            {
                Id = CardId,
                Name = "Art replacement",
                Type = CardType.Follower,
                Class = CardClass.Neutral,
                Description = "test",
                ArtPreview = kind == "preview" ? replacement.PreviousPath : null,
                ArtDetail = kind == "detail" ? replacement.PreviousPath : null,
                UpdatedAt = PreviousUpdatedAt,
                UpdatedBy = PreviousUpdatedBy
            });
            await replacement.Db.SaveChangesAsync();
            return replacement;
        }

        public async Task<ActionResult<CardDefinitionDto>> UploadAsync(byte[] bytes)
        {
            await using var stream = new MemoryStream(bytes);
            return await UploadAsync(new FormFile(stream, 0, bytes.Length, "file", "replacement"));
        }

        public Task<ActionResult<CardDefinitionDto>> UploadAsync(IFormFile upload) => _kind switch
        {
            "preview" => _controller.UploadPreviewArt(CardId, upload),
            "detail" => _controller.UploadDetailArt(CardId, upload),
            _ => throw new ArgumentOutOfRangeException(nameof(_kind))
        };

        public string GetDiskPath(string artPath) =>
            Path.Combine(_workspaceRoot, artPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        public static string? GetArtPath(CardDefinition card, string kind) => kind switch
        {
            "preview" => card.ArtPreview,
            "detail" => card.ArtDetail,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public async Task AssertPreviousArtAsync()
        {
            Assert.True(File.Exists(PreviousFile), "The previous art file must survive a failed replacement.");
            Assert.Equal(_previousBytes, await File.ReadAllBytesAsync(PreviousFile));
            Assert.Equal(PreviousFile, Assert.Single(Directory.GetFiles(CardFolder)));
            var tracked = await Db.CardDefinitions.SingleAsync();
            var stored = await Db.CardDefinitions.AsNoTracking().SingleAsync();
            foreach (var card in new[] { tracked, stored })
            {
                Assert.Equal(PreviousPath, GetArtPath(card, _kind));
                Assert.Equal(PreviousUpdatedAt, card.UpdatedAt);
                Assert.Equal(PreviousUpdatedBy, card.UpdatedBy);
            }
            GameService.Verify(service => service.NotifyCardDefinitionUpdatedAsync(CardId), Times.Never);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            Directory.Delete(_workspaceRoot, true);
        }
    }
}
