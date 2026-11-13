import { mountStyleEditor } from './cardStyles.js';
import { installCardHistoryActions } from './cardHistory.js';
import { confirmDialog } from './siteDialog.js';
import { showAlert } from './siteDialog.js';
import { VisualCardCanvas, sectionsFromDescription, findCardByReference } from './components/VisualCardCanvas.js';
import { normalizeRuleSectionText } from './mse-editor-test/document.js';
import { normalizeAbilityTargetsAndRefs } from './abilityTargetNormalization.js';
import { CARD_SETS_UPDATED_EVENT } from './devToolsCardSetEnvironmentMembership.js';
// src/cardEditor.js - 卡牌编辑器主模块
//
// 相关文档:
// - UI设计: 文档/06-UI设计/01-卡牌编辑器原型设计.md
// - 元数据驱动UI: 文档/06-UI设计/04-元数据驱动UI架构设计.md
//

import { MetadataManager } from './metadataManager.js';
import { DynamicUIRenderer } from './dynamicUIRenderer.js';
import { buildPreviewSlateHtml } from './previewSlate.js';
import { showPreviewDescriptionTooltip } from './previewDescriptionTooltip.js';
import { hydrateDescriptionReferences } from './previewDescriptionReferences.js';
import { showCardDetailModal as openCardDetailModal } from './components/CardDetailMockup.js';
const showCardDetailModal = (card, options = {}) => openCardDetailModal(card?.styleOriginalCard || card, { ...options, originalCardMode: true, stylePreferenceApplied: true });
import { getBlockingLoader } from './components/blockingLoader.js';
import { ensureEnumLocalizations, getCachedEnumLabel } from './enumLocalizationCache.js';
import {
    ensureAbilityData,
    refreshAbilityDataCache,
    upsertAbilityDataCacheEntry,
    removeAbilityDataCacheEntry,
    markAbilityDataCacheDirty,
    setActiveAbilityDataScope
} from './abilityDataCache.js';
import { compareCardsForEditorAndDeck } from './cardListSort.js';
import { compareCardSetDisplayOrder } from './cardSetDisplayOrder.js';
import {
    CARD_SET_UNSPECIFIED_ENVIRONMENT,
    buildGroupedCardSetOptions
} from './cardSetFilterOptions.js';
import { loadCardDefinitions, normalizeCardPoolScope } from './cardDefinitionsLoader.js';
import {
    ensureDeckEnvironmentOptions,
    getDeckEnvironmentLabel,
    getDeckEnvironmentOptions,
    normalizeDeckEnvironment
} from './deckEnvironmentOptions.js';
import {
    CARD_CATALOG_INVALIDATED_EVENT,
    isAbilityOnlyInvalidation,
    syncDataRevisionFromResponse
} from './dataChangeEvents.js';
import { getUserSession } from './userSession.js';

const EDITOR_USER_ID_HEADER = 'X-Editor-UserId';

function normalizeEnumName(value) {
    if (typeof value !== 'string') return value;
    const trimmed = value.trim();
    if (!trimmed) return trimmed;
    if (trimmed.includes('_')) {
        return trimmed
            .split('_')
            .map(part => part ? part[0].toUpperCase() + part.slice(1) : part)
            .join('_');
    }
    return trimmed[0].toUpperCase() + trimmed.slice(1);
}

function normalizeEnumLookupKey(value) {
    const text = String(value ?? '').trim();
    return text ? text.toLowerCase() : '';
}

function escapeRegExp(value) {
    return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function buildEditorIdentityHeaders(headers = {}) {
    const nextHeaders = { ...headers };
    const session = getUserSession();
    if (session?.userId) {
        nextHeaders[EDITOR_USER_ID_HEADER] = session.userId;
    }
    return nextHeaders;
}

async function readErrorResponseMessage(response) {
    const fallback = response?.statusText || 'Request failed';
    if (!response || typeof response.text !== 'function') {
        return fallback;
    }

    try {
        const text = (await response.text()).trim();
        if (!text) {
            return fallback;
        }

        try {
            const payload = JSON.parse(text);
            return payload?.error
                || payload?.message
                || payload?.title
                || payload?.detail
                || text;
        } catch (_) {
            return text;
        }
    } catch (_) {
        return fallback;
    }
}

function formatEditorMetaTimestamp(value) {
    if (!value) return '';

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
        return '';
    }

    return new Intl.DateTimeFormat('zh-CN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false
    }).format(parsed);
}

function equalsIgnoreCase(a, b) {
    if (typeof a !== 'string' || typeof b !== 'string') return a === b;
    return a.toLowerCase() === b.toLowerCase();
}

const LEADER_CARD_ID = 0;
const MIRAGE_LEADER_CARD_ID = -1;
const MIRAGE_CARD_SET_NAME = '幻境战棋';

function normalizeCardDefinitionId(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
}

function getCardDefinitionId(card) {
    const id = card?.id ?? card?.CardId ?? card?.Id ?? card?.cardId ?? null;
    return id === undefined ? null : id;
}

function isLeaderCardDefinition(card) {
    const cardId = normalizeCardDefinitionId(getCardDefinitionId(card));
    const rawType = card?.type ?? card?.Type ?? '';
    const cardType = normalizeEnumName(rawType);
    const cardName = card?.name ?? card?.Name ?? '';
    const isLeaderType = equalsIgnoreCase(cardType, 'Leader') || rawType === 0;
    return isLeaderType
        || cardId === LEADER_CARD_ID
        || cardId === MIRAGE_LEADER_CARD_ID
        || equalsIgnoreCase(cardName, '主战者')
        || equalsIgnoreCase(cardName, '幻境主战者');
}

let editorEnumLocalizationPromise = null;
function ensureEditorEnumLocalization(editorInstance) {
    if (!editorEnumLocalizationPromise) {
        editorEnumLocalizationPromise = ensureEnumLocalizations([
            'CardRarity',
            'CardClass',
            'CardType',
            'AbilityType',
            'AbilityKeyword'
        ])
            .then(() => {
                if (editorInstance?.currentCard) {
                    editorInstance.updateCardPreview();
                }
                if (editorInstance?.currentCardAbilities?.length) {
                    editorInstance.renderAbilitiesListContent(editorInstance.currentCardAbilities);
                }
            })
            .catch(error => {
                console.warn('CardEditor: Failed to load card enum labels.', error);
            });
    }
    return editorEnumLocalizationPromise;
}

function bindPreviewDetailHandler(previewContainer) {
    if (!previewContainer || previewContainer._detailHandlerBound) return;
    previewContainer._detailHandlerBound = true;
    previewContainer.addEventListener('click', (event) => {
        const clickedPreviewName = event.target.closest('.preview-detail-name');
        if (!clickedPreviewName) return;
        const payload = previewContainer._detailPayload;
        if (!payload?.card) return;
        showCardDetailModal(payload.card, {
            previewUrl: payload.previewUrl,
            detailUrl: payload.detailUrl
        });
    });
}

function getCardSetInfo(card) {
    const name = card?.card_set ?? card?.cardSet ?? card?.CardSet ?? card?.card_set_name ?? card?.cardSetName ?? '';
    const code = card?.card_set_code ?? card?.cardSetCode ?? '';
    const abbr = card?.card_set_abbr ?? card?.cardSetAbbr ?? '';
    const rawEnvironment = card?.card_set_environment ?? card?.cardSetEnvironment ?? card?.card_set_environments ?? card?.cardSetEnvironments ?? null;
    return {
        name: name == null ? '' : String(name).trim(),
        code: code == null ? '' : String(code).trim(),
        abbr: abbr == null ? '' : String(abbr).trim(),
        environments: Array.isArray(rawEnvironment)
            ? rawEnvironment.map(value => (value == null ? '' : String(value))).filter(Boolean)
            : (rawEnvironment == null ? [] : [String(rawEnvironment)])
    };
}

function getCardSetKey(card) {
    const info = getCardSetInfo(card);
    return info.name;
}

function getCardSetLabel(info) {
    if (!info) return '';
    return info.name || '';
}

function normalizeCardSetEnvironmentValue(value) {
    return normalizeDeckEnvironment(value, '');
}

function getCardSetEnvironmentLabel(value) {
    if (value === CARD_SET_UNSPECIFIED_ENVIRONMENT) {
        return '未指定环境';
    }

    return getDeckEnvironmentLabel(value);
}

function normalizeAbilityKeywords(ability) {
    const raw = ability?.abilityKeywords ?? [];
    const normalized = [];
    const addKeyword = (keyword, value) => {
        if (!keyword || typeof keyword !== 'string') return;
        const existing = normalized.find(k => k.keyword.toLowerCase() === keyword.toLowerCase());
        if (existing) {
            if (value !== null && value !== undefined) {
                existing.value = value;
            }
            return;
        }
        normalized.push({ keyword, value: value ?? null });
    };

    if (Array.isArray(raw)) {
        raw.forEach(entry => {
            if (!entry) return;
            if (typeof entry === 'string') {
                addKeyword(entry);
                return;
            }
            if (typeof entry === 'object') {
                const keyword = entry.keyword || entry.tag || entry.Keyword || entry.Tag;
                const value = entry.value ?? entry.amount ?? entry.cost ?? entry.Value ?? null;
                addKeyword(keyword, value);
            }
        });
    } else if (typeof raw === 'string') {
        addKeyword(raw);
    }

    return normalized;
}

function formatAbilityTagSummary(keywords) {
    if (!Array.isArray(keywords) || keywords.length === 0) return '';
    return keywords
        .map(entry => entry.value !== null && entry.value !== undefined
            ? `${entry.keyword}:${entry.value}`
            : entry.keyword)
        .filter(text => text && typeof text === 'string')
        .join(', ');
}

function formatAbilityKeywordsDisplay(ability) {
    const keywords = normalizeAbilityKeywords(ability);
    if (keywords.length === 0) {
        return '';
    }
    return formatAbilityTagSummary(keywords);
}

function formatAbilityKeywordLabel(entry) {
    if (!entry || !entry.keyword) return '';
    const localizedKeyword = getCachedEnumLabel('AbilityKeyword', entry.keyword, entry.keyword);
    if (entry.value !== null && entry.value !== undefined && entry.value !== '') {
        return `${localizedKeyword}:${entry.value}`;
    }
    return localizedKeyword;
}

const ABILITY_TYPE_LABELS = {
    0: 'Triggered',
    1: 'Continuous',
    2: 'Activated'
};

const ABILITY_TYPE_DISPLAY_LABELS = {
    triggered: '触发式',
    continuous: '持久式',
    activated: '启动式',
    '0': '触发式',
    '1': '持久式',
    '2': '启动式'
};

function getAbilityTypeDisplayLabel(value, fallback = '') {
    const normalizedValue = normalizeEnumLookupKey(value);
    if (normalizedValue && ABILITY_TYPE_DISPLAY_LABELS[normalizedValue]) {
        return ABILITY_TYPE_DISPLAY_LABELS[normalizedValue];
    }

    const cachedLabel = getCachedEnumLabel('AbilityType', value, fallback);
    const normalizedCachedLabel = normalizeEnumLookupKey(cachedLabel);
    if (normalizedCachedLabel && ABILITY_TYPE_DISPLAY_LABELS[normalizedCachedLabel]) {
        return ABILITY_TYPE_DISPLAY_LABELS[normalizedCachedLabel];
    }

    return cachedLabel || fallback || '触发式';
}

function resolveAbilityTypeLabel(ability, config) {
    const rawTypeValue = ability?.abilityType ?? ability?.AbilityType ?? config?.abilityType;
    const configType = normalizeEnumName(config?.type ?? ability?.type ?? ability?.Type ?? ability?.jsonConfig?.type ?? '');
    const localizedConfigType = getAbilityTypeDisplayLabel(configType, configType);
    if (rawTypeValue !== null && rawTypeValue !== undefined && rawTypeValue !== '') {
        const numericValue = Number(rawTypeValue);
        if (Number.isFinite(numericValue)) {
            const numericLabel = getAbilityTypeDisplayLabel(
                rawTypeValue,
                getAbilityTypeDisplayLabel(ABILITY_TYPE_LABELS[numericValue], ABILITY_TYPE_LABELS[numericValue])
            );
            if (configType && numericLabel && configType.toLowerCase() !== ABILITY_TYPE_LABELS[numericValue]?.toLowerCase()) {
                return localizedConfigType;
            }
            return numericLabel || localizedConfigType || getAbilityTypeDisplayLabel('Triggered', '触发式');
        }
        if (typeof rawTypeValue === 'string') {
            const normalizedType = normalizeEnumName(rawTypeValue);
            return getAbilityTypeDisplayLabel(
                rawTypeValue,
                getAbilityTypeDisplayLabel(normalizedType, normalizedType || localizedConfigType || '触发式')
            );
        }
    }
    return localizedConfigType || getAbilityTypeDisplayLabel('Triggered', '触发式');
}

function normalizeEventKey(value) {
    return String(value ?? '').trim().toLowerCase();
}

function normalizeEventTypeValue(value) {
    if (!value) return '';
    if (typeof value === 'string' || typeof value === 'number') {
        return String(value);
    }
    if (typeof value === 'object') {
        return value.eventType ?? value.EventType ?? value.type ?? value.Type ?? value.name ?? value.Name ?? '';
    }
    return String(value);
}

function normalizeEventTypeList(raw) {
    if (Array.isArray(raw)) {
        return raw
            .map(item => normalizeEventTypeValue(item))
            .filter(Boolean);
    }
    const value = normalizeEventTypeValue(raw);
    return value ? [value] : [];
}

function extractTriggerEventTypes(triggerConfig) {
    if (!triggerConfig) {
        return [];
    }
    const eventTypes = triggerConfig.eventTypes ?? triggerConfig.EventTypes;
    if (eventTypes !== undefined) {
        return normalizeEventTypeList(eventTypes);
    }
    return normalizeEventTypeList(triggerConfig);
}

function buildEventLabelLookup(events = []) {
    const lookup = new Map();
    (events || []).forEach(event => {
        if (!event) return;
        const key = normalizeEventKey(event.eventType ?? event.type ?? event.name);
        const label = event.displayName ?? event.name ?? event.eventType ?? event.type;
        if (key && label && !lookup.has(key)) {
            lookup.set(key, String(label));
        }
    });
    return lookup;
}

function resolveEventDisplayName(eventType, lookup) {
    const key = normalizeEventKey(eventType);
    if (key && lookup?.has(key)) {
        return lookup.get(key);
    }
    return normalizeEnumName(eventType);
}

function formatAbilityEventLabels(config, abilityTypeLabel, eventLabelLookup) {
    const typeName = String(abilityTypeLabel || config?.type || '').trim().toLowerCase();
    const isContinuous = typeName === 'continuous';
    const triggerEvents = normalizeEventTypeList(config?.trigger?.eventTypes ?? config?.trigger?.EventTypes);
    const selected = isContinuous ? triggerEvents : triggerEvents;
    const resolved = selected
        .map(eventType => resolveEventDisplayName(eventType, eventLabelLookup))
        .filter(Boolean);
    return Array.from(new Set(resolved));
}

/**
 * 卡牌编辑器主类
 * 实现三栏式布局和基础功能
 */
class CardEditor {
    constructor(options = {}) {
        this.container = null;
        this.containerId = options.containerId || 'card-editor-container';
        this.layoutMode = options.layoutMode || 'default';
        this.cardPoolScope = normalizeCardPoolScope(options.cardPoolScope || (this.containerId.includes('mirage') ? 'mirage' : 'standard'));
        this.metadataManager = new MetadataManager();
        this.uiRenderer = new DynamicUIRenderer();
        this.currentCard = null;
        this.isInitialized = false;
        this.keywordLabelMap = null;
        this.artCacheBuster = 0;
        this.cardListScrollTop = 0;
        this.shouldRestoreCardListScroll = false;
        this.allCardsData = [];
        this.filteredCardsData = [];
        this.cardSetDefinitions = [];
        this.dataRevision = null;
        this.cardListCache = null;
        this.cardDetailCache = new Map();
        this.abilitiesByCardCache = new Map();
        this.abilityDetailCache = new Map();
        this.abilityCountsCache = new Map();
        this.abilitiesLibraryCache = null;
        this.cardCatalogInvalidationHandler = null;
        this.cardSetsUpdatedHandler = null;
        this.editorMode = 'card';
        this.cardTab = 'basic';
        this.playerSettingsTab = 'leaderAbility';
        this.previousCardIdBeforeSettingsMode = null;
        this.tribeDefinitions = [];
        this.tribeSearchQuery = '';
        this.activeTribeDefinitionName = null;
        this.isCreatingTribeDefinition = false;
    }

    isMirageScope() {
        return this.cardPoolScope === 'mirage';
    }

    getScopeQueryParam(prefix = '?') {
        return `${prefix}scope=${encodeURIComponent(this.cardPoolScope)}`;
    }

    getLeaderCardId() {
        return this.isMirageScope() ? MIRAGE_LEADER_CARD_ID : LEADER_CARD_ID;
    }

    /**
     * 初始化卡牌编辑器
     */
    async initialize() {
        setActiveAbilityDataScope(this.cardPoolScope);
        if (this.isInitialized) {
            return;
        }


        try {
            // 获取容器元素
            this.container = document.getElementById(this.containerId);
            if (!this.container) {
                throw new Error('Card editor container not found');
            }

            // 显示加载状态
            this.showLoading('正在加载元数据...');

            // 初始化元数据管理器
            await this.metadataManager.initialize();

            // 初始化UI渲染器
            await this.uiRenderer.initialize(this.metadataManager);

            await ensureDeckEnvironmentOptions();

            // 加载卡包定义用于输入补全
            await this.loadCardSetDefinitions();

            // 创建编辑器布局
            this.createEditorLayout();
            await this.renderEditorModeContent();
            this.updateTabAvailability();

            // 初始化列表筛选控件选项
            await this.populateCardListFilterOptions();

            // 绑定事件监听器
            this.bindEventListeners();
            this.bindGlobalInvalidationListener();
            // 加载卡牌列表
            await this.loadCardList();

            this.isInitialized = true;

        } catch (error) {
            console.error('CardEditor: Initialization failed:', error);
            this.showError('初始化失败', error.message);
            throw error;
        }
    }

    /**
     * 显示加载状态
     */
    showLoading(message = 'SYSTEM LOADING...') {
        if (this.container) {
            this.container.innerHTML = `
                <div class="loading-container tech-panel">
                    <div class="loading-spinner-tech"></div>
                    <div class="loading-text neon-text">${message}</div>
                </div>
            `;
        }
    }

    /**
     * 显示错误信息
     */
    showError(title, message) {
        if (this.container) {
            this.container.innerHTML = `
                <div class="error-container tech-panel error-state">
                    <div class="error-icon-tech"></div>
                    <h3 class="error-title neon-text-red">${title}</h3>
                    <p class="error-message">${message}</p>
                    <button class="btn-tech btn-retry" onclick="location.reload()">REBOOT SYSTEM</button>
                </div>
            `;
        }
    }

