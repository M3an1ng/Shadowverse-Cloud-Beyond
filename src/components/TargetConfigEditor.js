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
        this.targets = targets;
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
                <h4>目标选择配置</h4>
                <p class="step-intro">定义能力将影响哪些对象。你可以配置多个目标，并为每个目标指定筛选条件。</p>
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
                <h5 class="empty-title">暂无目标配置</h5>
                <p class="empty-desc">定义能力将影响哪些对象<br>点击下方按钮添加第一个目标</p>
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
        header.innerHTML = `
            <div class="header-info">
                <span class="item-index">#${index + 1}</span>
                <span class="item-title">目标配置</span>
            </div>
            <div class="header-actions">
                <button class="btn-icon delete-btn" title="删除">×</button>
                <button class="btn-icon expand-btn" title="展开/折叠">▼</button>
            </div>
        `;

        // 绑定头部事件
        const expandBtn = header.querySelector('.expand-btn');
        const deleteBtn = header.querySelector('.delete-btn');
        
        deleteBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.removeTarget(index);
        });

        // 内容区域
        const content = document.createElement('div');
        content.className = 'accordion-content collapsed';
        
        // 1. 查询配置
        const querySection = this.createQueryEditor(target, index);
        content.appendChild(querySection);

        // 2. 验证与失败处理
        const validationSection = this.createValidationEditor(target, index);
        content.appendChild(validationSection);

        // 3. 玩家选择
        const playerChoiceSection = this.createPlayerChoiceEditor(target, index);
        content.appendChild(playerChoiceSection);

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

        // 操作列表
        const operationsGroup = this.createOperationsEditor(target, index);
        container.appendChild(operationsGroup);

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

        // 失败处理
        const failureOptions = [
            { value: 'AbortAbility', label: '中止能力' },
            { value: 'SkipEffect', label: '跳过效果' },
            { value: 'LogWarning', label: '仅记录警告' }
        ];
        const failureDropdown = this.createDropdown(
            failureOptions,
            target.onFailure || 'AbortAbility',
            (val) => this.updateTarget(index, { onFailure: val })
        );
        failureDropdown.className = 'form-select';
        grid.appendChild(this.createFormGroup('若无目标', failureDropdown));

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
            
            const promptInput = this.createTextInput(opts.prompt || '', (val) => {
                this.updatePlayerChoiceOption(index, { prompt: val });
            }, { placeholder: '例如：选择一个敌方随从' });
            promptInput.className = 'form-input';
            grid.appendChild(this.createFormGroup('提示文本', promptInput));

            // 数量限制
            const countGroup = document.createElement('div');
            countGroup.className = 'input-group-row';
            
            const minInput = this.createNumberInput(opts.minCount || 1, (val) => {
                this.updatePlayerChoiceOption(index, { minCount: val });
            }, { min: 0 });
            minInput.className = 'form-input small';
            
            const maxInput = this.createNumberInput(opts.maxCount || 1, (val) => {
                this.updatePlayerChoiceOption(index, { maxCount: val });
            }, { min: 1 });
            maxInput.className = 'form-input small';

            countGroup.appendChild(minInput);
            countGroup.appendChild(document.createTextNode(' 至 '));
            countGroup.appendChild(maxInput);
            
            grid.appendChild(this.createFormGroup('选择数量', countGroup));
            container.appendChild(grid);
        }

        return container;
    }

    // ... 辅助方法 (createToggleSwitch, updateTargetNode, etc.) ...
    // 复用 AbilityEditor 中的 createToggleSwitch 逻辑或将其提取到 MetadataDrivenComponent
    createToggleSwitch(checked, onChange, labelText) {
        const wrapper = document.createElement('label');
        wrapper.className = 'toggle-switch-wrapper';
        const input = document.createElement('input');
        input.type = 'checkbox';
        input.checked = checked;
        input.addEventListener('change', (e) => onChange(e.target.checked));
        const slider = document.createElement('span');
        slider.className = 'toggle-slider';
        const label = document.createElement('span');
        label.className = 'toggle-label';
        label.textContent = labelText;
        wrapper.appendChild(input);
        wrapper.appendChild(slider);
        wrapper.appendChild(label);
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

    // ... 其余辅助方法 (addTarget, removeTarget, etc.) ...
    addTarget() {
        this.targets.push({
            query: { sourcePoolExpression: '', operations: [] },
            onFailure: 'AbortAbility'
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
        this.targets[index].playerChoiceOptions = { ...this.targets[index].playerChoiceOptions, ...updates };
        this.notifyChange();
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
