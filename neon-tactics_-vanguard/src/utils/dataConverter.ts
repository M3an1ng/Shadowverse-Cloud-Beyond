
import { CardModel, CardType, ClassType, Rarity } from '../types';

// Helper to safely parse numbers
const safeNum = (val: any): number => {
    const n = Number(val);
    return isNaN(n) ? 0 : n;
};

/**
 * Parses the "ID" section of the JSON to create a Trait ID -> Name map.
 */
const generateTraitMap = (idData: any[]): Record<string, string> => {
    const map: Record<string, string> = {};
    if (Array.isArray(idData)) {
        idData.forEach(item => {
            if (item.Tap === 'Trait') {
                map[item.ID] = item.Name;
            }
        });
    }
    return map;
};

/**
 * Converts a raw JSON card entry into a strongly typed CardModel.
 */
export const convertRawCardToModel = (raw: any, traitMap: Record<string, string>): CardModel => {
    // Collect all non-empty effect strings
    const effects: string[] = [];
    for(let i = 1; i <= 9; i++) {
        const effectKey = `Effect${i}`;
        if(raw[effectKey] && typeof raw[effectKey] === 'string' && raw[effectKey].trim() !== "") {
            effects.push(raw[effectKey]);
        }
    }

    // Basic parsing of Class and Rarity strings to Enum
    // The JSON uses "Swordcraft", "Bronze", etc. matching our Enums mostly.
    const classMap: Record<string, ClassType> = {
        'Forestcraft': ClassType.FORESTCRAFT,
        'Swordcraft': ClassType.SWORDCRAFT,
        'Runecraft': ClassType.RUNECRAFT,
        'Dragoncraft': ClassType.DRAGONCRAFT,
        'Abysscraft': ClassType.ABYSSCRAFT,
        'Havencraft': ClassType.HAVENCRAFT,
        'Portalcraft': ClassType.PORTALCRAFT,
        'Neutral': ClassType.NEUTRAL
    };

    return {
        id: String(raw.ID),
        name: raw.Name,
        cardSet: raw.CardSet,
        class: classMap[raw.Class] || ClassType.NEUTRAL,
        rarity: raw.Rarity as Rarity || Rarity.BRONZE,
        type: raw.Type as CardType || CardType.FOLLOWER,
        trait: raw.Trait || "",
        cost: safeNum(raw.Cost),
        attack: safeNum(raw.Attack),
        defense: safeNum(raw.Defense),
        countdown: safeNum(raw.Countdown),
        effects: effects
    };
};

/**
 * Main entry point to parse the full game_data.json object
 */
export const parseRawGameData = (jsonData: any): Record<string, CardModel> => {
    const db: Record<string, CardModel> = {};
    
    // 1. Generate Maps
    const traitMap = generateTraitMap(jsonData.ID || []);

    // 2. Parse Effects (Cards)
    if (Array.isArray(jsonData.Effect)) {
        jsonData.Effect.forEach((rawItem: any) => {
            // Skip empty IDs or placeholder rows
            if (!rawItem.ID) return;
            
            const card = convertRawCardToModel(rawItem, traitMap);
            db[card.id] = card;
        });
    }

    console.log(`[DataConverter] Loaded ${Object.keys(db).length} cards.`);
    return db;
};
    