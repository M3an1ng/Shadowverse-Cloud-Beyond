export const CARD_CATALOG_INVALIDATED_EVENT = 'svb:card-catalog-invalidated';
export const DECK_VALIDATION_RULES_UPDATED_EVENT = 'svb:deck-validation-rules-updated';

const DATA_REVISION_HEADER = 'X-Data-Revision';
const ABILITY_ONLY_REASONS = new Set([
    'ability-created',
    'ability-updated',
    'ability-deleted',
    'ability-linked',
    'ability-unlinked'
]);

let latestDataRevision = null;

function buildEventDetail(detail = {}) {
    return {
        at: Date.now(),
        ...detail
    };
}

export function getDataRevisionFromResponse(response) {
    if (!response || !response.headers || typeof response.headers.get !== 'function') {
        return '';
    }
    return String(response.headers.get(DATA_REVISION_HEADER) || '').trim();
}

export function notifyCardCatalogInvalidated(detail = {}) {
    document.dispatchEvent(new CustomEvent(CARD_CATALOG_INVALIDATED_EVENT, {
        detail: buildEventDetail(detail)
    }));
}

export function notifyDeckValidationRulesUpdated(detail = {}) {
    document.dispatchEvent(new CustomEvent(DECK_VALIDATION_RULES_UPDATED_EVENT, {
        detail: buildEventDetail(detail)
    }));
}

export function isAbilityOnlyInvalidation(detail = {}) {
    const reason = String(detail?.reason ?? '').trim().toLowerCase();
    return ABILITY_ONLY_REASONS.has(reason);
}

export function syncDataRevisionFromResponse(response, detail = {}) {
    const revision = getDataRevisionFromResponse(response);
    if (!revision) {
        return false;
    }

    if (latestDataRevision === revision) {
        return false;
    }

    latestDataRevision = revision;
    notifyCardCatalogInvalidated({
        ...detail,
        revision
    });
    return true;
}
