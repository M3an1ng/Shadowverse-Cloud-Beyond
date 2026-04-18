import { CARD_CATALOG_INVALIDATED_EVENT } from './dataChangeEvents.js';

const abilityDataCache = {
    cards: null,
    abilities: null,
    cardIndex: new Map(),
    abilityIndex: new Map(),
    abilitiesByCard: new Map(),
    cardsPromise: null,
    abilitiesPromise: null
};

function normalizeId(value) {
    const id = Number(value);
    return Number.isFinite(id) && id >= 0 ? id : null;
}

function buildCardIndex(cards) {
    const index = new Map();
    (cards || []).forEach(card => {
        const id = normalizeId(card?.id ?? card?.CardId ?? card?.Id);
        if (id == null) return;
        index.set(id, card);
    });
    return index;
}

function buildAbilityIndex(abilities) {
    const index = new Map();
    (abilities || []).forEach(ability => {
        const id = normalizeId(ability?.id ?? ability?.Id);
        if (id == null) return;
        index.set(id, ability);
    });
    return index;
}

function buildAbilitiesByCard(abilities) {
    const map = new Map();
    (abilities || []).forEach(ability => {
        const cardId = normalizeId(ability?.cardId ?? ability?.CardId);
        if (cardId == null) return;
        const list = map.get(cardId) || [];
        list.push(ability);
        map.set(cardId, list);
    });
    return map;
}

async function fetchCards() {
    const response = await fetch('/api/cards', {
        method: 'GET',
        cache: 'no-store'
    });
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    const data = await response.json();
    return Array.isArray(data) ? data : [];
}

async function fetchAbilities() {
    const response = await fetch('/api/abilities');
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    const data = await response.json();
    return Array.isArray(data) ? data : [];
}

export async function ensureCardDefinitions() {
    if (abilityDataCache.cards) {
        return abilityDataCache.cards;
    }
    if (!abilityDataCache.cardsPromise) {
        abilityDataCache.cardsPromise = fetchCards()
            .then(cards => {
                abilityDataCache.cards = cards;
                abilityDataCache.cardIndex = buildCardIndex(cards);
                return cards;
            })
            .catch(error => {
                console.warn('AbilityDataCache: failed to load cards', error);
                abilityDataCache.cards = [];
                abilityDataCache.cardIndex = new Map();
                return [];
            })
            .finally(() => {
                abilityDataCache.cardsPromise = null;
            });
    }
    return await abilityDataCache.cardsPromise;
}

export async function ensureAbilityDefinitions() {
    if (abilityDataCache.abilities) {
        return abilityDataCache.abilities;
    }
    if (!abilityDataCache.abilitiesPromise) {
        abilityDataCache.abilitiesPromise = fetchAbilities()
            .then(abilities => {
                abilityDataCache.abilities = abilities;
                abilityDataCache.abilityIndex = buildAbilityIndex(abilities);
                abilityDataCache.abilitiesByCard = buildAbilitiesByCard(abilities);
                return abilities;
            })
            .catch(error => {
                console.warn('AbilityDataCache: failed to load abilities', error);
                abilityDataCache.abilities = [];
                abilityDataCache.abilityIndex = new Map();
                abilityDataCache.abilitiesByCard = new Map();
                return [];
            })
            .finally(() => {
                abilityDataCache.abilitiesPromise = null;
            });
    }
    return await abilityDataCache.abilitiesPromise;
}

export async function ensureAbilityData() {
    await Promise.all([ensureCardDefinitions(), ensureAbilityDefinitions()]);
    return {
        cards: abilityDataCache.cards || [],
        abilities: abilityDataCache.abilities || [],
        cardIndex: abilityDataCache.cardIndex,
        abilityIndex: abilityDataCache.abilityIndex,
        abilitiesByCard: abilityDataCache.abilitiesByCard
    };
}

export function getAbilityDataSnapshot() {
    return {
        cards: abilityDataCache.cards || [],
        abilities: abilityDataCache.abilities || [],
        cardIndex: abilityDataCache.cardIndex,
        abilityIndex: abilityDataCache.abilityIndex,
        abilitiesByCard: abilityDataCache.abilitiesByCard
    };
}

export function clearAbilityDataCache() {
    abilityDataCache.cards = null;
    abilityDataCache.abilities = null;
    abilityDataCache.cardIndex = new Map();
    abilityDataCache.abilityIndex = new Map();
    abilityDataCache.abilitiesByCard = new Map();
    abilityDataCache.cardsPromise = null;
    abilityDataCache.abilitiesPromise = null;
}

export async function refreshAbilityDataCache() {
    clearAbilityDataCache();
    return await ensureAbilityData();
}

document.addEventListener(CARD_CATALOG_INVALIDATED_EVENT, () => {
    abilityDataCache.cards = null;
    abilityDataCache.cardIndex = new Map();
    abilityDataCache.cardsPromise = null;
});
