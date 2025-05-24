// src/gameManager.js - 核心游戏状态与逻辑管理

import { createPlayer } from './player.js'; // 正确导入
import { getCardDefinition } from './cardManager.js'; // ADDED: Renamed import needed for evolveCard
// import { getCardDefinition } from './cardManager.js'; // initGame 不需要
// import { renderGame } from './ui.js'; // 渲染调用在 main.js
import { renderGame } from './ui.js'; // For updating UI after actions
import { startTurn } from './turnManager.js'; // Import startTurn to begin the first turn
import { showDamageNumber } from './ui.js'; // Import the damage number display function
import { performAIMulligan } from './ai.js'; // Placeholder for AI mulligan logic
// Note: Player's performMulligan is a method, called via playerState.performMulligan
    // import { MAX_FIELD_SIZE } from './main.js'; // Check if defined elsewhere

    // --- Constants ---
    const MAX_FIELD_SIZE = 5; // Maximum cards on a player's field

console.log("gameManager.js loaded");

// 核心游戏状态 (可导出或通过函数访问)
export let gameState = {
    players: [], // 存储 Player 实例
    currentPlayerId: null,
    turnNumber: 0,
    // 'Mulligan', 'TurnStart', 'Main', 'Battle', 'TurnEnd', 'GameOver'
    gamePhase: 'Initializing',
    activePlayer: null, // 对当前玩家实例的引用
    opponentPlayer: null, // 对对手玩家实例的引用
    gameOver: false,
    winner: null,
    selectedCardUid: null, // 手牌中选中的卡 (用于出牌或查看详情)
    selectedAttackerUid: null, // 战场上选中的攻击者
    player1MulliganComplete: false, // Player 1 mulligan status
    player2MulliganComplete: false, // Player 2 mulligan status
    // 稍后可能添加: eventLog, effectQueue 等
};

// 初始化游戏 (已实现版本)
export function initGame() {
    console.log("GAME MANAGER: Initializing New Game...");

    // 1. Reset State
    console.log("Resetting game state...");
    gameState.players = [];
    gameState.currentPlayerId = null;
    gameState.turnNumber = 0;
    gameState.gamePhase = 'Initializing';
    gameState.activePlayer = null;
    gameState.opponentPlayer = null;
    gameState.gameOver = false;
    gameState.winner = null;
    gameState.selectedCardUid = null;
    gameState.selectedAttackerUid = null;
    gameState.player1MulliganComplete = false; // Reset mulligan flags
    gameState.player2MulliganComplete = false;

    // 2. Create Players
    console.log("Creating players...");
    const player1 = createPlayer('player1');
    const player2 = createPlayer('player2', true); // Player 2 is AI
    gameState.players.push(player1, player2);
    console.log(`Players created: ${player1.id}, ${player2.id} (AI: ${player2.isAI})`);

    // 3. Define and Initialize Decks
    // Updated deck list including cards with new abilities for testing (IDs Corrected)
    const sampleDeckList = [ // 20 cards total
        // Storm
        'F002', 'F002', 'F002', 'F002',        // Fighter (ID: F002, Storm) x4
        // Vanilla
        'S001', 'S001', 'S001',                // Insight (ID: S001, Spell: Draw 1) x3
        'F001',                                // Goblin (ID: F001) x1
        'F003', 'F003', 'F003',                // Knight (ID: F003) x3
        'S002', 'S002', 'S002',                // Angelic Snipe (ID: S002) x3
        // Ward
        'F005', 'F005', 'F005',                // Shield Guardian (ID: F005, Ward) x3
        // Fanfare
        'F006', 'F006', 'F006',                // Bellringer Angel (ID: F006, Fanfare: Draw 1) x3
    ]; // Total: 4 + 3 + 1 + 3 + 3 + 3 + 3 = 20 cards (Storm x4, Insight x3, Goblin x1, Knight x3, Armored x3, Ward x3, Fanfare x3)
    console.log("Initializing decks...");
    player1.initializeDeck([...sampleDeckList]); // Pass copy, initializes and shuffles
    player2.initializeDeck([...sampleDeckList]); // Pass copy, initializes and shuffles

    // 4. Randomly Determine First Player & Set Initial PP
    console.log("Randomly determining first player...");
    const firstPlayerIndex = Math.floor(Math.random() * 2); // 0 or 1
    const firstPlayer = gameState.players[firstPlayerIndex];
    const secondPlayer = gameState.players[1 - firstPlayerIndex];

    gameState.firstPlayerId = firstPlayer.id;
    gameState.secondPlayerId = secondPlayer.id;
    gameState.currentPlayerId = firstPlayer.id; // First turn goes to the first player
    gameState.activePlayer = firstPlayer;
    gameState.opponentPlayer = secondPlayer;

    console.log(`GAME MANAGER: First player is ${gameState.firstPlayerId}, Second player is ${gameState.secondPlayerId}.`);
    console.log("GM_TRACE_ID: After determineFirstPlayer - FirstPlayer:", gameState.firstPlayerId, "SecondPlayer:", gameState.secondPlayerId); // ADDED LOG

    // Initialize PP for both players (EP and MaxEP are handled in startTurn)
    firstPlayer.pp = 0;
    firstPlayer.maxPP = 0;
    secondPlayer.pp = 0;
    secondPlayer.maxPP = 0;
    console.log(`GAME MANAGER: Initial PP set to 0/0 for both players.`);

    // 5. Initial Draw (跳过调度)
    console.log("Performing initial draw (3 cards each)...");
    const initialHandSize = 3;
    player1.drawCard(initialHandSize);
    console.log(`GAME MANAGER: Player 1 hand after draw (${player1.hand?.length || 0}):`, player1.hand?.map(c => c?.name || 'ERROR'));
    player2.drawCard(initialHandSize);
    console.log(`GAME MANAGER: Player 2 hand after draw (${player2.hand?.length || 0}):`, player2.hand?.map(c => c?.name || 'ERROR'));


    // 6. Set game phase to Mulligan
    gameState.turnNumber = 1; // Turn 1 hasn't officially started yet, but setup is for it
    gameState.gamePhase = 'Mulligan'; // Start the Mulligan phase
    gameState.gameOver = false;
    gameState.winner = null;
    console.log("GM: Game phase set to Mulligan.");

    // Log final state before exiting initGame
    try {
        const p1State = gameState.players.find(p => p.id === 'player1');
        const p2State = gameState.players.find(p => p.id === 'player2');
        console.log(`GAME MANAGER: Final check before exit - Turn: ${gameState.turnNumber}, CurrentPlayer: ${gameState.currentPlayerId}`);
        if (p1State) {
            console.log(`  P1 State: PP=${p1State.pp}/${p1State.maxPP}, EP=${p1State.ep}/${p1State.maxEP}, Deck=${p1State.deck?.length || 0}, Hand=${p1State.hand?.length || 0}`);
        } else {
             console.log("  P1 State: Not found in gameState!");
        }
        if (p2State) {
            console.log(`  P2 State: PP=${p2State.pp}/${p2State.maxPP}, EP=${p2State.ep}/${p2State.maxEP}, Deck=${p2State.deck?.length || 0}, Hand=${p2State.hand?.length || 0}`);
        } else {
             console.log("  P2 State: Not found in gameState!");
        }
    } catch (e) {
        console.error("Error logging final game state:", e);
    }

    console.log(`GAME MANAGER: Game Initialized. Turn ${gameState.turnNumber}. Current Player: ${gameState.currentPlayerId}`);
    // 可以打印状态进行调试，但注意对象可能很大
    // console.log("Initial gameState:", JSON.parse(JSON.stringify(gameState)));

    // Schedule the start of the Mulligan phase
    console.log("GM: Scheduling Mulligan phase start...");
    setTimeout(startMulliganPhase, 50); // Short delay before starting mulligan
}

