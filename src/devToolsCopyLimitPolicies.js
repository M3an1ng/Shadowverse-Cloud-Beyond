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
                void this.refreshQueryPolicies(true);
            };
            this.queryEnvironmentInput.addEventListener("change", handler);
            this.handlers.push(() => this.queryEnvironmentInput.removeEventListener("change", handler));
        }

        if (this.queryRuleInput) {
            const handler = () => {
                void this.refreshQueryPolicies(true);
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
                void this.refreshQueryPolicies(true);
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
            if (this.isModalOpen) {
                void this.refreshQueryPolicies(true);
            }
        };
        document.addEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler);
        this.handlers.push(() => document.removeEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler));
    }

    async openModal() {
        if (!this.modalEl) return;
        this.modalEl.classList.add("active");
        this.modalEl.setAttribute("aria-hidden", "false");
        this.isModalOpen = true;
        await this.refreshQueryPolicies(true);
    }

    closeModal() {
        if (!this.modalEl) return;
        this.closeCardPickerModal();
        this.closeEditorModal();
        this.modalEl.classList.remove("active");
        this.modalEl.setAttribute("aria-hidden", "true");
        this.isModalOpen = false;
    }

    openEditorModal() {
        if (!this.editorModalEl) return;
        this.editorModalEl.hidden = false;
        this.editorModalEl.setAttribute("aria-hidden", "false");
        this.editorModalEl.style.display = "flex";
        this.isEditorOpen = true;
    }

    closeEditorModal() {
        if (!this.editorModalEl) return;
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
        const id = toInt(pickValue(card, "id", "Id"), 0);
        if (id <= 0) return null;

        const name = String(pickValue(card, "name", "Name") || `卡牌 #${id}`);
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
        const source = Array.isArray(items) ? items : [];
        if (this.collectibleCardIdSet.size <= 0) {
            return source;
        }
        return source.filter((item) => this.collectibleCardIdSet.has(item.cardDefinitionId));
    }

    async ensureCardIndexLoaded() {
        if (this.cardIndexLoaded) return;

        const response = await fetch(CARDS_ENDPOINT, { method: "GET" });
        let payload = null;
        try {
            payload = await response.json();
        } catch {
            payload = null;
        }

        if (!response.ok) {
            const message = payload?.message || payload?.Message || response.statusText;
            throw new Error(message || `HTTP ${response.status}`);
        }

        const cards = Array.isArray(payload) ? payload : [];
        this.cardNameById.clear();
        this.collectibleCardIdSet.clear();
        this.cardEntries = cards
            .filter((item) => this.isCollectibleCard(item))
            .map((item) => this.normalizeCardEntry(item))
            .filter((item) => item != null)
            .sort((left, right) => left.id - right.id);

        this.cardEntries.forEach((entry) => {
            this.cardNameById.set(entry.id, entry.name);
            this.collectibleCardIdSet.add(entry.id);
        });

        this.cardIndexLoaded = true;
    }

    async verifyAdminAccess(session) {
        if (!session?.userId) return false;

        const params = new URLSearchParams();
        params.set("actorUserId", session.userId);
        params.set("limit", "1");

        const response = await fetch(`${USERS_ENDPOINT}?${params.toString()}`, { method: "GET" });
        if (response.status === 403) {
            return false;
        }

        let payload = null;
        try {
            payload = await response.json();
        } catch {
            payload = null;
        }

        if (!response.ok) {
            const message = payload?.message || payload?.Message || response.statusText;
            throw new Error(message || `HTTP ${response.status}`);
        }

        return true;
    }

    normalizePolicyItems(payload) {
        const source = Array.isArray(payload?.items)
            ? payload.items
            : (Array.isArray(payload?.Items) ? payload.Items : []);

        return source
            .map((item) => {
                const cardDefinitionId = toInt(pickValue(item, "cardDefinitionId", "CardDefinitionId"), 0);
                if (cardDefinitionId <= 0) return null;

                const maxCopies = normalizeLimit(pickValue(item, "maxCopies", "MaxCopies"));
                const updatedAtUtc = String(pickValue(item, "updatedAtUtc", "UpdatedAtUtc") || "");
                return { cardDefinitionId, maxCopies, updatedAtUtc };
            })
            .filter((item) => item != null)
            .sort((left, right) => left.cardDefinitionId - right.cardDefinitionId);
    }

    async fetchPolicies(environment, rule) {
        const params = new URLSearchParams();
        params.set("environment", environment);
        params.set("rule", rule);

        const response = await fetch(`${POLICY_ENDPOINT}?${params.toString()}`, { method: "GET" });
        let payload = null;
        try {
            payload = await response.json();
        } catch {
            payload = null;
        }

        if (!response.ok) {
            const message = payload?.message || payload?.Message || response.statusText;
            throw new Error(message || `HTTP ${response.status}`);
        }

        return this.normalizePolicyItems(payload);
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

    async refreshQueryPolicies(force = false) {
        if (!this.isModalOpen && !force) return;

        const session = getUserSession();
        if (!this.isLoggedInSession(session)) {
            this.queryItems = [];
            this.renderQueryRows([]);
            this.setAdminPanelVisible(false);
            this.closeCardPickerModal();
            this.closeEditorModal();
            this.setMessage("请先登录后再访问禁限卡牌管理。", true);
            return;
        }

        this.setQueryBusy(true);
        this.setMessage("正在加载禁限卡牌...");

        try {
            const isAdmin = await this.verifyAdminAccess(session);
            if (!isAdmin) {
                this.queryItems = [];
                this.renderQueryRows([]);
                this.setAdminPanelVisible(false);
                this.closeCardPickerModal();
                this.closeEditorModal();
                this.setMessage("仅管理员可访问禁限卡牌管理。", true);
                return;
            }

            await this.ensureCardIndexLoaded();
            const environment = this.getQueryEnvironment();
            const rule = this.getQueryRule();
            this.queryItems = this.filterCollectiblePolicyItems(await this.fetchPolicies(environment, rule));

            this.renderQueryRows(this.queryItems);
            this.setAdminPanelVisible(true);
            this.setMessage(`已加载 ${this.queryItems.length} 条规则。`);
        } catch (error) {
            this.queryItems = [];
            this.renderQueryRows([]);
            this.setAdminPanelVisible(false);
            this.setMessage(`加载失败：${error?.message || String(error)}`, true);
        } finally {
            this.setQueryBusy(false);
        }
    }

    async upsertItems(environment, rule, items) {
        const session = getUserSession();
        if (!this.isLoggedInSession(session)) {
            throw new Error("请先登录。");
        }

        const body = {
            actorUserId: session.userId,
            environment,
            rule,
            items
        };

        const response = await fetch(POLICY_ENDPOINT, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body)
        });

        let payload = null;
        try {
            payload = await response.json();
        } catch {
            payload = null;
        }

        if (!response.ok) {
            const message = payload?.message || payload?.Message || response.statusText;
            throw new Error(message || `HTTP ${response.status}`);
        }

        return this.normalizePolicyItems(payload);
    }

    async deleteItem(environment, rule, cardDefinitionId) {
        const session = getUserSession();
        if (!this.isLoggedInSession(session)) {
            throw new Error("请先登录。");
        }

        const params = new URLSearchParams();
        params.set("actorUserId", session.userId);
        params.set("environment", environment);
        params.set("rule", rule);
        params.set("cardDefinitionId", String(cardDefinitionId));

        const response = await fetch(`${POLICY_ENDPOINT}?${params.toString()}`, { method: "DELETE" });
        if (!response.ok) {
            let payload = null;
            try {
                payload = await response.json();
            } catch {
                payload = null;
            }
            const message = payload?.message || payload?.Message || response.statusText;
            throw new Error(message || `HTTP ${response.status}`);
        }
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

    async openCardPickerModal() {
        if (!this.isEditorOpen) return;
        if (this.editorMode === "edit") return;

        await this.ensureCardIndexLoaded();

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
            this.setMessage(`保存成功：${this.getCardName(cardDefinitionId)} 上限 ${maxCopies}。`);
            this.closeEditorModal();
            this.notifyDeckValidationRulesUpdated(environment, rule);

            if (environment === this.getQueryEnvironment() && rule === this.getQueryRule()) {
                this.queryItems = this.filterCollectiblePolicyItems(items);
                this.renderQueryRows(this.queryItems);
            }
        } catch (error) {
            this.setEditorSummary(`保存失败：${error?.message || String(error)}`, true);
            this.setMessage(`保存失败：${error?.message || String(error)}`, true);
        } finally {
            this.setEditorBusy(false);
        }
    }

    async deletePolicyForCard(cardDefinitionId, button) {
        const confirmed = window.confirm(`确认删除卡牌 ${cardDefinitionId} 的规则并恢复默认上限吗？`);
        if (!confirmed) return;

        const environment = this.getQueryEnvironment();
        const rule = this.getQueryRule();

        button?.setAttribute("disabled", "disabled");
        this.setMessage(`正在删除 ${this.getCardName(cardDefinitionId)} 的规则...`);
        try {
            await this.deleteItem(environment, rule, cardDefinitionId);
            this.setMessage(`已删除 ${this.getCardName(cardDefinitionId)} 的规则。`);
            this.notifyDeckValidationRulesUpdated(environment, rule);
            this.queryItems = this.filterCollectiblePolicyItems(await this.fetchPolicies(environment, rule));
            this.renderQueryRows(this.queryItems);
        } catch (error) {
            this.setMessage(`删除失败：${error?.message || String(error)}`, true);
        } finally {
            button?.removeAttribute("disabled");
        }
    }

    async dispose() {
        this.closeModal();
        this.handlers.forEach((remove) => remove());
        this.handlers = [];
        this.queryItems = [];
        this.cardEntries = [];
        this.cardNameById.clear();
        this.initialized = false;
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
