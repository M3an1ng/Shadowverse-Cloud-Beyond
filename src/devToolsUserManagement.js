import { getUserSession, setUserSession, USER_SESSION_CHANGED_EVENT } from "./userSession.js";

const USERS_ENDPOINT = "/api/users";
const MIN_PASSWORD_LENGTH = 6;
const MANAGED_ROLES = [
    { value: "Player", label: "普通用户", level: 10 },
    { value: "Admin", label: "管理员", level: 100 }
];

function pickValue(source, camelKey, pascalKey) {
    if (!source || typeof source !== "object") return null;
    if (source[camelKey] != null) return source[camelKey];
    if (source[pascalKey] != null) return source[pascalKey];
    return null;
}

function normalizeRole(role) {
    const text = String(role || "").trim().toLowerCase();
    const managedRole = MANAGED_ROLES.find(item => item.value.toLowerCase() === text);
    if (!managedRole) throw new Error("用户响应中的 role 无效。");
    return managedRole.value;
}

function resolvePermissionLevel(role) {
    return MANAGED_ROLES.find(item => item.value === role).level;
}

function roleLabel(role) {
    return role === "Admin" ? "管理员" : "普通用户";
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

function readResponseUserId(payload, expectedUserId) {
    const userId = pickValue(payload, "userId", "UserId");
    if (typeof userId !== "string" || !userId.trim() || (expectedUserId && userId !== expectedUserId)) {
        throw new Error("用户响应缺少有效或匹配的 userId。");
    }
    return userId;
}

function readUserSummary(payload, expectedUserId) {
    const userId = readResponseUserId(payload, expectedUserId);
    const username = pickValue(payload, "username", "Username");
    const role = normalizeRole(pickValue(payload, "role", "Role"));
    const permissionLevel = pickValue(payload, "permissionLevel", "PermissionLevel");
    const coins = pickValue(payload, "coins", "Coins");
    const isActive = pickValue(payload, "isActive", "IsActive");
    if (typeof username !== "string" || !username.trim()) throw new Error("用户响应中的 username 无效。");
    if (permissionLevel !== resolvePermissionLevel(role)) throw new Error("用户响应中的 permissionLevel 与角色不符。");
    if (!Number.isInteger(coins) || coins < 0) throw new Error("用户响应中的 coins 必须为非负整数。");
    if (typeof isActive !== "boolean") throw new Error("用户响应中的 isActive 必须为布尔值。");
    return {
        userId, username, role, permissionLevel, coins, isActive,
        createdAt: pickValue(payload, "createdAt", "CreatedAt"),
        lastLoginAt: pickValue(payload, "lastLoginAt", "LastLoginAt")
    };
}

class DevToolsUserManagement {
    constructor() {
        this.initialized = false;
        this.handlers = [];
        this.currentUsers = [];
        this.isModalOpen = false;
        this.requestOwner = {};
        this.usersRequest = null;
    }

    async init() {
        if (this.initialized) return;
        this.initialized = true;

        this.root = document.getElementById("admin-user-management");
        this.modalEl = document.getElementById("admin-user-modal");
        this.openBtn = document.getElementById("admin-user-open-btn");
        this.closeBtn = document.getElementById("admin-user-close-btn");
        this.panel = document.getElementById("admin-user-management-panel");
        this.messageEl = document.getElementById("admin-user-management-message");
        this.searchInput = document.getElementById("admin-user-search");
        this.refreshBtn = document.getElementById("admin-user-refresh");
        this.searchBtn = document.getElementById("admin-user-search-btn");
        this.countEl = document.getElementById("admin-user-count");
        this.tableBody = document.getElementById("admin-user-table-body");

        if (!this.root || !this.modalEl || !this.openBtn || !this.closeBtn || !this.panel || !this.messageEl || !this.tableBody) return;

        this.bindUI();
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
            if (event.key === "Escape" && this.isModalOpen) {
                this.closeModal();
            }
        };
        document.addEventListener("keydown", escHandler);
        this.handlers.push(() => document.removeEventListener("keydown", escHandler));

        if (this.refreshBtn) {
            const handler = () => {
                void this.refreshUsers();
            };
            this.refreshBtn.addEventListener("click", handler);
            this.handlers.push(() => this.refreshBtn.removeEventListener("click", handler));
        }

        if (this.searchBtn) {
            const handler = () => {
                void this.refreshUsers();
            };
            this.searchBtn.addEventListener("click", handler);
            this.handlers.push(() => this.searchBtn.removeEventListener("click", handler));
        }

        if (this.searchInput) {
            const handler = (event) => {
                if (event.key !== "Enter") return;
                event.preventDefault();
                void this.refreshUsers();
            };
            this.searchInput.addEventListener("keydown", handler);
            this.handlers.push(() => this.searchInput.removeEventListener("keydown", handler));
        }

        if (this.tableBody) {
            const handler = (event) => {
                const target = event.target;
                const button = target instanceof Element ? target.closest("button[data-action]") : null;
                if (!button) return;
                const action = button.getAttribute("data-action");
                const userId = button.getAttribute("data-user-id");
                if (!userId) return;
                if (action === "save-role") {
                    void this.updateUserRole(userId, button);
                    return;
                }
                if (action === "set-coins") {
                    void this.updateUserCoins(userId, button);
                    return;
                }
                if (action === "reset-password") {
                    void this.resetUserPassword(userId, button);
                }
            };
            this.tableBody.addEventListener("click", handler);
            this.handlers.push(() => this.tableBody.removeEventListener("click", handler));
        }

        const sessionHandler = () => {
            this.invalidateRequests();
            this.clearUsers();
            this.setMessage("");
            if (this.isModalOpen) {
                void this.refreshUsers();
            }
        };
        document.addEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler);
        this.handlers.push(() => document.removeEventListener(USER_SESSION_CHANGED_EVENT, sessionHandler));
    }

    async openModal() {
        if (!this.modalEl) return;
        this.invalidateRequests();
        this.clearUsers();
        this.modalEl.classList.add("active");
        this.modalEl.setAttribute("aria-hidden", "false");
        this.isModalOpen = true;
        await this.refreshUsers();
    }

    closeModal() {
        if (!this.modalEl) return;
        this.invalidateRequests();
        this.clearUsers();
        this.setMessage("");
        this.modalEl.classList.remove("active");
        this.modalEl.setAttribute("aria-hidden", "true");
        this.isModalOpen = false;
    }

    invalidateRequests() {
        // A reopened modal or the same account after switching is a new owner.
        this.requestOwner = {};
        this.usersRequest?.abort();
        this.usersRequest = null;
        this.setBusy(false);
    }

    ownsRequest(owner) {
        return this.initialized && this.isModalOpen && this.requestOwner === owner;
    }

    clearUsers() {
        this.currentUsers = [];
        this.renderRows([]);
        this.setAdminPanelVisible(false);
    }

    setBusy(busy) {
        if (this.refreshBtn) this.refreshBtn.disabled = busy;
        if (this.searchBtn) this.searchBtn.disabled = busy;
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

    getSearchKeyword() {
        const keyword = this.searchInput?.value?.trim();
        return keyword || "";
    }

    async refreshUsers() {
        if (!this.isModalOpen) return;

        this.usersRequest?.abort();
        const request = new AbortController();
        this.usersRequest = request;
        const owner = this.requestOwner;
        const ownsListRequest = () => this.usersRequest === request && this.ownsRequest(owner);

        const session = getUserSession();
        if (session.mode !== "user" || !session.userId) {
            this.clearUsers();
            this.setMessage("请先登录后再访问用户管理。", true);
            return;
        }

        const params = new URLSearchParams();
        params.set("actorUserId", session.userId);
        params.set("limit", "100");
        const keyword = this.getSearchKeyword();
        if (keyword) {
            params.set("keyword", keyword);
        }

        this.setBusy(true);
        this.setMessage("正在加载用户列表...");
        try {
            const response = await fetch(`${USERS_ENDPOINT}?${params.toString()}`, { method: "GET", signal: request.signal });
            if (!ownsListRequest()) return;

            if (response.status === 403) {
                this.clearUsers();
                this.setMessage("仅管理员可访问用户管理。", true);
                return;
            }

            const payload = await readJsonResponse(response);
            if (!ownsListRequest()) return;
            const rows = pickValue(payload, "items", "Items");
            if (!Array.isArray(rows)) throw new Error("用户列表响应中的 items 必须为数组。");
            const items = rows.map(item => readUserSummary(item));

            this.currentUsers = items;
            this.renderRows(items);
            this.setAdminPanelVisible(true);
            this.setMessage(`已加载 ${items.length} 个用户。`);

            if (String(session.role || "").toLowerCase() !== "admin") {
                setUserSession({
                    userId: session.userId,
                    username: session.displayName,
                    avatar: session.avatar,
                    role: "Admin",
                    permissionLevel: 100
                });
            }
        } catch (error) {
            if (!ownsListRequest()) return;
            this.clearUsers();
            this.setMessage(`加载失败：${error?.message || String(error)}`, true);
        } finally {
            if (ownsListRequest()) {
                this.usersRequest = null;
                this.setBusy(false);
            }
        }
    }

    renderRows(items) {
        if (!this.tableBody) return;
        this.tableBody.innerHTML = "";
        if (this.countEl) {
            this.countEl.textContent = String(items.length);
        }

        const session = getUserSession();
        const currentUserId = session.mode === "user" ? session.userId : null;

        if (items.length === 0) {
            const emptyRow = document.createElement("tr");
            emptyRow.innerHTML = `<td colspan="9" class="admin-user-empty">暂无用户数据</td>`;
            this.tableBody.appendChild(emptyRow);
            return;
        }

        for (const item of items) {
            const { userId, username, role, permissionLevel, coins, createdAt, lastLoginAt, isActive } = item;
            const isCurrent = currentUserId && currentUserId === userId;

            const row = document.createElement("tr");
            row.innerHTML = `
                <td class="admin-user-cell-id" title="${userId}">${userId || "-"}</td>
                <td>${username}${isCurrent ? ' <span class="admin-user-self-tag">当前账号</span>' : ''}</td>
                <td>${roleLabel(role)}</td>
                <td>L${permissionLevel}</td>
                <td>${coins}</td>
                <td>${isActive ? "启用" : "禁用"}</td>
                <td>${createdAt ? new Date(createdAt).toLocaleString() : "-"}</td>
                <td>${lastLoginAt ? new Date(lastLoginAt).toLocaleString() : "-"}</td>
                <td>
                    <div class="admin-user-role-actions">
                        <select class="form-input admin-user-role-select" data-user-id="${userId}">
                            ${MANAGED_ROLES.map(roleItem => `
                                <option value="${roleItem.value}" ${roleItem.value === role ? "selected" : ""}>
                                    ${roleItem.label} (L${roleItem.level})
                                </option>
                            `).join("")}
                        </select>
                        <button class="tool-button admin-user-save-btn" type="button" data-action="save-role" data-user-id="${userId}">
                            保存
                        </button>
                        <button class="tool-button admin-user-set-coins-btn" type="button" data-action="set-coins" data-user-id="${userId}">
                            设置金币
                        </button>
                        <button class="tool-button admin-user-reset-btn" type="button" data-action="reset-password" data-user-id="${userId}">
                            重置密码
                        </button>
                    </div>
                </td>
            `;
            this.tableBody.appendChild(row);
        }
    }

    resolveUsername(targetUserId) {
        const item = this.currentUsers.find(user => user.userId === targetUserId);
        return item?.username ?? targetUserId;
    }

    resolveUserCoins(targetUserId) {
        return this.currentUsers.find(user => user.userId === targetUserId).coins;
    }

    collectNewPassword(targetUserId) {
        const username = this.resolveUsername(targetUserId);
        const newPassword = window.prompt(`请输入用户 ${username} 的新密码（至少 ${MIN_PASSWORD_LENGTH} 位）`);
        if (newPassword == null) return null;

        const normalizedPassword = String(newPassword).trim();
        if (normalizedPassword.length < MIN_PASSWORD_LENGTH) {
            this.setMessage(`密码长度不能小于 ${MIN_PASSWORD_LENGTH} 位。`, true);
            return null;
        }

        const confirmPassword = window.prompt(`请再次输入 ${username} 的新密码`);
        if (confirmPassword == null) return null;

        if (normalizedPassword !== String(confirmPassword).trim()) {
            this.setMessage("两次输入的新密码不一致。", true);
            return null;
        }

        return normalizedPassword;
    }

    collectNewCoins(targetUserId) {
        const username = this.resolveUsername(targetUserId);
        const oldCoins = this.resolveUserCoins(targetUserId);
        const rawValue = window.prompt(`请输入用户 ${username} 的金币数量（当前：${oldCoins}）`, String(oldCoins));
        if (rawValue == null) return null;

        const normalized = String(rawValue).trim();
        if (!/^\d+$/.test(normalized)) {
            this.setMessage("金币必须是非负整数。", true);
            return null;
        }

        const coins = Number.parseInt(normalized, 10);
        if (!Number.isFinite(coins) || coins < 0) {
            this.setMessage("金币必须是非负整数。", true);
            return null;
        }

        return { coins };
    }

    async resetUserPassword(targetUserId, button) {
        if (!this.isModalOpen) return;
        const owner = this.requestOwner;
        const session = getUserSession();
        if (session.mode !== "user" || !session.userId) return;

        const newPassword = this.collectNewPassword(targetUserId);
        if (!newPassword) return;

        button.setAttribute("disabled", "disabled");
        this.setMessage(`正在重置 ${targetUserId} 的密码...`);
        try {
            const response = await fetch(`/api/users/${encodeURIComponent(targetUserId)}/password`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    actorUserId: session.userId,
                    newPassword
                })
            });

            const payload = await readJsonResponse(response);
            if (!this.ownsRequest(owner)) return;
            readResponseUserId(payload, targetUserId);

            this.setMessage(`已重置 ${this.resolveUsername(targetUserId)} 的密码。`);
        } catch (error) {
            if (!this.ownsRequest(owner)) return;
            this.setMessage(`重置密码失败：${error?.message || String(error)}`, true);
        } finally {
            if (this.ownsRequest(owner)) button.removeAttribute("disabled");
        }
    }

    async updateUserCoins(targetUserId, button) {
        if (!this.isModalOpen) return;
        const owner = this.requestOwner;
        const session = getUserSession();
        if (session.mode !== "user" || !session.userId) return;

        const payloadInput = this.collectNewCoins(targetUserId);
        if (!payloadInput) return;

        button.setAttribute("disabled", "disabled");
        this.setMessage(`正在更新 ${targetUserId} 的金币...`);
        try {
            const response = await fetch(`/api/users/${encodeURIComponent(targetUserId)}/coins`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    actorUserId: session.userId,
                    coins: payloadInput.coins
                })
            });

            const payload = await readJsonResponse(response);
            if (!this.ownsRequest(owner)) return;
            readResponseUserId(payload, targetUserId);
            const oldCoins = pickValue(payload, "oldCoins", "OldCoins");
            const newCoins = pickValue(payload, "coins", "Coins");
            if (!Number.isInteger(oldCoins) || oldCoins < 0 || !Number.isInteger(newCoins) || newCoins < 0) {
                throw new Error("金币更新响应必须包含有效的 oldCoins 和 coins。");
            }
            this.setMessage(`已更新 ${this.resolveUsername(targetUserId)} 的金币：${oldCoins} -> ${newCoins}。`);
            await this.refreshUsers();
        } catch (error) {
            if (!this.ownsRequest(owner)) return;
            this.setMessage(`更新金币失败：${error?.message || String(error)}`, true);
        } finally {
            if (this.ownsRequest(owner)) button.removeAttribute("disabled");
        }
    }

    async updateUserRole(targetUserId, button) {
        if (!this.isModalOpen) return;
        const owner = this.requestOwner;
        const session = getUserSession();
        if (session.mode !== "user" || !session.userId) return;
        const select = this.tableBody?.querySelector(`select[data-user-id="${targetUserId}"]`);
        const nextRole = select?.value;
        if (!nextRole) return;

        button.setAttribute("disabled", "disabled");
        this.setMessage(`正在更新 ${targetUserId} 的权限...`);
        try {
            const response = await fetch(`/api/users/${encodeURIComponent(targetUserId)}/role`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    actorUserId: session.userId,
                    role: nextRole
                })
            });

            const payload = await readJsonResponse(response);
            if (!this.ownsRequest(owner)) return;
            const user = readUserSummary(payload, targetUserId);

            if (targetUserId === session.userId) {
                setUserSession({
                    userId: session.userId,
                    username: session.displayName,
                    avatar: session.avatar,
                    role: user.role,
                    permissionLevel: user.permissionLevel
                });
                return;
            }

            this.setMessage("权限更新成功。");
            await this.refreshUsers();
        } catch (error) {
            if (!this.ownsRequest(owner)) return;
            this.setMessage(`更新失败：${error?.message || String(error)}`, true);
        } finally {
            if (this.ownsRequest(owner)) button.removeAttribute("disabled");
        }
    }

    async dispose() {
        this.closeModal();
        this.handlers.forEach(remove => remove());
        this.handlers = [];
        this.initialized = false;
    }
}

let instance = null;

export async function initializeDevToolsUserManagement() {
    if (!instance) {
        instance = new DevToolsUserManagement();
    }
    await instance.init();
}

export async function cleanupDevToolsUserManagement() {
    if (!instance) return;
    await instance.dispose();
    instance = null;
}
