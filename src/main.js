// src/main.js - 主入口模块
console.log("main.js loaded");

// 导入核心模块
// import * as signalR from "@microsoft/signalr"; // REMOVED: Now using global signalR from CDN
import { gameState, initializeGame, setSignalRConnection, updateFullGameState } from './gameManager.js'; // ADDED updateFullGameState
import { loadCards } from './cardManager.js'; // Keep existing correct import
import { renderGame } from './ui.js'; // Removed non-existent displayError/Info/initializeUI, Added UI function imports - functions to be implemented in ui
// import { endTurn } from './turnManager.js'; // Keep commented out
// Old SignalR setup code removed. New setup will be added later.
// --- End SignalR Setup ---


let isInitializing = false;
const HUB_URL = "/gamehub"; // Use relative URL, assumes same origin or proxy configured
let connection = null; // Module-level variable for SignalR connection - Moved here

// --- SignalR Connection Logic ---

/**
 * Sets up and starts the SignalR connection.
 * @param {string} gameId - The ID of the game.
 * @param {string} playerId - The ID of the player.
 * @returns {Promise<signalR.HubConnection>} A promise that resolves with the connection instance.
 */
async function setupSignalRConnection(gameId, playerId) {
    if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
        console.warn("MAIN: SignalR connection already exists and is not disconnected. Stopping existing one.");
        await connection.stop();
    }

    const hubUrlWithParams = `${HUB_URL}?gameId=${encodeURIComponent(gameId)}&playerId=${encodeURIComponent(playerId)}`;
    console.log(`MAIN: Attempting to connect to SignalR Hub: ${hubUrlWithParams}`);

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrlWithParams)
        .withAutomaticReconnect() // Enable automatic reconnection
        .configureLogging(signalR.LogLevel.Information)
        .build();

    registerSignalREventHandlers(connection);

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
function registerSignalREventHandlers(conn) {
    // Handler for full game state updates (e.g., on connect/reconnect)
    conn.on("ReceiveGameState", (receivedGameState) => {
        console.log('[main.js] ReceiveGameState handler invoked.'); // Add this log point
        console.log("MAIN: Received Full GameState:", receivedGameState);
        // Delegate the full state update to gameManager
        updateFullGameState(receivedGameState);
        // renderGame(gameState); // REMOVED: updateFullGameState now handles rendering
        alert("游戏状态已同步。"); // Use alert instead of displayInfo
    });

    // Handler for incremental game events
    conn.on("ReceiveGameEvent", (gameEvent) => {
        console.log("MAIN: Received GameEvent:", gameEvent.eventType, gameEvent);
        // Delegate event processing to gameManager
        // processGameEvent(gameEvent); // REMOVED: processGameEvent is not exported from gameManager. Events are handled by specific '.on()' listeners.
        // processGameEvent should update gameState and trigger UI updates (full or partial) via ui.js
    });

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
        requestId: requestId,
        selectedTargetIds: selectedIds,
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

        // 1. Load static card data
        console.log("MAIN: Loading card data...");
        await loadCards();
        console.log("MAIN: Card data loaded.");

        // 2. Get Game and Player IDs (Placeholder: from URL query params)
        console.log("MAIN: Getting Game/Player IDs...");
        const urlParams = new URLSearchParams(window.location.search);
        const gameId = urlParams.get('gameId') || '00000000-0000-0000-0000-000000000001'; // Default for testing - MUST be a valid GUID
        const playerId = urlParams.get('playerId') || 'player1';   // Default for testing
        console.log(`MAIN: Using GameID=${gameId}, PlayerID=${playerId}`);

        // 3. Render initial empty/loading state (optional, depends on UX)
        // renderGame(createEmptyGameState()); // Render board structure

        // 4. Connect to SignalR Hub
        console.log("MAIN: Establishing SignalR connection...");
        const currentConnection = await setupSignalRConnection(gameId, playerId);

        // 5. Pass connection to gameManager
        console.log("MAIN: Setting SignalR connection for gameManager...");
        setSignalRConnection(currentConnection); // Make connection available to gameManager

        // 6. Initialize core game logic (will likely rely on ReceiveGameState now)
        console.log("MAIN: Initializing game logic structure...");
        initializeGame(gameId, playerId); // Initialize gameManager with context, but state comes from server

        // 7. Setup core event listeners (like End Turn button)
        console.log("MAIN: Setting up core event listeners...");
        setupEventListeners(); // Setup listeners after connection is available

        // Note: Initial full game render is triggered by ReceiveGameState handler upon successful connection.

        console.log("MAIN: Application initialization sequence complete! Waiting for game state from server...");

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
        endTurnButton.addEventListener('click', () => {
            console.log("MAIN: End Turn button clicked.");
            // Check connection status before invoking
            if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
                console.error("MAIN: SignalR connection not available for EndTurn");
                alert("连接已断开，无法结束回合。"); // Use alert instead of displayError
                return;
            }
            console.log("MAIN: Invoking EndTurn Hub method...");
            connection.invoke("EndTurn")
                .then(() => console.log("MAIN: EndTurn invoked successfully.")) // Add success log
                .catch(err => {
                     console.error("MAIN: Error invoking EndTurn:", err);
                     alert(`结束回合失败: ${err.message}`); // Use alert instead of displayError
                 });
        });
        console.log("MAIN: End Turn button listener attached.");
    } else {
        console.error("MAIN: Element with ID 'end-turn-button' not found!");
    }

    // UI event delegation (e.g., clicks on field, hand) is handled within ui.js
    // We assume ui.js's initializeUI or specific handlers are called elsewhere appropriately.
    console.log("MAIN: Core event listeners set up.");
}

// 启动应用
initializeApp();