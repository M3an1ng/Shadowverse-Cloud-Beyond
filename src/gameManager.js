// src/gameManager.js - 核心游戏状态与逻辑管理

import { createPlayer } from './player.js'; // 正确导入
import { getCardDefinition } from './cardManager.js'; // ADDED: Renamed import needed for evolveCard
// import { getCardDefinition } from './cardManager.js'; // initGame 不需要
// import { renderGame } from './ui.js'; // 渲染调用在 main.js
import { renderGame } from './ui.js'; // Import UI functions for feedback (Removed displayError, displayInfo)
import { startTurn } from './turnManager.js'; // Import startTurn to begin the first turn
import { showDamageNumber } from './ui.js'; // Import the damage number display function
import { performAIMulligan } from './ai.js'; // Placeholder for AI mulligan logic
// Note: Player's performMulligan is a method, called via playerState.performMulligan
    // import { MAX_FIELD_SIZE } from './main.js'; // Check if defined elsewhere

    // --- Constants ---
    const MAX_FIELD_SIZE = 5; // Maximum cards on a player's field

console.log("gameManager.js loaded");
// --- SignalR Connection ---
let signalRConnection = null; // Holds the connection object passed from main.js

// 核心游戏状态 (可导出或通过函数访问)
export let gameState = {
    players: [], // 存储 Player 实例
    currentPlayerId: null,
    turnNumber: 0,
    // 'Mulligan', 'TurnStart', 'Main', 'Battle', 'TurnEnd', 'GameOver'
gameId: null,           // Unique ID for the current game session
    localPlayerId: null,    // ID of the player using this client instance
    gamePhase: 'Initializing',
    activePlayer: null, // 对当前玩家实例的引用
    opponentPlayer: null, // 对对手玩家实例的引用
    gameOver: false,
    winner: null,
    selectedCardUid: null, // 手牌中选中的卡 (用于出牌或查看详情)
    selectedAttackerUid: null, // 战场上选中的攻击者
    player1MulliganComplete: false, // Player 1 mulligan status
    player2MulliganComplete: false, // Player 2 mulligan status
    // 稍后可能添加: eventLog, effectQueue 等
};

// 初始化游戏 (已实现版本)
/**
 * Receives the SignalR connection object from main.js.
 * @param {signalR.HubConnection} connection - The active SignalR connection.
 */
export function setSignalRConnection(connection) { // ADDED export
   console.log("GAME MANAGER: Setting SignalR connection.");
   signalRConnection = connection;
}
export function initializeGame(idOfGame, idOfPlayer) { // ADDED export
console.log(`GAME MANAGER: Initializing with GameID=${idOfGame}, PlayerID=${idOfPlayer}. Waiting for server state...`);
    // Reset or initialize a minimal local state structure if needed.
    // Primarily, store the IDs for context.
    gameState.gameId = idOfGame; // Use properties added earlier
    gameState.localPlayerId = idOfPlayer; // Use properties added earlier

    // Clear out potentially stale complex objects from previous local games
    gameState.players = []; // Server will populate this via ReceiveGameState
    gameState.activePlayer = null;
    gameState.opponentPlayer = null;
    gameState.turnNumber = 0;
    gameState.phase = 'Connecting'; // Indicate waiting for server state
    gameState.gameOver = false;
    gameState.winner = null;
    gameState.selectedCardUid = null;
    gameState.selectedAttackerUid = null;
    // Remove mulligan flags, server handles mulligan phase start/end
    // delete gameState.player1MulliganComplete; // Commented out deletion for now
    // delete gameState.player2MulliganComplete; // Commented out deletion for now


    // NO local setup of decks, hands, first player, PP, EP, or turn start.
    // All this will be received from the server via ReceiveGameState or ReceiveGameEvent.

    // Optionally, render a 'Connecting...' or 'Waiting for game state...' UI message
    // renderGame(gameState); // Render the initial 'connecting' state
} // Closing brace for initializeGame

