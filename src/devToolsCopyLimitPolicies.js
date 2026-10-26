import { getUserSession, USER_SESSION_CHANGED_EVENT } from "./userSession.js";
import {
    ensureDeckEnvironmentOptions,
    fillDeckEnvironmentSelect,
    getDefaultDeckEnvironment,
    normalizeDeckEnvironment as normalizeDeckEnvironmentShared
} from "./deckEnvironmentOptions.js";
import { notifyDeckValidationRulesUpdated as publishDeckValidationRulesUpdated } from "./dataChangeEvents.js";

const USERS_ENDPOINT = "/api/users";
const CARDS_ENDPOINT = "/api/cards";
const POLICY_ENDPOINT = "/api/deck-copy-limit-policies";
const DEFAULT_RULE = "standard";
const DEFAULT_LIMIT = 3;
const CARD_PICKER_PAGE_SIZE = 12;

function pickValue(source, camelKey, pascalKey) {
    if (!source || typeof source !== "object") return null;
    if (source[camelKey] != null) return source[camelKey];
    if (source[pascalKey] != null) return source[pascalKey];
    return null;
}

async function readJsonResponse(response) {
    if (response.ok) return response.json();
    const rawText = await response.text();
    let message = rawText.trim();
    try {
        const payload = JSON.parse(rawText);
        message = payload?.message || payload?.Message || (typeof payload === "string" ? payload : message);
    } catch {
        // Error responses may be plain text; successful JSON responses must parse.
    }
    throw new Error(message || response.statusText || `HTTP ${response.status}`);
}

function normalizeEnvironment(value) {
    return normalizeDeckEnvironmentShared(value, getDefaultDeckEnvironment());
}

function normalizeRule(value) {
    const text = String(value ?? "").trim().toLowerCase();
    if (text === "dualclass" || text === "dual_class") return "dualClass";
    return DEFAULT_RULE;
}

function toInt(value, fallback = 0) {
    const parsed = Number.parseInt(String(value ?? ""), 10);
    return Number.isFinite(parsed) ? parsed : fallback;
}

function normalizeLimit(value, fallback = DEFAULT_LIMIT) {
    const parsed = toInt(value, fallback);
    return Math.max(0, Math.min(DEFAULT_LIMIT, parsed));
}

function formatDateTime(value) {
    if (!value) return "-";
    const date = new Date(String(value));
    if (Number.isNaN(date.getTime())) return String(value);
    return date.toLocaleString();
}

function escapeHtml(value) {
    const text = String(value ?? "");
    return text
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;")
        .replaceAll("'", "&#39;");
}

class DevToolsCopyLimitPolicies {
    constructor() {
        this.initialized = false;
        this.handlers = [];
        this.isModalOpen = false;
        this.isEditorOpen = false;
        this.isCardPickerOpen = false;

        this.queryItems = [];
        this.cardEntries = [];
        this.cardNameById = new Map();
        this.collectibleCardIdSet = new Set();
        this.cardIndexLoaded = false;

        this.editorMode = "create";
        this.editorCardDefinitionId = null;

        this.cardPickerCurrentPage = 1;
        this.cardPickerFilteredEntries = [];
        this.cardPickerSelectedId = null;
        this.requestOwner = {};
        this.editorOwner = {};
        this.queryRequest = null;
    }

