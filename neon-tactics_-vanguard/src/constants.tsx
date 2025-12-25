import { CardModel } from './types';
import CARD_DATABASE from './data/cardDatabase.json';

// Directly use the pre-processed JSON data.
export const ALL_CARDS_MAP: Record<string, CardModel> = CARD_DATABASE as unknown as Record<string, CardModel>;

export const MAX_BOARD_SIZE = 5;
export const MAX_HAND_SIZE = 9;
export const INITIAL_PP = 0;
export const MAX_PP_LIMIT = 10;

// A set of interesting IDs to test with from the provided data
export const TEST_DECK_IDS = [
    // --- Trigger & Observer Tests ---
    "1011403", // 缭乱之庭 (Amulet, When|Allied-Trait:1-Follower|EnterField -> Damage)
    "1010402", // 妖精 (Trait:1 Follower, triggers 1011403)
    "1010402", // 妖精
    "1010402", // 妖精
    
    // --- Condition & Value Tests ---
    "1041302", // 龙之启示 (Spell, PP Boost) -> Test Overflow
    "1041401", // 龙人碎击 (Spell, Overflow -> Damage 4) -> Test If|Overflow
    "1011404", // 温厚的树精 (Combo&3 -> Evolve) -> Test Combo
    
    // --- Target Selection Tests ---
    "1011303", // 昆虫的忠告 (ReturnToHand|Select&1-Allied-Card)
    "1000000", // 典当行 (Amulet, Discard|Select&2)
    
    // --- Complex Effects ---
    "1230102", // 安的巨大英灵 (Legendary, When|Enemy-Turn-End -> Destroy)
    "1441402", // 烈焰火蜥蜴 (Fanfare Damage)
    "1050403", // 怨灵 (Storm, LeaveBanish, When|Allied-Turn-End -> Banish)
    
    // --- Fillers ---
    "1010402", "1010402", "1010402",
    "1041302", "1041302",
    "1011404", "1011404"
];
