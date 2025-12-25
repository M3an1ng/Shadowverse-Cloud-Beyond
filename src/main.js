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
import { getDeckOwnerPayload, getDeckOwnerQuery, getUserSession } from './userSession.js';
import {
    gameState, initializeGame, initializeSpectator, setSignalRConnection, updateFullGameState, endTurnRequest,
    // Import specific event handlers instead of processGameEvent
    handleDeckUpdate, handleTurnStarted, handleTurnEnded, handlePpUpdate, handleEpUpdate, handleSepUpdate, handleHandUpdated,
    handleGraveyardUpdate, handleAttackDeclared, handleUnitDestroyed, handleDamageDealt,
    handleCardSummoned, handleCardPlayed, handleHealthChanged,
    handleStatChange, handleUnitEvolved, handleGameOver, handlePlayerMulliganCompleted, handleAttackStateUpdated,
    handleActStateUpdated, handleEvolveStateUpdated, handleSuperEvolveStateUpdated, handleHandPlayabilityUpdate, handlePpMaxUpdate, handleEpMaxUpdate, handleSepMaxUpdate, handlePlayerChoiceRequested, handleCardRevealed, handleCardsFused, appendGameLog // 【新增】导入最大值更新处理函数
} from './gameManager.js';
// loadCards is obsolete now that the server is the single source of truth.
import { renderGame } from './ui.js'; // Removed non-existent displayError/Info/initializeUI, Added UI function imports - functions to be implemented in ui
import { initDevConsole } from './devConsole.js'; // 开发者控制台
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
let initialStateReceived = false; // Flag to track if initial state has been handled
let forceFullStateOnNextReceive = false;
let lobbyHandshake = null; // { gameId, playerId, token, joinCode?, role, useAiFallback? }
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

async function getDefaultDeck() {
    await bootstrapDecks();
    const decks = await listDecks();
    if (!Array.isArray(decks) || decks.length === 0) {
        return null;
    }
    return decks[0];
}

async function submitDeckSelection(handshake, includeAi) {
    const deck = await getDefaultDeck();
    if (!deck?.deckId) {
        return { ok: false, message: "没有可用卡组，请先在牌组编辑中创建。" };
    }

    const ownerPayload = getDeckOwnerPayload();
    const payload = {
        playerId: handshake.playerId,
        token: handshake.token,
        deckId: deck.deckId,
        ...ownerPayload
    };

    if (includeAi) {
        payload.aiDeckId = deck.deckId;
        if (ownerPayload.userId) {
            payload.aiUserId = ownerPayload.userId;
        } else {
            payload.aiGuestId = ownerPayload.guestId;
        }
    }

    await apiPostJson(`/api/match/${encodeURIComponent(handshake.gameId)}/deck`, payload);
    return { ok: true, deckId: deck.deckId };
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

async function createMatch(useAiFallback) {
    const payload = await apiPostJson("/api/match", { useAiFallback });
    return {
        gameId: payload.gameId,
        playerId: payload.hostPlayerId,
        token: payload.hostToken,
        joinCode: payload.joinCode,
        role: "host",
        useAiFallback: payload.useAiFallback
    };
}

async function joinMatch(gameId, joinCode) {
    const payload = await apiPostJson(`/api/match/${encodeURIComponent(gameId)}/join`, { joinCode });
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
        console.log(`MAIN: Left PVP room ${gameId}.`, reasonForLog ? { reasonForLog } : undefined);
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
async function setupSignalRConnection(gameId, playerId, token, resolveInitialState) { // ADDED token parameter
    if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
        console.warn("MAIN: SignalR connection already exists and is not disconnected. Stopping existing one.");
        await connection.stop();
    }

    // Reset initial-state gate for every new connection/session
    initialStateReceived = false;

    const hubUrlWithParams = `${HUB_URL}?gameId=${encodeURIComponent(gameId)}&playerId=${encodeURIComponent(playerId)}&token=${encodeURIComponent(token)}`;
    console.log(`MAIN: Attempting to connect to SignalR Hub: ${hubUrlWithParams}`);

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrlWithParams)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Trace) // Changed level to Trace
        .build();

    registerSignalREventHandlers(connection, resolveInitialState); // Pass resolver down

    try {
        await connection.start();
        console.log("MAIN: SignalR Connected successfully. Connection ID:", connection.connectionId);
        return connection;
    } catch (err) {
        console.error("MAIN: SignalR Connection Failed: ", err);
        alert(`连接游戏服务器失败: ${err.message || 'Unknown error'}`); // Use alert instead of displayError
        connection = null;
        throw err;
    }
}

