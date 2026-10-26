// src/deckBuilder.js - Deck Builder Logic
//
// Implements the Deck Builder UI and logic.
// Follows the "Aetherial Architect" aesthetic.

import { getDeckOwnerQuery } from './userSession.js';
import { showCardDetailModal } from './components/CardDetailMockup.js';
import { createDeckPreviewModal } from './components/deckPreviewModal.js';
import { compareCardsForEditorAndDeck } from './cardListSort.js';
import {
    CARD_SET_UNSPECIFIED_ENVIRONMENT,
    buildGroupedCardSetOptions
} from './cardSetFilterOptions.js';
import { matchesCardLibrarySearch } from './cardLibrarySearch.js';
import { loadCardDefinitions } from './cardDefinitionsLoader.js';
import classNeutralIconUrl from './icon/svg/class_neutral.svg';
import classForestcraftIconUrl from './icon/svg/class_elf.svg';
import classSwordcraftIconUrl from './icon/svg/class_royal.svg';
import classRunecraftIconUrl from './icon/svg/class_witch.svg';
import classDragoncraftIconUrl from './icon/svg/class_dragon.svg';
import classAbysscraftIconUrl from './icon/svg/class_nightmare.svg';
import classHavencraftIconUrl from './icon/svg/class_bishop.svg';
import classPortalcraftIconUrl from './icon/svg/class_nemesis.svg';
import {
    ensureDeckEnvironmentOptions,
    getDeckEnvironmentOptions,
    getDefaultDeckEnvironment,
    normalizeDeckEnvironment as normalizeDeckEnvironmentShared
} from './deckEnvironmentOptions.js';
import {
    getDeckClassDisplay as getDeckClassDisplayShared,
    getDeckEnvironmentLabel as getDeckEnvironmentLabelShared,
    getDeckRuleLabel as getDeckRuleLabelShared
} from './deckPresentation.js';
import {
    filterUiVisibleDecks,
    getDefaultVisibleDeckRule,
    getUiVisibleDeckRuleOptions,
    isDeckRuleUiVisible
} from './deckRuleAvailability.js';
import {
    attachPendingDeckId,
    createPendingDeckCreationRecord,
    markPendingDeckCreationAbandoned,
    shouldCleanupPendingDeckCreation
} from './deckBuilderPendingCreationPolicy.js';
import {
    CARD_CATALOG_INVALIDATED_EVENT,
    DECK_VALIDATION_RULES_UPDATED_EVENT
} from './dataChangeEvents.js';
import { buildDeckCardRootClassName } from './deckCardTypeStyling.js';
import {
    createDefaultDeckBuilderFilters,
    resetDeckBuilderFilters
} from './deckBuilderFilters.js';
import { applyDeckBuilderFilterUiState } from './deckBuilderFilterUiState.js';
import {
    applyDeckBuilderCardCatalogPatch,
    canApplyDeckBuilderCardCatalogPatch,
    shouldSkipDeckBuilderCatalogReload
} from './deckBuilderCardCatalogPatch.js';
import { preloadCardArt } from './cardArtCache.js';

const DEFAULT_DECK_RULE = 'standard';
const DEFAULT_MAX_COPIES_PER_CARD = 3;

const CLASS_OPTIONS = [
    { value: 'neutral', label: '中立' },
    { value: 'forestcraft', label: '精灵' },
    { value: 'swordcraft', label: '皇家护卫' },
    { value: 'runecraft', label: '巫师' },
    { value: 'dragoncraft', label: '龙族' },
    { value: 'abysscraft', label: '梦魇' },
    { value: 'havencraft', label: '主教' },
    { value: 'portalcraft', label: '复仇者' }
];
const DECK_CLASS_OPTIONS = CLASS_OPTIONS.filter(option => option.value !== 'neutral');
const DECK_RULE_OPTIONS = [
    { value: 'standard', label: '标准' },
    { value: 'dualClass', label: '混合职业' }
];
const CLASS_ICON_URL_MAP = {
    neutral: classNeutralIconUrl,
    forestcraft: classForestcraftIconUrl,
    swordcraft: classSwordcraftIconUrl,
    runecraft: classRunecraftIconUrl,
    dragoncraft: classDragoncraftIconUrl,
    abysscraft: classAbysscraftIconUrl,
    havencraft: classHavencraftIconUrl,
    portalcraft: classPortalcraftIconUrl
};
const DECK_ENV_DESC = {
  open: "可使用全部可构筑卡牌",
  rotation: "按星云指定系列可用卡牌构筑",
  unlimited: "按星云历史系列卡牌构筑"
};

const TYPE_OPTIONS = [
    { value: 'follower', label: '从者' },
    { value: 'spell', label: '法术' },
    { value: 'amulet', label: '护符' },
    { value: 'crest', label: '纹章' },
    { value: 'faith', label: '信仰' },
    { value: 'leader', label: '主战者' }
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
    currentDeckOriginalSnapshot: null,
    rawCardsCache: null, // Raw card definitions from /api/cards
    rawCardIndex: new Map(),
    availableCards: [], // All cards available for building
    cardIndex: new Map(),
    environmentOptions: [],
    deckCreation: {
        mode: 'create',
        environment: null,
        class: null,
        deckRule: DEFAULT_DECK_RULE,
        secondaryClass: null,
        step: 'environment',
        importName: '',
        importCode: ''
    },
    filters: createDefaultDeckBuilderFilters(),
    pagination: {
        currentPage: 1,
        itemsPerPage: 10 // Fixed limit to prevent scrolling
    },
    newDeckId: null,
    pendingNewDeckCreation: null,
    saveErrorMessages: [],
    copyLimitPolicyCache: new Map(),
    copyLimitPolicyInFlight: new Map()
};
const deckBattleReadyCache = new Map();
const deckBattleReadyInFlight = new Map();
let routeAccess = null;
let nextPendingDeckCreationRequestId = 1;

function notifyDeckListUpdated(reason) {
    document.dispatchEvent(new CustomEvent('deckListUpdated', {
        detail: {
            reason: String(reason || 'unknown'),
            at: Date.now()
        }
    }));
}

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
let deckValidationRulesListenerBound = false;
let cardCatalogInvalidationListenerBound = false;

// --- Initialization ---

export async function initializeDeckBuilder(routeCapabilities) {
    routeAccess = routeCapabilities;
    container = document.getElementById('deck-builder-container');
    if (!container) {
        throw new Error("DeckBuilder: Container not found!");
    }

    if (!deckValidationRulesListenerBound) {
        document.addEventListener(DECK_VALIDATION_RULES_UPDATED_EVENT, () => {
            void handleDeckValidationRulesUpdated();
        });
        deckValidationRulesListenerBound = true;
    }

    if (!cardCatalogInvalidationListenerBound) {
        document.addEventListener(CARD_CATALOG_INVALIDATED_EVENT, (event) => {
            void handleCardCatalogInvalidated(event?.detail || {});
        });
        cardCatalogInvalidationListenerBound = true;
    }

    try {
        await loadEnvironmentOptions();
        await bootstrapDecks();
        const outcomes = await Promise.allSettled([loadDecks(), loadCards()]);
        const failures = outcomes.filter(outcome => outcome.status === "rejected").map(outcome => outcome.reason);
        if (failures.length) throw new AggregateError(failures, "牌组或卡牌加载失败。");
    } catch (error) {
        console.error("DeckBuilder: Failed to load data:", error);
        throw error;
    }

    renderDeckSelection();
}

async function loadEnvironmentOptions() {
    await ensureDeckEnvironmentOptions();
    const options = getDeckEnvironmentOptions();
    state.environmentOptions = options.map((item) => ({
        value: item.value,
        label: item.label,
        description: DECK_ENV_DESC[item.value] ?? `按${item.label}卡池构筑`
    }));
}

async function handleDeckValidationRulesUpdated() {
    state.copyLimitPolicyCache.clear();
    state.copyLimitPolicyInFlight.clear();
    deckBattleReadyCache.clear();
    deckBattleReadyInFlight.clear();

    try {
        await loadDecks();
    } catch (error) {
        console.warn("DeckBuilder: Failed to refresh decks after validation-rule update.", error);
    }

    if (state.currentDeck) {
        await ensureCopyLimitPolicyLoaded(state.currentDeck.environment, state.currentDeck.rule).catch(() => {});
        updateDeckStatusUI();
        return;
    }

    renderDeckSelection();
}

async function bootstrapDecks() {
    const query = getDeckOwnerQuery();
    await apiPostJson(`/api/decks/bootstrap?${query}`, {});
}

