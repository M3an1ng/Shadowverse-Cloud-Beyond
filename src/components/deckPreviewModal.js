import { compareCardsForEditorAndDeck } from "../cardListSort.js";
import { buildDeckCardRootClassName } from "../deckCardTypeStyling.js";

const TYPE_LABELS = {
    follower: "随从",
    spell: "法术",
    amulet: "护符"
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
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function formatWinRate(wins, games) {
    const totalGames = toInt(games, 0);
    if (totalGames <= 0) return "--";
    return `${((toInt(wins, 0) / totalGames) * 100).toFixed(1)}%`;
}

function getTypeLabel(value) {
    const normalized = normalizeEnumValue(value);
    return TYPE_LABELS[normalized] || "未知";
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
    const name = String(raw?.name ?? card?.name ?? card?.Name ?? `未知卡牌 #${cardDefinitionId}`);
    const type = String(raw?.type ?? card?.type ?? card?.Type ?? "unknown");
    const cardClass = String(raw?.cardClass ?? raw?.class ?? raw?.Class ?? card?.cardClass ?? card?.class ?? card?.Class ?? "");
    const rarity = String(raw?.rarity ?? card?.rarity ?? card?.Rarity ?? "");
    const cardSetDefinitionId = toInt(
        raw?.cardSetDefinitionId
        ?? raw?.CardSetDefinitionId
        ?? raw?.card_set_definition_id
        ?? card?.cardSetDefinitionId
        ?? card?.CardSetDefinitionId
        ?? card?.card_set_definition_id
        ?? 0,
        0
    );
    const artPreview = String(raw?.artPreview ?? card?.artPreview ?? card?.ArtPreview ?? "");
    return {
        cardDefinitionId,
        count,
        cost,
        name,
        type,
        cardClass,
        rarity,
        cardSetDefinitionId,
        artPreview
    };
}

function toComparableCard(item) {
    if (item?.card && typeof item.card === "object") {
        return item.card;
    }
    return {
        id: item?.cardDefinitionId ?? 0,
        cost: item?.cost ?? 99,
        class: item?.cardClass ?? "",
        type: item?.type ?? "",
        rarity: item?.rarity ?? "",
        cardSetDefinitionId: item?.cardSetDefinitionId ?? 0
    };
}

export function buildDeckPreviewSnapshot(rawCards) {
    const cards = (Array.isArray(rawCards) ? rawCards : [])
        .map(normalizeCardItem)
        .filter((item) => item.cardDefinitionId > 0 && item.count > 0)
        .sort((a, b) => {
            const orderDiff = compareCardsForEditorAndDeck(toComparableCard(a), toComparableCard(b));
            if (orderDiff !== 0) return orderDiff;
            const nameDiff = String(a.name || "").localeCompare(String(b.name || ""), undefined, { sensitivity: "base" });
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
        <div class="deck-preview-curve-col" title="${index === 8 ? "8+" : index} 费: ${count}">
            <div class="deck-preview-curve-bar-area">
                <div class="deck-preview-curve-bar" style="height: ${(count / max) * 100}%"></div>
            </div>
            <div class="deck-preview-curve-label">${index === 8 ? "8+" : index}</div>
        </div>
    `).join("");
}

function renderCardsHtml(cards) {
    if (!Array.isArray(cards) || cards.length === 0) {
        return '<div class="deck-preview-empty">当前卡组没有可预览的卡牌。</div>';
    }

    return cards.map((item) => {
        const previewUrl = item.artPreview ? encodeURI(item.artPreview).replace(/'/g, "%27") : "";
        const styleAttr = previewUrl ? ` style="--deck-preview-art: url('${previewUrl}');"` : "";
        const rarityClass = getRarityClass(item.rarity);
        const cardClassName = buildDeckCardRootClassName("deck-preview-card", {
            hasArt: Boolean(previewUrl),
            rarityClass,
            type: item.type
        });
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
        <div class="deck-preview-toolbar-stats-title">胜率统计</div>
        <div class="deck-preview-toolbar-stats-line primary">
            <span>总对局 ${totalGames}</span>
            <span>总胜率 ${formatWinRate(totalWins, totalGames)}</span>
        </div>
        <div class="deck-preview-toolbar-stats-line secondary">
            <span>先手 ${goingFirstGames}局 / ${formatWinRate(goingFirstWins, goingFirstGames)}</span>
            <span>后手 ${goingSecondGames}局 / ${formatWinRate(goingSecondWins, goingSecondGames)}</span>
        </div>
    `;
}

export function createDeckPreviewModal({
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
                const button = event.target instanceof Element
                    ? event.target.closest(".deck-preview-card[data-card-id]")
                    : null;
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
            <div class="deck-preview-type-row type-counts">
                <span class="type-count follower">从者 ${toNumber(snapshot.typeCounts.follower, 0)}</span>
                <span class="type-count spell">法术 ${toNumber(snapshot.typeCounts.spell, 0)}</span>
                <span class="type-count amulet">护符 ${toNumber(snapshot.typeCounts.amulet, 0)}</span>
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
