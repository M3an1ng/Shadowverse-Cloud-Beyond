// src/components/TriggerConfigEditor.js - 触发器配置编辑器 (Refactored)
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';

/**
 * 触发器配置编辑器
 * 专注于清晰的事件选择和时序配置
 */
export class TriggerConfigEditor extends MetadataDrivenComponent {
    constructor() {
        super();
        this.config = null;
        this.onChange = null;
        this.container = null;
    }

    /**
     * 渲染触发器配置编辑器
     * @param {HTMLElement} container - 容器元素
     * @param {Object} config - 触发器配置对象
     * @param {Function} onChange - 配置变化回调
     */
    async render(container, config = {}, onChange = () => {}) {
        await this.initialize();
        
        this.container = container;
        this.config = config;
        this.onChange = onChange;

        this.clearContainer(container);
        
        try {
            const wrapper = document.createElement('div');
            wrapper.className = 'step-content-wrapper trigger-step';

            // 标题区域
            const header = document.createElement('div');
            header.className = 'step-header';
            header.innerHTML = `
                <h4>触发时机配置</h4>
                <p class="step-intro">定义能力在何时被激活。准确的触发时机是卡牌逻辑的关键。</p>
            `;
            wrapper.appendChild(header);

            // 表单区域
            const formGrid = document.createElement('div');
            formGrid.className = 'form-grid-layout';

            // 1. 监听事件 (全宽)
            const eventSelector = await this.createEventTypesSelector();
            formGrid.appendChild(eventSelector);

            // 2. 触发时序 (半宽)
            const timingSelector = await this.createTimingSelector();
            formGrid.appendChild(timingSelector);

            // 3. 关联方 (半宽)
            const relationSelector = await this.createRelationTypeSelector();
            formGrid.appendChild(relationSelector);

            // 4. 有效区域 (全宽)
            const zoneSelector = await this.createValidZonesSelector();
            formGrid.appendChild(zoneSelector);

            wrapper.appendChild(formGrid);
            container.appendChild(wrapper);
            
        } catch (error) {
            console.error('TriggerConfigEditor: Render failed:', error);
            this.showError(`渲染触发器配置失败: ${error.message}`, container);
        }
    }

