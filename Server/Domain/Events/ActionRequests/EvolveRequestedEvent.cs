using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using AutoMapper;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Constants;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;

namespace ShadowverseBeyond.Server.Domain.Events.ActionRequests
{
    /// <summary>
    /// A top-level committable event representing the player's request to evolve a unit.
    /// This event encapsulates the entire evolution process, from validation to state change.
    /// </summary>
    [GameEventMetadata(
        "进化请求",
        "当玩家请求进行进化时触发的事件。",
        EventType.Evolve,
        AbilityEditorCategories.SystemRequestId,
        AbilityEditorCategories.SystemRequestLabel,
        EventMetadataVisibility.Hidden)]
    public class EvolveRequestedEvent : GameEventBase, ICommittableEvent
    {
        [GameEventParameter("玩家ID", "发起进化请求的玩家ID。")]
        public Guid PlayerId { get; }

        [GameEventParameter("进化动作", "进化动作的详细信息。")]
        public EvolveActionDto ActionDto { get; }

        public EvolveRequestedEvent(Guid playerId, EvolveActionDto actionDto)
            : base(EventType.Evolve) // Use Evolve event type for triggering purposes
        {
            PlayerId = playerId;
            ActionDto = actionDto;
        }

        public DomainOperationResult Commit(GameContext context)
        {
            var result = new DomainOperationResult();

            // =================================================================
            // 1. Resolve Entities from IDs
            // =================================================================
            var requestingPlayer = context.CurrentGameState.GetPlayerState(this.PlayerId);
            if (requestingPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Could not find player state for PlayerId {this.PlayerId}." };
            }

            if (!Guid.TryParse(this.ActionDto.UnitUid, out Guid unitGuidToEvolve))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Invalid UnitUid format in EvolveActionDto: {this.ActionDto.UnitUid}" };
            }

            var cardToEvolve = requestingPlayer.Field.FirstOrDefault(c => c.InstanceId == unitGuidToEvolve);

            // =================================================================
            // 2. Pre-condition Checks
            // =================================================================
            if (cardToEvolve == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Unit with UID {unitGuidToEvolve} not found on player {this.PlayerId}'s field for evolution." };
            }

            if (requestingPlayer.PlayerId != context.CurrentGameState.CurrentPlayerId)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player {requestingPlayer.PlayerId} attempted to evolve unit, but it's not their turn (Active: {context.CurrentGameState.CurrentPlayerId})." };
            }

            if (!cardToEvolve.CanEvolve())
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Unit {cardToEvolve.Name} (UID: {cardToEvolve.InstanceId}) cannot evolve intrinsically (already evolved or no form)." };
            }

            if (cardToEvolve.HasKeyword(CardKeyword.BanEvolve))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Unit {cardToEvolve.Name} (UID: {cardToEvolve.InstanceId}) cannot evolve due to BanEvolve." };
            }

            var hasFreeEvolve = cardToEvolve.HasKeyword(CardKeyword.FreeEvolve);
            if (!hasFreeEvolve && requestingPlayer.CurrentEvolutionPoints < GameConstants.Evolution.EvolutionPointCost)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player {requestingPlayer.PlayerId} does not have enough EP to evolve. Has: {requestingPlayer.CurrentEvolutionPoints}, Needs: {GameConstants.Evolution.EvolutionPointCost}" };
            }

            if (!requestingPlayer.CanEvolve)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player {requestingPlayer.PlayerId} has already used their evolution right this turn." };
            }

            int evolutionStartTurn = requestingPlayer.IsFirst ? GameConstants.Evolution.FirstPlayerEvolutionStartTurn : GameConstants.Evolution.SecondPlayerEvolutionStartTurn;
            if (requestingPlayer.TurnNumber < evolutionStartTurn)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Player {requestingPlayer.PlayerId} cannot evolve on turn {requestingPlayer.TurnNumber}. Evolution starts on turn {evolutionStartTurn}." };
            }

            // =================================================================
            // 3. State Changes
            // =================================================================

            // 3.1) 消耗 EP
            if (!hasFreeEvolve)
            {
                var epResult = requestingPlayer.ConsumeEvolutionPoints(GameConstants.Evolution.EvolutionPointCost);
                result.GeneratedEvents.AddRange(epResult.GeneratedEvents);
            }

            // 3.2) 设置本回合进化限制
            requestingPlayer.ApplyEvolutionRightUsed();
            result.GeneratedEvents.Add(new EvolveStateUpdatedEvent(requestingPlayer, false));

            // 3.3) 生成卡牌进化与身材变化事件
            var evolveResult = cardToEvolve.ApplyEvolution();
            result.GeneratedEvents.AddRange(evolveResult.GeneratedEvents);

            result.Success = true;
            return result;
        }
    }
}
