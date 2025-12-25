using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class BuffServiceCacheTests
    {
        [Fact]
        public void DerivedStateCache_ReusesWithinRefreshCycle()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var buff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Attack, 2, ModifierLayer.Additive) },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(buff, gameState);

            using (context)
            {
                var firstAttack = context.BuffService.GetEffectiveAttack(card, context);
                var secondAttack = context.BuffService.GetEffectiveAttack(card, context);

                Assert.Equal(2, firstAttack);
                Assert.Equal(2, secondAttack);
                Assert.Equal(1, evaluator.CallCount);
            }
        }

        [Fact]
        public void DerivedStateCache_RefreshesAfterApplicabilityChange()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(false);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var buff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Attack, 2, ModifierLayer.Additive) },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(buff, gameState);

            using (context)
            {
                var initialAttack = context.BuffService.GetEffectiveAttack(card, context);
                Assert.Equal(0, initialAttack);
                Assert.Equal(1, evaluator.CallCount);

                evaluator.Result = true;
                var cachedAttack = context.BuffService.GetEffectiveAttack(card, context);
                Assert.Equal(0, cachedAttack);
                Assert.Equal(1, evaluator.CallCount);

                context.BuffService.RefreshDerivedStateCache(context, "test_refresh");
                var refreshedAttack = context.BuffService.GetEffectiveAttack(card, context);
                Assert.Equal(2, refreshedAttack);
                Assert.Equal(2, evaluator.CallCount);
            }
        }

        [Fact]
        public void BuffExpiryCollections_RespectTurnTimingAndOwner()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, player1Id, card1) = CreateGameState(buffManager);
            var player2Id = gameState.Player2.PlayerId;
            var card2 = new CardInstance(Guid.NewGuid(), 1002, player2Id) { CurrentZone = CardZone.Field };
            gameState.Player2.Field.Add(card2);
            gameState.RegisterCardInstance(card2);

            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var endOfTurnBuff = buffFactory.CreateSystemStatBuff(
                target: card1,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.EndOfTurn,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(endOfTurnBuff, gameState);

            var startOfNextTurnBuff = buffFactory.CreateSystemStatBuff(
                target: card1,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.StartOfNextTurn,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(startOfNextTurnBuff, gameState);

            var opponentStartBuff = buffFactory.CreateSystemStatBuff(
                target: card2,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.StartOfNextTurn,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(opponentStartBuff, gameState);

            using (context)
            {
                var endOfTurnEvents = context.BuffService.CollectEndOfTurnExpiryEvents(context);
                Assert.Single(endOfTurnEvents);
                Assert.Equal(endOfTurnBuff.Id, endOfTurnEvents[0].RemovedBuff.Id);
                Assert.Equal("duration_end_of_turn", endOfTurnEvents[0].RemovalReason);

                var startOfNextTurnEvents = context.BuffService.CollectStartOfNextTurnExpiryEvents(context, gameState.Player1);
                Assert.Single(startOfNextTurnEvents);
                Assert.Equal(startOfNextTurnBuff.Id, startOfNextTurnEvents[0].RemovedBuff.Id);
                Assert.Equal("duration_start_of_next_turn", startOfNextTurnEvents[0].RemovalReason);
            }
        }

        private static (GameState GameState, Guid Player1Id, CardInstance Card) CreateGameState(TestBuffManager buffManager)
        {
            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id) { IsFirst = true };
            var player2 = new PlayerState(player2Id);

            var gameState = new GameState("buff-cache-test", player1, player2, player1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = buffManager
            };

            var card = new CardInstance(Guid.NewGuid(), 1001, player1Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            return (gameState, player1Id, card);
        }

        private static (GameContext Context, BuffFactory BuffFactory) CreateContext(
            GameState gameState,
            TestBuffManager buffManager,
            IBuffEvaluator evaluator)
        {
            var buffFactory = new BuffFactory(NullLogger<BuffFactory>.Instance);
            var stackingEngine = new BuffStackingEngine(NullLogger<BuffStackingEngine>.Instance);
            var buffService = new BuffService(
                buffManager,
                evaluator,
                buffFactory,
                NullLogger<BuffService>.Instance,
                stackingEngine);

            var services = new ServiceCollection();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(buffService);
            var serviceProvider = services.BuildServiceProvider();

            var context = new GameContext(
                serviceProvider,
                Mock.Of<IMapper>(),
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                gameState.Player1.PlayerId);

            return (context, buffFactory);
        }

        private sealed class CountingBuffEvaluator : IBuffEvaluator
        {
            public int CallCount { get; private set; }
            public bool Result { get; set; }

            public CountingBuffEvaluator(bool result)
            {
                Result = result;
            }

            public bool IsApplicable(Buff buff, CardInstance card, GameContext gameContext)
            {
                CallCount++;
                return Result;
            }
        }

        private sealed class TestBuffManager : IBuffManager
        {
            private readonly Dictionary<Guid, List<Buff>> _buffsByCard = new();
            private readonly Dictionary<Guid, Buff> _buffsById = new();

            public List<Buff> GetBuffsForCard(Guid cardInstanceId)
            {
                return _buffsByCard.TryGetValue(cardInstanceId, out var buffs)
                    ? buffs
                    : new List<Buff>();
            }

            public Buff? FindBuffInstance(Guid buffId)
            {
                return _buffsById.TryGetValue(buffId, out var buff) ? buff : null;
            }

            public void RegisterBuffInstance(Buff buff, GameState gameState)
            {
                if (!_buffsByCard.TryGetValue(buff.TargetCardInstanceId, out var buffs))
                {
                    buffs = new List<Buff>();
                    _buffsByCard[buff.TargetCardInstanceId] = buffs;
                }

                buffs.Add(buff);
                _buffsById[buff.Id] = buff;
            }

            public Buff? UnregisterBuffInstance(Guid buffId)
            {
                if (!_buffsById.TryGetValue(buffId, out var buff))
                {
                    return null;
                }

                _buffsById.Remove(buffId);
                if (_buffsByCard.TryGetValue(buff.TargetCardInstanceId, out var buffs))
                {
                    buffs.RemoveAll(item => item.Id == buffId);
                    if (buffs.Count == 0)
                    {
                        _buffsByCard.Remove(buff.TargetCardInstanceId);
                    }
                }

                return buff;
            }
        }
    }
}
