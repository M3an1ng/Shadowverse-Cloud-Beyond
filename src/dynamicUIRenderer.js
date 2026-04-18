// src/dynamicUIRenderer.js - 动态UI渲染器
//
// 相关文档:
// - UI节点映射: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
// - 开发实践指南: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

import { DslEditor } from './components/DslEditor.js';
import { loadCardDefinitions } from './cardDefinitionsLoader.js';

/**
 * 动态UI渲染器
 * 根据元数据动态生成UI组件
 */
export class DynamicUIRenderer {
    constructor() {
        this.metadataManager = null;
        this.componentRegistry = new Map();
        this.isInitialized = false;
    }

    /**
     * 初始化渲染器
     */
    async initialize(metadataManager) {
        if (this.isInitialized) {
            return;
        }

        this.metadataManager = metadataManager;
        this.registerDefaultComponents();
        this.isInitialized = true;

    }

    /**
     * 注册默认UI组件
     */
    registerDefaultComponents() {
        // 注册基础输入组件
        this.componentRegistry.set('TextInput', this.createTextInput.bind(this));
        this.componentRegistry.set('NumberStepper', this.createNumberStepper.bind(this));
        this.componentRegistry.set('Checkbox', this.createCheckbox.bind(this));
        this.componentRegistry.set('Dropdown', this.createDropdown.bind(this));
        this.componentRegistry.set('TextArea', this.createTextArea.bind(this));
        this.componentRegistry.set('CardSelector', this.createCardSelector.bind(this));
        this.componentRegistry.set('TargetSelector', this.createTargetSelector.bind(this));
        this.componentRegistry.set('ValueConfigEditor', this.createValueConfigEditor.bind(this));

    }

    /**
     * 根据参数元数据渲染UI组件
     */
    renderParameter(parameterMeta, value = null, onChange = null) {
        if (!parameterMeta) {
            console.error('DynamicUIRenderer: Parameter metadata is required');
            return this.createErrorElement('参数元数据缺失');
        }

        // 确定使用的组件类型
        const componentType = this.determineComponentType(parameterMeta);
        
        // 获取组件渲染函数
        const renderFunction = this.componentRegistry.get(componentType);
        if (!renderFunction) {
            console.error(`DynamicUIRenderer: Unknown component type "${componentType}"`);
            return this.createErrorElement(`未知组件类型: ${componentType}`);
        }

        try {
            return renderFunction(parameterMeta, value, onChange);
        } catch (error) {
            console.error('DynamicUIRenderer: Error rendering parameter:', error);
            return this.createErrorElement(`渲染错误: ${error.message}`);
        }
    }

    /**
     * 确定参数应使用的组件类型
     */
    determineComponentType(parameterMeta) {
        // 如果元数据中指定了控件类型，直接使用
        if (parameterMeta.controlType) {
            return this.mapControlTypeToComponent(parameterMeta.controlType);
        }

        // 根据参数类型和名称推断组件类型
        const typeName = parameterMeta.typeName?.toLowerCase() || '';
        const paramName = parameterMeta.name?.toLowerCase() || parameterMeta.propertyName?.toLowerCase() || '';

        // 布尔类型使用复选框
        if (typeName === 'boolean' || typeName === 'bool') {
            return 'Checkbox';
        }

        // 数字类型使用数字步进器
        if (typeName === 'int32' || typeName === 'int' || typeName === 'double' || typeName === 'float' ||
            typeName === 'decimal' || typeName === 'single') {
            return 'NumberStepper';
        }

        // ValueConfig类型使用DSL编辑器
        if (typeName === 'valueconfig' || paramName.includes('valueconfig')) {
            return 'ValueConfigEditor';
        }

        // 包含"amount"、"count"、"value"的参数使用数字步进器
        if (paramName.includes('amount') || paramName.includes('count') || paramName.includes('value')) {
            return 'NumberStepper';
        }

        // 包含"card"的参数可能需要卡牌选择器
        if (paramName.includes('card') || paramName.includes('carddef')) {
            return 'CardSelector';
        }

        // 包含"target"的参数使用目标选择器
        if (paramName.includes('target')) {
            return 'TargetSelector';
        }

        // 包含"expression"、"formula"的参数使用DSL编辑器
        if (paramName.includes('expression') || paramName.includes('formula')) {
            return 'ValueConfigEditor';
        }

        // 枚举类型使用下拉菜单
        if (parameterMeta.isEnum || (parameterMeta.options && parameterMeta.options.length > 0)) {
            return 'Dropdown';
        }

        // 字符串类型
        if (typeName === 'string') {
            // 长文本使用文本区域
            if (paramName.includes('description') || paramName.includes('text') || paramName.includes('content')) {
                return 'TextArea';
            }
            return 'TextInput';
        }

        // 默认使用文本输入
        return 'TextInput';
    }