    /**
     * 创建编辑器的三栏式布局
     */
    createEditorLayout() {
        const layoutClass = this.layoutMode === 'test'
            ? 'card-editor-layout layout-test'
            : 'card-editor-layout';

        this.container.innerHTML = `
            <div class="${layoutClass}" data-mobile-pane="list">
                <div class="editor-grid-bg"></div>

                <div class="editor-mobile-pane-bar" role="tablist" aria-label="工坊视图切换">
                    <button type="button" class="editor-mobile-pane-btn active" data-mobile-pane-btn="list">卡牌列表</button>
                    <button type="button" class="editor-mobile-pane-btn" data-mobile-pane-btn="edit">数值与能力</button>
                    <button type="button" class="editor-mobile-pane-btn" data-mobile-pane-btn="preview">预览与卡面</button>
                </div>
                
                <!-- 左栏：卡牌列表 -->
                <div class="editor-sidebar left-sidebar tech-panel">
                    <div class="sidebar-header">
                        <h3 class="tech-header">${this.isMirageScope() ? '幻境专属卡池' : '卡牌数据库'}</h3>
                        <div class="sidebar-header-actions">
                            <button class="btn-tech btn-primary" id="new-card-btn">
                                ${this.isMirageScope() ? '新建幻境卡' : '新建卡牌'}
                            </button>
                            ${this.isMirageScope() ? `
                            <button class="btn-tech btn-secondary" id="import-classic-card-btn" title="从普通对战卡池深拷贝导入至幻境卡池（自动复制立绘与衍生物闭包）">
                                从普通卡导入
                            </button>
                            ` : ''}
                            <button class="btn-tech btn-secondary" id="player-settings-btn">
                                玩家设定
                            </button>
                        </div>
                    </div>
                    <div class="sidebar-content">
                        <div class="search-section">
                            <input type="text" id="card-search" placeholder="搜索卡牌..." class="search-input tech-input">
                            <div class="filter-section">
                                <select id="class-filter" class="filter-select tech-select">
                                    <option value="">全部职业</option>
                                </select>
                                <select id="type-filter" class="filter-select tech-select">
                                    <option value="">全部类型</option>
                                </select>
                                <select id="rarity-filter" class="filter-select tech-select">
                                    <option value="">全部稀有度</option>
                                </select>
                                <select id="set-filter" class="filter-select tech-select">
                                    <option value="">全部卡包</option>
                                </select>
                            </div>
                        </div>
                        <div class="card-list custom-scrollbar" id="card-list">
                            <div class="card-list-placeholder">
                                <p class="tech-text-dim">未找到卡牌</p>
                                <small class="tech-text-small">可以从上方新建卡牌</small>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- 中栏：编辑区 -->
                <div class="editor-main tech-panel">
                    <div class="editor-header">
                        <div class="editor-title-block">
                            <h3 id="editor-title" class="tech-header-large">等待选择卡牌</h3>
                            <div id="editor-meta" class="editor-meta tech-text-dim" hidden></div>
                        </div>
                        <div class="editor-actions">
                            <button class="btn-tech btn-secondary" id="save-btn" disabled>保存</button>
                            <button class="btn-tech btn-secondary" id="save-as-btn" disabled>另存为</button>
                            <button class="btn-tech btn-danger" id="delete-btn" disabled>删除</button>
                        </div>
                    </div>
                    <div class="editor-content" id="editor-mode-host"></div>
                </div>

                <!-- 右栏：预览 / MSE 设置区 -->
                <div class="editor-sidebar editor-preview right-sidebar tech-panel">
                    <div class="sidebar-header">
                        <h3 class="tech-header" id="right-sidebar-title">预览</h3>
                    </div>
                    <div class="sidebar-content custom-scrollbar">
                        <div class="mse-inspector-slot" id="mse-right-settings" hidden></div>
                        <div class="classic-preview-pane" id="classic-preview-pane">
                            <div class="card-preview-container" id="card-preview">
                                <div class="preview-placeholder">
                                    <p class="tech-text-dim">等待载入数据</p>
                                </div>
                            </div>
                            <div class="preview-info">
                                <h4 class="tech-header-small">调试信息</h4>
                                <div class="preview-details tech-code-block" id="preview-details">
                                    <p>暂无数据</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    renderCardEditorShell() {
        return `
            <div class="card-editor-shell">
                <div class="editor-tabs">
                    <button class="tab-button${this.cardTab === 'basic' ? ' active' : ''}" data-tab="basic">卡牌信息</button>
                    <button class="tab-button${this.cardTab === 'abilities' ? ' active' : ''}" data-tab="abilities">能力管理</button>
                    <button class="tab-button${this.cardTab === 'assets' ? ' active' : ''}" data-tab="assets">媒体资源</button>
                    <button class="tab-button" data-tab="styles">卡片风格</button>
                </div>
                <div class="tab-content custom-scrollbar">
                    <div class="tab-panel${this.cardTab === 'basic' ? ' active' : ''}" id="basic-tab">
                        <div class="form-placeholder">
                            <p class="tech-text-dim">请选择一张卡牌进行编辑</p>
                        </div>
                    </div>
                    <div class="tab-panel${this.cardTab === 'abilities' ? ' active' : ''}" id="abilities-tab">
                        <div class="abilities-container">
                            <div class="abilities-header">
                                <h4 class="tech-header-small">能力配置</h4>
                                <div class="abilities-header-actions">
                                    <button class="btn-tech btn-small" id="generate-dsl-ability-btn" title="根据卡面描述自动编译生成 DSL 能力配置">从描述生成能力 (DSL)</button>
                                    <button class="btn-tech btn-small" id="add-ability-btn">新增能力</button>
                                    <button class="btn-tech btn-small" id="link-ability-btn">复制已有能力</button>
                                </div>
                            </div>
                            <div class="abilities-content">
                                <div class="abilities-list" id="abilities-list">
                                    <div class="abilities-placeholder">
                                        <p class="tech-text-dim">当前未绑定能力</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="tab-panel" id="styles-tab"></div>
                    <div class="tab-panel${this.cardTab === 'assets' ? ' active' : ''}" id="assets-tab">
                        <div class="assets-container" id="assets-container">
                            <div class="form-placeholder">
                                <p class="tech-text-dim">请选择一张卡牌进行编辑</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    renderPlayerSettingsShell() {
        return `
            <div class="settings-shell">
                <div class="settings-hero tech-section">
                    <div>
                        <h4 class="tech-header-small">玩家级设定</h4>
                        <p class="tech-text-dim">${this.isMirageScope() ? '集中维护幻境主战者能力等幻境系统设定。' : '集中维护主战者能力、种类定义等系统数据.'}</p>
                    </div>
                    <div class="settings-hero-badges">
                        <span class="tech-tag">主战者能力</span>
                        ${this.isMirageScope() ? '' : '<span class="tech-tag">种类管理</span>'}
                    </div>
                </div>
                <div class="editor-tabs settings-tabs" role="tablist" aria-label="玩家设定">
                    <button class="tab-button settings-tab-button${this.playerSettingsTab === 'leaderAbility' ? ' active' : ''}" data-settings-tab="leaderAbility" aria-selected="${this.playerSettingsTab === 'leaderAbility'}">
                        主战者能力
                    </button>
                    ${this.isMirageScope() ? '' : `
                    <button class="tab-button settings-tab-button${this.playerSettingsTab === 'tribeManagement' ? ' active' : ''}" data-settings-tab="tribeManagement" aria-selected="${this.playerSettingsTab === 'tribeManagement'}">
                        种类管理
                    </button>`}
                </div>
                <div class="tab-content player-settings-tab-content custom-scrollbar">
                    <div class="tab-panel settings-tab-panel${this.playerSettingsTab === 'leaderAbility' ? ' active' : ''}" id="leader-ability-settings-tab">
                        <div class="abilities-container">
                            <div class="abilities-header">
                                <div>
                                    <h4 class="tech-header-small">主战者能力</h4>
                                    <p class="tech-text-dim">维护主战者的能力列表与 DSL 配置。</p>
                                </div>
                                <div class="abilities-header-actions">
                                    <button class="btn-tech btn-small" id="generate-dsl-ability-btn" title="根据卡面描述自动编译生成 DSL 能力配置">从描述生成能力 (DSL)</button>
                                    <button class="btn-tech btn-small" id="add-ability-btn">新增能力</button>
                                    <button class="btn-tech btn-small" id="link-ability-btn">复制已有能力</button>
                                </div>
                            </div>
                            <div class="abilities-content">
                                <div class="abilities-list" id="abilities-list">
                                    <div class="abilities-placeholder">
                                        <p class="tech-text-dim">正在载入主战者能力...</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="tab-panel settings-tab-panel${this.playerSettingsTab === 'tribeManagement' ? ' active' : ''}" id="tribe-management-tab">
                        <div class="form-placeholder">
                            <p class="tech-text-dim">正在载入种类定义...</p>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    async renderEditorModeContent() {
        const host = this.container?.querySelector('#editor-mode-host');
        if (!host) {
            return;
        }

        host.innerHTML = this.editorMode === 'playerSettings'
            ? this.renderPlayerSettingsShell()
            : this.renderCardEditorShell();

        const editorMain = this.container.querySelector('.editor-main');
        if (editorMain) {
            editorMain.classList.toggle('player-settings-mode', this.editorMode === 'playerSettings');
        }
        this.syncVisualCanvasLayout();

        this.syncEditorModeUi();

        if (this.editorMode === 'playerSettings') {
            this.disableEditorButtons();
            await this.switchPlayerSettingsTab(this.playerSettingsTab);
            return;
        }

        this.updateTabAvailability();

        if (!this.currentCard) {
            this.updateEmptyPreviewState();
            return;
        }

        const activeTab = this.cardTab || 'basic';
        if (activeTab === 'basic') {
            await this.renderBasicInfoForm();
        } else if (activeTab === 'abilities') {
            await this.renderAbilitiesList();
        } else if (activeTab === 'assets') {
            this.renderAssetsTab();
        }
    }

    /**
     * 绑定事件监听器
     */
    bindEventListeners() {
        this.eventController?.abort();
        this.eventController = new AbortController();
        const listen = (target, type, handler) => target.addEventListener(type, handler, { signal: this.eventController.signal });
        listen(this.container, 'click', (event) => {
            const tokenEl = event.target.closest('[data-preview-token]');
            if (!tokenEl) return;
            const tokenType = tokenEl.dataset.previewToken;
            const tokenValue = tokenEl.dataset.previewValue;
            if (!tokenType || !tokenValue) return;
            event.preventDefault();
            event.stopPropagation();
            void showPreviewDescriptionTooltip(tokenEl, tokenType, tokenValue, {
                originalCardMode: true,
                openCardDetail: showCardDetailModal,
                cards: this.allCardsData,
                currentCardId: this.currentCard?.id ?? this.currentCard?.Id ?? this.currentCard?.CardId ?? null,
                currentCardName: this.currentCard?.name ?? this.currentCard?.Name ?? ''
            });
        });

        listen(this.container, 'click', async (event) => {
            const mobilePaneButton = event.target.closest('[data-mobile-pane-btn]');
            if (mobilePaneButton && this.container.contains(mobilePaneButton)) {
                this.setMobilePane(mobilePaneButton.dataset.mobilePaneBtn);
                return;
            }

            const settingsTabButton = event.target.closest('.settings-tab-button');
            if (settingsTabButton && this.container.contains(settingsTabButton)) {
                await this.switchPlayerSettingsTab(settingsTabButton.dataset.settingsTab);
                return;
            }

            const cardTabButton = event.target.closest('.tab-button[data-tab]');
            if (cardTabButton && this.container.contains(cardTabButton)) {
                if (cardTabButton.disabled) {
                    return;
                }

                this.switchTab(cardTabButton.dataset.tab);
                return;
            }

            const tribeListItem = event.target.closest('.tribe-list-item');
            if (tribeListItem && this.container.contains(tribeListItem)) {
                this.selectTribeDefinition(tribeListItem.dataset.tribeName);
                return;
            }

            const actionButton = event.target.closest('button');
            if (!actionButton || !this.container.contains(actionButton)) {
                return;
            }

            switch (actionButton.id) {
            case 'save-btn':
                await this.saveCurrentCard(false);
                return;
            case 'save-as-btn':
                await this.saveCurrentCard(true);
                return;
            case 'delete-btn':
                await this.deleteCurrentCard();
                return;
            case 'new-card-btn':
                await this.createNewCard();
                return;
            case 'import-classic-card-btn':
                await this.showImportClassicCardModal();
                return;
            case 'player-settings-btn':
                if (this.editorMode === 'playerSettings') {
                    await this.exitPlayerSettingsMode();
                } else {
                    await this.enterPlayerSettingsMode();
                }
                return;
            case 'generate-dsl-ability-btn':
                await this.generateAbilitiesFromVisualCard();
                return;
            case 'add-ability-btn':
                this.showAddAbilityModal();
                return;
            case 'link-ability-btn':
                this.showLinkExistingAbilityModal();
                return;
            case 'new-tribe-btn':
                this.startNewTribeDefinitionDraft();
                return;
            case 'refresh-tribes-btn':
                await this.loadTribeDefinitions({ force: true });
                await this.renderTribeManagementTab();
                return;
            case 'save-tribe-definition-btn':
                await this.saveTribeDefinitionFromForm();
                return;
            case 'delete-tribe-definition-btn':
                await this.deleteActiveTribeDefinition();
                return;
            default:
                break;
            }
        });

        listen(document, 'paste', async (event) => {
            if (!this.container || !this.container.offsetParent) return;
            if (document.querySelector('.mse-crop-modal-backdrop')) return;
            if (this.editorMode !== 'card' || this.cardTab !== 'basic' || this.useClassicBasicForm) return;
            const activeEl = document.activeElement;
            if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA')) return;

            const items = event.clipboardData?.items;
            if (!items) return;
            for (const item of items) {
                if (item.type.startsWith('image/')) {
                    const file = item.getAsFile();
                    if (file) {
                        event.preventDefault();
                        if (!this.currentCard) {
                            await this.createNewCard();
                        }
                        await this.visualCanvas?.handleOcrImportFile(file);
                        break;
                    }
                }
            }
        });

        listen(this.container, 'change', async (event) => {
            const target = event.target;
            if (!(target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement)) {
                return;
            }

            if (target.id === 'tribe-search-input') {
                this.tribeSearchQuery = target.value.trim();
                await this.renderTribeManagementTab();
            }
        });

        // 搜索与筛选
        const searchInput = this.container.querySelector('#card-search');
        if (searchInput) {
            searchInput.addEventListener('input', () => this.applyCardListFilters());
        }

        ['#class-filter', '#type-filter', '#rarity-filter', '#set-filter'].forEach(selector => {
            const el = this.container.querySelector(selector);
            if (el) {
                el.addEventListener('change', () => this.applyCardListFilters());
            }
        });

    }

    setMobilePane(pane) {
        const targetPane = pane === 'edit' || pane === 'preview' ? pane : 'list';
        const root = this.container?.querySelector('.card-editor-layout');
        if (!root) return;
        root.dataset.mobilePane = targetPane;
        root.querySelectorAll('[data-mobile-pane-btn]').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.mobilePaneBtn === targetPane);
        });
        if (targetPane === 'edit' && typeof window !== 'undefined') {
            window.requestAnimationFrame(() => {
                window.dispatchEvent(new Event('resize'));
            });
        }
    }

    syncVisualCanvasLayout() {
        const isMseActive = this.editorMode === 'card'
            && this.cardTab === 'basic'
            && !this.useClassicBasicForm
            && Boolean(this.currentCard);

        this.container.querySelector('.editor-main')?.classList.toggle('mse-canvas-active', isMseActive);
        this.container.querySelector('.right-sidebar')?.classList.toggle('is-mse-settings', isMseActive);

        const rightTitle = this.container.querySelector('#right-sidebar-title');
        if (rightTitle) {
            rightTitle.textContent = isMseActive ? '卡面设置' : '预览';
        }

        const mseSettings = this.container.querySelector('#mse-right-settings');
        const classicPreview = this.container.querySelector('#classic-preview-pane');
        if (mseSettings) {
            mseSettings.hidden = !isMseActive;
        }
        if (classicPreview) {
            classicPreview.hidden = isMseActive;
        }
    }

    /**
     * 切换标签页
     */
    switchTab(tabName) {
        if (!tabName) {
            return;
        }

        if (this.cardTab === 'basic' && tabName !== 'basic' && !this.useClassicBasicForm) {
            this.visualCanvas?.finishInline?.();
        }

        this.cardTab = tabName;
        this.syncVisualCanvasLayout();

        // 移除所有活动状态
        this.container.querySelectorAll('.tab-button').forEach(btn => {
            btn.classList.remove('active');
        });
        this.container.querySelectorAll('.tab-panel').forEach(panel => {
            panel.classList.remove('active');
        });

        // 激活选中的标签页
        const tabButton = this.container.querySelector(`[data-tab="${tabName}"]`);
        const tabPanel = this.container.querySelector(`#${tabName}-tab`);

        if (tabButton && tabPanel) {
            tabButton.classList.add('active');
            tabPanel.classList.add('active');

            if (tabName === 'basic' && this.currentCard) {
                this.renderBasicInfoForm();
            }
            // 如果切换到能力管理标签页，渲染能力列表
            if (tabName === 'abilities' && this.currentCard) {
                this.renderAbilitiesList();
            }
            if (tabName === 'styles') void mountStyleEditor(this);
            if (tabName === 'assets') {
                this.renderAssetsTab();
            }
        }
    }

    async switchPlayerSettingsTab(tabName) {
        this.playerSettingsTab = tabName || 'leaderAbility';

        this.container.querySelectorAll('.settings-tab-button').forEach(btn => {
            const isActive = btn.dataset.settingsTab === this.playerSettingsTab;
            btn.classList.toggle('active', isActive);
            btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
        });

        this.container.querySelectorAll('.settings-tab-panel').forEach(panel => {
            panel.classList.toggle('active', panel.id === `${this.playerSettingsTab === 'leaderAbility' ? 'leader-ability-settings' : 'tribe-management'}-tab`);
        });

        if (this.playerSettingsTab === 'leaderAbility') {
            if (this.currentCard) {
                await this.renderAbilitiesList();
                this.updateCardPreview();
            }
            return;
        }

        await this.renderTribeManagementTab();
        this.renderTribeManagementPreview();
    }

    async ensureCardEditorMode() {
        if (this.editorMode === 'card') {
            return;
        }

        this.editorMode = 'card';
        await this.renderEditorModeContent();
    }

    syncEditorModeUi() {
        const settingsBtn = this.container?.querySelector('#player-settings-btn');
        if (settingsBtn) {
            settingsBtn.textContent = this.editorMode === 'playerSettings' ? '返回编辑' : '玩家设定';
            settingsBtn.classList.toggle('btn-secondary', this.editorMode === 'playerSettings');
            settingsBtn.classList.toggle('btn-primary', this.editorMode !== 'playerSettings');
        }

        const editorActions = this.container?.querySelector('.editor-actions');
        if (editorActions) {
            editorActions.hidden = this.editorMode === 'playerSettings';
        }
    }

    updateEditorHeader(titleText) {
        const editorTitle = this.container?.querySelector('#editor-title');
        if (editorTitle && typeof titleText === 'string') {
            editorTitle.textContent = titleText;
        }

        const metaEl = this.container?.querySelector('#editor-meta');
        if (!metaEl) {
            return;
        }

        if (this.editorMode !== 'card' || !this.currentCard || this.currentCard.isNew) {
            metaEl.textContent = '';
            metaEl.hidden = true;
            return;
        }

        const updatedAt = this.currentCard.updatedAt ?? this.currentCard.UpdatedAt ?? '';
        const updatedBy = String(this.currentCard.updatedBy ?? this.currentCard.UpdatedBy ?? '').trim();
        const formattedTime = formatEditorMetaTimestamp(updatedAt);

        if (!formattedTime && !updatedBy) {
            metaEl.textContent = '';
            metaEl.hidden = true;
            return;
        }

        const parts = [];
        if (formattedTime) {
            parts.push(formattedTime);
        }
        if (updatedBy) {
            parts.push(updatedBy);
        }

        metaEl.textContent = `最近修改：${parts.join(' · ')}`;
        metaEl.hidden = false;
    }

    applyCardEditMetadata(source) {
        if (!this.currentCard || !source) {
            return;
        }

        const updatedAt = source.cardUpdatedAt ?? source.updatedAt ?? source.UpdatedAt ?? null;
        const updatedBy = source.cardUpdatedBy ?? source.updatedBy ?? source.UpdatedBy ?? null;

        if (updatedAt !== null && updatedAt !== undefined) {
            this.currentCard.updatedAt = updatedAt;
        }
        if (updatedBy !== null && updatedBy !== undefined) {
            this.currentCard.updatedBy = updatedBy;
        }
    }

    updateEmptyPreviewState(message = '等待载入数据', detail = '暂无数据') {
        const previewContainer = this.container?.querySelector('#card-preview');
        if (previewContainer) {
            previewContainer.innerHTML = `
                <div class="preview-placeholder">
                    <p class="tech-text-dim">${escapeHtml(message)}</p>
                </div>
            `;
        }

        const previewDetails = this.container?.querySelector('#preview-details');
        if (previewDetails) {
            previewDetails.innerHTML = `<p>${escapeHtml(detail)}</p>`;
        }
    }

    renderTribeManagementPreview() {
        const definitions = Array.isArray(this.tribeDefinitions) ? this.tribeDefinitions : [];
        const enabledCount = definitions.filter(item => item.isEnabled).length;
        const reservedCount = definitions.filter(item => item.isReserved).length;
        const previewContainer = this.container?.querySelector('#card-preview');
        if (previewContainer) {
            previewContainer.innerHTML = `
                <div class="settings-preview-card tech-panel">
                    <h4 class="tech-header-small">种类注册表</h4>
                    <div class="settings-preview-metrics">
                        <div class="settings-preview-metric">
                            <span class="settings-preview-value">${definitions.length}</span>
                            <span class="settings-preview-label">已登记种类</span>
                        </div>
                        <div class="settings-preview-metric">
                            <span class="settings-preview-value">${enabledCount}</span>
                            <span class="settings-preview-label">启用中</span>
                        </div>
                        <div class="settings-preview-metric">
                            <span class="settings-preview-value">${reservedCount}</span>
                            <span class="settings-preview-label">保留项</span>
                        </div>
                    </div>
                    <p class="tech-text-dim">卡牌分配 tribe 仍在卡牌信息页完成，这里只维护定义、别名与特殊行为。</p>
                </div>
            `;
        }

        const previewDetails = this.container?.querySelector('#preview-details');
        if (previewDetails) {
            previewDetails.innerHTML = `
                <p>模式: 玩家设定</p>
                <p>子页: 种类管理</p>
                <p>筛选: ${escapeHtml(this.tribeSearchQuery || '全部')}</p>
            `;
        }
    }

    async enterPlayerSettingsMode() {
        const previousCardId = this.currentCard?.id ?? null;
        if (this.editorMode !== 'playerSettings' && previousCardId !== null && previousCardId !== LEADER_CARD_ID) {
            this.previousCardIdBeforeSettingsMode = previousCardId;
        }

        const editorRoot = this.container?.querySelector('.card-editor-layout') || this.container;
        const loader = editorRoot ? getBlockingLoader(editorRoot) : null;
        loader?.show('正在加载玩家设定...');

        try {
            this.currentCard = await this.loadLeaderCard();
            this.editorMode = 'playerSettings';
            this.updateEditorHeader('玩家设定');

            await this.renderEditorModeContent();
            this.setMobilePane('edit');
        } catch (error) {
            console.error('CardEditor: Failed to enter player settings mode:', error);
            showAlert(`加载玩家设定失败: ${error.message}`);
        } finally {
            loader?.hide();
        }
    }

    async exitPlayerSettingsMode() {
        const previousCardId = this.previousCardIdBeforeSettingsMode;
        this.previousCardIdBeforeSettingsMode = null;
        this.editorMode = 'card';
        this.currentCard = null;

        await this.renderEditorModeContent();

        if (previousCardId !== null && previousCardId !== undefined) {
            await this.selectCard(previousCardId);
            return;
        }

        this.disableEditorButtons();
        this.updateTabAvailability();
        this.switchTab('basic');

        this.updateEditorHeader('等待选择卡牌');

        this.updateEmptyPreviewState();
    }

    selectTribeDefinition(name) {
        this.activeTribeDefinitionName = name || null;
        this.isCreatingTribeDefinition = false;
        void this.renderTribeManagementTab();
    }

    startNewTribeDefinitionDraft() {
        this.activeTribeDefinitionName = null;
        this.isCreatingTribeDefinition = true;
        void this.renderTribeManagementTab();
    }

    normalizeTribeDefinition(raw) {
        return {
            name: (raw?.name ?? raw?.Name ?? '').trim(),
            aliases: Array.isArray(raw?.aliases ?? raw?.Aliases) ? [...(raw.aliases ?? raw.Aliases)] : [],
            behavior: (raw?.behavior ?? raw?.Behavior ?? 'Normal').toString().trim() || 'Normal',
            isReserved: Boolean(raw?.isReserved ?? raw?.IsReserved),
            isEnabled: raw?.isEnabled ?? raw?.IsEnabled ?? true,
            usageCount: Number(raw?.usageCount ?? raw?.UsageCount ?? 0) || 0
        };
    }

    async loadTribeDefinitions(options = {}) {
        if (this.isMirageScope()) {
            this.tribeDefinitions = [];
            return this.tribeDefinitions;
        }
        if (!options.force && Array.isArray(this.tribeDefinitions) && this.tribeDefinitions.length > 0) {
            return this.tribeDefinitions;
        }

        const response = await fetch(`/api/tribes${this.getScopeQueryParam()}`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        this.applyDataRevisionFromResponse(response);
        const data = await response.json();
        this.tribeDefinitions = Array.isArray(data)
            ? data.map(item => this.normalizeTribeDefinition(item))
            : [];

        if (!this.isCreatingTribeDefinition && !this.activeTribeDefinitionName && this.tribeDefinitions.length > 0) {
            this.activeTribeDefinitionName = this.tribeDefinitions[0].name;
        }

        return this.tribeDefinitions;
    }

    async populateTribeSuggestionOptions(datalist) {
        if (!datalist) {
            return;
        }

        try {
            const response = await fetch(`/api/tribes/suggest${this.getScopeQueryParam()}`);
            if (response.ok) {
                const data = await response.json();
                const suggestions = (Array.isArray(data) ? data : [])
                    .map(item => String(item ?? '').trim())
                    .filter(Boolean)
                    .sort((left, right) => left.localeCompare(right, 'zh-Hans-CN'));
                datalist.innerHTML = suggestions
                    .map(item => `<option value="${escapeHtml(item)}"></option>`)
                    .join('');
                return;
            }

            if (!Array.isArray(this.tribeDefinitions) || this.tribeDefinitions.length === 0) {
                await this.loadTribeDefinitions();
            }

            const suggestions = (Array.isArray(this.tribeDefinitions) ? this.tribeDefinitions : [])
                .filter(item => item.isEnabled)
                .map(item => item.name)
                .sort((left, right) => left.localeCompare(right, 'zh-Hans-CN'));

            datalist.innerHTML = suggestions
                .map(item => `<option value="${escapeHtml(item)}"></option>`)
                .join('');
        } catch (error) {
            console.warn('CardEditor: Failed to populate tribe suggestions:', error);
            datalist.innerHTML = '';
        }
    }

    parseAliasInput(rawValue) {
        return String(rawValue ?? '')
            .split(/[\n,]/)
            .map(value => value.trim())
            .filter(Boolean)
            .filter((value, index, items) => items.findIndex(existing => existing.toLowerCase() === value.toLowerCase()) === index);
    }

    getFilteredTribeDefinitions() {
        const query = (this.tribeSearchQuery || '').trim().toLowerCase();
        const source = Array.isArray(this.tribeDefinitions) ? this.tribeDefinitions : [];
        if (!query) {
            return source;
        }

        return source.filter(item => {
            if (item.name.toLowerCase().includes(query)) {
                return true;
            }

            return item.aliases.some(alias => alias.toLowerCase().includes(query));
        });
    }

    getActiveTribeDefinition(definitions = this.tribeDefinitions) {
        if (this.isCreatingTribeDefinition) {
            return null;
        }

        const source = Array.isArray(definitions) ? definitions : [];
        const selected = source.find(item => item.name === this.activeTribeDefinitionName);
        if (selected) {
            return selected;
        }

        return source[0] ?? null;
    }

    async renderTribeManagementTab() {
        const panel = this.container?.querySelector('#tribe-management-tab');
        if (!panel) {
            return;
        }

        try {
            await this.loadTribeDefinitions();
            const definitions = this.getFilteredTribeDefinitions();
            const activeDefinition = this.getActiveTribeDefinition(definitions) ?? this.getActiveTribeDefinition(this.tribeDefinitions);
            if (!this.isCreatingTribeDefinition && activeDefinition && !this.activeTribeDefinitionName) {
                this.activeTribeDefinitionName = activeDefinition.name;
            }

            const isEditingExisting = !this.isCreatingTribeDefinition && !!activeDefinition;
            const definition = isEditingExisting
                ? activeDefinition
                : {
                    name: '',
                    aliases: [],
                    behavior: 'Normal',
                    isReserved: false,
                    isEnabled: true,
                    usageCount: 0
                };
            const nameReadOnlyAttr = isEditingExisting ? 'readonly' : '';
            const behaviorDisabledAttr = definition.isReserved ? 'disabled' : '';
            const enabledDisabledAttr = definition.isReserved ? 'disabled' : '';
            const deleteDisabledAttr = !isEditingExisting || definition.isReserved || definition.usageCount > 0 ? 'disabled' : '';

            panel.innerHTML = `
                <div class="tribe-management-shell">
                    <div class="tribe-overview-grid">
                        <div class="settings-overview-card tech-section">
                            <span class="settings-overview-label">定义数量</span>
                            <strong class="settings-overview-value">${this.tribeDefinitions.length}</strong>
                        </div>
                        <div class="settings-overview-card tech-section">
                            <span class="settings-overview-label">启用中</span>
                            <strong class="settings-overview-value">${this.tribeDefinitions.filter(item => item.isEnabled).length}</strong>
                        </div>
                        <div class="settings-overview-card tech-section">
                            <span class="settings-overview-label">保留项</span>
                            <strong class="settings-overview-value">${this.tribeDefinitions.filter(item => item.isReserved).length}</strong>
                        </div>
                    </div>
                    <div class="tribe-management-layout">
                        <section class="tribe-management-catalog tech-section">
                            <div class="tribe-management-catalog-header">
                                <div>
                                    <h4 class="tech-header-small">种类目录</h4>
                                    <p class="tech-text-dim">在同一视野内筛选、浏览并切换要维护的规范值。</p>
                                </div>
                                <div class="tribe-management-toolbar">
                                    <button class="btn-tech btn-small" id="new-tribe-btn">新增种类</button>
                                    <button class="btn-tech btn-small" id="refresh-tribes-btn">刷新</button>
                                </div>
                            </div>
                            <div class="tribe-management-search-row">
                                <input
                                    type="search"
                                    id="tribe-search-input"
                                    class="search-input tech-input"
                                    placeholder="搜索名称或别名"
                                    value="${escapeHtml(this.tribeSearchQuery)}">
                            </div>
                            <div class="tribe-definition-list">
                                ${definitions.length > 0 ? definitions.map(item => `
                                    <button type="button" class="tribe-list-item${!this.isCreatingTribeDefinition && item.name === definition.name ? ' is-active' : ''}" data-tribe-name="${escapeHtml(item.name)}">
                                        <div class="tribe-list-item-main">
                                            <span class="tribe-list-item-name">${escapeHtml(item.name)}</span>
                                            <div class="tribe-list-item-badges">
                                                ${item.isReserved ? '<span class="tech-tag">保留</span>' : ''}
                                                ${item.behavior === 'MatchAllKnownTribes' ? '<span class="tech-tag">全部</span>' : ''}
                                                ${!item.isEnabled ? '<span class="tech-tag">停用</span>' : ''}
                                            </div>
                                        </div>
                                        <div class="tribe-list-item-meta">
                                            <span>${item.aliases.length} 个别名</span>
                                            <span>${item.usageCount} 张卡使用</span>
                                        </div>
                                    </button>
                                `).join('') : `
                                    <div class="form-placeholder">
                                        <p class="tech-text-dim">没有匹配的种类定义</p>
                                    </div>
                                `}
                            </div>
                        </section>
                        <section class="tribe-management-workspace tech-section">
                            <div class="tribe-management-workspace-header">
                                <div>
                                    <h4 class="tech-header-small">${isEditingExisting ? '编辑种类定义' : '新增种类定义'}</h4>
                                    <p class="tech-text-dim">名称用于卡牌与 DSL 的规范比较；别名用于兼容旧输入。</p>
                                </div>
                                <div class="tribe-management-workspace-meta">
                                    ${definition.isReserved ? '<span class="tech-tag">系统保留</span>' : ''}
                                    ${definition.usageCount > 0 ? `<span class="tech-tag">${definition.usageCount} 张卡使用</span>` : ''}
                                </div>
                            </div>
                            <div class="form-grid form-grid-2">
                                <div class="form-group">
                                    <label class="form-label required">名称</label>
                                    <input type="text" id="tribe-definition-name" class="form-input tech-input" value="${escapeHtml(definition.name)}" placeholder="请输入种类名称" ${nameReadOnlyAttr}>
                                </div>
                                <div class="form-group">
                                    <label class="form-label">行为</label>
                                    <select id="tribe-definition-behavior" class="form-input tech-select" ${behaviorDisabledAttr}>
                                        <option value="Normal" ${definition.behavior === 'Normal' ? 'selected' : ''}>普通种类</option>
                                        <option value="MatchAllKnownTribes" ${definition.behavior === 'MatchAllKnownTribes' ? 'selected' : ''}>拥有全部已知种类</option>
                                    </select>
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label">别名</label>
                                <textarea id="tribe-definition-aliases" class="form-textarea tech-input" placeholder="每行或逗号分隔一个别名">${escapeHtml(definition.aliases.join('\n'))}</textarea>
                            </div>
                            <div class="form-group">
                                <label class="form-label">启用状态</label>
                                <label class="form-toggle">
                                    <input type="checkbox" id="tribe-definition-enabled" ${definition.isEnabled ? 'checked' : ''} ${enabledDisabledAttr}>
                                    <span class="form-toggle-text">${definition.isEnabled ? '启用' : '停用'}</span>
                                </label>
                            </div>
                            <div class="tribe-management-actions">
                                <button class="btn-tech btn-primary" id="save-tribe-definition-btn">${isEditingExisting ? '保存定义' : '创建定义'}</button>
                                <button class="btn-tech btn-danger" id="delete-tribe-definition-btn" ${deleteDisabledAttr}>删除定义</button>
                            </div>
                        </section>
                    </div>
                </div>
            `;
        } catch (error) {
            console.error('CardEditor: Failed to render tribe management tab:', error);
            panel.innerHTML = `
                <div class="error-container">
                    <div class="error-icon">⚠️</div>
                    <h3 class="error-title">种类定义加载失败</h3>
                    <p class="error-message">${escapeHtml(error.message)}</p>
                </div>
            `;
        }
    }

    async saveTribeDefinitionFromForm() {
        if (this.isMirageScope()) {
            showAlert('幻境卡牌工坊不支持编辑经典全局种类定义，请直接在卡牌信息中填写种类名称。');
            return;
        }
        try {
            const nameInput = this.container?.querySelector('#tribe-definition-name');
            const aliasesInput = this.container?.querySelector('#tribe-definition-aliases');
            const behaviorSelect = this.container?.querySelector('#tribe-definition-behavior');
            const enabledInput = this.container?.querySelector('#tribe-definition-enabled');

            if (!nameInput || !aliasesInput || !behaviorSelect || !enabledInput) {
                return;
            }

            const request = {
                name: nameInput.value.trim(),
                aliases: this.parseAliasInput(aliasesInput.value),
                behavior: behaviorSelect.value,
                isEnabled: enabledInput.checked
            };

            if (!request.name) {
                showAlert('请输入种类名称。');
                return;
            }

            const isCreate = this.isCreatingTribeDefinition;
            const url = isCreate
                ? `/api/tribes${this.getScopeQueryParam()}`
                : `/api/tribes/${encodeURIComponent(this.activeTribeDefinitionName)}${this.getScopeQueryParam()}`;
            const method = isCreate ? 'POST' : 'PUT';
            const body = isCreate
                ? request
                : {
                    aliases: request.aliases,
                    behavior: request.behavior,
                    isEnabled: request.isEnabled
                };

            const response = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });

            if (!response.ok) {
                const errorMessage = await response.text();
                throw new Error(errorMessage || `HTTP ${response.status}: ${response.statusText}`);
            }

            this.applyDataRevisionFromResponse(response);
            const saved = this.normalizeTribeDefinition(await response.json());
            this.isCreatingTribeDefinition = false;
            this.activeTribeDefinitionName = saved.name;
            this.tribeDefinitions = [];
            await this.loadTribeDefinitions({ force: true });
            await this.renderTribeManagementTab();
            this.renderTribeManagementPreview();
            showAlert(isCreate ? '种类定义已创建。' : '种类定义已保存。');
        } catch (error) {
            console.error('CardEditor: Failed to save tribe definition:', error);
            showAlert(`保存种类定义失败: ${error.message}`);
        }
    }

    async deleteActiveTribeDefinition() {
        if (this.isMirageScope()) {
            showAlert('幻境卡牌工坊不支持删除经典全局种类定义。');
            return;
        }
        try {
            const active = this.getActiveTribeDefinition(this.tribeDefinitions);
            if (!active) {
                return;
            }

            if (!await confirmDialog(`确定要删除种类定义 "${active.name}" 吗？`)) {
                return;
            }

            const response = await fetch(`/api/tribes/${encodeURIComponent(active.name)}${this.getScopeQueryParam()}`, {
                method: 'DELETE'
            });

            if (!response.ok && response.status !== 204) {
                const errorMessage = await response.text();
                throw new Error(errorMessage || `HTTP ${response.status}: ${response.statusText}`);
            }

            this.applyDataRevisionFromResponse(response);
            this.activeTribeDefinitionName = null;
            this.tribeDefinitions = [];
            await this.loadTribeDefinitions({ force: true });
            await this.renderTribeManagementTab();
            this.renderTribeManagementPreview();
            showAlert('种类定义已删除。');
        } catch (error) {
            console.error('CardEditor: Failed to delete tribe definition:', error);
            showAlert(`删除种类定义失败: ${error.message}`);
        }
    }

    /**
     * 创建新卡牌
     */
    async createNewCard() {
        if (this.editorMode !== 'card') {
            this.currentCard = null;
        }
        await this.ensureCardEditorMode();
        this.previousCardIdBeforeSettingsMode = null;

        const isMirage = this.isMirageScope();
        this.currentCard = {
            id: null,
            name: '',
            description: '',
            cost: isMirage ? 1 : 0,
            mirageTavernTier: isMirage ? 1 : null,
            mirageBuyCost: isMirage ? 3 : null,
            attack: isMirage ? 2 : 0,
            health: isMirage ? 2 : 1,
            countdown: 0,
            cardType: 'Follower',
            cardClass: 'Neutral',
            rarity: 'Bronze',
            cardSet: isMirage ? MIRAGE_CARD_SET_NAME : '特殊',
            keywords: [],
            tribe: [],
            canEvolve: false,
            evolveId: null,
            isCollectible: true,
            isImplemented: false,
            presentation: null,
            isMirageCard: isMirage,
            artPreview: null,
            artDetail: null,
            isNew: true
        };

        this.cardTab = 'basic';
        this.updateTabAvailability();

        this.updateEditorHeader('新建卡牌');

        this.enableEditorButtons();
        this.setMobilePane('edit');
        await this.renderBasicInfoForm();
        this.updateCardPreview();

        const activeTab = this.container.querySelector('.tab-button.active')?.dataset.tab;
        if (activeTab === 'assets') {
            this.renderAssetsTab();
        }

        const abilitiesListContainer = this.container.querySelector('#abilities-list');
        if (abilitiesListContainer) {
            abilitiesListContainer.innerHTML = `
                <div class="abilities-placeholder">
                    <p>请先保存卡牌后再配置能力</p>
                </div>
            `;
        }
    }

    /**
     * 清理资源
     */
    cleanup() {
        this.visualCanvas?.dispose?.();
        this.visualCanvas = null;
        this.eventController?.abort();
        this.eventController = null;
        this.isInitialized = false;
        this.currentCard = null;
        if (this.cardCatalogInvalidationHandler) {
            document.removeEventListener(CARD_CATALOG_INVALIDATED_EVENT, this.cardCatalogInvalidationHandler);
            this.cardCatalogInvalidationHandler = null;
        }
        if (this.cardSetsUpdatedHandler) {
            document.removeEventListener(CARD_SETS_UPDATED_EVENT, this.cardSetsUpdatedHandler);
            this.cardSetsUpdatedHandler = null;
        }

        if (this.container) {
            this.container.innerHTML = '';
        }
    }
}

// 全局卡牌编辑器实例
let cardEditorInstance = null;
const cardEditorInstances = new Map();

/**
 * 初始化卡牌编辑器
 */
