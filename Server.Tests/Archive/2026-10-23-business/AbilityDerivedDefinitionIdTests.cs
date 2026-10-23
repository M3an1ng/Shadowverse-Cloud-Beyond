using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class AbilityDerivedDefinitionIdTests
{
    [Fact]
    public void DefinitionId_UsesConfigId_WhenNoDerivedDefinitionIdAssigned()
    {
        var ability = CreateAbility(new AbilityConfig { Id = 1234, Name = "Root" });

        Assert.Equal(1234, ability.DefinitionId);
    }

    [Fact]
    public void DefinitionId_UsesStableNegativeDerivedId_ForInlineAbilityPath()
    {
        var ability = CreateAbility(new AbilityConfig { Name = "Inline" });
        var first = AbilityDefinitionIdResolver.CreateInlineDefinitionId(77, "effect/0/chain/1");
        var second = AbilityDefinitionIdResolver.CreateInlineDefinitionId(77, "effect/0/chain/1");
        var other = AbilityDefinitionIdResolver.CreateInlineDefinitionId(77, "effect/0/chain/2");

        ability.AssignDerivedDefinitionId(first);

        Assert.True(first < 0);
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Equal(first, ability.DefinitionId);
        Assert.Equal(0, ability.Config.Id);
    }

    [Fact]
    public void CreateAbilityCopy_PreservesDerivedDefinitionId()
    {
        var ability = CreateAbility(new AbilityConfig { Name = "Inline" });
        var derivedId = AbilityDefinitionIdResolver.CreateInlineDefinitionId(77, "effect/0/chain/1");
        ability.AssignDerivedDefinitionId(derivedId);
        var factory = new AbilityFactory(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AbilityFactory>.Instance,
            new NullConditionFactory(),
            new NullTargetFactory(),
            new TargetSelectionService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                new NullPendingActionService()));

        var copy = factory.CreateAbilityCopy(ability);

        Assert.Equal(derivedId, copy.DefinitionId);
    }

    [Fact]
    public void CreateInlineDefinitionId_UsesNegativeParentDefinitionId_ForNestedInlineAbility()
    {
        var runtimeA = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var runtimeB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var first = AbilityDefinitionIdResolver.CreateInlineDefinitionId(-9, "effect/0/decision/0", runtimeA);
        var second = AbilityDefinitionIdResolver.CreateInlineDefinitionId(-9, "effect/0/decision/0", runtimeB);

        Assert.True(first < 0);
        Assert.Equal(first, second);
    }

    [Fact]
    public void CreateInlineDefinitionId_FallsBackToParentRuntimeId_WhenParentDefinitionIdIsZero()
    {
        var runtimeId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var first = AbilityDefinitionIdResolver.CreateInlineDefinitionId(0, "effect/0/decision/0", runtimeId);
        var second = AbilityDefinitionIdResolver.CreateInlineDefinitionId(0, "effect/0/decision/0", runtimeId);

        Assert.True(first < 0);
        Assert.Equal(first, second);
    }

    private static Ability CreateAbility(AbilityConfig config)
    {
        var card = new CardInstance(Guid.NewGuid(), 1, Guid.NewGuid())
        {
            CurrentZone = CardZone.Field
        };

        return new Ability(
            config,
            new NullConditionFactory(),
            new NullTargetFactory(),
            new TargetSelectionService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<TargetSelectionService>.Instance,
                new NullQueryExecutor(),
                new NullPendingActionService()),
            card);
    }

    private sealed class NullConditionFactory : IConditionFactory
    {
        public ShadowverseBeyond.Server.Domain.Abilities.Runtime.Condition CreateCondition(ConditionConfig config)
            => throw new NotImplementedException("Conditions are not used in this test.");
    }

    private sealed class NullTargetFactory : ITargetFactory
    {
        public ShadowverseBeyond.Server.Domain.Abilities.Runtime.Target Create(string key, TargetConfig config)
            => throw new NotImplementedException("Targets are not used in this test.");
    }

    private sealed class NullQueryExecutor : IQueryExecutor
    {
        public QueryResult ExecuteQuery(string dsl, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
            => new() { Success = false, ErrorMessage = "Queries are not used in this test." };

        public QueryResult ExecuteQuery(QueryConfig queryConfig, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
            => new() { Success = false, ErrorMessage = "Queries are not used in this test." };
    }

    private sealed class NullPendingActionService : IPendingActionService
    {
        public Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, CancellationToken cancellationToken)
            => Task.FromResult(PlayerChoiceResult.Cancelled);

        public void CompletePlayerChoice(Guid choiceId, SubmitTargetChoiceActionDto dto)
        {
        }

        public PendingChoiceContext? GetPendingChoiceForPlayer(string gameId, Guid playerId)
            => null;

        public void ForceResolveChoice(Guid choiceId, PlayerChoiceResult forcedResult)
        {
        }
    }
}
