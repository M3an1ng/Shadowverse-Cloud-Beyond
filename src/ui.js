// src/ui.js - UI渲染与交互处理

// 导入依赖 (稍后根据需要添加)
// import { gameState } from './gameManager.js'; // 获取状态
// import { playCard, initiateAttack, selectAttackTarget, evolveCard } from './gameManager.js'; // 调用动作

// Import necessary functions and the gameState object
import { gameState, playCard, findCardInstanceByUid, getCurrentPlayerId, getPlayerState, initiateAttack, selectAttackTarget, cancelSelection, evolveCard, handleMulligan, isValidSpellTarget } from './gameManager.js'; // Added targeting utils, removed getCardDefinition
import { getCardDefinition } from './cardManager.js'; // Import card data helper
import { endTurn } from './turnManager.js';
console.log("ui.js loaded");

export { renderGame, showDamageNumber };

// --- UI State Variables ---
let isTargetingSpell = false; // Flag for spell targeting mode
let targetingSpellUid = null; // UID of the spell being targeted
// --- UI State Management Functions ---
function startSpellTargeting(spellUid) {
    isTargetingSpell = true;
    targetingSpellUid = spellUid;
    console.log(`UI State DBG: Started targeting spell ${spellUid}`);
}

function endSpellTargeting() {
    isTargetingSpell = false;
    targetingSpellUid = null;
    console.log(`UI State DBG: Ended spell targeting.`);
}
// --- End UI State Management Functions ---
// --- DOM Element References (缓存以提高性能) ---
// 变量将在 cacheDOMElements 中定义和赋值
let player1HandEl, player1FieldEl, player1HpEl, player1PpEl, player1MaxPpEl, player1EpEl, player1DeckEl, player1GraveEl;
let player2HandEl, player2FieldEl, player2HpEl, player2PpEl, player2MaxPpEl, player2EpEl, player2DeckEl, player2GraveEl;
let player1MaxEpEl, player2MaxEpEl; // ADDED Declaration for Max EP elements
let turnInfoEl, endTurnButtonEl, confirmMulliganButtonEl, gameContainerEl; // Added confirmMulliganButtonEl