    async init() {
        if (this.initialized) return;
        this.initialized = true;

        this.root = document.getElementById("devtools-copy-limit-panel");
        this.modalEl = document.getElementById("devtools-copy-limit-modal");
        this.openBtn = document.getElementById("devtools-copy-limit-open-btn");
        this.closeBtn = document.getElementById("devtools-copy-limit-close-btn");
        this.panel = document.getElementById("devtools-copy-limit-management-panel");
        this.messageEl = document.getElementById("devtools-copy-limit-message");

        this.queryEnvironmentInput = document.getElementById("devtools-copy-limit-query-environment");
        this.queryRuleInput = document.getElementById("devtools-copy-limit-query-rule");
        this.querySearchInput = document.getElementById("devtools-copy-limit-query-search");
        this.queryRefreshBtn = document.getElementById("devtools-copy-limit-query-refresh-btn");
        this.addBtn = document.getElementById("devtools-copy-limit-add-btn");
        this.countEl = document.getElementById("devtools-copy-limit-count");
        this.tableBody = document.getElementById("devtools-copy-limit-table-body");

        this.editorModalEl = document.getElementById("devtools-copy-limit-editor-modal");
        this.editorTitleEl = document.getElementById("devtools-copy-limit-editor-title");
        this.editorCloseBtn = document.getElementById("devtools-copy-limit-editor-close-btn");
        this.editorCancelBtn = document.getElementById("devtools-copy-limit-editor-cancel-btn");
        this.editorSaveBtn = document.getElementById("devtools-copy-limit-editor-save-btn");
        this.editorEnvironmentInput = document.getElementById("devtools-copy-limit-editor-environment");
        this.editorRuleInput = document.getElementById("devtools-copy-limit-editor-rule");
        this.editorMaxInput = document.getElementById("devtools-copy-limit-editor-max");
        this.editorCardDisplayEl = document.getElementById("devtools-copy-limit-editor-card-display");
        this.editorSelectCardBtn = document.getElementById("devtools-copy-limit-editor-select-card-btn");
        this.editorSummaryEl = document.getElementById("devtools-copy-limit-editor-summary");

        this.cardPickerModalEl = document.getElementById("devtools-copy-limit-card-picker-modal");
        this.cardPickerCloseBtn = document.getElementById("devtools-copy-limit-card-picker-close-btn");
        this.cardPickerCancelBtn = document.getElementById("devtools-copy-limit-card-picker-cancel");
        this.cardPickerConfirmBtn = document.getElementById("devtools-copy-limit-card-picker-confirm");
        this.cardPickerSearchInput = document.getElementById("devtools-copy-limit-card-picker-search");
        this.cardPickerStatsEl = document.getElementById("devtools-copy-limit-card-picker-stats");
        this.cardPickerListEl = document.getElementById("devtools-copy-limit-card-picker-list");
        this.cardPickerPrevBtn = document.getElementById("devtools-copy-limit-card-picker-prev");
        this.cardPickerNextBtn = document.getElementById("devtools-copy-limit-card-picker-next");
        this.cardPickerPageInfoEl = document.getElementById("devtools-copy-limit-card-picker-page-info");

        if (!this.root || !this.modalEl || !this.openBtn || !this.closeBtn || !this.panel || !this.messageEl || !this.tableBody) {
            return;
        }

        await this.loadEnvironmentOptions();
        if (!this.initialized) return;

        // Move modals to document body to avoid local stacking-context conflicts.
        if (this.modalEl && this.modalEl.parentElement !== document.body) {
            document.body.appendChild(this.modalEl);
        }
        if (this.editorModalEl && this.editorModalEl.parentElement !== document.body) {
            document.body.appendChild(this.editorModalEl);
        }
        if (this.cardPickerModalEl && this.cardPickerModalEl.parentElement !== document.body) {
            document.body.appendChild(this.cardPickerModalEl);
        }

        this.bindUI();
        if (this.editorModalEl) {
            this.editorModalEl.hidden = true;
            this.editorModalEl.setAttribute("aria-hidden", "true");
            this.editorModalEl.style.display = "none";
        }
        if (this.cardPickerModalEl) {
            this.cardPickerModalEl.hidden = true;
            this.cardPickerModalEl.setAttribute("aria-hidden", "true");
            this.cardPickerModalEl.style.display = "none";
        }
        this.closeModal();
    }

    async loadEnvironmentOptions() {
        await ensureDeckEnvironmentOptions();
        if (!this.initialized) return;
        fillDeckEnvironmentSelect(this.queryEnvironmentInput, this.queryEnvironmentInput?.value);
        fillDeckEnvironmentSelect(this.editorEnvironmentInput, this.editorEnvironmentInput?.value);
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
            if (this.isCardPickerOpen) {
                this.closeCardPickerModal();
                return;
            }
            if (this.isEditorOpen) {
                this.closeEditorModal();
                return;
            }
            if (this.isModalOpen) {
                this.closeModal();
            }
        };
        document.addEventListener("keydown", escHandler);
        this.handlers.push(() => document.removeEventListener("keydown", escHandler));

        if (this.queryEnvironmentInput) {
            const handler = () => {
                void this.refreshQueryPolicies();
            };
            this.queryEnvironmentInput.addEventListener("change", handler);
            this.handlers.push(() => this.queryEnvironmentInput.removeEventListener("change", handler));
        }

        if (this.queryRuleInput) {
            const handler = () => {
                void this.refreshQueryPolicies();
            };
            this.queryRuleInput.addEventListener("change", handler);
            this.handlers.push(() => this.queryRuleInput.removeEventListener("change", handler));
        }

        if (this.querySearchInput) {
            const handler = () => {
                this.renderQueryRows(this.queryItems);
            };
            this.querySearchInput.addEventListener("input", handler);
            this.handlers.push(() => this.querySearchInput.removeEventListener("input", handler));
        }

        if (this.queryRefreshBtn) {
            const handler = () => {
                void this.refreshQueryPolicies();
            };
            this.queryRefreshBtn.addEventListener("click", handler);
            this.handlers.push(() => this.queryRefreshBtn.removeEventListener("click", handler));
        }

        if (this.addBtn) {
            const handler = () => {
                this.openCreateEditorModal();
            };
            this.addBtn.addEventListener("click", handler);
            this.handlers.push(() => this.addBtn.removeEventListener("click", handler));
        }

        if (this.tableBody) {
            const handler = (event) => {
                const target = event.target;
                const button = target instanceof Element ? target.closest("button[data-action]") : null;
                if (!button) return;

                const action = button.getAttribute("data-action");
                const cardId = toInt(button.getAttribute("data-card-id"), 0);
                if (cardId <= 0) return;

                if (action === "edit-row") {
                    const maxCopies = normalizeLimit(button.getAttribute("data-max"));
                    this.openEditEditorModal(cardId, maxCopies);
                    return;
                }

                if (action === "delete-row") {
                    void this.deletePolicyForCard(cardId, button);
                }
            };
            this.tableBody.addEventListener("click", handler);
            this.handlers.push(() => this.tableBody.removeEventListener("click", handler));
        }

