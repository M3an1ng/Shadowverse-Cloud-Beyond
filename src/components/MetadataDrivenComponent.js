// src/components/MetadataDrivenComponent.js - 元数据驱动UI组件基类
//
// 相关文档:
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
//

import { metadataManager } from '../metadataManager.js';

/**
 * 元数据驱动UI组件基类
 * 提供通用的元数据访问和UI生成功能
 */
export class MetadataDrivenComponent {
    constructor() {
        this.metadata = null;
        this.isInitialized = false;
    }

    /**
     * 将后端类型名翻译为更易理解的中文显示。
     * 优先使用后端提供的 typeMetadata.displayName，其次使用前端内置映射。
     */
    getDisplayTypeName(typeName) {
        if (!typeName) return '未知类型';

        const trimmed = String(typeName).trim();
        if (!trimmed) return '未知类型';

        // 泛型集合：List<T> / IEnumerable<T>
        const match = trimmed.match(/^(List|IEnumerable)<(.+)>$/);
        if (match) {
            const inner = match[2];
            return `${this.getDisplayTypeName(inner)}列表`;
        }

        // 后端 DSL 元数据（DslExposable）提供的类型显示名
        const meta = this.metadata?.dsl?.typeMetadata?.[trimmed];
        if (meta?.displayName) return meta.displayName;

        // 常见 CLR/领域类型映射
        const map = {
            Int32: '整数',
            Int64: '整数',
            Number: '数字',
            Single: '数字',
            Double: '数字',
            Decimal: '数字',
            Boolean: '布尔',
            String: '文本',
            Guid: 'ID',
            DateTime: '时间',
            CardInstance: '卡牌',
            PlayerState: '玩家',
            TargetInfo: '目标',
            GameState: '对局状态',
            IAbility: '能力',
            Ability: '能力',
            Effect: '效果',
            IGameEvent: '事件'
        };

        return map[trimmed] || trimmed;
    }

    /**
     * 初始化组件，确保元数据已加载
     */
    async initialize() {
        if (this.isInitialized) {
            return;
        }

        try {
            this.metadata = await metadataManager.initialize();
            this.isInitialized = true;
            console.log('MetadataDrivenComponent: Initialized with metadata');
        } catch (error) {
            console.error('MetadataDrivenComponent: Failed to initialize:', error);
            throw error;
        }
    }

    /**
     * 确保组件已初始化
     */
    ensureInitialized() {
        if (!this.isInitialized) {
            throw new Error('组件尚未初始化，请先调用 initialize()');
        }
    }

    /**
     * 创建下拉选择器
     * @param {Array} options - 选项数组 [{value, label, description?}]
     * @param {string} selectedValue - 当前选中值
     * @param {Function} onChange - 变化回调
     * @param {string} placeholder - 占位符文本
     * @returns {HTMLElement} 下拉选择器元素
     */
    createDropdown(options, selectedValue, onChange, placeholder = '请选择...') {
        const select = document.createElement('select');
        select.className = 'form-select';

        // 添加占位符选项
        if (placeholder) {
            const placeholderOption = document.createElement('option');
            placeholderOption.value = '';
            placeholderOption.textContent = placeholder;
            placeholderOption.disabled = true;
            if (!selectedValue) {
                placeholderOption.selected = true;
            }
            select.appendChild(placeholderOption);
        }

        // 添加选项
        options.forEach(option => {
            const optionElement = document.createElement('option');
            optionElement.value = option.value;
            optionElement.textContent = option.label;
            if (option.description) {
                optionElement.title = option.description;
            }
            if (option.value === selectedValue) {
                optionElement.selected = true;
            }
            select.appendChild(optionElement);
        });

        // 绑定事件
        select.addEventListener('change', (e) => {
            onChange(e.target.value);
        });

        return select;
    }

    /**
     * 获取能力编辑器使用的分类顺序与默认标签。
     */
    getAbilityEditorCategoryOrder() {
        return [
            { id: 'base-action', label: '基础行动' },
            { id: 'position-change', label: '位置变化' },
            { id: 'temp-state', label: '临时状态' },
            { id: 'resource-value', label: '资源数值' },
            { id: 'turn-flow', label: '回合流程' },
            { id: 'special', label: '特殊事件' },
            { id: 'system-request', label: '系统请求' },
            { id: 'uncategorized', label: '未分类' }
        ];
    }

    /**
     * 创建多选标签组件
     * @param {Array} options - 选项数组
     * @param {Array} selectedValues - 当前选中值数组
     * @param {Function} onChange - 变化回调
     * @returns {HTMLElement} 多选标签容器
     */
    createMultiSelect(options, selectedValues = [], onChange) {
        const container = document.createElement('div');
        container.className = 'multi-select-container';

        options.forEach(option => {
            const label = document.createElement('label');
            label.className = 'multi-select-item';

            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.value = option.value;
            checkbox.checked = selectedValues.includes(option.value);

            const span = document.createElement('span');
            span.textContent = option.label;
            if (option.description) {
                span.title = option.description;
            }

            label.appendChild(checkbox);
            label.appendChild(span);
            container.appendChild(label);

            // 绑定事件
            checkbox.addEventListener('change', () => {
                const newValues = Array.from(container.querySelectorAll('input:checked'))
                    .map(input => input.value);
                onChange(newValues);
            });
        });

        return container;
    }

