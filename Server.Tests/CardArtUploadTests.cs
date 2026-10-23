using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
}