/**
 * Processes incremental game events received from the SignalR hub.
 * Updates the local gameState and triggers UI refresh.
 * @param {object} gameEvent - The event object received from the server.
 * Expected structure: { eventType: "...", payload: { ... } }
 */
 export function processGameEvent(gameEvent) { // ADDED EXPORT
     if (!gameEvent || !gameEvent.eventType) {
         console.error("GAME MANAGER: Received invalid game event object:", gameEvent);
        return;
    }

    console.log(`GAME MANAGER: Processing Game Event - Type: ${gameEvent.eventType}`, gameEvent.payload);

    try {
        switch (gameEvent.eventType) {
            case 'TurnEnded':
                // Payload might contain: { nextPlayerId: "...", newTurnNumber: N, playerUpdates: [{playerId:"..", pp: N, maxPP: N, ep: N, maxEP: N, drawnCard: {...}? }] }
                if (gameEvent.payload) {
                    gameState.currentPlayerId = gameEvent.payload.nextPlayerId;
                    gameState.turnNumber = gameEvent.payload.newTurnNumber;
                    gameState.phase = 'TurnStart'; // Assume server moves phase

                    // Update player states (PP, EP, drawn card)
                    gameEvent.payload.playerUpdates?.forEach(update => {
                        const player = getPlayerState(update.playerId);
                        if (player) {
                            player.pp = update.pp;
                            player.maxPP = update.maxPP;
                            player.ep = update.ep;
                            player.maxEP = update.maxEP;
                            if(update.drawnCard) {
                                // Need to ensure the drawnCard object matches client-side card structure
                                player.hand.push(update.drawnCard);
                                console.log(`GM: Player ${update.playerId} drew ${update.drawnCard.name}`);
                            }
                        }
                    });
                    // Update active/opponent player references
                    gameState.activePlayer = getPlayerState(gameState.currentPlayerId);
                    gameState.opponentPlayer = gameState.players.find(p => p && p.id !== gameState.currentPlayerId) || null;

                     // Reset attack status for the new turn's player field
                     gameState.activePlayer?.field.forEach(f => f.canAttack = true);
                }
                break;

            case 'CardPlayed':
                // Payload might contain: { playerId: "...", cardPlayed: {...}, ppLeft: N, fieldUpdate: [...], handUpdate: [...] }
                // Server sends the new state after the card play resolves.
                // We might just need to update the specific player's state based on the payload.
                if (gameEvent.payload) {
                     const player = getPlayerState(gameEvent.payload.playerId);
                     if (player) {
                         player.pp = gameEvent.payload.ppLeft;
                         // Find the played card in hand and remove it
                         const handIndex = player.hand.findIndex(c => c.uid === gameEvent.payload.cardPlayed.uid);
                         if (handIndex > -1) player.hand.splice(handIndex, 1);

                         // If it was a follower/amulet, add it to the field
                         if (gameEvent.payload.cardPlayed.location === 'field') {
                             // Ensure no duplicates if fieldUpdate also contains it
                             if (!player.field.some(f => f.uid === gameEvent.payload.cardPlayed.uid)) {
                                 player.field.push(gameEvent.payload.cardPlayed);
                             }
                         }
                         // Potentially update field/hand fully if payload provides complete lists? Simpler might be delta updates.
                         console.log(`GM: Card ${gameEvent.payload.cardPlayed.name} played by ${gameEvent.payload.playerId}.`);
                     }
                }
                break;

            case 'AttackOccurred':
                // Payload might contain: { attackerUid: "...", targetUid: "...", damageDealtToTarget: N, damageDealtToAttacker: N, attackerState: {...}, targetState: {...} }
                // Update health of involved units based on server calculation.
                if (gameEvent.payload) {
                    const attacker = findCardInstanceByUid(gameEvent.payload.attackerUid);
                    const target = findCardInstanceByUid(gameEvent.payload.targetUid);
                    if (attacker) {
                         Object.assign(attacker, gameEvent.payload.attackerState); // Update attacker state
                         console.log(`GM: Attacker ${attacker.name} state updated after attack.`);
                    }
                     if (target) {
                         Object.assign(target, gameEvent.payload.targetState); // Update target state
                         console.log(`GM: Target ${target.name} state updated after attack.`);
                         // Show damage number feedback? Server might send this as separate event?
                         // showDamageNumber(target.uid, gameEvent.payload.damageDealtToTarget);
                     }
                }
                break;

            case 'UnitEvolved':
                 // Payload might contain: { playerId: "...", unitUid: "...", evolvedState: {...}, epLeft: N }
                 if (gameEvent.payload) {
                    const player = getPlayerState(gameEvent.payload.playerId);
                     const unit = findCardInstanceByUid(gameEvent.payload.unitUid);
                     if (player) player.ep = gameEvent.payload.epLeft;
                     if (unit) {
                          Object.assign(unit, gameEvent.payload.evolvedState);
                          console.log(`GM: Unit ${unit.name} evolved by ${gameEvent.payload.playerId}.`);
                     }
                 }
                 break;

            case 'UnitDestroyed':
                // Payload might contain: { unitUid: "..." }
                 if (gameEvent.payload) {
                     const unit = findCardInstanceByUid(gameEvent.payload.unitUid);
                     if (unit) {
                         handleDestruction(unit); // Fixed call
                     } // Closing brace for if(unit)
                 } // Closing brace for if(gameEvent.payload)
                 break; // Closing break for case 'UnitDestroyed'

            case 'EpUpdate':
                 // Payload example: { playerId: "...", newEpValue: N }
                 if (gameEvent.payload) {
                     const player = getPlayerState(gameEvent.payload.playerId);
                    if (player) {
                        player.ep = gameEvent.payload.newEpValue;
                        console.log(`GM: Player ${player.playerId} EP updated to ${player.ep}/${player.maxEP}`);
                    }
                 }
                 break;

            case 'StatChange':
                 // Payload example: { unitId: "...", newAttack: N, newHealth: N }
                 // Assumes server sends the new base stats after modification (e.g., buffs, debuffs)
                 if (gameEvent.payload) {
                    const unit = findCardInstanceByUid(gameEvent.payload.unitId);
                    if (unit) {
                        // Update both base and current stats for consistency
                        unit.attack = gameEvent.payload.newAttack;
                        unit.health = gameEvent.payload.newHealth;
                        unit.currentAttack = gameEvent.payload.newAttack; // Reflect change in current stats too
                        unit.currentHealth = gameEvent.payload.newHealth; // Reflect change in current stats too
                        console.log(`GM: Unit ${unit.name} (UID: ${unit.uid}) stats changed to A:${unit.attack}/H:${unit.health}`);
                    }
                 }
                 break;

            case 'MulliganStarted':
                 // Payload might contain: { mulliganTimer: N } or initial hand for local player?
                 console.log("GM: Mulligan phase started.", gameEvent.payload);
                 gameState.phase = 'Mulligan';
                 // UI should now allow mulligan actions if this is the local player
                 // May need logic here to enable UI elements based on player ID?
                 break;

            case 'GameOver':
                 // Payload might contain: { winnerId: "...", loserId: "...", reason: "..." }
                 console.log("GM: Game over event received.", gameEvent.payload);
                 gameState.gameOver = true;
                 gameState.phase = 'GameOver';
                 gameState.winner = gameEvent.payload?.winnerId;
                 // Update UI to show game over screen with winner/loser info
                 alert(`游戏结束！ 玩家 ${gameState.winner} 获胜！ (${gameEvent.payload?.reason || '原因未知'})`); // Simple alert for now
                 break;

            case 'PlayerActionError':
                 // Payload might contain: { playerId: "...", actionType: "...", message: "..." }
                 console.warn(`GM: Player action error received for ${gameEvent.payload?.playerId} (${gameEvent.payload?.actionType}): ${gameEvent.payload?.message}`);
                 // Display the error message to the specific player (if it's the local player)
                 if (gameEvent.payload?.playerId === gameState.localPlayerId) {
                     alert(`操作错误: ${gameEvent.payload?.message}`);
                 }
                 break;

            // TODO: Add more event types from SignalR hub if needed later
            // case 'ReceiveGameState': // This should likely be handled separately in main.js on initial connect? Or here?
            //     // Payload would be the full gameState object
            //     console.log("GM: Received full game state update.");
            //     Object.assign(gameState, gameEvent.payload);
            //     // Need to update active/opponent player references after full update
            //     gameState.activePlayer = getPlayerState(gameState.currentPlayerId);
            //     gameState.opponentPlayer = gameState.players.find(p => p.id !== gameState.currentPlayerId);
            //     break;

            default:
                console.warn(`GAME MANAGER: Unhandled game event type received: ${gameEvent.eventType}`);
        } // Closing brace for switch
    } catch (error) {
        console.error(`GAME MANAGER: Error processing game event type ${gameEvent.eventType}:`, error);
        // Potentially display an error to the user via UI function
        alert(`处理游戏事件 ${gameEvent.eventType} 时出错: ${error.message}`);
    }

    // Always re-render the game state after processing an event
    // Ensure gameState and renderGame are correctly referenced/imported
    if (typeof renderGame === 'function') {
         console.log("GAME MANAGER: Game state rendered after event processing.");
         renderGame(gameState); // Pass the updated gameState
    } else {
         console.error("GAME MANAGER: renderGame function is not available for UI update after event processing.");
    }
} // Closing brace for processGameEvent
// --- Mulligan Phase Logic ---

