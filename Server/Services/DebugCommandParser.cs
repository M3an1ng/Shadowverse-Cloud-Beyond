using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos.Replay;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Services
{
    public sealed class DebugCommandParser : IDebugCommandParser
    {
        private readonly IRawEventValidator _rawEventValidator;
        private readonly IRawEventGameEventRegistry _rawEventRegistry;

        public DebugCommandParser(
            IRawEventValidator rawEventValidator,
            IRawEventGameEventRegistry rawEventRegistry)
        {
            _rawEventValidator = rawEventValidator ?? throw new ArgumentNullException(nameof(rawEventValidator));
            _rawEventRegistry = rawEventRegistry ?? throw new ArgumentNullException(nameof(rawEventRegistry));
        }

        public DebugCommandParseResult Parse(GameState gameState, Guid requestingPlayerId, DebugCommandDto dto)
        {
            if (gameState == null) throw new ArgumentNullException(nameof(gameState));

            var cmd = (dto?.Command ?? string.Empty).Trim().ToLowerInvariant();
            var args = dto?.Args != null
                ? new Dictionary<string, string>(dto.Args, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            static bool HasUnknownKeys(Dictionary<string, string> dict, HashSet<string> allowedKeys, out string? unknownKey)
            {
                foreach (var key in dict.Keys)
                {
                    if (!allowedKeys.Contains(key))
                    {
                        unknownKey = key;
                        return true;
                    }
                }

                unknownKey = null;
                return false;
            }

            static string? GetNonEmpty(Dictionary<string, string> dict, string key)
            {
                return dict.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val.Trim() : null;
            }

            PlayerState? ResolvePlayerStrict(string? token, bool allowDefault, out string? error)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    if (!allowDefault)
                    {
                        error = "参数缺失或非法";
                        return null;
                    }

                    error = null;
                    return gameState.GetPlayerState(requestingPlayerId);
                }

                if (string.Equals(token, "p1", StringComparison.OrdinalIgnoreCase))
                {
                    error = null;
                    return gameState.Player1;
                }

                if (string.Equals(token, "p2", StringComparison.OrdinalIgnoreCase))
                {
                    error = null;
                    return gameState.Player2;
                }

                if (InGameId.TryParse(token, out var inGameId) &&
                    inGameId.Kind == InGameIdKind.Player &&
                    gameState.InGameIds.TryResolveGuid(InGameIdKind.Player, token, out var pid))
                {
                    var player = gameState.GetPlayerState(pid);
                    if (player != null)
                    {
                        error = null;
                        return player;
                    }
                }

                error = "参数缺失或非法";
                return null;
            }

            CardInstance? ResolveCardStrict(string? token, out string? error)
            {
                if (string.IsNullOrWhiteSpace(token) ||
                    Guid.TryParse(token.Trim(), out _) ||
                    !gameState.InGameIds.TryResolveGuid(InGameIdKind.Card, token, out var cid))
                {
                    error = "参数缺失或非法";
                    return null;
                }

                var card = gameState.FindCardInstance(cid);
                if (card == null)
                {
                    error = "参数缺失或非法";
                    return null;
                }

                error = null;
                return card;
            }

            CardInstance? ResolveSourceCardStrict(string? token, out string? error)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    error = null;
                    return null;
                }

                if (string.Equals(token, "p1", StringComparison.OrdinalIgnoreCase))
                {
                    error = null;
                    return gameState.Player1?.Leader;
                }

                if (string.Equals(token, "p2", StringComparison.OrdinalIgnoreCase))
                {
                    error = null;
                    return gameState.Player2?.Leader;
                }

                if (InGameId.TryParse(token, out var id) && id.Kind == InGameIdKind.Player &&
                    gameState.InGameIds.TryResolveGuid(InGameIdKind.Player, token, out var pid))
                {
                    var player = gameState.GetPlayerState(pid);
                    if (player?.Leader != null)
                    {
                        error = null;
                        return player.Leader;
                    }
                }

                if (Guid.TryParse(token.Trim(), out _))
                {
                    error = "参数缺失或非法";
                    return null;
                }

                if (!gameState.InGameIds.TryResolveGuid(InGameIdKind.Card, token, out var gid))
                {
                    error = "参数缺失或非法";
                    return null;
                }

                var card = gameState.FindCardInstance(gid);
                if (card != null)
                {
                    error = null;
                    return card;
                }

                error = "参数缺失或非法";
                return null;
            }

            DebugCommandParseResult BuildAndCreateSingle(
                string eventType,
                string? sourceId,
                string[] targetIds,
                JsonObject payload,
                string errorPrefix)
            {
                var raw = new RawEventEnvelopeDto(
                    SchemaVersion: 2,
                    EventType: eventType,
                    EventId: null,
                    TimestampUtc: DateTime.UtcNow,
                    SourceId: sourceId,
                    TargetIds: targetIds ?? Array.Empty<string>(),
                    Payload: payload ?? new JsonObject());

                if (!_rawEventValidator.TryValidate(raw, out var err))
                    return DebugCommandParseResult.Fail($"{errorPrefix} {err ?? "参数缺失或非法"}");

                if (!_rawEventRegistry.TryCreateGameEvent(raw, gameState, requestingPlayerId, out var gameEvent, out var createErr) || gameEvent == null)
                    return DebugCommandParseResult.Fail($"{errorPrefix} {createErr ?? "参数缺失或非法"}");

                return DebugCommandParseResult.Ok(new[] { gameEvent });
            }

            DebugCommandParseResult BuildAndCreateMany(
                IReadOnlyList<RawEventEnvelopeDto> raws,
                string errorPrefix)
            {
                var events = new List<IGameEvent>(raws.Count);
                foreach (var raw in raws)
                {
                    if (!_rawEventValidator.TryValidate(raw, out var err))
                        return DebugCommandParseResult.Fail($"{errorPrefix} {err ?? "参数缺失或非法"}");

                    if (!_rawEventRegistry.TryCreateGameEvent(raw, gameState, requestingPlayerId, out var gameEvent, out var createErr) || gameEvent == null)
                        return DebugCommandParseResult.Fail($"{errorPrefix} {createErr ?? "参数缺失或非法"}");

                    events.Add(gameEvent);
                }

                return DebugCommandParseResult.Ok(events);
            }

            int? GetIntStrict(string key, out string? error)
            {
                var raw = GetNonEmpty(args, key);
                if (raw == null || !int.TryParse(raw, out var num))
                {
                    error = "参数缺失或非法";
                    return null;
                }

                error = null;
                return num;
            }

            try
            {
                switch (cmd)
                {
                    case "damage":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "target", "amount", "source" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"damage 参数不支持: {unknown}");

                            var targetToken = GetNonEmpty(args, "target");
                            var amountVal = GetIntStrict("amount", out var amountError);
                            var target = ResolveCardStrict(targetToken, out var targetError);
                            var sourceToken = GetNonEmpty(args, "source");
                            var source = ResolveSourceCardStrict(sourceToken, out var sourceError) ?? target;

                            if (target == null || source == null || amountVal is null || amountVal <= 0)
                                return DebugCommandParseResult.Fail("damage 参数缺失或非法");
                            if (targetError != null || amountError != null || (sourceToken != null && sourceError != null))
                                return DebugCommandParseResult.Fail("damage 参数缺失或非法");

                            return BuildAndCreateSingle(
                                EventType.Damage.ToString(),
                                gameState.InGameIds.Format(InGameIdKind.Card, source.InstanceId),
                                new[] { gameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId) },
                                new JsonObject { ["Damage"] = amountVal.Value },
                                "damage");
                        }
                    case "heal":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "target", "amount", "source" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"heal 参数不支持: {unknown}");

                            var targetToken = GetNonEmpty(args, "target");
                            var amountVal = GetIntStrict("amount", out var amountError);
                            var target = ResolveCardStrict(targetToken, out var targetError);
                            var sourceToken = GetNonEmpty(args, "source");
                            var source = ResolveSourceCardStrict(sourceToken, out var sourceError) ?? target;

                            if (target == null || source == null || amountVal is null || amountVal <= 0)
                                return DebugCommandParseResult.Fail("heal 参数缺失或非法");
                            if (targetError != null || amountError != null || (sourceToken != null && sourceError != null))
                                return DebugCommandParseResult.Fail("heal 参数缺失或非法");

                            return BuildAndCreateSingle(
                                EventType.Heal.ToString(),
                                gameState.InGameIds.Format(InGameIdKind.Card, source.InstanceId),
                                new[] { gameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId) },
                                new JsonObject { ["HealAmount"] = amountVal.Value },
                                "heal");
                        }
                    case "damage-target":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "target", "amount" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"damage-target 参数不支持: {unknown}");

                            var targetToken = GetNonEmpty(args, "target");
                            var amountVal = GetIntStrict("amount", out var amountError);
                            var target = ResolveCardStrict(targetToken, out var targetError);
                            if (target == null || amountVal is null || amountVal <= 0 || targetError != null || amountError != null)
                                return DebugCommandParseResult.Fail("damage-target 参数缺失或非法");
                            return BuildAndCreateSingle(
                                EventType.Damage.ToString(),
                                gameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId),
                                new[] { gameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId) },
                                new JsonObject { ["Damage"] = amountVal.Value },
                                "damage-target");
                        }
                    case "destroy":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "target", "source" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"destroy 参数不支持: {unknown}");

                            var targetToken = GetNonEmpty(args, "target");
                            var target = ResolveCardStrict(targetToken, out var targetError);
                            var sourceToken = GetNonEmpty(args, "source");
                            var source = ResolveSourceCardStrict(sourceToken, out var sourceError) ?? target;
                            if (target == null || source == null)
                                return DebugCommandParseResult.Fail("destroy 参数缺失或非法");
                            if (targetError != null || (sourceToken != null && sourceError != null))
                                return DebugCommandParseResult.Fail("destroy 参数缺失或非法");

                            return BuildAndCreateSingle(
                                EventType.Destroy.ToString(),
                                gameState.InGameIds.Format(InGameIdKind.Card, source.InstanceId),
                                new[] { gameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId) },
                                new JsonObject { ["Reason"] = nameof(EventReasonSource.System) },
                                "destroy");
                        }
                    case "vanish":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "target" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"vanish 参数不支持: {unknown}");

                            var targetToken = GetNonEmpty(args, "target");
                            var target = ResolveCardStrict(targetToken, out var targetError);
                            if (target == null || targetError != null)
                                return DebugCommandParseResult.Fail("vanish 参数缺失或非法");
                            return BuildAndCreateSingle(
                                EventType.CardMoved.ToString(),
                                gameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId),
                                Array.Empty<string>(),
                                new JsonObject { ["ToZone"] = CardZone.Extra.ToString() },
                                "vanish");
                        }
                    case "return":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "target" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"return 参数不支持: {unknown}");

                            var targetToken = GetNonEmpty(args, "target");
                            var target = ResolveCardStrict(targetToken, out var targetError);
                            if (target == null || targetError != null)
                                return DebugCommandParseResult.Fail("return 参数缺失或非法");
                            return BuildAndCreateSingle(
                                EventType.CardMoved.ToString(),
                                gameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId),
                                Array.Empty<string>(),
                                new JsonObject { ["ToZone"] = CardZone.Hand.ToString() },
                                "return");
                        }
                    case "draw":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "player", "count" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"draw 参数不支持: {unknown}");

                            var playerToken = GetNonEmpty(args, "player");
                            var player = ResolvePlayerStrict(playerToken, allowDefault: false, out var playerError);
                            var count = GetIntStrict("count", out var countError);
                            if (player == null || count is null || count <= 0 || playerError != null || countError != null)
                                return DebugCommandParseResult.Fail("draw 参数缺失或非法");
                            return BuildAndCreateSingle(
                                EventType.Draw.ToString(),
                                gameState.InGameIds.Format(InGameIdKind.Player, player.PlayerId),
                                Array.Empty<string>(),
                                new JsonObject { ["Count"] = count.Value },
                                "draw");
                        }
                    case "add-card":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "player", "card", "location" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"add-card 参数不支持: {unknown}");

                            var playerToken = GetNonEmpty(args, "player");
                            var player = ResolvePlayerStrict(playerToken, allowDefault: false, out var playerError);
                            var cardStr = GetNonEmpty(args, "card");
                            var locationStr = GetNonEmpty(args, "location");
                            if (player == null || playerError != null || string.IsNullOrWhiteSpace(cardStr) || string.IsNullOrWhiteSpace(locationStr))
                                return DebugCommandParseResult.Fail("add-card 参数缺失或非法");
                            if (!int.TryParse(cardStr, out var cardDefId))
                                return DebugCommandParseResult.Fail("add-card 参数缺失或非法");

                            var location = locationStr.ToLowerInvariant() switch
                            {
                                "hand" => CardZone.Hand,
                                "board" or "field" => CardZone.Field,
                                "deck" => CardZone.Deck,
                                _ => (CardZone?)null
                            };
                            if (location == null)
                                return DebugCommandParseResult.Fail("add-card 参数缺失或非法");

                            return BuildAndCreateSingle(
                                EventType.CreateCard.ToString(),
                                gameState.InGameIds.Format(InGameIdKind.Player, player.PlayerId),
                                Array.Empty<string>(),
                                new JsonObject
                                {
                                    ["CardDefinitionId"] = cardDefId,
                                    ["Location"] = location.Value.ToString()
                                },
                                "add-card");
                        }
                    case "mana":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "player", "delta" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"mana 参数不支持: {unknown}");

                            var playerToken = GetNonEmpty(args, "player");
                            var player = ResolvePlayerStrict(playerToken, allowDefault: false, out var playerError);
                            var delta = GetIntStrict("delta", out var deltaError);
                            if (player == null || playerError != null || delta is null || deltaError != null)
                                return DebugCommandParseResult.Fail("mana 参数缺失或非法");
                            var targetCurrent = Math.Max(0, Math.Min(player.CurrentPlayPoints + delta.Value, player.MaxPlayPoints));
                            return BuildAndCreateSingle(
                                EventType.PpUpdated.ToString(),
                                gameState.InGameIds.Format(InGameIdKind.Player, player.PlayerId),
                                Array.Empty<string>(),
                                new JsonObject { ["CurrentPp"] = targetCurrent },
                                "mana");
                        }
                    case "pp":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "player", "current", "max" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"pp 参数不支持: {unknown}");

                            var playerToken = GetNonEmpty(args, "player");
                            var player = ResolvePlayerStrict(playerToken, allowDefault: false, out var playerError);
                            if (player == null || playerError != null) return DebugCommandParseResult.Fail("pp 参数缺失或非法");

                            var current = GetIntStrict("current", out var currentError);
                            if (current is null || currentError != null) return DebugCommandParseResult.Fail("pp 参数缺失或非法");

                            var maxRaw = GetNonEmpty(args, "max");
                            int? max = null;
                            if (maxRaw != null)
                            {
                                if (!int.TryParse(maxRaw, out var maxNum)) return DebugCommandParseResult.Fail("pp 参数缺失或非法");
                                max = maxNum;
                            }

                            var raws = new List<RawEventEnvelopeDto>(2);
                            if (max.HasValue && max.Value > 0)
                            {
                                raws.Add(new RawEventEnvelopeDto(
                                    2,
                                    EventType.PpMaxUpdated.ToString(),
                                    null,
                                    DateTime.UtcNow,
                                    gameState.InGameIds.Format(InGameIdKind.Player, player.PlayerId),
                                    Array.Empty<string>(),
                                    new JsonObject { ["MaxPp"] = max.Value }));
                            }

                            raws.Add(new RawEventEnvelopeDto(
                                2,
                                EventType.PpUpdated.ToString(),
                                null,
                                DateTime.UtcNow,
                                gameState.InGameIds.Format(InGameIdKind.Player, player.PlayerId),
                                Array.Empty<string>(),
                                new JsonObject { ["CurrentPp"] = current.Value }));

                            return BuildAndCreateMany(raws, "pp");
                        }
                    case "ep":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "player", "current", "max" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"ep 参数不支持: {unknown}");

                            var playerToken = GetNonEmpty(args, "player");
                            var player = ResolvePlayerStrict(playerToken, allowDefault: false, out var playerError);
                            if (player == null || playerError != null) return DebugCommandParseResult.Fail("ep 参数缺失或非法");

                            var current = GetIntStrict("current", out var currentError);
                            if (current is null || currentError != null) return DebugCommandParseResult.Fail("ep 参数缺失或非法");

                            var maxRaw = GetNonEmpty(args, "max");
                            int? max = null;
                            if (maxRaw != null)
                            {
                                if (!int.TryParse(maxRaw, out var maxNum)) return DebugCommandParseResult.Fail("ep 参数缺失或非法");
                                max = maxNum;
                            }

                            var raws = new List<RawEventEnvelopeDto>(2);
                            if (max.HasValue && max.Value > 0)
                            {
                                raws.Add(new RawEventEnvelopeDto(
                                    2,
                                    EventType.EpMaxUpdated.ToString(),
                                    null,
                                    DateTime.UtcNow,
                                    gameState.InGameIds.Format(InGameIdKind.Player, player.PlayerId),
                                    Array.Empty<string>(),
                                    new JsonObject { ["MaxEp"] = max.Value }));
                            }

                            raws.Add(new RawEventEnvelopeDto(
                                2,
                                EventType.EpUpdated.ToString(),
                                null,
                                DateTime.UtcNow,
                                gameState.InGameIds.Format(InGameIdKind.Player, player.PlayerId),
                                Array.Empty<string>(),
                                new JsonObject { ["CurrentEp"] = current.Value }));

                            return BuildAndCreateMany(raws, "ep");
                        }
                    case "event":
                        {
                            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name", "type", "source", "target", "targets", "payload" };
                            if (HasUnknownKeys(args, allowed, out var unknown))
                                return DebugCommandParseResult.Fail($"event 参数不支持: {unknown}");
                            var name = GetNonEmpty(args, "name") ?? GetNonEmpty(args, "type");
                            if (name == null) return DebugCommandParseResult.Fail("event 参数缺失或非法");

                            string? sourceId = null;
                            var sourceToken = GetNonEmpty(args, "source");
                            if (!string.IsNullOrWhiteSpace(sourceToken))
                            {
                                var src = ResolveSourceCardStrict(sourceToken, out var sourceError);
                                if (src == null || sourceError != null) return DebugCommandParseResult.Fail("event 参数缺失或非法");
                                sourceId = gameState.InGameIds.Format(InGameIdKind.Card, src.InstanceId);
                            }

                            string[] targetIds = Array.Empty<string>();
                            var targetToken = GetNonEmpty(args, "target");
                            var targetsToken = GetNonEmpty(args, "targets");
                            if (!string.IsNullOrWhiteSpace(targetToken))
                            {
                                var target = ResolveCardStrict(targetToken, out var targetError);
                                if (target == null || targetError != null) return DebugCommandParseResult.Fail("event 参数缺失或非法");
                                targetIds = new[] { gameState.InGameIds.Format(InGameIdKind.Card, target.InstanceId) };
                            }
                            else if (!string.IsNullOrWhiteSpace(targetsToken))
                            {
                                var parts = targetsToken.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                var list = new List<string>(parts.Length);
                                foreach (var part in parts)
                                {
                                    var card = ResolveCardStrict(part, out var targetError);
                                    if (card == null || targetError != null) return DebugCommandParseResult.Fail("event 参数缺失或非法");
                                    list.Add(gameState.InGameIds.Format(InGameIdKind.Card, card.InstanceId));
                                }
                                targetIds = list.ToArray();
                            }

                            var payloadStr = GetNonEmpty(args, "payload");
                            JsonObject payload = new();
                            if (!string.IsNullOrWhiteSpace(payloadStr))
                            {
                                try
                                {
                                    var parsed = JsonNode.Parse(payloadStr);
                                    if (parsed is not JsonObject obj)
                                        return DebugCommandParseResult.Fail("event payload 必须是 JSON 对象");
                                    payload = obj;
                                }
                                catch
                                {
                                    return DebugCommandParseResult.Fail("event payload JSON 解析失败");
                                }
                            }

                            return BuildAndCreateSingle(name, sourceId, targetIds, payload, "event");
                        }
                    default:
                        return DebugCommandParseResult.Fail($"未知指令: {cmd}");
                }
            }
            catch (Exception ex)
            {
                return DebugCommandParseResult.Fail($"DebugCommand 解析失败: {ex.Message}");
            }
        }
    }
}
