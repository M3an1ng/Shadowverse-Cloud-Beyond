// src/components/TargetConfigEditor.js - 目标配置编辑器 (Refactored)
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';
import { DslEditor } from './DslEditor.js';

/**
 * 目标配置编辑器
 * 采用手风琴式列表设计，支持多目标管理
 */
export class TargetConfigEditor extends MetadataDrivenComponent {
    constructor() {
        super();
        this.targets = [];
        this.onChange = null;
        this.container = null;
        this.dslEditors = new Map();
        this.options = {};
    }

    async render(container, targets = [], onChange = () => {}, options = {}) {
        await this.initialize();
        
        this.container = container;
        this.targets = (targets || []).map(target => this.normalizeTargetConfig(target));
        this.onChange = onChange;
        this.options = options || {};

        this.clearContainer(container);
        
        try {
            const wrapper = document.createElement('div');
            wrapper.className = 'step-content-wrapper targets-step';

            // 标题区域
            const header = document.createElement('div');
            header.className = 'step-header';
            header.innerHTML = `
                <h4>卡牌目标选择配置</h4>
                <p class="step-intro">定义能力将影响哪些卡牌实例。你可以配置多个目标，并为每个目标指定筛选条件。</p>
            `;
            wrapper.appendChild(header);

            // 目标列表容器
            const listContainer = document.createElement('div');
            listContainer.className = 'targets-list';
            listContainer.id = 'targets-list-container';
            wrapper.appendChild(listContainer);

            // 渲染列表内容
            this.renderTargetsList(listContainer);

            // 添加按钮区域
            const actionsContainer = document.createElement('div');
            actionsContainer.className = 'add-target-container';
            const addBtn = this.createButton('添加新目标', () => this.addTarget(), 'primary');
            actionsContainer.appendChild(addBtn);
            wrapper.appendChild(actionsContainer);

            container.appendChild(wrapper);
            
        } catch (error) {
            console.error('TargetConfigEditor: Render failed:', error);
            this.showError(`渲染目标配置失败: ${error.message}`, container);
        }
    }

    renderTargetsList(container) {
        this.clearContainer(container);

        if (this.targets.length === 0) {
            const placeholder = document.createElement('div');
            placeholder.className = 'empty-state';
            placeholder.innerHTML = `
                <div class="empty-state-visual">
                    <div class="target-ring-outer">
                        <div class="target-ring-inner">
                            <div class="target-dot"></div>
                        </div>
                    </div>
                </div>
                <h5 class="empty-title">暂无卡牌目标配置</h5>
                <p class="empty-desc">定义能力将影响哪些卡牌实例<br>点击下方按钮添加第一个目标</p>
            `;
            container.appendChild(placeholder);
            return;
        }

        this.targets.forEach((target, index) => {
            const targetNode = this.createTargetNode(target, index);
            container.appendChild(targetNode);
        });
    }

