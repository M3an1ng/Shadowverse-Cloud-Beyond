// src/storageFallback.js
function getStorageByName(name) {
  if (typeof window === "undefined") return null;
  try {
    return window[name] ?? null;
  } catch {
    return null;
  }
}
function listKnownStorages() {
  return [getStorageByName("localStorage"), getStorageByName("sessionStorage")].filter(Boolean);
}
function getStoredValue(key) {
  if (!key) return null;
  const storages = listKnownStorages();
  for (const storage of storages) {
    try {
      const value = storage.getItem(key);
      if (value !== null && value !== void 0) {
        return value;
      }
    } catch {
    }
  }
  return null;
}
function setStoredValue(key, value) {
  if (!key) return false;
  const normalized = String(value ?? "");
  const storages = listKnownStorages();
  let wroteAny = false;
  for (const storage of storages) {
    try {
      storage.setItem(key, normalized);
      wroteAny = true;
    } catch {
    }
  }
  return wroteAny;
}

// src/userSession.js
var USER_ID_KEY = "svb.user.id";
var USER_NAME_KEY = "svb.user.name";
var USER_AVATAR_KEY = "svb.user.avatar";
var USER_ROLE_KEY = "svb.user.role";
var USER_PERMISSION_LEVEL_KEY = "svb.user.permissionLevel";
var GUEST_ID_KEY = "svb.guest.id";
var USER_SESSION_CHANGED_EVENT = "svb:user-session-changed";
var cachedGuestId = null;
function isValidGuid(value) {
  if (!value || typeof value !== "string") return false;
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}
function generateGuid() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = Math.random() * 16 | 0;
    const v = c === "x" ? r : r & 3 | 8;
    return v.toString(16);
  });
}
function getStoredUserId() {
  const raw = getStoredValue(USER_ID_KEY);
  return isValidGuid(raw) ? raw : null;
}
function normalizeRole(role) {
  const normalized = String(role || "").trim().toLowerCase();
  if (normalized === "admin") return "admin";
  if (normalized === "guest") return "guest";
  return "player";
}
function resolvePermissionLevel(role) {
  const normalizedRole = normalizeRole(role);
  if (normalizedRole === "admin") return 100;
  if (normalizedRole === "player") return 10;
  return 0;
}
function parsePermissionLevel(value) {
  const parsed = Number.parseInt(String(value ?? ""), 10);
  if (!Number.isFinite(parsed)) return null;
  return Math.max(0, Math.trunc(parsed));
}
function getGuestId() {
  if (isValidGuid(cachedGuestId)) return cachedGuestId;
  const existing = getStoredValue(GUEST_ID_KEY);
  if (isValidGuid(existing)) {
    cachedGuestId = existing;
    return existing;
  }
  const created = generateGuid();
  cachedGuestId = created;
  setStoredValue(GUEST_ID_KEY, created);
  return created;
}
function getUserSession() {
  const userId = getStoredUserId();
  if (userId) {
    const storedAvatar = getStoredValue(USER_AVATAR_KEY) || "";
    const role = normalizeRole(getStoredValue(USER_ROLE_KEY));
    const storedLevel = parsePermissionLevel(getStoredValue(USER_PERMISSION_LEVEL_KEY));
    return {
      mode: "user",
      userId,
      guestId: null,
      displayName: getStoredValue(USER_NAME_KEY) || "\u7528\u6237",
      avatar: storedAvatar || "U",
      role,
      permissionLevel: storedLevel ?? resolvePermissionLevel(role)
    };
  }
  return {
    mode: "guest",
    userId: null,
    guestId: getGuestId(),
    displayName: "\u6E38\u5BA2",
    avatar: "G",
    role: "guest",
    permissionLevel: 0
  };
}

