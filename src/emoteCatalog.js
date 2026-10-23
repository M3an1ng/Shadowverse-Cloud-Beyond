const EMPTY_CATALOG = Object.freeze({ packs: [] });

let cachedLoadResult = null;

export function normalizeEmoteCatalogResponse(rawCatalog) {
    const rawPacks = Array.isArray(rawCatalog?.packs)
        ? rawCatalog.packs
        : (Array.isArray(rawCatalog?.Packs) ? rawCatalog.Packs : []);

    const packs = [];
    rawPacks.forEach(rawPack => {
        const id = normalizeString(rawPack?.id ?? rawPack?.Id);
        if (!id) return;

        const rawItems = Array.isArray(rawPack?.items)
            ? rawPack.items
            : (Array.isArray(rawPack?.Items) ? rawPack.Items : []);
        const items = rawItems
            .map(rawItem => normalizeEmoteItem(rawItem, id))
            .filter(Boolean);
        if (items.length === 0) return;

        packs.push({
            id,
            name: normalizeString(rawPack?.name ?? rawPack?.Name) || id,
            items
        });
    });

    return { packs };
}

export async function loadEmoteCatalog() {
    if (cachedLoadResult?.ok) {
        return cachedLoadResult;
    }

    if (typeof fetch !== 'function') {
        return buildFailedResult('当前环境无法加载表情清单。');
    }

    try {
        const response = await fetch('/api/emotes', {
            method: 'GET',
            headers: { Accept: 'application/json' }
        });
        if (!response?.ok) {
            return buildFailedResult(`表情清单加载失败（${response?.status ?? 'network'}）。`);
        }

        const catalog = normalizeEmoteCatalogResponse(await response.json());
        cachedLoadResult = { ok: true, catalog, error: '' };
        return cachedLoadResult;
    } catch (error) {
        return buildFailedResult(error?.message || '表情清单加载失败。');
    }
}

export function findEmoteById(catalog, emoteId) {
    const normalizedId = normalizeString(emoteId);
    if (!normalizedId) return null;

    const packs = Array.isArray(catalog?.packs) ? catalog.packs : [];
    for (const pack of packs) {
        const item = Array.isArray(pack?.items)
            ? pack.items.find(candidate => candidate?.id === normalizedId)
            : null;
        if (item) return item;
    }
    return null;
}

export function hasUsableEmotes(catalog) {
    return Array.isArray(catalog?.packs)
        && catalog.packs.some(pack => Array.isArray(pack?.items) && pack.items.length > 0);
}

export function resetEmoteCatalogCache() {
    cachedLoadResult = null;
}

function normalizeEmoteItem(rawItem, fallbackPackId) {
    const id = normalizeString(rawItem?.id ?? rawItem?.Id);
    const packId = normalizeString(rawItem?.packId ?? rawItem?.PackId) || fallbackPackId;
    const url = normalizeString(rawItem?.url ?? rawItem?.Url);
    if (!id || !packId || !url) return null;

    return {
        id,
        packId,
        name: normalizeString(rawItem?.name ?? rawItem?.Name) || id,
        url
    };
}

function normalizeString(value) {
    return String(value ?? '').trim();
}

function buildFailedResult(error) {
    return {
        ok: false,
        catalog: EMPTY_CATALOG,
        error: normalizeString(error) || '表情清单加载失败。'
    };
}
