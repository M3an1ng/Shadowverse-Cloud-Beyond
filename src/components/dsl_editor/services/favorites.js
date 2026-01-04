export async function fetchFavoritesApi({ userId, query = "", expectedType = null, limit = 12, page = 1 }) {
    if (!userId) {
        return { items: [], total: 0 };
    }

    const params = new URLSearchParams();
    params.set("userId", userId);
    params.set("page", String(page));
    params.set("limit", String(limit));
    if (expectedType) {
        params.set("expectedType", expectedType);
    }
    if (query) {
        params.set("query", query);
    }

    const res = await fetch(`/api/dsl-favorites?${params.toString()}`);
    if (!res.ok) {
        throw new Error(`收藏加载失败 (${res.status})`);
    }
    const data = await res.json();
    const items = data.items || data.Items || [];
    const total = data.total ?? data.Total ?? items.length;
    return { items, total };
}

export async function markFavoriteUsedApi(userId, favoriteId) {
    if (!userId || !favoriteId) return { success: false };
    const res = await fetch(`/api/dsl-favorites/${favoriteId}/use?userId=${encodeURIComponent(userId)}`, {
        method: "POST"
    });
    if (!res.ok) {
        return { success: false, status: res.status };
    }
    return { success: true };
}

export async function createFavoriteApi(payload) {
    const res = await fetch("/api/dsl-favorites", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
    });
    if (!res.ok) {
        const msg = await res.text();
        throw new Error(msg || `收藏失败 (${res.status})`);
    }
    return res.json();
}
