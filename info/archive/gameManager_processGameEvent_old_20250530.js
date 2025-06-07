
/**
 * Processes incremental game events received from the SignalR hub.
 * Updates the local gameState and triggers UI refresh.
 * @param {object} gameEvent - The event object received from the server.
 * Expected structure: { eventType: "...", payload: { ... } }
 */
export function processGameEvent(gameEvent) {
    const eventTypeForLog = gameEvent?.type ?? 'UNKNOWN_EVENT'; // Safely get event type for logging
    console.log(`%cGM_REENTRANCY_DBG: >>> Entering processGameEvent (${eventTypeForLog})`, 'color: magenta; font-weight: bold;', new Date().toISOString()); // ADDED ENTRY LOG
    // ADDED EXPORT
// <<< ADD CONSOLE LOG AT THE VERY BEGINNING >>>
    // console.log(`[DEBUG_SIGNALR] processGameEvent invoked! Event received:`, gameEvent);
    // <<< END ADDED LOG >>>
    // console.log("GM_TRACE: >>> Entering processGameEvent"); // TRACE LOG ENTRY
    // Check for 'type' property instead of 'eventType'
    if (!gameEvent || !gameEvent.type) {
        // MODIFIED: Check gameEvent.type
        console.error(
            "GAME MANAGER: Received invalid game event object (missing 'type' property):",
            gameEvent
        );
        return;
    }

    // Use gameEvent.type for logging and switch statement
    // console.log( `GAME MANAGER: Processing Game Event - Type: ${gameEvent.type}`, gameEvent.payload ); // MODIFIED: Use gameEvent.type
// DEBUGGING Turn Number (Inserted Block 1):
    if (gameEvent.type === 'TurnEnded') { // Only log this specific detail for TurnEnded events
        // console.log('DEBUG_TURN (Before Assign): Received TurnEnded event data:', JSON.stringify(gameEvent, null, 2));
        // console.log(`DEBUG_TURN (Before Assign): Value of gameEvent.newTurnNumber: ${gameEvent.newTurnNumber} (Type: ${typeof gameEvent.newTurnNumber})`);
    }
    // END DEBUGGING (Inserted Block 1)
    // console.log(
    //     "GM_TRACE: Event Payload:",
    //     JSON.stringify(gameEvent.payload, null, 2)
    // ); // TRACE LOG PAYLOAD

    try {
        // console.log(`GM_TRACE: Entering switch for event type: ${gameEvent.type}`); // TRACE LOG BEFORE SWITCH
        switch (
        gameEvent.type // MODIFIED: Switch on gameEvent.type
        ) {
            case "TurnEnded":
                {
                    // console.log("GM_TRACE: --- Entering TurnEnded case ---"); // TRACE LOG CASE ENTRY
                    // Payload might contain: { nextPlayerId: "...", newTurnNumber: N, playerUpdates: [{playerId:"..", pp: N, maxPP: N, ep: N, maxEP: N, drawnCard: {...}? }] }
                    // Removed payload check
                    // console.log("GM_TRACE: TurnEnded payload exists, processing..."); // TRACE LOG PAYLOAD CHECK
                    // DETAILED LOGGING FOR TurnEnded START
                    // console.log(
                    //     `GM_DEBUG: TurnEnded Event Received. Payload:`,
                    //     JSON.stringify(gameEvent.payload, null, 2)
                    // );
                    const previousPlayerId = gameState.currentPlayerId;
// console.log(`DEBUG_TURN (After Assign): Value of gameState.turnNumber: ${gameState.turnNumber}`); // DEBUG LOG
                    // Per user feedback (2025-05-29), responsibility for changing currentPlayerId is moved solely to TurnStarted event handler
                    // gameState.currentPlayerId = gameEvent.nextPlayerId; // Keep commented out
                    console.log(`GM_LOG_CID: TurnEnded event received. Player *would be* set to: ${gameEvent.nextPlayerId}`); // RESTORED LOG (Modified text for clarity)
                    // console.log(
                    //     `GM_DEBUG: Updated currentPlayerId from "${previousPlayerId}" to "${gameState.currentPlayerId}" based on TurnEnded event.`
                    // );
                    // DETAILED LOGGING FOR TurnEnded END
                    // gameState.turnNumber = gameEvent.turnNumber; // FIX: Property name mismatch? Use newTurnNumber logged earlier.
                    // REMOVED: Turn number update is now handled by TurnStarted based on user request to show player's individual turn count.
                    // gameState.turnNumber = gameEvent.TurnNumber ?? gameEvent.turnNumber ?? gameState.turnNumber;
                    // console.log(`GM_TURN_ENDED_DBG: Updated turnNumber to: ${gameState.turnNumber} using gameEvent.TurnNumber or gameEvent.turnNumber`); // DEBUG LOG
                    // console.log(`GM_PHASE_DBG: Before TurnEnded update - Phase: ${gameState.phase}`); // ADDED DEBUG LOG
                    gameState.phase = "TurnStart"; // Assume server moves phase
                    // console.log(`GM_PHASE_DBG: After TurnEnded update - Phase set to: ${gameState.phase}`); // ADDED DEBUG LOG

                    // Update player states (PP, EP, drawn card)
                    // console.log("GM_TRACE: Processing playerUpdates loop..."); // TRACE LOG BEFORE LOOP
                    gameEvent.playerUpdates?.forEach((update) => {
                        const player = getPlayerState(update.playerId);
                        if (player) {
                            console.log(
                                `GM_TRACE: Updating player ${update.playerId}: PP=${update.pp
                                }, EP=${update.ep}, HasDrawnCard=${!!update.drawnCard}`
                            ); // TRACE LOG INSIDE LOOP
                            player.pp = update.pp;
                            player.maxPP = update.maxPP;
                            player.ep = update.ep;
                            player.maxEP = update.maxEP;
                            player.turn = update.turn ?? player.turn; // ADDED: Update player's turn count
                            if (update.drawnCard) {
                                // HYDRATION: Create instance from DTO before adding to hand
                                const drawnCardInstance = createCardInstance(update.drawnCard);
                                if (drawnCardInstance) {
                                    drawnCardInstance.ownerId = player.id; // Ensure owner is set
                                    drawnCardInstance.location = 'hand'; // Ensure location is set
                                    player.hand.push(drawnCardInstance);
                                    console.log(
                                        `GM: Player ${update.playerId} drew ${drawnCardInstance.name}`
                                    );
                                } else {
                                    console.error(`GM: Failed to create instance for drawn card DTO for player ${update.playerId}:`, update.drawnCard);
                                }
                            }
                        }
                    });
                    // console.log("GM_TRACE: Finished playerUpdates loop."); // TRACE LOG AFTER LOOP
                    // Update active/opponent player references
                    gameState.activePlayer = getPlayerState(gameState.currentPlayerId);
                    gameState.opponentPlayer =
                        gameState.players.find(
                            (p) => p && p.id !== gameState.currentPlayerId
                        ) || null;

                    // Reset attack status for the new turn's player field
                    gameState.activePlayer?.field.forEach((f) => (f.canAttack = true));
                    // console.log("GM_TRACE: Reset isEndingTurn flag."); // TRACE LOG

                    // Also update button state immediately after processing TurnEnded
                    const endTurnButton = document.getElementById("end-turn-button");
                    if (endTurnButton) {
                        const isLocalPlayerNowTurn =
                            gameState.currentPlayerId === gameState.localPlayerId;
                        endTurnButton.disabled = !isLocalPlayerNowTurn;
                        // console.log(
                        //     `GM_TRACE: End Turn button disabled state set to ${!isLocalPlayerNowTurn} based on new turn.`
                        // );
                    }
                }
                // console.log("GM_TRACE: --- Exiting TurnEnded case ---"); // TRACE LOG CASE EXIT
                break;

            case "CardPlayed":
                {
                    // Payload might contain: { playerId: "...", cardPlayed: {...}, ppLeft: N, fieldUpdate: [...], handUpdate: [...] }
                    // Server sends the new state after the card play resolves.
                    // We might just need to update the specific player's state based on the payload.
                    // Removed payload check
                    const player = getPlayerState(gameEvent.playerId);
                    if (player) {
                        player.pp = gameEvent.ppLeft;
                        // Find the played card in hand and remove it
                        const handIndex = player.hand.findIndex(
                            (c) => c.uid === gameEvent.cardPlayed.uid
                        );
                        if (handIndex > -1) player.hand.splice(handIndex, 1);

                        // If it was a follower/amulet, create instance and add it to the field
                        if (gameEvent.cardPlayed.location === "field") {
                            // HYDRATION: Create instance from DTO before adding to field
                            const playedCardInstance = createCardInstance(gameEvent.cardPlayed);
                            if (playedCardInstance) {
                                // Ensure no duplicates if fieldUpdate also contains it (rare case?)
                                if (!player.field.some((f) => f.uid === playedCardInstance.uid)) {
                                    playedCardInstance.ownerId = player.id; // Ensure owner is set
                                    player.field.push(playedCardInstance);
                                    console.log(`GM: Added ${playedCardInstance.name} instance to field for player ${player.id}.`);
                                }
                            } else {
                                console.error(`GM: Failed to create instance for played card DTO for player ${player.id}:`, gameEvent.cardPlayed);
                            }
                        }
                        // Potentially update field/hand fully if payload provides complete lists? Simpler might be delta updates.
                        console.log(
                            `GM: Card ${gameEvent.cardPlayed?.name || 'Unknown'} played by ${gameEvent.playerId}.`
                        );
                    }
                }
                break;

            case "AttackOccurred":
                {
                    // Payload might contain: { attackerUid: "...", targetUid: "...", damageDealtToTarget: N, damageDealtToAttacker: N, attackerState: {...}, targetState: {...} }
                    // Update health of involved units based on server calculation.
                    // Removed payload check
                    const attacker = findCardInstanceByUid(gameEvent.attackerUid);
                    const target = findCardInstanceByUid(gameEvent.targetUid);

                    // HYDRATION FIX: Update specific properties instead of using Object.assign
                    if (attacker && gameEvent.attackerState) {
                        attacker.currentHealth = gameEvent.attackerState.currentHealth ?? attacker.currentHealth;
                        attacker.canAttack = gameEvent.attackerState.canAttack ?? attacker.canAttack;
                        // Add other relevant property updates from attackerState DTO if needed
                        console.log(
                            `GM: Attacker ${attacker.name} state updated (Health: ${attacker.currentHealth}, CanAttack: ${attacker.canAttack}) after attack.`
                        );
                         if (attacker.currentHealth <= 0) {
                             console.log(`GM_POST_ATTACK: Attacker ${attacker.name} destroyed by combat.`);
                             // Server should send UnitDestroyed event, no need to handle locally
                         }
                    }
                    if (target && gameEvent.targetState) {
                        target.currentHealth = gameEvent.targetState.currentHealth ?? target.currentHealth;
                        // Add other relevant property updates from targetState DTO if needed
                        console.log(
                            `GM: Target ${target.name} state updated (Health: ${target.currentHealth}) after attack.`
                        );
                         if (target.currentHealth <= 0) {
                              console.log(`GM_POST_ATTACK: Target ${target.name} destroyed by combat.`);
                             // Server should send UnitDestroyed or GameOver event
                         }
                        // Show damage number feedback?
                        if (gameEvent.damageDealtToTarget > 0) {
                             showDamageNumber(target.uid, gameEvent.damageDealtToTarget);
                         }
                         if (gameEvent.damageDealtToAttacker > 0 && attacker) {
                             showDamageNumber(attacker.uid, gameEvent.damageDealtToAttacker);
                         }
                    }
                }
                break;

            case "UnitEvolved":
                {
                    // Payload might contain: { playerId: "...", unitUid: "...", evolvedState: {...}, epLeft: N }
                    // Removed payload check
                    const player = getPlayerState(gameEvent.playerId);
                    const unit = findCardInstanceByUid(gameEvent.unitUid);
                    if (player) player.ep = gameEvent.epLeft;
                    if (unit && gameEvent.evolvedState) {
                         // HYDRATION FIX: Update specific properties instead of using Object.assign
                         unit.isEvolved = gameEvent.evolvedState.isEvolved ?? true; // Assume true if state provided
                         unit.currentAttack = gameEvent.evolvedState.currentAttack ?? unit.currentAttack;
                         unit.currentHealth = gameEvent.evolvedState.currentHealth ?? unit.currentHealth;
                         unit.maxHealth = gameEvent.evolvedState.maxHealth ?? unit.maxHealth; // Update max health too
                         unit.canAttack = gameEvent.evolvedState.canAttack ?? unit.canAttack; // Update canAttack status
                         // Potentially update keywords or other properties based on evolvedState DTO
                         console.log(
                            `GM: Unit ${unit.name} evolved by ${gameEvent.playerId}. New State: Atk=${unit.currentAttack}, HP=${unit.currentHealth}/${unit.maxHealth}, Evolved=${unit.isEvolved}, CanAttack=${unit.canAttack}`
                        );
                    }
                }
                break;

            case "UnitDestroyed":
                {
                    // Payload might contain: { unitUid: "..." }
                    // Removed payload check
                    const unit = findCardInstanceByUid(gameEvent.unitUid);
                    if (unit) {
                        handleDestruction(unit); // Fixed call
                    } // Closing brace for if(unit)
                }
                break; // Closing break for case 'UnitDestroyed'

            case "EpUpdate":
                {
                    // Payload: { sourcePlayerId: string, newEpValue: int, newMaxEpValue: int } // Event likely uses sourcePlayerId
                    // console.log("GM: Processing EpUpdate event:", gameEvent); // Log the event itself
                    const playerToUpdate = getPlayerState(gameEvent.sourcePlayerId); // FIX: Use sourcePlayerId
                    if (playerToUpdate) {
                        // Check for properties directly on the event object
                        if (gameEvent.newEpValue !== undefined) {
                            playerToUpdate.ep = gameEvent.newEpValue;
                        }
                        if (gameEvent.newMaxEpValue !== undefined) {
                            playerToUpdate.maxEP = gameEvent.newMaxEpValue;
                        }
                        // console.log(
                        //     `GM: Updated player ${playerToUpdate.id} EP to ${playerToUpdate.ep}/${playerToUpdate.maxEP}`
                        // );
                    } else {
                        console.warn(`GM: EpUpdate event received for invalid player: ${gameEvent.sourcePlayerId}`); // FIX: Use sourcePlayerId in log
                    }
                }
                break; // End EpUpdate case

            case "StatChange":
                {
                    // Payload example: { unitId: "...", newAttack: N, newHealth: N }
                    // Assumes server sends the new base stats after modification (e.g., buffs, debuffs)
                    // Removed payload check
                    const unit = findCardInstanceByUid(gameEvent.unitId);
                    if (unit) {
                        // Update both base and current stats for consistency
                        unit.attack = gameEvent.newAttack;
                        unit.health = gameEvent.newHealth;
                        unit.currentAttack = gameEvent.newAttack; // Reflect change in current stats too
                        unit.currentHealth = gameEvent.newHealth; // Reflect change in current stats too
                        console.log(
                            `GM: Unit ${unit.name} (UID: ${unit.uid}) stats changed to A:${unit.attack}/H:${unit.health}`
                        );
                    }
                }
                break;

            case "MulliganStarted":
                // Payload: { mulliganTimer?: int } // Direct DTO properties (optional timer)
                // console.log("GM: Mulligan phase started.", gameEvent); // Log the event itself
                gameState.phase = "Mulligan";
                // UI should now allow mulligan actions. Timer handling (if any) would use gameEvent.mulliganTimer
                // console.log(`GM: Mulligan timer (if provided): ${gameEvent.mulliganTimer}`);
                break;

            case "PlayerMulliganCompleted":
                // Payload: { sourcePlayerId: "..." } // DTO likely has sourcePlayerId based on BasePlayerEventDto
                // console.log(`GM: PlayerMulliganCompleted event received for ${gameEvent.sourcePlayerId}`); // Access sourcePlayerId directly
                const completedPlayerId = gameEvent.sourcePlayerId; // Use sourcePlayerId from the event object
                if (completedPlayerId === 'player1') {
                    gameState.player1MulliganComplete = true;
                    // console.log("GM: Updated gameState.player1MulliganComplete to true.");
                } else if (completedPlayerId === 'player2') {
                    gameState.player2MulliganComplete = true;
                    // console.log("GM: Updated gameState.player2MulliganComplete to true.");
                } else {
                     console.warn(`GM: PlayerMulliganCompleted event received for unknown player ID: ${completedPlayerId}`);
                }
                // Re-render to reflect the change (e.g., hide the mulligan button for the completed player)
                renderGame(gameState);
                break;

            case "GameOver":
                // Payload: { winnerId: string, reason?: string } // Direct DTO properties
                // console.log("GM: Game over event received.", gameEvent); // Log the event itself
                gameState.gameOver = true;
                gameState.phase = "GameOver";
                gameState.winner = gameEvent.winnerId; // Access winnerId directly
                // Update UI to show game over screen with winner/loser info
                alert(
                    `游戏结束！ 玩家 ${gameState.winner} 获胜！ (${gameEvent.reason || "原因未知" // Access reason directly
                    })`
                ); // Simple alert for now
                break;

            case "TurnStarted": // <-- 新增 Case
                {
                    console.log(`%cGM_EVENT_HANDLER: ==> Entered 'TurnStarted' case`, 'color: blue; font-weight: bold;', gameEvent); // <<< ADDED ENTRY LOG >>>
                    // Payload: { startingPlayerId: string, turnNumber: int } // DTO properties
                    // console.log(`GM: TurnStarted event received for player ${gameEvent.startingPlayerId}, turn ${gameEvent.turnNumber}. Setting phase to Main.`); // Access properties directly
                    // <<< DEBUGGING START - ADDED LOGS >>>
                    console.log(`GM_TURN_STARTED_DBG: Received TurnStarted event. Raw event data:`, JSON.stringify(gameEvent, null, 2));
                    console.log(`GM_TURN_STARTED_DBG: Accessing event properties (camelCase): currentPlayerId=${gameEvent.currentPlayerId}, phase=${gameEvent.phase}, turnNumber=${gameEvent.turnNumber}`); // Log camelCase access
                    // <<< DEBUGGING END >>>

                    // --- FIX: Use correct properties received from server (camelCase due to SignalR JSON serialization) ---
                    const incomingPlayerId = gameEvent.currentPlayerId; // FIX: Use camelCase
                    const incomingPhase = gameEvent.phase;           // FIX: Use camelCase
                    const incomingTurnNumber = gameEvent.turnNumber;     // FIX: Use camelCase

                    // Validate the received camelCase properties
                    if (!incomingPlayerId || !incomingPhase || incomingTurnNumber === undefined) {
                        console.error("GM Error: Invalid or missing data in TurnStarted event (currentPlayerId, phase, or turnNumber):", gameEvent); // Update error message expectation
                        return; // Stop processing if essential data is missing
                    }

                    // Update core game state properties using validated camelCase data
                    console.log(`GM_EVENT_HANDLER: ==> BEFORE update in 'TurnStarted': gameState.currentPlayerId = ${gameState.currentPlayerId}`); // <<< ADDED PRE-UPDATE LOG >>>
                    gameState.currentPlayerId = incomingPlayerId;
                    // <<< 新日志：检查赋值后的即时值 >>>
                    console.log(`%cGM_EVENT_HANDLER: ==> AFTER update in 'TurnStarted': gameState.currentPlayerId = ${gameState.currentPlayerId}`, 'color: green; font-weight: bold;');
                    console.log(`GM_LOG_CID: Updated currentPlayerId to ${gameState.currentPlayerId} from TurnStarted event.`); // 现有日志
                    gameState.phase = incomingPhase; // Use phase from server
                    gameState.turnNumber = incomingTurnNumber; // Use turn number from server

                    console.log(`GM_TURN_STARTED_DBG: Updated gameState: currentPlayerId=${gameState.currentPlayerId}, phase=${gameState.phase}, turnNumber=${gameState.turnNumber}`);
                    // Update active/opponent references might also be prudent here if not covered by TurnEnded
                    console.log(`GM_TURN_STARTED_DBG: Before updatePlayerReferences equivalent`); // New Log
                    gameState.activePlayer = getPlayerState(gameState.currentPlayerId); // currentPlayerId should be correct from TurnEnded
                    gameState.opponentPlayer = gameState.players.find(p => p && p.id !== gameState.currentPlayerId);
                    // Log equivalent to GM_UPDATE_REFS_DBG
                    console.log(`GM_TURN_STARTED_DBG: After updatePlayerReferences equivalent. Active: ${gameState.activePlayer?.id ?? 'null'}, Opponent: ${gameState.opponentPlayer?.id ?? 'null'}`); // New Log

                    // --- FIX: Increment the active player's specific turn count ---
                    if (gameState.activePlayer) {
                        // Initialize turn to 1 if it's 0 or undefined, otherwise increment
                        gameState.activePlayer.turn = (gameState.activePlayer.turn || 0) + 1;
                        console.log(`GM_TURN_STARTED_DBG: Incremented/Set activePlayer (${gameState.activePlayer.id}) turn to: ${gameState.activePlayer.turn}`);
                    } else {
                         console.warn(`GM_TURN_STARTED_DBG: Cannot increment/set activePlayer turn because activePlayer is null.`);
                    }
                    // --- END FIX ---

                    console.log(`GM_TURN_STARTED_DBG: Before updateActionButtonsState equivalent`); // New Log
                    // Ensure End Turn button state is correct based on current player after TurnStarted confirms the turn transition
                    const endTurnButton = document.getElementById("end-turn-button");
                    let displayStyle = 'none'; // Default for button logic debug
                    let disabledState = true; // Default for button logic debug
                    if (endTurnButton) {
                       const isLocalPlayerTurn = gameState.currentPlayerId === gameState.localPlayerId;
                       disabledState = !isLocalPlayerTurn; // Capture state
                       endTurnButton.disabled = disabledState; // Apply state
                       displayStyle = (gameState.phase === 'Main' || gameState.phase === 'TurnStart') ? 'inline-block' : 'none'; // Capture display style based on phase
                       endTurnButton.style.display = displayStyle; // Apply display style
                       // console.log(`GM_TRACE: End Turn button disabled state set to ${!isLocalPlayerTurn} after TurnStarted event.`);
                    }
                     // Log equivalent to GM_UPDATE_BUTTONS_DBG
                    console.log(`GM_TURN_STARTED_DBG: After updateActionButtonsState equivalent. EndTurnButton display set to: ${displayStyle}, disabled set to: ${disabledState}. IsLocal: ${gameState.currentPlayerId === gameState.localPlayerId}, Phase: ${gameState.phase}`); // New Log
                    // <<< 新日志：检查处理程序结束时的最终值 >>>
                    console.log(`%cGM_EVENT_HANDLER: ==> END of 'TurnStarted' case: gameState.currentPlayerId = ${gameState.currentPlayerId}`, 'color: blue; font-weight: bold;');

                    // <<< ADD THIS CALL (REVERTED) >>>
                    // Now that the gameState is updated for the new turn, call startTurn from turnManager
                    // to execute client-side start-of-turn logic (draw, PP, EP, enable controls/AI).
                    // console.log(`%cGM_EVENT_HANDLER: Calling turnManager.startTurn() for player ${gameState.currentPlayerId}...`, 'color: orange; font-weight: bold;');
                    // try {
                         // Ensure startTurn is imported at the top of gameManager.js
                         // import { startTurn } from "./turnManager.js"; // Should already be there
                         // startTurn(); // <<< REVERTED THE CALL
                    // } catch (startTurnError) {
                    //     console.error("GM Error: Exception during turnManager.startTurn() call:", startTurnError);
                    //     alert(`处理回合开始逻辑时出错: ${startTurnError.message}`);
                         // Potentially set game state to error or try to recover?
                    // }
                    // <<< END ADDED CALL (REVERTED) >>>
                 }
                 break; // <-- 不要忘记 break

            case "PlayerActionError":
                // Payload: { playerId: string, actionType?: string, message: string } // Direct DTO properties
                console.warn(
                    `GM: Player action error received for ${gameEvent.playerId} (${gameEvent.actionType}): ${gameEvent.message}` // Access directly
                );
                // Display the error message to the specific player (if it's the local player)
                if (gameEvent.playerId === gameState.localPlayerId) {
                    alert(`操作错误: ${gameEvent.message}`); // Access directly
                }
                break;

            // --- ADDED Event Handlers for Card Play Consequences ---
            case "PpUpdate":
                {
                    // Payload: { sourcePlayerId: string, currentPp: int, maxPp: int } // Event likely uses sourcePlayerId
                    // console.log("GM: Processing PpUpdate event:", gameEvent); // Log the event itself
                    const playerToUpdate = getPlayerState(gameEvent.sourcePlayerId); // FIX: Use sourcePlayerId
                    if (playerToUpdate) {
                         // Check for properties directly on the event object
                        if (gameEvent.currentPp !== undefined) {
                            playerToUpdate.pp = gameEvent.currentPp;
                        }
                         if (gameEvent.maxPp !== undefined) {
                            playerToUpdate.maxPP = gameEvent.maxPp;
                         }
                        // console.log(`GM: Updated player ${playerToUpdate.id} PP to ${playerToUpdate.pp}/${playerToUpdate.maxPP}`);
                    } else {
                        console.warn(`GM: PpUpdate event received for invalid player: ${gameEvent.sourcePlayerId}`); // FIX: Use sourcePlayerId in log
                    }
                }
                break;
            case "HandUpdate":
                {
                    // Payload: { sourcePlayerId: string, discardedCardUid?: string, drawnCards?: CardDto[] } // Event likely uses sourcePlayerId
                    // console.log("GM: Processing HandUpdate event:", gameEvent); // Log the event itself
                    const playerToUpdate = getPlayerState(gameEvent.sourcePlayerId); // FIX: Use sourcePlayerId

                    if (playerToUpdate) {
                        // Handle card removal
                        if (gameEvent.discardedCardUid) { // Access discardedCardUid directly
                            const cardIndex = playerToUpdate.hand.findIndex(c => c && c.uid === gameEvent.discardedCardUid); // Use direct access
                            if (cardIndex > -1) {
                                const removedCard = playerToUpdate.hand.splice(cardIndex, 1)[0];
                                // console.log(`GM: Removed card ${removedCard?.name} (UID: ${gameEvent.discardedCardUid}) from player ${playerToUpdate.id}'s hand.`); // Use direct access in log
                            } else {
                                console.warn(`GM: HandUpdate requested removal of card UID ${gameEvent.discardedCardUid}, but it was not found in player ${playerToUpdate.id}'s hand.`); // Use direct access in log
                            }
                        }

                        // Handle card addition (drawn cards)
                        if (Array.isArray(gameEvent.drawnCards) && gameEvent.drawnCards.length > 0) { // Access drawnCards directly
                            // console.log(`GM: Adding ${gameEvent.drawnCards.length} drawn card(s) to player ${playerToUpdate.id}'s hand.`);
                            for (const cardDto of gameEvent.drawnCards) { // Iterate direct access array
                                const newCardInstance = createCardInstance(cardDto); // Use cardDto from loop
                                if (newCardInstance) {
                                    newCardInstance.ownerId = playerToUpdate.id;
                                    newCardInstance.location = 'hand';
                                    // Avoid duplicates - ADDED DETAILED LOGGING
                                    const cardAlreadyExists = playerToUpdate.hand.some(c => c && c.uid === newCardInstance.uid);
                                    // Log current hand state *before* adding
                                    console.log(`GM_HAND_UPDATE_DBG: Checking for duplicate UID ${newCardInstance.uid} (${newCardInstance.name}) in player ${playerToUpdate.id}'s hand. Current hand UIDs: [${playerToUpdate.hand.map(c => c?.uid).join(', ')}]`);
                                    if (!cardAlreadyExists) {
                                        playerToUpdate.hand.push(newCardInstance);
                                        console.log(`GM_HAND_UPDATE_DBG: Added card ${newCardInstance.name} (UID: ${newCardInstance.uid}) to player ${playerToUpdate.id}'s hand.`);
                                    } else {
                                        // Log the existing card that caused the conflict
                                        const existingCard = playerToUpdate.hand.find(c => c && c.uid === newCardInstance.uid);
                                        console.warn(`GM_HAND_UPDATE_DBG: HandUpdate attempted to add duplicate card UID ${newCardInstance.uid} (${newCardInstance.name}) for player ${playerToUpdate.id}. Existing card:`, existingCard);
                                    }
                                } else {
                                    console.error(`GM: Failed to create instance for drawn card via HandUpdate for player ${playerToUpdate.id}:`, cardDto); // Use cardDto from loop in error log
                                }
                            }
                        }
                    } else {
                        console.warn(`GM: HandUpdate event received for invalid player: ${gameEvent.sourcePlayerId}`); // FIX: Use sourcePlayerId in log
                    }
                }
                break;

            case "UnitPlayed": // Handles both Followers and Amulets based on UnitStateDto
                {
                    // Payload: { playerId: string, unitData: UnitStateDto, sourceCardUid?: string } // Direct DTO properties
                    // console.log("GM: Processing UnitPlayed event:", gameEvent); // Log the event itself
                    const ownerPlayer = getPlayerState(gameEvent.playerId); // Access playerId directly
                    if (ownerPlayer && gameEvent.unitData) { // Access unitData directly
                        // HYDRATION: Create full instance from the UnitStateDto
                         const unitInstance = createCardInstance(gameEvent.unitData); // Use direct access
                         if(unitInstance) {
                            unitInstance.ownerId = ownerPlayer.id; // Ensure owner is set
                            unitInstance.location = 'field'; // Ensure location
                             // Hydrate dynamic state from the DTO
                             unitInstance.canAttack = gameEvent.unitData.canAttack ?? false; // Access unitData properties directly
                             unitInstance.currentAttack = gameEvent.unitData.currentAttack ?? unitInstance.attack; // Access unitData properties directly
                             unitInstance.currentHealth = gameEvent.unitData.currentHealth ?? unitInstance.health; // Access unitData properties directly
                             unitInstance.isEvolved = gameEvent.unitData.isEvolved ?? false; // Access unitData properties directly
                             // Hydrate other potential dynamic properties from UnitStateDto if added later (e.g., effects, keywords applied)
                             // --- DEBUG LOG START ---
                             console.log(`GM_UNITPLAYED_DEBUG: Hydrated Unit Instance for UID ${unitInstance.uid}: Atk=${unitInstance.currentAttack}, HP=${unitInstance.currentHealth}. Raw unitData:`, gameEvent.unitData);
                             // --- DEBUG LOG END ---
                            // Avoid adding duplicates if server sends redundant events (unlikely but safe)
                            if (!ownerPlayer.field.some(f => f && f.uid === unitInstance.uid)) { // Added null check for field card 'f'
                                 if (ownerPlayer.field.length < MAX_FIELD_SIZE) {
                                    ownerPlayer.field.push(unitInstance);
                                    // console.log(`GM: Added unit ${unitInstance.name} (UID: ${unitInstance.uid}, Type: ${unitInstance.type}) to player ${ownerPlayer.id}'s field.`);
                                 } else {
                                     console.warn(`GM: UnitPlayed event received for ${unitInstance.name}, but player ${ownerPlayer.id}'s field is full (${ownerPlayer.field.length}/${MAX_FIELD_SIZE}). Unit not added client-side.`);
                                     // Server state is authoritative, but log discrepancy. UI might look wrong.
                                 }
                            } else {
                                console.warn(`GM: UnitPlayed event received for unit UID ${unitInstance.uid} which is already present on player ${ownerPlayer.id}'s field.`);
                            }
                         } else {
                              console.error(`GM: Failed to create instance for UnitPlayed DTO for player ${ownerPlayer.id}:`, gameEvent.unitData); // Use direct access in error
                         }
                    } else {
                         console.warn(`GM: UnitPlayed event received for invalid player or missing unit data. PlayerID: ${gameEvent.playerId}, UnitData: ${!!gameEvent.unitData}`); // Update warning message
                    }
                }
                break;

            case "SpellPlayed": // Basic handler, might need more detail later
                {
                    // Payload: { playerId: string, spellCardId: string, originalCardUid: string, targetUid?: string } // Direct DTO properties
                    // console.log("GM: Processing SpellPlayed event:", gameEvent); // Log the event itself
                    // Spells usually don't create a persistent entity on the board.
                    // We already handle PP update via PpUpdate and card removal via HandUpdate.
                    // This handler might be used for visual effects or logging.
                    // console.log(`GM: Spell (Original UID: ${gameEvent.originalCardUid}, CardID: ${gameEvent.spellCardId}) played by ${gameEvent.playerId}. Target: ${gameEvent.targetUid ?? 'None'}`); // Access directly
                    // Future: Trigger spell animation/VFX based on event properties?
                }
                break;
            // --- END Added Event Handlers ---

            // TODO: Add more event types from SignalR hub if needed later
            // case 'ReceiveGameState': // This should likely be handled separately in main.js on initial connect? Or here?
            //     // Payload would be the full gameState object
            //     console.log("GM: Received full game state update.");
            //     Object.assign(gameState, gameEvent.payload);
            //     // Need to update active/opponent player references after full update
            //     gameState.activePlayer = getPlayerState(gameState.currentPlayerId);
            //     gameState.opponentPlayer = gameState.players.find(p => p.id !== gameState.currentPlayerId);
            //     break;

            case "DeckUpdate":
                {
                    // Payload: { sourcePlayerId: string, deckCount: int } // Event likely uses sourcePlayerId
                    // console.log("GM: Processing DeckUpdate event:", gameEvent); // Log the event itself
                    const playerToUpdate = getPlayerState(gameEvent.sourcePlayerId); // FIX: Use sourcePlayerId
                    if (playerToUpdate) {
                        if (gameEvent.deckCount !== undefined) { // Access deckCount directly
                            playerToUpdate.deckCount = gameEvent.deckCount;
                            // console.log(`GM: Updated player ${playerToUpdate.id} deck count to ${playerToUpdate.deckCount}`);
                        } else {
                             // Log if deckCount is missing, even if player is valid
                             console.warn(`GM: DeckUpdate event for player ${gameEvent.playerId} is missing deckCount.`);
                        }
                    } else {
                        console.warn(`GM: DeckUpdate event received for invalid player: ${gameEvent.sourcePlayerId}`); // FIX: Use sourcePlayerId in log
                    }
                }
                break;

            default:
                console.warn(
                    `GAME MANAGER: Unhandled game event type received: ${gameEvent.type}`
                ); // MODIFIED: Use gameEvent.type
        } // Closing brace for switch
    } catch (error) {
        // TRACE LOG INSIDE CATCH
        console.error(
            `GM_TRACE: !!! Error caught in processGameEvent (Type: ${gameEvent.type})`,
            error
        );
        console.error(
            `GAME MANAGER: Error processing game event type ${gameEvent.type}:`,
            error
        ); // MODIFIED: Use gameEvent.type
        // Potentially display an error to the user via UI function
        alert(`处理游戏事件 ${gameEvent.type} 时出错: ${error.message}`); // MODIFIED: Use gameEvent.type
    }

    // Always re-render the game state after processing an event
    // Ensure gameState and renderGame are correctly referenced/imported
    //console.log(`GM_PROCESS_EVENT_DBG: Reached end of switch for event ${gameEvent.type}. Attempting final render.`); // New Log
    // <<< ADD LOGGING BEFORE RENDER >>>
    //console.log(`GM_TURN_STARTED_DBG: Final gameState before renderGame:`, JSON.stringify(gameState, null, 2));
    // <<< END LOGGING BEFORE RENDER >>>
    // console.log("GM_TRACE: Before final renderGame call."); // TRACE LOG BEFORE RENDER
    if (typeof renderGame === "function") {
        // console.log("GAME MANAGER: Game state rendered after event processing.");
        try {
            // Pass the most current player ID explicitly to renderGame
            renderGame(gameState, gameState.currentPlayerId);
        } catch (renderError) {
             // Restore original catch block content if needed, or keep simpler version
             console.error(`GM_RENDER_ERR: Error during renderGame call after event ${gameEvent.type}:`, renderError); // Restore original error message format
             alert(`渲染错误！事件: ${gameEvent.type}. 错误: ${renderError.message}. 请检查控制台.`); // Restore original alert
        }
        // console.log("GM_TRACE: After final renderGame call."); // TRACE LOG AFTER RENDER
   } else {
       console.error(
           "GAME MANAGER: renderGame function is not available for UI update after event processing."
       );
        alert("发生内部错误：无法渲染游戏状态。请检查控制台。"); // Add user feedback here too
   }
   // --- 添加日志点 ---
   console.log(`GM_PROCESS_EVENT_DBG: Reached end of processing logic for event ${gameEvent.type}, AFTER render call.`);
   // --- 结束添加 ---
   console.log(`GM_PROCESS_EVENT_DBG: Finished processing event ${gameEvent.type}.`); // New Log
   console.log(`%cGM_REENTRANCY_DBG: <<< Exiting processGameEvent (${eventTypeForLog})`, 'color: magenta; font-weight: bold;', new Date().toISOString()); // ADDED EXIT LOG (using variable from start)
} // Closing brace for processGameEvent
