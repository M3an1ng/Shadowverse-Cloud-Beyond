// src/main.js - 主入口模块
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - API集成: 文档/03-前端架构/03-API集成.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
//
// 导入核心模块
import * as signalR from "@microsoft/signalr";
import { LogLevel } from "@microsoft/signalr"; // Added for Trace logging
import { router } from './router.js'; // 导入路由系统
import { clearUserSession, getDeckOwnerPayload, getDeckOwnerQuery, getUserSession, setUserSession, USER_SESSION_CHANGED_EVENT } from './userSession.js';
import {
    gameState, initializeGame, initializeSpectator, setSignalRConnection, setGuestSignalRConnection, updateFullGameState, endTurnRequest,
    // Import specific event handlers instead of processGameEvent
    handleDeckUpdate, handleTurnStarted, handleTurnEnded, handlePpUpdate, handleEpUpdate, handleSepUpdate, handleHandUpdated,
    handleGraveyardUpdate, handleAttackDeclared, handleUnitDestroyed, handleDamageDealt, handleHealApplied,
    handleCardSummoned, handleCardPlayed, handleHealthChanged, handleCardVanished,
    handleStatChange, handleUnitEvolved, handleGameOver, handlePlayerMulliganCompleted, handleAttackStateUpdated,
    handleActStateUpdated, handleEvolveStateUpdated, handleSuperEvolveStateUpdated, handleHandPlayabilityUpdate, handleActPlayabilityUpdate, handlePpMaxUpdate, handleEpMaxUpdate, handleSepMaxUpdate, handlePpBoostStateUpdated, handlePlayerChoiceRequested, handleCardRevealed, handleFusion, handleKeywordAdded, handleKeywordRemoved, handleCardInstanceUpdated, handleCardMoved, handleEmblemUpdated, handleCounterChanged, handleCardReturned, handleTuck, appendGameLog // 【新增】导入最大值更新处理函数
} from './gameManager.js';
// loadCards is obsolete now that the server is the single source of truth.
import { renderGame, setConnectionStatus, setOpponentConnectionStatus } from './ui.js'; // Removed non-existent displayError/Info/initializeUI, Added UI function imports - functions to be implemented in ui
import { initDevConsole } from './devConsole.js'; // 开发者控制台
import { ensureEnumLocalizations } from './enumLocalizationCache.js';
// import { endTurn } from './turnManager.js'; // Keep commented out
// Old SignalR setup code removed. New setup will be added later.
// --- End SignalR Setup ---

// Simple GUID generator (RFC4122 v4 simulation)
function generateGuid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

// GUID validation function
function isValidGuid(str) {
    if (!str || typeof str !== 'string') {
        return false;
    }
    // RFC4122 GUID format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
    const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    return guidRegex.test(str);
}

let isInitializing = false;
const HUB_URL = "/gamehub"; // Use relative URL, assumes same origin or proxy configured
let connection = null; // Module-level variable for SignalR connection - Moved here
let guestConnection = null;
let initialStateReceived = false; // Flag to track if initial state has been handled
let forceFullStateOnNextReceive = false;
let reconnectInProgress = false;
let manualReconnectInProgress = false;
let lobbyHandshake = null; // { gameId, playerId, token, joinCode?, role, useAiFallback?, dualControl?, hostPlayerId?, hostToken?, guestPlayerId?, guestToken? }
let pendingHandshake = null; // Set by multiplayer UI before initialization
const DEBUG_FLAG = isDebugModeEnabled();
let DEBUG_CONSOLE_ENABLED = DEBUG_FLAG;
let DEBUG_CONSOLE_AUTOSHOW = false;

function isDebugModeEnabled() {
    try {
        const fromQuery = new URLSearchParams(window.location.search).get('debug');
        const fromStorage = localStorage.getItem('svb.debug') || localStorage.getItem('svb.debug.console');
        return fromQuery === '1' || fromQuery === 'true' || fromStorage === 'true' || fromStorage === '1';
    } catch (err) {
        console.warn('DEBUG flag detection failed, defaulting to disabled.', err);
        return false;
    }
}

async function apiPostJson(url, body) {
    const response = await fetch(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(body ?? {})
    });
    let payload = null;
    try {
        payload = await response.json();
    } catch (err) {
        // ignore parse errors; fall back to status text
    }
    if (!response.ok) {
        const message = (payload && (payload.message || payload.Message)) || response.statusText;
        throw new Error(message || `HTTP ${response.status}`);
    }
    return payload;
}

async function apiGetJson(url) {
    const response = await fetch(url, { method: "GET" });
    let payload = null;
    try {
        payload = await response.json();
    } catch (err) {
        // ignore
    }
    if (!response.ok) {
        const message = (payload && (payload.message || payload.Message)) || response.statusText;
        throw new Error(message || `HTTP ${response.status}`);
    }
    return payload;
}

async function bootstrapDecks() {
    const query = getDeckOwnerQuery();
    try {
        return await apiPostJson(`/api/decks/bootstrap?${query}`, {});
    } catch (err) {
        console.warn("MAIN: Deck bootstrap failed, fallback to list.", err);
        return null;
    }
}

async function listDecks() {
    const query = getDeckOwnerQuery();
    return await apiGetJson(`/api/decks?${query}`);
}

const deckSelectionState = {
    decks: [],
    loaded: false,
    loading: null,
    singleplayerMode: 'ai',
    selected: {
        multiplayer: null,
        singleplayer: null,
        ai: null
    }
};

const CARD_CLASS_LABELS = {
    neutral: '中立',
    forestcraft: '精灵',
    swordcraft: '皇家护卫',
    runecraft: '巫师',
    dragoncraft: '龙族',
    abysscraft: '梦魇',
    havencraft: '主教',
    portalcraft: '复仇者'
};

function normalizeDeckId(value) {
    const text = String(value ?? '').trim();
    return text.length > 0 ? text : null;
}

function resolveDeckId(deck) {
    return deck?.deckId || deck?.id || deck?.DeckId || null;
}

function getDeckClassLabel(value) {
    const normalized = String(value ?? '').trim().toLowerCase();
    return CARD_CLASS_LABELS[normalized] || value || '未知职业';
}

function formatDeckLabel(deck) {
    const name = deck?.name || '未命名卡组';
    const deckClass = getDeckClassLabel(deck?.class ?? deck?.Class);
    const count = deck?.cardCount ?? 0;
    return `${name} · ${deckClass} · ${count}/40`;
}

async function ensureDecksLoaded() {
    if (deckSelectionState.loading) {
        return deckSelectionState.loading;
    }
    if (deckSelectionState.loaded) {
        return deckSelectionState.decks;
    }

    deckSelectionState.loading = (async () => {
        await bootstrapDecks();
        const decks = await listDecks();
        deckSelectionState.decks = Array.isArray(decks) ? decks : [];
        deckSelectionState.loaded = true;
        deckSelectionState.loading = null;
        return deckSelectionState.decks;
    })();

    return deckSelectionState.loading;
}

function applyDeckOptions(selectEl, key, outputEl) {
    if (!selectEl) return;
    const decks = deckSelectionState.decks;
    if (!decks.length) {
        selectEl.innerHTML = '<option value="">没有可用卡组</option>';
        selectEl.disabled = true;
        if (outputEl) {
            outputEl.textContent = '没有可用卡组，请先在牌组编辑中创建。';
            outputEl.style.color = '#ff4f00';
        }
        return;
    }

    selectEl.disabled = false;
    selectEl.innerHTML = [
        '<option value="">请选择卡组</option>',
        ...decks.map(deck => {
            const deckId = resolveDeckId(deck);
            const label = formatDeckLabel(deck);
            return `<option value="${deckId}">${label}</option>`;
        })
    ].join('');

    const selected = normalizeDeckId(deckSelectionState.selected[key]);
    const fallback = resolveDeckId(decks[0]);
    const resolved = decks.some(deck => resolveDeckId(deck) === selected) ? selected : fallback;
    deckSelectionState.selected[key] = resolved;
    selectEl.value = resolved || '';
    if (outputEl) {
        outputEl.textContent = '';
        outputEl.style.color = '';
    }
}

function getDeckIndexById(deckId) {
    const normalized = normalizeDeckId(deckId);
    if (!normalized) return -1;
    return deckSelectionState.decks.findIndex(deck => resolveDeckId(deck) === normalized);
}

function updateMultiplayerDeckSwitcher() {
    const currentEl = document.getElementById('mp-deck-current');
    const prevBtn = document.getElementById('mp-deck-prev');
    const nextBtn = document.getElementById('mp-deck-next');
    if (!currentEl) return;

    const decks = deckSelectionState.decks;
    if (!decks.length) {
        currentEl.textContent = '没有可用卡组';
        if (prevBtn) prevBtn.disabled = true;
        if (nextBtn) nextBtn.disabled = true;
        return;
    }

    const selectedId = normalizeDeckId(deckSelectionState.selected.multiplayer);
    const resolvedIndex = getDeckIndexById(selectedId);
    const index = resolvedIndex >= 0 ? resolvedIndex : 0;
    const deck = decks[index];
    deckSelectionState.selected.multiplayer = resolveDeckId(deck);
    currentEl.textContent = formatDeckLabel(deck);

    const disableNav = decks.length <= 1;
    if (prevBtn) prevBtn.disabled = disableNav;
    if (nextBtn) nextBtn.disabled = disableNav;
}

function updateSingleplayerDeckSwitcher() {
    const currentEl = document.getElementById('sp-deck-current');
    const prevBtn = document.getElementById('sp-deck-prev');
    const nextBtn = document.getElementById('sp-deck-next');
    if (!currentEl) return;

    const decks = deckSelectionState.decks;
    if (!decks.length) {
        currentEl.textContent = '没有可用卡组';
        if (prevBtn) prevBtn.disabled = true;
        if (nextBtn) nextBtn.disabled = true;
        return;
    }

    const selectedId = normalizeDeckId(deckSelectionState.selected.singleplayer);
    const resolvedIndex = getDeckIndexById(selectedId);
    const index = resolvedIndex >= 0 ? resolvedIndex : 0;
    const deck = decks[index];
    deckSelectionState.selected.singleplayer = resolveDeckId(deck);
    currentEl.textContent = formatDeckLabel(deck);

    const disableNav = decks.length <= 1;
    if (prevBtn) prevBtn.disabled = disableNav;
    if (nextBtn) nextBtn.disabled = disableNav;
}

async function refreshDeckSelectionUI() {
    try {
        await ensureDecksLoaded();
        updateMultiplayerDeckSwitcher();
        const mpOutput = document.getElementById('mp-deck-output');
        if (mpOutput) {
            if (!deckSelectionState.decks.length) {
                mpOutput.textContent = '没有可用卡组，请先在牌组编辑中创建。';
                mpOutput.style.color = '#ff4f00';
            } else {
                mpOutput.textContent = '';
                mpOutput.style.color = '';
            }
        }
        updateSingleplayerDeckSwitcher();
        applyDeckOptions(document.getElementById('sp-ai-deck'), 'ai', document.getElementById('sp-deck-output'));
    } catch (err) {
        const message = err?.message || String(err);
        const outputEls = [
            document.getElementById('mp-deck-output'),
            document.getElementById('sp-deck-output')
        ];
        outputEls.forEach(el => {
            if (!el) return;
            el.textContent = `卡组加载失败：${message}`;
            el.style.color = '#ff4f00';
        });
    }
}

async function submitDeckSelection(handshake, options) {
    const deckId = normalizeDeckId(options?.deckId);
    const aiDeckId = normalizeDeckId(options?.aiDeckId);
    const requireAi = options?.requireAi === true;

    if (!deckId) {
        return { ok: false, message: "请先选择卡组。" };
    }
    if (requireAi && !aiDeckId) {
        return { ok: false, message: "请先选择 AI 卡组。" };
    }

    const ownerPayload = getDeckOwnerPayload();
    const payload = {
        playerId: handshake.playerId,
        token: handshake.token,
        deckId,
        ...ownerPayload
    };

    if (aiDeckId) {
        payload.aiDeckId = aiDeckId;
        if (ownerPayload.userId) {
            payload.aiUserId = ownerPayload.userId;
        } else {
            payload.aiGuestId = ownerPayload.guestId;
        }
    }

    await apiPostJson(`/api/match/${encodeURIComponent(handshake.gameId)}/deck`, payload);
    return { ok: true, deckId };
}

function cacheHandshake(handshake) {
    lobbyHandshake = handshake;
    pendingHandshake = handshake;
    try {
        localStorage.setItem("svb.lobby.handshake", JSON.stringify(handshake));
    } catch (err) {
        console.warn("Failed to cache handshake", err);
    }
}

function loadCachedHandshake() {
    if (lobbyHandshake) return lobbyHandshake;
    try {
        const raw = localStorage.getItem("svb.lobby.handshake");
        if (raw) {
            const parsed = JSON.parse(raw);
            if (parsed.gameId && parsed.playerId && parsed.token) {
                lobbyHandshake = parsed;
                return parsed;
            }
        }
    } catch (err) {
        console.warn("Failed to load cached handshake", err);
    }
    return null;
}

function clearCachedHandshake() {
    lobbyHandshake = null;
    pendingHandshake = null;
    try {
        localStorage.removeItem("svb.lobby.handshake");
    } catch (err) {
        console.warn("Failed to clear cached handshake", err);
    }
}