export async function initializeCardEditor(options = {}) {
    const containerId = options.containerId || 'card-editor-container';
    const layoutMode = options.layoutMode || 'default';
    const cardPoolScope = normalizeCardPoolScope(options.cardPoolScope || (containerId.includes('mirage') ? 'mirage' : 'standard'));

    setActiveAbilityDataScope(cardPoolScope);

    let instance = cardEditorInstances.get(containerId);
    if (!instance) {
        instance = new CardEditor({ containerId, layoutMode, cardPoolScope });
        cardEditorInstances.set(containerId, instance);
    } else {
        instance.layoutMode = layoutMode;
        instance.cardPoolScope = cardPoolScope;
    }

    cardEditorInstance = instance;
    const wasInitialized = instance.isInitialized;
    instance.useDefaultCardSet = !instance.isMirageScope();
    if (!instance.initializationTask) instance.initializationTask = instance.initialize().finally(() => { instance.initializationTask = null; });
    await instance.initializationTask;
    if (wasInitialized && instance.filteredCardsData) {
        instance.populateCardSetFilterOptions(instance.filteredCardsData);
        instance.applyCardListFilters();
    }
    return instance;
}

/**
 * 获取卡牌编辑器实例
 */
export function getCardEditor(containerId = 'card-editor-container') {
    return cardEditorInstances.get(containerId) || cardEditorInstance;
}

/**
 * 清理卡牌编辑器
 */
export function cleanupCardEditor(containerId = 'card-editor-container') {
    const instance = cardEditorInstances.get(containerId);
    if (instance) {
        instance.cleanup();
        cardEditorInstances.delete(containerId);
        if (cardEditorInstance === instance) {
            cardEditorInstance = null;
        }
    }
}

export { CardEditor };

// 扩展CardEditor类的原型，添加新方法
CardEditor.prototype.getCardIdKey = function(value) {
    if (value === undefined || value === null) return null;
    return String(value);
};

CardEditor.prototype.getResolvedCardId = function(card) {
    return getCardDefinitionId(card);
};

CardEditor.prototype.getCurrentCardId = function() {
    return this.getResolvedCardId(this.currentCard);
};

CardEditor.prototype.getAbilityIdKey = function(value) {
    if (value === undefined || value === null) return null;
    return String(value);
};

CardEditor.prototype.applyDataRevisionFromResponse = function(response, detail = {}) {
    if (!response || !response.headers) return false;
    const revision = response.headers.get('X-Data-Revision');
    if (!revision) return false;
    const hasIncrementalCardPatch = (Array.isArray(detail.changedCards) && detail.changedCards.length > 0)
        || (Array.isArray(detail.deletedCardIds) && detail.deletedCardIds.length > 0);
    const abilityOnly = isAbilityOnlyInvalidation(detail);
    let changed = false;
    if (this.dataRevision && this.dataRevision !== revision) {
        this.dataRevision = revision;
        if (!hasIncrementalCardPatch && !abilityOnly) {
            this.clearDataCaches();
            this.queueCardListReload();
        }
        changed = true;
    } else if (!this.dataRevision) {
        this.dataRevision = revision;
        changed = true;
    }

    syncDataRevisionFromResponse(response, {
        source: 'card-editor',
        containerId: this.containerId,
        scope: this.cardPoolScope,
        ...detail
    });
    return changed;
};

CardEditor.prototype.bindGlobalInvalidationListener = function() {
    if (this.cardCatalogInvalidationHandler) {
        return;
    }

    this.cardCatalogInvalidationHandler = (event) => {
        const detail = event?.detail || {};
        if (detail.source === 'card-editor' && detail.containerId === this.containerId) {
            return;
        }
        if (detail.scope && normalizeCardPoolScope(detail.scope) !== this.cardPoolScope) {
            return;
        }
        if (isAbilityOnlyInvalidation(detail)) {
            return;
        }

        this.clearDataCaches();
        this.queueCardListReload();
    };

    document.addEventListener(CARD_CATALOG_INVALIDATED_EVENT, this.cardCatalogInvalidationHandler);
    if (!this.isMirageScope()) {
        this.cardSetsUpdatedHandler = async () => {
            await this.loadCardSetDefinitions();
            if (!this.isInitialized) return;
            if (!this.useClassicBasicForm) await this.visualCanvas?.refreshCardSets();
        };
        document.addEventListener(CARD_SETS_UPDATED_EVENT, this.cardSetsUpdatedHandler);
    }
};

CardEditor.prototype.queueCardListReload = function() {
    if (this._revisionReloadPending) {
        return;
    }
    if (!this.container) {
        return;
    }
    const listEl = this.container.querySelector('#card-list');
    if (!listEl) {
        return;
    }
    this._revisionReloadPending = true;
    setTimeout(() => {
        this.loadCardList({ force: true })
            .catch(error => console.warn('CardEditor: Failed to reload card list after revision change.', error))
            .finally(() => {
                this._revisionReloadPending = false;
            });
    }, 0);
};

CardEditor.prototype.clearDataCaches = function() {
    this.cardListCache = null;
    this.cardDetailCache.clear();
    this.abilitiesByCardCache.clear();
    this.abilityDetailCache.clear();
    this.abilityCountsCache.clear();
    this.abilitiesLibraryCache = null;
    this.allCardsData = [];
    this.filteredCardsData = [];
};

CardEditor.prototype.refreshAbilityDataCacheAfterSave = async function() {
    try {
        await refreshAbilityDataCache(this.cardPoolScope);
    } catch (error) {
        console.warn('CardEditor: Failed to refresh ability data cache after save.', error);
    }
};

CardEditor.prototype.readAbilityWriteResponse = async function(response) {
    if (response.ok) {
        return await response.json();
    }

    const failure = await response.json();
    if (failure.code === 'ABILITY_CACHE_REFRESH_FAILED') {
        const change = failure.operation === 'unlink' ? `能力 ${failure.id} 的移除操作` : `能力 ${failure.id}`;
        const repeatWarning = failure.operation === 'create' ? '请勿重复新增' : '请勿重复提交';
        const savedMessage = `${change}已保存，但运行时缓存刷新失败，尚未生效。${repeatWarning}，请先排查服务端错误并刷新游戏数据缓存。`;
        // 数据已提交，撤销本地旧缓存并关闭新增表单，避免把缓存故障当作未创建。
        this.invalidateAbilityLibraryCache();
        this.invalidateAbilitiesForCard(failure.cardId);
        this.invalidateAbilityDetailCache(failure.id);
        markAbilityDataCacheDirty(this.cardPoolScope);
        if (this.getCardIdKey(this.getCurrentCardId()) === this.getCardIdKey(failure.cardId)) {
            this.applyCardEditMetadata(failure);
            if (failure.operation === 'create') {
                this.closeAddAbilityModal();
            }
            try {
                await this.renderAbilitiesList({ force: true });
            } catch (error) {
                throw new Error(`${savedMessage} 重新读取能力列表也失败：${error.message}`, { cause: error });
            }
        }
        throw new Error(savedMessage);
    }

    const errMsg = failure?.error || failure?.message || response.statusText;
    throw new Error(`HTTP ${response.status}: ${errMsg}`);
};

CardEditor.prototype.updateAbilityCachesAfterSave = function(ability, cardId) {
    if (!ability) {
        markAbilityDataCacheDirty(this.cardPoolScope);
        return;
    }

    const abilityKey = this.getAbilityIdKey(ability?.id ?? ability?.Id);
    if (abilityKey) {
        this.abilityDetailCache.set(abilityKey, ability);
    }

    const resolvedCardId = cardId ?? ability?.cardId ?? ability?.CardId;
    const cardKey = this.getCardIdKey(resolvedCardId);
    if (cardKey && this.abilitiesByCardCache.has(cardKey)) {
        const abilityId = ability?.id ?? ability?.Id;
        const abilityIdKey = this.getAbilityIdKey(abilityId);
        const cachedList = this.abilitiesByCardCache.get(cardKey) || [];
        const nextList = cachedList.filter(existing => {
            const existingKey = this.getAbilityIdKey(existing?.id ?? existing?.Id);
            return existingKey !== abilityIdKey;
        });
        nextList.push(ability);
        this.abilitiesByCardCache.set(cardKey, nextList);
    }

    upsertAbilityDataCacheEntry(ability, this.cardPoolScope);
};

CardEditor.prototype.removeAbilityFromCaches = function(abilityId, cardId) {
    this.invalidateAbilityDetailCache(abilityId);
    this.invalidateAbilitiesForCard(cardId);
    removeAbilityDataCacheEntry(abilityId, this.cardPoolScope);
};

CardEditor.prototype.invalidateCardListCache = function() {
    this.cardListCache = null;
    this.allCardsData = [];
    this.filteredCardsData = [];
};

CardEditor.prototype.invalidateCardDetailCache = function(cardId) {
    const key = this.getCardIdKey(cardId);
    if (!key) return;
    this.cardDetailCache.delete(key);
};

CardEditor.prototype.invalidateAbilitiesForCard = function(cardId) {
    const key = this.getCardIdKey(cardId);
    if (!key) return;
    this.abilitiesByCardCache.delete(key);
    this.abilityCountsCache.delete(key);
};

CardEditor.prototype.invalidateAbilityDetailCache = function(abilityId) {
    const key = this.getAbilityIdKey(abilityId);
    if (!key) return;
    this.abilityDetailCache.delete(key);
};

CardEditor.prototype.invalidateAbilityLibraryCache = function() {
    this.abilitiesLibraryCache = null;
};

CardEditor.prototype.upsertCardInCache = function(apiCard) {
    const key = this.getCardIdKey(this.getResolvedCardId(apiCard));
    if (!key) return;
    // Both caches are read as API card definitions, including when the card is reopened.
    this.cardDetailCache.set(key, apiCard);
    if (!Array.isArray(this.cardListCache)) return;
    const list = [...this.cardListCache];
    const index = list.findIndex(item => this.getCardIdKey(this.getResolvedCardId(item)) === key);
    if (index >= 0) {
        list[index] = apiCard;
    } else {
        list.push(apiCard);
    }
    this.cardListCache = list;
    this.allCardsData = list;
};

CardEditor.prototype.removeCardFromCache = function(cardId) {
    const key = this.getCardIdKey(cardId);
    if (!key) return;
    if (Array.isArray(this.cardListCache)) {
        this.cardListCache = this.cardListCache.filter(item => this.getCardIdKey(item?.id ?? item?.CardId) !== key);
    }
    if (Array.isArray(this.cardListCache)) {
        this.allCardsData = this.cardListCache;
    } else {
        this.allCardsData = [];
    }
    this.cardDetailCache.delete(key);
    this.abilitiesByCardCache.delete(key);
    this.abilityCountsCache.delete(key);
};

CardEditor.prototype.enableEditorButtons = function() {
    const saveBtn = this.container.querySelector('#save-btn');
    const saveAsBtn = this.container.querySelector('#save-as-btn');
    const deleteBtn = this.container.querySelector('#delete-btn');

    if (this.editorMode !== 'card') {
        this.disableEditorButtons();
        return;
    }

    if (saveBtn) saveBtn.disabled = false;
    if (saveAsBtn) saveAsBtn.disabled = false;
    if (deleteBtn) deleteBtn.disabled = this.currentCard?.isNew;
};

CardEditor.prototype.disableEditorButtons = function() {
    const saveBtn = this.container.querySelector('#save-btn');
    const saveAsBtn = this.container.querySelector('#save-as-btn');
    const deleteBtn = this.container.querySelector('#delete-btn');

    if (saveBtn) saveBtn.disabled = true;
    if (saveAsBtn) saveAsBtn.disabled = true;
    if (deleteBtn) deleteBtn.disabled = true;
};

CardEditor.prototype.generateSelectOptions = function(options, selectedValue) {
    if (!options || !Array.isArray(options)) {
        return '<option value="">加载中...</option>';
    }

    return options.map(option => {
        const value = option.value || option;
        const label = option.label || option;
        const selected = equalsIgnoreCase(value, selectedValue) ? 'selected' : '';
        return `<option value="${value}" ${selected}>${label}</option>`;
    }).join('');
};

CardEditor.prototype.renderOptionButtonGroup = function(options, selectedValue) {
    if (!options || !Array.isArray(options) || options.length === 0) {
        return '<div class="option-group-empty">LOADING...</div>';
    }

    return options.map(option => {
        const value = option.value || option;
        const label = option.label || option;
        const isSelected = equalsIgnoreCase(value, selectedValue);
        return `
            <button type="button" class="option-chip${isSelected ? ' is-selected' : ''}" data-option-value="${value}">
                ${label}
            </button>
        `;
    }).join('');
};

