// src/turnManager.js - 回合流程控制

// 导入依赖
import { gameState, getPlayerState } from './gameManager.js'; // Import state and getter
import { renderGame } from './ui.js'; // Import renderGame
import { runAILogic } from './ai.js'; // Import AI logic entry point

console.log("turnManager.js loaded");

// 开始回合
export function startTurn() {
    console.log(`TM: --- Starting turn ${gameState.turnNumber} for player ${gameState.currentPlayerId} ---`);
    console.log("TM_TRACE_ID: Entering startTurn - Current secondPlayerId:", gameState.secondPlayerId); // ADDED LOG

    // Check for game over
    if (gameState.gameOver) {
        console.log("TM: Game is over. Halting turn start.");
        return;
    }

    // Update active/opponent player references (important!)
    gameState.activePlayer = getPlayerState(gameState.currentPlayerId);
    // Use first/second player IDs to correctly identify the opponent
    gameState.opponentPlayer = getPlayerState(gameState.currentPlayerId === gameState.firstPlayerId ? gameState.secondPlayerId : gameState.firstPlayerId);

    if (!gameState.activePlayer) {
         console.error(`TM: Critical error - Active player ${gameState.currentPlayerId} not found!`);
         return;
    }

    // 1. Increase Max PP (if less than 10) - Happens *every* turn start, including T1
    // This allows for "on Max PP increase" trigger effects even on Turn 1
    if (gameState.activePlayer.maxPP < 10) { // Condition removed: gameState.turnNumber > 1 &&
        gameState.activePlayer.maxPP++;
        console.log(`TM: Player ${gameState.activePlayer.id} maxPP increased to ${gameState.activePlayer.maxPP} (Turn ${gameState.turnNumber})`);
    }
    // 2. Refill PP to Max PP
    gameState.activePlayer.pp = gameState.activePlayer.maxPP; // Refill happens every turn
    console.log(`TM: Player ${gameState.activePlayer.id} PP updated to ${gameState.activePlayer.pp}/${gameState.activePlayer.maxPP}`);

    // --- Evolution Point Handling (Using Flag) ---
    // Use firstPlayerId to determine the correct unlock turn
    const unlockTurn = (gameState.activePlayer.id === gameState.firstPlayerId) ? 5 : 4;
    const isEvolveUnlockedNow = gameState.turnNumber >= unlockTurn; // Check if potentially unlocked this turn or earlier
    // Use optional chaining for safety, though hasUnlockedEvolution should exist now
    console.log(`TM_DBG: Turn ${gameState.turnNumber}, Player ${gameState.activePlayer.id}. UnlockTurn: ${unlockTurn}, isEvolveUnlockedNow: ${isEvolveUnlockedNow}, Has Flag: ${gameState.activePlayer?.hasUnlockedEvolution}`);

    // Logic for the specific turn EP unlocks using the flag
// --- DETAILED EP DEBUG ---
        console.log(`TM_DETAILED_DBG: Pre-Check State: Turn=${gameState.turnNumber}, PlayerID=${gameState.activePlayer?.id}, UnlockTurn=${unlockTurn}, isEvolveUnlockedNow=${isEvolveUnlockedNow}, hasUnlockedEvolution=${gameState.activePlayer?.hasUnlockedEvolution}`);
        // --- END DETAILED EP DEBUG ---
    if (isEvolveUnlockedNow && !gameState.activePlayer?.hasUnlockedEvolution) {
        // This is the exact turn evolution unlocks
        // Use firstPlayerId to determine the correct starting max EP
        const targetMaxEP = (gameState.activePlayer.id === gameState.firstPlayerId) ? 2 : 3;
        console.log(`TM_DBG: First unlock turn detected! Setting EP/MaxEP to ${targetMaxEP}.`);
        gameState.activePlayer.maxEP = targetMaxEP; // Set maxEP
        gameState.activePlayer.ep = targetMaxEP;    // Set current EP
        gameState.activePlayer.hasUnlockedEvolution = true; // Set the flag
        console.log(`TM: Player ${gameState.activePlayer.id} EVOLUTION UNLOCKED. State: ${gameState.activePlayer.ep}/${gameState.activePlayer.maxEP}, Flag Set: ${gameState.activePlayer.hasUnlockedEvolution}`);
    } else if (gameState.activePlayer?.hasUnlockedEvolution) {
        // Subsequent turns after unlocking - do nothing to EP/maxEP
        console.log(`TM_DBG: Evolution already unlocked. EP remains ${gameState.activePlayer.ep}/${gameState.activePlayer.maxEP}.`);
    } else {
        // Turns before unlocking - do nothing
        console.log(`TM_DBG: EP is not unlocked yet.`);
    }

    // Update canEvolveThisTurn based on the flag and current EP
    gameState.activePlayer.canEvolveThisTurn = gameState.activePlayer.hasUnlockedEvolution && gameState.activePlayer.ep > 0;
    console.log(`TM: Player ${gameState.activePlayer.id} canEvolveThisTurn set to: ${gameState.activePlayer.canEvolveThisTurn} (Unlocked Flag: ${gameState.activePlayer?.hasUnlockedEvolution}, Current EP: ${gameState.activePlayer.ep})`);
    // --- End Evolution Point Handling (Using Flag) ---

    // 3. Reset Field Attack State (Ready followers to attack)
    if (gameState.activePlayer.field && gameState.activePlayer.field.length > 0) {
        console.log(`TM: Resetting attack state for ${gameState.activePlayer.id}'s field...`);
        gameState.activePlayer.field.forEach(card => {
             if (card) { // Ensure card exists
                card.canAttack = true; // Followers that survived the previous turn can now attack
                // TODO: Consider Rush/Storm abilities later
             }
        });
    }

    // 4. Draw Card(s)
    // Determine draw count (1 normally, 2 for second player's first turn - using turn number)
    // Check if it's the *second* player's first turn using the stored ID
    // --- DETAILED DRAW LOGGING ---
    const currentPId = gameState.currentPlayerId;
    const secondPId = gameState.secondPlayerId;
    const turnNum = gameState.turnNumber;
    console.log(`TM_DRAW_CHECK: CurrentPlayer=${currentPId}, SecondPlayer=${secondPId}, Turn=${turnNum}`);
    const isPlayer2FirstTurn = (currentPId === secondPId && turnNum === 1);
    console.log(`TM_DRAW_CHECK: isPlayer2FirstTurn calculated as: ${isPlayer2FirstTurn}`);
    // --- END DETAILED DRAW LOGGING ---
    const cardsToDraw = isPlayer2FirstTurn ? 2 : 1;
    console.log(`TM: Player ${gameState.activePlayer.id} intends to draw ${cardsToDraw} card(s)...`);
    gameState.activePlayer.drawCard(cardsToDraw); // Assumes drawCard handles deck out/hand full

    // 5. Set Game Phase to Main <<<<< THE FIX
    gameState.gamePhase = 'Main';
    console.log(`TM: Game phase set to: ${gameState.gamePhase}`);

    // --- TODO: Trigger Start of Turn Effects here ---
    // executeEffects(gameState.activePlayer.field, 'StartOfTurn', gameState.activePlayer.id);
    // executeEffects(gameState.opponentPlayer.field, 'StartOfOpponentTurn', gameState.opponentPlayer.id);


    // 6. Update UI
    console.log("TM: Calling renderGame after starting turn.");
    renderGame(gameState); // Update UI with new PP, hand, etc.

    // 7. Activate Player/AI
    if (gameState.activePlayer.isAI) { // Use the isAI flag from player state
        console.log("TM: AI Turn: Scheduling AI logic execution...");
        // Call runAILogic from ai.js, passing the current game state
        // Add a short delay to make it feel less instantaneous
        setTimeout(() => runAILogic(gameState), 500); // MODIFIED LINE
    } else {
        // Player's turn
        const endTurnButton = document.getElementById('end-turn-button');
        if(endTurnButton) {
            endTurnButton.disabled = false; // Enable button
             console.log("TM: Player Turn: End turn button enabled.");
        } else {
             console.error("TM: End turn button not found!");
        }
    }
}

