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
        
        // 清理可能存在的旧 tooltip
        this.cleanupTooltip();

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

            // 4. 有效区域 & 执行前校验 (合并，全宽)
            const zoneSelector = await this.createValidZonesSelector();
            formGrid.appendChild(zoneSelector);

            // 5. 触发次数限制 (全宽)
            const countLimitSelector = await this.createTriggerCountLimitSelector();
            formGrid.appendChild(countLimitSelector);

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
        const normalizeEventType = (value) => String(value || '').trim().toLowerCase();
        const eventTypeMap = new Map(
            events
                .map(event => String(event.eventType || '').trim())
                .filter(value => value)
                .map(value => [normalizeEventType(value), value])
        );
        const selectedValues = new Set(
            (this.config.eventTypes || []).map(value => {
                const key = normalizeEventType(value);
                return eventTypeMap.get(key) || value;
            })
        );
        const selectedNormalized = new Set(
            Array.from(selectedValues).map(value => normalizeEventType(value))
        );

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
        let tooltipMonitorId = null;

        const hideTooltip = () => {
            const tooltip = getGlobalTooltip();
            if (tooltip) {
                tooltip.style.display = 'none';
            }
            if (tooltipMonitorId) {
                cancelAnimationFrame(tooltipMonitorId);
                tooltipMonitorId = null;
            }
        };

        const showTooltip = (event, e) => {
            const target = e.target;
            
            // 检查触发元素是否仍然连接在 DOM 上
            if (!target.isConnected) {
                hideTooltip();
                return;
            }

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

            // 启动监控循环：如果目标元素被移除，立即隐藏 tooltip
            if (tooltipMonitorId) cancelAnimationFrame(tooltipMonitorId);
            
            const monitor = () => {
                if (!target.isConnected) {
                    hideTooltip();
                    return;
                }
                tooltipMonitorId = requestAnimationFrame(monitor);
            };
            tooltipMonitorId = requestAnimationFrame(monitor);
        };

        const updateTooltipPosition = (e) => {
            // 检查触发元素是否仍然连接在 DOM 上
            if (!e.target.isConnected) {
                hideTooltip();
                return;
            }

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
                    checkbox.checked = selectedNormalized.has(normalizeEventType(event.eventType));

                    const span = document.createElement('span');
                    span.textContent = event.displayName;

                    label.appendChild(checkbox);
                    label.appendChild(span);

                    checkbox.addEventListener('change', (e) => {
                        if (e.target.checked) {
                            selectedValues.add(event.eventType);
                            selectedNormalized.add(normalizeEventType(event.eventType));
                            label.classList.add('selected');
                        } else {
                            const key = normalizeEventType(event.eventType);
                            for (const value of Array.from(selectedValues)) {
                                if (normalizeEventType(value) === key) {
                                    selectedValues.delete(value);
                                }
                            }
                            selectedNormalized.delete(key);
                            label.classList.remove('selected');
                        }
                        this.updateConfig({ eventTypes: Array.from(selectedValues) });
                    });

                    if (selectedNormalized.has(normalizeEventType(event.eventType))) {
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
     * 创建有效区域选择器 (包含执行前校验)
     */
    async createValidZonesSelector() {
        const container = document.createElement('div');
        container.className = 'valid-zones-container';

        // 1. 区域选择 (横向排列)
        const zoneOptions = [
            { value: 'Hand', label: '手牌' },
            { value: 'Field', label: '战场' },
            { value: 'Deck', label: '牌库' },
            { value: 'Graveyard', label: '墓地' },
            { value: 'Emblem', label: '纹章区' }
        ];

        const selectedZones = new Set(this.config.validZones || []);
        
        const zonesWrapper = document.createElement('div');
        zonesWrapper.className = 'zones-horizontal-layout';

        zoneOptions.forEach(option => {
            const label = document.createElement('label');
            label.className = 'zone-checkbox-item';
            
            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.value = option.value;
            checkbox.checked = selectedZones.has(option.value);
            
            const span = document.createElement('span');
            span.textContent = option.label;

            label.appendChild(checkbox);
            label.appendChild(span);

            checkbox.addEventListener('change', (e) => {
                if (e.target.checked) {
                    selectedZones.add(option.value);
                    label.classList.add('selected');
                } else {
                    selectedZones.delete(option.value);
                    label.classList.remove('selected');
                }
                this.updateConfig({ validZones: Array.from(selectedZones) });
            });

            if (selectedZones.has(option.value)) {
                label.classList.add('selected');
            }

            zonesWrapper.appendChild(label);
        });

        container.appendChild(zonesWrapper);

        // 2. 执行前校验开关 (作为特殊选项整合)
        const revalidateLabel = document.createElement('label');
        revalidateLabel.className = 'zone-checkbox-item special-option';
        // revalidateLabel.title = ... // Removed native tooltip in favor of custom one

        // Tooltip Logic for Revalidate Option (Reusing global style)
        const getGlobalTooltip = () => {
            let tooltip = document.getElementById('global-event-tooltip');
            if (!tooltip) {
                tooltip = document.createElement('div');
                tooltip.id = 'global-event-tooltip';
                tooltip.className = 'event-info-tooltip';
                tooltip.style.position = 'fixed';
                tooltip.style.zIndex = '9999';
                tooltip.style.display = 'none';
                tooltip.style.pointerEvents = 'none';
                document.body.appendChild(tooltip);
            }
            return tooltip;
        };

        const hideTooltip = () => {
            const tooltip = getGlobalTooltip();
            if (tooltip) tooltip.style.display = 'none';
        };

        const updateTooltipPosition = (e) => {
            const tooltip = getGlobalTooltip();
            const tooltipWidth = 320;
            const tooltipHeight = tooltip.offsetHeight || 100;
            
            let left = e.clientX + 15;
            let top = e.clientY + 15;

            if (left + tooltipWidth > window.innerWidth) {
                left = e.clientX - tooltipWidth - 15;
            }
            if (top + tooltipHeight > window.innerHeight) {
                top = e.clientY - tooltipHeight - 15;
            }

            tooltip.style.left = `${left}px`;
            tooltip.style.top = `${top}px`;
        };

        const showTooltip = (e) => {
            const tooltip = getGlobalTooltip();
            tooltip.innerHTML = `
                <div class="event-body">
                    <p class="event-description">开启后，效果在结算前会再次检查卡牌是否仍在有效区域。</p>
                </div>
            `;
            tooltip.style.display = 'block';
            updateTooltipPosition(e);
        };

        revalidateLabel.addEventListener('mouseenter', showTooltip);
        revalidateLabel.addEventListener('mousemove', updateTooltipPosition);
        revalidateLabel.addEventListener('mouseleave', hideTooltip);

        const revalidateInput = document.createElement('input');
        revalidateInput.type = 'checkbox';
        revalidateInput.checked = !!this.config.revalidateZoneBeforeExecution;
        
        const revalidateText = document.createElement('span');
        revalidateText.textContent = '执行前再次校验';

        revalidateLabel.appendChild(revalidateInput);
        revalidateLabel.appendChild(revalidateText);

        revalidateInput.addEventListener('change', (e) => {
            this.updateConfig({ revalidateZoneBeforeExecution: e.target.checked });
            if (e.target.checked) {
                revalidateLabel.classList.add('selected');
            } else {
                revalidateLabel.classList.remove('selected');
            }
        });

        if (this.config.revalidateZoneBeforeExecution) {
            revalidateLabel.classList.add('selected');
        }

        // 添加分隔符和校验选项到区域列表
        const separator = document.createElement('div');
        separator.className = 'zone-separator';
        zonesWrapper.appendChild(separator);
        zonesWrapper.appendChild(revalidateLabel);

        const formGroup = this.createFormGroup(
            '有效区域与限制',
            container,
            '能力仅在卡牌位于这些区域时生效（留空则无限制）'
        );
        formGroup.className += ' full-width';
        return formGroup;
    }

    async createTriggerCountLimitSelector() {
        const container = document.createElement('div');
        container.className = 'trigger-count-limit-container';

        const currentLimit = this.config.countLimit || {};

        const perTurnInput = this.createNumberInput(
            currentLimit.maxPerTurn ?? null,
            (value) => this.updateCountLimit({ maxPerTurn: value }),
            { placeholder: '不限' }
        );
        perTurnInput.classList.add('form-input', 'small');

        const perGameInput = this.createNumberInput(
            currentLimit.maxPerGame ?? null,
            (value) => this.updateCountLimit({ maxPerGame: value }),
            { placeholder: '不限' }
        );
        perGameInput.classList.add('form-input', 'small');

        const grid = document.createElement('div');
        grid.className = 'form-grid-layout';
        grid.appendChild(this.createFormGroup('每回合上限', perTurnInput));
        grid.appendChild(this.createFormGroup('全局上限', perGameInput));

        container.appendChild(grid);

        const formGroup = this.createFormGroup(
            '触发次数限制',
            container,
            '留空或填写 0/负数表示不限（全局限制对单个能力实例贯穿整局生效）'
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

    updateCountLimit(updates) {
        const currentLimit = { ...(this.config.countLimit || {}), ...updates };
        const hasPerTurn = currentLimit.maxPerTurn !== null && currentLimit.maxPerTurn !== undefined;
        const hasPerGame = currentLimit.maxPerGame !== null && currentLimit.maxPerGame !== undefined;

        if (!hasPerTurn && !hasPerGame) {
            this.updateConfig({ countLimit: null });
            return;
        }

        this.updateConfig({ countLimit: currentLimit });
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

    /**
     * 清理全局 tooltip
     */
    cleanupTooltip() {
        const tooltip = document.getElementById('global-event-tooltip');
        if (tooltip) {
            tooltip.remove();
        }
    }
}
