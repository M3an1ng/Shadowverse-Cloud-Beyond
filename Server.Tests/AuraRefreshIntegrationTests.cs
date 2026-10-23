using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Auras;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Tests.Scenarios;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class AuraRefreshIntegrationTests
    {
        private const int SourceDefinitionId = 1001;
        private const int FollowerDefinitionId = 1002;
        private const int AmuletDefinitionId = 2001;

        [Fact]
        public async Task Aura_refreshes_on_card_moved_entering_and_leaving_field()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var target = harness.CreateCard(AmuletDefinitionId, harness.Player1, CardZone.Hand);

            var targets = BuildAmuletTargets();
            var auraConfig = BuildAuraConfig();
            harness.RegisterAbility(source, "AuraSourceMove", targets, null, new List<AuraConfig> { auraConfig }, EventType.CardMoved);

            Assert.Empty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));

            var enterResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(target, CardZone.Hand, CardZone.Field) });
            Assert.True(enterResult.IsSuccess);
            Assert.NotEmpty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));

            var leaveResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(target, CardZone.Field, CardZone.Hand) });
            Assert.True(leaveResult.IsSuccess);
            Assert.Empty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));
        }

        [Fact]
        public async Task Aura_refreshes_on_transform_type_change()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var target = harness.CreateCard(FollowerDefinitionId, harness.Player1, CardZone.Field);

            var targets = BuildAmuletTargets();
            var auraConfig = BuildAuraConfig();
            harness.RegisterAbility(source, "AuraSourceTransform", targets, null, new List<AuraConfig> { auraConfig }, EventType.Transform);

            Assert.Empty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));

            var transformResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new TransformEvent(target, AmuletDefinitionId) });
            Assert.True(transformResult.IsSuccess);
            Assert.NotEmpty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));
        }

        [Fact]
        public async Task Aura_is_removed_when_source_leaves_field()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var target = harness.CreateCard(AmuletDefinitionId, harness.Player1, CardZone.Hand);

            var targets = BuildAmuletTargets();
            var auraConfig = BuildAuraConfig();
            harness.RegisterAbility(source, "AuraSourceLeave", targets, null, new List<AuraConfig> { auraConfig }, EventType.CardMoved);

            var enterResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(target, CardZone.Hand, CardZone.Field) });
            Assert.True(enterResult.IsSuccess);
            Assert.NotEmpty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));

            var moveResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(source, CardZone.Field, CardZone.Graveyard) });
            Assert.True(moveResult.IsSuccess);
            Assert.Empty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));
        }

        [Fact]
        public async Task Aura_is_removed_when_source_is_silenced()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var target = harness.CreateCard(AmuletDefinitionId, harness.Player1, CardZone.Hand);

            var targets = BuildAmuletTargets();
            var auraConfig = BuildAuraConfig();
            harness.RegisterAbility(source, "AuraSourceSilence", targets, null, new List<AuraConfig> { auraConfig }, EventType.CardMoved, EventType.SilenceApplied);

            var enterResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(target, CardZone.Hand, CardZone.Field) });
            Assert.True(enterResult.IsSuccess);
            Assert.NotEmpty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));

            var silenceResult = source.ApplySilence();
            Assert.True(silenceResult.Success);
            var processResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                silenceResult.GeneratedEvents);
            Assert.True(processResult.IsSuccess);
            Assert.Empty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));
        }

        [Fact]
        public async Task Aura_stacking_applies_multiple_effect_keys()
        {
            using var harness = new AuraTestHarness();
            var sourceOne = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var sourceTwo = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var target = harness.CreateCard(AmuletDefinitionId, harness.Player1, CardZone.Hand);

            var targets = BuildAmuletTargets();
            var auraConfig = BuildAuraConfig();

            harness.RegisterAbility(sourceOne, "AuraSourceStackOne", targets, null, new List<AuraConfig> { auraConfig }, EventType.CardMoved);
            harness.RegisterAbility(sourceTwo, "AuraSourceStackTwo", targets, null, new List<AuraConfig> { auraConfig }, EventType.CardMoved);

            var enterResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(target, CardZone.Hand, CardZone.Field) });
            Assert.True(enterResult.IsSuccess);

            var buffs = harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId);
            Assert.True(buffs.Count >= 2);
            Assert.True(buffs.Select(b => b.EffectKey).Distinct().Count() >= 2);
        }

        [Fact]
        public async Task Aura_refresh_guard_skips_buff_applied_loop()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var target = harness.CreateCard(AmuletDefinitionId, harness.Player1, CardZone.Hand);
            var dummy = harness.CreateCard(FollowerDefinitionId, harness.Player1, CardZone.Field);

            var targets = BuildAmuletTargets();
            var auraConfig = BuildAuraConfig(canStack: true);
            harness.RegisterAbility(source, "AuraSourceLoopGuard", targets, null, new List<AuraConfig> { auraConfig }, EventType.BuffApplied);

            await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(target, CardZone.Hand, CardZone.Field) });
            Assert.Empty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));

            var dummyBuff = new Buff(Guid.Empty, dummy.InstanceId, harness.Player1.TurnNumber, new List<IBuffEffect>());
            var dummyEvent = new BuffAppliedEvent(dummyBuff, null, null);
            var refreshResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { dummyEvent });
            Assert.True(refreshResult.IsSuccess);

            var buffs = harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId);
            Assert.Single(buffs);
        }

        [Fact]
        public async Task AuraConfig_uses_ability_targets_by_default()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var amulet = harness.CreateCard(AmuletDefinitionId, harness.Player1, CardZone.Hand);
            var follower = harness.CreateCard(FollowerDefinitionId, harness.Player1, CardZone.Field);

            var targets = BuildAmuletTargets();
            var auraConfig = BuildAuraConfig();

            harness.RegisterAbility(source, "AuraConfigDefaultTargets", targets, null, new List<AuraConfig> { auraConfig }, EventType.CardMoved);

            var enterResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(amulet, CardZone.Hand, CardZone.Field) });
            Assert.True(enterResult.IsSuccess);

            Assert.NotEmpty(harness.GameState.BuffManager.GetBuffsForCard(amulet.InstanceId));
            Assert.Empty(harness.GameState.BuffManager.GetBuffsForCard(follower.InstanceId));
        }

        [Fact]
        public async Task Aura_uses_ability_targets_and_conditions()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var amulet = harness.CreateCard(AmuletDefinitionId, harness.Player1, CardZone.Field);
            var follower = harness.CreateCard(FollowerDefinitionId, harness.Player1, CardZone.Hand);

            var abilityConditions = new List<ConditionConfig> { ConditionConfig.FromExpression("true") };
            var auraConfig = BuildAuraConfig();
            harness.RegisterAbility(source, "AuraAbilityTargets", BuildFollowerTargets(), abilityConditions,
                new List<AuraConfig> { auraConfig }, EventType.CardMoved);

            var enterResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(follower, CardZone.Hand, CardZone.Field) });
            Assert.True(enterResult.IsSuccess);

            Assert.Empty(harness.GameState.BuffManager.GetBuffsForCard(amulet.InstanceId));
            Assert.NotEmpty(harness.GameState.BuffManager.GetBuffsForCard(follower.InstanceId));
        }

        [Fact]
        public async Task AuraConfig_conditions_gate_application()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var target = harness.CreateCard(AmuletDefinitionId, harness.Player1, CardZone.Hand);

            var auraConfig = BuildAuraConfig();
            harness.RegisterAbility(source, "AuraConditionFalse", BuildAmuletTargets(),
                new List<ConditionConfig> { ConditionConfig.FromExpression("false") },
                new List<AuraConfig> { auraConfig }, EventType.CardMoved);

            var enterResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(target, CardZone.Hand, CardZone.Field) });
            Assert.True(enterResult.IsSuccess);

            Assert.Empty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));

            var enabledAura = BuildAuraConfig();
            harness.RegisterAbility(source, "AuraConditionTrue", BuildAmuletTargets(),
                new List<ConditionConfig> { ConditionConfig.FromExpression("true") },
                new List<AuraConfig> { enabledAura }, EventType.CardMoved);

            var leaveResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(target, CardZone.Field, CardZone.Hand) });
            Assert.True(leaveResult.IsSuccess);
            var returnResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                new IGameEvent[] { new CardMovedEvent(target, CardZone.Hand, CardZone.Field) });
            Assert.True(returnResult.IsSuccess);

            Assert.NotEmpty(harness.GameState.BuffManager.GetBuffsForCard(target.InstanceId));
        }

        [Fact]
        public async Task AuraConfig_action_usage_limit_buff_updates_target_attack_limit()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(SourceDefinitionId, harness.Player1, CardZone.Field);
            var target = harness.CreateCard(FollowerDefinitionId, harness.Player1, CardZone.Field);
            target.ZoneStayTurn = 1;

            var auraConfig = new AuraConfig
            {
                BuffConfigs = new List<BuffConfig>
                {
                    new BuffConfig
                    {
                        DurationType = DurationType.Permanent,
                        DurationParameter = new StaticValueConfig { Value = 0 },
                        ActionUsageLimitModifiers = new List<ActionUsageLimitModifierConfig>
                        {
                            new ActionUsageLimitModifierConfig
                            {
                                ActionType = CardActionUsageType.Attack,
                                Value = new StaticValueConfig { Value = 2 }
                            }
                        }
                    }
                }
            };

            var ability = harness.RegisterAbility(source, "AuraActionUsage", BuildFollowerTargets(), null, new List<AuraConfig> { auraConfig });

            var refreshResult = await harness.AbilityManager.ProcessSingleAbilityAsync(
                ability,
                harness.Context with { SourceCard = source },
                false,
                false);
            Assert.True(refreshResult.IsSuccess, refreshResult.ErrorMessage);
            Assert.Contains(refreshResult.GeneratedEvents, gameEvent => gameEvent is BuffAppliedEvent);
            var applyResult = await harness.TransactionService.ProcessTransactionAsync(
                harness.Context,
                refreshResult.GeneratedEvents);
            Assert.True(applyResult.IsSuccess);

            Assert.Equal(2, target.MaxAttacksPerTurn);
        }

        [Fact]
        public async Task Aura_refresh_in_effect_context_keeps_effect_immune_target()
        {
            using var harness = new AuraTestHarness();
            var source = harness.CreateCard(AmuletDefinitionId, harness.Player1, CardZone.Field);
            var target = harness.CreateCard(FollowerDefinitionId, harness.Player1, CardZone.Field);

            var immuneBuff = new Buff(
                Guid.Empty,
                target.InstanceId,
                harness.Player1.TurnNumber,
                new List<IBuffEffect>
                {
                    new KeywordBuffEffect(new List<CardKeyword> { CardKeyword.EffectImmune })
                });
            harness.GameState.BuffManager.RegisterBuffInstance(immuneBuff, harness.GameState);
            harness.Context.BuffService.RefreshDerivedStateCache(harness.Context, "test_effect_immune_aura_refresh");

            var auraConfig = BuildAuraConfig();
            var ability = harness.RegisterAbility(source, "AuraEffectImmuneRefresh", BuildFollowerTargets(), null,
                new List<AuraConfig> { auraConfig }, EventType.EffectCompleted);
            var currentEffect = new Effect(
                new EffectConfig { Type = EffectType.Destroy },
                harness.Player1.PlayerId,
                new List<TargetInfo> { TargetInfo.ForCard(target) },
                null,
                Guid.NewGuid(),
                source.InstanceId);
            var effectContext = harness.Context with
            {
                SourceCard = source,
                SourceAbility = ability,
                CurrentEffect = currentEffect
            };

            var result = await harness.AbilityManager.ProcessSingleAbilityAsync(ability, effectContext, false, false);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Contains(result.GeneratedEvents.OfType<BuffAppliedEvent>(), gameEvent =>
                gameEvent.AppliedBuff.TargetCardInstanceId == target.InstanceId);
        }

        private static AuraConfig BuildAuraConfig(bool canStack = false)
        {
            return new AuraConfig
            {
                BuffConfigs = new List<BuffConfig>
                {
                    new BuffConfig
                    {
                        DurationType = DurationType.Permanent,
                        DurationParameter = new StaticValueConfig { Value = 0 },
                        CanStack = canStack,
                        StatModifiers = new List<StatModifierConfig>
                        {
                            new StatModifierConfig
                            {
                                Stat = StatType.Cost,
                                Layer = ModifierLayer.Set,
                                Value = new StaticValueConfig { Value = 0 }
                            }
                        }
                    }
                }
            };
        }

        private static Dictionary<string, TargetConfig> BuildAmuletTargets()
        {
            return new Dictionary<string, TargetConfig>
            {
                ["auraTargets"] = new TargetConfig
                {
                    Query = new QueryConfig
                    {
                        SourcePoolExpression = "owner.field.query(self.CardType == \"Amulet\")"
                    }
                }
            };
        }

        private static Dictionary<string, TargetConfig> BuildFollowerTargets()
        {
            return new Dictionary<string, TargetConfig>
            {
                ["auraTargets"] = new TargetConfig
                {
                    Query = new QueryConfig
                    {
                        SourcePoolExpression = "owner.field.query(self.CardType == \"Follower\")"
                    }
                }
            };
        }

        private sealed class AuraTestHarness : IDisposable
        {
            private static long _abilityId = 1;

            public IServiceProvider Services { get; }
            public GameActionTransactionService TransactionService { get; }
            public GameState GameState { get; }
            public GameContext Context { get; }
            public PlayerState Player1 { get; }
            public PlayerState Player2 { get; }
            public AbilityManager AbilityManager { get; }
            public AuraManager AuraManager { get; }

            public AuraTestHarness()
            {
                var dataProvider = BuildTestDataProvider();
                var pendingActionService = new ScenarioPendingActionService();
                Services = ScenarioServices.BuildServiceProvider(dataProvider, pendingActionService, 1);

                var mapper = Services.GetRequiredService<IMapper>();
                AbilityManager = Services.GetRequiredService<AbilityManager>();
                var effectExecutor = Services.GetRequiredService<IEffectExecutor>();
                TransactionService = ScenarioServices.BuildTransactionService(
                    Services,
                    AbilityManager,
                    pendingActionService,
                    effectExecutor);

                Player1 = new PlayerState(Guid.NewGuid());
                Player2 = new PlayerState(Guid.NewGuid());
                GameState = new GameState("aura-test", Player1, Player2, Player1.PlayerId)
                {
                    AbilityManager = AbilityManager,
                    BuffManager = Services.GetRequiredService<IBuffManager>()
                };

                AuraManager = new AuraManager(
                    NullLogger<AuraManager>.Instance,
                    Services.GetRequiredService<IValueSourceResolver>(),
                    Services.GetRequiredService<IBuffService>(),
                    Services.GetRequiredService<IBuffFactory>());
                GameState.AuraManager = AuraManager;

                var accessor = Services.GetRequiredService<IGameContextAccessor>();
                GameServiceProvider.ContextAccessor = accessor;
                GameServiceProvider.Logger = NullLogger.Instance;

                Context = new GameContext(Services, mapper, accessor, GameState, Player1.PlayerId);
            }

            public CardInstance CreateCard(int definitionId, PlayerState owner, CardZone zone)
            {
                var card = new CardInstance(Guid.NewGuid(), definitionId, owner.PlayerId)
                {
                    CurrentZone = zone
                };

                switch (zone)
                {
                    case CardZone.Field:
                        owner.Field.Add(card);
                        break;
                    case CardZone.Hand:
                        owner.Hand.Add(card);
                        break;
                    case CardZone.Graveyard:
                        owner.Graveyard.Add(card);
                        break;
                }

                GameState.RegisterCardInstance(card);
                return card;
            }

            public Ability RegisterAbility(
                CardInstance sourceCard,
                string name,
                Dictionary<string, TargetConfig>? targets = null,
                List<ConditionConfig>? conditions = null,
                List<AuraConfig>? auras = null,
                params EventType[] eventTypes)
            {
                var config = new AbilityConfig
                {
                    Id = _abilityId++,
                    CardId = sourceCard.BaseDefinitionId,
                    Name = name,
                    Description = string.Empty,
                    Type = AbilityType.Continuous,
                    Trigger = new TriggerConfig
                    {
                        EventTypes = eventTypes.ToList(),
                        Timing = TriggerTiming.After,
                        ValidZones = new List<CardZone> { CardZone.Field }
                    },
                    Targets = targets ?? new Dictionary<string, TargetConfig>(),
                    Conditions = conditions ?? new List<ConditionConfig>(),
                    Auras = auras ?? new List<AuraConfig>()
                };

                var abilities = AbilityManager.CreateAbilitiesForCard(sourceCard, new List<AbilityConfig> { config });
                return abilities.Single();
            }

            public void Dispose()
            {
                Context.Dispose();
            }

            private static TestGameDataProvider BuildTestDataProvider()
            {
                var cardDefinitions = new Dictionary<int, CardDefinition>
                {
                    [0] = new CardDefinition
                    {
                        Id = 0,
                        Name = "Leader",
                        Type = CardType.Leader,
                        Cost = 0,
                        Attack = 0,
                        Health = 20,
                        Keywords = new List<string>(),
                        Tribe = new List<string>()
                    },
                    [SourceDefinitionId] = new CardDefinition
                    {
                        Id = SourceDefinitionId,
                        Name = "Aura Source",
                        Type = CardType.Follower,
                        Cost = 2,
                        Attack = 2,
                        Health = 2,
                        Keywords = new List<string>(),
                        Tribe = new List<string>()
                    },
                    [FollowerDefinitionId] = new CardDefinition
                    {
                        Id = FollowerDefinitionId,
                        Name = "Test Follower",
                        Type = CardType.Follower,
                        Cost = 1,
                        Attack = 1,
                        Health = 1,
                        Keywords = new List<string>(),
                        Tribe = new List<string>()
                    },
                    [AmuletDefinitionId] = new CardDefinition
                    {
                        Id = AmuletDefinitionId,
                        Name = "Test Amulet",
                        Type = CardType.Amulet,
                        Cost = 1,
                        Countdown = 0,
                        Keywords = new List<string>(),
                        Tribe = new List<string>()
                    }
                };

                return new TestGameDataProvider(cardDefinitions, new Dictionary<int, List<AbilityConfig>>());
            }
        }
    }
}
