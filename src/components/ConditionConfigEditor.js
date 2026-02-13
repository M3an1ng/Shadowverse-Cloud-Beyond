// src/components/ConditionConfigEditor.js - 条件配置编辑器 (Refactored)
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md (第4节)
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md (第6节)
//

import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';
import { DslEditor } from './DslEditor.js';

/**
 * 条件配置编辑器
 * 提供可视化DSL表达式编辑器用于构建条件逻辑
 * 采用手风琴式列表设计，与目标配置编辑器保持一致
 */
export class ConditionConfigEditor extends MetadataDrivenComponent {
    constructor() {
        super();
        this.conditions = [];
        this.onChange = null;
        this.container = null;
        this.dslEditors = new Map(); // 存储DSL编辑器实例
        this.options = {};
    }

    /**
     * 渲染条件配置编辑器
     * @param {HTMLElement} container - 容器元素
     * @param {Array} conditions - 条件配置数组
     * @param {Function} onChange - 配置变化回调
     */
    async render(container, conditions = [], onChange = () => {}, options = {}) {
        await this.initialize();
        
        this.container = container;
        this.conditions = conditions;
        this.onChange = onChange;
        this.options = options || {};

        this.clearContainer(container);
        
        try {
            const wrapper = document.createElement('div');
            wrapper.className = 'step-content-wrapper conditions-step';

            // 标题区域
            const header = document.createElement('div');
            header.className = 'step-header';
            const titleText = this.options?.title || '发动条件配置';
            const introText = this.options?.intro || '设置能力发动的限制条件。只有满足所有启用条件时，能力才会执行。';
            const showHelp = this.options?.hideHelp !== true;
            const helpHtml = showHelp ? `
                    <div class="help-icon-wrapper">
                        <button class="btn-icon help-btn" id="condition-help-btn">?</button>
                        <div class="info-tooltip" id="condition-help-tooltip">
                            <h6 class="tooltip-title">执行逻辑</h6>
                            <div class="tooltip-section">
                                <div class="tooltip-item">
                                    <span class="label">无条件</span>
                                    <span class="desc">能力将在触发时直接执行</span>
                                </div>
                                <div class="tooltip-item">
                                    <span class="label">单个条件</span>
                                    <span class="desc">条件为 true 时执行能力</span>
                                </div>
                                <div class="tooltip-item">
                                    <span class="label">多个条件</span>
                                    <span class="desc">所有启用的条件都为 true 时才执行 (AND)</span>
                                </div>
                            </div>
                            <h6 class="tooltip-title">常用示例</h6>
                            <ul class="tooltip-list">
                                <li><code>player.pp >= 5</code> <span class="desc">PP大于等于5</span></li>
                                <li><code>Count(player.hand) > 3</code> <span class="desc">手牌数大于3</span></li>
                                <li><code>target.is_follower</code> <span class="desc">目标是随从</span></li>
                            </ul>
                        </div>
                    </div>
                ` : '';

            header.innerHTML = `
                <div class="header-with-help">
                    <h4>${titleText}</h4>
                    ${helpHtml}
                </div>
                <p class="step-intro">${introText}</p>
            `;
            wrapper.appendChild(header);

            // 绑定帮助提示交互
            if (showHelp) {
                this.bindHelpTooltip(header);
            }

            // 创建条件列表容器
            const listContainer = document.createElement('div');
            listContainer.className = 'conditions-list';
            listContainer.id = 'conditions-list-container';
            wrapper.appendChild(listContainer);
            this.renderConditionsList(listContainer);
            
            // 创建添加条件按钮
            const actionsContainer = document.createElement('div');
            actionsContainer.className = 'add-condition-container';
            const addBtn = this.createButton('添加新条件', () => this.addCondition(), 'primary');
            actionsContainer.appendChild(addBtn);
            wrapper.appendChild(actionsContainer);
            
            container.appendChild(wrapper);
            
        } catch (error) {
            console.error('ConditionConfigEditor: Render failed:', error);
            this.showError(`渲染条件配置失败: ${error.message}`, container);
        }
    }

