// src/userSession.js - simple user/guest session tracking
import { getStoredValue, removeStoredValue, setStoredValue } from './storageFallback.js';

const USER_ID_KEY = "svb.user.id";
const USER_NAME_KEY = "svb.user.name";
const USER_AVATAR_KEY = "svb.user.avatar";
const USER_ROLE_KEY = "svb.user.role";
const USER_PERMISSION_LEVEL_KEY = "svb.user.permissionLevel";
const GUEST_ID_KEY = "svb.guest.id";
export const USER_SESSION_CHANGED_EVENT = "svb:user-session-changed";
let cachedGuestId = null;

function isValidGuid(value) {
    if (!value || typeof value !== "string") return false;
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

export function generateGuid() {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
        return crypto.randomUUID();
    }
    // getRandomValues is also available on HTTP origins, unlike randomUUID.
    if (typeof crypto !== "undefined" && typeof crypto.getRandomValues === "function") {
        const bytes = crypto.getRandomValues(new Uint8Array(16));
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;
        const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
        return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
    }
    return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        const v = c === "x" ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

function getStoredUserId() {
    const raw = getStoredValue(USER_ID_KEY);
    return isValidGuid(raw) ? raw : null;
}

function normalizeRole(role) {
    const normalized = String(role || "").trim().toLowerCase();
    if (normalized === "admin") return "admin";
    if (normalized === "editor") return "editor";
    if (normalized === "guest") return "guest";
    return "player";
}

function resolvePermissionLevel(role) {
    const normalizedRole = normalizeRole(role);
    if (normalizedRole === "admin") return 100;
    if (normalizedRole === "editor") return 50;
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
    const existing = getStoredValue(GUEST_ID_KEY);
    if (isValidGuid(existing)) {
        cachedGuestId = existing;
        return existing;
    }
    const created = generateGuid();
    cachedGuestId = created;
    setStoredValue(GUEST_ID_KEY, created);
    return created;
}

export function getUserSession() {
    const userId = getStoredUserId();
    if (userId) {
        const storedAvatar = getStoredValue(USER_AVATAR_KEY) || "";
        const role = normalizeRole(getStoredValue(USER_ROLE_KEY));
        const storedLevel = parsePermissionLevel(getStoredValue(USER_PERMISSION_LEVEL_KEY));
        return {
            mode: "user",
            userId,
            guestId: null,
            displayName: getStoredValue(USER_NAME_KEY) || "用户",
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

    setStoredValue(USER_ID_KEY, userId);
    if (username) {
        setStoredValue(USER_NAME_KEY, username);
    } else {
        removeStoredValue(USER_NAME_KEY);
    }
    if (avatar) {
        setStoredValue(USER_AVATAR_KEY, avatar);
    } else {
        const fallbackAvatar = username ? username.trim().charAt(0).toUpperCase() : "U";
        setStoredValue(USER_AVATAR_KEY, fallbackAvatar);
    }
    setStoredValue(USER_ROLE_KEY, normalizedRole);
    setStoredValue(USER_PERMISSION_LEVEL_KEY, String(resolvedPermissionLevel));
    emitSessionChanged();
    return true;
}

export function clearUserSession() {
    removeStoredValue(USER_ID_KEY);
    removeStoredValue(USER_NAME_KEY);
    removeStoredValue(USER_AVATAR_KEY);
    removeStoredValue(USER_ROLE_KEY);
    removeStoredValue(USER_PERMISSION_LEVEL_KEY);
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
