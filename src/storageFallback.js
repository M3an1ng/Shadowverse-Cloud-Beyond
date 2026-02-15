// src/storageFallback.js
// Storage helper with graceful fallback between localStorage/sessionStorage.

function getStorageByName(name) {
    if (typeof window === 'undefined') return null;
    try {
        return window[name] ?? null;
    } catch {
        return null;
    }
}

function listKnownStorages() {
    return [getStorageByName('localStorage'), getStorageByName('sessionStorage')].filter(Boolean);
}

export function getStoredValue(key) {
    if (!key) return null;
    const storages = listKnownStorages();
    for (const storage of storages) {
        try {
            const value = storage.getItem(key);
            if (value !== null && value !== undefined) {
                return value;
            }
        } catch {
            // Try the next storage.
        }
    }
    return null;
}

export function setStoredValue(key, value) {
    if (!key) return false;
    const normalized = String(value ?? '');
    const storages = listKnownStorages();
    let wroteAny = false;
    for (const storage of storages) {
        try {
            storage.setItem(key, normalized);
            wroteAny = true;
        } catch {
            // Try the next storage.
        }
    }
    return wroteAny;
}

export function removeStoredValue(key) {
    if (!key) return false;
    const storages = listKnownStorages();
    let removedAny = false;
    for (const storage of storages) {
        try {
            storage.removeItem(key);
            removedAny = true;
        } catch {
            // Try the next storage.
        }
    }
    return removedAny;
}
