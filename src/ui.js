// src/ui.js - UI渲染与交互处理
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
//

// 导入依赖 (稍后根据需要添加)
// import { gameState } from './gameManager.js'; // 获取状态
// import { playCard, initiateAttack, selectAttackTarget, evolveCard } from './gameManager.js'; // 调用动作

// Import necessary functions and the gameState object
import {
    gameState,
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
    // handleMulligan, // Removed import - Now uses submitMulligan
    isValidSpellTarget,
    submitMulligan, // Added import for SignalR mulligan submission
    endTurnRequest,  // Added import for SignalR end turn request
    submitPlayerChoice,
    cancelPlayerChoice
} from './gameManager.js';
import { buildPreviewSlateHtml } from './previewSlate.js';
import { showPreviewDescriptionTooltip } from './previewDescriptionTooltip.js';
import { showCardDetailModal } from './components/CardDetailMockup.js';
import { hydrateDescriptionReferences } from './previewDescriptionReferences.js';
import { ensureEnumLocalizations, getCachedEnumLabel } from './enumLocalizationCache.js';
import { getUserSession } from './userSession.js';
import badgeWardUrl from './badge/Ward.png';
import badgeBaneUrl from './badge/Bane.png';
import badgeDrainUrl from './badge/Drain.png';
import badgeAmbushUrl from './badge/Ambush.png';
import badgePressureUrl from './badge/Pressure.png';
import badgeAuraUrl from './badge/Aura.png';
import badgeShieldUrl from './badge/Shield.png';
import badgeAbilityResistUrl from './badge/AbilityResist.png';
import badgeShackleUrl from './badge/Shackle.png';
// getCardDefinition is obsolete. Card instances now contain all necessary data.
// import { endTurn } from './turnManager.js'; // Removed import - Now uses endTurnRequest

export { renderGame, showDamageNumber, showHealNumber, updatePpDisplay, updateEpDisplay, updateSepDisplay, updatePpBoostDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateBloodPoolDisplay, updateComboDisplay, updateHealthDisplay, updateHandDisplay, addUnitToField, updateUnitDisplay, removeUnitFromField, updateFieldDisplay, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus, clearSelectionHighlights, resetMulliganSelection, refreshBattleCardPreviewIfCurrent, renderHighlights, updateEmblemZone, updateTurnTimerDisplay, setConnectionStatus, setOpponentConnectionStatus, refreshCardInstanceUi };

let mulliganSelectedUids = new Set();
const EMBLEM_SLOT_COUNT = 5;
const DOUBLE_CLICK_WINDOW_MS = 260;
let lastCardClick = { uid: null, time: 0 };
const cardArtLoadPromises = new Map();
const CARD_ART_RETRY_COUNT = 2;
const CARD_ART_RETRY_DELAY_MS = 120;

function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

function normalizeArtPath(rawPath) {
    if (rawPath === null || rawPath === undefined) return '';

    const trimmed = String(rawPath).trim();
    if (!trimmed) return '';

    if (/^(https?:)?\/\//i.test(trimmed) || trimmed.startsWith('data:')) {
        return trimmed;
    }

    let normalized = trimmed.replace(/\\/g, '/');
    if (!normalized.startsWith('/')) {
        if (normalized.startsWith('assets/')) {
            normalized = `/${normalized}`;
        } else if (!normalized.startsWith('./') && !normalized.startsWith('../')) {
            normalized = `/${normalized}`;
        }
    }

    return normalized;
}

