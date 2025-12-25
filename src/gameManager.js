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
import { createCardInstance } from "./cardManager.js";
import { renderGame, renderGameLog, showDamageNumber, updatePpDisplay, updateEpDisplay, updateSepDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateHealthDisplay, clearSelectionHighlights, resetMulliganSelection, updateHandDisplay, addUnitToField, updateUnitDisplay, removeUnitFromField, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus, updateSingleCardHighlight } from "./ui.js"; // Import UI functions + specific updates + clearSelectionHighlights + updateHandDisplay + addUnitToField + updateUnitDisplay + removeUnitFromField + highlightAttacker + highlightTargets + showMulliganInterface + updateMulliganStatus + updateSingleCardHighlight

// --- Constants ---
const MAX_FIELD_SIZE = 5; // Maximum cards on a player's field
const GAME_LOG_LIMIT = 200; // Prevent unbounded growth in UI log

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
    gameLog: [], // Local rolling log for UI display
    isSpectator: false, // Spectator mode disables actions
};

function resolvePlayModeSelection(playModes, preferredMode) {
    if (!Array.isArray(playModes) || playModes.length === 0) {
        return { mode: preferredMode ?? null, cost: null };
    }

    let selected = null;
    if (preferredMode) {
        selected = playModes.find(mode => mode.mode === preferredMode && mode.isAvailable) || null;
    }

    if (!selected) {
        const enhanceModes = playModes
            .filter(mode => mode.isAvailable && mode.mode === "Enhance")
            .sort((a, b) => b.effectiveCost - a.effectiveCost);
        selected = enhanceModes[0] || playModes.find(mode => mode.isAvailable) || null;
    }

    return {
        mode: selected ? selected.mode : null,
        cost: selected ? selected.effectiveCost : null
    };
}

function extractAnnouncement(payload) {
    if (payload === undefined || payload === null) return null;
    if (typeof payload === 'string') return payload;
    if (typeof payload !== 'object') return null;

    return payload.Announcement || payload.announcement || null;
}
// 初始化游戏 (已实现版本)
/**
 * Receives the SignalR connection object from main.js.
 * @param {signalR.HubConnection} connection - The active SignalR connection.
 */
export function setSignalRConnection(connection) {
    // ADDED export
    // This function now only sets the connection object for use by action-sending functions (e.g., playCard, endTurnRequest).
    // All event listeners are now registered in main.js's registerSignalREventHandlers.
    console.log("GAME MANAGER: Setting SignalR connection object.");
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
    gameState.isSpectator = false;

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
    gameState.gameLog = []; // Reset log on new session

} // Closing brace for initializeGame

export function initializeSpectator(gameId) {
    console.log(`GAME MANAGER: Initializing SPECTATOR mode for GameID=${gameId}. Waiting for server state...`);
    gameState.gameId = gameId;
    gameState.localPlayerId = null;
    gameState.isSpectator = true;

    gameState.players = [];
    gameState.activePlayer = null;
    gameState.opponentPlayer = null;
    gameState.turnNumber = 0;
    gameState.phase = "Connecting";
    gameState.gameOver = false;
    gameState.winner = null;
    gameState.selectedCardUid = null;
    gameState.selectedAttackerUid = null;
    gameState.gameLog = [];
}

// --- Game Log Helpers ---
export function appendGameLog(eventName, payload) {
    const ts = new Date().toLocaleTimeString('en-US', { hour12: false });
    const announcement = extractAnnouncement(payload);
    if (!announcement) return;
    const entry = `[${ts}] ${announcement}`;

    gameState.gameLog.push(entry);
    if (gameState.gameLog.length > GAME_LOG_LIMIT) {
        gameState.gameLog.shift();
    }

    // Keep console output consistent and structured
    console.log(
        `%cGAME EVENT%c ${eventName}`,
        'background:#1c1c1c;color:#0ff;padding:2px 4px;border-radius:3px',
        'color:#e5e5e5;font-weight:bold;',
        payload
    );

    renderGameLog(gameState.gameLog);
}
// --- Mulligan Phase Logic ---

