// src/deckBuilder.js - Deck Builder Logic
//
// Implements the Deck Builder UI and logic.
// Follows the "Aetherial Architect" aesthetic.

import { router } from './router.js';
import { getDeckOwnerQuery } from './userSession.js';
import { showCardDetailModal } from './components/CardDetailMockup.js';
import { compareCardsForEditorAndDeck } from './cardListSort.js';
import { loadCardDefinitions } from './cardDefinitionsLoader.js';

const DEFAULT_ENVIRONMENT = 'open';
const ENVIRONMENT_OPTIONS = [
    {
        value: 'open',
        label: '开放环�?,
        description: '所有卡牌集合可�?
    }
];

const CLASS_OPTIONS = [
    { value: 'forestcraft', label: '妖精' },
    { value: 'swordcraft', label: '皇家护卫' },
    { value: 'runecraft', label: '巫师' },
    { value: 'dragoncraft', label: '龙族' },
    { value: 'abysscraft', label: '梦魇' },
    { value: 'havencraft', label: '主教' },
    { value: 'portalcraft', label: '复仇�? }
];

const TYPE_OPTIONS = [
    { value: 'follower', label: '从�? },
    { value: 'spell', label: '法术' },
    { value: 'amulet', label: '护符' },
    { value: 'crest', label: '纹章' },
    { value: 'leader', label: '主战�? }
];

const RARITY_OPTIONS = [
    { value: 'bronze', label: '青铜' },
    { value: 'silver', label: '白银' },
    { value: 'gold', label: '黄金' },
    { value: 'legend', label: '传说' }
];

// --- State Management ---
const state = {
    decks: [], // List of all decks
    currentDeck: null, // The deck currently being edited
    rawCardsCache: null, // Raw card definitions from /api/cards
    availableCards: [], // All cards available for building
    cardIndex: new Map(),
    deckCreation: {
        mode: 'create',
        environment: null,
        class: null,
        step: 'environment',
        importName: '',
        importCode: ''
    },
    filters: {
        cost: null,
        class: '',
        type: '',
        rarity: '',
        set: '',
        search: ''
    },
    pagination: {
        currentPage: 1,
        itemsPerPage: 10 // Fixed limit to prevent scrolling
    },
    newDeckId: null
};

async function apiGetJson(url) {
    const response = await fetch(url, { method: "GET" });
    const payload = await response.json();
    if (!response.ok) {
        const message = payload?.message || payload?.Message || response.statusText;
        throw new Error(message || `HTTP ${response.status}`);
    }
    return payload;
}

async function apiPostJson(url, body) {
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body ?? {})
    });
    const payload = await response.json();
    if (!response.ok) {
        const message = payload?.message || payload?.Message || response.statusText;
        throw new Error(message || `HTTP ${response.status}`);
    }
    return payload;
}

async function apiPutJson(url, body) {
    const response = await fetch(url, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body ?? {})
    });
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

async function apiDelete(url) {
    const response = await fetch(url, { method: "DELETE" });
    if (!response.ok) {
        const payload = await response.json().catch(() => null);
        const message = payload?.message || payload?.Message || response.statusText;
        throw new Error(message || `HTTP ${response.status}`);
    }
}

// --- DOM Elements ---
let container = null;

// --- Initialization ---

export async function initializeDeckBuilder() {
    container = document.getElementById('deck-builder-container');
    if (!container) {
        console.error("DeckBuilder: Container not found!");
        return;
    }

    try {
        await bootstrapDecks();
        await Promise.all([loadDecks(), loadCards()]);
    } catch (error) {
        console.error("DeckBuilder: Failed to load data:", error);
    }

    renderDeckSelection();
}

async function bootstrapDecks() {
    const query = getDeckOwnerQuery();
    try {
        await apiPostJson(`/api/decks/bootstrap?${query}`, {});
    } catch (error) {
        console.warn("DeckBuilder: Bootstrap failed.", error);
    }
}

async function loadDecks() {
    const query = getDeckOwnerQuery();
    const decks = await apiGetJson(`/api/decks?${query}`);
    state.decks = Array.isArray(decks)
        ? decks.map(deck => ({
            ...deck,
            environment: normalizeEnvironment(deck.environment)
        }))
        : [];
}

async function loadCards() {
    const cards = await loadCardDefinitions({
        useCache: true,
        force: false,
        getCached: () => state.rawCardsCache,
        setCached: cardsFromApi => {
            state.rawCardsCache = cardsFromApi;
        }
    });
    const filtered = Array.isArray(cards)
        ? cards.filter(card => {
            const collectible = card?.isCollectible ?? card?.IsCollectible;
            if (collectible !== true) return false;
            const rarity = normalizeEnumValue(card?.rarity ?? card?.Rarity ?? '');
            if (rarity === 'special') return false;
            return Number(card?.id) > 0 && String(card?.type || '').toLowerCase() !== 'leader';
        })
        : [];

    state.availableCards = filtered;
    state.cardIndex = new Map(filtered.map(card => [Number(card.id), card]));
}

function getDeckCardCount(deck) {
    if (!deck?.cards) {
        return deck?.cardCount ?? 0;
    }
    return deck.cards.reduce((sum, entry) => sum + (entry.count || 0), 0);
}

function toSafeInt(value, fallback = 0) {
    const parsed = Number.parseInt(String(value ?? ''), 10);
    return Number.isFinite(parsed) ? parsed : fallback;
}

function formatDeckWinRate(totalWins, totalGames) {
    const games = Math.max(0, toSafeInt(totalGames, 0));
    if (games <= 0) {
        return '--';
    }

    const wins = Math.max(0, toSafeInt(totalWins, 0));
    return `${((wins / games) * 100).toFixed(1)}%`;
}

function formatDeckWinRateWithGames(totalWins, totalGames) {
    const games = Math.max(0, toSafeInt(totalGames, 0));
    if (games <= 0) {
        return '--';
    }

    const rate = formatDeckWinRate(totalWins, games);
    return `${rate} (${games})`;
}

function getDeckMatchStats(deck) {
    return {
        totalGames: Math.max(0, toSafeInt(deck?.totalGames ?? deck?.TotalGames, 0)),
        totalWins: Math.max(0, toSafeInt(deck?.totalWins ?? deck?.TotalWins, 0)),
        goingFirstGames: Math.max(0, toSafeInt(deck?.goingFirstGames ?? deck?.GoingFirstGames, 0)),
        goingFirstWins: Math.max(0, toSafeInt(deck?.goingFirstWins ?? deck?.GoingFirstWins, 0)),
        goingSecondGames: Math.max(0, toSafeInt(deck?.goingSecondGames ?? deck?.GoingSecondGames, 0)),
        goingSecondWins: Math.max(0, toSafeInt(deck?.goingSecondWins ?? deck?.GoingSecondWins, 0))
    };
}

function getDeckId(deck) {
    return deck?.deckId || deck?.id || null;
}

function resolveDeckName(deck, fallback = 'New Deck') {
    const value = deck?.name;
    if (value === null || value === undefined) {
        return fallback;
    }
    const trimmed = String(value).trim();
    return trimmed || fallback;
}