// --- Mulligan Phase Logic ---

// Called after initGame to start the mulligan process
export function startMulliganPhase() {
    console.log("GM: Starting Mulligan Phase...");
    gameState.gamePhase = 'Mulligan';
    gameState.player1MulliganComplete = false; // Ensure flags are reset
    gameState.player2MulliganComplete = false;

    // Render the mulligan UI (UI needs to know how to display this phase)
    renderGame(gameState);

    // Trigger AI mulligan immediately (AI makes its decision instantly)
    const aiPlayer = gameState.players.find(p => p.isAI);
    if (aiPlayer) {
        console.log("GM: Triggering AI mulligan...");
        performAIMulligan(aiPlayer); // This function needs to be defined in ai.js
                                    // and call handleMulligan('player2', chosenCards)
    } else {
        console.warn("GM: AI player not found, cannot trigger AI mulligan.");
    }
}

// Handles the mulligan action for a specific player
export function handleMulligan(playerId, cardUidsToReplace = []) {
    console.log(`GM: Handling mulligan for ${playerId}. Cards to replace:`, cardUidsToReplace);
    console.log("GM_TRACE_ID: Entering handleMulligan for", playerId, "- Current secondPlayerId:", gameState.secondPlayerId); // ADDED LOG

    const playerState = getPlayerState(playerId);

    // --- Validations ---
    if (gameState.gamePhase !== 'Mulligan') {
        console.warn(`GM: Mulligan rejected - Not in Mulligan phase (Current: ${gameState.gamePhase}).`);
        return;
    }
    if (!playerState) {
        console.error(`GM: Mulligan failed - Player state not found for ID: ${playerId}.`);
        return;
    }
    if (!Array.isArray(cardUidsToReplace)) {
         console.error(`GM: Mulligan failed - cardUidsToReplace must be an array.`);
         return;
    }
    if (cardUidsToReplace.length > playerState.hand.length || cardUidsToReplace.length > 3) { // Assuming initial hand size is 3
         console.warn(`GM: Mulligan rejected - Invalid number of cards to replace (${cardUidsToReplace.length}). Max 3.`);
         return;
    }

    // Check if this player already completed their mulligan
    const isPlayer1 = playerId === 'player1';
    if ((isPlayer1 && gameState.player1MulliganComplete) || (!isPlayer1 && gameState.player2MulliganComplete)) {
        console.warn(`GM: Mulligan rejected - Player ${playerId} already completed mulligan.`);
        return;
    }

    // --- Execute Mulligan (using Player method - to be implemented) ---
    try {
        playerState.performMulligan(cardUidsToReplace); // This method needs to exist in player.js
        console.log(`GM: Player ${playerId} mulligan performed. Hand size: ${playerState.hand.length}`);
    } catch (error) {
        console.error(`GM: Error during playerState.performMulligan for ${playerId}:`, error);
        return; // Stop processing if mulligan failed
    }


    // --- Update State ---
    if (isPlayer1) {
        gameState.player1MulliganComplete = true;
        console.log("GM: Player 1 mulligan complete.");
    } else {
        gameState.player2MulliganComplete = true;
        console.log("GM: Player 2 mulligan complete.");
    }

    // --- Check if Both Completed ---
    console.log("GM_TRACE_ID: Exiting handleMulligan for", playerId, "- Current secondPlayerId:", gameState.secondPlayerId); // ADDED LOG (before check)
    if (gameState.player1MulliganComplete && gameState.player2MulliganComplete) {
        console.log("GM: Both players finished mulligan. Starting first turn...");
        gameState.gamePhase = 'TurnStart';
        // No need for setTimeout here, transition directly
        startTurn(); // Proceed to the first turn
    } else {
        // If only one player finished, just update the UI to reflect their new hand/state
        console.log("GM: One player finished mulligan, waiting for the other. Rendering game state.");
         renderGame(gameState);
    }
}