const TRANSIENT_MATCH_QUERY_KEYS = [
    'spectate',
    'gameId',
    'playerId',
    'token',
    'joinCode',
    'useAiFallback'
];

function clearTransientMatchQueryParams() {
    try {
        const params = new URLSearchParams(window.location.search);
        let changed = false;
        for (const key of TRANSIENT_MATCH_QUERY_KEYS) {
            if (params.has(key)) {
                params.delete(key);
                changed = true;
            }
        }

        if (!changed) return;

        const query = params.toString();
        const nextUrl = `${window.location.pathname}${query ? `?${query}` : ''}${window.location.hash || ''}`;
        window.history.replaceState(window.history.state, '', nextUrl);
    } catch (err) {
        console.warn("Failed to clear transient match query params", err);
    }
}

function resetTransientConnectionUiState() {
    reconnectInProgress = false;
    manualReconnectInProgress = false;
    setConnectionStatus('connected');
    setOpponentConnectionStatus(true);
}

async function createMatch(useAiFallback) {
    const session = getUserSession();
    const payload = await apiPostJson("/api/match", {
        useAiFallback,
        hostPlayerId: session.userId || null
    });
    return {
        gameId: payload.gameId,
        playerId: payload.hostPlayerId,
        token: payload.hostToken,
        joinCode: payload.joinCode,
        role: "host",
        useAiFallback: payload.useAiFallback
    };
}

async function joinMatch(gameId, joinCode, options = {}) {
    const session = getUserSession();
    const resolvedPlayerId = options.playerId === undefined ? (session.userId || null) : options.playerId;
    const payload = await apiPostJson(`/api/match/${encodeURIComponent(gameId)}/join`, {
        joinCode,
        playerId: resolvedPlayerId
    });
    return {
        gameId: payload.gameId,
        playerId: payload.guestPlayerId,
        token: payload.guestToken,
        joinCode: payload.joinCode,
        role: "guest",
        useAiFallback: payload.useAiFallback
    };
}

async function markReady(gameId, playerId, token) {
    await apiPostJson(`/api/match/${encodeURIComponent(gameId)}/ready`, { playerId, token });
}

async function startMatch(gameId, playerId, token) {
    await apiPostJson(`/api/match/${encodeURIComponent(gameId)}/start`, { playerId, token });
}

async function getMatch(gameId) {
    return await apiGetJson(`/api/match/${encodeURIComponent(gameId)}`);
}

async function leaveMatch(gameId, playerId, token) {
    await apiPostJson(`/api/match/${encodeURIComponent(gameId)}/leave`, { playerId, token });
}

async function leaveCurrentPvpRoom(reasonForLog) {
    const handshake = pendingHandshake || lobbyHandshake || loadCachedHandshake();
    if (!handshake || handshake.useAiFallback === true) {
        clearCachedHandshake();
        return;
    }

    // Clear local state first to avoid stale handshake reuse during navigation.
    const { gameId, playerId, token } = handshake;
    clearCachedHandshake();

    try {
        await leaveMatch(gameId, playerId, token);
    } catch (err) {
        console.warn("MAIN: Leave match failed (best-effort).", err);
    }
}

// --- SignalR Connection Logic ---

/**
 * Sets up and starts the SignalR connection.
 * @param {string} gameId - The ID of the game.
 * @param {string} playerId - The ID of the player.
 * @param {string} token - Lobby token for hub validation.
 * @param {function} resolveInitialState - The resolve function for the initialStatePromise.
 * @returns {Promise<signalR.HubConnection>} A promise that resolves with the connection object, or rejects on failure.
 */
async function setupSignalRConnection(gameId, playerId, token, resolveInitialState, options = {}) { // ADDED token parameter
    if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
        console.warn("MAIN: SignalR connection already exists and is not disconnected. Stopping existing one.");
        await connection.stop();
    }

    // Reset initial-state gate for every new connection/session
    if (!options.skipInitialReset) {
        initialStateReceived = false;
    }

    const hubUrlWithParams = `${HUB_URL}?gameId=${encodeURIComponent(gameId)}&playerId=${encodeURIComponent(playerId)}&token=${encodeURIComponent(token)}`;

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrlWithParams)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Trace) // Changed level to Trace
        .build();

    registerSignalREventHandlers(connection, resolveInitialState); // Pass resolver down

    try {
        await connection.start();
        return connection;
    } catch (err) {
        console.error("MAIN: SignalR Connection Failed: ", err);
        alert(`连接游戏服务器失败: ${err.message || 'Unknown error'}`); // Use alert instead of displayError
        connection = null;
        throw err;
    }
}

async function setupActionSignalRConnection(gameId, playerId, token) {
    if (!playerId || !token) return null;

    if (guestConnection && guestConnection.state !== signalR.HubConnectionState.Disconnected) {
        console.warn("MAIN: Guest SignalR connection already exists and is not disconnected. Stopping existing one.");
        await guestConnection.stop();
    }

    const hubUrlWithParams = `${HUB_URL}?gameId=${encodeURIComponent(gameId)}&playerId=${encodeURIComponent(playerId)}&token=${encodeURIComponent(token)}`;

    guestConnection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrlWithParams)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    registerActionSignalREventHandlers(guestConnection);

    try {
        await guestConnection.start();
        return guestConnection;
    } catch (err) {
        console.error("MAIN: Guest SignalR Connection Failed: ", err);
        guestConnection = null;
        throw err;
    }
}

function registerActionSignalREventHandlers(conn) {
    if (!conn) return;

    let lastChoiceRequestKey = null;

    // Dual-control guest connection must receive player-scoped choice events,
    // otherwise the selected seat cannot submit a decision and the game stalls.
    conn.on("PlayerChoiceRequested", (eventData) => {
        const choiceRequest = eventData?.ChoiceRequest;
        const pendingChoiceId = choiceRequest?.PendingChoiceId;
        const sourceAbilityRuntimeId = choiceRequest?.SourceAbilityRuntimeId;
        const requestKey = pendingChoiceId && sourceAbilityRuntimeId
            ? `${pendingChoiceId}:${sourceAbilityRuntimeId}`
            : null;

        if (requestKey && requestKey === lastChoiceRequestKey) {
            return;
        }
        lastChoiceRequestKey = requestKey;

        handlePlayerChoiceRequested(eventData);
    });
}

async function setupSpectatorSignalRConnection(gameId, resolveInitialState, options = {}) {
    if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
        console.warn("MAIN: SignalR connection already exists and is not disconnected. Stopping existing one.");
        await connection.stop();
    }

    if (!options.skipInitialReset) {
        initialStateReceived = false;
    }

    const hubUrlWithParams = `${HUB_URL}?gameId=${encodeURIComponent(gameId)}&spectate=1`;

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrlWithParams)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Trace)
        .build();

    registerSignalREventHandlers(connection, resolveInitialState);

    try {
        await connection.start();
        return connection;
    } catch (err) {
        console.error("MAIN: SignalR Connection Failed (spectate): ", err);
        alert(`连接游戏服务器失败: ${err.message || 'Unknown error'}`);
        connection = null;
        throw err;
    }
}

/**
 * Registers handlers for events received from the SignalR Hub.
 * @param {signalR.HubConnection} conn - The SignalR connection instance.
 */
function registerSignalREventHandlers(conn, resolveInitialState) { // Accept resolver as parameter
    const EVENT_STREAM_NAMES = [
        "TurnStarted",
        "TurnEnded",
        "DeckUpdate",
        "PpUpdate",
        "EpUpdate",
        "PpMaxUpdate",
        "EpMaxUpdate",
        "PpBoostStateUpdated",
        "GraveyardUpdate",
        "EmblemUpdated",
        "CardMoved",
        "CardPlayed",
        "CardSummoned",
        "AttackDeclared",
        "UnitDestroyed",
        "CardVanished",
        "CardReturned",
        "Tuck",
        "DamageDealt",
        "HealApplied",
        "HealthChanged",
        "StatChange",
        "KeywordAdded",
        "KeywordRemoved",
        "CounterChanged",
        "UnitEvolved",
        "HandUpdated",
        "PlayerMulliganCompleted",
        "GameOver",
        "AttackStateUpdated",
        "ReceiveHandPlayabilityUpdate",
        "ReceiveActPlayabilityUpdate",
        "EvolveStateUpdated",
        "PlayerChoiceRequested",
        "LogEvent"
    ];

    const wireEvent = (eventName, handler, logMapper) => {
        conn.on(eventName, (eventData) => {
            if (EVENT_STREAM_NAMES.includes(eventName)) {
                const payloadForLog = typeof logMapper === 'function' ? logMapper(eventData) : eventData;
                appendGameLog(eventName, payloadForLog);
            }
            handler(eventData);
        });
    };

    // Handler for full game state updates (e.g., on connect/reconnect)
    // --- MODIFIED FOR M6 ---
    conn.on("ReceiveGameState", (receivedGameState) => {
        const timestamp = new Date().toISOString();
        if (!initialStateReceived && typeof resolveInitialState === 'function') {
            // Initial state handling during initializeApp
            setConnectionStatus('connected');
            resolveInitialState(receivedGameState); // Resolve the promise in initializeApp
            initialStateReceived = true; // Mark initial state as received
            // NOTE: updateFullGameState is called in initializeApp after Promise.all resolves based on this promise.
        } else if (initialStateReceived) {
            // Optimization:
            // During Mulligan, the server may broadcast full GameState snapshots on opponent actions.
            // Applying the full snapshot forces a full UI re-render and can disrupt local interaction state.
            // We rely on incremental events (HandUpdated / PlayerMulliganCompleted) during Mulligan instead.
            if (!forceFullStateOnNextReceive && gameState.phase === 'Mulligan' && receivedGameState?.Phase === 'Mulligan') {
                return;
            }
            forceFullStateOnNextReceive = false;

            // Subsequent state handling after initialization
            // Directly call gameManager's update function to handle the state and re-render
            // Ensure gameManager and updateFullGameState are imported correctly
            try {
                setConnectionStatus('connected');
                updateFullGameState(receivedGameState); // Directly update the game state and UI
            } catch (error) { // Catch for outer try (Line 98)
                console.error("MAIN: Error calling updateFullGameState for subsequent update:", error);
                alert("处理游戏状态更新时出错！");
            } // Closes outer catch
        } else {
            // This case should ideally not happen if setupSignalRConnection is always called with a resolver during init
            console.error("MAIN Error: Received game state but initialization seems incomplete or resolver missing!");
            alert("内部错误：无法正确处理游戏状态！");
        }
    });
    // --- END M6 MODIFICATION ---

    // Handler for incremental game events
    // Register specific handlers for different event types from IGameClient.cs
    // For now, all delegate to processGameEvent in gameManager, which needs implementation.
    // Call specific handlers from gameManager.js for each event type
    wireEvent("TurnStarted", handleTurnStarted);
    wireEvent("TurnEnded", handleTurnEnded);
    wireEvent("DeckUpdate", handleDeckUpdate); // Restored handler registration
    wireEvent("PpUpdate", handlePpUpdate);
    wireEvent("EpUpdate", handleEpUpdate); // Renamed from EvolvePointUpdate to match backend DTO/Interface
    wireEvent("SepUpdate", handleSepUpdate);
    // 新增：最大值更新事件监听
    wireEvent("PpMaxUpdate", handlePpMaxUpdate);
    wireEvent("EpMaxUpdate", handleEpMaxUpdate);
    wireEvent("SepMaxUpdate", handleSepMaxUpdate);
    wireEvent("PpBoostStateUpdated", handlePpBoostStateUpdated);

    // Removed conn.on("HandUpdate", ...) block as it's deprecated
    wireEvent("GraveyardUpdate", handleGraveyardUpdate);
    wireEvent("EmblemUpdated", handleEmblemUpdated);
    wireEvent("CardPlayed", handleCardPlayed);
    wireEvent("CardSummoned", handleCardSummoned);
    wireEvent("AttackDeclared", handleAttackDeclared); // Renamed from UnitAttacked to match backend DTO/Interface
    wireEvent("UnitDestroyed", handleUnitDestroyed);
    wireEvent("CardVanished", handleCardVanished);
    wireEvent("CardReturned", handleCardReturned);
    wireEvent("Tuck", handleTuck);
    wireEvent("DamageDealt", handleDamageDealt);
    wireEvent("HealApplied", handleHealApplied);
    wireEvent("HealthChanged", handleHealthChanged); // Added handler
    wireEvent("StatChange", handleStatChange); // Added handler
    wireEvent("KeywordAdded", handleKeywordAdded);
    wireEvent("KeywordRemoved", handleKeywordRemoved);
    wireEvent("CounterChanged", handleCounterChanged);
    wireEvent("UnitEvolved", handleUnitEvolved); // Added handler (was missing)
    wireEvent("HandUpdated", handleHandUpdated);
    wireEvent("CardInstanceUpdated", handleCardInstanceUpdated);
    wireEvent("CardMoved", handleCardMoved);
    wireEvent("PlayerMulliganCompleted", handlePlayerMulliganCompleted);
    wireEvent("GameOver", handleGameOver);
    wireEvent("AttackStateUpdated", handleAttackStateUpdated);
    wireEvent("ActStateUpdated", handleActStateUpdated);
    // 【BUG修复】注册处理手牌可玩性更新的事件监听器
    wireEvent("ReceiveHandPlayabilityUpdate", handleHandPlayabilityUpdate);
    wireEvent("ReceiveActPlayabilityUpdate", handleActPlayabilityUpdate);
    wireEvent("EvolveStateUpdated", handleEvolveStateUpdated);
    wireEvent("SuperEvolveStateUpdated", handleSuperEvolveStateUpdated);
    // NEW: Handler for when the server requests the player to make a choice
    wireEvent("PlayerChoiceRequested", handlePlayerChoiceRequested);
    wireEvent("CardRevealed", handleCardRevealed);
    wireEvent("Fusion", handleFusion);
    // Generic log entries (for events without specific DTO)
    conn.on("LogEvent", (eventData) => {
        appendGameLog("LogEvent", eventData);
    });

    // [COMPATIBILITY FIX] Add a handler for the obsolete 'receivegameevent' to suppress warnings.
    // The server should be updated to stop sending this event.
    conn.on("receivegameevent", (eventData) => {
        console.warn("[DEPRECATED] Received obsolete 'receivegameevent'. Server should be updated to use specific event handlers. Data:", eventData);
    });

    // The generic ReceiveGameEvent handler is implicitly removed by replacing this whole block.

    // Handler for server-side errors
    conn.on("ReceiveError", (errorMessage) => {
        const message = typeof errorMessage === 'string' ? errorMessage : String(errorMessage ?? '');
        console.error("MAIN: Received Error from Server:", message);

        if (message.startsWith("ROOM_CLOSED:")) {
            const reason = message.slice("ROOM_CLOSED:".length).trim() || "房间已关闭。";
            alert(reason);
            clearCachedHandshake();
            try {
                router.navigateTo('navigation');
            } catch (err) {
                // ignore navigation errors during teardown
            }
            return;
        }

        alert(`服务器错误: ${message}`); // Use alert instead of displayError
    });

    conn.on("PlayerConnectionStatusUpdated", (eventData) => {
        const playerId = eventData?.PlayerId || eventData?.playerId;
        const isConnected = eventData?.IsConnected ?? eventData?.isConnected;
        if (!playerId || typeof isConnected !== 'boolean') {
            return;
        }
        if (!gameState.localPlayerId || playerId === gameState.localPlayerId) {
            return;
        }
        setOpponentConnectionStatus(isConnected);
    });

    // Handler for when the server requests the client to select target(s)
    conn.on("RequestTargetSelection", (request) => {
        // Removed call to showTargetSelection, which is not exported/used.
    });

    // --- Connection Lifecycle Handlers ---
    conn.onclose((error) => {
        console.error(`MAIN: SignalR Connection Closed. Error: "${error}". State: ${conn.state}.`);
        if (manualReconnectInProgress) {
            connection = null;
            setSignalRConnection(null);
            return;
        }
        alert("与服务器连接已断开。" + (error ? ` 原因: ${error.message}` : "")); // Use alert instead of displayError
        // Automatic reconnect is enabled, but we might want to disable UI elements here.
        connection = null; // Clear the connection variable
        reconnectInProgress = false;
        setSignalRConnection(null);
        setConnectionStatus('disconnected', '连接已断开');
    });

    conn.onreconnecting((error) => {
        console.warn("MAIN: SignalR Connection Reconnecting...", error);
        alert("连接中断，正在尝试重新连接服务器..."); // Use alert instead of displayInfo
        // Disable UI interactions while reconnecting?
        reconnectInProgress = true;
        setConnectionStatus('reconnecting', '连接中断，正在重连...');
    });

    conn.onreconnected((connectionId) => {
        alert("已重新连接到服务器。"); // Use alert instead of displayInfo
        forceFullStateOnNextReceive = true;
        reconnectInProgress = false;
        setSignalRConnection(conn);
        setConnectionStatus('reconnecting', '连接已恢复，正在同步...');
        requestFullStateSync("auto-reconnect");
        // Request latest state after reconnection if server doesn't push automatically
        // connection.invoke("RequestInitialGameState").catch(err => console.error("Request state after reconnect failed:", err));
    });

}