    /**
     * 映射ControlType枚举到组件类型
     */
    mapControlTypeToComponent(controlType) {
        const mapping = {
            'TextInput': 'TextInput',
            'NumberStepper': 'NumberStepper',
            'Checkbox': 'Checkbox',
            'Dropdown': 'Dropdown',
            'TextArea': 'TextArea',
            'CardSelector': 'CardSelector',
            'TargetSelector': 'TargetSelector',
            'DslEditor': 'ValueConfigEditor',
            'ValueConfigEditor': 'ValueConfigEditor'
        };

        return mapping[controlType] || 'TextInput';
    }

    /**
     * 创建文本输入组件
     */
    createTextInput(parameterMeta, value, onChange) {
        const container = document.createElement('div');
        container.className = 'form-group';

        const label = document.createElement('label');
        label.textContent = parameterMeta.displayName || parameterMeta.name;
        label.className = 'form-label';

        const input = document.createElement('input');
        input.type = 'text';
        input.className = 'form-input';
        input.value = value || '';
        input.placeholder = parameterMeta.description || '';

        if (onChange) {
            input.addEventListener('input', (e) => onChange(e.target.value));
        }

        container.appendChild(label);
        container.appendChild(input);

        return container;
    }

    /**
     * 创建数字步进器组件
     */
    createNumberStepper(parameterMeta, value, onChange) {
        const container = document.createElement('div');
        container.className = 'form-group';

        const label = document.createElement('label');
        label.textContent = parameterMeta.displayName || parameterMeta.name;
        label.className = 'form-label';

        const inputContainer = document.createElement('div');
        inputContainer.className = 'number-stepper';

        const input = document.createElement('input');
        input.type = 'number';
        input.className = 'form-input number-input';
        input.value = value || 0;
        input.min = parameterMeta.min || 0;
        input.max = parameterMeta.max || 999;

        const decreaseBtn = document.createElement('button');
        decreaseBtn.type = 'button';
        decreaseBtn.className = 'stepper-btn decrease';
        decreaseBtn.textContent = '-';

        const increaseBtn = document.createElement('button');
        increaseBtn.type = 'button';
        increaseBtn.className = 'stepper-btn increase';
        increaseBtn.textContent = '+';

        // 事件处理
        const handleChange = () => {
            if (onChange) {
                onChange(parseInt(input.value) || 0);
            }
        };

        input.addEventListener('input', handleChange);
        decreaseBtn.addEventListener('click', () => {
            input.value = Math.max((parseInt(input.value) || 0) - 1, parseInt(input.min) || 0);
            handleChange();
        });
        increaseBtn.addEventListener('click', () => {
            input.value = Math.min((parseInt(input.value) || 0) + 1, parseInt(input.max) || 999);
            handleChange();
        });

        inputContainer.appendChild(decreaseBtn);
        inputContainer.appendChild(input);
        inputContainer.appendChild(increaseBtn);

        container.appendChild(label);
        container.appendChild(inputContainer);

        return container;
    }

    /**
     * 创建复选框组件
     */
    createCheckbox(parameterMeta, value, onChange) {
        const container = document.createElement('div');
        container.className = 'form-group checkbox-group';

        const label = document.createElement('label');
        label.className = 'checkbox-label';

        const input = document.createElement('input');
        input.type = 'checkbox';
        input.className = 'form-checkbox';
        input.checked = !!value;

        const labelText = document.createElement('span');
        labelText.textContent = parameterMeta.displayName || parameterMeta.name;

        if (onChange) {
            input.addEventListener('change', (e) => onChange(e.target.checked));
        }

        label.appendChild(input);
        label.appendChild(labelText);
        container.appendChild(label);

        return container;
    }

