// src/components/EffectConfigEditor.js - 效果配置编辑器 (Refactored)
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';
import { ValueConfigEditor } from './ValueConfigEditor.js';

/**
 * 效果配置编辑器
 * 采用手风琴式列表设计，支持多效果管理
 */
export class EffectConfigEditor extends MetadataDrivenComponent {
    constructor() {
        super();
        this.effects = [];
        this.onChange = null;
        this.container = null;
        this.options = {};
    }

    async render(container, effects = [], onChange = () => {}, options = {}) {
        await this.initialize();

        this.container = container;
        this.effects = effects;
        this.onChange = onChange;
        this.options = options || {};

        this.clearContainer(container);
        
        try {
            const wrapper = document.createElement('div');
            wrapper.className = 'step-content-wrapper effects-step';

            // 标题区域
            const header = document.createElement('div');
            header.className = 'step-header';
            header.innerHTML = `
                <h4>效果配置</h4>
                <p class="step-intro">定义能力触发后具体发生的事情。你可以组合多个效果来实现复杂的逻辑。</p>
            `;
            wrapper.appendChild(header);

            // 效果列表容器
            const listContainer = document.createElement('div');
            listContainer.className = 'effects-list';
            listContainer.id = 'effects-list-container';
            wrapper.appendChild(listContainer);

            // 渲染列表内容
            this.renderEffectsList(listContainer);

            // 添加按钮区域
            const actionsContainer = document.createElement('div');
            actionsContainer.className = 'add-effect-container';
            const addBtn = this.createButton('添加新效果', () => this.addEffect(), 'primary');
            actionsContainer.appendChild(addBtn);
            wrapper.appendChild(actionsContainer);

            container.appendChild(wrapper);
            
        } catch (error) {
            console.error('EffectConfigEditor: Render failed:', error);
            this.showError(`渲染效果配置失败: ${error.message}`, container);
        }
    }

    renderEffectsList(container) {
        this.clearContainer(container);

        if (this.effects.length === 0) {
            const placeholder = document.createElement('div');
            placeholder.className = 'empty-state';
            placeholder.innerHTML = `
                <div class="empty-state-visual">
                    <div class="target-ring-outer effect-ring">
                        <div class="target-ring-inner">
                            <div class="target-dot effect-dot"></div>
                        </div>
                    </div>
                </div>
                <h5 class="empty-title">暂无效果配置</h5>
                <p class="empty-desc">定义能力触发后具体发生的事情<br>点击下方按钮添加第一个效果</p>
            `;
            container.appendChild(placeholder);
            return;
        }

        this.effects.forEach((effect, index) => {
            const effectNode = this.createEffectNode(effect, index);
            container.appendChild(effectNode);
        });
    }

