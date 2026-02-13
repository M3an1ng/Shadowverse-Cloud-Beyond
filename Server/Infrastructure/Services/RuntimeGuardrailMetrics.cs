using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public static class RuntimeGuardrailMetrics
    {
        private static readonly Meter Meter = new("ShadowverseBeyond.RuntimeGuardrails", "1.0.0");

        public static readonly Counter<long> CannotCreateGameContext =
            Meter.CreateCounter<long>("cannot_create_game_context");

        public static readonly Counter<long> PrivatePayloadToGameGroup =
            Meter.CreateCounter<long>("private_payload_to_game_group");

        public static readonly Counter<long> CrossGameEntityViolation =
            Meter.CreateCounter<long>("cross_game_entity_violation");

        public static void RecordCannotCreateGameContext(string reason, string? gameId, string? methodName)
        {
            CannotCreateGameContext.Add(
                1,
                new KeyValuePair<string, object?>("reason", reason),
                new KeyValuePair<string, object?>("game_id", gameId ?? string.Empty),
                new KeyValuePair<string, object?>("hub_method", methodName ?? string.Empty));
        }

        public static void RecordPrivatePayloadToGameGroup(string gameId, string eventType, string groupName)
        {
            PrivatePayloadToGameGroup.Add(
                1,
                new KeyValuePair<string, object?>("game_id", gameId),
                new KeyValuePair<string, object?>("event_type", eventType),
                new KeyValuePair<string, object?>("group_name", groupName));
        }

        public static void RecordCrossGameEntityViolation(string entityId, string currentGameId, string observedGameId, string eventType)
        {
            CrossGameEntityViolation.Add(
                1,
                new KeyValuePair<string, object?>("entity_id", entityId),
                new KeyValuePair<string, object?>("current_game_id", currentGameId),
                new KeyValuePair<string, object?>("observed_game_id", observedGameId),
                new KeyValuePair<string, object?>("event_type", eventType));
        }
    }
}