// 新增：根据 UID 查找卡牌实例 (手牌或战场)
    export function findCardInstanceByUid(uid) {
        if (!uid) return null; // 如果没有提供 uid，直接返回 null

        for (const player of gameState.players) {
            // 检查手牌
            const cardInHand = player.hand?.find(card => card && card.uid === uid);
            if (cardInHand) {
                console.log(`GAME MANAGER: Found card UID ${uid} in ${player.id}'s hand.`);
                return cardInHand;
            }
            // 检查战场
            const cardOnField = player.field?.find(card => card && card.uid === uid);
            if (cardOnField) {
                console.log(`GAME MANAGER: Found card UID ${uid} on ${player.id}'s field.`);
                return cardOnField;
            }
            // 检查主战者
            // Ensure player.leaderCard exists before checking its uid
            if (player.leaderCard && player.leaderCard.uid === uid) {
                console.log(`GAME MANAGER: Found leader card instance UID ${uid} for player ${player.id} (Location: ${player.leaderCard.location}).`);
                return player.leaderCard;
            }
        }

        console.warn(`GAME MANAGER: Card instance with UID ${uid} not found in any player's hand, field, or leader zone.`);
        return null; // 遍历完所有玩家的手牌、战场和主战者区域都未找到
    }

// 新增：获取当前回合玩家ID
    export function getCurrentPlayerId() {
        return gameState.currentPlayerId;
    }

    // 新增：根据玩家ID获取玩家状态对象
    export function getPlayerState(playerId) {
        if (!playerId) return null;
        return gameState.players.find(p => p && p.id === playerId);
    }
// --- 其他函数占位符 (保持不变) ---

