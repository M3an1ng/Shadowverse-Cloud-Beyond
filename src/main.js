// src/main.js - 主入口模块
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - API集成: 文档/03-前端架构/03-API集成.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
//
// 导入核心模块
import * as signalR from "@microsoft/signalr";
import { router, initializeRouter } from './router.js';
import { BATTLE_EXIT_CONFIRM_MESSAGE, shouldConfirmBattleExit } from './battleExitConfirmPolicy.js';
import { clearUserSession, getDeckOwnerPayload, getDeckOwnerQuery, getUserSession, setUserSession, USER_SESSION_CHANGED_EVENT } from './userSession.js';
import {
    gameState, bindGameView, initializeGame, initializeSpectator, clearGameSession, setSignalRConnection, setGuestSignalRConnection, updateFullGameState, endTurnRequest,
    // Import specific event handlers instead of processGameEvent
    handleDeckUpdate, handleTurnStarted, handleTurnEnded, handlePpUpdate, handleEpUpdate, handleSepUpdate, handleHandUpdated,
    handleGraveyardUpdate, handleAttackDeclared, handleUnitDestroyed, handleDamageDealt, handleHealApplied,
    handleCardSummoned, handleCardPlayed, handleHealthChanged, handleCardVanished,
    handleStatChange, handleUnitEvolved, handleGameOver, handlePlayerMulliganCompleted, handleAttackStateUpdated,
    handleActStateUpdated, handleFusionStateUpdated, handleEvolveStateUpdated, handleSuperEvolveStateUpdated, handleHandPlayabilityUpdate, handleActPlayabilityUpdate,
    handlePpMaxUpdate, handleEpMaxUpdate, handleSepMaxUpdate, handlePpBoostStateUpdated, handlePlayerChoiceRequested,
    handleCardRevealed, handleFusion, handleAbilityTriggered, handleKeywordAdded, handleKeywordRemoved, handleCardInstanceUpdated,
    handleChatHistory, handleChatMessage, handleMatchHistorySync, surrenderGame,
    handleCardMoved, handleEmblemUpdated, handleCounterChanged, handleCardReturned, handleTuck, appendGameLog // 【新增】导入最大值更新处理函数
} from './gameManager.js';
// loadCards is obsolete now that the server is the single source of truth.
import {
    renderGame, setConnectionStatus, setOpponentConnectionStatus, renderGameLog, showDamageNumber,
    showHealNumber, updatePpDisplay, updateEpDisplay, updateSepDisplay, updatePpBoostDisplay,
    updateDeckCountDisplay, updateGraveyardCountDisplay, updateBloodPoolDisplay, updateComboDisplay, updateRallyDisplay,
    updateHealthDisplay, updateUnitCombatStats, clearSelectionHighlights, resetMulliganSelection, updateHandDisplay,
    refreshHandPlayabilityVisuals, updateUnitDisplay, updateFieldDisplay, highlightAttacker, highlightTargets,
    showMulliganInterface, updateMulliganStatus, refreshBattleCardPreviewIfCurrent, renderHighlights, updateActButtonState,
    updateCardActionButtons, updateEmblemZone, showTurnStartBanner, updateTurnTimerDisplay, refreshCardInstanceUi,
    refreshCardActionControls, showPlayerChoiceUI, showRevealOverlay, showFusionAnimation, showAbilityTriggerToast,
    replaceInMatchChatHistory, replaceInMatchCardHistory, appendInMatchChatMessage, clearInMatchChatUiState, appendInMatchUsedCard,
    appendInMatchDestroyedCard, clearInMatchUsedCardUiState, cancelCommittedHandPlayVisual
} from './ui.js'; // Removed non-existent displayError/Info/initializeUI, Added UI function imports - functions to be implemented in ui
import { initDevConsole } from './devConsole.js'; // 开发者控制台
import { ensureEnumLocalizations } from './enumLocalizationCache.js';
import { getRouteNameFromHash, shouldReuseCachedHandshake } from './handshakePolicy.js';
import { shouldDeferRoomLeaveForDecision } from './roomExitDecisionPolicy.js';
import { getStoredValue, removeStoredValue, setStoredValue } from './storageFallback.js';
import { buildDebugLogLine, getConnectionDebugId } from './debugDiagnostics.js';
import { createDeckSelectionPickerModal } from './components/deckSelectionPickerModal.js';
import { registerPwaServiceWorker, setupPwaInstallUI } from './pwaInstall.js';
import {
    ensureDeckEnvironmentOptions,
    getDeckEnvironmentLabel as getDeckEnvironmentLabelFromOptions,
    getDeckEnvironmentOptions,
    getDefaultDeckEnvironment,
    normalizeDeckEnvironment as normalizeDeckEnvironmentFromOptions
} from './deckEnvironmentOptions.js';
import {
    buildDeckPresentation,
    formatDeckSummaryLabel,
    getDeckEnvironmentLabel,
    getDeckRuleLabel,
    normalizeDeckRule as normalizeDeckRuleShared,
    normalizeEnvironment as normalizeEnvironmentShared
} from './deckPresentation.js';
import { filterUiVisibleDecks } from './deckRuleAvailability.js';
import { DECK_VALIDATION_RULES_UPDATED_EVENT } from './dataChangeEvents.js';
import { initializeBattleCustomization } from './battleCustomization.js';
import { normalizeMatchRoomList } from './matchRoomBrowser.js';
import { createLobbyNotificationSoundPlayer } from './lobbyNotificationSound.js';
import { createGuestJoinSoundTracker } from './multiplayerRoomSoundPolicy.js';
import {
    QUEST_PROGRESS_UPDATED_EVENT,
    QUEST_REWARD_CLAIMED_EVENT
} from './questFeedback.js';
// 在页面初始化前接入真实 console；面板首次打开时才创建 DOM。
bindGameView({
    renderGame, renderGameLog, showDamageNumber, showHealNumber, updatePpDisplay, updateEpDisplay, updateSepDisplay, updatePpBoostDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateBloodPoolDisplay, updateComboDisplay, updateRallyDisplay, updateHealthDisplay, updateUnitCombatStats, clearSelectionHighlights, resetMulliganSelection, updateHandDisplay, refreshHandPlayabilityVisuals, updateUnitDisplay, updateFieldDisplay, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus, refreshBattleCardPreviewIfCurrent, renderHighlights, updateActButtonState, updateCardActionButtons, updateEmblemZone, showTurnStartBanner, updateTurnTimerDisplay, refreshCardInstanceUi, refreshCardActionControls, showPlayerChoiceUI, showRevealOverlay, showFusionAnimation, showAbilityTriggerToast, replaceInMatchChatHistory, replaceInMatchCardHistory, appendInMatchChatMessage, clearInMatchChatUiState, appendInMatchUsedCard, appendInMatchDestroyedCard, clearInMatchUsedCardUiState, cancelCommittedHandPlayVisual
});
initDevConsole();
// import { endTurn } from './turnManager.js'; // Keep commented out
// Old SignalR setup code removed. New setup will be added later.
// --- End SignalR Setup ---

function getPerfDebugState() {
    if (typeof window === 'undefined') {
        return {
            enabled: false,
            menuStarrySky: null,
            handDrag: null
        };
    }

    if (!window.__svPerfDebug) {
        window.__svPerfDebug = {
            enabled: true,
            menuStarrySky: {
                isAnimating: false,
                route: '',
                lastFrameAt: 0,
                frameCount: 0,
                slowFrameCount: 0
            },
            handDrag: {
                sessionId: 0,
                pendingRafAt: 0,
                sampleCount: 0
            }
        };
    }

    return window.__svPerfDebug;
}

function logPerfDebug(scope, message, details = null) {
    const debugState = getPerfDebugState();
    if (!debugState.enabled) return;
    console.info(buildDebugLogLine(scope, message, details));
}

function summarizeSignalRPayload(eventName, eventData) {
    switch (eventName) {
        case 'ReceiveGameState': {
            const players = Array.isArray(eventData?.Players) ? eventData.Players : [];
            return {
                phase: eventData?.Phase ?? '',
                turnNumber: eventData?.TurnNumber ?? null,
                currentPlayerId: eventData?.CurrentPlayerId ?? null,
                players: players.length
            };
        }
        case 'HandUpdated':
            return {
                playerId: eventData?.PlayerId ?? eventData?.playerId ?? null,
                handCount: Array.isArray(eventData?.Hand) ? eventData.Hand.length : null
            };
        case 'PlayerMulliganCompleted':
            return {
                playerId: eventData?.PlayerId ?? eventData?.playerId ?? null
            };
        case 'CardMoved':
            return {
                cardUid: eventData?.CardUid ?? eventData?.cardUid ?? null,
                fromZone: eventData?.FromZone ?? eventData?.fromZone ?? null,
                toZone: eventData?.ToZone ?? eventData?.toZone ?? null
            };
        case 'CardInstanceUpdated':
            return {
                cardUid: eventData?.CardUid ?? eventData?.InstanceId ?? eventData?.cardUid ?? null
            };
        case 'ChatHistory':
            return {
                items: Array.isArray(eventData) ? eventData.length : null
            };
        case 'MatchHistorySync':
            return {
                items: Array.isArray(eventData?.Items) ? eventData.Items.length : (Array.isArray(eventData) ? eventData.length : null)
            };
        case 'PlayerConnectionStatusUpdated':
            return {
                playerId: eventData?.PlayerId ?? eventData?.playerId ?? null,
                isConnected: eventData?.IsConnected ?? eventData?.isConnected ?? null
            };
        default:
            return {
                hasPayload: eventData !== undefined && eventData !== null
            };
    }
}

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

function stopConnectionHeartbeat(conn) {
    if (!conn) return;

    const timerId = connectionHeartbeatTimers.get(conn);
    if (timerId) {
        window.clearInterval(timerId);
        connectionHeartbeatTimers.delete(conn);
    }
}

function startConnectionHeartbeat(conn, label) {
    if (!conn || connectionHeartbeatTimers.has(conn)) return;

    const timerId = window.setInterval(() => {
        if (conn.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        conn.invoke("Heartbeat").catch(err => {
            console.warn(`MAIN: ${label} heartbeat failed.`, err);
        });
    }, HEARTBEAT_INTERVAL_MS);

    connectionHeartbeatTimers.set(conn, timerId);
}

let isInitializing = false;
let gameInitialized = false;
let gamePageSession = 0;
const HUB_URL = "/gamehub"; // Use relative URL, assumes same origin or proxy configured
let connection = null; // Module-level variable for SignalR connection - Moved here
let guestConnection = null;
let initialStateReceived = false; // Flag to track if initial state has been handled
let forceFullStateOnNextReceive = false;
let reconnectInProgress = false;
let lobbyHandshake = null; // { gameId, playerId, token, joinCode?, role, useAiFallback?, dualControl?, hostPlayerId?, hostToken?, guestPlayerId?, guestToken? }
let pendingHandshake = null; // Set by multiplayer UI before initialization
let pendingRouteTransition = null; // { fromRoute, toRoute }
let pendingRoomExitDecision = null; // { sourceRoute, targetRoute, gameId }
let confirmedBattleExitSurrender = false;
let roomExitDecisionModalEl = null;
let roomExitDecisionPromise = null;
let roomExitDecisionResolve = null;
const LOBBY_HANDSHAKE_STORAGE_KEY = 'svb.lobby.handshake';
const HEARTBEAT_INTERVAL_MS = 30000;
const connectionHeartbeatTimers = new WeakMap();

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
    needsValidationRefresh: true,
    singleplayerMode: 'ai',
    available: {
        multiplayer: [],
        singleplayer: [],
        ai: []
    },
    validationCache: new Map(),
    validationInFlight: new Map(),
    multiplayerFormat: null,
    selected: {
        multiplayer: null,
        singleplayer: null,
        ai: null
    }
};

const DECK_SELECTION_CONTEXT_STORAGE_KEY = 'svb.deck-selection-context.v1';
const DEFAULT_MULTIPLAYER_DECK_FORMAT = Object.freeze({
    environment: getDefaultDeckEnvironment(),
    rule: 'standard'
});

let deckSelectionPickerModal = null;

function normalizeDeckId(value) {
    const text = String(value ?? '').trim();
    return text.length > 0 ? text : null;
}

function normalizeEnumValue(value) {
    if (value === null || value === undefined) return '';
    return String(value).trim().toLowerCase();
}

function normalizeEnvironment(value, fallback = DEFAULT_MULTIPLAYER_DECK_FORMAT.environment) {
    return normalizeEnvironmentShared(value, fallback);
}

function normalizeDeckRule(value, fallback = DEFAULT_MULTIPLAYER_DECK_FORMAT.rule) {
    return normalizeDeckRuleShared(value, fallback);
}

function resolveDeckId(deck) {
    return deck?.deckId || deck?.id || deck?.DeckId || null;
}

function resolveDeckUpdatedAt(deck) {
    return String(deck?.updatedAt ?? deck?.UpdatedAt ?? '');
}

function resolveDeckFormat(deck) {
    return {
        environment: normalizeEnvironment(deck?.environment ?? deck?.Environment),
        rule: normalizeDeckRule(deck?.rule ?? deck?.Rule)
    };
}

function matchesDeckFormat(deck, format) {
    if (!deck || !format) return false;
    const normalizedFormat = {
        environment: normalizeEnvironment(format.environment),
        rule: normalizeDeckRule(format.rule)
    };
    const deckFormat = resolveDeckFormat(deck);
    return deckFormat.environment === normalizedFormat.environment
        && deckFormat.rule === normalizedFormat.rule;
}

function getDeckValidationCacheKey(deck) {
    const deckId = resolveDeckId(deck);
    if (!deckId) return null;
    return `${deckId}|${resolveDeckUpdatedAt(deck)}`;
}

function pruneDeckValidationCache(decks) {
    const activeKeys = new Set(
        (Array.isArray(decks) ? decks : [])
            .map(deck => getDeckValidationCacheKey(deck))
            .filter(Boolean)
    );
    for (const key of deckSelectionState.validationCache.keys()) {
        if (!activeKeys.has(key)) {
            deckSelectionState.validationCache.delete(key);
        }
    }
    for (const key of deckSelectionState.validationInFlight.keys()) {
        if (!activeKeys.has(key)) {
            deckSelectionState.validationInFlight.delete(key);
        }
    }
}

