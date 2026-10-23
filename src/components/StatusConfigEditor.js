// src/components/StatusConfigEditor.js - 状态(Buff)配置编辑器
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';
import { ConditionConfigEditor } from './ConditionConfigEditor.js';
import { EffectConfigEditor } from './EffectConfigEditor.js';
import { ValueConfigEditor } from './ValueConfigEditor.js';
import { ensureEnumLocalization } from '../enumLocalizationCache.js';

/**
 * 状态(Buff)配置编辑器
 * 采用手风琴式列表，支持多条Buff配置，并可配置属性/关键词/能力关键字/能力四类效果
 */
export class StatusConfigEditor extends MetadataDrivenComponent {
    constructor() {
        super();
        this.buffConfigs = [];
        this.onChange = null;
        this.container = null;
        this.options = {};
        this.abilityListHelper = new EffectConfigEditor();
        this.conditionHelper = new ConditionConfigEditor();
    }

    async render(container, buffConfigs = [], onChange = () => {}, options = {}) {
        await this.initialize();
        await this.abilityListHelper.initialize();
        await this.conditionHelper.initialize();

        this.container = container;
        this.onChange = onChange;
        this.options = options || {};
        this.buffConfigs = this.normalizeBuffConfigList(buffConfigs || []);

        this.clearContainer(container);

        try {
            const wrapper = document.createElement('div');
            wrapper.className = 'step-content-wrapper effects-step';

            const header = document.createElement('div');
            header.className = 'step-header';
            header.innerHTML = `
                <h4>${this.resolveText('title', '状态配置')}</h4>
                <p class="step-intro">${this.resolveText('intro', '每条状态仅选择一种类型进行配置；复合状态由多条配置合并显示。')}</p>
            `;
            wrapper.appendChild(header);

            const listContainer = document.createElement('div');
            listContainer.className = 'effects-list';
            listContainer.id = 'buff-config-list-container';
            wrapper.appendChild(listContainer);

            this.renderBuffConfigList(listContainer);
            this.notifyChange();

            const actionsContainer = document.createElement('div');
            actionsContainer.className = 'add-effect-container';
            const addBtn = this.createButton(this.resolveText('addLabel', '添加状态'), () => this.addBuffConfig(), 'primary');
            actionsContainer.appendChild(addBtn);
            wrapper.appendChild(actionsContainer);

            container.appendChild(wrapper);
        } catch (error) {
            console.error('StatusConfigEditor: Render failed:', error);
            this.showError(`渲染状态配置失败: ${error.message}`, container);
        }
    }

    resolveText(key, fallback) {
        return this.options?.labels?.[key] ?? this.options?.[key] ?? fallback;
    }

