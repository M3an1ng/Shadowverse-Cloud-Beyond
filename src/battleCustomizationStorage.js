const DATABASE_NAME = 'svb-battle-customization';
const DATABASE_VERSION = 1;
const ASSET_STORE_NAME = 'assets';
const PREFERENCE_STORE_NAME = 'preferences';

export const BATTLE_CUSTOMIZATION_ASSET_KEYS = Object.freeze({
    background: 'background',
    bgm: 'bgm'
});

export const BATTLE_CUSTOMIZATION_PREFERENCE_KEYS = Object.freeze({
    bgmEnabled: 'bgmEnabled'
});

export const BATTLE_CUSTOMIZATION_LIMITS = Object.freeze({
    background: Object.freeze({
        supportedTypes: Object.freeze(['image/png', 'image/jpeg', 'image/webp']),
        maxBytes: 10 * 1024 * 1024
    }),
    bgm: Object.freeze({
        supportedTypes: Object.freeze(['audio/mpeg', 'audio/ogg', 'audio/wav', 'audio/mp4']),
        maxBytes: 20 * 1024 * 1024
    })
});

function getIndexedDbFactory(providedFactory) {
    if (providedFactory) {
        return providedFactory;
    }
    if (typeof indexedDB !== 'undefined') {
        return indexedDB;
    }
    return null;
}

function normalizeAssetKey(rawValue) {
    const value = String(rawValue ?? '').trim().toLowerCase();
    if (value === BATTLE_CUSTOMIZATION_ASSET_KEYS.background) {
        return BATTLE_CUSTOMIZATION_ASSET_KEYS.background;
    }
    if (value === BATTLE_CUSTOMIZATION_ASSET_KEYS.bgm) {
        return BATTLE_CUSTOMIZATION_ASSET_KEYS.bgm;
    }
    return null;
}

function formatBytes(byteCount) {
    const value = Number(byteCount) || 0;
    if (value < 1024) {
        return `${value} B`;
    }
    if (value < 1024 * 1024) {
        return `${(value / 1024).toFixed(1)} KB`;
    }
    return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

function buildValidationMessage(kind, code, config) {
    if (code === 'invalid-kind') {
        return '未知的本地自定义资源类型。';
    }
    if (code === 'missing-file') {
        return '请选择一个有效的本地文件。';
    }
    if (code === 'unsupported-type') {
        const supported = config?.supportedTypes?.join(', ') || '无';
        return `不支持的文件类型。支持类型：${supported}`;
    }
    if (code === 'too-large') {
        return `文件过大，最大允许 ${formatBytes(config?.maxBytes || 0)}。`;
    }
    return '文件校验失败。';
}

export function validateBattleCustomizationFile(kind, file) {
    const normalizedKind = normalizeAssetKey(kind);
    const config = normalizedKind ? BATTLE_CUSTOMIZATION_LIMITS[normalizedKind] : null;

    if (!normalizedKind || !config) {
        return {
            ok: false,
            kind: normalizedKind,
            code: 'invalid-kind',
            message: buildValidationMessage(normalizedKind, 'invalid-kind', config)
        };
    }

    if (!file || typeof file.size !== 'number') {
        return {
            ok: false,
            kind: normalizedKind,
            code: 'missing-file',
            message: buildValidationMessage(normalizedKind, 'missing-file', config)
        };
    }

    const mimeType = String(file.type ?? '').trim().toLowerCase();
    if (!config.supportedTypes.includes(mimeType)) {
        return {
            ok: false,
            kind: normalizedKind,
            code: 'unsupported-type',
            message: buildValidationMessage(normalizedKind, 'unsupported-type', config),
            supportedTypes: config.supportedTypes,
            receivedType: mimeType
        };
    }

    const size = Number(file.size) || 0;
    if (size > config.maxBytes) {
        return {
            ok: false,
            kind: normalizedKind,
            code: 'too-large',
            message: buildValidationMessage(normalizedKind, 'too-large', config),
            maxBytes: config.maxBytes,
            size
        };
    }

    return {
        ok: true,
        kind: normalizedKind,
        code: 'ok',
        message: '',
        supportedTypes: config.supportedTypes,
        maxBytes: config.maxBytes
    };
}

function createStorageError(code, message, cause) {
    const error = new Error(message);
    error.code = code;
    if (cause) {
        error.cause = cause;
    }
    return error;
}

function wrapRequest(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => {
            resolve(request.result);
        };
        request.onerror = () => {
            reject(request.error || createStorageError('request-failed', 'IndexedDB request failed.'));
        };
    });
}

