using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Infrastructure.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EventParamDslFunctionTests
    {
        [Fact]
        public void EventInt_prefers_raw_payload_when_available()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var sp = services.BuildServiceProvider();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var gameState = new GameState("dsl-event-param-test", new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var triggeringEvent = new RawBackedTestEvent(new JsonObject { ["Damage"] = 5 }, damagePropertyValue: 1);

            using var ctx = new GameContext(
                sp,
                Mock.Of<IMapper>(),
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                p1Id,
                default,
                triggeringEvent);

            var fn = new EventIntFunction();
            var result = fn.Execute(new List<object?> { "Damage" }, ctx);

            Assert.Equal(5, result);
        }

        [Fact]
        public void EventInt_falls_back_to_reflection_when_no_raw_payload()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var sp = services.BuildServiceProvider();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var gameState = new GameState("dsl-event-param-test", new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var triggeringEvent = new RawBackedTestEvent(new JsonObject(), damagePropertyValue: 7);

            using var ctx = new GameContext(
                sp,
                Mock.Of<IMapper>(),
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                p1Id,
                default,
                triggeringEvent);

            var fn = new EventIntFunction();
            var result = fn.Execute(new List<object?> { "Damage" }, ctx);

            Assert.Equal(7, result);
        }

        [Fact]
        public void EventCard_prefers_raw_payload_when_available()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var sp = services.BuildServiceProvider();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var gameState = new GameState("dsl-event-param-test", new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var cardFromPayload = new CardInstance(Guid.NewGuid(), 1001, p1Id);
            var cardFromProperty = new CardInstance(Guid.NewGuid(), 1001, p1Id);
            gameState.RegisterCardInstance(cardFromPayload);
            gameState.RegisterCardInstance(cardFromProperty);

            var triggeringEvent = new RawBackedCardTestEvent(
                new JsonObject { ["SummonedCard"] = cardFromPayload.InstanceId.ToString() },
                cardFromProperty);

            using var ctx = new GameContext(
                sp,
                Mock.Of<IMapper>(),
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                p1Id,
                default,
                triggeringEvent);

            var fn = new EventCardFunction();
            var result = fn.Execute(new List<object?> { "SummonedCard" }, ctx);

            Assert.Same(cardFromPayload, result);
        }

        [Fact]
        public void EventCard_falls_back_to_reflection_when_no_raw_payload()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var sp = services.BuildServiceProvider();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var gameState = new GameState("dsl-event-param-test", new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var card = new CardInstance(Guid.NewGuid(), 1001, p1Id);
            gameState.RegisterCardInstance(card);

            var triggeringEvent = new CardTestEvent(card);

            using var ctx = new GameContext(
                sp,
                Mock.Of<IMapper>(),
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                p1Id,
                default,
                triggeringEvent);

            var fn = new EventCardFunction();
            var result = fn.Execute(new List<object?> { "SummonedCard" }, ctx);

            Assert.Same(card, result);
        }

        [Fact]
        public void EventCard_returns_null_when_missing()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(Mock.Of<IBuffService>());
            services.AddSingleton<IEventHistoryService, InMemoryEventHistoryService>();
            var sp = services.BuildServiceProvider();

            var p1Id = Guid.NewGuid();
            var p2Id = Guid.NewGuid();
            var gameState = new GameState("dsl-event-param-test", new PlayerState(p1Id), new PlayerState(p2Id), p1Id)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };

            var triggeringEvent = new CardTestEvent(null);

            using var ctx = new GameContext(
                sp,
                Mock.Of<IMapper>(),
                sp.GetRequiredService<IGameContextAccessor>(),
                gameState,
                p1Id,
                default,
                triggeringEvent);

            var fn = new EventCardFunction();
            var result = fn.Execute(new List<object?> { "SummonedCard" }, ctx);

            Assert.Null(result);
        }

        private sealed class RawBackedTestEvent : IGameEvent, IRawEventBackedGameEvent
        {
            public EventType EventType => EventType.Damage;
            public Guid EventId { get; } = Guid.NewGuid();
            public DateTime Timestamp { get; } = DateTime.UtcNow;
            public TargetInfo? Source { get; } = null;
            public List<TargetInfo> Target { get; } = new();

            public int Damage { get; }
            public JsonObject OutputPayload { get; }

            public RawBackedTestEvent(JsonObject outputPayload, int damagePropertyValue)
            {
                OutputPayload = outputPayload;
                Damage = damagePropertyValue;
            }
        }

        private sealed class RawBackedCardTestEvent : IGameEvent, IRawEventBackedGameEvent
        {
            public EventType EventType => EventType.Summon;
            public Guid EventId { get; } = Guid.NewGuid();
            public DateTime Timestamp { get; } = DateTime.UtcNow;
            public TargetInfo? Source { get; } = null;
            public List<TargetInfo> Target { get; } = new();

            public CardInstance? SummonedCard { get; }
            public JsonObject OutputPayload { get; }

            public RawBackedCardTestEvent(JsonObject outputPayload, CardInstance? summonedCard)
            {
                OutputPayload = outputPayload;
                SummonedCard = summonedCard;
            }
        }

        private sealed class CardTestEvent : IGameEvent
        {
            public EventType EventType => EventType.Summon;
            public Guid EventId { get; } = Guid.NewGuid();
            public DateTime Timestamp { get; } = DateTime.UtcNow;
            public TargetInfo? Source { get; } = null;
            public List<TargetInfo> Target { get; } = new();

            public CardInstance? SummonedCard { get; }

            public CardTestEvent(CardInstance? summonedCard)
            {
                SummonedCard = summonedCard;
            }
        }
    }
}