function invalidateDeckValidationCache() {
    deckSelectionState.validationCache.clear();
    deckSelectionState.validationInFlight.clear();
    deckSelectionState.needsValidationRefresh = true;
}

function rebuildAvailableDeckBuckets() {
    const validDecks = deckSelectionState.decks.filter(deck => deck?.isBattleReady === true);
    deckSelectionState.available.singleplayer = validDecks;
    deckSelectionState.available.ai = validDecks;
    // Deck picker should always allow switching among all battle-ready decks.
    // Room/match format checks are still enforced when joining/syncing.
    deckSelectionState.available.multiplayer = validDecks;
}

async function validateDeckForSelection(deck) {
    const deckId = resolveDeckId(deck);
    if (!deckId) return false;

    const cacheKey = getDeckValidationCacheKey(deck);
    if (cacheKey && deckSelectionState.validationCache.has(cacheKey)) {
        return deckSelectionState.validationCache.get(cacheKey) === true;
    }

    if (cacheKey && deckSelectionState.validationInFlight.has(cacheKey)) {
        return await deckSelectionState.validationInFlight.get(cacheKey);
    }

    const validatePromise = (async () => {
        const query = getDeckOwnerQuery();
        const response = await fetch(`/api/decks/${encodeURIComponent(deckId)}/code?${query}`, { method: 'GET' });
        // 此端点以 400 表示牌组规则拒绝，以 404 表示牌组不存在；其他失败不是校验结论。
        if (response.status === 400 || response.status === 404) {
            return false;
        }
        if (!response.ok) {
            throw new Error(`卡组校验请求失败（HTTP ${response.status}）。`);
        }
        const payload = await response.json();
        if (typeof payload?.code !== 'string' || !payload.code.trim()) {
            throw new Error('卡组校验响应缺少有效的牌组代码。');
        }
        return true;
    })();

    if (cacheKey) {
        deckSelectionState.validationInFlight.set(cacheKey, validatePromise);
    }

    try {
        const valid = await validatePromise;
        if (cacheKey && deckSelectionState.validationInFlight.get(cacheKey) === validatePromise) {
            deckSelectionState.validationCache.set(cacheKey, valid);
        }
        return valid;
    } finally {
        if (cacheKey && deckSelectionState.validationInFlight.get(cacheKey) === validatePromise) {
            deckSelectionState.validationInFlight.delete(cacheKey);
        }
    }
}

function readDeckSelectionContext() {
    try {
        const raw = getStoredValue(DECK_SELECTION_CONTEXT_STORAGE_KEY);
        if (!raw) return null;
        const parsed = JSON.parse(raw);
        if (!parsed || typeof parsed !== 'object') return null;
        return parsed;
    } catch {
        return null;
    }
}

function writeDeckSelectionContext(context) {
    if (!context || typeof context !== 'object') return;
    const normalized = {
        source: context.source === 'multiplayer' ? 'multiplayer' : 'singleplayer',
        environment: normalizeEnvironment(context.environment),
        rule: normalizeDeckRule(context.rule),
        updatedAt: Date.now()
    };
    try {
        setStoredValue(DECK_SELECTION_CONTEXT_STORAGE_KEY, JSON.stringify(normalized));
    } catch (err) {
        console.warn('MAIN: Failed to persist deck selection context.', err);
    }
}

function setMultiplayerDeckFormat(format, source = 'multiplayer') {
    if (!format) return;
    deckSelectionState.multiplayerFormat = {
        environment: normalizeEnvironment(format.environment),
        rule: normalizeDeckRule(format.rule)
    };
    writeDeckSelectionContext({
        source,
        environment: deckSelectionState.multiplayerFormat.environment,
        rule: deckSelectionState.multiplayerFormat.rule
    });
}

function getCurrentMultiplayerDeckFormat() {
    if (deckSelectionState.multiplayerFormat) {
        return deckSelectionState.multiplayerFormat;
    }
    return null;
}

async function refreshSelectableDecks() {
    pruneDeckValidationCache(deckSelectionState.decks);

    const needValidation = deckSelectionState.needsValidationRefresh
        || deckSelectionState.decks.some(deck => typeof deck?.isBattleReady !== 'boolean');

    if (!needValidation) {
        rebuildAvailableDeckBuckets();
        return;
    }

    const decks = deckSelectionState.decks;
    const readiness = await Promise.all(decks.map(deck => validateDeckForSelection(deck)));
    decks.forEach((deck, index) => {
        deck.isBattleReady = readiness[index];
    });

    deckSelectionState.needsValidationRefresh = false;
    rebuildAvailableDeckBuckets();
}

function getDecksForKey(key) {
    return Array.isArray(deckSelectionState.available[key]) ? deckSelectionState.available[key] : [];
}

function formatDeckLabel(deck) {
    return formatDeckSummaryLabel(deck);
}

function getDeckById(deckId, decks = deckSelectionState.decks) {
    const normalized = normalizeDeckId(deckId);
    if (!normalized) return null;
    return decks.find(deck => resolveDeckId(deck) === normalized) ?? null;
}

function ensureSelectedDeckId(key, decks = getDecksForKey(key)) {
    if (!decks.length) {
        deckSelectionState.selected[key] = null;
        return null;
    }
    const selected = normalizeDeckId(deckSelectionState.selected[key]);
    const fallback = resolveDeckId(decks[0]);
    const resolved = decks.some(deck => resolveDeckId(deck) === selected) ? selected : fallback;
    deckSelectionState.selected[key] = resolved;
    return resolved;
}

function ensureDeckPickerModal() {
    if (!deckSelectionPickerModal) {
        deckSelectionPickerModal = createDeckSelectionPickerModal();
    }
    return deckSelectionPickerModal;
}

async function pickDeckFromModal({ key, title, skipRefresh = false }) {
    if (!skipRefresh) {
        await refreshSelectableDecks();
    }
    const decks = getDecksForKey(key);
    const selectedDeckId = ensureSelectedDeckId(key, decks);
    const pickerDecks = decks.map(deck => {
        const presentation = buildDeckPresentation(deck);
        return ({
        id: resolveDeckId(deck),
        name: deck?.name || '未命名卡组',
        classLabel: presentation.classLabel,
        environmentLabel: presentation.environmentLabel,
        ruleLabel: presentation.ruleLabel,
        isBattleReady: deck?.isBattleReady === true,
        cardCount: Number(deck?.cardCount ?? deck?.CardCount ?? 0),
        totalGames: Number(deck?.totalGames ?? deck?.TotalGames ?? 0),
        totalWins: Number(deck?.totalWins ?? deck?.TotalWins ?? 0),
        goingFirstGames: Number(deck?.goingFirstGames ?? deck?.GoingFirstGames ?? 0),
        goingFirstWins: Number(deck?.goingFirstWins ?? deck?.GoingFirstWins ?? 0),
        goingSecondGames: Number(deck?.goingSecondGames ?? deck?.GoingSecondGames ?? 0),
        goingSecondWins: Number(deck?.goingSecondWins ?? deck?.GoingSecondWins ?? 0)
    });
    });
    const pickedDeckId = await ensureDeckPickerModal().open({
        title,
        decks: pickerDecks,
        selectedDeckId
    });
    return normalizeDeckId(pickedDeckId);
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
        deckSelectionState.decks = filterUiVisibleDecks(Array.isArray(decks) ? decks : []);
        deckSelectionState.needsValidationRefresh = true;
        await refreshSelectableDecks();
        deckSelectionState.loaded = true;
        return deckSelectionState.decks;
    })();

    const loading = deckSelectionState.loading;
    try {
        return await loading;
    } finally {
        if (deckSelectionState.loading === loading) {
            deckSelectionState.loading = null;
        }
    }
}

function updateDeckPickerDisplay(currentEl, selectBtn, key, emptyText = '没有可用卡组') {
    if (!currentEl) return;
    const decks = getDecksForKey(key);
    if (!decks.length) {
        currentEl.textContent = emptyText;
        if (selectBtn) selectBtn.disabled = true;
        return;
    }

    const resolved = ensureSelectedDeckId(key, decks);
    const selectedDeck = getDeckById(resolved, decks) || decks[0];
    currentEl.textContent = formatDeckLabel(selectedDeck);
    if (selectBtn) selectBtn.disabled = false;
}

function updateMultiplayerDeckSwitcher() {
    const currentEl = document.getElementById('mp-deck-current');
    const selectBtn = document.getElementById('mp-deck-select-btn');
    updateDeckPickerDisplay(currentEl, selectBtn, 'multiplayer');
}

function updateSingleplayerDeckSwitcher() {
    const currentEl = document.getElementById('sp-deck-current');
    const selectBtn = document.getElementById('sp-deck-select-btn');
    updateDeckPickerDisplay(currentEl, selectBtn, 'singleplayer');
}

function updateSingleplayerAiDeckSwitcher() {
    const currentEl = document.getElementById('sp-ai-deck-current');
    const selectBtn = document.getElementById('sp-ai-deck-select-btn');
    updateDeckPickerDisplay(currentEl, selectBtn, 'ai');
}