CardEditor.prototype.renderBasicInfoFormCompact = function({
    cardTypeOptions,
    cardClassOptions,
    cardRarityOptions,
    readOnlyAttr,
    disabledAttr
}) {
    const previewPath = this.currentCard.artPreview || '';
    const previewUrl = previewPath ? this.getArtAssetUrl(previewPath) : '';
    const isEditable = !this.currentCard.isNew;

    return `
        <div class="form-container tech-form basic-compact">
            <div class="basic-compact-main">
                <div class="basic-compact-fields">
                    <div class="basic-compact-row">
                        <label class="form-label required">名称</label>
                        <input type="text" id="card-name" class="form-input tech-input" value="${this.currentCard.name}" placeholder="请输入名称" ${readOnlyAttr}>
                    </div>
                    <div class="basic-compact-row basic-compact-stats">
                        <label class="form-label required">属性</label>
                        <div class="basic-compact-stats-grid">
                            <div class="form-group">
                                <label class="form-label required">${this.isMirageScope() ? '费用 / 星级' : '费用'}</label>
                                <div class="number-stepper tech-stepper">
                                    <button type="button" class="stepper-btn decrease" data-target="card-cost" ${disabledAttr}>-</button>
                                    <input type="number" id="card-cost" class="form-input number-input tech-input" value="${this.currentCard.cost}" min="0" max="99" ${readOnlyAttr}>
                                    <button type="button" class="stepper-btn increase" data-target="card-cost" ${disabledAttr}>+</button>
                                </div>
                            </div>
                            <div id="stats-container" class="basic-compact-stats">
                                <div class="form-group">
                                    <label class="form-label">攻击</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-attack">-</button>
                                        <input type="number" id="card-attack" class="form-input number-input tech-input" value="${this.currentCard.attack}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-attack">+</button>
                                    </div>
                                </div>
                                <div class="form-group">
                                    <label class="form-label">生命</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-health">-</button>
                                        <input type="number" id="card-health" class="form-input number-input tech-input" value="${this.currentCard.health}" min="1" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-health">+</button>
                                    </div>
                                </div>
                            </div>
                            <div id="countdown-container" class="basic-compact-countdown">
                                ${this.isMirageScope() ? `
                                <div class="form-group">
                                    <label class="form-label">购买金币</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-mirageBuyCost">-</button>
                                        <input type="number" id="card-mirageBuyCost" class="form-input number-input tech-input" value="${this.currentCard.mirageBuyCost ?? 3}" min="0" max="10">
                                        <button type="button" class="stepper-btn increase" data-target="card-mirageBuyCost">+</button>
                                    </div>
                                    <div class="form-help">酒馆招募该棋子消耗的金币数（默认 3G）。</div>
                                </div>
                                ` : `
                                <div class="form-group">
                                    <label class="form-label">倒计时</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-countdown">-</button>
                                        <input type="number" id="card-countdown" class="form-input number-input tech-input" value="${this.currentCard.countdown ?? 0}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-countdown">+</button>
                                    </div>
                                    <div class="form-help">设置倒计时需要添加倒数关键字。</div>
                                </div>
                                `}
                            </div>
                        </div>
                    </div>
                    <div class="basic-compact-row">
                        <label class="form-label required">类型</label>
                        <div class="option-group" data-option-group="type">
                            ${this.renderOptionButtonGroup(cardTypeOptions, this.currentCard.cardType)}
                        </div>
                    </div>
                    <div class="basic-compact-row">
                        <label class="form-label required">稀有度</label>
                        <div class="option-group" data-option-group="rarity">
                            ${this.renderOptionButtonGroup(cardRarityOptions, this.currentCard.rarity)}
                        </div>
                    </div>
                    <div class="basic-compact-row">
                        <label class="form-label required">职业</label>
                        <div class="option-group" data-option-group="class">
                            ${this.renderOptionButtonGroup(cardClassOptions, this.currentCard.cardClass)}
                        </div>
                    </div>
                </div>
                <div class="basic-compact-art">
                    <div class="basic-art-header">
                        <div class="tech-header-small">预览图</div>
                        <span class="asset-hint">用于编辑器预览</span>
                    </div>
                    <div class="basic-art-preview${previewUrl ? ' has-image' : ''}">
                        ${previewUrl
                            ? `<img src="${previewUrl}" alt="Preview art">`
                            : `<div class="asset-placeholder">暂无预览图</div>`}
                    </div>
                    <div class="basic-art-actions">
                        <button type="button" class="btn-tech btn-small" data-upload-kind="preview" ${isEditable ? '' : 'disabled'}>上传</button>
                    </div>
                    <div class="basic-art-path">${previewPath || '暂无文件'}</div>
                </div>
            </div>
            <div class="basic-compact-extra">
                <div class="form-section tech-section">
                    <h4 class="form-section-title tech-header-small">描述</h4>
                    <div class="form-group">
                        <label class="form-label">描述</label>
                        <textarea id="card-description" class="form-textarea tech-input" placeholder="请输入描述" ${readOnlyAttr}>${this.currentCard.description}</textarea>
                    </div>
                </div>

                <div class="form-section tech-section">
                    <h4 class="form-section-title tech-header-small">标签与元数据</h4>
                    <div class="form-grid form-grid-2">
                        <div class="form-group">
                            <label class="form-label">关键字</label>
                            <div class="tag-input-container tech-tag-container">
                                <div class="tag-list" id="keywords-list">
                                    ${this.renderTagList(this.currentCard.keywords || [], 'keywords')}
                                </div>
                                <div class="tag-input-wrapper">
                                    <select id="keywords-select" class="tag-select tech-select">
                                        <option value="">选择关键字</option>
                                    </select>
                                    <button type="button" class="tag-add-btn btn-tech-small" data-target="keywords">+</button>
                                </div>
                            </div>
                        </div>
                        <div class="form-group">
                            <label class="form-label">种族</label>
                            <div class="tag-input-container tech-tag-container">
                                <div class="tag-list" id="tribe-list">
                                    ${this.renderTagList(this.currentCard.tribe || [], 'tribe')}
                                </div>
                                <div class="tag-input-wrapper">
                                    <input type="text" id="tribe-input" class="tag-input tech-input" placeholder="请输入种族" list="tribe-suggestions">
                                    <datalist id="tribe-suggestions"></datalist>
                                    <button type="button" class="tag-add-btn btn-tech-small" data-target="tribe">+</button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="form-section tech-section">
                    <h4 class="form-section-title tech-header-small">分类</h4>
                    <div class="form-grid form-grid-2">
                        <div class="form-group">
                            <label class="form-label">卡包</label>
                            <input type="text" id="card-set" class="form-input tech-input" value="${this.currentCard.cardSet || ''}" placeholder="请输入卡包" list="card-set-options" ${readOnlyAttr}>
                            <datalist id="card-set-options"></datalist>
                        </div>
                        <div class="form-group">
                            <label class="form-label">可收集</label>
                            <label class="form-toggle">
                                <input type="checkbox" id="card-collectible" ${this.currentCard.isCollectible ? 'checked' : ''}>
                                <span class="form-toggle-text" id="card-collectible-label">${this.currentCard.isCollectible ? '是' : '否'}</span>
                            </label>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
};

CardEditor.prototype.populateCardListFilterOptions = async function() {
    const classFilter = this.container.querySelector('#class-filter');
    const typeFilter = this.container.querySelector('#type-filter');
    const rarityFilter = this.container.querySelector('#rarity-filter');

    if (!classFilter || !typeFilter || !rarityFilter) {
        return;
    }

    const [classOptions, typeOptions, rarityOptions] = await Promise.all([
        this.metadataManager.getCardClassOptions(),
        this.metadataManager.getCardTypeOptions({ includeLeader: this.isMirageScope() }),
        this.metadataManager.getCardRarityOptions()
    ]);

    classFilter.innerHTML = '<option value="">全部职业</option>' + this.generateSelectOptions(classOptions, '');
    typeFilter.innerHTML = '<option value="">全部类型</option>' + this.generateSelectOptions(typeOptions, '');
    rarityFilter.innerHTML = '<option value="">全部稀有度</option>' + this.generateSelectOptions(rarityOptions, '');
};

CardEditor.prototype.populateCardSetFilterOptions = function(cards) {
    const setFilter = this.container.querySelector('#set-filter');
    if (!setFilter) {
        return;
    }

    const source = Array.isArray(cards) ? cards : [];
    const groupedOptions = buildGroupedCardSetOptions(source, {
        getCardSetInfo,
        normalizeEnvironment: normalizeCardSetEnvironmentValue,
        getEnvironmentLabel: getCardSetEnvironmentLabel,
        knownEnvironmentOptions: getDeckEnvironmentOptions()
    });
    const currentValue = this.useDefaultCardSet ? '星云基础' : (setFilter.value || '');
    this.useDefaultCardSet = false;
    const availableValues = new Set(
        groupedOptions.flatMap(group => group.options.map(option => option.value.toLowerCase()))
    );
    const nextValue = availableValues.has(currentValue.toLowerCase()) ? currentValue : '';

    setFilter.disabled = groupedOptions.length === 0;
    setFilter.innerHTML = this.renderGroupedCardSetFilterOptions(groupedOptions, nextValue);
};

CardEditor.prototype.renderGroupedCardSetFilterOptions = function(groups, selectedValue) {
    let hasSelected = false;
    const groupHtml = (Array.isArray(groups) ? groups : [])
        .map(group => {
            const optionsHtml = (Array.isArray(group.options) ? group.options : [])
                .map(option => {
                    const shouldSelect = !hasSelected && equalsIgnoreCase(option.value, selectedValue);
                    if (shouldSelect) {
                        hasSelected = true;
                    }

                    return `<option value="${escapeHtml(option.value)}"${shouldSelect ? ' selected' : ''}>${escapeHtml(option.label)}</option>`;
                })
                .join('');

            if (!optionsHtml) {
                return '';
            }

            return `<optgroup label="${escapeHtml(group.label)}">${optionsHtml}</optgroup>`;
        })
        .join('');

    return '<option value="">全部卡包</option>' + groupHtml;
};

CardEditor.prototype.loadCardSetDefinitions = async function() {
    try {
        const response = await fetch(`/api/cardsets${this.getScopeQueryParam()}`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }
        const payload = await response.json();
        this.cardSetDefinitions = Array.isArray(payload) ? payload : [];
    } catch (error) {
        console.warn('CardEditor: Failed to load card set definitions.', error);
        this.cardSetDefinitions = [];
    }

    this.refreshCardSetDatalist();
};

CardEditor.prototype.refreshCardSetDatalist = function() {
    const datalist = this.container?.querySelector('#card-set-options');
    if (!datalist) {
        return;
    }

    const values = new Set();
    (this.cardSetDefinitions || []).forEach(set => {
        const name = set?.name ?? set?.Name ?? '';
        if (name) values.add(String(name).trim());
    });

    const options = Array.from(values)
        .filter(Boolean)
        .sort((a, b) => compareCardSetDisplayOrder(
            (this.cardSetDefinitions || []).find(set => String(set?.name ?? set?.Name ?? '').trim() === a) ?? { name: a },
            (this.cardSetDefinitions || []).find(set => String(set?.name ?? set?.Name ?? '').trim() === b) ?? { name: b }
        ))
        .map(value => `<option value="${escapeHtml(value)}"></option>`)
        .join('');

    datalist.innerHTML = options;
};

CardEditor.prototype.applyCardListFilters = function() {
    const searchInput = this.container.querySelector('#card-search');
    const classFilter = this.container.querySelector('#class-filter');
    const typeFilter = this.container.querySelector('#type-filter');
    const rarityFilter = this.container.querySelector('#rarity-filter');
    const setFilter = this.container.querySelector('#set-filter');

    const searchText = (searchInput?.value || '').trim().toLowerCase();
    const selectedClass = classFilter?.value || '';
    const selectedType = typeFilter?.value || '';
    const selectedRarity = rarityFilter?.value || '';
    const selectedSet = setFilter?.value || '';

    const baseCards = Array.isArray(this.filteredCardsData)
        ? this.filteredCardsData
        : [];

    const filtered = baseCards.filter(card => {
        const name = ((card.name ?? card.Name) ?? '').toString().toLowerCase();
        const cardClass = normalizeEnumName((card.class ?? card.Class) ?? '');
        const cardType = normalizeEnumName((card.type ?? card.Type) ?? '');
        const rarity = normalizeEnumName((card.rarity ?? card.Rarity) ?? '');
        const setInfo = getCardSetInfo(card);
        const setMatches = !selectedSet || equalsIgnoreCase(setInfo.name, selectedSet);

        if (searchText && !name.includes(searchText)) return false;
        if (selectedClass && !equalsIgnoreCase(cardClass, selectedClass)) return false;
        if (selectedType && !equalsIgnoreCase(cardType, selectedType)) return false;
        if (selectedRarity && !equalsIgnoreCase(rarity, selectedRarity)) return false;
        if (!setMatches) return false;

        return true;
    });

    const sorted = filtered.slice().sort(compareCardsForEditorAndDeck);
    this.renderCardList(sorted);
};

CardEditor.prototype.updateCardPreview = function() {
    const previewContainer = this.container.querySelector('#card-preview');
    if (!previewContainer || !this.currentCard) {
        return;
    }

    installCardHistoryActions(this);
    const card = this.currentCard;
    const isFollower = equalsIgnoreCase(normalizeEnumName(card.cardType), 'Follower');
    const rawRarity = card.rarity || 'Common';
    const rawClass = card.cardClass || 'Neutral';
    const rawType = card.cardType || 'Spell';
    const rarityKey = normalizeEnumName(rawRarity) || 'Common';
    const classKey = normalizeEnumName(rawClass) || 'Neutral';
    const typeKey = normalizeEnumName(rawType) || 'Spell';
    const rarity = rarityKey.toLowerCase();
    const artPreview = card.artPreview || '';
    const artPreviewUrl = artPreview ? this.getArtAssetUrl(artPreview) : '';
    const detailPath = card.artDetail || '';
    const detailUrl = detailPath ? this.getArtAssetUrl(detailPath) : '';
    const countdownValue = (card.countdown ?? card.Countdown) ?? 0;
    // 用户需求：若为0则代表永久，无需显示
    const showCountdown = !isFollower && countdownValue > 0;
    const keywordList = Array.isArray(card.keywords)
        ? card.keywords
        : (Array.isArray(card.Keywords) ? card.Keywords : []);
    const hasEarthsigilKeyword = keywordList.some(keyword => equalsIgnoreCase(keyword, 'EarthSigil'));
    const earthsigilValue = hasEarthsigilKeyword ? 1 : 0;
    const showEarthsigil = !isFollower && hasEarthsigilKeyword;
    const showEvidence = equalsIgnoreCase(typeKey, 'Amulet')
        && keywordList.some(keyword => equalsIgnoreCase(keyword, 'Evidence'));

    const tribeList = Array.isArray(card.tribe) ? card.tribe : [];
    const tribeText = tribeList
        .map(item => (item ?? '').toString().trim())
        .filter(Boolean)
        .join('·');

    ensureEditorEnumLocalization(this);
    previewContainer.innerHTML = buildPreviewSlateHtml({
        rarityClass: rarity,
        rarityLabel: getCachedEnumLabel('CardRarity', rawRarity, rarityKey),
        classLabel: getCachedEnumLabel('CardClass', rawClass, classKey),
        typeLabel: getCachedEnumLabel('CardType', rawType, typeKey),
        name: card.name || 'UNTITLED',
        costValue: card.cost,
        isFollower,
        isLeader: equalsIgnoreCase(typeKey, 'Leader'),
        isMirageCard: Boolean(this.isMirageScope() || card.isMirageCard),
        mirageBuyCost: (this.isMirageScope() || card.isMirageCard) && !equalsIgnoreCase(typeKey, 'Leader')
            ? (card.mirageBuyCost ?? 3)
            : null,
        attackValue: card.attack,
        healthValue: card.health,
        countdownValue,
        showCountdown,
        earthsigilValue,
        showEarthsigil,
        evidenceValue: 1,
        showEvidence,
        description: card.description || '',
        previewUrl: artPreviewUrl,
        detailUrl,
        enableDetail: true,
        tribeText
    });
    previewContainer._detailPayload = {
        card,
        previewUrl: artPreviewUrl,
        detailUrl
    };
    bindPreviewDetailHandler(previewContainer);
    void hydrateDescriptionReferences(previewContainer, {
        originalCardMode: true,
        openCardDetail: referencedCard => showCardDetailModal(referencedCard, { cards: this.allCardsData }),
        cards: this.allCardsData,
        fallbackPreviewUrl: artPreviewUrl,
        currentCardId: card?.id ?? card?.Id ?? card?.CardId ?? null,
        currentCardName: card?.name ?? card?.Name ?? ''
    });

    // 更新 DEBUG INFO 区域
    const debugContainer = this.container.querySelector('#preview-details');
    if (debugContainer) {
        debugContainer.innerHTML = `
            <div class="debug-grid">
                <div class="debug-row">
                    <span class="debug-label">ID</span>
                    <span class="debug-value">${card.id || '无'}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">内部名称</span>
                    <span class="debug-value">${card.name || '无'}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">关键字</span>
                    <span class="debug-value">[${(card.keywords || []).join(', ')}]</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">种族</span>
                    <span class="debug-value">[${(card.tribe || []).join(', ')}]</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">进化目标 ID</span>
                    <span class="debug-value">${card.evolveId || '无'}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">新建卡牌</span>
                    <span class="debug-value">${card.isNew ? '是' : '否'}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">可收集</span>
                    <span class="debug-value">${card.isCollectible ? '是' : '否'}</span>
                </div>
            </div>
        `;
    }
};

CardEditor.prototype.renderAssetsTab = function() {
    const assetsContainer = this.container.querySelector('#assets-container');
    if (!assetsContainer) {
        return;
    }

    if (!this.currentCard) {
        assetsContainer.innerHTML = `
            <div class="form-placeholder">
                <p class="tech-text-dim">请选择一张卡牌进行编辑</p>
            </div>
        `;
        return;
    }

    const previewPath = this.currentCard.artPreview || '';
    const detailPath = this.currentCard.artDetail || '';
    const previewUrl = previewPath ? this.getArtAssetUrl(previewPath) : '';
    const detailUrl = detailPath ? this.getArtAssetUrl(detailPath) : '';
    const isEditable = !this.currentCard.isNew;
    const showPreview = this.layoutMode !== 'test';
    const gridClass = showPreview ? 'assets-grid' : 'assets-grid assets-grid-single';

    assetsContainer.innerHTML = `
        <div class="form-container tech-form">
            <div class="form-section tech-section">
                <h4 class="form-section-title tech-header-small">美术资源</h4>
                <div class="form-grid form-grid-2 ${gridClass}">
                    ${showPreview ? `
                        <div class="asset-panel">
                            <div class="asset-header">
                                <h4 class="tech-header-small">预览图</h4>
                                <span class="asset-hint">用于编辑器预览</span>
                            </div>
                            <div class="asset-preview${previewUrl ? ' has-image' : ''}">
                                ${previewUrl
                                    ? `<img src="${previewUrl}" alt="Preview art">`
                                    : `<div class="asset-placeholder">暂无预览图</div>`}
                            </div>
                            <div class="asset-actions">
                                <button type="button" class="btn-tech btn-small" data-upload-kind="preview" ${isEditable ? '' : 'disabled'}>上传</button>
                            </div>
                            <div class="asset-path">${previewPath || '暂无文件'}</div>
                        </div>
                    ` : ''}

                    <div class="asset-panel">
                        <div class="asset-header">
                            <h4 class="tech-header-small">详情图</h4>
                            <span class="asset-hint">用于卡牌详情展示</span>
                        </div>
                        <div class="asset-preview${detailUrl ? ' has-image' : ''}">
                            ${detailUrl
                                ? `<img src="${detailUrl}" alt="Detail art">`
                                : `<div class="asset-placeholder">暂无详情图</div>`}
                        </div>
                        <div class="asset-actions">
                            <button type="button" class="btn-tech btn-small" data-upload-kind="detail" ${isEditable ? '' : 'disabled'}>上传</button>
                        </div>
                        <div class="asset-path">${detailPath || '暂无文件'}</div>
                    </div>
                </div>
            </div>
            ${isEditable ? '' : '<div class="asset-note">请先保存卡牌，再上传图片</div>'}
        </div>
    `;

    if (isEditable) {
        this.bindAssetsTabEvents({ includePreview: showPreview });
    }
};

CardEditor.prototype.bindAssetsTabEvents = function(options = {}) {
    const includePreview = options.includePreview !== false;
    const previewButton = includePreview ? this.container.querySelector('[data-upload-kind="preview"]') : null;
    const detailButton = this.container.querySelector('[data-upload-kind="detail"]');

    if (previewButton) {
        previewButton.addEventListener('click', async () => {
            this.openArtCropModal('preview');
        });
    }

    if (detailButton) {
        detailButton.addEventListener('click', async () => {
            this.openArtCropModal('detail');
        });
    }
};

CardEditor.prototype.openArtCropModal = function(kind, options = {}) {
    if (document.querySelector('.card-art-crop-modal')) return;

    const isPreview = kind === 'preview';
    const outputWidth = 848;
    const outputHeight = 1024;
    const aspectRatio = isPreview ? outputWidth / outputHeight : null;
    const title = isPreview ? '预览图裁切' : '详情图裁切';
    const statusLabel = isPreview ? '上传预览图' : '上传详情图';

    const modal = document.createElement('div');
    modal.className = 'card-art-crop-modal';
    modal.innerHTML = `
        <div class="card-art-crop-card">
            <div class="card-art-crop-header">
                <div class="card-art-crop-title">${title}</div>
                <button type="button" class="card-art-crop-close">×</button>
            </div>
            <div class="card-art-crop-stage">
                <img class="card-art-crop-image" alt="Card art preview">
                <div class="card-art-crop-box">
                    <span class="card-art-crop-handle"></span>
                </div>
                <div class="card-art-crop-placeholder">
                    <div class="card-art-crop-placeholder-title">上传图片</div>
                    <div class="card-art-crop-placeholder-text">点击选择一张图片</div>
                </div>
            </div>
            <input class="card-art-crop-file" type="file" accept="image/*">
            <div class="card-art-crop-actions">
                <button type="button" class="btn-tech btn-small card-art-crop-cancel">取消</button>
                <button type="button" class="btn-tech btn-small btn-primary card-art-crop-upload">上传</button>
            </div>
            <div class="card-art-crop-status" aria-live="polite"></div>
        </div>
    `;

    document.body.appendChild(modal);

    const closeBtn = modal.querySelector('.card-art-crop-close');
    const cancelBtn = modal.querySelector('.card-art-crop-cancel');
    const uploadBtn = modal.querySelector('.card-art-crop-upload');
    const input = modal.querySelector('.card-art-crop-file');
    const status = modal.querySelector('.card-art-crop-status');
    const stage = modal.querySelector('.card-art-crop-stage');
    const imageEl = modal.querySelector('.card-art-crop-image');
    const cropBox = modal.querySelector('.card-art-crop-box');
    const resizeHandle = modal.querySelector('.card-art-crop-handle');

    const state = {
        objectUrl: null,
        imageRect: null,
        naturalWidth: 0,
        naturalHeight: 0,
        crop: { x: 0, y: 0, width: 0, height: 0 },
        dragging: false,
        resizing: false,
        startX: 0,
        startY: 0,
        startCropX: 0,
        startCropY: 0,
        startWidth: 0,
        startHeight: 0
    };

    const setModalStatus = (text, isError = false) => {
        if (!status) return;
        status.textContent = text || '';
        status.style.color = isError ? 'rgba(255, 120, 120, 0.9)' : 'var(--text-dim)';
    };

    const cleanup = () => {
        window.removeEventListener('resize', handleResize);
        if (state.objectUrl) {
            URL.revokeObjectURL(state.objectUrl);
        }
        modal.remove();
    };

    const closeModal = () => {
        cleanup();
    };

    const updateImageRect = () => {
        if (!stage || !imageEl) return;
        const stageRect = stage.getBoundingClientRect();
        const imgRect = imageEl.getBoundingClientRect();
        state.imageRect = {
            x: imgRect.left - stageRect.left,
            y: imgRect.top - stageRect.top,
            width: imgRect.width,
            height: imgRect.height
        };
    };

    const clampCrop = () => {
        if (!state.imageRect) return;
        const maxWidth = state.imageRect.width;
        const maxHeight = state.imageRect.height;
        if (aspectRatio) {
            const maxAllowedWidth = Math.min(maxWidth, maxHeight * aspectRatio);
            const minAllowedWidth = Math.min(120, maxAllowedWidth);
            const width = Math.min(Math.max(state.crop.width, minAllowedWidth), maxAllowedWidth);
            state.crop.width = width;
            state.crop.height = width / aspectRatio;
        } else {
            const minWidth = Math.min(120, maxWidth);
            const minHeight = Math.min(120, maxHeight);
            state.crop.width = Math.min(Math.max(state.crop.width, minWidth), maxWidth);
            state.crop.height = Math.min(Math.max(state.crop.height, minHeight), maxHeight);
        }

        const maxX = state.imageRect.x + state.imageRect.width - state.crop.width;
        const maxY = state.imageRect.y + state.imageRect.height - state.crop.height;
        state.crop.x = Math.min(Math.max(state.crop.x, state.imageRect.x), maxX);
        state.crop.y = Math.min(Math.max(state.crop.y, state.imageRect.y), maxY);
    };

    const renderCrop = () => {
        if (!cropBox) return;
        cropBox.style.left = `${state.crop.x}px`;
        cropBox.style.top = `${state.crop.y}px`;
        cropBox.style.width = `${state.crop.width}px`;
        cropBox.style.height = `${state.crop.height}px`;
    };

    const initCropBox = () => {
        if (!state.imageRect) return;
        if (aspectRatio) {
            let width = state.imageRect.width;
            let height = width / aspectRatio;
            if (height > state.imageRect.height) {
                height = state.imageRect.height;
                width = height * aspectRatio;
            }
            state.crop.width = width;
            state.crop.height = height;
        } else {
            state.crop.width = state.imageRect.width;
            state.crop.height = state.imageRect.height;
        }
        state.crop.x = state.imageRect.x + (state.imageRect.width - state.crop.width) / 2;
        state.crop.y = state.imageRect.y + (state.imageRect.height - state.crop.height) / 2;
        clampCrop();
        renderCrop();
    };

    const openFilePicker = () => {
        if (input) {
            input.value = '';
            input.click();
        }
    };

    const handleResize = () => {
        if (!state.imageRect || !stage?.classList.contains('has-image')) return;
        updateImageRect();
        clampCrop();
        renderCrop();
    };

    closeBtn?.addEventListener('click', closeModal);
    cancelBtn?.addEventListener('click', closeModal);
    modal.addEventListener('click', (event) => {
        if (event.target === modal) {
            closeModal();
        }
    });
    stage?.addEventListener('click', () => {
        if (!stage.classList.contains('has-image')) {
            openFilePicker();
        }
    });

    input?.addEventListener('change', () => {
        const file = input.files?.[0];
        if (!file) return;
        if (!file.type.startsWith('image/')) {
            setModalStatus('不支持的图片类型。', true);
            return;
        }
        stage?.classList.remove('has-image');
        if (cropBox) {
            cropBox.style.display = 'none';
        }
        if (state.objectUrl) {
            URL.revokeObjectURL(state.objectUrl);
        }
        const url = URL.createObjectURL(file);
        state.objectUrl = url;
        if (!imageEl) return;
        imageEl.onload = () => {
            state.naturalWidth = imageEl.naturalWidth;
            state.naturalHeight = imageEl.naturalHeight;
            requestAnimationFrame(() => {
                updateImageRect();
                initCropBox();
                setModalStatus('');
                stage?.classList.add('has-image');
            });
        };
        imageEl.onerror = () => {
            setModalStatus('图片加载失败。', true);
        };
        imageEl.src = url;
    });

    cropBox?.addEventListener('pointerdown', (event) => {
        if (event.target === resizeHandle) return;
        event.preventDefault();
        state.dragging = true;
        state.startX = event.clientX;
        state.startY = event.clientY;
        state.startCropX = state.crop.x;
        state.startCropY = state.crop.y;
        cropBox.setPointerCapture(event.pointerId);
        cropBox.classList.add('dragging');
    });

    resizeHandle?.addEventListener('pointerdown', (event) => {
        event.preventDefault();
        event.stopPropagation();
        state.resizing = true;
        state.startX = event.clientX;
        state.startY = event.clientY;
        state.startWidth = state.crop.width;
        state.startHeight = state.crop.height;
        resizeHandle.setPointerCapture(event.pointerId);
        cropBox?.classList.add('resizing');
    });

    const onPointerMove = (event) => {
        if (!state.imageRect) return;
        if (state.dragging) {
            const dx = event.clientX - state.startX;
            const dy = event.clientY - state.startY;
            state.crop.x = state.startCropX + dx;
            state.crop.y = state.startCropY + dy;
            clampCrop();
            renderCrop();
        } else if (state.resizing) {
            const dx = event.clientX - state.startX;
            const dy = event.clientY - state.startY;
            if (aspectRatio) {
                const delta = Math.abs(dx) > Math.abs(dy) ? dx : dy;
                state.crop.width = state.startWidth + delta;
                state.crop.height = state.crop.width / aspectRatio;
            } else {
                state.crop.width = state.startWidth + dx;
                state.crop.height = state.startHeight + dy;
            }
            clampCrop();
            renderCrop();
        }
    };

    const stopPointer = (event) => {
        if (state.dragging && cropBox) {
            state.dragging = false;
            cropBox.releasePointerCapture(event.pointerId);
            cropBox.classList.remove('dragging');
        }
        if (state.resizing && resizeHandle) {
            state.resizing = false;
            resizeHandle.releasePointerCapture(event.pointerId);
            cropBox?.classList.remove('resizing');
        }
    };

    stage?.addEventListener('pointermove', onPointerMove);
    stage?.addEventListener('pointerup', stopPointer);
    stage?.addEventListener('pointerleave', stopPointer);
    stage?.addEventListener('pointercancel', stopPointer);
    window.addEventListener('resize', handleResize);

    uploadBtn?.addEventListener('click', async () => {
        if (!imageEl?.src || !state.imageRect || state.crop.width <= 0 || state.crop.height <= 0) {
            setModalStatus('请先选择一张图片。', true);
            return;
        }
        setModalStatus(`${statusLabel}中...`);
        uploadBtn.disabled = true;
        try {
            const scaleX = state.naturalWidth / state.imageRect.width;
            const scaleY = state.naturalHeight / state.imageRect.height;
            const rawX = (state.crop.x - state.imageRect.x) * scaleX;
            const rawY = (state.crop.y - state.imageRect.y) * scaleY;
            const rawWidth = state.crop.width * scaleX;
            const rawHeight = state.crop.height * scaleY;
            const sourceX = Math.max(0, Math.min(state.naturalWidth - 1, rawX));
            const sourceY = Math.max(0, Math.min(state.naturalHeight - 1, rawY));
            const maxSourceWidth = state.naturalWidth - sourceX;
            const maxSourceHeight = state.naturalHeight - sourceY;
            const sourceWidth = Math.max(1, Math.min(rawWidth, maxSourceWidth));
            const sourceHeight = Math.max(1, Math.min(rawHeight, maxSourceHeight));

            const canvas = document.createElement('canvas');
            if (isPreview) {
                canvas.width = outputWidth;
                canvas.height = outputHeight;
            } else {
                canvas.width = Math.max(1, Math.round(sourceWidth));
                canvas.height = Math.max(1, Math.round(sourceHeight));
            }

            const ctx = canvas.getContext('2d');
            if (!ctx) {
                throw new Error('生成图片失败。');
            }

            ctx.drawImage(
                imageEl,
                sourceX,
                sourceY,
                sourceWidth,
                sourceHeight,
                0,
                0,
                canvas.width,
                canvas.height
            );

            const blob = await new Promise((resolve) => {
                canvas.toBlob(resolve, 'image/webp', 0.9);
            });

            if (!blob) {
                throw new Error('导出图片失败。');
            }

            const filename = isPreview ? 'preview.webp' : 'detail.webp';
            if(typeof options.onUpload === 'function')await options.onUpload(blob,filename);
            else await this.uploadCardArt(kind, blob, filename, { silent: true });
            setModalStatus('上传完成。');
            setTimeout(closeModal, 400);
        } catch (error) {
            setModalStatus(`上传失败：${error.message}`, true);
        } finally {
            uploadBtn.disabled = false;
        }
    });

    if (cropBox) {
        cropBox.style.display = 'none';
    }
    if (imageEl) {
        imageEl.addEventListener('load', () => {
            if (cropBox) cropBox.style.display = 'block';
        }, { once: true });
    }
};

CardEditor.prototype.uploadCardArt = async function(kind, file, filename, options = {}) {
    const targetCardId = options.cardId ?? this.getCurrentCardId();
    if (targetCardId == null || (!options.cardId && !this.currentCard)) {
        if (!options.silent) {
            showAlert('请先保存卡牌，再上传图片。');
        }
        throw new Error('必须先保存卡牌，才能上传图片。');
    }

    try {
        const formData = new FormData();
        if (filename) {
            formData.append('file', file, filename);
        } else {
            formData.append('file', file);
        }

        const endpoint = kind === 'preview'
            ? `/api/cards/${targetCardId}/art/preview${this.getScopeQueryParam()}`
            : `/api/cards/${targetCardId}/art/detail${this.getScopeQueryParam()}`;

        const response = await fetch(endpoint, {
            method: 'POST',
            headers: buildEditorIdentityHeaders(),
            body: formData
        });

        if (!response.ok) {
            const message = await readErrorResponseMessage(response);
            throw new Error(`HTTP ${response.status}: ${message}`);
        }

        const updated = await response.json();
        this.applyDataRevisionFromResponse(response, {
            reason: 'card-art-updated',
            changedCardIds: [updated?.id ?? updated?.CardId ?? updated?.Id ?? targetCardId],
            changedCards: [updated]
        });
        const updatedId = updated?.id ?? updated?.CardId ?? updated?.Id;
        if (updatedId != null) {
            const mergeCard = (list) => {
                if (!Array.isArray(list)) return;
                const index = list.findIndex(card => {
                    const cardId = card?.id ?? card?.CardId ?? card?.Id;
                    return cardId === updatedId;
                });
                if (index >= 0) {
                    list[index] = { ...list[index], ...updated };
                }
            };
            mergeCard(this.allCardsData);
            mergeCard(this.filteredCardsData);
        }
        this.artCacheBuster = Date.now();
        const converted = this.convertApiCardToEditorFormat(updated);
        if (converted.id != null) {
            this.cardDetailCache.set(this.getCardIdKey(converted.id), updated);
        }

        const isStillSameCard = Boolean(
            this.currentCard
            && this.getCurrentCardId() != null
            && this.getCardIdKey(this.getCurrentCardId()) === this.getCardIdKey(converted.id ?? targetCardId)
        );
        if (isStillSameCard) {
            this.currentCard.artPreview = converted.artPreview;
            this.currentCard.artDetail = converted.artDetail;
            this.currentCard.updatedAt = converted.updatedAt;
            this.currentCard.updatedBy = converted.updatedBy;
            this.updateEditorHeader(`编辑卡牌: ${this.currentCard.name}`);
            this.renderAssetsTab();
            // 静默上传由保存流程统一重挂载，避免在裁切状态重置前重新读取预览图。
            if (this.layoutMode === 'test' && !options.silent) {
                await this.renderBasicInfoForm();
            }
            this.updateCardPreview();
        }
        return { ...updated, previewArtUrl: this.getArtAssetUrl(converted.artPreview) };
    } catch (error) {
        console.error('CardEditor: Failed to upload art:', error);
        if (!options.silent) {
            showAlert(`上传失败：${error.message}`);
        }
        throw error;
    }
};

CardEditor.prototype.getArtAssetUrl = function(path) {
    if (!path) return '';
    const cacheToken = this.artCacheBuster;
    if (!cacheToken) return encodeURI(path);
    const separator = path.includes('?') ? '&' : '?';
    return encodeURI(`${path}${separator}t=${cacheToken}`);
};

CardEditor.prototype.renderBasicInfoForm = async function() {
    this.syncVisualCanvasLayout();
    const basicTab = this.container.querySelector('#basic-tab');
    if (!basicTab) {
        console.error('CardEditor: Basic tab not found');
        return;
    }

    try {
        await this.ensureKeywordLabelMap();

        if (!this.useClassicBasicForm) {
            const settingsHost = this.container.querySelector('#mse-right-settings');
            this.visualCanvas ??= new VisualCardCanvas(this);
            await this.visualCanvas.mount(basicTab, {
                settingsHost,
                keywordOptions: this.keywordLabelMap || {}
            });
            this.validateForm();
            return;
        }

        // 获取卡牌类型、职业、稀有度等枚举选项
        const [cardTypeOptions, cardClassOptions, cardRarityOptions] = await Promise.all([
            this.metadataManager.getCardTypeOptions({ includeLeader: this.isMirageScope() }),
            this.metadataManager.getCardClassOptions(),
            this.metadataManager.getCardRarityOptions()
        ]);

        // 进化形态现在也可以编辑，移除只读限制
        const readOnlyAttr = '';
        const disabledAttr = '';

        const formHtml = this.layoutMode === 'test'
            ? this.renderBasicInfoFormCompact({
                cardTypeOptions,
                cardClassOptions,
                cardRarityOptions,
                readOnlyAttr,
                disabledAttr
            })
            : `
                <div class="form-container tech-form">
                    <div class="visual-card-toolbar tech-section" style="justify-content: flex-end;">
                        <button type="button" class="btn-tech btn-small btn-primary" id="switch-visual-canvas-btn">
                            切换至卡面编辑
                        </button>
                    </div>
                    <div class="form-section tech-section">
                        <h4 class="form-section-title tech-header-small">基础属性</h4>
                        <div class="form-grid form-grid-2">
                            <div class="form-group">
                                <label class="form-label required">名称</label>
                                <input type="text" id="card-name" class="form-input tech-input" value="${this.currentCard.name}" placeholder="请输入名称" ${readOnlyAttr}>
                            </div>
                            <div class="form-group">
                                <label class="form-label required">${this.isMirageScope() ? '费用 / 星级' : '费用'}</label>
                                <div class="number-stepper tech-stepper">
                                    <button type="button" class="stepper-btn decrease" data-target="card-cost" ${disabledAttr}>-</button>
                                    <input type="number" id="card-cost" class="form-input number-input tech-input" value="${this.currentCard.cost}" min="0" max="99" ${readOnlyAttr}>
                                    <button type="button" class="stepper-btn increase" data-target="card-cost" ${disabledAttr}>+</button>
                                </div>
                            </div>
                        </div>
                        
                        <div id="stats-container">
                            <div class="form-grid form-grid-2" style="margin-bottom: var(--space-md);">
                                <div class="form-group">
                                    <label class="form-label">攻击</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-attack">-</button>
                                        <input type="number" id="card-attack" class="form-input number-input tech-input" value="${this.currentCard.attack}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-attack">+</button>
                                    </div>
                                </div>
                                <div class="form-group">
                                    <label class="form-label">生命</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-health">-</button>
                                        <input type="number" id="card-health" class="form-input number-input tech-input" value="${this.currentCard.health}" min="1" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-health">+</button>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div id="countdown-container">
                            <div class="form-grid form-grid-2" style="margin-bottom: var(--space-md);">
                                ${this.isMirageScope() ? `
                                <div class="form-group">
                                    <label class="form-label">购买金币</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-mirageBuyCost">-</button>
                                        <input type="number" id="card-mirageBuyCost" class="form-input number-input tech-input" value="${this.currentCard.mirageBuyCost ?? 3}" min="0" max="10">
                                        <button type="button" class="stepper-btn increase" data-target="card-mirageBuyCost">+</button>
                                    </div>
                                    <div class="form-help">酒馆招募该棋子消耗的金币数（默认 3G）。</div>
                                </div>
                                ` : `
                                <div class="form-group">
                                    <label class="form-label">倒计时</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-countdown">-</button>
                                        <input type="number" id="card-countdown" class="form-input number-input tech-input" value="${this.currentCard.countdown ?? 0}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-countdown">+</button>
                                    </div>
                                    <div class="form-help">设置倒计时需要添加倒数关键字。</div>
                                </div>
                                `}
                            </div>
                        </div>

                        <div class="form-group">
                            <label class="form-label">描述</label>
                            <textarea id="card-description" class="form-textarea tech-input" placeholder="请输入描述" ${readOnlyAttr}>${this.currentCard.description}</textarea>
                        </div>
                    </div>

                    <div class="form-section tech-section">
                        <h4 class="form-section-title tech-header-small">标签与元数据</h4>
                        <div class="form-grid form-grid-2">
                            <div class="form-group">
                                <label class="form-label">关键字</label>
                                <div class="tag-input-container tech-tag-container">
                                    <div class="tag-list" id="keywords-list">
                                        ${this.renderTagList(this.currentCard.keywords || [], 'keywords')}
                                    </div>
                                    <div class="tag-input-wrapper">
                                        <select id="keywords-select" class="tag-select tech-select">
                                            <option value="">选择关键字</option>
                                        </select>
                                        <button type="button" class="tag-add-btn btn-tech-small" data-target="keywords">+</button>
                                    </div>
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label">种族</label>
                                <div class="tag-input-container tech-tag-container">
                                    <div class="tag-list" id="tribe-list">
                                        ${this.renderTagList(this.currentCard.tribe || [], 'tribe')}
                                    </div>
                                    <div class="tag-input-wrapper">
                                        <input type="text" id="tribe-input" class="tag-input tech-input" placeholder="请输入种族" list="tribe-suggestions">
                                        <datalist id="tribe-suggestions"></datalist>
                                        <button type="button" class="tag-add-btn btn-tech-small" data-target="tribe">+</button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="form-section tech-section">
                        <h4 class="form-section-title tech-header-small">分类</h4>
                        <div class="form-grid form-grid-3">
                            <div class="form-group">
                                <label class="form-label required">类型</label>
                                <div class="option-group" data-option-group="type">
                                    ${this.renderOptionButtonGroup(cardTypeOptions, this.currentCard.cardType)}
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label required">职业</label>
                                <div class="option-group" data-option-group="class">
                                    ${this.renderOptionButtonGroup(cardClassOptions, this.currentCard.cardClass)}
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label required">稀有度</label>
                                <div class="option-group" data-option-group="rarity">
                                    ${this.renderOptionButtonGroup(cardRarityOptions, this.currentCard.rarity)}
                                </div>
                            </div>
                            <div class="form-group">
                            <label class="form-label">卡包</label>
                                <input type="text" id="card-set" class="form-input tech-input" value="${this.currentCard.cardSet || ''}" placeholder="请输入卡包" list="card-set-options" ${readOnlyAttr}>
                                <datalist id="card-set-options"></datalist>
                            </div>
                            <div class="form-group">
                                <label class="form-label">可收集</label>
                                <label class="form-toggle">
                                    <input type="checkbox" id="card-collectible" ${this.currentCard.isCollectible ? 'checked' : ''}>
                                    <span class="form-toggle-text" id="card-collectible-label">${this.currentCard.isCollectible ? '是' : '否'}</span>
                                </label>
                            </div>
                        </div>
                    </div>

                </div>
            `;

        // 创建表单HTML
        basicTab.innerHTML = formHtml;
        this.refreshCardSetDatalist();

        // 绑定表单事件
        this.bindBasicFormEvents();
        this.bindTagInputEvents();
        this.bindFormValidationEvents();

        // 根据卡牌类型显示/隐藏战斗属性
        this.updateStatsVisibility();
        this.updateCardPreview();

    } catch (error) {
        console.error('CardEditor: Failed to render basic info form:', error);
        basicTab.innerHTML = `
            <div class="error-container">
                <div class="error-icon">⚠️</div>
                <h3 class="error-title">表单加载失败</h3>
                <p class="error-message">${error.message}</p>
            </div>
        `;
    }
};

CardEditor.prototype.renderCardList = function(cards) {
    const cardListContainer = this.container.querySelector('#card-list');
    if (!cardListContainer || !cards) return;

    if (!Array.isArray(cards) || cards.length === 0) {
        cardListContainer.innerHTML = `
            <div class="card-list-empty">
                <p>暂无卡牌数据</p>
                <small>点击"新建卡牌"开始创建</small>
            </div>
        `;
        return;
    }

    // 生成卡牌列表HTML
    const cardListHTML = cards.map(card => {
        const rawType = (card.type ?? card.Type) ?? 'UNKNOWN';
        const rawClass = (card.class ?? card.Class) ?? 'NEUTRAL';
        const rawRarity = (card.rarity ?? card.Rarity) ?? 'COMMON';
        const normalizedType = normalizeEnumName(rawType);
        const normalizedClass = normalizeEnumName(rawClass);
        const normalizedRarity = normalizeEnumName(rawRarity);

        return `
            <div class="card-list-item tech-list-item" data-card-id="${card.id !== undefined ? card.id : card.CardId}">
                <div class="card-item-header">
                    <span class="card-item-name tech-text-bold">${card.name || card.Name || '未命名'}</span>
                    <div class="card-item-indicators">
                        ${(card.isImplemented ?? card.IsImplemented) === false ? '<span class="card-stage-badge is-unimplemented">待实装</span>' : ''}
                        <span class="card-item-cost tech-badge">${(card.cost ?? card.Cost) ?? 0}</span>
                    </div>
                </div>
                <div class="card-item-details">
                    <span class="card-item-type tech-tag">${getCachedEnumLabel('CardType', rawType, normalizedType)}</span>
                    <span class="card-item-class tech-tag">${getCachedEnumLabel('CardClass', rawClass, normalizedClass)}</span>
                    <span class="card-item-rarity tech-tag rarity-${normalizedRarity.toLowerCase()}">${getCachedEnumLabel('CardRarity', rawRarity, normalizedRarity)}</span>
                </div>
                ${(equalsIgnoreCase(normalizedType, 'Follower')) ? `
                    <div class="card-item-stats">
                        <span class="stat-attack">攻击力 ${(card.attack ?? card.Attack) ?? 0}</span>
                        <span class="stat-health">生命值 ${(card.health ?? card.Health) ?? 1}</span>
                    </div>
                ` : (equalsIgnoreCase(normalizedType, 'Leader')) ? `
                    <div class="card-item-stats">
                        <span class="stat-health">生命值 ${(card.health ?? card.Health) ?? 20}</span>
                    </div>
                ` : ''}
                <div class="tech-corner-decoration"></div>
            </div>
        `;
    }).join('');

    cardListContainer.innerHTML = cardListHTML;

    if (this.shouldRestoreCardListScroll) {
        const maxScrollTop = Math.max(0, cardListContainer.scrollHeight - cardListContainer.clientHeight);
        cardListContainer.scrollTop = Math.min(this.cardListScrollTop, maxScrollTop);
        this.shouldRestoreCardListScroll = false;
    }

    // 绑定点击事件
    this.bindCardListEvents();
};

CardEditor.prototype.captureCardListScroll = function() {
    const cardListContainer = this.container.querySelector('#card-list');
    if (!cardListContainer) return;
    this.cardListScrollTop = cardListContainer.scrollTop;
    this.shouldRestoreCardListScroll = true;
};

CardEditor.prototype.highlightCardInList = function(cardId) {
    this.container.querySelectorAll('.card-list-item').forEach(item => {
        item.classList.remove('selected');
    });

    const selectedItem = this.container.querySelector(`[data-card-id="${cardId}"]`);
    if (selectedItem) {
        selectedItem.classList.add('selected');
    }
};

CardEditor.prototype.bindCardListEvents = function() {
    const cardItems = this.container.querySelectorAll('.card-list-item');
    cardItems.forEach(item => {
        item.addEventListener('click', () => {
            const cardId = item.dataset.cardId;
            this.selectCard(cardId);
        });
    });
};

CardEditor.prototype.bindBasicFormEvents = function() {
    const basicTab = this.container.querySelector('#basic-tab');
    if (!basicTab) return;

    // 文本输入事件
    const nameInput = basicTab.querySelector('#card-name');
    const descInput = basicTab.querySelector('#card-description');

    if (nameInput) {
        nameInput.addEventListener('input', (e) => {
            this.currentCard.name = e.target.value;
            this.updateCardPreview();
        });
    }

    if (descInput) {
        descInput.addEventListener('input', (e) => {
            this.currentCard.description = e.target.value;
            if (this.currentCard.presentation) {
                this.currentCard.presentation.sections = sectionsFromDescription(e.target.value, {
                    cardType: this.currentCard.cardType,
                    cardName: this.currentCard.name,
                    previewArtUrl: this.currentCard.artPreview ? this.getArtAssetUrl(this.currentCard.artPreview) : '',
                    resolveReferenceCard: refName => findCardByReference(this.allCardsData, refName),
                    resolveArtUrl: path => path ? this.getArtAssetUrl(path) : ''
                });
            }
            this.updateCardPreview();
        });
    }

    const setInput = basicTab.querySelector('#card-set');
    if (setInput) {
        setInput.addEventListener('input', (e) => {
            this.updateCardProperty('set', e.target.value);
        });
    }

    // 数字步进器事件
    const stepperButtons = basicTab.querySelectorAll('.stepper-btn');
    stepperButtons.forEach(btn => {
        btn.addEventListener('click', (e) => {
            const target = e.target.dataset.target;
            const input = basicTab.querySelector(`#${target}`);
            if (!input) return;

            const isIncrease = e.target.classList.contains('increase');
            const currentValue = parseInt(input.value) || 0;
            const min = parseInt(input.min) || 0;
            const max = parseInt(input.max) || 99;

            let newValue = isIncrease ? currentValue + 1 : currentValue - 1;
            newValue = Math.max(min, Math.min(max, newValue));

            input.value = newValue;
            this.updateCardProperty(target.replace('card-', ''), newValue);
        });
    });

    // 数字输入直接修改事件
    const numberInputs = basicTab.querySelectorAll('input[type="number"]');
    numberInputs.forEach(input => {
        input.addEventListener('input', (e) => {
            const property = e.target.id.replace('card-', '');
            const value = parseInt(e.target.value) || 0;
            this.updateCardProperty(property, value);
        });
    });

    // 下拉选择事件
    const selects = basicTab.querySelectorAll('select');
    selects.forEach(select => {
        select.addEventListener('change', (e) => {
            const property = e.target.id.replace('card-', '');
            this.updateCardProperty(property, e.target.value);

            // 如果是卡牌类型变化，更新战斗属性显示
            if (property === 'type') {
                this.updateStatsVisibility();
            }
        });
    });

    const optionGroups = basicTab.querySelectorAll('[data-option-group]');
    optionGroups.forEach(group => {
        group.addEventListener('click', (event) => {
            const target = event.target.closest('.option-chip');
            if (!target) return;

            const property = group.dataset.optionGroup;
            const value = target.dataset.optionValue;
            if (!property || value === undefined) return;

            group.querySelectorAll('.option-chip').forEach(chip => {
                chip.classList.toggle('is-selected', chip === target);
            });

            this.updateCardProperty(property, value);
            if (property === 'type') {
                this.updateStatsVisibility();
            }
            this.validateForm();
        });
    });

    const collectibleInput = basicTab.querySelector('#card-collectible');
    if (collectibleInput) {
        collectibleInput.addEventListener('change', (e) => {
            const value = !!e.target.checked;
            this.updateCardProperty('isCollectible', value);
            const label = basicTab.querySelector('#card-collectible-label');
            if (label) {
                label.textContent = value ? '是' : '否';
            }
        });
    }

    const previewUploadButton = basicTab.querySelector('[data-upload-kind="preview"]');
    if (previewUploadButton) {
        previewUploadButton.addEventListener('click', () => {
            this.openArtCropModal('preview');
        });
    }

    const switchVisualBtn = basicTab.querySelector('#switch-visual-canvas-btn');
    if (switchVisualBtn) {
        switchVisualBtn.addEventListener('click', async () => {
            this.useClassicBasicForm = false;
            await this.renderBasicInfoForm();
        });
    }
};