// --- Effect Resolution ---
// context can contain { targetUid: "some-uid" } for targeted effects
function resolveEffects(cardInstance, triggerType, context = {}) {
    if (!cardInstance || !cardInstance.effects || !Array.isArray(cardInstance.effects)) {
        return; // No effects to resolve or invalid effects array
    }

    const ownerPlayer = getPlayerState(cardInstance.ownerId);
    if (!ownerPlayer) {
         console.error(`GM: ResolveEffects failed - Cannot find owner player state for ID: ${cardInstance.ownerId}`);
         return;
    }

    console.log(`GM: Resolving effects for ${cardInstance.name} (UID: ${cardInstance.uid}) triggered by: ${triggerType}, Context:`, JSON.stringify(context)); // Log context

    cardInstance.effects.forEach(effect => {
        // Basic check for effect object existence and trigger match
        if (effect && effect.trigger === triggerType) {
            console.log(`GM: Processing ${triggerType} effect:`, JSON.stringify(effect)); // Log the effect being processed

            try { // Add error handling for each effect resolution
                switch (effect.action) {
                    case 'drawCards':
                        if (typeof effect.value === 'number' && effect.value > 0) {
                            console.log(`GM: Executing drawCards(${effect.value}) for player ${ownerPlayer.id}.`);
                            ownerPlayer.drawCard(effect.value);
                            // Note: renderGame will be called later in the calling function (e.g., playCard)
                        } else {
                            console.warn(`GM: Invalid 'value' for drawCards effect:`, effect.value);
                        }
                        break;
                    case 'dealDamage':
                        const targetUid = context?.targetUid; // Get target from context
                        if (!targetUid) {
                            console.error(`GM: 'dealDamage' effect requires targetUid in context, but none provided for ${cardInstance.name}. Context:`, JSON.stringify(context));
                            break; // Skip this effect if no target
                        }
                        const targetInstance = findCardInstanceByUid(targetUid);
                        if (!targetInstance) {
                            console.error(`GM: 'dealDamage' target UID ${targetUid} not found.`);
                            break; // Skip if target not found
                        }
                        const damage = effect.value || 0;
                        if (typeof damage === 'number' && damage > 0) {
                            console.log(`GM: Dealing ${damage} damage to ${targetInstance.name} (UID: ${targetUid}). Current HP: ${targetInstance.currentHealth}`);
                            targetInstance.currentHealth -= damage;
                            showDamageNumber(targetUid, damage); // Show damage number in UI
                            console.log(`GM: ${targetInstance.name} HP after damage: ${targetInstance.currentHealth}`);

                            // Handle destruction only for Followers here. Leaders handled by checkGameOver after all effects.
                            if (targetInstance.currentHealth <= 0 && (targetInstance.type === 'Follower' || targetInstance.type === 'Follower_Evolved')) {
                                console.log(`GM: Target ${targetInstance.name} destroyed by damage.`);
                                handleDestruction(targetInstance); // Use the existing helper
                            } else if (targetInstance.currentHealth <= 0 && targetInstance.type === 'Leader') {
                                 console.log(`GM: Target Leader ${targetInstance.ownerId} health dropped to ${targetInstance.currentHealth}. Game over check will handle.`);
                            }
                        } else {
                             console.warn(`GM: Invalid 'value' for dealDamage effect:`, effect.value);
                        }
                        break;
                    // TODO: Add cases for other actions (e.g., giveKeyword, modifyStats)
                    default:
                        console.warn(`GM: Unhandled effect action in ${triggerType}: ${effect.action}`);
                }
            } catch (error) {
                console.error(`GM: Error resolving ${triggerType} effect action '${effect.action}' for ${cardInstance.name}:`, error);
            }
        }
    });
}
// 出牌逻辑
// Helper to validate if a target instance matches the spell's requirements
export function isValidSpellTarget(targetInstance, validTargets, castingPlayerId) {
    if (!targetInstance || !Array.isArray(validTargets) || validTargets.length === 0) {
        console.warn("isValidSpellTarget: Invalid input - targetInstance or validTargets missing/empty.");
        return false; // Basic sanity checks
    }

    const targetType = targetInstance.type; // e.g., 'Follower', 'Leader', 'Follower_Evolved'
    const targetOwnerId = targetInstance.ownerId;
    const isAlly = targetOwnerId === castingPlayerId;
    const isEnemy = targetOwnerId !== castingPlayerId;

    console.log(`isValidSpellTarget Check: TargetType=${targetType}, TargetOwner=${targetOwnerId}, Caster=${castingPlayerId}, IsEnemy=${isEnemy}, Required=${validTargets.join(', ')}`);

    for (const validTarget of validTargets) {
        switch (validTarget) {
            case 'EnemyFollower':
                if (isEnemy && (targetType === 'Follower' || targetType === 'Follower_Evolved')) {
                    console.log("isValidSpellTarget: Matched EnemyFollower");
                    return true;
                }
                break;
            case 'EnemyLeader':
                if (isEnemy && targetType === 'Leader') {
                    console.log("isValidSpellTarget: Matched EnemyLeader");
                    return true;
                }
                break;
            case 'AllyFollower':
                 if (isAlly && (targetType === 'Follower' || targetType === 'Follower_Evolved')) {
                     console.log("isValidSpellTarget: Matched AllyFollower");
                     return true;
                 }
                break;
            case 'AllyLeader':
                 if (isAlly && targetType === 'Leader') {
                     console.log("isValidSpellTarget: Matched AllyLeader");
                     return true;
                 }
                break;
             case 'AnyFollower': // Example of potentially targeting any follower
                 if (targetType === 'Follower' || targetType === 'Follower_Evolved') {
                      console.log("isValidSpellTarget: Matched AnyFollower");
                      return true;
                 }
                 break;
             case 'AnyLeader': // Example of potentially targeting any leader
                 if (targetType === 'Leader') {
                      console.log("isValidSpellTarget: Matched AnyLeader");
                      return true;
                 }
                 break;
            // Add more cases as needed (e.g., 'EnemyCharacter', 'AllyCharacter')
            default:
                console.warn(`isValidSpellTarget: Unknown validTarget type '${validTarget}'`);
                break;
        }
    }

    console.log("isValidSpellTarget: No criteria matched.");
    return false; // If no criteria matched
}
export function playCard(cardUid, targetUid = null) { // Keep targetUid for future use
    if (gameState.gameOver) {
        console.warn("GM: Cannot play card - Game is over.");
        return;
    }
    console.log(`GM: Attempting to play card UID: ${cardUid}`);

    const cardInstance = findCardInstanceByUid(cardUid);
    if (!cardInstance) {
        console.error(`GM: PlayCard failed - Card instance ${cardUid} not found.`);
        return; // Exit if instance not found
    }

    if (cardInstance.location !== 'hand') {
         console.warn(`GM: PlayCard failed - Card ${cardUid} (${cardInstance.name}) is not in hand (Location: ${cardInstance.location}).`);
         return; // Exit if not in hand
    }

    const playerState = getPlayerState(cardInstance.ownerId);
    if (!playerState) {
        console.error(`GM: PlayCard failed - Player state not found for owner ${cardInstance.ownerId}.`);
        return; // Exit if player state not found
    }

    // --- Pre-condition Checks ---
    if (playerState.id !== gameState.currentPlayerId) {
        console.warn(`GM: PlayCard failed - Not player ${playerState.id}'s turn.`);
        // alert("现在不是你的回合！"); // Optional UI feedback
        return;
    }
    // TODO: 确认游戏阶段检查，暂时只允许 'Main'
    if (gameState.gamePhase !== 'Main') {
        console.warn(`GM: PlayCard failed - Can only play cards during Main phase (Current: ${gameState.gamePhase}).`);
        // alert("只能在主阶段出牌！"); // Optional UI feedback
        return;
    }
    if (playerState.pp < cardInstance.cost) {
        console.warn(`GM: PlayCard failed - Not enough PP for ${cardInstance.name} (Cost: ${cardInstance.cost}, Have: ${playerState.pp}).`);
        // Note: UI should have already alerted for this, but check anyway.
        return;
    }

    // --- Card Type Specific Logic ---
    if (cardInstance.type === 'Follower') {
        // Check field space
        if (playerState.field.length >= MAX_FIELD_SIZE) {
            console.warn(`GM: PlayCard (Follower) failed - Field is full (${playerState.field.length}/${MAX_FIELD_SIZE}).`);
            alert("战场已满，无法召唤更多随从！");
            return;
        }

        // All checks passed for Follower - Execute play
        console.log(`GM: Playing Follower ${cardInstance.name} (UID: ${cardUid}).`);

        // 1. Deduct PP
        playerState.pp -= cardInstance.cost;
        console.log(`GM: PP updated for ${playerState.id}: ${playerState.pp}`);

        // 2. Remove from hand
        const handIndex = playerState.hand.findIndex(card => card && card.uid === cardUid);
        if (handIndex > -1) {
            playerState.hand.splice(handIndex, 1);
            console.log(`GM: Card ${cardUid} removed from hand.`);
        } else {
            console.error(`GM: Critical Error - Card ${cardUid} passed checks but not found in hand for removal!`);
            // Attempt to continue? Or stop? For now, continue but log error.
        }

        // 3. Set instance state for field
        cardInstance.location = 'field';
        // Summoning sickness check: Allow Storm followers to attack immediately
        const hasStorm = cardInstance.keywords?.includes('Storm');
        cardInstance.canAttack = hasStorm; // True if Storm, False otherwise
        console.log(`GM: Follower ${cardInstance.name} played. Has Storm: ${hasStorm}. Can Attack: ${cardInstance.canAttack}`);
        cardInstance.isEvolved = false; // Ensure it's not evolved on play
        // Reset current stats to base, could be modified by Fanfare later
        cardInstance.currentAttack = cardInstance.attack;
        cardInstance.currentHealth = cardInstance.health;
        console.log(`GM: Card ${cardUid} state updated for field.`);

        // 4. Add to field
        playerState.field.push(cardInstance);
            cardInstance.turnPlayed = gameState.turnNumber; // Track turn played for summoning sickness rules <-- CORRECTED
        console.log(`GM: Card ${cardUid} added to field.`);

        // 5. Trigger Fanfare effects
        resolveEffects(cardInstance, 'Fanfare');

        // 6. Render Update
         console.log("GM: Calling renderGame after playing follower.");
         renderGame(gameState); // Update the UI

    } else if (cardInstance.type === 'Spell') {
        // --- Spell Logic Implementation (REVISED FLOW) ---
        console.log(`GM: Playing Spell ${cardInstance.name} (UID: ${cardUid}). Provided target UID: ${targetUid}`);

        // --- Get Card Data ---
        const cardData = getCardDefinition(cardInstance.id); // Get base data for checks
        if (!cardData) {
            // This case should be rare, as the card instance exists.
            console.error(`GM: PlayCard failed - Could not find card definition for existing spell instance ID: ${cardInstance.id}`);
            return; // Cannot proceed without card data
        }

        // --- Pre-Validation: Targeting ---
        let context = {}; // Initialize context
        if (cardData.requiresTarget) {
            console.log(`GM: Spell ${cardInstance.name} requires target. Validating target UID: ${targetUid}`);
            if (!targetUid) {
                console.warn(`GM: PlayCard REJECTED - Targeting spell ${cardInstance.name} played without a target UID.`);
                alert(`法术 ${cardInstance.name} 需要一个目标！`); // User feedback
                return; // <<< EXIT EARLY: Target required but not provided
            }

            const targetInstance = findCardInstanceByUid(targetUid);
            if (!targetInstance) {
                console.warn(`GM: PlayCard REJECTED - Target UID ${targetUid} not found for spell ${cardInstance.name}.`);
                alert(`选择的目标无效！`);
                return; // <<< EXIT EARLY: Target instance not found
            }

            if (!isValidSpellTarget(targetInstance, cardData.validTargets || [], playerState.id)) {
                 console.warn(`GM: PlayCard REJECTED - Target ${targetInstance.name} (Type: ${targetInstance.type}, Owner: ${targetInstance.ownerId}) is not a valid target for spell ${cardInstance.name}. Valid: ${cardData.validTargets.join(', ')}`);
                 alert(`选择的 [${targetInstance.name}] 不是 ${cardInstance.name} 的有效目标！`);
                 return; // <<< EXIT EARLY: Target instance invalid
            }

            // Target is valid! Set context.
            console.log(`GM: Target ${targetInstance.name} (UID: ${targetUid}) validated successfully for spell ${cardInstance.name}.`);
            context = { targetUid: targetUid }; // Set context for resolveEffects
        }
        // --- End Pre-Validation ---

        // --- Resource Consumption (Only if target validation passed or wasn't needed) ---

        // 1. Deduct PP
        playerState.pp -= cardInstance.cost;
        console.log(`GM: PP updated for ${playerState.id}: ${playerState.pp}`);

        // 2. Remove from hand
        const handIndex = playerState.hand.findIndex(card => card && card.uid === cardUid);
        let removedCard = null;
        if (handIndex > -1) {
            removedCard = playerState.hand.splice(handIndex, 1)[0]; // Get the removed card instance
            console.log(`GM: Spell ${cardUid} removed from hand.`);
        } else {
            // This is inconsistent state, should not happen if checks passed
            console.error(`GM: Critical Error - Spell ${cardUid} passed PP/Target checks but not found in hand for removal!`);
            // Attempt to rollback PP
            playerState.pp += cardInstance.cost;
            console.log(`GM: PP Refunded due to removal error for ${playerState.id}: ${playerState.pp}`);
            // Re-render to show correct PP? Or let subsequent render handle?
            renderGame(gameState); // Render to reflect refunded PP
            return; // Stop processing
        }

        // --- Effect Resolution ---
        if (removedCard) { // Should always be true here now
            // No need to check targetValid again, as we returned early if it was invalid.
             console.log(`GM: Resolving OnPlay effects for ${removedCard.name}...`);
             resolveEffects(removedCard, 'OnPlay', context); // Pass context (empty or with targetUid)

            // --- Move to Graveyard ---
            removedCard.location = 'graveyard';
            playerState.graveyard.push(removedCard);
            console.log(`GM: Spell ${removedCard.name} (UID: ${removedCard.uid}) moved to graveyard.`);
        } else {
            // Defensive coding, should be unreachable due to earlier return
             console.error(`GM: Logic Error - removedCard is null after successful removal check and target validation.`);
        }

        // --- Clear Selection & Render Update ---
        gameState.selectedCardUid = null; // Clear hand selection AFTER successful play
        console.log("GM: Calling renderGame after playing spell.");
        renderGame(gameState); // Update the UI

    } else if (cardInstance.type === 'Amulet') {
        // TODO: Implement Amulet logic
         console.log(`GM: Playing Amulet ${cardInstance.name} (UID: ${cardUid}) - Logic Pending.`);
         alert(`尝试使用护符牌 ${cardInstance.name} (功能待实现)`);
         // Deduct PP, remove from hand, add to field (or special zone), render

    } else {
        console.error(`GM: PlayCard failed - Unknown card type: ${cardInstance.type}`);
        alert(`未知的卡牌类型: ${cardInstance.type}`);
    }
}
// 发起攻击 (选择我方攻击者)
export function initiateAttack(attackerUid) {
    console.log(`GM: Attempting to initiate attack with UID: ${attackerUid}`);

    if (gameState.gameOver) {
        console.warn("GM: Cannot initiate attack - Game is over.");
        return;
    }

    if (gameState.gamePhase !== 'Main') {
        console.warn(`GM: Cannot initiate attack - Not in Main phase (Current: ${gameState.gamePhase}).`);
        // alert("只能在主阶段攻击！"); // Optional UI feedback
        return;
    }

    const attackerInstance = findCardInstanceByUid(attackerUid);
    if (!attackerInstance) {
        console.error(`GM: InitiateAttack failed - Attacker instance ${attackerUid} not found.`);
        return;
    }

    if (attackerInstance.ownerId !== gameState.currentPlayerId) {
        console.warn(`GM: InitiateAttack failed - Card ${attackerUid} does not belong to current player (${gameState.currentPlayerId}).`);
        return;
    }

    if (attackerInstance.location !== 'field') {
         console.warn(`GM: InitiateAttack failed - Card ${attackerUid} (${attackerInstance.name}) is not on the field (Location: ${attackerInstance.location}).`);
         return;
    }

console.log('GM_DBG: Checking attacker instance before canAttack check:', JSON.stringify(attackerInstance)); // Log instance state
    if (!attackerInstance.canAttack) {
        console.warn(`GM: InitiateAttack failed - Follower ${attackerUid} (${attackerInstance.name}) cannot attack (Summoning sickness or already attacked).`);
        // alert("该随从本回合无法攻击！"); // Optional UI feedback
        return;
    }

    // Optional: Handle clicking the same attacker again to de-select?
    // if (gameState.selectedAttackerUid === attackerUid) {
    //     console.log(`GM: Deselecting attacker ${attackerUid}.`);
    //     gameState.selectedAttackerUid = null;
    // } else {
         console.log(`GM: Selecting attacker: ${attackerInstance.name} (UID: ${attackerUid})`);
         gameState.selectedAttackerUid = attackerUid;
    // }

    // Trigger UI update to highlight attacker and potential targets
    console.log("GM: Calling renderGame after initiating attack.");
    renderGame(gameState);
}