async function requestFullStateSync(reason) {
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
        console.warn("MAIN: Skip full state sync (connection not ready).", reason || "");
        return;
    }
    try {
        await connection.invoke("RequestInitialGameState");
    } catch (err) {
        console.error("MAIN: RequestInitialGameState failed:", err);
    }
}

/**
 * Responds to a target selection request from the server.
 * Called by ui.js after the user makes a selection.
 * @param {string} requestId - The unique ID of the selection request.
 * @param {string[]} selectedIds - An array of selected target UIDs.
 */
export async function respondToTargetSelection(requestId, selectedIds) {
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
        console.error("MAIN: SignalR connection not available for RespondToTargetSelection");
        alert("连接已断开，无法响应目标选择。"); // Use alert instead of displayError
        return;
    }
    const responseDto = {
        RequestId: requestId, // Changed from requestId
        SelectedTargetIds: selectedIds, // Changed from selectedTargetIds
    };
    try {
        await connection.invoke("RespondToTargetSelection", responseDto); // Assuming Hub method exists
    } catch (err) {
        console.error("MAIN: RespondToTargetSelection invocation failed:", err);
        alert(`发送目标选择失败: ${err.message}`); // Use alert instead of displayError
    }
}

// --- End SignalR Connection Logic ---

// --- Application Initialization ---

async function ensureLobbyHandshake() {
    // 0) Pending handshake set by multiplayer UI
    if (pendingHandshake && pendingHandshake.gameId && pendingHandshake.playerId && pendingHandshake.token) {
        cacheHandshake(pendingHandshake);
        return pendingHandshake;
    }

    // 1) URL parameters (deep link)
    const urlParams = new URLSearchParams(window.location.search);
    const urlGameId = urlParams.get("gameId");
    const urlSpectate = urlParams.get("spectate");
    const urlPlayerId = urlParams.get("playerId");
    const urlToken = urlParams.get("token");
    if (urlGameId && (urlSpectate === "1" || urlSpectate === "true")) {
        return {
            gameId: urlGameId,
            role: "spectator"
        };
    }
    if (urlGameId && urlPlayerId && urlToken) {
        const result = {
            gameId: urlGameId,
            playerId: urlPlayerId,
            token: urlToken,
            role: "url",
            joinCode: urlParams.get("joinCode") || undefined,
            useAiFallback: urlParams.get("useAiFallback") === "true"
        };
        cacheHandshake(result);
        return result;
    }

    // 2) Cached handshake (refresh/reconnect)
    const cached = loadCachedHandshake();
    // Avoid reusing multiplayer room when user is entering single-player (game route) from main menu.
    // Multiplayer rooms have useAiFallback=false; single-player should auto-create a fresh AI room.
    const isInMultiplayerUi = document.body.classList.contains('show-multiplayer');
    if (cached && (isInMultiplayerUi || cached.useAiFallback === true || cached.dualControl === true)) {
        return cached;
    }

    throw new Error("请先在单人对战或多人对战面板选择卡组并开始。");
}

function getReconnectHandshake() {
    return pendingHandshake || lobbyHandshake || loadCachedHandshake();
}

async function performManualReconnect() {
    if (reconnectInProgress) {
        return;
    }

    const handshake = getReconnectHandshake();
    if (!handshake || !handshake.gameId) {
        alert("没有可用的对战信息，无法重连。");
        setConnectionStatus('disconnected', '连接已断开');
        return;
    }

    reconnectInProgress = true;
    manualReconnectInProgress = true;
    setConnectionStatus('reconnecting', '正在重连...');

    try {
        if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
            await connection.stop();
        }
        if (guestConnection && guestConnection.state !== signalR.HubConnectionState.Disconnected) {
            await guestConnection.stop();
        }

        connection = null;
        guestConnection = null;
        setSignalRConnection(null);
        setGuestSignalRConnection(null);

        initialStateReceived = true;
        forceFullStateOnNextReceive = true;

        const connPromise = handshake.role === "spectator"
            ? setupSpectatorSignalRConnection(handshake.gameId, null, { skipInitialReset: true })
            : setupSignalRConnection(handshake.gameId, handshake.playerId, handshake.token, null, { skipInitialReset: true });

        const newConnection = await connPromise;
        setSignalRConnection(newConnection);

        if (handshake.role !== "spectator" && handshake.dualControl && handshake.guestPlayerId && handshake.guestToken) {
            const newGuestConnection = await setupActionSignalRConnection(
                handshake.gameId,
                handshake.guestPlayerId,
                handshake.guestToken
            );
            setGuestSignalRConnection(newGuestConnection);
        }

        await requestFullStateSync("manual-reconnect");
    } catch (err) {
        console.error("MAIN: Manual reconnect failed:", err);
        setConnectionStatus('disconnected', '重连失败，请重试');
        alert(`重连失败：${err?.message || '未知错误'}`);
    } finally {
        reconnectInProgress = false;
        manualReconnectInProgress = false;
    }
}

/**
 * Initializes the application, loads data, connects to SignalR, and sets up the UI.
 */
async function initializeApp() {
    if (isInitializing) {
        console.warn("MAIN: Initialization already in progress.");
        return;
    }
    isInitializing = true;

    try {
        // 0. Initialize basic UI elements (maybe show loading indicator)
        // initializeUI(); // REMOVED: ui.js does not export this. UI setup might need review later.

        // --- M6 Coordination Logic Start ---

        // 2. Get Game and Player IDs via lobby handshake (BEFORE starting async ops)
        const handshake = await ensureLobbyHandshake();
        const gameId = handshake.gameId;
        const playerId = handshake.playerId;
        const token = handshake.token;

        // 3. Render initial empty/loading state (optional)
        // renderGame(createEmptyGameState());

        // Create a promise that resolves when the initial game state is received
        let resolveInitialStatePromise;
        const initialStatePromise = new Promise(resolve => {
            resolveInitialStatePromise = resolve;
        });

        // 4. Start SignalR connection setup (PASSING the resolver)
        const connectionPromise = handshake.role === "spectator"
            ? setupSpectatorSignalRConnection(gameId, resolveInitialStatePromise)
            : setupSignalRConnection(gameId, playerId, token, resolveInitialStatePromise); // Pass resolver + token

        const guestConnectionPromise = (handshake.role === "spectator" || !handshake.dualControl)
            ? Promise.resolve(null)
            : setupActionSignalRConnection(gameId, handshake.guestPlayerId, handshake.guestToken);

        // 1. Start loading card data concurrently
        try {
            // Wait for SignalR connection and the initial state reception
            // connectionPromise ensures we handle setup errors even if initialStatePromise never resolves/rejects
            // We need the connection object resolved from connectionPromise to pass to gameManager
            const [serverState, currentConnection, currentGuestConnection] = await Promise.all([
                initialStatePromise,
                connectionPromise,
                guestConnectionPromise
            ]);
            // serverState is from initialStatePromise resolver, currentConnection from connectionPromise resolver


            // 5. Pass connection to gameManager (AFTER connection is confirmed ready)
            setSignalRConnection(currentConnection);
            if (currentGuestConnection) {
                setGuestSignalRConnection(currentGuestConnection);
            }

            // 6. Initialize gameManager with context (AFTER connection and IDs are ready)
            if (handshake.role === "spectator") {
                initializeSpectator(gameId);
            } else {
                initializeGame(gameId, playerId, {
                    dualControl: handshake.dualControl === true,
                    hostPlayerId: handshake.hostPlayerId || playerId,
                    guestPlayerId: handshake.guestPlayerId || null
                }); // Still needed to set IDs in gameManager
            }

            // CRITICAL: Restore the call to updateFullGameState here.
            // This ensures the *initial* state (received via the promise)
            // is processed and rendered correctly (e.g., showing the Mulligan phase).
            // The ReceiveGameState handler handles *subsequent* updates directly.
            updateFullGameState(serverState); // <-- RESTORED

            // 7. Setup core event listeners (AFTER connection and UI are ready)
            setupEventListeners();

            // 8. Initialize developer console
            DEBUG_CONSOLE_ENABLED = DEBUG_FLAG || !!handshake.useAiFallback || !!handshake.dualControl; // 单人模式也强制开启
            DEBUG_CONSOLE_AUTOSHOW = !!handshake.useAiFallback || !!handshake.dualControl;
            
            // Always initialize dev console to ensure the global object exists,
            // but control visibility/enabled state via logic
            initDevConsole(true, DEBUG_CONSOLE_AUTOSHOW);


        } catch (error) {
            console.error("MAIN: Error during M6 coordination (Promise.all failed):", error);
            // Handle errors from loadCards, setupSignalRConnection, or state reception issues
            alert(`游戏初始化失败：${error.message || '未知错误'}. 请刷新页面重试。`);
            // Optionally disable UI or show persistent error
            return; // Stop initialization on error
        }
        // --- M6 Coordination Logic End ---

    } catch (error) {
        console.error("MAIN: Application initialization failed:", error);
        // Display error via ui.js, check if connection error already shown
        if (connection === null || connection.state === signalR.HubConnectionState.Disconnected) {
            // alert might have already been called in setupSignalRConnection
        } else {
            alert(`游戏初始化失败: ${error.message || '未知错误'}`); // Use alert instead of displayError
        }
    } finally {
        isInitializing = false;
    }
}

// 绑定核心事件监听器
// --- Core Event Listeners ---

