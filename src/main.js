import { installLocalAiTraining } from './localAiTraining.js';
if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', installLocalAiTraining, {once:true}); else void installLocalAiTraining();
import { createRevealPresentationGate } from "./revealPresentationGate.js";
import { formatRoomHandshake } from "./multiplayerRoomPresentation.js";
import { loadAboutChangelog } from "./aboutChangelog.js";
import "./styles/mini-feature-theme.css";
import { initializeTakeTwo, tryJoinTakeTwo, leaveTakeTwoRoom, getActiveTakeTwoRoom, configureTakeTwoRoomEntry } from './takeTwo.js';
import { listSpectatableRooms, showTakeTwoSpectator, findTakeTwoSpectatorRoom } from './takeTwoSpectator.js';
import { confirmDialog } from './siteDialog.js';
import { showAlert } from './siteDialog.js';
// src/main.js - 主入口模块
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - API集成: 文档/03-前端架构/03-API集成.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
//
// 导入核心模块
import { installOperationSettingsButton } from './operationSettings.js';
if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', installOperationSettingsButton, { once: true });
else installOperationSettingsButton();
import * as signalR from "@microsoft/signalr";
import { router, initializeRouter } from './router.js';
import { battleExitConfirmMessage, isSpectatorBattle, shouldConfirmBattleExit } from './battleExitConfirmPolicy.js';
import { getDeckOwnerPayload, getDeckOwnerQuery, getUserSession, USER_SESSION_CHANGED_EVENT } from './userSession.js';
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
    handleCardMoved, handleEmblemUpdated, handleCounterChanged, handleCardReturned, handleTuck, appendGameLog,
    handleMirageCombatReplay, abortMirageCombatPlayback, handleMirageStandingsUpdated, switchMirageSpectateTargetRequest // 【新增】导入最大值更新处理函数与幻境战斗回放门禁
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
    refreshCardActionControls, replaceBattleCardPreviewIdentity, showPlayerChoiceUI, showRevealOverlay, showFusionAnimation, showAbilityTriggerToast,
    replaceInMatchChatHistory, replaceInMatchCardHistory, appendInMatchChatMessage, clearInMatchChatUiState, appendInMatchUsedCard,
    appendInMatchDestroyedCard, clearInMatchUsedCardUiState, cancelCommittedHandPlayVisual,
    prepareCardMoveVisual, commitCardMoveVisual, notifyCardPlayed, playAttackLunge, playUnitEvolutionEffect, notifyShieldRemoved, cancelAllBattleMotions,
    startMirageCombatPlaybackUi, renderMirageCombatStep, renderMirageCombatOutcome, hideMirageCombatPlaybackOverlay, showMirageReconnectLockOverlay
} from './ui.js'; // Removed non-existent displayError/Info/initializeUI, Added UI function imports - functions to be implemented in ui
import { initDevConsole } from './devConsole.js'; // 开发者控制台
import { ensureEnumLocalizations } from './enumLocalizationCache.js';
import { getRouteNameFromHash, shouldReuseCachedHandshake } from './handshakePolicy.js';
import {
    canShowRematchRoomButton,
    isNonAiMultiplayerHandshake,
    isRoomSeatAlreadyReleased,
    resolveRematchPollStartedAction,
    shouldDeferRoomLeaveForDecision
} from './roomExitDecisionPolicy.js';
import { getStoredValue, removeStoredValue, setStoredValue } from './storageFallback.js';
import { buildDebugLogLine, getConnectionDebugId } from './debugDiagnostics.js';
import { createDeckSelectionPickerModal } from './components/deckSelectionPickerModal.js';
import { registerPwaServiceWorker, setupPwaInstallUI } from './pwaInstall.js';
import { setupMobileViewport } from './mobileViewport.js';
import {
    ensureDeckEnvironmentOptions,
    getDefaultDeckEnvironment
} from './deckEnvironmentOptions.js';
import {
    buildDeckPresentation,
    formatDeckSummaryLabel,
    getDeckClassLabel,
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
import { setupDailyQuestUI } from './dailyQuestMenu.js';
import { setupUserInfoUI } from './userMenu.js';
import menuBg1Url from './background/background_1.jpg';
import menuBg2Url from './background/background_2.jpg';
import menuBg3Url from './background/background_3.jpg';
import menuBg4Url from './background/background_4.jpg';
import menuBg5Url from './background/background_5.jpg';
// 在页面初始化前接入真实 console；面板首次打开时才创建 DOM。
bindGameView({
    renderGame, renderGameLog, showDamageNumber, showHealNumber, updatePpDisplay, updateEpDisplay, updateSepDisplay, updatePpBoostDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateBloodPoolDisplay, updateComboDisplay, updateRallyDisplay, updateHealthDisplay, updateUnitCombatStats, clearSelectionHighlights, resetMulliganSelection, updateHandDisplay, refreshHandPlayabilityVisuals, updateUnitDisplay, updateFieldDisplay, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus, refreshBattleCardPreviewIfCurrent, renderHighlights, updateActButtonState, updateCardActionButtons, updateEmblemZone, showTurnStartBanner, updateTurnTimerDisplay, refreshCardInstanceUi, refreshCardActionControls, replaceBattleCardPreviewIdentity, showPlayerChoiceUI, showRevealOverlay, showFusionAnimation, showAbilityTriggerToast, replaceInMatchChatHistory, replaceInMatchCardHistory, appendInMatchChatMessage, clearInMatchChatUiState, appendInMatchUsedCard, appendInMatchDestroyedCard, clearInMatchUsedCardUiState, cancelCommittedHandPlayVisual,
    prepareCardMoveVisual, commitCardMoveVisual, notifyCardPlayed, playAttackLunge, playUnitEvolutionEffect, notifyShieldRemoved, cancelAllBattleMotions,
    startMirageCombatPlaybackUi, renderMirageCombatStep, renderMirageCombatOutcome, hideMirageCombatPlaybackOverlay, showMirageReconnectLockOverlay
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
let reconnectEpochResetPending = false;
let reconnectDesiredSpectateTarget = null;
let reconnectInProgress = false;
let lobbyHandshake = null; // { gameId, playerId, token, joinCode?, role, useAiFallback?, dualControl?, hostPlayerId?, hostToken?, guestPlayerId?, guestToken? }
let pendingHandshake = null; // Set by multiplayer UI before initialization
let pendingRouteTransition = null; // { fromRoute, toRoute }
let pendingRoomExitDecision = null; // { sourceRoute, targetRoute, gameId }
let confirmedBattleExitSurrender = false;
let returningToLobbyForRematch = false;
let rematchExitedActiveGameId = null;
let rematchCapabilityProbeGameId = null;
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
    return readApiJsonResponse(response);
}

async function apiGetJson(url) {
    const response = await fetch(url, { method: "GET" });
    return readApiJsonResponse(response);
}

async function readApiJsonResponse(response) {
    const text = await response.text();
    if (response.ok) return text ? JSON.parse(text) : null;
    let payload = text || null;
    try {
        if (text) payload = JSON.parse(text);
    } catch {
        // Error responses may be plain text rather than JSON.
    }
    const message = typeof payload === 'string' ? payload
        : payload?.message || payload?.Message || payload?.error || response.statusText;
    throw Object.assign(new Error(message || `HTTP ${response.status}`), { status: response.status });
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
    const localDraft = new URLSearchParams(window.location.search).has('localDraftRoom');
    const didStore = localDraft
        ? (sessionStorage.setItem(LOBBY_HANDSHAKE_STORAGE_KEY, serialized), true)
        : setStoredValue(LOBBY_HANDSHAKE_STORAGE_KEY, serialized);
    if (!didStore) {
        console.warn("Failed to cache handshake: no writable storage backend");
    }
}

function loadCachedHandshake() {
    if (lobbyHandshake) return lobbyHandshake;
    try {
        const raw = new URLSearchParams(window.location.search).has('localDraftRoom')
            ? sessionStorage.getItem(LOBBY_HANDSHAKE_STORAGE_KEY)
            : getStoredValue(LOBBY_HANDSHAKE_STORAGE_KEY);
        if (raw) {
            const parsed = JSON.parse(raw);
            if (parsed.kind === 'mirage-8p' && parsed.roomId) {
                lobbyHandshake = parsed;
                return parsed;
            }
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
    rematchExitedActiveGameId = null;
    rematchCapabilityProbeGameId = null;
    deckSelectionState.multiplayerFormat = null;
    clearPendingRoomExitDecision();
    const removed = new URLSearchParams(window.location.search).has('localDraftRoom')
        ? (sessionStorage.removeItem(LOBBY_HANDSHAKE_STORAGE_KEY), true)
        : removeStoredValue(LOBBY_HANDSHAKE_STORAGE_KEY);
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
    if (isSpectatorBattle(gameState, handshake)) return null;
    if (!isNonAiMultiplayerHandshake(handshake)) return null;
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

    const fromLobby = pendingRoomExitDecision?.sourceRoute === 'multiplayer';
    const bodyText = fromLobby
        ? '你仍在多人对战房间候战中，可以返回房间继续等待，或退出并解散/释放座位。'
        : '你可以返回当前多人对战，或断开并退出该房间。';
    const returnLabel = fromLobby ? '返回房间' : '返回对战';

    roomExitDecisionPromise = new Promise((resolve) => {
        roomExitDecisionResolve = resolve;

        const modal = document.createElement('div');
        modal.className = 'room-exit-decision-modal';
        modal.innerHTML = `
            <div class="room-exit-decision-card" role="dialog" aria-label="对战连接确认" aria-modal="true">
                <div class="room-exit-decision-title">已返回主界面</div>
                <div class="room-exit-decision-body">
                    ${bodyText}
                </div>
                <div class="room-exit-decision-actions">
                    <button type="button" class="room-exit-decision-btn room-exit-decision-return">${returnLabel}</button>
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

    const sourceRoute = pendingRoomExitDecision.sourceRoute;
    const action = await showRoomExitDecisionPrompt();

    if (!pendingRoomExitDecision) {
        return;
    }

    if (action === 'return') {
        clearPendingRoomExitDecision();
        await router.navigateTo(sourceRoute === 'multiplayer' ? 'multiplayer' : 'game');
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

        if (shouldConfirmBattleExit(oldRoute, newRoute, routeButton, gameState, getReconnectHandshake())) {
            if (!await confirmDialog(battleExitConfirmMessage(gameState, getReconnectHandshake()))) return false;
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
    const handshake = getReconnectHandshake();
    if (isSpectatorBattle(gameState, handshake)) {
        clearCachedHandshake();
        return true;
    }
    if (handshake?.kind === 'mirage-8p' && handshake.roomId && handshake.playerId && handshake.token) {
        try {
            await apiPostJson(`/api/mirage/rooms/${encodeURIComponent(handshake.roomId)}/leave`, {
                playerId: handshake.playerId,
                token: handshake.token
            });
            confirmedBattleExitSurrender = true;
            clearCachedHandshake();
            return true;
        } catch (err) {
            showAlert(`退赛失败：${err?.message || '未知错误'}`);
            return false;
        }
    }
    const result = await surrenderGame();
    if (!result?.ok) {
        showAlert(`投降失败：${result?.error || '未知错误'}`);
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

async function createMatch(useAiFallback, options = {}) {
    const session = getUserSession();
    const isPublic = options?.isPublic !== false;
    const mode = options?.mode || 'Standard';
    const body = {
        useAiFallback,
        isPublic: Boolean(isPublic),
        hostPlayerId: session.userId || null,
        mode
    };
    const payload = await apiPostJson("/api/match", body);
    const resolvedMode = String(payload.mode || mode || 'Standard');
    const isMirage = payload.isMirage === true || resolvedMode.toLowerCase() === 'mirage';
    return {
        gameId: payload.roomId || payload.gameId,
        playerId: payload.hostPlayerId,
        token: payload.hostToken,
        joinCode: payload.joinCode,
        role: "host",
        useAiFallback: payload.useAiFallback,
        isPublic: payload.isPublic !== false,
        mode: resolvedMode,
        isMirage
    };
}

async function joinMatch(gameId, joinCode, options = {}) {
    const session = getUserSession();
    const resolvedPlayerId = options.playerId === undefined
        ? (session.userId || session.guestId || null)
        : options.playerId;
    const trimmedGameId = String(gameId || '').trim();
    const trimmedCode = String(joinCode || '').trim();
    const endpoint = trimmedGameId
        ? `/api/match/${encodeURIComponent(trimmedGameId)}/join`
        : `/api/match/join-by-code`;
    const payload = await apiPostJson(endpoint, {
        joinCode: trimmedCode,
        playerId: resolvedPlayerId,
        ...(options.token ? { token: options.token } : {})
    });
    const resolvedMode = String(payload.mode || 'Standard');
    const isMirage = payload.isMirage === true || resolvedMode.toLowerCase() === 'mirage';
    return {
        gameId: payload.roomId || payload.gameId,
        playerId: payload.guestPlayerId,
        token: payload.guestToken,
        joinCode: payload.joinCode || trimmedCode,
        role: "guest",
        useAiFallback: payload.useAiFallback,
        isPublic: payload.isPublic !== false,
        mode: resolvedMode,
        isMirage,
        ...(typeof payload.canRematch === 'boolean' ? { canRematch: payload.canRematch } : {})
    };
}

async function markReady(gameId, playerId, token, ready = true) {
    return await apiPostJson(`/api/match/${encodeURIComponent(gameId)}/ready`, { playerId, token, ready });
}

async function startMatch(gameId, playerId, token) {
    return await apiPostJson(`/api/match/${encodeURIComponent(gameId)}/start`, { playerId, token });
}

async function kickGuestFromMatch(gameId, playerId, token, targetGuestPlayerId = null) {
    return await apiPostJson(`/api/match/${encodeURIComponent(gameId)}/kick`, {
        playerId,
        token,
        ...(targetGuestPlayerId ? { targetGuestPlayerId } : {})
    });
}

async function getMatch(gameId, auth = null) {
    const effectiveAuth = auth || (() => {
        const candidate = pendingHandshake || lobbyHandshake;
        if (candidate && candidate.gameId === gameId && candidate.playerId && candidate.token) {
            return { playerId: candidate.playerId, token: candidate.token };
        }
        return null;
    })();
    const query = effectiveAuth?.playerId && effectiveAuth?.token
        ? `?playerId=${encodeURIComponent(effectiveAuth.playerId)}&token=${encodeURIComponent(effectiveAuth.token)}`
        : '';
    return await apiGetJson(`/api/match/${encodeURIComponent(gameId)}${query}`);
}

async function listMatches() {
    return await apiGetJson("/api/match");
}

async function leaveMatch(gameId, playerId, token) {
    await apiPostJson(`/api/match/${encodeURIComponent(gameId)}/leave`, { playerId, token });
}

async function leaveCurrentPvpRoom(reasonForLog) {
    const handshake = pendingHandshake || lobbyHandshake || loadCachedHandshake();
    if (!handshake || handshake.useAiFallback === true || isSpectatorBattle(gameState, handshake)) {
        clearCachedHandshake();
        return;
    }

    try {
        if (handshake.kind === 'mirage-8p') {
            if (handshake.roomId && handshake.playerId && handshake.token) {
                await apiPostJson(`/api/mirage/rooms/${encodeURIComponent(handshake.roomId)}/leave`, {
                    playerId: handshake.playerId,
                    token: handshake.token
                });
            }
        } else {
            await leaveMatch(handshake.gameId, handshake.playerId, handshake.token);
        }
    } catch (error) {
        // A missing seat needs no further release; transient failures still retain its credentials.
        if (!isRoomSeatAlreadyReleased(error)) throw error;
    }
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

    const activeHs = getReconnectHandshake();
    const query = new URLSearchParams();
    query.set('gameId', gameId);
    query.set('playerId', playerId);
    query.set('token', token);
    if (activeHs?.kind === 'mirage-8p' && activeHs.roomId) {
        query.set('mirageRoomId', activeHs.roomId);
        if (activeHs.viewingPlayerId && activeHs.viewingPlayerId !== playerId) {
            query.set('targetPlayerId', activeHs.viewingPlayerId);
        }
    }
    const hubUrlWithParams = `${HUB_URL}?${query.toString()}`;

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
        showAlert(`连接游戏服务器失败: ${err.message || 'Unknown error'}`); // Use alert instead of displayError
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
            showAlert(reason);
            clearCachedHandshake();
            void router.navigateTo('navigation').catch(error => {
                console.error('MAIN: 退出已关闭房间失败。', error);
            });
            return;
        }

        showAlert(`操作失败: ${message}`);
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
    const activeHs = getReconnectHandshake();
    const query = new URLSearchParams();
    query.set('gameId', gameId);
    query.set('spectate', '1');
    if (activeHs?.kind === 'mirage-8p' && activeHs.roomId) {
        query.set('mirageRoomId', activeHs.roomId);
        if (activeHs.viewingPlayerId) {
            query.set('targetPlayerId', activeHs.viewingPlayerId);
        }
    }

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
        showAlert(`连接游戏服务器失败: ${err.message || 'Unknown error'}`);
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
    const presentationGate=createRevealPresentationGate(()=>connection===conn);
    const onCurrentEvent = (eventName, handler) => conn.on(eventName, (...args) => {
        presentationGate.dispatch(eventName,()=>handler(...args));
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
                const completion = handler(eventData);
                appendGameLog(eventName, payloadForLog);
                return completion;
            }
            return handler(eventData);
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
            if (!forceFullStateOnNextReceive && !reconnectEpochResetPending && gameState.phase === 'Mulligan' && receivedGameState?.Phase === 'Mulligan') {
                return;
            }
            const shouldResetViewEpoch = reconnectEpochResetPending;
            forceFullStateOnNextReceive = false;
            reconnectEpochResetPending = false;

            // Subsequent state handling after initialization
            // Directly call gameManager's update function to handle the state and re-render
            // Ensure gameManager and updateFullGameState are imported correctly
            try {
                setConnectionStatus('connected');
                const hydrationSucceeded = updateFullGameState(receivedGameState, {
                    isReconnectFullSync: true,
                    resetViewEpoch: shouldResetViewEpoch
                }); // Directly update the game state and UI
                if (hydrationSucceeded === false) {
                    void handleStateHydrationFailure('重连失败').catch(error => console.error('MAIN: 状态同步失败后的连接清理失败。', error));
                    return;
                }
                syncRematchRoomButtonVisibility();
            } catch (error) { // Catch for outer try (Line 98)
                console.error("MAIN: Error calling updateFullGameState for subsequent update:", error);
                void handleStateHydrationFailure('重连失败').catch(error => console.error('MAIN: 状态同步失败后的连接清理失败。', error));
                showAlert("处理游戏状态更新时出错！");
            } // Closes outer catch
        } else {
            // This case should ideally not happen if setupSignalRConnection is always called with a resolver during init
            console.error("MAIN Error: Received game state but initialization seems incomplete or resolver missing!");
            showAlert("内部错误：无法正确处理游戏状态！");
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
    wireEvent("GameOver", (eventData) => {
        handleGameOver(eventData);
        syncRematchRoomButtonVisibility();
    });
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
    onCurrentEvent("ReceiveMirageCombatReplay", (replayDto) => {
        logEventReceipt("ReceiveMirageCombatReplay", replayDto);
        void handleMirageCombatReplay(replayDto);
    });
    onCurrentEvent("ReceiveMirageStandings", (standingsDto) => {
        logEventReceipt("ReceiveMirageStandings", standingsDto);
        handleMirageStandingsUpdated(standingsDto);
    });
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

        if (message.startsWith("ROOM_CLOSED:") || message.startsWith("ROOM_KICKED:")) {
            abortMirageCombatPlayback("leave_room");
            const prefix = message.startsWith("ROOM_KICKED:") ? "ROOM_KICKED:" : "ROOM_CLOSED:";
            const reason = message.slice(prefix.length).trim() || "房间已关闭。";
            showAlert(reason);
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

        showAlert(`操作失败: ${message}`); // Use alert instead of displayError
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
        abortMirageCombatPlayback("reconnecting");
        logPerfDebug('signalr-game', 'connection closed', {
            connectionId,
            state: conn.state,
            error: error ? { message: error.message, name: error.name } : null
        });
        console.error(`MAIN: SignalR Connection Closed. Error: "${error}". State: ${conn.state}.`);
        showAlert("与服务器连接已断开。" + (error ? ` 原因: ${error.message}` : "")); // Use alert instead of displayError
        // Automatic reconnect is enabled, but we might want to disable UI elements here.
        connection = null; // Clear the connection variable
        reconnectInProgress = false;
        reconnectEpochResetPending = false;
        reconnectDesiredSpectateTarget = null;
        setSignalRConnection(null);
        setConnectionStatus('disconnected', '连接已断开');
    });

    conn.onreconnecting((error) => {
        if (connection !== conn) return;
        reconnectDesiredSpectateTarget = gameState.isSpectator
            ? (gameState.pendingSpectateTargetId || gameState.viewingPlayerId || null)
            : null;
        reconnectEpochResetPending = true;
        abortMirageCombatPlayback("reconnecting");
        setSignalRConnection(null);
        logPerfDebug('signalr-game', 'connection reconnecting', {
            connectionId,
            state: conn.state,
            error: error ? { message: error.message, name: error.name } : null
        });
        console.warn("MAIN: SignalR Connection Reconnecting...", error);
        showAlert("连接中断，正在尝试重新连接服务器..."); // Use alert instead of displayInfo
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
        showAlert("已重新连接到服务器。"); // Use alert instead of displayInfo
        const targetToRestore = reconnectDesiredSpectateTarget;
        reconnectDesiredSpectateTarget = null;
        gameState.viewEpoch = 0;
        gameState.pendingSpectateTargetId = null;
        forceFullStateOnNextReceive = true;
        reconnectInProgress = false;
        setSignalRConnection(conn);
        setConnectionStatus('reconnecting', '连接已恢复，正在同步...');
        void (async () => {
            try {
                const synced = await requestFullStateSync("auto-reconnect");
                if (!synced || connection !== conn) return;
                const activeMirageRoom = gameState.mirageRoomId
                    || gameState.mirage?.tournamentRoomId
                    || gameState.mirage?.TournamentRoomId
                    || null;
                if (gameState.isSpectator
                    && activeMirageRoom
                    && targetToRestore
                    && String(gameState.viewingPlayerId || '').toLowerCase() !== String(targetToRestore).toLowerCase()) {
                    await switchMirageSpectateTargetRequest(targetToRestore);
                }
            } catch (error) {
                console.error("MAIN: 自动重连同步失败。", error);
            }
        })();
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
        showAlert("连接已断开，无法响应目标选择。"); // Use alert instead of displayError
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
        showAlert(`发送目标选择失败: ${err.message}`); // Use alert instead of displayError
    }
}

// --- End SignalR Connection Logic ---

// --- Application Initialization ---

async function ensureLobbyHandshake() {
    // 0) Pending handshake set by multiplayer / mirage 8P UI
    if (pendingHandshake && pendingHandshake.kind === 'mirage-8p' && pendingHandshake.roomId) {
        if (pendingHandshake.role === 'spectator' || (pendingHandshake.playerId && pendingHandshake.token)) {
            cacheHandshake(pendingHandshake);
            return pendingHandshake;
        }
    }
    if (pendingHandshake && pendingHandshake.gameId && pendingHandshake.playerId && pendingHandshake.token) {
        cacheHandshake(pendingHandshake);
        return pendingHandshake;
    }

    // 1) URL parameters (deep link)
    const urlParams = new URLSearchParams(window.location.search);
    const urlMirageRoomId = urlParams.get("mirageRoomId");
    const urlGameId = urlParams.get("gameId");
    const urlSpectate = urlParams.get("spectate");
    const urlPlayerId = urlParams.get("playerId");
    const urlToken = urlParams.get("token");
    if (urlMirageRoomId && (urlSpectate === "1" || urlSpectate === "true")) {
        const specHs = {
            kind: "mirage-8p",
            roomId: urlMirageRoomId,
            gameId: urlMirageRoomId,
            role: "spectator",
            isMirage: true,
            mode: "Mirage"
        };
        cacheHandshake(specHs);
        return specHs;
    }
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
        if (cached?.kind === 'mirage-8p' && cached.roomId && cached.role !== 'spectator' && cached.playerId && cached.token) {
            try {
                const roomState = await apiGetJson(
                    `/api/mirage/rooms/${encodeURIComponent(cached.roomId)}?playerId=${encodeURIComponent(cached.playerId)}&token=${encodeURIComponent(cached.token)}`
                );
                if (roomState?.phase === 'Waiting') {
                    setTimeout(() => void router.navigateTo('mirage-lobby'), 0);
                    throw new Error("幻境房间仍在等待开局，正在返回准备室...");
                }
                if (roomState?.callerSeatGameId) {
                    cached.seatGameId = roomState.callerSeatGameId;
                    cached.gameId = roomState.callerSeatGameId;
                }
                if (roomState?.callerIsEliminated && cached.role === 'eliminated-spectator' && roomState.recommendedSpectatePlayerId) {
                    cached.viewingPlayerId = cached.viewingPlayerId || roomState.recommendedSpectatePlayerId;
                }
                cacheHandshake(cached);
            } catch (probeErr) {
                if (String(probeErr?.message || '').includes('等待开局')) throw probeErr;
            }
        }
        return cached;
    }

    throw new Error("请先在单人对战或多人对战开始对局。");
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
        showAlert("没有可用的对战信息，无法重连。");
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
        showAlert(`重连失败：${err?.message || '未知错误'}`);
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
            if (handshake.role === "spectator" || handshake.role === "eliminated-spectator") {
                initializeSpectator(gameId, {
                    viewingPlayerId: handshake.viewingPlayerId || null,
                    mirageRoomId: handshake.mirageRoomId || null
                });
                if (handshake.playerId) {
                    gameState.selfOriginalPlayerId = handshake.playerId;
                }
            } else {
                initializeGame(gameId, playerId, {
                    dualControl: handshake.dualControl === true,
                    hostPlayerId: handshake.hostPlayerId || playerId,
                    guestPlayerId: handshake.guestPlayerId || null,
                    mirageRoomId: handshake.mirageRoomId || null
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
            showAlert(`游戏初始化失败：${error.message || '未知错误'} 请刷新页面重试。`);
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
            showAlert(`游戏初始化失败: ${error.message || '未知错误'}`); // Use alert instead of displayError
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
let mpPollSessionId = 0;
let mpPollInFlight = false;
let resumeMultiplayerPollingFn = null;
let suppressMultiplayerLeaveOnce = false;
let mpLastCopied = { value: null, at: 0 };
const MP_COPY_STICKY_MS = 0;
const mpNotificationSoundPlayer = createLobbyNotificationSoundPlayer();

function stopMultiplayerPolling() {
    mpPollSessionId++;
    mpPollInFlight = false;
    if (mpPollTimerId) {
        clearInterval(mpPollTimerId);
        mpPollTimerId = null;
    }
}

function syncHandshakeRematchCapabilityFromRoom(handshake, room) {
    if (!handshake || !room || typeof room.canRematch !== 'boolean') return;
    if (!room.guestPlayerId && room.status !== 'Started' && room.canRematch === false) return;
    if (handshake.canRematch === room.canRematch) return;
    handshake.canRematch = room.canRematch;
    cacheHandshake(handshake);
}

function probeHandshakeRematchCapability(handshake) {
    const roomId = handshake?.gameId;
    if (!roomId || rematchCapabilityProbeGameId === roomId) return;
    rematchCapabilityProbeGameId = roomId;
    getMatch(roomId, { playerId: handshake.playerId, token: handshake.token })
        .then((room) => {
            const latest = getActiveNonAiRoomHandshake();
            if (!latest || latest.gameId !== roomId) return;
            if (room?.status === 'Closed') {
                latest.canRematch = false;
                cacheHandshake(latest);
            } else if (typeof room?.canRematch === 'boolean') {
                latest.canRematch = room.canRematch;
                cacheHandshake(latest);
            }
            syncRematchRoomButtonVisibility();
        })
        .catch(() => {
            if (rematchCapabilityProbeGameId === roomId) {
                rematchCapabilityProbeGameId = null;
            }
        });
}

function syncRematchRoomButtonVisibility() {
    const rematchBtn = document.getElementById('rematch-room-button');
    if (!rematchBtn) return;
    const handshake = getActiveNonAiRoomHandshake();
    if (
        gameState.gameOver === true &&
        handshake &&
        handshake.dualControl !== true &&
        handshake.isTakeTwo !== true &&
        typeof handshake.canRematch !== 'boolean' &&
        !isSpectatorBattle(gameState, handshake)
    ) {
        probeHandshakeRematchCapability(handshake);
    }
    const canShow = canShowRematchRoomButton(gameState, handshake);
    rematchBtn.style.display = canShow ? 'inline-flex' : 'none';
}

function formatHandshakeOutput(handshake) {
    return formatRoomHandshake(handshake, {copiedValue:mpLastCopied?.value});
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
                showAlert('复制失败，请手动选择复制。');
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

    const draftGuid = id => String(id).replace(/^(.{8})(.{4})(.{4})(.{4})(.{12})$/, '$1-$2-$3-$4-$5');
    const buildSpectateRoomMeta = (room) => {
        const parts = [];
        parts.push(room.isPublic === false ? '私密房' : '公开房');
        if (room.isMirage8P) {
            if (room.canJoin) {
                parts.push(`已入座 ${room.humanCount || 1}/${room.maxSeats || 8} 人（点击直接加入）`);
            } else {
                if (room.round) parts.push(`第 ${room.round} 轮`);
                if (room.aliveCount) parts.push(`存活 ${room.aliveCount}/${room.maxSeats || 8} 人`);
            }
            parts.push('幻境模式');
            return parts.join(' · ');
        }
        if (room.round) {
            parts.push(`第 ${room.round} 局`);
        }
        if ((room.hostWins || 0) + (room.guestWins || 0) > 0) {
            parts.push(`比分 ${room.hostWins || 0}:${room.guestWins || 0}`);
        }
        parts.push(room.hasGuest || room.useAiFallback ? '对手已就位' : (room.canJoin ? '等待对手（点击直接加入）' : '等待对手'));
        if (room.isTakeTwo) {
            const label = { rotation: '指定', unlimited: '无限', open: '开放', as2p: 'AS2P' }[room.draftEnvironment];
            parts.push(label === 'AS2P' ? 'AS2P' : `${label || getDeckEnvironmentLabel(room.requiredEnvironment) || '开放'} | 双选对战`);
            return parts.join(' · ');
        }
        if (room.isMirage) {
            parts.push('幻境模式');
            return parts.join(' · ');
        }
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
            const actionable = Boolean(room.canSpectate || room.canJoin);
            item.disabled = !actionable;
            item.title = room.canSpectate
                ? '点击填入观战房间号'
                : room.canJoin
                    ? '点击直接加入该公开房间'
                    : (room.isPublic === false ? '私密房间等待中，请通过6位邀请码加入' : '房间未开始，暂不可观战');

            const main = document.createElement('div');
            main.className = 'mp-room-main';

            const id = document.createElement('span');
            id.className = 'mp-room-id';
            id.textContent = room.draftRoomId ? draftGuid(room.draftRoomId) : room.roomId;

            const status = document.createElement('span');
            status.className = 'mp-room-status';
            status.textContent = room.canJoin ? '公开可加入' : room.statusLabel;

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
                        inputSpectateGame.value = room.draftRoomId ? draftGuid(room.draftRoomId) : room.roomId;
                        inputSpectateGame.focus();
                    }
                    setOutput(outSpectate, `已选择观战房间：${room.roomId}`);
                    closeSpectateRoomModal();
                });
            } else if (room.canJoin) {
                item.addEventListener('click', () => {
                    closeSpectateRoomModal();
                    if (inputJoinGame) inputJoinGame.value = room.roomId;
                    if (inputJoinCode) inputJoinCode.value = '';
                    void executeJoinFlow(room.roomId, '', {
                        isPublicDirectJoin: true,
                        isMirage: room.isMirage === true,
                        isMirage8P: room.isMirage8P === true
                    });
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
        if (handshake.isMirage === true) return null;
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

    const isMirageRoomPayload = (room, handshake = null) => {
        if (room?.isMirage === true || String(room?.mode || '').toLowerCase() === 'mirage') {
            return true;
        }
        return handshake?.isMirage === true || String(handshake?.mode || '').toLowerCase() === 'mirage';
    };

    const buildLobbyStatusHtml = (handshake, room = null, prefixMessage = '') => {
        const isHost = handshake.role === 'host';
        const isMirage = isMirageRoomPayload(room, handshake);
        const isPublic = room?.isPublic ?? handshake.isPublic ?? true;
        const visibilityTag = isPublic ? '公开房' : '私密房';
        const modeBadge = isMirage
            ? `<div style="color:#00e5ff;font-size:12px;margin-bottom:4px;">${visibilityTag} · 幻境模式</div>`
            : `<div style="color:#00e5ff;font-size:12px;margin-bottom:4px;">${visibilityTag}</div>`;

        let scoreHtml = '';
        if (room && ((room.hostWins || 0) + (room.guestWins || 0) + (room.draws || 0) > 0 || (room.round || 0) > 0)) {
            scoreHtml = `<div style="font-size:12px;color:#ffd54f;margin-bottom:4px;">房间战绩：房主 ${room.hostWins || 0} 胜 - ${room.guestWins || 0} 胜 访客${room.draws ? `（${room.draws} 平）` : ''} · 已进行 ${room.round || 0} 局</div>`;
        }

        let seatsHtml = '';
        if (room) {
            const hostClass = !isMirage && room.hostDeckClass ? ` [${getDeckClassLabel(room.hostDeckClass)}]` : '';
            const guestClass = !isMirage && room.guestDeckClass ? ` [${getDeckClassLabel(room.guestDeckClass)}]` : '';
            const hostStatus = room.hostReady ? '已准备' : '未准备';
            const guestStatus = room.guestPlayerId
                ? (room.guestReady ? '已准备' : '未准备')
                : '等待加入';
            seatsHtml = `<div style="font-size:12px;margin:4px 0;color:rgba(255,255,255,0.9);">房主${hostClass}：${hostStatus} ｜ 访客${guestClass}：${guestStatus}</div>`;
        }

        const myReady = room ? (isHost ? room.hostReady : room.guestReady) : true;
        const toggleReadyLabel = myReady ? '取消准备' : '准备就绪';
        const canKick = isHost && Boolean(room?.guestPlayerId) && room?.status !== 'Started';
        const actionsHtml = `<div class="mp-lobby-inline-actions" style="display:flex;gap:8px;flex-wrap:wrap;margin-top:8px;">
            <button type="button" class="mp-btn ghost" style="padding:4px 10px;font-size:12px;" data-mp-room-action="toggle-ready" data-next-ready="${!myReady}">${toggleReadyLabel}</button>
            ${canKick ? `<button type="button" class="mp-btn ghost" style="padding:4px 10px;font-size:12px;color:#ffb74d;" data-mp-room-action="kick-guest" data-guest-id="${room.guestPlayerId}">踢出访客</button>` : ''}
            <button type="button" class="mp-btn ghost" style="padding:4px 10px;font-size:12px;color:#ff6e6e;" data-mp-room-action="leave-room">${isHost ? '解散房间' : '退出房间'}</button>
        </div>`;

        return `${modeBadge}${scoreHtml}${prefixMessage ? `<div>${prefixMessage}</div>` : ''}${seatsHtml}${formatHandshakeOutput(handshake)}${actionsHtml}`;
    };

    const enterStartedRoom = async (handshake, room, outputEl) => {
        const action = resolveRematchPollStartedAction(room, rematchExitedActiveGameId);
        if (action === 'wait-reset') {
            setOutput(outputEl, buildLobbyStatusHtml(handshake, room, '正在结算上一局房间状态，请稍候...'));
            return;
        }
        if (action === 'ended-no-rematch') {
            stopMultiplayerPolling();
            await leaveCurrentPvpRoom('rematch-unsupported');
            setOutput(outputEl, '本局已结束，当前模式不支持原房间再战，请重新创建或加入房间。');
            return;
        }
        if (action !== 'enter-game') return;
        rematchExitedActiveGameId = null;
        stopMultiplayerPolling();
        suppressMultiplayerLeaveOnce = true;
        await router.navigateTo('game');
    };

    const startRoomLobbyPolling = (handshake, outputEl, intervalMs = 1300) => {
        stopMultiplayerPolling();
        const sessionId = ++mpPollSessionId;
        const isHost = handshake.role === 'host';
        const guestJoinSoundTracker = isHost
            ? createGuestJoinSoundTracker({ playGuestJoined: () => mpNotificationSoundPlayer.playGuestJoined() })
            : null;

        const pollOnce = async () => {
            if (sessionId !== mpPollSessionId || mpPollInFlight) return;
            const activeHandshake = pendingHandshake || lobbyHandshake;
            if (!activeHandshake || activeHandshake.gameId !== handshake.gameId) {
                stopMultiplayerPolling();
                return;
            }
            mpPollInFlight = true;
            try {
                const room = await getMatch(handshake.gameId, {
                    playerId: handshake.playerId,
                    token: handshake.token
                });
                if (sessionId !== mpPollSessionId) return;

                updateMultiplayerFormatFromRoom(room);
                syncHandshakeRematchCapabilityFromRoom(handshake, room);
                const status = room.status;
                const guestJoined = !!room.guestPlayerId;
                guestJoinSoundTracker?.observe(room);

                if (status === "Closed") {
                    rematchExitedActiveGameId = null;
                    stopMultiplayerPolling();
                    const reason = room.closedReason || "房间已关闭。";
                    setOutput(outputEl, `房间已解散：${reason}<br>${formatHandshakeOutput(handshake)}`, true);
                    clearCachedHandshake();
                    return;
                }

                if (!isHost && !room.guestPlayerId) {
                    rematchExitedActiveGameId = null;
                    stopMultiplayerPolling();
                    setOutput(outputEl, `已被房主移出房间或座位已失效。`, true);
                    clearCachedHandshake();
                    return;
                }

                const statusText = `房间状态：${status}${isHost ? (guestJoined ? "（对手已加入）" : "（等待对手）") : ""}`;
                setOutput(outputEl, buildLobbyStatusHtml(handshake, room, statusText));

                if (status === "Ready") {
                    rematchExitedActiveGameId = null;
                    try {
                        await startMatch(handshake.gameId, handshake.playerId, handshake.token);
                    } catch {
                        // another side may already start
                    }
                }

                if (status === "Started") {
                    await enterStartedRoom(handshake, room, outputEl);
                } else {
                    rematchExitedActiveGameId = null;
                }
            } catch (err) {
                if (sessionId !== mpPollSessionId) return;
                const message = err?.message || String(err);
                if (isRoomUnavailableError(message)) {
                    stopMultiplayerPolling();
                    setOutput(outputEl, `房间已解散或已失效。<br>${formatHandshakeOutput(handshake)}`, true);
                    clearCachedHandshake();
                    return;
                }
                setOutput(outputEl, buildLobbyStatusHtml(handshake, null, `轮询提示：${message}`), true);
            } finally {
                if (sessionId === mpPollSessionId) {
                    mpPollInFlight = false;
                }
            }
        };

        void pollOnce();
        mpPollTimerId = setInterval(pollOnce, intervalMs);
    };

    resumeMultiplayerPollingFn = () => {
        const handshake = getActiveNonAiRoomHandshake();
        if (!handshake || handshake.dualControl) return;
        const targetOutput = handshake.role === 'guest' ? outJoin : outCreate;
        if (targetOutput) {
            startRoomLobbyPolling(handshake, targetOutput, handshake.role === 'guest' ? 1200 : 1500);
        }
    };

    multiplayerPanel?.addEventListener('click', async (event) => {
        const actionBtn = event.target?.closest?.('[data-mp-room-action]');
        if (!actionBtn) return;
        event.preventDefault();
        const action = actionBtn.dataset.mpRoomAction;
        const handshake = getActiveNonAiRoomHandshake();
        if (!handshake) return;
        const targetOutput = handshake.role === 'guest' ? outJoin : outCreate;

        try {
            actionBtn.disabled = true;
            if (action === 'toggle-ready') {
                const nextReady = actionBtn.dataset.nextReady === 'true';
                if (nextReady && !handshake.isMirage) {
                    const deckId = await getSelectedMultiplayerDeckId();
                    if (deckId) {
                        await submitDeckSelection(handshake, { deckId });
                        markRoomDeckSynced(handshake, deckId);
                    }
                }
                const updatedRoom = await markReady(handshake.gameId, handshake.playerId, handshake.token, nextReady);
                const cancelHint = handshake.isMirage ? '已取消准备。' : '已取消准备，可自由更换卡组。';
                setOutput(targetOutput, buildLobbyStatusHtml(handshake, updatedRoom, nextReady ? '已准备就绪。' : cancelHint));
            } else if (action === 'kick-guest') {
                const guestId = actionBtn.dataset.guestId || null;
                const updatedRoom = await kickGuestFromMatch(handshake.gameId, handshake.playerId, handshake.token, guestId);
                setOutput(targetOutput, buildLobbyStatusHtml(handshake, updatedRoom, '已将访客移出房间，等待新对手加入。'));
            } else if (action === 'leave-room') {
                stopMultiplayerPolling();
                await leaveCurrentPvpRoom('manual-lobby-leave');
                setOutput(targetOutput, '已退出当前房间。');
            }
        } catch (err) {
            setOutput(targetOutput, buildLobbyStatusHtml(handshake, null, `操作失败：${err?.message || String(err)}`), true);
        } finally {
            actionBtn.disabled = false;
        }
    });

    const loadConfiguredMirageLobbyModule = async () => {
        const lobbyMod = await import('./mirageLobby.js');
        if (window.__svMirageLobbyHost) {
            lobbyMod.configureMirageLobbyHost(window.__svMirageLobbyHost);
        }
        return lobbyMod;
    };

    let roomEntryInProgress = false;
    const runRoomEntry = async (operation) => {
        if (roomEntryInProgress) return;
        roomEntryInProgress = true;
        const controls = [btnCreateHost, btnJoin].filter(Boolean);
        const disabledStates = controls.map(button => button.disabled);
        controls.forEach(button => { button.disabled = true; });
        stopMultiplayerPolling();
        try {
            await operation();
        } finally {
            roomEntryInProgress = false;
            controls.forEach((button, index) => { button.disabled = disabledStates[index]; });
            if (router.getCurrentRoute() === 'multiplayer') resumeMultiplayerPollingFn?.();
        }
    };

    const prepareRoomEntry = async (roomId = '', joinCode = '') => {
        const matchesTarget = (id, code) =>
            (roomId && String(id || '').replaceAll('-', '').toLowerCase() === roomId.replaceAll('-', '').toLowerCase()) ||
            (!roomId && joinCode && String(code || '').toLowerCase() === joinCode.toLowerCase());
        const draft = getActiveTakeTwoRoom();
        if (draft && !matchesTarget(draft.id, draft.code)) await leaveTakeTwoRoom();
        const handshake = getReconnectHandshake();
        if (handshake && !matchesTarget(handshake.roomId || handshake.gameId, handshake.joinCode)) {
            await leaveCurrentPvpRoom('switch-room');
        }
        return handshake && matchesTarget(handshake.roomId || handshake.gameId, handshake.joinCode) ? handshake : null;
    };

    configureTakeTwoRoomEntry(createRoom => runRoomEntry(async () => {
        await prepareRoomEntry();
        await createRoom();
    }));

    bindButton(btnCreateHost, () => runRoomEntry(async () => {
        if (!outCreate) return;
        mpNotificationSoundPlayer.unlock();
        try {
            const roomMode = btnCreateHost?.closest('.mp-card')?.dataset?.roomMode === 'mirage'
                ? 'Mirage'
                : 'Standard';
            const isMirage = roomMode === 'Mirage';
            const isPublic = document.getElementById('mp-is-public')?.checked !== false;

            if (isMirage) {
                setOutput(outCreate, '创建幻境房间中...');
                stopMultiplayerPolling();
                await prepareRoomEntry();
                const lobbyMod = await loadConfiguredMirageLobbyModule();
                await lobbyMod.createMirage8PRoomFromMultiplayer({ isPublic });
                setOutput(outCreate, '');
                return;
            }

            const deckId = await getSelectedMultiplayerDeckId();
            if (!deckId) {
                setDeckOutput('请先选择卡组。', true);
                return;
            }
            setOutput(outCreate, '创建房间中...');
            await prepareRoomEntry();
            const handshake = await createMatch(false, { isPublic, mode: roomMode });
            // Save handshake immediately so user can still retry or leave if deck submission fails
            setPendingHandshake(handshake);

            const selection = await submitDeckSelection(handshake, { deckId });
            if (!selection.ok) {
                setOutput(outCreate, buildLobbyStatusHtml(handshake, null, `房间已创建，但卡组选择失败：${selection.message}`), true);
                return;
            }
            markRoomDeckSynced(handshake, deckId);
            const readyRoom = await markReady(handshake.gameId, handshake.playerId, handshake.token, true);
            setOutput(outCreate, buildLobbyStatusHtml(handshake, readyRoom, '房间已创建，等待对手加入。'));
            startRoomLobbyPolling(handshake, outCreate, 1500);
        } catch (err) {
            setOutput(outCreate, `创建失败：${err.message}`, true);
        }
    }));

    const executeJoinFlow = (rawGameId, rawJoinCode, options = {}) => runRoomEntry(async () => {
        if (!outJoin) return;
        const gameId = String(rawGameId || '').trim();
        const joinCode = String(rawJoinCode || '').trim();
        if (!gameId && !joinCode) {
            setOutput(outJoin, '请输入 6 位邀请码（JoinCode），或在房间列表中点击公开房间加入。', true);
            return;
        }

        try {
            let targetRoomId = gameId;
            let targetKind = options.isMirage8P === true ? 'Mirage8P' : null;
            if (joinCode) {
                const resolved = await apiGetJson(`/api/mirage/rooms/resolve-code/${encodeURIComponent(joinCode)}`);
                if (!resolved.found) throw new Error('未找到该邀请码对应的房间。');
                if (gameId && resolved.roomId.replaceAll('-', '').toLowerCase() !== gameId.replaceAll('-', '').toLowerCase()) {
                    throw new Error('输入的 GameId 与邀请码不匹配。');
                }
                targetRoomId = resolved.roomId;
                targetKind = resolved.kind;
            }

            if (targetKind === 'Mirage8P' || (!targetKind && isValidGuid(targetRoomId))) {
                const mirageList = await apiGetJson('/api/mirage/rooms');
                const mirageItems = mirageList.items ?? mirageList.Items;
                const matchedMirage = mirageItems.find(
                    item => String(item.roomId ?? item.RoomId).replaceAll('-', '').toLowerCase() === targetRoomId.replaceAll('-', '').toLowerCase()
                );
                if (targetKind === 'Mirage8P' && !matchedMirage) throw new Error('幻境房间不存在或已关闭。');
                if (matchedMirage) {
                    if (!joinCode && !(matchedMirage.canJoin ?? matchedMirage.CanJoin)) throw new Error('该幻境房间当前无法加入。');
                    setOutput(outJoin, '加入幻境房间中...');
                    const lobbyMod = await loadConfiguredMirageLobbyModule();
                    await prepareRoomEntry(targetRoomId, joinCode);
                    if (joinCode) {
                        await lobbyMod.joinMirage8PByCodeFromExternal(joinCode);
                    } else {
                        await lobbyMod.joinMirage8PByRoomIdFromExternal(targetRoomId);
                    }
                    setOutput(outJoin, '');
                    return;
                }
            }

            if (targetKind === 'TakeTwo') {
                if (!await tryJoinTakeTwo(targetRoomId, joinCode, () => prepareRoomEntry(targetRoomId, joinCode))) {
                    throw new Error('双选房间不存在或已关闭。');
                }
                return;
            }

            const room = await getMatch(targetRoomId);
            if (room.status === 'Closed') throw new Error(room.closedReason || '房间已关闭。');
            const knownIsMirage = isMirageRoomPayload(room);
            let requiredFormat = null;
            if (!knownIsMirage) {
                const environment = normalizeEnvironment(
                    room.requiredEnvironment ?? room.hostDeckEnvironment ?? room.environment, ''
                );
                const rule = normalizeDeckRule(room.requiredRule ?? room.hostDeckRule ?? room.rule, '');
                if (environment && rule) requiredFormat = { environment, rule };
            }

            let selectedDeckId = null;
            if (!knownIsMirage) {
                await ensureDecksLoaded();
                selectedDeckId = await getSelectedMultiplayerDeckId();
                if (!selectedDeckId) {
                    setDeckOutput('请先在多人对战中选择卡组。', true);
                    return;
                }
                const selectedDeck = getDeckById(selectedDeckId, deckSelectionState.decks);
                if (!selectedDeck) {
                    setDeckOutput('当前已选卡组不存在，请重新选择后再加入。', true);
                    return;
                }
                if (!await validateDeckForSelection(selectedDeck)) {
                    setDeckOutput('当前选择卡组未通过校验，无法加入房间。', true);
                    return;
                }
                if (requiredFormat && !matchesDeckFormat(selectedDeck, requiredFormat)) {
                    setDeckOutput(`当前选择卡组与房主卡组不匹配（需要 ${getDeckEnvironmentLabel(requiredFormat.environment)} | ${getDeckRuleLabel(requiredFormat.rule)}），无法加入。`, true);
                    return;
                }
                await refreshSelectableDecks();
                setDeckOutput('');
            }

            setOutput(outJoin, '加入房间中...');
            const existingHandshake = await prepareRoomEntry(targetRoomId, joinCode);
            if (requiredFormat) setMultiplayerDeckFormat(requiredFormat, 'multiplayer');
            updateMultiplayerDeckSwitcher();
            const handshake = existingHandshake || await joinMatch(targetRoomId, joinCode);
            handshake.isMirage = knownIsMirage;

            // Save handshake immediately so user keeps seat ownership even if deck sync fails
            setPendingHandshake(handshake);

            if (!handshake.isMirage) {
                const selection = await submitDeckSelection(handshake, { deckId: selectedDeckId });
                if (!selection.ok) {
                    setOutput(outJoin, buildLobbyStatusHtml(handshake, null, `已进入房间，但卡组同步失败：${selection.message}`), true);
                    return;
                }
                markRoomDeckSynced(handshake, selectedDeckId);
            }
            const readyRoom = await markReady(handshake.gameId, handshake.playerId, handshake.token, true);
            try {
                await startMatch(handshake.gameId, handshake.playerId, handshake.token);
            } catch (err) {
                console.warn("Guest start skipped:", err.message);
            }
            setOutput(outJoin, buildLobbyStatusHtml(handshake, readyRoom, '加入成功，等待房间开始。'));
            startRoomLobbyPolling(handshake, outJoin, 1200);
        } catch (err) {
            setOutput(outJoin, `加入失败：${err.message}`, true);
        }
    });

    bindButton(btnJoin, async () => {
        const gameId = inputJoinGame?.value?.trim() || '';
        const joinCode = inputJoinCode?.value?.trim() || '';
        await executeJoinFlow(gameId, joinCode);
    });

    bindButton(btnGoGame, () => runRoomEntry(async () => {
        const handshake = pendingHandshake || loadCachedHandshake();
        if (!handshake) {
            showAlert('请先创建或加入房间。');
            return;
        }
        if (handshake.useAiFallback) {
            router.navigateTo('game');
            return;
        }

        // For PVP rooms, only enter game when Started (avoids hanging init while waiting)
        try {
            const room = await getMatch(handshake.gameId);
            if (router.getCurrentRoute() !== 'multiplayer' || getReconnectHandshake() !== handshake) return;
            updateMultiplayerFormatFromRoom(room);
            if (room.status === 'Started') {
                syncHandshakeRematchCapabilityFromRoom(handshake, room);
                await enterStartedRoom(handshake, room, handshake.role === 'guest' ? outJoin : outCreate);
                return;
            }
            if (!handshake.isMirage) {
                const deckId = await getSelectedMultiplayerDeckId();
                if (!deckId) {
                    showAlert('请先选择卡组。');
                    return;
                }
                const selection = await submitDeckSelection(handshake, { deckId });
                if (!selection.ok) {
                    showAlert(selection.message || "无法选择卡组。");
                    return;
                }
                markRoomDeckSynced(handshake, deckId);
            }
            if (room.status === "Ready") {
                try {
                    await startMatch(handshake.gameId, handshake.playerId, handshake.token);
                } catch (err) {
                    // Another side may already have started the room.
                }
            }
            const room2 = await getMatch(handshake.gameId);
            if (router.getCurrentRoute() !== 'multiplayer' || getReconnectHandshake() !== handshake) return;
            updateMultiplayerFormatFromRoom(room2);
            syncHandshakeRematchCapabilityFromRoom(handshake, room2);
            if (room2.status === "Closed") {
                const reason = room2.closedReason || "房间已关闭。";
                clearCachedHandshake();
                showAlert(`无法进入对战：${reason}`);
                return;
            }
            if (room2.status !== "Started") {
                showAlert('房间未开始，请等待对手加入并准备。');
                return;
            }
            await enterStartedRoom(handshake, room2, handshake.role === 'guest' ? outJoin : outCreate);
        } catch (err) {
            showAlert(`无法进入对战：${err.message}`);
        }
    }));

    bindButton(btnSpectateRooms, async () => {
        if (!outSpectate) return;

        try {
            if (btnSpectateRooms) {
                btnSpectateRooms.disabled = true;
            }
            setOutput(outSpectate, '加载房间状态中...');
            openSpectateRoomModal();

            const rooms = await listSpectatableRooms(listMatches, normalizeMatchRoomList);
            renderSpectateRoomList(rooms);

            const spectatableCount = rooms.filter(room => room.canSpectate).length;
            const joinableCount = rooms.filter(room => room.canJoin).length;
            if (spectatableCount > 0 || joinableCount > 0) {
                setOutput(outSpectate, `找到 ${spectatableCount} 个可观战房间、${joinableCount} 个可加入公开房，点击对应条目即可操作。`);
            } else if (rooms.length > 0) {
                setOutput(outSpectate, '当前仅有等待中的私密房间（请使用6位邀请码加入，开局后可观战）。', true);
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
        const gameId = draftGuid(inputSpectateGame?.value?.trim() || '');
        if (!gameId || !isValidGuid(gameId)) {
            setOutput(outSpectate, '请输入有效的 GameId（GUID）。', true);
            return;
        }

        try {
            setOutput(outSpectate, '检测房间状态中...');

            try {
                const mirageList = await apiGetJson('/api/mirage/rooms');
                const mirageItems = Array.isArray(mirageList?.items ?? mirageList?.Items)
                    ? (mirageList.items ?? mirageList.Items)
                    : [];
                const matchedMirage = mirageItems.find(
                    item => String(item?.roomId ?? item?.RoomId ?? '').toLowerCase() === gameId.toLowerCase()
                );
                if (matchedMirage) {
                    if (!Boolean(matchedMirage.canSpectate ?? matchedMirage.CanSpectate)) {
                        setOutput(outSpectate, '幻境房间尚未开始，暂不可观战。', true);
                        return;
                    }
                    stopMultiplayerPolling();
                    const lobbyMod = await loadConfiguredMirageLobbyModule();
                    setOutput(outSpectate, '开始观战中...');
                    await lobbyMod.enterSpectateMirageRoom(matchedMirage.roomId ?? matchedMirage.RoomId);
                    return;
                }
            } catch {
                // Fallback to TakeTwo / classic spectator lookup
            }

            const draft = await findTakeTwoSpectatorRoom(gameId);
            if (draft && !draft.matchRoomId) {
                if (draft.status === 'Waiting' || draft.players.length < 2) {
                    setOutput(outSpectate, '等待对手加入时无法观战。', true);
                    return;
                }
                await showTakeTwoSpectator(draft.roomId, { onMatch: (id) => {
                    inputSpectateGame.value = id;
                    btnSpectate.click();
                }});
                setOutput(outSpectate, '正在观战双选房间。');
                return;
            }
            const resolvedGameId = draft?.matchRoomId || gameId;
            const room = await getMatch(resolvedGameId);
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
            params.set('gameId', resolvedGameId);
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
        if (startDesc) {
            startDesc.textContent = isDual ? '确认双方卡组后开始' : '确认玩家与 AI 卡组后开始';
        }
        if (modeHint) {
            modeHint.textContent = isDual ? '提示：双人同机将自动加入客位并控制双方。' : '提示：单人对战需要选择玩家与 AI 卡组。';
        }
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

                await markReady(hostHandshake.gameId, hostHandshake.playerId, hostHandshake.token, true);
                await markReady(guestHandshake.gameId, guestHandshake.playerId, guestHandshake.token, true);

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
                const selection = await submitDeckSelection(handshake, { deckId: playerDeckId, aiDeckId, requireAi: true });
                if (!selection.ok) {
                    setStartOutput(`卡组选择失败：${selection.message}`, true);
                    return;
                }
                await markReady(handshake.gameId, handshake.playerId, handshake.token, true);
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

function syncTopNavHoverLock() {
    const topNav = document.querySelector('.top-nav-bar');
    if (!topNav) return;
    const hasPinnedCard = !!topNav.querySelector('.user-card-popup.active');
    document.body.classList.toggle('topnav-hover-locked', hasPinnedCard);
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
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
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

    loadAboutChangelog({
        onInteract: () => {
            if (!card.classList.contains('active')) {
                setOpen(true);
            }
        }
    });
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
    const routeBundledBgUrls = {
        'singleplayer': menuBg1Url,
        'card-editor': menuBg2Url,
        'deck-builder': menuBg3Url,
        'multiplayer': menuBg4Url,
        'dev-tools': menuBg5Url
    };
    const pinnedRoutes = new Set(['singleplayer', 'multiplayer']);

    const ensureOverlayImageLoaded = route => {
        const overlay = overlayByRoute.get(route);
        const image = overlay?.querySelector('.menu-route-image');
        const targetSrc = routeBundledBgUrls[route] || image?.dataset?.src;
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

async function initializeGamePage() {
    if (gameInitialized) return;
    gamePageSession++;
    gameInitialized = await initializeApp();
    syncRematchRoomButtonVisibility();
}

async function cleanupGamePage() {
    abortMirageCombatPlayback("leave_room");
    gamePageSession++;
    reconnectInProgress = false;
    const rematchBtn = document.getElementById('rematch-room-button');
    if (rematchBtn) rematchBtn.style.display = 'none';

    const handshake = getActiveNonAiRoomHandshake();
    const keepRoomForRematch = returningToLobbyForRematch === true && !!handshake;
    returningToLobbyForRematch = false;

    const shouldDeferLeave = !keepRoomForRematch && shouldDeferRoomLeaveForDecision(
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
        if (keepRoomForRematch || shouldDeferLeave) {
            leaveRoom = Promise.resolve();
        } else {
            leaveRoom = leaveCurrentPvpRoom(confirmedBattleExitSurrender ? 'cleanupGame-surrender' : 'cleanupGame');
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
        reconnectEpochResetPending = false;
        reconnectDesiredSpectateTarget = null;
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
    await leaveTakeTwoRoom();
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

window.__svMirageLobbyHost = {
    navigateTo: (route) => {
        if (router.getCurrentRoute() === 'multiplayer' && (route === 'mirage-lobby' || route === 'game')) {
            suppressMultiplayerLeaveOnce = true;
        }
        return router.navigateTo(route);
    },
    setMirage8PHandshake: (hs) => cacheHandshake(hs),
    getMirage8PHandshake: () => {
        const hs = getReconnectHandshake();
        return hs?.kind === 'mirage-8p' ? hs : null;
    },
    clearHandshake: () => clearCachedHandshake(),
    tryJoinClassicOrTakeTwoByCode: async (code) => {
        await router.navigateTo('multiplayer');
        const joinInput = document.getElementById('mp-join-code');
        if (joinInput) joinInput.value = code;
        document.getElementById('mp-join-btn')?.click();
    }
};

document.addEventListener('svb:mirage-stay-spectate', (event) => {
    const hs = getReconnectHandshake();
    const targetId = event?.detail?.targetPlayerId || null;
    if (hs?.kind === 'mirage-8p') {
        cacheHandshake({
            ...hs,
            role: 'eliminated-spectator',
            viewingPlayerId: targetId || hs.viewingPlayerId
        });
    }
    if (targetId) {
        void switchMirageSpectateTargetRequest(targetId);
    }
});

document.addEventListener('svb:mirage-return-lobby', () => {
    clearCachedHandshake();
    void router.navigateTo('multiplayer');
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

    const rematchBtn = document.getElementById('rematch-room-button');
    rematchBtn?.addEventListener('click', () => {
        rematchExitedActiveGameId = gameState.gameId || null;
        returningToLobbyForRematch = true;
        void router.navigateTo('multiplayer');
    });

    try {
        appendAfterRouteChangeHandler('deck-selection-context-route', async (_oldRoute, newRoute) => {
            if (newRoute === 'multiplayer') {
                resumeMultiplayerPollingFn?.();
            }
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
        setupUserInfoUI({ apiGetJson, apiPostJson, syncTopNavHoverLock });
    } catch (error) {
        console.error('MAIN: Failed to initialize user info UI:', error);
    }

    try {
        setupDailyQuestUI({ apiGetJson, apiPostJson, syncTopNavHoverLock });
    } catch (error) {
        console.error('MAIN: Failed to initialize daily quest UI:', error);
    }

    try {
        setupAboutUI();
    } catch (error) {
        console.error('MAIN: Failed to initialize about UI:', error);
    }

    try {
        setupMobileViewport();
    } catch (error) {
        console.error('MAIN: Failed to initialize mobile viewport:', error);
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

 document.addEventListener("svb:take-two-start",async event=>{stopMultiplayerPolling();rematchExitedActiveGameId=null;setPendingHandshake({...event.detail,isTakeTwo:true,canRematch:false});suppressMultiplayerLeaveOnce=true;await router.navigateTo("game");});
if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",initializeTakeTwo,{once:true});else initializeTakeTwo();
