// src/components/deckPreviewModal.js
var TYPE_LABELS = {
  follower: "\u968F\u4ECE",
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
  const type = String(raw?.type ?? card?.type ?? card?.Type ?? "unknown");
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
        <div class="deck-preview-curve-col" title="${index === 8 ? "8+" : index} \u8D39: ${count}">
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
                <span>\u968F\u4ECE: ${toNumber(snapshot.typeCounts.follower, 0)}</span>
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
export {
  buildDeckPreviewSnapshot,
  createDeckPreviewModal
};