    /**
     * 创建下拉菜单组件
     */
    createDropdown(parameterMeta, value, onChange) {
        const container = document.createElement('div');
        container.className = 'form-group';

        const label = document.createElement('label');
        label.textContent = parameterMeta.displayName || parameterMeta.name;
        label.className = 'form-label';

        const select = document.createElement('select');
        select.className = 'form-select';
        select.value = value || '';

        // 添加默认选项
        const defaultOption = document.createElement('option');
        defaultOption.value = '';
        defaultOption.textContent = '请选择...';
        select.appendChild(defaultOption);

        // 如果参数元数据中包含选项，则添加选项
        if (parameterMeta.options && Array.isArray(parameterMeta.options)) {
            parameterMeta.options.forEach(option => {
                const optionElement = document.createElement('option');
                optionElement.value = option.value || option;
                optionElement.textContent = option.label || option;
                if (value === optionElement.value) {
                    optionElement.selected = true;
                }
                select.appendChild(optionElement);
            });
        }

        if (onChange) {
            select.addEventListener('change', (e) => onChange(e.target.value));
        }

        container.appendChild(label);
        container.appendChild(select);

        return container;
    }

    /**
     * 创建文本区域组件
     */
    createTextArea(parameterMeta, value, onChange) {
        const container = document.createElement('div');
        container.className = 'form-group';

        const label = document.createElement('label');
        label.textContent = parameterMeta.displayName || parameterMeta.name;
        label.className = 'form-label';

        const textarea = document.createElement('textarea');
        textarea.className = 'form-textarea';
        textarea.value = value || '';
        textarea.placeholder = parameterMeta.description || '';
        textarea.rows = 4;

        if (onChange) {
            textarea.addEventListener('input', (e) => onChange(e.target.value));
        }

        container.appendChild(label);
        container.appendChild(textarea);

        return container;
    }

    /**
     * 创建卡牌选择器组件
     */
    createCardSelector(parameterMeta, value, onChange) {
        const container = document.createElement('div');
        container.className = 'form-group';

        const label = document.createElement('label');
        label.textContent = parameterMeta.displayName || parameterMeta.name;
        label.className = 'form-label';

        const selectorContainer = document.createElement('div');
        selectorContainer.className = 'card-selector-container';

        const searchInput = document.createElement('input');
        searchInput.type = 'text';
        searchInput.className = 'card-search-input';
        searchInput.placeholder = '搜索卡牌...';

        const selectedCard = document.createElement('div');
        selectedCard.className = 'selected-card-display';
        selectedCard.innerHTML = value ?
            `<span class="selected-card-name">${value}</span><button class="clear-selection-btn">×</button>` :
            '<span class="no-card-selected">未选择卡牌</span>';

        const cardList = document.createElement('div');
        cardList.className = 'card-selector-list';
        cardList.style.display = 'none';

        // 搜索事件
        searchInput.addEventListener('input', (e) => {
            this.searchCards(e.target.value, cardList);
        });

        // 聚焦时显示列表
        searchInput.addEventListener('focus', () => {
            cardList.style.display = 'block';
            this.loadCardOptions(cardList, onChange, selectedCard);
        });

        // 点击外部隐藏列表
        document.addEventListener('click', (e) => {
            if (!selectorContainer.contains(e.target)) {
                cardList.style.display = 'none';
            }
        });

        selectorContainer.appendChild(searchInput);
        selectorContainer.appendChild(selectedCard);
        selectorContainer.appendChild(cardList);

        container.appendChild(label);
        container.appendChild(selectorContainer);

        return container;
    }

    /**
     * 创建目标选择器组件（占位符）
     */
    createTargetSelector(parameterMeta, value, onChange) {
        const container = document.createElement('div');
        container.className = 'form-group';

        const label = document.createElement('label');
        label.textContent = parameterMeta.displayName || parameterMeta.name;
        label.className = 'form-label';

        const placeholder = document.createElement('div');
        placeholder.className = 'target-selector-placeholder';
        placeholder.textContent = '目标选择器功能正在开发中...';

        container.appendChild(label);
        container.appendChild(placeholder);

        return container;
    }