function wrapTransaction(transaction, resultPromise) {
    return new Promise((resolve, reject) => {
        transaction.oncomplete = async () => {
            try {
                resolve(await resultPromise);
            } catch (error) {
                reject(error);
            }
        };
        transaction.onerror = () => {
            reject(transaction.error || createStorageError('transaction-failed', 'IndexedDB transaction failed.'));
        };
        transaction.onabort = () => {
            reject(transaction.error || createStorageError('transaction-aborted', 'IndexedDB transaction aborted.'));
        };
    });
}

function toFiniteNumber(value) {
    const numeric = Number(value);
    return Number.isFinite(numeric) ? numeric : null;
}

function normalizeBackgroundMetadata(metadata) {
    if (!metadata || typeof metadata !== 'object' || Array.isArray(metadata)) {
        return null;
    }

    const naturalWidth = toFiniteNumber(metadata.naturalWidth);
    const naturalHeight = toFiniteNumber(metadata.naturalHeight);
    const crop = metadata.crop;

    if (!naturalWidth || !naturalHeight || !crop || typeof crop !== 'object' || Array.isArray(crop)) {
        return null;
    }

    const x = toFiniteNumber(crop.x);
    const y = toFiniteNumber(crop.y);
    const width = toFiniteNumber(crop.width);
    const height = toFiniteNumber(crop.height);

    if (
        x === null
        || y === null
        || width === null
        || height === null
        || width <= 0
        || height <= 0
        || x < 0
        || y < 0
        || x + width > 1
        || y + height > 1
    ) {
        return null;
    }

    return {
        naturalWidth,
        naturalHeight,
        crop: { x, y, width, height }
    };
}

function normalizeAssetMetadata(key, metadata) {
    if (key === BATTLE_CUSTOMIZATION_ASSET_KEYS.background) {
        return normalizeBackgroundMetadata(metadata);
    }
    return null;
}

function normalizeAssetRecord(record) {
    if (!record || typeof record !== 'object') {
        return null;
    }

    const key = normalizeAssetKey(record.key);
    if (!key || !(record.blob instanceof Blob)) {
        return null;
    }

    return {
        key,
        blob: record.blob,
        fileName: String(record.fileName ?? '').trim(),
        mimeType: String(record.mimeType ?? record.blob.type ?? '').trim().toLowerCase(),
        size: Number(record.size ?? record.blob.size ?? 0) || 0,
        updatedAt: String(record.updatedAt ?? '').trim(),
        metadata: normalizeAssetMetadata(key, record.metadata)
    };
}