// Removed startMulliganPhase - Server now dictates phase start via events.

// Handles the mulligan action for a specific player
// Removed handleMulligan - Client interaction should trigger SignalR invoke, state updates via events.

// 新增：根据 UID 查找卡牌实例 (手牌或战场)
    export function findCardInstanceByUid(uid) {
        if (!uid) return null; // 如果没有提供 uid，直接返回 null

        for (const player of gameState.players) {
            // 检查手牌
            const cardInHand = player.hand?.find(card => card && card.uid === uid);
            if (cardInHand) {
                console.log(`GAME MANAGER: Found card UID ${uid} in ${player.id}'s hand.`);
                return cardInHand;
            }
            // 检查战场
            const cardOnField = player.field?.find(card => card && card.uid === uid);
            if (cardOnField) {
                console.log(`GAME MANAGER: Found card UID ${uid} on ${player.id}'s field.`);
                return cardOnField;
            }
            // 检查主战者
            // Ensure player.leaderCard exists before checking its uid
            if (player.leaderCard && player.leaderCard.uid === uid) {
                console.log(`GAME MANAGER: Found leader card instance UID ${uid} for player ${player.id} (Location: ${player.leaderCard.location}).`);
                return player.leaderCard;
            }
        }

        console.warn(`GAME MANAGER: Card instance with UID ${uid} not found in any player's hand, field, or leader zone.`);
        return null; // 遍历完所有玩家的手牌、战场和主战者区域都未找到
    }

// 新增：获取当前回合玩家ID
    export function getCurrentPlayerId() {
        return gameState.currentPlayerId;
    }

    // 新增：根据玩家ID获取玩家状态对象
    export function getPlayerState(playerId) {
        if (!playerId) return null;
        return gameState.players.find(p => p && p.playerId === playerId); // FIX: Use p.playerId to match server DTO
    }
// --- 其他函数占位符 (保持不变) ---

// --- Effect Resolution ---
// context can contain { targetUid: "some-uid" } for targeted effects
// Removed resolveEffects - Server now handles effect resolution.

