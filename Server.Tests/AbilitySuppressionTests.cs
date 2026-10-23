using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
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
            bool auraEnabled = false,
            Guid? sourceBuffId = null)
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

            return new Ability(config, _mockConditionFactory.Object, _mockTargetFactory.Object, _targetSelectionService, card, sourceBuffId);
        }

        private static IServiceProvider BuildServiceProvider(IBuffService? buffService = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton(buffService ?? new Mock<IBuffService>().Object);
            return services.BuildServiceProvider();
        }

        private static GameContext BuildContext(IServiceProvider provider, GameState gameState, Guid requestingPlayerId)
        {
            var mapper = new Mock<IMapper>().Object;
            return new GameContext(provider, mapper, provider.GetRequiredService<IGameContextAccessor>(), gameState, requestingPlayerId);
        }

        private static GameStateActionResult ExecuteSingleTargetAction(
            GameContext context,
            IReadOnlyList<TargetInfo> targets,
            Func<TargetInfo, GameContext, DomainOperationResult> action)
        {
            var target = Assert.Single(targets);
            var domainResult = action(target, context);
            return new GameStateActionResult
            {
                Success = domainResult.Success,
                ErrorMessage = domainResult.ErrorMessage,
                GeneratedEvents = domainResult.GeneratedEvents,
                Results = new List<DomainOperationResult> { domainResult }
            };
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
        public void AbilitySuppressionEvaluator_Matches_Self()
        {
            var ownerId = Guid.NewGuid();
            var card = new CardInstance(Guid.NewGuid(), 1, ownerId);
            var ability = CreateAbility(card, 301, AbilityKeyword.Act);

            var suppression = new AbilitySuppressionBuffEffect(
                RemoveAbilityMode.Self,
                sourceAbilityRuntimeId: ability.RuntimeId);

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
            abilityManager.SetupSequence(manager => manager.GetEffectiveAbilitiesForCard(card.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility> { ability })
                .Returns(new List<IAbility>());

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
            abilityManager.SetupSequence(manager => manager.GetEffectiveAbilitiesForCard(card.InstanceId, It.IsAny<GameContext>()))
                .Returns(new List<IAbility>())
                .Returns(new List<IAbility> { ability });
            abilityManager.Setup(manager => manager.GetAbilityRuntimeIdsForBuff(It.IsAny<Guid>()))
                .Returns(new List<Guid>());
            abilityManager.Setup(manager => manager.RefreshAuraForAbility(It.IsAny<IAbility>(), It.IsAny<GameContext>()))
                .Returns(new DomainOperationResult { Success = true });

            var suppression = new AbilitySuppressionBuffEffect(RemoveAbilityMode.Id, new[] { 202L });
            var buff = new Buff(Guid.Empty, card.InstanceId, 1, new List<IBuffEffect> { suppression });
            var evt = new BuffRemovedEvent(buff, card, null);

            using var context = BuildContext(serviceProvider, gameState, playerId);
            var result = evt.Commit(context);

            Assert.True(result.Success);
            abilityManager.Verify(manager => manager.RefreshAuraForAbility(ability, It.IsAny<GameContext>()), Times.Once);
        }

        [Fact]
        public void GetEffectiveAbilitiesForCard_SuppressionOnlyAffectsPreviouslyExistingAbilities()
        {
            var playerId = Guid.NewGuid();
            var player1 = new PlayerState(playerId);
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, playerId);

            var buffManager = new Mock<IBuffManager>();
            var abilityManager = new AbilityManager(
                NullLogger<AbilityManager>.Instance,
                new Mock<IAbilityFactory>().Object,
                Array.Empty<IAbility>());

            gameState.AbilityManager = abilityManager;
            gameState.BuffManager = buffManager.Object;

            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var suppressionBuff = new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: card.InstanceId,
                currentTurn: 1,
                effects: new List<IBuffEffect> { new AbilitySuppressionBuffEffect(RemoveAbilityMode.All) });
            var laterGrantBuff = new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: card.InstanceId,
                currentTurn: 1,
                effects: new List<IBuffEffect> { new AbilityBuffEffect(new List<AbilityConfig>()) });

            buffManager
                .Setup(manager => manager.GetBuffsForCard(card.InstanceId))
                .Returns(new List<Buff> { suppressionBuff, laterGrantBuff });

            var baseAbility = CreateAbility(card, 301, AbilityKeyword.Fanfare);
            var laterGrantedAbility = CreateAbility(card, 302, AbilityKeyword.Lastword, sourceBuffId: laterGrantBuff.Id);

            abilityManager.RegisterAbility(baseAbility);
            abilityManager.RegisterAbility(laterGrantedAbility);

            using var context = BuildContext(BuildServiceProvider(), gameState, playerId);
            var effective = abilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context);

            Assert.DoesNotContain(effective, ability => ability.RuntimeId == baseAbility.RuntimeId);
            Assert.Contains(effective, ability => ability.RuntimeId == laterGrantedAbility.RuntimeId);
        }

        [Fact]
        public void GetEffectiveAbilitiesForCard_SuppressionAffectsEarlierGrantedAbilities()
        {
            var playerId = Guid.NewGuid();
            var player1 = new PlayerState(playerId);
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, playerId);

            var buffManager = new Mock<IBuffManager>();
            var abilityManager = new AbilityManager(
                NullLogger<AbilityManager>.Instance,
                new Mock<IAbilityFactory>().Object,
                Array.Empty<IAbility>());

            gameState.AbilityManager = abilityManager;
            gameState.BuffManager = buffManager.Object;

            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var earlierGrantBuff = new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: card.InstanceId,
                currentTurn: 1,
                effects: new List<IBuffEffect> { new AbilityBuffEffect(new List<AbilityConfig>()) });
            var suppressionBuff = new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: card.InstanceId,
                currentTurn: 1,
                effects: new List<IBuffEffect> { new AbilitySuppressionBuffEffect(RemoveAbilityMode.All) });

            buffManager
                .Setup(manager => manager.GetBuffsForCard(card.InstanceId))
                .Returns(new List<Buff> { earlierGrantBuff, suppressionBuff });

            var baseAbility = CreateAbility(card, 401, AbilityKeyword.Fanfare);
            var earlierGrantedAbility = CreateAbility(card, 402, AbilityKeyword.Lastword, sourceBuffId: earlierGrantBuff.Id);

            abilityManager.RegisterAbility(baseAbility);
            abilityManager.RegisterAbility(earlierGrantedAbility);

            using var context = BuildContext(BuildServiceProvider(), gameState, playerId);
            var effective = abilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context);

            Assert.DoesNotContain(effective, ability => ability.RuntimeId == baseAbility.RuntimeId);
            Assert.DoesNotContain(effective, ability => ability.RuntimeId == earlierGrantedAbility.RuntimeId);
        }

        [Fact]
        public void GetEffectiveAbilitiesForCard_SuppressionDoesNotAffectAbilitiesFromSameBuff()
        {
            var playerId = Guid.NewGuid();
            var player1 = new PlayerState(playerId);
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, playerId);

            var buffManager = new Mock<IBuffManager>();
            var abilityManager = new AbilityManager(
                NullLogger<AbilityManager>.Instance,
                new Mock<IAbilityFactory>().Object,
                Array.Empty<IAbility>());

            gameState.AbilityManager = abilityManager;
            gameState.BuffManager = buffManager.Object;

            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var sameBuff = new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: card.InstanceId,
                currentTurn: 1,
                effects: new List<IBuffEffect>
                {
                    new AbilitySuppressionBuffEffect(RemoveAbilityMode.All),
                    new AbilityBuffEffect(new List<AbilityConfig>())
                });

            buffManager
                .Setup(manager => manager.GetBuffsForCard(card.InstanceId))
                .Returns(new List<Buff> { sameBuff });

            var grantedBySameBuff = CreateAbility(card, 501, AbilityKeyword.Act, sourceBuffId: sameBuff.Id);
            abilityManager.RegisterAbility(grantedBySameBuff);

            using var context = BuildContext(BuildServiceProvider(), gameState, playerId);
            var effective = abilityManager.GetEffectiveAbilitiesForCard(card.InstanceId, context);

            Assert.Contains(effective, ability => ability.RuntimeId == grantedBySameBuff.RuntimeId);
        }

        [Fact]
        public void RemoveAbilityEffectHandler_RemoveModeAll_AddsRemoveKeywordEffectForCurrentKeywords()
        {
            var playerId = Guid.NewGuid();
            var player1 = new PlayerState(playerId);
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, playerId);

            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.GetAbilitiesForCard(card.InstanceId))
                .Returns(new List<IAbility>());
            gameState.AbilityManager = abilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var capturedBuff = default(Buff);
            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.GetEffectiveKeywords(card, It.IsAny<GameContext>()))
                .Returns(new List<CardKeyword> { CardKeyword.Shield, CardKeyword.Ward, CardKeyword.None });
            buffService.Setup(service => service.ApplyBuffProcess(It.IsAny<Buff>(), It.IsAny<GameContext>(), true))
                .Callback<Buff, GameContext, bool>((buff, _, _) => capturedBuff = buff)
                .Returns(new DomainOperationResult { Success = true });

            var targetProcessingService = new Mock<ITargetProcessingService>();
            targetProcessingService.Setup(service => service.ProcessTargets(
                    It.IsAny<GameContext>(),
                    It.IsAny<IReadOnlyList<TargetInfo>>(),
                    It.IsAny<Func<TargetInfo, GameContext, DomainOperationResult>>(),
                    It.IsAny<string>()))
                .Returns((GameContext context,
                    IReadOnlyList<TargetInfo> targets,
                    Func<TargetInfo, GameContext, DomainOperationResult> action,
                    string _) => ExecuteSingleTargetAction(context, targets, action));

            var handler = new RemoveAbilityEffectHandler(
                NullLogger<RemoveAbilityEffectHandler>.Instance,
                targetProcessingService.Object,
                new BuffFactory(NullLogger<BuffFactory>.Instance),
                buffService.Object);

            var effect = new Effect(playerId)
            {
                SelectedTargets = new List<TargetInfo> { TargetInfo.ForCard(card) }
            };
            var parameters = new RemoveAbilityEffectParams
            {
                RemoveMode = RemoveAbilityMode.All
            };

            using var context = BuildContext(BuildServiceProvider(buffService.Object), gameState, playerId);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            Assert.NotNull(capturedBuff);
            Assert.Contains(capturedBuff!.Effects, effect => effect is AbilitySuppressionBuffEffect);

            var removeKeywordEffect = Assert.Single(capturedBuff.Effects.OfType<RemoveKeywordBuffEffect>());
            Assert.Equal(
                new[] { CardKeyword.Ward, CardKeyword.Shield },
                removeKeywordEffect.KeywordsToRemove.OrderBy(keyword => keyword).ToArray());
        }

        [Fact]
        public void RemoveAbilityEffectHandler_RemoveModeKeyword_DoesNotAddRemoveKeywordEffect()
        {
            var playerId = Guid.NewGuid();
            var player1 = new PlayerState(playerId);
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, playerId);

            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.GetAbilitiesForCard(card.InstanceId))
                .Returns(new List<IAbility>());
            gameState.AbilityManager = abilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var capturedBuff = default(Buff);
            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.ApplyBuffProcess(It.IsAny<Buff>(), It.IsAny<GameContext>(), true))
                .Callback<Buff, GameContext, bool>((buff, _, _) => capturedBuff = buff)
                .Returns(new DomainOperationResult { Success = true });

            var targetProcessingService = new Mock<ITargetProcessingService>();
            targetProcessingService.Setup(service => service.ProcessTargets(
                    It.IsAny<GameContext>(),
                    It.IsAny<IReadOnlyList<TargetInfo>>(),
                    It.IsAny<Func<TargetInfo, GameContext, DomainOperationResult>>(),
                    It.IsAny<string>()))
                .Returns((GameContext context,
                    IReadOnlyList<TargetInfo> targets,
                    Func<TargetInfo, GameContext, DomainOperationResult> action,
                    string _) => ExecuteSingleTargetAction(context, targets, action));

            var handler = new RemoveAbilityEffectHandler(
                NullLogger<RemoveAbilityEffectHandler>.Instance,
                targetProcessingService.Object,
                new BuffFactory(NullLogger<BuffFactory>.Instance),
                buffService.Object);

            var effect = new Effect(playerId)
            {
                SelectedTargets = new List<TargetInfo> { TargetInfo.ForCard(card) }
            };
            var parameters = new RemoveAbilityEffectParams
            {
                RemoveMode = RemoveAbilityMode.Keyword,
                AbilityKeyword = AbilityKeyword.Lastword
            };

            using var context = BuildContext(BuildServiceProvider(buffService.Object), gameState, playerId);
            var result = handler.Apply(effect, context, parameters);

            Assert.True(result.Success);
            Assert.NotNull(capturedBuff);
            Assert.Empty(capturedBuff!.Effects.OfType<RemoveKeywordBuffEffect>());
        }

        [Fact]
        public void RemoveAbilityEffectHandler_RemoveModeSelf_CapturesCurrentAbilityRuntimeId()
        {
            var playerId = Guid.NewGuid();
            var player1 = new PlayerState(playerId);
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, playerId);

            var card = new CardInstance(Guid.NewGuid(), 1, playerId);
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            var sourceAbility = CreateAbility(card, 601, AbilityKeyword.Act);
            var otherAbility = CreateAbility(card, 602, AbilityKeyword.Lastword);

            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.GetAbilitiesForCard(card.InstanceId))
                .Returns(new List<IAbility> { sourceAbility, otherAbility });
            gameState.AbilityManager = abilityManager.Object;
            gameState.BuffManager = new Mock<IBuffManager>().Object;

            var capturedBuff = default(Buff);
            var buffService = new Mock<IBuffService>();
            buffService.Setup(service => service.ApplyBuffProcess(It.IsAny<Buff>(), It.IsAny<GameContext>(), true))
                .Callback<Buff, GameContext, bool>((buff, _, _) => capturedBuff = buff)
                .Returns(new DomainOperationResult { Success = true });

            var targetProcessingService = new Mock<ITargetProcessingService>();
            targetProcessingService.Setup(service => service.ProcessTargets(
                    It.IsAny<GameContext>(),
                    It.IsAny<IReadOnlyList<TargetInfo>>(),
                    It.IsAny<Func<TargetInfo, GameContext, DomainOperationResult>>(),
                    It.IsAny<string>()))
                .Returns((GameContext context,
                    IReadOnlyList<TargetInfo> targets,
                    Func<TargetInfo, GameContext, DomainOperationResult> action,
                    string _) => ExecuteSingleTargetAction(context, targets, action));

            var handler = new RemoveAbilityEffectHandler(
                NullLogger<RemoveAbilityEffectHandler>.Instance,
                targetProcessingService.Object,
                new BuffFactory(NullLogger<BuffFactory>.Instance),
                buffService.Object);

            var effect = new Effect(playerId)
            {
                SelectedTargets = new List<TargetInfo> { TargetInfo.ForCard(card) },
                SourceAbility = sourceAbility,
                sourceAbilityId = sourceAbility.RuntimeId
            };
            var parameters = new RemoveAbilityEffectParams
            {
                RemoveMode = RemoveAbilityMode.Self
            };

            using var context = BuildContext(BuildServiceProvider(buffService.Object), gameState, playerId);
            var result = handler.Apply(effect, context with { CurrentEffect = effect, SourceAbility = sourceAbility }, parameters);

            Assert.True(result.Success);
            Assert.NotNull(capturedBuff);

            var suppressionEffect = Assert.Single(capturedBuff!.Effects.OfType<AbilitySuppressionBuffEffect>());
            Assert.Equal(RemoveAbilityMode.Self, suppressionEffect.RemoveMode);
            Assert.Equal(sourceAbility.RuntimeId, suppressionEffect.SourceAbilityRuntimeId);
            Assert.True(AbilitySuppressionEvaluator.Matches(sourceAbility, suppressionEffect));
            Assert.False(AbilitySuppressionEvaluator.Matches(otherAbility, suppressionEffect));
        }
    }
}
