import { getDeckOwnerQuery } from "../userSession.js";
import { createDeckPreviewModal } from "./deckPreviewModal.js";
import { loadCardDefinitions } from "../cardDefinitionsLoader.js";
import { getDeckEnvironmentLabel, getDeckRuleLabel } from "../deckPresentation.js";
import { CARD_CATALOG_INVALIDATED_EVENT } from "../dataChangeEvents.js";

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function normalizeDeckCount(value) {
    const parsed = Number.parseInt(String(value ?? "0"), 10);
    return Number.isFinite(parsed) ? Math.max(0, parsed) : 0;
}

function buildDeckCardHtml(deck, selectedDeckId) {
    const id = String(deck?.id ?? "");
    const name = escapeHtml(deck?.name || "未命名卡组");
    const deckClass = escapeHtml(deck?.classLabel || "未知职业");
    const environmentLabel = escapeHtml(deck?.environmentLabel || getDeckEnvironmentLabel(deck?.environment));
    const ruleLabel = escapeHtml(deck?.ruleLabel || getDeckRuleLabel(deck?.rule));
    const count = normalizeDeckCount(deck?.cardCount);
    const isComplete = deck?.isBattleReady === true;
    const isSelected = id === String(selectedDeckId ?? "");

    return `
        <div class="deck-card wide-banner deck-picker-card${isSelected ? " selected" : ""}" data-deck-id="${escapeHtml(id)}">
            <div class="deck-art-bg"></div>
            <div class="deck-content">
                <div class="deck-info">
                    <div class="deck-class-name">${deckClass}</div>
                    <div class="deck-env-name">${environmentLabel} | ${ruleLabel}</div>
                    <div class="deck-name">${name}</div>
                    <div class="deck-status">
                        <span class="status-indicator ${isComplete ? "valid" : "invalid"}">${isComplete ? "Complete" : "Incomplete"}</span>
                        <span class="deck-count">${count} / 40</span>
                    </div>
                </div>
                <div class="deck-emblem">
                    <div class="emblem-icon"></div>
                </div>
            </div>
            <div class="deck-actions-overlay deck-picker-actions-overlay">
                <button class="deck-action-btn preview-deck-btn" type="button" data-picker-action="preview" data-deck-id="${escapeHtml(id)}">预览</button>
                <button class="deck-action-btn select-deck-btn" type="button" data-picker-action="select" data-deck-id="${escapeHtml(id)}">选择</button>
            </div>
        </div>
    `;
}

async function apiGetJson(url) {
    const response = await fetch(url, { method: "GET" });
    let payload = null;
    try {
        payload = await response.json();
    } catch (err) {
        // ignore parse errors; fall back to status text
    }
    if (!response.ok) {
        const message = payload?.message || payload?.Message || response.statusText;
        throw new Error(message || `HTTP ${response.status}`);
    }
    return payload;
}

function toSafeInt(value, fallback = 0) {
    const parsed = Number.parseInt(String(value ?? ""), 10);
    return Number.isFinite(parsed) ? Math.max(0, parsed) : fallback;
}

function getDeckStats(deck) {
    return {
        totalGames: toSafeInt(deck?.totalGames ?? deck?.TotalGames, 0),
        totalWins: toSafeInt(deck?.totalWins ?? deck?.TotalWins, 0),
        goingFirstGames: toSafeInt(deck?.goingFirstGames ?? deck?.GoingFirstGames, 0),
        goingFirstWins: toSafeInt(deck?.goingFirstWins ?? deck?.GoingFirstWins, 0),
        goingSecondGames: toSafeInt(deck?.goingSecondGames ?? deck?.GoingSecondGames, 0),
        goingSecondWins: toSafeInt(deck?.goingSecondWins ?? deck?.GoingSecondWins, 0)
    };
}

let cardDefinitionsCache = null;
let cardDefinitionsPromise = null;

document.addEventListener(CARD_CATALOG_INVALIDATED_EVENT, () => {
    cardDefinitionsCache = null;
    cardDefinitionsPromise = null;
});

