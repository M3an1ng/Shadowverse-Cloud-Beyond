// src/deckBuilder.js - Deck Builder Logic
//
// Implements the Deck Builder UI and logic.
// Follows the "Aetherial Architect" aesthetic.

import { router } from './router.js';
import { getDeckOwnerQuery } from './userSession.js';

const DEFAULT_ENVIRONMENT = 'open';
const ENVIRONMENT_OPTIONS = [
    {
        value: 'open',
        label: '开放环境',
        description: '所有卡包可用'
    }
];

const CLASS_OPTIONS = [
    { value: 'neutral', label: '中立' },
    { value: 'forestcraft', label: '妖精' },
    { value: 'swordcraft', label: '皇家护卫' },
    { value: 'runecraft', label: '巫师' },
    { value: 'dragoncraft', label: '龙族' },
    { value: 'shadowcraft', label: '死灵法师' },
    { value: 'bloodcraft', label: '吸血鬼' },
    { value: 'havencraft', label: '主教' },
    { value: 'portalcraft', label: '复仇者' }
];

// --- State Management ---
const state = {
    decks: [], // List of all decks
    currentDeck: null, // The deck currently being edited
    availableCards: [], // All cards available for building
    cardIndex: new Map(),
    deckCreation: {
        environment: null,
        class: null,
        step: 'environment'
    },
    filters: {
        cost: null,
        class: null,
        search: ''
    },
    pagination: {
        currentPage: 1,
        itemsPerPage: 10 // Fixed limit to prevent scrolling
    }
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
    const payload = await response.json();
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
    console.log("DeckBuilder: Initializing...");
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
    const cards = await apiGetJson('/api/cards');
    const filtered = Array.isArray(cards)
        ? cards.filter(card => Number(card?.id) > 0 && String(card?.type || '').toLowerCase() !== 'leader')
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

function formatClassName(value) {
    if (!value) return 'Neutral';
    const text = String(value);
    return text.charAt(0).toUpperCase() + text.slice(1);
}

function normalizeEnumValue(value) {
    if (value === null || value === undefined) return '';
    return String(value).trim().toLowerCase();
}

function normalizeEnvironment(value) {
    if (value === null || value === undefined || value === '') return DEFAULT_ENVIRONMENT;
    if (typeof value === 'number') {
        return value === 0 ? DEFAULT_ENVIRONMENT : String(value);
    }
    return normalizeEnumValue(value);
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

function getCardSet(card) {
    return card?.card_set ?? card?.cardSet ?? card?.cardSetName ?? '';
}

function isCardAllowedByEnvironment(card, environment) {
    const normalized = normalizeEnvironment(environment);
    if (normalized === DEFAULT_ENVIRONMENT) {
        return true;
    }
    // TODO: Add concrete environment rules when formats are defined.
    return true;
}

function isCardAllowedByClass(card, deckClass) {
    const cardClass = normalizeEnumValue(card?.class ?? card?.cardClass);
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
                    const deckId = deck.deckId || deck.id;
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
                                <div class="deck-name">${deck.name}</div>
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
                            <button class="btn-primary edit-deck-btn" data-id="${deckId}">Edit Deck</button>
                            <button class="btn-icon delete-deck-btn" data-id="${deckId}">Delete</button>
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
                    <div class="deck-create-actions">
                        <button class="btn-secondary" id="deck-create-back" type="button">上一步</button>
                        <button class="btn-primary" id="deck-create-next" type="button">下一步</button>
                        <button class="btn-primary" id="deck-create-confirm" type="button">创建</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Event Listeners
    const createModal = document.getElementById('deck-create-modal');
    const createClose = document.getElementById('deck-create-close');
    const createBack = document.getElementById('deck-create-back');
    const createNext = document.getElementById('deck-create-next');
    const createConfirm = document.getElementById('deck-create-confirm');
    const envStep = createModal?.querySelector('[data-step="environment"]');
    const classStep = createModal?.querySelector('[data-step="class"]');

    const resetDeckCreation = () => {
        state.deckCreation = {
            environment: null,
            class: null,
            step: 'environment'
        };
    };

    const syncDeckCreationUI = () => {
        if (!createModal || !envStep || !classStep || !createBack || !createNext || !createConfirm) return;
        const isEnvStep = state.deckCreation.step === 'environment';
        envStep.classList.toggle('hidden', !isEnvStep);
        classStep.classList.toggle('hidden', isEnvStep);
        createBack.disabled = isEnvStep;
        createNext.style.display = isEnvStep ? 'inline-flex' : 'none';
        createConfirm.style.display = isEnvStep ? 'none' : 'inline-flex';
        createNext.disabled = !state.deckCreation.environment;
        createConfirm.disabled = !state.deckCreation.class;

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
        createNewDeck(env, deckClass).catch(err => alert(`创建失败：${err.message}`));
    });
    
    document.querySelectorAll('.edit-deck-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            editDeck(e.target.dataset.id).catch(err => alert(`加载失败：${err.message}`));
        });
    });
    
    document.querySelectorAll('.delete-deck-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            deleteDeck(e.target.dataset.id).catch(err => alert(`删除失败：${err.message}`));
        });
    });

    // Allow clicking the whole card to edit
    document.querySelectorAll('.deck-card.wide-banner').forEach(card => {
        card.addEventListener('click', () => {
            editDeck(card.dataset.id).catch(err => alert(`加载失败：${err.message}`));
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
                        <button class="btn-icon edit-name-btn">✎</button>
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
                    <button class="btn-secondary small">Craft Missing</button>
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
                    <div class="footer-right">
                        <div class="search-bar">
                            <input type="text" placeholder="Search cards..." id="card-search-input">
                            <span class="search-icon">🔍</span>
                        </div>
                        <button class="btn-secondary">Filter</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    renderLibrary();
    renderDeckList();
    renderManaCurve();

    // Event Listeners
    document.getElementById('card-search-input').addEventListener('input', (e) => {
        state.filters.search = e.target.value;
        renderLibrary();
    });

    document.querySelectorAll('.cost-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const cost = parseInt(btn.dataset.cost);
            state.filters.cost = state.filters.cost === cost ? null : cost;
            
            // Update UI
            document.querySelectorAll('.cost-btn').forEach(b => b.classList.remove('active'));
            if (state.filters.cost) btn.classList.add('active');
            
            renderLibrary();
        });
    });

    document.getElementById('back-to-selection-btn').addEventListener('click', () => {
        state.currentDeck = null;
        renderDeckSelection();
    });

    document.getElementById('save-deck-btn').addEventListener('click', () => {
        saveDeck().catch(err => alert(`保存失败：${err.message}`));
    });
    document.getElementById('deck-name-input').addEventListener('change', (e) => {
        state.currentDeck.name = e.target.value;
    });
}

