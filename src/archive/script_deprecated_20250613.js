// DEPRECATED: This file has been replaced by the ES6 module system in src/
// Moved to archive on 2025-06-13 due to conflicts with new gameManager.js
// The new frontend uses src/main.js as entry point with ES6 modules

'use strict';

console.log("游戏脚本 script.js 加载成功！");

// --- 全局变量和常量 ---
const gameContainer = document.getElementById('game-container');
// 稍后会添加更多，例如玩家状态、卡牌数据、游戏阶段等
let gameState = {
    players: {}, // Stores player objects keyed by ID (e.g., 'player1', 'player2')
    currentPlayerId: null,
    turnNumber: 0,
    gamePhase: 'initialization', // e.g., 'initialization', 'main', 'battle', 'end'
    gameOver: false,
    winner: null,
    cardDatabase: [], // Will hold loaded card data
};

const PLAYER1_ID = 'player1';
const PLAYER2_ID = 'player2'; // Represents the AI for now
const STARTING_HEALTH = 20;
const STARTING_HAND_SIZE = 3; // Player 1 starts with this
const MAX_HAND_SIZE = 9;
const MAX_FIELD_SIZE = 5;
let secondPlayerId = ''; // To easily check who went second for draw rule

// --- 游戏初始化 ---
function initGame() {
    console.log("--- 初始化游戏 ---");
    gameState.gameOver = false;
    gameState.winner = null;
    gameState.turnNumber = 0;
    gameState.gamePhase = 'initialization'; // Reset phase

    // NOTE: Card data should have been loaded by loadCardData before this runs.

    // Create placeholder decks if cardDatabase is empty or failed to load
    let player1DeckData = [];
    let player2DeckData = [];
    if (!gameState.cardDatabase || gameState.cardDatabase.length === 0) {
        console.warn("Card database is empty or failed to load. Using placeholder decks.");
        // Create simple, distinct placeholders
        const p1PlaceholderCard = { id: `p1_placeholder`, name: `P1 Placeholder`, cost: 1, type: 'Follower', attack: 1, health: 1 };
        const p2PlaceholderCard = { id: `p2_placeholder`, name: `P2 Placeholder`, cost: 1, type: 'Follower', attack: 1, health: 1 };
        player1DeckData = Array(20).fill(null).map((_, i) => ({ ...p1PlaceholderCard, id: `p1_ph_${i}`}));
        player2DeckData = Array(20).fill(null).map((_, i) => ({ ...p2PlaceholderCard, id: `p2_ph_${i}`}));
    } else {
        console.log("Using card data from cardDatabase to build decks...");
        // TODO: Implement actual deck building/selection logic using cardDatabase
        // For now, just use the first 20 cards from the database if available
        // Ensure we don't mutate the original cardDatabase objects directly in deck
        player1DeckData = gameState.cardDatabase.slice(0, 20).map(card => ({ ...card }));
        player2DeckData = gameState.cardDatabase.slice(0, 20).map(card => ({ ...card })); // AI uses same deck for now
    }


    // 2. 初始化玩家和AI状态
    gameState.players[PLAYER1_ID] = new Player(PLAYER1_ID);
    gameState.players[PLAYER2_ID] = new Player(PLAYER2_ID, true); // Mark player 2 as AI

    // Assign decks (should be arrays of card definitions)
    // TODO: Implement proper shuffle based on a seed for reproducibility if needed
    gameState.players[PLAYER1_ID].deck = [...player1DeckData].sort(() => Math.random() - 0.5); // Basic shuffle
    gameState.players[PLAYER2_ID].deck = [...player2DeckData].sort(() => Math.random() - 0.5); // Basic shuffle
    console.log(`Player 1 deck size: ${gameState.players[PLAYER1_ID].deck.length}`);
    console.log(`Player 2 deck size: ${gameState.players[PLAYER2_ID].deck.length}`);


    // Decide who goes first (randomly for now)
    const firstPlayerId = Math.random() < 0.5 ? PLAYER1_ID : PLAYER2_ID;
    secondPlayerId = firstPlayerId === PLAYER1_ID ? PLAYER2_ID : PLAYER1_ID; // Update global-like variable
    gameState.currentPlayerId = firstPlayerId;
    console.log(`${firstPlayerId} goes first.`);

    // Set initial EP based on first/second player
    gameState.players[PLAYER1_ID].maxEp = firstPlayerId === PLAYER1_ID ? 2 : 3;
    gameState.players[PLAYER2_ID].maxEp = firstPlayerId === PLAYER2_ID ? 2 : 3;
    // Reset current EP
    gameState.players[PLAYER1_ID].ep = 0;
    gameState.players[PLAYER2_ID].ep = 0;
    // Reset hasTakenTurn flag
    gameState.players[PLAYER1_ID].hasTakenTurn = false;
    gameState.players[PLAYER2_ID].hasTakenTurn = false;


    // Draw starting hands (Player 1: 3 cards, Player 2: 3 cards initially)
    console.log("Drawing starting hands...");
    gameState.players[PLAYER1_ID].drawCard(STARTING_HAND_SIZE);
    gameState.players[PLAYER2_ID].drawCard(STARTING_HAND_SIZE);


    // 3. 渲染初始游戏界面
    renderGame(); // Call initial render


    // 4. 开始第一个回合
    console.log("Starting first turn...");
    // Use setTimeout to ensure initial render completes before first turn logic runs
    setTimeout(startTurn, 100); // Short delay
}

