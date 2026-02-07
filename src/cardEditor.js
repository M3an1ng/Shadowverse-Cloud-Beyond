// src/cardEditor.js - 卡牌编辑器主模块
//
// 相关文档:
// - UI设计: 文档/06-UI设计/01-卡牌编辑器原型设计.md
// - 元数据驱动UI: 文档/06-UI设计/04-元数据驱动UI架构设计.md
//

import { MetadataManager } from './metadataManager.js';
import { DynamicUIRenderer } from './dynamicUIRenderer.js';
import { AbilityEditor } from './components/AbilityEditor.js';
import { buildPreviewSlateHtml } from './previewSlate.js';
import { showPreviewDescriptionTooltip } from './previewDescriptionTooltip.js';
import { hydrateDescriptionReferences } from './previewDescriptionReferences.js';
import { showCardDetailModal } from './components/CardDetailMockup.js';
import { getBlockingLoader } from './components/blockingLoader.js';
import { ensureEnumLocalizations, getCachedEnumLabel } from './enumLocalizationCache.js';
import { ensureAbilityData, refreshAbilityDataCache } from './abilityDataCache.js';

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

function equalsIgnoreCase(a, b) {
    if (typeof a !== 'string' || typeof b !== 'string') return a === b;
    return a.toLowerCase() === b.toLowerCase();
}

