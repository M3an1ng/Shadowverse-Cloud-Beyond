import { getStoredValue, removeStoredValue, setStoredValue } from "../storageFallback.js";

class MockStorage {
    constructor(options = {}) {
        this.values = new Map();
        this.throwOnGet = options.throwOnGet === true;
        this.throwOnSet = options.throwOnSet === true;
        this.throwOnRemove = options.throwOnRemove === true;
    }

    getItem(key) {
        if (this.throwOnGet) {
            throw new Error("get blocked");
        }
        return this.values.has(key) ? this.values.get(key) : null;
    }

    setItem(key, value) {
        if (this.throwOnSet) {
            throw new Error("set blocked");
        }
        this.values.set(key, String(value));
    }

    removeItem(key) {
        if (this.throwOnRemove) {
            throw new Error("remove blocked");
        }
        this.values.delete(key);
    }
}

function installStorages(localStorageMock, sessionStorageMock) {
    Object.defineProperty(window, "localStorage", {
        configurable: true,
        value: localStorageMock
    });
    Object.defineProperty(window, "sessionStorage", {
        configurable: true,
        value: sessionStorageMock
    });
}

describe("storageFallback", () => {
    let originalLocalStorage;
    let originalSessionStorage;

    beforeEach(() => {
        originalLocalStorage = window.localStorage;
        originalSessionStorage = window.sessionStorage;
    });

    afterEach(() => {
        installStorages(originalLocalStorage, originalSessionStorage);
    });

    test("keeps reading localStorage even when local writes are blocked", () => {
        const local = new MockStorage({ throwOnSet: true, throwOnRemove: true });
        local.values.set("svb.user.id", "u-1");
        const session = new MockStorage();
        installStorages(local, session);

        expect(getStoredValue("svb.user.id")).toBe("u-1");
    });

    test("falls back to writable sessionStorage when localStorage write is blocked", () => {
        const local = new MockStorage({ throwOnSet: true, throwOnRemove: true });
        const session = new MockStorage();
        installStorages(local, session);

        expect(setStoredValue("svb.lobby.handshake", "{\"x\":1}")).toBe(true);
        expect(session.getItem("svb.lobby.handshake")).toBe("{\"x\":1}");
    });

    test("removeStoredValue succeeds when at least one backend can remove", () => {
        const local = new MockStorage({ throwOnRemove: true });
        const session = new MockStorage();
        session.setItem("svb.key", "value");
        installStorages(local, session);

        expect(removeStoredValue("svb.key")).toBe(true);
        expect(session.getItem("svb.key")).toBeNull();
    });
});
