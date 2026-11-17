import { showAlert } from '../siteDialog.js';
import { getDeckOwnerQuery, getLocalAvatarUrl } from "../userSession.js";
import { createDeckPreviewModal, loadDeckPreviewCards } from "./deckPreviewModal.js";
import { getDeckEnvironmentLabel, getDeckRuleLabel } from "../deckPresentation.js";

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/\"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function normalizeDeckCount(value) {
    const parsed = Number.parseInt(String(value ?? "0"), 10);
    return Number.isFinite(parsed) ? Math.max(0, parsed) : 0;
}

function buildDeckCardHtml(deck, selectedDeckId) {
    const id = String(deck?.id ?? "");
    const name = escapeHtml(deck?.name || "未命名卡组");
    const deckClass = escapeHtml(deck?.classLabel || "未知职业");
    const environmentLabel = escapeHtml(deck?.environmentLabel || getDeckEnvironmentLabel(deck?.environment));
    const ruleLabel = escapeHtml(deck?.ruleLabel || getDeckRuleLabel(deck?.rule));
    const count = normalizeDeckCount(deck?.cardCount);
    const isComplete = deck?.isBattleReady === true;
    const isSelected = id === String(selectedDeckId ?? "");

    return `
        <div class="deck-card wide-banner deck-picker-card${isSelected ? " selected" : ""}" data-deck-id="${escapeHtml(id)}">
            <div class="deck-art-bg"></div>
            <div class="deck-content">
                <div class="deck-info">
                    <div class="deck-class-name">${deckClass}</div>
                    <div class="deck-env-name">${environmentLabel} | ${ruleLabel}</div>
                    <div class="deck-name">${name}</div>
                    <div class="deck-status">
                        <span class="status-indicator ${isComplete ? "valid" : "invalid"}">${isComplete ? "Complete" : "Incomplete"}</span>
                        <span class="deck-count">${count} / 40</span>
                    </div>
                </div>
                <div class="deck-emblem">
                    <div class="emblem-icon"></div>
                </div>
            </div>
            <div class="deck-actions-overlay deck-picker-actions-overlay">
                <button class="deck-action-btn preview-deck-btn" type="button" data-picker-action="preview" data-deck-id="${escapeHtml(id)}">预览</button>
                <button class="deck-action-btn select-deck-btn" type="button" data-picker-action="select" data-deck-id="${escapeHtml(id)}">选择</button>
            </div>
        </div>
    `;
}

async function apiGetJson(url) {
    const response = await fetch(url, { method: "GET" });
    let payload = null;
    try {
        payload = await response.json();
    } catch (err) {
        // ignore parse errors; fall back to status text
    }
    if (!response.ok) {
        const message = payload?.message || payload?.Message || response.statusText;
        throw new Error(message || `HTTP ${response.status}`);
    }
    return payload;
}

function toSafeInt(value, fallback = 0) {
    const parsed = Number.parseInt(String(value ?? ""), 10);
    return Number.isFinite(parsed) ? Math.max(0, parsed) : fallback;
}

function getDeckStats(deck) {
    return {
        totalGames: toSafeInt(deck?.totalGames ?? deck?.TotalGames, 0),
        totalWins: toSafeInt(deck?.totalWins ?? deck?.TotalWins, 0),
        goingFirstGames: toSafeInt(deck?.goingFirstGames ?? deck?.GoingFirstGames, 0),
        goingFirstWins: toSafeInt(deck?.goingFirstWins ?? deck?.GoingFirstWins, 0),
        goingSecondGames: toSafeInt(deck?.goingSecondGames ?? deck?.GoingSecondGames, 0),
        goingSecondWins: toSafeInt(deck?.goingSecondWins ?? deck?.GoingSecondWins, 0)
    };
}

