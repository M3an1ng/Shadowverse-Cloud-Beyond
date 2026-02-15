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
import { renderGame, renderGameLog, showDamageNumber, showHealNumber, updatePpDisplay, updateEpDisplay, updateSepDisplay, updatePpBoostDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateBloodPoolDisplay, updateComboDisplay, updateHealthDisplay, clearSelectionHighlights, resetMulliganSelection, updateHandDisplay, updateUnitDisplay, updateFieldDisplay, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus, refreshBattleCardPreviewIfCurrent, renderHighlights, updateActButtonState, updateCardActionButtons, updateEmblemZone, showTurnStartBanner, updateTurnTimerDisplay, refreshCardInstanceUi, showPlayerChoiceUI, showRevealOverlay, showFusionAnimation, showAbilityTriggerToast, replaceInMatchChatHistory, appendInMatchChatMessage, clearInMatchChatUiState } from "./ui.js"; // Import UI functions + specific updates + clearSelectionHighlights + updateHandDisplay + updateUnitDisplay + highlightAttacker + highlightTargets + showMulliganInterface + updateMulliganStatus
import { getUserSession } from "./userSession.js";

// --- Constants ---
const MAX_FIELD_SIZE = 5; // Maximum cards on a player's field
const GAME_LOG_LIMIT = 200; // Prevent unbounded growth in UI log
const CHAT_HISTORY_LIMIT = 200;

// --- SignalR Connection ---
let signalRConnection = null; // Holds the connection object passed from main.js
let signalRGuestConnection = null;
const pendingChatClientMessageIds = new Set();

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
    actPlayabilityCache: {}, // Cache for Act playability results { cardUid: ActPlayabilityResultDto }
    gameLog: [], // Local rolling log for UI display
    chatMessages: [], // Room chat history shown in chat panel
    isSpectator: false, // Spectator mode disables actions
    turnTimeoutSeconds: 0,
    turnStartedAtUtc: null,
    turnEndsAtUtc: null,
    dualControl: false,
    dualControlSeatIds: {
        hostPlayerId: null,
        guestPlayerId: null
    }
};

let turnTimerIntervalId = null;
let turnTimerEndsAtMs = null;
let turnTimerTotalMs = 0;
let lastHandledChoiceRequestKey = null;

function stopTurnTimer() {
    if (turnTimerIntervalId) {
        clearInterval(turnTimerIntervalId);
        turnTimerIntervalId = null;
    }
    turnTimerEndsAtMs = null;
}

function startTurnTimer(endsAtMs, totalMs) {
    stopTurnTimer();
    turnTimerEndsAtMs = endsAtMs;
    turnTimerTotalMs = totalMs;

    turnTimerIntervalId = setInterval(() => {
        const remainingMs = Math.max(0, turnTimerEndsAtMs - Date.now());
        updateTurnTimerDisplay(remainingMs, turnTimerTotalMs, true);
        if (remainingMs <= 0) {
            stopTurnTimer();
        }
    }, 100);

    const initialRemainingMs = Math.max(0, turnTimerEndsAtMs - Date.now());
    updateTurnTimerDisplay(initialRemainingMs, turnTimerTotalMs, true);
}

function refreshTurnTimerFromState() {
    const isLocalTurn = gameState.localPlayerId && gameState.currentPlayerId === gameState.localPlayerId;
    const isActivePhase = gameState.phase === "TurnStart" || gameState.phase === "Main";
    const isActive = !gameState.isSpectator && isLocalTurn && isActivePhase;

    const fallbackSeconds = 180;
    const timeoutSeconds = Number.isFinite(gameState.turnTimeoutSeconds) && gameState.turnTimeoutSeconds > 0
        ? gameState.turnTimeoutSeconds
        : fallbackSeconds;
    const totalMs = timeoutSeconds * 1000;

    if (gameState.gameOver) {
        stopTurnTimer();
        updateTurnTimerDisplay(0, totalMs, false);
        return;
    }

    let endsAtMs = null;
    if (gameState.turnEndsAtUtc) {
        const parsed = Date.parse(gameState.turnEndsAtUtc);
        if (!Number.isNaN(parsed)) {
            endsAtMs = parsed;
        }
    }

    if (!endsAtMs && gameState.turnStartedAtUtc) {
        const parsedStart = Date.parse(gameState.turnStartedAtUtc);
        if (!Number.isNaN(parsedStart)) {
            endsAtMs = parsedStart + totalMs;
        }
    }

    if (!isActive || !endsAtMs || totalMs <= 0) {
        stopTurnTimer();
        updateTurnTimerDisplay(0, totalMs, false);
        return;
    }

    startTurnTimer(endsAtMs, totalMs);
}

function normalizePlayModeValue(value) {
    if (value === null || value === undefined) {
        return null;
    }
    if (typeof value === 'string') {
        const normalized = value.trim().toLowerCase();
        if (normalized === 'base') return 0;
        if (normalized === 'enhance') return 1;
        return normalized;
    }
    if (typeof value === 'number') {
        return value;
    }
    return value;
}

function isEnhanceMode(value) {
    const normalized = normalizePlayModeValue(value);
    return normalized === 1 || normalized === 'enhance';
}