async function ensureCardDefinitionMap() {
    if (cardDefinitionsCache instanceof Map) {
        return cardDefinitionsCache;
    }

    if (!cardDefinitionsPromise) {
        cardDefinitionsPromise = loadCardDefinitions({
            useCache: true,
            force: false
        }).then((cards) => {
            const map = new Map();
            (Array.isArray(cards) ? cards : []).forEach((card) => {
                const id = Number(card?.id ?? card?.Id ?? 0);
                if (Number.isFinite(id) && id > 0) {
                    map.set(id, card);
                }
            });
            cardDefinitionsCache = map;
            return map;
        }).finally(() => {
            cardDefinitionsPromise = null;
        });
    }

    return await cardDefinitionsPromise;
}

function normalizeDeckEntry(entry) {
    const cardDefinitionId = Number(entry?.cardDefinitionId ?? entry?.CardDefinitionId ?? 0);
    const count = Number(entry?.count ?? entry?.Count ?? 0);
    return {
        cardDefinitionId: Number.isFinite(cardDefinitionId) ? cardDefinitionId : 0,
        count: Number.isFinite(count) ? Math.max(0, count) : 0
    };
}

function buildPreviewCardFromDefinition(entry, definition) {
    const cardDefinitionId = entry.cardDefinitionId;
    return {
        cardDefinitionId,
        count: entry.count,
        cost: Number(definition?.cost ?? definition?.Cost ?? 99),
        name: String(definition?.name ?? definition?.Name ?? `未知卡牌 #${cardDefinitionId}`),
        type: String(definition?.type ?? definition?.Type ?? "unknown"),
        rarity: String(definition?.rarity ?? definition?.Rarity ?? ""),
        artPreview: String(definition?.artPreview ?? definition?.ArtPreview ?? ""),
        card: definition ?? null
    };
}

async function buildPreviewCards(deckDetail) {
    const rawEntries = Array.isArray(deckDetail?.cards)
        ? deckDetail.cards
        : (Array.isArray(deckDetail?.Cards) ? deckDetail.Cards : []);
    const entries = rawEntries.map(normalizeDeckEntry).filter((entry) => entry.cardDefinitionId > 0 && entry.count > 0);
    if (entries.length === 0) {
        return [];
    }

    let definitionMap = null;
    try {
        definitionMap = await ensureCardDefinitionMap();
    } catch {
        definitionMap = null;
    }

    return entries.map((entry) => {
        const definition = definitionMap?.get(entry.cardDefinitionId) ?? null;
        return buildPreviewCardFromDefinition(entry, definition);
    });
}

