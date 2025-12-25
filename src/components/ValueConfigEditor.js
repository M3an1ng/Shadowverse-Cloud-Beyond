import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';
import { DslEditor } from './DslEditor.js';

/**
 * ValueConfig编辑器
 * 支持所有ValueConfig类型：Static、Formula、Dynamic、Variable
 */
export class ValueConfigEditor extends MetadataDrivenComponent {
    constructor() {
        super();
        this.valueConfig = null;
        this.onChange = null;
        this.container = null;
    }

    normalizeExpectedType(typeName) {
        if (!typeName) return null;
        const trimmed = String(typeName).trim();
        if (!trimmed) return null;
        const nullable = trimmed.match(/^Nullable<(.+)>$/);
        if (nullable) return nullable[1];
        return trimmed;
    }

    getStaticInputKindForType(typeName) {
        const t = this.normalizeExpectedType(typeName);
        if (!t) return null;
        if (['Int32', 'Int64', 'Single', 'Double', 'Decimal', 'Number'].includes(t)) return 'number';
        if (t === 'Boolean') return 'boolean';
        if (t === 'String') return 'string';
        return 'string';
    }

    /**
     * 渲染ValueConfig编辑器
     * @param {HTMLElement} container - 容器元素
     * @param {Object} valueConfig - ValueConfig对象
     * @param {Function} onChange - 变化回调
     * @param {Object} options - 选项
     */
    render(container, valueConfig = null, onChange = () => {}, options = {}) {
        this.container = container;
        this.valueConfig = this.normalizeValueConfig(valueConfig);
        this.onChange = onChange;
        this.options = options;
        this.expectedType = this.normalizeExpectedType(options.parameterType || options.expectedType);
        this.triggeringEventContext = options.triggeringEventContext || null;
        // 首次渲染时缓存当前配置，便于外部直接读取
        if (this.container) {
            try {
                this.container.dataset.valueConfig = JSON.stringify(this.valueConfig);
                this.container._valueConfig = this.valueConfig;
            } catch {
                /* ignore */
            }
        }

        console.log('ValueConfigEditor: Rendering with valueConfig:', {
            original: valueConfig,
            normalized: this.valueConfig
        });

        this.clearContainer(container);
        this.renderEditor();
    }

    /**
     * 创建默认的ValueConfig
     */
    createDefaultValueConfig() {
        return {
            $type: 'StaticValueConfig',
            Value: 0
        };
    }

    /**
     * 标准化ValueConfig格式
     * 处理不同的数据格式
     */
    normalizeValueConfig(valueConfig) {
        if (!valueConfig) {
            return this.createDefaultValueConfig();
        }

        // 如果是旧格式的{value: 1}，转换为StaticValueConfig
        if (valueConfig.value !== undefined && !valueConfig.$type) {
            return {
                $type: 'StaticValueConfig',
                Value: valueConfig.value
            };
        }

        // 如果已经是正确格式，直接返回
        if (valueConfig.$type) {
            return valueConfig;
        }

        // 其他情况，创建默认配置
        return this.createDefaultValueConfig();
    }

    /**
     * 渲染编辑器界面
     */
    renderEditor() {
        const editorContainer = document.createElement('div');
        editorContainer.className = 'value-config-editor';

        // 类型选择器
        const typeSelector = this.createTypeSelector();
        const typeGroup = this.createFormGroup('值类型', typeSelector);
        editorContainer.appendChild(typeGroup);

        // 值编辑器
        const valueEditor = this.createValueEditor();
        editorContainer.appendChild(valueEditor);

        this.container.appendChild(editorContainer);
    }

    /**
     * 创建类型选择器
     */
    createTypeSelector() {
        const options = [
            { value: 'StaticValueConfig', label: '静态值', description: '固定的数值或文本' },
            { value: 'FormulaValueConfig', label: '公式值', description: '基于DSL表达式计算的动态值' }
        ];

        const currentType = this.valueConfig.$type || 'StaticValueConfig';

        return this.createDropdown(
            options,
            currentType,
            (newType) => {
                this.changeValueType(newType);
            },
            '选择值类型'
        );
    }

    /**
     * 创建值编辑器
     */
    createValueEditor() {
        const container = document.createElement('div');
        container.className = 'value-editor-container';

        const currentType = this.valueConfig.$type || 'StaticValueConfig';

        if (currentType === 'StaticValueConfig') {
            container.appendChild(this.createStaticValueEditor());
        } else if (currentType === 'FormulaValueConfig') {
            container.appendChild(this.createFormulaValueEditor());
        }

        return container;
    }

