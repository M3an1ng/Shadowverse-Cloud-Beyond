using System;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.DSL
{
    public class ConditionTraceTests
    {
        [Fact]
        public void Format_UsesTraceValues()
        {
            var collector = new DslTraceCollector();
            collector.Capture("player.Hand.Count", 2);

            var result = DslTraceFormatter.Format("player.Hand.Count > 3", collector);

            Assert.Equal("player.Hand.Count->(2) > 3", result);
        }

        [Fact]
        public void Evaluate_WithTrace_InvokesEvaluatorOnce()
        {
            var evaluator = new CountingEvaluator();
            var mapper = new Mock<IMapper>().Object;
            var condition = new ShadowverseBeyond.Server.Domain.Abilities.Runtime.Condition(
                new ConditionConfig { Expression = "true" },
                evaluator,
                mapper,
                NullLogger<ShadowverseBeyond.Server.Domain.Abilities.Runtime.Condition>.Instance);

            using var context = CreateTestGameContext(mapper);
            var traceCollector = new DslTraceCollector();

            condition.Evaluate(context, traceCollector);

            Assert.Equal(1, evaluator.Calls);
        }

        private static GameContext CreateTestGameContext(IMapper mapper)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton<IBuffService>(new Mock<IBuffService>().Object);
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var provider = services.BuildServiceProvider();

            var player1 = new PlayerState(Guid.NewGuid());
            var player2 = new PlayerState(Guid.NewGuid());
            var gameState = new GameState("test-game", player1, player2, player1.PlayerId);
            return new GameContext(provider, mapper, provider.GetRequiredService<IGameContextAccessor>(), gameState, player1.PlayerId);
        }

        private sealed class CountingEvaluator : IConditionEvaluator
        {
            public int Calls { get; private set; }

            public bool Evaluate(ConditionConfig config, GameContext context)
            {
                Calls++;
                return false;
            }
        }
    }
}
