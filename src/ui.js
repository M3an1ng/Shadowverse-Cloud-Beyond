// src/ui.js - UI渲染与交互处理

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
    endTurnRequest  // Added import for SignalR end turn request
} from './gameManager.js';
import { getCardDefinition } from './cardManager.js'; // Import card data helper
// import { endTurn } from './turnManager.js'; // Removed import - Now uses endTurnRequest
// console.log("ui.js loaded");

export { renderGame, showDamageNumber, updatePpDisplay, updateEpDisplay, updateDeckCountDisplay, updateGraveyardCountDisplay, updateHealthDisplay, clearSelectionHighlights, updateHandDisplay, addUnitToField, updateUnitDisplay, removeUnitFromField, highlightAttacker, highlightTargets, showMulliganInterface, updateMulliganStatus }; // Added updateMulliganStatus

// --- Specific UI Update Functions ---

/**
 * Updates the PP display for a specific player.
 * @param {string} playerId - 'player1' or 'player2'.
 * @param {number} currentPp - The current PP value.
 * @param {number} maxPp - The maximum PP value.
 */
function updatePpDisplay(playerId, currentPp, maxPp) {
    const ppEl = (playerId === 'player1') ? player1PpEl : player2PpEl;
    const maxPpEl = (playerId === 'player1') ? player1MaxPpEl : player2MaxPpEl;

    if (ppEl) {
        ppEl.textContent = currentPp ?? '??';
    } else {
        console.warn(`UI Update Error: ${playerId}PpEl missing, cannot update PP.`);
    }
    if (maxPpEl) {
        maxPpEl.textContent = maxPp ?? '??';
    } else {
        console.warn(`UI Update Error: ${playerId}MaxPpEl missing, cannot update Max PP.`);
    }
    // console.log(`UI UPDATE: Updated ${playerId} PP display to ${currentPp}/${maxPp}`); // Optional Debug
}

/**
 * Updates the EP display for a specific player.
 * @param {string} playerId - 'player1' or 'player2'.
 * @param {number} currentEp - The current EP value.
 * @param {number} maxEp - The maximum EP value.
 */
function updateEpDisplay(playerId, currentEp, maxEp) {
    const epEl = (playerId === 'player1') ? player1EpEl : player2EpEl;
    const maxEpEl = (playerId === 'player1') ? player1MaxEpEl : player2MaxEpEl;

    if (epEl) {
        epEl.textContent = currentEp ?? '?';
    } else {
        console.warn(`UI Update Error: ${playerId}EpEl missing, cannot update EP.`);
    }
    if (maxEpEl) {
        maxEpEl.textContent = maxEp ?? '?';
    } else {
        console.warn(`UI Update Error: ${playerId}MaxEpEl missing, cannot update Max EP.`);
    }
    // console.log(`UI UPDATE: Updated ${playerId} EP display to ${currentEp}/${maxEp}`); // Optional Debug
}

// --- End Specific UI Update Functions ---

/**
* Updates the deck count display for a specific player.
* @param {string} playerId - 'player1' or 'player2'.
* @param {number} count - The current deck count.
*/
function updateDeckCountDisplay(playerId, count) {
   const deckCountEl = (playerId === 'player1') ? player1DeckCountEl : player2DeckCountEl;

   if (deckCountEl) {
       deckCountEl.textContent = count ?? '??';
   } else {
       console.warn(`UI Update Error: ${playerId}DeckCountEl missing, cannot update Deck Count.`);
   }
   // console.log(`UI UPDATE: Updated ${playerId} Deck Count display to ${count}`); // Optional Debug
}

/**
* Updates the graveyard count display for a specific player.
* @param {string} playerId - 'player1' or 'player2'.
* @param {number} count - The current graveyard count.
*/
function updateGraveyardCountDisplay(playerId, count) {
   const graveyardCountEl = (playerId === 'player1') ? player1GraveyardCountEl : player2GraveyardCountEl;

   if (graveyardCountEl) {
       graveyardCountEl.textContent = count ?? '??';
   } else {
       console.warn(`UI Update Error: ${playerId}GraveyardCountEl missing, cannot update Graveyard Count.`);
   }
   // console.log(`UI UPDATE: Updated ${playerId} Graveyard Count display to ${count}`); // Optional Debug
}