CardEditor.prototype.updateCardProperty = function(property, value) {
    if (!this.currentCard) return;

    // 属性名映射
    const propertyMap = {
        'name': 'name',
        'description': 'description',
        'cost': 'cost',
        'attack': 'attack',
        'health': 'health',
        'countdown': 'countdown',
        'mirageBuyCost': 'mirageBuyCost',
        'type': 'cardType',
        'class': 'cardClass',
        'rarity': 'rarity',
        'set': 'cardSet',
        'isCollectible': 'isCollectible'
    };

    const actualProperty = propertyMap[property] || property;
    this.currentCard[actualProperty] = value;

    if (actualProperty === 'cost' && this.isMirageScope() && !equalsIgnoreCase(normalizeEnumName(this.currentCard.cardType), 'Leader')) {
        this.currentCard.mirageTavernTier = Math.max(1, Number(value) || 1);
    }

    if (actualProperty === 'cardType' && equalsIgnoreCase(normalizeEnumName(value), 'Leader')) {
        this.currentCard.attack = 0;
        if (!this.currentCard.health || this.currentCard.health <= 1) {
            this.currentCard.health = 20;
        }
    }

    // 更新预览
    this.updateCardPreview();

};

CardEditor.prototype.updateStatsVisibility = function() {
    const statsContainer = this.container.querySelector('#stats-container');
    const countdownContainer = this.container.querySelector('#countdown-container');
    if (!statsContainer || !countdownContainer || !this.currentCard) return;

    const cardType = normalizeEnumName(this.currentCard.cardType);
    const isFollower = equalsIgnoreCase(cardType, 'Follower');
    const isLeader = equalsIgnoreCase(cardType, 'Leader');
    const shouldShowStats = isFollower || isLeader;
    const shouldShowCountdown = this.isMirageScope()
        ? !isLeader
        : (equalsIgnoreCase(cardType, 'Amulet') ||
           equalsIgnoreCase(cardType, 'Crest') ||
           equalsIgnoreCase(cardType, 'Faith'));
    const visibleDisplay = this.layoutMode === 'test' ? 'flex' : 'block';
    statsContainer.style.display = shouldShowStats ? visibleDisplay : 'none';
    const attackGroup = statsContainer.querySelector('#card-attack')?.closest('.form-group');
    if (attackGroup) {
        attackGroup.style.display = isLeader ? 'none' : '';
    }
    const healthInput = statsContainer.querySelector('#card-health');
    if (isLeader && healthInput) {
        healthInput.value = this.currentCard.health ?? 20;
    }
    countdownContainer.style.display = shouldShowCountdown ? visibleDisplay : 'none';
};

CardEditor.prototype.buildCardUpsertDto = function(isCreate) {
    if (!this.currentCard) return null;

    const cardType = normalizeEnumName(this.currentCard.cardType);
    const isFollower = equalsIgnoreCase(cardType, 'Follower');
    const isLeader = equalsIgnoreCase(cardType, 'Leader');
    const isCountdownType =
        equalsIgnoreCase(cardType, 'Amulet') ||
        equalsIgnoreCase(cardType, 'Crest') ||
        equalsIgnoreCase(cardType, 'Faith');
    const hasCountdownKeyword = (this.currentCard.keywords || []).some(keyword => equalsIgnoreCase(keyword, 'Countdown'));

    const rawPack = (this.currentCard.cardSet || '').trim();
    const currentCardId = this.getCurrentCardId();
    const isMirage = this.isMirageScope();
    const rawCost = Number(this.currentCard.cost) || 0;
    const normalizedMirageCost = isMirage && !isLeader
        ? Math.max(1, Math.trunc(rawCost) || 1)
        : 0;
    const mirageTier = isMirage && !isLeader
        ? normalizedMirageCost
        : null;

    return {
        id: isCreate ? null : currentCardId,
        name: this.currentCard.name?.trim() || '',
        type: cardType,
        rarity: normalizeEnumName(this.currentCard.rarity),
        card_set: isMirage ? MIRAGE_CARD_SET_NAME : (rawPack || '特殊'),
        card_set_definition_id: this.cardSetDefinitions.find(set =>
            String(set.name ?? set.Name).trim().toLowerCase() === (isMirage ? MIRAGE_CARD_SET_NAME : rawPack).toLowerCase()
        )?.id ?? null,
        class: normalizeEnumName(this.currentCard.cardClass),
        cost: isMirage ? normalizedMirageCost : Math.max(0, rawCost),
        mirageTavernTier: mirageTier,
        mirageBuyCost: isMirage && !isLeader
            ? Math.max(0, Math.min(10, Number.isFinite(Number(this.currentCard.mirageBuyCost)) ? Math.trunc(Number(this.currentCard.mirageBuyCost)) : 3))
            : null,
        isMirageCard: isMirage,
        attack: isFollower ? (Number(this.currentCard.attack) || 0) : (isLeader ? 0 : null),
        health: isFollower ? (Number(this.currentCard.health) || 1) : (isLeader ? Math.max(1, Number(this.currentCard.health) || 20) : null),
        countdown: isCountdownType && hasCountdownKeyword ? (Number(this.currentCard.countdown) || 0) : null,
        description: this.currentCard.description || '',
        artPreview: this.currentCard.artPreview || null,
        artDetail: this.currentCard.artDetail || null,
        isCollectible: this.currentCard.isCollectible ?? true,
        isImplemented: this.currentCard.isImplemented ?? true,
        presentation: this.currentCard.presentation ?? null,
        can_evolve: !!this.currentCard.canEvolve,
        evolve_id: this.currentCard.evolveId ?? null,
        keywords: Array.isArray(this.currentCard.keywords) ? this.currentCard.keywords : [],
        tribe: Array.isArray(this.currentCard.tribe) ? this.currentCard.tribe : []
    };
};

CardEditor.prototype.isKnownCardSetName = function(name) {
    const trimmed = (name || '').trim();
    if (!trimmed) return true;
    const definitions = Array.isArray(this.cardSetDefinitions) ? this.cardSetDefinitions : [];
    if (definitions.length === 0) return true;
    const normalized = trimmed.toLowerCase();
    return definitions.some(definition => {
        const candidate = definition?.name ?? definition?.Name ?? '';
        if (!candidate) return false;
        return String(candidate).trim().toLowerCase() === normalized;
    });
};

CardEditor.prototype.saveCurrentCard = async function(isSaveAs, options = {}) {
    if(this.cardTab==='styles' && !isSaveAs && this._saveStyleCardId===this.getCurrentCardId()){await this._saveStyle?.();return;}
    if (this._savePending) return false;
    this._savePending = true;
    try {
    if (this.editorMode !== 'card') {
        showAlert('玩家设定模式下不允许修改卡牌数据。');
        return false;
    }

    if (!this.currentCard) {
        showAlert('请先选择或创建一张卡牌');
        return false;
    }

    const savingCardRef = this.currentCard;
    const preSaveCardId = this.getCurrentCardId();
    const preSaveDraftKey = this.currentCard._draftKey
        || (preSaveCardId != null ? String(preSaveCardId) : null);
    const hasMatchingCanvas = Boolean(
        this.visualCanvas?.mseCard
        && preSaveDraftKey
        && String(this.visualCanvas.mseCard.editorId) === preSaveDraftKey
    );

    if (hasMatchingCanvas && !this.useClassicBasicForm) {
        this.visualCanvas.finishInline();
        this.visualCanvas.syncToEditorCard();
    }

    if (!this.validateForm()) {
        showAlert('请先修正表单错误后再保存');
        return false;
    }

    const rawSetName = (this.currentCard.cardSet || '').trim();
    if (!this.isMirageScope() && rawSetName && !this.isKnownCardSetName(rawSetName)) {
        const confirmed = await confirmDialog(`卡包 "${rawSetName}" 不存在，保存后将自动注册为新卡包。是否继续？`);
        if (!confirmed) {
            return false;
        }
        // 确认弹窗等待期间若用户已切换了卡牌，放弃本次旧保存
        if (this.currentCard !== savingCardRef) {
            return false;
        }
    }

    const isCreate = !!this.currentCard.isNew || !!isSaveAs;
    const hasPendingCanvasArt = hasMatchingCanvas && this.visualCanvas.hasPendingArtChanges();
    const shouldSyncCanvasAssets = hasMatchingCanvas && (!this.useClassicBasicForm || hasPendingCanvasArt);
    const preSaveMseCardSnapshot = shouldSyncCanvasAssets
        ? structuredClone(this.visualCanvas.mseCard)
        : null;

    const dto = this.buildCardUpsertDto(isCreate);
    if (hasPendingCanvasArt && dto.presentation?.art) {
        // 预览上传会烘焙裁切与翻转，后端保存中性裁切，避免重载后再次应用。
        dto.presentation = structuredClone(dto.presentation);
        dto.presentation.art.crop = { zoom: 1, x: 0.5, y: 0.5, flipX: false };
    }

    const editorRoot = this.container?.querySelector('.card-editor-layout') || this.container;
    const loader = editorRoot ? getBlockingLoader(editorRoot) : null;
    loader?.show('正在保存卡牌...');

    try {
        const url = isCreate
            ? `/api/cards${this.getScopeQueryParam()}`
            : `/api/cards/${preSaveCardId}${this.getScopeQueryParam()}`;
        const method = isCreate ? 'POST' : 'PUT';

        const response = await fetch(url, {
            method,
            headers: buildEditorIdentityHeaders({ 'Content-Type': 'application/json' }),
            body: JSON.stringify(dto)
        });

        if (!response.ok) {
            const msg = await readErrorResponseMessage(response);
            throw new Error(`HTTP ${response.status}: ${msg}`);
        }

        const saved = await response.json();
        const savedId = saved?.id ?? saved?.cardId ?? saved?.CardId;
        this.applyDataRevisionFromResponse(response, {
            reason: isCreate ? 'card-created' : 'card-updated',
            changedCardIds: savedId != null ? [savedId] : [],
            changedCards: [saved]
        });

        let isStillEditingSavedCard = false;
        if (savedId != null) {
            const finalSavedCard = this.convertApiCardToEditorFormat(saved);
            finalSavedCard.isNew = false;

            const sectionsToSync = preSaveMseCardSnapshot?.presentation?.sections
                || finalSavedCard.presentation?.sections
                || [];

            if (shouldSyncCanvasAssets && preSaveMseCardSnapshot && this.visualCanvas) {
                preSaveMseCardSnapshot.sourceCardId = savedId;
                let uploadedPreviewUrl = null;

                if (preSaveMseCardSnapshot.presentation?.art?.src && hasPendingCanvasArt) {
                    loader?.show('正在上传插画预览...');
                    const previewBlob = await this.visualCanvas.exportCroppedPreviewBlob(preSaveMseCardSnapshot);
                    if (previewBlob) {
                        const uploadResult = await this.uploadCardArt('preview', previewBlob, 'preview.webp', {
                            silent: true,
                            cardId: savedId
                        });
                        uploadedPreviewUrl = uploadResult?.previewArtUrl || null;
                        if (uploadResult?.artPreview) {
                            finalSavedCard.artPreview = uploadResult.artPreview;
                        }
                    }
                }

                await this.syncLinkedCrestAndFaithSections(finalSavedCard, sectionsToSync, { isSaveAs: !!isSaveAs });

                let detailBlob = null;
                if (preSaveMseCardSnapshot.presentation?.art?.src) {
                    loader?.show('正在生成并上传卡面...');
                    detailBlob = await this.visualCanvas.exportRenderedCardBlob('image/webp', preSaveMseCardSnapshot);
                }

                const isCanvasStillSame = Boolean(
                    this.visualCanvas.mseCard
                    && preSaveDraftKey
                    && String(this.visualCanvas.mseCard.editorId) === preSaveDraftKey
                );
                if (isCanvasStillSame) {
                    this.visualCanvas.markArtSaved(uploadedPreviewUrl, savedId);
                } else if (preSaveDraftKey) {
                    this.visualCanvas.migrateArtSession(preSaveDraftKey, savedId);
                }
                this.visualCanvas.saveLocalPresentationMeta(savedId, preSaveMseCardSnapshot.presentation);

                if (detailBlob) {
                    const detailRes = await this.uploadCardArt('detail', detailBlob, 'detail.webp', {
                        silent: true,
                        cardId: savedId
                    });
                    if (detailRes?.artDetail) {
                        finalSavedCard.artDetail = detailRes.artDetail;
                    }
                }
            } else {
                await this.syncLinkedCrestAndFaithSections(finalSavedCard, sectionsToSync, { isSaveAs: !!isSaveAs });
            }

            this.upsertCardInCache({
                ...saved,
                artPreview: finalSavedCard.artPreview,
                artDetail: finalSavedCard.artDetail
            });

            isStillEditingSavedCard = Boolean(
                this.currentCard && (
                    this.currentCard === savingCardRef
                    || (!isCreate && preSaveCardId != null && this.getCardIdKey(this.getCurrentCardId()) === this.getCardIdKey(savedId))
                )
            );

            if (isStillEditingSavedCard) {
                this.currentCard = finalSavedCard;
                this.updateTabAvailability();
                this.updateEditorHeader(`编辑卡牌: ${this.currentCard.name}`);
                await this.renderBasicInfoForm();
                this.updateCardPreview();

                const activeTab = this.container.querySelector('.tab-button.active')?.dataset.tab;
                if (activeTab === 'abilities') {
                    await this.renderAbilitiesList();
                } else if (activeTab === 'assets') {
                    this.renderAssetsTab();
                }
            }
        }

        await this.loadCardList();
        await this.loadCardSetDefinitions();
        await this.refreshAbilityDataCacheAfterSave();
        if (savedId != null && isStillEditingSavedCard) {
            this.highlightCardInList(savedId);
        }

        if (!options.silent) {
            showAlert(isCreate ? '卡牌创建成功！' : '卡牌保存成功！');
        }
        return options.returnResult
            ? { ok: true, savedId, isStillSameCard: isStillEditingSavedCard }
            : true;
    } catch (error) {
        console.error('CardEditor: Failed to save card:', error);
        showAlert(`保存失败: ${error.message}`);
        return false;
    } finally {
        loader?.hide();
    }
    } finally { this._savePending = false; }
};

CardEditor.prototype.syncLinkedCrestAndFaithSections = async function(ownerCard, sections, options = {}) {
    if (!ownerCard || !Array.isArray(sections)) return;
    const ownerType = normalizeEnumName(ownerCard.cardType);
    if (equalsIgnoreCase(ownerType, 'Crest') || equalsIgnoreCase(ownerType, 'Faith')) {
        return;
    }

    const activeCrestSections = sections.filter(sec =>
        sec && (sec.style === '纹章' || sec.style === '信仰') && String(sec.text ?? '').trim().length > 0
    );
    if (activeCrestSections.length === 0) return;

    const isMirage = this.isMirageScope();
    const ownerId = ownerCard.id;
    const ownerName = String(ownerCard.name ?? '').trim();

    const uploadDataUrlAsPreview = async (targetCardId, dataUrl) => {
        const blob = await (await fetch(dataUrl)).blob();
        const ext = blob.type === 'image/png' ? 'png' : blob.type === 'image/jpeg' ? 'jpg' : 'webp';
        const formData = new FormData();
        formData.append('file', blob, `crest-icon.${ext}`);
        const resp = await fetch(`/api/cards/${targetCardId}/art/preview${this.getScopeQueryParam()}`, {
            method: 'POST',
            headers: buildEditorIdentityHeaders(),
            body: formData
        });
        if (!resp.ok) {
            throw new Error(await readErrorResponseMessage(resp));
        }
        const updated = await resp.json();
        this.applyDataRevisionFromResponse(resp, {
            reason: 'card-art-updated',
            changedCardIds: [targetCardId],
            changedCards: [updated]
        });
        return updated;
    };

    for (const sec of activeCrestSections) {
        try {
            const expectedType = sec.style === '纹章' ? 'Crest' : 'Faith';
            const nextDesc = normalizeRuleSectionText(sec.text, { stripItalic: true })
                .replace(/^[【〖](?:纹章|紋章|信仰)[】〗]\s*\n?/, '')
                .trim();
            if (!nextDesc) continue;

            const cdMatch = nextDesc.match(/[【〖](?:倒数|倒數|吟唱)[_\s]*([0-9０-９]+)[】〗]/);
            const parsedCountdown = cdMatch
                ? Number(cdMatch[1].replace(/[０-９]/g, ch => String(ch.charCodeAt(0) - 0xff10)))
                : null;
            const customDataIcon = String(sec.icon ?? '').startsWith('data:image/') ? sec.icon : null;

            let matchedCard = null;
            const candidateRefId = Number(sec.refCardId);
            if (!options.isSaveAs && Number.isInteger(candidateRefId) && candidateRefId > 0 && candidateRefId !== Number(ownerId)) {
                const byId = (this.allCardsData || []).find(c => Number(c?.id ?? c?.Id ?? c?.CardId) === candidateRefId);
                const byIdType = normalizeEnumName(byId?.type ?? byId?.Type ?? byId?.cardType ?? '');
                if (byId && (equalsIgnoreCase(byIdType, 'Crest') || equalsIgnoreCase(byIdType, 'Faith'))) {
                    matchedCard = byId;
                }
            }

            const rawRefToken = String(sec.refToken ?? '').trim() || (ownerName ? `${sec.style}：${ownerName}` : '');
            if (!matchedCard && rawRefToken) {
                const byRef = findCardByReference(this.allCardsData || [], rawRefToken);
                const byRefId = Number(byRef?.id ?? byRef?.Id ?? byRef?.CardId);
                const byRefType = normalizeEnumName(byRef?.type ?? byRef?.Type ?? byRef?.cardType ?? '');
                if (byRef && byRefId !== Number(ownerId) && (equalsIgnoreCase(byRefType, 'Crest') || equalsIgnoreCase(byRefType, 'Faith'))) {
                    matchedCard = byRef;
                }
            }

            if (matchedCard) {
                const targetId = Number(matchedCard.id ?? matchedCard.Id ?? matchedCard.CardId);
                let fullTarget = this.cardDetailCache.get(this.getCardIdKey(targetId));
                if (!fullTarget) {
                    const detailResp = await fetch(`/api/cards/${targetId}${this.getScopeQueryParam()}`);
                    if (detailResp.ok) {
                        fullTarget = await detailResp.json();
                    } else {
                        fullTarget = matchedCard;
                    }
                }

                const prevDesc = normalizeRuleSectionText(fullTarget.description ?? fullTarget.Description ?? '', { stripItalic: true }).trim();
                let latestApiCard = fullTarget;

                if (prevDesc !== nextDesc) {
                    const existingKeywords = Array.isArray(fullTarget.keywords ?? fullTarget.Keywords)
                        ? [...(fullTarget.keywords ?? fullTarget.Keywords)]
                        : [];
                    if (parsedCountdown != null && !existingKeywords.some(k => equalsIgnoreCase(k, 'Countdown'))) {
                        existingKeywords.push('Countdown');
                    }
                    const targetSetName = isMirage
                        ? MIRAGE_CARD_SET_NAME
                        : (getCardSetInfo(fullTarget).name || ownerCard.cardSet || '特殊');
                    const updateDto = {
                        id: targetId,
                        name: String(fullTarget.name ?? fullTarget.Name ?? rawRefToken).trim(),
                        type: normalizeEnumName((fullTarget.type ?? fullTarget.Type) || expectedType),
                        rarity: normalizeEnumName((fullTarget.rarity ?? fullTarget.Rarity) || ownerCard.rarity || 'Bronze'),
                        card_set: targetSetName,
                        card_set_definition_id: this.cardSetDefinitions.find(set =>
                            String(set.name ?? set.Name).trim().toLowerCase() === targetSetName.toLowerCase()
                        )?.id ?? null,
                        class: normalizeEnumName((fullTarget.class ?? fullTarget.Class) || ownerCard.cardClass || 'Neutral'),
                        cost: isMirage ? Math.min(6, Math.max(1, Number(fullTarget.cost ?? fullTarget.Cost) || 1)) : Math.max(0, Number(fullTarget.cost ?? fullTarget.Cost) || 0),
                        mirageTavernTier: isMirage ? Math.min(6, Math.max(1, Number(fullTarget.mirageTavernTier ?? fullTarget.MirageTavernTier ?? fullTarget.cost ?? 1) || 1)) : null,
                        isMirageCard: isMirage,
                        attack: null,
                        health: null,
                        countdown: parsedCountdown != null ? parsedCountdown : (fullTarget.countdown ?? fullTarget.Countdown ?? null),
                        description: nextDesc,
                        artPreview: fullTarget.artPreview ?? fullTarget.ArtPreview ?? null,
                        artDetail: fullTarget.artDetail ?? fullTarget.ArtDetail ?? null,
                        isCollectible: Boolean(fullTarget.isCollectible ?? fullTarget.IsCollectible ?? false),
                        isImplemented: Boolean(fullTarget.isImplemented ?? fullTarget.IsImplemented ?? true),
                        presentation: null,
                        abilityIds: Array.isArray(fullTarget.abilityIds ?? fullTarget.AbilityIds) ? (fullTarget.abilityIds ?? fullTarget.AbilityIds) : undefined,
                        keywords: existingKeywords,
                        tribe: Array.isArray(fullTarget.tribe ?? fullTarget.Tribe) ? (fullTarget.tribe ?? fullTarget.Tribe) : []
                    };

                    const putResp = await fetch(`/api/cards/${targetId}${this.getScopeQueryParam()}`, {
                        method: 'PUT',
                        headers: buildEditorIdentityHeaders({ 'Content-Type': 'application/json' }),
                        body: JSON.stringify(updateDto)
                    });
                    if (!putResp.ok) {
                        throw new Error(await readErrorResponseMessage(putResp));
                    }
                    latestApiCard = await putResp.json();
                    try {
                        localStorage.removeItem(`svwb-mse-card-meta:${targetId}`);
                    } catch (_) {}
                    this.applyDataRevisionFromResponse(putResp, {
                        reason: 'card-updated',
                        changedCardIds: [targetId],
                        changedCards: [latestApiCard]
                    });
                }

                if (customDataIcon) {
                    latestApiCard = await uploadDataUrlAsPreview(targetId, customDataIcon);
                }

                const convertedLinked = this.convertApiCardToEditorFormat(latestApiCard);
                this.upsertCardInCache(latestApiCard);
                sec.refCardId = targetId;
                sec.refToken = convertedLinked.name || rawRefToken;
                if (convertedLinked.artPreview) {
                    sec.icon = this.getArtAssetUrl(convertedLinked.artPreview);
                }
            } else if (ownerName) {
                let derivedName = rawRefToken;
                if (!derivedName || derivedName.toLowerCase() === ownerName.toLowerCase()) {
                    derivedName = `${sec.style}：${ownerName}`;
                }
                const targetSetName = isMirage ? MIRAGE_CARD_SET_NAME : (ownerCard.cardSet || '特殊');
                const createDto = {
                    id: null,
                    name: derivedName,
                    type: expectedType,
                    rarity: normalizeEnumName(ownerCard.rarity || 'Bronze'),
                    card_set: targetSetName,
                    card_set_definition_id: this.cardSetDefinitions.find(set =>
                        String(set.name ?? set.Name).trim().toLowerCase() === targetSetName.toLowerCase()
                    )?.id ?? null,
                    class: normalizeEnumName(ownerCard.cardClass || 'Neutral'),
                    cost: isMirage ? 1 : 0,
                    mirageTavernTier: isMirage ? 1 : null,
                    isMirageCard: isMirage,
                    attack: null,
                    health: null,
                    countdown: parsedCountdown,
                    description: nextDesc,
                    artPreview: !customDataIcon ? (ownerCard.artPreview || null) : null,
                    artDetail: null,
                    isCollectible: false,
                    isImplemented: Boolean(ownerCard.isImplemented ?? false),
                    presentation: null,
                    keywords: parsedCountdown != null ? ['Countdown'] : [],
                    tribe: []
                };

                const postResp = await fetch(`/api/cards${this.getScopeQueryParam()}`, {
                    method: 'POST',
                    headers: buildEditorIdentityHeaders({ 'Content-Type': 'application/json' }),
                    body: JSON.stringify(createDto)
                });
                if (!postResp.ok) {
                    throw new Error(await readErrorResponseMessage(postResp));
                }
                let createdApiCard = await postResp.json();
                const createdId = Number(createdApiCard?.id ?? createdApiCard?.Id ?? createdApiCard?.CardId);
                this.applyDataRevisionFromResponse(postResp, {
                    reason: 'card-created',
                    changedCardIds: Number.isInteger(createdId) ? [createdId] : [],
                    changedCards: [createdApiCard]
                });

                if (customDataIcon && Number.isInteger(createdId) && createdId > 0) {
                    createdApiCard = await uploadDataUrlAsPreview(createdId, customDataIcon);
                }

                const convertedCreated = this.convertApiCardToEditorFormat(createdApiCard);
                this.upsertCardInCache(createdApiCard);
                sec.refCardId = convertedCreated.id;
                sec.refToken = convertedCreated.name || derivedName;
                if (convertedCreated.artPreview) {
                    sec.icon = this.getArtAssetUrl(convertedCreated.artPreview);
                }
            }
        } catch (err) {
            console.warn('CardEditor: Failed to sync linked Crest/Faith card:', err);
        }
    }
};

CardEditor.prototype.deleteCurrentCard = async function() {
    if (this.editorMode !== 'card') {
        showAlert('玩家设定模式下不允许删除卡牌。');
        return;
    }

    if (!this.currentCard || this.currentCard.isNew) {
        return;
    }

    if (!await confirmDialog(`确定要删除卡牌 "${this.currentCard.name}" 吗？此操作不可撤销。`)) {
        return;
    }

    try {
        const deletingCardId = this.getCurrentCardId();
        const response = await fetch(`/api/cards/${deletingCardId}${this.getScopeQueryParam()}`, { method: 'DELETE' });
        if (!response.ok && response.status !== 204) {
            const msg = await readErrorResponseMessage(response);
            throw new Error(`HTTP ${response.status}: ${msg}`);
        }
        this.removeCardFromCache(deletingCardId);
        this.applyDataRevisionFromResponse(response, {
            reason: 'card-deleted',
            deletedCardIds: deletingCardId != null ? [deletingCardId] : []
        });

        this.currentCard = null;
        this.syncVisualCanvasLayout();
        this.disableEditorButtons();
        this.updateTabAvailability();
        this.updateEditorHeader('等待选择卡牌');

        const basicTab = this.container.querySelector('#basic-tab');
        if (basicTab) {
            basicTab.innerHTML = `
                <div class="form-placeholder">
                    <p class="tech-text-dim">请选择一张卡牌进行编辑</p>
                </div>
            `;
        }

        this.updateEmptyPreviewState();

        await this.loadCardList();

        showAlert('卡牌已删除');
    } catch (error) {
        console.error('CardEditor: Failed to delete card:', error);
        showAlert(`删除失败: ${error.message}`);
    }
};

