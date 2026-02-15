using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Dtos.Replay;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public sealed class RawEventGameEventRegistry : IRawEventGameEventRegistry
    {
        private readonly IReadOnlyDictionary<string, IFactory> _factories;

        public RawEventGameEventRegistry()
        {
            _factories = new Dictionary<string, IFactory>(StringComparer.OrdinalIgnoreCase)
            {
                [EventType.Damage.ToString()] = new DamageFactory(),
                [EventType.Heal.ToString()] = new HealFactory(),
                [EventType.Draw.ToString()] = new DrawFactory(),
                [EventType.CardMoved.ToString()] = new CardMovedFactory(),
                [EventType.Destroy.ToString()] = new DestroyFactory(),
                [EventType.CreateCard.ToString()] = new CreateCardFactory(),
                [EventType.PpUpdated.ToString()] = new PpUpdateFactory(),
                [EventType.PpMaxUpdated.ToString()] = new PpMaxUpdateFactory(),
                [EventType.EpUpdated.ToString()] = new EpUpdateFactory(),
                [EventType.EpMaxUpdated.ToString()] = new EpMaxUpdateFactory()
            };
        }

        public bool TryCreateGameEvent(
            RawEventEnvelopeDto input,
            GameState gameState,
            Guid requestingPlayerId,
            out IGameEvent? gameEvent,
            out string? errorMessage)
        {
            gameEvent = null;
            errorMessage = null;

            if (input == null)
            {
                errorMessage = "RawEvent 为空";
                return false;
            }

            if (gameState == null)
            {
                errorMessage = "GameState 为空";
                return false;
            }

            if (!_factories.TryGetValue(input.EventType, out var factory))
            {
                errorMessage = $"未知事件类型: {input.EventType}";
                return false;
            }

            return factory.TryCreate(input, gameState, requestingPlayerId, out gameEvent, out errorMessage);
        }

        private interface IFactory
        {
            bool TryCreate(
                RawEventEnvelopeDto input,
                GameState gameState,
                Guid requestingPlayerId,
                out IGameEvent? gameEvent,
                out string? errorMessage);
        }

        private static class Resolver
        {
            public static bool TryResolveCardOrLeader(GameState gameState, string token, out CardInstance? card)
            {
                card = null;
                if (string.IsNullOrWhiteSpace(token)) return false;

                var trimmed = token.Trim();
                if (InGameId.TryParse(trimmed, out var id) && id.Kind == InGameIdKind.Player)
                {
                    if (!gameState.InGameIds.TryResolveGuid(InGameIdKind.Player, trimmed, out var playerGuid) || playerGuid == Guid.Empty)
                        return false;

                    if (playerGuid == gameState.Player1.PlayerId)
                    {
                        card = gameState.Player1.Leader;
                        return card != null;
                    }

                    if (playerGuid == gameState.Player2.PlayerId)
                    {
                        card = gameState.Player2.Leader;
                        return card != null;
                    }
                }

                if (!TryResolveGuid(gameState, trimmed, out var instanceId)) return false;
                if (instanceId == Guid.Empty) return false;

                if (instanceId == gameState.Player1.PlayerId)
                {
                    card = gameState.Player1.Leader;
                    return card != null;
                }

                if (instanceId == gameState.Player2.PlayerId)
                {
                    card = gameState.Player2.Leader;
                    return card != null;
                }

                card = gameState.FindCardInstance(instanceId);
                return card != null;
            }

            public static bool TryResolvePlayer(GameState gameState, string token, out PlayerState? player)
            {
                player = null;
                if (string.IsNullOrWhiteSpace(token)) return false;
                if (!TryResolveGuid(gameState, token.Trim(), out var playerId)) return false;

                player = gameState.GetPlayerState(playerId);
                return player != null;
            }

            private static bool TryResolveGuid(GameState gameState, string token, out Guid guid)
            {
                guid = Guid.Empty;
                if (string.IsNullOrWhiteSpace(token)) return false;

                if (Guid.TryParse(token, out guid)) return guid != Guid.Empty;

                if (!InGameId.TryParse(token, out var inGameId)) return false;
                return gameState.InGameIds.TryResolveGuid(inGameId.Kind, token, out guid) && guid != Guid.Empty;
            }
        }

        private static class Payload
        {
            public static bool HasUnknownKeys(JsonObject payload, ISet<string> allowedKeys, out string? unknownKey)
            {
                foreach (var key in payload.Select(kv => kv.Key))
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

            public static bool TryGetInt(JsonObject payload, string key, out int value)
            {
                value = default;
                if (!TryGetNode(payload, key, out var node)) return false;

                if (node is JsonValue jv)
                {
                    try { value = jv.GetValue<int>(); return true; } catch { }
                    try { value = (int)jv.GetValue<double>(); return true; } catch { }
                    try
                    {
                        var s = jv.GetValue<string>();
                        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
                    }
                    catch { return false; }
                }

                return false;
            }

            public static bool TryGetString(JsonObject payload, string key, out string? value)
            {
                value = null;
                if (!TryGetNode(payload, key, out var node)) return false;
                if (node is not JsonValue jv) return false;
                try { value = jv.GetValue<string>(); return true; } catch { return false; }
            }

            public static bool TryGetOptionalBool(JsonObject payload, string key, out bool value, out bool found)
            {
                value = default;
                found = false;
                if (!TryGetNode(payload, key, out var node)) return true;
                found = true;
                if (node is not JsonValue jv) return false;
                try { value = jv.GetValue<bool>(); return true; } catch { }
                try
                {
                    var s = jv.GetValue<string>();
                    if (bool.TryParse(s, out var parsed))
                    {
                        value = parsed;
                        return true;
                    }
                }
                catch { }
                try { value = jv.GetValue<int>() != 0; return true; } catch { }
                try { value = Math.Abs(jv.GetValue<double>()) > double.Epsilon; return true; } catch { }
                return false;
            }

            private static bool TryGetNode(JsonObject payload, string key, out JsonNode? node)
            {
                node = null;
                foreach (var kv in payload)
                {
                    if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
                    node = kv.Value;
                    return true;
                }

                return false;
            }
        }

        private sealed class DamageFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "Damage", "Amount"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "damage 缺少 sourceId";
                    return false;
                }

                if (input.TargetIds.Length != 1)
                {
                    errorMessage = "damage 需要且仅需要 1 个 targetId";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"damage payload 不支持字段: {unknown}";
                    return false;
                }

                if (!Payload.TryGetInt(input.Payload, "Damage", out var dmg) && !Payload.TryGetInt(input.Payload, "Amount", out dmg))
                {
                    errorMessage = "damage payload 缺少 Damage/Amount";
                    return false;
                }

                if (dmg <= 0)
                {
                    errorMessage = "damage Damage 必须大于 0";
                    return false;
                }

                if (!Resolver.TryResolveCardOrLeader(gameState, input.SourceId, out var source) || source == null)
                {
                    errorMessage = "damage sourceId 无法解析为卡牌/主战者";
                    return false;
                }

                if (!Resolver.TryResolveCardOrLeader(gameState, input.TargetIds[0], out var target) || target == null)
                {
                    errorMessage = "damage targetId 无法解析为卡牌/主战者";
                    return false;
                }

                gameEvent = new DamageEvent(target, dmg, source);
                return true;
            }
        }

        private sealed class HealFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "HealAmount", "Amount"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "heal 缺少 sourceId";
                    return false;
                }

                if (input.TargetIds.Length != 1)
                {
                    errorMessage = "heal 需要且仅需要 1 个 targetId";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"heal payload 不支持字段: {unknown}";
                    return false;
                }

                if (!Payload.TryGetInt(input.Payload, "HealAmount", out var amount) && !Payload.TryGetInt(input.Payload, "Amount", out amount))
                {
                    errorMessage = "heal payload 缺少 HealAmount/Amount";
                    return false;
                }

                if (amount <= 0)
                {
                    errorMessage = "heal HealAmount 必须大于 0";
                    return false;
                }

                if (!Resolver.TryResolveCardOrLeader(gameState, input.SourceId, out var source) || source == null)
                {
                    errorMessage = "heal sourceId 无法解析为卡牌/主战者";
                    return false;
                }

                if (!Resolver.TryResolveCardOrLeader(gameState, input.TargetIds[0], out var target) || target == null)
                {
                    errorMessage = "heal targetId 无法解析为卡牌/主战者";
                    return false;
                }

                gameEvent = new HealEvent(target, amount, source);
                return true;
            }
        }

        private sealed class DrawFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "Count",
                "DrawnCards"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "draw 缺少 sourceId（玩家ID）";
                    return false;
                }

                if (input.TargetIds.Length != 0)
                {
                    errorMessage = "draw 不需要 targetIds";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"draw payload 不支持字段: {unknown}";
                    return false;
                }

                var hasCount = Payload.TryGetInt(input.Payload, "Count", out var count);
                if (!hasCount)
                {
                    if (input.Payload.TryGetPropertyValue("DrawnCards", out var drawnCardsNode)
                        && drawnCardsNode is JsonArray drawnCardsArray)
                    {
                        count = drawnCardsArray.Count;
                        hasCount = true;
                    }
                }

                if (!hasCount || count <= 0)
                {
                    errorMessage = "draw payload 缺少 Count 或 Count 非法";
                    return false;
                }

                if (!Resolver.TryResolvePlayer(gameState, input.SourceId, out var player) || player == null)
                {
                    errorMessage = "draw sourceId 无法解析为玩家";
                    return false;
                }

                gameEvent = new DrawEvent(player, count);
                return true;
            }
        }

        private sealed class DestroyFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "Reason"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "destroy 缺少 sourceId";
                    return false;
                }

                if (input.TargetIds.Length != 1)
                {
                    errorMessage = "destroy 需要且仅需要 1 个 targetId";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"destroy payload 不支持字段: {unknown}";
                    return false;
                }

                var reason = EventReasonSource.System;
                if (Payload.TryGetString(input.Payload, "Reason", out var reasonVal) && !string.IsNullOrWhiteSpace(reasonVal))
                {
                    if (!TryParseReason(reasonVal!, out reason))
                    {
                        errorMessage = "destroy Reason 仅支持 System 或 Ability（兼容值：Effect）";
                        return false;
                    }
                }

                if (!Resolver.TryResolveCardOrLeader(gameState, input.SourceId, out var source) || source == null)
                {
                    errorMessage = "destroy sourceId 无法解析为卡牌/主战者";
                    return false;
                }

                if (!Resolver.TryResolveCardOrLeader(gameState, input.TargetIds[0], out var target) || target == null)
                {
                    errorMessage = "destroy targetId 无法解析为卡牌/主战者";
                    return false;
                }

                gameEvent = new DestroyEvent(target, source, reason);
                return true;
            }

            private static bool TryParseReason(string rawReason, out EventReasonSource reason)
            {
                if (string.Equals(rawReason, "Effect", StringComparison.OrdinalIgnoreCase))
                {
                    reason = EventReasonSource.Ability;
                    return true;
                }

                return Enum.TryParse(rawReason, true, out reason);
            }
        }

        private sealed class CreateCardFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "CardDefinitionId"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "add-card 缺少 sourceId（玩家ID）";
                    return false;
                }

                if (input.TargetIds.Length != 0)
                {
                    errorMessage = "add-card 不需要 targetIds";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"add-card payload 不支持字段: {unknown}";
                    return false;
                }

                if (!Payload.TryGetInt(input.Payload, "CardDefinitionId", out var cardDefinitionId))
                {
                    errorMessage = "add-card payload 缺少 CardDefinitionId";
                    return false;
                }

                if (!Resolver.TryResolvePlayer(gameState, input.SourceId, out var player) || player == null)
                {
                    errorMessage = "add-card sourceId 无法解析为玩家";
                    return false;
                }

                gameEvent = new CreateCardEvent(player, cardDefinitionId);
                return true;
            }
        }

        private sealed class CardMovedFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "ToZone"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "card-moved 缺少 sourceId";
                    return false;
                }

                if (input.TargetIds.Length != 0)
                {
                    errorMessage = "card-moved 不需要 targetIds（使用 sourceId 指定卡牌）";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"card-moved payload 不支持字段: {unknown}";
                    return false;
                }

                if (!Payload.TryGetString(input.Payload, "ToZone", out var zoneStr) || string.IsNullOrWhiteSpace(zoneStr))
                {
                    errorMessage = "card-moved payload 缺少 ToZone";
                    return false;
                }

                if (!Enum.TryParse<CardZone>(zoneStr, ignoreCase: true, out var toZone))
                {
                    errorMessage = "card-moved ToZone 非法";
                    return false;
                }

                if (!Resolver.TryResolveCardOrLeader(gameState, input.SourceId, out var card) || card == null)
                {
                    errorMessage = "card-moved sourceId 无法解析为卡牌/主战者";
                    return false;
                }

                var fromZone = card.CurrentZone;
                gameEvent = new CardMovedEvent(card, fromZone, toZone);
                return true;
            }
        }

        private sealed class PpUpdateFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "CurrentPp", "Current", "AllowOverflow"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "pp 缺少 sourceId（玩家ID）";
                    return false;
                }

                if (input.TargetIds.Length != 0)
                {
                    errorMessage = "pp 不需要 targetIds";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"pp payload 不支持字段: {unknown}";
                    return false;
                }

                if (!Payload.TryGetInt(input.Payload, "CurrentPp", out var current) && !Payload.TryGetInt(input.Payload, "Current", out current))
                {
                    errorMessage = "pp payload 缺少 CurrentPp/Current";
                    return false;
                }

                var allowOverflow = false;
                if (!Payload.TryGetOptionalBool(input.Payload, "AllowOverflow", out allowOverflow, out _))
                {
                    errorMessage = "pp payload AllowOverflow 非法";
                    return false;
                }

                if (!Resolver.TryResolvePlayer(gameState, input.SourceId, out var player) || player == null)
                {
                    errorMessage = "pp sourceId 无法解析为玩家";
                    return false;
                }

                gameEvent = new PpUpdateEvent(player, current, allowOverflow);
                return true;
            }
        }

        private sealed class PpMaxUpdateFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "MaxPp", "Max"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "pp-max 缺少 sourceId（玩家ID）";
                    return false;
                }

                if (input.TargetIds.Length != 0)
                {
                    errorMessage = "pp-max 不需要 targetIds";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"pp-max payload 不支持字段: {unknown}";
                    return false;
                }

                if (!Payload.TryGetInt(input.Payload, "MaxPp", out var max) && !Payload.TryGetInt(input.Payload, "Max", out max))
                {
                    errorMessage = "pp-max payload 缺少 MaxPp/Max";
                    return false;
                }

                if (!Resolver.TryResolvePlayer(gameState, input.SourceId, out var player) || player == null)
                {
                    errorMessage = "pp-max sourceId 无法解析为玩家";
                    return false;
                }

                gameEvent = new PpMaxUpdateEvent(player, max);
                return true;
            }
        }

        private sealed class EpUpdateFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "CurrentEp", "Current"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "ep 缺少 sourceId（玩家ID）";
                    return false;
                }

                if (input.TargetIds.Length != 0)
                {
                    errorMessage = "ep 不需要 targetIds";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"ep payload 不支持字段: {unknown}";
                    return false;
                }

                if (!Payload.TryGetInt(input.Payload, "CurrentEp", out var current) && !Payload.TryGetInt(input.Payload, "Current", out current))
                {
                    errorMessage = "ep payload 缺少 CurrentEp/Current";
                    return false;
                }

                if (!Resolver.TryResolvePlayer(gameState, input.SourceId, out var player) || player == null)
                {
                    errorMessage = "ep sourceId 无法解析为玩家";
                    return false;
                }

                gameEvent = new EpUpdateEvent(player, current);
                return true;
            }
        }

        private sealed class EpMaxUpdateFactory : IFactory
        {
            private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
            {
                "MaxEp", "Max"
            };

            public bool TryCreate(RawEventEnvelopeDto input, GameState gameState, Guid requestingPlayerId, out IGameEvent? gameEvent, out string? errorMessage)
            {
                gameEvent = null;
                errorMessage = null;

                if (string.IsNullOrWhiteSpace(input.SourceId))
                {
                    errorMessage = "ep-max 缺少 sourceId（玩家ID）";
                    return false;
                }

                if (input.TargetIds.Length != 0)
                {
                    errorMessage = "ep-max 不需要 targetIds";
                    return false;
                }

                if (Payload.HasUnknownKeys(input.Payload, Allowed, out var unknown))
                {
                    errorMessage = $"ep-max payload 不支持字段: {unknown}";
                    return false;
                }

                if (!Payload.TryGetInt(input.Payload, "MaxEp", out var max) && !Payload.TryGetInt(input.Payload, "Max", out max))
                {
                    errorMessage = "ep-max payload 缺少 MaxEp/Max";
                    return false;
                }

                if (!Resolver.TryResolvePlayer(gameState, input.SourceId, out var player) || player == null)
                {
                    errorMessage = "ep-max sourceId 无法解析为玩家";
                    return false;
                }

                gameEvent = new EpMaxUpdateEvent(player, max);
                return true;
            }
        }
    }
}
