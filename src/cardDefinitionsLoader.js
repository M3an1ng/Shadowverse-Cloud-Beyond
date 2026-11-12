async function createHttpError(response) {
    let message = response?.statusText || '';
    try {
        const payload = await response.json();
        message = payload?.message || payload?.Message || payload?.error || message;
    } catch {
        // Ignore parse errors and fall back to status text.
    }

    return new Error(message || `HTTP ${response?.status ?? 'Error'}`);
}

function readCache(getCached) {
    if (typeof getCached !== 'function') return null;
    const cached = getCached();
    return Array.isArray(cached) ? cached : null;
}

function writeCache(setCached, cards) {
    if (typeof setCached !== 'function') return;
    setCached(cards);
}

export function normalizeCardPoolScope(scope) {
    return String(scope ?? '').trim().toLowerCase() === 'mirage' ? 'mirage' : 'standard';
}

export async function loadCardDefinitions(options = {}) {
    const {
        useCache = true,
        force = false,
        scope = 'standard',
        getCached,
        setCached,
        onResponse
    } = options;

    if (useCache && !force) {
        const cached = readCache(getCached);
        if (cached) {
            return cached;
        }
    }

    const normalizedScope = normalizeCardPoolScope(scope);
    const url = `/api/cards?scope=${encodeURIComponent(normalizedScope)}`;

    const response = await fetch(url, {
        method: 'GET',
        cache: 'no-store'
    });
    if (!response.ok) {
        throw await createHttpError(response);
    }

    const payload = await response.json();
    if (!Array.isArray(payload)) {
        throw new TypeError('Card definitions response must be an array.');
    }
    if (typeof onResponse === 'function') {
        onResponse(response);
    }
    writeCache(setCached, payload);
    return payload;
}
