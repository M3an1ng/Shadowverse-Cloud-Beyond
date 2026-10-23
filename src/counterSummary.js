const PLAYER_SYSTEM_COUNTER_KEYS = new Set(['combo', 'graveyard', 'bloodpool', 'rally']);

function normalizeCounterKey(key) {
    return String(key ?? '').trim();
}

function getCaseInsensitiveObjectValue(source, key) {
    if (!source || typeof source !== 'object') {
        return undefined;
    }

    const normalizedKey = normalizeCounterKey(key);
    if (!normalizedKey) {
        return undefined;
    }

    if (Object.prototype.hasOwnProperty.call(source, normalizedKey)) {
        return source[normalizedKey];
    }

    const lowerKey = normalizedKey.toLowerCase();
    for (const [entryKey, entryValue] of Object.entries(source)) {
        if (String(entryKey).trim().toLowerCase() === lowerKey) {
            return entryValue;
        }
    }

    return undefined;
}

function normalizeSummaryEntry(entry) {
    if (!entry || typeof entry !== 'object') {
        return null;
    }

    const key = normalizeCounterKey(entry.key ?? entry.Key);
    if (!key) {
        return null;
    }

    const rawLabel = entry.label ?? entry.Label ?? key;
    const label = String(rawLabel ?? '').trim();
    if (!label) {
        return null;
    }

    const numericValue = Number(entry.value ?? entry.Value);
    if (!Number.isFinite(numericValue)) {
        return null;
    }

    return {
        key,
        label,
        value: Math.max(0, Math.trunc(numericValue))
    };
}

export function buildCounterSummaryEntries(options = {}) {
    const {
        summaryEntries,
        localCounterMap,
        localLabelMap,
        ownerCounterMap,
        ownerLabelMap,
        ownerReferenceKeys = [],
        includeOwnerCounters = false,
        hideOwnerSystemKeys = false,
        showZeroSummaryEntries = false,
        showZeroOwnerEntries = false,
        resolveLabel = () => ''
    } = options;

    const entries = [];
    const localKeys = new Set();

    if (Array.isArray(summaryEntries) && summaryEntries.length > 0) {
        for (const entry of summaryEntries) {
            const normalized = normalizeSummaryEntry(entry);
            if (!normalized) {
                continue;
            }

            const lowerKey = normalized.key.toLowerCase();
            if (localKeys.has(lowerKey)) {
                continue;
            }

            if (hideOwnerSystemKeys && PLAYER_SYSTEM_COUNTER_KEYS.has(lowerKey)) {
                continue;
            }

            if (!showZeroSummaryEntries && normalized.value === 0) {
                continue;
            }

            entries.push(normalized);
            localKeys.add(lowerKey);
        }
    } else if (localCounterMap && typeof localCounterMap === 'object') {
        for (const [key, value] of Object.entries(localCounterMap)) {
            const normalizedKey = normalizeCounterKey(key);
            if (!normalizedKey) {
                continue;
            }

            const label = resolveLabel(localLabelMap, normalizedKey);
            if (!label) {
                continue;
            }

            const numericValue = Number(value);
            if (!Number.isFinite(numericValue)) {
                continue;
            }

            const displayValue = Math.max(0, Math.trunc(numericValue));
            if (displayValue === 0) {
                continue;
            }

            entries.push({
                key: normalizedKey,
                label,
                value: displayValue
            });
            localKeys.add(normalizedKey.toLowerCase());
        }
    }

    if (includeOwnerCounters && ownerCounterMap && typeof ownerCounterMap === 'object') {
        const candidateKeys = Array.isArray(ownerReferenceKeys)
            ? ownerReferenceKeys
            : [];

        for (const key of candidateKeys) {
            const normalizedKey = normalizeCounterKey(key);
            if (!normalizedKey) {
                continue;
            }

            const lowerKey = normalizedKey.toLowerCase();
            if (localKeys.has(lowerKey)) {
                continue;
            }

            if (hideOwnerSystemKeys && PLAYER_SYSTEM_COUNTER_KEYS.has(lowerKey)) {
                continue;
            }

            const value = getCaseInsensitiveObjectValue(ownerCounterMap, normalizedKey);
            if (value === undefined) {
                continue;
            }

            const label = resolveLabel(ownerLabelMap, normalizedKey)
                || resolveLabel(localLabelMap, normalizedKey);
            if (!label) {
                continue;
            }

            const numericValue = Number(value);
            if (!Number.isFinite(numericValue)) {
                continue;
            }

            const displayValue = Math.max(0, Math.trunc(numericValue));
            if (!showZeroOwnerEntries && displayValue === 0) {
                continue;
            }

            entries.push({
                key: normalizedKey,
                label,
                value: displayValue
            });
        }
    }

    if (!Array.isArray(summaryEntries) || summaryEntries.length === 0) {
        entries.sort((a, b) => a.key.localeCompare(b.key, 'zh-CN', { sensitivity: 'base' }));
    }
    return entries;
}

export { PLAYER_SYSTEM_COUNTER_KEYS };
