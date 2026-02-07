import * as signalR from "@microsoft/signalr";
import { getUserSession } from "./userSession.js";

const MAX_ENTRIES = 2000;
const SNAPSHOT_COUNT = 200;
const TRACE_ENDPOINT = "/api/devtools/condition-trace";

class DevToolsLogViewer {
    constructor() {
        this.initialized = false;
        this.entries = [];
        this.filteredEntries = [];
        this.connection = null;
        this.handlers = [];
        this.isModalOpen = false;
        this.traceEnabled = false;
    }

    async init() {
        if (this.initialized) return;
        this.initialized = true;

        this.root = document.getElementById("devtools-log-viewer");
        this.pageEl = document.getElementById("dev-tools-page");
        this.pageLockEl = document.getElementById("dev-tools-lock");
        this.modalEl = document.getElementById("log-viewer-modal");
        this.openBtn = document.getElementById("log-open-btn");
        this.closeBtn = document.getElementById("log-close-btn");
        if (!this.root) return;

        this.listEl = document.getElementById("log-stream-list");
        this.statusEl = document.getElementById("log-stream-status");
        this.lockEl = document.getElementById("log-stream-lock");
        this.searchInput = document.getElementById("log-search-input");
        this.autoScrollToggle = document.getElementById("log-auto-scroll");
        this.exportBtn = document.getElementById("log-export-btn");
        this.clearBtn = document.getElementById("log-clear-btn");
        this.levelCheckboxes = Array.from(this.root.querySelectorAll('input[name="log-level"]'));
        this.traceToggleBtn = document.getElementById("condition-trace-toggle");

        this.bindUI();
        this.refreshSessionGate();
        if (this.traceToggleBtn) {
            await this.refreshTraceState();
        }
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
                void this.closeModal();
            };
            this.closeBtn.addEventListener("click", handler);
            this.handlers.push(() => this.closeBtn.removeEventListener("click", handler));
        }

        if (this.modalEl) {
            const handler = (event) => {
                if (event.target === this.modalEl) {
                    void this.closeModal();
                }
            };
            this.modalEl.addEventListener("click", handler);
            this.handlers.push(() => this.modalEl.removeEventListener("click", handler));
        }

        const escHandler = (event) => {
            if (event.key === "Escape" && this.isModalOpen) {
                void this.closeModal();
            }
        };
        document.addEventListener("keydown", escHandler);
        this.handlers.push(() => document.removeEventListener("keydown", escHandler));

        if (this.searchInput) {
            const handler = () => this.applyFilters();
            this.searchInput.addEventListener("input", handler);
            this.handlers.push(() => this.searchInput.removeEventListener("input", handler));
        }

        if (this.levelCheckboxes.length > 0) {
            this.levelCheckboxes.forEach((checkbox) => {
                const handler = () => this.applyFilters();
                checkbox.addEventListener("change", handler);
                this.handlers.push(() => checkbox.removeEventListener("change", handler));
            });
        }

        if (this.autoScrollToggle) {
            const handler = () => this.maybeAutoScroll(true);
            this.autoScrollToggle.addEventListener("change", handler);
            this.handlers.push(() => this.autoScrollToggle.removeEventListener("change", handler));
        }

        if (this.exportBtn) {
            const handler = () => this.exportVisible();
            this.exportBtn.addEventListener("click", handler);
            this.handlers.push(() => this.exportBtn.removeEventListener("click", handler));
        }

        if (this.clearBtn) {
            const handler = () => this.clearEntries();
            this.clearBtn.addEventListener("click", handler);
            this.handlers.push(() => this.clearBtn.removeEventListener("click", handler));
        }

        if (this.traceToggleBtn) {
            const handler = () => {
                void this.toggleTrace();
            };
            this.traceToggleBtn.addEventListener("click", handler);
            this.handlers.push(() => this.traceToggleBtn.removeEventListener("click", handler));
        }
    }

    refreshSessionGate() {
        const session = getUserSession();
        const isUser = Boolean(session.userId && session.mode === "user");
        this.setLocked(!isUser);
        this.setPageLocked(!isUser);
        if (!isUser) {
            this.setStatus("Login required to view log stream.", "warning");
        }
        if (this.traceToggleBtn) {
            this.traceToggleBtn.disabled = !isUser;
        }
        return isUser ? session.userId : null;
    }

    async refreshTraceState() {
        const session = getUserSession();
        if (!session.userId || session.mode !== "user") {
            this.setTraceState(false);
            return;
        }

        try {
            const response = await fetch(`${TRACE_ENDPOINT}?userId=${encodeURIComponent(session.userId)}`);
            if (!response.ok) {
                throw new Error(`Request failed with status ${response.status}`);
            }
            const payload = await response.json();
            this.setTraceState(Boolean(payload?.conditionEnabled ?? payload?.ConditionEnabled));
        } catch (error) {
            console.warn("Failed to load condition trace state:", error);
            this.setTraceState(false);
        }
    }

    setTraceState(enabled) {
        this.traceEnabled = Boolean(enabled);
        if (!this.traceToggleBtn) return;
        this.traceToggleBtn.classList.toggle("is-active", this.traceEnabled);
        this.traceToggleBtn.setAttribute("aria-pressed", String(this.traceEnabled));
        this.traceToggleBtn.textContent = this.traceEnabled ? "条件追踪日志：已开启" : "条件追踪日志：已关闭";
    }

    async toggleTrace() {
        const session = getUserSession();
        if (!session.userId || session.mode !== "user") {
            this.setTraceState(false);
            return;
        }

        const nextState = !this.traceEnabled;
        if (this.traceToggleBtn) {
            this.traceToggleBtn.disabled = true;
        }

        try {
            const response = await fetch(TRACE_ENDPOINT, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    userId: session.userId,
                    conditionEnabled: nextState,
                    targetEnabled: nextState,
                    effectEnabled: nextState
                })
            });
            if (!response.ok) {
                throw new Error(`Request failed with status ${response.status}`);
            }
            const payload = await response.json();
            this.setTraceState(Boolean(payload?.conditionEnabled ?? payload?.ConditionEnabled));
        } catch (error) {
            console.warn("Failed to update condition trace state:", error);
        } finally {
            if (this.traceToggleBtn) {
                this.traceToggleBtn.disabled = false;
            }
        }
    }

    async openModal() {
        if (this.modalEl) {
            this.modalEl.classList.add("active");
            this.modalEl.setAttribute("aria-hidden", "false");
        }
        this.isModalOpen = true;
        const userId = this.refreshSessionGate();
        if (!userId) return;
        await this.ensureConnected(userId);
        this.maybeAutoScroll(true);
    }

    async closeModal() {
        if (this.modalEl) {
            this.modalEl.classList.remove("active");
            this.modalEl.setAttribute("aria-hidden", "true");
        }
        this.isModalOpen = false;
        await this.stopConnection();
    }

    setLocked(isLocked) {
        if (this.lockEl) {
            this.lockEl.classList.toggle("active", isLocked);
        }
        if (this.root) {
            this.root.classList.toggle("is-locked", isLocked);
        }
    }

    setPageLocked(isLocked) {
        if (this.pageEl) {
            this.pageEl.classList.toggle("is-locked", isLocked);
        }
        if (this.pageLockEl) {
            this.pageLockEl.setAttribute("aria-hidden", String(!isLocked));
        }
    }

    setStatus(message, tone = "neutral") {
        if (!this.statusEl) return;
        this.statusEl.textContent = message;
        this.statusEl.dataset.tone = tone;
    }

    async ensureConnected(userId) {
        if (this.connection) {
            const state = this.connection.state;
            if (state === signalR.HubConnectionState.Connected || state === signalR.HubConnectionState.Connecting) {
                return;
            }
        }
        await this.connect(userId);
    }

    async stopConnection() {
        if (!this.connection) return;
        try {
            await this.connection.stop();
        } catch (error) {
            console.warn("Failed to stop log stream connection:", error);
        }
        this.connection = null;
        this.setStatus("Disconnected", "warning");
    }

    async connect(userId) {
        this.setStatus("Connecting...", "neutral");

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(`/loghub?userId=${encodeURIComponent(userId)}`)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        this.connection.on("LogEntry", (entry) => {
            this.handleEntry(entry);
        });

        this.connection.onclose(() => {
            this.setStatus("Disconnected", "warning");
        });

        this.connection.onreconnecting(() => {
            this.setStatus("Reconnecting...", "warning");
        });

        this.connection.onreconnected(() => {
            this.setStatus("Reconnected", "success");
        });

        try {
            await this.connection.start();
            this.setStatus("Connected", "success");
            const snapshot = await this.connection.invoke("GetSnapshot", SNAPSHOT_COUNT);
            if (Array.isArray(snapshot)) {
                snapshot.forEach((entry) => this.handleEntry(entry, false));
                this.applyFilters();
            }
        } catch (error) {
            console.error("Failed to connect log stream:", error);
            this.setStatus("Connection failed", "error");
        }
    }

    handleEntry(entry, autoRender = true) {
        if (!entry) return;
        const normalized = this.normalizeEntry(entry);
        this.entries.push(normalized);
        if (this.entries.length > MAX_ENTRIES) {
            this.entries.shift();
        }

        if (!autoRender) return;

        if (this.matchesFilters(normalized)) {
            this.appendEntry(normalized);
            this.maybeAutoScroll();
        }
    }

    normalizeEntry(entry) {
        return {
            timestamp: entry.timestamp ?? entry.Timestamp,
            level: entry.level ?? entry.Level,
            message: entry.message ?? entry.Message,
            exception: entry.exception ?? entry.Exception,
            source: entry.source ?? entry.Source
        };
    }

    matchesFilters(entry) {
        const levels = this.getSelectedLevels();
        if (levels.size > 0 && !levels.has(entry.level)) {
            return false;
        }

        const query = (this.searchInput?.value || "").trim().toLowerCase();
        if (!query) return true;

        const message = `${entry.message || ""} ${entry.exception || ""} ${entry.source || ""}`.toLowerCase();
        return message.includes(query);
    }

    getSelectedLevels() {
        const selected = new Set();
        if (this.levelCheckboxes.length === 0) return selected;
        this.levelCheckboxes.forEach((checkbox) => {
            if (checkbox.checked) {
                selected.add(checkbox.value);
            }
        });
        return selected;
    }

    applyFilters() {
        if (!this.listEl) return;
        this.listEl.innerHTML = "";
        this.filteredEntries = this.entries.filter((entry) => this.matchesFilters(entry));
        this.filteredEntries.forEach((entry) => this.appendEntry(entry));
        this.maybeAutoScroll(true);
    }

    appendEntry(entry) {
        if (!this.listEl) return;

        const row = document.createElement("div");
        row.className = `log-stream-entry level-${String(entry.level || "").toLowerCase()}`;

        const timestamp = document.createElement("span");
        timestamp.className = "log-stream-timestamp";
        timestamp.textContent = this.formatTimestamp(entry.timestamp);

        const level = document.createElement("span");
        level.className = "log-stream-level";
        level.textContent = String(entry.level || "-");

        const message = document.createElement("span");
        message.className = "log-stream-message";
        message.textContent = entry.message || "";

        row.append(timestamp, level, message);

        if (entry.source) {
            const source = document.createElement("span");
            source.className = "log-stream-source";
            source.textContent = entry.source;
            row.append(source);
        }

        if (entry.exception) {
            const exception = document.createElement("pre");
            exception.className = "log-stream-exception";
            exception.textContent = entry.exception;
            row.append(exception);
        }

        this.listEl.appendChild(row);
    }

    formatTimestamp(value) {
        if (!value) return "";
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return String(value);
        return date.toLocaleString();
    }

    maybeAutoScroll(force = false) {
        if (!this.listEl || !this.autoScrollToggle) return;
        if (!this.autoScrollToggle.checked) return;

        if (force) {
            this.listEl.scrollTop = this.listEl.scrollHeight;
            return;
        }

        const nearBottom = Math.abs(this.listEl.scrollHeight - this.listEl.scrollTop - this.listEl.clientHeight) < 12;
        if (nearBottom) {
            this.listEl.scrollTop = this.listEl.scrollHeight;
        }
    }

    exportVisible() {
        const entries = this.entries.filter((entry) => this.matchesFilters(entry));
        const payload = JSON.stringify(entries, null, 2);
        const blob = new Blob([payload], { type: "application/json" });
        const url = URL.createObjectURL(blob);

        const link = document.createElement("a");
        const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
        link.href = url;
        link.download = `log_export_${timestamp}.json`;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    }

    clearEntries() {
        this.entries = [];
        this.filteredEntries = [];
        if (this.listEl) {
            this.listEl.innerHTML = "";
        }
    }

    async dispose() {
        this.handlers.forEach((remove) => remove());
        this.handlers = [];
        await this.stopConnection();
        if (this.modalEl) {
            this.modalEl.classList.remove("active");
            this.modalEl.setAttribute("aria-hidden", "true");
        }
        this.isModalOpen = false;
        this.initialized = false;
    }
}

let viewerInstance = null;

export async function initializeDevToolsLogViewer() {
    if (!viewerInstance) {
        viewerInstance = new DevToolsLogViewer();
    }
    await viewerInstance.init();
}

export async function cleanupDevToolsLogViewer() {
    if (!viewerInstance) return;
    await viewerInstance.dispose();
    viewerInstance = null;
}
