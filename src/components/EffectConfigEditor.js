// src/components/EffectConfigEditor.js - 效果配置编辑器 (Refactored)
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';
import { ValueConfigEditor } from './ValueConfigEditor.js';
import { DslEditor } from './DslEditor.js';
import { getBlockingLoader } from './blockingLoader.js';
import { ensureAbilityData, ensureCardDefinitions, getAbilityDataSnapshot } from '../abilityDataCache.js';
import { ensureEnumLocalization } from '../enumLocalizationCache.js';

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
        this.activeChainedModal = null;
        this.activeGrantAbilityEditorModal = null;
        this.activeGrantAbilityCopyModal = null;
        this.activeAbilityDefinitionPickerModal = null;
        this.activeCardDefinitionPickerModal = null;
        this.parameterModeCache = new Map();
        this.cardDefinitionCache = null;
        this.cardDefinitionIndex = new Map();
        this.cardDefinitionPromise = null;
        this.abilityDefinitionCache = null;
        this.abilityDefinitionIndex = new Map();
        this.abilityDefinitionPromise = null;
    }

    async render(container, effects = [], onChange = () => {}, options = {}) {
        await this.initialize();
        
        this.container = container;
        this.effects = (effects || []).map(effect => this.normalizeEffectConfig(effect));
        this.onChange = onChange;
        this.options = options || {};
        this.closeChainedAbilityModal();

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
            // 首次渲染后立即触发一次变更回调，确保旧数据被清洗为标准格式
            this.notifyChange();

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

        const chainedBtn = document.createElement('button');
        chainedBtn.type = 'button';
        chainedBtn.className = 'btn-icon chained-btn';
        chainedBtn.title = '连锁能力';
        chainedBtn.setAttribute('aria-label', '连锁能力');

        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'btn-icon delete-btn';
        deleteBtn.title = '删除';
        deleteBtn.setAttribute('aria-label', '删除');

        const expandBtn = document.createElement('button');
        expandBtn.className = 'btn-icon expand-btn';
        expandBtn.title = '展开/折叠';
        expandBtn.textContent = '▼';

        headerActions.appendChild(toggle);
        headerActions.appendChild(settingsBtn);
        headerActions.appendChild(chainedBtn);
        headerActions.appendChild(deleteBtn);
        headerActions.appendChild(expandBtn);

        header.appendChild(headerInfo);
        header.appendChild(headerActions);

        // 绑定头部事件
        deleteBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.removeEffect(index);
        });

        chainedBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.openChainedAbilityModal(index);
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
                const paramsPanel = this.createParametersPanel(
                    effectMeta,
                    effect.parameters || {},
                    (newParams) => {
                        this.updateEffect(index, { parameters: newParams });
                    },
                    { effectIndex: index }
                );
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

    createParametersPanel(effectMeta, parameters, onChange, options = {}) {
        const container = document.createElement('div');
        container.className = 'parameters-panel';
        const effectIndex = Number.isInteger(options.effectIndex) ? options.effectIndex : null;
        const getLatestParameters = typeof options.getLatestParameters === 'function'
            ? options.getLatestParameters
            : (() => {
                if (effectIndex == null) return parameters || {};
                const latest = this.effects?.[effectIndex]?.parameters;
                return latest && typeof latest === 'object' ? latest : {};
            });
        
        if (!effectMeta.parameters || effectMeta.parameters.length === 0) {
            container.innerHTML = '<div class="text-muted">此效果无需额外参数</div>';
            return container;
        }

        container.innerHTML = '';
        const grid = document.createElement('div');
        grid.className = 'form-grid-layout';

        const hasSearchMode = this.hasEffectParameter(effectMeta, 'UseSearchMode');
        const hasCardSourceMode = this.hasEffectParameter(effectMeta, 'CardSourceMode');
        const hasRemoveMode = this.hasEffectParameter(effectMeta, 'RemoveMode');
        const useSearchMode = hasSearchMode
            ? this.normalizeBooleanValue(this.getEffectParameterValue(parameters, effectMeta, 'UseSearchMode'))
            : false;
        const cardSourceMode = hasCardSourceMode
            ? this.normalizeCardSourceMode(this.getEffectParameterValue(parameters, effectMeta, 'CardSourceMode'))
            : null;
        const removeMode = hasRemoveMode
            ? this.normalizeRemoveAbilityMode(this.getEffectParameterValue(parameters, effectMeta, 'RemoveMode'))
            : null;
        const renderContext = {
            hasSearchMode,
            useSearchMode,
            hasCardSourceMode,
            cardSourceMode,
            hasRemoveMode,
            removeMode
        };

        effectMeta.parameters.forEach(paramMeta => {
            const paramName = this.getEffectParameterName(paramMeta);
            if (!paramName) {
                return;
            }
            const displayName = paramMeta.displayName || paramMeta.DisplayName;
            const description = paramMeta.description || paramMeta.Description;
            const controlType = (paramMeta.controlType || paramMeta.ControlType || '').toString();
            const controlKey = controlType.toLowerCase();
            const typeName = paramMeta.typeName || paramMeta.TypeName || paramMeta.type || paramMeta.TypeName;
            const fallbackInfo = this.resolveParameterDefault(paramMeta);
            const fallbackPlaceholder = this.formatDefaultPlaceholder(fallbackInfo.display);
            const dslSupport = this.resolveDslSupport(paramMeta, typeName);

            if (!this.shouldRenderEffectParameter(paramMeta, renderContext)) {
                return;
            }

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

            const updateParam = (newValue) => {
                const latest = typeof getLatestParameters === 'function' ? getLatestParameters() : parameters;
                const base = latest && typeof latest === 'object' ? latest : {};
                const newParams = { ...base };
                const targetKey = String(paramName || '').toLowerCase();
                Object.keys(newParams).forEach((key) => {
                    if (String(key).toLowerCase() === targetKey && key !== paramName) {
                        delete newParams[key];
                    }
                });
                if (newValue === undefined) {
                    delete newParams[paramName];
                } else {
                    newParams[paramName] = newValue;
                }
                onChange(newParams);
            };

            const inputMode = this.getParameterInputMode({
                paramValue,
                dslSupport
            });

            this.cacheParameterModeState(effectIndex, paramName, paramValue, fallbackInfo.saveValue);

            this.renderParameterControl(controlContainer, {
                controlType,
                typeName,
                paramName,
                displayName,
                description,
                paramMeta,
                paramValue,
                parameters,
                getLatestParameters,
                onChange,
                updateParam,
                effectMeta,
                inputMode,
                dslSupport,
                fallbackInfo,
                fallbackPlaceholder,
                effectIndex
            });

            const isValueConfigEditor = controlKey === 'valueconfigeditor';
            const headerLabel = !isValueConfigEditor
                ? this.createParameterHeader({
                    title: displayName || paramName,
                    controlKey,
                    allowDsl: dslSupport.allowDsl,
                    dslMode: dslSupport.dslMode,
                    isDslMode: inputMode === 'dsl',
                    onModeChange: (mode) => {
                        this.switchParameterInputMode({
                            effectIndex,
                            paramName,
                            mode,
                            paramValue,
                            fallbackValue: fallbackInfo.saveValue,
                            updateParam
                        });
                    }
                })
                : null;

            const groupLabel = headerLabel || (isValueConfigEditor ? null : displayName);
            const group = this.createFormGroup(groupLabel, controlContainer, description);
            if (isValueConfigEditor) {
                group.classList.add('value-config-group');
            }
            // 如果是复杂参数，占满一行
            // ValueConfigEditor needs full width for better UX (especially the DSL editor)
            if (['CardFilter', 'EffectList'].includes(paramMeta.typeName) ||
                controlKey === 'valueconfigeditor' ||
                controlKey === 'textarea' ||
                controlKey === 'abilitylisteditor' ||
                inputMode === 'dsl') {
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

        const title = document.createElement('h4');
        title.className = 'effect-options-title';
        title.textContent = '效果选项';
        container.appendChild(title);

        const selectionSection = this.createTargetSelectorEditor(effect, index);
        container.appendChild(selectionSection);

        const failureSection = this.createFailureHandlingEditor(effect, index);
        container.appendChild(failureSection);

        const repeatSection = this.createRepeatCountEditor(effect, index);
        container.appendChild(repeatSection);

        const repeatConditionSection = this.createRepeatConditionEditor(effect, index);
        container.appendChild(repeatConditionSection);

        return container;
    }

    createTargetSelectorEditor(effect, index) {
        const container = document.createElement('div');
        container.className = 'config-section';
        container.innerHTML = '<h5 class="section-title">目标选择</h5>';

        const availableKeys = this.getAvailableTargetKeys();
        const currentKeys = this.normalizeTargetSelector(effect?.targetSelector);
        const currentSet = new Set(currentKeys);
        const hasAllToken = currentKeys.some(this.isSelectAllToken);

        const group = document.createElement('div');
        group.className = 'btn-group-row target-selector-group';

        if (availableKeys.length > 0) {
            availableKeys.forEach((key) => {
                const label = this.formatTargetSelectorLabel(key);
                const btn = this.createTargetSelectorButton(key, label);
                if (hasAllToken || currentSet.has(key)) {
                    btn.classList.add('active');
                }
                group.appendChild(btn);
            });
        }

        const updateSelection = () => {
            const activeButtons = Array.from(group.querySelectorAll('.btn-option.active'));
            const nextKeys = activeButtons.map(btn => btn.dataset.value).filter(Boolean);
            this.updateEffect(index, { targetSelector: nextKeys });
        };

        group.addEventListener('click', (event) => {
            const button = event.target.closest('.btn-option');
            if (!button) return;

            button.classList.toggle('active');

            updateSelection();
        });

        if (availableKeys.length === 0) {
            const hint = document.createElement('div');
            hint.className = 'text-muted';
            hint.textContent = '暂无可用目标池，请先在目标配置中添加目标。';
            container.appendChild(hint);
        }

        container.appendChild(this.createFormGroup(null, group));
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

    formatTargetSelectorLabel(key) {
        if (!key) return '';
        const raw = String(key).trim();
        const match = /^target(\d+)$/i.exec(raw);
        if (match) {
            return `目标#${match[1]}`;
        }
        return raw;
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

        container.appendChild(this.createFormGroup(null, select));
        return container;
    }

    createRepeatCountEditor(effect, index) {
        const container = document.createElement('div');
        container.className = 'config-section';
        container.innerHTML = '<h5 class="section-title">重复执行</h5>';

        const current = this.normalizeRepeatCount(effect);
        const input = document.createElement('input');
        input.type = 'number';
        input.className = 'form-input';
        input.min = '0';
        input.step = '1';
        input.value = String(current);

        input.addEventListener('change', (e) => {
            const rawValue = e.target.value;
            const parsed = rawValue === '' ? 0 : Number(rawValue);
            const safeValue = Number.isNaN(parsed) ? 0 : Math.max(0, Math.floor(parsed));
            input.value = String(safeValue);
            this.updateEffect(index, { repeatCount: safeValue });
        });

        container.appendChild(this.createFormGroup(null, input));
        return container;
    }

    createRepeatConditionEditor(effect, index) {
        const container = document.createElement('div');
        container.className = 'config-section';

        const header = document.createElement('div');
        header.className = 'repeat-condition-header';

        const title = document.createElement('h5');
        title.className = 'section-title';
        title.textContent = '重复条件';
        header.appendChild(title);

        const current = this.normalizeRepeatCondition(effect?.repeatCondition ?? effect?.RepeatCondition ?? effect?.repeatcondition);
        const currentExpression = current?.expression ?? '';
        const hasCondition = !!current;

        const editorSection = document.createElement('div');
        editorSection.className = 'repeat-condition-editor';
        if (!hasCondition) {
            editorSection.style.display = 'none';
        }

        const toggle = this.createToggleSwitch(
            hasCondition,
            (checked) => {
                if (!checked) {
                    editorSection.style.display = 'none';
                    this.updateEffect(index, { repeatCondition: null });
                    return;
                }
                editorSection.style.display = '';
                const latest = this.normalizeRepeatCondition(this.effects?.[index]?.repeatCondition ?? current);
                if (!latest) {
                    this.updateEffect(index, {
                        repeatCondition: {
                            id: this.generateRepeatConditionId(),
                            isEnabled: true,
                            expression: ''
                        }
                    });
                }
            },
            '启用重复条件'
        );
        const toggleInput = toggle.querySelector('input');

        const toggleWrap = document.createElement('div');
        toggleWrap.className = 'repeat-condition-toggle';
        toggleWrap.appendChild(toggle);
        header.appendChild(toggleWrap);
        container.appendChild(header);

        const editorWrapper = document.createElement('div');
        const dslHost = document.createElement('div');
        dslHost.className = 'dsl-editor-wrapper';
        editorWrapper.appendChild(dslHost);

        const dslEditor = new DslEditor();
        dslEditor.render(
            dslHost,
            currentExpression,
            (expression) => {
                const trimmed = (expression || '').trim();
                if (!trimmed) {
                    if (toggleInput) {
                        toggleInput.checked = false;
                    }
                    editorSection.style.display = 'none';
                    this.updateEffect(index, { repeatCondition: null });
                    return;
                }
                const latest = this.normalizeRepeatCondition(this.effects?.[index]?.repeatCondition ?? current);
                const base = latest ?? { id: this.generateRepeatConditionId(), isEnabled: true };
                this.updateEffect(index, {
                    repeatCondition: {
                        ...base,
                        expression: expression,
                        isEnabled: true
                    }
                });
            },
            {
                expectedType: 'Boolean',
                triggeringEventContext: {
                    eventTypes: this.options?.trigger?.eventTypes || [],
                    timing: this.options?.trigger?.timing || 'On'
                }
            }
        ).catch(error => {
            console.error('EffectConfigEditor: failed to render repeat condition DSL editor', error);
            dslHost.innerHTML = `<div class="error-message">DSL编辑器加载失败: ${error.message}</div>`;
        });

        editorSection.appendChild(this.createFormGroup(
            '条件表达式',
            editorWrapper,
            '仅当条件为 true 时继续下一次重复；为空默认视为满足。'
        ));
        container.appendChild(editorSection);
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
        if (Object.prototype.hasOwnProperty.call(value, 'Value')) {
            return { ok: true, value: value.Value };
        }
        return { ok: false, value: null };
    }

    toStaticValueConfig(value) {
        return { $type: 'StaticValueConfig', Value: value };
    }

    async populateEnumDropdown(select, enumName, currentValue, filterFn) {
        try {
            const { raw } = await ensureEnumLocalization(enumName);
            const data = raw || {};
            if (Object.keys(data).length === 0) {
                throw new Error('Empty enum localization');
            }

            select.innerHTML = '';
            const entries = Object.entries(data).filter(([value, label]) => {
                return typeof filterFn === 'function' ? filterFn(value, label) : true;
            });

            const filtered = Object.fromEntries(entries);

            entries.forEach(([value, label]) => {
                const opt = document.createElement('option');
                opt.value = value;
                opt.textContent = label;
                select.appendChild(opt);
            });

            if (currentValue != null && String(currentValue) !== '') {
                const normalized = this.normalizeEnumValue(currentValue, filtered);
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
        const {
            controlType,
            paramName,
            typeName,
            paramMeta,
            paramValue,
            parameters,
            getLatestParameters,
            onChange,
            updateParam,
            displayName,
            effectMeta,
            inputMode,
            dslSupport,
            fallbackInfo,
            fallbackPlaceholder,
            effectIndex
        } = ctx;
        const controlKey = (controlType || '').toString().toLowerCase();
        const enumName = paramMeta.enumName || paramMeta.EnumName;
        const optionsProviderKey = paramMeta.optionsProviderKey || paramMeta.OptionsProviderKey;
        const effectTypeName = this.getEffectTypeName(effectMeta);
        const resolvedFallback = fallbackInfo || this.resolveParameterDefault(paramMeta);
        const fallbackDisplay = resolvedFallback.display;
        const fallbackSave = resolvedFallback.saveValue;
        const defaultPlaceholder = fallbackPlaceholder ?? this.formatDefaultPlaceholder(fallbackDisplay);
        const hasExplicitValue = paramValue !== undefined && paramValue !== null && paramValue !== '';
        const setParam = typeof updateParam === 'function'
            ? updateParam
            : (newValue) => {
                const latest = typeof getLatestParameters === 'function' ? getLatestParameters() : parameters;
                const base = latest && typeof latest === 'object' ? latest : {};
                const newParams = { ...base };
                const targetKey = String(paramName || '').toLowerCase();
                Object.keys(newParams).forEach((key) => {
                    if (String(key).toLowerCase() === targetKey && key !== paramName) {
                        delete newParams[key];
                    }
                });
                if (newValue === undefined) {
                    delete newParams[paramName];
                } else {
                    newParams[paramName] = newValue;
                }
                onChange(newParams);
            };

        // Normalize: if paramValue is a ValueConfig but the control expects a scalar, try to unwrap StaticValueConfig.
        const unwrapped = this.unwrapStaticValueConfig(paramValue);
        const scalarValue = unwrapped.ok ? unwrapped.value : paramValue;
        const resolvedDslType = dslSupport?.dslType || typeName;

        if (inputMode === 'dsl' && dslSupport?.allowDsl) {
            const formula = this.getDslExpressionFromValueConfig(paramValue) ??
                this.getCachedDslExpression(effectIndex, paramName) ??
                '';
            this.renderDslExpressionControl(container, {
                expression: formula,
                expectedType: resolvedDslType,
                onChange: (newFormula) => {
                    this.cacheDslExpression(effectIndex, paramName, newFormula);
                    setParam({
                        $type: 'FormulaValueConfig',
                        Formula: newFormula ?? ''
                    });
                },
                triggeringEventContext: {
                    eventTypes: this.options?.trigger?.eventTypes || [],
                    timing: this.options?.trigger?.timing || 'On'
                }
            });
            return;
        }

        if (controlKey === 'abilitydefinitionpicker') {
            this.renderAbilityDefinitionIdEditor(container, {
                values: scalarValue,
                onChange: (next) => {
                    this.cacheStructuredValue(effectIndex, paramName, next);
                    if (!next || next.length === 0) {
                        setParam(undefined);
                        return;
                    }
                    setParam(next);
                }
            });
            return;
        }

        if (controlKey === 'carddefinitionpicker') {
            const allowMultiple = this.isIntListType(typeName) || Array.isArray(scalarValue);
            this.renderCardDefinitionIdEditor(container, {
                values: scalarValue,
                allowMultiple,
                onChange: (next) => {
                    this.cacheStructuredValue(effectIndex, paramName, next);
                    if (allowMultiple) {
                        if (!next || next.length === 0) {
                            setParam(undefined);
                            return;
                        }
                        setParam(next);
                        return;
                    }
                    if (!Number.isFinite(next)) {
                        setParam(undefined);
                        return;
                    }
                    setParam(next);
                }
            });
            return;
        }

        if ((controlKey === 'textinput' || controlKey === 'textarea') && this.isIntListType(typeName)) {
            this.renderIntListEditor(container, {
                values: scalarValue,
                onChange: (next) => {
                    this.cacheStructuredValue(effectIndex, paramName, next);
                    if (!next || next.length === 0) {
                        setParam(undefined);
                        return;
                    }
                    setParam(next);
                }
            });
            return;
        }

        switch (controlKey) {
            case 'valueconfigeditor': {
                const valueConfig = paramValue !== undefined ? paramValue : null;
                const isEmptyValueConfig = this.isValueConfigEmpty(valueConfig);
                const effectiveValueConfig = isEmptyValueConfig ? null : valueConfig;
                const shouldUsePlaceholder = isEmptyValueConfig && defaultPlaceholder !== undefined;
                const valueEditor = new ValueConfigEditor();
                valueEditor.render(container, effectiveValueConfig, (newValue) => {
                    if (this.isValueConfigEmpty(newValue)) {
                        setParam(undefined);
                        return;
                    }
                    setParam(newValue);
                }, {
                    parameterName: paramName,
                    parameterLabel: displayName || paramName,
                    parameterType: typeName,
                    usePlaceholder: shouldUsePlaceholder,
                    placeholderValue: defaultPlaceholder,
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
                input.checked = !!(scalarValue ?? fallbackDisplay);
                input.addEventListener('change', (e) => {
                    const next = !!e.target.checked;
                    this.cacheStructuredValue(effectIndex, paramName, next);
                    setParam(next);
                });
                container.appendChild(input);
                return;
            }
            case 'numberstepper': {
                const input = this.createNumberInput(
                    hasExplicitValue ? scalarValue : '',
                    (val) => {
                        this.cacheStructuredValue(effectIndex, paramName, val);
                        setParam(val);
                    },
                    {
                        min: paramMeta.min,
                        max: paramMeta.max,
                        placeholder: !hasExplicitValue && defaultPlaceholder !== undefined ? defaultPlaceholder : undefined
                    }
                );
                input.className = 'form-input';
                container.appendChild(input);
                return;
            }
            case 'abilitylisteditor': {
                this.renderAbilityListEditor(container, paramValue, (val) => {
                    this.cacheStructuredValue(effectIndex, paramName, val);
                    setParam(val);
                }, {
                    effectTypeName,
                    paramName
                });
                return;
            }
            case 'textarea': {
                const expectsIntList = this.isIntListType(typeName);
                const displayValue = Array.isArray(scalarValue) ? scalarValue.join(',') : scalarValue;
                const input = this.createTextInput(
                    hasExplicitValue ? displayValue : '',
                    (val) => {
                        if (expectsIntList) {
                            const parsed = this.parseDelimitedIntList(val);
                            if (parsed.length === 0) {
                                this.cacheStructuredValue(effectIndex, paramName, parsed);
                                setParam(undefined);
                                return;
                            }
                            this.cacheStructuredValue(effectIndex, paramName, parsed);
                            setParam(parsed);
                            return;
                        }
                        this.cacheStructuredValue(effectIndex, paramName, val);
                        setParam(val);
                    },
                    {
                        multiline: true,
                        placeholder: !hasExplicitValue && defaultPlaceholder !== undefined ? defaultPlaceholder : undefined
                    }
                );
                input.className = 'form-textarea';
                container.appendChild(input);
                return;
            }
            case 'textinput': {
                const expectsIntList = this.isIntListType(typeName);
                const displayValue = Array.isArray(scalarValue) ? scalarValue.join(',') : scalarValue;
                const input = this.createTextInput(
                    hasExplicitValue ? displayValue : '',
                    (val) => {
                        if (expectsIntList) {
                            const parsed = this.parseDelimitedIntList(val);
                            if (parsed.length === 0) {
                                this.cacheStructuredValue(effectIndex, paramName, parsed);
                                setParam(undefined);
                                return;
                            }
                            this.cacheStructuredValue(effectIndex, paramName, parsed);
                            setParam(parsed);
                            return;
                        }
                        this.cacheStructuredValue(effectIndex, paramName, val);
                        setParam(val);
                    },
                    {
                        multiline: false,
                        placeholder: !hasExplicitValue && defaultPlaceholder !== undefined ? defaultPlaceholder : undefined
                    }
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

                const current = scalarValue ?? fallbackDisplay ?? '';
                const restrictCardSourceMode = enumName === 'CardSourceMode'
                    && this.isCardSourceModeRestrictedEffect(effectTypeName);

                if (enumName) {
                    this.populateEnumDropdown(
                        select,
                        enumName,
                        current,
                        restrictCardSourceMode ? (value) => value !== 'Instance' : null
                    ).finally(() => {
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

                select.addEventListener('change', (e) => {
                    const next = e.target.value;
                    this.cacheStructuredValue(effectIndex, paramName, next);
                    setParam(next);
                });
                container.appendChild(select);
                return;
            }
            case 'targetselector': {
                const input = this.createTextInput(
                    hasExplicitValue ? scalarValue : '',
                    (val) => {
                        this.cacheStructuredValue(effectIndex, paramName, val);
                        setParam(val);
                    },
                    {
                        multiline: false,
                        placeholder: !hasExplicitValue && defaultPlaceholder !== undefined
                            ? defaultPlaceholder
                            : 'TODO: TargetSelector'
                    }
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

        async loadCardDefinitions() {
            const snapshot = getAbilityDataSnapshot();
            if (snapshot.cards?.length) {
                this.cardDefinitionCache = this.normalizeCardDefinitions(snapshot.cards);
                this.cardDefinitionIndex = new Map(this.cardDefinitionCache.map(entry => [entry.id, entry]));
                return this.cardDefinitionCache;
            }

            if (!this.cardDefinitionPromise) {
                this.cardDefinitionPromise = ensureCardDefinitions()
                    .then(cards => {
                        this.cardDefinitionCache = this.normalizeCardDefinitions(cards);
                        this.cardDefinitionIndex = new Map(this.cardDefinitionCache.map(entry => [entry.id, entry]));
                        return this.cardDefinitionCache;
                    })
                    .catch(error => {
                        console.warn('EffectConfigEditor: failed to load card definitions', error);
                        this.cardDefinitionCache = [];
                        this.cardDefinitionIndex = new Map();
                        return [];
                    })
                    .finally(() => {
                        this.cardDefinitionPromise = null;
                    });
            }

            return await this.cardDefinitionPromise;
        }

        normalizeCardDefinitions(cards) {
            const entries = (cards || [])
                .map((card, index) => {
                    const idRaw = card?.id ?? card?.Id ?? index;
                    const id = Number(idRaw);
                    if (!Number.isFinite(id) || id <= 0) {
                        return null;
                    }
                    const name = (card?.name ?? card?.Name ?? `Card ${id}`).toString().trim() || `Card ${id}`;
                    const description = (card?.description ?? card?.Description ?? '').toString().trim();
                    const cost = Number(card?.cost ?? card?.Cost ?? 0);
                    const cardClass = (card?.class ?? card?.Class ?? '').toString().trim();
                    const cardType = (card?.type ?? card?.Type ?? '').toString().trim();
                    const rarity = (card?.rarity ?? card?.Rarity ?? '').toString().trim();
                    const searchText = [name, id, cost, description, cardClass, cardType, rarity].join(' ').toLowerCase();
                    return { id, name, description, cost, cardClass, cardType, rarity, searchText };
                })
                .filter(Boolean)
                .sort((a, b) => a.id - b.id);
            return entries;
        }

        async loadAbilityDefinitions() {
            const snapshot = getAbilityDataSnapshot();
            if (snapshot.abilities?.length) {
                await this.loadCardDefinitions();
                this.abilityDefinitionCache = this.normalizeAbilityDefinitions(snapshot.abilities);
                this.abilityDefinitionIndex = new Map(this.abilityDefinitionCache.map(entry => [entry.id, entry]));
                return this.abilityDefinitionCache;
            }

            if (!this.abilityDefinitionPromise) {
                this.abilityDefinitionPromise = ensureAbilityData()
                    .then(async data => {
                        await this.loadCardDefinitions();
                        this.abilityDefinitionCache = this.normalizeAbilityDefinitions(data.abilities);
                        this.abilityDefinitionIndex = new Map(this.abilityDefinitionCache.map(entry => [entry.id, entry]));
                        return this.abilityDefinitionCache;
                    })
                    .catch(error => {
                        console.warn('EffectConfigEditor: failed to load ability definitions', error);
                        this.abilityDefinitionCache = [];
                        this.abilityDefinitionIndex = new Map();
                        return [];
                    })
                    .finally(() => {
                        this.abilityDefinitionPromise = null;
                    });
            }

            return await this.abilityDefinitionPromise;
        }

        normalizeAbilityDefinitions(abilities) {
            const entries = (abilities || [])
                .map((ability, index) => {
                    const idRaw = ability?.id ?? ability?.Id ?? index;
                    const id = Number(idRaw);
                    if (!Number.isFinite(id) || id <= 0) {
                        return null;
                    }
                    const name = (ability?.name ?? ability?.Name ?? `Ability ${id}`).toString().trim() || `Ability ${id}`;
                    const description = (ability?.description ?? ability?.Description ?? '').toString().trim();
                    const cardId = Number(ability?.cardId ?? ability?.CardId ?? 0);
                    const cardEntry = this.cardDefinitionIndex?.get?.(cardId);
                    const cardName = cardEntry?.name ?? '';
                    const keywords = Array.isArray(ability?.abilityKeywords)
                        ? ability.abilityKeywords.map(item => item?.keyword ?? item?.Keyword ?? '').filter(Boolean)
                        : [];
                    const keywordText = keywords.join(' ');
                    const searchText = [name, id, description, cardName, cardId, keywordText].join(' ').toLowerCase();
                    return { id, name, description, cardId, cardName, keywords, searchText };
                })
                .filter(Boolean)
                .sort((a, b) => a.id - b.id);
            return entries;
        }

        renderAbilityDefinitionIdEditor(container, options = {}) {
            const { values, onChange } = options;
            let list = [];
            if (Array.isArray(values)) {
                list = values.map(item => Number(item)).filter(Number.isFinite);
            } else if (Number.isFinite(values)) {
                list = [Number(values)];
            } else if (typeof values === 'string') {
                list = this.parseDelimitedIntList(values);
            }

            const wrapper = document.createElement('div');
            wrapper.className = 'int-list-editor ability-definition-id-editor';

            const inputRow = document.createElement('div');
            inputRow.className = 'int-list-input-row';

            const input = document.createElement('input');
            input.type = 'number';
            input.className = 'form-input int-list-input';
            input.placeholder = '输入能力ID并添加';

            const addBtn = this.createButton('添加', () => {
                const text = (input.value ?? '').toString().trim();
                if (!text) return;

                const items = this.parseDelimitedIntList(text);
                if (items.length === 0) {
                    input.value = '';
                    return;
                }

                list = Array.from(new Set([...list, ...items]));
                input.value = '';
                renderList();
                if (typeof onChange === 'function') {
                    onChange([...list]);
                }
            }, 'secondary');
            addBtn.classList.add('btn-small');

            const pickBtn = this.createButton('选择能力', async () => {
                const selected = await this.openAbilityDefinitionPickerModal(list);
                if (!Array.isArray(selected) || selected.length === 0) {
                    return;
                }
                list = Array.from(new Set([...list, ...selected]));
                renderList();
                if (typeof onChange === 'function') {
                    onChange([...list]);
                }
            }, 'secondary');
            pickBtn.classList.add('btn-small');

            input.addEventListener('keydown', (event) => {
                if (event.key !== 'Enter') return;
                event.preventDefault();
                addBtn.click();
            });

            inputRow.appendChild(input);
            inputRow.appendChild(addBtn);
            inputRow.appendChild(pickBtn);

            const listWrap = document.createElement('div');
            listWrap.className = 'int-list-chips';

            const renderList = () => {
                listWrap.innerHTML = '';
                if (list.length === 0) {
                    const empty = document.createElement('div');
                    empty.className = 'text-muted';
                    empty.textContent = '暂无列表项';
                    listWrap.appendChild(empty);
                    return;
                }

                list.forEach((value, index) => {
                    const chip = document.createElement('div');
                    chip.className = 'int-list-chip';

                    const label = document.createElement('span');
                    const entry = this.abilityDefinitionIndex?.get?.(value);
                    const cardName = entry?.cardName ? ` · ${entry.cardName}` : '';
                    label.textContent = entry ? `${entry.name} (#${value})${cardName}` : String(value);

                    const remove = document.createElement('button');
                    remove.type = 'button';
                    remove.className = 'int-list-remove';
                    remove.textContent = '×';
                    remove.addEventListener('click', () => {
                        list.splice(index, 1);
                        renderList();
                        if (typeof onChange === 'function') {
                            onChange([...list]);
                        }
                    });

                    chip.appendChild(label);
                    chip.appendChild(remove);
                    listWrap.appendChild(chip);
                });
            };

            renderList();
            void this.loadAbilityDefinitions().then(() => {
                renderList();
            });

            wrapper.appendChild(inputRow);
            wrapper.appendChild(listWrap);
            container.appendChild(wrapper);
        }

        renderCardDefinitionIdEditor(container, options = {}) {
            const { values, onChange, allowMultiple = true } = options;
            let list = [];
            if (Array.isArray(values)) {
                list = values.map(item => Number(item)).filter(Number.isFinite);
            } else if (Number.isFinite(values)) {
                list = [Number(values)];
            } else if (typeof values === 'string') {
                list = this.parseDelimitedIntList(values);
            }
            if (!allowMultiple && list.length > 1) {
                list = [list[0]];
            }

            const wrapper = document.createElement('div');
            wrapper.className = 'int-list-editor card-definition-id-editor';

            const inputRow = document.createElement('div');
            inputRow.className = 'int-list-input-row';

            const input = document.createElement('input');
            input.type = 'number';
            input.className = 'form-input int-list-input';
            input.placeholder = allowMultiple ? '输入卡牌ID并添加' : '输入卡牌ID';

            const addBtn = this.createButton(allowMultiple ? '添加' : '设置', () => {
                const text = (input.value ?? '').toString().trim();
                if (!text) return;

                const items = this.parseDelimitedIntList(text);
                if (items.length === 0) {
                    input.value = '';
                    return;
                }

                list = allowMultiple
                    ? Array.from(new Set([...list, ...items]))
                    : [items[0]];
                input.value = '';
                renderList();
                if (typeof onChange === 'function') {
                    onChange(allowMultiple ? [...list] : list[0]);
                }
            }, 'secondary');
            addBtn.classList.add('btn-small');

            const pickBtn = this.createButton('选择卡牌', async () => {
                const selected = await this.openCardDefinitionPickerModal(list, { allowMultiple });
                if (allowMultiple) {
                    if (!Array.isArray(selected) || selected.length === 0) {
                        return;
                    }
                    list = Array.from(new Set([...list, ...selected]));
                } else {
                    const next = Array.isArray(selected) ? selected[0] : selected;
                    if (!Number.isFinite(next)) {
                        return;
                    }
                    list = [Number(next)];
                }
                renderList();
                if (typeof onChange === 'function') {
                    onChange(allowMultiple ? [...list] : list[0]);
                }
            }, 'secondary');
            pickBtn.classList.add('btn-small');

            input.addEventListener('keydown', (event) => {
                if (event.key !== 'Enter') return;
                event.preventDefault();
                addBtn.click();
            });

            inputRow.appendChild(input);
            inputRow.appendChild(addBtn);
            inputRow.appendChild(pickBtn);

            const listWrap = document.createElement('div');
            listWrap.className = 'int-list-chips';

            const renderList = () => {
                listWrap.innerHTML = '';
                if (list.length === 0) {
                    const empty = document.createElement('div');
                    empty.className = 'text-muted';
                    empty.textContent = '暂无列表项';
                    listWrap.appendChild(empty);
                    return;
                }

                list.forEach((value, index) => {
                    const chip = document.createElement('div');
                    chip.className = 'int-list-chip';

                    const label = document.createElement('span');
                    const entry = this.cardDefinitionIndex?.get?.(value);
                    label.textContent = entry ? `${entry.name} (#${value})` : String(value);

                    const remove = document.createElement('button');
                    remove.type = 'button';
                    remove.className = 'int-list-remove';
                    remove.textContent = '×';
                    remove.addEventListener('click', () => {
                        list.splice(index, 1);
                        renderList();
                        if (typeof onChange === 'function') {
                            onChange(allowMultiple ? [...list] : list[0]);
                        }
                    });

                    chip.appendChild(label);
                    chip.appendChild(remove);
                    listWrap.appendChild(chip);
                });
            };

            renderList();
            void this.loadCardDefinitions().then(() => {
                renderList();
            });

            wrapper.appendChild(inputRow);
            wrapper.appendChild(listWrap);
            container.appendChild(wrapper);
        }

        async openAbilityDefinitionPickerModal(selectedIds = [], options = {}) {
            if (this.activeAbilityDefinitionPickerModal) {
                this.activeAbilityDefinitionPickerModal.remove();
                this.activeAbilityDefinitionPickerModal = null;
            }

            return await new Promise((resolve) => {
                const allowMultiple = options.allowMultiple !== false;
                const modalTitle = options.title || '选择能力';
                const confirmText = options.confirmText || (allowMultiple ? '确认添加' : '确认选择');
                const hintText = options.hintText || (allowMultiple ? '选择能力后确认添加' : '选择能力后确认');

                const overlay = document.createElement('div');
                overlay.className = 'modal-overlay grant-ability-copy-overlay';

                const modal = document.createElement('div');
                modal.className = 'modal-container ability-edit-container grant-ability-copy-modal';

                const header = document.createElement('div');
                header.className = 'modal-header';
                header.innerHTML = `<h3>${modalTitle}</h3>`;

                const closeBtn = document.createElement('button');
                closeBtn.className = 'modal-close-btn';
                closeBtn.type = 'button';
                closeBtn.setAttribute('aria-label', '关闭');
                closeBtn.textContent = '×';
                header.appendChild(closeBtn);

                const content = document.createElement('div');
                content.className = 'modal-content ability-copy-modal';
                content.innerHTML = `
                    <div class="ability-copy-filters">
                        <div class="ability-copy-search">
                            <label class="form-label" for="remove-ability-card-search">筛选</label>
                            <input class="tech-input" type="search" id="remove-ability-card-search" placeholder="搜索卡牌或能力">
                        </div>
                        <div class="ability-copy-stats tech-text-dim" id="remove-ability-card-stats">卡牌 0 / 能力 0</div>
                    </div>
                    <div class="tech-text-dim ability-copy-hint">${hintText}</div>
                    <div class="ability-copy-list custom-scrollbar" id="remove-ability-card-list">
                        <div class="loading-spinner"></div>
                        <p>正在加载卡牌列表...</p>
                    </div>
                    <div class="ability-copy-pagination">
                        <button class="btn-tech btn-secondary btn-small" id="remove-ability-card-prev" disabled>上一页</button>
                        <div class="ability-copy-page-info tech-text-dim" id="remove-ability-card-page-info">1 / 1</div>
                        <button class="btn-tech btn-secondary btn-small" id="remove-ability-card-next" disabled>下一页</button>
                    </div>
                    <div class="modal-actions ability-copy-actions">
                        <button class="btn-tech btn-secondary" id="remove-ability-card-cancel">取消</button>
                        <button class="btn-tech btn-primary" id="remove-ability-card-confirm" disabled>${confirmText}</button>
                    </div>
                `;

                modal.appendChild(header);
                modal.appendChild(content);
                overlay.appendChild(modal);
                document.body.appendChild(overlay);

                this.activeAbilityDefinitionPickerModal = overlay;
                const modalLoader = getBlockingLoader(modal);

                const finish = (value) => {
                    resolve(value);
                    modalLoader?.hide();
                    if (this.activeAbilityDefinitionPickerModal) {
                        this.activeAbilityDefinitionPickerModal.remove();
                        this.activeAbilityDefinitionPickerModal = null;
                    }
                };

                const close = () => finish([]);
                closeBtn.addEventListener('click', close);

                const searchInput = content.querySelector('#remove-ability-card-search');
                const statsEl = content.querySelector('#remove-ability-card-stats');
                const listEl = content.querySelector('#remove-ability-card-list');
                const pageInfoEl = content.querySelector('#remove-ability-card-page-info');
                const prevBtn = content.querySelector('#remove-ability-card-prev');
                const nextBtn = content.querySelector('#remove-ability-card-next');
                const cancelBtn = content.querySelector('#remove-ability-card-cancel');
                const confirmBtn = content.querySelector('#remove-ability-card-confirm');

                const initialSelection = (selectedIds || []).map(item => Number(item)).filter(Number.isFinite);
                const selectedSet = new Set(allowMultiple ? initialSelection : initialSelection.slice(0, 1));
                const expandedCards = new Set();
                const abilitiesByCard = new Map();
                let cardEntries = [];
                let filteredEntries = [];
                const pageSize = 10;
                let currentPage = 1;

                const updateStats = (cardCount, abilityCount) => {
                    if (statsEl) {
                        const selectedCount = selectedSet.size;
                        const base = `卡牌 ${cardCount} / 能力 ${abilityCount}`;
                        statsEl.textContent = selectedCount > 0 ? `${base} · 已选 ${selectedCount}` : base;
                    }
                };

                const updatePagination = (totalPages) => {
                    const normalizedTotal = Math.max(totalPages, 1);
                    if (pageInfoEl) {
                        pageInfoEl.textContent = `${currentPage} / ${normalizedTotal}`;
                    }
                    if (prevBtn) prevBtn.disabled = currentPage <= 1;
                    if (nextBtn) nextBtn.disabled = currentPage >= normalizedTotal;
                };

                const buildAbilityHtml = (abilities) => {
                    return abilities.map(ability => {
                        const abilityId = Number(ability.id);
                        const abilityName = ability?.name ?? ability?.Name ?? '未命名能力';
                        const abilityDescription = ability?.description ?? ability?.Description ?? '';
                        const keywordText = Array.isArray(ability.keywords) ? ability.keywords.join(', ') : '';
                        const isSelected = selectedSet.has(abilityId);
                        return `
                            <button type="button" class="ability-copy-ability${isSelected ? ' selected' : ''}" data-ability-id="${abilityId}">
                                <div class="ability-copy-ability-header">
                                    <span class="ability-copy-ability-name tech-text-bold">${abilityName}</span>
                                    ${keywordText ? `<span class="ability-copy-ability-tag tech-tag">${keywordText}</span>` : ''}
                                </div>
                                ${abilityDescription ? `<div class="ability-copy-ability-desc tech-text-dim">${abilityDescription}</div>` : ''}
                            </button>
                        `;
                    }).join('');
                };

                const renderList = () => {
                    if (!listEl) return;
                    const totalPages = Math.max(1, Math.ceil(filteredEntries.length / pageSize));
                    currentPage = Math.min(currentPage, totalPages);

                    if (filteredEntries.length === 0) {
                        listEl.innerHTML = '<div class="abilities-placeholder"><p>没有匹配的卡牌</p></div>';
                        updateStats(0, 0);
                        updatePagination(0);
                        return;
                    }

                    const start = (currentPage - 1) * pageSize;
                    const pageEntries = filteredEntries.slice(start, start + pageSize);
                    const totalAbilityCount = filteredEntries.reduce((sum, entry) => {
                        const abilities = abilitiesByCard.get(entry.cardId);
                        return sum + (Array.isArray(abilities) ? abilities.length : 0);
                    }, 0);

                    listEl.innerHTML = pageEntries.map(entry => {
                        const abilities = abilitiesByCard.get(entry.cardId) || [];
                        const isExpanded = expandedCards.has(entry.cardId);
                        const abilityCount = abilities.length;
                        const costLabel = Number.isFinite(entry.cardCost) ? entry.cardCost : 0;
                        const bodyHtml = isExpanded
                            ? (abilityCount === 0
                                ? `<div class="abilities-placeholder"><p>暂无可用能力</p></div>`
                                : `<div class="ability-copy-abilities">${buildAbilityHtml(abilities)}</div>`)
                            : '';

                        return `
                            <div class="ability-copy-card${isExpanded ? ' expanded' : ''}" data-card-id="${entry.cardId}">
                                <button type="button" class="ability-copy-card-header" aria-expanded="${isExpanded}">
                                    <div class="ability-copy-card-main">
                                        <span class="ability-copy-card-name tech-text-bold">${entry.cardName}</span>
                                        <span class="ability-copy-card-id tech-text-dim">#${entry.cardId}</span>
                                    </div>
                                    <div class="ability-copy-card-meta">
                                        <span class="tech-badge">COST ${costLabel}</span>
                                        ${entry.cardType ? `<span class="tech-tag">${entry.cardType}</span>` : ''}
                                        ${entry.cardClass ? `<span class="tech-tag">${entry.cardClass}</span>` : ''}
                                        <span class="tech-tag">ABILITIES ${abilityCount}</span>
                                    </div>
                                    <span class="ability-copy-card-toggle" aria-hidden="true">></span>
                                </button>
                                <div class="ability-copy-card-body">
                                    ${bodyHtml}
                                </div>
                            </div>
                        `;
                    }).join('');

                    listEl.querySelectorAll('.ability-copy-card').forEach(cardEl => {
                        const header = cardEl.querySelector('.ability-copy-card-header');
                        const cardId = Number(cardEl.dataset.cardId);
                        if (!header || !Number.isFinite(cardId)) return;
                        header.addEventListener('click', () => {
                            const isExpanded = cardEl.classList.toggle('expanded');
                            header.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
                            if (isExpanded) {
                                expandedCards.add(cardId);
                            } else {
                                expandedCards.delete(cardId);
                            }
                            renderList();
                        });
                    });

                    listEl.querySelectorAll('.ability-copy-ability').forEach(abilityEl => {
                        const abilityId = Number(abilityEl.dataset.abilityId);
                        if (!Number.isFinite(abilityId)) return;
                        abilityEl.addEventListener('click', (event) => {
                            event.stopPropagation();
                            if (!allowMultiple) {
                                selectedSet.clear();
                                selectedSet.add(abilityId);
                                listEl.querySelectorAll('.ability-copy-ability').forEach(btn => btn.classList.remove('selected'));
                                abilityEl.classList.add('selected');
                            } else if (selectedSet.has(abilityId)) {
                                selectedSet.delete(abilityId);
                                abilityEl.classList.remove('selected');
                            } else {
                                selectedSet.add(abilityId);
                                abilityEl.classList.add('selected');
                            }
                            if (confirmBtn) confirmBtn.disabled = selectedSet.size === 0;
                            updateStats(filteredEntries.length, totalAbilityCount);
                        });
                    });

                    updateStats(filteredEntries.length, totalAbilityCount);
                    updatePagination(totalPages);
                };

                const applyFilter = (value) => {
                    const term = (value || '').trim().toLowerCase();
                    if (!term) {
                        filteredEntries = [...cardEntries];
                    } else {
                        filteredEntries = cardEntries.filter(entry => entry.cardSearchText.includes(term));
                    }
                    currentPage = 1;
                    renderList();
                };

                const init = async () => {
                    modalLoader?.show('正在加载能力库...');
                    try {
                        const abilities = await this.loadAbilityDefinitions();
                        const cards = Array.isArray(this.cardDefinitionCache) ? this.cardDefinitionCache : [];

                        abilitiesByCard.clear();
                        abilities.forEach(ability => {
                            const cardId = Number(ability.cardId);
                            if (!Number.isFinite(cardId) || cardId <= 0) return;
                            const list = abilitiesByCard.get(cardId) || [];
                            list.push(ability);
                            abilitiesByCard.set(cardId, list);
                        });

                        cardEntries = cards
                            .map(card => {
                                const cardId = Number(card.id);
                                if (!Number.isFinite(cardId) || !abilitiesByCard.has(cardId)) {
                                    return null;
                                }
                                const cardName = card.name || `Card ${cardId}`;
                                const cardCost = Number(card.cost ?? 0);
                                const cardType = card.cardType || '';
                                const cardClass = card.cardClass || '';
                                const cardRarity = card.rarity || '';
                                const abilityNames = (abilitiesByCard.get(cardId) || [])
                                    .map(item => item.name)
                                    .join(' ');
                                const cardSearchText = [
                                    cardName,
                                    cardId,
                                    cardCost,
                                    cardType,
                                    cardClass,
                                    cardRarity,
                                    abilityNames
                                ].join(' ').toLowerCase();

                                return {
                                    cardId,
                                    cardName,
                                    cardCost,
                                    cardType,
                                    cardClass,
                                    cardRarity,
                                    cardSearchText
                                };
                            })
                            .filter(Boolean)
                            .sort((a, b) => String(a.cardName).localeCompare(String(b.cardName), undefined, { numeric: true, sensitivity: 'base' }));

                        applyFilter('');
                    } catch (error) {
                        console.error('EffectConfigEditor: failed to load abilities', error);
                        if (listEl) {
                            listEl.innerHTML = `
                                <div class="error-message">加载能力失败: ${error.message}</div>
                            `;
                        }
                    } finally {
                        modalLoader?.hide();
                    }
                };

                if (searchInput) {
                    searchInput.addEventListener('input', (event) => {
                        applyFilter(event.target.value);
                    });
                }

                if (prevBtn) {
                    prevBtn.addEventListener('click', () => {
                        if (currentPage > 1) {
                            currentPage -= 1;
                            renderList();
                        }
                    });
                }

                if (nextBtn) {
                    nextBtn.addEventListener('click', () => {
                        const totalPages = Math.max(1, Math.ceil(filteredEntries.length / pageSize));
                        if (currentPage < totalPages) {
                            currentPage += 1;
                            renderList();
                        }
                    });
                }

                if (cancelBtn) {
                    cancelBtn.addEventListener('click', close);
                }

                if (confirmBtn) {
                    confirmBtn.addEventListener('click', () => {
                        const selected = Array.from(selectedSet).sort((a, b) => a - b);
                        finish(selected);
                    });
                }

                if (confirmBtn) {
                    confirmBtn.disabled = selectedSet.size === 0;
                }

                void init();
            });
        }

        renderAbilityListEditor(container, paramValue, updateParam, context = {}) {
            const normalized = this.normalizeAbilityListValue(paramValue);
            const state = { items: normalized.items };
            const effectTypeName = context?.effectTypeName || '';
            const isDecisionAbility = this.isDecisionAbilityEffect(effectTypeName);
            const getDecisionEditorOptions = (ability) => {
                if (!isDecisionAbility) {
                    return null;
                }
                return {
                    lockedTrigger: this.getDecisionAbilityLockConfig(ability),
                    lockedTriggerMessage: '分支能力触发时机与有效区域已锁定，无法修改。',
                    lockValidZones: true
                };
            };

            const wrapper = document.createElement('div');
            wrapper.className = 'grant-ability-list-editor';

            const header = document.createElement('div');
            header.className = 'grant-ability-list-header';

            const hint = document.createElement('div');
            hint.className = 'text-muted';
            hint.textContent = '通过列表管理要授予的能力，支持新增、编辑、排序与复制已有能力。';

            const actions = document.createElement('div');
            actions.className = 'grant-ability-list-actions';

            const addBtn = document.createElement('button');
            addBtn.type = 'button';
            addBtn.className = 'btn-primary btn-small';
            addBtn.textContent = '新增能力';

            const copyBtn = document.createElement('button');
            copyBtn.type = 'button';
            copyBtn.className = 'btn-secondary btn-small';
            copyBtn.textContent = '复制已有';

            actions.appendChild(addBtn);
            actions.appendChild(copyBtn);
            header.appendChild(hint);
            header.appendChild(actions);
            wrapper.appendChild(header);

            const listContainer = document.createElement('div');
            listContainer.className = 'grant-ability-list';
            wrapper.appendChild(listContainer);

            container.appendChild(wrapper);

            const syncValue = (next) => {
                const safeList = Array.isArray(next) ? next : [];
                state.items = safeList;
                updateParam(safeList);
                renderList();
            };

            const handleAdd = async () => {
                const created = await this.openGrantAbilityEditorModal(null, getDecisionEditorOptions(null));
                if (!created) return;
                syncValue([...state.items, created]);
            };

            addBtn.addEventListener('click', () => {
                void handleAdd();
            });

            copyBtn.addEventListener('click', async () => {
                const copied = await this.openAbilityConfigPickerModal();
                if (!copied) return;
                syncValue([...state.items, copied]);
            });

            const moveItem = (from, to) => {
                if (to < 0 || to >= state.items.length) return;
                const next = [...state.items];
                const [moved] = next.splice(from, 1);
                next.splice(to, 0, moved);
                syncValue(next);
            };

            const renderList = () => {
                listContainer.innerHTML = '';

                if (!state.items.length) {
                    const empty = document.createElement('div');
                    empty.className = 'grant-ability-empty';
                    empty.innerHTML = `
                        <div class="empty-state">
                            <h5 class="empty-title">暂无授予能力</h5>
                            <p class="empty-desc">点击下方按钮新增能力，或从已有能力库复制。</p>
                            <button type="button" class="btn-primary btn-small">添加第一条能力</button>
                        </div>
                    `;
                    const emptyBtn = empty.querySelector('button');
                    if (emptyBtn) {
                        emptyBtn.addEventListener('click', () => void handleAdd());
                    }
                    listContainer.appendChild(empty);
                    return;
                }

                state.items.forEach((ability, index) => {
                    const row = document.createElement('div');
                    row.className = 'grant-ability-row';
                    row.dataset.index = String(index);

                    const main = document.createElement('div');
                    main.className = 'grant-ability-main';

                    const title = document.createElement('div');
                    title.className = 'grant-ability-title';
                    title.textContent = ability?.name || ability?.Name || `未命名能力${index + 1}`;

                    const summary = document.createElement('div');
                    summary.className = 'grant-ability-summary';
                    summary.textContent = this.formatAbilityTriggerName(ability);

                    main.appendChild(title);
                    main.appendChild(summary);

                    const actionsRow = document.createElement('div');
                    actionsRow.className = 'grant-ability-actions';

                    const moveUp = document.createElement('button');
                    moveUp.type = 'button';
                    moveUp.className = 'btn-icon grant-ability-move';
                    moveUp.textContent = '↑';
                    moveUp.title = '上移';
                    moveUp.setAttribute('aria-label', '上移');
                    moveUp.disabled = index === 0;
                    moveUp.addEventListener('click', () => moveItem(index, index - 1));

                    const moveDown = document.createElement('button');
                    moveDown.type = 'button';
                    moveDown.className = 'btn-icon grant-ability-move';
                    moveDown.textContent = '↓';
                    moveDown.title = '下移';
                    moveDown.setAttribute('aria-label', '下移');
                    moveDown.disabled = index === state.items.length - 1;
                    moveDown.addEventListener('click', () => moveItem(index, index + 1));

                    const editBtn = document.createElement('button');
                    editBtn.type = 'button';
                    editBtn.className = 'btn-secondary btn-small';
                    editBtn.textContent = '编辑';
                    editBtn.addEventListener('click', async () => {
                        const updated = await this.openGrantAbilityEditorModal(ability, getDecisionEditorOptions(ability));
                        if (!updated) return;
                        const next = state.items.map((item, idx) => idx === index ? updated : item);
                        syncValue(next);
                    });

                    const deleteBtn = document.createElement('button');
                    deleteBtn.type = 'button';
                    deleteBtn.className = 'btn-icon delete-btn';
                    deleteBtn.title = '删除';
                    deleteBtn.setAttribute('aria-label', '删除');
                    deleteBtn.textContent = '×';
                    deleteBtn.addEventListener('click', () => {
                        if (!confirm('确定删除该授予能力？')) return;
                        const next = state.items.filter((_, idx) => idx !== index);
                        syncValue(next);
                    });

                    actionsRow.appendChild(moveUp);
                    actionsRow.appendChild(moveDown);
                    actionsRow.appendChild(editBtn);
                    actionsRow.appendChild(deleteBtn);

                    row.appendChild(main);
                    row.appendChild(actionsRow);
                    listContainer.appendChild(row);
                });
            };

            renderList();

            if (normalized.didNormalize) {
                updateParam(state.items);
            }
        }

        normalizeAbilityListValue(raw) {
            let didNormalize = false;
            let value = raw;

            const unwrapped = this.unwrapStaticValueConfig(raw);
            if (unwrapped.ok) {
                value = unwrapped.value;
                didNormalize = true;
            }

            if (typeof value === 'string') {
                try {
                    const parsed = JSON.parse(value);
                    if (Array.isArray(parsed)) {
                        return { items: parsed.filter(Boolean), didNormalize: true };
                    }
                } catch (error) {
                    return { items: [], didNormalize: true };
                }
                return { items: [], didNormalize: true };
            }

            if (!Array.isArray(value)) {
                return { items: [], didNormalize };
            }

            return { items: value.filter(Boolean), didNormalize };
        }

        normalizeAbilityConfigForList(ability) {
            if (!ability || typeof ability !== 'object') return null;

            const normalized = { ...ability };
            const name = ability.name ?? ability.Name ?? '';
            const description = ability.description ?? ability.Description ?? '';
            const keywords = ability.abilityKeywords ?? ability.AbilityKeywords ?? [];
            const trigger = ability.trigger ?? ability.Trigger ?? {
                eventTypes: [],
                relationType: '',
                timing: 'On',
                validZones: []
            };

            normalized.name = String(name || '').trim() || '未命名能力';
            normalized.description = description ?? '';
            normalized.abilityKeywords = Array.isArray(keywords) ? keywords : [];
            const legacyExecutionKind = ability.executionKind ?? ability.ExecutionKind ?? null;
            const rawType = ability.type ?? ability.Type ?? null;
            normalized.type = rawType
                || (String(legacyExecutionKind).toLowerCase() === 'aura' ? 'Continuous' : 'Triggered');
            normalized.trigger = trigger;
            normalized.conditions = ability.conditions ?? ability.Conditions ?? [];
            normalized.targets = ability.targets ?? ability.Targets ?? [];
            normalized.effects = ability.effects ?? ability.Effects ?? [];
            normalized.auras = ability.auras ?? ability.Auras ?? [];
            normalized.counterRegistrations = ability.counterRegistrations ?? ability.CounterRegistrations ?? [];
            normalized.isActive = ability.isActive ?? ability.IsActive ?? ability.isEnabled ?? ability.IsEnabled ?? true;
            normalized.version = ability.version ?? ability.Version ?? '1.0';

            delete normalized.id;
            delete normalized.Id;
            delete normalized.cardId;
            delete normalized.CardId;
            delete normalized.abilityType;
            delete normalized.priority;
            delete normalized.jsonConfig;
            delete normalized.executionKind;
            delete normalized.ExecutionKind;

            return normalized;
        }

        parseAbilityConfigFromSource(ability) {
            const raw = ability?.jsonConfig ?? ability?.JsonConfig ?? null;
            if (!raw) return null;
            try {
                return typeof raw === 'string' ? JSON.parse(raw) : raw;
            } catch (error) {
                console.warn('EffectConfigEditor: failed to parse ability jsonConfig', error);
                return null;
            }
        }

        normalizeAbilityConfigFromSource(ability, parsedConfig) {
            if (!ability && !parsedConfig) return null;
            const base = parsedConfig || {};
            const config = {
                ...base,
                name: ability?.name ?? ability?.Name ?? base.name ?? '未命名能力',
                description: ability?.description ?? ability?.Description ?? base.description ?? '',
                abilityKeywords: ability?.abilityKeywords ?? ability?.AbilityKeywords ?? base.abilityKeywords ?? [],
                isActive: ability?.isActive ?? ability?.IsActive ?? base.isActive ?? true,
                version: base.version ?? base.Version ?? '1.0'
            };
            return this.normalizeAbilityConfigForList(config);
        }

        async fetchAbilityDetailById(abilityId) {
            if (!abilityId) return null;
            try {
                const response = await fetch(`/api/abilities/${abilityId}`);
                if (!response.ok) return null;
                return await response.json();
            } catch (error) {
                console.warn('EffectConfigEditor: failed to fetch ability detail', error);
                return null;
            }
        }

        async buildAbilityConfigFromAbilityId(abilityId) {
            if (!abilityId) return null;
            const detail = await this.fetchAbilityDetailById(abilityId);
            const parsed = this.parseAbilityConfigFromSource(detail);
            if (detail || parsed) {
                return this.normalizeAbilityConfigFromSource(detail, parsed);
            }

            const fallback = this.abilityDefinitionIndex?.get?.(abilityId);
            if (!fallback) return null;
            return this.normalizeAbilityConfigFromSource(fallback, this.parseAbilityConfigFromSource(fallback));
        }

        async openAbilityConfigPickerModal() {
            const selected = await this.openAbilityDefinitionPickerModal([], {
                allowMultiple: false,
                title: '复制现有能力',
                hintText: '选择能力后确认复制',
                confirmText: '确认复制'
            });
            const abilityId = Array.isArray(selected) ? selected[0] : selected;
            if (!Number.isFinite(Number(abilityId))) return null;
            return await this.buildAbilityConfigFromAbilityId(Number(abilityId));
        }

        buildAbilityEditorInput(ability) {
            if (!ability) {
                return {
                    name: '',
                    description: '',
                    abilityKeywords: [],
                    trigger: {
                        eventTypes: [],
                        relationType: '',
                        timing: 'On',
                        validZones: []
                    },
                conditions: [],
                targets: [],
                effects: [],
                auras: [],
                type: 'Triggered',
                counterRegistrations: [],
                isActive: true
            };
        }

            return {
                name: ability.name ?? ability.Name ?? '',
                description: ability.description ?? ability.Description ?? '',
                abilityKeywords: ability.abilityKeywords ?? ability.AbilityKeywords ?? [],
                type: ability.type ?? ability.Type ?? 'Triggered',
                trigger: ability.trigger ?? ability.Trigger ?? {
                    eventTypes: [],
                    relationType: '',
                    timing: 'On',
                    validZones: []
                },
                conditions: ability.conditions ?? ability.Conditions ?? [],
                targets: ability.targets ?? ability.Targets ?? [],
                effects: ability.effects ?? ability.Effects ?? [],
                auras: ability.auras ?? ability.Auras ?? [],
                counterRegistrations: ability.counterRegistrations ?? ability.CounterRegistrations ?? [],
                isActive: ability.isActive ?? ability.IsActive ?? ability.isEnabled ?? ability.IsEnabled ?? true
            };
        }

        extractAbilityConfigFromEditor(finalConfig) {
            if (!finalConfig || typeof finalConfig !== 'object') return null;

            const rawConfig = finalConfig.jsonConfig ?? null;
            if (rawConfig) {
                try {
                    const parsed = typeof rawConfig === 'string' ? JSON.parse(rawConfig) : rawConfig;
                    return this.normalizeAbilityConfigForList(parsed);
                } catch (error) {
                    console.warn('EffectConfigEditor: failed to parse ability jsonConfig', error);
                }
            }

            const fallback = {
                name: finalConfig.name ?? '',
                description: finalConfig.description ?? '',
                abilityKeywords: finalConfig.abilityKeywords ?? [],
                type: finalConfig.type ?? 'Triggered',
                trigger: finalConfig.trigger ?? {
                    eventTypes: [],
                    relationType: '',
                    timing: 'On',
                    validZones: []
                },
                conditions: finalConfig.conditions ?? [],
                targets: finalConfig.targets ?? [],
                effects: finalConfig.effects ?? [],
                auras: finalConfig.auras ?? [],
                counterRegistrations: finalConfig.counterRegistrations ?? [],
                isActive: finalConfig.isActive ?? true,
                version: finalConfig.version ?? '1.0'
            };

            return this.normalizeAbilityConfigForList(fallback);
        }

        formatAbilityTriggerName(ability) {
            const trigger = ability?.trigger ?? ability?.Trigger ?? null;
            const rawEvents = trigger?.eventTypes ?? trigger?.EventTypes ?? [];
            const events = Array.isArray(rawEvents)
                ? rawEvents.map(eventType => String(eventType)).filter(Boolean)
                : [];

            if (events.length === 0) {
                return '触发: 未配置';
            }
            return `触发: ${events.join(' / ')}`;
        }

        async openGrantAbilityEditorModal(baseAbility, editorOptions = null) {
            if (this.activeGrantAbilityEditorModal) {
                this.activeGrantAbilityEditorModal.remove();
                this.activeGrantAbilityEditorModal = null;
            }

            return await new Promise((resolve) => {
                const overlay = document.createElement('div');
                overlay.className = 'modal-overlay grant-ability-editor-overlay';

                const modal = document.createElement('div');
                modal.className = 'modal-container ability-edit-container grant-ability-editor-modal';

                const header = document.createElement('div');
                header.className = 'modal-header';
                header.innerHTML = `<h3>${baseAbility ? '编辑授予能力' : '新增授予能力'}</h3>`;

                const closeBtn = document.createElement('button');
                closeBtn.className = 'modal-close-btn';
                closeBtn.type = 'button';
                closeBtn.setAttribute('aria-label', '关闭');
                closeBtn.textContent = '×';
                header.appendChild(closeBtn);

                const content = document.createElement('div');
                content.className = 'modal-content grant-ability-editor-content';

                modal.appendChild(header);
                modal.appendChild(content);
                overlay.appendChild(modal);
                document.body.appendChild(overlay);

                const finish = (value) => {
                    resolve(value);
                    if (this.activeGrantAbilityEditorModal) {
                        this.activeGrantAbilityEditorModal.remove();
                        this.activeGrantAbilityEditorModal = null;
                    }
                };

                const close = () => finish(null);
                closeBtn.addEventListener('click', close);

                this.activeGrantAbilityEditorModal = overlay;

                const renderEditor = async () => {
                    const { AbilityEditor } = await import('./AbilityEditor.js');
                    const abilityEditor = new AbilityEditor();
                    const editorInput = this.buildAbilityEditorInput(baseAbility);

                    const options = editorOptions || {};
                    await abilityEditor.render(
                        content,
                        editorInput,
                        (updatedConfig) => {
                            if (updatedConfig === null) {
                                finish(null);
                                return;
                            }
                            const abilityConfig = this.extractAbilityConfigFromEditor(updatedConfig);
                            finish(abilityConfig);
                        },
                        options
                    );
                };

                void renderEditor();
            });
        }

        async openGrantAbilityCopyModal() {
            return await this.openAbilityConfigPickerModal();
        }

        async openCardDefinitionPickerModal(selectedIds = [], options = {}) {
            if (this.activeCardDefinitionPickerModal) {
                this.activeCardDefinitionPickerModal.remove();
                this.activeCardDefinitionPickerModal = null;
            }

            return await new Promise((resolve) => {
                const allowMultiple = options.allowMultiple !== false;
                const modalTitle = options.title || '选择卡牌';
                const confirmText = options.confirmText || (allowMultiple ? '确认添加' : '确认选择');
                const hintText = options.hintText || (allowMultiple ? '选择卡牌后确认添加' : '选择卡牌后确认');

                const overlay = document.createElement('div');
                overlay.className = 'modal-overlay grant-ability-copy-overlay';

                const modal = document.createElement('div');
                modal.className = 'modal-container ability-edit-container grant-ability-copy-modal';

                const header = document.createElement('div');
                header.className = 'modal-header';
                header.innerHTML = `<h3>${modalTitle}</h3>`;

                const closeBtn = document.createElement('button');
                closeBtn.className = 'modal-close-btn';
                closeBtn.type = 'button';
                closeBtn.setAttribute('aria-label', '关闭');
                closeBtn.textContent = '×';
                header.appendChild(closeBtn);

                const content = document.createElement('div');
                content.className = 'modal-content ability-copy-modal';
                content.innerHTML = `
                    <div class="ability-copy-filters">
                        <div class="ability-copy-search">
                            <label class="form-label" for="card-definition-search">筛选</label>
                            <input class="tech-input" type="search" id="card-definition-search" placeholder="搜索卡牌名称或ID">
                        </div>
                        <div class="ability-copy-stats tech-text-dim" id="card-definition-stats">卡牌 0</div>
                    </div>
                    <div class="tech-text-dim ability-copy-hint">${hintText}</div>
                    <div class="ability-copy-list custom-scrollbar" id="card-definition-list">
                        <div class="loading-spinner"></div>
                        <p>正在加载卡牌列表...</p>
                    </div>
                    <div class="ability-copy-pagination">
                        <button class="btn-tech btn-secondary btn-small" id="card-definition-prev" disabled>上一页</button>
                        <div class="ability-copy-page-info tech-text-dim" id="card-definition-page-info">1 / 1</div>
                        <button class="btn-tech btn-secondary btn-small" id="card-definition-next" disabled>下一页</button>
                    </div>
                    <div class="modal-actions ability-copy-actions">
                        <button class="btn-tech btn-secondary" id="card-definition-cancel">取消</button>
                        <button class="btn-tech btn-primary" id="card-definition-confirm" disabled>${confirmText}</button>
                    </div>
                `;

                modal.appendChild(header);
                modal.appendChild(content);
                overlay.appendChild(modal);
                document.body.appendChild(overlay);

                this.activeCardDefinitionPickerModal = overlay;
                const modalLoader = getBlockingLoader(modal);

                const finish = (value) => {
                    resolve(value);
                    modalLoader?.hide();
                    if (this.activeCardDefinitionPickerModal) {
                        this.activeCardDefinitionPickerModal.remove();
                        this.activeCardDefinitionPickerModal = null;
                    }
                };

                const close = () => finish([]);
                closeBtn.addEventListener('click', close);

                const searchInput = content.querySelector('#card-definition-search');
                const statsEl = content.querySelector('#card-definition-stats');
                const listEl = content.querySelector('#card-definition-list');
                const pageInfoEl = content.querySelector('#card-definition-page-info');
                const prevBtn = content.querySelector('#card-definition-prev');
                const nextBtn = content.querySelector('#card-definition-next');
                const cancelBtn = content.querySelector('#card-definition-cancel');
                const confirmBtn = content.querySelector('#card-definition-confirm');

                const initialSelection = (selectedIds || []).map(item => Number(item)).filter(Number.isFinite);
                const selectedSet = new Set(allowMultiple ? initialSelection : initialSelection.slice(0, 1));
                let cardEntries = [];
                let filteredEntries = [];
                const pageSize = 12;
                let currentPage = 1;

                const updateStats = () => {
                    if (statsEl) {
                        const base = `卡牌 ${filteredEntries.length}`;
                        const selectedCount = selectedSet.size;
                        statsEl.textContent = selectedCount > 0 ? `${base} · 已选 ${selectedCount}` : base;
                    }
                };

                const updatePagination = (totalPages) => {
                    const normalizedTotal = Math.max(totalPages, 1);
                    if (pageInfoEl) {
                        pageInfoEl.textContent = `${currentPage} / ${normalizedTotal}`;
                    }
                    if (prevBtn) prevBtn.disabled = currentPage <= 1;
                    if (nextBtn) nextBtn.disabled = currentPage >= normalizedTotal;
                };

                const renderList = () => {
                    if (!listEl) return;
                    const totalPages = Math.max(1, Math.ceil(filteredEntries.length / pageSize));
                    currentPage = Math.min(currentPage, totalPages);

                    if (filteredEntries.length === 0) {
                        listEl.innerHTML = '<div class="abilities-placeholder"><p>没有匹配的卡牌</p></div>';
                        updateStats();
                        updatePagination(0);
                        return;
                    }

                    const start = (currentPage - 1) * pageSize;
                    const pageEntries = filteredEntries.slice(start, start + pageSize);

                    listEl.innerHTML = pageEntries.map(entry => {
                        const isSelected = selectedSet.has(entry.id);
                        const costLabel = Number.isFinite(entry.cost) ? entry.cost : 0;
                        return `
                            <div class="ability-copy-card${isSelected ? ' selected' : ''}" data-card-id="${entry.id}">
                                <button type="button" class="ability-copy-card-header" aria-pressed="${isSelected}">
                                    <div class="ability-copy-card-main">
                                        <span class="ability-copy-card-name tech-text-bold">${entry.name}</span>
                                        <span class="ability-copy-card-id tech-text-dim">#${entry.id}</span>
                                    </div>
                                    <div class="ability-copy-card-meta">
                                        <span class="tech-badge">COST ${costLabel}</span>
                                        ${entry.cardType ? `<span class="tech-tag">${entry.cardType}</span>` : ''}
                                        ${entry.cardClass ? `<span class="tech-tag">${entry.cardClass}</span>` : ''}
                                        ${entry.rarity ? `<span class="tech-tag">${entry.rarity}</span>` : ''}
                                    </div>
                                </button>
                            </div>
                        `;
                    }).join('');

                    listEl.querySelectorAll('.ability-copy-card').forEach(cardEl => {
                        const header = cardEl.querySelector('.ability-copy-card-header');
                        const cardId = Number(cardEl.dataset.cardId);
                        if (!header || !Number.isFinite(cardId)) return;
                        header.addEventListener('click', () => {
                            if (!allowMultiple) {
                                selectedSet.clear();
                                selectedSet.add(cardId);
                                listEl.querySelectorAll('.ability-copy-card').forEach(btn => btn.classList.remove('selected'));
                                cardEl.classList.add('selected');
                            } else if (selectedSet.has(cardId)) {
                                selectedSet.delete(cardId);
                                cardEl.classList.remove('selected');
                            } else {
                                selectedSet.add(cardId);
                                cardEl.classList.add('selected');
                            }
                            if (confirmBtn) confirmBtn.disabled = selectedSet.size === 0;
                            updateStats();
                        });
                    });

                    updateStats();
                    updatePagination(totalPages);
                };

                const applyFilter = (value) => {
                    const term = (value || '').trim().toLowerCase();
                    if (!term) {
                        filteredEntries = [...cardEntries];
                    } else {
                        filteredEntries = cardEntries.filter(entry => entry.searchText.includes(term));
                    }
                    currentPage = 1;
                    renderList();
                };

                const init = async () => {
                    modalLoader?.show('正在加载卡牌列表...');
                    try {
                        const cards = await this.loadCardDefinitions();
                        cardEntries = Array.isArray(cards) ? cards : [];
                        applyFilter('');
                    } catch (error) {
                        console.error('EffectConfigEditor: failed to load cards', error);
                        if (listEl) {
                            listEl.innerHTML = `
                                <div class="error-message">加载卡牌失败: ${error.message}</div>
                            `;
                        }
                    } finally {
                        modalLoader?.hide();
                    }
                };

                if (searchInput) {
                    searchInput.addEventListener('input', (event) => {
                        applyFilter(event.target.value);
                    });
                }

                if (prevBtn) {
                    prevBtn.addEventListener('click', () => {
                        if (currentPage > 1) {
                            currentPage -= 1;
                            renderList();
                        }
                    });
                }

                if (nextBtn) {
                    nextBtn.addEventListener('click', () => {
                        const totalPages = Math.max(1, Math.ceil(filteredEntries.length / pageSize));
                        if (currentPage < totalPages) {
                            currentPage += 1;
                            renderList();
                        }
                    });
                }

                if (cancelBtn) {
                    cancelBtn.addEventListener('click', close);
                }

                if (confirmBtn) {
                    confirmBtn.addEventListener('click', () => {
                        const selected = Array.from(selectedSet).sort((a, b) => a - b);
                        finish(selected);
                    });
                }

                if (confirmBtn) {
                    confirmBtn.disabled = selectedSet.size === 0;
                }

                void init();
            });
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

        const rawParameters = effect?.parameters ?? effect?.Parameters ?? effect?.parameters;
        if (rawParameters && typeof rawParameters === 'object') {
            normalized.parameters = rawParameters;
        }

        const rawSelector = effect?.targetSelector ?? effect?.TargetSelector ?? effect?.targetselector;
        normalized.targetSelector = this.normalizeTargetSelector(rawSelector);

        const rawFailure = effect?.onFailure ?? effect?.OnFailure ?? effect?.onfailure;
        if (rawFailure != null && rawFailure !== '') {
            normalized.onFailure = rawFailure;
        }

        const rawChained = effect?.chainedAbilities ?? effect?.ChainedAbilities ?? effect?.chainedabilities;
        normalized.chainedAbilities = this.normalizeChainedAbilities(rawChained);
        delete normalized.ChainedAbilities;
        delete normalized.chainedabilities;

        normalized.repeatCount = this.normalizeRepeatCount(effect);
        normalized.repeatCondition = this.normalizeRepeatCondition(
            effect?.repeatCondition ?? effect?.RepeatCondition ?? effect?.repeatcondition
        );

        return normalized;
    }

    normalizeRepeatCondition(raw) {
        if (!raw) return null;
        if (typeof raw === 'string') {
            return {
                id: this.generateRepeatConditionId(),
                expression: raw,
                isEnabled: true
            };
        }
        if (Array.isArray(raw)) {
            const first = raw.find(Boolean);
            return this.normalizeRepeatCondition(first);
        }
        if (typeof raw === 'object') {
            return {
                id: raw.id ?? raw.Id ?? this.generateRepeatConditionId(),
                expression: raw.expression ?? raw.Expression ?? '',
                isEnabled: raw.isEnabled ?? raw.IsEnabled ?? true
            };
        }
        return null;
    }

    generateRepeatConditionId() {
        return `repeat_condition_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
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
        return raw ?? 'CompleteWithFailure';
    }

    normalizeRepeatCount(effect) {
        const raw = effect?.repeatCount ?? effect?.RepeatCount ?? effect?.repeatcount;
        if (raw == null || raw === '') {
            return 0;
        }

        const value = Number(raw);
        if (Number.isNaN(value)) {
            return 0;
        }

        return Math.max(0, Math.floor(value));
    }

    getEffectTypeName(effectMeta) {
        if (!effectMeta) return '';
        return effectMeta.effectTypeName || effectMeta.EffectTypeName || '';
    }

    isCardSourceModeRestrictedEffect(effectTypeName) {
        if (!effectTypeName) return false;
        const normalized = effectTypeName.toLowerCase();
        return normalized === 'gettoken' || normalized === 'adddeck';
    }

    getEffectParameterName(paramMeta) {
        return paramMeta?.propertyName || paramMeta?.PropertyName || paramMeta?.name || paramMeta?.Name || '';
    }

    hasEffectParameter(effectMeta, paramName) {
        if (!effectMeta?.parameters || !paramName) return false;
        const target = paramName.toLowerCase();
        return effectMeta.parameters.some(param => this.getEffectParameterName(param).toLowerCase() === target);
    }

    getParameterValueFromObject(parameters, paramName) {
        if (!parameters || !paramName) return undefined;
        if (Object.prototype.hasOwnProperty.call(parameters, paramName)) {
            return parameters[paramName];
        }
        const lower = paramName.toLowerCase();
        if (Object.prototype.hasOwnProperty.call(parameters, lower)) {
            return parameters[lower];
        }
        const camel = paramName.charAt(0).toLowerCase() + paramName.slice(1);
        if (Object.prototype.hasOwnProperty.call(parameters, camel)) {
            return parameters[camel];
        }
        return undefined;
    }

    getEffectParameterMetadata(effectMeta, paramName) {
        if (!effectMeta?.parameters || !paramName) return null;
        const target = paramName.toLowerCase();
        return effectMeta.parameters.find(param => this.getEffectParameterName(param).toLowerCase() === target) || null;
    }

    getEffectParameterValue(parameters, effectMeta, paramName) {
        const raw = this.getParameterValueFromObject(parameters, paramName);
        if (raw !== undefined) {
            const unwrapped = this.unwrapStaticValueConfig(raw);
            return unwrapped.ok ? unwrapped.value : raw;
        }
        const meta = this.getEffectParameterMetadata(effectMeta, paramName);
        if (!meta) return undefined;
        const fallback = meta.defaultValue ?? meta.DefaultValue;
        const unwrappedFallback = this.unwrapStaticValueConfig(fallback);
        return unwrappedFallback.ok ? unwrappedFallback.value : fallback;
    }

    normalizeBooleanValue(value) {
        if (value == null) return false;
        if (typeof value === 'boolean') return value;
        if (typeof value === 'number') return value !== 0;
        const raw = String(value).trim().toLowerCase();
        return raw === 'true' || raw === '1' || raw === 'yes';
    }

    normalizeCardSourceMode(value) {
        if (value == null || value === '') return 'Definition';
        if (typeof value === 'number') {
            return this.mapCardSourceModeValue(value);
        }
        const raw = String(value).trim();
        if (raw === '') return 'Definition';
        if (/^\d+$/.test(raw)) {
            return this.mapCardSourceModeValue(Number(raw));
        }
        const lowered = raw.toLowerCase();
        if (lowered === 'definition') return 'Definition';
        if (lowered === 'instance') return 'Instance';
        if (lowered === 'copy') return 'Copy';
        return raw;
    }

    mapCardSourceModeValue(value) {
        if (value === 1) return 'Instance';
        if (value === 2) return 'Copy';
        return 'Definition';
    }

    normalizeRemoveAbilityMode(value) {
        if (value == null || value === '') return 'Id';
        if (typeof value === 'number') {
            return this.mapRemoveAbilityModeValue(value);
        }
        const raw = String(value).trim();
        if (raw === '') return 'Id';
        if (/^\d+$/.test(raw)) {
            return this.mapRemoveAbilityModeValue(Number(raw));
        }
        const lowered = raw.toLowerCase();
        if (lowered === 'id') return 'Id';
        if (lowered === 'keyword') return 'Keyword';
        if (lowered === 'all') return 'All';
        return raw;
    }

    mapRemoveAbilityModeValue(value) {
        if (value === 1) return 'Keyword';
        if (value === 2) return 'All';
        return 'Id';
    }

    shouldRenderEffectParameter(paramMeta, context) {
        const paramName = this.getEffectParameterName(paramMeta);
        if (!paramName) return true;
        const normalized = paramName.toLowerCase();

        if (context?.hasSearchMode) {
            if (normalized === 'searchtargets') return !!context.useSearchMode;
            if (normalized === 'drawamount') return !context.useSearchMode;
        }

        if (context?.hasCardSourceMode) {
            const isDefinition = context.cardSourceMode === 'Definition';
            const isInstance = context.cardSourceMode === 'Instance';
            if (normalized === 'carddefinitionid') return isDefinition;
            if (normalized === 'amount' || normalized === 'summoncount') return !isInstance;
        }

        if (context?.hasRemoveMode) {
            if (normalized === 'abilitydefinitionids') return context.removeMode === 'Id';
            if (normalized === 'abilitykeyword') return context.removeMode === 'Keyword';
        }

        return true;
    }

    shouldRefreshParameterPanel(effectMeta, previousParams, nextParams) {
        if (!effectMeta?.parameters) return false;
        const hasSearchMode = this.hasEffectParameter(effectMeta, 'UseSearchMode');
        const hasCardSourceMode = this.hasEffectParameter(effectMeta, 'CardSourceMode');
        const hasRemoveMode = this.hasEffectParameter(effectMeta, 'RemoveMode');

        if (hasSearchMode) {
            const prevValue = this.normalizeBooleanValue(this.getEffectParameterValue(previousParams, effectMeta, 'UseSearchMode'));
            const nextValue = this.normalizeBooleanValue(this.getEffectParameterValue(nextParams, effectMeta, 'UseSearchMode'));
            if (prevValue !== nextValue) return true;
        }

        if (hasCardSourceMode) {
            const prevMode = this.normalizeCardSourceMode(this.getEffectParameterValue(previousParams, effectMeta, 'CardSourceMode'));
            const nextMode = this.normalizeCardSourceMode(this.getEffectParameterValue(nextParams, effectMeta, 'CardSourceMode'));
            if (prevMode !== nextMode) return true;
        }

        if (hasRemoveMode) {
            const prevMode = this.normalizeRemoveAbilityMode(this.getEffectParameterValue(previousParams, effectMeta, 'RemoveMode'));
            const nextMode = this.normalizeRemoveAbilityMode(this.getEffectParameterValue(nextParams, effectMeta, 'RemoveMode'));
            if (prevMode !== nextMode) return true;
        }

        // Refresh when a DSL-capable parameter switches between structured and DSL modes.
        for (const paramMeta of effectMeta.parameters) {
            const paramName = this.getEffectParameterName(paramMeta);
            if (!paramName) continue;
            const dslSupport = this.resolveDslSupport(paramMeta, paramMeta.typeName || paramMeta.TypeName || null);
            if (!dslSupport.allowDsl) continue;
            const prevValue = this.getParameterValueFromObject(previousParams, paramName);
            const nextValue = this.getParameterValueFromObject(nextParams, paramName);
            if (this.isFormulaValueConfig(prevValue) !== this.isFormulaValueConfig(nextValue)) {
                return true;
            }
        }

        return false;
    }

    normalizeChainedAbilities(value) {
        if (!Array.isArray(value)) {
            return [];
        }
        return value.filter(Boolean).map(ability => ({ ...ability }));
    }

    getChainedAbilityLockConfig() {
        return {
            eventTypes: ['EffectCompleted'],
            relationType: 'Any',
            timing: 'On',
            validZones: [],
            revalidateZoneBeforeExecution: false
        };
    }

    isDecisionAbilityEffect(effectTypeName) {
        const normalized = String(effectTypeName || '').trim().toLowerCase();
        return normalized === 'choose' || normalized === 'random';
    }

    getDecisionAbilityLockConfig(ability) {
        const fallback = {
            eventTypes: [],
            relationType: '',
            timing: 'On',
            validZones: [],
            revalidateZoneBeforeExecution: false
        };
        const defaultTrigger = this.options?.trigger ?? fallback;
        const rawTrigger = ability?.trigger ?? ability?.Trigger ?? null;
        const rawEventTypes = rawTrigger?.eventTypes ?? rawTrigger?.EventTypes ?? [];
        const rawValidZones = rawTrigger?.validZones ?? rawTrigger?.ValidZones ?? [];
        const hasTrigger = rawTrigger && typeof rawTrigger === 'object' && (
            (Array.isArray(rawEventTypes) && rawEventTypes.length > 0)
            || (rawTrigger.relationType ?? rawTrigger.RelationType)
            || (rawTrigger.timing ?? rawTrigger.Timing)
            || (Array.isArray(rawValidZones) && rawValidZones.length > 0)
            || rawTrigger.revalidateZoneBeforeExecution
            || rawTrigger.RevalidateZoneBeforeExecution
        );
        const source = hasTrigger ? rawTrigger : defaultTrigger;

        const eventTypes = source?.eventTypes ?? source?.EventTypes ?? fallback.eventTypes;
        const relationType = source?.relationType ?? source?.RelationType ?? fallback.relationType;
        const timing = source?.timing ?? source?.Timing ?? fallback.timing;
        const validZones = source?.validZones ?? source?.ValidZones ?? fallback.validZones;
        const revalidateZoneBeforeExecution =
            source?.revalidateZoneBeforeExecution ?? source?.RevalidateZoneBeforeExecution ?? false;

        return {
            eventTypes: Array.isArray(eventTypes) ? eventTypes : fallback.eventTypes,
            relationType,
            timing,
            validZones: Array.isArray(validZones) ? validZones : fallback.validZones,
            revalidateZoneBeforeExecution: !!revalidateZoneBeforeExecution
        };
    }

    openChainedAbilityModal(effectIndex) {
        if (!this.effects[effectIndex]) {
            return;
        }

        this.closeChainedAbilityModal();

        const overlay = document.createElement('div');
        overlay.className = 'modal-overlay chained-abilities-overlay';

        const modal = document.createElement('div');
        modal.className = 'modal-container chained-abilities-modal';

        const header = document.createElement('div');
        header.className = 'modal-header';
        header.innerHTML = `
            <h3>连锁能力配置</h3>
        `;

        const closeBtn = document.createElement('button');
        closeBtn.className = 'modal-close-btn';
        closeBtn.type = 'button';
        closeBtn.setAttribute('aria-label', '关闭');
        closeBtn.textContent = '×';
        header.appendChild(closeBtn);

        const content = document.createElement('div');
        content.className = 'modal-content chained-abilities-content';

        modal.appendChild(header);
        modal.appendChild(content);
        overlay.appendChild(modal);
        document.body.appendChild(overlay);

        const close = () => this.closeChainedAbilityModal();
        closeBtn.addEventListener('click', close);

        this.activeChainedModal = overlay;
        this.renderChainedAbilityListView(effectIndex, content);
    }

    closeChainedAbilityModal() {
        if (this.activeChainedModal) {
            this.activeChainedModal.remove();
            this.activeChainedModal = null;
        }
    }

    renderChainedAbilityListView(effectIndex, container) {
        this.clearContainer(container);

        const abilities = this.getChainedAbilities(effectIndex);

        const toolbar = document.createElement('div');
        toolbar.className = 'chained-abilities-toolbar';

        const hint = document.createElement('div');
        hint.className = 'text-muted';
        hint.textContent = '连锁能力会在效果结算后触发（固定为 EffectCompletedEvent）。';

        const addBtn = this.createButton('新增连锁能力', () => {
            this.renderChainedAbilityEditorView(effectIndex, null, container);
        }, 'primary');
        addBtn.classList.add('btn-small');

        toolbar.appendChild(hint);
        toolbar.appendChild(addBtn);
        container.appendChild(toolbar);

        const list = document.createElement('div');
        list.className = 'chained-abilities-list';

        if (abilities.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'empty-state';
            empty.innerHTML = `
                <h5 class="empty-title">暂无连锁能力</h5>
                <p class="empty-desc">点击上方按钮添加一个连锁能力。</p>
            `;
            list.appendChild(empty);
        } else {
            abilities.forEach((ability, index) => {
                const row = document.createElement('div');
                row.className = 'chained-ability-row';
                row.dataset.index = String(index);
                row.draggable = true;

                row.addEventListener('dragstart', (event) => {
                    event.dataTransfer?.setData('text/plain', String(index));
                    row.classList.add('is-dragging');
                });

                row.addEventListener('dragend', () => {
                    row.classList.remove('is-dragging');
                });

                row.addEventListener('dragover', (event) => {
                    event.preventDefault();
                    row.classList.add('is-drag-over');
                });

                row.addEventListener('dragleave', () => {
                    row.classList.remove('is-drag-over');
                });

                row.addEventListener('drop', (event) => {
                    event.preventDefault();
                    row.classList.remove('is-drag-over');
                    const fromIndex = Number(event.dataTransfer?.getData('text/plain'));
                    const toIndex = Number(row.dataset.index);
                    if (!Number.isNaN(fromIndex) && !Number.isNaN(toIndex)) {
                        this.moveChainedAbility(effectIndex, fromIndex, toIndex);
                        this.renderChainedAbilityListView(effectIndex, container);
                    }
                });

                const handle = document.createElement('span');
                handle.className = 'chained-ability-handle';
                handle.title = '拖动排序';
                handle.textContent = '::';

                const indexBadge = document.createElement('span');
                indexBadge.className = 'chained-ability-index';
                indexBadge.textContent = `#${index + 1}`;

                const main = document.createElement('div');
                main.className = 'chained-ability-main';

                const name = document.createElement('div');
                name.className = 'chained-ability-name';
                name.textContent = ability?.name || '未命名连锁能力';

                const meta = document.createElement('div');
                meta.className = 'chained-ability-meta';
                meta.textContent = this.formatChainedAbilitySummary(ability);

                main.appendChild(name);
                main.appendChild(meta);

                const actions = document.createElement('div');
                actions.className = 'chained-ability-actions';

                const editBtn = document.createElement('button');
                editBtn.type = 'button';
                editBtn.className = 'btn-secondary btn-small';
                editBtn.textContent = '编辑';
                editBtn.addEventListener('click', () => {
                    this.renderChainedAbilityEditorView(effectIndex, index, container);
                });

                const deleteBtn = document.createElement('button');
                deleteBtn.type = 'button';
                deleteBtn.className = 'btn-icon delete-btn';
                deleteBtn.title = '删除';
                deleteBtn.setAttribute('aria-label', '删除');
                deleteBtn.addEventListener('click', () => {
                    if (!confirm('确定删除此连锁能力？')) {
                        return;
                    }
                    const next = abilities.filter((_, i) => i !== index);
                    this.setChainedAbilities(effectIndex, next);
                    this.renderChainedAbilityListView(effectIndex, container);
                });

                actions.appendChild(editBtn);
                actions.appendChild(deleteBtn);

                row.appendChild(handle);
                row.appendChild(indexBadge);
                row.appendChild(main);
                row.appendChild(actions);
                list.appendChild(row);
            });
        }

        container.appendChild(list);
    }

    async renderChainedAbilityEditorView(effectIndex, abilityIndex, container) {
        this.clearContainer(container);

        const header = document.createElement('div');
        header.className = 'chained-abilities-editor-header';

        const backBtn = document.createElement('button');
        backBtn.type = 'button';
        backBtn.className = 'btn-secondary btn-small';
        backBtn.textContent = '返回列表';
        backBtn.addEventListener('click', () => {
            this.renderChainedAbilityListView(effectIndex, container);
        });

        const title = document.createElement('div');
        title.className = 'chained-abilities-editor-title';
        title.textContent = abilityIndex === null ? '新增连锁能力' : '编辑连锁能力';

        header.appendChild(backBtn);
        header.appendChild(title);
        container.appendChild(header);

        const editorHost = document.createElement('div');
        editorHost.className = 'chained-abilities-editor-host';
        container.appendChild(editorHost);

        const { AbilityEditor } = await import('./AbilityEditor.js');
        const abilityEditor = new AbilityEditor();
        const abilities = this.getChainedAbilities(effectIndex);
        const baseAbility = abilityIndex === null
            ? this.createDefaultChainedAbility(effectIndex)
            : this.cloneChainedAbility(abilities[abilityIndex], effectIndex);

        await abilityEditor.render(
            editorHost,
            baseAbility,
            (updatedConfig) => {
                if (updatedConfig === null) {
                    this.renderChainedAbilityListView(effectIndex, container);
                    return;
                }

                const nextAbility = this.buildChainedAbilityConfigFromEditor(updatedConfig);
                const next = abilityIndex === null
                    ? [...abilities, nextAbility]
                    : abilities.map((item, idx) => idx === abilityIndex ? nextAbility : item);

                this.setChainedAbilities(effectIndex, next);
                this.renderChainedAbilityListView(effectIndex, container);
            },
            {
                lockedTrigger: this.getChainedAbilityLockConfig(),
                lockedTriggerMessage: '连锁能力触发时机与有效区域已锁定，无法修改。',
                lockValidZones: true
            }
        );
    }

    getChainedAbilities(effectIndex) {
        const effect = this.effects[effectIndex];
        if (!effect) return [];
        const raw = effect.chainedAbilities ?? effect.ChainedAbilities ?? [];
        return Array.isArray(raw) ? raw : [];
    }

    setChainedAbilities(effectIndex, chainedAbilities) {
        this.updateEffect(effectIndex, { chainedAbilities });
    }

    moveChainedAbility(effectIndex, fromIndex, toIndex) {
        if (fromIndex === toIndex) return;
        const abilities = [...this.getChainedAbilities(effectIndex)];
        if (fromIndex < 0 || fromIndex >= abilities.length || toIndex < 0 || toIndex >= abilities.length) {
            return;
        }
        const [moved] = abilities.splice(fromIndex, 1);
        abilities.splice(toIndex, 0, moved);
        this.setChainedAbilities(effectIndex, abilities);
    }

    createDefaultChainedAbility(effectIndex) {
        const sequence = this.getChainedAbilities(effectIndex).length + 1;
        const trigger = this.getChainedAbilityLockConfig();
        return {
            name: `连锁能力${sequence}`,
            description: '',
            abilityKeywords: [],
            type: 'Triggered',
            trigger: { ...trigger },
            conditions: [],
            targets: [],
            effects: [],
            counterRegistrations: []
        };
    }

    cloneChainedAbility(ability, effectIndex = 0) {
        return ability ? JSON.parse(JSON.stringify(ability)) : this.createDefaultChainedAbility(effectIndex);
    }

    buildChainedAbilityConfigFromEditor(updatedConfig) {
        const rawConfig = updatedConfig?.jsonConfig ?? updatedConfig;
        let parsed = null;
        if (typeof rawConfig === 'string') {
            try {
                parsed = JSON.parse(rawConfig);
            } catch (error) {
                console.warn('EffectConfigEditor: failed to parse chained ability jsonConfig', error);
            }
        } else if (rawConfig && typeof rawConfig === 'object') {
            parsed = rawConfig;
        }

        const lockConfig = this.getChainedAbilityLockConfig();
        const next = {
            ...(parsed || {}),
            name: (parsed?.name ?? updatedConfig?.name ?? '连锁能力'),
            description: parsed?.description ?? updatedConfig?.description ?? '',
            type: parsed?.type || updatedConfig?.type || 'Triggered',
            abilityKeywords: Array.isArray(parsed?.abilityKeywords)
                ? parsed.abilityKeywords
                : (Array.isArray(updatedConfig?.abilityKeywords) ? updatedConfig.abilityKeywords : []),
            trigger: {
                ...(parsed?.trigger || updatedConfig?.trigger || {}),
                ...lockConfig,
                eventTypes: lockConfig.eventTypes
            },
            conditions: Array.isArray(parsed?.conditions)
                ? parsed.conditions
                : (Array.isArray(updatedConfig?.conditions) ? updatedConfig.conditions : []),
            targets: Array.isArray(parsed?.targets)
                ? parsed.targets
                : (Array.isArray(updatedConfig?.targets) ? updatedConfig.targets : []),
            effects: Array.isArray(parsed?.effects)
                ? parsed.effects
                : (Array.isArray(updatedConfig?.effects) ? updatedConfig.effects : []),
            counterRegistrations: Array.isArray(parsed?.counterRegistrations)
                ? parsed.counterRegistrations
                : (Array.isArray(updatedConfig?.counterRegistrations) ? updatedConfig.counterRegistrations : []),
            version: parsed?.version || '1.0'
        };

        return next;
    }

    formatChainedAbilitySummary(ability) {
        if (!ability) return '未配置内容';
        const parts = [];
        const effects = ability.effects ?? ability.Effects ?? [];
        const conditions = ability.conditions ?? ability.Conditions ?? [];
        const effectCount = Array.isArray(effects) ? effects.length : 0;
        const conditionCount = Array.isArray(conditions) ? conditions.length : 0;
        if (effectCount > 0) parts.push(`效果 ${effectCount}`);
        if (conditionCount > 0) parts.push(`条件 ${conditionCount}`);
        if (parts.length === 0) return '暂无内容';
        return parts.join(' · ');
    }

    // ... 辅助方法 (addEffect, removeEffect, updateEffect, etc.) ...
    addEffect() {
        this.effects.push({
            type: '',
            parameters: {},
            isEnabled: true,
            repeatCount: 0,
            repeatCondition: null,
            chainedAbilities: []
        });
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
        const previous = this.effects[index] || {};
        const next = { ...previous, ...updates };
        this.effects[index] = next;

        const effectMeta = this.getEffectMetadata(next.type || previous.type);
        const shouldRefresh = !!updates.type
            || (updates.parameters && this.shouldRefreshParameterPanel(effectMeta, previous.parameters || {}, next.parameters || {}));

        // 如果更新了类型或参数模式，需要重新渲染整个节点以更新标题和参数面板
        if (shouldRefresh) {
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
        if (!this.onChange) return;

        const sanitizedEffects = this.effects.map(effect => {
            const cleanParams = this.sanitizeParameters(effect.parameters);
            return { ...effect, parameters: cleanParams };
        });

        this.onChange(sanitizedEffects);
    }

    sanitizeParameters(parameters) {
        if (!parameters || typeof parameters !== 'object') return {};
        const result = {};
        for (const [key, value] of Object.entries(parameters)) {
            if (value === undefined) continue;
            const isPrimitive = value === null || typeof value !== 'object';
            const isArray = Array.isArray(value);
            const isValueConfig = !isArray && typeof value === 'object'
                && (Object.prototype.hasOwnProperty.call(value, '$type')
                    || Object.prototype.hasOwnProperty.call(value, 'value')
                    || Object.prototype.hasOwnProperty.call(value, 'Value')
                    || Object.prototype.hasOwnProperty.call(value, 'formula')
                    || Object.prototype.hasOwnProperty.call(value, 'Formula')
                    || Object.prototype.hasOwnProperty.call(value, '$type'));

            if (isPrimitive) {
                result[key] = this.toStaticValueConfig(value);
            } else if (isValueConfig) {
                result[key] = this.normalizeValueConfig(value);
            } else if (isArray) {
                result[key] = value;
            } else {
                result[key] = value; // 其他对象保持原样，避免破坏复杂参数
            }
        }
        return result;
    }

    normalizeValueConfig(value) {
        if (!value || typeof value !== 'object' || Array.isArray(value)) return value;

        const rawType = typeof value.$type === 'string' ? value.$type : '';
        const rawValue = value.Value ?? value.value;
        const rawFormula = value.Formula ?? value.formula;

        if (!rawType) {
            if (rawFormula !== undefined) {
                return { $type: 'FormulaValueConfig', Formula: rawFormula };
            }
            if (rawValue !== undefined) {
                return { $type: 'StaticValueConfig', Value: rawValue };
            }
            return value;
        }

        const normalized = { ...value };
        if (rawValue !== undefined && normalized.Value === undefined) {
            normalized.Value = rawValue;
        }
        if (rawFormula !== undefined && normalized.Formula === undefined) {
            normalized.Formula = rawFormula;
        }
        if (Object.prototype.hasOwnProperty.call(normalized, 'value')) {
            delete normalized.value;
        }
        if (Object.prototype.hasOwnProperty.call(normalized, 'formula')) {
            delete normalized.formula;
        }
        return normalized;
    }

    isParamValueMissing(value) {
        if (value === undefined || value === null) return true;
        if (typeof value === 'string') return value.trim() === '';
        return false;
    }

    isValueConfigEmpty(value) {
        if (value === undefined || value === null) return true;
        if (typeof value !== 'object') {
            return typeof value === 'string' ? value.trim() === '' : value === null;
        }

        const rawType = value.$type || value.Type || value.type;
        if (rawType === 'FormulaValueConfig') {
            const formula = (value.Formula ?? value.formula ?? '').toString().trim();
            return formula === '';
        }

        const rawValue = value.Value ?? value.value;
        if (rawValue === undefined || rawValue === null) return true;
        if (typeof rawValue === 'string') return rawValue.trim() === '';
        return false;
    }

    formatDefaultPlaceholder(value) {
        if (value === undefined) return undefined;
        if (value === null) return '（空）';
        if (typeof value === 'string') {
            const trimmed = value.trim();
            return trimmed === '' ? '（空）' : trimmed;
        }
        if (typeof value === 'boolean') {
            return value ? '是' : '否';
        }
        if (typeof value === 'number') {
            return Number.isNaN(value) ? '（空）' : String(value);
        }
        try {
            return JSON.stringify(value);
        } catch (e) {
            return String(value);
        }
    }

    resolveParameterDefault(paramMeta) {
        let fallback = paramMeta?.defaultValue ?? paramMeta?.DefaultValue;
        const controlKey = (paramMeta?.controlType || paramMeta?.ControlType || '').toString().toLowerCase();
        if (fallback === undefined && controlKey === 'valueconfigeditor') {
            fallback = null;
        }
        const unwrapped = this.unwrapStaticValueConfig(fallback);
        const display = unwrapped.ok ? unwrapped.value : fallback;
        let saveValue = controlKey === 'valueconfigeditor' ? fallback : display;
        if (controlKey === 'valueconfigeditor' && fallback === null) {
            saveValue = undefined;
        }
        return { raw: fallback, display, saveValue };
    }

    cloneDefaultValue(value) {
        if (!value || typeof value !== 'object') return value;
        try {
            return JSON.parse(JSON.stringify(value));
        } catch (e) {
            return { ...value };
        }
    }

    applyDefaultParameters(effect, effectMeta) {
        const rawParams = effect?.parameters && typeof effect.parameters === 'object'
            ? effect.parameters
            : {};
        if (!effectMeta?.parameters || effectMeta.parameters.length === 0) {
            return { ...effect, parameters: rawParams };
        }

        const parameters = { ...rawParams };
        effectMeta.parameters.forEach(paramMeta => {
            const paramName = this.getEffectParameterName(paramMeta);
            if (!paramName) return;
            const currentValue = this.getParameterValueFromObject(parameters, paramName);
            if (!this.isParamValueMissing(currentValue)) return;
            const { saveValue } = this.resolveParameterDefault(paramMeta);
            if (saveValue === undefined) return;
            parameters[paramName] = this.cloneDefaultValue(saveValue);
        });

        return { ...effect, parameters };
    }

    buildEffectsForSave(effects, metadataOverride = null) {
        if (!Array.isArray(effects)) return effects;
        const meta = metadataOverride || this.metadata;
        const effectList = meta?.effects || [];

        const getEffectMeta = (type) => effectList.find(item =>
            item?.effectTypeName === type || item?.EffectTypeName === type
        );

        return effects.map(effect => {
            if (!effect || typeof effect !== 'object') return effect;
            const effectMeta = getEffectMeta(effect.type);
            const withDefaults = this.applyDefaultParameters(effect, effectMeta);
            const cleanParams = this.sanitizeParameters(withDefaults.parameters);
            return { ...withDefaults, parameters: cleanParams };
        });
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

    resolveDslSupport(paramMeta, fallbackType) {
        const allowDsl = paramMeta?.allowDsl ?? paramMeta?.AllowDsl ?? true;
        const rawMode = paramMeta?.dslMode ?? paramMeta?.DslMode ?? 'Optional';
        const normalizedMode = this.normalizeDslMode(rawMode);
        const dslType = paramMeta?.dslType ?? paramMeta?.DslType ?? fallbackType ?? null;
        return {
            allowDsl: Boolean(allowDsl) && normalizedMode !== 'Disabled',
            dslMode: normalizedMode,
            dslType
        };
    }

    normalizeDslMode(mode) {
        if (!mode) return 'Optional';
        const text = String(mode).trim().toLowerCase();
        if (text === 'forced') return 'Forced';
        if (text === 'disabled') return 'Disabled';
        return 'Optional';
    }

    getParameterInputMode({ paramValue, dslSupport }) {
        if (!dslSupport || !dslSupport.allowDsl || dslSupport.dslMode === 'Disabled') {
            return 'structured';
        }
        if (dslSupport.dslMode === 'Forced') {
            return 'dsl';
        }
        if (this.isFormulaValueConfig(paramValue)) {
            return 'dsl';
        }
        return 'structured';
    }

    isFormulaValueConfig(value) {
        if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
        const rawType = value.$type || value.Type || value.type;
        if (rawType && String(rawType).toLowerCase() === 'formulavalueconfig') {
            return true;
        }
        if (value.Formula !== undefined || value.formula !== undefined) {
            return true;
        }
        return false;
    }

    isIntListType(typeName) {
        if (!typeName) return false;
        const text = String(typeName).toLowerCase();
        return text.includes('list<') && (text.includes('int32') || text.includes('int64') || text.includes('int'));
    }

    parseDelimitedIntList(text) {
        if (!text || typeof text !== 'string') return [];
        return text
            .split(/[,，\s]+/)
            .map(item => item.trim())
            .filter(Boolean)
            .map(item => parseInt(item, 10))
            .filter(num => !Number.isNaN(num));
    }

    getDslExpressionFromValueConfig(value) {
        if (!value || typeof value !== 'object') return null;
        return value.Formula ?? value.formula ?? null;
    }

    getParameterCacheKey(effectIndex, paramName) {
        const prefix = Number.isInteger(effectIndex) ? String(effectIndex) : 'global';
        return `${prefix}:${String(paramName || '').toLowerCase()}`;
    }

    getParameterCache(effectIndex, paramName) {
        const key = this.getParameterCacheKey(effectIndex, paramName);
        return this.parameterModeCache.get(key);
    }

    setParameterCache(effectIndex, paramName, next) {
        const key = this.getParameterCacheKey(effectIndex, paramName);
        this.parameterModeCache.set(key, next);
    }

    cacheParameterModeState(effectIndex, paramName, paramValue, fallbackValue) {
        if (!paramName) return;
        const cache = { ...(this.getParameterCache(effectIndex, paramName) || {}) };
        if (this.isFormulaValueConfig(paramValue)) {
            cache.dslExpression = this.getDslExpressionFromValueConfig(paramValue) ?? '';
        } else if (!this.isParamValueMissing(paramValue)) {
            const unwrapped = this.unwrapStaticValueConfig(paramValue);
            cache.structuredValue = unwrapped.ok ? unwrapped.value : paramValue;
        } else if (fallbackValue !== undefined && cache.structuredValue === undefined) {
            cache.structuredValue = this.cloneDefaultValue(fallbackValue);
        }
        this.setParameterCache(effectIndex, paramName, cache);
    }

    cacheDslExpression(effectIndex, paramName, expression) {
        if (!paramName) return;
        const cache = { ...(this.getParameterCache(effectIndex, paramName) || {}) };
        cache.dslExpression = expression ?? '';
        this.setParameterCache(effectIndex, paramName, cache);
    }

    cacheStructuredValue(effectIndex, paramName, value) {
        if (!paramName) return;
        const cache = { ...(this.getParameterCache(effectIndex, paramName) || {}) };
        cache.structuredValue = value;
        this.setParameterCache(effectIndex, paramName, cache);
    }

    getCachedDslExpression(effectIndex, paramName) {
        const cache = this.getParameterCache(effectIndex, paramName);
        return cache?.dslExpression;
    }

    switchParameterInputMode({ effectIndex, paramName, mode, paramValue, fallbackValue, updateParam }) {
        if (!paramName || typeof updateParam !== 'function') return;
        if (mode === 'dsl') {
            const cached = this.getCachedDslExpression(effectIndex, paramName);
            const formula = cached ?? this.getDslExpressionFromValueConfig(paramValue) ?? '';
            this.cacheDslExpression(effectIndex, paramName, formula);
            updateParam({
                $type: 'FormulaValueConfig',
                Formula: formula
            });
            return;
        }

        const cache = this.getParameterCache(effectIndex, paramName);
        let nextValue = cache?.structuredValue;
        if (nextValue === undefined && fallbackValue !== undefined) {
            nextValue = this.cloneDefaultValue(fallbackValue);
        }
        updateParam(nextValue);
    }

    renderDslExpressionControl(container, options) {
        const { expression, expectedType, onChange, triggeringEventContext } = options || {};
        const dslEditorContainer = document.createElement('div');
        dslEditorContainer.className = 'dsl-editor-wrapper ability-editor';

        const dslEditor = new DslEditor();
        dslEditor.render(
            dslEditorContainer,
            expression || '',
            (newFormula) => {
                dslEditorContainer.dataset.dslExpression = newFormula || '';
                if (typeof onChange === 'function') {
                    onChange(newFormula || '');
                }
            },
            {
                expectedType: expectedType || null,
                triggeringEventContext: triggeringEventContext || null
            }
        );
        dslEditorContainer._dslEditor = dslEditor;

        container.appendChild(dslEditorContainer);

        // removed help text to avoid layout collapse
    }

    renderIntListEditor(container, options = {}) {
        const { values, onChange } = options;
        let list = [];
        if (Array.isArray(values)) {
            list = values.filter(Number.isFinite);
        } else if (Number.isFinite(values)) {
            list = [Number(values)];
        } else if (typeof values === 'string') {
            list = this.parseDelimitedIntList(values);
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'int-list-editor';

        const inputRow = document.createElement('div');
        inputRow.className = 'int-list-input-row';

        const input = document.createElement('input');
        input.type = 'number';
        input.className = 'form-input int-list-input';
        input.placeholder = '输入ID并添加';

        const addBtn = this.createButton('添加', () => {
            const text = (input.value ?? '').toString().trim();
            if (!text) return;

            const items = this.parseDelimitedIntList(text);
            if (items.length === 0) {
                input.value = '';
                return;
            }

            list.push(...items);
            input.value = '';
            renderList();
            if (typeof onChange === 'function') {
                onChange([...list]);
            }
        }, 'secondary');
        addBtn.classList.add('btn-small');

        input.addEventListener('keydown', (event) => {
            if (event.key !== 'Enter') return;
            event.preventDefault();
            addBtn.click();
        });

        inputRow.appendChild(input);
        inputRow.appendChild(addBtn);

        const listWrap = document.createElement('div');
        listWrap.className = 'int-list-chips';

        const renderList = () => {
            listWrap.innerHTML = '';
            if (list.length === 0) {
                const empty = document.createElement('div');
                empty.className = 'text-muted';
                empty.textContent = '暂无列表项';
                listWrap.appendChild(empty);
                return;
            }

            list.forEach((value, index) => {
                const chip = document.createElement('div');
                chip.className = 'int-list-chip';

                const label = document.createElement('span');
                label.textContent = String(value);

                const remove = document.createElement('button');
                remove.type = 'button';
                remove.className = 'int-list-remove';
                remove.textContent = '×';
                remove.addEventListener('click', () => {
                    list.splice(index, 1);
                    renderList();
                    if (typeof onChange === 'function') {
                        onChange([...list]);
                    }
                });

                chip.appendChild(label);
                chip.appendChild(remove);
                listWrap.appendChild(chip);
            });
        };

        renderList();

        wrapper.appendChild(inputRow);
        wrapper.appendChild(listWrap);
        container.appendChild(wrapper);
    }

    createParameterHeader({ title, controlKey, allowDsl, dslMode, isDslMode, onModeChange }) {
        const header = document.createElement('div');
        header.className = 'parameter-header';

        const titleEl = document.createElement('span');
        titleEl.className = 'parameter-title';
        titleEl.textContent = title || '';
        header.appendChild(titleEl);

        const meta = document.createElement('div');
        meta.className = 'parameter-meta';

        const badgeText = this.getControlBadgeLabel(controlKey);
        if (badgeText) {
            const badge = document.createElement('span');
            badge.className = 'parameter-badge';
            badge.textContent = badgeText;
            meta.appendChild(badge);
        }

        if (allowDsl) {
            const toggle = document.createElement('div');
            toggle.className = 'parameter-mode-toggle';

            const structuredBtn = document.createElement('button');
            structuredBtn.type = 'button';
            structuredBtn.className = 'mode-btn';
            structuredBtn.textContent = '简化';
            structuredBtn.disabled = dslMode === 'Forced';
            if (!isDslMode) structuredBtn.classList.add('active');
            structuredBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                if (dslMode === 'Forced') return;
                if (typeof onModeChange === 'function') {
                    onModeChange('structured');
                }
            });

            const dslBtn = document.createElement('button');
            dslBtn.type = 'button';
            dslBtn.className = 'mode-btn';
            dslBtn.textContent = '高级';
            if (isDslMode) dslBtn.classList.add('active');
            dslBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                if (typeof onModeChange === 'function') {
                    onModeChange('dsl');
                }
            });

            toggle.appendChild(structuredBtn);
            toggle.appendChild(dslBtn);
            meta.appendChild(toggle);
        }

        header.appendChild(meta);
        return header;
    }

    getControlBadgeLabel(controlKey) {
        const key = String(controlKey || '').toLowerCase();
        switch (key) {
            case 'dropdown':
                return '枚举值';
            case 'checkbox':
                return '布尔';
            case 'numberstepper':
                return '整数';
            case 'textinput':
                return '文本';
            case 'textarea':
                return '文本';
            case 'abilitylisteditor':
                return '列表';
            case 'targetselector':
                return '目标';
            case 'valueconfigeditor':
                return '数值/表达式';
            default:
                return '';
        }
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
