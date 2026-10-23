const STATUS_LABELS = {
    Waiting: "等待中",
    Ready: "已准备",
    Started: "对战中",
    Closed: "已关闭"
};

const STATUS_ORDER = {
    Started: 0,
    Ready: 1,
    Waiting: 2,
    Closed: 3
};

function readValue(source, camelKey, pascalKey) {
    return source?.[camelKey] ?? source?.[pascalKey];
}

function normalizeString(value) {
    return String(value ?? "").trim();
}

function normalizeStatus(value) {
    const text = normalizeString(value);
    if (!text) return "Unknown";
    const match = Object.keys(STATUS_LABELS).find(status => status.toLowerCase() === text.toLowerCase());
    return match || text;
}

function normalizeRound(value) {
    const number = Number(value);
    return Number.isFinite(number) && number > 0 ? number : null;
}

function normalizeDateMs(value) {
    const time = Date.parse(value);
    return Number.isFinite(time) ? time : 0;
}

export function normalizeMatchRoomList(payload) {
    const items = Array.isArray(payload)
        ? payload
        : Array.isArray(payload?.items)
            ? payload.items
            : Array.isArray(payload?.Items)
                ? payload.Items
                : [];

    return items
        .map(item => {
            const roomId = normalizeString(
                readValue(item, "roomId", "RoomId") || readValue(item, "gameId", "GameId")
            );
            const activeGameId = normalizeString(readValue(item, "activeGameId", "ActiveGameId"));
            const status = normalizeStatus(readValue(item, "status", "Status"));
            const canSpectate = status === "Started" && !!roomId && !!activeGameId;
            const hasGuest = Boolean(readValue(item, "hasGuest", "HasGuest"));
            const round = normalizeRound(readValue(item, "round", "Round"));
            const createdAt = normalizeString(readValue(item, "createdAt", "CreatedAt"));

            return {
                roomId,
                activeGameId,
                status,
                statusLabel: STATUS_LABELS[status] || status || "未知",
                canSpectate,
                hasGuest,
                round,
                createdAt,
                requiredEnvironment: readValue(item, "requiredEnvironment", "RequiredEnvironment") || null,
                requiredRule: readValue(item, "requiredRule", "RequiredRule") || null
            };
        })
        .filter(item => item.roomId)
        .sort((a, b) => {
            if (a.canSpectate !== b.canSpectate) {
                return a.canSpectate ? -1 : 1;
            }

            const statusDelta = (STATUS_ORDER[a.status] ?? 99) - (STATUS_ORDER[b.status] ?? 99);
            if (statusDelta !== 0) {
                return statusDelta;
            }

            return normalizeDateMs(b.createdAt) - normalizeDateMs(a.createdAt);
        });
}
