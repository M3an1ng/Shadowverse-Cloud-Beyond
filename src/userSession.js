// src/userSession.js - simple user/guest session tracking

const USER_ID_KEY = "svb.user.id";
const USER_NAME_KEY = "svb.user.name";
const USER_AVATAR_KEY = "svb.user.avatar";
const GUEST_ID_KEY = "svb.guest.id";

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
    const raw = localStorage.getItem(USER_ID_KEY);
    return isValidGuid(raw) ? raw : null;
}

function getGuestId() {
    const existing = localStorage.getItem(GUEST_ID_KEY);
    if (isValidGuid(existing)) return existing;
    const created = generateGuid();
    localStorage.setItem(GUEST_ID_KEY, created);
    return created;
}

export function getUserSession() {
    const userId = getStoredUserId();
    if (userId) {
        return {
            mode: "user",
            userId,
            guestId: null,
            displayName: localStorage.getItem(USER_NAME_KEY) || "用户",
            avatar: localStorage.getItem(USER_AVATAR_KEY) || "U"
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
