// src/cardManager.js - 卡牌数据加载与实例创建

console.log("cardManager.js loaded");

let cardDatabase = []; // 存储从 JSON 加载的卡牌定义
let uidCounter = 0; // 用于生成卡牌实例唯一ID的计数器

// 异步加载卡牌数据
export async function loadCards() {
    // 注意：fetch 的路径是相对于 HTML 文件的，而不是 JS 文件
    // const dataPath = './data/card_data.json'; // Replaced with API endpoint
    const apiUrl = '/api/cards'; // Use the API endpoint relative to the domain
    console.log(`Attempting to load card data from API: ${apiUrl}`);
    try {
        console.log("loadCards: Inside try block, before fetch."); // ADDED
        const response = await fetch(apiUrl); // Fetch from the API
        console.log(`loadCards: Fetch completed. Status: ${response.status}`); // ADDED
        if (!response.ok) {
            console.error(`loadCards: HTTP error! Status: ${response.status}`); // ADDED Log before throw
            // 网络错误或文件未找到等
            throw new Error(`HTTP error loading card data! status: ${response.status}`);
        }
        console.log("loadCards: Response OK. Attempting response.json()..."); // ADDED
        const loadedData = await response.json();
        console.log("loadCards: response.json() completed."); // ADDED
        if (!Array.isArray(loadedData)) {
            console.error("loadCards: Loaded data is not an array!"); // ADDED Log before throw
            throw new Error("Loaded card data is not an array.");
        }
        console.log("loadCards: Data format OK (Array). Assigning to cardDatabase.");// ADDED
        cardDatabase = loadedData;
        uidCounter = 0; // 重置 UID 计数器
        console.log(`Card data loaded successfully. ${cardDatabase.length} cards found.`);
        // 可以添加数据验证逻辑
    } catch (error) {
        console.error("loadCards: CATCH BLOCK triggered!"); // ADDED
        console.error("Failed to load or parse card data:", error);
        cardDatabase = []; // 加载失败时确保数据库为空
        // 向上抛出错误，让初始化流程知道失败了
        throw error;
    }
}

// 根据 ID 获取卡牌定义
export function getCardDefinition(cardId) {
    const definition = cardDatabase.find(card => card.id === cardId);
    if (!definition) {
        console.warn(`Card definition not found for ID: ${cardId}`);
        // 返回 null 或 undefined，调用者需要处理
        return null;
    }
    // 返回定义的副本，防止意外修改原始数据库
    return { ...definition };
}

// 创建卡牌运行时实例 (包含唯一标识和当前状态)
export function createCardInstance(cardIdOrDefinition) {
    let definition;
    if (typeof cardIdOrDefinition === 'string') {
        definition = getCardDefinition(cardIdOrDefinition);
    } else if (typeof cardIdOrDefinition === 'object' && cardIdOrDefinition !== null) {
        definition = cardIdOrDefinition; // 假设传入的是已获取的定义
    }

    if (!definition) {
        console.error("Cannot create instance: Invalid card ID or definition provided.");
        return null;
    }

    uidCounter++;
    const instance = {
        ...definition, // 复制基础属性 (id, name, cost, attack, health, etc.)
        uid: `${definition.type.charAt(0)}${uidCounter}`, // 运行时唯一 ID (e.g., F1, F2)
        currentAttack: definition.attack,   // 当前攻击力 (可能因 buff/debuff 变化)
        currentHealth: definition.health,   // 当前生命值
        maxHealth: definition.health,     // 最大生命值 (用于显示伤害)
        canAttack: false, // 本回合是否能攻击 (召唤当回合通常不能, 除非 Rush/Storm)
        turnPlayed: null, // Track the turn this instance was played to the field
        attackCount: 0,   // 本回合已攻击次数
        isEvolved: false,
        // REMOVED: effects: [], // This was overwriting the effects copied from definition!
        // The spread operator (...) above correctly copies the 'effects' array from definition.
        // 其他运行时属性...
    };
    // console.log("Created card instance:", instance); // 调试时开启
    return instance;
}

// 获取所有卡牌定义 (例如用于未来的卡组构建界面)
export function getAllCardDefinitions() {
    // 返回数据库的深拷贝，防止外部修改
    return JSON.parse(JSON.stringify(cardDatabase));
}