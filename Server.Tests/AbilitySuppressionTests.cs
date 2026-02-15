using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class AbilitySuppressionTests
    {
        private readonly TargetSelectionService _targetSelectionService;
        private readonly Mock<IConditionFactory> _mockConditionFactory = new();
        private readonly Mock<ITargetFactory> _mockTargetFactory = new();
        private readonly Mock<IQueryExecutor> _mockQueryExecutor = new();
        private readonly Mock<IPendingActionService> _mockPendingActionService = new();

        public AbilitySuppressionTests()
        {
            _targetSelectionService = new TargetSelectionService(
                new NullLogger<TargetSelectionService>(),
                _mockQueryExecutor.Object,
                _mockPendingActionService.Object);
        }

        private Ability CreateAbility(
            CardInstance card,
            long definitionId,
            AbilityKeyword keyword,
            AbilityType type = AbilityType.Triggered,
            bool auraEnabled = false)
        {
            var config = new AbilityConfig
            {
                Id = definitionId,
                Name = $"Ability-{definitionId}",
                Type = type,
                AbilityKeywords = new List<AbilityKeywordConfig>
                {
                    new AbilityKeywordConfig { Keyword = keyword }
                }
            };

            if (auraEnabled)
            {
                config.Auras.Add(new AuraConfig { IsEnabled = true });
            }

            return new Ability(config, _mockConditionFactory.Object, _mockTargetFactory.Object, _targetSelectionService, card);
        }

        private static IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            return services.BuildServiceProvider();
        }

        private static GameContext BuildContext(IServiceProvider provider, GameState gameState, Guid requestingPlayerId)
        {
            var mapper = new Mock<IMapper>().Object;
            return new GameContext(provider, mapper, provider.GetRequiredService<IGameContextAccessor>(), gameState, requestingPlayerId);
        }

        [Fact]
        public void AbilitySuppressionEvaluator_Matches_ByDefinitionId()
        {
            var ownerId = Guid.NewGuid();
            var card = new CardInstance(Guid.NewGuid(), 1, ownerId);
            var ability = CreateAbility(card, 100, AbilityKeyword.Fanfare);

            var suppression = new AbilitySuppressionBuffEffect(RemoveAbilityMode.Id, new[] { 100L });

            Assert.True(AbilitySuppressionEvaluator.Matches(ability, suppression));
        }

        [Fact]
        public void AbilitySuppressionEvaluator_Matches_ByKeyword()
        {
            var ownerId = Guid.NewGuid();
            var card = new CardInstance(Guid.NewGuid(), 1, ownerId);
            var ability = CreateAbility(card, 200, AbilityKeyword.Lastword);

            var suppression = new AbilitySuppressionBuffEffect(RemoveAbilityMode.Keyword, abilityKeyword: AbilityKeyword.Lastword);

            Assert.True(AbilitySuppressionEvaluator.Matches(ability, suppression));
        }

        [Fact]
        public void AbilitySuppressionEvaluator_Matches_All()
        {
            var ownerId = Guid.NewGuid();
            var card = new CardInstance(Guid.NewGuid(), 1, ownerId);
            var ability = CreateAbility(card, 300, AbilityKeyword.Act);

            var suppression = new AbilitySuppressionBuffEffect(RemoveAbilityMode.All);

            Assert.True(AbilitySuppressionEvaluator.Matches(ability, suppression));
        }

        [Fact]
        public void BuffAppliedEvent_UnregistersAuraForSuppressedAbilities()
        {
            var serviceProvider = BuildServiceProvider();
            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

            var playerId = Guid.NewGuid();
            var player1 = new PlayerState(playerId);
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, playerId);

            var abilityManager = new Mock<IAbilityManager>();
            var buffManager = new Mock<IBuffManager>();
            var auraManager = new Mock<IAuraManager>();

            gameState.AbilityManager = abilityManager.Object;
            gameState.BuffManager = buffManager.Object;
            gameState.AuraManager = auraManager.Object;

            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var ability = CreateAbility(card, 101, AbilityKeyword.Fanfare);
            abilityManager.Setup(manager => manager.GetAbilitiesForCard(card.InstanceId))
                .Returns(new List<IAbility> { ability });

            IEnumerable<Guid>? removedIds = null;
            auraManager.Setup(manager => manager.UnregisterBySourceAbilities(It.IsAny<IEnumerable<Guid>>(), It.IsAny<GameContext>()))
                .Callback<IEnumerable<Guid>, GameContext>((ids, _) => removedIds = ids?.ToList())
                .Returns(new DomainOperationResult { Success = true });

            var suppression = new AbilitySuppressionBuffEffect(RemoveAbilityMode.Id, new[] { 101L });
            var buff = new Buff(Guid.Empty, card.InstanceId, 1, new List<IBuffEffect> { suppression });
            var evt = new BuffAppliedEvent(buff, null, null);

            using var context = BuildContext(serviceProvider, gameState, playerId);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            Assert.NotNull(removedIds);
            Assert.Contains(ability.RuntimeId, removedIds!);
        }

        [Fact]
        public void BuffRemovedEvent_RefreshesAuraWhenSuppressionEnds()
        {
            var serviceProvider = BuildServiceProvider();
            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

            var playerId = Guid.NewGuid();
            var player1 = new PlayerState(playerId);
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, playerId);

            var abilityManager = new Mock<IAbilityManager>();
            var buffManager = new Mock<IBuffManager>();
            var auraManager = new Mock<IAuraManager>();

            gameState.AbilityManager = abilityManager.Object;
            gameState.BuffManager = buffManager.Object;
            gameState.AuraManager = auraManager.Object;

            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var ability = CreateAbility(card, 202, AbilityKeyword.Lastword, AbilityType.Continuous, auraEnabled: true);
            abilityManager.Setup(manager => manager.GetAbilitiesForCard(card.InstanceId))
                .Returns(new List<IAbility> { ability });
            abilityManager.Setup(manager => manager.GetEffectiveAbilitiesForCard(card.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { ability });
            abilityManager.Setup(manager => manager.GetAbilityRuntimeIdsForBuff(It.IsAny<Guid>()))
                .Returns(new List<Guid>());
            abilityManager.Setup(manager => manager.RefreshAuraForAbility(It.IsAny<IAbility>(), It.IsAny<GameContext>()))
                .Returns(new DomainOperationResult { Success = true });

            var suppression = new AbilitySuppressionBuffEffect(RemoveAbilityMode.Id, new[] { 202L });
            var buff = new Buff(Guid.Empty, card.InstanceId, 1, new List<IBuffEffect> { suppression });
            var evt = new BuffRemovedEvent(buff, null, null);

            using var context = BuildContext(serviceProvider, gameState, playerId);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            abilityManager.Verify(manager => manager.RefreshAuraForAbility(ability, It.IsAny<GameContext>()), Times.Once);
        }
    }
}
