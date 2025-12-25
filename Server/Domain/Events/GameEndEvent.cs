using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events;

/// <summary>
/// Event triggered when the game ends (win/loss).
/// </summary>
[GameEventMetadata("游戏结束", "当对局分出胜负时触发。", EventType.GameEnd)]
public sealed class GameEndEvent : GameEventBase, ICommittableEvent, ILogAnnounceableEvent
{
    public Guid WinnerId { get; }
    public Guid LoserId { get; }
    public string? Reason { get; }

    public GameEndEvent(Guid winnerId, Guid loserId, string? reason = null)
        : base(EventType.GameEnd)
    {
        WinnerId = winnerId;
        LoserId = loserId;
        Reason = reason;
    }

    public DomainOperationResult Commit(GameContext context)
    {
        var result = new DomainOperationResult { Success = true };

        if (context.CurrentGameState.IsGameOver)
        {
            return result;
        }

        context.CurrentGameState.Status = GameStatus.Finished;
        context.CurrentGameState.Phase = GamePhase.GameOver;
        context.CurrentGameState.WinnerId = WinnerId;
        context.CurrentGameState.GameMessage = Reason;

        return result;
    }

    public string GetAnnouncement(GameState gameState)
    {
        var winnerToken = gameState.InGameIds.Format(InGameIdKind.Player, WinnerId);
        var loserToken = gameState.InGameIds.Format(InGameIdKind.Player, LoserId);
        return string.IsNullOrWhiteSpace(Reason)
            ? $"game_end winner={winnerToken} loser={loserToken}"
            : $"game_end winner={winnerToken} loser={loserToken} reason={Reason}";
    }
}
