using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using Xunit;
using Moq;

namespace ShadowverseBeyond.Server.Tests
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using ShadowverseBeyond.Server.Controllers;

    [Collection("DatabaseCollection")]
    public class DiagnosticsControllerTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private readonly IServiceProvider _serviceProvider;

        public DiagnosticsControllerTests(DatabaseFixture fixture, Xunit.Abstractions.ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.OutputHelper = output;
            _serviceProvider = _fixture.ServiceProvider;
        }

        public Task InitializeAsync() => Task.CompletedTask;
        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task Get_Abilities_ReturnsOkResult_WithData()
        {
            // Arrange
            // The GameDataProvider is now the real one, seeded by the fixture
            var gameDataProvider = _serviceProvider.GetRequiredService<IGameDataProvider>();

            // Ensure the provider is initialized (the fixture should have done this, but we can be explicit)
            if (!gameDataProvider.IsInitialized)
            {
                await gameDataProvider.InitializeAsync();
            }

            var logger = _serviceProvider.GetRequiredService<ILogger<DiagnosticsController>>();
            var controller = new DiagnosticsController(logger);

            // Act
            var response = controller.GetAllAbilities();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(response.Result);
            var data = Assert.IsAssignableFrom<IReadOnlyDictionary<int, List<AbilityConfig>>>(okResult.Value);
            Assert.True(data.Count > 0, "Expected the seeded abilities to be returned.");
            Assert.Equal((int)HttpStatusCode.OK, okResult.StatusCode);
        }
    }
}