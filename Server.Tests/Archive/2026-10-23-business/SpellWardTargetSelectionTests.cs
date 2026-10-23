using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class SpellWardTargetSelectionTests
    {
        [Fact]
        public async Task ResolveTargetsAsync_NonChoiceTarget_DoesNotFilterBySpellWard()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    // Simulate a query that already produced a random/auto-selected target.
                    Results = new List<object> { harness.NormalFollower }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var result = await ability.ResolveTargetsAsync(context);

            Assert.Equal(ResolutionStatus.Completed, result.Status);
            Assert.True(result.ResolvedTargets.TryGetValue("target", out var resolvedTargets));
            Assert.Single(resolvedTargets);
            Assert.Equal(harness.NormalFollower.InstanceId, resolvedTargets[0].InstanceId);
        }

        [Fact]
        public async Task CheckTargetAvailabilityAsync_PlayerChoiceTarget_IsFilteredBySpellWard()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower, harness.SpellWardFollower }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 1,
                        MaxCount = 1
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var availability = await ability.CheckTargetAvailabilityAsync(context);

            Assert.True(availability.IsAvailable);
            Assert.NotNull(availability.ValidTargetIds);
            Assert.Contains(harness.SpellWardFollower.InstanceId, availability.ValidTargetIds!);
            Assert.DoesNotContain(harness.NormalFollower.InstanceId, availability.ValidTargetIds!);
        }

        [Fact]
        public async Task CheckTargetAvailabilityAsync_PlayerChoiceBelowMin_BlockExecutionOnly_RemainsAvailable()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    TargetHandling = TargetHandlingStrategy.BlockAbilityExecutionOnly,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 2,
                        MaxCount = 2
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var availability = await ability.CheckTargetAvailabilityAsync(context);

            Assert.True(availability.IsAvailable);
            Assert.True(availability.HasValidTargets);
            Assert.True(availability.CanPlayWithoutTarget);
            Assert.NotNull(availability.ValidTargetIds);
            Assert.Contains(harness.NormalFollower.InstanceId, availability.ValidTargetIds!);
        }

        [Fact]
        public async Task ResolveTargetsAsync_PlayerChoiceBelowMin_BlockExecutionOnly_SkipsAbilityExecution()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    TargetHandling = TargetHandlingStrategy.BlockAbilityExecutionOnly,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 2,
                        MaxCount = 2
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var availability = await ability.CheckTargetAvailabilityAsync(context);
            Assert.True(availability.IsAvailable);

            var result = await ability.ResolveTargetsAsync(context);
            Assert.Equal(ResolutionStatus.Skipped, result.Status);
            Assert.Contains("可选目标不足", result.ErrorMessage ?? string.Empty);

            harness.PendingActionService.Verify(x => x.WaitForPlayerChoiceAsync(
                It.IsAny<string>(),
                It.IsAny<PlayerChoiceRequest>(),
                It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ResolveTargetsAsync_PlayerChoiceBelowMin_BlockPlayerOperation_FailsAtResolution()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    TargetHandling = TargetHandlingStrategy.BlockPlayerOperation,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 2,
                        MaxCount = 2
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var availability = await ability.CheckTargetAvailabilityAsync(context);
            Assert.False(availability.IsAvailable);

            var result = await ability.ResolveTargetsAsync(context);
            Assert.Equal(ResolutionStatus.Failed, result.Status);
            Assert.Contains("可选目标不足", result.ErrorMessage ?? string.Empty);

            harness.PendingActionService.Verify(x => x.WaitForPlayerChoiceAsync(
                It.IsAny<string>(),
                It.IsAny<PlayerChoiceRequest>(),
                It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ResolveTargetsAsync_PlayerChoiceBelowMin_DoNotIntercept_AutoSelectsAllSelectableTargets()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = false,
                    TargetHandling = TargetHandlingStrategy.DoNotIntercept,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 2,
                        MaxCount = 2
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var result = await ability.ResolveTargetsAsync(context);

            Assert.Equal(ResolutionStatus.Completed, result.Status);
            Assert.True(result.ResolvedTargets.TryGetValue("target", out var resolvedTargets));
            Assert.Single(resolvedTargets);
            Assert.Equal(harness.NormalFollower.InstanceId, resolvedTargets[0].InstanceId);

            harness.PendingActionService.Verify(x => x.WaitForPlayerChoiceAsync(
                It.IsAny<string>(),
                It.IsAny<PlayerChoiceRequest>(),
                It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckTargetAvailabilityAsync_PlayerChoiceBelowMin_BlockPlayerOperation_BecomesUnavailable()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    TargetHandling = TargetHandlingStrategy.BlockPlayerOperation,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 2,
                        MaxCount = 2
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var availability = await ability.CheckTargetAvailabilityAsync(context);

            Assert.False(availability.IsAvailable);
            Assert.Contains("可选目标不足", availability.FailureReason ?? string.Empty);
        }

        [Fact]
        public async Task CheckTargetAvailabilityAsync_PlayerChoiceBelowMin_LegacyRequireValidTarget_BlocksPlayerOperation()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 2,
                        MaxCount = 2
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var availability = await ability.CheckTargetAvailabilityAsync(context);

            Assert.False(availability.IsAvailable);
            Assert.Contains("可选目标不足", availability.FailureReason ?? string.Empty);
        }

        [Fact]
        public async Task ResolveTargetsAsync_PlayerChoiceMultiTarget_AllowsNormalAfterSpellWard()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower, harness.SpellWardFollower }
                });

            harness.PendingActionService
                .Setup(x => x.WaitForPlayerChoiceAsync(
                    It.IsAny<string>(),
                    It.IsAny<PlayerChoiceRequest>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, System.Threading.CancellationToken _) =>
                    new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = new List<Guid> { harness.SpellWardFollower.InstanceId, harness.NormalFollower.InstanceId }
                    });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 2,
                        MaxCount = 2
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var result = await ability.ResolveTargetsAsync(context);

            Assert.Equal(ResolutionStatus.Completed, result.Status);
            Assert.True(result.ResolvedTargets.TryGetValue("target", out var resolvedTargets));
            Assert.Equal(2, resolvedTargets.Count);
            Assert.Contains(resolvedTargets, target => target.InstanceId == harness.SpellWardFollower.InstanceId);
            Assert.Contains(resolvedTargets, target => target.InstanceId == harness.NormalFollower.InstanceId);
        }

        [Fact]
        public async Task ResolveTargetsAsync_PlayerChoiceSingleTarget_IgnoresSpellWardOutsideInitialPool()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    // SpellWard follower exists on board but is not in initial query pool.
                    Results = new List<object> { harness.NormalFollower }
                });

            harness.PendingActionService
                .Setup(x => x.WaitForPlayerChoiceAsync(
                    It.IsAny<string>(),
                    It.IsAny<PlayerChoiceRequest>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync((string _, PlayerChoiceRequest request, System.Threading.CancellationToken _) =>
                    new PlayerChoiceResult
                    {
                        WasCancelled = false,
                        ChosenTargetIds = new List<Guid> { harness.NormalFollower.InstanceId }
                    });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 1,
                        MaxCount = 1
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var result = await ability.ResolveTargetsAsync(context);

            Assert.Equal(ResolutionStatus.Completed, result.Status);
            Assert.True(result.ResolvedTargets.TryGetValue("target", out var resolvedTargets));
            Assert.Single(resolvedTargets);
            Assert.Equal(harness.NormalFollower.InstanceId, resolvedTargets[0].InstanceId);
        }

        [Fact]
        public async Task ResolveTargetsAsync_PlayerChoiceInverseSingleTarget_SpellWardOnlyAffectsSelectableRange()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower, harness.SpellWardFollower }
                });

            PlayerChoiceRequest? capturedRequest = null;
            harness.PendingActionService
                .Setup(x => x.WaitForPlayerChoiceAsync(
                    It.IsAny<string>(),
                    It.IsAny<PlayerChoiceRequest>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .Callback((string _, PlayerChoiceRequest request, System.Threading.CancellationToken _) =>
                {
                    capturedRequest = request;
                })
                .ReturnsAsync(new PlayerChoiceResult
                {
                    WasCancelled = false,
                    ChosenTargetIds = new List<Guid> { harness.SpellWardFollower.InstanceId }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 1,
                        MaxCount = 1,
                        IsInverseSelection = true
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var result = await ability.ResolveTargetsAsync(context);

            Assert.NotNull(capturedRequest);
            Assert.Single(capturedRequest!.SelectableTargetIds);
            Assert.Equal(harness.SpellWardFollower.InstanceId, capturedRequest.SelectableTargetIds[0]);

            Assert.Equal(ResolutionStatus.Completed, result.Status);
            Assert.True(result.ResolvedTargets.TryGetValue("target", out var resolvedTargets));
            Assert.Single(resolvedTargets);
            Assert.Equal(harness.NormalFollower.InstanceId, resolvedTargets[0].InstanceId);
        }

        [Fact]
        public async Task ResolveTargetsAsync_PlayerChoiceInverseSingleTarget_AmbushOnlyAffectsSelectableRange()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object> { harness.NormalFollower, harness.AmbushFollower }
                });

            PlayerChoiceRequest? capturedRequest = null;
            harness.PendingActionService
                .Setup(x => x.WaitForPlayerChoiceAsync(
                    It.IsAny<string>(),
                    It.IsAny<PlayerChoiceRequest>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .Callback((string _, PlayerChoiceRequest request, System.Threading.CancellationToken _) =>
                {
                    capturedRequest = request;
                })
                .ReturnsAsync(new PlayerChoiceResult
                {
                    WasCancelled = false,
                    ChosenTargetIds = new List<Guid> { harness.NormalFollower.InstanceId }
                });

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 1,
                        MaxCount = 1,
                        IsInverseSelection = true
                    }
                });

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var result = await ability.ResolveTargetsAsync(context);

            Assert.NotNull(capturedRequest);
            Assert.Single(capturedRequest!.SelectableTargetIds);
            Assert.Equal(harness.NormalFollower.InstanceId, capturedRequest.SelectableTargetIds[0]);

            Assert.Equal(ResolutionStatus.Completed, result.Status);
            Assert.True(result.ResolvedTargets.TryGetValue("target", out var resolvedTargets));
            Assert.Single(resolvedTargets);
            Assert.Equal(harness.AmbushFollower.InstanceId, resolvedTargets[0].InstanceId);
        }

        [Fact]
        public async Task ResolveTargetsAsync_FusionChoice_NoSelectableTargets_StillRequestsCancellableChoice()
        {
            var harness = CreateHarness();
            harness.QueryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult
                {
                    Success = true,
                    Results = new List<object>()
                });

            PlayerChoiceRequest? capturedRequest = null;
            harness.PendingActionService
                .Setup(x => x.WaitForPlayerChoiceAsync(
                    It.IsAny<string>(),
                    It.IsAny<PlayerChoiceRequest>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .Callback((string _, PlayerChoiceRequest request, System.Threading.CancellationToken _) =>
                {
                    capturedRequest = request;
                })
                .ReturnsAsync(PlayerChoiceResult.Cancelled);

            var ability = CreateAbility(
                harness.TargetSelectionService,
                harness.SourceCard,
                new TargetConfig
                {
                    Query = new QueryConfig { SourcePoolExpression = "stub" },
                    RequireValidTarget = true,
                    PlayerChoiceOptions = new PlayerChoiceConfig
                    {
                        IsRequired = true,
                        MinCount = 1,
                        MaxCount = 1
                    }
                },
                AbilityKeyword.Fusion);

            using var context = new GameContext(
                harness.ServiceProvider,
                Mock.Of<IMapper>(),
                harness.Accessor,
                harness.GameState,
                harness.Player1.PlayerId,
                sourceCard: harness.SourceCard);

            var result = await ability.ResolveTargetsAsync(context, isCancellable: true);

            Assert.Equal(ResolutionStatus.Failed, result.Status);
            Assert.True(result.WasCancelled);
            Assert.NotNull(capturedRequest);
            Assert.Empty(capturedRequest!.SelectableTargetIds);
            Assert.Equal(1, capturedRequest.MinSelectionCount);
            Assert.Equal(1, capturedRequest.MaxSelectionCount);
            Assert.True(capturedRequest.IsCancellable);
        }

        private static Ability CreateAbility(
            TargetSelectionService targetSelectionService,
            CardInstance sourceCard,
            TargetConfig targetConfig,
            params AbilityKeyword[] abilityKeywords)
        {
            var conditionFactory = Mock.Of<IConditionFactory>();
            var targetFactory = new TargetFactory(targetSelectionService, NullLogger<Target>.Instance);

            var config = new AbilityConfig
            {
                Name = "spellward-target-test",
                AbilityKeywords = abilityKeywords.Select(keyword => new AbilityKeywordConfig
                {
                    Keyword = keyword
                }).ToList(),
                Targets = new Dictionary<string, TargetConfig>
                {
                    ["target"] = targetConfig
                }
            };

            return new Ability(config, conditionFactory, targetFactory, targetSelectionService, sourceCard);
        }

        private static TestHarness CreateHarness()
        {
            var accessor = new GameContextAccessor();
            var queryExecutor = new Mock<IQueryExecutor>();
            var pendingActionService = new Mock<IPendingActionService>();
            var buffService = new Mock<IBuffService>();

            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("spellward-target-selection-test", player1, player2, player1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>(),
                CurrentPlayerId = player1.PlayerId,
                Phase = GamePhase.Main
            };

            var sourceCard = new CardInstance(Guid.NewGuid(), 1000, player1.PlayerId) { CurrentZone = CardZone.Field };
            var spellWardFollower = new CardInstance(Guid.NewGuid(), 1001, player2.PlayerId) { CurrentZone = CardZone.Field };
            var normalFollower = new CardInstance(Guid.NewGuid(), 1002, player2.PlayerId) { CurrentZone = CardZone.Field };
            var ambushFollower = new CardInstance(Guid.NewGuid(), 1003, player2.PlayerId) { CurrentZone = CardZone.Field };

            player1.Field.Add(sourceCard);
            player2.Field.Add(spellWardFollower);
            player2.Field.Add(normalFollower);
            player2.Field.Add(ambushFollower);

            gameState.RegisterCardInstance(sourceCard);
            gameState.RegisterCardInstance(spellWardFollower);
            gameState.RegisterCardInstance(normalFollower);
            gameState.RegisterCardInstance(ambushFollower);

            buffService
                .Setup(x => x.GetEffectiveKeywords(It.IsAny<CardInstance>(), It.IsAny<GameContext>()))
                .Returns<CardInstance, GameContext>((card, _) =>
                {
                    if (card.InstanceId == spellWardFollower.InstanceId)
                    {
                        return new List<CardKeyword> { CardKeyword.SpellWard };
                    }

                    if (card.InstanceId == ambushFollower.InstanceId)
                    {
                        return new List<CardKeyword> { CardKeyword.Ambush };
                    }

                    return new List<CardKeyword>();
                });

            queryExecutor
                .Setup(x => x.ExecuteQuery(
                    It.IsAny<QueryConfig>(),
                    It.IsAny<GameContext>(),
                    It.IsAny<Dictionary<string, List<TargetInfo>>?>()))
                .Returns(new QueryResult { Success = true, Results = new List<object>() });

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor>(accessor);
            services.AddSingleton<IQueryExecutor>(queryExecutor.Object);
            services.AddSingleton<IBuffService>(buffService.Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();

            var serviceProvider = services.BuildServiceProvider();
            GameServiceProvider.ContextAccessor = accessor;

            var targetSelectionService = new TargetSelectionService(
                NullLogger<TargetSelectionService>.Instance,
                queryExecutor.Object,
                pendingActionService.Object);

            return new TestHarness(
                serviceProvider,
                accessor,
                queryExecutor,
                pendingActionService,
                targetSelectionService,
                gameState,
                player1,
                sourceCard,
                spellWardFollower,
                normalFollower,
                ambushFollower);
        }

        private sealed record TestHarness(
            IServiceProvider ServiceProvider,
            GameContextAccessor Accessor,
            Mock<IQueryExecutor> QueryExecutor,
            Mock<IPendingActionService> PendingActionService,
            TargetSelectionService TargetSelectionService,
            GameState GameState,
            PlayerState Player1,
            CardInstance SourceCard,
            CardInstance SpellWardFollower,
            CardInstance NormalFollower,
            CardInstance AmbushFollower);
    }
}
