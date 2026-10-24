using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Services;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class GameDataPublicationTests : IDisposable
{
    private readonly List<ServiceProvider> _containers = new();

    [Fact]
    public async Task IncrementalRefreshAfterFullRefreshStartsIsNotOverwritten()
    {
        var originalInstance = GameDataProvider.Instance;
        var allCards = new TaskCompletionSource<IReadOnlyList<CardDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<ICardRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetAllCardDefinitionsAsync()).Returns(() => allCards.Task);
        repository.Setup(repo => repo.GetCardDefinitionByIdAsync(42))
            .ReturnsAsync(new CardDefinition { Id = 42, Name = "incremental" });
        var provider = CreateProvider(repository.Object);

        try
        {
            var fullRefresh = provider.RefreshCacheAsync();
            Assert.False(fullRefresh.IsCompleted);
            var incrementalRefresh = provider.RefreshCardAsync(42);
            allCards.SetResult(Array.Empty<CardDefinition>());
            await Task.WhenAll(fullRefresh, incrementalRefresh);

            Assert.Equal("incremental", provider.GetCardDefinition(42)?.Name);
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(originalInstance);
        }
    }

    [Fact]
    public async Task ClearCacheDuringRefreshDoesNotAllowTheOldLoadToRepublish()
    {
        var originalInstance = GameDataProvider.Instance;
        var allCards = new TaskCompletionSource<IReadOnlyList<CardDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<ICardRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetAllCardDefinitionsAsync()).Returns(() => allCards.Task);
        var provider = CreateProvider(repository.Object);

        try
        {
            var refresh = provider.RefreshCacheAsync();
            Assert.False(refresh.IsCompleted);

            provider.ClearCache();
            allCards.SetResult(new[] { new CardDefinition { Id = 7, Name = "stale" } });
            await Assert.ThrowsAsync<OperationCanceledException>(() => refresh);

            Assert.False(provider.IsInitialized);
            Assert.Null(provider.GetCardDefinition(7));
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(originalInstance);
        }
    }

    [Theory]
    [InlineData("full")]
    [InlineData("card")]
    public async Task AbilityReadFailureKeepsThePublishedSnapshotAndRevision(string refreshKind)
    {
        var originalInstance = GameDataProvider.Instance;
        var repository = new Mock<ICardRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetAllCardDefinitionsAsync())
            .ReturnsAsync(new[] { new CardDefinition { Id = 7, Name = "published" } });
        repository.Setup(repo => repo.GetCardDefinitionByIdAsync(7))
            .ReturnsAsync(new CardDefinition { Id = 7, Name = "unpublished" });
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        Exception? queryFailure = null;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => repository.Object);
        services.AddScoped<ApplicationDbContext>(_ =>
        {
            var context = new Mock<ApplicationDbContext>(options) { CallBase = true };
            if (queryFailure != null)
            {
                context.Setup(db => db.Set<AbilityDefinition>()).Throws(queryFailure);
            }
            return context.Object;
        });
        services.AddScoped<AbilityLoader>();
        using var container = services.BuildServiceProvider();
        var provider = new GameDataProvider(container.GetRequiredService<IServiceScopeFactory>(),
            container.GetRequiredService<ILogger<GameDataProvider>>());

        try
        {
            await provider.InitializeAsync();
            var revision = provider.DataRevision;
            repository.Setup(repo => repo.GetAllCardDefinitionsAsync())
                .ReturnsAsync(new[] { new CardDefinition { Id = 7, Name = "unpublished" } });
            var failure = new InvalidOperationException("Ability database read failed.");
            queryFailure = failure;

            var observed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                refreshKind == "full" ? provider.RefreshCacheAsync() : provider.RefreshCardAsync(7));

            Assert.Same(failure, observed);
            Assert.Equal(revision, provider.DataRevision);
            Assert.Equal("published", provider.GetCardDefinition(7)?.Name);
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(originalInstance);
        }
    }

    [Theory]
    [InlineData("initialize")]
    [InlineData("card")]
    public async Task ClearCacheRevokesOtherPendingLoadKinds(string loadKind)
    {
        var originalInstance = GameDataProvider.Instance;
        var allCards = new TaskCompletionSource<IReadOnlyList<CardDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oneCard = new TaskCompletionSource<CardDefinition?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<ICardRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetAllCardDefinitionsAsync()).Returns(allCards.Task);
        repository.Setup(repo => repo.GetCardDefinitionByIdAsync(7)).Returns(oneCard.Task);
        var provider = CreateProvider(repository.Object);

        try
        {
            var pending = loadKind == "initialize" ? provider.InitializeAsync() : provider.RefreshCardAsync(7);
            Assert.False(pending.IsCompleted);
            provider.ClearCache();
            allCards.SetResult(new[] { new CardDefinition { Id = 7 } });
            oneCard.SetResult(new CardDefinition { Id = 7 });

            await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
            Assert.False(provider.IsInitialized);
            Assert.Equal(Guid.Empty, provider.DataRevision);
            Assert.Empty(provider.Cards);
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(originalInstance);
        }
    }

    [Fact]
    public async Task ClearCacheRevokesQueuedLoadsAndAllowsANewExplicitRefresh()
    {
        var originalInstance = GameDataProvider.Instance;
        var allCards = new TaskCompletionSource<IReadOnlyList<CardDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<ICardRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetAllCardDefinitionsAsync()).Returns(allCards.Task);
        repository.Setup(repo => repo.GetCardDefinitionByIdAsync(7))
            .ReturnsAsync(new CardDefinition { Id = 7 });
        var provider = CreateProvider(repository.Object);

        try
        {
            var pendingFull = provider.RefreshCacheAsync();
            var pendingCard = provider.RefreshCardAsync(7);
            provider.ClearCache();
            allCards.SetResult(Array.Empty<CardDefinition>());
            await Assert.ThrowsAsync<OperationCanceledException>(() => pendingFull);
            await Assert.ThrowsAsync<OperationCanceledException>(() => pendingCard);
            Assert.Empty(provider.Cards);
            Assert.Equal(Guid.Empty, provider.DataRevision);

            await provider.RefreshCardAsync(7);
            Assert.Equal(7, provider.GetCardDefinition(7)?.Id);
            Assert.NotEqual(Guid.Empty, provider.DataRevision);
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(originalInstance);
        }
    }

    [Fact]
    public async Task ConcurrentDifferentCardRefreshesBothRemainVisible()
    {
        var originalInstance = GameDataProvider.Instance;
        var firstCard = new TaskCompletionSource<CardDefinition?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<ICardRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetCardDefinitionByIdAsync(7)).Returns(firstCard.Task);
        repository.Setup(repo => repo.GetCardDefinitionByIdAsync(8))
            .ReturnsAsync(new CardDefinition { Id = 8, Name = "second" });
        var provider = CreateProvider(repository.Object);

        try
        {
            var first = provider.RefreshCardAsync(7);
            var second = provider.RefreshCardAsync(8);
            firstCard.SetResult(new CardDefinition { Id = 7, Name = "first" });
            await Task.WhenAll(first, second);

            Assert.Equal("first", provider.GetCardDefinition(7)?.Name);
            Assert.Equal("second", provider.GetCardDefinition(8)?.Name);
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(originalInstance);
        }
    }

    [Fact]
    public async Task ConcurrentInitializationSharesOneLoadAndAcceptsEmptyData()
    {
        var originalInstance = GameDataProvider.Instance;
        var allCards = new TaskCompletionSource<IReadOnlyList<CardDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<ICardRepository>(MockBehavior.Strict);
        repository.SetupSequence(repo => repo.GetAllCardDefinitionsAsync())
            .Returns(allCards.Task)
            .Throws(new InvalidOperationException("A second initialization load is not expected."));
        var provider = CreateProvider(repository.Object);

        try
        {
            var first = provider.InitializeAsync();
            var second = provider.InitializeAsync();
            allCards.SetResult(Array.Empty<CardDefinition>());
            await Task.WhenAll(first, second);

            Assert.True(provider.IsInitialized);
            Assert.NotEqual(Guid.Empty, provider.DataRevision);
            Assert.Empty(provider.Cards);
            Assert.Empty(provider.GetAllAbilityConfigs());
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(originalInstance);
        }
    }

    [Fact]
    public async Task IncrementalReplacementRebuildsItsIndexesAndPreservesOtherCards()
    {
        var originalInstance = GameDataProvider.Instance;
        var firstCard = new CardDefinition
        {
            Id = 7, Name = "old", CardSetDefinition = new CardSetDefinition()
        };
        var secondCard = new CardDefinition { Id = 8, Name = "other" };
        var repository = new Mock<ICardRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetAllCardDefinitionsAsync()).ReturnsAsync(new[] { firstCard, secondCard });
        repository.Setup(repo => repo.GetCardDefinitionByIdAsync(7))
            .ReturnsAsync(new CardDefinition { Id = 7, Name = "new" });
        var provider = CreateProvider(repository.Object,
            new AbilityDefinition { Id = 101, CardId = 7, Name = "old ability", JsonConfig = "{}" },
            new AbilityDefinition { Id = 202, CardId = 8, Name = "other ability", JsonConfig = "{}" });

        try
        {
            await provider.InitializeAsync();
            var oldRevision = provider.DataRevision;
            Assert.Single(provider.GetCardDefinitionsForEnvironment(DeckEnvironment.Open));
            using (var scope = _containers[^1].CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await context.CardAbilities.SingleAsync(ability => ability.Id == 101)).IsActive = false;
                context.CardAbilities.Add(new AbilityDefinition { Id = 303, CardId = 7, Name = "new ability", JsonConfig = "{}" });
                await context.SaveChangesAsync();
            }

            await provider.RefreshCardAsync(7);
            Assert.Equal("new", provider.GetCardDefinition(7)?.Name);
            Assert.Equal("other", provider.GetCardDefinition(8)?.Name);
            Assert.Null(provider.GetAbilityConfig(101));
            Assert.Equal("new ability", provider.GetAbilityConfig(303)?.Name);
            Assert.Equal("other ability", provider.GetAbilityConfig(202)?.Name);
            Assert.Empty(provider.GetCardDefinitionsForEnvironment(DeckEnvironment.Open));
            Assert.NotEqual(oldRevision, provider.DataRevision);
        }
        finally
        {
            GameDataProvider.SetInstanceForTesting(originalInstance);
        }
    }

    private GameDataProvider CreateProvider(ICardRepository repository, params AbilityDefinition[] abilities)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<AbilityLoader>();
        services.AddScoped(_ => repository);

        var serviceProvider = services.BuildServiceProvider();
        _containers.Add(serviceProvider);
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.CardAbilities.AddRange(abilities);
            context.SaveChanges();
        }
        return new GameDataProvider(serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<ILogger<GameDataProvider>>());
    }

    public void Dispose()
    {
        foreach (var container in _containers)
        {
            container.Dispose();
        }
    }

}