// 选择攻击目标 (敌方随从或英雄)
export function selectAttackTarget(targetUid) {
    console.log(`GM: Attempting to select target UID: ${targetUid}`);
    const attackerUid = gameState.selectedAttackerUid;

    // Validation 1: Check if an attacker is selected
    if (!attackerUid) {
        console.warn("GM: SelectTarget failed - No attacker currently selected.");
        // This might happen if the user clicks an opponent card without selecting an attacker first.
        return;
    }

    // Find instances
    const attackerInstance = findCardInstanceByUid(attackerUid);
    const targetInstance = findCardInstanceByUid(targetUid);

    // Validation 2: Check instances exist
    if (!attackerInstance) {
         console.error(`GM: SelectTarget failed - Selected attacker instance ${attackerUid} not found!`);
         gameState.selectedAttackerUid = null; // Reset selection if attacker is invalid
         renderGame(gameState); // Update UI to remove highlight
         return;
    }
    if (!targetInstance) {
        console.error(`GM: SelectTarget failed - Target instance ${targetUid} not found!`);
        return;
    }

    // Validation 3: Check target is opponent's and on field OR is the opponent's leader
    if (targetInstance.ownerId === gameState.currentPlayerId) {
         console.warn(`GM: SelectTarget failed - Cannot target friendly unit ${targetUid}.`);
         return;
    }
    // Allow targeting 'field' or 'leader_zone'
    if (targetInstance.location !== 'field' && targetInstance.location !== 'leader_zone') {
        console.warn(`GM: SelectTarget failed - Target ${targetUid} is not on field or leader zone (Location: ${targetInstance.location}).`);
        return;
    }

    // Validation 4: Check target is a valid attack target type
    const validTargetTypes = ['Follower', 'Follower_Evolved', 'Leader'];
    if (!validTargetTypes.includes(targetInstance.type)) {
         console.warn(`GM: SelectTarget failed - Target ${targetUid} is not an attackable type (Type: ${targetInstance.type}).`);
         return;
    }

    // REMOVED: Redundant Summoning Sickness Check for Leader Target.
    // The general 'canAttack' flag (set in playCard based on Storm,
    // and checked in initiateAttack) now handles this correctly for all targets.

    // Validation 5: Check for Ward (守护)
    const opponentPlayer = getPlayerState(targetInstance.ownerId); // Get the owner of the target
    if (!opponentPlayer) {
         console.error(`GM: SelectTarget failed - Could not find opponent player state for ID: ${targetInstance.ownerId}`);
         return;
    }

    // Check if opponent has any follower with Ward on the field
    const opponentHasWard = opponentPlayer.field.some(card =>
        card &&
        (card.type === 'Follower' || card.type === 'Follower_Evolved') &&
        card.keywords?.includes('Ward')
    );

    // If opponent has Ward, enforce targeting rules
    if (opponentHasWard) {
        const targetIsFollower = targetInstance.type === 'Follower' || targetInstance.type === 'Follower_Evolved';
        const targetHasWard = targetInstance.keywords?.includes('Ward');

        // Valid targets when opponent has Ward: Only followers that also have Ward
        if (!targetIsFollower || !targetHasWard) {
             console.warn(`GM: SelectTarget failed - Cannot target ${targetInstance.name} (${targetInstance.type}) because opponent has Ward follower(s). Must target a Ward follower first.`);
             alert("必须先攻击带有【守护】的随从！");
             return; // Reject the target selection
        }
        // If target is a follower with Ward, the check passes.
        console.log(`GM: Ward check passed - Target ${targetInstance.name} is a valid Ward target.`);
    }
    // If opponent does NOT have Ward, any valid target (follower or leader, respecting other rules) is fine.


    // All validations passed (including Ward check)
    console.log(`GM: Attack target selected - ${attackerInstance.name} (${attackerUid}) will attack ${targetInstance.name} (${targetUid})`);

    // Execute the attack (this will now handle both follower and leader targets)
    executeAttack(attackerUid, targetUid); // Pass control to the updated executeAttack

    // Post-attack state updates (These happen AFTER executeAttack completes)
    attackerInstance.canAttack = false; // Mark as attacked this turn
    gameState.selectedAttackerUid = null; // Clear selection

    // Trigger UI update
    console.log("GM: Calling renderGame after selecting attack target.");
    renderGame(gameState);
}

