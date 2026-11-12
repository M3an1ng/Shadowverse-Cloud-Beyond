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
export const CARD_SETS_UPDATED_EVENT = "cardsets:updated";

function pickValue(source, camelKey, pascalKey) {
    if (!source || typeof source !== "object") return null;
    if (source[camelKey] != null) return source[camelKey];
    if (source[pascalKey] != null) return source[pascalKey];
    return null;
}

function normalizeEnvironment(value) {
    return normalizeDeckEnvironmentShared(value, getDefaultDeckEnvironment());
}

async function readJsonResponse(response) {
    if (response.ok) return response.json();
    const rawText = await response.text();
    let message = rawText.trim();
    try {
        const payload = JSON.parse(rawText);
        message = payload?.message || payload?.Message || (typeof payload === "string" ? payload : message);
    } catch {
        // Error responses may be plain text; successful responses must be JSON.
    }
    throw new Error(message || response.statusText || `HTTP ${response.status}`);
}

function getIncludedCardSetIds(items) {
    return items.filter(item => item.included).map(item => item.cardSetDefinitionId).sort((a, b) => a - b);
}

function sameCardSetIds(left, right) {
    return left.length === right.length && left.every((id, index) => id === right[index]);
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
        this.requestOwner = {};
        this.membershipRequest = null;
        this.isAdmin = false;
        this.busy = false;
        this.highlightSetName = "";
        this.uploadSetId = null;
        this.initializationTask = null;
    }

    async init() {
        if (this.initialized) return;
        this.initialized = true;

        this.modalEl = document.getElementById("devtools-cardset-env-modal");
        this.closeBtn = document.getElementById("devtools-cardset-env-close-btn");
        this.messageEl = document.getElementById("devtools-cardset-env-message");
        this.environmentInput = document.getElementById("devtools-cardset-env-environment");
        this.searchInput = document.getElementById("devtools-cardset-env-search");
        this.logoFilter = document.getElementById("devtools-cardset-logo-filter");
        this.logoInput = document.getElementById("devtools-cardset-logo-input");
        this.createRow = document.getElementById("devtools-cardset-create-row");
        this.newNameInput = document.getElementById("devtools-cardset-new-name");
        this.newAbbreviationInput = document.getElementById("devtools-cardset-new-abbr");
        this.createBtn = document.getElementById("devtools-cardset-create-btn");
        this.refreshBtn = document.getElementById("devtools-cardset-env-refresh-btn");
        this.saveBtn = document.getElementById("devtools-cardset-env-save-btn");
        this.countEl = document.getElementById("devtools-cardset-env-count");
        this.tableBody = document.getElementById("devtools-cardset-env-table-body");

        await this.loadEnvironmentOptions();
        if (!this.initialized) return;
        this.bindUI();
        this.closeModal();
    }

    async loadEnvironmentOptions() {
        await ensureDeckEnvironmentOptions();
        if (!this.initialized) return;
        fillDeckEnvironmentSelect(this.environmentInput, this.environmentInput?.value);
    }

    bindUI() {
        const openHandler = (event) => {
            if (event.target.closest('#card-editor-pack-manager-btn')) {
                void this.openModal();
            }
        };
        document.addEventListener("click", openHandler);
        this.handlers.push(() => document.removeEventListener("click", openHandler));

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
            const handler = () => void this.refreshData();
            this.environmentInput.addEventListener("change", handler);
            this.handlers.push(() => this.environmentInput.removeEventListener("change", handler));
        }

        if (this.searchInput) {
            const handler = () => this.renderRows(this.items);
            this.searchInput.addEventListener("input", handler);
            this.handlers.push(() => this.searchInput.removeEventListener("input", handler));
        }

        const logoFilterHandler = () => this.renderRows(this.items);
        this.logoFilter.addEventListener("change", logoFilterHandler);
        this.handlers.push(() => this.logoFilter.removeEventListener("change", logoFilterHandler));

        const createHandler = () => void this.createCardSet();
        this.createBtn.addEventListener("click", createHandler);
        this.handlers.push(() => this.createBtn.removeEventListener("click", createHandler));

        const logoHandler = () => {
            const file = this.logoInput.files[0];
            if (file) void this.uploadLogo(file);
        };
        this.logoInput.addEventListener("change", logoHandler);
        this.handlers.push(() => this.logoInput.removeEventListener("change", logoHandler));

        const uploadHandler = (event) => {
            const button = event.target.closest("[data-upload-logo-id]");
            if (!button || !this.isAdmin || this.busy) return;
            this.uploadSetId = Number(button.dataset.uploadLogoId);
            this.logoInput.value = "";
            this.logoInput.click();
        };
        this.tableBody.addEventListener("click", uploadHandler);
        this.handlers.push(() => this.tableBody.removeEventListener("click", uploadHandler));

        if (this.refreshBtn) {
            const handler = () => void this.refreshData();
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
                if (!checkbox || !this.isAdmin || this.busy) return;
                const id = Number(checkbox.getAttribute("data-card-set-id"));
                // Included flags are the editable selection, also for rows hidden by filtering.
                this.items.find(item => item.cardSetDefinitionId === id).included = checkbox.checked;
                if (this.logoFilter.value === "included") this.renderRows(this.items);
            };
            this.tableBody.addEventListener("change", handler);
            this.handlers.push(() => this.tableBody.removeEventListener("change", handler));
        }

        const sessionHandler = () => {
            this.invalidateRequests();
            this.clearItems();
            this.setMessage("");
            if (this.isModalOpen) {
                void this.refreshData();
            }
        };
        document.addEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler);
        this.handlers.push(() => document.removeEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler));
    }

    async openModal({ highlightSetName = "" } = {}) {
        if (!this.modalEl) return;
        this.invalidateRequests();
        this.clearItems();
        this.highlightSetName = highlightSetName.trim().toLowerCase();
        this.searchInput.value = "";
        this.logoFilter.value = "";
        this.modalEl.classList.add("active");
        this.modalEl.setAttribute("aria-hidden", "false");
        this.isModalOpen = true;
        await this.refreshData();
    }

    closeModal() {
        if (!this.modalEl) return;
        this.invalidateRequests();
        this.clearItems();
        this.setMessage("");
        this.modalEl.classList.remove("active");
        this.modalEl.setAttribute("aria-hidden", "true");
        this.isModalOpen = false;
    }

    invalidateRequests() {
        this.requestOwner = {};
        this.membershipRequest?.abort();
        this.membershipRequest = null;
        this.uploadSetId = null;
        this.setBusy(false);
    }

    ownsRequest(owner) {
        return this.initialized && this.isModalOpen && this.requestOwner === owner;
    }

    clearItems() {
        this.items = [];
        this.setAdminPermission(false);
        this.renderRows([]);
    }

    setBusy(busy) {
        this.busy = busy;
        const disabled = Boolean(busy);
        if (this.environmentInput) this.environmentInput.disabled = disabled;
        if (this.searchInput) this.searchInput.disabled = disabled;
        if (this.refreshBtn) this.refreshBtn.disabled = disabled;
        if (this.logoFilter) this.logoFilter.disabled = disabled;
        if (this.saveBtn) this.saveBtn.disabled = disabled || !this.isAdmin;
        if (this.newNameInput) this.newNameInput.disabled = disabled || !this.isAdmin;
        if (this.newAbbreviationInput) this.newAbbreviationInput.disabled = disabled || !this.isAdmin;
        if (this.createBtn) this.createBtn.disabled = disabled || !this.isAdmin;
        this.tableBody?.querySelectorAll("input[data-card-set-id], button[data-upload-logo-id]").forEach(control => {
            control.disabled = disabled || !this.isAdmin;
        });
    }

    setAdminPermission(isAdmin) {
        this.isAdmin = isAdmin;
        this.saveBtn.hidden = !isAdmin;
        this.createRow.hidden = !isAdmin;
        this.setBusy(this.busy);
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

    async canManage(actorUserId, signal) {
        const params = new URLSearchParams();
        params.set("actorUserId", actorUserId);
        params.set("limit", "1");
        const response = await fetch(`${USERS_ENDPOINT}?${params.toString()}`, { method: "GET", signal });
        if (response.status === 403) return false;
        const payload = await readJsonResponse(response);
        if (!Array.isArray(pickValue(payload, "items", "Items"))) {
            throw new Error("管理员查询响应中的 items 必须为数组。");
        }
        return true;
    }

    normalizeItems(payload, environment) {
        const responseEnvironment = pickValue(payload, "environment", "Environment");
        if (typeof responseEnvironment !== "string" || responseEnvironment.toLowerCase() !== environment.toLowerCase()) {
            throw new Error("卡包响应的环境与请求不符。");
        }
        const items = pickValue(payload, "items", "Items");
        if (!Array.isArray(items)) throw new Error("卡包响应中的 items 必须为数组。");
        const ids = new Set();
        return items.map(item => {
            const cardSetDefinitionId = pickValue(item, "cardSetDefinitionId", "CardSetDefinitionId");
            const name = pickValue(item, "name", "Name");
            const abbreviation = pickValue(item, "abbreviation", "Abbreviation");
            const setCode = pickValue(item, "setCode", "SetCode");
            const included = pickValue(item, "included", "Included");
            const logoUrl = item.logoUrl ?? item.logo_url ?? item.LogoUrl ?? "";
            if (!Number.isInteger(cardSetDefinitionId) || cardSetDefinitionId <= 0 || ids.has(cardSetDefinitionId)) {
                throw new Error("卡包响应中的 ID 必须为不重复的正整数。");
            }
            if (typeof name !== "string" || !name.trim()
                || (abbreviation != null && typeof abbreviation !== "string")
                || (setCode != null && typeof setCode !== "string")) {
                throw new Error("卡包响应中的名称、简称或代码无效。");
            }
            if (typeof included !== "boolean") throw new Error("卡包响应中的 included 必须为布尔值。");
            ids.add(cardSetDefinitionId);
            return { cardSetDefinitionId, name, abbreviation: abbreviation ?? "", setCode: setCode ?? "", included, logoUrl };
        }).sort((a, b) => a.cardSetDefinitionId - b.cardSetDefinitionId);
    }

    async fetchMembership(environment, signal) {
        const params = new URLSearchParams();
        params.set("environment", environment);
        const response = await fetch(`${CARD_SET_ENV_ENDPOINT}?${params.toString()}`, { method: "GET", signal });
        return this.normalizeItems(await readJsonResponse(response), environment);
    }

    renderRows(items) {
        if (!this.tableBody) return;
        const keyword = this.getKeyword();
        const logoFilter = this.logoFilter.value;
        const filtered = items.filter((item) => {
            if (logoFilter === "with-logo" && !item.logoUrl) return false;
            if (logoFilter === "without-logo" && item.logoUrl) return false;
            if (logoFilter === "included" && !item.included) return false;
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
            row.innerHTML = '<td colspan="7" class="devtools-copy-limit-empty">暂无匹配的卡包</td>';
            this.tableBody.appendChild(row);
            return;
        }

        const html = filtered.map((item) => `
            <tr class="${item.name.trim().toLowerCase() === this.highlightSetName ? 'pack-manager-highlighted' : ''}">
                <td>
                    <input type="checkbox" data-card-set-id="${item.cardSetDefinitionId}" ${item.included ? "checked" : ""} ${this.isAdmin && !this.busy ? '' : 'disabled'} aria-label="${escapeHtml(item.name)}：当前环境包含">
                </td>
                <td>${item.cardSetDefinitionId}</td>
                <td>${escapeHtml(item.setCode)}</td>
                <td>${escapeHtml(item.name)}</td>
                <td>${escapeHtml(item.abbreviation)}</td>
                <td><div class="pack-manager-logo-box">${item.logoUrl
                    ? `<img src="${escapeHtml(item.logoUrl)}" class="pack-manager-logo-thumb" alt="${escapeHtml(item.name)}">`
                    : '<span class="pack-manager-logo-empty">未设置</span>'}</div></td>
                <td><button type="button" class="btn-tech btn-small" data-upload-logo-id="${item.cardSetDefinitionId}" ${this.isAdmin ? (this.busy ? 'disabled' : '') : 'disabled title="需管理员权限"'}>${item.logoUrl ? '更换Logo' : '上传Logo'}</button></td>
            </tr>
        `).join("");
        this.tableBody.innerHTML = html;
    }

    async refreshData() {
        if (!this.isModalOpen) return;
        this.membershipRequest?.abort();
        const request = new AbortController();
        // The retained controller also identifies the loaded selection when a save returns.
        this.membershipRequest = request;
        const owner = this.requestOwner;
        const ownsLoad = () => this.ownsRequest(owner) && this.membershipRequest === request;
        const environment = this.getEnvironment();
        const session = getUserSession();
        // A reload replaces the draft; do not leave its outgoing rows editable while waiting.
        this.clearItems();
        this.setBusy(true);
        this.setMessage("正在加载卡包...");
        try {
            const [isAdmin, items] = await Promise.all([
                this.isLoggedInSession(session) ? this.canManage(session.userId, request.signal) : false,
                this.fetchMembership(environment, request.signal)
            ]);
            if (!ownsLoad()) return;
            this.setAdminPermission(isAdmin);
            this.items = items;
            this.renderRows(this.items);
            this.tableBody.querySelector('.pack-manager-highlighted')?.scrollIntoView({ block: "nearest" });
            this.setMessage(this.isAdmin
                ? `管理员模式：已加载 ${this.items.length} 个卡包，可勾选环境配置、上传卡包 Logo 或新建卡包。`
                : `检视模式：已加载 ${this.items.length} 个卡包（修改环境卡包配置与上传 Logo 需管理员权限）。`);
        } catch (error) {
            if (!ownsLoad()) return;
            this.clearItems();
            this.setMessage(`加载失败：${error?.message || String(error)}`, true);
        } finally {
            if (ownsLoad()) this.setBusy(false);
        }
    }

    async saveMembership() {
        if (!this.isModalOpen || !this.isAdmin || this.busy) return;
        const owner = this.requestOwner;
        const request = this.membershipRequest;
        const ownsSave = () => this.ownsRequest(owner) && this.membershipRequest === request;
        const session = getUserSession();
        if (!this.isLoggedInSession(session)) {
            this.setMessage("请先登录后再保存。", true);
            return;
        }
        const environment = this.getEnvironment();
        const includedCardSetDefinitionIds = getIncludedCardSetIds(this.items);
        const knownCardSetIds = this.items.map(item => item.cardSetDefinitionId);
        this.setBusy(true);
        this.setMessage("正在保存环境卡包配置...");
        try {
            const response = await fetch(CARD_SET_ENV_ENDPOINT, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ actorUserId: session.userId, environment, includedCardSetDefinitionIds })
            });
            const items = this.normalizeItems(await readJsonResponse(response), environment);
            if (!knownCardSetIds.every(id => items.some(item => item.cardSetDefinitionId === id))) {
                throw new Error("保存响应缺少已加载的卡包。");
            }
            if (!sameCardSetIds(getIncludedCardSetIds(items), includedCardSetDefinitionIds)) {
                throw new Error("保存响应与提交的卡包选择不一致。");
            }
            // Confirmed catalogue/rule changes must invalidate caches even after this view closes.
            notifyDeckValidationRulesUpdated({ environment });
            notifyCardCatalogInvalidated({ reason: "card-set-environment-membership-updated", environment });
            document.dispatchEvent(new CustomEvent(CARD_SETS_UPDATED_EVENT, { detail: { reason: "environment-membership", environment } }));
            if (!ownsSave()) return;
            this.items = items;
            this.renderRows(this.items);
            this.setMessage(`保存成功：当前环境包含 ${includedCardSetDefinitionIds.length} 个卡包。`);
        } catch (error) {
            if (!ownsSave()) return;
            this.setMessage(`保存失败：${error?.message || String(error)}`, true);
        } finally {
            if (ownsSave()) this.setBusy(false);
        }
    }

    async uploadLogo(file) {
        if (!this.isModalOpen || !this.isAdmin || this.busy) return;
        const owner = this.requestOwner;
        const request = this.membershipRequest;
        const ownsUpload = () => this.ownsRequest(owner) && this.membershipRequest === request;
        const item = this.items.find(entry => entry.cardSetDefinitionId === this.uploadSetId);
        const session = getUserSession();
        const form = new FormData();
        form.append("file", file);
        form.append("actorUserId", session.userId);
        this.setBusy(true);
        this.setMessage(`正在上传「${item.name}」的 Logo...`);
        try {
            const response = await fetch(`/api/cardsets/${item.cardSetDefinitionId}/logo`, { method: "POST", body: form });
            const updated = await readJsonResponse(response);
            document.dispatchEvent(new CustomEvent(CARD_SETS_UPDATED_EVENT, { detail: { reason: "logo", cardSet: updated } }));
            if (!ownsUpload()) return;
            item.logoUrl = updated.logo_url;
            this.renderRows(this.items);
            this.setMessage(`「${item.name}」的 Logo 已更新。`);
        } catch (error) {
            if (!ownsUpload()) return;
            this.setMessage(`Logo 上传失败：${error.message}`, true);
        } finally {
            if (ownsUpload()) this.setBusy(false);
        }
    }

    async createCardSet() {
        if (!this.isModalOpen || !this.isAdmin || this.busy) return;
        const name = this.newNameInput.value.trim();
        if (!name) {
            this.setMessage("请填写新卡包名称。", true);
            this.newNameInput.focus();
            return;
        }
        const owner = this.requestOwner;
        const request = this.membershipRequest;
        const ownsCreate = () => this.ownsRequest(owner) && this.membershipRequest === request;
        const environment = this.getEnvironment();
        const session = getUserSession();
        this.setBusy(true);
        this.setMessage("正在新建卡包...");
        let created = null;
        try {
            const response = await fetch("/api/cardsets", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ actorUserId: session.userId, name, abbreviation: this.newAbbreviationInput.value.trim() })
            });
            created = await readJsonResponse(response);
            document.dispatchEvent(new CustomEvent(CARD_SETS_UPDATED_EVENT, { detail: { reason: "created", cardSet: created } }));
            if (!ownsCreate()) return;
            this.newNameInput.value = "";
            this.newAbbreviationInput.value = "";
            const items = await this.fetchMembership(environment, request.signal);
            if (!ownsCreate()) return;
            // Adding a set must preserve unsaved membership choices for existing sets.
            for (const item of items) {
                const previous = this.items.find(entry => entry.cardSetDefinitionId === item.cardSetDefinitionId);
                if (previous) item.included = previous.included;
            }
            this.items = items;
            this.highlightSetName = created.name.trim().toLowerCase();
            this.searchInput.value = "";
            this.logoFilter.value = "";
            this.renderRows(this.items);
            this.tableBody.querySelector('.pack-manager-highlighted')?.scrollIntoView({ block: "nearest" });
            this.setMessage(`卡包「${created.name}」已创建，默认加入 Open 环境。`);
        } catch (error) {
            if (!ownsCreate()) return;
            this.setMessage(created ? `卡包已创建，但列表刷新失败：${error.message}` : `新建卡包失败：${error.message}`, true);
        } finally {
            if (ownsCreate()) this.setBusy(false);
        }
    }

    async dispose() {
        this.initialized = false;
        this.closeModal();
        this.handlers.forEach((remove) => remove());
        this.handlers = [];
    }
}

let instance = null;

export async function initializeDevToolsCardSetEnvironmentMembership() {
    if (!instance) {
        instance = new DevToolsCardSetEnvironmentMembership();
        instance.initializationTask = instance.init();
    }
    const manager = instance;
    await manager.initializationTask;
    return manager;
}

export async function openCardSetManagerModal(options = {}) {
    const manager = await initializeDevToolsCardSetEnvironmentMembership();
    if (manager.initialized) await manager.openModal(options);
}

export async function cleanupDevToolsCardSetEnvironmentMembership() {
    if (!instance) return;
    await instance.dispose();
    instance = null;
}