    /**
     * 创建值配置编辑器组件（DSL表达式编辑器）
     */
    createValueConfigEditor(parameterMeta, value, onChange) {
        const container = document.createElement('div');
        container.className = 'form-group';

        const label = document.createElement('label');
        label.textContent = parameterMeta.displayName || parameterMeta.name;
        label.className = 'form-label';

        const editorContainer = document.createElement('div');
        editorContainer.className = 'dsl-editor-container';

        container.appendChild(label);
        container.appendChild(editorContainer);

        const expectedType = this.getDefaultExpectedType(parameterMeta);

        // 异步初始化DSL编辑器
        this.initializeDslEditor(editorContainer, value, onChange, { expectedType });

        return container;
    }

    /**
     * 初始化DSL表达式编辑器
     */
    async initializeDslEditor(container, value, onChange, options = {}) {
        try {
            const dslEditor = new DslEditor();
            const handleChange = (expr) => {
                if (container && container.dataset) {
                    container.dataset.dslExpression = expr || "";
                }
                if (typeof onChange === 'function') {
                    onChange(expr);
                }
            };

            await dslEditor.render(container, value || '', handleChange, options);

            // 缓存实例，便于外部读取表达式或清理
            container._dslEditor = dslEditor;

            // 存储编辑器实例以便后续清理
            if (!this.dslEditors) {
                this.dslEditors = [];
            }
            this.dslEditors.push(dslEditor);

        } catch (error) {
            console.error('DynamicUIRenderer: Failed to initialize DSL editor:', error);
            container.innerHTML = `
                <div class="error-message">
                    DSL编辑器初始化失败: ${error.message}
                </div>
            `;
        }
    }

    getDefaultExpectedType(parameterMeta) {
        if (!parameterMeta) return null;
        return parameterMeta.typeName || parameterMeta.TypeName || null;
    }

    /**
     * 创建错误显示元素
     */
    createErrorElement(message) {
        const container = document.createElement('div');
        container.className = 'form-group error-group';

        const errorDiv = document.createElement('div');
        errorDiv.className = 'error-message';
        errorDiv.textContent = message;

        container.appendChild(errorDiv);
        return container;
    }

    /**
     * 注册自定义组件
     */
    registerComponent(type, renderFunction) {
        this.componentRegistry.set(type, renderFunction);
    }

    /**
     * 渲染效果参数表单
     */
    renderEffectParametersForm(effectType, parameters = {}, onChange = null) {
        if (!this.metadataManager) {
            console.error('DynamicUIRenderer: MetadataManager not initialized');
            return this.createErrorElement('元数据管理器未初始化');
        }

        // 获取效果元数据
        const effectMetadata = this.metadataManager.getEffectByType(effectType);
        if (!effectMetadata) {
            console.error(`DynamicUIRenderer: No metadata found for effect type "${effectType}"`);
            return this.createErrorElement(`未找到效果类型 "${effectType}" 的元数据`);
        }

        const container = document.createElement('div');
        container.className = 'effect-parameters-form';
        // 当前参数缓存，便于统一写回
        let currentParams = { ...parameters };

        // 渲染每个参数
        if (effectMetadata.parameters && effectMetadata.parameters.length > 0) {
            effectMetadata.parameters.forEach(paramMeta => {
                const paramValue = currentParams[paramMeta.propertyName];
                const paramElement = this.renderParameter(paramMeta, paramValue, (newValue) => {
                    currentParams = { ...currentParams, [paramMeta.propertyName]: newValue };
                    // 将最新参数缓存到容器，便于外部提取
                    container.dataset.effectParams = JSON.stringify(currentParams);
                    if (onChange) {
                        onChange(currentParams);
                    }
                });
                // 标记参数名，方便回退提取
                paramElement.dataset.paramName = paramMeta.propertyName;
                container.appendChild(paramElement);
            });
            // 初始化数据缓存
            container.dataset.effectParams = JSON.stringify(currentParams);
        } else {
            const noParamsMessage = document.createElement('div');
            noParamsMessage.className = 'no-parameters-message';
            noParamsMessage.textContent = '此效果无需配置参数';
            container.appendChild(noParamsMessage);
        }

        return container;
    }

