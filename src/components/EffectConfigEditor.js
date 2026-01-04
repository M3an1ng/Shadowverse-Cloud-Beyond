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
        this.effects = (effects || []).map(effect => this.normalizeEffectConfig(effect));
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

        const isEnabled = effect.isEnabled !== false;
        const statusClass = isEnabled ? 'status-enabled' : 'status-disabled';
        const statusText = isEnabled ? '启用' : '禁用';

        const headerInfo = document.createElement('div');
        headerInfo.className = 'header-info';
        headerInfo.innerHTML = `
            <span class="item-index">#${index + 1}</span>
            <span class="item-title">${titleText}</span>
            <span class="item-status ${statusClass}">${statusText}</span>
        `;

        const headerActions = document.createElement('div');
        headerActions.className = 'header-actions';

        const toggle = this.createToggleSwitch(
            isEnabled,
            (checked) => {
                this.updateEffect(index, { isEnabled: checked });
                const statusSpan = header.querySelector('.item-status');
                if (checked) {
                    statusSpan.textContent = '启用';
                    statusSpan.className = 'item-status status-enabled';
                    toggle.title = '点击禁用效果';
                } else {
                    statusSpan.textContent = '禁用';
                    statusSpan.className = 'item-status status-disabled';
                    toggle.title = '点击启用效果';
                }
            },
            '',
            { compact: true }
        );
        toggle.title = isEnabled ? '点击禁用效果' : '点击启用效果';

        const settingsBtn = document.createElement('button');
        settingsBtn.type = 'button';
        settingsBtn.className = 'btn-icon settings-btn';
        settingsBtn.title = '效果选项';
        settingsBtn.setAttribute('aria-label', '效果选项');

        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'btn-icon delete-btn';
        deleteBtn.title = '删除';
        deleteBtn.innerHTML = `
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="3 6 5 6 21 6"></polyline>
                <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                <line x1="10" y1="11" x2="10" y2="17"></line>
                <line x1="14" y1="11" x2="14" y2="17"></line>
            </svg>
        `;

        const expandBtn = document.createElement('button');
        expandBtn.className = 'btn-icon expand-btn';
        expandBtn.title = '展开/折叠';
        expandBtn.textContent = '▼';

        headerActions.appendChild(toggle);
        headerActions.appendChild(settingsBtn);
        headerActions.appendChild(deleteBtn);
        headerActions.appendChild(expandBtn);

        header.appendChild(headerInfo);
        header.appendChild(headerActions);

        // 绑定头部事件
        deleteBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.removeEffect(index);
        });

        // 内容区域
        const content = document.createElement('div');
        content.className = 'accordion-content collapsed';

        const optionsPanel = this.createEffectOptionsPanel(effect, index);
        content.appendChild(optionsPanel);

        // 1. 效果类型选择 (如果未选择类型，显示网格选择器；否则显示参数面板)
        if (!effect.type) {
            const selectionContainer = document.createElement('div');
            selectionContainer.className = 'effect-type-selection-container';
            
            this.renderEffectTypeSelection(selectionContainer, (newType) => {
                this.updateEffect(index, { type: newType, parameters: {} });
            });
            
            content.appendChild(selectionContainer);
        } else {
            // 已选择类型，显示参数面板
            // 添加一个"更改类型"的微小入口，或者依赖删除重加
            // 2. 动态参数面板
            if (effectMeta) {
                const paramsPanel = this.createParametersPanel(effectMeta, effect.parameters || {}, (newParams) => {
                    this.updateEffect(index, { parameters: newParams });
                });
                content.appendChild(paramsPanel);
            }
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

        settingsBtn.addEventListener('click', (e) => {
            e.stopPropagation();

            if (content.classList.contains('collapsed')) {
                content.classList.remove('collapsed');
                expandBtn.innerHTML = '▲';
                node.classList.add('expanded');
            }

            const isCollapsed = optionsPanel.classList.contains('collapsed');
            if (isCollapsed) {
                optionsPanel.classList.remove('collapsed');
                settingsBtn.classList.add('active');
            } else {
                optionsPanel.classList.add('collapsed');
                settingsBtn.classList.remove('active');
            }
        });

        return node;
    }

    renderEffectTypeSelection(container, onSelect) {
        const effects = this.metadata.effects || [];
        
        if (effects.length === 0) {
            container.innerHTML = '<div class="loading-spinner">加载效果元数据中...</div>';
            return;
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

        // 渲染分类网格
        orderedCategories.forEach(category => {
            const items = grouped.get(category.id);
            if (!items || items.length === 0) return;

            items.sort((a, b) => String(a.displayName || '').localeCompare(String(b.displayName || '')));

            const categorySection = document.createElement('div');
            categorySection.className = 'effect-category-section';
            
            const categoryTitle = document.createElement('h5');
            categoryTitle.className = 'effect-category-title';
            categoryTitle.textContent = categoryLabelMap.get(category.id) || category.label;
            categorySection.appendChild(categoryTitle);

            const grid = document.createElement('div');
            grid.className = 'effect-type-grid';

            items.forEach(effect => {
                const btn = document.createElement('button');
                btn.className = 'effect-type-btn';
                btn.innerHTML = `
                    <span class="effect-name">${effect.displayName}</span>
                    ${effect.description ? `<span class="effect-desc">${effect.description}</span>` : ''}
                `;
                btn.title = effect.description || effect.displayName;
                btn.addEventListener('click', () => onSelect(effect.effectTypeName));
                grid.appendChild(btn);
            });

            categorySection.appendChild(grid);
            container.appendChild(categorySection);
        });
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

            const isValueConfigEditor = controlType === 'valueConfigEditor';
            const groupLabel = isValueConfigEditor ? null : displayName;
            const group = this.createFormGroup(groupLabel, controlContainer, description);
            if (isValueConfigEditor) {
                group.classList.add('value-config-group');
            }
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

    createEffectOptionsPanel(effect, index) {
        const container = document.createElement('div');
        container.className = 'collapsible-content effect-options-panel collapsed';

        const title = document.createElement('h5');
        title.className = 'section-title';
        title.textContent = '效果选项';
        container.appendChild(title);

        const selectionSection = this.createTargetSelectorEditor(effect, index);
        container.appendChild(selectionSection);

        const failureSection = this.createFailureHandlingEditor(effect, index);
        container.appendChild(failureSection);

        return container;
    }

    createTargetSelectorEditor(effect, index) {
        const container = document.createElement('div');
        container.className = 'config-section';
        container.innerHTML = '<h5 class="section-title">目标选择</h5>';

        const availableKeys = this.getAvailableTargetKeys();
        const currentKeys = this.normalizeTargetSelector(effect?.targetSelector);
        const currentSet = new Set(currentKeys);

        const group = document.createElement('div');
        group.className = 'btn-group-row target-selector-group';

        const allBtn = this.createTargetSelectorButton('*', '全部');
        if (currentKeys.some(this.isSelectAllToken)) {
            allBtn.classList.add('active');
        }
        group.appendChild(allBtn);

        if (availableKeys.length > 0) {
            availableKeys.forEach((key) => {
                const btn = this.createTargetSelectorButton(key, key);
                if (currentSet.has(key)) {
                    btn.classList.add('active');
                }
                group.appendChild(btn);
            });
        }

        const updateSelection = () => {
            const activeButtons = Array.from(group.querySelectorAll('.btn-option.active'));
            const nextKeys = activeButtons.map(btn => btn.dataset.value).filter(Boolean);
            const finalKeys = nextKeys.some(this.isSelectAllToken) ? ['*'] : nextKeys;
            this.updateEffect(index, { targetSelector: finalKeys });
        };

        group.addEventListener('click', (event) => {
            const button = event.target.closest('.btn-option');
            if (!button) return;

            const value = button.dataset.value;
            const isActive = button.classList.contains('active');

            if (this.isSelectAllToken(value)) {
                group.querySelectorAll('.btn-option').forEach(btn => btn.classList.remove('active'));
                if (!isActive) {
                    button.classList.add('active');
                }
            } else {
                const allButton = group.querySelector('.btn-option[data-value="*"]');
                if (allButton) {
                    allButton.classList.remove('active');
                }
                button.classList.toggle('active');
            }

            updateSelection();
        });

        if (availableKeys.length === 0) {
            const hint = document.createElement('div');
            hint.className = 'text-muted';
            hint.textContent = '暂无可用目标池，请先在目标配置中添加目标。';
            container.appendChild(hint);
        }

        container.appendChild(this.createFormGroup('目标池', group, '点击按钮选择目标池，* 表示所有目标池'));
        return container;
    }

    createTargetSelectorButton(value, label) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn-option';
        btn.dataset.value = value;
        btn.textContent = label;
        return btn;
    }

    createFailureHandlingEditor(effect, index) {
        const container = document.createElement('div');
        container.className = 'config-section';
        container.innerHTML = '<h5 class="section-title">失败处理</h5>';

        const select = document.createElement('select');
        select.className = 'form-select';
        select.innerHTML = '<option value="">加载中...</option>';
        select.disabled = true;

        const current = this.normalizeFailureHandling(effect);
        this.populateEnumDropdown(select, 'FailureHandling', current).finally(() => {
            select.disabled = false;
        });

        select.addEventListener('change', (e) => {
            this.updateEffect(index, { onFailure: e.target.value });
        });

        container.appendChild(this.createFormGroup('失败策略', select, '效果失败时的处理方式'));
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
                const normalized = this.normalizeEnumValue(currentValue, data);
                if (normalized != null) {
                    select.value = normalized;
                }
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
        const { controlType, paramName, typeName, paramMeta, paramValue, parameters, onChange, displayName } = ctx;
        const controlKey = (controlType || '').toString().toLowerCase();
        const enumName = paramMeta.enumName || paramMeta.EnumName;
        const optionsProviderKey = paramMeta.optionsProviderKey || paramMeta.OptionsProviderKey;

        const updateParam = (newValue) => {
            const newParams = { ...parameters, [paramName]: newValue };
            onChange(newParams);
        };

        // Normalize: if paramValue is a ValueConfig but the control expects a scalar, try to unwrap StaticValueConfig.
        const unwrapped = this.unwrapStaticValueConfig(paramValue);
        const scalarValue = unwrapped.ok ? unwrapped.value : paramValue;

        switch (controlKey) {
            case 'valueconfigeditor': {
                const valueConfig = (paramValue && typeof paramValue === 'object' && paramValue.$type)
                    ? paramValue
                    : this.toStaticValueConfig(paramValue ?? paramMeta.defaultValue ?? 0);

                const valueEditor = new ValueConfigEditor();
                valueEditor.render(container, valueConfig, (newValue) => updateParam(newValue), {
                    parameterName: paramName,
                    parameterLabel: displayName || paramName,
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
            case 'numberstepper': {
                const input = this.createNumberInput(
                    scalarValue ?? paramMeta.defaultValue ?? 0,
                    (val) => updateParam(val),
                    { min: paramMeta.min, max: paramMeta.max }
                );
                input.className = 'form-input';
                container.appendChild(input);
                return;
            }
            case 'textarea': {
                const input = this.createTextInput(
                    scalarValue ?? paramMeta.defaultValue ?? '',
                    (val) => updateParam(val),
                    { multiline: true }
                );
                input.className = 'form-textarea';
                container.appendChild(input);
                return;
            }
            case 'textinput': {
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
            case 'targetselector': {
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

    normalizeEnumValue(value, options) {
        if (value == null) return null;
        const raw = String(value);
        const entries = options ? Object.keys(options) : [];
        if (entries.length === 0) return raw;

        const match = entries.find((key) => key.toLowerCase() === raw.toLowerCase());
        return match || raw;
    }

    normalizeEffectConfig(effect) {
        const normalized = {
            isEnabled: effect?.isEnabled !== false,
            ...(effect || {})
        };

        const rawSelector = effect?.targetSelector ?? effect?.TargetSelector ?? effect?.targetselector;
        normalized.targetSelector = this.normalizeTargetSelector(rawSelector);

        const rawFailure = effect?.onFailure ?? effect?.OnFailure ?? effect?.onfailure;
        if (rawFailure != null && rawFailure !== '') {
            normalized.onFailure = rawFailure;
        }

        return normalized;
    }

    normalizeTargetSelector(value) {
        if (!value) return [];
        let keys = [];
        if (Array.isArray(value)) {
            keys = value;
        } else if (typeof value === 'string') {
            keys = this.parseTargetSelectorInput(value);
        }

        return keys
            .filter(item => typeof item === 'string' && item.trim())
            .map(item => this.normalizeTargetSelectorKey(item))
            .filter(Boolean);
    }

    normalizeTargetSelectorKey(value) {
        if (!value) return '';
        const raw = String(value).trim();
        if (this.isSelectAllToken(raw)) {
            return '*';
        }
        const prefix = 'ResolvedTargets.';
        if (raw.toLowerCase().startsWith(prefix.toLowerCase())) {
            return raw.slice(prefix.length).trim();
        }
        return raw;
    }

    getAvailableTargetKeys() {
        const targets = this.options?.targets;
        if (!targets) return [];

        if (Array.isArray(targets)) {
            if (targets.length === 1) {
                return ['default'];
            }
            return targets.map((target, index) => {
                if (target && typeof target.key === 'string' && target.key.trim()) {
                    return target.key.trim();
                }
                return `target${index + 1}`;
            });
        }

        if (typeof targets === 'object') {
            return Object.keys(targets);
        }

        return [];
    }

    formatTargetSelector(value) {
        if (!value) return '';
        if (Array.isArray(value)) {
            return value.join(' | ');
        }
        if (typeof value === 'string') {
            return value;
        }
        return '';
    }

    parseTargetSelectorInput(raw) {
        if (!raw) return [];
        return raw
            .split(/[|,;]+/)
            .map(item => item.trim())
            .filter(Boolean);
    }

    isSelectAllToken(token) {
        if (!token) return false;
        const normalized = String(token).trim().toLowerCase();
        return normalized === '*'
            || normalized === 'all'
            || normalized === 'resolvedtargets'
            || normalized === 'resolvedtargets.*'
            || normalized === 'resolvedtargets.all';
    }

    normalizeFailureHandling(effect) {
        const raw = effect?.onFailure ?? effect?.OnFailure ?? effect?.onfailure;
        return raw ?? 'AbortAbility';
    }

    // ... 辅助方法 (addEffect, removeEffect, updateEffect, etc.) ...
    addEffect() {
        this.effects.push({ type: '', parameters: {}, isEnabled: true });
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
            if (effect.isEnabled === false) {
                return;
            }
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
        input.setAttribute('aria-label', labelText || '启用效果');
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
}
