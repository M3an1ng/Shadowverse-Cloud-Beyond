async function createHttpError(response) {
    let message = response?.statusText || '';
    try {
        const payload = await response.json();
        message = payload?.message || payload?.Message || message;
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
    if (typeof setCached === 'function') {
        setCached(cards);
    }
}

export async function loadCardDefinitions(options = {}) {
    const {
        useCache = true,
        force = false,
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

    const response = await fetch('/api/cards', { method: 'GET' });
    if (!response.ok) {
        throw await createHttpError(response);
    }

    if (typeof onResponse === 'function') {
        onResponse(response);
    }

    const payload = await response.json();
    const cards = Array.isArray(payload) ? payload : [];
    writeCache(setCached, cards);
    return cards;
}

