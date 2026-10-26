import { getDeckOwnerQuery, getUserSession } from "./userSession.js";
import { setStoredValue } from "./storageFallback.js";

let initialized = false;
let handlers = [];
let decksCache = [];

const START_ENDPOINT = "/api/devtools/matches/start";
const HANDSHAKE_STORAGE_KEY = "svb.lobby.handshake";

function bind(el, event, handler) {
    if (!el) return;
    el.addEventListener(event, handler);
    handlers.push(() => el.removeEventListener(event, handler));
}

function setLocked(pageEl, lockEl, locked) {
    if (pageEl) {
        pageEl.classList.toggle("is-locked", locked);
    }
    if (lockEl) {
        lockEl.setAttribute("aria-hidden", String(!locked));
    }
}

function setStatus(statusEl, message, isError = false) {
    if (!statusEl) return;
    statusEl.textContent = message || "";
    statusEl.dataset.tone = isError ? "error" : "neutral";
}

function parseNumberInput(inputEl) {
    if (!inputEl) return null;
    const raw = String(inputEl.value || "").trim();
    if (!raw) return null;
    const parsed = Number(raw);
    if (Number.isNaN(parsed)) return null;
    return Math.trunc(parsed);
}

async function readJsonResponse(response) {
    if (response.ok) {
        return response.json();
    }

    const rawText = await response.text();
    let message = rawText.trim();
    try {
        const payload = JSON.parse(rawText);
        message = payload?.message || payload?.Message || (typeof payload === "string" ? payload : message);
    } catch {
        // Error responses may contain plain text; successful responses must be JSON.
    }
    throw new Error(message || response.statusText || `HTTP ${response.status}`);
}

async function apiPostJson(url, body) {
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body ?? {})
    });
    return readJsonResponse(response);
}

async function apiGetJson(url) {
    const response = await fetch(url, { method: "GET" });
    return readJsonResponse(response);
}

async function loadDecks() {
    const query = getDeckOwnerQuery();
    const decks = await apiGetJson(`/api/decks?${query}`);
    if (!Array.isArray(decks)) {
        throw new Error("卡组列表响应必须为数组。");
    }
    for (const deck of decks) {
        const deckId = resolveDeckId(deck);
        if (typeof deckId !== "string" || !deckId.trim()) {
            throw new Error("卡组列表响应缺少有效的卡组 ID。");
        }
    }
    return decks;
}

function formatDeckLabel(deck) {
    const name = deck?.name || deck?.Name || "未命名卡组";
    const deckClass = deck?.class || deck?.Class || "未知职业";
    const count = deck?.cardCount ?? deck?.CardCount ?? 0;
    return `${name} · ${deckClass} · ${count}/40`;
}

function resolveDeckId(deck) {
    return deck?.deckId || deck?.id || deck?.DeckId || null;
}

function fillDeckSelect(selectEl, decks) {
    if (!selectEl) return;
    if (!decks.length) {
        selectEl.innerHTML = '<option value="">没有可用卡组</option>';
        selectEl.disabled = true;
        return;
    }
    selectEl.disabled = false;
    selectEl.innerHTML = [
        '<option value="">请选择卡组</option>',
        ...decks.map(deck => {
            const deckId = resolveDeckId(deck);
            const label = formatDeckLabel(deck);
            return `<option value="${deckId}">${label}</option>`;
        })
    ].join("");
}

function buildConfig(inputs) {
    return {
        startingPlayPoints: parseNumberInput(inputs.startPp),
        maxPlayPoints: parseNumberInput(inputs.maxPp),
        startingHealth: parseNumberInput(inputs.startHp),
        startingHandSize: parseNumberInput(inputs.handSize),
        startingTurn: parseNumberInput(inputs.startTurn),
        ppBoostUses: parseNumberInput(inputs.ppBoost),
        seed: parseNumberInput(inputs.seed)
    };
}

function applyPreset(inputs, preset) {
    if (!inputs) return;
    if (preset === "standard") {
        if (inputs.startPp) inputs.startPp.value = "0";
        if (inputs.maxPp) inputs.maxPp.value = "0";
        if (inputs.startHp) inputs.startHp.value = "20";
        if (inputs.handSize) inputs.handSize.value = "4";
        if (inputs.startTurn) inputs.startTurn.value = "";
        if (inputs.ppBoost) inputs.ppBoost.value = "";
        if (inputs.seed) inputs.seed.value = "";
    }
    if (preset === "high-pp") {
        if (inputs.startPp) inputs.startPp.value = "8";
        if (inputs.maxPp) inputs.maxPp.value = "10";
        if (inputs.startHp) inputs.startHp.value = "20";
        if (inputs.handSize) inputs.handSize.value = "4";
        if (inputs.startTurn) inputs.startTurn.value = "";
        if (inputs.ppBoost) inputs.ppBoost.value = "";
        if (inputs.seed) inputs.seed.value = "";
    }
    if (preset === "high-hp") {
        if (inputs.startPp) inputs.startPp.value = "0";
        if (inputs.maxPp) inputs.maxPp.value = "0";
        if (inputs.startHp) inputs.startHp.value = "20";
        if (inputs.handSize) inputs.handSize.value = "4";
        if (inputs.startTurn) inputs.startTurn.value = "";
        if (inputs.ppBoost) inputs.ppBoost.value = "";
        if (inputs.seed) inputs.seed.value = "";
    }
}