/*
// --- 卡牌数据加载 ---
async function loadCardData() {
    // 从 data/card_data.json 获取数据
    console.log("加载卡牌数据...");
    // 存放卡牌数据的逻辑
*/
// --- 卡牌数据加载 (Corrected Implementation) ---
async function loadCardData() { // 使用临时名称以避免冲突
    console.log('Attempting to load card data from data/card_data.json...');
    try {
        const response = await fetch('data/card_data.json');
        // Check if the response is ok (status in the range 200-299)
        if (!response.ok) {
             // If response status is 404 (Not Found) or other error
            if (response.status === 404) {
                console.warn("card_data.json not found (404). Proceeding without card data.");
                gameState.cardDatabase = []; // Ensure it's empty
            } else {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
        } else {
            // Try to parse the JSON response
            const data = await response.json();
            // Check if the parsed data is actually an array
            if (Array.isArray(data)) {
                gameState.cardDatabase = data;
                console.log("Card data loaded successfully:", gameState.cardDatabase.length, "cards found.");
            } else {
                 console.warn("card_data.json does not contain a valid JSON array. Proceeding without card data.");
                 gameState.cardDatabase = []; // Ensure it's empty if format is wrong
            }
        }
    } catch (error) {
        // Catch fetch errors (network issues) or JSON parsing errors
        console.error("Failed to load or parse card_data.json:", error);
        // Optionally display a user-friendly message
        // alert("Error loading card data. Please check the console for details.");
        gameState.cardDatabase = []; // Ensure it's empty on error
    } finally {
        // Ensure initGame is called regardless of whether card data loaded successfully or not
        console.log("Card data loading attempt finished. Initializing game...");
        initGame();
    }
}
// --- 游戏状态管理 ---
class Player {
    constructor(id, isAI = false) {
        this.id = id;
        this.isAI = isAI;
        this.health = STARTING_HEALTH;
        this.pp = 0;
        this.maxPp = 0;
        this.ep = 0;
        this.maxEp = 0; // Will be set based on first/second player during initGame
        this.deck = []; // Array of card *definitions* (plain objects from JSON)
        this.hand = []; // Array of card *instance* objects in hand
        this.field = []; // Array of card *instance* objects on field (max MAX_FIELD_SIZE)
        this.graveyard = []; // Array of card *instance* objects
        this.effects = []; // Player-specific effects - future use
        this.hasTakenTurn = false; // Flag for second player draw rule
    }

    // Draw cards from deck to hand
    drawCard(count = 1) {
        console.log(`${this.id} attempts to draw ${count} card(s).`);
        let drawnCount = 0;
        for (let i = 0; i < count; i++) {
            if (this.deck.length === 0) {
                console.log(`${this.id}'s deck is empty!`);
                // TODO: Implement fatigue damage here later
                break; // Stop drawing
            }
            if (this.hand.length >= MAX_HAND_SIZE) {
                const burntCardData = this.deck.pop(); // Remove card data from deck
                // Create an instance to put in graveyard if needed, or just log
                const burntCardInstance = { ...burntCardData, uid: `burnt-${this.id}-${Date.now()}-${i}` };
                this.graveyard.push(burntCardInstance);
                console.log(`${this.id}'s hand is full! Burnt card: ${burntCardData?.name || 'Unknown'}`);
                // TODO: Optionally trigger "on discard" effects later
            } else {
                const drawnCardData = this.deck.pop(); // Get card data from deck
                // Create a unique instance of the card for the hand
                const cardInstance = {
                    ...drawnCardData, // Copy data from database/deck
                    uid: `${this.id}-hand-${Date.now()}-${i}-${Math.random().toString(16).slice(2)}`, // Unique ID for this specific instance
                    currentHealth: drawnCardData.health, // Add current health for tracking damage
                    canAttack: false, // Reset canAttack status
                    isEvolved: false // Reset evolved status
                    // Add other instance-specific state as needed
                };
                this.hand.push(cardInstance);
                drawnCount++;
            }
        }
        console.log(`${this.id} drew ${drawnCount} card(s). Hand: ${this.hand.length}, Deck: ${this.deck.length}`);
        // Note: renderGame() should be called by the function that triggers drawing (e.g., startTurn)
    }

    // TODO: Add methods for playCard, attack, evolve etc. later
}

// --- 游戏状态管理 ---
// (例如：玩家、AI、回合数、当前阶段等)

// --- 核心游戏循环 ---
function startTurn() {
    // Check for game over condition before starting turn
    if (gameState.gameOver) {
        console.log("Game is over. Cannot start new turn.");
        return;
    }

    gameState.turnNumber++;
    const currentPlayer = gameState.players[gameState.currentPlayerId];
    console.log(`%c--- Turn ${gameState.turnNumber} (${currentPlayer.id}) Start ---`, 'color: blue; font-weight: bold;');

    // --- Start of Turn Effects ---
    // TODO: Process any start-of-turn effects (e.g., amulets)

    // 1. Increment Max PP (up to 10)
    if (currentPlayer.maxPp < 10) {
        currentPlayer.maxPp++;
    }
    // 2. Refill PP
    currentPlayer.pp = currentPlayer.maxPp;

    // 3. Gain EP (Simplified logic: gain all available EP at the start of specific turns)
    const evolveTurnP1 = 5; // Turn number when P1 can evolve
    const evolveTurnP2 = 4; // Turn number when P2 can evolve
    let gainedEP = false;
    const evolveTurn = (gameState.currentPlayerId === PLAYER1_ID) ? evolveTurnP1 : evolveTurnP2;
    if (gameState.turnNumber >= evolveTurn && currentPlayer.ep < currentPlayer.maxEp) {
        currentPlayer.ep = currentPlayer.maxEp; // Give all EP at once
        gainedEP = true;
        console.log(`${currentPlayer.id} gained EP. Total EP: ${currentPlayer.ep}/${currentPlayer.maxEp}`);
    }

    // 4. Draw a card
    // Second player draws an extra card at the start of *their* first turn.
    let drawCount = 1;
    if (gameState.currentPlayerId === secondPlayerId && !currentPlayer.hasTakenTurn) {
        drawCount = 2;
        console.log("Second player draws an extra card on their first turn.");
    }
    currentPlayer.drawCard(drawCount); // drawCard now logs details and updates hand/deck count


    // 5. Set game phase
    gameState.gamePhase = 'main';
    console.log("Phase: main");

    // --- Start of Main Phase Effects ---
    // TODO: Process effects that trigger when main phase begins

    // Reset attack status for field cards (they can attack this turn if they survived previous turn)
    currentPlayer.field.forEach(card => {
        if (card.type === 'Follower') { // Only followers can attack
             card.canAttack = true; // Assuming they weren't just played this turn (handle that in playCard)
        }
    });


    // 6. Update UI
    renderGame(); // Reflect PP, EP, drawn card, etc.

    // 7. Enable actions for the current player and potentially trigger AI
    if (currentPlayer.isAI) {
        console.log("AI's turn to act...");
        // Disable button just in case, although AI shouldn't use it
        const endTurnButton = document.getElementById('end-turn-button');
        if (endTurnButton) endTurnButton.disabled = true;
         // setTimeout(runAILogic, 1500); // Placeholder for AI turn logic call
         // For now, AI immediately ends turn
         console.log("AI thinking... (will end turn shortly)");
         setTimeout(endTurn, 2000); // AI ends turn after 2 seconds
    } else {
         // Enable player actions (e.g., enable end turn button)
         const endTurnButton = document.getElementById('end-turn-button');
         if (endTurnButton) endTurnButton.disabled = false; // Enable player's end turn button
         console.log("Player's turn. Waiting for action or End Turn.");
    }
}

function endTurn() {
     if (gameState.gameOver) return; // Don't proceed if game is over

    const currentPlayerId = gameState.currentPlayerId;
    const currentPlayer = gameState.players[currentPlayerId];
    console.log(`%c--- Turn ${gameState.turnNumber} (${currentPlayer.id}) End ---`, 'color: orange; font-weight: bold;');


    // Set flag indicating player has completed their first turn (for draw rule)
    currentPlayer.hasTakenTurn = true;


    // --- End of Turn Effects ---
    // TODO: Process end-of-turn effects (damage, healing, etc.)

    // Disable current player's actions if it was the human player
    // Disable current player's actions if it was the human player
    if (!currentPlayer.isAI) {
        // Move button disable logic (from current line ~428) here
        const endTurnButton = document.getElementById('end-turn-button');
        if (endTurnButton) endTurnButton.disabled = true;
    } // End of if (!currentPlayer.isAI) block

    // --- End of Turn Cleanup ---
    // (Keep original TODO from line 298)
    // TODO: Process end-of-turn effects (damage, healing, etc.)

    // (Keep original logic from 294)
    currentPlayer.hasTakenTurn = true;

    // Switch player (Moved from current line ~816)
    gameState.currentPlayerId = gameState.currentPlayerId === PLAYER1_ID ? PLAYER2_ID : PLAYER1_ID;

    // Start the next turn after a short delay (Moved from current line ~820)
    console.log("Ending turn, scheduling next turn start..."); // Added log
    setTimeout(startTurn, 500); // Delay before next turn starts
} // This is now the CORRECT closing brace for endTurn function

// 法术效果处理占位符
function executeSpellEffect(spellCard, casterId) {
    console.log(`Executing effects for spell: ${spellCard.name}`);
    if (!spellCard.effects || spellCard.effects.length === 0) {
        console.log("Spell has no defined effects.");
        return;
    }

    const effect = spellCard.effects[0]; // 假设现在只有一个效果

    if (effect.type === 'Damage') {
        // TODO: 实现目标选择界面!
        console.warn("Target selection for Damage spell not implemented yet!");
        // 占位符: 暂时直接伤害对方英雄
        const opponentId = casterId === PLAYER1_ID ? PLAYER2_ID : PLAYER1_ID;
        const opponent = gameState.players[opponentId];
        const damageValue = effect.value || 0;
        if(opponent) {
            console.log(`Dealing ${damageValue} damage to ${opponentId} (placeholder target).`);
            opponent.health -= damageValue;
             console.log(`${opponentId} health is now ${opponent.health}`);
            // TODO: checkGameOver();
        } else {
             console.error("Opponent not found!");
        }

    } else {
        console.log(`Unhandled spell effect type: ${effect.type}`);
    }
    // 后续添加更多效果类型 (抽牌, 治疗, Buff 等)
}
// --- 卡牌交互 ---
function playCard(cardUid) {
    console.log(`Attempting to play card with UID: ${cardUid}`);
    const player = gameState.players[PLAYER1_ID]; // Assume only human player plays cards for now

    // --- 基本检查 ---
    if (gameState.currentPlayerId !== PLAYER1_ID) {
        console.warn("Not your turn!");
        alert("现在不是你的回合！"); // Add user feedback
        return;
    }
    if (gameState.gamePhase !== 'main') {
        console.warn("Cannot play cards outside of main phase!");
         alert("只能在主要阶段出牌！");
        return;
    }

    // 从手牌查找卡牌
    const cardIndex = player.hand.findIndex(card => card.uid === cardUid);
    if (cardIndex === -1) {
        console.error(`Card with UID ${cardUid} not found in hand.`);
        return; // Should not happen if UI is correct
    }
    const cardInstance = player.hand[cardIndex];

    // 检查 PP 费用
    if (player.pp < cardInstance.cost) {
        console.warn(`Not enough PP to play ${cardInstance.name}. Need ${cardInstance.cost}, have ${player.pp}.`);
        alert(`PP 不足！需要 ${cardInstance.cost} PP，你只有 ${player.pp} PP。`);
        return;
    }

    // --- 卡牌类型特定的检查和执行 ---
    if (cardInstance.type === 'Follower') {
        // 检查战场空间
        if (player.field.length >= MAX_FIELD_SIZE) {
            console.warn(`Field is full! Cannot play ${cardInstance.name}.`);
            alert("你的战场已满（最多5个随从）！");
            return;
        }

        // 执行打出随从逻辑
        console.log(`Playing follower: ${cardInstance.name}`);
        player.pp -= cardInstance.cost; // 扣除费用

        // 从手牌移除
        player.hand.splice(cardIndex, 1);

        // 准备战场实例
        cardInstance.canAttack = false; // 召唤失调 (除非有 突进/疾驰)
        cardInstance.isEvolved = false; // 重置进化状态
        // 确保 currentHealth 被初始化
        cardInstance.currentHealth = cardInstance.currentHealth !== undefined ? cardInstance.currentHealth : cardInstance.health;
        // Ensure currentAttack is also initialized for consistency
        cardInstance.currentAttack = cardInstance.currentAttack !== undefined ? cardInstance.currentAttack : cardInstance.attack;


        // 添加到战场
        player.field.push(cardInstance);
        console.log(`${player.id}'s field:`, player.field.map(c => c.name));


        // TODO: 触发 "战吼" (Fanfare / on play) 效果

    } else if (cardInstance.type === 'Spell') {
        console.log(`Playing spell: ${cardInstance.name}`);
        // 法术通常不进入战场
        player.pp -= cardInstance.cost; // 扣除费用

        // 从手牌移除
        player.hand.splice(cardIndex, 1);

        // 执行法术效果
        executeSpellEffect(cardInstance, player.id); // 需要实现此函数

        // 法术通常进入墓地
        player.graveyard.push(cardInstance);


    } else {
        console.error(`Unknown card type: ${cardInstance.type}`);
        alert(`未知的卡牌类型: ${cardInstance.type}`);
        return; // 如果类型未知则不继续
    }


    // --- 出牌后操作 ---
    console.log(`Played ${cardInstance.name}. Remaining PP: ${player.pp}`);
    renderGame(); // 出牌后立即更新界面
}
        // This logic (current lines ~428-429) moved into endTurn's if block above.
    // Removed stray brace from line 445

    // Reset 'canAttack' status is often done at START of turn for clarity, moved there.
    // Reset other statuses if needed (e.g., temporary buffs)
function renderGame() {
    // Basic placeholder rendering - replace with more robust UI updates later
    // console.log("Rendering game state..."); // Reduce console noise for now

    const player1 = gameState.players[PLAYER1_ID];
    const player2 = gameState.players[PLAYER2_ID];

    // Ensure players are initialized before trying to render
    if (!player1 || !player2) {
         // Avoid error if called before initGame completes fully
         gameContainer.innerHTML = '<p>Initializing players...</p>';
         return;
    }

    // Find or create game info display area
    let infoDiv = document.getElementById('game-info');
    let endTurnButton = document.getElementById('end-turn-button');

    // Create elements if they don't exist (e.g., on first render)
    if (!infoDiv) {
        gameContainer.innerHTML = ''; // Clear "Loading..." or "Initializing..."
        infoDiv = document.createElement('div');
        infoDiv.id = 'game-info';
        gameContainer.appendChild(infoDiv);
    }
     // Ensure button is created only once
     if (!endTurnButton) {
        endTurnButton = document.createElement('button');
        endTurnButton.id = 'end-turn-button';
        endTurnButton.textContent = 'End Turn';
        // Disable initially, enable only during player's main phase
        endTurnButton.disabled = true;
        endTurnButton.onclick = () => {
            // Only allow player to end turn during their main phase and if game not over
            if (gameState.currentPlayerId === PLAYER1_ID && gameState.gamePhase === 'main' && !gameState.gameOver) {
                console.log("Player clicked End Turn button.");
                endTurnButton.disabled = true; // Disable button immediately on click
                endTurn();
            } else {
                 console.log("End Turn button clicked, but condition not met (Not player's turn, not main phase, or game over).");
            }
        };
        // Append button outside the infoDiv if preferred layout-wise
        gameContainer.appendChild(endTurnButton);
    }

     // Update basic player info display
     // Show player's hand card names for debugging, hide AI's
    const p1Hand = player1.hand.map(card => card.name || 'Card').join(', ');
    const p2HandCount = player2.hand.length; // Just show count for AI

    let player1Info = `<b>Player 1 (You):</b> HP=${player1.health}, PP=${player1.pp}/${player1.maxPp}, EP=${player1.ep}/${player1.maxEp}, Hand(${player1.hand.length}): [${p1Hand}], Deck: ${player1.deck.length}, Field: ${player1.field.length}`;
    let player2Info = `<b>Player 2 (AI):</b> HP=${player2.health}, PP=${player2.pp}/${player2.maxPp}, EP=${player2.ep}/${player2.maxEp}, Hand: ${p2HandCount}, Deck: ${player2.deck.length}, Field: ${player2.field.length}`;
    let turnInfo = `Turn: ${gameState.turnNumber}, Current: ${gameState.currentPlayerId}, Phase: ${gameState.gamePhase}${gameState.gameOver ? ` | GAME OVER! Winner: ${gameState.winner}` : ''}`;

    infoDiv.innerHTML = `<p>${player1Info}</p><p>${player2Info}</p><hr><p>${turnInfo}</p>`;

    // Update button state based on current player and phase (and game over status)
    endTurnButton.disabled = !(gameState.currentPlayerId === PLAYER1_ID && gameState.gamePhase === 'main' && !gameState.gameOver);


    // Render Player's Hand
    let playerHandDiv = document.getElementById('player-hand');
    if (!playerHandDiv) {
        playerHandDiv = document.createElement('div');
        playerHandDiv.id = 'player-hand';
        playerHandDiv.style.marginTop = '20px'; // Add some basic spacing
        playerHandDiv.style.borderTop = '1px solid #eee'; // Separator
        playerHandDiv.style.paddingTop = '10px';
        gameContainer.appendChild(playerHandDiv);
    }
    playerHandDiv.innerHTML = '<b>Your Hand:</b><br>'; // Clear previous hand render and add title
    player1.hand.forEach((cardInstance, index) => {
        const cardElement = document.createElement('button'); // Use button for easier click handling
        cardElement.classList.add('card', 'hand-card'); // Add classes for styling
        cardElement.dataset.uid = cardInstance.uid; // Store unique instance ID
        cardElement.dataset.index = index; // Store hand index if needed

cardElement.onclick = () => {
            console.log(`Clicked hand card: ${cardInstance.name} (UID: ${cardInstance.uid})`);
             // 添加检查：仅允许在玩家的主阶段且游戏未结束时出牌
             if (gameState.currentPlayerId === PLAYER1_ID && gameState.gamePhase === 'main' && !gameState.gameOver) {
                playCard(cardInstance.uid);
            } else {
                 console.log("Cannot play card: Not player's turn, not main phase, or game over.");
            }
        };
        let cardText = `[${cardInstance.cost} PP] ${cardInstance.name || 'Unknown'}`;
        if (cardInstance.type === 'Follower') {
            // Use currentHealth if available (it might be added later), otherwise base health
            const displayHealth = cardInstance.currentHealth !== undefined ? cardInstance.currentHealth : cardInstance.health;
            cardText += ` (${cardInstance.attack || 0}/${displayHealth || 0})`; // Add defaults for safety
        }
         if (cardInstance.type === 'Spell') {
             cardText += ` (Spell)`;
         }
        cardElement.title = cardInstance.description || ''; // Tooltip with description

        // Add basic styling to button
        cardElement.style.margin = '5px';
        cardElement.style.padding = '10px';
        cardElement.style.border = '1px solid #ccc';
        cardElement.style.cursor = 'pointer';


        // Add evolve indicator if applicable and can be played
        // (Can be played check comes later, for now just check EP)
        if (cardInstance.type === 'Follower' && cardInstance.canEvolve && player1.ep > 0 && gameState.turnNumber >= 5) { // Assuming P1 evo turn is 5
             cardText += ' (Evo Ready)';
             cardElement.style.borderColor = 'lightblue'; // Highlight evo-ready cards
        }


        cardElement.textContent = cardText;

        // Add click listener later for playing the card
        // cardElement.onclick = () => {
        //     console.log(`Clicked card: ${cardInstance.name} (UID: ${cardInstance.uid})`);
        //     // Call playCard function here
        // };

        playerHandDiv.appendChild(cardElement);
    });
// Render Fields
    //    const player1 = gameState.players[PLAYER1_ID];
    //    const player2 = gameState.players[PLAYER2_ID];

    // --- Player 2 (Opponent) Field ---
    let player2FieldDiv = document.getElementById('player2-field');
     if (!player2FieldDiv) {
         player2FieldDiv = document.createElement('div');
         player2FieldDiv.id = 'player2-field';
         player2FieldDiv.style.minHeight = '80px'; // 确保空场时也有高度
         player2FieldDiv.style.backgroundColor = '#ffeeee'; // 浅红色背景
         player2FieldDiv.style.padding = '10px';
         player2FieldDiv.style.marginTop = '10px';
         player2FieldDiv.style.border = '1px dashed red';
         // 将对手战场插入到玩家手牌之前
         const playerHandDiv = document.getElementById('player-hand'); // 需要获取手牌div引用
         if(playerHandDiv) {
            gameContainer.insertBefore(player2FieldDiv, playerHandDiv);
         } else {
             gameContainer.appendChild(player2FieldDiv); // Fallback if hand div not found yet
         }
     }
     player2FieldDiv.innerHTML = '<b>Opponent Field:</b><br>';
     player2.field.forEach((cardInstance, index) => {
         const fieldCardElement = document.createElement('div');
         fieldCardElement.classList.add('card', 'field-card', 'opponent-card');
         fieldCardElement.dataset.uid = cardInstance.uid;
         fieldCardElement.dataset.index = index;
         // 使用当前属性（战斗或效果后可能变化）
         const displayAttack = cardInstance.currentAttack !== undefined ? cardInstance.currentAttack : cardInstance.attack;
         const displayHealth = cardInstance.currentHealth !== undefined ? cardInstance.currentHealth : cardInstance.health;
         fieldCardElement.textContent = `[${cardInstance.name}] (${displayAttack}/${displayHealth})`;
         fieldCardElement.title = cardInstance.description || ''; // Tooltip
         fieldCardElement.style.border = '1px solid orange';
         fieldCardElement.style.padding = '5px';
         fieldCardElement.style.margin = '2px';
         fieldCardElement.style.display = 'inline-block'; // 横向排列
         fieldCardElement.style.backgroundColor = 'white';
         player2FieldDiv.appendChild(fieldCardElement);
     });
     // 如果战场为空，显示提示
     if (player2.field.length === 0) {
         player2FieldDiv.innerHTML += '<i>(empty)</i>';
     }


    // --- Player 1 (Your) Field ---
    let player1FieldDiv = document.getElementById('player1-field');
    if (!player1FieldDiv) {
        player1FieldDiv = document.createElement('div');
        player1FieldDiv.id = 'player1-field';

     // TODO: Add rendering for opponent's hand (just card backs/count) - info already in player2Info string for now.
    // TODO: Add rendering for field cards (player and opponent)

// Removed misplaced closing brace for renderGame from line 627
player1FieldDiv.innerHTML = '<b>Your Field:</b><br>'; // 创建完 div 后再设置标题
    player1.field.forEach((cardInstance, index) => {
        const fieldCardElement = document.createElement('div'); // 之后可改为button用于攻击
        fieldCardElement.classList.add('card', 'field-card', 'player-card');
        fieldCardElement.dataset.uid = cardInstance.uid;
        fieldCardElement.dataset.index = index;
        const displayAttack = cardInstance.currentAttack !== undefined ? cardInstance.currentAttack : cardInstance.attack;
        const displayHealth = cardInstance.currentHealth !== undefined ? cardInstance.currentHealth : cardInstance.health;
        // 显示攻击状态 (召唤失调)
        const attackStatus = cardInstance.canAttack ? '(Ready)' : '(Summoning Sick)';
        fieldCardElement.textContent = `[${cardInstance.name}] (${displayAttack}/${displayHealth}) ${attackStatus}`;
        fieldCardElement.title = cardInstance.description || ''; // Tooltip
        fieldCardElement.style.border = '1px solid blue';
        fieldCardElement.style.padding = '5px';
        fieldCardElement.style.margin = '2px';
        fieldCardElement.style.display = 'inline-block'; // 横向排列
        fieldCardElement.style.backgroundColor = 'white';
        // 如果可以攻击，改变鼠标样式提示
        // --- Step 1: Set basic visual style (cursor, border) ---
        if (cardInstance.canAttack) {
            fieldCardElement.style.cursor = 'pointer'; // Use pointer for clickable items
            fieldCardElement.style.borderColor = 'darkblue'; // Highlight attack-ready border
        } else {
            fieldCardElement.style.cursor = 'default';
            fieldCardElement.style.borderColor = 'blue'; // Ensure default border color for non-attackable
        } // --- End of style setting block ---

        // --- Step 2: Add or remove the onclick attack listener ---
        // Check if all attack conditions are met
        if (cardInstance.canAttack && gameState.currentPlayerId === PLAYER1_ID && gameState.gamePhase === 'main' && !gameState.gameOver) {
            // Conditions met: Add listener and ready class
            fieldCardElement.onclick = () => {
                console.log(`Attempting attack with: ${cardInstance.name} (UID: ${cardInstance.uid})`);
                initiateAttack(cardInstance.uid);
            };
            fieldCardElement.classList.add('attack-ready');
            // Ensure cursor is pointer (should be set above, but safe to double-check)
             if (fieldCardElement.style.cursor !== 'pointer') { // Defensive check
                 fieldCardElement.style.cursor = 'pointer';
             }
        } else {
            // Conditions not met: Remove listener and ready class
            fieldCardElement.onclick = null;
            fieldCardElement.classList.remove('attack-ready');
            // Ensure cursor is default (should be set above, but safe to double-check)
             if (fieldCardElement.style.cursor !== 'default') { // Defensive check
                 fieldCardElement.style.cursor = 'default';
             }
        } // --- End of onclick setting block ---
         // Removed extra closing brace from the previous line
         // ★★★ 攻击/指定目标的 onclick 事件稍后添加 ★★★
        player1FieldDiv.appendChild(fieldCardElement);
    });
    // 如果战场为空，显示提示 (Moved from ~line 818)
    if (player1.field.length === 0) {
        player1FieldDiv.innerHTML += '<i>(empty)</i>';
    }
} // End of renderGame function - Correctly placed brace
// Removed the block of misplaced/historical comments and code fragments (lines 686-708)
// The section below is the correct start of the Attack Logic.
// --- Attack Logic ---
let selectedAttackerUid = null; // 全局变量，跟踪当前选择的攻击者UID

/**
 * 玩家点击己方战场随从时调用，尝试开始攻击流程
 * @param {string} attackerUid - 被点击的己方随从的唯一ID
 */
function initiateAttack(attackerUid) {
    const attacker = gameState.players[PLAYER1_ID].field.find(card => card.uid === attackerUid);
    if (!attacker) {
        console.error("攻击者未找到!", attackerUid);
        selectedAttackerUid = null; // 清除可能存在的旧选择
        // 可以考虑添加一些视觉反馈，比如取消高亮
        // renderGame();
        return;
    }

    // 检查是否满足攻击条件 (防御性检查，理论上onclick时已处理)
    if (!attacker.canAttack || gameState.currentPlayerId !== PLAYER1_ID || gameState.gamePhase !== 'main' || gameState.gameOver) {
         console.log("不能攻击：不满足条件（无法攻击/非本人回合/非主阶段/游戏结束）");
         selectedAttackerUid = null; // 清除选择
         // renderGame(); // 更新视觉效果
         return;
    }

    // 如果重复点击同一个已选中的攻击者，则取消选择
    if (selectedAttackerUid === attackerUid) {
        console.log(`取消选择攻击者: ${attacker.name}`);
        selectedAttackerUid = null;
        // TODO: 更新UI，移除攻击者高亮和目标可选高亮
        renderGame(); // 重绘以移除高亮（如果实现了高亮的话）
        return;
    }

    console.log(`选择攻击者: ${attacker.name} (UID: ${attackerUid}). 请选择对方战场上的目标。`);
    selectedAttackerUid = attackerUid;

    // TODO: 更新UI - 高亮选中的攻击者
    // TODO: 更新UI - 高亮对方战场上所有可被攻击的目标，并为它们添加 selectAttackTarget 的 onclick 事件
    renderGame(); // 重绘以应用高亮效果
}

/**
 * 玩家点击对方战场随从时调用（应只在选择了攻击者后才添加此事件）
 * @param {string} targetUid - 被点击的对方随从的唯一ID
 */
function selectAttackTarget(targetUid) {
     console.log(`尝试选择目标: UID ${targetUid}`);
     if (!selectedAttackerUid) {
         console.log("错误：尚未选择攻击者。");
         // 可能是在错误的时机触发了这个事件，或者状态不同步
         return;
     }

     const target = gameState.players[PLAYER2_ID].field.find(card => card.uid === targetUid);
     if (!target) {
        console.log("错误：选择的目标无效或未找到。");
        // 不清除攻击者选择，允许玩家选择其他目标
        return;
     }

     // 找到了攻击者和目标，执行攻击
     console.log(`目标 ${target.name} (UID: ${targetUid}) 被选定，执行攻击!`);
     executeAttack(selectedAttackerUid, targetUid);
}


/**
 * 执行实际的攻击计算和状态更新
 * @param {string} attackerUid
 * @param {string} targetUid
 */
function executeAttack(attackerUid, targetUid) {
     if (gameState.gamePhase !== 'main' || gameState.currentPlayerId !== PLAYER1_ID || gameState.gameOver) {
         console.log("无法执行攻击：条件不满足（非主阶段/非本人回合/游戏结束）。");
         selectedAttackerUid = null; // 攻击流程意外中止，清除选择
         renderGame();
         return;
     }

    // 再次获取引用（防御性编程）
    const attacker = gameState.players[PLAYER1_ID].field.find(card => card.uid === attackerUid);
    const target = gameState.players[PLAYER2_ID].field.find(card => card.uid === targetUid);

    // Final validation check
    if (!attacker || !target || !attacker.canAttack) {
        console.error("执行攻击校验失败：攻击者或目标丢失，或攻击者不能攻击。");
        selectedAttackerUid = null; // 清除选择
        renderGame();
        return;
    }

    console.log(`战斗开始: ${attacker.name} VS ${target.name}`);

    // --- 战斗计算 ---
    // 获取当前攻防，若无则使用基础值
     const attackerAttack = attacker.currentAttack !== undefined ? attacker.currentAttack : attacker.attack;
     let attackerHealth = attacker.currentHealth !== undefined ? attacker.currentHealth : attacker.health;
     const targetAttack = target.currentAttack !== undefined ? target.currentAttack : target.attack;
     let targetHealth = target.currentHealth !== undefined ? target.currentHealth : target.health;

     console.log(`  开战前: ${attacker.name}(${attackerAttack}/${attackerHealth}), ${target.name}(${targetAttack}/${targetHealth})`);

     // 互相造成伤害
     targetHealth -= attackerAttack;
     attackerHealth -= targetAttack;

     console.log(`  战斗后: ${attacker.name}(${attackerAttack}/${attackerHealth}), ${target.name}(${targetAttack}/${targetHealth})`);

     // 更新当前生命值
     attacker.currentHealth = attackerHealth;
     target.currentHealth = targetHealth;

     // --- 战斗后处理 ---
     attacker.canAttack = false; // 本回合无法再次攻击
     const attackerId = attacker.uid; // 保存ID以备后用
     const targetId = target.uid;   // 保存ID以备后用

     // 检查随从是否被破坏
     let attackerDestroyed = false;
     let targetDestroyed = false;

     if (attacker.currentHealth <= 0) {
         console.log(`  ${attacker.name} 被破坏了!`);
         attackerDestroyed = true;
         gameState.players[PLAYER1_ID].graveyard.push(attacker); // 移入墓地
         // 从战场移除 (过滤掉被破坏的随从)
         gameState.players[PLAYER1_ID].field = gameState.players[PLAYER1_ID].field.filter(card => card.uid !== attackerId);

     }
      if (target.currentHealth <= 0) {
         console.log(`  ${target.name} 被破坏了!`);
         targetDestroyed = true;
          gameState.players[PLAYER2_ID].graveyard.push(target); // 移入墓地
         // 从战场移除
         gameState.players[PLAYER2_ID].field = gameState.players[PLAYER2_ID].field.filter(card => card.uid !== targetId);
     }

     // --- 清理与更新 ---
     selectedAttackerUid = null; // 清除攻击者选择状态

     // TODO: 触发 "攻击后" 或 "被破坏时" 的卡牌效果（如果未来有的话）
     // TODO: 检查游戏是否结束 checkGameOver();

     console.log("攻击结算完毕。");
     renderGame(); // Update UI with results
}
// --- 启动游戏 ---
document.addEventListener('DOMContentLoaded', () => {
    console.log("DOM fully loaded and parsed. Starting game setup by loading card data...");
    // Load card data first. loadCardData will call initGame upon completion.
    loadCardData();
});

// 更多函数和逻辑将在这里添加...
} // Added closing brace based on persistent error message