function buildTournamentDeckHtml(deck) {
    const { totalGames, totalWins } = getDeckStats(deck.stats);
    const rate = totalGames ? (totalWins / totalGames * 100).toFixed(1) : null;
    const name = escapeHtml(deck.name || "未命名卡组");
    const deckClass = escapeHtml(String(deck.class ?? "").toLowerCase());
    const record = totalGames ? `${totalWins} 胜 · ${totalGames - totalWins} 负`
        : (deck.stats ? "暂无小局" : "暂无统计");
    const bans = deck.stats?.bansReceived;
    return `
        <article class="tournament-player-deck" data-deck-class="${deckClass}">
            <div class="tournament-player-deck-heading">
                <div class="tournament-player-deck-emblem" aria-hidden="true"></div>
                <div>
                    <span>${escapeHtml(deck.classLabel)}</span>
                    <h4>${name}</h4>
                </div>
            </div>
            <div class="tournament-player-deck-stats">
                <div><span>小局胜率</span><strong>${rate === null ? "—" : rate + "%"}</strong></div>
                <span>${record}</span>
            </div>
            <div class="tournament-player-deck-meter" aria-hidden="true"><span style="width: ${rate ?? 0}%"></span></div>
            ${bans == null ? "" : `
                <div class="tournament-player-deck-ban" title="本阶段已结算对局的禁用次数">
                    <span>被禁用</span><span><strong>${toSafeInt(bans)}</strong> 次</span>
                </div>
            `}
            <button class="tournament-button tournament-player-deck-preview" type="button"
                data-picker-action="preview" data-deck-id="${escapeHtml(deck.id)}">预览卡组</button>
        </article>
    `;
}

let sharedPicker;

export function openDeckSelectionPicker(options) {
    sharedPicker ??= createDeckSelectionPickerModal();
    return sharedPicker.open(options);
}

export function closeDeckSelectionPicker() {
    sharedPicker?.close();
}