// 缓存 DOM 元素的函数
function cacheDOMElements() {
    console.log("UI: Caching DOM elements...");
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
             console.error(`UI Cache Error: Missing critical elements with IDs: ${missing.join(', ')}. Check index.html.`);
        } else {
            console.log("UI: DOM elements caching successful.");
        }
    } catch (error) {
        console.error("UI Error during DOM element caching:", error);
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
// 接收完整的 gameState 作为参数
function renderGame(currentGameState) {
    console.log("UI: renderGame called");
    if (!currentGameState || !currentGameState.players || currentGameState.players.length < 2) {
        // 游戏状态无效，可能在初始化阶段，或者出错
        console.warn("UI: renderGame called with invalid or incomplete gameState.");
        // 可以考虑显示“加载中”或错误信息
        return;
    }

    // 假设 gameManager.gameState.players 存储了 player 对象
    const player1 = currentGameState.players.find(p => p.id === 'player1'); // 需要确保 player 对象存在
    const player2 = currentGameState.players.find(p => p.id === 'player2');

    if(!player1 || !player2) {
        console.error("UI: Could not find player objects in gameState for rendering.");
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
    renderTurnInfo(currentGameState.turnNumber, currentGameState.currentPlayerId);

    // 5. Render Highlights (Playable cards, selected attackers, targets)
    renderHighlights(currentGameState);

    // 6. Render Action Buttons based on Game Phase
    renderActionButtons(currentGameState);

    // console.log("UI: renderGame finished."); // Optional debug log
}

// --- 具体渲染辅助函数 (占位符) ---
function renderPlayerStats(playerState, prefix) { // prefix = 'player1' or 'player2'
    console.log(`UI-${prefix}: renderPlayerStats called. Data:`, JSON.stringify(playerState)); // ADDED: Log received data
    // console.log(`UI Rendering stats for ${prefix}`); // Log a bit noisy, disable for now
     if (!playerState) {
         console.warn(`UI: Player state for ${prefix} is missing for stats rendering.`);
         return;
     }

     try {
         console.log(`UI-${prefix}: Attempting to update DOM with PP: ${playerState?.pp}/${playerState?.maxPP}, EP: ${playerState?.ep}, HP: ${playerState?.health}`); // ADDED: Log update attempt (added optional chaining for safety)
         // Dynamically select elements based on prefix using cached variables
         const hpEl = (prefix === 'player1') ? player1HpEl : player2HpEl;
         const ppEl = (prefix === 'player1') ? player1PpEl : player2PpEl;
         const maxPpEl = (prefix === 'player1') ? player1MaxPpEl : player2MaxPpEl;
         const epEl = (prefix === 'player1') ? player1EpEl : player2EpEl;
         const deckEl = (prefix === 'player1') ? player1DeckEl : player2DeckEl;
         const graveEl = (prefix === 'player1') ? player1GraveEl : player2GraveEl;

         // Update text content only if the element was successfully cached
         if (hpEl) {
             hpEl.textContent = playerState.health ?? '??';
             console.log(`UI-${prefix}: Set hpEl.textContent to: ${hpEl.textContent}`); // ADDED
         } else { console.warn(`UI: ${prefix}HpEl missing, cannot render HP.`); }

         if (ppEl) {
             ppEl.textContent = playerState.pp ?? '??';
             console.log(`UI-${prefix}: Set ppEl.textContent to: ${ppEl.textContent}`); // ADDED
         } else { console.warn(`UI: ${prefix}PpEl missing, cannot render PP.`); }

         if (maxPpEl) {
             maxPpEl.textContent = playerState.maxPP ?? '??';
             console.log(`UI-${prefix}: Set maxPpEl.textContent to: ${maxPpEl.textContent}`); // ADDED
         } else { console.warn(`UI: ${prefix}MaxPpEl missing, cannot render MaxPP.`); }

         if (epEl) {
             epEl.textContent = playerState.ep ?? '?'; // Default EP display
             console.log(`UI-${prefix}: Set epEl.textContent to: ${epEl.textContent}`); // ADDED
         } else { console.warn(`UI: ${prefix}EpEl missing, cannot render EP.`); }

// ADDED: Update Max EP element
         const maxEpEl = (prefix === 'player1') ? player1MaxEpEl : player2MaxEpEl;
         if (maxEpEl) {
             maxEpEl.textContent = playerState.maxEP ?? '?'; // Use the correct playerState property
             console.log(`UI-${prefix}: Set maxEpEl.textContent to: ${maxEpEl.textContent}`);
         } else { console.warn(`UI: ${prefix}MaxEpEl missing, cannot render Max EP.`); }
         // Use optional chaining and nullish coalescing for deck/graveyard counts
         if (deckEl) {
             deckEl.textContent = playerState.deck?.length ?? 0;
             console.log(`UI-${prefix}: Set deckEl.textContent to: ${deckEl.textContent}`); // ADDED
         } else { console.warn(`UI: ${prefix}DeckEl missing, cannot render Deck count.`); }

         if (graveEl) {
             graveEl.textContent = playerState.graveyard?.length ?? 0;
             console.log(`UI-${prefix}: Set graveEl.textContent to: ${graveEl.textContent}`); // ADDED
         } else { console.warn(`UI: ${prefix}GraveEl missing, cannot render Graveyard count.`); }
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
             // console.log(`UI-${prefix}: Cleared turn indicator (ID mismatch)`); // Optional Debug
         }
     } else {
         turnIndicatorEl.textContent = ''; // Clear if first/second IDs aren't set yet or playerState missing
         // console.log(`UI-${prefix}: Cleared turn indicator (gameState IDs or playerState missing)`); // Optional Debug
     }
 } else {
      console.warn(`UI: Turn indicator element #${turnIndicatorId} not found.`);
 }
 // --- END ADDED ---

} catch (error) {
    console.error(`UI Error rendering stats for ${prefix}:`, error);
}
}
function renderPlayerHand(handCards = [], handElement) {
    // console.log(`UI Rendering player hand with ${handCards.length} cards.`); // Log might be too frequent
    if (!handElement) {
        console.error("UI Error: Hand element (player1HandEl or player2HandEl?) not provided for rendering hand.");
        return;
    }
    // Clear existing hand display
    handElement.innerHTML = '';
    // Iterate through hand card instances
    if (!Array.isArray(handCards)) {
        console.error("UI Error: handCards is not an array in renderPlayerHand.");
        handCards = []; // Ensure it's an array to prevent errors
    }
    handCards.forEach(cardInstance => {
        if (!cardInstance) {
             console.warn("UI Warning: null or undefined card instance found in handCards array.");
             return; // Skip this iteration
        }
        // Create the card's DOM element using the helper, specifying 'hand' context
        const cardElement = createCardElement(cardInstance, 'hand');
        if (cardElement) {
            // Append the created card element to the hand container
            handElement.appendChild(cardElement);
        } else {
            // Log if element creation failed for some reason
            console.warn(`UI Warning: Failed to create element for card instance in hand:`, cardInstance);
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
        console.error("UI Error: Opponent hand element (player2HandEl) not provided.");
        return;
    }
    // Clear existing hand display
    handElement.innerHTML = '';
    // Ensure handCards is an array
    if (!Array.isArray(handCards)) {
        console.error("UI Error: handCards is not an array in renderOpponentHand.");
        handCards = [];
    }
    // Create and append a card back for each card in hand
    // Ensure there's a '.card-back' style defined in style.css
    handCards.forEach(() => {
        const cardBackDiv = document.createElement('div');
        cardBackDiv.classList.add('card-back'); // Use existing CSS class if available, or create one
        handElement.appendChild(cardBackDiv);
    });
    // console.log(`UI: Rendered ${handCards.length} card backs for opponent.`); // Optional debug
}

function renderField(fieldCards = [], fieldElement, isOpponent = false) {
    // console.log(`UI Rendering field. Opponent: ${isOpponent}, Cards: ${fieldCards.length}`); // Noisy log
    if (!fieldElement) {
        console.error(`UI Error: Field element not provided for rendering field (Opponent: ${isOpponent}).`);
        return;
    }
    // Clear existing field display
    fieldElement.innerHTML = '';
    // Iterate through field card instances
    if (!Array.isArray(fieldCards)) {
        console.error(`UI Error: fieldCards is not an array in renderField (Opponent: ${isOpponent}).`);
        fieldCards = []; // Prevent errors by ensuring it's an array
    }
    fieldCards.forEach(cardInstance => {
        if (!cardInstance) {
             console.warn("UI Warning: null or undefined card instance found in fieldCards array.");
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
            console.warn(`UI Warning: Failed to create element for card instance on field (Opponent: ${isOpponent}):`, cardInstance);
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
        console.error("UI Error: createCardElement called with null cardInstance.");
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
        // 如果是已进化的随从，添加 'evolved' class (需要 cardInstance 有 isEvolved 属性)
        // if (cardInstance.isEvolved) { cardDiv.classList.add('evolved'); }
    }

    // 基本的 Inner HTML 结构 - 需要根据 style.css 进行调整
    // 使用 ?? 提供默认值，防止属性缺失导致显示错误
    cardDiv.innerHTML = `
        <div class="card-art"></div> <!-- 卡面美术图占位 -->
        <div class="cost">${cardInstance.cost ?? '?'}</div>
        <div class="name">${cardInstance.name ?? 'Unknown Card'}</div>
        ${
            // 仅为随从显示攻击力和生命值区域
            (cardInstance.type === 'Follower' || cardInstance.type === 'Follower_Evolved') ?
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
                    gameState.gamePhase === 'Main' &&
                    playerState.ep > 0 &&
                    playerState.canEvolveThisTurn === true &&
                    cardInstance.type === 'Follower' &&
                    cardInstance.isEvolved === false // Explicitly check instance state
                    // Base card data check (cardData.can_evolve) is skipped here for UI simplicity;
                    // gameManager will perform the definitive check.
                );

                if (showEvolveButton) {
                    const evolveBtn = document.createElement('button');
                    evolveBtn.className = 'evolve-button'; // Class for styling and event targeting
                    evolveBtn.textContent = 'Evolve';
                    evolveBtn.dataset.evolveUid = cardInstance.uid; // Store UID to identify which card to evolve
                    cardDiv.appendChild(evolveBtn); // Add button to the card div
                    console.log(`UI DBG: Added Evolve button to ${cardInstance.name} (UID: ${cardInstance.uid})`); 
                }
            }
            // --- END Evolve Button ---
    return cardDiv;
}

// --- 渲染回合信息的辅助函数 ---
function renderTurnInfo(turnNumber, currentPlayerId) {
    // Update the turn information display element if it was cached successfully
    if (turnInfoEl) {
        // Use template literals for clarity and potential future localization
        // Displaying in English for code consistency, can be localized later.
        turnInfoEl.textContent = `Turn ${turnNumber} - Player: ${currentPlayerId ?? 'N/A'}`;
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
    console.log("UI Highlights: Applying spell targeting highlights.");
    // Add global targeting class
    gameContainerEl?.classList.add('targeting-active');

    // Highlight the spell card itself in hand
    const spellCardElement = player1HandEl?.querySelector(`.card[data-uid="${targetingSpellUid}"]`);
    if (spellCardElement) {
        spellCardElement.classList.add('targeting-spell');
    } else {
        console.warn(`UI Highlights: Could not find element for targeting spell UID ${targetingSpellUid} in hand.`);
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
                  console.log(`UI Highlights: Marked ${targetInstance.name} (UID: ${targetInstance.uid}) as targetable-spell.`);
             } else {
                  // Log warning if element not found using either method
                  console.warn(`UI Highlights: Could not find element for spell target UID ${targetInstance.uid}`);
             }
        }
    });
}

// Helper 3: Render Playable Card Highlights (in hand)
function renderPlayableCardHighlights(currentGameState) {
     const currentPlayerId = getCurrentPlayerId();
     const playerState = getPlayerState(currentPlayerId);
     if (!playerState || !player1HandEl) return; // Exit if state or element missing

     playerState.hand.forEach(cardInstance => {
         if (!cardInstance) return; // Skip if null
         // Check if the card is playable (based on PP cost)
         if (cardInstance.cost <= playerState.pp) {
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
             console.warn(`UI Highlights: Could not find element for selected attacker UID ${currentGameState.selectedAttackerUid}`);
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
                      const targetElement = gameContainerEl?.querySelector(`.card[data-uid="${targetInstance.uid}"]`); // Target might be leader or follower
                      if (targetElement) {
                          targetElement.classList.add('targetable');
                          // console.log(`UI Highlights: Marked ${targetInstance.name} as targetable.`); // Optional debug
                      } else {
                          console.warn(`UI Highlights: Could not find element for attack target UID ${targetInstance.uid}`);
                      }
                  }
             });
         } else {
              console.warn(`UI Highlights: Could not find opponent player state for ID: ${opponentPlayerId}`);
         }
    }
}

// --- 渲染行动按钮（结束回合/确认调度）的辅助函数 ---
function renderActionButtons(currentGameState) {
    if (!endTurnButtonEl || !confirmMulliganButtonEl) {
        console.warn("UI: Action buttons not cached, cannot render visibility.");
        return;
    }

    const isPlayer1Turn = currentGameState.currentPlayerId === 'player1';

    if (currentGameState.gamePhase === 'Mulligan') {
        // Mulligan Phase: Show confirm button (only if P1 hasn't confirmed), hide end turn
        confirmMulliganButtonEl.style.display = currentGameState.player1MulliganComplete ? 'none' : 'inline-block';
        endTurnButtonEl.style.display = 'none';
        console.log("UI: Phase Mulligan - Confirm Button:", confirmMulliganButtonEl.style.display, "End Turn Button: none");
    } else if (currentGameState.gamePhase === 'Main' && isPlayer1Turn) {
        // Player 1's Main Phase: Show end turn, hide confirm
        confirmMulliganButtonEl.style.display = 'none';
        endTurnButtonEl.style.display = 'inline-block';
         console.log("UI: Phase Main (P1) - Confirm Button: none", "End Turn Button: inline-block");
    } else {
        // Other phases or opponent's turn: Hide both (or just hide confirm, end turn might be handled by turn manager logic implicitly)
        confirmMulliganButtonEl.style.display = 'none';
        endTurnButtonEl.style.display = 'none'; // Hide end turn if not P1's main phase
        console.log("UI: Phase Other/Opponent - Confirm Button: none", "End Turn Button: none");
    }
}
// --- 显示伤害数字的函数 ---
function showDamageNumber(targetUid, damageAmount) {
  console.log(`UI: showDamageNumber called for UID: ${targetUid}, Amount: ${damageAmount}`);
  if (!targetUid || damageAmount === undefined || damageAmount === null || damageAmount <= 0) { // Also check for non-positive damage
    console.warn("UI: showDamageNumber called with invalid arguments (UID missing, damage invalid, or damage <= 0).");
    return;
  }

  let targetElement = null;
  // 特殊处理玩家头像
  if (targetUid === 'L1') {
      targetElement = document.getElementById('player1-avatar');
  } else if (targetUid === 'L2') {
      targetElement = document.getElementById('player2-avatar');
  } else {
      // 尝试通过 data-uid 查找其他单位 (随从/护符等)
      targetElement = gameContainerEl?.querySelector(`[data-uid='${targetUid}']`);
  }

  if (!targetElement) {
    console.warn(`UI: Could not find target element for UID: ${targetUid}`);
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
    console.log(`UI: Removing damage number element for ${targetUid}`);
    if (damageElement.parentNode) {
         damageElement.remove();
    }
  }, { once: true }); // Use { once: true } for efficiency
}
// --- 事件处理 ---

// 更新 initializeUI 以确保调用事件设置 (确保 DOM 加载后执行)
function initializeUI() {
    console.log("UI: Initializing...");
    cacheDOMElements();
    setupGlobalEventListeners(); // 确保调用
}
// (确保 initializeUI 在 DOMContentLoaded 后调用)

// 设置全局事件监听器 (事件委托模式)
function setupGlobalEventListeners() {
     if (!gameContainerEl) {
        console.error("UI Error: Cannot setup global listeners - gameContainerEl not found.");
        return;
     }
     console.log("UI: Setting up global click listener on game container...");
     // 先移除旧监听器，防止重复添加 (如果 initializeUI 可能被多次调用)
     gameContainerEl.removeEventListener('click', handleGameClick);
     gameContainerEl.addEventListener('click', handleGameClick);
     console.log("UI: Global click listener attached.");
}

// 全局点击事件处理器 (事件委托)
function handleGameClick(event) {
    console.log("UI_DBG: handleGameClick fired! Phase:", gameState.gamePhase); // Log phase on click

    // --- Mulligan Phase Logic ---
    if (gameState.gamePhase === 'Mulligan') {
        const clickedMulliganConfirm = event.target.closest('#confirm-mulligan-button');
        const clickedHandCard = event.target.closest('#player1-hand .card'); // Only care about player 1 hand

        if (clickedMulliganConfirm) {
            console.log("UI: Confirm Mulligan button clicked!");
            // Gather selected card UIDs
            const selectedCards = player1HandEl.querySelectorAll('.card.selected-for-mulligan');
            const selectedUids = Array.from(selectedCards).map(card => card.dataset.uid);
            console.log("UI: Selected UIDs for mulligan:", selectedUids);

            // Call handleMulligan from gameManager
            if (typeof handleMulligan === 'function') {
                handleMulligan('player1', selectedUids); // Pass player ID and selected UIDs
            } else {
                console.error("UI Error: handleMulligan function is not available/imported!");
                alert("错误：调度功能未加载！");
            }
            // Note: No need to manually remove 'selected-for-mulligan' class here,
            // as the next renderGame call after handleMulligan will redraw the hand.
            return; // Stop processing click after handling mulligan confirm

        } else if (clickedHandCard) {
            // Toggle selection class on the clicked hand card
            console.log(`UI: Mulligan phase - Hand card ${clickedHandCard.dataset.uid} clicked.`);
            clickedHandCard.classList.toggle('selected-for-mulligan');
            // Optionally provide visual feedback immediately if needed, or rely on style changes
            return; // Stop processing click after toggling selection

        } else {
            // Clicked somewhere else during mulligan (e.g., empty space, opponent card) - do nothing
            console.log("UI: Mulligan phase - Clicked non-interactive element.");
            return; // Stop processing click
        }
    }
    // --- End Mulligan Phase Logic ---

    // If not in Mulligan phase, continue with normal game interactions:
    console.log("UI: Not in Mulligan phase, processing standard click..."); // Added log

    // Check if the click was on the End Turn button
        const clickedEndTurnButton = event.target.closest('#end-turn-button');
        if (clickedEndTurnButton) {
            console.log("UI: End Turn button clicked!");
            if (typeof endTurn === 'function') {
                endTurn(); // Call the imported function
            } else {
                console.error("UI Error: endTurn function is not available/imported!");
                alert("Error: End Turn functionality not loaded.");
            }
            return; // Stop processing this click further
        }

        // Existing logic follows... (This comment isn't inserted, just for clarity here)
// --- Check for Evolve Button Click FIRST ---
        const clickedEvolveButton = event.target.closest('.evolve-button');
        if (clickedEvolveButton) {
            console.log("UI: Evolve button clicked!");
            const evolveUid = clickedEvolveButton.dataset.evolveUid; // Get UID from button's data attribute
            if (evolveUid) {
                console.log(`UI: Calling evolveCard for UID: ${evolveUid}`);
                if (typeof evolveCard === 'function') { // Check if function is imported
                    evolveCard(evolveUid); // Call the imported function from gameManager
                } else {
                     console.error("UI Error: evolveCard function is not available/imported!");
                     alert("错误：进化功能未加载！");
                }
            } else {
                console.warn("UI: Evolve button clicked but missing data-evolve-uid attribute.");
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
        console.log("UI_DBG: Click identified as Card:", targetUid);
    } else if (clickedPlayer1Avatar) {
        targetUid = 'L1'; // Assign leader UID
        console.log("UI_DBG: Click identified as Player 1 Avatar (L1)");
    } else if (clickedPlayer2Avatar) {
        targetUid = 'L2'; // Assign leader UID
        console.log("UI_DBG: Click identified as Player 2 Avatar (L2)");
    }

    if (targetUid) {
        // We clicked a card or an avatar
        const cardInstance = findCardInstanceByUid(targetUid);
        if (cardInstance) {
            handleCardClick(cardInstance); // Call the unified handler
        } else {
            console.warn(`UI: Clicked target (UID: ${targetUid}) instance not found in gameState.`);
            // Avoid alert spam, just log
        }
    } else {
        // Clicked on non-card, non-avatar element
        console.log("UI: Clicked non-interactive area.");

        // 1. Check if currently targeting a spell - Clicking outside cancels it
        if (isTargetingSpell) {
            console.log("UI: Clicked non-interactive area while targeting a spell. Cancelling targeting.");
            endSpellTargeting(); // Use helper function
            renderGame(gameState); // Re-render to remove highlights
        }
        // 2. If not targeting a spell, check if an attacker is selected - Clicking outside cancels attack
        else if (gameState.selectedAttackerUid) {
            // Only cancel if click wasn't end turn or evolve button (already handled)
             if (!event.target.closest('#end-turn-button') && !event.target.closest('.evolve-button')) {
                  console.log("UI: Clicked non-interactive area while attacker selected, cancelling attack selection.");
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
            console.log(`UI: In targeting mode (Spell: ${targetingSpellUid}). Clicked card UID: ${cardInstance.uid}`);
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
                    console.log(`UI: Spell target selected (Field): ${cardInstance.name} (UID: ${cardInstance.uid})`);
                    const currentTargetingSpellUid = targetingSpellUid; // Store UID before resetting
                    endSpellTargeting(); // Use helper function
                    // Let playCard handle the render update
                    playCard(currentTargetingSpellUid, cardInstance.uid); // Call gameManager AFTER resetting UI state, pass stored UID
                } else {
                     console.log("UI: Clicked field card is not a valid target for the current spell.");
                     // Do nothing, allow user to click another target or cancel
                }
            } else if (cardInstance.location === 'hand' && cardInstance.uid === targetingSpellUid) {
                 // Clicked the spell card itself again while targeting - CANCEL
                 console.log("UI: Clicked targeting spell card again. Cancelling targeting.");
                 endSpellTargeting(); // Use helper function
                 renderGame(gameState); // Re-render to remove highlights
} else if (cardInstance.location === 'leader_zone') {
                 // --- Potential Spell Target Clicked (Leader) ---
                 console.log(`UI: Targeting Spell ${targetingSpellUid}. Check if leader ${cardInstance.uid} is valid target.`);
                 // Get spell data and caster ID to check validity
                 const spellInstance = findCardInstanceByUid(targetingSpellUid);
                 const spellData = spellInstance ? getCardDefinition(spellInstance.id) : null;
                 const casterId = getCurrentPlayerId();
                 // Check validity
                 if (isValidSpellTarget(cardInstance, spellData?.validTargets || [], casterId)) {
                     console.log(`UI: Spell target selected (Leader): ${cardInstance.name} (UID: ${cardInstance.uid}). Playing card.`);
                     const currentTargetingSpellUid = targetingSpellUid; // Store UID before resetting
                     endSpellTargeting(); // Use helper function
                     // Render will happen via gameManager callback
                     playCard(currentTargetingSpellUid, cardInstance.uid); // Play spell targeting the leader AFTER resetting UI state, pass stored UID
                 } else {
                     console.log(`UI: Clicked leader ${cardInstance.uid} is NOT a valid target for spell ${targetingSpellUid}.`);
                     // Do nothing, allow re-selection or cancellation
                 }
            } else {
                 // Clicked an irrelevant card (e.g., opponent hand, own hand but not the spell, etc.)
                 console.log("UI: Clicked irrelevant card during spell targeting.");
                 // Do nothing, wait for valid target click or cancellation
            }
            // REMOVED premature return; Fall through to allow other logic if necessary,
            // though typically spell selection should conclude the interaction within this block.
        }
        // --- End Spell Target Selection ---

        // --- Normal Card Interaction (Not Spell Targeting) ---
        // (Original function body starts below this comment)
// Get current player context for normal interactions
         const currentPlayerId = getCurrentPlayerId();
         const playerState = getPlayerState(currentPlayerId);

         // Basic check for player state validity
         if (!playerState) {
             console.error("UI Error in handleCardClick: Could not get player state for normal interaction.");
             // Optionally alert the user or return to prevent further errors
             alert("发生内部错误：无法获取玩家状态！");
             return;
         }
    console.log("UI: handleCardClick processing UID:", cardInstance?.uid);

    if (!cardInstance) {
        console.error("UI: handleCardClick received null cardInstance.");
        return; // 安全检查
    }

      // Check if the card is owned by the current player and is in their hand
        if (cardInstance.ownerId === currentPlayerId && cardInstance.location === 'hand') {
            console.log(`UI: Attempting to interact with hand card ${cardInstance.name} (Cost: ${cardInstance.cost}, Player PP: ${playerState?.pp})`);

            // Get card definition to check if it requires target
            const cardData = getCardDefinition(cardInstance.id);
            if (!cardData) {
                console.error(`UI: Cannot find card data for ${cardInstance.id} in handleCardClick`);
                alert("错误：找不到卡牌定义！");
                return; // Stop if card data is missing
            }

            // Check PP cost
            if (playerState && playerState.pp >= cardInstance.cost) {
                 // Check if the card requires a target
                 if (cardData.requiresTarget) {
                     // --- Initiate Spell Targeting ---
                     startSpellTargeting(cardInstance.uid); // Use helper function
                     console.log(`UI: Initiating targeting mode for spell: ${cardInstance.name} (UID: ${targetingSpellUid})`); // Log still uses module var for confirmation
                     renderGame(gameState); // Re-render immediately to show targeting UI (highlights etc.)
                     // IMPORTANT: Do NOT call playCard here yet! Wait for target selection click.
                 } else {
                     // --- Play Non-Targeted Card ---
                     console.log(`UI: Playing non-targeted card ${cardInstance.name} (UID: ${cardInstance.uid})`);
                     playCard(cardInstance.uid); // Pass only card UID for non-targeted play
                     // UI update will be triggered by gameManager after state changes
                 }
            } else {
                 console.log("UI: Not enough PP to play card.");
                 alert("PP 不足，无法使用此卡牌！"); // User feedback
            }
} // Closes if block starting on line 743 (hand card check)
                // Handle click on own field card
                else if (cardInstance.ownerId === currentPlayerId && cardInstance.location === 'field') {
                    // Initiate Attack with Own Field Card (ONLY if not currently targeting a spell)
                    if (!isTargetingSpell) {
                        console.log(`UI: Attempting to initiate attack with own field card: ${cardInstance.name} (UID: ${cardInstance.uid})`);
                        initiateAttack(cardInstance.uid); // Call gameManager
                    } else {
                         console.log("UI: Cannot initiate attack while targeting a spell.");
                         // Ignore click while targeting spell.
                    }
                }
                else if (cardInstance.location === 'leader_zone') {
                    // --- Handle Click on Leader (NOT targeting spell) ---
                    // Spell targeting on leader is handled inside the "if (isTargetingSpell)" block earlier.
                    // This block now only handles attack targeting or non-actionable clicks.
                    console.log(`UI: Clicked leader zone (UID: ${cardInstance.uid}) while NOT targeting spell.`);
                    if (gameState.selectedAttackerUid) {
                         // --- Handle Attack Targeting on Leader ---
                         console.log(`UI: Attacker ${gameState.selectedAttackerUid} selected, attempting to target Leader ${cardInstance.uid} for attack.`);
                          // Get opponent ID (simplified check, assumes attacker UID format 'playerId_...')
                         const attackerOwnerId = gameState.selectedAttackerUid.split('_')[0];
                         const opponentPlayerId = gameState.players.find(p => p.id !== attackerOwnerId)?.id;
                         if (opponentPlayerId && cardInstance.ownerId === opponentPlayerId) { // Ensure it's opponent's leader
                             selectAttackTarget(cardInstance.uid); // Call gameManager
                         } else {
                             console.warn(`UI: Attempted to attack non-opponent leader ${cardInstance.uid}?`);
                         }
                    } else {
                         // --- Handle Non-Actionable Click on Leader ---
                         console.log(`UI: Clicked leader ${cardInstance.uid} in non-actionable context.`);
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
                console.log(`UI: Spell target selected (Opponent Field): ${cardInstance.name} (UID: ${cardInstance.uid})`);
                const currentTargetingSpellUid = targetingSpellUid; // Store UID before resetting
                endSpellTargeting(); // Use helper function
                // Render will happen via gameManager callback
                playCard(currentTargetingSpellUid, cardInstance.uid); // Call gameManager with spell and target AFTER resetting UI state, pass stored UID
             } else {
                 console.log("UI: Clicked opponent field card is not a valid target for the current spell.");
                 // Do nothing, allow user to click another target or cancel
             }
        } else {
            // If NOT targeting a spell, proceed with attack target selection
            console.log(`UI: Clicked opponent field card for potential attack target: ${cardInstance.name} (UID: ${cardInstance.uid})`);
            // Check if an attacker is currently selected from our side
            if (gameState.selectedAttackerUid) { // Check the imported global state
                console.log(`UI: Attacker ${gameState.selectedAttackerUid} is selected, attempting to target ${cardInstance.uid} for attack.`);
                selectAttackTarget(cardInstance.uid); // Call the imported function
            } else {
                console.log("UI: Clicked opponent field card, but no attacker is selected for attack.");
                // Optionally provide feedback or do nothing
            }
        }
    }
else { // Handle clicks on cards in other locations (deck, graveyard, etc.) - usually ignore
        // 卡牌在牌库、墓地或其他不可点击的位置/状态
        console.log(`UI: Clicked card in unhandled location/state: ${cardInstance.location}`);
    }
}