/**
 * Sets up global event listeners (like End Turn button).
 */
function setupEventListeners() {
    const endTurnButton = document.getElementById('end-turn-button');
    if (endTurnButton) {
        // Removed redundant event listener for end-turn-button.
        // Event delegation in ui.js handles this now.
    } else {
        console.error("MAIN: Element with ID 'end-turn-button' not found!");
    }

    // UI event delegation (e.g., clicks on field, hand) is handled within ui.js
    // We assume ui.js's initializeUI or specific handlers are called elsewhere appropriately.
}

// 导出 connection 供其他模块使用
export { connection };

// --- Multiplayer UI Helpers ---
let mpPollTimerId = null;
let suppressMultiplayerLeaveOnce = false;
let mpLastCopied = { value: null, at: 0 };
const MP_COPY_STICKY_MS = 0;

function stopMultiplayerPolling() {
    if (mpPollTimerId) {
        clearInterval(mpPollTimerId);
        mpPollTimerId = null;
    }
}

function formatHandshakeOutput(handshake) {
    const wrapCopy = (label, value) => {
        if (!value) return null;
        const safeValue = String(value);
        const copiedClass = mpLastCopied?.value === safeValue ? ' copied' : '';
        return `${label}: <span class="mp-copy${copiedClass}" role="button" tabindex="0" title="点击复制" data-copy="${safeValue}"><strong>${safeValue}</strong></span>`;
    };

    const parts = [
        wrapCopy('GameId', handshake.gameId),
        wrapCopy('PlayerId', handshake.playerId),
    ].filter(Boolean);
    if (handshake.joinCode) {
        parts.push(wrapCopy('JoinCode', handshake.joinCode));
    }
    if (handshake.role) {
        parts.push(`Role: ${handshake.role}`);
    }
    return parts.join('<br>');
}

function setPendingHandshake(handshake) {
    pendingHandshake = handshake;
    cacheHandshake(handshake);
}

function bindButton(button, handler) {
    if (!button) return;
    button.addEventListener('click', handler);
}

function setupMultiplayerUI() {
    const btnCreateHost = document.getElementById('mp-create-host');
    const btnJoin = document.getElementById('mp-join-btn');
    const btnGoGame = document.getElementById('mp-go-game');
    const multiplayerPanel = document.getElementById('multiplayer-panel');
    const outCreate = document.getElementById('mp-create-output');
    const outJoin = document.getElementById('mp-join-output');
    const outDeck = document.getElementById('mp-deck-output');
    const inputJoinGame = document.getElementById('mp-join-game');
    const inputJoinCode = document.getElementById('mp-join-code');
    const btnSpectate = document.getElementById('mp-spectate-btn');
    const inputSpectateGame = document.getElementById('mp-spectate-game');
    const outSpectate = document.getElementById('mp-spectate-output');
    const deckPrev = document.getElementById('mp-deck-prev');
    const deckNext = document.getElementById('mp-deck-next');

    const copyTextToClipboard = async (text) => {
        const value = String(text ?? '');
        if (!value) return false;

        try {
            if (navigator?.clipboard?.writeText) {
                await navigator.clipboard.writeText(value);
                return true;
            }
        } catch (err) {
            // fall back
        }

        try {
            const el = document.createElement('textarea');
            el.value = value;
            el.setAttribute('readonly', '');
            el.style.position = 'fixed';
            el.style.top = '-1000px';
            el.style.left = '-1000px';
            document.body.appendChild(el);
            el.select();
            const ok = document.execCommand('copy');
            document.body.removeChild(el);
            return ok;
        } catch (err) {
            return false;
        }
    };

    const wireCopyHandlers = () => {
        if (!multiplayerPanel) return;
        if (multiplayerPanel.dataset.copyWired === 'true') return;
        multiplayerPanel.dataset.copyWired = 'true';

        const handleCopy = async (target) => {
            const text = target?.dataset?.copy;
            const ok = await copyTextToClipboard(text);
            if (!ok) {
                alert('复制失败，请手动选择复制。');
                return;
            }
            mpLastCopied = { value: String(text ?? ''), at: Date.now() };
            try {
                multiplayerPanel.querySelectorAll('.mp-copy.copied').forEach(el => el.classList.remove('copied'));
            } catch (_) {
                // ignore
            }
            target.classList.add('copied');
        };

        multiplayerPanel.addEventListener('click', (event) => {
            const target = event.target?.closest?.('.mp-copy');
            if (!target) return;
            event.preventDefault();
            handleCopy(target);
        });

        multiplayerPanel.addEventListener('keydown', (event) => {
            const target = event.target?.closest?.('.mp-copy');
            if (!target) return;
            if (event.key !== 'Enter' && event.key !== ' ') return;
            event.preventDefault();
            handleCopy(target);
        });
    };

    wireCopyHandlers();

    const setOutput = (el, text, isError = false) => {
        if (!el) return;
        if (el.dataset.lastOutput === text) {
            return;
        }
        el.innerHTML = text;
        el.style.color = isError ? '#ff4f00' : 'rgba(255,255,255,0.85)';
        el.dataset.lastOutput = text;
    };

    const setDeckOutput = (text, isError = false) => {
        if (!outDeck) return;
        outDeck.textContent = text || '';
        outDeck.style.color = isError ? '#ff4f00' : 'rgba(255,255,255,0.85)';
    };

    const getSelectedMultiplayerDeckId = () => {
        const current = normalizeDeckId(deckSelectionState.selected.multiplayer);
        if (current) return current;
        const fallback = resolveDeckId(deckSelectionState.decks[0]);
        deckSelectionState.selected.multiplayer = fallback;
        return fallback;
    };

    const cycleDeck = (delta) => {
        const decks = deckSelectionState.decks;
        if (!decks.length) return;
        const currentIndex = getDeckIndexById(deckSelectionState.selected.multiplayer);
        const index = currentIndex >= 0 ? currentIndex : 0;
        const nextIndex = (index + delta + decks.length) % decks.length;
        const deck = decks[nextIndex];
        deckSelectionState.selected.multiplayer = resolveDeckId(deck);
        updateMultiplayerDeckSwitcher();
        setDeckOutput('');
    };

    deckPrev?.addEventListener('click', () => cycleDeck(-1));
    deckNext?.addEventListener('click', () => cycleDeck(1));

    refreshDeckSelectionUI().catch(() => {
        setDeckOutput('卡组加载失败，请稍后重试。', true);
    });

    bindButton(btnCreateHost, async () => {
        if (!outCreate) return;
        const deckId = getSelectedMultiplayerDeckId();
        if (!deckId) {
            setDeckOutput('请先选择卡组。', true);
            return;
        }
        setOutput(outCreate, '创建房间中...');
        try {
            const handshake = await createMatch(false);
            await markReady(handshake.gameId, handshake.playerId, handshake.token);
            const selection = await submitDeckSelection(handshake, { deckId });
            if (!selection.ok) {
                setOutput(outCreate, `卡组选择失败：${selection.message}`, true);
                return;
            }
            setPendingHandshake(handshake);
            setOutput(outCreate, `房间已创建，等待对手加入。<br>${formatHandshakeOutput(handshake)}`);

            stopMultiplayerPolling();
            mpPollTimerId = setInterval(async () => {
                try {
                    const room = await getMatch(handshake.gameId);
                    const status = room.status;
                    const guestJoined = !!room.guestPlayerId;
                    if (status === "Closed") {
                        stopMultiplayerPolling();
                        const reason = room.closedReason || "房间已关闭。";
                        setOutput(outCreate, `房间已解散：${reason}<br>${formatHandshakeOutput(handshake)}`, true);
                        clearCachedHandshake();
                        return;
                    }
                    setOutput(outCreate, `房间状态：${status}${guestJoined ? "（对手已加入）" : "（等待对手）"}<br>${formatHandshakeOutput(handshake)}`);

                    if (status === "Ready") {
                        try {
                            await startMatch(handshake.gameId, handshake.playerId, handshake.token);
                        } catch (err) {
                            // another side may already start
                        }
                    }

                    if (status === "Started") {
                        stopMultiplayerPolling();
                        suppressMultiplayerLeaveOnce = true;
                        router.navigateTo('game');
                    }
                } catch (err) {
                    const message = err?.message || String(err);
                    if (message.includes('房间不存在')) {
                        stopMultiplayerPolling();
                        setOutput(outCreate, `房间已解散：房间不存在（可能对手已退出）<br>${formatHandshakeOutput(handshake)}`, true);
                        clearCachedHandshake();
                        return;
                    }
                    setOutput(outCreate, `轮询失败：${message}<br>${formatHandshakeOutput(handshake)}`, true);
                }
            }, 1500);
        } catch (err) {
            setOutput(outCreate, `创建失败：${err.message}`, true);
        }
    });

    bindButton(btnJoin, async () => {
        if (!outJoin) return;
        const deckId = getSelectedMultiplayerDeckId();
        if (!deckId) {
            setDeckOutput('请先选择卡组。', true);
            return;
        }
        const gameId = inputJoinGame?.value?.trim();
        const joinCode = inputJoinCode?.value?.trim();
        if (!gameId || !joinCode) {
            setOutput(outJoin, '请输入 GameId 与 JoinCode。', true);
            return;
        }
        setOutput(outJoin, '加入房间中...');
        try {
            const handshake = await joinMatch(gameId, joinCode);
            await markReady(handshake.gameId, handshake.playerId, handshake.token);
            const selection = await submitDeckSelection(handshake, { deckId });
            if (!selection.ok) {
                setOutput(outJoin, `卡组选择失败：${selection.message}`, true);
                return;
            }
            try {
                await startMatch(handshake.gameId, handshake.playerId, handshake.token);
            } catch (err) {
                console.warn("Guest start skipped:", err.message);
            }
            setPendingHandshake(handshake);
            setOutput(outJoin, `加入成功，等待房间开始。<br>${formatHandshakeOutput(handshake)}`);

            stopMultiplayerPolling();
            mpPollTimerId = setInterval(async () => {
                try {
                    const room = await getMatch(handshake.gameId);
                    if (room.status === "Closed") {
                        stopMultiplayerPolling();
                        const reason = room.closedReason || "房间已关闭。";
                        setOutput(outJoin, `房间已解散：${reason}<br>${formatHandshakeOutput(handshake)}`, true);
                        clearCachedHandshake();
                        return;
                    }
                    setOutput(outJoin, `房间状态：${room.status}<br>${formatHandshakeOutput(handshake)}`);
                    if (room.status === "Started") {
                        stopMultiplayerPolling();
                        suppressMultiplayerLeaveOnce = true;
                        router.navigateTo('game');
                    }
                } catch (err) {
                    const message = err?.message || String(err);
                    if (message.includes('房间不存在')) {
                        stopMultiplayerPolling();
                        setOutput(outJoin, `房间已解散：房间不存在（可能对手已退出）<br>${formatHandshakeOutput(handshake)}`, true);
                        clearCachedHandshake();
                        return;
                    }
                    setOutput(outJoin, `轮询失败：${message}<br>${formatHandshakeOutput(handshake)}`, true);
                }
            }, 1200);
        } catch (err) {
            setOutput(outJoin, `加入失败：${err.message}`, true);
        }
    });

    bindButton(btnGoGame, () => {
        const handshake = pendingHandshake || loadCachedHandshake();
        if (!handshake) {
            alert('请先创建或加入房间。');
            return;
        }
        if (handshake.useAiFallback) {
            router.navigateTo('game');
            return;
        }

        // For PVP rooms, only enter game when Started (avoids hanging init while waiting)
        (async () => {
            try {
                const deckId = getSelectedMultiplayerDeckId();
                if (!deckId) {
                    alert('请先选择卡组。');
                    return;
                }
                const selection = await submitDeckSelection(handshake, { deckId });
                if (!selection.ok) {
                    alert(selection.message || "无法选择卡组。");
                    return;
                }
                const room = await getMatch(handshake.gameId);
                if (room.status === "Ready") {
                    try {
                        await startMatch(handshake.gameId, handshake.playerId, handshake.token);
                    } catch (err) {
                        // ignore
                    }
                }
                const room2 = await getMatch(handshake.gameId);
                if (room2.status === "Closed") {
                    const reason = room2.closedReason || "房间已关闭。";
                    clearCachedHandshake();
                    alert(`无法进入对战：${reason}`);
                    return;
                }
                if (room2.status !== "Started") {
                    alert('房间未开始，请等待对手加入并准备。');
                    return;
                }
                suppressMultiplayerLeaveOnce = true;
                router.navigateTo('game');
            } catch (err) {
                alert(`无法进入对战：${err.message}`);
            }
        })();
    });

    bindButton(btnSpectate, async () => {
        if (!outSpectate) return;
        const gameId = inputSpectateGame?.value?.trim();
        if (!gameId || !isValidGuid(gameId)) {
            setOutput(outSpectate, '请输入有效的 GameId（GUID）。', true);
            return;
        }

        try {
            stopMultiplayerPolling();
            clearCachedHandshake();
            suppressMultiplayerLeaveOnce = true;

            const params = new URLSearchParams(window.location.search);
            params.set('gameId', gameId);
            params.set('spectate', '1');
            params.delete('playerId');
            params.delete('token');
            params.delete('joinCode');
            params.delete('useAiFallback');

            window.history.replaceState(window.history.state, '', `?${params.toString()}${window.location.hash}`);
            router.navigateTo('game');
        } catch (err) {
            setOutput(outSpectate, `无法开始观战：${err?.message || String(err)}`, true);
        }
    });
}