// src/components/deckPreviewModal.js
var TYPE_LABELS = {
  follower: "\u4ECE\u8005",
  spell: "\u6CD5\u672F",
  amulet: "\u62A4\u7B26"
};
function toInt(value, fallback = 0) {
  const parsed = Number.parseInt(String(value ?? ""), 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}
function toNumber(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}
function normalizeEnumValue(value) {
  return String(value ?? "").trim().toLowerCase();
}
function escapeHtml(value) {
  return String(value ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/\"/g, "&quot;").replace(/'/g, "&#39;");
}
function formatWinRate(wins, games) {
  const totalGames = toInt(games, 0);
  if (totalGames <= 0) return "--";
  return `${(toInt(wins, 0) / totalGames * 100).toFixed(1)}%`;
}
function getTypeLabel(value) {
  const normalized = normalizeEnumValue(value);
  return TYPE_LABELS[normalized] || "\u672A\u77E5";
}
function getRarityClass(value) {
  const normalized = normalizeEnumValue(value);
  if (!normalized) return "";
  if (normalized === "legendary") return "rarity-legendary";
  return `rarity-${normalized}`;
}
function normalizeCardItem(raw) {
  const card = raw?.card ?? null;
  const cardDefinitionId = toInt(raw?.cardDefinitionId ?? card?.id ?? card?.Id ?? 0, 0);
  const count = toInt(raw?.count, 0);
  const cost = toInt(raw?.cost ?? card?.cost ?? card?.Cost ?? 99, 99);
  const name = String(raw?.name ?? card?.name ?? card?.Name ?? `\u672A\u77E5\u5361\u724C #${cardDefinitionId}`);
  const type = String(raw?.type ?? card?.type ?? card?.Type ?? "Unknown");
  const rarity = String(raw?.rarity ?? card?.rarity ?? card?.Rarity ?? "");
  const artPreview = String(raw?.artPreview ?? card?.artPreview ?? card?.ArtPreview ?? "");
  return {
    cardDefinitionId,
    count,
    cost,
    name,
    type,
    rarity,
    artPreview
  };
}
function buildDeckPreviewSnapshot(rawCards) {
  const cards = (Array.isArray(rawCards) ? rawCards : []).map(normalizeCardItem).filter((item) => item.cardDefinitionId > 0 && item.count > 0).sort((a, b) => {
    if (a.cost !== b.cost) return a.cost - b.cost;
    const nameDiff = String(a.name || "").localeCompare(String(b.name || ""), void 0, { sensitivity: "base" });
    if (nameDiff !== 0) return nameDiff;
    return a.cardDefinitionId - b.cardDefinitionId;
  });
  const curve = Array(9).fill(0);
  const typeCounts = { follower: 0, spell: 0, amulet: 0 };
  let totalCount = 0;
  cards.forEach((item) => {
    const bucket = Math.min(Math.max(item.cost, 0), 8);
    curve[bucket] += item.count;
    totalCount += item.count;
    const normalizedType = normalizeEnumValue(item.type);
    if (normalizedType === "follower" || normalizedType === "spell" || normalizedType === "amulet") {
      typeCounts[normalizedType] += item.count;
    }
  });
  return { items: cards, curve, typeCounts, totalCount };
}
function renderCurveHtml(curve) {
  const values = Array.isArray(curve) ? curve : Array(9).fill(0);
  const max = Math.max(...values, 1);
  return values.map((count, index) => `
        <div class="deck-preview-curve-col" title="${index === 8 ? "8+" : index} \u8D39\u7528: ${count}">
            <div class="deck-preview-curve-bar" style="height: ${count / max * 100}%"></div>
            <div class="deck-preview-curve-label">${index === 8 ? "8+" : index}</div>
        </div>
    `).join("");
}
function renderCardsHtml(cards) {
  if (!Array.isArray(cards) || cards.length === 0) {
    return '<div class="deck-preview-empty">\u5F53\u524D\u5361\u7EC4\u6CA1\u6709\u53EF\u9884\u89C8\u7684\u5361\u724C\u3002</div>';
  }
  return cards.map((item) => {
    const previewUrl = item.artPreview ? encodeURI(item.artPreview).replace(/'/g, "%27") : "";
    const styleAttr = previewUrl ? ` style="--deck-preview-art: url('${previewUrl}');"` : "";
    const rarityClass = getRarityClass(item.rarity);
    const cardClassName = `deck-preview-card${previewUrl ? " has-art" : ""}${rarityClass ? ` ${rarityClass}` : ""}`;
    const costLabel = Number.isFinite(item.cost) && item.cost >= 0 && item.cost < 99 ? item.cost : "?";
    return `
            <button class="${cardClassName}" type="button" data-card-id="${item.cardDefinitionId}"${styleAttr}>
                <span class="deck-preview-card-cost">${costLabel}</span>
                <span class="deck-preview-card-count">x${item.count}</span>
                <span class="deck-preview-card-name">${escapeHtml(item.name)}</span>
                <span class="deck-preview-card-type">${escapeHtml(getTypeLabel(item.type))}</span>
            </button>
        `;
  }).join("");
}
function renderStatsHtml(stats) {
  const totalGames = toInt(stats?.totalGames, 0);
  const totalWins = toInt(stats?.totalWins, 0);
  const goingFirstGames = toInt(stats?.goingFirstGames, 0);
  const goingFirstWins = toInt(stats?.goingFirstWins, 0);
  const goingSecondGames = toInt(stats?.goingSecondGames, 0);
  const goingSecondWins = toInt(stats?.goingSecondWins, 0);
  return `
        <div class="deck-preview-toolbar-stats-title">\u80DC\u7387\u7EDF\u8BA1</div>
        <div class="deck-preview-toolbar-stats-line primary">
            <span>\u603B\u5BF9\u5C40 ${totalGames}</span>
            <span>\u603B\u80DC\u7387 ${formatWinRate(totalWins, totalGames)}</span>
        </div>
        <div class="deck-preview-toolbar-stats-line secondary">
            <span>\u5148\u624B ${goingFirstGames}\u5C40 / ${formatWinRate(goingFirstWins, goingFirstGames)}</span>
            <span>\u540E\u624B ${goingSecondGames}\u5C40 / ${formatWinRate(goingSecondWins, goingSecondGames)}</span>
        </div>
    `;
}
function createDeckPreviewModal({
  idPrefix,
  onExport = null,
  onCardClick = null,
  onClose = null
} = {}) {
  const getElement = (suffix) => document.getElementById(`${idPrefix}-${suffix}`);
  const modal = getElement("modal");
  const title = getElement("title");
  const meta = getElement("meta");
  const curve = getElement("curve");
  const toolbarStats = getElement("toolbar-stats");
  const counts = getElement("counts");
  const cards = getElement("cards");
  const exportBtn = getElement("export-btn");
  const closeBtn = getElement("close-btn");
  const available = Boolean(modal && title && meta && curve && counts && cards);
  const removeHandlers = [];
  const close = () => {
    if (!available) return;
    modal.classList.remove("open");
    modal.setAttribute("aria-hidden", "true");
    if (typeof onClose === "function") {
      onClose();
    }
  };
  const isOpen = () => Boolean(available && modal.classList.contains("open"));
  if (available) {
    const modalClick = (event) => {
      if (event.target === modal) {
        close();
      }
    };
    modal.addEventListener("click", modalClick);
    removeHandlers.push(() => modal.removeEventListener("click", modalClick));
    if (closeBtn) {
      const closeClick = () => close();
      closeBtn.addEventListener("click", closeClick);
      removeHandlers.push(() => closeBtn.removeEventListener("click", closeClick));
    }
    if (cards && typeof onCardClick === "function") {
      const cardClick = (event) => {
        const button = event.target instanceof Element ? event.target.closest(".deck-preview-card[data-card-id]") : null;
        if (!button) return;
        event.stopPropagation();
        const cardId = toInt(button.getAttribute("data-card-id"), 0);
        if (cardId <= 0) return;
        onCardClick(cardId);
      };
      cards.addEventListener("click", cardClick);
      removeHandlers.push(() => cards.removeEventListener("click", cardClick));
    }
    if (exportBtn) {
      const exportClick = () => {
        if (exportBtn.disabled) return;
        if (typeof onExport === "function") {
          onExport();
        }
      };
      exportBtn.addEventListener("click", exportClick);
      removeHandlers.push(() => exportBtn.removeEventListener("click", exportClick));
    }
  }
  const open = ({
    titleText = "-",
    metaText = "-",
    cards: rawCards = [],
    stats = null,
    exportEnabled = true,
    exportVisible = true
  } = {}) => {
    if (!available) return false;
    const snapshot = buildDeckPreviewSnapshot(rawCards);
    title.textContent = String(titleText || "-");
    meta.textContent = String(metaText || "-");
    curve.innerHTML = renderCurveHtml(snapshot.curve);
    counts.innerHTML = `
            <div class="deck-preview-total">${toNumber(snapshot.totalCount, 0)} / 40</div>
            <div class="deck-preview-type-row">
                <span>\u4ECE\u8005: ${toNumber(snapshot.typeCounts.follower, 0)}</span>
                <span>\u6CD5\u672F: ${toNumber(snapshot.typeCounts.spell, 0)}</span>
                <span>\u62A4\u7B26: ${toNumber(snapshot.typeCounts.amulet, 0)}</span>
            </div>
        `;
    cards.innerHTML = renderCardsHtml(snapshot.items);
    if (toolbarStats) {
      toolbarStats.innerHTML = renderStatsHtml(stats);
    }
    if (exportBtn) {
      exportBtn.style.display = exportVisible ? "" : "none";
      exportBtn.disabled = !exportEnabled || typeof onExport !== "function";
    }
    modal.classList.add("open");
    modal.setAttribute("aria-hidden", "false");
    return true;
  };
  const dispose = () => {
    while (removeHandlers.length > 0) {
      const remove = removeHandlers.pop();
      remove?.();
    }
  };
  return {
    isAvailable: () => available,
    isOpen,
    open,
    close,
    dispose
  };
}

// src/devToolsWinrateStats.js
var SUMMARY_ENDPOINT = "/api/devtools/winrate-stats/summary";
var DECK_COMPOSITION_ENDPOINT = "/api/devtools/winrate-stats/deck-composition";
var DEFAULT_ENVIRONMENT = "open";
var DEFAULT_TOP = 50;
var MAX_TOP = 200;
var DEFAULT_SORT = {
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
function toInt2(value, fallback = 0) {
  const parsed = Number.parseInt(String(value ?? ""), 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}
function toNumber2(value, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}
function toNullableNumber(value) {
  if (value == null || value === "") return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}
function escapeHtml2(value) {
  return String(value ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/\"/g, "&quot;").replace(/'/g, "&#39;");
}
function formatTimestamp(value) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  return date.toLocaleString();
}
function parseTimestamp(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 0;
  return date.getTime();
}
function normalizeRate(rawRate, wins, games) {
  const totalGames = toInt2(games, 0);
  if (totalGames <= 0) return 0;
  const rate = toNumber2(rawRate, NaN);
  if (Number.isFinite(rate)) {
    return Math.round(rate * 100) / 100;
  }
  return Math.round(toInt2(wins, 0) / totalGames * 1e4) / 100;
}
function formatRate(rate, games) {
  if (toInt2(games, 0) <= 0) return "--";
  return `${toNumber2(rate, 0).toFixed(2)}%`;
}
function formatAverageTurn(value) {
  const number = toNullableNumber(value);
  if (number == null || number <= 0) return "--";
  return number.toFixed(2);
}
function compareValues(a, b, type) {
  if (type === "number") {
    const left2 = toNumber2(a, 0);
    const right2 = toNumber2(b, 0);
    return left2 - right2;
  }
  if (type === "date") {
    const left2 = parseTimestamp(a);
    const right2 = parseTimestamp(b);
    return left2 - right2;
  }
  const left = String(a ?? "");
  const right = String(b ?? "");
  return left.localeCompare(right, void 0, { sensitivity: "base", numeric: true });
}
var DevToolsWinrateStats = class {
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
      onExport: () => this.setMessage("\u5F53\u524D\u770B\u677F\u9884\u89C8\u6682\u4E0D\u652F\u6301\u5BFC\u51FA\u5361\u7EC4\u4EE3\u7801\u3002", true)
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
        const button = event.target instanceof Element ? event.target.closest("button[data-action='view-deck'][data-deck-hash]") : null;
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
    if (this.environmentInput) this.environmentInput.disabled = busy;
    if (this.topInput) this.topInput.disabled = busy;
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
      row.innerHTML = '<td colspan="11" class="devtools-winrate-empty">\u6682\u65E0\u5361\u724C\u80DC\u7387\u6570\u636E\u3002</td>';
      this.cardBody.appendChild(row);
      return;
    }
    const html = items.map((item) => `
            <tr>
                <td>${item.cardDefinitionId}</td>
                <td>${escapeHtml2(item.cardName)}</td>
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
      row.innerHTML = '<td colspan="15" class="devtools-winrate-empty">\u6682\u65E0\u5361\u7EC4\u80DC\u7387\u6570\u636E\u3002</td>';
      this.deckBody.appendChild(row);
      return;
    }
    const html = items.map((item) => `
            <tr>
                <td>
                    <button
                        class="tool-button devtools-winrate-view-deck-btn"
                        type="button"
                        data-action="view-deck"
                        data-deck-hash="${escapeHtml2(item.deckShapeHash)}"
                        data-deck-class="${escapeHtml2(item.deckClass)}"
                        title="\u67E5\u770B\u5361\u7EC4\u6784\u7B51">
                        \u67E5\u770B\u5361\u7EC4
                    </button>
                </td>
                <td>${escapeHtml2(item.deckClass)}</td>
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
    this.deckBody.innerHTML = html;
  }
  renderClassRows(items) {
    if (!this.classBody) return;
    this.classBody.innerHTML = "";
    if (!Array.isArray(items) || items.length === 0) {
      const row = document.createElement("tr");
      row.innerHTML = '<td colspan="14" class="devtools-winrate-empty">\u6682\u65E0\u804C\u4E1A\u80DC\u7387\u6570\u636E\u3002</td>';
      this.classBody.appendChild(row);
      return;
    }
    const html = items.map((item) => `
            <tr>
                <td>${escapeHtml2(item.deckClass)}</td>
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
      const cardDefinitionId = toInt2(pickValue(item, "cardDefinitionId", "CardDefinitionId"), 0);
      const cardName = String(pickValue(item, "cardName", "CardName") || `\u5361\u724C #${cardDefinitionId}`);
      const deckGames = toInt2(pickValue(item, "deckGames", "DeckGames"), 0);
      const deckWins = toInt2(pickValue(item, "deckWins", "DeckWins"), 0);
      const drawnGames = toInt2(pickValue(item, "drawnGames", "DrawnGames"), 0);
      const drawnWins = toInt2(pickValue(item, "drawnWins", "DrawnWins"), 0);
      const playedGames = toInt2(pickValue(item, "playedGames", "PlayedGames"), 0);
      const playedWins = toInt2(pickValue(item, "playedWins", "PlayedWins"), 0);
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
      const totalGames = toInt2(pickValue(item, "totalGames", "TotalGames"), 0);
      const totalWins = toInt2(pickValue(item, "totalWins", "TotalWins"), 0);
      const goingFirstGames = toInt2(pickValue(item, "goingFirstGames", "GoingFirstGames"), 0);
      const goingFirstWins = toInt2(pickValue(item, "goingFirstWins", "GoingFirstWins"), 0);
      const goingSecondGames = toInt2(pickValue(item, "goingSecondGames", "GoingSecondGames"), 0);
      const goingSecondWins = toInt2(pickValue(item, "goingSecondWins", "GoingSecondWins"), 0);
      const updatedAt = pickValue(item, "updatedAtUtc", "UpdatedAtUtc");
      const averageEndTurn = toNullableNumber(pickValue(item, "averageEndTurn", "AverageEndTurn"));
      const goingFirstAverageEndTurn = toNullableNumber(pickValue(item, "goingFirstAverageEndTurn", "GoingFirstAverageEndTurn"));
      const goingSecondAverageEndTurn = toNullableNumber(pickValue(item, "goingSecondAverageEndTurn", "GoingSecondAverageEndTurn"));
      return {
        deckShapeHash: String(pickValue(item, "deckShapeHash", "DeckShapeHash") || "-"),
        deckClass: String(pickValue(item, "deckClass", "DeckClass") || "\u672A\u77E5\u804C\u4E1A"),
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
      const totalGames = toInt2(pickValue(item, "totalGames", "TotalGames"), 0);
      const totalWins = toInt2(pickValue(item, "totalWins", "TotalWins"), 0);
      const goingFirstGames = toInt2(pickValue(item, "goingFirstGames", "GoingFirstGames"), 0);
      const goingFirstWins = toInt2(pickValue(item, "goingFirstWins", "GoingFirstWins"), 0);
      const goingSecondGames = toInt2(pickValue(item, "goingSecondGames", "GoingSecondGames"), 0);
      const goingSecondWins = toInt2(pickValue(item, "goingSecondWins", "GoingSecondWins"), 0);
      const updatedAt = pickValue(item, "updatedAtUtc", "UpdatedAtUtc");
      return {
        deckClass: String(pickValue(item, "deckClass", "DeckClass") || "\u672A\u77E5\u804C\u4E1A"),
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
      const cardDefinitionId = toInt2(pickValue(item, "cardDefinitionId", "CardDefinitionId"), 0);
      const count = toInt2(pickValue(item, "count", "Count"), 0);
      const cost = toInt2(pickValue(item, "cost", "Cost"), 0);
      const name = String(pickValue(item, "name", "Name") || `\u5361\u724C #${cardDefinitionId}`);
      const type = String(pickValue(item, "type", "Type") || "\u672A\u77E5");
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
    return this.deckStats.find((item) => String(item.deckShapeHash ?? "").trim().toUpperCase() === targetHash) ?? null;
  }
  async fetchDeckCompositionByHash(deckShapeHash) {
    const session = getUserSession();
    if (session.mode !== "user" || !session.userId) {
      throw new Error("\u8BF7\u5148\u767B\u5F55\u540E\u518D\u67E5\u770B\u5361\u7EC4\u6784\u7B51\u3002");
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
    }
    if (!response.ok) {
      const message = payload?.message || payload?.Message || response.statusText;
      throw new Error(message || `HTTP ${response.status}`);
    }
    return payload;
  }
  async openDeckPreviewByHash(deckShapeHash, fallbackDeckClass = "\u672A\u77E5\u804C\u4E1A") {
    if (!this.deckPreview?.isAvailable()) {
      this.setMessage("\u5361\u7EC4\u9884\u89C8\u754C\u9762\u4E0D\u53EF\u7528\u3002", true);
      return;
    }
    try {
      this.setMessage("\u6B63\u5728\u52A0\u8F7D\u5361\u7EC4\u6784\u7B51...");
      const payload = await this.fetchDeckCompositionByHash(deckShapeHash);
      const cardsRaw = pickValue(payload, "cards", "Cards");
      const cards = this.normalizeDeckCompositionCards(cardsRaw);
      const deckClass = String(pickValue(payload, "deckClass", "DeckClass") || fallbackDeckClass || "\u672A\u77E5\u804C\u4E1A");
      const environment = String(pickValue(payload, "environment", "Environment") || this.environmentInput?.value || DEFAULT_ENVIRONMENT);
      const stats = this.findDeckStatsByHash(deckShapeHash);
      const opened = this.deckPreview.open({
        titleText: `${deckClass}\u5361\u7EC4`,
        metaText: `${deckClass} | ${environment}`,
        cards,
        stats,
        exportEnabled: false,
        exportVisible: true
      });
      if (!opened) {
        this.setMessage("\u5361\u7EC4\u9884\u89C8\u754C\u9762\u4E0D\u53EF\u7528\u3002", true);
        return;
      }
      const totalCount = cards.reduce((sum, item) => sum + toInt2(item.count, 0), 0);
      this.setMessage(`\u5361\u7EC4\u6784\u7B51\u5DF2\u52A0\u8F7D\uFF1A${totalCount} \u5F20\u5361\u724C\u3002`);
    } catch (error) {
      this.setMessage(`\u52A0\u8F7D\u5361\u7EC4\u6784\u7B51\u5931\u8D25\uFF1A${error?.message || String(error)}`, true);
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
      this.setMessage("\u8BF7\u767B\u5F55\u540E\u67E5\u770B\u80DC\u7387\u7EDF\u8BA1\u3002", true);
      return;
    }
    const selectedEnvironment = String(this.environmentInput?.value || DEFAULT_ENVIRONMENT).trim() || DEFAULT_ENVIRONMENT;
    let top = toInt2(this.topInput?.value, DEFAULT_TOP);
    top = Math.min(Math.max(top, 1), MAX_TOP);
    if (this.topInput) {
      this.topInput.value = String(top);
    }
    const params = new URLSearchParams();
    params.set("userId", session.userId);
    params.set("environment", selectedEnvironment);
    params.set("top", String(top));
    this.setBusy(true);
    this.setMessage("\u6B63\u5728\u52A0\u8F7D\u80DC\u7387\u7EDF\u8BA1...");
    try {
      const response = await fetch(`${SUMMARY_ENDPOINT}?${params.toString()}`, { method: "GET" });
      let payload = null;
      try {
        payload = await response.json();
      } catch (err) {
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
      this.setMessage(`\u5DF2\u52A0\u8F7D\uFF1A\u5361\u724C ${this.cardStats.length} \u884C\uFF0C\u5361\u7EC4 ${this.deckStats.length} \u884C\uFF0C\u804C\u4E1A ${this.classStats.length} \u884C\u3002`);
    } catch (error) {
      this.cardStats = [];
      this.deckStats = [];
      this.classStats = [];
      this.renderActiveView();
      if (this.cardCountEl) this.cardCountEl.textContent = "0";
      if (this.deckCountEl) this.deckCountEl.textContent = "0";
      if (this.classCountEl) this.classCountEl.textContent = "0";
      if (this.generatedAtEl) this.generatedAtEl.textContent = "-";
      this.setMessage(`\u52A0\u8F7D\u5931\u8D25\uFF1A${error?.message || String(error)}`, true);
    } finally {
      this.setBusy(false);
    }
  }
  async dispose() {
    this.closeModal();
    this.deckPreview?.dispose();
    this.deckPreview = null;
    this.handlers.forEach((remove) => remove());
    this.handlers = [];
    this.initialized = false;
  }
};
var instance = null;
async function initializeDevToolsWinrateStats() {
  if (!instance) {
    instance = new DevToolsWinrateStats();
  }
  await instance.init();
}
async function cleanupDevToolsWinrateStats() {
  if (!instance) return;
  await instance.dispose();
  instance = null;
}
export {
  cleanupDevToolsWinrateStats,
  initializeDevToolsWinrateStats
};
