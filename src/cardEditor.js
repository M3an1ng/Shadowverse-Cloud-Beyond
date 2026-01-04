// src/cardEditor.js - 卡牌编辑器主模块
//
// 相关文档:
// - UI设计: 文档/06-UI设计/01-卡牌编辑器原型设计.md
// - 元数据驱动UI: 文档/06-UI设计/04-元数据驱动UI架构设计.md
//

import { MetadataManager } from './metadataManager.js';
import { DynamicUIRenderer } from './dynamicUIRenderer.js';
import { AbilityEditor } from './components/AbilityEditor.js';

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

function equalsIgnoreCase(a, b) {
    if (typeof a !== 'string' || typeof b !== 'string') return a === b;
    return a.toLowerCase() === b.toLowerCase();
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
        return 'Unknown';
    }
    return formatAbilityTagSummary(keywords);
}

/**
 * 卡牌编辑器主类
 * 实现三栏式布局和基础功能
 */
class CardEditor {
    constructor() {
        this.container = null;
        this.metadataManager = new MetadataManager();
        this.uiRenderer = new DynamicUIRenderer();
        this.currentCard = null;
        this.isInitialized = false;
    }

    /**
     * 初始化卡牌编辑器
     */
    async initialize() {
        if (this.isInitialized) {
            return;
        }

        console.log('CardEditor: Initializing...');

        try {
            // 获取容器元素
            this.container = document.getElementById('card-editor-container');
            if (!this.container) {
                throw new Error('Card editor container not found');
            }

            // 显示加载状态
            this.showLoading('正在加载元数据...');

            // 初始化元数据管理器
            await this.metadataManager.initialize();

            // 初始化UI渲染器
            await this.uiRenderer.initialize(this.metadataManager);

            // 创建编辑器布局
            this.createEditorLayout();

            // 初始化列表筛选控件选项
            await this.populateCardListFilterOptions();

            // 绑定事件监听器
            this.bindEventListeners();
            // 加载卡牌列表
            await this.loadCardList();

            this.isInitialized = true;
            console.log('CardEditor: Initialization complete');

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
        this.container.innerHTML = `
            <div class="card-editor-layout">
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
                                <div class="form-placeholder">
                                    <p class="tech-text-dim">ASSET MANAGEMENT // UNDER CONSTRUCTION</p>
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
                this.switchTab(event.target.dataset.tab);
            }
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

        ['#class-filter', '#type-filter', '#rarity-filter'].forEach(selector => {
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
        }
    }

    /**
     * 创建新卡牌
     */
    async createNewCard() {
        console.log('CardEditor: Creating new card...');

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
            keywords: [],
            tribe: [],
            canEvolve: false,
            evolveId: null,
            isNew: true
        };

        const editorTitle = this.container.querySelector('#editor-title');
        if (editorTitle) {
            editorTitle.textContent = '新建卡牌';
        }

        this.enableEditorButtons();
        await this.renderBasicInfoForm();
        this.updateCardPreview();

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
        console.log('CardEditor: Cleaning up...');
        this.isInitialized = false;
        this.currentCard = null;

        if (this.container) {
            this.container.innerHTML = '';
        }
    }
}

// 全局卡牌编辑器实例
let cardEditorInstance = null;

/**
 * 初始化卡牌编辑器
 */
export async function initializeCardEditor() {
    if (!cardEditorInstance) {
        cardEditorInstance = new CardEditor();
    }

    await cardEditorInstance.initialize();
    return cardEditorInstance;
}

/**
 * 获取卡牌编辑器实例
 */
export function getCardEditor() {
    return cardEditorInstance;
}

/**
 * 清理卡牌编辑器
 */
export function cleanupCardEditor() {
    if (cardEditorInstance) {
        cardEditorInstance.cleanup();
    }
}

// 扩展CardEditor类的原型，添加新方法
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

CardEditor.prototype.applyCardListFilters = function() {
    const searchInput = this.container.querySelector('#card-search');
    const classFilter = this.container.querySelector('#class-filter');
    const typeFilter = this.container.querySelector('#type-filter');
    const rarityFilter = this.container.querySelector('#rarity-filter');

    const searchText = (searchInput?.value || '').trim().toLowerCase();
    const selectedClass = classFilter?.value || '';
    const selectedType = typeFilter?.value || '';
    const selectedRarity = rarityFilter?.value || '';

    const baseCards = Array.isArray(this.filteredCardsData)
        ? this.filteredCardsData
        : (Array.isArray(this.allCardsData) ? this.allCardsData : []);

    const filtered = baseCards.filter(card => {
        const name = ((card.name ?? card.Name) ?? '').toString().toLowerCase();
        const cardClass = normalizeEnumName((card.class ?? card.Class) ?? '');
        const cardType = normalizeEnumName((card.type ?? card.Type) ?? '');
        const rarity = normalizeEnumName((card.rarity ?? card.Rarity) ?? '');

        if (searchText && !name.includes(searchText)) return false;
        if (selectedClass && !equalsIgnoreCase(cardClass, selectedClass)) return false;
        if (selectedType && !equalsIgnoreCase(cardType, selectedType)) return false;
        if (selectedRarity && !equalsIgnoreCase(rarity, selectedRarity)) return false;

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
    const rarity = normalizeEnumName(card.rarity || 'Common').toLowerCase();
    const cardClass = normalizeEnumName(card.cardClass || 'Neutral').toLowerCase();
    const cardType = normalizeEnumName(card.cardType || 'Spell').toLowerCase();
    const hasCountdownKeyword = (card.keywords || []).some(keyword => equalsIgnoreCase(keyword, 'Countdown'));
    const showCountdown = !isFollower && hasCountdownKeyword;
    const countdownValue = (card.countdown ?? card.Countdown) ?? 0;

    previewContainer.innerHTML = `
        <div class="preview-slate rarity-${rarity}">
            <!-- 顶部：核心识别区 -->
            <div class="slate-header">
                <div class="slate-identity">
                    <div class="identity-top">
                        <div class="slate-cost-badge-embedded">
                            <span class="cost-value">${card.cost}</span>
                        </div>
                        <h2 class="slate-name">${card.name || 'UNTITLED'}</h2>
                    </div>
                    <div class="slate-meta">
                        <span class="meta-tag rarity-tag">${card.rarity || 'Common'}</span>
                        <span class="meta-tag class-tag">${card.cardClass || 'Neutral'}</span>
                        <span class="meta-tag type-tag">${card.cardType || 'Spell'}</span>
                    </div>
                </div>
                
                <!-- 战斗数值 (仅随从显示，位于Header右下) -->
                ${isFollower ? `
                <div class="header-stats">
                    <div class="stat-mini attack">
                        <span class="stat-icon">⚔</span>
                        <span class="stat-value">${card.attack}</span>
                    </div>
                    <div class="stat-mini health">
                        <span class="stat-icon">♥</span>
                        <span class="stat-value">${card.health}</span>
                    </div>
                </div>
                ` : showCountdown ? `
                <div class="header-stats">
                    <div class="stat-mini countdown">
                        <span class="stat-icon">⏳</span>
                        <span class="stat-value">${countdownValue}</span>
                    </div>
                </div>
                ` : ''}
            </div>

            <!-- 背景插画层 -->
            <div class="slate-art-background">
                <!-- 这里将来可以动态插入图片，目前使用占位符或CSS背景 -->
            </div>

            <!-- 中部：数据与描述 -->
            <div class="slate-body">
                <!-- 描述区域 -->
                <div class="slate-content-area">
                    <div class="slate-description custom-scrollbar">${card.description || '<span class="text-dim">NO DATA DESCRIPTION...</span>'}</div>
                </div>
            </div>
            
            <!-- 装饰性全息扫描线 -->
            <div class="holo-scanline"></div>
            <div class="holo-grid-overlay"></div>
        </div>
    `;

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
            </div>
        `;
    }
};

CardEditor.prototype.renderBasicInfoForm = async function() {
    const basicTab = this.container.querySelector('#basic-tab');
    if (!basicTab) {
        console.error('CardEditor: Basic tab not found');
        return;
    }

    try {
        // 获取卡牌类型、职业、稀有度等枚举选项
        const [cardTypeOptions, cardClassOptions, cardRarityOptions] = await Promise.all([
            this.metadataManager.getCardTypeOptions(),
            this.metadataManager.getCardClassOptions(),
            this.metadataManager.getCardRarityOptions()
        ]);

        // 进化形态现在也可以编辑，移除只读限制
        const readOnlyAttr = '';
        const disabledAttr = '';

        // 创建表单HTML
        basicTab.innerHTML = `
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
                        <div class="form-grid form-grid-1" style="margin-bottom: var(--space-md);">
                            <div class="form-group">
                                <label class="form-label">COUNTDOWN</label>
                                <div class="number-stepper tech-stepper">
                                    <button type="button" class="stepper-btn decrease" data-target="card-countdown">-</button>
                                    <input type="number" id="card-countdown" class="form-input number-input tech-input" value="${this.currentCard.countdown ?? 0}" min="0" max="99">
                                    <button type="button" class="stepper-btn increase" data-target="card-countdown">+</button>
                                </div>
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
                            <select id="card-type" class="form-select tech-select" ${disabledAttr}>
                                ${this.generateSelectOptions(cardTypeOptions, this.currentCard.cardType)}
                            </select>
                        </div>
                        <div class="form-group">
                            <label class="form-label required">CLASS</label>
                            <select id="card-class" class="form-select tech-select" ${disabledAttr}>
                                ${this.generateSelectOptions(cardClassOptions, this.currentCard.cardClass)}
                            </select>
                        </div>
                        <div class="form-group">
                            <label class="form-label required">RARITY</label>
                            <select id="card-rarity" class="form-select tech-select" ${disabledAttr}>
                                ${this.generateSelectOptions(cardRarityOptions, this.currentCard.rarity)}
                            </select>
                        </div>
                    </div>
                </div>

            </div>
        `;

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

    // 绑定点击事件
    this.bindCardListEvents();
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
        'rarity': 'rarity'
    };

    const actualProperty = propertyMap[property] || property;
    this.currentCard[actualProperty] = value;

    // 更新预览
    this.updateCardPreview();

    console.log(`CardEditor: Updated ${actualProperty} to ${value}`);
};

CardEditor.prototype.updateStatsVisibility = function() {
    const statsContainer = this.container.querySelector('#stats-container');
    const countdownContainer = this.container.querySelector('#countdown-container');
    if (!statsContainer || !countdownContainer || !this.currentCard) return;

    // 只有随从卡牌显示攻击力和生命值
    const cardType = normalizeEnumName(this.currentCard.cardType);
    const shouldShowStats = equalsIgnoreCase(cardType, 'Follower');
    const shouldShowCountdown = equalsIgnoreCase(cardType, 'Amulet') || equalsIgnoreCase(cardType, 'Crest');
    statsContainer.style.display = shouldShowStats ? 'block' : 'none';
    countdownContainer.style.display = shouldShowCountdown ? 'block' : 'none';
};

CardEditor.prototype.buildCardUpsertDto = function(isCreate) {
    if (!this.currentCard) return null;

    const cardType = normalizeEnumName(this.currentCard.cardType);
    const isFollower = equalsIgnoreCase(cardType, 'Follower');
    const isCountdownType = equalsIgnoreCase(cardType, 'Amulet') || equalsIgnoreCase(cardType, 'Crest');
    const hasCountdownKeyword = (this.currentCard.keywords || []).some(keyword => equalsIgnoreCase(keyword, 'Countdown'));

    return {
        id: isCreate ? null : this.currentCard.id,
        name: this.currentCard.name?.trim() || '',
        type: cardType,
        rarity: normalizeEnumName(this.currentCard.rarity),
        card_set: 'Basic',
        class: normalizeEnumName(this.currentCard.cardClass),
        cost: Number(this.currentCard.cost) || 0,
        attack: isFollower ? (Number(this.currentCard.attack) || 0) : null,
        health: isFollower ? (Number(this.currentCard.health) || 1) : null,
        countdown: isCountdownType && hasCountdownKeyword ? (Number(this.currentCard.countdown) || 0) : null,
        description: this.currentCard.description || '',
        can_evolve: !!this.currentCard.canEvolve,
        evolve_id: this.currentCard.evolveId ?? null,
        keywords: Array.isArray(this.currentCard.keywords) ? this.currentCard.keywords : [],
        tribe: Array.isArray(this.currentCard.tribe) ? this.currentCard.tribe : []
    };
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

    const isCreate = !!this.currentCard.isNew || !!isSaveAs;
    const dto = this.buildCardUpsertDto(isCreate);

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

        const saved = await response.json();
        const savedId = saved?.id ?? saved?.cardId ?? saved?.CardId;

        await this.loadCardList();
        if (savedId != null) {
            await this.selectCard(savedId);
        }

        alert(isCreate ? '卡牌创建成功！' : '卡牌保存成功！');
    } catch (error) {
        console.error('CardEditor: Failed to save card:', error);
        alert(`保存失败: ${error.message}`);
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
        const response = await fetch(`/api/cards/${this.currentCard.id}`, { method: 'DELETE' });
        if (!response.ok && response.status !== 204) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        this.currentCard = null;
        this.disableEditorButtons();
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

CardEditor.prototype.loadCardList = async function() {
    const cardListContainer = this.container.querySelector('#card-list');
    if (!cardListContainer) {
        console.error('CardEditor: Card list container not found');
        return;
    }

    try {
        // 显示加载状态
        cardListContainer.innerHTML = `
            <div class="card-list-loading">
                <div class="loading-spinner"></div>
                <p>正在加载卡牌列表...</p>
            </div>
        `;

        console.log('CardEditor: Loading card list from API...');

        // 调用后端API获取卡牌列表
        const response = await fetch('/api/cards');
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const allCards = await response.json();
        console.log('CardEditor: Loaded all cards:', allCards);

        // 过滤卡牌：移除进化卡牌，只显示原始卡牌
        const filteredCards = this.filterCardsForList(allCards);
        console.log('CardEditor: Filtered cards for list:', filteredCards);
        console.log('CardEditor: Original card count:', allCards.length, 'Filtered count:', filteredCards.length);

        // 存储所有卡牌数据（包括进化卡牌）以供后续使用
        this.allCardsData = allCards;

        // 渲染过滤后的卡牌列表（支持搜索/筛选进一步过滤）
        this.filteredCardsData = filteredCards;
        this.applyCardListFilters();

    } catch (error) {
        console.error('CardEditor: Failed to load card list:', error);
        cardListContainer.innerHTML = `
            <div class="card-list-error">
                <div class="error-icon">⚠️</div>
                <p>加载卡牌列表失败</p>
                <small>${error.message}</small>
                <button class="retry-btn" onclick="cardEditorInstance.loadCardList()">重试</button>
            </div>
        `;
    }
};

CardEditor.prototype.filterCardsForList = function(cards) {
    if (!Array.isArray(cards)) {
        return [];
    }

    // 创建一个Set来存储所有进化卡牌的ID
    const evolvedCardIds = new Set();
    cards.forEach(card => {
        const evolveId = card.evolve_id || card.EvolveId;
        if (evolveId) {
            evolvedCardIds.add(evolveId);
        }
    });

    return cards.filter(card => {
        const cardId = card.id !== undefined ? card.id : card.CardId;
        const cardType = normalizeEnumName(card.type || card.Type || '');

        // 检查是否为进化卡牌：
        // 进化卡牌的特征：ID在evolvedCardIds集合中，且名称包含"进化"
        const isEvolvedCard = evolvedCardIds.has(cardId) &&
                             (card.name || '').includes('进化');

        // 主战者卡牌特殊处理：即使isCollectible为false也要显示
        const isLeaderCard = equalsIgnoreCase(cardType, 'Leader');

        // 显示条件：
        // 1. 非进化卡牌
        // 2. 或者是主战者卡牌
        return !isEvolvedCard || isLeaderCard;
    });
};

CardEditor.prototype.findEvolvedFormsForCard = function(baseCard) {
    console.log('CardEditor: Finding evolved forms for card:', baseCard);

    if (!this.allCardsData || !Array.isArray(this.allCardsData)) {
        console.log('CardEditor: No allCardsData available');
        return [];
    }

    const baseCardEvolveId = baseCard.evolve_id || baseCard.EvolveId || baseCard.evolveId;
    console.log('CardEditor: Base card evolve_id:', baseCardEvolveId);

    // 查找进化形态：
    // 基础卡牌的evolve_id指向进化形态的ID
    if (!baseCardEvolveId) {
        console.log('CardEditor: No evolve_id found, card cannot evolve');
        return []; // 如果没有evolve_id，说明不能进化
    }

    const evolvedForms = this.allCardsData.filter(card => {
        const cardId = card.id !== undefined ? card.id : card.CardId;

        // 查找ID等于基础卡牌evolve_id的卡牌
        const matches = cardId === baseCardEvolveId;
        if (matches) {
            console.log('CardEditor: Found evolved form:', card);
        }
        return matches;
    });

    console.log('CardEditor: Total evolved forms found:', evolvedForms.length);
    return evolvedForms;
};

CardEditor.prototype.selectCard = async function(cardId) {
    try {
        console.log('CardEditor: Selecting card:', cardId);

        // 移除之前的选中状态
        this.container.querySelectorAll('.card-list-item').forEach(item => {
            item.classList.remove('selected');
        });

        // 添加选中状态
        const selectedItem = this.container.querySelector(`[data-card-id="${cardId}"]`);
        if (selectedItem) {
            selectedItem.classList.add('selected');
        }

        // 获取卡牌详细信息
        const response = await fetch(`/api/cards/${cardId}`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const cardData = await response.json();
        console.log('CardEditor: Loaded card details:', cardData);

        // 转换为编辑器格式
        this.currentCard = this.convertApiCardToEditorFormat(cardData);

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

        // 更新预览
        this.updateCardPreview();

        // 加载进化形态信息
        this.loadEvolvedForms();

    } catch (error) {
        console.error('CardEditor: Failed to select card:', error);
        alert(`加载卡牌失败: ${error.message}`);
    }
};

CardEditor.prototype.loadEvolvedForms = function() {
    if (!this.currentCard) {
        console.log('CardEditor: No current card for evolution check');
        return;
    }

    console.log('CardEditor: Checking evolved forms for card:', this.currentCard);
    console.log('CardEditor: Card evolve_id:', this.currentCard.evolveId);

    // 查找当前卡牌的进化形态
    const evolvedForms = this.findEvolvedFormsForCard(this.currentCard);
    this.currentCardEvolvedForms = evolvedForms;
    this.currentEvolvedFormIndex = -1; // -1表示显示原始形态

    console.log('CardEditor: Found evolved forms for current card:', evolvedForms);
    console.log('CardEditor: Evolved forms count:', evolvedForms.length);

    // 更新进化形态导航UI
    this.updateEvolvedFormsNavigation();
};

CardEditor.prototype.updateEvolvedFormsNavigation = function() {
    console.log('CardEditor: Updating evolution navigation...');

    // 查找或创建进化形态导航区域
    let evolutionNav = this.container.querySelector('#evolution-navigation');
    console.log('CardEditor: Existing evolution nav:', evolutionNav);

    if (!this.currentCardEvolvedForms || this.currentCardEvolvedForms.length === 0) {
        console.log('CardEditor: No evolved forms, hiding navigation');
        // 如果没有进化形态，隐藏导航
        if (evolutionNav) {
            evolutionNav.style.display = 'none';
        }
        return;
    }

    console.log('CardEditor: Has evolved forms, showing navigation');

    // 如果导航不存在，创建它
    if (!evolutionNav) {
        const editorMain = this.container.querySelector('.editor-main');
        console.log('CardEditor: Editor main element:', editorMain);
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
        cost: (card.cost ?? card.Cost) ?? 0,
        attack: (card.attack ?? card.Attack) ?? 0,
        health: (card.health ?? card.Health) ?? 1,
        countdown: (card.countdown ?? card.Countdown) ?? 0,
        cardType: normalizeEnumName((card.type ?? card.Type) ?? 'Follower'),
        cardClass: normalizeEnumName((card.class ?? card.Class) ?? 'Neutral'),
        rarity: normalizeEnumName((card.rarity ?? card.Rarity) ?? 'Bronze'),
        keywords: card.keywords || card.Keywords || [],
        tribe: card.tribe || card.Tribe || [],
        canEvolve: (card.canEvolve ?? card.CanEvolve) ?? false,
        evolveId: (card.evolve_id ?? card.evolveId ?? card.EvolveId) ?? null,
        isNew: false // 标记为现有卡牌
    };
};

CardEditor.prototype.renderTagList = function(tags, tagType) {
    if (!Array.isArray(tags) || tags.length === 0) {
        return '<div class="tag-list-empty">暂无标签</div>';
    }

    return tags.map(tag => `
        <div class="tag-item" data-tag="${tag}" data-type="${tagType}">
            <span class="tag-text">${tag}</span>
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

    console.log(`CardEditor: Added ${tagType} tag: ${tagValue}`);
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

        console.log(`CardEditor: Removed ${tagType} tag: ${tagValue}`);
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

CardEditor.prototype.renderAbilitiesList = async function() {
    const abilitiesListContainer = this.container.querySelector('#abilities-list');
    if (!abilitiesListContainer || !this.currentCard) {
        return;
    }

    try {
        // 显示加载状态
        abilitiesListContainer.innerHTML = `
            <div class="abilities-loading">
                <div class="loading-spinner"></div>
                <p>正在加载能力列表...</p>
            </div>
        `;

        console.log('CardEditor: Loading abilities for card:', this.currentCard.id);

        // 获取卡牌的能力列表
        const response = await fetch(`/api/cards/${this.currentCard.id}/abilities`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const abilities = await response.json();
        console.log('CardEditor: Loaded abilities:', abilities);

        // 渲染能力列表
        this.renderAbilitiesListContent(abilities);

    } catch (error) {
        console.error('CardEditor: Failed to load abilities:', error);
        abilitiesListContainer.innerHTML = `
            <div class="abilities-error">
                <div class="error-icon">⚠️</div>
                <p>加载能力列表失败</p>
                <small>${error.message}</small>
                <button class="retry-btn" onclick="cardEditorInstance.renderAbilitiesList()">重试</button>
            </div>
        `;
    }
};

CardEditor.prototype.renderAbilitiesListContent = function(abilities) {
    const abilitiesListContainer = this.container.querySelector('#abilities-list');
    if (!abilitiesListContainer) return;

    if (!Array.isArray(abilities) || abilities.length === 0) {
        abilitiesListContainer.innerHTML = `
            <div class="abilities-placeholder">
                <p>暂无关联能力</p>
                <small>点击"添加能力"开始配置</small>
            </div>
        `;
        return;
    }

    // 生成能力列表HTML
    const abilitiesHTML = abilities.map(ability => `
        <div class="ability-item tech-panel-item" data-ability-id="${ability.id}">
            <div class="ability-header">
                <div class="ability-info">
                    <span class="ability-name tech-text-bold">${ability.name || '未命名能力'}</span>
                    <span class="ability-tag tech-tag">${formatAbilityKeywordsDisplay(ability)}</span>
                </div>
                <div class="ability-actions">
                    <button class="btn-tech-small edit-ability-btn" data-ability-id="${ability.id}">EDIT</button>
                    <button class="btn-tech-small btn-danger remove-ability-btn" data-ability-id="${ability.id}">DELETE</button>
                </div>
            </div>
            <div class="ability-details">
                <div class="ability-description tech-text-dim">${ability.description || '暂无描述'}</div>
                <div class="ability-config">
                    <span class="config-item tech-tag">TYPE: ${ability.type || 'Triggered'}</span>
                    <span class="config-item tech-tag">PRIORITY: ${ability.priority || 0}</span>
                    <span class="config-item tech-tag ${ability.isActive ? 'status-active' : 'status-inactive'}">
                        ${ability.isActive ? 'ACTIVE' : 'INACTIVE'}
                    </span>
                </div>
            </div>
        </div>
    `).join('');

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

console.log('CardEditor: Showing add ability modal for card:', this.currentCard.id);

try {
    // 创建模态框HTML - 使用与编辑模式相同的容器结构
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

    // 添加模态框到页面
    document.body.insertAdjacentHTML('beforeend', modalHTML);

    // 创建并渲染新的能力编辑器
    const editorContainer = document.getElementById('add-ability-editor-container');
    const abilityEditor = new AbilityEditor();

    // 默认空能力配置
    const defaultAbilityConfig = {
        cardId: this.currentCard.id,
        name: '',
        description: '',
        abilityKeywords: [],
        abilityType: 1,
        priority: 0,
        isActive: true,
        trigger: {
            eventTypes: [],
            relationType: 'Self',
            timing: 'On',
            validZones: []
        },
        conditions: [],
        targets: [],
        effects: []
    };

    await abilityEditor.render(
        editorContainer,
        defaultAbilityConfig,
        async (newConfig) => {
            if (newConfig === null) {
                // 用户取消
                this.closeAddAbilityModal();
            } else {
                // 用户确认添加
                await this.createNewAbilityFromConfig(newConfig);
                this.closeAddAbilityModal();
            }
        }
    );

    // 绑定关闭事件
    const closeBtn = document.getElementById('close-add-ability-modal');
    if (closeBtn) {
        closeBtn.addEventListener('click', () => this.closeAddAbilityModal());
    }

    // 点击遮罩关闭
    const modal = document.getElementById('add-ability-modal');
    modal.addEventListener('click', (e) => {
        if (e.target === modal) {
            this.closeAddAbilityModal();
        }
    });

} catch (error) {
    console.error('CardEditor: Failed to show add ability modal:', error);
    alert(`打开添加能力窗口失败: ${error.message}`);
}
};

/**
* 使用配置创建新能力
*/
CardEditor.prototype.createNewAbilityFromConfig = async function(config) {
try {
    console.log('CardEditor: Creating new ability from config:', config);

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

    const result = await response.json();
    console.log('CardEditor: Created ability:', result);

    // 刷新能力列表
    await this.renderAbilitiesList();
    
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
    const overlay = modal;

    [closeBtn, cancelBtn].forEach(btn => {
        if (btn) {
            btn.addEventListener('click', () => this.closeAddAbilityModal());
        }
    });

    overlay.addEventListener('click', (e) => {
        if (e.target === overlay) {
            this.closeAddAbilityModal();
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

CardEditor.prototype.showLinkExistingAbilityModal = async function() {
    if (!this.currentCard || !this.currentCard.id) {
        alert('请先选择并保存一张卡牌');
        return;
    }

    try {
        const modalHTML = `
            <div class="modal-overlay" id="link-ability-modal">
                <div class="modal-container ability-edit-container">
                    <div class="modal-header">
                        <h3>复制现有能力</h3>
                        <button class="modal-close-btn" id="close-link-ability-modal">×</button>
                    </div>
                    <div class="modal-content">
                        <div class="tech-text-dim" style="margin-bottom: 12px;">请选择一个能力并复制到当前卡牌</div>
                        <div class="abilities-list custom-scrollbar" id="link-abilities-list">
                            <div class="loading-spinner"></div>
                            <p>正在加载能力列表...</p>
                        </div>
                        <div class="modal-actions" style="margin-top: 12px; display: flex; justify-content: flex-end; gap: 8px;">
                            <button class="btn-tech btn-secondary" id="cancel-link-ability">取消</button>
                            <button class="btn-tech btn-primary" id="confirm-link-ability" disabled>确认复制</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHTML);

        const modal = document.getElementById('link-ability-modal');
        const closeBtn = document.getElementById('close-link-ability-modal');
        const cancelBtn = document.getElementById('cancel-link-ability');
        const confirmBtn = document.getElementById('confirm-link-ability');
        const listEl = document.getElementById('link-abilities-list');

        const close = () => this.closeLinkExistingAbilityModal();
        if (closeBtn) closeBtn.addEventListener('click', close);
        if (cancelBtn) cancelBtn.addEventListener('click', close);
        if (modal) {
            modal.addEventListener('click', (e) => {
                if (e.target === modal) close();
            });
        }

        this.selectedExistingAbilityId = null;

        const response = await fetch('/api/abilities');
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const abilities = await response.json();
        if (!Array.isArray(abilities) || abilities.length === 0) {
            listEl.innerHTML = `<div class="abilities-placeholder"><p>暂无可用能力</p></div>`;
            return;
        }

        listEl.innerHTML = abilities.map(a => `
            <div class="ability-item tech-panel-item" data-ability-id="${a.id}">
                <div class="ability-header">
                    <div class="ability-info">
                        <span class="ability-name tech-text-bold">${a.name || '未命名能力'}</span>
                        <span class="ability-tag tech-tag">${formatAbilityKeywordsDisplay(a)}</span>
                    </div>
                    <div class="ability-config">
                        <span class="config-item tech-tag">CARD: ${a.cardId ?? '-'}</span>
                    </div>
                </div>
                <div class="ability-details">
                    <div class="ability-description tech-text-dim">${a.description || ''}</div>
                </div>
            </div>
        `).join('');

        listEl.querySelectorAll('.ability-item').forEach(item => {
            item.addEventListener('click', () => {
                listEl.querySelectorAll('.ability-item').forEach(x => x.classList.remove('selected'));
                item.classList.add('selected');
                this.selectedExistingAbilityId = item.dataset.abilityId;
                if (confirmBtn) confirmBtn.disabled = false;
            });
        });

        if (confirmBtn) {
            confirmBtn.addEventListener('click', async () => {
                if (!this.selectedExistingAbilityId) return;
                confirmBtn.disabled = true;
                try {
                    await this.copyExistingAbilityById(this.selectedExistingAbilityId);
                    await this.renderAbilitiesList();
                    close();
                    alert('能力复制成功！');
                } catch (error) {
                    console.error('CardEditor: Failed to copy ability:', error);
                    alert(`复制能力失败: ${error.message}`);
                } finally {
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
        console.log('CardEditor: Loading existing abilities...');

        // 调用后端API获取所有能力
        const response = await fetch('/api/abilities');
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const abilities = await response.json();
        console.log('CardEditor: Loaded existing abilities:', abilities);

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
    const abilitiesHTML = abilities.map(ability => `
        <div class="existing-ability-item" data-ability-id="${ability.id}">
            <div class="ability-info">
                <div class="ability-name">${ability.name || '未命名能力'}</div>
                <div class="ability-tag">${formatAbilityKeywordsDisplay(ability)}</div>
                <div class="ability-description">${ability.description || '暂无描述'}</div>
            </div>
            <button class="btn-secondary select-ability-btn" data-ability-id="${ability.id}">选择</button>
        </div>
    `).join('');

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
    console.log('CardEditor: Selected existing ability:', abilityId);
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

    console.log('CardEditor: Creating new ability:', abilityConfig);

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

    const result = await response.json();
    console.log('CardEditor: Created ability:', result);
};

CardEditor.prototype.linkExistingAbility = async function() {
    if (!this.selectedExistingAbilityId) {
        alert('请选择一个现有能力');
        return;
    }

    console.log('CardEditor: Copying existing ability:', this.selectedExistingAbilityId);
    const result = await this.copyExistingAbilityById(this.selectedExistingAbilityId);
    console.log('CardEditor: Copied ability:', result);

    // 清除选中状态
    this.selectedExistingAbilityId = null;
};

CardEditor.prototype.copyExistingAbilityById = async function(abilityId) {
    if (!this.currentCard || !this.currentCard.id) {
        throw new Error('请先选择并保存一张卡牌');
    }

    const abilityResponse = await fetch(`/api/abilities/${abilityId}`);
    if (!abilityResponse.ok) {
        throw new Error(`HTTP ${abilityResponse.status}: ${abilityResponse.statusText}`);
    }

    const ability = await abilityResponse.json();

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

    return await createRes.json();
};

CardEditor.prototype.editAbility = async function(abilityId) {
    console.log('CardEditor: Editing ability:', abilityId);

    try {
        // 获取能力详细信息
        const response = await fetch(`/api/abilities/${abilityId}`);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const ability = await response.json();
        console.log('CardEditor: Loaded ability for editing:', ability);

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
        console.log('CardEditor: Removing ability:', abilityId);

        // 调用后端API移除能力关联
        const response = await fetch(`/api/cards/${this.currentCard.id}/abilities/${abilityId}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        console.log('CardEditor: Ability removed successfully');

        // 刷新能力列表
        await this.renderAbilitiesList();

    } catch (error) {
        console.error('CardEditor: Failed to remove ability:', error);
        alert(`移除能力失败: ${error.message}`);
    }
};

CardEditor.prototype.showAbilityEditModal = async function(ability) {
    console.log('CardEditor: Showing ability edit modal for:', ability);

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
        abilityKeywords: normalizeAbilityKeywords(ability),
        abilityType: ability.abilityType || 1,
        priority: ability.priority || 0,
        isActive: ability.isActive !== false,
        trigger: {
            eventTypes: [],
            relationType: '',
            timing: 'On',
            validZones: []
        },
        conditions: [],
        targets: [],
        effects: []
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

    // 点击遮罩关闭
    modal.addEventListener('click', (e) => {
        if (e.target === modal) {
            this.closeAbilityEditModal();
        }
    });

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
        console.log('CardEditor: Saving ability changes:', updatedConfig);

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

        const result = await response.json();
        console.log('CardEditor: Ability updated successfully:', result);

        // 刷新能力列表
        await this.renderAbilitiesList();

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

        selectElement.innerHTML = '<option value="">选择关键字</option>';
        Object.entries(basicKeywords).forEach(([key, value]) => {
            const option = document.createElement('option');
            option.value = key;
            option.textContent = value;
            selectElement.appendChild(option);
        });
    }
};