async function loadDecks() {
    const query = getDeckOwnerQuery();
    const decks = await apiGetJson(`/api/decks?${query}`);
    if (!Array.isArray(decks)) throw new TypeError("Deck list response must be an array.");
    const normalizedDecks = decks.map(deck => {
            const deckClass = deck.class ?? deck.Class;
            const deckRule = deck.rule ?? deck.Rule;
            const deckSecondaryClass = deck.secondaryClass ?? deck.SecondaryClass;
            return {
                ...deck,
                class: normalizeEnumValue(deckClass),
                environment: normalizeEnvironment(deck.environment ?? deck.Environment),
                rule: normalizeDeckRule(deckRule),
                secondaryClass: resolveSecondaryClass(deckRule, deckClass, deckSecondaryClass)
            };
        });
    const visibleDecks = filterUiVisibleDecks(normalizedDecks);
    pruneDeckBattleReadyCache(visibleDecks);
    state.decks = await attachBattleReadyState(visibleDecks);
}

function pruneDeckBattleReadyCache(decks) {
    const activeKeys = new Set(
        (Array.isArray(decks) ? decks : [])
            .map(deck => getDeckValidationCacheKey(deck))
            .filter(Boolean)
    );
    for (const key of deckBattleReadyCache.keys()) {
        if (!activeKeys.has(key)) {
            deckBattleReadyCache.delete(key);
        }
    }
}

function getDeckValidationCacheKey(deck) {
    const deckId = getDeckId(deck);
    if (!deckId) return null;
    const updatedAt = String(deck?.updatedAt ?? deck?.UpdatedAt ?? "");
    return `${deckId}|${updatedAt}`;
}

async function validateDeckBattleReady(deck) {
    const deckId = getDeckId(deck);
    if (!deckId) return false;
    const cacheKey = getDeckValidationCacheKey(deck);
    if (cacheKey && deckBattleReadyCache.has(cacheKey)) {
        return deckBattleReadyCache.get(cacheKey) === true;
    }
    if (cacheKey && deckBattleReadyInFlight.has(cacheKey)) {
        return await deckBattleReadyInFlight.get(cacheKey);
    }

    const validatePromise = (async () => {
        const query = getDeckOwnerQuery();
        try {
            const response = await fetch(`/api/decks/${encodeURIComponent(deckId)}/code?${query}`, { method: "GET" });
            return response.ok;
        } catch {
            return false;
        }
    })();

    if (cacheKey) {
        deckBattleReadyInFlight.set(cacheKey, validatePromise);
    }

    try {
        const valid = await validatePromise;
        if (cacheKey) {
            deckBattleReadyCache.set(cacheKey, valid);
        }
        return valid;
    } finally {
        if (cacheKey) {
            deckBattleReadyInFlight.delete(cacheKey);
        }
    }
}

async function attachBattleReadyState(decks) {
    if (!Array.isArray(decks) || decks.length === 0) {
        return [];
    }

    const results = await Promise.all(decks.map(async (deck) => ({
        deck,
        isBattleReady: await validateDeckBattleReady(deck)
    })));

    return results.map(item => ({
        ...item.deck,
        isBattleReady: item.isBattleReady
    }));
}

async function loadCards(options = {}) {
    if (options.force === true) {
        state.rawCardsCache = null;
    }
    const cards = await loadCardDefinitions({
        useCache: options.force !== true,
        force: options.force === true,
        getCached: () => state.rawCardsCache,
        setCached: cardsFromApi => {
            state.rawCardsCache = cardsFromApi;
        }
    });
    const filtered = cards.filter(card => {
            const collectible = card?.isCollectible ?? card?.IsCollectible;
            if (collectible !== true) return false;
            const rarity = normalizeEnumValue(card?.rarity ?? card?.Rarity ?? '');
            if (rarity === 'special') return false;
            return Number(card?.id ?? card?.Id) > 0 && String(card?.type ?? card?.Type ?? '').toLowerCase() !== 'leader';
        });

    state.rawCardIndex = new Map(cards.map(card => [Number(card?.id ?? card?.Id), card]));
    state.availableCards = filtered;
    state.cardIndex = new Map(filtered.map(card => [Number(card?.id ?? card?.Id), card]));
}

function getKnownCardDefinition(cardDefinitionId) {
    const id = Number(cardDefinitionId);
    if (!Number.isFinite(id) || id <= 0) {
        return null;
    }
    return state.cardIndex.get(id) ?? state.rawCardIndex.get(id) ?? null;
}

async function handleCardCatalogInvalidated(detail = {}) {
    if (shouldSkipDeckBuilderCatalogReload(detail)) {
        return;
    }

    if (canApplyDeckBuilderCardCatalogPatch(state, detail)) {
        applyDeckBuilderCardCatalogPatch(state, detail);
        if (state.currentDeck) {
            renderDeckList();
            renderLibrary();
            renderManaCurve();
            updateDeckStatusUI();
            return;
        }
        renderDeckSelection();
        return;
    }

    try {
        await loadCards({ force: true });
    } catch (error) {
        console.warn('DeckBuilder: Failed to reload cards after card catalog invalidation.', error);
        return;
    }

    if (state.currentDeck) {
        renderDeckBuilder();
        return;
    }

    renderDeckSelection();
}

function getDeckCardCount(deck) {
    if (!deck?.cards) {
        return deck?.cardCount ?? 0;
    }
    return deck.cards.reduce((sum, entry) => sum + (entry.count || 0), 0);
}

function getDeckTypeCounts(deck) {
    const counts = { follower: 0, spell: 0, amulet: 0 };
    const entries = Array.isArray(deck?.cards) ? deck.cards : [];

    entries.forEach(entry => {
        const count = Number(entry?.count ?? 0);
        if (!Number.isFinite(count) || count <= 0) {
            return;
        }
        const definition = getKnownCardDefinition(entry?.cardDefinitionId);
        if (!definition) {
            return;
        }
        const type = normalizeEnumValue(definition?.type ?? definition?.Type ?? definition?.cardType ?? definition?.card_type);
        if (type === 'follower' || type === 'spell' || type === 'amulet') {
            counts[type] += count;
        }
    });

    return counts;
}