// Helper to validate if a target instance matches the spell's requirements
export function isValidSpellTarget(targetInstance, validTargets, castingPlayerId) {
    if (!targetInstance || !Array.isArray(validTargets) || validTargets.length === 0) {
        console.warn("isValidSpellTarget: Invalid input - targetInstance or validTargets missing/empty.");
        return false; // Basic sanity checks
    }

    const targetType = targetInstance.type; // e.g., 'Follower', 'Leader', 'Follower_Evolved'
    const targetOwnerId = targetInstance.ownerId;
    const isAlly = targetOwnerId === castingPlayerId;
    const isEnemy = targetOwnerId !== castingPlayerId;

    console.log(`isValidSpellTarget Check: TargetType=${targetType}, TargetOwner=${targetOwnerId}, Caster=${castingPlayerId}, IsEnemy=${isEnemy}, Required=${validTargets.join(', ')}`);

    for (const validTarget of validTargets) {
        switch (validTarget) {
            case 'EnemyFollower':
                if (isEnemy && (targetType === 'Follower' || targetType === 'Follower_Evolved')) {
                    console.log("isValidSpellTarget: Matched EnemyFollower");
                    return true;
                }
                break;
            case 'EnemyLeader':
                if (isEnemy && targetType === 'Leader') {
                    console.log("isValidSpellTarget: Matched EnemyLeader");
                    return true;
                }
                break;
            case 'AllyFollower':
                 if (isAlly && (targetType === 'Follower' || targetType === 'Follower_Evolved')) {
                     console.log("isValidSpellTarget: Matched AllyFollower");
                     return true;
                 }
                break;
            case 'AllyLeader':
                 if (isAlly && targetType === 'Leader') {
                     console.log("isValidSpellTarget: Matched AllyLeader");
                     return true;
                 }
                break;
             case 'AnyFollower': // Example of potentially targeting any follower
                 if (targetType === 'Follower' || targetType === 'Follower_Evolved') {
                      console.log("isValidSpellTarget: Matched AnyFollower");
                      return true;
                 }
                 break;
             case 'AnyLeader': // Example of potentially targeting any leader
                 if (targetType === 'Leader') {
                      console.log("isValidSpellTarget: Matched AnyLeader");
                      return true;
                 }
                 break;
            // Add more cases as needed (e.g., 'EnemyCharacter', 'AllyCharacter')
            default:
                console.warn(`isValidSpellTarget: Unknown validTarget type '${validTarget}'`);
                break;
        }
    }

    console.log("isValidSpellTarget: No criteria matched.");
    return false; // If no criteria matched
}
export async function playCard(cardUid, targetUid = null) {
if (gameState.gameOver) {
        console.warn("GM: Cannot play card - Game is over.");
        alert("游戏已经结束，无法出牌！"); // User feedback
        return;
    }
    console.log(`GM: Attempting to play card UID: ${cardUid}, Target UID: ${targetUid}`);

    const cardInstance = findCardInstanceByUid(cardUid);
    if (!cardInstance) {
        console.error(`GM: PlayCard failed - Card instance ${cardUid} not found.`);
        alert("内部错误：无法找到卡牌实例。");
        return;
    }

    if (cardInstance.location !== 'hand') {
         console.warn(`GM: PlayCard failed - Card ${cardUid} (${cardInstance.name}) is not in hand (Location: ${cardInstance.location}).`);
         // No error display needed, UI should prevent this.
         return;
    }

    const playerState = getPlayerState(cardInstance.ownerId);
    if (!playerState) {
        console.error(`GM: PlayCard failed - Player state not found for owner ${cardInstance.ownerId}.`);
        alert("内部错误：无法找到玩家状态。");
        return;
    }

    // --- Pre-condition Checks (Client-side) ---
    // IMPORTANT: Ensure we check against localPlayerId provided during initGame
    if (playerState.id !== gameState.localPlayerId || playerState.id !== gameState.currentPlayerId) {
        console.warn(`GM: PlayCard rejected - Not local player's turn or local player ID mismatch (Local: ${gameState.localPlayerId}, Current: ${gameState.currentPlayerId}, CardOwner: ${playerState.id}).`);
        alert("现在不是你的回合！");
        return;
    }
    if (gameState.phase !== 'Main') {
        console.warn(`GM: PlayCard rejected - Can only play cards during Main phase (Current: ${gameState.phase}).`);
        alert("只能在你的回合主阶段出牌！");
        return;
    }
    if (playerState.pp < cardInstance.cost) {
        console.warn(`GM: PlayCard rejected - Not enough PP for ${cardInstance.name} (Cost: ${cardInstance.cost}, Have: ${playerState.pp}).`);
        alert("PP不足！"); // UI should prevent this, but double check.
        return;
    }

    // Follower/Amulet specific checks before invoking server
    if (cardInstance.type === 'Follower' || cardInstance.type === 'Amulet') {
        // Use MAX_FIELD_SIZE constant defined at the top of the file
        if (playerState.field.length >= MAX_FIELD_SIZE) {
             console.warn(`GM: PlayCard rejected - Field is full (${playerState.field.length}/${MAX_FIELD_SIZE}).`);
             alert("战场已满！");
             return;
        }
    }

    // Spell target check (basic) - Requires cardData access
    // NOTE: This check might be better handled server-side for consistency,
    // but a basic client-side check can provide faster feedback.
    // We need access to card definitions here, assuming getCardDefinition is available.
    const cardData = getCardDefinition(cardInstance.id); // Needs cardManager import
    if (cardInstance.type === 'Spell' && cardData?.requires_target && !targetUid) {
         console.warn(`GM: PlayCard rejected - Spell ${cardInstance.name} requires a target, but none provided.`);
         alert("该法术需要指定一个目标！");
         // TODO: Add more specific target validation later if needed (e.g., using isValidSpellTarget helper)
         return;
    }
    // Consider adding isValidSpellTarget check here if spell has targets


    // --- SignalR Invocation ---
    // Ensure signalRConnection is accessible (should be via module scope variable set by setSignalRConnection)
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error("GM: PlayCard failed - SignalR connection not available or not connected.");
        alert("与服务器连接丢失，无法出牌！");
        // Optionally try to reconnect or inform the user more drastically
        return;
    }

    console.log(`GM: Invoking SignalR Hub method 'PlayCard'. GameID: ${gameState.gameId}, CardUID: ${cardUid}, TargetUID: ${targetUid}`);
    try {
        // Invoke the server method. The server will validate everything again (cost, turn, phase, target, etc.)
        // and handle the actual game logic.
        await signalRConnection.invoke("PlayCard", gameState.gameId, cardUid, targetUid);

        // If invoke succeeds, DO NOT modify local state directly.
        // Wait for the server to send back game events (e.g., CardPlayed, UnitSpawned, PPDeducted, EffectResolved, UnitDestroyed, TurnEnded...)
        // The processGameEvent function will handle these events and update the state.
        console.log("GM: 'PlayCard' invocation sent successfully. Waiting for server response...");

        // Provide immediate feedback to the user that the action is in progress
        alert(`正在使用 ${cardInstance.name}...`);

        // Clear the local hand selection *optimistically* after successful invocation.
        // If the server rejects the play later via an error event, the UI might need to adjust.
        gameState.selectedCardUid = null;
        // Re-render *might* be needed just to clear highlight, but event processing should handle final state render.
        // Rely on event processing for rendering for now.
        // renderGame(gameState); // Consider removing or making conditional

    } catch (err) {
        console.error("GM: SignalR 'PlayCard' invocation failed:", err);
        // Attempt to parse a more specific error message if available
        let errorMessage = "服务器通信错误";
        if (err && err.message) {
            // Example: Hub exceptions might be in err.message
             errorMessage = err.message;
             // You might want to sanitize or simplify complex server error messages here
        }
        alert(`出牌失败：${errorMessage}`);

        // Clear selection on error too
        gameState.selectedCardUid = null;
        renderGame(gameState); // Re-render needed to clear selection highlight on error.
    }
    // New body will be inserted here by next step
}
// 发起攻击 (选择我方攻击者)
export function initiateAttack(attackerUid) {
    console.log(`GM: Attempting to initiate attack with UID: ${attackerUid}`);

    if (gameState.gameOver) {
        console.warn("GM: Cannot initiate attack - Game is over.");
        return;
    }

    if (gameState.phase !== 'Main') {
        console.warn(`GM: Cannot initiate attack - Not in Main phase (Current: ${gameState.phase}).`);
        // alert("只能在主阶段攻击！"); // Optional UI feedback
        return;
    }

    const attackerInstance = findCardInstanceByUid(attackerUid);
    if (!attackerInstance) {
        console.error(`GM: InitiateAttack failed - Attacker instance ${attackerUid} not found.`);
        return;
    }

    if (attackerInstance.ownerId !== gameState.currentPlayerId) {
        console.warn(`GM: InitiateAttack failed - Card ${attackerUid} does not belong to current player (${gameState.currentPlayerId}).`);
        return;
    }

    if (attackerInstance.location !== 'field') {
         console.warn(`GM: InitiateAttack failed - Card ${attackerUid} (${attackerInstance.name}) is not on the field (Location: ${attackerInstance.location}).`);
         return;
    }

console.log('GM_DBG: Checking attacker instance before canAttack check:', JSON.stringify(attackerInstance)); // Log instance state
    if (!attackerInstance.canAttack) {
        console.warn(`GM: InitiateAttack failed - Follower ${attackerUid} (${attackerInstance.name}) cannot attack (Summoning sickness or already attacked).`);
        // alert("该随从本回合无法攻击！"); // Optional UI feedback
        return;
    }

    // Optional: Handle clicking the same attacker again to de-select?
    // if (gameState.selectedAttackerUid === attackerUid) {
    //     console.log(`GM: Deselecting attacker ${attackerUid}.`);
    //     gameState.selectedAttackerUid = null;
    // } else {
         console.log(`GM: Selecting attacker: ${attackerInstance.name} (UID: ${attackerUid})`);
         gameState.selectedAttackerUid = attackerUid;
    // }

    // Trigger UI update to highlight attacker and potential targets
    console.log("GM: Calling renderGame after initiating attack.");
    renderGame(gameState);
}