    /**
     * 创建事件类型选择器 (Grid Layout with Tooltip)
     */
    async createEventTypesSelector() {
        const events = this.metadata.events || [];
        const selectedValues = new Set(this.config.eventTypes || []);

        const container = document.createElement('div');
        container.className = 'event-selector-container';
        // Container no longer needs relative position for tooltip, but good for layout
        container.style.position = 'relative';

        // 1. Grid Area (Clearer Layout)
        const multiSelect = document.createElement('div');
        multiSelect.className = 'multi-select-grid custom-scrollbar';
        
        // 2. Global Tooltip Helper
        const getGlobalTooltip = () => {
            let tooltip = document.getElementById('global-event-tooltip');
            if (!tooltip) {
                tooltip = document.createElement('div');
                tooltip.id = 'global-event-tooltip';
                tooltip.className = 'event-info-tooltip';
                tooltip.style.position = 'fixed'; // Fixed positioning to escape container overflow
                tooltip.style.zIndex = '9999'; // High z-index
                tooltip.style.display = 'none';
                tooltip.style.pointerEvents = 'none'; // Prevent tooltip from blocking mouse events
                document.body.appendChild(tooltip);
            }
            return tooltip;
        };

        // Helper to format parameter object safely
        const formatParam = (p) => {
            if (typeof p === 'string') return p;
            if (p && typeof p === 'object') {
                const propertyName = p.propertyName || p.PropertyName || p.name;
                const name = p.displayName || p.DisplayName || propertyName;
                const rawType = p.typeName || p.TypeName || p.type;
                const type = rawType ? this.getDisplayTypeName(rawType) : null;
                const desc = p.description;
                const rawScope = p.scope || p.Scope;
                const scope = String(rawScope || 'Input').toLowerCase() === 'output' ? '输出' : '输入';
                
                let display = `<span class="param-name">${name || 'Unknown'}</span>`;
                if (type) display += ` <span class="param-type">(${type})</span>`;
                if (scope) display += ` <span class="param-scope">[${scope}]</span>`;
                if (desc) display += `: <span class="param-desc">${desc}</span>`;
                
                return display;
            }
            return String(p);
        };

        // Helper to update and show tooltip
        const showTooltip = (event, e) => {
            const tooltip = getGlobalTooltip();
            let paramsHtml = '';
            const params = event.contextVariables || event.parameters;
            
            if (params && params.length > 0) {
                paramsHtml = `
                    <div class="event-params">
                        <span class="params-label">可用参数:</span>
                        <ul class="params-list-vertical">
                            ${params.map(p => `<li>${formatParam(p)}</li>`).join('')}
                        </ul>
                    </div>
                `;
            }

            tooltip.innerHTML = `
                <div class="event-info-header">
                    <span class="event-name">${event.displayName}</span>
                </div>
                <div class="event-body">
                    <p class="event-description">${event.description || '暂无描述'}</p>
                    ${paramsHtml}
                </div>
            `;

            // Initial show
            tooltip.style.display = 'block';
            updateTooltipPosition(e);
        };

        const updateTooltipPosition = (e) => {
            const tooltip = getGlobalTooltip();
            const tooltipWidth = 320; // Defined in CSS
            const tooltipHeight = tooltip.offsetHeight || 200;
            
            // Calculate position relative to viewport (fixed)
            let left = e.clientX + 15;
            let top = e.clientY + 15;

            // Boundary checks (Viewport)
            const viewportWidth = window.innerWidth;
            const viewportHeight = window.innerHeight;

            // Right edge check
            if (left + tooltipWidth > viewportWidth) {
                left = e.clientX - tooltipWidth - 15; // Flip to left
            }

            // Bottom edge check
            if (top + tooltipHeight > viewportHeight) {
                top = e.clientY - tooltipHeight - 15; // Flip up
            }

            tooltip.style.left = `${left}px`;
            tooltip.style.top = `${top}px`;
        };

        const hideTooltip = () => {
            const tooltip = getGlobalTooltip();
            if (tooltip) {
                tooltip.style.display = 'none';
            }
        };

        const toggleWrapper = document.createElement('label');
        toggleWrapper.className = 'event-system-toggle';

        const toggleInput = document.createElement('input');
        toggleInput.type = 'checkbox';
        toggleInput.checked = false;

        const toggleText = document.createElement('span');
        toggleText.textContent = '显示系统请求事件';

        toggleWrapper.appendChild(toggleInput);
        toggleWrapper.appendChild(toggleText);
        container.appendChild(toggleWrapper);

        const orderedCategories = this.getAbilityEditorCategoryOrder();
        const orderedCategoryIds = new Set(orderedCategories.map(category => category.id));
        const categoryLabelMap = new Map(orderedCategories.map(category => [category.id, category.label]));

        const normalizeCategoryId = (rawId) => orderedCategoryIds.has(rawId) ? rawId : 'uncategorized';
        const getVisibility = (event) => String(event.visibility || event.Visibility || 'visible').toLowerCase();

        const renderEventGroups = () => {
            multiSelect.innerHTML = '';

            const grouped = new Map();
            events.forEach(event => {
                const visibility = getVisibility(event);
                if (visibility === 'hidden') {
                    return;
                }

                if (!toggleInput.checked && visibility === 'system') {
                    return;
                }

                const rawCategoryId = event.categoryId || event.CategoryId;
                const categoryId = normalizeCategoryId(rawCategoryId);
                const categoryLabel = event.categoryLabel || event.CategoryLabel || categoryLabelMap.get(categoryId);

                if (categoryLabel) {
                    categoryLabelMap.set(categoryId, categoryLabel);
                }

                if (!grouped.has(categoryId)) {
                    grouped.set(categoryId, []);
                }
                grouped.get(categoryId).push(event);
            });

            orderedCategories.forEach(category => {
                const items = grouped.get(category.id);
                if (!items || items.length === 0) {
                    return;
                }

                items.sort((a, b) => String(a.displayName || '').localeCompare(String(b.displayName || '')));

                const header = document.createElement('div');
                header.className = 'event-group-label';
                header.textContent = categoryLabelMap.get(category.id) || category.label;
                multiSelect.appendChild(header);

                items.forEach(event => {
                    const label = document.createElement('label');
                    label.className = 'multi-select-item';

                    const checkbox = document.createElement('input');
                    checkbox.type = 'checkbox';
                    checkbox.value = event.eventType;
                    checkbox.checked = selectedValues.has(event.eventType);

                    const span = document.createElement('span');
                    span.textContent = event.displayName;

                    label.appendChild(checkbox);
                    label.appendChild(span);

                    checkbox.addEventListener('change', (e) => {
                        if (e.target.checked) {
                            selectedValues.add(event.eventType);
                            label.classList.add('selected');
                        } else {
                            selectedValues.delete(event.eventType);
                            label.classList.remove('selected');
                        }
                        this.updateConfig({ eventTypes: Array.from(selectedValues) });
                    });

                    if (selectedValues.has(event.eventType)) {
                        label.classList.add('selected');
                    }

                    label.addEventListener('mouseenter', (e) => showTooltip(event, e));
                    label.addEventListener('mousemove', (e) => updateTooltipPosition(e));
                    label.addEventListener('mouseleave', () => hideTooltip());

                    multiSelect.appendChild(label);
                });
            });
        };

        toggleInput.addEventListener('change', renderEventGroups);
        renderEventGroups();

        container.appendChild(multiSelect);
        // Tooltip is now global, not appended here

        const formGroup = this.createFormGroup(
            '监听事件',
            container,
            '选择触发此能力的游戏事件（可多选）'
        );
        formGroup.className += ' full-width';
        return formGroup;
    }

