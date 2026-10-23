import { isAbilityOnlyInvalidation } from './dataChangeEvents.js';

function getCardDefinitionId(card) {
    const id = Number(card?.id ?? card?.Id ?? card?.cardId ?? card?.CardId);
    return Number.isFinite(id) && id > 0 ? id : null;
}

function normalizeIdList(values) {
    if (!Array.isArray(values)) {
        return [];
    }
    return Array.from(new Set(values
        .map(value => Number(value))
        .filter(value => Number.isFinite(value) && value > 0)));
}

function isDeckBuilderCollectibleCard(card) {
    const collectible = card?.isCollectible ?? card?.IsCollectible;
    if (collectible !== true) return false;
    const rarity = String(card?.rarity ?? card?.Rarity ?? '').trim().toLowerCase();
    if (rarity === 'special') return false;
    const type = String(card?.type ?? card?.Type ?? card?.cardType ?? card?.card_type ?? '').trim().toLowerCase();
    return type !== 'leader';
}

function upsertCard(list, card, cardId) {
    const next = Array.isArray(list) ? [...list] : [];
    const index = next.findIndex(item => getCardDefinitionId(item) === cardId);
    if (index >= 0) {
        next[index] = card;
    } else {
        next.push(card);
    }
    return next;
}

function removeCards(list, deletedIds) {
    if (!Array.isArray(list)) {
        return [];
    }
    if (!deletedIds.length) {
        return list;
    }
    const deletedSet = new Set(deletedIds);
    return list.filter(card => !deletedSet.has(getCardDefinitionId(card)));
}

function rebuildIndexes(state) {
    const rawCards = Array.isArray(state.rawCardsCache) ? state.rawCardsCache : [];
    state.rawCardIndex = new Map(rawCards
        .map(card => [getCardDefinitionId(card), card])
        .filter(([id]) => id != null));

    state.availableCards = rawCards.filter(card => getCardDefinitionId(card) != null && isDeckBuilderCollectibleCard(card));
    state.cardIndex = new Map(state.availableCards.map(card => [getCardDefinitionId(card), card]));
}

export function normalizeCardCatalogPatch(detail = {}) {
    const changedCards = Array.isArray(detail.changedCards) ? detail.changedCards : [];
    const changedIds = changedCards
        .map(card => getCardDefinitionId(card))
        .filter(id => id != null);
    const deletedIds = normalizeIdList(detail.deletedCardIds);

    return {
        changedCards,
        changedIds,
        deletedIds,
        hasPatch: changedCards.length > 0 || deletedIds.length > 0
    };
}

export function canApplyDeckBuilderCardCatalogPatch(state, detail = {}) {
    const patch = normalizeCardCatalogPatch(detail);
    if (!patch.hasPatch) {
        return false;
    }
    return Array.isArray(state?.rawCardsCache);
}

export function shouldSkipDeckBuilderCatalogReload(detail = {}) {
    return isAbilityOnlyInvalidation(detail);
}

export function applyDeckBuilderCardCatalogPatch(state, detail = {}) {
    const patch = normalizeCardCatalogPatch(detail);
    if (!patch.hasPatch) {
        return {
            applied: false,
            changedIds: [],
            deletedIds: []
        };
    }

    let rawCards = removeCards(state.rawCardsCache, patch.deletedIds);
    patch.changedCards.forEach(card => {
        const cardId = getCardDefinitionId(card);
        if (cardId == null || patch.deletedIds.includes(cardId)) {
            return;
        }
        rawCards = upsertCard(rawCards, card, cardId);
    });

    state.rawCardsCache = rawCards;
    rebuildIndexes(state);

    return {
        applied: true,
        changedIds: patch.changedIds,
        deletedIds: patch.deletedIds
    };
}
