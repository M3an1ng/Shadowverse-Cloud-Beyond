// src/userSession.js - simple user/guest session tracking

const USER_ID_KEY = "svb.user.id";
const USER_NAME_KEY = "svb.user.name";
const USER_AVATAR_KEY = "svb.user.avatar";
const GUEST_ID_KEY = "svb.guest.id";
const STORAGE_TEST_KEY = "svb.storage.test";
let cachedGuestId = null;

function resolveStorage() {
    if (typeof window === "undefined") return null;
    const candidates = [window.localStorage, window.sessionStorage];
    for (const candidate of candidates) {
        if (!candidate) continue;
        try {
            candidate.setItem(STORAGE_TEST_KEY, "1");
            candidate.removeItem(STORAGE_TEST_KEY);
            return candidate;
        } catch (err) {
            // Ignore and try the next storage.
        }
    }
    return null;
}

const storage = resolveStorage();

function isValidGuid(value) {
    if (!value || typeof value !== "string") return false;
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

function generateGuid() {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
        return crypto.randomUUID();
    }
    return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        const v = c === "x" ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

function getStoredUserId() {
    const raw = storage?.getItem(USER_ID_KEY);
    return isValidGuid(raw) ? raw : null;
}

function getGuestId() {
    if (isValidGuid(cachedGuestId)) return cachedGuestId;
    const existing = storage?.getItem(GUEST_ID_KEY);
    if (isValidGuid(existing)) {
        cachedGuestId = existing;
        return existing;
    }
    const created = generateGuid();
    cachedGuestId = created;
    storage?.setItem(GUEST_ID_KEY, created);
    return created;
}

export function getUserSession() {
    const userId = getStoredUserId();
    if (userId) {
        const storedAvatar = storage?.getItem(USER_AVATAR_KEY) || "";
        return {
            mode: "user",
            userId,
            guestId: null,
            displayName: storage?.getItem(USER_NAME_KEY) || "用户",
            avatar: storedAvatar || "U"
        };
    }

    return {
        mode: "guest",
        userId: null,
        guestId: getGuestId(),
        displayName: "游客",
        avatar: "G"
    };
}

export function setUserSession({ userId, username, avatar }) {
    if (!isValidGuid(userId)) return false;
    storage?.setItem(USER_ID_KEY, userId);
    if (username) {
        storage?.setItem(USER_NAME_KEY, username);
    } else {
        storage?.removeItem(USER_NAME_KEY);
    }
    if (avatar) {
        storage?.setItem(USER_AVATAR_KEY, avatar);
    } else {
        const fallbackAvatar = username ? username.trim().charAt(0).toUpperCase() : "U";
        storage?.setItem(USER_AVATAR_KEY, fallbackAvatar);
    }
    return true;
}

export function clearUserSession() {
    storage?.removeItem(USER_ID_KEY);
    storage?.removeItem(USER_NAME_KEY);
    storage?.removeItem(USER_AVATAR_KEY);
}

export function getDeckOwnerQuery() {
    const session = getUserSession();
    if (session.userId) {
        return `userId=${encodeURIComponent(session.userId)}`;
    }
    return `guestId=${encodeURIComponent(session.guestId || "")}`;
}

export function getDeckOwnerPayload() {
    const session = getUserSession();
    if (session.userId) {
        return { userId: session.userId };
    }
    return { guestId: session.guestId };
}