// 选择攻击目标 (敌方随从或英雄)
export async function selectAttackTarget(targetUid) {
    console.log(`GM: Attempting to select target UID: ${targetUid}`);
    const attackerUid = gameState.selectedAttackerUid;

    // Validation 1: Check if an attacker is selected
    if (!attackerUid) {
        console.warn("GM: SelectTarget failed - No attacker currently selected.");
        // This might happen if the user clicks an opponent card without selecting an attacker first.
        return;
    }

    // Find instances
    const attackerInstance = findCardInstanceByUid(attackerUid);
    const targetInstance = findCardInstanceByUid(targetUid);

    // Validation 2: Check instances exist
    if (!attackerInstance) {
         console.error(`GM: SelectTarget failed - Selected attacker instance ${attackerUid} not found!`);
         gameState.selectedAttackerUid = null; // Reset selection if attacker is invalid
         renderGame(gameState); // Update UI to remove highlight
         return;
    }
    if (!targetInstance) {
        console.error(`GM: SelectTarget failed - Target instance ${targetUid} not found!`);
        return;
    }

    // Validation 3: Check target is opponent's and on field OR is the opponent's leader
    if (targetInstance.ownerId === gameState.currentPlayerId) {
         console.warn(`GM: SelectTarget failed - Cannot target friendly unit ${targetUid}.`);
         return;
    }
    // Allow targeting 'field' or 'leader_zone'
    if (targetInstance.location !== 'field' && targetInstance.location !== 'leader_zone') {
        console.warn(`GM: SelectTarget failed - Target ${targetUid} is not on field or leader zone (Location: ${targetInstance.location}).`);
        return;
    }

    // Validation 4: Check target is a valid attack target type
    const validTargetTypes = ['Follower', 'Follower_Evolved', 'Leader'];
    if (!validTargetTypes.includes(targetInstance.type)) {
         console.warn(`GM: SelectTarget failed - Target ${targetUid} is not an attackable type (Type: ${targetInstance.type}).`);
         return;
    }

    // REMOVED: Redundant Summoning Sickness Check for Leader Target.
    // The general 'canAttack' flag (set in playCard based on Storm,
    // and checked in initiateAttack) now handles this correctly for all targets.

    // Validation 5: Check for Ward (守护)
    const opponentPlayer = getPlayerState(targetInstance.ownerId); // Get the owner of the target
    if (!opponentPlayer) {
         console.error(`GM: SelectTarget failed - Could not find opponent player state for ID: ${targetInstance.ownerId}`);
         return;
    }

    // Check if opponent has any follower with Ward on the field
    const opponentHasWard = opponentPlayer.field.some(card =>
        card &&
        (card.type === 'Follower' || card.type === 'Follower_Evolved') &&
        card.keywords?.includes('Ward')
    );

    // If opponent has Ward, enforce targeting rules
    if (opponentHasWard) {
        const targetIsFollower = targetInstance.type === 'Follower' || targetInstance.type === 'Follower_Evolved';
        const targetHasWard = targetInstance.keywords?.includes('Ward');

        // Valid targets when opponent has Ward: Only followers that also have Ward
        if (!targetIsFollower || !targetHasWard) {
             console.warn(`GM: SelectTarget failed - Cannot target ${targetInstance.name} (${targetInstance.type}) because opponent has Ward follower(s). Must target a Ward follower first.`);
             alert("必须先攻击带有【守护】的随从！");
             return; // Reject the target selection
        }
        // If target is a follower with Ward, the check passes.
        console.log(`GM: Ward check passed - Target ${targetInstance.name} is a valid Ward target.`);
    }
    // If opponent does NOT have Ward, any valid target (follower or leader, respecting other rules) is fine.


    // All validations passed (including Ward check)
    console.log(`GM: Attack target selected - ${attackerInstance.name} (${attackerUid}) will attack ${targetInstance.name} (${targetUid})`);

    // Execute the attack (this will now handle both follower and leader targets)
        // --- SignalR Invocation ---
        if (!signalRConnection || signalRConnection.state !== "Connected") {
            console.error("GM: SelectTarget failed - SignalR connection not available.");
            alert("与服务器连接丢失，无法进行攻击！");
            // Clear selection on connection error before trying to attack
            gameState.selectedAttackerUid = null;
            renderGame(gameState); // Render to clear selection
            return;
        }

        console.log(`GM: Invoking SignalR Hub method 'PerformAttack'. GameID: ${gameState.gameId}, AttackerUID: ${attackerUid}, TargetUID: ${targetUid}`);
        try {
            // Invoke server method. Server handles validation and attack execution.
            await signalRConnection.invoke("PerformAttack", gameState.gameId, attackerUid, targetUid);

            // Wait for server events (AttackOccurred, UnitDestroyed, GameOver) via processGameEvent.
            // DO NOT change attackerInstance.canAttack here. Server manages the authoritative state.
            console.log("GM: 'PerformAttack' invocation sent successfully. Waiting for server response...");

            // Optimistically clear selection and provide feedback
            alert(`${attackerInstance.name} 正在攻击 ${targetInstance.name}...`);
            gameState.selectedAttackerUid = null; // Clear selection locally *after* successful invoke
            renderGame(gameState); // Re-render to clear highlights after successful invocation.

        } catch (err) {
            console.error("GM: SignalR 'PerformAttack' invocation failed:", err);
            let errorMessage = "服务器通信错误";
            if (err && err.message) {
                errorMessage = err.message;
            }
            alert(`攻击失败：${errorMessage}`);

            // Clear selection on error
            gameState.selectedAttackerUid = null;
            renderGame(gameState); // Re-render to clear highlights on error.
        }

    // Trigger UI update
    console.log("GM: Calling renderGame after selecting attack target.");
    renderGame(gameState);
}

