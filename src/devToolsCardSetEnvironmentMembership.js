import { getUserSession, USER_SESSION_CHANGED_EVENT } from "./userSession.js";
import {
    ensureDeckEnvironmentOptions,
    fillDeckEnvironmentSelect,
    getDefaultDeckEnvironment,
    normalizeDeckEnvironment as normalizeDeckEnvironmentShared
} from "./deckEnvironmentOptions.js";
import {
    notifyCardCatalogInvalidated,
    notifyDeckValidationRulesUpdated
} from "./dataChangeEvents.js";

const USERS_ENDPOINT = "/api/users";
const CARD_SET_ENV_ENDPOINT = "/api/cardsets/environment-membership";

function pickValue(source, camelKey, pascalKey) {
    if (!source || typeof source !== "object") return null;
    if (source[camelKey] != null) return source[camelKey];
    if (source[pascalKey] != null) return source[pascalKey];
    return null;
}

function normalizeEnvironment(value) {
    return normalizeDeckEnvironmentShared(value, getDefaultDeckEnvironment());
}

function toInt(value, fallback = 0) {
    const parsed = Number.parseInt(String(value ?? ""), 10);
    return Number.isFinite(parsed) ? parsed : fallback;
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

class DevToolsCardSetEnvironmentMembership {
    constructor() {
        this.initialized = false;
        this.handlers = [];
        this.isModalOpen = false;
        this.items = [];
        this.includedIdSet = new Set();
    }

    async init() {
        if (this.initialized) return;
        this.initialized = true;

        this.root = document.getElementById("devtools-cardset-env-panel");
        this.modalEl = document.getElementById("devtools-cardset-env-modal");
        this.openBtn = document.getElementById("devtools-cardset-env-open-btn");
        this.closeBtn = document.getElementById("devtools-cardset-env-close-btn");
        this.messageEl = document.getElementById("devtools-cardset-env-message");
        this.panel = document.getElementById("devtools-cardset-env-management-panel");
        this.environmentInput = document.getElementById("devtools-cardset-env-environment");
        this.searchInput = document.getElementById("devtools-cardset-env-search");
        this.refreshBtn = document.getElementById("devtools-cardset-env-refresh-btn");
        this.saveBtn = document.getElementById("devtools-cardset-env-save-btn");
        this.countEl = document.getElementById("devtools-cardset-env-count");
        this.tableBody = document.getElementById("devtools-cardset-env-table-body");

        if (!this.root || !this.modalEl || !this.openBtn || !this.closeBtn || !this.messageEl || !this.panel || !this.tableBody) {
            return;
        }

        await this.loadEnvironmentOptions();
        this.bindUI();
        this.closeModal();
    }

    async loadEnvironmentOptions() {
        await ensureDeckEnvironmentOptions();
        fillDeckEnvironmentSelect(this.environmentInput, this.environmentInput?.value);
    }

    bindUI() {
        if (this.openBtn) {
            const handler = () => void this.openModal();
            this.openBtn.addEventListener("click", handler);
            this.handlers.push(() => this.openBtn.removeEventListener("click", handler));
        }

        if (this.closeBtn) {
            const handler = () => this.closeModal();
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
            if (event.key === "Escape" && this.isModalOpen) {
                this.closeModal();
            }
        };
        document.addEventListener("keydown", escHandler);
        this.handlers.push(() => document.removeEventListener("keydown", escHandler));

        if (this.environmentInput) {
            const handler = () => void this.refreshData(true);
            this.environmentInput.addEventListener("change", handler);
            this.handlers.push(() => this.environmentInput.removeEventListener("change", handler));
        }

        if (this.searchInput) {
            const handler = () => this.renderRows(this.items);
            this.searchInput.addEventListener("input", handler);
            this.handlers.push(() => this.searchInput.removeEventListener("input", handler));
        }

        if (this.refreshBtn) {
            const handler = () => void this.refreshData(true);
            this.refreshBtn.addEventListener("click", handler);
            this.handlers.push(() => this.refreshBtn.removeEventListener("click", handler));
        }

        if (this.saveBtn) {
            const handler = () => void this.saveMembership();
            this.saveBtn.addEventListener("click", handler);
            this.handlers.push(() => this.saveBtn.removeEventListener("click", handler));
        }

        if (this.tableBody) {
            const handler = (event) => {
                const target = event.target;
                const checkbox = target instanceof HTMLInputElement && target.matches("input[data-card-set-id]")
                    ? target
                    : null;
                if (!checkbox) return;
                const id = toInt(checkbox.getAttribute("data-card-set-id"), 0);
                if (id <= 0) return;
                if (checkbox.checked) {
                    this.includedIdSet.add(id);
                } else {
                    this.includedIdSet.delete(id);
                }
            };
            this.tableBody.addEventListener("change", handler);
            this.handlers.push(() => this.tableBody.removeEventListener("change", handler));
        }

        const sessionHandler = () => {
            if (this.isModalOpen) {
                void this.refreshData(true);
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
        await this.refreshData(true);
    }

    closeModal() {
        if (!this.modalEl) return;
        this.modalEl.classList.remove("active");
        this.modalEl.setAttribute("aria-hidden", "true");
        this.isModalOpen = false;
    }

    setBusy(busy) {
        const disabled = Boolean(busy);
        if (this.environmentInput) this.environmentInput.disabled = disabled;
        if (this.searchInput) this.searchInput.disabled = disabled;
        if (this.refreshBtn) this.refreshBtn.disabled = disabled;
        if (this.saveBtn) this.saveBtn.disabled = disabled;
    }

    setAdminPanelVisible(visible) {
        if (!this.panel || !this.root) return;
        this.panel.hidden = !visible;
        this.root.classList.toggle("is-admin", visible);
    }

    setMessage(text, isError = false) {
        if (!this.messageEl) return;
        this.messageEl.textContent = text || "";
        this.messageEl.dataset.tone = isError ? "error" : "neutral";
    }

    getEnvironment() {
        return normalizeEnvironment(this.environmentInput?.value || getDefaultDeckEnvironment());
    }

    getKeyword() {
        return String(this.searchInput?.value || "").trim().toLowerCase();
    }

    isLoggedInSession(session) {
        return Boolean(session && session.mode === "user" && session.userId);
    }

    async canManage(actorUserId) {
        if (!actorUserId) return false;
        const params = new URLSearchParams();
        params.set("actorUserId", actorUserId);
        params.set("limit", "1");

        const response = await fetch(`${USERS_ENDPOINT}?${params.toString()}`, { method: "GET" });
        if (response.status === 403) return false;
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
        return true;
    }

    normalizeItems(payload) {
        const source = Array.isArray(payload?.items)
            ? payload.items
            : (Array.isArray(payload?.Items) ? payload.Items : []);

        return source
            .map((item) => {
                const cardSetDefinitionId = toInt(pickValue(item, "cardSetDefinitionId", "CardSetDefinitionId"), 0);
                if (cardSetDefinitionId <= 0) return null;
                return {
                    cardSetDefinitionId,
                    name: String(pickValue(item, "name", "Name") || `CardSet #${cardSetDefinitionId}`),
                    abbreviation: String(pickValue(item, "abbreviation", "Abbreviation") || ""),
                    setCode: String(pickValue(item, "setCode", "SetCode") || ""),
                    included: Boolean(pickValue(item, "included", "Included"))
                };
            })
            .filter((item) => item != null)
            .sort((a, b) => a.cardSetDefinitionId - b.cardSetDefinitionId);
    }

    async fetchMembership(environment) {
        const params = new URLSearchParams();
        params.set("environment", environment);
        const response = await fetch(`${CARD_SET_ENV_ENDPOINT}?${params.toString()}`, { method: "GET" });
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

        return this.normalizeItems(payload);
    }

    renderRows(items) {
        if (!this.tableBody) return;
        const keyword = this.getKeyword();
        const source = Array.isArray(items) ? items : [];
        const filtered = source.filter((item) => {
            if (!keyword) return true;
            return item.name.toLowerCase().includes(keyword)
                || item.abbreviation.toLowerCase().includes(keyword)
                || item.setCode.toLowerCase().includes(keyword)
                || String(item.cardSetDefinitionId).includes(keyword);
        });

        this.tableBody.innerHTML = "";
        if (this.countEl) this.countEl.textContent = String(filtered.length);

        if (filtered.length === 0) {
            const row = document.createElement("tr");
            row.innerHTML = '<td colspan="5" class="devtools-copy-limit-empty">暂无卡包数据</td>';
            this.tableBody.appendChild(row);
            return;
        }

        const html = filtered.map((item) => `
            <tr>
                <td>
                    <input type="checkbox" data-card-set-id="${item.cardSetDefinitionId}" ${this.includedIdSet.has(item.cardSetDefinitionId) ? "checked" : ""}>
                </td>
                <td>${item.cardSetDefinitionId}</td>
                <td>${escapeHtml(item.setCode)}</td>
                <td>${escapeHtml(item.name)}</td>
                <td>${escapeHtml(item.abbreviation)}</td>
            </tr>
        `).join("");
        this.tableBody.innerHTML = html;
    }

    async refreshData(force = false) {
        if (!this.isModalOpen && !force) return;

        const session = getUserSession();
        if (!this.isLoggedInSession(session)) {
            this.items = [];
            this.includedIdSet.clear();
            this.renderRows([]);
            this.setAdminPanelVisible(false);
            this.setMessage("请先登录后再访问环境卡包配置。", true);
            return;
        }

        this.setBusy(true);
        this.setMessage("正在加载环境卡包配置...");

        try {
            const isAdmin = await this.canManage(session.userId);
            if (!isAdmin) {
                this.items = [];
                this.includedIdSet.clear();
                this.renderRows([]);
                this.setAdminPanelVisible(false);
                this.setMessage("仅管理员可访问环境卡包配置。", true);
                return;
            }

            const environment = this.getEnvironment();
            this.items = await this.fetchMembership(environment);
            this.includedIdSet = new Set(this.items.filter((item) => item.included).map((item) => item.cardSetDefinitionId));
            this.renderRows(this.items);
            this.setAdminPanelVisible(true);
            this.setMessage(`已加载 ${this.items.length} 个卡包。`);
        } catch (error) {
            this.items = [];
            this.includedIdSet.clear();
            this.renderRows([]);
            this.setAdminPanelVisible(false);
            this.setMessage(`加载失败：${error?.message || String(error)}`, true);
        } finally {
            this.setBusy(false);
        }
    }

    async saveMembership() {
        const session = getUserSession();
        if (!this.isLoggedInSession(session)) {
            this.setMessage("请先登录后再保存。", true);
            return;
        }

        const environment = this.getEnvironment();
        const includedCardSetDefinitionIds = Array.from(this.includedIdSet.values()).sort((a, b) => a - b);
        const body = {
            actorUserId: session.userId,
            environment,
            includedCardSetDefinitionIds
        };

        this.setBusy(true);
        this.setMessage("正在保存环境卡包配置...");
        try {
            const response = await fetch(CARD_SET_ENV_ENDPOINT, {
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

            this.items = this.normalizeItems(payload);
            this.includedIdSet = new Set(this.items.filter((item) => item.included).map((item) => item.cardSetDefinitionId));
            this.renderRows(this.items);
            this.setMessage(`保存成功：当前环境包含 ${this.includedIdSet.size} 个卡包。`);

            notifyDeckValidationRulesUpdated({ environment });
            notifyCardCatalogInvalidated({
                reason: "card-set-environment-membership-updated",
                environment
            });
        } catch (error) {
            this.setMessage(`保存失败：${error?.message || String(error)}`, true);
        } finally {
            this.setBusy(false);
        }
    }

    async dispose() {
        this.closeModal();
        this.handlers.forEach((remove) => remove());
        this.handlers = [];
        this.items = [];
        this.includedIdSet.clear();
        this.initialized = false;
    }
}

let instance = null;

export async function initializeDevToolsCardSetEnvironmentMembership() {
    if (!instance) {
        instance = new DevToolsCardSetEnvironmentMembership();
    }
    await instance.init();
}

export async function cleanupDevToolsCardSetEnvironmentMembership() {
    if (!instance) return;
    await instance.dispose();
    instance = null;
}