// --- Destruction Helper ---
function handleDestruction(cardInstance) {
    if (!cardInstance) return;
    // Avoid handling destruction for leaders here, game over logic handles it.
    if (cardInstance.type === 'Leader') return;

    console.log(`GM: Handling destruction of ${cardInstance.name} (UID: ${cardInstance.uid})`);

    const ownerPlayer = getPlayerState(cardInstance.ownerId);
    if (!ownerPlayer) {
        console.error(`GM: Cannot handle destruction, owner player ${cardInstance.ownerId} not found.`);
        return;
    }

    // Remove from field
    const fieldIndex = ownerPlayer.field.findIndex(card => card && card.uid === cardInstance.uid);
    if (fieldIndex > -1) {
        const destroyedCard = ownerPlayer.field.splice(fieldIndex, 1)[0]; // Get the removed card
        if (destroyedCard) {
            destroyedCard.location = 'graveyard';
            // Reset stats? Or keep them as they were upon destruction? Keeping them for now.
            ownerPlayer.graveyard.push(destroyedCard);
            console.log(`GM: ${destroyedCard.name} (UID: ${destroyedCard.uid}) removed from field and added to graveyard.`);

             // TODO: Trigger Last Words effects here
             // executeEffects(destroyedCard, 'LastWords', ownerPlayer.id);
        }
    } else {
        console.warn(`GM: Card ${cardInstance.uid} marked for destruction but not found on owner's field.`);
    }
}

