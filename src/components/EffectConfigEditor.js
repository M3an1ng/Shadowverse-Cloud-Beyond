// src/components/EffectConfigEditor.js - 效果配置编辑器 (Refactored)
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

import { mergeInheritedTriggerFields, isDecisionAbilityEffect } from './grantedAbilityConfig.js';
import { GrantedAbilityListEditor } from './GrantedAbilityListEditor.js';
import { normalizeDurationType } from './buffDuration.js';
import { unwrapStaticValueConfig } from './staticValueConfig.js';
import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';
import { ValueConfigEditor } from './ValueConfigEditor.js';
import { DslEditor } from './DslEditor.js';
import { getBlockingLoader } from './blockingLoader.js';
import { DefinitionPickerCatalog } from './definitionPickerCatalog.js';
import { DefinitionPicker } from './DefinitionPicker.js';
import { ensureEnumLocalization } from '../enumLocalizationCache.js';
import { validateDslExpression } from '../dslValidation.js';
import { getSemanticNodeType as getCoreSemanticNodeType } from './dsl_editor/core.js';

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
        this.parameterModeCache = new Map();
        this.definitionPickerCatalog = new DefinitionPickerCatalog();
        this.definitionPicker = new DefinitionPicker(this.definitionPickerCatalog);
        this.grantedAbilityListEditor = new GrantedAbilityListEditor(this.definitionPicker, () => this.options);
        this.outputEditorsByEffect = new Map();
        this.replaceEventParamMetadataCache = new Map();
        this.replaceEventParamMetadataRequests = new Map();
    }

    async render(container, effects = [], onChange = () => {}, options = {}) {
        await this.initialize();
        
        this.container = container;
        this.onChange = onChange;
        this.options = options || {};
        this.effects = this.synchronizeDecisionBranchTriggers(
            (effects || []).map(effect => this.normalizeEffectConfig(effect)),
            this.options?.trigger
        );
        this.outputEditorsByEffect.clear();
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

        const toggle = this.createAccordionToggleSwitch(
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
            { compact: true, ariaLabel: '启用效果' }
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
        
        const effectiveParameters = this.getEffectiveEffectParameterMetadata(effectMeta, parameters, effectIndex);
        if (effectiveParameters.length === 0) {
            container.innerHTML = '<div class="text-muted">此效果无需额外参数</div>';
            return container;
        }

        container.innerHTML = '';
        const grid = document.createElement('div');
        grid.className = 'form-grid-layout';

        const hasSearchMode = this.hasEffectParameter(effectMeta, 'UseSearchMode');
        const hasCardSourceMode = this.hasEffectParameter(effectMeta, 'CardSourceMode');
        const hasRemoveMode = this.hasEffectParameter(effectMeta, 'RemoveMode');
        const hasDurationType = this.hasEffectParameter(effectMeta, 'DurationType');
        const hasModifierLayer = this.hasEffectParameter(effectMeta, 'ModifierLayer');
        const hasTriggerAbilityMode = this.hasEffectParameter(effectMeta, 'TriggerMode');
        const useSearchMode = hasSearchMode
            ? this.normalizeBooleanValue(this.getEffectParameterValue(parameters, effectMeta, 'UseSearchMode'))
            : false;
        const cardSourceMode = hasCardSourceMode
            ? this.normalizeCardSourceMode(this.getEffectParameterValue(parameters, effectMeta, 'CardSourceMode'))
            : null;
        const removeMode = hasRemoveMode
            ? this.normalizeRemoveAbilityMode(this.getEffectParameterValue(parameters, effectMeta, 'RemoveMode'))
            : null;
        const durationType = hasDurationType
            ? normalizeDurationType(this.getEffectParameterValue(parameters, effectMeta, 'DurationType'))
            : null;
        const modifierLayer = hasModifierLayer
            ? this.normalizeModifierLayer(this.getEffectParameterValue(parameters, effectMeta, 'ModifierLayer'))
            : null;
        const triggerAbilityMode = hasTriggerAbilityMode
            ? this.normalizeTriggerAbilityMode(this.getEffectParameterValue(parameters, effectMeta, 'TriggerMode'))
            : null;
        const renderContext = {
            hasSearchMode,
            useSearchMode,
            hasCardSourceMode,
            cardSourceMode,
            hasRemoveMode,
            removeMode,
            hasDurationType,
            durationType,
            hasModifierLayer,
            modifierLayer,
            hasTriggerAbilityMode,
            triggerAbilityMode
        };

        effectiveParameters.forEach(paramMeta => {
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

                if (this.isReplaceEventEffectMeta(effectMeta) && this.isReplacementEventTypeParameter(paramName)) {
                    Object.keys(newParams).forEach((key) => {
                        if (this.isReplacementParamsParameter(key)) {
                            delete newParams[key];
                        }
                    });
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
        if (renderContext.modifierLayer === 'Multiplicative') {
            const multiplierHint = document.createElement('div');
            multiplierHint.className = 'text-muted multiplicative-mode-hint';
            multiplierHint.textContent = '当前为倍乘模式：参数按百分比解释（50 表示 0.5 倍，150 表示 1.5 倍，100 表示不变）。';
            container.appendChild(multiplierHint);
        }
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

        const outputsSection = this.createOutputsEditor(effect, index);
        container.appendChild(outputsSection);

        const failureSection = this.createFailureHandlingEditor(effect, index);
        container.appendChild(failureSection);

        const repeatSection = this.createRepeatCountEditor(effect, index);
        container.appendChild(repeatSection);

        const repeatConditionSection = this.createRepeatConditionEditor(effect, index);
        container.appendChild(repeatConditionSection);

        return container;
    }

    createOutputsEditor(effect, index) {
        const container = document.createElement('div');
        container.className = 'config-section';
        container.innerHTML = '<h5 class="section-title">输出变量</h5>';

        const desc = document.createElement('div');
        desc.className = 'text-muted';
        desc.textContent = '将当前效果结果保存为可复用变量，供后续连锁通过 context.triggeringEvent.Outputs.<变量名> 访问。';
        container.appendChild(desc);

        const list = document.createElement('div');
        list.className = 'effect-output-list';

        const warning = document.createElement('div');
        warning.className = 'output-duplicate-warning hidden';
        warning.textContent = '存在重复变量名，保存时将按最后一条同名项覆盖。';

        const editorPanel = document.createElement('div');
        editorPanel.className = 'output-dsl-editor-host hidden';
        const editorHeader = document.createElement('div');
        editorHeader.className = 'output-dsl-editor-title';
        const editorTitleText = document.createElement('span');
        editorTitleText.className = 'output-dsl-editor-title-text';
        editorHeader.appendChild(editorTitleText);
        const collapseBtn = this.createButton('折叠', () => {
            state.editorExpanded = false;
            renderRows();
            renderDslEditor();
        }, 'secondary');
        collapseBtn.classList.add('btn-small', 'output-dsl-editor-collapse');
        editorHeader.appendChild(collapseBtn);
        const editorBody = document.createElement('div');
        editorBody.className = 'dsl-editor-wrapper';
        editorPanel.appendChild(editorHeader);
        editorPanel.appendChild(editorBody);

        const createEntry = (name = '', formula = '', typeName = null) => ({
            id: `output_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
            name: String(name || ''),
            formula: String(formula || ''),
            typeName: typeName ? String(typeName) : null
        });

        const currentOutputs = this.normalizeEffectOutputs(effect?.outputs ?? effect?.Outputs);
        const entries = Object.entries(currentOutputs);
        const state = {
            rows: entries.length > 0
                ? entries.map(([name, formula]) => createEntry(name, formula))
                : [],
            activeRowId: null,
            editorExpanded: false,
            editorLoaded: false,
            dslEditorInstance: null
        };

        const getActiveRow = () => state.rows.find(row => row.id === state.activeRowId) || null;
        const getOutputTypeLabel = (entry) => {
            const rawType = String(entry?.typeName || '').trim();
            if (!rawType) return '类型: Void';
            const display = (state.dslEditorInstance && typeof state.dslEditorInstance.getDisplayTypeName === 'function')
                ? state.dslEditorInstance.getDisplayTypeName(rawType)
                : rawType;
            return `类型: ${display || rawType}`;
        };
        const getTriggeringEventContext = () => this.getDslTriggeringEventContext();
        const syncActiveRowTypeFromEditor = () => {
            const active = getActiveRow();
            if (!active || !state.dslEditorInstance) return;

            let inferred = null;
            const root = state.dslEditorInstance.astRoot || state.dslEditorInstance.virtualRoot?.mainExpression || null;
            if (root && typeof state.dslEditorInstance.getSemanticNodeType === 'function') {
                inferred = state.dslEditorInstance.getSemanticNodeType(root);
            }
            if (!inferred && root?.returnType) {
                inferred = root.returnType;
            }
            if (inferred && typeof state.dslEditorInstance.normalizeTypeName === 'function') {
                inferred = state.dslEditorInstance.normalizeTypeName(inferred);
            }
            active.typeName = inferred || null;
        };
        const mountEditorPanelToActiveRow = () => {
            const activeId = state.activeRowId;
            if (state.editorExpanded && activeId) {
                const activeSlot = list.querySelector(`.output-row-editor-slot[data-row-id="${activeId}"]`);
                if (activeSlot && editorPanel.parentElement !== activeSlot) {
                    activeSlot.appendChild(editorPanel);
                }
                editorPanel.classList.remove('hidden');
                return;
            }

            editorPanel.classList.add('hidden');
            if (editorPanel.parentElement !== container) {
                container.appendChild(editorPanel);
            }
        };

        const commitOutputs = () => {
            const normalized = {};
            const keyCounter = new Map();
            state.rows.forEach((row) => {
                const key = String(row.name || '').trim();
                const formula = String(row.formula || '');

                if (!key) return;

                const lowerKey = key.toLowerCase();
                keyCounter.set(lowerKey, (keyCounter.get(lowerKey) || 0) + 1);
                normalized[key] = formula;
            });

            const hasDuplicate = Array.from(keyCounter.values()).some(count => count > 1);
            warning.classList.toggle('hidden', !hasDuplicate);
            this.updateEffect(index, { outputs: normalized });
        };

        const renderDslEditor = () => {
            mountEditorPanelToActiveRow();
            editorBody.innerHTML = '';
            if (!state.editorExpanded) return;

            const active = getActiveRow();
            if (!active) {
                editorTitleText.textContent = '未选择输出变量';
                const empty = document.createElement('div');
                empty.className = 'text-muted';
                empty.textContent = '请选择一条输出变量后编辑公式。';
                editorBody.appendChild(empty);
                return;
            }

            const title = String(active.name || '').trim();
            editorTitleText.textContent = title
                ? `公式编辑：${title}`
                : '公式编辑：未命名变量';

            const dslContainer = document.createElement('div');
            dslContainer.className = 'dsl-editor-wrapper ability-editor';
            editorBody.appendChild(dslContainer);

            if (!state.editorLoaded) {
                state.editorLoaded = true;
            }
            if (!state.dslEditorInstance) {
                state.dslEditorInstance = new DslEditor();
            }

            state.dslEditorInstance.render(
                dslContainer,
                active.formula || '',
                (newFormula) => {
                    const row = getActiveRow();
                    if (!row) return;
                    row.formula = newFormula || '';
                    syncActiveRowTypeFromEditor();
                    commitOutputs();
                    refreshRowView(row.id);
                },
                {
                    expectedType: null,
                    triggeringEventContext: getTriggeringEventContext()
                }
            ).then(() => {
                syncActiveRowTypeFromEditor();
                renderRows();
                mountEditorPanelToActiveRow();
            }).catch((error) => {
                dslContainer.innerHTML = `<div class="error-message">DSL编辑器加载失败: ${error.message}</div>`;
            });
        };

        const renderRows = () => {
            list.innerHTML = '';
            const keyCounter = new Map();
            state.rows.forEach((row) => {
                const key = String(row.name || '').trim().toLowerCase();
                if (!key) return;
                keyCounter.set(key, (keyCounter.get(key) || 0) + 1);
            });

            state.rows.forEach((row) => {
                const key = String(row.name || '').trim().toLowerCase();
                const isDuplicate = key && (keyCounter.get(key) || 0) > 1;
                list.appendChild(createRow(row, isDuplicate));
            });
        };

        const refreshRowView = (rowId) => {
            if (!rowId) return;
            const rowData = state.rows.find(item => item.id === rowId);
            if (!rowData) return;

            const rowBlock = list.querySelector(`.effect-output-row-block[data-row-id="${rowId}"]`);
            if (!rowBlock) return;

            const previewInput = rowBlock.querySelector('.output-formula-preview');
            if (previewInput) {
                const hasFormula = Boolean(rowData.formula && rowData.formula.trim());
                previewInput.value = hasFormula ? rowData.formula : '';
            }

            const typeBadge = rowBlock.querySelector('.output-type-badge');
            if (typeBadge) {
                const label = getOutputTypeLabel(rowData);
                typeBadge.textContent = label;
                typeBadge.title = label;
            }
        };

        const setActiveRow = (rowId) => {
            state.activeRowId = rowId;
            renderRows();
            if (state.editorExpanded) {
                renderDslEditor();
            }
        };

        const toggleDslEditorForRow = (rowId) => {
            if (state.editorExpanded && state.activeRowId === rowId) {
                state.editorExpanded = false;
                renderRows();
                renderDslEditor();
                return;
            }
            state.activeRowId = rowId;
            state.editorExpanded = true;
            renderRows();
            renderDslEditor();
        };

        const createRow = (entry, isDuplicate = false) => {
            const rowBlock = document.createElement('div');
            rowBlock.className = 'effect-output-row-block';
            rowBlock.dataset.rowId = entry.id;

            const rowElement = document.createElement('div');
            rowElement.className = 'effect-output-row';
            if (state.editorExpanded && entry.id === state.activeRowId) {
                rowElement.classList.add('active');
            }
            if (isDuplicate) {
                rowElement.classList.add('duplicate-key');
            }

            const nameCell = document.createElement('div');
            nameCell.className = 'output-name-cell';
            const nameInput = document.createElement('input');
            nameInput.type = 'text';
            nameInput.className = 'form-input output-name-input';
            nameInput.placeholder = '（变量名）';
            nameInput.value = entry.name;
            nameInput.addEventListener('click', (event) => event.stopPropagation());
            nameInput.addEventListener('focus', () => {
                state.activeRowId = entry.id;
                if (state.editorExpanded) {
                    renderDslEditor();
                }
            });
            nameInput.addEventListener('input', (event) => {
                entry.name = event.target.value || '';
                commitOutputs();
            });
            nameInput.addEventListener('change', () => {
                renderRows();
                renderDslEditor();
            });
            const typeBadge = document.createElement('span');
            typeBadge.className = 'output-type-badge';
            typeBadge.textContent = getOutputTypeLabel(entry);
            typeBadge.title = typeBadge.textContent;
            nameCell.appendChild(nameInput);
            nameCell.appendChild(typeBadge);

            const preview = document.createElement('input');
            preview.type = 'text';
            preview.readOnly = true;
            preview.className = 'form-input output-formula-preview clickable';
            const hasFormula = Boolean(entry.formula && entry.formula.trim());
            preview.value = hasFormula ? entry.formula : '';
            preview.placeholder = '（未输入表达式）';
            preview.title = '点击展开/折叠该变量的DSL编辑器';
            preview.addEventListener('click', (event) => {
                event.stopPropagation();
                toggleDslEditorForRow(entry.id);
            });

            const removeBtn = this.createButton('删除', () => {
                state.rows = state.rows.filter(item => item.id !== entry.id);
                if (!state.rows.some(item => item.id === state.activeRowId)) {
                    state.activeRowId = null;
                    state.editorExpanded = false;
                }
                commitOutputs();
                renderRows();
                renderDslEditor();
            }, 'danger');
            removeBtn.classList.add('btn-small', 'output-remove-btn');
            removeBtn.addEventListener('click', (event) => event.stopPropagation());

            rowElement.addEventListener('click', () => setActiveRow(entry.id));

            rowElement.appendChild(nameCell);
            rowElement.appendChild(preview);
            rowElement.appendChild(removeBtn);

            const editorSlot = document.createElement('div');
            editorSlot.className = 'output-row-editor-slot';
            editorSlot.dataset.rowId = entry.id;

            rowBlock.appendChild(rowElement);
            rowBlock.appendChild(editorSlot);
            return rowBlock;
        };

        const actions = document.createElement('div');
        actions.className = 'output-actions';
        const addBtn = this.createButton('添加变量', () => {
            const next = createEntry('', '');
            state.rows.push(next);
            commitOutputs();
            setActiveRow(next.id);
            state.editorExpanded = true;
            renderDslEditor();
        }, 'secondary');
        addBtn.classList.add('btn-small');
        actions.appendChild(addBtn);

        renderRows();
        renderDslEditor();

        this.outputEditorsByEffect.set(index, {
            flushDraftToEffect: () => {
                const active = getActiveRow();
                const editor = state.dslEditorInstance;
                if (!active || !editor) {
                    return { flushed: false, reason: 'no-active-editor' };
                }

                if (typeof editor.validateExpressionForCompletion === 'function') {
                    const validation = editor.validateExpressionForCompletion();
                    if (validation && validation.isValid === false) {
                        return { flushed: false, reason: 'incomplete-expression' };
                    }
                }

                // Ensure workspace draft is committed back to main expression before reading.
                if (typeof editor.saveWorkspace === 'function') {
                    editor.saveWorkspace();
                } else if (typeof editor.updateExpressionFromAST === 'function') {
                    editor.updateExpressionFromAST({ skipRender: true });
                } else if (typeof editor.computeCurrentExpression === 'function') {
                    const nextExpr = String(editor.computeCurrentExpression() || '');
                    if (nextExpr !== active.formula) {
                        active.formula = nextExpr;
                    }
                }

                if (typeof editor.computeCurrentExpression === 'function') {
                    const nextExpr = String(editor.computeCurrentExpression() || '');
                    if (nextExpr !== active.formula) {
                        active.formula = nextExpr;
                    }
                }

                syncActiveRowTypeFromEditor();
                commitOutputs();
                refreshRowView(active.id);
                return { flushed: true };
            },
            getRowsSnapshot: () => state.rows.map((row) => ({
                name: String(row?.name || '').trim(),
                formula: String(row?.formula || ''),
                typeName: row?.typeName ? String(row.typeName) : null
            }))
        });

        container.appendChild(list);
        container.appendChild(warning);
        container.appendChild(editorPanel);
        container.appendChild(actions);
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
        const cacheParamName = '__repeat_count';
        const getLatestEffect = () => this.effects?.[index] || effect || {};
        const getLatestRepeatCount = () => this.normalizeRepeatCount(getLatestEffect());
        const getLatestRepeatCountValue = () => this.normalizeRepeatCountValue(getLatestEffect());

        const initialRepeatCount = getLatestRepeatCount();
        const initialRepeatCountValue = getLatestRepeatCountValue();
        this.cacheParameterModeState(
            index,
            cacheParamName,
            initialRepeatCountValue || this.toStaticValueConfig(initialRepeatCount),
            initialRepeatCount
        );

        let isDslMode = this.isFormulaValueConfig(initialRepeatCountValue);

        const createModeToggleRow = () => {
            const row = document.createElement('div');
            row.className = 'repeat-count-mode-row';

            const badge = document.createElement('span');
            badge.className = 'parameter-badge';
            badge.textContent = this.getControlBadgeLabel('numberstepper') || '整数';
            row.appendChild(badge);

            const toggle = document.createElement('div');
            toggle.className = 'parameter-mode-toggle';

            const structuredBtn = document.createElement('button');
            structuredBtn.type = 'button';
            structuredBtn.className = 'mode-btn';
            structuredBtn.textContent = '简化';
            if (!isDslMode) structuredBtn.classList.add('active');
            structuredBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                const cache = this.getParameterCache(index, cacheParamName);
                const cachedStructured = cache?.structuredValue;
                const fallback = getLatestRepeatCount();
                const next = Number.isFinite(cachedStructured)
                    ? Number(cachedStructured)
                    : fallback;
                const safeValue = Math.max(0, Math.floor(next));
                this.cacheStructuredValue(index, cacheParamName, safeValue);
                this.updateEffect(index, {
                    repeatCount: safeValue,
                    repeatCountValue: null
                });
                isDslMode = false;
                renderSection();
            });

            const dslBtn = document.createElement('button');
            dslBtn.type = 'button';
            dslBtn.className = 'mode-btn';
            dslBtn.textContent = '高级';
            if (isDslMode) dslBtn.classList.add('active');
            dslBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                const latestValueConfig = getLatestRepeatCountValue();
                const formula = this.getCachedDslExpression(index, cacheParamName)
                    ?? this.getDslExpressionFromValueConfig(latestValueConfig)
                    ?? '';
                this.cacheDslExpression(index, cacheParamName, formula);
                this.updateEffect(index, {
                    repeatCountValue: {
                        $type: 'FormulaValueConfig',
                        Formula: formula
                    }
                });
                isDslMode = true;
                renderSection();
            });

            toggle.appendChild(structuredBtn);
            toggle.appendChild(dslBtn);
            row.appendChild(toggle);
            return row;
        };

        const renderControlBody = (host) => {
            host.innerHTML = '';

            if (isDslMode) {
                const latestValueConfig = getLatestRepeatCountValue();
                const formula = this.getDslExpressionFromValueConfig(latestValueConfig)
                    ?? this.getCachedDslExpression(index, cacheParamName)
                    ?? '';

                this.renderDslExpressionControl(host, {
                    expression: formula,
                    expectedType: 'Int32',
                    onChange: (newFormula) => {
                        const normalizedFormula = newFormula || '';
                        this.cacheDslExpression(index, cacheParamName, normalizedFormula);
                        this.updateEffect(index, {
                            repeatCountValue: {
                                $type: 'FormulaValueConfig',
                                Formula: normalizedFormula
                            }
                        });
                    },
                    triggeringEventContext: this.getDslTriggeringEventContext()
                });
                return;
            }

            const current = getLatestRepeatCount();
            const input = this.createNumberInput(
                current,
                (val) => {
                    const numeric = Number.isFinite(val) ? Number(val) : 0;
                    const safeValue = Math.max(0, Math.floor(numeric));
                    this.cacheStructuredValue(index, cacheParamName, safeValue);
                    this.updateEffect(index, {
                        repeatCount: safeValue,
                        repeatCountValue: null
                    });
                },
                { min: 0, step: 1 }
            );
            input.className = 'form-input';
            host.appendChild(input);
        };

        const renderSection = () => {
            container.innerHTML = '';

            const title = document.createElement('h5');
            title.className = 'section-title';
            title.textContent = '重复次数';

            const body = document.createElement('div');
            body.className = 'repeat-count-control-body';
            renderControlBody(body);

            container.appendChild(title);
            container.appendChild(createModeToggleRow());
            container.appendChild(body);
        };

        renderSection();
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

        const toggle = this.createAccordionToggleSwitch(
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
                triggeringEventContext: this.getDslTriggeringEventContext()
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
        const unwrapped = unwrapStaticValueConfig(paramValue);
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
                triggeringEventContext: this.getDslTriggeringEventContext()
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
                    triggeringEventContext: this.getDslTriggeringEventContext()
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
                this.grantedAbilityListEditor.render(container, paramValue, (val) => {
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
                const selected = await this.definitionPicker.openAbilities(list);
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
                    const entry = this.definitionPickerCatalog.getAbility(value);
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
            void this.definitionPickerCatalog.loadAbilities().then(() => {
                renderList();
            }).catch(error => {
                console.error('EffectConfigEditor: failed to load ability definitions', error);
                this.showError(`加载能力失败: ${error.message}`, listWrap);
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
                const selected = await this.definitionPicker.openCards(list, { allowMultiple });
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
                    const entry = this.definitionPickerCatalog.getCard(value);
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
            void this.definitionPickerCatalog.loadCards().then(() => {
                renderList();
            }).catch(error => {
                console.error('EffectConfigEditor: failed to load card definitions', error);
                this.showError(`加载卡牌失败: ${error.message}`, listWrap);
            });

            wrapper.appendChild(inputRow);
            wrapper.appendChild(listWrap);
            container.appendChild(wrapper);
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
        const rawOutputs = effect?.outputs ?? effect?.Outputs ?? effect?.output;
        normalized.outputs = this.normalizeEffectOutputs(rawOutputs);
        delete normalized.Outputs;
        delete normalized.output;

        const rawFailure = effect?.onFailure ?? effect?.OnFailure ?? effect?.onfailure;
        if (rawFailure != null && rawFailure !== '') {
            normalized.onFailure = rawFailure;
        }

        const rawChained = effect?.chainedAbilities ?? effect?.ChainedAbilities ?? effect?.chainedabilities;
        normalized.chainedAbilities = this.normalizeChainedAbilities(rawChained);
        delete normalized.ChainedAbilities;
        delete normalized.chainedabilities;

        normalized.repeatCount = this.normalizeRepeatCount(effect);
        normalized.repeatCountValue = this.normalizeRepeatCountValue(effect);
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
        const unwrapped = unwrapStaticValueConfig(raw);
        const candidate = unwrapped.ok ? unwrapped.value : raw;

        if (candidate == null || candidate === '') {
            return 0;
        }

        const value = Number(candidate);
        if (Number.isNaN(value)) {
            return 0;
        }

        return Math.max(0, Math.floor(value));
    }

    normalizeRepeatCountValue(effect) {
        const raw = effect?.repeatCountValue ?? effect?.RepeatCountValue ?? effect?.repeatcountvalue;
        if (raw == null || raw === '') {
            return null;
        }

        if (typeof raw === 'string') {
            const formula = raw.trim();
            if (!formula) return null;
            return {
                $type: 'FormulaValueConfig',
                Formula: formula
            };
        }

        if (typeof raw !== 'object' || Array.isArray(raw)) {
            return null;
        }

        return this.normalizeValueConfig(raw);
    }

    getEffectTypeName(effectMeta) {
        if (!effectMeta) return '';
        return effectMeta.effectTypeName || effectMeta.EffectTypeName || '';
    }

    isCardSourceModeRestrictedEffect(effectTypeName) {
        if (!effectTypeName) return false;
        const normalized = effectTypeName.toLowerCase();
        return normalized === 'gettoken' || normalized === 'adddeck' || normalized === 'transform';
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
            const unwrapped = unwrapStaticValueConfig(raw);
            return unwrapped.ok ? unwrapped.value : raw;
        }
        const meta = this.getEffectParameterMetadata(effectMeta, paramName);
        if (!meta) return undefined;
        const fallback = meta.defaultValue ?? meta.DefaultValue;
        const unwrappedFallback = unwrapStaticValueConfig(fallback);
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

    normalizeTriggerAbilityMode(value) {
        if (value == null || value === '') return 'ByKeyword';
        if (typeof value === 'number') {
            return this.mapTriggerAbilityModeValue(value);
        }
        const raw = String(value).trim();
        if (raw === '') return 'ByKeyword';
        if (/^\d+$/.test(raw)) {
            return this.mapTriggerAbilityModeValue(Number(raw));
        }
        const lowered = raw.toLowerCase();
        if (lowered === 'bykeyword') return 'ByKeyword';
        if (lowered === 'byabilitydefinitionid') return 'ByAbilityDefinitionId';
        if (lowered === 'byeventtype') return 'ByEventType';
        return raw;
    }

    mapTriggerAbilityModeValue(value) {
        if (value === 1) return 'ByAbilityDefinitionId';
        if (value === 2) return 'ByEventType';
        return 'ByKeyword';
    }

    normalizeModifierLayer(value) {
        if (value == null || value === '') return 'Additive';
        if (typeof value === 'number') {
            return this.mapModifierLayerValue(value);
        }
        const raw = String(value).trim();
        if (raw === '') return 'Additive';
        if (/^\d+$/.test(raw)) {
            return this.mapModifierLayerValue(Number(raw));
        }
        const lowered = raw.toLowerCase();
        if (lowered === 'base') return 'Base';
        if (lowered === 'additive') return 'Additive';
        if (lowered === 'multiplicative') return 'Multiplicative';
        if (lowered === 'set') return 'Set';
        return raw;
    }

    mapModifierLayerValue(value) {
        if (value === 0) return 'Base';
        if (value === 1) return 'Additive';
        if (value === 2) return 'Multiplicative';
        if (value === 3) return 'Set';
        return 'Additive';
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
            const isCopy = context.cardSourceMode === 'Copy';
            if (normalized === 'carddefinitionid' || normalized === 'targetcarddefinitionid') return isDefinition;
            if (normalized === 'copysourcetargets') return isCopy;
            if (normalized === 'amount' || normalized === 'summoncount') return !isInstance;
        }

        if (context?.hasRemoveMode) {
            if (normalized === 'abilitydefinitionids') return context.removeMode === 'Id';
            if (normalized === 'abilitykeyword') return context.removeMode === 'Keyword';
        }

        if (context?.hasDurationType) {
            if (normalized === 'durationparameter') return context.durationType !== 'ConditionalRemoval';
            if (normalized === 'removecondition') return context.durationType === 'ConditionalRemoval';
        }

        if (context?.hasTriggerAbilityMode) {
            if (normalized === 'abilitykeyword') return context.triggerAbilityMode === 'ByKeyword';
            if (normalized === 'abilitydefinitionids') return context.triggerAbilityMode === 'ByAbilityDefinitionId';
            if (normalized === 'triggereventtype') return context.triggerAbilityMode === 'ByEventType';
        }

        return true;
    }

    shouldRefreshParameterPanel(effectMeta, previousParams, nextParams) {
        if (!effectMeta?.parameters) return false;
        const hasSearchMode = this.hasEffectParameter(effectMeta, 'UseSearchMode');
        const hasCardSourceMode = this.hasEffectParameter(effectMeta, 'CardSourceMode');
        const hasRemoveMode = this.hasEffectParameter(effectMeta, 'RemoveMode');
        const hasDurationType = this.hasEffectParameter(effectMeta, 'DurationType');
        const hasModifierLayer = this.hasEffectParameter(effectMeta, 'ModifierLayer');
        const hasTriggerMode = this.hasEffectParameter(effectMeta, 'TriggerMode');

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

        if (hasDurationType) {
            const prevDurationType = normalizeDurationType(this.getEffectParameterValue(previousParams, effectMeta, 'DurationType'));
            const nextDurationType = normalizeDurationType(this.getEffectParameterValue(nextParams, effectMeta, 'DurationType'));
            if (prevDurationType !== nextDurationType) return true;
        }

        if (hasModifierLayer) {
            const prevLayer = this.normalizeModifierLayer(this.getEffectParameterValue(previousParams, effectMeta, 'ModifierLayer'));
            const nextLayer = this.normalizeModifierLayer(this.getEffectParameterValue(nextParams, effectMeta, 'ModifierLayer'));
            if (prevLayer !== nextLayer) return true;
        }

        if (hasTriggerMode) {
            const prevMode = this.normalizeTriggerAbilityMode(this.getEffectParameterValue(previousParams, effectMeta, 'TriggerMode'));
            const nextMode = this.normalizeTriggerAbilityMode(this.getEffectParameterValue(nextParams, effectMeta, 'TriggerMode'));
            if (prevMode !== nextMode) return true;
        }

        if (this.isReplaceEventEffectMeta(effectMeta)) {
            const prevType = this.normalizeMetadataLookupKey(this.getReplacementEventTypeValue(effectMeta, previousParams));
            const nextType = this.normalizeMetadataLookupKey(this.getReplacementEventTypeValue(effectMeta, nextParams));
            if (prevType !== nextType) return true;

            const dynamicKeys = new Set([
                ...Object.keys(previousParams || {}),
                ...Object.keys(nextParams || {})
            ].filter((key) => this.isReplacementParamsParameter(key)));

            for (const key of dynamicKeys) {
                const prevValue = this.getParameterValueFromObject(previousParams, key);
                const nextValue = this.getParameterValueFromObject(nextParams, key);
                if (this.isFormulaValueConfig(prevValue) !== this.isFormulaValueConfig(nextValue)) {
                    return true;
                }
            }
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

    getDslTriggeringEventContext() {
        return {
            eventTypes: this.options?.trigger?.eventTypes || [],
            timing: this.options?.trigger?.timing || 'On',
            outputVariables: this.options?.triggeringEventOutputs || []
        };
    }

    unwrapOuterParens(text) {
        let value = String(text || '').trim();
        const isWrapped = (input) => {
            if (!input.startsWith('(') || !input.endsWith(')')) return false;
            let depth = 0;
            for (let i = 0; i < input.length; i++) {
                const ch = input[i];
                if (ch === '(') depth++;
                if (ch === ')') depth--;
                if (depth === 0 && i < input.length - 1) return false;
            }
            return depth === 0;
        };
        while (isWrapped(value)) {
            value = value.slice(1, -1).trim();
        }
        return value;
    }

    inferTypeFromTriggeringOutputReference(formula, context = null) {
        const cleaned = this.unwrapOuterParens(formula).replace(/\s+/g, '');
        const prefix = 'context.triggeringEvent.Outputs.';
        if (!cleaned.startsWith(prefix)) return null;
        const refName = String(cleaned.slice(prefix.length) || '').trim();
        if (!refName) return null;
        if (/[+\-*/<>=!&|,[\]{}()]/.test(refName)) return null;

        const outputVariables = Array.isArray(context?.outputVariables)
            ? context.outputVariables
            : [];
        const matched = outputVariables.find(entry =>
            String(entry?.name || '').trim().toLowerCase() === refName.toLowerCase()
        );
        if (!matched) return null;
        return String(matched?.typeName || '').trim() || null;
    }

    getEffectOutputDraftSnapshot(effectIndex) {
        const helper = this.outputEditorsByEffect.get(effectIndex);
        if (!helper || typeof helper.getRowsSnapshot !== 'function') {
            return [];
        }
        try {
            return helper.getRowsSnapshot();
        } catch (error) {
            console.warn('EffectConfigEditor: failed to read output draft snapshot', error);
            return [];
        }
    }

    async buildEffectOutputVariableHints(effectIndex) {
        const triggeringEventContext = this.getDslTriggeringEventContext();
        const draftRows = this.getEffectOutputDraftSnapshot(effectIndex)
            .filter(row => row?.name);
        const entries = draftRows.length > 0
            ? draftRows.map(row => [row.name, row.formula, row.typeName])
            : (() => {
                const effect = this.effects?.[effectIndex];
                if (!effect) return [];
                const outputs = this.normalizeEffectOutputs(effect.outputs ?? effect.Outputs);
                return Object.entries(outputs).map(([name, formula]) => [name, formula, null]);
            })();
        if (entries.length === 0) return [];

        const typeHints = [];
        for (const [rawName, rawFormula, draftTypeName] of entries) {
            const name = String(rawName || '').trim();
            if (!name) continue;

                const formula = String(rawFormula || '').trim();
                let typeName = 'Object';
                const passthroughType = this.inferTypeFromTriggeringOutputReference(formula, triggeringEventContext);
                if (passthroughType) {
                    typeName = passthroughType;
                }
                if (!formula) {
                    typeName = 'Void';
                } else if (!passthroughType) {
                    try {
                        const res = await validateDslExpression(this.metadata, formula, {
                            allowEmpty: false,
                            triggeringEventContext
                        });
                        if (res?.ast) {
                            const inferred = getCoreSemanticNodeType(res.ast) || res.ast.returnType || null;
                            if (inferred) {
                                typeName = String(inferred);
                        }
                    }
                } catch (error) {
                    console.warn(`EffectConfigEditor: failed to infer output type for ${name}`, error);
                }
            }

            if ((!typeName || typeName === 'Object') && draftTypeName) {
                typeName = String(draftTypeName);
            }

            typeHints.push({
                name,
                typeName,
                formula
            });
        }

        return typeHints;
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

        this.flushEffectOutputDrafts(effectIndex);

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
        const triggeringEventOutputs = await this.buildEffectOutputVariableHints(effectIndex);

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
                lockValidZones: true,
                triggeringEventOutputs
            }
        );
    }

    flushEffectOutputDrafts(effectIndex) {
        const helper = this.outputEditorsByEffect.get(effectIndex);
        if (!helper || typeof helper.flushDraftToEffect !== 'function') {
            return;
        }

        try {
            helper.flushDraftToEffect();
        } catch (error) {
            console.warn('EffectConfigEditor: failed to flush output draft before opening chained ability editor', error);
        }
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
            counterRegistrations: [],
            playerCounterRegistrations: []
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
            playerCounterRegistrations: Array.isArray(parsed?.playerCounterRegistrations)
                ? parsed.playerCounterRegistrations
                : (Array.isArray(updatedConfig?.playerCounterRegistrations) ? updatedConfig.playerCounterRegistrations : []),
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
            repeatCountValue: null,
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

    getEffectiveEffectParameterMetadata(effectMeta, parameters, effectIndex) {
        const baseParameters = Array.isArray(effectMeta?.parameters) ? effectMeta.parameters : [];
        const replacementParameters = this.getReplacementDynamicParameterMetadata(effectMeta, parameters, effectIndex);
        if (replacementParameters.length === 0) {
            return baseParameters;
        }

        return [...baseParameters, ...replacementParameters];
    }

    getReplacementDynamicParameterMetadata(effectMeta, parameters, effectIndex) {
        if (!this.isReplaceEventEffectMeta(effectMeta)) {
            return [];
        }

        const replacementEventType = this.getReplacementEventTypeValue(effectMeta, parameters);
        if (!replacementEventType) {
            return [];
        }

        const cacheKey = this.normalizeMetadataLookupKey(replacementEventType);
        const cached = this.replaceEventParamMetadataCache.get(cacheKey);
        if (Array.isArray(cached)) {
            return cached.map((meta) => {
                const rawPropertyName = this.getEffectParameterName(meta);
                const displayName = meta.displayName || meta.DisplayName || rawPropertyName;
                const description = meta.description || meta.Description || '';
                return {
                    ...meta,
                    propertyName: `ReplacementParams.${rawPropertyName}`,
                    displayName: `替换参数 · ${displayName}`,
                    description
                };
            });
        }

        this.ensureReplacementParameterMetadata(replacementEventType, effectIndex);
        return [];
    }

    async ensureReplacementParameterMetadata(replacementEventType, effectIndex) {
        const cacheKey = this.normalizeMetadataLookupKey(replacementEventType);
        if (this.replaceEventParamMetadataCache.has(cacheKey)) {
            return this.replaceEventParamMetadataCache.get(cacheKey);
        }

        if (this.replaceEventParamMetadataRequests.has(cacheKey)) {
            return this.replaceEventParamMetadataRequests.get(cacheKey);
        }

        const promise = this.fetchReplacementParameterMetadata(replacementEventType)
            .then((metadata) => {
                this.replaceEventParamMetadataCache.set(cacheKey, metadata);

                if (Number.isInteger(effectIndex)) {
                    const effect = this.effects?.[effectIndex];
                    const effectMeta = this.getEffectMetadata(effect?.type);
                    const currentReplacementType = this.getReplacementEventTypeValue(effectMeta, effect?.parameters || {});
                    if (this.normalizeMetadataLookupKey(currentReplacementType) === cacheKey) {
                        this.updateEffectNode(effectIndex);
                    }
                }

                return metadata;
            })
            .finally(() => {
                this.replaceEventParamMetadataRequests.delete(cacheKey);
            });

        this.replaceEventParamMetadataRequests.set(cacheKey, promise);
        return promise;
    }

    async fetchReplacementParameterMetadata(replacementEventType) {
        try {
            const res = await fetch(`/api/abilityEditorOptions/replaceEventParameters/${encodeURIComponent(replacementEventType)}`);
            if (!res.ok) {
                throw new Error(`HTTP ${res.status}`);
            }

            const raw = await res.json();
            if (!Array.isArray(raw)) {
                return [];
            }

            return raw.filter((item) => this.getEffectParameterName(item));
        } catch (error) {
            console.warn('EffectConfigEditor: failed to load replace-event parameter metadata', replacementEventType, error);
            return [];
        }
    }

    normalizeMetadataLookupKey(value) {
        if (value == null) return '';
        return String(value).trim().toLowerCase();
    }

    isReplaceEventEffectMeta(effectMeta) {
        const typeName = this.getEffectTypeName(effectMeta);
        return typeName.toLowerCase() === 'replaceevent';
    }

    isReplacementEventTypeParameter(paramName) {
        return String(paramName || '').trim().toLowerCase() === 'replacementeventtype';
    }

    isReplacementParamsParameter(paramName) {
        return String(paramName || '').trim().toLowerCase().startsWith('replacementparams.');
    }

    getReplacementEventTypeValue(effectMeta, parameters) {
        if (!effectMeta || !parameters || typeof parameters !== 'object') {
            return '';
        }

        const raw = this.getEffectParameterValue(parameters, effectMeta, 'ReplacementEventType');
        if (raw == null) {
            return '';
        }

        return String(raw).trim();
    }

    notifyChange() {
        if (!this.onChange) return;

        this.effects = this.synchronizeDecisionBranchTriggers(this.effects, this.options?.trigger);

        const sanitizedEffects = this.effects.map(effect => {
            const cleanParams = this.sanitizeParameters(effect.parameters);
            const cleanOutputs = this.sanitizeOutputs(effect.outputs ?? effect.Outputs);
            return { ...effect, parameters: cleanParams, outputs: cleanOutputs };
        });

        this.onChange(sanitizedEffects);
    }

    normalizeEffectOutputs(outputs) {
        if (!outputs || typeof outputs !== 'object' || Array.isArray(outputs)) {
            return {};
        }

        const normalized = {};
        Object.entries(outputs).forEach(([rawKey, rawValue]) => {
            const key = String(rawKey || '').trim();
            if (!key) return;
            if (rawValue == null) {
                normalized[key] = '';
                return;
            }
            normalized[key] = String(rawValue);
        });

        return normalized;
    }

    sanitizeOutputs(outputs) {
        const normalized = this.normalizeEffectOutputs(outputs);
        const result = {};

        Object.entries(normalized).forEach(([rawKey, rawValue]) => {
            const key = String(rawKey || '').trim();
            if (!key) return;
            result[key] = String(rawValue ?? '');
        });

        return result;
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



    synchronizeDecisionAbilityConfig(ability, parentTrigger) {
        if (!ability || typeof ability !== 'object') {
            return ability;
        }

        return {
            ...ability,
            trigger: mergeInheritedTriggerFields(ability.trigger ?? ability.Trigger, parentTrigger)
        };
    }

    synchronizeDecisionBranchTriggers(effects, parentTrigger) {
        if (!Array.isArray(effects) || !parentTrigger || typeof parentTrigger !== 'object') {
            return Array.isArray(effects) ? effects : [];
        }

        return effects.map(effect => {
            if (!effect || typeof effect !== 'object') {
                return effect;
            }

            if (!isDecisionAbilityEffect(effect.type ?? effect.Type ?? '')) {
                return effect;
            }

            const parameters = effect.parameters ?? effect.Parameters ?? {};
            const rawAbilityConfigs = parameters.abilityConfigs ?? parameters.AbilityConfigs;
            if (!Array.isArray(rawAbilityConfigs)) {
                return effect;
            }

            const abilityConfigs = rawAbilityConfigs.map(ability =>
                this.synchronizeDecisionAbilityConfig(ability, parentTrigger)
            );
            const nextParameters = {
                ...parameters,
                abilityConfigs: abilityConfigs
            };
            delete nextParameters.AbilityConfigs;

            return {
                ...effect,
                parameters: nextParameters
            };
        });
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
        const unwrapped = unwrapStaticValueConfig(fallback);
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
        const normalizedEffects = this.synchronizeDecisionBranchTriggers(effects, this.options?.trigger);

        const getEffectMeta = (type) => effectList.find(item =>
            item?.effectTypeName === type || item?.EffectTypeName === type
        );

        return normalizedEffects.map(effect => {
            if (!effect || typeof effect !== 'object') return effect;
            const effectMeta = getEffectMeta(effect.type);
            const withDefaults = this.applyDefaultParameters(effect, effectMeta);
            const cleanParams = this.sanitizeParameters(withDefaults.parameters);
            const cleanOutputs = this.sanitizeOutputs(withDefaults.outputs ?? withDefaults.Outputs);
            return { ...withDefaults, parameters: cleanParams, outputs: cleanOutputs };
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
            const unwrapped = unwrapStaticValueConfig(paramValue);
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


}