    /**
     * 渲染DSL表达式编辑器
     */
    renderDslExpressionEditor(value = '', onChange = null) {
        const container = document.createElement('div');
        container.className = 'dsl-expression-editor';

        const label = document.createElement('label');
        label.textContent = 'DSL表达式';
        label.className = 'form-label';

        const editorContainer = document.createElement('div');
        editorContainer.className = 'dsl-editor-container';

        const textarea = document.createElement('textarea');
        textarea.className = 'dsl-textarea';
        textarea.value = value;
        textarea.placeholder = '输入DSL表达式，例如：player.health + 5';
        textarea.rows = 3;

        const helpText = document.createElement('div');
        helpText.className = 'dsl-help-text';
        helpText.innerHTML = `
            <small>
                可用的上下文对象：player, opponent, self, target<br>
                可用的函数：count(), sum(), filter() 等
            </small>
        `;

        if (onChange) {
            textarea.addEventListener('input', (e) => onChange(e.target.value));
        }

        editorContainer.appendChild(textarea);
        editorContainer.appendChild(helpText);

        container.appendChild(label);
        container.appendChild(editorContainer);

        return container;
    }

    /**
     * 加载卡牌选项
     */
    async loadCardOptions(cardList, onChange, selectedCard) {
        try {
            cardList.innerHTML = '<div class="loading-spinner"></div><p>正在加载卡牌...</p>';

            const cards = await loadCardDefinitions({
                useCache: false,
                force: true
            });
            this.renderCardOptions(cards, cardList, onChange, selectedCard);

        } catch (error) {
            console.error('DynamicUIRenderer: Failed to load cards:', error);
            cardList.innerHTML = `
                <div class="error-message">
                    加载卡牌失败: ${error.message}
                </div>
            `;
        }
    }

    /**
     * 渲染卡牌选项
     */
    renderCardOptions(cards, cardList, onChange, selectedCard) {
        if (!Array.isArray(cards) || cards.length === 0) {
            cardList.innerHTML = '<div class="no-cards-message">暂无可用卡牌</div>';
            return;
        }

        const cardsHTML = cards.map(card => `
            <div class="card-option" data-card-id="${card.id}" data-card-name="${card.name}">
                <div class="card-option-info">
                    <span class="card-option-name">${card.name}</span>
                    <span class="card-option-cost">${card.cost}费</span>
                </div>
                <div class="card-option-details">
                    <span class="card-option-type">${card.type}</span>
                    <span class="card-option-class">${card.cardClass}</span>
                </div>
            </div>
        `).join('');

        cardList.innerHTML = cardsHTML;

        // 绑定选择事件
        const cardOptions = cardList.querySelectorAll('.card-option');
        cardOptions.forEach(option => {
            option.addEventListener('click', () => {
                const cardId = option.dataset.cardId;
                const cardName = option.dataset.cardName;

                // 更新选中显示
                selectedCard.innerHTML = `
                    <span class="selected-card-name">${cardName}</span>
                    <button class="clear-selection-btn">×</button>
                `;

                // 绑定清除按钮
                const clearBtn = selectedCard.querySelector('.clear-selection-btn');
                if (clearBtn) {
                    clearBtn.addEventListener('click', (e) => {
                        e.stopPropagation();
                        selectedCard.innerHTML = '<span class="no-card-selected">未选择卡牌</span>';
                        if (onChange) onChange(null);
                    });
                }

                // 隐藏列表
                cardList.style.display = 'none';

                // 触发变更
                if (onChange) onChange(cardId);
            });
        });
    }

    /**
     * 搜索卡牌
     */
    searchCards(query, cardList) {
        const cardOptions = cardList.querySelectorAll('.card-option');
        const searchTerm = query.toLowerCase();

        cardOptions.forEach(option => {
            const cardName = option.dataset.cardName.toLowerCase();
            const isMatch = cardName.includes(searchTerm);
            option.style.display = isMatch ? 'block' : 'none';
        });
    }

    /**
     * 清理资源
     */
    cleanup() {
        // 清理DSL编辑器实例
        if (this.dslEditors) {
            this.dslEditors.forEach(editor => {
                try {
                    // DslEditor没有cleanup方法，只需要清空引用
                    if (editor.clear) {
                        editor.clear();
                    }
                } catch (error) {
                    console.error('DynamicUIRenderer: Error cleaning up DSL editor:', error);
                }
            });
            this.dslEditors = [];
        }

        this.metadataManager = null;
        this.componentRegistry.clear();
        this.isInitialized = false;
    }
}
