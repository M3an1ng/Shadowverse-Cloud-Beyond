using Moq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Replay;
using ShadowverseBeyond.Server.Infrastructure.Services;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Constants;
using System.Reflection;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Tests
{
    public class JsonGameEventRawEventMapperTests
    {
        [Fact]
        public void MapToOutputRawEventJson_includes_annotated_properties()
        {
            var p1 = new PlayerState(Guid.NewGuid());
            var p2 = new PlayerState(Guid.NewGuid());
            var target = new CardInstance(Guid.NewGuid(), 1001, p1.PlayerId);
            var source = new CardInstance(Guid.NewGuid(), 1002, p2.PlayerId);

            var gameState = new GameState("raw-mapper-test", p1, p2, p1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };
            gameState.RegisterCardInstance(target);
            gameState.RegisterCardInstance(source);

            var ev = new DamageEvent(target, 3, source);

            var mapper = new JsonGameEventRawEventMapper();
            var json = mapper.MapToOutputRawEventJson(gameState, ev);
            var envelope = JsonSerializer.Deserialize<RawEventEnvelopeDto>(json);

            Assert.NotNull(envelope);
            Assert.Equal(EventType.Damage.ToString(), envelope!.EventType);
            Assert.True(envelope.Payload.TryGetPropertyValue("Damage", out var dmgNode));
            Assert.Equal(3, dmgNode!.GetValue<int>());
        }

        [Fact]
        public void MapToOutputRawEventJson_includes_base_snapshot_properties()
        {
            var p1 = new PlayerState(Guid.NewGuid()) { TurnNumber = 4 };
            var p2 = new PlayerState(Guid.NewGuid());
            var target = new CardInstance(Guid.NewGuid(), 1001, p1.PlayerId);
            var source = new CardInstance(Guid.NewGuid(), 1002, p2.PlayerId);

            var gameState = new GameState("raw-mapper-snapshot-test", p1, p2, p1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };
            gameState.RegisterCardInstance(target);
            gameState.RegisterCardInstance(source);

            var ev = new DamageEvent(target, 3, source);
            SetEventSnapshot(ev, p1.PlayerId, 4);

            var mapper = new JsonGameEventRawEventMapper();
            var json = mapper.MapToOutputRawEventJson(gameState, ev);
            var envelope = JsonSerializer.Deserialize<RawEventEnvelopeDto>(json);

            Assert.NotNull(envelope);
            Assert.Equal(gameState.InGameIds.Format(ShadowverseBeyond.Server.Domain.Identifiers.InGameIdKind.Player, p1.PlayerId), envelope!.Payload[nameof(GameEventBase.CurrentPlayerId)]!.GetValue<string>());
            Assert.Equal(4, envelope.Payload[nameof(GameEventBase.CurrentPlayerTurnNumber)]!.GetValue<int>());
        }

        [Fact]
        public void MapToOutputRawEventJson_includes_source_ability_snapshot_properties()
        {
            var p1 = new PlayerState(Guid.NewGuid()) { TurnNumber = 2 };
            var p2 = new PlayerState(Guid.NewGuid());
            var target = new CardInstance(Guid.NewGuid(), 1001, p1.PlayerId);
            var source = new CardInstance(Guid.NewGuid(), 1002, p2.PlayerId);
            var abilityRuntimeId = Guid.NewGuid();

            var gameState = new GameState("raw-mapper-ability-snapshot-test", p1, p2, p1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };
            gameState.RegisterCardInstance(target);
            gameState.RegisterCardInstance(source);
            gameState.InGameIds.GetOrCreate(InGameIdKind.Ability, abilityRuntimeId);

            var sourceAbility = new Mock<IAbility>();
            sourceAbility.SetupGet(x => x.RuntimeId).Returns(abilityRuntimeId);
            sourceAbility.SetupGet(x => x.DefinitionId).Returns(3001L);
            sourceAbility.SetupGet(x => x.Name).Returns("Test Ability");
            sourceAbility.SetupGet(x => x.Keywords).Returns(new[] { AbilityKeyword.Fanfare, AbilityKeyword.Enhance });
            sourceAbility.SetupGet(x => x.AbilityKeywords).Returns(new[]
            {
                new AbilityKeywordConfig { Keyword = AbilityKeyword.Fanfare },
                new AbilityKeywordConfig { Keyword = AbilityKeyword.Enhance, Value = 7 }
            });

            var ev = new DamageEvent(target, 3, source);
            using var context = CreateContext(gameState, p1.PlayerId, sourceAbility.Object);
            ev.CaptureCurrentPlayerSnapshot(context);

            var mapper = new JsonGameEventRawEventMapper();
            var json = mapper.MapToOutputRawEventJson(gameState, ev);
            var envelope = JsonSerializer.Deserialize<RawEventEnvelopeDto>(json);

            Assert.NotNull(envelope);
            Assert.Equal(
                gameState.InGameIds.Format(InGameIdKind.Ability, abilityRuntimeId),
                envelope!.Payload[nameof(GameEventBase.SourceAbilityRuntimeId)]!.GetValue<string>());
            Assert.Equal(3001L, envelope.Payload[nameof(GameEventBase.SourceAbilityDefinitionId)]!.GetValue<long>());

            var keywords = envelope.Payload[nameof(GameEventBase.SourceAbilityKeywords)]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
            Assert.Equal(new[] { AbilityKeyword.Fanfare.ToString(), AbilityKeyword.Enhance.ToString() }, keywords);
        }

        [Fact]
        public void MapToOutputRawEventJson_is_guid_free()
        {
            var p1 = new PlayerState(Guid.NewGuid());
            var p2 = new PlayerState(Guid.NewGuid());
            var target = new CardInstance(Guid.NewGuid(), 1001, p1.PlayerId);
            var source = new CardInstance(Guid.NewGuid(), 1002, p2.PlayerId);

            var gameState = new GameState("raw-mapper-test-2", p1, p2, p1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };
            gameState.RegisterCardInstance(target);
            gameState.RegisterCardInstance(source);

            var mapper = new JsonGameEventRawEventMapper();
            var json = mapper.MapToOutputRawEventJson(gameState, new DamageEvent(target, 1, source));

            var node = JsonNode.Parse(json);
            Assert.False(ContainsGuidString(node), json);
        }

        [Fact]
        public void MapToOutputRawEventJson_includes_reveal_payload()
        {
            var p1 = new PlayerState(Guid.NewGuid());
            var p2 = new PlayerState(Guid.NewGuid());

            var revealed = new CardInstance(Guid.NewGuid(), 1001, p1.PlayerId) { CurrentZone = ShadowverseBeyond.Server.Dtos.Enums.CardZone.Hand };

            var gameState = new GameState("raw-mapper-reveal", p1, p2, p1.PlayerId)
            {
                AbilityManager = Mock.Of<IAbilityManager>(),
                BuffManager = Mock.Of<IBuffManager>()
            };
            gameState.RegisterCardInstance(revealed);

            var ev = new CardRevealedEvent(p1, revealed);

            var mapper = new JsonGameEventRawEventMapper();
            var json = mapper.MapToOutputRawEventJson(gameState, ev);
            var envelope = JsonSerializer.Deserialize<RawEventEnvelopeDto>(json);

            Assert.NotNull(envelope);
            Assert.Equal(EventType.CardRevealed.ToString(), envelope!.EventType);

            Assert.True(envelope.Payload.TryGetPropertyValue("RevealedCard", out var revealedCardNode));
            Assert.False(string.IsNullOrWhiteSpace(revealedCardNode!.GetValue<string>()));
        }

        private static bool ContainsGuidString(JsonNode? node)
        {
            if (node == null) return false;

            if (node is JsonValue value)
            {
                try
                {
                    var s = value.GetValue<string?>();
                    return !string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out var g) && g != Guid.Empty;
                }
                catch
                {
                    return false;
                }
            }

            if (node is JsonObject obj)
            {
                foreach (var kv in obj)
                {
                    if (ContainsGuidString(kv.Value)) return true;
                }
                return false;
            }

            if (node is JsonArray arr)
            {
                foreach (var child in arr)
                {
                    if (ContainsGuidString(child)) return true;
                }
                return false;
            }

            return false;
        }

        private static void SetEventSnapshot(GameEventBase gameEvent, Guid currentPlayerId, int currentPlayerTurnNumber)
        {
            var currentPlayerIdProperty = typeof(GameEventBase).GetProperty(
                nameof(GameEventBase.CurrentPlayerId),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            currentPlayerIdProperty!.SetValue(gameEvent, currentPlayerId);

            var currentPlayerTurnProperty = typeof(GameEventBase).GetProperty(
                nameof(GameEventBase.CurrentPlayerTurnNumber),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            currentPlayerTurnProperty!.SetValue(gameEvent, currentPlayerTurnNumber);
        }

        private static GameContext CreateContext(GameState gameState, Guid requestingPlayerId, IAbility sourceAbility)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IGameContextAccessor, GameContextAccessor>();
            services.AddSingleton(Mock.Of<IBuffService>());
            services.AddSingleton(Mock.Of<IEventHistoryService>());

            var serviceProvider = services.BuildServiceProvider();
            var accessor = serviceProvider.GetRequiredService<IGameContextAccessor>();

            return new GameContext(
                serviceProvider,
                Mock.Of<IMapper>(),
                accessor,
                gameState,
                requestingPlayerId)
            {
                SourceAbility = sourceAbility
            };
        }
    }
}
