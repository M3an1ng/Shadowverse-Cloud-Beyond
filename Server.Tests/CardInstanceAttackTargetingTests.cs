using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class CardInstanceAttackTargetingTests
{
    private const int AttackerDefinitionId = 1001;
    private const int WardFollowerDefinitionId = 1002;
    private const int NormalFollowerDefinitionId = 1003;
    private const int LeaderDefinitionId = 1004;

    [Fact]
    public void CanPerformAttack_WithoutIgnoreWard_CannotBypassWardFollower()
    {
        using var fixture = CreateFixture(attackerKeywords: Array.Empty<CardKeyword>());

        Assert.True(fixture.Attacker.CanPerformAttack(fixture.GameState, fixture.AttackingPlayer, fixture.WardFollower));
        Assert.False(fixture.Attacker.CanPerformAttack(fixture.GameState, fixture.AttackingPlayer, fixture.NormalFollower));
        Assert.False(fixture.Attacker.CanPerformAttack(fixture.GameState, fixture.AttackingPlayer, fixture.DefendingLeader));
    }

    [Fact]
    public void CanPerformAttack_WithIgnoreWard_CanBypassWardFollower()
    {
        using var fixture = CreateFixture(attackerKeywords: new[] { CardKeyword.IgnoreWard });

        Assert.True(fixture.Attacker.CanPerformAttack(fixture.GameState, fixture.AttackingPlayer, fixture.WardFollower));
        Assert.True(fixture.Attacker.CanPerformAttack(fixture.GameState, fixture.AttackingPlayer, fixture.NormalFollower));
        Assert.True(fixture.Attacker.CanPerformAttack(fixture.GameState, fixture.AttackingPlayer, fixture.DefendingLeader));
    }

    private static AttackFixture CreateFixture(IReadOnlyCollection<CardKeyword> attackerKeywords)
    {
        SetCardDefinitions();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
        services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

        var buffService = new Mock<IBuffService>();
        buffService.Setup(service => service.GetEffectiveAttack(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
            .Returns<CardInstance, GameContext>((card, _) => card.BaseAttack);
        buffService.Setup(service => service.GetEffectiveMaxHealth(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
            .Returns<CardInstance, GameContext>((card, _) => card.BaseHealth <= 0 ? 20 : card.BaseHealth);
        buffService.Setup(service => service.GetEffectiveCost(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
            .Returns<CardInstance, GameContext>((card, _) => card.BaseCost);
        buffService.Setup(service => service.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
            .Returns<CardInstance, GameContext>((card, _) => GetKeywords(card, attackerKeywords));
        services.AddSingleton(buffService.Object);

        var serviceProvider = services.BuildServiceProvider();
        var attackerPlayer = new PlayerState(Guid.NewGuid());
        var defendingPlayer = new PlayerState(Guid.NewGuid());
        var gameState = new GameState("attack-targeting", attackerPlayer, defendingPlayer, attackerPlayer.PlayerId)
        {
            CurrentPlayerId = attackerPlayer.PlayerId,
            AbilityManager = new Mock<IAbilityManager>().Object,
            BuffManager = Mock.Of<IBuffManager>()
        };

        var attacker = new CardInstance(Guid.NewGuid(), AttackerDefinitionId, attackerPlayer.PlayerId)
        {
            CurrentZone = CardZone.Field,
            ZoneStayTurn = 1
        };
        var wardFollower = new CardInstance(Guid.NewGuid(), WardFollowerDefinitionId, defendingPlayer.PlayerId)
        {
            CurrentZone = CardZone.Field,
            ZoneStayTurn = 1
        };
        var normalFollower = new CardInstance(Guid.NewGuid(), NormalFollowerDefinitionId, defendingPlayer.PlayerId)
        {
            CurrentZone = CardZone.Field,
            ZoneStayTurn = 1
        };
        var leader = new CardInstance(Guid.NewGuid(), LeaderDefinitionId, defendingPlayer.PlayerId)
        {
            CurrentZone = CardZone.Field
        };

        attackerPlayer.Field.Add(attacker);
        defendingPlayer.Field.Add(wardFollower);
        defendingPlayer.Field.Add(normalFollower);
        attackerPlayer.Leader = new CardInstance(Guid.NewGuid(), LeaderDefinitionId, attackerPlayer.PlayerId) { CurrentZone = CardZone.Field };
        defendingPlayer.Leader = leader;

        gameState.RegisterCardInstance(attacker);
        gameState.RegisterCardInstance(wardFollower);
        gameState.RegisterCardInstance(normalFollower);
        gameState.RegisterCardInstance(attackerPlayer.Leader);
        gameState.RegisterCardInstance(leader);

        var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
        var context = new GameContext(serviceProvider, Mock.Of<IMapper>(), accessor, gameState, attackerPlayer.PlayerId);
        GameServiceProvider.ContextAccessor = accessor;
        GameServiceProvider.Logger = NullLogger.Instance;

        return new AttackFixture(context, gameState, attackerPlayer, attacker, wardFollower, normalFollower, leader);
    }

    private static List<CardKeyword> GetKeywords(CardInstance card, IReadOnlyCollection<CardKeyword> attackerKeywords)
    {
        if (card.BaseDefinitionId == AttackerDefinitionId)
        {
            return new List<CardKeyword>(attackerKeywords);
        }

        if (card.BaseDefinitionId == WardFollowerDefinitionId)
        {
            return new List<CardKeyword> { CardKeyword.Ward };
        }

        return new List<CardKeyword>();
    }

    private static void SetCardDefinitions()
    {
        var mockProvider = new Mock<IGameDataProvider>();
        mockProvider.Setup(provider => provider.GetCardDefinition(AttackerDefinitionId))
            .Returns(new CardDefinition { Id = AttackerDefinitionId, Type = CardType.Follower, Name = "Attacker", Attack = 3, Health = 3 });
        mockProvider.Setup(provider => provider.GetCardDefinition(WardFollowerDefinitionId))
            .Returns(new CardDefinition { Id = WardFollowerDefinitionId, Type = CardType.Follower, Name = "Ward", Attack = 1, Health = 3 });
        mockProvider.Setup(provider => provider.GetCardDefinition(NormalFollowerDefinitionId))
            .Returns(new CardDefinition { Id = NormalFollowerDefinitionId, Type = CardType.Follower, Name = "Target", Attack = 2, Health = 2 });
        mockProvider.Setup(provider => provider.GetCardDefinition(LeaderDefinitionId))
            .Returns(new CardDefinition { Id = LeaderDefinitionId, Type = CardType.Leader, Name = "Leader", Attack = 0, Health = 20 });
        GameDataProvider.SetInstanceForTesting(mockProvider.Object);
    }

    private sealed class AttackFixture : IDisposable
    {
        public AttackFixture(
            GameContext context,
            GameState gameState,
            PlayerState attackingPlayer,
            CardInstance attacker,
            CardInstance wardFollower,
            CardInstance normalFollower,
            CardInstance defendingLeader)
        {
            Context = context;
            GameState = gameState;
            AttackingPlayer = attackingPlayer;
            Attacker = attacker;
            WardFollower = wardFollower;
            NormalFollower = normalFollower;
            DefendingLeader = defendingLeader;
        }

        public GameContext Context { get; }
        public GameState GameState { get; }
        public PlayerState AttackingPlayer { get; }
        public CardInstance Attacker { get; }
        public CardInstance WardFollower { get; }
        public CardInstance NormalFollower { get; }
        public CardInstance DefendingLeader { get; }

        public void Dispose()
        {
            Context.Dispose();
        }
    }
}
