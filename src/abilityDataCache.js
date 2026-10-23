import {
    CARD_CATALOG_INVALIDATED_EVENT,
    isAbilityOnlyInvalidation
} from './dataChangeEvents.js';

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

function setAbilityDefinitions(abilities) {
    abilityDataCache.abilities = abilities;
    abilityDataCache.abilityIndex = buildAbilityIndex(abilities);
    abilityDataCache.abilitiesByCard = buildAbilitiesByCard(abilities);
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
    if (!Array.isArray(data)) {
        throw new TypeError('Card definitions response must be an array.');
    }
    return data;
}

async function fetchAbilities() {
    const response = await fetch('/api/abilities');
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    const data = await response.json();
    if (!Array.isArray(data)) {
        throw new TypeError('Ability definitions response must be an array.');
    }
    return data;
}

export async function ensureCardDefinitions() {
    if (abilityDataCache.cards) {
        return abilityDataCache.cards;
    }
    if (!abilityDataCache.cardsPromise) {
        const request = fetchCards()
            .then(cards => {
                // 失效会撤销请求的发布权，旧响应不能覆盖刷新后的目录。
                if (abilityDataCache.cardsPromise === request) {
                    abilityDataCache.cards = cards;
                    abilityDataCache.cardIndex = buildCardIndex(cards);
                }
                return cards;
            })
            .finally(() => {
                if (abilityDataCache.cardsPromise === request) {
                    abilityDataCache.cardsPromise = null;
                }
            });
        abilityDataCache.cardsPromise = request;
    }
    return await abilityDataCache.cardsPromise;
}

export async function ensureAbilityDefinitions() {
    if (abilityDataCache.abilities) {
        return abilityDataCache.abilities;
    }
    if (!abilityDataCache.abilitiesPromise) {
        const request = fetchAbilities()
            .then(abilities => {
                if (abilityDataCache.abilitiesPromise === request) {
                    setAbilityDefinitions(abilities);
                }
                return abilities;
            })
            .finally(() => {
                if (abilityDataCache.abilitiesPromise === request) {
                    abilityDataCache.abilitiesPromise = null;
                }
            });
        abilityDataCache.abilitiesPromise = request;
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

export function markAbilityDataCacheDirty() {
    abilityDataCache.abilities = null;
    abilityDataCache.abilityIndex = new Map();
    abilityDataCache.abilitiesByCard = new Map();
    abilityDataCache.abilitiesPromise = null;
}

export function upsertAbilityDataCacheEntry(ability) {
    const abilityId = normalizeId(ability?.id ?? ability?.Id);
    if (abilityId == null) {
        markAbilityDataCacheDirty();
        return;
    }

    if (!abilityDataCache.abilities) {
        return;
    }

    const nextAbilities = abilityDataCache.abilities.filter(existing => {
        const existingId = normalizeId(existing?.id ?? existing?.Id);
        return existingId !== abilityId;
    });
    nextAbilities.push(ability);
    setAbilityDefinitions(nextAbilities);
}

export function removeAbilityDataCacheEntry(abilityId) {
    const normalizedId = normalizeId(abilityId);
    if (normalizedId == null) {
        markAbilityDataCacheDirty();
        return;
    }

    if (!abilityDataCache.abilities) {
        return;
    }

    const nextAbilities = abilityDataCache.abilities.filter(existing => {
        const existingId = normalizeId(existing?.id ?? existing?.Id);
        return existingId !== normalizedId;
    });
    setAbilityDefinitions(nextAbilities);
}

export async function refreshAbilityDataCache() {
    clearAbilityDataCache();
    return await ensureAbilityData();
}

document.addEventListener(CARD_CATALOG_INVALIDATED_EVENT, (event) => {
    if (isAbilityOnlyInvalidation(event?.detail || {})) {
        return;
    }
    abilityDataCache.cards = null;
    abilityDataCache.cardIndex = new Map();
    abilityDataCache.cardsPromise = null;
});