function setupSingleplayerUI() {
    const singlePanel = document.getElementById('singleplayer-panel');
    if (!singlePanel) return;

    const modeSelect = document.getElementById('sp-mode');
    const aiSelect = document.getElementById('sp-ai-deck');
    const outDeck = document.getElementById('sp-deck-output');
    const outStart = document.getElementById('sp-start-output');
    const btnStart = document.getElementById('sp-start-btn');
    const deckPrev = document.getElementById('sp-deck-prev');
    const deckNext = document.getElementById('sp-deck-next');
    const modeNote = document.getElementById('sp-mode-note');
    const modeHint = document.getElementById('sp-mode-hint');
    const startDesc = document.getElementById('sp-start-desc');
    const opponentLabel = document.getElementById('sp-ai-deck-label');

    const setDeckOutput = (text, isError = false) => {
        if (!outDeck) return;
        outDeck.textContent = text || '';
        outDeck.style.color = isError ? '#ff4f00' : 'rgba(255,255,255,0.85)';
    };

    const setStartOutput = (text, isError = false) => {
        if (!outStart) return;
        outStart.textContent = text || '';
        outStart.style.color = isError ? '#ff4f00' : 'rgba(255,255,255,0.85)';
    };

    const normalizeSingleplayerMode = (value) => (value === 'dual' ? 'dual' : 'ai');

    const getSingleplayerMode = () => {
        const selected = normalizeSingleplayerMode(modeSelect?.value || deckSelectionState.singleplayerMode);
        deckSelectionState.singleplayerMode = selected;
        return selected;
    };

    const applySingleplayerMode = (mode) => {
        const normalized = normalizeSingleplayerMode(mode);
        deckSelectionState.singleplayerMode = normalized;
        if (modeSelect) modeSelect.value = normalized;

        const isDual = normalized === 'dual';
        if (startDesc) startDesc.textContent = isDual ? '确认双方卡组后开始' : '确认玩家与 AI 卡组后开始';
        if (modeHint) modeHint.textContent = isDual
            ? '提示：双人同机将自动加入客位并控制双方。'
            : '提示：单人对战需要选择玩家与 AI 卡组。';
        if (opponentLabel) opponentLabel.textContent = isDual ? '对手卡组' : 'AI 卡组';
    };

    const getSelectedSingleplayerDeckId = () => {
        const current = normalizeDeckId(deckSelectionState.selected.singleplayer);
        if (current) return current;
        const fallback = resolveDeckId(deckSelectionState.decks[0]);
        deckSelectionState.selected.singleplayer = fallback;
        return fallback;
    };

    const cycleDeck = (delta) => {
        const decks = deckSelectionState.decks;
        if (!decks.length) return;
        const currentIndex = getDeckIndexById(deckSelectionState.selected.singleplayer);
        const index = currentIndex >= 0 ? currentIndex : 0;
        const nextIndex = (index + delta + decks.length) % decks.length;
        const deck = decks[nextIndex];
        deckSelectionState.selected.singleplayer = resolveDeckId(deck);
        updateSingleplayerDeckSwitcher();
        setDeckOutput('');
    };

    deckPrev?.addEventListener('click', () => cycleDeck(-1));
    deckNext?.addEventListener('click', () => cycleDeck(1));

    modeSelect?.addEventListener('change', () => {
        applySingleplayerMode(modeSelect.value);
        setDeckOutput('');
    });

    aiSelect?.addEventListener('change', () => {
        deckSelectionState.selected.ai = normalizeDeckId(aiSelect.value);
        setDeckOutput('');
    });

    refreshDeckSelectionUI().catch(() => {
        setDeckOutput('卡组加载失败，请稍后重试。', true);
    });
    applySingleplayerMode(deckSelectionState.singleplayerMode);

    bindButton(btnStart, async () => {
        const mode = getSingleplayerMode();
        const playerDeckId = getSelectedSingleplayerDeckId();
        const aiDeckId = normalizeDeckId(aiSelect?.value || deckSelectionState.selected.ai);
        if (!playerDeckId || !aiDeckId) {
            const opponentLabelText = mode === 'dual' ? '对手' : 'AI';
            setDeckOutput(`请先选择玩家与 ${opponentLabelText} 卡组。`, true);
            return;
        }

        setStartOutput('创建对战中...');
        try {
            resetTransientConnectionUiState();
            clearTransientMatchQueryParams();
            clearCachedHandshake();
            if (mode === 'dual') {
                const hostHandshake = await createMatch(false);
                // Join as an anonymous guest to avoid host/guest sharing the same playerId when logged in.
                const guestHandshake = await joinMatch(hostHandshake.gameId, hostHandshake.joinCode, { playerId: null });

                await markReady(hostHandshake.gameId, hostHandshake.playerId, hostHandshake.token);
                await markReady(guestHandshake.gameId, guestHandshake.playerId, guestHandshake.token);

                const hostSelection = await submitDeckSelection(hostHandshake, { deckId: playerDeckId });
                if (!hostSelection.ok) {
                    setStartOutput(`卡组选择失败：${hostSelection.message}`, true);
                    return;
                }

                const guestSelection = await submitDeckSelection(guestHandshake, { deckId: aiDeckId });
                if (!guestSelection.ok) {
                    setStartOutput(`卡组选择失败：${guestSelection.message}`, true);
                    return;
                }

                await startMatch(hostHandshake.gameId, hostHandshake.playerId, hostHandshake.token);

                const dualHandshake = {
                    ...hostHandshake,
                    dualControl: true,
                    hostPlayerId: hostHandshake.playerId,
                    hostToken: hostHandshake.token,
                    guestPlayerId: guestHandshake.playerId,
                    guestToken: guestHandshake.token
                };

                setPendingHandshake(dualHandshake);
                router.navigateTo('game');
            } else {
                const handshake = await createMatch(true);
                await markReady(handshake.gameId, handshake.playerId, handshake.token);
                const selection = await submitDeckSelection(handshake, { deckId: playerDeckId, aiDeckId, requireAi: true });
                if (!selection.ok) {
                    setStartOutput(`卡组选择失败：${selection.message}`, true);
                    return;
                }
                await startMatch(handshake.gameId, handshake.playerId, handshake.token);
                setPendingHandshake(handshake);
                router.navigateTo('game');
            }
        } catch (err) {
            setStartOutput(`进入失败：${err.message}`, true);
        }
    });

    if (router && typeof router.setAfterRouteChange === 'function') {
        const originalAfter = router.afterRouteChange;
        router.setAfterRouteChange(async (oldRoute, newRoute) => {
            if (originalAfter) await originalAfter(oldRoute, newRoute);
            setStartOutput('');
        });
    }
}