    /**
     * 绑定帮助提示的交互逻辑
     */
    bindHelpTooltip(header) {
        const btn = header.querySelector('#condition-help-btn');
        const tooltip = header.querySelector('#condition-help-tooltip');
        
        if (!btn || !tooltip) return;

        // 鼠标悬停显示
        btn.addEventListener('mouseenter', () => {
            tooltip.classList.add('visible');
        });

        btn.addEventListener('mouseleave', () => {
            tooltip.classList.remove('visible');
        });
        
        // 确保 tooltip 自身被悬停时也不消失（方便复制内容）
        tooltip.addEventListener('mouseenter', () => {
            tooltip.classList.add('visible');
        });
        
        tooltip.addEventListener('mouseleave', () => {
            tooltip.classList.remove('visible');
        });
    }

    /**
     * 渲染条件列表
     */
    renderConditionsList(container) {
        this.clearContainer(container);

        if (this.conditions.length === 0) {
            const placeholder = document.createElement('div');
            placeholder.className = 'empty-state';
            placeholder.innerHTML = `
                <div class="empty-state-visual">
                    <div class="target-ring-outer condition-ring">
                        <div class="target-ring-inner">
                            <div class="target-dot condition-dot"></div>
                        </div>
                    </div>
                </div>
                <h5 class="empty-title">暂无条件配置</h5>
                <p class="empty-desc">能力将在触发时无条件执行<br>点击下方按钮添加限制条件</p>
            `;
            container.appendChild(placeholder);
        } else {
            this.conditions.forEach((condition, index) => {
                const conditionNode = this.createConditionNode(condition, index);
                container.appendChild(conditionNode);
            });
        }
    }