function getDeckPreviewItems(deck) {
    const entries = Array.isArray(deck?.cards) ? deck.cards : [];
    return entries
        .map(entry => {
            const cardDefinitionId = Number(entry?.cardDefinitionId);
            const count = Number(entry?.count ?? 0);
            const card = state.cardIndex.get(cardDefinitionId) ?? null;
            const costValue = Number(card?.cost);
            return {
                cardDefinitionId,
                count: Number.isFinite(count) ? count : 0,
                card,
                cost: Number.isFinite(costValue) ? costValue : 99
            };
        })
        .filter(item => item.count > 0);
}

function compareDeckPreviewItems(a, b) {
    if (a.cost !== b.cost) {
        return a.cost - b.cost;
    }
    const nameA = String(a.card?.name ?? a.cardDefinitionId ?? '');
    const nameB = String(b.card?.name ?? b.cardDefinitionId ?? '');
    return nameA.localeCompare(nameB, undefined, { sensitivity: 'base' });
}

function buildDeckPreviewSnapshot(deck) {
    const items = getDeckPreviewItems(deck).sort(compareDeckPreviewItems);
    const curve = Array(9).fill(0);
    const typeCounts = { follower: 0, spell: 0, amulet: 0 };
    let totalCount = 0;

    items.forEach(item => {
        const bucket = Math.min(Math.max(item.cost, 0), 8);
        curve[bucket] += item.count;
        totalCount += item.count;

        const type = normalizeEnumValue(item.card?.type ?? item.card?.Type ?? item.card?.cardType ?? item.card?.card_type);
        if (type === 'follower' || type === 'spell' || type === 'amulet') {
            typeCounts[type] += item.count;
        }
    });

    return {
        items,
        curve,
        typeCounts,
        totalCount
    };
}

function renderDeckPreviewCurveHtml(curve) {
    const values = Array.isArray(curve) ? curve : Array(9).fill(0);
    const max = Math.max(...values, 1);
    return values.map((count, index) => `
        <div class="deck-preview-curve-col" title="${index === 8 ? '8+' : index} �? ${count}">
            <div class="deck-preview-curve-bar" style="height: ${(count / max) * 100}%"></div>
            <div class="deck-preview-curve-label">${index === 8 ? '8+' : index}</div>
        </div>
    `).join('');
}

function renderDeckPreviewCardsHtml(items) {
    if (!Array.isArray(items) || items.length === 0) {
        return `
            <div class="deck-preview-empty">
                当前卡组没有可预览的卡牌�?
            </div>
        `;
    }

    return items.map(item => {
        const preview = item.card?.artPreview ?? item.card?.ArtPreview ?? '';
        const previewUrl = preview ? encodeURI(preview).replace(/'/g, '%27') : '';
        const styleAttr = previewUrl ? ` style="--deck-preview-art: url('${previewUrl}');"` : '';
        const rarityClass = getCardRarityClass(item.card);
        const cardClassName = `deck-preview-card${previewUrl ? ' has-art' : ''}${rarityClass ? ` ${rarityClass}` : ''}`;
        const costLabel = Number.isFinite(item.cost) && item.cost >= 0 && item.cost < 99 ? item.cost : '?';
        const cardName = item.card?.name ?? `未知卡牌 #${item.cardDefinitionId}`;
        const typeLabel = getTypeLabel(item.card?.type ?? item.card?.Type ?? '');
        return `
            <button class="${cardClassName}" type="button" data-card-id="${item.cardDefinitionId}"${styleAttr}>
                <span class="deck-preview-card-cost">${costLabel}</span>
                <span class="deck-preview-card-count">x${item.count}</span>
                <span class="deck-preview-card-name">${cardName}</span>
                <span class="deck-preview-card-type">${typeLabel || '未知'}</span>
            </button>
        `;
    }).join('');
}

async function fetchDeckDetail(deckId) {
    const query = getDeckOwnerQuery();
    const deck = await apiGetJson(`/api/decks/${deckId}?${query}`);
    const deckClass = normalizeEnumValue(deck?.class ?? deck?.Class ?? deck?.cardClass ?? deck?.card_class);
    return {
        ...deck,
        environment: normalizeEnvironment(deck?.environment),
        class: deckClass,
        cards: Array.isArray(deck?.cards) ? deck.cards : []
    };
}

async function fetchDeckCode(deckId) {
    const query = getDeckOwnerQuery();
    const payload = await apiGetJson(`/api/decks/${deckId}/code?${query}`);
    const code = payload?.code ?? payload?.Code ?? '';
    if (!code) {
        throw new Error('未获取到卡组代码');
    }
    return String(code);
}

async function copyTextToClipboard(text) {
    if (navigator?.clipboard?.writeText) {
        await navigator.clipboard.writeText(text);
        return;
    }

    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.setAttribute('readonly', 'true');
    textarea.style.position = 'fixed';
    textarea.style.top = '-1000px';
    textarea.style.left = '-1000px';
    document.body.appendChild(textarea);
    textarea.select();
    const copied = document.execCommand('copy');
    document.body.removeChild(textarea);
    if (!copied) {
        throw new Error('Clipboard copy is not supported in this environment.');
    }
}

function formatClassName(value) {
    if (!value) return 'Neutral';
    const text = String(value);
    return text.charAt(0).toUpperCase() + text.slice(1);
}

function normalizeEnumValue(value) {
    if (value === null || value === undefined) return '';
    return String(value).trim().toLowerCase();
}

function getCardRarityClass(card) {
    const rarity = normalizeEnumValue(card?.rarity ?? card?.Rarity ?? '');
    if (!rarity || rarity === 'special') return '';
    if (rarity === 'legendary') return 'rarity-legend';
    return `rarity-${rarity}`;
}

function normalizeEnvironmentValue(value, fallback = '') {
    if (value === null || value === undefined || value === '') return fallback;
    if (typeof value === 'number') {
        return value === 0 ? DEFAULT_ENVIRONMENT : String(value);
    }
    return normalizeEnumValue(value);
}

function normalizeEnvironment(value) {
    return normalizeEnvironmentValue(value, DEFAULT_ENVIRONMENT);
}

function normalizeEnvironmentList(value) {
    if (Array.isArray(value)) {
        return value
            .map(item => normalizeEnvironmentValue(item))
            .filter(Boolean);
    }
    const single = normalizeEnvironmentValue(value);
    return single ? [single] : [];
}

function getEnvironmentLabel(value) {
    const normalized = normalizeEnvironment(value);
    const match = ENVIRONMENT_OPTIONS.find(option => option.value === normalized);
    return match?.label || '未知环境';
}

function getClassLabel(value) {
    const normalized = normalizeEnumValue(value);
    const match = CLASS_OPTIONS.find(option => option.value === normalized);
    return match?.label || formatClassName(value);
}

function getTypeLabel(value) {
    const normalized = normalizeEnumValue(value);
    const match = TYPE_OPTIONS.find(option => option.value === normalized);
    return match?.label || formatEnumLabel(normalized);
}

function getRarityLabel(value) {
    const normalized = normalizeEnumValue(value);
    const match = RARITY_OPTIONS.find(option => option.value === normalized);
    return match?.label || formatEnumLabel(normalized);
}

function formatEnumLabel(value) {
    if (!value) return '';
    return String(value)
        .split('_')
        .filter(Boolean)
        .map(part => part.charAt(0).toUpperCase() + part.slice(1))
        .join(' ');
}

function getCardSetInfo(card) {
    const name = card?.card_set ?? card?.cardSet ?? card?.card_set_name ?? card?.cardSetName ?? '';
    const code = card?.card_set_code ?? card?.cardSetCode ?? '';
    const abbr = card?.card_set_abbr ?? card?.cardSetAbbr ?? '';
    const environment = card?.card_set_environment ?? card?.cardSetEnvironment ?? card?.card_set_environments ?? card?.cardSetEnvironments ?? null;
    return {
        name: name == null ? '' : String(name).trim(),
        code: code == null ? '' : String(code).trim(),
        abbr: abbr == null ? '' : String(abbr).trim(),
        environments: normalizeEnvironmentList(environment)
    };
}

function getCardSetLabel(info) {
    if (!info) return '';
    return info.name || '';
}

function getLibraryFilterSourceCards() {
    const source = Array.isArray(state.availableCards) ? state.availableCards : [];
    if (!state.currentDeck) return source;
    return source.filter(card => {
        if (!isCardAllowedByEnvironment(card, state.currentDeck.environment)) return false;
        if (!isCardAllowedByClass(card, state.currentDeck.class)) return false;
        return true;
    });
}

function buildEnumFilterOptions(cards, getter, baseOptions, labelResolver) {
    const valueSet = new Set();
    cards.forEach(card => {
        const value = normalizeEnumValue(getter(card));
        if (value) {
            valueSet.add(value);
        }
    });

    const ordered = [];
    baseOptions.forEach(option => {
        if (valueSet.has(option.value)) {
            ordered.push({ value: option.value, label: option.label });
            valueSet.delete(option.value);
        }
    });

    const remainder = Array.from(valueSet)
        .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))
        .map(value => ({
            value,
            label: labelResolver(value)
        }));

    return ordered.concat(remainder);
}