// --- Destruction Helper ---
function handleDestruction(cardInstance) {
    if (!cardInstance) return;
    // Avoid handling destruction for leaders here, game over logic handles it.
    if (cardInstance.type === 'Leader') return;

    console.log(`GM: Handling destruction of ${cardInstance.name} (UID: ${cardInstance.uid})`);

    const ownerPlayer = getPlayerState(cardInstance.ownerId);
    if (!ownerPlayer) {
        console.error(`GM: Cannot handle destruction, owner player ${cardInstance.ownerId} not found.`);
        return;
    }

    // Remove from field
    const fieldIndex = ownerPlayer.field.findIndex(card => card && card.uid === cardInstance.uid);
    if (fieldIndex > -1) {
        const destroyedCard = ownerPlayer.field.splice(fieldIndex, 1)[0]; // Get the removed card
        if (destroyedCard) {
            destroyedCard.location = 'graveyard';
            // Reset stats? Or keep them as they were upon destruction? Keeping them for now.
            ownerPlayer.graveyard.push(destroyedCard);
            console.log(`GM: ${destroyedCard.name} (UID: ${destroyedCard.uid}) removed from field and added to graveyard.`);

             // TODO: Trigger Last Words effects here
             // executeEffects(destroyedCard, 'LastWords', ownerPlayer.id);
        }
    } else {
        console.warn(`GM: Card ${cardInstance.uid} marked for destruction but not found on owner's field.`);
    }
}

// --- Combat Execution ---
// --- Cancel Selection ---
    export function cancelSelection() {
        console.log("GM: Cancelling selection.");
        if (gameState.selectedAttackerUid) {
            gameState.selectedAttackerUid = null;
            console.log("GM: Attacker selection cancelled.");
             // Trigger UI update to remove highlights
             renderGame(gameState);
        } else {
             console.log("GM: Cancel selection called, but nothing was selected.");
        }
        // TODO: Add cancellation for selectedCardUid if needed later
    }


// 检查游戏是否结束
// 检查游戏是否结束 (Refactored to check leaderCard health)
// Removed checkGameOver - Server now dictates game end via events.