    renderBuffConfigList(container) {
        this.clearContainer(container);

        if (!this.buffConfigs.length) {
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
                <h5 class="empty-title">暂无状态配置</h5>
                <p class="empty-desc">点击下方按钮添加第一个状态配置</p>
            `;
            container.appendChild(placeholder);
            return;
        }

        this.buffConfigs.forEach((buff, index) => {
            const node = this.createBuffConfigNode(buff, index);
            container.appendChild(node);
        });
    }

    createBuffConfigNode(buff, index) {
        const node = document.createElement('div');
        node.className = 'effect-node accordion-item';
        node.dataset.buffIndex = index;

        const type = this.getBuffType(buff);
        const count = this.getBuffTypeCount(buff, type);
        const typeLabel = this.getBuffTypeLabel(type);
        const summary = type ? `${typeLabel}${count}` : '未选择类型';
        const title = type ? `${typeLabel}状态` : '未选择状态类型';

        const header = document.createElement('div');
        header.className = 'accordion-header';

        const headerInfo = document.createElement('div');
        headerInfo.className = 'header-info';
        headerInfo.innerHTML = `
            <span class="item-index">#${index + 1}</span>
            <span class="item-title">${title}</span>
            <span class="item-status status-enabled">${summary}</span>
        `;

        const headerActions = document.createElement('div');
        headerActions.className = 'header-actions';

        const settingsBtn = document.createElement('button');
        settingsBtn.type = 'button';
        settingsBtn.className = 'btn-icon settings-btn';
        settingsBtn.title = '通用设置';
        settingsBtn.setAttribute('aria-label', '通用设置');

        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'btn-icon delete-btn';
        deleteBtn.title = '删除';
        deleteBtn.setAttribute('aria-label', '删除');

        const expandBtn = document.createElement('button');
        expandBtn.className = 'btn-icon expand-btn';
        expandBtn.title = '展开/折叠';
        expandBtn.textContent = '▼';

        headerActions.appendChild(settingsBtn);
        headerActions.appendChild(deleteBtn);
        headerActions.appendChild(expandBtn);

        header.appendChild(headerInfo);
        header.appendChild(headerActions);

        deleteBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.removeBuffConfig(index);
        });

        const content = document.createElement('div');
        content.className = 'accordion-content collapsed';

        const optionsPanel = this.createBuffOptionsPanel(buff, index);
        content.appendChild(optionsPanel);

        if (!type) {
            const typeSection = this.createTypeSelectionSection(index);
            content.appendChild(typeSection);
        } else if (type === 'stat') {
            const statSection = this.createStatModifiersSection(buff, index);
            content.appendChild(statSection);
        } else if (type === 'keyword') {
            const keywordSection = this.createKeywordModifiersSection(buff, index);
            content.appendChild(keywordSection);
        } else if (type === 'tribe') {
            const tribeSection = this.createTribeModifiersSection(buff, index);
            content.appendChild(tribeSection);
        } else if (type === 'abilityKeyword') {
            const abilityKeywordSection = this.createAbilityKeywordModifiersSection(buff, index);
            content.appendChild(abilityKeywordSection);
        } else if (type === 'actionUsage') {
            const actionUsageSection = this.createActionUsageModifiersSection(buff, index);
            content.appendChild(actionUsageSection);
        } else if (type === 'ability') {
            const abilitySection = this.createGrantedAbilitiesSection(buff, index);
            content.appendChild(abilitySection);
        }

        node.appendChild(header);
        node.appendChild(content);

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

    createBuffOptionsPanel(buff, index) {
        const container = document.createElement('div');
        container.className = 'collapsible-content effect-options-panel collapsed';

        const title = document.createElement('h4');
        title.className = 'effect-options-title';
        title.textContent = '通用设置';
        container.appendChild(title);

        const section = document.createElement('div');
        section.className = 'config-section';
        section.innerHTML = '<h5 class="section-title">叠加规则</h5>';

        const stackToggle = this.createToggleSwitch(
            !!buff.canStack,
            (checked) => this.updateBuffConfig(index, { canStack: checked }),
            '允许叠加'
        );
        section.appendChild(this.createFormGroup(null, stackToggle));

        const durationTypeControl = document.createElement('div');
        durationTypeControl.className = 'parameter-control';
        const durationTypeSelect = this.createEnumSelect(
            'DurationType',
            buff.durationType || 'Permanent',
            (value) => this.updateBuffConfig(index, { durationType: value || 'Permanent' })
        );
        durationTypeControl.appendChild(durationTypeSelect);
        section.appendChild(this.createFormGroup(this.createParameterLabel('持续时间'), durationTypeControl));

        if (this.normalizeDurationType(buff.durationType) === 'ConditionalRemoval') {
            const removeConditionContainer = document.createElement('div');
            removeConditionContainer.className = 'parameter-control';
            this.conditionHelper.render(
                removeConditionContainer,
                buff.removeCondition ? [buff.removeCondition] : [],
                (conditions) => this.updateBuffConfig(index, { removeCondition: Array.isArray(conditions) && conditions[0] ? conditions[0] : null }),
                {
                    title: '移除条件',
                    intro: '当条件成立时移除该状态。',
                    addLabel: '设置移除条件',
                    hideHelp: true
                }
            );
            section.appendChild(this.createFormGroup(this.createParameterLabel('移除条件'), removeConditionContainer));
        } else {
            const durationParameterControl = document.createElement('div');
            durationParameterControl.className = 'parameter-control';
            const durationParameterEditor = this.createValueConfigEditorControl(
                buff.durationParameter,
                (value) => this.updateBuffConfig(index, { durationParameter: value }),
                { parameterType: 'Int32', parameterLabel: '持续参数', placeholderValue: 0 }
            );
            durationParameterControl.appendChild(durationParameterEditor);
            section.appendChild(this.createValueConfigFormGroup(null, durationParameterControl));
        }

        container.appendChild(section);
        return container;
    }

    createTypeSelectionSection(buffIndex) {
        const section = document.createElement('div');
        section.className = 'config-section';
        section.innerHTML = '<h5 class="section-title">状态类型</h5>';

        const container = document.createElement('div');
        container.className = 'effect-type-selection-container';

        const grid = document.createElement('div');
        grid.className = 'effect-type-grid';

        const options = [
            { type: 'stat', title: '属性状态', desc: '调整攻击/生命/费用等属性' },
            { type: 'keyword', title: '关键词状态', desc: '添加或移除卡牌关键词' },
            { type: 'tribe', title: '种类状态', desc: '为卡牌附加额外种类' },
            { type: 'abilityKeyword', title: '能力关键字状态', desc: '调整启动/爆能/融合等关键字数值' },
            { type: 'actionUsage', title: '动作次数状态', desc: '设置攻击/启动/融合的每回合次数' },
            { type: 'ability', title: '能力状态', desc: '授予额外能力' }
        ];

        options.forEach(option => {
            const btn = document.createElement('button');
            btn.className = 'effect-type-btn';
            btn.innerHTML = `
                <span class="effect-name">${option.title}</span>
                <span class="effect-desc">${option.desc}</span>
            `;
            btn.addEventListener('click', () => {
                this.setBuffType(buffIndex, option.type);
            });
            grid.appendChild(btn);
        });

        container.appendChild(grid);
        section.appendChild(container);
        return section;
    }

    getBuffType(buff) {
        if (!buff || typeof buff !== 'object') return '';
        if (buff._uiType) return buff._uiType;
        if (Array.isArray(buff.statModifiers) && buff.statModifiers.length > 0) return 'stat';
        if (Array.isArray(buff.keywordModifiers) && buff.keywordModifiers.length > 0) return 'keyword';
        if (Array.isArray(buff.tribeModifiers) && buff.tribeModifiers.length > 0) return 'tribe';
        if (Array.isArray(buff.abilityKeywordModifiers) && buff.abilityKeywordModifiers.length > 0) return 'abilityKeyword';
        if (Array.isArray(buff.actionUsageLimitModifiers) && buff.actionUsageLimitModifiers.length > 0) return 'actionUsage';
        if (Array.isArray(buff.grantedAbilities) && buff.grantedAbilities.length > 0) return 'ability';
        return '';
    }

    getBuffTypeLabel(type) {
        switch (type) {
            case 'stat':
                return '属性';
            case 'keyword':
                return '关键词';
            case 'tribe':
                return '种类';
            case 'abilityKeyword':
                return '能力关键字';
            case 'actionUsage':
                return '动作次数';
            case 'ability':
                return '能力';
            default:
                return '未选择';
        }
    }

    getBuffTypeCount(buff, type) {
        if (!type) return 0;
        if (type === 'stat') return this.getStatAdjustmentCount(buff);
        if (type === 'keyword') return buff.keywordModifiers?.length || 0;
        if (type === 'tribe') return buff.tribeModifiers?.length || 0;
        if (type === 'abilityKeyword') return buff.abilityKeywordModifiers?.length || 0;
        if (type === 'actionUsage') return buff.actionUsageLimitModifiers?.length || 0;
        if (type === 'ability') return buff.grantedAbilities?.length || 0;
        return 0;
    }

    setBuffType(index, type) {
        const current = this.buffConfigs[index];
        if (!current) return;

        if (type === 'stat') {
            const statAdjustment = current._statAdjustment ?? this.deriveStatAdjustmentState(current.statModifiers || []);
            const statAdjustmentDirty = current._statAdjustmentDirty ?? false;
            this.updateBuffConfig(index, {
                _uiType: 'stat',
                statModifiers: Array.isArray(current.statModifiers) ? current.statModifiers : [],
                _statAdjustment: statAdjustment,
                _statAdjustmentDirty: statAdjustmentDirty,
                keywordModifiers: [],
                tribeModifiers: [],
                abilityKeywordModifiers: [],
                actionUsageLimitModifiers: [],
                grantedAbilities: []
            });
            return;
        }
        if (type === 'keyword') {
            this.updateBuffConfig(index, {
                _uiType: 'keyword',
                statModifiers: [],
                keywordModifiers: (current.keywordModifiers && current.keywordModifiers.length > 0)
                    ? current.keywordModifiers
                    : [{ keyword: 'None', operation: 'Add' }],
                tribeModifiers: [],
                abilityKeywordModifiers: [],
                actionUsageLimitModifiers: [],
                grantedAbilities: []
            });
            return;
        }
        if (type === 'tribe') {
            this.updateBuffConfig(index, {
                _uiType: 'tribe',
                statModifiers: [],
                keywordModifiers: [],
                tribeModifiers: (current.tribeModifiers && current.tribeModifiers.length > 0)
                    ? current.tribeModifiers
                    : [{ tribe: '' }],
                abilityKeywordModifiers: [],
                actionUsageLimitModifiers: [],
                grantedAbilities: []
            });
            return;
        }
        if (type === 'abilityKeyword') {
            this.updateBuffConfig(index, {
                _uiType: 'abilityKeyword',
                statModifiers: [],
                keywordModifiers: [],
                tribeModifiers: [],
                abilityKeywordModifiers: (current.abilityKeywordModifiers && current.abilityKeywordModifiers.length > 0)
                    ? current.abilityKeywordModifiers
                    : [{ keyword: 'None', layer: 'Additive', value: null }],
                actionUsageLimitModifiers: [],
                grantedAbilities: []
            });
            return;
        }
        if (type === 'actionUsage') {
            this.updateBuffConfig(index, {
                _uiType: 'actionUsage',
                statModifiers: [],
                keywordModifiers: [],
                tribeModifiers: [],
                abilityKeywordModifiers: [],
                actionUsageLimitModifiers: (current.actionUsageLimitModifiers && current.actionUsageLimitModifiers.length > 0)
                    ? current.actionUsageLimitModifiers
                    : [{ actionType: 'Attack', value: null }],
                grantedAbilities: []
            });
            return;
        }
        if (type === 'ability') {
            this.updateBuffConfig(index, {
                _uiType: 'ability',
                statModifiers: [],
                keywordModifiers: [],
                tribeModifiers: [],
                abilityKeywordModifiers: [],
                actionUsageLimitModifiers: [],
                grantedAbilities: Array.isArray(current.grantedAbilities) ? current.grantedAbilities : []
            });
        }
    }

    createStatModifiersSection(buff, buffIndex) {
        const section = document.createElement('div');
        section.className = 'parameters-panel';

        const state = this.getStatAdjustmentState(buff);
        if (state.legacy) {
            const warning = document.createElement('div');
            warning.className = 'text-muted';
            warning.textContent = '检测到多层或重复属性修正，编辑将以简化模式保存。';
            container.appendChild(warning);
        }

        const grid = document.createElement('div');
        grid.className = 'form-grid-layout';

        const attackControl = document.createElement('div');
        attackControl.className = 'parameter-control';
        const attackInput = this.createValueConfigEditorControl(
            state.attackModifier,
            (value) => this.updateStatAdjustmentValue(buffIndex, 'attackModifier', value),
            { parameterType: 'Int32', parameterLabel: '攻击力修正' }
        );
        attackControl.appendChild(attackInput);
        grid.appendChild(this.createValueConfigFormGroup(null, attackControl));

        const healthControl = document.createElement('div');
        healthControl.className = 'parameter-control';
        const healthInput = this.createValueConfigEditorControl(
            state.healthModifier,
            (value) => this.updateStatAdjustmentValue(buffIndex, 'healthModifier', value),
            { parameterType: 'Int32', parameterLabel: '生命值修正' }
        );
        healthControl.appendChild(healthInput);
        grid.appendChild(this.createValueConfigFormGroup(null, healthControl));

        const costControl = document.createElement('div');
        costControl.className = 'parameter-control';
        const costInput = this.createValueConfigEditorControl(
            state.costModifier,
            (value) => this.updateStatAdjustmentValue(buffIndex, 'costModifier', value),
            { parameterType: 'Int32', parameterLabel: '费用修正' }
        );
        costControl.appendChild(costInput);
        grid.appendChild(this.createValueConfigFormGroup(null, costControl));

        const layerControl = document.createElement('div');
        layerControl.className = 'parameter-control';
        const layerSelect = this.createEnumSelect(
            'ModifierLayer',
            state.layer || 'Additive',
            (value) => this.updateStatAdjustment(buffIndex, { layer: value || 'Additive' })
        );
        layerControl.appendChild(layerSelect);
        grid.appendChild(this.createFormGroup(this.createParameterLabel('调整类型'), layerControl));

        section.appendChild(grid);

        return section;
    }

    createKeywordModifiersSection(buff, buffIndex) {
        const section = document.createElement('div');
        section.className = 'parameters-panel';

        const list = document.createElement('div');
        list.className = 'modifier-list';
        section.appendChild(list);

        const getBuff = () => this.buffConfigs[buffIndex] || {};

        const renderList = () => {
            list.innerHTML = '';
            const items = getBuff().keywordModifiers || [];

            if (!items.length) {
                const empty = document.createElement('div');
                empty.className = 'text-muted';
                empty.textContent = '暂无关键词修正';
                list.appendChild(empty);
                return;
            }

            items.forEach((modifier, index) => {
                const row = document.createElement('div');
                row.className = 'operation-item';

                const grid = document.createElement('div');
                grid.className = 'form-grid-layout';

                const keywordControl = document.createElement('div');
                keywordControl.className = 'parameter-control';
                const keywordSelect = this.createEnumSelect(
                    'CardKeyword',
                    modifier.keyword || 'None',
                    (value) => this.updateKeywordModifier(buffIndex, index, { keyword: value })
                );
                keywordControl.appendChild(keywordSelect);
                grid.appendChild(this.createFormGroup(this.createParameterLabel('关键词'), keywordControl));

                const opControl = document.createElement('div');
                opControl.className = 'parameter-control';
                const opSelect = this.createEnumSelect(
                    'KeywordOperation',
                    modifier.operation || 'Add',
                    (value) => this.updateKeywordModifier(buffIndex, index, { operation: value })
                );
                opControl.appendChild(opSelect);
                grid.appendChild(this.createFormGroup(this.createParameterLabel('操作'), opControl));

                const removeBtn = document.createElement('button');
                removeBtn.className = 'btn-icon delete-btn';
                removeBtn.type = 'button';
                removeBtn.title = '移除关键词';
                removeBtn.textContent = '×';
                removeBtn.addEventListener('click', () => {
                    this.removeKeywordModifier(buffIndex, index);
                    renderList();
                });

                row.appendChild(grid);
                row.appendChild(removeBtn);
                list.appendChild(row);
            });
        };

        renderList();

        const addBtn = this.createButton('添加关键词', () => {
            this.addKeywordModifier(buffIndex);
            renderList();
        }, 'secondary');
        addBtn.classList.add('btn-small');
        section.appendChild(addBtn);

        return section;
    }

    createTribeModifiersSection(buff, buffIndex) {
        const section = document.createElement('div');
        section.className = 'parameters-panel';

        const list = document.createElement('div');
        list.className = 'modifier-list';
        section.appendChild(list);

        const getBuff = () => this.buffConfigs[buffIndex] || {};

        const renderList = () => {
            list.innerHTML = '';
            const items = getBuff().tribeModifiers || [];

            if (!items.length) {
                const empty = document.createElement('div');
                empty.className = 'text-muted';
                empty.textContent = '暂无种类修正';
                list.appendChild(empty);
                return;
            }

            items.forEach((modifier, index) => {
                const row = document.createElement('div');
                row.className = 'operation-item';

                const grid = document.createElement('div');
                grid.className = 'form-grid-layout';

                const tribeControl = document.createElement('div');
                tribeControl.className = 'parameter-control';
                const tribeInput = this.createTextInput(
                    modifier.tribe || '',
                    (value) => this.updateTribeModifier(buffIndex, index, { tribe: value }),
                    { placeholder: '妖精' }
                );
                tribeControl.appendChild(tribeInput);
                grid.appendChild(this.createFormGroup(this.createParameterLabel('种类'), tribeControl));

                const removeBtn = document.createElement('button');
                removeBtn.className = 'btn-icon delete-btn';
                removeBtn.type = 'button';
                removeBtn.title = '移除种类';
                removeBtn.textContent = '×';
                removeBtn.addEventListener('click', () => {
                    this.removeTribeModifier(buffIndex, index);
                    renderList();
                });

                row.appendChild(grid);
                row.appendChild(removeBtn);
                list.appendChild(row);
            });
        };

        renderList();

        const addBtn = this.createButton('添加种类', () => {
            this.addTribeModifier(buffIndex);
            renderList();
        }, 'secondary');
        addBtn.classList.add('btn-small');
        section.appendChild(addBtn);

        return section;
    }

    createGrantedAbilitiesSection(buff, buffIndex) {
        const section = document.createElement('div');
        section.className = 'parameters-panel';

        const container = document.createElement('div');
        container.className = 'parameter-control';

        const updateAbilities = (next) => {
            this.updateBuffConfig(buffIndex, { grantedAbilities: Array.isArray(next) ? next : [] });
        };

        this.abilityListHelper.renderAbilityListEditor(
            container,
            buff.grantedAbilities || [],
            updateAbilities,
            { effectTypeName: '' }
        );

        const group = this.createFormGroup(this.createParameterLabel('授予能力'), container);
        group.classList.add('full-width');
        section.appendChild(group);

        return section;
    }

    createAbilityKeywordModifiersSection(buff, buffIndex) {
        const section = document.createElement('div');
        section.className = 'parameters-panel';

        const list = document.createElement('div');
        list.className = 'modifier-list';
        section.appendChild(list);

        const getBuff = () => this.buffConfigs[buffIndex] || {};

        const renderList = () => {
            list.innerHTML = '';
            const items = getBuff().abilityKeywordModifiers || [];

            if (!items.length) {
                const empty = document.createElement('div');
                empty.className = 'text-muted';
                empty.textContent = '暂无能力关键字修正';
                list.appendChild(empty);
                return;
            }

            items.forEach((modifier, index) => {
                const row = document.createElement('div');
                row.className = 'operation-item';

                const grid = document.createElement('div');
                grid.className = 'form-grid-layout';

                const keywordControl = document.createElement('div');
                keywordControl.className = 'parameter-control';
                const keywordSelect = this.createEnumSelect(
                    'AbilityKeyword',
                    modifier.keyword || 'None',
                    (value) => this.updateAbilityKeywordModifier(buffIndex, index, { keyword: value })
                );
                keywordControl.appendChild(keywordSelect);
                grid.appendChild(this.createFormGroup(this.createParameterLabel('能力关键字'), keywordControl));

                const layerControl = document.createElement('div');
                layerControl.className = 'parameter-control';
                const layerSelect = this.createEnumSelect(
                    'ModifierLayer',
                    modifier.layer || 'Additive',
                    (value) => this.updateAbilityKeywordModifier(buffIndex, index, { layer: value || 'Additive' })
                );
                layerControl.appendChild(layerSelect);
                grid.appendChild(this.createFormGroup(this.createParameterLabel('调整类型'), layerControl));

                const valueControl = document.createElement('div');
                valueControl.className = 'parameter-control';
                const valueInput = this.createValueConfigEditorControl(
                    modifier.value,
                    (value) => this.updateAbilityKeywordModifier(buffIndex, index, { value }),
                    { parameterType: 'Int32', parameterLabel: '修正值' }
                );
                valueControl.appendChild(valueInput);
                grid.appendChild(this.createValueConfigFormGroup(null, valueControl));

                const removeBtn = document.createElement('button');
                removeBtn.className = 'btn-icon delete-btn';
                removeBtn.type = 'button';
                removeBtn.title = '移除能力关键字修正';
                removeBtn.textContent = '×';
                removeBtn.addEventListener('click', () => {
                    this.removeAbilityKeywordModifier(buffIndex, index);
                    renderList();
                });

                row.appendChild(grid);
                row.appendChild(removeBtn);
                list.appendChild(row);
            });
        };

        renderList();

        const addBtn = this.createButton('添加能力关键字修正', () => {
            this.addAbilityKeywordModifier(buffIndex);
            renderList();
        }, 'secondary');
        addBtn.classList.add('btn-small');
        section.appendChild(addBtn);

        return section;
    }

    createActionUsageModifiersSection(buff, buffIndex) {
        const section = document.createElement('div');
        section.className = 'parameters-panel';

        const list = document.createElement('div');
        list.className = 'modifier-list';

        const getBuff = () => this.buffConfigs[buffIndex] || {};
        const renderList = () => {
            this.clearContainer(list);
            const modifiers = getBuff().actionUsageLimitModifiers || [];

            if (!modifiers.length) {
                const empty = document.createElement('div');
                empty.className = 'text-muted';
                empty.textContent = '暂无动作次数配置';
                list.appendChild(empty);
                return;
            }

            modifiers.forEach((modifier, index) => {
                const row = document.createElement('div');
                row.className = 'operation-item';

                const grid = document.createElement('div');
                grid.className = 'form-grid-layout';

                const actionTypeControl = document.createElement('div');
                actionTypeControl.className = 'parameter-control';
                const actionTypeSelect = this.createEnumSelect(
                    'CardActionUsageType',
                    modifier.actionType || 'Attack',
                    (value) => this.updateActionUsageModifier(buffIndex, index, { actionType: value || 'Attack' })
                );
                actionTypeControl.appendChild(actionTypeSelect);
                grid.appendChild(this.createFormGroup(this.createParameterLabel('类型'), actionTypeControl));

                const valueControl = document.createElement('div');
                valueControl.className = 'parameter-control';
                const valueInput = this.createValueConfigEditorControl(
                    modifier.value,
                    (value) => this.updateActionUsageModifier(buffIndex, index, { value }),
                    { parameterType: 'Int32', parameterLabel: '数值' }
                );
                valueControl.appendChild(valueInput);
                grid.appendChild(this.createValueConfigFormGroup(null, valueControl));

                const removeBtn = document.createElement('button');
                removeBtn.className = 'btn-icon delete-btn';
                removeBtn.type = 'button';
                removeBtn.title = '移除动作次数配置';
                removeBtn.textContent = '×';
                removeBtn.addEventListener('click', () => {
                    this.removeActionUsageModifier(buffIndex, index);
                    renderList();
                });

                row.appendChild(grid);
                row.appendChild(removeBtn);
                list.appendChild(row);
            });
        };

        renderList();
        section.appendChild(list);

        const addBtn = this.createButton('添加动作次数配置', () => {
            this.addActionUsageModifier(buffIndex);
            renderList();
        }, 'secondary');
        addBtn.classList.add('btn-small');
        section.appendChild(addBtn);

        return section;
    }

    createEnumSelect(enumName, currentValue, onChange, placeholder = '加载中...') {
        const select = document.createElement('select');
        select.className = 'form-select';
        select.innerHTML = `<option value="">${placeholder}</option>`;
        select.addEventListener('change', (e) => {
            onChange(e.target.value);
        });
        this.populateEnumDropdown(select, enumName, currentValue);
        return select;
    }

    async populateEnumDropdown(select, enumName, currentValue) {
        try {
            const { raw } = await ensureEnumLocalization(enumName);
            const data = raw || {};
            if (Object.keys(data).length === 0) {
                throw new Error('Empty enum localization');
            }

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
        } catch (error) {
            console.warn('StatusConfigEditor: Failed to load enum options', enumName, error);
            select.disabled = true;
            select.innerHTML = '<option value="">加载失败</option>';
        }
    }

    normalizeEnumValue(value, enumMap) {
        if (!enumMap) return null;
        const target = String(value ?? '').trim().toLowerCase();
        if (!target) return null;
        const entries = Object.keys(enumMap);
        const match = entries.find(key => String(key).toLowerCase() === target);
        return match ?? null;
    }

    coerceNumericValue(value) {
        if (typeof value === 'number') {
            return value;
        }
        if (value && typeof value === 'object') {
            const raw = value.Value ?? value.value;
            if (typeof raw === 'number') {
                return raw;
            }
        }
        return null;
    }

    normalizeValueConfigInput(value) {
        if (value === undefined || value === null) {
            return null;
        }
        if (typeof value === 'object') {
            return value;
        }
        return { $type: 'StaticValueConfig', Value: value };
    }

    createValueConfigEditorControl(value, onChange, options = {}) {
        const container = document.createElement('div');
        container.className = 'value-config-inline';
        const parameterType = options.parameterType || 'Int32';
        const defaultPlaceholderValue =
            parameterType === 'Int32' || parameterType === 'Int64' || parameterType === 'Single' || parameterType === 'Double' || parameterType === 'Decimal'
                ? 0
                : null;

        const editor = new ValueConfigEditor();
        editor.render(
            container,
            this.normalizeValueConfigInput(value),
            (nextValue) => {
                if (this.isValueConfigEmpty(nextValue)) {
                    onChange(null);
                    return;
                }
                onChange(nextValue);
            },
            {
                parameterType,
                usePlaceholder: true,
                placeholderValue: options.placeholderValue ?? defaultPlaceholderValue,
                parameterLabel: options.parameterLabel || '',
                inline: true,
                hideValueLabel: true,
                hideFormulaHelp: true
            }
        );

        return container;
    }

    createValueConfigFormGroup(label, control, description = null) {
        const group = this.createFormGroup(label, control, description);
        group.classList.add('value-config-group');

        const syncFullWidth = () => {
            const hasDslEditor = !!control.querySelector('.dsl-editor-wrapper');
            group.classList.toggle('full-width', hasDslEditor);
        };

        syncFullWidth();
        if (typeof MutationObserver === 'function') {
            const observer = new MutationObserver(() => syncFullWidth());
            observer.observe(control, { childList: true, subtree: true });
        }

        return group;
    }

    createParameterLabel(title) {
        const header = document.createElement('div');
        header.className = 'parameter-header';

        const label = document.createElement('span');
        label.className = 'parameter-title';
        label.textContent = title;

        header.appendChild(label);
        return header;
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
        input.setAttribute('aria-label', labelText || '切换');
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

    normalizeBuffConfigList(list) {
        const rawList = Array.isArray(list) ? list : [];
        const normalized = [];

        rawList.forEach(raw => {
            const base = this.normalizeBuffConfig(raw);
            const types = [];
            if (base.statModifiers.length > 0) types.push('stat');
            if (base.keywordModifiers.length > 0) types.push('keyword');
            if (base.tribeModifiers.length > 0) types.push('tribe');
            if (base.abilityKeywordModifiers.length > 0) types.push('abilityKeyword');
            if (base.actionUsageLimitModifiers.length > 0) types.push('actionUsage');
            if (base.grantedAbilities.length > 0) types.push('ability');

            if (types.length === 0) {
                normalized.push({ ...base, _uiType: base._uiType || '' });
                return;
            }

            if (types.length === 1) {
                normalized.push({ ...base, _uiType: base._uiType || types[0] });
                return;
            }

            types.forEach(type => {
                const split = {
                    key: base.key,
                    durationType: base.durationType,
                    durationParameter: base.durationParameter,
                    canStack: base.canStack,
                    _uiType: type,
                    statModifiers: type === 'stat' ? base.statModifiers : [],
                    keywordModifiers: type === 'keyword' ? base.keywordModifiers : [],
                    tribeModifiers: type === 'tribe' ? base.tribeModifiers : [],
                    abilityKeywordModifiers: type === 'abilityKeyword' ? base.abilityKeywordModifiers : [],
                    actionUsageLimitModifiers: type === 'actionUsage' ? base.actionUsageLimitModifiers : [],
                    grantedAbilities: type === 'ability' ? base.grantedAbilities : [],
                    _statAdjustment: type === 'stat' ? base._statAdjustment : this.deriveStatAdjustmentState([]),
                    _statAdjustmentDirty: type === 'stat' ? (base._statAdjustmentDirty ?? false) : false
                };
                normalized.push(split);
            });
        });

        return normalized;
    }

    normalizeBuffConfig(raw) {
        if (!raw || typeof raw !== 'object') {
            return this.createDefaultBuffConfig();
        }

        const statModifiers = Array.isArray(raw.statModifiers ?? raw.StatModifiers)
            ? (raw.statModifiers ?? raw.StatModifiers).map(item => this.normalizeStatModifier(item))
            : [];
        const keywordModifiers = Array.isArray(raw.keywordModifiers ?? raw.KeywordModifiers)
            ? (raw.keywordModifiers ?? raw.KeywordModifiers).map(item => this.normalizeKeywordModifier(item))
            : [];
        const tribeModifiers = Array.isArray(raw.tribeModifiers ?? raw.TribeModifiers)
            ? (raw.tribeModifiers ?? raw.TribeModifiers).map(item => this.normalizeTribeModifier(item))
            : [];
        const abilityKeywordModifiers = Array.isArray(raw.abilityKeywordModifiers ?? raw.AbilityKeywordModifiers)
            ? (raw.abilityKeywordModifiers ?? raw.AbilityKeywordModifiers).map(item => this.normalizeAbilityKeywordModifier(item))
            : [];
        const actionUsageLimitModifiers = Array.isArray(raw.actionUsageLimitModifiers ?? raw.ActionUsageLimitModifiers)
            ? (raw.actionUsageLimitModifiers ?? raw.ActionUsageLimitModifiers).map(item => this.normalizeActionUsageLimitModifier(item))
            : [];
        const rawAbilities = raw.grantedAbilities ?? raw.GrantedAbilities;
        const grantedAbilities = Array.isArray(rawAbilities)
            ? rawAbilities.map(item => this.abilityListHelper.normalizeAbilityConfigForList(item)).filter(Boolean)
            : [];

        return {
            key: raw.key ?? raw.Key ?? '',
            durationType: raw.durationType ?? raw.DurationType ?? 'Permanent',
            durationParameter: raw.durationParameter ?? raw.DurationParameter ?? 0,
            removeCondition: raw.removeCondition ?? raw.RemoveCondition ?? null,
            canStack: raw.canStack ?? raw.CanStack ?? false,
            statModifiers,
            keywordModifiers,
            tribeModifiers,
            abilityKeywordModifiers,
            actionUsageLimitModifiers,
            grantedAbilities,
            _uiType: raw._uiType ?? raw.UiType ?? '',
            _statAdjustment: this.deriveStatAdjustmentState(statModifiers),
            _statAdjustmentDirty: false
        };
    }

    createDefaultBuffConfig() {
        return {
            key: '',
            durationType: 'Permanent',
            durationParameter: 0,
            removeCondition: null,
            canStack: false,
            statModifiers: [],
            keywordModifiers: [],
            tribeModifiers: [],
            abilityKeywordModifiers: [],
            actionUsageLimitModifiers: [],
            grantedAbilities: [],
            _uiType: '',
            _statAdjustment: this.deriveStatAdjustmentState([]),
            _statAdjustmentDirty: false
        };
    }

    normalizeStatModifier(raw) {
        if (!raw || typeof raw !== 'object') {
            return { stat: 'Attack', layer: 'Additive', value: null };
        }
        return {
            stat: raw.stat ?? raw.Stat ?? 'Attack',
            layer: raw.layer ?? raw.Layer ?? 'Additive',
            value: raw.value ?? raw.Value ?? null
        };
    }

    normalizeKeywordModifier(raw) {
        if (!raw || typeof raw !== 'object') {
            return { keyword: 'None', operation: 'Add' };
        }
        return {
            keyword: raw.keyword ?? raw.Keyword ?? 'None',
            operation: raw.operation ?? raw.Operation ?? 'Add'
        };
    }

    normalizeTribeModifier(raw) {
        if (!raw || typeof raw !== 'object') {
            return { tribe: '' };
        }
        return {
            tribe: (raw.tribe ?? raw.Tribe ?? '').toString().trim()
        };
    }

    normalizeAbilityKeywordModifier(raw) {
        if (!raw || typeof raw !== 'object') {
            return { keyword: 'None', layer: 'Additive', value: null };
        }
        return {
            keyword: raw.keyword ?? raw.Keyword ?? 'None',
            layer: raw.layer ?? raw.Layer ?? 'Additive',
            value: raw.value ?? raw.Value ?? null
        };
    }

    normalizeActionUsageLimitModifier(raw) {
        if (!raw || typeof raw !== 'object') {
            return { actionType: 'Attack', value: null };
        }
        return {
            actionType: raw.actionType ?? raw.ActionType ?? 'Attack',
            value: raw.value ?? raw.Value ?? null
        };
    }

    deriveStatAdjustmentState(statModifiers) {
        const state = {
            layer: 'Additive',
            attackModifier: null,
            healthModifier: null,
            costModifier: null,
            legacy: false
        };

        if (!Array.isArray(statModifiers) || statModifiers.length === 0) {
            return state;
        }

        const layerSet = new Set();
        statModifiers.forEach((modifier) => {
            if (!modifier || typeof modifier !== 'object') {
                return;
            }

            const stat = String(modifier.stat ?? modifier.Stat ?? '');
            const layer = modifier.layer ?? modifier.Layer ?? 'Additive';
            const value = modifier.value ?? modifier.Value ?? null;

            if (!['Attack', 'Health', 'Cost'].includes(stat)) {
                state.legacy = true;
                return;
            }

            if (layer) {
                layerSet.add(layer);
            }

            if (stat === 'Attack') {
                if (state.attackModifier !== null) state.legacy = true;
                state.attackModifier = state.attackModifier ?? value;
            } else if (stat === 'Health') {
                if (state.healthModifier !== null) state.legacy = true;
                state.healthModifier = state.healthModifier ?? value;
            } else if (stat === 'Cost') {
                if (state.costModifier !== null) state.legacy = true;
                state.costModifier = state.costModifier ?? value;
            }
        });

        if (layerSet.size === 1) {
            state.layer = [...layerSet][0];
        } else if (layerSet.size > 1) {
            state.layer = [...layerSet][0];
            state.legacy = true;
        }

        return state;
    }

    getStatAdjustmentState(buff) {
        if (!buff || typeof buff !== 'object') {
            return this.deriveStatAdjustmentState([]);
        }

        if (!buff._statAdjustment) {
            buff._statAdjustment = this.deriveStatAdjustmentState(buff.statModifiers || []);
            buff._statAdjustmentDirty = false;
        }

        return buff._statAdjustment;
    }

    updateStatAdjustment(buffIndex, updates) {
        const buff = this.buffConfigs[buffIndex];
        if (!buff) return;

        const current = this.getStatAdjustmentState(buff);
        const next = {
            ...current,
            ...updates,
            legacy: false
        };

        this.updateBuffConfig(buffIndex, {
            _uiType: 'stat',
            _statAdjustment: next,
            _statAdjustmentDirty: true
        });
    }

    updateStatAdjustmentValue(buffIndex, key, value) {
        const normalized = this.isValueConfigEmpty(value) ? null : value;
        this.updateStatAdjustment(buffIndex, { [key]: normalized });
    }

    getStatAdjustmentCount(buff) {
        const state = this.getStatAdjustmentState(buff);
        let count = 0;
        if (!this.isValueConfigEmpty(state.attackModifier)) count += 1;
        if (!this.isValueConfigEmpty(state.healthModifier)) count += 1;
        if (!this.isValueConfigEmpty(state.costModifier)) count += 1;
        return count;
    }

    buildStatModifiersFromAdjustment(state) {
        if (!state) return [];
        const layer = state.layer || 'Additive';
        const modifiers = [];

        if (!this.isValueConfigEmpty(state.attackModifier)) {
            modifiers.push({ stat: 'Attack', layer, value: state.attackModifier });
        }
        if (!this.isValueConfigEmpty(state.healthModifier)) {
            modifiers.push({ stat: 'Health', layer, value: state.healthModifier });
        }
        if (!this.isValueConfigEmpty(state.costModifier)) {
            modifiers.push({ stat: 'Cost', layer, value: state.costModifier });
        }

        return modifiers;
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

    addBuffConfig() {
        this.buffConfigs.push(this.createDefaultBuffConfig());
        this.updateBuffConfigList();
        this.notifyChange();
    }

    removeBuffConfig(index) {
        if (!confirm('确定删除该状态配置？')) return;
        this.buffConfigs.splice(index, 1);
        this.updateBuffConfigList();
        this.notifyChange();
    }

    updateBuffConfig(index, updates) {
        if (index < 0 || index >= this.buffConfigs.length) return;
        const previous = this.buffConfigs[index];
        const next = { ...previous, ...updates };
        this.buffConfigs[index] = next;

        const previousType = this.getBuffType(previous);
        const nextType = this.getBuffType(next);
        const previousDurationType = this.normalizeDurationType(previous.durationType);
        const nextDurationType = this.normalizeDurationType(next.durationType);
        const shouldRefreshNode = previousType !== nextType || previousDurationType !== nextDurationType;

        if (shouldRefreshNode) {
            this.updateBuffNode(index);
        } else {
            this.updateBuffHeader(index);
        }

        this.notifyChange();
    }

    updateBuffConfigList() {
        const list = this.container?.querySelector('#buff-config-list-container');
        if (list) {
            this.renderBuffConfigList(list);
        }
    }

    normalizeDurationType(value) {
        return this.abilityListHelper.normalizeDurationType(value);
    }

    updateBuffNode(index) {
        const list = this.container?.querySelector('#buff-config-list-container');
        if (!list) return;
        const existingNode = list.querySelector(`[data-buff-index="${index}"]`);
        if (!existingNode) return;

        const wasExpanded = existingNode.classList.contains('expanded');
        const optionsPanel = existingNode.querySelector('.effect-options-panel');
        const wasOptionsOpen = optionsPanel && !optionsPanel.classList.contains('collapsed');

        const newNode = this.createBuffConfigNode(this.buffConfigs[index], index);

        if (wasExpanded) {
            newNode.classList.add('expanded');
            const content = newNode.querySelector('.accordion-content');
            if (content) content.classList.remove('collapsed');
            const expandBtn = newNode.querySelector('.expand-btn');
            if (expandBtn) expandBtn.innerHTML = '▲';
        }

        if (wasOptionsOpen) {
            const nextOptions = newNode.querySelector('.effect-options-panel');
            if (nextOptions) nextOptions.classList.remove('collapsed');
            const settingsBtn = newNode.querySelector('.settings-btn');
            if (settingsBtn) settingsBtn.classList.add('active');
        }

        existingNode.replaceWith(newNode);
    }

    updateBuffHeader(index) {
        const list = this.container?.querySelector('#buff-config-list-container');
        if (!list) return;
        const node = list.querySelector(`[data-buff-index="${index}"]`);
        if (!node) return;

        const buff = this.buffConfigs[index];
        const type = this.getBuffType(buff);
        const count = this.getBuffTypeCount(buff, type);
        const typeLabel = this.getBuffTypeLabel(type);
        const summary = type ? `${typeLabel}${count}` : '未选择类型';
        const title = type ? `${typeLabel}状态` : '未选择状态类型';

        const titleEl = node.querySelector('.item-title');
        if (titleEl) titleEl.textContent = title;
        const statusEl = node.querySelector('.item-status');
        if (statusEl) statusEl.textContent = summary;
    }

    addStatModifier(buffIndex) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.statModifiers || [])];
        next.push({ stat: 'Attack', layer: 'Additive', value: null });
        this.updateBuffConfig(buffIndex, { statModifiers: next });
    }

    updateStatModifier(buffIndex, index, updates) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.statModifiers || [])];
        next[index] = { ...next[index], ...updates };
        this.updateBuffConfig(buffIndex, { statModifiers: next });
    }

    removeStatModifier(buffIndex, index) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.statModifiers || [])];
        next.splice(index, 1);
        this.updateBuffConfig(buffIndex, { statModifiers: next });
    }

    addKeywordModifier(buffIndex) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.keywordModifiers || [])];
        next.push({ keyword: 'None', operation: 'Add' });
        this.updateBuffConfig(buffIndex, { keywordModifiers: next });
    }

    updateKeywordModifier(buffIndex, index, updates) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.keywordModifiers || [])];
        next[index] = { ...next[index], ...updates };
        this.updateBuffConfig(buffIndex, { keywordModifiers: next });
    }

    removeKeywordModifier(buffIndex, index) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.keywordModifiers || [])];
        next.splice(index, 1);
        this.updateBuffConfig(buffIndex, { keywordModifiers: next });
    }

    addTribeModifier(buffIndex) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.tribeModifiers || [])];
        next.push({ tribe: '' });
        this.updateBuffConfig(buffIndex, { tribeModifiers: next });
    }

    updateTribeModifier(buffIndex, index, updates) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.tribeModifiers || [])];
        next[index] = { ...next[index], ...updates };
        this.updateBuffConfig(buffIndex, { tribeModifiers: next });
    }

    removeTribeModifier(buffIndex, index) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.tribeModifiers || [])];
        next.splice(index, 1);
        this.updateBuffConfig(buffIndex, { tribeModifiers: next });
    }

    addAbilityKeywordModifier(buffIndex) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.abilityKeywordModifiers || [])];
        next.push({ keyword: 'None', layer: 'Additive', value: null });
        this.updateBuffConfig(buffIndex, { abilityKeywordModifiers: next });
    }

    updateAbilityKeywordModifier(buffIndex, index, updates) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.abilityKeywordModifiers || [])];
        next[index] = { ...next[index], ...updates };
        this.updateBuffConfig(buffIndex, { abilityKeywordModifiers: next });
    }

    removeAbilityKeywordModifier(buffIndex, index) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.abilityKeywordModifiers || [])];
        next.splice(index, 1);
        this.updateBuffConfig(buffIndex, { abilityKeywordModifiers: next });
    }

    addActionUsageModifier(buffIndex) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.actionUsageLimitModifiers || [])];
        next.push({ actionType: 'Attack', value: null });
        this.updateBuffConfig(buffIndex, { actionUsageLimitModifiers: next });
    }

    updateActionUsageModifier(buffIndex, index, updates) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.actionUsageLimitModifiers || [])];
        next[index] = { ...next[index], ...updates };
        this.updateBuffConfig(buffIndex, { actionUsageLimitModifiers: next });
    }

    removeActionUsageModifier(buffIndex, index) {
        const buff = this.buffConfigs[buffIndex];
        const next = [...(buff.actionUsageLimitModifiers || [])];
        next.splice(index, 1);
        this.updateBuffConfig(buffIndex, { actionUsageLimitModifiers: next });
    }

    buildBuffConfigsForSave(buffConfigs) {
        const list = Array.isArray(buffConfigs) ? buffConfigs : [];
        return list.map(item => {
            const type = this.getBuffType(item);
            let statModifiers = [];
            if (type === 'stat') {
                const state = item._statAdjustment ?? this.deriveStatAdjustmentState(item.statModifiers || []);
                if (state.legacy && !item._statAdjustmentDirty && Array.isArray(item.statModifiers)) {
                    statModifiers = item.statModifiers;
                } else {
                    statModifiers = this.buildStatModifiersFromAdjustment(state);
                }
            }
            return {
                key: item.key ?? '',
                durationType: item.durationType ?? 'Permanent',
                durationParameter: item.durationParameter ?? 0,
                canStack: item.canStack ?? false,
                statModifiers: type === 'stat' ? statModifiers : [],
                keywordModifiers: type === 'keyword' ? (Array.isArray(item.keywordModifiers) ? item.keywordModifiers : []) : [],
                tribeModifiers: type === 'tribe'
                    ? (Array.isArray(item.tribeModifiers) ? item.tribeModifiers.filter(modifier => String(modifier?.tribe ?? '').trim() !== '') : [])
                    : [],
                abilityKeywordModifiers: type === 'abilityKeyword' ? (Array.isArray(item.abilityKeywordModifiers) ? item.abilityKeywordModifiers : []) : [],
                actionUsageLimitModifiers: type === 'actionUsage'
                    ? (Array.isArray(item.actionUsageLimitModifiers) ? item.actionUsageLimitModifiers : [])
                    : [],
                grantedAbilities: type === 'ability' ? (Array.isArray(item.grantedAbilities) ? item.grantedAbilities : []) : []
            };
        });
    }

    validateBuffConfigs() {
        const errors = [];
        this.buffConfigs.forEach((buff, index) => {
            const type = this.getBuffType(buff);
            if (!type) {
                errors.push(`状态 #${index + 1}: 请选择状态类型`);
                return;
            }
            const count = this.getBuffTypeCount(buff, type);
            if (type !== 'ability' && count === 0) {
                errors.push(`状态 #${index + 1}: 请添加至少一条${this.getBuffTypeLabel(type)}修正`);
            }
        });
        return errors;
    }

    notifyChange() {
        if (this.onChange) {
            this.onChange(this.buffConfigs);
        }
    }
}
