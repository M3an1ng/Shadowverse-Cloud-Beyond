import { getUserSession, USER_SESSION_CHANGED_EVENT } from "./userSession.js";
import { createDeckPreviewModal } from "./components/deckPreviewModal.js";

const SUMMARY_ENDPOINT = "/api/devtools/winrate-stats/summary";
const DECK_COMPOSITION_ENDPOINT = "/api/devtools/winrate-stats/deck-composition";
const DEFAULT_ENVIRONMENT = "open";
const DEFAULT_TOP = 50;
const MAX_TOP = 2000;
const CLASS_LABELS = {
    forestcraft: "妖精",
    swordcraft: "皇家护卫",
    runecraft: "巫师",
    dragoncraft: "龙族",
    abysscraft: "梦魇",
    havencraft: "主教",
    portalcraft: "复仇者",
    neutral: "中立"
};
const ENVIRONMENT_LABELS = {
    open: "开放环境",
    rotation: "指定系列"
};
const DEFAULT_SORT = {
    card: { key: "deckWinRate", direction: "desc", type: "number" },
    deck: { key: "winRate", direction: "desc", type: "number" },
    class: { key: "winRate", direction: "desc", type: "number" }
};

function pickValue(source, camelKey, pascalKey) {
    if (!source || typeof source !== "object") return null;
    if (source[camelKey] != null) return source[camelKey];
    if (source[pascalKey] != null) return source[pascalKey];
    return null;
}

function toInt(value, fallback = 0) {
    const parsed = Number.parseInt(String(value ?? ""), 10);
    return Number.isFinite(parsed) ? parsed : fallback;
}

function toNumber(value, fallback = 0) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
}

function toNullableNumber(value) {
    if (value == null || value === "") return null;
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
}

function normalizeEnumValue(value) {
    return String(value ?? "").trim().toLowerCase();
}

function parseLocalTemporalFilter(value) {
    const text = String(value ?? "").trim();
    if (!text) {
        return { ok: true, text: "", timestamp: null };
    }

    if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2})?$/.test(text)) {
        return { ok: false, text, timestamp: null };
    }

    const date = new Date(text);
    if (Number.isNaN(date.getTime())) {
        return { ok: false, text, timestamp: null };
    }

    return { ok: true, text, timestamp: date.getTime() };
}

function toUtcIsoStringFromLocalTemporal(value) {
    const parsed = parseLocalTemporalFilter(value);
    if (!parsed.ok || parsed.timestamp == null) return "";
    return new Date(parsed.timestamp).toISOString();
}

function tryOpenNativeDatePicker(input, event = null) {
    if (!input || input.disabled || input.readOnly || typeof input.showPicker !== "function") return false;
    try {
        if (event && event.type === "pointerdown" && typeof event.preventDefault === "function") {
            event.preventDefault();
        }
        if (typeof input.focus === "function") {
            try {
                input.focus({ preventScroll: true });
            } catch {
                input.focus();
            }
        }
        input.showPicker();
        return true;
    } catch {
        return false;
    }
}

function getClassLabel(value) {
    const normalized = normalizeEnumValue(value);
    if (normalized && CLASS_LABELS[normalized]) {
        return CLASS_LABELS[normalized];
    }
    return String(value ?? "").trim() || "未知职业";
}

function getEnvironmentLabel(value) {
    const normalized = normalizeEnumValue(value);
    if (normalized && ENVIRONMENT_LABELS[normalized]) {
        return ENVIRONMENT_LABELS[normalized];
    }
    return String(value ?? "").trim() || "未知环境";
}

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function normalizeUtcTimestampInput(value) {
    const raw = String(value ?? "").trim();
    if (!raw) return "";

    // If backend returns no timezone suffix, treat it as UTC.
    const hasTimezone = /([zZ]|[+\-]\d{2}:\d{2})$/.test(raw);
    return hasTimezone ? raw : `${raw}Z`;
}

function formatTimestamp(value) {
    if (!value) return "-";
    const date = new Date(normalizeUtcTimestampInput(value));
    if (Number.isNaN(date.getTime())) return String(value);
    const yyyy = date.getFullYear();
    const mm = String(date.getMonth() + 1).padStart(2, "0");
    const dd = String(date.getDate()).padStart(2, "0");
    const hh = String(date.getHours()).padStart(2, "0");
    const mi = String(date.getMinutes()).padStart(2, "0");
    const ss = String(date.getSeconds()).padStart(2, "0");
    return `${yyyy}-${mm}-${dd} ${hh}:${mi}:${ss}`;
}