CardEditor.prototype.loadCardList = async function(options = {}) {
    const cardListContainer = this.container.querySelector('#card-list');
    if (!cardListContainer) {
        console.error('CardEditor: Card list container not found');
        return;
    }

    try {
        this.captureCardListScroll();
        // 显示加载状态
        cardListContainer.innerHTML = `
            <div class="card-list-loading">
                <div class="loading-spinner"></div>
                <p>正在加载卡牌列表...</p>
            </div>
        `;

        const allCards = await loadCardDefinitions({
            scope: this.cardPoolScope,
            useCache: !options.force,
            force: !!options.force,
            getCached: () => this.cardListCache,
            setCached: cards => {
                this.cardListCache = cards;
            },
            onResponse: response => {
                this.applyDataRevisionFromResponse(response);
            }
        });

        // 过滤卡牌：移除进化卡牌，只显示原始卡牌
        const filteredCards = this.filterCardsForList(allCards);

        // 存储所有卡牌数据（包括进化卡牌）以供后续使用
        this.allCardsData = allCards;

        // 渲染过滤后的卡牌列表（支持搜索/筛选进一步过滤）
        this.filteredCardsData = filteredCards;
        this.populateCardSetFilterOptions(this.filteredCardsData);
        this.applyCardListFilters();

    } catch (error) {
        console.error('CardEditor: Failed to load card list:', error);
        cardListContainer.innerHTML = `
            <div class="card-list-error">
                <div class="error-icon">⚠️</div>
                <p>加载卡牌列表失败</p>
                <small>${error.message}</small>
                <button class="retry-btn" onclick="cardEditorInstance.loadCardList({ force: true })">重试</button>
            </div>
        `;
    }
};

CardEditor.prototype.filterCardsForList = function(cards) {
    if (!Array.isArray(cards)) {
        return [];
    }

    const expectMirage = this.isMirageScope();
    return cards.filter(card => {
        if (Boolean(card.isMirageCard ?? card.IsMirageCard) !== expectMirage) {
            return false;
        }
        const rarity = normalizeEnumName(card.rarity ?? card.Rarity ?? '');
        if (isLeaderCardDefinition(card)) {
            const cardId = normalizeCardDefinitionId(getCardDefinitionId(card));
            const isEditableMirageLeader = expectMirage
                && cardId != null
                && cardId > 0;
            if (!isEditableMirageLeader) {
                return false;
            }
        }

        if (equalsIgnoreCase(rarity, 'Special')) {
            return false;
        }

        return true;
    });
};

CardEditor.prototype.selectCard = async function(cardId) {
    try {
        this.previousCardIdBeforeSettingsMode = null;
        if (this.editorMode !== 'card') {
            this.currentCard = null;
        }
        await this.ensureCardEditorMode();

        const editorRoot = this.container?.querySelector('.card-editor-layout') || this.container;
        const loader = editorRoot ? getBlockingLoader(editorRoot) : null;
        loader?.show('正在加载卡牌信息...');

        // 移除之前的选中状态
        this.container.querySelectorAll('.card-list-item').forEach(item => {
            item.classList.remove('selected');
        });

        // 添加选中状态
        const selectedItem = this.container.querySelector(`[data-card-id="${cardId}"]`);
        if (selectedItem) {
            selectedItem.classList.add('selected');
        }

        const cardKey = this.getCardIdKey(cardId);
        let cardData = cardKey ? this.cardDetailCache.get(cardKey) : null;

        if (cardData) {
        } else {
            // 获取卡牌详细信息
            const response = await fetch(`/api/cards/${cardId}${this.getScopeQueryParam()}`);
            if (!response.ok) {
                const msg = await readErrorResponseMessage(response);
                throw new Error(`HTTP ${response.status}: ${msg}`);
            }
            this.applyDataRevisionFromResponse(response);

            cardData = await response.json();
            if (Boolean(cardData.isMirageCard ?? cardData.IsMirageCard) !== this.isMirageScope()) {
                throw new Error('该卡牌不属于当前编辑环境，无法在此录卡入口加载。');
            }
            if (cardKey) {
                this.cardDetailCache.set(cardKey, cardData);
            }
        }

        // 转换为编辑器格式
        this.currentCard = this.convertApiCardToEditorFormat(cardData);
        this.cardTab = this.cardTab || 'basic';
        this.updateTabAvailability();

        // 更新编辑器标题
        this.updateEditorHeader(`编辑卡牌: ${this.currentCard.name}`);

        // 启用编辑器按钮
        this.enableEditorButtons();
        this.setMobilePane('edit');

        // 渲染基础信息表单
        await this.renderBasicInfoForm();

        const activeTab = this.container.querySelector('.tab-button.active')?.dataset.tab;
        if (activeTab === 'abilities') {
            await this.renderAbilitiesList();
        }
        if (activeTab === 'assets') {
            this.renderAssetsTab();
        }

        // 更新预览
        this.updateCardPreview();
        if(this.cardTab==='styles')void mountStyleEditor(this);
    } catch (error) {
        console.error('CardEditor: Failed to select card:', error);
        showAlert(`加载卡牌失败: ${error.message}`);
    } finally {
        const editorRoot = this.container?.querySelector('.card-editor-layout') || this.container;
        const loader = editorRoot ? getBlockingLoader(editorRoot) : null;
        loader?.hide();
    }
};

CardEditor.prototype.updateTabAvailability = function() {
    const basicTab = this.container.querySelector('.tab-button[data-tab="basic"]');
    const abilitiesTab = this.container.querySelector('.tab-button[data-tab="abilities"]');
    const assetsTab = this.container.querySelector('.tab-button[data-tab="assets"]');
    const canAccess = !!this.currentCard && !this.currentCard.isNew;
    const basicEnabled = this.editorMode === 'card';
    const abilitiesEnabled = this.editorMode === 'card' && canAccess;
    const assetsEnabled = this.editorMode === 'card' && canAccess;

    [
        [basicTab, basicEnabled],
        [abilitiesTab, abilitiesEnabled],
        [assetsTab, assetsEnabled]
    ].forEach(([tab, isEnabled]) => {
        if (!tab) return;
        tab.disabled = !isEnabled;
        tab.classList.toggle('is-disabled', !isEnabled);
    });

    if (this.editorMode === 'card' && !canAccess) {
        const activeTab = this.container.querySelector('.tab-button.active');
        if (activeTab && activeTab.dataset.tab !== 'basic') {
            this.switchTab('basic');
        }
    }

    const stylesTab=this.container.querySelector('[data-tab=styles]');if(stylesTab)stylesTab.disabled=!canAccess;
    this.syncEditorModeUi();
};

CardEditor.prototype.loadLeaderCard = async function() {
    const leaderId = this.getLeaderCardId();
    const leaderCardKey = this.getCardIdKey(leaderId);
    let leaderCard = leaderCardKey ? this.cardDetailCache.get(leaderCardKey) : null;
    if (!leaderCard) {
        const response = await fetch(`/api/cards/${leaderId}${this.getScopeQueryParam()}`);
        if (!response.ok) {
            const msg = await readErrorResponseMessage(response);
            throw new Error(`HTTP ${response.status}: ${msg}`);
        }
        this.applyDataRevisionFromResponse(response);
        leaderCard = await response.json();
        if (leaderCardKey) {
            this.cardDetailCache.set(leaderCardKey, leaderCard);
        }
    }

    return this.convertApiCardToEditorFormat(leaderCard);
};

CardEditor.prototype.enterLeaderAbilityMode = async function() {
    await this.enterPlayerSettingsMode();
};

CardEditor.prototype.exitLeaderAbilityMode = async function() {
    await this.exitPlayerSettingsMode();
};

CardEditor.prototype.convertApiCardToEditorFormat = function(apiCard) {
    // 处理不同的API响应格式
    const card = apiCard.data || apiCard;

    return {
        id: card.id !== undefined ? card.id : card.CardId,
        name: (card.name ?? card.Name) ?? '',
        description: (card.description ?? card.Description) ?? '',
        artPreview: (card.artPreview ?? card.ArtPreview) ?? null,
        artDetail: (card.artDetail ?? card.ArtDetail) ?? null,
        updatedAt: (card.updatedAt ?? card.UpdatedAt ?? card.cardUpdatedAt) ?? null,
        updatedBy: (card.updatedBy ?? card.UpdatedBy ?? card.cardUpdatedBy) ?? null,
        cost: (card.cost ?? card.Cost) ?? 0,
        attack: (card.attack ?? card.Attack) ?? 0,
        health: (card.health ?? card.Health) ?? 1,
        countdown: (card.countdown ?? card.Countdown) ?? 0,
        cardType: normalizeEnumName((card.type ?? card.Type) ?? 'Follower'),
        cardClass: normalizeEnumName((card.class ?? card.Class) ?? 'Neutral'),
        rarity: normalizeEnumName((card.rarity ?? card.Rarity) ?? 'Bronze'),
        cardSet: getCardSetInfo(card).name || '特殊',
        keywords: card.keywords || card.Keywords || [],
        tribe: card.tribe || card.Tribe || [],
        canEvolve: (card.canEvolve ?? card.CanEvolve) ?? false,
        evolveId: (card.evolve_id ?? card.evolveId ?? card.EvolveId) ?? null,
        isCollectible: (card.isCollectible ?? card.IsCollectible) ?? true,
        isImplemented: card.isImplemented ?? card.IsImplemented ?? true,
        presentation: card.presentation ?? card.Presentation ?? null,
        isMirageCard: Boolean(card.isMirageCard ?? card.IsMirageCard),
        mirageTavernTier: (Boolean(card.isMirageCard ?? card.IsMirageCard) && !equalsIgnoreCase(normalizeEnumName((card.type ?? card.Type) ?? 'Follower'), 'Leader'))
            ? Math.max(1, Number(card.cost ?? card.Cost) || 1)
            : null,
        mirageBuyCost: (Boolean(card.isMirageCard ?? card.IsMirageCard) && !equalsIgnoreCase(normalizeEnumName((card.type ?? card.Type) ?? 'Follower'), 'Leader'))
            ? (Number.isFinite(Number(card.mirageBuyCost ?? card.MirageBuyCost)) ? Number(card.mirageBuyCost ?? card.MirageBuyCost) : 3)
            : null,
        isNew: false // 标记为现有卡牌
    };
};

CardEditor.prototype.ensureKeywordLabelMap = async function() {
    if (this.keywordLabelMap) {
        return this.keywordLabelMap;
    }

    try {
        this.keywordLabelMap = await this.metadataManager.getEnum('CardKeyword');
    } catch (error) {
        console.warn('CardEditor: Failed to load keyword labels:', error);
        this.keywordLabelMap = {};
    }

    return this.keywordLabelMap;
};

CardEditor.prototype.getTagLabel = function(tag, tagType) {
    if (tagType !== 'keywords') {
        return tag;
    }

    if (!this.keywordLabelMap || !tag) {
        return tag;
    }

    if (this.keywordLabelMap[tag]) {
        return this.keywordLabelMap[tag];
    }

    const normalized = String(tag).toLowerCase();
    const key = Object.keys(this.keywordLabelMap).find(k => k.toLowerCase() === normalized);
    return key ? this.keywordLabelMap[key] : tag;
};

CardEditor.prototype.renderTagList = function(tags, tagType) {
    if (!Array.isArray(tags) || tags.length === 0) {
        return '<div class="tag-list-empty">暂无标签</div>';
    }

    return tags.map(tag => `
        <div class="tag-item" data-tag="${tag}" data-type="${tagType}">
            <span class="tag-text">${this.getTagLabel(tag, tagType)}</span>
            <button type="button" class="tag-remove-btn" data-tag="${tag}" data-type="${tagType}">×</button>
        </div>
    `).join('');
};

CardEditor.prototype.bindTagInputEvents = function() {
    const basicTab = this.container.querySelector('#basic-tab');
    if (!basicTab) return;

    // Keywords标签选择事件
    const keywordsSelect = basicTab.querySelector('#keywords-select');
    const keywordsAddBtn = basicTab.querySelector('[data-target="keywords"]');

    if (keywordsSelect) {
        // 加载关键字选项
        this.loadKeywordOptions(keywordsSelect);

        keywordsSelect.addEventListener('change', (e) => {
            const value = e.target.value.trim();
            if (value) {
                this.addTag('keywords', value);
                e.target.value = ''; // 重置选择
            }
        });
    }

    if (keywordsAddBtn) {
        keywordsAddBtn.addEventListener('click', () => {
            const value = keywordsSelect.value.trim();
            if (value) {
                this.addTag('keywords', value);
                keywordsSelect.value = ''; // 重置选择
            }
        });
    }

    // Tribe标签输入事件
    const tribeInput = basicTab.querySelector('#tribe-input');
    const tribeAddBtn = basicTab.querySelector('[data-target="tribe"]');
    const tribeSuggestionList = basicTab.querySelector('#tribe-suggestions');

    if (tribeSuggestionList) {
        void this.populateTribeSuggestionOptions(tribeSuggestionList);
    }

    if (tribeInput) {
        tribeInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                this.addTag('tribe', tribeInput.value.trim());
                tribeInput.value = '';
            }
        });
    }

    if (tribeAddBtn) {
        tribeAddBtn.addEventListener('click', () => {
            const value = tribeInput.value.trim();
            if (value) {
                this.addTag('tribe', value);
                tribeInput.value = '';
            }
        });
    }

    // 标签删除事件
    basicTab.addEventListener('click', (e) => {
        if (e.target.classList.contains('tag-remove-btn')) {
            const tag = e.target.dataset.tag;
            const type = e.target.dataset.type;
            this.removeTag(type, tag);
        }
    });
};

CardEditor.prototype.addTag = function(tagType, tagValue) {
    if (!this.currentCard || !tagValue) return;

    // 确保标签数组存在
    if (!this.currentCard[tagType]) {
        this.currentCard[tagType] = [];
    }

    // 检查是否已存在
    if (this.currentCard[tagType].includes(tagValue)) {
        showAlert(`标签 "${tagValue}" 已存在`);
        return;
    }

    // 添加标签
    this.currentCard[tagType].push(tagValue);

    // 更新UI
    this.updateTagList(tagType);

    // 更新预览
    this.updateCardPreview();

};

CardEditor.prototype.removeTag = function(tagType, tagValue) {
    if (!this.currentCard || !this.currentCard[tagType]) return;

    // 移除标签
    const index = this.currentCard[tagType].indexOf(tagValue);
    if (index > -1) {
        this.currentCard[tagType].splice(index, 1);

        // 更新UI
        this.updateTagList(tagType);

        // 更新预览
        this.updateCardPreview();

    }
};

CardEditor.prototype.updateTagList = function(tagType) {
    const tagListContainer = this.container.querySelector(`#${tagType}-list`);
    if (!tagListContainer) return;

    const tags = this.currentCard[tagType] || [];
    tagListContainer.innerHTML = this.renderTagList(tags, tagType);
};

CardEditor.prototype.bindFormValidationEvents = function() {
    const basicTab = this.container.querySelector('#basic-tab');
    if (!basicTab) return;

    // 获取所有需要验证的字段
    const nameInput = basicTab.querySelector('#card-name');
    const costInput = basicTab.querySelector('#card-cost');
    const attackInput = basicTab.querySelector('#card-attack');
    const healthInput = basicTab.querySelector('#card-health');
    const countdownInput = basicTab.querySelector('#card-countdown');
    const typeSelect = basicTab.querySelector('#card-type');
    const classSelect = basicTab.querySelector('#card-class');
    const raritySelect = basicTab.querySelector('#card-rarity');

    // 为每个字段添加验证事件
    [nameInput, costInput, attackInput, healthInput, countdownInput, typeSelect, classSelect, raritySelect].forEach(input => {
        if (input) {
            input.addEventListener('input', () => this.validateForm());
            input.addEventListener('change', () => this.validateForm());
        }
    });

    const optionGroups = basicTab.querySelectorAll('[data-option-group]');
    optionGroups.forEach(group => {
        group.addEventListener('click', () => this.validateForm());
    });

    // 初始验证
    this.validateForm();
};

CardEditor.prototype.validateForm = function() {
    if (!this.currentCard) return;

    const errors = [];

    // 验证必填字段
    if (!this.currentCard.name || this.currentCard.name.trim() === '') {
        errors.push('卡牌名称不能为空');
    }

    if (!this.currentCard.cardType) {
        errors.push('请选择卡牌类型');
    }

    if (!this.currentCard.cardClass) {
        errors.push('请选择卡牌职业');
    }

    if (!this.currentCard.rarity) {
        errors.push('请选择卡牌稀有度');
    }

    // 验证数值范围
    if (this.currentCard.cost < 0) {
        errors.push('费用必须为0或以上');
    }

    const cardType = normalizeEnumName(this.currentCard.cardType);
    const hasCountdownKeyword = (this.currentCard.keywords || []).some(keyword => equalsIgnoreCase(keyword, 'Countdown'));

    if (equalsIgnoreCase(cardType, 'Follower')) {
        if (this.currentCard.attack < 0) {
            errors.push('攻击力必须为0或以上');
        }

        if (this.currentCard.health < 0) {
            errors.push('生命值必须为0或以上');
        }
    } else if (equalsIgnoreCase(cardType, 'Leader')) {
        if (this.currentCard.health < 1 || this.currentCard.health > 99) {
            errors.push('主战者生命值必须在1至99之间');
        }
    } else if (
        (equalsIgnoreCase(cardType, 'Amulet') || equalsIgnoreCase(cardType, 'Crest') || equalsIgnoreCase(cardType, 'Faith'))
        && hasCountdownKeyword
    ) {
        if (this.currentCard.countdown < 0) {
            errors.push('倒计时必须为0或以上');
        }
    }

    // 更新保存按钮状态
    this.updateSaveButtonState(errors.length === 0);

    // 显示验证错误
    this.displayValidationErrors(errors);

    return errors.length === 0;
};

CardEditor.prototype.updateSaveButtonState = function(isValid) {
    const saveBtn = this.container.querySelector('#save-btn');
    const saveAsBtn = this.container.querySelector('#save-as-btn');

    if (saveBtn) {
        saveBtn.disabled = !isValid || !this.currentCard;
        saveBtn.title = isValid ? '保存卡牌' : '请修正表单错误后再保存';
    }

    if (saveAsBtn) {
        saveAsBtn.disabled = !isValid || !this.currentCard;
        saveAsBtn.title = isValid ? '另存为新卡牌' : '请修正表单错误后再保存';
    }
};

CardEditor.prototype.displayValidationErrors = function(errors) {
    // 移除之前的错误提示
    const existingErrors = this.container.querySelectorAll('.validation-error');
    existingErrors.forEach(error => error.remove());

    if (errors.length === 0) return;

    // 在表单顶部显示错误提示
    const basicTab = this.container.querySelector('#basic-tab .form-container');
    if (basicTab) {
        const errorContainer = document.createElement('div');
        errorContainer.className = 'validation-error';
        errorContainer.innerHTML = `
            <div class="error-header">
                <span class="error-icon">⚠️</span>
                <span class="error-title">表单验证失败</span>
            </div>
            <ul class="error-list">
                ${errors.map(error => `<li>${error}</li>`).join('')}
            </ul>
        `;

        basicTab.insertBefore(errorContainer, basicTab.firstChild);
    }
};

CardEditor.prototype.renderAbilitiesList = async function(options = {}) {
    const abilitiesListContainer = this.container.querySelector('#abilities-list');
    if (!abilitiesListContainer || !this.currentCard) {
        return;
    }

    try {
        const currentCardId = this.getCurrentCardId();
        const cardKey = this.getCardIdKey(currentCardId);
        const useCache = !options.force;
        const cachedAbilities = useCache && cardKey ? this.abilitiesByCardCache.get(cardKey) : null;

        if (Array.isArray(cachedAbilities)) {
            this.renderAbilitiesListContent(cachedAbilities);
            return;
        }

        // 显示加载状态
        abilitiesListContainer.innerHTML = `
            <div class="abilities-loading">
                <div class="loading-spinner"></div>
                <p>正在加载能力列表...</p>
            </div>
        `;


        // 获取卡牌的能力列表
        const response = await fetch(`/api/cards/${currentCardId}/abilities${this.getScopeQueryParam()}`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        this.applyDataRevisionFromResponse(response);

        const abilities = await response.json();

        if (cardKey) {
            this.abilitiesByCardCache.set(cardKey, Array.isArray(abilities) ? abilities : []);
        }

        // 渲染能力列表
        this.renderAbilitiesListContent(abilities);

    } catch (error) {
        console.error('CardEditor: Failed to load abilities:', error);
        abilitiesListContainer.innerHTML = `
            <div class="abilities-error">
                <div class="error-icon">⚠️</div>
                <p>加载能力列表失败</p>
                <small>${error.message}</small>
                <button class="retry-btn" onclick="cardEditorInstance.renderAbilitiesList({ force: true })">重试</button>
            </div>
        `;
    }
};

CardEditor.prototype.renderAbilitiesListContent = function(abilities) {
    const abilitiesListContainer = this.container.querySelector('#abilities-list');
    if (!abilitiesListContainer) return;

    this.currentCardAbilities = Array.isArray(abilities) ? abilities : [];
    if (Array.isArray(abilities)) {
        abilities.forEach(ability => {
            const key = this.getAbilityIdKey(ability?.id ?? ability?.Id);
            if (key) {
                this.abilityDetailCache.set(key, ability);
            }
        });
    }
    const currentCardKey = this.getCardIdKey(this.getCurrentCardId());
    if (currentCardKey) {
        this.abilityCountsCache.set(currentCardKey, this.currentCardAbilities.length);
    }

    if (!Array.isArray(abilities) || abilities.length === 0) {
        abilitiesListContainer.innerHTML = `
            <div class="abilities-placeholder">
                <p>暂无关联能力</p>
                <small>点击"添加能力"开始配置</small>
            </div>
        `;
        return;
    }

    let eventLabelLookup = new Map();
    try {
        eventLabelLookup = buildEventLabelLookup(this.metadataManager?.getEvents?.() ?? []);
    } catch (error) {
        eventLabelLookup = new Map();
    }

    // 生成能力列表HTML
    const abilitiesHTML = abilities.map(ability => {
        const abilityConfig = this.parseAbilityConfig(ability);
        const abilityTypeLabel = resolveAbilityTypeLabel(ability, abilityConfig);
        const eventLabels = formatAbilityEventLabels(abilityConfig, abilityTypeLabel, eventLabelLookup);
        const eventTagsHtml = eventLabels
            .map(label => label ? `<span class="ability-tag tech-tag">${label}</span>` : '')
            .filter(Boolean)
            .join('');
        const keywordEntries = normalizeAbilityKeywords(abilityConfig);
        const keywordTagsHtml = keywordEntries
            .map(entry => {
                const label = formatAbilityKeywordLabel(entry);
                return label ? `<span class="config-item tech-tag">${label}</span>` : '';
            })
            .filter(Boolean)
            .join('');
        return `
        <div class="ability-item tech-panel-item" data-ability-id="${ability.id}">
            <div class="ability-header">
                <div class="ability-info">
                    <span class="ability-name tech-text-bold">${ability.name || '未命名能力'}</span>
                    ${eventTagsHtml}
                </div>
                <div class="ability-actions">
                    <button class="btn-tech-small edit-ability-btn" data-ability-id="${ability.id}">编辑</button>
                    <button class="btn-tech-small btn-danger remove-ability-btn" data-ability-id="${ability.id}">删除</button>
                </div>
            </div>
            <div class="ability-details">
                <div class="ability-description tech-text-dim">${ability.description || '暂无描述'}</div>
                <div class="ability-config">
                    <span class="config-item tech-tag">类型：${abilityTypeLabel}</span>
                    ${keywordTagsHtml}
                    <span class="config-item tech-tag ${ability.isActive ? 'status-active' : 'status-inactive'}">
                        ${ability.isActive ? '启用中' : '未启用'}
                    </span>
                </div>
            </div>
        </div>
    `;
    }).join('');

    abilitiesListContainer.innerHTML = abilitiesHTML;

    // 绑定能力项事件
    this.bindAbilityItemEvents();
};

CardEditor.prototype.bindAbilityItemEvents = function() {
    const abilitiesListContainer = this.container.querySelector('#abilities-list');
    if (!abilitiesListContainer) return;

    // 编辑能力按钮事件
    const editButtons = abilitiesListContainer.querySelectorAll('.edit-ability-btn');
    editButtons.forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const abilityId = btn.dataset.abilityId;
            this.editAbility(abilityId);
        });
    });

    // 移除能力按钮事件
    const removeButtons = abilitiesListContainer.querySelectorAll('.remove-ability-btn');
    removeButtons.forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const abilityId = btn.dataset.abilityId;
            this.removeAbility(abilityId);
        });
    });

    // 能力项点击事件（展开/收起详情）
    const abilityItems = abilitiesListContainer.querySelectorAll('.ability-item');
    abilityItems.forEach(item => {
        item.addEventListener('click', () => {
            item.classList.toggle('expanded');
        });
    });
};

CardEditor.prototype.showAddAbilityModal = async function() {
if (!this.currentCard) {
    showAlert('请先选择一张卡牌');
    return;
}


try {
    this.showAddAbilityTypeModal();

} catch (error) {
    console.error('CardEditor: Failed to show add ability modal:', error);
    showAlert(`打开添加能力窗口失败: ${error.message}`);
}
};

CardEditor.prototype.showAddAbilityTypeModal = function() {
    const modalHTML = `
        <div class="modal-overlay" id="add-ability-type-modal">
            <div class="modal-container ability-edit-container ability-type-modal">
                <div class="modal-header">
                    <h3>选择能力类型</h3>
                    <button class="modal-close-btn" id="close-add-ability-type-modal">×</button>
                </div>
                <div class="modal-content">
                    <div class="ability-mode-selector">
                        <div class="btn-group-row" id="add-ability-mode-options">
                            <button type="button" class="btn-option" data-mode="effect">触发式能力</button>
                            <button type="button" class="btn-option" data-mode="aura">持久式能力</button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHTML);

    const modal = document.getElementById('add-ability-type-modal');
    const closeBtn = document.getElementById('close-add-ability-type-modal');
    const modeOptions = document.getElementById('add-ability-mode-options');

    const close = () => {
        if (modal) {
            modal.remove();
        }
    };

    if (closeBtn) {
        closeBtn.addEventListener('click', close);
    }

    if (modeOptions) {
        modeOptions.addEventListener('click', async (event) => {
            const button = event.target.closest('button[data-mode]');
            if (!button) return;
            const mode = button.dataset.mode;
            close();
            await this.showAddAbilityEditorModal(mode);
        });
    }
};

CardEditor.prototype.showAddAbilityEditorModal = async function(mode) {
    const modalHTML = `
        <div class="modal-overlay" id="add-ability-modal">
            <div class="modal-container ability-edit-container">
                <div class="modal-header">
                    <h3>添加能力</h3>
                    <button class="modal-close-btn" id="close-add-ability-modal">×</button>
                </div>
                <div class="modal-content" id="add-ability-editor-container">
                    <!-- 能力编辑器将在这里渲染 -->
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHTML);

    const editorContainer = document.getElementById('add-ability-editor-container');
    const { AbilityEditor } = await import('./components/AbilityEditor.js');
    let abilityEditor = new AbilityEditor();
    const currentCardId = this.getCurrentCardId();

    const defaultAbilityConfig = {
        cardId: currentCardId,
        cardName: this.currentCard.name || '',
        abilitySequence: this.getNextAbilitySequence(),
        name: '',
        description: '',
        abilityKeywords: [],
        abilityType: 1,
        priority: 0,
        isActive: true,
        broadcastOnTrigger: true,
        buffDisplay: true,
        type: mode === 'aura' ? 'Continuous' : 'Triggered',
        trigger: {
            eventTypes: [],
            relationType: 'Any',
            timing: 'On',
            validZones: []
        },
        conditions: [],
        targets: [],
        effects: [],
        counterRegistrations: [],
        playerCounterRegistrations: []
    };

    await abilityEditor.render(
        editorContainer,
        defaultAbilityConfig,
        async (newConfig) => {
            if (newConfig === null) {
                this.closeAddAbilityModal();
            } else {
                await this.createNewAbilityFromConfig(newConfig);
                this.closeAddAbilityModal();
            }
        },
        { mode }
    );

    const closeBtn = document.getElementById('close-add-ability-modal');
    if (closeBtn) {
        closeBtn.addEventListener('click', () => this.closeAddAbilityModal());
    }
};

/**
* 使用配置创建新能力
*/
CardEditor.prototype.createNewAbilityFromConfig = async function(config) {
try {
    const currentCardId = this.getCurrentCardId();

    // 构建API请求数据
    // 注意：后端API可能需要特定的字段结构，这里确保与saveAbilityChanges保持一致但用于创建
    const abilityData = {
        cardId: currentCardId,
        name: config.name,
        description: config.description,
        abilityKeywords: config.abilityKeywords || [],
        abilityType: config.abilityType || 1,
        priority: config.priority || 0,
        isActive: config.isActive,
        jsonConfig: config.jsonConfig // AbilityEditor已经构建了jsonConfig
    };

    // 调用后端API创建能力
    const response = await fetch(`/api/abilities${this.getScopeQueryParam()}`, {
        method: 'POST',
        headers: buildEditorIdentityHeaders({
            'Content-Type': 'application/json'
        }),
        body: JSON.stringify(abilityData)
    });

    const result = await this.readAbilityWriteResponse(response);
    this.applyDataRevisionFromResponse(response, {
        reason: 'ability-created',
        changedCardIds: currentCardId != null ? [currentCardId] : []
    });
    this.applyCardEditMetadata(result);
    this.updateEditorHeader(`编辑卡牌: ${this.currentCard?.name || ''}`);

    this.invalidateAbilityLibraryCache();
    this.updateAbilityCachesAfterSave(result, currentCardId);

    // 刷新能力列表
    await this.renderAbilitiesList();
    
    showAlert('能力添加成功！');

} catch (error) {
    console.error('CardEditor: Failed to create ability:', error);
    showAlert(`添加能力失败: ${error.message}`);
}
};

