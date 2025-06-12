// src/gameManager.js - 核心游戏状态与逻辑管理
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
// - API集成: 文档/03-前端架构/03-API集成.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
//

import { createPlayer } from "./player.js"; // 正确导入
import { getCardDefinition, createCardInstance } from "./cardManager.js"; // ADDED: createCardInstance for hydration
// import { getCardDefinition } from './cardManager.js'; // initGame 不需要
// import { renderGame } from './ui.js'; // 渲染调用在 main.js
import { renderGame, showDamageNumber, updatePpDisplay, updateEpDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateHealthDisplay, clearSelectionHighlights, updateHandDisplay, addUnitToField, updateUnitDisplay, removeUnitFromField, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus, updateSingleCardHighlight } from "./ui.js"; // Import UI functions + specific updates + clearSelectionHighlights + updateHandDisplay + addUnitToField + updateUnitDisplay + removeUnitFromField + highlightAttacker + highlightTargets + showMulliganInterface + updateMulliganStatus + updateSingleCardHighlight
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
    lastPlayedCardInstanceId: null, // BUG-016: Temp state for card played
    playabilityCache: {}, // NEW: Cache for card playability check results { cardUid: PlayabilityResultDto }
};
// 初始化游戏 (已实现版本)
/**
 * Receives the SignalR connection object from main.js.
 * @param {signalR.HubConnection} connection - The active SignalR connection.
 */
export function setSignalRConnection(connection) {
    // ADDED export
    console.log("GAME MANAGER: Setting SignalR connection and registering handlers."); // Updated log
    signalRConnection = connection;

    // --- Register SignalR Event Handlers ---
    // Ensure handlers are registered only once per connection setup.
    // Remove existing handlers first to prevent duplicates if this function is called again.
    connection.off("ReceiveGameState");
    connection.off("ReceiveGameEvent");
    connection.off("ReceiveError");
    connection.off("ReceivePlayabilityResult"); // Remove previous handler if any

    connection.on("ReceiveGameState", (gameStateDto) => {
        console.log("GM: Received full GameState DTO:", gameStateDto);
        updateFullGameState(gameStateDto); // Use the hydration function
        // Initial render after receiving state
        console.log("%c[UI_TRIGGER] Triggering initial renderGame from ReceiveGameState", 'color: blue; font-weight: bold;');
        renderGame(gameState); // Use the imported renderGame function
    });

    connection.on("ReceiveGameEvent", (gameEventDto) => {
        console.log("GM: Received GameEvent DTO:", gameEventDto);
        processGameEvent(gameEventDto); // Process the event to update local state
        // Note: processGameEvent or its sub-handlers should now trigger renderGame
    });

    connection.on("ReceiveError", (errorMessage) => {
        console.error("GM: Received error from server:", errorMessage);
        alert(`服务器错误: ${errorMessage}`);
        // Potentially trigger UI state change (e.g., show error panel)
    });

    // NEW: Handler for Playability Check Results
    connection.on("ReceivePlayabilityResult", (resultDto) => {
        handlePlayabilityResult(resultDto); // Call the dedicated handler function
    });

    // Add more handlers as needed for other server-to-client messages
    // e.g., connection.on("PlayerJoined", (playerInfo) => { ... });

    console.log("GM: SignalR event handlers registered (including ReceivePlayabilityResult).");
    // --- End Register SignalR Event Handlers ---
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
        // Original leader check (now acts as fallback for GUIDs or if player ID didn't match)
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

    // --- DEBUG: Check player state retrieval ---
    console.log(`%c[DEBUG_PLAYER_STATE] Attempting playCard for card UID: ${cardUid}`, 'color: orange; font-weight: bold;');
    console.log(`%c[DEBUG_PLAYER_STATE] Card Instance found:`, 'color: orange;', cardInstance ? JSON.parse(JSON.stringify(cardInstance)) : 'null'); // Log instance copy
    console.log(`%c[DEBUG_PLAYER_STATE] Attempting to get player state for ownerId: ${cardInstance?.ownerId}`, 'color: orange;');
    console.log(`%c[DEBUG_PLAYER_STATE] Current gameState.players (IDs): [${gameState.players?.map(p => p?.id).join(', ')}]`, 'color: orange;');
    // --- END DEBUG ---

    const playerState = getPlayerState(cardInstance?.ownerId); // Use optional chaining for safety
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
    const cardData = getCardDefinition(cardInstance.id); // FIX: Use lowercase 'id' property
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
        // Construct the DTO object expected by the server Hub method (PascalCase)
        const actionDto = {
            CardUid: cardUid, // Use PascalCase
            TargetUid: targetUid // Use PascalCase
        };
        console.log("GM: Constructed PlayCard DTO for invoke:", actionDto); // Log the object being sent

        // Invoke with the single DTO object
        // BUG-016: Store UID before invoking, so PpUpdate can remove it from hand
        gameState.lastPlayedCardInstanceId = cardUid;
        console.log(`%c[BUG-016] Stored lastPlayedCardInstanceId: ${gameState.lastPlayedCardInstanceId}`, 'color: magenta; font-weight: bold;');
        await signalRConnection.invoke("PlayCard", actionDto); // Pass the DTO object

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
        clearSelectionHighlights(); // Use specific UI update to clear selection highlight on error.
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
    console.log("GM: Calling specific UI functions for attack initiation highlights.");
    clearSelectionHighlights(); // Clear previous highlights first
    highlightAttacker(attackerUid); // Highlight the selected attacker
    highlightTargets(attackerInstance); // Highlight potential targets for this attacker
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
        clearSelectionHighlights(); // Use specific UI update
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
        clearSelectionHighlights(); // Render to clear selection
        return;
    }

    // Construct the DTO object expected by the server Hub method (PascalCase)
    const actionDto = {
        AttackerUid: attackerUid, // Use PascalCase
        TargetUid: targetUid      // Use PascalCase
    };
    console.log(
        `GM: Invoking SignalR Hub method 'PerformAttack'. GameID: ${gameState.gameId}, Action DTO:`, actionDto
    );
    try {
        // Invoke server method with the single DTO object. Server handles validation and attack execution.
        await signalRConnection.invoke("PerformAttack", actionDto); // Pass the DTO object

        // Wait for server events (AttackOccurred, UnitDestroyed, GameOver) via processGameEvent.
        // --- BUG-017 FIX: Optimistically disable attacker client-side ---
        const attackerToDisable = findCardInstanceByUid(attackerUid); // Find instance again
        if (attackerToDisable) {
            attackerToDisable.canAttack = false;
            console.log(`%c[BUG-017] Optimistically set ${attackerToDisable.name} (UID: ${attackerUid}) canAttack to false client-side.`, 'color: blue; font-weight: bold;');
        } else {
             console.warn(`%c[BUG-017] Could not find attacker instance (UID: ${attackerUid}) to optimistically disable canAttack.`, 'color: orange;');
        }
        // --- END BUG-017 FIX ---
        console.log(
            "GM: 'PerformAttack' invocation sent successfully. Waiting for server response..."
        );

        // Optimistically clear selection and provide feedback
        alert(`${attackerInstance.name} 正在攻击 ${targetInstance.name}...`);
        gameState.selectedAttackerUid = null; // Clear selection locally *after* successful invoke
        // Re-render specifically to clear highlights and update attacker state
        clearSelectionHighlights();
        updateUnitDisplay(attackerInstance); // Update the attacker's display (shows canAttack=false)
    } catch (err) {
        console.error("GM: SignalR 'PerformAttack' invocation failed:", err);
        let errorMessage = "服务器通信错误";
        if (err && err.message) {
            errorMessage = err.message;
        }
        alert(`攻击失败：${errorMessage}`);

        // Clear selection on error
        gameState.selectedAttackerUid = null;
        clearSelectionHighlights(); // Use specific UI update
    }

    // Trigger UI update
    console.log("GM: Attack target selected and PerformAttack invoked. UI updated in try block."); // Updated log message
    // renderGame(gameState); // REMOVED redundant render call
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
            ownerPlayer.graveyard = [...ownerPlayer.graveyard, destroyedCard]; // Create new array
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
        clearSelectionHighlights(); // <<<< REPLACED with specific UI call
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
    if (!playerState.canEvolve) {
        console.warn("GM: EvolveCard rejected - Local state indicates evolution already used or unavailable.");
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
        // Construct DTO (PascalCase) - Fixed property name to match backend
        const actionDto = { UnitUid: cardUid }; // Use PascalCase and correct property name
        await signalRConnection.invoke("PerformEvolve", actionDto); // Corrected Hub method name and pass DTO

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
        // Pass the array directly, as expected by the Hub method List<string> parameter
        await signalRConnection.invoke("SubmitMulligan", cardUidsToReplace); // Pass the array directly
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
        // Send the string directly, as expected by the Hub method string parameter
        await signalRConnection.invoke("EndTurn", requestId); // Send the string directly

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
} // Closing brace for sendGameAction function
/**
 * Requests the server to check the playability of a specific card in hand.
 * @param {string} cardUid - The unique identifier of the card instance in hand.
 */