let editorEnumLocalizationPromise = null;
function ensureEditorEnumLocalization(editorInstance) {
    if (!editorEnumLocalizationPromise) {
        editorEnumLocalizationPromise = ensureEnumLocalizations(['CardRarity', 'CardClass', 'CardType'])
            .then(() => {
                if (editorInstance?.currentCard) {
                    editorInstance.updateCardPreview();
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
    if (entry.value !== null && entry.value !== undefined && entry.value !== '') {
        return `${entry.keyword}:${entry.value}`;
    }
    return entry.keyword;
}

const ABILITY_TYPE_LABELS = {
    0: 'Triggered',
    1: 'Continuous',
    2: 'Activated'
};

function resolveAbilityTypeLabel(ability, config) {
    const rawTypeValue = ability?.abilityType ?? ability?.AbilityType ?? config?.abilityType;
    const configType = normalizeEnumName(config?.type ?? ability?.type ?? ability?.Type ?? ability?.jsonConfig?.type ?? '');
    if (rawTypeValue !== null && rawTypeValue !== undefined && rawTypeValue !== '') {
        const numericValue = Number(rawTypeValue);
        if (Number.isFinite(numericValue)) {
            const numericLabel = ABILITY_TYPE_LABELS[numericValue];
            if (configType && numericLabel && configType.toLowerCase() !== numericLabel.toLowerCase()) {
                return configType;
            }
            return numericLabel || configType || 'Triggered';
        }
        if (typeof rawTypeValue === 'string') {
            return normalizeEnumName(rawTypeValue) || configType || 'Triggered';
        }
    }
    return configType || 'Triggered';
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
    }

    /**
     * 初始化卡牌编辑器
     */
    async initialize() {
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

            // 加载卡包定义用于输入补全
            await this.loadCardSetDefinitions();

            // 创建编辑器布局
            this.createEditorLayout();
            this.updateTabAvailability();

            // 初始化列表筛选控件选项
            await this.populateCardListFilterOptions();

            // 绑定事件监听器
            this.bindEventListeners();
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
            <div class="${layoutClass}">
                <div class="editor-grid-bg"></div>
                
                <!-- 左栏：卡牌列表 -->
                <div class="editor-sidebar left-sidebar tech-panel">
                    <div class="sidebar-header">
                        <h3 class="tech-header">DATABASE // CARDS</h3>
                        <button class="btn-tech btn-primary" id="new-card-btn">
                            NEW ENTRY
                        </button>
                    </div>
                    <div class="sidebar-content">
                        <div class="search-section">
                            <input type="text" id="card-search" placeholder="SEARCH DATABASE..." class="search-input tech-input">
                            <div class="filter-section">
                                <select id="class-filter" class="filter-select tech-select">
                                    <option value="">ALL CLASSES</option>
                                </select>
                                <select id="type-filter" class="filter-select tech-select">
                                    <option value="">ALL TYPES</option>
                                </select>
                                <select id="rarity-filter" class="filter-select tech-select">
                                    <option value="">ALL RARITIES</option>
                                </select>
                                <select id="set-filter" class="filter-select tech-select">
                                    <option value="">ALL SETS</option>
                                </select>
                            </div>
                        </div>
                        <div class="card-list custom-scrollbar" id="card-list">
                            <div class="card-list-placeholder">
                                <p class="tech-text-dim">NO DATA FOUND</p>
                                <small class="tech-text-small">INITIATE NEW ENTRY</small>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- 中栏：编辑区 -->
                <div class="editor-main tech-panel">
                    <div class="editor-header">
                        <h3 id="editor-title" class="tech-header-large">AWAITING SELECTION</h3>
                        <div class="editor-actions">
                            <button class="btn-tech btn-secondary" id="save-btn" disabled>SAVE DATA</button>
                            <button class="btn-tech btn-secondary" id="save-as-btn" disabled>SAVE AS</button>
                            <button class="btn-tech btn-danger" id="delete-btn" disabled>DELETE</button>
                        </div>
                    </div>
                    <div class="editor-content">
                        <div class="editor-tabs">
                            <button class="tab-button active" data-tab="basic">BASIC INFO</button>
                            <button class="tab-button" data-tab="abilities">ABILITIES</button>
                            <button class="tab-button" data-tab="assets">ASSETS</button>
                        </div>
                        <div class="tab-content custom-scrollbar">
                            <div class="tab-panel active" id="basic-tab">
                                <div class="form-placeholder">
                                    <p class="tech-text-dim">SELECT A CARD TO EDIT</p>
                                </div>
                            </div>
                            <div class="tab-panel" id="abilities-tab">
                                <div class="abilities-container">
                                    <div class="abilities-header">
                                        <h4 class="tech-header-small">ABILITY CONFIGURATION</h4>
                                        <div class="abilities-header-actions">
                                            <button class="btn-tech btn-small" id="add-ability-btn">ADD ABILITY</button>
                                            <button class="btn-tech btn-small" id="link-ability-btn">COPY EXISTING</button>
                                        </div>
                                    </div>
                                    <div class="abilities-content">
                                        <div class="abilities-list" id="abilities-list">
                                            <div class="abilities-placeholder">
                                                <p class="tech-text-dim">NO ABILITIES LINKED</p>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            <div class="tab-panel" id="assets-tab">
                                <div class="assets-container" id="assets-container">
                                    <div class="form-placeholder">
                                        <p class="tech-text-dim">SELECT A CARD TO EDIT</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- 右栏：预览区 -->
                <div class="editor-sidebar right-sidebar tech-panel">
                    <div class="sidebar-header">
                        <h3 class="tech-header">VISUALIZATION</h3>
                    </div>
                    <div class="sidebar-content">
                        <div class="card-preview-container" id="card-preview">
                            <div class="preview-placeholder">
                                <p class="tech-text-dim">AWAITING DATA</p>
                            </div>
                        </div>
                        <div class="preview-info">
                            <h4 class="tech-header-small">DEBUG INFO</h4>
                            <div class="preview-details tech-code-block" id="preview-details">
                                <p>NO DATA</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * 绑定事件监听器
     */
    bindEventListeners() {
        // 标签页切换
        this.container.addEventListener('click', (event) => {
            if (event.target.classList.contains('tab-button')) {
                if (event.target.disabled) {
                    return;
                }
                this.switchTab(event.target.dataset.tab);
            }
        });

        this.container.addEventListener('click', (event) => {
            const tokenEl = event.target.closest('[data-preview-token]');
            if (!tokenEl) return;
            const tokenType = tokenEl.dataset.previewToken;
            const tokenValue = tokenEl.dataset.previewValue;
            if (!tokenType || !tokenValue) return;
            event.preventDefault();
            event.stopPropagation();
            void showPreviewDescriptionTooltip(tokenEl, tokenType, tokenValue, {
                cards: this.allCardsData
            });
        });

        // 保存按钮
        const saveBtn = this.container.querySelector('#save-btn');
        if (saveBtn) {
            saveBtn.addEventListener('click', async () => {
                await this.saveCurrentCard(false);
            });
        }

        // 另存为按钮
        const saveAsBtn = this.container.querySelector('#save-as-btn');
        if (saveAsBtn) {
            saveAsBtn.addEventListener('click', async () => {
                await this.saveCurrentCard(true);
            });
        }

        // 删除按钮
        const deleteBtn = this.container.querySelector('#delete-btn');
        if (deleteBtn) {
            deleteBtn.addEventListener('click', async () => {
                await this.deleteCurrentCard();
            });
        }

        // 新建卡牌按钮
        const newCardBtn = this.container.querySelector('#new-card-btn');
        if (newCardBtn) {
            newCardBtn.addEventListener('click', () => this.createNewCard());
        }

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

        // 添加能力按钮
        const addAbilityBtn = this.container.querySelector('#add-ability-btn');
        if (addAbilityBtn) {
            addAbilityBtn.addEventListener('click', () => this.showAddAbilityModal());
        }

        const linkAbilityBtn = this.container.querySelector('#link-ability-btn');
        if (linkAbilityBtn) {
            linkAbilityBtn.addEventListener('click', () => this.showLinkExistingAbilityModal());
        }
    }

    /**
     * 切换标签页
     */
    switchTab(tabName) {
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

            // 如果切换到能力管理标签页，渲染能力列表
            if (tabName === 'abilities' && this.currentCard) {
                this.renderAbilitiesList();
            }
            if (tabName === 'assets') {
                this.renderAssetsTab();
            }
        }
    }

    /**
     * 创建新卡牌
     */
    async createNewCard() {

        this.currentCard = {
            id: null,
            name: '',
            description: '',
            cost: 0,
            attack: 0,
            health: 1,
            countdown: 0,
            cardType: 'Follower',
            cardClass: 'Neutral',
            rarity: 'Bronze',
            cardSet: '特殊',
            keywords: [],
            tribe: [],
            canEvolve: false,
            evolveId: null,
            isCollectible: true,
            artPreview: null,
            artDetail: null,
            isNew: true
        };

        this.updateTabAvailability();

        const editorTitle = this.container.querySelector('#editor-title');
        if (editorTitle) {
            editorTitle.textContent = '新建卡牌';
        }

        this.enableEditorButtons();
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
        this.isInitialized = false;
        this.currentCard = null;

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

    let instance = cardEditorInstances.get(containerId);
    if (!instance) {
        instance = new CardEditor({ containerId, layoutMode });
        cardEditorInstances.set(containerId, instance);
    } else {
        instance.layoutMode = layoutMode;
    }

    cardEditorInstance = instance;
    await instance.initialize();
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

// 扩展CardEditor类的原型，添加新方法
CardEditor.prototype.getCardIdKey = function(value) {
    if (value === undefined || value === null) return null;
    return String(value);
};

CardEditor.prototype.getAbilityIdKey = function(value) {
    if (value === undefined || value === null) return null;
    return String(value);
};

CardEditor.prototype.applyDataRevisionFromResponse = function(response) {
    if (!response || !response.headers) return false;
    const revision = response.headers.get('X-Data-Revision');
    if (!revision) return false;
    if (this.dataRevision && this.dataRevision !== revision) {
        this.clearDataCaches();
        this.dataRevision = revision;
        this.queueCardListReload();
        return true;
    }
    if (!this.dataRevision) {
        this.dataRevision = revision;
    }
    return false;
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
        await refreshAbilityDataCache();
    } catch (error) {
        console.warn('CardEditor: Failed to refresh ability data cache after save.', error);
    }
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

CardEditor.prototype.upsertCardInCache = function(card) {
    const key = this.getCardIdKey(card?.id ?? card?.CardId);
    if (!key) return;
    const list = Array.isArray(this.cardListCache) ? [...this.cardListCache] : [];
    const index = list.findIndex(item => this.getCardIdKey(item?.id ?? item?.CardId) === key);
    if (index >= 0) {
        list[index] = card;
    } else {
        list.push(card);
    }
    this.cardListCache = list;
    this.allCardsData = list;
    this.cardDetailCache.set(key, card);
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
            ${this.currentCard.isEvolvedForm ? `
                <div class="evolution-form-notice tech-notice">
                    <span class="evolution-notice-icon">EVOLVED</span>
                    <span class="evolution-notice-text">EDITING EVOLVED FORM</span>
                </div>
            ` : ''}
            <div class="basic-compact-main">
                <div class="basic-compact-fields">
                    <div class="basic-compact-row">
                        <label class="form-label required">NAME</label>
                        <input type="text" id="card-name" class="form-input tech-input" value="${this.currentCard.name}" placeholder="ENTER NAME" ${readOnlyAttr}>
                    </div>
                    <div class="basic-compact-row basic-compact-stats">
                        <label class="form-label required">STATS</label>
                        <div class="basic-compact-stats-grid">
                            <div class="form-group">
                                <label class="form-label required">COST</label>
                                <div class="number-stepper tech-stepper">
                                    <button type="button" class="stepper-btn decrease" data-target="card-cost" ${disabledAttr}>-</button>
                                    <input type="number" id="card-cost" class="form-input number-input tech-input" value="${this.currentCard.cost}" min="0" max="20" ${readOnlyAttr}>
                                    <button type="button" class="stepper-btn increase" data-target="card-cost" ${disabledAttr}>+</button>
                                </div>
                            </div>
                            <div id="stats-container" class="basic-compact-stats">
                                <div class="form-group">
                                    <label class="form-label">ATTACK</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-attack">-</button>
                                        <input type="number" id="card-attack" class="form-input number-input tech-input" value="${this.currentCard.attack}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-attack">+</button>
                                    </div>
                                </div>
                                <div class="form-group">
                                    <label class="form-label">HEALTH</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-health">-</button>
                                        <input type="number" id="card-health" class="form-input number-input tech-input" value="${this.currentCard.health}" min="1" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-health">+</button>
                                    </div>
                                </div>
                            </div>
                            <div id="countdown-container" class="basic-compact-countdown">
                                <div class="form-group">
                                    <label class="form-label">COUNTDOWN</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-countdown">-</button>
                                        <input type="number" id="card-countdown" class="form-input number-input tech-input" value="${this.currentCard.countdown ?? 0}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-countdown">+</button>
                                    </div>
                                    <div class="form-help">设置倒计时需要添加倒数关键字。</div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="basic-compact-row">
                        <label class="form-label required">TYPE</label>
                        <div class="option-group" data-option-group="type">
                            ${this.renderOptionButtonGroup(cardTypeOptions, this.currentCard.cardType)}
                        </div>
                    </div>
                    <div class="basic-compact-row">
                        <label class="form-label required">RARITY</label>
                        <div class="option-group" data-option-group="rarity">
                            ${this.renderOptionButtonGroup(cardRarityOptions, this.currentCard.rarity)}
                        </div>
                    </div>
                    <div class="basic-compact-row">
                        <label class="form-label required">CLASS</label>
                        <div class="option-group" data-option-group="class">
                            ${this.renderOptionButtonGroup(cardClassOptions, this.currentCard.cardClass)}
                        </div>
                    </div>
                </div>
                <div class="basic-compact-art">
                    <div class="basic-art-header">
                        <div class="tech-header-small">PREVIEW ART</div>
                        <span class="asset-hint">Used in editor preview</span>
                    </div>
                    <div class="basic-art-preview${previewUrl ? ' has-image' : ''}">
                        ${previewUrl
                            ? `<img src="${previewUrl}" alt="Preview art">`
                            : `<div class="asset-placeholder">NO PREVIEW IMAGE</div>`}
                    </div>
                    <div class="basic-art-actions">
                        <button type="button" class="btn-tech btn-small" data-upload-kind="preview" ${isEditable ? '' : 'disabled'}>UPLOAD</button>
                    </div>
                    <div class="basic-art-path">${previewPath || 'NO FILE'}</div>
                </div>
            </div>
            <div class="basic-compact-extra">
                <div class="form-section tech-section">
                    <h4 class="form-section-title tech-header-small">DESCRIPTION</h4>
                    <div class="form-group">
                        <label class="form-label">DESCRIPTION</label>
                        <textarea id="card-description" class="form-textarea tech-input" placeholder="ENTER DESCRIPTION" ${readOnlyAttr}>${this.currentCard.description}</textarea>
                    </div>
                </div>

                <div class="form-section tech-section">
                    <h4 class="form-section-title tech-header-small">TAGS & METADATA</h4>
                    <div class="form-grid form-grid-2">
                        <div class="form-group">
                            <label class="form-label">KEYWORDS</label>
                            <div class="tag-input-container tech-tag-container">
                                <div class="tag-list" id="keywords-list">
                                    ${this.renderTagList(this.currentCard.keywords || [], 'keywords')}
                                </div>
                                <div class="tag-input-wrapper">
                                    <select id="keywords-select" class="tag-select tech-select">
                                        <option value="">SELECT KEYWORD</option>
                                    </select>
                                    <button type="button" class="tag-add-btn btn-tech-small" data-target="keywords">ADD</button>
                                </div>
                            </div>
                        </div>
                        <div class="form-group">
                            <label class="form-label">TRIBE</label>
                            <div class="tag-input-container tech-tag-container">
                                <div class="tag-list" id="tribe-list">
                                    ${this.renderTagList(this.currentCard.tribe || [], 'tribe')}
                                </div>
                                <div class="tag-input-wrapper">
                                    <input type="text" id="tribe-input" class="tag-input tech-input" placeholder="ENTER TRIBE">
                                    <button type="button" class="tag-add-btn btn-tech-small" data-target="tribe">ADD</button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="form-section tech-section">
                    <h4 class="form-section-title tech-header-small">CLASSIFICATION</h4>
                    <div class="form-grid form-grid-2">
                        <div class="form-group">
                            <label class="form-label">SET</label>
                            <input type="text" id="card-set" class="form-input tech-input" value="${this.currentCard.cardSet || ''}" placeholder="ENTER SET" list="card-set-options" ${readOnlyAttr}>
                            <datalist id="card-set-options"></datalist>
                        </div>
                        <div class="form-group">
                            <label class="form-label">COLLECTIBLE</label>
                            <label class="form-toggle">
                                <input type="checkbox" id="card-collectible" ${this.currentCard.isCollectible ? 'checked' : ''}>
                                <span class="form-toggle-text" id="card-collectible-label">${this.currentCard.isCollectible ? 'YES' : 'NO'}</span>
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
        this.metadataManager.getCardTypeOptions(),
        this.metadataManager.getCardRarityOptions()
    ]);

    classFilter.innerHTML = '<option value="">ALL CLASSES</option>' + this.generateSelectOptions(classOptions, '');
    typeFilter.innerHTML = '<option value="">ALL TYPES</option>' + this.generateSelectOptions(typeOptions, '');
    rarityFilter.innerHTML = '<option value="">ALL RARITIES</option>' + this.generateSelectOptions(rarityOptions, '');
};

CardEditor.prototype.populateCardSetFilterOptions = function(cards) {
    const setFilter = this.container.querySelector('#set-filter');
    if (!setFilter) {
        return;
    }

    const currentValue = setFilter.value || '';
    const setMap = new Map();
    const source = Array.isArray(cards) ? cards : [];

    source.forEach(card => {
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

    const setOptions = Array.from(setMap.values())
        .sort((a, b) => a.label.localeCompare(b.label, undefined, { numeric: true, sensitivity: 'base' }));

    setFilter.innerHTML = '<option value="">ALL SETS</option>' + this.generateSelectOptions(setOptions, currentValue);
};

CardEditor.prototype.loadCardSetDefinitions = async function() {
    try {
        const response = await fetch('/api/cardsets');
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
        .sort((a, b) => a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' }))
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

    this.renderCardList(filtered);
};

CardEditor.prototype.updateCardPreview = function() {
    const previewContainer = this.container.querySelector('#card-preview');
    if (!previewContainer || !this.currentCard) {
        return;
    }

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
        attackValue: card.attack,
        healthValue: card.health,
        countdownValue,
        showCountdown,
        earthsigilValue,
        showEarthsigil,
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
        cards: this.allCardsData,
        fallbackPreviewUrl: artPreviewUrl || detailUrl
    });

    // 更新 DEBUG INFO 区域
    const debugContainer = this.container.querySelector('#preview-details');
    if (debugContainer) {
        debugContainer.innerHTML = `
            <div class="debug-grid">
                <div class="debug-row">
                    <span class="debug-label">ID</span>
                    <span class="debug-value">${card.id || 'N/A'}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">INTERNAL NAME</span>
                    <span class="debug-value">${card.name || 'N/A'}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">KEYWORDS</span>
                    <span class="debug-value">[${(card.keywords || []).join(', ')}]</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">TRIBE</span>
                    <span class="debug-value">[${(card.tribe || []).join(', ')}]</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">EVOLVE ID</span>
                    <span class="debug-value">${card.evolveId || 'None'}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">IS NEW</span>
                    <span class="debug-value">${card.isNew ? 'YES' : 'NO'}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">COLLECTIBLE</span>
                    <span class="debug-value">${card.isCollectible ? 'YES' : 'NO'}</span>
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
                <p class="tech-text-dim">SELECT A CARD TO EDIT</p>
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
                <h4 class="form-section-title tech-header-small">ART ASSETS</h4>
                <div class="form-grid form-grid-2 ${gridClass}">
                    ${showPreview ? `
                        <div class="asset-panel">
                            <div class="asset-header">
                                <h4 class="tech-header-small">PREVIEW ART</h4>
                                <span class="asset-hint">Used in editor preview</span>
                            </div>
                            <div class="asset-preview${previewUrl ? ' has-image' : ''}">
                                ${previewUrl
                                    ? `<img src="${previewUrl}" alt="Preview art">`
                                    : `<div class="asset-placeholder">NO PREVIEW IMAGE</div>`}
                            </div>
                            <div class="asset-actions">
                                <button type="button" class="btn-tech btn-small" data-upload-kind="preview" ${isEditable ? '' : 'disabled'}>UPLOAD</button>
                            </div>
                            <div class="asset-path">${previewPath || 'NO FILE'}</div>
                        </div>
                    ` : ''}

                    <div class="asset-panel">
                        <div class="asset-header">
                            <h4 class="tech-header-small">DETAIL ART</h4>
                            <span class="asset-hint">Used in deck detail view</span>
                        </div>
                        <div class="asset-preview${detailUrl ? ' has-image' : ''}">
                            ${detailUrl
                                ? `<img src="${detailUrl}" alt="Detail art">`
                                : `<div class="asset-placeholder">NO DETAIL IMAGE</div>`}
                        </div>
                        <div class="asset-actions">
                            <button type="button" class="btn-tech btn-small" data-upload-kind="detail" ${isEditable ? '' : 'disabled'}>UPLOAD</button>
                        </div>
                        <div class="asset-path">${detailPath || 'NO FILE'}</div>
                    </div>
                </div>
            </div>
            ${isEditable ? '' : '<div class="asset-note">SAVE CARD BEFORE UPLOADING ART</div>'}
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

CardEditor.prototype.openArtCropModal = function(kind) {
    if (document.querySelector('.card-art-crop-modal')) return;

    const isPreview = kind === 'preview';
    const outputWidth = 848;
    const outputHeight = 1024;
    const aspectRatio = isPreview ? outputWidth / outputHeight : null;
    const title = isPreview ? 'PREVIEW ART CROP' : 'DETAIL ART CROP';
    const statusLabel = isPreview ? 'Preview art upload' : 'Detail art upload';

    const modal = document.createElement('div');
    modal.className = 'card-art-crop-modal';
    modal.innerHTML = `
        <div class="card-art-crop-card">
            <div class="card-art-crop-header">
                <div class="card-art-crop-title">${title}</div>
                <button type="button" class="card-art-crop-close">x</button>
            </div>
            <div class="card-art-crop-stage">
                <img class="card-art-crop-image" alt="Card art preview">
                <div class="card-art-crop-box">
                    <span class="card-art-crop-handle"></span>
                </div>
                <div class="card-art-crop-placeholder">
                    <div class="card-art-crop-placeholder-title">UPLOAD IMAGE</div>
                    <div class="card-art-crop-placeholder-text">Click to select an image</div>
                </div>
            </div>
            <input class="card-art-crop-file" type="file" accept="image/*">
            <div class="card-art-crop-actions">
                <button type="button" class="btn-tech btn-small card-art-crop-cancel">CANCEL</button>
                <button type="button" class="btn-tech btn-small btn-primary card-art-crop-upload">UPLOAD</button>
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
            setModalStatus('Unsupported image type.', true);
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
            setModalStatus('Failed to load image.', true);
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
            setModalStatus('Select an image first.', true);
            return;
        }
        setModalStatus(`${statusLabel}...`);
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
                throw new Error('Failed to create output.');
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
                canvas.toBlob(resolve, 'image/png');
            });

            if (!blob) {
                throw new Error('Failed to generate output.');
            }

            const filename = isPreview ? 'preview.png' : 'detail.png';
            await this.uploadCardArt(kind, blob, filename, { silent: true });
            setModalStatus('Upload complete.');
            setTimeout(closeModal, 400);
        } catch (error) {
            setModalStatus(`Upload failed: ${error.message}`, true);
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
    if (!this.currentCard || !this.currentCard.id) {
        if (!options.silent) {
            alert('Save the card before uploading art.');
        }
        throw new Error('Card must be saved before uploading art.');
    }

    try {
        const formData = new FormData();
        if (filename) {
            formData.append('file', file, filename);
        } else {
            formData.append('file', file);
        }

        const endpoint = kind === 'preview'
            ? `/api/cards/${this.currentCard.id}/art/preview`
            : `/api/cards/${this.currentCard.id}/art/detail`;

        const response = await fetch(endpoint, {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const updated = await response.json();
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
        this.currentCard = this.convertApiCardToEditorFormat(updated);
        this.renderAssetsTab();
        if (this.layoutMode === 'test') {
            await this.renderBasicInfoForm();
        }
        this.updateCardPreview();
    } catch (error) {
        console.error('CardEditor: Failed to upload art:', error);
        if (!options.silent) {
            alert(`Upload failed: ${error.message}`);
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
    const basicTab = this.container.querySelector('#basic-tab');
    if (!basicTab) {
        console.error('CardEditor: Basic tab not found');
        return;
    }

    try {
        await this.ensureKeywordLabelMap();
        // 获取卡牌类型、职业、稀有度等枚举选项
        const [cardTypeOptions, cardClassOptions, cardRarityOptions] = await Promise.all([
            this.metadataManager.getCardTypeOptions(),
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
                    ${this.currentCard.isEvolvedForm ? `
                        <div class="evolution-form-notice tech-notice">
                            <span class="evolution-notice-icon">EVOLVED</span>
                            <span class="evolution-notice-text">EDITING EVOLVED FORM</span>
                        </div>
                    ` : ''}
                    <div class="form-section tech-section">
                        <h4 class="form-section-title tech-header-small">BASIC ATTRIBUTES</h4>
                        <div class="form-grid form-grid-2">
                            <div class="form-group">
                                <label class="form-label required">NAME</label>
                                <input type="text" id="card-name" class="form-input tech-input" value="${this.currentCard.name}" placeholder="ENTER NAME" ${readOnlyAttr}>
                            </div>
                            <div class="form-group">
                                <label class="form-label required">COST</label>
                                <div class="number-stepper tech-stepper">
                                    <button type="button" class="stepper-btn decrease" data-target="card-cost" ${disabledAttr}>-</button>
                                    <input type="number" id="card-cost" class="form-input number-input tech-input" value="${this.currentCard.cost}" min="0" max="20" ${readOnlyAttr}>
                                    <button type="button" class="stepper-btn increase" data-target="card-cost" ${disabledAttr}>+</button>
                                </div>
                            </div>
                        </div>
                        
                        <div id="stats-container">
                            <div class="form-grid form-grid-2" style="margin-bottom: var(--space-md);">
                                <div class="form-group">
                                    <label class="form-label">ATTACK</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-attack">-</button>
                                        <input type="number" id="card-attack" class="form-input number-input tech-input" value="${this.currentCard.attack}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-attack">+</button>
                                    </div>
                                </div>
                                <div class="form-group">
                                    <label class="form-label">HEALTH</label>
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
                                <div class="form-group">
                                    <label class="form-label">COUNTDOWN</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-countdown">-</button>
                                        <input type="number" id="card-countdown" class="form-input number-input tech-input" value="${this.currentCard.countdown ?? 0}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-countdown">+</button>
                                    </div>
                                    <div class="form-help">设置倒计时需要添加倒数关键字。</div>
                                </div>
                            </div>
                        </div>

                        <div class="form-group">
                            <label class="form-label">DESCRIPTION</label>
                            <textarea id="card-description" class="form-textarea tech-input" placeholder="ENTER DESCRIPTION" ${readOnlyAttr}>${this.currentCard.description}</textarea>
                        </div>
                    </div>

                    <div class="form-section tech-section">
                        <h4 class="form-section-title tech-header-small">TAGS & METADATA</h4>
                        <div class="form-grid form-grid-2">
                            <div class="form-group">
                                <label class="form-label">KEYWORDS</label>
                                <div class="tag-input-container tech-tag-container">
                                    <div class="tag-list" id="keywords-list">
                                        ${this.renderTagList(this.currentCard.keywords || [], 'keywords')}
                                    </div>
                                    <div class="tag-input-wrapper">
                                        <select id="keywords-select" class="tag-select tech-select">
                                            <option value="">SELECT KEYWORD</option>
                                        </select>
                                        <button type="button" class="tag-add-btn btn-tech-small" data-target="keywords">ADD</button>
                                    </div>
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label">TRIBE</label>
                                <div class="tag-input-container tech-tag-container">
                                    <div class="tag-list" id="tribe-list">
                                        ${this.renderTagList(this.currentCard.tribe || [], 'tribe')}
                                    </div>
                                    <div class="tag-input-wrapper">
                                        <input type="text" id="tribe-input" class="tag-input tech-input" placeholder="ENTER TRIBE">
                                        <button type="button" class="tag-add-btn btn-tech-small" data-target="tribe">ADD</button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="form-section tech-section">
                        <h4 class="form-section-title tech-header-small">CLASSIFICATION</h4>
                        <div class="form-grid form-grid-3">
                            <div class="form-group">
                                <label class="form-label required">TYPE</label>
                                <div class="option-group" data-option-group="type">
                                    ${this.renderOptionButtonGroup(cardTypeOptions, this.currentCard.cardType)}
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label required">CLASS</label>
                                <div class="option-group" data-option-group="class">
                                    ${this.renderOptionButtonGroup(cardClassOptions, this.currentCard.cardClass)}
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label required">RARITY</label>
                                <div class="option-group" data-option-group="rarity">
                                    ${this.renderOptionButtonGroup(cardRarityOptions, this.currentCard.rarity)}
                                </div>
                            </div>
                            <div class="form-group">
                            <label class="form-label">SET</label>
                                <input type="text" id="card-set" class="form-input tech-input" value="${this.currentCard.cardSet || ''}" placeholder="ENTER SET" list="card-set-options" ${readOnlyAttr}>
                                <datalist id="card-set-options"></datalist>
                            </div>
                            <div class="form-group">
                                <label class="form-label">COLLECTIBLE</label>
                                <label class="form-toggle">
                                    <input type="checkbox" id="card-collectible" ${this.currentCard.isCollectible ? 'checked' : ''}>
                                    <span class="form-toggle-text" id="card-collectible-label">${this.currentCard.isCollectible ? 'YES' : 'NO'}</span>
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
        // 检查是否有进化形态
        const evolvedForms = this.findEvolvedFormsForCard(card);
        const hasEvolvedForms = evolvedForms.length > 0;

        return `
            <div class="card-list-item tech-list-item" data-card-id="${card.id !== undefined ? card.id : card.CardId}">
                <div class="card-item-header">
                    <span class="card-item-name tech-text-bold">${card.name || card.Name || 'UNTITLED'}</span>
                    <div class="card-item-indicators">
                        ${hasEvolvedForms ? '<div class="indicator-icon evolve" title="EVOLVE"></div>' : ''}
                        <span class="card-item-cost tech-badge">${(card.cost ?? card.Cost) ?? 0}</span>
                    </div>
                </div>
                <div class="card-item-details">
                    <span class="card-item-type tech-tag">${normalizeEnumName((card.type ?? card.Type) ?? 'UNKNOWN')}</span>
                    <span class="card-item-class tech-tag">${normalizeEnumName((card.class ?? card.Class) ?? 'NEUTRAL')}</span>
                    <span class="card-item-rarity tech-tag rarity-${normalizeEnumName((card.rarity ?? card.Rarity) ?? 'Common').toLowerCase()}">${normalizeEnumName((card.rarity ?? card.Rarity) ?? 'COMMON')}</span>
                </div>
                ${(equalsIgnoreCase(normalizeEnumName(card.type || card.Type), 'Follower')) ? `
                    <div class="card-item-stats">
                        <span class="stat-attack">ATK ${(card.attack ?? card.Attack) ?? 0}</span>
                        <span class="stat-health">HP ${(card.health ?? card.Health) ?? 1}</span>
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
                label.textContent = value ? 'YES' : 'NO';
            }
        });
    }

    const previewUploadButton = basicTab.querySelector('[data-upload-kind="preview"]');
    if (previewUploadButton) {
        previewUploadButton.addEventListener('click', () => {
            this.openArtCropModal('preview');
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
        'type': 'cardType',
        'class': 'cardClass',
        'rarity': 'rarity',
        'set': 'cardSet',
        'isCollectible': 'isCollectible'
    };

    const actualProperty = propertyMap[property] || property;
    this.currentCard[actualProperty] = value;

    // 更新预览
    this.updateCardPreview();

};

CardEditor.prototype.updateStatsVisibility = function() {
    const statsContainer = this.container.querySelector('#stats-container');
    const countdownContainer = this.container.querySelector('#countdown-container');
    if (!statsContainer || !countdownContainer || !this.currentCard) return;

    // 只有随从卡牌显示攻击力和生命值
    const cardType = normalizeEnumName(this.currentCard.cardType);
    const shouldShowStats = equalsIgnoreCase(cardType, 'Follower');
    const shouldShowCountdown = equalsIgnoreCase(cardType, 'Amulet') || equalsIgnoreCase(cardType, 'Crest');
    const visibleDisplay = this.layoutMode === 'test' ? 'flex' : 'block';
    statsContainer.style.display = shouldShowStats ? visibleDisplay : 'none';
    countdownContainer.style.display = shouldShowCountdown ? visibleDisplay : 'none';
};

CardEditor.prototype.buildCardUpsertDto = function(isCreate) {
    if (!this.currentCard) return null;

    const cardType = normalizeEnumName(this.currentCard.cardType);
    const isFollower = equalsIgnoreCase(cardType, 'Follower');
    const isCountdownType = equalsIgnoreCase(cardType, 'Amulet') || equalsIgnoreCase(cardType, 'Crest');
    const hasCountdownKeyword = (this.currentCard.keywords || []).some(keyword => equalsIgnoreCase(keyword, 'Countdown'));

    const rawPack = (this.currentCard.cardSet || '').trim();

    return {
        id: isCreate ? null : this.currentCard.id,
        name: this.currentCard.name?.trim() || '',
        type: cardType,
        rarity: normalizeEnumName(this.currentCard.rarity),
        card_set: rawPack || '特殊',
        class: normalizeEnumName(this.currentCard.cardClass),
        cost: Number(this.currentCard.cost) || 0,
        attack: isFollower ? (Number(this.currentCard.attack) || 0) : null,
        health: isFollower ? (Number(this.currentCard.health) || 1) : null,
        countdown: isCountdownType && hasCountdownKeyword ? (Number(this.currentCard.countdown) || 0) : null,
        description: this.currentCard.description || '',
        artPreview: this.currentCard.artPreview || null,
        artDetail: this.currentCard.artDetail || null,
        isCollectible: this.currentCard.isCollectible ?? true,
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

CardEditor.prototype.saveCurrentCard = async function(isSaveAs) {
    if (!this.currentCard) {
        alert('请先选择或创建一张卡牌');
        return;
    }

    if (!this.validateForm()) {
        alert('请先修正表单错误后再保存');
        return;
    }

    const rawSetName = (this.currentCard.cardSet || '').trim();
    if (rawSetName && !this.isKnownCardSetName(rawSetName)) {
        const confirmed = confirm(`卡包 "${rawSetName}" 不存在，保存后将自动注册为新卡包。是否继续？`);
        if (!confirmed) {
            return;
        }
    }

    const isCreate = !!this.currentCard.isNew || !!isSaveAs;
    const dto = this.buildCardUpsertDto(isCreate);

    const editorRoot = this.container?.querySelector('.card-editor-layout') || this.container;
    const loader = editorRoot ? getBlockingLoader(editorRoot) : null;
    loader?.show('正在保存卡牌...');

    try {
        const url = isCreate ? '/api/cards' : `/api/cards/${this.currentCard.id}`;
        const method = isCreate ? 'POST' : 'PUT';

        const response = await fetch(url, {
            method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(dto)
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        this.applyDataRevisionFromResponse(response);

        const saved = await response.json();
        const savedId = saved?.id ?? saved?.cardId ?? saved?.CardId;

        if (savedId != null) {
            this.currentCard = this.convertApiCardToEditorFormat(saved);
            this.currentCard.isNew = false;
            this.updateTabAvailability();
            this.upsertCardInCache(saved);
            const editorTitle = this.container.querySelector('#editor-title');
            if (editorTitle) {
                editorTitle.textContent = `编辑卡牌: ${this.currentCard.name}`;
            }
            await this.renderBasicInfoForm();
            this.updateCardPreview();

            const activeTab = this.container.querySelector('.tab-button.active')?.dataset.tab;
            if (activeTab === 'abilities') {
                await this.renderAbilitiesList();
            } else if (activeTab === 'assets') {
                this.renderAssetsTab();
            }
        }

        await this.loadCardList();
        await this.loadCardSetDefinitions();
        await this.refreshAbilityDataCacheAfterSave();
        if (savedId != null) {
            this.highlightCardInList(savedId);
        }

        alert(isCreate ? '卡牌创建成功！' : '卡牌保存成功！');
    } catch (error) {
        console.error('CardEditor: Failed to save card:', error);
        alert(`保存失败: ${error.message}`);
    } finally {
        loader?.hide();
    }
};

CardEditor.prototype.deleteCurrentCard = async function() {
    if (!this.currentCard || this.currentCard.isNew) {
        return;
    }

    if (!confirm(`确定要删除卡牌 "${this.currentCard.name}" 吗？此操作不可撤销。`)) {
        return;
    }

    try {
        const deletingCardId = this.currentCard.id;
        const response = await fetch(`/api/cards/${this.currentCard.id}`, { method: 'DELETE' });
        if (!response.ok && response.status !== 204) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        this.applyDataRevisionFromResponse(response);
        this.removeCardFromCache(deletingCardId);

        this.currentCard = null;
        this.disableEditorButtons();
        this.updateTabAvailability();
        const editorTitle = this.container.querySelector('#editor-title');
        if (editorTitle) editorTitle.textContent = 'AWAITING SELECTION';

        const basicTab = this.container.querySelector('#basic-tab');
        if (basicTab) {
            basicTab.innerHTML = `
                <div class="form-placeholder">
                    <p class="tech-text-dim">SELECT A CARD TO EDIT</p>
                </div>
            `;
        }

        const previewContainer = this.container.querySelector('#card-preview');
        if (previewContainer) {
            previewContainer.innerHTML = `
                <div class="preview-placeholder">
                    <p class="tech-text-dim">AWAITING DATA</p>
                </div>
            `;
        }

        await this.loadCardList();

        alert('卡牌已删除');
    } catch (error) {
        console.error('CardEditor: Failed to delete card:', error);
        alert(`删除失败: ${error.message}`);
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

        const useCache = !options.force;
        let allCards = null;

        if (useCache && Array.isArray(this.cardListCache)) {
            allCards = this.cardListCache;
        } else {

            // 调用后端API获取卡牌列表
            const response = await fetch('/api/cards');
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            this.applyDataRevisionFromResponse(response);

            allCards = await response.json();
            this.cardListCache = Array.isArray(allCards) ? allCards : [];
        }

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


    return cards.filter(card => {
        const cardId = card.id !== undefined ? card.id : card.CardId;
        const rawType = card.type ?? card.Type ?? '';
        const cardType = normalizeEnumName(rawType);
        const rarity = normalizeEnumName(card.rarity ?? card.Rarity ?? '');
        const cardName = card.name ?? card.Name ?? '';
        const isLeaderType = equalsIgnoreCase(cardType, 'Leader') || rawType === 0;
        const isLeaderCard = isLeaderType || cardId === 0 || equalsIgnoreCase(cardName, '主战者');


        if (isLeaderCard) {
            return false;
        }

        if (equalsIgnoreCase(rarity, 'Special')) {
            return false;
        }

        return true;
    });
};

CardEditor.prototype.findEvolvedFormsForCard = function(baseCard) {

    if (!this.allCardsData || !Array.isArray(this.allCardsData)) {
        return [];
    }

    const baseCardEvolveId = baseCard.evolve_id || baseCard.EvolveId || baseCard.evolveId;

    // 查找进化形态：
    // 基础卡牌的evolve_id指向进化形态的ID
    if (!baseCardEvolveId) {
        return []; // 如果没有evolve_id，说明不能进化
    }

    const evolvedForms = this.allCardsData.filter(card => {
        const cardId = card.id !== undefined ? card.id : card.CardId;

        // 查找ID等于基础卡牌evolve_id的卡牌
        const matches = cardId === baseCardEvolveId;
        if (matches) {
        }
        return matches;
    });

    return evolvedForms;
};

CardEditor.prototype.selectCard = async function(cardId) {
    try {

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
            const response = await fetch(`/api/cards/${cardId}`);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            this.applyDataRevisionFromResponse(response);

            cardData = await response.json();
            if (cardKey) {
                this.cardDetailCache.set(cardKey, cardData);
            }
        }

        // 转换为编辑器格式
        this.currentCard = this.convertApiCardToEditorFormat(cardData);
        this.updateTabAvailability();

        // 更新编辑器标题
        const editorTitle = this.container.querySelector('#editor-title');
        if (editorTitle) {
            editorTitle.textContent = `编辑卡牌: ${this.currentCard.name}`;
        }

        // 启用编辑器按钮
        this.enableEditorButtons();

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

        // 加载进化形态信息
        this.loadEvolvedForms();
    } catch (error) {
        console.error('CardEditor: Failed to select card:', error);
        alert(`加载卡牌失败: ${error.message}`);
    } finally {
        const editorRoot = this.container?.querySelector('.card-editor-layout') || this.container;
        const loader = editorRoot ? getBlockingLoader(editorRoot) : null;
        loader?.hide();
    }
};

CardEditor.prototype.updateTabAvailability = function() {
    const abilitiesTab = this.container.querySelector('.tab-button[data-tab="abilities"]');
    const assetsTab = this.container.querySelector('.tab-button[data-tab="assets"]');
    const canAccess = !!this.currentCard && !this.currentCard.isNew;

    [abilitiesTab, assetsTab].forEach(tab => {
        if (!tab) return;
        tab.disabled = !canAccess;
        tab.classList.toggle('is-disabled', !canAccess);
    });

    if (!canAccess) {
        const activeTab = this.container.querySelector('.tab-button.active');
        if (activeTab && activeTab.dataset.tab !== 'basic') {
            this.switchTab('basic');
        }
    }
};

CardEditor.prototype.loadEvolvedForms = function() {
    if (!this.currentCard) {
        return;
    }


    // 查找当前卡牌的进化形态
    const evolvedForms = this.findEvolvedFormsForCard(this.currentCard);
    this.currentCardEvolvedForms = evolvedForms;
    this.currentEvolvedFormIndex = -1; // -1表示显示原始形态


    // 更新进化形态导航UI
    this.updateEvolvedFormsNavigation();
};

CardEditor.prototype.updateEvolvedFormsNavigation = function() {

    // 查找或创建进化形态导航区域
    let evolutionNav = this.container.querySelector('#evolution-navigation');

    if (!this.currentCardEvolvedForms || this.currentCardEvolvedForms.length === 0) {
        // 如果没有进化形态，隐藏导航
        if (evolutionNav) {
            evolutionNav.style.display = 'none';
        }
        return;
    }


    // 如果导航不存在，创建它
    if (!evolutionNav) {
        const editorMain = this.container.querySelector('.editor-main');
        if (editorMain) {
            const navHTML = `
                <div id="evolution-navigation" class="evolution-navigation">
                    <div class="evolution-nav-header">
                        <span class="evolution-nav-title">卡牌形态</span>
                        <div class="evolution-nav-controls">
                            <button id="prev-evolution" class="evolution-nav-btn" title="上一个形态">◀</button>
                            <span id="evolution-indicator" class="evolution-indicator-text">原始形态</span>
                            <button id="next-evolution" class="evolution-nav-btn" title="下一个形态">▶</button>
                        </div>
                    </div>
                </div>
            `;
            editorMain.insertAdjacentHTML('afterbegin', navHTML);
            evolutionNav = this.container.querySelector('#evolution-navigation');

            // 绑定导航事件
            this.bindEvolutionNavigationEvents();
        }
    }

    if (evolutionNav) {
        evolutionNav.style.display = 'block';
        this.updateEvolutionIndicator();
    }
};

CardEditor.prototype.bindEvolutionNavigationEvents = function() {
    const prevBtn = this.container.querySelector('#prev-evolution');
    const nextBtn = this.container.querySelector('#next-evolution');

    if (prevBtn) {
        prevBtn.addEventListener('click', () => {
            this.showPreviousEvolution();
        });
    }

    if (nextBtn) {
        nextBtn.addEventListener('click', () => {
            this.showNextEvolution();
        });
    }
};

CardEditor.prototype.showPreviousEvolution = function() {
    if (!this.currentCardEvolvedForms) return;

    const totalForms = this.currentCardEvolvedForms.length + 1; // +1 for base form
    this.currentEvolvedFormIndex = (this.currentEvolvedFormIndex - 1 + totalForms) % totalForms - 1;

    this.switchToEvolutionForm();
};

CardEditor.prototype.showNextEvolution = function() {
    if (!this.currentCardEvolvedForms) return;

    const totalForms = this.currentCardEvolvedForms.length + 1; // +1 for base form
    this.currentEvolvedFormIndex = (this.currentEvolvedFormIndex + 1) % totalForms;
    if (this.currentEvolvedFormIndex >= this.currentCardEvolvedForms.length) {
        this.currentEvolvedFormIndex = -1; // Back to base form
    }

    this.switchToEvolutionForm();
};

CardEditor.prototype.switchToEvolutionForm = function() {
    if (this.currentEvolvedFormIndex === -1) {
        // 显示原始形态 - 重新加载原始卡牌数据
        this.loadOriginalCardForm();
    } else {
        // 显示进化形态
        const evolvedForm = this.currentCardEvolvedForms[this.currentEvolvedFormIndex];
        this.loadEvolvedCardForm(evolvedForm);
    }

    this.updateEvolutionIndicator();
};

CardEditor.prototype.loadOriginalCardForm = async function() {
    // 重新加载原始卡牌数据
    try {
        const response = await fetch(`/api/cards/${this.currentCard.id}`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const cardData = await response.json();
        this.currentCard = this.convertApiCardToEditorFormat(cardData);

        // 更新表单和预览
        await this.renderBasicInfoForm();
        this.updateCardPreview();

    } catch (error) {
        console.error('CardEditor: Failed to load original card form:', error);
        alert(`加载原始卡牌失败: ${error.message}`);
    }
};

CardEditor.prototype.loadEvolvedCardForm = function(evolvedCard) {
    // 将进化卡牌数据转换为编辑器格式
    this.currentCard = this.convertApiCardToEditorFormat(evolvedCard);

    // 标记为进化形态（只读模式）
    this.currentCard.isEvolvedForm = true;

    // 更新表单和预览
    this.renderBasicInfoForm();
    this.updateCardPreview();
};

CardEditor.prototype.updateEvolutionIndicator = function() {
    const indicator = this.container.querySelector('#evolution-indicator');
    if (!indicator) return;

    if (this.currentEvolvedFormIndex === -1) {
        indicator.textContent = '原始形态';
    } else {
        const evolvedForm = this.currentCardEvolvedForms[this.currentEvolvedFormIndex];
        const formName = evolvedForm.name || evolvedForm.Name || '进化形态';
        indicator.textContent = formName;
    }
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
        alert(`标签 "${tagValue}" 已存在`);
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
    if (this.currentCard.cost < 0 || this.currentCard.cost > 20) {
        errors.push('费用必须在0-20之间');
    }

    const cardType = normalizeEnumName(this.currentCard.cardType);
    const hasCountdownKeyword = (this.currentCard.keywords || []).some(keyword => equalsIgnoreCase(keyword, 'Countdown'));

    if (equalsIgnoreCase(cardType, 'Follower')) {
        if (this.currentCard.attack < 0 || this.currentCard.attack > 99) {
            errors.push('攻击力必须在0-99之间');
        }

        if (this.currentCard.health < 1 || this.currentCard.health > 99) {
            errors.push('生命值必须在1-99之间');
        }
    } else if ((equalsIgnoreCase(cardType, 'Amulet') || equalsIgnoreCase(cardType, 'Crest')) && hasCountdownKeyword) {
        if (this.currentCard.countdown < 0 || this.currentCard.countdown > 99) {
            errors.push('倒计时必须在0-99之间');
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
        const cardKey = this.getCardIdKey(this.currentCard.id);
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
        const response = await fetch(`/api/cards/${this.currentCard.id}/abilities`);
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
    const currentCardKey = this.getCardIdKey(this.currentCard?.id);
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
                    <button class="btn-tech-small edit-ability-btn" data-ability-id="${ability.id}">EDIT</button>
                    <button class="btn-tech-small btn-danger remove-ability-btn" data-ability-id="${ability.id}">DELETE</button>
                </div>
            </div>
            <div class="ability-details">
                <div class="ability-description tech-text-dim">${ability.description || '暂无描述'}</div>
                <div class="ability-config">
                    <span class="config-item tech-tag">TYPE: ${abilityTypeLabel}</span>
                    ${keywordTagsHtml}
                    <span class="config-item tech-tag ${ability.isActive ? 'status-active' : 'status-inactive'}">
                        ${ability.isActive ? 'ACTIVE' : 'INACTIVE'}
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
    alert('请先选择一张卡牌');
    return;
}


try {
    this.showAddAbilityTypeModal();

} catch (error) {
    console.error('CardEditor: Failed to show add ability modal:', error);
    alert(`打开添加能力窗口失败: ${error.message}`);
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
    let abilityEditor = new AbilityEditor();

    const defaultAbilityConfig = {
        cardId: this.currentCard.id,
        cardName: this.currentCard.name || '',
        abilitySequence: this.getNextAbilitySequence(),
        name: '',
        description: '',
        abilityKeywords: [],
        abilityType: 1,
        priority: 0,
        isActive: true,
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
        counterRegistrations: []
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

    // 构建API请求数据
    // 注意：后端API可能需要特定的字段结构，这里确保与saveAbilityChanges保持一致但用于创建
    const abilityData = {
        cardId: this.currentCard.id,
        name: config.name,
        description: config.description,
        abilityKeywords: config.abilityKeywords || [],
        abilityType: config.abilityType || 1,
        priority: config.priority || 0,
        isActive: config.isActive,
        jsonConfig: config.jsonConfig // AbilityEditor已经构建了jsonConfig
    };

    // 调用后端API创建能力
    const response = await fetch('/api/abilities', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(abilityData)
    });

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    this.applyDataRevisionFromResponse(response);

    const result = await response.json();

    this.invalidateAbilitiesForCard(this.currentCard.id);
    this.invalidateAbilityLibraryCache();
    const resultKey = this.getAbilityIdKey(result?.id ?? result?.Id);
    if (resultKey) {
        this.abilityDetailCache.set(resultKey, result);
    }

    // 刷新能力列表
    await this.renderAbilitiesList();
    await this.refreshAbilityDataCacheAfterSave();
    
    alert('能力添加成功！');

} catch (error) {
    console.error('CardEditor: Failed to create ability:', error);
    alert(`添加能力失败: ${error.message}`);
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
    if (!this.currentCard || !this.currentCard.id) {
        alert('请先选择并保存一张卡牌');
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
            const abilityData = await ensureAbilityData();
            allCards = Array.isArray(abilityData.cards) ? abilityData.cards : [];
            this.cardListCache = Array.isArray(allCards) ? allCards : [];
            this.allCardsData = this.cardListCache;

            const abilityMap = new Map();
            const countMap = this.abilityCountsCache;
            countMap.clear();

            this.cardListCache.forEach(card => {
                const rawCardId = card?.id ?? card?.CardId;
                const cardId = normalizeCardId(rawCardId);
                if (!cardId) return;
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
            const cardId = normalizeCardId(card?.id ?? card?.CardId);
            if (!cardId) return null;

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
                                <span class="tech-badge">COST ${entry.cardCost}</span>
                                <span class="tech-tag">${entry.cardType}</span>
                                <span class="tech-tag">${entry.cardClass}</span>
                                <span class="tech-tag">ABILITIES ${abilityCountLabel}</span>
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
                    alert('能力复制成功！');
                } catch (error) {
                    console.error('CardEditor: Failed to copy ability:', error);
                    alert(`复制能力失败: ${error.message}`);
                } finally {
                    modalLoader?.hide();
                    confirmBtn.disabled = false;
                }
            });
        }
    } catch (error) {
        console.error('CardEditor: Failed to show copy ability modal:', error);
        alert(`打开复制窗口失败: ${error.message}`);
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
        const response = await fetch('/api/abilities');
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
        alert(`添加能力失败: ${error.message}`);
    }
};

CardEditor.prototype.createNewAbility = async function() {
    const modal = document.getElementById('add-ability-modal');
    if (!modal) return;

    // 获取表单数据
    const name = modal.querySelector('#ability-name').value.trim();
    const description = modal.querySelector('#ability-description').value.trim();
    const abilityTag = modal.querySelector('#ability-tag').value;
    const abilityType = modal.querySelector('#ability-type').value;
    const priority = parseInt(modal.querySelector('#ability-priority').value) || 0;

    // 验证必填字段
    if (!name) {
        alert('请输入能力名称');
        return;
    }

    if (!abilityTag) {
        alert('请选择能力标签');
        return;
    }

    // 构建能力配置
    const abilityConfig = {
        cardId: this.currentCard.id,
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
            effects: []
        })
    };


    // 调用后端API创建能力
    const response = await fetch('/api/abilities', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(abilityConfig)
    });

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    this.applyDataRevisionFromResponse(response);

    const result = await response.json();
    this.invalidateAbilitiesForCard(this.currentCard.id);
    this.invalidateAbilityLibraryCache();
    const resultKey = this.getAbilityIdKey(result?.id ?? result?.Id);
    if (resultKey) {
        this.abilityDetailCache.set(resultKey, result);
    }
    await this.refreshAbilityDataCacheAfterSave();
};

CardEditor.prototype.linkExistingAbility = async function() {
    if (!this.selectedExistingAbilityId) {
        alert('请选择一个现有能力');
        return;
    }

    const result = await this.copyExistingAbilityById(this.selectedExistingAbilityId);

    // 清除选中状态
    this.selectedExistingAbilityId = null;
};

CardEditor.prototype.copyExistingAbilityById = async function(abilityId) {
    if (!this.currentCard || !this.currentCard.id) {
        throw new Error('请先选择并保存一张卡牌');
    }

    const abilityKey = this.getAbilityIdKey(abilityId);
    let ability = abilityKey ? this.abilityDetailCache.get(abilityKey) : null;

    if (!ability) {
        const abilityResponse = await fetch(`/api/abilities/${abilityId}`);
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
        cardId: this.currentCard.id,
        name: ability.name,
        description: ability.description,
        abilityKeywords: ability.abilityKeywords || normalizeAbilityKeywords(ability),
        abilityType: ability.abilityType,
        priority: ability.priority,
        isActive: ability.isActive,
        jsonConfig: ability.jsonConfig
    };

    const createRes = await fetch('/api/abilities', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(abilityData)
    });

    if (!createRes.ok) {
        throw new Error(`HTTP ${createRes.status}: ${createRes.statusText}`);
    }
    this.applyDataRevisionFromResponse(createRes);

    const created = await createRes.json();
    this.invalidateAbilitiesForCard(this.currentCard.id);
    this.invalidateAbilityLibraryCache();
    const createdKey = this.getAbilityIdKey(created?.id ?? created?.Id);
    if (createdKey) {
        this.abilityDetailCache.set(createdKey, created);
    }
    await this.refreshAbilityDataCacheAfterSave();
    return created;
};

CardEditor.prototype.editAbility = async function(abilityId) {

    try {
        const abilityKey = this.getAbilityIdKey(abilityId);
        let ability = abilityKey ? this.abilityDetailCache.get(abilityKey) : null;

        if (!ability) {
            // 获取能力详细信息
            const response = await fetch(`/api/abilities/${abilityId}`);
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
        alert(`加载能力失败: ${error.message}`);
    }
};

CardEditor.prototype.removeAbility = async function(abilityId) {
    if (!confirm('确定要移除这个能力吗？')) {
        return;
    }

    try {

        const editorRoot = this.container?.querySelector('.card-editor-layout') || this.container;
        const loader = editorRoot ? getBlockingLoader(editorRoot) : null;
        loader?.show('正在移除能力...');

        try {
            // 调用后端API移除能力关联
            const response = await fetch(`/api/cards/${this.currentCard.id}/abilities/${abilityId}`, {
                method: 'DELETE'
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            this.applyDataRevisionFromResponse(response);


            this.invalidateAbilitiesForCard(this.currentCard.id);
            this.invalidateAbilityDetailCache(abilityId);
            this.invalidateAbilityLibraryCache();

            // 刷新能力列表
            await this.renderAbilitiesList();
        } finally {
            loader?.hide();
        }
    } catch (error) {
        console.error('CardEditor: Failed to remove ability:', error);
        alert(`移除能力失败: ${error.message}`);
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
        alert(`打开能力编辑器失败: ${error.message}`);
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
        counterRegistrations: []
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
            if (jsonConfig.conditions) {
                config.conditions = jsonConfig.conditions;
            }
            if (jsonConfig.targets) {
                config.targets = jsonConfig.targets;
            }
            if (jsonConfig.effects) {
                config.effects = jsonConfig.effects;
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
        const response = await fetch(`/api/abilities/${originalAbility.id}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(updatedConfig)
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        this.applyDataRevisionFromResponse(response);

        const result = await response.json();

        const abilityKey = this.getAbilityIdKey(originalAbility?.id ?? result?.id);
        if (abilityKey) {
            this.abilityDetailCache.set(abilityKey, result);
        }
        const cardId = this.currentCard?.id ?? result?.cardId ?? originalAbility?.cardId;
        if (cardId) {
            this.invalidateAbilitiesForCard(cardId);
        }
        this.invalidateAbilityLibraryCache();

        // 刷新能力列表
        await this.renderAbilitiesList();
        await this.refreshAbilityDataCacheAfterSave();

        alert('能力保存成功！');

    } catch (error) {
        console.error('CardEditor: Failed to save ability changes:', error);
        alert(`保存能力失败: ${error.message}`);
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