    /**
     * 创建单个条件节点 (Accordion Style)
     * @param {Object} condition - 条件配置
     * @param {number} index - 条件索引
     */
    createConditionNode(condition, index) {
        const node = document.createElement('div');
        node.className = 'condition-node accordion-item';
        node.dataset.conditionIndex = index;

        // 头部
        const header = document.createElement('div');
        header.className = 'accordion-header';
        
        // 状态标记
        const statusClass = condition.isEnabled !== false ? 'status-enabled' : 'status-disabled';
        const statusText = condition.isEnabled !== false ? '启用' : '禁用';

        const headerInfo = document.createElement('div');
        headerInfo.className = 'header-info';
        headerInfo.innerHTML = `
            <span class="item-index">#${index + 1}</span>
            <span class="item-title">条件配置</span>
            <span class="item-status ${statusClass}">${statusText}</span>
        `;

        const headerActions = document.createElement('div');
        headerActions.className = 'header-actions';

        const toggle = this.createToggleSwitch(
            condition.isEnabled !== false,
            (checked) => {
                this.updateCondition(index, { isEnabled: checked });
                const statusSpan = header.querySelector('.item-status');
                if (checked) {
                    statusSpan.textContent = '启用';
                    statusSpan.className = 'item-status status-enabled';
                    toggle.title = '点击禁用条件';
                } else {
                    statusSpan.textContent = '禁用';
                    statusSpan.className = 'item-status status-disabled';
                    toggle.title = '点击启用条件';
                }
            },
            '',
            { compact: true }
        );
        toggle.title = condition.isEnabled !== false ? '点击禁用条件' : '点击启用条件';

        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'btn-icon delete-btn';
        deleteBtn.title = '删除';
        deleteBtn.setAttribute('aria-label', '删除');

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
            this.removeCondition(index);
        });

        // 内容区域
        const content = document.createElement('div');
        content.className = 'accordion-content collapsed';

        // 条件表达式编辑器
        const expressionGroup = this.createConditionExpressionEditor(condition, index);
        content.appendChild(expressionGroup);

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

    /**
     * 创建条件表达式编辑器
     */
    createConditionExpressionEditor(condition, index) {
        const container = document.createElement('div');
        container.className = 'config-section';
        
        // 创建DSL编辑器
        const dslEditor = new DslEditor();
        const editorContainer = document.createElement('div');
        editorContainer.className = 'dsl-editor-wrapper';
        
        // 存储编辑器实例
        const editorKey = `condition_${index}`;
        this.dslEditors.set(editorKey, dslEditor);

        // 渲染DSL编辑器（异步）
        dslEditor.render(
            editorContainer,
            condition.expression || '',
            (expression) => {
                this.updateCondition(index, { expression: expression });
            },
            {
                expectedType: 'Boolean',
                triggeringEventContext: {
                    eventTypes: this.options?.trigger?.eventTypes || [],
                    timing: this.options?.trigger?.timing || 'On',
                    outputVariables: this.options?.triggeringEventOutputs || []
                }
            }
        ).catch(error => {
            console.error('ConditionConfigEditor: Failed to render DSL editor:', error);
            editorContainer.innerHTML = `<div class="error">DSL编辑器加载失败: ${error.message}</div>`;
        });

        container.appendChild(this.createFormGroup(
            '条件表达式',
            editorContainer,
            '构建一个返回 true 或 false 的逻辑表达式'
        ));

        return container;
    }

    /**
     * 创建条件描述编辑器
     */
    /**
     * 复用 Toggle Switch 创建逻辑
     */
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
        input.setAttribute('aria-label', labelText || '启用条件');
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

    /**
     * 添加新条件
     */
    addCondition() {
        const newCondition = {
            id: this.generateConditionId(),
            expression: '',
            isEnabled: true
        };

        this.conditions.push(newCondition);
        this.updateConditionsList();
        this.notifyChange();
    }

    /**
     * 移除条件
     * @param {number} index - 条件索引
     */
    removeCondition(index) {
        if (confirm('确定要删除这个条件吗？')) {
            // 清理对应的DSL编辑器
            const editorKey = `condition_${index}`;
            this.dslEditors.delete(editorKey);
            
            this.conditions.splice(index, 1);
            this.updateConditionsList();
            this.notifyChange();
        }
    }

    /**
     * 更新条件
     * @param {number} index - 条件索引
     * @param {Object} updates - 更新内容
     */
    updateCondition(index, updates) {
        if (index >= 0 && index < this.conditions.length) {
            this.conditions[index] = { ...this.conditions[index], ...updates };
            this.notifyChange();
        }
    }

    /**
     * 更新条件列表显示
     */
    updateConditionsList() {
        const listContainer = this.container.querySelector('#conditions-list-container');
        if (listContainer) {
            this.renderConditionsList(listContainer);
        }
    }

    /**
     * 生成条件ID
     */
    generateConditionId() {
        return `condition_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    }

    /**
     * 通知配置变化
     */
    notifyChange() {
        if (this.onChange) {
            this.onChange(this.conditions);
        }
    }

    /**
     * 验证条件配置
     */
    validateConditions() {
        const errors = [];

        this.conditions.forEach((condition, index) => {
            if (condition.isEnabled !== false) { // 只验证启用的条件
                if (!condition.expression || condition.expression.trim() === '') {
                    errors.push(`条件 ${index + 1}: 表达式不能为空`);
                }
                
                // 简单的表达式语法检查
                if (condition.expression && !this.isValidExpression(condition.expression)) {
                    errors.push(`条件 ${index + 1}: 表达式语法可能有误`);
                }
            }
        });

        return errors;
    }

    /**
     * 简单的表达式验证
     * @param {string} expression - 表达式字符串
     */
    isValidExpression(expression) {
        // 这是一个简化的验证，实际项目中应该使用更完善的语法检查
        if (!expression || expression.trim() === '') {
            return false;
        }

        // 宽松验证：只要有非空字符即可，避免误报
        return expression.length > 0;
    }

    /**
     * 清理资源
     */
    cleanup() {
        // 清理所有DSL编辑器实例
        this.dslEditors.clear();
    }
}