    createTargetNode(target, index) {
        const node = document.createElement('div');
        node.className = 'target-node accordion-item';
        node.dataset.targetIndex = index;

        // 头部
        const header = document.createElement('div');
        header.className = 'accordion-header';

        const isEnabled = target.isEnabled !== false;
        const statusClass = isEnabled ? 'status-enabled' : 'status-disabled';
        const statusText = isEnabled ? '启用' : '禁用';

        const headerInfo = document.createElement('div');
        headerInfo.className = 'header-info';
        headerInfo.innerHTML = `
            <span class="item-index">#${index + 1}</span>
            <span class="item-title">目标配置</span>
            <span class="item-status ${statusClass}">${statusText}</span>
        `;

        const headerActions = document.createElement('div');
        headerActions.className = 'header-actions';

        const toggle = this.createToggleSwitch(
            isEnabled,
            (checked) => {
                this.updateTarget(index, { isEnabled: checked });
                const statusSpan = header.querySelector('.item-status');
                if (checked) {
                    statusSpan.textContent = '启用';
                    statusSpan.className = 'item-status status-enabled';
                    toggle.title = '点击禁用目标';
                } else {
                    statusSpan.textContent = '禁用';
                    statusSpan.className = 'item-status status-disabled';
                    toggle.title = '点击启用目标';
                }
            },
            '',
            { compact: true }
        );
        toggle.title = isEnabled ? '点击禁用目标' : '点击启用目标';

        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'btn-icon delete-btn';
        deleteBtn.title = '删除';
        deleteBtn.textContent = '×';

        const expandBtn = document.createElement('button');
        expandBtn.className = 'btn-icon expand-btn';
        expandBtn.title = '展开/折叠';
        expandBtn.textContent = '▼';

        headerActions.appendChild(toggle);
        headerActions.appendChild(deleteBtn);
        headerActions.appendChild(expandBtn);

        header.appendChild(headerInfo);
        header.appendChild(headerActions);

        // 绑定头部事件
        deleteBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.removeTarget(index);
        });

        // 内容区域
        const content = document.createElement('div');
        content.className = 'accordion-content collapsed';
        
        // 模式切换按钮
        const modeSwitchContainer = document.createElement('div');
        modeSwitchContainer.className = 'mode-switch-container';
        const isAdvanced = target._uiMode === 'advanced';
        
        const switchBtn = this.createButton(
            isAdvanced ? '切换至简化模式' : '切换至高级模式',
            () => this.toggleTargetMode(index),
            'secondary small'
        );
        modeSwitchContainer.appendChild(switchBtn);
        content.appendChild(modeSwitchContainer);

        if (isAdvanced) {
            // 高级模式：原有 DSL 编辑器
            const querySection = this.createQueryEditor(target, index);
            content.appendChild(querySection);

            const validationSection = this.createValidationEditor(target, index);
            content.appendChild(validationSection);

            const playerChoiceSection = this.createPlayerChoiceEditor(target, index);
            content.appendChild(playerChoiceSection);
        } else {
            // 简化模式：按钮引导式 UI
            const simplifiedEditor = this.createSimplifiedEditor(target, index);
            content.appendChild(simplifiedEditor);
        }

        node.appendChild(header);
        node.appendChild(content);

        // 绑定折叠逻辑
        header.addEventListener('click', () => {
            const isCollapsed = content.classList.contains('collapsed');
            if (isCollapsed) {
                content.classList.remove('collapsed');
                expandBtn.innerHTML = '▲';
                node.classList.add('expanded');
            } else {
                content.classList.add('collapsed');
                expandBtn.innerHTML = '▼';
                node.classList.remove('expanded');
            }
        });

        return node;
    }

    createQueryEditor(target, index) {
        const container = document.createElement('div');
        container.className = 'config-section';
        container.innerHTML = '<h5 class="section-title">查询逻辑</h5>';

        // 起始卡池
        const sourcePoolGroup = this.createSourcePoolEditor(target, index);
        container.appendChild(sourcePoolGroup);

        return container;
    }

    createSourcePoolEditor(target, index) {
        const container = document.createElement('div');
        container.className = 'dsl-editor-wrapper';

        const dslEditor = new DslEditor();
        const editorKey = `sourcePool_${index}`;
        this.dslEditors.set(editorKey, dslEditor);

        dslEditor.render(
            container,
            target.query?.sourcePoolExpression || '',
            (expression) => {
                this.updateTargetQuery(index, { sourcePoolExpression: expression });
            },
            {
                expectedType: 'List<CardInstance>',
                triggeringEventContext: {
                    eventTypes: this.options?.trigger?.eventTypes || [],
                    timing: this.options?.trigger?.timing || 'On'
                }
            }
        );

        return this.createFormGroup('起始卡池', container, '选择查询的起点（如：所有敌方随从）');
    }

    createOperationsEditor(target, index) {
        const container = document.createElement('div');
        container.className = 'operations-container';

        const list = document.createElement('div');
        list.className = 'operations-list';
        
        const operations = target.query?.operations || [];
        if (operations.length === 0) {
            list.innerHTML = '<div class="text-muted">无额外操作</div>';
        } else {
            operations.forEach((op, opIndex) => {
                const opNode = this.createOperationNode(op, index, opIndex);
                list.appendChild(opNode);
            });
        }
        container.appendChild(list);

        const addBtn = this.createButton('+ 添加操作', () => this.showAddOperationMenu(index), 'secondary small');
        container.appendChild(addBtn);

        return this.createFormGroup('处理流程', container, '对卡池进行筛选、排序或随机抽取');
    }

    createOperationNode(operation, targetIndex, opIndex) {
        const node = document.createElement('div');
        node.className = 'operation-item';
        
        const meta = this.getOperationMetadata(operation.$type);
        const title = meta ? meta.displayName : operation.$type;

        node.innerHTML = `
            <div class="op-header">
                <span class="op-type">${title}</span>
                <button class="btn-icon-small delete-op">×</button>
            </div>
            <div class="op-body"></div>
        `;

        // 绑定删除
        node.querySelector('.delete-op').addEventListener('click', () => {
            this.removeOperation(targetIndex, opIndex);
        });

        // 参数配置
        const body = node.querySelector('.op-body');
        this.renderOperationParams(body, operation, targetIndex, opIndex);

        return node;
    }

    renderOperationParams(container, operation, targetIndex, opIndex) {
        switch (operation.$type) {
            case 'FilterOperation':
                // 简化版：只显示添加按钮和现有过滤器
                (operation.filters || []).forEach((filter, fIndex) => {
                    const dslContainer = document.createElement('div');
                    dslContainer.className = 'dsl-mini-wrapper';
                    const dslEditor = new DslEditor();
                    dslEditor.render(dslContainer, filter.valueSource?.formula || '', (expr) => {
                        this.updateOperationFilter(targetIndex, opIndex, fIndex, expr);
                    }, {
                        expectedType: 'Boolean',
                        triggeringEventContext: {
                            eventTypes: this.options?.trigger?.eventTypes || [],
                            timing: this.options?.trigger?.timing || 'On'
                        }
                    });
                    container.appendChild(dslContainer);
                });
                const addFilterBtn = this.createButton('添加条件', () => this.addFilter(targetIndex, opIndex), 'secondary tiny');
                container.appendChild(addFilterBtn);
                break;
            case 'TakeOperation':
            case 'RandomOperation':
                const countInput = this.createNumberInput(operation.count || 1, (val) => {
                    this.updateOperation(targetIndex, opIndex, { count: val });
                }, { min: 1 });
                countInput.className = 'form-input small';
                container.appendChild(this.createFormGroup('数量', countInput));
                break;
            // ... 其他类型
        }
    }

    createValidationEditor(target, index) {
        const container = document.createElement('div');
        container.className = 'config-section';
        container.innerHTML = '<h5 class="section-title">验证规则</h5>';

        const grid = document.createElement('div');
        grid.className = 'form-grid-layout';

        // 强制有效
        const validCheck = this.createToggleSwitch(
            target.requireValidTarget || false,
            (checked) => this.updateTarget(index, { requireValidTarget: checked }),
            '必须存在有效目标才能发动'
        );
        grid.appendChild(this.createFormGroup('发动限制', validCheck));

        container.appendChild(grid);
        return container;
    }

    createPlayerChoiceEditor(target, index) {
        const container = document.createElement('div');
        container.className = 'config-section';
        
        const header = document.createElement('div');
        header.className = 'section-header-with-toggle';
        
        const enabled = target.playerChoiceOptions != null;
        const toggle = this.createToggleSwitch(enabled, (checked) => {
            if (checked) {
                this.updateTarget(index, {
                    playerChoiceOptions: { isRequired: true, minCount: 1, maxCount: 1 }
                });
            } else {
                this.updateTarget(index, { playerChoiceOptions: null });
            }
            this.updateTargetNode(index); // 重新渲染
        }, '启用玩家手动选择');
        
        header.appendChild(toggle);
        container.appendChild(header);

        if (enabled) {
            const opts = target.playerChoiceOptions;
            const grid = document.createElement('div');
            grid.className = 'form-grid-layout';
            
            // 数量限制 (Range Slider)
            const rangeSlider = this.createRangeSlider(
                opts.minCount || 1,
                opts.maxCount || 1,
                (min, max) => {
                    this.updatePlayerChoiceOption(index, { minCount: min, maxCount: max });
                }
            );
            
            grid.appendChild(this.createFormGroup('选择数量', rangeSlider));
            container.appendChild(grid);
        }

        return container;
    }

    createRangeSlider(minVal, maxVal, onChange, options = {}) {
        const mode = options.mode || 'range'; // 'range' or 'single'
        const maxLimit = options.max || 10;
        
        const container = document.createElement('div');
        container.className = 'range-slider-container';
        if (mode === 'single') container.classList.add('single-mode');

        // Min Input (Hidden in single mode)
        const minInput = document.createElement('input');
        minInput.type = 'number';
        minInput.className = 'range-input-min form-input small';
        minInput.value = minVal;
        minInput.min = 0;
        minInput.max = maxLimit;
        if (mode === 'single') minInput.style.display = 'none';

        // Max Input
        const maxInput = document.createElement('input');
        maxInput.type = 'number';
        maxInput.className = 'range-input-max form-input small';
        maxInput.value = maxVal;
        maxInput.min = 0;
        maxInput.max = maxLimit;

        // Slider Track Wrapper
        const trackWrapper = document.createElement('div');
        trackWrapper.className = 'slider-track-wrapper';

        const track = document.createElement('div');
        track.className = 'slider-track';
        
        const highlight = document.createElement('div');
        highlight.className = 'slider-range-highlight';

        // Range Inputs (Thumbs)
        const rangeMin = document.createElement('input');
        rangeMin.type = 'range';
        rangeMin.className = 'thumb thumb-left';
        rangeMin.min = 0;
        rangeMin.max = maxLimit;
        rangeMin.value = minVal;
        if (mode === 'single') rangeMin.style.display = 'none';

        const rangeMax = document.createElement('input');
        rangeMax.type = 'range';
        rangeMax.className = 'thumb thumb-right';
        rangeMax.min = 0;
        rangeMax.max = maxLimit;
        rangeMax.value = maxVal;

        trackWrapper.appendChild(track);
        trackWrapper.appendChild(highlight);
        trackWrapper.appendChild(rangeMin);
        trackWrapper.appendChild(rangeMax);

        container.appendChild(minInput);
        container.appendChild(trackWrapper);
        container.appendChild(maxInput);

        // Update Logic
        const updateUI = () => {
            let min = parseInt(rangeMin.value);
            let max = parseInt(rangeMax.value);

            if (mode === 'range') {
                // Ensure Min <= Max
                if (min > max) {
                    rangeMin.value = max;
                    rangeMax.value = min;
                    // Swap values for calculation
                    const temp = min; min = max; max = temp;
                }
                
                minInput.value = min;
                maxInput.value = max;

                const percentMin = (min / maxLimit) * 100;
                const percentMax = (max / maxLimit) * 100;
                highlight.style.left = percentMin + '%';
                highlight.style.width = (percentMax - percentMin) + '%';
            } else {
                // Single Mode: Highlight from 0 to Value
                maxInput.value = max;
                const percentMax = (max / maxLimit) * 100;
                highlight.style.left = '0%';
                highlight.style.width = percentMax + '%';
            }
        };

        const triggerChange = () => {
            onChange(parseInt(minInput.value), parseInt(maxInput.value));
        };

        // Event Listeners
        if (mode === 'range') {
            rangeMin.addEventListener('input', () => {
                if (parseInt(rangeMin.value) > parseInt(rangeMax.value)) {
                    rangeMin.value = rangeMax.value;
                }
                updateUI();
            });
            rangeMin.addEventListener('change', triggerChange);

            minInput.addEventListener('change', () => {
                let val = parseInt(minInput.value);
                if (val > parseInt(maxInput.value)) val = parseInt(maxInput.value);
                if (val < 0) val = 0;
                rangeMin.value = val;
                updateUI();
                triggerChange();
            });
        }

        rangeMax.addEventListener('input', () => {
            if (mode === 'range') {
                if (parseInt(rangeMax.value) < parseInt(rangeMin.value)) {
                    rangeMax.value = rangeMin.value;
                }
            }
            updateUI();
        });
        rangeMax.addEventListener('change', triggerChange);

        maxInput.addEventListener('change', () => {
            let val = parseInt(maxInput.value);
            if (mode === 'range') {
                if (val < parseInt(minInput.value)) val = parseInt(minInput.value);
            }
            if (val > maxLimit) val = maxLimit;
            if (val < 0) val = 0;
            rangeMax.value = val;
            updateUI();
            triggerChange();
        });

        // Initialize
        updateUI();

        return container;
    }

    // ... 辅助方法 (createToggleSwitch, updateTargetNode, etc.) ...
    // 复用 AbilityEditor 中的 createToggleSwitch 逻辑或将其提取到 MetadataDrivenComponent
    createToggleSwitch(checked, onChange, labelText, options = {}) {
        const wrapper = document.createElement('label');
        wrapper.className = 'toggle-switch-wrapper';
        if (options.compact) {
            wrapper.classList.add('compact');
        }
        wrapper.addEventListener('click', (e) => e.stopPropagation());
        const input = document.createElement('input');
        input.type = 'checkbox';
        input.checked = checked;
        input.setAttribute('aria-label', labelText || '启用目标');
        input.addEventListener('click', (e) => e.stopPropagation());
        input.addEventListener('change', (e) => {
            e.stopPropagation();
            onChange(e.target.checked);
        });
        const slider = document.createElement('span');
        slider.className = 'toggle-slider';
        wrapper.appendChild(input);
        wrapper.appendChild(slider);
        if (labelText) {
            const label = document.createElement('span');
            label.className = 'toggle-label';
            label.textContent = labelText;
            wrapper.appendChild(label);
        }
        return wrapper;
    }

    // ... 其他逻辑保持不变，主要是 updateTargetNode 需要适配新的 createTargetNode ...
    updateTargetNode(index) {
        const listContainer = this.container.querySelector('#targets-list-container');
        if (!listContainer) return;
        const existingNode = listContainer.querySelector(`[data-target-index="${index}"]`);
        if (!existingNode) return;
        
        const isExpanded = existingNode.classList.contains('expanded');
        const newNode = this.createTargetNode(this.targets[index], index);
        
        if (isExpanded) {
            newNode.classList.add('expanded');
            newNode.querySelector('.accordion-content').classList.remove('collapsed');
            newNode.querySelector('.expand-btn').innerHTML = '▲';
        }
        
        existingNode.replaceWith(newNode);
    }

    toggleTargetMode(index) {
        const target = this.targets[index];
        const currentMode = target._uiMode || 'simplified';
        
        if (currentMode === 'simplified') {
            // 切换到高级模式：无需特殊处理，DSL 已经在 updateTargetFromSimplifiedUI 中生成
            this.updateTarget(index, { _uiMode: 'advanced' });
        } else {
            // 切换到简化模式：尝试解析 DSL
            // 如果解析失败（DSL太复杂），则提示用户
            const simplifiedState = this.parseDslToSimplifiedUI(target);
            if (simplifiedState) {
                this.updateTarget(index, { _uiMode: 'simplified', ...simplifiedState });
            } else {
                if (confirm('当前配置过于复杂，无法完全转换为简化模式。切换将重置部分高级配置。是否继续？')) {
                    this.updateTarget(index, { _uiMode: 'simplified' }); // 重置为默认简化状态
                }
            }
        }
        this.updateTargetNode(index);
    }

    createSimplifiedEditor(target, index) {
        const container = document.createElement('div');
        container.className = 'simplified-target-editor';

        // 1. 选择方式 (Selection Method)
        const methodGroup = this.createButtonGroup(
            '选择方式',
            [
                { value: 'All', label: '所有' },
                { value: 'Random', label: '随机' },
                { value: 'Select', label: '选择' }
            ],
            target._simpleMethod || 'All',
            (val) => this.updateSimplifiedTarget(index, { _simpleMethod: val }),
            'method-group'
        );
        container.appendChild(methodGroup);

        // 6. 动态内容区域 (选择数量等) - 移至选择方式下方
        const dynamicContent = document.createElement('div');
        dynamicContent.className = 'simplified-dynamic-content';
        this.renderSimplifiedDynamicContent(dynamicContent, target, index);
        container.appendChild(dynamicContent);

        // 2. 生效方 (Player Scope)
        const scopeGroup = this.createCheckboxGroup(
            '生效方',
            [
                { value: 'Self', label: '自己' },
                { value: 'Opponent', label: '对手' }
            ],
            target._simpleScope || ['Self'],
            (vals) => this.updateSimplifiedTarget(index, { _simpleScope: vals }),
            'scope-group'
        );
        container.appendChild(scopeGroup);

        // 3. 生效区域 (Zone)
        const zoneGroup = this.createCheckboxGroup(
            '生效区域',
            [
                { value: 'Battlefield', label: '战场' },
                { value: 'Hand', label: '手牌' },
                { value: 'Deck', label: '牌组' },
                { value: 'Graveyard', label: '墓地' }
            ],
            target._simpleZone || ['Battlefield'],
            (vals) => this.updateSimplifiedTarget(index, { _simpleZone: vals }),
            'zone-group'
        );
        container.appendChild(zoneGroup);

        // 4. 生效种类 (Card Type)
        const typeGroup = this.createCheckboxGroup(
            '生效种类',
            [
                { value: 'Unit', label: '随从' },
                { value: 'Spell', label: '法术' },
                { value: 'Amulet', label: '护符' },
                { value: 'Leader', label: '主战者' }
            ],
            target._simpleType || ['Unit'],
            (vals) => this.updateSimplifiedTarget(index, { _simpleType: vals }),
            'type-group'
        );
        container.appendChild(typeGroup);

        // 5. 验证规则 (Validation Rules) - 复用高级模式组件
        const validationSection = this.createValidationEditor(target, index);
        validationSection.classList.add('simplified-section'); // 添加标记以便样式微调
        container.appendChild(validationSection);

        return container;
    }

    renderSimplifiedDynamicContent(container, target, index) {
        this.clearContainer(container);
        const method = target._simpleMethod || 'All';
        let hasContent = false;

        if (method === 'Random') {
            hasContent = true;
            // 随机模式：显示单滑块（随机数量）
            const randomCountContainer = document.createElement('div');
            randomCountContainer.className = 'config-section simplified-section';
            
            const grid = document.createElement('div');
            grid.className = 'form-grid-layout';
            
            const randomOp = (target.query?.operations || []).find(op => op.$type === 'RandomOperation');
            const currentCount = randomOp?.count || target._simpleRandomCount || 1;

            const slider = this.createRangeSlider(
                0, // Min ignored in single mode
                currentCount,
                (min, max) => {
                    this.updateSimplifiedTarget(index, { _simpleRandomCount: max });
                },
                { mode: 'single', label: '随机数量', max: 10 }
            );
            
            grid.appendChild(this.createFormGroup('随机数量', slider));
            randomCountContainer.appendChild(grid);
            container.appendChild(randomCountContainer);

        } else if (method === 'Select') {
            hasContent = true;
            // 选择模式：显示双滑块（最小/最大数量）
            const selectCountContainer = document.createElement('div');
            selectCountContainer.className = 'config-section simplified-section';
            
            const grid = document.createElement('div');
            grid.className = 'form-grid-layout';
            
            const opts = target.playerChoiceOptions || {};
            const minCount = opts.minCount || 1;
            const maxCount = opts.maxCount || 1;

            const slider = this.createRangeSlider(
                minCount,
                maxCount,
                (min, max) => {
                    this.updateSimplifiedTarget(index, {
                        playerChoiceOptions: { ...target.playerChoiceOptions, isRequired: true, minCount: min, maxCount: max }
                    });
                },
                { mode: 'range', label: '选择数量', max: 10 }
            );
            
            grid.appendChild(this.createFormGroup('选择数量', slider));
            selectCountContainer.appendChild(grid);
            container.appendChild(selectCountContainer);
        }

        // 控制容器显示/隐藏以保持布局间距
        container.style.display = hasContent ? 'block' : 'none';
    }

    createButtonGroup(label, options, currentValue, onChange, groupClass = '') {
        const wrapper = document.createElement('div');
        wrapper.className = `form-group ${groupClass}`;
        
        const labelEl = document.createElement('label');
        labelEl.className = 'form-label';
        labelEl.textContent = label;
        wrapper.appendChild(labelEl);

        const btnGroup = document.createElement('div');
        btnGroup.className = 'btn-group-row';
        
        options.forEach(opt => {
            const btn = document.createElement('button');
            btn.className = `btn-option ${currentValue === opt.value ? 'active' : ''}`;
            btn.textContent = opt.label;
            btn.dataset.value = opt.value;
            btn.addEventListener('click', () => onChange(opt.value));
            btnGroup.appendChild(btn);
        });
        
        wrapper.appendChild(btnGroup);
        return wrapper;
    }

    createCheckboxGroup(label, options, currentValues, onChange, groupClass = '') {
        const wrapper = document.createElement('div');
        wrapper.className = `form-group ${groupClass}`;
        
        const labelEl = document.createElement('label');
        labelEl.className = 'form-label';
        labelEl.textContent = label;
        wrapper.appendChild(labelEl);

        const btnGroup = document.createElement('div');
        btnGroup.className = 'btn-group-row';
        
        options.forEach(opt => {
            const btn = document.createElement('button');
            const isChecked = currentValues.includes(opt.value);
            btn.className = `btn-option ${isChecked ? 'active' : ''}`;
            btn.textContent = opt.label;
            btn.dataset.value = opt.value;
            btn.addEventListener('click', () => {
                // 重新获取当前值，因为闭包中的 currentValues 可能是旧的
                // 但这里我们依赖外部传入的 onChange 来处理状态更新，所以这里只负责触发
                // 实际上，为了 UI 响应更快，我们可以先切换 active 类，但为了数据一致性，
                // 最好还是依赖 refreshSimplifiedUI。
                // 这里的逻辑是：点击 -> 计算新值 -> onChange -> updateSimplifiedTarget -> refreshSimplifiedUI
                
                // 注意：这里需要获取最新的选中状态，不能依赖闭包中的 currentValues
                // 我们可以通过检查 btn.classList.contains('active') 来判断
                const currentlyActive = btn.classList.contains('active');
                const newValues = currentlyActive
                    ? currentValues.filter(v => v !== opt.value)
                    : [...currentValues, opt.value];
                
                // 修正：由于 currentValues 是闭包变量，它不会更新。
                // 我们需要一种方式获取当前的真实值。
                // 简单的方法是：在 refreshSimplifiedUI 中更新 DOM 的 dataset，或者
                // 更好的方法是：在 updateSimplifiedTarget 中我们已经有了最新的 target 对象。
                // 但在这里（事件处理函数中），我们只能访问到闭包时的 currentValues。
                // 这是一个潜在的 Bug。
                
                // 修复方案：onChange 回调应该接收 "toggle" 指令而不是绝对值，或者
                // 我们在 createCheckboxGroup 时不绑定具体的值计算逻辑，而是让上层处理。
                // 但为了保持接口简单，我们可以在这里做一个 hack：
                // 从 DOM 中读取当前所有 active 的按钮值。
                
                const allActiveBtns = Array.from(btnGroup.querySelectorAll('.btn-option.active'));
                let currentActiveValues = allActiveBtns.map(b => b.dataset.value);
                
                if (currentlyActive) {
                    currentActiveValues = currentActiveValues.filter(v => v !== opt.value);
                } else {
                    currentActiveValues.push(opt.value);
                }
                
                onChange(currentActiveValues);
            });
            btnGroup.appendChild(btn);
        });
        
        wrapper.appendChild(btnGroup);
        return wrapper;
    }

    updateSimplifiedTarget(index, updates) {
        const target = { ...this.targets[index], ...updates };
        
        // 根据简化状态生成 DSL
        const method = target._simpleMethod || 'All';
        const scopes = Array.isArray(target._simpleScope) && target._simpleScope.length > 0
            ? target._simpleScope
            : ['Self'];
        const zones = Array.isArray(target._simpleZone) && target._simpleZone.length > 0
            ? target._simpleZone
            : ['Battlefield'];
        const types = Array.isArray(target._simpleType) && target._simpleType.length > 0
            ? target._simpleType
            : ['Unit'];

        const scopeSet = new Set(scopes);
        const zoneSet = new Set(zones);
        const typeSet = new Set(types);
        const hasLeaderType = typeSet.has('Leader');
        const nonLeaderTypes = Array.from(typeSet).filter(type => type !== 'Leader');

        const mapScopeRoot = (scope) => scope === 'Opponent' ? 'opponent' : 'owner';
        const mapZoneProperty = (zone) => {
            switch (zone) {
                case 'Hand':
                    return 'hand';
                case 'Deck':
                    return 'deck';
                case 'Graveyard':
                    return 'graveyard';
                case 'Battlefield':
                default:
                    return 'field';
            }
        };
        const mapTypePredicate = (type) => {
            switch (type) {
                case 'Unit':
                    return 'self.Type == "Follower"';
                case 'Spell':
                    return 'self.Type == "Spell"';
                case 'Amulet':
                    return 'self.Type == "Amulet"';
                case 'Leader':
                    return 'self.Type == "Leader"';
                default:
                    return null;
            }
        };

        const wrapForCall = (expression) => {
            if (!expression) return expression;
            const trimmed = expression.trim();
            if (trimmed.startsWith('(') && trimmed.endsWith(')')) {
                return trimmed;
            }
            if (trimmed.includes(' | ') || trimmed.includes(' & ') || trimmed.includes(' - ')) {
                return `(${trimmed})`;
            }
            return trimmed;
        };

        const scopeRoots = [];
        if (scopeSet.has('Self')) scopeRoots.push('owner');
        if (scopeSet.has('Opponent')) scopeRoots.push('opponent');
        if (scopeRoots.length === 0) scopeRoots.push('owner');

        const zoneValues = zoneSet.size > 0 ? Array.from(zoneSet) : ['Battlefield'];
        const zoneSources = [];
        scopeRoots.forEach((scopeRoot) => {
            zoneValues.forEach((zone) => {
                zoneSources.push(`${scopeRoot}.${mapZoneProperty(zone)}`);
            });
        });

        let nonLeaderSource = null;
        if (nonLeaderTypes.length > 0) {
            if (zoneSources.length === 0) {
                zoneSources.push('owner.field');
            }
            nonLeaderSource = zoneSources.length > 1
                ? `(${zoneSources.join(' | ')})`
                : zoneSources[0];

            const typePredicates = nonLeaderTypes
                .map(mapTypePredicate)
                .filter(Boolean);
            if (typePredicates.length > 0) {
                const typeExpr = typePredicates.length > 1
                    ? `(${typePredicates.join(' || ')})`
                    : typePredicates[0];
                nonLeaderSource = `${wrapForCall(nonLeaderSource)}.query(${typeExpr})`;
            }
        }

        let leaderSource = null;
        if (hasLeaderType) {
            leaderSource = 'state.allLeaders';
            if (scopeRoots.length === 1) {
                leaderSource = `${leaderSource}.query(self.OwnerPlayerId == ${scopeRoots[0]}.PlayerId)`;
            }
        }

        let sourcePoolExpression = '';
        if (nonLeaderSource && leaderSource) {
            sourcePoolExpression = `(${nonLeaderSource} | ${leaderSource})`;
        } else if (nonLeaderSource) {
            sourcePoolExpression = nonLeaderSource;
        } else if (leaderSource) {
            sourcePoolExpression = leaderSource;
        } else {
            sourcePoolExpression = 'owner.field';
        }

        if (method === 'Random') {
            const count = Math.max(1, Number(target._simpleRandomCount || 1));
            sourcePoolExpression = `${wrapForCall(sourcePoolExpression)}.take(random ${count})`;
            target.playerChoiceOptions = null;
        } else if (method === 'Select') {
            if (!target.playerChoiceOptions) {
                target.playerChoiceOptions = { isRequired: true, minCount: 1, maxCount: 1 };
            }
        } else {
            target.playerChoiceOptions = null;
        }

        this.targets[index] = {
            ...target,
            query: { sourcePoolExpression, operations: [] },
            playerChoiceOptions: target.playerChoiceOptions
        };
        
        // 优化：仅更新 UI 状态而不重建 DOM
        this.refreshSimplifiedUI(index);
        this.notifyChange();
    }

    refreshSimplifiedUI(index) {
        const targetNode = this.container.querySelector(`[data-target-index="${index}"]`);
        if (!targetNode) return;

        const target = this.targets[index];
        
        // 更新按钮状态
        const updateGroup = (groupClass, currentValue) => {
            const group = targetNode.querySelector(groupClass);
            if (!group) return;
            
            const buttons = group.querySelectorAll('.btn-option');
            buttons.forEach(btn => {
                const val = btn.dataset.value;
                const isActive = Array.isArray(currentValue)
                    ? currentValue.includes(val)
                    : currentValue === val;
                
                if (isActive) {
                    btn.classList.add('active');
                } else {
                    btn.classList.remove('active');
                }
            });
        };

        updateGroup('.method-group', target._simpleMethod || 'All');
        updateGroup('.scope-group', target._simpleScope || ['Self']);
        updateGroup('.zone-group', target._simpleZone || ['Battlefield']);
        updateGroup('.type-group', target._simpleType || ['Unit']);

        // 刷新动态内容 (滑块等)
        const dynamicContainer = targetNode.querySelector('.simplified-dynamic-content');
        if (dynamicContainer) {
            this.renderSimplifiedDynamicContent(dynamicContainer, target, index);
        }
    }

    parseDslToSimplifiedUI(target) {
        // 这是一个逆向解析过程，非常脆弱，仅用于演示
        // 实际项目中需要更强大的 AST 分析
        return {
            _simpleMethod: 'All',
            _simpleScope: ['Self'],
            _simpleZone: ['Battlefield'],
            _simpleType: ['Unit']
        };
    }

    // ... 其余辅助方法 (addTarget, removeTarget, etc.) ...
    addTarget() {
        this.targets.push({
            isEnabled: true,
            query: { sourcePoolExpression: '', operations: [] },
            _uiMode: 'simplified', // 默认使用简化模式
            _simpleMethod: 'All',
            _simpleScope: ['Self'],
            _simpleZone: ['Battlefield'],
            _simpleType: ['Unit']
        });
        this.renderTargetsList(this.container.querySelector('#targets-list-container'));
        this.notifyChange();
    }

    removeTarget(index) {
        if (confirm('确定删除此目标？')) {
            this.targets.splice(index, 1);
            this.renderTargetsList(this.container.querySelector('#targets-list-container'));
            this.notifyChange();
        }
    }

    updateTarget(index, updates) {
        this.targets[index] = { ...this.targets[index], ...updates };
        this.notifyChange();
    }

    updateTargetQuery(index, updates) {
        this.targets[index].query = { ...this.targets[index].query, ...updates };
        this.notifyChange();
    }

    updatePlayerChoiceOption(index, updates) {
        const currentOptions = this.targets[index].playerChoiceOptions || {};
        const { prompt, ...rest } = currentOptions;
        this.targets[index].playerChoiceOptions = { ...rest, ...updates };
        this.notifyChange();
    }

    normalizeTargetConfig(target) {
        if (!target || typeof target !== 'object') return target;
        const { onFailure, ...rest } = target;

        if (rest.playerChoiceOptions && typeof rest.playerChoiceOptions === 'object') {
            const { prompt, ...playerChoiceOptions } = rest.playerChoiceOptions;
            rest.playerChoiceOptions = playerChoiceOptions;
        }

        if (rest.isEnabled === undefined) {
            rest.isEnabled = true;
        }

        return rest;
    }

    showAddOperationMenu(index) {
        // 简化版菜单
        const type = prompt('输入操作类型 (FilterOperation, TakeOperation, RandomOperation, SortOperation):', 'FilterOperation');
        if (type) this.addOperation(index, type);
    }

    addOperation(targetIndex, type) {
        const op = { $type: type };
        if (type === 'FilterOperation') op.filters = [];
        if (type === 'TakeOperation' || type === 'RandomOperation') op.count = 1;
        this.targets[targetIndex].query.operations.push(op);
        this.updateTargetNode(targetIndex);
        this.notifyChange();
    }

    removeOperation(targetIndex, opIndex) {
        this.targets[targetIndex].query.operations.splice(opIndex, 1);
        this.updateTargetNode(targetIndex);
        this.notifyChange();
    }

    addFilter(targetIndex, opIndex) {
        this.targets[targetIndex].query.operations[opIndex].filters.push({ valueSource: { formula: '' } });
        this.updateTargetNode(targetIndex);
        this.notifyChange();
    }

    updateOperationFilter(targetIndex, opIndex, filterIndex, expr) {
        this.targets[targetIndex].query.operations[opIndex].filters[filterIndex].valueSource.formula = expr;
        this.notifyChange();
    }

    updateOperation(targetIndex, opIndex, updates) {
        const op = this.targets[targetIndex].query.operations[opIndex];
        this.targets[targetIndex].query.operations[opIndex] = { ...op, ...updates };
        this.notifyChange();
    }

    getOperationMetadata(type) {
        return (this.metadata.queryOperations || []).find(op => op.typeName === type);
    }

    notifyChange() {
        if (this.onChange) this.onChange(this.targets);
    }
}
