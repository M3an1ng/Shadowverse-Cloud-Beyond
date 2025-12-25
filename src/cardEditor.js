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
                                            <button class="btn-tech btn-small" id="link-ability-btn">LINK EXISTING</button>
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

    previewContainer.innerHTML = `
        <div class="card-preview-content">
            <div class="card-preview-header">
                <div class="card-name">${this.currentCard.name || '未命名卡牌'}</div>
                <div class="card-cost">${this.currentCard.cost}</div>
            </div>
            <div class="card-preview-body">
                <div class="card-type">${this.currentCard.cardType} - ${this.currentCard.cardClass}</div>
                <div class="card-description">${this.currentCard.description || '暂无描述'}</div>
                ${equalsIgnoreCase(normalizeEnumName(this.currentCard.cardType), 'Follower') ? `
                    <div class="card-stats">
                        <span class="attack">${this.currentCard.attack}</span>
                        <span class="health">${this.currentCard.health}</span>
                    </div>
                ` : ''}
            </div>
            <div class="card-preview-footer">
                <div class="card-rarity">${this.currentCard.rarity}</div>
            </div>
        </div>
    `;
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
    if (!statsContainer || !this.currentCard) return;

    // 只有随从卡牌显示攻击力和生命值
    const shouldShowStats = equalsIgnoreCase(normalizeEnumName(this.currentCard.cardType), 'Follower');
    statsContainer.style.display = shouldShowStats ? 'block' : 'none';
};