// 进化随从
// 进化随从 (Implemented Version)
export async function evolveCard(cardUid) {
    if (gameState.gameOver) {
        console.warn("GM: Cannot evolve card - Game is over.");
        alert("游戏已经结束，无法进化！");
        return;
    }
    console.log(`GM: Attempting to evolve card UID: ${cardUid}`);

    const playerState = getPlayerState(gameState.currentPlayerId);
    const cardInstance = findCardInstanceByUid(cardUid);
    const cardData = cardInstance ? getCardDefinition(cardInstance.id) : null; // Base data

    // --- Basic Client-Side Validations ---
    if (!playerState || !cardInstance || !cardData) {
        console.error(`GM: EvolveCard failed - Missing player state, card instance, or card definition for UID ${cardUid}.`);
        alert("内部错误：无法找到进化所需信息。");
        return;
    }

    // Check if it's the local player's turn
    if (playerState.id !== gameState.localPlayerId || playerState.id !== gameState.currentPlayerId) {
         console.warn(`GM: EvolveCard rejected - Not local player's turn.`);
         alert("现在不是你的回合！");
         return;
    }
    if (gameState.phase !== 'Main') {
        console.warn(`GM: EvolveCard rejected - Cannot evolve outside Main phase.`);
        alert("只能在你的回合主阶段进化！");
        return;
    }
    // Optimistic client-side checks (Server will re-validate)
    if (!playerState.canEvolveThisTurn) { // Check local flag
        console.warn("GM: EvolveCard rejected - Local state indicates evolution already used or unavailable.");
        alert("本回合已使用过进化点或无法进化。");
        return;
    }
    if (playerState.ep < 1) { // Check local EP
        console.warn("GM: EvolveCard rejected - Local state shows insufficient EP.");
        alert("进化点不足！");
        return;
    }
    if (cardInstance.location !== 'field') {
        console.warn(`GM: EvolveCard rejected - Card not on field.`);
        alert("只能进化场上的随从！");
        return;
    }
    if (cardInstance.type !== 'Follower') {
        console.warn(`GM: EvolveCard rejected - Cannot evolve non-Follower.`);
        alert("只能进化随从！");
        return;
    }
    if (cardInstance.isEvolved === true) {
        console.warn(`GM: EvolveCard rejected - Card is already evolved.`);
         alert("该随从已经进化！");
        return;
    }
    if (cardData.can_evolve !== true || !cardData.evolves_into || cardData.evolves_into.length === 0) {
        console.warn("GM: EvolveCard rejected - Card definition does not allow evolution or lacks target.");
         alert("该随从无法进化！");
        return;
    }
     // --- End Client-Side Validations ---

    // --- SignalR Invocation ---
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error("GM: EvolveCard failed - SignalR connection not available.");
        alert("与服务器连接丢失，无法进化！");
        return;
    }

    console.log(`GM: Invoking SignalR Hub method 'EvolveCard'. GameID: ${gameState.gameId}, CardUID: ${cardUid}`);
    try {
        await signalRConnection.invoke("EvolveCard", gameState.gameId, cardUid);

        // Wait for server event 'UnitEvolved' via processGameEvent for state update.
        console.log("GM: 'EvolveCard' invocation sent successfully. Waiting for server response...");
        alert(`正在进化 ${cardInstance.name}...`);

        // Optimistically update the local flag? Or wait for server confirmation?
        // Let's wait for server confirmation via event processing to keep client thin.
        // playerState.canEvolveThisTurn = false; // DO NOT DO THIS HERE

    } catch (err) {
        console.error("GM: SignalR 'EvolveCard' invocation failed:", err);
        let errorMessage = "服务器通信错误";
        if (err && err.message) {
             errorMessage = err.message;
        }
        alert(`进化失败：${errorMessage}`);
        // No state change locally on error
    }
}
// Duplicate evolveCard function removed to fix SyntaxError
// --- SignalR Hub Invocation Wrappers ---

/**
 * Submits the player's mulligan decision to the server.
 * @param {string[]} cardUidsToReplace - An array of card UIDs the player wishes to replace.
 */
export async function submitMulligan(cardUidsToReplace = []) {
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error("GM: SubmitMulligan failed - SignalR connection not available.");
        alert("与服务器连接丢失，无法提交调度！");
        return;
    }
    if (gameState.phase !== 'Mulligan') {
        console.warn("GM: SubmitMulligan rejected - Not in Mulligan phase.");
        // No user error display needed, UI should prevent this call
        return;
    }
     // Removed check: 'localPlayerId !== currentPlayerId' as it's incorrect during Mulligan phase.
     // Server-side validation handles individual player mulligan submission correctly.


     console.log(`GM: Invoking SignalR Hub method 'SubmitMulligan'. GameID: ${gameState.gameId}, CardsToReplace:`, cardUidsToReplace);
    try {
        await signalRConnection.invoke("SubmitMulligan", cardUidsToReplace); // REMOVED gameState.gameId argument
        console.log("GM: 'SubmitMulligan' invocation sent successfully. Waiting for server response...");
        alert("正在提交调度决定...");
        // Do not modify local state (like player1MulliganComplete) here.
        // Server should send an event (e.g., MulliganEnded, TurnStarted) to confirm and update state.

    } catch (err) {
        console.error("GM: SignalR 'SubmitMulligan' invocation failed:", err);
        let errorMessage = "服务器通信错误";
        if (err && err.message) {
            errorMessage = err.message;
        }
        alert(`提交调度失败：${errorMessage}`);
    }
}

/**
 * Sends a request to the server to end the current player's turn.
 */
export async function endTurnRequest() {
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error("GM: EndTurnRequest failed - SignalR connection not available.");
        alert("与服务器连接丢失，无法结束回合！");
        return;
    }
    // Optional: Add phase check? Should EndTurn be allowed outside Main phase? Usually yes.
    if (gameState.phase === 'Mulligan' || gameState.phase === 'GameOver') {
         console.warn(`GM: EndTurnRequest rejected - Cannot end turn during ${gameState.phase} phase.`);
         return; // Silently reject if UI somehow allows this
    }
      // Additional check: Ensure it's the local player trying to end the turn
      if (gameState.localPlayerId !== gameState.currentPlayerId) {
           console.warn("GM: EndTurnRequest rejected - Not the local player's turn.");
           return; // Silently fail
      }

    console.log(`GM: Invoking SignalR Hub method 'EndTurn'. GameID: ${gameState.gameId}`);
    try {
        await signalRConnection.invoke("EndTurn", gameState.gameId);
        console.log("GM: 'EndTurn' invocation sent successfully. Waiting for server response...");
        alert("正在结束回合...");
        // Wait for TurnEnded event from server via processGameEvent.

    } catch (err) {
        console.error("GM: SignalR 'EndTurn' invocation failed:", err);
        let errorMessage = "服务器通信错误";
        if (err && err.message) {
            errorMessage = err.message;
        }
        alert(`结束回合失败：${errorMessage}`);
    }
}
/**
 * Sends a generic game action object to the SignalR Hub.
 * Used for actions initiated by the client (Player or AI) that need server processing,
 * especially when a dedicated function wrapper isn't necessary or available yet.
 * @param {object} actionData - The action data object (e.g., { actionType: "Attack", attackerUid: "...", targetUid: "..." }). Requires at least `actionType`.
 */