// --- Combat Execution ---
export function executeAttack(attackerUid, targetUid) {
    console.log(`GM: Executing attack - Attacker: ${attackerUid}, Target: ${targetUid}`);
    const attacker = findCardInstanceByUid(attackerUid);
    const target = findCardInstanceByUid(targetUid);

    // Basic validation
    if (!attacker || !target) {
        console.error(`GM: ExecuteAttack failed - Attacker (found: ${!!attacker}) or Target (found: ${!!target}) not found.`);
        if (gameState.selectedAttackerUid === attackerUid) {
             gameState.selectedAttackerUid = null; // Clear selection if invalid
        }
        // Avoid rendering here? selectAttackTarget should have done necessary UI cleanup if target was invalid.
        return;
    }

    // --- Pre-attack state log ---
    const attackerInitialHealth = attacker.currentHealth;
    const targetInitialHealth = target.currentHealth;
    const attackerAttack = attacker.currentAttack || 0; // Ensure attack is not null/undefined
    const targetAttack = (target.type === 'Follower' || target.type === 'Follower_Evolved') ? (target.currentAttack || 0) : 0; // Leaders don't attack back

    console.log(`GM: Combat Start - ${attacker.name}(${attackerAttack}/${attackerInitialHealth}) vs ${target.name}(${targetAttack}/${targetInitialHealth})`);

    let attackerDestroyed = false;
    let targetDestroyed = false;

    // --- Damage Calculation & Application ---
    if (target.type === 'Follower' || target.type === 'Follower_Evolved') {
        // --- Follower vs Follower ---
        console.log(`GM: Dealing ${attackerAttack} damage to Follower ${target.name}.`);
        target.currentHealth -= attackerAttack;
        showDamageNumber(target.uid, attackerAttack); // Show damage number in UI
        console.log(`GM: ${target.name} health after damage: ${target.currentHealth}`);

        console.log(`GM: Dealing ${targetAttack} damage back to Attacker ${attacker.name}.`);
        attacker.currentHealth -= targetAttack;
        showDamageNumber(attacker.uid, targetAttack); // Show damage number in UI
        console.log(`GM: ${attacker.name} health after damage: ${attacker.currentHealth}`);

        // Check destruction after combat
        if (target.currentHealth <= 0) {
             targetDestroyed = true;
             console.log(`GM: Target Follower ${target.name} destroyed.`);
        }
        if (attacker.currentHealth <= 0) {
             attackerDestroyed = true;
             console.log(`GM: Attacker Follower ${attacker.name} destroyed.`);
        }

    } else if (target.type === 'Leader') {
        // --- Follower vs Leader ---
        console.log(`GM: Dealing ${attackerAttack} damage to Leader ${target.name}.`);
        target.currentHealth -= attackerAttack; // Target IS the leader instance
        showDamageNumber(target.uid, attackerAttack); // Show damage number in UI
        console.log(`GM: Leader ${target.ownerId} health after damage: ${target.currentHealth}`);

        // Check if leader health dropped to 0 or below
        if (target.currentHealth <= 0) {
            console.log(`GM: Leader ${target.ownerId} has been defeated.`);
            // checkGameOver will handle setting the game state flags
            targetDestroyed = true; // conceptually, for flow
        }
        // Attacker takes no damage from leader in standard combat
        // Still check if attacker died from other effects (e.g., reflect?) - unlikely now
        if (attacker.currentHealth <= 0) {
             attackerDestroyed = true;
             console.log(`GM: Attacker Follower ${attacker.name} destroyed.`);
        }
    } else {
         console.error(`GM: ExecuteAttack failed - Target type ${target.type} is not attackable.`);
         // Clear selection?
          if (gameState.selectedAttackerUid === attackerUid) {
             gameState.selectedAttackerUid = null;
         }
         return; // Exit if target type invalid
    }

    // --- Post-Combat State Updates & Cleanup ---
    attacker.canAttack = false; // Mark attacker as having attacked this turn
    gameState.selectedAttackerUid = null; // Clear selection state regardless of outcome
    console.log(`GM: Attacker ${attacker.name} canAttack set to false. Selection cleared.`);

    // Handle Destruction (only for followers via helper)
    if (targetDestroyed && target.type !== 'Leader') {
         handleDestruction(target);
    }
    if (attackerDestroyed) {
         handleDestruction(attacker);
    }

    // Check Game Over (important after any health change)
    const isGameOver = checkGameOver(); // This should update gameState.gameOver and gameState.winner

    // --- Final Render ---
    // Always render to reflect health changes, destruction, and removal of highlights.
    console.log("GM: Calling renderGame after executing attack.");
    renderGame(gameState);

    // Optional: TODO - Trigger post-attack effects (e.g., Drain, Bane) here?
}
// --- Cancel Selection ---
    export function cancelSelection() {
        console.log("GM: Cancelling selection.");
        if (gameState.selectedAttackerUid) {
            gameState.selectedAttackerUid = null;
            console.log("GM: Attacker selection cancelled.");
             // Trigger UI update to remove highlights
             renderGame(gameState);
        } else {
             console.log("GM: Cancel selection called, but nothing was selected.");
        }
        // TODO: Add cancellation for selectedCardUid if needed later
    }


