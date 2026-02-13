// src/userSession.js - simple user/guest session tracking

const USER_ID_KEY = "svb.user.id";
const USER_NAME_KEY = "svb.user.name";
const USER_AVATAR_KEY = "svb.user.avatar";
const USER_ROLE_KEY = "svb.user.role";
const USER_PERMISSION_LEVEL_KEY = "svb.user.permissionLevel";
const GUEST_ID_KEY = "svb.guest.id";
const STORAGE_TEST_KEY = "svb.storage.test";
export const USER_SESSION_CHANGED_EVENT = "svb:user-session-changed";
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

function normalizeRole(role) {
    const normalized = String(role || "").trim().toLowerCase();
    if (normalized === "admin") return "admin";
    if (normalized === "guest") return "guest";
    return "player";
}

function resolvePermissionLevel(role) {
    const normalizedRole = normalizeRole(role);
    if (normalizedRole === "admin") return 100;
    if (normalizedRole === "player") return 10;
    return 0;
}

function parsePermissionLevel(value) {
    const parsed = Number.parseInt(String(value ?? ""), 10);
    if (!Number.isFinite(parsed)) return null;
    return Math.max(0, Math.trunc(parsed));
}

function emitSessionChanged() {
    if (typeof document === "undefined") return;
    document.dispatchEvent(new CustomEvent(USER_SESSION_CHANGED_EVENT, { detail: getUserSession() }));
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
        const role = normalizeRole(storage?.getItem(USER_ROLE_KEY));
        const storedLevel = parsePermissionLevel(storage?.getItem(USER_PERMISSION_LEVEL_KEY));
        return {
            mode: "user",
            userId,
            guestId: null,
            displayName: storage?.getItem(USER_NAME_KEY) || "用户",
            avatar: storedAvatar || "U",
            role,
            permissionLevel: storedLevel ?? resolvePermissionLevel(role)
        };
    }

    return {
        mode: "guest",
        userId: null,
        guestId: getGuestId(),
        displayName: "游客",
        avatar: "G",
        role: "guest",
        permissionLevel: 0
    };
}

export function setUserSession({ userId, username, avatar, role, permissionLevel }) {
    if (!isValidGuid(userId)) return false;
    const normalizedRole = normalizeRole(role);
    const resolvedPermissionLevel = Number.isFinite(permissionLevel)
        ? Math.max(0, Math.trunc(permissionLevel))
        : resolvePermissionLevel(normalizedRole);

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
    storage?.setItem(USER_ROLE_KEY, normalizedRole);
    storage?.setItem(USER_PERMISSION_LEVEL_KEY, String(resolvedPermissionLevel));
    emitSessionChanged();
    return true;
}

export function clearUserSession() {
    storage?.removeItem(USER_ID_KEY);
    storage?.removeItem(USER_NAME_KEY);
    storage?.removeItem(USER_AVATAR_KEY);
    storage?.removeItem(USER_ROLE_KEY);
    storage?.removeItem(USER_PERMISSION_LEVEL_KEY);
    emitSessionChanged();
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