    /**
     * 创建数字输入框
     * @param {number} value - 当前值
     * @param {Function} onChange - 变化回调
     * @param {Object} options - 配置选项 {min, max, step, placeholder}
     * @returns {HTMLElement} 数字输入框
     */
    createNumberInput(value, onChange, options = {}) {
        const input = document.createElement('input');
        input.type = 'number';
        input.className = 'form-input';
        input.value = value || '';

        if (options.min !== undefined) input.min = options.min;
        if (options.max !== undefined) input.max = options.max;
        if (options.step !== undefined) input.step = options.step;
        if (options.placeholder) input.placeholder = options.placeholder;

        input.addEventListener('change', (e) => {
            const numValue = parseFloat(e.target.value);
            onChange(isNaN(numValue) ? null : numValue);
        });

        return input;
    }

    /**
     * 创建文本输入框
     * @param {string} value - 当前值
     * @param {Function} onChange - 变化回调
     * @param {Object} options - 配置选项 {placeholder, multiline}
     * @returns {HTMLElement} 文本输入框
     */
    createTextInput(value, onChange, options = {}) {
        const input = options.multiline ? 
            document.createElement('textarea') : 
            document.createElement('input');
        
        if (!options.multiline) {
            input.type = 'text';
        }
        
        input.className = 'form-input';
        input.value = value || '';
        
        if (options.placeholder) {
            input.placeholder = options.placeholder;
        }

        input.addEventListener('input', (e) => {
            onChange(e.target.value);
        });

        return input;
    }

    /**
     * 创建复选框
     * @param {boolean} checked - 是否选中
     * @param {Function} onChange - 变化回调
     * @param {Object} options - 选项
     */
    createCheckbox(checked, onChange, options = {}) {
        const container = document.createElement('div');
        container.className = 'checkbox-container';

        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.checked = !!checked;

        if (options.label) {
            const label = document.createElement('label');
            label.className = 'checkbox-label';

            label.appendChild(checkbox);

            const span = document.createElement('span');
            span.textContent = options.label;
            label.appendChild(span);

            container.appendChild(label);
        } else {
            container.appendChild(checkbox);
        }

        checkbox.addEventListener('change', (e) => {
            onChange(e.target.checked);
        });

        return container;
    }

    /**
     * 创建表单组
     * @param {string} label - 标签文本
     * @param {HTMLElement} control - 控件元素
     * @param {string} description - 描述文本
     * @returns {HTMLElement} 表单组容器
     */
    createFormGroup(label, control, description = null) {
        const group = document.createElement('div');
        group.className = 'form-group';

        if (label) {
            const labelElement = document.createElement('label');
            labelElement.className = 'form-label';
            if (label instanceof HTMLElement) {
                labelElement.appendChild(label);
            } else {
                labelElement.textContent = label;
            }
            group.appendChild(labelElement);
        }

        group.appendChild(control);

        if (description) {
            const desc = document.createElement('small');
            desc.className = 'form-description';
            desc.textContent = description;
            group.appendChild(desc);
        }

        return group;
    }

    /**
     * 创建按钮
     * @param {string} text - 按钮文本
     * @param {Function} onClick - 点击回调
     * @param {string} variant - 按钮样式变体 ('primary', 'secondary', 'danger')
     * @returns {HTMLElement} 按钮元素
     */
    createButton(text, onClick, variant = 'primary') {
        const button = document.createElement('button');
        button.className = `btn-${variant}`;
        button.textContent = text;
        button.addEventListener('click', onClick);
        return button;
    }

    /**
     * 创建可折叠面板
     * @param {string} title - 面板标题
     * @param {HTMLElement} content - 面板内容
     * @param {boolean} expanded - 是否默认展开
     * @returns {HTMLElement} 面板容器
     */
    createCollapsiblePanel(title, content, expanded = true) {
        const panel = document.createElement('div');
        panel.className = 'collapsible-panel';

        const header = document.createElement('div');
        header.className = 'panel-header';
        header.textContent = title;
        header.style.cursor = 'pointer';

        const body = document.createElement('div');
        body.className = 'panel-body';
        body.style.display = expanded ? 'block' : 'none';
        body.appendChild(content);

        header.addEventListener('click', () => {
            const isVisible = body.style.display !== 'none';
            body.style.display = isVisible ? 'none' : 'block';
            header.classList.toggle('collapsed', isVisible);
        });

        panel.appendChild(header);
        panel.appendChild(body);

        return panel;
    }

    /**
     * 显示错误消息
     * @param {string} message - 错误消息
     * @param {HTMLElement} container - 容器元素
     */
    showError(message, container) {
        const errorDiv = document.createElement('div');
        errorDiv.className = 'error-message';
        errorDiv.textContent = message;
        container.appendChild(errorDiv);
    }

    /**
     * 清除容器内容
     * @param {HTMLElement} container - 容器元素
     */
    clearContainer(container) {
        if (container) {
            while (container.firstChild) {
                container.removeChild(container.firstChild);
            }
        }
    }
}