async function setupSpectatorSignalRConnection(gameId, resolveInitialState) {
    if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
        console.warn("MAIN: SignalR connection already exists and is not disconnected. Stopping existing one.");
        await connection.stop();
    }

    initialStateReceived = false;

    const hubUrlWithParams = `${HUB_URL}?gameId=${encodeURIComponent(gameId)}&spectate=1`;
    console.log(`MAIN: Attempting to connect to SignalR Hub (spectate): ${hubUrlWithParams}`);

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrlWithParams)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Trace)
        .build();

    registerSignalREventHandlers(connection, resolveInitialState);

    try {
        await connection.start();
        console.log("MAIN: SignalR Connected successfully (spectate). Connection ID:", connection.connectionId);
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
        "GraveyardUpdate",
        "CardPlayed",
        "CardSummoned",
        "AttackDeclared",
        "UnitDestroyed",
        "DamageDealt",
        "HealthChanged",
        "StatChange",
        "UnitEvolved",
        "HandUpdated",
        "PlayerMulliganCompleted",
        "GameOver",
        "AttackStateUpdated",
        "ReceiveHandPlayabilityUpdate",
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
        console.log(`%c[SIGNALR_HANDLER_MAIN] ${timestamp} - Event: ReceiveGameState - Phase: ${receivedGameState?.Phase}, ActiveTurn: ${receivedGameState?.ActivePlayer?.TurnNumber}`, 'color: orange; background: #333;'); // Phase, ActivePlayer, TurnNumber
        if (!initialStateReceived && typeof resolveInitialState === 'function') {
            // Initial state handling during initializeApp
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

    // Removed conn.on("HandUpdate", ...) block as it's deprecated
    wireEvent("GraveyardUpdate", handleGraveyardUpdate);
    wireEvent("CardPlayed", handleCardPlayed);
    wireEvent("CardSummoned", handleCardSummoned);
    wireEvent("AttackDeclared", handleAttackDeclared); // Renamed from UnitAttacked to match backend DTO/Interface
    wireEvent("UnitDestroyed", handleUnitDestroyed);
    wireEvent("DamageDealt", handleDamageDealt);
    wireEvent("HealthChanged", handleHealthChanged); // Added handler
    wireEvent("StatChange", handleStatChange); // Added handler
    wireEvent("UnitEvolved", handleUnitEvolved); // Added handler (was missing)
    wireEvent("HandUpdated", handleHandUpdated);
    wireEvent("PlayerMulliganCompleted", handlePlayerMulliganCompleted);
    wireEvent("GameOver", handleGameOver);
    wireEvent("AttackStateUpdated", handleAttackStateUpdated);
    wireEvent("ActStateUpdated", handleActStateUpdated);
    // 【BUG修复】注册处理手牌可玩性更新的事件监听器
    wireEvent("ReceiveHandPlayabilityUpdate", handleHandPlayabilityUpdate);
    wireEvent("EvolveStateUpdated", handleEvolveStateUpdated);
    wireEvent("SuperEvolveStateUpdated", handleSuperEvolveStateUpdated);
    // NEW: Handler for when the server requests the player to make a choice
    wireEvent("PlayerChoiceRequested", handlePlayerChoiceRequested);
    wireEvent("CardRevealed", handleCardRevealed);
    wireEvent("CardsFused", handleCardsFused);
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

    // Handler for when the server requests the client to select target(s)
    conn.on("RequestTargetSelection", (request) => {
        console.log("MAIN: Received TargetSelectionRequest:", request);
        // Removed call to showTargetSelection, which is not exported/used.
    });

    // --- Connection Lifecycle Handlers ---
    conn.onclose((error) => {
        console.error(`MAIN: SignalR Connection Closed. Error: "${error}". State: ${conn.state}.`);
        alert("与服务器连接已断开。" + (error ? ` 原因: ${error.message}` : "")); // Use alert instead of displayError
        // Automatic reconnect is enabled, but we might want to disable UI elements here.
        connection = null; // Clear the connection variable
    });

    conn.onreconnecting((error) => {
        console.warn("MAIN: SignalR Connection Reconnecting...", error);
        alert("连接中断，正在尝试重新连接服务器..."); // Use alert instead of displayInfo
        // Disable UI interactions while reconnecting?
    });

    conn.onreconnected((connectionId) => {
        console.log("MAIN: SignalR Connection Reconnected. New Connection ID:", connectionId);
        alert("已重新连接到服务器。"); // Use alert instead of displayInfo
        forceFullStateOnNextReceive = true;
        // Request latest state after reconnection if server doesn't push automatically
        // connection.invoke("RequestInitialGameState").catch(err => console.error("Request state after reconnect failed:", err));
    });

    console.log("MAIN: SignalR event handlers registered.");
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
    console.log("MAIN: Sending target selection response:", responseDto);
    try {
        await connection.invoke("RespondToTargetSelection", responseDto); // Assuming Hub method exists
        console.log("MAIN: RespondToTargetSelection invoked successfully.");
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
    if (cached && (isInMultiplayerUi || cached.useAiFallback === true)) {
        return cached;
    }

    // 3) Fallback: auto-create vs AI for single-player quick start
    const aiHandshake = await createMatch(true);
    await markReady(aiHandshake.gameId, aiHandshake.playerId, aiHandshake.token);
    const selection = await submitDeckSelection(aiHandshake, true);
    if (!selection.ok) {
        throw new Error(selection.message || "无法选择卡组。");
    }
    await startMatch(aiHandshake.gameId, aiHandshake.playerId, aiHandshake.token);
    cacheHandshake(aiHandshake);
    return aiHandshake;
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
    console.log("MAIN: Initializing application...");

    try {
        // 0. Initialize basic UI elements (maybe show loading indicator)
        console.log("MAIN: Initializing basic UI...");
        // initializeUI(); // REMOVED: ui.js does not export this. UI setup might need review later.

        // --- M6 Coordination Logic Start ---
        console.log("MAIN: Starting M6 Coordination...");

        // 2. Get Game and Player IDs via lobby handshake (BEFORE starting async ops)
        console.log("MAIN: Getting Game/Player IDs via lobby handshake...");
        const handshake = await ensureLobbyHandshake();
        const gameId = handshake.gameId;
        const playerId = handshake.playerId;
        const token = handshake.token;
        console.log(`MAIN: Using GameID=${gameId}, PlayerID=${playerId}, role=${handshake.role || 'auto'}`);

        // 3. Render initial empty/loading state (optional)
        // renderGame(createEmptyGameState());

        // Create a promise that resolves when the initial game state is received
        let resolveInitialStatePromise;
        const initialStatePromise = new Promise(resolve => {
            resolveInitialStatePromise = resolve;
        });

        // 4. Start SignalR connection setup (PASSING the resolver)
        console.log("MAIN: Establishing SignalR connection (part of coordination)...");
        const connectionPromise = handshake.role === "spectator"
            ? setupSpectatorSignalRConnection(gameId, resolveInitialStatePromise)
            : setupSignalRConnection(gameId, playerId, token, resolveInitialStatePromise); // Pass resolver + token

        // 1. Start loading card data concurrently
        console.log("MAIN: Loading card data (part of coordination)...");
        try {
            // Wait for SignalR connection and the initial state reception
            console.log("MAIN: Waiting for Promise.all([initialStatePromise, connectionPromise])...");
            // connectionPromise ensures we handle setup errors even if initialStatePromise never resolves/rejects
            // We need the connection object resolved from connectionPromise to pass to gameManager
            const [serverState, currentConnection] = await Promise.all([initialStatePromise, connectionPromise]);
            // serverState is from initialStatePromise resolver, currentConnection from connectionPromise resolver

            console.log("MAIN: Promise.all resolved. SignalR connected, initial state received.");
            console.log("MAIN: Received initial server state:", serverState); // Log the received state

            // 5. Pass connection to gameManager (AFTER connection is confirmed ready)
            console.log("MAIN: Setting SignalR connection for gameManager...");
            setSignalRConnection(currentConnection);

            // 6. Initialize gameManager with context (AFTER connection and IDs are ready)
            console.log("MAIN: Initializing game logic structure...");
            if (handshake.role === "spectator") {
                initializeSpectator(gameId);
            } else {
                initializeGame(gameId, playerId); // Still needed to set IDs in gameManager
            }

            // CRITICAL: Restore the call to updateFullGameState here.
            // This ensures the *initial* state (received via the promise)
            // is processed and rendered correctly (e.g., showing the Mulligan phase).
            // The ReceiveGameState handler handles *subsequent* updates directly.
            console.log("MAIN: Applying initial server state via updateFullGameState..."); // Added log
            updateFullGameState(serverState); // <-- RESTORED

            // 7. Setup core event listeners (AFTER connection and UI are ready)
            console.log("MAIN: Setting up core event listeners...");
            setupEventListeners();

            // 8. Initialize developer console
            DEBUG_CONSOLE_ENABLED = DEBUG_FLAG || !!handshake.useAiFallback; // 单人模式也强制开启
            DEBUG_CONSOLE_AUTOSHOW = !!handshake.useAiFallback;
            
            // Always initialize dev console to ensure the global object exists,
            // but control visibility/enabled state via logic
            console.log("MAIN: Initializing developer console. Enabled:", DEBUG_CONSOLE_ENABLED, "AutoShow:", DEBUG_CONSOLE_AUTOSHOW);
            initDevConsole(true, DEBUG_CONSOLE_AUTOSHOW);

            console.log("MAIN: M6 Coordination complete. Game ready.");

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
            console.log("MAIN: Initialization failed likely due to SignalR connection error.");
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
    console.log("MAIN: Setting up event listeners...");
    const endTurnButton = document.getElementById('end-turn-button');
    if (endTurnButton) {
        // Removed redundant event listener for end-turn-button.
        // Event delegation in ui.js handles this now.
    } else {
        console.error("MAIN: Element with ID 'end-turn-button' not found!");
    }

    // UI event delegation (e.g., clicks on field, hand) is handled within ui.js
    // We assume ui.js's initializeUI or specific handlers are called elsewhere appropriately.
    console.log("MAIN: Core event listeners set up.");
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
    const inputJoinGame = document.getElementById('mp-join-game');
    const inputJoinCode = document.getElementById('mp-join-code');
    const btnSpectate = document.getElementById('mp-spectate-btn');
    const inputSpectateGame = document.getElementById('mp-spectate-game');
    const outSpectate = document.getElementById('mp-spectate-output');

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

    bindButton(btnCreateHost, async () => {
        if (!outCreate) return;
        setOutput(outCreate, '创建房间中...');
        try {
            const handshake = await createMatch(false);
            await markReady(handshake.gameId, handshake.playerId, handshake.token);
            const selection = await submitDeckSelection(handshake, false);
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
            const selection = await submitDeckSelection(handshake, false);
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
                const selection = await submitDeckSelection(handshake, false);
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

function setupUserInfoUI() {
    const toggle = document.getElementById('user-info-toggle');
    const card = document.getElementById('user-info-card');
    const nameEl = document.getElementById('user-info-name');
    const roleEl = document.getElementById('user-info-role');
    const idEl = document.getElementById('user-info-id');
    const avatarEl = document.getElementById('user-info-avatar');

    if (!toggle || !card || !nameEl || !roleEl || !idEl || !avatarEl) {
        return;
    }

    const applySession = () => {
        const session = getUserSession();
        const isUser = session.mode === "user";
        // toggle button is now an icon, no text content update needed
        nameEl.textContent = session.displayName || (isUser ? "用户" : "游客");
        roleEl.textContent = isUser ? "USER" : "GUEST";
        idEl.textContent = session.userId || session.guestId || "-";
        avatarEl.textContent = session.avatar || (isUser ? "U" : "G");
    };

    const setOpen = (open) => {
        card.classList.toggle('active', open); // Changed from 'open' to 'active' to match CSS
        card.setAttribute('aria-hidden', open ? 'false' : 'true');
    };

    applySession();

    toggle.addEventListener('click', (event) => {
        event.stopPropagation();
        applySession();
        const isOpen = card.classList.contains('active');
        setOpen(!isOpen);
    });

    // Copy ID functionality
    idEl.addEventListener('click', async (event) => {
        event.stopPropagation();
        const idText = idEl.textContent;
        if (idText && idText !== '-') {
            try {
                await navigator.clipboard.writeText(idText);
                const originalTitle = idEl.getAttribute('title');
                
                // Visual feedback
                idEl.style.color = 'var(--menu-accent-cyan)';
                idEl.textContent = 'Copied!';
                
                setTimeout(() => {
                    idEl.textContent = idText;
                    idEl.style.color = '';
                }, 1500);
            } catch (err) {
                console.error('Failed to copy ID:', err);
            }
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
    console.log('MAIN: Received initializeGame event from router');
    if (!window.gameInitialized) {
        await initializeApp();
        window.gameInitialized = true;
    }
});

// 设置路由系统的游戏清理钩子
document.addEventListener('cleanupGame', () => {
    console.log('MAIN: Received cleanupGame event from router');
    // 这里可以添加游戏清理逻辑
    // Best-effort: if this is a PVP room, explicitly close it so the opponent can be notified.
    leaveCurrentPvpRoom("cleanupGame").catch(() => { });
    if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
        console.log('MAIN: Stopping SignalR connection...');
        connection.stop().catch(err => {
            console.error('MAIN: Error stopping SignalR connection:', err);
        });
    }
    connection = null;
    initialStateReceived = false;
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

// 初始化开发者控制台（如果需要）
document.addEventListener('DOMContentLoaded', () => {
    console.log('MAIN: DOM loaded, initializing dev console...');
    
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
        setupUserInfoUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize user info UI:', error);
    }

    // Meta progression panel is currently hidden on main menu; keep wiring disabled.
});

// 注意：不再自动启动游戏，而是通过路由系统按需启动
console.log('MAIN: Module loaded, waiting for router to initialize game...');
