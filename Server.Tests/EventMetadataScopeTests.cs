using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using ShadowverseBeyond.Server.Services;
using System.Linq;
using Xunit;

namespace ShadowverseBeyond.Server.Tests
{
    public class EventMetadataScopeTests
    {
        [Fact]
        public void DrawEvent_parameters_expose_scopes()
        {
            var svc = new EventMetadataService(NullLogger<EventMetadataService>.Instance);
            var draw = svc.GetAllEventMetadata().First(m => m.EventType == EventType.Draw);

            var drawingPlayer = draw.Parameters.First(p => p.PropertyName == "DrawingPlayer");
            Assert.Equal(GameEventParameterScope.Input, drawingPlayer.Scope);

            var drawnCards = draw.Parameters.First(p => p.PropertyName == "DrawnCards");
            Assert.Equal(GameEventParameterScope.Output, drawnCards.Scope);
        }

        [Fact]
        public void MulliganSubmitted_parameters_expose_scopes()
        {
            var svc = new EventMetadataService(NullLogger<EventMetadataService>.Instance);
            var mulligan = svc.GetAllEventMetadata().First(m => m.EventType == EventType.MulliganSubmitted);

            var playerId = mulligan.Parameters.First(p => p.PropertyName == "PlayerId");
            Assert.Equal(GameEventParameterScope.Input, playerId.Scope);
        }

        [Fact]
        public void Event_metadata_exposes_category_and_visibility()
        {
            var svc = new EventMetadataService(NullLogger<EventMetadataService>.Instance);
            var draw = svc.GetAllEventMetadata().First(m => m.EventType == EventType.Draw);

            Assert.Equal(AbilityEditorCategories.PositionChangeId, draw.CategoryId);
            Assert.Equal(AbilityEditorCategories.PositionChangeLabel, draw.CategoryLabel);
            Assert.Equal(EventMetadataVisibility.Visible, draw.Visibility);
        }

        [Fact]
        public void Action_request_events_are_marked_as_system()
        {
            var svc = new EventMetadataService(NullLogger<EventMetadataService>.Instance);
            var mulligan = svc.GetAllEventMetadata().First(m => m.EventType == EventType.MulliganSubmitted);

            Assert.Equal(AbilityEditorCategories.SystemRequestId, mulligan.CategoryId);
            Assert.Equal(EventMetadataVisibility.System, mulligan.Visibility);
        }

        [Fact]
        public void DamageEvent_hides_base_snapshot_parameters_from_trigger_tips()
        {
            var svc = new EventMetadataService(NullLogger<EventMetadataService>.Instance);
            var damage = svc.GetAllEventMetadata().First(m => m.EventType == EventType.Damage);

            Assert.DoesNotContain(damage.Parameters, p => p.PropertyName == nameof(GameEventBase.CurrentPlayerId));
            Assert.DoesNotContain(damage.Parameters, p => p.PropertyName == nameof(GameEventBase.CurrentPlayerTurnNumber));
            Assert.DoesNotContain(damage.Parameters, p => p.PropertyName == nameof(GameEventBase.SourceAbilityRuntimeId));
            Assert.DoesNotContain(damage.Parameters, p => p.PropertyName == nameof(GameEventBase.SourceAbilityDefinitionId));
            Assert.DoesNotContain(damage.Parameters, p => p.PropertyName == nameof(GameEventBase.SourceAbilityKeywords));
        }

        [Fact]
        public void CounterChangedEvent_exposes_calculated_source_and_target_parameters()
        {
            var svc = new EventMetadataService(NullLogger<EventMetadataService>.Instance);
            var counterChanged = svc.GetAllEventMetadata().First(m => m.EventType == EventType.CounterChanged);

            var sourcePlayer = counterChanged.Parameters.First(p => p.PropertyName == "SourcePlayer");
            Assert.Equal(GameEventParameterScope.Output, sourcePlayer.Scope);
            Assert.Equal("事件源", sourcePlayer.DslDisplayTypeName);

            var targetCard = counterChanged.Parameters.First(p => p.PropertyName == "TargetCard");
            Assert.Equal(GameEventParameterScope.Output, targetCard.Scope);
            Assert.Equal("事件目标", targetCard.DslDisplayTypeName);
        }

        [Fact]
        public void Snapshot_parameters_are_outputs()
        {
            var svc = new EventMetadataService(NullLogger<EventMetadataService>.Instance);

            AssertSnapshotOutput(EventType.LeaveField, "LeaveTargetSnapshot");
            AssertSnapshotOutput(EventType.LeaveField, "LeaveSourceSnapshot");
            AssertSnapshotOutput(EventType.Destroy, "DestroyTargetSnapshot");
            AssertSnapshotOutput(EventType.Destroy, "DestroySourceSnapshot");
            AssertSnapshotOutput(EventType.Vanish, "VanishedCardSnapshot");
            AssertSnapshotOutput(EventType.Vanish, "VanishSourceSnapshot");
            AssertSnapshotOutput(EventType.Return, "ReturnedCardSnapshot");
            AssertSnapshotOutput(EventType.Return, "ReturnSourceSnapshot");
            AssertSnapshotOutput(EventType.Tuck, "TuckedCardSnapshot");
            AssertSnapshotOutput(EventType.Tuck, "TuckSourceSnapshot");
            AssertSnapshotOutput(EventType.CardMoved, "MovedCardSnapshot");
            AssertSnapshotOutput(EventType.CardMoved, "MoveSourceSnapshot");
            AssertSnapshotOutput(EventType.Transform, "TransformingCardSnapshot");
            AssertSnapshotOutput(EventType.Transform, "TransformSourceSnapshot");

            void AssertSnapshotOutput(EventType eventType, string propertyName)
            {
                var metadata = svc.GetAllEventMetadata().First(m => m.EventType == eventType);
                var parameter = metadata.Parameters.First(p => p.PropertyName == propertyName);
                Assert.Equal(GameEventParameterScope.Output, parameter.Scope);
            }
        }

        [Fact]
        public void EffectCompleted_generated_card_snapshots_remain_input()
        {
            var svc = new EventMetadataService(NullLogger<EventMetadataService>.Instance);
            var effectCompleted = svc.GetAllEventMetadata().First(m => m.EventType == EventType.EffectCompleted);

            var snapshots = effectCompleted.Parameters.First(p => p.PropertyName == "GeneratedCardSnapshots");
            Assert.Equal(GameEventParameterScope.Input, snapshots.Scope);
        }
    }
}