function buildSetFilterOptions(cards) {
    const setMap = new Map();
    cards.forEach(card => {
        const info = getCardSetInfo(card);
        const value = info.name;
        if (!value) return;
        const key = value.toLowerCase();
        if (!setMap.has(key)) {
            setMap.set(key, {
                value,
                label: getCardSetLabel(info)
            });
        }
    });

    return Array.from(setMap.values())
        .sort((a, b) => a.label.localeCompare(b.label, undefined, { numeric: true, sensitivity: 'base' }))
        .map(option => ({ value: option.value.toLowerCase(), label: option.label }));
}

function buildSelectOptions(options, selectedValue) {
    return options.map(option => {
        const value = option.value ?? '';
        const label = option.label ?? value;
        const selected = selectedValue && value === selectedValue ? 'selected' : '';
        return `<option value="${value}" ${selected}>${label}</option>`;
    }).join('');
}

function populateLibraryFilters() {
    const classFilter = document.getElementById('library-class-filter');
    const typeFilter = document.getElementById('library-type-filter');
    const rarityFilter = document.getElementById('library-rarity-filter');
    const setFilter = document.getElementById('library-set-filter');

    if (!classFilter || !typeFilter || !rarityFilter || !setFilter) return;

    const source = getLibraryFilterSourceCards();
    const classOptions = buildEnumFilterOptions(
        source,
        card => card?.class ?? card?.Class ?? card?.cardClass ?? card?.card_class,
        CLASS_OPTIONS,
        getClassLabel
    );
    const typeOptions = buildEnumFilterOptions(
        source,
        card => card?.type ?? card?.Type ?? card?.cardType ?? card?.card_type,
        TYPE_OPTIONS,
        getTypeLabel
    );
    const rarityOptions = buildEnumFilterOptions(
        source,
        card => card?.rarity ?? card?.Rarity,
        RARITY_OPTIONS,
        getRarityLabel
    );
    const setOptions = buildSetFilterOptions(source);

    const classValues = new Set(classOptions.map(option => option.value));
    const typeValues = new Set(typeOptions.map(option => option.value));
    const rarityValues = new Set(rarityOptions.map(option => option.value));
    const setValues = new Set(setOptions.map(option => option.value));

    if (state.filters.class && !classValues.has(state.filters.class)) state.filters.class = '';
    if (state.filters.type && !typeValues.has(state.filters.type)) state.filters.type = '';
    if (state.filters.rarity && !rarityValues.has(state.filters.rarity)) state.filters.rarity = '';
    if (state.filters.set && !setValues.has(state.filters.set)) state.filters.set = '';

    classFilter.innerHTML = '<option value="">ALL CLASSES</option>' + buildSelectOptions(classOptions, state.filters.class);
    typeFilter.innerHTML = '<option value="">ALL TYPES</option>' + buildSelectOptions(typeOptions, state.filters.type);
    rarityFilter.innerHTML = '<option value="">ALL RARITIES</option>' + buildSelectOptions(rarityOptions, state.filters.rarity);
    setFilter.innerHTML = '<option value="">ALL SETS</option>' + buildSelectOptions(setOptions, state.filters.set);
}

function isCardAllowedByEnvironment(card, environment) {
    const normalized = normalizeEnvironment(environment);
    if (normalized === DEFAULT_ENVIRONMENT) {
        return true;
    }
    const setEnvironments = getCardSetInfo(card).environments;
    if (!setEnvironments || setEnvironments.length === 0) return true;
    return setEnvironments.includes(normalized);
}

function isCardAllowedByClass(card, deckClass) {
    const cardClass = normalizeEnumValue(card?.class ?? card?.Class ?? card?.cardClass ?? card?.card_class);
    const targetClass = normalizeEnumValue(deckClass);
    if (!cardClass || !targetClass) return true;
    if (cardClass === 'neutral') return true;
    return cardClass === targetClass;
}

// --- Rendering: Deck Selection View ---