function toSafeInt(value, fallback = 0) {
    const parsed = Number.parseInt(String(value ?? ''), 10);
    return Number.isFinite(parsed) ? parsed : fallback;
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

function getDeckSummaryById(deckId) {
    return state.decks.find(deck => String(getDeckId(deck)) === String(deckId)) ?? null;
}

function getDeckId(deck) {
    return deck?.deckId || deck?.id || deck?.DeckId || null;
}

function resolveDeckName(deck, fallback = 'New Deck') {
    const value = deck?.name;
    if (value === null || value === undefined) {
        return fallback;
    }
    const trimmed = String(value).trim();
    return trimmed || fallback;
}

function getVisibleDeckRuleOptions() {
    return getUiVisibleDeckRuleOptions(DECK_RULE_OPTIONS);
}

function hasMultipleVisibleDeckRules() {
    return getVisibleDeckRuleOptions().length > 1;
}

function decodeDeckRuleFromCode(code) {
    const trimmed = String(code ?? '').trim();
    if (!trimmed.startsWith('SVB2')) {
        return null;
    }

    try {
        const payloadToken = trimmed.slice(4).replace(/-/g, '+').replace(/_/g, '/');
        const padded = payloadToken + '='.repeat((4 - (payloadToken.length % 4)) % 4);
        const json = atob(padded);
        const payload = JSON.parse(json);
        return normalizeDeckRule(payload?.rule ?? payload?.Rule ?? '');
    } catch {
        return null;
    }
}

function buildDeckEditorSnapshot(deck) {
    if (!deck) return '';

    const cards = Array.isArray(deck.cards)
        ? deck.cards
            .map(entry => ({
                cardDefinitionId: Number(entry?.cardDefinitionId ?? 0),
                count: Number(entry?.count ?? 0)
            }))
            .filter(entry => entry.cardDefinitionId > 0 && entry.count > 0)
            .sort((left, right) => left.cardDefinitionId - right.cardDefinitionId)
        : [];

    return JSON.stringify({
        name: String(deck?.name ?? '').trim(),
        class: normalizeEnumValue(deck?.class),
        environment: normalizeEnvironment(deck?.environment),
        rule: normalizeDeckRule(deck?.rule),
        secondaryClass: resolveSecondaryClass(deck?.rule, deck?.class, deck?.secondaryClass),
        cards
    });
}

function setCurrentDeckSnapshot(deck) {
    state.currentDeckOriginalSnapshot = buildDeckEditorSnapshot(deck);
}

function hasCurrentDeckChanges() {
    if (!state.currentDeck) {
        return false;
    }
    return buildDeckEditorSnapshot(state.currentDeck) !== (state.currentDeckOriginalSnapshot ?? '');
}

function clearCurrentDeckState() {
    state.currentDeck = null;
    state.currentDeckOriginalSnapshot = null;
    resetDeckBuilderFilters(state.filters);
    state.pagination.currentPage = 1;
    clearDeckSaveErrors();
}

function getCurrentRouteName() {
    return routeAccess.getCurrentRoute();
}

function beginPendingNewDeckCreation() {
    const pending = createPendingDeckCreationRecord(nextPendingDeckCreationRequestId++);
    pending.deletePromise = null;
    state.pendingNewDeckCreation = pending;
    return pending;
}

function findPendingNewDeckCreation(requestId) {
    const pending = state.pendingNewDeckCreation;
    return pending?.requestId === requestId ? pending : null;
}

function clearPendingNewDeckCreation(requestId) {
    const pending = findPendingNewDeckCreation(requestId);
    if (!pending) {
        return;
    }

    if (state.newDeckId && pending.deckId && String(state.newDeckId) === String(pending.deckId)) {
        state.newDeckId = null;
    }

    state.pendingNewDeckCreation = null;
}

function setPendingNewDeckCreationDeckId(requestId, deckId) {
    const pending = findPendingNewDeckCreation(requestId);
    if (!pending) {
        return null;
    }

    const updated = attachPendingDeckId(pending, deckId);
    updated.deletePromise = pending.deletePromise ?? null;
    state.pendingNewDeckCreation = updated;
    return updated;
}

function abandonPendingNewDeckCreation() {
    const pending = state.pendingNewDeckCreation;
    if (!pending) {
        return null;
    }

    const updated = markPendingDeckCreationAbandoned(pending);
    updated.deletePromise = pending.deletePromise ?? null;
    state.pendingNewDeckCreation = updated;
    return updated;
}

async function cleanupPendingNewDeckCreation(pending, reason = 'discard') {
    if (!pending?.deckId) {
        if (pending?.requestId) {
            clearPendingNewDeckCreation(pending.requestId);
        }
        return false;
    }

    if (pending.deletePromise) {
        return await pending.deletePromise;
    }

    const query = getDeckOwnerQuery();
    pending.deletePromise = (async () => {
        await apiDelete(`/api/decks/${pending.deckId}?${query}`);
        await loadDecks();
        notifyDeckListUpdated(reason);
        clearPendingNewDeckCreation(pending.requestId);
        return true;
    })().catch((error) => {
        pending.deletePromise = null;
        throw error;
    });

    return await pending.deletePromise;
}

async function cleanupAbandonedPendingNewDeckCreation(pending, reason = 'discard') {
    if (!pending) {
        return false;
    }

    try {
        return await cleanupPendingNewDeckCreation(pending, reason);
    } catch (error) {
        console.warn('DeckBuilder: Failed to cleanup abandoned pending deck creation.', error);
        clearPendingNewDeckCreation(pending.requestId);
        return false;
    }
}

function promptDeckEditorExitAction() {
    const shouldSave = window.confirm('当前卡组已修改。点击“确定”保存并返回，点击“取消”选择是否放弃修改。');
    if (shouldSave) {
        return 'save';
    }

    const shouldDiscard = window.confirm('是否放弃当前修改并返回？点击“确定”放弃，点击“取消”继续编辑。');
    if (shouldDiscard) {
        return 'discard';
    }

    return 'cancel';
}

async function discardCurrentDeckChanges({ renderSelectionOnComplete = true } = {}) {
    if (!state.currentDeck) {
        if (renderSelectionOnComplete) {
            renderDeckSelection();
        }
        return true;
    }

    const deckId = state.currentDeck.deckId || state.currentDeck.id;
    const isNewDeck = state.currentDeck.isNew === true;
    clearCurrentDeckState();

    if (deckId && isNewDeck) {
        const query = getDeckOwnerQuery();
        await apiDelete(`/api/decks/${deckId}?${query}`);
        await loadDecks();
        notifyDeckListUpdated('discard');
    }

    if (renderSelectionOnComplete) {
        renderDeckSelection();
    }

    return true;
}

async function handleDeckEditorExit({ renderSelectionOnComplete = true } = {}) {
    if (!state.currentDeck) {
        const pendingCreation = abandonPendingNewDeckCreation();
        if (pendingCreation?.deckId) {
            await cleanupAbandonedPendingNewDeckCreation(pendingCreation, 'discard');
        }
        if (renderSelectionOnComplete) {
            renderDeckSelection();
        }
        return true;
    }

    if (!hasCurrentDeckChanges()) {
        return await discardCurrentDeckChanges({ renderSelectionOnComplete });
    }

    const action = promptDeckEditorExitAction();
    if (action === 'save') {
        return await saveDeck({ closeEditorOnSuccess: renderSelectionOnComplete });
    }

    if (action === 'discard') {
        return await discardCurrentDeckChanges({ renderSelectionOnComplete });
    }

    return false;
}

export async function beforeLeaveDeckBuilder() {
    return await handleDeckEditorExit({ renderSelectionOnComplete: false });
}

function getDeckPreviewItems(deck) {
    const entries = Array.isArray(deck?.cards) ? deck.cards : [];
    return entries
        .map(entry => {
            const cardDefinitionId = Number(entry?.cardDefinitionId);
            const count = Number(entry?.count ?? 0);
            const card = getKnownCardDefinition(cardDefinitionId);
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
        throw new Error('当前环境不支持自动复制');
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
    const normalizedFallback = fallback === ''
        ? ''
        : (normalizeEnumValue(fallback) || getDefaultDeckEnvironment());
    return normalizeDeckEnvironmentShared(value, normalizedFallback);
}

function normalizeEnvironment(value) {
    return normalizeEnvironmentValue(value, getDefaultDeckEnvironment());
}

function normalizeDeckRuleValue(value, fallback = '') {
    if (value === null || value === undefined || value === '') return fallback;
    if (typeof value === 'number') {
        return value === 1 ? 'dualClass' : DEFAULT_DECK_RULE;
    }
    return String(value).trim();
}

function normalizeDeckRule(value) {
    const normalized = normalizeDeckRuleValue(value, DEFAULT_DECK_RULE).toLowerCase();
    return normalized === 'dualclass' || normalized === 'dual_class'
        ? 'dualClass'
        : DEFAULT_DECK_RULE;
}

function getCopyLimitPolicyCacheKey(environment, rule) {
    return `${normalizeEnvironment(environment)}|${normalizeDeckRule(rule)}`;
}

function normalizeCopyLimitValue(value) {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) {
        return DEFAULT_MAX_COPIES_PER_CARD;
    }
    return Math.max(0, Math.min(DEFAULT_MAX_COPIES_PER_CARD, Math.trunc(parsed)));
}

function buildCopyLimitPolicySnapshot(payload) {
    const defaultMaxRaw = payload?.defaultMaxCopies ?? payload?.DefaultMaxCopies;
    const defaultMaxCopies = normalizeCopyLimitValue(defaultMaxRaw);
    const limits = new Map();
    const source = Array.isArray(payload?.items)
        ? payload.items
        : (Array.isArray(payload?.Items) ? payload.Items : []);

    source.forEach(item => {
        const cardDefinitionId = Number(item?.cardDefinitionId ?? item?.CardDefinitionId);
        if (!Number.isFinite(cardDefinitionId) || cardDefinitionId <= 0) {
            return;
        }
        const maxCopies = normalizeCopyLimitValue(item?.maxCopies ?? item?.MaxCopies);
        limits.set(cardDefinitionId, maxCopies);
    });

    return { defaultMaxCopies, limits };
}

async function ensureCopyLimitPolicyLoaded(environment, rule) {
    const cacheKey = getCopyLimitPolicyCacheKey(environment, rule);
    if (state.copyLimitPolicyCache.has(cacheKey)) {
        return state.copyLimitPolicyCache.get(cacheKey);
    }
    if (state.copyLimitPolicyInFlight.has(cacheKey)) {
        return await state.copyLimitPolicyInFlight.get(cacheKey);
    }

    const fetchPromise = (async () => {
        try {
            const params = new URLSearchParams({
                environment: normalizeEnvironment(environment),
                rule: normalizeDeckRule(rule)
            });
            const payload = await apiGetJson(`/api/deck-copy-limit-policies?${params.toString()}`);
            const snapshot = buildCopyLimitPolicySnapshot(payload);
            state.copyLimitPolicyCache.set(cacheKey, snapshot);
            return snapshot;
        } catch (error) {
            console.warn('DeckBuilder: Failed to load copy-limit policy. Falling back to default.', error);
            const fallback = {
                defaultMaxCopies: DEFAULT_MAX_COPIES_PER_CARD,
                limits: new Map()
            };
            state.copyLimitPolicyCache.set(cacheKey, fallback);
            return fallback;
        } finally {
            state.copyLimitPolicyInFlight.delete(cacheKey);
        }
    })();

    state.copyLimitPolicyInFlight.set(cacheKey, fetchPromise);
    return await fetchPromise;
}

function getCopyLimitPolicySnapshot(deck) {
    const cacheKey = getCopyLimitPolicyCacheKey(deck?.environment, deck?.rule);
    return state.copyLimitPolicyCache.get(cacheKey) || {
        defaultMaxCopies: DEFAULT_MAX_COPIES_PER_CARD,
        limits: new Map()
    };
}

function resolveEffectiveCopyLimit(policySnapshot, cardDefinitionId) {
    const cardId = Number(cardDefinitionId);
    if (policySnapshot?.limits instanceof Map && policySnapshot.limits.has(cardId)) {
        return normalizeCopyLimitValue(policySnapshot.limits.get(cardId));
    }
    return normalizeCopyLimitValue(policySnapshot?.defaultMaxCopies);
}

function ensureCurrentDeckCopyLimitPolicyLoaded() {
    if (!state.currentDeck) {
        return;
    }

    const targetEnvironment = normalizeEnvironment(state.currentDeck.environment);
    const targetRule = normalizeDeckRule(state.currentDeck.rule);
    const cacheKey = getCopyLimitPolicyCacheKey(targetEnvironment, targetRule);
    const hadCache = state.copyLimitPolicyCache.has(cacheKey);
    if (hadCache || state.copyLimitPolicyInFlight.has(cacheKey)) {
        return;
    }

    ensureCopyLimitPolicyLoaded(targetEnvironment, targetRule)
        .then(() => {
            if (!state.currentDeck) return;
            const currentKey = getCopyLimitPolicyCacheKey(state.currentDeck.environment, state.currentDeck.rule);
            if (currentKey === cacheKey) {
                updateDeckStatusUI();
            }
        })
        .catch(() => {
            // Fallback policy is already applied in ensureCopyLimitPolicyLoaded.
        });
}

function getDeckRuleLabel(value) {
    return getDeckRuleLabelShared(value);
}

function resolveSecondaryClass(mode, primaryClass, secondaryClass) {
    if (normalizeDeckRule(mode) !== 'dualClass') {
        return null;
    }

    const normalizedPrimary = normalizeEnumValue(primaryClass);
    const normalizedSecondary = normalizeEnumValue(secondaryClass);
    if (!normalizedSecondary || normalizedSecondary === normalizedPrimary || normalizedSecondary === 'neutral') {
        return null;
    }

    return normalizedSecondary;
}

function getDeckClassDisplay(primaryClass, mode, secondaryClass) {
    return getDeckClassDisplayShared(primaryClass, mode, secondaryClass);
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
    if (value === CARD_SET_UNSPECIFIED_ENVIRONMENT) {
        return '未指定环境';
    }

    return getDeckEnvironmentLabelShared(value);
}

function getClassLabel(value) {
    const normalized = normalizeEnumValue(value);
    const match = CLASS_OPTIONS.find(option => option.value === normalized);
    return match?.label || formatClassName(value);
}

function getClassIconPath(value) {
    const normalized = normalizeEnumValue(value) || 'neutral';
    return CLASS_ICON_URL_MAP[normalized] || CLASS_ICON_URL_MAP.neutral;
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
        if (!isCardAllowedByClass(card, state.currentDeck.class, state.currentDeck.rule, state.currentDeck.secondaryClass)) return false;
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
    return buildGroupedCardSetOptions(cards, {
        getCardSetInfo,
        normalizeEnvironment,
        getEnvironmentLabel,
        knownEnvironmentOptions: getDeckEnvironmentOptions()
    }).map(group => ({
        ...group,
        options: group.options.map(option => ({
            value: option.value.toLowerCase(),
            label: option.label
        }))
    }));
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
    const setGroups = buildSetFilterOptions(source);

    const classValues = new Set(classOptions.map(option => option.value));
    const typeValues = new Set(typeOptions.map(option => option.value));
    const rarityValues = new Set(rarityOptions.map(option => option.value));
    const setValues = new Set(setGroups.flatMap(group => group.options.map(option => option.value)));

    if (state.filters.class && !classValues.has(state.filters.class)) state.filters.class = '';
    if (state.filters.type && !typeValues.has(state.filters.type)) state.filters.type = '';
    if (state.filters.rarity && !rarityValues.has(state.filters.rarity)) state.filters.rarity = '';
    if (state.filters.set && !setValues.has(state.filters.set)) state.filters.set = '';

    classFilter.innerHTML = '<option value="">ALL CLASSES</option>' + buildSelectOptions(classOptions, state.filters.class);
    typeFilter.innerHTML = '<option value="">ALL TYPES</option>' + buildSelectOptions(typeOptions, state.filters.type);
    rarityFilter.innerHTML = '<option value="">ALL RARITIES</option>' + buildSelectOptions(rarityOptions, state.filters.rarity);
    setFilter.disabled = setGroups.length === 0;
    setFilter.innerHTML = buildGroupedSetFilterOptionsHtml(setGroups, state.filters.set);
}

function buildGroupedSetFilterOptionsHtml(groups, selectedValue) {
    let hasSelected = false;
    const groupHtml = (Array.isArray(groups) ? groups : [])
        .map(group => {
            const optionsHtml = (Array.isArray(group.options) ? group.options : [])
                .map(option => {
                    const shouldSelect = !hasSelected && option.value === selectedValue;
                    if (shouldSelect) {
                        hasSelected = true;
                    }

                    return `<option value="${option.value}"${shouldSelect ? ' selected' : ''}>${option.label}</option>`;
                })
                .join('');

            if (!optionsHtml) {
                return '';
            }

            return `<optgroup label="${group.label}">${optionsHtml}</optgroup>`;
        })
        .join('');

    return '<option value="">ALL SETS</option>' + groupHtml;
}

function isCardAllowedByEnvironment(card, environment) {
    const normalized = normalizeEnvironment(environment);
    const setDefinitionId = Number(card?.card_set_definition_id ?? card?.cardSetDefinitionId ?? 0);
    const hasSetDefinition = Number.isFinite(setDefinitionId) && setDefinitionId > 0;
    if (!hasSetDefinition) {
        return true;
    }

    const setEnvironments = getCardSetInfo(card).environments;
    if (!setEnvironments || setEnvironments.length === 0) return false;
    return setEnvironments.includes(normalized);
}

function isCardAllowedByClass(card, deckClass, mode = DEFAULT_DECK_RULE, secondaryClass = null) {
    const cardClass = normalizeEnumValue(card?.class ?? card?.Class ?? card?.cardClass ?? card?.card_class);
    const targetClass = normalizeEnumValue(deckClass);
    if (!cardClass || !targetClass) return true;
    if (cardClass === 'neutral') return true;
    if (cardClass === targetClass) return true;

    if (normalizeDeckRule(mode) !== 'dualClass') {
        return false;
    }

    const normalizedSecondary = resolveSecondaryClass(mode, deckClass, secondaryClass);
    return Boolean(normalizedSecondary) && cardClass === normalizedSecondary;
}

// --- Rendering: Deck Selection View ---

function renderDeckSelection() {
    const environmentOptions = Array.isArray(state.environmentOptions) && state.environmentOptions.length > 0
        ? state.environmentOptions
        : getDeckEnvironmentOptions().map((item) => ({
            value: item.value,
            label: item.label,
            description: `按${item.label}卡池构筑`
        }));
    const visibleDeckRuleOptions = getVisibleDeckRuleOptions();
    const showDeckRuleStep = visibleDeckRuleOptions.length > 1;

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
                    const rule = normalizeDeckRule(deck.rule);
                    const className = getDeckClassDisplay(deck.class, rule, deck.secondaryClass);
                    const isComplete = deck?.isBattleReady === true;
                    return `
                    <div class="deck-card wide-banner" data-id="${deckId}">
                        <div class="deck-art-bg"></div> <!-- Placeholder for Character Art -->
                        <div class="deck-content">
                            <div class="deck-info">
                                <div class="deck-class-name">${className}</div>
                                <div class="deck-env-name">${getEnvironmentLabel(deck.environment)} | ${getDeckRuleLabel(rule)}</div>
                                <div class="deck-name">${deckName}</div>
                                <div class="deck-status">
                                    <span class="status-indicator ${isComplete ? 'valid' : 'invalid'}">
                                        ${isComplete ? '可用' : '禁用'}
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
                                ${environmentOptions.map(option => `
                                    <button class="deck-option" data-env="${option.value}" type="button">
                                        <div class="deck-option-title">${option.label}</div>
                                        <div class="deck-option-desc">${option.description}</div>
                                    </button>
                                `).join('')}
                            </div>
                        </div>
                        <div class="deck-create-step hidden" data-step="mode">
                            <div class="deck-create-step-title">选择规则</div>
                            <div class="deck-create-options" data-deck-mode-options>
                                ${visibleDeckRuleOptions.map(option => `
                                    <button class="deck-option" data-deck-mode="${option.value}" type="button">
                                        <div class="deck-option-title">${option.label}</div>
                                    </button>
                                `).join('')}
                            </div>
                        </div>
                        <div class="deck-create-step hidden" data-step="class">
                            <div class="deck-create-step-title">选择职业</div>
                            <div class="deck-create-options grid">
                                ${DECK_CLASS_OPTIONS.map(option => `
                                    <button class="deck-option" data-class="${option.value}" type="button">
                                        <div class="deck-option-title">${option.label}</div>
                                    </button>
                                `).join('')}
                            </div>
                            <div class="deck-create-step-title" id="deck-secondary-class-title" style="display:none;">选择副职业</div>
                            <div class="deck-create-options grid" id="deck-secondary-class-options" style="display:none;">
                                ${DECK_CLASS_OPTIONS.map(option => `
                                    <button class="deck-option" data-secondary-class="${option.value}" type="button">
                                        <div class="deck-option-title">${option.label}</div>
                                    </button>
                                `).join('')}
                            </div>
                        </div>

                        <div class="deck-create-actions" id="deck-create-create-actions">
                            <button class="btn-secondary" id="deck-create-back" type="button">上一步</button>
                            <button class="btn-primary" id="deck-create-next" type="button">下一步</button>
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
                            <textarea id="deck-import-code-input" class="deck-import-textarea" rows="5" placeholder="粘贴导出的卡组代码"></textarea>
                        </label>
                        <div class="deck-import-hint">使用导出的代码直接创建卡组，创建后将进入卡组编辑。</div>

                        <div class="deck-create-actions" id="deck-create-import-actions">
                            <button class="btn-primary" id="deck-import-submit-btn" type="button">导入并创建</button>
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
                        <div class="deck-preview-toolbar-stats" id="deck-preview-toolbar-stats"></div>
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
    const modeStep = createModal?.querySelector('[data-step="mode"]');
    const classStep = createModal?.querySelector('[data-step="class"]');
    const modeOptions = modeStep?.querySelectorAll('[data-deck-mode]');
    const createSecondaryClassTitle = document.getElementById('deck-secondary-class-title');
    const createSecondaryClassOptionsContainer = document.getElementById('deck-secondary-class-options');
    const createSecondaryClassOptions = classStep?.querySelectorAll('[data-secondary-class]');
    const deckSelectionView = createModal?.closest('.deck-selection-view') ?? null;
    const codeModal = document.getElementById('deck-code-modal');
    const codeCloseBtn = document.getElementById('deck-code-close-btn');
    const codeCopyBtn = document.getElementById('deck-code-copy-btn');
    const codeOutput = document.getElementById('deck-code-output');
    const codeTitle = document.getElementById('deck-code-title');
    const codeStatus = document.getElementById('deck-code-status');

    let previewDeckId = null;
    let previewDeckName = '';
    let activeDeckCode = '';
    let deckPreview = null;

    const resetDeckCreation = () => {
        state.deckCreation = {
            mode: 'create',
            environment: null,
            class: null,
            deckRule: getDefaultVisibleDeckRule(),
            secondaryClass: null,
            step: 'environment',
            importName: '',
            importCode: ''
        };
    };

    const setDeckSelectionScrollLock = (locked) => {
        if (!deckSelectionView) return;
        deckSelectionView.classList.toggle('deck-selection-modal-open', Boolean(locked));
    };

    const syncDeckSelectionModalLock = () => {
        const shouldLock = Boolean(
            createModal?.classList.contains('open')
            || codeModal?.classList.contains('open')
            || deckPreview?.isOpen?.()
        );
        setDeckSelectionScrollLock(shouldLock);
    };

    const syncDeckCreationUI = () => {
        if (!createModal || !envStep || !modeStep || !classStep || !createBack || !createNext || !createConfirm) return;
        state.deckCreation.deckRule = normalizeDeckRule(state.deckCreation.deckRule);
        if (!isDeckRuleUiVisible(state.deckCreation.deckRule)) {
            state.deckCreation.deckRule = getDefaultVisibleDeckRule();
        }
        if (!showDeckRuleStep && state.deckCreation.step === 'mode') {
            state.deckCreation.step = 'class';
        }
        if (state.deckCreation.deckRule !== 'dualClass') {
            state.deckCreation.secondaryClass = null;
        } else {
            state.deckCreation.secondaryClass = resolveSecondaryClass(
                state.deckCreation.deckRule,
                state.deckCreation.class,
                state.deckCreation.secondaryClass);
        }

        const isImportMode = state.deckCreation.mode === 'import';
        const isEnvStep = state.deckCreation.step === 'environment';
        const isModeStep = state.deckCreation.step === 'mode';
        const isClassStep = state.deckCreation.step === 'class';
        createPanel?.classList.toggle('hidden', isImportMode);
        importPanel?.classList.toggle('hidden', !isImportMode);
        envStep.classList.toggle('hidden', isImportMode || !isEnvStep);
        modeStep.classList.toggle('hidden', isImportMode || !showDeckRuleStep || !isModeStep);
        classStep.classList.toggle('hidden', isImportMode || !isClassStep);
        createBack.disabled = isImportMode || isEnvStep;
        createNext.style.display = isImportMode ? 'none' : (isClassStep ? 'none' : 'inline-flex');
        createConfirm.style.display = isImportMode ? 'none' : (isClassStep ? 'inline-flex' : 'none');
        const canNext = isEnvStep
            ? Boolean(state.deckCreation.environment)
            : isModeStep
                ? Boolean(state.deckCreation.deckRule)
                : false;
        createNext.disabled = isImportMode || !canNext;
        const dualClassMissingSecondary = state.deckCreation.deckRule === 'dualClass' && !state.deckCreation.secondaryClass;
        createConfirm.disabled = isImportMode || !state.deckCreation.class || dualClassMissingSecondary;
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
        classStep.querySelectorAll('[data-class]').forEach(option => {
            const value = option.getAttribute('data-class');
            option.classList.toggle('selected', value === state.deckCreation.class);
        });

        modeOptions?.forEach(option => {
            const value = option.getAttribute('data-deck-mode');
            option.classList.toggle('selected', value === state.deckCreation.deckRule);
        });

        const isDualClassMode = state.deckCreation.deckRule === 'dualClass';
        if (createSecondaryClassTitle) {
            createSecondaryClassTitle.style.display = isDualClassMode ? '' : 'none';
        }
        if (createSecondaryClassOptionsContainer) {
            createSecondaryClassOptionsContainer.style.display = isDualClassMode ? 'grid' : 'none';
        }
        createSecondaryClassOptions?.forEach(option => {
            const value = option.getAttribute('data-secondary-class');
            const isPrimaryClass = value === state.deckCreation.class;
            option.style.display = isPrimaryClass ? 'none' : '';
            option.disabled = !isDualClassMode || isPrimaryClass;
            option.classList.toggle('selected', isDualClassMode && value === state.deckCreation.secondaryClass);
        });
    };

    const openDeckCreation = () => {
        if (!createModal) return;
        resetDeckCreation();
        createModal.classList.add('open');
        createModal.setAttribute('aria-hidden', 'false');
        syncDeckSelectionModalLock();
        syncDeckCreationUI();
    };

    const closeDeckCode = () => {
        if (!codeModal) return;
        codeModal.classList.remove('open');
        codeModal.setAttribute('aria-hidden', 'true');
        activeDeckCode = '';
        if (codeStatus) codeStatus.textContent = '';
        syncDeckSelectionModalLock();
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
        setCodeStatus('代码已生成，可直接复制。');
        codeModal.classList.add('open');
        codeModal.setAttribute('aria-hidden', 'false');
        syncDeckSelectionModalLock();
    };

    const openDeckCodeById = async (deckId, fallbackName = 'New Deck') => {
        const deckName = deckNameLookup.get(String(deckId)) || fallbackName;
        const code = await fetchDeckCode(deckId);
        openDeckCode(deckName, code);
    };

    deckPreview = createDeckPreviewModal({
        idPrefix: 'deck-preview',
        onClose: () => {
            previewDeckId = null;
            syncDeckSelectionModalLock();
        },
        onExport: () => {
            if (!previewDeckId) return;
            openDeckCodeById(previewDeckId, previewDeckName).catch(err => {
                alert(`导出失败：${err.message}`);
            });
        },
        onCardClick: (cardId) => {
            const card = state.cardIndex.get(Number(cardId));
            openCardDetailModal(card);
        }
    });

    const openDeckPreview = async (deckId) => {
        if (!deckPreview.isAvailable()) return;

        const deckDetail = await fetchDeckDetail(deckId);
        const previewSnapshot = buildDeckPreviewSnapshot(deckDetail);
        const deckSummary = getDeckSummaryById(deckId) ?? deckDetail;
        previewDeckId = deckId;
        previewDeckName = resolveDeckName(deckDetail);

        const opened = deckPreview.open({
            titleText: previewDeckName,
            metaText: `${getDeckClassDisplay(deckDetail.class, deckDetail.rule, deckDetail.secondaryClass)} | ${getDeckRuleLabel(deckDetail.rule)} | ${getEnvironmentLabel(deckDetail.environment)}`,
            cards: previewSnapshot.items,
            stats: getDeckMatchStats(deckSummary),
            exportEnabled: true,
            exportVisible: true
        });
        if (opened) {
            syncDeckSelectionModalLock();
        }
    };

    const closeDeckCreation = () => {
        if (!createModal) return;
        createModal.classList.remove('open');
        createModal.setAttribute('aria-hidden', 'true');
        syncDeckSelectionModalLock();
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
    codeModal?.addEventListener('click', (event) => {
        if (event.target === codeModal) {
            closeDeckCode();
        }
    });
    codeCloseBtn?.addEventListener('click', closeDeckCode);
    codeCopyBtn?.addEventListener('click', () => {
        if (!activeDeckCode) return;
        copyTextToClipboard(activeDeckCode)
            .then(() => setCodeStatus('复制成功。'))
            .catch(err => setCodeStatus(`复制失败：${err.message}`, true));
    });

    envStep?.querySelectorAll('.deck-option').forEach(option => {
        option.addEventListener('click', () => {
            const value = option.getAttribute('data-env');
            state.deckCreation.environment = value;
            syncDeckCreationUI();
        });
    });

    classStep?.querySelectorAll('[data-class]').forEach(option => {
        option.addEventListener('click', () => {
            const value = option.getAttribute('data-class');
            state.deckCreation.class = value;
            syncDeckCreationUI();
        });
    });

    modeOptions?.forEach(option => {
        option.addEventListener('click', () => {
            const value = option.getAttribute('data-deck-mode');
            if (!value) return;
            state.deckCreation.deckRule = value;
            syncDeckCreationUI();
        });
    });

    createSecondaryClassOptions?.forEach(option => {
        option.addEventListener('click', () => {
            if (state.deckCreation.deckRule !== 'dualClass') return;
            const value = option.getAttribute('data-secondary-class');
            if (!value || value === state.deckCreation.class) return;
            state.deckCreation.secondaryClass = value;
            syncDeckCreationUI();
        });
    });

    createNext?.addEventListener('click', () => {
        if (state.deckCreation.step === 'environment') {
            if (!state.deckCreation.environment) return;
            state.deckCreation.step = showDeckRuleStep ? 'mode' : 'class';
            syncDeckCreationUI();
            return;
        }
        if (state.deckCreation.step === 'mode') {
            state.deckCreation.step = 'class';
            syncDeckCreationUI();
            return;
        }
        syncDeckCreationUI();
    });

    createBack?.addEventListener('click', () => {
        if (state.deckCreation.step === 'class') {
            state.deckCreation.step = showDeckRuleStep ? 'mode' : 'environment';
        } else {
            state.deckCreation.step = 'environment';
        }
        syncDeckCreationUI();
    });

    createConfirm?.addEventListener('click', () => {
        if (!state.deckCreation.environment || !state.deckCreation.class) return;
        const env = state.deckCreation.environment;
        const deckClass = state.deckCreation.class;
        const deckRule = normalizeDeckRule(state.deckCreation.deckRule);
        const secondaryClass = resolveSecondaryClass(deckRule, deckClass, state.deckCreation.secondaryClass);
        if (deckRule === 'dualClass' && !secondaryClass) return;
        closeDeckCreation();
        createNewDeck(env, deckClass, deckRule, secondaryClass).catch(err => alert(`创建失败：${err.message}`));
    });
    importSubmit?.addEventListener('click', async () => {
        const code = state.deckCreation.importCode?.trim();
        if (!code) return;
        const importedRule = decodeDeckRuleFromCode(code);
        if (importedRule && !isDeckRuleUiVisible(importedRule)) {
            alert('混合职业规则当前未开放，无法导入该卡组。');
            return;
        }
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
            alert(`导入失败：${err.message}`);
            importSubmit.disabled = false;
            syncDeckCreationUI();
        }
    });

    document.querySelectorAll('.preview-deck-btn').forEach(btn => {
        btn.addEventListener('click', (event) => {
            event.stopPropagation();
            openDeckPreview(btn.dataset.id).catch(err => alert(`预览失败：${err.message}`));
        });
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
    state.currentDeck.rule = normalizeDeckRule(state.currentDeck.rule);
    state.currentDeck.secondaryClass = resolveSecondaryClass(
        state.currentDeck.rule,
        state.currentDeck.class,
        state.currentDeck.secondaryClass);
    const deckValidation = getDeckSaveValidation(state.currentDeck);
    const isDualClassMode = deckValidation.stats.rule === 'dualClass';
    const totalCount = getDeckCardCount(state.currentDeck);
    const typeCounts = getDeckTypeCounts(state.currentDeck);
    const primaryClassIcon = getClassIconPath(state.currentDeck.class);
    const secondaryClassIcon = getClassIconPath(state.currentDeck.secondaryClass || 'neutral');
    const neutralIcon = getClassIconPath('neutral');

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
                        <div class="deck-env-tag">${getDeckRuleLabel(state.currentDeck.rule)}</div>
                    </div>
                    
                    <div class="mana-curve-mini" id="mana-curve">
                        <!-- Mana curve bars -->
                    </div>

                    <div class="deck-meta-controls">
                        <div class="deck-counts">
                            <div class="deck-quota-summary">
                                <span class="total-count deck-quota-item">
                                    <img class="deck-quota-icon" src="${neutralIcon}" alt="总数">
                                    <span id="deck-count-display">${totalCount}</span> / 40
                                </span>
                                ${isDualClassMode ? `
                                    <span class="total-count deck-quota-item deck-quota-dual">
                                        <img class="deck-quota-icon" src="${primaryClassIcon}" alt="主职业">
                                        <span id="deck-primary-count-display">${deckValidation.stats.primary}</span> / 24↑
                                    </span>
                                    <span class="total-count deck-quota-item deck-quota-dual">
                                        <img class="deck-quota-icon" src="${secondaryClassIcon}" alt="副职业">
                                        <span id="deck-secondary-count-display">${deckValidation.stats.secondary}</span> / 9↑
                                    </span>
                                ` : ''}
                            </div>
                            <div class="type-counts">
                                <span class="type-count follower" id="deck-follower-count">从者 ${typeCounts.follower}</span>
                                <span class="type-count spell" id="deck-spell-count">法术 ${typeCounts.spell}</span>
                                <span class="type-count amulet" id="deck-amulet-count">护符 ${typeCounts.amulet}</span>
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
                    <div class="deck-save-warning ${deckValidation.message ? '' : 'hidden'}" id="deck-save-warning">${deckValidation.message}</div>
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

    applyDeckBuilderFilterUiState(container, state.filters);

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
        void handleDeckEditorExit({ renderSelectionOnComplete: true }).catch(err => {
            alert(`返回失败：${err.message}`);
        });
    });

    document.getElementById('save-deck-btn').addEventListener('click', () => {
        void saveDeck();
    });

    document.getElementById('deck-name-input').addEventListener('change', (e) => {
        state.currentDeck.name = e.target.value;
        clearDeckSaveErrors();
        updateDeckStatusUI();
    });

    updateDeckStatusUI();
}

function getFilteredCards() {
    const filtered = state.availableCards.filter(card => {
        if (state.currentDeck) {
            if (!isCardAllowedByEnvironment(card, state.currentDeck.environment)) return false;
            if (!isCardAllowedByClass(card, state.currentDeck.class, state.currentDeck.rule, state.currentDeck.secondaryClass)) return false;
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
        if (!matchesCardLibrarySearch(card, state.filters.search)) return false;
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
    paginatedCards.forEach(card => {
        const preview = card.artPreview ?? card.ArtPreview ?? '';
        if (preview) {
            void preloadCardArt(preview, { priority: 'low' }).catch(() => {});
        }
    });

    grid.innerHTML = paginatedCards.map(card => {
        const attack = card.attack ?? '-';
        const health = card.health ?? '-';
        const preview = card.artPreview ?? card.ArtPreview ?? '';
        const previewUrl = preview ? encodeURI(preview) : '';
        const rarityClass = getCardRarityClass(card);
        const cardClassName = buildDeckCardRootClassName('library-card', {
            hasArt: Boolean(previewUrl),
            rarityClass,
            type: card?.type ?? card?.Type ?? card?.cardType ?? card?.card_type
        });
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
            const card = getKnownCardDefinition(cardId);
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
            card: getKnownCardDefinition(entry.cardDefinitionId)
        }))
        .filter(item => item.card)
        .sort((a, b) => compareCardsForEditorAndDeck(a.card, b.card));
    deckCards.forEach(item => {
        const preview = item.card.artPreview ?? item.card.ArtPreview ?? '';
        if (preview) {
            void preloadCardArt(preview, { priority: 'low' }).catch(() => {});
        }
    });

    // Render as vertical cards (miniature) for horizontal scroll
    list.innerHTML = deckCards.map(item => {
        const preview = item.card.artPreview ?? item.card.ArtPreview ?? '';
        const previewUrl = preview ? encodeURI(preview) : '';
        const visualClass = `mini-visual${previewUrl ? ' has-art' : ''}`;
        const visualStyle = previewUrl ? ` style="background-image: url('${previewUrl}');"` : '';
        const rarityClass = getCardRarityClass(item.card);
        const cardClassName = buildDeckCardRootClassName('deck-card-mini', {
            hasArt: false,
            rarityClass,
            type: item.card?.type ?? item.card?.Type ?? item.card?.cardType ?? item.card?.card_type
        });
        return `
        <div class="${cardClassName}" data-card-id="${item.entry.cardDefinitionId}">
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
            const card = getKnownCardDefinition(cardId);
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
        const card = getKnownCardDefinition(entry.cardDefinitionId);
        if (!card) return;
        const cost = Number(card.cost ?? 0);
        const index = Math.min(cost, 8);
        curve[index] += Number(entry.count ?? 0);
    });

    const max = Math.max(...curve, 1); // Avoid divide by zero

    container.innerHTML = curve.map((count, i) => `
        <div class="curve-bar-container" title="Cost ${i}: ${count}">
            <div class="curve-bar-area">
                <div class="curve-bar" style="height: ${(count / max) * 100}%"></div>
            </div>
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

function getDeckQuotaStats(deck) {
    const rule = normalizeDeckRule(deck?.rule ?? deck?.Rule);
    const primaryClass = normalizeEnumValue(deck?.class);
    const secondaryClass = resolveSecondaryClass(rule, primaryClass, deck?.secondaryClass);
    const copyLimitPolicy = getCopyLimitPolicySnapshot(deck);
    const entries = Array.isArray(deck?.cards) ? deck.cards : [];

    let total = 0;
    let primary = 0;
    let secondary = 0;
    let neutral = 0;
    let offClass = 0;
    let unknown = 0;
    let nonCollectible = 0;
    let invalidCopies = 0;
    const copyLimitViolations = [];
    const nonCollectibleCards = [];

    entries.forEach(entry => {
        const count = Number(entry?.count ?? 0);
        if (!Number.isFinite(count) || count <= 0) {
            return;
        }

        total += count;
        const cardDefinitionId = Number(entry?.cardDefinitionId);
        const effectiveCopyLimit = resolveEffectiveCopyLimit(copyLimitPolicy, cardDefinitionId);
        if (count > effectiveCopyLimit) {
            invalidCopies += (count - effectiveCopyLimit);
            const definition = getKnownCardDefinition(cardDefinitionId);
            const cardName = definition?.name ?? definition?.Name ?? `卡牌#${cardDefinitionId}`;
            copyLimitViolations.push({
                cardDefinitionId,
                cardName,
                count,
                limit: effectiveCopyLimit
            });
        }

        const definition = getKnownCardDefinition(cardDefinitionId);
        if (!definition) {
            unknown += count;
            return;
        }

        const collectible = definition?.isCollectible ?? definition?.IsCollectible;
        if (collectible !== true) {
            nonCollectible += count;
            nonCollectibleCards.push(definition?.name ?? definition?.Name ?? `卡牌#${cardDefinitionId}`);
            return;
        }

        const cardClass = normalizeEnumValue(definition?.class ?? definition?.Class ?? definition?.cardClass ?? definition?.card_class);
        if (!cardClass) {
            unknown += count;
            return;
        }

        if (cardClass === 'neutral') {
            neutral += count;
            return;
        }

        if (cardClass === primaryClass) {
            primary += count;
            return;
        }

        if (rule === 'dualClass' && secondaryClass && cardClass === secondaryClass) {
            secondary += count;
            return;
        }

        offClass += count;
    });

    return {
        rule,
        mode: rule,
        primaryClass,
        secondaryClass,
        total,
        primary,
        secondary,
        neutral,
        offClass,
        unknown,
        nonCollectible,
        nonCollectibleCards: Array.from(new Set(nonCollectibleCards)),
        invalidCopies,
        copyLimitViolations
    };
}