function resolvePlayModeSelection(playModes, preferredMode) {
    if (!Array.isArray(playModes) || playModes.length === 0) {
        return { mode: preferredMode ?? null, cost: null };
    }

    let selected = null;
    if (preferredMode !== null && preferredMode !== undefined) {
        const normalizedPreferred = normalizePlayModeValue(preferredMode);
        selected = playModes.find(mode =>
            normalizePlayModeValue(mode.mode) === normalizedPreferred && mode.isAvailable
        ) || null;
    }

    if (!selected) {
        const enhanceModes = playModes
            .filter(mode => mode.isAvailable && isEnhanceMode(mode.mode))
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
    signalRConnection = connection || null;
}
export function setGuestSignalRConnection(connection) {
    signalRGuestConnection = connection || null;
}

function getConnectionForPlayer(playerId) {
    if (!gameState.dualControl) {
        return signalRConnection;
    }

    const guestId = gameState.dualControlSeatIds?.guestPlayerId;
    if (playerId && guestId && playerId === guestId) {
        return signalRGuestConnection || null;
    }

    return signalRConnection;
}

function getActiveConnection() {
    return getConnectionForPlayer(gameState.localPlayerId);
}

function selectDualControlMulliganSeat() {
    if (!gameState.dualControl || gameState.isSpectator || gameState.phase !== 'Mulligan') {
        return;
    }

    const players = Array.isArray(gameState.players) ? gameState.players : [];
    if (!players.length) return;

    const pending = players.filter(p => p && p.hasCompletedMulligan !== true);
    if (!pending.length) return;

    const currentId = gameState.localPlayerId;
    const currentStillPending = pending.some(p => p.id === currentId);
    if (currentStillPending) return;

    const hostId = gameState.dualControlSeatIds?.hostPlayerId;
    const nextSeat = pending.find(p => p.id === hostId) || pending[0];
    if (!nextSeat || !nextSeat.id) return;

    gameState.localPlayerId = nextSeat.id;
    resetMulliganSelection();
}

export function initializeGame(idOfGame, idOfPlayer, options = {}) {
    // ADDED export
    // Reset or initialize a minimal local state structure if needed.
    // Primarily, store the IDs for context.
    gameState.gameId = idOfGame; // Use properties added earlier
    gameState.localPlayerId = idOfPlayer; // Use properties added earlier
    gameState.isSpectator = false;
    gameState.dualControl = options.dualControl === true;
    gameState.dualControlSeatIds = {
        hostPlayerId: options.hostPlayerId || idOfPlayer || null,
        guestPlayerId: options.guestPlayerId || null
    };
    if (!gameState.dualControl) {
        signalRGuestConnection = null;
    }

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
    gameState.chatMessages = [];
    pendingChatClientMessageIds.clear();
    renderGameLog(gameState.gameLog);
    clearInMatchChatUiState();

} // Closing brace for initializeGame

export function initializeSpectator(gameId) {
    gameState.gameId = gameId;
    gameState.localPlayerId = null;
    gameState.isSpectator = true;
    gameState.dualControl = false;
    gameState.dualControlSeatIds = {
        hostPlayerId: null,
        guestPlayerId: null
    };
    signalRGuestConnection = null;

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
    gameState.chatMessages = [];
    pendingChatClientMessageIds.clear();
    renderGameLog(gameState.gameLog);
    clearInMatchChatUiState();
}

// --- Game Log Helpers ---
function formatTurnPrefix() {
    const turn = Number.isFinite(gameState.turnNumber) ? gameState.turnNumber : 0;
    return `[Turn ${turn}]`;
}

function formatLogEntry(message) {
    if (!message) return null;
    const text = String(message);
    const match = text.match(/^\[Turn\s+\d+\]/i);
    if (match) {
        const prefix = match[0];
        let rest = text.slice(prefix.length).trimStart();
        if (rest.startsWith('>')) {
            rest = rest.slice(1).trimStart();
        }
        return rest.length > 0 ? `${prefix} > ${rest}` : `${prefix} >`;
    }
    return `${formatTurnPrefix()} > ${text}`;
}

export function appendGameLog(eventName, payload) {
    const announcement = extractAnnouncement(payload);
    if (!announcement) return;
    const entry = formatLogEntry(announcement);
    if (!entry) return;

    gameState.gameLog.push(entry);
    if (gameState.gameLog.length > GAME_LOG_LIMIT) {
        gameState.gameLog.shift();
    }

    // Keep console output consistent and structured

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
        // 检查纹章区
        const cardInEmblem = player.emblem?.find((card) => card && (card.uid === uid || card.InstanceId === uid));
        if (cardInEmblem) {
            return cardInEmblem;
        }
        // 检查主战者
        // Original leader check (now acts as fallback for GUIDs or if player ID didn't match)
        // Ensure player.leaderCard exists before checking its uid or InstanceId
        if (player.leaderCard && (player.leaderCard.uid === uid || player.leaderCard.InstanceId === uid)) {
            return player.leaderCard;
        }
    }

    console.warn(
        `GAME MANAGER: Card instance with UID ${uid} not found in any player's Hand, Field, Emblem, or Leader zone.`
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

function normalizeZoneName(zoneValue) {
    if (zoneValue === null || zoneValue === undefined) {
        return null;
    }

    const normalized = String(zoneValue).trim().toLowerCase();
    if (!normalized) {
        return null;
    }

    switch (normalized) {
        case "hand":
            return "hand";
        case "field":
            return "field";
        case "deck":
            return "deck";
        case "graveyard":
            return "graveyard";
        case "emblem":
            return "emblem";
        case "playing":
            return "playing";
        case "staging":
            return "staging";
        case "extra":
            return "extra";
        case "removed":
            return "removed";
        case "none":
            return "none";
        default:
            return normalized;
    }
}

function toZoneLabel(zoneName) {
    if (!zoneName) return "None";
    return zoneName.charAt(0).toUpperCase() + zoneName.slice(1);
}

function extractCardUid(card) {
    if (!card || typeof card !== "object") return null;
    return card.uid ?? card.InstanceId ?? card.instanceId ?? null;
}

function findCardInZoneArray(zoneArray, cardUid) {
    if (!Array.isArray(zoneArray) || !cardUid) {
        return -1;
    }

    return zoneArray.findIndex(card => {
        const uid = extractCardUid(card);
        return uid && String(uid) === String(cardUid);
    });
}

function detachCardFromAllZones(cardUid) {
    if (!cardUid) {
        return { card: null, removed: [] };
    }

    const removed = [];
    let detachedCard = null;
    const zones = ["hand", "field", "graveyard", "emblem", "deck"];

    for (const player of gameState.players) {
        if (!player) continue;
        for (const zone of zones) {
            const zoneArray = player[zone];
            if (!Array.isArray(zoneArray)) continue;

            const index = findCardInZoneArray(zoneArray, cardUid);
            if (index < 0) continue;

            const [removedCard] = zoneArray.splice(index, 1);
            if (!detachedCard && removedCard) {
                detachedCard = removedCard;
            }
            removed.push({ playerId: player.id, zone });
        }
    }

    return { card: detachedCard, removed };
}

function ensureZoneArray(player, zoneName) {
    if (!player || !zoneName) {
        return null;
    }

    if (!Array.isArray(player[zoneName])) {
        player[zoneName] = [];
    }

    return player[zoneName];
}

function refreshPlayerZoneUi(player, zoneName) {
    if (!player || !zoneName) return;

    if (zoneName === "hand") {
        updateHandDisplay(player.id, player.hand);
        return;
    }

    if (zoneName === "field") {
        updateFieldDisplay(player.id, player.field);
        return;
    }

    if (zoneName === "emblem") {
        updateEmblemZone(player);
        return;
    }

    if (zoneName === "deck") {
        updateDeckCountDisplay(player.id, player.deckCount ?? player.deck?.length ?? 0);
        return;
    }

    if (zoneName === "graveyard") {
        const count = player.graveyardCount ?? player.graveyard?.length ?? 0;
        updateGraveyardCountDisplay(player.id, count);
    }
}

function adjustZoneCount(player, zoneName, delta) {
    if (!player || !zoneName || !Number.isFinite(delta) || delta === 0) {
        return;
    }

    if (zoneName === "deck") {
        const base = Number.isFinite(player.deckCount) ? player.deckCount : (player.deck?.length ?? 0);
        player.deckCount = Math.max(0, base + delta);
    } else if (zoneName === "graveyard") {
        const base = Number.isFinite(player.graveyardCount) ? player.graveyardCount : (player.graveyard?.length ?? 0);
        player.graveyardCount = Math.max(0, base + delta);
    }
}
// --- 其他函数占位符 (保持不变) ---

// --- Effect Resolution ---
// context can contain { targetUid: "some-uid" } for targeted effects
// Removed resolveEffects - Server now handles effect resolution.

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

    if (cardHasKeyword(cardInstance, "CannotUse")) {
        console.warn(
            `GM: PlayCard rejected - Card ${cardUid} (${cardInstance.name}) has CannotUse.`
        );
        alert("该卡牌处于【无法使用】状态。");
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
    if (playabilityInfo?.isPlayable === false) {
        const failureReason = playabilityInfo.failureReason || "该卡牌当前无法使用。";
        console.warn(
            `GM: PlayCard rejected - Card ${cardUid} (${cardInstance.name}) is marked unplayable. Reason: ${failureReason}`
        );
        alert(failureReason);
        return;
    }
    const selection = resolvePlayModeSelection(playabilityInfo?.playModes, playabilityInfo?.selectedPlayMode);
    const selectedPlayMode = selection.mode;
    const fallbackCostRaw =
        cardInstance.currentCost
        ?? cardInstance.CurrentCost
        ?? cardInstance.cost
        ?? cardInstance.Cost
        ?? 0;
    const fallbackCost = Number.isFinite(fallbackCostRaw)
        ? Math.max(0, fallbackCostRaw)
        : Math.max(0, Number(fallbackCostRaw) || 0);
    const effectiveCost = Number.isFinite(selection.cost) ? selection.cost : fallbackCost;
    const selectedAbilityRuntimeId = playabilityInfo?.selectedAbilityRuntimeId ?? null;

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

    // Legacy single-target spell guard. Final target legality is server-authoritative
    // via playability + PlayerChoice workflow.
    if (
        cardInstance.type === "Spell" &&
        cardInstance.requires_target && // Check the instance directly
        !targetUid
    ) {
        console.warn(
            `GM: PlayCard rejected - Spell ${cardInstance.name} requires a target, but none provided.`
        );
        alert("该法术需要指定一个目标！");
        return;
    }

    // --- SignalR Invocation ---
    const connection = getActiveConnection();
    // Ensure signalRConnection is accessible (should be via module scope variable set by setSignalRConnection)
    if (!connection || connection.state !== "Connected") {
        console.error(
            "GM: PlayCard failed - SignalR connection not available or not connected."
        );
        alert("与服务器连接丢失，无法出牌！");
        // Optionally try to reconnect or inform the user more drastically
        return;
    }

    try {
        // Invoke the server method. The server will validate everything again (cost, turn, phase, target, etc.)
        // and handle the actual game logic.
        // Construct the DTO object expected by the server Hub method (PascalCase)
        const actionDto = {
            CardUid: cardUid,
            TargetUids: targetUid ? [targetUid] : []
        };
        if (selectedPlayMode !== null && selectedPlayMode !== undefined) {
            actionDto.PlayMode = selectedPlayMode;
            actionDto.PaidCost = effectiveCost;
            if (isEnhanceMode(selectedPlayMode) && selectedAbilityRuntimeId) {
                actionDto.AbilityRuntimeId = selectedAbilityRuntimeId;
            }
        }
        // Invoke with the single DTO object
        // BUG-016: Store UID before invoking, so PpUpdate can remove it from hand
        gameState.lastPlayedCardInstanceId = cardUid;
        await connection.invoke("PlayCard", actionDto); // Pass the DTO object

        // If invoke succeeds, DO NOT modify local state directly.
        // Wait for the server to send back game events (e.g., CardPlayed, UnitSpawned, PPDeducted, EffectResolved, UnitDestroyed, TurnEnded...)
        // The processGameEvent function will handle these events and update the state.

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

    if (attackerInstance.type !== "Follower") {
        console.warn(
            `GM: InitiateAttack failed - Card ${attackerUid} (${attackerInstance.name}) is not a follower (Type: ${attackerInstance.type}).`
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
    //     gameState.selectedAttackerUid = null;
    // } else {
    gameState.selectedAttackerUid = attackerUid;
    // }

    // Trigger UI update to highlight attacker and potential targets
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
    if (attackerInstance.type !== "Follower") {
        console.warn(
            `GM: SelectTarget failed - Attacker ${attackerUid} (${attackerInstance.name}) is not a follower (Type: ${attackerInstance.type}).`
        );
        gameState.selectedAttackerUid = null;
        clearSelectionHighlights();
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

    const attackerCannotAttackLeader = cardHasKeyword(attackerInstance, "CannotAttackLeader");
    const attackerCannotAttackFollower = cardHasKeyword(attackerInstance, "CannotAttackFollower");
    const targetIsFollower =
        targetInstance.type === "Follower" ||
        targetInstance.type === "Follower_Evolved";
    const targetIsLeader = targetInstance.type === "Leader";

    // Validation 5: Check for Ward (守护)
    const opponentPlayer = getPlayerState(targetInstance.ownerId); // Get the owner of the target
    if (!opponentPlayer) {
        console.error(
            `GM: SelectTarget failed - Could not find opponent player state for ID: ${targetInstance.ownerId}`
        );
        return;
    }

    const hasEffectiveWard = card => {
        const hasWard = cardHasKeyword(card, "Ward");
        const hasAmbush = cardHasKeyword(card, "Ambush");
        const hasPressure = cardHasKeyword(card, "Pressure");
        return hasWard && !hasAmbush && !hasPressure;
    };

    // Check if opponent has any follower with Ward on the field
    const opponentHasWard = opponentPlayer.field.some(
        (card) =>
            card &&
            (card.type === "Follower" || card.type === "Follower_Evolved") &&
            hasEffectiveWard(card)
    );

    // If opponent has Ward, enforce targeting rules
    if (opponentHasWard) {
        const targetHasWard = hasEffectiveWard(targetInstance);

        // Valid targets when opponent has Ward: Only followers that also have Ward
        if (!targetIsFollower || !targetHasWard) {
            console.warn(
                `GM: SelectTarget failed - Cannot target ${targetInstance.name} (${targetInstance.type}) because opponent has Ward follower(s). Must target a Ward follower first.`
            );
            alert("必须先攻击带有【守护】的随从！");
            return; // Reject the target selection
        }
        // If target is a follower with Ward, the check passes.
    }
    // If opponent does NOT have Ward, any valid target (follower or leader, respecting other rules) is fine.
    else if (targetIsLeader) {
        // If target is the leader and there's no Ward, check Rush/Storm rules
        const hasStorm = cardHasKeyword(attackerInstance, "Storm");
        const attackerFieldTurn = attackerInstance.fieldTurn ?? attackerInstance.FieldTurn ?? 0;
        const isSummoningSick = attackerFieldTurn === 0;

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

    if (targetIsLeader && attackerCannotAttackLeader) {
        console.warn(
            `GM: SelectTarget failed - Attacker ${attackerInstance.name} cannot attack leaders due to CannotAttackLeader.`
        );
        alert("该随从无法攻击敌方主战者！");
        return;
    }

    if (targetIsFollower && attackerCannotAttackFollower) {
        console.warn(
            `GM: SelectTarget failed - Attacker ${attackerInstance.name} cannot attack followers due to CannotAttackFollower.`
        );
        alert("该随从无法攻击敌方随从！");
        return;
    }

    if (targetIsFollower && cardHasKeyword(targetInstance, "Ambush")) {
        console.warn(
            `GM: SelectTarget failed - Target ${targetInstance.name} cannot be targeted due to Ambush.`
        );
        alert("该随从处于【潜行】状态，无法被攻击！");
        return;
    }

    if (targetIsFollower && cardHasKeyword(targetInstance, "Pressure")) {
        console.warn(
            `GM: SelectTarget failed - Target ${targetInstance.name} cannot be targeted due to Pressure.`
        );
        alert("该随从处于【威慑】状态，无法被攻击！");
        return;
    }

    // All validations passed (including Ward and Rush/Storm checks)

    // Execute the attack
    // --- SignalR Invocation ---
    const connection = getActiveConnection();
    if (!connection || connection.state !== "Connected") {
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
    try {
        // Invoke server method with the single DTO object. Server handles validation and attack execution.
        await connection.invoke("PerformAttack", actionDto); // Pass the DTO object

        // 【重复伤害修复】移除客户端优化性状态更新，完全依赖服务器端状态同步
        // 所有状态更新（包括canAttack、生命值等）都应该通过服务器事件来处理
        // 这样可以避免客户端和服务器端状态不一致导致的重复计算问题

        // 【重复伤害修复】简化客户端反馈，避免状态更新冲突
        gameState.selectedAttackerUid = null; // 仅清除选择状态
        renderHighlights(gameState);
    } catch (err) {
        console.error("GM: SignalR 'PerformAttack' invocation failed:", err);
        let errorMessage = "服务器通信错误";
        if (err && err.message) {
            errorMessage = err.message;
        }
        alert(`攻击失败：${errorMessage}`);

        // Clear selection on error
        gameState.selectedAttackerUid = null;
        renderHighlights(gameState);
    }

    // Trigger UI update
    // renderGame(gameState); // REMOVED redundant render call
}

// --- Destruction Helper ---
function handleDestruction(cardInstance) {
    if (!cardInstance) return;
    // Avoid handling destruction for leaders here, game over logic handles it.
    if (cardInstance.type === "Leader") return;


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
    if (gameState.selectedAttackerUid) {
        gameState.selectedAttackerUid = null;
        // Trigger UI update to remove highlights
        clearSelectionHighlights(); // <<<< REPLACED with specific UI call
    } else {
    }
    // TODO: Add cancellation for selectedCardUid if needed later
}

// 检查游戏是否结束
// 检查游戏是否结束 (Refactored to check leaderCard health)
// Removed checkGameOver - Server now dictates game end via events.

// 进化随从
// 进化随从 (Implemented Version)
function cardHasKeyword(cardInstance, keywordName) {
    if (!cardInstance || !keywordName) return false;
    const rawKeywords = cardInstance.keywords ?? cardInstance.Keywords ?? [];
    if (!Array.isArray(rawKeywords)) return false;
    const normalizedTarget = String(keywordName).trim().toLowerCase();
    return rawKeywords.some(keyword => String(keyword ?? '').trim().toLowerCase() === normalizedTarget);
}

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
        alert("本回合已使用过进化权或无法进化。");
        return;
    }
    const hasFreeEvolve = cardHasKeyword(cardInstance, "FreeEvolve");
    if (!hasFreeEvolve && playerState.ep < 1) {
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
    const connection = getActiveConnection();
    if (!connection || connection.state !== "Connected") {
        console.error("GM: EvolveCard failed - SignalR connection not available.");
        alert("与服务器连接丢失，无法进化！");
        return;
    }

    try {
        // Construct DTO (PascalCase) - Fixed property name to match backend
        const actionDto = { UnitUid: cardUid }; // Use PascalCase and correct property name
        await connection.invoke("PerformEvolve", actionDto); // Corrected Hub method name and pass DTO

        // Wait for server event 'UnitEvolved' via processGameEvent for state update.
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

    const connection = getActiveConnection();
    if (!connection || connection.state !== "Connected") {
        console.error("GM: SuperEvolveCard failed - SignalR connection not available.");
        alert("与服务器连接丢失，无法超进化！");
        return;
    }

    try {
        const actionDto = { UnitUid: cardUid };
        await connection.invoke("PerformSuperEvolve", actionDto);
    } catch (error) {
        console.error("GM: Error invoking PerformSuperEvolve:", error);
        alert("超进化请求失败，请稍后再试。");
    }
}

// Act (Activated ability) request for a field card
export async function requestAct(cardUid, abilityRuntimeId = null) {
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
    if (gameState.gameOver) {
        console.warn("GM: Cannot use Act - Game is over.");
        return;
    }
    const actConnection = getActiveConnection();
    if (!actConnection || actConnection.state !== "Connected") {
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

    const actCache = gameState.actPlayabilityCache[cardUid];
    if (actCache && actCache.isPlayable === false) {
        const reason = actCache.failureReason || "启动条件不满足。";
        alert(reason);
        return;
    }

    const lastUsed = cardInstance.actLastUsedTurnNumber ?? null;
    if (lastUsed !== null && lastUsed === (playerState.turnNumber ?? 0)) {
        alert("该卡牌本回合已启动过。");
        return;
    }

    try {
        const actionDto = { CardUid: cardUid };
        if (abilityRuntimeId) {
            actionDto.AbilityRuntimeId = abilityRuntimeId;
        }
        await actConnection.invoke("RequestAct", actionDto);
    } catch (err) {
        console.error("GM: SignalR 'RequestAct' invocation failed:", err);
        alert(`启动失败：${err?.message || "服务器通信错误"}`);
    }
}

// Fusion request for a hand card
export async function requestFusion(cardUid, abilityRuntimeId = null) {
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
    if (gameState.gameOver) {
        console.warn("GM: Cannot use Fusion - Game is over.");
        return;
    }
    const fusionConnection = getActiveConnection();
    if (!fusionConnection || fusionConnection.state !== "Connected") {
        console.error("GM: RequestFusion failed - SignalR connection not available or not connected.");
        alert("与服务器连接丢失，无法融合！");
        return;
    }

    const playerState = getPlayerState(gameState.currentPlayerId);
    const cardInstance = findCardInstanceByUid(cardUid);

    if (!playerState || !cardInstance) {
        console.error(`GM: RequestFusion failed - Missing player/card state for UID ${cardUid}.`);
        return;
    }

    if (playerState.id !== gameState.localPlayerId || playerState.id !== gameState.currentPlayerId) {
        alert("现在不是你的回合！");
        return;
    }

    if (gameState.phase !== "Main") {
        alert("只能在主阶段进行融合！");
        return;
    }

    if (cardInstance.ownerId !== playerState.id || cardInstance.zone !== "Hand") {
        alert("只能融合自己手牌中的卡牌！");
        return;
    }

    try {
        const actionDto = { CardUid: cardUid };
        if (abilityRuntimeId) {
            actionDto.AbilityRuntimeId = abilityRuntimeId;
        }
        await fusionConnection.invoke("RequestFusion", actionDto);
    } catch (err) {
        console.error("GM: SignalR 'RequestFusion' invocation failed:", err);
        alert(`融合失败：${err?.message || "服务器通信错误"}`);
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
    const connection = getActiveConnection();
    if (!connection || connection.state !== "Connected") {
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

    try {
        // Pass the array directly, as expected by the Hub method List<string> parameter
        await connection.invoke("SubmitMulligan", cardUidsToReplace); // Pass the array directly
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
    // --- Proceed with checks ---
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
    const connection = getActiveConnection();
    if (!connection || connection.state !== "Connected") {
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
    try {
        const requestId = generateRequestId(); // Generate unique ID for this request

        // Send the string directly, as expected by the Hub method string parameter
        await connection.invoke("EndTurn", requestId); // Send the string directly
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
 * Sends in-match chat message through SignalR.
 * @param {string} content
 * @returns {Promise<{ok: boolean, error?: string}>}
 */
export async function sendChatMessage(content) {
    const normalizedContent = String(content ?? '').replace(/\s+/g, ' ').trim();
    if (!normalizedContent) {
        return { ok: false, error: '消息不能为空。' };
    }

    const connection = getActiveConnection();
    if (!connection || connection.state !== "Connected") {
        return { ok: false, error: '连接已断开，无法发送消息。' };
    }

    const clientMessageId = generateRequestId();
    const actionDto = {
        ClientMessageId: clientMessageId,
        Content: normalizedContent
    };

    try {
        pendingChatClientMessageIds.add(clientMessageId);
        await connection.invoke("SendChatMessage", actionDto);
        return { ok: true };
    } catch (err) {
        pendingChatClientMessageIds.delete(clientMessageId);
        const errorMessage = err?.message || '服务器通信错误';
        return { ok: false, error: errorMessage };
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

    const connection = getConnectionForPlayer(actionData.playerId || gameState.localPlayerId);
    if (!connection || connection.state !== "Connected") {
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

    try {
        // Invoke the generic server method. Server uses actionData.actionType to route internally.
        await connection.invoke(
            "SendGameAction",
            gameState.gameId,
            actionData
        );

        // Wait for server events triggered by this action via processGameEvent.
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
    const opponentPlayer = gameState.players.find(p => p.id !== gameState.localPlayerId);
    if (opponentPlayer) {
        if(opponentPlayer.leaderCard) {
        }
    }
    // --- 结束临时调试日志 ---

    // 只处理本地玩家的可玩性更新
    if (updateDto.PlayerId !== gameState.localPlayerId) {
        return;
    }


    const localPlayer = getPlayerState(gameState.localPlayerId);
    if (!localPlayer || !Array.isArray(localPlayer.hand)) {
        return;
    }

    const normalizeInstanceId = value => String(value ?? '').trim().toLowerCase();
    let hasHandCacheUpdate = false;
    let hasHandDisplayImpact = false;

    // 【修复】更新缓存：将服务器端的 InstanceId (Guid) 映射到客户端的 uid (string)
    const rawResults = updateDto.PlayabilityResults;
    Object.entries(rawResults).forEach(([instanceId, playabilityResult]) => {
        const normalizedInstanceId = normalizeInstanceId(instanceId);
        const cardInstance = localPlayer.hand.find(card => {
            if (!card) return false;
            return normalizeInstanceId(card.uid) === normalizedInstanceId
                || normalizeInstanceId(card.InstanceId) === normalizedInstanceId;
        });

        if (cardInstance) {
            // 使用统一的UID作为缓存键
            const cardUid = cardInstance.uid ?? cardInstance.InstanceId;
            const previousInfo = gameState.playabilityCache[cardUid] ?? null;
            const playModes = (playabilityResult.PlayModes || []).map(mode => ({
                mode: mode.Mode,
                effectiveCost: mode.EffectiveCost,
                isAvailable: mode.IsAvailable
            }));
            const selection = resolvePlayModeSelection(playModes, playabilityResult.SelectedPlayMode);

            const playabilityInfo = {
                cardUid: cardUid,
                isPlayable: playabilityResult.IsPlayable,
                requiresTargetSelection: playabilityResult.RequiresTargetSelection,
                hasValidTargets: playabilityResult.HasValidTargets,
                canPlayWithoutTarget: playabilityResult.CanPlayWithoutTarget,
                failureReason: playabilityResult.FailureReason,
                validTargetIds: playabilityResult.ValidTargetIds || [],
                playModes: playModes,
                selectedPlayMode: selection.mode,
                selectedPlayCost: selection.cost,
                selectedAbilityRuntimeId: playabilityResult.SelectedAbilityRuntimeId ?? null
            };
            gameState.playabilityCache[cardUid] = playabilityInfo;
            hasHandCacheUpdate = true;
            const previousMode = normalizePlayModeValue(previousInfo?.selectedPlayMode);
            const nextMode = normalizePlayModeValue(playabilityInfo.selectedPlayMode);
            const previousCost = previousInfo?.selectedPlayCost ?? null;
            const nextCost = playabilityInfo.selectedPlayCost ?? null;
            if (!previousInfo || previousMode !== nextMode || previousCost !== nextCost) {
                hasHandDisplayImpact = true;
            }
            refreshBattleCardPreviewIfCurrent(cardInstance);
        } else {
            console.warn(`GM: Could not find card instance with UID ${instanceId} in local player's hand`);
        }
    });

    if (hasHandCacheUpdate && hasHandDisplayImpact && gameState.phase !== 'Mulligan' && gameState.phase !== 'GameOver') {
        updateHandDisplay(localPlayer.id, localPlayer.hand);
    }

    // 可玩性高亮只在主流程阶段需要；Mulligan 阶段频繁重渲染会打断本地交互（如调度选牌）。
    if (gameState.phase === 'Mulligan' || gameState.phase === 'GameOver') {
        return;
    }

    renderHighlights(gameState);
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
    playerInstance.isAI = playerDto.IsAI ?? playerDto.isAI ?? playerInstance.isAI ?? false;
    playerInstance.isFirst = playerDto.IsFirst ?? playerDto.isFirst ?? playerInstance.isFirst ?? false;
    playerInstance.class = playerDto.Class ?? playerDto.class ?? playerInstance.class ?? null;
    playerInstance.displayName = playerDto.DisplayName ?? playerDto.displayName ?? playerInstance.displayName ?? '';
    playerInstance.avatarUrl = playerDto.AvatarUrl ?? playerDto.avatarUrl ?? playerInstance.avatarUrl ?? '';
    playerInstance.deckClass = playerDto.DeckClass ?? playerDto.deckClass ?? playerInstance.deckClass ?? '';
    playerInstance.pp = playerDto.CurrentPP ?? 0; // PascalCase
    playerInstance.maxPP = playerDto.MaxPP ?? 0; // PascalCase
    playerInstance.ppBoostUsesAvailable = playerDto.PpBoostUsesAvailable ?? 0;
    playerInstance.ep = playerDto.CurrentEP ?? 0; // PascalCase
    playerInstance.maxEP = playerDto.MaxEP ?? 0; // PascalCase
    playerInstance.sep = playerDto.CurrentSEP ?? 0;
    playerInstance.maxSEP = playerDto.MaxSEP ?? 0;
    const rawCounters = playerDto.Counters ?? playerDto.counters;
    playerInstance.counters = (rawCounters && typeof rawCounters === 'object') ? { ...rawCounters } : {};
    const rawCounterDisplayNames = playerDto.CounterDisplayNames ?? playerDto.counterDisplayNames;
    playerInstance.counterDisplayNames = (rawCounterDisplayNames && typeof rawCounterDisplayNames === 'object')
        ? { ...rawCounterDisplayNames }
        : {};

    const readCounter = (key, fallback = 0) => {
        const normalizedKey = String(key ?? '').trim().toLowerCase();
        if (normalizedKey && playerInstance.counters && typeof playerInstance.counters === 'object') {
            for (const [entryKey, entryValue] of Object.entries(playerInstance.counters)) {
                if (String(entryKey).trim().toLowerCase() !== normalizedKey) continue;
                const parsed = Number(entryValue);
                if (Number.isFinite(parsed)) {
                    return Math.max(0, Math.trunc(parsed));
                }
            }
        }
        return Number.isFinite(Number(fallback)) ? Math.max(0, Math.trunc(Number(fallback))) : 0;
    };

    playerInstance.bloodPool = readCounter('bloodpool', playerDto.BloodPool ?? playerDto.bloodPool ?? 0);
    playerInstance.combo = readCounter('combo', playerDto.Combo ?? playerDto.combo ?? 0);
    // FIXED: Backend sends CanEvolve, not CanEvolveThisTurn
      playerInstance.canEvolve = playerDto.CanEvolve ?? false; // PascalCase - Fixed property name
      playerInstance.canSuperEvolve = playerDto.CanSuperEvolve ?? false;
      playerInstance.hasUnlockedEvolution =
          playerDto.IsEvolutionUnlocked ?? playerDto.HasUnlockedEvolution ?? false;
      playerInstance.hasUnlockedSuperEvolution =
          playerDto.IsSuperEvolutionUnlocked ?? playerDto.HasUnlockedSuperEvolution ?? false;
      playerInstance.turnNumber = playerDto.TurnNumber ?? playerDto.turnNumber ?? 0; // PascalCase/camelCase
      playerInstance.deckCount = playerDto.DeckCount ?? playerDto.deckCount ?? (Array.isArray(playerDto.Deck ?? playerDto.deck) ? (playerDto.Deck ?? playerDto.deck).length : 0); // PascalCase/camelCase
      playerInstance.graveyardCount = readCounter('graveyard', playerDto.GraveyardCount ?? playerInstance.graveyardCount ?? 0);
      // Keep deck as simple array of IDs/DTOs from server if no methods needed client-side
      playerInstance.deck = Array.isArray(playerDto.Deck ?? playerDto.deck) ? [...(playerDto.Deck ?? playerDto.deck)] : []; // PascalCase/camelCase
      // Mulligan
    playerInstance.hasCompletedMulligan = playerDto.HasCompletedMulligan ?? playerDto.hasCompletedMulligan ?? false; // PascalCase/camelCase


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
                cardInstance.isSuperEvolved = cardDto.IsSuperEvolved ?? cardDto.isSuperEvolved ?? false;
                cardInstance.fieldTurn = cardDto.FieldTurn ?? 0; // Hydrate FieldTurn
                // ADDED: Hydrate keywords (Ensure DTO has 'Keywords' property as an array)
                cardInstance.keywords = Array.isArray(cardDto.Keywords) ? [...cardDto.Keywords] : []; // PascalCase
            }
            // ADDED: Hydrate keywords for hand cards too (needed for UI checks like Ward indicator)
            else if (zoneName === 'Hand') {
                 // Use PascalCase: Keywords
                 cardInstance.keywords = Array.isArray(cardDto.Keywords) ? [...cardDto.Keywords] : []; // PascalCase
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
    const readStateField = (source, pascal, camel) => source?.[pascal] ?? source?.[camel];
    const normalizeId = (value) => String(value ?? '').trim().toLowerCase();
    const playersDto = readStateField(serverState, 'Players', 'players');
    const incomingStateGameId = readStateField(serverState, 'GameId', 'gameId');

    // Use PascalCase: Players, GameId
    if (!serverState || !Array.isArray(playersDto) || !incomingStateGameId) { // PascalCase/camelCase compatible check
        console.error(
            "GAME MANAGER: Received invalid or incomplete full game state object:",
            serverState
        );
        alert("错误：收到无效的游戏状态数据！");
        return false;
    }

    // Deep merge or careful assignment is needed if local state has properties server doesn't send.
    // For now, assuming serverState is the complete source of truth for shared properties.
    // We preserve local-only properties like signalRConnection if they exist outside gameState.

    // Merge the server state into the local gameState
    // --- Smart Hydration: Assign top-level properties ---
    const incomingGameId = incomingStateGameId;
    const isNewGameSession = incomingGameId && incomingGameId !== gameState.gameId;
    if (isNewGameSession) {
        gameState.gameLog = [];
        gameState.chatMessages = [];
        pendingChatClientMessageIds.clear();
        renderGameLog(gameState.gameLog);
        clearInMatchChatUiState();
    }
    // Use PascalCase: GameId
    gameState.gameId = incomingGameId; // PascalCase
    // IMPORTANT:
    // - In PVP, localPlayerId MUST come from the lobby handshake (initializeGame), and MUST NOT be inferred here.
    // - In VS-AI, localPlayerId may be inferred as the only non-AI player if the handshake is missing.
    if (playersDto.length > 0) {
        const serverPlayerIds = playersDto
            .map(p => p?.PlayerId ?? p?.playerId ?? p?.Id ?? p?.id)
            .filter(Boolean);
        const serverPlayerIdMap = new Map(
            serverPlayerIds.map(id => [normalizeId(id), id])
        );
        const existingLocalPlayerId = gameState.localPlayerId;
        const matchedLocalPlayerId = serverPlayerIdMap.get(normalizeId(existingLocalPlayerId)) || null;
        const localPlayerIdIsValid = !!matchedLocalPlayerId;
        const isDualControl = gameState.dualControl && !gameState.isSpectator;

        if (localPlayerIdIsValid && gameState.localPlayerId !== matchedLocalPlayerId) {
            gameState.localPlayerId = matchedLocalPlayerId;
        }

        if (isDualControl) {
            const phase = readStateField(serverState, 'Phase', 'phase');
            const currentPlayerId = readStateField(serverState, 'CurrentPlayerId', 'currentPlayerId');
            if (phase !== 'Mulligan' && currentPlayerId) {
                gameState.localPlayerId = currentPlayerId;
            } else if (!localPlayerIdIsValid) {
                const normalizedHostSeatId = normalizeId(gameState.dualControlSeatIds?.hostPlayerId);
                const resolvedHostSeatId = serverPlayerIdMap.get(normalizedHostSeatId) || null;
                gameState.localPlayerId =
                    resolvedHostSeatId || serverPlayerIds[0] || null;
            }
        } else if (!localPlayerIdIsValid) {
            if (gameState.isSpectator) {
                gameState.localPlayerId = serverPlayerIds[0] || null;
            }

            const nonAiPlayers = playersDto.filter(p => {
                const playerId = p?.PlayerId ?? p?.playerId ?? p?.Id ?? p?.id;
                const isAi = p?.IsAI ?? p?.isAI;
                return !!playerId && isAi === false;
            });

            if (nonAiPlayers.length === 1) {
                gameState.localPlayerId = nonAiPlayers[0].PlayerId ?? nonAiPlayers[0].playerId ?? nonAiPlayers[0].Id ?? nonAiPlayers[0].id;
                console.warn(
                    `GM HYDRATION: localPlayerId was missing/invalid; inferred non-AI player ${gameState.localPlayerId} for VS-AI.`
                );
            } else {
                if (!gameState.isSpectator) {
                    console.error(
                        "GM HYDRATION: localPlayerId is missing/invalid and cannot be inferred safely for PVP.",
                        { existingLocalPlayerId, serverPlayerIds }
                    );
                    return false;
                }
            }
        }
    }
    // Use PascalCase: CurrentPlayerId
    gameState.currentPlayerId = readStateField(serverState, 'CurrentPlayerId', 'currentPlayerId'); // PascalCase/camelCase
    // 【架构重构】不再从serverState获取全局TurnNumber，改为从当前玩家的PlayerState获取
    // Use PascalCase: Phase
    gameState.phase = readStateField(serverState, 'Phase', 'phase') || "Unknown"; // PascalCase/camelCase
    const timeoutSeconds = serverState.TurnTimeoutSeconds ?? serverState.turnTimeoutSeconds;
    gameState.turnTimeoutSeconds = Number.isFinite(timeoutSeconds) ? timeoutSeconds : gameState.turnTimeoutSeconds;
    gameState.turnStartedAtUtc = serverState.TurnStartedAtUtc ?? serverState.turnStartedAtUtc ?? null;
    gameState.turnEndsAtUtc = serverState.TurnEndsAtUtc ?? serverState.turnEndsAtUtc ?? null;
    // Use PascalCase: GameOver
    gameState.gameOver = readStateField(serverState, 'GameOver', 'gameOver') || false; // PascalCase/camelCase
    // Use PascalCase: Winner
    gameState.winner = readStateField(serverState, 'Winner', 'winner') || null; // PascalCase/camelCase
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
    if (playersDto.length > 0) {
        // Use PascalCase: Players
        for (const playerDto of playersDto) {
            const playerDtoId = playerDto?.PlayerId ?? playerDto?.playerId ?? playerDto?.Id ?? playerDto?.id;
            // Use PascalCase: PlayerId
            if (!playerDto || !playerDtoId) {
                console.warn("GM HYDRATION: Skipping invalid player DTO:", playerDto);
                continue;
            }

            // --- REFACTOR: Find existing or create new player instance ---
            let playerInstance = gameState.players.find(p => p && normalizeId(p.id) === normalizeId(playerDtoId)); // Find existing by ID (case-insensitive)
            let isNewPlayer = false;

            if (!playerInstance) {
                // If not found, create a new one using the modified createPlayer
                // Use PascalCase: PlayerId, IsAI
                playerInstance = createPlayer(playerDtoId, playerDto.IsAI ?? playerDto.isAI ?? false); // PascalCase/camelCase
                isNewPlayer = true; // Flag that this is a new instance
                if (!playerInstance) {
                    // Use PascalCase: PlayerId
                    console.error(`GM HYDRATION: Failed to create NEW player instance for ID ${playerDtoId}. Skipping.`); // PascalCase/camelCase
                    continue; // Skip this player DTO if creation fails
                }
            }
            // --- END REFACTOR ---

            // Hydrate basic properties (works for both new and existing instances)
            _hydratePlayerProperties(playerInstance, playerDto);

            // --- REFACTOR: Integrate Leader Card Hydration ---
            // Use PascalCase: LeaderCard
            const leaderDto = playerDto.LeaderCard ?? playerDto.leaderCard;
            if (leaderDto) {
                // Use PascalCase: LeaderCard
                const serverLeaderDto = leaderDto;
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
            playerInstance.hand = _hydrateCardZone(playerDto.Hand ?? playerDto.hand, playerInstance.id, 'Hand'); // PascalCase/camelCase
            // Use PascalCase: Field
            playerInstance.field = _hydrateCardZone(playerDto.Field ?? playerDto.field, playerInstance.id, 'Field'); // PascalCase/camelCase
            // Use PascalCase: Graveyard
            playerInstance.graveyard = _hydrateCardZone(playerDto.Graveyard ?? playerDto.graveyard, playerInstance.id, 'Graveyard'); // PascalCase/camelCase
            // Use PascalCase: Emblem
            playerInstance.emblem = _hydrateCardZone(playerDto.Emblem ?? playerDto.emblem, playerInstance.id, 'Emblem'); // PascalCase/camelCase
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
    // if (serverState.players && serverState.players.length > 0) {
    // }
    // GM DEBUG END - REMOVED
    // The players array from serverState now replaces the old one.
    // Find the active and opponent player *instances* within the *new* players array.
    gameState.activePlayer =
        gameState.players.find(
            (p) => p && normalizeId(p.id) === normalizeId(gameState.currentPlayerId) // FIX: Use p.id (case-insensitive)
        ) || null;

    gameState.opponentPlayer =
        gameState.players.find(
            (p) => p && normalizeId(p.id) !== normalizeId(gameState.currentPlayerId) // FIX: Use p.id (case-insensitive)
        ) || null;


    // Add detailed logging to verify player references
    if (gameState.activePlayer) {
    } else {
        console.warn(
            `GM: Could not find active player instance for ID: ${gameState.currentPlayerId} after full state update.`
        );
    }
    if (gameState.opponentPlayer) {
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

    selectDualControlMulliganSeat();

    // Ensure the UI is re-rendered with the fully updated state
    if (typeof renderGame === "function") {
        renderGame(gameState); // Pass the updated complete gameState
        refreshTurnTimerFromState();
    } else {
        console.error(
            "GAME MANAGER: renderGame function is not available for UI update after full state update."
        );
    }

    return true;
}

// TODO: Add other exports here later if needed (e.g., processGameEvent) - Updated export list

// --- NEW Specific Event Handlers ---

export function handleTurnStarted(eventData) {

    // --- ROO DEBUG: Log raw event data received by handleTurnStarted ---
    // --- END ROO DEBUG ---

    // --- 【修复】改进缓存清理策略 ---
    // 只在回合真正开始且游戏状态发生重大变化时清理缓存
    // 避免频繁清理导致的缓存未命中问题
    if (eventData?.GameState?.CurrentPlayerId && eventData.GameState.CurrentPlayerId === gameState.localPlayerId) {
        // 检查是否是新的回合（回合数增加）或游戏状态发生重大变化
        const currentTurnNumber = eventData.GameState.Players?.find(p => p.Id === gameState.localPlayerId)?.TurnNumber || 0;
        const previousTurnNumber = gameState.turnNumber || 0;

        if (currentTurnNumber > previousTurnNumber) {
            gameState.playabilityCache = {}; // Clear the cache object only for new turns
            gameState.actPlayabilityCache = {}; // Clear Act cache for new turns
        } else {
        }
    }
    // --- End Cache Clearing ---


    // The "TurnStarted" event now carries the full GameStateDto nested within GameState property.
    // Delegate the processing to the function designed for full state updates, passing the nested object.
    // Use PascalCase: GameState
    if (eventData && eventData.GameState) { // PascalCase
        const hydrationSucceeded = updateFullGameState(eventData.GameState); // Pass the nested GameState object
        if (hydrationSucceeded === false) {
            console.error("GM: TurnStarted state hydration failed; skipping turn-start banner.");
            return;
        }
        if (typeof showTurnStartBanner === 'function') {
            showTurnStartBanner(gameState.currentPlayerId);
        }
    } else {
        console.error("GM Error: TurnStarted event received without a valid nested GameState object:", eventData);
        alert("错误：回合开始事件数据无效！");
    }

    // No need for separate logic here to update currentPlayerId, phase, player turns, or render.
    // updateFullGameState handles all necessary state updates and the final render call.
}

export function handleTurnEnded(eventData) {
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

    // Update player references just in case, although TurnStarted should be the source of truth
    // gameState.activePlayer = getPlayerState(gameState.currentPlayerId); // Keep current player active during this phase
    // gameState.opponentPlayer = gameState.players.find(p => p && p.id !== gameState.currentPlayerId);

    // Log expected next player for debugging purposes

    // Do NOT change currentPlayerId or turnNumber here.
    // Do NOT update PP/EP or draw cards here.

    // renderGame(gameState); // REMOVED: Rendering handled by subsequent TurnStarted event.
}
export function handlePpUpdate(eventData) {
    // DTO: PpUpdateEventDto { string SourcePlayerId, int CurrentPp, int MaxPp }
    if (!eventData || !eventData.SourcePlayerId || eventData.CurrentPp === undefined || eventData.MaxPp === undefined) { // PascalCase
        console.error("GM Error: Invalid data in handlePpUpdate:", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId); // PascalCase

    let handChanged = false; // BUG-016 Flag to track if hand was modified

    if (playerToUpdate) {
        playerToUpdate.pp = eventData.CurrentPp; // PascalCase
        playerToUpdate.maxPP = eventData.MaxPp; // PascalCase

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
    // Fixed: Use correct DTO property names CurrentEp and MaxEp
    if (!eventData || !eventData.SourcePlayerId || eventData.CurrentEp === undefined || eventData.MaxEp === undefined) {
        console.error("GM Error: Invalid data in handleEpUpdate (expected SourcePlayerId, CurrentEp, MaxEp):", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.ep = eventData.CurrentEp;
        playerToUpdate.maxEP = eventData.MaxEp;
        updateEpDisplay(playerToUpdate.id, playerToUpdate.ep, playerToUpdate.maxEP);
        if (Array.isArray(playerToUpdate.field)) {
            playerToUpdate.field.forEach(card => {
                updateCardActionButtons(card);
                refreshBattleCardPreviewIfCurrent(card);
            });
        }
    } else {
        console.warn(`GM: EpUpdate event received for invalid player: ${eventData.SourcePlayerId}`);
    }
}

export function handleSepUpdate(eventData) {
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
    } else {
        console.warn(`GM: SepUpdate event received for invalid player: ${eventData.SourcePlayerId}`);
    }
}

// 新增：仅处理 PP 最大值更新事件
export function handlePpMaxUpdate(eventData) {
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
    updateEpDisplay(playerToUpdate.id, playerToUpdate.ep, playerToUpdate.maxEP);
    if (Array.isArray(playerToUpdate.field)) {
        playerToUpdate.field.forEach(card => {
            updateCardActionButtons(card);
            refreshBattleCardPreviewIfCurrent(card);
        });
    }
}

export function handleSepMaxUpdate(eventData) {
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
}

/**
 * 处理服务器主动推送的启动可用性更新
 * @param {Object} updateDto - ActPlayabilityUpdateDto from server
 */
export function handleActPlayabilityUpdate(updateDto) {
    if (!updateDto || !updateDto.PlayerId || !updateDto.PlayabilityResults) {
        console.warn("GM: Received invalid Act playability update:", updateDto);
        return;
    }

    if (updateDto.PlayerId !== gameState.localPlayerId) {
        return;
    }

    Object.entries(updateDto.PlayabilityResults).forEach(([instanceId, playabilityResult]) => {
        const localPlayer = getPlayerState(gameState.localPlayerId);
        if (localPlayer && localPlayer.field) {
            const cardInstance = localPlayer.field.find(card => card && (card.uid === instanceId || card.InstanceId === instanceId));
            if (cardInstance) {
                const cardUid = cardInstance.uid ?? cardInstance.InstanceId;
                const abilityEntries = Array.isArray(playabilityResult.Abilities)
                    ? playabilityResult.Abilities
                    : (Array.isArray(playabilityResult.abilities) ? playabilityResult.abilities : []);
                const normalizedAbilities = abilityEntries.map(entry => ({
                    abilityRuntimeId: entry.AbilityRuntimeId ?? entry.abilityRuntimeId ?? null,
                    abilityName: entry.AbilityName ?? entry.abilityName ?? '',
                    actCost: entry.ActCost ?? entry.actCost ?? 0,
                    isPlayable: entry.IsPlayable ?? entry.isPlayable ?? true,
                    requiresTargetSelection: entry.RequiresTargetSelection ?? entry.requiresTargetSelection ?? false,
                    hasValidTargets: entry.HasValidTargets ?? entry.hasValidTargets ?? true,
                    failureReason: entry.FailureReason ?? entry.failureReason ?? '',
                    validTargetIds: entry.ValidTargetIds ?? entry.validTargetIds ?? []
                }));
                const actInfo = {
                    cardUid,
                    isPlayable: playabilityResult.IsPlayable,
                    actCost: playabilityResult.ActCost,
                    requiresTargetSelection: playabilityResult.RequiresTargetSelection,
                    hasValidTargets: playabilityResult.HasValidTargets,
                    failureReason: playabilityResult.FailureReason,
                    validTargetIds: playabilityResult.ValidTargetIds || [],
                    abilities: normalizedAbilities
                };
                gameState.actPlayabilityCache[cardUid] = actInfo;
                updateActButtonState(cardInstance);
                refreshBattleCardPreviewIfCurrent(cardInstance);
            }
        }
    });

    // Act updates are handled per-card; avoid full re-render to prevent UI jumps.
}

/**
 * Sends a request to the server to use PP+1.
 */
export async function requestPpBoost() {
    if (gameState.isSpectator) {
        alert("观战模式：不能进行操作。");
        return;
    }
    const connection = getActiveConnection();
    if (!connection || connection.state !== "Connected") {
        console.error("GM: requestPpBoost failed - SignalR connection not available.");
        alert("与服务器连接丢失，无法使用PP+1！");
        return;
    }

    const playerState = getPlayerState(gameState.currentPlayerId);
    if (!playerState) {
        console.error("GM: requestPpBoost failed - Player state missing.");
        return;
    }

    if (playerState.id !== gameState.localPlayerId || playerState.id !== gameState.currentPlayerId) {
        alert("现在不是你的回合！");
        return;
    }

    if (gameState.phase !== "Main" && gameState.phase !== "TurnStart") {
        alert("只能在自己的回合使用PP+1！");
        return;
    }

    if ((playerState.ppBoostUsesAvailable ?? 0) <= 0) {
        alert("没有可用的PP+1次数。");
        return;
    }

    try {
        await connection.invoke("PerformPpBoost");
    } catch (err) {
        console.error("GM: SignalR 'PerformPpBoost' invocation failed:", err);
        alert(`PP+1使用失败：${err?.message || "服务器通信错误"}`);
    }
}

export function handlePpBoostStateUpdated(eventData) {
    if (!eventData || !eventData.SourcePlayerId || eventData.PpBoostUsesAvailable === undefined) {
        console.error("GM Error: Invalid data in handlePpBoostStateUpdated (expected SourcePlayerId, PpBoostUsesAvailable):", eventData);
        return;
    }
    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (!playerToUpdate) {
        console.warn(`GM: PpBoostStateUpdated event received for invalid player: ${eventData.SourcePlayerId}`);
        return;
    }

    playerToUpdate.ppBoostUsesAvailable = eventData.PpBoostUsesAvailable;
    updatePpBoostDisplay(playerToUpdate.id, playerToUpdate.ppBoostUsesAvailable);
}


/**
 * Handles HandUpdated events from the server.
 * This event is sent when a player's hand is completely updated (e.g., after mulligan).
 * @param {Object} eventData - HandUpdatedEventDto from server
 */
export function handleHandUpdated(eventData) {

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

    // HandUpdated is treated as a hand snapshot sync only.
    playerToUpdate.hand = eventData.UpdatedHand
        .map(cardDto => createCardInstance(cardDto))
        .filter(card => card !== null);

    playerToUpdate.hand.forEach(card => {
        card.ownerId = playerToUpdate.id;
        card.zone = 'Hand';
    });

    // Update the hand display
    updateHandDisplay(playerToUpdate.id, playerToUpdate.hand);

    // Clear any mulligan selection state since hand was updated
    if (typeof resetMulliganSelection === 'function' && playerToUpdate.id === gameState.localPlayerId) {
        resetMulliganSelection();
    }
}

export function handleCardInstanceUpdated(eventData) {

    if (!eventData || !Array.isArray(eventData.UpdatedCards)) {
        console.error("GM Error: Invalid data in handleCardInstanceUpdated:", eventData);
        return;
    }

    const handOwnersToRefresh = new Set();
    const emblemOwnersToRefresh = new Set();
    const updatedInstances = [];

    for (const cardDto of eventData.UpdatedCards) {
        const updatedInstance = createCardInstance(cardDto);
        if (!updatedInstance) {
            continue;
        }

        const existingInstance = findCardInstanceByUid(updatedInstance.uid);
        if (!existingInstance) {
            continue;
        }

        const preservedZone = existingInstance.zone;
        const preservedOwnerId = existingInstance.ownerId;
        const preservedTurnPlayed = existingInstance.turnPlayed;
        const preservedAttackCount = existingInstance.attackCount;

        Object.assign(existingInstance, updatedInstance);

        if (preservedZone !== undefined) {
            existingInstance.zone = preservedZone;
        }
        if (preservedOwnerId !== undefined) {
            existingInstance.ownerId = preservedOwnerId;
        }
        existingInstance.turnPlayed = preservedTurnPlayed ?? existingInstance.turnPlayed;
        existingInstance.attackCount = preservedAttackCount ?? existingInstance.attackCount;

        updatedInstances.push(existingInstance);

        refreshCardInstanceUi(existingInstance, handOwnersToRefresh, emblemOwnersToRefresh);
    }

    for (const ownerId of handOwnersToRefresh) {
        const player = getPlayerState(ownerId);
        if (player) {
            updateHandDisplay(player.id, player.hand);
        }
    }

    for (const ownerId of emblemOwnersToRefresh) {
        const player = getPlayerState(ownerId);
        if (player) {
            updateEmblemZone(player);
        }
    }

    updatedInstances.forEach(instance => refreshBattleCardPreviewIfCurrent(instance));
}

export function handleCardMoved(eventData) {
    const cardUid = eventData?.CardUid ?? eventData?.MovedCard?.InstanceId;
    const ownerId = eventData?.PlayerId ?? eventData?.MovedCard?.OwnerPlayerId ?? eventData?.SourcePlayerId;
    const fromZone = normalizeZoneName(eventData?.FromZone);
    const toZone = normalizeZoneName(eventData?.ToZone);

    if (!cardUid || !toZone) {
        console.error("GM Error: Invalid data in handleCardMoved:", eventData);
        return;
    }

    const ownerPlayer = ownerId ? getPlayerState(ownerId) : null;
    const detached = detachCardFromAllZones(cardUid);

    let movedInstance = eventData?.MovedCard ? createCardInstance(eventData.MovedCard) : null;
    if (!movedInstance) {
        movedInstance = detached.card;
    }

    if (!movedInstance) {
        console.warn(`GM: CardMoved cannot hydrate card instance: ${cardUid}`);
    }

    const touchedZones = new Set();
    const markTouched = (playerId, zoneName) => {
        if (!playerId || !zoneName) return;
        touchedZones.add(`${playerId}:${zoneName}`);
    };

    for (const removed of detached.removed) {
        adjustZoneCount(getPlayerState(removed.playerId), removed.zone, removed.zone === "deck" || removed.zone === "graveyard" ? -1 : 0);
        markTouched(removed.playerId, removed.zone);
    }

    const trackedZones = new Set(["hand", "field", "graveyard", "emblem", "deck"]);
    if (trackedZones.has(toZone) && ownerPlayer) {
        const destination = ensureZoneArray(ownerPlayer, toZone);
        if (destination && movedInstance) {
            movedInstance.ownerId = ownerPlayer.id;
            movedInstance.zone = toZoneLabel(toZone);

            const existingIndex = findCardInZoneArray(destination, cardUid);
            if (existingIndex < 0) {
                if (toZone === "field" && Number.isInteger(movedInstance.zoneIndex) && movedInstance.zoneIndex >= 0 && movedInstance.zoneIndex <= destination.length) {
                    destination.splice(movedInstance.zoneIndex, 0, movedInstance);
                } else {
                    destination.push(movedInstance);
                }
            } else {
                destination[existingIndex] = movedInstance;
            }
        }

        adjustZoneCount(ownerPlayer, toZone, toZone === "deck" || toZone === "graveyard" ? 1 : 0);
        markTouched(ownerPlayer.id, toZone);
    }

    if (fromZone && ownerPlayer && detached.removed.length === 0 && (fromZone === "deck" || fromZone === "graveyard")) {
        adjustZoneCount(ownerPlayer, fromZone, -1);
        markTouched(ownerPlayer.id, fromZone);
    }

    for (const key of touchedZones) {
        const [playerId, zoneName] = key.split(":");
        const player = getPlayerState(playerId);
        if (player) {
            refreshPlayerZoneUi(player, zoneName);
        }
    }

    if (movedInstance) {
        refreshBattleCardPreviewIfCurrent(movedInstance);
    }

    if (gameState.selectedAttackerUid && !findCardInstanceByUid(gameState.selectedAttackerUid)) {
        gameState.selectedAttackerUid = null;
        clearSelectionHighlights();
    }
}

export function handleTuck(eventData) {
    // Placeholder for tuck visual effects. Zone movement is handled by CardMoved.
    if (!eventData || !eventData.CardUid) {
        console.error("GM Error: Invalid data in handleTuck:", eventData);
        return;
    }
}

export function handleCounterChanged(eventData) {
    if (!eventData || !eventData.CounterName) {
        console.error("GM Error: Invalid data in handleCounterChanged:", eventData);
        return;
    }

    const targetCardId = eventData.TargetCardInstanceId ?? eventData.targetCardInstanceId;
    const targetPlayerId = eventData.TargetPlayerId ?? eventData.targetPlayerId;
    const counterName = String(eventData.CounterName ?? eventData.counterName ?? '').trim();
    const newValue = eventData.NewValue ?? eventData.newValue;

    if (counterName.length === 0 || newValue === undefined) {
        console.warn("GM: CounterChanged missing counter name or new value:", eventData);
        return;
    }

    const normalizedCounter = counterName.toLowerCase();

    if (targetCardId) {
        const cardInstance = findCardInstanceByUid(targetCardId);
        if (!cardInstance) {
            console.warn(`GM: CounterChanged target card not found: ${targetCardId}`);
        } else {
            if (!cardInstance.counters || typeof cardInstance.counters !== 'object') {
                cardInstance.counters = {};
            }
            cardInstance.counters[counterName] = newValue;

            if (normalizedCounter === 'earthsigil') {
                cardInstance.earthsigil = newValue;
            } else if (normalizedCounter === 'spellboost') {
                cardInstance.spellboost = newValue;
            }

            refreshCardInstanceUi(cardInstance);
            refreshBattleCardPreviewIfCurrent(cardInstance);
        }
    }

    if (targetPlayerId) {
        const player = getPlayerState(targetPlayerId);
        if (!player) {
            console.warn(`GM: CounterChanged target player not found: ${targetPlayerId}`);
            return;
        }

        if (!player.counters || typeof player.counters !== 'object') {
            player.counters = {};
        }
        player.counters[counterName] = newValue;

        if (normalizedCounter === 'bloodpool') {
            player.bloodPool = newValue;
            updateBloodPoolDisplay(player.id, player.bloodPool);
        } else if (normalizedCounter === 'combo') {
            player.combo = newValue;
            updateComboDisplay(player.id, player.combo);
        } else if (normalizedCounter === 'graveyard') {
            player.graveyardCount = newValue;
            updateGraveyardCountDisplay(player.id, player.graveyardCount);
        }

        if (player.leaderCard) {
            refreshBattleCardPreviewIfCurrent(player.leaderCard);
        }
    }
}

export function handleDeckUpdate(eventData) {
    // DTO: DeckUpdateEventDto { string SourcePlayerId, int DeckCount }
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
    if (!playerToUpdate.counters || typeof playerToUpdate.counters !== 'object') {
        playerToUpdate.counters = {};
    }
    playerToUpdate.counters.graveyard = newServerCount;

    // Log the update for clarity.

    // Directly call the UI update function with the correct player ID and the new count.
    // The UI function is responsible for determining if this is the 'local' or 'opponent' player.
    updateGraveyardCountDisplay(updatedPlayerId, newServerCount);
    if (playerToUpdate.leaderCard) {
        refreshBattleCardPreviewIfCurrent(playerToUpdate.leaderCard);
    }
}

export function handleEmblemUpdated(eventData) {
    // Zone movement is handled by CardMoved.
    // This event is reserved for emblem-related visual effects.
    if (!eventData) {
        console.error("GM Error: Invalid data in handleEmblemUpdated:", eventData);
    }
}

// Removed handleUnitPlayed function
// Removed handleAmuletPlayed function

// Removed handleSpellPlayed function

export function handleUnitEvolved(eventData) {
    // DTO: UnitEvolvedEventDto { string SourcePlayerId, CardInstanceDto EvolvedUnitState }
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
        updateUnitDisplay(unit); // Use specific UI update instead of full render
        updateHealthDisplay(unit);
    } else {
        console.warn(`GM: StatChange event received for invalid unit ID: ${targetUid}`); // PascalCase
    }
}

function updateCardKeywords(cardInstance, keyword, shouldAdd) {
    if (!cardInstance || !keyword) return false;
    const normalizedKeyword = String(keyword).trim();
    if (!normalizedKeyword) return false;
    if (!Array.isArray(cardInstance.keywords)) {
        cardInstance.keywords = [];
    }

    const hasKeyword = cardInstance.keywords.some(k => String(k).toLowerCase() === normalizedKeyword.toLowerCase());
    if (shouldAdd) {
        if (!hasKeyword) {
            cardInstance.keywords.push(normalizedKeyword);
        }
    } else if (hasKeyword) {
        cardInstance.keywords = cardInstance.keywords.filter(
            k => String(k).toLowerCase() !== normalizedKeyword.toLowerCase()
        );
    }
    return true;
}

function refreshAttackHighlightsIfNeeded() {
    const attackerUid = gameState.selectedAttackerUid;
    if (!attackerUid) return;
    const attackerInstance = findCardInstanceByUid(attackerUid);
    if (!attackerInstance) {
        gameState.selectedAttackerUid = null;
        clearSelectionHighlights();
        return;
    }
    clearSelectionHighlights();
    highlightAttacker(attackerUid);
    highlightTargets(attackerInstance);
}

export function handleKeywordAdded(eventData) {
    const targetUid = eventData?.CardInstanceId;
    const keyword = eventData?.Keyword;
    if (!targetUid || !keyword) {
        console.error("GM Error: Invalid data in handleKeywordAdded (missing CardInstanceId/Keyword):", eventData);
        return;
    }

    const unit = findCardInstanceByUid(targetUid);
    if (!unit) {
        console.warn(`GM: KeywordAdded event received for invalid card UID: ${targetUid}`);
        return;
    }

    updateCardKeywords(unit, keyword, true);
    updateUnitDisplay(unit);
    refreshBattleCardPreviewIfCurrent(unit);
    refreshAttackHighlightsIfNeeded();
}

export function handleKeywordRemoved(eventData) {
    const targetUid = eventData?.CardInstanceId;
    const keyword = eventData?.Keyword;
    if (!targetUid || !keyword) {
        console.error("GM Error: Invalid data in handleKeywordRemoved (missing CardInstanceId/Keyword):", eventData);
        return;
    }

    const unit = findCardInstanceByUid(targetUid);
    if (!unit) {
        console.warn(`GM: KeywordRemoved event received for invalid card UID: ${targetUid}`);
        return;
    }

    updateCardKeywords(unit, keyword, false);
    updateUnitDisplay(unit);
    refreshBattleCardPreviewIfCurrent(unit);
    refreshAttackHighlightsIfNeeded();
}

export function handleUnitDestroyed(eventData) {
    // DTO: UnitDestroyedEventDto { UnitUid, PlayerId, Reason }
    // Zone movement is handled by CardMoved.
    if (!eventData || !eventData.UnitUid) {
        console.error("GM Error: Invalid data in handleUnitDestroyed:", eventData);
        return;
    }
    const unit = findCardInstanceByUid(eventData.UnitUid);
    if (!unit) {
        console.warn(`GM: UnitDestroyed event received for invalid unit UID: ${eventData.UnitUid}`);
        return;
    }

    // Placeholder for destroy visual effects.
    refreshBattleCardPreviewIfCurrent(unit);
}

export function handleCardVanished(eventData) {
    // DTO: CardVanishedEventDto { CardUid, PlayerId, Reason }
    // Zone movement is handled by CardMoved.
    if (!eventData || !eventData.CardUid) {
        console.error("GM Error: Invalid data in handleCardVanished:", eventData);
        return;
    }

    const card = findCardInstanceByUid(eventData.CardUid);
    if (card) {
        // Placeholder for vanish visual effects.
        refreshBattleCardPreviewIfCurrent(card);
    }
}

export function handleCardReturned(eventData) {
    // Zone movement is handled by CardMoved.
    const cardUid = eventData?.CardUid ?? eventData?.ReturnedCard?.InstanceId;
    if (!cardUid) {
        console.error("GM Error: Invalid data in handleCardReturned:", eventData);
        return;
    }

    const returnedCard = findCardInstanceByUid(cardUid);
    if (returnedCard) {
        // Placeholder for return visual effects.
        refreshBattleCardPreviewIfCurrent(returnedCard);
    }
}

export function handleAttackDeclared(eventData) {
    // DTO: Assume AttackDeclaredEventDto { string AttackerUid, string TargetUid }
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


    // Future: Trigger attack animation start here?
    // Example: showAttackArrow(attacker.uid, target.uid);

    // No state change or re-render needed here usually, wait for AttackOccurred.
    // renderGame(gameState); // Avoid re-rendering unless specific UI effect requires it
}
export function handleDamageDealt(eventData) {
    // DTO: Assume DamageDealtEventDto { string? SourceUid, string TargetUid, int DamageAmount }
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
    if (eventData.DamageAmount >= 0) {
        // 确定伤害数字应该显示在哪个UI元素上
        // 【修复】直接传递目标单位的UID给UI函数
        // showDamageNumber 函数现在能够智能地根据UID找到正确的DOM元素
        showDamageNumber(target.uid, eventData.DamageAmount);
    }
}
export function handleHealthChanged(eventData) {
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


        // 【重构】调用统一的UI更新函数
        // 新的 updateHealthDisplay 函数能够智能处理主战者和场上单位
        updateHealthDisplay(unit);

        // Log if health drops to 0, but let the server's UnitDestroyed event handle the actual removal
        if (unit.currentHealth <= 0) {
        }
    } else {
        console.warn(`GM: HealthChanged event received for invalid unit UID: ${cardUid}`);
    }
}

export function handleMulliganStarted(eventData) {
    // DTO: MulliganStartedEventDto { MulliganTimer?: int } // Assuming PascalCase DTO
    gameState.gamePhase = "Mulligan";
    // UI should now allow mulligan actions. Timer handling (if any) would use eventData.MulliganTimer
    // Use PascalCase: MulliganTimer
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
        updateMulliganStatus(completedPlayerId, true);
        selectDualControlMulliganSeat();
        if (gameState.dualControl && gameState.phase === 'Mulligan' && typeof renderGame === "function") {
            renderGame(gameState);
        }
    } else {
        console.warn(`GM: PlayerMulliganCompleted event received for unknown player ID: ${completedPlayerId}`);
    }
}

export function handleHealApplied(eventData) {
    if (!eventData || !eventData.TargetUid || eventData.HealAmount === undefined) {
        console.error("GM Error: Invalid data in handleHealApplied:", eventData);
        return;
    }

    const targetUid = eventData.TargetUid;
    const target = findCardInstanceByUid(targetUid);

    if (!target) {
        console.warn(`GM: HealApplied event received for invalid target UID: ${targetUid}`);
        return;
    }

    if (eventData.HealAmount >= 0) {
        showHealNumber(target.uid, eventData.HealAmount);
    }
}
// --- NEW Refactored Event Handlers ---

export function handleCardPlayed(eventData) {
    // Zone movement is handled by CardMoved.
    if (!eventData || !eventData.SourcePlayerId || !eventData.PlayedCardInstance?.InstanceId) {
        console.error("GM Error: Invalid data in handleCardPlayed:", eventData);
        return;
    }

    // Placeholder for play-card visual effects.
}

export function handleCardSummoned(eventData) {
    // Zone movement is handled by CardMoved.
    // This event is reserved for summon visual effects.
    if (!eventData || !eventData.SourcePlayerId || !eventData.SummonedCard) {
        console.error("GM Error: Invalid data in handleCardSummoned:", eventData);
        return;
    }
}

// --- END NEW Refactored Event Handlers ---

export function handleGameOver(eventData) {
    // DTO: GameOverEventDto { WinnerId: string, Reason?: string } // Assuming PascalCase DTO
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

    appendSystemLog('提示：结算完成后，可返回主菜单右上角「元进度」面板刷新查看金币/任务。');
    const session = getUserSession();
    const maybeUserId = session?.mode === "user" ? session.userId : null;
    if (looksLikeGuid(maybeUserId)) {
        tryFetchMetaProgressionState(maybeUserId).catch(() => { });
    }
    renderGame(gameState); // Render final game over state
    refreshTurnTimerFromState();
    // Consider disabling all further interactions more forcefully
    // disablePlayerInteractions(); // REMOVED: Relying on existing logic per user preference
}

function appendSystemLog(message) {
    if (!message) return;
    const entry = formatLogEntry(message);
    if (!entry) return;
    gameState.gameLog.push(entry);
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

function generateRequestId() {
    return `req_${Date.now()}_${Math.random().toString(36).slice(2, 10)}`;
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

    if (!eventData || !eventData.TargetCardInstanceId) {
        console.error("GM Error: Invalid data in handleAttackStateUpdated:", eventData);
        return;
    }

    const unit = findCardInstanceByUid(eventData.TargetCardInstanceId);

    if (unit) {
        unit.canAttack = eventData.CanAttack;

        // Trigger a UI update for the specific unit to reflect the change in its attack-readiness (e.g., remove glow)
        updateUnitDisplay(unit);
        if (!unit.canAttack && gameState.selectedAttackerUid === unit.uid) {
            gameState.selectedAttackerUid = null;
        }
        renderHighlights(gameState);
    } else {
        console.warn(`GM: AttackStateUpdated event received for invalid unit UID: ${eventData.TargetCardInstanceId}`);
    }
}

export function handleActStateUpdated(eventData) {
    // DTO: ActStateUpdatedEventDto { TargetCardInstanceId: string, ActLastUsedTurnNumber: number }

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
    updateCardActionButtons(unit);
    refreshBattleCardPreviewIfCurrent(unit);
}

export function handleEvolveStateUpdated(eventData) {
    // DTO: PlayerEvolvedEventDto { PlayerId: string }

    if (!eventData || !eventData.SourcePlayerId) {
        console.error("GM Error: Invalid data in handleEvolveStateUpdated:", eventData);
        return;
    }

    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.canEvolve = eventData.CanEvolve ?? false;

        if (Array.isArray(playerToUpdate.field)) {
            playerToUpdate.field.forEach(card => {
                updateCardActionButtons(card);
                refreshBattleCardPreviewIfCurrent(card);
            });
        }
    } else {
        console.warn(`GM: EvolveStateUpdated event received for invalid player: ${eventData.SourcePlayerId}`);
    }
}

export function handleSuperEvolveStateUpdated(eventData) {

    if (!eventData || !eventData.SourcePlayerId) {
        console.error("GM Error: Invalid data in handleSuperEvolveStateUpdated:", eventData);
        return;
    }

    const playerToUpdate = getPlayerState(eventData.SourcePlayerId);
    if (playerToUpdate) {
        playerToUpdate.canSuperEvolve = eventData.CanSuperEvolve ?? false;
        if (Array.isArray(playerToUpdate.field)) {
            playerToUpdate.field.forEach(card => {
                updateCardActionButtons(card);
                refreshBattleCardPreviewIfCurrent(card);
            });
        }
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

    // BUG-FIX: The actual choice data is nested inside the 'choiceRequest' property.
    // C# PascalCase becomes camelCase in JS, so we look for 'choiceRequest'.
    // BUG-FIX: C# DTOs use PascalCase. Access the correct property from the event data.
    const choiceRequest = eventData.ChoiceRequest;

    // Further validation using PascalCase properties
    if (!choiceRequest || !choiceRequest.SourceAbilityRuntimeId || !choiceRequest.SelectableTargetIds) {
        console.error("GM Error: Invalid data in handlePlayerChoiceRequested. Missing nested ChoiceRequest object or its properties (SourceAbilityRuntimeId, SelectableTargetIds).", eventData);
        return;
    }

    const dedupeKey = `${choiceRequest.PendingChoiceId ?? ''}:${choiceRequest.SourceAbilityRuntimeId}`;
    if (dedupeKey === lastHandledChoiceRequestKey) {
        return;
    }
    lastHandledChoiceRequestKey = dedupeKey;

    // The primary role of this handler is to pass the choice request to the UI module.
    try {
        if (showPlayerChoiceUI) {
            // Pass the nested choiceRequest object to the UI function.
            showPlayerChoiceUI(choiceRequest);
        } else {
            console.error("GM Error: ui.js module does not have showPlayerChoiceUI function.");
        }
    } catch (error) {
        console.error("GM Error: Failed to load or call ui.js for PlayerChoiceRequested:", error);
    }
}

/**
 * Handles the CardRevealed event from the server.
 * This event is purely informational/visual and MUST NOT mutate hidden-zone sizes.
 */
export function handleCardRevealed(eventData) {

    const revealedCard = eventData?.RevealedCard ?? null;
    if (!revealedCard) {
        console.warn("GM: CardRevealed event missing RevealedCard payload.");
        return;
    }

    // Show a transient overlay; do not modify authoritative state.
    try {
        if (showRevealOverlay) {
            showRevealOverlay(revealedCard);
        }
    } catch (error) {
        console.error("GM Error: Failed to show reveal overlay:", error);
    }

    // Best-effort log entry.
    const name = revealedCard.Name ?? 'Unknown';
    const cost = Number.isFinite(revealedCard.Cost) ? revealedCard.Cost : '?';
    appendGameLog("Reveal", { name, cost, instanceId: revealedCard.InstanceId, cardId: revealedCard.CardId });
}

function readAbilityParameter(parameters, keys) {
    if (!parameters || typeof parameters !== 'object' || !Array.isArray(keys)) return null;

    for (const key of keys) {
        if (!key) continue;
        const value = parameters[key];
        if (value === undefined || value === null) continue;
        const normalized = String(value).trim();
        if (normalized.length > 0) {
            return normalized;
        }
    }

    return null;
}

export function handleAbilityTriggered(eventData) {
    const sourceCardUid = eventData?.SourceCardUid ?? eventData?.sourceCardUid ?? null;
    const sourcePlayerId = eventData?.SourcePlayerId ?? eventData?.sourcePlayerId ?? null;
    const sourceCard = sourceCardUid ? findCardInstanceByUid(sourceCardUid) : null;
    const localPlayerId = gameState.localPlayerId ?? null;
    const parameters = (eventData?.AbilityParameters && typeof eventData.AbilityParameters === 'object')
        ? eventData.AbilityParameters
        : ((eventData?.abilityParameters && typeof eventData.abilityParameters === 'object')
            ? eventData.abilityParameters
            : {});

    const abilityDescription = readAbilityParameter(parameters, [
        'ability_description',
        'AbilityDescription',
        'description',
        'Description'
    ]) || '';

    const sourceCardName = readAbilityParameter(parameters, [
        'source_card_name',
        'SourceCardName',
        'card_name',
        'CardName'
    ]) || String(sourceCard?.name ?? '').trim() || '能力触发';

    const abilityName = readAbilityParameter(parameters, [
        'ability_name',
        'AbilityName'
    ]) || '';

    const previewPath = readAbilityParameter(parameters, [
        'source_card_art_preview',
        'SourceCardArtPreview',
        'art_preview',
        'ArtPreview'
    ]) || String(sourceCard?.artPreview ?? sourceCard?.ArtPreview ?? '').trim();
    const ownerId = sourcePlayerId ?? sourceCard?.ownerId ?? sourceCard?.OwnerPlayerId ?? null;
    const tone = (localPlayerId && ownerId && String(ownerId) !== String(localPlayerId))
        ? 'enemy'
        : 'ally';

    try {
        if (showAbilityTriggerToast) {
            showAbilityTriggerToast({
                title: sourceCardName,
                subtitle: abilityName && abilityName !== sourceCardName ? abilityName : '',
                description: abilityDescription,
                previewPath,
                tone,
                sourcePlayerId,
                sourceCardUid
            });
        }
    } catch (error) {
        console.error("GM Error: Failed to show ability trigger toast:", error);
    }
}

export function handleFusion(eventData) {
    // DTO: FusionEventDto { SourcePlayerId, FusedCount, FusedCardInstanceIds }

    const sourcePlayerId = eventData?.SourcePlayerId ?? null;
    const fusedIds = Array.isArray(eventData?.FusedCardInstanceIds) ? eventData.FusedCardInstanceIds : [];
    const fusedCount = Number.isFinite(eventData?.FusedCount) ? eventData.FusedCount : fusedIds.length;

    if (!sourcePlayerId || fusedCount <= 0) return;

    const hasAnnouncement = Boolean(eventData?.Announcement || eventData?.announcement);
    if (!hasAnnouncement) {
        const who = sourcePlayerId === gameState.localPlayerId ? '你' : '对手';
        appendSystemLog(`${who}融合了${fusedCount}张卡`);
    }

    try {
        if (showFusionAnimation) {
            showFusionAnimation(sourcePlayerId, fusedIds);
        }
    } catch (error) {
        console.error("GM Error: Failed to show fusion animation:", error);
    }
}

function normalizeChatMessageDto(rawMessage) {
    if (!rawMessage || typeof rawMessage !== 'object') {
        return null;
    }

    const messageId = String(rawMessage.MessageId ?? rawMessage.messageId ?? '').trim();
    const senderTypeRaw = String(rawMessage.SenderType ?? rawMessage.senderType ?? '').trim().toLowerCase();
    const senderType = senderTypeRaw === 'spectator'
        ? 'spectator'
        : senderTypeRaw === 'system'
            ? 'system'
            : 'player';

    const senderId = String(rawMessage.SenderId ?? rawMessage.senderId ?? '').trim();
    const senderNameFallback = senderType === 'spectator' ? '观战者' : (senderType === 'system' ? '系统' : '玩家');
    const senderName = String(rawMessage.SenderName ?? rawMessage.senderName ?? senderNameFallback).trim() || senderNameFallback;
    const content = String(rawMessage.Content ?? rawMessage.content ?? '').trim();
    if (!content) {
        return null;
    }

    const sentAtUtc = String(rawMessage.SentAtUtc ?? rawMessage.sentAtUtc ?? '').trim();
    const clientMessageId = String(rawMessage.ClientMessageId ?? rawMessage.clientMessageId ?? '').trim();
    const localPlayerId = String(gameState.localPlayerId ?? '').trim();
    const senderIdLower = senderId.toLowerCase();
    const localPlayerIdLower = localPlayerId.toLowerCase();
    const isSelfBySenderId = senderType === 'player'
        && senderIdLower.length > 0
        && localPlayerIdLower.length > 0
        && senderIdLower === localPlayerIdLower;
    const isSelfByClientId = clientMessageId.length > 0 && pendingChatClientMessageIds.has(clientMessageId);
    const isSelf = isSelfBySenderId || isSelfByClientId;

    if (clientMessageId) {
        pendingChatClientMessageIds.delete(clientMessageId);
    }

    return {
        messageId,
        senderType,
        senderId,
        senderName,
        senderAvatarUrl: String(rawMessage.SenderAvatarUrl ?? rawMessage.senderAvatarUrl ?? '').trim(),
        content,
        sentAtUtc,
        clientMessageId,
        isSelf
    };
}

function pushChatMessageToState(message) {
    if (!message) return false;
    const messages = Array.isArray(gameState.chatMessages) ? gameState.chatMessages : [];
    if (message.messageId && messages.some(entry => entry?.messageId === message.messageId)) {
        return false;
    }
    messages.push(message);
    if (messages.length > CHAT_HISTORY_LIMIT) {
        messages.splice(0, messages.length - CHAT_HISTORY_LIMIT);
    }
    gameState.chatMessages = messages;
    return true;
}

export function handleChatHistory(eventData) {
    const rawMessages = Array.isArray(eventData?.Messages)
        ? eventData.Messages
        : (Array.isArray(eventData?.messages) ? eventData.messages : []);

    const normalizedMessages = [];
    for (const rawMessage of rawMessages) {
        const normalized = normalizeChatMessageDto(rawMessage);
        if (normalized) {
            normalizedMessages.push(normalized);
        }
    }

    if (normalizedMessages.length > CHAT_HISTORY_LIMIT) {
        normalizedMessages.splice(0, normalizedMessages.length - CHAT_HISTORY_LIMIT);
    }

    gameState.chatMessages = normalizedMessages;
    replaceInMatchChatHistory(gameState.chatMessages);
}

export function handleChatMessage(eventData) {
    const normalizedMessage = normalizeChatMessageDto(eventData);
    if (!normalizedMessage) {
        return;
    }

    const appended = pushChatMessageToState(normalizedMessage);
    if (!appended) {
        return;
    }

    const shouldPlayBarrage = normalizedMessage.senderType !== 'system';
    appendInMatchChatMessage(normalizedMessage, shouldPlayBarrage);
}

/**
 * Submits the player's choice back to the server.
 * @param {string} choiceId - The UID of the selected target.
 * @param {string} sourceCardUid - The UID of the card that initiated the choice.
 */
export async function submitPlayerChoice(pendingChoiceId, chosenTargetIds, sourceAbilityRuntimeId, wasCancelled = false) {
    const connection = getActiveConnection();
    if (!connection || connection.state !== "Connected") {
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
    try {
        await connection.invoke("SubmitTargetChoice", choiceDto);
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
    // To cancel, we submit a choice with the correct PendingChoiceId, an empty target array, and the WasCancelled flag set to true.
    return await submitPlayerChoice(pendingChoiceId, [], sourceAbilityRuntimeId, true);
}