function renderDeckSelection() {
    container.innerHTML = `
        <div class="deck-selection-view">
            <div class="view-header">
                <div class="header-left">
                    <h2>Deck List</h2>
                </div>
                <div class="header-right">
                    <!-- Placeholder for "Edit List" or other actions -->
                </div>
            </div>
            <div class="deck-grid">
                ${state.decks.map(deck => {
                    const deckId = getDeckId(deck);
                    const deckName = resolveDeckName(deck);
                    const cardCount = deck.cardCount ?? getDeckCardCount(deck);
                    const className = formatClassName(deck.class);
                    const isComplete = cardCount === 40;
                    return `
                    <div class="deck-card wide-banner" data-id="${deckId}">
                        <div class="deck-art-bg"></div> <!-- Placeholder for Character Art -->
                        <div class="deck-content">
                            <div class="deck-info">
                                <div class="deck-class-name">${className}</div>
                                <div class="deck-env-name">${getEnvironmentLabel(deck.environment)}</div>
                                <div class="deck-name">${deckName}</div>
                                <div class="deck-status">
                                    <span class="status-indicator ${isComplete ? 'valid' : 'invalid'}">
                                        ${isComplete ? 'Complete' : 'Incomplete'}
                                    </span>
                                    <span class="deck-count">${cardCount} / 40</span>
                                </div>
                            </div>
                            <div class="deck-emblem">
                                <div class="emblem-icon"></div>
                            </div>
                        </div>
                        <div class="deck-actions-overlay">
                            <button class="deck-action-btn preview-deck-btn" data-id="${deckId}">预览</button>
                            <button class="deck-action-btn edit-deck-btn" data-id="${deckId}">编辑</button>
                            <button class="deck-action-btn delete-deck-btn" data-id="${deckId}">删除</button>
                        </div>
                    </div>
                `;
                }).join('')}
                
                <!-- New Deck Button as a Grid Item -->
                <div class="deck-card create-new-deck" id="create-deck-card">
                    <div class="create-content">
                        <span class="plus-icon">+</span>
                        <span class="create-text">New Deck</span>
                    </div>
                </div>
            </div>

            <div class="deck-create-modal" id="deck-create-modal" aria-hidden="true">
                <div class="deck-create-dialog" role="dialog" aria-modal="true">
                    <div class="deck-create-header">
                        <div class="deck-create-title">新建卡组</div>
                        <button class="btn-icon deck-create-close" id="deck-create-close" type="button">×</button>
                    </div>
                    <div class="deck-create-mode-switch" role="tablist" aria-label="创建卡组方式">
                        <button class="deck-mode-btn selected" data-mode="create" type="button">新建卡组</button>
                        <button class="deck-mode-btn" data-mode="import" type="button">导入代码</button>
                    </div>

                    <div class="deck-create-panel" data-mode-panel="create">
                        <div class="deck-create-step" data-step="environment">
                            <div class="deck-create-step-title">选择环境</div>
                            <div class="deck-create-options">
                                ${ENVIRONMENT_OPTIONS.map(option => `
                                    <button class="deck-option" data-env="${option.value}" type="button">
                                        <div class="deck-option-title">${option.label}</div>
                                        <div class="deck-option-desc">${option.description}</div>
                                    </button>
                                `).join('')}
                            </div>
                        </div>
                        <div class="deck-create-step hidden" data-step="class">
                            <div class="deck-create-step-title">选择职业</div>
                            <div class="deck-create-options grid">
                                ${CLASS_OPTIONS.map(option => `
                                    <button class="deck-option" data-class="${option.value}" type="button">
                                        <div class="deck-option-title">${option.label}</div>
                                    </button>
                                `).join('')}
                            </div>
                        </div>

                        <div class="deck-create-actions" id="deck-create-create-actions">
                            <button class="btn-secondary" id="deck-create-back" type="button">Back</button>
                            <button class="btn-primary" id="deck-create-next" type="button">Next</button>
                            <button class="btn-primary" id="deck-create-confirm" type="button">创建</button>
                        </div>
                    </div>

                    <div class="deck-create-panel hidden" data-mode-panel="import">
                        <div class="deck-create-step-title">导入卡组代码</div>
                        <label class="deck-import-field">
                            <span class="deck-import-label">卡组名称（可选）</span>
                            <input id="deck-import-name-input" class="deck-import-input" type="text" maxlength="64" placeholder="Imported Deck">
                        </label>
                        <label class="deck-import-field">
                            <span class="deck-import-label">卡组代码</span>
                            <textarea id="deck-import-code-input" class="deck-import-textarea" rows="5" placeholder="粘贴导出的卡组代�?></textarea>
                        </label>
                        <div class="deck-import-hint">使用导出的代码直接创建卡组，创建后将进入卡组编辑�?/div>

                        <div class="deck-create-actions" id="deck-create-import-actions">
                            <button class="btn-primary" id="deck-import-submit-btn" type="button">Import &amp; Create</button>
                        </div>
                    </div>
                </div>
            </div>

            <div class="deck-preview-modal" id="deck-preview-modal" aria-hidden="true">
                <div class="deck-preview-dialog" role="dialog" aria-modal="true" aria-labelledby="deck-preview-title">
                    <div class="deck-preview-toolbar">
                        <div class="deck-preview-toolbar-left">
                            <div class="deck-preview-title-group">
                                <div class="deck-preview-caption">卡组预览</div>
                                <h3 class="deck-preview-title" id="deck-preview-title">-</h3>
                                <div class="deck-preview-meta" id="deck-preview-meta">-</div>
                            </div>
                        </div>
                        <div class="deck-preview-toolbar-actions">
                            <button class="btn-secondary" id="deck-preview-export-btn" type="button">导出卡组代码</button>
                            <button class="btn-secondary" id="deck-preview-close-btn" type="button" aria-label="关闭预览">关闭</button>
                        </div>
                    </div>

                    <div class="deck-preview-summary">
                        <div class="deck-preview-curve" id="deck-preview-curve"></div>
                        <div class="deck-preview-counts" id="deck-preview-counts"></div>
                    </div>

                    <div class="deck-preview-cards" id="deck-preview-cards"></div>
                </div>
            </div>

            <div class="deck-code-modal" id="deck-code-modal" aria-hidden="true">
                <div class="deck-code-dialog" role="dialog" aria-modal="true" aria-labelledby="deck-code-title">
                    <div class="deck-code-header">
                        <div>
                            <div class="deck-code-caption">卡组代码</div>
                            <h3 class="deck-code-title" id="deck-code-title">-</h3>
                        </div>
                        <button class="btn-secondary small" id="deck-code-close-btn" type="button" aria-label="关闭导出">关闭</button>
                    </div>
                    <textarea class="deck-code-output" id="deck-code-output" readonly></textarea>
                    <div class="deck-code-footer">
                        <span class="deck-code-status" id="deck-code-status"></span>
                        <button class="btn-primary" id="deck-code-copy-btn" type="button">复制代码</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Event Listeners
    const deckNameLookup = new Map(
        state.decks.map(deck => [String(getDeckId(deck)), resolveDeckName(deck)])
    );
    const createModal = document.getElementById('deck-create-modal');
    const createClose = document.getElementById('deck-create-close');
    const createBack = document.getElementById('deck-create-back');
    const createNext = document.getElementById('deck-create-next');
    const createConfirm = document.getElementById('deck-create-confirm');
    const createModeButtons = createModal?.querySelectorAll('.deck-mode-btn');
    const createPanel = createModal?.querySelector('[data-mode-panel="create"]');
    const importPanel = createModal?.querySelector('[data-mode-panel="import"]');
    const importNameInput = document.getElementById('deck-import-name-input');
    const importCodeInput = document.getElementById('deck-import-code-input');
    const importSubmit = document.getElementById('deck-import-submit-btn');
    const envStep = createModal?.querySelector('[data-step="environment"]');
    const classStep = createModal?.querySelector('[data-step="class"]');
    const previewModal = document.getElementById('deck-preview-modal');
    const previewTitle = document.getElementById('deck-preview-title');
    const previewMeta = document.getElementById('deck-preview-meta');
    const previewCurve = document.getElementById('deck-preview-curve');
    const previewCounts = document.getElementById('deck-preview-counts');
    const previewCards = document.getElementById('deck-preview-cards');
    const previewCloseBtn = document.getElementById('deck-preview-close-btn');
    const previewBackBtn = document.getElementById('deck-preview-back-btn');
    const previewExportBtn = document.getElementById('deck-preview-export-btn');
    const codeModal = document.getElementById('deck-code-modal');
    const codeCloseBtn = document.getElementById('deck-code-close-btn');
    const codeCopyBtn = document.getElementById('deck-code-copy-btn');
    const codeOutput = document.getElementById('deck-code-output');
    const codeTitle = document.getElementById('deck-code-title');
    const codeStatus = document.getElementById('deck-code-status');

    let previewDeckId = null;
    let previewDeckName = '';
    let activeDeckCode = '';

    const resetDeckCreation = () => {
        state.deckCreation = {
            mode: 'create',
            environment: null,
            class: null,
            step: 'environment',
            importName: '',
            importCode: ''
        };
    };

    const syncDeckCreationUI = () => {
        if (!createModal || !envStep || !classStep || !createBack || !createNext || !createConfirm) return;
        const isImportMode = state.deckCreation.mode === 'import';
        const isEnvStep = state.deckCreation.step === 'environment';
        createPanel?.classList.toggle('hidden', isImportMode);
        importPanel?.classList.toggle('hidden', !isImportMode);
        envStep.classList.toggle('hidden', isImportMode || !isEnvStep);
        classStep.classList.toggle('hidden', isImportMode || isEnvStep);
        createBack.disabled = isImportMode || isEnvStep;
        createNext.style.display = isImportMode ? 'none' : (isEnvStep ? 'inline-flex' : 'none');
        createConfirm.style.display = isImportMode ? 'none' : (isEnvStep ? 'none' : 'inline-flex');
        createNext.disabled = isImportMode || !state.deckCreation.environment;
        createConfirm.disabled = isImportMode || !state.deckCreation.class;
        if (importSubmit) {
            importSubmit.disabled = !state.deckCreation.importCode?.trim();
        }
        if (importNameInput && importNameInput.value !== state.deckCreation.importName) {
            importNameInput.value = state.deckCreation.importName;
        }
        if (importCodeInput && importCodeInput.value !== state.deckCreation.importCode) {
            importCodeInput.value = state.deckCreation.importCode;
        }

        createModeButtons?.forEach(button => {
            const mode = button.getAttribute('data-mode');
            button.classList.toggle('selected', mode === state.deckCreation.mode);
        });

        envStep.querySelectorAll('.deck-option').forEach(option => {
            const value = option.getAttribute('data-env');
            option.classList.toggle('selected', value === state.deckCreation.environment);
        });
        classStep.querySelectorAll('.deck-option').forEach(option => {
            const value = option.getAttribute('data-class');
            option.classList.toggle('selected', value === state.deckCreation.class);
        });
    };

    const openDeckCreation = () => {
        if (!createModal) return;
        resetDeckCreation();
        createModal.classList.add('open');
        createModal.setAttribute('aria-hidden', 'false');
        syncDeckCreationUI();
    };

    const closeDeckPreview = () => {
        if (!previewModal) return;
        previewModal.classList.remove('open');
        previewModal.setAttribute('aria-hidden', 'true');
        previewDeckId = null;
    };

    const closeDeckCode = () => {
        if (!codeModal) return;
        codeModal.classList.remove('open');
        codeModal.setAttribute('aria-hidden', 'true');
        activeDeckCode = '';
        if (codeStatus) codeStatus.textContent = '';
    };

    const setCodeStatus = (message, isError = false) => {
        if (!codeStatus) return;
        codeStatus.textContent = message;
        codeStatus.classList.toggle('error', Boolean(isError));
    };

    const openDeckCode = (deckName, code) => {
        if (!codeModal || !codeOutput || !codeTitle) return;
        codeTitle.textContent = deckName;
        codeOutput.value = code;
        activeDeckCode = code;
        setCodeStatus('Code generated. You can copy it directly.');
        codeModal.classList.add('open');
        codeModal.setAttribute('aria-hidden', 'false');
    };

    const openDeckCodeById = async (deckId, fallbackName = 'New Deck') => {
        const deckName = deckNameLookup.get(String(deckId)) || fallbackName;
        const code = await fetchDeckCode(deckId);
        openDeckCode(deckName, code);
    };

    const openDeckPreview = async (deckId) => {
        if (!previewModal || !previewTitle || !previewMeta || !previewCurve || !previewCounts || !previewCards) return;

        const deckDetail = await fetchDeckDetail(deckId);
        const previewSnapshot = buildDeckPreviewSnapshot(deckDetail);
        const deckSummary = state.decks.find(deck => String(getDeckId(deck)) === String(deckId)) ?? deckDetail;
        const stats = getDeckMatchStats(deckSummary);
        const overallWinRate = formatDeckWinRate(stats.totalWins, stats.totalGames);
        previewDeckId = deckId;
        previewDeckName = resolveDeckName(deckDetail);

        previewTitle.textContent = previewDeckName;
        previewMeta.textContent = `${getClassLabel(deckDetail.class)} | ${getEnvironmentLabel(deckDetail.environment)}`;
        previewCurve.innerHTML = renderDeckPreviewCurveHtml(previewSnapshot.curve);
        previewCounts.innerHTML = `
            <div class="deck-preview-total">${previewSnapshot.totalCount} / 40</div>
            <div class="deck-preview-type-row">
                <span>随从: ${previewSnapshot.typeCounts.follower}</span>
                <span>法术: ${previewSnapshot.typeCounts.spell}</span>
                <span>护符: ${previewSnapshot.typeCounts.amulet}</span>
            </div>
            <div class="deck-preview-match-row">
                <span>Games: ${stats.totalGames}</span>
                <span>Win Rate: ${overallWinRate}</span>
            </div>
            <div class="deck-preview-match-row detail">
                <span>First: ${formatDeckWinRateWithGames(stats.goingFirstWins, stats.goingFirstGames)}</span>
                <span>Second: ${formatDeckWinRateWithGames(stats.goingSecondWins, stats.goingSecondGames)}</span>
            </div>
        `;
        previewCards.innerHTML = renderDeckPreviewCardsHtml(previewSnapshot.items);
        previewCards.querySelectorAll('.deck-preview-card').forEach(cardEl => {
            cardEl.addEventListener('click', (event) => {
                event.stopPropagation();
                const cardId = Number(cardEl.getAttribute('data-card-id'));
                const card = state.cardIndex.get(cardId);
                openCardDetailModal(card);
            });
        });

        previewModal.classList.add('open');
        previewModal.setAttribute('aria-hidden', 'false');
    };

    const closeDeckCreation = () => {
        if (!createModal) return;
        createModal.classList.remove('open');
        createModal.setAttribute('aria-hidden', 'true');
        resetDeckCreation();
    };

    document.getElementById('create-deck-card').addEventListener('click', openDeckCreation);
    createClose?.addEventListener('click', closeDeckCreation);
    createModal?.addEventListener('click', (event) => {
        if (event.target === createModal) {
            closeDeckCreation();
        }
    });
    createModeButtons?.forEach(button => {
        button.addEventListener('click', () => {
            const mode = button.getAttribute('data-mode');
            if (mode !== 'create' && mode !== 'import') return;
            state.deckCreation.mode = mode;
            syncDeckCreationUI();
        });
    });
    importNameInput?.addEventListener('input', (event) => {
        state.deckCreation.importName = event.target.value;
    });
    importCodeInput?.addEventListener('input', (event) => {
        state.deckCreation.importCode = event.target.value;
        syncDeckCreationUI();
    });
    previewModal?.addEventListener('click', (event) => {
        if (event.target === previewModal) {
            closeDeckPreview();
        }
    });
    codeModal?.addEventListener('click', (event) => {
        if (event.target === codeModal) {
            closeDeckCode();
        }
    });
    previewCloseBtn?.addEventListener('click', closeDeckPreview);
    previewBackBtn?.addEventListener('click', closeDeckPreview);
    codeCloseBtn?.addEventListener('click', closeDeckCode);
    previewExportBtn?.addEventListener('click', () => {
        if (!previewDeckId) return;
        openDeckCodeById(previewDeckId, previewDeckName).catch(err => {
            alert(`导出失败�${err.message}`);
        });
    });
    codeCopyBtn?.addEventListener('click', () => {
        if (!activeDeckCode) return;
        copyTextToClipboard(activeDeckCode)
            .then(() => setCodeStatus('Copied successfully.'))
            .catch(err => setCodeStatus(`复制失败�${err.message}`, true));
    });

    envStep?.querySelectorAll('.deck-option').forEach(option => {
        option.addEventListener('click', () => {
            const value = option.getAttribute('data-env');
            state.deckCreation.environment = value;
            syncDeckCreationUI();
        });
    });

    classStep?.querySelectorAll('.deck-option').forEach(option => {
        option.addEventListener('click', () => {
            const value = option.getAttribute('data-class');
            state.deckCreation.class = value;
            syncDeckCreationUI();
        });
    });

    createNext?.addEventListener('click', () => {
        if (!state.deckCreation.environment) return;
        state.deckCreation.step = 'class';
        syncDeckCreationUI();
    });

    createBack?.addEventListener('click', () => {
        state.deckCreation.step = 'environment';
        syncDeckCreationUI();
    });

    createConfirm?.addEventListener('click', () => {
        if (!state.deckCreation.environment || !state.deckCreation.class) return;
        const env = state.deckCreation.environment;
        const deckClass = state.deckCreation.class;
        closeDeckCreation();
        createNewDeck(env, deckClass).catch(err => alert(`创建失败�${err.message}`));
    });
    importSubmit?.addEventListener('click', async () => {
        const code = state.deckCreation.importCode?.trim();
        if (!code) return;
        const name = state.deckCreation.importName?.trim() || '';
        importSubmit.disabled = true;
        try {
            const importedDeckId = await importDeckByCode(code, name);
            closeDeckCreation();
            if (importedDeckId) {
                await editDeck(importedDeckId);
                return;
            }
            renderDeckSelection();
        } catch (err) {
            alert(`导入失败�${err.message}`);
            importSubmit.disabled = false;
            syncDeckCreationUI();
        }
    });

    document.querySelectorAll('.preview-deck-btn').forEach(btn => {
        btn.addEventListener('click', (event) => {
            event.stopPropagation();
            openDeckPreview(btn.dataset.id).catch(err => alert(`预览失败�${err.message}`));
        });
    });
    
    document.querySelectorAll('.edit-deck-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            editDeck(e.target.dataset.id).catch(err => alert(`加载失败�${err.message}`));
        });
    });
    
    document.querySelectorAll('.delete-deck-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            deleteDeck(e.target.dataset.id).catch(err => alert(`删除失败�${err.message}`));
        });
    });

    // Allow clicking the whole card to edit
    document.querySelectorAll('.deck-card.wide-banner').forEach(card => {
        card.addEventListener('click', () => {
            editDeck(card.dataset.id).catch(err => alert(`加载失败�${err.message}`));
        });
    });
}

// --- Rendering: Deck Builder View ---

function renderDeckBuilder() {
    if (!state.currentDeck) return;

    container.innerHTML = `
        <div class="deck-builder-view vertical-layout">
            <!-- Top Section: Deck Info & Contents -->
            <div class="deck-section">
                <div class="deck-header-row">
                    <div class="deck-identity">
                        <div class="class-icon-small"></div>
                        <input type="text" class="deck-name-input" value="${state.currentDeck.name}" id="deck-name-input">
                        <button class="btn-icon edit-name-btn">Edit</button>
                        <div class="deck-env-tag">${getEnvironmentLabel(state.currentDeck.environment)}</div>
                    </div>
                    
                    <div class="mana-curve-mini" id="mana-curve">
                        <!-- Mana curve bars -->
                    </div>

                    <div class="deck-meta-controls">
                        <div class="deck-counts">
                            <span class="total-count"><span id="deck-count-display">${getDeckCardCount(state.currentDeck)}</span> / 40</span>
                            <div class="type-counts">
                                <span class="type-count follower">Follower: 0</span>
                                <span class="type-count spell">Spell: 0</span>
                                <span class="type-count amulet">Amulet: 0</span>
                            </div>
                        </div>
                        <div class="header-actions">
                            <button class="btn-primary" id="save-deck-btn">Confirm Deck</button>
                            <button class="btn-secondary" id="back-to-selection-btn">Back</button>
                        </div>
                    </div>
                </div>

                <!-- Action Bar -->
                <div class="deck-action-bar">
                    <button class="btn-secondary small">Save</button>
                    <button class="btn-secondary small">CRAFT MISSING</button>
                    <button class="btn-secondary small">Auto Edit</button>
                    <div class="spacer"></div>
                    <div class="filter-bar-mini">
                        ${[1, 2, 3, 4, 5, 6, 7, 8].map(cost => `
                            <button class="filter-btn cost-btn" data-cost="${cost}">${cost}</button>
                        `).join('')}
                    </div>
                </div>

                <!-- Horizontal Deck List -->
                <div class="deck-contents-scroll" id="deck-list">
                    <!-- Deck cards injected here -->
                </div>
            </div>

            <!-- Bottom Section: Library -->
            <div class="library-section">
                <div class="library-search-row">
                    <div class="search-section">
                        <input type="text" id="card-search-input" placeholder="SEARCH LIBRARY..." class="search-input tech-input">
                        <div class="filter-section">
                            <select id="library-class-filter" class="filter-select tech-select">
                                <option value="">ALL CLASSES</option>
                            </select>
                            <select id="library-type-filter" class="filter-select tech-select">
                                <option value="">ALL TYPES</option>
                            </select>
                            <select id="library-rarity-filter" class="filter-select tech-select">
                                <option value="">ALL RARITIES</option>
                            </select>
                            <select id="library-set-filter" class="filter-select tech-select">
                                <option value="">ALL SETS</option>
                            </select>
                        </div>
                    </div>
                </div>
                <div class="library-grid" id="library-grid">
                    <!-- Library cards injected here -->
                </div>
                
                <div class="library-footer">
                    <div class="footer-left">
                        <button class="btn-tab active">Owned Cards</button>
                        <button class="btn-tab">Craft Mode</button>
                    </div>
                    <div class="footer-center">
                        <span class="page-indicator">1 / 18</span>
                    </div>
                    <div class="footer-right"></div>
                </div>
            </div>
        </div>
    `;

    populateLibraryFilters();
    renderLibrary();
    renderDeckList();
    renderManaCurve();

    // Event Listeners
    document.getElementById('card-search-input').addEventListener('input', (e) => {
        state.filters.search = e.target.value;
        state.pagination.currentPage = 1;
        renderLibrary();
    });

    document.getElementById('library-class-filter').addEventListener('change', (e) => {
        state.filters.class = e.target.value;
        state.pagination.currentPage = 1;
        renderLibrary();
    });

    document.getElementById('library-type-filter').addEventListener('change', (e) => {
        state.filters.type = e.target.value;
        state.pagination.currentPage = 1;
        renderLibrary();
    });

    document.getElementById('library-rarity-filter').addEventListener('change', (e) => {
        state.filters.rarity = e.target.value;
        state.pagination.currentPage = 1;
        renderLibrary();
    });

    document.getElementById('library-set-filter').addEventListener('change', (e) => {
        state.filters.set = e.target.value;
        state.pagination.currentPage = 1;
        renderLibrary();
    });

    document.querySelectorAll('.cost-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const cost = parseInt(btn.dataset.cost);
            state.filters.cost = state.filters.cost === cost ? null : cost;
            
            // Update UI
            document.querySelectorAll('.cost-btn').forEach(b => b.classList.remove('active'));
            if (state.filters.cost) btn.classList.add('active');
            
            state.pagination.currentPage = 1;
            renderLibrary();
        });
    });

    document.getElementById('back-to-selection-btn').addEventListener('click', () => {
        if (!state.currentDeck) {
            renderDeckSelection();
            return;
        }
        if (getDeckCardCount(state.currentDeck) > 0) {
            saveDeck().catch(err => alert(`保存失败�${err.message}`));
            return;
        }
        const deckId = state.currentDeck.deckId || state.currentDeck.id;
        const isNewDeck = state.currentDeck.isNew === true;
        state.currentDeck = null;
        if (deckId && isNewDeck) {
            deleteDeck(deckId, true).catch(err => alert(`删除失败�${err.message}`));
            return;
        }
        renderDeckSelection();
    });

    document.getElementById('save-deck-btn').addEventListener('click', () => {
        saveDeck().catch(err => alert(`保存失败�${err.message}`));
    });
    document.getElementById('deck-name-input').addEventListener('change', (e) => {
        state.currentDeck.name = e.target.value;
    });
}

function getFilteredCards() {
    const filtered = state.availableCards.filter(card => {
        if (state.currentDeck) {
            if (!isCardAllowedByEnvironment(card, state.currentDeck.environment)) return false;
            if (!isCardAllowedByClass(card, state.currentDeck.class)) return false;
        }
        if (state.filters.cost && card.cost !== state.filters.cost) return false;
        if (state.filters.class) {
            const cardClass = normalizeEnumValue(card?.class ?? card?.Class ?? card?.cardClass ?? card?.card_class);
            if (cardClass !== state.filters.class) return false;
        }
        if (state.filters.type) {
            const cardType = normalizeEnumValue(card?.type ?? card?.Type ?? card?.cardType ?? card?.card_type);
            if (cardType !== state.filters.type) return false;
        }
        if (state.filters.rarity) {
            const rarity = normalizeEnumValue(card?.rarity ?? card?.Rarity ?? '');
            if (rarity !== state.filters.rarity) return false;
        }
        if (state.filters.set) {
            const info = getCardSetInfo(card);
            const setValue = normalizeEnumValue(info.name);
            if (!setValue || setValue !== state.filters.set) return false;
        }
        const name = String(card.name || '').toLowerCase();
        if (state.filters.search && !name.includes(state.filters.search.toLowerCase())) return false;
        return true;
    });

    return filtered.sort(compareCardsForEditorAndDeck);
}

function getLibraryPageCapacity(grid) {
    const sampleCard = grid.querySelector('.library-card');
    if (!sampleCard) return null;
    const gridStyle = window.getComputedStyle(grid);
    const gap = parseFloat(gridStyle.gap || gridStyle.rowGap || '0');
    const cardRect = sampleCard.getBoundingClientRect();
    const gridRect = grid.getBoundingClientRect();
    const columns = Math.max(1, Math.floor((gridRect.width + gap) / (cardRect.width + gap)));
    const rows = Math.max(1, Math.floor((gridRect.height + gap) / (cardRect.height + gap)));
    return Math.max(1, columns * rows);
}

function renderLibrary(skipCapacitySync = false) {
    const grid = document.getElementById('library-grid');
    if (!grid) return;

    const filteredCards = getFilteredCards();
    const totalPages = Math.ceil(filteredCards.length / state.pagination.itemsPerPage) || 1;
    
    // Ensure current page is valid
    if (state.pagination.currentPage > totalPages) state.pagination.currentPage = totalPages;

    const startIndex = (state.pagination.currentPage - 1) * state.pagination.itemsPerPage;
    const paginatedCards = filteredCards.slice(startIndex, startIndex + state.pagination.itemsPerPage);

    grid.innerHTML = paginatedCards.map(card => {
        const attack = card.attack ?? '-';
        const health = card.health ?? '-';
        const preview = card.artPreview ?? card.ArtPreview ?? '';
        const previewUrl = preview ? encodeURI(preview) : '';
        const rarityClass = getCardRarityClass(card);
        const cardClassName = `library-card${previewUrl ? ' has-art' : ''}${rarityClass ? ` ${rarityClass}` : ''}`;
        const styleAttr = previewUrl ? ` style="--library-card-art: url('${previewUrl}');"` : '';
        return `
        <div class="${cardClassName}" data-card-id="${card.id}"${styleAttr}>
            <button class="card-detail-btn" data-card-id="${card.id}" type="button">DETAIL</button>
            <div class="card-cost">${card.cost ?? '-'}</div>
            <div class="card-name">${card.name ?? '-'}</div>
            <div class="card-stats">${attack}/${health}</div>
            <div class="card-overlay">
                <span class="add-icon">+</span>
            </div>
        </div>
        `;
    }).join('');

    if (!skipCapacitySync && paginatedCards.length > 0) {
        const capacity = getLibraryPageCapacity(grid);
        if (capacity && capacity !== state.pagination.itemsPerPage) {
            state.pagination.itemsPerPage = capacity;
            state.pagination.currentPage = 1;
            renderLibrary(true);
            return;
        }
    }

    // Update Footer Pagination UI
    const footerCenter = document.querySelector('.library-footer .footer-center');
    if (footerCenter) {
        footerCenter.innerHTML = `
            <button class="btn-secondary small btn-prev" ${state.pagination.currentPage === 1 ? 'disabled' : ''}><</button>
            <span class="page-indicator">${state.pagination.currentPage} / ${totalPages}</span>
            <button class="btn-secondary small btn-next" ${state.pagination.currentPage === totalPages ? 'disabled' : ''}>></button>
        `;

        const prevBtn = footerCenter.querySelector('.btn-prev');
        const nextBtn = footerCenter.querySelector('.btn-next');
        prevBtn?.addEventListener('click', () => {
            if (state.pagination.currentPage <= 1) return;
            state.pagination.currentPage -= 1;
            renderLibrary(true);
        });
        nextBtn?.addEventListener('click', () => {
            if (state.pagination.currentPage >= totalPages) return;
            state.pagination.currentPage += 1;
            renderLibrary(true);
        });
    }

    // Add click listeners
    grid.querySelectorAll('.library-card').forEach(el => {
        el.addEventListener('click', (event) => {
            if (event.target.closest('.card-detail-btn')) {
                return;
            }
            addCardToDeck(Number(el.dataset.cardId));
        });
    });

    grid.querySelectorAll('.card-detail-btn').forEach(btn => {
        btn.addEventListener('click', (event) => {
            event.stopPropagation();
            const cardId = Number(btn.dataset.cardId);
            const card = state.cardIndex.get(cardId);
            openCardDetailModal(card);
        });
    });
}

function renderDeckList() {
    const list = document.getElementById('deck-list');
    if (!list) return;

    const entries = Array.isArray(state.currentDeck.cards) ? state.currentDeck.cards : [];
    const deckCards = entries
        .map(entry => ({
            entry,
            card: state.cardIndex.get(Number(entry.cardDefinitionId))
        }))
        .filter(item => item.card)
        .sort((a, b) => compareCardsForEditorAndDeck(a.card, b.card));

    // Render as vertical cards (miniature) for horizontal scroll
    list.innerHTML = deckCards.map(item => {
        const preview = item.card.artPreview ?? item.card.ArtPreview ?? '';
        const previewUrl = preview ? encodeURI(preview) : '';
        const visualClass = `mini-visual${previewUrl ? ' has-art' : ''}`;
        const visualStyle = previewUrl ? ` style="background-image: url('${previewUrl}');"` : '';
        const rarityClass = getCardRarityClass(item.card);
        return `
        <div class="deck-card-mini${rarityClass ? ` ${rarityClass}` : ''}" data-card-id="${item.entry.cardDefinitionId}">
            <button class="mini-detail-btn" data-card-id="${item.entry.cardDefinitionId}" type="button">DETAIL</button>
            <div class="mini-cost">${item.card.cost ?? '-'}</div>
            <div class="${visualClass}"${visualStyle}></div>
            <div class="mini-name">${item.card.name ?? '-'}</div>
            <div class="mini-count">x${item.entry.count}</div>
            <div class="mini-overlay">
                <span class="remove-icon">-</span>
            </div>
        </div>
    `;
    }).join('');

    // Add remove listeners
    list.querySelectorAll('.deck-card-mini').forEach(el => {
        el.addEventListener('click', (e) => {
            if (e.target.closest('.mini-detail-btn')) {
                return;
            }
            e.stopPropagation();
            removeCardFromDeck(Number(el.dataset.cardId));
        });
    });

    list.querySelectorAll('.mini-detail-btn').forEach(btn => {
        btn.addEventListener('click', (event) => {
            event.stopPropagation();
            const cardId = Number(btn.dataset.cardId);
            const card = state.cardIndex.get(cardId);
            openCardDetailModal(card);
        });
    });
}

function renderManaCurve() {
    const container = document.getElementById('mana-curve');
    if (!container) return;

    const curve = Array(9).fill(0); // 0-8+
    const entries = Array.isArray(state.currentDeck.cards) ? state.currentDeck.cards : [];
    entries.forEach(entry => {
        const card = state.cardIndex.get(Number(entry.cardDefinitionId));
        if (!card) return;
        const cost = Number(card.cost ?? 0);
        const index = Math.min(cost, 8);
        curve[index] += Number(entry.count ?? 0);
    });

    const max = Math.max(...curve, 1); // Avoid divide by zero

    container.innerHTML = curve.map((count, i) => `
        <div class="curve-bar-container" title="Cost ${i}: ${count}">
            <div class="curve-bar" style="height: ${(count / max) * 100}%"></div>
            <div class="curve-label">${i === 8 ? '8+' : i}</div>
        </div>
    `).join('');
}

function openCardDetailModal(card) {
    if (!card) {
        return;
    }

    const preview = card.artPreview ?? card.ArtPreview ?? '';
    const detail = card.artDetail ?? card.ArtDetail ?? '';
    const previewUrl = preview ? encodeURI(preview).replace(/'/g, '%27') : '';
    const detailUrl = detail ? encodeURI(detail).replace(/'/g, '%27') : '';

    showCardDetailModal(card, { previewUrl, detailUrl });
}

// --- Logic ---

function updateDeckCountDisplay() {
    const el = document.getElementById('deck-count-display');
    if (el && state.currentDeck) {
        el.textContent = `${getDeckCardCount(state.currentDeck)}`;
    }
}

async function deleteDeck(deckId, skipConfirm = false) {
    if (!deckId) return;
    if (!skipConfirm) {
        const ok = window.confirm("确定要删除该卡组吗？");
        if (!ok) return;
    }
    const query = getDeckOwnerQuery();
    await apiDelete(`/api/decks/${deckId}?${query}`);
    await loadDecks();
    renderDeckSelection();
}

async function createNewDeck(environment, deckClass) {
    const query = getDeckOwnerQuery();
    const payload = {
        name: 'New Deck',
        class: normalizeEnumValue(deckClass) || 'neutral',
        environment: normalizeEnvironment(environment)
    };
    const created = await apiPostJson(`/api/decks?${query}`, payload);
    const createdId = created?.deckId ?? created?.id ?? null;
    state.newDeckId = createdId;
    await loadDecks();
    if (createdId) {
        await editDeck(createdId);
    }
}

async function importDeckByCode(code, name = '') {
    const query = getDeckOwnerQuery();
    const payload = { code: String(code).trim() };
    if (name && String(name).trim()) {
        payload.name = String(name).trim();
    }
    const imported = await apiPostJson(`/api/decks/import?${query}`, payload);
    const importedDeckId = getDeckId(imported);
    await loadDecks();
    return importedDeckId;
}

async function editDeck(deckId) {
    const deck = await fetchDeckDetail(deckId);
    const resolvedDeckId = getDeckId(deck) ?? deckId;
    const isNewDeck = state.newDeckId && resolvedDeckId && String(state.newDeckId) === String(resolvedDeckId);
    state.currentDeck = {
        ...deck,
        environment: normalizeEnvironment(deck.environment),
        class: normalizeEnumValue(deck.class),
        cards: Array.isArray(deck.cards) ? deck.cards : []
    };
    state.currentDeck.isNew = Boolean(isNewDeck);
    if (isNewDeck) {
        state.newDeckId = null;
    }
    renderDeckBuilder();
}

function addCardToDeck(cardId) {
    if (!state.currentDeck) return;
    const totalCount = getDeckCardCount(state.currentDeck);
    if (totalCount >= 40) {
        alert("牌组已满（最�?40 张）");
        return;
    }

    const entries = state.currentDeck.cards ?? [];
    const entry = entries.find(item => Number(item.cardDefinitionId) === cardId);
    if (entry && entry.count >= 3) {
        alert("Max 3 copies per card.");
        return;
    }

    if (entry) {
        entry.count += 1;
    } else {
        entries.push({ cardDefinitionId: cardId, count: 1 });
    }
    state.currentDeck.cards = entries;
    renderDeckList();
    renderManaCurve();
    updateDeckCountDisplay();
}

function removeCardFromDeck(cardId) {
    if (!state.currentDeck) return;

    const entries = state.currentDeck.cards ?? [];
    const entryIndex = entries.findIndex(item => Number(item.cardDefinitionId) === cardId);
    if (entryIndex === -1) return;

    const entry = entries[entryIndex];
    if (entry.count > 1) {
        entry.count -= 1;
    } else {
        entries.splice(entryIndex, 1);
    }
    state.currentDeck.cards = entries;
    renderDeckList();
    renderManaCurve();
    updateDeckCountDisplay();
}

async function saveDeck() {
    if (!state.currentDeck) return;
    const query = getDeckOwnerQuery();
    const deckId = state.currentDeck.deckId || state.currentDeck.id;
    const payload = {
        name: state.currentDeck.name,
        class: state.currentDeck.class || 'Neutral',
        environment: normalizeEnvironment(state.currentDeck.environment),
        cards: state.currentDeck.cards ?? []
    };
    await apiPutJson(`/api/decks/${deckId}?${query}`, payload);
    await loadDecks();
    document.dispatchEvent(new CustomEvent('deckListUpdated'));
    state.currentDeck = null;
    renderDeckSelection();
}