// 检查游戏是否结束
// 检查游戏是否结束 (Refactored to check leaderCard health)
export function checkGameOver(forcedLoserId = null, reason = null) {
    // Avoid redundant checks if game already ended
    if (gameState.gameOver) {
        console.log("GM: checkGameOver called, but game is already over.");
        return true;
    }

    let loserId = forcedLoserId;
    let gameOverReason = reason ? ` (${reason})` : ' (HP 归零)'; // Default reason if not provided

    // If not forced, check HP condition
    if (!loserId) {
        // console.log("GM: Checking HP for game over condition..."); // Can be noisy
        for (const player of gameState.players) {
            if (player && player.leaderCard && player.leaderCard.currentHealth <= 0) {
                loserId = player.id;
                console.log(`GM: Detected HP <= 0 for player ${loserId}.`);
                break; // Found the loser by HP
            }
        }
    } else {
         console.log(`GM: Game over triggered externally for loser ${loserId} due to ${reason || 'unknown reason'}.`);
    }

    // If a loser was determined (either by force or by HP check)
    if (loserId) {
        gameState.gameOver = true;
        gameState.gamePhase = 'GameOver';
        // Determine the winner
        const winnerPlayer = gameState.players.find(p => p && p.id !== loserId);
        if (winnerPlayer) {
            gameState.winner = winnerPlayer.id;
            console.log(`GM: Game Over! Loser: ${loserId}, Winner: ${gameState.winner}, Reason: ${gameOverReason.trim()}`);
            // Simple alert for now, UI should handle a proper game over screen
            alert(`游戏结束！ 玩家 ${gameState.winner} 获胜！${gameOverReason}`);
        } else {
            console.error("GM: Game over condition met, but could not determine the winner.");
            gameState.winner = 'Unknown';
            alert(`游戏结束！无法确定胜者。 原因: ${gameOverReason.trim()}`);
        }
        return true; // Game has ended
    }

    // If no loser was determined
    // console.log("GM: Game continues, no loser determined."); // Can be noisy
    return false; // Game is not over
}

// 进化随从
// 进化随从 (Implemented Version)
    export function evolveCard(cardUid) {
        if (gameState.gameOver) {
            console.warn("GM: Cannot evolve card - Game is over.");
            return;
        }
        console.log(`GM: Attempting to evolve card UID: ${cardUid}`);

        const playerState = getPlayerState(gameState.currentPlayerId);
        const cardInstance = findCardInstanceByUid(cardUid);
        
        // --- Basic Validations ---
        if (!playerState) {
            console.error("GM: EvolveCard failed - Cannot get current player state.");
            return;
        }
        if (!cardInstance) {
            console.error(`GM: EvolveCard failed - Card instance ${cardUid} not found.`);
            return;
        }
        // Need base card data to check can_evolve and evolves_into
        const cardData = getCardDefinition(cardInstance.id); // Use .id property from instance 
         if (!cardData) {
            console.error(`GM: EvolveCard failed - Base card data not found for ID: ${cardInstance.cardId}.`);
            return;
        }

        // --- Condition Checks ---
        let failReason = null;
        if (gameState.gamePhase !== 'Main') { // Can only evolve in Main phase normally
            failReason = `Cannot evolve outside Main phase (Current: ${gameState.gamePhase}).`;
        } else if (!playerState.canEvolveThisTurn) {
            failReason = "Evolution not unlocked or already used this turn.";
        } else if (playerState.ep < 1) { // Assuming evolve costs 1 EP
            failReason = `Not enough EP (Have: ${playerState.ep}, Need: 1).`;
        } else if (cardInstance.ownerId !== playerState.id) {
            failReason = "Cannot evolve opponent's card.";
        } else if (cardInstance.location !== 'field') {
            failReason = `Card not on field (Location: ${cardInstance.location}).`;
        } else if (cardInstance.type !== 'Follower') { 
             failReason = `Cannot evolve non-Follower type (${cardInstance.type}).`;
        } else if (cardInstance.isEvolved === true) { // Check INSTANCE state
             failReason = `Card is already evolved.`;
        } else if (cardData.can_evolve !== true) { // Check BASE DATA property
            failReason = "Card definition does not allow evolution.";
        } else if (!cardData.evolves_into || cardData.evolves_into.length === 0) {
            failReason = "Card definition missing 'evolves_into' data.";
        }

        if (failReason) {
            console.warn(`GM: EvolveCard failed for ${cardInstance.name} (${cardUid}): ${failReason}`);
            // Optional: alert(`无法进化：${failReason}`);
            return;
        }

        // --- Branching (Simplified: Use first path) ---
        // TODO: Implement UI choice if cardData.evolves_into.length > 1
        const targetEvolvedCardId = cardData.evolves_into[0];
        const newCardData = getCardDefinition(targetEvolvedCardId);

        if (!newCardData) {
             console.error(`GM: EvolveCard failed - Evolved card data not found for ID: ${targetEvolvedCardId}.`);
             return;
        }

        console.log(`GM: Evolving ${cardInstance.name} (${cardUid}) into ${newCardData.name} (${targetEvolvedCardId})...`);

        // --- Execute Evolution ---
        if (!playerState.spendEP(1)) { // Use the method from player.js to decrement EP
             console.error(`GM: EvolveCard failed - spendEP reported failure unexpectedly.`); // Should have been caught by check above
             return;
        }
        playerState.canEvolveThisTurn = false; // Consume the one evolution allowed per turn

        // Update Card Instance State
        cardInstance.id = targetEvolvedCardId; // Update .id property // Link instance to the new card data ID
        cardInstance.name = newCardData.name;
        cardInstance.baseAttack = newCardData.attack; // Update BASE stats
        cardInstance.baseHealth = newCardData.health;
        cardInstance.abilities = newCardData.abilities ? [...newCardData.abilities] : []; // Deep copy abilities array
        cardInstance.art = newCardData.art; // Update art reference
        cardInstance.description = newCardData.description;
        cardInstance.isEvolved = true; // Mark THIS INSTANCE as evolved

        // HP Handling: Heal to new max health for simplicity (Shadowverse rules often do this)
        cardInstance.currentHealth = cardInstance.baseHealth;
        // Attack Handling: Reset currentAttack to new baseAttack (Assume buffs are lost for now)
        cardInstance.currentAttack = cardInstance.baseAttack;

        // Allow evolved follower to attack this turn (mimics typical Rush-on-evolve behavior)
        // Can be refined later based on specific card having Rush or not.
        cardInstance.canAttack = true; 

        console.log(`GM: ${cardInstance.name} (UID: ${cardUid}) successfully evolved. State updated.`);

        // TODO: Trigger "on Evolve" effects here
        // executeEffects(cardInstance, 'OnEvolve', playerState.id);

        // --- Final Render ---
        renderGame(gameState); // Update UI to show evolved state
    }
// Duplicate evolveCard function removed to fix SyntaxError