export async function requestPlayabilityCheck(cardUid) {
    if (!cardUid) {
        console.warn("GM: requestPlayabilityCheck called with no cardUid.");
        return;
    }
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error("GM: requestPlayabilityCheck failed - SignalR connection not available.");
        // Optionally alert the user or handle reconnection
        return;
    }
    // Only request if it's the local player's turn and in the Main phase (basic client check)
    // Also, avoid redundant checks if the result is already cached for this turn/phase state
    // (We might need a more robust cache invalidation strategy later)
    if (gameState.localPlayerId !== gameState.currentPlayerId || gameState.phase !== "Main") {
        // console.log("GM: Skipping playability check (not local player's turn or not Main phase).");
        // Clear any stale cache for this card if the turn/phase changed
        if (gameState.playabilityCache[cardUid]) {
            delete gameState.playabilityCache[cardUid];
            // TODO: Trigger a UI update if necessary to remove old highlights
        }
        return;
    }
    // Optional: Check cache before sending request?
    // if (gameState.playabilityCache[cardUid]) {
    //     console.log(`GM: Playability check for ${cardUid} already cached. Skipping request.`);
    //     // Ensure UI reflects cached state if needed
    //     return;
    // }

    console.log(`GM: Invoking SignalR Hub method 'CheckCardPlayability'. CardUID: ${cardUid}`);
    try {
        // Server expects the card instance UID as a string argument
        await signalRConnection.invoke("CheckCardPlayability", cardUid);
        // No need to do anything here, wait for ReceivePlayabilityResult event
        console.log(`GM: 'CheckCardPlayability' for ${cardUid} invoked successfully.`);
    } catch (err) {
        console.error(`GM: SignalR 'CheckCardPlayability' invocation for ${cardUid} failed:`, err);
        // Optionally clear cache entry on error?
        delete gameState.playabilityCache[cardUid];
        // Optionally alert the user
        // alert(`无法检查卡牌 ${cardUid} 的可玩性：服务器错误`);
    }
}
/**
 * Handles the playability check result received from the server.
 * Stores the result in the cache and potentially triggers a UI update.
 * @param {object} result - The PlayabilityResultDto object from the server.
 *                          Expected properties: CardUid, IsPlayable, RequiresTargetSelection,
 *                                               HasValidTargets, CanPlayWithoutTarget, FailureReason, ValidTargetIds
 */