function getFilteredCards() {
    return state.availableCards.filter(card => {
        if (state.currentDeck) {
            if (!isCardAllowedByEnvironment(card, state.currentDeck.environment)) return false;
            if (!isCardAllowedByClass(card, state.currentDeck.class)) return false;
        }
        if (state.filters.cost && card.cost !== state.filters.cost) return false;
        const name = String(card.name || '').toLowerCase();
        if (state.filters.search && !name.includes(state.filters.search.toLowerCase())) return false;
        return true;
    });
}

function renderLibrary() {
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
        return `
        <div class="library-card" data-card-id="${card.id}">
            <div class="card-cost">${card.cost ?? '-'}</div>
            <div class="card-name">${card.name ?? '-'}</div>
            <div class="card-stats">${attack}/${health}</div>
            <div class="card-overlay">
                <span class="add-icon">+</span>
            </div>
        </div>
        `;
    }).join('');

    // Update Footer Pagination UI
    const footerCenter = document.querySelector('.library-footer .footer-center');
    if (footerCenter) {
        footerCenter.innerHTML = `
            <button class="btn-secondary small btn-prev" ${state.pagination.currentPage === 1 ? 'disabled' : ''}><</button>
            <span class="page-indicator">${state.pagination.currentPage} / ${totalPages}</span>
            <button class="btn-secondary small btn-next" ${state.pagination.currentPage === totalPages ? 'disabled' : ''}>></button>
        `;
    }

    // Add click listeners
    grid.querySelectorAll('.library-card').forEach(el => {
        el.addEventListener('click', () => addCardToDeck(Number(el.dataset.cardId)));
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
        .sort((a, b) => (a.card.cost ?? 0) - (b.card.cost ?? 0));

    // Render as vertical cards (miniature) for horizontal scroll
    list.innerHTML = deckCards.map(item => `
        <div class="deck-card-mini" data-card-id="${item.entry.cardDefinitionId}">
            <div class="mini-cost">${item.card.cost ?? '-'}</div>
            <div class="mini-visual"></div>
            <div class="mini-name">${item.card.name ?? '-'}</div>
            <div class="mini-count">x${item.entry.count}</div>
            <div class="mini-overlay">
                <span class="remove-icon">-</span>
            </div>
        </div>
    `).join('');

    // Add remove listeners
    list.querySelectorAll('.deck-card-mini').forEach(el => {
        el.addEventListener('click', (e) => {
            e.stopPropagation();
            removeCardFromDeck(Number(el.dataset.cardId));
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

// --- Logic ---

function updateDeckCountDisplay() {
    const el = document.getElementById('deck-count-display');
    if (el && state.currentDeck) {
        el.textContent = `${getDeckCardCount(state.currentDeck)}`;
    }
}

async function deleteDeck(deckId) {
    if (!deckId) return;
    const ok = window.confirm("确定要删除该卡组吗？");
    if (!ok) return;
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
    await loadDecks();
    await editDeck(created.deckId);
}

async function editDeck(deckId) {
    const query = getDeckOwnerQuery();
    const deck = await apiGetJson(`/api/decks/${deckId}?${query}`);
    state.currentDeck = {
        ...deck,
        environment: normalizeEnvironment(deck.environment),
        cards: Array.isArray(deck.cards) ? deck.cards : []
    };
    renderDeckBuilder();
}

function addCardToDeck(cardId) {
    if (!state.currentDeck) return;
    const totalCount = getDeckCardCount(state.currentDeck);
    if (totalCount >= 40) {
        alert("牌组已满（最多 40 张）");
        return;
    }

    const entries = state.currentDeck.cards ?? [];
    const entry = entries.find(item => Number(item.cardDefinitionId) === cardId);
    if (entry && entry.count >= 3) {
        alert("同名卡最多 3 张");
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
    state.currentDeck = null;
    renderDeckSelection();
}