export function createBattleCustomizationStorage(options = {}) {
    const indexedDbFactory = getIndexedDbFactory(options.indexedDB);
    let openPromise = null;

    function isSupported() {
        return Boolean(indexedDbFactory);
    }

    async function openDatabase() {
        if (!indexedDbFactory) {
            throw createStorageError('unsupported', 'IndexedDB is not available in this environment.');
        }

        if (!openPromise) {
            openPromise = new Promise((resolve, reject) => {
                const request = indexedDbFactory.open(DATABASE_NAME, DATABASE_VERSION);

                request.onupgradeneeded = () => {
                    const db = request.result;
                    if (!db.objectStoreNames.contains(ASSET_STORE_NAME)) {
                        db.createObjectStore(ASSET_STORE_NAME, { keyPath: 'key' });
                    }
                    if (!db.objectStoreNames.contains(PREFERENCE_STORE_NAME)) {
                        db.createObjectStore(PREFERENCE_STORE_NAME, { keyPath: 'key' });
                    }
                };

                request.onsuccess = () => {
                    resolve(request.result);
                };

                request.onerror = () => {
                    reject(request.error || createStorageError('open-failed', 'Unable to open IndexedDB database.'));
                };
            });
        }

        return openPromise;
    }

    async function getRecord(storeName, key) {
        const db = await openDatabase();
        const transaction = db.transaction(storeName, 'readonly');
        const store = transaction.objectStore(storeName);
        const request = store.get(key);
        return await wrapRequest(request);
    }

    async function putRecord(storeName, value) {
        const db = await openDatabase();
        const transaction = db.transaction(storeName, 'readwrite');
        const store = transaction.objectStore(storeName);
        const request = store.put(value);
        return await wrapTransaction(transaction, wrapRequest(request));
    }

    async function deleteRecord(storeName, key) {
        const db = await openDatabase();
        const transaction = db.transaction(storeName, 'readwrite');
        const store = transaction.objectStore(storeName);
        const request = store.delete(key);
        return await wrapTransaction(transaction, wrapRequest(request));
    }

    async function saveAsset(kind, file, options = {}) {
        const validation = validateBattleCustomizationFile(kind, file);
        if (!validation.ok) {
            throw createStorageError(validation.code, validation.message);
        }

        const normalizedKind = validation.kind;
        const blob = file instanceof Blob ? file : new Blob([file], { type: file?.type || '' });
        const record = {
            key: normalizedKind,
            blob,
            fileName: String(file?.name ?? '').trim(),
            mimeType: String(blob.type ?? '').trim().toLowerCase(),
            size: Number(blob.size ?? 0) || 0,
            updatedAt: new Date().toISOString(),
            metadata: normalizeAssetMetadata(normalizedKind, options.metadata)
        };

        await putRecord(ASSET_STORE_NAME, record);
        return normalizeAssetRecord(record);
    }

    async function getAsset(kind) {
        const normalizedKind = normalizeAssetKey(kind);
        if (!normalizedKind) {
            return null;
        }

        const record = await getRecord(ASSET_STORE_NAME, normalizedKind);
        return normalizeAssetRecord(record);
    }

    async function removeAsset(kind) {
        const normalizedKind = normalizeAssetKey(kind);
        if (!normalizedKind) {
            return false;
        }

        await deleteRecord(ASSET_STORE_NAME, normalizedKind);
        return true;
    }

    async function setPreference(key, value) {
        const normalizedKey = String(key ?? '').trim();
        if (!normalizedKey) {
            throw createStorageError('invalid-key', 'Preference key is required.');
        }

        const record = {
            key: normalizedKey,
            value,
            updatedAt: new Date().toISOString()
        };
        await putRecord(PREFERENCE_STORE_NAME, record);
        return record.value;
    }

    async function getPreference(key, fallbackValue = null) {
        const normalizedKey = String(key ?? '').trim();
        if (!normalizedKey) {
            return fallbackValue;
        }

        const record = await getRecord(PREFERENCE_STORE_NAME, normalizedKey);
        if (!record || typeof record !== 'object' || !Object.prototype.hasOwnProperty.call(record, 'value')) {
            return fallbackValue;
        }
        return record.value;
    }

    async function removePreference(key) {
        const normalizedKey = String(key ?? '').trim();
        if (!normalizedKey) {
            return false;
        }

        await deleteRecord(PREFERENCE_STORE_NAME, normalizedKey);
        return true;
    }

    return {
        isSupported,
        openDatabase,
        saveAsset,
        getAsset,
        removeAsset,
        setPreference,
        getPreference,
        removePreference
    };
}

export const battleCustomizationStorage = createBattleCustomizationStorage();