async function refreshDeckSelectionUI() {
    try {
        await ensureDecksLoaded();
        await refreshSelectableDecks();
        updateMultiplayerDeckSwitcher();
        const mpOutput = document.getElementById('mp-deck-output');
        if (mpOutput) {
            if (!getDecksForKey('multiplayer').length) {
                mpOutput.textContent = '没有通过校验的可用卡组，请先在牌组编辑中修正。';
                mpOutput.style.color = '#ff4f00';
            } else {
                mpOutput.textContent = '';
                mpOutput.style.color = '';
            }
        }
        const spOutput = document.getElementById('sp-deck-output');
        if (spOutput) {
            if (!getDecksForKey('singleplayer').length) {
                spOutput.textContent = '没有通过校验的卡组，请先在牌组编辑中修正。';
                spOutput.style.color = '#ff4f00';
            } else {
                spOutput.textContent = '';
                spOutput.style.color = '';
            }
        }
        updateSingleplayerDeckSwitcher();
        updateSingleplayerAiDeckSwitcher();
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
    const serialized = JSON.stringify(handshake);
    const didStore = setStoredValue(LOBBY_HANDSHAKE_STORAGE_KEY, serialized);
    if (!didStore) {
        console.warn("Failed to cache handshake: no writable storage backend");
    }
}

function loadCachedHandshake() {
    if (lobbyHandshake) return lobbyHandshake;
    try {
        const raw = getStoredValue(LOBBY_HANDSHAKE_STORAGE_KEY);
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
    deckSelectionState.multiplayerFormat = null;
    clearPendingRoomExitDecision();
    const removed = removeStoredValue(LOBBY_HANDSHAKE_STORAGE_KEY);
    if (!removed) {
        console.warn("Failed to clear cached handshake from storage backends");
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
    setConnectionStatus('connected');
    setOpponentConnectionStatus(true);
}

function closeRoomExitDecisionModalElement() {
    if (roomExitDecisionModalEl) {
        roomExitDecisionModalEl.remove();
        roomExitDecisionModalEl = null;
    }
}

function settleRoomExitDecision(action) {
    closeRoomExitDecisionModalElement();
    const resolve = roomExitDecisionResolve;
    roomExitDecisionResolve = null;
    roomExitDecisionPromise = null;
    if (typeof resolve === 'function') {
        resolve(action);
    }
}

function clearPendingRoomExitDecision() {
    pendingRoomExitDecision = null;
    if (roomExitDecisionResolve) {
        settleRoomExitDecision('cleared');
        return;
    }
    closeRoomExitDecisionModalElement();
    roomExitDecisionPromise = null;
}

function getActiveNonAiRoomHandshake() {
    const handshake = getReconnectHandshake();
    if (!handshake) return null;
    if (handshake.useAiFallback === true) return null;
    if (!handshake.gameId || !handshake.playerId || !handshake.token) return null;
    return handshake;
}

function rememberPendingRoomExitDecision() {
    const handshake = getActiveNonAiRoomHandshake();
    if (!handshake) return false;

    pendingRoomExitDecision = {
        sourceRoute: pendingRouteTransition?.fromRoute || null,
        targetRoute: pendingRouteTransition?.toRoute || null,
        gameId: handshake.gameId
    };
    return true;
}

function showRoomExitDecisionPrompt() {
    if (roomExitDecisionPromise) {
        return roomExitDecisionPromise;
    }

    roomExitDecisionPromise = new Promise((resolve) => {
        roomExitDecisionResolve = resolve;

        const modal = document.createElement('div');
        modal.className = 'room-exit-decision-modal';
        modal.innerHTML = `
            <div class="room-exit-decision-card" role="dialog" aria-label="对战连接确认" aria-modal="true">
                <div class="room-exit-decision-title">已返回主界面</div>
                <div class="room-exit-decision-body">
                    你可以返回当前多人对战，或断开并退出该房间。
                </div>
                <div class="room-exit-decision-actions">
                    <button type="button" class="room-exit-decision-btn room-exit-decision-return">返回对战</button>
                    <button type="button" class="room-exit-decision-btn room-exit-decision-disconnect">断开连接</button>
                </div>
            </div>
        `;

        const returnBtn = modal.querySelector('.room-exit-decision-return');
        const disconnectBtn = modal.querySelector('.room-exit-decision-disconnect');

        returnBtn?.addEventListener('click', () => settleRoomExitDecision('return'));
        disconnectBtn?.addEventListener('click', () => settleRoomExitDecision('disconnect'));

        document.body.appendChild(modal);
        roomExitDecisionModalEl = modal;
    });

    return roomExitDecisionPromise;
}

async function handlePendingRoomExitDecisionOnNavigation() {
    if (!pendingRoomExitDecision) return;

    const activeHandshake = getActiveNonAiRoomHandshake();
    if (!activeHandshake || activeHandshake.gameId !== pendingRoomExitDecision.gameId) {
        clearPendingRoomExitDecision();
        return;
    }

    const action = await showRoomExitDecisionPrompt();

    if (!pendingRoomExitDecision) {
        return;
    }

    if (action === 'return') {
        clearPendingRoomExitDecision();
        await router.navigateTo('game');
        return;
    }

    clearPendingRoomExitDecision();
    await leaveCurrentPvpRoom('mainmenu-room-exit-decision');
}

function appendAfterRouteChangeHandler(handlerName, callback) {
    const previousAfter = router.afterRouteChange;
    router.setAfterRouteChange(async (oldRoute, newRoute) => {
        if (previousAfter) await previousAfter(oldRoute, newRoute);
        try {
            await callback(oldRoute, newRoute);
        } catch (error) {
            throw new Error(`路由后置处理失败：${handlerName}`, { cause: error });
        }
    });
    return true;
}

function registerRoomExitDecisionRouteHooks() {
    const originalBefore = router.beforeRouteChange;
    router.setBeforeRouteChange(async (oldRoute, newRoute, routeButton) => {
        if (typeof originalBefore === 'function') {
            const shouldContinue = await originalBefore(oldRoute, newRoute, routeButton);
            if (!shouldContinue) {
                return false;
            }
        }

        if (shouldConfirmBattleExit(oldRoute, newRoute, routeButton, gameState)) {
            if (!window.confirm(BATTLE_EXIT_CONFIRM_MESSAGE)) return false;
            if (!await handleConfirmedBattleExit()) return false;
        }
        pendingRouteTransition = {
            fromRoute: oldRoute || '',
            toRoute: newRoute || ''
        };
        return true;
    });

    appendAfterRouteChangeHandler('room-exit-decision', async (_oldRoute, newRoute) => {
        try {
            if (newRoute === 'navigation') {
                await handlePendingRoomExitDecisionOnNavigation();
            }
        } finally {
            pendingRouteTransition = null;
        }
    });
}

async function disconnectGameConnections() {
    const previousConnections = [connection, guestConnection];
    // 先撤销所有权；stop 的回调及已排队事件不能再改动当前页面。
    connection = null;
    guestConnection = null;
    setSignalRConnection(null);
    setGuestSignalRConnection(null);
    const stopped = await Promise.allSettled(previousConnections.map(async conn => {
        stopConnectionHeartbeat(conn);
        if (conn && conn.state !== signalR.HubConnectionState.Disconnected) {
            await conn.stop();
        }
    }));
    const failures = stopped.filter(item => item.status === 'rejected').map(item => item.reason);
    if (failures.length) throw new AggregateError(failures, '对战连接未能完整关闭');
}

async function handleStateHydrationFailure(statusMessage = '重连失败') {
    reconnectInProgress = false;
    setConnectionStatus('disconnected', statusMessage);
    await disconnectGameConnections();
}

function shouldWarnBeforeUnload() {
    const routeFromHash = getRouteNameFromHash(window.location.hash);
    const currentRoute = routeFromHash || (typeof router?.getCurrentRoute === 'function' ? router.getCurrentRoute() : '');
    const isMatchRelatedRoute = currentRoute === 'game' || currentRoute === 'multiplayer';

    if (!isMatchRelatedRoute && !pendingRoomExitDecision) {
        return false;
    }

    return !!getActiveNonAiRoomHandshake();
}

async function handleConfirmedBattleExit() {
    const result = await surrenderGame();
    if (!result?.ok) {
        alert(`投降失败：${result?.error || '未知错误'}`);
        return false;
    }

    confirmedBattleExitSurrender = true;
    return true;
}

function registerBeforeUnloadGuard() {
    window.addEventListener('beforeunload', (event) => {
        if (!shouldWarnBeforeUnload()) {
            return;
        }

        // Required by modern browsers to trigger native leave confirmation.
        event.preventDefault();
        event.returnValue = '';
    });
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
    const resolvedPlayerId = options.playerId === undefined
        ? (session.userId || session.guestId || null)
        : options.playerId;
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

async function listMatches() {
    return await apiGetJson("/api/match");
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

    const { gameId, playerId, token } = handshake;
    // 路由会等待退出结果；失败时保留房间信息，让错误可见且仍可重试退出。
    await leaveMatch(gameId, playerId, token);
    clearCachedHandshake();
}

// --- SignalR Connection Logic ---

/**
 * Sets up and starts the SignalR connection.
 * @param {string} gameId - The ID of the game.
 * @param {string} playerId - The ID of the player.
 * @param {string} token - Lobby token for hub validation.
 * @param {{resolve?: Function, reject?: Function}|null} initialStateGate - Initial-state gate for resolve/reject.
 * @returns {Promise<signalR.HubConnection>} A promise that resolves with the connection object, or rejects on failure.
 */
async function setupSignalRConnection(gameId, playerId, token, initialStateGate, options = {}) { // ADDED token parameter
    const pageSession = gamePageSession;
    await disconnectGameConnections();
    if (pageSession !== gamePageSession) throw new Error('页面已退出，对战连接已取消');

    // Reset initial-state gate for every new connection/session
    if (!options.skipInitialReset) {
        initialStateReceived = false;
    }

    const hubUrlWithParams = `${HUB_URL}?gameId=${encodeURIComponent(gameId)}&playerId=${encodeURIComponent(playerId)}&token=${encodeURIComponent(token)}`;

    const conn = new signalR.HubConnectionBuilder()
        .withUrl(hubUrlWithParams)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Trace) // Changed level to Trace
        .build();
    connection = conn;

    const connectionId = getConnectionDebugId(conn, 'game');
    logPerfDebug('signalr-game', 'connection created', {
        connectionId,
        gameId,
        playerId,
        skipInitialReset: options.skipInitialReset === true
    });

    registerSignalREventHandlers(conn, initialStateGate); // Pass resolver down

    try {
        logPerfDebug('signalr-game', 'starting connection', {
            connectionId,
            state: conn.state
        });
        await conn.start();
        if (connection !== conn) throw new Error('对战连接已被替换或关闭');
        startConnectionHeartbeat(conn, 'game');
        conn.invoke("Heartbeat").catch(err => {
            console.warn("MAIN: game heartbeat refresh failed after connect.", err);
        });
        logPerfDebug('signalr-game', 'connection started', {
            connectionId,
            state: conn.state
        });
        return conn;
    } catch (err) {
        console.error("MAIN: SignalR Connection Failed: ", err);
        alert(`连接游戏服务器失败: ${err.message || 'Unknown error'}`); // Use alert instead of displayError
        if (connection === conn) {
            connection = null;
            setSignalRConnection(null);
        }
        throw err;
    }
}

async function setupActionSignalRConnection(gameId, playerId, token) {
    const pageSession = gamePageSession;
    if (!playerId || !token) return null;

    const previous = guestConnection;
    guestConnection = null;
    setGuestSignalRConnection(null);
    stopConnectionHeartbeat(previous);
    if (previous && previous.state !== signalR.HubConnectionState.Disconnected) await previous.stop();
    if (pageSession !== gamePageSession) throw new Error('页面已退出，副席位连接已取消');

    const hubUrlWithParams = `${HUB_URL}?gameId=${encodeURIComponent(gameId)}&playerId=${encodeURIComponent(playerId)}&token=${encodeURIComponent(token)}`;

    const trackedGuestConnection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrlWithParams)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    guestConnection = trackedGuestConnection;
    registerActionSignalREventHandlers(trackedGuestConnection);
    trackedGuestConnection.onclose(() => {
        stopConnectionHeartbeat(trackedGuestConnection);
        if (guestConnection === trackedGuestConnection) {
            guestConnection = null;
            setGuestSignalRConnection(null);
        }
    });

    try {
        await trackedGuestConnection.start();
        if (guestConnection !== trackedGuestConnection) throw new Error('副席位连接已被替换或关闭');
        startConnectionHeartbeat(trackedGuestConnection, 'guest');
        trackedGuestConnection.invoke("Heartbeat").catch(err => {
            console.warn("MAIN: guest heartbeat refresh failed after connect.", err);
        });
        return trackedGuestConnection;
    } catch (err) {
        console.error("MAIN: Guest SignalR Connection Failed: ", err);
        if (guestConnection === trackedGuestConnection) {
            guestConnection = null;
            setGuestSignalRConnection(null);
        }
        throw err;
    }
}

function registerActionSignalREventHandlers(conn) {
    const onCurrentEvent = (eventName, handler) => conn.on(eventName, (...args) => {
        if (guestConnection === conn) handler(...args);
    });
    let lastChoiceRequestKey = null;
    // Keep this list aligned with gameplay interaction handlers in registerSignalREventHandlers.
    onCurrentEvent("ReceiveHandPlayabilityUpdate", handleHandPlayabilityUpdate);
    onCurrentEvent("ReceiveActPlayabilityUpdate", handleActPlayabilityUpdate);
    onCurrentEvent("AttackStateUpdated", handleAttackStateUpdated);
    onCurrentEvent("ActStateUpdated", handleActStateUpdated);
    onCurrentEvent("FusionStateUpdated", handleFusionStateUpdated);
    onCurrentEvent("EvolveStateUpdated", handleEvolveStateUpdated);
    onCurrentEvent("SuperEvolveStateUpdated", handleSuperEvolveStateUpdated);
    onCurrentEvent("PpBoostStateUpdated", handlePpBoostStateUpdated);

    // Dual-control guest connection must receive player-scoped choice events,
    // otherwise the selected seat cannot submit a decision and the game stalls.
    onCurrentEvent("PlayerChoiceRequested", (eventData) => {
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

    onCurrentEvent("ReceiveError", (errorMessage) => {
        const message = typeof errorMessage === 'string' ? errorMessage : String(errorMessage ?? '');
        console.error("MAIN: Received Error from Server (action connection):", message);

        if (message.startsWith("ROOM_CLOSED:")) {
            const reason = message.slice("ROOM_CLOSED:".length).trim() || "房间已关闭。";
            alert(reason);
            clearCachedHandshake();
            void router.navigateTo('navigation').catch(error => {
                console.error('MAIN: 退出已关闭房间失败。', error);
            });
            return;
        }

        alert(`操作失败: ${message}`);
    });
}

async function setupSpectatorSignalRConnection(gameId, initialStateGate, options = {}) {
    const pageSession = gamePageSession;
    await disconnectGameConnections();
    if (pageSession !== gamePageSession) throw new Error('页面已退出，对战连接已取消');

    if (!options.skipInitialReset) {
        initialStateReceived = false;
    }

    const session = getUserSession();
    const query = new URLSearchParams();
    query.set('gameId', gameId);
    query.set('spectate', '1');

    const spectatorName = String(session?.displayName || '').trim();
    if (spectatorName) {
        query.set('spectatorName', spectatorName);
    }

    const spectatorUserId = String(session?.userId || session?.guestId || '').trim();
    if (spectatorUserId) {
        query.set('spectatorUserId', spectatorUserId);
    }

    const spectatorAvatar = String(session?.avatar || '').trim();
    if (spectatorAvatar && (
        spectatorAvatar.startsWith('/')
        || spectatorAvatar.startsWith('http://')
        || spectatorAvatar.startsWith('https://')
        || spectatorAvatar.startsWith('data:image/')
    )) {
        query.set('spectatorAvatar', spectatorAvatar);
    }

    const hubUrlWithParams = `${HUB_URL}?${query.toString()}`;

    const conn = new signalR.HubConnectionBuilder()
        .withUrl(hubUrlWithParams)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Trace)
        .build();
    connection = conn;

    const connectionId = getConnectionDebugId(conn, 'spectator');
    logPerfDebug('signalr-game', 'spectator connection created', {
        connectionId,
        gameId,
        skipInitialReset: options.skipInitialReset === true
    });

    registerSignalREventHandlers(conn, initialStateGate);

    try {
        logPerfDebug('signalr-game', 'starting spectator connection', {
            connectionId,
            state: conn.state
        });
        await conn.start();
        if (connection !== conn) throw new Error('对战连接已被替换或关闭');
        logPerfDebug('signalr-game', 'spectator connection started', {
            connectionId,
            state: conn.state
        });
        return conn;
    } catch (err) {
        console.error("MAIN: SignalR Connection Failed (spectate): ", err);
        alert(`连接游戏服务器失败: ${err.message || 'Unknown error'}`);
        if (connection === conn) {
            connection = null;
            setSignalRConnection(null);
        }
        throw err;
    }
}

/**
 * Registers handlers for events received from the SignalR Hub.
 * @param {signalR.HubConnection} conn - The SignalR connection instance.
 */
function registerSignalREventHandlers(conn, initialStateGate) { // Accept resolver as parameter
    const onCurrentEvent = (eventName, handler) => conn.on(eventName, (...args) => {
        if (connection === conn) handler(...args);
    });
    const connectionId = getConnectionDebugId(conn, 'game');
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
        "Fusion",
        "AbilityTriggered",
        "PlayerChoiceRequested",
        "LogEvent"
    ];
    const eventReceiptCounts = new Map();
    const criticalEvents = new Set([
        'ReceiveGameState',
        'HandUpdated',
        'PlayerMulliganCompleted',
        'CardMoved',
        'CardInstanceUpdated',
        'ChatHistory',
        'MatchHistorySync',
        'PlayerConnectionStatusUpdated'
    ]);

    const logEventReceipt = (eventName, eventData, extra = null) => {
        const nextCount = (eventReceiptCounts.get(eventName) ?? 0) + 1;
        eventReceiptCounts.set(eventName, nextCount);
        if (!criticalEvents.has(eventName) || nextCount > 4) {
            return nextCount;
        }
        logPerfDebug('signalr-game', 'incoming event', {
            connectionId,
            eventName,
            sequence: nextCount,
            ...summarizeSignalRPayload(eventName, eventData),
            ...(extra || {})
        });
        return nextCount;
    };

    logPerfDebug('signalr-game', 'register handlers complete', {
        connectionId,
        eventHandlerCount: EVENT_STREAM_NAMES.length + 5,
        criticalEvents: Array.from(criticalEvents)
    });

    const wireEvent = (eventName, handler, logMapper) => {
        onCurrentEvent(eventName, (eventData) => {
            logEventReceipt(eventName, eventData);
            if (EVENT_STREAM_NAMES.includes(eventName)) {
                const payloadForLog = typeof logMapper === 'function' ? logMapper(eventData) : eventData;
                handler(eventData);
                appendGameLog(eventName, payloadForLog);
                return;
            }
            handler(eventData);
        });
    };

    // Handler for full game state updates (e.g., on connect/reconnect)
    // --- MODIFIED FOR M6 ---
    onCurrentEvent("ReceiveGameState", (receivedGameState) => {
        const timestamp = new Date().toISOString();
        logEventReceipt('ReceiveGameState', receivedGameState, {
            initialStateReceived,
            hasInitialStateGate: typeof initialStateGate?.resolve === 'function',
            timestamp
        });
        if (!initialStateReceived && typeof initialStateGate?.resolve === 'function') {
            if (!receivedGameState) {
                initialStateReceived = true;
                initialStateGate.reject?.(new Error('初始对局状态为空'));
                return;
            }
            // Initial state handling during initializeApp
            setConnectionStatus('connected');
            initialStateGate.resolve(receivedGameState); // Resolve the promise in initializeApp
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
                const hydrationSucceeded = updateFullGameState(receivedGameState); // Directly update the game state and UI
                if (hydrationSucceeded === false) {
                    void handleStateHydrationFailure('重连失败').catch(error => console.error('MAIN: 状态同步失败后的连接清理失败。', error));
                    return;
                }
            } catch (error) { // Catch for outer try (Line 98)
                console.error("MAIN: Error calling updateFullGameState for subsequent update:", error);
                void handleStateHydrationFailure('重连失败').catch(error => console.error('MAIN: 状态同步失败后的连接清理失败。', error));
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

    // Removed onCurrentEvent("HandUpdate", ...) block as it's deprecated
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
    wireEvent("FusionStateUpdated", handleFusionStateUpdated);
    // 【BUG修复】注册处理手牌可玩性更新的事件监听器
    wireEvent("ReceiveHandPlayabilityUpdate", handleHandPlayabilityUpdate);
    wireEvent("ReceiveActPlayabilityUpdate", handleActPlayabilityUpdate);
    wireEvent("EvolveStateUpdated", handleEvolveStateUpdated);
    wireEvent("SuperEvolveStateUpdated", handleSuperEvolveStateUpdated);
    // NEW: Handler for when the server requests the player to make a choice
    wireEvent("PlayerChoiceRequested", handlePlayerChoiceRequested);
    wireEvent("CardRevealed", handleCardRevealed);
    wireEvent("Fusion", handleFusion);
    wireEvent("AbilityTriggered", handleAbilityTriggered);
    wireEvent("ChatHistory", handleChatHistory);
    wireEvent("ChatMessage", handleChatMessage);
    wireEvent("MatchHistorySync", handleMatchHistorySync);
    // Generic log entries (for events without specific DTO)
    onCurrentEvent("LogEvent", (eventData) => {
        appendGameLog("LogEvent", eventData);
    });

    // [COMPATIBILITY FIX] Add a handler for the obsolete 'receivegameevent' to suppress warnings.
    // The server should be updated to stop sending this event.
    onCurrentEvent("receivegameevent", (eventData) => {
        console.warn("[DEPRECATED] Received obsolete 'receivegameevent'. Server should be updated to use specific event handlers. Data:", eventData);
    });

    // The generic ReceiveGameEvent handler is implicitly removed by replacing this whole block.

    // Handler for server-side errors
    onCurrentEvent("ReceiveError", (errorMessage) => {
        const message = typeof errorMessage === 'string' ? errorMessage : String(errorMessage ?? '');
        console.error("MAIN: Received Error from Server:", message);

        if (message.startsWith("ROOM_CLOSED:")) {
            const reason = message.slice("ROOM_CLOSED:".length).trim() || "房间已关闭。";
            alert(reason);
            clearCachedHandshake();
            void router.navigateTo('navigation').catch(error => {
                console.error('MAIN: 退出已关闭房间失败。', error);
            });
            return;
        }

        if (!initialStateReceived && typeof initialStateGate?.reject === 'function') {
            initialStateReceived = true;
            initialStateGate.reject(new Error(message || '初始化阶段收到服务器错误'));
            setConnectionStatus('disconnected', '连接失败，请点击重连');
        }

        alert(`操作失败: ${message}`); // Use alert instead of displayError
    });

    onCurrentEvent("PlayerConnectionStatusUpdated", (eventData) => {
        logEventReceipt('PlayerConnectionStatusUpdated', eventData);
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
    onCurrentEvent("RequestTargetSelection", (request) => {
        // Removed call to showTargetSelection, which is not exported/used.
    });

    // --- Connection Lifecycle Handlers ---
    conn.onclose((error) => {
        stopConnectionHeartbeat(conn);
        if (connection !== conn) return;
        logPerfDebug('signalr-game', 'connection closed', {
            connectionId,
            state: conn.state,
            error: error ? { message: error.message, name: error.name } : null
        });
        console.error(`MAIN: SignalR Connection Closed. Error: "${error}". State: ${conn.state}.`);
        alert("与服务器连接已断开。" + (error ? ` 原因: ${error.message}` : "")); // Use alert instead of displayError
        // Automatic reconnect is enabled, but we might want to disable UI elements here.
        connection = null; // Clear the connection variable
        reconnectInProgress = false;
        setSignalRConnection(null);
        setConnectionStatus('disconnected', '连接已断开');
    });

    conn.onreconnecting((error) => {
        if (connection !== conn) return;
        setSignalRConnection(null);
        logPerfDebug('signalr-game', 'connection reconnecting', {
            connectionId,
            state: conn.state,
            error: error ? { message: error.message, name: error.name } : null
        });
        console.warn("MAIN: SignalR Connection Reconnecting...", error);
        alert("连接中断，正在尝试重新连接服务器..."); // Use alert instead of displayInfo
        // Disable UI interactions while reconnecting?
        reconnectInProgress = true;
        setConnectionStatus('reconnecting', '连接中断，正在重连...');
    });

    conn.onreconnected((connectionId) => {
        if (connection !== conn) return;
        logPerfDebug('signalr-game', 'connection reconnected', {
            connectionId: getConnectionDebugId(conn, 'game'),
            transportConnectionId: connectionId,
            state: conn.state
        });
        alert("已重新连接到服务器。"); // Use alert instead of displayInfo
        forceFullStateOnNextReceive = true;
        reconnectInProgress = false;
        setSignalRConnection(conn);
        setConnectionStatus('reconnecting', '连接已恢复，正在同步...');
        void requestFullStateSync("auto-reconnect").catch(error => {
            console.error("MAIN: 自动重连同步失败。", error);
        });
        // Request latest state after reconnection if server doesn't push automatically
        // connection.invoke("RequestInitialGameState").catch(err => console.error("Request state after reconnect failed:", err));
    });

}

async function requestFullStateSync(reason) {
    const requestedConnection = connection;
    if (!requestedConnection || requestedConnection.state !== signalR.HubConnectionState.Connected) {
        console.warn("MAIN: Skip full state sync (connection not ready).", reason || "");
        await handleStateHydrationFailure('重连失败');
        return false;
    }
    try {
        await requestedConnection.invoke("RequestInitialGameState");
        return true;
    } catch (err) {
        console.error("MAIN: RequestInitialGameState failed:", err);
        if (connection !== requestedConnection) return false;
        await handleStateHydrationFailure('重连失败');
        return false;
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
    const routeName = getRouteNameFromHash(window.location.hash);
    const isInMultiplayerUi = document.body.classList.contains('show-multiplayer');
    if (shouldReuseCachedHandshake(cached, { routeName, isInMultiplayerUi })) {
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

    const pageSession = gamePageSession;
    reconnectInProgress = true;
    setConnectionStatus('reconnecting', '正在重连...');

    try {
        if (handshake.role === "spectator") {
            initializeSpectator(handshake.gameId);
        } else {
            initializeGame(handshake.gameId, handshake.playerId, {
                dualControl: handshake.dualControl === true,
                hostPlayerId: handshake.hostPlayerId || handshake.playerId,
                guestPlayerId: handshake.guestPlayerId || null
            });
        }

        initialStateReceived = true;
        forceFullStateOnNextReceive = true;

        const connPromise = handshake.role === "spectator"
            ? setupSpectatorSignalRConnection(handshake.gameId, null, { skipInitialReset: true })
            : setupSignalRConnection(handshake.gameId, handshake.playerId, handshake.token, null, { skipInitialReset: true });

        const newConnection = await connPromise;
        if (pageSession !== gamePageSession) return false;
        setSignalRConnection(newConnection);

        if (handshake.role !== "spectator" && handshake.dualControl && handshake.guestPlayerId && handshake.guestToken) {
            const newGuestConnection = await setupActionSignalRConnection(
                handshake.gameId,
                handshake.guestPlayerId,
                handshake.guestToken
            );
            setGuestSignalRConnection(newGuestConnection);
        }

        const syncSucceeded = await requestFullStateSync("manual-reconnect");
        if (!syncSucceeded) {
            throw new Error("请求完整状态失败");
        }
    } catch (err) {
        if (pageSession !== gamePageSession) {
            console.info('MAIN: 页面已退出，重连操作已取消。');
            return false;
        }
        console.error("MAIN: Manual reconnect failed:", err);
        setConnectionStatus('disconnected', '重连失败，请重试');
        alert(`重连失败：${err?.message || '未知错误'}`);
    } finally {
        if (pageSession === gamePageSession) reconnectInProgress = false;
    }
}

/**
 * Initializes the application, loads data, connects to SignalR, and sets up the UI.
 */
async function initializeApp() {
    if (isInitializing) {
        console.warn("MAIN: Initialization already in progress.");
        return false;
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

        // Create an initial-state gate that supports resolve/reject + timeout.
        let initialStateResolve;
        let initialStateReject;
        let initialStateGateSettled = false;
        let initialStateTimeoutId = null;
        const initialStatePromise = new Promise((resolve, reject) => {
            initialStateResolve = resolve;
            initialStateReject = reject;
        });
        const settleInitialStateResolve = (value) => {
            if (initialStateGateSettled) return;
            initialStateGateSettled = true;
            if (initialStateTimeoutId) {
                clearTimeout(initialStateTimeoutId);
                initialStateTimeoutId = null;
            }
            initialStateResolve(value);
        };
        const settleInitialStateReject = (reason) => {
            if (initialStateGateSettled) return;
            initialStateGateSettled = true;
            if (initialStateTimeoutId) {
                clearTimeout(initialStateTimeoutId);
                initialStateTimeoutId = null;
            }
            const error = reason instanceof Error ? reason : new Error(String(reason || '初始化失败'));
            initialStateReject(error);
        };
        const initialStateGate = {
            resolve: settleInitialStateResolve,
            reject: settleInitialStateReject
        };
        initialStateTimeoutId = setTimeout(() => {
            settleInitialStateReject(new Error('等待初始对局状态超时'));
        }, 15000);

        // 4. Start SignalR connection setup (PASSING the resolver)
        const connectionPromise = handshake.role === "spectator"
            ? setupSpectatorSignalRConnection(gameId, initialStateGate)
            : setupSignalRConnection(gameId, playerId, token, initialStateGate); // Pass resolver + token

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
            const hydrationSucceeded = updateFullGameState(serverState); // <-- RESTORED
            if (hydrationSucceeded === false) {
                await handleStateHydrationFailure('重连失败');
                return false;
            }

            // 7. Setup core event listeners (AFTER connection and UI are ready)
            setupEventListeners();



        } catch (error) {
            if (initialStateTimeoutId) {
                clearTimeout(initialStateTimeoutId);
                initialStateTimeoutId = null;
            }
            console.error("MAIN: Error during M6 coordination (Promise.all failed):", error);
            // Handle errors from loadCards, setupSignalRConnection, or state reception issues
            alert(`游戏初始化失败：${error.message || '未知错误'} 请刷新页面重试。`);
            // Optionally disable UI or show persistent error
            await handleStateHydrationFailure('连接失败，请点击重连');
            return false; // Stop initialization on error
        }
        // --- M6 Coordination Logic End ---
        return true;

    } catch (error) {
        console.error("MAIN: Application initialization failed:", error);
        const cached = getReconnectHandshake();
        if (cached?.gameId) {
            setConnectionStatus('disconnected', '连接失败，请点击重连');
        }
        // Display error via ui.js, check if connection error already shown
        if (connection === null || connection.state === signalR.HubConnectionState.Disconnected) {
            // alert might have already been called in setupSignalRConnection
        } else {
            alert(`游戏初始化失败: ${error.message || '未知错误'}`); // Use alert instead of displayError
        }
        return false;
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
const mpNotificationSoundPlayer = createLobbyNotificationSoundPlayer();

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
    const btnSpectateRooms = document.getElementById('mp-spectate-rooms-btn');
    const inputSpectateGame = document.getElementById('mp-spectate-game');
    const spectateRoomModal = document.getElementById('mp-spectate-room-modal');
    const spectateRoomModalClose = document.getElementById('mp-room-modal-close');
    const spectateRoomList = document.getElementById('mp-spectate-room-list');
    const outSpectate = document.getElementById('mp-spectate-output');
    const deckSelectBtn = document.getElementById('mp-deck-select-btn');

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

    const clearSpectateRoomList = () => {
        if (!spectateRoomList) return;
        spectateRoomList.replaceChildren();
    };

    const openSpectateRoomModal = () => {
        if (!spectateRoomModal) return;
        spectateRoomModal.classList.add('is-open');
        spectateRoomModal.setAttribute('aria-hidden', 'false');
        spectateRoomModalClose?.focus?.();
    };

    const closeSpectateRoomModal = () => {
        if (!spectateRoomModal) return;
        spectateRoomModal.classList.remove('is-open');
        spectateRoomModal.setAttribute('aria-hidden', 'true');
        btnSpectateRooms?.focus?.();
    };

    spectateRoomModal?.addEventListener('click', (event) => {
        if (event.target?.closest?.('[data-close-room-modal]')) {
            closeSpectateRoomModal();
        }
    });

    spectateRoomModalClose?.addEventListener('click', closeSpectateRoomModal);

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && spectateRoomModal?.classList?.contains('is-open')) {
            closeSpectateRoomModal();
        }
    });

    const buildSpectateRoomMeta = (room) => {
        const parts = [];
        if (room.round) {
            parts.push(`第 ${room.round} 局`);
        }
        parts.push(room.hasGuest || room.useAiFallback ? '对手已就位' : '等待对手');
        if (room.requiredEnvironment) {
            parts.push(getDeckEnvironmentLabel(room.requiredEnvironment));
        }
        if (room.requiredRule) {
            parts.push(getDeckRuleLabel(room.requiredRule));
        }
        return parts.join(' · ');
    };

    const renderSpectateRoomList = (rooms) => {
        if (!spectateRoomList) return;
        spectateRoomList.replaceChildren();

        if (!rooms.length) {
            const empty = document.createElement('div');
            empty.className = 'mp-room-empty';
            empty.textContent = '暂无当前房间。';
            spectateRoomList.appendChild(empty);
            return;
        }

        rooms.forEach(room => {
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'mp-room-item';
            item.disabled = !room.canSpectate;
            item.title = room.canSpectate ? '点击填入房间号' : '房间未开始，暂不可观战';

            const main = document.createElement('div');
            main.className = 'mp-room-main';

            const id = document.createElement('span');
            id.className = 'mp-room-id';
            id.textContent = room.roomId;

            const status = document.createElement('span');
            status.className = 'mp-room-status';
            status.textContent = room.statusLabel;

            main.append(id, status);
            item.appendChild(main);

            const metaText = buildSpectateRoomMeta(room);
            if (metaText) {
                const meta = document.createElement('div');
                meta.className = 'mp-room-meta';
                meta.textContent = metaText;
                item.appendChild(meta);
            }

            if (room.canSpectate) {
                item.addEventListener('click', () => {
                    if (inputSpectateGame) {
                        inputSpectateGame.value = room.roomId;
                        inputSpectateGame.focus();
                    }
                    setOutput(outSpectate, `已选择房间：${room.roomId}`);
                    closeSpectateRoomModal();
                });
            }

            spectateRoomList.appendChild(item);
        });
    };

    const roomDeckSyncState = {
        gameId: null,
        lastDeckId: null,
        inFlight: null,
        queuedDeckId: null
    };

    const updateMultiplayerFormatFromRoom = (room) => {
        const environment = normalizeEnvironment(
            room?.requiredEnvironment ?? room?.hostDeckEnvironment ?? room?.environment,
            ''
        );
        const rule = normalizeDeckRule(
            room?.requiredRule ?? room?.hostDeckRule ?? room?.rule,
            ''
        );
        if (!environment || !rule) {
            return false;
        }

        const current = getCurrentMultiplayerDeckFormat();
        if (current && current.environment === environment && current.rule === rule) {
            return false;
        }

        setMultiplayerDeckFormat({ environment, rule }, 'multiplayer');
        void refreshDeckSelectionUI();
        return true;
    };

    const markRoomDeckSynced = (handshake, deckId) => {
        const normalizedDeckId = normalizeDeckId(deckId);
        if (!handshake?.gameId || !normalizedDeckId) return;
        roomDeckSyncState.gameId = handshake.gameId;
        roomDeckSyncState.lastDeckId = normalizedDeckId;
        roomDeckSyncState.queuedDeckId = null;
        // Keep room-constrained format source as authoritative (from room status),
        // don't lock global picker format to a local deck selection.
    };

    const getDeckSyncHandshake = () => {
        const handshake = pendingHandshake || lobbyHandshake || loadCachedHandshake();
        if (!handshake) return null;
        if (handshake.role === 'spectator') return null;
        if (handshake.useAiFallback === true) return null;
        if (!handshake.gameId || !handshake.playerId || !handshake.token) return null;

        if (roomDeckSyncState.gameId !== handshake.gameId) {
            roomDeckSyncState.gameId = handshake.gameId;
            roomDeckSyncState.lastDeckId = null;
            roomDeckSyncState.queuedDeckId = null;
        }

        return handshake;
    };

    const isRoomUnavailableError = (message) => {
        const text = String(message || '');
        return text.includes('房间已关闭')
            || text.includes('房间不存在')
            || text.includes('房间已过期')
            || text.includes('Not Found')
            || text.includes('404');
    };

    const syncSelectedDeckToRoom = async (deckId, options = {}) => {
        const normalizedDeckId = normalizeDeckId(deckId);
        if (!normalizedDeckId) return;

        const handshake = getDeckSyncHandshake();
        if (!handshake) {
            if (!options.silentWhenNoRoom) {
                setDeckOutput('卡组已切换。创建或加入房间后会自动提交。');
            }
            return;
        }

        if (roomDeckSyncState.lastDeckId === normalizedDeckId && !roomDeckSyncState.inFlight) {
            if (!options.silentWhenAlreadySynced) {
                setDeckOutput('当前房间卡组已是最新。');
            }
            return;
        }

        roomDeckSyncState.queuedDeckId = normalizedDeckId;
        if (roomDeckSyncState.inFlight) {
            return roomDeckSyncState.inFlight;
        }

        roomDeckSyncState.inFlight = (async () => {
            while (roomDeckSyncState.queuedDeckId) {
                const targetDeckId = roomDeckSyncState.queuedDeckId;
                roomDeckSyncState.queuedDeckId = null;

                const latestHandshake = getDeckSyncHandshake();
                if (!latestHandshake) break;

                try {
                    await submitDeckSelection(latestHandshake, { deckId: targetDeckId });
                    markRoomDeckSynced(latestHandshake, targetDeckId);
                    setDeckOutput('已同步到当前房间。');
                } catch (err) {
                    const message = err?.message || String(err);
                    if (message.includes('房间已开始')) {
                        setDeckOutput('房间已开始，切换卡组不会影响本局。', true);
                    } else if (isRoomUnavailableError(message)) {
                        // Cached handshake may be stale after room close/expiry.
                        // Clear it to avoid repeated sync attempts when no active room exists.
                        clearCachedHandshake();
                        roomDeckSyncState.gameId = null;
                        roomDeckSyncState.lastDeckId = null;
                        roomDeckSyncState.queuedDeckId = null;
                        setDeckOutput('卡组已切换。当前无可用房间，创建或加入后会自动提交。');
                    } else {
                        setDeckOutput(`同步房间卡组失败：${message}`, true);
                    }
                }
            }
        })();

        try {
            await roomDeckSyncState.inFlight;
        } finally {
            roomDeckSyncState.inFlight = null;
        }
    };

    const getSelectedMultiplayerDeckId = async () => {
        await refreshSelectableDecks();
        const decks = getDecksForKey('multiplayer');
        const current = normalizeDeckId(deckSelectionState.selected.multiplayer);
        if (current && decks.some(deck => resolveDeckId(deck) === current)) {
            return current;
        }
        const fallback = resolveDeckId(decks[0]);
        deckSelectionState.selected.multiplayer = fallback;
        return fallback;
    };

    deckSelectBtn?.addEventListener('click', async () => {
        try {
            const targetRoomId = inputJoinGame?.value?.trim();
            if (targetRoomId && isValidGuid(targetRoomId)) {
                try {
                    const room = await getMatch(targetRoomId);
                    updateMultiplayerFormatFromRoom(room);
                } catch {
                    // Ignore probing errors while just opening deck picker.
                }
            }

            await ensureDecksLoaded();
            await refreshSelectableDecks();
            if (!getDecksForKey('multiplayer').length) {
                setDeckOutput('没有通过校验的可用卡组，请先在牌组编辑中修正。', true);
                return;
            }
            const selectedDeckId = await pickDeckFromModal({
                key: 'multiplayer',
                title: '选择多人对战卡组',
                skipRefresh: true
            });
            if (!selectedDeckId) return;
            deckSelectionState.selected.multiplayer = selectedDeckId;
            updateMultiplayerDeckSwitcher();
            setDeckOutput('');
            void syncSelectedDeckToRoom(selectedDeckId, {
                silentWhenNoRoom: true,
                silentWhenAlreadySynced: true
            });
        } catch (error) {
            setDeckOutput(`卡组加载失败：${error.message}`, true);
        }
    });

    refreshDeckSelectionUI().catch(() => {
        setDeckOutput('卡组加载失败，请稍后重试。', true);
    });

    bindButton(btnCreateHost, async () => {
        if (!outCreate) return;
        mpNotificationSoundPlayer.unlock();
        try {
            const deckId = await getSelectedMultiplayerDeckId();
            if (!deckId) {
                setDeckOutput('请先选择卡组。', true);
                return;
            }
            setOutput(outCreate, '创建房间中...');
            const handshake = await createMatch(false);
            await markReady(handshake.gameId, handshake.playerId, handshake.token);
            const selection = await submitDeckSelection(handshake, { deckId });
            if (!selection.ok) {
                setOutput(outCreate, `卡组选择失败：${selection.message}`, true);
                return;
            }
            markRoomDeckSynced(handshake, deckId);
            setPendingHandshake(handshake);
            setOutput(outCreate, `房间已创建，等待对手加入。<br>${formatHandshakeOutput(handshake)}`);

            stopMultiplayerPolling();
            const guestJoinSoundTracker = createGuestJoinSoundTracker({
                playGuestJoined: () => mpNotificationSoundPlayer.playGuestJoined()
            });
            mpPollTimerId = setInterval(async () => {
                try {
                    const room = await getMatch(handshake.gameId);
                    updateMultiplayerFormatFromRoom(room);
                    const status = room.status;
                    const guestJoined = !!room.guestPlayerId;
                    guestJoinSoundTracker.observe(room);
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

        try {
            await ensureDecksLoaded();
            const selectedDeckId = normalizeDeckId(deckSelectionState.selected.multiplayer);
            if (!selectedDeckId) {
                setDeckOutput('请先在多人对战中选择卡组。', true);
                return;
            }
            const selectedDeck = getDeckById(selectedDeckId, deckSelectionState.decks);
            if (!selectedDeck) {
                setDeckOutput('当前已选卡组不存在，请重新选择后再加入。', true);
                return;
            }

            let requiredFormat = null;
            try {
                const room = await getMatch(gameId);
                const environment = normalizeEnvironment(
                    room?.requiredEnvironment ?? room?.hostDeckEnvironment ?? room?.environment,
                    ''
                );
                const rule = normalizeDeckRule(
                    room?.requiredRule ?? room?.hostDeckRule ?? room?.rule,
                    ''
                );
                if (environment && rule) {
                    requiredFormat = { environment, rule };
                }
            } catch {
                // Ignore here and let join flow surface authoritative errors.
            }

            const selectedDeckValid = await validateDeckForSelection(selectedDeck);
            if (!selectedDeckValid) {
                setDeckOutput('当前选择卡组未通过校验，无法加入房间。', true);
                return;
            }

            if (requiredFormat && !matchesDeckFormat(selectedDeck, requiredFormat)) {
                setDeckOutput(`当前选择卡组与房主卡组不匹配（需要 ${getDeckEnvironmentLabel(requiredFormat.environment)} | ${getDeckRuleLabel(requiredFormat.rule)}），无法加入。`, true);
                return;
            }

            if (requiredFormat) {
                setMultiplayerDeckFormat(requiredFormat, 'multiplayer');
            }
            await refreshSelectableDecks();
            updateMultiplayerDeckSwitcher();
            setDeckOutput('');

            setOutput(outJoin, '加入房间中...');
            const handshake = await joinMatch(gameId, joinCode);
            await markReady(handshake.gameId, handshake.playerId, handshake.token);
            const selection = await submitDeckSelection(handshake, { deckId: selectedDeckId });
            if (!selection.ok) {
                setOutput(outJoin, `卡组选择失败：${selection.message}`, true);
                return;
            }
            markRoomDeckSynced(handshake, selectedDeckId);
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
                    updateMultiplayerFormatFromRoom(room);
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
                const deckId = await getSelectedMultiplayerDeckId();
                if (!deckId) {
                    alert('请先选择卡组。');
                    return;
                }
                const selection = await submitDeckSelection(handshake, { deckId });
                if (!selection.ok) {
                    alert(selection.message || "无法选择卡组。");
                    return;
                }
                markRoomDeckSynced(handshake, deckId);
                const room = await getMatch(handshake.gameId);
                updateMultiplayerFormatFromRoom(room);
                if (room.status === "Ready") {
                    try {
                        await startMatch(handshake.gameId, handshake.playerId, handshake.token);
                    } catch (err) {
                        // ignore
                    }
                }
                const room2 = await getMatch(handshake.gameId);
                updateMultiplayerFormatFromRoom(room2);
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

    bindButton(btnSpectateRooms, async () => {
        if (!outSpectate) return;

        try {
            if (btnSpectateRooms) {
                btnSpectateRooms.disabled = true;
            }
            setOutput(outSpectate, '加载房间状态中...');
            openSpectateRoomModal();

            const payload = await listMatches();
            const rooms = normalizeMatchRoomList(payload);
            renderSpectateRoomList(rooms);

            const spectatableCount = rooms.filter(room => room.canSpectate).length;
            if (spectatableCount > 0) {
                setOutput(outSpectate, `找到 ${spectatableCount} 个正在对战的房间，点击房间即可填入。`);
            } else if (rooms.length > 0) {
                setOutput(outSpectate, '当前尚无正在对战的房间。', true);
            } else {
                setOutput(outSpectate, '当前没有可显示的房间。', true);
            }
        } catch (err) {
            clearSpectateRoomList();
            setOutput(outSpectate, `加载房间失败：${err?.message || String(err)}`, true);
        } finally {
            if (btnSpectateRooms) {
                btnSpectateRooms.disabled = false;
            }
        }
    });

    bindButton(btnSpectate, async () => {
        if (!outSpectate) return;
        const gameId = inputSpectateGame?.value?.trim();
        if (!gameId || !isValidGuid(gameId)) {
            setOutput(outSpectate, '请输入有效的 GameId（GUID）。', true);
            return;
        }

        try {
            setOutput(outSpectate, '检测房间状态中...');

            const room = await getMatch(gameId);
            if (room?.status === "Closed") {
                const reason = room.closedReason || '房间已关闭。';
                setOutput(outSpectate, `无法开始观战：${reason}`, true);
                return;
            }

            if (room?.status !== "Started" || !room?.activeGameId) {
                setOutput(outSpectate, '房间尚未开始，暂不可观战。', true);
                return;
            }

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
            setOutput(outSpectate, '开始观战中...');
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
    const playerDeckSelectBtn = document.getElementById('sp-deck-select-btn');
    const aiDeckSelectBtn = document.getElementById('sp-ai-deck-select-btn');
    const outDeck = document.getElementById('sp-deck-output');
    const outStart = document.getElementById('sp-start-output');
    const btnStart = document.getElementById('sp-start-btn');
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

    const getSelectedSingleplayerDeckId = async () => {
        await refreshSelectableDecks();
        const decks = getDecksForKey('singleplayer');
        const current = normalizeDeckId(deckSelectionState.selected.singleplayer);
        if (current && decks.some(deck => resolveDeckId(deck) === current)) {
            return current;
        }
        const fallback = resolveDeckId(decks[0]);
        deckSelectionState.selected.singleplayer = fallback;
        return fallback;
    };

    playerDeckSelectBtn?.addEventListener('click', async () => {
        try {
            await ensureDecksLoaded();
            await refreshSelectableDecks();
            if (!getDecksForKey('singleplayer').length) {
                setDeckOutput('没有通过校验的可用卡组，请先在牌组编辑中修正。', true);
                return;
            }
            const selectedDeckId = await pickDeckFromModal({
                key: 'singleplayer',
                title: '选择玩家卡组',
                skipRefresh: true
            });
            if (!selectedDeckId) return;
            deckSelectionState.selected.singleplayer = selectedDeckId;
            updateSingleplayerDeckSwitcher();
            setDeckOutput('');
        } catch (error) {
            setDeckOutput(`卡组加载失败：${error.message}`, true);
        }
    });

    modeSelect?.addEventListener('change', () => {
        applySingleplayerMode(modeSelect.value);
        setDeckOutput('');
    });

    aiDeckSelectBtn?.addEventListener('click', async () => {
        try {
            await ensureDecksLoaded();
            await refreshSelectableDecks();
            if (!getDecksForKey('ai').length) {
                setDeckOutput('没有通过校验的可用卡组，请先在牌组编辑中修正。', true);
                return;
            }
            const title = normalizeSingleplayerMode(modeSelect?.value || deckSelectionState.singleplayerMode) === 'dual'
                ? '选择对手卡组'
                : '选择 AI 卡组';
            const selectedDeckId = await pickDeckFromModal({
                key: 'ai',
                title,
                skipRefresh: true
            });
            if (!selectedDeckId) return;
            deckSelectionState.selected.ai = selectedDeckId;
            updateSingleplayerAiDeckSwitcher();
            setDeckOutput('');
        } catch (error) {
            setDeckOutput(`卡组加载失败：${error.message}`, true);
        }
    });

    refreshDeckSelectionUI().catch(() => {
        setDeckOutput('卡组加载失败，请稍后重试。', true);
    });
    applySingleplayerMode(deckSelectionState.singleplayerMode);

    bindButton(btnStart, async () => {
        try {
            const mode = getSingleplayerMode();
            const playerDeckId = await getSelectedSingleplayerDeckId();
            const aiDeckId = ensureSelectedDeckId('ai', getDecksForKey('ai'));
            if (!playerDeckId || !aiDeckId) {
                const opponentLabelText = mode === 'dual' ? '对手' : 'AI';
                setDeckOutput(`请先选择玩家与 ${opponentLabelText} 卡组。`, true);
                return;
            }

            setStartOutput('创建对战中...');
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

    appendAfterRouteChangeHandler('singleplayer-output-reset', async () => {
        setStartOutput('');
    });
}

function setupUserInfoUI() {
    const toggle = document.getElementById('user-info-toggle');
    const card = document.getElementById('user-info-card');
    const aboutCard = document.getElementById('about-card');
    const dailyQuestCard = document.getElementById('daily-quest-card');
    const devConsoleToggle = document.getElementById('dev-console-toggle');
    const nameEl = document.getElementById('user-info-name');
    const roleEl = document.getElementById('user-info-role');
    const idEl = document.getElementById('user-info-id');
    const avatarEl = document.getElementById('user-info-avatar');
    const avatarImgEl = document.getElementById('user-info-avatar-img');
    const avatarTextEl = document.getElementById('user-info-avatar-text');
    const idInline = document.getElementById('user-id-inline');
    const coinsEl = document.getElementById('user-info-coins');
    const coinsItemEl = coinsEl?.closest('.user-resource-item.resource-coin') ?? null;
    const totalWinsEl = document.getElementById('user-info-total-wins');
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

    const winStatColumns = Array.from(card.querySelectorAll('.win-stat-column[data-class]')).map((column) => ({
        key: String(column.getAttribute('data-class') || '').trim().toLowerCase(),
        fillEl: column.querySelector('.win-stat-fill'),
        valueEl: column.querySelector('.win-stat-value')
    })).filter((item) => item.key.length > 0);
    const winStatsTitleEl = card.querySelector('#win-stats-title');
    const winStatsEnvironmentToggleBtn = card.querySelector('#win-stats-mode-toggle');

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
    let winStatsRequest = null;
    const economyCache = {
        userId: null,
        coins: null,
        fetchedAt: 0
    };
    const winStatsCache = {
        userId: null,
        environment: null,
        totalWins: null,
        classWins: null,
        fetchedAt: 0
    };
    const DEFAULT_WIN_STATS_ENVIRONMENT = getDefaultDeckEnvironment();
    const FIXED_WIN_STATS_DECK_RULE = 'standard';
    let activeWinStatsEnvironment = DEFAULT_WIN_STATS_ENVIRONMENT;
    let winStatsEnvironmentsLoaded = false;
    let winStatsEnvironments = [];

    const pickValue = (source, camelKey, pascalKey) => {
        if (!source || typeof source !== 'object') return null;
        if (source[camelKey] != null) return source[camelKey];
        if (source[pascalKey] != null) return source[pascalKey];
        return null;
    };

    const toSafeInt = (value, fallback = 0) => {
        const parsed = Number.parseInt(String(value ?? ''), 10);
        return Number.isFinite(parsed) ? parsed : fallback;
    };

    const normalizeClassKey = (value) => String(value ?? '').trim().toLowerCase();
    const normalizeWinStatsEnvironment = (value) =>
        normalizeDeckEnvironmentFromOptions(value, getDefaultDeckEnvironment());
    const getDefaultWinStatsEnvironments = () =>
        getDeckEnvironmentOptions().map((item) => ({ key: item.value, label: item.label }));
    const resolveWinStatsEnvironmentOption = (environment) => {
        const normalized = normalizeWinStatsEnvironment(environment);
        return winStatsEnvironments.find((item) => item.key === normalized) || null;
    };
    const resolveWinStatsEnvironment = (environment) =>
        resolveWinStatsEnvironmentOption(environment)?.key
        || (winStatsEnvironments[0]?.key ?? DEFAULT_WIN_STATS_ENVIRONMENT);
    const getNextWinStatsEnvironment = (environment) => {
        const current = resolveWinStatsEnvironment(environment);
        if (winStatsEnvironments.length <= 1) return current;
        const currentIndex = winStatsEnvironments.findIndex((item) => item.key === current);
        const nextIndex = currentIndex >= 0 ? (currentIndex + 1) % winStatsEnvironments.length : 0;
        return winStatsEnvironments[nextIndex].key;
    };
    const getWinStatsEnvironmentLabel = (environment) => {
        return resolveWinStatsEnvironmentOption(environment)?.label
            || getDeckEnvironmentLabelFromOptions(environment);
    };
    const getWinStatsTitleText = (environment) => `位阶对战：${getWinStatsEnvironmentLabel(environment)}`;
    const getEnvironmentToggleAriaLabel = (environment) => {
        if (winStatsEnvironments.length <= 1) {
            return `当前环境：${getWinStatsEnvironmentLabel(environment)}`;
        }
        const nextEnvironment = getNextWinStatsEnvironment(environment);
        return `切换到${getWinStatsEnvironmentLabel(nextEnvironment)}职业胜场`;
    };

    const ensureWinStatsEnvironmentsLoaded = async () => {
        if (winStatsEnvironmentsLoaded) return;
        winStatsEnvironments = getDefaultWinStatsEnvironments();
        try {
            await ensureDeckEnvironmentOptions();
            const options = getDeckEnvironmentOptions();
            if (options.length > 0) {
                winStatsEnvironments = options.map((item) => ({
                    key: item.value,
                    label: item.label
                }));
            }
        } catch (error) {
            console.warn('MAIN: Failed to load DeckEnvironment enum localization, fallback to default environment list.', error);
        } finally {
            winStatsEnvironmentsLoaded = true;
            activeWinStatsEnvironment = resolveWinStatsEnvironment(activeWinStatsEnvironment);
            syncWinStatsEnvironmentToggle();
        }
    };

    const syncWinStatsEnvironmentToggle = () => {
        const normalizedEnvironment = resolveWinStatsEnvironment(activeWinStatsEnvironment);
        activeWinStatsEnvironment = normalizedEnvironment;
        if (winStatsTitleEl) {
            winStatsTitleEl.textContent = getWinStatsTitleText(normalizedEnvironment);
        }
        if (!winStatsEnvironmentToggleBtn) return;
        winStatsEnvironmentToggleBtn.dataset.environment = normalizedEnvironment;
        winStatsEnvironmentToggleBtn.setAttribute('aria-label', getEnvironmentToggleAriaLabel(normalizedEnvironment));
        winStatsEnvironmentToggleBtn.disabled = winStatsEnvironments.length <= 1;
        const nextEnvironment = getNextWinStatsEnvironment(normalizedEnvironment);
        const nextEnvironmentText = getWinStatsEnvironmentLabel(nextEnvironment);
        winStatsEnvironmentToggleBtn.setAttribute('title', winStatsEnvironments.length <= 1 ? `当前仅支持${getWinStatsEnvironmentLabel(normalizedEnvironment)}` : `切换到${nextEnvironmentText}`);
        if (winStatsEnvironmentToggleBtn instanceof HTMLButtonElement) {
            winStatsEnvironmentToggleBtn.dataset.nextEnvironment = nextEnvironment;
        }
    };

    const setWinStatsEnvironment = (environment, forceRefresh = false) => {
        const normalized = resolveWinStatsEnvironment(environment);
        const changed = normalized !== activeWinStatsEnvironment;
        activeWinStatsEnvironment = normalized;
        syncWinStatsEnvironmentToggle();
        if (changed || forceRefresh) {
            refreshWinStats(true).catch(() => { });
        }
    };

    const createZeroClassWins = () => {
        const wins = {};
        for (const item of winStatColumns) {
            wins[item.key] = 0;
        }
        return wins;
    };

    const normalizeClassWins = (statePayload) => {
        const normalizedWins = createZeroClassWins();
        if (!winStatColumns.length) return normalizedWins;
        const classStats = pickValue(statePayload, 'classStats', 'ClassStats');
        if (!Array.isArray(classStats)) return normalizedWins;

        for (const item of classStats) {
            const rawClass = normalizeClassKey(pickValue(item, 'deckClass', 'DeckClass'));
            if (!rawClass || !(rawClass in normalizedWins)) continue;
            const wins = Math.max(0, toSafeInt(pickValue(item, 'totalWins', 'TotalWins'), 0));
            normalizedWins[rawClass] += wins;
        }
        return normalizedWins;
    };

    const setClassWinsValue = (winsByClass) => {
        if (!winStatColumns.length) return;
        const fillToneClasses = [
            'tone-forestcraft',
            'tone-swordcraft',
            'tone-runecraft',
            'tone-dragoncraft',
            'tone-abysscraft',
            'tone-havencraft',
            'tone-portalcraft'
        ];
        const normalizedWins = createZeroClassWins();
        if (winsByClass && typeof winsByClass === 'object') {
            for (const key of Object.keys(normalizedWins)) {
                const wins = toSafeInt(winsByClass[key], 0);
                normalizedWins[key] = Math.max(0, wins);
            }
        }

        let maxWins = 0;
        for (const item of winStatColumns) {
            const wins = normalizedWins[item.key] ?? 0;
            maxWins = Math.max(maxWins, wins);
            if (item.valueEl) {
                item.valueEl.textContent = String(wins);
            }
            if (item.fillEl) {
                item.fillEl.style.height = '0%';
                item.fillEl.classList.remove('active', ...fillToneClasses);
            }
        }

        if (maxWins <= 0) return;

        const minimumBarHeightPercent = 6;
        for (const item of winStatColumns) {
            const wins = normalizedWins[item.key] ?? 0;
            if (wins <= 0 || !item.fillEl) continue;
            const ratio = wins / maxWins;
            const barHeight = Math.max(minimumBarHeightPercent, Math.round(ratio * 100));
            item.fillEl.style.height = `${Math.min(barHeight, 100)}%`;
            item.fillEl.classList.add('active');
            const toneClass = `tone-${item.key}`;
            if (fillToneClasses.includes(toneClass)) {
                item.fillEl.classList.add(toneClass);
            }
        }
    };

    const setCoinsValue = (value) => {
        if (!coinsEl) return;
        coinsEl.textContent = value;
    };

    let coinsGainAnimationTimer = null;
    const triggerCoinsGainAnimation = (gain) => {
        if (!coinsItemEl || !Number.isFinite(gain) || gain <= 0) {
            return;
        }

        coinsItemEl.dataset.coinGain = `+${gain}`;
        coinsItemEl.classList.remove('is-gain-pulse');
        void coinsItemEl.offsetWidth;
        coinsItemEl.classList.add('is-gain-pulse');

        if (coinsGainAnimationTimer) {
            window.clearTimeout(coinsGainAnimationTimer);
        }
        coinsGainAnimationTimer = window.setTimeout(() => {
            coinsItemEl.classList.remove('is-gain-pulse');
            delete coinsItemEl.dataset.coinGain;
            coinsGainAnimationTimer = null;
        }, 1500);
    };

    const applyCoinsUpdate = (userId, coins) => {
        if (!coinsEl) return;
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId || session.userId !== userId) {
            return;
        }

        const previousCoins = economyCache.userId === userId && Number.isFinite(economyCache.coins)
            ? economyCache.coins
            : null;
        economyCache.userId = userId;
        economyCache.coins = coins;
        economyCache.fetchedAt = Date.now();
        setCoinsValue(String(coins));
        if (previousCoins != null && coins > previousCoins) {
            triggerCoinsGainAnimation(coins - previousCoins);
        }
    };

    const setTotalWinsValue = (value) => {
        if (!totalWinsEl) return;
        totalWinsEl.textContent = value;
    };

    const refreshEconomy = async (force = false) => {
        if (!coinsEl) return;
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId) {
            setCoinsValue('0');
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
                const coinsPayload = await apiGetJson(`/api/economy/coins/${session.userId}`);
                const coins = Math.max(0, toSafeInt(coinsPayload, 0));
                economyCache.userId = session.userId;
                economyCache.coins = coins;
                economyCache.fetchedAt = Date.now();
                setCoinsValue(String(coins));
            } catch (err) {
                setCoinsValue('0');
            } finally {
                economyRequest = null;
            }
        })();

        await economyRequest;
    };

    const refreshWinStats = async (force = false) => {
        if (!totalWinsEl && !winStatColumns.length) return;
        await ensureWinStatsEnvironmentsLoaded();
        const session = getUserSession();
        const activeEnvironment = resolveWinStatsEnvironment(activeWinStatsEnvironment);
        if (session.mode !== 'user' || !session.userId) {
                setTotalWinsValue('--');
                setClassWinsValue(null);
            return;
        }

        const now = Date.now();
        if (!force &&
            winStatsCache.userId === session.userId &&
            winStatsCache.environment === activeEnvironment &&
            now - winStatsCache.fetchedAt < 15000) {
            if (winStatsCache.totalWins != null || winStatsCache.classWins != null) {
                setTotalWinsValue(winStatsCache.totalWins != null ? String(winStatsCache.totalWins) : '--');
                setClassWinsValue(winStatsCache.classWins);
                return;
            }
        }

        if (winStatsRequest) {
            await winStatsRequest;
            if (!force &&
                winStatsCache.userId === session.userId &&
                winStatsCache.environment === activeEnvironment) {
                return;
            }
        }

        setTotalWinsValue('...');
        winStatsRequest = (async () => {
            try {
                const query = new URLSearchParams();
                query.set('environment', activeEnvironment);
                query.set('deckRule', FIXED_WIN_STATS_DECK_RULE);
                const state = await apiGetJson(`/api/economy/state/${session.userId}?${query.toString()}`);
                const classWins = normalizeClassWins(state);
                const classWinsTotal = Object.values(classWins).reduce((sum, item) => sum + toSafeInt(item, 0), 0);
                // Keep total wins aligned with current environment/rule filters.
                const totalWins = classWinsTotal;

                winStatsCache.userId = session.userId;
                winStatsCache.environment = activeEnvironment;
                winStatsCache.totalWins = totalWins;
                winStatsCache.classWins = classWins;
                winStatsCache.fetchedAt = Date.now();
                setTotalWinsValue(String(totalWins));
                setClassWinsValue(classWins);
            } catch (err) {
                setTotalWinsValue('--');
                setClassWinsValue(null);
            } finally {
                winStatsRequest = null;
            }
        })();

        await winStatsRequest;
    };

    winStatsEnvironmentToggleBtn?.addEventListener('click', () => {
        const nextEnvironment = getNextWinStatsEnvironment(activeWinStatsEnvironment);
        setWinStatsEnvironment(nextEnvironment, true);
    });
    syncWinStatsEnvironmentToggle();
    ensureWinStatsEnvironmentsLoaded().catch(() => { });

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
        refreshWinStats(true).catch(() => { });
    };

    const setOpen = (open) => {
        card.classList.toggle('active', open); // Changed from 'open' to 'active' to match CSS
        card.setAttribute('aria-hidden', open ? 'false' : 'true');
        if (open && aboutCard) {
            aboutCard.classList.remove('active');
            aboutCard.setAttribute('aria-hidden', 'true');
        }
        if (open && dailyQuestCard) {
            dailyQuestCard.classList.remove('active');
            dailyQuestCard.setAttribute('aria-hidden', 'true');
            document.getElementById('daily-quest-toggle')?.setAttribute('aria-expanded', 'false');
        }
        if (!open) {
            resetIdRow(idEl?.dataset.fullId || idEl?.textContent || "-");
        } else {
            refreshEconomy(false).catch(() => { });
            refreshWinStats(false).catch(() => { });
        }
        syncTopNavHoverLock();
    };

    applySession();

    document.addEventListener(USER_SESSION_CHANGED_EVENT, () => {
        applySession();
    });
    document.addEventListener(QUEST_REWARD_CLAIMED_EVENT, (event) => {
        const detail = event?.detail;
        const userId = String(detail?.userId ?? '').trim();
        const coins = Number.parseInt(String(detail?.coins ?? ''), 10);
        if (!userId || !Number.isFinite(coins) || coins < 0) {
            return;
        }
        applyCoinsUpdate(userId, coins);
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

function setupDailyQuestUI() {
    const widget = document.getElementById('daily-quest-widget');
    const toggle = document.getElementById('daily-quest-toggle');
    const badge = document.getElementById('daily-quest-badge');
    const toast = document.getElementById('daily-quest-toast');
    const card = document.getElementById('daily-quest-card');
    const refreshBtn = document.getElementById('daily-quest-refresh');
    const summaryEl = document.getElementById('daily-quest-summary');
    const listEl = document.getElementById('daily-quest-list');

    if (!widget || !toggle || !badge || !card || !refreshBtn || !summaryEl || !listEl) return;

    let stateRequest = null;
    let toastTimer = null;

    const pickValue = (source, camelKey, pascalKey, fallback = null) => {
        if (!source || typeof source !== 'object') return fallback;
        if (source[camelKey] !== undefined && source[camelKey] !== null) return source[camelKey];
        if (source[pascalKey] !== undefined && source[pascalKey] !== null) return source[pascalKey];
        return fallback;
    };

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const toSafeInt = (value, fallback = 0) => {
        const parsed = Number.parseInt(String(value ?? ''), 10);
        return Number.isFinite(parsed) ? parsed : fallback;
    };

    const isQuestClaimable = (quest) =>
        !!pickValue(quest, 'isCompleted', 'IsCompleted', false) &&
        !pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false) &&
        !pickValue(quest, 'isExpired', 'IsExpired', false);

    const setBadge = (claimableCount) => {
        const count = Math.max(0, toSafeInt(claimableCount, 0));
        badge.textContent = count > 9 ? '9+' : String(count);
        badge.classList.toggle('is-visible', count > 0);
        toggle.setAttribute('aria-label', count > 0 ? `日常任务，${count} 个奖励可领取` : '日常任务');
    };

    const showToast = (message) => {
        if (!toast || !message) return;
        toast.textContent = message;
        toast.classList.add('is-visible');
        if (toastTimer) {
            window.clearTimeout(toastTimer);
        }
        toastTimer = window.setTimeout(() => {
            toast.classList.remove('is-visible');
            toastTimer = null;
        }, 1500);
    };

    const pulseToggle = (completed) => {
        const className = completed ? 'is-complete-pulse' : 'is-progress-pulse';
        toggle.classList.remove('is-progress-pulse', 'is-complete-pulse');
        void toggle.offsetWidth;
        toggle.classList.add(className);
        window.setTimeout(() => {
            toggle.classList.remove(className);
        }, completed ? 760 : 560);
    };

    const setSummary = (html, isError = false) => {
        summaryEl.innerHTML = html;
        summaryEl.classList.toggle('daily-quest-error-text', isError);
    };

    const renderEmpty = (message, isError = false) => {
        listEl.innerHTML = `<div class="${isError ? 'daily-quest-error' : 'daily-quest-empty'}">${escapeHtml(message)}</div>`;
    };

    const getQuestStatus = (quest) => {
        if (pickValue(quest, 'isExpired', 'IsExpired', false)) return '已过期';
        if (pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false)) return '已领取';
        if (pickValue(quest, 'isCompleted', 'IsCompleted', false)) return '已完成';
        return '';
    };

    const getQuestIconPath = (quest) => {
        const typeText = `${pickValue(quest, 'typeDisplayName', 'TypeDisplayName', '')} ${pickValue(quest, 'name', 'Name', '')}`;
        if (typeText.includes('评论')) {
            return 'M4 5.5A2.5 2.5 0 0 1 6.5 3h11A2.5 2.5 0 0 1 20 5.5v7A2.5 2.5 0 0 1 17.5 15H9l-5 4V5.5z';
        }
        if (typeText.includes('登录')) {
            return 'M10 3h9v18h-9v-2h7V5h-7V3zm1.4 5.6L14.8 12l-3.4 3.4L10 14l1-1H4v-2h7l-1-1 1.4-1.4z';
        }
        if (typeText.includes('观战')) {
            return 'M12 5c5 0 8.5 4.2 9.6 7-1.1 2.8-4.6 7-9.6 7s-8.5-4.2-9.6-7C3.5 9.2 7 5 12 5zm0 3.5A3.5 3.5 0 1 0 12 15a3.5 3.5 0 0 0 0-7z';
        }
        if (typeText.includes('胜') || typeText.includes('对战')) {
            return 'M7 4h10v3h3v3a5 5 0 0 1-5 5h-.2A5.5 5.5 0 0 1 13 17.7V20h3v2H8v-2h3v-2.3A5.5 5.5 0 0 1 9.2 15H9a5 5 0 0 1-5-5V7h3V4zm10 5V7h-1v5.6A3 3 0 0 0 17 9zM7 7H6v2a3 3 0 0 0 1 2.2V7z';
        }
        return 'M7 3h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2zm1 4h8V5H8v2zm1 4.7 1.3 1.3 3.7-3.7 1.4 1.4-5.1 5.1L7.6 13l1.4-1.3z';
    };

    const renderQuestRow = (quest) => {
        const id = String(pickValue(quest, 'id', 'Id', '') || '');
        const name = String(pickValue(quest, 'name', 'Name', '任务') || '任务');
        const description = String(pickValue(quest, 'description', 'Description', '') || '');
        const currentProgress = Math.max(0, toSafeInt(pickValue(quest, 'currentProgress', 'CurrentProgress', 0), 0));
        const targetValue = Math.max(0, toSafeInt(pickValue(quest, 'targetValue', 'TargetValue', 0), 0));
        const coinReward = Math.max(0, toSafeInt(pickValue(quest, 'coinReward', 'CoinReward', 0), 0));
        const percent = targetValue > 0 ? Math.min(100, Math.round((currentProgress / targetValue) * 100)) : 0;
        const canClaim = isQuestClaimable(quest);
        const isClaimed = !!pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false);
        const status = getQuestStatus(quest);

        return `
            <div class="daily-quest-row ${canClaim ? 'is-claimable' : ''} ${isClaimed ? 'is-claimed' : ''}">
                <div class="daily-quest-icon" aria-hidden="true">
                    <svg viewBox="0 0 24 24"><path d="${getQuestIconPath(quest)}"></path></svg>
                </div>
                <div class="daily-quest-main">
                    <div class="daily-quest-row-head">
                        <div class="daily-quest-name" title="${escapeHtml(name)}">${escapeHtml(name)}</div>
                        <div class="daily-quest-reward">+${coinReward} 金币</div>
                    </div>
                    <div class="daily-quest-desc">${escapeHtml(description || status || '活动任务')}</div>
                    <div class="daily-quest-progress-line">
                        <div class="daily-quest-progress-track" role="progressbar" aria-valuemin="0" aria-valuemax="${targetValue}" aria-valuenow="${currentProgress}">
                            <div class="daily-quest-progress-fill" style="width: ${percent}%"></div>
                        </div>
                        <div class="daily-quest-progress-text">${currentProgress}/${targetValue}</div>
                    </div>
                </div>
                ${canClaim
                    ? `<button class="meta-btn daily-quest-claim" type="button" data-quest-id="${escapeHtml(id)}">领取</button>`
                    : `<div class="daily-quest-status">${escapeHtml(status)}</div>`}
            </div>
        `;
    };

    const renderQuestGroup = (title, quests) => {
        if (!quests.length) return '';
        return `
            <div class="daily-quest-group">
                <div class="daily-quest-group-title">${escapeHtml(title)}</div>
                ${quests.map(renderQuestRow).join('')}
            </div>
        `;
    };

    const renderQuests = (quests, claimableCount) => {
        const list = Array.isArray(quests) ? quests : [];
        if (list.length === 0) {
            renderEmpty('暂无可显示任务');
            return;
        }

        const claimable = list.filter(isQuestClaimable);
        const claimed = list.filter(quest => pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false));
        const inProgress = list.filter(quest => !isQuestClaimable(quest) && !pickValue(quest, 'isRewardClaimed', 'IsRewardClaimed', false));
        const completedCount = list.filter(quest => pickValue(quest, 'isCompleted', 'IsCompleted', false)).length;
        const resetAt = list
            .map(quest => new Date(pickValue(quest, 'expiresAt', 'ExpiresAt', '')))
            .filter(date => !Number.isNaN(date.getTime()))
            .sort((a, b) => a.getTime() - b.getTime())[0];
        const resetText = resetAt
            ? resetAt.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
            : '--:--';

        setSummary(`今日 ${completedCount}/${list.length} · <strong>可领取 ${Math.max(0, toSafeInt(claimableCount, claimable.length))}</strong> · 重置 ${resetText}`);
        listEl.innerHTML = [
            renderQuestGroup('可领取', claimable),
            renderQuestGroup('进行中', inProgress),
            renderQuestGroup('已领取', claimed)
        ].join('');
    };

    const refresh = async (force = false) => {
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId) {
            setBadge(0);
            setSummary('登录后查看任务进度');
            renderEmpty('请先登录账号');
            return;
        }

        if (stateRequest && !force) {
            await stateRequest;
            return;
        }

        refreshBtn.disabled = true;
        setSummary('加载中...');
        stateRequest = (async () => {
            try {
                const state = await apiGetJson(`/api/economy/state/${session.userId}`);
                const quests = Array.isArray(state?.quests) ? state.quests : [];
                const claimableCount = toSafeInt(pickValue(state, 'claimableQuestCount', 'ClaimableQuestCount', 0), quests.filter(isQuestClaimable).length);
                setBadge(claimableCount);
                renderQuests(quests, claimableCount);
            } catch (err) {
                setBadge(0);
                setSummary('任务加载失败', true);
                renderEmpty(`加载失败：${err?.message || String(err)}`, true);
            } finally {
                refreshBtn.disabled = false;
                stateRequest = null;
            }
        })();

        await stateRequest;
    };

    const setOpen = (open) => {
        card.classList.toggle('active', open);
        card.setAttribute('aria-hidden', open ? 'false' : 'true');
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (open) {
            document.querySelectorAll('.top-nav-bar .user-card-popup.active').forEach((item) => {
                if (item !== card) {
                    item.classList.remove('active');
                    item.setAttribute('aria-hidden', 'true');
                }
            });
            refresh(false).catch(() => { });
        }
        syncTopNavHoverLock();
    };

    toggle.addEventListener('click', (event) => {
        event.stopPropagation();
        setOpen(!card.classList.contains('active'));
    });

    refreshBtn.addEventListener('click', () => {
        refresh(true).catch(() => { });
    });

    listEl.addEventListener('click', async (event) => {
        const target = event.target;
        const btn = target instanceof Element ? target.closest('button[data-quest-id]') : null;
        const questId = btn?.getAttribute('data-quest-id');
        const session = getUserSession();
        if (!btn || !questId || session.mode !== 'user' || !session.userId) return;

        btn.disabled = true;
        btn.textContent = '领取中';
        try {
            const payload = await apiPostJson(`/api/economy/quests/${session.userId}/${questId}/claim`, {});
            const currentCoins = toSafeInt(pickValue(payload, 'currentCoins', 'CurrentCoins', null), -1);
            const coinReward = Math.max(0, toSafeInt(pickValue(payload, 'coinReward', 'CoinReward', 0), 0));
            if (currentCoins >= 0) {
                document.dispatchEvent(new CustomEvent(QUEST_REWARD_CLAIMED_EVENT, {
                    detail: {
                        userId: session.userId,
                        coins: currentCoins,
                        gain: coinReward
                    }
                }));
            }
            showToast(`已领取 +${coinReward} 金币`);
        } catch (err) {
            showToast(`领取失败：${err?.message || String(err)}`);
        } finally {
            await refresh(true);
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

    document.addEventListener(USER_SESSION_CHANGED_EVENT, () => {
        refresh(true).catch(() => { });
    });

    document.addEventListener(QUEST_PROGRESS_UPDATED_EVENT, (event) => {
        const detail = event?.detail;
        const session = getUserSession();
        if (session.mode !== 'user' || !session.userId || String(detail?.userId ?? '') !== session.userId) {
            return;
        }

        const progress = detail?.questProgress;
        const completed = Array.isArray(progress?.completedQuestIds) && progress.completedQuestIds.length > 0;
        const updated = Array.isArray(progress?.updatedQuestIds) && progress.updatedQuestIds.length > 0;
        if (!completed && !updated) {
            return;
        }

        pulseToggle(completed);
        showToast(completed ? '任务完成，可领取奖励' : '任务进度 +1');
        refresh(true).catch(() => { });
    });

    refresh(false).catch(() => { });
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
    const dailyQuestCard = document.getElementById('daily-quest-card');
    const devConsoleToggle = document.getElementById('dev-console-toggle');
    if (!toggle || !card) return;

    const setOpen = (open) => {
        card.classList.toggle('active', open);
        card.setAttribute('aria-hidden', open ? 'false' : 'true');
        if (open && userCard) {
            userCard.classList.remove('active');
            userCard.setAttribute('aria-hidden', 'true');
        }
        if (open && dailyQuestCard) {
            dailyQuestCard.classList.remove('active');
            dailyQuestCard.setAttribute('aria-hidden', 'true');
            document.getElementById('daily-quest-toggle')?.setAttribute('aria-expanded', 'false');
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
    if (!appendAfterRouteChangeHandler('topnav-hover-lock', async () => {
        syncTopNavHoverLock();
    })) return;
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

    const ensureOverlayImageLoaded = route => {
        const overlay = overlayByRoute.get(route);
        const image = overlay?.querySelector('.menu-route-image');
        const targetSrc = image?.dataset?.src;
        if (!image || !targetSrc || image.dataset.loaded === 'true') {
            return;
        }

        image.decoding = 'async';
        image.loading = 'lazy';
        image.fetchPriority = 'low';
        image.src = targetSrc;
        image.dataset.loaded = 'true';
    };

    let pinnedRoute = null;
    let activeRoute = null;
    let hoverRoute = null;

    const showOverlay = (route) => {
        ensureOverlayImageLoaded(route);
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

    if (appendAfterRouteChangeHandler('menu-route-overlays', async (_oldRoute, newRoute) => {
            syncPinnedWithRoute(newRoute);
    })) {
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
    const perfDebug = getPerfDebugState();
    const menuPerfState = perfDebug.menuStarrySky;

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
        const now = typeof performance !== 'undefined' && typeof performance.now === 'function'
            ? performance.now()
            : Date.now();
        if (menuPerfState) {
            const deltaMs = menuPerfState.lastFrameAt > 0 ? now - menuPerfState.lastFrameAt : 0;
            menuPerfState.lastFrameAt = now;
            menuPerfState.frameCount += 1;
            if (deltaMs >= 34) {
                menuPerfState.slowFrameCount += 1;
                if (menuPerfState.slowFrameCount <= 12) {
                    logPerfDebug('menu-starry-sky', 'slow frame detected', {
                        deltaMs: Number(deltaMs.toFixed(1)),
                        route: router.getCurrentRoute ? router.getCurrentRoute() : '',
                        frameCount: menuPerfState.frameCount,
                        slowFrameCount: menuPerfState.slowFrameCount
                    });
                }
            }
        }

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
        if (menuPerfState) {
            menuPerfState.isAnimating = true;
            menuPerfState.lastFrameAt = 0;
            menuPerfState.frameCount = 0;
            menuPerfState.slowFrameCount = 0;
            menuPerfState.route = router.getCurrentRoute ? router.getCurrentRoute() : '';
        }
        logPerfDebug('menu-starry-sky', 'start', {
            route: router.getCurrentRoute ? router.getCurrentRoute() : '',
            prefersReducedMotion,
            isMobile
        });
        draw();
    };

    const stop = () => {
        if (!isAnimating) return;
        isAnimating = false;
        cancelAnimationFrame(animationFrameId);
        if (menuPerfState) {
            menuPerfState.isAnimating = false;
            menuPerfState.route = router.getCurrentRoute ? router.getCurrentRoute() : '';
        }
        logPerfDebug('menu-starry-sky', 'stop', {
            route: router.getCurrentRoute ? router.getCurrentRoute() : '',
            frameCount: menuPerfState?.frameCount ?? 0,
            slowFrameCount: menuPerfState?.slowFrameCount ?? 0
        });
    };

    const syncAnimationWithRoute = (route) => {
        const shouldAnimate = !route || menuRoutes.has(route);
        allowAnimation = shouldAnimate;
        if (menuPerfState) {
            menuPerfState.route = route || '';
        }
        logPerfDebug('menu-starry-sky', 'sync route state', {
            route: route || '',
            shouldAnimate,
            isAnimating
        });
        if (!shouldAnimate) {
            stop();
            return;
        }
        if (document.hidden) return;
        start();
    };

    window.addEventListener('resize', resizeHandler);
    resizeHandler();

    if (appendAfterRouteChangeHandler('menu-starry-sky', async (_oldRoute, newRoute) => {
            syncAnimationWithRoute(newRoute);
    })) {
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

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

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
            const current = Number.parseInt(String(quest?.currentProgress ?? 0), 10) || 0;
            const target = Number.parseInt(String(quest?.targetValue ?? 0), 10) || 0;
            const percent = target > 0 ? Math.min(100, Math.round((current / target) * 100)) : 0;
            const progressText = `${current}/${target} · +${quest?.coinReward ?? 0} 金币`;
            const statusText = canClaim ? '可领取' : (quest?.statusDescription ?? '');

            const wrapper = document.createElement('div');
            wrapper.className = `meta-quest ${canClaim ? 'is-claimable' : ''}`;
            wrapper.innerHTML = `
                <div class="meta-quest-title">
                    <div>${escapeHtml(quest?.name ?? 'Quest')}</div>
                    <small>${escapeHtml(statusText || quest?.typeDisplayName || '')}</small>
                </div>
                <div class="meta-quest-progress">${escapeHtml(progressText)}</div>
                <div class="meta-quest-progress-bar" role="progressbar" aria-valuemin="0" aria-valuemax="${target}" aria-valuenow="${current}">
                    <span style="width: ${percent}%"></span>
                </div>
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
            const claimableQuestCount = Number.parseInt(String(state?.claimableQuestCount ?? 0), 10) || 0;

            setSummary(`金币 ${coins} · 段位 ${rankName} (${rankPoints}) · 可领取任务 ${claimableQuestCount} · 每日 ${canClaimDaily ? '可领取' : '已领取'}`);
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
            const payload = await apiPostJson(`/api/economy/quests/${userId}/${questId}/claim`, {});
            const currentCoins = Number.parseInt(String(payload?.currentCoins ?? payload?.CurrentCoins ?? ''), 10);
            const coinReward = Number.parseInt(String(payload?.coinReward ?? payload?.CoinReward ?? 0), 10) || 0;
            if (Number.isFinite(currentCoins) && currentCoins >= 0) {
                document.dispatchEvent(new CustomEvent(QUEST_REWARD_CLAIMED_EVENT, {
                    detail: {
                        userId,
                        coins: currentCoins,
                        gain: coinReward
                    }
                }));
            }
        } catch (err) {
            setSummary(`领取失败：${err?.message || String(err)}`, true);
        } finally {
            await refresh();
        }
    });

    refresh().catch(() => { });
}

async function initializeGamePage() {
    if (gameInitialized) return;
    gamePageSession++;
    gameInitialized = await initializeApp();
}

async function cleanupGamePage() {
    gamePageSession++;
    reconnectInProgress = false;
    const handshake = getActiveNonAiRoomHandshake();
    const shouldDeferLeave = shouldDeferRoomLeaveForDecision(
        pendingRouteTransition?.fromRoute,
        pendingRouteTransition?.toRoute,
        handshake,
        { gameOver: gameState.gameOver === true, surrenderSubmitted: confirmedBattleExitSurrender === true }
    );
    if (shouldDeferLeave) rememberPendingRoomExitDecision();
    document.dispatchEvent(new CustomEvent('cleanupGame'));
    try {
        const closeConnections = disconnectGameConnections();
        let leaveRoom;
        if (shouldDeferLeave) {
            leaveRoom = Promise.resolve();
        } else if (confirmedBattleExitSurrender) {
            clearCachedHandshake();
            leaveRoom = Promise.resolve();
        } else {
            leaveRoom = leaveCurrentPvpRoom('cleanupGame');
        }
        const completed = await Promise.allSettled([closeConnections, leaveRoom]);
        const failures = completed.filter(item => item.status === 'rejected').map(item => item.reason);
        if (failures.length) throw new AggregateError(failures, '退出对战未完成，请重试');
        resetTransientConnectionUiState();
    } catch (error) {
        clearPendingRoomExitDecision();
        setConnectionStatus('disconnected', error.message);
        throw error;
    } finally {
        clearGameSession();
        initialStateReceived = false;
        forceFullStateOnNextReceive = false;
        confirmedBattleExitSurrender = false;
        gameInitialized = false;
        clearTransientMatchQueryParams();
    }
}

async function cleanupMultiplayerPage() {
    document.body.classList.remove('show-multiplayer');
    stopMultiplayerPolling();
    if (suppressMultiplayerLeaveOnce) {
        suppressMultiplayerLeaveOnce = false;
        return;
    }
    const handshake = getActiveNonAiRoomHandshake();
    if (shouldDeferRoomLeaveForDecision(
        pendingRouteTransition?.fromRoute, pendingRouteTransition?.toRoute, handshake, { gameOver: false }
    )) {
        rememberPendingRoomExitDecision();
    } else {
        await leaveCurrentPvpRoom('cleanupMultiplayer');
    }
    stopConnectionHeartbeat(connection);
    stopConnectionHeartbeat(guestConnection);
}

// 入口拥有连接与页面业务；路由等待这些生命周期，DOM 事件仅通知视图清理。
router.setRouteLifecycle('game', { init: initializeGamePage, cleanup: cleanupGamePage });
router.setRouteLifecycle('multiplayer', { cleanup: cleanupMultiplayerPage });
registerRoomExitDecisionRouteHooks();
registerBeforeUnloadGuard();

document.addEventListener('requestReconnect', () => {
    performManualReconnect().catch(error => console.error('MAIN: 重连处理失败。', error));
});

// 初始化开发者控制台（如果需要）
document.addEventListener('DOMContentLoaded', async () => {

    ensureEnumLocalizations(['CardRarity', 'CardClass', 'CardType', 'DeckEnvironment'])
        .catch(error => {
            console.warn('MAIN: Failed to prefetch card enum labels.', error);
        });
    ensureDeckEnvironmentOptions().catch(error => {
        console.warn('MAIN: Failed to prefetch DeckEnvironment options.', error);
    });
    
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
        appendAfterRouteChangeHandler('deck-selection-context-route', async (_oldRoute, newRoute) => {
            if (newRoute !== 'multiplayer' && newRoute !== 'singleplayer') {
                return;
            }

            writeDeckSelectionContext({
                source: newRoute,
                ...(getCurrentMultiplayerDeckFormat() || DEFAULT_MULTIPLAYER_DECK_FORMAT)
            });
        });

        const initialRoute = router?.getCurrentRoute?.();
        if (initialRoute === 'multiplayer' || initialRoute === 'singleplayer') {
            writeDeckSelectionContext({
                source: initialRoute,
                ...(getCurrentMultiplayerDeckFormat() || DEFAULT_MULTIPLAYER_DECK_FORMAT)
            });
        }
    } catch (error) {
        console.error('MAIN: Failed to initialize deck selection context route handler:', error);
    }

    try {
        setupUserInfoUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize user info UI:', error);
    }

    try {
        setupDailyQuestUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize daily quest UI:', error);
    }

    try {
        setupAboutUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize about UI:', error);
    }

    try {
        setupPwaInstallUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize PWA install UI:', error);
    }

    try {
        await registerPwaServiceWorker({ productionMode: import.meta.env.PROD });
    } catch (error) {
        console.error('MAIN: Failed to register PWA service worker:', error);
    }

    try {
        setupTopNavHoverLock();
    } catch (error) {
        console.error('MAIN: Failed to initialize top nav hover lock:', error);
    }

    try {
        await initializeBattleCustomization({
            appendAfterRouteChangeHandler,
            getCurrentRoute: () => router?.getCurrentRoute?.() || ''
        });
    } catch (error) {
        console.error('MAIN: Failed to initialize battle customization:', error);
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
    invalidateDeckValidationCache();
    deckSelectionState.loaded = false;
    deckSelectionState.loading = null;
    refreshDeckSelectionUI().catch(() => {});
});

document.addEventListener(DECK_VALIDATION_RULES_UPDATED_EVENT, () => {
    invalidateDeckValidationCache();
    deckSelectionState.loaded = false;
    deckSelectionState.loading = null;
    refreshDeckSelectionUI().catch(() => {});
});

// 注意：不再自动启动游戏，而是通过路由系统按需启动

function startRouter() {
    initializeRouter().catch(error => console.error('MAIN: 初始页面加载失败。', error));
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', startRouter, { once: true });
} else {
    startRouter();
}
