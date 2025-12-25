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
    // handleMulligan, // Removed import - Now uses submitMulligan
    isValidSpellTarget,
    submitMulligan, // Added import for SignalR mulligan submission
    endTurnRequest,  // Added import for SignalR end turn request
    submitPlayerChoice,
    cancelPlayerChoice
} from './gameManager.js';
// getCardDefinition is obsolete. Card instances now contain all necessary data.
// import { endTurn } from './turnManager.js'; // Removed import - Now uses endTurnRequest
// console.log("ui.js loaded");

export { renderGame, showDamageNumber, updatePpDisplay, updateEpDisplay, updateSepDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateHealthDisplay, updateHandDisplay, addUnitToField, updateUnitDisplay, removeUnitFromField, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus, clearSelectionHighlights, resetMulliganSelection };

let mulliganSelectedUids = new Set();

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
        const filledSegments = currentPp || 0;
        const unlockedSegments = maxPp || 0;

        for (let i = 0; i < displayMax; i++) {
            const segment = document.createElement('div');
            segment.classList.add('pp-segment');
            
            if (i < filledSegments) {
                segment.classList.add('active'); // Filled PP
            } else if (i < unlockedSegments) {
                // Unlocked but empty (default style)
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

            if (valueEl) valueEl.textContent = `${newHealth}/${maxHp}`;
            
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
        // console.log(`UI UPDATE: Updated health for ${unitInstance.name} (UID: ${unitInstance.uid}) to ${newHealth}`);
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

        // Opponent's hand is rendered with card backs
        if (!isLocalPlayer) {
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
    console.log(`UI: Showing Mulligan interface for local player ${localPlayerId}`);

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

    console.log("UI: Local player Mulligan interface displayed.");
}


/**
 * Updates the UI to reflect a player's mulligan completion status.
 * Currently focuses on disabling the local player's confirm button and updating text.
 * @param {string} playerId - The ID of the player who completed mulligan.
 * @param {boolean} isComplete - Always true when called from the handler.
 */
function updateMulliganStatus(playerId, isComplete) {
    console.log(`UI UPDATE: Mulligan completed for player ${playerId}. Updating UI status.`);

    // Focus on local player's UI changes when they complete
    if (playerId === gameState.localPlayerId && isComplete) {
        if (confirmMulliganButtonEl) {
            confirmMulliganButtonEl.disabled = true;
            confirmMulliganButtonEl.textContent = '等待对方...'; // Change button text to indicate waiting
            console.log(`UI UPDATE: Disabled confirm mulligan button and updated text for local player ${playerId}.`);
        } else {
            console.warn("UI Update Warning: Confirm Mulligan button element not found, cannot update status.");
        }
        // Optionally, add a class to indicate local completion if needed for styling
        // gameContainerEl?.classList.add('local-mulligan-complete');
    } else if (playerId !== gameState.localPlayerId && isComplete) {
        // Handle opponent completion if needed (e.g., hide a general "Waiting..." message if one exists)
        console.log(`UI UPDATE: Opponent ${playerId} completed mulligan. (No specific UI update implemented yet)`);
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
        return;
    }
    if (!cardInstance) {
        console.warn(`UI Update Error: cardInstance is null or undefined in addUnitToField for player ${playerId}.`);
        return;
    }

    const cardElement = createCardElement(cardInstance, 'field');
    if (cardElement) {
        fieldElement.appendChild(cardElement);
    }
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
    // console.log(`UI UPDATE: Updating display for unit UID: ${unitInstance.uid}. Element found: ${!!unitElement}`); // Optional Debug

    if (!unitElement) {
        console.warn(`UI Update Error: Could not find field element for unit UID: ${unitInstance.uid}`);
        return;
    }

    // Update Stats
    const attackEl = unitElement.querySelector('.attack');
    const healthEl = unitElement.querySelector('.health');

    if (attackEl) {
        attackEl.textContent = unitInstance.currentAttack ?? '?';
    } else {
        console.warn(`UI Update Warning: Could not find .attack element for unit UID: ${unitInstance.uid}`);
    }
    // 生命值更新已移至独立的 updateHealthDisplay 函数，以遵循单一职责原则
    // if (healthEl) {
    //     healthEl.textContent = unitInstance.currentHealth ?? '?';
    // } else {
    //     console.warn(`UI Update Warning: Could not find .health element for unit UID: ${unitInstance.uid}`);
    // }

    // Update Evolved Status Class
    if (unitInstance.isEvolved) {
        unitElement.classList.add('evolved');
        // console.log(`UI UPDATE: Added .evolved class to unit UID: ${unitInstance.uid}`); // Optional Debug
    } else {
        // Should typically not de-evolve, but handle just in case
        unitElement.classList.remove('evolved');
    }

    // Update Ward Status Class
    if (Array.isArray(unitInstance.keywords) && unitInstance.keywords.includes('Ward')) {
        unitElement.classList.add('has-ward');
    } else {
        unitElement.classList.remove('has-ward');
    }

    // TODO: Update other visual cues if necessary (e.g., 'canAttack' indicator, effects)
    // For now, rely on existing classes or add specific ones as needed.
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
    // console.log(`UI UPDATE: Attempting to remove unit UID: ${unitUid}. Element found: ${!!unitElement}`); // Optional Debug

    if (unitElement && unitElement.parentNode) {
        unitElement.parentNode.removeChild(unitElement);
        // console.log(`UI UPDATE: Successfully removed unit element UID: ${unitUid} from the DOM.`); // Optional Debug
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
        console.log(`UI Highlights: Marked UID ${attackerUid} as selected-attacker.`); // Optional debug
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
    // 【BUG修复】在函数作用域内正确定义 hasWard 变量
    const hasWard = opponentBoard.some(follower => follower?.keywords?.includes('Ward'));
    const wardFollowers = opponentBoard.filter(f => f && f.keywords?.includes('Ward'));

    let validTargets = [];

    if (wardFollowers.length > 0) {
        // If Ward is present, only Ward followers are valid targets
        validTargets = wardFollowers;
    } else {
        // If no Ward, all followers are valid targets
        validTargets = [...opponentBoard];
        
        // Check if the leader is a valid target
        const hasStorm = attackerInstance.keywords?.includes("Storm");
        const isSummoningSick = attackerInstance.fieldTurn === 0 && !attackerInstance.isEvolved;

        if (hasStorm || !isSummoningSick) {
            if (opponentLeader) {
                validTargets.push(opponentLeader);
            }
        }
    }
    
    const finalTargets = validTargets.filter(Boolean);
    console.log(`UI Highlights: Highlighting ${finalTargets.length} valid targets for attacker ${attackerInstance.uid}.`);
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
            console.log(`UI Highlights: Leader target - OpponentID: ${opponentPlayerId}, AvatarID: ${avatarId}, Found: ${!!targetElement}`);
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
                console.log(`UI Highlights: Marked ${targetInstance.name ?? targetInstance.uid} (Leader: ${isLeader}) as ${cssClass}.`);
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

function startPlayerChoice(requestData) {
    isAwaitingPlayerChoice = true;
    currentChoiceRequest = requestData;
    currentChoiceSelectedTargetIds = new Set();
    console.log("UI State: Entered player choice mode.", requestData);
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

    console.log(`UI State: Exited player choice mode. Notify backend of cancellation: ${notifyBackendOfCancellation}`);
    clearSelectionHighlights();
    gameContainerEl?.classList.remove('player-choice-active');

    removeChoiceOverlayUI();

    // BUG-FIX: Notify the backend that the choice was cancelled, only if requested.
    if (notifyBackendOfCancellation && choiceToCancel && choiceToCancel.PendingChoiceId && choiceToCancel.SourceAbilityRuntimeId) {
        console.log(`UI: Choice cancelled. Notifying game manager for ability ${choiceToCancel.SourceAbilityRuntimeId} with PendingChoiceId ${choiceToCancel.PendingChoiceId}.`);
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
    if (ok) {
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
        if (ok) {
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

// 缓存 DOM 元素的函数
function cacheDOMElements() {
    // console.log("UI: Caching DOM elements...");
    try {
        gameContainerEl = document.getElementById('game-container');
        turnInfoEl = document.getElementById('turn-info');
        endTurnButtonEl = document.getElementById('end-turn-button');
        confirmMulliganButtonEl = document.getElementById('confirm-mulligan-button'); // Cache the new button
        spectatorToggleViewButtonEl = document.getElementById('spectator-toggle-view');

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
            gameContainerEl, turnInfoEl, endTurnButtonEl, confirmMulliganButtonEl, // Added confirmMulliganButtonEl
            player1HandEl, player1FieldEl, player1HpEl, player1PpEl, player1MaxPpEl, player1EpEl, player1MaxEpEl, player1SepEl, player1MaxSepEl, player1DeckEl, player1GraveEl,
            player2HandEl, player2FieldEl, player2HpEl, player2PpEl, player2MaxPpEl, player2EpEl, player2MaxEpEl, player2SepEl, player2MaxSepEl, player2DeckEl, player2GraveEl
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
            // console.log("UI: DOM elements caching successful.");
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
            spectatorToggleViewButtonEl.style.display = '';
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
    // Always render opponent's hand with card backs at the top
    renderOpponentHand(player2?.hand ?? [], player2HandEl);

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

    // console.log("UI: renderGame finished."); // Optional debug log
}

// --- 具体渲染辅助函数 (占位符) ---
function renderPlayerStats(playerState, prefix) { // prefix = 'player1' or 'player2'
    if (!playerState) {
        return;
    }

    try {
        // We need the container element for the player stats block.
        // Assuming index.html has a container like #player1-stats-container or we use the existing structure.
        // Based on previous code, we were selecting individual elements.
        // To implement the new layout, we should ideally target the parent container of these stats.
        // However, since we might not want to rewrite index.html, we can inject the new structure into the existing parent
        // if we can identify it, OR we can repurpose one of the existing elements as the root.
        
        // Let's assume the existing HTML structure has a container that holds these elements.
        // If not, we might need to be creative.
        // Looking at typical structure: <div id="player1-info"><div class="player-stats">...</div></div>
        
        const infoContainer = document.getElementById(`${prefix}-info`);
        const statsContainer = infoContainer ? infoContainer.querySelector('.player-stats') : null;
        
        if (!statsContainer) {
            console.warn(`UI: Stats container for ${prefix} not found.`);
            return;
        }

        // Update Values
        
        // 1. HP
        const currentHp = playerState.leaderCard?.currentHealth ?? 0;
        const hpValueEl = statsContainer.querySelector('.hp-value');
        if (hpValueEl) {
            hpValueEl.textContent = currentHp;
            // Optional: Add color change for low HP
            if (currentHp <= 5) hpValueEl.style.color = '#ef4444'; // Red
            else hpValueEl.style.color = '#fff';
        }

        // 2. PP
        const currentPp = playerState.pp ?? 0;
        const maxPp = playerState.maxPP ?? 0;
        const ppTextEl = statsContainer.querySelector('.pp-text');
        if (ppTextEl) ppTextEl.textContent = `${currentPp} / ${maxPp}`;
        updatePpDisplay(playerState.id, currentPp, maxPp);

        // 3. EP
        // Default maxEP to 2 if 0 or undefined (User Request)
        const maxEp = (playerState.maxEP && playerState.maxEP > 0) ? playerState.maxEP : 2;
        updateEpDisplay(playerState.id, playerState.ep, maxEp);

        // 4. SEP
        // Default maxSEP to 2 if 0 or undefined (User Request)
        const maxSep = (playerState.maxSEP && playerState.maxSEP > 0) ? playerState.maxSEP : 2;
        updateSepDisplay(playerState.id, playerState.sep, maxSep);

        // 5. Deck
        const deckCountEl = statsContainer.querySelector(`#${prefix}-deck-count`);
        if (deckCountEl) deckCountEl.textContent = playerState.deckCount ?? 0;

        // 6. Graveyard
        const graveCountEl = statsContainer.querySelector(`#${prefix}-grave-count`);
        if (graveCountEl) graveCountEl.textContent = playerState.graveyard?.length ?? 0;

        // 7. Class Name (Update if changed, e.g. transformation)
        const classEl = statsContainer.querySelector('.class-name');
        if (classEl && playerState.class) classEl.textContent = playerState.class;

        // 8. Player Name & Avatar (Ensure they are correct)
        const isLocal = prefix === 'player1';
        const playerNameEl = statsContainer.querySelector('.player-name');
        if (playerNameEl) {
             // For now, static names, but could be dynamic from playerState.name
             playerNameEl.textContent = isLocal ? 'PLAYER' : 'OPPONENT';
        }
        const avatarEl = statsContainer.querySelector('.stats-avatar');
        if (avatarEl) {
            avatarEl.textContent = isLocal ? 'YOU' : 'OPP';
        }

    } catch (error) {
        console.error(`UI Error rendering stats for ${prefix}:`, error);
    }
}
function renderPlayerHand(handCards = [], handElement) {
    // console.log(`UI Rendering player hand with ${handCards.length} cards.`); // Log might be too frequent
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
            // console.log(`UI DBG: Added .evolved class to ${cardInstance.name} (UID: ${cardInstance.uid})`); // 添加调试日志
        }
    }

    // --- ADDED: Ward Indicator ---
    // Check if keywords array exists and includes 'Ward' (Only for field context)
    if (context === 'field' && Array.isArray(cardInstance.keywords) && cardInstance.keywords.includes('Ward')) {
        cardDiv.classList.add('has-ward');
        // console.log(`UI DBG: Added .has-ward class to ${cardInstance.name} (UID: ${cardInstance.uid})`); // Optional Debug
    }
    // --- END Ward Indicator ---

    // 基本的 Inner HTML 结构 - 需要根据 style.css 进行调整
    // 使用 ?? 提供默认值，防止属性缺失导致显示错误
    // 【修复】使用正确的属性名：Name（大写）而不是name（小写）
    cardDiv.innerHTML = `
        <div class="card-art"></div> <!-- 卡面美术图占位 -->
        <div class="cost">${cardInstance.Cost ?? cardInstance.cost ?? '?'}</div>
        <div class="name">${cardInstance.Name ?? cardInstance.name ?? 'Unknown Card'}</div>
        ${
        // 仅为随从显示攻击力和生命值区域
        (cardInstance.Type === 'Follower' || cardInstance.type === 'Follower' || cardInstance.Type === 'Follower_Evolved' || cardInstance.type === 'Follower_Evolved') ?
            // Use CurrentAttack and CurrentHealth from the DTO received from server
            `<div class="stats">
                <span class="attack">${cardInstance.CurrentAttack ?? cardInstance.currentAttack ?? '?'}</span>/<span class="health">${cardInstance.CurrentHealth ?? cardInstance.currentHealth ?? '?'}</span>
            </div>`
            : '' // 法术和护符不显示 stats 区域
        }
        <div class="description">${cardInstance.Description ?? cardInstance.description ?? ''}</div> <!-- 卡牌描述区域 -->
    `;

    // 注意: 'playable', 'selected-attacker', 'targetable' 等交互状态相关的 class
    // 应该由 renderHighlights 函数根据当前游戏状态来动态添加或移除。

    // --- Add Evolve Button Conditionally (UI Side) ---
    if (context === 'field') {
        // Add null checks for safety, although these should exist if we reach here
        const currentPlayerId = getCurrentPlayerId();
        const playerState = getPlayerState(currentPlayerId);

        // Check conditions for showing the button (mirrors server-side but simplified)
        const cardType = cardInstance.type ?? cardInstance.Type;
        const isEvolved = (cardInstance.isEvolved ?? cardInstance.IsEvolved) === true;
        const canEvolve = (cardInstance.canEvolve ?? cardInstance.CanEvolve) === true;

        const showSuperEvolveButton = (
            playerState &&
            currentPlayerId === gameState.localPlayerId &&
            currentPlayerId === cardInstance.ownerId &&
            gameState.phase === 'Main' &&
            playerState.sep > 0 &&
            playerState.canSuperEvolve === true &&
            cardType === 'Follower' &&
            isEvolved === false &&
            canEvolve === true
        );

        const showEvolveButton = (
            playerState && // Ensure playerState exists
            currentPlayerId === gameState.localPlayerId && // Only show button for human player's turn
            currentPlayerId === cardInstance.ownerId &&
            gameState.phase === 'Main' &&
            playerState.ep > 0 &&
            playerState.canEvolve === true &&
            cardType === 'Follower' &&
            isEvolved === false &&
            canEvolve === true
        );

        if (showSuperEvolveButton) {
            const superEvolveBtn = document.createElement('button');
            superEvolveBtn.className = 'super-evolve-button';
            superEvolveBtn.textContent = 'Super Evolve';
            superEvolveBtn.dataset.superEvolveUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
            cardDiv.appendChild(superEvolveBtn);
        }

        if (showEvolveButton) {
            const evolveBtn = document.createElement('button');
            evolveBtn.className = 'evolve-button';
            evolveBtn.textContent = 'Evolve';
            evolveBtn.dataset.evolveUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
            cardDiv.appendChild(evolveBtn);
        }
    }
    // --- END Evolve Button ---

    // --- Add Act Button Conditionally (UI Side) ---
    if (context === 'field') {
        const currentPlayerId = getCurrentPlayerId();
        const playerState = getPlayerState(currentPlayerId);

        const hasActAbility = (cardInstance.hasActAbility === true) || (cardInstance.HasActAbility === true);
        const lastActTurn = cardInstance.actLastUsedTurnNumber ?? cardInstance.ActLastUsedTurnNumber ?? null;
        const currentTurnNumber = playerState?.turnNumber ?? playerState?.TurnNumber ?? 0;
        const usedThisTurn = lastActTurn !== null && lastActTurn === currentTurnNumber;

        const showActButton = (
            playerState &&
            currentPlayerId === gameState.localPlayerId &&
            currentPlayerId === cardInstance.ownerId &&
            gameState.phase === 'Main' &&
            hasActAbility
        );

        if (showActButton) {
            const actBtn = document.createElement('button');
            actBtn.className = 'act-button';
            actBtn.textContent = usedThisTurn ? 'Act (Used)' : 'Act';
            actBtn.disabled = usedThisTurn;
            actBtn.dataset.actUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
            cardDiv.appendChild(actBtn);
        }
    }
    // --- END Act Button ---

    // 【修复】移除鼠标悬停时的可玩性检查请求，改为依赖服务器主动推送
    // 服务器会在玩家行动完成后自动推送手牌可玩性信息
    // --- Mouse Enter Listener Removed (Server now pushes playability proactively) ---
    // if (context === 'hand') {
    //     cardDiv.addEventListener('mouseenter', () => {
    //         // Only check for the local player's cards
    //         const ownerPlayer = getPlayerState(cardInstance.ownerId);
    //         if (ownerPlayer && ownerPlayer.id === gameState.localPlayerId) {
    //              // console.log(`UI: Mouse entered card ${cardInstance.uid}. Requesting playability check.`); // Debug log
    //              requestPlayabilityCheck(cardInstance.uid); // Call the function from gameManager
    //         }
    //     });
    //     // Optional: Add mouseleave listener to clear highlights or cache?
    //     // cardDiv.addEventListener('mouseleave', () => { ... });
    // }
    // --- END Mouse Enter Listener ---

    return cardDiv;
}

// --- 渲染回合信息的辅助函数 ---
function renderTurnInfo(turnNumber, currentPlayerId) {
    // Update the turn information display element if it was cached successfully
    if (turnInfoEl) {
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

// --- 渲染游戏日志的辅助函数 ---
let isLogExpanded = false; // Default to collapsed

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

        // Create Title
        const title = document.createElement('span');
        title.className = 'log-title';
        title.textContent = 'BATTLE LOG';

        header.appendChild(toggleBtn);
        header.appendChild(title);

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

    // Update Logs Content
    if (!logs || !Array.isArray(logs)) {
        return;
    }

    // Clear existing logs in content wrapper
    logContent.innerHTML = '';

    // Render last 15 logs
    const recentLogs = logs.slice(-15);
    
    recentLogs.forEach(log => {
        const logEntry = document.createElement('div');
        logEntry.className = 'log-entry';
        logEntry.textContent = `> ${log}`;
        logContent.appendChild(logEntry);
    });

    // Auto-scroll to bottom if expanded
    if (isLogExpanded) {
        logContent.scrollTop = logContent.scrollHeight;
    }
}


// --- 处理高亮 (可交互状态) 的辅助函数 ---
// --- 处理高亮 (可交互状态) 的辅助函数 --- // Keeping the comment from original line 411
function renderHighlights(currentGameState) {
    clearSelectionHighlights();

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
    // console.log("UI Highlights: Cleared all highlight classes."); // Optional debug
}

// Helper 2: Render Ability Target Highlights (Replaces renderSpellTargetHighlights)
function renderAbilityTargetHighlights(currentGameState) {
    if (!currentChoiceRequest) {
        console.warn("UI Highlights: renderAbilityTargetHighlights called but currentChoiceRequest is null.");
        return;
    }

    console.log(`UI Highlights: Applying ability target highlights.`);
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

    console.log(`UI Highlights: Highlighting ${validTargetIds.length} valid targets for player choice:`, validTargetIds);

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
        // console.log("UI Highlights: Skipping playable card highlights (not local player's turn or not Main phase).");
        return;
    }

    const playerState = getPlayerState(currentPlayerId);
    if (!playerState || !player1HandEl) {
        console.warn("UI Highlights Error: Missing local player state or hand element for highlights.");
        return;
    }

    // console.log("UI Highlights: Applying playable card highlights based on cache..."); // Debug log

    playerState.hand.forEach(cardInstance => {
        if (!cardInstance) return;

        const cardElement = player1HandEl.querySelector(`.card[data-uid="${cardInstance.uid}"]`);
        if (!cardElement) return; // Skip if element not found

        const playabilityInfo = gameState.playabilityCache[cardInstance.uid];

        // Remove previous highlights first
        cardElement.classList.remove('playable', 'requires-target', 'no-valid-targets', 'can-play-without-target');

        if (playabilityInfo) {
            // console.log(`UI Highlights: Cache found for ${cardInstance.uid}:`, playabilityInfo); // Debug log
            if (playabilityInfo.isPlayable) {
                cardElement.classList.add('playable');
                // console.log(`UI Highlights: Marked ${cardInstance.name} as playable.`);

                if (playabilityInfo.requiresTargetSelection) {
                    cardElement.classList.add('requires-target');
                    // console.log(`UI Highlights: Marked ${cardInstance.name} as requires-target.`);
                    if (!playabilityInfo.hasValidTargets) {
                        cardElement.classList.add('no-valid-targets');
                        // console.log(`UI Highlights: Marked ${cardInstance.name} as no-valid-targets.`);
                        // Optionally add class if it can be played without target anyway
                        if (playabilityInfo.canPlayWithoutTarget) {
                             cardElement.classList.add('can-play-without-target');
                             // console.log(`UI Highlights: Marked ${cardInstance.name} as can-play-without-target.`);
                        }
                    }
                }
            } else {
                 // Optionally add a 'not-playable' class or title attribute with FailureReason
                 cardElement.title = playabilityInfo.failureReason || "不可用"; // Show reason on hover
                 // console.log(`UI Highlights: Marked ${cardInstance.name} as not playable (Reason: ${playabilityInfo.failureReason}).`);
            }
        } else {
            // console.log(`UI Highlights: No cache entry for ${cardInstance.uid}. No highlight applied.`); // Debug log
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
        // console.log(`UI Update: Card element ${cardUid} not found in hand for highlight update.`);
        return; // Card might not be in hand anymore, or element not rendered yet
    }

    // console.log(`UI Update: Updating highlights for single card ${cardUid} based on:`, playabilityInfo); // Debug log

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
     // console.log(`UI Update: Final classes for ${cardUid}:`, cardElement.classList); // Debug log
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
        confirmMulliganButtonEl.disabled = localCompleted;
        confirmMulliganButtonEl.textContent = localCompleted ? '等待对方...' : '确认调度';

        endTurnButtonEl.style.display = 'none';
        endTurnButtonEl.disabled = true; // Also disable
        // console.log("UI: Phase Mulligan - Confirm Button:", confirmMulliganButtonEl.style.display, "End Turn Button: none/disabled");
    } else if (currentGameState.phase === 'TurnStart' || currentGameState.phase === 'Main') {
        // TurnStart or Main Phase: Show end turn, hide confirm. Enable/disable based on whose turn it is.
        confirmMulliganButtonEl.style.display = 'none';
        confirmMulliganButtonEl.disabled = false;
        confirmMulliganButtonEl.textContent = '确认调度';
        endTurnButtonEl.style.display = 'inline-block';
        // ADDED: Detailed log before setting disabled state
        endTurnButtonEl.disabled = !isLocalPlayerTurn; // Disable if not local player's turn
        // console.log(`UI: Phase ${currentGameState.phase} - End Turn Button Display: inline-block, Disabled: ${!isLocalPlayerTurn}`);
    } else {
        // Other phases or opponent's turn: Hide both (or just hide confirm, end turn might be handled by turn manager logic implicitly)
        confirmMulliganButtonEl.style.display = 'none';
        confirmMulliganButtonEl.disabled = false;
        confirmMulliganButtonEl.textContent = '确认调度';
        endTurnButtonEl.style.display = 'none'; // Hide end turn if not P1's main phase
        endTurnButtonEl.disabled = true; // Also disable when hidden
        // console.log("UI: Phase Other/Opponent - Confirm Button: none", "End Turn Button: none");
    }
}
// --- 显示伤害数字的函数 ---
function showDamageNumber(targetUid, damageAmount) {
    // console.log(`UI: showDamageNumber called for UID: ${targetUid}, Amount: ${damageAmount}`);
    if (!targetUid || damageAmount === undefined || damageAmount === null || damageAmount <= 0) { // Also check for non-positive damage
        // console.warn("UI: showDamageNumber called with invalid arguments (UID missing, damage invalid, or damage <= 0).");
        return;
    }

    let targetElement = null;
    // --- REVISED: Find target element, handling actual Leader Instance IDs ---
    const localPlayer = gameState.players.find(p => p.id === gameState.localPlayerId);
    const opponentPlayer = gameState.players.find(p => p.id !== gameState.localPlayerId);
    const player1LeaderUid = localPlayer?.leaderCard?.uid;
    const player2LeaderUid = opponentPlayer?.leaderCard?.uid;

    // console.log(`[DMG_NUM_DBG] Target UID: ${targetUid}, P1 Leader UID: ${player1LeaderUid}, P2 Leader UID: ${player2LeaderUid}`); // Debug Log

    if (player1LeaderUid && targetUid === player1LeaderUid) {
        targetElement = document.getElementById('player1-avatar');
        // console.log(`[DMG_NUM_DBG] Matched P1 Leader Avatar.`); // Debug Log
    } else if (player2LeaderUid && targetUid === player2LeaderUid) {
        targetElement = document.getElementById('player2-avatar');
        // console.log(`[DMG_NUM_DBG] Matched P2 Leader Avatar.`); // Debug Log
    } else {
        // Fallback for non-leader units (followers, amulets if they get UIDs later)
        targetElement = gameContainerEl?.querySelector(`[data-uid='${targetUid}']`);
        // console.log(`[DMG_NUM_DBG] Using fallback selector for UID: ${targetUid}. Found: ${!!targetElement}`); // Debug Log
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
    damageElement.textContent = `-${damageAmount}`; // Display damage as negative number

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
        // console.log(`UI: Removing damage number element for ${targetUid}`);
        if (damageElement.parentNode) {
            damageElement.remove();
        }
    }, { once: true }); // Use { once: true } for efficiency
}
// --- 事件处理 ---

// 更新 initializeUI 以确保调用事件设置 (确保 DOM 加载后执行)
function initializeUI() {
    // console.log("UI: Initializing...");
    cacheDOMElements();
    setupGlobalEventListeners(); // 确保调用
}
// (确保 initializeUI 在 DOMContentLoaded 后调用)

// 设置全局事件监听器 (事件委托模式)
function setupGlobalEventListeners() {
    if (!gameContainerEl) {
        // console.error("UI Error: Cannot setup global listeners - gameContainerEl not found.");
        return;
    }
    // console.log("UI: Setting up global click listener on game container...");
    // 先移除旧监听器，防止重复添加 (如果 initializeUI 可能被多次调用)
    gameContainerEl.removeEventListener('click', handleGameClick);
    gameContainerEl.addEventListener('click', handleGameClick);
    // console.log("UI: Global click listener attached.");
}

// 全局点击事件处理器 (事件委托)
function handleGameClick(event) {
    // console.log("UI_DBG: handleGameClick fired! Phase:", gameState.phase); // Log phase on click

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
    // console.log("UI: Not in Mulligan phase, processing standard click..."); // Added log

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
        // console.log("UI: Evolve button clicked!");
        const evolveUid = clickedEvolveButton.dataset.evolveUid; // Get UID from button's data attribute
        if (evolveUid) {
            // console.log(`UI: Calling evolveCard for UID: ${evolveUid}`);
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

    // --- Check for Act Button Click ---
    const clickedActButton = event.target.closest('.act-button');
    if (clickedActButton) {
        if (gameState.isSpectator) return;
        const actUid = clickedActButton.dataset.actUid;
        if (actUid) {
            if (typeof requestAct === 'function') {
                requestAct(actUid);
            } else {
                alert("错误：启动功能未加载！");
            }
        }
        return;
    }
    // --- END Check for Act Button ---

    const clickedCardElement = event.target.closest('.card');
    const clickedPlayer1Avatar = event.target.closest('#player1-avatar'); // Check player avatar
    const clickedPlayer2Avatar = event.target.closest('#player2-avatar'); // Check opponent avatar

    let targetUid = null;
    if (clickedCardElement) {
        targetUid = clickedCardElement.dataset.uid;
        // console.log("UI_DBG: Click identified as Card:", targetUid);
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
            // Attack/other interactions: only opponent leader is actionable.
            if (clickedPlayerActualId && clickedPlayerActualId === opponentPlayerId) {
                const opponentPlayer = getPlayerState(opponentPlayerId);
                const leaderUid = opponentPlayer?.leaderCard?.uid ?? opponentPlayer?.leaderCard?.InstanceId ?? null;
                if (leaderUid) {
                    targetUid = leaderUid;
                } else {
                    console.error(`UI Error: Could not find opponent leader card instance or its UID for player ${opponentPlayerId}`, opponentPlayer);
                }
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
        // console.log("UI: Clicked non-interactive area.");

        // Player choice mode: clicking blank area cancels only if allowed.
        if (isAwaitingPlayerChoice) {
            // Ignore clicks inside the overlay UI.
            if (event.target.closest?.('#choice-overlay-root')) return;
            console.log("UI: Clicked non-interactive area while awaiting player choice.");
            requestCancelPlayerChoiceUI();
            return;
        }

        // 1. Check if currently targeting a spell - Clicking outside cancels it
        if (gameState.selectedAttackerUid) {
            // Only cancel if click wasn't end turn or evolve button (already handled)
            if (!event.target.closest('#end-turn-button') && !event.target.closest('.evolve-button') && !event.target.closest('.super-evolve-button') && !event.target.closest('.act-button')) {
                cancelSelection(); // Call the imported gameManager function
            }
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
    // Get current player context for normal interactions
    const currentPlayerId = uiCurrentPlayerId; // Use the ID updated by renderGame

    // --- DEBUG REMOVED ---

    const playerState = getPlayerState(currentPlayerId); // Get player state object using the known ID

    // Basic check for player state validity
    if (!playerState) {
        // console.error("UI Error in handleCardClick: Could not get player state for normal interaction.");
        // Optionally alert the user or return to prevent further errors
        alert("发生内部错误：无法获取玩家状态！");
        return;
    }
    // console.log("UI: handleCardClick processing UID:", cardInstance?.uid);

    if (!cardInstance) {
        // console.error("UI: handleCardClick received null cardInstance.");
        return; // 安全检查
    }

    // <<< MOVED DEBUG LOGGING EARLIER (Before owner/zone check) >>>
    // Use optional chaining (?) in case cardInstance is unexpectedly null/undefined at this point
    // <<< END MOVED DEBUG LOGGING >>>

    // Check if the card is owned by the current player and is in their hand
    if (cardInstance.ownerId === currentPlayerId && cardInstance.zone.toLowerCase() === 'hand') {
        // NEW SIMPLIFIED LOGIC
        // 1. Check the cache. If the card isn't marked as playable, show an error and stop.
        const playabilityInfo = gameState.playabilityCache[cardInstance.uid];
        if (!playabilityInfo || !playabilityInfo.isPlayable) {
            const reason = playabilityInfo?.failureReason || "该卡牌现在无法使用。";
            console.warn(`UI: Clicked card ${cardInstance.name} (${cardInstance.uid}) is not playable. Reason: ${reason}`);
            alert(reason);
            return;
        }

        // 2. If the card IS playable, just call playCard.
        //    The server will now handle everything else. If a target is needed,
        //    the server will send a `PlayerChoiceRequested` event.
        console.log(`UI: Playable card ${cardInstance.name} (${cardInstance.uid}) clicked. Invoking playCard.`);
        playCard(cardInstance.uid);

    } // Closes if block for hand card check
    // Handle click on own field card
    else if (cardInstance.ownerId === currentPlayerId && cardInstance.zone === 'Field') {
        // Initiate Attack with Own Field Card (ONLY if not currently targeting a spell AND unit can attack)
        // Check if the unit is actually allowed to attack
        if (cardInstance.canAttack === true) {
            // console.log(`UI: Attempting to initiate attack with own field card: ${cardInstance.name} (UID: ${cardInstance.uid})`);
            initiateAttack(cardInstance.uid); // Call gameManager
        } else {
            // console.log(`UI: Clicked own field card ${cardInstance.name}, but it cannot attack (canAttack: ${cardInstance.canAttack}).`);
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
             // console.log(`UI: No attacker selected. Click on leader is non-actionable.`);
        }
    } // End of 'else if (cardInstance.zone === 'Leader')' block
    else if (cardInstance.ownerId !== currentPlayerId && cardInstance.zone === 'Field') {
        // Handle click on opponent field card (Select Attack Target OR Select Spell Target)
        // If NOT targeting a spell, proceed with attack target selection
        // console.log(`UI: Clicked opponent field card for potential attack target: ${cardInstance.name} (UID: ${cardInstance.uid})`);
        // Check if an attacker is currently selected from our side
        if (gameState.selectedAttackerUid) { // Check the imported global state
            // console.log(`UI: Attacker ${gameState.selectedAttackerUid} is selected, attempting to target ${cardInstance.uid} for attack.`);
            selectAttackTarget(cardInstance.uid); // Call the imported function
        } else {
            // console.log("UI: Clicked opponent field card, but no attacker is selected for attack.");
            // Optionally provide feedback or do nothing
        }
    }
    else { // Handle clicks on cards in other zones (deck, graveyard, etc.) - usually ignore
        // 卡牌在牌库、墓地或其他不可点击的位置/状态
        // console.log(`UI: Clicked card in unhandled zone/state: ${cardInstance.zone}`);
    }
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
    console.log("UI: showPlayerChoiceUI called with data:", choiceRequest);

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
                startPlayerChoice(choiceRequest);
                const ok = await submitPlayerChoice(choiceRequest.PendingChoiceId, [], choiceRequest.SourceAbilityRuntimeId, false);
                if (ok) endPlayerChoice(false);
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
    console.log(`UI: Player choice UI activated.`);
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
