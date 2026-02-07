// src/turnManager.js - 回合流程控制

// 导入依赖
import { gameState, getPlayerState } from './gameManager.js'; // Import state and getter
import { renderGame } from './ui.js'; // Import renderGame
import { runAILogic } from './ai.js'; // Import AI logic entry point


// 开始回合
export function startTurn() {

    // Check for game over
    if (gameState.gameOver) {
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
    }
    // 2. Refill PP to Max PP
    gameState.activePlayer.pp = gameState.activePlayer.maxPP; // Refill happens every turn

    // --- Evolution Point Handling (Using Flag) ---
    // Use firstPlayerId to determine the correct unlock turn
    const unlockTurn = (gameState.activePlayer.id === gameState.firstPlayerId) ? 5 : 4;
    const isEvolveUnlockedNow = gameState.turnNumber >= unlockTurn; // Check if potentially unlocked this turn or earlier
    // Use optional chaining for safety, though hasUnlockedEvolution should exist now

    // Logic for the specific turn EP unlocks using the flag
// --- DETAILED EP DEBUG ---
        // --- END DETAILED EP DEBUG ---
    if (isEvolveUnlockedNow && !gameState.activePlayer?.hasUnlockedEvolution) {
        // This is the exact turn evolution unlocks
        // Use firstPlayerId to determine the correct starting max EP
        const targetMaxEP = (gameState.activePlayer.id === gameState.firstPlayerId) ? 2 : 3;
        gameState.activePlayer.maxEP = targetMaxEP; // Set maxEP
        gameState.activePlayer.ep = targetMaxEP;    // Set current EP
        gameState.activePlayer.hasUnlockedEvolution = true; // Set the flag
    } else if (gameState.activePlayer?.hasUnlockedEvolution) {
        // Subsequent turns after unlocking - do nothing to EP/maxEP
    } else {
        // Turns before unlocking - do nothing
    }

    // Update canEvolveThisTurn based on the flag and current EP
    gameState.activePlayer.canEvolveThisTurn = gameState.activePlayer.hasUnlockedEvolution && gameState.activePlayer.ep > 0;
    // --- End Evolution Point Handling (Using Flag) ---

    // 3. Reset Field Attack State (Ready followers to attack)
    if (gameState.activePlayer.field && gameState.activePlayer.field.length > 0) {
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
    const isPlayer2FirstTurn = (currentPId === secondPId && turnNum === 1);
    // --- END DETAILED DRAW LOGGING ---
    const cardsToDraw = isPlayer2FirstTurn ? 2 : 1;
    gameState.activePlayer.drawCard(cardsToDraw); // Assumes drawCard handles deck out/hand full

    // 5. Set Game Phase to Main <<<<< THE FIX
    gameState.gamePhase = 'Main';

    // --- TODO: Trigger Start of Turn Effects here ---
    // executeEffects(gameState.activePlayer.field, 'StartOfTurn', gameState.activePlayer.id);
    // executeEffects(gameState.opponentPlayer.field, 'StartOfOpponentTurn', gameState.opponentPlayer.id);


    // 6. Update UI
    renderGame(gameState); // Update UI with new PP, hand, etc.

    // 7. Activate Player/AI
    if (gameState.activePlayer.isAI) { // Use the isAI flag from player state
        // Call runAILogic from ai.js, passing the current game state
        // Add a short delay to make it feel less instantaneous
        setTimeout(() => runAILogic(gameState), 500); // MODIFIED LINE
    } else {
        // Player's turn
        const endTurnButton = document.getElementById('end-turn-button');
        if(endTurnButton) {
            endTurnButton.disabled = false; // Enable button
        } else {
             console.error("TM: End turn button not found!");
        }
    }
}

// 结束回合
// // 结束回合 (功能已废弃，由服务器事件驱动)
// export function endTurn() {
//
//     // Check for game over before proceeding
//     if (gameState.gameOver) {
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
//         }
//
//
//     // TODO: Trigger End of Turn Effects for the player whose turn is ending
//     // executeEffects(currentPlayerToEnd.field, 'EndOfTurn', currentPlayerToEnd.id);
//
//     // Clear transient states (like selections) at end of turn
//     gameState.selectedCardUid = null;
//     gameState.selectedAttackerUid = null;
//
//
//     // Switch Player ID  <<< !!! CONFLICTING LOCAL LOGIC !!! >>>
//     const oldPlayerId = gameState.currentPlayerId;
//     // gameState.currentPlayerId = oldPlayerId === 'player1' ? 'player2' : 'player1'; // <<< CONFLICTING LINE COMMENTED OUT
//
//     // Update active/opponent references in gameState (important for next startTurn)
//     // gameState.activePlayer = getPlayerState(gameState.currentPlayerId); // Obsolete
//     // gameState.opponentPlayer = getPlayerState(oldPlayerId); // Obsolete
//
//
//     // Increment Turn Number only AFTER the second player finishes their turn.
//     if (oldPlayerId === gameState.secondPlayerId) { // **** THE FIX: Check if the player who JUST FINISHED was the second player ****
//         // If the player who just finished IS the second player, advance the game turn counter.
//         // This ensures the turn number advances from 1 to 2 only after the second player completes their actions in turn 1.
//         // gameState.turnNumber++; // Obsolete
//     }
//     // If the first player finished, the game turn number does not advance yet.
//
//     // Set phase for the *next* turn start
//     // gameState.gamePhase = 'TurnStart'; // Obsolete
//
//     // Update UI immediately before the delay to show opponent's turn starting etc.
//     // renderGame(gameState); // Obsolete
//
//     // Schedule the start of the next turn
//     // Using setTimeout prevents potential deep call stacks and allows UI to update
//     //    setTimeout(startTurn, 50); // Obsolete
//     } catch (error) { // ADDED catch block
//         console.error("TM: CRITICAL ERROR caught within obsolete endTurn:", error);
//         console.error("TM: Error Stack Trace:", error.stack); // Add stack trace
//         try {
//             // Use try...catch to prevent stringify itself from erroring
//         } catch (stringifyError) {
//             console.error("TM: Error stringifying gameState:", stringifyError);
//         }
//         // Optional: Notify the user more clearly, but logging is the primary goal for debugging
//         alert("发生严重错误，回合无法结束（在已废弃的函数中），请检查控制台日志！");
//         // Do NOT schedule startTurn here, as the state is likely broken
//     } // End of added catch block
// }