    createEffectNode(effect, index) {
        const node = document.createElement('div');
        node.className = 'effect-node accordion-item';
        node.dataset.effectIndex = index;

        const effectMeta = this.getEffectMetadata(effect.type);
        const titleText = effectMeta ? effectMeta.displayName : (effect.type || '未配置效果');

        // 头部
        const header = document.createElement('div');
        header.className = 'accordion-header';
        header.innerHTML = `
            <div class="header-info">
                <span class="item-index">#${index + 1}</span>
                <span class="item-title">${titleText}</span>
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
            this.removeEffect(index);
        });

        // 内容区域
        const content = document.createElement('div');
        content.className = 'accordion-content collapsed';
        
        // 1. 效果类型选择
        const typeSelector = this.createEffectTypeSelector(effect.type, (newType) => {
            this.updateEffect(index, { type: newType, parameters: {} });
        });
        const typeGroup = this.createFormGroup('效果类型', typeSelector, '选择要执行的具体操作');
        content.appendChild(typeGroup);

        // 2. 动态参数面板
        if (effectMeta && effect.type) {
            const paramsPanel = this.createParametersPanel(effectMeta, effect.parameters || {}, (newParams) => {
                this.updateEffect(index, { parameters: newParams });
            });
            content.appendChild(paramsPanel);
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

    createEffectTypeSelector(selectedType, onChange) {
        const effects = this.metadata.effects || [];
        const select = document.createElement('select');
        select.className = 'form-select';

        const placeholderOption = document.createElement('option');
        placeholderOption.value = '';
        placeholderOption.textContent = effects.length === 0 ? '加载中...' : '选择效果类型...';
        placeholderOption.disabled = true;
        if (!selectedType) {
            placeholderOption.selected = true;
        }
        select.appendChild(placeholderOption);

        if (effects.length === 0) {
            select.disabled = true;
            return select;
        }

        const orderedCategories = this.getAbilityEditorCategoryOrder();
        const orderedCategoryIds = new Set(orderedCategories.map(category => category.id));
        const categoryLabelMap = new Map(orderedCategories.map(category => [category.id, category.label]));

        const normalizeCategoryId = (rawId) => orderedCategoryIds.has(rawId) ? rawId : 'uncategorized';
        const grouped = new Map();

        effects.forEach(effect => {
            const rawCategoryId = effect.categoryId || effect.CategoryId;
            const categoryId = normalizeCategoryId(rawCategoryId);
            const categoryLabel = effect.categoryLabel || effect.CategoryLabel || categoryLabelMap.get(categoryId);

            if (categoryLabel) {
                categoryLabelMap.set(categoryId, categoryLabel);
            }

            if (!grouped.has(categoryId)) {
                grouped.set(categoryId, []);
            }
            grouped.get(categoryId).push(effect);
        });

        orderedCategories.forEach(category => {
            const items = grouped.get(category.id);
            if (!items || items.length === 0) {
                return;
            }

            items.sort((a, b) => String(a.displayName || '').localeCompare(String(b.displayName || '')));

            const optgroup = document.createElement('optgroup');
            optgroup.label = categoryLabelMap.get(category.id) || category.label;

            items.forEach(effect => {
                const optionElement = document.createElement('option');
                optionElement.value = effect.effectTypeName;
                optionElement.textContent = effect.displayName;
                if (effect.description) {
                    optionElement.title = effect.description;
                }
                if (effect.effectTypeName === selectedType) {
                    optionElement.selected = true;
                }
                optgroup.appendChild(optionElement);
            });

            select.appendChild(optgroup);
        });

        select.addEventListener('change', (e) => {
            onChange(e.target.value);
        });

        return select;
    }

    createParametersPanel(effectMeta, parameters, onChange) {
        const container = document.createElement('div');
        container.className = 'parameters-panel';
        
        if (!effectMeta.parameters || effectMeta.parameters.length === 0) {
            container.innerHTML = '<div class="text-muted">此效果无需额外参数</div>';
            return container;
        }

        container.innerHTML = '<h5 class="section-title">参数配置</h5>';
        const grid = document.createElement('div');
        grid.className = 'form-grid-layout';

        effectMeta.parameters.forEach(paramMeta => {
            const paramName = paramMeta.propertyName || paramMeta.PropertyName || paramMeta.name;
            const displayName = paramMeta.displayName || paramMeta.DisplayName;
            const description = paramMeta.description || paramMeta.Description;
            const controlType = (paramMeta.controlType || paramMeta.ControlType || '').toString();
            const typeName = paramMeta.typeName || paramMeta.TypeName || paramMeta.type || paramMeta.TypeName;

            // 智能匹配参数值 (保持原有逻辑)
            let paramValue = parameters[paramName];
            if (paramValue === undefined) {
                const lower = paramName.toLowerCase();
                paramValue = parameters[lower];
                if (paramValue === undefined) {
                    const camel = paramName.charAt(0).toLowerCase() + paramName.slice(1);
                    paramValue = parameters[camel];
                }
            }

            const controlContainer = document.createElement('div');
            controlContainer.className = 'parameter-control';

            this.renderParameterControl(controlContainer, {
                controlType,
                typeName,
                paramName,
                displayName,
                description,
                paramMeta,
                paramValue,
                parameters,
                onChange
            });

            const group = this.createFormGroup(displayName, controlContainer, description);
            // 如果是复杂参数，占满一行
            // ValueConfigEditor needs full width for better UX (especially the DSL editor)
            if (['CardFilter', 'EffectList'].includes(paramMeta.typeName) ||
                controlType === 'valueConfigEditor' ||
                controlType === 'textArea') {
                group.className += ' full-width';
            }
            grid.appendChild(group);
        });

        container.appendChild(grid);
        return container;
    }

    unwrapStaticValueConfig(value) {
        if (!value) return { ok: false, value: null };
        if (typeof value !== 'object') return { ok: false, value: null };
        if (value.$type === 'StaticValueConfig' && Object.prototype.hasOwnProperty.call(value, 'Value')) {
            return { ok: true, value: value.Value };
        }
        if (Object.prototype.hasOwnProperty.call(value, 'value')) {
            return { ok: true, value: value.value };
        }
        return { ok: false, value: null };
    }

    toStaticValueConfig(value) {
        return { $type: 'StaticValueConfig', Value: value };
    }

    async populateEnumDropdown(select, enumName, currentValue) {
        try {
            const res = await fetch(`/api/localization/enums/${encodeURIComponent(enumName)}`);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();

            select.innerHTML = '';
            Object.entries(data).forEach(([value, label]) => {
                const opt = document.createElement('option');
                opt.value = value;
                opt.textContent = label;
                select.appendChild(opt);
            });

            if (currentValue != null && String(currentValue) !== '') {
                select.value = String(currentValue);
            }
        } catch (e) {
            console.warn('EffectConfigEditor: failed to load enum options', enumName, e);
            select.disabled = true;
            select.innerHTML = '';
            const opt = document.createElement('option');
            opt.value = '';
            opt.textContent = '加载失败';
            select.appendChild(opt);
        }
    }

    async populateOptionsProviderDropdown(select, key, currentValue) {
        try {
            const res = await fetch(`/api/abilityEditorOptions/${encodeURIComponent(key)}`);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();

            select.innerHTML = '';
            const entries = Object.entries(data || {});
            if (entries.length === 0) {
                const opt = document.createElement('option');
                opt.value = '';
                opt.textContent = '无可用选项';
                select.appendChild(opt);
                return;
            }

            entries.forEach(([value, label]) => {
                const opt = document.createElement('option');
                opt.value = value;
                opt.textContent = label;
                select.appendChild(opt);
            });

            if (currentValue != null && String(currentValue) !== '') {
                select.value = String(currentValue);
            }
        } catch (e) {
            console.warn('EffectConfigEditor: failed to load option provider', key, e);
            select.disabled = true;
            select.innerHTML = '';
            const opt = document.createElement('option');
            opt.value = '';
            opt.textContent = '加载失败';
            select.appendChild(opt);
        }
    }

    renderParameterControl(container, ctx) {
        const { controlType, paramName, typeName, paramMeta, paramValue, parameters, onChange } = ctx;
        const enumName = paramMeta.enumName || paramMeta.EnumName;
        const optionsProviderKey = paramMeta.optionsProviderKey || paramMeta.OptionsProviderKey;

        const updateParam = (newValue) => {
            const newParams = { ...parameters, [paramName]: newValue };
            onChange(newParams);
        };

        // Normalize: if paramValue is a ValueConfig but the control expects a scalar, try to unwrap StaticValueConfig.
        const unwrapped = this.unwrapStaticValueConfig(paramValue);
        const scalarValue = unwrapped.ok ? unwrapped.value : paramValue;

        switch (controlType) {
            case 'valueConfigEditor': {
                const valueConfig = (paramValue && typeof paramValue === 'object' && paramValue.$type)
                    ? paramValue
                    : this.toStaticValueConfig(paramValue ?? paramMeta.defaultValue ?? 0);

                const valueEditor = new ValueConfigEditor();
                valueEditor.render(container, valueConfig, (newValue) => updateParam(newValue), {
                    parameterName: paramName,
                    parameterType: typeName,
                    triggeringEventContext: {
                        eventTypes: this.options?.trigger?.eventTypes || [],
                        timing: this.options?.trigger?.timing || 'On'
                    }
                });
                return;
            }
            case 'checkbox': {
                const input = document.createElement('input');
                input.type = 'checkbox';
                input.checked = !!(scalarValue ?? paramMeta.defaultValue);
                input.addEventListener('change', (e) => updateParam(!!e.target.checked));
                container.appendChild(input);
                return;
            }
            case 'numberStepper': {
                const input = this.createNumberInput(
                    scalarValue ?? paramMeta.defaultValue ?? 0,
                    (val) => updateParam(val),
                    { min: paramMeta.min, max: paramMeta.max }
                );
                input.className = 'form-input';
                container.appendChild(input);
                return;
            }
            case 'textArea': {
                const input = this.createTextInput(
                    scalarValue ?? paramMeta.defaultValue ?? '',
                    (val) => updateParam(val),
                    { multiline: true }
                );
                input.className = 'form-textarea';
                container.appendChild(input);
                return;
            }
            case 'textInput': {
                const input = this.createTextInput(
                    scalarValue ?? paramMeta.defaultValue ?? '',
                    (val) => updateParam(val),
                    { multiline: false }
                );
                input.className = 'form-input';
                container.appendChild(input);
                return;
            }
            case 'dropdown': {
                const select = document.createElement('select');
                select.className = 'form-select';
                select.innerHTML = '<option value=\"\">加载中...</option>';
                select.disabled = true;

                const current = scalarValue ?? paramMeta.defaultValue ?? '';

                if (enumName) {
                    this.populateEnumDropdown(select, enumName, current).finally(() => {
                        select.disabled = false;
                    });
                } else if (optionsProviderKey) {
                    this.populateOptionsProviderDropdown(select, optionsProviderKey, current).finally(() => {
                        select.disabled = false;
                    });
                } else {
                    select.innerHTML = '<option value=\"\">缺少选项源</option>';
                    select.disabled = true;
                }

                select.addEventListener('change', (e) => updateParam(e.target.value));
                container.appendChild(select);
                return;
            }
            case 'targetSelector': {
                const input = this.createTextInput(
                    scalarValue ?? paramMeta.defaultValue ?? '',
                    (val) => updateParam(val),
                    { multiline: false, placeholder: 'TODO: TargetSelector' }
                );
                input.className = 'form-input';
                container.appendChild(input);
                return;
            }
            default: {
                const fallback = document.createElement('div');
                fallback.className = 'text-muted';
                fallback.textContent = `未支持的控件类型: ${controlType || '(空)'}，请使用 ValueConfigEditor 或调整元数据。`;
                container.appendChild(fallback);
                return;
            }
        }
    }

    // ... 辅助方法 (addEffect, removeEffect, updateEffect, etc.) ...
    addEffect() {
        this.effects.push({ type: '', parameters: {} });
        this.renderEffectsList(this.container.querySelector('#effects-list-container'));
        this.notifyChange();
    }

    removeEffect(index) {
        if (confirm('确定删除此效果？')) {
            this.effects.splice(index, 1);
            this.renderEffectsList(this.container.querySelector('#effects-list-container'));
            this.notifyChange();
        }
    }

    updateEffect(index, updates) {
        this.effects[index] = { ...this.effects[index], ...updates };
        // 如果更新了类型，需要重新渲染整个节点以更新标题和参数面板
        if (updates.type) {
            this.updateEffectNode(index);
        }
        this.notifyChange();
    }

    updateEffectNode(index) {
        const listContainer = this.container.querySelector('#effects-list-container');
        if (!listContainer) return;
        const existingNode = listContainer.querySelector(`[data-effect-index="${index}"]`);
        if (!existingNode) return;

        const isExpanded = existingNode.classList.contains('expanded');
        const newNode = this.createEffectNode(this.effects[index], index);

        if (isExpanded) {
            newNode.classList.add('expanded');
            newNode.querySelector('.accordion-content').classList.remove('collapsed');
            newNode.querySelector('.expand-btn').innerHTML = '▲';
        }

        existingNode.replaceWith(newNode);
    }

    getEffectMetadata(type) {
        return (this.metadata.effects || []).find(e => e.effectTypeName === type);
    }

    notifyChange() {
        if (this.onChange) this.onChange(this.effects);
    }

    validateEffects() {
        const errors = [];
        this.effects.forEach((effect, index) => {
            if (!effect.type) {
                errors.push(`效果 #${index + 1}: 必须选择效果类型`);
            } else {
                const meta = this.getEffectMetadata(effect.type);
                if (meta && meta.parameters) {
                    meta.parameters.forEach(p => {
                        if (!p.isOptional && !effect.parameters[p.name]) {
                            // 简单检查，实际可能需要更深入检查ValueConfig
                            // errors.push(`效果 #${index + 1}: 缺少参数 "${p.displayName}"`);
                        }
                    });
                }
            }
        });
        return errors;
    }
}