function setupUserInfoUI() {
    const toggle = document.getElementById('user-info-toggle');
    const card = document.getElementById('user-info-card');
    const aboutCard = document.getElementById('about-card');
    const devConsoleToggle = document.getElementById('dev-console-toggle');
    const nameEl = document.getElementById('user-info-name');
    const roleEl = document.getElementById('user-info-role');
    const idEl = document.getElementById('user-info-id');
    const avatarEl = document.getElementById('user-info-avatar');
    const avatarImgEl = document.getElementById('user-info-avatar-img');
    const avatarTextEl = document.getElementById('user-info-avatar-text');
    const idInline = document.getElementById('user-id-inline');
    const coinsEl = document.getElementById('user-info-coins');
    const authSection = document.getElementById('user-auth');
    const usernameInput = document.getElementById('user-auth-username');
    const passwordInput = document.getElementById('user-auth-password');
    const registerBtn = document.getElementById('user-auth-register');
    const loginBtn = document.getElementById('user-auth-login');
    const logoutBtn = document.getElementById('user-auth-logout');
    const statusEl = document.getElementById('user-auth-status');

    if (!toggle || !card || !nameEl || !roleEl || !idEl || !avatarEl || !avatarTextEl) {
        return;
    }

    const isAvatarUrl = (value) => {
        if (!value) return false;
        return value.startsWith('/') || value.startsWith('http://') || value.startsWith('https://') || value.startsWith('data:');
    };

    const setStatus = (text, isError = false) => {
        if (!statusEl) return;
        statusEl.textContent = text || '';
        statusEl.style.color = isError ? 'rgba(255, 120, 120, 0.9)' : 'rgba(255, 255, 255, 0.7)';
    };

    const setAuthBusy = (busy) => {
        if (registerBtn) registerBtn.disabled = busy;
        if (loginBtn) loginBtn.disabled = busy;
        if (logoutBtn) logoutBtn.disabled = busy;
    };

    const normalizeRole = (role) => {
        const normalized = String(role || "").trim().toLowerCase();
        if (normalized === "admin") return "admin";
        if (normalized === "guest") return "guest";
        return "player";
    };

    const resolvePermissionLevel = (role, permissionLevel) => {
        if (Number.isFinite(permissionLevel)) {
            return Math.max(0, Math.trunc(permissionLevel));
        }
        const normalizedRole = normalizeRole(role);
        if (normalizedRole === "admin") return 100;
        if (normalizedRole === "player") return 10;
        return 0;
    };

    const formatRoleDisplay = (role, permissionLevel) => {
        const normalizedRole = normalizeRole(role);
        const level = resolvePermissionLevel(normalizedRole, permissionLevel);
        const label = normalizedRole === "admin"
            ? "ADMIN"
            : (normalizedRole === "guest" ? "GUEST" : "PLAYER");
        return `${label} · L${level}`;
    };

    const maskIdValue = (value) => {
        if (!value || value === '-') return '-';
        return '********';
    };

    const resetIdRow = (value) => {
        if (!idInline || !idEl) return;
        const fullValue = value || '-';
        idInline.classList.remove('expanded');
        idInline.setAttribute('aria-expanded', 'false');
        idEl.dataset.fullId = fullValue;
        idEl.textContent = maskIdValue(fullValue);
        idEl.setAttribute('title', '点击展示');
    };

    const expandIdRow = () => {
        if (!idInline || !idEl) return;
        idInline.classList.add('expanded');
        idInline.setAttribute('aria-expanded', 'true');
        const fullValue = idEl.dataset.fullId || idEl.textContent || '-';
        idEl.textContent = fullValue;
        idEl.setAttribute('title', '点击隐藏');
    };

    const toggleIdRow = () => {
        if (!idInline || !idEl) return;
        if (idInline.classList.contains('expanded')) {
            resetIdRow(idEl.dataset.fullId || idEl.textContent || '-');
            return;
        }
        expandIdRow();
    };

    let economyRequest = null;
    const economyCache = {
        userId: null,
        coins: null,
        fetchedAt: 0
    };

    const setCoinsValue = (value) => {
        if (!coinsEl) return;
        coinsEl.textContent = value;
    };

    const refreshEconomy = async (force = false) => {
        if (!coinsEl) return;
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId) {
            setCoinsValue('--');
            return;
        }

        const now = Date.now();
        if (!force && economyCache.userId === session.userId && now - economyCache.fetchedAt < 15000) {
            if (economyCache.coins != null) {
                setCoinsValue(String(economyCache.coins));
                return;
            }
        }

        if (economyRequest) {
            await economyRequest;
            return;
        }

        setCoinsValue('...');
        economyRequest = (async () => {
            try {
                const state = await apiGetJson(`/api/economy/state/${session.userId}`);
                const coins = Number.isFinite(state?.rank?.coins) ? state.rank.coins : 0;
                economyCache.userId = session.userId;
                economyCache.coins = coins;
                economyCache.fetchedAt = Date.now();
                setCoinsValue(String(coins));
            } catch (err) {
                setCoinsValue('--');
            } finally {
                economyRequest = null;
            }
        })();

        await economyRequest;
    };

    const applySession = () => {
        const session = getUserSession();
        const isUser = session.mode === "user";
        // toggle button is now an icon, no text content update needed
        nameEl.textContent = session.displayName || (isUser ? "用户" : "游客");
        roleEl.textContent = formatRoleDisplay(session.role, session.permissionLevel);
        card.classList.toggle('is-user', isUser);
        resetIdRow(session.userId || session.guestId || "-");
        const avatarValue = session.avatar || (isUser ? "U" : "G");
        const hasAvatarImage = isAvatarUrl(avatarValue);
        if (hasAvatarImage && avatarImgEl) {
            avatarImgEl.src = avatarValue;
            avatarImgEl.alt = `${session.displayName || "User"} avatar`;
            avatarEl.classList.add('has-image');
        } else {
            avatarEl.classList.remove('has-image');
            if (avatarImgEl) {
                avatarImgEl.removeAttribute('src');
            }
        }
        avatarTextEl.textContent = hasAvatarImage
            ? (session.displayName || "U").trim().charAt(0).toUpperCase()
            : avatarValue;
        if (authSection) {
            authSection.classList.toggle('is-user', isUser);
        }
        if (isUser) {
            if (usernameInput) usernameInput.value = session.displayName || '';
            if (passwordInput) passwordInput.value = '';
        }

        refreshEconomy(true).catch(() => { });
    };

    const setOpen = (open) => {
        card.classList.toggle('active', open); // Changed from 'open' to 'active' to match CSS
        card.setAttribute('aria-hidden', open ? 'false' : 'true');
        if (open && aboutCard) {
            aboutCard.classList.remove('active');
            aboutCard.setAttribute('aria-hidden', 'true');
        }
        if (!open) {
            resetIdRow(idEl?.dataset.fullId || idEl?.textContent || "-");
        } else {
            refreshEconomy(false).catch(() => { });
        }
        syncTopNavHoverLock();
    };

    applySession();

    document.addEventListener(USER_SESSION_CHANGED_EVENT, () => {
        applySession();
    });

    toggle.addEventListener('click', (event) => {
        event.stopPropagation();
        applySession();
        const isOpen = card.classList.contains('active');
        setOpen(!isOpen);
    });

    devConsoleToggle?.addEventListener('click', () => {
        setOpen(false);
    });

    idInline?.addEventListener('click', (event) => {
        event.stopPropagation();
        toggleIdRow();
    });

    idInline?.addEventListener('keydown', (event) => {
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            toggleIdRow();
        }
    });

    document.addEventListener('click', (event) => {
        if (!card.classList.contains('active')) return;
        const target = event.target;
        if (target instanceof Element && (card.contains(target) || toggle.contains(target))) {
            return;
        }
        setOpen(false);
    });

    const openAvatarCropModal = (session) => {
        if (document.querySelector('.avatar-crop-modal')) return;

        const modal = document.createElement('div');
        modal.className = 'avatar-crop-modal';
        modal.innerHTML = `
            <div class="avatar-crop-card">
                <div class="avatar-crop-header">
                    <div class="avatar-crop-title">上传头像</div>
                    <button type="button" class="avatar-crop-close">×</button>
                </div>
                <div class="avatar-crop-stage">
                    <img class="avatar-crop-image" alt="Avatar preview">
                    <div class="avatar-crop-box">
                        <span class="avatar-crop-handle"></span>
                    </div>
                    <div class="avatar-crop-placeholder">
                        <div class="avatar-crop-placeholder-icon">UPLOAD</div>
                        <div class="avatar-crop-placeholder-text">点击选择头像图片</div>
                    </div>
                </div>
                <input class="avatar-crop-file" type="file" accept="image/*">
                <div class="avatar-crop-actions">
                    <button type="button" class="btn avatar-crop-cancel">取消</button>
                    <button type="button" class="btn btn-primary avatar-crop-upload">上传</button>
                </div>
                <div class="avatar-crop-status"></div>
            </div>
        `;

        document.body.appendChild(modal);

        const closeBtn = modal.querySelector('.avatar-crop-close');
        const cancelBtn = modal.querySelector('.avatar-crop-cancel');
        const uploadBtn = modal.querySelector('.avatar-crop-upload');
        const input = modal.querySelector('.avatar-crop-file');
        const status = modal.querySelector('.avatar-crop-status');
        const stage = modal.querySelector('.avatar-crop-stage');
        const imageEl = modal.querySelector('.avatar-crop-image');
        const cropBox = modal.querySelector('.avatar-crop-box');
        const resizeHandle = modal.querySelector('.avatar-crop-handle');

        const state = {
            objectUrl: null,
            imageRect: null,
            naturalWidth: 0,
            naturalHeight: 0,
            crop: { x: 0, y: 0, size: 0 },
            dragging: false,
            resizing: false,
            startX: 0,
            startY: 0,
            startCropX: 0,
            startCropY: 0,
            startSize: 0
        };

        const setModalStatus = (text, isError = false) => {
            if (!status) return;
            status.textContent = text || '';
            status.style.color = isError ? 'rgba(255, 120, 120, 0.9)' : 'rgba(255, 255, 255, 0.7)';
        };

        const cleanup = () => {
            if (state.objectUrl) {
                URL.revokeObjectURL(state.objectUrl);
            }
            modal.remove();
        };

        const closeModal = () => {
            cleanup();
        };

        const updateImageRect = () => {
            if (!stage || !imageEl) return;
            const stageRect = stage.getBoundingClientRect();
            const imgRect = imageEl.getBoundingClientRect();
            state.imageRect = {
                x: imgRect.left - stageRect.left,
                y: imgRect.top - stageRect.top,
                width: imgRect.width,
                height: imgRect.height
            };
        };

        const clampCrop = () => {
            if (!state.imageRect) return;
            const minSize = 80;
            const maxSize = Math.min(state.imageRect.width, state.imageRect.height);
            state.crop.size = Math.min(Math.max(state.crop.size, minSize), maxSize);
            const maxX = state.imageRect.x + state.imageRect.width - state.crop.size;
            const maxY = state.imageRect.y + state.imageRect.height - state.crop.size;
            state.crop.x = Math.min(Math.max(state.crop.x, state.imageRect.x), maxX);
            state.crop.y = Math.min(Math.max(state.crop.y, state.imageRect.y), maxY);
        };

        const renderCrop = () => {
            if (!cropBox) return;
            cropBox.style.left = `${state.crop.x}px`;
            cropBox.style.top = `${state.crop.y}px`;
            cropBox.style.width = `${state.crop.size}px`;
            cropBox.style.height = `${state.crop.size}px`;
        };

        const initCropBox = () => {
            if (!state.imageRect) return;
            const size = Math.min(state.imageRect.width, state.imageRect.height) * 0.7;
            state.crop.size = Math.max(80, size);
            state.crop.x = state.imageRect.x + (state.imageRect.width - state.crop.size) / 2;
            state.crop.y = state.imageRect.y + (state.imageRect.height - state.crop.size) / 2;
            clampCrop();
            renderCrop();
        };

        closeBtn?.addEventListener('click', closeModal);
        cancelBtn?.addEventListener('click', closeModal);
        modal.addEventListener('click', (event) => {
            if (event.target === modal) {
                closeModal();
            }
        });

        input?.addEventListener('change', () => {
            const file = input.files?.[0];
            if (!file) return;
            if (!file.type.startsWith('image/')) {
                setModalStatus('不支持的图片类型。', true);
                return;
            }
            stage?.classList.remove('has-image');
            if (cropBox) {
                cropBox.style.display = 'none';
            }
            if (state.objectUrl) {
                URL.revokeObjectURL(state.objectUrl);
            }
            const url = URL.createObjectURL(file);
            state.objectUrl = url;
            if (!imageEl) return;
            imageEl.onload = () => {
                state.naturalWidth = imageEl.naturalWidth;
                state.naturalHeight = imageEl.naturalHeight;
                requestAnimationFrame(() => {
                    updateImageRect();
                    initCropBox();
                    setModalStatus('');
                    stage?.classList.add('has-image');
                });
            };
            imageEl.onerror = () => {
                setModalStatus('图片加载失败。', true);
            };
            imageEl.src = url;
        });

        imageEl?.addEventListener('click', () => {
            if (!stage?.classList.contains('has-image')) {
                input?.click();
            }
        });

        cropBox?.addEventListener('pointerdown', (event) => {
            if (event.target === resizeHandle) return;
            event.preventDefault();
            state.dragging = true;
            state.startX = event.clientX;
            state.startY = event.clientY;
            state.startCropX = state.crop.x;
            state.startCropY = state.crop.y;
            cropBox.setPointerCapture(event.pointerId);
            cropBox.classList.add('dragging');
        });

        resizeHandle?.addEventListener('pointerdown', (event) => {
            event.preventDefault();
            event.stopPropagation();
            state.resizing = true;
            state.startX = event.clientX;
            state.startY = event.clientY;
            state.startSize = state.crop.size;
            resizeHandle.setPointerCapture(event.pointerId);
            cropBox?.classList.add('resizing');
        });

        const onPointerMove = (event) => {
            if (!state.imageRect) return;
            if (state.dragging) {
                const dx = event.clientX - state.startX;
                const dy = event.clientY - state.startY;
                state.crop.x = state.startCropX + dx;
                state.crop.y = state.startCropY + dy;
                clampCrop();
                renderCrop();
            } else if (state.resizing) {
                const dx = event.clientX - state.startX;
                const dy = event.clientY - state.startY;
                const delta = Math.max(dx, dy);
                state.crop.size = state.startSize + delta;
                clampCrop();
                renderCrop();
            }
        };

        const stopPointer = (event) => {
            if (state.dragging && cropBox) {
                state.dragging = false;
                cropBox.releasePointerCapture(event.pointerId);
                cropBox.classList.remove('dragging');
            }
            if (state.resizing && resizeHandle) {
                state.resizing = false;
                resizeHandle.releasePointerCapture(event.pointerId);
                cropBox?.classList.remove('resizing');
            }
        };

        stage?.addEventListener('pointermove', onPointerMove);
        stage?.addEventListener('pointerup', stopPointer);
        stage?.addEventListener('pointerleave', stopPointer);
        stage?.addEventListener('pointercancel', stopPointer);

        uploadBtn?.addEventListener('click', async () => {
            if (!imageEl?.src || !state.imageRect || state.crop.size <= 0) {
                setModalStatus('请先选择图片。', true);
                return;
            }
            setModalStatus('上传中...');
            uploadBtn.disabled = true;
            try {
                const scaleX = state.naturalWidth / state.imageRect.width;
                const scaleY = state.naturalHeight / state.imageRect.height;
                const scale = Math.min(scaleX, scaleY);
                const rawX = (state.crop.x - state.imageRect.x) * scale;
                const rawY = (state.crop.y - state.imageRect.y) * scale;
                const rawSize = state.crop.size * scale;
                const sourceSize = Math.min(rawSize, state.naturalWidth, state.naturalHeight);
                const sourceX = Math.max(0, Math.min(state.naturalWidth - sourceSize, rawX));
                const sourceY = Math.max(0, Math.min(state.naturalHeight - sourceSize, rawY));

                const outputSize = 320;
                const canvas = document.createElement('canvas');
                canvas.width = outputSize;
                canvas.height = outputSize;
                const ctx = canvas.getContext('2d');
                if (!ctx) {
                    throw new Error('头像生成失败。');
                }
                ctx.drawImage(
                    imageEl,
                    sourceX,
                    sourceY,
                    sourceSize,
                    sourceSize,
                    0,
                    0,
                    outputSize,
                    outputSize
                );

                const blob = await new Promise((resolve) => {
                    canvas.toBlob(resolve, 'image/png');
                });
                if (!blob) {
                    throw new Error('头像生成失败。');
                }
                const formData = new FormData();
                formData.append('file', blob, 'avatar.png');
                const response = await fetch(`/api/users/${encodeURIComponent(session.userId)}/avatar`, {
                    method: 'POST',
                    body: formData
                });
                let payload = null;
                try {
                    payload = await response.json();
                } catch (err) {
                    // ignore parse errors
                }
                if (!response.ok) {
                    const message = payload?.message || payload?.Message || response.statusText;
                    throw new Error(message || `HTTP ${response.status}`);
                }
                const avatarUrl = payload?.avatarUrl || payload?.AvatarUrl;
                const cacheBusted = avatarUrl ? `${avatarUrl}?t=${Date.now()}` : '';
                setUserSession({
                    userId: session.userId,
                    username: session.displayName,
                    avatar: cacheBusted || session.avatar,
                    role: session.role,
                    permissionLevel: session.permissionLevel
                });
                applySession();
                setModalStatus('上传完成。');
                setTimeout(closeModal, 400);
            } catch (err) {
                setModalStatus(`上传失败：${err.message}`, true);
            } finally {
                uploadBtn.disabled = false;
            }
        });

        if (cropBox) {
            cropBox.style.display = 'none';
        }
        if (imageEl) {
            imageEl.addEventListener('load', () => {
                if (cropBox) cropBox.style.display = 'block';
                stage?.classList.add('has-image');
            }, { once: true });
        }
    };

    avatarEl.addEventListener('click', (event) => {
        event.stopPropagation();
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId) {
            setStatus('请先登录。', true);
            return;
        }
        openAvatarCropModal(session);
    });

    if (registerBtn && loginBtn && logoutBtn && usernameInput && passwordInput) {
        registerBtn.addEventListener('click', async () => {
            const username = usernameInput.value.trim();
            const password = passwordInput.value;
            if (!username || !password) {
                setStatus('请输入用户名和密码。', true);
                return;
            }
            setAuthBusy(true);
            setStatus('注册中...');
            try {
                const session = getUserSession();
                if (session.mode === 'user') {
                    setStatus('请先退出当前账号。', true);
                    return;
                }
                const payload = await apiPostJson('/api/auth/register', {
                    username,
                    password,
                    guestId: session.guestId || null
                });
                setUserSession({
                    userId: payload.userId,
                    username: payload.username,
                    avatar: payload.avatarUrl,
                    role: payload.role,
                    permissionLevel: payload.permissionLevel
                });
                passwordInput.value = '';
                applySession();
                const migrated = payload.migratedDecks || 0;
                const failed = payload.failedDecks || 0;
                if (failed > 0) {
                    setStatus(`注册成功，迁移卡组 ${migrated}，失败 ${failed}。`, true);
                } else if (migrated > 0) {
                    setStatus(`注册成功，已迁移卡组 ${migrated}。`);
                } else {
                    setStatus('注册成功。');
                }
                document.dispatchEvent(new Event('deckListUpdated'));
            } catch (err) {
                setStatus(`注册失败：${err.message}`, true);
            } finally {
                setAuthBusy(false);
            }
        });

        loginBtn.addEventListener('click', async () => {
            const username = usernameInput.value.trim();
            const password = passwordInput.value;
            if (!username || !password) {
                setStatus('请输入用户名和密码。', true);
                return;
            }
            setAuthBusy(true);
            setStatus('登录中...');
            try {
                const session = getUserSession();
                if (session.mode === 'user') {
                    setStatus('请先退出当前账号。', true);
                    return;
                }
                const payload = await apiPostJson('/api/auth/login', { username, password });
                setUserSession({
                    userId: payload.userId,
                    username: payload.username,
                    avatar: payload.avatarUrl,
                    role: payload.role,
                    permissionLevel: payload.permissionLevel
                });
                passwordInput.value = '';
                applySession();
                setStatus('登录成功。');
                document.dispatchEvent(new Event('deckListUpdated'));
            } catch (err) {
                setStatus(`登录失败：${err.message}`, true);
            } finally {
                setAuthBusy(false);
            }
        });

        logoutBtn.addEventListener('click', () => {
            clearUserSession();
            if (passwordInput) passwordInput.value = '';
            applySession();
            setStatus('已退出。');
            document.dispatchEvent(new Event('deckListUpdated'));
        });
    }
}

