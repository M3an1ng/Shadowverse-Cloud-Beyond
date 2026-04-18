// src/player.js - 玩家状态与行为
//
// 相关文档:
// - 前端组件结构: 文档/03-前端架构/01-组件结构.md
// - 状态管理: 文档/03-前端架构/02-状态管理.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
//

// 导入依赖
import { createCardInstance } from './cardManager.js';

// 使用工厂函数创建玩家对象，更灵活简单
export function createPlayer(id, isAI = false) {
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
        isFirst: false,
        class: null,
        displayName: '',
        avatarUrl: '',
        deckClass: '',
        hasCompletedMulligan: false,
        leaderCard: null, // Initialize leaderCard as null, will be populated during hydration
        // NOTE: Direct 'health' and 'maxHealth' properties are removed. Use getters below.
        pp: 0,         // 当前 Play Points
        maxPP: 0,      // 当前回合 Play Points 上限
        ppBoostUsesAvailable: 0, // PP+1 可用次数
        ppBoostArmed: false, // PP+1 是否已预留
        ep: 0,         // 当前 Evolution Points
        maxEP: 0,      // EP 上限 (先手2, 后手2)
        sep: 0,        // 当前 Super Evolution Points
        maxSEP: 0,     // SEP 上限
        canEvolveThisTurn: false, // ADDED: Flag to track if evolve is possible this turn
        canSuperEvolve: false,
        hasUnlockedEvolution: false, // Flag to track if EP has been unlocked for this player
        hasUnlockedSuperEvolution: false, // Flag to track if SEP has been unlocked for this player
        turn: 0,       // 玩家经历的回合数 (可用于进化点解锁)

        deck: [],      // 牌库 (存储卡牌 ID)
        hand: [],      // 手牌 (存储卡牌实例)
        field: [],     // 战场 (存储卡牌实例)
        graveyard: [], // 墓地 (存储卡牌实例)
        emblem: [],    // 纹章区 (存储卡牌实例)
        graveyardCount: 0, // 墓场计数
        bloodPool: 0, // 血池
        combo: 0, // 连击数
        rally: 0, // 连携数
        counters: {}, // 玩家变量映射
        counterDisplayNames: {}, // 玩家变量显示名

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
            // deckList 现在应该是一个完整的卡牌DTO对象数组
            this.deck = [...deckList];
            this.shuffleDeck(); // Initialize and immediately shuffle
        },

        // 洗牌
        shuffleDeck() {
            // 标准 Fisher-Yates 洗牌算法
            for (let i = this.deck.length - 1; i > 0; i--) {
                const j = Math.floor(Math.random() * (i + 1));
                [this.deck[i], this.deck[j]] = [this.deck[j], this.deck[i]];
            }
        },

        // 抽牌
        drawCard(count = 1) {
            const drawnCards = [];
            for (let i = 0; i < count; i++) {
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

                const cardDto = this.deck.pop(); // 从牌库（现在是DTO数组）的末尾抽牌
                if (!cardDto) {
                    console.error(`Player ${this.id}: Drew an invalid DTO from the deck. Skipping draw.`);
                    continue;
                }
                // 直接将完整的DTO传递给createCardInstance
                const cardInstance = createCardInstance(cardDto);

                if (cardInstance) {
                    cardInstance.zone = 'hand'; // <-- Set zone
                    cardInstance.ownerId = this.id; // <-- Set owner ID

                    this.hand.push(cardInstance);
                    drawnCards.push(cardInstance);
                } else {
                    console.error(`Player ${this.id}: Failed to create instance from DTO:`, cardDto);
                    // Put the ID back? Or handle differently? For now, it's lost.
                }
            }
            return drawnCards; // Return array of card instances drawn
        },

        // ADDED: Spend Evolution Points
        spendEP(amount = 1) {
            if (this.ep >= amount) {
                this.ep -= amount;
                return true; // Success
            } else {
                console.warn(`PLAYER [${this.id}]: Not enough EP to spend ${amount}. Have: ${this.ep}`);
                return false; // Failure
            }
        },
        // ADDED: Gain Evolution Points (up to max)
        gainEP(amount = 1) { // ADDED MORE LOGS
            const potentialEP = this.ep + amount;
            const newEP = Math.min(potentialEP, this.maxEP); // Use the current maxEP as the cap
            // Only update if the value actually changes to avoid unnecessary updates
            if (newEP !== this.ep) {
                 this.ep = newEP;
            }
            return this.ep; // Return the current EP after potential gain
        },

        // ADDED: Perform Mulligan
        performMulligan(cardUidsToReplace = []) {
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

            // Return selected cards to deck
            cardsToReturn.forEach(card => {
                card.zone = 'deck'; // Update zone before returning
                this.deck.push(card.id); // Return card ID to deck
            });


            // Shuffle the deck
            this.shuffleDeck();

            // Draw replacements
            this.drawCard(cardsToReturn.length); // Draw the same number we returned

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
