using Microsoft.AspNetCore.SignalR;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Infrastructure.Logging
{
    public sealed class AbilityTracePublisher : IAbilityTracePublisher
    {
        private readonly IConditionTraceSettings _settings;
        private readonly IHubContext<GameHub, IGameClient> _hub;
        private readonly IMatchService _matches;

        public AbilityTracePublisher(IConditionTraceSettings settings, IHubContext<GameHub, IGameClient> hub, IMatchService matches)
        {
            _settings = settings;
            _hub = hub;
            _matches = matches;
        }

        public void PublishCondition(GameContext context, IAbility ability, bool success, IReadOnlyList<ConditionTraceEntryDto> entries, string message)
        {
            Publish(context, _settings.IsConditionEnabled, () => CreateTrace(context, ability, success, entries, message, "Condition", null));
        }

        public void PublishTarget(GameContext context, IAbility ability, bool success, IReadOnlyList<ConditionTraceEntryDto> entries, int targetCount, string message)
        {
            Publish(context, _settings.IsTargetEnabled, () => CreateTrace(context, ability, success, entries, message, "Target", targetCount));
        }

        private void Publish(GameContext context, Func<Guid, bool> isEnabled, Func<ConditionTraceFailedDto> createTrace)
        {
            var game = context.CurrentGameState;
            var roomScopedId = _matches.TryGetRoomIdByGameId(game.GameId, out var roomId) && !string.IsNullOrWhiteSpace(roomId)
                ? roomId
                : game.GameId;
            var playerIds = new[] { game.Player1?.PlayerId, game.Player2?.PlayerId };
            foreach (var playerId in playerIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
            {
                if (playerId == Guid.Empty || !isEnabled(playerId))
                {
                    continue;
                }

                var trace = createTrace();
                var client = _hub.Clients.Group(SignalRGroupNames.Player(roomScopedId, playerId));
                // Keep diagnostics non-blocking; rule evaluation must not wait for client delivery.
                if (trace.Success)
                {
                    _ = client.ConditionTraceSucceeded(trace);
                }
                else
                {
                    _ = client.ConditionTraceFailed(trace);
                }
            }
        }

        private static ConditionTraceFailedDto CreateTrace(GameContext context, IAbility ability, bool success,
            IReadOnlyList<ConditionTraceEntryDto> entries, string message, string traceKind, int? targetCount)
        {
            var card = ability.SourceCard;
            return new ConditionTraceFailedDto
            {
                GameId = context.CurrentGameState.GameId,
                AbilityRuntimeId = ability.RuntimeId,
                AbilityName = ability.Name ?? string.Empty,
                CardName = card?.Name ?? "UnknownCard",
                CardId = card?.InstanceId.ToString() ?? "UnknownId",
                Success = success,
                TraceKind = traceKind,
                TargetCount = targetCount,
                ConditionId = string.Empty,
                Expression = entries.Count > 0 ? entries[0].Expression : string.Empty,
                Trace = entries.Count > 0 ? entries[0].Trace : string.Empty,
                Conditions = entries,
                Message = message
            };
        }
    }
}