CardEditor.prototype.bindAddAbilityModalEvents = function() {
    const modal = document.getElementById('add-ability-modal');
    if (!modal) return;

    // 关闭模态框事件
    const closeBtn = modal.querySelector('#close-add-ability-modal');
    const cancelBtn = modal.querySelector('#cancel-add-ability');
    [closeBtn, cancelBtn].forEach(btn => {
        if (btn) {
            btn.addEventListener('click', () => this.closeAddAbilityModal());
        }
    });

    // 标签页切换事件
    const tabButtons = modal.querySelectorAll('.modal-tab-btn');
    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const tabName = btn.dataset.tab;
            this.switchAddAbilityTab(tabName);
        });
    });

    // 确认添加按钮事件
    const confirmBtn = modal.querySelector('#confirm-add-ability');
    if (confirmBtn) {
        confirmBtn.addEventListener('click', async () => {
            confirmBtn.disabled = true;
            confirmBtn.textContent = 'PROCESSING...';
            try {
                await this.confirmAddAbility();
            } catch (error) {
                console.error('Add ability failed:', error);
            } finally {
                confirmBtn.disabled = false;
                confirmBtn.textContent = '确认添加';
            }
        });
    }

    // 加载现有能力列表
    this.loadExistingAbilities();
};

CardEditor.prototype.closeAddAbilityModal = function() {
    const modal = document.getElementById('add-ability-modal');
    if (modal) {
        modal.remove();
    }
};

CardEditor.prototype.getNextAbilitySequence = function() {
    const baseName = (this.currentCard?.name || '').trim();
    const abilities = Array.isArray(this.currentCardAbilities) ? this.currentCardAbilities : [];
    if (!baseName) {
        return abilities.length + 1;
    }

    const pattern = new RegExp(`^${escapeRegExp(baseName)}(\\d+)$`);
    let max = 0;

    abilities.forEach(ability => {
        const name = (ability?.name ?? ability?.Name ?? '').toString().trim();
        const match = name.match(pattern);
        if (!match) return;
        const value = parseInt(match[1], 10);
        if (!Number.isNaN(value) && value > max) {
            max = value;
        }
    });

    return max + 1;
};

CardEditor.prototype.showLinkExistingAbilityModal = async function() {
    const currentCardId = this.getCurrentCardId();
    if (!this.currentCard || currentCardId == null) {
        showAlert('请先选择并保存一张卡牌');
        return;
    }

    try {
        const modalHTML = `
            <div class="modal-overlay" id="link-ability-modal">
                <div class="modal-container ability-edit-container ability-copy-container">
                    <div class="modal-header">
                        <h3>复制现有能力</h3>
                        <button class="modal-close-btn" id="close-link-ability-modal">×</button>
                    </div>
                    <div class="modal-content ability-copy-modal">
                        <div class="ability-copy-filters">
                            <div class="ability-copy-search">
                                <label class="form-label" for="ability-copy-search">筛选</label>
                                <input class="tech-input" type="search" id="ability-copy-search" placeholder="搜索卡牌名称或ID">
                            </div>
                            <div class="ability-copy-stats tech-text-dim" id="ability-copy-stats">卡牌 0 / 能力 0</div>
                        </div>
                        <div class="tech-text-dim ability-copy-hint">点击卡牌展开能力后选择复制</div>
                        <div class="ability-copy-list custom-scrollbar" id="link-abilities-list">
                            <div class="loading-spinner"></div>
                            <p>正在加载卡牌列表...</p>
                        </div>
                        <div class="ability-copy-pagination">
                            <button class="btn-tech btn-secondary btn-small" id="ability-copy-prev" disabled>上一页</button>
                            <div class="ability-copy-page-info tech-text-dim" id="ability-copy-page-info">1 / 1</div>
                            <button class="btn-tech btn-secondary btn-small" id="ability-copy-next" disabled>下一页</button>
                        </div>
                        <div class="modal-actions ability-copy-actions">
                            <button class="btn-tech btn-secondary" id="cancel-link-ability">取消</button>
                            <button class="btn-tech btn-primary" id="confirm-link-ability" disabled>确认复制</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHTML);

        const modal = document.getElementById('link-ability-modal');
        const modalContainer = modal?.querySelector('.ability-copy-container');
        const modalLoader = modalContainer ? getBlockingLoader(modalContainer) : null;
        const closeBtn = document.getElementById('close-link-ability-modal');
        const cancelBtn = document.getElementById('cancel-link-ability');
        const confirmBtn = document.getElementById('confirm-link-ability');
        const listEl = document.getElementById('link-abilities-list');

        const close = () => {
            modalLoader?.hide();
            this.closeLinkExistingAbilityModal();
        };
        if (closeBtn) closeBtn.addEventListener('click', close);
        if (cancelBtn) cancelBtn.addEventListener('click', close);
        if (modal) {
            modal.addEventListener('click', (e) => {
                if (e.target === modal) close();
            });
        }

        const searchInput = document.getElementById('ability-copy-search');
        const statsEl = document.getElementById('ability-copy-stats');
        const pageInfoEl = document.getElementById('ability-copy-page-info');
        const prevBtn = document.getElementById('ability-copy-prev');
        const nextBtn = document.getElementById('ability-copy-next');
        const expandedCards = new Set();
        const pageSize = 12;
        let currentPage = 1;
        let cardEntries = [];
        let filteredEntries = [];

        this.selectedExistingAbilityId = null;

        const normalizeCardId = (value) => value === undefined || value === null ? null : String(value);

        let allCards = Array.isArray(this.cardListCache) ? this.cardListCache : null;
        if (!Array.isArray(allCards) || allCards.length === 0) {
            allCards = Array.isArray(this.allCardsData) ? this.allCardsData : null;
        }

        modalLoader?.show('正在加载能力库...');
        try {
            const abilityData = await ensureAbilityData(this.cardPoolScope);
            allCards = Array.isArray(abilityData.cards) ? abilityData.cards : [];
            this.cardListCache = Array.isArray(allCards) ? allCards : [];
            this.allCardsData = this.cardListCache;

            const abilityMap = new Map();
            const countMap = this.abilityCountsCache;
            countMap.clear();

            this.cardListCache.forEach(card => {
                const rawCardId = this.getResolvedCardId(card);
                const cardId = normalizeCardId(rawCardId);
                if (cardId == null) return;
                const abilities = abilityData.abilitiesByCard?.get?.(Number(rawCardId)) || [];
                const normalized = Array.isArray(abilities) ? abilities : [];
                abilityMap.set(cardId, normalized);
                countMap.set(cardId, normalized.length);
                normalized.forEach(ability => {
                    const key = this.getAbilityIdKey(ability?.id ?? ability?.Id);
                    if (key) {
                        this.abilityDetailCache.set(key, ability);
                    }
                });
            });

            this.abilitiesByCardCache = abilityMap;
        } catch (error) {
            modalLoader?.hide();
            listEl.innerHTML = `<div class="abilities-placeholder"><p>加载能力失败</p></div>`;
            return;
        } finally {
            modalLoader?.hide();
        }

        const abilityCache = this.abilitiesByCardCache;
        const abilityCountCache = this.abilityCountsCache;

        if (!Array.isArray(allCards) || allCards.length === 0) {
            listEl.innerHTML = `<div class="abilities-placeholder"><p>暂无可用卡牌</p></div>`;
            return;
        }

        this.allCardsData = Array.isArray(allCards) ? allCards : [];
        const visibleCards = this.filterCardsForList(allCards);
        cardEntries = visibleCards.map(card => {
            const cardId = normalizeCardId(this.getResolvedCardId(card));
            if (cardId == null) return null;

            const cardName = (card.name ?? card.Name ?? `Card ${cardId}`).toString().trim() || `Card ${cardId}`;
            const cardCost = (card.cost ?? card.Cost) ?? 0;
            const cardType = normalizeEnumName((card.type ?? card.Type) ?? 'UNKNOWN');
            const cardClass = normalizeEnumName((card.class ?? card.Class) ?? 'NEUTRAL');
            const cardRarity = normalizeEnumName((card.rarity ?? card.Rarity) ?? 'COMMON');
            const abilities = abilityCache.get(cardId) || [];
            const abilitySearchText = Array.isArray(abilities)
                ? abilities.map(ability => (ability?.name ?? ability?.Name ?? '').toString().trim()).join(' ')
                : '';
            const cardSearchText = [
                cardName,
                cardId,
                cardCost,
                cardType,
                cardClass,
                cardRarity,
                abilitySearchText
            ].join(' ').toLowerCase();

            return {
                cardId,
                cardName,
                cardCost,
                cardType,
                cardClass,
                cardRarity,
                cardSearchText
            };
        }).filter(Boolean).sort((a, b) => a.cardName.localeCompare(b.cardName, undefined, { numeric: true, sensitivity: 'base' }));

        const updateStats = (cardCount, loadedAbilityCount) => {
            if (statsEl) {
                statsEl.textContent = `卡牌 ${cardCount} / 能力 ${loadedAbilityCount}`;
            }
        };

        const updatePagination = (totalPages) => {
            const normalizedTotal = Math.max(totalPages, 1);
            if (pageInfoEl) {
                pageInfoEl.textContent = `${currentPage} / ${normalizedTotal}`;
            }
            if (prevBtn) prevBtn.disabled = currentPage <= 1;
            if (nextBtn) nextBtn.disabled = currentPage >= normalizedTotal;
        };

        const buildAbilityHtml = (abilities) => {
            const selectedAbilityId = this.selectedExistingAbilityId ? String(this.selectedExistingAbilityId) : null;
            return abilities.map(ability => {
                const abilityId = String(ability.id ?? '');
                const abilityName = ability?.name ?? ability?.Name ?? '未命名能力';
                const abilityDescription = ability?.description ?? ability?.Description ?? '';
                const keywordText = formatAbilityKeywordsDisplay(ability);
                const isSelected = selectedAbilityId === abilityId;
                return `
                    <button type="button" class="ability-copy-ability${isSelected ? ' selected' : ''}" data-ability-id="${abilityId}">
                        <div class="ability-copy-ability-header">
                            <span class="ability-copy-ability-name tech-text-bold">${abilityName}</span>
                            ${keywordText ? `<span class="ability-copy-ability-tag tech-tag">${keywordText}</span>` : ''}
                        </div>
                        ${abilityDescription ? `<div class="ability-copy-ability-desc tech-text-dim">${abilityDescription}</div>` : ''}
                    </button>
                `;
            }).join('');
        };

        const renderList = () => {
            const totalPages = Math.max(1, Math.ceil(filteredEntries.length / pageSize));
            currentPage = Math.min(currentPage, totalPages);

            if (filteredEntries.length === 0) {
                listEl.innerHTML = `<div class="abilities-placeholder"><p>没有匹配的卡牌</p></div>`;
                updateStats(0, 0);
                updatePagination(0);
                return;
            }

            const start = (currentPage - 1) * pageSize;
            const pageEntries = filteredEntries.slice(start, start + pageSize);
            const getAbilityCount = (cardId) => {
                const cachedCount = abilityCountCache.get(cardId);
                if (Number.isFinite(cachedCount)) {
                    return cachedCount;
                }
                const abilities = abilityCache.get(cardId);
                return Array.isArray(abilities) ? abilities.length : 0;
            };

            const totalKnownAbilityCount = filteredEntries.reduce((sum, entry) => {
                return sum + getAbilityCount(entry.cardId);
            }, 0);

            listEl.innerHTML = pageEntries.map(entry => {
                const cachedAbilities = abilityCache.get(entry.cardId);
                const abilities = Array.isArray(cachedAbilities) ? cachedAbilities : [];
                const abilityCountLabel = getAbilityCount(entry.cardId);
                const isExpanded = expandedCards.has(entry.cardId);

                let bodyHtml = '';
                if (isExpanded) {
                    if (abilities.length === 0) {
                        bodyHtml = `<div class="abilities-placeholder"><p>暂无可复制能力</p></div>`;
                    } else {
                        bodyHtml = `<div class="ability-copy-abilities">${buildAbilityHtml(abilities)}</div>`;
                    }
                }

                return `
                    <div class="ability-copy-card${isExpanded ? ' expanded' : ''}" data-card-id="${entry.cardId}">
                        <button type="button" class="ability-copy-card-header" aria-expanded="${isExpanded}">
                            <div class="ability-copy-card-main">
                                <span class="ability-copy-card-name tech-text-bold">${entry.cardName}</span>
                                <span class="ability-copy-card-id tech-text-dim">#${entry.cardId}</span>
                            </div>
                            <div class="ability-copy-card-meta">
                                <span class="tech-badge">费用 ${entry.cardCost}</span>
                                <span class="tech-tag">${entry.cardType}</span>
                                <span class="tech-tag">${entry.cardClass}</span>
                                <span class="tech-tag">能力 ${abilityCountLabel}</span>
                            </div>
                            <span class="ability-copy-card-toggle" aria-hidden="true">></span>
                        </button>
                        <div class="ability-copy-card-body">
                            ${bodyHtml}
                        </div>
                    </div>
                `;
            }).join('');

            listEl.querySelectorAll('.ability-copy-card').forEach(cardEl => {
                const header = cardEl.querySelector('.ability-copy-card-header');
                const cardId = cardEl.dataset.cardId;
                if (!header || !cardId) return;
                header.addEventListener('click', () => {
                    const isExpanded = cardEl.classList.toggle('expanded');
                    header.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
                    if (isExpanded) {
                        expandedCards.add(cardId);
                    } else {
                        expandedCards.delete(cardId);
                    }
                    renderList();
                });
            });

            listEl.querySelectorAll('.ability-copy-ability').forEach(abilityEl => {
                const abilityId = abilityEl.dataset.abilityId;
                if (!abilityId) return;
                abilityEl.addEventListener('click', (event) => {
                    event.stopPropagation();
                    listEl.querySelectorAll('.ability-copy-ability').forEach(el => el.classList.remove('selected'));
                    abilityEl.classList.add('selected');
                    this.selectedExistingAbilityId = abilityId;
                    if (confirmBtn) confirmBtn.disabled = false;
                });
            });

            updateStats(filteredEntries.length, totalKnownAbilityCount);
            updatePagination(totalPages);
        };

        const applyFilter = (filterText = '') => {
            const normalizedFilter = filterText.trim().toLowerCase();
            filteredEntries = cardEntries.filter(entry => {
                if (!normalizedFilter) return true;
                return entry.cardSearchText.includes(normalizedFilter);
            });
            currentPage = 1;
            renderList();
        };

        if (searchInput) {
            searchInput.addEventListener('input', () => applyFilter(searchInput.value));
        }
        if (prevBtn) {
            prevBtn.addEventListener('click', () => {
                if (currentPage > 1) {
                    currentPage -= 1;
                    renderList();
                }
            });
        }
        if (nextBtn) {
            nextBtn.addEventListener('click', () => {
                const totalPages = Math.max(1, Math.ceil(filteredEntries.length / pageSize));
                if (currentPage < totalPages) {
                    currentPage += 1;
                    renderList();
                }
            });
        }

        applyFilter('');

        if (confirmBtn) {
            confirmBtn.addEventListener('click', async () => {
                if (!this.selectedExistingAbilityId) return;
                confirmBtn.disabled = true;
                modalLoader?.show('复制能力中...');
                try {
                    await this.copyExistingAbilityById(this.selectedExistingAbilityId);
                    await this.renderAbilitiesList();
                    close();
                    showAlert('能力复制成功！');
                } catch (error) {
                    console.error('CardEditor: Failed to copy ability:', error);
                    showAlert(`复制能力失败: ${error.message}`);
                } finally {
                    modalLoader?.hide();
                    confirmBtn.disabled = false;
                }
            });
        }
    } catch (error) {
        console.error('CardEditor: Failed to show copy ability modal:', error);
        showAlert(`打开复制窗口失败: ${error.message}`);
        this.closeLinkExistingAbilityModal();
    }
};

CardEditor.prototype.closeLinkExistingAbilityModal = function() {
    const modal = document.getElementById('link-ability-modal');
    if (modal) {
        modal.remove();
    }
};

CardEditor.prototype.switchAddAbilityTab = function(tabName) {
    const modal = document.getElementById('add-ability-modal');
    if (!modal) return;

    // 移除所有活动状态
    modal.querySelectorAll('.modal-tab-btn').forEach(btn => {
        btn.classList.remove('active');
    });
    modal.querySelectorAll('.modal-tab-panel').forEach(panel => {
        panel.classList.remove('active');
    });

    // 激活选中的标签页
    const tabButton = modal.querySelector(`[data-tab="${tabName}"]`);
    const tabPanel = modal.querySelector(`#${tabName}-ability-panel`);

    if (tabButton && tabPanel) {
        tabButton.classList.add('active');
        tabPanel.classList.add('active');
    }
};

CardEditor.prototype.loadExistingAbilities = async function() {
    const existingAbilitiesList = document.getElementById('existing-abilities-list');
    if (!existingAbilitiesList) return;

    try {
        if (Array.isArray(this.abilitiesLibraryCache)) {
            this.renderExistingAbilitiesList(this.abilitiesLibraryCache);
            return;
        }


        // 调用后端API获取所有能力
        const response = await fetch(`/api/abilities${this.getScopeQueryParam()}`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        this.applyDataRevisionFromResponse(response);

        const abilities = await response.json();
        this.abilitiesLibraryCache = Array.isArray(abilities) ? abilities : [];
        if (Array.isArray(abilities)) {
            abilities.forEach(ability => {
                const key = this.getAbilityIdKey(ability?.id ?? ability?.Id);
                if (key) {
                    this.abilityDetailCache.set(key, ability);
                }
            });
        }

        // 渲染现有能力列表
        this.renderExistingAbilitiesList(abilities);

    } catch (error) {
        console.error('CardEditor: Failed to load existing abilities:', error);
        existingAbilitiesList.innerHTML = `
            <div class="error-container">
                <div class="error-icon">⚠️</div>
                <p>加载能力库失败</p>
                <small>${error.message}</small>
            </div>
        `;
    }
};

CardEditor.prototype.renderExistingAbilitiesList = function(abilities) {
    const existingAbilitiesList = document.getElementById('existing-abilities-list');
    if (!existingAbilitiesList) return;

    if (!Array.isArray(abilities) || abilities.length === 0) {
        existingAbilitiesList.innerHTML = `
            <div class="empty-state">
                <p>暂无可用能力</p>
                <small>请先创建一些能力</small>
            </div>
        `;
        return;
    }

    // 生成现有能力列表HTML
    const abilitiesHTML = abilities.map(ability => {
        const keywordText = formatAbilityKeywordsDisplay(ability);
        return `
        <div class="existing-ability-item" data-ability-id="${ability.id}">
            <div class="ability-info">
                <div class="ability-name">${ability.name || '未命名能力'}</div>
                ${keywordText ? `<div class="ability-tag">${keywordText}</div>` : ''}
                <div class="ability-description">${ability.description || '暂无描述'}</div>
            </div>
            <button class="btn-secondary select-ability-btn" data-ability-id="${ability.id}">选择</button>
        </div>
    `;
    }).join('');

    existingAbilitiesList.innerHTML = abilitiesHTML;

    // 绑定选择按钮事件
    const selectButtons = existingAbilitiesList.querySelectorAll('.select-ability-btn');
    selectButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const abilityId = btn.dataset.abilityId;
            this.selectExistingAbility(abilityId);
        });
    });
};

CardEditor.prototype.selectExistingAbility = function(abilityId) {
    // 这里可以添加选中状态的视觉反馈
    const existingAbilitiesList = document.getElementById('existing-abilities-list');
    if (existingAbilitiesList) {
        // 移除之前的选中状态
        existingAbilitiesList.querySelectorAll('.existing-ability-item').forEach(item => {
            item.classList.remove('selected');
        });

        // 添加当前选中状态
        const selectedItem = existingAbilitiesList.querySelector(`[data-ability-id="${abilityId}"]`);
        if (selectedItem) {
            selectedItem.classList.add('selected');
        }
    }

    // 存储选中的能力ID
    this.selectedExistingAbilityId = abilityId;
};

CardEditor.prototype.confirmAddAbility = async function() {
    const modal = document.getElementById('add-ability-modal');
    if (!modal) return;

    const activeTab = modal.querySelector('.modal-tab-btn.active');
    if (!activeTab) return;

    const tabType = activeTab.dataset.tab;

    try {
        if (tabType === 'create') {
            await this.createNewAbility();
        } else if (tabType === 'existing') {
            await this.linkExistingAbility();
        }

        // 关闭模态框
        this.closeAddAbilityModal();

        // 刷新能力列表
        await this.renderAbilitiesList();

    } catch (error) {
        console.error('CardEditor: Failed to add ability:', error);
        showAlert(`添加能力失败: ${error.message}`);
    }
};

CardEditor.prototype.createNewAbility = async function() {
    const modal = document.getElementById('add-ability-modal');
    if (!modal) return;
    const currentCardId = this.getCurrentCardId();

    // 获取表单数据
    const name = modal.querySelector('#ability-name').value.trim();
    const description = modal.querySelector('#ability-description').value.trim();
    const abilityTag = modal.querySelector('#ability-tag').value;
    const abilityType = modal.querySelector('#ability-type').value;
    const priority = parseInt(modal.querySelector('#ability-priority').value) || 0;

    // 验证必填字段
    if (!name) {
        showAlert('请输入能力名称');
        return;
    }

    if (!abilityTag) {
        showAlert('请选择能力标签');
        return;
    }

    // 构建能力配置
    const abilityConfig = {
        cardId: currentCardId,
        name: name,
        description: description,
        abilityKeywords: [{ keyword: abilityTag }],
        abilityType: abilityType ? parseInt(abilityType) : 1, // 默认为Triggered
        priority: priority,
        isActive: true,
        jsonConfig: JSON.stringify({
            name: name,
            description: description,
            abilityKeywords: [{ keyword: abilityTag }],
            type: abilityType || "Triggered",
            trigger: {
                eventTypes: ["CardPlayed"],
                timing: "On",
                validZones: []
            },
            conditions: [],
            targets: {},
            effects: [],
            broadcastOnTrigger: true,
            buffDisplay: true
        })
    };


    // 调用后端API创建能力
    const response = await fetch(`/api/abilities${this.getScopeQueryParam()}`, {
        method: 'POST',
        headers: buildEditorIdentityHeaders({
            'Content-Type': 'application/json'
        }),
        body: JSON.stringify(abilityConfig)
    });

    const result = await this.readAbilityWriteResponse(response);
    this.applyDataRevisionFromResponse(response, {
        reason: 'ability-created',
        changedCardIds: currentCardId != null ? [currentCardId] : []
    });
    this.applyCardEditMetadata(result);
    this.updateEditorHeader(`编辑卡牌: ${this.currentCard?.name || ''}`);
    this.invalidateAbilityLibraryCache();
    this.updateAbilityCachesAfterSave(result, currentCardId);
};

CardEditor.prototype.linkExistingAbility = async function() {
    if (!this.selectedExistingAbilityId) {
        showAlert('请选择一个现有能力');
        return;
    }

    const result = await this.copyExistingAbilityById(this.selectedExistingAbilityId);

    // 清除选中状态
    this.selectedExistingAbilityId = null;
};

CardEditor.prototype.copyExistingAbilityById = async function(abilityId) {
    const currentCardId = this.getCurrentCardId();
    if (!this.currentCard || currentCardId == null) {
        throw new Error('请先选择并保存一张卡牌');
    }

    const abilityKey = this.getAbilityIdKey(abilityId);
    let ability = abilityKey ? this.abilityDetailCache.get(abilityKey) : null;

    if (!ability) {
        const abilityResponse = await fetch(`/api/abilities/${abilityId}${this.getScopeQueryParam()}`);
        if (!abilityResponse.ok) {
            throw new Error(`HTTP ${abilityResponse.status}: ${abilityResponse.statusText}`);
        }
        this.applyDataRevisionFromResponse(abilityResponse);
        ability = await abilityResponse.json();
        if (abilityKey) {
            this.abilityDetailCache.set(abilityKey, ability);
        }
    }

    const abilityData = {
        cardId: currentCardId,
        name: ability.name,
        description: ability.description,
        abilityKeywords: ability.abilityKeywords || normalizeAbilityKeywords(ability),
        abilityType: ability.abilityType,
        priority: ability.priority,
        isActive: ability.isActive,
        jsonConfig: ability.jsonConfig
    };

    const createRes = await fetch(`/api/abilities${this.getScopeQueryParam()}`, {
        method: 'POST',
        headers: buildEditorIdentityHeaders({
            'Content-Type': 'application/json'
        }),
        body: JSON.stringify(abilityData)
    });

    const created = await this.readAbilityWriteResponse(createRes);
    this.applyDataRevisionFromResponse(createRes, {
        reason: 'ability-created',
        changedCardIds: currentCardId != null ? [currentCardId] : []
    });
    this.applyCardEditMetadata(created);
    this.updateEditorHeader(`编辑卡牌: ${this.currentCard?.name || ''}`);
    this.invalidateAbilityLibraryCache();
    this.updateAbilityCachesAfterSave(created, currentCardId);
    return created;
};

CardEditor.prototype.editAbility = async function(abilityId) {

    try {
        const abilityKey = this.getAbilityIdKey(abilityId);
        let ability = abilityKey ? this.abilityDetailCache.get(abilityKey) : null;

        if (!ability) {
            // 获取能力详细信息
            const response = await fetch(`/api/abilities/${abilityId}${this.getScopeQueryParam()}`);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            this.applyDataRevisionFromResponse(response);

            ability = await response.json();
            if (abilityKey) {
                this.abilityDetailCache.set(abilityKey, ability);
            }
        } else {
        }

        // 显示能力编辑模态框
        this.showAbilityEditModal(ability);

    } catch (error) {
        console.error('CardEditor: Failed to load ability for editing:', error);
        showAlert(`加载能力失败: ${error.message}`);
    }
};

CardEditor.prototype.removeAbility = async function(abilityId) {
    if (!await confirmDialog('确定要移除这个能力吗？')) {
        return;
    }

    try {

        const editorRoot = this.container?.querySelector('.card-editor-layout') || this.container;
        const loader = editorRoot ? getBlockingLoader(editorRoot) : null;
        loader?.show('正在移除能力...');

        try {
            const currentCardId = this.getCurrentCardId();
            // 调用后端API移除能力关联
            const response = await fetch(`/api/cards/${currentCardId}/abilities/${abilityId}${this.getScopeQueryParam()}`, {
                method: 'DELETE',
                headers: buildEditorIdentityHeaders()
            });

            const result = await this.readAbilityWriteResponse(response);
            this.applyDataRevisionFromResponse(response, {
                reason: 'ability-unlinked',
                changedCardIds: currentCardId != null ? [currentCardId] : []
            });
            this.applyCardEditMetadata(result);
            this.updateEditorHeader(`编辑卡牌: ${this.currentCard?.name || ''}`);

            this.removeAbilityFromCaches(abilityId, currentCardId);
            this.invalidateAbilityLibraryCache();

            // 刷新能力列表
            await this.renderAbilitiesList();
        } finally {
            loader?.hide();
        }
    } catch (error) {
        console.error('CardEditor: Failed to remove ability:', error);
        showAlert(`移除能力失败: ${error.message}`);
    }
};

CardEditor.prototype.showAbilityEditModal = async function(ability) {

    try {
        // 解析能力配置
        const abilityConfig = this.parseAbilityConfig(ability);

        // 创建简化的模态框HTML
        const modalHTML = `
            <div class="modal-overlay" id="ability-edit-modal">
                <div class="modal-container ability-edit-container">
                    <div class="modal-header">
                        <h3>编辑能力: ${ability.name}</h3>
                        <button class="modal-close-btn" id="close-ability-edit-modal">×</button>
                    </div>
                    <div class="modal-content" id="ability-editor-container">
                        <!-- 能力编辑器将在这里渲染 -->
                    </div>
                </div>
            </div>
        `;

        // 添加模态框到页面
        document.body.insertAdjacentHTML('beforeend', modalHTML);

        // 创建并渲染新的能力编辑器
        const editorContainer = document.getElementById('ability-editor-container');
        const { AbilityEditor } = await import('./components/AbilityEditor.js');
        const abilityEditor = new AbilityEditor();

        await abilityEditor.render(
            editorContainer,
            abilityConfig,
            async (updatedConfig) => {
                if (updatedConfig === null) {
                    // 用户取消编辑
                    this.closeAbilityEditModal();
                } else {
                    // 用户保存编辑
                    await this.saveAbilityChanges(ability, updatedConfig);
                    this.closeAbilityEditModal();
                }
            }
        );

        // 绑定关闭事件
        this.bindAbilityEditModalCloseEvents();

    } catch (error) {
        console.error('CardEditor: Failed to show ability edit modal:', error);
        showAlert(`打开能力编辑器失败: ${error.message}`);
    }
};

/**
 * 解析能力配置
 */
CardEditor.prototype.parseAbilityConfig = function(ability) {
    let config = {
        id: ability.id,
        name: ability.name || '',
        description: ability.description || '',
        cardName: this.currentCard?.name || '',
        abilityKeywords: normalizeAbilityKeywords(ability),
        abilityType: ability.abilityType || 1,
        priority: ability.priority || 0,
        isActive: ability.isActive !== false,
        broadcastOnTrigger: true,
        buffDisplay: true,
        type: ability.type || ability.Type || 'Triggered',
        trigger: {
            eventTypes: [],
            relationType: '',
            timing: 'On',
            validZones: []
        },
        conditions: [],
        targets: [],
        effects: [],
        auras: [],
        counterRegistrations: [],
        playerCounterRegistrations: []
    };

    // 尝试解析JSON配置
    if (ability.jsonConfig) {
        try {
            let jsonConfig;
            if (typeof ability.jsonConfig === 'string') {
                jsonConfig = JSON.parse(ability.jsonConfig);
            } else {
                jsonConfig = ability.jsonConfig;
            }

            if (jsonConfig.trigger) {
                config.trigger = { ...config.trigger, ...jsonConfig.trigger };
            }
            if (jsonConfig.abilityKeywords) {
                config.abilityKeywords = normalizeAbilityKeywords({ abilityKeywords: jsonConfig.abilityKeywords });
            }
            const normalizedRefs = normalizeAbilityTargetsAndRefs(jsonConfig);
            if (jsonConfig.conditions) {
                config.conditions = normalizedRefs.conditions;
            }
            if (jsonConfig.targets) {
                config.targets = normalizedRefs.targets;
            }
            if (jsonConfig.effects) {
                config.effects = normalizedRefs.effects;
            }
            if (jsonConfig.auras) {
                config.auras = jsonConfig.auras;
            }
            if (jsonConfig.type) {
                config.type = jsonConfig.type;
            } else if (jsonConfig.executionKind && String(jsonConfig.executionKind).toLowerCase() === 'aura') {
                config.type = 'Continuous';
            }
            if (jsonConfig.counterRegistrations) {
                config.counterRegistrations = jsonConfig.counterRegistrations;
            }
            if (jsonConfig.playerCounterRegistrations) {
                config.playerCounterRegistrations = jsonConfig.playerCounterRegistrations;
            }
            const broadcastOnTrigger = jsonConfig.broadcastOnTrigger ?? jsonConfig.BroadcastOnTrigger;
            if (broadcastOnTrigger !== undefined) {
                config.broadcastOnTrigger = broadcastOnTrigger !== false;
            }
            const buffDisplay = jsonConfig.buffDisplay ?? jsonConfig.BuffDisplay;
            if (buffDisplay !== undefined) {
                config.buffDisplay = buffDisplay !== false;
            } else if (broadcastOnTrigger !== undefined) {
                config.buffDisplay = broadcastOnTrigger !== false;
            }
        } catch (error) {
            console.warn('CardEditor: Failed to parse ability JSON config:', error);
        }
    }

    return config;
};