export async function sendGameAction(actionData) {
    if (!actionData || !actionData.actionType) {
        console.error("GM: sendGameAction failed - Invalid actionData received:", actionData);
        alert("内部错误：无效的游戏动作数据。");
        return;
    }

    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error(`GM: sendGameAction (${actionData.actionType}) failed - SignalR connection not available.`);
        alert(`与服务器连接丢失，无法执行动作 (${actionData.actionType})！`);
        return;
    }

    // Optional: Add client-side player turn/phase checks if deemed useful for immediate feedback,
    // but the server MUST perform the authoritative validation.
    // Example:
    // if (gameState.localPlayerId !== gameState.currentPlayerId) {
    //     console.warn(`GM: sendGameAction (${actionData.actionType}) rejected - Not local player's turn.`);
    //     alert("现在不是你的回合！");
    //     return;
    // }
    // if (gameState.phase !== 'Main' && actionData.actionType !== 'SomeActionAllowedOutsideMain') { // Adjust condition based on action
    //     console.warn(`GM: sendGameAction (${actionData.actionType}) rejected - Action not allowed during ${gameState.phase} phase.`);
    //     alert(`无法在 ${gameState.phase} 阶段执行此动作！`);
    //     return;
    // }


    console.log(`GM: Invoking SignalR Hub method 'SendGameAction' with GameID: ${gameState.gameId}, Action:`, actionData);
    try {
        // Invoke the generic server method. Server uses actionData.actionType to route internally.
        await signalRConnection.invoke("SendGameAction", gameState.gameId, actionData);

        // Wait for server events triggered by this action via processGameEvent.
        console.log(`GM: 'SendGameAction' (${actionData.actionType}) invocation sent successfully. Waiting for server response...`);
        alert(`正在执行动作: ${actionData.actionType}...`); // Generic feedback

    } catch (err) {
        console.error(`GM: SignalR 'SendGameAction' (${actionData.actionType}) invocation failed:`, err);
        let errorMessage = "服务器通信错误";
        if (err && err.message) {
            // Try to extract a more specific error from potential HubException
             errorMessage = err.message.split(': ').pop() || err.message;
        }
        alert(`执行动作 (${actionData.actionType}) 失败：${errorMessage}`);
        // Optionally clear selection state if applicable based on actionType (e.g., clear selected attacker on attack fail)
        // if(actionData.actionType === 'Attack') { gameState.selectedAttackerUid = null; renderGame(gameState); }
    }
}

// Export the necessary functions/variables
// REMOVED export block - functions are now exported directly at definition.

/**
 * Updates the entire local game state based on a full state object received from the server.
 * This is typically used for the initial state synchronization.
 * @param {object} serverState - The complete game state object from the server.
 */
export function updateFullGameState(serverState) {
    console.log("GAME MANAGER: Receiving full game state update from server.", serverState);

    if (!serverState || !serverState.players || !serverState.gameId) {
        console.error("GAME MANAGER: Received invalid or incomplete full game state object:", serverState);
        alert("错误：收到无效的游戏状态数据！");
        return;
    }

    // Deep merge or careful assignment is needed if local state has properties server doesn't send.
    // For now, assuming serverState is the complete source of truth for shared properties.
    // We preserve local-only properties like signalRConnection if they exist outside gameState.

    // Merge the server state into the local gameState
    Object.assign(gameState, serverState);

    // --- Add Mapping Logic START ---
    // Map server DTO names (leaderHealth, currentPP, currentEP) to UI expected names (health, pp, ep)
    gameState.players.forEach(player => {
        if (player) {
            player.health = player.leaderHealth; // Map leaderHealth to health
            player.pp = player.currentPP;         // Map currentPP to pp
            player.ep = player.currentEP;         // Map currentEP to ep
            // Note: maxPP and maxEP seem consistent or handled by TurnEnded, check later if needed.
            // console.log(`GM MAP: Mapped player ${player.playerId} - Health:${player.health}, PP:${player.pp}, EP:${player.ep}`); // Debug log removed
           // Optionally delete old properties if they cause confusion, but keep for now maybe
           // delete player.leaderHealth;
           // delete player.currentPP;
            // delete player.currentEP;
        }
    });
    // --- Add Mapping Logic END ---


    // --- Crucial: Update player references after assigning players array ---
    // GM DEBUG START: Log received player structure in full state update - REMOVED
    // console.log("GM DEBUG: Received players array in full state:", JSON.stringify(serverState.players, null, 2));
    // if (serverState.players && serverState.players.length > 0) {
    //     console.log("GM DEBUG: Inspecting first player object structure:", JSON.stringify(serverState.players[0], null, 2));
    // }
     // GM DEBUG END - REMOVED
    // The players array from serverState now replaces the old one.
    // Find the active and opponent player *instances* within the *new* players array.
    gameState.activePlayer = gameState.players.find(p => p && p.playerId === gameState.currentPlayerId) || null;
    gameState.opponentPlayer = gameState.players.find(p => p && p.playerId !== gameState.currentPlayerId) || null;

    // Add detailed logging to verify player references
    if (gameState.activePlayer) {
        console.log(`GM: Updated activePlayer reference to Player ID: ${gameState.activePlayer.playerId}`);
    } else {
        console.warn(`GM: Could not find active player instance for ID: ${gameState.currentPlayerId} after full state update.`);
    }
    if (gameState.opponentPlayer) {
        console.log(`GM: Updated opponentPlayer reference to Player ID: ${gameState.opponentPlayer.playerId}`);
    } else {
        // This might be normal in a single-player context initially if opponent reference isn't needed immediately
        console.warn(`GM: Could not find opponent player instance after full state update (might be expected).`);
    }


    // Additional sanity checks/logging:
    console.log(`GM: Full state update complete. CurrentTurn=${gameState.turnNumber}, CurrentPlayer=${gameState.currentPlayerId}, Phase=${gameState.phase}`);
    console.log("GM: Active Player Hand:", gameState.activePlayer?.hand); // Log hand to check card data

    // Ensure the UI is re-rendered with the fully updated state
    if (typeof renderGame === 'function') {
        console.log("GAME MANAGER: Rendering game after full state update.");
        renderGame(gameState); // Pass the updated complete gameState
    } else {
        console.error("GAME MANAGER: renderGame function is not available for UI update after full state update.");
    }
}


// TODO: Add other exports here later if needed (e.g., processGameEvent) - Updated export list