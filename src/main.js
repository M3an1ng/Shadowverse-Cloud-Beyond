// src/main.js - 主入口模块
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - API集成: 文档/03-前端架构/03-API集成.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
//
console.log("main.js loaded");

// 导入核心模块
import * as signalR from "@microsoft/signalr";
import { LogLevel } from "@microsoft/signalr"; // Added for Trace logging
import {
    gameState, initializeGame, setSignalRConnection, updateFullGameState, endTurnRequest,
    // Import specific event handlers instead of processGameEvent
    handleDeckUpdate, handleTurnStarted, handleTurnEnded, handlePpUpdate, handleEpUpdate, // handleHandUpdate removed
    handleGraveyardUpdate, handleAttackDeclared, handleUnitDestroyed, handleDamageDealt,
    handleCardSummoned, handleCardPlayed, handleHealthChanged,
    handleStatChange, handleUnitEvolved, handleGameOver, handlePlayerMulliganCompleted
} from './gameManager.js';
import { loadCards } from './cardManager.js'; // Keep existing correct import
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

let isInitializing = false;
const HUB_URL = "/gamehub"; // Use relative URL, assumes same origin or proxy configured
let connection = null; // Module-level variable for SignalR connection - Moved here
let initialStateReceived = false; // Flag to track if initial state has been handled

// --- SignalR Connection Logic ---

/**
 * Sets up and starts the SignalR connection.
 * @param {string} gameId - The ID of the game.
 * @param {string} playerId - The ID of the player.
 * @param {function} resolveInitialState - The resolve function for the initialStatePromise.
 * @returns {Promise<signalR.HubConnection>} A promise that resolves with the connection object, or rejects on failure.
 */
async function setupSignalRConnection(gameId, playerId, resolveInitialState) { // ADDED resolveInitialState parameter
    if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
        console.warn("MAIN: SignalR connection already exists and is not disconnected. Stopping existing one.");
        await connection.stop();
    }

    const hubUrlWithParams = `${HUB_URL}?gameId=${encodeURIComponent(gameId)}&playerId=${encodeURIComponent(playerId)}`;
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

/**
 * Registers handlers for events received from the SignalR Hub.
 * @param {signalR.HubConnection} conn - The SignalR connection instance.
 */