function parseTimestamp(value) {
    const date = new Date(normalizeUtcTimestampInput(value));
    if (Number.isNaN(date.getTime())) return 0;
    return date.getTime();
}

function normalizeRate(rawRate, wins, games) {
    const totalGames = toInt(games, 0);
    if (totalGames <= 0) return 0;

    const rate = toNumber(rawRate, NaN);
    if (Number.isFinite(rate)) {
        return Math.round(rate * 100) / 100;
    }

    return Math.round((toInt(wins, 0) / totalGames) * 10000) / 100;
}

function formatRate(rate, games) {
    if (toInt(games, 0) <= 0) return "--";
    return `${toNumber(rate, 0).toFixed(2)}%`;
}

function formatAverageTurn(value) {
    const number = toNullableNumber(value);
    if (number == null || number <= 0) return "--";
    return number.toFixed(2);
}

function compareValues(a, b, type) {
    if (type === "number") {
        const left = toNumber(a, 0);
        const right = toNumber(b, 0);
        return left - right;
    }

    if (type === "date") {
        const left = parseTimestamp(a);
        const right = parseTimestamp(b);
        return left - right;
    }

    const left = String(a ?? "");
    const right = String(b ?? "");
    return left.localeCompare(right, undefined, { sensitivity: "base", numeric: true });
}

class DevToolsWinrateStats {
    constructor() {
        this.initialized = false;
        this.handlers = [];
        this.isModalOpen = false;
        this.activeView = "card";
        this.sortState = {
            card: { ...DEFAULT_SORT.card },
            deck: { ...DEFAULT_SORT.deck },
            class: { ...DEFAULT_SORT.class }
        };

        this.cardStats = [];
        this.deckStats = [];
        this.classStats = [];
        this.deckPreview = null;
    }

    async init() {
        if (this.initialized) return;
        this.initialized = true;

        this.root = document.getElementById("devtools-winrate-panel");
        this.modalEl = document.getElementById("devtools-winrate-modal");
        this.openBtn = document.getElementById("devtools-winrate-open-btn");
        this.closeBtn = document.getElementById("devtools-winrate-close-btn");
        this.refreshBtn = document.getElementById("devtools-winrate-refresh");
        this.environmentInput = document.getElementById("devtools-winrate-environment");
        this.startDateInput = document.getElementById("devtools-winrate-start-date");
        this.endDateInput = document.getElementById("devtools-winrate-end-date");
        this.cardNameInput = document.getElementById("devtools-winrate-card-name");
        this.topInput = document.getElementById("devtools-winrate-top");
        this.messageEl = document.getElementById("devtools-winrate-message");
        this.cardCountEl = document.getElementById("devtools-winrate-card-count");
        this.deckCountEl = document.getElementById("devtools-winrate-deck-count");
        this.classCountEl = document.getElementById("devtools-winrate-class-count");
        this.generatedAtEl = document.getElementById("devtools-winrate-generated-at");
        this.cardBody = document.getElementById("devtools-winrate-card-body");
        this.deckBody = document.getElementById("devtools-winrate-deck-body");
        this.classBody = document.getElementById("devtools-winrate-class-body");

        this.tabButtons = Array.from(this.modalEl?.querySelectorAll(".devtools-winrate-tab[data-view]") ?? []);
        this.views = Array.from(this.modalEl?.querySelectorAll(".devtools-winrate-view[data-view]") ?? []);

        if (!this.root || !this.modalEl || !this.openBtn || !this.closeBtn || !this.messageEl || !this.cardBody || !this.deckBody || !this.classBody) {
            return;
        }

        this.deckPreview = createDeckPreviewModal({
            idPrefix: "devtools-winrate-deck-preview",
            onExport: () => this.setMessage("当前看板预览暂不支持导出卡组代码。", true)
        });

        this.bindUI();
        this.setActiveView("card");
        this.closeModal();
    }

