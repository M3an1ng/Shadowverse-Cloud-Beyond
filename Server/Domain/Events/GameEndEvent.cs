using System;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Identifiers;
using ShadowverseBeyond.Server.Services;

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

    public GameEndEvent(Guid winnerId, Guid loserId, string? reason = null, Guid? eventId = null, DateTime? timestamp = null)
        : base(EventType.GameEnd, eventId, timestamp)
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

        if (context.CurrentGameState.Mode == GameMode.Mirage && !context.CurrentGameState.IsCombatSandbox)
        {
            var shopService = context.ServiceProvider.GetService<IMirageShopService>();
            if (shopService != null)
            {
                if (LoserId != Guid.Empty)
                {
                    shopService.ReturnEliminatedPlayerCardsToPool(context.CurrentGameState, LoserId);
                }
                else if (WinnerId == Guid.Empty)
                {
                    shopService.ReturnEliminatedPlayerCardsToPool(context.CurrentGameState, context.CurrentGameState.Player1.PlayerId);
                    shopService.ReturnEliminatedPlayerCardsToPool(context.CurrentGameState, context.CurrentGameState.Player2.PlayerId);
                }
            }
        }

        context.CurrentGameState.Status = GameStatus.Finished;
        context.CurrentGameState.Phase = GamePhase.GameOver;
        context.CurrentGameState.WinnerId = WinnerId;
        context.CurrentGameState.GameMessage = Reason;

        return result;
    }

    public string GetAnnouncement(GameState gameState)
    {
        var winner = gameState.GetPlayerState(WinnerId);
        var loser = gameState.GetPlayerState(LoserId);
        var winnerToken = winner != null ? AnnouncementTokens.Player(winner) : AnnouncementTokens.PlayerId(WinnerId);
        var loserToken = loser != null ? AnnouncementTokens.Player(loser) : AnnouncementTokens.PlayerId(LoserId);

        return string.IsNullOrWhiteSpace(Reason)
            ? $"{winnerToken}获胜，{loserToken}败北。"
            : $"{winnerToken}获胜，{loserToken}败北。原因：{Reason}";
    }
}
