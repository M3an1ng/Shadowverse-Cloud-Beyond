import {
    BATTLE_CUSTOMIZATION_ASSET_KEYS,
    BATTLE_CUSTOMIZATION_PREFERENCE_KEYS,
    BATTLE_CUSTOMIZATION_LIMITS,
    createBattleCustomizationStorage,
    validateBattleCustomizationFile
} from '../battleCustomizationStorage.js';

class FakeIdbRequest {
    constructor() {
        this.result = undefined;
        this.error = null;
        this.onsuccess = null;
        this.onerror = null;
        this.onupgradeneeded = null;
    }
}

class FakeObjectStoreData {
    constructor(keyPath) {
        this.keyPath = keyPath;
        this.records = new Map();
    }
}

class FakeObjectStore {
    constructor(storeData, transaction) {
        this.storeData = storeData;
        this.transaction = transaction;
    }

    get(key) {
        const request = new FakeIdbRequest();
        setTimeout(() => {
            request.result = this.storeData.records.has(key) ? this.storeData.records.get(key) : undefined;
            request.onsuccess?.({ target: request });
            this.transaction.finish();
        }, 0);
        return request;
    }

    put(value) {
        const request = new FakeIdbRequest();
        setTimeout(() => {
            const key = value[this.storeData.keyPath];
            this.storeData.records.set(key, value);
            request.result = key;
            request.onsuccess?.({ target: request });
            this.transaction.finish();
        }, 0);
        return request;
    }

    delete(key) {
        const request = new FakeIdbRequest();
        setTimeout(() => {
            this.storeData.records.delete(key);
            request.result = undefined;
            request.onsuccess?.({ target: request });
            this.transaction.finish();
        }, 0);
        return request;
    }
}

class FakeTransaction {
    constructor(storeData) {
        this.storeData = storeData;
        this.error = null;
        this.oncomplete = null;
        this.onerror = null;
        this.onabort = null;
    }

    objectStore() {
        return new FakeObjectStore(this.storeData, this);
    }

    finish() {
        setTimeout(() => {
            this.oncomplete?.({ target: this });
        }, 0);
    }
}

class FakeDatabase {
    constructor() {
        this.stores = new Map();
        this.objectStoreNames = {
            contains: (name) => this.stores.has(name)
        };
    }

    createObjectStore(name, options = {}) {
        const store = new FakeObjectStoreData(options.keyPath || 'id');
        this.stores.set(name, store);
        return store;
    }

    transaction(storeName) {
        return new FakeTransaction(this.stores.get(storeName));
    }
}

class FakeIndexedDbFactory {
    constructor() {
        this.database = null;
    }

    open() {
        const request = new FakeIdbRequest();
        setTimeout(() => {
            if (!this.database) {
                this.database = new FakeDatabase();
                request.result = this.database;
                request.onupgradeneeded?.({ target: request });
            }
            request.result = this.database;
            request.onsuccess?.({ target: request });
        }, 0);
        return request;
    }
}

function createFile(parts, name, options = {}) {
    return new File(parts, name, options);
}

describe('battle customization storage', () => {
    test('accepts supported background files within the size limit', () => {
        const file = createFile(['image-bytes'], 'playmat.png', { type: 'image/png' });
        const result = validateBattleCustomizationFile(BATTLE_CUSTOMIZATION_ASSET_KEYS.background, file);

        expect(result.ok).toBe(true);
        expect(result.kind).toBe(BATTLE_CUSTOMIZATION_ASSET_KEYS.background);
    });

    test('rejects unsupported BGM mime types', () => {
        const file = createFile(['video'], 'bgm.webm', { type: 'audio/webm' });
        const result = validateBattleCustomizationFile(BATTLE_CUSTOMIZATION_ASSET_KEYS.bgm, file);

        expect(result.ok).toBe(false);
        expect(result.code).toBe('unsupported-type');
        expect(result.message).toContain('不支持的文件类型');
    });

    test('rejects oversized background files', () => {
        const oversized = { size: BATTLE_CUSTOMIZATION_LIMITS.background.maxBytes + 1, type: 'image/png' };
        const result = validateBattleCustomizationFile(BATTLE_CUSTOMIZATION_ASSET_KEYS.background, oversized);

        expect(result.ok).toBe(false);
        expect(result.code).toBe('too-large');
    });

    test('saves, loads, replaces, and removes asset records', async () => {
        const storage = createBattleCustomizationStorage({ indexedDB: new FakeIndexedDbFactory() });
        const firstFile = createFile(['first'], 'first.png', { type: 'image/png' });
        const secondFile = createFile(['second'], 'second.png', { type: 'image/png' });
        const firstMetadata = {
            naturalWidth: 2400,
            naturalHeight: 1350,
            crop: {
                x: 0.125,
                y: 0.2,
                width: 0.75,
                height: 0.6
            }
        };

        const firstRecord = await storage.saveAsset(
            BATTLE_CUSTOMIZATION_ASSET_KEYS.background,
            firstFile,
            { metadata: firstMetadata }
        );
        expect(firstRecord.fileName).toBe('first.png');
        expect(firstRecord.metadata).toEqual(firstMetadata);

        const loadedFirst = await storage.getAsset(BATTLE_CUSTOMIZATION_ASSET_KEYS.background);
        expect(loadedFirst.fileName).toBe('first.png');
        expect(loadedFirst.blob.size).toBe(firstFile.size);
        expect(loadedFirst.metadata).toEqual(firstMetadata);

        await storage.saveAsset(BATTLE_CUSTOMIZATION_ASSET_KEYS.background, secondFile);
        const loadedSecond = await storage.getAsset(BATTLE_CUSTOMIZATION_ASSET_KEYS.background);
        expect(loadedSecond.fileName).toBe('second.png');
        expect(loadedSecond.blob.size).toBe(secondFile.size);
        expect(loadedSecond.metadata).toBeNull();

        await storage.removeAsset(BATTLE_CUSTOMIZATION_ASSET_KEYS.background);
        const removedRecord = await storage.getAsset(BATTLE_CUSTOMIZATION_ASSET_KEYS.background);
        expect(removedRecord).toBeNull();
    });

    test('persists preferences independently from asset records', async () => {
        const storage = createBattleCustomizationStorage({ indexedDB: new FakeIndexedDbFactory() });

        await storage.setPreference(BATTLE_CUSTOMIZATION_PREFERENCE_KEYS.bgmEnabled, false);
        const enabled = await storage.getPreference(BATTLE_CUSTOMIZATION_PREFERENCE_KEYS.bgmEnabled, true);
        expect(enabled).toBe(false);

        await storage.removePreference(BATTLE_CUSTOMIZATION_PREFERENCE_KEYS.bgmEnabled);
        const restoredFallback = await storage.getPreference(BATTLE_CUSTOMIZATION_PREFERENCE_KEYS.bgmEnabled, true);
        expect(restoredFallback).toBe(true);
    });
});
