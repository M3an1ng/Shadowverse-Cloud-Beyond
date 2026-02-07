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
using ShadowverseBeyond.Server.Domain.Buffs;

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
    }
}
