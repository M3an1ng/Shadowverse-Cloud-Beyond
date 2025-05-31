// src/gameManager.js - 核心游戏状态与逻辑管理

import { createPlayer } from "./player.js"; // 正确导入
import { getCardDefinition, createCardInstance } from "./cardManager.js"; // ADDED: createCardInstance for hydration
// import { getCardDefinition } from './cardManager.js'; // initGame 不需要
// import { renderGame } from './ui.js'; // 渲染调用在 main.js
import { renderGame, showDamageNumber } from "./ui.js"; // Import UI functions + showDamage (Removed enable/disable interaction calls)
//import { startTurn } from "./turnManager.js"; // Import startTurn to begin the first turn
//import { performAIMulligan } from "./ai.js"; // Placeholder for AI mulligan logic
// Note: Player's performMulligan is a method, called via playerState.performMulligan
// import { MAX_FIELD_SIZE } from './main.js'; // Check if defined elsewhere

// --- Constants ---
const MAX_FIELD_SIZE = 5; // Maximum cards on a player's field

// console.log("gameManager.js loaded");
// --- SignalR Connection ---
let signalRConnection = null; // Holds the connection object passed from main.js

// 核心游戏状态 (可导出或通过函数访问)
export let gameState = {
    players: [], // 存储 Player 实例
    currentPlayerId: null,
    turnNumber: 0,
    // 'Mulligan', 'TurnStart', 'Main', 'Battle', 'TurnEnd', 'GameOver'
    gameId: null, // Unique ID for the current game session
    localPlayerId: null, // ID of the player using this client instance
    gamePhase: "Initializing",
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
export function setSignalRConnection(connection) {
    // ADDED export
    // console.log("GAME MANAGER: Setting SignalR connection.");
    signalRConnection = connection;
}
export function initializeGame(idOfGame, idOfPlayer) {
    // ADDED export
    console.log(
        `GAME MANAGER: Initializing with GameID=${idOfGame}, PlayerID=${idOfPlayer}. Waiting for server state...`
    );
    // Reset or initialize a minimal local state structure if needed.
    // Primarily, store the IDs for context.
    gameState.gameId = idOfGame; // Use properties added earlier
    gameState.localPlayerId = idOfPlayer; // Use properties added earlier

    // Clear out potentially stale complex objects from previous local games
    gameState.players = []; // Server will populate this via ReceiveGameState
    gameState.activePlayer = null;
    gameState.opponentPlayer = null;
    gameState.turnNumber = 0;
    gameState.phase = "Connecting"; // Indicate waiting for server state
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
// --- Mulligan Phase Logic ---

// Removed startMulliganPhase - Server now dictates phase start via events.

// Handles the mulligan action for a specific player
// Removed handleMulligan - Client interaction should trigger SignalR invoke, state updates via events.

// 新增：根据 UID 查找卡牌实例 (手牌或战场)
export function findCardInstanceByUid(uid) {
    if (!uid) return null; // 如果没有提供 uid，直接返回 null

    for (const player of gameState.players) {
        // 检查手牌
        const cardInHand = player.hand?.find((card) => card && card.uid === uid);
        if (cardInHand) {
            console.log(
                `GAME MANAGER: Found card UID ${uid} in ${player.id}'s hand.`
            );
            return cardInHand;
        }
        // 检查战场
        const cardOnField = player.field?.find((card) => card && card.uid === uid);
        if (cardOnField) {
            console.log(
                `GAME MANAGER: Found card UID ${uid} on ${player.id}'s field.`
            );
            return cardOnField;
        }
        // 检查主战者
        // Ensure player.leaderCard exists before checking its uid
        if (player.leaderCard && player.leaderCard.uid === uid) {
            console.log(
                `GAME MANAGER: Found leader card instance UID ${uid} for player ${player.id} (Location: ${player.leaderCard.location}).`
            );
            return player.leaderCard;
        }
    }

    console.warn(
        `GAME MANAGER: Card instance with UID ${uid} not found in any player's hand, field, or leader zone.`
    );
    return null; // 遍历完所有玩家的手牌、战场和主战者区域都未找到
}

// 新增：获取当前回合玩家ID
export function getCurrentPlayerId() {
    return gameState.currentPlayerId;
}

// 新增：根据玩家ID获取玩家状态对象
export function getPlayerState(playerId) {
    if (!playerId) return null;
    // FIX: Use p.id which is set during createPlayer from the DTO's playerId
    return gameState.players.find((p) => p && p.id === playerId);
}
// --- 其他函数占位符 (保持不变) ---

// --- Effect Resolution ---
// context can contain { targetUid: "some-uid" } for targeted effects
// Removed resolveEffects - Server now handles effect resolution.

// Helper to validate if a target instance matches the spell's requirements
export function isValidSpellTarget(
    targetInstance,
    validTargets,
    castingPlayerId
) {
    if (
        !targetInstance ||
        !Array.isArray(validTargets) ||
        validTargets.length === 0
    ) {
        console.warn(
            "isValidSpellTarget: Invalid input - targetInstance or validTargets missing/empty."
        );
        return false; // Basic sanity checks
    }

    const targetType = targetInstance.type; // e.g., 'Follower', 'Leader', 'Follower_Evolved'
    const targetOwnerId = targetInstance.ownerId;
    const isAlly = targetOwnerId === castingPlayerId;
    const isEnemy = targetOwnerId !== castingPlayerId;

    console.log(
        `isValidSpellTarget Check: TargetType=${targetType}, TargetOwner=${targetOwnerId}, Caster=${castingPlayerId}, IsEnemy=${isEnemy}, Required=${validTargets.join(
            ", "
        )}`
    );

    for (const validTarget of validTargets) {
        switch (validTarget) {
            case "EnemyFollower":
                if (
                    isEnemy &&
                    (targetType === "Follower" || targetType === "Follower_Evolved")
                ) {
                    console.log("isValidSpellTarget: Matched EnemyFollower");
                    return true;
                }
                break;
            case "EnemyLeader":
                if (isEnemy && targetType === "Leader") {
                    console.log("isValidSpellTarget: Matched EnemyLeader");
                    return true;
                }
                break;
            case "AllyFollower":
                if (
                    isAlly &&
                    (targetType === "Follower" || targetType === "Follower_Evolved")
                ) {
                    console.log("isValidSpellTarget: Matched AllyFollower");
                    return true;
                }
                break;
            case "AllyLeader":
                if (isAlly && targetType === "Leader") {
                    console.log("isValidSpellTarget: Matched AllyLeader");
                    return true;
                }
                break;
            case "AnyFollower": // Example of potentially targeting any follower
                if (targetType === "Follower" || targetType === "Follower_Evolved") {
                    console.log("isValidSpellTarget: Matched AnyFollower");
                    return true;
                }
                break;
            case "AnyLeader": // Example of potentially targeting any leader
                if (targetType === "Leader") {
                    console.log("isValidSpellTarget: Matched AnyLeader");
                    return true;
                }
                break;
            // Add more cases as needed (e.g., 'EnemyCharacter', 'AllyCharacter')
            default:
                console.warn(
                    `isValidSpellTarget: Unknown validTarget type '${validTarget}'`
                );
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
    console.log(
        `GM: Attempting to play card UID: ${cardUid}, Target UID: ${targetUid}`
    );

    const cardInstance = findCardInstanceByUid(cardUid);
    if (!cardInstance) {
        console.error(`GM: PlayCard failed - Card instance ${cardUid} not found.`);
        alert("内部错误：无法找到卡牌实例。");
        return;
    }

    if (cardInstance.location !== "hand") {
        console.warn(
            `GM: PlayCard failed - Card ${cardUid} (${cardInstance.name}) is not in hand (Location: ${cardInstance.location}).`
        );
        // No error display needed, UI should prevent this.
        return;
    }

    // BUG-003 DIAGNOSIS START
    console.log(`GM_DBG_BUG003: Attempting to get player state for ownerId: ${cardInstance?.ownerId}`);
    console.log(`GM_DBG_BUG003: Current gameState.players:`, JSON.stringify(gameState.players.map(p => ({ id: p?.id, isAI: p?.isAI })), null, 2));
    // BUG-003 DIAGNOSIS END
    const playerState = getPlayerState(cardInstance.ownerId);
    if (!playerState) {
        console.error(
            `GM: PlayCard failed - Player state not found for owner ${cardInstance.ownerId}.`
        );
        alert("内部错误：无法找到玩家状态。");
        return;
    }

    // --- Pre-condition Checks (Client-side) ---
    // IMPORTANT: Ensure we check against localPlayerId provided during initGame
    if (
        playerState.id !== gameState.localPlayerId ||
        playerState.id !== gameState.currentPlayerId
    ) {
        console.warn(
            `GM: PlayCard rejected - Not local player's turn or local player ID mismatch (Local: ${gameState.localPlayerId}, Current: ${gameState.currentPlayerId}, CardOwner: ${playerState.id}).`
        );
        alert("现在不是你的回合！");
        return;
    }
    if (gameState.phase !== "Main") {
        console.warn(
            `GM: PlayCard rejected - Can only play cards during Main phase (Current: ${gameState.phase}).`
        );
        alert("只能在你的回合主阶段出牌！");
        return;
    }
    if (playerState.pp < cardInstance.cost) {
        console.warn(
            `GM: PlayCard rejected - Not enough PP for ${cardInstance.name} (Cost: ${cardInstance.cost}, Have: ${playerState.pp}).`
        );
        alert("PP不足！"); // UI should prevent this, but double check.
        return;
    }

    // Follower/Amulet specific checks before invoking server
    if (cardInstance.type === "Follower" || cardInstance.type === "Amulet") {
        // Use MAX_FIELD_SIZE constant defined at the top of the file
        if (playerState.field.length >= MAX_FIELD_SIZE) {
            console.warn(
                `GM: PlayCard rejected - Field is full (${playerState.field.length}/${MAX_FIELD_SIZE}).`
            );
            alert("战场已满！");
            return;
        }
    }

    // Spell target check (basic) - Requires cardData access
    // NOTE: This check might be better handled server-side for consistency,
    // but a basic client-side check can provide faster feedback.
    // We need access to card definitions here, assuming getCardDefinition is available.
    const cardData = getCardDefinition(cardInstance.cardId); // FIX: Use cardId instead of id
    if (
        cardInstance.type === "Spell" &&
        cardData?.requires_target &&
        !targetUid
    ) {
        console.warn(
            `GM: PlayCard rejected - Spell ${cardInstance.name} requires a target, but none provided.`
        );
        alert("该法术需要指定一个目标！");
        // TODO: Add more specific target validation later if needed (e.g., using isValidSpellTarget helper)
        return;
    }
    // Consider adding isValidSpellTarget check here if spell has targets

    // --- SignalR Invocation ---
    // Ensure signalRConnection is accessible (should be via module scope variable set by setSignalRConnection)
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error(
            "GM: PlayCard failed - SignalR connection not available or not connected."
        );
        alert("与服务器连接丢失，无法出牌！");
        // Optionally try to reconnect or inform the user more drastically
        return;
    }

    console.log(
        `GM: Invoking SignalR Hub method 'PlayCard'. GameID: ${gameState.gameId}, CardUID: ${cardUid}, TargetUID: ${targetUid}`
    );
    try {
        // Invoke the server method. The server will validate everything again (cost, turn, phase, target, etc.)
        // and handle the actual game logic.
        // Construct the DTO object expected by the server Hub method
        const actionDto = {
            cardUid: cardUid, // Property names should match C# DTO (camelCase after JSON serialization)
            targetUid: targetUid
        };
        console.log("GM: Constructed PlayCard DTO for invoke:", actionDto); // Log the object being sent

        // Invoke with the single DTO object
        await signalRConnection.invoke("PlayCard", actionDto);

        // If invoke succeeds, DO NOT modify local state directly.
        // Wait for the server to send back game events (e.g., CardPlayed, UnitSpawned, PPDeducted, EffectResolved, UnitDestroyed, TurnEnded...)
        // The processGameEvent function will handle these events and update the state.
        console.log(
            "GM: 'PlayCard' invocation sent successfully. Waiting for server response..."
        );

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

    if (gameState.phase !== "Main") {
        console.warn(
            `GM: Cannot initiate attack - Not in Main phase (Current: ${gameState.phase}).`
        );
        // alert("只能在主阶段攻击！"); // Optional UI feedback
        return;
    }

    const attackerInstance = findCardInstanceByUid(attackerUid);
    if (!attackerInstance) {
        console.error(
            `GM: InitiateAttack failed - Attacker instance ${attackerUid} not found.`
        );
        return;
    }

    if (attackerInstance.ownerId !== gameState.currentPlayerId) {
        console.warn(
            `GM: InitiateAttack failed - Card ${attackerUid} does not belong to current player (${gameState.currentPlayerId}).`
        );
        return;
    }

    if (attackerInstance.location !== "field") {
        console.warn(
            `GM: InitiateAttack failed - Card ${attackerUid} (${attackerInstance.name}) is not on the field (Location: ${attackerInstance.location}).`
        );
        return;
    }

    console.log(
        "GM_DBG: Checking attacker instance before canAttack check:",
        JSON.stringify(attackerInstance)
    ); // Log instance state
    if (!attackerInstance.canAttack) {
        console.warn(
            `GM: InitiateAttack failed - Follower ${attackerUid} (${attackerInstance.name}) cannot attack (Summoning sickness or already attacked).`
        );
        // alert("该随从本回合无法攻击！"); // Optional UI feedback
        return;
    }

    // Optional: Handle clicking the same attacker again to de-select?
    // if (gameState.selectedAttackerUid === attackerUid) {
    //     console.log(`GM: Deselecting attacker ${attackerUid}.`);
    //     gameState.selectedAttackerUid = null;
    // } else {
    console.log(
        `GM: Selecting attacker: ${attackerInstance.name} (UID: ${attackerUid})`
    );
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
        console.error(
            `GM: SelectTarget failed - Selected attacker instance ${attackerUid} not found!`
        );
        gameState.selectedAttackerUid = null; // Reset selection if attacker is invalid
        renderGame(gameState); // Update UI to remove highlight
        return;
    }
    if (!targetInstance) {
        console.error(
            `GM: SelectTarget failed - Target instance ${targetUid} not found!`
        );
        return;
    }

    // Validation 3: Check target is opponent's and on field OR is the opponent's leader
    if (targetInstance.ownerId === gameState.currentPlayerId) {
        console.warn(
            `GM: SelectTarget failed - Cannot target friendly unit ${targetUid}.`
        );
        return;
    }
    // Allow targeting 'field' or 'leader_zone'
    if (
        targetInstance.location !== "field" &&
        targetInstance.location !== "leader_zone"
    ) {
        console.warn(
            `GM: SelectTarget failed - Target ${targetUid} is not on field or leader zone (Location: ${targetInstance.location}).`
        );
        return;
    }

    // Validation 4: Check target is a valid attack target type
    const validTargetTypes = ["Follower", "Follower_Evolved", "Leader"];
    if (!validTargetTypes.includes(targetInstance.type)) {
        console.warn(
            `GM: SelectTarget failed - Target ${targetUid} is not an attackable type (Type: ${targetInstance.type}).`
        );
        return;
    }

    // REMOVED: Redundant Summoning Sickness Check for Leader Target.
    // The general 'canAttack' flag (set in playCard based on Storm,
    // and checked in initiateAttack) now handles this correctly for all targets.

    // Validation 5: Check for Ward (守护)
    const opponentPlayer = getPlayerState(targetInstance.ownerId); // Get the owner of the target
    if (!opponentPlayer) {
        console.error(
            `GM: SelectTarget failed - Could not find opponent player state for ID: ${targetInstance.ownerId}`
        );
        return;
    }

    // Check if opponent has any follower with Ward on the field
    const opponentHasWard = opponentPlayer.field.some(
        (card) =>
            card &&
            (card.type === "Follower" || card.type === "Follower_Evolved") &&
            card.keywords?.includes("Ward")
    );

    // If opponent has Ward, enforce targeting rules
    if (opponentHasWard) {
        const targetIsFollower =
            targetInstance.type === "Follower" ||
            targetInstance.type === "Follower_Evolved";
        const targetHasWard = targetInstance.keywords?.includes("Ward");

        // Valid targets when opponent has Ward: Only followers that also have Ward
        if (!targetIsFollower || !targetHasWard) {
            console.warn(
                `GM: SelectTarget failed - Cannot target ${targetInstance.name} (${targetInstance.type}) because opponent has Ward follower(s). Must target a Ward follower first.`
            );
            alert("必须先攻击带有【守护】的随从！");
            return; // Reject the target selection
        }
        // If target is a follower with Ward, the check passes.
        console.log(
            `GM: Ward check passed - Target ${targetInstance.name} is a valid Ward target.`
        );
    }
    // If opponent does NOT have Ward, any valid target (follower or leader, respecting other rules) is fine.

    // All validations passed (including Ward check)
    console.log(
        `GM: Attack target selected - ${attackerInstance.name} (${attackerUid}) will attack ${targetInstance.name} (${targetUid})`
    );

    // Execute the attack (this will now handle both follower and leader targets)
    // --- SignalR Invocation ---
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error(
            "GM: SelectTarget failed - SignalR connection not available."
        );
        alert("与服务器连接丢失，无法进行攻击！");
        // Clear selection on connection error before trying to attack
        gameState.selectedAttackerUid = null;
        renderGame(gameState); // Render to clear selection
        return;
    }

    console.log(
        `GM: Invoking SignalR Hub method 'PerformAttack'. GameID: ${gameState.gameId}, AttackerUID: ${attackerUid}, TargetUID: ${targetUid}`
    );
    try {
        // Invoke server method. Server handles validation and attack execution.
        await signalRConnection.invoke(
            "PerformAttack",
            gameState.gameId,
            attackerUid,
            targetUid
        );

        // Wait for server events (AttackOccurred, UnitDestroyed, GameOver) via processGameEvent.
        // DO NOT change attackerInstance.canAttack here. Server manages the authoritative state.
        console.log(
            "GM: 'PerformAttack' invocation sent successfully. Waiting for server response..."
        );

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
    if (cardInstance.type === "Leader") return;

    console.log(
        `GM: Handling destruction of ${cardInstance.name} (UID: ${cardInstance.uid})`
    );

    const ownerPlayer = getPlayerState(cardInstance.ownerId);
    if (!ownerPlayer) {
        console.error(
            `GM: Cannot handle destruction, owner player ${cardInstance.ownerId} not found.`
        );
        return;
    }

    // Remove from field
    const fieldIndex = ownerPlayer.field.findIndex(
        (card) => card && card.uid === cardInstance.uid
    );
    if (fieldIndex > -1) {
        const destroyedCard = ownerPlayer.field.splice(fieldIndex, 1)[0]; // Get the removed card
        if (destroyedCard) {
            destroyedCard.location = "graveyard";
            // Reset stats? Or keep them as they were upon destruction? Keeping them for now.
            ownerPlayer.graveyard.push(destroyedCard);
            console.log(
                `GM: ${destroyedCard.name} (UID: ${destroyedCard.uid}) removed from field and added to graveyard.`
            );

            // TODO: Trigger Last Words effects here
            // executeEffects(destroyedCard, 'LastWords', ownerPlayer.id);
        }
    } else {
        console.warn(
            `GM: Card ${cardInstance.uid} marked for destruction but not found on owner's field.`
        );
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
        console.error(
            `GM: EvolveCard failed - Missing player state, card instance, or card definition for UID ${cardUid}.`
        );
        alert("内部错误：无法找到进化所需信息。");
        return;
    }

    // Check if it's the local player's turn
    if (
        playerState.id !== gameState.localPlayerId ||
        playerState.id !== gameState.currentPlayerId
    ) {
        console.warn(`GM: EvolveCard rejected - Not local player's turn.`);
        alert("现在不是你的回合！");
        return;
    }
    if (gameState.phase !== "Main") {
        console.warn(`GM: EvolveCard rejected - Cannot evolve outside Main phase.`);
        alert("只能在你的回合主阶段进化！");
        return;
    }
    // Optimistic client-side checks (Server will re-validate)
    if (!playerState.canEvolveThisTurn) {
        // Check local flag
        console.warn(
            "GM: EvolveCard rejected - Local state indicates evolution already used or unavailable."
        );
        alert("本回合已使用过进化点或无法进化。");
        return;
    }
    if (playerState.ep < 1) {
        // Check local EP
        console.warn(
            "GM: EvolveCard rejected - Local state shows insufficient EP."
        );
        alert("进化点不足！");
        return;
    }
    if (cardInstance.location !== "field") {
        console.warn(`GM: EvolveCard rejected - Card not on field.`);
        alert("只能进化场上的随从！");
        return;
    }
    if (cardInstance.type !== "Follower") {
        console.warn(`GM: EvolveCard rejected - Cannot evolve non-Follower.`);
        alert("只能进化随从！");
        return;
    }
    if (cardInstance.isEvolved === true) {
        console.warn(`GM: EvolveCard rejected - Card is already evolved.`);
        alert("该随从已经进化！");
        return;
    }
    if (
        cardData.can_evolve !== true ||
        !cardData.evolves_into ||
        cardData.evolves_into.length === 0
    ) {
        console.warn(
            "GM: EvolveCard rejected - Card definition does not allow evolution or lacks target."
        );
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

    console.log(
        `GM: Invoking SignalR Hub method 'EvolveCard'. GameID: ${gameState.gameId}, CardUID: ${cardUid}`
    );
    try {
        await signalRConnection.invoke("EvolveCard", gameState.gameId, cardUid);

        // Wait for server event 'UnitEvolved' via processGameEvent for state update.
        console.log(
            "GM: 'EvolveCard' invocation sent successfully. Waiting for server response..."
        );
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
        console.error(
            "GM: SubmitMulligan failed - SignalR connection not available."
        );
        alert("与服务器连接丢失，无法提交调度！");
        return;
    }
    if (gameState.phase !== "Mulligan") {
        console.warn("GM: SubmitMulligan rejected - Not in Mulligan phase.");
        // No user error display needed, UI should prevent this call
        return;
    }
    // Removed check: 'localPlayerId !== currentPlayerId' as it's incorrect during Mulligan phase.
    // Server-side validation handles individual player mulligan submission correctly.

    console.log(
        `GM: Invoking SignalR Hub method 'SubmitMulligan'. GameID: ${gameState.gameId}, CardsToReplace:`,
        cardUidsToReplace
    );
    try {
        await signalRConnection.invoke("SubmitMulligan", cardUidsToReplace); // REMOVED gameState.gameId argument
        console.log(
            "GM: 'SubmitMulligan' invocation sent successfully. Waiting for server response..."
        );
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
    const timestamp = new Date().toISOString();
    console.log(`[${timestamp}] GM_DBG: Entered endTurnRequest() function.`); // ADDED: Log function entry
    // --- Proceed with checks ---
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error(
            "GM: EndTurnRequest failed - SignalR connection not available."
        );
        alert("与服务器连接丢失，无法结束回合！");
        return;
    }
    // Optional: Add phase check? Should EndTurn be allowed outside Main phase? Usually yes.
    if (gameState.phase === "Mulligan" || gameState.phase === "GameOver") {
        console.warn(
            `GM: EndTurnRequest rejected - Cannot end turn during ${gameState.phase} phase.`
        );
        return; // Silently reject if UI somehow allows this
    }
    // Additional check: Ensure it's the local player trying to end the turn
    if (gameState.localPlayerId != gameState.currentPlayerId) {
        console.warn(
            `GM: EndTurnRequest rejected - Not the local player's turn. Local: ${gameState.localPlayerId}, Current: ${gameState.currentPlayerId}`
        ); // Log values in rejection message
        return; // Silently fail
    }

    // Immediately disable the button visually (provides redundancy with UI disable)
    const endTurnButton = document.getElementById("end-turn-button");
    if (endTurnButton) {
        endTurnButton.disabled = true;
    }
    console.log(
        `GM: Invoking SignalR Hub method 'EndTurn'. GameID: ${gameState.gameId}`
    );
    try {
        const requestId = crypto.randomUUID(); // Generate unique ID for this request
        console.log(`GM: Generated Request ID for EndTurn: ${requestId}`); // Log the generated ID

        // --- DETAILED INVOKE LOGGING ---
        // Removed pre-invoke check logs
        if (typeof requestId !== 'string' || !requestId) {
            alert("内部错误：无法生成有效的请求ID，无法结束回合。");
            // Reset flag since invoke won't happen
            gameState.isEndingTurn = false;
            const endTurnButtonOnError = document.getElementById("end-turn-button");
            if (endTurnButtonOnError) { endTurnButtonOnError.disabled = false; }
            return; // Stop execution before invalid invoke
        }

        await signalRConnection.invoke("EndTurn", requestId); // Send requestId to the Hub

        // --- END DETAILED INVOKE LOGGING ---
        alert("正在结束回合...");
        // Wait for TurnEnded event from server via processGameEvent.
    } catch (err) {
        // Re-enable the button on error
        const endTurnButton = document.getElementById("end-turn-button");
        if (endTurnButton) {
            endTurnButton.disabled = false; // Re-enable button
        }
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
        console.error(
            "GM: sendGameAction failed - Invalid actionData received:",
            actionData
        );
        alert("内部错误：无效的游戏动作数据。");
        return;
    }

    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error(
            `GM: sendGameAction (${actionData.actionType}) failed - SignalR connection not available.`
        );
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

    console.log(
        `GM: Invoking SignalR Hub method 'SendGameAction' with GameID: ${gameState.gameId}, Action:`,
        actionData
    );
    try {
        // Invoke the generic server method. Server uses actionData.actionType to route internally.
        await signalRConnection.invoke(
            "SendGameAction",
            gameState.gameId,
            actionData
        );

        // Wait for server events triggered by this action via processGameEvent.
        console.log(
            `GM: 'SendGameAction' (${actionData.actionType}) invocation sent successfully. Waiting for server response...`
        );
        alert(`正在执行动作: ${actionData.actionType}...`); // Generic feedback
    } catch (err) {
        console.error(
            `GM: SignalR 'SendGameAction' (${actionData.actionType}) invocation failed:`,
            err
        );
        let errorMessage = "服务器通信错误";
        if (err && err.message) {
            // Try to extract a more specific error from potential HubException
            errorMessage = err.message.split(": ").pop() || err.message;
        }
        alert(`执行动作 (${actionData.actionType}) 失败：${errorMessage}`);
        // Optionally clear selection state if applicable based on actionType (e.g., clear selected attacker on attack fail)
        // if(actionData.actionType === 'Attack') { gameState.selectedAttackerUid = null; renderGame(gameState); }
    }
}

// Export the necessary functions/variables
// REMOVED export block - functions are now exported directly at definition.

// --- Hydration Helper Functions ---

/**
 * Hydrates simple properties of a player instance from its DTO.
 * @param {object} playerInstance - The player instance to hydrate.
 * @param {object} playerDto - The player DTO from the server.
 */
function _hydratePlayerProperties(playerInstance, playerDto) {
    if (!playerInstance || !playerDto) return;
    playerInstance.pp = playerDto.currentPP ?? 0;
    playerInstance.maxPP = playerDto.maxPP ?? 0;
    playerInstance.ep = playerDto.currentEP ?? 0;
    playerInstance.maxEP = playerDto.maxEP ?? 0;
    playerInstance.canEvolveThisTurn = playerDto.canEvolveThisTurn ?? false;
    playerInstance.hasUnlockedEvolution = playerDto.hasUnlockedEvolution ?? false;
    playerInstance.turn = playerDto.turn ?? 0;
    playerInstance.deckCount = playerDto.deckCount ?? (Array.isArray(playerDto.deck) ? playerDto.deck.length : 0); // Calculate deck count too
    // Keep deck as simple array of IDs/DTOs from server if no methods needed client-side
    playerInstance.deck = Array.isArray(playerDto.deck) ? [...playerDto.deck] : [];
}

/**
 * Hydrates dynamic properties of a leader card instance.
 * @param {object} playerInstance - The player instance owning the leader.
 * @param {object} playerDto - The player DTO containing the leader card DTO.
 */
function _hydrateLeaderCard(playerInstance, playerDto) {
    if (!playerInstance || !playerDto || !playerDto.leaderCard || !playerInstance.leaderCard) {
        console.warn(`GM HYDRATION HELPER: Missing data for leader card hydration for player ${playerDto?.playerId}`);
        return;
    }
    // Update currentHealth, the most critical dynamic stat
    playerInstance.leaderCard.currentHealth = playerDto.leaderCard.currentHealth ?? playerInstance.leaderCard.health;
    // Update UID if server sends it (should be stable, but good practice)
    playerInstance.leaderCard.uid = playerDto.leaderCard.uid ?? playerInstance.leaderCard.uid;
    // Add hydration for leader effects if applicable later
}

/**
 * Hydrates an array of card DTOs for a specific zone (Hand, Field, Graveyard).
 * @param {Array<object>|null|undefined} zoneDtoArray - The array of card DTOs from the server state.
 * @param {string} ownerId - The ID of the player who owns these cards.
 * @param {'hand'|'field'|'graveyard'} zoneName - The name of the zone.
 * @returns {Array<object>} - An array of hydrated card instances.
 */
function _hydrateCardZone(zoneDtoArray, ownerId, zoneName) {
    const hydratedZone = [];
    if (!Array.isArray(zoneDtoArray)) {
        // console.warn(`GM HYDRATION HELPER: ${zoneName} DTO for player ${ownerId} is not an array.`);
        return hydratedZone; // Return empty array if DTO is invalid
    }

    for (const cardDto of zoneDtoArray) {
        const cardInstance = createCardInstance(cardDto); // Use the factory from cardManager
        if (cardInstance) {
            cardInstance.ownerId = ownerId;
            cardInstance.location = zoneName;

            // Add specific hydration for field units' dynamic state
            if (zoneName === 'field') {
                cardInstance.canAttack = cardDto.canAttack ?? false;
                cardInstance.currentAttack = cardDto.currentAttack ?? cardInstance.attack;
                cardInstance.currentHealth = cardDto.currentHealth ?? cardInstance.health;
                cardInstance.isEvolved = cardDto.isEvolved ?? false;
                // Add hydration for effects, keywords etc. if needed
            }
            hydratedZone.push(cardInstance);
        } else {
            console.error(`GM HYDRATION HELPER: Failed to create ${zoneName} card instance from DTO for player ${ownerId}:`, cardDto);
        }
    }
    return hydratedZone;
}

// --- End Hydration Helper Functions ---
/**
 * Updates the entire local game state based on a full state object received from the server.
 * This is typically used for the initial state synchronization.
 * @param {object} serverState - The complete game state object from the server.
 */
export function updateFullGameState(serverState) {
    console.log(
        "GAME MANAGER: Receiving full game state update from server.",
        serverState
    );

    if (!serverState || !serverState.players || !serverState.gameId) {
        console.error(
            "GAME MANAGER: Received invalid or incomplete full game state object:",
            serverState
        );
        alert("错误：收到无效的游戏状态数据！");
        return;
    }

    // Deep merge or careful assignment is needed if local state has properties server doesn't send.
    // For now, assuming serverState is the complete source of truth for shared properties.
    // We preserve local-only properties like signalRConnection if they exist outside gameState.

    // Merge the server state into the local gameState
    // --- Smart Hydration: Assign top-level properties ---
    console.log("GM HYDRATION: Starting Smart Hydration process...");
    gameState.gameId = serverState.gameId;
    // Preserve localPlayerId if it was already set during initializeGame
    // gameState.localPlayerId = gameState.localPlayerId || serverState.localPlayerId; // Already set in init
    gameState.currentPlayerId = serverState.currentPlayerId;
    console.log(`GM_LOG_CID: Updated currentPlayerId to ${gameState.currentPlayerId} from updateFullGameState.`); // ADDED LOG
    gameState.turnNumber = serverState.turnNumber;
    gameState.phase = serverState.phase || "Unknown"; // Use the phase from the server state
    gameState.gameOver = serverState.gameOver || false;
    gameState.winner = serverState.winner || null;
    gameState.selectedCardUid = null; // Reset selections on full update
    gameState.selectedAttackerUid = null; // Reset selections on full update
    // Find player DTOs to get mulligan status
    const player1Dto = serverState.players?.find(p => p.playerId === 'player1');
    const player2Dto = serverState.players?.find(p => p.playerId === 'player2');
    // Read 'hasCompletedMulligan' from the correct player DTO (JS uses camelCase)
    gameState.player1MulliganComplete = player1Dto?.hasCompletedMulligan ?? false;
    gameState.player2MulliganComplete = player2Dto?.hasCompletedMulligan ?? false;
    gameState.isEndingTurn = false; // Reset flag
    // Corrected log to show the actual phase being set
    console.log(`GM HYDRATION: Assigned top-level state. GameID=${gameState.gameId}, CurrentPlayer=${gameState.currentPlayerId}, Turn=${gameState.turnNumber}, Phase=${gameState.phase}`);
    // Player hydration will replace the players array below

    // Player instantiation will happen next (original player mapping logic removed below)
    // --- Smart Hydration: Hydrate Players using Helper Functions ---
    const hydratedPlayers = [];
    if (Array.isArray(serverState.players)) {
        for (const playerDto of serverState.players) {
            if (!playerDto || !playerDto.playerId) {
                console.warn("GM HYDRATION: Skipping invalid player DTO:", playerDto);
                continue;
            }
            console.log(`GM HYDRATION: Processing Player DTO ID: ${playerDto.playerId}`);

            const playerInstance = createPlayer(playerDto.playerId, playerDto.isAI ?? false);
            if (!playerInstance) {
                console.error(`GM HYDRATION: Failed to create player instance for ID ${playerDto.playerId}.`);
                continue;
            }

            // Use helpers for hydration
            _hydratePlayerProperties(playerInstance, playerDto);
            _hydrateLeaderCard(playerInstance, playerDto);
            playerInstance.hand = _hydrateCardZone(playerDto.hand, playerInstance.id, 'hand');
            playerInstance.field = _hydrateCardZone(playerDto.field, playerInstance.id, 'field');
            playerInstance.graveyard = _hydrateCardZone(playerDto.graveyard, playerInstance.id, 'graveyard');
            // Deck is handled in _hydratePlayerProperties

            console.log(`GM HYDRATION: Finished hydrating player ${playerDto.playerId} using helpers. Hand:${playerInstance.hand.length}, Field:${playerInstance.field.length}, Graveyard:${playerInstance.graveyard.length}, Deck:${playerInstance.deckCount}`);
            console.log(`GM HYDRATION DEBUG: Player ${playerInstance.id} hydrated hand:`, JSON.stringify(playerInstance.hand.map(c => ({ id: c.id, uid: c.uid, name: c.name })), null, 2)); // ADDED: Log hydrated hand UIDs
            hydratedPlayers.push(playerInstance);
        }
    } else {
        console.warn("GM HYDRATION: serverState.players is not an array or is missing.");
    }
    // Replace the old players array entirely
    gameState.players = hydratedPlayers;
    console.log("GM HYDRATION: Replaced gameState.players with instances hydrated by helpers.");
    // Player reference updates will happen in the next block (original code remains)

    // --- Crucial: Update player references after assigning players array ---
    // GM DEBUG START: Log received player structure in full state update - REMOVED
    // console.log("GM DEBUG: Received players array in full state:", JSON.stringify(serverState.players, null, 2));
    // if (serverState.players && serverState.players.length > 0) {
    //     console.log("GM DEBUG: Inspecting first player object structure:", JSON.stringify(serverState.players[0], null, 2));
    // }
    // GM DEBUG END - REMOVED
    // The players array from serverState now replaces the old one.
    // Find the active and opponent player *instances* within the *new* players array.
    gameState.activePlayer =
        gameState.players.find(
            (p) => p && p.id === gameState.currentPlayerId // FIX: Use p.id
        ) || null;
    gameState.opponentPlayer =
        gameState.players.find(
            (p) => p && p.id !== gameState.currentPlayerId // FIX: Use p.id
        ) || null;

    // Add detailed logging to verify player references
    if (gameState.activePlayer) {
        console.log(
            `GM: Updated activePlayer reference to Player ID: ${gameState.activePlayer.id}` // FIX: Log .id instead of .playerId
        );
    } else {
        console.warn(
            `GM: Could not find active player instance for ID: ${gameState.currentPlayerId} after full state update.`
        );
    }
    if (gameState.opponentPlayer) {
        console.log(
            `GM: Updated opponentPlayer reference to Player ID: ${gameState.opponentPlayer.id}` // FIX: Log .id instead of .playerId
        );
    } else {
        // This might be normal in a single-player context initially if opponent reference isn't needed immediately
        console.warn(
            `GM: Could not find opponent player instance after full state update (might be expected).`
        );
    }

    // Additional sanity checks/logging:
    // Corrected log to show the actual phase after update
    console.log(
        `GM: Full state update complete. CurrentTurn=${gameState.turnNumber}, CurrentPlayer=${gameState.currentPlayerId}, Phase=${gameState.phase}`
    );
    console.log("GM: Active Player Hand:", gameState.activePlayer?.hand); // Log hand to check card data

    // Ensure the UI is re-rendered with the fully updated state
    if (typeof renderGame === "function") {
        console.log("GAME MANAGER: Rendering game after full state update.");
        renderGame(gameState); // Pass the updated complete gameState
    } else {
        console.error(
            "GAME MANAGER: renderGame function is not available for UI update after full state update."
        );
    }
}

// TODO: Add other exports here later if needed (e.g., processGameEvent) - Updated export list

// --- NEW Specific Event Handlers ---

export function handleTurnStarted(eventData) {
    console.log('%c[EVENT RECEIVED] handleTurnStarted:', 'background: #28a745; color: #ffffff; font-weight: bold;', eventData); // Green background
    // DTO: TurnStartedEventDto { string PlayerId, int TurnNumber, GamePhase Phase }
    console.log(`%cGM HANDLE: handleTurnStarted`, 'color: #28a745; font-weight: bold;', eventData);
    // Robust check for turnNumber (null or undefined)
    // Refined validation: Check for object, property existence, and non-null/undefined values
    if (!eventData ||
        !eventData.hasOwnProperty('currentPlayerId') || eventData.currentPlayerId === null || eventData.currentPlayerId === undefined || // CORRECTED: playerId -> currentPlayerId
        !eventData.hasOwnProperty('turnNumber') || eventData.turnNumber === null || eventData.turnNumber === undefined ||
        !eventData.hasOwnProperty('phase') || eventData.phase === null || eventData.phase === undefined) {
        console.error("GM Error: Invalid data structure or null/undefined values in handleTurnStarted:", eventData);
        return;
    }

    gameState.currentPlayerId = eventData.currentPlayerId; // CORRECTED: playerId -> currentPlayerId
    gameState.turnNumber = eventData.turnNumber; // FIX BUG-001: Update global turn number from event
    gameState.gamePhase = eventData.phase; // Trust the phase sent by server

    // Update player references which might have changed if players reconnect etc.
    gameState.activePlayer = getPlayerState(gameState.currentPlayerId);
    gameState.opponentPlayer = gameState.players.find(p => p && p.id !== gameState.currentPlayerId);

    // Update the active player's specific turn count (important for evolve conditions etc.)
    if (gameState.activePlayer) {
        // Use the turn number from the event DTO if it represents the player's turn count,
        // otherwise, increment locally (incrementing seems safer if DTO turnNumber is global)
        gameState.activePlayer.turn = (gameState.activePlayer.turn || 0) + 1;
        console.log(`GM: Player ${gameState.activePlayer.id}'s specific turn counter updated to ${gameState.activePlayer.turn}`);
    } else {
        console.warn(`GM: Cannot update active player turn counter, activePlayer not found for ID ${gameState.currentPlayerId}`);
    }

    // Enable/Disable UI Interactions based on who's turn it is
    if (gameState.currentPlayerId === gameState.localPlayerId) {
        console.log("GM: It's the local player's turn. Enabling interactions.");
        // enablePlayerInteractions(); // REMOVED: Relying on existing logic per user preference
    } else {
        console.log("GM: It's the opponent's turn. Disabling interactions.");
        // disablePlayerInteractions(); // REMOVED: Relying on existing logic per user preference
    }

    renderGame(gameState); // Render the updated state
}

export function handleTurnEnded(eventData) {
    console.log('%c[EVENT RECEIVED] handleTurnEnded:', 'background: #dc3545; color: #ffffff; font-weight: bold;', eventData); // Red background
    // DTO: TurnEndedEventDto { string NextPlayerId, int NewTurnNumber, List&lt;PlayerTurnEndUpdateDto&gt; PlayerUpdates }
    // Note: Most state updates (currentPlayerId, turnNumber, PP/EP, card draw) are now handled by the subsequent TurnStarted event.
    // This handler primarily logs and renders the intermediate state if needed.
    console.log(`%cGM HANDLE: handleTurnEnded`, 'color: #dc3545; font-weight: bold;', eventData);
    // Robust check for newTurnNumber (null or undefined)
    // Refined validation: Check for object, property existence, and non-null/undefined values
    // REMOVED check for newTurnNumber as it's no longer in the DTO
    if (!eventData ||
        !eventData.hasOwnProperty('nextPlayerId') || eventData.nextPlayerId === null || eventData.nextPlayerId === undefined) {
        console.error("GM Error: Invalid data structure or null/undefined values in handleTurnEnded:", eventData);
        return;
    }

    // Optional: Update phase to an intermediate state like 'TurnEnding' if required by UI/logic.
    // gameState.gamePhase = "TurnEnding";
    // console.log(`GM: Phase set to TurnEnding.`);

    // Update player references just in case, although TurnStarted should be the source of truth
    // gameState.activePlayer = getPlayerState(gameState.currentPlayerId); // Keep current player active during this phase
    // gameState.opponentPlayer = gameState.players.find(p => p && p.id !== gameState.currentPlayerId);

    // Log expected next player for debugging purposes
    console.log(`GM: Turn ended. Expected next player: ${eventData.nextPlayerId}. Waiting for TurnStarted event.`);

    // Do NOT change currentPlayerId or turnNumber here.
    // Do NOT update PP/EP or draw cards here.

    renderGame(gameState); // Render the state potentially reflecting 'TurnEnding' phase
}
export function handlePpUpdate(eventData) {
    console.log('%c[EVENT RECEIVED] handlePpUpdate:', 'background: #ffc107; color: #000000; font-weight: bold;', eventData); // Yellow background
    // DTO: PpUpdateEventDto { string SourcePlayerId, int CurrentPp, int MaxPp }
    console.log(`%cGM HANDLE: handlePpUpdate`, 'color: #ffc107; font-weight: bold;', eventData);
    if (!eventData || !eventData.sourcePlayerId || eventData.currentPp === undefined || eventData.maxPp === undefined) {
        console.error("GM Error: Invalid data in handlePpUpdate:", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.sourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.pp = eventData.currentPp;
        playerToUpdate.maxPP = eventData.maxPp;
        console.log(`GM: Updated player ${playerToUpdate.id} PP to ${playerToUpdate.pp}/${playerToUpdate.maxPP}`);
        console.log('%c[EVENT RECEIVED] handleEpUpdate:', 'background: #17a2b8; color: #ffffff; font-weight: bold;', eventData); // Cyan background
        renderGame(gameState); // Re-render after PP update
    } else {
        console.warn(`GM: PpUpdate event received for invalid player: ${eventData.sourcePlayerId}`);
    }
}

export function handleEpUpdate(eventData) {
    // DTO: EpUpdateEventDto { string SourcePlayerId, int NewEpValue (current), int NewMaxEpValue (max) }
    console.log(`%cGM HANDLE: handleEpUpdate`, 'color: #17a2b8; font-weight: bold;', eventData);
    // Note: DTO properties might be NewEpValue/NewMaxEpValue based on old handler, adjust if needed
    if (!eventData || !eventData.sourcePlayerId || eventData.newEpValue === undefined || eventData.newMaxEpValue === undefined) {
        console.error("GM Error: Invalid data in handleEpUpdate (expected sourcePlayerId, newEpValue, newMaxEpValue):", eventData);
        console.log('%c[EVENT RECEIVED] handleHandUpdate:', 'background: #6f42c1; color: #ffffff; font-weight: bold;', eventData); // Purple background
        return;
    }
    const playerToUpdate = getPlayerState(eventData.sourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.ep = eventData.newEpValue;
        playerToUpdate.maxEP = eventData.newMaxEpValue;
        console.log(`GM: Updated player ${playerToUpdate.id} EP to ${playerToUpdate.ep}/${playerToUpdate.maxEP}`);
        renderGame(gameState); // Re-render after EP update
    } else {
        console.warn(`GM: EpUpdate event received for invalid player: ${eventData.sourcePlayerId}`);
    }
}

export function handleHandUpdate(eventData) {
    // DTO: HandUpdateEventDto { string SourcePlayerId, int HandCount, bool IsBurned, CardDto? CardAdded, string? CardRemovedUid }
    console.log(`%cGM HANDLE: handleHandUpdate`, 'color: #6f42c1; font-weight: bold;', eventData);
    if (!eventData || !eventData.sourcePlayerId || eventData.handCount === undefined) {
        console.error("GM Error: Invalid data in handleHandUpdate (missing sourcePlayerId or handCount):", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.sourcePlayerId);
    if (!playerToUpdate) {
        console.warn(`GM: HandUpdate event received for invalid player: ${eventData.sourcePlayerId}`);
        console.log('%c[EVENT RECEIVED] handleDeckUpdate:', 'background: #fd7e14; color: #ffffff; font-weight: bold;', eventData); // Orange background
        return;
    }

    let updated = false;

    // Handle card removal (if specified)
    if (eventData.cardRemovedUid) {
        const cardIndex = playerToUpdate.hand.findIndex(c => c && c.uid === eventData.cardRemovedUid);
        if (cardIndex > -1) {
            const removedCard = playerToUpdate.hand.splice(cardIndex, 1)[0];
            console.log(`GM: Removed card ${removedCard?.name} (UID: ${eventData.cardRemovedUid}) from player ${playerToUpdate.id}'s hand.`);
            updated = true;
            console.log('%c[EVENT RECEIVED] handleFieldUpdate:', 'background: #20c997; color: #ffffff; font-weight: bold;', eventData); // Teal background
        } else {
            console.warn(`GM: HandUpdate requested removal of card UID ${eventData.cardRemovedUid}, but it was not found in player ${playerToUpdate.id}'s hand.`);
        }
    }

    // Handle card addition (if specified)
    // Check for the DrawnCards list property instead of cardAdded


    // FIX: Use lowercase 'drawnCards' due to CamelCase policy
    if (eventData.drawnCards && Array.isArray(eventData.drawnCards) && eventData.drawnCards.length > 0) {
        const drawnCardDto = eventData.drawnCards[0]; // FIX: Use lowercase 'drawnCards'
        console.log(`%c  [DBG_HAND_UPDATE] Received drawnCards DTO for Player ${playerToUpdate.id}. Processing first card:`, 'color: #e83e8c', JSON.parse(JSON.stringify(drawnCardDto))); // Deep copy for logging
        const newCardInstance = createCardInstance(drawnCardDto); // Use the DTO from the list
        console.log(`%c  [DBG_HAND_UPDATE] Result of createCardInstance from DrawnCards[0]:`, 'color: #e83e8c', newCardInstance ? JSON.parse(JSON.stringify(newCardInstance)) : null); // Deep copy for logging

        // --- DEBUG START: Inspect created instance ---
        console.log(`%c  [DBG_HAND_UPDATE] INSPECT INSTANCE BEFORE ADD: UID=${newCardInstance?.uid}, Name=${newCardInstance?.name}, Owner=${newCardInstance?.ownerId}, Loc=${newCardInstance?.location}`, 'color: #007bff; font-weight: bold;');
        // --- DEBUG END ---
        if (newCardInstance) {
            newCardInstance.ownerId = playerToUpdate.id;
            newCardInstance.location = 'hand';
            // Check if hand is full *before* adding, considering server might have already burned it
            const isHandFull = playerToUpdate.hand.length >= 10; // Max hand size 10
            console.log(`%c  [DBG_HAND_UPDATE] Hand size check: Current=${playerToUpdate.hand.length}, IsFull=${isHandFull}`, 'color: #e83e8c');
            if (!isHandFull) {
                const cardAlreadyExists = playerToUpdate.hand.some(c => c && c.uid === newCardInstance.uid);
                console.log(`%c  [DBG_HAND_UPDATE] Duplicate check: Card UID ${newCardInstance.uid} already exists? ${cardAlreadyExists}`, 'color: #e83e8c');
                if (!cardAlreadyExists) {
                    console.log(`%c  [DBG_HAND_UPDATE] BEFORE PUSH: Hand length = ${playerToUpdate.hand.length}`, 'color: #e83e8c');
                    playerToUpdate.hand.push(newCardInstance);
                    console.log(`%c  [DBG_HAND_UPDATE] AFTER PUSH: Hand length = ${playerToUpdate.hand.length}. Added: ${newCardInstance.name} (UID: ${newCardInstance.uid})`, 'color: #e83e8c');
                    console.log('%c[EVENT RECEIVED] handleGraveyardUpdate:', 'background: #6c757d; color: #ffffff; font-weight: bold;', eventData); // Gray background
                    // console.log(`GM: Added card ${newCardInstance.name} (UID: ${newCardInstance.uid}) to player ${playerToUpdate.id}'s hand.`); // Covered by DBG log above
                    // --- DEBUG START: Inspect hand array after push ---
                    console.log(`%c  [DBG_HAND_UPDATE] INSPECT HAND AFTER PUSH: Player ${playerToUpdate.id} Hand (UIDs): [${playerToUpdate.hand.map(c => c?.uid).join(', ')}]`, 'color: #007bff; font-weight: bold;');
                    // --- DEBUG END ---
                    updated = true;
                    console.log(`%c  [DBG_HAND_UPDATE] Set updated = true because card was added.`, 'color: #e83e8c');
                } else {
                    console.warn(`GM: HandUpdate attempted to add duplicate card UID ${newCardInstance.uid} (${newCardInstance.name}) for player ${playerToUpdate.id}.`);
                    console.log(`%c  [DBG_HAND_UPDATE] Set updated = false (or kept false) because card was duplicate.`, 'color: #e83e8c');
                }
            } else { // Hand is full
                console.warn(`GM: HandUpdate received CardAdded ${newCardInstance.name} (UID: ${newCardInstance.uid}) for player ${playerToUpdate.id}, but hand is full (client-side). Expecting IsBurned flag.`);
                console.log(`%c  [DBG_HAND_UPDATE] Set updated = false (or kept false) because hand was full.`, 'color: #e83e8c');
            }
        } else { // Failed to create instance
            console.error(`GM: Failed to create instance for added card via HandUpdate (from DrawnCards) for player ${playerToUpdate.id}:`, drawnCardDto);
            console.log(`%c  [DBG_HAND_UPDATE] Set updated = false (or kept false) because instance creation failed.`, 'color: #e83e8c');
        }
    } // End of DrawnCards check block
    console.log('%c[EVENT RECEIVED] handleBanishedUpdate:', 'background: #343a40; color: #ffffff; font-weight: bold;', eventData); // Dark gray background

    // Handle burned card indication (Check drawnCards instead of cardAdded)
    // FIX: Use lowercase 'drawnCards'
    if (eventData.isBurned && eventData.drawnCards && eventData.drawnCards.length > 0) {
        const burnedCardDto = eventData.drawnCards[0]; // FIX: Use lowercase 'drawnCards'
        console.warn(`GM: Card ${burnedCardDto.name} (UID: ${burnedCardDto.uid}) was burned due to full hand for player ${playerToUpdate.id}.`);
        // Optionally trigger a "card burned" visual effect
    }

    // Update hand count (server is source of truth)
    // Note: This might seem redundant if we add/remove locally, but it corrects any drift.
    // We should ideally rely *only* on add/remove events, but the DTO provides the count.
    // Let's log if the count mismatches our local calculation.
    const localHandCount = playerToUpdate.hand.length;
    console.log('%c[EVENT RECEIVED] handleLeaderHpUpdate:', 'background: #e83e8c; color: #ffffff; font-weight: bold;', eventData); // Pink background
    if (localHandCount !== eventData.handCount) {
        console.warn(`GM: Hand count mismatch for player ${playerToUpdate.id}. Local: ${localHandCount}, Server DTO: ${eventData.handCount}. Server count will be used if UI relies on it.`);
        // If UI directly uses playerToUpdate.hand.length, this log is sufficient.
        // If UI uses a separate count variable, update it here:
        // playerToUpdate.handCount = eventData.handCount;
    }

    console.log(`%c  [DBG_HAND_UPDATE] Final check before render: updated = ${updated}`, 'color: #e83e8c');
    if (updated) {
        console.log(`%c  [DBG_HAND_UPDATE] Calling renderGame() because updated is true.`, 'color: #e83e8c');
        renderGame(gameState); // Re-render if hand changed
    } else {
        console.log(`%c  [DBG_HAND_UPDATE] Skipping renderGame() because updated is false.`, 'color: #e83e8c');
    }
}

export function handleDeckUpdate(eventData) {
    console.log('%c[EVENT RECEIVED] handleUnitSpawned:', 'background: #007bff; color: #ffffff; font-weight: bold;', eventData); // Blue background
    // DTO: DeckUpdateEventDto { string SourcePlayerId, int DeckCount }
    console.log(`%cGM HANDLE: handleDeckUpdate`, 'color: #fd7e14; font-weight: bold;', eventData);
    if (!eventData || !eventData.sourcePlayerId || eventData.deckCount === undefined) {
        console.error("GM Error: Invalid data in handleDeckUpdate:", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.sourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.deckCount = eventData.deckCount;
        console.log(`GM: Updated player ${playerToUpdate.id} deck count to ${playerToUpdate.deckCount}`);
        renderGame(gameState); // Re-render if deck count display needs update
    } else {
        console.warn(`GM: DeckUpdate event received for invalid player: ${eventData.sourcePlayerId}`);
    }
}
export function handleGraveyardUpdate(eventData) {
    // DTO: GraveyardUpdateEventDto { string SourcePlayerId, int GraveyardCount, CardDto? CardAdded, string? CardRemovedUid }
    console.log(`%cGM HANDLE: handleGraveyardUpdate`, 'color: #6c757d; font-weight: bold;', eventData); // Gray color
    if (!eventData || !eventData.sourcePlayerId || eventData.graveyardCount === undefined) {
        console.error("GM Error: Invalid data in handleGraveyardUpdate (missing sourcePlayerId or graveyardCount):", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.sourcePlayerId);
    if (!playerToUpdate) {
        console.warn(`GM: GraveyardUpdate event received for invalid player: ${eventData.sourcePlayerId}`);
        return;
    }

    let updated = false;
    console.log('%c[EVENT RECEIVED] handleUnitAttacked:', 'background: #dc3545; color: #ffffff; font-weight: bold;', eventData); // Red background (same as TurnEnded)

    // Handle card removal (e.g., Necromancy effect)
    if (eventData.cardRemovedUid) {
        const cardIndex = playerToUpdate.graveyard.findIndex(c => c && c.uid === eventData.cardRemovedUid);
        if (cardIndex > -1) {
            const removedCard = playerToUpdate.graveyard.splice(cardIndex, 1)[0];
            console.log(`GM: Removed card ${removedCard?.name} (UID: ${eventData.cardRemovedUid}) from player ${playerToUpdate.id}'s graveyard.`);
            updated = true;
        } else {
            console.warn(`GM: GraveyardUpdate requested removal of card UID ${eventData.cardRemovedUid}, but it was not found in player ${playerToUpdate.id}'s graveyard.`);
        }
    }

    // Handle card addition (e.g., unit destroyed, card discarded)
    if (eventData.cardAdded) {
        const newCardInstance = createCardInstance(eventData.cardAdded);
        if (newCardInstance) {
            newCardInstance.ownerId = playerToUpdate.id;
            newCardInstance.location = 'graveyard';
            // Check if already present (unlikely for graveyard unless server sends duplicates)
            const cardAlreadyExists = playerToUpdate.graveyard.some(c => c && c.uid === newCardInstance.uid);
            if (!cardAlreadyExists) {
                playerToUpdate.graveyard.push(newCardInstance);
                console.log(`GM: Added card ${newCardInstance.name} (UID: ${newCardInstance.uid}) to player ${playerToUpdate.id}'s graveyard.`);
                updated = true;
            } else {
                console.warn(`GM: GraveyardUpdate attempted to add duplicate card UID ${newCardInstance.uid} (${newCardInstance.name}) to player ${playerToUpdate.id}'s graveyard.`);
            }
        } else {
            console.error(`GM: Failed to create instance for added card via GraveyardUpdate for player ${playerToUpdate.id}:`, eventData.cardAdded);
        }
    }

    console.log('%c[EVENT RECEIVED] handleUnitDamaged:', 'background: #ffc107; color: #000000; font-weight: bold;', eventData); // Yellow background (same as PpUpdate)
    // Update graveyard count (server is source of truth)
    const localGraveyardCount = playerToUpdate.graveyard.length;
    if (localGraveyardCount !== eventData.graveyardCount) {
        console.warn(`GM: Graveyard count mismatch for player ${playerToUpdate.id}. Local: ${localGraveyardCount}, Server DTO: ${eventData.graveyardCount}.`);
        // If UI relies on a separate count variable, update it:
        // playerToUpdate.graveyardCount = eventData.graveyardCount;
    } else {
        console.log(`GM: Updated player ${playerToUpdate.id}'s graveyard. New count: ${eventData.graveyardCount}`);
    }


    if (updated) {
        renderGame(gameState); // Re-render if graveyard changed
    }
}
export function handleCardDrawn(eventData) {
    // DTO: CardDrawnEventDto { string SourcePlayerId, string DrawnCardUid, string DrawnCardId }
    console.log(`%cGM HANDLE: handleCardDrawn`, 'color: #007bff; font-weight: bold;', eventData); // Blue color
    if (!eventData || !eventData.sourcePlayerId || !eventData.drawnCardUid || !eventData.drawnCardId) {
        console.error("GM Error: Invalid data in handleCardDrawn (missing sourcePlayerId, drawnCardUid, or drawnCardId):", eventData);
        return;
    }
    console.log('%c[EVENT RECEIVED] handleUnitHealed:', 'background: #28a745; color: #ffffff; font-weight: bold;', eventData); // Green background (same as TurnStarted) // Note: This log seems misplaced, but keeping it for diff accuracy

    // const playerToUpdate = getPlayerState(eventData.sourcePlayerId);
    // if (!playerToUpdate) {
    //     console.warn(`GM: CardDrawn event received for invalid player: ${eventData.sourcePlayerId}`);
    //     return;
    // }

    // let updated = false;


    // // Update deck count (server should send DeckUpdate, but this provides immediate feedback)
    // // This might be slightly inaccurate if server draws multiple cards before sending DeckUpdate
    // if (playerToUpdate.deckCount > 0) {
    //     playerToUpdate.deckCount--;
    //     console.log(`GM (handleCardDrawn): Decremented local deck count for player ${playerToUpdate.id} to ${playerToUpdate.deckCount}.`);
    //     updated = true; // Deck count changed, need render
    // }

    // // Future: Add any draw-specific animation triggers here if needed.

    // if (updated) {
    //     renderGame(gameState); // Re-render if hand or deck count changed
    // }
}
export function handleUnitPlayed(eventData) {
    // DTO: UnitPlayedEventDto { string PlayerId, UnitStateDto UnitData, string SourceCardUid }
    console.log(`%cGM HANDLE: handleUnitPlayed`, 'color: #007bff; font-weight: bold;', eventData);
    if (!eventData || !eventData.playerId || !eventData.unitData) {
        console.error("GM Error: Invalid data in handleUnitPlayed:", eventData);
        return;
    }
    const ownerPlayer = getPlayerState(eventData.playerId);
    if (!ownerPlayer) {
        console.log('%c[EVENT RECEIVED] handleUnitBuffed:', 'background: #17a2b8; color: #ffffff; font-weight: bold;', eventData); // Cyan background (same as EpUpdate)
        console.warn(`GM: UnitPlayed event received for invalid player: ${eventData.playerId}`);
        return;
    }

    // Remove the card from hand using sourceCardUid if provided
    if (eventData.sourceCardUid) {
        const handIndex = ownerPlayer.hand.findIndex(c => c && c.uid === eventData.sourceCardUid);
        if (handIndex > -1) {
            const removedCard = ownerPlayer.hand.splice(handIndex, 1)[0];
            console.log(`GM: Removed played unit source card ${removedCard?.name} (UID: ${eventData.sourceCardUid}) from player ${ownerPlayer.id}'s hand.`);
            // No separate render needed here, render happens after adding to field or if field is full
        } else {
            // This might happen if the card was generated (e.g., token) or if state is slightly out of sync.
            console.warn(`GM: UnitPlayed event sourceCardUid ${eventData.sourceCardUid} not found in player ${ownerPlayer.id}'s hand.`);
        }
    }

    // HYDRATION: Create full instance from the UnitStateDto
    const unitInstance = createCardInstance(eventData.unitData);
    if (!unitInstance) {
        console.error(`GM: Failed to create instance for UnitPlayed DTO for player ${ownerPlayer.id}:`, eventData.unitData);
        return;
    }
    console.log('%c[EVENT RECEIVED] handleUnitDebuffed:', 'background: #6f42c1; color: #ffffff; font-weight: bold;', eventData); // Purple background (same as HandUpdate)

    unitInstance.ownerId = ownerPlayer.id; // Ensure owner is set
    unitInstance.location = 'field'; // Ensure location

    // Hydrate dynamic state from the DTO
    unitInstance.canAttack = eventData.unitData.canAttack ?? false;
    unitInstance.currentAttack = eventData.unitData.currentAttack ?? unitInstance.attack;
    unitInstance.currentHealth = eventData.unitData.currentHealth ?? unitInstance.health;
    unitInstance.isEvolved = eventData.unitData.isEvolved ?? false;
    // Add hydration for other potential dynamic properties from UnitStateDto if added later

    console.log(`GM_UNITPLAYED_DEBUG: Hydrated Unit Instance for UID ${unitInstance.uid}: Atk=${unitInstance.currentAttack}, HP=${unitInstance.currentHealth}. Raw unitData:`, eventData.unitData);

    // Avoid adding duplicates if server sends redundant events
    if (!ownerPlayer.field.some(f => f && f.uid === unitInstance.uid)) {
        if (ownerPlayer.field.length < MAX_FIELD_SIZE) {
            ownerPlayer.field.push(unitInstance);
            console.log(`GM: Added unit ${unitInstance.name} (UID: ${unitInstance.uid}, Type: ${unitInstance.type}) to player ${ownerPlayer.id}'s field.`);
            renderGame(gameState); // Re-render after adding unit to field AND removing from hand
        } else {
            console.warn(`GM: UnitPlayed event received for ${unitInstance.name}, but player ${ownerPlayer.id}'s field is full (${ownerPlayer.field.length}/${MAX_FIELD_SIZE}). Unit not added client-side.`);
            renderGame(gameState); // Re-render even if field is full, because hand might have changed
        }
        console.log('%c[EVENT RECEIVED] handleUnitDestroyed:', 'background: #fd7e14; color: #ffffff; font-weight: bold;', eventData); // Orange background (same as DeckUpdate)
    } else {
        console.warn(`GM: UnitPlayed event received for unit UID ${unitInstance.uid} which is already present on player ${ownerPlayer.id}'s field.`);
        // Consider rendering if hand changed even if unit wasn't added to field
        if (eventData.sourceCardUid) {
            renderGame(gameState);
        }
    }
}
export function handleAmuletPlayed(eventData) {
    // DTO: Assume AmuletPlayedEventDto { string PlayerId, object AmuletData, string SourceCardUid }
    console.log(`%cGM HANDLE: handleAmuletPlayed`, 'color: #17a2b8; font-weight: bold;', eventData); // Use a distinct color for amulets
    if (!eventData || !eventData.playerId || !eventData.amuletData) { // Check for amuletData
        console.error("GM Error: Invalid data in handleAmuletPlayed:", eventData);
        return;
    }
    const ownerPlayer = getPlayerState(eventData.playerId);
    if (!ownerPlayer) {
        console.warn(`GM: AmuletPlayed event received for invalid player: ${eventData.playerId}`);
        return;
    }

    let handChanged = false;
    // Remove the card from hand using sourceCardUid if provided
    if (eventData.sourceCardUid) {
        console.log('%c[EVENT RECEIVED] handleUnitEvolved:', 'background: #20c997; color: #ffffff; font-weight: bold;', eventData); // Teal background (same as FieldUpdate)
        const handIndex = ownerPlayer.hand.findIndex(c => c && c.uid === eventData.sourceCardUid);
        if (handIndex > -1) {
            const removedCard = ownerPlayer.hand.splice(handIndex, 1)[0];
            console.log(`GM: Removed played amulet source card ${removedCard?.name} (UID: ${eventData.sourceCardUid}) from player ${ownerPlayer.id}'s hand.`);
            handChanged = true;
            // No separate render needed here, render happens after adding to field or if field is full
        } else {
            // This might happen if the card was generated or if state is slightly out of sync.
            console.warn(`GM: AmuletPlayed event sourceCardUid ${eventData.sourceCardUid} not found in player ${ownerPlayer.id}'s hand.`);
        }
    }

    // HYDRATION: Create full instance from the AmuletData
    // Note: AmuletData structure might be simpler than UnitStateDto. Adapt if needed.
    const amuletInstance = createCardInstance(eventData.amuletData);
    if (!amuletInstance) {
        console.error(`GM: Failed to create instance for AmuletPlayed DTO for player ${ownerPlayer.id}:`, eventData.amuletData);
        return;
    }

    amuletInstance.ownerId = ownerPlayer.id; // Ensure owner is set
    amuletInstance.location = 'field'; // Ensure location is field

    // Hydrate dynamic state specific to amulets if any (e.g., countdown, health if applicable)
    // Example: amuletInstance.countdown = eventData.amuletData.countdown ?? amuletInstance.countdown;
    // For now, assume basic hydration handled by createCardInstance

    console.log('%c[EVENT RECEIVED] handleSpellPlayed:', 'background: #6c757d; color: #ffffff; font-weight: bold;', eventData); // Gray background (same as Graveyard)
    console.log(`GM_AMULETPLAYED_DEBUG: Hydrated Amulet Instance for UID ${amuletInstance.uid}. Raw amuletData:`, eventData.amuletData);

    let fieldChanged = false;
    // Avoid adding duplicates if server sends redundant events
    if (!ownerPlayer.field.some(f => f && f.uid === amuletInstance.uid)) {
        if (ownerPlayer.field.length < MAX_FIELD_SIZE) {
            ownerPlayer.field.push(amuletInstance);
            console.log(`GM: Added amulet ${amuletInstance.name} (UID: ${amuletInstance.uid}, Type: ${amuletInstance.type}) to player ${ownerPlayer.id}'s field.`);
            fieldChanged = true;
        } else {
            console.warn(`GM: AmuletPlayed event received for ${amuletInstance.name}, but player ${ownerPlayer.id}'s field is full (${ownerPlayer.field.length}/${MAX_FIELD_SIZE}). Amulet not added client-side.`);
        }
    } else {
        console.warn(`GM: AmuletPlayed event received for amulet UID ${amuletInstance.uid} which is already present on player ${ownerPlayer.id}'s field.`);
    }

    // Re-render if hand or field changed
    if (handChanged || fieldChanged) {
        renderGame(gameState);
    }
}

export function handleSpellPlayed(eventData) {
    // DTO: SpellPlayedEventDto { string PlayerId, string SpellCardId, string OriginalCardUid, string TargetUid }
    console.log(`%cGM HANDLE: handleSpellPlayed`, 'color: #6610f2; font-weight: bold;', eventData);
    console.log('%c[EVENT RECEIVED] handleAmuletPlayed:', 'background: #343a40; color: #ffffff; font-weight: bold;', eventData); // Dark gray background (same as Banished)
    // Corrected validation to use camelCase properties from DTO: sourcePlayerId, cardId, sourceCardUid
    if (!eventData || !eventData.sourcePlayerId || !eventData.cardId || !eventData.sourceCardUid) {
        console.error("GM Error: Invalid data in handleSpellPlayed (expected sourcePlayerId, cardId, sourceCardUid):", eventData);
        return;
    }
    // Spells don't create persistent entities. State changes (PP, hand, target stats, destruction)
    // are handled by other dedicated events (PpUpdate, HandUpdate, StatChange, UnitDestroyed, etc.).
    // This handler is primarily for logging or triggering VFX.
    // Corrected log to use camelCase properties
    console.log(`GM: Spell (Original UID: ${eventData.sourceCardUid}, CardID: ${eventData.cardId}) played by ${eventData.sourcePlayerId}. Target: ${eventData.targetUid ?? 'None'}`);
    // Future: Trigger spell animation/VFX based on spellCardId or other event properties?
    // No state change or re-render needed here, rely on subsequent events.
}

export function handleUnitEvolved(eventData) {
    // DTO: UnitEvolvedEventDto { string PlayerId, string UnitUid, UnitStateDto EvolvedState, int EpLeft }
    console.log(`%cGM HANDLE: handleUnitEvolved`, 'color: #e83e8c; font-weight: bold;', eventData);
    if (!eventData || !eventData.playerId || !eventData.unitUid || !eventData.evolvedState || eventData.epLeft === undefined) {
        console.error("GM Error: Invalid data in handleUnitEvolved:", eventData);
        return;
    }
    const player = getPlayerState(eventData.playerId);
    const unit = findCardInstanceByUid(eventData.unitUid);

    console.log('%c[EVENT RECEIVED] handleGameOver:', 'background: #000000; color: #ff0000; font-weight: bold;', eventData); // Black background, Red text
    let updated = false;
    if (player) {
        player.ep = eventData.epLeft;
        console.log(`GM: Player ${player.id} EP updated to ${player.ep} after evolution.`);
        updated = true;
    } else {
        console.warn(`GM: UnitEvolved event received for invalid player: ${eventData.playerId}`);
    }

    if (unit && eventData.evolvedState) {
        // HYDRATION: Update specific properties from evolvedState DTO
        unit.isEvolved = eventData.evolvedState.isEvolved ?? true; // Assume true if state provided
        unit.currentAttack = eventData.evolvedState.currentAttack ?? unit.currentAttack;
        unit.currentHealth = eventData.evolvedState.currentHealth ?? unit.currentHealth;
        unit.maxHealth = eventData.evolvedState.maxHealth ?? unit.maxHealth; // Update max health too
        unit.canAttack = eventData.evolvedState.canAttack ?? unit.canAttack; // Update canAttack status (Rush on evolve)
        // Potentially update keywords or other properties based on evolvedState DTO
        console.log(`GM: Unit ${unit.name} evolved. New State: Atk=${unit.currentAttack}, HP=${unit.currentHealth}/${unit.maxHealth}, Evolved=${unit.isEvolved}, CanAttack=${unit.canAttack}`);
        updated = true;
    } else {
        console.warn(`GM: UnitEvolved event received for invalid unit UID: ${eventData.unitUid}`);
    }

    if (updated) {
        renderGame(gameState); // Re-render if player EP or unit state changed
    }
}

export function handleStatChange(eventData) {
    // DTO: StatChangeEventDto { string UnitId, int NewAttack, int NewHealth }
    console.log(`%cGM HANDLE: handleStatChange`, 'color: #fd7e14; font-weight: bold;', eventData);
    // Note: DTO uses UnitId
    if (!eventData || !eventData.unitId || eventData.newAttack === undefined || eventData.newHealth === undefined) {
        console.error("GM Error: Invalid data in handleStatChange:", eventData);
        return;
    }
    const unit = findCardInstanceByUid(eventData.unitId); // Use unitId from DTO
    if (unit) {
        // Update base and current stats for consistency unless server logic differs
        unit.attack = eventData.newAttack;
        unit.health = eventData.newHealth;
        unit.currentAttack = eventData.newAttack; // Reflect change in current stats too
        unit.currentHealth = eventData.newHealth; // Reflect change in current stats too
        console.log(`GM: Unit ${unit.name} (UID: ${unit.uid}) stats changed to A:${unit.attack}/H:${unit.health}`);
        renderGame(gameState); // Re-render after stat update
    } else {
        console.warn(`GM: StatChange event received for invalid unit ID: ${eventData.unitId}`);
    }
}

export function handleUnitDestroyed(eventData) {
    // DTO: UnitDestroyedEventDto { string UnitUid }
    console.log(`%cGM HANDLE: handleUnitDestroyed`, 'color: #dc3545; font-weight: bold;', eventData);
    if (!eventData || !eventData.unitUid) {
        console.error("GM Error: Invalid data in handleUnitDestroyed:", eventData);
        return;
    }
    const unit = findCardInstanceByUid(eventData.unitUid);
    if (unit) {
        handleDestruction(unit); // Use the existing helper function
        renderGame(gameState); // Re-render after destruction
    } else {
        console.warn(`GM: UnitDestroyed event received for invalid unit UID: ${eventData.unitUid}`);
    }
}

export function handleAttackDeclared(eventData) {
    // DTO: Assume AttackDeclaredEventDto { string AttackerUid, string TargetUid }
    console.log(`%cGM HANDLE: handleAttackDeclared`, 'color: #dc3545; font-weight: bold;', eventData); // Red color for attack declaration
    if (!eventData || !eventData.attackerUid || !eventData.targetUid) {
        console.error("GM Error: Invalid data in handleAttackDeclared:", eventData);
        return;
    }
    const attacker = findCardInstanceByUid(eventData.attackerUid);
    const target = findCardInstanceByUid(eventData.targetUid);

    if (!attacker || !target) {
        console.warn(`GM: AttackDeclared event received for invalid attacker/target UID: Attacker=${eventData.attackerUid}, Target=${eventData.targetUid}`);
        return;
    }

    console.log(`GM: Attack declared - ${attacker.name} (UID: ${attacker.uid}) is attacking ${target.name} (UID: ${target.uid}). Waiting for AttackOccurred event for results.`);

    // Future: Trigger attack animation start here?
    // Example: showAttackArrow(attacker.uid, target.uid);

    // No state change or re-render needed here usually, wait for AttackOccurred.
    // renderGame(gameState); // Avoid re-rendering unless specific UI effect requires it
}
export function handleDamageDealt(eventData) {
    // DTO: Assume DamageDealtEventDto { string? SourceUid, string TargetUid, int DamageAmount }
    console.log(`%cGM HANDLE: handleDamageDealt`, 'color: #ff6347; font-weight: bold;', eventData); // Tomato color for damage
    if (!eventData || !eventData.targetUid || eventData.damageAmount === undefined) {
        console.error("GM Error: Invalid data in handleDamageDealt:", eventData);
        return;
    }

    const target = findCardInstanceByUid(eventData.targetUid);
    if (!target) {
        console.warn(`GM: DamageDealt event received for invalid target UID: ${eventData.targetUid}`);
        return;
    } // Closing brace for if (!target)

    // Log source if available
    if (eventData.sourceUid) {
        const source = findCardInstanceByUid(eventData.sourceUid);
        console.log(`GM: Target ${target.name} (UID: ${target.uid}) received ${eventData.damageAmount} damage from Source UID: ${eventData.sourceUid} (${source?.name ?? 'Unknown Source'}).`);
    } else {
        console.log(`GM: Target ${target.name} (UID: ${target.uid}) received ${eventData.damageAmount} damage from Unknown Source.`);
    }


    // Update target health locally (Server should send UnitDestroyed separately if HP <= 0)
    // We might receive a StatChange event too, but updating here provides immediate feedback.
    // Let's be cautious and only update if health > 0, let UnitDestroyed handle removal.
    if (target.currentHealth > 0) {
        // Avoid going below zero visually before destruction event arrives
        target.currentHealth = Math.max(0, target.currentHealth - eventData.damageAmount);
        console.log(`GM: Updated target ${target.name} health to ${target.currentHealth}.`);
    } else {
        console.log(`GM: Target ${target.name} health was already ${target.currentHealth} or less. Awaiting potential destruction event.`);
    }


    // Show damage number visual effect
    if (eventData.damageAmount > 0) {
        showDamageNumber(target.uid, eventData.damageAmount);
    }

    renderGame(gameState); // Re-render after health update and damage number display
}

export function handleHealthChanged(eventData) {
    // DTO: Assume HealthChangedEventDto { string UnitUid, int NewHealth }
    console.log(`%cGM HANDLE: handleHealthChanged`, 'color: #20c997; font-weight: bold;', eventData); // Teal color
    if (!eventData || !eventData.unitUid || eventData.newHealth === undefined) {
        console.error("GM Error: Invalid data in handleHealthChanged:", eventData);
        return;
    }
    const unit = findCardInstanceByUid(eventData.unitUid); // Can affect followers or leaders
    if (unit) {
        const oldHealth = unit.currentHealth;
        unit.currentHealth = eventData.newHealth;

        console.log(`GM: Unit ${unit.name} (UID: ${unit.uid}) health changed from ${oldHealth} to ${unit.currentHealth}.`);

        // Optional: Show healing/damage number visual effect (needs more context)
        // if (unit.currentHealth > oldHealth) { showHealingNumber(unit.uid, unit.currentHealth - oldHealth); }

        // Log if health drops to 0 or below, but let server events handle actual destruction
        if (unit.currentHealth <= 0) {
            console.log(`GM: Unit ${unit.name} health reached ${unit.currentHealth}. Waiting for server destruction event.`);
        }

        renderGame(gameState); // Re-render after health update
    } else {
        console.warn(`GM: HealthChanged event received for invalid unit UID: ${eventData.unitUid}`);
    }
}

export function handleMulliganStarted(eventData) {
    // DTO: MulliganStartedEventDto { mulliganTimer?: int }
    console.log(`%cGM HANDLE: handleMulliganStarted`, 'color: #20c997; font-weight: bold;', eventData);
    gameState.gamePhase = "Mulligan";
    // UI should now allow mulligan actions. Timer handling (if any) would use eventData.mulliganTimer
    console.log(`GM: Mulligan phase started. Timer (if provided): ${eventData?.mulliganTimer}`);
    // Reset player mulligan completion flags here? Server DTO should be source of truth,
    // but resetting ensures clean state if reconnect happens during mulligan.
    gameState.player1MulliganComplete = false;
    gameState.player2MulliganComplete = false;
    renderGame(gameState); // Render to show mulligan interface
}

export function handlePlayerMulliganCompleted(eventData) {
    // DTO: PlayerMulliganCompletedEventDto { sourcePlayerId: string }
    console.log(`%cGM HANDLE: handlePlayerMulliganCompleted`, 'color: #20c997; font-weight: bold;', eventData);
    if (!eventData || !eventData.sourcePlayerId) {
        console.error("GM Error: Invalid data in handlePlayerMulliganCompleted:", eventData);
        return;
    }
    const completedPlayerId = eventData.sourcePlayerId;
    if (completedPlayerId === 'player1') { // Assuming player IDs are 'player1', 'player2'
        gameState.player1MulliganComplete = true;
        console.log("GM: Updated gameState.player1MulliganComplete to true.");
    } else if (completedPlayerId === 'player2') {
        gameState.player2MulliganComplete = true;
        console.log("GM: Updated gameState.player2MulliganComplete to true.");
    } else {
        console.warn(`GM: PlayerMulliganCompleted event received for unknown player ID: ${completedPlayerId}`);
    }
    // Re-render to reflect the change (e.g., hide the mulligan button for the completed player)
    renderGame(gameState);
}

export function handleGameOver(eventData) {
    // DTO: GameOverEventDto { winnerId: string, reason?: string }
    console.log(`%cGM HANDLE: handleGameOver`, 'color: #dc3545; font-weight: bold;', eventData);
    if (!eventData || !eventData.winnerId) {
        console.error("GM Error: Invalid data in handleGameOver:", eventData);
        return;
    }
    gameState.gameOver = true;
    gameState.gamePhase = "GameOver";
    gameState.winner = eventData.winnerId;
    // Update UI to show game over screen with winner/loser info
    console.log(`GM: Game over! Winner: ${gameState.winner}. Reason: ${eventData.reason || 'N/A'}`);
    alert(`游戏结束！ 玩家 ${gameState.winner} 获胜！ (${eventData.reason || "原因未知"})`);
    renderGame(gameState); // Render final game over state
    // Consider disabling all further interactions more forcefully
    // disablePlayerInteractions(); // REMOVED: Relying on existing logic per user preference
}

export function handlePlayerActionError(eventData) {
    // DTO: PlayerActionErrorEventDto { playerId: string, actionType?: string, message: string }
    console.log(`%cGM HANDLE: handlePlayerActionError`, 'color: #ffc107; font-weight: bold;', eventData);
    if (!eventData || !eventData.playerId || !eventData.message) {
        console.error("GM Error: Invalid data in handlePlayerActionError:", eventData);
        return;
    }
    console.warn(`GM: Player action error received for ${eventData.playerId} (Action: ${eventData.actionType || 'Unknown'}): ${eventData.message}`);
    // Display the error message *only* to the specific player who caused the error
    if (eventData.playerId === gameState.localPlayerId) {
        alert(`操作错误 (${eventData.actionType || '未知'}): ${eventData.message}`);
        // Optionally re-enable controls or clear selections if the action failed
        // e.g., if play card failed, maybe clear selectedCardUid
        // This depends on server guarantees and desired UX
    }
    // No re-render usually needed unless error implies state needs reverting/clearing selections
}
// --- END NEW Specific Event Handlers --- (Placeholder comment for structure)
