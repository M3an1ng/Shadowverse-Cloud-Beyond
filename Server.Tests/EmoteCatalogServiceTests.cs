using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EmoteCatalogServiceTests : IDisposable
    {
        private readonly string _workspaceRoot;
        private readonly string _serverRoot;

        public EmoteCatalogServiceTests()
        {
            _workspaceRoot = Path.Combine(Path.GetTempPath(), "sv-emotes-" + Guid.NewGuid().ToString("N"));
            _serverRoot = Path.Combine(_workspaceRoot, "Server");
            Directory.CreateDirectory(_serverRoot);
        }

        [Fact]
        public void GetCatalog_ShouldGroupFirstLevelFoldersAndUseStableNaturalOrdering()
        {
            WriteEmote("line", "hello2.png");
            WriteEmote("line", "hello10.png");
            WriteEmote("default", "smile.png");

            var service = CreateService();

            var catalog = service.GetCatalog();

            Assert.Equal(new[] { "default", "line" }, catalog.Packs.Select(pack => pack.Id));
            var line = Assert.Single(catalog.Packs.Where(pack => pack.Id == "line"));
            Assert.Equal(new[] { "line/hello2.png", "line/hello10.png" }, line.Items.Select(item => item.Id));
            Assert.Equal("/assets/emotes/line/hello2.png", line.Items[0].Url);
            Assert.Equal("hello2", line.Items[0].Name);
        }

        [Fact]
        public void GetCatalog_ShouldFilterUnsupportedFilesEmptyFoldersAndRootFiles()
        {
            WriteRootFile("root.png");
            WriteEmote("default", "smile.png");
            WriteEmote("default", "readme.txt");
            WriteEmote("default", "script.js");
            Directory.CreateDirectory(Path.Combine(_workspaceRoot, "assets", "emotes", "empty"));

            var service = CreateService();

            var catalog = service.GetCatalog();

            var pack = Assert.Single(catalog.Packs);
            Assert.Equal("default", pack.Id);
            var item = Assert.Single(pack.Items);
            Assert.Equal("default/smile.png", item.Id);
        }

        [Theory]
        [InlineData("../default/smile.png")]
        [InlineData("default/../smile.png")]
        [InlineData("default\\smile.png")]
        [InlineData("/default/smile.png")]
        [InlineData("https://example.test/smile.png")]
        [InlineData("default/missing.png")]
        public void TryResolveEmote_ShouldRejectTraversalAbsoluteUrlAndMissingIds(string emoteId)
        {
            WriteEmote("default", "smile.png");
            var service = CreateService();

            var resolved = service.TryResolveEmote(emoteId, out var item);

            Assert.False(resolved);
            Assert.Null(item);
        }

        [Fact]
        public void TryResolveEmote_ShouldReturnCatalogItemForValidId()
        {
            WriteEmote("default", "smile.png");
            var service = CreateService();

            var resolved = service.TryResolveEmote("default/smile.png", out var item);

            Assert.True(resolved);
            Assert.NotNull(item);
            Assert.Equal("default/smile.png", item!.Id);
            Assert.Equal("/assets/emotes/default/smile.png", item.Url);
        }

        public void Dispose()
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
        }

        private EmoteCatalogService CreateService()
        {
            return new EmoteCatalogService(
                new TestWebHostEnvironment { ContentRootPath = _serverRoot },
                Microsoft.Extensions.Options.Options.Create(new EmoteCatalogOptions()),
                TimeProvider.System,
                NullLogger<EmoteCatalogService>.Instance);
        }

        private void WriteEmote(string pack, string fileName)
        {
            var directory = Path.Combine(_workspaceRoot, "assets", "emotes", pack);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), "sample");
        }

        private void WriteRootFile(string fileName)
        {
            var directory = Path.Combine(_workspaceRoot, "assets", "emotes");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), "sample");
        }

        private sealed class TestWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Development";
            public string ApplicationName { get; set; } = "Tests";
            public string WebRootPath { get; set; } = string.Empty;
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
