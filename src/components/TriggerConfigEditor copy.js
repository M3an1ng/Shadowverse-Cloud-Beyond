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
     * 创建事件类型选择器 (Master-Detail Layout)
     */
    async createEventTypesSelector() {
        const events = this.metadata.events || [];
        const selectedValues = new Set(this.config.eventTypes || []);

        const container = document.createElement('div');
        container.className = 'event-selector-container master-detail-layout';

        // 1. Left: Scrollable List
        const listContainer = document.createElement('div');
        listContainer.className = 'event-list-container custom-scrollbar';

        // 2. Right: Info Panel
        const infoPanel = document.createElement('div');
        infoPanel.className = 'event-info-panel sticky-panel';
        infoPanel.innerHTML = '<p class="placeholder-text">请选择或悬停事件以查看详情...</p>';

        // Helper to format parameter object safely
        const formatParam = (p) => {
            if (typeof p === 'string') return p;
            if (p && typeof p === 'object') {
                // Try common fields
                return p.name || p.type || p.key || p.id || JSON.stringify(p);
            }
            return String(p);
        };

        // Helper to update info panel
        const updateInfoPanel = (event) => {
            if (!event) {
                infoPanel.innerHTML = '<p class="placeholder-text">请选择或悬停事件以查看详情...</p>';
                return;
            }
            
            let paramsHtml = '';
            // Check both contextVariables and parameters
            const params = event.contextVariables || event.parameters;
            
            if (params && params.length > 0) {
                paramsHtml = `
                    <div class="event-params">
                        <span class="params-label">可用上下文参数:</span>
                        <div class="params-list">
                            ${params.map(p => `<code>${formatParam(p)}</code>`).join('')}
                        </div>
                    </div>
                `;
            } else {
                paramsHtml = `<div class="event-params"><span class="params-label text-muted">无可用参数</span></div>`;
            }

            infoPanel.innerHTML = `
                <div class="event-info-header">
                    <span class="event-name">${event.displayName}</span>
                    <span class="event-id-badge">${event.eventType}</span>
                </div>
                <div class="event-body">
                    <p class="event-description">${event.description || '暂无描述'}</p>
                    ${paramsHtml}
                </div>
            `;
        };

        events.forEach(event => {
            const item = document.createElement('div');
            item.className = `event-list-item ${selectedValues.has(event.eventType) ? 'selected' : ''}`;
            
            // Checkbox
            const checkbox = document.createElement('div');
            checkbox.className = 'event-checkbox';
            if (selectedValues.has(event.eventType)) checkbox.classList.add('checked');
            
            // Label
            const label = document.createElement('span');
            label.className = 'event-item-label';
            label.textContent = event.displayName;

            item.appendChild(checkbox);
            item.appendChild(label);
            
            // Click handling
            item.addEventListener('click', () => {
                if (selectedValues.has(event.eventType)) {
                    selectedValues.delete(event.eventType);
                    item.classList.remove('selected');
                    checkbox.classList.remove('checked');
                } else {
                    selectedValues.add(event.eventType);
                    item.classList.add('selected');
                    checkbox.classList.add('checked');
                }
                this.updateConfig({ eventTypes: Array.from(selectedValues) });
                // Also update info panel on click
                updateInfoPanel(event);
            });

            // Hover handling
            item.addEventListener('mouseenter', () => updateInfoPanel(event));

            listContainer.appendChild(item);
        });

        container.appendChild(listContainer);
        container.appendChild(infoPanel);

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
