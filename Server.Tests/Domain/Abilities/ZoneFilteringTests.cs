using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities.Triggers;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Tests.Domain.Abilities
{
    /// <summary>
    /// 测试区域过滤功能的单元测试
    /// </summary>
    public class ZoneFilteringTests
    {
        [Fact]
        public void TriggerConfig_IsValidZone_ShouldReturnTrueForValidZones()
        {
            // Arrange
            var config = new TriggerConfig();
            config.ValidZones = new List<CardZone> { CardZone.Field, CardZone.Hand };

            // Act & Assert
            Assert.True(config.IsValidZone(CardZone.Field));
            Assert.True(config.IsValidZone(CardZone.Hand));
            Assert.False(config.IsValidZone(CardZone.Deck));
            Assert.False(config.IsValidZone(CardZone.Graveyard));
        }

        [Fact]
        public void TriggerConfig_IsValidZone_ShouldReturnTrueForEmptyValidZones()
        {
            // Arrange
            var config = new TriggerConfig();
            config.ValidZones = new List<CardZone>(); // Empty list means all zones are valid

            // Act & Assert
            Assert.True(config.IsValidZone(CardZone.Field));
            Assert.True(config.IsValidZone(CardZone.Hand));
            Assert.True(config.IsValidZone(CardZone.Deck));
            Assert.True(config.IsValidZone(CardZone.Graveyard));
        }

        [Fact]
        public void TriggerConfig_WithZone_ShouldSetSingleZone()
        {
            // Arrange
            var config = new TriggerConfig();

            // Act
            config.WithZone(CardZone.Field);

            // Assert
            Assert.Single(config.ValidZones);
            Assert.Contains(CardZone.Field, config.ValidZones);
        }

        [Fact]
        public void TriggerConfig_WithZones_ShouldSetMultipleZones()
        {
            // Arrange
            var config = new TriggerConfig();

            // Act
            config.WithZones(CardZone.Field, CardZone.Hand, CardZone.Graveyard);

            // Assert
            Assert.Equal(3, config.ValidZones.Count);
            Assert.Contains(CardZone.Field, config.ValidZones);
            Assert.Contains(CardZone.Hand, config.ValidZones);
            Assert.Contains(CardZone.Graveyard, config.ValidZones);
        }

        [Fact]
        public void TriggerConfig_WithAllZones_ShouldClearValidZones()
        {
            // Arrange
            var config = new TriggerConfig();
            config.ValidZones = new List<CardZone> { CardZone.Field };

            // Act
            config.WithAllZones();

            // Assert
            Assert.Empty(config.ValidZones);
            Assert.True(config.IsValidZone(CardZone.Deck)); // Should allow all zones
        }

        [Fact]
        public void RuntimeTrigger_CanTrigger_ShouldRespectZoneRestrictions()
        {
            // Arrange
            var config = new TriggerConfig
            {
                EventTypes = new List<GameEventType> { GameEventType.PlayCard },
                ValidZones = new List<CardZone> { CardZone.Field }
            };
            var trigger = new Runtime.Trigger(config);

            var cardInField = new CardInstance { CurrentZone = CardZone.Field };
            var cardInHand = new CardInstance { CurrentZone = CardZone.Hand };

            var gameEvent = new TestGameEvent("play_card");

            // Act & Assert
            Assert.True(trigger.CanTrigger(gameEvent, cardInField));
            Assert.False(trigger.CanTrigger(gameEvent, cardInHand));
        }

        [Fact]
        public void ZoneFilteringHelper_IsActiveZone_ShouldIdentifyActiveZones()
        {
            // Act & Assert
            Assert.True(ZoneFilteringHelper.IsActiveZone(CardZone.Field));
            Assert.True(ZoneFilteringHelper.IsActiveZone(CardZone.Leader));
            Assert.True(ZoneFilteringHelper.IsActiveZone(CardZone.Playing));

            Assert.False(ZoneFilteringHelper.IsActiveZone(CardZone.Hand));
            Assert.False(ZoneFilteringHelper.IsActiveZone(CardZone.Deck));
            Assert.False(ZoneFilteringHelper.IsActiveZone(CardZone.Graveyard));
        }

        [Fact]
        public void ZoneFilteringHelper_GetZonePriority_ShouldReturnCorrectPriorities()
        {
            // Act & Assert
            Assert.Equal(1, ZoneFilteringHelper.GetZonePriority(CardZone.Field));
            Assert.Equal(2, ZoneFilteringHelper.GetZonePriority(CardZone.Leader));
            Assert.True(ZoneFilteringHelper.GetZonePriority(CardZone.Hand) > ZoneFilteringHelper.GetZonePriority(CardZone.Field));
            Assert.True(ZoneFilteringHelper.GetZonePriority(CardZone.Deck) > ZoneFilteringHelper.GetZonePriority(CardZone.Hand));
        }

        [Fact]
        public void TriggerConfigExtensions_IsActiveZoneOnly_ShouldDetectActiveZoneOptimization()
        {
            // Arrange
            var activeZoneConfig = new TriggerConfig();
            activeZoneConfig.ValidZones = new List<CardZone> { CardZone.Field, CardZone.Leader };

            var mixedZoneConfig = new TriggerConfig();
            mixedZoneConfig.ValidZones = new List<CardZone> { CardZone.Field, CardZone.Hand };

            var allZoneConfig = new TriggerConfig();
            allZoneConfig.ValidZones = new List<CardZone>();

            // Act & Assert
            Assert.True(activeZoneConfig.IsActiveZoneOnly());
            Assert.False(mixedZoneConfig.IsActiveZoneOnly());
            Assert.False(allZoneConfig.IsActiveZoneOnly());
        }

        [Fact]
        public void TriggerConfigExtensions_GetPerformanceInfo_ShouldProvideCorrectAnalysis()
        {
            // Arrange
            var optimizedConfig = new TriggerConfig();
            optimizedConfig.ValidZones = new List<CardZone> { CardZone.Field };

            var unoptimizedConfig = new TriggerConfig();
            unoptimizedConfig.ValidZones = new List<CardZone>();

            // Act
            var optimizedInfo = optimizedConfig.GetPerformanceInfo();
            var unoptimizedInfo = unoptimizedConfig.GetPerformanceInfo();

            // Assert
            Assert.True(optimizedInfo.IsOptimized);
            Assert.False(unoptimizedInfo.IsOptimized);
            Assert.True(optimizedInfo.GetPerformanceScore() > unoptimizedInfo.GetPerformanceScore());
        }

        [Fact]
        public void TriggerConfig_BackwardCompatibility_ShouldWorkWithObsoleteProperty()
        {
            // Arrange
            var config = new TriggerConfig();

            // Act
#pragma warning disable CS0618 // Type or member is obsolete
            config.CardZone = CardZone.Hand;
            var retrievedZone = config.CardZone;
#pragma warning restore CS0618 // Type or member is obsolete

            // Assert
            Assert.Equal(CardZone.Hand, retrievedZone);
            Assert.Single(config.ValidZones);
            Assert.Contains(CardZone.Hand, config.ValidZones);
        }
    }

    /// <summary>
    /// 简单的测试游戏事件实现
    /// </summary>
    public class TestGameEvent : IGameEvent
    {
        public int EventId { get; set; } = Random.Shared.Next(1000, 9999);
        public string EventType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public PlayerStateDto? SourcePlayer { get; set; }
        public CardInstance? SourceCard { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();

        public TestGameEvent(string eventType)
        {
            EventType = eventType;
        }

        public Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            return Task.FromResult(new List<IGameEvent>());
        }
    }
}