function createDeckSelectionPickerModal() {
    const root = document.createElement("div");
    root.className = "deck-picker-modal";
    root.setAttribute("aria-hidden", "true");
    root.innerHTML = `
        <div class="deck-selection-view deck-picker-selection-view" role="dialog" aria-modal="true" aria-labelledby="deck-picker-modal-title">
            <div class="view-header">
                <div class="header-left">
                    <div class="deck-picker-profile-avatar" aria-hidden="true" hidden>
                        <span></span>
                        <img alt="" decoding="async" hidden>
                    </div>
                    <div class="deck-picker-profile-identity">
                        <h2 id="deck-picker-modal-title">选择卡组</h2>
                        <div class="deck-picker-profile-record" hidden>
                            <span>本赛事战绩</span>
                            <div class="deck-picker-profile-tally">
                                <span><strong class="deck-picker-profile-wins"></strong> 胜</span>
                                <span><strong class="deck-picker-profile-losses"></strong> 负</span>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="header-right">
                    <button class="mp-btn ghost deck-picker-close-btn" type="button">关闭</button>
                </div>
            </div>
            <h3 class="deck-picker-profile-lineup" hidden>参赛卡组</h3>
            <div class="deck-grid deck-picker-grid" id="deck-picker-grid"></div>
            <section class="deck-picker-profile-history" aria-labelledby="deck-picker-history-title" hidden>
                <h3 id="deck-picker-history-title">前半程卡组</h3>
                <div class="deck-grid deck-picker-grid"></div>
            </section>
        </div>
        <div class="deck-preview-modal" id="deck-picker-preview-modal" aria-hidden="true">
            <div class="deck-preview-dialog" role="dialog" aria-modal="true" aria-labelledby="deck-picker-preview-title">
                <div class="deck-preview-toolbar">
                    <div class="deck-preview-toolbar-left">
                        <div class="deck-preview-title-group">
                            <div class="deck-preview-caption">卡组预览</div>
                            <h3 class="deck-preview-title" id="deck-picker-preview-title">-</h3>
                            <div class="deck-preview-meta" id="deck-picker-preview-meta">-</div>
                        </div>
                    </div>
                    <div class="deck-preview-toolbar-stats" id="deck-picker-preview-toolbar-stats"></div>
                    <div class="deck-preview-toolbar-actions">
                        <select class="form-select deck-picker-preview-switcher" aria-label="切换卡组" hidden></select>
                        <button class="btn-secondary" id="deck-picker-preview-close-btn" type="button" aria-label="关闭预览">关闭</button>
                    </div>
                </div>
                <div class="deck-preview-summary">
                    <div class="deck-preview-curve" id="deck-picker-preview-curve"></div>
                    <div class="deck-preview-counts" id="deck-picker-preview-counts"></div>
                </div>
                <div class="deck-preview-cards" id="deck-picker-preview-cards"></div>
            </div>
        </div>
    `;
    document.body.appendChild(root);

    const titleEl = root.querySelector("#deck-picker-modal-title");
    const selectionView = root.querySelector(".deck-picker-selection-view");
    const profileAvatar = root.querySelector(".deck-picker-profile-avatar");
    const profileAvatarImage = profileAvatar.querySelector("img");
    const profileAvatarFallback = profileAvatar.querySelector("span");
    const profileRecord = root.querySelector(".deck-picker-profile-record");
    const profileWins = root.querySelector(".deck-picker-profile-wins");
    const profileLosses = root.querySelector(".deck-picker-profile-losses");
    const profileLineup = root.querySelector(".deck-picker-profile-lineup");
    const gridEl = root.querySelector("#deck-picker-grid");
    const profileHistory = root.querySelector(".deck-picker-profile-history");
    const historyTitle = profileHistory.querySelector("h3");
    const historyGrid = profileHistory.querySelector(".deck-picker-grid");
    const closeBtn = root.querySelector(".deck-picker-close-btn");
    const previewCaption = root.querySelector(".deck-preview-caption");
    const previewClose = root.querySelector("#deck-picker-preview-close-btn");
    const previewSwitcher = root.querySelector(".deck-picker-preview-switcher");
    const deckMap = new Map();

    let resolver = null;
    let openVersion = 0;
    let previewVersion = -1;
    let profileMode = false;
    let returnFocus;
    let previewFocus;
    profileAvatarImage.addEventListener("error", () => { profileAvatarImage.hidden = true; });

    const deckPreview = createDeckPreviewModal({
        idPrefix: "deck-picker-preview",
        onClose: () => {
            selectionView.inert = false;
            if (root.classList.contains("open") && previewFocus?.isConnected) previewFocus.focus();
            previewFocus = undefined;
        }
    });

    const fetchDeckDetail = async (deckId) => {
        const query = getDeckOwnerQuery();
        return await apiGetJson(`/api/decks/${encodeURIComponent(deckId)}?${query}`);
    };

    const openDeckPreview = async (deckId, previewBtn) => {
        if (!deckPreview.isAvailable() || !deckId || previewVersion === openVersion) return;
        const deck = deckMap.get(String(deckId));
        if (!deck) return;

        const buttonText = previewBtn?.textContent;
        const version = openVersion;
        previewVersion = version;
        if (previewBtn instanceof HTMLButtonElement) {
            previewBtn.disabled = true;
            previewBtn.textContent = "加载中...";
        }

        try {
            const detail = profileMode ? deck : await fetchDeckDetail(deckId);
            const cards = await loadDeckPreviewCards(detail);
            if (version !== openVersion) return;
            const firstPreview = !deckPreview.isOpen();
            deckPreview.open({
                titleText: deck.name || "未命名卡组",
                metaText: `${deck.classLabel || "未知职业"} | ${deck.ruleLabel || getDeckRuleLabel(deck?.rule)} | ${deck.environmentLabel || getDeckEnvironmentLabel(deck?.environment)}`,
                cards,
                stats: getDeckStats(profileMode ? deck.stats : deck),
                exportEnabled: false,
                exportVisible: false
            });
            if (profileMode) previewSwitcher.value = deckId;
            if (firstPreview) {
                selectionView.inert = true;
                previewClose.focus();
            }
        } catch (error) {
            if (version === openVersion) throw error;
        } finally {
            if (previewVersion === version) previewVersion = -1;
            if (previewBtn instanceof HTMLButtonElement) {
                previewBtn.disabled = false;
                previewBtn.textContent = buttonText;
            }
        }
    };

    const close = (selectedDeckId = null) => {
        if (!root.classList.contains("open")) return;
        openVersion++;
        root.classList.remove("open");
        root.setAttribute("aria-hidden", "true");
        if (deckPreview.isOpen()) deckPreview.close();
        if (returnFocus?.isConnected) returnFocus.focus();
        returnFocus = null;
        if (resolver) {
            const resolve = resolver;
            resolver = null;
            resolve(selectedDeckId);
        }
    };

    function requestPreview(deckId, button) {
        if (!deckPreview.isOpen() && previewVersion !== openVersion) previewFocus = button ?? document.activeElement;
        openDeckPreview(deckId, button).catch((err) => {
            showAlert(`预览失败：${err.message}`);
        });
    }

    const open = ({
        title = "选择卡组", decks = [], selectedDeckId = null, playerProfile = null,
        emptyText = "没有可用卡组，请先在牌组编辑中创建。"
    } = {}) => {
        close();
        openVersion++;
        returnFocus = document.activeElement;
        profileMode = Boolean(playerProfile);
        const previous = playerProfile?.previousLineup;
        const earlierDecks = previous?.decks ?? [];

        titleEl.textContent = title;
        previewCaption.textContent = profileMode ? title + "的参赛卡组" : "卡组预览";
        root.toggleAttribute("data-player-profile", profileMode);
        profileAvatar.hidden = !profileMode;
        profileRecord.hidden = !profileMode;
        profileLineup.hidden = !profileMode;
        profileHistory.hidden = earlierDecks.length === 0;
        closeBtn.textContent = profileMode ? "×" : "关闭";
        closeBtn.setAttribute("aria-label", profileMode ? "关闭选手资料" : "关闭");
        closeBtn.title = "关闭";
        if (profileMode) {
            const avatarUrl = getLocalAvatarUrl(playerProfile.avatarUrl);
            profileAvatarFallback.textContent = Array.from(title.trim())[0] ?? "?";
            profileAvatarImage.hidden = !avatarUrl;
            if (avatarUrl) profileAvatarImage.src = avatarUrl;
            else profileAvatarImage.removeAttribute("src");
            profileWins.textContent = playerProfile.record ? toSafeInt(playerProfile.record.wins) : "—";
            profileLosses.textContent = playerProfile.record ? toSafeInt(playerProfile.record.losses) : "—";
            const label = playerProfile.stageLabel ? playerProfile.stageLabel + "卡组" : "参赛卡组";
            profileLineup.textContent = earlierDecks.length ? "后半程 · " + label : label;
        }
        deckMap.clear();

        if (!Array.isArray(decks) || decks.length === 0) {
            gridEl.innerHTML = '<div class="deck-picker-empty">' + escapeHtml(emptyText) + '</div>';
        } else {
            decks.forEach(deck => {
                const id = String(deck?.id ?? "");
                if (id) {
                    deckMap.set(id, deck);
                }
            });
            gridEl.innerHTML = decks.map(deck => profileMode
                ? buildTournamentDeckHtml(deck) : buildDeckCardHtml(deck, selectedDeckId)).join("");
        }
        if (earlierDecks.length) {
            historyTitle.textContent = previous.stageLabel ? "前半程 · " + previous.stageLabel + "卡组" : "前半程卡组";
            for (const deck of earlierDecks) {
                const id = String(deck.id);
                if (!deckMap.has(id)) deckMap.set(id, deck);
            }
        }
        historyGrid.innerHTML = earlierDecks.map(buildTournamentDeckHtml).join("");
        root.toggleAttribute("data-single-profile-deck", profileMode && !earlierDecks.length && deckMap.size === 1);
        previewSwitcher.replaceChildren(...(profileMode
            ? Array.from(deckMap.values(), deck => new Option(
                earlierDecks.length && deck.stageLabel ? `${deck.stageLabel} · ${deck.name}` : deck.name, deck.id)) : []));
        previewSwitcher.hidden = !profileMode || deckMap.size <= 1;

        root.classList.add("open");
        root.setAttribute("aria-hidden", "false");
        selectionView.scrollTop = 0;
        closeBtn?.focus();

        return new Promise((resolve) => {
            resolver = resolve;
        });
    };

    root.addEventListener("click", (event) => {
        if (event.target === root) {
            close(null);
            return;
        }
        const actionButton = event.target instanceof Element
            ? event.target.closest("button[data-picker-action][data-deck-id]")
            : null;
        if (!(actionButton instanceof HTMLButtonElement)) return;
        const action = String(actionButton.getAttribute("data-picker-action") || "");
        const deckId = String(actionButton.getAttribute("data-deck-id") || "");
        if (!deckId) return;

        if (action === "preview") {
            requestPreview(deckId, actionButton);
            return;
        }

        if (action === "select" && !profileMode) {
            close(deckId);
        }
    });

    closeBtn?.addEventListener("click", () => close(null));
    previewSwitcher.addEventListener("change", () => requestPreview(previewSwitcher.value));

    document.addEventListener("keydown", (event) => {
        if (!root.classList.contains("open")) return;
        if (event.key !== "Escape") return;
        event.preventDefault();
        if (deckPreview.isOpen()) {
            deckPreview.close();
            return;
        }
        close(null);
    });

    return { open, close };
}