function syncTopNavHoverLock() {
    const topNav = document.querySelector('.top-nav-bar');
    if (!topNav) return;
    const hasPinnedCard = !!topNav.querySelector('.user-card-popup.active');
    document.body.classList.toggle('topnav-hover-locked', hasPinnedCard);
}

async function loadAboutChangelog() {
    const list = document.getElementById('about-changelog');
    const versionEl = document.getElementById('about-card-version');
    if (!list) return;

    const normalizeVersion = (value) => {
        if (typeof value !== 'string') return null;
        const trimmed = value.trim();
        if (!trimmed) return null;
        return trimmed.startsWith('v') ? trimmed : `v${trimmed}`;
    };

    const parseVersion = (value) => {
        const normalized = normalizeVersion(value);
        if (!normalized) return [0, 0, 0];
        const raw = normalized.slice(1).split('.');
        const parts = raw.map(part => Number.parseInt(part, 10));
        return [parts[0] || 0, parts[1] || 0, parts[2] || 0];
    };

    const compareVersions = (a, b) => {
        const va = parseVersion(a);
        const vb = parseVersion(b);
        for (let i = 0; i < Math.max(va.length, vb.length); i += 1) {
            const diff = (va[i] || 0) - (vb[i] || 0);
            if (diff !== 0) return diff;
        }
        return 0;
    };

    try {
        const changelogUrl = new URL('../data/about-changelog.json', import.meta.url);
        const response = await fetch(changelogUrl, { cache: 'no-store' });
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }
        const items = await response.json();
        list.innerHTML = '';

        const entries = Array.isArray(items) ? items : [];
        if (!entries.length) {
            const emptyItem = document.createElement('li');
            emptyItem.textContent = '暂无更新日志';
            list.appendChild(emptyItem);
            return;
        }

        const versions = entries
            .map(item => (typeof item === 'object' && item ? item.version : null))
            .filter(Boolean);
        const latestVersion = versions.length
            ? versions.reduce((latest, current) => {
                if (!latest) return current;
                return compareVersions(current, latest) > 0 ? current : latest;
            }, null)
            : null;

        if (versionEl && latestVersion) {
            versionEl.textContent = normalizeVersion(latestVersion);
        }

        const displayEntries = latestVersion
            ? entries.filter(item => item && typeof item === 'object' && item.version === latestVersion)
            : entries;

        displayEntries.forEach((item) => {
            if (!item) return;
            const li = document.createElement('li');
            if (typeof item === 'string') {
                li.textContent = item;
                list.appendChild(li);
                return;
            }
            const tag = document.createElement('span');
            tag.className = 'about-tag';
            tag.textContent = normalizeVersion(item.version) || 'v0.0.0';
            li.appendChild(tag);
            li.append(document.createTextNode(` ${item.text || ''}`));
            list.appendChild(li);
        });
    } catch (err) {
        console.warn('MAIN: Failed to load about changelog:', err);
        list.innerHTML = '';
        const fallbackItem = document.createElement('li');
        fallbackItem.textContent = '更新日志加载失败';
        list.appendChild(fallbackItem);
    }
}

function setupAboutUI() {
    const toggle = document.getElementById('about-toggle');
    const card = document.getElementById('about-card');
    const userCard = document.getElementById('user-info-card');
    const devConsoleToggle = document.getElementById('dev-console-toggle');
    if (!toggle || !card) return;

    const setOpen = (open) => {
        card.classList.toggle('active', open);
        card.setAttribute('aria-hidden', open ? 'false' : 'true');
        if (open && userCard) {
            userCard.classList.remove('active');
            userCard.setAttribute('aria-hidden', 'true');
        }
        syncTopNavHoverLock();
    };

    toggle.addEventListener('click', (event) => {
        event.stopPropagation();
        const isOpen = card.classList.contains('active');
        setOpen(!isOpen);
    });

    devConsoleToggle?.addEventListener('click', () => {
        setOpen(false);
    });

    document.addEventListener('click', (event) => {
        if (!card.classList.contains('active')) return;
        const target = event.target;
        if (target instanceof Element && (card.contains(target) || toggle.contains(target))) {
            return;
        }
        setOpen(false);
    });

    loadAboutChangelog();
}

function setupTopNavHoverLock() {
    if (!router || typeof router.setAfterRouteChange !== 'function') return;
    const originalAfter = router.afterRouteChange;
    router.setAfterRouteChange(async (oldRoute, newRoute) => {
        if (originalAfter) await originalAfter(oldRoute, newRoute);
        syncTopNavHoverLock();
    });
    syncTopNavHoverLock();
}

function setupMenuRouteOverlays() {
    const navMenu = document.querySelector('#navigation-page .nav-menu');
    const navItems = navMenu ? Array.from(navMenu.querySelectorAll('.nav-item[data-route]')) : [];
    const overlays = Array.from(document.querySelectorAll('#navigation-page .menu-route-overlay'));
    const overlayImages = Array.from(document.querySelectorAll('#navigation-page .menu-route-image'));

    if (!navItems.length || !overlays.length) return;

    const overlayByRoute = new Map(
        overlays.map(overlay => [overlay.dataset.route || '', overlay])
    );
    const pinnedRoutes = new Set(['singleplayer', 'multiplayer']);

    let pinnedRoute = null;
    let activeRoute = null;
    let hoverRoute = null;

    const showOverlay = (route) => {
        activeRoute = route;
        overlays.forEach(overlay => {
            const matches = overlay.dataset.route === route;
            overlay.classList.toggle('is-visible', matches);
        });
    };

    const clearOverlay = () => {
        activeRoute = null;
        overlays.forEach(overlay => overlay.classList.remove('is-visible'));
    };

    const syncPinnedWithRoute = (route) => {
        if (pinnedRoutes.has(route) && overlayByRoute.has(route)) {
            pinnedRoute = route;
            showOverlay(route);
            return;
        }
        pinnedRoute = null;
        if (hoverRoute && overlayByRoute.has(hoverRoute)) {
            showOverlay(hoverRoute);
        } else {
            clearOverlay();
        }
    };

    navItems.forEach(item => {
        const route = item.dataset.route || '';
        if (!overlayByRoute.has(route)) return;

        const showRouteOverlay = () => {
            hoverRoute = route;
            showOverlay(route);
        };
        const restoreOverlay = () => {
            hoverRoute = null;
            if (pinnedRoute && overlayByRoute.has(pinnedRoute)) {
                showOverlay(pinnedRoute);
            } else {
                clearOverlay();
            }
        };

        item.addEventListener('mouseenter', showRouteOverlay);
        item.addEventListener('mouseleave', restoreOverlay);
        item.addEventListener('focusin', showRouteOverlay);
        item.addEventListener('focusout', restoreOverlay);

        item.addEventListener('click', () => {
            pinnedRoute = route;
            showOverlay(route);
        });
    });

    const updateOverlayImageSizing = () => {
        const viewportWidth = window.innerWidth || 0;
        const viewportHeight = window.innerHeight || 0;
        if (!viewportWidth || !viewportHeight) return;

        const maxWidth = viewportWidth;
        const maxHeight = viewportHeight;

        overlayImages.forEach(img => {
            if (!img.naturalWidth || !img.naturalHeight) return;
            const aspect = img.naturalWidth / img.naturalHeight;
            const width = Math.min(maxWidth, maxHeight * aspect);
            img.style.width = `${Math.max(1, width)}px`;
            img.style.height = 'auto';
        });
    };

    overlayImages.forEach(img => {
        if (img.complete && img.naturalWidth) {
            updateOverlayImageSizing();
            return;
        }
        img.addEventListener('load', updateOverlayImageSizing, { once: true });
    });

    window.addEventListener('resize', updateOverlayImageSizing);
    updateOverlayImageSizing();

    if (router && typeof router.setAfterRouteChange === 'function') {
        const originalAfter = router.afterRouteChange;
        router.setAfterRouteChange(async (oldRoute, newRoute) => {
            if (originalAfter) await originalAfter(oldRoute, newRoute);
            syncPinnedWithRoute(newRoute);
        });
        syncPinnedWithRoute(router.getCurrentRoute ? router.getCurrentRoute() : '');
    }
}

function setupMenuStarrySky() {
    const canvas = document.getElementById('menu-starry-sky');
    if (!(canvas instanceof HTMLCanvasElement)) return;
    if (canvas.dataset.ready === 'true') return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    canvas.dataset.ready = 'true';

    let width = 0;
    let height = 0;
    let animationFrameId = 0;
    let time = 0;
    let dpr = window.devicePixelRatio || 1;
    let isAnimating = false;
    const stars = [];
    const nebulas = [];
    const visibleStars = [];

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const isMobile = window.matchMedia('(max-width: 768px)').matches;
    const STAR_COUNT = isMobile ? 200 : 320;
    const NEBULA_COUNT = isMobile ? 2 : 3;

    const initNebulas = () => {
        nebulas.length = 0;
        for (let i = 0; i < NEBULA_COUNT; i += 1) {
            nebulas.push({
                x: Math.random() * width,
                y: Math.random() * height,
                radius: Math.min(width, height) * (0.35 + Math.random() * 0.35),
                hue: 215 + Math.random() * 50,
                vx: (Math.random() - 0.5) * 0.03,
                vy: (Math.random() - 0.5) * 0.03
            });
        }
    };

    const initStars = () => {
        stars.length = 0;
        for (let i = 0; i < STAR_COUNT; i += 1) {
            const isSpecial = Math.random() < 0.18;
            const z = Math.random() * Math.max(1, width);
            const baseSize = isSpecial ? 1.6 + Math.random() * 2.4 : 0.6 + Math.random() * 0.9;

            const driftRange = prefersReducedMotion ? 0 : (isSpecial ? 0.18 : 0.12);

            stars.push({
                x: Math.random() * width,
                y: Math.random() * height,
                z,
                size: baseSize,
                baseSize,
                hue: isSpecial
                    ? (Math.random() < 0.5 ? 200 + Math.random() * 40 : 35 + Math.random() * 20)
                    : 220,
                saturation: isSpecial ? 65 + Math.random() * 30 : 15 + Math.random() * 20,
                brightness: isSpecial ? 80 + Math.random() * 15 : 70,
                pulseOffset: Math.random() * Math.PI * 2,
                pulseSpeed: 0.008 + Math.random() * 0.018,
                vx: (Math.random() - 0.5) * driftRange,
                vy: (Math.random() - 0.5) * driftRange
            });
        }
    };

    const resizeHandler = () => {
        width = window.innerWidth;
        height = window.innerHeight;
        dpr = window.devicePixelRatio || 1;
        canvas.width = Math.floor(width * dpr);
        canvas.height = Math.floor(height * dpr);
        canvas.style.width = `${width}px`;
        canvas.style.height = `${height}px`;
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        initStars();
        initNebulas();
    };

    const draw = () => {
        if (!isAnimating) return;
        time += prefersReducedMotion ? 0.2 : 1;
        ctx.clearRect(0, 0, width, height);

        ctx.globalCompositeOperation = 'screen';
        nebulas.forEach(neb => {
            neb.x += neb.vx;
            neb.y += neb.vy;

            if (neb.x < -neb.radius) neb.x = width + neb.radius;
            if (neb.x > width + neb.radius) neb.x = -neb.radius;
            if (neb.y < -neb.radius) neb.y = height + neb.radius;
            if (neb.y > height + neb.radius) neb.y = -neb.radius;

            const gradient = ctx.createRadialGradient(neb.x, neb.y, 0, neb.x, neb.y, neb.radius);
            gradient.addColorStop(0, `hsla(${neb.hue}, 45%, 18%, 0.12)`);
            gradient.addColorStop(1, 'transparent');
            ctx.fillStyle = gradient;
            ctx.beginPath();
            ctx.arc(neb.x, neb.y, neb.radius, 0, Math.PI * 2);
            ctx.fill();
        });
        ctx.globalCompositeOperation = 'source-over';

        const depthRange = Math.max(1, Math.max(width, height));
        visibleStars.length = 0;

        stars.forEach(star => {
            star.x += star.vx;
            star.y += star.vy;

            if (star.x < 0) star.x = width;
            if (star.x > width) star.x = 0;
            if (star.y < 0) star.y = height;
            if (star.y > height) star.y = 0;

            const depthScale = 1 - (star.z / depthRange) * 0.75;
            const size = star.baseSize * depthScale;
            const sineValue = Math.sin(time * star.pulseSpeed + star.pulseOffset);

            let opacity = 0;
            if (star.baseSize > 1.8) {
                opacity = 0.35 + (sineValue + 1) * 0.3;
            } else {
                opacity = Math.max(0, -0.15 + (sineValue + 1) * 0.45);
            }

            opacity *= depthScale;

            if (opacity > 0.02) {
                visibleStars.push({
                    x: star.x,
                    y: star.y,
                    size,
                    opacity,
                    hue: star.hue,
                    saturation: star.saturation,
                    brightness: star.brightness,
                    baseSize: star.baseSize
                });
            }
        });

        if (visibleStars.length > 0) {
            ctx.globalCompositeOperation = 'screen';
            visibleStars.forEach(star => {
                const glowScale = star.baseSize > 1.8 ? 9 : 6;
                const glowRadius = star.size * glowScale;
                const glowOpacity = star.opacity * (star.baseSize > 1.8 ? 0.5 : 0.35);
                const gradient = ctx.createRadialGradient(star.x, star.y, 0, star.x, star.y, glowRadius);
                gradient.addColorStop(0, `hsla(${star.hue}, 80%, 75%, ${glowOpacity})`);
                gradient.addColorStop(0.55, `hsla(${star.hue}, 65%, 65%, ${glowOpacity * 0.35})`);
                gradient.addColorStop(1, 'transparent');
                ctx.fillStyle = gradient;
                ctx.beginPath();
                ctx.arc(star.x, star.y, glowRadius, 0, Math.PI * 2);
                ctx.fill();
            });
            ctx.globalCompositeOperation = 'source-over';

            visibleStars.forEach(star => {
                ctx.beginPath();
                ctx.fillStyle = `hsla(${star.hue}, ${star.saturation}%, ${star.brightness}%, ${star.opacity})`;

                if (star.size > 1.5 && star.opacity > 0.3) {
                    ctx.shadowBlur = star.size * 10;
                    ctx.shadowColor = `hsla(${star.hue}, 85%, 75%, ${star.opacity * 0.9})`;
                } else if (star.opacity > 0.15) {
                    ctx.shadowBlur = star.size * 5;
                    ctx.shadowColor = `hsla(${star.hue}, 70%, 70%, ${star.opacity * 0.6})`;
                } else {
                    ctx.shadowBlur = 0;
                }

                ctx.arc(star.x, star.y, star.size, 0, Math.PI * 2);
                ctx.fill();
                ctx.shadowBlur = 0;
            });
        }

        animationFrameId = requestAnimationFrame(draw);
    };

    const menuRoutes = new Set(['navigation', 'singleplayer', 'multiplayer']);
    let allowAnimation = true;

    const start = () => {
        if (isAnimating) return;
        isAnimating = true;
        draw();
    };

    const stop = () => {
        if (!isAnimating) return;
        isAnimating = false;
        cancelAnimationFrame(animationFrameId);
    };

    const syncAnimationWithRoute = (route) => {
        const shouldAnimate = !route || menuRoutes.has(route);
        allowAnimation = shouldAnimate;
        if (!shouldAnimate) {
            stop();
            return;
        }
        if (document.hidden) return;
        start();
    };

    window.addEventListener('resize', resizeHandler);
    resizeHandler();

    if (router && typeof router.setAfterRouteChange === 'function') {
        const originalAfter = router.afterRouteChange;
        router.setAfterRouteChange(async (oldRoute, newRoute) => {
            if (originalAfter) await originalAfter(oldRoute, newRoute);
            syncAnimationWithRoute(newRoute);
        });
        syncAnimationWithRoute(router.getCurrentRoute ? router.getCurrentRoute() : '');
    } else {
        syncAnimationWithRoute('');
    }

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) {
            stop();
            return;
        }
        if (allowAnimation) start();
    });
}