    /**
     * 创建触发时序选择器
     */
    async createTimingSelector() {
        const timingOptions = [
            { value: 'Before', label: '事件发生前 (Before)' },
            { value: 'On', label: '事件发生时 (On)' },
            { value: 'After', label: '事件发生后 (After)' }
        ];

        const dropdown = this.createDropdown(
            timingOptions,
            this.config.timing || 'On',
            (value) => this.updateConfig({ timing: value }),
            '选择时序...'
        );
        dropdown.className = 'form-select';

        return this.createFormGroup(
            '触发时序',
            dropdown,
            '决定能力结算的优先级顺序'
        );
    }

    /**
     * 创建关系类型选择器
     */
    async createRelationTypeSelector() {
        const relationTypes = this.metadata.relationTypes || [];
        const relationOptions = relationTypes.map(relationType => ({
            value: relationType.value,
            label: relationType.displayName
        }));

        if (relationOptions.length === 0) {
            relationOptions.push({ value: 'Any', label: '加载中...' });
        }

        const dropdown = this.createDropdown(
            relationOptions,
            this.config.relationType,
            (value) => this.updateConfig({ relationType: value }),
            '选择关联方...'
        );
        dropdown.className = 'form-select';

        return this.createFormGroup(
            '事件关联方',
            dropdown,
            '谁触发了事件？（例如：仅限己方随从）'
        );
    }

    /**
     * 创建有效区域选择器
     */
    async createValidZonesSelector() {
        const zoneOptions = [
            { value: 'Hand', label: '手牌 (Hand)' },
            { value: 'Field', label: '战场 (Field)' },
            { value: 'Deck', label: '牌库 (Deck)' },
            { value: 'Graveyard', label: '墓地 (Graveyard)' },
            { value: 'Banished', label: '放逐区 (Banished)' }
        ];

        const selectedZones = this.config.validZones || [];
        
        const multiSelect = this.createMultiSelect(
            zoneOptions,
            selectedZones,
            (newValues) => this.updateConfig({ validZones: newValues })
        );
        multiSelect.className = 'form-select multi-select';

        const formGroup = this.createFormGroup(
            '有效区域',
            multiSelect,
            '能力仅在卡牌位于这些区域时生效（留空则无限制）'
        );
        formGroup.className += ' full-width';
        return formGroup;
    }

    updateConfig(updates) {
        this.config = { ...this.config, ...updates };
        if (this.onChange) {
            this.onChange(this.config);
        }
    }

    validateConfig() {
        const errors = [];
        if (!this.config.eventTypes || this.config.eventTypes.length === 0) {
            errors.push('触发器: 必须选择至少一个监听事件');
        }
        if (!this.config.relationType) {
            errors.push('触发器: 必须选择事件关联方');
        }
        return errors;
    }
}
