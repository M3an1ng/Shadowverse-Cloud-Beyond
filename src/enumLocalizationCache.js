const enumCache = new Map();

function normalizeEnumKey(value) {
    const text = String(value ?? '').trim();
    return text ? text.toLowerCase() : '';
}

function buildEnumLookup(entries) {
    const lookup = new Map();
    Object.entries(entries || {}).forEach(([key, label]) => {
        const normalizedKey = normalizeEnumKey(key);
        if (!normalizedKey || !label) return;
        if (!lookup.has(normalizedKey)) {
            lookup.set(normalizedKey, String(label).trim());
        }
    });
    return lookup;
}

async function fetchEnumLocalization(enumName) {
    const response = await fetch(`/api/localization/enums/${encodeURIComponent(enumName)}`);
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    return await response.json();
}

export async function ensureEnumLocalization(enumName) {
    const name = String(enumName ?? '').trim();
    if (!name) {
        return { raw: {}, lookup: new Map() };
    }

    const cached = enumCache.get(name);
    if (cached?.lookup) return cached;
    if (!cached?.promise) {
        const promise = fetchEnumLocalization(name)
            .then(raw => {
                const entry = {
                    raw: raw || {},
                    lookup: buildEnumLookup(raw)
                };
                enumCache.set(name, entry);
                return entry;
            })
            .catch(error => {
                console.warn(`EnumLocalizationCache: Failed to load ${name}.`, error);
                const entry = { raw: {}, lookup: new Map() };
                enumCache.set(name, entry);
                return entry;
            });

        enumCache.set(name, { promise });
    }

    return await enumCache.get(name).promise;
}

export async function ensureEnumLocalizations(enumNames = []) {
    const names = Array.isArray(enumNames) ? enumNames : [enumNames];
    return await Promise.all(names.map(name => ensureEnumLocalization(name)));
}

export function getCachedEnumLabel(enumName, value, fallback = '') {
    const name = String(enumName ?? '').trim();
    if (!name) return fallback;
    const key = normalizeEnumKey(value);
    if (!key) return fallback;
    const entry = enumCache.get(name);
    const label = entry?.lookup?.get(key);
    return label ?? fallback ?? '';
}

export function getCachedEnumRaw(enumName) {
    const name = String(enumName ?? '').trim();
    if (!name) return {};
    return enumCache.get(name)?.raw || {};
}
