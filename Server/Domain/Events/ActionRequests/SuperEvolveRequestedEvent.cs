using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using System;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// A top-level committable event representing the player's request to super evolve a unit.
    /// </summary>
    [GameEventMetadata(
        "超进化请求",
        "当玩家请求进行超进化时触发的事件。",
        EventType.SuperEvolveRequested,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.System)]
    public class SuperEvolveRequestedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起超进化请求的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("超进化动作", "超进化动作的详细信息。")]
        public SuperEvolveActionDto ActionDto { get; }

        public SuperEvolveRequestedEvent(Guid playerId, SuperEvolveActionDto actionDto)
            : base(EventType.SuperEvolveRequested)
        {
            PlayerId = playerId;
            ActionDto = actionDto;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();

            var requestingPlayer = context.CurrentGameState.GetPlayerState(this.PlayerId);
            if (requestingPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Could not find player state for PlayerId {this.PlayerId}." };
            }

            if (!Guid.TryParse(this.ActionDto.UnitUid, out Guid unitGuidToEvolve))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Invalid UnitUid format in SuperEvolveActionDto: {this.ActionDto.UnitUid}" };
            }

            var cardToEvolve = requestingPlayer.Field.FirstOrDefault(c => c.InstanceId == unitGuidToEvolve);

            if (cardToEvolve == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Unit with UID {unitGuidToEvolve} not found on player {this.PlayerId}'s field for super evolution." };
            }

            if (requestingPlayer.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player {requestingPlayer.PlayerId} attempted to super evolve unit, but it's not their turn (Active: {context.CurrentGameState.CurrentPlayerId})." };
            }

            if (!cardToEvolve.CanEvolve())
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Unit {cardToEvolve.Name} (UID: {cardToEvolve.InstanceId}) cannot super evolve intrinsically (already evolved or no form)." };
            }

            if (cardToEvolve.HasKeyword(CardKeyword.BanEvolve))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Unit {cardToEvolve.Name} (UID: {cardToEvolve.InstanceId}) cannot super evolve due to BanEvolve." };
            }

            if (requestingPlayer.CurrentSuperEvolutionPoints < GameConstants.SuperEvolution.SuperEvolutionPointCost)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player {requestingPlayer.PlayerId} does not have enough SEP to super evolve. Has: {requestingPlayer.CurrentSuperEvolutionPoints}, Needs: {GameConstants.SuperEvolution.SuperEvolutionPointCost}" };
            }

            if (!requestingPlayer.CanSuperEvolve)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player {requestingPlayer.PlayerId} has already used their super evolution right this turn." };
            }

            int superEvolutionStartTurn = requestingPlayer.IsFirst
                ? GameConstants.SuperEvolution.FirstPlayerSuperEvolutionStartTurn
                : GameConstants.SuperEvolution.SecondPlayerSuperEvolutionStartTurn;

            if (requestingPlayer.TurnNumber < superEvolutionStartTurn)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player {requestingPlayer.PlayerId} cannot super evolve on turn {requestingPlayer.TurnNumber}. Super evolution starts on turn {superEvolutionStartTurn}." };
            }

            var sepResult = requestingPlayer.ConsumeSuperEvolutionPoints(GameConstants.SuperEvolution.SuperEvolutionPointCost);
            result.GeneratedEvents.AddRange(sepResult.GeneratedEvents);

            requestingPlayer.ApplySuperEvolutionRightUsed();
            result.GeneratedEvents.Add(new SuperEvolveStateUpdatedEvent(requestingPlayer, false));

            // Super evolve counts as evolution for the turn.
            requestingPlayer.ApplyEvolutionRightUsed();
            result.GeneratedEvents.Add(new EvolveStateUpdatedEvent(requestingPlayer, false));

            var evolveResult = cardToEvolve.ApplySuperEvolution();
            result.GeneratedEvents.AddRange(evolveResult.GeneratedEvents);

            result.Success = true;
            return result;
        }
    }
}
