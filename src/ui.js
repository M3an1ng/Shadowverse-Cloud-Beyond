import { miniCardBadge } from './miniCardBadge.js';
import { createCardInstance as createDiscoverPreviewCard } from './cardManager.js';
import { createRevealAnimator } from './revealAnimation.js';
import { playModeVisual } from './playModeVisual.js';
import { summarizeAttachedNumericEffects, numericEffectTitle, renderAttachedNumericEffects, effectSourceHtml } from './liveEffectSummary.js';
import { showAlert } from './siteDialog.js';
import { createModeChoiceDialog, toggleModeSelection } from './ui/modeChoiceDialog.js';
import './styles/mode-choice.css';
// src/ui.js - UI渲染与交互处理
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
//

// 导入依赖 (稍后根据需要添加)
// import { gameState } from './gameManager.js'; // 获取状态
// import { playCard, initiateAttack, selectAttackTarget, evolveCard } from './gameManager.js'; // 璋冪敤鍔ㄤ綔

// Import necessary functions and the gameState object
import {
    gameState,
    selectHandCard,
    switchSpectatorPerspective,
    playCard,
    findCardInstanceByUid,
    getCurrentPlayerId,
    getPlayerState,
    initiateAttack,
    selectAttackTarget,
    cancelSelection,
    evolveCard,
    superEvolveCard,
    requestAct,
    requestFusion,
    requestPpBoost,
    getActPlayabilityForCard,
    getPlayabilityForCard,
    // handleMulligan, // Removed import - Now uses submitMulligan
    submitMulligan, // Added import for SignalR mulligan submission
    endTurnRequest,  // Added import for SignalR end turn request
    mirageSetReadyRequest,
    mirageRefreshShopRequest,
    mirageUpgradeTierRequest,
    mirageToggleFreezeRequest,
    mirageBuyOfferRequest,
    mirageSellCardRequest,
    mirageReorderFieldRequest,
    mirageRedeemTicketRequest,
    mirageSelectLeaderRequest,
    abortMirageCombatPlayback,
    switchMirageSpectateTargetRequest,
    submitPlayerChoice,
    cancelPlayerChoice
} from './gameManager.js';

function isMirageMode(state = gameState) {
    const rawMode = state?.mode ?? state?.Mode;
    return rawMode === 'Mirage' || rawMode === 1;
}

function buildMirageTierBadgeInnerHtml(tier) {
    const safeTier = Math.max(1, Math.trunc(Number(tier) || 1));
    return `<svg class="mirage-tier-glyph" viewBox="0 0 12 12" aria-hidden="true"><path d="M6 0.7L7.45 4.55L11.3 6L7.45 7.45L6 11.3L4.55 7.45L0.7 6L4.55 4.55Z" fill="currentColor"/></svg><span class="mirage-tier-num">${safeTier}</span>`;
}

function buildMirageTavernAstrolabeSvg(tavernTier, maxTavernTier = 6) {
    const count = Math.min(8, Math.max(6, Math.trunc(Number(maxTavernTier) || 6)));
    const litCount = Math.max(1, Math.min(count, Math.trunc(Number(tavernTier) || 1)));
    const orbitR = 20.5;
    const hexPoints = [];
    const nodesSvg = [];
    for (let i = 0; i < count; i++) {
        const rad = ((-90 + (360 / count) * i) * Math.PI) / 180;
        const nx = 32 + orbitR * Math.cos(rad);
        const ny = 32 + orbitR * Math.sin(rad);
        hexPoints.push(`${nx.toFixed(2)},${ny.toFixed(2)}`);
        if (i < litCount) {
            nodesSvg.push(
                `<polygon class="astrolabe-node is-lit" points="${nx.toFixed(2)},${(ny - 3.1).toFixed(2)} ${(nx + 2.3).toFixed(2)},${ny.toFixed(2)} ${nx.toFixed(2)},${(ny + 3.1).toFixed(2)} ${(nx - 2.3).toFixed(2)},${ny.toFixed(2)}"/>`
            );
        } else {
            nodesSvg.push(
                `<circle class="astrolabe-node is-dim" cx="${nx.toFixed(2)}" cy="${ny.toFixed(2)}" r="1.55"/>`
            );
        }
    }
    return `
        <svg class="tavern-astrolabe-svg" viewBox="0 0 64 64" aria-hidden="true">
            <circle class="astrolabe-outer-ring" cx="32" cy="32" r="27.5" stroke-dasharray="1.4 3.1"/>
            <path class="astrolabe-crosshair" d="M32 2.5V6.5M32 57.5V61.5M2.5 32H6.5M57.5 32H61.5"/>
            <rect class="astrolabe-square-frame" x="17" y="17" width="30" height="30" rx="1"/>
            <polygon class="astrolabe-diamond-frame" points="32,10.5 53.5,32 32,53.5 10.5,32"/>
            <polygon class="astrolabe-hex-track" points="${hexPoints.join(' ')}"/>
            <polygon class="astrolabe-core-plate" points="32,19.2 42.8,32 32,44.8 21.2,32"/>
            <path class="astrolabe-chalice" d="M25.8 26.2L32 28.8L38.2 26.2L35.7 33.2L32 35.4L28.3 33.2Z"/>
            <path class="astrolabe-chalice-stem" d="M32 35.4V39.8M28.4 39.8H35.6"/>
            <polygon class="astrolabe-core-spark" points="32,21.8 33.3,24.3 32,26.4 30.7,24.3"/>
            ${nodesSvg.join('')}
        </svg>
    `;
}
import {
    buildDebugLogLine,
    createHandDragPerfSession,
    recordHandDragPerfFrame,
    summarizeHandDragPerfSession
} from './debugDiagnostics.js';
import { confirmDialog } from './siteDialog.js';
import { shouldAutoConfirmChoice } from './choiceSettingsPolicy.js';
import { readOperationSettings } from './operationSettings.js';
import { renderDescriptionHtml } from './descriptionParser.js';
import { buildPreviewSlateHtml } from './previewSlate.js';
import { showPreviewDescriptionTooltip, hidePreviewDescriptionTooltip } from './previewDescriptionTooltip.js';
import { showCardDetailModal } from './components/CardDetailMockup.js';
import { hydrateAlternatePlayReferences, hydrateDescriptionReferences } from './previewDescriptionReferences.js';
import { ensureEnumLocalizations, getCachedEnumLabel } from './enumLocalizationCache.js';
import {
    renderGameLog,
    replaceInMatchChatHistory,
    replaceInMatchCardHistory,
    appendInMatchChatMessage,
    clearInMatchChatUiState,
    appendInMatchUsedCard,
    appendInMatchDestroyedCard,
    clearInMatchUsedCardUiState,
    findLogCardInstanceByUid,
    buildMatchHistoryPreviewCard
} from './ui/logPanel.js';
import { createCardAnimationSystem, reconcileCardElementInPlace } from './ui/cardAnimationSystem.js';
import { createBattleMotionController, createCleanCardClone, toStageRect } from './ui/battleCardMotion.js';
import { createEvolutionDrag } from './ui/evolutionDrag.js';
import { isPresentationFastForward } from './revealPresentationGate.js';
import { buildCounterSummaryEntries, PLAYER_SYSTEM_COUNTER_KEYS } from './counterSummary.js';
import { getUserSession } from './userSession.js';
import { preventNativeGameDrag } from './nativeGameDragGuard.js';
import {
    applyCardArtBackground as applyArtBackground,
    deferCardArtQueue,
    preloadCardArt as preloadArt,
    resolveArtUrl
} from './cardArtCache.js';
import badgeWardUrl from './badge/Ward.png';
import badgeBaneUrl from './badge/Bane.png';
import badgeDrainUrl from './badge/Drain.png';
import badgeAmbushUrl from './badge/Ambush.png';
import badgePressureUrl from './badge/Pressure.png';
import badgeAuraUrl from './badge/Aura.png';
import badgeShieldUrl from './badge/Shield.png';
import badgeAbilityResistUrl from './badge/AbilityResist.png';
import badgeShackleUrl from './badge/Shackle.png';
import classNeutralIconUrl from './icon/svg/class_neutral.svg';
import classForestcraftIconUrl from './icon/svg/class_elf.svg';
import classSwordcraftIconUrl from './icon/svg/class_royal.svg';
import classRunecraftIconUrl from './icon/svg/class_witch.svg';
import classDragoncraftIconUrl from './icon/svg/class_dragon.svg';
import classAbysscraftIconUrl from './icon/svg/class_nightmare.svg';
import classHavencraftIconUrl from './icon/svg/class_bishop.svg';
import classPortalcraftIconUrl from './icon/svg/class_nemesis.svg';
// getCardDefinition is obsolete. Card instances now contain all necessary data.
// import { endTurn } from './turnManager.js'; // Removed import - Now uses endTurnRequest

export { renderGame, showDamageNumber, showHealNumber, updatePpDisplay, updateEpDisplay, updateSepDisplay, updatePpBoostDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateBloodPoolDisplay, updateComboDisplay, updateRallyDisplay, updateHealthDisplay, updateUnitCombatStats, updateHandDisplay, refreshHandPlayabilityVisuals, addUnitToField, updateUnitDisplay, removeUnitFromField, updateFieldDisplay, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus, clearSelectionHighlights, resetMulliganSelection, refreshBattleCardPreviewIfCurrent, renderHighlights, updateEmblemZone, updateTurnTimerDisplay, setConnectionStatus, setOpponentConnectionStatus, refreshCardInstanceUi, refreshCardActionControls, renderGameLog, replaceInMatchChatHistory, replaceInMatchCardHistory, appendInMatchChatMessage, clearInMatchChatUiState, appendInMatchUsedCard, appendInMatchDestroyedCard, clearInMatchUsedCardUiState, getActionButtonsHtml, resolveRenderableTargetElement, prepareCardMoveVisual, commitCardMoveVisual, notifyCardPlayed, playAttackLunge, playUnitEvolutionEffect, notifyShieldRemoved, cancelAllBattleMotions };

const CLASS_ICON_URL_MAP = Object.freeze({
    Neutral: classNeutralIconUrl,
    Forestcraft: classForestcraftIconUrl,
    Swordcraft: classSwordcraftIconUrl,
    Runecraft: classRunecraftIconUrl,
    Dragoncraft: classDragoncraftIconUrl,
    Abysscraft: classAbysscraftIconUrl,
    Havencraft: classHavencraftIconUrl,
    Portalcraft: classPortalcraftIconUrl
});

let mulliganSelectedUids = new Set();
const EMBLEM_SLOT_COUNT = 5;
const CARD_MOVE_EPSILON = 0.5;
const CARD_MOVE_OPACITY = '0.82';
const CARD_ENTER_TRANSITION_MS = 220;
const CARD_MOVE_TRANSITION_MS = 220;
const CARD_EXIT_TRANSITION_MS = 190;
const CARD_ENTER_STAGGER_MS = 45;
const CARD_MOVE_STAGGER_MS = 0;
const CARD_EXIT_STAGGER_MS = 0;
const CARD_STAGGER_MAX_MS = 420;
const CARD_ENTER_AFTER_EXIT_DELAY_MS = 0;
const ATTACK_DRAG_MIN_DISTANCE_PX = 10;
const ATTACK_DRAG_CLICK_SUPPRESS_MS = 260;
const ATTACK_DRAG_LINE_ID = 'attack-drag-line';
const ATTACK_DRAG_LEADER_CONFIRM_MS = 140;
const HP_GHOST_SETTLE_DELAY_MS = 180;
const HAND_DRAG_MIN_DISTANCE_PX = 10;
const HAND_DRAG_CLICK_SUPPRESS_MS = 260;
const HAND_DRAG_DROP_LANE_ID = 'hand-play-drop-lane';
const HAND_DRAG_SNAP_BACK_MS = 190;
const TRANSIENT_VISUAL_ROOT_ID = 'game-transient-visual-root';
const TRANSIENT_VISUAL_CLEANUP_SELECTOR = '.card-fade-enter-overlay, .card-fade-exit-overlay, .fusion-float-card, .damage-number, .heal-number, .hand-drag-ghost, .hand-play-drop-lane, .bui-motion-carrier, .bui-exit-clip-shell, .bui-impact-burst, .bui-float-num, .bui-evo-burst, .bui-spell-arcane-burst, #fusion-burst-root, #reveal-overlay-root';
const transientVisualTimeoutIds = new Set();
const hpGhostSettleTimers = new Map();
let suppressGameClickUntil = 0;
let evolutionActionPending = false;
const evolutionDrag = createEvolutionDrag({
    getAvailability: getEvolutionDragAvailability,
    onDrop: (cardUid, mode) => void requestEvolutionAction(cardUid, mode),
    suppressClick: () => { suppressGameClickUntil = Date.now() + ATTACK_DRAG_CLICK_SUPPRESS_MS; }
});
const attackDragState = {
    pointerId: null,
    isPending: false,
    isActive: false,
    attackerUid: null,
    sourceElement: null,
    sourceCenterX: 0,
    sourceCenterY: 0,
    startX: 0,
    startY: 0,
    currentX: 0,
    currentY: 0,
    hoverTargetUid: null,
    hoverElement: null,
    hoverKind: null,
    lineElement: null
};
const handDragState = {
    pointerId: null,
    isPending: false,
    isActive: false,
    cardUid: null,
    sourceElement: null,
    ghostElement: null,
    dropLaneElement: null,
    startX: 0,
    startY: 0,
    currentX: 0,
    currentY: 0,
    pointerOffsetX: 0,
    pointerOffsetY: 0,
    ghostWidth: 0,
    ghostHeight: 0,
    hoverTargetUid: null,
    hoverElement: null,
    hoverKind: null,
    dragFramePending: false,
    renderedGhostX: 0,
    renderedGhostY: 0
};
const handPlayCommitState = {
    cardUid: null,
    ghostElement: null
};
const PRESERVED_CARD_STATE_CLASSES = Object.freeze([
    'selected-attacker',
    'targetable',
    'targetable-restricted',
    'targeting-spell',
    'targetable-spell',
    'choice-selected',
    'playable',
    'requires-target',
    'no-valid-targets',
    'can-play-without-target',
    'mulligan-selectable',
    'selected-for-mulligan'
]);

function normalizePlayerId(value) {
    return String(value ?? '').trim().toLowerCase();
}

function isSamePlayerId(left, right) {
    const normalizedLeft = normalizePlayerId(left);
    const normalizedRight = normalizePlayerId(right);
    if (!normalizedLeft || !normalizedRight) return false;
    return normalizedLeft === normalizedRight;
}

function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

function getUiNowMs() {
    if (typeof performance !== 'undefined' && typeof performance.now === 'function') {
        return performance.now();
    }
    return Date.now();
}

function getUiPerfDebugState() {
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
                sampleCount: 0,
                lastPointerMoveAt: 0,
                pointerMoveCount: 0,
                metrics: null
            }
        };
    }

    return window.__svPerfDebug;
}

function logUiPerfDebug(scope, message, details = null) {
    const debugState = getUiPerfDebugState();
    if (!debugState.enabled) return;
    console.info(buildDebugLogLine(scope, message, details));
}

function runTrackedVisualTimeout(callback, delayMs) {
    const timeoutId = window.setTimeout(() => {
        transientVisualTimeoutIds.delete(timeoutId);
        callback();
    }, delayMs);
    transientVisualTimeoutIds.add(timeoutId);
    return timeoutId;
}

function clearTrackedVisualTimeouts() {
    transientVisualTimeoutIds.forEach(timeoutId => window.clearTimeout(timeoutId));
    transientVisualTimeoutIds.clear();
}

function ensureTransientVisualRoot() {
    const container = gameContainerEl ?? document.getElementById('game-container');
    if (!container || !isGamePageActive()) return null;

    let root = container.querySelector(`#${TRANSIENT_VISUAL_ROOT_ID}`);
    if (root) {
        return root;
    }

    root = document.createElement('div');
    root.id = TRANSIENT_VISUAL_ROOT_ID;
    root.style.position = 'absolute';
    root.style.inset = '0';
    root.style.pointerEvents = 'none';
    root.style.overflow = 'visible';
    root.style.zIndex = '9600';
    container.appendChild(root);
    return root;
}

function removeTransientVisualArtifacts() {
    clearCommittedHandPlayVisual();
    document.querySelectorAll(TRANSIENT_VISUAL_CLEANUP_SELECTOR).forEach(node => node.remove());
    document.getElementById(TRANSIENT_VISUAL_ROOT_ID)?.remove();
}

function removeStrayBattleCardArtifacts() {
    const container = gameContainerEl ?? document.getElementById('game-container');
    if (container) {
        // Safety net: transient card clones should never be direct children of game container.
        container.querySelectorAll(':scope > .card, :scope > .card-back').forEach(node => node.remove());
    }

    // Legacy/edge-case cleanup: defensive removal for any detached card overlays.
    document.querySelectorAll('body > .card-fade-exit-overlay, body > .fusion-float-card').forEach(node => node.remove());
}

function clearAttackDragHover() {
    if (attackDragState.hoverElement) {
        attackDragState.hoverElement.classList.remove('attack-drag-hover', 'attack-drag-invalid');
    }
    attackDragState.hoverElement = null;
    attackDragState.hoverTargetUid = null;
    attackDragState.hoverKind = null;
}

function ensureAttackDragLineElement() {
    let svgEl = document.getElementById(ATTACK_DRAG_LINE_ID);
    if (svgEl) return svgEl;
    const ns = 'http://www.w3.org/2000/svg';
    svgEl = document.createElementNS(ns, 'svg');
    svgEl.setAttribute('id', ATTACK_DRAG_LINE_ID);
    svgEl.setAttribute('class', 'attack-drag-svg');
    svgEl.setAttribute('aria-hidden', 'true');

    const originDot = document.createElementNS(ns, 'circle');
    originDot.setAttribute('class', 'attack-drag-origin-dot');
    originDot.setAttribute('r', '3.5');

    const line = document.createElementNS(ns, 'line');
    line.setAttribute('class', 'attack-drag-vector');

    const arrow = document.createElementNS(ns, 'polygon');
    arrow.setAttribute('class', 'attack-drag-arrowhead');

    svgEl.appendChild(originDot);
    svgEl.appendChild(line);
    svgEl.appendChild(arrow);
    document.body.appendChild(svgEl);
    return svgEl;
}

function removeAttackDragLineElement() {
    const lineEl = attackDragState.lineElement ?? document.getElementById(ATTACK_DRAG_LINE_ID);
    lineEl?.remove();
    attackDragState.lineElement = null;
}

function resetAttackDragState() {
    clearAttackDragHover();
    removeAttackDragLineElement();
    document.body.classList.remove('attack-dragging');
    attackDragState.pointerId = null;
    attackDragState.isPending = false;
    attackDragState.isActive = false;
    attackDragState.attackerUid = null;
    attackDragState.sourceElement = null;
    attackDragState.sourceCenterX = 0;
    attackDragState.sourceCenterY = 0;
    attackDragState.startX = 0;
    attackDragState.startY = 0;
    attackDragState.currentX = 0;
    attackDragState.currentY = 0;
}

function clearHandDragHover() {
    if (handDragState.hoverElement) {
        handDragState.hoverElement.classList.remove('hand-drag-target-valid', 'hand-drag-target-invalid', 'targetable-spell', 'targetable-restricted');
    }
    handDragState.hoverElement = null;
    handDragState.hoverTargetUid = null;
    handDragState.hoverKind = null;

    if (handDragState.dropLaneElement) {
        handDragState.dropLaneElement.classList.remove('is-valid', 'is-invalid');
    }
}

function removeHandDragGhostElement() {
    handDragState.ghostElement?.remove();
    handDragState.ghostElement = null;
}

function ensureHandDragDropLaneElement() {
    const container = gameContainerEl ?? document.getElementById('game-container');
    if (!container || !isGamePageActive()) return null;

    let laneEl = handDragState.dropLaneElement ?? container.querySelector(`#${HAND_DRAG_DROP_LANE_ID}`);
    if (laneEl) {
        handDragState.dropLaneElement = laneEl;
        return laneEl;
    }

    laneEl = document.createElement('div');
    laneEl.id = HAND_DRAG_DROP_LANE_ID;
    laneEl.className = 'hand-play-drop-lane';
    container.appendChild(laneEl);
    handDragState.dropLaneElement = laneEl;
    return laneEl;
}

function removeHandDragDropLaneElement() {
    handDragState.dropLaneElement?.remove();
    handDragState.dropLaneElement = null;
}

function getCommittedHandSourceElement(cardUid = handPlayCommitState.cardUid) {
    if (!cardUid || !player1HandEl) return null;
    return player1HandEl.querySelector(`.hand-card[data-uid="${String(cardUid)}"]`);
}

function clearCommittedHandSourceClass(cardUid = handPlayCommitState.cardUid) {
    getCommittedHandSourceElement(cardUid)?.classList.remove('hand-play-committed-source');
}

function clearCommittedHandPlayVisual({ removeGhost = true, clearSource = true } = {}) {
    if (clearSource) {
        clearCommittedHandSourceClass();
    }
    if (removeGhost) {
        handPlayCommitState.ghostElement?.remove();
    }
    handPlayCommitState.cardUid = null;
    handPlayCommitState.ghostElement = null;
}

function startCommittedHandPlayVisual(cardUid, ghostElement) {
    if (!cardUid || !ghostElement) return;

    clearCommittedHandPlayVisual();
    handPlayCommitState.cardUid = String(cardUid);
    handPlayCommitState.ghostElement = ghostElement;
    ghostElement.classList.remove('is-invalid', 'is-snapping-back');
    ghostElement.classList.add('is-committed');
    getCommittedHandSourceElement(cardUid)?.classList.add('hand-play-committed-source');
}

function animateCommittedHandPlayBackToSource(cardUid = handPlayCommitState.cardUid) {
    const committedUid = String(cardUid ?? handPlayCommitState.cardUid ?? '');
    if (!committedUid || committedUid !== String(handPlayCommitState.cardUid ?? '')) {
        return false;
    }

    const ghostElement = handPlayCommitState.ghostElement;
    const sourceElement = getCommittedHandSourceElement(committedUid);
    if (!ghostElement || !sourceElement) {
        return false;
    }

    ghostElement.classList.remove('is-committed', 'is-invalid');
    animateHandDragGhostToSource(ghostElement, sourceElement);
    handPlayCommitState.cardUid = null;
    handPlayCommitState.ghostElement = null;
    runTrackedVisualTimeout(() => {
        clearCommittedHandSourceClass(committedUid);
    }, HAND_DRAG_SNAP_BACK_MS + 40);
    return true;
}

export function cancelCommittedHandPlayVisual(cardUid = null, { animateBack = true } = {}) {
    const targetCardUid = cardUid ?? handPlayCommitState.cardUid ?? null;
    if (targetCardUid && String(targetCardUid) !== String(handPlayCommitState.cardUid ?? '')) {
        return;
    }

    if (animateBack && animateCommittedHandPlayBackToSource(targetCardUid)) {
        return;
    }

    clearCommittedHandPlayVisual();
}

function syncCommittedHandPlayVisual(handCards = []) {
    if (!handPlayCommitState.cardUid) return;

    const committedUid = String(handPlayCommitState.cardUid);
    const stillInHand = Array.isArray(handCards)
        && handCards.some(card => String(getCardUid(card) ?? '') === committedUid);

    if (!stillInHand) {
        clearCommittedHandPlayVisual();
        return;
    }

    getCommittedHandSourceElement(committedUid)?.classList.add('hand-play-committed-source');
}

function resetHandDragState({ keepGhost = false, preserveSourceClass = false } = {}) {
    clearHandDragHover();
    document.body.classList.remove('hand-card-dragging');
    if (!preserveSourceClass) {
        handDragState.sourceElement?.classList.remove('hand-drag-origin');
    }
    if (!keepGhost) {
        removeHandDragGhostElement();
    }
    removeHandDragDropLaneElement();
    handDragState.pointerId = null;
    handDragState.isPending = false;
    handDragState.isActive = false;
    handDragState.cardUid = null;
    handDragState.sourceElement = null;
    handDragState.startX = 0;
    handDragState.startY = 0;
    handDragState.currentX = 0;
    handDragState.currentY = 0;
    handDragState.pointerOffsetX = 0;
    handDragState.pointerOffsetY = 0;
    handDragState.ghostWidth = 0;
    handDragState.ghostHeight = 0;
    handDragState.dragFramePending = false;
    handDragState.renderedGhostX = 0;
    handDragState.renderedGhostY = 0;
    refreshSelfPpPreview();
}

function resolveAvatarLeaderUid(avatarElement) {
    if (!avatarElement?.id) return null;
    const localPlayerId = gameState.localPlayerId ?? uiCurrentPlayerId;
    const opponentPlayerId = gameState.players?.find(p => p?.id !== localPlayerId)?.id ?? null;
    const ownerPlayerId = avatarElement.id === 'player2-avatar' ? opponentPlayerId : localPlayerId;
    const ownerPlayer = ownerPlayerId ? getPlayerState(ownerPlayerId) : null;
    return ownerPlayer?.leaderCard?.uid ?? ownerPlayer?.leaderCard?.InstanceId ?? null;
}

function resolveOpponentLeaderUid() {
    const localPlayerId = gameState.localPlayerId ?? uiCurrentPlayerId;
    const opponentPlayer = gameState.players?.find(player => !isSamePlayerId(player?.id, localPlayerId)) ?? null;
    return opponentPlayer?.leaderCard?.uid ?? opponentPlayer?.leaderCard?.InstanceId ?? null;
}

function resolveOpponentLeaderAvatarElement() {
    const opponentLeaderUid = resolveOpponentLeaderUid();
    if (opponentLeaderUid) {
        const avatarByUid = resolveLeaderAvatarElementByUid(opponentLeaderUid);
        if (avatarByUid) return avatarByUid;
    }
    return document.getElementById('player2-avatar');
}

function isAttackDragLeaderTargetable(avatarElement) {
    return avatarElement?.classList?.contains('targetable') === true;
}

function resolveLeaderAvatarElementByUid(targetUid) {
    if (!targetUid) return null;

    const targetKey = String(targetUid);
    const localPlayerId = gameState.localPlayerId ?? uiCurrentPlayerId;
    const localPlayer = localPlayerId ? getPlayerState(localPlayerId) : null;
    const opponentPlayer = gameState.players?.find(p => p?.id !== localPlayerId) ?? null;

    const player1LeaderUid = localPlayer?.leaderCard?.uid ?? localPlayer?.leaderCard?.InstanceId ?? null;
    if (player1LeaderUid && String(player1LeaderUid) === targetKey) {
        return document.getElementById('player1-avatar');
    }

    const player2LeaderUid = opponentPlayer?.leaderCard?.uid ?? opponentPlayer?.leaderCard?.InstanceId ?? null;
    if (player2LeaderUid && String(player2LeaderUid) === targetKey) {
        return document.getElementById('player2-avatar');
    }

    return null;
}

function resolveRenderableTargetElement(targetUid) {
    if (!targetUid) return null;

    const targetKey = String(targetUid);
    const renderedCard = document.querySelector(`.card[data-uid="${targetKey}"], .emblem-slot[data-uid="${targetKey}"]`);
    if (renderedCard) {
        return renderedCard;
    }

    return resolveLeaderAvatarElementByUid(targetUid);
}

function getElementsFromPointSafe(clientX, clientY) {
    if (typeof document.elementsFromPoint === 'function') {
        const elements = document.elementsFromPoint(clientX, clientY);
        if (Array.isArray(elements) && elements.length > 0) {
            return elements;
        }
    }

    const single = typeof document.elementFromPoint === 'function'
        ? document.elementFromPoint(clientX, clientY)
        : null;
    return single ? [single] : [];
}

function getHandDragCastAreaElement(cardInstance) {
    if (equalsIgnoreCase(cardInstance?.type, 'Follower') || equalsIgnoreCase(cardInstance?.type, 'Amulet')) {
        return player1FieldEl ?? document.getElementById('player1-field') ?? document.getElementById('battlefield');
    }
    return document.getElementById('battlefield') ?? player1FieldEl ?? document.getElementById('player1-field');
}

function isPointInsideRect(clientX, clientY, rect) {
    if (!rect) return false;
    return clientX >= rect.left
        && clientX <= rect.right
        && clientY >= rect.top
        && clientY <= rect.bottom;
}

function isHandDragStartAllowed(cardInstance) {
    if (!cardInstance) return false;
    if (isAwaitingPlayerChoice || isConnectionLocked || gameState.isSpectator) return false;

    const localPlayerId = gameState.localPlayerId ?? uiCurrentPlayerId;
    if (!localPlayerId || gameState.currentPlayerId !== localPlayerId) return false;
    if (gameState.phase !== 'Main') return false;

    const ownerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId;
    if (ownerId !== localPlayerId) return false;
    if (!equalsIgnoreCase(cardInstance.zone, 'Hand')) return false;

    const useActionState = getUseActionState(cardInstance, 'hand');
    if (!useActionState.showUse) return false;
    if (useActionState.usePlayable === false) return false;
    return true;
}

function buildHandDragGhost(sourceElement, sourceRect = null) {
    if (!sourceElement) return null;
    const transientRoot = ensureTransientVisualRoot();
    if (!transientRoot) return null;

    const ghostEl = sourceElement.cloneNode(true);
    const rect = sourceRect ?? sourceElement.getBoundingClientRect();
    const baseW = sourceElement.offsetWidth || Math.round(rect.width) || 102;
    const baseH = sourceElement.offsetHeight || Math.round(rect.height) || 140;
    const initialX = (rect.left + rect.width / 2) - (baseW / 2);
    const initialY = (rect.top + rect.height / 2) - (baseH / 2);
    ghostEl.querySelectorAll?.('[id]')?.forEach?.(node => node.removeAttribute('id'));
    ghostEl.classList.remove(
        'hand-card-floated',
        'hand-drag-origin',
        'playable',
        'requires-target',
        'no-valid-targets',
        'can-play-without-target',
        'targetable-spell',
        'targetable-restricted',
        'choice-hand-hover'
    );
    ghostEl.classList.add('hand-drag-ghost');
    if (prefersReducedMotion()) {
        ghostEl.classList.add('is-reduced-motion');
    }
    ghostEl.style.position = 'fixed';
    ghostEl.style.left = '0';
    ghostEl.style.top = '0';
    ghostEl.style.margin = '0';
    ghostEl.style.pointerEvents = 'none';
    ghostEl.style.width = `${baseW}px`;
    ghostEl.style.height = `${baseH}px`;
    ghostEl.style.setProperty('--fan-offset', '0');
    ghostEl.style.setProperty('--fan-abs', '0');
    ghostEl.style.transform = `translate3d(${initialX}px, ${initialY}px, 0) rotate(-2deg) scale(1.04)`;
    transientRoot.appendChild(ghostEl);
    handDragState.renderedGhostX = initialX;
    handDragState.renderedGhostY = initialY;
    return ghostEl;
}

function updateHandDragDropLaneLayout(cardInstance) {
    const laneEl = ensureHandDragDropLaneElement();
    const castAreaEl = getHandDragCastAreaElement(cardInstance);
    if (!laneEl || !castAreaEl) return;

    const rect = castAreaEl.getBoundingClientRect();
    laneEl.style.left = `${rect.left}px`;
    laneEl.style.top = `${rect.top}px`;
    laneEl.style.width = `${rect.width}px`;
    laneEl.style.height = `${rect.height}px`;
}

function flushHandDragGhostPosition() {
    handDragState.dragFramePending = false;
    if (!handDragState.isActive || !handDragState.ghostElement) return;

    const translateX = handDragState.currentX - handDragState.pointerOffsetX;
    const translateY = handDragState.currentY - handDragState.pointerOffsetY;
    handDragState.ghostElement.style.transform = `translate3d(${translateX}px, ${translateY}px, 0) rotate(-2deg) scale(1.04)`;
    handDragState.renderedGhostX = translateX;
    handDragState.renderedGhostY = translateY;
}

function scheduleHandDragGhostUpdate() {
    if (handDragState.dragFramePending) return;
    handDragState.dragFramePending = true;
    const perfDebug = getUiPerfDebugState();
    if (perfDebug.handDrag) {
        perfDebug.handDrag.pendingRafAt = getUiNowMs();
    }

    if (typeof window === 'undefined' || typeof window.requestAnimationFrame !== 'function') {
        flushHandDragGhostPosition();
        return;
    }

    window.requestAnimationFrame(() => {
        const sampleState = perfDebug.handDrag;
        if (sampleState) {
            const nowMs = getUiNowMs();
            const rafDelayMs = nowMs - Number(sampleState.pendingRafAt || 0);
            const targetX = handDragState.currentX - handDragState.pointerOffsetX;
            const targetY = handDragState.currentY - handDragState.pointerOffsetY;
            const lagPx = Math.hypot(
                targetX - Number(handDragState.renderedGhostX || 0),
                targetY - Number(handDragState.renderedGhostY || 0)
            );
            const pointerMoveAgeMs = sampleState.lastPointerMoveAt > 0
                ? nowMs - Number(sampleState.lastPointerMoveAt)
                : 0;
            sampleState.metrics = recordHandDragPerfFrame(sampleState.metrics, {
                rafDelayMs,
                lagPx,
                pointerMoveAgeMs
            });
            const frameCount = sampleState.metrics?.frameCount ?? 0;
            const shouldLogSample = rafDelayMs >= 20
                || lagPx >= 24
                || frameCount <= 5
                || frameCount % 12 === 0;
            if (shouldLogSample) {
                logUiPerfDebug('hand-drag', 'ghost update frame', {
                    sessionId: sampleState.sessionId,
                    rafDelayMs: Number(rafDelayMs.toFixed(1)),
                    lagPx: Number(lagPx.toFixed(1)),
                    pointerMoveAgeMs: Number(pointerMoveAgeMs.toFixed(1)),
                    frameCount,
                    menuStarrySkyAnimating: perfDebug.menuStarrySky?.isAnimating === true,
                    menuRoute: perfDebug.menuStarrySky?.route || '',
                    bodyDragging: document.body?.classList?.contains('hand-card-dragging') === true
                });
            }
        }
        flushHandDragGhostPosition();
    });
}

function resolveHandDragDropTarget(clientX, clientY) {
    const cardUid = handDragState.cardUid;
    if (!cardUid) {
        return { kind: 'invalid', uid: null, element: null, isValid: false };
    }

    const cardInstance = findCardInstanceByUid(cardUid);
    if (!cardInstance) {
        return { kind: 'invalid', uid: null, element: null, isValid: false };
    }

    const localPlayerId = gameState.localPlayerId ?? uiCurrentPlayerId;
    const playabilityInfo = getPlayabilityForCard(cardUid, localPlayerId) ?? null;
    if (playabilityInfo?.isPlayable === false) {
        return { kind: 'invalid', uid: null, element: null, isValid: false };
    }

    const validTargetIds = new Set((playabilityInfo?.validTargetIds || []).map(value => String(value)));
    const requiresTargetSelection = playabilityInfo?.requiresTargetSelection === true;
    const hitElements = getElementsFromPointSafe(clientX, clientY);

    let hoveredTargetElement = null;
    let hoveredTargetUid = null;
    for (const element of hitElements) {
        const targetCard = element?.closest?.('.field-card[data-uid], .emblem-slot[data-uid]');
        if (targetCard?.dataset?.uid) {
            hoveredTargetElement = targetCard;
            hoveredTargetUid = String(targetCard.dataset.uid);
            break;
        }

        const leaderHudEl = element?.closest?.('#player1-info, #player2-info');
        if (leaderHudEl && !element?.closest?.('button')) {
            const avatarId = leaderHudEl.id === 'player2-info' ? 'player2-avatar' : 'player1-avatar';
            const avatarEl = document.getElementById(avatarId);
            const avatarUid = resolveAvatarLeaderUid(avatarEl);
            if (avatarUid && avatarEl) {
                hoveredTargetElement = avatarEl;
                hoveredTargetUid = String(avatarUid);
                break;
            }
        }
    }

    if (requiresTargetSelection && hoveredTargetUid) {
        if (validTargetIds.has(hoveredTargetUid)) {
            return { kind: 'target', uid: hoveredTargetUid, element: hoveredTargetElement, isValid: true };
        }
        return { kind: 'target-invalid', uid: hoveredTargetUid, element: hoveredTargetElement, isValid: false };
    }

    const castAreaEl = getHandDragCastAreaElement(cardInstance);
    const castAreaRect = castAreaEl?.getBoundingClientRect?.() ?? null;
    if (castAreaRect && isPointInsideRect(clientX, clientY, castAreaRect)) {
        return { kind: 'play-area', uid: null, element: castAreaEl, isValid: true };
    }

    return { kind: 'invalid', uid: hoveredTargetUid, element: hoveredTargetElement, isValid: false };
}

function updateHandDragHover(targetInfo) {
    const nextKind = targetInfo?.kind ?? null;
    const nextUid = targetInfo?.uid ? String(targetInfo.uid) : null;
    const nextElement = targetInfo?.element ?? null;

    if (
        handDragState.hoverKind === nextKind
        && handDragState.hoverTargetUid === nextUid
        && handDragState.hoverElement === nextElement
    ) {
        return;
    }

    clearHandDragHover();
    if (handDragState.ghostElement) {
        handDragState.ghostElement.classList.toggle('is-invalid', targetInfo?.isValid === false);
    }

    if (targetInfo?.kind === 'play-area' && handDragState.dropLaneElement) {
        handDragState.dropLaneElement.classList.add('is-valid');
    } else if (handDragState.dropLaneElement) {
        handDragState.dropLaneElement.classList.add('is-invalid');
    }

    if (nextElement && nextKind === 'target') {
        nextElement.classList.add('hand-drag-target-valid', 'targetable-spell');
        handDragState.hoverElement = nextElement;
        handDragState.hoverTargetUid = nextUid;
        handDragState.hoverKind = nextKind;
        return;
    }

    if (nextElement && nextKind === 'target-invalid') {
        nextElement.classList.add('hand-drag-target-invalid', 'targetable-restricted');
        handDragState.hoverElement = nextElement;
        handDragState.hoverTargetUid = nextUid;
        handDragState.hoverKind = nextKind;
        return;
    }

    handDragState.hoverKind = nextKind;
    handDragState.hoverTargetUid = nextUid;
}

function activateHandDragIfNeeded() {
    if (!handDragState.isPending || handDragState.isActive) return;

    const cardUid = handDragState.cardUid;
    if (!cardUid) return;

    const sourceElement = handDragState.sourceElement ?? gameContainerEl?.querySelector(`.hand-card[data-uid="${cardUid}"]`);
    if (!sourceElement) {
        resetHandDragState();
        return;
    }

    const sourceRect = sourceElement.getBoundingClientRect();
    const ghostEl = buildHandDragGhost(sourceElement, sourceRect);
    if (!ghostEl) {
        resetHandDragState();
        return;
    }

    const baseW = sourceElement.offsetWidth || Math.round(sourceRect.width) || 102;
    const baseH = sourceElement.offsetHeight || Math.round(sourceRect.height) || 140;
    const ratioX = sourceRect.width > 0
        ? Math.max(0.1, Math.min(0.9, (handDragState.startX - sourceRect.left) / sourceRect.width))
        : 0.5;
    const ratioY = sourceRect.height > 0
        ? Math.max(0.1, Math.min(0.9, (handDragState.startY - sourceRect.top) / sourceRect.height))
        : 0.5;

    handDragState.sourceElement = sourceElement;
    handDragState.ghostElement = ghostEl;
    handDragState.pointerOffsetX = ratioX * baseW;
    handDragState.pointerOffsetY = ratioY * baseH;
    handDragState.ghostWidth = baseW;
    handDragState.ghostHeight = baseH;
    handDragState.isPending = false;
    handDragState.isActive = true;
    sourceElement.classList.add('hand-drag-origin');
    document.body.classList.add('hand-card-dragging');
    deferCardArtQueue();
    logUiPerfDebug('hand-drag', 'activated', {
        sessionId: getUiPerfDebugState().handDrag?.sessionId ?? 0,
        cardUid,
        menuStarrySkyAnimating: getUiPerfDebugState().menuStarrySky?.isAnimating === true,
        menuRoute: getUiPerfDebugState().menuStarrySky?.route || ''
    });

    updateHandDragDropLaneLayout(findCardInstanceByUid(cardUid));
    scheduleHandDragGhostUpdate();
    refreshSelfPpPreview();
}

function animateHandDragGhostToSource(ghostEl, sourceElement) {
    if (!ghostEl || !sourceElement) {
        ghostEl?.remove();
        return;
    }

    const sourceRect = sourceElement.getBoundingClientRect();
    ghostEl.classList.add('is-snapping-back');
    ghostEl.style.transform = `translate3d(${sourceRect.left}px, ${sourceRect.top}px, 0) scale(1)`;
    runTrackedVisualTimeout(() => {
        ghostEl.remove();
    }, HAND_DRAG_SNAP_BACK_MS + 40);
}

function resolveAttackDragDropTarget(clientX, clientY) {
    const hit = document.elementFromPoint(clientX, clientY);
    if (!hit) {
        return { kind: 'none', uid: null, element: null, isValid: false };
    }

    const targetCard = hit.closest?.('.card[data-uid]');
    if (targetCard?.dataset?.uid) {
        const uid = String(targetCard.dataset.uid);
        const isValid = targetCard.classList.contains('targetable');
        return { kind: 'card', uid, element: targetCard, isValid };
    }

    const opponentHudEl = hit.closest?.('#player2-info');
    if (opponentHudEl && !hit.closest?.('button')) {
        const avatarEl = resolveOpponentLeaderAvatarElement();
        const uid = resolveAvatarLeaderUid(avatarEl) ?? resolveOpponentLeaderUid();
        const isValid = Boolean(uid) && isAttackDragLeaderTargetable(avatarEl);
        return {
            kind: 'leader-hud',
            uid: uid ? String(uid) : null,
            element: opponentHudEl,
            isValid
        };
    }

    const avatarEl = hit.closest?.('#player1-avatar, #player2-avatar');
    if (avatarEl) {
        const uid = resolveAvatarLeaderUid(avatarEl);
        const isValid = avatarEl.classList.contains('targetable');
        return { kind: 'avatar', uid: uid ? String(uid) : null, element: avatarEl, isValid };
    }

    return { kind: 'none', uid: null, element: null, isValid: false };
}

function isAttackDragStartAllowed(cardInstance) {
    if (!cardInstance) return false;
    if (isAwaitingPlayerChoice || isConnectionLocked || gameState.isSpectator) return false;

    const localPlayerId = gameState.localPlayerId ?? uiCurrentPlayerId;
    if (!localPlayerId || gameState.currentPlayerId !== localPlayerId) return false;
    if (gameState.phase !== 'Main') return false;

    if (cardInstance.ownerId !== localPlayerId) return false;
    if (!equalsIgnoreCase(cardInstance.zone, 'Field')) return false;
    if (!equalsIgnoreCase(cardInstance.type, 'Follower')) return false;
    if ((cardInstance.canAttack ?? cardInstance.CanAttack) !== true) return false;

    return true;
}

function updateAttackDragLine() {
    const svgEl = attackDragState.lineElement;
    if (!attackDragState.isActive || !svgEl) return;

    const x1 = attackDragState.sourceCenterX;
    const y1 = attackDragState.sourceCenterY;
    const x2 = attackDragState.currentX;
    const y2 = attackDragState.currentY;
    const dx = x2 - x1;
    const dy = y2 - y1;
    const dist = Math.hypot(dx, dy);
    const ux = dist >= 2 ? dx / dist : 0;
    const uy = dist >= 2 ? dy / dist : -1;
    const px = -uy;
    const py = ux;

    const arrowBack = 13;
    const arrowNotch = 9.5;
    const arrowHalfWing = 5.2;
    const lineEndX = dist > arrowNotch + 2 ? x2 - ux * arrowNotch : x2;
    const lineEndY = dist > arrowNotch + 2 ? y2 - uy * arrowNotch : y2;

    const originDot = svgEl.querySelector('.attack-drag-origin-dot');
    const line = svgEl.querySelector('.attack-drag-vector');
    const arrow = svgEl.querySelector('.attack-drag-arrowhead');

    if (originDot) {
        originDot.setAttribute('cx', x1.toFixed(1));
        originDot.setAttribute('cy', y1.toFixed(1));
    }
    if (line) {
        line.setAttribute('x1', x1.toFixed(1));
        line.setAttribute('y1', y1.toFixed(1));
        line.setAttribute('x2', lineEndX.toFixed(1));
        line.setAttribute('y2', lineEndY.toFixed(1));
    }
    if (arrow) {
        const leftX = x2 - ux * arrowBack + px * arrowHalfWing;
        const leftY = y2 - uy * arrowBack + py * arrowHalfWing;
        const notchX = x2 - ux * arrowNotch;
        const notchY = y2 - uy * arrowNotch;
        const rightX = x2 - ux * arrowBack - px * arrowHalfWing;
        const rightY = y2 - uy * arrowBack - py * arrowHalfWing;
        arrow.setAttribute(
            'points',
            `${x2.toFixed(1)},${y2.toFixed(1)} ${leftX.toFixed(1)},${leftY.toFixed(1)} ${notchX.toFixed(1)},${notchY.toFixed(1)} ${rightX.toFixed(1)},${rightY.toFixed(1)}`
        );
    }

    const isLockedValid = Boolean(attackDragState.hoverTargetUid && attackDragState.hoverElement?.classList?.contains('attack-drag-hover'));
    svgEl.classList.toggle('is-locked', isLockedValid);
}

function updateAttackDragHover(targetInfo) {
    const nextKind = String(targetInfo?.kind ?? 'none');
    const nextUid = targetInfo?.uid ? String(targetInfo.uid) : null;
    const nextElement = targetInfo?.element ?? null;

    if (
        attackDragState.hoverKind === nextKind
        && attackDragState.hoverTargetUid === nextUid
        && attackDragState.hoverElement === nextElement
    ) {
        return;
    }

    clearAttackDragHover();

    if (nextKind === 'leader-hud' && nextElement) {
        if (targetInfo?.isValid === true) {
            nextElement.classList.add('attack-drag-hover');
        } else {
            nextElement.classList.add('attack-drag-invalid');
        }
        attackDragState.hoverKind = nextKind;
        attackDragState.hoverTargetUid = targetInfo?.isValid === true ? nextUid : null;
        attackDragState.hoverElement = nextElement;
        return;
    }

    if (targetInfo?.isValid && nextUid && nextElement) {
        nextElement.classList.add('attack-drag-hover');
        attackDragState.hoverKind = nextKind;
        attackDragState.hoverTargetUid = nextUid;
        attackDragState.hoverElement = nextElement;
    }
}

function activateAttackDragIfNeeded() {
    if (!attackDragState.isPending || attackDragState.isActive) return;
    const attackerUid = attackDragState.attackerUid;
    if (!attackerUid) {
        resetAttackDragState();
        return;
    }

    initiateAttack(attackerUid);
    if (String(gameState.selectedAttackerUid ?? '') !== String(attackerUid)) {
        resetAttackDragState();
        return;
    }

    const sourceElement = gameContainerEl?.querySelector(`.field-card[data-uid="${attackerUid}"]`) ?? attackDragState.sourceElement;
    if (!sourceElement) {
        resetAttackDragState();
        return;
    }

    const sourceRect = sourceElement.getBoundingClientRect();
    attackDragState.sourceCenterX = sourceRect.left + sourceRect.width / 2;
    attackDragState.sourceCenterY = sourceRect.top + sourceRect.height * 0.38;
    attackDragState.lineElement = ensureAttackDragLineElement();
    attackDragState.isPending = false;
    attackDragState.isActive = true;
    document.body.classList.add('attack-dragging');
    updateAttackDragLine();
}

function resetMulliganSelection() {
    mulliganSelectedUids = new Set();
    selectHandCard(null);
    try {
        player1HandEl?.querySelectorAll?.('.card.selected-for-mulligan')?.forEach?.(el => el.classList.remove('selected-for-mulligan'));
        document.querySelectorAll('.hand-card.hand-card-floated')
            .forEach(el => el.classList.remove('hand-card-floated'));
    } catch (_) {
        // ignore
    }
}

function clearMulliganVisualState() {
    mulliganSelectedUids = new Set();
    gameContainerEl?.classList.remove('mulligan-active');
    try {
        document.querySelectorAll('.card.mulligan-selectable, .card.selected-for-mulligan')
            .forEach(el => el.classList.remove('mulligan-selectable', 'selected-for-mulligan'));
    } catch (_) {
        // ignore
    }
}

let hoveredHandPreviewUid = null;

function resolveActiveSelfPpPreviewCost() {
    if (gameState.isSpectator) return 0;
    const localPlayerId = gameState.localPlayerId;
    if (!localPlayerId) return 0;

    const isMirage = isMirageMode(gameState);
    if (isMirage) {
        if (gameState.phase !== 'Recruit') return 0;
    } else {
        if (gameState.currentPlayerId !== localPlayerId) return 0;
        if (gameState.phase !== 'Main' && gameState.phase !== 'TurnStart') return 0;
    }

    const candidateUid = String(
        (handDragState.isActive && handDragState.cardUid)
        || hoveredHandPreviewUid
        || gameState.selectedCardUid
        || ''
    ).trim();
    if (!candidateUid) return 0;

    const cardInstance = findCardInstanceByUid(candidateUid);
    if (!cardInstance) return 0;
    const ownerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId;
    if (!isSamePlayerId(ownerId, localPlayerId)) return 0;
    if (!equalsIgnoreCase(cardInstance.zone, 'Hand')) return 0;

    const costInfo = resolveCardCostDisplay(cardInstance, 'hand');
    const numericCost = Number(costInfo?.displayCost);
    return Number.isFinite(numericCost) && numericCost > 0 ? Math.trunc(numericCost) : 0;
}

function refreshSelfPpPreview() {
    const localPlayerId = gameState.localPlayerId;
    if (!localPlayerId) return;
    const localPlayer = getPlayerState(localPlayerId);
    if (!localPlayer) return;
    updatePpDisplay(
        localPlayer.id,
        localPlayer.pp ?? 0,
        localPlayer.maxPP ?? 0,
        localPlayer.ppBoostArmed === true
    );
}

function setFloatingHandCard(cardUid) {
    const nextUid = selectHandCard(cardUid);

    document.querySelectorAll('.hand-card.hand-card-floated')
        .forEach(el => el.classList.remove('hand-card-floated'));

    if (nextUid) {
        document.querySelectorAll('.hand-card[data-uid]')
            .forEach(el => {
                if (String(el.dataset.uid ?? '') === nextUid) {
                    el.classList.add('hand-card-floated');
                }
            });
    }

    refreshSelfPpPreview();
}

function preserveCardStateClasses(sourceEl, targetEl) {
    if (!sourceEl || !targetEl) return;
    PRESERVED_CARD_STATE_CLASSES.forEach(className => {
        if (sourceEl.classList.contains(className)) {
            targetEl.classList.add(className);
        }
    });
}

function clearTransientCardAnimationClasses(element) {
    if (!element?.classList) return;
    element.classList.remove(
        'card-fade-enter',
        'hand-card-fade-enter-down',
        'card-fade-exit',
        'hand-card-fade-exit-up',
        'card-fade-move',
        'card-enter-hidden',
        'hand-card-enter-hidden'
    );
}

function prefersReducedMotion() {
    return typeof window !== 'undefined'
        && typeof window.matchMedia === 'function'
        && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

function toSafeInteger(value, fallback = 0) {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) return fallback;
    return Math.trunc(parsed);
}

function toSafeCounterValue(value, fallback = 0) {
    return Math.max(0, toSafeInteger(value, fallback));
}

function tweenIntegerText(element, targetValue, options = {}) {
    if (!element) return;

    const prevRaw = Number.parseInt(String(element.textContent ?? '').trim(), 10);
    const hasPrev = Number.isFinite(prevRaw);
    const safeTarget = options?.nonNegative === true
        ? toSafeCounterValue(targetValue, 0)
        : toSafeInteger(targetValue, 0);
    element.textContent = `${safeTarget}`;

    if (
        hasPrev
        && prevRaw !== safeTarget
        && isGamePageActive()
        && gameState.phase !== 'Initializing'
        && gameState.phase !== 'Mulligan'
    ) {
        const isCombatStat = element.matches?.('.attack, .health, .hp-value') === true;
        const isNeutralBadge = element.matches?.('.countdown, .emblem-countdown') === true
            || element.parentElement?.classList?.contains('cost') === true;
        if (isCombatStat || isNeutralBadge) {
            const tone = isCombatStat
                ? (safeTarget > prevRaw ? 'buff' : 'damage')
                : 'neutral';
            const popTarget = element.parentElement?.classList?.contains('cost')
                ? element.parentElement
                : element;
            getBattleMotionController().animateStatPop(popTarget, tone);
        }
    }
}

function resolveRenderedCardKey(element, index) {
    if (!element) return `legacy-${index}`;
    const explicitKey = String(element.dataset.renderKey || '').trim();
    if (explicitKey) return explicitKey;

    const uid = String(element.dataset.uid || '').trim();
    if (uid) {
        if (element.classList.contains('field-card')) return `field-${uid}`;
        if (element.classList.contains('hand-card')) return `hand-${uid}`;
    }

    return `legacy-${index}`;
}

const LIVE_BATTLE_ZONE_SPECS = [
    { zone: 'opp:field', selector: '#player2-field > .card[data-uid]' },
    { zone: 'self:field', selector: '#player1-field > .card[data-uid]' },
    { zone: 'self:hand', selector: '#player1-hand > .card[data-uid]' },
    { zone: 'opp:hand', selector: '#player2-hand.is-face-up-opponent > .card[data-uid]' },
    { zone: 'emblem', selector: '.emblem-zone .emblem-slot.filled[data-uid]' }
];

let battleMotionController = null;
const suppressedEnterUids = new Map();
const suppressedExitUids = new Map();
const pendingCardPlayHoverMap = new Map();
const recentShieldBrokenUntil = new Map();
const recentFusedUids = new Map();

function normalizeMotionUidKey(uid) {
    return String(uid ?? '').trim().toLowerCase();
}

function markTimedUidFlag(map, uid, ttlMs = 2400) {
    const key = normalizeMotionUidKey(uid);
    if (!key) return;
    map.set(key, Date.now() + ttlMs);
}

function consumeTimedUidFlag(map, uid) {
    const key = normalizeMotionUidKey(uid);
    if (!key || !map.has(key)) return false;
    const expiresAt = map.get(key);
    map.delete(key);
    return Date.now() <= expiresAt;
}

function hasActiveTimedUidFlag(map, uid) {
    const key = normalizeMotionUidKey(uid);
    if (!key || !map.has(key)) return false;
    const expiresAt = map.get(key);
    if (Date.now() > expiresAt) {
        map.delete(key);
        return false;
    }
    return true;
}

function getBattleMotionController() {
    if (battleMotionController) {
        return battleMotionController;
    }
    battleMotionController = createBattleMotionController({
        getStageElement: () => gameContainerEl ?? document.getElementById('game-container'),
        getTransientLayer: () => ensureTransientVisualRoot(),
        uidAttribute: 'data-uid',
        zoneSpecs: LIVE_BATTLE_ZONE_SPECS,
        resolveEntityElement: (_stageEl, uid) => resolveRenderableTargetElement(uid),
        resolveOppHandElement: stageEl => stageEl?.querySelector('#player2-hand'),
        resolveSelfDeckElement: stageEl => stageEl?.querySelector('#player1-corner-pod .deck-box, #player1-deck-count'),
        resolveOppDeckElement: stageEl => stageEl?.querySelector('#player2-corner-pod .deck-box, #player2-deck-count')
    });
    return battleMotionController;
}

function ensureEagerCardArt(cardEl, cardData) {
    if (!cardEl || !cardData) return;
    const artEl = cardEl.querySelector?.('.card-art');
    if (!artEl) return;
    const previewPath = cardData.artPreview
        ?? cardData.ArtPreview
        ?? cardData.artDetail
        ?? cardData.ArtDetail
        ?? '';
    const resolvedUrl = resolveArtUrl(previewPath);
    if (resolvedUrl) {
        artEl.style.backgroundImage = `url("${resolvedUrl}")`;
        artEl.classList.add('has-art');
    }
}

function buildStandaloneMotionCardClone(cardDtoOrInstance, paidCost = null, playMode = null) {
    if (!cardDtoOrInstance) return null;
    const hydrated = createDiscoverPreviewCard(cardDtoOrInstance) ?? cardDtoOrInstance;
    const snapshotCost = cardDtoOrInstance.CurrentCost
        ?? cardDtoOrInstance.currentCost
        ?? cardDtoOrInstance.Cost
        ?? cardDtoOrInstance.cost;
    if (hydrated && hydrated !== cardDtoOrInstance && Number.isFinite(Number(snapshotCost))) {
        hydrated.currentCost = Number(snapshotCost);
        hydrated.cost = Number(snapshotCost);
    }
    const rawCardEl = createCardElement(hydrated, 'hand', paidCost, playMode);
    if (!rawCardEl) return null;
    ensureEagerCardArt(rawCardEl, cardDtoOrInstance);
    return createCleanCardClone(rawCardEl, 102, 140);
}

function cancelAllBattleMotions() {
    suppressedEnterUids.clear();
    suppressedExitUids.clear();
    pendingCardPlayHoverMap.clear();
    recentShieldBrokenUntil.clear();
    recentFusedUids.clear();
    battleMotionController?.cancelAll();
}

function notifyShieldRemoved(unitUid) {
    markTimedUidFlag(recentShieldBrokenUntil, unitUid, 450);
}

function playAttackLunge(attackerUid, targetUid) {
    if (!isGamePageActive() || !attackerUid || !targetUid) {
        return Promise.resolve();
    }
    return getBattleMotionController().playAttackLungeAndHold({
        attackerUid: String(attackerUid),
        targetUid: String(targetUid)
    });
}

function playUnitEvolutionEffect(unitUid, isSuper = false) {
    if (!isGamePageActive() || !unitUid) {
        return Promise.resolve();
    }
    return getBattleMotionController().playEvolveBurst(String(unitUid), Boolean(isSuper));
}

function notifyCardPlayed(playedCardDto, sourcePlayerId, paidCost, playMode) {
    if (!isGamePageActive() || !playedCardDto) return undefined;
    const stageEl = gameContainerEl ?? document.getElementById('game-container');
    if (!stageEl) return undefined;

    const now = Date.now();
    for (const [key, entry] of pendingCardPlayHoverMap.entries()) {
        if (now - entry.createdAt > 3000) {
            pendingCardPlayHoverMap.delete(key);
        }
    }

    const cardUid = String(
        playedCardDto.InstanceId ?? playedCardDto.instanceId ?? playedCardDto.uid ?? ''
    ).trim();
    if (!cardUid) return undefined;

    const stageRect = stageEl.getBoundingClientRect();
    const isOpponent = !isSamePlayerId(sourcePlayerId, gameState.localPlayerId);
    const anchors = getBattleMotionController().getAnchorRects();

    let isFromDrag = false;
    let sourceRect = null;

    if (
        !isOpponent
        && handPlayCommitState.ghostElement
        && normalizeMotionUidKey(handPlayCommitState.cardUid) === normalizeMotionUidKey(cardUid)
    ) {
        isFromDrag = true;
        sourceRect = toStageRect(handPlayCommitState.ghostElement.getBoundingClientRect(), stageRect);
        clearCommittedHandPlayVisual({ removeGhost: true, clearSource: true });
    } else {
        const handCardEl = stageEl.querySelector(`.hand-card[data-uid="${cardUid}"]`);
        if (handCardEl) {
            sourceRect = toStageRect(handCardEl.getBoundingClientRect(), stageRect);
        } else if (isOpponent) {
            sourceRect = anchors?.oppHandRect ?? null;
        } else if (player1HandEl) {
            sourceRect = toStageRect(player1HandEl.getBoundingClientRect(), stageRect);
        }
    }

    const cardHtmlElement = buildStandaloneMotionCardClone(playedCardDto, paidCost, playMode);
    if (!cardHtmlElement) return undefined;

    const rawType = String(playedCardDto.Type ?? playedCardDto.type ?? '').trim().toLowerCase();
    const isSpell = rawType === 'spell';
    const legacyName = playMode == null ? (playedCardDto.Name ?? playedCardDto.name ?? '') : '';
    const alternateAnimation = playModeVisual(playMode, legacyName).animation;

    markTimedUidFlag(suppressedExitUids, cardUid, 2600);
    if (!isSpell) {
        markTimedUidFlag(suppressedEnterUids, cardUid, 2600);
    }

    pendingCardPlayHoverMap.set(normalizeMotionUidKey(cardUid), {
        cardUid,
        cardHtmlElement,
        sourceRect,
        isOpponent,
        isFromDrag,
        isSpell,
        alternateAnimation,
        createdAt: now
    });
    return undefined;
}

function isFaceUpHandForOwner(ownerId) {
    if (isSamePlayerId(ownerId, gameState.localPlayerId)) return true;
    return gameState.dualControl === true || gameState.isSpectator === true;
}

function prepareCardMoveVisual(moveContext) {
    if (!isGamePageActive() || !moveContext?.cardUid) return null;

    const { cardUid, ownerId, fromZone, toZone, moveReason } = moveContext;
    const motion = getBattleMotionController();
    const existingEl = resolveRenderableTargetElement(cardUid);
    const isCardDom = existingEl?.classList?.contains('card') === true
        || existingEl?.classList?.contains('emblem-slot') === true;
    const snap = isCardDom
        ? motion.captureElementSnapshot(existingEl, cardUid, fromZone || 'unknown')
        : null;

    const pendingKey = normalizeMotionUidKey(cardUid);
    const hasPendingPlay = pendingCardPlayHoverMap.has(pendingKey);

    if (
        hasPendingPlay
        || moveReason === 'destroyed'
        || moveReason === 'vanished'
        || moveReason === 'burned'
        || moveReason === 'discarded'
        || moveReason === 'returnedtohand'
        || (toZone === 'deck' && (fromZone === 'field' || fromZone === 'hand'))
    ) {
        markTimedUidFlag(suppressedExitUids, cardUid, 2200);
    }

    if (
        (hasPendingPlay && toZone === 'field')
        || (toZone === 'hand' && isFaceUpHandForOwner(ownerId))
    ) {
        markTimedUidFlag(suppressedEnterUids, cardUid, 2200);
    }

    return { snap };
}

function commitCardMoveVisual(moveContext, preMoveVisual, movedInstance) {
    if (!isGamePageActive() || !moveContext?.cardUid) return undefined;

    const { cardUid, ownerId, fromZone, toZone, moveReason } = moveContext;
    const motion = getBattleMotionController();
    const anchors = motion.getAnchorRects();
    const isOwnerLocal = isSamePlayerId(ownerId, gameState.localPlayerId);

    const pendingKey = normalizeMotionUidKey(cardUid);
    const pendingPlay = pendingCardPlayHoverMap.get(pendingKey) ?? null;

    if (pendingPlay && (toZone === 'field' || (pendingPlay.isSpell && toZone === 'playing'))) {
        pendingCardPlayHoverMap.delete(pendingKey);
        return motion.playCastHoverSequence({
            cardHtmlElement: pendingPlay.cardHtmlElement,
            sourceRect: pendingPlay.sourceRect,
            targetUid: toZone === 'field' ? cardUid : null,
            isSpell: toZone !== 'field',
            isOpponent: pendingPlay.isOpponent,
            isFromDrag: pendingPlay.isFromDrag,
            alternateAnimation: pendingPlay.alternateAnimation,
            fastForward: isPresentationFastForward()
        });
    }

    if (moveReason === 'destroyed' || moveReason === 'burned' || moveReason === 'discarded') {
        if (moveReason === 'burned' && preMoveVisual?.snap?.cloneEl) {
            return motion.playBurnCard(preMoveVisual.snap);
        }
        if (motion.markActiveLungeExit(cardUid, 'destroy')) {
            return undefined;
        }
        if (preMoveVisual?.snap?.cloneEl) {
            motion.spawnSemanticExitClone(preMoveVisual.snap, 'destroy');
            return undefined;
        }
        if (moveReason === 'burned' && movedInstance && anchors) {
            const deckAnchor = isOwnerLocal ? anchors.selfDeckRect : anchors.oppDeckRect;
            const burnedClone = buildStandaloneMotionCardClone(movedInstance);
            if (burnedClone && deckAnchor) {
                const burnX = isOwnerLocal
                    ? Math.max(90, deckAnchor.cx + 76)
                    : Math.min(anchors.stageRect.width - 90, deckAnchor.cx - 76);
                const burnY = isOwnerLocal
                    ? Math.max(120, deckAnchor.cy - 86)
                    : Math.min(anchors.stageRect.height - 120, deckAnchor.cy + 86);
                return motion.playBurnCard({
                    uid: cardUid,
                    rect: { left: burnX - 51, top: burnY - 70, width: 102, height: 140, cx: burnX, cy: burnY },
                    unscaledW: 102,
                    unscaledH: 140,
                    isCard: true,
                    cloneEl: burnedClone
                });
            }
        }
        return undefined;
    }

    if (moveReason === 'vanished') {
        if (hasActiveTimedUidFlag(recentFusedUids, cardUid)) {
            return undefined;
        }
        if (motion.markActiveLungeExit(cardUid, 'vanish')) {
            return undefined;
        }
        if (preMoveVisual?.snap?.cloneEl) {
            motion.spawnSemanticExitClone(preMoveVisual.snap, 'vanish');
        }
        return undefined;
    }

    if (moveReason === 'returnedtohand' || (fromZone === 'field' && toZone === 'hand')) {
        if (isFaceUpHandForOwner(ownerId)) {
            const newHandEl = gameContainerEl?.querySelector(`.hand-card[data-uid="${cardUid}"]`);
            const sourceRect = preMoveVisual?.snap?.rect ?? null;
            if (newHandEl && sourceRect) {
                motion.runCrossZoneCarrierFlight({
                    targetUid: cardUid,
                    targetEl: newHandEl,
                    sourceRect,
                    durationMs: 250,
                    pulseTone: 'cyan'
                });
                return undefined;
            }
            if (newHandEl) {
                motion.playHandRightSlideIn({
                    targetUid: cardUid,
                    targetEl: newHandEl,
                    durationMs: 240,
                    pulseTone: 'cyan'
                });
                return undefined;
            }
        }
        if (preMoveVisual?.snap?.cloneEl && anchors?.oppHandRect) {
            motion.spawnSemanticExitClone(preMoveVisual.snap, 'return', null, anchors.oppHandRect);
        }
        return undefined;
    }

    if (toZone === 'deck' && (fromZone === 'field' || fromZone === 'hand')) {
        if (preMoveVisual?.snap?.cloneEl && anchors) {
            const deckRect = isOwnerLocal ? anchors.selfDeckRect : anchors.oppDeckRect;
            motion.spawnSemanticExitClone(preMoveVisual.snap, 'deck', null, deckRect);
        }
        return undefined;
    }

    if (toZone === 'hand' && isFaceUpHandForOwner(ownerId)) {
        const newHandEl = gameContainerEl?.querySelector(`.hand-card[data-uid="${cardUid}"]`);
        if (newHandEl) {
            motion.playHandRightSlideIn({
                targetUid: cardUid,
                targetEl: newHandEl,
                durationMs: 240,
                pulseTone: 'cyan'
            });
        }
    }

    return undefined;
}

let cardAnimationSystem = null;

function getCardAnimationSystem() {
    if (cardAnimationSystem) {
        return cardAnimationSystem;
    }
    cardAnimationSystem = createCardAnimationSystem({
        durations: {
            enterMs: CARD_ENTER_TRANSITION_MS,
            moveMs: CARD_MOVE_TRANSITION_MS,
            exitMs: CARD_EXIT_TRANSITION_MS
        },
        move: {
            epsilon: CARD_MOVE_EPSILON,
            opacity: CARD_MOVE_OPACITY
        },
        stagger: {
            enterMs: CARD_ENTER_STAGGER_MS,
            moveMs: CARD_MOVE_STAGGER_MS,
            exitMs: CARD_EXIT_STAGGER_MS,
            maxMs: CARD_STAGGER_MAX_MS
        },
        sequencing: {
            enterAfterExitMs: CARD_ENTER_AFTER_EXIT_DELAY_MS
        },
        isPageActive: isGamePageActive,
        ensureTransientRoot: ensureTransientVisualRoot,
        scheduleTimeout: runTrackedVisualTimeout,
        preserveStateClasses: preserveCardStateClasses,
        clearTransientClasses: clearTransientCardAnimationClasses,
        resolveRenderedCardKey,
        prefersReducedMotion,
        shouldSkipEnter: (_key, element) => consumeTimedUidFlag(suppressedEnterUids, element?.dataset?.uid),
        shouldSkipExit: (_key, element) => consumeTimedUidFlag(suppressedExitUids, element?.dataset?.uid),
        onElementMounted: (_key, element) => {
            const uid = element?.dataset?.uid;
            if (uid) {
                getBattleMotionController().syncEntityElementVisibility(uid, element);
            }
        }
    });
    return cardAnimationSystem;
}

function animateCardEnter(element) {
    if (!element) return;
    const key = String(element.dataset.renderKey || element.dataset.uid || 'enter-unknown');
    getCardAnimationSystem().animateEnter(element, key, element.parentElement);
}

function animateCardLeaveOverlay(sourceElement, sourceRect) {
    if (!sourceElement || !sourceRect) return;
    const key = String(sourceElement.dataset.renderKey || sourceElement.dataset.uid || 'exit-unknown');
    getCardAnimationSystem().animateLeaveOverlay(sourceElement, sourceRect, key, sourceElement.parentElement);
}

function renderAnimatedCardCollection(container, entries = [], options = {}) {
    getCardAnimationSystem().renderCollection(container, entries, options);
}

function buildFaceUpHandEntries(handCards = [], isLocalHand = false) {
    const cards = Array.isArray(handCards) ? handCards : [];
    const selectedHandUid = String(gameState.selectedCardUid ?? '').trim();
    const entries = [];
    const total = cards.length;
    const mid = (total - 1) / 2;

    cards.forEach((cardInstance, index) => {
        if (!cardInstance) return;
        const cardElement = createCardElement(cardInstance, 'hand');
        if (!cardElement) return;

        const offset = index - mid;
        cardElement.style.setProperty('--fan-offset', offset.toFixed(2));
        cardElement.style.setProperty('--fan-abs', Math.abs(offset).toFixed(2));
        cardElement.style.setProperty('--hand-count', String(total));

        if (isLocalHand && gameState.phase === 'Mulligan') {
            const localPlayer = getPlayerState(gameState.localPlayerId);
            const localCompleted = localPlayer?.hasCompletedMulligan === true;
            if (!localCompleted) {
                cardElement.classList.add('mulligan-selectable');
                const uid = cardElement.dataset.uid;
                if (uid && mulliganSelectedUids.has(uid)) {
                    cardElement.classList.add('selected-for-mulligan');
                }
            }
        }

        const cardUid = getCardUid(cardInstance);
        if (isLocalHand && selectedHandUid && String(cardUid ?? '') === selectedHandUid) {
            cardElement.classList.add('hand-card-floated');
        }
        const key = cardUid ? `hand-${cardUid}` : `hand-index-${index}`;
        entries.push({ key, element: cardElement });
    });

    return entries;
}

function buildFaceDownHandEntries(handCards = []) {
    const cards = Array.isArray(handCards) ? handCards : [];
    const total = cards.length;
    const mid = (total - 1) / 2;
    return cards.map((_, index) => {
        const cardBackDiv = document.createElement('div');
        cardBackDiv.classList.add('card-back');
        const offset = index - mid;
        cardBackDiv.style.setProperty('--fan-offset', offset.toFixed(2));
        cardBackDiv.style.setProperty('--fan-abs', Math.abs(offset).toFixed(2));
        return {
            key: `opp-hand-back-${index}`,
            element: cardBackDiv
        };
    });
}

function buildFieldEntries(fieldCards = []) {
    const cards = Array.isArray(fieldCards) ? fieldCards : [];
    const entries = [];
    cards.forEach((cardInstance, index) => {
        if (!cardInstance) return;
        const cardElement = createCardElement(cardInstance, 'field');
        if (!cardElement) return;
        const cardUid = getCardUid(cardInstance);
        const key = cardUid ? `field-${cardUid}` : `field-index-${index}`;
        entries.push({ key, element: cardElement });
    });
    return entries;
}

function applyHandOverlapCssVars(handElement, count) {
    if (!handElement) return;
    let overlapPx = 3;
    let hoverOverlapPx = 5;
    if (count >= 8) {
        overlapPx = -15;
        hoverOverlapPx = -8;
    } else if (count === 7) {
        overlapPx = -5;
        hoverOverlapPx = 0;
    } else if (count === 6) {
        overlapPx = 1.5;
        hoverOverlapPx = 3;
    }
    handElement.style.setProperty('--hand-overlap', `${overlapPx}px`);
    handElement.style.setProperty('--hand-overlap-hover', `${hoverOverlapPx}px`);
}

function syncFieldZoneDensity(fieldElement, count) {
    const zoneWrapper = fieldElement?.closest?.('.battle-field-zone') ?? fieldElement;
    if (!zoneWrapper) return;
    const safeCount = Math.max(0, Number(count) || 0);
    zoneWrapper.style.setProperty('--slot-count', String(Math.max(5, safeCount)));
    zoneWrapper.classList.toggle('is-crowded', safeCount >= 6);
}

function applyLeaderHpBarWithGhostSettle(hpContainer, hpPercent, isDanger) {
    if (!hpContainer) return;
    const safePct = Math.max(0, Math.min(100, Number(hpPercent) || 0));
    hpContainer.classList.toggle('is-danger', Boolean(isDanger));
    hpContainer.classList.toggle('is-low', safePct < 30 || Boolean(isDanger));
    hpContainer.closest('.leader-rpg-hud')?.classList.toggle('is-danger', Boolean(isDanger));

    const fillEl = hpContainer.querySelector('.hp-bar-fill');
    const ghostEl = hpContainer.querySelector('.hp-bar-ghost');
    if (fillEl) {
        fillEl.style.width = `${safePct}%`;
        fillEl.classList.toggle('low-hp', safePct < 30 || Boolean(isDanger));
    }

    if (!ghostEl) return;
    const prevPctRaw = Number.parseFloat(hpContainer.dataset.lastHpPct ?? '');
    hpContainer.dataset.lastHpPct = safePct.toFixed(2);

    const containerKey = hpContainer.id || 'hp-unknown';
    const existingTimer = hpGhostSettleTimers.get(containerKey);
    if (existingTimer) {
        window.clearTimeout(existingTimer);
        hpGhostSettleTimers.delete(containerKey);
    }

    if (Number.isFinite(prevPctRaw) && safePct < prevPctRaw - 0.1 && !prefersReducedMotion()) {
        ghostEl.style.width = `${prevPctRaw}%`;
        const timerId = runTrackedVisualTimeout(() => {
            hpGhostSettleTimers.delete(containerKey);
            if (ghostEl.isConnected) {
                ghostEl.style.width = `${safePct}%`;
            }
        }, HP_GHOST_SETTLE_DELAY_MS);
        hpGhostSettleTimers.set(containerKey, timerId);
    } else {
        ghostEl.style.width = `${safePct}%`;
    }
}

// --- Specific UI Update Functions ---

/**
 * Updates the PP display for a specific player.
 * @param {string} playerId - The GUID of the player.
 * @param {number} currentPp - The current PP value.
 * @param {number} maxPp - The maximum PP value.
 * @param {boolean} isPpBoostArmed - Whether PP+1 is currently armed.
 */
function updatePpDisplay(playerId, currentPp, maxPp, isPpBoostArmed = false) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const ppBarContainer = document.getElementById(`${prefix}-pp-bar`);
    const rawCurrentPp = Math.max(0, Number(currentPp ?? 0) || 0);
    const rawMaxPp = Math.max(0, Number(maxPp ?? 0) || 0);
    const baseFilledCount = Math.min(rawCurrentPp, rawMaxPp);
    const naturalOverflowCount = Math.max(0, rawCurrentPp - rawMaxPp);
    const armedOverflowCount = isPpBoostArmed ? 1 : 0;
    const overflowCount = naturalOverflowCount + armedOverflowCount;
    const displayCurrentPp = rawCurrentPp + armedOverflowCount;

    // In Mirage mode, upper block displays Tavern Tier (handled in renderMirageShopPanel / renderPlayerStats)
    if (!isLocalPlayer && isMirageMode(gameState)) {
        return;
    }

    const ppLabelEl = document.getElementById(`${prefix}-pp-label`);
    if (ppLabelEl) {
        ppLabelEl.textContent = (isLocalPlayer && isMirageMode(gameState)) ? 'GP' : 'PP';
    }

    const previewCost = isLocalPlayer ? resolveActiveSelfPpPreviewCost() : 0;
    const isPreviewEnough = previewCost > 0 && previewCost <= displayCurrentPp;
    const isPreviewInsufficient = previewCost > displayCurrentPp;

    const ppTextEl = document.getElementById(`${prefix}-pp-text`)
        ?? document.getElementById(`${prefix}-stats`)?.querySelector('.pp-text');
    if (ppTextEl) {
        const curLabel = isPreviewEnough
            ? `${displayCurrentPp}→${displayCurrentPp - previewCost}`
            : `${displayCurrentPp}`;
        ppTextEl.innerHTML = `<span class="pp-cur">${curLabel}</span><span class="pp-max">/ ${maxPp ?? 0}</span>`;
        ppTextEl.classList.toggle('is-previewing', isPreviewEnough);
        ppTextEl.classList.toggle('is-insufficient', isPreviewInsufficient);
    }

    const selfPpWrapper = isLocalPlayer ? document.getElementById('right-dock-self-pp') : document.getElementById('right-dock-upper-pp');
    if (selfPpWrapper && isLocalPlayer) {
        selfPpWrapper.classList.toggle('is-gold', isMirageMode(gameState));
    }

    // Render PP Bar Segments (with live PP cost preview on self gauge)
    if (ppBarContainer) {
        ppBarContainer.classList.remove('is-six');
        ppBarContainer.innerHTML = '';
        const displayMax = 10; // Always show 10 slots for consistency
        const safeMax = Math.max(0, Math.min(rawMaxPp, displayMax));
        const safeBaseFilled = Math.max(0, Math.min(baseFilledCount, safeMax));
        const safeOverflowStart = Math.max(0, Math.min(safeBaseFilled, displayMax));
        const safeOverflowCount = Math.max(0, Math.min(overflowCount, Math.max(0, displayMax - safeOverflowStart)));
        const safeAvailTotal = Math.min(displayMax, safeBaseFilled + safeOverflowCount);
        const spendStartIdx = isPreviewEnough ? Math.max(0, safeAvailTotal - previewCost) : -1;
        const insufficientEndIdx = isPreviewInsufficient ? Math.min(displayMax, previewCost) : -1;

        for (let i = 0; i < displayMax; i++) {
            const segment = document.createElement('div');
            segment.classList.add('pp-segment');

            if (i < safeBaseFilled) {
                segment.classList.add('active'); // Filled PP
            } else if (i >= safeOverflowStart && i < safeOverflowStart + safeOverflowCount) {
                segment.classList.add('overflow'); // PP+1 overflow
            } else if (i < safeMax) {
                segment.classList.add('empty', 'unlocked'); // Unlocked this match, spent this turn
            } else {
                segment.classList.add('empty', 'locked'); // Locked/Max capacity limit
            }

            if (isPreviewEnough && i >= spendStartIdx && i < safeAvailTotal) {
                segment.classList.add('will-spend');
            } else if (isPreviewInsufficient && i >= safeAvailTotal && i < insufficientEndIdx) {
                segment.classList.add('insufficient-preview');
            }

            ppBarContainer.appendChild(segment);
        }
    }
}

/**
 * Updates the EP display for a specific player.
 * @param {string} playerId - The GUID of the player.
 * @param {number} currentEp - The current EP value.
 * @param {number} maxEp - The maximum EP value.
 */
function updateEpDisplay(playerId, currentEp, maxEp) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const epDotsContainer = document.getElementById(`${prefix}-ep-dots`);

    // Render EP Dots
    if (epDotsContainer) {
        epDotsContainer.innerHTML = '';
        // Default to 2 max EP if 0 or undefined (User Request for initial state)
        const totalDots = (maxEp && maxEp > 0) ? maxEp : 2;
        const currentDots = currentEp || 0;

        for (let i = 0; i < totalDots; i++) {
            const dot = document.createElement('div');
            dot.classList.add('ep-dot');
            if (i < currentDots) {
                dot.classList.add('active');
            }
            epDotsContainer.appendChild(dot);
        }
    }
    if (isLocalPlayer) evolutionDrag.refresh();
}

/**
 * Updates the SEP display for a specific player.
 * @param {string} playerId - The GUID of the player.
 * @param {number} currentSep - The current SEP value.
 * @param {number} maxSep - The maximum SEP value.
 */
function updateSepDisplay(playerId, currentSep, maxSep) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const sepDotsContainer = document.getElementById(`${prefix}-sep-dots`);

    if (sepDotsContainer) {
        sepDotsContainer.innerHTML = '';
        // Default to 2 max SEP if 0 or undefined (User Request for initial state)
        const totalDots = (maxSep && maxSep > 0) ? maxSep : 2;
        const currentDots = currentSep || 0;

        for (let i = 0; i < totalDots; i++) {
            const dot = document.createElement('div');
            dot.classList.add('sep-dot');
            // Add animation delay for staggered effect if multiple dots update
            dot.style.transitionDelay = `${i * 0.1}s`;

            if (i < currentDots) {
                dot.classList.add('active');
            }
            sepDotsContainer.appendChild(dot);
        }
    }
    if (isLocalPlayer) evolutionDrag.refresh();
}

// --- End Specific UI Update Functions ---

/**
* Updates the deck count display for a specific player.
* @param {string} playerId - The GUID of the player.
* @param {number} count - The current deck count.
*/
function updateDeckCountDisplay(playerId, count) {
    const isLocalPlayer = isSamePlayerId(playerId, gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const deckCountEl = document.getElementById(`${prefix}-deck-count`);

    if (deckCountEl) {
        tweenIntegerText(deckCountEl, count, {
            nonNegative: true
        });
    }
}

/**
* Updates the hand count display for a specific player.
* @param {string} playerId - The GUID of the player.
* @param {number} count - The current hand count.
*/
function updateHandCountDisplay(playerId, count) {
    const isLocalPlayer = isSamePlayerId(playerId, gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const handCountEl = document.getElementById(`${prefix}-hand-count`);

    if (handCountEl) {
        tweenIntegerText(handCountEl, count, {
            nonNegative: true
        });
    }
}

/**
* Updates the graveyard count display for a specific player.
* @param {string} playerId - 'player1' or 'player2'.
* @param {number} count - The current graveyard count.
*/
function updateGraveyardCountDisplay(playerId, count) {
    if (!gameState.localPlayerId) return;

    const isLocalPlayer = isSamePlayerId(playerId, gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const graveyardCountEl = document.getElementById(`${prefix}-grave-count`);

    if (graveyardCountEl) {
        tweenIntegerText(graveyardCountEl, count, {
            nonNegative: true
        });
    }
}

/**
* Updates the blood pool display for a specific player.
* @param {string} playerId - The GUID of the player.
* @param {number} count - The current blood pool count.
*/
function updateBloodPoolDisplay(playerId, count) {
    if (!gameState.localPlayerId) return;

    const isLocalPlayer = isSamePlayerId(playerId, gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const bloodCountEl = document.getElementById(`${prefix}-blood-count`);

    if (bloodCountEl) {
        const safeVal = toSafeCounterValue(count, 0);
        tweenIntegerText(bloodCountEl, safeVal, {
            nonNegative: true
        });
        bloodCountEl.closest('.blood-box')?.classList.toggle('is-zero', safeVal <= 0);
    }
}

/**
* Updates the combo display for a specific player.
* @param {string} playerId - The GUID of the player.
* @param {number} count - The current combo count.
*/
function updateComboDisplay(playerId, count) {
    if (!gameState.localPlayerId) return;

    const isLocalPlayer = isSamePlayerId(playerId, gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const comboCountEl = document.getElementById(`${prefix}-combo-count`);

    if (comboCountEl) {
        const safeVal = toSafeCounterValue(count, 0);
        tweenIntegerText(comboCountEl, safeVal, {
            nonNegative: true
        });
        comboCountEl.closest('.combo-box')?.classList.toggle('is-zero', safeVal <= 0);
    }
}

/**
* Updates the rally display for a specific player.
* @param {string} playerId - The GUID of the player.
* @param {number} count - The current rally count.
*/
function updateRallyDisplay(playerId, count) {
    if (!gameState.localPlayerId) return;

    const isLocalPlayer = isSamePlayerId(playerId, gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const rallyCountEl = document.getElementById(`${prefix}-rally-count`);

    if (rallyCountEl) {
        const safeVal = toSafeCounterValue(count, 0);
        tweenIntegerText(rallyCountEl, safeVal, {
            nonNegative: true
        });
        rallyCountEl.closest('.rally-box')?.classList.toggle('is-zero', safeVal <= 0);
    }
}

/**
 * Updates the health display for a specific player's leader.
 * @param {object} unitInstance - The instance of the unit (leader or follower).
 * @param {number} currentHealth - The current health value of the leader.
 */
function updateHealthDisplay(unitInstance) {
    if (!unitInstance || !unitInstance.uid) {
        console.warn("UI Update Error: updateHealthDisplay called with invalid unitInstance.");
        return;
    }

    let healthElement;
    const rawHealth = Number(unitInstance.currentHealth);
    const hasNumericHealth = Number.isFinite(rawHealth);
    const nextHealthValue = hasNumericHealth ? Math.trunc(rawHealth) : null;
    const healthText = hasNumericHealth ? `${nextHealthValue}` : `${unitInstance.currentHealth ?? '??'}`;

    // 【重构】统一的生命值更新函数
    if (unitInstance.type === 'Leader') {
        // 根据ownerId和本地玩家ID，判断是player1还是player2
        const uiPlayerPrefix = (unitInstance.ownerId === gameState.localPlayerId) ? 'player1' : 'player2';
        if (uiPlayerPrefix === 'player2' && document.getElementById('player2-info')?.classList.contains('is-mirage-tavern')) {
            return;
        }
        const hpContainer = document.getElementById(`${uiPlayerPrefix}-hp`);

        if (hpContainer) {
            const maxHpRaw = Number(unitInstance.maxHealth ?? unitInstance.MaxHealth ?? unitInstance.health ?? unitInstance.Health);
            const maxHp = Number.isFinite(maxHpRaw) && maxHpRaw > 0 ? Math.trunc(maxHpRaw) : 20;
            const hpForBar = hasNumericHealth ? nextHealthValue : 0;
            const hpPercent = Math.max(0, Math.min(100, (hpForBar / maxHp) * 100));

            const valueEl = hpContainer.querySelector('.hp-value');
            const maxEl = hpContainer.querySelector('.hp-max');

            if (valueEl) {
                if (hasNumericHealth) {
                    tweenIntegerText(valueEl, nextHealthValue);
                } else {
                    valueEl.textContent = healthText;
                }
            }
            if (maxEl) {
                maxEl.textContent = `/ ${maxHp}`;
            }

            applyLeaderHpBarWithGhostSettle(hpContainer, hpPercent, hasNumericHealth && nextHealthValue <= 5);
            return; // Exit early for Leader
        }
    }

    // Follower or other field unit
    const unitElement = document.querySelector(`.field-card[data-uid="${unitInstance.uid}"]`);
    if (unitElement) {
        healthElement = unitElement.querySelector('.health');
    }

    if (healthElement) {
        if (hasNumericHealth) {
            tweenIntegerText(healthElement, nextHealthValue);
        } else {
            healthElement.textContent = healthText;
        }
    } else {
        // console.warn(`UI Update Error: Could not find health element for unit UID: ${unitInstance.uid} (Type: ${unitInstance.type})`);
    }
}

function updateUnitCombatStats(unitInstance) {
    if (!unitInstance || !unitInstance.uid) {
        console.warn("UI Update Error: updateUnitCombatStats called with invalid unitInstance.");
        return;
    }

    const normalizedType = String(unitInstance.type ?? unitInstance.Type ?? '').trim().toLowerCase();
    if (normalizedType === 'leader') {
        updateHealthDisplay(unitInstance);
        return;
    }

    const unitElement = document.querySelector(`.field-card[data-uid="${unitInstance.uid}"]`);
    if (!unitElement) {
        console.warn(`UI Update Error: Could not find field element for combat stat update UID: ${unitInstance.uid}`);
        return;
    }

    const costInfo = resolveCardCostDisplay(unitInstance, 'field');
    const costEl = unitElement.querySelector('.cost span');
    if (costEl) {
        const costNumber = Number(costInfo.displayCost);
        if (Number.isFinite(costNumber)) {
            tweenIntegerText(costEl, Math.max(0, Math.trunc(costNumber)), {
                nonNegative: true
            });
        } else {
            costEl.textContent = `${costInfo.displayCost ?? '?'}`;
        }
    }
    unitElement.classList.toggle('enhance-active', costInfo.enhanceActive === true);
        unitElement.classList.toggle('alternate-active', costInfo.modeVisual.alternate);
        unitElement.dataset.playModeLabel = costInfo.modeVisual.label;
        unitElement.style.setProperty('--play-mode-label', JSON.stringify(costInfo.modeVisual.label));

    const attackValueRaw = Number(unitInstance.currentAttack ?? unitInstance.CurrentAttack ?? unitInstance.attack ?? unitInstance.Attack);
    if (Number.isFinite(attackValueRaw)) {
        const attackEl = unitElement.querySelector('.attack');
        if (attackEl) {
            tweenIntegerText(attackEl, Math.trunc(attackValueRaw));
        }
    }

    updateHealthDisplay(unitInstance);
}

// --- Specific UI Update Functions (Continued) ---

/**
 * Updates the hand display for a specific player.
 * Clears the existing hand and renders the new cards.
 * @param {string} playerId - The GUID of the player.
 * @param {Array<object>} handCards - Array of card instance objects in the hand.
 */
function updateHandDisplay(playerId, handCards = []) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const revealOpponentHand = gameState.dualControl === true || gameState.isSpectator === true;
    const handElement = isLocalPlayer ? player1HandEl : player2HandEl;

    if (isLocalPlayer && gameState.phase !== 'Mulligan') {
        clearMulliganVisualState();
    }

    if (Array.isArray(handCards)) {
        updateHandCountDisplay(playerId, handCards.length);
    }

    if (!handElement) {
        const elName = isLocalPlayer ? 'player1HandEl' : 'player2HandEl';
        console.warn(`UI Update Error: ${elName} missing, cannot update hand display for player ${playerId}.`);
        return;
    }

    if (!Array.isArray(handCards)) {
        console.warn(`UI Update Warning: handCards for player ${playerId} is not an array.`);
        return;
    }

    if (isLocalPlayer && gameState.selectedCardUid) {
        const selectedUid = String(gameState.selectedCardUid);
        const selectedCardStillInHand = handCards.some(card => String(getCardUid(card) ?? '') === selectedUid);
        if (!selectedCardStillInHand) {
            setFloatingHandCard(null);
        }
    }

    const isFaceUpHand = isLocalPlayer || revealOpponentHand;
    if (isLocalPlayer) {
        applyHandOverlapCssVars(handElement, handCards.length);
    } else {
        handElement.classList.toggle('is-face-up-opponent', revealOpponentHand);
    }
    const entries = (!isLocalPlayer && !revealOpponentHand)
        ? buildFaceDownHandEntries(handCards)
        : buildFaceUpHandEntries(handCards, isLocalPlayer);
    renderAnimatedCardCollection(handElement, entries, {
        animateEnter: isFaceUpHand,
        animateExit: isFaceUpHand,
        animateInitialEnter: false,
        animateMove: isFaceUpHand
    });

    if (isLocalPlayer) {
        syncCommittedHandPlayVisual(handCards);
    }
}

function refreshHandPlayabilityVisuals(playerId, handCards = null) {
    const isLocalPlayer = isSamePlayerId(playerId, gameState.localPlayerId);
    const revealOpponentHand = gameState.dualControl === true || gameState.isSpectator === true;
    const handElement = isLocalPlayer ? player1HandEl : player2HandEl;

    // Opponent hand is hidden in normal mode; there is no face-up card DOM to update.
    if (!isLocalPlayer && !revealOpponentHand) {
        return;
    }
    if (!handElement) {
        return;
    }

    const sourceCards = Array.isArray(handCards)
        ? handCards
        : (Array.isArray(getPlayerState(playerId)?.hand) ? getPlayerState(playerId).hand : []);
    if (!Array.isArray(sourceCards) || sourceCards.length === 0) {
        return;
    }

    sourceCards.forEach(cardInstance => {
        const cardUid = getCardUid(cardInstance);
        if (!cardUid) return;

        const cardElement = handElement.querySelector(`.card[data-uid="${cardUid}"]`);
        if (!cardElement) return;

        const costInfo = resolveCardCostDisplay(cardInstance, 'hand');
        const costEl = cardElement.querySelector('.cost span');
        if (costEl) {
            costEl.textContent = `${costInfo.displayCost ?? '?'}`;
        }
        cardElement.classList.toggle('enhance-active', costInfo.enhanceActive === true);
        cardElement.classList.toggle('alternate-active', costInfo.modeVisual.alternate);
        cardElement.dataset.playModeLabel = costInfo.modeVisual.label;
        cardElement.style.setProperty('--play-mode-label', JSON.stringify(costInfo.modeVisual.label));
    });
}

/**
 * Shows the Mulligan interface elements for the local player.
 * Sets the game container class, updates the local player's hand display for mulligan,
 * and shows the confirm mulligan button.
 * Assumes this is called only for the local player receiving the MulliganStarted event.
 * @param {string} localPlayerId - The ID of the local player.
 * @param {Array<object>} handArray - The initial hand cards for the local player.
 */
function showMulliganInterface(localPlayerId, handArray) {

    // Ensure necessary elements are cached.
    // Assuming player1HandEl is always the local player's hand element based on HTML structure
    if (!gameContainerEl || !confirmMulliganButtonEl || !player1HandEl) {
        console.error("UI Error: Cannot show Mulligan interface - critical elements missing (gameContainer, confirmButton, player1Hand).");
        return;
    }

    // 1. Add class to game container to indicate Mulligan phase visually
    gameContainerEl.classList.add('mulligan-active');

    // Clear any stale selection from previous match/re-render.
    resetMulliganSelection();

    // 2. Update the local player's hand display (using existing function)
    updateHandDisplay(localPlayerId, handArray); // Render the actual cards for selection

    // 3. Add a class to the displayed hand cards to indicate they are selectable (for styling/feedback)
    player1HandEl.querySelectorAll('.card').forEach(cardEl => cardEl.classList.add('mulligan-selectable'));

    // 4. Show the Confirm Mulligan button
    confirmMulliganButtonEl.style.display = 'inline-block';
    confirmMulliganButtonEl.disabled = false; // Ensure it's enabled
    // Reset any stale state from previous matches (e.g., "等待对方..." after leaving a PVP room mid-mulligan)
    confirmMulliganButtonEl.textContent = '确认调度';

    // 5. Hide End Turn button during Mulligan
    if (endTurnButtonEl) {
        endTurnButtonEl.style.display = 'none';
        endTurnButtonEl.disabled = true;
    }

}


/**
 * Updates the UI to reflect a player's mulligan completion status.
 * Currently focuses on disabling the local player's confirm button and updating text.
 * @param {string} playerId - The ID of the player who completed mulligan.
 * @param {boolean} isComplete - Always true when called from the handler.
 */
function updateMulliganStatus(playerId, isComplete) {

    // Focus on local player's UI changes when they complete
    if (isSamePlayerId(playerId, gameState.localPlayerId) && isComplete) {
        if (confirmMulliganButtonEl) {
            confirmMulliganButtonEl.disabled = true;
            confirmMulliganButtonEl.textContent = '等待对方...'; // Change button text to indicate waiting
        } else {
            console.warn("UI Update Warning: Confirm Mulligan button element not found, cannot update status.");
        }
        // Optionally, add a class to indicate local completion if needed for styling
        // gameContainerEl?.classList.add('local-mulligan-complete');
    } else if (!isSamePlayerId(playerId, gameState.localPlayerId) && isComplete) {
        // Handle opponent completion if needed (e.g., hide a general "Waiting..." message if one exists)
        // Example: const waitingIndicator = document.getElementById('waiting-opponent-mulligan');
        // if (waitingIndicator) waitingIndicator.style.display = 'none';
    }
}

/**
 * Adds a single unit card element to the specified player's field.
 * Does not clear the existing field.
 * @param {string} playerId - The GUID of the player.
 * @param {object} cardInstance - The card instance object to add to the field.
 */
function addUnitToField(playerId, cardInstance) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const fieldElement = isLocalPlayer ? player1FieldEl : player2FieldEl;

    if (!fieldElement) {
        const elName = isLocalPlayer ? 'player1FieldEl' : 'player2FieldEl';
        console.warn(`UI Update Error: ${elName} missing, cannot add unit to field for player ${playerId}.`);
        return false;
    }
    if (!cardInstance) {
        console.warn(`UI Update Error: cardInstance is null or undefined in addUnitToField for player ${playerId}.`);
        return false;
    }

    const cardElement = createCardElement(cardInstance, 'field');
    if (cardElement) {
        const cardUid = getCardUid(cardInstance);
        if (cardUid) {
            cardElement.dataset.renderKey = `field-${cardUid}`;
        }
        fieldElement.appendChild(cardElement);
        syncFieldZoneDensity(fieldElement, fieldElement.querySelectorAll(':scope > .card').length);
        animateCardEnter(cardElement);
        return true;
    }

    return false;
}

// --- End Specific UI Update Functions ---

/**
 * Updates the display of a single unit on the field, typically after evolution or stat changes.
 * Finds the unit's DOM element and updates its stats, classes, etc.
 * @param {object} unitInstance - The updated card instance object for the unit.
 */
function updateUnitDisplay(unitInstance) {
    if (!unitInstance || !unitInstance.uid) {
        console.warn("UI Update Error: updateUnitDisplay called with invalid unitInstance:", unitInstance);
        return;
    }

    const unitElement = document.querySelector(`.field-card[data-uid="${unitInstance.uid}"]`);

    if (!unitElement) {
        console.warn(`UI Update Error: Could not find field element for unit UID: ${unitInstance.uid}`);
        return;
    }

    // Rebuild the full field card DOM so transforms can update artwork/name/type immediately.
    const replacement = createCardElement(unitInstance, 'field');
    if (!replacement) {
        console.warn(`UI Update Error: Failed to rebuild field element for unit UID: ${unitInstance.uid}`);
        return;
    }

    const transientClasses = [
        'selected-attacker',
        'targetable',
        'targetable-restricted',
        'targeting-spell',
        'targetable-spell',
        'choice-selected'
    ];

    for (const className of transientClasses) {
        if (unitElement.classList.contains(className)) {
            replacement.classList.add(className);
        }
    }
    clearTransientCardAnimationClasses(replacement);
    preserveCardStateClasses(unitElement, replacement);

    const unitUid = getCardUid(unitInstance);
    const prevRenderKey = unitElement.dataset.renderKey
        || (unitUid ? `field-${unitUid}` : replacement.dataset.renderKey || '');

    reconcileCardElementInPlace(unitElement, replacement, preserveCardStateClasses);
    unitElement.dataset.renderKey = prevRenderKey;
    if (unitUid) {
        getBattleMotionController().syncEntityElementVisibility(unitUid, unitElement);
    }
}

function refreshCardInstanceUi(cardInstance, handOwnersToRefresh = null, emblemOwnersToRefresh = null) {
    if (!cardInstance) return;

    const zone = typeof cardInstance.zone === 'string' ? cardInstance.zone.toLowerCase() : '';
    if (zone === 'hand') {
        if (cardInstance.ownerId && handOwnersToRefresh) {
            handOwnersToRefresh.add(cardInstance.ownerId);
        } else if (cardInstance.ownerId) {
            const player = getPlayerState(cardInstance.ownerId);
            if (player) {
                updateHandDisplay(player.id, player.hand);
            }
        }
        return;
    }

    if (zone === 'field') {
        updateUnitDisplay(cardInstance);
        updateHealthDisplay(cardInstance);
        return;
    }

    if (zone === 'emblem') {
        if (cardInstance.ownerId && emblemOwnersToRefresh) {
            emblemOwnersToRefresh.add(cardInstance.ownerId);
        } else if (cardInstance.ownerId) {
            const player = getPlayerState(cardInstance.ownerId);
            if (player) {
                updateEmblemZone(player);
            }
        }
        return;
    }

    if (cardInstance.type === 'Leader' || zone === 'leader') {
        updateHealthDisplay(cardInstance);
    }
}


/**
 * Removes a single unit card element from the field based on its UID.
 * @param {string} unitUid - The UID of the unit to remove.
 */
function removeUnitFromField(unitUid) {
    if (!unitUid) {
        console.warn("UI Update Error: removeUnitFromField called with no UID.");
        return;
    }

    const unitElement = gameContainerEl?.querySelector(`.field-card[data-uid="${unitUid}"]`);

    if (unitElement && unitElement.parentNode) {
        const parentField = unitElement.parentNode;
        const rect = unitElement.getBoundingClientRect();
        parentField.removeChild(unitElement);
        syncFieldZoneDensity(parentField, parentField.querySelectorAll?.(':scope > .card')?.length ?? 0);
        animateCardLeaveOverlay(unitElement, rect);
    } else if (!unitElement) {
        console.warn(`UI Update Error: Could not find field element for unit UID: ${unitUid} to remove.`);
    } else {
        // This case should be rare unless the element was already removed or detached.
        console.warn(`UI Update Warning: Found element for unit UID: ${unitUid}, but it has no parentNode to remove from.`);
    }
}
// --- Highlight Functions for Attack ---

/**
 * Adds the 'selected-attacker' class to the specified unit's DOM element.
 * @param {string} attackerUid - The UID of the unit initiating the attack.
 */
function highlightAttacker(attackerUid) {
    if (!attackerUid) {
        console.warn("UI Highlight Error: highlightAttacker called with no UID.");
        return;
    }
    const attackerElement = gameContainerEl?.querySelector(`.field-card[data-uid="${attackerUid}"]`);
    if (attackerElement) {
        attackerElement.classList.add('selected-attacker');
    } else {
        console.warn(`UI Highlights: Could not find element for attacker UID ${attackerUid} to highlight.`);
    }
}

/**
 * Highlights all valid attack targets for the given attacker instance.
 * Considers opponent's field, leader, and the Ward keyword.
 * @param {object} attackerInstance - The card instance object of the attacker.
 */
function highlightTargets(attackerInstance) {
    if (!attackerInstance || !attackerInstance.ownerId) {
        console.warn("UI Highlight Error: highlightTargets called with invalid attackerInstance:", attackerInstance);
        return;
    }

    if (attackerInstance.type !== "Follower") {
        console.warn(`UI Highlights: Attacker ${attackerInstance.uid} is not a follower (Type: ${attackerInstance.type}).`);
        return;
    }

    const attackerOwnerId = attackerInstance.ownerId;
    const opponentPlayerId = gameState.players.find(p => p.id !== attackerOwnerId)?.id;
    const opponentPlayerState = getPlayerState(opponentPlayerId);

    if (!opponentPlayerState) {
        console.warn(`UI Highlights: Could not find opponent player state for ID: ${opponentPlayerId} to highlight targets.`);
        return;
    }

    // --- Start of Attack Target Validation Logic ---
    const opponentBoard = (opponentPlayerState.field ?? []).filter(
        card => card
            && (equalsIgnoreCase(card.type, 'Follower') || equalsIgnoreCase(card.type, 'Follower_Evolved'))
    );
    const opponentLeader = opponentPlayerState.leaderCard;
    const attackerKeywords = Array.isArray(attackerInstance.keywords) ? attackerInstance.keywords : [];
    const attackerCannotAttackFollowers = attackerKeywords.some(keyword => equalsIgnoreCase(keyword, 'CannotAttackFollower'));
    const attackerCannotAttackLeader = attackerKeywords.some(keyword => equalsIgnoreCase(keyword, 'CannotAttackLeader'));
    const attackerIgnoresWard = attackerKeywords.some(keyword => equalsIgnoreCase(keyword, 'IgnoreWard'));
    const hasAmbushOrPressure = follower => {
        const keywords = Array.isArray(follower?.keywords) ? follower.keywords : [];
        const hasAmbush = keywords.some(keyword => equalsIgnoreCase(keyword, 'Ambush'));
        const hasPressure = keywords.some(keyword => equalsIgnoreCase(keyword, 'Pressure'));
        return hasAmbush || hasPressure;
    };
    const hasEffectiveWard = follower => {
        const keywords = Array.isArray(follower?.keywords) ? follower.keywords : [];
        const hasWard = keywords.some(keyword => equalsIgnoreCase(keyword, 'Ward'));
        const hasAmbush = keywords.some(keyword => equalsIgnoreCase(keyword, 'Ambush'));
        const hasPressure = keywords.some(keyword => equalsIgnoreCase(keyword, 'Pressure'));
        return hasWard && !hasAmbush && !hasPressure;
    };
    const attackableFollowers = opponentBoard.filter(f => !hasAmbushOrPressure(f));
    // 【BUG修复】在函数作用域内正确定义 hasWard 变量
    const hasWard = !attackerIgnoresWard && opponentBoard.some(follower => hasEffectiveWard(follower));
    const wardFollowers = opponentBoard.filter(f => hasEffectiveWard(f));

    let validTargets = [];

    if (!attackerIgnoresWard && wardFollowers.length > 0) {
        // If Ward is present, only Ward followers are valid targets
        validTargets = attackerCannotAttackFollowers ? [] : wardFollowers;
    } else {
        // If no Ward, all followers are valid targets
        validTargets = attackerCannotAttackFollowers ? [] : [...attackableFollowers];

        // Check if the leader is a valid target
        const hasStorm = attackerKeywords.some(keyword => equalsIgnoreCase(keyword, 'Storm'));
        const attackerZoneStayTurn = attackerInstance.zoneStayTurn ?? attackerInstance.ZoneStayTurn ?? 0;
        const isSummoningSick = attackerZoneStayTurn === 0;

        if (!attackerCannotAttackLeader && (hasStorm || !isSummoningSick)) {
            if (opponentLeader) {
                validTargets.push(opponentLeader);
            }
        }
    }

    const finalTargets = validTargets.filter(Boolean);
    // --- End of Attack Target Validation Logic ---

    finalTargets.forEach(targetInstance => {
        if (!targetInstance) return; // Skip null/undefined

        let targetElement = null;
        const isLeader = opponentPlayerState?.leaderCard && targetInstance.uid === opponentPlayerState.leaderCard.uid;

        if (isLeader) {
            // 【BUG-018修复】正确映射玩家ID到UI元素ID
            // opponentPlayerId是实际的GUID，需要映射到player1-avatar或player2-avatar
            let avatarId = null;

            // 方法1: 通过localPlayerId确定对手是player1还是player2
            if (gameState.localPlayerId) {
                // 如果对手不是本地玩家，且本地玩家通常映射到player1，则对手是player2
                // 但需要考虑实际的映射逻辑
                const localPlayer = gameState.players.find(p => p.id === gameState.localPlayerId);
                const opponentPlayer = gameState.players.find(p => p.id === opponentPlayerId);

                // 根据renderGame函数的逻辑：localPlayer映射到player1，opponent映射到player2
                if (opponentPlayer) {
                    avatarId = 'player2-avatar'; // 对手总是映射到player2
                }
            }

            // 方法2: 备用方案，通过IsAI属性判断
            if (!avatarId) {
                const opponentPlayer = gameState.players.find(p => p.id === opponentPlayerId);
                if (opponentPlayer?.isAI) {
                    avatarId = 'player2-avatar'; // AI通常是对手，映射到player2
                } else {
                    avatarId = 'player1-avatar'; // 非AI玩家映射到player1
                }
            }

            // 方法3: 最后的备用方案
            if (!avatarId) {
                avatarId = 'player2-avatar'; // 默认假设对手是player2
            }

            targetElement = document.getElementById(avatarId);
        } else {
            targetElement = gameContainerEl?.querySelector(`.card[data-uid="${targetInstance.uid}"]`);
        }

        if (targetElement) {
            // 【BUG-017修复】区分UI显示和实际攻击规则
            // UI中总是显示所有潜在目标，Ward规则在点击时验证
            let shouldHighlight = true; // 默认高亮所有潜在目标
            let cssClass = 'targetable';

            if (hasWard) {
                // 有Ward时，Ward单位正常高亮，非Ward目标用不同样式表示受限
                if (!isLeader && hasEffectiveWard(targetInstance)) {
                    // Ward单位：正常可攻击
                    cssClass = 'targetable';
                } else {
                    // 非Ward目标（包括主战者）：显示为受限目标
                    cssClass = 'targetable-restricted';
                }
            } else {
                // 没有Ward时，所有目标都正常可攻击
                cssClass = 'targetable';
            }

            if (shouldHighlight) {
                targetElement.classList.add(cssClass);
            }
        } else {
            console.warn(`%c[UI_HIGHLIGHT_TARGETS] Could not find target element for UID ${targetInstance.uid} (Is Leader: ${isLeader})`, 'color: orange;');
        }
    });
}

// --- UI State Variables ---

// --- NEW: Player Choice State Management ---
let isAwaitingPlayerChoice = false;
let currentChoiceRequest = null;
let currentChoiceSelectedTargetIds = new Set();
let currentChoiceSubmission = null;
let modeChoiceDialog = null;

function isCurrentChoiceRequest(pendingChoiceId, sourceAbilityRuntimeId) {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) {
        return false;
    }

    return String(currentChoiceRequest.PendingChoiceId) === String(pendingChoiceId)
        && String(currentChoiceRequest.SourceAbilityRuntimeId) === String(sourceAbilityRuntimeId);
}

function startPlayerChoice(requestData) {
    evolutionDrag.cancel();
    isAwaitingPlayerChoice = true;
    evolutionDrag.refresh();
    currentChoiceRequest = requestData;
    currentChoiceSubmission = null;
    currentChoiceSelectedTargetIds = new Set();
    gameContainerEl?.classList.add('player-choice-active');

    // BUG-FIX: Disable other actions while a choice is pending.
    if (endTurnButtonEl) {
        endTurnButtonEl.disabled = true;
    }
    // Potentially disable other buttons like evolve here as well if needed.
}

function endPlayerChoice(notifyBackendOfCancellation = true) {
    if (!isAwaitingPlayerChoice) return; // Prevent multiple calls

    const choiceToCancel = currentChoiceRequest; // Capture before clearing
    isAwaitingPlayerChoice = false;
    currentChoiceRequest = null;
    currentChoiceSubmission = null;
    currentChoiceSelectedTargetIds = new Set();

    clearSelectionHighlights();
    gameContainerEl?.classList.remove('player-choice-active');
    evolutionDrag.refresh();

    removeChoiceOverlayUI();

    // BUG-FIX: Notify the backend that the choice was cancelled, only if requested.
    if (notifyBackendOfCancellation && choiceToCancel && choiceToCancel.PendingChoiceId && choiceToCancel.SourceAbilityRuntimeId) {
        cancelPlayerChoice(choiceToCancel.PendingChoiceId, choiceToCancel.SourceAbilityRuntimeId);
    } else if (notifyBackendOfCancellation) {
        console.warn("UI: endPlayerChoice was called to notify backend, but there was no active choice request to cancel.");
    }

    if (notifyBackendOfCancellation) {
        cancelCommittedHandPlayVisual(choiceToCancel?.SourceCardUid ?? null);
    }

    // BUG-FIX: Restore the state of the end turn button.
    if (endTurnButtonEl) {
        const isLocalPlayerTurn = gameState.localPlayerId === gameState.currentPlayerId;
        // Only enable if it's the local player's turn and the game is in a phase where ending the turn is possible.
        endTurnButtonEl.disabled = !isLocalPlayerTurn || (gameState.phase !== 'Main' && gameState.phase !== 'TurnStart');
    }

    // Re-apply normal highlights after exiting choice mode.
    renderHighlights(gameState);
}
// --- End UI State Management Functions ---

let restoreDiscoverPreview = null;
let cleanupDiscoverBoardView = null;
function removeChoiceOverlayUI() {
    modeChoiceDialog?.destroy(); modeChoiceDialog = null;
    cleanupDiscoverBoardView?.(); cleanupDiscoverBoardView = null;
    restoreDiscoverPreview?.(); restoreDiscoverPreview = null;
    document.getElementById('choice-overlay-root')?.remove();
    // Backward compatibility cleanup (older prompt overlay).
    document.getElementById('choice-prompt-overlay')?.remove();
}

function getChoiceMinMax(choiceRequest) {
    const minRaw = choiceRequest?.MinSelectionCount ?? choiceRequest?.minSelectionCount;
    const maxRaw = choiceRequest?.MaxSelectionCount ?? choiceRequest?.maxSelectionCount;
    const minNumeric = Number(minRaw);
    const maxNumeric = Number(maxRaw);
    const min = Number.isFinite(minNumeric) ? Math.max(0, Math.trunc(minNumeric)) : 1;
    const maxCandidate = Number.isFinite(maxNumeric) ? Math.max(0, Math.trunc(maxNumeric)) : 1;
    const max = Math.max(min, maxCandidate);
    return { min, max };
}

function isChoiceSelectionValid(choiceRequest, selectedCount) {
    const { min, max } = getChoiceMinMax(choiceRequest);
    return selectedCount >= min && selectedCount <= max;
}

function isFixedChoiceCount(choiceRequest) {
    const { min, max } = getChoiceMinMax(choiceRequest);
    return min === max;
}

function getChoiceCardOwnerId(cardInstance) {
    return cardInstance?.ownerId ?? cardInstance?.OwnerPlayerId ?? null;
}

function getChoiceCardKeywords(cardInstance) {
    const keywords = cardInstance?.keywords ?? cardInstance?.Keywords ?? [];
    return Array.isArray(keywords) ? keywords : [];
}

function hasChoiceCardKeyword(cardInstance, keyword) {
    return getChoiceCardKeywords(cardInstance).some(k => equalsIgnoreCase(k, keyword));
}

function getActiveSpellWardChoiceTargetIds() {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return new Set();

    const localPlayerId = gameState.localPlayerId;
    const activeIds = new Set();
    const selectableIds = currentChoiceRequest.SelectableTargetIds || [];

    selectableIds.forEach(id => {
        const card = findCardInstanceByUid(id);
        if (!card) return;
        const ownerId = getChoiceCardOwnerId(card);
        const isOpponentCard = ownerId && String(ownerId) !== String(localPlayerId);
        if (!isOpponentCard) return;
        if (hasChoiceCardKeyword(card, 'SpellWard') && !hasChoiceCardKeyword(card, 'Aura')) {
            activeIds.add(String(id));
        }
    });

    return activeIds;
}

function canSelectChoiceTarget(targetUid) {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return false;

    const targetKey = String(targetUid);
    const validTargetUids = new Set((currentChoiceRequest.SelectableTargetIds || []).map(x => String(x)));
    if (!validTargetUids.has(targetKey)) return false;

    // Always allow toggling already selected target.
    if (currentChoiceSelectedTargetIds.has(targetKey)) return true;

    const card = findCardInstanceByUid(targetUid);
    if (!card) return true;

    const localPlayerId = gameState.localPlayerId;
    const ownerId = getChoiceCardOwnerId(card);
    const isOpponentCard = ownerId && String(ownerId) !== String(localPlayerId);
    if (!isOpponentCard) return true;

    // SpellWard itself is always selectable.
    if (hasChoiceCardKeyword(card, 'SpellWard') && !hasChoiceCardKeyword(card, 'Aura')) {
        return true;
    }

    const activeSpellWardIds = getActiveSpellWardChoiceTargetIds();
    if (activeSpellWardIds.size === 0) return true;

    // Opponent non-SpellWard targets stay blocked until all active SpellWard targets are selected.
    for (const spellWardId of activeSpellWardIds) {
        if (!currentChoiceSelectedTargetIds.has(spellWardId)) {
            return false;
        }
    }

    return true;
}

function formatChoiceCountLabel(selectedCount, min, max) {
    return min === max ? `${selectedCount} / ${max}` : `${selectedCount} / ${min}-${max}`;
}

function updateChoiceOverlayUI() {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return;

    if (modeChoiceDialog) {
        modeChoiceDialog.update({ selectedIds: currentChoiceSelectedTargetIds, pending: currentChoiceSubmission === currentChoiceRequest });
        return;
    }

    const { min, max } = getChoiceMinMax(currentChoiceRequest);
    const selectedCount = currentChoiceSelectedTargetIds.size;
    const isValid = isChoiceSelectionValid(currentChoiceRequest, selectedCount);

    const countEl = document.getElementById('choice-selected-count');
    if (countEl) {
        countEl.textContent = formatChoiceCountLabel(selectedCount, min, max);
    }

    const controlsEl = document.querySelector('#choice-overlay-root .choice-controls-overlay');
    if (controlsEl) {
        controlsEl.classList.toggle('is-satisfied', isValid);
    }

    const confirmBtn = document.getElementById('choice-confirm-btn');
    if (confirmBtn) {
        confirmBtn.disabled = !isValid;
    }

    const cancelBtn = document.getElementById('choice-cancel-btn');
    if (cancelBtn) {
        cancelBtn.disabled = currentChoiceRequest.IsCancellable !== true;
        cancelBtn.style.display = currentChoiceRequest.IsCancellable === true ? '' : 'none';
    }

    // Update selected visuals in the choice panel, if present.
    document.querySelectorAll('.choice-card[data-uid]').forEach(el => {
        const uid = el.dataset.uid;
        if (!uid) return;
        const selected = currentChoiceSelectedTargetIds.has(uid);
        const selectableNow = canSelectChoiceTarget(uid);
        el.classList.toggle('choice-card-selected', selected);
        el.classList.toggle('choice-card-restricted', !selected && !selectableNow);
        if (!selected && !selectableNow) {
            el.setAttribute('disabled', 'disabled');
        } else {
            el.removeAttribute('disabled');
        }
    });
}

async function confirmPlayerChoice() {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return;
    if (currentChoiceSubmission === currentChoiceRequest) return;
    const request = currentChoiceRequest;
    const { PendingChoiceId, SourceAbilityRuntimeId } = currentChoiceRequest;

    const chosenIds = Array.from(currentChoiceSelectedTargetIds);
    if (!isChoiceSelectionValid(currentChoiceRequest, chosenIds.length)) return;

    currentChoiceSubmission = request;
    updateChoiceOverlayUI();
    try {
        const ok = await submitPlayerChoice(PendingChoiceId, chosenIds, SourceAbilityRuntimeId, false);
        if (ok && isCurrentChoiceRequest(PendingChoiceId, SourceAbilityRuntimeId)) {
            endPlayerChoice(false);
        }
    } finally {
        if (currentChoiceSubmission === request) currentChoiceSubmission = null;
        if (currentChoiceRequest === request) updateChoiceOverlayUI();
    }
}

function requestCancelPlayerChoiceUI() {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return;
    if (currentChoiceSubmission === currentChoiceRequest) return;
    if (currentChoiceRequest.IsCancellable !== true) {
        console.warn("UI: Player choice is not cancellable. Ignoring cancel request.");
        return;
    }
    endPlayerChoice(true);
}

async function handleChoiceTargetClick(targetUid) {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return;
    if (currentChoiceSubmission === currentChoiceRequest) return;

    const validTargetUids = new Set((currentChoiceRequest.SelectableTargetIds || []).map(x => String(x)));
    const key = String(targetUid);
    if (!validTargetUids.has(key)) return;
    if (!currentChoiceSelectedTargetIds.has(key) && !canSelectChoiceTarget(targetUid)) return;

    const { max } = getChoiceMinMax(currentChoiceRequest);
    const fixedChoiceCount = isFixedChoiceCount(currentChoiceRequest);
    if (currentChoiceRequest.TargetKey === 'decision') {
        if (max > 1 && currentChoiceSelectedTargetIds.size >= max && !currentChoiceSelectedTargetIds.has(key)) {
            modeChoiceDialog?.showLimitHint();
            return;
        }
        currentChoiceSelectedTargetIds = toggleModeSelection(currentChoiceSelectedTargetIds, key, max);
    } else if (currentChoiceSelectedTargetIds.has(key)) {
        currentChoiceSelectedTargetIds.delete(key);
    } else {
        if (currentChoiceSelectedTargetIds.size >= max) return;
        currentChoiceSelectedTargetIds.add(key);
    }

    // Refresh highlights so selected targets remain visually consistent across zones.
    renderHighlights(gameState);
    updateChoiceOverlayUI();

    if (!fixedChoiceCount || !shouldAutoConfirmChoice(currentChoiceRequest, currentChoiceSelectedTargetIds.size, readOperationSettings())) {
        return;
    }

    await confirmPlayerChoice();
}

// --- Module-level variable to store the latest known current player ID ---
let uiCurrentPlayerId = null;

// --- DOM Element References (缓存以提高性能) ---
// 变量将在 cacheDOMElements 中定义和赋值
let player1HandEl, player1FieldEl, player1HpEl, player1PpEl, player1MaxPpEl, player1EpEl, player1SepEl, player1DeckEl, player1GraveEl;
let player2HandEl, player2FieldEl, player2HpEl, player2PpEl, player2MaxPpEl, player2EpEl, player2SepEl, player2DeckEl, player2GraveEl;
let player1MaxEpEl, player2MaxEpEl;
let player1MaxSepEl, player2MaxSepEl;
let turnInfoEl, endTurnButtonEl, confirmMulliganButtonEl, gameContainerEl; // Added confirmMulliganButtonEl
let spectatorToggleViewButtonEl;
let battleCardPreviewEl;
let endTurnCountdownEl;
let endTurnIndicatorLights = [];
let reconnectButtonEl;
let isConnectionLocked = false;
let opponentDisconnected = false;
let connectionStatus = 'connected';
let connectionStatusMessage = '';

// 缓存 DOM 元素的函数
function cacheDOMElements() {
    try {
        gameContainerEl = document.getElementById('game-container');
        turnInfoEl = document.getElementById('turn-info');
        endTurnButtonEl = document.getElementById('end-turn-button');
        endTurnCountdownEl = endTurnButtonEl?.querySelector('.end-turn-countdown') || null;
        endTurnIndicatorLights = Array.from(endTurnButtonEl?.querySelectorAll('.end-turn-indicator-light') || []);
        confirmMulliganButtonEl = document.getElementById('confirm-mulligan-button'); // Cache the new button
        spectatorToggleViewButtonEl = document.getElementById('spectator-toggle-view');
        battleCardPreviewEl = document.getElementById('battle-card-preview');
        reconnectButtonEl = document.getElementById('reconnect-button');

        // Player 1 elements
        player1HandEl = document.getElementById('player1-hand');
        player1FieldEl = document.getElementById('player1-field');
        player1HpEl = document.getElementById('player1-hp');
        // Updated IDs to match new HTML structure
        player1PpEl = document.getElementById('player1-pp-bar');
        player1EpEl = document.getElementById('player1-ep-dots');
        player1SepEl = document.getElementById('player1-sep-dots');
        player1DeckEl = document.getElementById('player1-deck-count');
        player1GraveEl = document.getElementById('player1-grave-count');

        // Player 2 elements
        player2HandEl = document.getElementById('player2-hand'); // Often hidden/simplified
        player2FieldEl = document.getElementById('player2-field');
        player2HpEl = document.getElementById('player2-hp');
        // Updated IDs to match new HTML structure
        player2PpEl = document.getElementById('player2-pp-bar');
        player2EpEl = document.getElementById('player2-ep-dots');
        player2SepEl = document.getElementById('player2-sep-dots');
        player2DeckEl = document.getElementById('player2-deck-count');
        player2GraveEl = document.getElementById('player2-grave-count');

        // Verification
        const elementsToVerify = {
            gameContainerEl, turnInfoEl, endTurnButtonEl, confirmMulliganButtonEl, reconnectButtonEl, // Added confirmMulliganButtonEl
            player1HandEl, player1FieldEl, player1HpEl, player1PpEl, player1MaxPpEl, player1EpEl, player1MaxEpEl, player1SepEl, player1MaxSepEl, player1DeckEl, player1GraveEl,
            player2HandEl, player2FieldEl, player2HpEl, player2PpEl, player2MaxPpEl, player2EpEl, player2MaxEpEl, player2SepEl, player2MaxSepEl, player2DeckEl, player2GraveEl,
            battleCardPreviewEl
        };
        let missing = [];
        for (const key in elementsToVerify) {
            if (!elementsToVerify[key]) {
                // Derive ID from variable name (e.g., player1HandEl -> player1-hand)
                const id = key.replace(/([A-Z])/g, '-$1').toLowerCase().replace('-el', '');
                missing.push(id);
            }
        }
        if (missing.length > 0) {
            // console.error(`UI Cache Error: Missing critical elements with IDs: ${missing.join(', ')}. Check index.html.`);
        } else {
        }
    } catch (error) {
        // console.error("UI Error during DOM element caching:", error);
    }
}

// --- 初始化 ---
// 模块加载时尝试缓存 DOM，并设置基础事件监听器
// [修正] 删除了此处重复的 initializeUI 函数定义。
// 正确的定义在文件后面，并通过 DOMContentLoaded 调用。

// 确保 DOM 加载完毕后再初始化 UI
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeUI);
} else {
    // DOM is already loaded
    initializeUI();
}

// --- 后续将逐步添加 renderGame, 渲染辅助函数, 事件处理函数等 ---
// --- 主渲染函数 ---
/**
 * Renders the entire game UI based on the provided game state.
 * @param {object} currentGameState - The current game state object from gameManager.
 */
function renderGame(currentGameState) { // Remove second parameter
    // Get currentPlayerId directly from the gameState object
    const latestCurrentPlayerId = currentGameState?.currentPlayerId; // Use optional chaining for safety

    // Update the module-level variable
    uiCurrentPlayerId = latestCurrentPlayerId;
    if (!currentGameState || !currentGameState.players || currentGameState.players.length < 2) {
        // 游戏状态无效，可能在初始化阶段，或者出错
        console.warn(`UI: renderGame called with invalid or incomplete gameState.`);
        // 可以考虑显示“加载中”或错误信息
        return;
    }

    if (currentGameState.phase !== 'Mulligan') {
        clearMulliganVisualState();
    }

    if (gameContainerEl) {
        const mirageActive = isMirageMode(currentGameState);
        gameContainerEl.dataset.gameMode = mirageActive ? 'Mirage' : 'Standard';
        gameContainerEl.dataset.miragePhase = mirageActive ? String(currentGameState.phase || 'Recruit') : '';
    }

    if (spectatorToggleViewButtonEl) {
        if (gameState.isSpectator) {
            spectatorToggleViewButtonEl.style.display = 'inline-block';
            spectatorToggleViewButtonEl.textContent = '切换视角';
            spectatorToggleViewButtonEl.title = `Toggle perspective (bottom player: ${gameState.localPlayerId || 'unknown'})`;
        } else {
            spectatorToggleViewButtonEl.style.display = 'none';
        }
    }

    if (endTurnButtonEl) {
        endTurnButtonEl.style.display = gameState.isSpectator ? 'none' : '';
    }

    // Use dynamic player IDs instead of hardcoded 'player1' and 'player2'
    // Get the current player (activePlayer) and opponent player (opponentPlayer)
    const currentPlayer = currentGameState.activePlayer;
    const opponentPlayer = currentGameState.opponentPlayer;

    if (!currentPlayer || !opponentPlayer) {
        console.error(`UI Error: Could not find activePlayer or opponentPlayer object in renderGame.`);
        return;
    }

    // 【BUG修复】强化本地玩家vs对手的识别逻辑，防止敌方手牌显示为UnknownCard
    // 确保UI布局一致性：本地玩家在底部(player1)，对手在顶部(player2)
    let localPlayer = null;
    let opponentPlayerState = null;

    // 双控的下方席位跟随当前操控方；观战仍使用服务端指定的视角。
    const primarySeatId = currentGameState.dualControl && !currentGameState.isSpectator
        ? currentGameState.localPlayerId
        : currentGameState.viewingPlayerId || currentGameState.localPlayerId;
    if (primarySeatId) {
        const normalizedSeatId = String(primarySeatId).trim().toLowerCase();
        localPlayer = currentGameState.players.find(p => String(p?.id ?? '').trim().toLowerCase() === normalizedSeatId);
        opponentPlayerState = currentGameState.players.find(p => p !== localPlayer);
    }

    // 方法2: 如果localPlayerId未设置或查找失败，仅在“人类 vs AI”场景下使用 IsAI 属性进行区分
    if (!localPlayer || !opponentPlayerState) {
        const aiPlayers = currentGameState.players.filter(p => p?.isAI === true);
        const nonAiPlayers = currentGameState.players.filter(p => p?.isAI !== true);

        if (aiPlayers.length === 1 && nonAiPlayers.length === 1) {
            console.warn(`[BUG修复] localPlayerId查找失败，推断为人类 vs AI，对齐到人类玩家`);
            localPlayer = nonAiPlayers[0];
            opponentPlayerState = aiPlayers[0];
        } else {
            console.error(`[PVP] localPlayerId missing or mismatch; cannot safely infer seat.`, {
                localPlayerId: gameState.localPlayerId,
                players: currentGameState.players.map(p => ({ id: p?.id, isAI: p?.isAI }))
            });
            return;
        }
    }

    // 方法3: 最后的备用方案，使用数组索引（假设第一个是本地玩家）
    if (!localPlayer || !opponentPlayerState) {
        console.warn(`[BUG修复] IsAI属性查找失败，使用数组索引备用方案`);
        localPlayer = currentGameState.players[0];
        opponentPlayerState = currentGameState.players[1];
    }

    const player1 = localPlayer;     // 本地玩家（屏幕底部）
    const player2 = opponentPlayerState;  // 对手玩家（屏幕顶部）

    const isMulliganPhase = !isMirageMode(currentGameState)
        && currentGameState.phase === 'Mulligan'
        && currentGameState.isSpectator !== true;
    gameContainerEl?.classList.toggle('mulligan-active', isMulliganPhase);

    // Call helper functions to render specific parts of the UI using game state and cached elements

    const isMirageRecruitHud = isMirageMode(currentGameState) && currentGameState.phase === 'Recruit';

    // 1. Render Player Stats (In Mirage Recruit phase, #player2-info renders as the Neutral Tavern HUD)
    renderPlayerStats(player1, 'player1');
    if (isMirageRecruitHud) {
        renderMirageNeutralTavernHud(currentGameState, player1);
    } else {
        clearMirageNeutralTavernHud();
        renderPlayerStats(player2, 'player2');
    }

    // 2. Render Hands (Ensure hand array exists, fallback to empty array)
    // Always render local player's hand with full details at the bottom
    renderPlayerHand(player1?.hand ?? [], player1HandEl);
    // Render opponent hand with faces in dual-control or spectator mode, otherwise use card backs
    if (currentGameState.dualControl || currentGameState.isSpectator) {
        renderPlayerHand(player2?.hand ?? [], player2HandEl);
    } else {
        renderOpponentHand(player2?.hand ?? [], player2HandEl);
    }

    // 3. Render Fields (Ensure field array exists)
    renderField(player1.field ?? [], player1FieldEl);
    renderField(player2.field ?? [], player2FieldEl, true); // isOpponent = true

    // 4. Render Turn Info
    renderTurnInfo(currentGameState.activePlayer?.turnNumber, currentGameState.currentPlayerId); // Use active player's turn count

    // 4.5 Render Game Log
    renderGameLog(currentGameState.gameLog);

    // 5. Render Highlights (Playable cards, selected attackers, targets)
    renderHighlights(currentGameState);

    // 6. Render Action Buttons based on Game Phase
    renderActionButtons(currentGameState);
    renderMirageShopPanel(currentGameState, player1);
    renderMirage8PHudAndModal(currentGameState, player1);

    const activePreviewUid = battleCardPreviewEl?.dataset?.previewUid;
    if (activePreviewUid) {
        const refreshedPreviewCard = findCardInstanceByUid(activePreviewUid);
        renderBattleCardPreview(refreshedPreviewCard ?? null);
    }

}

function syncMirageRightDockUpperTier(currentGameState, _localPlayer) {
    const upperPpBox = document.getElementById('right-dock-upper-pp');
    const barEl = document.getElementById('player2-pp-bar');
    if (!upperPpBox || !barEl) return;

    if (!isMirageMode(currentGameState)) {
        upperPpBox.classList.remove('is-gold');
        barEl.classList.remove('is-six');
    }
}

function renderMirageShopPanel(currentGameState, localPlayer) {
    if (!gameContainerEl) return;
    const upperFieldZone = document.getElementById('upper-field-zone');
    const upperZoneTag = document.getElementById('upper-zone-tag');
    const lowerZoneTag = document.getElementById('lower-zone-tag');
    const mirageSubActions = document.getElementById('mirage-dock-sub-actions');
    let stageEl = document.getElementById('mirage-tavern-stage');
    let drawerEl = document.getElementById('mirage-decision-drawer');

    const mirageActive = isMirageMode(currentGameState);
    if (mirageActive) {
        syncMirageRightDockUpperTier(currentGameState, localPlayer);
    }

    if (!mirageActive || currentGameState.phase !== 'Recruit') {
        if (stageEl) stageEl.style.display = 'none';
        if (drawerEl) drawerEl.style.display = 'none';
        if (upperFieldZone) upperFieldZone.classList.remove('is-tavern', 'is-frozen');
        if (mirageSubActions) {
            mirageSubActions.hidden = true;
            mirageSubActions.innerHTML = '';
        }
        if (upperZoneTag) {
            upperZoneTag.textContent = mirageActive ? 'OPPONENT WARBAND' : '';
        }
        if (lowerZoneTag) {
            lowerZoneTag.textContent = mirageActive ? 'PLAYER WARBAND' : '';
        }
        return;
    }

    const stageHost = upperFieldZone ?? gameContainerEl;
    if (!stageEl) {
        stageEl = document.createElement('div');
        stageEl.id = 'mirage-tavern-stage';
        stageEl.className = 'mirage-tavern-stage';
        stageHost.appendChild(stageEl);
    } else if (stageEl.parentElement !== stageHost) {
        stageHost.appendChild(stageEl);
    }
    if (!drawerEl) {
        drawerEl = document.createElement('div');
        drawerEl.id = 'mirage-decision-drawer';
        drawerEl.className = 'mirage-decision-drawer';
        gameContainerEl.appendChild(drawerEl);
    }
    stageEl.style.display = 'flex';

    const miragePlayer = localPlayer?.mirage ?? {};
    const roundNumber = Number(currentGameState.mirage?.roundNumber ?? currentGameState.mirage?.RoundNumber ?? 1);
    const tavernTier = Number(miragePlayer.tavernTier ?? miragePlayer.TavernTier ?? 1);
    const maxTavernTier = Number(miragePlayer.maxTavernTier ?? miragePlayer.MaxTavernTier ?? 6);
    const upgradeCost = Number(miragePlayer.upgradeCost ?? miragePlayer.UpgradeCost ?? 0);
    const refreshCost = Number(miragePlayer.refreshCost ?? miragePlayer.RefreshCost ?? 1);
    const isShopFrozen = Boolean(miragePlayer.isShopFrozen ?? miragePlayer.IsShopFrozen);
    const isReady = Boolean(miragePlayer.isReady ?? miragePlayer.IsReady);
    const canUpgradeTier = Boolean(miragePlayer.canUpgradeTier ?? miragePlayer.CanUpgradeTier);
    const hasSelectedLeader = Boolean(miragePlayer.hasSelectedLeader ?? miragePlayer.HasSelectedLeader ?? true);
    const currentPp = Number(localPlayer?.pp ?? 0);
    const isSpectator = currentGameState.isSpectator === true;
    const isHandFull = (Array.isArray(localPlayer?.hand) ? localPlayer.hand.length : 0) >= 9;
    const offers = Array.isArray(miragePlayer.shopOffers ?? miragePlayer.ShopOffers)
        ? (miragePlayer.shopOffers ?? miragePlayer.ShopOffers)
        : [];
    const pendingTickets = Array.isArray(miragePlayer.pendingTickets ?? miragePlayer.PendingTickets)
        ? (miragePlayer.pendingTickets ?? miragePlayer.PendingTickets)
        : [];
    const leaderCandidates = Array.isArray(miragePlayer.leaderCandidates ?? miragePlayer.LeaderCandidates)
        ? (miragePlayer.leaderCandidates ?? miragePlayer.LeaderCandidates)
        : [];

    const canRefresh = !isSpectator && !isReady && currentPp >= refreshCost;
    const canFreeze = !isSpectator && !isReady;
    const canUpgrade = !isSpectator && !isReady && canUpgradeTier && tavernTier < maxTavernTier;

    if (upperFieldZone) {
        upperFieldZone.classList.add('is-tavern');
        upperFieldZone.classList.toggle('is-frozen', isShopFrozen);
    }
    if (upperZoneTag) {
        const shopCount = offers.length;
        upperZoneTag.textContent = `TAVERN · ${shopCount}`;
    }
    if (lowerZoneTag) {
        const warbandCount = Array.isArray(localPlayer?.field) ? localPlayer.field.length : 0;
        lowerZoneTag.textContent = `WARBAND · ${warbandCount}`;
    }

    if (mirageSubActions) {
        mirageSubActions.hidden = false;
        mirageSubActions.innerHTML = `
            <button type="button" class="mirage-sub-console-btn is-amber" data-mirage-action="refresh" ${canRefresh ? '' : 'disabled'}>
                <span>刷新 ${refreshCost}G</span>
            </button>
            <button type="button" class="mirage-sub-console-btn is-ice ${isShopFrozen ? 'is-active' : ''}" data-mirage-action="freeze" ${canFreeze ? '' : 'disabled'}>
                <span>${isShopFrozen ? '已冻结' : '冻结'}</span>
            </button>
        `;
    }

    const offersHtml = offers.length === 0
        ? `<div class="mirage-tavern-empty">本轮星界酒馆暂无可招募随从</div>`
        : offers.map(offer => {
            const offerId = escapeAttributeValue(offer.offerId ?? offer.OfferId ?? '');
            const name = escapeAttributeValue(offer.name ?? offer.Name ?? '未知随从');
            const desc = escapeAttributeValue(offer.description ?? offer.Description ?? '');
            const artPreview = escapeAttributeValue(offer.artPreview ?? offer.ArtPreview ?? '');
            const tier = Number(offer.tavernTier ?? offer.TavernTier ?? 1);
            const atk = Number(offer.attack ?? offer.Attack ?? 0);
            const hp = Number(offer.health ?? offer.Health ?? 0);
            const buyCost = Number(offer.buyCost ?? offer.BuyCost ?? 3);
            const rawPoolCopies = offer.remainingPoolCopies ?? offer.RemainingPoolCopies;
            const hasPoolCopies = rawPoolCopies !== null && rawPoolCopies !== undefined && Number.isFinite(Number(rawPoolCopies));
            const poolCopiesBadge = hasPoolCopies ? ` · 池余${Number(rawPoolCopies)}` : '';
            const canBuy = !isSpectator && !isReady && !isHandFull && currentPp >= buyCost;
            const artStyle = artPreview ? `background-image:url('${artPreview}');` : '';
            const tierBadgeInner = buildMirageTierBadgeInnerHtml(tier);
            return `<div class="mirage-offer-card mirage-shop-offer-card ${isShopFrozen ? 'is-frozen' : ''}" data-offer-id="${offerId}" title="${escapeAttributeValue(`${desc || name}${poolCopiesBadge}`)}">
                <div class="mirage-offer-art" style="${artStyle}"></div>
                <div class="mirage-offer-top">
                    <span class="mirage-offer-tier ${tier >= 7 ? 'is-mythic' : ''}" data-tier="${tier}" title="酒馆星级 ★${tier}">${tierBadgeInner}</span>
                    <span class="mirage-offer-name">${name}</span>
                    <button type="button" class="mirage-offer-cost mirage-buy-offer-btn" data-mirage-buy-offer="${offerId}" ${canBuy ? '' : 'disabled'} title="${canBuy ? `点击招募 (${buyCost} 金币)` : `招募需要 ${buyCost} 金币`}">${buyCost}</button>
                </div>
                ${desc ? `<div class="mirage-offer-desc">${desc}</div>` : ''}
                <button type="button" class="mirage-offer-hover-cta" data-mirage-buy-offer="${offerId}" ${canBuy ? '' : 'disabled'}>招募</button>
                <div class="mirage-offer-stats-row">
                    <span class="mirage-offer-stat mirage-offer-atk" title="攻击力">${atk}</span>
                    <span class="mirage-offer-stat mirage-offer-hp" title="生命值">${hp}</span>
                </div>
            </div>`;
        }).join('');

    stageEl.innerHTML = `<div class="mirage-tavern-offers mirage-shop-offers">${offersHtml}</div>`;
    void canUpgrade;
    void upgradeCost;

    const needsLeaderSelection = !hasSelectedLeader && leaderCandidates.length > 0 && roundNumber === 1;
    if (needsLeaderSelection) {
        const canSelectLeader = !isSpectator && !isReady;
        const fallbackCrest = CLASS_ICON_URL_MAP.Neutral || '';
        drawerEl.style.display = 'block';
        drawerEl.innerHTML = `
            <div class="mirage-drawer-header">
                <div class="mirage-drawer-title-group">
                    <span class="mirage-drawer-kicker">COMMANDER SELECTION</span>
                    <span class="mirage-drawer-title">选择幻境主战者</span>
                </div>
                <span class="mirage-drawer-subtitle">决定本局专属统帅技能与初始生命上限</span>
            </div>
            <div class="mirage-drawer-grid">
                ${leaderCandidates.map(cand => {
                    const defId = Number(cand.cardDefinitionId ?? cand.CardDefinitionId ?? 0);
                    const cName = escapeAttributeValue(cand.name ?? cand.Name ?? `#${defId}`);
                    const cDesc = escapeAttributeValue(cand.description ?? cand.Description ?? '');
                    const cHp = Number(cand.health ?? cand.Health ?? 20);
                    const cArt = escapeAttributeValue(cand.artPreview ?? cand.ArtPreview ?? '');
                    const artImgHtml = cArt
                        ? `<img class="mirage-choice-avatar-img" src="${cArt}" alt="" onerror="this.remove()">`
                        : '';
                    const fallbackImgHtml = fallbackCrest
                        ? `<img class="mirage-choice-avatar-fallback" src="${fallbackCrest}" alt="" aria-hidden="true">`
                        : '';
                    return `<button type="button" class="mirage-choice-card" data-mirage-select-leader="${defId}" ${canSelectLeader ? '' : 'disabled'}>
                        <div class="mirage-choice-card-top">
                            <div class="mirage-choice-avatar">${fallbackImgHtml}${artImgHtml}</div>
                            <div class="mirage-choice-meta">
                                <div class="mirage-choice-name">${cName}</div>
                                <div class="mirage-choice-stats">${cHp} HP · COMMANDER</div>
                            </div>
                        </div>
                        <div class="mirage-choice-desc">${cDesc || '无技能说明'}</div>
                        <span class="mirage-choice-cta">选定主战者</span>
                    </button>`;
                }).join('')}
            </div>
        `;
    } else if (pendingTickets.length > 0) {
        const activeTicket = pendingTickets[0];
        const ticketId = escapeAttributeValue(activeTicket.ticketId ?? activeTicket.TicketId ?? '');
        const targetTier = Number(activeTicket.targetTier ?? activeTicket.TargetTier ?? 1);
        const candidates = Array.isArray(activeTicket.candidates ?? activeTicket.Candidates)
            ? (activeTicket.candidates ?? activeTicket.Candidates)
            : (Array.isArray(activeTicket.candidateCardDefinitionIds ?? activeTicket.CandidateCardDefinitionIds)
                ? (activeTicket.candidateCardDefinitionIds ?? activeTicket.CandidateCardDefinitionIds).map(id => ({ cardDefinitionId: id, name: `#${id}`, tavernTier: targetTier, attack: 0, health: 0 }))
                : []);
        const canRedeem = !isSpectator && !isReady && !isHandFull;
        drawerEl.style.display = 'block';
        drawerEl.innerHTML = `
            <div class="mirage-drawer-header">
                <div class="mirage-drawer-title-group">
                    <span class="mirage-drawer-kicker">TRIPLE REWARD · TIER ${targetTier}</span>
                    <span class="mirage-drawer-title">三连发现奖励（★${targetTier}）</span>
                </div>
                <span class="mirage-drawer-subtitle">待兑现凭据：${pendingTickets.length}</span>
            </div>
            <div class="mirage-drawer-grid">
                ${candidates.map(cand => {
                    const defId = Number(cand.cardDefinitionId ?? cand.CardDefinitionId ?? 0);
                    const cName = escapeAttributeValue(cand.name ?? cand.Name ?? `#${defId}`);
                    const cDesc = escapeAttributeValue(cand.description ?? cand.Description ?? '');
                    const cAtk = Number(cand.attack ?? cand.Attack ?? 0);
                    const cHp = Number(cand.health ?? cand.Health ?? 0);
                    const cTier = Number(cand.tavernTier ?? cand.TavernTier ?? targetTier);
                    const starIdx = Math.min(7, Math.max(1, cTier));
                    const rawPoolCopies = cand.remainingPoolCopies ?? cand.RemainingPoolCopies;
                    const poolText = (rawPoolCopies !== null && rawPoolCopies !== undefined && Number.isFinite(Number(rawPoolCopies)))
                        ? ` · 池余 ${Number(rawPoolCopies)}`
                        : '';
                    const cArt = escapeAttributeValue(cand.artPreview ?? cand.ArtPreview ?? '');
                    const artImgHtml = cArt
                        ? `<img class="mirage-choice-avatar-img" src="${cArt}" alt="" onerror="this.remove()">`
                        : '';
                    const fallbackImgHtml = `<img class="mirage-choice-avatar-fallback" src="/mse-beyond/numbers/mirage-stars/${starIdx}.png" alt="" aria-hidden="true">`;
                    return `<button type="button" class="mirage-choice-card" data-mirage-redeem-ticket="${ticketId}" data-mirage-redeem-card="${defId}" title="${cDesc}" ${canRedeem ? '' : 'disabled'}>
                        <div class="mirage-choice-card-top">
                            <div class="mirage-choice-avatar">${fallbackImgHtml}${artImgHtml}</div>
                            <div class="mirage-choice-meta">
                                <div class="mirage-choice-name">${cName}</div>
                                <div class="mirage-choice-stats">★${cTier} · ATK ${cAtk} / HP ${cHp}${poolText}</div>
                            </div>
                        </div>
                        <div class="mirage-choice-desc">${cDesc || '白板战棋随从'}</div>
                        <span class="mirage-choice-cta">招募至手牌</span>
                    </button>`;
                }).join('')}
            </div>
        `;
    } else {
        drawerEl.style.display = 'none';
        drawerEl.innerHTML = '';
    }
}

function ensureMirageCombatOverlayElement() {
    if (!gameContainerEl) return null;
    let overlayEl = document.getElementById('mirage-combat-playback-overlay');
    if (!overlayEl) {
        overlayEl = document.createElement('div');
        overlayEl.id = 'mirage-combat-playback-overlay';
        overlayEl.className = 'mirage-combat-overlay';
        overlayEl.style.display = 'none';
        overlayEl.addEventListener('click', e => {
            const skipBtn = e.target?.closest?.('[data-mirage-skip-combat]');
            if (skipBtn) {
                e.stopPropagation();
                abortMirageCombatPlayback('skip');
            }
        });
        gameContainerEl.appendChild(overlayEl);
    }
    return overlayEl;
}

function hydrateCombatPlayerHudState(playerDto) {
    if (!playerDto) return null;
    const rawLeader = playerDto.leaderCard ?? playerDto.LeaderCard ?? null;
    let leaderCard = null;
    if (rawLeader) {
        leaderCard = createDiscoverPreviewCard(rawLeader) ?? { ...rawLeader };
        leaderCard.id = Number(rawLeader.cardDefinitionId ?? rawLeader.CardDefinitionId ?? rawLeader.id ?? rawLeader.Id ?? leaderCard.id ?? 0);
        leaderCard.uid = rawLeader.instanceId ?? rawLeader.InstanceId ?? rawLeader.uid ?? leaderCard.uid ?? '';
        leaderCard.name = rawLeader.name ?? rawLeader.Name ?? leaderCard.name ?? '';
        leaderCard.description = rawLeader.description ?? rawLeader.Description ?? leaderCard.description ?? '';
        leaderCard.artPreview = rawLeader.artPreview ?? rawLeader.ArtPreview ?? leaderCard.artPreview ?? '';
        leaderCard.currentHealth = rawLeader.currentHealth ?? rawLeader.CurrentHealth ?? leaderCard.currentHealth ?? 20;
        leaderCard.maxHealth = rawLeader.maxHealth ?? rawLeader.MaxHealth ?? rawLeader.health ?? rawLeader.Health ?? leaderCard.maxHealth ?? 20;
        leaderCard.actAbilities = rawLeader.actAbilities ?? rawLeader.ActAbilities ?? leaderCard.actAbilities ?? [];
        leaderCard.actsThisTurn = rawLeader.actsThisTurn ?? rawLeader.ActsThisTurn ?? leaderCard.actsThisTurn ?? 0;
        leaderCard.maxActsPerTurn = rawLeader.maxActsPerTurn ?? rawLeader.MaxActsPerTurn ?? leaderCard.maxActsPerTurn ?? 1;
    }
    return {
        id: playerDto.playerId ?? playerDto.PlayerId ?? '',
        displayName: playerDto.displayName ?? playerDto.DisplayName ?? '',
        avatarUrl: playerDto.avatarUrl ?? playerDto.AvatarUrl ?? '',
        class: playerDto.class ?? playerDto.Class ?? 'Neutral',
        isAI: Boolean(playerDto.isAI ?? playerDto.IsAI),
        pp: Number(playerDto.currentPP ?? playerDto.CurrentPP ?? playerDto.pp ?? 0),
        maxPP: Number(playerDto.maxPP ?? playerDto.MaxPP ?? 0),
        ep: Number(playerDto.currentEP ?? playerDto.CurrentEP ?? playerDto.ep ?? 0),
        maxEP: Number(playerDto.maxEP ?? playerDto.MaxEP ?? 2),
        sep: Number(playerDto.currentSEP ?? playerDto.CurrentSEP ?? playerDto.sep ?? 0),
        maxSEP: Number(playerDto.maxSEP ?? playerDto.MaxSEP ?? 2),
        deckCount: Number(playerDto.deckCount ?? playerDto.DeckCount ?? 0),
        graveyardCount: Number(playerDto.graveyardCount ?? playerDto.GraveyardCount ?? 0),
        bloodPool: Number(playerDto.bloodPool ?? playerDto.BloodPool ?? 0),
        hand: Array.isArray(playerDto.hand ?? playerDto.Hand) ? (playerDto.hand ?? playerDto.Hand) : [],
        emblem: Array.isArray(playerDto.emblem ?? playerDto.Emblem) ? (playerDto.emblem ?? playerDto.Emblem) : [],
        mirage: playerDto.mirage ?? playerDto.Mirage ?? null,
        leaderCard
    };
}

function renderMirageCombatBoardFromStateDto(stateDto, activeAttackerUid = null, activeDefenderUid = null, options = {}) {
    if (!stateDto) return;
    const playersDto = Array.isArray(stateDto.players ?? stateDto.Players)
        ? (stateDto.players ?? stateDto.Players)
        : [];
    if (playersDto.length === 0) return;

    const localId = String(gameState.viewingPlayerId || gameState.localPlayerId || '').trim().toLowerCase();
    const localDto = playersDto.find(p => String(p?.playerId ?? p?.PlayerId ?? '').trim().toLowerCase() === localId) ?? playersDto[0];
    const opponentDto = playersDto.find(p => p !== localDto) ?? playersDto[1];

    if (options.syncOpponentHud && opponentDto) {
        clearMirageNeutralTavernHud();
        const hydratedOpp = hydrateCombatPlayerHudState(opponentDto);
        if (hydratedOpp) {
            renderPlayerStats(hydratedOpp, 'player2');
        }
    }

    const hydrateCombatField = (fieldDtos, ownerId) => {
        if (!Array.isArray(fieldDtos)) return [];
        return fieldDtos
            .map(dto => {
                const inst = createDiscoverPreviewCard(dto);
                if (!inst) return null;
                inst.ownerId = ownerId;
                inst.zone = 'Field';
                inst.isEvolved = Boolean(dto.isEvolved ?? dto.IsEvolved ?? inst.isEvolved);
                inst.isMirageTriple = Boolean(dto.isMirageTriple ?? dto.IsMirageTriple ?? inst.isMirageTriple);
                inst.currentAttack = dto.currentAttack ?? dto.CurrentAttack ?? inst.attack;
                inst.currentHealth = dto.currentHealth ?? dto.CurrentHealth ?? inst.health;
                inst.keywords = Array.isArray(dto.keywords ?? dto.Keywords) ? [...(dto.keywords ?? dto.Keywords)] : [];
                return inst;
            })
            .filter(Boolean);
    };

    if (localDto && player1FieldEl) {
        const localOwnerId = localDto.playerId ?? localDto.PlayerId ?? gameState.localPlayerId;
        const localField = hydrateCombatField(localDto.field ?? localDto.Field, localOwnerId);
        renderField(localField, player1FieldEl, false);
    }
    if (opponentDto && player2FieldEl) {
        const oppOwnerId = opponentDto.playerId ?? opponentDto.PlayerId ?? 'opponent';
        const oppField = hydrateCombatField(opponentDto.field ?? opponentDto.Field, oppOwnerId);
        renderField(oppField, player2FieldEl, true);
    }

    clearSelectionHighlights();
    if (activeAttackerUid) {
        highlightAttacker(String(activeAttackerUid));
    }
    if (activeDefenderUid) {
        const defenderEl = document.querySelector(`.field-card[data-uid="${activeDefenderUid}"]`);
        if (defenderEl) {
            defenderEl.classList.add('valid-target');
        }
    }
}

export function startMirageCombatPlaybackUi(replayDto) {
    if (gameContainerEl) {
        gameContainerEl.dataset.miragePhase = 'AutoCombat';
    }
    clearMirageNeutralTavernHud();
    const upperFieldZone = document.getElementById('upper-field-zone');
    if (upperFieldZone) upperFieldZone.classList.remove('is-tavern', 'is-frozen');
    const upperZoneTag = document.getElementById('upper-zone-tag');
    if (upperZoneTag) upperZoneTag.textContent = 'OPPONENT WARBAND';
    const mirageSubActions = document.getElementById('mirage-dock-sub-actions');
    if (mirageSubActions) mirageSubActions.hidden = true;

    const shopPanel = document.getElementById('mirage-shop-panel');
    if (shopPanel) shopPanel.style.display = 'none';
    const stageEl = document.getElementById('mirage-tavern-stage');
    if (stageEl) stageEl.style.display = 'none';
    const drawerEl = document.getElementById('mirage-decision-drawer');
    if (drawerEl) drawerEl.style.display = 'none';
    const overlayEl = ensureMirageCombatOverlayElement();
    if (overlayEl) {
        const roundNumber = Number(replayDto?.roundNumber ?? replayDto?.RoundNumber ?? gameState.mirage?.roundNumber ?? 1);
        const totalSteps = Array.isArray(replayDto?.steps ?? replayDto?.Steps)
            ? (replayDto.steps ?? replayDto.Steps).length
            : 0;
        overlayEl.style.display = 'flex';
        overlayEl.innerHTML = `
            <div class="mirage-combat-overlay-info">
                <div class="mirage-combat-overlay-title">幻境第 ${roundNumber} 轮 · 自动交战回放</div>
                <div id="mirage-combat-playback-status" class="mirage-combat-overlay-sub">战斗开始（共 ${totalSteps} 步）</div>
            </div>
            <button type="button" class="mirage-combat-skip-btn" data-mirage-skip-combat="1">跳过战斗</button>
        `;
    }
    const initialState = replayDto?.initialState ?? replayDto?.InitialState;
    renderMirageCombatBoardFromStateDto(initialState, null, null, { syncOpponentHud: true });
}

export function renderMirageCombatStep(stepDto, replayDto, stepIndex = 0) {
    const overlayEl = ensureMirageCombatOverlayElement();
    const totalSteps = Array.isArray(replayDto?.steps ?? replayDto?.Steps)
        ? (replayDto.steps ?? replayDto.Steps).length
        : 0;
    const statusEl = overlayEl?.querySelector('#mirage-combat-playback-status');
    if (statusEl) {
        statusEl.textContent = `交战步数 ${stepIndex + 1} / ${totalSteps}`;
    }
    const attackerUid = String(stepDto?.attackerInstanceId ?? stepDto?.AttackerInstanceId ?? '').trim();
    const defenderUid = String(stepDto?.defenderInstanceId ?? stepDto?.DefenderInstanceId ?? '').trim();
    const postState = stepDto?.postStepState ?? stepDto?.PostStepState;

    if (!postState || !isGamePageActive()) {
        renderMirageCombatBoardFromStateDto(postState, attackerUid || null, defenderUid || null);
        return;
    }

    const motion = getBattleMotionController();
    const prevSnap = motion.captureSnapshot();
    const prevShieldSet = new Set();
    gameContainerEl?.querySelectorAll('#player1-field > .card.has-shield[data-uid], #player2-field > .card.has-shield[data-uid]')
        .forEach(el => {
            if (el.dataset?.uid) {
                prevShieldSet.add(el.dataset.uid);
            }
        });

    const postUnitMap = new Map();
    const postPlayers = Array.isArray(postState.players ?? postState.Players)
        ? (postState.players ?? postState.Players)
        : [];
    postPlayers.forEach(pDto => {
        const fieldList = Array.isArray(pDto?.field ?? pDto?.Field) ? (pDto.field ?? pDto.Field) : [];
        fieldList.forEach(cardDto => {
            const uid = String(cardDto?.instanceId ?? cardDto?.InstanceId ?? cardDto?.uid ?? '').trim();
            if (!uid) return;
            const kw = Array.isArray(cardDto?.keywords ?? cardDto?.Keywords) ? (cardDto.keywords ?? cardDto.Keywords) : [];
            postUnitMap.set(uid, {
                hp: Number(cardDto?.currentHealth ?? cardDto?.CurrentHealth ?? cardDto?.health ?? cardDto?.Health ?? 0),
                hasShield: kw.some(k => equalsIgnoreCase(k, 'Shield'))
            });
        });
    });

    const attackerSnap = (attackerUid && prevSnap?.entities?.get(attackerUid)) || null;
    const targetSnap = (defenderUid && prevSnap?.entities?.get(defenderUid)) || null;
    const attackerFatal = Boolean(attackerUid && !postUnitMap.has(attackerUid));

    const applyPostImpactBoard = () => {
        if (prevSnap) {
            for (const [uid, snapItem] of prevSnap.entities.entries()) {
                if ((snapItem.zone === 'self:field' || snapItem.zone === 'opp:field') && !postUnitMap.has(uid)) {
                    markTimedUidFlag(suppressedExitUids, uid, 1200);
                }
            }
        }

        renderMirageCombatBoardFromStateDto(postState, attackerUid || null, defenderUid || null);

        if (!prevSnap) return;

        const hits = [];
        for (const [uid, snapItem] of prevSnap.entities.entries()) {
            if (snapItem.zone !== 'self:field' && snapItem.zone !== 'opp:field') continue;
            const postInfo = postUnitMap.get(uid);
            const shieldBroken = prevShieldSet.has(uid) && (!postInfo || !postInfo.hasShield);
            if (!postInfo) {
                const dmg = Math.max(1, Number(snapItem.hp ?? 1));
                hits.push({ targetUid: uid, damage: dmg, shieldBroken, fallbackRect: snapItem.rect });
                if (!(attackerFatal && uid === attackerUid) && snapItem.cloneEl) {
                    motion.spawnSemanticExitClone(snapItem, 'destroy');
                }
            } else if (snapItem.hp !== null && postInfo.hp < snapItem.hp) {
                hits.push({
                    targetUid: uid,
                    damage: snapItem.hp - postInfo.hp,
                    shieldBroken
                });
            } else if (shieldBroken) {
                hits.push({ targetUid: uid, damage: 0, shieldBroken: true });
            }
        }

        if (hits.length > 0) {
            motion.triggerWaveHits(hits, prevSnap);
        }
    };

    if (attackerSnap && targetSnap) {
        motion.playAttackSequence({
            attackerSnap,
            targetSnap,
            attackerFatal,
            onImpactFrame: () => applyPostImpactBoard()
        });
    } else {
        applyPostImpactBoard();
    }
}

export function renderMirageCombatOutcome(replayDto) {
    const overlayEl = ensureMirageCombatOverlayElement();
    const statusEl = overlayEl?.querySelector('#mirage-combat-playback-status');
    if (!statusEl) return;
    const outcome = replayDto?.outcome ?? replayDto?.Outcome ?? {};
    const isTie = Boolean(outcome.isTie ?? outcome.IsTie);
    const damage = Number(outcome.leaderDamage ?? outcome.LeaderDamage ?? 0);
    const winnerId = String(outcome.winnerPlayerId ?? outcome.WinnerPlayerId ?? '').trim().toLowerCase();
    const localId = String(gameState.viewingPlayerId || gameState.localPlayerId || '').trim().toLowerCase();
    if (isTie || damage <= 0) {
        statusEl.textContent = '本轮交战平局（0 伤害）';
    } else if (winnerId && winnerId === localId) {
        statusEl.textContent = `本轮获胜！对敌方主战者造成 ${damage} 点伤害`;
    } else {
        statusEl.textContent = `本轮落败，主战者受到 ${damage} 点伤害`;
    }
}

function renderMirage8PHudAndModal(currentGameState, localPlayer) {
    let standingsPodEl = document.getElementById('mirage-8p-standings-pod');
    let placementModalEl = document.getElementById('mirage-8p-placement-modal');

    const isTournamentSeat = isMirageMode(currentGameState)
        && Boolean(currentGameState.mirage?.tournamentRoomId ?? currentGameState.mirage?.TournamentRoomId ?? currentGameState.mirageStandings);
    const standings = currentGameState.mirageStandings;
    const entries = Array.isArray(standings?.entries ?? standings?.Entries)
        ? (standings.entries ?? standings.Entries)
        : [];

    if (!isTournamentSeat || entries.length === 0) {
        if (standingsPodEl) standingsPodEl.remove();
        if (placementModalEl) placementModalEl.remove();
        return;
    }

    const cornerHost = document.getElementById('player1-corner-pod') ?? gameContainerEl;
    if (cornerHost) {
        if (!standingsPodEl) {
            standingsPodEl = document.createElement('div');
            standingsPodEl.id = 'mirage-8p-standings-pod';
            standingsPodEl.className = 'mirage-standings-pod';
            standingsPodEl.addEventListener('click', (e) => {
                const row = e.target?.closest?.('[data-mirage-spectate-player]');
                if (!row) return;
                const targetPid = row.dataset.mirageSpectatePlayer;
                if (targetPid) {
                    void switchMirageSpectateTargetRequest(targetPid);
                }
            });
            cornerHost.prepend(standingsPodEl);
        } else if (standingsPodEl.parentElement !== cornerHost) {
            cornerHost.prepend(standingsPodEl);
        }

        const viewingId = String(currentGameState.viewingPlayerId || currentGameState.localPlayerId || '').toLowerCase();
        const selfOrigId = String(currentGameState.selfOriginalPlayerId || '').toLowerCase();
        const nextMap = standings?.nextOpponentsByPlayerId ?? standings?.NextOpponentsByPlayerId ?? {};
        const findNextOpp = () => {
            for (const [k, v] of Object.entries(nextMap)) {
                if (String(k).toLowerCase() === viewingId) return v;
            }
            return null;
        };
        const nextOpp = findNextOpp();
        const nextOppId = String(nextOpp?.playerId ?? nextOpp?.PlayerId ?? '').toLowerCase();
        const nextIsGhost = Boolean(nextOpp?.isGhost ?? nextOpp?.IsGhost);
        const canSwitchSpectate = currentGameState.isSpectator === true || currentGameState.gameOver === true;
        const aliveCount = Number(standings?.aliveCount ?? standings?.AliveCount ?? entries.filter(e => !(e.isEliminated ?? e.IsEliminated)).length);
        const readyCount = Number(standings?.readyCount ?? standings?.ReadyCount ?? 0);

        const rowsHtml = entries.map(entry => {
            const pid = String(entry.playerId ?? entry.PlayerId ?? '');
            const pidLower = pid.toLowerCase();
            const rank = Number(entry.rank ?? entry.Rank ?? 1);
            const name = escapeAttributeValue(entry.displayName ?? entry.DisplayName ?? '旅人');
            const isAi = Boolean(entry.isAi ?? entry.IsAi);
            const isEliminated = Boolean(entry.isEliminated ?? entry.IsEliminated);
            const hp = Number(entry.currentHp ?? entry.CurrentHp ?? 0);
            const tier = Number(entry.tavernTier ?? entry.TavernTier ?? 1);
            const ready = Boolean(entry.isReady ?? entry.IsReady);
            const isSelf = selfOrigId && pidLower === selfOrigId;
            const isViewing = viewingId && pidLower === viewingId;
            const isNext = !nextIsGhost && nextOppId && pidLower === nextOppId;
            const clickable = canSwitchSpectate && !isEliminated && pidLower !== viewingId;

            return `
                <div class="mirage-standing-row ${isSelf ? 'is-self' : ''} ${isViewing ? 'is-viewing' : ''} ${isNext ? 'is-next' : ''} ${isEliminated ? 'is-eliminated' : ''} ${clickable ? 'is-clickable' : ''}"
                     ${clickable ? `data-mirage-spectate-player="${escapeAttributeValue(pid)}"` : ''}
                     title="${name}${isAi ? ' (AI)' : ''}${isNext ? ' · 本轮交战对手' : ''}${clickable ? ' · 点击切换观战视角' : ''}">
                    <span class="mirage-standing-rank">#${rank}</span>
                    <span class="mirage-standing-name">${isNext ? '<span class="mirage-standing-vs-tag">VS</span>' : ''}<span>${name}${ready && !isEliminated ? ' · ✓' : ''}</span></span>
                    <span class="mirage-standing-tier">★${tier}</span>
                    <span class="mirage-standing-hp">${isEliminated ? 'OUT' : `${hp}`}</span>
                </div>
            `;
        }).join('');

        standingsPodEl.innerHTML = `
            <div class="mirage-standings-header">
                <span>MIRAGE 8P · 存活 ${aliveCount}/8</span>
                <span>就绪 ${readyCount}/${aliveCount}</span>
            </div>
            ${rowsHtml}
        `;

        // Top Tavern HUD (#player2-info) already shows next opponent summary in Recruit phase
        void nextOpp;
    }

    // Placement Result Modal when local human player finishes (Champion #1 or Eliminated #2..#8)
    const shouldShowPlacementModal = currentGameState.gameOver === true
        && currentGameState.isSpectator !== true
        && currentGameState.miragePlacementModalDismissed !== true;

    if (!shouldShowPlacementModal) {
        if (placementModalEl) placementModalEl.remove();
        return;
    }

    const selfOrigLower = String(currentGameState.selfOriginalPlayerId || currentGameState.localPlayerId || '').toLowerCase();
    const selfEntry = entries.find(e => String(e.playerId ?? e.PlayerId ?? '').toLowerCase() === selfOrigLower);
    const isChampion = Boolean(currentGameState.winner && String(currentGameState.winner).toLowerCase() === selfOrigLower)
        || Number(selfEntry?.finalPlacement ?? selfEntry?.FinalPlacement) === 1;
    const placement = isChampion ? 1 : Number(selfEntry?.finalPlacement ?? selfEntry?.FinalPlacement ?? selfEntry?.rank ?? 8);
    const aliveRemaining = Number(standings?.aliveCount ?? standings?.AliveCount ?? 0);
    const tourneyFinished = String(standings?.phase ?? standings?.Phase ?? '') === 'Finished' || aliveRemaining <= 1;
    const canStaySpectate = !isChampion && !tourneyFinished && aliveRemaining >= 1;

    if (!placementModalEl && gameContainerEl) {
        placementModalEl = document.createElement('div');
        placementModalEl.id = 'mirage-8p-placement-modal';
        placementModalEl.className = 'mirage-placement-modal';
        placementModalEl.addEventListener('click', (e) => {
            if (e.target?.closest?.('[data-mirage-modal-stay-spectate]')) {
                const topAlive = entries.find(en => !(en.isEliminated ?? en.IsEliminated));
                const targetId = topAlive?.playerId ?? topAlive?.PlayerId ?? null;
                document.dispatchEvent(new CustomEvent('svb:mirage-stay-spectate', { detail: { targetPlayerId: targetId } }));
                placementModalEl?.remove();
                return;
            }
            if (e.target?.closest?.('[data-mirage-modal-dismiss]')) {
                currentGameState.miragePlacementModalDismissed = true;
                placementModalEl?.remove();
                return;
            }
            if (e.target?.closest?.('[data-mirage-modal-back-lobby]')) {
                document.dispatchEvent(new CustomEvent('svb:mirage-return-lobby'));
            }
        });
        gameContainerEl.appendChild(placementModalEl);
    }

    if (placementModalEl) {
        placementModalEl.innerHTML = `
            <div class="mirage-placement-card ${isChampion ? 'is-champion' : ''}">
                <div class="mirage-placement-rank">#${placement}</div>
                <div class="mirage-placement-title">${isChampion ? 'VICTORY · 星界幻境霸主' : `第 ${placement} 名 · 征程结束`}</div>
                <div class="mirage-placement-desc">${escapeAttributeValue(currentGameState.gameMessage || (isChampion ? '击败全部对手夺得第一名！' : '可留场全公开观战剩余选手对决，或直接返回幻境大厅开启下一局。'))}</div>
                <div class="mirage-placement-actions">
                    ${canStaySpectate
                        ? `<button type="button" class="mlobby-btn is-primary" data-mirage-modal-stay-spectate="1">留场观战（全公开视角）</button>`
                        : `<button type="button" class="mlobby-btn" data-mirage-modal-dismiss="1">查看最终战场</button>`}
                    <button type="button" class="mlobby-btn is-gold" data-mirage-modal-back-lobby="1">返回多人对战</button>
                </div>
            </div>
        `;
    }
}

export function hideMirageCombatPlaybackOverlay() {
    cancelAllBattleMotions();
    const overlayEl = document.getElementById('mirage-combat-playback-overlay');
    if (overlayEl) {
        overlayEl.style.display = 'none';
        overlayEl.innerHTML = '';
    }
}

export function showMirageReconnectLockOverlay() {
    const shopPanel = document.getElementById('mirage-shop-panel');
    if (shopPanel) shopPanel.style.display = 'none';
    const stageEl = document.getElementById('mirage-tavern-stage');
    if (stageEl) stageEl.style.display = 'none';
    const drawerEl = document.getElementById('mirage-decision-drawer');
    if (drawerEl) drawerEl.style.display = 'none';
    const overlayEl = ensureMirageCombatOverlayElement();
    if (overlayEl) {
        overlayEl.style.display = 'flex';
        overlayEl.innerHTML = `
            <div class="mirage-combat-overlay-sync">正在同步最新幻境对局状态，请稍候...</div>
        `;
    }
}

// --- 具体渲染辅助函数 (占位符) ---
function updateEmblemDisplay(playerState, prefix) {
    const emblemZone = document.getElementById(`${prefix}-emblem-zone`)
        ?? document.getElementById(`${prefix}-info`)?.querySelector('.emblem-zone');
    if (!emblemZone) return;

    const rawEmblems = Array.isArray(playerState?.emblem)
        ? playerState.emblem
        : Array.isArray(playerState?.Emblem)
            ? playerState.Emblem
            : [];
    const activeEmblems = rawEmblems.filter(Boolean).slice(0, EMBLEM_SLOT_COUNT);

    emblemZone.innerHTML = '';
    emblemZone.classList.toggle('is-empty', activeEmblems.length === 0);
    emblemZone.dataset.emblemCount = String(activeEmblems.length);

    // Top-right opponent emblems fill right-to-left in the single-row 5-slot crest dock
    const alignRight = prefix === 'player2';
    const slotOrder = alignRight ? [4, 3, 2, 1, 0] : [0, 1, 2, 3, 4];
    const slotMap = new Array(EMBLEM_SLOT_COUNT).fill(null);
    activeEmblems.forEach((emblem, idx) => {
        slotMap[slotOrder[idx]] = emblem;
    });

    for (let i = 0; i < EMBLEM_SLOT_COUNT; i++) {
        const slot = document.createElement('div');
        slot.classList.add('emblem-slot');

        const emblem = slotMap[i];
        if (emblem) {
            const emblemUid = emblem.uid ?? emblem.InstanceId ?? emblem.instanceId ?? '';
            const previewPath = emblem.artPreview ?? emblem.ArtPreview ?? emblem.artDetail ?? emblem.ArtDetail ?? '';
            const previewUrl = resolveArtUrl(previewPath);
            const rawCountdown = emblem.countdown
                ?? emblem.CurrentCountdown
                ?? emblem.currentCountdown
                ?? emblem.Countdown;
            const showCountdown = rawCountdown !== null
                && rawCountdown !== undefined
                && rawCountdown !== '';
            const artWrapper = document.createElement('div');
            artWrapper.classList.add('emblem-art');
            if (previewUrl) {
                const img = document.createElement('img');
                img.src = previewUrl;
                img.alt = emblem.name ?? emblem.Name ?? 'Emblem';
                artWrapper.appendChild(img);
                void preloadArt(previewPath, { priority: 'low' }).catch(() => {
                    // ignore preload errors for emblem icons
                });
            }
            slot.classList.add('filled');
            if (emblemUid) {
                slot.dataset.uid = String(emblemUid);
            }
            slot.title = emblem.name ?? emblem.Name ?? '';
            slot.appendChild(artWrapper);

            if (showCountdown) {
                const countdown = document.createElement('span');
                countdown.classList.add('emblem-countdown');
                countdown.textContent = rawCountdown;
                slot.appendChild(countdown);
            }
        }

        emblemZone.appendChild(slot);
    }
}

function updateEmblemZone(playerState) {
    if (!playerState) return;
    const isLocal = playerState.id === gameState.localPlayerId;
    const prefix = isLocal ? 'player1' : 'player2';
    updateEmblemDisplay(playerState, prefix);
}

const CARD_CLASS_ENUM_KEYS = Object.freeze([
    'Neutral',
    'Forestcraft',
    'Swordcraft',
    'Runecraft',
    'Dragoncraft',
    'Abysscraft',
    'Havencraft',
    'Portalcraft'
]);

const CARD_CLASS_ALIAS_MAP = Object.freeze({
    neutral: 'Neutral',
    forest: 'Forestcraft',
    forestcraft: 'Forestcraft',
    sword: 'Swordcraft',
    swordcraft: 'Swordcraft',
    rune: 'Runecraft',
    runecraft: 'Runecraft',
    dragon: 'Dragoncraft',
    dragoncraft: 'Dragoncraft',
    abyss: 'Abysscraft',
    abysscraft: 'Abysscraft',
    blood: 'Abysscraft',
    bloodcraft: 'Abysscraft',
    haven: 'Havencraft',
    havencraft: 'Havencraft',
    portal: 'Portalcraft',
    portalcraft: 'Portalcraft'
});

function normalizeCardClassKey(rawValue) {
    if (rawValue === null || rawValue === undefined) return '';

    if (typeof rawValue === 'number' && Number.isFinite(rawValue)) {
        return CARD_CLASS_ENUM_KEYS[rawValue] ?? '';
    }

    const rawText = String(rawValue).trim();
    if (!rawText) return '';

    if (/^\d+$/.test(rawText)) {
        const index = Number.parseInt(rawText, 10);
        return CARD_CLASS_ENUM_KEYS[index] ?? '';
    }

    const aliasKey = rawText.replace(/[\s_-]/g, '').toLowerCase();
    return CARD_CLASS_ALIAS_MAP[aliasKey] ?? '';
}

function resolvePlayerClassLabel(playerState) {
    const classKey = normalizeCardClassKey(
        playerState?.class
        ?? playerState?.Class
        ?? playerState?.deckClass
        ?? playerState?.DeckClass
        ?? 'Neutral'
    ) || 'Neutral';
    return getCachedEnumLabel('CardClass', classKey, normalizeEnumName(classKey || 'Neutral'));
}

function resolvePlayerDisplayName(playerState, isLocal) {
    const rawName = playerState?.displayName
        ?? playerState?.DisplayName
        ?? playerState?.name
        ?? playerState?.Name
        ?? playerState?.username
        ?? playerState?.Username
        ?? '';
    const normalizedName = String(rawName ?? '').trim();
    if (normalizedName) return normalizedName;

    if (isLocal && !gameState.isSpectator) {
        const session = getUserSession();
        const sessionName = String(session?.displayName ?? '').trim();
        if (sessionName) return sessionName;
    }

    if (playerState?.isAI === true || playerState?.IsAI === true) {
        return 'AI';
    }

    return isLocal ? 'PLAYER' : 'OPPONENT';
}

function resolveAvatarImageSource(rawValue) {
    const text = String(rawValue ?? '').trim();
    if (!text) return '';
    if (/^data:image\//i.test(text)) return text;
    if (/^(https?:)?\/\//i.test(text)) return text;
    if (text.startsWith('/') || text.startsWith('./') || text.startsWith('../')) {
        return encodeURI(text).replace(/'/g, '%27');
    }
    return '';
}

function resolvePlayerAvatar(playerState, isLocal, displayName) {
    const session = (isLocal && !gameState.isSpectator) ? getUserSession() : null;
    const candidates = [
        playerState?.avatarUrl,
        playerState?.AvatarUrl,
        playerState?.avatar,
        playerState?.Avatar,
        session?.avatar
    ];

    let imageSrc = '';
    let textAvatar = '';
    for (const candidate of candidates) {
        const resolved = resolveAvatarImageSource(candidate);
        if (resolved && !imageSrc) {
            imageSrc = resolved;
            continue;
        }
        if (!resolved && !textAvatar) {
            const value = String(candidate ?? '').trim();
            if (value) textAvatar = value;
        }
    }

    const fallbackText = textAvatar || (isLocal ? 'YOU' : 'OPP');
    return { imageSrc, fallbackText };
}

function resolvePlayerOrderBadge(playerState) {
    const raw = playerState?.isFirst ?? playerState?.IsFirst;
    if (typeof raw !== 'boolean') {
        return null;
    }
    return raw
        ? { text: '先手', className: 'first' }
        : { text: '后手', className: 'second' };
}

const playerCounterLabelMap = new Map([
    ['combo', '连击数'],
    ['graveyard', '墓场数'],
    ['bloodpool', '血池'],
    ['rally', '连携数']
]);

function getPlayerCounterValue(playerState, key, fallbackValue = 0) {
    const normalizedKey = String(key ?? '').trim().toLowerCase();
    if (!normalizedKey) {
        return fallbackValue;
    }

    const counterMap = playerState?.counters ?? playerState?.Counters;
    if (counterMap && typeof counterMap === 'object') {
        for (const [entryKey, entryValue] of Object.entries(counterMap)) {
            if (String(entryKey).trim().toLowerCase() !== normalizedKey) continue;
            const parsed = Number(entryValue);
            if (Number.isFinite(parsed)) {
                return Math.max(0, Math.trunc(parsed));
            }
        }
    }

    const parsedFallback = Number(fallbackValue);
    return Number.isFinite(parsedFallback) ? Math.max(0, Math.trunc(parsedFallback)) : 0;
}

const CORNER_PINNED_COUNTER_KEYS = new Set(['spellboost', 'earthsigil', 'necromancy', 'resonance', 'artifactsdestroyed']);

function renderCornerExtraCounters(playerState, prefix) {
    const extraContainer = document.getElementById(`${prefix}-extra-counters`);
    if (!extraContainer) return;
    if (!playerState || isMirageMode(gameState)) {
        extraContainer.innerHTML = '';
        return;
    }

    const ownerLabelMap = playerState.counterDisplayNames ?? playerState.CounterDisplayNames ?? {};
    const ownerCounterMap = playerState.counters ?? playerState.Counters ?? {};
    const entries = buildCounterSummaryEntries({
        summaryEntries: playerState.counterSummaryEntries ?? playerState.CounterSummaryEntries ?? [],
        localCounterMap: null,
        localLabelMap: null,
        ownerCounterMap,
        ownerLabelMap,
        ownerReferenceKeys: Object.keys(ownerLabelMap),
        includeOwnerCounters: true,
        hideOwnerSystemKeys: true,
        showZeroSummaryEntries: false,
        showZeroOwnerEntries: false,
        resolveLabel: getCounterLabel
    }).filter(entry => {
        const normalizedKey = String(entry?.key ?? '').trim().toLowerCase();
        if (!normalizedKey || PLAYER_SYSTEM_COUNTER_KEYS.has(normalizedKey)) return false;
        if (!(Number(entry?.value) > 0)) return false;
        if (CORNER_PINNED_COUNTER_KEYS.has(normalizedKey)) return true;
        const label = String(entry?.label ?? '').trim();
        return label.length > 0 && label.length <= 5 && !label.includes('次数') && !label.includes('回合');
    });

    extraContainer.innerHTML = entries
        .slice(0, 2)
        .map(entry => `
            <div class="resource-box extra-counter-box is-pill">
                <span class="resource-label">${escapeAttributeValue(entry.label)}</span>
                <span class="resource-value">${entry.value}</span>
            </div>
        `.trim())
        .join('');
}

function resolveMirageNextOpponentSummary(currentGameState) {
    const standings = currentGameState?.mirageStandings;
    const nextMap = standings?.nextOpponentsByPlayerId ?? standings?.NextOpponentsByPlayerId;
    if (!nextMap || typeof nextMap !== 'object') return null;
    const viewingId = String(currentGameState?.viewingPlayerId || currentGameState?.localPlayerId || '').trim().toLowerCase();
    if (!viewingId) return null;
    for (const [k, v] of Object.entries(nextMap)) {
        if (String(k).trim().toLowerCase() === viewingId) {
            return v || null;
        }
    }
    return null;
}

function clearMirageNeutralTavernHud() {
    const p2Info = document.getElementById('player2-info');
    if (!p2Info) return;
    p2Info.classList.remove('is-mirage-tavern', 'is-tavern-frozen');
    const avatarEl = p2Info.querySelector('.stats-avatar');
    if (avatarEl) {
        avatarEl.classList.remove('is-tavern-emblem');
    }
    const tavernUpgradePod = p2Info.querySelector('.mirage-tavern-upgrade-pod');
    if (tavernUpgradePod) {
        tavernUpgradePod.style.display = 'none';
        tavernUpgradePod.innerHTML = '';
    }
    const tavernTierBar = p2Info.querySelector('.mirage-tavern-tier-segments');
    if (tavernTierBar) {
        tavernTierBar.style.display = 'none';
    }
}

function renderMirageNeutralTavernHud(currentGameState, localPlayer) {
    const p2Info = document.getElementById('player2-info');
    const statsContainer = p2Info?.querySelector('.player-stats');
    if (!p2Info || !statsContainer) return;

    const mirageInfo = localPlayer?.mirage ?? {};
    const roundNumber = Math.max(1, Number(currentGameState?.mirage?.roundNumber ?? currentGameState?.mirage?.RoundNumber ?? 1) || 1);
    const tavernTier = Math.max(1, Number(mirageInfo.tavernTier ?? mirageInfo.TavernTier ?? 1) || 1);
    const maxTavernTier = Math.max(1, Number(mirageInfo.maxTavernTier ?? mirageInfo.MaxTavernTier ?? 6) || 6);
    const upgradeCost = Math.max(0, Number(mirageInfo.upgradeCost ?? mirageInfo.UpgradeCost ?? 0) || 0);
    const canUpgradeTier = Boolean(mirageInfo.canUpgradeTier ?? mirageInfo.CanUpgradeTier);
    const isShopFrozen = Boolean(mirageInfo.isShopFrozen ?? mirageInfo.IsShopFrozen);
    const isReady = Boolean(mirageInfo.isReady ?? mirageInfo.IsReady);
    const isSpectator = currentGameState?.isSpectator === true;
    const starSpriteIndex = Math.min(7, Math.max(1, tavernTier));

    p2Info.classList.add('is-mirage-tavern');
    p2Info.classList.toggle('is-tavern-frozen', isShopFrozen);
    p2Info.classList.remove('is-danger', 'attack-drag-hover', 'attack-drag-invalid', 'is-attack-confirmed');

    const oppSkillPod = statsContainer.querySelector('.mirage-leader-skill-pod');
    if (oppSkillPod) {
        oppSkillPod.style.display = 'none';
        oppSkillPod.innerHTML = '';
    }
    updateEmblemDisplay({ emblem: [] }, 'player2');

    const avatarEl = statsContainer.querySelector('.stats-avatar');
    if (avatarEl) {
        avatarEl.classList.remove('mirage-leader-avatar', 'targetable', 'targetable-spell', 'targetable-restricted', 'choice-selected', 'hand-drag-target-valid');
        avatarEl.classList.add('has-image', 'is-tavern-emblem');
        avatarEl.innerHTML = buildMirageTavernAstrolabeSvg(tavernTier, maxTavernTier);
    }

    const classIconEl = document.getElementById('player2-class-icon');
    if (classIconEl) {
        const neutralIcon = CLASS_ICON_URL_MAP.Neutral;
        if (neutralIcon) {
            classIconEl.src = neutralIcon;
            classIconEl.hidden = false;
        } else {
            classIconEl.hidden = true;
        }
    }

    const playerNameEl = statsContainer.querySelector('.player-name');
    if (playerNameEl) {
        playerNameEl.textContent = '';
        const nameTextEl = document.createElement('span');
        nameTextEl.className = 'player-name-text';
        nameTextEl.textContent = '星界酒馆';
        playerNameEl.appendChild(nameTextEl);

        const badgeEl = document.createElement('span');
        badgeEl.className = `player-order-badge tavern-badge ${isShopFrozen ? 'is-frozen' : ''}`;
        badgeEl.textContent = isShopFrozen ? 'FROZEN' : `R${roundNumber}`;
        playerNameEl.appendChild(badgeEl);
    }

    const classEl = statsContainer.querySelector('.class-name');
    if (classEl) {
        const nextOpp = resolveMirageNextOpponentSummary(currentGameState);
        if (nextOpp) {
            const oppName = String(nextOpp.displayName ?? nextOpp.DisplayName ?? '未知对手');
            const oppIsGhost = Boolean(nextOpp.isGhost ?? nextOpp.IsGhost);
            classEl.textContent = `VS ${oppName}${oppIsGhost ? ' (残影)' : ''}`;
        } else {
            classEl.textContent = 'NEUTRAL TAVERN';
        }
    }

    const slotsRow = document.getElementById('player2-slots-row') ?? statsContainer.querySelector('.leader-rpg-slots-row');
    let upgradePod = statsContainer.querySelector('.mirage-tavern-upgrade-pod');
    if (!upgradePod) {
        upgradePod = document.createElement('div');
        upgradePod.className = 'mirage-tavern-upgrade-pod';
        if (slotsRow) {
            slotsRow.appendChild(upgradePod);
        } else {
            statsContainer.appendChild(upgradePod);
        }
    }
    upgradePod.style.display = 'inline-flex';

    const isMaxTier = tavernTier >= maxTavernTier;
    const canUp = !isSpectator && !isReady && canUpgradeTier && !isMaxTier;

    if (isMaxTier) {
        upgradePod.innerHTML = `
            <button type="button" class="leader-rpg-chip is-tavern-upgrade mirage-hud-upgrade-btn is-max" data-mirage-action="upgrade" disabled title="酒馆已达最高等级 ★${maxTavernTier}">
                <span class="resource-label tavern-up-tier">酒馆 ★${tavernTier}</span>
                <span class="mirage-chip-val tavern-up-cost">MAX</span>
            </button>
        `;
    } else {
        const nextTier = Math.min(maxTavernTier, tavernTier + 1);
        const tooltip = escapeAttributeValue(
            isSpectator
                ? `当前酒馆 ★${tavernTier} · 升级至 ★${nextTier} 需 ${upgradeCost} 金币`
                : `当前酒馆 ★${tavernTier} · 点击花费 ${upgradeCost} 金币升至 ★${nextTier}`
        );
        upgradePod.innerHTML = `
            <button type="button" class="leader-rpg-chip is-tavern-upgrade mirage-hud-upgrade-btn ${canUp ? 'is-affordable' : ''}" data-mirage-action="upgrade" ${canUp ? '' : 'disabled'} title="${tooltip}">
                <span class="resource-label tavern-up-tier">★${tavernTier}▸★${nextTier}</span>
                <span class="mirage-chip-val tavern-up-cost">${upgradeCost}G</span>
            </button>
        `;
    }

    const hpContainer = statsContainer.querySelector('.hp-display');
    const hpValueEl = statsContainer.querySelector('.hp-value');
    const hpMaxEl = statsContainer.querySelector('.hp-max');

    if (hpValueEl) {
        hpValueEl.textContent = `★${tavernTier}`;
        hpValueEl.style.color = '';
    }
    if (hpMaxEl) {
        hpMaxEl.textContent = `/ ${maxTavernTier}`;
    }
    if (hpContainer) {
        const existingTimer = hpGhostSettleTimers.get(hpContainer.id || 'player2-hp');
        if (existingTimer) {
            window.clearTimeout(existingTimer);
            hpGhostSettleTimers.delete(hpContainer.id || 'player2-hp');
        }
        delete hpContainer.dataset.lastHpPct;
        hpContainer.classList.remove('is-danger', 'is-low');

        let tierSegmentsEl = hpContainer.querySelector('.mirage-tavern-tier-segments');
        if (!tierSegmentsEl) {
            tierSegmentsEl = document.createElement('div');
            tierSegmentsEl.className = 'mirage-tavern-tier-segments';
            tierSegmentsEl.setAttribute('aria-hidden', 'true');
            hpContainer.prepend(tierSegmentsEl);
        }
        tierSegmentsEl.style.display = 'grid';
        tierSegmentsEl.style.gridTemplateColumns = `repeat(${maxTavernTier}, 1fr)`;
        tierSegmentsEl.innerHTML = Array.from({ length: maxTavernTier }, (_, idx) => (
            `<span class="tavern-tier-seg ${idx < tavernTier ? 'is-active' : ''}"></span>`
        )).join('');
    }
}

function renderMirageLeaderSkillPod(playerState, statsContainer, isLocal, prefix = 'player1') {
    if (!statsContainer) return;
    const slotsRow = document.getElementById(`${prefix}-slots-row`) ?? statsContainer.querySelector('.leader-rpg-slots-row');
    let podEl = statsContainer.querySelector('.mirage-leader-skill-pod');
    const leaderCard = playerState?.leaderCard;
    const leaderCardId = Number(leaderCard?.id ?? leaderCard?.Id ?? 0);
    if (!isMirageMode(gameState) || !leaderCard || leaderCardId <= 0) {
        if (podEl) {
            podEl.style.display = 'none';
            podEl.innerHTML = '';
        }
        return;
    }

    if (!podEl) {
        podEl = document.createElement('div');
        podEl.className = 'mirage-leader-skill-pod';
        if (slotsRow) {
            slotsRow.appendChild(podEl);
        } else {
            statsContainer.appendChild(podEl);
        }
    }
    podEl.style.display = 'inline-flex';

    const mirageInfo = playerState?.mirage ?? {};
    const isReady = Boolean(mirageInfo.isReady ?? mirageInfo.IsReady);

    const rawActAbilities = Array.isArray(leaderCard.actAbilities)
        ? leaderCard.actAbilities
        : (Array.isArray(leaderCard.ActAbilities) ? leaderCard.ActAbilities : []);
    const actionState = isLocal ? getCardActionState(leaderCard, 'leader') : null;
    const actAbilities = (actionState?.actAbilities && actionState.actAbilities.length > 0)
        ? actionState.actAbilities
        : rawActAbilities;
    const hasAct = actAbilities.length > 0 || Boolean(actionState?.showAct);
    const desc = escapeAttributeValue(leaderCard.description ?? leaderCard.Description ?? '无技能说明');
    const leaderUid = escapeAttributeValue(getCardUid(leaderCard) ?? '');

    let skillChipHtml = '';
    if (hasAct) {
        if (isLocal && !gameState.isSpectator) {
            const firstAbility = actAbilities[0] ?? {};
            const rawAbilityCost = firstAbility.actCost ?? firstAbility.ActCost ?? actionState?.actCost;
            const hasAbilityCost = rawAbilityCost !== null && rawAbilityCost !== undefined && Number.isFinite(Number(rawAbilityCost));
            const abilityCost = hasAbilityCost ? Number(rawAbilityCost) : null;
            const abilityRuntimeId = escapeAttributeValue(firstAbility.abilityRuntimeId ?? firstAbility.AbilityRuntimeId ?? '');
            const actsThisTurn = Number(leaderCard.actsThisTurn ?? leaderCard.ActsThisTurn ?? 0);
            const maxActsPerTurn = Number(leaderCard.maxActsPerTurn ?? leaderCard.MaxActsPerTurn ?? 1);
            const isUsed = (Number.isFinite(actsThisTurn) && Number.isFinite(maxActsPerTurn) && actsThisTurn >= maxActsPerTurn)
                || Boolean(actionState?.actUsedThisTurn);
            const syncPending = actionState?.actPlayabilityKnown !== true;
            const abilityExplicitPlayable = firstAbility.isPlayable ?? firstAbility.IsPlayable;
            const isPlayable = gameState.phase === 'Recruit'
                && !isReady
                && !isUsed
                && !syncPending
                && (abilityExplicitPlayable !== undefined ? abilityExplicitPlayable === true : actionState?.actPlayable === true);
            const failureReason = isUsed
                ? '本回合已发动'
                : (syncPending
                    ? ACT_PLAYABILITY_SYNC_PENDING_REASON
                    : String(firstAbility.failureReason ?? firstAbility.FailureReason ?? actionState?.actFailureReason ?? ''));
            const valLabel = isUsed
                ? '已用'
                : (syncPending
                    ? '同步'
                    : (abilityCost !== null ? `${abilityCost}G` : '发动'));
            const runtimeAttr = abilityRuntimeId ? `data-act-ability-id="${abilityRuntimeId}"` : '';
            const tooltip = escapeAttributeValue(failureReason ? `${desc}（${failureReason}）` : desc);
            skillChipHtml = `
                <button type="button" class="act-button leader-rpg-chip is-skill mirage-leader-act-btn ${isPlayable ? 'playable' : 'disabled'}" data-act-uid="${leaderUid}" ${runtimeAttr} ${isPlayable ? '' : 'disabled'} title="${tooltip}">
                    <span class="resource-label skill-label">统帅技</span>
                    <span class="mirage-chip-val">${valLabel}</span>
                </button>
            `;
        } else {
            const firstAbility = actAbilities[0] ?? {};
            const rawAbilityCost = firstAbility.actCost ?? firstAbility.ActCost;
            const hasAbilityCost = rawAbilityCost !== null && rawAbilityCost !== undefined && Number.isFinite(Number(rawAbilityCost));
            const actsThisTurn = Number(leaderCard.actsThisTurn ?? leaderCard.ActsThisTurn ?? 0);
            const maxActsPerTurn = Number(leaderCard.maxActsPerTurn ?? leaderCard.MaxActsPerTurn ?? 1);
            const isUsed = Number.isFinite(actsThisTurn) && Number.isFinite(maxActsPerTurn) && actsThisTurn >= maxActsPerTurn;
            const badgeText = isUsed ? '已用' : (hasAbilityCost ? `${Number(rawAbilityCost)}G` : '主动');
            skillChipHtml = `
                <span class="leader-rpg-chip is-skill mirage-passive-skill-badge" title="${desc}">
                    <span class="resource-label skill-label">统帅技</span>
                    <span class="mirage-chip-val">${badgeText}</span>
                </span>
            `;
        }
    } else {
        skillChipHtml = `
            <span class="leader-rpg-chip is-skill mirage-passive-skill-badge" title="${desc}">
                <span class="resource-label skill-label">被动</span>
                <span class="mirage-chip-val">光环</span>
            </span>
        `;
    }

    podEl.innerHTML = skillChipHtml;
}

function renderPlayerStats(playerState, prefix) { // prefix = 'player1' or 'player2'
    if (!playerState) {
        return;
    }

    try {
        const infoContainer = document.getElementById(`${prefix}-info`);
        const statsContainer = infoContainer ? infoContainer.querySelector('.player-stats') : null;
        const cornerPod = document.getElementById(`${prefix}-corner-pod`);

        if (!statsContainer) {
            console.warn(`UI: Stats container for ${prefix} not found.`);
            return;
        }

        const currentHp = playerState.leaderCard?.currentHealth ?? 0;
        const maxHpRaw = Number(playerState.leaderCard?.maxHealth ?? playerState.leaderCard?.MaxHealth ?? playerState.leaderCard?.health ?? playerState.leaderCard?.Health ?? 20);
        const maxHp = Number.isFinite(maxHpRaw) && maxHpRaw > 0 ? Math.trunc(maxHpRaw) : 20;
        const hpPercent = Math.max(0, Math.min(100, (Math.max(0, Number(currentHp) || 0) / maxHp) * 100));
        const hpContainer = statsContainer.querySelector('.hp-display');
        const hpValueEl = statsContainer.querySelector('.hp-value');
        const hpMaxEl = statsContainer.querySelector('.hp-max');
        if (hpValueEl) {
            hpValueEl.textContent = currentHp;
            hpValueEl.style.color = currentHp <= 5 ? '#fda4af' : '';
        }
        if (hpMaxEl) {
            hpMaxEl.textContent = `/ ${maxHp}`;
        }
        if (hpContainer) {
            applyLeaderHpBarWithGhostSettle(hpContainer, hpPercent, Number(currentHp) <= 5);
        }

        const currentPp = playerState.pp ?? 0;
        const maxPp = playerState.maxPP ?? 0;
        updatePpDisplay(playerState.id, currentPp, maxPp, playerState.ppBoostArmed === true);

        const maxEp = (playerState.maxEP && playerState.maxEP > 0) ? playerState.maxEP : 2;
        updateEpDisplay(playerState.id, playerState.ep, maxEp);

        const maxSep = (playerState.maxSEP && playerState.maxSEP > 0) ? playerState.maxSEP : 2;
        updateSepDisplay(playerState.id, playerState.sep, maxSep);

        updatePpBoostDisplay(playerState.id, playerState.ppBoostUsesAvailable ?? 0, playerState.ppBoostArmed === true);

        const nextDeckCount = playerState.deckCount ?? playerState.DeckCount ?? 0;
        const nextHandCount = Array.isArray(playerState.hand)
            ? playerState.hand.length
            : (Array.isArray(playerState.Hand) ? playerState.Hand.length : 0);
        const nextGraveyardCount = getPlayerCounterValue(
            playerState,
            'graveyard',
            playerState.graveyardCount
            ?? playerState.GraveyardCount
            ?? playerState.graveyard?.length
            ?? 0
        );
        const nextBloodPoolCount = getPlayerCounterValue(
            playerState,
            'bloodpool',
            playerState.bloodPool
            ?? playerState.BloodPool
            ?? 0
        );
        const nextComboCount = getPlayerCounterValue(
            playerState,
            'combo',
            playerState.combo
            ?? playerState.Combo
            ?? 0
        );
        const nextRallyCount = getPlayerCounterValue(
            playerState,
            'rally',
            playerState.rally
            ?? playerState.Rally
            ?? 0
        );

        updateDeckCountDisplay(playerState.id, nextDeckCount);
        updateHandCountDisplay(playerState.id, nextHandCount);
        updateGraveyardCountDisplay(playerState.id, nextGraveyardCount);
        updateBloodPoolDisplay(playerState.id, nextBloodPoolCount);
        updateComboDisplay(playerState.id, nextComboCount);
        updateRallyDisplay(playerState.id, nextRallyCount);

        const counterScope = cornerPod ?? statsContainer;
        counterScope.querySelector('.blood-box')?.classList.toggle('is-zero', nextBloodPoolCount <= 0);
        counterScope.querySelector('.combo-box')?.classList.toggle('is-zero', nextComboCount <= 0);
        counterScope.querySelector('.rally-box')?.classList.toggle('is-zero', nextRallyCount <= 0);
        renderCornerExtraCounters(playerState, prefix);

        const isLocal = prefix === 'player1';
        const displayName = resolvePlayerDisplayName(playerState, isLocal);
        const classKey = normalizeCardClassKey(
            playerState?.class
            ?? playerState?.Class
            ?? playerState?.deckClass
            ?? playerState?.DeckClass
            ?? 'Neutral'
        ) || 'Neutral';
        const classLabel = resolvePlayerClassLabel(playerState);
        const leaderCard = playerState.leaderCard;
        const leaderCardId = Number(leaderCard?.id ?? leaderCard?.Id ?? 0);
        const isCustomMirageLeader = isMirageMode(gameState) && leaderCardId > 0;

        const classIconEl = document.getElementById(`${prefix}-class-icon`);
        if (classIconEl) {
            const iconUrl = CLASS_ICON_URL_MAP[classKey] ?? CLASS_ICON_URL_MAP.Neutral;
            if (iconUrl) {
                classIconEl.src = iconUrl;
                classIconEl.hidden = false;
            } else {
                classIconEl.hidden = true;
            }
        }

        const classEl = statsContainer.querySelector('.class-name');
        if (classEl) {
            classEl.textContent = isCustomMirageLeader
                ? (leaderCard.name ?? leaderCard.Name ?? classLabel ?? 'Neutral')
                : (classLabel || 'Neutral');
        }

        const playerNameEl = statsContainer.querySelector('.player-name');
        if (playerNameEl) {
            playerNameEl.textContent = '';
            const nameTextEl = document.createElement('span');
            nameTextEl.className = 'player-name-text';
            nameTextEl.textContent = displayName;
            playerNameEl.appendChild(nameTextEl);

            const orderBadge = isMirageMode(gameState) ? null : resolvePlayerOrderBadge(playerState);
            if (orderBadge) {
                const badgeEl = document.createElement('span');
                badgeEl.className = `player-order-badge ${orderBadge.className}`;
                badgeEl.textContent = orderBadge.text;
                playerNameEl.appendChild(badgeEl);
            }
        }

        const avatarEl = statsContainer.querySelector('.stats-avatar');
        if (avatarEl) {
            const avatar = resolvePlayerAvatar(playerState, isLocal, displayName);
            const leaderArt = isCustomMirageLeader
                ? String(leaderCard?.artPreview ?? leaderCard?.ArtPreview ?? '').trim()
                : '';
            const effectiveImageSrc = leaderArt || avatar.imageSrc;
            avatarEl.textContent = '';
            avatarEl.classList.toggle('mirage-leader-avatar', isCustomMirageLeader && Boolean(effectiveImageSrc));
            avatarEl.classList.toggle('has-image', Boolean(effectiveImageSrc));

            if (effectiveImageSrc) {
                const imageEl = document.createElement('img');
                imageEl.className = 'stats-avatar-img';
                imageEl.src = effectiveImageSrc;
                imageEl.alt = `${displayName} avatar`;
                imageEl.onerror = () => {
                    avatarEl.classList.remove('has-image');
                    avatarEl.classList.remove('mirage-leader-avatar');
                    avatarEl.textContent = avatar.fallbackText;
                };
                avatarEl.appendChild(imageEl);
            } else {
                avatarEl.textContent = avatar.fallbackText;
            }
        }

        renderMirageLeaderSkillPod(playerState, statsContainer, isLocal, prefix);
        updateEmblemDisplay(playerState, prefix);
    } catch (error) {
        console.error(`UI Error rendering stats for ${prefix}:`, error);
    }
}
function renderPlayerHand(handCards = [], handElement) {
    if (!handElement) {
        return;
    }
    const isLocal = handElement === player1HandEl;
    if (isLocal) {
        applyHandOverlapCssVars(handElement, handCards.length);
    } else {
        handElement.classList.add('is-face-up-opponent');
    }

    const existingHandEls = Array.from(handElement.querySelectorAll(':scope > .card[data-uid]'));
    const hadPreviousCards = existingHandEls.length > 0;
    const previousUidSet = new Set(existingHandEls.map(el => normalizeMotionUidKey(el.dataset.uid)));
    const canAnimateTurnDraw = hadPreviousCards
        && isGamePageActive()
        && !isMirageMode(gameState)
        && (gameState.phase === 'TurnStart' || gameState.phase === 'Main')
        && Number(gameState.turnNumber ?? 0) > 0;

    const entries = buildFaceUpHandEntries(handCards, isLocal);
    const newlyDrawnUids = [];
    if (canAnimateTurnDraw) {
        entries.forEach(entry => {
            const uid = entry?.element?.dataset?.uid;
            const normUid = normalizeMotionUidKey(uid);
            if (normUid && !previousUidSet.has(normUid) && !hasActiveTimedUidFlag(suppressedEnterUids, uid)) {
                markTimedUidFlag(suppressedEnterUids, uid, 1500);
                newlyDrawnUids.push(uid);
            }
        });
    }

    renderAnimatedCardCollection(handElement, entries, {
        animateEnter: true,
        animateExit: true,
        animateInitialEnter: false,
        animateMove: true
    });

    if (newlyDrawnUids.length > 0) {
        const motion = getBattleMotionController();
        newlyDrawnUids.forEach((uid, idx) => {
            const targetEl = handElement.querySelector(`.card[data-uid="${uid}"]`);
            if (targetEl) {
                motion.playHandRightSlideIn({
                    targetUid: uid,
                    targetEl,
                    delayMs: Math.min(idx * 50, 200),
                    durationMs: 240,
                    pulseTone: 'cyan'
                });
            }
        });
    }

    if (isLocal) {
        syncCommittedHandPlayVisual(handCards);
    }
}

// --- 渲染对方手牌（只显示牌背）---
function renderOpponentHand(handCards = [], handElement) {
    if (!handElement) {
        return;
    }
    handElement.classList.remove('is-face-up-opponent');
    const entries = buildFaceDownHandEntries(handCards);
    renderAnimatedCardCollection(handElement, entries, {
        animateEnter: false,
        animateExit: false,
        animateMove: false
    });
}

function renderField(fieldCards = [], fieldElement, isOpponent = false) {
    if (!fieldElement) {
        return;
    }
    void isOpponent;
    const entries = buildFieldEntries(fieldCards);
    syncFieldZoneDensity(fieldElement, entries.length);
    renderAnimatedCardCollection(fieldElement, entries);
}
// --- 创建卡牌 DOM 元素的辅助函数 ---
function isEnhanceModeValue(value) {
    if (value === 1) return true;
    if (typeof value === 'string') {
        return value.trim().toLowerCase() === 'enhance';
    }
    return false;
}

function isBasePlayModeValue(value) {
    if (value === null || value === undefined) return true;
    if (value === 0) return true;
    if (typeof value === 'string') {
        const normalized = value.trim().toLowerCase();
        return normalized === '' || normalized === 'base';
    }
    return false;
}

function resolveCardCostDisplay(cardInstance, context) {
    const baseCostRaw = context === 'field'
        ? (cardInstance.cost ?? cardInstance.Cost ?? cardInstance.currentCost ?? cardInstance.CurrentCost)
        : (cardInstance.currentCost ?? cardInstance.CurrentCost ?? cardInstance.cost ?? cardInstance.Cost);
    const baseCost = Number.isFinite(baseCostRaw) ? baseCostRaw : (baseCostRaw ?? '?');
    let displayCost = baseCost;
    let enhanceActive = false;
    let modeVisual = playModeVisual(null);

    if (context === 'hand') {
        const cardUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
        const ownerPlayerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId ?? gameState.localPlayerId;
        const playabilityInfo = cardUid ? getPlayabilityForCard(cardUid, ownerPlayerId) : null;
        const selectedCost = playabilityInfo?.selectedPlayCost;
        modeVisual = playModeVisual(playabilityInfo?.selectedPlayMode, cardInstance.name ?? cardInstance.Name ?? '');
        enhanceActive = !!modeVisual.label;
        if (playabilityInfo && !isBasePlayModeValue(playabilityInfo.selectedPlayMode) && Number.isFinite(selectedCost)) {
            displayCost = selectedCost;
            // Show Enhance visuals whenever Enhance mode is selected, even when
            // base cost equals enhance cost.
            enhanceActive = !isBasePlayModeValue(playabilityInfo.selectedPlayMode);
        }
    }

    return { displayCost, enhanceActive, modeVisual };
}

function resolveCardContext(cardInstance, context) {
    if (context) return context;
    const zone = cardInstance?.zone ?? cardInstance?.Zone;
    if (typeof zone === 'string') {
        return zone.toLowerCase();
    }
    return '';
}

function getCardUid(cardInstance) {
    return cardInstance?.uid ?? cardInstance?.InstanceId ?? cardInstance?.instanceId ?? null;
}

function getUseActionState(cardInstance, context) {
    const empty = {
        showUse: false,
        usePlayable: true,
        useFailureReason: '',
        useUid: null,
        usePlayabilityKnown: false,
        useSyncPending: false,
        useHint: ''
    };

    if (!cardInstance || isAwaitingPlayerChoice || gameState.isSpectator) {
        return empty;
    }

    const resolvedContext = resolveCardContext(cardInstance, context);
    if (resolvedContext !== 'hand') {
        return empty;
    }

    const localPlayerId = gameState.localPlayerId ?? uiCurrentPlayerId;
    if (!localPlayerId) {
        return empty;
    }

    const playerState = getPlayerState(localPlayerId);
    const isMirageRecruit = gameState.mode === 'Mirage'
        && gameState.phase === 'Recruit'
        && playerState?.mirage?.isReady !== true;
    const isLocalTurn = gameState.currentPlayerId === localPlayerId;
    if (!playerState || (!isMirageRecruit && (!isLocalTurn || gameState.phase !== 'Main'))) {
        return empty;
    }

    const ownerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId;
    if (ownerId !== localPlayerId) {
        return empty;
    }

    const useUid = getCardUid(cardInstance);
    if (!useUid) {
        return empty;
    }

    const playabilityInfo = getPlayabilityForCard(useUid, ownerId);
    const usePlayabilityKnown = Boolean(playabilityInfo);
    const usePlayable = usePlayabilityKnown ? playabilityInfo.isPlayable !== false : true;
    const useFailureReason = usePlayabilityKnown ? (playabilityInfo?.failureReason ?? '') : '';
    const useSyncPending = !usePlayabilityKnown;
    const useHint = useSyncPending ? USE_PLAYABILITY_SYNC_PENDING_REASON : '';

    return {
        showUse: true,
        usePlayable,
        useFailureReason,
        useUid,
        usePlayabilityKnown,
        useSyncPending,
        useHint
    };
}

const ACT_PLAYABILITY_SYNC_PENDING_REASON = '启动状态同步中，请稍候。';
const USE_PLAYABILITY_SYNC_PENDING_REASON = '可用性同步中，请稍候。';

function getCardActionState(cardInstance, context) {
    const empty = {
        showSuperEvolve: false,
        showEvolve: false,
        showAct: false,
        showFusion: false,
        showUse: false,
        actUsedThisTurn: false,
        fusionUsedThisTurn: false,
        actPlayabilityKnown: false,
        actPlayable: false,
        actFailureReason: '',
        actCost: null,
        actAbilities: [],
        fusionAbilities: [],
        usePlayable: true,
        useFailureReason: '',
        useSyncPending: false,
        useHint: ''
    };
    if (!cardInstance) return empty;

    const resolvedContext = resolveCardContext(cardInstance, context);
    const isFieldContext = resolvedContext === 'field';
    const isHandContext = resolvedContext === 'hand';
    const useActionState = getUseActionState(cardInstance, resolvedContext);

    const currentPlayerId = getCurrentPlayerId();
    const playerState = getPlayerState(currentPlayerId);
    const cardOwnerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId;

    if (isMirageMode(gameState)) {
        const localPlayer = getPlayerState(gameState.localPlayerId);
        const isLocalReady = Boolean(localPlayer?.mirage?.isReady ?? localPlayer?.mirage?.IsReady);
        if (!localPlayer || cardOwnerId !== gameState.localPlayerId || gameState.phase !== 'Recruit' || isLocalReady) {
            return empty;
        }
        const cardType = cardInstance.type ?? cardInstance.Type;
        const isLeaderCard = equalsIgnoreCase(cardType, 'Leader')
            || resolvedContext === 'leader'
            || equalsIgnoreCase(cardInstance.zone ?? cardInstance.Zone, 'Leader');
        if (isLeaderCard || isFieldContext) {
            const actAbilityList = Array.isArray(cardInstance.actAbilities)
                ? cardInstance.actAbilities
                : (Array.isArray(cardInstance.ActAbilities) ? cardInstance.ActAbilities : []);
            const actsThisTurn = Number(cardInstance.actsThisTurn ?? cardInstance.ActsThisTurn ?? 0);
            const maxActsPerTurn = Number(cardInstance.maxActsPerTurn ?? cardInstance.MaxActsPerTurn ?? 1);
            const actUsedThisTurn = Number.isFinite(actsThisTurn) && Number.isFinite(maxActsPerTurn) && actsThisTurn >= maxActsPerTurn;
            const cardUid = getCardUid(cardInstance);
            const actInfo = cardUid ? getActPlayabilityForCard(cardUid, cardOwnerId) : null;
            const actPlayabilityKnown = Boolean(actInfo);
            const actPlayable = actPlayabilityKnown ? actInfo?.isPlayable === true : false;
            const actFailureReason = actPlayabilityKnown ? (actInfo?.failureReason ?? '') : ACT_PLAYABILITY_SYNC_PENDING_REASON;
            const actCost = Number.isFinite(actInfo?.actCost) ? actInfo.actCost : null;
            const actAbilities = actPlayabilityKnown
                ? (Array.isArray(actInfo?.abilities) ? actInfo.abilities : [])
                : actAbilityList;
            const hasActCapability = actAbilityList.length > 0
                || (Array.isArray(actInfo?.abilities) && actInfo.abilities.length > 0);
            return {
                ...empty,
                showAct: hasActCapability,
                actUsedThisTurn,
                actPlayabilityKnown,
                actPlayable,
                actFailureReason,
                actCost,
                actAbilities
            };
        }
        return {
            ...empty,
            showUse: useActionState.showUse,
            usePlayable: useActionState.usePlayable,
            useFailureReason: useActionState.useFailureReason,
            useSyncPending: useActionState.useSyncPending,
            useHint: useActionState.useHint
        };
    }

    if (!playerState ||
        currentPlayerId !== gameState.localPlayerId ||
        currentPlayerId !== cardOwnerId ||
        gameState.phase !== 'Main') {
        return empty;
    }

    const cardType = cardInstance.type ?? cardInstance.Type;
    const isEvolved = (cardInstance.isEvolved ?? cardInstance.IsEvolved) === true;
    const canEvolve = (cardInstance.canEvolve ?? cardInstance.CanEvolve) === true;
    const keywordList = Array.isArray(cardInstance.keywords)
        ? cardInstance.keywords
        : (Array.isArray(cardInstance.Keywords) ? cardInstance.Keywords : []);
    const hasFreeEvolve = keywordList.some(keyword => equalsIgnoreCase(keyword, 'FreeEvolve'));
    const currentEp = Number(playerState.ep ?? 0);
    const canPayEvolveCost = Number.isFinite(currentEp) && currentEp > 0;
    const canUseEvolutionPointOrFreeEvolve = hasFreeEvolve || canPayEvolveCost;
    const actAbilityList = Array.isArray(cardInstance.actAbilities)
        ? cardInstance.actAbilities
        : (Array.isArray(cardInstance.ActAbilities) ? cardInstance.ActAbilities : []);
    const fusionAbilityList = Array.isArray(cardInstance.fusionAbilities)
        ? cardInstance.fusionAbilities
        : (Array.isArray(cardInstance.FusionAbilities) ? cardInstance.FusionAbilities : []);
    const actsThisTurn = Number(cardInstance.actsThisTurn ?? cardInstance.ActsThisTurn ?? 0);
    const maxActsPerTurn = Number(cardInstance.maxActsPerTurn ?? cardInstance.MaxActsPerTurn ?? 1);
    const fusionsThisTurn = Number(cardInstance.fusionsThisTurn ?? cardInstance.FusionsThisTurn ?? 0);
    const maxFusionsPerTurn = Number(cardInstance.maxFusionsPerTurn ?? cardInstance.MaxFusionsPerTurn ?? 1);
    const actUsedThisTurn = Number.isFinite(actsThisTurn) && Number.isFinite(maxActsPerTurn) && actsThisTurn >= maxActsPerTurn;
    const fusionUsedThisTurn = Number.isFinite(fusionsThisTurn) && Number.isFinite(maxFusionsPerTurn) && fusionsThisTurn >= maxFusionsPerTurn;
    const cardUid = getCardUid(cardInstance);
    const actInfo = cardUid ? getActPlayabilityForCard(cardUid, cardOwnerId) : null;
    const actPlayabilityKnown = Boolean(actInfo);
    const actPlayable = actPlayabilityKnown ? actInfo?.isPlayable === true : true;
    const actFailureReason = actPlayabilityKnown
        ? (actInfo?.failureReason ?? '')
        : '';
    const actCost = Number.isFinite(actInfo?.actCost) ? actInfo.actCost : null;
    const actAbilities = actPlayabilityKnown
        ? (Array.isArray(actInfo?.abilities) ? actInfo.abilities : [])
        : actAbilityList;
    const hasActCapability = actAbilityList.length > 0
        || (Array.isArray(actInfo?.abilities) && actInfo.abilities.length > 0);

    return {
        showSuperEvolve: (
            isFieldContext &&
            playerState.sep > 0 &&
            playerState.canSuperEvolve === true &&
            cardType === 'Follower' &&
            isEvolved === false &&
            canEvolve === true
        ),
        showEvolve: (
            isFieldContext &&
            canUseEvolutionPointOrFreeEvolve &&
            playerState.canEvolve === true &&
            cardType === 'Follower' &&
            isEvolved === false &&
            canEvolve === true
        ),
        showAct: isFieldContext && hasActCapability,
        showFusion: isHandContext && fusionAbilityList.length > 0,
        showUse: useActionState.showUse,
        actUsedThisTurn,
        fusionUsedThisTurn,
        actPlayabilityKnown,
        actPlayable,
        actFailureReason,
        usePlayable: useActionState.usePlayable,
        useFailureReason: useActionState.useFailureReason,
        useSyncPending: useActionState.useSyncPending,
        useHint: useActionState.useHint,
        actCost,
        actAbilities,
        fusionAbilities: fusionAbilityList
    };
}

const CARD_ACTION_BUTTON_SELECTOR = '.super-evolve-button, .evolve-button, .fusion-button, .act-button, .use-button';
const CARD_ACTION_BUTTON_TOP_START = 28;
const CARD_ACTION_BUTTON_VERTICAL_GAP = 28;
const CARD_ACTION_BUTTON_PRIORITY = Object.freeze({
    'super-evolve-button': 10,
    'evolve-button': 20,
    'use-button': 30,
    'fusion-button': 40,
    'act-button': 50
});

function getActionButtonPriority(button) {
    if (!button?.classList) return Number.MAX_SAFE_INTEGER;
    if (button.classList.contains('super-evolve-button')) return CARD_ACTION_BUTTON_PRIORITY['super-evolve-button'];
    if (button.classList.contains('evolve-button')) return CARD_ACTION_BUTTON_PRIORITY['evolve-button'];
    if (button.classList.contains('use-button')) return CARD_ACTION_BUTTON_PRIORITY['use-button'];
    if (button.classList.contains('fusion-button')) return CARD_ACTION_BUTTON_PRIORITY['fusion-button'];
    if (button.classList.contains('act-button')) return CARD_ACTION_BUTTON_PRIORITY['act-button'];
    return Number.MAX_SAFE_INTEGER;
}

function applyCardActionButtonStack(cardElement) {
    if (!cardElement) return;
    const orderedButtons = Array.from(cardElement.querySelectorAll(CARD_ACTION_BUTTON_SELECTOR))
        .map((button, index) => ({ button, index, priority: getActionButtonPriority(button) }))
        .sort((a, b) => (a.priority - b.priority) || (a.index - b.index))
        .map(item => item.button);
    orderedButtons.forEach((button, index) => {
        const top = CARD_ACTION_BUTTON_TOP_START + (index * CARD_ACTION_BUTTON_VERTICAL_GAP);
        button.style.setProperty('--action-button-top', `${top}px`);
    });
}

const keywordBadgeConfig = new Map([
    ['ward', badgeWardUrl],
    ['bane', badgeBaneUrl],
    ['drain', badgeDrainUrl],
    ['ambush', badgeAmbushUrl],
    ['pressure', badgePressureUrl],
    ['aura', badgeAuraUrl],
    ['shield', badgeShieldUrl],
    ['abilityresist', badgeAbilityResistUrl],
    ['shackle', badgeShackleUrl],
]);

function escapeAttributeValue(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/"/g, '&quot;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

function buildKeywordBadgesHtml(cardInstance, context) {
    const resolvedContext = resolveCardContext(cardInstance, context);
    if (resolvedContext !== 'field') return '';

    const keywords = cardInstance?.keywords ?? cardInstance?.Keywords ?? [];
    if (!Array.isArray(keywords) || keywords.length === 0) {
        return '';
    }

    const badges = [];
    const seen = new Set();
    keywords.forEach(keyword => {
        const normalized = String(keyword ?? '').trim().toLowerCase();
        if (!normalized || seen.has(normalized)) return;
        const badgeSrc = keywordBadgeConfig.get(normalized);
        if (!badgeSrc) return;
        seen.add(normalized);
        const label = escapeAttributeValue(getCardKeywordLabel(keyword) || normalizeEnumName(keyword));
        const wardClass = normalized === 'ward' ? ' is-ward' : '';
        badges.push(
            `<div class="keyword-badge${wardClass}" title="${label}" data-keyword="${normalized}">` +
            `<img class="badge-icon" src="${badgeSrc}" alt="${label}">` +
            `</div>`
        );
    });

    if (badges.length === 0) return '';
    const denseClass = badges.length >= 4 ? ' is-dense' : '';
    return `<div class="keyword-badges${denseClass}">${badges.join('')}</div>`;
}

function createCardElement(cardInstance, context = 'hand', displayCostOverride = null, playModeOverride = null) { // context: 'hand', 'field'
    if (!cardInstance) {
        return null;
    }

    const cardDiv = document.createElement('div');
    cardDiv.classList.add('card'); // Base card class
    const cardType = cardInstance.Type ?? cardInstance.type;
    if (cardType) {
        cardDiv.classList.add(cardType.toLowerCase().split('_')[0]); // Handle 'Follower_Evolved' -> 'follower'
    }
    cardDiv.dataset.uid = cardInstance.uid ?? cardInstance.InstanceId;

    const isEvolvedVisual = (cardInstance.isEvolved ?? cardInstance.IsEvolved) === true
        || (cardInstance.isMirageTriple ?? cardInstance.IsMirageTriple) === true;
    if ((context === 'hand' || context === 'field') && isEvolvedVisual) {
        cardDiv.classList.add('evolved');
    }

    const isSuperEvolvedField = context === 'field' && (cardInstance.isSuperEvolved ?? cardInstance.IsSuperEvolved) === true;
    if (context === 'hand') {
        cardDiv.classList.add('hand-card');
    } else if (context === 'field') {
        cardDiv.classList.add('field-card');
        if (isSuperEvolvedField) {
            cardDiv.classList.add('super-evolved');
        }
    }

    applyAttackReadinessClasses(cardDiv, cardInstance, context);

    const rawKeywordList = Array.isArray(cardInstance.keywords)
        ? cardInstance.keywords
        : (Array.isArray(cardInstance.Keywords) ? cardInstance.Keywords : []);
    if (context === 'field' && rawKeywordList.some(k => equalsIgnoreCase(k, 'Ward'))) {
        cardDiv.classList.add('has-ward');
    }
    if (rawKeywordList.some(k => equalsIgnoreCase(k, 'Shield'))) {
        cardDiv.classList.add('has-shield');
    }

    const costInfo = resolveCardCostDisplay(cardInstance, context);
    const effectiveDisplayCost = displayCostOverride ?? costInfo.displayCost;
    const overrideModeVisual = playModeOverride != null
        ? playModeVisual(playModeOverride, cardInstance.name ?? cardInstance.Name ?? '')
        : null;
    const activeModeVisual = overrideModeVisual?.label ? overrideModeVisual : costInfo.modeVisual;
    const isEnhanceActive = overrideModeVisual ? Boolean(overrideModeVisual.label) : costInfo.enhanceActive;
    if (isEnhanceActive) {
        cardDiv.classList.add('enhance-active');
    }

    cardDiv.classList.toggle('alternate-active', activeModeVisual.alternate);
    cardDiv.dataset.playModeLabel = activeModeVisual.label;
    cardDiv.style.setProperty('--play-mode-label', JSON.stringify(activeModeVisual.label));

    const countdownValue = cardInstance.countdown
        ?? cardInstance.CurrentCountdown
        ?? cardInstance.currentCountdown
        ?? cardInstance.Countdown;
    const isFollowerType = equalsIgnoreCase(cardType, 'Follower') || equalsIgnoreCase(cardType, 'Follower_Evolved');
    const isAmuletType = equalsIgnoreCase(cardType, 'Amulet');
    const isSpellType = equalsIgnoreCase(cardType, 'Spell');
    const hasCountdownKeyword = rawKeywordList.some(keyword => equalsIgnoreCase(keyword, 'Countdown'));
    const hasEarthsigilKeyword = rawKeywordList.some(keyword => equalsIgnoreCase(keyword, 'EarthSigil'));
    const showCountdown = isAmuletType && hasCountdownKeyword
        && (countdownValue !== null && countdownValue !== undefined);
    const earthsigilValue = cardInstance.earthsigil
        ?? cardInstance.Earthsigil
        ?? cardInstance.EarthSigil
        ?? cardInstance.counters?.earthsigil
        ?? cardInstance.Counters?.Earthsigil
        ?? 0;
    const showEarthsigil = isAmuletType && hasEarthsigilKeyword;
    const showEvidence = isAmuletType && rawKeywordList.some(keyword => equalsIgnoreCase(keyword, 'Evidence'));
    const evidenceValue = cardInstance.evidence ?? cardInstance.Evidence ?? cardInstance.counters?.evidence ?? cardInstance.Counters?.evidence ?? 0;

    const keywordBadgesHtml = buildKeywordBadgesHtml(cardInstance, context);
    const archetypeOrnamentHtml = isAmuletType
        ? '<span class="bui-amulet-gem-dot" aria-hidden="true"></span>'
        : (isSpellType ? '<span class="bui-spell-crest" aria-hidden="true">─ ✦ ─</span>' : '');
    const sevoRingHtml = isSuperEvolvedField
        ? '<span class="bui-sevo-ring" aria-hidden="true"></span>'
        : '';

    const isMirageHandZeroCostFollower = context === 'hand'
        && isMirageMode(gameState)
        && isFollowerType
        && Number(effectiveDisplayCost) === 0;
    const isMirageTierBadge = (context === 'field' && isMirageMode(gameState)) || isMirageHandZeroCostFollower;
    const rawBaseTier = cardInstance.cost ?? cardInstance.Cost ?? effectiveDisplayCost;
    const mirageFieldTier = Math.max(1, Math.trunc(Number(rawBaseTier) || 1));
    const costBadgeHtml = isMirageTierBadge
        ? `<div class="cost is-mirage-tier ${mirageFieldTier >= 7 ? 'is-mythic' : ''}" data-tier="${mirageFieldTier}" title="酒馆星级 ★${mirageFieldTier}">${buildMirageTierBadgeInnerHtml(mirageFieldTier)}</div>`
        : `<div class="cost"><span>${effectiveDisplayCost ?? '?'}</span></div>`;

    cardDiv.innerHTML = `
          <div class="card-art"></div>
          ${archetypeOrnamentHtml}
          ${sevoRingHtml}
          <span class="target-hover-ring" aria-hidden="true"></span>
          ${costBadgeHtml}
          <div class="name">${cardInstance.Name ?? cardInstance.name ?? 'Unknown Card'}</div>
          ${
        isFollowerType ?
            `<div class="stats">
                <span class="attack">${cardInstance.CurrentAttack ?? cardInstance.currentAttack ?? '?'}</span>
                <span class="health">${cardInstance.CurrentHealth ?? cardInstance.currentHealth ?? '?'}</span>
            </div>`
            : (showCountdown || showEarthsigil || showEvidence) ?
                `<div class="stats stats-amulet">
                ${showEvidence ? `<span class="evidence" title="证据数">${evidenceValue}</span>` : ''}
                ${showEarthsigil ? `<span class="earthsigil">${earthsigilValue}</span>` : ''}
                ${showCountdown ? `<span class="countdown">${countdownValue}</span>` : ''}
            </div>`
                : ''
        }
        ${keywordBadgesHtml}
    `;

    const previewPath = cardInstance.artPreview ?? cardInstance.ArtPreview ?? '';
    const artEl = cardDiv.querySelector('.card-art');
    if (artEl) {
        applyArtBackground(artEl, previewPath, { priority: 'normal' });
    }

    // 注意: 'playable', 'selected-attacker', 'targetable' 等交互状态相关的 class
    // 应该由 renderHighlights 函数根据当前游戏状态来动态添加或移除。

    // --- Add Action Buttons Conditionally (UI Side) ---
    const actionState = getCardActionState(cardInstance, context);
    if (actionState.showSuperEvolve) {
        const superEvolveBtn = document.createElement('button');
        superEvolveBtn.className = 'super-evolve-button';
        superEvolveBtn.textContent = getBattleActionLabel('superEvolve');
        superEvolveBtn.dataset.superEvolveUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
        cardDiv.appendChild(superEvolveBtn);
    }

    if (actionState.showEvolve) {
        const evolveBtn = document.createElement('button');
        evolveBtn.className = 'evolve-button';
        evolveBtn.textContent = getBattleActionLabel('evolve');
        evolveBtn.dataset.evolveUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
        cardDiv.appendChild(evolveBtn);
    }

    if (actionState.showUse) {
        const useBtn = document.createElement('button');
        useBtn.className = 'use-button';
        useBtn.textContent = getBattleActionLabel('use');
        useBtn.dataset.useUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
        useBtn.disabled = actionState.usePlayable === false;
        if (actionState.usePlayable === false && actionState.useFailureReason) {
            useBtn.title = actionState.useFailureReason;
        } else if (actionState.useHint) {
            useBtn.title = actionState.useHint;
        } else {
            useBtn.removeAttribute('title');
        }
        cardDiv.appendChild(useBtn);
    }

    if (actionState.showFusion) {
        const fusionAbilityEntries = Array.isArray(actionState.fusionAbilities) && actionState.fusionAbilities.length > 0
            ? actionState.fusionAbilities
            : [null];
        fusionAbilityEntries.forEach((fusionAbility, index) => {
            const fusionBtn = document.createElement('button');
            fusionBtn.className = 'fusion-button';
            const fusionLabel = getBattleActionLabel('fusion', index, fusionAbilityEntries.length);
            fusionBtn.textContent = fusionLabel;
            fusionBtn.dataset.fusionUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
            fusionBtn.disabled = actionState.fusionUsedThisTurn === true;
            if (fusionBtn.disabled) {
                fusionBtn.title = '该卡牌本回合已融合过。';
            }
            const abilityRuntimeId = fusionAbility?.runtimeId ?? fusionAbility?.RuntimeId ?? '';
            if (abilityRuntimeId) {
                fusionBtn.dataset.fusionAbilityId = abilityRuntimeId;
            }
            cardDiv.appendChild(fusionBtn);
        });
    }

    if (actionState.showAct) {
        const actAbilityEntries = getActAbilityEntries(actionState);
        actAbilityEntries.forEach((actAbility, index) => {
            const actBtn = document.createElement('button');
            actBtn.className = 'act-button';
            const actButtonState = resolveActButtonState(actionState, actAbility);
            const actLabel = getBattleActionLabel('act', index, actAbilityEntries.length);
            actBtn.textContent = actLabel;
            actBtn.disabled = actButtonState.disabled;
            if (actButtonState.title) {
                actBtn.title = actButtonState.title;
            } else {
                actBtn.removeAttribute('title');
            }
            actBtn.dataset.actUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
            const abilityRuntimeId = actAbility?.abilityRuntimeId ?? actAbility?.AbilityRuntimeId ?? '';
            if (abilityRuntimeId) {
                actBtn.dataset.actAbilityId = abilityRuntimeId;
            }
            cardDiv.appendChild(actBtn);
        });
    }
    applyCardActionButtonStack(cardDiv);

    if (context === 'field' && isMirageMode(gameState) && gameState.phase === 'Recruit' && !gameState.isSpectator) {
        const localPlayerId = gameState.localPlayerId;
        const localPlayer = getPlayerState(localPlayerId);
        const ownerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId;
        const isLocalReady = Boolean(localPlayer?.mirage?.isReady ?? localPlayer?.mirage?.IsReady);
        if (localPlayer && ownerId === localPlayerId && !isLocalReady) {
            const cardUid = String(cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId ?? '');
            const fieldCards = Array.isArray(localPlayer.field) ? localPlayer.field : [];
            const fieldIndex = fieldCards.findIndex(c => String(getCardUid(c) ?? '') === cardUid);
            const controlsDiv = document.createElement('div');
            controlsDiv.className = 'mirage-field-controls';
            if (fieldIndex > 0) {
                const leftBtn = document.createElement('button');
                leftBtn.type = 'button';
                leftBtn.className = 'mirage-move-left-btn';
                leftBtn.dataset.mirageMoveLeft = cardUid;
                leftBtn.textContent = '◀';
                leftBtn.title = '左移站位';
                controlsDiv.appendChild(leftBtn);
            }
            if (isFollowerType) {
                const sellBtn = document.createElement('button');
                sellBtn.type = 'button';
                sellBtn.className = 'mirage-sell-button';
                sellBtn.dataset.mirageSellUid = cardUid;
                sellBtn.textContent = '出售+1G';
                sellBtn.title = '出售该随从并获得1金币';
                controlsDiv.appendChild(sellBtn);
            }
            if (fieldIndex >= 0 && fieldIndex < fieldCards.length - 1) {
                const rightBtn = document.createElement('button');
                rightBtn.type = 'button';
                rightBtn.className = 'mirage-move-right-btn';
                rightBtn.dataset.mirageMoveRight = cardUid;
                rightBtn.textContent = '▶';
                rightBtn.title = '右移站位';
                controlsDiv.appendChild(rightBtn);
            }
            if (controlsDiv.childElementCount > 0) {
                cardDiv.appendChild(controlsDiv);
            }
        }
    }
    // --- END Action Buttons ---

    // 【修复】移除鼠标悬停时的可玩性检查请求，改为依赖服务器主动推送
    // 服务器会在玩家行动完成后自动推送手牌可玩性信息
    // --- Mouse Enter Listener Removed (Server now pushes playability proactively) ---
    // if (context === 'hand') {
    //     cardDiv.addEventListener('mouseenter', () => {
    //         // Only check for the local player's cards
    //         const ownerPlayer = getPlayerState(cardInstance.ownerId);
    //         if (ownerPlayer && ownerPlayer.id === gameState.localPlayerId) {
    //              requestPlayabilityCheck(cardInstance.uid); // Call the function from gameManager
    //         }
    //     });
    //     // Optional: Add mouseleave listener to clear highlights or cache?
    //     // cardDiv.addEventListener('mouseleave', () => { ... });
    // }
    // --- END Mouse Enter Listener ---

    return cardDiv;
}

function normalizeEnumName(value) {
    if (typeof value !== 'string') return value;
    const trimmed = value.trim();
    if (!trimmed) return trimmed;
    if (trimmed.includes('_')) {
        return trimmed
            .split('_')
            .map(part => part ? part[0].toUpperCase() + part.slice(1) : part)
            .join('_');
    }
    return trimmed[0].toUpperCase() + trimmed.slice(1);
}

function equalsIgnoreCase(a, b) {
    if (typeof a !== 'string' || typeof b !== 'string') return a === b;
    return a.toLowerCase() === b.toLowerCase();
}

let previewEnumLocalizationPromise = null;
function ensurePreviewEnumLocalization() {
    if (!previewEnumLocalizationPromise) {
        previewEnumLocalizationPromise = ensureEnumLocalizations(['CardRarity', 'CardClass', 'CardType'])
            .then(() => {
                const previewUid = battleCardPreviewEl?.dataset?.previewUid;
                if (!previewUid) return;
                const instance = findCardInstanceByUid(previewUid);
                if (instance) {
                    renderBattleCardPreview(instance);
                }
            })
            .catch(error => {
                console.warn('UI: Failed to load card enum labels.', error);
            });
    }
    return previewEnumLocalizationPromise;
}

let cardKeywordLabelLookup = null;
let cardKeywordLabelPromise = null;

function buildCardKeywordLabelLookup(entries) {
    const map = new Map();
    Object.entries(entries || {}).forEach(([key, label]) => {
        if (!key || !label) return;
        const normalizedKey = String(key).trim().toLowerCase();
        if (!normalizedKey) return;
        if (!map.has(normalizedKey)) {
            map.set(normalizedKey, String(label).trim());
        }
    });
    return map;
}

function ensureCardKeywordLabelLookup() {
    if (cardKeywordLabelLookup) {
        return Promise.resolve(cardKeywordLabelLookup);
    }
    if (!cardKeywordLabelPromise) {
        cardKeywordLabelPromise = fetch('/api/localization/enums/CardKeyword')
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                return response.json();
            })
            .then(entries => buildCardKeywordLabelLookup(entries))
            .catch(error => {
                console.warn('UI: Failed to load card keyword labels.', error);
                return new Map();
            })
            .then(map => {
                cardKeywordLabelLookup = map;
                const previewUid = battleCardPreviewEl?.dataset?.previewUid;
                if (previewUid) {
                    const instance = findCardInstanceByUid(previewUid);
                    if (instance) {
                        renderBattleCardPreview(instance);
                    }
                }
                return map;
            });
    }
    return cardKeywordLabelPromise;
}

function getCardKeywordLabel(keyword) {
    const raw = String(keyword ?? '').trim();
    if (!raw) return '';
    const normalizedKey = raw.toLowerCase();
    const label = cardKeywordLabelLookup?.get(normalizedKey);
    if (label) return label;
    return normalizeEnumName(raw);
}

const BATTLE_ACTION_LABELS = Object.freeze({
    use: '使用',
    superEvolve: '超进化',
    evolve: '进化',
    act: '启动',
    fusion: '融合'
});

function getBattleActionLabel(actionKey, index = 0, total = 1) {
    const baseLabel = BATTLE_ACTION_LABELS[actionKey] ?? '';
    if (!baseLabel) return '';
    return total > 1 ? `${baseLabel} ${index + 1}` : baseLabel;
}

function getActAbilityEntries(actionState) {
    if (Array.isArray(actionState?.actAbilities) && actionState.actAbilities.length > 0) {
        return actionState.actAbilities;
    }
    return [null];
}

function resolveActButtonState(actionState, actAbility) {
    if (!actionState || actionState.actUsedThisTurn) {
        return { disabled: true, title: '该卡牌本回合已启动过。' };
    }
    if (actionState.actPlayabilityKnown !== true) {
        return {
            disabled: isMirageMode(gameState),
            title: ACT_PLAYABILITY_SYNC_PENDING_REASON
        };
    }

    const abilityHasPlayability = actAbility && (
        Object.prototype.hasOwnProperty.call(actAbility, 'isPlayable')
        || Object.prototype.hasOwnProperty.call(actAbility, 'IsPlayable')
    );
    const rawAbilityPlayable = abilityHasPlayability
        ? (actAbility.isPlayable ?? actAbility.IsPlayable)
        : null;
    const abilityPlayable = rawAbilityPlayable === true;
    const fallbackPlayable = actionState.actPlayable === true;
    const isPlayable = abilityHasPlayability ? abilityPlayable : fallbackPlayable;
    const failureReason = actAbility?.failureReason
        ?? actAbility?.FailureReason
        ?? actionState.actFailureReason
        ?? '';

    return {
        disabled: isPlayable !== true,
        title: isPlayable !== true ? failureReason : ''
    };
}

function syncUseButtonForHandCard(cardInstance, cardElement) {
    if (!cardInstance || !cardElement) return;

    const useActionState = getUseActionState(cardInstance, 'hand');
    const existingUseButton = cardElement.querySelector('.use-button');

    if (!useActionState.showUse) {
        existingUseButton?.remove();
        applyCardActionButtonStack(cardElement);
        return;
    }

    const useUid = useActionState.useUid ?? getCardUid(cardInstance);
    if (!useUid) {
        existingUseButton?.remove();
        return;
    }

    const useBtn = existingUseButton ?? document.createElement('button');
    useBtn.className = 'use-button';
    useBtn.textContent = getBattleActionLabel('use');
    useBtn.dataset.useUid = String(useUid);
    useBtn.disabled = useActionState.usePlayable === false;

    if (useActionState.usePlayable === false && useActionState.useFailureReason) {
        useBtn.title = useActionState.useFailureReason;
    } else if (useActionState.useHint) {
        useBtn.title = useActionState.useHint;
    } else {
        useBtn.removeAttribute('title');
    }

    if (!existingUseButton) {
        cardElement.appendChild(useBtn);
    }
    applyCardActionButtonStack(cardElement);
}

function syncHandUseButtons() {
    if (!player1HandEl) return;

    const localPlayerId = gameState.localPlayerId ?? uiCurrentPlayerId;
    if (!localPlayerId) return;

    const localPlayer = getPlayerState(localPlayerId);
    const handCards = Array.isArray(localPlayer?.hand) ? localPlayer.hand : [];

    handCards.forEach(cardInstance => {
        const cardUid = getCardUid(cardInstance);
        if (!cardUid) return;
        const cardElement = player1HandEl.querySelector(`.card[data-uid="${cardUid}"]`);
        if (!cardElement) return;
        syncUseButtonForHandCard(cardInstance, cardElement);
    });
}

/**
 * Generates HTML for action buttons (Use, Evolve, Super Evolve, Act) based on card state.
 * @param {object} cardInstance
 */
function getActionButtonsHtml(cardInstance) {
    if (!cardInstance) return '';

    const cardUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
    const actionState = getCardActionState(cardInstance);
    if (!actionState.showUse && !actionState.showSuperEvolve && !actionState.showEvolve && !actionState.showAct && !actionState.showFusion) {
        return '';
    }

    let buttonsHtml = '';
    if (actionState.showUse) {
        const useDisabled = actionState.usePlayable === false;
        const useTitleText = useDisabled
            ? (actionState.useFailureReason || '')
            : (actionState.useHint || '');
        const useTitle = useTitleText
            ? ` title="${escapeAttributeValue(useTitleText)}"`
            : '';
        buttonsHtml += `<button class="use-button" ${useDisabled ? 'disabled' : ''}${useTitle} data-use-uid="${cardUid}">${getBattleActionLabel('use')}</button>`;
    }

    if (actionState.showSuperEvolve) {
        buttonsHtml += `<button class="super-evolve-button" data-super-evolve-uid="${cardUid}">${getBattleActionLabel('superEvolve')}</button>`;
    }

    if (actionState.showEvolve) {
        buttonsHtml += `<button class="evolve-button" data-evolve-uid="${cardUid}">${getBattleActionLabel('evolve')}</button>`;
    }

    if (actionState.showFusion) {
        const fusionAbilityEntries = Array.isArray(actionState.fusionAbilities) && actionState.fusionAbilities.length > 0
            ? actionState.fusionAbilities
            : [null];
        fusionAbilityEntries.forEach((fusionAbility, index) => {
            const label = getBattleActionLabel('fusion', index, fusionAbilityEntries.length);
            const fusionDisabled = actionState.fusionUsedThisTurn === true;
            const fusionTitle = fusionDisabled
                ? ` title="${escapeAttributeValue('该卡牌本回合已融合过。')}"`
                : '';
            const abilityRuntimeId = fusionAbility?.runtimeId ?? fusionAbility?.RuntimeId ?? '';
            const abilityAttr = abilityRuntimeId ? ` data-fusion-ability-id="${abilityRuntimeId}"` : '';
            buttonsHtml += `<button class="fusion-button" ${fusionDisabled ? 'disabled' : ''}${fusionTitle} data-fusion-uid="${cardUid}"${abilityAttr}>${label}</button>`;
        });
    }

    if (actionState.showAct) {
        const actAbilityEntries = getActAbilityEntries(actionState);
        actAbilityEntries.forEach((actAbility, index) => {
            const actButtonState = resolveActButtonState(actionState, actAbility);
            const actLabel = getBattleActionLabel('act', index, actAbilityEntries.length);
            const actDisabled = actButtonState.disabled;
            const actTitle = actButtonState.title
                ? ` title="${escapeAttributeValue(actButtonState.title)}"`
                : '';
            const abilityRuntimeId = actAbility?.abilityRuntimeId ?? actAbility?.AbilityRuntimeId ?? '';
            const abilityAttr = abilityRuntimeId ? ` data-act-ability-id="${abilityRuntimeId}"` : '';
            buttonsHtml += `<button class="act-button" ${actDisabled ? 'disabled' : ''}${actTitle} data-act-uid="${cardUid}"${abilityAttr}>${actLabel}</button>`;
        });
    }

    return buttonsHtml ? `<div class="preview-actions">${buttonsHtml}</div>` : '';
}

function getAttackReadinessClass(cardInstance, context) {
    if (context !== 'field' || !cardInstance) return null;

    const cardType = cardInstance.Type ?? cardInstance.type;
    const isFollowerType = equalsIgnoreCase(cardType, 'Follower') || equalsIgnoreCase(cardType, 'Follower_Evolved');
    const isAmuletType = equalsIgnoreCase(cardType, 'Amulet');

    if (!isFollowerType && !isAmuletType) return null;

    if (isAmuletType) {
        const actionState = getCardActionState(cardInstance, context);
        if (!actionState.showAct || actionState.actUsedThisTurn) return null;

        const cardUid = getCardUid(cardInstance);
        const cardOwnerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId;
        const actCache = cardUid ? getActPlayabilityForCard(cardUid, cardOwnerId) : null;
        if (!actCache) {
            // Avoid optimistic glow before act playability data is available.
            return null;
        }

        const actAbilityEntries = Array.isArray(actCache.abilities) ? actCache.abilities : [];
        const hasPlayableAct = actAbilityEntries.length > 0
            ? actAbilityEntries.some(actAbility => (actAbility?.isPlayable ?? actAbility?.IsPlayable) === true)
            : (actCache.isPlayable === true);

        return hasPlayableAct ? 'act-ready' : null;
    }

    const canAttack = (cardInstance.canAttack ?? cardInstance.CanAttack) === true;
    if (!canAttack) return null;

    const keywords = cardInstance.keywords ?? cardInstance.Keywords ?? [];
    const hasStorm = Array.isArray(keywords) && keywords.some(keyword => equalsIgnoreCase(keyword, 'Storm'));
    const hasRush = Array.isArray(keywords) && keywords.some(keyword => equalsIgnoreCase(keyword, 'Rush'));
    const isEvolved = (cardInstance.isEvolved ?? cardInstance.IsEvolved) === true;
    const zoneStayTurn = cardInstance.zoneStayTurn ?? cardInstance.ZoneStayTurn ?? 0;

    if (hasStorm || zoneStayTurn > 0) {
        return 'attack-ready-all';
    }

    if (hasRush || isEvolved) {
        return 'attack-ready-follower';
    }

    return 'attack-ready-follower';
}

function applyAttackReadinessClasses(cardElement, cardInstance, context) {
    if (!cardElement) return;
    cardElement.classList.remove('attack-ready-follower', 'attack-ready-all', 'act-ready');
    const readinessClass = getAttackReadinessClass(cardInstance, context);
    if (readinessClass) {
        cardElement.classList.add(readinessClass);
    }
}

const previewCounterLabelMap = new Map([
    ['spellboost', '魔力增幅数'],
    ['earthsigil', '土之印数'],
    ['evidence', '证据数'],
    ['combo', '连击数'],
    ['graveyard', '墓场数'],
    ['bloodpool', '血池'],
    ['rally', '连携数']
]);

function getCounterLabel(labelMap, key) {
    const rawKey = String(key ?? '').trim();
    if (!rawKey) return '';
    const systemLabel = previewCounterLabelMap.get(rawKey.toLowerCase());
    if (systemLabel) return systemLabel;
    if (rawKey.toLowerCase().startsWith('sequence.cursor.')) return '';
    if (labelMap && typeof labelMap === 'object') {
        const direct = labelMap[rawKey];
        if (typeof direct === 'string' && direct.trim()) {
            return direct.trim();
        }
        const lowerKey = rawKey.toLowerCase();
        for (const [entryKey, entryValue] of Object.entries(labelMap)) {
            if (String(entryKey).toLowerCase() !== lowerKey) continue;
            if (typeof entryValue === 'string' && entryValue.trim()) {
                return entryValue.trim();
            }
        }
    }
    const normalizedKey = rawKey.toLowerCase();
    const localized = previewCounterLabelMap.get(normalizedKey) || playerCounterLabelMap.get(normalizedKey);
    return localized || '';
}

function resolveCounterSummarySource(cardInstance) {
    if (!cardInstance) {
        return null;
    }

    const ownerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId ?? null;
    const ownerPlayer = ownerId ? getPlayerState(ownerId) : null;
    const isLeader = equalsIgnoreCase(cardInstance.type ?? cardInstance.Type, 'Leader');

    if (isLeader && ownerPlayer) {
        const ownerLabelMap = ownerPlayer.counterDisplayNames ?? ownerPlayer.CounterDisplayNames ?? {};
        const ownerCounterMap = ownerPlayer.counters ?? ownerPlayer.Counters ?? {};
        return {
            summaryEntries: ownerPlayer.counterSummaryEntries ?? ownerPlayer.CounterSummaryEntries ?? [],
            localCounterMap: null,
            localLabelMap: null,
            ownerCounterMap,
            ownerLabelMap,
            ownerReferenceKeys: Object.keys(ownerLabelMap),
            includeOwnerCounters: true,
            hideOwnerSystemKeys: true,
            showZeroSummaryEntries: true,
            showZeroOwnerEntries: true
        };
    }

    const localLabelMap = cardInstance.counterDisplayNames ?? cardInstance.CounterDisplayNames ?? {};
    return {
        summaryEntries: cardInstance.counterSummaryEntries ?? cardInstance.CounterSummaryEntries ?? [],
        localCounterMap: cardInstance.counters ?? cardInstance.Counters ?? cardInstance.counterMap ?? cardInstance.CounterMap,
        localLabelMap,
        ownerCounterMap: ownerPlayer?.counters ?? ownerPlayer?.Counters ?? null,
        ownerLabelMap: ownerPlayer?.counterDisplayNames ?? ownerPlayer?.CounterDisplayNames ?? null,
        ownerReferenceKeys: Object.keys(localLabelMap),
        includeOwnerCounters: !!ownerPlayer,
        hideOwnerSystemKeys: false,
        showZeroSummaryEntries: false,
        showZeroOwnerEntries: true
    };
}

function getCounterSummaryHtml(cardInstance) {
    if (!cardInstance) return '';
    const summarySource = resolveCounterSummarySource(cardInstance);
    if (!summarySource) {
        return '';
    }

    const entries = buildCounterSummaryEntries({
        ...summarySource,
        hideOwnerSystemKeys: summarySource.hideOwnerSystemKeys,
        resolveLabel: getCounterLabel
    }).filter(entry => {
        const normalizedKey = String(entry?.key ?? '').trim().toLowerCase();
        if (!normalizedKey) {
            return false;
        }
        if (!summarySource.hideOwnerSystemKeys) {
            return true;
        }
        return !PLAYER_SYSTEM_COUNTER_KEYS.has(normalizedKey);
    });
    if (!entries.length) return '';

    const lines = entries
        .map(entry => `
            <div class="counter-summary-line">
                <span class="counter-summary-label">${escapeAttributeValue(entry.label)}</span>
                <span class="counter-summary-value">${entry.value}</span>
            </div>
        `.trim())
        .join('');

    return `
        <div class="counter-summary-list">
            ${lines}
        </div>
    `;
}

export function getBuffSummaryHtml(cardInstance) {
    if (!cardInstance) return '';
    const summaries = cardInstance.buffSummaries ?? cardInstance.BuffSummaries ?? [];
    const statSummary = cardInstance.buffStatSummary ?? cardInstance.BuffStatSummary ?? null;
    const rawKeywords = cardInstance.keywords ?? cardInstance.Keywords ?? [];

    const numericGroups = summarizeAttachedNumericEffects(summaries);
    const numericFields = new Set(numericGroups.map(group => group.field));
    const segments = [];
    if (statSummary) {
        const attackDelta = statSummary.attackDelta ?? statSummary.AttackDelta ?? 0;
        const healthDelta = statSummary.healthDelta ?? statSummary.HealthDelta ?? 0;
        const costDelta = statSummary.costDelta ?? statSummary.CostDelta ?? 0;

        if (attackDelta !== 0 && !numericFields.has('Attack')) segments.push(`ATK ${attackDelta > 0 ? `+${attackDelta}` : attackDelta}`);
        if (healthDelta !== 0 && !numericFields.has('Health') && !numericFields.has('Defense')) segments.push(`HP ${healthDelta > 0 ? `+${healthDelta}` : healthDelta}`);
        if (costDelta !== 0 && !numericFields.has('Cost')) segments.push(`COST ${costDelta > 0 ? `+${costDelta}` : costDelta}`);
    }

    const summaryLine = segments.length > 0
        ? `<div class="buff-summary-total"> ${segments.join(' / ')}</div>`
        : '';

    let keywordLine = '';
    if (Array.isArray(rawKeywords) && rawKeywords.length > 0) {
        void ensureCardKeywordLabelLookup();
        const seen = new Set();
        const keywordLabels = rawKeywords
            .map(keyword => String(keyword ?? '').trim())
            .filter(Boolean)
            .filter(keyword => {
                const key = keyword.toLowerCase();
                if (seen.has(key)) return false;
                seen.add(key);
                return true;
            })
            .map(keyword => getCardKeywordLabel(keyword))
            .filter(Boolean);

        if (keywordLabels.length > 0) {
            keywordLine = `<div class="buff-summary-keywords"><span class="buff-summary-label">关键词</span><span class="buff-summary-value">${keywordLabels.join(' / ')}</span></div>`;
        }
    }

    const numericHtml = renderAttachedNumericEffects(numericGroups);
    const lines = Array.isArray(summaries)
        ? summaries
            .map(summary => {
                const sourceCardName = summary?.sourceCardName ?? summary?.SourceCardName;
                const sourceCardArtPreview = summary?.sourceCardArtPreview ?? summary?.SourceCardArtPreview;
                const sourceAbilityName = summary?.sourceAbilityName ?? summary?.SourceAbilityName;
                if (!sourceCardName) {
                    return '';
                }
                const sourceCardArtUrl = resolveArtUrl(sourceCardArtPreview);
                const granted = summary.GrantedAbilities ?? summary.grantedAbilities ?? [];
                const grantedTexts = granted.map(ability => ability.Description || ability.description || ability.Name || ability.name || '').filter(Boolean);
                const description = grantedTexts.length ? '' : summary.description ?? summary.Description ?? '';
                const details = summary.details ?? summary.Details ?? [];
                const labels = { Attack:'攻击力', Health:'生命值', Defense:'生命值', Cost:'费用', Act:'启动费用', Reanimate:'亡召上限', Permanent:'永久', TurnEnd:'回合结束', TurnStart:'回合开始', NextTurnEnd:'下次回合结束', NextTurnStart:'下次回合开始', Ward:'守护', Rush:'突进', Storm:'疾驰', Shield:'屏障', EndOfTurn:'回合结束时', StartOfTurn:'下回合开始时', OwnerTurnStart:'我方回合开始时', OwnerTurnEnd:'我方回合结束时', OpponentTurnStart:'敌方回合开始时', OpponentTurnEnd:'敌方回合结束时', ConditionalRemoval:'满足条件时移除', UntilTurnEnd:'回合结束前', UntilTurnStart:'回合开始前' };
                const localize = value => String(value).replace(/\b[A-Za-z]+\b/g, token => labels[token] || getCardKeywordLabel(token) || token);
                const numericTexts = new Set((summary.NumericEffects ?? summary.numericEffects ?? []).map(entry => entry.DisplayText ?? entry.displayText));
                const detailCounts = new Map();
                if (Array.isArray(details)) for (const detail of details) if (!numericTexts.has(detail) && !grantedTexts.includes(detail)) detailCounts.set(detail, (detailCounts.get(detail) || 0) + 1);
                if (detailCounts.size === 0 && !grantedTexts.length && (numericTexts.size || details.length)) return '';
                return `<section class="battle-attached-effect"><b class="battle-attached-title">${renderDescriptionHtml(grantedTexts.join('\n') || [...detailCounts.keys()].map(localize).join(' / ') || description || '附加效果')}</b>${effectSourceHtml({id:summary.SourceCardDefinitionId ?? summary.sourceCardDefinitionId,name:sourceCardName,abilityName:sourceAbilityName,art:sourceCardArtUrl})}</section>`;
            })
            .filter(Boolean)
            .join('')
        : '';

    if (!summaryLine && !keywordLine && !numericHtml && !lines) {
        return '';
    }

    return `<div class="buff-summary-list">${summaryLine}${keywordLine}${numericHtml}${lines}</div>`;
}

function getBattlePreviewPlaceholderHtml() {
    return '';
}

function buildBattlePreviewHtml(cardInstance) {
    const rawRarity = cardInstance.rarity ?? cardInstance.Rarity ?? 'Common';
    const rawClass = cardInstance.class ?? cardInstance.Class ?? 'Neutral';
    const rawType = cardInstance.type ?? cardInstance.Type ?? 'Spell';
    const rarityKey = normalizeEnumName(rawRarity) || 'Common';
    const classKey = normalizeEnumName(rawClass) || 'Neutral';
    const typeKey = normalizeEnumName(rawType) || 'Spell';
    ensurePreviewEnumLocalization();
    const rarityLabel = getCachedEnumLabel('CardRarity', rawRarity, rarityKey);
    const classLabel = getCachedEnumLabel('CardClass', rawClass, classKey);
    const typeLabel = getCachedEnumLabel('CardType', rawType, typeKey);
    const rarityClass = String(rarityKey).toLowerCase();
    const normalizedType = normalizeEnumName(rawType);
    const isLeader = equalsIgnoreCase(normalizedType, 'Leader');
    const isFollower = equalsIgnoreCase(normalizedType, 'Follower') || equalsIgnoreCase(normalizedType, 'Follower_Evolved');
    const cardZone = cardInstance.zone ?? cardInstance.Zone;
    const isHandZone = typeof cardZone === 'string' && cardZone.toLowerCase() === 'hand';
    const previewCostInfo = resolveCardCostDisplay(cardInstance, isHandZone ? 'hand' : 'other');
    const costValue = previewCostInfo.displayCost ?? 0;
    const attackValue = cardInstance.currentAttack ?? cardInstance.CurrentAttack ?? cardInstance.attack ?? cardInstance.Attack ?? 0;
    const healthValue = cardInstance.currentHealth ?? cardInstance.CurrentHealth ?? cardInstance.health ?? cardInstance.Health ?? 0;
    const description = cardInstance.description ?? cardInstance.Description ?? '';
    const previewPath = cardInstance.artPreview ?? cardInstance.ArtPreview ?? '';
    const previewUrl = resolveArtUrl(previewPath);
    const detailPath = cardInstance.artDetail ?? cardInstance.ArtDetail ?? '';
    const detailUrl = resolveArtUrl(detailPath);
    if (previewPath) {
        void preloadArt(previewPath, { priority: 'high' }).catch(() => {
            // Ignore preload errors in preview panel.
        });
    }
    if (detailPath && detailPath !== previewPath) {
        void preloadArt(detailPath, { priority: 'high' }).catch(() => {
            // Ignore preload errors in preview panel.
        });
    }
    const tribeRaw = cardInstance.tribe ?? cardInstance.Tribe ?? [];
    const tribeList = Array.isArray(tribeRaw) ? tribeRaw : [tribeRaw];
    const tribeText = tribeList
        .map(item => (item ?? '').toString().trim())
        .filter(Boolean)
        .join('·');
    const rawCountdown = cardInstance.countdown
        ?? cardInstance.CurrentCountdown
        ?? cardInstance.currentCountdown
        ?? cardInstance.Countdown;
    const countdownValue = rawCountdown ?? null;
    const showCountdown = !isFollower
        && countdownValue !== null
        && countdownValue !== undefined
        && Number(countdownValue) > 0;
    const keywordList = Array.isArray(cardInstance.keywords)
        ? cardInstance.keywords
        : (Array.isArray(cardInstance.Keywords) ? cardInstance.Keywords : []);
    const hasEarthsigilKeyword = keywordList.some(keyword => equalsIgnoreCase(keyword, 'EarthSigil'));
    const earthsigilValue = cardInstance.earthsigil
        ?? cardInstance.Earthsigil
        ?? cardInstance.EarthSigil
        ?? cardInstance.counters?.earthsigil
        ?? cardInstance.Counters?.Earthsigil
        ?? 0;
    const showEarthsigil = !isFollower && hasEarthsigilKeyword;
    const showEvidence = equalsIgnoreCase(cardInstance.type ?? cardInstance.Type, 'Amulet')
        && keywordList.some(keyword => equalsIgnoreCase(keyword, 'Evidence'));
    const evidenceValue = cardInstance.evidence ?? cardInstance.Evidence ?? cardInstance.counters?.evidence ?? cardInstance.Counters?.evidence ?? 0;

    // Generate Action Buttons
    const actionsHtml = getActionButtonsHtml(cardInstance);
    const buffSummaryHtml = getBuffSummaryHtml(cardInstance);
    const counterSummaryHtml = getCounterSummaryHtml(cardInstance);

    const rawMirageBuyCost = cardInstance.mirageBuyCost ?? cardInstance.MirageBuyCost ?? cardInstance.buyCost ?? cardInstance.BuyCost;
    const isMiragePreview = isMirageMode(gameState) || Boolean(cardInstance.isMirageCard ?? cardInstance.IsMirageCard);
    const showMirageTierInSlate = isMiragePreview && !isLeader && (!isHandZone || (isFollower && Number(costValue) === 0));
    const effectiveSlateCost = showMirageTierInSlate
        ? Math.max(1, Math.trunc(Number(cardInstance.cost ?? cardInstance.Cost ?? costValue) || 1))
        : costValue;

    return buildPreviewSlateHtml({
        rarityClass,
        rarityLabel,
        classLabel,
        typeLabel,
        name: cardInstance.name ?? cardInstance.Name ?? 'UNTITLED',
        costValue: effectiveSlateCost,
        enhanceActive: previewCostInfo.enhanceActive,
        modeLabel: previewCostInfo.modeVisual.label,
        alternateActive: previewCostInfo.modeVisual.alternate,
        isFollower,
        isLeader,
        isMirageCard: showMirageTierInSlate,
        mirageBuyCost: isMiragePreview && !isLeader && rawMirageBuyCost !== undefined ? rawMirageBuyCost : null,
        attackValue,
        healthValue,
        countdownValue,
        showCountdown,
        earthsigilValue,
        showEarthsigil,
        evidenceValue,
        showEvidence,
        isIntromezzoActive: cardInstance.isIntromezzoActive ?? cardInstance.IsIntromezzoActive ?? false,
        description,
        hideEmptyDescriptionPlaceholder: isLeader,
        previewUrl,
        detailUrl,
        enableDetail: true,
        tribeText,
        counterSummaryHtml,
        buffSummaryHtml,
        actionsHtml
    });
}

function renderBattleCardPreview(cardInstance) {
    if (!battleCardPreviewEl) {
        battleCardPreviewEl = document.getElementById('battle-card-preview');
    }
    if (!battleCardPreviewEl) return;

    if (!cardInstance) {
        battleCardPreviewEl.classList.add('is-empty');
        battleCardPreviewEl.innerHTML = getBattlePreviewPlaceholderHtml();
        delete battleCardPreviewEl.dataset.previewUid;
        return;
    }

    battleCardPreviewEl.classList.remove('is-empty');
    const previewUid = cardInstance.uid ?? cardInstance.InstanceId ?? '';
    if (previewUid) {
        battleCardPreviewEl.dataset.previewUid = previewUid;
    } else {
        delete battleCardPreviewEl.dataset.previewUid;
    }
    const previewCardId = cardInstance.cardId
        ?? cardInstance.CardId
        ?? cardInstance.cardDefinitionId
        ?? cardInstance.CardDefinitionId
        ?? '';
    if (previewCardId !== '') {
        battleCardPreviewEl.dataset.previewCardId = String(previewCardId);
    } else {
        delete battleCardPreviewEl.dataset.previewCardId;
    }
    const previewCardName = cardInstance.name ?? cardInstance.Name ?? '';
    if (previewCardName) {
        battleCardPreviewEl.dataset.previewCardName = String(previewCardName);
    } else {
        delete battleCardPreviewEl.dataset.previewCardName;
    }
    battleCardPreviewEl.innerHTML = buildBattlePreviewHtml(cardInstance);
    for (const button of battleCardPreviewEl.querySelectorAll('.battle-effect-source-button[data-card-id]')) {
        button.addEventListener('click', async event => {
            event.preventDefault();event.stopPropagation();
            try {
                const response=await fetch(`/api/cards/${Number(button.dataset.cardId)}`);
                if(!response.ok) throw new Error('来源卡牌详情加载失败');
                showCardDetailModal(await response.json());
            } catch(error) { showAlert(error.message); }
        });
    }
    const fallbackPreviewPath = cardInstance.artPreview
        ?? cardInstance.ArtPreview
        ?? '';
    const fallbackPreviewUrl = resolveArtUrl(fallbackPreviewPath);
    void hydrateAlternatePlayReferences(battleCardPreviewEl, previewCardId, { fallbackPreviewUrl, openCardDetail: showCardDetailModal, references: cardInstance.alternatePlayReferences ?? cardInstance.AlternatePlayReferences ?? [] });
    void hydrateDescriptionReferences(battleCardPreviewEl, {
        openCardDetail: showCardDetailModal,
        fallbackPreviewUrl,
        currentCardId: previewCardId || null,
        currentCardName: previewCardName
    });
}

export function clearSelfReturnVisuals(cardUid) {
    cancelCommittedHandPlayVisual(cardUid, { animateBack: false });
    if (String(battleCardPreviewEl?.dataset.previewUid).toLowerCase() === String(cardUid).toLowerCase()) {
        renderBattleCardPreview(null);
    }
}

export function replaceBattleCardPreviewIdentity(previousUid, cardInstance) {
    if (String(battleCardPreviewEl?.dataset.previewUid).toLowerCase() === String(previousUid).toLowerCase()) {
        renderBattleCardPreview(cardInstance);
    }
}

function refreshBattleCardPreviewIfCurrent(cardInstance) {
    if (!cardInstance) {
        return;
    }
    if (!battleCardPreviewEl) {
        battleCardPreviewEl = document.getElementById('battle-card-preview');
    }
    if (!battleCardPreviewEl) return;

    const previewUid = battleCardPreviewEl.dataset.previewUid;
    const cardUid = cardInstance.uid ?? cardInstance.InstanceId ?? '';
    if (!previewUid || !cardUid) return;

    if (previewUid === String(cardUid)) {
        renderBattleCardPreview(cardInstance);
    }
}

// --- 渲染回合信息的辅助函数 ---
function renderTurnInfo(turnNumber, currentPlayerId) {
    latestTurnNumber = turnNumber;
    latestTurnPlayerId = currentPlayerId;
    if (isTurnStartActive) {
        return;
    }
    // Update the turn information display element if it was cached successfully
    if (turnInfoEl) {
        if (gameState.gameOver) {
            const winnerId = gameState.winner;
            const localPlayerId = gameState.localPlayerId;
            let displayResult = "GAME OVER";
            let displayColor = 'var(--color-secondary)';
            let ownerState = 'over';

            if (winnerId && localPlayerId) {
                if (winnerId === localPlayerId) {
                    displayResult = "YOU WIN";
                    displayColor = 'var(--color-success)';
                    ownerState = 'win';
                } else {
                    displayResult = "YOU LOSE";
                    displayColor = 'var(--color-accent)';
                    ownerState = 'lose';
                }
            }

            turnInfoEl.textContent = displayResult;
            turnInfoEl.dataset.turnOwner = ownerState;
            turnInfoEl.style.color = displayColor;
            return;
        }
        if (isMirageMode(gameState)) {
            const roundNum = Number(gameState.mirage?.roundNumber ?? gameState.mirage?.RoundNumber ?? turnNumber ?? 1);
            const phaseLabel = gameState.phase === 'Recruit' ? 'RECRUIT' : 'COMBAT';
            turnInfoEl.textContent = `ROUND ${roundNum} · ${phaseLabel}`;
            turnInfoEl.dataset.turnOwner = 'mirage';
            turnInfoEl.style.color = '';
            return;
        }
        // Display "Starting..." or similar if turnNumber is 0 (initial hydration)
        const displayTurn = turnNumber === 0 ? "GAME START" : `TURN ${turnNumber}`;

        turnInfoEl.textContent = displayTurn;

        const isLocalPlayerTurn = (currentPlayerId === gameState.localPlayerId);
        turnInfoEl.dataset.turnOwner = isLocalPlayerTurn ? 'self' : 'opponent';
        turnInfoEl.style.color = '';
    }
}

function formatTurnCountdown(remainingMs) {
    const safeMs = Math.max(0, remainingMs);
    const seconds = safeMs / 1000;
    return `${seconds.toFixed(1)}`;
}

function updateTurnTimerDisplay(remainingMs, totalMs, isActive) {
    if (!endTurnButtonEl) return;

    const safeTotalMs = Math.max(0, totalMs);
    const safeRemainingMs = Math.max(0, remainingMs);
    const progress = safeTotalMs > 0 ? Math.min(1, safeRemainingMs / safeTotalMs) : 0;
    const warningMs = 60 * 1000;
    const criticalMs = 30 * 1000;

    if (isActive) {
        endTurnButtonEl.classList.add('turn-timer-active');
    } else {
        endTurnButtonEl.classList.remove('turn-timer-active');
    }
    endTurnButtonEl.classList.toggle('turn-timer-warning', isActive && safeRemainingMs <= warningMs);
    endTurnButtonEl.classList.toggle('turn-timer-critical', isActive && safeRemainingMs <= criticalMs);

    if (endTurnCountdownEl) {
        endTurnCountdownEl.textContent = formatTurnCountdown(safeRemainingMs);
    }

    if (endTurnIndicatorLights.length) {
        const litCount = Math.round(progress * endTurnIndicatorLights.length);
        endTurnIndicatorLights.forEach((light, index) => {
            light.classList.toggle('is-active', index < litCount);
        });
    }

}

let turnStartTimeoutId = null;
let isTurnStartActive = false;
let latestTurnNumber = 0;
let latestTurnPlayerId = null;
const TURN_START_ANIMATION_MS = 1300;

export function showTurnStartBanner(currentPlayerId) {
    if (!turnInfoEl) return;

    const isLocal = Boolean(gameState.localPlayerId) && currentPlayerId === gameState.localPlayerId;
    const sideClass = isLocal ? 'turn-start-friendly' : 'turn-start-enemy';
    if (turnInfoEl._turnStartHandler) {
        turnInfoEl.removeEventListener('animationend', turnInfoEl._turnStartHandler);
        turnInfoEl._turnStartHandler = null;
    }

    isTurnStartActive = true;
    turnInfoEl.textContent = 'TURN START';
    turnInfoEl.classList.remove('turn-start-friendly', 'turn-start-enemy', 'turn-start-active');
    turnInfoEl.classList.add(sideClass);
    turnInfoEl.style.color = '';

    // Restart animation by forcing reflow.
    void turnInfoEl.offsetWidth;
    turnInfoEl.classList.add('turn-start-active');

    if (turnStartTimeoutId) {
        clearTimeout(turnStartTimeoutId);
    }
    const onAnimationEnd = (event) => {
        if (event.animationName !== 'turn-start-wipe') return;
        turnInfoEl.removeEventListener('animationend', onAnimationEnd);
        turnInfoEl._turnStartHandler = null;
        isTurnStartActive = false;
        turnInfoEl.classList.remove('turn-start-active', 'turn-start-friendly', 'turn-start-enemy');
        renderTurnInfo(latestTurnNumber, latestTurnPlayerId);
    };
    turnInfoEl._turnStartHandler = onAnimationEnd;
    turnInfoEl.addEventListener('animationend', onAnimationEnd);
    turnStartTimeoutId = window.setTimeout(() => {
        if (!isTurnStartActive) return;
        isTurnStartActive = false;
        turnInfoEl.classList.remove('turn-start-active', 'turn-start-friendly', 'turn-start-enemy');
        renderTurnInfo(latestTurnNumber, latestTurnPlayerId);
        if (turnInfoEl._turnStartHandler) {
            turnInfoEl.removeEventListener('animationend', turnInfoEl._turnStartHandler);
            turnInfoEl._turnStartHandler = null;
        }
        turnStartTimeoutId = null;
    }, TURN_START_ANIMATION_MS + 200);
}

// --- 渲染游戏日志的辅助函数 ---
// log panel state and rendering moved to src/ui/logPanel.js

let pendingHighlightRenderRafId = null;
let pendingHighlightRenderState = null;

function renderHighlightsNow(currentGameState) {
    clearSelectionHighlights();
    syncHandUseButtons();
    evolutionDrag.refresh();

    if (isAwaitingPlayerChoice) {
        renderAbilityTargetHighlights(currentGameState);
    } else {
        renderPlayableCardHighlights(currentGameState);
        renderFieldReadinessHighlights(currentGameState);
        renderAttackHighlights(currentGameState);
    }
}

function renderHighlights(currentGameState) {
    pendingHighlightRenderState = currentGameState || gameState;

    if (pendingHighlightRenderRafId !== null) {
        return;
    }

    if (typeof window === 'undefined' || typeof window.requestAnimationFrame !== 'function') {
        const stateToRender = pendingHighlightRenderState || gameState;
        pendingHighlightRenderState = null;
        renderHighlightsNow(stateToRender);
        return;
    }

    pendingHighlightRenderRafId = window.requestAnimationFrame(() => {
        pendingHighlightRenderRafId = null;
        const stateToRender = pendingHighlightRenderState || gameState;
        pendingHighlightRenderState = null;
        renderHighlightsNow(stateToRender);
    });
}

function renderFieldReadinessHighlights(currentGameState) {
    const players = Array.isArray(currentGameState?.players) ? currentGameState.players : [];
    players.forEach(player => {
        const fieldCards = Array.isArray(player?.field) ? player.field : [];
        fieldCards.forEach(cardInstance => {
            const cardUid = getCardUid(cardInstance);
            if (!cardUid) return;
            const cardElement = document.querySelector(`.field-card[data-uid="${cardUid}"]`);
            if (!cardElement) return;
            applyAttackReadinessClasses(cardElement, cardInstance, 'field');
        });
    });
}

// --- Highlight Helper Functions ---

// Helper 1: Clear All Highlight Classes
function clearSelectionHighlights() {
    const highlightClasses = [
        'playable',
        'requires-target',
        'no-valid-targets',
        'can-play-without-target',
        'attack-ready-follower',
        'attack-ready-all',
        'act-ready',
        'selected-attacker',
        'targetable',
        'targetable-restricted',
        'targeting-active',
        'targeting-spell',
        'targetable-spell',
        'choice-selected',
        'choice-hand-hover',
        'attack-drag-hover'
    ];
    gameContainerEl?.classList.remove('targeting-active'); // Remove global targeting class
    document.querySelectorAll('.' + highlightClasses.join(', .'))
        .forEach(el => el.classList.remove(...highlightClasses));
}

function applyChoiceHandHoverEffect(validTargetIds) {
    if (!Array.isArray(validTargetIds) || validTargetIds.length === 0) {
        return;
    }

    const targetIdSet = new Set(validTargetIds.map(id => String(id)));
    const handOwnerIds = new Set();

    validTargetIds.forEach(targetUid => {
        const card = findCardInstanceByUid(targetUid);
        if (!card) return;
        if (resolveCardContext(card) !== 'hand') return;

        const ownerId = getChoiceCardOwnerId(card);
        if (ownerId) {
            handOwnerIds.add(String(ownerId));
        }
    });

    const selectableCardInstances = Array.isArray(currentChoiceRequest?.SelectableCardInstances)
        ? currentChoiceRequest.SelectableCardInstances
        : [];
    selectableCardInstances.forEach(dto => {
        const uid = dto?.InstanceId ?? dto?.instanceId;
        if (!uid || !targetIdSet.has(String(uid))) return;
        const zone = String(dto?.Zone ?? dto?.zone ?? '').toLowerCase();
        if (zone !== 'hand') return;
        const ownerId = dto?.OwnerPlayerId ?? dto?.ownerId;
        if (ownerId) {
            handOwnerIds.add(String(ownerId));
        }
    });

    if (handOwnerIds.size === 0) {
        return;
    }

    const localPlayerId = String(gameState.localPlayerId ?? '');
    const applyHoverClass = handRoot => {
        handRoot?.querySelectorAll?.('.card')?.forEach?.(cardEl => cardEl.classList.add('choice-hand-hover'));
    };

    if (!localPlayerId) {
        applyHoverClass(player1HandEl);
        applyHoverClass(player2HandEl);
        return;
    }

    if (handOwnerIds.has(localPlayerId)) {
        applyHoverClass(player1HandEl);
    }

    const hasOpponentHandTarget = Array.from(handOwnerIds).some(ownerId => ownerId !== localPlayerId);
    if (hasOpponentHandTarget) {
        applyHoverClass(player2HandEl);
    }
}

// Helper 2: Render Ability Target Highlights (Replaces renderSpellTargetHighlights)
function renderAbilityTargetHighlights(currentGameState) {
    if (!currentChoiceRequest) {
        console.warn("UI Highlights: renderAbilityTargetHighlights called but currentChoiceRequest is null.");
        return;
    }

    gameContainerEl?.classList.add('targeting-active'); // Use a generic targeting class

    const { SelectableTargetIds, SourceCardUid } = currentChoiceRequest;

    // Highlight the source card itself (whether in hand or on field)
    if (SourceCardUid) {
        const sourceCardElement = document.querySelector(`.card[data-uid="${SourceCardUid}"]`);
        if (sourceCardElement) {
            sourceCardElement.classList.add('targeting-spell'); // Reuse same class as spell source
        }
    }

    const validTargetIds = SelectableTargetIds || [];

    if (validTargetIds.length === 0) {
        console.warn(`UI Highlights: No valid targets found in currentChoiceRequest.`);
        return;
    }

    applyChoiceHandHoverEffect(validTargetIds);

    validTargetIds.forEach(targetUid => {
        const targetElement = resolveRenderableTargetElement(targetUid);

        if (targetElement) {
            if (canSelectChoiceTarget(targetUid)) {
                targetElement.classList.add('targetable-spell'); // Reuse same class as spell target
            } else {
                targetElement.classList.add('targetable-restricted');
            }
            if (currentChoiceSelectedTargetIds?.has?.(String(targetUid))) {
                targetElement.classList.add('choice-selected');
            }
        } else {
            console.warn(`UI Highlights: Could not find element for player choice target UID ${targetUid}`);
        }
    });
}

// Helper 3: Render Playable Card Highlights (in hand) using Playability Cache - ONLY for local player
function renderPlayableCardHighlights(currentGameState) {
    const localPlayerId = gameState.localPlayerId;
    const isMirage = isMirageMode(currentGameState);
    if (isMirage) {
        const localPlayer = getPlayerState(localPlayerId);
        if (!localPlayer || currentGameState.phase !== 'Recruit' || localPlayer.mirage?.isReady === true) {
            return;
        }
    } else {
        const currentPlayerId = getCurrentPlayerId();
        // Only highlight if it's the LOCAL player's turn and in Main phase
        if (currentPlayerId !== localPlayerId || currentGameState.phase !== 'Main') {
            return;
        }
    }

    const playerState = getPlayerState(localPlayerId);
    if (!playerState || !player1HandEl) {
        console.warn("UI Highlights Error: Missing local player state or hand element for highlights.");
        return;
    }


    playerState.hand.forEach(cardInstance => {
        if (!cardInstance) return;

        const handCardUid = getCardUid(cardInstance);
        if (!handCardUid) return;
        const cardElement = player1HandEl.querySelector(`.card[data-uid="${handCardUid}"]`);
        if (!cardElement) return; // Skip if element not found

        const playabilityInfo = handCardUid ? getPlayabilityForCard(handCardUid, localPlayerId) : null;

        // Remove previous highlights first
        cardElement.classList.remove('playable', 'requires-target', 'no-valid-targets', 'can-play-without-target');

        if (playabilityInfo) {
            if (playabilityInfo.isPlayable) {
                cardElement.classList.add('playable');

                if (playabilityInfo.requiresTargetSelection) {
                    cardElement.classList.add('requires-target');
                    if (!playabilityInfo.hasValidTargets) {
                        cardElement.classList.add('no-valid-targets');
                        // Optionally add class if it can be played without target anyway
                        if (playabilityInfo.canPlayWithoutTarget) {
                            cardElement.classList.add('can-play-without-target');
                        }
                    }
                }
            } else {
                // Optionally add a 'not-playable' class or title attribute with FailureReason
                cardElement.title = playabilityInfo.failureReason || "不可用"; // Show reason on hover
            }
        } else {
            // No highlight if not checked or cache cleared
            cardElement.title = ''; // Clear any previous failure reason tooltip
        }
    });
}
// Helper 4: Update Highlight for a Single Card based on Cache Result
// Exported function to be called by gameManager when a result arrives.
export function updateSingleCardHighlight(cardUid, playabilityInfo) {
    if (!cardUid || !playabilityInfo) {
        console.warn("UI Update Error: updateSingleCardHighlight called with invalid arguments.", cardUid, playabilityInfo);
        return;
    }

    // Find the card element in the player's hand (assuming player 1 is local)
    const cardElement = player1HandEl?.querySelector(`.card[data-uid="${cardUid}"]`);
    if (!cardElement) {
        return; // Card might not be in hand anymore, or element not rendered yet
    }


    // Remove previous highlights first
    cardElement.classList.remove('playable', 'requires-target', 'no-valid-targets', 'can-play-without-target');
    cardElement.title = ''; // Clear tooltip

    // Apply new highlights based on the provided info
    if (playabilityInfo.isPlayable) {
        cardElement.classList.add('playable');
        if (playabilityInfo.requiresTargetSelection) {
            cardElement.classList.add('requires-target');
            if (!playabilityInfo.hasValidTargets) {
                cardElement.classList.add('no-valid-targets');
                if (playabilityInfo.canPlayWithoutTarget) {
                    cardElement.classList.add('can-play-without-target');
                }
            }
        }
    } else {
        // Not playable, set tooltip
        cardElement.title = playabilityInfo.failureReason || "不可用";
    }
}

function updateFieldDisplay(playerId, fieldCards = []) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const fieldElement = isLocalPlayer ? player1FieldEl : player2FieldEl;
    if (!fieldElement) {
        return;
    }
    renderField(fieldCards, fieldElement, !isLocalPlayer);
}

export function updateCardActionButtons(cardInstance) {
    if (!cardInstance) return;
    evolutionDrag.refresh();
    const cardUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
    if (!cardUid) return;

    const cardElement = document.querySelector(`.field-card[data-uid="${cardUid}"]`);
    if (!cardElement) return;

    const actionState = getCardActionState(cardInstance, 'field');
    const existingButtons = cardElement.querySelectorAll(CARD_ACTION_BUTTON_SELECTOR);
    existingButtons.forEach(button => button.remove());

    if (actionState.showSuperEvolve) {
        const superEvolveBtn = document.createElement('button');
        superEvolveBtn.className = 'super-evolve-button';
        superEvolveBtn.textContent = getBattleActionLabel('superEvolve');
        superEvolveBtn.dataset.superEvolveUid = cardUid;
        cardElement.appendChild(superEvolveBtn);
    }

    if (actionState.showEvolve) {
        const evolveBtn = document.createElement('button');
        evolveBtn.className = 'evolve-button';
        evolveBtn.textContent = getBattleActionLabel('evolve');
        evolveBtn.dataset.evolveUid = cardUid;
        cardElement.appendChild(evolveBtn);
    }

    if (actionState.showFusion) {
        const fusionAbilityEntries = Array.isArray(actionState.fusionAbilities) && actionState.fusionAbilities.length > 0
            ? actionState.fusionAbilities
            : [null];
        fusionAbilityEntries.forEach((fusionAbility, index) => {
            const fusionBtn = document.createElement('button');
            fusionBtn.className = 'fusion-button';
            const fusionLabel = getBattleActionLabel('fusion', index, fusionAbilityEntries.length);
            fusionBtn.textContent = fusionLabel;
            fusionBtn.dataset.fusionUid = cardUid;
            const abilityRuntimeId = fusionAbility?.runtimeId ?? fusionAbility?.RuntimeId ?? '';
            if (abilityRuntimeId) {
                fusionBtn.dataset.fusionAbilityId = abilityRuntimeId;
            }
            cardElement.appendChild(fusionBtn);
        });
    }

    if (actionState.showAct) {
        const actAbilityEntries = getActAbilityEntries(actionState);
        actAbilityEntries.forEach((actAbility, index) => {
            const actBtn = document.createElement('button');
            actBtn.className = 'act-button';
            const actButtonState = resolveActButtonState(actionState, actAbility);
            const actLabel = getBattleActionLabel('act', index, actAbilityEntries.length);
            actBtn.textContent = actLabel;
            actBtn.disabled = actButtonState.disabled;
            if (actButtonState.title) {
                actBtn.title = actButtonState.title;
            } else {
                actBtn.removeAttribute('title');
            }
            actBtn.dataset.actUid = cardUid;
            const abilityRuntimeId = actAbility?.abilityRuntimeId ?? actAbility?.AbilityRuntimeId ?? '';
            if (abilityRuntimeId) {
                actBtn.dataset.actAbilityId = abilityRuntimeId;
            }
            cardElement.appendChild(actBtn);
        });
    }
    applyCardActionButtonStack(cardElement);
}

export function updateActButtonState(cardInstance) {
    const zone = String(cardInstance?.zone ?? cardInstance?.Zone ?? '').trim().toLowerCase();
    const type = String(cardInstance?.type ?? cardInstance?.Type ?? '').trim().toLowerCase();
    if (zone === 'leader' || type === 'leader') {
        const ownerId = cardInstance?.ownerId ?? cardInstance?.OwnerPlayerId;
        const owner = ownerId ? getPlayerState(ownerId) : null;
        if (owner) {
            const prefix = owner.id === gameState.localPlayerId ? 'player1' : 'player2';
            renderPlayerStats(owner, prefix);
        }
        return;
    }
    updateCardActionButtons(cardInstance);
    const cardUid = cardInstance?.uid ?? cardInstance?.InstanceId ?? cardInstance?.instanceId;
    if (!cardUid) return;
    const cardElement = document.querySelector(`.field-card[data-uid="${cardUid}"]`);
    if (cardElement) {
        applyAttackReadinessClasses(cardElement, cardInstance, 'field');
    }
}

function refreshCardActionControls(cardInstance) {
    if (!cardInstance) return;

    const zone = String(cardInstance.zone ?? cardInstance.Zone ?? '').trim().toLowerCase();
    const type = String(cardInstance.type ?? cardInstance.Type ?? '').trim().toLowerCase();
    if (zone === 'hand') {
        const ownerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId;
        if (!ownerId) return;

        const player = getPlayerState(ownerId);
        if (!player || !Array.isArray(player.hand)) return;

        updateHandDisplay(player.id, player.hand);
        return;
    }

    if (zone === 'field' || zone === 'leader' || type === 'leader') {
        updateActButtonState(cardInstance);
    }
}

// Helper 4: Render Attack Selection & Target Highlights
function renderAttackHighlights(currentGameState) {
    // Highlight Selected Attacker
    if (currentGameState.selectedAttackerUid) {
        const attackerElement = gameContainerEl?.querySelector(`.field-card[data-uid="${currentGameState.selectedAttackerUid}"]`);
        if (attackerElement) {
            attackerElement.classList.add('selected-attacker');
        }

        // 【BUG修复】重构高亮逻辑，确保调用唯一且正确的`highlightTargets`函数
        // 1. 从gameState中获取最新的攻击者实例
        const attackerInstance = findCardInstanceByUid(currentGameState.selectedAttackerUid);

        // 2. 如果找到了实例，则调用标准的目标高亮函数
        if (attackerInstance) {
            highlightTargets(attackerInstance);
        } else {
            console.warn(`UI Highlights: Could not find attacker instance for UID ${currentGameState.selectedAttackerUid} in renderAttackHighlights.`);
        }
    }
}

// --- 渲染行动按钮（结束回合/确认调度）的辅助函数 ---
function renderActionButtons(currentGameState) {
    if (!endTurnButtonEl || !confirmMulliganButtonEl) {
        // console.warn("UI: Action buttons not cached, cannot render visibility.");
        return;
    }

    if (currentGameState.isSpectator === true) {
        confirmMulliganButtonEl.style.display = 'none';
        confirmMulliganButtonEl.disabled = true;
        endTurnButtonEl.style.display = 'none';
        endTurnButtonEl.disabled = true;
        document.getElementById('battle-right-dock')?.classList.add('is-hidden-hub');
        return;
    }

    // --- REVISED BUTTON LOGIC: Base visibility/enabled state on localPlayerId vs currentPlayerId ---
    const isLocalPlayerTurn = currentGameState.localPlayerId === currentGameState.currentPlayerId;
    const endTurnLabelEl = endTurnButtonEl.querySelector('.end-turn-label');
    const rightDockEl = document.getElementById('battle-right-dock');
    const phaseTagEl = document.getElementById('turn-hub-phase-tag');

    endTurnButtonEl.classList.remove('is-self-turn', 'is-enemy-turn', 'is-mirage-recruit', 'is-mirage-locked');
    rightDockEl?.classList.remove('is-hidden-hub', 'is-self-turn', 'is-enemy-turn', 'is-mirage-recruit', 'is-mirage-locked');

    if (isMirageMode(currentGameState)) {
        confirmMulliganButtonEl.style.display = 'none';
        confirmMulliganButtonEl.disabled = true;
        if (currentGameState.phase === 'Recruit') {
            const localPlayer = getPlayerState(currentGameState.localPlayerId);
            const isReady = Boolean(localPlayer?.mirage?.isReady ?? localPlayer?.mirage?.IsReady);
            const gateClosed = Boolean(currentGameState.mirage?.isNextRoundGateClosed ?? currentGameState.mirage?.IsNextRoundGateClosed);
            const standings = currentGameState.mirageStandings;
            const aliveCnt = Number(standings?.aliveCount ?? standings?.AliveCount ?? 0);
            const readyCnt = Number(standings?.readyCount ?? standings?.ReadyCount ?? 0);
            const countSuffix = aliveCnt > 0 ? ` (${readyCnt}/${aliveCnt})` : '';
            endTurnButtonEl.style.display = 'inline-block';
            endTurnButtonEl.disabled = isConnectionLocked || isReady || gateClosed;
            const mirageStateCls = isReady ? 'is-mirage-locked' : 'is-mirage-recruit';
            endTurnButtonEl.classList.add(mirageStateCls);
            rightDockEl?.classList.add(mirageStateCls);
            if (phaseTagEl) {
                phaseTagEl.textContent = isReady ? 'LINEUP LOCKED' : 'RECRUIT PHASE';
            }
            if (endTurnLabelEl) {
                endTurnLabelEl.textContent = isReady ? `LOCKED${countSuffix}` : `LOCK LINEUP${countSuffix}`;
            }
            endTurnButtonEl.title = isReady ? `已锁定阵容${countSuffix}，等待全房就绪或招募结束` : '锁定本轮幻境阵容';
        } else {
            endTurnButtonEl.style.display = 'none';
            endTurnButtonEl.disabled = true;
            rightDockEl?.classList.add('is-hidden-hub');
        }
        return;
    }

    if (currentGameState.phase === 'Mulligan') {
        const localPlayer = getPlayerState(currentGameState.localPlayerId);
        const localCompleted = localPlayer?.hasCompletedMulligan === true;

        // Mulligan Phase:
        // - If local has not confirmed: show enabled confirm button
        // - If local has confirmed: keep showing as disabled "waiting" (avoids stale UI across matches)
        confirmMulliganButtonEl.style.display = 'inline-block';
        confirmMulliganButtonEl.disabled = isConnectionLocked || localCompleted;
        confirmMulliganButtonEl.textContent = localCompleted ? '等待对方...' : '确认调度';

        endTurnButtonEl.style.display = 'none';
        endTurnButtonEl.disabled = true; // Also disable
        rightDockEl?.classList.add('is-hidden-hub');
    } else if (currentGameState.phase === 'TurnStart' || currentGameState.phase === 'Main') {
        // TurnStart or Main Phase: Show end turn, hide confirm. Enable/disable based on whose turn it is.
        confirmMulliganButtonEl.style.display = 'none';
        confirmMulliganButtonEl.disabled = isConnectionLocked;
        confirmMulliganButtonEl.textContent = '确认调度';
        endTurnButtonEl.style.display = 'inline-block';
        const turnStateCls = isLocalPlayerTurn ? 'is-self-turn' : 'is-enemy-turn';
        endTurnButtonEl.classList.add(turnStateCls);
        rightDockEl?.classList.add(turnStateCls);
        if (phaseTagEl) {
            phaseTagEl.textContent = isLocalPlayerTurn ? 'YOUR TURN' : 'OPPONENT';
        }
        if (endTurnLabelEl) {
            endTurnLabelEl.textContent = isLocalPlayerTurn ? 'END TURN' : 'ENEMY TURN';
        }
        endTurnButtonEl.disabled = isConnectionLocked || !isLocalPlayerTurn; // Disable if not local player's turn
    } else {
        // Other phases or opponent's turn: Hide both (or just hide confirm, end turn might be handled by turn manager logic implicitly)
        confirmMulliganButtonEl.style.display = 'none';
        confirmMulliganButtonEl.disabled = isConnectionLocked;
        confirmMulliganButtonEl.textContent = '确认调度';
        endTurnButtonEl.style.display = 'none'; // Hide end turn if not P1's main phase
        endTurnButtonEl.disabled = true; // Also disable when hidden
        rightDockEl?.classList.add('is-hidden-hub');
    }
}

function updateReconnectButton() {
    if (!reconnectButtonEl) return;

    if (isConnectionLocked) {
        reconnectButtonEl.style.display = 'inline-block';
        if (connectionStatus === 'reconnecting') {
            reconnectButtonEl.textContent = connectionStatusMessage || '正在重连...';
            reconnectButtonEl.disabled = true;
        } else {
            const baseText = connectionStatusMessage || '连接已断开';
            reconnectButtonEl.textContent = `${baseText}`;
            reconnectButtonEl.disabled = false;
        }
        confirmMulliganButtonEl && (confirmMulliganButtonEl.style.display = 'none');
        endTurnButtonEl && (endTurnButtonEl.style.display = 'none');
        return;
    }

    if (opponentDisconnected) {
        reconnectButtonEl.style.display = 'inline-block';
        reconnectButtonEl.textContent = '等待对方重连...';
        reconnectButtonEl.disabled = true;
        renderActionButtons(gameState);
        return;
    }

    reconnectButtonEl.style.display = 'none';
    renderActionButtons(gameState);
}

function setConnectionStatus(status, message) {
    const normalized = status || 'connected';
    const isConnected = normalized === 'connected';
    connectionStatus = normalized;
    connectionStatusMessage = message || '';
    isConnectionLocked = !isConnected;

    if (!isConnected) {
        evolutionDrag.cancel();
        updateTurnTimerDisplay(0, 0, false);
    }
    evolutionDrag.refresh();

    updateReconnectButton();

    if (!isConnected) {
        endTurnButtonEl?.setAttribute('disabled', 'true');
        confirmMulliganButtonEl?.setAttribute('disabled', 'true');
        document.querySelectorAll('.pp-boost-button').forEach(button => {
            button.disabled = true;
        });
    }
}

function setOpponentConnectionStatus(isConnected) {
    opponentDisconnected = !isConnected;
    updateReconnectButton();
}
// --- 显示伤害数字的函数 ---
function showDamageNumber(targetUid, damageAmount) {
    if (!targetUid || damageAmount === undefined || damageAmount === null || damageAmount < 0 || !isGamePageActive()) {
        return;
    }

    const targetElement = resolveRenderableTargetElement(targetUid);
    if (!targetElement) {
        return;
    }

    const safeDamage = Math.max(0, Math.trunc(Number(damageAmount) || 0));
    const targetInstance = findCardInstanceByUid(targetUid);
    const targetHasShieldNow = Array.isArray(targetInstance?.keywords)
        && targetInstance.keywords.some(keyword => equalsIgnoreCase(keyword, 'Shield'));
    const shieldBroken = safeDamage === 0 && (
        targetHasShieldNow
        || targetElement.classList?.contains('has-shield') === true
        || hasActiveTimedUidFlag(recentShieldBrokenUntil, targetUid)
    );

    getBattleMotionController().triggerWaveHits([
        {
            targetUid: String(targetUid),
            damage: safeDamage,
            shieldBroken
        }
    ]);
}

function showHealNumber(targetUid, healAmount) {
    if (!targetUid || healAmount === undefined || healAmount === null || healAmount <= 0 || !isGamePageActive()) {
        return;
    }

    const targetElement = resolveRenderableTargetElement(targetUid);
    if (!targetElement) {
        return;
    }

    const safeHeal = Math.max(0, Math.trunc(Number(healAmount) || 0));
    if (safeHeal <= 0) return;

    getBattleMotionController().triggerWaveHits([
        {
            targetUid: String(targetUid),
            heal: safeHeal
        }
    ]);
}
// --- 浜嬩欢澶勭悊 ---

// 更新 initializeUI 以确保调用事件设置（确保 DOM 加载后执行）
function initializeUI() {
    cacheDOMElements();
    removeTransientVisualArtifacts();
    removeStrayBattleCardArtifacts();
    hidePreviewDescriptionTooltip();
    removeChoiceOverlayUI();
    renderBattleCardPreview(null);
    setupGlobalEventListeners(); // 确保调用
}
// (确保 initializeUI 在 DOMContentLoaded 后调用)

function handleHandDragPointerDown(event) {
    if (!gameContainerEl) return;
    if (evolutionDrag.active) return;
    if (isConnectionLocked || isAwaitingPlayerChoice) return;
    if ((event.button ?? 0) !== 0) return;
    if (event.target?.closest?.('button, .preview-actions, #choice-overlay-root, #card-detail-modal, #card-detail-image-modal')) return;

    const sourceCardElement = event.target?.closest?.('#player1-hand .hand-card[data-uid]');
    const cardUid = sourceCardElement?.dataset?.uid;
    if (!cardUid) return;

    const cardInstance = findCardInstanceByUid(cardUid);
    if (!isHandDragStartAllowed(cardInstance)) return;

    handDragState.pointerId = event.pointerId;
    handDragState.isPending = true;
    handDragState.isActive = false;
    handDragState.cardUid = String(cardUid);
    handDragState.sourceElement = sourceCardElement;
    handDragState.startX = event.clientX;
    handDragState.startY = event.clientY;
    handDragState.currentX = event.clientX;
    handDragState.currentY = event.clientY;

    const perfDebug = getUiPerfDebugState();
    if (perfDebug.handDrag) {
        perfDebug.handDrag.sessionId += 1;
        perfDebug.handDrag.sampleCount = 0;
        perfDebug.handDrag.pendingRafAt = 0;
        perfDebug.handDrag.lastPointerMoveAt = getUiNowMs();
        perfDebug.handDrag.pointerMoveCount = 0;
        perfDebug.handDrag.metrics = createHandDragPerfSession(
            perfDebug.handDrag.sessionId,
            getUiNowMs()
        );
    }
    logUiPerfDebug('hand-drag', 'pointer down', {
        sessionId: perfDebug.handDrag?.sessionId ?? 0,
        cardUid: String(cardUid),
        routeHash: typeof window !== 'undefined' ? window.location.hash : '',
        menuStarrySkyAnimating: perfDebug.menuStarrySky?.isAnimating === true,
        menuRoute: perfDebug.menuStarrySky?.route || ''
    });
}

function handleHandDragPointerMove(event) {
    if ((!handDragState.isPending && !handDragState.isActive) || handDragState.pointerId !== event.pointerId) {
        return;
    }

    handDragState.currentX = event.clientX;
    handDragState.currentY = event.clientY;
    const perfDebug = getUiPerfDebugState();
    if (perfDebug.handDrag) {
        perfDebug.handDrag.lastPointerMoveAt = getUiNowMs();
        perfDebug.handDrag.pointerMoveCount = Number(perfDebug.handDrag.pointerMoveCount || 0) + 1;
    }

    if (handDragState.isPending) {
        const dx = handDragState.currentX - handDragState.startX;
        const dy = handDragState.currentY - handDragState.startY;
        if (Math.hypot(dx, dy) >= HAND_DRAG_MIN_DISTANCE_PX) {
            activateHandDragIfNeeded();
        }
    }

    if (!handDragState.isActive) {
        return;
    }

    const dragTarget = resolveHandDragDropTarget(handDragState.currentX, handDragState.currentY);
    updateHandDragHover(dragTarget);
    deferCardArtQueue();
    scheduleHandDragGhostUpdate();
    event.preventDefault();
}

function finalizeHandDrag(event, { wasCancelled = false } = {}) {
    if ((!handDragState.isPending && !handDragState.isActive) || handDragState.pointerId !== event.pointerId) {
        return;
    }

    const wasActiveDrag = handDragState.isActive;
    const playedCardUid = handDragState.cardUid;
    const sourceElement = handDragState.sourceElement;
    const ghostElement = handDragState.ghostElement;
    const perfDebug = getUiPerfDebugState();
    const dragMetrics = perfDebug.handDrag?.metrics || null;
    const pointerMoveCount = Number(perfDebug.handDrag?.pointerMoveCount || 0);
    let dropTarget = { kind: 'invalid', uid: null, isValid: false };

    if (handDragState.isActive && !wasCancelled) {
        const clientX = Number.isFinite(event.clientX) ? event.clientX : handDragState.currentX;
        const clientY = Number.isFinite(event.clientY) ? event.clientY : handDragState.currentY;
        dropTarget = resolveHandDragDropTarget(clientX, clientY);
    }

    resetHandDragState({
        keepGhost: true,
        preserveSourceClass: dropTarget.isValid && Boolean(playedCardUid)
    });

    if (!ghostElement) {
        return;
    }

    if (!wasActiveDrag) {
        ghostElement.remove();
        return;
    }

    if (dragMetrics) {
        dragMetrics.pointerMoveCount = pointerMoveCount;
        dragMetrics.lastPointerMoveAt = Number(perfDebug.handDrag?.lastPointerMoveAt || 0);
        logUiPerfDebug('hand-drag', 'session summary', {
            ...summarizeHandDragPerfSession(dragMetrics, getUiNowMs()),
            dropKind: dropTarget.kind,
            dropValid: dropTarget.isValid === true,
            wasCancelled
        });
    }

    suppressGameClickUntil = Date.now() + HAND_DRAG_CLICK_SUPPRESS_MS;
    event.preventDefault();

    if (dropTarget.isValid && playedCardUid) {
        sourceElement?.classList.remove('hand-drag-origin');
        sourceElement?.classList.add('hand-play-committed-source');
        startCommittedHandPlayVisual(playedCardUid, ghostElement);
        if (dropTarget.kind === 'target' && dropTarget.uid) {
            void playCard(playedCardUid, dropTarget.uid);
            return;
        }
        void playCard(playedCardUid);
        return;
    }

    animateHandDragGhostToSource(ghostElement, sourceElement);
}

function handleHandDragPointerUp(event) {
    finalizeHandDrag(event, { wasCancelled: false });
}

function handleHandDragPointerCancel(event) {
    finalizeHandDrag(event, { wasCancelled: true });
}

function handleAttackDragPointerDown(event) {
    if (!gameContainerEl) return;
    if (evolutionDrag.active) return;
    if (isConnectionLocked || isAwaitingPlayerChoice) return;
    if ((event.button ?? 0) !== 0) return;
    if (event.target?.closest?.('button, .preview-actions, #choice-overlay-root, #card-detail-modal, #card-detail-image-modal')) return;

    const sourceCardElement = event.target?.closest?.('.field-card[data-uid]');
    const attackerUid = sourceCardElement?.dataset?.uid;
    if (!attackerUid) return;

    const attackerCard = findCardInstanceByUid(attackerUid);
    if (!isAttackDragStartAllowed(attackerCard)) return;

    attackDragState.pointerId = event.pointerId;
    attackDragState.isPending = true;
    attackDragState.isActive = false;
    attackDragState.attackerUid = String(attackerUid);
    attackDragState.sourceElement = sourceCardElement;
    attackDragState.startX = event.clientX;
    attackDragState.startY = event.clientY;
    attackDragState.currentX = event.clientX;
    attackDragState.currentY = event.clientY;
}

function handleAttackDragPointerMove(event) {
    if ((!attackDragState.isPending && !attackDragState.isActive) || attackDragState.pointerId !== event.pointerId) {
        return;
    }

    attackDragState.currentX = event.clientX;
    attackDragState.currentY = event.clientY;

    if (attackDragState.isPending) {
        const dx = attackDragState.currentX - attackDragState.startX;
        const dy = attackDragState.currentY - attackDragState.startY;
        const movedDistance = Math.hypot(dx, dy);
        if (movedDistance >= ATTACK_DRAG_MIN_DISTANCE_PX) {
            activateAttackDragIfNeeded();
        }
    }

    if (!attackDragState.isActive) {
        return;
    }

    const targetInfo = resolveAttackDragDropTarget(attackDragState.currentX, attackDragState.currentY);
    updateAttackDragHover(targetInfo);
    updateAttackDragLine();
    event.preventDefault();
}

function triggerOpponentHudAttackConfirmPulse(hudEl) {
    if (!hudEl) return;
    hudEl.classList.remove('is-attack-confirmed');
    void hudEl.offsetWidth;
    hudEl.classList.add('is-attack-confirmed');
    runTrackedVisualTimeout(() => {
        hudEl.classList.remove('is-attack-confirmed');
    }, ATTACK_DRAG_LEADER_CONFIRM_MS);
}

function finalizeAttackDrag(event, { wasCancelled = false } = {}) {
    if ((!attackDragState.isPending && !attackDragState.isActive) || attackDragState.pointerId !== event.pointerId) {
        return;
    }

    const wasActiveDrag = attackDragState.isActive;
    let dropTargetInfo = { kind: 'none', uid: null, element: null, isValid: false };
    let dropTargetUid = null;

    if (attackDragState.isActive && !wasCancelled) {
        const clientX = Number.isFinite(event.clientX) ? event.clientX : attackDragState.currentX;
        const clientY = Number.isFinite(event.clientY) ? event.clientY : attackDragState.currentY;
        dropTargetInfo = resolveAttackDragDropTarget(clientX, clientY);
        if (dropTargetInfo.isValid && dropTargetInfo.uid) {
            dropTargetUid = String(dropTargetInfo.uid);
        }
    }

    const committedToLeaderHud = (dropTargetInfo.kind === 'leader-hud' || dropTargetInfo.kind === 'avatar') && Boolean(dropTargetUid);
    const leaderHudEl = dropTargetInfo.kind === 'leader-hud'
        ? dropTargetInfo.element
        : document.getElementById('player2-info');

    resetAttackDragState();

    if (committedToLeaderHud && leaderHudEl) {
        triggerOpponentHudAttackConfirmPulse(leaderHudEl);
    }

    if (wasActiveDrag) {
        suppressGameClickUntil = Date.now() + ATTACK_DRAG_CLICK_SUPPRESS_MS;
        event.preventDefault();
        if (dropTargetUid && gameState.selectedAttackerUid) {
            void selectAttackTarget(dropTargetUid);
        }
    }
}

function handleAttackDragPointerUp(event) {
    finalizeAttackDrag(event, { wasCancelled: false });
}

function handleAttackDragPointerCancel(event) {
    finalizeAttackDrag(event, { wasCancelled: true });
}

function handleHandPreviewPointerOver(event) {
    const cardEl = event.target?.closest?.('#player1-hand .hand-card[data-uid]');
    const nextUid = cardEl?.dataset?.uid ? String(cardEl.dataset.uid) : null;
    if (nextUid === hoveredHandPreviewUid) return;
    hoveredHandPreviewUid = nextUid;
    refreshSelfPpPreview();
}

function handleHandPreviewPointerOut(event) {
    if (!hoveredHandPreviewUid) return;
    const fromCard = event.target?.closest?.('#player1-hand .hand-card[data-uid]');
    if (!fromCard) return;
    const toCard = event.relatedTarget?.closest?.('#player1-hand .hand-card[data-uid]');
    if (toCard === fromCard) return;
    const nextUid = toCard?.dataset?.uid ? String(toCard.dataset.uid) : null;
    if (nextUid === hoveredHandPreviewUid) return;
    hoveredHandPreviewUid = nextUid;
    refreshSelfPpPreview();
}

// 设置全局事件监听器 (事件委托模式)
function setupGlobalEventListeners() {
    if (!gameContainerEl) {
        // console.error("UI Error: Cannot setup global listeners - gameContainerEl not found.");
        return;
    }
    evolutionDrag.attach(gameContainerEl);
    evolutionDrag.refresh();
    // 先移除旧监听器，防止重复添加 (如果 initializeUI 可能被多次调用)
    gameContainerEl.removeEventListener('click', handleGameClick);
    gameContainerEl.removeEventListener('dragstart', preventNativeGameDrag, true);
    gameContainerEl.removeEventListener('pointerdown', handleHandDragPointerDown);
    gameContainerEl.removeEventListener('pointerdown', handleAttackDragPointerDown);
    gameContainerEl.removeEventListener('pointerover', handleHandPreviewPointerOver);
    gameContainerEl.removeEventListener('pointerout', handleHandPreviewPointerOut);
    window.removeEventListener('pointermove', handleHandDragPointerMove);
    window.removeEventListener('pointerup', handleHandDragPointerUp);
    window.removeEventListener('pointercancel', handleHandDragPointerCancel);
    window.removeEventListener('pointermove', handleAttackDragPointerMove);
    window.removeEventListener('pointerup', handleAttackDragPointerUp);
    window.removeEventListener('pointercancel', handleAttackDragPointerCancel);
    gameContainerEl.addEventListener('click', handleGameClick);
    gameContainerEl.addEventListener('dragstart', preventNativeGameDrag, true);
    gameContainerEl.addEventListener('pointerdown', handleHandDragPointerDown);
    gameContainerEl.addEventListener('pointerdown', handleAttackDragPointerDown);
    gameContainerEl.addEventListener('pointerover', handleHandPreviewPointerOver);
    gameContainerEl.addEventListener('pointerout', handleHandPreviewPointerOut);
    window.addEventListener('pointermove', handleHandDragPointerMove);
    window.addEventListener('pointerup', handleHandDragPointerUp);
    window.addEventListener('pointercancel', handleHandDragPointerCancel);
    window.addEventListener('pointermove', handleAttackDragPointerMove);
    window.addEventListener('pointerup', handleAttackDragPointerUp);
    window.addEventListener('pointercancel', handleAttackDragPointerCancel);
}

function togglePlayerInfoCollapse(targetId) {
    const panel = document.getElementById(String(targetId || '').trim());
    if (!panel) return;

    const collapsed = panel.classList.toggle('is-collapsed');
    const button = panel.querySelector('.player-info-collapse-btn');
    if (!button) return;

    button.setAttribute('aria-pressed', collapsed ? 'true' : 'false');
    button.setAttribute('title', collapsed ? '展开信息栏' : '收起信息栏');
    const icon = button.querySelector('.player-info-collapse-icon');
    if (icon) {
        icon.textContent = collapsed ? '+' : '-';
    }
}

/**
 * Updates the PP+1 availability display for a specific player.
 * @param {string} playerId - The GUID of the player.
 * @param {number} usesAvailable - Remaining PP+1 uses.
 */
function updatePpBoostDisplay(playerId, usesAvailable, isArmed = false) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const playerState = getPlayerState(playerId);
    const rawIsFirst = playerState?.isFirst ?? playerState?.IsFirst;
    const isSecondPlayer = rawIsFirst === false;
    const safeLeft = Math.max(0, Number(usesAvailable) || 0);
    const isMirage = isMirageMode(gameState);
    const shouldShowSecondSlot = !isMirage && (isSecondPlayer || safeLeft > 0 || isArmed);

    const countEl = document.getElementById(`${prefix}-pp-boost-count`);
    if (countEl) {
        countEl.textContent = `×${safeLeft}`;
        countEl.style.display = isArmed ? 'none' : 'inline';
    }

    if (isLocalPlayer) {
        const buttonEl = document.getElementById('player1-pp-boost-button');
        if (!buttonEl) return;
        buttonEl.hidden = !shouldShowSecondSlot;
        if (!shouldShowSecondSlot) return;

        const isLocalTurn = gameState.currentPlayerId === gameState.localPlayerId;
        const isTurnPhase = gameState.phase === 'Main' || gameState.phase === 'TurnStart';
        const canUse = !isConnectionLocked
            && isLocalTurn
            && isTurnPhase
            && (safeLeft > 0 || isArmed)
            && !gameState.isSpectator;

        buttonEl.disabled = !canUse;
        buttonEl.classList.toggle('available', canUse);
        buttonEl.classList.toggle('armed', isArmed);
        buttonEl.classList.toggle('is-spent', !isArmed && safeLeft <= 0);

        const textEl = buttonEl.querySelector('.pp-boost-action-text');
        if (textEl) {
            textEl.textContent = isArmed ? '+1PP ON' : '+1PP';
        }
        buttonEl.title = isArmed
            ? '点击取消本回合额外能量点'
            : (safeLeft > 0 ? `后手额外能量点（剩余 ${safeLeft} 次）` : '后手额外能量点已耗尽');
    } else {
        const oppBadgeEl = document.getElementById('player2-pp-boost-badge');
        if (!oppBadgeEl) return;
        const showOpp = shouldShowSecondSlot && (safeLeft > 0 || isArmed);
        oppBadgeEl.hidden = !showOpp;
        oppBadgeEl.classList.toggle('armed', isArmed);
        const textEl = oppBadgeEl.querySelector('.pp-boost-action-text');
        if (textEl) {
            textEl.textContent = isArmed ? '+1PP ON' : '+1PP';
        }
    }
}

// 全局点击事件处理器 (事件委托)
async function showMatchHistoryCardPreview(historyRow) {
    if (!historyRow?.dataset) return false;

    const previewCard = await buildMatchHistoryPreviewCard({
        cardId: historyRow.dataset.historyCardId,
        cardName: historyRow.dataset.historyCardName,
        cost: historyRow.dataset.historyCardCost,
        artPreview: historyRow.dataset.historyCardArtPreview,
        ownerId: historyRow.dataset.historyOwnerId
    });
    if (!previewCard) {
        return false;
    }

    renderBattleCardPreview(previewCard);
    setFloatingHandCard(null);
    return true;
}

async function handleGameClick(event) {
    if (Date.now() < suppressGameClickUntil) {
        event.preventDefault();
        event.stopPropagation();
        return;
    }

    const clickedReconnectButton = event.target.closest('#reconnect-button');
    if (clickedReconnectButton) {
        if (reconnectButtonEl?.disabled) {
            return;
        }
        document.dispatchEvent(new Event('requestReconnect'));
        return;
    }

    const clickedInfoCollapseButton = event.target.closest('.player-info-collapse-btn');
    if (clickedInfoCollapseButton) {
        event.preventDefault();
        event.stopPropagation();
        togglePlayerInfoCollapse(clickedInfoCollapseButton.dataset.target);
        return;
    }

    const clickedPortraitDismiss = event.target.closest('#battle-portrait-dismiss-btn');
    if (clickedPortraitDismiss) {
        event.preventDefault();
        event.stopPropagation();
        document.getElementById('game-page')?.classList.add('portrait-guard-dismissed');
        return;
    }

    if (isConnectionLocked) {
        return;
    }

    const clickedPreviewRoot = event.target.closest('#battle-card-preview');
    const clickedPreviewTooltip = event.target.closest('.preview-description-tooltip');
    const clickedDetailModal = event.target.closest('#card-detail-modal, #card-detail-image-modal');
    const clickedMatchHistoryRow = event.target.closest('.match-history-card-row');
    const clickedCardOrAvatar = event.target.closest('.card, #player1-info, #player2-info, .emblem-slot[data-uid], .mirage-offer-card[data-offer-id]');
    if (!clickedPreviewRoot && !clickedPreviewTooltip && !clickedDetailModal && !clickedMatchHistoryRow && !clickedCardOrAvatar) {
        renderBattleCardPreview(null);
        setFloatingHandCard(null);
    }

    const clickedSpectatorToggle = event.target.closest('#spectator-toggle-view');
    if (clickedSpectatorToggle) {
        switchSpectatorPerspective();
        return;
    }

    const clickedPreviewToken = event.target.closest('[data-preview-token]');
    if (clickedPreviewToken) {
        const tokenType = clickedPreviewToken.dataset.previewToken;
        const tokenValue = clickedPreviewToken.dataset.previewValue;
        if (tokenType && tokenValue) {
            event.preventDefault();
            event.stopPropagation();
            const previewRoot = clickedPreviewToken.closest('.battle-card-preview');
            const currentCardId = previewRoot?.dataset?.previewCardId || null;
            const currentCardName = previewRoot?.dataset?.previewCardName || '';
            void showPreviewDescriptionTooltip(clickedPreviewToken, tokenType, tokenValue, {
                openCardDetail: showCardDetailModal,
                currentCardId,
                currentCardName
            });
        }
        return;
    }

    if (clickedMatchHistoryRow) {
        event.preventDefault();
        event.stopPropagation();
        void showMatchHistoryCardPreview(clickedMatchHistoryRow).catch(error => {
            console.error('UI: Failed to load match history card preview.', error);
        });
        return;
    }

    const clickedLogCardToken = event.target.closest('[data-log-card-uid]');
    if (clickedLogCardToken) {
        const logCardUid = String(clickedLogCardToken.dataset.logCardUid || '').trim();
        if (!logCardUid) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        const card = findLogCardInstanceByUid(logCardUid);
        if (card) {
            showCardDetailModal(card);
        }
        return;
    }

    // --- Mulligan Phase Logic ---
    if (gameState.phase === 'Mulligan') {
        const clickedMulliganConfirm = event.target.closest('#confirm-mulligan-button');
        const clickedHandCard = event.target.closest('#player1-hand .card'); // Only care about player 1 hand

        if (clickedMulliganConfirm) {
            const localPlayer = getPlayerState(gameState.localPlayerId);
            const handUids = (localPlayer?.hand || []).map(c => c?.uid ?? c?.InstanceId).filter(Boolean);
            const selectedUids = Array.from(mulliganSelectedUids).filter(uid => handUids.includes(uid));

            // Call submitMulligan (the imported SignalR wrapper) from gameManager
            if (typeof submitMulligan === 'function') { // <<< FIXED: Check for the correct imported function 'submitMulligan'
                // Optimistically lock UI to prevent double-submit and to survive re-renders.
                if (confirmMulliganButtonEl) {
                    confirmMulliganButtonEl.disabled = true;
                    confirmMulliganButtonEl.textContent = '等待对方...';
                }
                if (localPlayer) {
                    localPlayer.hasCompletedMulligan = true;
                }
                mulliganSelectedUids = new Set();
                submitMulligan(selectedUids); // Call new SignalR wrapper
            } else {
                // console.error("UI Error: submitMulligan function is not available/imported!"); // Log the correct function name in error
                showAlert("错误：调度功能未加载（submitMulligan）"); // Clarify alert slightly for debugging
            }
            // Note: No need to manually remove 'selected-for-mulligan' class here,
            // as the next renderGame call after handleMulligan will redraw the hand.
            return; // Stop processing click after handling mulligan confirm

        } else if (clickedHandCard) {
            const localPlayer = getPlayerState(gameState.localPlayerId);
            const localCompleted = localPlayer?.hasCompletedMulligan === true;
            if (localCompleted) return;

            const uid = clickedHandCard.dataset.uid;
            if (!uid) return;

            const previewCard = findCardInstanceByUid(uid);
            if (previewCard) {
                renderBattleCardPreview(previewCard);
            }
            setFloatingHandCard(uid);

            if (mulliganSelectedUids.has(uid)) {
                mulliganSelectedUids.delete(uid);
                clickedHandCard.classList.remove('selected-for-mulligan');
            } else {
                mulliganSelectedUids.add(uid);
                clickedHandCard.classList.add('selected-for-mulligan');
            }
            // Optionally provide visual feedback immediately if needed, or rely on style changes
            return; // Stop processing click after toggling selection

        } else {
            // Clicked somewhere else during mulligan (e.g., empty space, opponent card) - do nothing
            return; // Stop processing click
        }
    }
    // --- End Mulligan Phase Logic ---

    // If not in Mulligan phase, continue with normal game interactions:

    const clickedMirageActionBtn = event.target.closest('[data-mirage-action]');
    if (clickedMirageActionBtn) {
        event.preventDefault();
        event.stopPropagation();
        if (isAwaitingPlayerChoice || gameState.isSpectator || clickedMirageActionBtn.disabled) return;
        const action = clickedMirageActionBtn.dataset.mirageAction;
        if (action === 'upgrade') void mirageUpgradeTierRequest();
        else if (action === 'refresh') void mirageRefreshShopRequest();
        else if (action === 'freeze') void mirageToggleFreezeRequest();
        else if (action === 'ready') void mirageSetReadyRequest();
        return;
    }

    const clickedMirageBuyBtn = event.target.closest('[data-mirage-buy-offer]');
    if (clickedMirageBuyBtn) {
        event.preventDefault();
        event.stopPropagation();
        if (isAwaitingPlayerChoice || gameState.isSpectator || clickedMirageBuyBtn.disabled) return;
        const offerId = clickedMirageBuyBtn.dataset.mirageBuyOffer;
        if (offerId) void mirageBuyOfferRequest(offerId);
        return;
    }

    const clickedMirageOfferCard = event.target.closest('.mirage-offer-card[data-offer-id]');
    if (clickedMirageOfferCard) {
        const offerId = String(clickedMirageOfferCard.dataset.offerId || '').trim();
        const localPlayer = getPlayerState(gameState.localPlayerId);
        const offers = Array.isArray(localPlayer?.mirage?.shopOffers ?? localPlayer?.mirage?.ShopOffers)
            ? (localPlayer.mirage.shopOffers ?? localPlayer.mirage.ShopOffers)
            : [];
        const matchedOffer = offers.find(o => String(o?.offerId ?? o?.OfferId ?? '') === offerId);
        if (matchedOffer) {
            const previewCard = createDiscoverPreviewCard({
                ...matchedOffer,
                CardDefinitionId: matchedOffer.cardDefinitionId ?? matchedOffer.CardDefinitionId ?? 0,
                Name: matchedOffer.name ?? matchedOffer.Name ?? '未知随从',
                Description: matchedOffer.description ?? matchedOffer.Description ?? '',
                ArtPreview: matchedOffer.artPreview ?? matchedOffer.ArtPreview ?? '',
                Type: matchedOffer.type ?? matchedOffer.Type ?? 'Follower',
                Cost: matchedOffer.tavernTier ?? matchedOffer.TavernTier ?? 1,
                Attack: matchedOffer.attack ?? matchedOffer.Attack ?? 0,
                Health: matchedOffer.health ?? matchedOffer.Health ?? 0
            });
            if (previewCard) {
                previewCard.isMirageCard = true;
                previewCard.mirageBuyCost = matchedOffer.buyCost ?? matchedOffer.BuyCost ?? 3;
                renderBattleCardPreview(previewCard);
            }
        }
        return;
    }

    const clickedMirageSelectLeaderBtn = event.target.closest('[data-mirage-select-leader]');
    if (clickedMirageSelectLeaderBtn) {
        event.preventDefault();
        event.stopPropagation();
        if (isAwaitingPlayerChoice || gameState.isSpectator || clickedMirageSelectLeaderBtn.disabled) return;
        const leaderDefId = Number(clickedMirageSelectLeaderBtn.dataset.mirageSelectLeader);
        if (Number.isInteger(leaderDefId) && leaderDefId > 0) {
            void mirageSelectLeaderRequest(leaderDefId);
        }
        return;
    }

    const clickedMirageRedeemBtn = event.target.closest('[data-mirage-redeem-ticket]');
    if (clickedMirageRedeemBtn) {
        event.preventDefault();
        event.stopPropagation();
        if (isAwaitingPlayerChoice || gameState.isSpectator || clickedMirageRedeemBtn.disabled) return;
        const ticketId = clickedMirageRedeemBtn.dataset.mirageRedeemTicket;
        const chosenCardDefId = Number(clickedMirageRedeemBtn.dataset.mirageRedeemCard);
        if (ticketId && Number.isInteger(chosenCardDefId) && chosenCardDefId > 0) {
            void mirageRedeemTicketRequest(ticketId, chosenCardDefId);
        }
        return;
    }

    const clickedMirageSellBtn = event.target.closest('[data-mirage-sell-uid]');
    if (clickedMirageSellBtn) {
        event.preventDefault();
        event.stopPropagation();
        if (isAwaitingPlayerChoice || gameState.isSpectator || clickedMirageSellBtn.disabled) return;
        const sellUid = clickedMirageSellBtn.dataset.mirageSellUid;
        if (sellUid) void mirageSellCardRequest(sellUid);
        return;
    }

    const clickedMirageMoveBtn = event.target.closest('[data-mirage-move-left], [data-mirage-move-right]');
    if (clickedMirageMoveBtn) {
        event.preventDefault();
        event.stopPropagation();
        if (isAwaitingPlayerChoice || gameState.isSpectator || clickedMirageMoveBtn.disabled) return;
        const moveUid = String(clickedMirageMoveBtn.dataset.mirageMoveLeft || clickedMirageMoveBtn.dataset.mirageMoveRight || '');
        const delta = clickedMirageMoveBtn.dataset.mirageMoveLeft ? -1 : 1;
        const localPlayer = getPlayerState(gameState.localPlayerId);
        const fieldUids = (Array.isArray(localPlayer?.field) ? localPlayer.field : [])
            .map(c => String(getCardUid(c) ?? ''))
            .filter(Boolean);
        const idx = fieldUids.indexOf(moveUid);
        const targetIdx = idx + delta;
        if (idx >= 0 && targetIdx >= 0 && targetIdx < fieldUids.length) {
            const swapped = [...fieldUids];
            [swapped[idx], swapped[targetIdx]] = [swapped[targetIdx], swapped[idx]];
            void mirageReorderFieldRequest(swapped);
        }
        return;
    }

    // Check if the click was on the End Turn / Lock Lineup button
    const clickedEndTurnButton = event.target.closest('#end-turn-button');
    if (clickedEndTurnButton) {
        if (gameState.isSpectator) return;
        if (clickedEndTurnButton.disabled) return;
        if (isMirageMode(gameState)) {
            if (gameState.phase === 'Recruit' && typeof mirageSetReadyRequest === 'function') {
                endTurnButtonEl.disabled = true;
                void mirageSetReadyRequest();
            }
            return;
        }
        if (!await confirmEndTurnAction()) return;
        if (typeof endTurnRequest === 'function') {
            endTurnButtonEl.disabled = true;
            endTurnRequest();
        } else {
            console.error(`UI Error: endTurnRequest function is not available/imported!`);
            showAlert("错误：结束回合功能未加载（endTurnRequest）");
        }
        return;
    }

    const clickedPpBoostButton = event.target.closest('.pp-boost-button');
    if (clickedPpBoostButton) {
        if (isAwaitingPlayerChoice) return;
        if (clickedPpBoostButton.disabled) return;
        if (typeof requestPpBoost === 'function') {
            requestPpBoost();
        } else {
            showAlert("错误：PP+1功能未加载！");
        }
        return;
    }

    const clickedUseButton = event.target.closest('.use-button');
    if (clickedUseButton) {
        if (isAwaitingPlayerChoice) return;
        if (gameState.isSpectator) return;
        const useUid = clickedUseButton.dataset.useUid;
        if (!useUid) return;
        const useCardInstance = findCardInstanceByUid(useUid);
        if (useCardInstance) {
            void tryUseHandCard(useCardInstance, 'button');
        } else {
            console.warn(`UI: Use button clicked but card instance ${useUid} not found.`);
        }
        return;
    }

    const clickedEvolutionButton = event.target.closest('.super-evolve-button, .evolve-button');
    if (clickedEvolutionButton) {
        const { superEvolveUid, evolveUid } = clickedEvolutionButton.dataset;
        await requestEvolutionAction(superEvolveUid ?? evolveUid, superEvolveUid ? 'super' : 'evolve');
        return;
    }

    // --- Check for Fusion Button Click ---
    const clickedFusionButton = event.target.closest('.fusion-button');
    if (clickedFusionButton) {
        if (isAwaitingPlayerChoice) return;
        if (gameState.isSpectator) return;
        const fusionUid = clickedFusionButton.dataset.fusionUid;
        const fusionAbilityId = clickedFusionButton.dataset.fusionAbilityId || null;
        if (!fusionUid) return;
        if (readOperationSettings().fusionConfirmation && !await confirmDialog('确认发动融合吗？')) return;
        requestFusion(fusionUid, fusionAbilityId);
        return;
    }
    // --- END Check for Fusion Button ---

    // --- Check for Act Button Click ---
    const clickedActButton = event.target.closest('.act-button');
    if (clickedActButton) {
        if (isAwaitingPlayerChoice) return;
        if (gameState.isSpectator) return;
        const actUid = clickedActButton.dataset.actUid;
        const actAbilityId = clickedActButton.dataset.actAbilityId || null;
        if (actUid) {
            if (typeof requestAct === 'function') {
                requestAct(actUid, actAbilityId);
            } else {
                showAlert("错误：启用功能未加载。");
            }
        }
        return;
    }
    // --- END Check for Act Button ---

    const clickedPreviewName = event.target.closest('.preview-detail-name');
    if (clickedPreviewName) {
        const previewUid = battleCardPreviewEl?.dataset?.previewUid;
        const previewCard = previewUid ? findCardInstanceByUid(previewUid) : null;
        if (previewCard) {
            showCardDetailModal(previewCard);
        }
        return;
    }

    const clickedCardElement = event.target.closest('.card');
    const clickedEmblemElement = event.target.closest('.emblem-slot[data-uid]');
    const clickedPlayer1Hud = event.target.closest('#player1-info');
    const clickedPlayer2Hud = event.target.closest('#player2-info');

    let targetUid = null;
    if (clickedCardElement) {
        targetUid = clickedCardElement.dataset.uid;
    } else if (clickedEmblemElement) {
        targetUid = clickedEmblemElement.dataset.uid;
    } else if ((clickedPlayer1Hud || clickedPlayer2Hud) && !event.target.closest('button')) {
        if (clickedPlayer2Hud && !isAwaitingPlayerChoice && clickedPlayer2Hud.classList.contains('is-mirage-tavern')) {
            return;
        }
        const clickedAvatarId = clickedPlayer1Hud ? 'player1-avatar' : 'player2-avatar';

        const localPlayerId = gameState.localPlayerId;
        const opponentPlayerId = gameState.players.find(p => p.id !== localPlayerId)?.id;

        let clickedPlayerActualId = null;
        if (clickedAvatarId === 'player2-avatar') {
            clickedPlayerActualId = opponentPlayerId;
        } else {
            clickedPlayerActualId = localPlayerId;
        }

        if (isAwaitingPlayerChoice) {
            const player = getPlayerState(clickedPlayerActualId);
            const leaderUid = player?.leaderCard?.uid ?? player?.leaderCard?.InstanceId ?? null;
            if (leaderUid) {
                targetUid = leaderUid;
            }
        } else {
            const clickedPlayer = clickedPlayerActualId ? getPlayerState(clickedPlayerActualId) : null;
            const leaderUid = clickedPlayer?.leaderCard?.uid ?? clickedPlayer?.leaderCard?.InstanceId ?? null;
            if (leaderUid) {
                targetUid = leaderUid;
            } else {
                console.error(`UI Error: Could not find leader card instance or UID for player ${clickedPlayerActualId}`, clickedPlayer);
            }
        }
    }

    if (targetUid) {
        // We clicked a card or an avatar
        const cardInstance = findCardInstanceByUid(targetUid);
        if (cardInstance) {
            handleCardClick(cardInstance); // Call the unified handler
        } else {
            // console.warn(`UI: Clicked target (UID: ${targetUid}) instance not found in gameState.`);
            // Avoid alert spam, just log
        }
    } else {
        // Clicked on non-card, non-avatar element

        // Player choice mode: clicking blank area cancels only if allowed.
        if (isAwaitingPlayerChoice) {
            // Ignore clicks inside the overlay UI.
            if (event.target.closest?.('#choice-overlay-root')) return;
            requestCancelPlayerChoiceUI();
            return;
        }

        // 1. Check if currently targeting a spell - Clicking outside cancels it
        if (gameState.selectedAttackerUid) {
            // Only cancel if click wasn't end turn or evolve button (already handled)
            if (!event.target.closest('#end-turn-button')
                && !event.target.closest('.evolve-button')
                && !event.target.closest('.super-evolve-button')
                && !event.target.closest('.act-button')
                && !event.target.closest('.use-button')) {
                cancelSelection(); // Call the imported gameManager function
            }
        }
        const clickedInBattleArea = event.target.closest('#battlefield, .hand, .field');
        if (clickedInBattleArea) {
            renderBattleCardPreview(null);
        }
        // 3. If neither targeting nor attacking, clicking non-interactive does nothing
        // else { }
    }
} // This brace correctly closes the handleGameClick function started on line 392


// 具体处理卡牌点击的逻辑 (由 handleGameClick 在找到实例后调用)
function handleCardClick(cardInstance) { // 需要传入卡牌实例
    // --- NEW: Player Choice Handling Logic ---
    if (isAwaitingPlayerChoice) {
        void handleChoiceTargetClick(cardInstance.uid);
        return; // IMPORTANT: Stop further execution in this click handler
    }
    // --- End Spell Target Selection ---

    // --- Normal Card Interaction (Not Spell Targeting) ---
    // (Original function body starts below this comment)
    // Use local player context for interactions (never the opponent's view)
    const localPlayerId = gameState.localPlayerId ?? uiCurrentPlayerId;
    const isLocalTurn = gameState.currentPlayerId === localPlayerId;

    // --- DEBUG REMOVED ---

    const playerState = getPlayerState(localPlayerId); // Get player state object using the known ID

    // Basic check for player state validity
    if (!playerState) {
        // console.error("UI Error in handleCardClick: Could not get player state for normal interaction.");
        // Optionally alert the user or return to prevent further errors
        showAlert("发生内部错误：无法获取玩家状态！");
        return;
    }

    if (!cardInstance) {
        // console.error("UI: handleCardClick received null cardInstance.");
        return; // 安全检查
    }

    renderBattleCardPreview(cardInstance);
    const clickedCardUid = getCardUid(cardInstance);
    const cardContext = resolveCardContext(cardInstance);
    const cardOwnerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId;
    const isLocalHandCard = cardContext === 'hand' && cardOwnerId === localPlayerId;
    if (isLocalHandCard && clickedCardUid) {
        setFloatingHandCard(clickedCardUid);
    } else {
        setFloatingHandCard(null);
    }

    // Only allow interactive actions on the local player's turn (preview still allowed).
    if (!isLocalTurn || gameState.isSpectator) {
        return;
    }

    // <<< MOVED DEBUG LOGGING EARLIER (Before owner/zone check) >>>
    // Use optional chaining (?) in case cardInstance is unexpectedly null/undefined at this point
    // <<< END MOVED DEBUG LOGGING >>>

    // Check if the card is owned by the current player and is in their hand
    if (cardInstance.ownerId === localPlayerId && cardInstance.zone.toLowerCase() === 'hand') {
        return;
    } // Closes if block for hand card check
    // Handle click on own field card
    else if (cardInstance.ownerId === localPlayerId && cardInstance.zone === 'Field') {
        // Initiate Attack with Own Field Card (ONLY if not currently targeting a spell AND unit can attack)
        // Check if the unit is actually allowed to attack
        if (cardInstance.canAttack === true && cardInstance.type === 'Follower') {
            initiateAttack(cardInstance.uid); // Call gameManager
        } else {
            // Optionally provide user feedback (e.g., a slight visual shake or message)
            // For now, just ignore the click.
        }
    }
    else if (cardInstance.zone === 'Leader') {
        // --- Handle Click on Leader (NOT targeting spell) ---
        // Spell targeting on leader is handled inside the "if (isTargetingSpell)" block earlier.
        // This block now only handles attack targeting or non-actionable clicks.
        if (gameState.selectedAttackerUid) {
            // --- Handle Attack Targeting on Leader ---
            // Get opponent ID (simplified check, assumes attacker UID format 'playerId_...')
            // FIX: selectedAttackerUid might not contain player ID prefix. Get attacker instance first.
            const attackerInstance = findCardInstanceByUid(gameState.selectedAttackerUid);
            const attackerOwnerId = attackerInstance?.ownerId; // Get owner from the instance
            const opponentPlayerId = gameState.players.find(p => p.id !== attackerOwnerId)?.id;

            if (opponentPlayerId && cardInstance.ownerId === opponentPlayerId) { // Ensure it's opponent's leader
                selectAttackTarget(cardInstance.uid); // Call gameManager
            } else {
                console.warn(`UI: Target leader owner (${cardInstance.ownerId}) is not the opponent (${opponentPlayerId}).`);
            }
        } else {
            // --- Handle Non-Actionable Click on Leader ---
        }
    } // End of 'else if (cardInstance.zone === 'Leader')' block
    else if (cardInstance.ownerId !== localPlayerId && cardInstance.zone === 'Field') {
        // Handle click on opponent field card (Select Attack Target OR Select Spell Target)
        // If NOT targeting a spell, proceed with attack target selection
        // Check if an attacker is currently selected from our side
        if (gameState.selectedAttackerUid) { // Check the imported global state
            selectAttackTarget(cardInstance.uid); // Call the imported function
        } else {
            // Optionally provide feedback or do nothing
        }
    }
    else { // Handle clicks on cards in other zones (deck, graveyard, etc.) - usually ignore
        // 卡牌在牌库、墓地或其他不可点击的位置/状态
    }
}

function tryUseHandCard(cardInstance, source = 'button') {
    const useActionState = getUseActionState(cardInstance, 'hand');
    if (!useActionState.showUse || !useActionState.useUid) {
        return false;
    }

    if (useActionState.usePlayable === false) {
        const reason = useActionState.useFailureReason || "该卡牌当前无法使用。";
        console.warn(`UI: ${source} use rejected for card ${cardInstance?.name ?? ''} (${useActionState.useUid}). Reason: ${reason}`);
        showAlert(reason);
        return true;
    }

    playCard(useActionState.useUid);
    return true;
}

function getEvolutionTurnKey() {
    return JSON.stringify([gameState.gameId, gameState.viewEpoch, gameState.turnNumber, gameState.localPlayerId]);
}

function getEvolutionPlayer() {
    if (isConnectionLocked || isAwaitingPlayerChoice || gameState.isSpectator || gameState.gameOver
        || gameState.isEndingTurn || gameState.phase !== 'Main' || isMirageMode(gameState)
        || gameState.currentPlayerId !== gameState.localPlayerId) return null;

    return getPlayerState(gameState.localPlayerId);
}

function canEvolveTarget(card, mode) {
    const state = getCardActionState(card);
    return mode === 'super' ? state.showSuperEvolve : state.showEvolve;
}

function getEvolutionDragAvailability(mode) {
    if (evolutionActionPending || handDragState.pointerId !== null || attackDragState.pointerId !== null) return null;
    const player = getEvolutionPlayer();
    if (!Array.isArray(player?.field)) return null;
    const targets = new Set();
    for (const card of player.field) {
        const uid = getCardUid(card);
        if (uid && canEvolveTarget(card, mode)) targets.add(String(uid));
    }
    return { turnKey: getEvolutionTurnKey(), targets };
}

async function requestEvolutionAction(cardUid, mode) {
    if (!cardUid || evolutionActionPending) return;
    const turnKey = getEvolutionTurnKey();
    const canSubmit = () => {
        const player = getEvolutionPlayer();
        const target = player?.field?.find(card => String(getCardUid(card)) === String(cardUid));
        return target && canEvolveTarget(target, mode);
    };
    if (!canSubmit()) return;

    evolutionActionPending = true;
    evolutionDrag.refresh();
    try {
        if (await confirmEvolutionAction(cardUid, mode) && getEvolutionTurnKey() === turnKey && canSubmit()) {
            await (mode === 'super' ? superEvolveCard : evolveCard)(cardUid);
        }
    } finally {
        evolutionActionPending = false;
        evolutionDrag.refresh();
    }
}

async function confirmEvolutionAction(cardUid, mode = 'evolve') {
    if (!readOperationSettings().evolutionConfirmation) return true;
    const normalizedMode = mode === 'super' ? 'super' : 'evolve';
    const card = findCardInstanceByUid(cardUid);
    const cardName = String(card?.name || card?.Name || '').trim() || '该卡牌';
    const actionLabel = normalizedMode === 'super' ? '超进化' : '进化';
    const message = `确认要对【${cardName}】执行${actionLabel}吗？`;

    if (typeof window === 'undefined' || typeof confirmDialog !== 'function') {
        return true;
    }
    return confirmDialog(message);
}

async function confirmEndTurnAction() {
    if (!readOperationSettings().endTurnConfirmation) return true;
    const message = '确认结束当前回合吗？';
    if (typeof window === 'undefined' || typeof confirmDialog !== 'function') {
        return true;
    }
    return confirmDialog(message);
}

/**
 * Shows the player choice UI when the server requests a choice from the player.
 * This function is called by gameManager when a PlayerChoiceRequested event is received.
 * @param {Object} eventData - The choice request data from the server
 * @param {string} eventData.SourceCardUid - The UID of the card that initiated the choice
 * @param {Array<string>} eventData.SelectableTargets - Array of UIDs that can be selected
 * @param {string} eventData.ChoicePrompt - The prompt to show to the player
 */
export function showPlayerChoiceUI(choiceRequest) {

    if (isAwaitingPlayerChoice) {
        console.warn("UI: Received a new player choice request while another one is active. Clearing UI state without notifying backend.");
        endPlayerChoice(false);
    }

    const selectableTargetIds = Array.isArray(choiceRequest?.SelectableTargetIds) ? choiceRequest.SelectableTargetIds : [];

    if (selectableTargetIds.length === 0) {
        const canSubmitEmpty = isChoiceSelectionValid(choiceRequest, 0);
        if (choiceRequest?.IsCancellable !== true && !canSubmitEmpty) {
            console.warn("UI: No selectable targets provided for a non-cancellable choice. Ending choice UI.");
            endPlayerChoice(false);
            return;
        }

        console.info("UI: No selectable targets provided. Rendering choice overlay for explicit confirmation.");
    }

    // Enter player choice mode and render.
    startPlayerChoice(choiceRequest);
    renderGame(gameState);
    renderChoiceOverlayUI();
    updateChoiceOverlayUI();
}

// --- Ability Trigger Toast UI ---
const ABILITY_TRIGGER_TOAST_DEFAULT_DURATION_MS = 5000;
const ABILITY_TRIGGER_TOAST_FADE_MS = 360;

export function showAbilityTriggerToast(payload = {}) {
    // Guard against rendering on non-game routes, but do not force-clear here
    // to avoid dropping valid toasts during route transition edge frames.
    if (!isGamePageActive()) {
        return;
    }

    const normalized = normalizeAbilityTriggerToastPayload(payload);
    if (!normalized) return;

    const root = ensureAbilityTriggerToastRoot();
    positionAbilityTriggerToastRoot(root);
    const toastItem = renderAbilityTriggerToast(root, normalized);
    if (!toastItem) return;

    requestAnimationFrame(() => {
        toastItem.classList.add('is-visible');
    });

    void scheduleAbilityTriggerToastRemoval(root, toastItem, normalized.durationMs);
}

function isGamePageActive() {
    const gamePage = document.getElementById('game-page');
    return !!gamePage && gamePage.classList.contains('active');
}

export function clearAbilityTriggerToasts() {
    const root = document.getElementById('ability-trigger-toast-root');
    if (!root) return;
    root.remove();
}

function normalizeAbilityTriggerToastPayload(payload) {
    if (!payload || typeof payload !== 'object') return null;

    const description = String(payload.description ?? '').trim();

    const title = String(payload.title ?? '').trim() || '能力触发';
    const subtitle = String(payload.subtitle ?? '').trim();
    const previewPath = String(payload.previewPath ?? '').trim();
    const previewUrl = resolveArtUrl(previewPath);
    const tone = String(payload.tone ?? '').trim().toLowerCase() === 'enemy' ? 'enemy' : 'ally';
    const durationMs = Number.isFinite(payload.durationMs)
        ? Math.max(2400, Math.trunc(payload.durationMs))
        : ABILITY_TRIGGER_TOAST_DEFAULT_DURATION_MS;

    return {
        title,
        subtitle,
        description,
        hasDetail: description.length > 0,
        previewUrl,
        tone,
        durationMs
    };
}

async function scheduleAbilityTriggerToastRemoval(root, toastItem, durationMs) {
    if (!root || !toastItem) return;

    const visibleMs = Math.max(800, durationMs - ABILITY_TRIGGER_TOAST_FADE_MS);
    await delay(visibleMs);

    toastItem.classList.remove('is-visible');
    toastItem.classList.add('is-hiding');
    await delay(ABILITY_TRIGGER_TOAST_FADE_MS);

    if (toastItem.isConnected) {
        toastItem.remove();
    }

    if (root.childElementCount === 0) {
        root.remove();
    }
}

function ensureAbilityTriggerToastRoot() {
    let root = document.getElementById('ability-trigger-toast-root');
    if (root) return root;

    root = document.createElement('div');
    root.id = 'ability-trigger-toast-root';
    root.className = 'ability-trigger-toast-root';
    const gameContainer = gameContainerEl ?? document.getElementById('game-container');
    if (gameContainer) {
        gameContainer.appendChild(root);
    } else {
        document.body.appendChild(root);
    }
    return root;
}

function positionAbilityTriggerToastRoot(root) {
    if (!root) return;

    const cornerPod = document.getElementById('player1-corner-pod');
    if (!cornerPod) {
        root.style.left = '18px';
        root.style.bottom = '136px';
        return;
    }

    const podRect = cornerPod.getBoundingClientRect();
    const left = Math.max(14, Math.round(podRect.left));
    const bottom = Math.max(120, Math.round(window.innerHeight - podRect.top + 12));

    root.style.left = `${left}px`;
    root.style.bottom = `${bottom}px`;
}

function renderAbilityTriggerToast(root, payload) {
    if (!root || !payload) return;

    const item = document.createElement('div');
    item.className = 'ability-trigger-toast-item';

    const panel = document.createElement('div');
    panel.className = 'ability-trigger-toast';
    panel.classList.add(payload.tone === 'enemy' ? 'ability-trigger-toast--enemy' : 'ability-trigger-toast--ally');

    const header = document.createElement('div');
    header.className = 'ability-trigger-toast-header';

    const icon = document.createElement('div');
    icon.className = 'ability-trigger-toast-icon';
    if (payload.previewUrl) {
        icon.style.backgroundImage = `url('${payload.previewUrl}')`;
        icon.classList.add('has-art');
    } else {
        icon.classList.add('is-empty');
        icon.textContent = '能';
    }

    const titleGroup = document.createElement('div');
    titleGroup.className = 'ability-trigger-toast-title-group';

    const title = document.createElement('div');
    title.className = 'ability-trigger-toast-title';
    title.textContent = payload.title;
    titleGroup.appendChild(title);

    if (payload.subtitle) {
        const subtitle = document.createElement('div');
        subtitle.className = 'ability-trigger-toast-subtitle';
        subtitle.textContent = payload.subtitle;
        titleGroup.appendChild(subtitle);
    }

    header.appendChild(icon);
    header.appendChild(titleGroup);

    panel.appendChild(header);
    if (payload.hasDetail) {
        const body = document.createElement('div');
        body.className = 'ability-trigger-toast-body';
        body.textContent = payload.description;
        panel.appendChild(body);
    } else {
        panel.classList.add('no-detail');
    }
    item.appendChild(panel);
    root.appendChild(item);
    return item;
}

// Route switch lifecycle: remove combat toasts immediately when leaving game page.
document.addEventListener('cleanupGame', () => {
    evolutionDrag.cancel();
    cancelAllBattleMotions();
    cardAnimationSystem?.dispose();
    cardAnimationSystem = null;
    hpGhostSettleTimers.forEach(timerId => window.clearTimeout(timerId));
    hpGhostSettleTimers.clear();
    resetAttackDragState();
    hidePreviewDescriptionTooltip();
    removeChoiceOverlayUI();
    isAwaitingPlayerChoice = false;
    currentChoiceRequest = null;
    currentChoiceSelectedTargetIds = new Set();
    hoveredHandPreviewUid = null;
    gameContainerEl?.classList.remove('player-choice-active');
    setFloatingHandCard(null);
    renderBattleCardPreview(null);
    player1FieldEl?.replaceChildren();
    player2FieldEl?.replaceChildren();
    player1HandEl?.replaceChildren();
    player2HandEl?.replaceChildren();
    clearAbilityTriggerToasts();
    clearInMatchChatUiState();
    clearInMatchUsedCardUiState();
    clearTrackedVisualTimeouts();
    removeTransientVisualArtifacts();
    removeStrayBattleCardArtifacts();
    revealAnimator.clear();
});

// --- Reveal UI: Mini flight, flip, return, then conceal the whole hidden batch. ---
const revealAnimator = createRevealAnimator({
    getContainer: () => gameContainerEl ?? document.getElementById('game-container'),
    isActive: isGamePageActive,
    resolveArt: resolveArtUrl,
    resolveOrigin(card, options) {
        const container = gameContainerEl ?? document.getElementById('game-container');
        if (!container) return null;
        const uid = String(card.InstanceId ?? card.instanceId ?? card.uid ?? '');
        const own = String(card.OwnerPlayerId ?? card.ownerId ?? options.playerId ?? '') === String(gameState.localPlayerId);
        const hand = document.getElementById(own ? 'player1-hand' : 'player2-hand');
        const direct = [...container.querySelectorAll('[data-uid]')].find(element => String(element.dataset.uid) === uid);
        const index = Number(options.handIndex);
        const element = direct ?? (Number.isInteger(index) && index >= 0 ? hand?.children[index] : null);
        const bounds = container.getBoundingClientRect();
        const origin = element?.getBoundingClientRect() ?? hand?.getBoundingClientRect() ?? {left:bounds.left+bounds.width/2-60,top:own?bounds.bottom-170:bounds.top,width:120,height:170};
        const hidden = !own && !gameState.dualControl && !gameState.isSpectator && (!direct || direct.classList.contains('card-back'));
        return {origin, element, hidden};
    }
});
export function showRevealOverlay(cardDto, options = {}) { return revealAnimator.enqueue(cardDto, options); }

// --- Fusion UI ---
export function showFusionAnimation(fusingPlayerId, fusedCardInstanceIds = []) {
    const ids = Array.isArray(fusedCardInstanceIds) ? fusedCardInstanceIds : [];
    if (!fusingPlayerId || ids.length === 0 || !isGamePageActive()) return;

    ids.forEach(uid => {
        markTimedUidFlag(recentFusedUids, uid, 2200);
        markTimedUidFlag(suppressedExitUids, uid, 2200);
    });

    const transientRoot = ensureTransientVisualRoot();
    if (!transientRoot) return;

    const isLocal = fusingPlayerId === gameState.localPlayerId;
    if (!isLocal) {
        showFusionBurst(ids.length);
        return;
    }

    const handRoot = document.getElementById('player1-hand');
    if (!handRoot) return;

    const target = getFusionTargetPoint();
    showFusionBurst(ids.length);

    ids.forEach((uid, idx) => {
        const el = handRoot.querySelector(`.card[data-uid="${uid}"]`);
        if (!el) return;

        const rect = el.getBoundingClientRect();
        const clone = el.cloneNode(true);
        clone.classList.add('fusion-float-card');
        clone.style.left = `${rect.left}px`;
        clone.style.top = `${rect.top}px`;
        clone.style.width = `${rect.width}px`;
        clone.style.height = `${rect.height}px`;

        transientRoot.appendChild(clone);

        const fromX = rect.left + rect.width / 2;
        const fromY = rect.top + rect.height / 2;
        const dx = (target.x - fromX);
        const dy = (target.y - fromY) - (idx * 8);

        // Start next frame so the transition runs reliably.
        requestAnimationFrame(() => {
            clone.style.transform = `translate(${dx}px, ${dy}px) scale(0.25)`;
            clone.style.opacity = '0';
        });

        runTrackedVisualTimeout(() => clone.remove(), 700);
    });
}

function getFusionTargetPoint() {
    const rect = (gameContainerEl ?? document.body).getBoundingClientRect();
    return {
        x: rect.left + rect.width * 0.5,
        y: rect.top + rect.height * 0.52
    };
}

function showFusionBurst(count) {
    if (!isGamePageActive()) return;
    const transientRoot = ensureTransientVisualRoot();
    if (!transientRoot) return;

    transientRoot.querySelector('#fusion-burst-root')?.remove();

    const root = document.createElement('div');
    root.id = 'fusion-burst-root';
    root.className = 'fusion-burst-root';
    root.innerHTML = `
        <div class="fusion-burst">
            <div class="fusion-burst-ring"></div>
            <div class="fusion-burst-text">Fusion x${count}</div>
        </div>
    `;

    transientRoot.appendChild(root);
    runTrackedVisualTimeout(() => root.remove(), 700);
}

/**
 * Renders the player-choice overlay UI (prompt/controls + optional selection panel).
 */
function renderChoiceOverlayUI() {
    removeChoiceOverlayUI();
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return;

    const container = gameContainerEl ?? document.getElementById('game-container') ?? document.body;

    const overlayRoot = document.createElement('div');
    overlayRoot.id = 'choice-overlay-root';
    overlayRoot.className = 'choice-overlay-root';
    overlayRoot.dataset.choiceKind = currentChoiceRequest.TargetKey === 'decision' ? 'decision' : 'target';
    container.appendChild(overlayRoot);

    if (currentChoiceRequest.TargetKey === 'discover') {
        document.body.append(overlayRoot);
        overlayRoot.classList.add('discover-overlay');
        const dialog = document.createElement('section');
        dialog.className = 'discover-dialog';dialog.setAttribute('role','dialog');dialog.setAttribute('aria-modal','true');dialog.setAttribute('aria-label','发现');
        dialog.innerHTML = '<h2>发现</h2><p>请选择一张卡片加入手牌</p><div class="discover-candidates"></div>';
        overlayRoot.append(dialog);
        const boardToggle = document.createElement('button');
        boardToggle.type = 'button';
        boardToggle.className = 'discover-board-toggle';
        boardToggle.textContent = '查看战场';
        boardToggle.setAttribute('aria-pressed', 'false');
        overlayRoot.append(boardToggle);
        let viewingBoard = false;
        // Keep the pending choice intact. Only card inspection is allowed while
        // the discovery panel is folded away; never pass input to battle actions.
        const guardBoardInput = event => {
            if (!viewingBoard) return;
            event.preventDefault();
            event.stopImmediatePropagation();
            if (event.type !== 'click') return;
            const element = event.target.closest?.('[data-uid]');
            const card = element ? findCardInstanceByUid(element.dataset.uid) : null;
            if (card) renderBattleCardPreview(card);
        };
        const guardedEvents = ['click', 'pointerdown', 'mousedown', 'touchstart', 'dblclick', 'contextmenu', 'keydown'];
        for (const type of guardedEvents) container.addEventListener(type, guardBoardInput, {capture: true, passive: false});
        cleanupDiscoverBoardView = () => {
            for (const type of guardedEvents) container.removeEventListener(type, guardBoardInput, true);
        };
        boardToggle.onclick = () => {
            viewingBoard = !viewingBoard;
            restoreDiscoverPreview?.(); restoreDiscoverPreview = null;
            renderBattleCardPreview(null);
            overlayRoot.classList.toggle('discover-viewing-board', viewingBoard);
            dialog.hidden = viewingBoard;
            boardToggle.textContent = viewingBoard ? '返回发现' : '查看战场';
            boardToggle.setAttribute('aria-pressed', String(viewingBoard));
            dialog.setAttribute('aria-modal', String(!viewingBoard));
        };
        const list=dialog.querySelector('.discover-candidates');
        for(const dto of currentChoiceRequest.SelectableCardInstances || []) {
            const card=createDiscoverPreviewCard(dto); if(!card)continue;
            const tile=document.createElement('article');tile.className='discover-candidate';
            const preview=document.createElement('button');preview.type='button';preview.className='discover-card';
            preview.setAttribute('aria-label', `预览${dto.Name || dto.name || '卡片'}`);
            const art=document.createElement('img');art.src=resolveArtUrl(dto.ArtPreview ?? dto.artPreview ?? '');art.alt=dto.Name ?? dto.name ?? '';preview.append(art);
            preview.append(miniCardBadge('cost', dto.CurrentCost ?? dto.Cost ?? dto.cost ?? 0));
            if (String(dto.Type ?? dto.type ?? '').toLowerCase() === 'follower') {
                preview.append(miniCardBadge('attack', dto.CurrentAttack ?? dto.Attack ?? dto.attack ?? 0), miniCardBadge('health', dto.CurrentHealth ?? dto.Health ?? dto.health ?? 0));
            }
            const name=document.createElement('button');name.type='button';name.className='discover-card-name';name.textContent=dto.Name ?? dto.name ?? '';
            name.onclick=()=>showCardDetailModal(card);
            preview.onclick=()=> {
                renderBattleCardPreview(card);
                const previewName=battleCardPreviewEl?.querySelector('.preview-detail-name');
                if(previewName) previewName.onclick=event=>{event.preventDefault();event.stopPropagation();showCardDetailModal(card);};
                if(battleCardPreviewEl && !restoreDiscoverPreview) {
                    const parent=battleCardPreviewEl.parentNode,next=battleCardPreviewEl.nextSibling;
                    overlayRoot.append(battleCardPreviewEl);battleCardPreviewEl.classList.add('discover-preview');
                    restoreDiscoverPreview=()=>{battleCardPreviewEl.classList.remove('discover-preview');parent?.insertBefore(battleCardPreviewEl,next?.parentNode===parent?next:null);};
                }
            };
            const select=document.createElement('button');select.type='button';select.className='discover-select';select.textContent='选择';
            select.onclick=async()=>{for(const b of list.querySelectorAll('.discover-select'))b.disabled=true;currentChoiceSelectedTargetIds=new Set([String(dto.InstanceId)]);try{await confirmPlayerChoice();}finally{if(overlayRoot.isConnected)for(const b of list.querySelectorAll('.discover-select'))b.disabled=false;}};
            tile.append(preview,name,select);list.append(tile);
        }
        return;
    }

    const { min, max } = getChoiceMinMax(currentChoiceRequest);
    const hasSelectableTargets = Array.isArray(currentChoiceRequest.SelectableTargetIds)
        && currentChoiceRequest.SelectableTargetIds.length > 0;
    const isCancellable = currentChoiceRequest.IsCancellable === true;
    const isDecisionKind = currentChoiceRequest.TargetKey === 'decision';

    if (isDecisionKind) {
        modeChoiceDialog = createModeChoiceDialog({
            root: overlayRoot, request: currentChoiceRequest, min, max,
            renderDescription: renderDescriptionHtml,
            onSelect: uid => { void handleChoiceTargetClick(uid); },
            onConfirm: () => { void confirmPlayerChoice(); },
            onCancel: requestCancelPlayerChoiceUI
        });
        updateChoiceOverlayUI();
        return;
    }

    const controls = document.createElement('div');
    controls.className = `choice-controls-overlay choice-overlay${hasSelectableTargets ? '' : ' is-empty-targets'}`;
    controls.addEventListener('click', e => e.stopPropagation());

    const message = currentChoiceRequest.Message || '请选择目标';
    const kickerLabel = isDecisionKind
        ? 'MODE SELECT'
        : (hasSelectableTargets ? 'TARGET SELECT' : 'NO TARGET');
    const hint = hasSelectableTargets
        ? (min === max
            ? (isCancellable ? `点选 ${max} 个高亮目标（可取消）` : `点选 ${max} 个高亮目标后提交`)
            : (min === 0 ? `可选至多 ${max} 个目标，确定提交` : `请选择 ${min}~${max} 个目标后提交`))
        : (isCancellable ? '当前无可选目标，确定继续或取消中止' : '当前无可选目标，点击确定继续');

    controls.innerHTML = `
        <div class="choice-pod-header">
            <span class="choice-pod-kicker">
                <span class="choice-pod-reticle" aria-hidden="true"></span>
                <span class="choice-pod-kicker-label">${kickerLabel}</span>
            </span>
            <div class="choice-selected-count"><span class="choice-count-prefix">已选</span><span id="choice-selected-count">${formatChoiceCountLabel(0, min, max)}</span></div>
        </div>
        <div class="choice-prompt-text">${escapeAttributeValue(message)}</div>
        <div class="choice-prompt-hint">${hint}</div>
        <div class="choice-controls-row">
            <div class="choice-controls-actions">
                <button id="choice-confirm-btn" class="choice-btn choice-confirm-btn" type="button">确定</button>
                <button id="choice-cancel-btn" class="choice-btn choice-cancel-btn" type="button">取消</button>
            </div>
        </div>
    `;

    overlayRoot.appendChild(controls);

    const confirmBtn = controls.querySelector('#choice-confirm-btn');
    const cancelBtn = controls.querySelector('#choice-cancel-btn');

    if (confirmBtn) {
        confirmBtn.addEventListener('click', e => {
            e.stopPropagation();
            void confirmPlayerChoice();
        });
    }

    if (cancelBtn) {
        cancelBtn.style.display = currentChoiceRequest.IsCancellable === true ? '' : 'none';
        cancelBtn.addEventListener('click', e => {
            e.stopPropagation();
            requestCancelPlayerChoiceUI();
        });
    }

    // Optional: selection panel for non-clickable zones (deck/graveyard candidates).
    const allCardInstances = Array.isArray(currentChoiceRequest.SelectableCardInstances)
        ? currentChoiceRequest.SelectableCardInstances
        : [];

    const selectableIdSet = new Set((currentChoiceRequest.SelectableTargetIds || []).map(x => String(x)));
    const missingCardInstances = allCardInstances
        .filter(dto => {
            const uid = dto?.InstanceId ?? null;
            if (!uid) return false;
            if (!selectableIdSet.has(String(uid))) return false;
            return !resolveRenderableTargetElement(uid);
        });

    if (missingCardInstances.length > 0) {
        overlayRoot.classList.add('choice-centered-modal');
        document.body.appendChild(overlayRoot); // Escape the battlefield's stacking context and cover drag ghosts.
        const dialog = document.createElement('section'); dialog.className = 'choice-centered-dialog'; dialog.setAttribute('role', 'dialog'); dialog.setAttribute('aria-modal', 'true'); dialog.setAttribute('aria-label', currentChoiceRequest.TargetKey === 'decision' ? '选择模式' : '选择卡牌');
        overlayRoot.append(dialog); dialog.append(controls);
        const panel = document.createElement('div');
        panel.className = 'choice-panel-overlay choice-overlay';
        panel.addEventListener('click', e => e.stopPropagation());

        const list = document.createElement('div');
        list.className = 'choice-card-list';

        allCardInstances.filter(dto => selectableIdSet.has(String(dto?.InstanceId))).forEach(dto => {
            const uid = dto.InstanceId;
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'choice-card';
            btn.dataset.uid = String(uid);
            const choiceArt = resolveArtUrl(dto.ArtPreview ?? dto.artPreview ?? '');
            btn.innerHTML = `
                ${choiceArt ? `<img class="choice-card-art" src="${escapeAttributeValue(choiceArt)}" alt="" loading="lazy" decoding="async">` : ''}
                <div class="choice-card-cost">${dto.Cost ?? '?'}</div>
                <div class="choice-card-name">${escapeAttributeValue(dto.Name ?? 'Unknown Card')}</div>
                <div class="choice-card-desc">${renderDescriptionHtml(dto.Description ?? '')}</div>
            `;
            btn.addEventListener('click', e => {
                e.stopPropagation();
                void handleChoiceTargetClick(uid);
            });
            list.appendChild(btn);
        });

        panel.innerHTML = currentChoiceRequest.TargetKey === 'decision' ? '' : '<div class="choice-panel-title">可选择的卡牌</div>';
        panel.appendChild(list);
        dialog.appendChild(panel);
    }
}