function resolveArtUrl(rawPath) {
    const normalized = normalizeArtPath(rawPath);
    if (!normalized) return '';
    return encodeURI(normalized).replace(/'/g, '%27');
}

function withRetryQuery(url, attempt) {
    const separator = url.includes('?') ? '&' : '?';
    return `${url}${separator}r=${Date.now()}_${attempt}`;
}

async function probeImage(url) {
    await new Promise((resolve, reject) => {
        const image = new Image();
        image.decoding = 'async';
        image.onload = () => resolve();
        image.onerror = () => reject(new Error(`Image load failed: ${url}`));
        image.src = url;
    });
}

function preloadArt(rawPath) {
    const url = resolveArtUrl(rawPath);
    if (!url) {
        return Promise.resolve('');
    }

    const existing = cardArtLoadPromises.get(url);
    if (existing) {
        return existing;
    }

    const loadPromise = (async () => {
        for (let attempt = 0; attempt <= CARD_ART_RETRY_COUNT; attempt++) {
            const targetUrl = attempt === 0 ? url : withRetryQuery(url, attempt);
            try {
                await probeImage(targetUrl);
                return url;
            } catch (error) {
                if (attempt >= CARD_ART_RETRY_COUNT) {
                    throw error;
                }
                await delay(CARD_ART_RETRY_DELAY_MS * (attempt + 1));
            }
        }
        return url;
    })();

    const guardedPromise = loadPromise.catch(error => {
        cardArtLoadPromises.delete(url);
        throw error;
    });

    cardArtLoadPromises.set(url, guardedPromise);
    return guardedPromise;
}

function applyArtBackground(artEl, rawPath) {
    if (!artEl) return;
    const resolvedUrl = resolveArtUrl(rawPath);
    artEl.dataset.artUrl = resolvedUrl;

    if (!resolvedUrl) {
        artEl.style.backgroundImage = '';
        artEl.classList.remove('has-art');
        return;
    }

    void preloadArt(rawPath)
        .then(loadedUrl => {
            if (artEl.dataset.artUrl !== loadedUrl) return;
            artEl.style.backgroundImage = `url('${loadedUrl}')`;
            artEl.classList.add('has-art');
        })
        .catch(() => {
            if (artEl.dataset.artUrl !== resolvedUrl) return;
            artEl.style.backgroundImage = '';
            artEl.classList.remove('has-art');
        });
}

function resetMulliganSelection() {
    mulliganSelectedUids = new Set();
    try {
        player1HandEl?.querySelectorAll?.('.card.selected-for-mulligan')?.forEach?.(el => el.classList.remove('selected-for-mulligan'));
    } catch (_) {
        // ignore
    }
}

// --- Specific UI Update Functions ---

/**
 * Updates the PP display for a specific player.
 * @param {string} playerId - The GUID of the player.
 * @param {number} currentPp - The current PP value.
 * @param {number} maxPp - The maximum PP value.
 */
function updatePpDisplay(playerId, currentPp, maxPp) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const ppBarContainer = document.getElementById(`${prefix}-pp-bar`);
    
    // Update text if container exists (handled in renderPlayerStats, but good for standalone updates)
    const statsContainer = document.getElementById(`${prefix}-stats`);
    if (statsContainer) {
        const ppTextEl = statsContainer.querySelector('.pp-text');
        if (ppTextEl) ppTextEl.textContent = `${currentPp ?? 0} / ${maxPp ?? 0}`;
    }

    // Render PP Bar Segments
    if (ppBarContainer) {
        ppBarContainer.innerHTML = '';
        const displayMax = 10; // Always show 10 slots for consistency
        const safeMax = Math.max(0, Math.min(maxPp ?? 0, displayMax));
        const safeCurrent = Math.max(0, Math.min(currentPp ?? 0, displayMax));
        const overflowCount = Math.max(0, safeCurrent - safeMax);

        for (let i = 0; i < displayMax; i++) {
            const segment = document.createElement('div');
            segment.classList.add('pp-segment');
            
            if (i < safeMax) {
                if (i < safeCurrent) {
                    segment.classList.add('active'); // Filled PP
                }
            } else if (i < safeMax + overflowCount) {
                segment.classList.add('overflow'); // PP+1 overflow
            } else {
                segment.classList.add('empty'); // Locked/Max capacity limit
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
}

// --- End Specific UI Update Functions ---

/**
* Updates the deck count display for a specific player.
* @param {string} playerId - The GUID of the player.
* @param {number} count - The current deck count.
*/
function updateDeckCountDisplay(playerId, count) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const deckCountEl = document.getElementById(`${prefix}-deck-count`);

    if (deckCountEl) {
        deckCountEl.textContent = count ?? '0';
    }
}

/**
* Updates the graveyard count display for a specific player.
* @param {string} playerId - 'player1' or 'player2'.
* @param {number} count - The current graveyard count.
*/
function updateGraveyardCountDisplay(playerId, count) {
    if (!gameState.localPlayerId) return;

    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const graveyardCountEl = document.getElementById(`${prefix}-grave-count`);

    if (graveyardCountEl) {
        graveyardCountEl.textContent = count ?? '0';
    }
}

/**
* Updates the blood pool display for a specific player.
* @param {string} playerId - The GUID of the player.
* @param {number} count - The current blood pool count.
*/
function updateBloodPoolDisplay(playerId, count) {
    if (!gameState.localPlayerId) return;

    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const bloodCountEl = document.getElementById(`${prefix}-blood-count`);

    if (bloodCountEl) {
        bloodCountEl.textContent = count ?? '0';
    }
}

/**
* Updates the combo display for a specific player.
* @param {string} playerId - The GUID of the player.
* @param {number} count - The current combo count.
*/
function updateComboDisplay(playerId, count) {
    if (!gameState.localPlayerId) return;

    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const comboCountEl = document.getElementById(`${prefix}-combo-count`);

    if (comboCountEl) {
        comboCountEl.textContent = count ?? '0';
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
    const newHealth = unitInstance.currentHealth ?? '??';

    // 【重构】统一的生命值更新函数
    if (unitInstance.type === 'Leader') {
        // 根据ownerId和本地玩家ID，判断是player1还是player2
        const uiPlayerPrefix = (unitInstance.ownerId === gameState.localPlayerId) ? 'player1' : 'player2';
        const hpContainer = document.getElementById(`${uiPlayerPrefix}-hp`);
        
        if (hpContainer) {
            const maxHp = unitInstance.maxHealth ?? 20;
            const hpPercent = Math.max(0, Math.min(100, (newHealth / maxHp) * 100));
            
            const valueEl = hpContainer.querySelector('.hp-value');
            const fillEl = hpContainer.querySelector('.hp-bar-fill');
            const ghostEl = hpContainer.querySelector('.hp-bar-ghost');

            if (valueEl) valueEl.textContent = `${newHealth}`;
            
            if (fillEl) {
                fillEl.style.width = `${hpPercent}%`;
                if (hpPercent < 30) fillEl.classList.add('low-hp');
                else fillEl.classList.remove('low-hp');
            }
            
            if (ghostEl) {
                ghostEl.style.width = `${hpPercent}%`;
            }
            return; // Exit early for Leader
        }
    }
    
    // Follower or other field unit
    const unitElement = document.querySelector(`.field-card[data-uid="${unitInstance.uid}"]`);
    if (unitElement) {
        healthElement = unitElement.querySelector('.health');
    }

    if (healthElement) {
        healthElement.textContent = newHealth;
    } else {
        // console.warn(`UI Update Error: Could not find health element for unit UID: ${unitInstance.uid} (Type: ${unitInstance.type})`);
    }
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

    if (!handElement) {
        const elName = isLocalPlayer ? 'player1HandEl' : 'player2HandEl';
        console.warn(`UI Update Error: ${elName} missing, cannot update hand display for player ${playerId}.`);
        return;
    }

    handElement.innerHTML = '';

    if (!Array.isArray(handCards)) {
        console.warn(`UI Update Warning: handCards for player ${playerId} is not an array.`);
        return;
    }

    handCards.forEach(cardInstance => {
        if (!cardInstance) {
            console.warn(`UI Update Warning: null or undefined card instance found in player ${playerId}'s hand.`);
            return;
        }

        // Opponent's hand is rendered with card backs unless dual-control or spectator mode is active
        if (!isLocalPlayer && !revealOpponentHand) {
            const cardBackDiv = document.createElement('div');
            cardBackDiv.classList.add('card-back');
            handElement.appendChild(cardBackDiv);
        } else {
            // Local player's hand is rendered with full card details
            const cardElement = createCardElement(cardInstance, 'hand');
            if (cardElement) {
                if (gameState.phase === 'Mulligan') {
                    const localPlayer = gameState.players?.find(p => p?.id === gameState.localPlayerId) || null;
                    const localCompleted = localPlayer?.hasCompletedMulligan === true;
                    if (!localCompleted) {
                        cardElement.classList.add('mulligan-selectable');
                        const uid = cardElement.dataset.uid;
                        if (uid && mulliganSelectedUids.has(uid)) {
                            cardElement.classList.add('selected-for-mulligan');
                        }
                    }
                }
                handElement.appendChild(cardElement);
            }
        }
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
    if (playerId === gameState.localPlayerId && isComplete) {
        if (confirmMulliganButtonEl) {
            confirmMulliganButtonEl.disabled = true;
            confirmMulliganButtonEl.textContent = '等待对方...'; // Change button text to indicate waiting
        } else {
            console.warn("UI Update Warning: Confirm Mulligan button element not found, cannot update status.");
        }
        // Optionally, add a class to indicate local completion if needed for styling
        // gameContainerEl?.classList.add('local-mulligan-complete');
    } else if (playerId !== gameState.localPlayerId && isComplete) {
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
        fieldElement.appendChild(cardElement);
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

    unitElement.replaceWith(replacement);
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
        unitElement.parentNode.removeChild(unitElement);
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

    // --- Start of Rush/Storm Validation Logic ---
    const opponentBoard = opponentPlayerState.field ?? [];
    const opponentLeader = opponentPlayerState.leaderCard;
    const attackerKeywords = Array.isArray(attackerInstance.keywords) ? attackerInstance.keywords : [];
    const attackerCannotAttackFollowers = attackerKeywords.some(keyword => equalsIgnoreCase(keyword, 'CannotAttackFollower'));
    const attackerCannotAttackLeader = attackerKeywords.some(keyword => equalsIgnoreCase(keyword, 'CannotAttackLeader'));
    const hasEffectiveWard = follower => {
        const keywords = Array.isArray(follower?.keywords) ? follower.keywords : [];
        const hasWard = keywords.some(keyword => equalsIgnoreCase(keyword, 'Ward'));
        const hasAmbush = keywords.some(keyword => equalsIgnoreCase(keyword, 'Ambush'));
        const hasPressure = keywords.some(keyword => equalsIgnoreCase(keyword, 'Pressure'));
        return hasWard && !hasAmbush && !hasPressure;
    };
    // 【BUG修复】在函数作用域内正确定义 hasWard 变量
    const hasWard = opponentBoard.some(follower => hasEffectiveWard(follower));
    const wardFollowers = opponentBoard.filter(f => hasEffectiveWard(f));

    let validTargets = [];

    if (wardFollowers.length > 0) {
        // If Ward is present, only Ward followers are valid targets
        validTargets = attackerCannotAttackFollowers ? [] : wardFollowers;
    } else {
        // If no Ward, all followers are valid targets
        validTargets = attackerCannotAttackFollowers ? [] : [...opponentBoard];
        
        // Check if the leader is a valid target
        const hasStorm = attackerKeywords.some(keyword => equalsIgnoreCase(keyword, 'Storm'));
        const isSummoningSick = attackerInstance.fieldTurn === 0 && !attackerInstance.isEvolved;

        if (!attackerCannotAttackLeader && (hasStorm || !isSummoningSick)) {
            if (opponentLeader) {
                validTargets.push(opponentLeader);
            }
        }
    }
    
    const finalTargets = validTargets.filter(Boolean);
    // --- End of Rush/Storm Validation Logic ---

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
                if (!isLeader && targetInstance.keywords?.includes('Ward')) {
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

function isCurrentChoiceRequest(pendingChoiceId, sourceAbilityRuntimeId) {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) {
        return false;
    }

    return String(currentChoiceRequest.PendingChoiceId) === String(pendingChoiceId)
        && String(currentChoiceRequest.SourceAbilityRuntimeId) === String(sourceAbilityRuntimeId);
}

function startPlayerChoice(requestData) {
    isAwaitingPlayerChoice = true;
    currentChoiceRequest = requestData;
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
    currentChoiceSelectedTargetIds = new Set();

    clearSelectionHighlights();
    gameContainerEl?.classList.remove('player-choice-active');

    removeChoiceOverlayUI();

    // BUG-FIX: Notify the backend that the choice was cancelled, only if requested.
    if (notifyBackendOfCancellation && choiceToCancel && choiceToCancel.PendingChoiceId && choiceToCancel.SourceAbilityRuntimeId) {
        cancelPlayerChoice(choiceToCancel.PendingChoiceId, choiceToCancel.SourceAbilityRuntimeId);
    } else if (notifyBackendOfCancellation) {
        console.warn("UI: endPlayerChoice was called to notify backend, but there was no active choice request to cancel.");
    }

    // BUG-FIX: Restore the state of the end turn button.
    if (endTurnButtonEl) {
        const isLocalPlayerTurn = gameState.localPlayerId === gameState.currentPlayerId;
        // Only enable if it's the local player's turn and the game is in a phase where ending the turn is possible.
        endTurnButtonEl.disabled = !isLocalPlayerTurn || (gameState.phase !== 'Main' && gameState.phase !== 'TurnStart');
    }
}
// --- End UI State Management Functions ---

function removeChoiceOverlayUI() {
    document.getElementById('choice-overlay-root')?.remove();
    // Backward compatibility cleanup (older prompt overlay).
    document.getElementById('choice-prompt-overlay')?.remove();
}

function getChoiceMinMax(choiceRequest) {
    const min = Number.isFinite(choiceRequest?.MinSelectionCount) ? choiceRequest.MinSelectionCount : 1;
    const max = Number.isFinite(choiceRequest?.MaxSelectionCount) ? choiceRequest.MaxSelectionCount : 1;
    return { min, max };
}

function isSingleMandatoryChoice(choiceRequest) {
    const { min, max } = getChoiceMinMax(choiceRequest);
    return min === 1 && max === 1;
}

function isChoiceSelectionValid(choiceRequest, selectedCount) {
    const { min, max } = getChoiceMinMax(choiceRequest);
    return selectedCount >= min && selectedCount <= max;
}

function updateChoiceOverlayUI() {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return;

    const { min, max } = getChoiceMinMax(currentChoiceRequest);
    const selectedCount = currentChoiceSelectedTargetIds.size;

    const countEl = document.getElementById('choice-selected-count');
    if (countEl) {
        countEl.textContent = `${selectedCount}/${min}-${max}`;
    }

    const confirmBtn = document.getElementById('choice-confirm-btn');
    if (confirmBtn) {
        confirmBtn.disabled = !isChoiceSelectionValid(currentChoiceRequest, selectedCount);
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
        el.classList.toggle('choice-card-selected', currentChoiceSelectedTargetIds.has(uid));
    });
}

async function confirmPlayerChoice() {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return;
    const { PendingChoiceId, SourceAbilityRuntimeId } = currentChoiceRequest;

    const chosenIds = Array.from(currentChoiceSelectedTargetIds);
    if (!isChoiceSelectionValid(currentChoiceRequest, chosenIds.length)) return;

    const ok = await submitPlayerChoice(PendingChoiceId, chosenIds, SourceAbilityRuntimeId, false);
    if (ok && isCurrentChoiceRequest(PendingChoiceId, SourceAbilityRuntimeId)) {
        endPlayerChoice(false);
    }
}

function requestCancelPlayerChoiceUI() {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return;
    if (currentChoiceRequest.IsCancellable !== true) {
        console.warn("UI: Player choice is not cancellable. Ignoring cancel request.");
        return;
    }
    endPlayerChoice(true);
}

async function handleChoiceTargetClick(targetUid) {
    if (!isAwaitingPlayerChoice || !currentChoiceRequest) return;

    const validTargetUids = new Set((currentChoiceRequest.SelectableTargetIds || []).map(x => String(x)));
    if (!validTargetUids.has(String(targetUid))) return;

    const { PendingChoiceId, SourceAbilityRuntimeId } = currentChoiceRequest;

    if (isSingleMandatoryChoice(currentChoiceRequest)) {
        const ok = await submitPlayerChoice(PendingChoiceId, [targetUid], SourceAbilityRuntimeId, false);
        if (ok && isCurrentChoiceRequest(PendingChoiceId, SourceAbilityRuntimeId)) {
            endPlayerChoice(false);
        }
        return;
    }

    const { max } = getChoiceMinMax(currentChoiceRequest);
    const key = String(targetUid);
    if (currentChoiceSelectedTargetIds.has(key)) {
        currentChoiceSelectedTargetIds.delete(key);
    } else {
        if (currentChoiceSelectedTargetIds.size >= max) return;
        currentChoiceSelectedTargetIds.add(key);
    }

    // Refresh highlights so selected targets remain visually consistent across zones.
    renderHighlights(gameState);
    updateChoiceOverlayUI();
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

    if (spectatorToggleViewButtonEl) {
        if (gameState.isSpectator) {
            spectatorToggleViewButtonEl.style.display = 'inline-block';
            spectatorToggleViewButtonEl.textContent = '切换视角';
            spectatorToggleViewButtonEl.title = `切换视角（当前底部玩家：${gameState.localPlayerId || 'unknown'}）`;
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

    // 方法1: 使用localPlayerId进行查找（主要方法）
    if (gameState.localPlayerId) {
        localPlayer = currentGameState.players.find(p => p.id === gameState.localPlayerId);
        opponentPlayerState = currentGameState.players.find(p => p.id !== gameState.localPlayerId);
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
            console.error(`[PVP] localPlayerId 未设置或不匹配，无法安全推断座位。`, {
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


    // Call helper functions to render specific parts of the UI using game state and cached elements

    // 1. Render Player Stats
    renderPlayerStats(player1, 'player1');
    renderPlayerStats(player2, 'player2');

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

}

// --- 具体渲染辅助函数 (占位符) ---
function updateEmblemDisplay(playerState, statsContainer) {
    if (!statsContainer) return;
    const emblemZone = statsContainer.querySelector('.emblem-zone');
    if (!emblemZone) return;

    const emblems = Array.isArray(playerState?.emblem)
        ? playerState.emblem
        : Array.isArray(playerState?.Emblem)
            ? playerState.Emblem
            : [];

    emblemZone.innerHTML = '';

    for (let i = 0; i < EMBLEM_SLOT_COUNT; i++) {
        const slot = document.createElement('div');
        slot.classList.add('emblem-slot');

        const emblem = emblems[i];
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
                void preloadArt(previewPath).catch(() => {
                    // ignore preload errors for emblem icons
                });
            }
            slot.classList.add('filled');
            if (emblemUid) {
                slot.dataset.uid = String(emblemUid);
            }
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
    const infoContainer = document.getElementById(`${prefix}-info`);
    const statsContainer = infoContainer ? infoContainer.querySelector('.player-stats') : null;
    updateEmblemDisplay(playerState, statsContainer);
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
    portalcraft: 'Portalcraft',
    中立: 'Neutral',
    精灵: 'Forestcraft',
    皇家护卫: 'Swordcraft',
    巫师: 'Runecraft',
    龙族: 'Dragoncraft',
    梦魇: 'Abysscraft',
    主教: 'Havencraft',
    复仇者: 'Portalcraft'
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
    ['bloodpool', '血池']
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

function renderPlayerStats(playerState, prefix) { // prefix = 'player1' or 'player2'
    if (!playerState) {
        return;
    }

    try {
        const infoContainer = document.getElementById(`${prefix}-info`);
        const statsContainer = infoContainer ? infoContainer.querySelector('.player-stats') : null;

        if (!statsContainer) {
            console.warn(`UI: Stats container for ${prefix} not found.`);
            return;
        }

        const currentHp = playerState.leaderCard?.currentHealth ?? 0;
        const hpValueEl = statsContainer.querySelector('.hp-value');
        if (hpValueEl) {
            hpValueEl.textContent = currentHp;
            hpValueEl.style.color = currentHp <= 5 ? '#ef4444' : '#fff';
        }

        const currentPp = playerState.pp ?? 0;
        const maxPp = playerState.maxPP ?? 0;
        const ppTextEl = statsContainer.querySelector('.pp-text');
        if (ppTextEl) ppTextEl.textContent = `${currentPp} / ${maxPp}`;
        updatePpDisplay(playerState.id, currentPp, maxPp);

        const maxEp = (playerState.maxEP && playerState.maxEP > 0) ? playerState.maxEP : 2;
        updateEpDisplay(playerState.id, playerState.ep, maxEp);

        const maxSep = (playerState.maxSEP && playerState.maxSEP > 0) ? playerState.maxSEP : 2;
        updateSepDisplay(playerState.id, playerState.sep, maxSep);

        updatePpBoostDisplay(playerState.id, playerState.ppBoostUsesAvailable ?? 0);

        const deckCountEl = statsContainer.querySelector(`#${prefix}-deck-count`);
        if (deckCountEl) deckCountEl.textContent = playerState.deckCount ?? 0;

        const graveCountEl = statsContainer.querySelector(`#${prefix}-grave-count`);
        if (graveCountEl) {
            graveCountEl.textContent = getPlayerCounterValue(
                playerState,
                'graveyard',
                playerState.graveyardCount
                ?? playerState.GraveyardCount
                ?? playerState.graveyard?.length
                ?? 0
            );
        }

        const bloodCountEl = statsContainer.querySelector(`#${prefix}-blood-count`);
        if (bloodCountEl) {
            bloodCountEl.textContent = getPlayerCounterValue(
                playerState,
                'bloodpool',
                playerState.bloodPool
                ?? playerState.BloodPool
                ?? 0
            );
        }

        const comboCountEl = statsContainer.querySelector(`#${prefix}-combo-count`);
        if (comboCountEl) {
            comboCountEl.textContent = getPlayerCounterValue(
                playerState,
                'combo',
                playerState.combo
                ?? playerState.Combo
                ?? 0
            );
        }

        const isLocal = prefix === 'player1';
        const displayName = resolvePlayerDisplayName(playerState, isLocal);
        const classLabel = resolvePlayerClassLabel(playerState);

        const classEl = statsContainer.querySelector('.class-name');
        if (classEl) {
            classEl.textContent = classLabel || 'Neutral';
        }

        const playerNameEl = statsContainer.querySelector('.player-name');
        if (playerNameEl) {
            playerNameEl.textContent = '';
            const nameTextEl = document.createElement('span');
            nameTextEl.className = 'player-name-text';
            nameTextEl.textContent = displayName;
            playerNameEl.appendChild(nameTextEl);

            const orderBadge = resolvePlayerOrderBadge(playerState);
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
            avatarEl.textContent = '';
            avatarEl.classList.toggle('has-image', Boolean(avatar.imageSrc));

            if (avatar.imageSrc) {
                const imageEl = document.createElement('img');
                imageEl.className = 'stats-avatar-img';
                imageEl.src = avatar.imageSrc;
                imageEl.alt = `${displayName} avatar`;
                imageEl.onerror = () => {
                    avatarEl.classList.remove('has-image');
                    avatarEl.textContent = avatar.fallbackText;
                };
                avatarEl.appendChild(imageEl);
            } else {
                avatarEl.textContent = avatar.fallbackText;
            }
        }

        updateEmblemDisplay(playerState, statsContainer);
    } catch (error) {
        console.error(`UI Error rendering stats for ${prefix}:`, error);
    }
}
function renderPlayerHand(handCards = [], handElement) {
    if (!handElement) {
        // console.error("UI Error: Hand element (player1HandEl or player2HandEl?) not provided for rendering hand.");
        return;
    }
    // Clear existing hand display
    handElement.innerHTML = '';
    // Iterate through hand card instances
    if (!Array.isArray(handCards)) {
        handCards = []; // Ensure it's an array to prevent errors
    }
    handCards.forEach(cardInstance => {
        if (!cardInstance) {
            // console.warn("UI Warning: null or undefined card instance found in handCards array.");
            return; // Skip this iteration
        }
        // Create the card's DOM element using the helper, specifying 'hand' context
        const cardElement = createCardElement(cardInstance, 'hand');
        if (cardElement) {
            const isLocalHand = handElement === player1HandEl;
            if (isLocalHand && gameState.phase === 'Mulligan') {
                const localPlayer = gameState.players?.find(p => p?.id === gameState.localPlayerId) || null;
                const localCompleted = localPlayer?.hasCompletedMulligan === true;
                if (!localCompleted) {
                    cardElement.classList.add('mulligan-selectable');
                    const uid = cardElement.dataset.uid;
                    if (uid && mulliganSelectedUids.has(uid)) {
                        cardElement.classList.add('selected-for-mulligan');
                    }
                }
            }
            // Append the created card element to the hand container
            handElement.appendChild(cardElement);
        } else {
            // Log if element creation failed for some reason
            // console.warn(`UI Warning: Failed to create element for card instance in hand:`, cardInstance);
        }
    });
    // Optional: Add visual feedback if hand reaches max size
    // const maxHandSize = 9; // Or get from playerState if available
    // if (handCards.length >= maxHandSize) { handElement.classList.add('full-hand'); }
    // else { handElement.classList.remove('full-hand'); }
}

// --- 渲染对方手牌（只显示牌背） ---
function renderOpponentHand(handCards = [], handElement) {
    if (!handElement) {
        // console.error("UI Error: Opponent hand element (player2HandEl) not provided.");
        return;
    }
    // Clear existing hand display
    handElement.innerHTML = '';
    // Ensure handCards is an array
    if (!Array.isArray(handCards)) {
        // console.error("UI Error: handCards is not an array in renderOpponentHand.");
        handCards = [];
    }
    // Create and append a card back for each card in hand
    // Ensure there's a '.card-back' style defined in style.css
    handCards.forEach(() => {
        const cardBackDiv = document.createElement('div');
        cardBackDiv.classList.add('card-back'); // Use existing CSS class if available, or create one
        handElement.appendChild(cardBackDiv);
    });
}

function renderField(fieldCards = [], fieldElement, isOpponent = false) {
    if (!fieldElement) {
        return;
    }
    // Clear existing field display
    fieldElement.innerHTML = '';
    // Iterate through field card instances
    if (!Array.isArray(fieldCards)) {
        fieldCards = []; // Prevent errors by ensuring it's an array
    }
    fieldCards.forEach(cardInstance => {
        if (!cardInstance) {
            // console.warn("UI Warning: null or undefined card instance found in fieldCards array.");
            return; // Skip null/undefined entries
        }
        // Create the card's DOM element using the helper, specifying 'field' context
        const cardElement = createCardElement(cardInstance, 'field');
        if (cardElement) {
            // if (isOpponent) {
            //     // Optionally add a specific class for styling opponent cards differently
            //     cardElement.classList.add('opponent-card');
            // }
            // Append the created card element to the field container
            fieldElement.appendChild(cardElement);
        } else {
            // Log if element creation failed
            // console.warn(`UI Warning: Failed to create element for card instance on field (Opponent: ${isOpponent}):`, cardInstance);
        }
    });
    // Optional: Add visual feedback if the field reaches max size
    // const maxFieldSize = 5; // Or get from playerState/gameState constant
    // if (fieldCards.length >= maxFieldSize) { fieldElement.classList.add('full-field'); }
    // else { fieldElement.classList.remove('full-field'); }
}
// --- 创建卡牌 DOM 元素的辅助函数 ---
  function isEnhanceModeValue(value) {
      if (value === 1) return true;
      if (typeof value === 'string') {
          return value.trim().toLowerCase() === 'enhance';
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

      if (context === 'hand') {
          const cardUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
          const playabilityInfo = cardUid ? gameState.playabilityCache[cardUid] : null;
          const selectedCost = playabilityInfo?.selectedPlayCost;
          if (
              playabilityInfo
              && isEnhanceModeValue(playabilityInfo.selectedPlayMode)
              && Number.isFinite(selectedCost)
          ) {
              displayCost = selectedCost;
              enhanceActive = Number.isFinite(baseCostRaw) ? selectedCost !== baseCostRaw : true;
          }
      }

      return { displayCost, enhanceActive };
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
          useUid: null
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
      const isLocalTurn = gameState.currentPlayerId === localPlayerId;
      if (!playerState || !isLocalTurn || gameState.phase !== 'Main') {
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

      const playabilityInfo = gameState.playabilityCache?.[useUid];
      const usePlayable = playabilityInfo ? playabilityInfo.isPlayable !== false : true;
      const useFailureReason = playabilityInfo?.failureReason ?? '';

      return {
          showUse: true,
          usePlayable,
          useFailureReason,
          useUid
      };
  }

  function hasFusionAbility(cardInstance) {
      if (!cardInstance) return false;
      const fusionAbilities = cardInstance.fusionAbilities ?? cardInstance.FusionAbilities ?? [];
      if (Array.isArray(fusionAbilities) && fusionAbilities.length > 0) {
          return true;
      }
      const directFlag = cardInstance.hasFusionAbility ?? cardInstance.HasFusionAbility;
      if (directFlag === true) return true;

      const description = cardInstance.description ?? cardInstance.Description ?? '';
      const effectDescriptions = cardInstance.effectDescriptions ?? cardInstance.EffectDescriptions ?? [];
      const effectText = Array.isArray(effectDescriptions) ? effectDescriptions.join(' ') : '';
      const combined = `${description} ${effectText}`;
      return /fusion|融合/i.test(combined);
  }

  function getCardActionState(cardInstance, context) {
      const empty = {
          showSuperEvolve: false,
          showEvolve: false,
          showAct: false,
          showFusion: false,
          showUse: false,
          usedThisTurn: false,
          usePlayable: true,
          useFailureReason: ''
      };
      if (!cardInstance) return empty;

      const resolvedContext = resolveCardContext(cardInstance, context);
      const isFieldContext = resolvedContext === 'field';
      const isHandContext = resolvedContext === 'hand';
      const useActionState = getUseActionState(cardInstance, resolvedContext);

      const currentPlayerId = getCurrentPlayerId();
      const playerState = getPlayerState(currentPlayerId);
      const cardOwnerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId;

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
      const hasActAbility = (cardInstance.hasActAbility === true) || (cardInstance.HasActAbility === true);
      const actAbilityList = Array.isArray(cardInstance.actAbilities)
          ? cardInstance.actAbilities
          : (Array.isArray(cardInstance.ActAbilities) ? cardInstance.ActAbilities : []);
      const fusionAbilityList = Array.isArray(cardInstance.fusionAbilities)
          ? cardInstance.fusionAbilities
          : (Array.isArray(cardInstance.FusionAbilities) ? cardInstance.FusionAbilities : []);
      const lastActTurn = cardInstance.actLastUsedTurnNumber ?? cardInstance.ActLastUsedTurnNumber ?? null;
      const currentTurnNumber = playerState?.turnNumber ?? playerState?.TurnNumber ?? 0;
      const usedThisTurn = lastActTurn !== null && lastActTurn === currentTurnNumber;
      const cardUid = getCardUid(cardInstance);
      const actInfo = cardUid ? gameState.actPlayabilityCache?.[cardUid] : null;
      const actPlayable = actInfo ? actInfo.isPlayable !== false : true;
      const actFailureReason = actInfo?.failureReason ?? '';
      const actCost = Number.isFinite(actInfo?.actCost) ? actInfo.actCost : null;
      const actAbilities = (Array.isArray(actInfo?.abilities) && actInfo.abilities.length > 0)
          ? actInfo.abilities
          : actAbilityList;

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
          showAct: isFieldContext && (hasActAbility || actAbilities.length > 0),
          showFusion: isHandContext && (fusionAbilityList.length > 0 || hasFusionAbility(cardInstance)),
          showUse: useActionState.showUse,
          usedThisTurn,
          actPlayable,
          actFailureReason,
          usePlayable: useActionState.usePlayable,
          useFailureReason: useActionState.useFailureReason,
          actCost,
          actAbilities,
          fusionAbilities: fusionAbilityList
      };
  }

  const CARD_ACTION_BUTTON_SELECTOR = '.super-evolve-button, .evolve-button, .fusion-button, .act-button, .use-button';
  const CARD_ACTION_BUTTON_TOP_START = 28;
  const CARD_ACTION_BUTTON_VERTICAL_GAP = 28;

  function applyCardActionButtonStack(cardElement) {
      if (!cardElement) return;
      const buttons = cardElement.querySelectorAll(CARD_ACTION_BUTTON_SELECTOR);
      buttons.forEach((button, index) => {
          const top = CARD_ACTION_BUTTON_TOP_START + (index * CARD_ACTION_BUTTON_VERTICAL_GAP);
          button.style.setProperty('--action-button-top', `${top}px`);
      });
  }

  const keywordBadgeConfig = new Map([
      ['ward', badgeWardUrl],
      ['spellward', badgeWardUrl],
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
          badges.push(
              `<div class="keyword-badge" title="${label}" data-keyword="${normalized}">` +
              `<img class="badge-icon" src="${badgeSrc}" alt="${label}">` +
              `</div>`
          );
      });

      if (badges.length === 0) return '';
      return `<div class="keyword-badges">${badges.join('')}</div>`;
  }

  function createCardElement(cardInstance, context = 'hand') { // context: 'hand', 'field'
      if (!cardInstance) {
          // console.error("UI Error: createCardElement called with null cardInstance.");
          return null;
      }

      const cardDiv = document.createElement('div');
    cardDiv.classList.add('card'); // Base card class
    // 添加卡牌类型作为 class (方便样式化)
    // 【修复】使用正确的属性名：Type（大写）而不是type（小写）
    const cardType = cardInstance.Type ?? cardInstance.type;
    if (cardType) {
        // 将类型转为小写，例如 'Follower' -> 'follower'
        cardDiv.classList.add(cardType.toLowerCase().split('_')[0]); // Handle 'Follower_Evolved' -> 'follower'
    }
    // 【修复】使用正确的UID属性：优先使用uid，备用InstanceId
    cardDiv.dataset.uid = cardInstance.uid ?? cardInstance.InstanceId; // Store UID for event handling & state lookup

    // 添加上下文特定的 class
    if (context === 'hand') {
        cardDiv.classList.add('hand-card');
    } else if (context === 'field') {
        cardDiv.classList.add('field-card');
        // 如果是已进化的随从，添加 'evolved' class
        if (cardInstance.isEvolved === true) {
            cardDiv.classList.add('evolved');
        }
    }

    applyAttackReadinessClasses(cardDiv, cardInstance, context);

    // --- ADDED: Ward Indicator ---
    // Check if keywords array exists and includes 'Ward' (Only for field context)
    if (context === 'field' && Array.isArray(cardInstance.keywords) && cardInstance.keywords.includes('Ward')) {
        cardDiv.classList.add('has-ward');
    }
    // --- END Ward Indicator ---

      const costInfo = resolveCardCostDisplay(cardInstance, context);
      if (costInfo.enhanceActive) {
          cardDiv.classList.add('enhance-active');
      }

      // 基本的 Inner HTML 结构 - 需要根据 style.css 进行调整
      // 使用 ?? 提供默认值，防止属性缺失导致显示错误
      // 【修复】使用正确的属性名：Name（大写）而不是name（小写）
    
    // Determine Countdown / Earthsigil visibility (Amulets only)
    const countdownValue = cardInstance.countdown
        ?? cardInstance.CurrentCountdown
        ?? cardInstance.currentCountdown
        ?? cardInstance.Countdown;
    const isFollowerType = equalsIgnoreCase(cardType, 'Follower') || equalsIgnoreCase(cardType, 'Follower_Evolved');
    const isAmuletType = equalsIgnoreCase(cardType, 'Amulet');
    const hasCountdownKeyword = Array.isArray(cardInstance.keywords)
        && cardInstance.keywords.some(keyword => equalsIgnoreCase(keyword, 'Countdown'));
    const hasEarthsigilKeyword = Array.isArray(cardInstance.keywords)
        && cardInstance.keywords.some(keyword => equalsIgnoreCase(keyword, 'EarthSigil'));
    const showCountdown = isAmuletType && hasCountdownKeyword
        && (countdownValue !== null && countdownValue !== undefined);
    const earthsigilValue = cardInstance.earthsigil
        ?? cardInstance.Earthsigil
        ?? cardInstance.EarthSigil
        ?? cardInstance.counters?.earthsigil
        ?? cardInstance.Counters?.Earthsigil
        ?? 0;
    const showEarthsigil = isAmuletType && hasEarthsigilKeyword;

      const keywordBadgesHtml = buildKeywordBadgesHtml(cardInstance, context);

      cardDiv.innerHTML = `
          <div class="card-art"></div> <!-- 卡面美术图占位 -->
          <div class="cost"><span>${costInfo.displayCost ?? '?'}</span></div>
          <div class="name">${cardInstance.Name ?? cardInstance.name ?? 'Unknown Card'}</div>
          ${
        // 仅为随从显示攻击力和生命值区域
        isFollowerType ?
            // Use CurrentAttack and CurrentHealth from the DTO received from server
            `<div class="stats">
                <span class="attack">${cardInstance.CurrentAttack ?? cardInstance.currentAttack ?? '?'}</span>
                <span class="health">${cardInstance.CurrentHealth ?? cardInstance.currentHealth ?? '?'}</span>
            </div>`
            : (showCountdown || showEarthsigil) ?
            `<div class="stats stats-amulet">
                ${showEarthsigil ? `<span class="earthsigil">${earthsigilValue}</span>` : ''}
                ${showCountdown ? `<span class="countdown">${countdownValue}</span>` : ''}
            </div>`
            : '' // 法术和护符不显示 stats 区域
        }
        ${keywordBadgesHtml}
        <!-- Description removed for minimalist design -->
    `;

    const previewPath = cardInstance.artPreview ?? cardInstance.ArtPreview ?? '';
    const artEl = cardDiv.querySelector('.card-art');
    if (artEl) {
        applyArtBackground(artEl, previewPath);
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
            const abilityRuntimeId = fusionAbility?.runtimeId ?? fusionAbility?.RuntimeId ?? '';
            if (abilityRuntimeId) {
                fusionBtn.dataset.fusionAbilityId = abilityRuntimeId;
            }
            cardDiv.appendChild(fusionBtn);
        });
    }

    if (actionState.showAct) {
        const actAbilityEntries = Array.isArray(actionState.actAbilities) && actionState.actAbilities.length > 0
            ? actionState.actAbilities
            : [null];
        actAbilityEntries.forEach((actAbility, index) => {
            const actBtn = document.createElement('button');
            actBtn.className = 'act-button';
            const isPlayable = actAbility ? actAbility.isPlayable !== false : actionState.actPlayable !== false;
            const failureReason = actAbility?.failureReason ?? actionState.actFailureReason;
            const actLabel = getBattleActionLabel('act', index, actAbilityEntries.length);
            actBtn.textContent = actLabel;
            actBtn.disabled = actionState.usedThisTurn || isPlayable === false;
            if (!actionState.usedThisTurn && isPlayable === false && failureReason) {
                actBtn.title = failureReason;
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
        const useTitle = (useDisabled && actionState.useFailureReason)
            ? ` title="${escapeAttributeValue(actionState.useFailureReason)}"`
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
            const abilityRuntimeId = fusionAbility?.runtimeId ?? fusionAbility?.RuntimeId ?? '';
            const abilityAttr = abilityRuntimeId ? ` data-fusion-ability-id="${abilityRuntimeId}"` : '';
            buttonsHtml += `<button class="fusion-button" data-fusion-uid="${cardUid}"${abilityAttr}>${label}</button>`;
        });
    }

    if (actionState.showAct) {
        const actAbilityEntries = Array.isArray(actionState.actAbilities) && actionState.actAbilities.length > 0
            ? actionState.actAbilities
            : [null];
        actAbilityEntries.forEach((actAbility, index) => {
            const isPlayable = actAbility ? actAbility.isPlayable !== false : actionState.actPlayable !== false;
            const failureReason = actAbility?.failureReason ?? actionState.actFailureReason;
            const actLabel = getBattleActionLabel('act', index, actAbilityEntries.length);
            const actDisabled = actionState.usedThisTurn || isPlayable === false;
            const actTitle = (!actionState.usedThisTurn && isPlayable === false && failureReason)
                ? ` title="${escapeAttributeValue(failureReason)}"`
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
        if (!actionState.showAct || actionState.usedThisTurn) return null;

        const actAbilityEntries = Array.isArray(actionState.actAbilities) && actionState.actAbilities.length > 0
            ? actionState.actAbilities
            : [null];
        const hasPlayableAct = actAbilityEntries.some(actAbility => {
            const isPlayable = actAbility ? actAbility.isPlayable !== false : actionState.actPlayable !== false;
            return isPlayable;
        });

        return hasPlayableAct ? 'act-ready' : null;
    }

    const canAttack = (cardInstance.canAttack ?? cardInstance.CanAttack) === true;
    if (!canAttack) return null;

    const keywords = cardInstance.keywords ?? cardInstance.Keywords ?? [];
    const hasStorm = Array.isArray(keywords) && keywords.some(keyword => equalsIgnoreCase(keyword, 'Storm'));
    const hasRush = Array.isArray(keywords) && keywords.some(keyword => equalsIgnoreCase(keyword, 'Rush'));
    const isEvolved = (cardInstance.isEvolved ?? cardInstance.IsEvolved) === true;
    const fieldTurn = cardInstance.fieldTurn ?? cardInstance.FieldTurn ?? 0;

    if (hasStorm || fieldTurn > 0) {
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
    ['combo', '连击数'],
    ['graveyard', '墓场数'],
    ['bloodpool', '血池']
]);
const playerSystemCounterKeys = new Set(['combo', 'graveyard', 'bloodpool']);

function getCounterLabel(labelMap, key) {
    const rawKey = String(key ?? '').trim();
    if (!rawKey) return '';
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
        return { counterMap: null, labelMap: null, isPlayerCounterSource: false };
    }

    const isLeader = equalsIgnoreCase(cardInstance.type ?? cardInstance.Type, 'Leader');
    if (isLeader) {
        const ownerId = cardInstance.ownerId ?? cardInstance.OwnerPlayerId ?? null;
        const ownerPlayer = ownerId ? getPlayerState(ownerId) : null;
        if (ownerPlayer) {
            return {
                counterMap: ownerPlayer.counters ?? ownerPlayer.Counters ?? {},
                labelMap: ownerPlayer.counterDisplayNames ?? ownerPlayer.CounterDisplayNames ?? {},
                isPlayerCounterSource: true
            };
        }
    }

    return {
        counterMap: cardInstance.counters ?? cardInstance.Counters ?? cardInstance.counterMap ?? cardInstance.CounterMap,
        labelMap: cardInstance.counterDisplayNames ?? cardInstance.CounterDisplayNames ?? {},
        isPlayerCounterSource: false
    };
}

function getCounterSummaryHtml(cardInstance) {
    if (!cardInstance) return '';
    const { counterMap, labelMap, isPlayerCounterSource } = resolveCounterSummarySource(cardInstance);
    if (!counterMap || typeof counterMap !== 'object') {
        return '';
    }

    const entries = Object.entries(counterMap)
        .map(([key, value]) => {
            const normalizedKey = String(key ?? '').trim();
            if (!normalizedKey) return null;
            if (isPlayerCounterSource && playerSystemCounterKeys.has(normalizedKey.toLowerCase())) return null;
            const label = getCounterLabel(labelMap, normalizedKey);
            if (!label) return null;
            const numericValue = Number(value);
            if (!Number.isFinite(numericValue)) return null;
            const displayValue = Math.max(0, Math.trunc(numericValue));
            if (displayValue == 0) return null;
            return {
                key: normalizedKey,
                label,
                value: displayValue
            };
        })
        .filter(Boolean);

    if (!entries.length) return '';

    entries.sort((a, b) => a.key.localeCompare(b.key, 'zh-CN', { sensitivity: 'base' }));

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

function getBuffSummaryHtml(cardInstance) {
    if (!cardInstance) return '';
    const summaries = cardInstance.buffSummaries ?? cardInstance.BuffSummaries ?? [];
    const statSummary = cardInstance.buffStatSummary ?? cardInstance.BuffStatSummary ?? null;
    const rawKeywords = cardInstance.keywords ?? cardInstance.Keywords ?? [];

    const segments = [];
    if (statSummary) {
        const attackDelta = statSummary.attackDelta ?? statSummary.AttackDelta ?? 0;
        const healthDelta = statSummary.healthDelta ?? statSummary.HealthDelta ?? 0;
        const costDelta = statSummary.costDelta ?? statSummary.CostDelta ?? 0;

        if (attackDelta !== 0) segments.push(`ATK ${attackDelta > 0 ? `+${attackDelta}` : attackDelta}`);
        if (healthDelta !== 0) segments.push(`HP ${healthDelta > 0 ? `+${healthDelta}` : healthDelta}`);
        if (costDelta !== 0) segments.push(`COST ${costDelta > 0 ? `+${costDelta}` : costDelta}`);
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

    const lines = Array.isArray(summaries)
        ? summaries
            .map(summary => {
                const sourceCardName = summary?.sourceCardName ?? summary?.SourceCardName;
                const sourceAbilityName = summary?.sourceAbilityName ?? summary?.SourceAbilityName;
                if (!sourceCardName || !sourceAbilityName) {
                    return '';
                }
                return `<div class="buff-summary-line">${sourceCardName} - ${sourceAbilityName}</div>`;
            })
            .filter(Boolean)
            .join('')
        : '';

    if (!summaryLine && !keywordLine && !lines) {
        return '';
    }

    return `<div class="buff-summary-list">${summaryLine}${keywordLine}${lines}</div>`;
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
        void preloadArt(previewPath).catch(() => {
            // Ignore preload errors in preview panel.
        });
    }
    if (detailPath && detailPath !== previewPath) {
        void preloadArt(detailPath).catch(() => {
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
    
    // Generate Action Buttons
    const actionsHtml = getActionButtonsHtml(cardInstance);
    const buffSummaryHtml = getBuffSummaryHtml(cardInstance);
    const counterSummaryHtml = getCounterSummaryHtml(cardInstance);

    return buildPreviewSlateHtml({
        rarityClass,
        rarityLabel,
        classLabel,
        typeLabel,
        name: cardInstance.name ?? cardInstance.Name ?? 'UNTITLED',
        costValue,
        enhanceActive: previewCostInfo.enhanceActive,
        isFollower,
        attackValue,
        healthValue,
        countdownValue,
        showCountdown,
        earthsigilValue,
        showEarthsigil,
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
    battleCardPreviewEl.innerHTML = buildBattlePreviewHtml(cardInstance);
    const fallbackPreviewPath = cardInstance.artPreview
        ?? cardInstance.ArtPreview
        ?? cardInstance.artDetail
        ?? cardInstance.ArtDetail
        ?? '';
    const fallbackPreviewUrl = resolveArtUrl(fallbackPreviewPath);
    void hydrateDescriptionReferences(battleCardPreviewEl, { fallbackPreviewUrl });
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

            if (winnerId && localPlayerId) {
                if (winnerId === localPlayerId) {
                    displayResult = "YOU WIN";
                    displayColor = 'var(--color-success)';
                } else {
                    displayResult = "YOU LOSE";
                    displayColor = 'var(--color-accent)';
                }
            }

            turnInfoEl.textContent = displayResult;
            turnInfoEl.style.color = displayColor;
            return;
        }
        // Display "Starting..." or similar if turnNumber is 0 (initial hydration)
        const displayTurn = turnNumber === 0 ? "GAME START" : `TURN ${turnNumber}`;
        
        // Simplified display: Just the Turn Number.
        // The active player is indicated by the turn badge or other UI cues (like glowing avatar).
        turnInfoEl.textContent = displayTurn;
        
        // Optional: Add a class to indicate whose turn it is for styling the text
        const isLocalPlayerTurn = (currentPlayerId === gameState.localPlayerId);
        if (isLocalPlayerTurn) {
            turnInfoEl.style.color = 'var(--color-primary)';
        } else {
            turnInfoEl.style.color = 'var(--color-secondary)';
        }
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
let isLogExpanded = false; // Default to collapsed
const LOG_RENDER_LIMIT = 200; // Keep in sync with GAME_LOG_LIMIT in gameManager.js
const LOG_AUTO_SCROLL_THRESHOLD_PX = 12;

function isLogNearBottom(container) {
    if (!container) return true;
    const remaining = container.scrollHeight - container.scrollTop - container.clientHeight;
    return remaining < LOG_AUTO_SCROLL_THRESHOLD_PX;
}

function parseLogTokens(text) {
    const segments = [];
    const pattern = /\[\[(.+?)\]\]/g;
    let lastIndex = 0;
    let match = pattern.exec(text);

    while (match) {
        if (match.index > lastIndex) {
            segments.push({ type: 'text', value: text.slice(lastIndex, match.index) });
        }
        segments.push({ type: 'token', value: match[1] });
        lastIndex = pattern.lastIndex;
        match = pattern.exec(text);
    }

    if (lastIndex < text.length) {
        segments.push({ type: 'text', value: text.slice(lastIndex) });
    }

    return segments;
}

function getPlayerLabel(playerId) {
    if (!playerId || playerId === 'unknown') return '玩家';
    if (gameState.localPlayerId) {
        return playerId === gameState.localPlayerId ? '我方' : '敌方';
    }

    const players = Array.isArray(gameState.players) ? gameState.players : [];
    const index = players.findIndex(p => p && p.id === playerId);
    if (index === 0) return '玩家1';
    if (index === 1) return '玩家2';
    return '玩家';
}

function getSideClass(ownerId) {
    if (!gameState.localPlayerId || !ownerId || ownerId === 'unknown') return '';
    return ownerId === gameState.localPlayerId ? 'log-token-friendly' : 'log-token-enemy';
}

function findCardInstanceForLog(uid) {
    if (!uid) return null;
    const primary = findCardInstanceByUid(uid);
    if (primary) return primary;

    const players = Array.isArray(gameState.players) ? gameState.players : [];
    const zones = ['graveyard', 'deck', 'extra', 'emblem'];
    for (const player of players) {
        if (!player) continue;
        for (const zone of zones) {
            const list = player[zone];
            if (!Array.isArray(list)) continue;
            const match = list.find(card => card && (card.uid === uid || card.InstanceId === uid || card.instanceId === uid));
            if (match) return match;
        }
    }
    return null;
}

function normalizeLogName(value) {
    return String(value || '')
        .replace(/\[\[|\]\]/g, '')
        .replace(/\s+/g, ' ')
        .trim();
}

function formatCardTokenText(value) {
    return normalizeLogName(value);
}

function isWhitespaceChar(value) {
    return /\s/.test(value);
}

function isPunctuationChar(value) {
    return /[，。！？、：；…）)]/.test(value);
}

function shouldInsertLeadingSpace(prevChar) {
    if (!prevChar) return false;
    if (isWhitespaceChar(prevChar)) return false;
    return true;
}

function shouldInsertTrailingSpace(nextChar, nextIsToken) {
    if (nextIsToken) return true;
    if (!nextChar) return false;
    if (isWhitespaceChar(nextChar)) return false;
    if (isPunctuationChar(nextChar)) return false;
    return true;
}

function parseCardToken(rawToken) {
    const text = String(rawToken || '').trim();
    if (!text) return { id: '', name: '', ownerId: '' };

    const parts = text.split('|').map(part => part.trim()).filter(Boolean);
    const id = parts.shift() || '';
    let name = '';
    let ownerId = '';

    for (const part of parts) {
        const idx = part.indexOf(':');
        if (idx > 0) {
            const key = part.slice(0, idx).trim().toLowerCase();
            const value = part.slice(idx + 1).trim();
            if (key === 'name') {
                name = value;
                continue;
            }
            if (key === 'owner') {
                ownerId = value;
                continue;
            }
        }
        if (!name) {
            name = part;
        }
    }

    return { id, name, ownerId };
}

function shouldRevealLogCard(fallbackName) {
    return Boolean(fallbackName);
}

function resolveCardToken(rawToken) {
    const { id, name, ownerId } = parseCardToken(rawToken);
    const card = findCardInstanceForLog(id);
    if (!card) {
        const fallbackName = normalizeLogName(name);
        if (shouldRevealLogCard(fallbackName)) {
            const sideClass = getSideClass(ownerId);
            return {
                text: formatCardTokenText(fallbackName),
                className: `log-token log-token-card ${sideClass}`.trim()
            };
        }
        return { text: formatCardTokenText('未知卡牌'), className: 'log-token log-token-card is-unknown' };
    }

    const sideClass = getSideClass(card.ownerId);
    if (card.type === 'Leader') {
        const side = getPlayerLabel(card.ownerId);
        return {
            text: formatCardTokenText(`${side}主战者`),
            className: `log-token log-token-card log-token-leader ${sideClass}`.trim()
        };
    }

    const resolvedName = normalizeLogName(card.name || card.Name || '未知卡牌');
    return {
        text: formatCardTokenText(resolvedName),
        className: `log-token log-token-card ${sideClass}`.trim()
    };
}

function resolveLogToken(rawToken) {
    const token = String(rawToken || '').trim();
    if (token.startsWith('card:')) {
        return resolveCardToken(token.slice(5));
    }
    if (token.startsWith('cardname:')) {
        const name = normalizeLogName(token.slice(9)) || '未知卡牌';
        return { text: formatCardTokenText(name), className: 'log-token log-token-card' };
    }
    if (token.startsWith('player:')) {
        const playerId = token.slice(7).trim();
        const label = getPlayerLabel(playerId);
        return { text: label, className: `log-token log-token-player ${getSideClass(playerId)}`.trim() };
    }
    if (token.startsWith('num:')) {
        const value = token.slice(4).trim();
        return { text: value, className: 'log-token log-token-number' };
    }
    if (token.startsWith('text:')) {
        const value = token.slice(5).trim();
        return { text: value, className: 'log-token' };
    }

    return { text: token, className: 'log-token' };
}

export function renderGameLog(logs) {
    let logContainer = document.getElementById('game-log-container');
    let logContent = document.getElementById('game-log-content');
    
    // Create container structure if it doesn't exist
    if (!logContainer) {
        logContainer = document.createElement('div');
        logContainer.id = 'game-log-container';
        logContainer.className = isLogExpanded ? 'expanded' : 'collapsed'; // Set initial state

        // Create Header Container
        const header = document.createElement('div');
        header.className = 'log-header';

        // Create Toggle Button
        const toggleBtn = document.createElement('button');
        toggleBtn.id = 'game-log-toggle';
        toggleBtn.className = 'log-toggle-btn';
        toggleBtn.title = 'Toggle Log';
        toggleBtn.innerHTML = `
            <svg viewBox="0 0 24 24" class="icon-svg">
                <path d="M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/>
            </svg>
        `;
        toggleBtn.onclick = () => {
            isLogExpanded = !isLogExpanded;
            logContainer.className = isLogExpanded ? 'expanded' : 'collapsed';
            // Auto-scroll to bottom when expanding
            if (isLogExpanded && logContent) {
                logContent.scrollTop = logContent.scrollHeight;
            }
        };

        // Create left title group
        const headerMarker = document.createElement('span');
        headerMarker.className = 'log-header-marker';
        headerMarker.setAttribute('aria-hidden', 'true');

        const titleGroup = document.createElement('div');
        titleGroup.className = 'log-title-group';

        const titleMain = document.createElement('span');
        titleMain.className = 'log-title-main';
        titleMain.textContent = '战斗记录';

        const titleSub = document.createElement('span');
        titleSub.className = 'log-title-sub';
        titleSub.textContent = 'BATTLE TIMELINE';

        titleGroup.appendChild(titleMain);
        titleGroup.appendChild(titleSub);

        header.appendChild(headerMarker);
        header.appendChild(titleGroup);
        header.appendChild(toggleBtn);

        // Create Content Wrapper
        logContent = document.createElement('div');
        logContent.id = 'game-log-content';

        logContainer.appendChild(header);
        logContainer.appendChild(logContent);

        // Append to game-container
        const gameContainer = document.getElementById('game-container');
        if (gameContainer) {
            gameContainer.appendChild(logContainer);
        } else {
            document.body.appendChild(logContainer); // Fallback
        }
    } else {
        // Ensure content element exists if container was found but might be malformed (legacy support)
        logContent = document.getElementById('game-log-content');
        if (!logContent) {
            logContainer.innerHTML = ''; // Reset if structure is wrong
            // Re-call to recreate structure
            renderGameLog(logs);
            return;
        }
        // Sync class with state
        logContainer.className = isLogExpanded ? 'expanded' : 'collapsed';
    }

    const previousScrollTop = logContent?.scrollTop ?? 0;
    const wasNearBottom = isLogExpanded ? isLogNearBottom(logContent) : true;

    // Update Logs Content
    if (!logs || !Array.isArray(logs)) {
        return;
    }

    // Clear existing logs in content wrapper
    logContent.innerHTML = '';

    // Render recent logs with a safe cap
    const recentLogs = logs.slice(-LOG_RENDER_LIMIT);
    
    recentLogs.forEach(log => {
        const logEntry = document.createElement('div');
        logEntry.className = 'log-entry';
        const rawText = String(log ?? '');
        const segments = parseLogTokens(rawText);
        let lastChar = '';
        segments.forEach((segment, index) => {
            if (segment.type === 'text') {
                if (segment.value) {
                    logEntry.appendChild(document.createTextNode(segment.value));
                    lastChar = segment.value.slice(-1);
                }
                return;
            }

            const resolved = resolveLogToken(segment.value);
            const isSpacedToken = resolved.className.includes('log-token-card')
                || resolved.className.includes('log-token-player')
                || resolved.className.includes('log-token-number');
            const nextSegment = segments[index + 1] || null;
            const nextIsToken = nextSegment?.type === 'token';
            const nextChar = nextSegment?.type === 'text' && nextSegment.value
                ? nextSegment.value[0]
                : '';
            if (isSpacedToken && shouldInsertLeadingSpace(lastChar)) {
                logEntry.appendChild(document.createTextNode(' '));
                lastChar = ' ';
            }
            const span = document.createElement('span');
            span.className = resolved.className;
            span.textContent = resolved.text;
            logEntry.appendChild(span);
            if (resolved.text) {
                lastChar = resolved.text.slice(-1);
            }
            if (isSpacedToken && shouldInsertTrailingSpace(nextChar, nextIsToken)) {
                logEntry.appendChild(document.createTextNode(' '));
                lastChar = ' ';
            }
        });
        logContent.appendChild(logEntry);
    });

    // Auto-scroll only if user is already near bottom
    if (isLogExpanded) {
        if (wasNearBottom) {
            logContent.scrollTop = logContent.scrollHeight;
        } else {
            const maxScrollTop = Math.max(0, logContent.scrollHeight - logContent.clientHeight);
            logContent.scrollTop = Math.min(previousScrollTop, maxScrollTop);
        }
    }
}


// --- 处理高亮 (可交互状态) 的辅助函数 ---
// --- 处理高亮 (可交互状态) 的辅助函数 --- // Keeping the comment from original line 411
function renderHighlights(currentGameState) {
    clearSelectionHighlights();
    syncHandUseButtons();

    if (isAwaitingPlayerChoice) {
        renderAbilityTargetHighlights(currentGameState);
    } else {
        renderPlayableCardHighlights(currentGameState);
        renderAttackHighlights(currentGameState);
    }
}

// --- Highlight Helper Functions ---

// Helper 1: Clear All Highlight Classes
function clearSelectionHighlights() {
    const highlightClasses = ['playable', 'selected-attacker', 'targetable', 'targetable-restricted', 'targeting-active', 'targeting-spell', 'targetable-spell', 'choice-selected'];
    gameContainerEl?.classList.remove('targeting-active'); // Remove global targeting class
    document.querySelectorAll('.' + highlightClasses.join(', .'))
        .forEach(el => el.classList.remove(...highlightClasses));
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


    validTargetIds.forEach(targetUid => {
        let targetElement = null;
        targetElement = document.querySelector(`.card[data-uid="${targetUid}"]`);

        if (!targetElement) {
            const localPlayer = getPlayerState(gameState.localPlayerId);
            const opponentPlayer = currentGameState.players.find(p => p.id !== gameState.localPlayerId);
            const player1LeaderUid = localPlayer?.leaderCard?.uid;
            const player2LeaderUid = opponentPlayer?.leaderCard?.uid;

            if (targetUid === player1LeaderUid) {
                targetElement = document.getElementById('player1-avatar');
            } else if (targetUid === player2LeaderUid) {
                targetElement = document.getElementById('player2-avatar');
            }
        }

        if (targetElement) {
            targetElement.classList.add('targetable-spell'); // Reuse same class as spell target
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
    const currentPlayerId = getCurrentPlayerId();

    // Only highlight if it's the LOCAL player's turn and in Main phase
    if (currentPlayerId !== gameState.localPlayerId || currentGameState.phase !== 'Main') {
        return;
    }

    const playerState = getPlayerState(currentPlayerId);
    if (!playerState || !player1HandEl) {
        console.warn("UI Highlights Error: Missing local player state or hand element for highlights.");
        return;
    }


    playerState.hand.forEach(cardInstance => {
        if (!cardInstance) return;

        const cardElement = player1HandEl.querySelector(`.card[data-uid="${cardInstance.uid}"]`);
        if (!cardElement) return; // Skip if element not found

        const playabilityInfo = gameState.playabilityCache[cardInstance.uid];

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
        const actAbilityEntries = Array.isArray(actionState.actAbilities) && actionState.actAbilities.length > 0
            ? actionState.actAbilities
            : [null];
        actAbilityEntries.forEach((actAbility, index) => {
            const actBtn = document.createElement('button');
            actBtn.className = 'act-button';
            const isPlayable = actAbility ? actAbility.isPlayable !== false : actionState.actPlayable !== false;
            const failureReason = actAbility?.failureReason ?? actionState.actFailureReason;
            const actLabel = getBattleActionLabel('act', index, actAbilityEntries.length);
            actBtn.textContent = actLabel;
            actBtn.disabled = actionState.usedThisTurn || isPlayable === false;
            if (!actionState.usedThisTurn && isPlayable === false && failureReason) {
                actBtn.title = failureReason;
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
    updateCardActionButtons(cardInstance);
    const cardUid = cardInstance?.uid ?? cardInstance?.InstanceId ?? cardInstance?.instanceId;
    if (!cardUid) return;
    const cardElement = document.querySelector(`.field-card[data-uid="${cardUid}"]`);
    if (cardElement) {
        applyAttackReadinessClasses(cardElement, cardInstance, 'field');
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

    // --- REVISED BUTTON LOGIC: Base visibility/enabled state on localPlayerId vs currentPlayerId ---
    const isLocalPlayerTurn = currentGameState.localPlayerId === currentGameState.currentPlayerId;

    if (currentGameState.phase === 'Mulligan') {
        const localPlayer = currentGameState.players?.find(p => p?.id === currentGameState.localPlayerId) || null;
        const localCompleted = localPlayer?.hasCompletedMulligan === true;

        // Mulligan Phase:
        // - If local has not confirmed: show enabled confirm button
        // - If local has confirmed: keep showing as disabled "waiting" (avoids stale UI across matches)
        confirmMulliganButtonEl.style.display = 'inline-block';
        confirmMulliganButtonEl.disabled = isConnectionLocked || localCompleted;
        confirmMulliganButtonEl.textContent = localCompleted ? '等待对方...' : '确认调度';

        endTurnButtonEl.style.display = 'none';
        endTurnButtonEl.disabled = true; // Also disable
    } else if (currentGameState.phase === 'TurnStart' || currentGameState.phase === 'Main') {
        // TurnStart or Main Phase: Show end turn, hide confirm. Enable/disable based on whose turn it is.
        confirmMulliganButtonEl.style.display = 'none';
        confirmMulliganButtonEl.disabled = isConnectionLocked;
        confirmMulliganButtonEl.textContent = '确认调度';
        endTurnButtonEl.style.display = 'inline-block';
        // ADDED: Detailed log before setting disabled state
        endTurnButtonEl.disabled = isConnectionLocked || !isLocalPlayerTurn; // Disable if not local player's turn
    } else {
        // Other phases or opponent's turn: Hide both (or just hide confirm, end turn might be handled by turn manager logic implicitly)
        confirmMulliganButtonEl.style.display = 'none';
        confirmMulliganButtonEl.disabled = isConnectionLocked;
        confirmMulliganButtonEl.textContent = '确认调度';
        endTurnButtonEl.style.display = 'none'; // Hide end turn if not P1's main phase
        endTurnButtonEl.disabled = true; // Also disable when hidden
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
            reconnectButtonEl.textContent = `${baseText}，点击重连`;
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
        gameState.turnEndsAtUtc = null;
        gameState.turnStartedAtUtc = null;
        updateTurnTimerDisplay(0, 0, false);
    }

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
    if (!targetUid || damageAmount === undefined || damageAmount === null || damageAmount < 0) { // Allow zero damage
        // console.warn("UI: showDamageNumber called with invalid arguments (UID missing, damage invalid, or damage <= 0).");
        return;
    }

    let targetElement = null;
    // --- REVISED: Find target element, handling actual Leader Instance IDs ---
    const localPlayer = gameState.players.find(p => p.id === gameState.localPlayerId);
    const opponentPlayer = gameState.players.find(p => p.id !== gameState.localPlayerId);
    const player1LeaderUid = localPlayer?.leaderCard?.uid;
    const player2LeaderUid = opponentPlayer?.leaderCard?.uid;


    if (player1LeaderUid && targetUid === player1LeaderUid) {
        targetElement = document.getElementById('player1-avatar');
    } else if (player2LeaderUid && targetUid === player2LeaderUid) {
        targetElement = document.getElementById('player2-avatar');
    } else {
        // Fallback for non-leader units (followers, amulets if they get UIDs later)
        targetElement = gameContainerEl?.querySelector(`[data-uid='${targetUid}']`);
    }
    // --- END REVISED ---

    if (!targetElement) {
        // console.warn(`UI: Could not find target element for UID: ${targetUid}`);
        return;
    }

    // 使用 document.body 作为参照物，简化定位计算
    const targetRect = targetElement.getBoundingClientRect();

    const damageElement = document.createElement('div');
    damageElement.classList.add('damage-number');
    damageElement.textContent = damageAmount === 0 ? '0' : `-${damageAmount}`; // Display damage as negative number

    // 设置初始位置 (定位在目标元素的顶部中心)
    damageElement.style.position = 'absolute'; // Position relative to viewport initially
    // Initial position calculation needs refinement AFTER element is added to DOM to get offsetWidth
    // Set temporary position first
    damageElement.style.left = `${targetRect.left + targetRect.width / 2}px`;
    damageElement.style.top = `${targetRect.top - 30}px`; // Approx. 30px above target

    // Add to body for global positioning and to avoid parent overflow issues
    document.body.appendChild(damageElement);

    // Refine position calculation AFTER appending to get actual width/height
    // Center horizontally based on its own width
    damageElement.style.left = `${targetRect.left + (targetRect.width / 2) - (damageElement.offsetWidth / 2)}px`;

    // Ensure the element is removed after animation completes
    damageElement.addEventListener('animationend', () => {
        if (damageElement.parentNode) {
            damageElement.remove();
        }
    }, { once: true }); // Use { once: true } for efficiency
}

function showHealNumber(targetUid, healAmount) {
    if (!targetUid || healAmount === undefined || healAmount === null || healAmount < 0) {
        return;
    }

    let targetElement = null;
    const localPlayer = gameState.players.find(p => p.id === gameState.localPlayerId);
    const opponentPlayer = gameState.players.find(p => p.id !== gameState.localPlayerId);
    const player1LeaderUid = localPlayer?.leaderCard?.uid;
    const player2LeaderUid = opponentPlayer?.leaderCard?.uid;

    if (player1LeaderUid && targetUid === player1LeaderUid) {
        targetElement = document.getElementById('player1-avatar');
    } else if (player2LeaderUid && targetUid === player2LeaderUid) {
        targetElement = document.getElementById('player2-avatar');
    } else {
        targetElement = gameContainerEl?.querySelector(`[data-uid='${targetUid}']`);
    }

    if (!targetElement) {
        return;
    }

    const targetRect = targetElement.getBoundingClientRect();
    const healElement = document.createElement('div');
    healElement.classList.add('heal-number');
    healElement.textContent = `+${healAmount}`;

    healElement.style.position = 'absolute';
    healElement.style.left = `${targetRect.left + targetRect.width / 2}px`;
    healElement.style.top = `${targetRect.top - 30}px`;

    document.body.appendChild(healElement);

    healElement.style.left = `${targetRect.left + (targetRect.width / 2) - (healElement.offsetWidth / 2)}px`;

    healElement.addEventListener('animationend', () => {
        if (healElement.parentNode) {
            healElement.remove();
        }
    }, { once: true });
}
// --- 事件处理 ---

// 更新 initializeUI 以确保调用事件设置 (确保 DOM 加载后执行)
function initializeUI() {
    cacheDOMElements();
    renderBattleCardPreview(null);
    setupGlobalEventListeners(); // 确保调用
}
// (确保 initializeUI 在 DOMContentLoaded 后调用)

// 设置全局事件监听器 (事件委托模式)
function setupGlobalEventListeners() {
    if (!gameContainerEl) {
        // console.error("UI Error: Cannot setup global listeners - gameContainerEl not found.");
        return;
    }
    // 先移除旧监听器，防止重复添加 (如果 initializeUI 可能被多次调用)
    gameContainerEl.removeEventListener('click', handleGameClick);
    gameContainerEl.addEventListener('click', handleGameClick);
}

/**
 * Updates the PP+1 availability display for a specific player.
 * @param {string} playerId - The GUID of the player.
 * @param {number} usesAvailable - Remaining PP+1 uses.
 */
function updatePpBoostDisplay(playerId, usesAvailable) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const prefix = isLocalPlayer ? 'player1' : 'player2';
    const countEl = document.getElementById(`${prefix}-pp-boost-count`);
    const buttonEl = document.getElementById(`${prefix}-pp-boost-button`);

    if (countEl) {
        // 隐藏次数显示
        countEl.style.display = 'none';
    }

    if (buttonEl) {
        const isLocalTurn = gameState.currentPlayerId === gameState.localPlayerId;
        const isTurnPhase = gameState.phase === 'Main' || gameState.phase === 'TurnStart';
        const canUse = !isConnectionLocked && isLocalPlayer && isLocalTurn && isTurnPhase && (usesAvailable ?? 0) > 0 && !gameState.isSpectator;

        buttonEl.disabled = !canUse;
        buttonEl.classList.toggle('available', canUse);
        
        // 确保按钮文本为 PP+1
        const textEl = buttonEl.querySelector('.pp-boost-action-text');
        if (textEl) {
            textEl.textContent = 'PP+1';
        }

        // 移除复杂的 title，保持简洁
        buttonEl.title = '';
    }
}

function handleGameDoubleClick(event) {
    const clickedCardElement = event.target.closest('.card');
    if (!clickedCardElement) return;

    const uid = clickedCardElement.dataset.uid;
    if (!uid) return;

    const cardInstance = findCardInstanceByUid(uid);
    if (cardInstance) {
        handleCardDoubleClick(cardInstance);
    }
}

// 全局点击事件处理器 (事件委托)
function handleGameClick(event) {
    const clickedReconnectButton = event.target.closest('#reconnect-button');
    if (clickedReconnectButton) {
        if (reconnectButtonEl?.disabled) {
            return;
        }
        document.dispatchEvent(new Event('requestReconnect'));
        return;
    }

    if (isConnectionLocked) {
        return;
    }

    const clickedPreviewRoot = event.target.closest('#battle-card-preview');
    const clickedPreviewTooltip = event.target.closest('.preview-description-tooltip');
    const clickedDetailModal = event.target.closest('#card-detail-modal, #card-detail-image-modal');
    const clickedCardOrAvatar = event.target.closest('.card, #player1-avatar, #player2-avatar, .emblem-slot[data-uid]');
    if (!clickedPreviewRoot && !clickedPreviewTooltip && !clickedDetailModal && !clickedCardOrAvatar) {
        renderBattleCardPreview(null);
    }

    const clickedSpectatorToggle = event.target.closest('#spectator-toggle-view');
    if (clickedSpectatorToggle) {
        if (!gameState.isSpectator) return;
        const playerIds = (gameState.players || []).map(p => p?.id).filter(Boolean);
        if (playerIds.length < 2) return;

        const next = playerIds.find(id => id !== gameState.localPlayerId) || playerIds[0];
        gameState.localPlayerId = next;
        clearSelectionHighlights();
        renderGame(gameState);
        return;
    }

    const clickedPreviewToken = event.target.closest('[data-preview-token]');
    if (clickedPreviewToken) {
        const tokenType = clickedPreviewToken.dataset.previewToken;
        const tokenValue = clickedPreviewToken.dataset.previewValue;
        if (tokenType && tokenValue) {
            event.preventDefault();
            event.stopPropagation();
            void showPreviewDescriptionTooltip(clickedPreviewToken, tokenType, tokenValue);
        }
        return;
    }

    // --- Mulligan Phase Logic ---
    if (gameState.phase === 'Mulligan') {
        const clickedMulliganConfirm = event.target.closest('#confirm-mulligan-button');
        const clickedHandCard = event.target.closest('#player1-hand .card'); // Only care about player 1 hand

        if (clickedMulliganConfirm) {
            const localPlayer = gameState.players?.find(p => p?.id === gameState.localPlayerId) || null;
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
                alert("错误：调度功能未加载！(submitMulligan)"); // Clarify alert slightly for debugging
            }
            // Note: No need to manually remove 'selected-for-mulligan' class here,
            // as the next renderGame call after handleMulligan will redraw the hand.
            return; // Stop processing click after handling mulligan confirm

        } else if (clickedHandCard) {
            const localPlayer = gameState.players?.find(p => p?.id === gameState.localPlayerId) || null;
            const localCompleted = localPlayer?.hasCompletedMulligan === true;
            if (localCompleted) return;

            const uid = clickedHandCard.dataset.uid;
            if (!uid) return;

            const previewCard = findCardInstanceByUid(uid);
            if (previewCard) {
                renderBattleCardPreview(previewCard);
            }

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

    // Custom double-click detection for hand cards (avoid native dblclick delay).
    const clickedCardElementForDouble = event.target.closest('.card');
    if (clickedCardElementForDouble) {
        const uid = clickedCardElementForDouble.dataset.uid;
        if (uid) {
            const now = Date.now();
            const isDoubleClick = lastCardClick.uid === uid && (now - lastCardClick.time) <= DOUBLE_CLICK_WINDOW_MS;
            lastCardClick = { uid, time: now };

            if (isDoubleClick) {
                lastCardClick = { uid: null, time: 0 };
                const cardInstance = findCardInstanceByUid(uid);
                if (cardInstance) {
                    handleCardDoubleClick(cardInstance);
                    return;
                }
            }
        }
    } else {
        lastCardClick = { uid: null, time: 0 };
    }

    // If not in Mulligan phase, continue with normal game interactions:

    // Check if the click was on the End Turn button
    const clickedEndTurnButton = event.target.closest('#end-turn-button');
    if (clickedEndTurnButton) {
        if (gameState.isSpectator) return;
        // Correctly check for the imported endTurnRequest function
        if (typeof endTurnRequest === 'function') {
            endTurnButtonEl.disabled = true; // Disable button immediately on click
            endTurnRequest(); // Call new SignalR wrapper
        } else {
            // This error should ideally not happen if imports are correct, but keep for safety
            console.error(`UI Error: endTurnRequest function is not available/imported!`);
            alert("错误：结束回合功能未加载！(endTurnRequest)"); // Update alert message
        }
        return; // Stop processing this click further
    }

    const clickedPpBoostButton = event.target.closest('.pp-boost-button');
    if (clickedPpBoostButton) {
        if (clickedPpBoostButton.disabled) return;
        if (typeof requestPpBoost === 'function') {
            requestPpBoost();
        } else {
            alert("错误：PP+1功能未加载！");
        }
        return;
    }

    const clickedUseButton = event.target.closest('.use-button');
    if (clickedUseButton) {
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

    // Existing logic follows... (This comment isn't inserted, just for clarity here)
    // --- Check for Super Evolve Button Click FIRST ---
    const clickedSuperEvolveButton = event.target.closest('.super-evolve-button');
    if (clickedSuperEvolveButton) {
        const superEvolveUid = clickedSuperEvolveButton.dataset.superEvolveUid;
        if (superEvolveUid) {
            if (typeof superEvolveCard === 'function') {
                superEvolveCard(superEvolveUid);
            } else {
                alert("错误：超进化功能未加载！");
            }
        }
        return;
    }

    // --- Check for Evolve Button Click FIRST ---
    const clickedEvolveButton = event.target.closest('.evolve-button');
    if (clickedEvolveButton) {
        const evolveUid = clickedEvolveButton.dataset.evolveUid; // Get UID from button's data attribute
        if (evolveUid) {
            if (typeof evolveCard === 'function') { // Check if function is imported
                evolveCard(evolveUid); // Call the imported function from gameManager
            } else {
                // console.error("UI Error: evolveCard function is not available/imported!");
                alert("错误：进化功能未加载！");
            }
        } else {
            // console.warn("UI: Evolve button clicked but missing data-evolve-uid attribute.");
        }
        return; // <<< IMPORTANT: Stop processing click here if evolve button was hit
    }
    // --- END Check for Evolve Button ---

    // --- Check for Fusion Button Click ---
    const clickedFusionButton = event.target.closest('.fusion-button');
    if (clickedFusionButton) {
        if (gameState.isSpectator) return;
        const fusionUid = clickedFusionButton.dataset.fusionUid;
        const fusionAbilityId = clickedFusionButton.dataset.fusionAbilityId || null;
        if (!fusionUid) return;
        requestFusion(fusionUid, fusionAbilityId);
        return;
    }
    // --- END Check for Fusion Button ---

    // --- Check for Act Button Click ---
    const clickedActButton = event.target.closest('.act-button');
    if (clickedActButton) {
        if (gameState.isSpectator) return;
        const actUid = clickedActButton.dataset.actUid;
        const actAbilityId = clickedActButton.dataset.actAbilityId || null;
        if (actUid) {
            if (typeof requestAct === 'function') {
                requestAct(actUid, actAbilityId);
            } else {
                alert("错误：启动功能未加载！");
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
    const clickedPlayer1Avatar = event.target.closest('#player1-avatar'); // Check player avatar
    const clickedPlayer2Avatar = event.target.closest('#player2-avatar'); // Check opponent avatar

    let targetUid = null;
    if (clickedCardElement) {
        targetUid = clickedCardElement.dataset.uid;
    } else if (clickedEmblemElement) {
        targetUid = clickedEmblemElement.dataset.uid;
    // --- BUG-017 FIX: Get actual leader UID instead of hardcoding L1/L2 ---
    } else if (clickedPlayer1Avatar || clickedPlayer2Avatar) {
        const clickedAvatarId = clickedPlayer1Avatar ? 'player1-avatar' : 'player2-avatar';

        // 【关键修复】正确映射DOM ID到实际玩家
        // 根据renderGame函数的逻辑：localPlayer总是映射到player1，opponent总是映射到player2
        const localPlayerId = gameState.localPlayerId;
        const opponentPlayerId = gameState.players.find(p => p.id !== localPlayerId)?.id;

        // 确定点击的是哪个玩家的头像
        let clickedPlayerActualId = null;
        if (clickedAvatarId === 'player2-avatar') { // 对手头像总是 player2-avatar
            clickedPlayerActualId = opponentPlayerId;
        } else {
            clickedPlayerActualId = localPlayerId;
        }

        // Player choice mode: both leaders may be valid targets.
        if (isAwaitingPlayerChoice) {
            const player = getPlayerState(clickedPlayerActualId);
            const leaderUid = player?.leaderCard?.uid ?? player?.leaderCard?.InstanceId ?? null;
            if (leaderUid) {
                targetUid = leaderUid;
            }
        } else {
            // Always resolve the clicked leader UID so spells/abilities can target either leader.
            // Combat legality is still enforced later in handleCardClick/selectAttackTarget.
            const clickedPlayer = clickedPlayerActualId ? getPlayerState(clickedPlayerActualId) : null;
            const leaderUid = clickedPlayer?.leaderCard?.uid ?? clickedPlayer?.leaderCard?.InstanceId ?? null;
            if (leaderUid) {
                targetUid = leaderUid;
            } else {
                console.error(`UI Error: Could not find leader card instance or UID for player ${clickedPlayerActualId}`, clickedPlayer);
            }
        }
    // --- END BUG-017 FIX ---
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
        alert("发生内部错误：无法获取玩家状态！");
        return;
    }

    if (!cardInstance) {
        // console.error("UI: handleCardClick received null cardInstance.");
        return; // 安全检查
    }

    renderBattleCardPreview(cardInstance);

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
        const reason = useActionState.useFailureReason || "该卡牌现在无法使用。";
        console.warn(`UI: ${source} use rejected for card ${cardInstance?.name ?? ''} (${useActionState.useUid}). Reason: ${reason}`);
        alert(reason);
        return true;
    }

    playCard(useActionState.useUid);
    return true;
}

function handleCardDoubleClick(cardInstance) {
    void tryUseHandCard(cardInstance, 'double-click');
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
    const { min } = getChoiceMinMax(choiceRequest);

    if (selectableTargetIds.length === 0) {
        // If there are no candidates and min==0, the only valid submission is an empty selection.
        if (min === 0 && choiceRequest?.PendingChoiceId && choiceRequest?.SourceAbilityRuntimeId) {
            void (async () => {
                const pendingChoiceId = choiceRequest.PendingChoiceId;
                const sourceAbilityRuntimeId = choiceRequest.SourceAbilityRuntimeId;
                startPlayerChoice(choiceRequest);
                const ok = await submitPlayerChoice(pendingChoiceId, [], sourceAbilityRuntimeId, false);
                if (ok && isCurrentChoiceRequest(pendingChoiceId, sourceAbilityRuntimeId)) {
                    endPlayerChoice(false);
                }
            })();
            return;
        }

        console.warn("UI: No selectable targets provided for player choice. Ending choice UI.");
        endPlayerChoice(false);
        return;
    }

    // Enter player choice mode and render.
    startPlayerChoice(choiceRequest);
    renderGame(gameState);
    renderChoiceOverlayUI();
    updateChoiceOverlayUI();
}

// --- Reveal UI ---
const revealQueue = [];
let revealIsShowing = false;

export function showRevealOverlay(cardDto, options = {}) {
    if (!cardDto) return;
    const durationMs = Number.isFinite(options.durationMs) ? options.durationMs : 1600;
    revealQueue.push({ cardDto, durationMs });
    void processRevealQueue();
}

async function processRevealQueue() {
    if (revealIsShowing) return;
    revealIsShowing = true;

    try {
        while (revealQueue.length > 0) {
            const { cardDto, durationMs } = revealQueue.shift();
            renderRevealOverlay(cardDto);
            await new Promise(r => setTimeout(r, durationMs));
            document.getElementById('reveal-overlay-root')?.remove();
            await new Promise(r => setTimeout(r, 120));
        }
    } finally {
        revealIsShowing = false;
    }
}

function renderRevealOverlay(cardDto) {
    document.getElementById('reveal-overlay-root')?.remove();

    const overlayRoot = document.createElement('div');
    overlayRoot.id = 'reveal-overlay-root';
    overlayRoot.className = 'reveal-overlay-root';

    const name = cardDto?.Name ?? 'Unknown Card';
    const cost = Number.isFinite(cardDto?.Cost) ? cardDto.Cost : '?';
    const desc = cardDto?.Description ?? '';

    overlayRoot.innerHTML = `
        <div class="reveal-overlay-backdrop"></div>
        <div class="reveal-overlay-content">
            <div class="reveal-title">公开</div>
            <div class="card reveal-card">
                <div class="cost">${cost}</div>
                <div class="name">${name}</div>
                <div class="desc">${desc}</div>
            </div>
        </div>
    `;

    overlayRoot.addEventListener('click', () => {
        document.getElementById('reveal-overlay-root')?.remove();
        revealQueue.length = 0;
        revealIsShowing = false;
    }, { once: true });

    if (gameContainerEl) {
        gameContainerEl.appendChild(overlayRoot);
    } else {
        document.body.appendChild(overlayRoot);
    }
}

// --- Fusion UI ---
export function showFusionAnimation(fusingPlayerId, fusedCardInstanceIds = []) {
    const ids = Array.isArray(fusedCardInstanceIds) ? fusedCardInstanceIds : [];
    if (!fusingPlayerId || ids.length === 0) return;

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

        document.body.appendChild(clone);

        const fromX = rect.left + rect.width / 2;
        const fromY = rect.top + rect.height / 2;
        const dx = (target.x - fromX);
        const dy = (target.y - fromY) - (idx * 8);

        // Start next frame so the transition runs reliably.
        requestAnimationFrame(() => {
            clone.style.transform = `translate(${dx}px, ${dy}px) scale(0.25)`;
            clone.style.opacity = '0';
        });

        window.setTimeout(() => clone.remove(), 700);
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
    document.getElementById('fusion-burst-root')?.remove();

    const root = document.createElement('div');
    root.id = 'fusion-burst-root';
    root.className = 'fusion-burst-root';
    root.innerHTML = `
        <div class="fusion-burst">
            <div class="fusion-burst-ring"></div>
            <div class="fusion-burst-text">Fusion x${count}</div>
        </div>
    `;

    (gameContainerEl ?? document.body).appendChild(root);
    window.setTimeout(() => root.remove(), 700);
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
    container.appendChild(overlayRoot);

    const { min, max } = getChoiceMinMax(currentChoiceRequest);
    const singleMandatory = isSingleMandatoryChoice(currentChoiceRequest);

    const controls = document.createElement('div');
    controls.className = 'choice-controls-overlay choice-overlay';
    controls.addEventListener('click', e => e.stopPropagation());

    const message = currentChoiceRequest.Message || '请选择目标';
    const hint = singleMandatory
        ? (currentChoiceRequest.IsCancellable === true ? '点击目标选择；点击空白处或取消可中止' : '点击目标选择')
        : (currentChoiceRequest.IsCancellable === true ? '点击目标可多选；点击确定提交，取消可中止' : '点击目标可多选；点击确定提交');

    controls.innerHTML = `
        <div class="choice-prompt-text">${message}</div>
        <div class="choice-prompt-hint">${hint}</div>
        <div class="choice-controls-row">
            <div class="choice-selected-count">已选：<span id="choice-selected-count">0/${min}-${max}</span></div>
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
        confirmBtn.style.display = singleMandatory ? 'none' : '';
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
            return !document.querySelector(`.card[data-uid="${uid}"]`);
        });

    if (missingCardInstances.length > 0) {
        const panel = document.createElement('div');
        panel.className = 'choice-panel-overlay choice-overlay';
        panel.addEventListener('click', e => e.stopPropagation());

        const list = document.createElement('div');
        list.className = 'choice-card-list';

        missingCardInstances.forEach(dto => {
            const uid = dto.InstanceId;
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'choice-card';
            btn.dataset.uid = String(uid);
            btn.innerHTML = `
                <div class="choice-card-cost">${dto.Cost ?? '?'}</div>
                <div class="choice-card-name">${dto.Name ?? 'Unknown Card'}</div>
                <div class="choice-card-desc">${dto.Description ?? ''}</div>
            `;
            btn.addEventListener('click', e => {
                e.stopPropagation();
                void handleChoiceTargetClick(uid);
            });
            list.appendChild(btn);
        });

        panel.innerHTML = `<div class="choice-panel-title">可选择的卡牌</div>`;
        panel.appendChild(list);
        overlayRoot.appendChild(panel);
    }
}
