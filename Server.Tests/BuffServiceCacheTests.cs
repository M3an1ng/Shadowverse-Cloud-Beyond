using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
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
                var baseAttack = card.BaseAttack;
                var firstAttack = context.BuffService.GetEffectiveAttack(card, context);
                var secondAttack = context.BuffService.GetEffectiveAttack(card, context);

                Assert.Equal(baseAttack + 2, firstAttack);
                Assert.Equal(baseAttack + 2, secondAttack);
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
                Assert.Equal(card.BaseAttack, initialAttack);
                Assert.Equal(1, evaluator.CallCount);

                evaluator.Result = true;
                var cachedAttack = context.BuffService.GetEffectiveAttack(card, context);
                Assert.Equal(card.BaseAttack, cachedAttack);
                Assert.Equal(1, evaluator.CallCount);

                context.BuffService.RefreshDerivedStateCache(context, "test_refresh");
                var refreshedAttack = context.BuffService.GetEffectiveAttack(card, context);
                Assert.Equal(card.BaseAttack + 2, refreshedAttack);
                Assert.Equal(2, evaluator.CallCount);
            }
        }

        [Fact]
        public void BuffExpiryCollections_RespectTurnTimingAndGlobalStart()
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

            var startOfTurnBuff = buffFactory.CreateSystemStatBuff(
                target: card1,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.StartOfTurn,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(startOfTurnBuff, gameState);

            var opponentStartBuff = buffFactory.CreateSystemStatBuff(
                target: card2,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.StartOfTurn,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(opponentStartBuff, gameState);

            using (context)
            {
                var endOfTurnEvents = context.BuffService.CollectEndOfTurnExpiryEvents(context);
                Assert.Single(endOfTurnEvents);
                Assert.Equal(endOfTurnBuff.Id, endOfTurnEvents[0].RemovedBuff.Id);
                Assert.Equal("duration_end_of_turn", endOfTurnEvents[0].RemovalReason);

                var startOfTurnEvents = context.BuffService.CollectStartOfTurnExpiryEvents(context, gameState.Player1);
                Assert.Equal(2, startOfTurnEvents.Count);
                Assert.Contains(startOfTurnEvents, e => e.RemovedBuff.Id == startOfTurnBuff.Id);
                Assert.Contains(startOfTurnEvents, e => e.RemovedBuff.Id == opponentStartBuff.Id);
                Assert.All(startOfTurnEvents, e => Assert.Equal("duration_start_of_turn", e.RemovalReason));
            }
        }

        [Fact]
        public void BuffExpiryCollections_CountDownDurationParameter()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var endOfTurnBuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.EndOfTurn,
                durationParameter: 2,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(endOfTurnBuff, gameState);

            var startOfTurnBuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.StartOfTurn,
                durationParameter: 2,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(startOfTurnBuff, gameState);

            using (context)
            {
                var firstEndEvents = context.BuffService.CollectEndOfTurnExpiryEvents(context);
                Assert.Empty(firstEndEvents);
                Assert.Equal(1, endOfTurnBuff.DurationParameter);

                var firstStartEvents = context.BuffService.CollectStartOfTurnExpiryEvents(context, gameState.Player1);
                Assert.Empty(firstStartEvents);
                Assert.Equal(1, startOfTurnBuff.DurationParameter);

                var secondEndEvents = context.BuffService.CollectEndOfTurnExpiryEvents(context);
                Assert.Single(secondEndEvents);
                Assert.Equal(endOfTurnBuff.Id, secondEndEvents[0].RemovedBuff.Id);

                var secondStartEvents = context.BuffService.CollectStartOfTurnExpiryEvents(context, gameState.Player1);
                Assert.Single(secondStartEvents);
                Assert.Equal(startOfTurnBuff.Id, secondStartEvents[0].RemovedBuff.Id);
            }
        }

        [Fact]
        public void BuffExpiryCollections_RemoveConditionalBuffWhenRemoveConditionMatches()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);
            var conditionEvaluator = new TestConditionEvaluator();

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, _) = CreateContext(gameState, buffManager, evaluator, conditionEvaluator);

            var removeCondition = ConditionConfig.FromExpression("test.remove");
            var conditionalBuff = new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: card.InstanceId,
                currentTurn: 1,
                effects: new List<IBuffEffect>
                {
                    new StatBuffEffect(new List<StatModification>
                    {
                        new(StatType.Attack, 1, ModifierLayer.Additive)
                    })
                },
                durationType: DurationType.ConditionalRemoval,
                durationParameter: 99,
                removalCondition: removeCondition);
            buffManager.RegisterBuffInstance(conditionalBuff, gameState);

            using (context)
            {
                conditionEvaluator.Result = false;
                var firstEndEvents = context.BuffService.CollectEndOfTurnExpiryEvents(context);
                Assert.Empty(firstEndEvents);
                Assert.Null(conditionalBuff.DurationParameter);
                Assert.Equal(removeCondition, conditionEvaluator.LastConfig);

                conditionEvaluator.Result = true;
                var secondEndEvents = context.BuffService.CollectEndOfTurnExpiryEvents(context);
                var removedEvent = Assert.Single(secondEndEvents);
                Assert.Equal(conditionalBuff.Id, removedEvent.RemovedBuff.Id);
                Assert.Equal("duration_conditional_removal", removedEvent.RemovalReason);
            }
        }

        [Fact]
        public void BuffExpiryCollections_RespectOwnerOpponentTurnTypes()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card1) = CreateGameState(buffManager);
            var player2Id = gameState.Player2.PlayerId;
            var card2 = new CardInstance(Guid.NewGuid(), 1002, player2Id) { CurrentZone = CardZone.Field };
            gameState.Player2.Field.Add(card2);
            gameState.RegisterCardInstance(card2);

            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var ownerStartBuff = buffFactory.CreateSystemStatBuff(
                target: card1,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.OwnerTurnStart,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(ownerStartBuff, gameState);

            var opponentStartBuff = buffFactory.CreateSystemStatBuff(
                target: card1,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.OpponentTurnStart,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(opponentStartBuff, gameState);

            var ownerEndBuff = buffFactory.CreateSystemStatBuff(
                target: card2,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.OwnerTurnEnd,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(ownerEndBuff, gameState);

            var opponentEndBuff = buffFactory.CreateSystemStatBuff(
                target: card2,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.OpponentTurnEnd,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(opponentEndBuff, gameState);

            using (context)
            {
                var startEventsPlayer1 = context.BuffService.CollectStartOfTurnExpiryEvents(context, gameState.Player1);
                Assert.Single(startEventsPlayer1);
                Assert.Equal(ownerStartBuff.Id, startEventsPlayer1[0].RemovedBuff.Id);

                var startEventsPlayer2 = context.BuffService.CollectStartOfTurnExpiryEvents(context, gameState.Player2);
                Assert.Single(startEventsPlayer2);
                Assert.Equal(opponentStartBuff.Id, startEventsPlayer2[0].RemovedBuff.Id);

                gameState.CurrentPlayerId = gameState.Player1.PlayerId;
                var endEventsPlayer1 = context.BuffService.CollectEndOfTurnExpiryEvents(context);
                Assert.Single(endEventsPlayer1);
                Assert.Equal(opponentEndBuff.Id, endEventsPlayer1[0].RemovedBuff.Id);

                gameState.CurrentPlayerId = gameState.Player2.PlayerId;
                var endEventsPlayer2 = context.BuffService.CollectEndOfTurnExpiryEvents(context);
                Assert.Single(endEventsPlayer2);
                Assert.Equal(ownerEndBuff.Id, endEventsPlayer2[0].RemovedBuff.Id);
            }
        }

        [Fact]
        public void BoundaryCleanup_EndOfTurn_CollectsBuffExpiryEvents()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var buff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.EndOfTurn,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(buff, gameState);

            using (context)
            {
                var cleanup = new TurnBoundaryCleanupService(context.BuffService);

                var events = cleanup.CollectCleanupEvents(context, TurnBoundaryCleanupTiming.TurnEnd, gameState.Player1);

                var removedEvent = Assert.Single(events.OfType<BuffRemovedEvent>());
                Assert.Equal(buff.Id, removedEvent.RemovedBuff.Id);
                Assert.Equal("duration_end_of_turn", removedEvent.RemovalReason);
            }
        }

        [Fact]
        public void BoundaryCleanup_StartOfTurn_CollectsBuffExpiryEvents()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var buff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Attack, 1, ModifierLayer.Additive) },
                durationType: DurationType.StartOfTurn,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(buff, gameState);

            using (context)
            {
                var cleanup = new TurnBoundaryCleanupService(context.BuffService);

                var events = cleanup.CollectCleanupEvents(context, TurnBoundaryCleanupTiming.TurnStart, gameState.Player1);

                var removedEvent = Assert.Single(events.OfType<BuffRemovedEvent>());
                Assert.Equal(buff.Id, removedEvent.RemovedBuff.Id);
                Assert.Equal("duration_start_of_turn", removedEvent.RemovalReason);
            }
        }

        [Fact]
        public void BuffRemovedEvent_ClampsCurrentHealthAfterHealthBuffRemoval()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var healthBuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Health, 2, ModifierLayer.Additive) },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(healthBuff, gameState);

            using (context)
            {
                var oldMaxHealth = context.BuffService.GetEffectiveMaxHealth(card, context);
                card.Damage = 1;
                var oldCurrent = Math.Max(0, oldMaxHealth - card.Damage);

                var removeEvent = new BuffRemovedEvent(healthBuff, card, null, "test_remove");
                var result = removeEvent.Commit(context);
                Assert.True(result.Success);

                var newMaxHealth = context.BuffService.GetEffectiveMaxHealth(card, context);
                var newCurrent = Math.Max(0, newMaxHealth - card.Damage);

                Assert.Equal(card.BaseHealth, newMaxHealth);
                Assert.Equal(Math.Min(oldCurrent, newMaxHealth), newCurrent);
            }
        }

        [Fact]
        public void BuffRemovedEvent_PreservesFollowerAtOneHealthWhenRemainingHealthBuffsLeaveRawMaxHealthAtZero()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var lethalDebuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Health, -card.BaseHealth, ModifierLayer.Additive) },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            var expiringHealthBuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Health, 1, ModifierLayer.Additive) },
                durationType: DurationType.EndOfTurn,
                durationParameter: null,
                currentTurn: 1);

            buffManager.RegisterBuffInstance(lethalDebuff, gameState);
            buffManager.RegisterBuffInstance(expiringHealthBuff, gameState);

            using (context)
            {
                Assert.Equal(1, context.BuffService.GetEffectiveMaxHealth(card, context));

                var removeEvent = new BuffRemovedEvent(expiringHealthBuff, card, null, "duration_end_of_turn");
                var result = removeEvent.Commit(context);

                Assert.True(result.Success);
                Assert.Empty(result.GeneratedEvents.OfType<LeaveFieldEvent>());
                Assert.Equal(1, context.BuffService.GetEffectiveMaxHealth(card, context));
                Assert.Equal(1, card.CurrentHealth);
                Assert.Equal(0, card.Damage);
                Assert.Equal(1, card.HealthFloorFromBuffRemoval);
            }
        }

        [Fact]
        public void BuffRemovedEvent_PreservesFollowerAtOneHealthWhenRemainingHealthBuffsLeaveRawMaxHealthBelowZero()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var lethalDebuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Health, -(card.BaseHealth + 1), ModifierLayer.Additive) },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            var expiringHealthBuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Health, 2, ModifierLayer.Additive) },
                durationType: DurationType.EndOfTurn,
                durationParameter: null,
                currentTurn: 1);

            buffManager.RegisterBuffInstance(lethalDebuff, gameState);
            buffManager.RegisterBuffInstance(expiringHealthBuff, gameState);

            using (context)
            {
                Assert.Equal(1, context.BuffService.GetEffectiveMaxHealth(card, context));

                var removeEvent = new BuffRemovedEvent(expiringHealthBuff, card, null, "duration_end_of_turn");
                var result = removeEvent.Commit(context);

                Assert.True(result.Success);
                Assert.Empty(result.GeneratedEvents.OfType<LeaveFieldEvent>());
                Assert.Equal(1, context.BuffService.GetEffectiveMaxHealth(card, context));
                Assert.Equal(1, card.CurrentHealth);
                Assert.Equal(0, card.Damage);
                Assert.Equal(1, card.HealthFloorFromBuffRemoval);
            }
        }

        [Fact]
        public void StatChangedEvent_HealthReductionStillDestroysFollower()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, _) = CreateContext(gameState, buffManager, evaluator);

            using (context)
            {
                var statChanged = new StatChangedEvent(
                    targetCard: card,
                    attackModifier: null,
                    healthModifier: -card.BaseHealth,
                    costModifier: null,
                    modifierLayer: ModifierLayer.Additive);

                var result = statChanged.Commit(context);

                Assert.True(result.Success);
                var leaveField = Assert.Single(result.GeneratedEvents.OfType<LeaveFieldEvent>());
                Assert.Equal(LeaveFieldKind.Destroy, leaveField.RequestedKind);
                Assert.Equal(EventReasonSource.System, leaveField.ReasonSource);
                Assert.Equal(0, card.HealthFloorFromBuffRemoval);
            }
        }

        [Fact]
        public void StatChangedEvent_HealthReductionDestroysFollowerWithExistingBuffRemovalFloor()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            using (context)
            {
                ApplyBuffRemovalHealthFloor(context, buffManager, buffFactory, gameState, card);
                Assert.Equal(1, card.HealthFloorFromBuffRemoval);

                var statChanged = new StatChangedEvent(
                    targetCard: card,
                    attackModifier: null,
                    healthModifier: -1,
                    costModifier: null,
                    modifierLayer: ModifierLayer.Additive);

                var result = statChanged.Commit(context);

                Assert.True(result.Success);
                var leaveField = Assert.Single(result.GeneratedEvents.OfType<LeaveFieldEvent>());
                Assert.Equal(LeaveFieldKind.Destroy, leaveField.RequestedKind);
                Assert.Equal(EventReasonSource.System, leaveField.ReasonSource);
            }
        }

        [Fact]
        public void DamageEvent_DestroysFollowerWithExistingBuffRemovalFloor()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            using (context)
            {
                ApplyBuffRemovalHealthFloor(context, buffManager, buffFactory, gameState, card);
                Assert.Equal(1, card.CurrentHealth);

                var damageEvent = new DamageEvent(card, 1, card, DamageKind.Ability);
                var result = damageEvent.Commit(context);

                Assert.True(result.Success);
                var leaveField = Assert.Single(result.GeneratedEvents.OfType<LeaveFieldEvent>());
                Assert.Equal(LeaveFieldKind.Destroy, leaveField.RequestedKind);
            }
        }

        [Fact]
        public void BuffService_ClearsBuffRemovalHealthFloorWhenRawMaxHealthBecomesPositive()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            using (context)
            {
                ApplyBuffRemovalHealthFloor(context, buffManager, buffFactory, gameState, card);
                Assert.Equal(1, card.HealthFloorFromBuffRemoval);

                var recoveryBuff = buffFactory.CreateSystemStatBuff(
                    target: card,
                    statModifications: new List<StatModification> { new(StatType.Health, 2, ModifierLayer.Additive) },
                    durationType: DurationType.Permanent,
                    durationParameter: null,
                    currentTurn: 1);
                buffManager.RegisterBuffInstance(recoveryBuff, gameState);
                context.BuffService.InvalidateDerivedState(gameState, card.InstanceId);

                Assert.Equal(2, context.BuffService.GetEffectiveMaxHealth(card, context));
                Assert.Equal(0, card.HealthFloorFromBuffRemoval);
            }
        }

        [Fact]
        public void CardInstance_ResetToDefinitionState_ClearsBuffRemovalHealthFloor()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            using (context)
            {
                ApplyBuffRemovalHealthFloor(context, buffManager, buffFactory, gameState, card);
                Assert.Equal(1, card.HealthFloorFromBuffRemoval);

                card.ResetToDefinitionState();

                Assert.Equal(0, card.HealthFloorFromBuffRemoval);
                Assert.Equal(0, card.Damage);
            }
        }

        [Fact]
        public void CardInstance_CopyRuntimeStateFrom_DoesNotCopyBuffRemovalHealthFloor()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, source) = CreateGameState(buffManager);
            var target = new CardInstance(Guid.NewGuid(), 1002, gameState.Player1.PlayerId) { CurrentZone = CardZone.Field };
            gameState.Player1.Field.Add(target);
            gameState.RegisterCardInstance(target);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            using (context)
            {
                ApplyBuffRemovalHealthFloor(context, buffManager, buffFactory, gameState, source);
                Assert.Equal(1, source.HealthFloorFromBuffRemoval);

                target.CopyRuntimeStateFrom(source);

                Assert.Equal(0, target.HealthFloorFromBuffRemoval);
                Assert.Equal(source.Damage, target.Damage);
            }
        }

        [Fact]
        public void DerivedStateCache_MultiplicativeUsesMultiplierAndCeiling()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var setBuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Attack, 3, ModifierLayer.Set) },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(setBuff, gameState);

            var multiplyBuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Attack, 1.5, ModifierLayer.Multiplicative) },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(multiplyBuff, gameState);

            using (context)
            {
                var effectiveAttack = context.BuffService.GetEffectiveAttack(card, context);
                Assert.Equal(5, effectiveAttack);
            }
        }

        [Fact]
        public void AbilityKeywordMultiplicative_UsesMultiplierAndCeiling()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, _) = CreateContext(gameState, buffManager, evaluator);

            var buff = new Buff(
                sourceAbilityId: Guid.Empty,
                targetCardInstanceId: card.InstanceId,
                currentTurn: 1,
                effects: new List<IBuffEffect>
                {
                    new AbilityKeywordBuffEffect(new List<AbilityKeywordModification>
                    {
                        new(AbilityKeyword.Act, 0.5, ModifierLayer.Multiplicative)
                    })
                },
                durationType: DurationType.Permanent,
                durationParameter: null);

            buffManager.RegisterBuffInstance(buff, gameState);

            using (context)
            {
                var effective = context.BuffService.GetEffectiveAbilityKeywordValue(card, AbilityKeyword.Act, 3, context);
                Assert.Equal(2, effective);
            }
        }

        [Fact]
        public void RemoveAllBuffs_BatchesByStoredSourceIdentity_WhenRuntimeSourceCardCannotBeResolved()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, _) = CreateContext(gameState, buffManager, evaluator);

            var sourceCardId = Guid.NewGuid();
            var firstBuff = new Buff(
                sourceAbilityId: Guid.NewGuid(),
                targetCardInstanceId: card.InstanceId,
                currentTurn: 1,
                effects: new List<IBuffEffect>())
            {
                SourceCardInstanceId = sourceCardId
            };
            var secondBuff = new Buff(
                sourceAbilityId: Guid.NewGuid(),
                targetCardInstanceId: card.InstanceId,
                currentTurn: 1,
                effects: new List<IBuffEffect>())
            {
                SourceCardInstanceId = sourceCardId
            };

            buffManager.RegisterBuffInstance(firstBuff, gameState);
            buffManager.RegisterBuffInstance(secondBuff, gameState);

            using (context)
            {
                var result = context.BuffService.RemoveAllBuffs(card.InstanceId, context);

                Assert.True(result.Success);
                var removedEvent = Assert.Single(result.GeneratedEvents.OfType<BuffRemovedEvent>());
                Assert.Null(removedEvent.RemovalSource);
                Assert.Equal(card.InstanceId, removedEvent.RemovalTarget?.InstanceId);
                Assert.Equal(2, removedEvent.RemovedBuffs.Count);
                Assert.Contains(removedEvent.RemovedBuffs, buff => buff.Id == firstBuff.Id);
                Assert.Contains(removedEvent.RemovedBuffs, buff => buff.Id == secondBuff.Id);
            }
        }

        [Fact]
        public void DerivedStateKeywords_RemoveThenAdd_ReappliesKeyword()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var addShieldBeforeRemove = buffFactory.CreateSystemKeywordBuff(
                target: card,
                keywords: new List<CardKeyword> { CardKeyword.Shield },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(addShieldBeforeRemove, gameState);

            var removeShield = buffFactory.CreateSystemRemoveKeywordBuff(
                target: card,
                keywordsToRemove: new List<CardKeyword> { CardKeyword.Shield },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(removeShield, gameState);

            var addShieldAfterRemove = buffFactory.CreateSystemKeywordBuff(
                target: card,
                keywords: new List<CardKeyword> { CardKeyword.Shield },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(addShieldAfterRemove, gameState);

            using (context)
            {
                var effectiveKeywords = context.BuffService.GetEffectiveKeywords(card, context);
                Assert.Contains(CardKeyword.Shield, effectiveKeywords);
            }
        }

        [Fact]
        public void DerivedStateKeywords_AddThenRemove_KeywordIsAbsent()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var addShield = buffFactory.CreateSystemKeywordBuff(
                target: card,
                keywords: new List<CardKeyword> { CardKeyword.Shield },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(addShield, gameState);

            var removeShield = buffFactory.CreateSystemRemoveKeywordBuff(
                target: card,
                keywordsToRemove: new List<CardKeyword> { CardKeyword.Shield },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(removeShield, gameState);

            using (context)
            {
                var effectiveKeywords = context.BuffService.GetEffectiveKeywords(card, context);
                Assert.DoesNotContain(CardKeyword.Shield, effectiveKeywords);
            }
        }

        [Fact]
        public void TribeBuff_ApplyAndRemove_UpdatesCardTribe()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, _) = CreateContext(gameState, buffManager, evaluator);

            using (context)
            {
                Assert.DoesNotContain("亡者", card.Tribe);

                var applyResult = context.BuffService.ApplyTribeBuff(
                    context,
                    card,
                    new List<string> { "亡者" },
                    DurationType.Permanent,
                    null);
                Assert.True(applyResult.Success);

                var appliedEvent = Assert.Single(applyResult.GeneratedEvents.OfType<BuffAppliedEvent>());
                var applyCommit = appliedEvent.Commit(context);
                Assert.True(applyCommit.Success);
                Assert.Contains("亡者", card.Tribe);

                var removeResult = context.BuffService.RemoveBuffProcess(appliedEvent.AppliedBuff.Id, context);
                Assert.True(removeResult.Success);

                var removedEvent = Assert.Single(removeResult.GeneratedEvents.OfType<BuffRemovedEvent>());
                var removeCommit = removedEvent.Commit(context);
                Assert.True(removeCommit.Success);
                Assert.DoesNotContain("亡者", card.Tribe);
            }
        }

        [Fact]
        public void ActionUsageLimitBuff_EndOfTurnExpiry_RestoresBaseLimit()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var buff = buffFactory.CreateSystemActionUsageLimitBuff(
                target: card,
                modifications: new List<ActionUsageLimitModification>
                {
                    new(CardActionUsageType.Attack, 2, ModifierLayer.Set)
                },
                durationType: DurationType.EndOfTurn,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(buff, gameState);

            using (context)
            {
                Assert.Equal(2, context.BuffService.GetEffectiveActionUsageLimit(card, CardActionUsageType.Attack, context));

                var removedEvent = Assert.Single(context.BuffService.CollectEndOfTurnExpiryEvents(context));
                var removeResult = removedEvent.Commit(context);

                Assert.True(removeResult.Success);
                Assert.Equal(1, context.BuffService.GetEffectiveActionUsageLimit(card, CardActionUsageType.Attack, context));
            }
        }

        [Fact]
        public void ActionUsageLimitBuff_IsIgnoredWhileCardIsSilenced()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, buffFactory) = CreateContext(gameState, buffManager, evaluator);

            var buff = buffFactory.CreateSystemActionUsageLimitBuff(
                target: card,
                modifications: new List<ActionUsageLimitModification>
                {
                    new(CardActionUsageType.Attack, 2, ModifierLayer.Set)
                },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            buffManager.RegisterBuffInstance(buff, gameState);

            using (context)
            {
                Assert.Equal(2, context.BuffService.GetEffectiveActionUsageLimit(card, CardActionUsageType.Attack, context));

                var silenceResult = card.ApplySilence();
                Assert.True(silenceResult.Success);
                Assert.Equal(1, context.BuffService.GetEffectiveActionUsageLimit(card, CardActionUsageType.Attack, context));

                var unsilenceResult = card.RemoveSilence();
                Assert.True(unsilenceResult.Success);
                Assert.Equal(2, context.BuffService.GetEffectiveActionUsageLimit(card, CardActionUsageType.Attack, context));
            }
        }

        [Fact]
        public void AttackTimesUpdatedEvent_AppliesBuffAndSupportsMultipleUses()
        {
            var buffManager = new TestBuffManager();
            var evaluator = new CountingBuffEvaluator(true);

            var (gameState, _, card) = CreateGameState(buffManager);
            var (context, _) = CreateContext(gameState, buffManager, evaluator);
            card.ZoneStayTurn = 1;

            using (context)
            {
                var updateResult = new AttackTimesUpdatedEvent(card, 2, "test.multi_attack").Commit(context);
                Assert.True(updateResult.Success);

                var appliedEvent = Assert.Single(updateResult.GeneratedEvents.OfType<BuffAppliedEvent>());
                var applyResult = appliedEvent.Commit(context);
                Assert.True(applyResult.Success);

                var stateUpdatedEvent = Assert.Single(applyResult.GeneratedEvents.OfType<AttackStateUpdatedEvent>());
                Assert.True(stateUpdatedEvent.CanAttack);
                Assert.Equal(2, card.MaxAttacksPerTurn);

                card.ConsumeActionUsage(CardActionUsageType.Attack);
                Assert.True(card.CanAttack);

                card.ConsumeActionUsage(CardActionUsageType.Attack);
                Assert.False(card.CanAttack);
                Assert.False(card.HasRemainingActionUsage(CardActionUsageType.Attack));
            }
        }

        private static (GameState GameState, Guid Player1Id, CardInstance Card) CreateGameState(TestBuffManager buffManager)
        {
            SetTestGameDataProvider();

            var player1Id = Guid.NewGuid();
            var player2Id = Guid.NewGuid();
            var player1 = new PlayerState(player1Id) { IsFirst = true };
            var player2 = new PlayerState(player2Id);
            var abilityManager = new Mock<IAbilityManager>();
            abilityManager.Setup(manager => manager.GetAbilityRuntimeIdsForBuff(It.IsAny<Guid>()))
                .Returns(new List<Guid>());
            abilityManager.Setup(manager => manager.GetEffectiveAbilitiesForCard(It.IsAny<Guid>(), It.IsAny<GameContext>()))
                .Returns(new List<IAbility>());

            var gameState = new GameState("buff-cache-test", player1, player2, player1Id)
            {
                AbilityManager = abilityManager.Object,
                BuffManager = buffManager
            };

            var card = new CardInstance(Guid.NewGuid(), 1001, player1Id) { CurrentZone = CardZone.Field };
            player1.Field.Add(card);
            gameState.RegisterCardInstance(card);

            return (gameState, player1Id, card);
        }

        private static void SetTestGameDataProvider()
        {
            var mockProvider = new Mock<IGameDataProvider>();
            mockProvider.SetupGet(provider => provider.IsInitialized).Returns(true);
            mockProvider.Setup(provider => provider.GetCardDefinition(1001)).Returns(new CardDefinition
            {
                Id = 1001,
                Name = "TestFollower",
                Type = CardType.Follower,
                Attack = 1,
                Health = 1
            });
            mockProvider.Setup(provider => provider.GetCardDefinition(1002)).Returns(new CardDefinition
            {
                Id = 1002,
                Name = "TestFollower2",
                Type = CardType.Follower,
                Attack = 1,
                Health = 1
            });
            mockProvider.Setup(provider => provider.GetAbilityConfigs(It.IsAny<int>())).Returns(new List<AbilityConfig>());
            GameDataProvider.SetInstanceForTesting(mockProvider.Object);
        }

        private static void ApplyBuffRemovalHealthFloor(
            GameContext context,
            TestBuffManager buffManager,
            BuffFactory buffFactory,
            GameState gameState,
            CardInstance card)
        {
            var lethalDebuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Health, -card.BaseHealth, ModifierLayer.Additive) },
                durationType: DurationType.Permanent,
                durationParameter: null,
                currentTurn: 1);
            var expiringHealthBuff = buffFactory.CreateSystemStatBuff(
                target: card,
                statModifications: new List<StatModification> { new(StatType.Health, 1, ModifierLayer.Additive) },
                durationType: DurationType.EndOfTurn,
                durationParameter: null,
                currentTurn: 1);

            buffManager.RegisterBuffInstance(lethalDebuff, gameState);
            buffManager.RegisterBuffInstance(expiringHealthBuff, gameState);
            context.BuffService.InvalidateDerivedState(gameState, card.InstanceId);

            var result = new BuffRemovedEvent(expiringHealthBuff, card, null, "duration_end_of_turn").Commit(context);

            Assert.True(result.Success);
            Assert.Empty(result.GeneratedEvents.OfType<LeaveFieldEvent>());
            Assert.Equal(1, context.BuffService.GetEffectiveMaxHealth(card, context));
            Assert.Equal(1, card.CurrentHealth);
        }

        private static (GameContext Context, BuffFactory BuffFactory) CreateContext(
            GameState gameState,
            TestBuffManager buffManager,
            IBuffEvaluator evaluator,
            IConditionEvaluator? conditionEvaluator = null)
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
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(buffService);
            services.AddSingleton(conditionEvaluator ?? new TestConditionEvaluator());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var serviceProvider = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

            var context = new GameContext(
                serviceProvider,
                Mock.Of<IMapper>(),
                serviceProvider.GetRequiredService<IGameContextAccessor>(),
                gameState,
                gameState.Player1.PlayerId);

            return (context, buffFactory);
        }

        private sealed class TestConditionEvaluator : IConditionEvaluator
        {
            public bool Result { get; set; }
            public ConditionConfig? LastConfig { get; private set; }

            public bool Evaluate(ConditionConfig config, GameContext context)
            {
                LastConfig = config;
                return Result;
            }
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
