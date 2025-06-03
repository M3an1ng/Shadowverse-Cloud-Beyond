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
    // --- UNKNOWNCARD BUG DIAGNOSIS START ---
    console.log(`GM_DBG_UNKNOWN: getCardDefinition called with cardId: ${cardId}`);
    console.log(`GM_DBG_UNKNOWN: Current cardDatabase length: ${cardDatabase?.length ?? 'undefined'}`);
    // Log first few IDs for comparison, only if database has items
    if (cardDatabase && cardDatabase.length > 0) {
        console.log(`GM_DBG_UNKNOWN: First few IDs in cardDatabase: ${cardDatabase.slice(0, 5).map(c => c.id).join(', ')}`);
    }
    // --- UNKNOWNCARD BUG DIAGNOSIS END ---

    const definition = cardDatabase.find(card => card && card.id === cardId); // Added check for card existence before accessing id
    if (!definition) {
        console.warn(`Card definition not found for ID: ${cardId}. cardDatabase length was ${cardDatabase?.length ?? 'undefined'}.`); // Added length to warning
        // 返回 null 或 undefined，调用者需要处理
        return null;
    }
    // 返回定义的副本，防止意外修改原始数据库
    return { ...definition };
}

// 创建卡牌运行时实例 (包含唯一标识和当前状态)
// Input is assumed to be a DTO object from the server (e.g., CardDto, UnitStateDto)
export function createCardInstance(cardDto) {
    // Input Validation
    // --- DEBUG START: Log incoming DTO ---
    console.log(`DEBUG_CARDMANAGER: createCardInstance received:`, JSON.stringify(cardDto, null, 2));
    // --- DEBUG END ---
    if (!cardDto || typeof cardDto !== 'object') {
        console.error("Cannot create instance: Invalid DTO provided. Input was:", cardDto); // Log the problematic input
        return null;
    }

    // --- UNKNOWNCARD BUG DIAGNOSIS ---
    // Log the received DTO to understand its structure if issues persist
    // console.log("GM_DBG_UNKNOWN: createCardInstance received DTO:", JSON.stringify(cardDto));
    // ---

    // --- UNIFIED ID LOGIC START ---
    // Input object MUST have an 'id' (lowercase) OR 'CardId' (PascalCase) property
    // Prioritize lowercase 'id' if both exist, otherwise use 'CardId'.
    const identifier = cardDto.id ?? cardDto.CardId; // Use nullish coalescing

    // Check if identifier is null or undefined (after checking both id and CardId), allowing 0 as a valid ID
    if (identifier === null || identifier === undefined) {
         console.error("Cannot create instance: Input object missing required 'id' or 'CardId' property. Input was:", cardDto);
        return null;
    }

    // Find the base definition using the resolved identifier
    const baseDefinition = cardDatabase.find(card => card && card.id === identifier);

    if (!baseDefinition) {
        console.error(`Cannot create instance: Base definition not found for id '${identifier}'. Input object:`, cardDto);
        return null;
    }

    // Increment UID counter for fallback scenario if Uid is missing in DTO/input
    uidCounter++;

    // Create the instance:
    // - Use baseDefinition for static properties.
    // - Use cardDto (the input object) for runtime properties if available, otherwise fallback to baseDefinition or defaults.
    const instance = {
        // Base static properties from definition
        id: baseDefinition.id, // Use id from definition
        name: baseDefinition.name ?? 'Unknown Card',
        cost: baseDefinition.cost ?? 0,
        attack: baseDefinition.attack ?? 0, // Base attack from definition
        health: baseDefinition.health ?? 0, // Base health from definition
        type: baseDefinition.type ?? 'Unknown',
        effects: Array.isArray(baseDefinition.effects) ? [...baseDefinition.effects] : [], // Use definition effects
        keywords: Array.isArray(baseDefinition.keywords) ? [...baseDefinition.keywords] : [], // Use definition keywords

        // Runtime/State properties, prioritizing input object (cardDto) values if they exist
        // This assumes cardDto might contain runtime state even if it's primarily a definition (like for the leader)
        // OR it might be an actual DTO that has been pre-processed to have 'id' instead of 'CardId'.

        // !! 核心修复点 !!
        // 尝试从服务器 DTO 读取 instanceId (注意 JSON 序列化后的大小写)
        uid: (() => { // Use IIFE for cleaner logic
            const serverUid = cardDto.instanceId || cardDto.InstanceId || cardDto.uid || cardDto.Uid;
            if (serverUid) {
                return serverUid; // 使用服务器提供的真实 GUID
            } else {
                // 服务器未提供 UID，这是一个严重错误，记录日志
                console.error(`CRITICAL: Server DTO missing instanceId/Uid for card definition ID: ${baseDefinition.id}`, cardDto);
                // 不应生成备用 UID。让 uid 保持 null。
                return null;
            }
        })(), // End IIFE

        currentAttack: cardDto.CurrentAttack ?? baseDefinition.attack ?? 0,       // Prioritize CurrentAttack from input, fallback to definition base attack
        currentHealth: cardDto.CurrentHealth ?? baseDefinition.health ?? 0,       // Prioritize CurrentHealth from input, fallback to definition base health
        maxHealth: cardDto.MaxHealth ?? baseDefinition.health ?? 0,           // Prioritize MaxHealth from input, fallback to definition base health

        // Default runtime state (can be overridden by hydration logic later if needed)
        canAttack: cardDto.CanAttack ?? false,                                // Prioritize CanAttack from input
        turnPlayed: null,                                                     // Initialized locally
        attackCount: 0,                                                       // Initialized locally
        isEvolved: cardDto.IsEvolved ?? false,                                // Prioritize IsEvolved from input
        // Add other potential DTO properties here if needed (e.g., Countdown for amulets)
    };
    // --- UNIFIED ID LOGIC END ---
    // console.log("Created card instance:", instance); // Debugging
    return instance;
}

// 获取所有卡牌定义 (例如用于未来的卡组构建界面)
export function getAllCardDefinitions() {
    // 返回数据库的深拷贝，防止外部修改
    return JSON.parse(JSON.stringify(cardDatabase));
}