// src/player.js - 玩家状态与行为

// 导入依赖
import { createCardInstance, getCardDefinition } from './cardManager.js'; // Import getCardDefinition

console.log("player.js loaded");

// 使用工厂函数创建玩家对象，更灵活简单
export function createPlayer(id, isAI = false) {
    console.log(`Creating player object for ID: ${id}, isAI: ${isAI}`);
    // --- REMOVED: Leader instance creation is now handled by gameManager hydration ---
    // const leaderId = 0;
    // const leaderDefinition = getCardDefinition(leaderId);
    // ... (rest of the removed leader creation logic) ...
    // const leaderInstance = createCardInstance(leaderDefinition);
    // ... (rest of the removed leader creation logic) ...


    // Return the player object
    return {
        id: id,
        isAI: isAI,
        leaderCard: null, // Initialize leaderCard as null, will be populated during hydration
        // NOTE: Direct 'health' and 'maxHealth' properties are removed. Use getters below.
        pp: 0,         // 当前 Play Points
        maxPP: 0,      // 当前回合 Play Points 上限
        ep: 0,         // 当前 Evolution Points
        maxEP: 0,      // EP 上限 (先手2, 后手3)
        canEvolveThisTurn: false, // ADDED: Flag to track if evolve is possible this turn
        hasUnlockedEvolution: false, // Flag to track if EP has been unlocked for this player
        turn: 0,       // 玩家经历的回合数 (可用于进化点解锁)

        deck: [],      // 牌库 (存储卡牌 ID)
        hand: [],      // 手牌 (存储卡牌实例)
        field: [],     // 战场 (存储卡牌实例)
        graveyard: [], // 墓地 (存储卡牌实例)

        maxFieldSize: 5, // 战场格子数量
        maxHandSize: 9,  // 手牌上限

        // Getter for current health, reads from leader card
        get health() {
            // Return 0 if leaderCard somehow doesn't exist to prevent errors
            return this.leaderCard ? this.leaderCard.currentHealth : 0;
        },

        // Getter for max health, reads base health from leader card definition
        get maxHealth() {
            // Return 0 if leaderCard doesn't exist
            return this.leaderCard ? this.leaderCard.health : 0;
        },

        // --- Methods ---

        // 初始化牌库 (由外部调用传入卡组列表)
        initializeDeck(deckList) {
            // deckList 是一个卡牌 ID 数组
            this.deck = [...deckList]; // 直接用 ID
            console.log(`Player ${this.id}: Deck initialized with ${this.deck.length} card IDs.`);
            this.shuffleDeck(); // Initialize and immediately shuffle
        },

        // 洗牌
        shuffleDeck() {
            console.log(`Player ${this.id} attempting to shuffle deck...`);
            // 标准 Fisher-Yates 洗牌算法
            for (let i = this.deck.length - 1; i > 0; i--) {
                const j = Math.floor(Math.random() * (i + 1));
                [this.deck[i], this.deck[j]] = [this.deck[j], this.deck[i]];
            }
            console.log(`Player ${this.id}: Deck shuffled.`);
        },

        // 抽牌
        drawCard(count = 1) {
            console.log(`Player ${this.id} drawing ${count} card(s)...`);
            const drawnCards = [];
            console.log(`PLAYER_DRAW_LOOP [${this.id}]: Starting draw loop. Count = ${count}`); // Log loop start
            for (let i = 0; i < count; i++) {
                console.log(`PLAYER_DRAW_LOOP [${this.id}]: Iteration ${i+1}/${count}`); // Log each iteration
                if (this.deck.length === 0) {
                    console.warn(`Player ${this.id}: Deck is empty! Cannot draw. Triggering game over.`);
                    // Trigger game over immediately with 'deck_out' reason
                    // TODO: Implement proper game over handling (triggered by server ideally)
                    // For now, just log and return, preventing infinite draw loops.
                    console.error(`Player ${this.id} triggered DECK OUT condition! Game should end.`);
                    return drawnCards; // Exit the drawCard function immediately after triggering game over
                }
                if (this.hand.length >= this.maxHandSize) {
                    const burntCardId = this.deck.pop(); // Remove card from deck
                    console.warn(`Player ${this.id}: Hand is full! Burnt card ID: ${burntCardId}`);
                    // TODO: Add burnt card to graveyard or log?
                    continue; // Skip adding to hand, but continue loop if drawing multiple
                }

                const cardId = this.deck.pop(); // Draw from the end (top) of the deck
                // --- FIX: Get card definition first, then create instance ---
                const cardDefinition = getCardDefinition(cardId);
                if (!cardDefinition) {
                    console.error(`Player ${this.id}: Failed to get definition for card ID: ${cardId}. Skipping draw.`);
                    continue; // Skip to next iteration if definition not found
                }
                // Pass the definition object (acting as DTO) to createCardInstance
                const cardInstance = createCardInstance(cardDefinition);

                if (cardInstance) {
                    cardInstance.location = 'hand'; // <-- Set location
                    cardInstance.ownerId = this.id; // <-- Set owner ID

                    this.hand.push(cardInstance);
                    drawnCards.push(cardInstance);
                    console.log(`PLAYER_DRAW_LOOP [${this.id}]: Successfully drew card ${cardInstance.name} (UID: ${cardInstance.uid}) in iteration ${i+1}.`); // Log success within loop
                    // console.log(`Player ${this.id}: Drew card -> ${cardInstance.name} (ID: ${cardId}, UID: ${cardInstance.uid}, Loc: ${cardInstance.location}, Owner: ${cardInstance.ownerId})`); // Reduced log noise
                } else {
                    console.error(`Player ${this.id}: Failed to create instance for card ID: ${cardId}. Card might be missing from database.`);
                    // Put the ID back? Or handle differently? For now, it's lost.
                }
            }
            console.log(`Player ${this.id}: Finished drawing. Hand size: ${this.hand.length}`);
            return drawnCards; // Return array of card instances drawn
        },

        // ADDED: Spend Evolution Points
        spendEP(amount = 1) {
            if (this.ep >= amount) {
                this.ep -= amount;
                console.log(`PLAYER [${this.id}]: Spent ${amount} EP. Remaining: ${this.ep}`);
                return true; // Success
            } else {
                console.warn(`PLAYER [${this.id}]: Not enough EP to spend ${amount}. Have: ${this.ep}`);
                return false; // Failure
            }
        },
        // ADDED: Gain Evolution Points (up to max)
        gainEP(amount = 1) { // ADDED MORE LOGS
            console.log(`PLAYER_DBG [${this.id}]: gainEP called. Amount: ${amount}, Current EP: ${this.ep}, Current maxEP: ${this.maxEP}`);
            const potentialEP = this.ep + amount;
            const newEP = Math.min(potentialEP, this.maxEP); // Use the current maxEP as the cap
            console.log(`PLAYER_DBG [${this.id}]: Potential EP: ${potentialEP}, Capped New EP: ${newEP}`);
            // Only update if the value actually changes to avoid unnecessary updates
            if (newEP !== this.ep) {
                 this.ep = newEP;
                 console.log(`PLAYER [${this.id}]: EP after gain: ${this.ep}/${this.maxEP}`);
            } else {
                 console.log(`PLAYER_DBG [${this.id}]: EP unchanged (${this.ep}/${this.maxEP}).`);
            }
            return this.ep; // Return the current EP after potential gain
        },

        // ADDED: Perform Mulligan
        performMulligan(cardUidsToReplace = []) {
            console.log(`PLAYER [${this.id}]: Performing mulligan. Replacing UIDs:`, cardUidsToReplace);

            if (!Array.isArray(cardUidsToReplace)) {
                console.error(`PLAYER [${this.id}]: performMulligan failed - cardUidsToReplace must be an array.`);
                return; // Or throw error
            }

            const numToReplace = cardUidsToReplace.length;
            if (numToReplace < 0 || numToReplace > this.hand.length) {
                console.error(`PLAYER [${this.id}]: performMulligan failed - Invalid number of cards to replace (${numToReplace}).`);
                return; // Or throw error
            }
             if (numToReplace === 0) {
                console.log(`PLAYER [${this.id}]: No cards selected for mulligan. Hand remains unchanged.`);
                return; // Nothing to do
            }

            const cardsToKeep = [];
            const cardsToReturn = [];

            // Separate hand based on UIDs to replace
            for (const card of this.hand) {
                if (cardUidsToReplace.includes(card.uid)) {
                    cardsToReturn.push(card);
                } else {
                    cardsToKeep.push(card);
                }
            }

            // Verify count matches
            if (cardsToReturn.length !== numToReplace) {
                console.warn(`PLAYER [${this.id}]: Mulligan count mismatch. Expected ${numToReplace}, found ${cardsToReturn.length}. Proceeding anyway.`);
                // This might indicate an issue with provided UIDs, but we try to proceed.
            }

            // Update hand with cards kept
            this.hand = cardsToKeep;
            console.log(`PLAYER [${this.id}]: Hand updated after keeping cards. Size: ${this.hand.length}`);

            // Return selected cards to deck
            cardsToReturn.forEach(card => {
                card.location = 'deck'; // Update location before returning
                this.deck.push(card.id); // Return card ID to deck
                console.log(`PLAYER_DBG [${this.id}]: Returning ${card.name} (ID: ${card.id}) to deck.`);
            });
            console.log(`PLAYER [${this.id}]: Returned ${cardsToReturn.length} card(s) to deck. Deck size: ${this.deck.length}`);


            // Shuffle the deck
            this.shuffleDeck();

            // Draw replacements
            const drawnReplacements = this.drawCard(cardsToReturn.length); // Draw the same number we returned
             console.log(`PLAYER [${this.id}]: Drew ${drawnReplacements.length} replacement card(s). Final hand size: ${this.hand.length}`);

        },

        // (其他方法占位符，如: 受到伤害 takeDamage, 治疗 heal, 增加/减少PP/EP 等)
        // TODO: Refactor takeDamage/heal methods to target leaderCard.currentHealth
        updateStats(updates) { // 简单的状态更新方法
            for(const key in updates) {
                if(this.hasOwnProperty(key) && key !== 'health' && key !== 'maxHealth') { // Prevent direct update of health/maxHealth
                    // Check if property exists and is not a getter/method before assigning
                    const descriptor = Object.getOwnPropertyDescriptor(this, key);
                    if (descriptor && typeof descriptor.get === 'undefined' && typeof this[key] !== 'function') {
                         this[key] = updates[key];
                    } else if (key === 'leaderCard' && typeof updates[key] === 'object') {
                        // Allow targeted updates to leaderCard properties if needed? Be cautious.
                        console.warn(`PLAYER [${this.id}]: Attempted complex update of leaderCard via updateStats. Ignoring for now.`);
                    } else {
                         console.warn(`PLAYER [${this.id}]: Cannot update property '${key}' via updateStats (might be getter or method).`);
                    }
                }
            }
             // Need UI update call here? Or should gameManager handle it? GameManager usually triggers render.
        }
    };
}