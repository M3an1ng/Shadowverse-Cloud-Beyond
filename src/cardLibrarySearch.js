function normalizeText(value) {
    return String(value ?? '').trim().toLowerCase();
}

function resolveCardDescription(card) {
    const directDescription = card?.description ?? card?.Description;
    if (directDescription !== undefined && directDescription !== null && directDescription !== '') {
        return directDescription;
    }
    if (Array.isArray(card?.effectDescriptions)) {
        return card.effectDescriptions.filter(Boolean).join('\n');
    }
    return '';
}

export function matchesCardLibrarySearch(card, searchText) {
    const normalizedSearch = normalizeText(searchText);
    if (!normalizedSearch) return true;

    const cardName = normalizeText(card?.name ?? card?.Name);
    if (cardName.includes(normalizedSearch)) return true;

    const cardDescription = normalizeText(resolveCardDescription(card));
    return cardDescription.includes(normalizedSearch);
}