function getDeckSaveValidation(deck) {
    const stats = getDeckQuotaStats(deck);
    const issues = [];

    if (stats.total !== 40) {
        issues.push(`总数必须为 40（当前 ${stats.total}）`);
    }

    if (Array.isArray(stats.copyLimitViolations) && stats.copyLimitViolations.length > 0) {
        stats.copyLimitViolations.forEach(violation => {
            if (violation.limit === 0) {
                issues.push(`${violation.cardName} 在当前环境/规则已禁用（当前 ${violation.count} 张）`);
                return;
            }
            issues.push(`${violation.cardName} 最多可携带 ${violation.limit} 张（当前 ${violation.count} 张）`);
        });
    }

    if (stats.unknown > 0) {
        issues.push('包含未知卡牌，请刷新卡池后重试');
    }

    if (stats.nonCollectible > 0) {
        const names = Array.isArray(stats.nonCollectibleCards) ? stats.nonCollectibleCards.filter(Boolean) : [];
        issues.push(
            names.length > 0
                ? `包含不可收集卡牌：${names.join('、')}`
                : `包含不可收集卡牌 ${stats.nonCollectible} 张`
        );
    }

    if (stats.offClass > 0) {
        issues.push(`包含不合法职业卡 ${stats.offClass} 张`);
    }

    if (stats.rule === 'dualClass') {
        if (!stats.secondaryClass) {
            issues.push('混合职业模式必须选择副职业');
        }
        if (stats.primary < 24) {
            issues.push(`主职业卡片必须为24张以上（当前${stats.primary}张）`);
        }
        if (stats.secondary < 9) {
            issues.push(`副职业卡片必须为9张以上（当前${stats.secondary}张）`);
        }
    }

    return {
        stats,
        canSave: issues.length === 0,
        messages: issues,
        message: issues[0] ?? ''
    };
}

