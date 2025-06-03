// Server/Services/AiTurnExecutor.cs
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic; // Added for List<>
using ShadowverseBeyond.Server.Models; // For GameActionContext

namespace ShadowverseBeyond.Server.Services
{
    public class AiTurnExecutor
    {
        private readonly IGameService _gameService; // To interact with game state and execute actions
        private readonly IAiService _aiService;     // To get AI decisions
        private readonly ILogger<AiTurnExecutor> _logger;
        // private readonly IServiceScopeFactory _serviceScopeFactory; // Add if IAiService needs scoped deps

        // Constructor injection
        public AiTurnExecutor(
            IGameService gameService,
            IAiService aiService,
            ILogger<AiTurnExecutor> logger)
        // IServiceScopeFactory serviceScopeFactory)
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        // The core AI turn logic, moved from GameService
        // M8 Change: Reverted - Context should be created internally per action
        public async Task ExecuteTurnAsync(string gameId, string aiPlayerId, GameActionContext context) // Re-added context parameter for M8 sequence
        {
            _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI turn execution started.", gameId, aiPlayerId);
            bool actionTaken = false;
            GameStateDto? currentGameState = null; // Declare variable *before* the main try block

            // The StartPlayerTurnAsync call and related logic have been removed.
            // The AI turn is now started by GameService.EndTurnAsync before this executor is called.
            try
            {
                // TODO: Add a mechanism to prevent infinite loops (e.g., max actions per turn)
                int maxActions = 20; // Example limit
                int actionsPerformed = 0;

                do // Loop until AI decides to end turn or cannot perform more actions
                {

                    // Fetch the game state at the start of each iteration
                    _logger.LogDebug("[AI_EXECUTOR:{GameId}:{PlayerId}] Fetching current game state for iteration.", gameId, aiPlayerId);
                    currentGameState = await _gameService.GetGameStateAsync(gameId);
                    if (currentGameState == null)
                    {
                        _logger.LogError("[AI_EXECUTOR:{GameId}:{PlayerId}] Failed to retrieve game state in loop. Breaking AI turn.", gameId, aiPlayerId);
                        break; // Exit loop if state fetch fails
                    }
                    if (currentGameState.IsGameOver)
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] Game is over (checked in loop). Breaking AI actions.", gameId, aiPlayerId);
                        break; // Exit loop if game ended
                    }
                    if (currentGameState.CurrentPlayerId != aiPlayerId)
                    {
                        _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Not AI's turn (checked in loop - Current: {CurrentPlayer}). Breaking.", gameId, aiPlayerId, currentGameState.CurrentPlayerId);
                        break; // Exit loop if not AI's turn
                    }

                    // --- Roo Debug Log: Loop Start ---
                    _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Starting action loop iteration #{Iteration}. Actions performed so far: {ActionsPerformed}", gameId, aiPlayerId, actionsPerformed + 1, actionsPerformed);
                    // --- End Roo Debug Log ---


                    if (actionsPerformed >= maxActions)
                    {
                        // --- Roo Debug Log: Max Actions Reached ---
                        _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Max actions ({MaxActions}) reached. Breaking action loop.", gameId, aiPlayerId, maxActions);
                        // --- End Roo Debug Log ---
                        // _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Reached max action limit ({MaxActions}). Forcing end turn.", gameId, aiPlayerId, maxActions); // Original log
                        break;
                    }

                    actionTaken = false;
                    // Removed redundant inner declaration

                    // Safely get the latest game state via IGameService
                    // TODO: Ensure IGameService has a suitable method like GetCurrentGameStateAsync(gameId)
                    // For now, assume GetOrCreateGameStateAsync can be used, although it might create if missing (undesirable here).
                    // Let's proceed assuming we can get the state. We need a way to get it.
                    // Fetch state ONCE at the beginning of the loop iteration (will be moved outside loop later)
                    // --- AI Decision Sequence ---
                    actionsPerformed++;
                    _logger.LogDebug("[AI_EXECUTOR:{GameId}:{PlayerId}] Starting action decision cycle #{ActionNum}.", gameId, aiPlayerId, actionsPerformed);

                    // --- Roo Debug Log: Decide Play Card Start ---
                    _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Calling _aiService.DecidePlayCardAsync.", gameId, aiPlayerId);
                    // --- End Roo Debug Log ---
                    // 1. Decide Play Card
                    var playCardAction = await _aiService.DecidePlayCardAsync(currentGameState, aiPlayerId);
                    // --- Roo Debug Log: Decide Play Card End ---
                    _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] _aiService.DecidePlayCardAsync result: {Decision}", gameId, aiPlayerId, playCardAction == null ? "null" : $"Card={playCardAction.CardUid}, Target={playCardAction.TargetUid ?? "None"}");
                    // --- End Roo Debug Log ---
                    if (playCardAction != null)
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI decided to play card: CardUID={CardUid}, Target={TargetUid}",
                            gameId, aiPlayerId, playCardAction.CardUid, playCardAction.TargetUid ?? "None");
                        try
                        {
                            // AI performs the action by calling the GameService method
                            // --- Roo Debug Log: Execute Play Card Start ---
                            _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Calling _gameService.PlayCardAsync: Card={CardUid}, Target={TargetUid}", gameId, aiPlayerId, playCardAction.CardUid, playCardAction.TargetUid ?? "None");
                            // --- End Roo Debug Log ---
                            // M8 Change: Create context, pass it, then broadcast events
                            // Pass the received context down
                            await _gameService.PlayCardAsync(gameId, aiPlayerId, playCardAction, context);
                            // Broadcasting is handled within PlayCardAsync
                            _logger.LogDebug("[AI_EXECUTOR:{GameId}:{PlayerId}] PlayCardAsync and BroadcastEventsAsync completed.", gameId, aiPlayerId);
                            actionTaken = true;
                            continue; // Check for another action after playing a card
                        }
                        catch (GameLogicException ex)
                        {
                            // --- Roo Debug Log: Play Card GameLogicException ---
                            _logger.LogError(ex, "[AI_EXEC_DEBUG:{GameId}:{PlayerId}] AI failed PlayCard (GameLogicException). Card={CardUid}. Trying next action.", gameId, aiPlayerId, playCardAction.CardUid);
                            // --- End Roo Debug Log ---
                            // _logger.LogWarning(ex, "[AI_EXECUTOR:{GameId}:{PlayerId}] AI failed to execute PlayCard action. Trying next action type.", gameId, aiPlayerId); // Original log
                        }
                        catch (Exception ex)
                        {
                            // --- Roo Debug Log: Play Card Exception ---
                            _logger.LogError(ex, "[AI_EXEC_DEBUG:{GameId}:{PlayerId}] AI failed PlayCard (Exception). Card={CardUid}. Aborting turn.", gameId, aiPlayerId, playCardAction.CardUid);
                            // --- End Roo Debug Log ---
                            // _logger.LogError(ex, "[AI_EXECUTOR:{GameId}:{PlayerId}] Unexpected error executing AI PlayCard action. Aborting turn.", gameId, aiPlayerId); // Original log
                            break; // Exit the loop to end turn
                        }
                    }

                    // 2. Decide Evolve
                    // State is no longer re-fetched here. Assumes currentGameState is mutated by PlayCardAsync.
                    // Check if game ended during the previous action.
                    if (currentGameState.IsGameOver) { _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Game over after PlayCard check.", gameId, aiPlayerId); return; }

                    // --- Roo Debug Log: Decide Evolve Start ---
                    _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Calling _aiService.DecideEvolveAsync.", gameId, aiPlayerId);
                    // --- End Roo Debug Log ---
                    var evolveAction = await _aiService.DecideEvolveAsync(currentGameState, aiPlayerId);
                    // --- Roo Debug Log: Decide Evolve End ---
                    _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] _aiService.DecideEvolveAsync result: {Decision}", gameId, aiPlayerId, evolveAction == null ? "null" : $"Unit={evolveAction.UnitUid}");
                    // --- End Roo Debug Log ---
                    if (evolveAction != null)
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI decided to evolve unit: UnitUID={UnitUid}", gameId, aiPlayerId, evolveAction.UnitUid);
                        try
                        {
                            // --- Roo Debug Log: Execute Evolve Start ---
                            _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Calling _gameService.EvolveAsync: Unit={UnitUid}", gameId, aiPlayerId, evolveAction.UnitUid);
                            // --- End Roo Debug Log ---
                            // M8 Change: Create context, pass it, then broadcast events
                            // Pass the received context down
                            await _gameService.EvolveAsync(gameId, aiPlayerId, evolveAction, context);
                            // Broadcasting is handled within EvolveAsync
                            _logger.LogDebug("[AI_EXECUTOR:{GameId}:{PlayerId}] EvolveAsync and BroadcastEventsAsync completed.", gameId, aiPlayerId);
                            actionTaken = true;
                            continue; // Check for another action after evolving
                        }
                        catch (GameLogicException ex)
                        {
                            // --- Roo Debug Log: Evolve GameLogicException ---
                            _logger.LogError(ex, "[AI_EXEC_DEBUG:{GameId}:{PlayerId}] AI failed Evolve (GameLogicException). Unit={UnitUid}. Trying next action.", gameId, aiPlayerId, evolveAction.UnitUid);
                            // --- End Roo Debug Log ---
                            // _logger.LogWarning(ex, "[AI_EXECUTOR:{GameId}:{PlayerId}] AI failed to execute Evolve action. Trying next action type.", gameId, aiPlayerId); // Original log
                        }
                        catch (Exception ex)
                        {
                            // --- Roo Debug Log: Evolve Exception ---
                            _logger.LogError(ex, "[AI_EXEC_DEBUG:{GameId}:{PlayerId}] AI failed Evolve (Exception). Unit={UnitUid}. Aborting turn.", gameId, aiPlayerId, evolveAction.UnitUid);
                            // --- End Roo Debug Log ---
                            // _logger.LogError(ex, "[AI_EXECUTOR:{GameId}:{PlayerId}] Unexpected error executing AI Evolve action. Aborting turn.", gameId, aiPlayerId); // Original log
                            break;
                        }
                    }

                    // 3. Decide Attacks
                    // State is no longer re-fetched here. Assumes currentGameState is mutated by EvolveAsync.
                    // Check if game ended during the previous action.
                    if (currentGameState.IsGameOver) { _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Game over after Evolve check.", gameId, aiPlayerId); return; }

                    // --- Roo Debug Log: Decide Attack Start ---
                    _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Calling _aiService.DecideAttacksAsync.", gameId, aiPlayerId);
                    // --- End Roo Debug Log ---
                    var attackActions = await _aiService.DecideAttacksAsync(currentGameState, aiPlayerId);
                    // --- BUG-017 DIAGNOSTIC LOG START ---
                    if (attackActions == null || !attackActions.Any())
                    {
                        _logger.LogWarning("[BUG-017_DIAG:{GameId}:{PlayerId}] AI decided NO attacks. Attack Actions List: null or empty.", gameId, aiPlayerId);
                    }
                    else
                    {
                        var attackDetails = string.Join(", ", attackActions.Select(a => $"({a.AttackerUid} -> {a.TargetUid})"));
                        _logger.LogWarning("[BUG-017_DIAG:{GameId}:{PlayerId}] AI decided {Count} attacks: [{Details}]", gameId, aiPlayerId, attackActions.Count, attackDetails);
                    }
                    // --- BUG-017 DIAGNOSTIC LOG END ---
                    // --- Roo Debug Log: Decide Attack End ---
                    _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] _aiService.DecideAttacksAsync result: {Count} actions.", gameId, aiPlayerId, attackActions?.Count ?? 0);
                    // --- End Roo Debug Log ---
                    if (attackActions != null && attackActions.Any())
                    {
                        _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI decided to perform {AttackCount} attacks.", gameId, aiPlayerId, attackActions.Count);
                        bool anyAttackSucceeded = false;
                        foreach (var attackAction in attackActions)
                        {
                            // State is no longer re-fetched here. Assumes currentGameState is mutated by previous actions/attacks.
                            // Check if game ended during the previous action/attack.
                            if (currentGameState.IsGameOver) { _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Game over during attacks sequence.", gameId, aiPlayerId); break; } // Break attack loop if game ended

                            _logger.LogDebug("[AI_EXECUTOR:{GameId}:{PlayerId}] AI attempting attack: Attacker={AttackerUid}, Target={TargetUid}",
                               gameId, aiPlayerId, attackAction.AttackerUid, attackAction.TargetUid);
                            try
                            {
                                // --- Roo Debug Log: Execute Attack Start ---
                                _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Calling _gameService.AttackAsync: Attacker={AttackerUid}, Target={TargetUid}", gameId, aiPlayerId, attackAction.AttackerUid, attackAction.TargetUid);
                                // --- End Roo Debug Log ---
                                // M8 Change: Create context, pass it, then broadcast events
                                // Pass the received context down
                                await _gameService.AttackAsync(gameId, aiPlayerId, attackAction, context);
                                // --- BUG-017 DIAGNOSTIC LOG START ---
                                _logger.LogWarning("[BUG-017_DIAG:{GameId}:{PlayerId}] Attempting attack: Attacker={AttackerUid}, Target={TargetUid}", gameId, aiPlayerId, attackAction.AttackerUid, attackAction.TargetUid);
                                // --- BUG-017 DIAGNOSTIC LOG END ---
                                // Broadcasting is handled within AttackAsync
                                _logger.LogDebug("[AI_EXECUTOR:{GameId}:{PlayerId}] AttackAsync and BroadcastEventsAsync completed for one attack.", gameId, aiPlayerId);
                                anyAttackSucceeded = true;
                            }
                            catch (GameLogicException ex)
                            {
                                // --- Roo Debug Log: Attack GameLogicException ---
                                _logger.LogError(ex, "[AI_EXEC_DEBUG:{GameId}:{PlayerId}] AI failed Attack (GameLogicException). Attacker={AttackerUid}, Target={TargetUid}. Skipping attack.", gameId, aiPlayerId, attackAction.AttackerUid, attackAction.TargetUid);
                                // --- End Roo Debug Log ---
                                // _logger.LogWarning(ex, "[AI_EXECUTOR:{GameId}:{PlayerId}] AI failed to execute one Attack action (Attacker: {AttackerUid}). Skipping this attack.", gameId, aiPlayerId, attackAction.AttackerUid); // Original log
                                // --- BUG-017 DIAGNOSTIC LOG START ---
                                _logger.LogError(ex, "[BUG-017_DIAG:{GameId}:{PlayerId}] GameLogicException during attack: Attacker={AttackerUid}, Target={TargetUid}. Exception: {ExceptionString}", gameId, aiPlayerId, attackAction.AttackerUid, attackAction.TargetUid, ex.ToString());
                                // --- BUG-017 DIAGNOSTIC LOG END ---
                            }
                            catch (Exception ex)
                            {
                                // --- Roo Debug Log: Attack Exception ---
                                _logger.LogError(ex, "[AI_EXEC_DEBUG:{GameId}:{PlayerId}] AI failed Attack (Exception). Attacker={AttackerUid}, Target={TargetUid}. Aborting turn.", gameId, aiPlayerId, attackAction.AttackerUid, attackAction.TargetUid);
                                // --- End Roo Debug Log ---
                                // _logger.LogError(ex, "[AI_EXECUTOR:{GameId}:{PlayerId}] Unexpected error executing AI Attack action (Attacker: {AttackerUid}). Aborting turn.", gameId, aiPlayerId, attackAction.AttackerUid); // Original log
                                actionTaken = false; // Ensure loop terminates
                                                     // --- BUG-017 DIAGNOSTIC LOG START ---
                                _logger.LogError(ex, "[BUG-017_DIAG:{GameId}:{PlayerId}] Exception during attack: Attacker={AttackerUid}, Target={TargetUid}. Exception: {ExceptionString}", gameId, aiPlayerId, attackAction.AttackerUid, attackAction.TargetUid, ex.ToString());
                                // --- BUG-017 DIAGNOSTIC LOG END ---
                                break; // Exit the attack loop and the outer action loop
                            }
                        }
                        // If any attack succeeded, mark actionTaken true and re-evaluate
                        if (anyAttackSucceeded)
                        {
                            actionTaken = true;
                            continue; // Allow further actions after attacks
                        }
                        if (!actionTaken)
                        { // If unexpected error broke the attack loop
                            break; // Exit outer loop
                        }
                    }

                    // 4. If no other action was taken in this cycle, break the loop to end the turn.
                    _logger.LogDebug("[AI_EXECUTOR:{GameId}:{PlayerId}] AI finished decision cycle #{ActionNum}. ActionTaken={ActionTaken}", gameId, aiPlayerId, actionsPerformed, actionTaken);
                    if (!actionTaken)
                    {
                        // --- Roo Debug Log: No Action Taken ---
                        _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] No more AI actions decided this iteration (Play/Evolve/Attack returned null or failed). Ending action loop.", gameId, aiPlayerId);
                        // --- End Roo Debug Log ---
                        break; // Exit loop if no action taken in this pass
                    }
                } while (true); // Loop controlled by 'continue' and 'break'

                // --- Roo Debug Log: Exited Action Loop ---
                // Note: The 'reason' logic might need refinement based on how errors are handled vs. no actions decided.
                _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Exited action loop. Reason: {ExitReason}. actionsPerformed={ActionsPerformed}",
                                 gameId, aiPlayerId,
                                 actionsPerformed >= maxActions ? "Max actions reached" : (!actionTaken ? "No action taken" : "Error occurred"),
                                 actionsPerformed);
                // --- End Roo Debug Log ---

                // --- End Turn ---
                _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] AI action loop finished or max actions reached. Calling EndTurnAsync.", gameId, aiPlayerId);
                // Use the existing currentGameState which should reflect the final state after the loop.
                var finalGameState = currentGameState;

                if (finalGameState != null && !finalGameState.IsGameOver && finalGameState.CurrentPlayerId == aiPlayerId)
                {
                    // RESTORED EndTurnAsync call - AI must end its own turn!
                    // --- Roo Debug Log: Final End Turn Call Start ---
                    _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] AI loop finished normally. Calling _gameService.EndTurnAsync.", gameId, aiPlayerId);
                    // --- End Roo Debug Log ---
                    // --- Roo Modify Start: Capture and Broadcast End Turn Events (Normal) ---
                    // Roo Change: Deconstruct the tuple returned by the updated EndTurnAsync
                    // Call EndTurnAsync and discard the result tuple, as it's handled elsewhere.
                    // M8 Change: Create context, pass it, then broadcast events
                    // Pass the received context down
                    await _gameService.EndTurnAsync(gameId, aiPlayerId, context);
                    // Broadcasting is handled within EndTurnAsync
                    // --- Roo Debug Log: Final End Turn Call End ---
                    _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] _gameService.EndTurnAsync and BroadcastEventsAsync completed normally.", gameId, aiPlayerId);
                    // --- End Roo Debug Log ---
                    // Removed broadcast for endTurnEvents as EndTurnAsync no longer returns them

                    // Removed logic that started the next player's turn.
                    // This responsibility is moved to GameService.ExecuteAiTurnInternalAsync.
                }
                else if (finalGameState != null && finalGameState.IsGameOver)
                {
                    _logger.LogInformation("[AI_EXECUTOR:{GameId}:{PlayerId}] Game is already over. Skipping final EndTurn call.", gameId, aiPlayerId);
                } // Closing brace for the main try block
                else if (finalGameState != null && finalGameState.CurrentPlayerId != aiPlayerId)
                {
                    _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Turn already passed? CurrentPlayer is {CurrentPlayer}. Skipping final EndTurn call.", gameId, aiPlayerId, finalGameState.CurrentPlayerId);
                }
                else
                {
                    _logger.LogError("[AI_EXECUTOR:{GameId}:{PlayerId}] Could not retrieve final game state or state invalid before ending turn. AI EndTurn skipped.", gameId, aiPlayerId);
                }

            } // Closing brace for the main try block
            catch (Exception ex)
            {
                // --- Roo Debug Log: Outer Catch ---
                _logger.LogError(ex, "[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Critical error during AI turn execution (outer catch).", gameId, aiPlayerId);
                // --- End Roo Debug Log ---
                // _logger.LogError(ex, "[AI_EXECUTOR:{GameId}:{PlayerId}] Unexpected error during AI turn execution main loop.", gameId, aiPlayerId); // Original log
                // Attempt to end the turn gracefully if possible, to avoid stalling the game.
                try
                {
                    // Use the existing currentGameState if available, otherwise we can't check state reliably.
                    var errorState = currentGameState;

                    if (errorState != null && !errorState.IsGameOver && errorState.CurrentPlayerId == aiPlayerId)
                    {
                        // _logger.LogWarning("[AI_EXECUTOR:{GameId}:{PlayerId}] Attempting emergency EndTurn after error.", gameId, aiPlayerId); // Original log
                        // --- Roo Debug Log: Emergency End Turn Start ---
                        _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Attempting emergency EndTurn due to critical error.", gameId, aiPlayerId);
                        // --- End Roo Debug Log ---
                        // --- Roo Modify Start: Capture and Broadcast End Turn Events (Emergency) ---
                        // Roo Change: Deconstruct tuple, ignoring next player info in emergency
                        // Call emergency EndTurnAsync, ignore the returned string (nextPlayerId)
                        // M8 Change: Create context, pass it, then broadcast events
                        // Pass the received context down
                        await _gameService.EndTurnAsync(gameId, aiPlayerId, context); // RESTORED emergency call
                        // Broadcasting is handled within EndTurnAsync
                        // --- Roo Debug Log: Emergency End Turn End ---
                        _logger.LogWarning("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Emergency EndTurn and BroadcastEventsAsync completed.", gameId, aiPlayerId);
                        // --- End Roo Debug Log ---
                        // Removed broadcast for emergencyEvents as EndTurnAsync no longer returns them
                        // --- Roo Modify End: Capture and Broadcast End Turn Events (Emergency) ---
                    }
                }
                catch (Exception endEx)
                {
                    // --- Roo Debug Log: Emergency End Turn Exception ---
                    _logger.LogError(endEx, "[AI_EXEC_DEBUG:{GameId}:{PlayerId}] Failed to execute emergency EndTurn after critical error.", gameId, aiPlayerId);
                    // --- End Roo Debug Log ---
                    // _logger.LogError(endEx, "[AI_EXECUTOR:{GameId}:{PlayerId}] Error during emergency EndTurn after previous error.", gameId, aiPlayerId); // Original log
                }
                // Do not re-throw here as this is a fire-and-forget task.
            }
            finally // ADDED Finally block
            {
                // --- Roo Debug Log: Finally Block ---
                _logger.LogInformation("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] ExecuteTurnAsync finally block reached.", gameId, aiPlayerId);
                // --- End Roo Debug Log ---
                // Signal that the AI turn processing is complete (important for GameHub coordination if needed, though likely not now)
                // If using TCS: _aiTurnCompletionSources.TryRemove(gameId, out var tcs); tcs?.TrySetResult(true);
                // --- Roo Debug Log: Finally Block End ---
                _logger.LogInformation("[AI_EXEC_DEBUG:{GameId}:{PlayerId}] ExecuteTurnAsync finally block completed.", gameId, aiPlayerId);
                // --- End Roo Debug Log ---
            }
        }
    }
}