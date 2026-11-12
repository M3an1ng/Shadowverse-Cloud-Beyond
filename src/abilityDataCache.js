import {
    CARD_CATALOG_INVALIDATED_EVENT,
    isAbilityOnlyInvalidation
} from './dataChangeEvents.js';
import { normalizeCardPoolScope } from './cardDefinitionsLoader.js';

function createScopedCache() {
    return {
        cards: null,
        abilities: null,
        cardIndex: new Map(),
        abilityIndex: new Map(),
        abilitiesByCard: new Map(),
        cardsPromise: null,
        abilitiesPromise: null
    };
}

const cachesByScope = new Map([
    ['standard', createScopedCache()],
    ['mirage', createScopedCache()]
]);

let activeScope = 'standard';

export function setActiveAbilityDataScope(scope) {
    activeScope = normalizeCardPoolScope(scope);
    return activeScope;
}

export function getActiveAbilityDataScope() {
    return activeScope;
}

function resolveScope(scope) {
    if (scope === undefined || scope === null || scope === '') {
        return activeScope || 'standard';
    }
    return normalizeCardPoolScope(scope);
}

function getCache(scope) {
    const resolved = resolveScope(scope);
    if (!cachesByScope.has(resolved)) {
        cachesByScope.set(resolved, createScopedCache());
    }
    return { scope: resolved, cache: cachesByScope.get(resolved) };
}

function normalizeId(value) {
    const id = Number(value);
    return Number.isFinite(id) && id >= -1 ? id : null;
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

function setAbilityDefinitions(cache, abilities) {
    cache.abilities = abilities;
    cache.abilityIndex = buildAbilityIndex(abilities);
    cache.abilitiesByCard = buildAbilitiesByCard(abilities);
}

async function fetchCards(scope) {
    const response = await fetch(`/api/cards?scope=${encodeURIComponent(scope)}`, {
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

async function fetchAbilities(scope) {
    const response = await fetch(`/api/abilities?scope=${encodeURIComponent(scope)}`);
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    const data = await response.json();
    if (!Array.isArray(data)) {
        throw new TypeError('Ability definitions response must be an array.');
    }
    return data;
}

export async function ensureCardDefinitions(scope = null) {
    const { scope: resolvedScope, cache } = getCache(scope);
    if (cache.cards) {
        return cache.cards;
    }
    if (!cache.cardsPromise) {
        const request = fetchCards(resolvedScope)
            .then(cards => {
                if (cache.cardsPromise === request) {
                    cache.cards = cards;
                    cache.cardIndex = buildCardIndex(cards);
                }
                return cards;
            })
            .finally(() => {
                if (cache.cardsPromise === request) {
                    cache.cardsPromise = null;
                }
            });
        cache.cardsPromise = request;
    }
    return await cache.cardsPromise;
}

export async function ensureAbilityDefinitions(scope = null) {
    const { scope: resolvedScope, cache } = getCache(scope);
    if (cache.abilities) {
        return cache.abilities;
    }
    if (!cache.abilitiesPromise) {
        const request = fetchAbilities(resolvedScope)
            .then(abilities => {
                if (cache.abilitiesPromise === request) {
                    setAbilityDefinitions(cache, abilities);
                }
                return abilities;
            })
            .finally(() => {
                if (cache.abilitiesPromise === request) {
                    cache.abilitiesPromise = null;
                }
            });
        cache.abilitiesPromise = request;
    }
    return await cache.abilitiesPromise;
}

export async function ensureAbilityData(scope = null) {
    const { scope: resolvedScope, cache } = getCache(scope);
    await Promise.all([ensureCardDefinitions(resolvedScope), ensureAbilityDefinitions(resolvedScope)]);
    return {
        scope: resolvedScope,
        cards: cache.cards || [],
        abilities: cache.abilities || [],
        cardIndex: cache.cardIndex,
        abilityIndex: cache.abilityIndex,
        abilitiesByCard: cache.abilitiesByCard
    };
}

export function getAbilityDataSnapshot(scope = null) {
    const { scope: resolvedScope, cache } = getCache(scope);
    return {
        scope: resolvedScope,
        cards: cache.cards || [],
        abilities: cache.abilities || [],
        cardIndex: cache.cardIndex,
        abilityIndex: cache.abilityIndex,
        abilitiesByCard: cache.abilitiesByCard
    };
}

export function clearAbilityDataCache(scope = null) {
    if (scope === 'all') {
        for (const entry of cachesByScope.values()) {
            entry.cards = null;
            entry.abilities = null;
            entry.cardIndex = new Map();
            entry.abilityIndex = new Map();
            entry.abilitiesByCard = new Map();
            entry.cardsPromise = null;
            entry.abilitiesPromise = null;
        }
        return;
    }
    const { cache } = getCache(scope);
    cache.cards = null;
    cache.abilities = null;
    cache.cardIndex = new Map();
    cache.abilityIndex = new Map();
    cache.abilitiesByCard = new Map();
    cache.cardsPromise = null;
    cache.abilitiesPromise = null;
}

export function markAbilityDataCacheDirty(scope = null) {
    const { cache } = getCache(scope);
    cache.abilities = null;
    cache.abilityIndex = new Map();
    cache.abilitiesByCard = new Map();
    cache.abilitiesPromise = null;
}

export function upsertAbilityDataCacheEntry(ability, scope = null) {
    const { cache } = getCache(scope);
    const abilityId = normalizeId(ability?.id ?? ability?.Id);
    if (abilityId == null) {
        markAbilityDataCacheDirty(scope);
        return;
    }

    if (!cache.abilities) {
        return;
    }

    const nextAbilities = cache.abilities.filter(existing => {
        const existingId = normalizeId(existing?.id ?? existing?.Id);
        return existingId !== abilityId;
    });
    nextAbilities.push(ability);
    setAbilityDefinitions(cache, nextAbilities);
}

export function removeAbilityDataCacheEntry(abilityId, scope = null) {
    const { cache } = getCache(scope);
    const normalizedId = normalizeId(abilityId);
    if (normalizedId == null) {
        markAbilityDataCacheDirty(scope);
        return;
    }

    if (!cache.abilities) {
        return;
    }

    const nextAbilities = cache.abilities.filter(existing => {
        const existingId = normalizeId(existing?.id ?? existing?.Id);
        return existingId !== normalizedId;
    });
    setAbilityDefinitions(cache, nextAbilities);
}

export async function refreshAbilityDataCache(scope = null) {
    clearAbilityDataCache(scope);
    return await ensureAbilityData(scope);
}

document.addEventListener(CARD_CATALOG_INVALIDATED_EVENT, (event) => {
    const detail = event?.detail || {};
    if (isAbilityOnlyInvalidation(detail)) {
        return;
    }
    const targetScopes = detail.scope
        ? [normalizeCardPoolScope(detail.scope)]
        : ['standard', 'mirage'];
    for (const s of targetScopes) {
        const entry = cachesByScope.get(s);
        if (!entry) continue;
        entry.cards = null;
        entry.cardIndex = new Map();
        entry.cardsPromise = null;
    }
});