function normalizeSaveErrorMessages(value) {
    if (value === null || value === undefined) {
        return [];
    }
    const source = Array.isArray(value) ? value.join('\n') : String(value);
    return source
        .split(/\r?\n|[；;]+/)
        .map(item => item.trim())
        .filter(Boolean);
}

function getDeckValidationDisplayMessages(validation) {
    const baseMessages = Array.isArray(validation?.messages) && validation.messages.length > 0
        ? validation.messages
        : normalizeSaveErrorMessages(validation?.message || '当前卡组未通过构筑校验');

    return Array.from(new Set(baseMessages));
}

function getDeckSaveFeedbackMessages(validation) {
    const messages = validation.canSave ? [] : getDeckValidationDisplayMessages(validation);
    if (Array.isArray(state.saveErrorMessages) && state.saveErrorMessages.length > 0) {
        messages.push(...state.saveErrorMessages);
    }
    return Array.from(new Set(messages));
}

function clearDeckSaveErrors() {
    state.saveErrorMessages = [];
}

function updateDeckStatusUI() {
    if (!state.currentDeck) {
        return;
    }

    ensureCurrentDeckCopyLimitPolicyLoaded();

    const validation = getDeckSaveValidation(state.currentDeck);
    const feedbackMessages = getDeckSaveFeedbackMessages(validation);
    const displayMessage = feedbackMessages.join('\n');
    const countEl = document.getElementById('deck-count-display');
    if (countEl) {
        countEl.textContent = `${validation.stats.total}`;
    }
    const primaryCountEl = document.getElementById('deck-primary-count-display');
    if (primaryCountEl) {
        primaryCountEl.textContent = `${validation.stats.primary}`;
    }
    const secondaryCountEl = document.getElementById('deck-secondary-count-display');
    if (secondaryCountEl) {
        secondaryCountEl.textContent = `${validation.stats.secondary}`;
    }
    const typeCounts = getDeckTypeCounts(state.currentDeck);
    const followerCountEl = document.getElementById('deck-follower-count');
    if (followerCountEl) {
        followerCountEl.textContent = `从者 ${typeCounts.follower}`;
    }
    const spellCountEl = document.getElementById('deck-spell-count');
    if (spellCountEl) {
        spellCountEl.textContent = `法术 ${typeCounts.spell}`;
    }
    const amuletCountEl = document.getElementById('deck-amulet-count');
    if (amuletCountEl) {
        amuletCountEl.textContent = `护符 ${typeCounts.amulet}`;
    }

    const warningEl = document.getElementById('deck-save-warning');
    if (warningEl) {
        warningEl.textContent = displayMessage;
        warningEl.classList.toggle('hidden', feedbackMessages.length === 0);
    }

    const saveBtn = document.getElementById('save-deck-btn');
    if (saveBtn) {
        saveBtn.title = displayMessage;
    }
}

