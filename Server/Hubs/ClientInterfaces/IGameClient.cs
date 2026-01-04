using ShadowverseBeyond.Server.Dtos.SignalR.Events;
// File: ShadowverseBeyond.Server.Hubs.ClientInterfaces.IGameClient.cs
using ShadowverseBeyond.Server.Dtos; // For GameStateDto etc.
using ShadowverseBeyond.Server.Dtos.SignalR; // <<< ADDED: For base GameEventDto
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Hubs.ClientInterfaces
{
    /// <summary>
    /// Defines the methods that the server Hub can invoke on connected clients.
    /// Used for strong-typed Hubs (Hub<IGameClient>).
    /// </summary>
    public interface IGameClient
    {
        /// <summary>
        /// Sends a specific game event DTO to the client.
        /// The client should inspect the Type property of the DTO to determine how to process it.
        /// </summary>
        /// <param name="gameEvent">The event data (base class from .Events namespace).</param>
        Task ReceiveGameEvent(GameEventDto gameEvent); // Should now resolve to Events.GameEventDto due to using statement

        /// <summary>
        /// Sends an error message related to game logic or player action to the client.
        /// </summary>
        /// <param name="errorMessage">The error message content.</param>
        Task ReceiveError(string errorMessage);

        /// <summary>
        /// Sends the result of a card playability check back to the client that requested it.
        /// </summary>
        /// <param name="result">A DTO containing the playability status and target information.</param>
        Task ReceivePlayabilityResult(PlayabilityResultDto result);

        /// <summary>
        /// Notifies the client that a card (Unit, Spell, Amulet) has been played from hand.
        /// </summary>
        Task CardPlayed(CardPlayedEventDto eventDto);

        /// <summary>
        /// Notifies the client that an entity (Unit or Amulet) has entered the play area.
        /// </summary>
        Task CardSummoned(CardSummonedEventDto eventDto);
        /// <summary>
        /// Notifies the client of an update to a player's Play Points (PP).
        /// </summary>
        Task PpUpdate(PpUpdateEventDto eventDto);

        /// <summary>
        /// Notifies the client that a player has finished their mulligan phase.
        /// </summary>
        /// <param name="eventData">Contains the ID of the player who completed.</param>
        Task PlayerMulliganCompleted(MulliganCompletedEventDto eventData);
        // --- Methods requiring DTOs not yet created (Commented out for now) ---
        Task ReceiveGameState(GameStateDto gameState); // Sends the full initial or updated game state
                                                       // Task RequestTargetSelection(TargetSelectionRequestDto request); // Keep commented for now
                                                       // --- Methods added for BroadcastEventAsync helper ---

        // TurnStarted method removed - Handled by ReceiveGameState

        /// <summary>
        /// Notifies the client that a turn has started.
        /// </summary>
        Task TurnStarted(TurnStartedEventDto eventDto);
        /// <summary>
        /// Notifies the client that a turn has ended.
        /// </summary>
        Task TurnEnded(TurnEndedEventDto eventDto);

        /// <summary>
        /// Notifies the client of an update to a player's Evolution Points (EP).
        /// </summary>
        Task EpUpdate(EpUpdateEventDto eventDto);

        /// <summary>
        /// Notifies the client of an update to a player's Super Evolution Points (SEP).
        /// </summary>
        Task SepUpdate(SepUpdateEventDto eventDto);

        /// <summary>
        /// Notifies the client of an update to a player's maximum Play Points (PP).
        /// </summary>
        Task PpMaxUpdate(PpMaxUpdateEventDto eventDto);

        /// <summary>
        /// Notifies the client of an update to a player's maximum Evolution Points (EP).
        /// </summary>
        Task EpMaxUpdate(EpMaxUpdateEventDto eventDto);

        /// <summary>
        /// Notifies the client of an update to a player's maximum Super Evolution Points (SEP).
        /// </summary>
        Task SepMaxUpdate(SepMaxUpdateEventDto eventDto);


        /// <summary>
        /// Notifies the client of an update to their hand (cards drawn, discarded, etc.).
        /// </summary>
        Task HandUpdated(HandUpdatedEventDto eventDto); // Restored for mulligan functionality

        /// <summary>
        /// Notifies the client of an update to their deck count.
        /// </summary>
        /// <summary>
        /// Notifies the client of an update to their deck count.
        /// </summary>
        Task DeckUpdate(DeckUpdateEventDto dto); // Restored method definition
        // /// <summary>
        // /// Notifies the client that a card has been drawn. (REMOVED as per design - HandUpdate/DeckUpdate handle UI)
        // /// </summary>
        // Task CardDrawn(CardDrawnEventDto eventDto); // REMOVED

        /// <summary>
        /// Notifies the client that a unit has evolved.
        /// </summary>
        Task UnitEvolved(UnitEvolvedEventDto eventDto);

        /// <summary>
        /// Notifies the client that a unit has been destroyed.
        /// </summary>
        Task UnitDestroyed(UnitDestroyedEventDto eventDto);

        /// <summary>
        /// Notifies the client that an attack has been declared.
        /// </summary>
        Task AttackDeclared(AttackDeclaredEventDto eventDto);

        /// <summary>
        /// Notifies the client that damage has been dealt to an entity.
        /// </summary>
        Task DamageDealt(DamageDealtEventDto eventDto);

        /// <summary>
        /// Notifies the client that an entity's health has changed.
        /// </summary>
        Task HealthChanged(HealthChangedEventDto eventDto);

        /// <summary>
        /// Notifies the client that a unit's stats (attack/health) have changed (e.g., buffs, debuffs).
        /// </summary>
        Task StatChange(StatChangeEventDto eventDto);

        /// <summary>
        /// Notifies the client of an update to their graveyard count or contents.
        /// </summary>
        Task GraveyardUpdate(GraveyardUpdateEventDto eventDto);

        /// <summary>
        /// Sends hand playability update to the client after player actions complete.
        /// Contains playability status for all cards in the player's hand.
        /// </summary>
        /// <param name="updateDto">The hand playability update DTO containing playability results.</param>
        Task ReceiveHandPlayabilityUpdate(HandPlayabilityUpdateDto updateDto);

        /// <summary>
        /// Notifies the client that a unit's attack state has been updated.
        /// </summary>
        /// <param name="eventDto">The attack state update DTO.</param>
        Task AttackStateUpdated(AttackStateUpdatedEventDto eventDto);

        /// <summary>
        /// Notifies the client that a player's evolve state has been updated.
        /// </summary>
        Task EvolveStateUpdated(EvolveStateUpdatedEventDto eventDto);

        /// <summary>
        /// Notifies the client that a player's super evolve state has been updated.
        /// </summary>
        Task SuperEvolveStateUpdated(SuperEvolveStateUpdatedEventDto eventDto);

        /// <summary>
        /// Notifies the client that a card's Act state has been updated.
        /// </summary>
        Task ActStateUpdated(ActStateUpdatedEventDto eventDto);

        /// <summary>
        /// Notifies the client that a player's PP+1 uses availability has been updated.
        /// </summary>
        Task PpBoostStateUpdated(PpBoostStateUpdatedEventDto eventDto);

        /// <summary>
        /// Notifies the client that the game has ended.
        /// </summary>
        Task GameOver(GameOverEventDto eventDto);

        /// <summary>
        /// Notifies the client that the game is paused and requires a choice from the player.
        /// </summary>
        /// <param name="eventDto">The DTO containing details about the choice required.</param>
        Task PlayerChoiceRequested(PlayerChoiceRequestedEventDto eventDto);

        /// <summary>
        /// Sends a generic log/announcement entry for events without a dedicated UI DTO.
        /// </summary>
        Task LogEvent(LogEventDto eventDto);

        /// <summary>
        /// Notifies both clients that a card has been revealed.
        /// </summary>
        Task CardRevealed(CardRevealedEventDto eventDto);

        /// <summary>
        /// Notifies clients that Fusion materials have been consumed (UI animation/log).
        /// </summary>
        Task Fusion(FusionEventDto eventDto);
    }
}