export function handlePlayabilityResult(result) {
    if (!result || !result.cardUid) {
        console.warn("GM: Received invalid playability result:", result);
        return;
    }

    // console.log(`GM: Received playability result for CardUID ${result.cardUid}:`, result); // Less verbose log

    // Store the result in the cache, keyed by card UID
    gameState.playabilityCache[result.cardUid] = result;

    // --- Trigger Specific UI Update ---
    // Call the dedicated function in ui.js to update only the affected card's highlight.
    // Ensure updateSingleCardHighlight is imported at the top of gameManager.js
    try {
        // console.log(`%c[UI_TRIGGER] Calling updateSingleCardHighlight for card ${result.cardUid}`, 'color: blue; font-weight: bold;');
        updateSingleCardHighlight(result.cardUid, result); // Call the specific UI update function
    } catch (error) {
        console.error(`GM: Error calling updateSingleCardHighlight for card ${result.cardUid}:`, error);
        // Fallback to full render if specific update fails? Or just log?
        // console.log("%c[UI_TRIGGER] Fallback: Triggering renderGame due to updateSingleCardHighlight error.", 'color: red; font-weight: bold;');
        // renderGame(gameState);
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
    // Use PascalCase: CurrentPP, MaxPP, CurrentEP, MaxEP, CanEvolve, HasUnlockedEvolution, TurnNumber, DeckCount, Deck
    playerInstance.pp = playerDto.CurrentPP ?? 0; // PascalCase
    playerInstance.maxPP = playerDto.MaxPP ?? 0; // PascalCase
    playerInstance.ep = playerDto.CurrentEP ?? 0; // PascalCase
    playerInstance.maxEP = playerDto.MaxEP ?? 0; // PascalCase
    // FIXED: Backend sends CanEvolve, not CanEvolveThisTurn
    playerInstance.canEvolve = playerDto.CanEvolve ?? false; // PascalCase - Fixed property name
    playerInstance.hasUnlockedEvolution = playerDto.HasUnlockedEvolution ?? false; // PascalCase
    playerInstance.turn = playerDto.TurnNumber ?? 0; // PascalCase (Already was, but good to confirm)
    playerInstance.deckCount = playerDto.DeckCount ?? (Array.isArray(playerDto.Deck) ? playerDto.Deck.length : 0); // PascalCase (x2)
    // Keep deck as simple array of IDs/DTOs from server if no methods needed client-side
    playerInstance.deck = Array.isArray(playerDto.Deck) ? [...playerDto.Deck] : []; // PascalCase (x2)


}

/**
 * Hydrates dynamic properties of a leader card instance.
 * @param {object} playerInstance - The player instance owning the leader.
 * @param {object} playerDto - The player DTO containing the leader card DTO.
 */
function _hydrateLeaderCard(playerInstance, playerDto) {
    // Use PascalCase: LeaderCard, PlayerId, CurrentHealth, Uid
    if (!playerInstance || !playerDto || !playerDto.LeaderCard || !playerInstance.leaderCard) { // PascalCase LeaderCard
        console.warn(`GM HYDRATION HELPER: Missing data for leader card hydration for player ${playerDto?.PlayerId}`); // PascalCase PlayerId
        return;
    }
    // Update currentHealth, the most critical dynamic stat
    playerInstance.leaderCard.currentHealth = playerDto.LeaderCard.CurrentHealth ?? playerInstance.leaderCard.health; // PascalCase LeaderCard.CurrentHealth
    // Update UID if server sends it (should be stable, but good practice)
    // Look for InstanceId first (likely from C# DTO), then instanceId, then Uid/uid as fallbacks
    const serverUid = playerDto.LeaderCard.InstanceId || playerDto.LeaderCard.instanceId || playerDto.LeaderCard.Uid || playerDto.LeaderCard.uid;
    playerInstance.leaderCard.uid = serverUid ?? playerInstance.leaderCard.uid; // Assign if found, otherwise keep existing (which might be null initially)
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
        // Pass the DTO directly, assuming it now contains 'id' instead of 'CardId'
        // createCardInstance expects an object with an 'id' property.
        const cardInstance = createCardInstance(cardDto);
        if (cardInstance) {
            // ownerId and location are set based on the zone being hydrated, not from DTO directly
            cardInstance.ownerId = ownerId;
            cardInstance.location = zoneName;

            // Add specific hydration for field units' dynamic state
            if (zoneName === 'field') {
                // Use PascalCase: CanAttack, CurrentAttack, CurrentHealth, IsEvolved, Keywords
                cardInstance.canAttack = cardDto.CanAttack ?? false; // PascalCase
                cardInstance.currentAttack = cardDto.CurrentAttack ?? cardInstance.attack; // PascalCase
                cardInstance.currentHealth = cardDto.CurrentHealth ?? cardInstance.health; // PascalCase
                cardInstance.isEvolved = cardDto.IsEvolved ?? false; // PascalCase
                // ADDED: Hydrate keywords (Ensure DTO has 'Keywords' property as an array)
                cardInstance.keywords = Array.isArray(cardDto.Keywords) ? [...cardDto.Keywords] : []; // PascalCase
                // console.log(`GM HYDRATION DBG: Hydrated keywords for ${cardInstance.name} (UID: ${cardInstance.uid}):`, cardInstance.keywords); // Optional Debug
            }
            // ADDED: Hydrate keywords for hand cards too (needed for UI checks like Ward indicator)
            else if (zoneName === 'hand') {
                 // Use PascalCase: Keywords
                 cardInstance.keywords = Array.isArray(cardDto.Keywords) ? [...cardDto.Keywords] : []; // PascalCase
                 // console.log(`GM HYDRATION DBG: Hydrated keywords for hand card ${cardInstance.name} (UID: ${cardInstance.uid}):`, cardInstance.keywords); // Optional Debug
            }
            // Graveyard keywords might not be needed client-side unless effects depend on them

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

    // Use PascalCase: Players, GameId
    if (!serverState || !serverState.Players || !serverState.GameId) { // PascalCase check for Players and GameId
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
    // Use PascalCase: GameId
    gameState.gameId = serverState.GameId; // PascalCase
    // Preserve localPlayerId if it was already set during initializeGame
    // gameState.localPlayerId = gameState.localPlayerId || serverState.LocalPlayerId; // Already set in init, PascalCase if needed elsewhere
    // Use PascalCase: CurrentPlayerId
    gameState.currentPlayerId = serverState.CurrentPlayerId; // PascalCase
    console.log(`GM_LOG_CID: Updated currentPlayerId to ${gameState.currentPlayerId} from updateFullGameState.`); // ADDED LOG
    // gameState.turnNumber = serverState.TurnNumber; // FIX: Removed, TurnNumber is not in GameStateDto and global turn is deprecated
    // Use PascalCase: Phase
    gameState.phase = serverState.Phase || "Unknown"; // PascalCase // Use the phase from the server state
    // Use PascalCase: GameOver
    gameState.gameOver = serverState.GameOver || false; // PascalCase
    // Use PascalCase: Winner
    gameState.winner = serverState.Winner || null; // PascalCase
    gameState.selectedCardUid = null; // Reset selections on full update
    gameState.selectedAttackerUid = null; // Reset selections on full update
    // Find player DTOs to get mulligan status
    // Use PascalCase: Players, PlayerId
    const player1Dto = serverState.Players?.find(p => p.PlayerId === 'player1'); // PascalCase (x2)
    // Use PascalCase: Players, PlayerId
    const player2Dto = serverState.Players?.find(p => p.PlayerId === 'player2'); // PascalCase (x2)
    // Read 'hasCompletedMulligan' from the correct player DTO
    // Use PascalCase: HasCompletedMulligan
    gameState.player1MulliganComplete = player1Dto?.HasCompletedMulligan ?? false; // PascalCase
    // Use PascalCase: HasCompletedMulligan
    gameState.player2MulliganComplete = player2Dto?.HasCompletedMulligan ?? false; // PascalCase
    gameState.isEndingTurn = false; // Reset flag
    // Corrected log to show the actual phase being set
    console.log(`GM HYDRATION: Assigned top-level state. GameID=${gameState.gameId}, CurrentPlayer=${gameState.currentPlayerId}, Turn=${gameState.turnNumber}, Phase=${gameState.phase}`);
    // Player hydration will replace the players array below

    // Player instantiation will happen next (original player mapping logic removed below)
    // --- Smart Hydration: Hydrate Players using Helper Functions ---
    const hydratedPlayers = [];
    // Use PascalCase: Players
    if (Array.isArray(serverState.Players)) { // PascalCase
        // Use PascalCase: Players
        for (const playerDto of serverState.Players) { // PascalCase
            // Use PascalCase: PlayerId
            if (!playerDto || !playerDto.PlayerId) { // PascalCase
                console.warn("GM HYDRATION: Skipping invalid player DTO:", playerDto);
                continue;
            }

            // --- REFACTOR: Find existing or create new player instance ---
            let playerInstance = gameState.players.find(p => p && p.id === playerDto.PlayerId); // Find existing by ID
            let isNewPlayer = false;

            if (!playerInstance) {
                // If not found, create a new one using the modified createPlayer
                // Use PascalCase: PlayerId, IsAI
                playerInstance = createPlayer(playerDto.PlayerId, playerDto.IsAI ?? false); // PascalCase (x2)
                isNewPlayer = true; // Flag that this is a new instance
                if (!playerInstance) {
                    // Use PascalCase: PlayerId
                    console.error(`GM HYDRATION: Failed to create NEW player instance for ID ${playerDto.PlayerId}. Skipping.`); // PascalCase
                    continue; // Skip this player DTO if creation fails
                }
                console.log(`%cGM HYDRATION: Created NEW player instance for ID ${playerDto.PlayerId}.`, 'color: green; font-weight: bold;');
            } else {
                console.log(`%cGM HYDRATION: Found EXISTING player instance for ID ${playerDto.PlayerId}. Updating...`, 'color: blue; font-weight: bold;');
            }
            // --- END REFACTOR ---

            // Hydrate basic properties (works for both new and existing instances)
            _hydratePlayerProperties(playerInstance, playerDto);

            // --- REFACTOR: Integrate Leader Card Hydration ---
            // Use PascalCase: LeaderCard
            if (playerDto.LeaderCard) { // PascalCase
                // Use PascalCase: LeaderCard
                const serverLeaderDto = playerDto.LeaderCard; // PascalCase
                // Use PascalCase: InstanceId, instanceId, Uid, uid
                const serverLeaderUid = serverLeaderDto.InstanceId || serverLeaderDto.instanceId || serverLeaderDto.Uid || serverLeaderDto.uid; // PascalCase (multiple checks)

                // Check if client leader exists and matches UID, or if it needs creation/replacement
                if (!playerInstance.leaderCard || playerInstance.leaderCard.uid !== serverLeaderUid) {
                    // Create a new leader instance from the server DTO
                    // NOTE: Assumes createCardInstance can handle the server DTO directly. May need adjustment later.
                    const newLeaderInstance = createCardInstance(serverLeaderDto); // Pass DTO
                    if (newLeaderInstance) {
                        newLeaderInstance.ownerId = playerInstance.id;
                        newLeaderInstance.location = 'leader_zone';
                        // Hydrate dynamic state from DTO
                        // Use PascalCase: CurrentHealth
                        newLeaderInstance.currentHealth = serverLeaderDto.CurrentHealth ?? newLeaderInstance.health; // PascalCase
                        // Assign the new instance
                        playerInstance.leaderCard = newLeaderInstance;
                        console.log(`%cGM HYDRATION: ${isNewPlayer ? 'Created' : 'Replaced'} leader card instance (UID: ${newLeaderInstance.uid}) for player ${playerInstance.id}. Health: ${newLeaderInstance.currentHealth}`, 'color: green;');
                    } else {
                        console.error(`GM HYDRATION: Failed to create leader card instance from DTO for player ${playerInstance.id}:`, serverLeaderDto);
                        playerInstance.leaderCard = null; // Ensure it's null if creation failed
                    }
                } else {
                    // Leader exists and UID matches, just update dynamic properties
                    // Use PascalCase: CurrentHealth
                    playerInstance.leaderCard.currentHealth = serverLeaderDto.CurrentHealth ?? playerInstance.leaderCard.health; // PascalCase
                    // Add other dynamic updates if needed (e.g., effects, keywords)
                    console.log(`%cGM HYDRATION: Updated existing leader card instance (UID: ${playerInstance.leaderCard.uid}) for player ${playerInstance.id}. Health: ${playerInstance.leaderCard.currentHealth}`, 'color: blue;');
                }
            } else if (playerInstance.leaderCard) {
                // Server DTO doesn't have a leader, but client does? Log warning and remove client leader.
                console.warn(`GM HYDRATION: Server DTO for player ${playerInstance.id} missing LeaderCard, but client has one (UID: ${playerInstance.leaderCard.uid}). Removing client leader.`);
                playerInstance.leaderCard = null;
            }
            // --- End Leader Card Hydration ---

            // Hydrate other zones (using existing _hydrateCardZone which recreates zones)
            // Use PascalCase: Hand
            playerInstance.hand = _hydrateCardZone(playerDto.Hand, playerInstance.id, 'hand'); // PascalCase
            // Use PascalCase: Field
            playerInstance.field = _hydrateCardZone(playerDto.Field, playerInstance.id, 'field'); // PascalCase
            // Use PascalCase: Graveyard
            playerInstance.graveyard = _hydrateCardZone(playerDto.Graveyard, playerInstance.id, 'graveyard'); // PascalCase
            // Deck is handled in _hydratePlayerProperties

            // Add the updated or new instance to the temporary array
            hydratedPlayers.push(playerInstance);
        }
    } else {
        // Use PascalCase: Players
        console.warn("GM HYDRATION: serverState.Players is not an array or is missing."); // PascalCase
    }
    // --- DEBUG START: Log hydrated players array before assignment ---
    console.log(`DEBUG_GM HYDRATION: Array of hydrated player instances (before filtering nulls):`, hydratedPlayers.map(p => p ? `Player ${p.id}`: 'null'));
    // --- DEBUG END ---

    // Filter out nulls before assigning
    gameState.players = hydratedPlayers.filter(p => p !== null);

    // --- DEBUG START: Log final assigned gameState.players ---
    console.log(`DEBUG_GM HYDRATION: Final gameState.players after filtering nulls:`, gameState.players.map(p => `Player ${p.id}`));
    // --- DEBUG END ---
    console.log("GM HYDRATION: Assigned hydrated player instances to gameState.players.");
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
    console.log(`DEBUG_GM HYDRATION: Attempting to find active player with ID: ${gameState.currentPlayerId}`); // Log before find
    gameState.activePlayer =
        gameState.players.find(
            (p) => p && p.id === gameState.currentPlayerId // FIX: Use p.id
        ) || null;
    console.log(`DEBUG_GM HYDRATION: Found active player:`, gameState.activePlayer ? `Player ${gameState.activePlayer.id}` : 'null'); // Log result

    console.log(`DEBUG_GM HYDRATION: Attempting to find opponent player (ID not equal to ${gameState.currentPlayerId})`); // Log before find
    gameState.opponentPlayer =
        gameState.players.find(
            (p) => p && p.id !== gameState.currentPlayerId // FIX: Use p.id
        ) || null;
    console.log(`DEBUG_GM HYDRATION: Found opponent player:`, gameState.opponentPlayer ? `Player ${gameState.opponentPlayer.id}` : 'null'); // Log result


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

    // --- ROO DEBUG: Log raw event data received by handleTurnStarted ---
    // console.log(`%c[DEBUG_TURN_START_EVENT] handleTurnStarted received raw eventData:`, 'color: purple; font-weight: bold;', JSON.parse(JSON.stringify(eventData))); // Log deep copy
    // --- END ROO DEBUG ---

    // --- Clear Playability Cache for Local Player's Turn Start ---
    // Check if the event contains the necessary state and if it's the local player's turn
    if (eventData?.GameState?.CurrentPlayerId && eventData.GameState.CurrentPlayerId === gameState.localPlayerId) {
        console.log(`%c[CACHE_INVALIDATION] Clearing playabilityCache for local player's turn start (Player: ${gameState.localPlayerId}).`, 'color: orange; font-weight: bold;');
        gameState.playabilityCache = {}; // Clear the cache object
    }
    // --- End Cache Clearing ---


    // The "TurnStarted" event now carries the full GameStateDto nested within GameState property.
    // Delegate the processing to the function designed for full state updates, passing the nested object.
    // Use PascalCase: GameState
    if (eventData && eventData.GameState) { // PascalCase
        updateFullGameState(eventData.GameState); // Pass the nested GameState object
    } else {
        console.error("GM Error: TurnStarted event received without a valid nested GameState object:", eventData);
        alert("错误：回合开始事件数据无效！");
    }

    // No need for separate logic here to update currentPlayerId, phase, player turns, or render.
    // updateFullGameState handles all necessary state updates and the final render call.
    console.log("GM: Delegated TurnStarted event processing to updateFullGameState.");
}

export function handleTurnEnded(eventData) {
    console.log('%c[EVENT RECEIVED] handleTurnEnded:', 'background: #dc3545; color: #ffffff; font-weight: bold;', eventData); // Red background
    // DTO: TurnEndedEventDto { string NextPlayerId, int NewTurnNumber, List<PlayerTurnEndUpdateDto> PlayerUpdates }
    // Note: Most state updates (currentPlayerId, turnNumber, PP/EP, card draw) are now handled by the subsequent TurnStarted event.
    // This handler primarily logs and renders the intermediate state if needed.
    // Robust check for newTurnNumber (null or undefined)
    // Refined validation: Check for object, property existence, and non-null/undefined values
    // REMOVED check for newTurnNumber as it's no longer in the DTO
    if (!eventData ||
        !eventData.hasOwnProperty('NextPlayerId') || eventData.NextPlayerId === null || eventData.NextPlayerId === undefined) { // PascalCase
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
    console.log(`GM: Turn ended. Expected next player: ${eventData.NextPlayerId}. Waiting for TurnStarted event.`); // PascalCase

    // Do NOT change currentPlayerId or turnNumber here.
    // Do NOT update PP/EP or draw cards here.

    // renderGame(gameState); // REMOVED: Rendering handled by subsequent TurnStarted event.
}
export function handlePpUpdate(eventData) {
    console.log('%c[EVENT RECEIVED] handlePpUpdate:', 'background: #ffc107; color: #000000; font-weight: bold;', eventData); // Yellow background
    // DTO: PpUpdateEventDto { string SourcePlayerId, int CurrentPp, int MaxPp }
    console.log(`%cGM HANDLE: handlePpUpdate`, 'color: #ffc107; font-weight: bold;', eventData);
    if (!eventData || !eventData.SourcePlayerId || eventData.CurrentPp === undefined || eventData.MaxPp === undefined) { // PascalCase
        console.error("GM Error: Invalid data in handlePpUpdate:", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId); // PascalCase
    console.log(`%cGM HANDLE: handlePpUpdate SourcePlayerId`, 'color: #ffc107; font-weight: bold;', eventData.SourcePlayerId);

    let handChanged = false; // BUG-016 Flag to track if hand was modified

    if (playerToUpdate) {
        playerToUpdate.pp = eventData.CurrentPp; // PascalCase
        playerToUpdate.maxPP = eventData.MaxPp; // PascalCase
        console.log(`GM: Updated player ${playerToUpdate.id} PP to ${playerToUpdate.pp}/${playerToUpdate.maxPP}`);

        // BUG-016: Remove the card that was just played (cost deducted) from hand
        const cardToRemoveUid = gameState.lastPlayedCardInstanceId;
        console.log(`%c[BUG-016] Checking if lastPlayedCardInstanceId (${cardToRemoveUid}) needs removal from hand for player ${playerToUpdate.id}`, 'color: magenta; font-weight: bold;');
        if (cardToRemoveUid) {
            const cardIndex = playerToUpdate.hand.findIndex(c => c && c.uid === cardToRemoveUid);
            if (cardIndex > -1) {
                const removedCard = playerToUpdate.hand.splice(cardIndex, 1)[0];
                console.log(`%c[BUG-016] Removed card ${removedCard?.name} (UID: ${cardToRemoveUid}) from player ${playerToUpdate.id}'s hand via PpUpdate.`, 'color: magenta; font-weight: bold;');
                handChanged = true;
            } else {
                console.warn(`%c[BUG-016] PpUpdate handler tried to remove card UID ${cardToRemoveUid}, but it was not found in player ${playerToUpdate.id}'s hand. Maybe it was summoned from elsewhere?`, 'color: magenta;');
            }
            // Clear the temporary state regardless of whether the card was found in hand
            gameState.lastPlayedCardInstanceId = null;
            console.log(`%c[BUG-016] Cleared lastPlayedCardInstanceId.`, 'color: magenta; font-weight: bold;');
        } else {
             console.log(`%c[BUG-016] No lastPlayedCardInstanceId found, skipping hand removal check.`, 'color: magenta;');
        }

        // Render only if PP changed OR hand changed
        updatePpDisplay(playerToUpdate.id, playerToUpdate.pp, playerToUpdate.maxPP); // Use specific UI update
        // BUG-022 FIX: Update hand display if it was changed
        // Hand update is now handled by handleCardPlayed
    } else {
        console.warn(`GM: PpUpdate event received for invalid player: ${eventData.SourcePlayerId}`); // PascalCase
    }
}

export function handleEpUpdate(eventData) {
    // DTO: EpUpdateEventDto { string SourcePlayerId, int CurrentEp, int MaxEp }
    console.log(`%cGM HANDLE: handleEpUpdate`, 'color: #17a2b8; font-weight: bold;', eventData);
    // Fixed: Use correct DTO property names CurrentEp and MaxEp
    if (!eventData || !eventData.SourcePlayerId || eventData.CurrentEp === undefined || eventData.MaxEp === undefined) {
        console.error("GM Error: Invalid data in handleEpUpdate (expected SourcePlayerId, CurrentEp, MaxEp):", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.ep = eventData.CurrentEp;
        playerToUpdate.maxEP = eventData.MaxEp;

        // Update canEvolve flag based on EP availability
        playerToUpdate.canEvolve = playerToUpdate.maxEP > 0 && playerToUpdate.ep > 0;

        console.log(`GM: Updated player ${playerToUpdate.id} EP to ${playerToUpdate.ep}/${playerToUpdate.maxEP}`);

        updateEpDisplay(playerToUpdate.id, playerToUpdate.ep, playerToUpdate.maxEP);

        // Trigger UI re-render to update evolution buttons
        if (typeof renderGame === "function") {
            renderGame(gameState);
        }
    } else {
        console.warn(`GM: EpUpdate event received for invalid player: ${eventData.SourcePlayerId}`);
    }
}

// BUG-016: handleHandUpdate function removed as it's deprecated.
// Hand updates are now handled by handlePpUpdate (for card removal after play)
// and potentially handleCardDrawn (if a specific draw event exists) or full state sync.

export function handleDeckUpdate(eventData) {
    // DTO: DeckUpdateEventDto { string SourcePlayerId, int DeckCount }
    console.log(`%cGM HANDLE: handleDeckUpdate`, 'color: #fd7e14; font-weight: bold;', eventData); // Orange color
    // Use PascalCase: SourcePlayerId, DeckCount
    if (!eventData || !eventData.SourcePlayerId || eventData.DeckCount === undefined) { // PascalCase
        console.error("GM Error: Invalid data in handleDeckUpdate:", eventData);
        return;
    }
    // Use PascalCase: SourcePlayerId
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId); // PascalCase
    if (playerToUpdate) {
        // Use PascalCase: DeckCount
        playerToUpdate.deckCount = eventData.DeckCount; // PascalCase
        console.log(`GM: Updated player ${playerToUpdate.id}'s deck count to ${playerToUpdate.deckCount}`);
        // renderGame(gameState); // Re-render after deck count update - REPLACED
        updateDeckCountDisplay(playerToUpdate.id, playerToUpdate.deckCount); // Use specific UI update
    } else {
        // Use PascalCase: SourcePlayerId
        console.warn(`GM: DeckUpdate event received for invalid player: ${eventData.SourcePlayerId}`); // PascalCase
    }
} // Restored function definition
export function handleGraveyardUpdate(eventData) {
    // DTO: GraveyardUpdateEventDto { string SourcePlayerId, int GraveyardCount, CardDto? CardAdded, string? CardRemovedUid }
    console.log(`%cGM HANDLE: handleGraveyardUpdate`, 'color: #6c757d; font-weight: bold;', eventData); // Gray color
    if (!eventData || !eventData.SourcePlayerId || eventData.GraveyardCount === undefined) { // PascalCase
        console.error("GM Error: Invalid data in handleGraveyardUpdate (missing SourcePlayerId or GraveyardCount):", eventData); // PascalCase in message
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId); // PascalCase
    if (!playerToUpdate) {
        console.warn(`GM: GraveyardUpdate event received for invalid player: ${eventData.SourcePlayerId}`); // PascalCase
        return;
    }

    let updated = false;
    console.log('%c[EVENT RECEIVED] handleUnitAttacked:', 'background: #dc3545; color: #ffffff; font-weight: bold;', eventData); // Red background (same as TurnEnded)

    // Handle card removal (e.g., Necromancy effect)
    if (eventData.CardRemovedUid) { // PascalCase
        const cardIndex = playerToUpdate.graveyard.findIndex(c => c && c.uid === eventData.CardRemovedUid); // PascalCase
        if (cardIndex > -1) {
            const removedCard = playerToUpdate.graveyard.splice(cardIndex, 1)[0];
            console.log(`GM: Removed card ${removedCard?.name} (UID: ${eventData.CardRemovedUid}) from player ${playerToUpdate.id}'s graveyard.`); // PascalCase
            updated = true;
        } else {
            console.warn(`GM: GraveyardUpdate requested removal of card UID ${eventData.CardRemovedUid}, but it was not found in player ${playerToUpdate.id}'s graveyard.`); // PascalCase
        }
    }

    // Handle card addition (e.g., unit destroyed, card discarded)
    if (eventData.CardAdded) { // PascalCase
        const newCardInstance = createCardInstance(eventData.CardAdded); // PascalCase
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
            console.error(`GM: Failed to create instance for added card via GraveyardUpdate for player ${playerToUpdate.id}:`, eventData.CardAdded); // PascalCase
        }
    }

    console.log('%c[EVENT RECEIVED] handleUnitDamaged:', 'background: #ffc107; color: #000000; font-weight: bold;', eventData); // Yellow background (same as PpUpdate)
    // Update graveyard count (server is source of truth)
    const localGraveyardCount = playerToUpdate.graveyard.length;
    if (localGraveyardCount !== eventData.GraveyardCount) { // PascalCase
        console.warn(`GM: Graveyard count mismatch for player ${playerToUpdate.id}. Local: ${localGraveyardCount}, Server DTO: ${eventData.GraveyardCount}.`); // PascalCase
        // If UI relies on a separate count variable, update it:
        // playerToUpdate.graveyardCount = eventData.GraveyardCount; // PascalCase
    } else {
        console.log(`GM: Updated player ${playerToUpdate.id}'s graveyard. New count: ${eventData.GraveyardCount}`); // PascalCase
    }


    // Update the graveyard count display specifically
    // The server DTO count is the source of truth
    updateGraveyardCountDisplay(playerToUpdate.id, eventData.GraveyardCount); // Use specific UI update
    // No full render needed if only the count changed and UI reflects it
    // if (updated) { // Keep this conditional logic if other graveyard-related UI needs full refresh
    //     renderGame(gameState); // Re-render if graveyard changed
    // }
    // REMOVED EXTRA BRACE
}

// Removed handleUnitPlayed function
// Removed handleAmuletPlayed function

// Removed handleSpellPlayed function

export function handleUnitEvolved(eventData) {
    console.log('%c[BUG-015 LOG] handleUnitEvolved START', 'color: orange; font-weight: bold;', eventData); // ADDED BUG LOG
    // DTO: UnitEvolvedEventDto { string SourcePlayerId, CardInstanceDto EvolvedUnitState }
    console.log(`%cGM HANDLE: handleUnitEvolved`, 'color: #e83e8c; font-weight: bold;', eventData);
    if (!eventData || !eventData.SourcePlayerId || !eventData.EvolvedUnitState) { // PascalCase - Fixed to match actual DTO
        console.error("GM Error: Invalid data in handleUnitEvolved:", eventData);
        console.log('%c[BUG-015 LOG] handleUnitEvolved END (Invalid Data)', 'color: orange; font-weight: bold;'); // ADDED BUG LOG
        return;
    }
    const player = getPlayerState(eventData.SourcePlayerId); // PascalCase - Fixed property name
    const unitUid = eventData.EvolvedUnitState.InstanceId; // Get UID from EvolvedUnitState
    const unit = findCardInstanceByUid(unitUid);

    console.log('%c[EVENT RECEIVED] handleUnitEvolved:', 'background: #e83e8c; color: #ffffff; font-weight: bold;', eventData); // Fixed log message
    let updated = false;

    // Note: EP update should be handled by separate EpUpdateEvent, not here
    // This event only handles the unit evolution state changes

    if (unit && eventData.EvolvedUnitState) { // PascalCase
        console.log(`%c[BUG-015 LOG] handleUnitEvolved Unit ${unit?.name} state before update: Atk=${unit?.currentAttack}, HP=${unit?.currentHealth}, Evolved=${unit?.isEvolved}, CanAttack=${unit?.canAttack}`, 'color: orange;'); // ADDED BUG LOG
        // HYDRATION: Update specific properties from EvolvedUnitState DTO
        unit.isEvolved = eventData.EvolvedUnitState.IsEvolved ?? true; // PascalCase (nested) // Assume true if state provided
        unit.currentAttack = eventData.EvolvedUnitState.CurrentAttack ?? unit.currentAttack; // PascalCase (nested)
        unit.currentHealth = eventData.EvolvedUnitState.CurrentHealth ?? unit.currentHealth; // PascalCase (nested)
        unit.maxHealth = eventData.EvolvedUnitState.MaxHealth ?? unit.maxHealth; // PascalCase (nested) // Update max health too
        unit.canAttack = eventData.EvolvedUnitState.CanAttack ?? unit.canAttack; // PascalCase (nested) // Update canAttack status (Rush on evolve)

        // Update card type to evolved version if needed
        if (unit.type === 'Follower' && unit.isEvolved) {
            unit.type = 'Follower_Evolved';
        }

        // Potentially update keywords or other properties based on EvolvedUnitState DTO
        console.log(`GM: Unit ${unit.name} evolved. New State: Atk=${unit.currentAttack}, HP=${unit.currentHealth}/${unit.maxHealth}, Evolved=${unit.isEvolved}, CanAttack=${unit.canAttack}`);
        console.log(`%c[BUG-015 LOG] handleUnitEvolved Unit ${unit?.name} state AFTER update: Atk=${unit?.currentAttack}, HP=${unit?.currentHealth}, Evolved=${unit?.isEvolved}, CanAttack=${unit?.canAttack}`, 'color: orange;'); // ADDED BUG LOG
        updated = true;
    } else {
        console.warn(`GM: UnitEvolved event received for invalid unit UID: ${unitUid}`); // PascalCase
    }

    if (updated) {
        console.log('%c[BUG-015 LOG] handleUnitEvolved calling specific UI updates', 'color: orange;'); // ADDED BUG LOG & MODIFIED
        // Re-render specifically the unit (EP display will be updated by separate EpUpdateEvent)
        updateUnitDisplay(unit);
    }
    console.log('%c[BUG-015 LOG] handleUnitEvolved END', 'color: orange; font-weight: bold;'); // ADDED BUG LOG
}

export function handleStatChange(eventData) {
    console.log('%c[BUG-015 LOG] handleStatChange START', 'color: orange; font-weight: bold;', eventData); // ADDED BUG LOG
    // DTO: StatChangeEventDto { string UnitId, int NewAttack, int NewHealth }
    console.log(`%cGM HANDLE: handleStatChange`, 'color: #fd7e14; font-weight: bold;', eventData);
    // Note: DTO uses UnitId
    if (!eventData || !eventData.UnitId || eventData.NewAttack === undefined || eventData.NewHealth === undefined) { // PascalCase
        console.error("GM Error: Invalid data in handleStatChange:", eventData);
        console.log('%c[BUG-015 LOG] handleStatChange END (Invalid Data)', 'color: orange; font-weight: bold;'); // ADDED BUG LOG
        return;
    }
    const unit = findCardInstanceByUid(eventData.UnitId); // PascalCase // Use UnitId from DTO
    if (unit) {
        console.log(`%c[BUG-015 LOG] handleStatChange Unit ${unit?.name} state before update: Atk=${unit?.currentAttack}, HP=${unit?.currentHealth}`, 'color: orange;'); // ADDED BUG LOG
        // Update base and current stats for consistency unless server logic differs
        unit.attack = eventData.NewAttack; // PascalCase
        unit.health = eventData.NewHealth; // PascalCase
        unit.currentAttack = eventData.NewAttack; // PascalCase // Reflect change in current stats too
        unit.currentHealth = eventData.NewHealth; // PascalCase // Reflect change in current stats too
        console.log(`GM: Unit ${unit.name} (UID: ${unit.uid}) stats changed to A:${unit.attack}/H:${unit.health}`);
        console.log(`%c[BUG-015 LOG] handleStatChange Unit ${unit?.name} state AFTER update: Atk=${unit?.currentAttack}, HP=${unit?.currentHealth}`, 'color: orange;'); // ADDED BUG LOG
        console.log('%c[BUG-015 LOG] handleStatChange calling specific UI update', 'color: orange;'); // MODIFIED LOG
        updateUnitDisplay(unit); // Use specific UI update instead of full render
    } else {
        console.warn(`GM: StatChange event received for invalid unit ID: ${eventData.UnitId}`); // PascalCase
    }
    console.log('%c[BUG-015 LOG] handleStatChange END', 'color: orange; font-weight: bold;'); // ADDED BUG LOG
}

export function handleUnitDestroyed(eventData) {
    console.log('%c[BUG-015 LOG] handleUnitDestroyed START', 'color: orange; font-weight: bold;', eventData); // ADDED BUG LOG
    // DTO: UnitDestroyedEventDto { string UnitUid }
    console.log(`%cGM HANDLE: handleUnitDestroyed`, 'color: #dc3545; font-weight: bold;', eventData);
    if (!eventData || !eventData.UnitUid) { // PascalCase
        console.error("GM Error: Invalid data in handleUnitDestroyed:", eventData);
        console.log('%c[BUG-015 LOG] handleUnitDestroyed END (Invalid Data)', 'color: orange; font-weight: bold;'); // ADDED BUG LOG
        return;
    }
    const unit = findCardInstanceByUid(eventData.UnitUid); // PascalCase
    if (unit) {
        const ownerPlayer = getPlayerState(unit.ownerId); // Get owner before destruction
        console.log(`%c[BUG-015 LOG] handleUnitDestroyed Player ${ownerPlayer?.id} state before update: Field=${ownerPlayer?.field?.length}, Graveyard=${ownerPlayer?.graveyard?.length}`, 'color: orange;'); // ADDED BUG LOG
        handleDestruction(unit); // Use the existing helper function
        console.log(`%c[BUG-015 LOG] handleUnitDestroyed Player ${ownerPlayer?.id} state AFTER update: Field=${ownerPlayer?.field?.length}, Graveyard=${ownerPlayer?.graveyard?.length}`, 'color: orange;'); // ADDED BUG LOG
        console.log('%c[BUG-015 LOG] handleUnitDestroyed calling renderGame', 'color: orange;'); // ADDED BUG LOG
        // Replace full render with specific UI updates
        const destroyedUnit = findCardInstanceByUid(eventData.UnitUid); // Find the unit again to get ownerId
        const playerState = destroyedUnit ? getPlayerState(destroyedUnit.ownerId) : null; // Get player state for graveyard count
        removeUnitFromField(eventData.UnitUid); // Remove the unit's element
        if (playerState) {
            updateGraveyardCountDisplay(destroyedUnit.ownerId, playerState.graveyard.length); // Update graveyard count
        } else {
            console.warn(`[GM_DESTROY] Could not find player state for owner ${destroyedUnit?.ownerId} to update graveyard count.`);
        }
        console.log(`%c[GM_DESTROY] Called specific UI updates for unit ${eventData.UnitUid} destruction.`, 'color: red;'); // Log specific update
    } else {
        console.warn(`GM: UnitDestroyed event received for invalid unit UID: ${eventData.UnitUid}`); // PascalCase
    }
    console.log('%c[BUG-015 LOG] handleUnitDestroyed END', 'color: orange; font-weight: bold;'); // ADDED BUG LOG
}

export function handleAttackDeclared(eventData) {
    // DTO: Assume AttackDeclaredEventDto { string AttackerUid, string TargetUid }
    console.log(`%cGM HANDLE: handleAttackDeclared`, 'color: #dc3545; font-weight: bold;', eventData); // Red color for attack declaration
    if (!eventData || !eventData.AttackerUid || !eventData.TargetUid) { // PascalCase
        console.error("GM Error: Invalid data in handleAttackDeclared:", eventData);
        return;
    }
    const attacker = findCardInstanceByUid(eventData.AttackerUid); // PascalCase
    const target = findCardInstanceByUid(eventData.TargetUid); // PascalCase

    if (!attacker || !target) {
        console.warn(`GM: AttackDeclared event received for invalid attacker/target UID: Attacker=${eventData.AttackerUid}, Target=${eventData.TargetUid}`); // PascalCase
        return;
    }

    console.log(`GM: Attack declared - ${attacker.name} (UID: ${attacker.uid}) is attacking ${target.name} (UID: ${target.uid}). Waiting for AttackOccurred event for results.`);

    // Future: Trigger attack animation start here?
    // Example: showAttackArrow(attacker.uid, target.uid);

    // No state change or re-render needed here usually, wait for AttackOccurred.
    // renderGame(gameState); // Avoid re-rendering unless specific UI effect requires it
}
export function handleDamageDealt(eventData) {
        console.debug(`%c[DMG_HANDLER] START`, 'color: tomato; font-weight: bold;', eventData); // DEBUG LOG START
        // DTO: Assume DamageDealtEventDto { string? SourceUid, string TargetUid, int DamageAmount }
        // console.log(`%cGM HANDLE: handleDamageDealt`, 'color: #ff6347; font-weight: bold;', eventData); // Tomato color for damage
        // Use PascalCase: TargetUid, DamageAmount
        if (!eventData || !eventData.TargetUid || eventData.DamageAmount === undefined) { // PascalCase
            console.error("GM Error: Invalid data in handleDamageDealt:", eventData);
            console.debug(`%c[DMG_HANDLER] END (Invalid Data)`, 'color: tomato; font-weight: bold;'); // DEBUG LOG END
            return;
        }
    
        // Use PascalCase: TargetUid
        const targetUid = eventData.TargetUid; // PascalCase
        console.debug(`%c[DMG_HANDLER] Finding target with UID: ${targetUid}`, 'color: tomato;'); // DEBUG LOG
        const target = findCardInstanceByUid(targetUid); // PascalCase
        console.debug(`%c[DMG_HANDLER] Found target instance:`, 'color: tomato;', target ? JSON.parse(JSON.stringify(target)) : 'null'); // DEBUG LOG (log copy)
    
        if (!target) {
            // Use PascalCase: TargetUid
            console.warn(`GM: DamageDealt event received for invalid target UID: ${targetUid}`); // PascalCase
            console.debug(`%c[DMG_HANDLER] END (Invalid Target)`, 'color: tomato; font-weight: bold;'); // DEBUG LOG END
            return;
        } // Closing brace for if (!target)
    
        console.debug(`%c[DMG_HANDLER] Target ${target?.name} (UID: ${targetUid}) state BEFORE update: HP=${target?.currentHealth}`, 'color: tomato;'); // DEBUG LOG
    
        // Log source if available
        // Use PascalCase: SourceUid
        if (eventData.SourceUid) { // PascalCase
            // Use PascalCase: SourceUid
            const source = findCardInstanceByUid(eventData.SourceUid); // PascalCase
            // Use PascalCase: DamageAmount, SourceUid
            console.debug(`%c[DMG_HANDLER] Target received ${eventData.DamageAmount} damage from Source UID: ${eventData.SourceUid} (${source?.name ?? 'Unknown Source'}).`, 'color: tomato;'); // PascalCase (x2) // DEBUG LOG
        } else {
            // Use PascalCase: DamageAmount
            console.debug(`%c[DMG_HANDLER] Target received ${eventData.DamageAmount} damage from Unknown Source.`, 'color: tomato;'); // PascalCase // DEBUG LOG
        }
    
    
        // Update target health locally (Server should send UnitDestroyed separately if HP <= 0)
        // We might receive a StatChange event too, but updating here provides immediate feedback.
        // Let's be cautious and only update if health > 0, let UnitDestroyed handle removal.
        const oldHealth = target.currentHealth; // Store old health for logging
        if (oldHealth > 0) {
            // Avoid going below zero visually before destruction event arrives
            // Use PascalCase: DamageAmount
            target.currentHealth = Math.max(0, oldHealth - eventData.DamageAmount); // PascalCase
            console.debug(`%c[DMG_HANDLER] Updated target ${target.name} health from ${oldHealth} to ${target.currentHealth}.`, 'color: tomato;'); // DEBUG LOG
        } else {
            console.debug(`%c[DMG_HANDLER] Target ${target.name} health was already ${oldHealth} or less. Awaiting potential destruction event.`, 'color: tomato;'); // DEBUG LOG
        }
    
        console.debug(`%c[DMG_HANDLER] Target ${target?.name} (UID: ${targetUid}) state AFTER update: HP=${target?.currentHealth}`, 'color: tomato;'); // DEBUG LOG
    
        // Show damage number visual effect
        // Use PascalCase: DamageAmount
        if (eventData.DamageAmount > 0) { // PascalCase
            console.debug('%c[DMG_HANDLER] Calling showDamageNumber...', 'color: tomato;'); // DEBUG LOG
            // Use PascalCase: DamageAmount
            showDamageNumber(target.uid, eventData.DamageAmount); // PascalCase
        }
    
        console.debug('%c[DMG_HANDLER] Calling renderGame...', 'color: tomato;'); // DEBUG LOG
        // Determine target element ID
        let targetElementId = '';
        if (target.location === 'leader_zone') {
            targetElementId = `${target.ownerId}-leader`;
        } else if (target.location === 'field') {
            targetElementId = `unit-${target.uid}`;
        } else {
            console.warn(`[DMG_HANDLER] Cannot determine targetElementId for unit in location: ${target.location}`);
        }

        // Update the health display specifically
        if (targetElementId) {
            updateHealthDisplay(targetElementId, target.currentHealth, target.maxHealth);
            console.debug(`%c[DMG_HANDLER] Called specific health update for ${targetElementId}.`, 'color: tomato;');
        }
        // renderGame(gameState); // <<<< REMOVED THIS LINE
        console.debug(`%c[DMG_HANDLER] END`, 'color: tomato; font-weight: bold;'); // DEBUG LOG END
    }
export function handleHealthChanged(eventData) {
    console.log('%c[BUG-015 LOG] handleHealthChanged START', 'color: orange; font-weight: bold;', eventData); // ADDED BUG LOG
    // DTO: Assume HealthChangedEventDto { string UnitUid, int NewHealth }
console.debug(`%c[HP_HANDLER] START`, 'color: mediumseagreen; font-weight: bold;', eventData); // DEBUG LOG START
    // console.log(`%cGM HANDLE: handleHealthChanged`, 'color: #20c997; font-weight: bold;', eventData); // Teal color
    // Use PascalCase: TargetUid, NewHealth
    if (!eventData || !eventData.TargetUid || eventData.NewHealth === undefined) { // PascalCase - Changed UnitUid to TargetUid
        console.error("GM Error: Invalid data in handleHealthChanged:", eventData);
        return;
    }
    // Use PascalCase: TargetUid
    const unit = findCardInstanceByUid(eventData.TargetUid); // PascalCase - Changed UnitUid to TargetUid // Can affect followers or leaders
const unitUid = eventData.TargetUid; // PascalCase - Changed UnitUid to TargetUid // Can affect followers or leaders
console.debug(`%c[HP_HANDLER] Finding unit with UID: ${unitUid}`, 'color: mediumseagreen;'); // DEBUG LOG
    if (unit) {
console.debug(`%c[HP_HANDLER] Found unit instance:`, 'color: mediumseagreen;', unit ? JSON.parse(JSON.stringify(unit)) : 'null'); // DEBUG LOG (log copy)
        console.log(`%c[BUG-015 LOG] handleHealthChanged Unit ${unit?.name} state before update: HP=${unit?.currentHealth}`, 'color: orange;'); // ADDED BUG LOG
        const oldHealth = unit.currentHealth;
console.debug(`%c[HP_HANDLER] Unit ${unit?.name} (UID: ${unitUid}) state BEFORE update: HP=${unit?.currentHealth}`, 'color: mediumseagreen;'); // DEBUG LOG
        // Use PascalCase: NewHealth
        unit.currentHealth = eventData.NewHealth; // PascalCase

        console.log(`GM: Unit ${unit.name} (UID: ${unit.uid}) health changed from ${oldHealth} to ${unit.currentHealth}.`);
        console.log(`%c[BUG-015 LOG] handleHealthChanged Unit ${unit?.name} state AFTER update: HP=${unit?.currentHealth}`, 'color: orange;'); // ADDED BUG LOG
console.debug(`%c[HP_HANDLER] Unit ${unit.name} (UID: ${unit.uid}) health changed from ${oldHealth} to ${unit.currentHealth}.`, 'color: mediumseagreen;'); // DEBUG LOG

console.debug(`%c[HP_HANDLER] Unit ${unit?.name} (UID: ${unitUid}) state AFTER update: HP=${unit?.currentHealth}`, 'color: mediumseagreen;'); // DEBUG LOG
        // Optional: Show healing/damage number visual effect (needs more context)
        // if (unit.currentHealth > oldHealth) { showHealingNumber(unit.uid, unit.currentHealth - oldHealth); }

        // Log if health drops to 0 or below, but let server events handle actual destruction
        if (unit.currentHealth <= 0) {
            console.log(`GM: Unit ${unit.name} health reached ${unit.currentHealth}. Waiting for server destruction event.`);
        }
console.debug(`%c[HP_HANDLER] Unit ${unit.name} health reached ${unit.currentHealth}. Waiting for server destruction event.`, 'color: mediumseagreen;'); // DEBUG LOG
        console.log('%c[BUG-015 LOG] handleHealthChanged calling specific UI update', 'color: orange;'); // ADDED BUG LOG & MODIFIED

        // Determine target element ID
        let targetElementId = '';
        if (unit.location === 'leader_zone') { // Check if it's the leader
            targetElementId = `${unit.ownerId}-leader`;
        } else if (unit.location === 'field') { // Check if it's on the field
            targetElementId = `unit-${unit.uid}`;
        } else {
             console.warn(`[HP_HANDLER] Cannot determine targetElementId for unit in location: ${unit.location}`);
        }

        if (targetElementId) {
             // Update UI specifically
             console.debug(`%c[HP_HANDLER] Calling updateHealthDisplay for ${targetElementId}...`, 'color: mediumseagreen;'); // DEBUG LOG
             updateHealthDisplay(targetElementId, unit.currentHealth, unit.maxHealth); // Use specific UI update
        }
        // Note: showDamageNumber is NOT called here as ChangeAmount is unavailable in this event.
        // renderGame(gameState); // REPLACED

console.debug('%c[HP_HANDLER] Finished specific UI update.', 'color: mediumseagreen;'); // DEBUG LOG MODIFIED
    } else {
        // Use PascalCase: TargetUid
        console.warn(`GM: HealthChanged event received for invalid unit UID: ${eventData.TargetUid}`); // PascalCase - Changed UnitUid to TargetUid
        console.log('%c[BUG-015 LOG] handleHealthChanged END (Invalid Unit)', 'color: orange; font-weight: bold;'); // ADDED BUG LOG
    }
console.debug(`%c[HP_HANDLER] END (Invalid Unit)`, 'color: mediumseagreen; font-weight: bold;'); // DEBUG LOG END
    console.log('%c[BUG-015 LOG] handleHealthChanged END', 'color: orange; font-weight: bold;'); // ADDED BUG LOG
    console.debug(`%c[HP_HANDLER] END`, 'color: mediumseagreen; font-weight: bold;'); // DEBUG LOG END // MOVED INSIDE
}

export function handleMulliganStarted(eventData) {
    // DTO: MulliganStartedEventDto { MulliganTimer?: int } // Assuming PascalCase DTO
    console.log(`%cGM HANDLE: handleMulliganStarted`, 'color: #20c997; font-weight: bold;', eventData);
    gameState.gamePhase = "Mulligan";
    // UI should now allow mulligan actions. Timer handling (if any) would use eventData.MulliganTimer
    // Use PascalCase: MulliganTimer
    console.log(`GM: Mulligan phase started. Timer (if provided): ${eventData?.MulliganTimer}`); // PascalCase
    // Reset player mulligan completion flags here? Server DTO should be source of truth,
    // but resetting ensures clean state if reconnect happens during mulligan.
    gameState.player1MulliganComplete = false;
    gameState.player2MulliganComplete = false;
    // Get local player's hand for the UI function
    const localPlayerState = getPlayerState(gameState.localPlayerId);
    const handArray = localPlayerState ? localPlayerState.hand : [];
    if (!localPlayerState) {
        console.error("GM Error: handleMulliganStarted could not find local player state for ID:", gameState.localPlayerId);
        return; // Exit if local player state is missing
    }
    showMulliganInterface(gameState.localPlayerId, handArray); // Use specific UI update
}

export function handlePlayerMulliganCompleted(eventData) {
    // DTO: PlayerMulliganCompletedEventDto { SourcePlayerId: string } // Assuming PascalCase DTO
    console.log(`%cGM HANDLE: handlePlayerMulliganCompleted`, 'color: #20c997; font-weight: bold;', eventData);
    // Use PascalCase: SourcePlayerId
    if (!eventData || !eventData.SourcePlayerId) { // PascalCase
        console.error("GM Error: Invalid data in handlePlayerMulliganCompleted:", eventData);
        return;
    }
    // Use PascalCase: SourcePlayerId
    const completedPlayerId = eventData.SourcePlayerId; // PascalCase
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
    updateMulliganStatus(completedPlayerId, true); // Use specific UI update
}
// --- NEW Refactored Event Handlers ---

export function handleCardPlayed(eventData) {
    // DTO: CardPlayedEventDto { SourcePlayerId: string, PlayedCardInstance: CardInstanceDto, TargetUid?: string } // Corrected DTO structure based on error log
    console.log(`%cGM HANDLE: handleCardPlayed`, 'color: #6610f2; font-weight: bold;', eventData);

    try {
        // Use PascalCase: SourcePlayerId, PlayedCardInstance, PlayedCardInstance.InstanceId
        // Validate the corrected DTO structure based on the error log
        if (!eventData || !eventData.SourcePlayerId || !eventData.PlayedCardInstance || !eventData.PlayedCardInstance.InstanceId) {
            console.error("GM Error: Invalid data in handleCardPlayed (Expected SourcePlayerId and PlayedCardInstance with InstanceId):", eventData);
            return;
        }

        // Find the player who played the card using SourcePlayerId
        const playingPlayer = getPlayerState(eventData.SourcePlayerId); // Use SourcePlayerId
        if (!playingPlayer) {
            console.error(`[handleCardPlayed] Player not found: ${eventData.SourcePlayerId}`); // Use SourcePlayerId
            return;
        }

        // --- BUG-022 Fix Start ---
        // Extract the UID from the nested instance object
        const playedCardUid = eventData.PlayedCardInstance.InstanceId; // Get UID from instance

        // Find the index of the played card in the player's hand using the extracted UID
        const cardIndex = playingPlayer.hand.findIndex(card => card && card.uid === playedCardUid);

        if (cardIndex !== -1) {
            // Remove the card from the hand state
            const playedCard = playingPlayer.hand.splice(cardIndex, 1)[0]; // Remove and get the card
            console.log(`%c[BUG-022 Fix] Removed card ${playedCard?.name} (UID: ${playedCardUid}) from ${playingPlayer.id}'s hand state via handleCardPlayed.`, 'color: blue; font-weight: bold;');

            // Update the hand UI
            console.log(`%c[BUG-022 Fix] Calling updateHandDisplay for player ${playingPlayer.id} via handleCardPlayed.`, 'color: green; font-weight: bold;');
            updateHandDisplay(playingPlayer.id, playingPlayer.hand);
        } else {
            // This might happen if the card was already removed by another event
            // or if the event data is inconsistent.
            console.warn(`[handleCardPlayed] Card with UID ${playedCardUid} not found in ${playingPlayer.id}'s hand state. Hand UI might be already updated or state is inconsistent.`);
            // Still update the display just in case the UI is out of sync with the state we have
            updateHandDisplay(playingPlayer.id, playingPlayer.hand);
        }
        // --- BUG-022 Fix End ---

        // TODO: Implement logic to visually show the card being played (animation?)
        // Note: PP update should be handled by PpUpdateEvent.
        // Note: Graveyard update should be handled by GraveyardUpdateEvent.

    } catch (error) {
        console.error("Error handling CardPlayed event:", error, eventData);
    }
}

export function handleCardSummoned(eventData) {
    // DTO: CardSummonedEventDto { SourcePlayerId, SummonedCard (CardInstanceDto), Source, TimestampUtc }
    console.log(`%cGM HANDLE: handleCardSummoned`, 'color: #007bff; font-weight: bold;', eventData); // Corrected function name in log
    // Use PascalCase: SourcePlayerId, SummonedCard
    if (!eventData || !eventData.SourcePlayerId || !eventData.SummonedCard) { // Corrected property check
        console.error("GM Error: Invalid data in handleCardSummoned:", eventData); // Corrected function name in error
        return;
    }
    // Use PascalCase: SourcePlayerId
    const ownerPlayer = getPlayerState(eventData.SourcePlayerId); // Corrected property access
    if (!ownerPlayer) {
        // Use PascalCase: SourcePlayerId
        console.warn(`GM: CardSummoned event received for invalid player: ${eventData.SourcePlayerId}`); // Corrected property access
        return;
    }

    let handChanged = false;
    // Remove the card from hand using SourceCardUid if provided (optional) - Assuming SourceCardUid might still be relevant if summoned from hand effect
    // Use PascalCase: SourceCardUid
    if (eventData.SourceCardUid) { // PascalCase
        // Use PascalCase: SourceCardUid
        const handIndex = ownerPlayer.hand.findIndex(c => c && c.uid === eventData.SourceCardUid); // PascalCase
        if (handIndex > -1) {
            const removedCard = ownerPlayer.hand.splice(handIndex, 1)[0];
            // Use PascalCase: SourceCardUid
            console.log(`GM: Removed source card ${removedCard?.name} (UID: ${eventData.SourceCardUid}) from player ${ownerPlayer.id}'s hand.`); // PascalCase
            handChanged = true;
        } else {
            // Use PascalCase: SourceCardUid
            console.warn(`GM: CardSummoned event SourceCardUid ${eventData.SourceCardUid} not found in player ${ownerPlayer.id}'s hand.`); // PascalCase
        }
    }

    // HYDRATION: Create full instance from the SummonedCard DTO
    // Use PascalCase: SummonedCard
    const entityInstance = createCardInstance(eventData.SummonedCard); // Corrected property access
    if (!entityInstance) {
        // Use PascalCase: SummonedCard
        console.error(`GM: Failed to create instance for CardSummoned DTO for player ${ownerPlayer.id}:`, eventData.SummonedCard); // Corrected property access
        return;
    }

    entityInstance.ownerId = ownerPlayer.id; // Ensure owner is set
    entityInstance.location = 'field'; // Ensure location

    // Hydrate dynamic state from the SummonedCard DTO (which is a CardInstanceDto)
    if (entityInstance.type === 'Follower' || entityInstance.type === 'Follower_Evolved') {
        // Use PascalCase: SummonedCard.CanAttack, SummonedCard.CurrentAttack, SummonedCard.CurrentHealth, SummonedCard.IsEvolved
        entityInstance.canAttack = eventData.SummonedCard.CanAttack ?? false; // Corrected property access
        entityInstance.currentAttack = eventData.SummonedCard.CurrentAttack ?? entityInstance.attack; // Corrected property access
        entityInstance.currentHealth = eventData.SummonedCard.CurrentHealth ?? entityInstance.health; // Corrected property access
        entityInstance.isEvolved = eventData.SummonedCard.IsEvolved ?? false; // Corrected property access
    } else if (entityInstance.type === 'Amulet') {
        // Add amulet-specific hydration if needed (e.g., countdown)
        // Use PascalCase: SummonedCard.Countdown
        // entityInstance.countdown = eventData.SummonedCard.Countdown ?? entityInstance.countdown; // Corrected property access
    }

    // Use PascalCase: SummonedCard
    console.log(`GM_CARDSUMMONED_DEBUG: Hydrated Entity Instance for UID ${entityInstance.uid}: Type=${entityInstance.type}, Atk=${entityInstance.currentAttack}, HP=${entityInstance.currentHealth}. Raw SummonedCard DTO:`, eventData.SummonedCard); // Corrected log and property access

    let fieldChanged = false;
    // Avoid adding duplicates
    if (!ownerPlayer.field.some(f => f && f.uid === entityInstance.uid)) {
        if (ownerPlayer.field.length < MAX_FIELD_SIZE) {
            ownerPlayer.field = [...ownerPlayer.field, entityInstance]; // Create new array
            console.log(`GM: Added entity ${entityInstance.name} (UID: ${entityInstance.uid}, Type: ${entityInstance.type}) to player ${ownerPlayer.id}'s field.`);
            fieldChanged = true;
        } else {
            console.warn(`GM: EntityEnteredPlay event received for ${entityInstance.name}, but player ${ownerPlayer.id}'s field is full (${ownerPlayer.field.length}/${MAX_FIELD_SIZE}). Entity not added client-side.`);
        }
    } else {
        console.warn(`GM: EntityEnteredPlay event received for entity UID ${entityInstance.uid} which is already present on player ${ownerPlayer.id}'s field.`);
    }

    // Re-render if hand or field changed
    if (fieldChanged) { // Only update field if field changed
        addUnitToField(ownerPlayer.id, entityInstance); // Add the specific unit to the field UI
    }
    // Hand update is now handled by handleCardPlayed
}

// --- END NEW Refactored Event Handlers ---

export function handleGameOver(eventData) {
    // DTO: GameOverEventDto { WinnerId: string, Reason?: string } // Assuming PascalCase DTO
    console.log(`%cGM HANDLE: handleGameOver`, 'color: #dc3545; font-weight: bold;', eventData);
    // Use PascalCase: WinnerId
    if (!eventData || !eventData.WinnerId) { // PascalCase
        console.error("GM Error: Invalid data in handleGameOver:", eventData);
        return;
    }
    gameState.gameOver = true;
    gameState.gamePhase = "GameOver";
    // Use PascalCase: WinnerId
    gameState.winner = eventData.WinnerId; // PascalCase
    // Update UI to show game over screen with winner/loser info
    // Use PascalCase: Reason
    console.log(`GM: Game over! Winner: ${gameState.winner}. Reason: ${eventData.Reason || 'N/A'}`); // PascalCase
    // Use PascalCase: Reason
    alert(`游戏结束！ 玩家 ${gameState.winner} 获胜！ (${eventData.Reason || "原因未知"})`); // PascalCase
    renderGame(gameState); // Render final game over state
    // Consider disabling all further interactions more forcefully
    // disablePlayerInteractions(); // REMOVED: Relying on existing logic per user preference
}

export function handlePlayerActionError(eventData) {
    // DTO: PlayerActionErrorEventDto { PlayerId: string, ActionType?: string, Message: string } // Assuming PascalCase DTO
    console.log(`%cGM HANDLE: handlePlayerActionError`, 'color: #ffc107; font-weight: bold;', eventData);
    // Use PascalCase: PlayerId, Message
    if (!eventData || !eventData.PlayerId || !eventData.Message) { // PascalCase
        console.error("GM Error: Invalid data in handlePlayerActionError:", eventData);
        return;
    }
    // Use PascalCase: PlayerId, ActionType, Message
    console.warn(`GM: Player action error received for ${eventData.PlayerId} (Action: ${eventData.ActionType || 'Unknown'}): ${eventData.Message}`); // PascalCase (x3)
    // Display the error message *only* to the specific player who caused the error
    // Use PascalCase: PlayerId
    if (eventData.PlayerId === gameState.localPlayerId) { // PascalCase
        // Use PascalCase: ActionType, Message
        alert(`操作错误 (${eventData.ActionType || '未知'}): ${eventData.Message}`); // PascalCase (x2)
        // Optionally re-enable controls or clear selections if the action failed
        // e.g., if play card failed, maybe clear selectedCardUid
        // This depends on server guarantees and desired UX
    }
    // No re-render usually needed unless error implies state needs reverting/clearing selections
}
// --- END NEW Specific Event Handlers --- (Placeholder comment for structure)