export function createDeckSelectionPickerModal() {
    const root = document.createElement("div");
    root.className = "deck-picker-modal";
    root.setAttribute("aria-hidden", "true");
    root.innerHTML = `
        <div class="deck-selection-view deck-picker-selection-view" role="dialog" aria-modal="true" aria-labelledby="deck-picker-modal-title">
            <div class="view-header">
                <div class="header-left">
                    <h2 id="deck-picker-modal-title">选择卡组</h2>
                </div>
                <div class="header-right">
                    <button class="mp-btn ghost deck-picker-close-btn" type="button">关闭</button>
                </div>
            </div>
            <div class="deck-grid deck-picker-grid" id="deck-picker-grid"></div>
        </div>
        <div class="deck-preview-modal" id="deck-picker-preview-modal" aria-hidden="true">
            <div class="deck-preview-dialog" role="dialog" aria-modal="true" aria-labelledby="deck-picker-preview-title">
                <div class="deck-preview-toolbar">
                    <div class="deck-preview-toolbar-left">
                        <div class="deck-preview-title-group">
                            <div class="deck-preview-caption">卡组预览</div>
                            <h3 class="deck-preview-title" id="deck-picker-preview-title">-</h3>
                            <div class="deck-preview-meta" id="deck-picker-preview-meta">-</div>
                        </div>
                    </div>
                    <div class="deck-preview-toolbar-stats" id="deck-picker-preview-toolbar-stats"></div>
                    <div class="deck-preview-toolbar-actions">
                        <button class="btn-secondary" id="deck-picker-preview-close-btn" type="button" aria-label="关闭预览">关闭</button>
                    </div>
                </div>
                <div class="deck-preview-summary">
                    <div class="deck-preview-curve" id="deck-picker-preview-curve"></div>
                    <div class="deck-preview-counts" id="deck-picker-preview-counts"></div>
                </div>
                <div class="deck-preview-cards" id="deck-picker-preview-cards"></div>
            </div>
        </div>
    `;
    document.body.appendChild(root);

    const titleEl = root.querySelector("#deck-picker-modal-title");
    const gridEl = root.querySelector("#deck-picker-grid");
    const closeBtn = root.querySelector(".deck-picker-close-btn");
    const deckMap = new Map();

    let resolver = null;
    let previewOpening = false;

    const deckPreview = createDeckPreviewModal({
        idPrefix: "deck-picker-preview"
    });

    const fetchDeckDetail = async (deckId) => {
        const query = getDeckOwnerQuery();
        return await apiGetJson(`/api/decks/${encodeURIComponent(deckId)}?${query}`);
    };

    const openDeckPreview = async (deckId) => {
        if (!deckPreview.isAvailable() || !deckId || previewOpening) return;
        const deck = deckMap.get(String(deckId));
        if (!deck) return;

        const previewBtn = gridEl.querySelector(`[data-picker-action="preview"][data-deck-id="${deckId}"]`);
        previewOpening = true;
        if (previewBtn instanceof HTMLButtonElement) {
            previewBtn.disabled = true;
            previewBtn.textContent = "加载中...";
        }

        try {
            const detail = await fetchDeckDetail(deckId);
            const cards = await buildPreviewCards(detail);
            deckPreview.open({
                titleText: deck.name || "未命名卡组",
                metaText: `${deck.classLabel || "未知职业"} | ${deck.ruleLabel || getDeckRuleLabel(deck?.rule)} | ${deck.environmentLabel || getDeckEnvironmentLabel(deck?.environment)}`,
                cards,
                stats: getDeckStats(deck),
                exportEnabled: false,
                exportVisible: false
            });
        } finally {
            previewOpening = false;
            if (previewBtn instanceof HTMLButtonElement) {
                previewBtn.disabled = false;
                previewBtn.textContent = "预览";
            }
        }
    };

    const close = (selectedDeckId = null) => {
        if (!root.classList.contains("open")) return;
        deckPreview.close();
        root.classList.remove("open");
        root.setAttribute("aria-hidden", "true");
        if (resolver) {
            const resolve = resolver;
            resolver = null;
            resolve(selectedDeckId);
        }
    };

    const open = ({ title = "选择卡组", decks = [], selectedDeckId = null } = {}) => {
        if (resolver) {
            const pendingResolve = resolver;
            resolver = null;
            pendingResolve(null);
        }

        titleEl.textContent = title;
        deckMap.clear();

        if (!Array.isArray(decks) || decks.length === 0) {
            gridEl.innerHTML = '<div class="deck-picker-empty">没有可用卡组，请先在牌组编辑中创建。</div>';
        } else {
            decks.forEach(deck => {
                const id = String(deck?.id ?? "");
                if (id) {
                    deckMap.set(id, deck);
                }
            });
            gridEl.innerHTML = decks.map(deck => buildDeckCardHtml(deck, selectedDeckId)).join("");
        }

        root.classList.add("open");
        root.setAttribute("aria-hidden", "false");
        closeBtn?.focus();

        return new Promise((resolve) => {
            resolver = resolve;
        });
    };

    root.addEventListener("click", (event) => {
        if (event.target === root) {
            close(null);
            return;
        }
        const actionButton = event.target instanceof Element
            ? event.target.closest("button[data-picker-action][data-deck-id]")
            : null;
        if (!(actionButton instanceof HTMLButtonElement)) return;
        const action = String(actionButton.getAttribute("data-picker-action") || "");
        const deckId = String(actionButton.getAttribute("data-deck-id") || "");
        if (!deckId) return;

        if (action === "preview") {
            openDeckPreview(deckId).catch((err) => {
                alert(`预览失败：${err.message}`);
            });
            return;
        }

        if (action === "select") {
            close(deckId);
        }
    });

    closeBtn?.addEventListener("click", () => close(null));

    document.addEventListener("keydown", (event) => {
        if (!root.classList.contains("open")) return;
        if (event.key !== "Escape") return;
        event.preventDefault();
        if (deckPreview.isOpen()) {
            deckPreview.close();
            return;
        }
        close(null);
    });

    return { open, close };
}
