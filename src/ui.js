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
    // handleMulligan, // Removed import - Now uses submitMulligan
    isValidSpellTarget,
    submitMulligan, // Added import for SignalR mulligan submission
    endTurnRequest,  // Added import for SignalR end turn request
    requestPlayabilityCheck // Added import for playability check function
} from './gameManager.js';
import { getCardDefinition } from './cardManager.js'; // Import card data helper
// import { endTurn } from './turnManager.js'; // Removed import - Now uses endTurnRequest
// console.log("ui.js loaded");

export { renderGame, showDamageNumber, updatePpDisplay, updateEpDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateHealthDisplay, clearSelectionHighlights, updateHandDisplay, addUnitToField, updateUnitDisplay, removeUnitFromField, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus};

// --- Specific UI Update Functions ---

/**
 * Updates the PP display for a specific player.
 * @param {string} playerId - The GUID of the player.
 * @param {number} currentPp - The current PP value.
 * @param {number} maxPp - The maximum PP value.
 */
function updatePpDisplay(playerId, currentPp, maxPp) {
    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const ppEl = isLocalPlayer ? player1PpEl : player2PpEl;
    const maxPpEl = isLocalPlayer ? player1MaxPpEl : player2MaxPpEl;

    if (ppEl) {
        ppEl.textContent = currentPp ?? '??';
    }
    if (maxPpEl) {
        maxPpEl.textContent = maxPp ?? '??';
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
    const epEl = isLocalPlayer ? player1EpEl : player2EpEl;
    const maxEpEl = isLocalPlayer ? player1MaxEpEl : player2MaxEpEl;

    if (epEl) {
        epEl.textContent = currentEp ?? '?';
    }
    if (maxEpEl) {
        maxEpEl.textContent = maxEp ?? '?';
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
    const deckCountEl = isLocalPlayer ? player1DeckCountEl : player2DeckCountEl;

    if (deckCountEl) {
        deckCountEl.textContent = count ?? '??';
    } else {
        const elName = isLocalPlayer ? 'player1DeckCountEl' : 'player2DeckCountEl';
        console.warn(`UI Update Error: ${elName} missing, cannot update Deck Count for player ${playerId}.`);
    }
}

/**
* Updates the graveyard count display for a specific player.
* @param {string} playerId - 'player1' or 'player2'.
* @param {number} count - The current graveyard count.
*/
function updateGraveyardCountDisplay(playerId, count) {
    // 【BUG修复】通过与本地玩家ID比较，正确识别UI元素
    if (!gameState.localPlayerId) {
        console.error("UI Error: localPlayerId is not set in gameState. Cannot update graveyard count correctly.");
        return;
    }

    const isLocalPlayer = (playerId === gameState.localPlayerId);
    const graveyardCountEl = isLocalPlayer ? player1GraveEl : player2GraveEl;

    if (graveyardCountEl) {
        graveyardCountEl.textContent = count ?? '??';
    } else {
        const elName = isLocalPlayer ? 'player1GraveEl' : 'player2GraveEl';
        console.warn(`UI Update Error: ${elName} missing, cannot update Graveyard Count for player ${playerId}.`);
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
        healthElement = document.getElementById(`${uiPlayerPrefix}-hp`);
    } else { // Follower or other field unit
        const unitElement = document.querySelector(`.field-card[data-uid="${unitInstance.uid}"]`);
        if (unitElement) {
            healthElement = unitElement.querySelector('.health');
        }
    }

    if (healthElement) {
        healthElement.textContent = newHealth;
        // console.log(`UI UPDATE: Updated health for ${unitInstance.name} (UID: ${unitInstance.uid}) to ${newHealth}`);
    } else {
        console.warn(`UI Update Error: Could not find health element for unit UID: ${unitInstance.uid} (Type: ${unitInstance.type})`);
    }
}
/**
 * Removes all selection and targeting highlight classes from UI elements.
 */
function clearSelectionHighlights() {
    const highlightClasses = ['playable', 'selected-attacker', 'targetable', 'targeting-active', 'targeting-spell', 'targetable-spell', 'selected-for-mulligan']; // Added mulligan class
    gameContainerEl?.classList.remove('targeting-active'); // Remove global targeting class
    document.querySelectorAll('.' + highlightClasses.join(', .'))
        .forEach(el => el.classList.remove(...highlightClasses));
    // console.log("UI Highlights: Cleared all selection/targeting highlight classes via clearSelectionHighlights()."); // Optional debug
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

    // 2. Update the local player's hand display (using existing function)
    updateHandDisplay(localPlayerId, handArray); // Render the actual cards for selection

    // 3. Add a class to the displayed hand cards to indicate they are selectable (for styling/feedback)
    player1HandEl.querySelectorAll('.card').forEach(cardEl => cardEl.classList.add('mulligan-selectable'));

    // 4. Show the Confirm Mulligan button
    confirmMulliganButtonEl.style.display = 'inline-block';
    confirmMulliganButtonEl.disabled = false; // Ensure it's enabled

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

    const potentialTargets = [...(opponentPlayerState.field ?? [])];
    const hasWard = potentialTargets.some(follower => follower?.keywords?.includes('Ward'));

    // 【BUG-017修复】总是将敌方主战者添加到潜在目标中，Ward规则在后续处理
    if (opponentPlayerState.leaderCard) {
        potentialTargets.push(opponentPlayerState.leaderCard);
    }

    console.log(`UI Highlights: Highlighting targets for attacker ${attackerInstance.uid}. Opponent: ${opponentPlayerId}. Has Ward on field: ${hasWard}. Potential targets count: ${potentialTargets.length}`); // Debug

    potentialTargets.forEach(targetInstance => {
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
let isTargetingSpell = false; // Flag for spell targeting mode
let targetingSpellUid = null; // UID of the spell being targeted
// --- UI State Management Functions ---
function startSpellTargeting(spellUid) {
    isTargetingSpell = true;
    targetingSpellUid = spellUid;
    // console.log(`UI State DBG: Started targeting spell ${spellUid}`);
}

function endSpellTargeting() {
    isTargetingSpell = false;
    targetingSpellUid = null;
    // console.log(`UI State DBG: Ended spell targeting.`);
}
// --- End UI State Management Functions ---

// --- Module-level variable to store the latest known current player ID ---
let uiCurrentPlayerId = null;

// --- DOM Element References (缓存以提高性能) ---
// 变量将在 cacheDOMElements 中定义和赋值
let player1HandEl, player1FieldEl, player1HpEl, player1PpEl, player1MaxPpEl, player1EpEl, player1DeckEl, player1GraveEl;
let player2HandEl, player2FieldEl, player2HpEl, player2PpEl, player2MaxPpEl, player2EpEl, player2DeckEl, player2GraveEl;
let player1MaxEpEl, player2MaxEpEl; // ADDED Declaration for Max EP elements
let turnInfoEl, endTurnButtonEl, confirmMulliganButtonEl, gameContainerEl; // Added confirmMulliganButtonEl

// 缓存 DOM 元素的函数
function cacheDOMElements() {
    // console.log("UI: Caching DOM elements...");
    try {
        gameContainerEl = document.getElementById('game-container');
        turnInfoEl = document.getElementById('turn-info');
        endTurnButtonEl = document.getElementById('end-turn-button');
        confirmMulliganButtonEl = document.getElementById('confirm-mulligan-button'); // Cache the new button

        // Player 1 elements
        player1HandEl = document.getElementById('player1-hand');
        player1FieldEl = document.getElementById('player1-field');
        player1HpEl = document.getElementById('player1-hp');
        player1PpEl = document.getElementById('player1-pp');
        player1MaxPpEl = document.getElementById('player1-max-pp');
        player1EpEl = document.getElementById('player1-ep');
        player1MaxEpEl = document.getElementById('player1-max-ep'); // ADDED
        player1DeckEl = document.getElementById('player1-deck');
        player1GraveEl = document.getElementById('player1-graveyard');

        // Player 2 elements
        player2HandEl = document.getElementById('player2-hand'); // Often hidden/simplified
        player2FieldEl = document.getElementById('player2-field');
        player2HpEl = document.getElementById('player2-hp');
        player2PpEl = document.getElementById('player2-pp');
        player2MaxPpEl = document.getElementById('player2-max-pp');
        player2EpEl = document.getElementById('player2-ep');
        player2MaxEpEl = document.getElementById('player2-max-ep'); // ADDED
        player2DeckEl = document.getElementById('player2-deck');
        player2GraveEl = document.getElementById('player2-graveyard');

        // Verification
        const elementsToVerify = {
            gameContainerEl, turnInfoEl, endTurnButtonEl, confirmMulliganButtonEl, // Added confirmMulliganButtonEl
            player1HandEl, player1FieldEl, player1HpEl, player1PpEl, player1MaxPpEl, player1EpEl, player1MaxEpEl, player1DeckEl, player1GraveEl, // ADDED player1MaxEpEl
            player2HandEl, player2FieldEl, player2HpEl, player2PpEl, player2MaxPpEl, player2EpEl, player2MaxEpEl, player2DeckEl, player2GraveEl // ADDED player2MaxEpEl
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

    // 方法2: 如果localPlayerId未设置或查找失败，使用IsAI属性进行区分
    if (!localPlayer || !opponentPlayerState) {
        console.warn(`[BUG修复] localPlayerId查找失败，使用IsAI属性区分玩家`);
        localPlayer = currentGameState.players.find(p => !p.isAI);  // 人类玩家
        opponentPlayerState = currentGameState.players.find(p => p.isAI);  // AI玩家
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
    renderTurnInfo(currentGameState.activePlayer?.turn, currentGameState.currentPlayerId); // Use active player's turn count

    // 5. Render Highlights (Playable cards, selected attackers, targets)
    renderHighlights(currentGameState);

    // 6. Render Action Buttons based on Game Phase
    renderActionButtons(currentGameState);

    // console.log("UI: renderGame finished."); // Optional debug log
}

// --- 具体渲染辅助函数 (占位符) ---
function renderPlayerStats(playerState, prefix) { // prefix = 'player1' or 'player2'
    if (!playerState) {
        // console.warn(`UI: Player state for ${prefix} is missing for stats rendering.`);
        return;
    }

    try {
        // Dynamically select elements based on prefix using cached variables
        const hpEl = (prefix === 'player1') ? player1HpEl : player2HpEl;
        const ppEl = (prefix === 'player1') ? player1PpEl : player2PpEl;
        const maxPpEl = (prefix === 'player1') ? player1MaxPpEl : player2MaxPpEl;
        const epEl = (prefix === 'player1') ? player1EpEl : player2EpEl;
        const deckEl = (prefix === 'player1') ? player1DeckEl : player2DeckEl;
        const graveEl = (prefix === 'player1') ? player1GraveEl : player2GraveEl;

        // Update text content only if the element was successfully cached
        if (hpEl) {
            // FIX: Use leaderCard.currentHealth for leader HP display
            hpEl.textContent = playerState.leaderCard?.currentHealth ?? '??';
        } else { /* console.warn(`UI: ${prefix}HpEl missing, cannot render HP.`); */ }

        if (ppEl) {
            ppEl.textContent = playerState.pp ?? '??';
        } else { /* console.warn(`UI: ${prefix}PpEl missing, cannot render PP.`); */ }

        if (maxPpEl) {
            maxPpEl.textContent = playerState.maxPP ?? '??';
        } else { /* console.warn(`UI: ${prefix}MaxPpEl missing, cannot render MaxPP.`); */ }

        if (epEl) {
            epEl.textContent = playerState.ep ?? '?'; // Default EP display
        } else { /* console.warn(`UI: ${prefix}EpEl missing, cannot render EP.`); */ }

        // ADDED: Update Max EP element
        const maxEpEl = (prefix === 'player1') ? player1MaxEpEl : player2MaxEpEl;
        if (maxEpEl) {
            maxEpEl.textContent = playerState.maxEP ?? '?'; // Use the correct playerState property
        } else { /* console.warn(`UI: ${prefix}MaxEpEl missing, cannot render Max EP.`); */ }
        // Use optional chaining and nullish coalescing for deck/graveyard counts
        if (deckEl) {
            deckEl.textContent = playerState.deckCount ?? 0; // FIX: Use deckCount property updated by DeckUpdate event
        } else { /* console.warn(`UI: ${prefix}DeckEl missing, cannot render Deck count.`); */ }

        if (graveEl) {
            graveEl.textContent = playerState.graveyard?.length ?? 0;
        } else { /* console.warn(`UI: ${prefix}GraveEl missing, cannot render Graveyard count.`); */ }
        // ADDED: Render First/Second Player Indicator
        const turnIndicatorId = `${prefix}-turn-indicator`;
        const turnIndicatorEl = document.getElementById(turnIndicatorId);
        if (turnIndicatorEl) {
            // Access gameState directly here since it's imported at the top
            if (gameState.firstPlayerId && gameState.secondPlayerId && playerState) {
                if (playerState.id === gameState.firstPlayerId) {
                    turnIndicatorEl.textContent = '(先手)';
                    // console.log(`UI-${prefix}: Set turn indicator to (先手)`); // Optional Debug
                } else if (playerState.id === gameState.secondPlayerId) {
                    turnIndicatorEl.textContent = '(后手)';
                    // console.log(`UI-${prefix}: Set turn indicator to (后手)`); // Optional Debug
                } else {
                    turnIndicatorEl.textContent = ''; // Clear if neither (shouldn't happen in 2-player)
                }
            } else {
                turnIndicatorEl.textContent = ''; // Clear if first/second IDs aren't set yet or playerState missing
            }
        } else {
            // console.warn(`UI: Turn indicator element #${turnIndicatorId} not found.`);
        }
        // --- END ADDED ---

    } catch (error) {
        // console.error(`UI Error rendering stats for ${prefix}:`, error);
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
        const showEvolveButton = (
            playerState && // Ensure playerState exists
            currentPlayerId === gameState.localPlayerId && // Only show button for human player's turn
            currentPlayerId === cardInstance.ownerId &&
            gameState.phase === 'Main' &&
            playerState.ep > 0 &&
            playerState.canEvolve === true &&
            cardInstance.type === 'Follower' &&
            cardInstance.isEvolved === false
        );

        if (showEvolveButton) {
            const evolveBtn = document.createElement('button');
            evolveBtn.className = 'evolve-button';
            evolveBtn.textContent = 'Evolve';
            evolveBtn.dataset.evolveUid = cardInstance.uid ?? cardInstance.InstanceId ?? cardInstance.instanceId;
            cardDiv.appendChild(evolveBtn);
        }
    }
    // --- END Evolve Button ---

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
        const displayTurn = turnNumber === 0 ? "Starting" : `Turn ${turnNumber}`;
        // Use template literals for clarity and potential future localization
        // Displaying in English for code consistency, can be localized later.
        turnInfoEl.textContent = `${displayTurn} - Player: ${currentPlayerId ?? 'N/A'}`;
    } else {
        // Log an error if the element is missing, as this indicates a UI setup issue
        console.error("UI Error: Cannot render turn info because turnInfoEl is missing or wasn't cached.");
    }
}


// --- 处理高亮 (可交互状态) 的辅助函数 ---
// --- 处理高亮 (可交互状态) 的辅助函数 --- // Keeping the comment from original line 411
function renderHighlights(currentGameState) {
    // console.log("UI Highlights: Processing highlights..."); // Optional debug
    clearAllHighlights(); // Step 1: Clear everything

    if (isTargetingSpell && targetingSpellUid) { // Condition starts here now
        renderSpellTargetHighlights(currentGameState); // Step 2a: Apply spell target highlights
    } else {
        renderPlayableCardHighlights(currentGameState); // Step 2b-i: Apply playable card highlights
        renderAttackHighlights(currentGameState);     // Step 2b-ii: Apply attack selection/target highlights
    }
}

// --- Highlight Helper Functions ---

// Helper 1: Clear All Highlight Classes
function clearAllHighlights() {
    const highlightClasses = ['playable', 'selected-attacker', 'targetable', 'targetable-restricted', 'targeting-active', 'targeting-spell', 'targetable-spell'];
    gameContainerEl?.classList.remove('targeting-active'); // Remove global targeting class
    document.querySelectorAll('.' + highlightClasses.join(', .'))
        .forEach(el => el.classList.remove(...highlightClasses));
    // console.log("UI Highlights: Cleared all highlight classes."); // Optional debug
}

// Helper 2: Render Spell Targeting Highlights
function renderSpellTargetHighlights(currentGameState) {
    // console.log("UI Highlights: Applying spell targeting highlights.");
    // Add global targeting class
    gameContainerEl?.classList.add('targeting-active');

    // Highlight the spell card itself in hand
    const spellCardElement = player1HandEl?.querySelector(`.card[data-uid="${targetingSpellUid}"]`);
    if (spellCardElement) {
        spellCardElement.classList.add('targeting-spell');
    } else {
        // console.warn(`UI Highlights: Could not find element for targeting spell UID ${targetingSpellUid} in hand.`);
    }

    // Highlight valid targets using isValidSpellTarget from gameManager
    const allPotentialTargets = [
        ...(gameState.players.find(p => p.id === gameState.localPlayerId)?.field ?? []),
        ...(gameState.players.find(p => p.id !== gameState.localPlayerId)?.field ?? []),
        gameState.players.find(p => p.id === gameState.localPlayerId)?.leaderCard,
        gameState.players.find(p => p.id !== gameState.localPlayerId)?.leaderCard
    ].filter(Boolean); // Filter out null/undefined leaderCards if not set yet

    allPotentialTargets.forEach(targetInstance => {
        // Get spell data and caster ID to check validity correctly
        const spellInstance = findCardInstanceByUid(targetingSpellUid);
        const spellData = spellInstance ? getCardDefinition(spellInstance.id) : null;
        const casterId = getCurrentPlayerId();
        if (isValidSpellTarget(targetInstance, spellData?.validTargets || [], casterId)) { // Pass correct args for highlighting
            let targetElement = null; // Initialize targetElement
            if (targetInstance.uid === 'L1') {
                targetElement = document.getElementById('player1-avatar'); // Use specific ID for Player 1 Leader
            } else if (targetInstance.uid === 'L2') {
                targetElement = document.getElementById('player2-avatar'); // Use specific ID for Player 2 Leader
            } else {
                // For non-leader cards (followers), use the original selector
                targetElement = gameContainerEl?.querySelector(`.card[data-uid="${targetInstance.uid}"]`);
            }

            if (targetElement) {
                targetElement.classList.add('targetable-spell'); // Apply highlight to the found element
                // console.log(`UI Highlights: Marked ${targetInstance.name} (UID: ${targetInstance.uid}) as targetable-spell.`);
            } else {
                // Log warning if element not found using either method
                // console.warn(`UI Highlights: Could not find element for spell target UID ${targetInstance.uid}`);
            }
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
            // console.log(`UI Highlights: Marked ${attackerElement.querySelector('.name')?.textContent} as selected-attacker.`); // Optional debug
        } else {
            // console.warn(`UI Highlights: Could not find element for selected attacker UID ${currentGameState.selectedAttackerUid}`);
        }

        // Highlight Valid Targets for the selected attacker
        // Determine opponent player ID robustly
        const currentPlayerId = getCurrentPlayerId();
        const opponentPlayerId = currentGameState.players.find(p => p.id !== currentPlayerId)?.id;
        const opponentPlayerState = getPlayerState(opponentPlayerId);

        if (opponentPlayerState) {
            const potentialTargets = [...(opponentPlayerState.field ?? [])];
            // Check for Ward keyword
            const hasWard = potentialTargets.some(follower => follower?.keywords?.includes('Ward'));
            // 【BUG-017修复】总是将敌方主战者添加到潜在目标中，Ward规则在后续处理
            if (opponentPlayerState.leaderCard) {
                potentialTargets.push(opponentPlayerState.leaderCard);
            }

            potentialTargets.forEach(targetInstance => {
                if (targetInstance) { // Check if targetInstance is not null/undefined
                    let targetElement = null; // Initialize targetElement
                    // --- REVISED FIX: Handle Leader Avatar Element Selection ---
                    // Check if the instance represents a leader (using ownerId and comparing with opponent's leader UID)
                    // Ensure opponentPlayerState and leaderCard are available before comparison
                    const isLeader = opponentPlayerState?.leaderCard && targetInstance.uid === opponentPlayerState.leaderCard.uid;

                    if (isLeader) {
                        // 【BUG-018修复】正确映射玩家ID到UI元素ID
                        // opponentPlayerId是实际的GUID，需要映射到player1-avatar或player2-avatar
                        let avatarId = null;

                        // 方法1: 通过localPlayerId确定对手是player1还是player2
                        if (gameState.localPlayerId) {
                            // 根据renderGame函数的逻辑：localPlayer映射到player1，opponent映射到player2
                            const opponentPlayer = gameState.players.find(p => p.id === opponentPlayerId);
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
                        console.log(`%c[UI_HIGHLIGHT_FIX] Identified Leader target. OpponentID: ${opponentPlayerId}, AvatarID: ${avatarId}, Found: ${!!targetElement}`, 'color: cyan;');
                    } else {
                        // It's a follower, use the original data-uid selector
                        targetElement = gameContainerEl?.querySelector(`.card[data-uid="${targetInstance.uid}"]`);
                        // console.log(`%c[UI_HIGHLIGHT_FIX] Identified Follower target. Selecting element by UID: ${targetInstance.uid}`, 'color: cyan;'); // Debug log
                    }
                    // --- END REVISED FIX ---

                    if (targetElement) {
                        // 【BUG-017修复】区分UI显示和实际攻击规则
                        // UI中总是显示所有潜在目标，Ward规则在点击时验证
                        let shouldHighlight = true; // 默认高亮所有潜在目标
                        let cssClass = 'targetable';

                        if (hasWard) {
                            // 有Ward时，Ward单位正常高亮，非Ward目标用不同样式表示受限
                            if (targetInstance.keywords?.includes('Ward')) {
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
                            // console.log(`UI Highlights: Marked ${targetInstance.name ?? targetInstance.uid} as ${cssClass}.`);
                        }
                    } else {
                        // Log warning if element not found using either method
                        console.warn(`%c[UI_HIGHLIGHT_FIX] Could not find target element for UID ${targetInstance.uid} (Is Leader: ${isLeader})`, 'color: orange;');
                    }
                }
            });
        } else {
            // console.warn(`UI Highlights: Could not find opponent player state for ID: ${opponentPlayerId}`);
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
        // Mulligan Phase: Show confirm button (only if local player hasn't confirmed), hide end turn
        // Assuming player1MulliganComplete refers to the local player for now
        confirmMulliganButtonEl.style.display = currentGameState.player1MulliganComplete ? 'none' : 'inline-block';
        endTurnButtonEl.style.display = 'none';
        endTurnButtonEl.disabled = true; // Also disable
        // console.log("UI: Phase Mulligan - Confirm Button:", confirmMulliganButtonEl.style.display, "End Turn Button: none/disabled");
    } else if (currentGameState.phase === 'TurnStart' || currentGameState.phase === 'Main') {
        // TurnStart or Main Phase: Show end turn, hide confirm. Enable/disable based on whose turn it is.
        confirmMulliganButtonEl.style.display = 'none';
        endTurnButtonEl.style.display = 'inline-block';
        // ADDED: Detailed log before setting disabled state
        endTurnButtonEl.disabled = !isLocalPlayerTurn; // Disable if not local player's turn
        // console.log(`UI: Phase ${currentGameState.phase} - End Turn Button Display: inline-block, Disabled: ${!isLocalPlayerTurn}`);
    } else {
        // Other phases or opponent's turn: Hide both (or just hide confirm, end turn might be handled by turn manager logic implicitly)
        confirmMulliganButtonEl.style.display = 'none';
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

    // --- Mulligan Phase Logic ---
    if (gameState.phase === 'Mulligan') {
        const clickedMulliganConfirm = event.target.closest('#confirm-mulligan-button');
        const clickedHandCard = event.target.closest('#player1-hand .card'); // Only care about player 1 hand

        if (clickedMulliganConfirm) {
            // Gather selected card UIDs
            const selectedCards = player1HandEl.querySelectorAll('.card.selected-for-mulligan');
            const selectedUids = Array.from(selectedCards).map(card => card.dataset.uid);

            // Call submitMulligan (the imported SignalR wrapper) from gameManager
            if (typeof submitMulligan === 'function') { // <<< FIXED: Check for the correct imported function 'submitMulligan'
                submitMulligan(selectedUids); // Call new SignalR wrapper
            } else {
                // console.error("UI Error: submitMulligan function is not available/imported!"); // Log the correct function name in error
                alert("错误：调度功能未加载！(submitMulligan)"); // Clarify alert slightly for debugging
            }
            // Note: No need to manually remove 'selected-for-mulligan' class here,
            // as the next renderGame call after handleMulligan will redraw the hand.
            return; // Stop processing click after handling mulligan confirm

        } else if (clickedHandCard) {
            // Toggle selection class on the clicked hand card
            clickedHandCard.classList.toggle('selected-for-mulligan');
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

        // 检查是否点击的是对手头像
        if (clickedPlayerActualId && clickedPlayerActualId === opponentPlayerId) {
             const opponentPlayer = getPlayerState(opponentPlayerId);
             if (opponentPlayer && opponentPlayer.leaderCard && opponentPlayer.leaderCard.uid) {
                 targetUid = opponentPlayer.leaderCard.uid;
             } else {
                  console.error(`UI Error: Could not find opponent leader card instance or its UID for player ${opponentPlayerId}`, opponentPlayer);
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

        // 1. Check if currently targeting a spell - Clicking outside cancels it
        if (isTargetingSpell) {
            // console.log("UI: Clicked non-interactive area while targeting a spell. Cancelling targeting.");
            endSpellTargeting(); // Use helper function
            renderGame(gameState); // Re-render to remove highlights
        }
        // 2. If not targeting a spell, check if an attacker is selected - Clicking outside cancels attack
        else if (gameState.selectedAttackerUid) {
            // Only cancel if click wasn't end turn or evolve button (already handled)
            if (!event.target.closest('#end-turn-button') && !event.target.closest('.evolve-button')) {
                cancelSelection(); // Call the imported gameManager function
            }
        }
        // 3. If neither targeting nor attacking, clicking non-interactive does nothing
        // else { }
    }
} // This brace correctly closes the handleGameClick function started on line 392


// 具体处理卡牌点击的逻辑 (由 handleGameClick 在找到实例后调用)
function handleCardClick(cardInstance) { // 需要传入卡牌实例
    // --- Spell Target Selection Logic ---
    if (isTargetingSpell) {
        // console.log(`UI: In targeting mode (Spell: ${targetingSpellUid}). Clicked card UID: ${cardInstance.uid}`);
        // Check if clicked a field card (potential target)
        if (cardInstance.zone === 'Field') {
            const targetElement = gameContainerEl?.querySelector(`.card[data-uid="${cardInstance.uid}"]`);
            // Check if the clicked element is marked as a valid spell target
            // Placeholder check: Replace with actual class check later after renderHighlights update
            // Get spell data and caster ID to check validity correctly when clicking target
            const spellInstance = findCardInstanceByUid(targetingSpellUid);
            const spellData = spellInstance ? getCardDefinition(spellInstance.id) : null;
            const casterId = getCurrentPlayerId();
            // Check validity using the correct arguments (target instance, spell rules, caster)
            if (isValidSpellTarget(cardInstance, spellData?.validTargets || [], casterId)) {
                // console.log(`UI: Spell target selected (Field): ${cardInstance.name} (UID: ${cardInstance.uid})`);
                const currentTargetingSpellUid = targetingSpellUid; // Store UID before resetting
                endSpellTargeting(); // Use helper function
                // Let playCard handle the render update
                playCard(currentTargetingSpellUid, cardInstance.uid); // Call gameManager AFTER resetting UI state, pass stored UID
            } else {
                // console.log("UI: Clicked field card is not a valid target for the current spell.");
                // Do nothing, allow user to click another target or cancel
            }
        } else if (cardInstance.zone === 'Hand' && cardInstance.uid === targetingSpellUid) {
            // Clicked the spell card itself again while targeting - CANCEL
            // console.log("UI: Clicked targeting spell card again. Cancelling targeting.");
            endSpellTargeting(); // Use helper function
            renderGame(gameState); // Re-render to remove highlights
        } else if (cardInstance.zone === 'LeaderZone') {
            // --- Potential Spell Target Clicked (Leader) ---
            // Get spell data and caster ID to check validity
            const spellInstance = findCardInstanceByUid(targetingSpellUid);
            const spellData = spellInstance ? getCardDefinition(spellInstance.id) : null;
            const casterId = getCurrentPlayerId();
            // Check validity
            if (isValidSpellTarget(cardInstance, spellData?.validTargets || [], casterId)) {
                // console.log(`UI: Spell target selected (Leader): ${cardInstance.name} (UID: ${cardInstance.uid}). Playing card.`);
                const currentTargetingSpellUid = targetingSpellUid; // Store UID before resetting
                endSpellTargeting(); // Use helper function
                // Render will happen via gameManager callback
                playCard(currentTargetingSpellUid, cardInstance.uid); // Play spell targeting the leader AFTER resetting UI state, pass stored UID
            } else {
                // console.log(`UI: Clicked leader ${cardInstance.uid} is NOT a valid target for spell ${targetingSpellUid}.`);
                // Do nothing, allow re-selection or cancellation
            }
        } else {
            // Clicked an irrelevant card (e.g., opponent hand, own hand but not the spell, etc.)
            // console.log("UI: Clicked irrelevant card during spell targeting.");
            // Do nothing, wait for valid target click or cancellation
        }
        // REMOVED premature return; Fall through to allow other logic if necessary,
        // though typically spell selection should conclude the interaction within this block.
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
        console.log(`UI: Clicked on hand card ${cardInstance.uid}. Checking playability cache.`);
        const playabilityInfo = gameState.playabilityCache[cardInstance.uid];

        // 【修复】如果缓存未命中，说明服务器还未推送可玩性信息
        if (!playabilityInfo) {
            console.log(`UI: No cache entry for ${cardInstance.name} (${cardInstance.uid}). Server has not yet pushed playability info.`);
            // 给用户友好的提示，不再主动请求（服务器会自动推送）
            alert("卡牌可用性信息尚未就绪，请稍后再试。");
            return; // 等待服务器推送更新
        }

        if (!playabilityInfo.isPlayable) {
            console.warn(`UI: Card ${cardInstance.name} (${cardInstance.uid}) is not playable. Reason: ${playabilityInfo.failureReason}`);
            alert(playabilityInfo.failureReason || "该卡牌现在无法使用。"); // Show reason from cache or generic message
            return; // Stop processing if not playable
        }

        // Card is playable, check if it requires a target
        if (playabilityInfo.requiresTargetSelection) {
            if (playabilityInfo.hasValidTargets) {
                console.log(`UI: Card ${cardInstance.name} requires target. Entering targeting mode.`);
                startSpellTargeting(cardInstance.uid); // Enter targeting mode (reuse spell targeting flag/logic)
                // Highlight the source card and potential targets based on ValidTargetIds from cache
                clearSelectionHighlights();
                // --- Highlight Source Card ---
                const sourceCardElement = player1HandEl?.querySelector(`.card[data-uid="${cardInstance.uid}"]`);
                if (sourceCardElement) sourceCardElement.classList.add('targeting-spell'); // Reuse class
                // --- Highlight Valid Targets ---
                playabilityInfo.validTargetIds?.forEach(targetUid => {
                    // Find target element (could be field card or leader avatar)
                    let targetElement = document.querySelector(`.card[data-uid="${targetUid}"]`); // Try finding card first
                    if (!targetElement) {
                         // If not found as card, check if it's a leader UID
                         const localPlayer = getPlayerState(gameState.localPlayerId);
                         const opponentPlayer = gameState.players.find(p => p.id !== gameState.localPlayerId);
                         const player1LeaderUid = localPlayer?.leaderCard?.uid;
                         const player2LeaderUid = opponentPlayer?.leaderCard?.uid;
                         if (targetUid === player1LeaderUid) {
                             targetElement = document.getElementById('player1-avatar');
                         } else if (targetUid === player2LeaderUid) {
                             targetElement = document.getElementById('player2-avatar');
                         }
                    }

                    if (targetElement) {
                        targetElement.classList.add('targetable-spell'); // Reuse class
                    } else {
                         console.warn(`UI Highlights: Could not find element for target UID ${targetUid} during targeting initiation.`);
                    }
                });
                gameContainerEl?.classList.add('targeting-active'); // Add global indicator
                // IMPORTANT: Do NOT call playCard here yet! Wait for target selection click.
            } else {
                 // Requires target, but none available
                 if (playabilityInfo.canPlayWithoutTarget) {
                     console.log(`UI: Card ${cardInstance.name} requires target, none available, but can play without target. Playing directly.`);
                     playCard(cardInstance.uid, null); // Play without target UID
                 } else {
                     console.warn(`UI: Card ${cardInstance.name} requires target, but no valid targets available and cannot play without target.`);
                     alert("该卡牌需要目标，但当前没有可用的目标。");
                 }
            }
        } else {
            // Card is playable and does not require a target, play directly
            console.log(`UI: Card ${cardInstance.name} is playable and does not require target. Playing directly.`);
            playCard(cardInstance.uid); // Call gameManager function without target
        }
    } // Closes if block for hand card check
    // Handle click on own field card
    else if (cardInstance.ownerId === currentPlayerId && cardInstance.zone === 'Field') {
        // Initiate Attack with Own Field Card (ONLY if not currently targeting a spell AND unit can attack)
        if (!isTargetingSpell) {
            // Check if the unit is actually allowed to attack
            if (cardInstance.canAttack === true) {
                // console.log(`UI: Attempting to initiate attack with own field card: ${cardInstance.name} (UID: ${cardInstance.uid})`);
                initiateAttack(cardInstance.uid); // Call gameManager
            } else {
                // console.log(`UI: Clicked own field card ${cardInstance.name}, but it cannot attack (canAttack: ${cardInstance.canAttack}).`);
                // Optionally provide user feedback (e.g., a slight visual shake or message)
                // For now, just ignore the click.
            }
        } else {
            // console.log("UI: Cannot initiate attack while targeting a spell.");
            // Ignore click while targeting spell.
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
        if (isTargetingSpell) {
            // If targeting a spell, check if this is a valid target
            // Get spell data to find valid targets
            const spellInstance = findCardInstanceByUid(targetingSpellUid);
            const spellData = spellInstance ? getCardDefinition(spellInstance.id) : null;
            const casterId = getCurrentPlayerId(); // Get the ID of the player casting the spell
            // Check validity using the correct arguments
            if (isValidSpellTarget(cardInstance, spellData?.validTargets || [], casterId)) { // Pass target instance, spell's valid targets, and caster ID
                // console.log(`UI: Spell target selected (Opponent Field): ${cardInstance.name} (UID: ${cardInstance.uid})`);
                const currentTargetingSpellUid = targetingSpellUid; // Store UID before resetting
                endSpellTargeting(); // Use helper function
                // Render will happen via gameManager callback
                playCard(currentTargetingSpellUid, cardInstance.uid); // Call gameManager with spell and target AFTER resetting UI state, pass stored UID
            } else {
                // console.log("UI: Clicked opponent field card is not a valid target for the current spell.");
                // Do nothing, allow user to click another target or cancel
            }
        } else {
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
    }
    else { // Handle clicks on cards in other zones (deck, graveyard, etc.) - usually ignore
        // 卡牌在牌库、墓地或其他不可点击的位置/状态
        // console.log(`UI: Clicked card in unhandled zone/state: ${cardInstance.zone}`);
    }
}