CardEditor.prototype.buildCardUpsertDto = function(isCreate) {
    if (!this.currentCard) return null;

    const cardType = normalizeEnumName(this.currentCard.cardType);
    const isFollower = equalsIgnoreCase(cardType, 'Follower');

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
    const typeSelect = basicTab.querySelector('#card-type');
    const classSelect = basicTab.querySelector('#card-class');
    const raritySelect = basicTab.querySelector('#card-rarity');

    // 为每个字段添加验证事件
    [nameInput, costInput, attackInput, healthInput, typeSelect, classSelect, raritySelect].forEach(input => {
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

    if (equalsIgnoreCase(normalizeEnumName(this.currentCard.cardType), 'Follower')) {
        if (this.currentCard.attack < 0 || this.currentCard.attack > 99) {
            errors.push('攻击力必须在0-99之间');
        }

        if (this.currentCard.health < 1 || this.currentCard.health > 99) {
            errors.push('生命值必须在1-99之间');
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
                    <span class="ability-tag tech-tag">${ability.abilityTag || 'Unknown'}</span>
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
        abilityTag: '',
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
        abilityTag: config.abilityTag,
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
                        <h3>关联现有能力</h3>
                        <button class="modal-close-btn" id="close-link-ability-modal">×</button>
                    </div>
                    <div class="modal-content">
                        <div class="tech-text-dim" style="margin-bottom: 12px;">请选择一个能力并确认关联</div>
                        <div class="abilities-list custom-scrollbar" id="link-abilities-list">
                            <div class="loading-spinner"></div>
                            <p>正在加载能力列表...</p>
                        </div>
                        <div class="modal-actions" style="margin-top: 12px; display: flex; justify-content: flex-end; gap: 8px;">
                            <button class="btn-tech btn-secondary" id="cancel-link-ability">取消</button>
                            <button class="btn-tech btn-primary" id="confirm-link-ability" disabled>确认关联</button>
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
                        <span class="ability-tag tech-tag">${a.abilityTag || 'Unknown'}</span>
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
                    const linkRes = await fetch(`/api/cards/${this.currentCard.id}/abilities/${this.selectedExistingAbilityId}`, { method: 'POST' });
                    if (!linkRes.ok) {
                        throw new Error(`HTTP ${linkRes.status}: ${linkRes.statusText}`);
                    }
                    await this.renderAbilitiesList();
                    close();
                    alert('能力关联成功！');
                } catch (error) {
                    console.error('CardEditor: Failed to link ability:', error);
                    alert(`关联能力失败: ${error.message}`);
                } finally {
                    confirmBtn.disabled = false;
                }
            });
        }
    } catch (error) {
        console.error('CardEditor: Failed to show link ability modal:', error);
        alert(`打开关联窗口失败: ${error.message}`);
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
                <div class="ability-tag">${ability.abilityTag || 'Unknown'}</div>
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
        abilityTag: abilityTag,
        abilityType: abilityType ? parseInt(abilityType) : 1, // 默认为Triggered
        priority: priority,
        isActive: true,
        jsonConfig: JSON.stringify({
            name: name,
            description: description,
            abilityTag: abilityTag,
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

    console.log('CardEditor: Linking existing ability:', this.selectedExistingAbilityId);

    // 调用后端API关联能力到卡牌
    const response = await fetch(`/api/cards/${this.currentCard.id}/abilities/${this.selectedExistingAbilityId}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        }
    });

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    const result = await response.json();
    console.log('CardEditor: Linked ability:', result);

    // 清除选中状态
    this.selectedExistingAbilityId = null;
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
        abilityTag: ability.abilityTag || '',
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

CardEditor.prototype.bindAbilityEditModalEvents = function(ability) {
    const modal = document.getElementById('ability-edit-modal');
    if (!modal) return;

    // 关闭模态框事件
    const closeBtn = modal.querySelector('#close-ability-edit-modal');
    const cancelBtn = modal.querySelector('#cancel-ability-edit');

    [closeBtn, cancelBtn].forEach(btn => {
        if (btn) {
            btn.addEventListener('click', () => this.closeAbilityEditModal());
        }
    });

    // 标签页切换事件
    const tabButtons = modal.querySelectorAll('.ability-edit-tab-btn');
    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const tabName = btn.dataset.tab;
            this.switchAbilityEditTab(tabName);
        });
    });

    // 保存按钮事件
    const saveBtn = modal.querySelector('#save-ability-edit');
    if (saveBtn) {
        saveBtn.addEventListener('click', () => this.saveAbilityEdit(ability));
    }

    // 添加条件按钮事件
    const addConditionBtn = modal.querySelector('#add-condition-btn');
    if (addConditionBtn) {
        addConditionBtn.addEventListener('click', () => this.addConditionEditor());
    }

    // 添加目标按钮事件
    const addTargetBtn = modal.querySelector('#add-target-btn');
    if (addTargetBtn) {
        addTargetBtn.addEventListener('click', () => this.addTargetEditor());
    }

    // 添加效果按钮事件
    const addEffectBtn = modal.querySelector('#add-effect-btn');
    if (addEffectBtn) {
        addEffectBtn.addEventListener('click', () => this.addEffectEditor());
    }

    // 触发器配置事件
    const addEventTypeBtn = modal.querySelector('#add-event-type-btn');
    if (addEventTypeBtn) {
        addEventTypeBtn.addEventListener('click', () => this.addEventTypeEditor());
    }

    const addValidZoneBtn = modal.querySelector('#add-valid-zone-btn');
    if (addValidZoneBtn) {
        addValidZoneBtn.addEventListener('click', () => this.addValidZoneEditor());
    }
};

CardEditor.prototype.initializeAbilityEditModal = async function(ability) {
    try {
        // 加载能力标签选项
        const abilityTagOptions = await this.metadataManager.getAbilityTagOptions();
        const abilityTagSelect = document.getElementById('edit-ability-tag');

        if (abilityTagSelect && abilityTagOptions) {
            abilityTagOptions.forEach(option => {
                const optionElement = document.createElement('option');
                optionElement.value = option.value;
                optionElement.textContent = option.label;
                // abilityTag现在是数组，检查是否包含该值
                if (Array.isArray(ability.abilityTag) && ability.abilityTag.includes(option.value)) {
                    optionElement.selected = true;
                } else if (ability.abilityTag === option.value) {
                    optionElement.selected = true;
                }
                abilityTagSelect.appendChild(optionElement);
            });
        }

        // 解析并显示现有配置
        if (ability.jsonConfig) {
            try {
                // jsonConfig已经是对象，不需要JSON.parse
                const config = typeof ability.jsonConfig === 'string'
                    ? JSON.parse(ability.jsonConfig)
                    : ability.jsonConfig;
                this.loadAbilityConfig(config);
            } catch (error) {
                console.error('CardEditor: Failed to parse ability config:', error);
            }
        }

    } catch (error) {
        console.error('CardEditor: Failed to initialize ability edit modal:', error);
    }
};

CardEditor.prototype.switchAbilityEditTab = function(tabName) {
    const modal = document.getElementById('ability-edit-modal');
    if (!modal) return;

    // 移除所有活动状态
    modal.querySelectorAll('.ability-edit-tab-btn').forEach(btn => {
        btn.classList.remove('active');
    });
    modal.querySelectorAll('.ability-edit-panel').forEach(panel => {
        panel.classList.remove('active');
    });

    // 激活选中的标签页
    const tabButton = modal.querySelector(`[data-tab="${tabName}"]`);
    const tabPanel = modal.querySelector(`#${tabName}-edit-panel`);

    if (tabButton && tabPanel) {
        tabButton.classList.add('active');
        tabPanel.classList.add('active');
    }
};

CardEditor.prototype.closeAbilityEditModal = function() {
    const modal = document.getElementById('ability-edit-modal');
    if (modal) {
        modal.remove();
    }
};

CardEditor.prototype.loadAbilityConfig = function(config) {
    console.log('CardEditor: Loading ability config:', config);

    try {
        // 显示触发器配置
        if (config.trigger) {
            this.displayTriggerInfo(config.trigger);
        }

        // 显示条件配置
        if (config.conditions && Array.isArray(config.conditions)) {
            this.displayConditionsInfo(config.conditions);
        }

        // 显示目标配置
        if (config.targets && Array.isArray(config.targets)) {
            this.displayTargetsInfo(config.targets);
        }

        // 显示效果配置
        if (config.effects && Array.isArray(config.effects)) {
            this.displayEffectsInfo(config.effects);
        }

        console.log('CardEditor: Ability config loaded successfully');
    } catch (error) {
        console.error('CardEditor: Error loading ability config:', error);
    }
};

CardEditor.prototype.addConditionEditor = function() {
    console.log('CardEditor: Adding condition editor');

    const conditionsListContainer = document.querySelector('#conditions-list');
    if (!conditionsListContainer) return;

    // 移除占位符
    const placeholder = conditionsListContainer.querySelector('.conditions-placeholder');
    if (placeholder) {
        placeholder.remove();
    }

    // 创建条件编辑器
    const conditionId = 'condition_' + Math.random().toString(36).substr(2, 9);
    const conditionEditorHTML = `
        <div class="condition-item" data-condition-id="${conditionId}">
            <div class="condition-item-header">
                <div class="condition-item-title">条件表达式</div>
                <div class="condition-item-actions">
                    <button class="btn-icon remove-condition-btn" data-condition-id="${conditionId}" title="移除条件">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="condition-item-content">
                <div class="form-group">
                    <label class="form-label">条件表达式</label>
                    <div class="condition-expression-editor" id="condition-expr-${conditionId}">
                        <div class="loading-spinner"></div>
                        <p>正在初始化DSL编辑器...</p>
                    </div>
                </div>
                <div class="form-group">
                    <label class="checkbox-label">
                        <input type="checkbox" class="form-checkbox condition-enabled" checked>
                        <span>启用条件</span>
                    </label>
                </div>
            </div>
        </div>
    `;

    conditionsListContainer.insertAdjacentHTML('beforeend', conditionEditorHTML);

    // 绑定移除按钮事件
    const removeBtn = conditionsListContainer.querySelector(`[data-condition-id="${conditionId}"].remove-condition-btn`);
    if (removeBtn) {
        removeBtn.addEventListener('click', () => {
            const conditionItem = conditionsListContainer.querySelector(`[data-condition-id="${conditionId}"].condition-item`);
            if (conditionItem && confirm('确定要移除这个条件吗？')) {
                conditionItem.remove();

                // 如果没有条件了，显示占位符
                if (conditionsListContainer.children.length === 0) {
                    conditionsListContainer.innerHTML = `
                        <div class="conditions-placeholder">
                            <p>暂无条件</p>
                            <button class="btn-primary" id="add-condition-btn">添加条件</button>
                        </div>
                    `;

                    // 重新绑定添加按钮
                    const addBtn = conditionsListContainer.querySelector('#add-condition-btn');
                    if (addBtn) {
                        addBtn.addEventListener('click', () => this.addConditionEditor());
                    }
                }
            }
        });
    }

    // 初始化DSL编辑器
    this.initializeConditionDslEditor(conditionId);
};

CardEditor.prototype.initializeConditionDslEditor = async function(conditionId) {
    const editorContainer = document.getElementById(`condition-expr-${conditionId}`);
    if (!editorContainer) return;

    try {
        console.log('CardEditor: Initializing condition DSL editor for:', conditionId);

        // 使用DynamicUIRenderer创建DSL编辑器
        const dslEditor = this.uiRenderer.createValueConfigEditor(
            {
                name: 'conditionExpression',
                displayName: '条件表达式',
                typeName: 'ValueConfig'
            },
            '', // 初始值
            (value) => {
                // 缓存到DOM，后续构建配置时读取
                editorContainer.dataset.dslExpression = value || "";
                console.log('CardEditor: Condition expression updated:', value);
            }
        );

        // 清空容器并添加编辑器
        editorContainer.innerHTML = '';
        editorContainer.appendChild(dslEditor);

    } catch (error) {
        console.error('CardEditor: Failed to initialize condition DSL editor:', error);
        editorContainer.innerHTML = `
            <div class="error-message">
                初始化DSL编辑器失败: ${error.message}
            </div>
        `;
    }
};

CardEditor.prototype.addTargetEditor = function() {
    console.log('CardEditor: Adding target editor');

    const targetsListContainer = document.querySelector('#targets-list');
    if (!targetsListContainer) return;

    // 移除占位符
    const placeholder = targetsListContainer.querySelector('.targets-placeholder');
    if (placeholder) {
        placeholder.remove();
    }

    // 创建目标编辑器
    const targetId = 'target_' + Math.random().toString(36).substr(2, 9);
    const targetKey = `target${targetsListContainer.children.length + 1}`;
    const targetEditorHTML = `
        <div class="target-item" data-target-id="${targetId}" data-target-key="${targetKey}">
            <div class="target-item-header">
                <div class="target-item-title">目标: ${targetKey}</div>
                <div class="target-item-actions">
                    <button class="btn-icon remove-target-btn" data-target-id="${targetId}" title="移除目标">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="target-item-content">
                <div class="form-group">
                    <label class="form-label">目标池表达式</label>
                    <div class="target-pool-editor" id="target-pool-${targetId}">
                        <div class="loading-spinner"></div>
                        <p>正在初始化目标池编辑器...</p>
                    </div>
                </div>
                <div class="form-group">
                    <label class="form-label">失败处理方式</label>
                    <select class="form-select target-failure-handling">
                        <option value="AbortAbility" selected>中止能力</option>
                        <option value="SkipEffect">跳过效果</option>
                        <option value="UseDefault">使用默认值</option>
                    </select>
                </div>
                <div class="form-group">
                    <label class="checkbox-label">
                        <input type="checkbox" class="form-checkbox target-player-choice">
                        <span>需要玩家选择</span>
                    </label>
                </div>
                <div class="player-choice-config" style="display: none;">
                    <div class="form-grid form-grid-2">
                        <div class="form-group">
                            <label class="form-label">最小选择数量</label>
                            <input type="number" class="form-input target-min-count" value="1" min="0">
                        </div>
                        <div class="form-group">
                            <label class="form-label">最大选择数量</label>
                            <input type="number" class="form-input target-max-count" value="1" min="1">
                        </div>
                    </div>
                    <div class="form-group">
                        <label class="form-label">选择提示</label>
                        <input type="text" class="form-input target-prompt" placeholder="请选择目标...">
                    </div>
                </div>
            </div>
        </div>
    `;

    targetsListContainer.insertAdjacentHTML('beforeend', targetEditorHTML);

    // 绑定移除按钮事件
    const removeBtn = targetsListContainer.querySelector(`[data-target-id="${targetId}"].remove-target-btn`);
    if (removeBtn) {
        removeBtn.addEventListener('click', () => {
            const targetItem = targetsListContainer.querySelector(`[data-target-id="${targetId}"].target-item`);
            if (targetItem && confirm('确定要移除这个目标吗？')) {
                targetItem.remove();

                // 如果没有目标了，显示占位符
                if (targetsListContainer.children.length === 0) {
                    targetsListContainer.innerHTML = `
                        <div class="targets-placeholder">
                            <p>暂无目标</p>
                            <button class="btn-primary" id="add-target-btn">添加目标</button>
                        </div>
                    `;

                    // 重新绑定添加按钮
                    const addBtn = targetsListContainer.querySelector('#add-target-btn');
                    if (addBtn) {
                        addBtn.addEventListener('click', () => this.addTargetEditor());
                    }
                }
            }
        });
    }

    // 绑定玩家选择复选框事件
    const playerChoiceCheckbox = targetsListContainer.querySelector(`[data-target-id="${targetId}"] .target-player-choice`);
    const playerChoiceConfig = targetsListContainer.querySelector(`[data-target-id="${targetId}"] .player-choice-config`);

    if (playerChoiceCheckbox && playerChoiceConfig) {
        playerChoiceCheckbox.addEventListener('change', () => {
            playerChoiceConfig.style.display = playerChoiceCheckbox.checked ? 'block' : 'none';
        });
    }

    // 初始化目标池编辑器
    this.initializeTargetPoolEditor(targetId);
};

CardEditor.prototype.initializeTargetPoolEditor = async function(targetId) {
    const editorContainer = document.getElementById(`target-pool-${targetId}`);
    if (!editorContainer) return;

    try {
        console.log('CardEditor: Initializing target pool editor for:', targetId);

        // 使用DynamicUIRenderer创建DSL编辑器
        const dslEditor = this.uiRenderer.createValueConfigEditor(
            {
                name: 'sourcePoolExpression',
                displayName: '目标池表达式',
                typeName: 'ValueConfig'
            },
            'battlefield.allUnits', // 默认值
            (value) => {
                editorContainer.dataset.dslExpression = value || "";
                console.log('CardEditor: Target pool expression updated:', value);
            }
        );

        // 清空容器并添加编辑器
        editorContainer.innerHTML = '';
        editorContainer.appendChild(dslEditor);

    } catch (error) {
        console.error('CardEditor: Failed to initialize target pool editor:', error);
        editorContainer.innerHTML = `
            <div class="error-message">
                初始化目标池编辑器失败: ${error.message}
            </div>
        `;
    }
};

CardEditor.prototype.addEffectEditor = async function() {
    console.log('CardEditor: Adding effect editor');

    try {
        // 获取效果类型选项
        const effectTypes = await this.metadataManager.getEffectTypes();

        if (!effectTypes || effectTypes.length === 0) {
            alert('暂无可用的效果类型');
            return;
        }

        // 显示效果类型选择模态框
        this.showEffectTypeSelectionModal(effectTypes);

    } catch (error) {
        console.error('CardEditor: Failed to load effect types:', error);
        alert(`加载效果类型失败: ${error.message}`);
    }
};

CardEditor.prototype.showEffectTypeSelectionModal = function(effectTypes) {
    // 创建效果类型选择模态框
    const modalHTML = `
        <div class="modal-overlay" id="effect-type-selection-modal">
            <div class="modal-container">
                <div class="modal-header">
                    <h3>选择效果类型</h3>
                    <button class="modal-close-btn" id="close-effect-type-modal">×</button>
                </div>
                <div class="modal-content">
                    <div class="effect-types-grid">
                        ${effectTypes.map(effectType => `
                            <div class="effect-type-card" data-effect-type="${effectType.typeName}">
                                <div class="effect-type-header">
                                    <span class="effect-type-name">${effectType.displayName || effectType.typeName}</span>
                                </div>
                                <div class="effect-type-description">
                                    ${effectType.description || '暂无描述'}
                                </div>
                                <div class="effect-type-params">
                                    参数数量: ${effectType.parameters ? effectType.parameters.length : 0}
                                </div>
                            </div>
                        `).join('')}
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn-secondary" id="cancel-effect-type-selection">取消</button>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHTML);

    // 绑定事件
    this.bindEffectTypeSelectionEvents(effectTypes);
};

CardEditor.prototype.bindEffectTypeSelectionEvents = function(effectTypes) {
    const modal = document.getElementById('effect-type-selection-modal');
    if (!modal) return;

    // 关闭按钮
    const closeBtn = modal.querySelector('#close-effect-type-modal');
    const cancelBtn = modal.querySelector('#cancel-effect-type-selection');

    [closeBtn, cancelBtn].forEach(btn => {
        if (btn) {
            btn.addEventListener('click', () => {
                modal.remove();
            });
        }
    });

    // 效果类型卡片点击
    const effectTypeCards = modal.querySelectorAll('.effect-type-card');
    effectTypeCards.forEach(card => {
        card.addEventListener('click', () => {
            const effectTypeName = card.dataset.effectType;
            const effectType = effectTypes.find(et => et.typeName === effectTypeName);

            if (effectType) {
                modal.remove();
                this.createEffectEditor(effectType);
            }
        });
    });
};

CardEditor.prototype.createEffectEditor = function(effectType) {
    console.log('CardEditor: Creating effect editor for:', effectType);

    const effectsListContainer = document.querySelector('#effects-list');
    if (!effectsListContainer) return;

    // 移除占位符
    const placeholder = effectsListContainer.querySelector('.effects-placeholder');
    if (placeholder) {
        placeholder.remove();
    }

    // 创建效果编辑器
    const effectId = 'effect_' + Math.random().toString(36).substr(2, 9);
    const effectEditorHTML = `
        <div class="effect-item" data-effect-id="${effectId}" data-effect-type="${effectType.typeName}">
            <div class="effect-item-header">
                <div class="effect-item-title">${effectType.displayName || effectType.typeName}</div>
                <div class="effect-item-actions">
                    <button class="btn-icon remove-effect-btn" data-effect-id="${effectId}" title="移除效果">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="effect-item-content">
                <div class="effect-parameters-container" id="effect-params-${effectId}">
                    <div class="loading-spinner"></div>
                    <p>正在加载参数配置...</p>
                </div>
            </div>
        </div>
    `;

    effectsListContainer.insertAdjacentHTML('beforeend', effectEditorHTML);

    // 绑定移除按钮事件
    const removeBtn = effectsListContainer.querySelector(`[data-effect-id="${effectId}"].remove-effect-btn`);
    if (removeBtn) {
        removeBtn.addEventListener('click', () => {
            const effectItem = effectsListContainer.querySelector(`[data-effect-id="${effectId}"].effect-item`);
            if (effectItem && confirm('确定要移除这个效果吗？')) {
                effectItem.remove();

                // 如果没有效果了，显示占位符
                if (effectsListContainer.children.length === 0) {
                    effectsListContainer.innerHTML = `
                        <div class="effects-placeholder">
                            <p>暂无效果</p>
                            <button class="btn-primary" id="add-effect-btn">添加效果</button>
                        </div>
                    `;

                    // 重新绑定添加按钮
                    const addBtn = effectsListContainer.querySelector('#add-effect-btn');
                    if (addBtn) {
                        addBtn.addEventListener('click', () => this.addEffectEditor());
                    }
                }
            }
        });
    }

    // 渲染效果参数
    this.renderEffectParameters(effectType, effectId);
};

CardEditor.prototype.renderEffectParameters = async function(effectType, effectId) {
    const parametersContainer = document.getElementById(`effect-params-${effectId}`);
    if (!parametersContainer) return;

    try {
        console.log('CardEditor: Rendering effect parameters for:', effectType);

        // 使用DynamicUIRenderer渲染参数表单
        const parametersForm = this.uiRenderer.renderEffectParametersForm(
            effectType.typeName,
            {}, // 初始参数值
            (updatedParams) => {
                console.log('CardEditor: Effect parameters updated:', updatedParams);
                // 缓存到容器数据集，后续保存时提取
                parametersContainer.dataset.effectParams = JSON.stringify(updatedParams || {});
            }
        );

        // 清空容器并添加表单
        parametersContainer.innerHTML = '';
        parametersContainer.appendChild(parametersForm);

    } catch (error) {
        console.error('CardEditor: Failed to render effect parameters:', error);
        parametersContainer.innerHTML = `
            <div class="error-message">
                渲染参数失败: ${error.message}
            </div>
        `;
    }
};

CardEditor.prototype.addEventTypeEditor = async function() {
    console.log('CardEditor: Adding event type editor');

    try {
        // 获取事件类型选项
        const eventTypes = await this.metadataManager.getEventTypes();

        if (!eventTypes || eventTypes.length === 0) {
            alert('暂无可用的事件类型');
            return;
        }

        // 显示事件类型选择模态框
        this.showEventTypeSelectionModal(eventTypes);

    } catch (error) {
        console.error('CardEditor: Failed to load event types:', error);
        alert(`加载事件类型失败: ${error.message}`);
    }
};

CardEditor.prototype.showEventTypeSelectionModal = function(eventTypes) {
    const modalHTML = `
        <div class="modal-overlay" id="event-type-selection-modal">
            <div class="modal-container">
                <div class="modal-header">
                    <h3>选择事件类型</h3>
                    <button class="modal-close-btn" id="close-event-type-modal">×</button>
                </div>
                <div class="modal-content">
                    <div class="event-types-grid">
                        ${eventTypes.map(eventType => `
                            <div class="event-type-card" data-event-type="${eventType.value}">
                                <div class="event-type-header">
                                    <span class="event-type-name">${eventType.label}</span>
                                </div>
                                <div class="event-type-description">
                                    ${eventType.description || '暂无描述'}
                                </div>
                            </div>
                        `).join('')}
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn-secondary" id="cancel-event-type-selection">取消</button>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHTML);

    // 绑定事件
    this.bindEventTypeSelectionEvents(eventTypes);
};

CardEditor.prototype.bindEventTypeSelectionEvents = function(eventTypes) {
    const modal = document.getElementById('event-type-selection-modal');
    if (!modal) return;

    // 关闭按钮
    const closeBtn = modal.querySelector('#close-event-type-modal');
    const cancelBtn = modal.querySelector('#cancel-event-type-selection');

    [closeBtn, cancelBtn].forEach(btn => {
        if (btn) {
            btn.addEventListener('click', () => {
                modal.remove();
            });
        }
    });

    // 事件类型卡片点击
    const eventTypeCards = modal.querySelectorAll('.event-type-card');
    eventTypeCards.forEach(card => {
        card.addEventListener('click', () => {
            const eventTypeName = card.dataset.eventType;
            const eventType = eventTypes.find(et => et.value === eventTypeName);

            if (eventType) {
                modal.remove();
                this.addEventTypeToTrigger(eventType);
            }
        });
    });
};

CardEditor.prototype.addEventTypeToTrigger = function(eventType) {
    const eventTypesContainer = document.getElementById('event-types-container');
    if (!eventTypesContainer) return;

    // 移除占位符
    const placeholder = eventTypesContainer.querySelector('.event-types-placeholder');
    if (placeholder) {
        placeholder.remove();
    }

    // 创建事件类型项
    const eventTypeId = 'event_' + Math.random().toString(36).substr(2, 9);
    const eventTypeHTML = `
        <div class="event-type-item" data-event-id="${eventTypeId}">
            <span class="event-type-label">${eventType.label}</span>
            <button class="btn-icon remove-event-type-btn" data-event-id="${eventTypeId}" title="移除事件类型">
                <span>×</span>
            </button>
        </div>
    `;

    eventTypesContainer.insertAdjacentHTML('beforeend', eventTypeHTML);

    // 绑定移除按钮事件
    const removeBtn = eventTypesContainer.querySelector(`[data-event-id="${eventTypeId}"].remove-event-type-btn`);
    if (removeBtn) {
        removeBtn.addEventListener('click', () => {
            const eventItem = eventTypesContainer.querySelector(`[data-event-id="${eventTypeId}"].event-type-item`);
            if (eventItem && confirm('确定要移除这个事件类型吗？')) {
                eventItem.remove();

                // 如果没有事件类型了，显示占位符
                if (eventTypesContainer.children.length === 0) {
                    eventTypesContainer.innerHTML = `
                        <div class="event-types-placeholder">
                            <p>暂无监听事件</p>
                            <button class="btn-primary" id="add-event-type-btn">添加事件类型</button>
                        </div>
                    `;

                    // 重新绑定添加按钮
                    const addBtn = eventTypesContainer.querySelector('#add-event-type-btn');
                    if (addBtn) {
                        addBtn.addEventListener('click', () => this.addEventTypeEditor());
                    }
                }
            }
        });
    }
};

CardEditor.prototype.addValidZoneEditor = async function() {
    console.log('CardEditor: Adding valid zone editor');

    try {
        // 获取卡牌区域选项
        const cardZones = await this.metadataManager.getCardZones();

        if (!cardZones || cardZones.length === 0) {
            alert('暂无可用的卡牌区域');
            return;
        }

        // 显示区域选择模态框
        this.showValidZoneSelectionModal(cardZones);

    } catch (error) {
        console.error('CardEditor: Failed to load card zones:', error);
        alert(`加载卡牌区域失败: ${error.message}`);
    }
};

CardEditor.prototype.showValidZoneSelectionModal = function(cardZones) {
    const modalHTML = `
        <div class="modal-overlay" id="valid-zone-selection-modal">
            <div class="modal-container">
                <div class="modal-header">
                    <h3>选择有效区域</h3>
                    <button class="modal-close-btn" id="close-valid-zone-modal">×</button>
                </div>
                <div class="modal-content">
                    <div class="valid-zones-grid">
                        ${cardZones.map(zone => `
                            <div class="valid-zone-card" data-zone="${zone.value}">
                                <div class="valid-zone-header">
                                    <span class="valid-zone-name">${zone.label}</span>
                                </div>
                                <div class="valid-zone-description">
                                    ${zone.description || '暂无描述'}
                                </div>
                            </div>
                        `).join('')}
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn-secondary" id="cancel-valid-zone-selection">取消</button>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', modalHTML);

    // 绑定事件
    this.bindValidZoneSelectionEvents(cardZones);
};

CardEditor.prototype.bindValidZoneSelectionEvents = function(cardZones) {
    const modal = document.getElementById('valid-zone-selection-modal');
    if (!modal) return;

    // 关闭按钮
    const closeBtn = modal.querySelector('#close-valid-zone-modal');
    const cancelBtn = modal.querySelector('#cancel-valid-zone-selection');

    [closeBtn, cancelBtn].forEach(btn => {
        if (btn) {
            btn.addEventListener('click', () => {
                modal.remove();
            });
        }
    });

    // 区域卡片点击
    const zoneCards = modal.querySelectorAll('.valid-zone-card');
    zoneCards.forEach(card => {
        card.addEventListener('click', () => {
            const zoneName = card.dataset.zone;
            const zone = cardZones.find(z => z.value === zoneName);

            if (zone) {
                modal.remove();
                this.addValidZoneToTrigger(zone);
            }
        });
    });
};

CardEditor.prototype.addValidZoneToTrigger = function(zone) {
    const validZonesContainer = document.getElementById('valid-zones-container');
    if (!validZonesContainer) return;

    // 移除占位符
    const placeholder = validZonesContainer.querySelector('.valid-zones-placeholder');
    if (placeholder) {
        placeholder.remove();
    }

    // 创建区域项
    const zoneId = 'zone_' + Math.random().toString(36).substr(2, 9);
    const zoneHTML = `
        <div class="valid-zone-item" data-zone-id="${zoneId}">
            <span class="valid-zone-label">${zone.label}</span>
            <button class="btn-icon remove-valid-zone-btn" data-zone-id="${zoneId}" title="移除有效区域">
                <span>×</span>
            </button>
        </div>
    `;

    validZonesContainer.insertAdjacentHTML('beforeend', zoneHTML);

    // 绑定移除按钮事件
    const removeBtn = validZonesContainer.querySelector(`[data-zone-id="${zoneId}"].remove-valid-zone-btn`);
    if (removeBtn) {
        removeBtn.addEventListener('click', () => {
            const zoneItem = validZonesContainer.querySelector(`[data-zone-id="${zoneId}"].valid-zone-item`);
            if (zoneItem && confirm('确定要移除这个有效区域吗？')) {
                zoneItem.remove();

                // 如果没有区域了，显示占位符
                if (validZonesContainer.children.length === 0) {
                    validZonesContainer.innerHTML = `
                        <div class="valid-zones-placeholder">
                            <p>所有区域有效</p>
                            <button class="btn-secondary" id="add-valid-zone-btn">限制区域</button>
                        </div>
                    `;

                    // 重新绑定添加按钮
                    const addBtn = validZonesContainer.querySelector('#add-valid-zone-btn');
                    if (addBtn) {
                        addBtn.addEventListener('click', () => this.addValidZoneEditor());
                    }
                }
            }
        });
    }
};

CardEditor.prototype.saveAbilityEdit = async function(ability) {
    console.log('CardEditor: Saving ability edit:', ability);

    try {
        // 收集所有配置数据
        const abilityConfig = this.buildAbilityConfig(ability);

        console.log('CardEditor: Built ability config:', abilityConfig);

        // 调用后端API保存
        const response = await fetch(`/api/abilities/${ability.id}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(abilityConfig)
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const result = await response.json();
        console.log('CardEditor: Ability saved successfully:', result);

        // 关闭模态框
        this.closeAbilityEditModal();

        // 刷新能力列表
        await this.renderAbilitiesList();

        alert('能力保存成功！');

    } catch (error) {
        console.error('CardEditor: Failed to save ability:', error);
        alert(`保存能力失败: ${error.message}`);
    }
};

CardEditor.prototype.buildAbilityConfig = function(ability) {
    const modal = document.getElementById('ability-edit-modal');
    if (!modal) {
        throw new Error('Ability edit modal not found');
    }

    // 收集基础信息
    const name = modal.querySelector('#edit-ability-name').value.trim();
    const description = modal.querySelector('#edit-ability-description').value.trim();
    const abilityTag = modal.querySelector('#edit-ability-tag').value;
    const priority = parseInt(modal.querySelector('#edit-ability-priority').value) || 0;
    const isActive = modal.querySelector('#edit-ability-active').checked;

    // 收集触发器配置
    const triggerConfig = this.buildTriggerConfig(modal);

    // 收集条件配置
    const conditions = this.buildConditionsConfig(modal);

    // 收集目标配置
    const targets = this.buildTargetsConfig(modal);

    // 收集效果配置
    const effects = this.buildEffectsConfig(modal);

    // 构建完整的AbilityConfig
    const abilityConfig = {
        id: ability.id,
        cardId: this.currentCard.id,
        name: name,
        description: description,
        abilityTag: abilityTag,
        abilityType: ability.abilityType || 1, // 默认为Triggered
        priority: priority,
        isActive: isActive,
        jsonConfig: JSON.stringify({
            name: name,
            description: description,
            abilityTag: abilityTag,
            type: "Triggered",
            trigger: triggerConfig,
            preConditions: [], // TODO: 实现预条件配置
            conditions: conditions,
            targets: targets,
            effects: effects,
            isActive: isActive,
            version: "1.0"
        })
    };

    return abilityConfig;
};

CardEditor.prototype.buildTriggerConfig = function(modal) {
    const timing = modal.querySelector('#trigger-timing').value;
    const relationType = modal.querySelector('#trigger-relation-type').value;
    const isEnabled = modal.querySelector('#trigger-enabled').checked;

    // 收集事件类型 - 需要转换为EventType枚举值
    const eventTypeItems = modal.querySelectorAll('.event-type-item');
    const eventTypes = Array.from(eventTypeItems).map(item => {
        const eventTypeLabel = item.querySelector('.event-type-label').textContent;
        // 这里需要将显示标签转换回EventType枚举值
        return this.convertEventTypeLabelToEnum(eventTypeLabel);
    });

    // 收集有效区域 - 需要转换为CardZone枚举值
    const validZoneItems = modal.querySelectorAll('.valid-zone-item');
    const validZones = Array.from(validZoneItems).map(item => {
        const zoneLabel = item.querySelector('.valid-zone-label').textContent;
        // 这里需要将显示标签转换回CardZone枚举值
        return this.convertCardZoneLabelToEnum(zoneLabel);
    });

    return {
        id: 'trigger_' + Math.random().toString(36).substr(2, 9),
        eventTypes: eventTypes, // List<EventType>
        timing: timing, // TriggerTiming枚举
        validZones: validZones, // List<CardZone>
        relationType: relationType, // TriggerRelationType枚举
        isEnabled: isEnabled
    };
};

CardEditor.prototype.convertEventTypeLabelToEnum = function(label) {
    // 从MetadataManager获取事件类型映射
    try {
        const eventTypes = this.metadataManager.getEventTypes();
        const eventType = eventTypes.find(et => et.label === label);
        return eventType ? eventType.value : label;
    } catch (error) {
        console.error('CardEditor: Failed to convert event type label:', error);
        return label;
    }
};

CardEditor.prototype.convertCardZoneLabelToEnum = function(label) {
    // 从MetadataManager获取卡牌区域映射
    try {
        const cardZones = this.metadataManager.getCardZones();
        const cardZone = cardZones.find(cz => cz.label === label);
        return cardZone ? cardZone.value : label;
    } catch (error) {
        console.error('CardEditor: Failed to convert card zone label:', error);
        return label;
    }
};

CardEditor.prototype.buildConditionsConfig = function(modal) {
    const conditionItems = modal.querySelectorAll('.condition-item');
    return Array.from(conditionItems).map(item => {
        const conditionId = item.dataset.conditionId;
        const isEnabled = item.querySelector('.condition-enabled').checked;

        // 从DSL编辑器中获取实际表达式
        const conditionEditor = item.querySelector(`#condition-expr-${conditionId}`);
        const expression = this.extractDslEditorValue(conditionEditor) || "true";

        return {
            id: conditionId,
            expression: expression, // DSL表达式字符串
            isEnabled: isEnabled
        };
    });
};

CardEditor.prototype.buildTargetsConfig = function(modal) {
    const targetItems = modal.querySelectorAll('.target-item');
    const targets = {};

    Array.from(targetItems).forEach((item, index) => {
        const targetKey = item.dataset.targetKey || `target${index + 1}`;
        const failureHandling = item.querySelector('.target-failure-handling').value;
        const needsPlayerChoice = item.querySelector('.target-player-choice').checked;

        // 从DSL编辑器中获取实际目标池表达式
        const targetPoolEditor = item.querySelector(`#target-pool-${item.dataset.targetId}`);
        const sourcePoolExpression = this.extractDslEditorValue(targetPoolEditor) || "battlefield.allUnits";

        let playerChoiceOptions = null;
        if (needsPlayerChoice) {
            const minCount = parseInt(item.querySelector('.target-min-count').value) || 1;
            const maxCount = parseInt(item.querySelector('.target-max-count').value) || 1;
            const prompt = item.querySelector('.target-prompt').value || '请选择目标...';

            playerChoiceOptions = {
                isRequired: true,
                prompt: prompt,
                canCancel: false,
                autoSelect: false,
                minCount: minCount,
                maxCount: maxCount
            };
        }

        // 构建完整的CardQueryConfig结构
        targets[targetKey] = {
            query: {
                sourcePoolExpression: sourcePoolExpression,
                operations: [] // List<IQueryOperation> - 目前为空，后续可扩展
            },
            onFailure: this.convertFailureHandlingToEnum(failureHandling),
            playerChoiceOptions: playerChoiceOptions
        };
    });

    return targets;
};

CardEditor.prototype.convertFailureHandlingToEnum = function(value) {
    // 将字符串值转换为FailureHandling枚举
    const failureHandlingMapping = {
        'AbortAbility': 'AbortAbility',
        'SkipEffect': 'SkipEffect',
        'UseDefault': 'UseDefault'
    };

    return failureHandlingMapping[value] || 'AbortAbility';
};

CardEditor.prototype.buildEffectsConfig = function(modal) {
    const effectItems = modal.querySelectorAll('.effect-item');
    return Array.from(effectItems).map((item, index) => {
        const effectType = item.dataset.effectType || item.querySelector('.effect-item-title')?.textContent || '';

        // 从效果参数容器中提取实际参数
        const parametersContainer = item.querySelector('.effect-parameters-container');
        const parameters = this.extractEffectParameters(parametersContainer);

        return {
            type: effectType,
            executionOrder: index,
            parameters: parameters, // Dictionary<string, ValueConfig>格式
            targetQuery: null, // 效果级别的目标查询
            outputs: null // 效果输出配置
        };
    });
};

CardEditor.prototype.extractEffectParameters = function(parametersContainer) {
    if (!parametersContainer) {
        return {};
    }

    // 优先使用数据集中缓存的参数
    if (parametersContainer.dataset && parametersContainer.dataset.effectParams) {
        try {
            const cached = JSON.parse(parametersContainer.dataset.effectParams);
            if (cached && typeof cached === 'object') {
                return cached;
            }
        } catch {
            // ignore parse error, fall back to DOM extraction
        }
    }

    const parameters = {};

    // 遍历所有参数表单组件
    const formGroups = parametersContainer.querySelectorAll('.form-group');
    formGroups.forEach(group => {
        // 优先使用参数名标记
        const paramName = group.dataset.paramName || group.querySelector('.form-label')?.textContent?.trim();
        if (!paramName) return;
        let paramValue = null;

        // 根据不同的输入类型提取值
        const textInput = group.querySelector('.form-input[type="text"]');
        const numberInput = group.querySelector('.form-input[type="number"]');
        const checkbox = group.querySelector('.form-checkbox');
        const select = group.querySelector('.form-select');
        const textarea = group.querySelector('.form-textarea');
        const dslEditor = group.querySelector('.dsl-editor');

        if (textInput) {
            paramValue = {
                type: "Literal",
                value: textInput.value
            };
        } else if (numberInput) {
            paramValue = {
                type: "Literal",
                value: parseFloat(numberInput.value) || 0
            };
        } else if (checkbox) {
            paramValue = {
                type: "Literal",
                value: checkbox.checked
            };
        } else if (select) {
            paramValue = {
                type: "Literal",
                value: select.value
            };
        } else if (textarea) {
            paramValue = {
                type: "Literal",
                value: textarea.value
            };
        } else if (group.querySelector('.value-config-editor')) {
            // ValueConfig编辑器，直接取缓存或实例
            let valueConfig = null;
            if (group.dataset && group.dataset.valueConfig) {
                try {
                    valueConfig = JSON.parse(group.dataset.valueConfig);
                } catch {
                    valueConfig = null;
                }
            }
            if (!valueConfig) {
                const vcInstance = group._valueConfig || group.querySelector('.value-config-editor')._valueConfig;
                if (vcInstance) valueConfig = vcInstance;
            }
            if (valueConfig) {
                paramValue = valueConfig;
            }
        } else if (dslEditor) {
            // DSL编辑器的值需要特殊处理
            paramValue = {
                type: "Expression",
                expression: this.extractDslEditorValue(dslEditor)
            };
        }

        if (paramValue) {
            parameters[paramName] = paramValue;
        }
    });

    return parameters;
};

CardEditor.prototype.extractDslEditorValue = function(dslEditor) {
    if (!dslEditor) return null;

    // 优先从数据集读取（由 onChange 同步）
    if (dslEditor.dataset && typeof dslEditor.dataset.dslExpression !== 'undefined') {
        return dslEditor.dataset.dslExpression;
    }

    // 其次尝试挂载的 DslEditor 实例
    if (dslEditor._dslEditor && typeof dslEditor._dslEditor.expression === 'string') {
        return dslEditor._dslEditor.expression;
    }

    // 最后回退到预览区域文本（旧实现）
    const previewContent = dslEditor.querySelector('.dsl-preview-content code');
    return previewContent ? previewContent.textContent : null;
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

// 显示触发器信息
CardEditor.prototype.displayTriggerInfo = function(trigger) {
    const triggerPanel = document.getElementById('trigger-edit-panel');
    if (!triggerPanel) return;

    // 查找并填充事件类型选择器
    const eventTypeSelect = triggerPanel.querySelector('#trigger-event-type');
    if (eventTypeSelect && trigger.eventTypes && trigger.eventTypes.length > 0) {
        // 清空现有选项
        eventTypeSelect.innerHTML = '<option value="">选择事件类型</option>';

        // 添加当前事件类型选项
        trigger.eventTypes.forEach(eventType => {
            const option = document.createElement('option');
            option.value = eventType;
            option.textContent = eventType;
            option.selected = true;
            eventTypeSelect.appendChild(option);
        });
    }

    // 查找并填充关系类型选择器
    const relationTypeSelect = triggerPanel.querySelector('#trigger-relation-type');
    if (relationTypeSelect && trigger.relationType) {
        // 清空现有选项
        relationTypeSelect.innerHTML = '<option value="">选择关系类型</option>';

        // 添加当前关系类型选项
        const option = document.createElement('option');
        option.value = trigger.relationType;
        option.textContent = trigger.relationType;
        option.selected = true;
        relationTypeSelect.appendChild(option);
    }

    // 显示配置摘要信息
    const triggerInfo = document.createElement('div');
    triggerInfo.className = 'trigger-config-summary';
    triggerInfo.innerHTML = `
        <div class="config-summary-item">
            <strong>已配置事件类型:</strong> ${trigger.eventTypes ? trigger.eventTypes.join(', ') : '未设置'}
        </div>
        <div class="config-summary-item">
            <strong>已配置关系类型:</strong> ${trigger.relationType || '未设置'}
        </div>
    `;

    // 移除现有的触发器信息
    const existingInfo = triggerPanel.querySelector('.trigger-config-summary');
    if (existingInfo) {
        existingInfo.remove();
    }

    triggerPanel.appendChild(triggerInfo);
};

// 显示条件信息
CardEditor.prototype.displayConditionsInfo = function(conditions) {
    const conditionsPanel = document.getElementById('conditions-edit-panel');
    if (!conditionsPanel) return;

    const conditionsList = conditionsPanel.querySelector('#conditions-list');
    if (!conditionsList) return;

    // 移除占位符
    const placeholder = conditionsList.querySelector('.conditions-placeholder');
    if (placeholder) {
        placeholder.remove();
    }

    // 清空现有条件
    conditionsList.innerHTML = '';

    if (conditions.length === 0) {
        conditionsList.innerHTML = `
            <div class="conditions-placeholder">
                <p>暂无条件</p>
                <button class="btn-primary" id="add-condition-btn">添加条件</button>
            </div>
        `;
        return;
    }

    // 为每个条件创建可编辑的UI组件
    conditions.forEach((condition, index) => {
        const conditionId = `condition_${index}`;
        const conditionItem = document.createElement('div');
        conditionItem.className = 'condition-item';
        conditionItem.setAttribute('data-condition-id', conditionId);

        conditionItem.innerHTML = `
            <div class="condition-item-header">
                <div class="condition-item-title">条件 ${index + 1}</div>
                <div class="condition-item-actions">
                    <button class="btn-icon remove-condition-btn" data-condition-id="${conditionId}" title="移除条件">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="condition-item-content">
                <div class="form-group">
                    <label>条件类型:</label>
                    <input type="text" class="form-control" value="${condition.type || ''}" readonly>
                </div>
                <div class="form-group">
                    <label>条件配置:</label>
                    <textarea class="form-control" rows="4" readonly>${JSON.stringify(condition, null, 2)}</textarea>
                </div>
            </div>
        `;

        conditionsList.appendChild(conditionItem);
    });

    // 添加"添加条件"按钮
    const addButton = document.createElement('div');
    addButton.className = 'add-condition-container';
    addButton.innerHTML = '<button class="btn-primary" id="add-condition-btn">添加条件</button>';
    conditionsList.appendChild(addButton);
};

// 显示目标信息
CardEditor.prototype.displayTargetsInfo = function(targets) {
    const targetsPanel = document.getElementById('targets-edit-panel');
    if (!targetsPanel) return;

    const targetsList = targetsPanel.querySelector('#targets-list');
    if (!targetsList) return;

    // 移除占位符
    const placeholder = targetsList.querySelector('.targets-placeholder');
    if (placeholder) {
        placeholder.remove();
    }

    // 清空现有目标
    targetsList.innerHTML = '';

    if (targets.length === 0) {
        targetsList.innerHTML = `
            <div class="targets-placeholder">
                <p>暂无目标</p>
                <button class="btn-primary" id="add-target-btn">添加目标</button>
            </div>
        `;
        return;
    }

    // 为每个目标创建可编辑的UI组件
    targets.forEach((target, index) => {
        const targetId = `target_${index}`;
        const targetKey = `target${index + 1}`;
        const targetItem = document.createElement('div');
        targetItem.className = 'target-item';
        targetItem.setAttribute('data-target-id', targetId);
        targetItem.setAttribute('data-target-key', targetKey);

        targetItem.innerHTML = `
            <div class="target-item-header">
                <div class="target-item-title">目标 ${index + 1}</div>
                <div class="target-item-actions">
                    <button class="btn-icon remove-target-btn" data-target-id="${targetId}" title="移除目标">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="target-item-content">
                <div class="form-group">
                    <label>选择器类型:</label>
                    <select class="form-control target-selector-type">
                        <option value="">选择选择器类型</option>
                        <option value="Filtered" ${target.selectorType === 'Filtered' ? 'selected' : ''}>Filtered</option>
                        <option value="All" ${target.selectorType === 'All' ? 'selected' : ''}>All</option>
                        <option value="Random" ${target.selectorType === 'Random' ? 'selected' : ''}>Random</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>查询配置:</label>
                    <textarea class="form-control target-query-config" rows="4">${JSON.stringify(target.query || {}, null, 2)}</textarea>
                </div>
                <div class="form-group">
                    <label>完整目标配置:</label>
                    <textarea class="form-control" rows="3" readonly>${JSON.stringify(target, null, 2)}</textarea>
                </div>
            </div>
        `;

        targetsList.appendChild(targetItem);
    });

    // 添加"添加目标"按钮
    const addButton = document.createElement('div');
    addButton.className = 'add-target-container';
    addButton.innerHTML = '<button class="btn-primary" id="add-target-btn">添加目标</button>';
    targetsList.appendChild(addButton);
};

// 显示效果信息
CardEditor.prototype.displayEffectsInfo = function(effects) {
    const effectsPanel = document.getElementById('effects-edit-panel');
    if (!effectsPanel) return;

    const effectsList = effectsPanel.querySelector('#effects-list');
    if (!effectsList) return;

    // 移除占位符
    const placeholder = effectsList.querySelector('.effects-placeholder');
    if (placeholder) {
        placeholder.remove();
    }

    // 清空现有效果
    effectsList.innerHTML = '';

    if (effects.length === 0) {
        effectsList.innerHTML = `
            <div class="effects-placeholder">
                <p>暂无效果</p>
                <button class="btn-primary" id="add-effect-btn">添加效果</button>
            </div>
        `;
        return;
    }

    // 为每个效果创建可编辑的UI组件
    effects.forEach((effect, index) => {
        const effectId = `effect_${index}`;
        const effectItem = document.createElement('div');
        effectItem.className = 'effect-item';
        effectItem.setAttribute('data-effect-id', effectId);

        effectItem.innerHTML = `
            <div class="effect-item-header">
                <div class="effect-item-title">效果 ${index + 1}</div>
                <div class="effect-item-actions">
                    <button class="btn-icon remove-effect-btn" data-effect-id="${effectId}" title="移除效果">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="effect-item-content">
                <div class="form-group">
                    <label>效果类型:</label>
                    <select class="form-control effect-type-select">
                        <option value="">选择效果类型</option>
                        <option value="Draw" ${effect.type === 'Draw' ? 'selected' : ''}>Draw (抽牌)</option>
                        <option value="Damage" ${effect.type === 'Damage' ? 'selected' : ''}>Damage (伤害)</option>
                        <option value="Heal" ${effect.type === 'Heal' ? 'selected' : ''}>Heal (治疗)</option>
                        <option value="AddBuff" ${effect.type === 'AddBuff' ? 'selected' : ''}>AddBuff (添加增益)</option>
                        <option value="Summon" ${effect.type === 'Summon' ? 'selected' : ''}>Summon (召唤)</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>效果参数:</label>
                    <textarea class="form-control effect-parameters" rows="4">${JSON.stringify(effect.parameters || {}, null, 2)}</textarea>
                </div>
                <div class="form-group">
                    <label>完整效果配置:</label>
                    <textarea class="form-control" rows="3" readonly>${JSON.stringify(effect, null, 2)}</textarea>
                </div>
            </div>
        `;

        effectsList.appendChild(effectItem);
    });

    // 添加"添加效果"按钮
    const addButton = document.createElement('div');
    addButton.className = 'add-effect-container';
    addButton.innerHTML = '<button class="btn-primary" id="add-effect-btn">添加效果</button>';
    effectsList.appendChild(addButton);
};
