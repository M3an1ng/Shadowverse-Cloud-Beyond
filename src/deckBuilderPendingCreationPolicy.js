export function createPendingDeckCreationRecord(requestId) {
    return {
        requestId: Number(requestId) || 0,
        deckId: null,
        abandoned: false
    };
}

export function markPendingDeckCreationAbandoned(record) {
    if (!record) return null;
    return {
        ...record,
        abandoned: true
    };
}

export function attachPendingDeckId(record, deckId) {
    if (!record) return null;
    return {
        ...record,
        deckId: deckId ? String(deckId) : null
    };
}

export function shouldCleanupPendingDeckCreation(record, currentRoute) {
    if (!record?.deckId) return false;
    return record.abandoned === true || String(currentRoute || '') !== 'deck-builder';
}