// 结束回合
// // 结束回合 (功能已废弃，由服务器事件驱动)
// export function endTurn() {
//     console.log(`TM: Ending turn for player ${gameState.currentPlayerId}...`);
//
//     // Check for game over before proceeding
//     if (gameState.gameOver) {
//          console.log("TM: Game is over. Halting turn end.");
//          return;
//     }
//
//     // Check if activePlayer is valid (should be set by startTurn)
//     // Use getPlayerState for safety, though gameState.activePlayer should be correct if startTurn worked
//     const currentPlayerToEnd = getPlayerState(gameState.currentPlayerId);
//     if (!currentPlayerToEnd) {
//         console.error(`TM: Cannot end turn, active player ${gameState.currentPlayerId} not found in gameState.players!`);
//         return; // Avoid errors if state is somehow corrupt
//     }
//
//     try { // ADDED try block
//         // Disable end turn button (regardless of AI or player)
//         const endTurnButton = document.getElementById('end-turn-button');
//         if (endTurnButton) {
//             endTurnButton.disabled = true;
//             console.log("TM: End turn button disabled.");
//         }
//
//
//     // TODO: Trigger End of Turn Effects for the player whose turn is ending
//     // executeEffects(currentPlayerToEnd.field, 'EndOfTurn', currentPlayerToEnd.id);
//
//     // Clear transient states (like selections) at end of turn
//     gameState.selectedCardUid = null;
//     gameState.selectedAttackerUid = null;
//     console.log("TM: Cleared selections.");
//
//
//     // Switch Player ID  <<< !!! CONFLICTING LOCAL LOGIC !!! >>>
//     const oldPlayerId = gameState.currentPlayerId;
//     // gameState.currentPlayerId = oldPlayerId === 'player1' ? 'player2' : 'player1'; // <<< CONFLICTING LINE COMMENTED OUT
//     console.log(`TM (Obsolete): Attempted to switch current player ID locally to ${oldPlayerId === 'player1' ? 'player2' : 'player1'}.`);
//
//     // Update active/opponent references in gameState (important for next startTurn)
//     // gameState.activePlayer = getPlayerState(gameState.currentPlayerId); // Obsolete
//     // gameState.opponentPlayer = getPlayerState(oldPlayerId); // Obsolete
//
//
//     // Increment Turn Number only AFTER the second player finishes their turn.
//     console.log(`TM_TURN_INC_CHECK (Obsolete): Ending turn for ${oldPlayerId}. Current turn: ${gameState.turnNumber}. Second player is ${gameState.secondPlayerId}. Checking if ended player was second player to increment turn.`); // MODIFIED LOG
//     if (oldPlayerId === gameState.secondPlayerId) { // **** THE FIX: Check if the player who JUST FINISHED was the second player ****
//         // If the player who just finished IS the second player, advance the game turn counter.
//         // This ensures the turn number advances from 1 to 2 only after the second player completes their actions in turn 1.
//         // gameState.turnNumber++; // Obsolete
//         console.log(`TM (Obsolete): Would have advanced game turn number.`);
//     }
//     // If the first player finished, the game turn number does not advance yet.
//
//     // Set phase for the *next* turn start
//     // gameState.gamePhase = 'TurnStart'; // Obsolete
//     console.log(`TM (Obsolete): Would have set game phase to TurnStart for next turn.`);
//
//     // Update UI immediately before the delay to show opponent's turn starting etc.
//     console.log("TM (Obsolete): Would have called renderGame after ending turn actions.");
//     // renderGame(gameState); // Obsolete
//
//     // Schedule the start of the next turn
//     // Using setTimeout prevents potential deep call stacks and allows UI to update
//     console.log("TM (Obsolete): Would have scheduled startTurn for the new player...");
//     //    setTimeout(startTurn, 50); // Obsolete
//     } catch (error) { // ADDED catch block
//         console.error("TM: CRITICAL ERROR caught within obsolete endTurn:", error);
//         console.error("TM: Error Stack Trace:", error.stack); // Add stack trace
//         try {
//             // Use try...catch to prevent stringify itself from erroring
//             console.log("TM: GameState at time of error:", JSON.stringify(gameState, null, 2));
//         } catch (stringifyError) {
//             console.error("TM: Error stringifying gameState:", stringifyError);
//             console.log("TM: Raw gameState object:", gameState); // Attempt to log raw object
//         }
//         // Optional: Notify the user more clearly, but logging is the primary goal for debugging
//         alert("发生严重错误，回合无法结束（在已废弃的函数中），请检查控制台日志！");
//         // Do NOT schedule startTurn here, as the state is likely broken
//     } // End of added catch block
// }