    /**
     * 创建静态值编辑器
     */
    createStaticValueEditor() {
        const container = document.createElement('div');
        container.className = 'static-value-editor';

        const currentValue = this.valueConfig.Value;
        const inferredKind = this.getStaticInputKindForType(this.expectedType);
        const valueType = inferredKind || typeof currentValue;

        // 如果外部给了 expectedType，则固定静态输入类型，避免用户选择与参数真实类型冲突
        if (!inferredKind) {
            const typeOptions = [
                { value: 'number', label: '数字' },
                { value: 'string', label: '文本' },
                { value: 'boolean', label: '布尔值' }
            ];

            const typeSelector = this.createDropdown(
                typeOptions,
                valueType,
                (newType) => {
                    this.changeStaticValueType(newType);
                },
                '选择数据类型'
            );

            const typeGroup = this.createFormGroup('数据类型', typeSelector);
            container.appendChild(typeGroup);
        }

        // 值输入控件
        const valueInput = this.createStaticValueInput(currentValue, valueType);
        const valueGroup = this.createFormGroup('值', valueInput);
        container.appendChild(valueGroup);

        return container;
    }

    /**
     * 创建静态值输入控件
     */
    createStaticValueInput(value, valueType) {
        if (valueType === 'number') {
            return this.createNumberInput(value, (newValue) => {
                this.updateValueConfig({ Value: newValue });
            });
        } else if (valueType === 'boolean') {
            return this.createCheckbox(value, (newValue) => {
                this.updateValueConfig({ Value: newValue });
            });
        } else {
            return this.createTextInput(value, (newValue) => {
                this.updateValueConfig({ Value: newValue });
            });
        }
    }

    /**
     * 创建公式值编辑器
     */
    createFormulaValueEditor() {
        const container = document.createElement('div');
        container.className = 'formula-value-editor';

        const currentFormula = this.valueConfig.Formula || '';

        // 创建DSL编辑器容器
        const dslEditorContainer = document.createElement('div');
        dslEditorContainer.className = 'dsl-editor-wrapper ability-editor'; // 关键修复：添加父容器类名以应用样式

        // 实例化并渲染DSL编辑器
        const dslEditor = new DslEditor();
        dslEditor.render(
            dslEditorContainer,
            currentFormula,
            (newFormula) => {
                dslEditorContainer.dataset.dslExpression = newFormula || "";
                this.updateValueConfig({ Formula: newFormula });
            },
            {
                expectedType: this.expectedType || 'Int32',
                triggeringEventContext: this.triggeringEventContext
            }
        );
        dslEditorContainer._dslEditor = dslEditor;

        const formulaGroup = this.createFormGroup('DSL公式', dslEditorContainer);
        container.appendChild(formulaGroup);

        // 帮助文本
        const helpText = document.createElement('div');
        helpText.className = 'formula-help-text';
        helpText.innerHTML = `
            <small class="help-text">
                使用可视化编辑器构建表达式，或直接在上方输入。
            </small>
        `;
        container.appendChild(helpText);

        return container;
    }

    /**
     * 改变ValueConfig类型
     */
    changeValueType(newType) {
        let newValueConfig;

        if (newType === 'StaticValueConfig') {
            const kind = this.getStaticInputKindForType(this.expectedType);
            const defaultValue =
                kind === 'boolean' ? false :
                kind === 'string' ? '' :
                0;
            newValueConfig = {
                $type: 'StaticValueConfig',
                Value: defaultValue
            };
        } else if (newType === 'FormulaValueConfig') {
            newValueConfig = {
                $type: 'FormulaValueConfig',
                Formula: ''
            };
        }

        this.valueConfig = newValueConfig;
        this.notifyChange();
        this.renderEditor(); // 重新渲染编辑器
    }

    /**
     * 改变静态值类型
     */
    changeStaticValueType(newType) {
        let defaultValue;
        if (newType === 'number') {
            defaultValue = 0;
        } else if (newType === 'boolean') {
            defaultValue = false;
        } else {
            defaultValue = '';
        }

        this.updateValueConfig({ Value: defaultValue });
        this.renderEditor(); // 重新渲染编辑器
    }

    /**
     * 更新ValueConfig
     */
    updateValueConfig(updates) {
        this.valueConfig = { ...this.valueConfig, ...updates };
        this.notifyChange();
    }

    /**
     * 通知变化
     */
    notifyChange() {
        console.log('ValueConfigEditor: Value changed:', this.valueConfig);
        // 缓存到容器数据集/引用，方便外部直接读取
        if (this.container) {
            try {
                this.container.dataset.valueConfig = JSON.stringify(this.valueConfig);
                this.container._valueConfig = this.valueConfig;
            } catch (e) {
                console.warn('ValueConfigEditor: Failed to serialize valueConfig', e);
            }
        }
        if (this.onChange) {
            this.onChange(this.valueConfig);
        }
    }

    /**
     * 获取当前值（用于显示）
     */
    getCurrentDisplayValue() {
        if (!this.valueConfig) return '';

        if (this.valueConfig.$type === 'StaticValueConfig') {
            return String(this.valueConfig.Value || '');
        } else if (this.valueConfig.$type === 'FormulaValueConfig') {
            return this.valueConfig.Formula || '';
        }

        return '';
    }
}