        if (this.editorModalEl) {
            const handler = (event) => {
                if (event.target === this.editorModalEl) {
                    this.closeEditorModal();
                }
            };
            this.editorModalEl.addEventListener("click", handler);
            this.handlers.push(() => this.editorModalEl.removeEventListener("click", handler));
        }

        if (this.editorCloseBtn) {
            const handler = () => {
                this.closeEditorModal();
            };
            this.editorCloseBtn.addEventListener("click", handler);
            this.handlers.push(() => this.editorCloseBtn.removeEventListener("click", handler));
        }

        if (this.editorCancelBtn) {
            const handler = () => {
                this.closeEditorModal();
            };
            this.editorCancelBtn.addEventListener("click", handler);
            this.handlers.push(() => this.editorCancelBtn.removeEventListener("click", handler));
        }

        if (this.editorSaveBtn) {
            const handler = () => {
                void this.saveEditorPolicy();
            };
            this.editorSaveBtn.addEventListener("click", handler);
            this.handlers.push(() => this.editorSaveBtn.removeEventListener("click", handler));
        }

        if (this.editorSelectCardBtn) {
            const handler = () => {
                void this.openCardPickerModal();
            };
            this.editorSelectCardBtn.addEventListener("click", handler);
            this.handlers.push(() => this.editorSelectCardBtn.removeEventListener("click", handler));
        }

        if (this.cardPickerModalEl) {
            const handler = (event) => {
                if (event.target === this.cardPickerModalEl) {
                    this.closeCardPickerModal();
                }
            };
            this.cardPickerModalEl.addEventListener("click", handler);
            this.handlers.push(() => this.cardPickerModalEl.removeEventListener("click", handler));
        }

        if (this.cardPickerCloseBtn) {
            const handler = () => {
                this.closeCardPickerModal();
            };
            this.cardPickerCloseBtn.addEventListener("click", handler);
            this.handlers.push(() => this.cardPickerCloseBtn.removeEventListener("click", handler));
        }

        if (this.cardPickerCancelBtn) {
            const handler = () => {
                this.closeCardPickerModal();
            };
            this.cardPickerCancelBtn.addEventListener("click", handler);
            this.handlers.push(() => this.cardPickerCancelBtn.removeEventListener("click", handler));
        }

        if (this.cardPickerConfirmBtn) {
            const handler = () => {
                this.confirmCardPickerSelection();
            };
            this.cardPickerConfirmBtn.addEventListener("click", handler);
            this.handlers.push(() => this.cardPickerConfirmBtn.removeEventListener("click", handler));
        }

        if (this.cardPickerSearchInput) {
            const handler = () => {
                this.applyCardPickerFilter(this.cardPickerSearchInput.value);
            };
            this.cardPickerSearchInput.addEventListener("input", handler);
            this.handlers.push(() => this.cardPickerSearchInput.removeEventListener("input", handler));
        }

        if (this.cardPickerPrevBtn) {
            const handler = () => {
                if (this.cardPickerCurrentPage <= 1) return;
                this.cardPickerCurrentPage -= 1;
                this.renderCardPickerList();
            };
            this.cardPickerPrevBtn.addEventListener("click", handler);
            this.handlers.push(() => this.cardPickerPrevBtn.removeEventListener("click", handler));
        }

        if (this.cardPickerNextBtn) {
            const handler = () => {
                const totalPages = Math.max(1, Math.ceil(this.cardPickerFilteredEntries.length / CARD_PICKER_PAGE_SIZE));
                if (this.cardPickerCurrentPage >= totalPages) return;
                this.cardPickerCurrentPage += 1;
                this.renderCardPickerList();
            };
            this.cardPickerNextBtn.addEventListener("click", handler);
            this.handlers.push(() => this.cardPickerNextBtn.removeEventListener("click", handler));
        }