/**
 * 绑定能力编辑模态框关闭事件
 */
CardEditor.prototype.bindAbilityEditModalCloseEvents = function() {
    const modal = document.getElementById('ability-edit-modal');
    if (!modal) return;

    // 关闭按钮事件
    const closeBtn = modal.querySelector('#close-ability-edit-modal');
    if (closeBtn) {
        closeBtn.addEventListener('click', () => {
            this.closeAbilityEditModal();
        });
    }

    // ESC键关闭
    const handleEscape = (e) => {
        if (e.key === 'Escape') {
            this.closeAbilityEditModal();
            document.removeEventListener('keydown', handleEscape);
        }
    };
    document.addEventListener('keydown', handleEscape);
};

/**
 * 关闭能力编辑模态框
 */
CardEditor.prototype.closeAbilityEditModal = function() {
    const modal = document.getElementById('ability-edit-modal');
    if (modal) {
        modal.remove();
    }
};

/**
 * 保存能力修改
 */
CardEditor.prototype.saveAbilityChanges = async function(originalAbility, updatedConfig) {
    try {

        // 调用后端API更新能力
        const response = await fetch(`/api/abilities/${originalAbility.id}${this.getScopeQueryParam()}`, {
            method: 'PUT',
            headers: buildEditorIdentityHeaders({
                'Content-Type': 'application/json'
            }),
            body: JSON.stringify(updatedConfig)
        });

        const result = await this.readAbilityWriteResponse(response);
        this.applyDataRevisionFromResponse(response, {
            reason: 'ability-updated',
            changedCardIds: [this.getCurrentCardId() ?? result?.cardId ?? originalAbility?.cardId].filter(value => value != null)
        });
        this.applyCardEditMetadata(result);
        this.updateEditorHeader(`编辑卡牌: ${this.currentCard?.name || ''}`);

        const cardId = this.getCurrentCardId() ?? result?.cardId ?? originalAbility?.cardId;
        this.invalidateAbilityLibraryCache();
        this.updateAbilityCachesAfterSave({
            ...originalAbility,
            ...updatedConfig,
            ...result,
            id: result?.id ?? originalAbility?.id,
            cardId: result?.cardId ?? cardId
        }, cardId);

        // 刷新能力列表
        await this.renderAbilitiesList();

        showAlert('能力保存成功！');

    } catch (error) {
        console.error('CardEditor: Failed to save ability changes:', error);
        showAlert(`保存能力失败: ${error.message}`);
        throw error;
    }
};

// 加载关键字选项
CardEditor.prototype.loadKeywordOptions = async function(selectElement) {
    try {
        const keywords = await this.metadataManager.getEnum('CardKeyword');
        this.keywordLabelMap = keywords;

        // 清空现有选项（保留默认选项）
        selectElement.innerHTML = '<option value="">选择关键字</option>';

        // 添加关键字选项
        Object.entries(keywords).forEach(([key, value]) => {
            if (key !== 'None') { // 跳过 None 选项
                const option = document.createElement('option');
                option.value = key;
                option.textContent = value;
                selectElement.appendChild(option);
            }
        });
    } catch (error) {
        console.error('Failed to load keyword options:', error);
        // 如果加载失败，提供基本的关键字选项
        const basicKeywords = {
            'Storm': '疾走',
            'Ward': '守护',
            'Bane': '必杀',
            'Rush': '突进',
            'Ambush': '潜行',
            'Drain': '吸血'
        };
        this.keywordLabelMap = basicKeywords;

        selectElement.innerHTML = '<option value="">选择关键字</option>';
        Object.entries(basicKeywords).forEach(([key, value]) => {
            const option = document.createElement('option');
            option.value = key;
            option.textContent = value;
            selectElement.appendChild(option);
        });
    }
};

/**
 * 3.3 自然语言/卡面描述转 DSL 能力配置 (Copilot)
 */
CardEditor.prototype.isAutoGeneratedCompilerAbility = function(ability) {
    if (!ability) return false;
    try {
        const cfg = typeof ability.jsonConfig === 'string'
            ? JSON.parse(ability.jsonConfig)
            : ability.jsonConfig;
        return Boolean(cfg && cfg.generatedBy === 'mse-compiler');
    } catch (_) {
        return false;
    }
};

CardEditor.prototype.generateAbilitiesFromVisualCard = async function() {
    if (this._compilePending) return;
    if (!this.currentCard) {
        showAlert('请先选择或新建一张卡牌');
        return;
    }

    this._compilePending = true;
    const editorRoot = this.container?.querySelector('.card-editor-layout') || this.container;
    const loader = editorRoot ? getBlockingLoader(editorRoot) : null;

    try {
        const initialDraftKey = this.currentCard._draftKey
            || (this.currentCard.id != null ? String(this.currentCard.id) : null);
        const visualCanvas = !this.useClassicBasicForm
            && initialDraftKey
            && String(this.visualCanvas?.mseCard?.editorId) === initialDraftKey
            ? this.visualCanvas
            : null;
        visualCanvas?.finishInline();
        visualCanvas?.syncToEditorCard();

        const rawDesc = (this.currentCard.description || '').trim();
        let sections = visualCanvas?.mseCard?.presentation?.sections || this.currentCard.presentation?.sections;
        if (!Array.isArray(sections) && this.currentCard.id != null) {
            const savedMeta = JSON.parse(
                localStorage.getItem('svwb-mse-card-meta:' + this.currentCard.id)
                || localStorage.getItem('sv_mse_presentation_v1_' + this.currentCard.id)
                || 'null'
            );
            sections = savedMeta?.sections;
        }
        const isStandaloneCrestOrFaith = equalsIgnoreCase(normalizeEnumName(this.currentCard.cardType), 'Crest')
            || equalsIgnoreCase(normalizeEnumName(this.currentCard.cardType), 'Faith');
        const sectionsPayload = Array.isArray(sections)
            ? structuredClone(sections.filter(section => {
                if (!String(section.text ?? '').trim().length) return false;
                if (!isStandaloneCrestOrFaith && (section.style === '纹章' || section.style === '信仰')) {
                    return false;
                }
                return true;
            }))
            : [];

        if (!rawDesc && sectionsPayload.length === 0) {
            showAlert('当前卡面暂无规则描述文本，请先直接在卡面规则区点击输入效果描述（例如：【入场曲】抽2张牌。对敌方主战者造成3点伤害。）');
            return;
        }

        if ((this.currentCard.isNew || this.getCurrentCardId() == null) && (!this.currentCard.name || !this.currentCard.name.trim())) {
            this.currentCard.name = '未命名新卡';
        }

        const targetCardName = this.currentCard.name || '';
        const targetCardSnapshot = {
            cardType: this.currentCard.cardType || 'Follower',
            cardClass: this.currentCard.cardClass || 'Neutral',
            cost: Number(this.currentCard.cost ?? 0),
            description: this.currentCard.description || ''
        };

        // 若是尚未保存的新卡，先自动保存以分配数据库 CardId（捕获真实 savedId，防止等待期间切卡错绑）
        let targetCardId = this.getCurrentCardId();
        if (this.currentCard.isNew || targetCardId == null) {
            const saveRes = await this.saveCurrentCard(false, { silent: true, returnResult: true });
            if (!saveRes?.ok || saveRes.savedId == null) {
                return;
            }
            targetCardId = saveRes.savedId;
        }

        loader?.show('正在编译卡面规则并同步能力配置...');

        const [compileResp, existingAbilitiesResp] = await Promise.all([
            fetch('/api/dsl/generate-from-card', {
                method: 'POST',
                headers: buildEditorIdentityHeaders({ 'Content-Type': 'application/json' }),
                body: JSON.stringify({
                    cardId: targetCardId,
                    name: targetCardName,
                    cardType: targetCardSnapshot.cardType,
                    cardClass: targetCardSnapshot.cardClass,
                    cost: targetCardSnapshot.cost,
                    description: targetCardSnapshot.description,
                    sections: sectionsPayload
                })
            }),
            fetch(`/api/cards/${targetCardId}/abilities${this.getScopeQueryParam()}`)
        ]);

        if (!compileResp.ok) {
            const errText = await compileResp.text();
            throw new Error(errText || `HTTP ${compileResp.status}`);
        }
        if (!existingAbilitiesResp.ok) {
            const errText = await readErrorResponseMessage(existingAbilitiesResp);
            throw new Error(`无法读取现有能力列表，已中止编译以防重复追加: ${errText || `HTTP ${existingAbilitiesResp.status}`}`);
        }

        const result = await compileResp.json();
        const existingAbilities = await existingAbilitiesResp.json();
        const previousAutoAbilities = (Array.isArray(existingAbilities) ? existingAbilities : [])
            .filter(ab => this.isAutoGeneratedCompilerAbility(ab));

        const detectedKeywords = Array.isArray(result.detectedKeywords)
            ? [...new Set(result.detectedKeywords.filter(Boolean))]
            : [];
        const unrecognizedClauses = Array.isArray(result.unrecognizedClauses)
            ? result.unrecognizedClauses.filter(Boolean)
            : [];

        // 过滤掉无任何实际效果节点的空壳草稿，绝不写入伪激活空能力
        const rawDrafts = Array.isArray(result.abilities) ? result.abilities : [];
        const validDrafts = [];
        for (const item of rawDrafts) {
            if (item.hasEffects === false) continue;
            let parsedCfg = null;
            if (typeof item.jsonConfig === 'string' && item.jsonConfig.trim()) {
                try {
                    parsedCfg = JSON.parse(item.jsonConfig);
                } catch (_) {
                    parsedCfg = null;
                }
            }
            if (!parsedCfg || !Array.isArray(parsedCfg.effects) || parsedCfg.effects.length === 0) {
                continue;
            }
            const normalizedRefs = normalizeAbilityTargetsAndRefs(parsedCfg);
            parsedCfg.targets = normalizedRefs.targets;
            parsedCfg.conditions = normalizedRefs.conditions;
            parsedCfg.effects = normalizedRefs.effects;
            parsedCfg.generatedBy = 'mse-compiler';
            validDrafts.push({
                ...item,
                normalizedJsonConfig: JSON.stringify(parsedCfg)
            });
        }

        if (validDrafts.length === 0 && detectedKeywords.length === 0) {
            const detailMsg = unrecognizedClauses.length > 0
                ? `\n未自动识别的描述：\n- ${unrecognizedClauses.join('\n- ')}\n请前往「能力管理 -> 新增能力」使用向导配置复杂效果。`
                : '';
            showAlert(`未从当前描述中识别出可自动编译的效果或关键字，未修改实装状态。${detailMsg}`);
            return;
        }

        // 先创建本轮新编译的能力，若中途失败则回滚半批新能力，保留旧能力完好无损
        const newlyCreatedIds = [];
        try {
            for (const item of validDrafts) {
                const createResp = await fetch(`/api/abilities${this.getScopeQueryParam()}`, {
                    method: 'POST',
                    headers: buildEditorIdentityHeaders({ 'Content-Type': 'application/json' }),
                    body: JSON.stringify({
                        cardId: targetCardId,
                        name: item.name || `${targetCardName}_能力`,
                        description: item.description || '',
                        abilityKeywords: Array.isArray(item.abilityKeywords) ? item.abilityKeywords : [],
                        abilityType: item.abilityType || 1,
                        priority: item.priority || 0,
                        isActive: item.isActive !== false,
                        jsonConfig: item.normalizedJsonConfig
                    })
                });

                if (!createResp.ok) throw new Error(await readErrorResponseMessage(createResp));
                const createdAbility = await this.readAbilityWriteResponse(createResp);
                const newId = createdAbility?.id ?? createdAbility?.Id;
                if (newId != null) newlyCreatedIds.push(newId);
                this.applyDataRevisionFromResponse(createResp, {
                    reason: 'ability-created',
                    changedCardIds: targetCardId != null ? [targetCardId] : []
                });
                this.updateAbilityCachesAfterSave(createdAbility, targetCardId);
            }
        } catch (createErr) {
            const rollbackFailures = [];
            for (const rollbackId of newlyCreatedIds) {
                try {
                    const rbResp = await fetch(`/api/cards/${targetCardId}/abilities/${rollbackId}${this.getScopeQueryParam()}`, {
                        method: 'DELETE',
                        headers: buildEditorIdentityHeaders()
                    });
                    if (rbResp.ok || rbResp.status === 404) {
                        this.removeAbilityFromCaches(rollbackId, targetCardId);
                    } else {
                        rollbackFailures.push(rollbackId);
                    }
                } catch (_) {
                    rollbackFailures.push(rollbackId);
                }
            }
            if (rollbackFailures.length > 0) {
                throw new Error(`${createErr.message}（且回滚新建能力 [${rollbackFailures.join(', ')}] 失败，请在能力管理中检查清理）`);
            }
            throw createErr;
        }

        // 新能力全部落库后，清理上一次自动编译生成的旧能力（仅限 generatedBy === 'mse-compiler'，保留手工能力）
        const failedDeleteIds = [];
        for (const oldAuto of previousAutoAbilities) {
            const oldId = oldAuto?.id ?? oldAuto?.Id;
            if (oldId == null || newlyCreatedIds.includes(oldId)) continue;
            const delResp = await fetch(`/api/cards/${targetCardId}/abilities/${oldId}${this.getScopeQueryParam()}`, {
                method: 'DELETE',
                headers: buildEditorIdentityHeaders()
            });
            if (delResp.ok || delResp.status === 404) {
                this.removeAbilityFromCaches(oldId, targetCardId);
            } else {
                failedDeleteIds.push(oldId);
            }
        }

        this.invalidateAbilityLibraryCache();
        this.invalidateAbilitiesForCard(targetCardId);

        if (failedDeleteIds.length > 0) {
            throw new Error(`新能力已创建，但删除旧自动编译能力 [${failedDeleteIds.join(', ')}] 失败，请前往能力管理清理重复项。`);
        }

        const fullyCompiled = unrecognizedClauses.length === 0
            && result.fullyCompiled !== false
            && validDrafts.every(d => d.dslValid !== false && d.isActive !== false);

        // 校验当前编辑器是否仍停留在发起编译的那张卡牌上，避免切卡串写状态
        const isStillSameCard = Boolean(
            this.currentCard
            && this.getCurrentCardId() != null
            && this.getCardIdKey(this.getCurrentCardId()) === this.getCardIdKey(targetCardId)
        );

        if (isStillSameCard) {
            const hasCountdown = (this.currentCard.keywords ?? []).some(keyword => equalsIgnoreCase(keyword, 'Countdown'));
            const keywords = hasCountdown && !detectedKeywords.includes('Countdown')
                ? [...detectedKeywords, 'Countdown']
                : detectedKeywords;
            const initialCountdown = result.initialCountdown;
            this.currentCard.keywords = keywords;
            if (Number.isInteger(initialCountdown)) this.currentCard.countdown = initialCountdown;
            this.currentCard.isImplemented = fullyCompiled;
            const liveCanvas = !this.useClassicBasicForm
                && String(this.visualCanvas?.mseCard?.editorId) === String(targetCardId)
                ? this.visualCanvas
                : null;
            if (liveCanvas) {
                liveCanvas.mseCard.keywords = [...keywords];
                liveCanvas.mseCard.countdown = this.currentCard.countdown;
                liveCanvas.mseCard.isImplemented = fullyCompiled;
                liveCanvas.renderKeywordsList();
                liveCanvas.syncToEditorCard();
            }
            this.updateCardPreview();
            if (!await this.saveCurrentCard(false, { silent: true })) {
                throw new Error('能力已更新，但卡牌属性保存未完成，请手动点击保存。');
            }
            if (liveCanvas) await liveCanvas.paint();

            this.switchTab('abilities');
            await this.renderAbilitiesList({ force: true });
        }

        const notesSummary = validDrafts
            .map(a => Array.isArray(a.notes) ? a.notes.join('；') : (a.notes || ''))
            .filter(Boolean)
            .slice(0, 4)
            .join('；');

        if (!isStillSameCard) {
            showAlert(`原卡牌（#${targetCardId}）的 ${newlyCreatedIds.length} 条能力已编译写入；因编译期间已切换卡牌，原卡关键字与实装标记请切回后确认保存。`);
        } else if (!fullyCompiled && unrecognizedClauses.length > 0) {
            showAlert(
                `已部分编译并写入 ${newlyCreatedIds.length} 条能力草稿（已替换旧自动编译项），但仍有未识别规则，当前保持「待实装」状态：\n- ${unrecognizedClauses.join('\n- ')}\n请在能力管理中补全并启用后再勾选「已实装」。`
            );
        } else {
            showAlert(newlyCreatedIds.length > 0
                ? `已编译并实装，生成 ${newlyCreatedIds.length} 条 DSL 能力配置（自动替换旧编译项 ${previousAutoAbilities.length} 条）。${notesSummary ? `\n(${notesSummary})` : ''}`
                : `已编译并实装，保存关键字：${detectedKeywords.join('、')}（无需额外触发能力配置）。`);
        }
    } catch (error) {
        console.error('CardEditor: Failed to generate abilities from card text:', error);
        showAlert(`生成 DSL 能力失败: ${error.message}`);
    } finally {
        loader?.hide();
        this._compilePending = false;
    }
};

/**
 * 从普通卡牌深拷贝导入为独立幻境卡牌（无两栖：分配新ID、重写关联衍生卡与能力引用、物理隔离卡面资源）
 */
CardEditor.prototype.showImportClassicCardModal = async function() {
    if (!this.isMirageScope()) {
        showAlert('仅幻境卡牌工坊支持从普通卡导入。');
        return;
    }

    this.closeImportClassicCardModal();

    const modalHTML = `
        <div class="modal-overlay" id="import-classic-card-modal">
            <div class="modal-container ability-edit-container ability-copy-container">
                <div class="modal-header">
                    <h3>从普通卡导入到幻境卡池（深拷贝隔离）</h3>
                    <button class="modal-close-btn" id="close-import-classic-card-modal" type="button">×</button>
                </div>
                <div class="modal-content ability-copy-modal">
                    <div class="ability-copy-filters">
                        <div class="ability-copy-search">
                            <label class="form-label" for="import-classic-card-search">筛选普通卡</label>
                            <input class="tech-input" type="search" id="import-classic-card-search" placeholder="搜索普通卡名称、ID、职业或费用">
                        </div>
                        <div class="ability-copy-stats tech-text-dim" id="import-classic-card-stats">普通卡 0</div>
                    </div>
                    <div class="form-grid form-grid-2" style="margin-bottom: 10px;">
                        <div class="form-group">
                            <label class="form-label" for="import-classic-card-tier">导入后费用 / 星级 (★7+ 为特殊奖励卡)</label>
                            <input type="number" id="import-classic-card-tier" class="form-input tech-input" min="1" max="99" value="1">
                        </div>
                        <div class="form-group" style="display:flex; align-items:flex-end; padding-bottom: 6px;">
                            <label class="form-toggle">
                                <input type="checkbox" id="import-classic-card-abilities" checked>
                                <span class="form-toggle-text">深拷贝关联能力与衍生卡（自动重写ID与卡面副本）</span>
                            </label>
                        </div>
                    </div>
                    <div class="tech-text-dim ability-copy-hint">选中普通卡后点击“确认导入”，将生成全新幻境专属卡牌ID，后续修改绝不影响经典对战</div>
                    <div class="ability-copy-list custom-scrollbar" id="import-classic-card-list">
                        <div class="loading-spinner"></div>
                        <p>正在加载普通卡牌目录...</p>
                    </div>
                    <div class="ability-copy-pagination">
                        <button class="btn-tech btn-secondary btn-small" id="import-classic-card-prev" type="button" disabled>上一页</button>
                        <div class="ability-copy-page-info tech-text-dim" id="import-classic-card-page-info">1 / 1</div>
                        <button class="btn-tech btn-secondary btn-small" id="import-classic-card-next" type="button" disabled>下一页</button>
                    </div>
                    <div class="modal-actions ability-copy-actions">
                        <button class="btn-tech btn-secondary" id="cancel-import-classic-card" type="button">取消</button>
                        <button class="btn-tech btn-primary" id="confirm-import-classic-card" type="button" disabled>确认导入为幻境卡</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHTML);

    const modal = document.getElementById('import-classic-card-modal');
    const modalContainer = modal?.querySelector('.ability-copy-container');
    const modalLoader = modalContainer ? getBlockingLoader(modalContainer) : null;
    const closeBtn = document.getElementById('close-import-classic-card-modal');
    const cancelBtn = document.getElementById('cancel-import-classic-card');
    const confirmBtn = document.getElementById('confirm-import-classic-card');
    const listEl = document.getElementById('import-classic-card-list');
    const searchInput = document.getElementById('import-classic-card-search');
    const statsEl = document.getElementById('import-classic-card-stats');
    const pageInfoEl = document.getElementById('import-classic-card-page-info');
    const prevBtn = document.getElementById('import-classic-card-prev');
    const nextBtn = document.getElementById('import-classic-card-next');
    const tierSelect = document.getElementById('import-classic-card-tier');
    const includeAbilitiesCheckbox = document.getElementById('import-classic-card-abilities');

    const close = () => {
        modalLoader?.hide();
        this.closeImportClassicCardModal();
    };

    closeBtn?.addEventListener('click', close);
    cancelBtn?.addEventListener('click', close);
    modal?.addEventListener('click', (event) => {
        if (event.target === modal) close();
    });

    let selectedSourceCardId = null;
    const pageSize = 15;
    let currentPage = 1;
    let allEntries = [];
    let filteredEntries = [];

    try {
        const response = await fetch('/api/cards?scope=standard');
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        const rawCards = await response.json();
        const classicCards = (Array.isArray(rawCards) ? rawCards : []).filter(card => {
            const id = Number(card?.id ?? card?.Id);
            const isMirage = Boolean(card?.isMirageCard ?? card?.IsMirageCard);
            return Number.isInteger(id) && id > 0 && !isMirage;
        });

        allEntries = classicCards.map(card => {
            const cardId = Number(card.id ?? card.Id);
            const cardName = String(card.name ?? card.Name ?? `Card ${cardId}`).trim() || `Card ${cardId}`;
            const cardCost = Number(card.cost ?? card.Cost ?? 0) || 0;
            const cardType = normalizeEnumName(card.type ?? card.Type ?? 'Follower');
            const cardClass = normalizeEnumName(card.class ?? card.Class ?? 'Neutral');
            const cardSet = String(card.cardSet ?? card.CardSet ?? '').trim();
            const attack = Number(card.attack ?? card.Attack ?? 0) || 0;
            const health = Number(card.health ?? card.Health ?? 0) || 0;
            const searchText = [cardName, cardId, cardCost, cardType, cardClass, cardSet].join(' ').toLowerCase();
            return {
                cardId,
                cardName,
                cardCost,
                cardType,
                cardClass,
                cardSet,
                attack,
                health,
                searchText
            };
        }).sort((a, b) => a.cardId - b.cardId);
    } catch (error) {
        console.error('CardEditor: Failed to load classic cards for import:', error);
        if (listEl) {
            listEl.innerHTML = `<div class="abilities-placeholder"><p>加载普通卡牌列表失败：${escapeHtml(error.message)}</p></div>`;
        }
        return;
    }

    const renderList = () => {
        const totalPages = Math.max(1, Math.ceil(filteredEntries.length / pageSize));
        currentPage = Math.min(currentPage, totalPages);

        if (statsEl) {
            statsEl.textContent = `普通卡 ${filteredEntries.length}`;
        }
        if (pageInfoEl) {
            pageInfoEl.textContent = `${currentPage} / ${totalPages}`;
        }
        if (prevBtn) prevBtn.disabled = currentPage <= 1;
        if (nextBtn) nextBtn.disabled = currentPage >= totalPages;

        if (!listEl) return;
        if (filteredEntries.length === 0) {
            listEl.innerHTML = `<div class="abilities-placeholder"><p>没有匹配的普通卡牌</p></div>`;
            return;
        }

        const start = (currentPage - 1) * pageSize;
        const pageItems = filteredEntries.slice(start, start + pageSize);

        listEl.innerHTML = pageItems.map(entry => {
            const isSelected = selectedSourceCardId === entry.cardId;
            const statBadge = equalsIgnoreCase(entry.cardType, 'Follower')
                ? `<span class="tech-tag">${entry.attack}/${entry.health}</span>`
                : '';
            return `
                <div class="ability-copy-card${isSelected ? ' expanded' : ''}" data-source-card-id="${entry.cardId}" data-source-cost="${entry.cardCost}">
                    <button type="button" class="ability-copy-card-header" style="${isSelected ? 'border-color: var(--tech-cyan, #00e5ff); background: rgba(0, 229, 255, 0.12);' : ''}">
                        <div class="ability-copy-card-main">
                            <span class="ability-copy-card-name tech-text-bold">${escapeHtml(entry.cardName)}</span>
                            <span class="ability-copy-card-id tech-text-dim">#${entry.cardId}</span>
                        </div>
                        <div class="ability-copy-card-meta">
                            <span class="tech-badge">费用 ${entry.cardCost}</span>
                            ${statBadge}
                            <span class="tech-tag">${escapeHtml(entry.cardType)}</span>
                            <span class="tech-tag">${escapeHtml(entry.cardClass)}</span>
                            ${entry.cardSet ? `<span class="tech-tag">${escapeHtml(entry.cardSet)}</span>` : ''}
                        </div>
                    </button>
                </div>
            `;
        }).join('');

        listEl.querySelectorAll('.ability-copy-card').forEach(cardEl => {
            const sourceId = Number(cardEl.dataset.sourceCardId);
            const sourceCost = Number(cardEl.dataset.sourceCost ?? 1);
            cardEl.querySelector('.ability-copy-card-header')?.addEventListener('click', () => {
                selectedSourceCardId = sourceId;
                if (tierSelect) {
                    const recommendedTier = Math.max(1, sourceCost || 1);
                    tierSelect.value = String(recommendedTier);
                }
                if (confirmBtn) {
                    confirmBtn.disabled = false;
                }
                renderList();
            });
        });
    };

    const applyFilter = (query = '') => {
        const normalized = String(query ?? '').trim().toLowerCase();
        filteredEntries = allEntries.filter(entry => !normalized || entry.searchText.includes(normalized));
        currentPage = 1;
        renderList();
    };

    searchInput?.addEventListener('input', () => applyFilter(searchInput.value));
    prevBtn?.addEventListener('click', () => {
        if (currentPage > 1) {
            currentPage -= 1;
            renderList();
        }
    });
    nextBtn?.addEventListener('click', () => {
        const totalPages = Math.max(1, Math.ceil(filteredEntries.length / pageSize));
        if (currentPage < totalPages) {
            currentPage += 1;
            renderList();
        }
    });

    applyFilter('');

    confirmBtn?.addEventListener('click', async () => {
        if (!selectedSourceCardId) return;
        confirmBtn.disabled = true;
        modalLoader?.show('正在深拷贝导入为幻境卡...');
        try {
            const tavernTier = Math.max(1, parseInt(tierSelect?.value ?? '1', 10) || 1);
            const includeAbilities = includeAbilitiesCheckbox ? includeAbilitiesCheckbox.checked : true;

            const response = await fetch('/api/cards/mirage/import', {
                method: 'POST',
                headers: buildEditorIdentityHeaders({
                    'Content-Type': 'application/json'
                }),
                body: JSON.stringify({
                    sourceCardId: selectedSourceCardId,
                    mirageTavernTier: tavernTier,
                    includeAbilities
                })
            });

            if (!response.ok) {
                const errText = await response.text();
                throw new Error(errText || `HTTP ${response.status}`);
            }

            const result = await response.json();
            const importedCard = result?.card ?? result?.Card;
            const newCardId = importedCard?.id ?? importedCard?.Id;
            const derivedIds = result?.importedDerivedCardIds ?? result?.ImportedDerivedCardIds ?? [];
            const abilityCount = Number(result?.importedAbilityCount ?? result?.ImportedAbilityCount ?? 0);

            this.applyDataRevisionFromResponse(response, {
                reason: 'mirage-card-imported',
                changedCardIds: newCardId != null ? [newCardId, ...derivedIds] : []
            });
            this.invalidateAbilityLibraryCache();

            close();
            await this.loadCardList({ force: true });
            if (newCardId != null) {
                await this.selectCard(newCardId);
            }

            showAlert(`导入成功！已生成独立幻境卡 #${newCardId}「${importedCard?.name ?? importedCard?.Name ?? ''}」（同步克隆衍生卡 ${derivedIds.length} 张、能力 ${abilityCount} 条）。`);
        } catch (error) {
            console.error('CardEditor: Failed to import classic card to mirage:', error);
            showAlert(`导入失败: ${error.message}`);
            confirmBtn.disabled = false;
        } finally {
            modalLoader?.hide();
        }
    });
};

CardEditor.prototype.closeImportClassicCardModal = function() {
    const modal = document.getElementById('import-classic-card-modal');
    if (modal) {
        modal.remove();
    }
};