function registerSignalREventHandlers(conn, resolveInitialState) { // Accept resolver as parameter
    // Handler for full game state updates (e.g., on connect/reconnect)
    // --- MODIFIED FOR M6 ---
    conn.on("ReceiveGameState", (receivedGameState) => {
        const timestamp = new Date().toISOString();
        console.log(`%c[SIGNALR_HANDLER_MAIN] ${timestamp} - Event: ReceiveGameState - Phase: ${receivedGameState?.Phase}, ActiveTurn: ${receivedGameState?.ActivePlayer?.TurnNumber}`, 'color: orange; background: #333;'); // Phase, ActivePlayer, TurnNumber
        console.log('[main.js] ReceiveGameState handler invoked.');
        console.log("MAIN: Received Full GameState via SignalR:", 'color: green; font-weight: bold;', receivedGameState);

        if (!initialStateReceived && typeof resolveInitialState === 'function') {
            // Initial state handling during initializeApp
            console.log("MAIN: Resolving initialStatePromise with received state (Initial Load)...");
            resolveInitialState(receivedGameState); // Resolve the promise in initializeApp
            initialStateReceived = true; // Mark initial state as received
            console.log("MAIN: Initial state processed via Promise.");
            // NOTE: updateFullGameState is called in initializeApp after Promise.all resolves based on this promise.
        } else if (initialStateReceived) {
            // Subsequent state handling after initialization
            console.log("MAIN: Received subsequent full game state update. Processing directly...");
            // Directly call gameManager's update function to handle the state and re-render
            // Ensure gameManager and updateFullGameState are imported correctly
            try {
                // --- ROO DEBUG: Log TurnNumber from received DTO (Subsequent Update) ---
                try { // Inner try for debug logging (Line 100)
                    if (receivedGameState && receivedGameState.Players && receivedGameState.CurrentPlayerId) { // Players, CurrentPlayerId
                        const activePlayerDto = receivedGameState.Players.find(p => p && p.PlayerId === receivedGameState.CurrentPlayerId); // Players, PlayerId, CurrentPlayerId
                        if (activePlayerDto) {
                            // Log the TurnNumber property directly as received
                            console.warn(`%c[MAIN_DEBUG_ReceiveGameState_Subsequent] Active Player DTO (ID: ${activePlayerDto.PlayerId}) received. TurnNumber property value:`, 'color: fuchsia; font-weight: bold;', activePlayerDto.TurnNumber); // PlayerId
                            // Also log the entire active player DTO for context
                            console.warn(`%c[MAIN_DEBUG_ReceiveGameState_Subsequent] Full Active Player DTO received:`, 'color: fuchsia;', activePlayerDto);
                        } else {
                            console.warn(`%c[MAIN_DEBUG_ReceiveGameState_Subsequent] Active player DTO not found for CurrentPlayerId: ${receivedGameState.CurrentPlayerId}`, 'color: fuchsia; font-weight: bold;'); // CurrentPlayerId
                        }
                    } else {
                        console.warn(`%c[MAIN_DEBUG_ReceiveGameState_Subsequent] Received serverState is missing Players or CurrentPlayerId.`, 'color: fuchsia; font-weight: bold;'); // Players, CurrentPlayerId
                    }
                } catch (e) { // Catch for inner try (Line 100)
                    console.error(`%c[MAIN_DEBUG_ReceiveGameState_Subsequent] Error during debug logging:`, 'color: red; font-weight: bold;', e); // Moved console.error here
                }
                // --- END ROO DEBUG ---
                updateFullGameState(receivedGameState); // Directly update the game state and UI
                console.log("MAIN: Subsequent game state processed via updateFullGameState.");
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
    conn.on("TurnStarted", (eventData) => {
        const timestamp = new Date().toISOString();
        // --- ROO DEBUG: Log raw event data received by main.js ---
        console.log(`%c[DEBUG_MAIN_EVENT] MAIN.JS Received TurnStarted Event. Raw Data:`, 'color: purple; font-weight: bold;', JSON.parse(JSON.stringify(eventData))); // Log deep copy
        // --- END ROO DEBUG ---
        // Call the specific handler in gameManager, which now needs to process the full state
        handleTurnStarted(eventData);
    });
    conn.on("TurnEnded", (eventData) => {
        // Assuming TurnEndedEventDto might have Phase, check if needed later
        handleTurnEnded(eventData); // Call specific handler
        conn.on("DeckUpdate", handleDeckUpdate); // Restored handler registration
    });
    conn.on("PpUpdate", (eventData) => {
        // Properties: PlayerId, CurrentPp, MaxPp
        console.log("MAIN: Received PpUpdate:", eventData);
        handlePpUpdate(eventData); // Call specific handler
    });
    conn.on("EpUpdate", (eventData) => { // Renamed from EvolvePointUpdate to match backend DTO/Interface
        // Properties: PlayerId, CurrentEp, MaxEp
        console.log("MAIN: Received EpUpdate:", eventData);
        handleEpUpdate(eventData); // Call specific handler
    });
    // Removed conn.on("HandUpdate", ...) block as it's deprecated
    conn.on("GraveyardUpdate", (eventData) => {
        // Properties: PlayerId, GraveyardCount
        console.log("MAIN: Received GraveyardUpdate:", eventData);
        handleGraveyardUpdate(eventData); // Call specific handler
    });
    conn.on("CardPlayed", (eventData) => {
        // Properties: PlayerId, Card, HandIndex, Targets (optional)
        console.log("MAIN: Received CardPlayed event:", eventData); // Updated log prefix
        handleCardPlayed(eventData); // Call gameManager method
    });
    conn.on("CardSummoned", (eventData) => {
        // Properties: PlayerId, EntityType (Unit/Amulet), EntityState (UnitStateDto/AmuletStateDto), BoardIndex
        console.log("MAIN: Received EntityEnteredPlay event:", eventData); // Updated log prefix
        handleCardSummoned(eventData); // Call gameManager method
    });
    conn.on("AttackDeclared", (eventData) => { // Renamed from UnitAttacked to match backend DTO/Interface
        console.log("MAIN: Received AttackDeclared:", eventData);
        handleAttackDeclared(eventData); // Call specific handler
    });
    conn.on("UnitDestroyed", (eventData) => {
        console.log("MAIN: Received UnitDestroyed:", eventData);
        handleUnitDestroyed(eventData); // Call specific handler
    });
    conn.on("DamageDealt", (eventData) => {
        console.log("MAIN: Received DamageDealt:", eventData);
        handleDamageDealt(eventData); // Call specific handler
    });
    conn.on("HealthChanged", (eventData) => { // Added handler
        console.log("MAIN: Received HealthChanged:", eventData);
        handleHealthChanged(eventData); // Call specific handler
    });
    conn.on("StatChange", (eventData) => { // Added handler
        console.log("MAIN: Received StatChange:", eventData);
        handleStatChange(eventData); // Call specific handler
    });
    conn.on("UnitEvolved", (eventData) => { // Added handler (was missing)
        console.log("MAIN: Received UnitEvolved:", eventData);
        handleUnitEvolved(eventData); // Call specific handler
    });
    // LeaderStatusUpdate seems specific, keep it if needed, or handle via other events?
    // For now, let's comment it out as other events cover health/stats.
    // conn.on("LeaderStatusUpdate", (eventData) => {
    //     console.log("MAIN: Received LeaderStatusUpdate:", eventData);
    //     // processGameEvent(eventData); // Or handleLeaderStatusUpdate(eventData) if needed
    // });
    conn.on("PlayerMulliganCompleted", (eventData) => {
        const timestamp = new Date().toISOString();
        // Properties: PlayerId
        console.log(`%c[SIGNALR_HANDLER_MAIN] ${timestamp} - Event: PlayerMulliganCompleted - Player: ${eventData?.PlayerId}`, 'color: pink; background: #333;'); // PlayerId
        console.log("MAIN: Received PlayerMulliganCompleted:", eventData);
        handlePlayerMulliganCompleted(eventData); // Call specific handler
    });
    conn.on("GameOver", (eventData) => {
        console.log("MAIN: Received GameOver:", eventData);
        handleGameOver(eventData); // Call specific handler
    });

    // The generic ReceiveGameEvent handler is implicitly removed by replacing this whole block.

    // Handler for server-side errors
    conn.on("ReceiveError", (errorMessage) => {
        console.error("MAIN: Received Error from Server:", errorMessage);
        alert(`服务器错误: ${errorMessage}`); // Use alert instead of displayError
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

        // 2. Get Game and Player IDs (BEFORE starting async ops)
        console.log("MAIN: Getting Game/Player IDs...");
        const urlParams = new URLSearchParams(window.location.search);
        const gameId = urlParams.get('gameId') || generateGuid(); // Generate GUID if not provided
        const playerId = urlParams.get('playerId') || 'player1';
        console.log(`MAIN: Using GameID=${gameId}, PlayerID=${playerId}`);

        // 3. Render initial empty/loading state (optional)
        // renderGame(createEmptyGameState());

        // Create a promise that resolves when the initial game state is received
        let resolveInitialStatePromise;
        const initialStatePromise = new Promise(resolve => {
            resolveInitialStatePromise = resolve;
        });

        // 4. Start SignalR connection setup (PASSING the resolver)
        console.log("MAIN: Establishing SignalR connection (part of coordination)...");
        const connectionPromise = setupSignalRConnection(gameId, playerId, resolveInitialStatePromise); // Pass resolver

        // 1. Start loading card data concurrently
        console.log("MAIN: Loading card data (part of coordination)...");
        const cardLoadPromise = loadCards(); // Returns a promise

        try {
            // Wait for BOTH card data loading AND SignalR connection (implicitly via initialStatePromise) + initial state reception
            console.log("MAIN: Waiting for Promise.all([cardLoadPromise, initialStatePromise, connectionPromise])...");
            // connectionPromise ensures we handle setup errors even if initialStatePromise never resolves/rejects
            // We need the connection object resolved from connectionPromise to pass to gameManager
            const [_, serverState, currentConnection] = await Promise.all([cardLoadPromise, initialStatePromise, connectionPromise]);
            // serverState is from initialStatePromise resolver, currentConnection from connectionPromise resolver

            console.log("MAIN: Promise.all resolved. Card data loaded, SignalR connected, initial state received.");
            console.log("MAIN: Received initial server state:", serverState); // Log the received state

            // 5. Pass connection to gameManager (AFTER connection is confirmed ready)
            console.log("MAIN: Setting SignalR connection for gameManager...");
            setSignalRConnection(currentConnection);

            // 6. Initialize gameManager with context (AFTER connection and IDs are ready)
            console.log("MAIN: Initializing game logic structure...");
            initializeGame(gameId, playerId); // Still needed to set IDs in gameManager

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
            console.log("MAIN: Initializing developer console...");
            initDevConsole();

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

// 启动应用
initializeApp();