        const sessionHandler = () => {
            this.invalidateRequests();
            this.clearQuery();
            this.closeEditorModal();
            if (this.isModalOpen) {
                void this.refreshQueryPolicies();
            }
        };
        document.addEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler);
        this.handlers.push(() => document.removeEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler));
    }

    async openModal() {
        if (!this.modalEl) return;
        this.invalidateRequests();
        this.clearQuery();
        this.modalEl.classList.add("active");
        this.modalEl.setAttribute("aria-hidden", "false");
        this.isModalOpen = true;
        await this.refreshQueryPolicies();
    }

    closeModal() {
        if (!this.modalEl) return;
        this.invalidateRequests();
        this.clearQuery();
        this.closeCardPickerModal();
        this.closeEditorModal();
        this.modalEl.classList.remove("active");
        this.modalEl.setAttribute("aria-hidden", "true");
        this.isModalOpen = false;
    }

    invalidateRequests() {
        this.requestOwner = {};
        this.queryRequest?.abort();
        this.queryRequest = null;
        this.setQueryBusy(false);
    }

    ownsRequest(owner) {
        return this.initialized && this.isModalOpen && this.requestOwner === owner;
    }

    clearQuery() {
        this.queryItems = [];
        this.renderQueryRows([]);
        this.setAdminPanelVisible(false);
    }

    openEditorModal() {
        if (!this.editorModalEl) return;
        this.editorOwner = {};
        this.setEditorBusy(false);
        this.editorModalEl.hidden = false;
        this.editorModalEl.setAttribute("aria-hidden", "false");
        this.editorModalEl.style.display = "flex";
        this.isEditorOpen = true;
    }

    closeEditorModal() {
        if (!this.editorModalEl) return;
        this.editorOwner = {};
        this.setEditorBusy(false);
        this.setMessage("");
        this.closeCardPickerModal();
        this.editorModalEl.hidden = true;
        this.editorModalEl.setAttribute("aria-hidden", "true");
        this.editorModalEl.style.display = "none";
        this.isEditorOpen = false;
    }

    openCardPickerFrame() {
        if (!this.cardPickerModalEl) return;
        this.cardPickerModalEl.hidden = false;
        this.cardPickerModalEl.setAttribute("aria-hidden", "false");
        this.cardPickerModalEl.style.display = "flex";
        this.isCardPickerOpen = true;
    }

    closeCardPickerModal() {
        if (!this.cardPickerModalEl) return;
        this.cardPickerModalEl.hidden = true;
        this.cardPickerModalEl.setAttribute("aria-hidden", "true");
        this.cardPickerModalEl.style.display = "none";
        this.isCardPickerOpen = false;
    }

    setMessage(text, isError = false) {
        if (!this.messageEl) return;
        this.messageEl.textContent = text || "";
        this.messageEl.dataset.tone = isError ? "error" : "neutral";
    }

    notifyDeckValidationRulesUpdated(environment, rule) {
        publishDeckValidationRulesUpdated({
            environment: normalizeEnvironment(environment),
            rule: normalizeRule(rule)
        });
    }

    setEditorSummary(text, isError = false) {
        if (!this.editorSummaryEl) return;
        this.editorSummaryEl.textContent = text || "";
        this.editorSummaryEl.dataset.tone = isError ? "error" : "neutral";
    }

    setQueryBusy(busy) {
        const disabled = Boolean(busy);
        if (this.queryEnvironmentInput) this.queryEnvironmentInput.disabled = disabled;
        if (this.queryRuleInput) this.queryRuleInput.disabled = disabled;
        if (this.querySearchInput) this.querySearchInput.disabled = disabled;
        if (this.queryRefreshBtn) this.queryRefreshBtn.disabled = disabled;
        if (this.addBtn) this.addBtn.disabled = disabled;
    }

    setEditorBusy(busy) {
        const disabled = Boolean(busy);
        if (this.editorEnvironmentInput) this.editorEnvironmentInput.disabled = disabled;
        if (this.editorRuleInput) this.editorRuleInput.disabled = disabled;
        if (this.editorMaxInput) this.editorMaxInput.disabled = disabled;
        if (this.editorSelectCardBtn) this.editorSelectCardBtn.disabled = disabled || this.editorMode === "edit";
        if (this.editorSaveBtn) this.editorSaveBtn.disabled = disabled;
        if (this.editorCancelBtn) this.editorCancelBtn.disabled = disabled;
        if (this.editorCloseBtn) this.editorCloseBtn.disabled = disabled;
    }

    setAdminPanelVisible(visible) {
        if (!this.panel || !this.root) return;
        this.panel.hidden = !visible;
        this.root.classList.toggle("is-admin", visible);
    }

    getQueryEnvironment() {
        return normalizeEnvironment(this.queryEnvironmentInput?.value || getDefaultDeckEnvironment());
    }

    getQueryRule() {
        return normalizeRule(this.queryRuleInput?.value || DEFAULT_RULE);
    }

    getQuerySearchKeyword() {
        return String(this.querySearchInput?.value || "").trim().toLowerCase();
    }

    getEditorEnvironment() {
        return normalizeEnvironment(this.editorEnvironmentInput?.value || getDefaultDeckEnvironment());
    }

    getEditorRule() {
        return normalizeRule(this.editorRuleInput?.value || DEFAULT_RULE);
    }

    getEditorMaxCopies() {
        return normalizeLimit(this.editorMaxInput?.value, DEFAULT_LIMIT);
    }

    isLoggedInSession(session) {
        return Boolean(session && session.mode === "user" && session.userId);
    }

    getCardName(cardDefinitionId) {
        return this.cardNameById.get(cardDefinitionId) || `卡牌 #${cardDefinitionId}`;
    }

    normalizeCardEntry(card) {
        const id = pickValue(card, "id", "Id");
        const name = pickValue(card, "name", "Name");
        const cost = toInt(pickValue(card, "cost", "Cost"), 0);
        const cardType = String(
            pickValue(card, "cardType", "CardType") ??
            pickValue(card, "type", "Type") ??
            ""
        ).trim();
        const cardClass = String(
            pickValue(card, "cardClass", "CardClass") ??
            pickValue(card, "class", "Class") ??
            ""
        ).trim();
        const rarity = String(pickValue(card, "rarity", "Rarity") ?? "").trim();
        const searchText = `${id} ${name} ${cardType} ${cardClass} ${rarity}`.toLowerCase();

        return { id, name, cost, cardType, cardClass, rarity, searchText };
    }

    isCollectibleCard(card) {
        const collectible = pickValue(card, "isCollectible", "IsCollectible");
        return collectible === true;
    }

    filterCollectiblePolicyItems(items) {
        return items.filter(item => this.collectibleCardIdSet.has(item.cardDefinitionId));
    }

    async ensureCardIndexLoaded(signal) {
        if (this.cardIndexLoaded) return;
        const response = await fetch(CARDS_ENDPOINT, { method: "GET", signal });
        const cards = await readJsonResponse(response);
        if (signal.aborted) return;
        if (!Array.isArray(cards)) throw new Error("卡牌目录响应必须为数组。");
        for (const card of cards) {
            const id = pickValue(card, "id", "Id");
            const name = pickValue(card, "name", "Name");
            if (!Number.isInteger(id) || id < 0 || typeof name !== "string" || !name.trim()
                || typeof pickValue(card, "isCollectible", "IsCollectible") !== "boolean") {
                throw new Error("卡牌目录响应中的 ID、名称或可构筑标记无效。");
            }
        }
        // Card 0 is the catalogue leader placeholder, not a deck card.
        const entries = cards
            .filter(card => pickValue(card, "id", "Id") > 0 && this.isCollectibleCard(card))
            .map(card => this.normalizeCardEntry(card))
            .sort((left, right) => left.id - right.id);
        this.cardNameById.clear();
        this.collectibleCardIdSet.clear();
        this.cardEntries = entries;
        for (const entry of entries) {
            this.cardNameById.set(entry.id, entry.name);
            this.collectibleCardIdSet.add(entry.id);
        }
        this.cardIndexLoaded = true;
    }

    async verifyAdminAccess(session, signal) {
        const params = new URLSearchParams();
        params.set("actorUserId", session.userId);
        params.set("limit", "1");
        const response = await fetch(`${USERS_ENDPOINT}?${params.toString()}`, { method: "GET", signal });
        if (response.status === 403) return false;
        const payload = await readJsonResponse(response);
        if (!Array.isArray(pickValue(payload, "items", "Items"))) {
            throw new Error("管理员查询响应中的 items 必须为数组。");
        }
        return true;
    }

    normalizePolicyItems(payload, environment, rule) {
        const responseEnvironment = pickValue(payload, "environment", "Environment");
        const responseRule = pickValue(payload, "rule", "Rule");
        if (typeof responseEnvironment !== "string" || responseEnvironment.toLowerCase() !== environment.toLowerCase()
            || typeof responseRule !== "string" || responseRule.toLowerCase() !== rule.toLowerCase()) {
            throw new Error("规则响应的环境或对战规则与请求不符。");
        }
        const items = pickValue(payload, "items", "Items");
        if (!Array.isArray(items)) throw new Error("规则响应中的 items 必须为数组。");
        return items.map(item => {
            const cardDefinitionId = pickValue(item, "cardDefinitionId", "CardDefinitionId");
            const maxCopies = pickValue(item, "maxCopies", "MaxCopies");
            if (!Number.isInteger(cardDefinitionId) || cardDefinitionId <= 0) {
                throw new Error("规则响应中的 cardDefinitionId 必须为正整数。");
            }
            if (!Number.isInteger(maxCopies) || maxCopies < 0 || maxCopies > DEFAULT_LIMIT) {
                throw new Error(`规则响应中的 maxCopies 必须为 0 到 ${DEFAULT_LIMIT} 的整数。`);
            }
            const updatedAtUtc = String(pickValue(item, "updatedAtUtc", "UpdatedAtUtc") || "");
            return { cardDefinitionId, maxCopies, updatedAtUtc };
        }).sort((left, right) => left.cardDefinitionId - right.cardDefinitionId);
    }

    async fetchPolicies(environment, rule, signal) {
        const params = new URLSearchParams();
        params.set("environment", environment);
        params.set("rule", rule);
        const response = await fetch(`${POLICY_ENDPOINT}?${params.toString()}`, { method: "GET", signal });
        return this.normalizePolicyItems(await readJsonResponse(response), environment, rule);
    }

    renderQueryRows(items) {
        if (!this.tableBody) return;

        const keyword = this.getQuerySearchKeyword();
        const source = this.filterCollectiblePolicyItems(items);
        const filtered = source.filter((item) => {
            if (!keyword) return true;
            const cardName = this.getCardName(item.cardDefinitionId).toLowerCase();
            return cardName.includes(keyword) || String(item.cardDefinitionId).includes(keyword);
        });

        if (this.countEl) {
            this.countEl.textContent = String(filtered.length);
        }

        this.tableBody.innerHTML = "";
        if (filtered.length === 0) {
            const row = document.createElement("tr");
            row.innerHTML = '<td colspan="5" class="devtools-copy-limit-empty">暂无规则数据</td>';
            this.tableBody.appendChild(row);
            return;
        }

        const html = filtered.map((item) => `
            <tr>
                <td class="devtools-copy-limit-card-id">${item.cardDefinitionId}</td>
                <td>${escapeHtml(this.getCardName(item.cardDefinitionId))}</td>
                <td>${item.maxCopies}</td>
                <td>${formatDateTime(item.updatedAtUtc)}</td>
                <td>
                    <div class="devtools-copy-limit-row-actions">
                        <button class="tool-button devtools-copy-limit-row-edit-btn" type="button" data-action="edit-row" data-card-id="${item.cardDefinitionId}" data-max="${item.maxCopies}">编辑</button>
                        <button class="tool-button devtools-copy-limit-row-delete-btn" type="button" data-action="delete-row" data-card-id="${item.cardDefinitionId}">删除</button>
                    </div>
                </td>
            </tr>
        `).join("");

        this.tableBody.innerHTML = html;
    }

    async refreshQueryPolicies() {
        if (!this.isModalOpen) return;
        this.queryRequest?.abort();
        const request = new AbortController();
        // Retain the displayed-query identity so late writes cannot replace another query.
        this.queryRequest = request;
        const owner = this.requestOwner;
        const ownsQuery = () => this.ownsRequest(owner) && this.queryRequest === request;
        const environment = this.getQueryEnvironment();
        const rule = this.getQueryRule();
        const session = getUserSession();
        if (!this.isLoggedInSession(session)) {
            this.clearQuery();
            this.closeEditorModal();
            this.setMessage("请先登录后再访问禁限卡牌管理。", true);
            return;
        }
        this.setQueryBusy(true);
        this.setMessage("正在加载禁限卡牌...");
        try {
            const isAdmin = await this.verifyAdminAccess(session, request.signal);
            if (!ownsQuery()) return;
            if (!isAdmin) {
                this.clearQuery();
                this.closeEditorModal();
                this.setMessage("仅管理员可访问禁限卡牌管理。", true);
                return;
            }
            await this.ensureCardIndexLoaded(request.signal);
            if (!ownsQuery()) return;
            const items = await this.fetchPolicies(environment, rule, request.signal);
            if (!ownsQuery()) return;
            this.queryItems = this.filterCollectiblePolicyItems(items);
            this.renderQueryRows(this.queryItems);
            this.setAdminPanelVisible(true);
            this.setMessage(`已加载 ${this.queryItems.length} 条规则。`);
        } catch (error) {
            if (!ownsQuery()) return;
            this.clearQuery();
            this.setMessage(`加载失败：${error?.message || String(error)}`, true);
        } finally {
            if (ownsQuery()) this.setQueryBusy(false);
        }
    }

    async upsertItems(environment, rule, items) {
        const session = getUserSession();
        if (!this.isLoggedInSession(session)) throw new Error("请先登录。");
        const response = await fetch(POLICY_ENDPOINT, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ actorUserId: session.userId, environment, rule, items })
        });
        const savedItems = this.normalizePolicyItems(await readJsonResponse(response), environment, rule);
        if (!items.every(item => savedItems.some(saved => saved.cardDefinitionId === item.cardDefinitionId && saved.maxCopies === item.maxCopies))) {
            throw new Error("保存响应未包含请求的卡牌规则。");
        }
        return savedItems;
    }

    async deleteItem(environment, rule, cardDefinitionId) {
        const session = getUserSession();
        if (!this.isLoggedInSession(session)) throw new Error("请先登录。");
        const params = new URLSearchParams();
        params.set("actorUserId", session.userId);
        params.set("environment", environment);
        params.set("rule", rule);
        params.set("cardDefinitionId", String(cardDefinitionId));
        const response = await fetch(`${POLICY_ENDPOINT}?${params.toString()}`, { method: "DELETE" });
        if (!response.ok) await readJsonResponse(response);
        if (response.status !== 204) throw new Error("删除接口必须返回 204。");
    }

    openCreateEditorModal() {
        this.editorMode = "create";
        this.editorCardDefinitionId = null;

        if (this.editorTitleEl) this.editorTitleEl.textContent = "新增禁限卡牌";
        if (this.editorEnvironmentInput) this.editorEnvironmentInput.value = this.getQueryEnvironment();
        if (this.editorRuleInput) this.editorRuleInput.value = this.getQueryRule();
        if (this.editorMaxInput) this.editorMaxInput.value = String(DEFAULT_LIMIT);

        if (this.editorSelectCardBtn) {
            this.editorSelectCardBtn.disabled = false;
            this.editorSelectCardBtn.textContent = "选择卡牌";
        }

        this.renderEditorCardDisplay();
        this.setEditorSummary("请先选择卡牌，再保存禁限卡牌。", false);
        this.openEditorModal();
    }

    openEditEditorModal(cardDefinitionId, maxCopies) {
        this.editorMode = "edit";
        this.editorCardDefinitionId = cardDefinitionId;

        if (this.editorTitleEl) this.editorTitleEl.textContent = "编辑禁限卡牌";
        if (this.editorEnvironmentInput) this.editorEnvironmentInput.value = this.getQueryEnvironment();
        if (this.editorRuleInput) this.editorRuleInput.value = this.getQueryRule();
        if (this.editorMaxInput) this.editorMaxInput.value = String(normalizeLimit(maxCopies, DEFAULT_LIMIT));

        if (this.editorSelectCardBtn) {
            this.editorSelectCardBtn.disabled = true;
            this.editorSelectCardBtn.textContent = "编辑模式卡牌固定";
        }

        this.renderEditorCardDisplay();
        this.setEditorSummary("可修改该卡在当前环境/规则下的携带上限。", false);
        this.openEditorModal();
    }

    renderEditorCardDisplay() {
        if (!this.editorCardDisplayEl) return;
        const cardId = toInt(this.editorCardDefinitionId, 0);
        if (cardId <= 0) {
            this.editorCardDisplayEl.textContent = "未选择卡牌";
            return;
        }

        const name = this.getCardName(cardId);
        this.editorCardDisplayEl.textContent = `${name} (#${cardId})`;
    }

    openCardPickerModal() {
        if (!this.isEditorOpen) return;
        if (this.editorMode === "edit") return;

        this.cardPickerSelectedId = toInt(this.editorCardDefinitionId, 0) || null;
        this.cardPickerCurrentPage = 1;
        if (this.cardPickerSearchInput) this.cardPickerSearchInput.value = "";
        this.cardPickerFilteredEntries = [...this.cardEntries];

        this.openCardPickerFrame();
        this.renderCardPickerList();
    }

    applyCardPickerFilter(rawValue) {
        const keyword = String(rawValue || "").trim().toLowerCase();
        if (!keyword) {
            this.cardPickerFilteredEntries = [...this.cardEntries];
        } else {
            this.cardPickerFilteredEntries = this.cardEntries.filter((entry) => entry.searchText.includes(keyword));
        }
        this.cardPickerCurrentPage = 1;
        this.renderCardPickerList();
    }

    updateCardPickerStats() {
        if (!this.cardPickerStatsEl) return;
        const base = `卡牌 ${this.cardPickerFilteredEntries.length}`;
        this.cardPickerStatsEl.textContent = this.cardPickerSelectedId ? `${base} · 已选 1` : base;
    }

    updateCardPickerPagination(totalPages) {
        const normalizedTotal = Math.max(totalPages, 1);
        if (this.cardPickerPageInfoEl) {
            this.cardPickerPageInfoEl.textContent = `${this.cardPickerCurrentPage} / ${normalizedTotal}`;
        }
        if (this.cardPickerPrevBtn) this.cardPickerPrevBtn.disabled = this.cardPickerCurrentPage <= 1;
        if (this.cardPickerNextBtn) this.cardPickerNextBtn.disabled = this.cardPickerCurrentPage >= normalizedTotal;
    }

    renderCardPickerList() {
        if (!this.cardPickerListEl) return;
        const totalPages = Math.max(1, Math.ceil(this.cardPickerFilteredEntries.length / CARD_PICKER_PAGE_SIZE));
        this.cardPickerCurrentPage = Math.min(this.cardPickerCurrentPage, totalPages);

        if (this.cardPickerFilteredEntries.length === 0) {
            this.cardPickerListEl.innerHTML = '<div class="abilities-placeholder"><p>没有匹配的卡牌</p></div>';
            this.updateCardPickerStats();
            this.updateCardPickerPagination(0);
            if (this.cardPickerConfirmBtn) this.cardPickerConfirmBtn.disabled = true;
            return;
        }

        const start = (this.cardPickerCurrentPage - 1) * CARD_PICKER_PAGE_SIZE;
        const pageEntries = this.cardPickerFilteredEntries.slice(start, start + CARD_PICKER_PAGE_SIZE);

        const html = pageEntries.map((entry) => {
            const isSelected = this.cardPickerSelectedId === entry.id;
            const cardType = entry.cardType ? `<span class="tech-tag">${escapeHtml(entry.cardType)}</span>` : "";
            const cardClass = entry.cardClass ? `<span class="tech-tag">${escapeHtml(entry.cardClass)}</span>` : "";
            const rarity = entry.rarity ? `<span class="tech-tag">${escapeHtml(entry.rarity)}</span>` : "";
            return `
                <div class="ability-copy-card${isSelected ? " selected" : ""}" data-card-id="${entry.id}">
                    <button type="button" class="ability-copy-card-header" aria-pressed="${isSelected}">
                        <div class="ability-copy-card-main">
                            <span class="ability-copy-card-name tech-text-bold">${escapeHtml(entry.name)}</span>
                            <span class="ability-copy-card-id tech-text-dim">#${entry.id}</span>
                        </div>
                        <div class="ability-copy-card-meta">
                            <span class="tech-badge">COST ${entry.cost}</span>
                            ${cardType}
                            ${cardClass}
                            ${rarity}
                        </div>
                    </button>
                </div>
            `;
        }).join("");

        this.cardPickerListEl.innerHTML = html;
        this.cardPickerListEl.querySelectorAll(".ability-copy-card").forEach((cardEl) => {
            const cardId = toInt(cardEl.getAttribute("data-card-id"), 0);
            const header = cardEl.querySelector(".ability-copy-card-header");
            if (!header || cardId <= 0) return;

            header.addEventListener("click", () => {
                this.cardPickerSelectedId = cardId;
                this.cardPickerListEl.querySelectorAll(".ability-copy-card").forEach((node) => node.classList.remove("selected"));
                cardEl.classList.add("selected");
                if (this.cardPickerConfirmBtn) this.cardPickerConfirmBtn.disabled = false;
                this.updateCardPickerStats();
            });
        });

        this.updateCardPickerStats();
        this.updateCardPickerPagination(totalPages);
        if (this.cardPickerConfirmBtn) this.cardPickerConfirmBtn.disabled = !this.cardPickerSelectedId;
    }

    confirmCardPickerSelection() {
        const cardId = toInt(this.cardPickerSelectedId, 0);
        if (cardId <= 0) return;
        this.editorCardDefinitionId = cardId;
        this.renderEditorCardDisplay();
        this.setEditorSummary(`已选择卡牌：${this.getCardName(cardId)}。`, false);
        this.closeCardPickerModal();
    }

    async saveEditorPolicy() {
        if (!this.isModalOpen || !this.isEditorOpen) return;
        const owner = this.requestOwner;
        const editor = this.editorOwner;
        const query = this.queryRequest;
        const ownsEditor = () => this.ownsRequest(owner) && this.isEditorOpen && this.editorOwner === editor;
        const cardDefinitionId = toInt(this.editorCardDefinitionId, 0);
        if (cardDefinitionId <= 0) {
            this.setEditorSummary("请选择卡牌后再保存。", true);
            return;
        }

        if (!this.cardNameById.has(cardDefinitionId)) {
            this.setEditorSummary(`未找到卡牌 ID ${cardDefinitionId}。`, true);
            return;
        }

        const environment = this.getEditorEnvironment();
        const rule = this.getEditorRule();
        const maxCopies = this.getEditorMaxCopies();

        this.setEditorBusy(true);
        this.setMessage(`正在保存 ${this.getCardName(cardDefinitionId)} 的规则...`);
        try {
            const items = await this.upsertItems(environment, rule, [{ cardDefinitionId, maxCopies }]);
            // Confirmed rule changes still invalidate deck caches after this view closes.
            this.notifyDeckValidationRulesUpdated(environment, rule);
            if (!this.ownsRequest(owner)) return;
            if (query === this.queryRequest && environment === this.getQueryEnvironment() && rule === this.getQueryRule()) {
                this.queryItems = this.filterCollectiblePolicyItems(items);
                this.renderQueryRows(this.queryItems);
            }
            if (!ownsEditor()) return;
            this.closeEditorModal();
            this.setMessage(`保存成功：${this.getCardName(cardDefinitionId)} 上限 ${maxCopies}。`);
        } catch (error) {
            if (!ownsEditor()) return;
            this.setEditorSummary(`保存失败：${error?.message || String(error)}`, true);
            this.setMessage(`保存失败：${error?.message || String(error)}`, true);
        } finally {
            if (ownsEditor()) this.setEditorBusy(false);
        }
    }

    async deletePolicyForCard(cardDefinitionId, button) {
        if (!this.isModalOpen) return;
        const owner = this.requestOwner;
        const query = this.queryRequest;
        const ownsQuery = () => this.ownsRequest(owner) && this.queryRequest === query;
        const confirmed = window.confirm(`确认删除卡牌 ${cardDefinitionId} 的规则并恢复默认上限吗？`);
        if (!confirmed) return;

        const environment = this.getQueryEnvironment();
        const rule = this.getQueryRule();

        button?.setAttribute("disabled", "disabled");
        this.setMessage(`正在删除 ${this.getCardName(cardDefinitionId)} 的规则...`);
        try {
            await this.deleteItem(environment, rule, cardDefinitionId);
            this.notifyDeckValidationRulesUpdated(environment, rule);
            if (!ownsQuery()) return;
            const items = await this.fetchPolicies(environment, rule, query.signal);
            if (!ownsQuery()) return;
            this.queryItems = this.filterCollectiblePolicyItems(items);
            this.renderQueryRows(this.queryItems);
            this.setMessage(`已删除 ${this.getCardName(cardDefinitionId)} 的规则。`);
        } catch (error) {
            if (!ownsQuery()) return;
            this.setMessage(`删除失败：${error?.message || String(error)}`, true);
        } finally {
            if (ownsQuery()) button?.removeAttribute("disabled");
        }
    }

    async dispose() {
        this.initialized = false;
        this.closeModal();
        this.handlers.forEach((remove) => remove());
        this.handlers = [];
        this.queryItems = [];
        this.cardEntries = [];
        this.cardNameById.clear();
        this.collectibleCardIdSet.clear();
        this.cardIndexLoaded = false;
    }
}

let instance = null;

export async function initializeDevToolsCopyLimitPolicies() {
    if (!instance) {
        instance = new DevToolsCopyLimitPolicies();
    }
    await instance.init();
}

export async function cleanupDevToolsCopyLimitPolicies() {
    if (!instance) return;
    await instance.dispose();
    instance = null;
}