// 新增：根据 UID 查找卡牌实例 (手牌或战场)
export function findCardInstanceByUid(uid) {
    if (!uid) return null; // 如果没有提供 uid，直接返回 null

    for (const player of gameState.players) {
        // 检查手牌
        // 【修复】使用正确的UID属性：优先使用uid，备用InstanceId
        const cardInHand = player.hand?.find((card) => card && (card.uid === uid || card.InstanceId === uid));
        if (cardInHand) {
            return cardInHand;
        }
        // 检查战场
        const cardOnField = player.field?.find((card) => card && (card.uid === uid || card.InstanceId === uid));
        if (cardOnField) {
            return cardOnField;
        }
        // 检查主战者
        // Original leader check (now acts as fallback for GUIDs or if player ID didn't match)
        // Ensure player.leaderCard exists before checking its uid or InstanceId
        if (player.leaderCard && (player.leaderCard.uid === uid || player.leaderCard.InstanceId === uid)) {
            return player.leaderCard;
        }
    }

    console.warn(
        `GAME MANAGER: Card instance with UID ${uid} not found in any player's Hand, Field, or Leader zone.`
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

    for (const validTarget of validTargets) {
        switch (validTarget) {
            case "EnemyFollower":
                if (
                    isEnemy &&
                    (targetType === "Follower" || targetType === "Follower_Evolved")
                ) {
                    return true;
                }
                break;
            case "EnemyLeader":
                if (isEnemy && targetType === "Leader") {
                    return true;
                }
                break;
            case "AllyFollower":
                if (
                    isAlly &&
                    (targetType === "Follower" || targetType === "Follower_Evolved")
                ) {
                    return true;
                }
                break;
            case "AllyLeader":
                if (isAlly && targetType === "Leader") {
                    return true;
                }
                break;
            case "AnyFollower": // Example of potentially targeting any follower
                if (targetType === "Follower" || targetType === "Follower_Evolved") {
                    return true;
                }
                break;
            case "AnyLeader": // Example of potentially targeting any leader
                if (targetType === "Leader") {
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
    return false; // If no criteria matched
}
export async function playCard(cardUid, targetUid = null) {
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
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

    if (cardInstance.zone !== "Hand") {
        console.warn(
            `GM: PlayCard failed - Card ${cardUid} (${cardInstance.name}) is not in hand (Location: ${cardInstance.zone}).`
        );
        // No error display needed, UI should prevent this.
        return;
    }



    const playerState = getPlayerState(cardInstance?.ownerId); // Use optional chaining for safety
    if (!playerState) {
        console.error(
            `GM: PlayCard failed - Player state not found for owner ${cardInstance.ownerId}.`
        );
        alert("内部错误：无法找到玩家状态。");
        return;
    }

    const playabilityInfo = gameState.playabilityCache[cardUid];
    const selection = resolvePlayModeSelection(playabilityInfo?.playModes, playabilityInfo?.selectedPlayMode);
    const selectedPlayMode = selection.mode;
    const effectiveCost = selection.cost ?? cardInstance.cost;

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
    if (playerState.pp < effectiveCost) {
        console.warn(
            `GM: PlayCard rejected - Not enough PP for ${cardInstance.name} (Cost: ${effectiveCost}, Have: ${playerState.pp}).`
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
    // The 'requires_target' property should now exist directly on the cardInstance
    if (
        cardInstance.type === "Spell" &&
        cardInstance.requires_target && // Check the instance directly
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
            CardUid: cardUid,
            TargetUids: targetUid ? [targetUid] : []
        };
        if (selectedPlayMode) {
            actionDto.PlayMode = selectedPlayMode;
            actionDto.PaidCost = effectiveCost;
        }
        // Invoke with the single DTO object
        // BUG-016: Store UID before invoking, so PpUpdate can remove it from hand
        gameState.lastPlayedCardInstanceId = cardUid;
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

    if (attackerInstance.zone !== "Field") {
        console.warn(
            `GM: InitiateAttack failed - Card ${attackerUid} (${attackerInstance.name}) is not on the field (Location: ${attackerInstance.zone}).`
        );
        return;
    }

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
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
    const attackerUid = gameState.selectedAttackerUid;

    // Validation 1: Check if an attacker is selected
    if (!attackerUid) {
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
    // Allow targeting 'Field' or 'Leader'
    if (
        targetInstance.zone !== "Field" &&
        targetInstance.zone !== "Leader"
    ) {
        console.warn(
            `GM: SelectTarget failed - Target ${targetUid} is not in a valid zone (Zone: ${targetInstance.zone}).`
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
    else if (targetInstance.type === 'Leader') {
        // If target is the leader and there's no Ward, check Rush/Storm rules
        const hasStorm = attackerInstance.keywords?.includes("Storm");
        const hasRush = attackerInstance.keywords?.includes("Rush");
        const isSummoningSick = attackerInstance.fieldTurn === 0 && !attackerInstance.isEvolved;

        // Can attack leader if: has Storm OR is not summoning sick.
        // Rush alone is not enough on the first turn.
        if (!hasStorm && isSummoningSick) {
             console.warn(
                `GM: SelectTarget failed - Attacker ${attackerInstance.name} has Rush or no keyword and cannot attack the leader on the turn it enters play.`
            );
            alert("该随从本回合无法攻击敌方主战者！");
            return;
        }
    }

    // All validations passed (including Ward and Rush/Storm checks)
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

        // 【重复伤害修复】移除客户端优化性状态更新，完全依赖服务器端状态同步
        // 所有状态更新（包括canAttack、生命值等）都应该通过服务器事件来处理
        // 这样可以避免客户端和服务器端状态不一致导致的重复计算问题
        console.log(
            "GM: 'PerformAttack' invocation sent successfully. Waiting for server response..."
        );

        // 【重复伤害修复】简化客户端反馈，避免状态更新冲突
        console.log(`${attackerInstance.name} 正在攻击 ${targetInstance.name}，等待服务器响应...`);
        gameState.selectedAttackerUid = null; // 仅清除选择状态
        // 【UI修复】强制进行一次完整的UI重绘，以确保在攻击后清除所有悬挂的UI状态（如高亮）
        renderGame(gameState);
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
        (card) => card && card.InstanceId === cardInstance.InstanceId
    );
    if (fieldIndex > -1) {
        const destroyedCard = ownerPlayer.field.splice(fieldIndex, 1)[0]; // Get the removed card
        if (destroyedCard) {
            destroyedCard.zone = "Graveyard";
            // Reset stats? Or keep them as they were upon destruction? Keeping them for now.
            ownerPlayer.graveyard = [...ownerPlayer.graveyard, destroyedCard]; // Create new array
            console.log(
                `GM: ${destroyedCard.Name} (UID: ${destroyedCard.InstanceId}) removed from field and added to graveyard.`
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
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
    if (gameState.gameOver) {
        console.warn("GM: Cannot evolve card - Game is over.");
        alert("游戏已经结束，无法进化！");
        return;
    }
    console.log(`GM: Attempting to evolve card UID: ${cardUid}`);

    const playerState = getPlayerState(gameState.currentPlayerId);
    const cardInstance = findCardInstanceByUid(cardUid);
    // cardData is no longer needed, we check properties on the instance itself.

    // --- Basic Client-Side Validations ---
    if (!playerState || !cardInstance) {
        console.error(
            `GM: EvolveCard failed - Missing player state or card instance for UID ${cardUid}.`
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
    if (cardInstance.zone !== "Field") {
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
    // Check the canEvolve property directly on the instance, which is hydrated from the DTO.
    if (cardInstance.canEvolve !== true) {
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

// 超进化随从
export async function superEvolveCard(cardUid) {
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
    if (gameState.gameOver) {
        console.warn("GM: Cannot super evolve card - Game is over.");
        alert("游戏已经结束，无法超进化！");
        return;
    }
    console.log(`GM: Attempting to super evolve card UID: ${cardUid}`);

    const playerState = getPlayerState(gameState.currentPlayerId);
    const cardInstance = findCardInstanceByUid(cardUid);

    if (!playerState || !cardInstance) {
        console.error(`GM: SuperEvolveCard failed - Missing player state or card instance for UID ${cardUid}.`);
        alert("内部错误：无法找到超进化所需信息。");
        return;
    }

    if (
        playerState.id !== gameState.localPlayerId ||
        playerState.id !== gameState.currentPlayerId
    ) {
        console.warn(`GM: SuperEvolveCard rejected - Not local player's turn.`);
        alert("现在不是你的回合！");
        return;
    }
    if (gameState.phase !== "Main") {
        console.warn(`GM: SuperEvolveCard rejected - Cannot super evolve outside Main phase.`);
        alert("只能在你的回合主阶段超进化！");
        return;
    }
    if (!playerState.canSuperEvolve) {
        console.warn("GM: SuperEvolveCard rejected - Local state indicates super evolution already used or unavailable.");
        alert("本回合已使用过超进化点或无法超进化。");
        return;
    }
    if (playerState.sep < 1) {
        console.warn("GM: SuperEvolveCard rejected - Local state shows insufficient SEP.");
        alert("超进化点不足！");
        return;
    }
    if (cardInstance.zone !== "Field") {
        console.warn(`GM: SuperEvolveCard rejected - Card not on field.`);
        alert("只能超进化场上的随从！");
        return;
    }
    if (cardInstance.type !== "Follower") {
        console.warn(`GM: SuperEvolveCard rejected - Cannot super evolve non-Follower.`);
        alert("只能超进化随从！");
        return;
    }
    if (cardInstance.isEvolved === true) {
        console.warn(`GM: SuperEvolveCard rejected - Card is already evolved.`);
        alert("该随从已经进化！");
        return;
    }
    if (cardInstance.canEvolve !== true) {
        console.warn("GM: SuperEvolveCard rejected - Card definition does not allow evolution or lacks target.");
        alert("该随从无法超进化！");
        return;
    }

    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error("GM: SuperEvolveCard failed - SignalR connection not available.");
        alert("与服务器连接丢失，无法超进化！");
        return;
    }

    console.log(`GM: Invoking SignalR Hub method 'PerformSuperEvolve'. GameID: ${gameState.gameId}, CardUID: ${cardUid}`);
    try {
        const actionDto = { UnitUid: cardUid };
        await signalRConnection.invoke("PerformSuperEvolve", actionDto);
        console.log(`GM: SuperEvolve request sent for UID: ${cardUid}`);
    } catch (error) {
        console.error("GM: Error invoking PerformSuperEvolve:", error);
        alert("超进化请求失败，请稍后再试。");
    }
}

// Act (Activated ability) request for a field card
export async function requestAct(cardUid) {
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
    if (gameState.gameOver) {
        console.warn("GM: Cannot use Act - Game is over.");
        return;
    }
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error("GM: RequestAct failed - SignalR connection not available or not connected.");
        alert("与服务器连接丢失，无法启动！");
        return;
    }

    const playerState = getPlayerState(gameState.currentPlayerId);
    const cardInstance = findCardInstanceByUid(cardUid);

    if (!playerState || !cardInstance) {
        console.error(`GM: RequestAct failed - Missing player/card state for UID ${cardUid}.`);
        return;
    }

    if (playerState.id !== gameState.localPlayerId || playerState.id !== gameState.currentPlayerId) {
        alert("现在不是你的回合！");
        return;
    }

    if (gameState.phase !== "Main") {
        alert("只能在主阶段启动！");
        return;
    }

    if (cardInstance.ownerId !== playerState.id || cardInstance.zone !== "Field") {
        alert("只能启动自己场上的卡牌！");
        return;
    }

    if (cardInstance.hasActAbility !== true) {
        alert("该卡牌没有启动能力。");
        return;
    }

    const lastUsed = cardInstance.actLastUsedTurnNumber ?? null;
    if (lastUsed !== null && lastUsed === (playerState.turnNumber ?? 0)) {
        alert("该卡牌本回合已启动过。");
        return;
    }

    try {
        const actionDto = { CardUid: cardUid };
        await signalRConnection.invoke("RequestAct", actionDto);
    } catch (err) {
        console.error("GM: SignalR 'RequestAct' invocation failed:", err);
        alert(`启动失败：${err?.message || "服务器通信错误"}`);
    }
}
// Duplicate evolveCard function removed to fix SyntaxError
// --- SignalR Hub Invocation Wrappers ---

/**
 * Submits the player's mulligan decision to the server.
 * @param {string[]} cardUidsToReplace - An array of card UIDs the player wishes to replace.
 */
export async function submitMulligan(cardUidsToReplace = []) {
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
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
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
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

        // Send the string directly, as expected by the Hub method string parameter
        await signalRConnection.invoke("EndTurn", requestId); // Send the string directly
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
 * 【新增】处理服务器主动推送的手牌可玩性更新
 * @param {Object} updateDto - HandPlayabilityUpdateDto from server
 */
export function handleHandPlayabilityUpdate(updateDto) {
    if (!updateDto || !updateDto.PlayerId || !updateDto.PlayabilityResults) {
        console.warn("GM: Received invalid hand playability update:", updateDto);
        return;
    }

    // 【临时调试日志】打印收到的原始DTO和当前敌方单位状态
    console.log('%c[DEBUG_PLAYABILITY] Received HandPlayabilityUpdate DTO:', 'color: red; font-weight: bold;', JSON.parse(JSON.stringify(updateDto)));
    const opponentPlayer = gameState.players.find(p => p.id !== gameState.localPlayerId);
    if (opponentPlayer) {
        console.log('%c[DEBUG_PLAYABILITY] Current opponent field state:', 'color: red; font-weight: bold;', JSON.parse(JSON.stringify(opponentPlayer.field)));
        if(opponentPlayer.leaderCard) {
             console.log('%c[DEBUG_PLAYABILITY] Current opponent leader state:', 'color: red; font-weight: bold;', JSON.parse(JSON.stringify(opponentPlayer.leaderCard)));
        }
    }
    // --- 结束临时调试日志 ---

    // 只处理本地玩家的可玩性更新
    if (updateDto.PlayerId !== gameState.localPlayerId) {
        console.log(`GM: Ignoring hand playability update for non-local player: ${updateDto.PlayerId}`);
        return;
    }

    console.log(`🎯 [HAND_PLAYABILITY_UPDATE] Received update for Player ${updateDto.PlayerId} with ${Object.keys(updateDto.PlayabilityResults).length} cards`);

    // 【修复】更新缓存：将服务器端的 InstanceId (Guid) 映射到客户端的 uid (string)
    Object.entries(updateDto.PlayabilityResults).forEach(([instanceId, playabilityResult]) => {
        // 查找对应的卡牌实例 - 使用 uid 属性进行匹配
        const localPlayer = getPlayerState(gameState.localPlayerId);
        if (localPlayer && localPlayer.hand) {
            // 【修复】使用正确的UID属性：优先使用uid，备用InstanceId（与findCardInstanceByUid保持一致）
            const cardInstance = localPlayer.hand.find(card => card && (card.uid === instanceId || card.InstanceId === instanceId));
            if (cardInstance) {
                // 使用统一的UID作为缓存键
                const cardUid = cardInstance.uid ?? cardInstance.InstanceId;
                const playModes = (playabilityResult.PlayModes || []).map(mode => ({
                    mode: mode.Mode,
                    effectiveCost: mode.EffectiveCost,
                    isAvailable: mode.IsAvailable
                }));
                const selection = resolvePlayModeSelection(playModes, playabilityResult.SelectedPlayMode);

                gameState.playabilityCache[cardUid] = {
                    cardUid: cardUid,
                    isPlayable: playabilityResult.IsPlayable,
                    requiresTargetSelection: playabilityResult.RequiresTargetSelection,
                    hasValidTargets: playabilityResult.HasValidTargets,
                    canPlayWithoutTarget: playabilityResult.CanPlayWithoutTarget,
                    failureReason: playabilityResult.FailureReason,
                    validTargetIds: playabilityResult.ValidTargetIds || [],
                    playModes: playModes,
                    selectedPlayMode: selection.mode,
                    selectedPlayCost: selection.cost
                };
            } else {
                console.warn(`GM: Could not find card instance with UID ${instanceId} in local player's hand`);
            }
        }
    });

    // 可玩性高亮只在主流程阶段需要；Mulligan 阶段频繁重渲染会打断本地交互（如调度选牌）。
    if (gameState.phase === 'Mulligan' || gameState.phase === 'GameOver') {
        return;
    }

    // 触发UI更新以反映新的可玩性状态
    try {
        import('./ui.js').then(uiModule => {
            if (uiModule.renderGame) {
                uiModule.renderGame(gameState);
            }
        });
    } catch (error) {
        console.warn("GM: Could not trigger UI update after playability update:", error);
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
    playerInstance.sep = playerDto.CurrentSEP ?? 0;
    playerInstance.maxSEP = playerDto.MaxSEP ?? 0;
    // FIXED: Backend sends CanEvolve, not CanEvolveThisTurn
    playerInstance.canEvolve = playerDto.CanEvolve ?? false; // PascalCase - Fixed property name
    playerInstance.canSuperEvolve = playerDto.CanSuperEvolve ?? false;
    playerInstance.hasUnlockedEvolution = playerDto.HasUnlockedEvolution ?? false; // PascalCase
    playerInstance.turnNumber = playerDto.TurnNumber ?? 0; // 【架构重构】使用turnNumber属性名保持一致性
    playerInstance.deckCount = playerDto.DeckCount ?? (Array.isArray(playerDto.Deck) ? playerDto.Deck.length : 0); // PascalCase (x2)
    // Keep deck as simple array of IDs/DTOs from server if no methods needed client-side
    playerInstance.deck = Array.isArray(playerDto.Deck) ? [...playerDto.Deck] : []; // PascalCase (x2)
    // Mulligan
    playerInstance.hasCompletedMulligan = playerDto.HasCompletedMulligan ?? false; // PascalCase


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
 * @param {'Hand'|'Field'|'Graveyard'} zoneName - The name of the zone.
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
            // ownerId and zone are set based on the zone being hydrated, not from DTO directly
            cardInstance.ownerId = ownerId;
            cardInstance.zone = zoneName;

            // Add specific hydration for field units' dynamic state
            if (zoneName === 'Field') {
                // Use PascalCase: CanAttack, CurrentAttack, CurrentHealth, IsEvolved, Keywords
                cardInstance.canAttack = cardDto.CanAttack ?? false; // PascalCase
                cardInstance.currentAttack = cardDto.CurrentAttack ?? cardInstance.attack; // PascalCase
                cardInstance.currentHealth = cardDto.CurrentHealth ?? cardInstance.health; // PascalCase
                cardInstance.isEvolved = cardDto.IsEvolved ?? false; // PascalCase
                cardInstance.fieldTurn = cardDto.FieldTurn ?? 0; // Hydrate FieldTurn
                // ADDED: Hydrate keywords (Ensure DTO has 'Keywords' property as an array)
                cardInstance.keywords = Array.isArray(cardDto.Keywords) ? [...cardDto.Keywords] : []; // PascalCase
                // console.log(`GM HYDRATION DBG: Hydrated keywords for ${cardInstance.name} (UID: ${cardInstance.uid}):`, cardInstance.keywords); // Optional Debug
            }
            // ADDED: Hydrate keywords for hand cards too (needed for UI checks like Ward indicator)
            else if (zoneName === 'Hand') {
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
    // IMPORTANT:
    // - In PVP, localPlayerId MUST come from the lobby handshake (initializeGame), and MUST NOT be inferred here.
    // - In VS-AI, localPlayerId may be inferred as the only non-AI player if the handshake is missing.
    if (Array.isArray(serverState.Players) && serverState.Players.length > 0) {
        const serverPlayerIds = serverState.Players.map(p => p?.PlayerId).filter(Boolean);
        const existingLocalPlayerId = gameState.localPlayerId;

        const localPlayerIdIsValid =
            !!existingLocalPlayerId && serverPlayerIds.includes(existingLocalPlayerId);

        if (!localPlayerIdIsValid) {
            if (gameState.isSpectator) {
                gameState.localPlayerId = serverPlayerIds[0] || null;
            }

            const nonAiPlayers = serverState.Players.filter(p => p?.PlayerId && p.IsAI === false);

            if (nonAiPlayers.length === 1) {
                gameState.localPlayerId = nonAiPlayers[0].PlayerId;
                console.warn(
                    `GM HYDRATION: localPlayerId was missing/invalid; inferred non-AI player ${gameState.localPlayerId} for VS-AI.`
                );
            } else {
                if (!gameState.isSpectator) {
                    console.error(
                        "GM HYDRATION: localPlayerId is missing/invalid and cannot be inferred safely for PVP.",
                        { existingLocalPlayerId, serverPlayerIds }
                    );
                }
            }
        }
    }
    // Use PascalCase: CurrentPlayerId
    gameState.currentPlayerId = serverState.CurrentPlayerId; // PascalCase
    // 【架构重构】不再从serverState获取全局TurnNumber，改为从当前玩家的PlayerState获取
    // Use PascalCase: Phase
    gameState.phase = serverState.Phase || "Unknown"; // PascalCase // Use the phase from the server state
    // Use PascalCase: GameOver
    gameState.gameOver = serverState.GameOver || false; // PascalCase
    // Use PascalCase: Winner
    gameState.winner = serverState.Winner || null; // PascalCase
    gameState.selectedCardUid = null; // Reset selections on full update
    gameState.selectedAttackerUid = null; // Reset selections on full update
    // Find player DTOs to get mulligan status
    // 【修复】移除过时的、基于硬编码ID的mulligan状态更新。
    // 新的玩家对象水合流程会自动处理每个玩家的mulligan状态。
    gameState.isEndingTurn = false; // Reset flag
    // Corrected log to show the actual phase being set
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
                        newLeaderInstance.zone = 'Leader';
                        // Hydrate dynamic state from DTO
                        // Use PascalCase: CurrentHealth
                        newLeaderInstance.currentHealth = serverLeaderDto.CurrentHealth ?? newLeaderInstance.health; // PascalCase
                        // Assign the new instance
                        playerInstance.leaderCard = newLeaderInstance;
                        newLeaderInstance.currentHealth = serverLeaderDto.CurrentHealth ?? newLeaderInstance.health; // PascalCase
                        // Assign the new instance
                        playerInstance.leaderCard = newLeaderInstance;
                    } else {
                        console.error(`GM HYDRATION: Failed to create leader card instance from DTO for player ${playerInstance.id}:`, serverLeaderDto);
                        playerInstance.leaderCard = null; // Ensure it's null if creation failed
                    }
                } else {
                    // Leader exists and UID matches, just update dynamic properties
                    // Use PascalCase: CurrentHealth
                    playerInstance.leaderCard.currentHealth = serverLeaderDto.CurrentHealth ?? playerInstance.leaderCard.health; // PascalCase
                    // Add other dynamic updates if needed (e.g., effects, keywords)
                }
            } else if (playerInstance.leaderCard) {
                // Server DTO doesn't have a leader, but client does? Log warning and remove client leader.
                console.warn(`GM HYDRATION: Server DTO for player ${playerInstance.id} missing LeaderCard, but client has one (UID: ${playerInstance.leaderCard.uid}). Removing client leader.`);
                playerInstance.leaderCard = null;
            }
            // --- End Leader Card Hydration ---

            // Hydrate other zones (using existing _hydrateCardZone which recreates zones)
            // Use PascalCase: Hand
            playerInstance.hand = _hydrateCardZone(playerDto.Hand, playerInstance.id, 'Hand'); // PascalCase
            // Use PascalCase: Field
            playerInstance.field = _hydrateCardZone(playerDto.Field, playerInstance.id, 'Field'); // PascalCase
            // Use PascalCase: Graveyard
            playerInstance.graveyard = _hydrateCardZone(playerDto.Graveyard, playerInstance.id, 'Graveyard'); // PascalCase
            // Deck is handled in _hydratePlayerProperties

            // Add the updated or new instance to the temporary array
            hydratedPlayers.push(playerInstance);
        }
    } else {
        // Use PascalCase: Players
        console.warn("GM HYDRATION: serverState.Players is not an array or is missing."); // PascalCase
    }
    // Filter out nulls before assigning
    gameState.players = hydratedPlayers.filter(p => p !== null);
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

    // 【架构重构】从当前玩家的PlayerState获取回合数
    gameState.turnNumber = gameState.activePlayer?.turnNumber ?? 0;

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

    // --- 【修复】改进缓存清理策略 ---
    // 只在回合真正开始且游戏状态发生重大变化时清理缓存
    // 避免频繁清理导致的缓存未命中问题
    if (eventData?.GameState?.CurrentPlayerId && eventData.GameState.CurrentPlayerId === gameState.localPlayerId) {
        // 检查是否是新的回合（回合数增加）或游戏状态发生重大变化
        const currentTurnNumber = eventData.GameState.Players?.find(p => p.Id === gameState.localPlayerId)?.TurnNumber || 0;
        const previousTurnNumber = gameState.turnNumber || 0;

        if (currentTurnNumber > previousTurnNumber) {
            console.log(`%c[CACHE_INVALIDATION] Clearing playabilityCache for new turn (Turn: ${currentTurnNumber}, Player: ${gameState.localPlayerId}).`, 'color: orange; font-weight: bold;');
            gameState.playabilityCache = {}; // Clear the cache object only for new turns
        } else {
            console.log(`%c[CACHE_PRESERVATION] Preserving playabilityCache for same turn (Turn: ${currentTurnNumber}, Player: ${gameState.localPlayerId}).`, 'color: green; font-weight: bold;');
        }
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
        if (cardToRemoveUid) {
            const cardIndex = playerToUpdate.hand.findIndex(c => c && c.uid === cardToRemoveUid);
            if (cardIndex > -1) {
                playerToUpdate.hand.splice(cardIndex, 1)[0];
                handChanged = true;
            }
            // Clear the temporary state regardless of whether the card was found in hand
            gameState.lastPlayedCardInstanceId = null;
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

export function handleSepUpdate(eventData) {
    console.log(`%cGM HANDLE: handleSepUpdate`, 'color: #e11d48; font-weight: bold;', eventData);
    if (!eventData || !eventData.SourcePlayerId || eventData.CurrentSep === undefined || eventData.MaxSep === undefined) {
        console.error("GM Error: Invalid data in handleSepUpdate (expected SourcePlayerId, CurrentSep, MaxSep):", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.sep = eventData.CurrentSep;
        playerToUpdate.maxSEP = eventData.MaxSep;
        playerToUpdate.canSuperEvolve = playerToUpdate.maxSEP > 0 && playerToUpdate.sep > 0;

        updateSepDisplay(playerToUpdate.id, playerToUpdate.sep, playerToUpdate.maxSEP);
        if (typeof renderGame === "function") {
            renderGame(gameState);
        }
    } else {
        console.warn(`GM: SepUpdate event received for invalid player: ${eventData.SourcePlayerId}`);
    }
}

// 新增：仅处理 PP 最大值更新事件
export function handlePpMaxUpdate(eventData) {
    console.log('%c[EVENT RECEIVED] handlePpMaxUpdate:', 'background: #6f42c1; color: #ffffff; font-weight: bold;', eventData);
    if (!eventData || !eventData.SourcePlayerId || eventData.MaxPp === undefined) {
        console.error("GM Error: Invalid data in handlePpMaxUpdate (expected SourcePlayerId, MaxPp):", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (!playerToUpdate) {
        console.warn(`GM: PpMaxUpdate event received for invalid player: ${eventData.SourcePlayerId}`);
        return;
    }

    // 更新最大值；如果事件同时携带当前值，则一并更新（由后端下修保障一致性）
    playerToUpdate.maxPP = eventData.MaxPp;
    if (eventData.CurrentPp !== undefined && eventData.CurrentPp !== null) {
        playerToUpdate.pp = eventData.CurrentPp;
    } else if (playerToUpdate.pp > playerToUpdate.maxPP) {
        // 兜底：若当前值超过最大值，进行下修
        playerToUpdate.pp = playerToUpdate.maxPP;
    }

    updatePpDisplay(playerToUpdate.id, playerToUpdate.pp, playerToUpdate.maxPP);
}

// 新增：仅处理 EP 最大值更新事件
export function handleEpMaxUpdate(eventData) {
    console.log('%c[EVENT RECEIVED] handleEpMaxUpdate:', 'background: #20c997; color: #000; font-weight: bold;', eventData);
    if (!eventData || !eventData.SourcePlayerId || eventData.MaxEp === undefined) {
        console.error("GM Error: Invalid data in handleEpMaxUpdate (expected SourcePlayerId, MaxEp):", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (!playerToUpdate) {
        console.warn(`GM: EpMaxUpdate event received for invalid player: ${eventData.SourcePlayerId}`);
        return;
    }

    playerToUpdate.maxEP = eventData.MaxEp;
    if (eventData.CurrentEp !== undefined && eventData.CurrentEp !== null) {
        playerToUpdate.ep = eventData.CurrentEp;
    } else if (playerToUpdate.ep > playerToUpdate.maxEP) {
        playerToUpdate.ep = playerToUpdate.maxEP;
    }

    // canEvolve 规则：有上限且当前>0
    playerToUpdate.canEvolve = playerToUpdate.maxEP > 0 && playerToUpdate.ep > 0;

    updateEpDisplay(playerToUpdate.id, playerToUpdate.ep, playerToUpdate.maxEP);
    if (typeof renderGame === "function") {
        renderGame(gameState);
    }
}

export function handleSepMaxUpdate(eventData) {
    console.log('%c[EVENT RECEIVED] handleSepMaxUpdate:', 'background: #fb7185; color: #000; font-weight: bold;', eventData);
    if (!eventData || !eventData.SourcePlayerId || eventData.MaxSep === undefined) {
        console.error("GM Error: Invalid data in handleSepMaxUpdate (expected SourcePlayerId, MaxSep):", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (!playerToUpdate) {
        console.warn(`GM: SepMaxUpdate event received for invalid player: ${eventData.SourcePlayerId}`);
        return;
    }

    playerToUpdate.maxSEP = eventData.MaxSep;
    if (eventData.CurrentSep !== undefined && eventData.CurrentSep !== null) {
        playerToUpdate.sep = eventData.CurrentSep;
    } else if (playerToUpdate.sep > playerToUpdate.maxSEP) {
        playerToUpdate.sep = playerToUpdate.maxSEP;
    }

    playerToUpdate.canSuperEvolve = playerToUpdate.maxSEP > 0 && playerToUpdate.sep > 0;

    updateSepDisplay(playerToUpdate.id, playerToUpdate.sep, playerToUpdate.maxSEP);
    if (typeof renderGame === "function") {
        renderGame(gameState);
    }
}


/**
 * Handles HandUpdated events from the server.
 * This event is sent when a player's hand is completely updated (e.g., after mulligan).
 * @param {Object} eventData - HandUpdatedEventDto from server
 */
export function handleHandUpdated(eventData) {
    console.log(`%cGM HANDLE: handleHandUpdated`, 'color: #28a745; font-weight: bold;', eventData);

    // Validate event data
    if (!eventData || !eventData.PlayerId || !eventData.UpdatedHand) {
        console.error("GM Error: Invalid data in handleHandUpdated:", eventData);
        return;
    }

    // Find the player to update
    const playerToUpdate = getPlayerState(eventData.PlayerId);
    if (!playerToUpdate) {
        console.warn(`GM: HandUpdated event received for invalid player: ${eventData.PlayerId}`);
        return;
    }

    // Update the player's hand with the new cards
    // 【修复】确保手牌中的卡牌实例经过createCardInstance处理，以便有正确的uid属性
    playerToUpdate.hand = eventData.UpdatedHand.map(cardDto => createCardInstance(cardDto));
    console.log(`GM: Updated player ${playerToUpdate.id}'s hand with ${eventData.UpdatedHand.length} cards`);
    console.log("GM: New hand cards:", playerToUpdate.hand.map(card => `${card.name} (${card.uid})`));

    // Update the hand display
    updateHandDisplay(playerToUpdate.id, playerToUpdate.hand);

    // Clear any mulligan selection state since hand was updated
    if (typeof resetMulliganSelection === 'function' && playerToUpdate.id === gameState.localPlayerId) {
        resetMulliganSelection();
    }
}

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
    // DTO: GraveyardUpdateEventDto { string PlayerId, int GraveyardCount, CardDto? CardAdded, string? CardRemovedUid }
    // Note: The DTO's base 'SourcePlayerId' is now correctly interpreted as the PlayerId whose graveyard is being updated.
    console.log(`%cGM HANDLE: handleGraveyardUpdate`, 'color: #6c757d; font-weight: bold;', eventData);

    if (!eventData || !eventData.SourcePlayerId || eventData.GraveyardCount === undefined) {
        console.error("GM Error: Invalid data in handleGraveyardUpdate (missing SourcePlayerId or GraveyardCount):", eventData);
        return;
    }

    const updatedPlayerId = eventData.SourcePlayerId;
    const playerToUpdate = getPlayerState(updatedPlayerId);

    if (!playerToUpdate) {
        console.warn(`GM: GraveyardUpdate event received for invalid player: ${updatedPlayerId}`);
        return;
    }

    // The server's count is the source of truth.
    const newServerCount = eventData.GraveyardCount;
    playerToUpdate.graveyardCount = newServerCount; // Update local state model if you use it.

    // Log the update for clarity.
    console.log(`GM: Updating graveyard count for player ${updatedPlayerId} to ${newServerCount}.`);

    // Directly call the UI update function with the correct player ID and the new count.
    // The UI function is responsible for determining if this is the 'local' or 'opponent' player.
    updateGraveyardCountDisplay(updatedPlayerId, newServerCount);
}

// Removed handleUnitPlayed function
// Removed handleAmuletPlayed function

// Removed handleSpellPlayed function

export function handleUnitEvolved(eventData) {
    // DTO: UnitEvolvedEventDto { string SourcePlayerId, CardInstanceDto EvolvedUnitState }
    console.log('%c[EVENT RECEIVED] handleUnitEvolved:', 'background: #e83e8c; color: #ffffff; font-weight: bold;', eventData);
    if (!eventData || !eventData.SourcePlayerId || !eventData.EvolvedUnitState) { // PascalCase - Fixed to match actual DTO
        console.error("GM Error: Invalid data in handleUnitEvolved:", eventData);
        return;
    }
    const unitUid = eventData.EvolvedUnitState.InstanceId; // Get UID from EvolvedUnitState
    const unit = findCardInstanceByUid(unitUid);

    let updated = false;

    if (unit && eventData.EvolvedUnitState) {
        const ownerPlayer = getPlayerState(unit.ownerId);
        if (ownerPlayer) {
            const unitIndex = ownerPlayer.field.findIndex(u => u && u.uid === unitUid);
            if (unitIndex !== -1) {
                // Create a new, fully hydrated instance from the DTO
                const evolvedUnitInstance = createCardInstance(eventData.EvolvedUnitState);
                evolvedUnitInstance.ownerId = ownerPlayer.id;
                evolvedUnitInstance.zone = 'Field';

                // Replace the old instance with the new one in the game state
                ownerPlayer.field[unitIndex] = evolvedUnitInstance;

                console.log(`GM: Unit ${evolvedUnitInstance.name} (UID: ${unitUid}) fully updated after evolution. New State:`, evolvedUnitInstance);
                updated = true;
            } else {
                 console.warn(`GM: Could not find unit with UID ${unitUid} on player ${ownerPlayer.id}'s field to apply evolution.`);
            }
        } else {
            console.warn(`GM: Could not find owner player for unit UID: ${unitUid}`);
        }
    } else {
        console.warn(`GM: UnitEvolved event received for invalid unit UID or missing DTO: ${unitUid}`);
    }

    if (updated) {
        const updatedUnit = findCardInstanceByUid(unitUid);
        if (updatedUnit) {
            // Re-render specifically the unit (EP display will be updated by separate EpUpdateEvent)
            updateUnitDisplay(updatedUnit);
            updateHealthDisplay(updatedUnit);
        }
    }
}

export function handleStatChange(eventData) {
    // DTO: StatChangeEventDto { TargetEntityUid, NewAttack, NewCurrentHealth, NewMaxHealth }
    console.log('%c[EVENT RECEIVED] handleStatChange:', 'background: #fd7e14; color: #ffffff; font-weight: bold;', eventData);
    const targetUid = eventData?.UnitId ?? eventData?.TargetEntityUid;
    const newAttack = eventData?.NewAttack;
    const newCurrentHealth = eventData?.NewHealth ?? eventData?.NewCurrentHealth;
    const newMaxHealth = eventData?.NewMaxHealth;

    if (!targetUid) {
        console.error("GM Error: Invalid data in handleStatChange (missing TargetEntityUid/UnitId):", eventData);
        return;
    }

    const unit = findCardInstanceByUid(targetUid); // PascalCase // Use TargetEntityUid from DTO
    if (unit) {
        // Update base and current stats for consistency unless server logic differs
        if (newAttack !== undefined && newAttack !== null) {
            unit.attack = newAttack; // PascalCase
            unit.currentAttack = newAttack; // PascalCase // Reflect change in current stats too
        }
        if (newCurrentHealth !== undefined && newCurrentHealth !== null) {
            unit.health = newCurrentHealth; // PascalCase
            unit.currentHealth = newCurrentHealth; // PascalCase // Reflect change in current stats too
        }
        if (newMaxHealth !== undefined && newMaxHealth !== null) {
            unit.maxHealth = newMaxHealth; // PascalCase
        }
        console.log(`GM: Unit ${unit.name} (UID: ${unit.uid}) stats changed to A:${unit.currentAttack}/H:${unit.currentHealth}`);
        updateUnitDisplay(unit); // Use specific UI update instead of full render
        updateHealthDisplay(unit);
    } else {
        console.warn(`GM: StatChange event received for invalid unit ID: ${targetUid}`); // PascalCase
    }
}

export function handleUnitDestroyed(eventData) {
    // DTO: UnitDestroyedEventDto { string UnitUid }
    console.log('%c[EVENT RECEIVED] handleUnitDestroyed:', 'background: #dc3545; color: #ffffff; font-weight: bold;', eventData);
    if (!eventData || !eventData.UnitUid) { // PascalCase
        console.error("GM Error: Invalid data in handleUnitDestroyed:", eventData);
        return;
    }
    const unit = findCardInstanceByUid(eventData.UnitUid); // PascalCase
    if (unit) {
        // 【BUG修复】核心问题修复：在处理破坏事件时，必须将单位从本地gameState的field数组中移除。
        // 之前的实现只移除了UI，但没有更新数据状态，导致后续重绘时“幽灵单位”重现。
        const ownerPlayer = getPlayerState(unit.ownerId);
        if (ownerPlayer && Array.isArray(ownerPlayer.field)) {
            const unitIndex = ownerPlayer.field.findIndex(u => u && u.uid === eventData.UnitUid);
            if (unitIndex > -1) {
                ownerPlayer.field.splice(unitIndex, 1);
                console.log(`GM: Unit ${eventData.UnitUid} removed from gameState.`);
            }
        }

        // 仅从UI上移除单位的DOM元素
        removeUnitFromField(eventData.UnitUid);
        console.log(`GM: Unit ${unit.name} (UID: ${eventData.UnitUid}) removed from field. Graveyard count is handled by GraveyardUpdate event.`);

    } else {
        console.warn(`GM: UnitDestroyed event received for invalid unit UID: ${eventData.UnitUid}`); // PascalCase
    }
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
    // DTO: Assume DamageDealtEventDto { string? SourceUid, string TargetUid, int DamageAmount }
    console.log('%c[EVENT RECEIVED] handleDamageDealt:', 'background: #ff6347; color: #ffffff; font-weight: bold;', eventData); // Tomato color
    // Use PascalCase: TargetUid, DamageAmount
    if (!eventData || !eventData.TargetUid || eventData.DamageAmount === undefined) { // PascalCase
        console.error("GM Error: Invalid data in handleDamageDealt:", eventData);
        return;
    }

    const targetUid = eventData.TargetUid; // PascalCase
    const target = findCardInstanceByUid(targetUid); // PascalCase

    if (!target) {
        console.warn(`GM: DamageDealt event received for invalid target UID: ${targetUid}`); // PascalCase
        return;
    }

    // 【职责分离】此事件处理器仅负责显示伤害数字的视觉效果。
    // 生命值的实际状态更新由 HealthChanged 事件处理。

    // Show damage number visual effect
    if (eventData.DamageAmount > 0) {
        // 确定伤害数字应该显示在哪个UI元素上
        // 【修复】直接传递目标单位的UID给UI函数
        // showDamageNumber 函数现在能够智能地根据UID找到正确的DOM元素
        showDamageNumber(target.uid, eventData.DamageAmount);
    }
}
export function handleHealthChanged(eventData) {
    console.log('%c[EVENT RECEIVED] handleHealthChanged:', 'background: #20c997; color: #ffffff; font-weight: bold;', eventData);
    // DTO: HealthChangedEventDto { string CardInstanceId, int NewHealth, int DamageTaken }
    if (!eventData || !eventData.CardInstanceId || eventData.NewHealth === undefined) {
        console.error("GM Error: Invalid data in handleHealthChanged:", eventData);
        return;
    }

    const cardUid = eventData.CardInstanceId;
    const unit = findCardInstanceByUid(cardUid);

    if (unit) {
        const oldHealth = unit.currentHealth;
        unit.currentHealth = eventData.NewHealth;

        console.log(`GM: Unit ${unit.name} (UID: ${unit.uid}) health changed from ${oldHealth} to ${unit.currentHealth}.`);

        // 【重构】调用统一的UI更新函数
        // 新的 updateHealthDisplay 函数能够智能处理主战者和场上单位
        updateHealthDisplay(unit);

        // Log if health drops to 0, but let the server's UnitDestroyed event handle the actual removal
        if (unit.currentHealth <= 0) {
            console.log(`GM: Unit ${unit.name} health reached ${unit.currentHealth}. Waiting for server destruction event.`);
        }
    } else {
        console.warn(`GM: HealthChanged event received for invalid unit UID: ${cardUid}`);
    }
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
    const completedPlayerId = eventData.SourcePlayerId;
    const player = getPlayerState(completedPlayerId);

    if (player) {
        player.hasCompletedMulligan = true;
        // 状态更新应由完整的GameState同步，这里只调用UI更新
        console.log(`GM: Player ${completedPlayerId} completed mulligan. Triggering UI update.`);
        updateMulliganStatus(completedPlayerId, true);
    } else {
        console.warn(`GM: PlayerMulliganCompleted event received for unknown player ID: ${completedPlayerId}`);
    }
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
        const cardIndex = playingPlayer.hand.findIndex(card => card && card.InstanceId === playedCardUid);

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
    entityInstance.zone = 'Field'; // Ensure zone

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

    appendSystemLog('提示：结算完成后，可返回主菜单右上角「元进度」面板刷新查看金币/任务。');
    const maybeUserId = gameState.localPlayerId;
    if (looksLikeGuid(maybeUserId)) {
        tryFetchMetaProgressionState(maybeUserId).catch(() => { });
    }
    renderGame(gameState); // Render final game over state
    // Consider disabling all further interactions more forcefully
    // disablePlayerInteractions(); // REMOVED: Relying on existing logic per user preference
}

function appendSystemLog(message) {
    if (!message) return;
    const ts = new Date().toLocaleTimeString('en-US', { hour12: false });
    gameState.gameLog.push(`[${ts}] ${message}`);
    if (gameState.gameLog.length > GAME_LOG_LIMIT) {
        gameState.gameLog.shift();
    }
    renderGameLog(gameState.gameLog);
}

function looksLikeGuid(value) {
    if (!value || typeof value !== 'string') return false;
    const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    return guidRegex.test(value);
}

async function tryFetchMetaProgressionState(userId) {
    const response = await fetch(`/api/economy/state/${userId}`, { method: 'GET' });
    if (!response.ok) return;
    const state = await response.json();

    const coins = state?.rank?.coins ?? 0;
    const quests = Array.isArray(state?.quests) ? state.quests : [];
    const claimable = quests.filter(q => q?.isCompleted && !q?.isRewardClaimed && !q?.isExpired).length;

    appendSystemLog(`结算落库：Coins=${coins}，可领取任务=${claimable}`);
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

export function handleAttackStateUpdated(eventData) {
    // DTO: AttackStateUpdatedEventDto { TargetCardInstanceId: string, CanAttack: bool }
    console.log('%c[EVENT RECEIVED] handleAttackStateUpdated:', 'background: #6f42c1; color: #ffffff; font-weight: bold;', eventData); // Violet color

    if (!eventData || !eventData.TargetCardInstanceId) {
        console.error("GM Error: Invalid data in handleAttackStateUpdated:", eventData);
        return;
    }

    const unit = findCardInstanceByUid(eventData.TargetCardInstanceId);

    if (unit) {
        const oldCanAttack = unit.canAttack;
        unit.canAttack = eventData.CanAttack;
        console.log(`GM: Unit ${unit.name} (UID: ${unit.uid}) canAttack state changed from ${oldCanAttack} to ${unit.canAttack}.`);

        // Trigger a UI update for the specific unit to reflect the change in its attack-readiness (e.g., remove glow)
        updateUnitDisplay(unit);
    } else {
        console.warn(`GM: AttackStateUpdated event received for invalid unit UID: ${eventData.TargetCardInstanceId}`);
    }
}

export function handleActStateUpdated(eventData) {
    // DTO: ActStateUpdatedEventDto { TargetCardInstanceId: string, ActLastUsedTurnNumber: number }
    console.log('%c[EVENT RECEIVED] handleActStateUpdated:', 'background: #0dcaf0; color: #000000; font-weight: bold;', eventData);

    if (!eventData || !eventData.TargetCardInstanceId) {
        console.error("GM Error: Invalid data in handleActStateUpdated:", eventData);
        return;
    }

    const unit = findCardInstanceByUid(eventData.TargetCardInstanceId);
    if (!unit) {
        console.warn(`GM: ActStateUpdated event received for invalid card UID: ${eventData.TargetCardInstanceId}`);
        return;
    }

    unit.actLastUsedTurnNumber = eventData.ActLastUsedTurnNumber ?? unit.actLastUsedTurnNumber ?? null;

    // Re-render to update Act buttons (similar rationale to EvolveStateUpdated).
    renderGame(gameState);
}

export function handleEvolveStateUpdated(eventData) {
    // DTO: PlayerEvolvedEventDto { PlayerId: string }
    console.log('%c[EVENT RECEIVED] handleEvolveStateUpdated:', 'background: #6f42c1; color: #ffffff; font-weight: bold;', eventData); // Violet color

    if (!eventData || !eventData.SourcePlayerId) {
        console.error("GM Error: Invalid data in handleEvolveStateUpdated:", eventData);
        return;
    }

    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.canEvolve = eventData.CanEvolve ?? false;
        console.log(`GM: Player ${playerToUpdate.id} evolve state updated: ${playerToUpdate.canEvolve}`);

        // A full re-render is necessary to update the state of all evolve buttons on the field.
        renderGame(gameState);
    } else {
        console.warn(`GM: EvolveStateUpdated event received for invalid player: ${eventData.SourcePlayerId}`);
    }
}

export function handleSuperEvolveStateUpdated(eventData) {
    console.log('%c[EVENT RECEIVED] handleSuperEvolveStateUpdated:', 'background: #fb7185; color: #111827; font-weight: bold;', eventData);

    if (!eventData || !eventData.SourcePlayerId) {
        console.error("GM Error: Invalid data in handleSuperEvolveStateUpdated:", eventData);
        return;
    }

    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.canSuperEvolve = eventData.CanSuperEvolve ?? false;
        renderGame(gameState);
    } else {
        console.warn(`GM: SuperEvolveStateUpdated event received for invalid player: ${eventData.SourcePlayerId}`);
    }
}
// --- END NEW Specific Event Handlers --- (Placeholder comment for structure)

/**
 * Handles the PlayerChoiceRequested event from the server.
 * This event pauses the game flow and asks the client to make a selection.
 * @param {object} eventData - The PlayerChoiceRequestDto from the server.
 *                           - SourceCardUid: The card initiating the choice.
 *                           - SelectableTargets: An array of UIDs that are valid choices.
 *                           - PromptMessage: A message to display to the user.
 */
export function handlePlayerChoiceRequested(eventData) {
    console.log('%c[EVENT RECEIVED] handlePlayerChoiceRequested:', 'background: #007bff; color: #ffffff; font-weight: bold;', eventData);

    // BUG-FIX: The actual choice data is nested inside the 'choiceRequest' property.
    // C# PascalCase becomes camelCase in JS, so we look for 'choiceRequest'.
    // BUG-FIX: C# DTOs use PascalCase. Access the correct property from the event data.
    const choiceRequest = eventData.ChoiceRequest;

    // Further validation using PascalCase properties
    if (!choiceRequest || !choiceRequest.SourceAbilityRuntimeId || !choiceRequest.SelectableTargetIds) {
        console.error("GM Error: Invalid data in handlePlayerChoiceRequested. Missing nested ChoiceRequest object or its properties (SourceAbilityRuntimeId, SelectableTargetIds).", eventData);
        return;
    }

    // The primary role of this handler is to pass the choice request to the UI module.
    try {
        import('./ui.js').then(uiModule => {
            if (uiModule.showPlayerChoiceUI) {
                // Pass the nested choiceRequest object to the UI function.
                uiModule.showPlayerChoiceUI(choiceRequest);
            } else {
                console.error("GM Error: ui.js module does not have showPlayerChoiceUI function.");
            }
        });
    } catch (error) {
        console.error("GM Error: Failed to load or call ui.js for PlayerChoiceRequested:", error);
    }
}

/**
 * Handles the CardRevealed event from the server.
 * This event is purely informational/visual and MUST NOT mutate hidden-zone sizes.
 */
export function handleCardRevealed(eventData) {
    console.log(`%cGM HANDLE: handleCardRevealed`, 'color: #f59e0b; font-weight: bold;', eventData);

    const revealedCard = eventData?.RevealedCard ?? null;
    if (!revealedCard) {
        console.warn("GM: CardRevealed event missing RevealedCard payload.");
        return;
    }

    // Show a transient overlay; do not modify authoritative state.
    try {
        import('./ui.js').then(uiModule => {
            if (uiModule.showRevealOverlay) {
                uiModule.showRevealOverlay(revealedCard);
            }
        });
    } catch (error) {
        console.error("GM Error: Failed to show reveal overlay:", error);
    }

    // Best-effort log entry.
    const name = revealedCard.Name ?? 'Unknown';
    const cost = Number.isFinite(revealedCard.Cost) ? revealedCard.Cost : '?';
    appendGameLog("Reveal", { name, cost, instanceId: revealedCard.InstanceId, cardId: revealedCard.CardId });
}

export function handleCardsFused(eventData) {
    // DTO: CardsFusedEventDto { SourcePlayerId, FusedCount, FusedCardInstanceIds }
    console.log(`%cGM HANDLE: handleCardsFused`, 'color: #a855f7; font-weight: bold;', eventData);

    const sourcePlayerId = eventData?.SourcePlayerId ?? null;
    const fusedIds = Array.isArray(eventData?.FusedCardInstanceIds) ? eventData.FusedCardInstanceIds : [];
    const fusedCount = Number.isFinite(eventData?.FusedCount) ? eventData.FusedCount : fusedIds.length;

    if (!sourcePlayerId || fusedCount <= 0) return;

    const who = sourcePlayerId === gameState.localPlayerId ? '你' : '对手';
    appendSystemLog(`${who}融合了${fusedCount}张卡`);

    try {
        import('./ui.js').then(uiModule => {
            if (uiModule.showFusionAnimation) {
                uiModule.showFusionAnimation(sourcePlayerId, fusedIds);
            }
        });
    } catch (error) {
        console.error("GM Error: Failed to show fusion animation:", error);
    }
}

/**
 * Submits the player's choice back to the server.
 * @param {string} choiceId - The UID of the selected target.
 * @param {string} sourceCardUid - The UID of the card that initiated the choice.
 */
export async function submitPlayerChoice(pendingChoiceId, chosenTargetIds, sourceAbilityRuntimeId, wasCancelled = false) {
    if (!signalRConnection || signalRConnection.state !== "Connected") {
        console.error("GM: SubmitPlayerChoice failed - SignalR connection not available.");
        alert("与服务器连接丢失，无法提交选择！");
        return false;
    }

    // Ensure chosenTargetIds is always an array
    const targetIds = Array.isArray(chosenTargetIds) ? chosenTargetIds : (chosenTargetIds ? [chosenTargetIds] : []);

    const choiceDto = {
        PendingChoiceId: pendingChoiceId,
        SourceAbilityRuntimeId: sourceAbilityRuntimeId,
        ChosenTargetIds: targetIds,
        WasCancelled: wasCancelled // Add the new property
    };

    // --- DIAGNOSTIC LOGGING ---
    console.log("[CHOICE_DEBUG] GameManager: Invoking 'SubmitTargetChoice' on Hub. DTO:", choiceDto);
    try {
        await signalRConnection.invoke("SubmitTargetChoice", choiceDto);
        console.log("[CHOICE_DEBUG] GameManager: 'SubmitTargetChoice' invocation successful.");
        return true;
    } catch (err) {
        console.error("[CHOICE_DEBUG] GameManager: 'SubmitTargetChoice' invocation FAILED.", err);
        alert(`提交选择失败：${err.message}`);
        return false;
    }
    // --- END DIAGNOSTIC LOGGING ---
}

/**
 * Sends a cancellation for the current player choice request to the server.
 * @param {string} sourceAbilityRuntimeId - The runtime ID of the ability that initiated the choice.
 */
export async function cancelPlayerChoice(pendingChoiceId, sourceAbilityRuntimeId) {
    console.log(`[CHOICE_DEBUG] GameManager: Cancelling player choice for ability ${sourceAbilityRuntimeId} with PendingChoiceId ${pendingChoiceId}.`);
    // To cancel, we submit a choice with the correct PendingChoiceId, an empty target array, and the WasCancelled flag set to true.
    return await submitPlayerChoice(pendingChoiceId, [], sourceAbilityRuntimeId, true);
}
