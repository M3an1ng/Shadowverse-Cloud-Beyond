function normalizeNumericCode(value) {
    const text = String(value ?? '').trim();
    if (!text || !/^\d+$/.test(text)) {
        return null;
    }

    const parsed = Number.parseInt(text, 10);
    return Number.isFinite(parsed) ? parsed : null;
}

function getPrimaryLabel(entry) {
    return String(
        entry?.label
        ?? entry?.name
        ?? entry?.Name
        ?? entry?.value
        ?? ''
    ).trim();
}

function getSortCode(entry) {
    const directCode = normalizeNumericCode(
        entry?.code
        ?? entry?.set_code
        ?? entry?.setCode
        ?? entry?.SetCode
        ?? entry?.card_set_code
        ?? entry?.cardSetCode
        ?? entry?.CardSetCode
    );
    if (directCode !== null) {
        return directCode;
    }

    const region = normalizeNumericCode(entry?.region ?? entry?.Region);
    const sequence = normalizeNumericCode(entry?.sequence ?? entry?.Sequence);
    if (region === null || sequence === null) {
        return null;
    }

    return region * 100 + sequence;
}

export function compareCardSetDisplayOrder(left, right) {
    const leftCode = getSortCode(left);
    const rightCode = getSortCode(right);

    if (leftCode !== null || rightCode !== null) {
        if (leftCode === null) return 1;
        if (rightCode === null) return -1;
        if (leftCode !== rightCode) {
            return leftCode - rightCode;
        }
    }

    return getPrimaryLabel(left).localeCompare(getPrimaryLabel(right), undefined, {
        numeric: true,
        sensitivity: 'base'
    });
}
