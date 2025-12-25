using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Replay;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public sealed class RawEventValidator : IRawEventValidator
    {
        public bool TryValidate(RawEventEnvelopeDto rawEvent, out string? errorMessage)
        {
            errorMessage = null;

            if (rawEvent == null)
            {
                errorMessage = "RawEvent 为空";
                return false;
            }

            if (rawEvent.SchemaVersion <= 0)
            {
                errorMessage = "RawEvent.SchemaVersion 非法";
                return false;
            }

            if (string.IsNullOrWhiteSpace(rawEvent.EventType))
            {
                errorMessage = "RawEvent.EventType 缺失";
                return false;
            }

            if (rawEvent.TimestampUtc == default)
            {
                errorMessage = "RawEvent.TimestampUtc 缺失";
                return false;
            }

            if (rawEvent.TargetIds == null)
            {
                errorMessage = "RawEvent.TargetIds 缺失";
                return false;
            }

            if (rawEvent.TargetIds.Any(string.IsNullOrWhiteSpace))
            {
                errorMessage = "RawEvent.TargetIds 包含非法值";
                return false;
            }

            if (rawEvent.Payload == null)
            {
                errorMessage = "RawEvent.Payload 缺失";
                return false;
            }

            // Validate id formats (best-effort for input; canonical is recommended).
            if (!string.IsNullOrWhiteSpace(rawEvent.EventId))
            {
                if (!InGameId.TryParse(rawEvent.EventId, out var eid) || eid.Kind != InGameIdKind.Event)
                {
                    errorMessage = "RawEvent.EventId 非法（应为 EV-000001）";
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(rawEvent.SourceId))
            {
                if (!InGameId.TryParse(rawEvent.SourceId, out var sid) ||
                    (sid.Kind != InGameIdKind.Player && sid.Kind != InGameIdKind.Card))
                {
                    errorMessage = "RawEvent.SourceId 非法（应为 P-1 或 C-000001）";
                    return false;
                }
            }

            foreach (var tidToken in rawEvent.TargetIds)
            {
                if (!InGameId.TryParse(tidToken, out var tid) ||
                    (tid.Kind != InGameIdKind.Player && tid.Kind != InGameIdKind.Card))
                {
                    errorMessage = "RawEvent.TargetIds 包含非法值（应为 P-1 或 C-000001）";
                    return false;
                }
            }

            if (ContainsGuidString(rawEvent.Payload))
            {
                errorMessage = "RawEvent.Payload 不允许包含 Guid";
                return false;
            }

            return true;
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