export async function initializeDevToolsMatchLab({ navigateTo }) {
    if (initialized) return;
    initialized = true;

    const pageEl = document.getElementById("dev-tools-match-lab-page");
    const lockEl = document.getElementById("dev-tools-match-lab-lock");
    const statusEl = document.getElementById("match-lab-status");
    const modeEl = document.getElementById("match-lab-mode");
    const playerDeckEl = document.getElementById("match-lab-player-deck");
    const opponentDeckEl = document.getElementById("match-lab-opponent-deck");
    const opponentLabelEl = document.getElementById("match-lab-opponent-label");
    const startBtn = document.getElementById("match-lab-start-btn");

    const inputs = {
        startPp: document.getElementById("match-lab-start-pp"),
        maxPp: document.getElementById("match-lab-max-pp"),
        startHp: document.getElementById("match-lab-start-hp"),
        handSize: document.getElementById("match-lab-hand-size"),
        startTurn: document.getElementById("match-lab-start-turn"),
        ppBoost: document.getElementById("match-lab-pp-boost"),
        seed: document.getElementById("match-lab-seed")
    };

    const session = getUserSession();
    const isUser = session.mode === "user" && !!session.userId;
    setLocked(pageEl, lockEl, !isUser);
    if (!isUser) {
        setStatus(statusEl, "请先登录后再使用对战实验室。", true);
        return;
    }

    try {
        decksCache = await loadDecks();
        fillDeckSelect(playerDeckEl, decksCache);
        fillDeckSelect(opponentDeckEl, decksCache);
    } catch (err) {
        setStatus(statusEl, `卡组加载失败：${err.message || String(err)}`, true);
    }

    const updateMode = () => {
        const mode = (modeEl?.value || "ai").toLowerCase();
        if (opponentLabelEl) {
            opponentLabelEl.textContent = mode === "dual" ? "对手卡组" : "AI 卡组";
        }
    };

    bind(modeEl, "change", () => {
        updateMode();
        setStatus(statusEl, "");
    });

    bind(document.getElementById("match-lab-preset-standard"), "click", () => {
        applyPreset(inputs, "standard");
        setStatus(statusEl, "已应用标准开局。");
    });

    bind(document.getElementById("match-lab-preset-high-pp"), "click", () => {
        applyPreset(inputs, "high-pp");
        setStatus(statusEl, "已应用高费开局。");
    });

    bind(document.getElementById("match-lab-preset-high-hp"), "click", () => {
        applyPreset(inputs, "high-hp");
        setStatus(statusEl, "已应用高血开局。");
    });

    bind(startBtn, "click", async () => {
        setStatus(statusEl, "");
        const mode = (modeEl?.value || "ai").toLowerCase();
        const playerDeckId = playerDeckEl?.value || "";
        const opponentDeckId = opponentDeckEl?.value || "";
        if (!playerDeckId || !opponentDeckId) {
            setStatus(statusEl, "请先选择双方卡组。", true);
            return;
        }

        const config = buildConfig(inputs);
        setStatus(statusEl, "创建对战中...");
        try {
            const payload = {
                userId: session.userId,
                mode,
                playerDeckId,
                opponentDeckId,
                config
            };
            const response = await apiPostJson(START_ENDPOINT, payload);

            const requiredFields = ["gameId", "hostPlayerId", "hostToken", "joinCode"];
            if (mode === "dual") {
                requiredFields.push("guestPlayerId", "guestToken");
            }
            for (const field of requiredFields) {
                if (typeof response?.[field] !== "string" || !response[field].trim()) {
                    throw new Error(`对战创建响应缺少有效的 ${field}。`);
                }
            }
            if (typeof response.useAiFallback !== "boolean") {
                throw new Error("对战创建响应中的 useAiFallback 必须为布尔值。");
            }

            const handshake = {
                gameId: response.gameId,
                playerId: response.hostPlayerId,
                token: response.hostToken,
                joinCode: response.joinCode,
                role: "host",
                useAiFallback: response.useAiFallback === true
            };

            if (mode === "dual") {
                handshake.dualControl = true;
                handshake.hostPlayerId = response.hostPlayerId;
                handshake.hostToken = response.hostToken;
                handshake.guestPlayerId = response.guestPlayerId;
                handshake.guestToken = response.guestToken;
            }

            const didStore = setStoredValue(HANDSHAKE_STORAGE_KEY, JSON.stringify(handshake));
            if (!didStore) {
                console.warn("Failed to cache devtools handshake: no writable storage backend.");
            }

            await navigateTo("game");
        } catch (err) {
            setStatus(statusEl, `创建失败：${err.message || String(err)}`, true);
        }
    });

    updateMode();
}

export async function cleanupDevToolsMatchLab() {
    handlers.forEach(cleanup => cleanup());
    handlers = [];
    initialized = false;
}
