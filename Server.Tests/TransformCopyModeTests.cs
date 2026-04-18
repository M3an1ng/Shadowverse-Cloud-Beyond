using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class TransformCopyModeTests
    {
        private static ServiceProvider BuildServiceProvider(
            Mock<IBuffService> buffService,
            Mock<IBuffFactory>? buffFactory = null,
            Mock<IValueSourceResolver>? valueSourceResolver = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IGameDataProvider>(GameDataProvider.Instance);
            services.AddSingleton(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            services.AddSingleton((valueSourceResolver ?? new Mock<IValueSourceResolver>()).Object);
            if (buffFactory != null)
            {
                services.AddSingleton(buffFactory.Object);
            }

            return services.BuildServiceProvider();
        }

        private static GameState BuildGameState(Guid player1Id, Guid player2Id, IAbilityManager abilityManager, IBuffManager buffManager)
        {
            var player1 = new PlayerState(player1Id);
            var player2 = new PlayerState(player2Id);
            return new GameState("test-game", player1, player2, player1Id)
            {
                AbilityManager = abilityManager,
                BuffManager = buffManager
            };
        }

        private static void SetTestGameDataProvider(params CardDefinition[] definitions)
        {
            var definitionMap = definitions.ToDictionary(definition => definition.Id);
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(It.IsAny<int>()))
                .Returns<int>(definitionId => definitionMap.TryGetValue(definitionId, out var definition) ? definition : null);
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>()))
                .Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        private static GameContext BuildContext(
            GameState gameState,
            Guid requestingPlayerId,
            IServiceProvider serviceProvider,
            CardInstance sourceCard,
            Effect effect)
        {
            var mapper = new Mock<IMapper>().Object;
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();
            return new GameContext(
                serviceProvider,
                mapper,
                accessor,
                gameState,
                requestingPlayerId,
                sourceCard: sourceCard,
                currentEffect: effect);
        }

        private static Mock<ITargetProcessingService> BuildPassThroughTargetProcessor()
        {
            var targetProcessor = new Mock<ITargetProcessingService>();
            targetProcessor
                .Setup(service => service.ProcessTargets(
                    It.IsAny<GameContext>(),
                    It.IsAny<IReadOnlyList<TargetInfo>>(),
                    It.IsAny<Func<TargetInfo, GameContext, DomainOperationResult>>(),
                    It.IsAny<string>()))
                .Returns((GameContext context, IReadOnlyList<TargetInfo> targets, Func<TargetInfo, GameContext, DomainOperationResult> action, string _) =>
                {
                    var result = new GameStateActionResult { Success = true };
                    foreach (var target in targets)
                    {
                        var operation = action(target, context);
                        if (!operation.Success)
                        {
                            return new GameStateActionResult
                            {
                                Success = false,
                                ErrorMessage = operation.ErrorMessage
                            };
                        }

                        result.GeneratedEvents.AddRange(operation.GeneratedEvents);
                    }

                    return result;
                });

            return targetProcessor;
        }

        private static Mock<IBuffService> BuildBuffService()
        {
            var buffService = new Mock<IBuffService>();
            buffService
                .Setup(service => service.RemoveAllBuffs(
                    It.IsAny<Guid>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Func<Buff, bool>?>()))
                .Returns(new DomainOperationResult { Success = true });
            buffService
                .Setup(service => service.InvalidateDerivedState(It.IsAny<GameState>(), It.IsAny<Guid>()));
            return buffService;
        }

        [Fact]
        public void Transform_CopyMode_TransformsTargetIntoSourceCopy()
        {
            const int sourceDefinitionId = 1;
            const int targetDefinitionId = 2;
            SetTestGameDataProvider(
                new CardDefinition { Id = sourceDefinitionId, Name = "Source", Type = CardType.Follower, Attack = 3, Health = 4 },
                new CardDefinition { Id = targetDefinitionId, Name = "Target", Type = CardType.Follower, Attack = 1, Health = 2 });

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();

            var abilityManager = new Mock<IAbilityManager>();
            abilityManager
                .Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<GameState>()))
                .Returns(new List<Ability>());

            var sourceBuff = new Buff(Guid.Empty, Guid.NewGuid(), 1, new List<IBuffEffect>());
            var buffManager = new Mock<IBuffManager>();
            buffManager.Setup(manager => manager.GetBuffsForCard(It.IsAny<Guid>()))
                .Returns<Guid>(cardId => cardId == Guid.Empty ? new List<Buff>() : new List<Buff>());
            var buffFactory = new Mock<IBuffFactory>();
            buffFactory
                .Setup(factory => factory.CreateBuffCopy(sourceBuff, It.IsAny<CardInstance>()))
                .Returns((Buff _, CardInstance? newTarget) => new Buff(Guid.Empty, newTarget!.InstanceId, 1, new List<IBuffEffect>()));

            var gameState = BuildGameState(player1Id, player2Id, abilityManager.Object, buffManager.Object);
            var source = new CardInstance(Guid.NewGuid(), sourceDefinitionId, player1Id)
            {
                CurrentZone = CardZone.Field,
                Damage = 2,
                IsEvolved = true,
                ZoneStayTurn = 3
            };
            source.AbilityState["copied"] = 7;
            source.ApplySilence();

            var target = new CardInstance(Guid.NewGuid(), targetDefinitionId, player2Id)
            {
                CurrentZone = CardZone.Field,
                Damage = 0,
                ZoneStayTurn = 1
            };

            gameState.RegisterCardInstance(source);
            gameState.RegisterCardInstance(target);
            gameState.GetPlayerState(player1Id)!.Field.Add(source);
            gameState.GetPlayerState(player2Id)!.Field.Add(target);

            buffManager.Setup(manager => manager.GetBuffsForCard(source.InstanceId))
                .Returns(new List<Buff> { sourceBuff });

            var buffService = BuildBuffService();
            using var serviceProvider = BuildServiceProvider(buffService, buffFactory);

            var effectConfig = new EffectConfig { Type = EffectType.Transform };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo> { TargetInfo.ForCard(target) }, null, Guid.NewGuid(), source.InstanceId);
            using var context = BuildContext(gameState, player1Id, serviceProvider, source, effect);

            var handler = new TransformEffectHandler(
                NullLogger<TransformEffectHandler>.Instance,
                BuildPassThroughTargetProcessor().Object);

            var applyResult = handler.Apply(effect, context, new TransformEffectParams
            {
                CardSourceMode = CardSourceMode.Copy,
                CopySourceTargets = new StaticValueConfig
                {
                    Value = new List<Guid> { source.InstanceId }
                }
            });

            Assert.True(applyResult.Success);
            var transformEvent = Assert.Single(applyResult.GeneratedEvents.OfType<TransformEvent>());

            var commitResult = transformEvent.Commit(context);

            Assert.True(commitResult.Success);
            Assert.Equal(sourceDefinitionId, target.CurrentDefinitionId);
            Assert.Equal(player2Id, target.OwnerPlayerId);
            Assert.Equal(CardZone.Field, target.CurrentZone);
            Assert.Equal(source.Damage, target.Damage);
            Assert.Equal(source.IsEvolved, target.IsEvolved);
            Assert.Equal(source.IsSilenced, target.IsSilenced);
            Assert.Equal(source.ZoneStayTurn, target.ZoneStayTurn);
            Assert.Equal(7, target.AbilityState["copied"]);
            buffFactory.Verify(factory => factory.CreateBuffCopy(sourceBuff, target), Times.Once);
            buffManager.Verify(manager => manager.RegisterBuffInstance(
                It.Is<Buff>(buff => buff.TargetCardInstanceId == target.InstanceId),
                gameState), Times.Once);
        }

        [Fact]
        public void Transform_CopyMode_PreservesPreTransformStateWhenTargetIsSource()
        {
            const int sourceDefinitionId = 1;
            SetTestGameDataProvider(
                new CardDefinition { Id = sourceDefinitionId, Name = "Source", Type = CardType.Follower, Attack = 3, Health = 4 });

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();

            var abilityManager = new Mock<IAbilityManager>();
            abilityManager
                .Setup(manager => manager.CreateAbilitiesForCard(
                    It.IsAny<CardInstance>(),
                    It.IsAny<List<AbilityConfig>>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<GameState>()))
                .Returns(new List<Ability>());

            var buffManager = new Mock<IBuffManager>();
            buffManager.Setup(manager => manager.GetBuffsForCard(It.IsAny<Guid>()))
                .Returns(new List<Buff>());

            var gameState = BuildGameState(player1Id, player2Id, abilityManager.Object, buffManager.Object);
            var source = new CardInstance(Guid.NewGuid(), sourceDefinitionId, player1Id)
            {
                CurrentZone = CardZone.Field,
                Damage = 3,
                IsEvolved = true,
                ZoneStayTurn = 4
            };
            source.AbilityState["self-copy"] = 9;
            source.ApplySilence();

            gameState.RegisterCardInstance(source);
            gameState.GetPlayerState(player1Id)!.Field.Add(source);

            var buffService = BuildBuffService();
            using var serviceProvider = BuildServiceProvider(buffService);

            var effectConfig = new EffectConfig { Type = EffectType.Transform };
            var effect = new Effect(effectConfig, player1Id, new List<TargetInfo> { TargetInfo.ForCard(source) }, null, Guid.NewGuid(), source.InstanceId);
            using var context = BuildContext(gameState, player1Id, serviceProvider, source, effect);

            var handler = new TransformEffectHandler(
                NullLogger<TransformEffectHandler>.Instance,
                BuildPassThroughTargetProcessor().Object);

            var applyResult = handler.Apply(effect, context, new TransformEffectParams
            {
                CardSourceMode = CardSourceMode.Copy,
                CopySourceTargets = new StaticValueConfig
                {
                    Value = source.InstanceId
                }
            });

            Assert.True(applyResult.Success);
            var transformEvent = Assert.Single(applyResult.GeneratedEvents.OfType<TransformEvent>());

            var commitResult = transformEvent.Commit(context);

            Assert.True(commitResult.Success);
            Assert.Equal(sourceDefinitionId, source.CurrentDefinitionId);
            Assert.Equal(3, source.Damage);
            Assert.True(source.IsEvolved);
            Assert.True(source.IsSilenced);
            Assert.Equal(4, source.ZoneStayTurn);
            Assert.Equal(9, source.AbilityState["self-copy"]);
        }
    }
}
