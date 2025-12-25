
import { CardModel, CardType, ClassType, Rarity } from './types';

// In a real app, this large JSON would be loaded from an external file.
// Here we define the raw data provided in the prompt.
export const RAW_CARD_DATA = {
    "Effect": [
        {
            "ID": "1000000",
            "CardSet": "神眷的棋盘",
            "Class": "Swordcraft",
            "Rarity": "Silver",
            "Name": "典当行",
            "Type": "Amulet",
            "Trait": "",
            "Cost": "2",
            "Attack": "",
            "Defense": "",
            "Countdown": "",
            "Effect1": "Fanfare||Discard|Select&2-Allied-Trait:8-Card||Draw|X||X=Cards|It",
            "Effect2": "Engage&0||Destroy|ThisCard",
            "Effect3": "Engage&0||Replicate|Fanfare",
            "Effect4": "Evolve||Give|ThisCard|{LastWords||Summon|1521306|1}",
            "Effect5": "Clash||If|{Cards|Allied-Field-Trait:5-Follower}>0||Give|ThisCard|{Barrier}",
            "Effect6": "Evolve||Give|Allied-Field-Trait:5-Follower|{Rush}||+2/+0|Allied-Field-Trait:5-Follower"
        },
        {
            "ID": "1010402",
            "CardSet": "基础卡牌",
            "Class": "Forestcraft",
            "Rarity": "Bronze",
            "Name": "妖精",
            "Type": "Follower",
            "Trait": "妖精",
            "Cost": "1",
            "Attack": "1",
            "Defense": "1",
            "Countdown": "",
            "Effect1": "Rush",
            "Effect2": "",
            "Effect3": "",
            "Effect4": "",
            "Effect5": "",
            "Effect6": ""
        },
        {
            "ID": "1020401",
            "CardSet": "基础卡牌",
            "Class": "Swordcraft",
            "Rarity": "Bronze",
            "Name": "骑士",
            "Type": "Follower",
            "Trait": "士兵",
            "Cost": "1",
            "Attack": "1",
            "Defense": "1",
            "Countdown": "",
            "Effect1": "",
            "Effect2": "",
            "Effect3": "",
            "Effect4": "",
            "Effect5": "",
            "Effect6": ""
        },
        {
            "ID": "1421401",
            "CardSet": "星云基础",
            "Class": "Swordcraft",
            "Rarity": "Bronze",
            "Name": "须臾剑士",
            "Type": "Follower",
            "Trait": "",
            "Cost": "1",
            "Attack": "1",
            "Defense": "1",
            "Countdown": "",
            "Effect1": "Storm",
            "Effect2": "",
            "Effect3": "",
            "Effect4": "",
            "Effect5": "",
            "Effect6": ""
        },
        {
            "ID": "1441402",
            "CardSet": "星云基础",
            "Class": "Dragoncraft",
            "Rarity": "Bronze",
            "Name": "烈焰火蜥蜴",
            "Type": "Follower",
            "Trait": "",
            "Cost": "2",
            "Attack": "2",
            "Defense": "2",
            "Countdown": "",
            "Effect1": "Fanfare||Damage|Select&1-Enemy-Follower|1",
            "Effect2": "",
            "Effect3": "",
            "Effect4": "",
            "Effect5": "",
            "Effect6": ""
        },
        {
             "ID": "1230102",
             "CardSet": "传说揭幕",
             "Class": "Runecraft",
             "Rarity": "Legendary",
             "Name": "安的巨大英灵",
             "Type": "Follower",
             "Trait": "玛纳利亚",
             "Cost": "3",
             "Attack": "5",
             "Defense": "5",
             "Countdown": "",
             "Effect1": "Rush",
             "Effect2": "Ward",
             "Effect3": "When|Enemy-Turn-End||Destroy|ThisCard",
             "Effect4": "",
             "Effect5": "",
             "Effect6": ""
        },
        {
            "ID": "1041302",
            "CardSet": "基础卡牌",
            "Class": "Dragoncraft",
            "Rarity": "Silver",
            "Name": "龙之启示",
            "Type": "Spell",
            "Trait": "",
            "Cost": "3",
            "Attack": "",
            "Defense": "",
            "Countdown": "",
            "Effect1": "Play||MaxPP|1",
            "Effect2": "Play||If|{MaxPP}>8||Draw|1",
            "Effect3": "",
            "Effect4": "",
            "Effect5": "",
            "Effect6": ""
        }
    ]
};

export const MAX_BOARD_SIZE = 5;
export const MAX_HAND_SIZE = 9;
export const INITIAL_PP = 0;
export const MAX_PP_LIMIT = 10;

// Helper to parse the raw JSON entry into a CardModel
export const parseCardData = (raw: any): CardModel => {
    const effects = [];
    for(let i=1; i<=6; i++) {
        if(raw[`Effect${i}`]) effects.push(raw[`Effect${i}`]);
    }

    return {
        id: raw.ID,
        name: raw.Name,
        cardSet: raw.CardSet,
        class: raw.Class,
        rarity: raw.Rarity,
        type: raw.Type,
        trait: raw.Trait,
        cost: Number(raw.Cost) || 0,
        attack: Number(raw.Attack) || 0,
        defense: Number(raw.Defense) || 0,
        countdown: Number(raw.Countdown) || 0,
        effects: effects
    };
};

export const ALL_CARDS_MAP: Record<string, CardModel> = RAW_CARD_DATA.Effect.reduce((acc, item) => {
    acc[item.ID] = parseCardData(item);
    return acc;
}, {} as Record<string, CardModel>);

export const TEST_DECK_IDS = ["1010402", "1020401", "1421401", "1441402", "1230102", "1041302", "1010402", "1421401"];