function setupMetaProgressionUI() {
    const panel = document.getElementById('meta-progression-panel');
    if (!panel) return;

    const inputUserId = document.getElementById('meta-user-id');
    const btnRefresh = document.getElementById('meta-refresh');
    const summaryEl = document.getElementById('meta-summary');
    const questsEl = document.getElementById('meta-quests');
    const btnClaimDaily = document.getElementById('meta-claim-daily');

    if (!inputUserId || !btnRefresh || !summaryEl || !questsEl || !btnClaimDaily) return;

    const setSummary = (text, isError = false) => {
        summaryEl.textContent = text;
        summaryEl.style.color = isError ? 'rgba(255, 79, 0, 0.9)' : 'rgba(255, 255, 255, 0.85)';
    };

    const resolveDefaultUserId = () => {
        const cached = loadCachedHandshake();
        if (cached?.playerId && isValidGuid(cached.playerId)) return cached.playerId;
        const fromQuery = new URLSearchParams(window.location.search).get('playerId');
        if (fromQuery && isValidGuid(fromQuery)) return fromQuery;
        return '';
    };

    if (!inputUserId.value) {
        inputUserId.value = resolveDefaultUserId();
    }

    const renderQuests = (userId, quests) => {
        questsEl.innerHTML = '';
        const list = Array.isArray(quests) ? quests : [];

        if (list.length === 0) {
            questsEl.innerHTML = '<div class="meta-summary">暂无任务</div>';
            return;
        }

        for (const quest of list) {
            const canClaim = !!quest?.isCompleted && !quest?.isRewardClaimed && !quest?.isExpired;
            const progressText = `${quest?.currentProgress ?? 0}/${quest?.targetValue ?? 0} · +${quest?.coinReward ?? 0} coins`;
            const statusText = quest?.statusDescription ?? '';

            const wrapper = document.createElement('div');
            wrapper.className = 'meta-quest';
            wrapper.innerHTML = `
                <div class="meta-quest-title">
                    <div>${quest?.name ?? 'Quest'}</div>
                    <small>${quest?.typeDisplayName ?? ''}</small>
                </div>
                <div class="meta-quest-progress">${progressText}</div>
                <div class="meta-quest-progress">${statusText}</div>
                <div class="meta-quest-actions">
                    <button class="meta-btn meta-quest-claim" type="button" data-quest-id="${quest?.id ?? ''}" ${canClaim ? '' : 'disabled'}>
                        领取
                    </button>
                </div>
            `;
            questsEl.appendChild(wrapper);
        }
    };

    const refresh = async () => {
        const userId = inputUserId.value?.trim();
        if (!userId || !isValidGuid(userId)) {
            btnClaimDaily.disabled = true;
            questsEl.innerHTML = '';
            setSummary('请输入有效 UserId（GUID）', true);
            return;
        }

        btnClaimDaily.disabled = true;
        setSummary('加载中…');

        try {
            const state = await apiGetJson(`/api/economy/state/${userId}`);
            const rank = state?.rank;
            const coins = rank?.coins ?? 0;
            const rankName = rank?.rankName ?? '青铜';
            const rankPoints = rank?.rankPoints ?? 0;
            const canClaimDaily = !!rank?.canClaimDailyReward;

            setSummary(`Coins: ${coins} · Rank: ${rankName} (${rankPoints}) · Daily: ${canClaimDaily ? '可领取' : '已领取'}`);
            btnClaimDaily.disabled = !canClaimDaily;
            renderQuests(userId, state?.quests);
        } catch (err) {
            questsEl.innerHTML = '';
            btnClaimDaily.disabled = true;
            setSummary(`加载失败：${err?.message || String(err)}`, true);
        }
    };

    btnRefresh.addEventListener('click', () => refresh());
    inputUserId.addEventListener('change', () => refresh());

    btnClaimDaily.addEventListener('click', async () => {
        const userId = inputUserId.value?.trim();
        if (!userId || !isValidGuid(userId)) return;
        try {
            btnClaimDaily.disabled = true;
            await apiPostJson(`/api/economy/daily-reward/${userId}/claim`, {});
        } catch (err) {
            setSummary(`领取失败：${err?.message || String(err)}`, true);
        } finally {
            await refresh();
        }
    });

    questsEl.addEventListener('click', async (event) => {
        const target = event.target;
        const btn = target instanceof Element ? target.closest('button[data-quest-id]') : null;
        const questId = btn?.getAttribute('data-quest-id');
        const userId = inputUserId.value?.trim();
        if (!btn || !questId || !userId || !isValidGuid(userId) || !isValidGuid(questId)) return;

        btn.setAttribute('disabled', 'disabled');
        try {
            await apiPostJson(`/api/economy/quests/${userId}/${questId}/claim`, {});
        } catch (err) {
            setSummary(`领取失败：${err?.message || String(err)}`, true);
        } finally {
            await refresh();
        }
    });

    refresh().catch(() => { });
}

// 设置路由系统的游戏初始化钩子
document.addEventListener('initializeGame', async () => {
    if (!window.gameInitialized) {
        await initializeApp();
        window.gameInitialized = true;
    }
});

// 设置路由系统的游戏清理钩子
document.addEventListener('cleanupGame', () => {
    // 这里可以添加游戏清理逻辑
    // Best-effort: if this is a PVP room, explicitly close it so the opponent can be notified.
    leaveCurrentPvpRoom("cleanupGame").catch(() => { });
    if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
        connection.stop().catch(err => {
            console.error('MAIN: Error stopping SignalR connection:', err);
        });
    }
    if (guestConnection && guestConnection.state !== signalR.HubConnectionState.Disconnected) {
        guestConnection.stop().catch(err => {
            console.error('MAIN: Error stopping guest SignalR connection:', err);
        });
    }
    connection = null;
    guestConnection = null;
    initialStateReceived = false;
    resetTransientConnectionUiState();
    clearTransientMatchQueryParams();
    // clearCachedHandshake() is called inside leaveCurrentPvpRoom (or directly for VS-AI)
    window.gameInitialized = false;
});

document.addEventListener('cleanupMultiplayer', () => {
    // When transitioning from lobby -> game, DO NOT leave/close the room.
    if (suppressMultiplayerLeaveOnce) {
        suppressMultiplayerLeaveOnce = false;
        stopMultiplayerPolling();
        return;
    }

    // Leaving the multiplayer page (e.g., back to main menu) is treated as leaving the current room
    // to avoid stale pendingHandshake reuse and to notify the other participant.
    leaveCurrentPvpRoom("cleanupMultiplayer").catch(() => { });
    stopMultiplayerPolling();
});

document.addEventListener('requestReconnect', () => {
    performManualReconnect().catch(() => { });
});

// 初始化开发者控制台（如果需要）
document.addEventListener('DOMContentLoaded', () => {

    ensureEnumLocalizations(['CardRarity', 'CardClass', 'CardType'])
        .catch(error => {
            console.warn('MAIN: Failed to prefetch card enum labels.', error);
        });
    
    // Initialize dev console early to bind global handlers
    try {
        // We pass true to ensure the instance is created, but visibility is controlled later
        const consoleInstance = initDevConsole(true, false);
        
        // Ensure window.devConsole points to the instance for the toggle button
        if (consoleInstance && !window.devConsole) {
            window.devConsole = consoleInstance;
        }
        
        // Update console button state based on route
        const updateConsoleButtonState = (route) => {
            const btn = document.getElementById('dev-console-toggle');
            if (!btn) return;

            // Only enable in game route
            if (route === 'game') {
                btn.removeAttribute('disabled');
                btn.style.opacity = '1';
                btn.style.pointerEvents = 'auto';
            } else {
                btn.setAttribute('disabled', 'true');
                btn.style.opacity = '0.3';
                btn.style.pointerEvents = 'none';
                // Also hide console if leaving game
                if (window.devConsole && window.devConsole.hide) {
                    window.devConsole.hide();
                }
            }
        };


        // Hook into router
        if (router) {
            // Initial check
            updateConsoleButtonState(router.getCurrentRoute());
            
            // Listen for route changes
            const originalAfter = router.afterRouteChange;
            router.setAfterRouteChange(async (oldRoute, newRoute) => {
                if (originalAfter) await originalAfter(oldRoute, newRoute);
                updateConsoleButtonState(newRoute);
            });
        }
    } catch (error) {
        console.error('MAIN: Failed to initialize dev console:', error);
    }

    try {
        setupMultiplayerUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize multiplayer UI:', error);
    }

    try {
        setupSingleplayerUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize singleplayer UI:', error);
    }

    try {
        setupUserInfoUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize user info UI:', error);
    }

    try {
        setupAboutUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize about UI:', error);
    }

    try {
        setupTopNavHoverLock();
    } catch (error) {
        console.error('MAIN: Failed to initialize top nav hover lock:', error);
    }

    try {
        setupMenuRouteOverlays();
    } catch (error) {
        console.error('MAIN: Failed to initialize menu route overlays:', error);
    }

    try {
        setupMenuStarrySky();
    } catch (error) {
        console.error('MAIN: Failed to initialize menu starry sky:', error);
    }

    // Meta progression panel is currently hidden on main menu; keep wiring disabled.

});

document.addEventListener('deckListUpdated', () => {
    deckSelectionState.loaded = false;
    deckSelectionState.loading = null;
    refreshDeckSelectionUI().catch(() => {});
});

// 注意：不再自动启动游戏，而是通过路由系统按需启动
