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
        public void DamageEvent_includes_base_current_player_snapshot_parameters()
        {
            var svc = new EventMetadataService(NullLogger<EventMetadataService>.Instance);
            var damage = svc.GetAllEventMetadata().First(m => m.EventType == EventType.Damage);

            var currentPlayerId = Assert.Single(damage.Parameters.Where(p => p.PropertyName == nameof(GameEventBase.CurrentPlayerId)));
            var currentPlayerTurnNumber = Assert.Single(damage.Parameters.Where(p => p.PropertyName == nameof(GameEventBase.CurrentPlayerTurnNumber)));

            Assert.Equal(GameEventParameterScope.Input, currentPlayerId.Scope);
            Assert.Equal(GameEventParameterScope.Input, currentPlayerTurnNumber.Scope);
        }
    }
}
