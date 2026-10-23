using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests;

public class CardInstanceEffectKeywordsTests
{
    [Fact]
    public void EffectKeywords_AggregatesAndDeduplicates()
    {
        var ownerId = Guid.NewGuid();
        var card = new CardInstance(Guid.NewGuid(), 1, ownerId);

        var abilityA = CreateAbility(
            new AbilityConfig
            {
                AbilityKeywords = new List<AbilityKeywordConfig>
                {
                    new AbilityKeywordConfig { Keyword = AbilityKeyword.Fanfare },
                    new AbilityKeywordConfig { Keyword = AbilityKeyword.Act }
                }
            },
            card);

        var abilityB = CreateAbility(
            new AbilityConfig
            {
                AbilityKeywords = new List<AbilityKeywordConfig>
                {
                    new AbilityKeywordConfig { Keyword = AbilityKeyword.Act },
                    new AbilityKeywordConfig { Keyword = AbilityKeyword.None }
                }
            },
            card);

        card.SetAbilities(new List<Ability> { abilityA, abilityB });

        var keywords = card.AbilityKeywords;

        Assert.Contains(AbilityKeyword.Fanfare, keywords);
        Assert.Contains(AbilityKeyword.Act, keywords);
        Assert.DoesNotContain(AbilityKeyword.None, keywords);
        Assert.Equal(2, keywords.Count);
    }

    private static Ability CreateAbility(AbilityConfig config, CardInstance owner)
    {
        var conditionFactory = new NullConditionFactory();
        var targetFactory = new NullTargetFactory();
        var targetSelectionService = new TargetSelectionService(
            NullLogger<TargetSelectionService>.Instance,
            new NullQueryExecutor(),
            new NullPendingActionService());

        return new Ability(config, conditionFactory, targetFactory, targetSelectionService, owner);
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
            => new() { Success = false, ErrorMessage = "Not used in this test." };

        public QueryResult ExecuteQuery(QueryConfig queryConfig, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
            => new() { Success = false, ErrorMessage = "Not used in this test." };
    }

    private sealed class NullPendingActionService : IPendingActionService
    {
        public System.Threading.Tasks.Task<PlayerChoiceResult> WaitForPlayerChoiceAsync(string gameId, PlayerChoiceRequest request, System.Threading.CancellationToken cancellationToken)
            => throw new NotImplementedException("Not used in this test.");

        public void CompletePlayerChoice(Guid choiceId, Dtos.SignalR.Actions.SubmitTargetChoiceActionDto dto)
            => throw new NotImplementedException("Not used in this test.");

        public PendingChoiceContext? GetPendingChoiceForPlayer(string gameId, Guid playerId)
            => throw new NotImplementedException("Not used in this test.");

        public void ForceResolveChoice(Guid choiceId, PlayerChoiceResult forcedResult)
            => throw new NotImplementedException("Not used in this test.");
    }
}