    bindUI() {
        if (this.openBtn) {
            const handler = () => {
                void this.openModal();
            };
            this.openBtn.addEventListener("click", handler);
            this.handlers.push(() => this.openBtn.removeEventListener("click", handler));
        }

        if (this.closeBtn) {
            const handler = () => {
                this.closeModal();
            };
            this.closeBtn.addEventListener("click", handler);
            this.handlers.push(() => this.closeBtn.removeEventListener("click", handler));
        }

        if (this.modalEl) {
            const handler = (event) => {
                if (event.target === this.modalEl) {
                    this.closeModal();
                }
            };
            this.modalEl.addEventListener("click", handler);
            this.handlers.push(() => this.modalEl.removeEventListener("click", handler));
        }

        const escHandler = (event) => {
            if (event.key !== "Escape") return;
            if (this.deckPreview?.isOpen()) {
                this.closeDeckPreview();
                return;
            }
            if (this.isModalOpen) {
                this.closeModal();
            }
        };
        document.addEventListener("keydown", escHandler);
        this.handlers.push(() => document.removeEventListener("keydown", escHandler));

        if (this.refreshBtn) {
            const handler = () => {
                void this.refreshSummary(true);
            };
            this.refreshBtn.addEventListener("click", handler);
            this.handlers.push(() => this.refreshBtn.removeEventListener("click", handler));
        }

        if (this.environmentInput) {
            const handler = () => {
                void this.refreshSummary(true);
            };
            this.environmentInput.addEventListener("change", handler);
            this.handlers.push(() => this.environmentInput.removeEventListener("change", handler));
        }

        if (this.topInput) {
            const handler = (event) => {
                if (event.key !== "Enter") return;
                event.preventDefault();
                void this.refreshSummary(true);
            };
            this.topInput.addEventListener("keydown", handler);
            this.handlers.push(() => this.topInput.removeEventListener("keydown", handler));
        }

        if (this.cardNameInput) {
            const changeHandler = () => {
                void this.refreshSummary(true);
            };
            this.cardNameInput.addEventListener("change", changeHandler);
            this.handlers.push(() => this.cardNameInput.removeEventListener("change", changeHandler));

            const enterHandler = (event) => {
                if (event.key !== "Enter") return;
                event.preventDefault();
                void this.refreshSummary(true);
            };
            this.cardNameInput.addEventListener("keydown", enterHandler);
            this.handlers.push(() => this.cardNameInput.removeEventListener("keydown", enterHandler));
        }

        const bindDateInput = (input) => {
            if (!input) return;

            input.type = "datetime-local";
            input.removeAttribute("readonly");
            input.removeAttribute("disabled");

            const changeHandler = () => {
                void this.refreshSummary(true);
            };
            input.addEventListener("change", changeHandler);
            this.handlers.push(() => input.removeEventListener("change", changeHandler));

            const openPickerHandler = (event) => {
                if (event.type === "pointerdown" && typeof event.button === "number" && event.button !== 0) return;
                tryOpenNativeDatePicker(input, event);
            };
            input.addEventListener("pointerdown", openPickerHandler);
            input.addEventListener("click", openPickerHandler);
            this.handlers.push(() => input.removeEventListener("pointerdown", openPickerHandler));
            this.handlers.push(() => input.removeEventListener("click", openPickerHandler));

            const enterHandler = (event) => {
                if (event.key !== "Enter") return;
                event.preventDefault();
                void this.refreshSummary(true);
            };
            input.addEventListener("keydown", enterHandler);
            this.handlers.push(() => input.removeEventListener("keydown", enterHandler));
        };

        bindDateInput(this.startDateInput);
        bindDateInput(this.endDateInput);

        if (this.tabButtons.length > 0) {
            const handler = (event) => {
                const button = event.target instanceof Element ? event.target.closest("button[data-view]") : null;
                if (!button) return;
                const view = String(button.getAttribute("data-view") || "").trim();
                if (!view) return;
                this.setActiveView(view);
            };
            this.tabButtons.forEach((button) => {
                button.addEventListener("click", handler);
                this.handlers.push(() => button.removeEventListener("click", handler));
            });
        }

        if (this.modalEl) {
            const headerSortHandler = (event) => {
                const target = event.target instanceof Element ? event.target.closest("th[data-sort-key][data-view]") : null;
                if (!target) return;

                const view = String(target.getAttribute("data-view") || "").trim();
                const key = String(target.getAttribute("data-sort-key") || "").trim();
                const type = String(target.getAttribute("data-sort-type") || "number").trim();

                if (!view || !key) return;
                this.toggleSort(view, key, type);
            };
            this.modalEl.addEventListener("click", headerSortHandler);
            this.handlers.push(() => this.modalEl.removeEventListener("click", headerSortHandler));
        }

        if (this.deckBody) {
            const deckPreviewHandler = (event) => {
                const button = event.target instanceof Element
                    ? event.target.closest("button[data-action='view-deck'][data-deck-hash]")
                    : null;
                if (!button) return;

                const deckShapeHash = String(button.getAttribute("data-deck-hash") || "").trim();
                const deckClass = String(button.getAttribute("data-deck-class") || "").trim();
                if (!deckShapeHash) return;
                void this.openDeckPreviewByHash(deckShapeHash, deckClass);
            };
            this.deckBody.addEventListener("click", deckPreviewHandler);
            this.handlers.push(() => this.deckBody.removeEventListener("click", deckPreviewHandler));
        }


        const sessionHandler = () => {
            if (this.isModalOpen) {
                void this.refreshSummary(true);
            }
        };
        document.addEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler);
        this.handlers.push(() => document.removeEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler));
    }

    setActiveView(view) {
        const targetView = ["card", "deck", "class"].includes(view) ? view : "card";
        this.activeView = targetView;

        this.tabButtons.forEach((button) => {
            const selected = button.getAttribute("data-view") === targetView;
            button.classList.toggle("is-active", selected);
            button.setAttribute("aria-selected", String(selected));
        });

        this.views.forEach((section) => {
            const selected = section.getAttribute("data-view") === targetView;
            section.classList.toggle("active", selected);
        });

        this.updateSortIndicators();
        this.renderActiveView();
    }

    toggleSort(view, key, type) {
        const current = this.sortState[view] || { key: "", direction: "desc", type: "number" };
        if (current.key === key) {
            current.direction = current.direction === "asc" ? "desc" : "asc";
        } else {
            current.key = key;
            current.direction = "desc";
            current.type = type || "number";
        }
        current.type = type || current.type || "number";
        this.sortState[view] = current;

        if (view !== this.activeView) {
            this.setActiveView(view);
            return;
        }

        this.updateSortIndicators();
        this.renderActiveView();
    }

    updateSortIndicators() {
        if (!this.modalEl) return;

        this.modalEl.querySelectorAll("th[data-sort-key][data-view]").forEach((th) => {
            th.classList.remove("sorted-asc", "sorted-desc");
            th.setAttribute("aria-sort", "none");

            const view = th.getAttribute("data-view");
            const key = th.getAttribute("data-sort-key");
            if (!view || !key) return;

            const state = this.sortState[view];
            if (!state || state.key !== key) return;

            const className = state.direction === "asc" ? "sorted-asc" : "sorted-desc";
            th.classList.add(className);
            th.setAttribute("aria-sort", state.direction === "asc" ? "ascending" : "descending");
        });
    }

    async openModal() {
        if (!this.modalEl) return;
        this.modalEl.classList.add("active");
        this.modalEl.setAttribute("aria-hidden", "false");
        this.isModalOpen = true;
        await this.refreshSummary(true);
    }

    closeModal() {
        if (!this.modalEl) return;
        this.closeDeckPreview();
        this.modalEl.classList.remove("active");
        this.modalEl.setAttribute("aria-hidden", "true");
        this.isModalOpen = false;
    }

    setBusy(busy) {
        if (this.refreshBtn) this.refreshBtn.disabled = busy;
    }

    setMessage(text, isError = false) {
        if (!this.messageEl) return;
        this.messageEl.textContent = text || "";
        this.messageEl.dataset.tone = isError ? "error" : "neutral";
    }

    sortItems(items, view) {
        const state = this.sortState[view] || DEFAULT_SORT[view];
        if (!state || !state.key) return [...items];

        const sorted = [...items].sort((a, b) => {
            const result = compareValues(a[state.key], b[state.key], state.type || "number");
            if (result !== 0) return result;
            return compareValues(a.updatedAtTs ?? 0, b.updatedAtTs ?? 0, "number");
        });

        if (state.direction === "desc") {
            sorted.reverse();
        }

        return sorted;
    }

    renderCardRows(items) {
        if (!this.cardBody) return;
        this.cardBody.innerHTML = "";

        if (!Array.isArray(items) || items.length === 0) {
            const row = document.createElement("tr");
            row.innerHTML = '<td colspan="11" class="devtools-winrate-empty">暂无卡牌胜率数据。</td>';
            this.cardBody.appendChild(row);
            return;
        }

        const html = items.map((item, index) => `
            <tr>
                <td>${index + 1}</td>
                <td>${escapeHtml(item.cardName)}</td>
                <td>${item.deckGames}</td>
                <td>${item.deckWins}</td>
                <td>${formatRate(item.deckWinRate, item.deckGames)}</td>
                <td>${item.drawnGames}</td>
                <td>${item.drawnWins}</td>
                <td>${formatRate(item.drawnWinRate, item.drawnGames)}</td>
                <td>${item.playedGames}</td>
                <td>${item.playedWins}</td>
                <td>${formatRate(item.playedWinRate, item.playedGames)}</td>
            </tr>
        `).join("");

        this.cardBody.innerHTML = html;
    }

    renderDeckRows(items) {
        if (!this.deckBody) return;
        this.deckBody.innerHTML = "";

        if (!Array.isArray(items) || items.length === 0) {
            const row = document.createElement("tr");
            row.innerHTML = '<td colspan="16" class="devtools-winrate-empty">暂无卡组胜率数据。</td>';
            this.deckBody.appendChild(row);
            return;
        }

        const html = items.map((item, index) => `
            <tr>
                <td>${index + 1}</td>
                <td>${escapeHtml(item.deckClass)}</td>
                <td>${item.totalGames}</td>
                <td>${item.totalWins}</td>
                <td>${formatRate(item.winRate, item.totalGames)}</td>
                <td>${item.goingFirstGames}</td>
                <td>${item.goingFirstWins}</td>
                <td>${formatRate(item.goingFirstWinRate, item.goingFirstGames)}</td>
                <td>${item.goingSecondGames}</td>
                <td>${item.goingSecondWins}</td>
                <td>${formatRate(item.goingSecondWinRate, item.goingSecondGames)}</td>
                <td>${formatAverageTurn(item.averageEndTurn)}</td>
                <td>${formatAverageTurn(item.goingFirstAverageEndTurn)}</td>
                <td>${formatAverageTurn(item.goingSecondAverageEndTurn)}</td>
                <td>${item.updatedAtText}</td>
                <td>
                    <button
                        class="tool-button devtools-winrate-view-deck-btn"
                        type="button"
                        data-action="view-deck"
                        data-deck-hash="${escapeHtml(item.deckShapeHash)}"
                        data-deck-class="${escapeHtml(item.deckClassRaw || item.deckClass)}"
                        title="查看卡组构筑">
                        查看卡组
                    </button>
                </td>
            </tr>
        `).join("");

        this.deckBody.innerHTML = html;
    }

    renderClassRows(items) {
        if (!this.classBody) return;
        this.classBody.innerHTML = "";

        if (!Array.isArray(items) || items.length === 0) {
            const row = document.createElement("tr");
            row.innerHTML = '<td colspan="14" class="devtools-winrate-empty">暂无职业胜率数据。</td>';
            this.classBody.appendChild(row);
            return;
        }

        const html = items.map((item) => `
            <tr>
                <td>${escapeHtml(item.deckClass)}</td>
                <td>${item.totalGames}</td>
                <td>${item.totalWins}</td>
                <td>${formatRate(item.winRate, item.totalGames)}</td>
                <td>${item.goingFirstGames}</td>
                <td>${item.goingFirstWins}</td>
                <td>${formatRate(item.goingFirstWinRate, item.goingFirstGames)}</td>
                <td>${item.goingSecondGames}</td>
                <td>${item.goingSecondWins}</td>
                <td>${formatRate(item.goingSecondWinRate, item.goingSecondGames)}</td>
                <td>${formatAverageTurn(item.averageEndTurn)}</td>
                <td>${formatAverageTurn(item.goingFirstAverageEndTurn)}</td>
                <td>${formatAverageTurn(item.goingSecondAverageEndTurn)}</td>
                <td>${item.updatedAtText}</td>
            </tr>
        `).join("");

        this.classBody.innerHTML = html;
    }

    renderActiveView() {
        if (this.activeView === "card") {
            this.renderCardRows(this.sortItems(this.cardStats, "card"));
            return;
        }

        if (this.activeView === "deck") {
            this.renderDeckRows(this.sortItems(this.deckStats, "deck"));
            return;
        }

        this.renderClassRows(this.sortItems(this.classStats, "class"));
    }

    normalizeCardStats(items) {
        return (Array.isArray(items) ? items : []).map((item) => {
            const cardDefinitionId = toInt(pickValue(item, "cardDefinitionId", "CardDefinitionId"), 0);
            const cardName = String(pickValue(item, "cardName", "CardName") || `卡牌 #${cardDefinitionId}`);
            const deckGames = toInt(pickValue(item, "deckGames", "DeckGames"), 0);
            const deckWins = toInt(pickValue(item, "deckWins", "DeckWins"), 0);
            const drawnGames = toInt(pickValue(item, "drawnGames", "DrawnGames"), 0);
            const drawnWins = toInt(pickValue(item, "drawnWins", "DrawnWins"), 0);
            const playedGames = toInt(pickValue(item, "playedGames", "PlayedGames"), 0);
            const playedWins = toInt(pickValue(item, "playedWins", "PlayedWins"), 0);

            return {
                cardDefinitionId,
                cardName,
                deckGames,
                deckWins,
                deckWinRate: normalizeRate(pickValue(item, "deckWinRate", "DeckWinRate"), deckWins, deckGames),
                drawnGames,
                drawnWins,
                drawnWinRate: normalizeRate(pickValue(item, "drawnWinRate", "DrawnWinRate"), drawnWins, drawnGames),
                playedGames,
                playedWins,
                playedWinRate: normalizeRate(pickValue(item, "playedWinRate", "PlayedWinRate"), playedWins, playedGames)
            };
        });
    }

    normalizeDeckStats(items) {
        return (Array.isArray(items) ? items : []).map((item) => {
            const totalGames = toInt(pickValue(item, "totalGames", "TotalGames"), 0);
            const totalWins = toInt(pickValue(item, "totalWins", "TotalWins"), 0);
            const goingFirstGames = toInt(pickValue(item, "goingFirstGames", "GoingFirstGames"), 0);
            const goingFirstWins = toInt(pickValue(item, "goingFirstWins", "GoingFirstWins"), 0);
            const goingSecondGames = toInt(pickValue(item, "goingSecondGames", "GoingSecondGames"), 0);
            const goingSecondWins = toInt(pickValue(item, "goingSecondWins", "GoingSecondWins"), 0);
            const updatedAt = pickValue(item, "updatedAtUtc", "UpdatedAtUtc");
            const averageEndTurn = toNullableNumber(pickValue(item, "averageEndTurn", "AverageEndTurn"));
            const goingFirstAverageEndTurn = toNullableNumber(pickValue(item, "goingFirstAverageEndTurn", "GoingFirstAverageEndTurn"));
            const goingSecondAverageEndTurn = toNullableNumber(pickValue(item, "goingSecondAverageEndTurn", "GoingSecondAverageEndTurn"));

            return {
                deckShapeHash: String(pickValue(item, "deckShapeHash", "DeckShapeHash") || "-"),
                deckClassRaw: String(pickValue(item, "deckClass", "DeckClass") || ""),
                deckClass: getClassLabel(pickValue(item, "deckClass", "DeckClass")),
                totalGames,
                totalWins,
                winRate: normalizeRate(pickValue(item, "winRate", "WinRate"), totalWins, totalGames),
                goingFirstGames,
                goingFirstWins,
                goingFirstWinRate: normalizeRate(pickValue(item, "goingFirstWinRate", "GoingFirstWinRate"), goingFirstWins, goingFirstGames),
                goingSecondGames,
                goingSecondWins,
                goingSecondWinRate: normalizeRate(pickValue(item, "goingSecondWinRate", "GoingSecondWinRate"), goingSecondWins, goingSecondGames),
                averageEndTurn,
                goingFirstAverageEndTurn,
                goingSecondAverageEndTurn,
                updatedAtTs: parseTimestamp(updatedAt),
                updatedAtText: formatTimestamp(updatedAt)
            };
        });
    }

    normalizeClassStats(items) {
        return (Array.isArray(items) ? items : []).map((item) => {
            const totalGames = toInt(pickValue(item, "totalGames", "TotalGames"), 0);
            const totalWins = toInt(pickValue(item, "totalWins", "TotalWins"), 0);
            const goingFirstGames = toInt(pickValue(item, "goingFirstGames", "GoingFirstGames"), 0);
            const goingFirstWins = toInt(pickValue(item, "goingFirstWins", "GoingFirstWins"), 0);
            const goingSecondGames = toInt(pickValue(item, "goingSecondGames", "GoingSecondGames"), 0);
            const goingSecondWins = toInt(pickValue(item, "goingSecondWins", "GoingSecondWins"), 0);
            const updatedAt = pickValue(item, "updatedAtUtc", "UpdatedAtUtc");
            const deckClassRaw = String(pickValue(item, "deckClass", "DeckClass") || "");
            return {
                deckClassRaw,
                deckClass: getClassLabel(deckClassRaw),
                totalGames,
                totalWins,
                winRate: normalizeRate(pickValue(item, "winRate", "WinRate"), totalWins, totalGames),
                goingFirstGames,
                goingFirstWins,
                goingFirstWinRate: normalizeRate(pickValue(item, "goingFirstWinRate", "GoingFirstWinRate"), goingFirstWins, goingFirstGames),
                goingSecondGames,
                goingSecondWins,
                goingSecondWinRate: normalizeRate(pickValue(item, "goingSecondWinRate", "GoingSecondWinRate"), goingSecondWins, goingSecondGames),
                averageEndTurn: toNullableNumber(pickValue(item, "averageEndTurn", "AverageEndTurn")),
                goingFirstAverageEndTurn: toNullableNumber(pickValue(item, "goingFirstAverageEndTurn", "GoingFirstAverageEndTurn")),
                goingSecondAverageEndTurn: toNullableNumber(pickValue(item, "goingSecondAverageEndTurn", "GoingSecondAverageEndTurn")),
                updatedAtTs: parseTimestamp(updatedAt),
                updatedAtText: formatTimestamp(updatedAt)
            };
        });
    }

    normalizeDeckCompositionCards(items) {
        return (Array.isArray(items) ? items : []).map((item) => {
            const cardDefinitionId = toInt(pickValue(item, "cardDefinitionId", "CardDefinitionId"), 0);
            const count = toInt(pickValue(item, "count", "Count"), 0);
            const cost = toInt(pickValue(item, "cost", "Cost"), 0);
            const name = String(pickValue(item, "name", "Name") || `卡牌 #${cardDefinitionId}`);
            const type = String(pickValue(item, "type", "Type") || "未知");
            const rarity = String(pickValue(item, "rarity", "Rarity") || "");
            const artPreview = String(pickValue(item, "artPreview", "ArtPreview") || "");
            return {
                cardDefinitionId,
                count,
                cost,
                name,
                type,
                rarity,
                artPreview
            };
        }).filter((item) => item.cardDefinitionId > 0 && item.count > 0);
    }

    findDeckStatsByHash(deckShapeHash) {
        const targetHash = String(deckShapeHash ?? "").trim().toUpperCase();
        if (!targetHash) return null;
        return this.deckStats.find((item) =>
            String(item.deckShapeHash ?? "").trim().toUpperCase() === targetHash) ?? null;
    }
    async fetchDeckCompositionByHash(deckShapeHash) {
        const session = getUserSession();
        if (session.mode !== "user" || !session.userId) {
            throw new Error("请先登录后再查看卡组构筑。");
        }

        const selectedEnvironment = String(this.environmentInput?.value || DEFAULT_ENVIRONMENT).trim() || DEFAULT_ENVIRONMENT;
        const params = new URLSearchParams();
        params.set("userId", session.userId);
        params.set("environment", selectedEnvironment);
        params.set("deckShapeHash", deckShapeHash);

        const response = await fetch(`${DECK_COMPOSITION_ENDPOINT}?${params.toString()}`, { method: "GET" });
        let payload = null;
        try {
            payload = await response.json();
        } catch (err) {
            // ignore parse errors
        }

        if (!response.ok) {
            const message = payload?.message || payload?.Message || response.statusText;
            throw new Error(message || `HTTP ${response.status}`);
        }

        return payload;
    }

    async openDeckPreviewByHash(deckShapeHash, fallbackDeckClass = "未知职业") {
        if (!this.deckPreview?.isAvailable()) {
            this.setMessage("卡组预览界面不可用。", true);
            return;
        }

        try {
            this.setMessage("正在加载卡组构筑...");
            const payload = await this.fetchDeckCompositionByHash(deckShapeHash);
            const cardsRaw = pickValue(payload, "cards", "Cards");
            const cards = this.normalizeDeckCompositionCards(cardsRaw);

            const deckClassRaw = String(pickValue(payload, "deckClass", "DeckClass") || fallbackDeckClass || "");
            const deckClassLabel = getClassLabel(deckClassRaw);
            const environmentRaw = String(pickValue(payload, "environment", "Environment") || this.environmentInput?.value || DEFAULT_ENVIRONMENT);
            const environmentLabel = getEnvironmentLabel(environmentRaw);
            const stats = this.findDeckStatsByHash(deckShapeHash);
            const opened = this.deckPreview.open({
                titleText: `${deckClassLabel}卡组`,
                metaText: `${deckClassLabel} | ${environmentLabel}`,
                cards,
                stats,
                exportEnabled: false,
                exportVisible: true
            });
            if (!opened) {
                this.setMessage("卡组预览界面不可用。", true);
                return;
            }
            const totalCount = cards.reduce((sum, item) => sum + toInt(item.count, 0), 0);
            this.setMessage(`卡组构筑已加载：${totalCount} 张卡牌。`);
        } catch (error) {
            this.setMessage(`加载卡组构筑失败：${error?.message || String(error)}`, true);
        }
    }

    closeDeckPreview() {
        this.deckPreview?.close();
    }

    async refreshSummary(force = false) {
        if (!this.isModalOpen && !force) return;

        const session = getUserSession();
        if (session.mode !== "user" || !session.userId) {
            this.cardStats = [];
            this.deckStats = [];
            this.classStats = [];
            this.renderActiveView();
            if (this.cardCountEl) this.cardCountEl.textContent = "0";
            if (this.deckCountEl) this.deckCountEl.textContent = "0";
            if (this.classCountEl) this.classCountEl.textContent = "0";
            if (this.generatedAtEl) this.generatedAtEl.textContent = "-";
            this.setMessage("请登录后查看胜率统计。", true);
            return;
        }

        const selectedEnvironment = String(this.environmentInput?.value || DEFAULT_ENVIRONMENT).trim() || DEFAULT_ENVIRONMENT;
        let top = toInt(this.topInput?.value, DEFAULT_TOP);
        top = Math.min(Math.max(top, 1), MAX_TOP);
        if (this.topInput) {
            this.topInput.value = String(top);
        }
        const startDate = String(this.startDateInput?.value || "").trim();
        const endDate = String(this.endDateInput?.value || "").trim();
        const cardNameKeyword = String(this.cardNameInput?.value || "").trim();
        const parsedStart = parseLocalTemporalFilter(startDate);
        const parsedEnd = parseLocalTemporalFilter(endDate);
        if (!parsedStart.ok || !parsedEnd.ok) {
            this.setMessage("时间格式无效，请使用 YYYY-MM-DDTHH:mm[:ss]。", true);
            return;
        }
        if (parsedStart.timestamp != null && parsedEnd.timestamp != null && parsedStart.timestamp > parsedEnd.timestamp) {
            this.setMessage("时间范围无效：开始时间不能晚于结束时间。", true);
            return;
        }

        const params = new URLSearchParams();
        params.set("userId", session.userId);
        params.set("environment", selectedEnvironment);
        params.set("top", String(top));
        if (cardNameKeyword) {
            params.set("cardName", cardNameKeyword);
        }
        const startDateUtc = toUtcIsoStringFromLocalTemporal(startDate);
        const endDateUtc = toUtcIsoStringFromLocalTemporal(endDate);
        if (startDateUtc) {
            params.set("startDate", startDateUtc);
        }
        if (endDateUtc) {
            params.set("endDate", endDateUtc);
        }

        this.setBusy(true);
        this.setMessage("正在加载胜率统计...");
        try {
            const response = await fetch(`${SUMMARY_ENDPOINT}?${params.toString()}`, { method: "GET" });
            let payload = null;
            try {
                payload = await response.json();
            } catch (err) {
                // ignore parse errors
            }

            if (!response.ok) {
                const message = payload?.message || payload?.Message || response.statusText;
                throw new Error(message || `HTTP ${response.status}`);
            }

            const cardStatsRaw = pickValue(payload, "cardStats", "CardStats");
            const deckStatsRaw = pickValue(payload, "deckStats", "DeckStats");
            const classStatsRaw = pickValue(payload, "classStats", "ClassStats");
            const generatedAt = formatTimestamp(pickValue(payload, "generatedAtUtc", "GeneratedAtUtc"));

            this.cardStats = this.normalizeCardStats(cardStatsRaw);
            this.deckStats = this.normalizeDeckStats(deckStatsRaw);
            this.classStats = this.normalizeClassStats(classStatsRaw);

            if (this.cardCountEl) this.cardCountEl.textContent = String(this.cardStats.length);
            if (this.deckCountEl) this.deckCountEl.textContent = String(this.deckStats.length);
            if (this.classCountEl) this.classCountEl.textContent = String(this.classStats.length);
            if (this.generatedAtEl) this.generatedAtEl.textContent = generatedAt;

            this.renderActiveView();
            this.updateSortIndicators();
            this.setMessage(`已加载：卡牌 ${this.cardStats.length} 行，卡组 ${this.deckStats.length} 行，职业 ${this.classStats.length} 行。`);
        } catch (error) {
            this.cardStats = [];
            this.deckStats = [];
            this.classStats = [];
            this.renderActiveView();
            if (this.cardCountEl) this.cardCountEl.textContent = "0";
            if (this.deckCountEl) this.deckCountEl.textContent = "0";
            if (this.classCountEl) this.classCountEl.textContent = "0";
            if (this.generatedAtEl) this.generatedAtEl.textContent = "-";
            this.setMessage(`加载失败：${error?.message || String(error)}`, true);
        } finally {
            this.setBusy(false);
        }
    }

    async dispose() {
        this.closeModal();
        this.deckPreview?.dispose();
        this.deckPreview = null;
        this.handlers.forEach(remove => remove());
        this.handlers = [];
        this.initialized = false;
    }
}

let instance = null;

export async function initializeDevToolsWinrateStats() {
    if (!instance) {
        instance = new DevToolsWinrateStats();
    }
    await instance.init();
}

export async function cleanupDevToolsWinrateStats() {
    if (!instance) return;
    await instance.dispose();
    instance = null;
}