/**
 * Updates the health display for a specific player's leader.
 * @param {string} playerId - 'player1' or 'player2'.
 * @param {number} currentHealth - The current health value of the leader.
 */
function updateHealthDisplay(playerId, currentHealth) {
    const hpEl = (playerId === 'player1') ? player1HpEl : player2HpEl;

    if (hpEl) {
        hpEl.textContent = currentHealth ?? '??';
    } else {
        console.warn(`UI Update Error: ${playerId}HpEl missing, cannot update Health display.`);
    }
    // console.log(`UI UPDATE: Updated ${playerId} Health display to ${currentHealth}`); // Optional Debug
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
 * @param {string} playerId - 'player1' or 'player2'.
 * @param {Array<object>} handCards - Array of card instance objects in the hand.
 */
function updateHandDisplay(playerId, handCards = []) {
    const handElement = (playerId === 'player1') ? player1HandEl : player2HandEl;
    // console.log(`UI UPDATE: Updating ${playerId} hand display with ${handCards.length} cards.`); // Optional Debug

    if (!handElement) {
        console.warn(`UI Update Error: ${playerId}HandEl missing, cannot update hand display.`);
        return;
    }

    // Clear existing hand display
    handElement.innerHTML = '';

    // Determine if it's the opponent's hand for rendering card backs
    const isOpponent = (playerId !== gameState.localPlayerId); // Use localPlayerId for comparison

    // Iterate through hand card instances
    if (!Array.isArray(handCards)) {
        console.warn(`UI Update Warning: handCards for ${playerId} is not an array in updateHandDisplay.`);
        handCards = []; // Ensure it's an array
    }

    handCards.forEach(cardInstance => {
        if (!cardInstance) {
            console.warn(`UI Update Warning: null or undefined card instance found in ${playerId}'s handCards array.`);
            return; // Skip this iteration
        }

        if (isOpponent) {
            // Render card back for opponent
            const cardBackDiv = document.createElement('div');
            cardBackDiv.classList.add('card-back');
            handElement.appendChild(cardBackDiv);
        } else {
            // Render full card for local player
            const cardElement = createCardElement(cardInstance, 'hand');
            if (cardElement) {
                handElement.appendChild(cardElement);
            } else {
                console.warn(`UI Update Warning: Failed to create element for card instance in ${playerId}'s hand:`, cardInstance);
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
 * @param {string} playerId - 'player1' or 'player2'.
 * @param {object} cardInstance - The card instance object to add to the field.
 */
function addUnitToField(playerId, cardInstance) {
    const fieldElement = (playerId === 'player1') ? player1FieldEl : player2FieldEl;
    // console.log(`UI UPDATE: Adding unit ${cardInstance?.name} (UID: ${cardInstance?.uid}) to ${playerId}'s field.`); // Optional Debug

    if (!fieldElement) {
        console.warn(`UI Update Error: ${playerId}FieldEl missing, cannot add unit to field.`);
        return;
    }
    if (!cardInstance) {
        console.warn(`UI Update Error: cardInstance is null or undefined in addUnitToField for ${playerId}.`);
        return;
    }

    // Create the card's DOM element using the helper, specifying 'field' context
    const cardElement = createCardElement(cardInstance, 'field');

    if (cardElement) {
        // Append the created card element to the field container
        fieldElement.appendChild(cardElement);
        // console.log(`UI UPDATE: Successfully added unit ${cardInstance.name} to ${playerId}'s field element.`); // Optional Debug
    } else {
        // Log if element creation failed
        console.warn(`UI Update Warning: Failed to create element for card instance on field (Player: ${playerId}):`, cardInstance);
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
    if (healthEl) {
        healthEl.textContent = unitInstance.currentHealth ?? '?';
    } else {
        console.warn(`UI Update Warning: Could not find .health element for unit UID: ${unitInstance.uid}`);
    }

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

    if (!hasWard && opponentPlayerState.leaderCard) {
        potentialTargets.push(opponentPlayerState.leaderCard);
    }

    console.log(`UI Highlights: Highlighting targets for attacker ${attackerInstance.uid}. Opponent: ${opponentPlayerId}. Has Ward on field: ${hasWard}. Potential targets count: ${potentialTargets.length}`); // Debug

    potentialTargets.forEach(targetInstance => {
        if (!targetInstance) return; // Skip null/undefined

        let targetElement = null;
        const isLeader = opponentPlayerState?.leaderCard && targetInstance.uid === opponentPlayerState.leaderCard.uid;

        if (isLeader) {
            const avatarId = `${opponentPlayerId}-avatar`;
            targetElement = document.getElementById(avatarId);
        } else {
            targetElement = gameContainerEl?.querySelector(`.card[data-uid="${targetInstance.uid}"]`);
        }

        if (targetElement) {
            let canTarget = false;
            if (hasWard) {
                // Only target units with Ward if Ward exists
                if (!isLeader && targetInstance.keywords?.includes('Ward')) {
                    canTarget = true;
                }
            } else {
                // Target anything if no Ward
                canTarget = true;
            }

            if (canTarget) {
                targetElement.classList.add('targetable');
                console.log(`UI Highlights: Marked ${targetInstance.name ?? targetInstance.uid} (Leader: ${isLeader}) as targetable.`); // Optional debug
            } else {
                 console.log(`UI Highlights: SKIPPED targeting ${targetInstance.name ?? targetInstance.uid} due to Ward rule (hasWard=${hasWard}, targetHasWard=${targetInstance.keywords?.includes('Ward')})`); // Optional debug
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
    console.log(`%c[UI_RENDER] Updated uiCurrentPlayerId to: '${uiCurrentPlayerId}' from currentGameState`, 'color: purple; font-weight: bold;'); // Add confirmation log
    // --- ROO DEBUG: Log entry and state ---
    const currentTurnForLog = currentGameState?.activePlayer?.turn ?? 'N/A';
    const playersLengthForLog = currentGameState?.players?.length ?? 'N/A';
    //console.log(`%c[DEBUG_RENDER_GAME_ENTRY] renderGame called. gameState:`, 'color: green; font-weight: bold;', currentGameState);
    // --- END ROO DEBUG ---
    console.log(`UI_RENDER_TRACE: --- renderGame START (Known Player ID: ${uiCurrentPlayerId}) ---`); // <<< ADDED TRACE LOG & ID
    // console.log("UI Received Game State:", JSON.stringify(currentGameState, null, 2)); // 打印接收到的完整状态
    // console.log("UI: renderGame called");
    if (!currentGameState || !currentGameState.players || currentGameState.players.length < 2) {
        // 游戏状态无效，可能在初始化阶段，或者出错
        // --- ROO DEBUG: Log early return ---
        console.warn(`%c[DEBUG_RENDER_GAME_CHECKS] renderGame returning early: Invalid gameState or insufficient players. State: ${!!currentGameState}, Players: ${currentGameState?.players}, Length: ${currentGameState?.players?.length}`, 'color: orange;');
        // --- END ROO DEBUG ---
        // console.warn("UI: renderGame called with invalid or incomplete gameState.");
        // 可以考虑显示“加载中”或错误信息
        return;
    }

    // 假设 gameManager.gameState.players 存储了 player 对象
    const player1 = currentGameState.players.find(p => p && p.id === 'player1'); // FIX: Use p.id
    const player2 = currentGameState.players.find(p => p && p.id === 'player2'); // FIX: Use p.id

    if (!player1 || !player2) {
        // --- ROO DEBUG: Log early return ---
        console.error(`%c[DEBUG_RENDER_GAME_CHECKS] renderGame returning early: Could not find player1 or player2 object. Found P1: ${!!player1}, Found P2: ${!!player2}`, 'color: red; font-weight: bold;');
        // --- END ROO DEBUG ---
        // console.error("UI: Could not find player objects in gameState for rendering.");
        return;
    }

    // Call helper functions to render specific parts of the UI using game state and cached elements

    // 1. Render Player Stats
    renderPlayerStats(player1, 'player1');
    renderPlayerStats(player2, 'player2');

    // 2. Render Hands (Ensure hand array exists, fallback to empty array)
    renderPlayerHand(player1.hand ?? [], player1HandEl);
    // Note: Typically opponent's hand (player2HandEl) isn't fully rendered.
    // We might render card backs or just a count later. For now, render if needed for debugging.
    // Render opponent's hand using card backs
    renderOpponentHand(player2.hand ?? [], player2HandEl); // NEW Call

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
    // console.log(`UI Rendering field. Opponent: ${isOpponent}, Cards: ${fieldCards.length}`); // Noisy log
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
    if (cardInstance.type) {
        // 将类型转为小写，例如 'Follower' -> 'follower'
        cardDiv.classList.add(cardInstance.type.toLowerCase().split('_')[0]); // Handle 'Follower_Evolved' -> 'follower'
    }
    cardDiv.dataset.uid = cardInstance.uid; // Store UID for event handling & state lookup

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
    cardDiv.innerHTML = `
        <div class="card-art"></div> <!-- 卡面美术图占位 -->
        <div class="cost">${cardInstance.cost ?? '?'}</div>
        <div class="name">${cardInstance.name ?? 'Unknown Card'}</div>
        ${
        // 仅为随从显示攻击力和生命值区域
        (cardInstance.type === 'Follower' || cardInstance.type === 'Follower_Evolved') ?
            // Use baseAttack and baseHealth from the DTO received from server
            `<div class="stats">
                <span class="attack">${cardInstance.currentAttack ?? '?'}</span>/<span class="health">${cardInstance.currentHealth ?? '?'}</span>
            </div>`
            : '' // 法术和护符不显示 stats 区域
        }
        <div class="description">${cardInstance.description ?? ''}</div> <!-- 卡牌描述区域 -->
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
            currentPlayerId === 'player1' && // Only show button for human player's turn
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
            evolveBtn.dataset.evolveUid = cardInstance.instanceId || cardInstance.uid;
            cardDiv.appendChild(evolveBtn);
        }
    }
    // --- END Evolve Button ---
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
    const highlightClasses = ['playable', 'selected-attacker', 'targetable', 'targeting-active', 'targeting-spell', 'targetable-spell'];
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
        ...(getPlayerState('player1')?.field ?? []),
        ...(getPlayerState('player2')?.field ?? []),
        getPlayerState('player1')?.leaderCard, // Assuming leaderCard object exists
        getPlayerState('player2')?.leaderCard
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

// Helper 3: Render Playable Card Highlights (in hand) - ONLY for local player
function renderPlayableCardHighlights(currentGameState) {
    const currentPlayerId = getCurrentPlayerId();
    // <<< FIX: Only highlight if it's the LOCAL player's turn >>>
    // Use gameState.localPlayerId which should be set during initialization
    if (currentPlayerId !== gameState.localPlayerId) {
        // console.log("UI Highlights: Skipping playable card highlights (not local player's turn)."); // Optional debug
        return; // Do nothing if it's not the local player's turn
    }
    // <<< END FIX >>>

    const playerState = getPlayerState(currentPlayerId); // Now guaranteed to be local player
    // <<< FIX: Check local player's hand element (player1HandEl assumed to be correct) >>>
    // Ensure the correct hand element and player state are available
    if (!playerState || !player1HandEl) {
        console.warn("UI Highlights Error: Missing local player state or hand element for highlights.");
        return;
    }
    // <<< END FIX >>>

    playerState.hand.forEach(cardInstance => {
        if (!cardInstance) return; // Skip if null
        // Check if the card is playable (based on PP cost AND Game Phase) // MODIFIED comment
        if (currentGameState.phase === 'Main' && cardInstance.cost <= playerState.pp) { // MODIFIED condition
            const cardElement = player1HandEl.querySelector(`.card[data-uid="${cardInstance.uid}"]`);
            if (cardElement) {
                cardElement.classList.add('playable');
                // console.log(`UI Highlights: Marked ${cardInstance.name} as playable.`); // Optional debug
            }
        }
    });
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
            // Add opponent leader if not hidden by Ward followers
            const hasWard = potentialTargets.some(follower => follower?.keywords?.includes('Ward')); // Check for Ward keyword
            if (!hasWard && opponentPlayerState.leaderCard) { // Check if leader exists
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
                         // It's the leader, select the avatar based on the opponent's ID
                         const avatarId = `${opponentPlayerId}-avatar`; // Construct ID like 'player1-avatar' or 'player2-avatar'
                         targetElement = document.getElementById(avatarId);
                         console.log(`%c[UI_HIGHLIGHT_FIX] Identified Leader target. Selecting element by ID: #${avatarId}`, 'color: cyan;'); // Debug log
                    } else {
                        // It's a follower, use the original data-uid selector
                        targetElement = gameContainerEl?.querySelector(`.card[data-uid="${targetInstance.uid}"]`);
                        // console.log(`%c[UI_HIGHLIGHT_FIX] Identified Follower target. Selecting element by UID: ${targetInstance.uid}`, 'color: cyan;'); // Debug log
                    }
                    // --- END REVISED FIX ---

                    if (targetElement) {
                        // --- ADDED WARD CHECK ---
                        let canTarget = false;
                        if (hasWard) {
                            // If Ward exists on board, ONLY target units that HAVE Ward
                            if (targetInstance.keywords?.includes('Ward')) {
                                canTarget = true;
                            }
                        } else {
                            // If no Ward on board, any potential target is valid
                            canTarget = true;
                        }
                        // --- END WARD CHECK ---

                        if (canTarget) { // Apply class only if targetable based on Ward rule
                            targetElement.classList.add('targetable');
                            // console.log(`UI Highlights: Marked ${targetInstance.name ?? targetInstance.uid} as targetable.`);
                        } else {
                             // Optional: Log why it wasn't targeted
                             // console.log(`UI Highlights: SKIPPED targeting ${targetInstance.name ?? targetInstance.uid} due to Ward rule (hasWard=${hasWard}, targetHasWard=${targetInstance.keywords?.includes('Ward')})`);
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
        console.log(`UI_RENDER_BUTTONS: Comparing IDs for button state. LocalID='${currentGameState.localPlayerId}', CurrentPlayerID='${currentGameState.currentPlayerId}', isLocalPlayerTurn=${isLocalPlayerTurn}`);
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
    const player1LeaderUid = gameState?.players?.find(p => p.id === 'player1')?.leaderCard?.uid;
    const player2LeaderUid = gameState?.players?.find(p => p.id === 'player2')?.leaderCard?.uid;

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
        const timestamp = new Date().toISOString();
        console.log(`[${timestamp}] UI_DBG: End Turn button detected.`);

        // Correctly check for the imported endTurnRequest function
        if (typeof endTurnRequest === 'function') {
            const isDisabledBefore = endTurnButtonEl.disabled;
            console.log(`[${timestamp}] UI_DBG: End Turn button check passed. BEFORE disabling: button.disabled = ${isDisabledBefore}`);

            endTurnButtonEl.disabled = true; // Disable button immediately on click
            console.log(`[${timestamp}] UI_DBG: End Turn button AFTER disabling: button.disabled = ${endTurnButtonEl.disabled}`);

            endTurnRequest(); // Call new SignalR wrapper
            console.log(`[${timestamp}] UI_DBG: Called endTurnRequest().`);
        } else {
            // This error should ideally not happen if imports are correct, but keep for safety
            console.error(`[${timestamp}] UI Error: endTurnRequest function is not available/imported!`);
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
        const clickedPlayerId = clickedPlayer1Avatar ? 'player1' : 'player2';
        console.log(`%c[BUG-017] Clicked Avatar for player: ${clickedPlayerId}`, 'color: blue; font-weight: bold;');
        // Determine opponent's ID based on local player ID
        const opponentPlayerId = gameState.players.find(p => p.id !== gameState.localPlayerId)?.id;
        console.log(`%c[BUG-017] Determined Opponent Player ID: ${opponentPlayerId}`, 'color: blue; font-weight: bold;');

        // Check if the clicked avatar belongs to the opponent
        if (opponentPlayerId && clickedPlayerId === opponentPlayerId) {
             const opponentPlayer = getPlayerState(opponentPlayerId); // Use imported helper
             if (opponentPlayer && opponentPlayer.leaderCard && opponentPlayer.leaderCard.uid) {
                 targetUid = opponentPlayer.leaderCard.uid; // Use the actual leader instance UID (Guid)
                 console.log(`%c[BUG-017] Found opponent leader UID: ${targetUid} for player ${opponentPlayerId}`, 'color: blue; font-weight: bold;');
             } else {
                  console.error(`%c[BUG-017] Could not find opponent leader card instance or its UID for player ${opponentPlayerId} in handleGameClick. State:`, 'color: red; font-weight: bold;', opponentPlayer);
             }
        } else {
             console.log(`%c[BUG-017] Clicked own avatar or opponent ID not found. Clicked: ${clickedPlayerId}, Opponent: ${opponentPlayerId}`, 'color: orange;');
             // If own avatar is clicked, targetUid remains null, no action needed here for attack targeting.
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
        if (cardInstance.location === 'field') {
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
        } else if (cardInstance.location === 'hand' && cardInstance.uid === targetingSpellUid) {
            // Clicked the spell card itself again while targeting - CANCEL
            // console.log("UI: Clicked targeting spell card again. Cancelling targeting.");
            endSpellTargeting(); // Use helper function
            renderGame(gameState); // Re-render to remove highlights
        } else if (cardInstance.location === 'leader_zone') {
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

    // <<< MOVED DEBUG LOGGING EARLIER (Before owner/location check) >>>
    // Use optional chaining (?) in case cardInstance is unexpectedly null/undefined at this point
    console.log(`UI_CLICK_TARGET_DBG: Clicked Card Instance: UID=${cardInstance?.uid}, Name=${cardInstance?.name}, Owner=${cardInstance?.ownerId}, Location=${cardInstance?.location}`);
    console.log(`UI_CLICK_TARGET_DBG: Known Player ID at click time (from uiCurrentPlayerId): ${currentPlayerId}`); // Use known ID in log
    console.log(`UI_CLICK_TARGET_DBG: Game State Phase at click time: ${gameState?.phase}`);
    // <<< END MOVED DEBUG LOGGING >>>

    // Check if the card is owned by the current player and is in their hand
    if (cardInstance.ownerId === currentPlayerId && cardInstance.location === 'hand') {
        // Conditional logging for phase/pp check remains inside if needed later, but primary log moved out.
        // console.log(`UI_HAND_CLICK_DBG: Phase Check: gameState.phase (${gameState.phase}) === 'Main' -> ${gameState.phase === 'Main'}`);
        // console.log(`UI_HAND_CLICK_DBG: PP Check: playerState.pp (${playerState?.pp}) >= cardInstance.cost (${cardInstance.cost}) -> ${playerState && playerState.pp >= cardInstance.cost}`);

        // console.log(`UI: Attempting to interact with hand card ${cardInstance.name} (Cost: ${cardInstance.cost}, Player PP: ${playerState?.pp})`);

        // Get card definition to check if it requires target
        // Use cardInstance.cardId (linking to CardDefinition) instead of cardInstance.id (which might be undefined)
        const cardData = getCardDefinition(cardInstance.id); // FIX: Use lowercase 'id' property
        if (!cardData) {
            // Updated error log to show the correct property we tried to use
            // console.error(`UI: Cannot find card data using cardId: ${cardInstance.cardId} (from cardInstance UID: ${cardInstance.uid}) in handleCardClick`);
            alert(`错误：找不到卡牌定义 (ID: ${cardInstance.cardId})！`); // Show the attempted ID in the alert
            return; // Stop if card data is missing
        }

        // Check PP cost
        // Check PP cost AND Phase // MODIFIED Condition
        if (gameState.phase === 'Main' && playerState && playerState.pp >= cardInstance.cost) { // ADDED phase check
            // Check if the card requires a target
            if (cardData.requiresTarget) {
                // --- Initiate Spell Targeting ---
                startSpellTargeting(cardInstance.uid); // Use helper function
                // console.log(`UI: Initiating targeting mode for spell: ${cardInstance.name} (UID: ${targetingSpellUid})`); // Log still uses module var for confirmation
                renderGame(gameState); // Re-render immediately to show targeting UI (highlights etc.)
                // IMPORTANT: Do NOT call playCard here yet! Wait for target selection click.
            } else {
                // --- Play Non-Targeted Card ---
                // console.log(`UI: Playing non-targeted card ${cardInstance.name} (UID: ${cardInstance.uid})`);
                playCard(cardInstance.uid); // Pass only card UID for non-targeted play
                // UI update will be triggered by gameManager after state changes
            }
        } else if (gameState.phase !== 'Main') { // ADDED Else If block for feedback
            console.log("UI: Card click rejected - Not in Main phase.");
            alert("只能在你的回合主阶段出牌！"); // Provide feedback if clicked outside Main phase
        } else {
            // Original 'else' for insufficient PP
            // console.log("UI: Not enough PP to play card.");
            alert("PP 不足，无法使用此卡牌！"); // User feedback
        }
    } // Closes if block starting on line 743 (hand card check)
    // Handle click on own field card
    else if (cardInstance.ownerId === currentPlayerId && cardInstance.location === 'field') {
        // Initiate Attack with Own Field Card (ONLY if not currently targeting a spell AND unit can attack)
        if (!isTargetingSpell) {
            // Check if the unit is actually allowed to attack
// --- DEBUG: Check canAttack status before initiating attack ---
            console.log(`%c[UI_ATTACK_CHECK] Clicking own field card: ${cardInstance.name} (UID: ${cardInstance.uid}). Checking canAttack: ${cardInstance.canAttack}`, 'color: blue; font-weight: bold;');
            // --- END DEBUG ---
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
    else if (cardInstance.location === 'leader_zone') {
        // --- Handle Click on Leader (NOT targeting spell) ---
        console.log(`%c[UI_LEADER_CLICK] Entered 'leader_zone' block. UID: ${cardInstance.uid}, Location: ${cardInstance.location}`, 'color: magenta; font-weight: bold;');
        // Spell targeting on leader is handled inside the "if (isTargetingSpell)" block earlier.
        // This block now only handles attack targeting or non-actionable clicks.
        // console.log(`UI: Clicked leader zone (UID: ${cardInstance.uid}) while NOT targeting spell.`);
        console.log(`%c[UI_LEADER_CLICK] Checking gameState.selectedAttackerUid: ${gameState.selectedAttackerUid}`, 'color: magenta;');
        if (gameState.selectedAttackerUid) {
            // --- Handle Attack Targeting on Leader ---
            console.log(`%c[UI_LEADER_CLICK] Attacker selected (${gameState.selectedAttackerUid}). Checking if target leader is opponent.`, 'color: magenta;');
            // console.log(`UI: Attacker ${gameState.selectedAttackerUid} selected, attempting to target Leader ${cardInstance.uid} for attack.`);
            // Get opponent ID (simplified check, assumes attacker UID format 'playerId_...')
            // FIX: selectedAttackerUid might not contain player ID prefix. Get attacker instance first.
            const attackerInstance = findCardInstanceByUid(gameState.selectedAttackerUid);
            const attackerOwnerId = attackerInstance?.ownerId; // Get owner from the instance
            const opponentPlayerId = gameState.players.find(p => p.id !== attackerOwnerId)?.id;
            console.log(`%c[UI_LEADER_CLICK] Attacker Owner: ${attackerOwnerId}, Determined Opponent ID: ${opponentPlayerId}, Target Leader Owner: ${cardInstance.ownerId}`, 'color: magenta;');

            if (opponentPlayerId && cardInstance.ownerId === opponentPlayerId) { // Ensure it's opponent's leader
                console.log(`%c[UI_LEADER_CLICK] Target is opponent leader. Calling selectAttackTarget(${cardInstance.uid})...`, 'color: green; font-weight: bold;');
                selectAttackTarget(cardInstance.uid); // Call gameManager
            } else {
                 console.warn(`%c[UI_LEADER_CLICK] Target leader owner (${cardInstance.ownerId}) does not match opponent ID (${opponentPlayerId}). Attack target selection aborted.`, 'color: orange;');
                // console.warn(`UI: Attempted to attack non-opponent leader ${cardInstance.uid}?`);
            }
        } else {
            // --- Handle Non-Actionable Click on Leader ---
             console.log(`%c[UI_LEADER_CLICK] No attacker selected. Click on leader is non-actionable in this context.`, 'color: gray;');
            // console.log(`UI: Clicked leader ${cardInstance.uid} in non-actionable context.`);
        }
    } // End of 'else if (cardInstance.location === 'leader_zone')' block
    else if (cardInstance.ownerId !== currentPlayerId && cardInstance.location === 'field') {
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
    else { // Handle clicks on cards in other locations (deck, graveyard, etc.) - usually ignore
        // 卡牌在牌库、墓地或其他不可点击的位置/状态
        // console.log(`UI: Clicked card in unhandled location/state: ${cardInstance.location}`);
    }
}