function updateDeckCountDisplay() {
    updateDeckStatusUI();
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
    notifyDeckListUpdated('delete');
    renderDeckSelection();
}

async function createNewDeck(environment, deckClass, rule = DEFAULT_DECK_RULE, secondaryClass = null) {
    const pendingCreation = beginPendingNewDeckCreation();
    const normalizedRule = normalizeDeckRule(rule);
    const normalizedSecondaryClass = resolveSecondaryClass(normalizedRule, deckClass, secondaryClass);
    const query = getDeckOwnerQuery();
    const payload = {
        name: 'New Deck',
        class: normalizeEnumValue(deckClass) || 'neutral',
        environment: normalizeEnvironment(environment),
        rule: normalizedRule
    };
    if (normalizedRule === 'dualClass' && normalizedSecondaryClass) {
        payload.secondaryClass = normalizedSecondaryClass;
    }
    try {
        const created = await apiPostJson(`/api/decks?${query}`, payload);
        const createdId = created?.deckId ?? created?.id ?? null;
        const createdPending = setPendingNewDeckCreationDeckId(pendingCreation.requestId, createdId);
        if (shouldCleanupPendingDeckCreation(createdPending, getCurrentRouteName())) {
            await cleanupAbandonedPendingNewDeckCreation(createdPending, 'discard');
            return;
        }

        state.newDeckId = createdId;
        await loadDecks();

        const latestPending = findPendingNewDeckCreation(pendingCreation.requestId);
        if (shouldCleanupPendingDeckCreation(latestPending, getCurrentRouteName())) {
            await cleanupAbandonedPendingNewDeckCreation(latestPending, 'discard');
            return;
        }

        notifyDeckListUpdated('create');
        if (createdId) {
            await editDeck(createdId);
        }
        clearPendingNewDeckCreation(pendingCreation.requestId);
    } catch (error) {
        clearPendingNewDeckCreation(pendingCreation.requestId);
        throw error;
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
    notifyDeckListUpdated('import');
    return importedDeckId;
}

async function editDeck(deckId) {
    const deck = await fetchDeckDetail(deckId);
    const resolvedDeckId = getDeckId(deck) ?? deckId;
    const isNewDeck = state.newDeckId && resolvedDeckId && String(state.newDeckId) === String(resolvedDeckId);
    const deckClass = deck.class ?? deck.Class;
    const deckRule = deck.rule ?? deck.Rule;
    if (!isDeckRuleUiVisible(deckRule)) {
        alert('混合职业卡组当前已临时下线。');
        await loadDecks();
        renderDeckSelection();
        return;
    }
    const deckSecondaryClass = deck.secondaryClass ?? deck.SecondaryClass;
    state.currentDeck = {
        ...deck,
        environment: normalizeEnvironment(deck.environment ?? deck.Environment),
        class: normalizeEnumValue(deckClass),
        rule: normalizeDeckRule(deckRule),
        secondaryClass: resolveSecondaryClass(deckRule, deckClass, deckSecondaryClass),
        cards: Array.isArray(deck.cards) ? deck.cards : []
    };
    state.currentDeck.isNew = Boolean(isNewDeck);
    setCurrentDeckSnapshot(state.currentDeck);
    await ensureCopyLimitPolicyLoaded(state.currentDeck.environment, state.currentDeck.rule);
    clearDeckSaveErrors();
    if (isNewDeck) {
        state.newDeckId = null;
    }
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
    if (entry && entry.count >= DEFAULT_MAX_COPIES_PER_CARD) {
        alert(`同名卡最多 ${DEFAULT_MAX_COPIES_PER_CARD} 张`);
        return;
    }

    if (entry) {
        entry.count += 1;
    } else {
        entries.push({ cardDefinitionId: cardId, count: 1 });
    }
    state.currentDeck.cards = entries;
    clearDeckSaveErrors();
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
    clearDeckSaveErrors();
    renderDeckList();
    renderManaCurve();
    updateDeckCountDisplay();
}

async function saveDeck({ closeEditorOnSuccess = true } = {}) {
    if (!state.currentDeck) return false;
    updateDeckStatusUI();
    const validation = getDeckSaveValidation(state.currentDeck);
    const feedbackMessages = getDeckSaveFeedbackMessages(validation);
    if (feedbackMessages.length > 0) {
        const shouldContinue = window.confirm(
            `${feedbackMessages.join('\n')}\n是否继续保存？`
        );
        if (!shouldContinue) {
            return false;
        }
    }

    clearDeckSaveErrors();
    updateDeckStatusUI();

    const normalizedRule = normalizeDeckRule(state.currentDeck.rule);
    const normalizedSecondaryClass = resolveSecondaryClass(normalizedRule, state.currentDeck.class, state.currentDeck.secondaryClass);
    const query = getDeckOwnerQuery();
    const deckId = state.currentDeck.deckId || state.currentDeck.id;
    const allowCreateFallback = state.currentDeck.isNew === true || !deckId;
    const payload = {
        name: state.currentDeck.name,
        class: state.currentDeck.class || 'Neutral',
        environment: normalizeEnvironment(state.currentDeck.environment),
        rule: normalizedRule,
        secondaryClass: normalizedSecondaryClass,
        cards: state.currentDeck.cards ?? []
    };

    const isNotFoundError = (error) => {
        const message = String(error?.message || '').toLowerCase();
        return message.includes('deck not found') || message.includes('target deck not found') || message.includes('not found');
    };

    if (deckId) {
        try {
            await apiPutJson(`/api/decks/${deckId}?${query}`, payload);
        } catch (error) {
            if (!isNotFoundError(error) || !allowCreateFallback) {
                state.saveErrorMessages = normalizeSaveErrorMessages(error?.message || '保存失败，请稍后重试');
                updateDeckStatusUI();
                return false;
            }
            try {
                await apiPostJson(`/api/decks?${query}`, payload);
            } catch (createError) {
                state.saveErrorMessages = normalizeSaveErrorMessages(createError?.message || '保存失败，请稍后重试');
                updateDeckStatusUI();
                return false;
            }
        }
    } else {
        try {
            await apiPostJson(`/api/decks?${query}`, payload);
        } catch (error) {
            state.saveErrorMessages = normalizeSaveErrorMessages(error?.message || '保存失败，请稍后重试');
            updateDeckStatusUI();
            return false;
        }
    }

    clearDeckSaveErrors();
    await loadDecks();
    notifyDeckListUpdated('save');
    clearCurrentDeckState();
    if (closeEditorOnSuccess) {
        renderDeckSelection();
    }
    return true;
}
