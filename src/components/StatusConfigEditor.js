// src/components/StatusConfigEditor.js - 状态(Buff)配置编辑器
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';
import { EffectConfigEditor } from './EffectConfigEditor.js';
import { ensureEnumLocalization } from '../enumLocalizationCache.js';

/**
 * 状态(Buff)配置编辑器
 * 采用手风琴式列表，支持多条Buff配置，并可配置属性/关键词/能力三类效果
 */
export class StatusConfigEditor extends MetadataDrivenComponent {
    constructor() {
        super();
        this.buffConfigs = [];
        this.onChange = null;
        this.container = null;
        this.options = {};
        this.abilityListHelper = new EffectConfigEditor();
    }

    async render(container, buffConfigs = [], onChange = () => {}, options = {}) {
        await this.initialize();
        await this.abilityListHelper.initialize();

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
        if (Array.isArray(buff.grantedAbilities) && buff.grantedAbilities.length > 0) return 'ability';
        return '';
    }

    getBuffTypeLabel(type) {
        switch (type) {
            case 'stat':
                return '属性';
            case 'keyword':
                return '关键词';
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
                grantedAbilities: []
            });
            return;
        }
        if (type === 'ability') {
            this.updateBuffConfig(index, {
                _uiType: 'ability',
                statModifiers: [],
                keywordModifiers: [],
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
        const attackInput = this.createNumberInput(
            this.coerceNumericValue(state.attackModifier),
            (value) => this.updateStatAdjustmentValue(buffIndex, 'attackModifier', value),
            { placeholder: '（空）' }
        );
        attackControl.appendChild(attackInput);
        grid.appendChild(this.createFormGroup(this.createParameterLabel('攻击力修正'), attackControl));

        const healthControl = document.createElement('div');
        healthControl.className = 'parameter-control';
        const healthInput = this.createNumberInput(
            this.coerceNumericValue(state.healthModifier),
            (value) => this.updateStatAdjustmentValue(buffIndex, 'healthModifier', value),
            { placeholder: '（空）' }
        );
        healthControl.appendChild(healthInput);
        grid.appendChild(this.createFormGroup(this.createParameterLabel('生命值修正'), healthControl));

        const costControl = document.createElement('div');
        costControl.className = 'parameter-control';
        const costInput = this.createNumberInput(
            this.coerceNumericValue(state.costModifier),
            (value) => this.updateStatAdjustmentValue(buffIndex, 'costModifier', value),
            { placeholder: '（空）' }
        );
        costControl.appendChild(costInput);
        grid.appendChild(this.createFormGroup(this.createParameterLabel('费用修正'), costControl));

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
        const rawAbilities = raw.grantedAbilities ?? raw.GrantedAbilities;
        const grantedAbilities = Array.isArray(rawAbilities)
            ? rawAbilities.map(item => this.abilityListHelper.normalizeAbilityConfigForList(item)).filter(Boolean)
            : [];

        return {
            key: raw.key ?? raw.Key ?? '',
            durationType: raw.durationType ?? raw.DurationType ?? 'Permanent',
            durationParameter: raw.durationParameter ?? raw.DurationParameter ?? 0,
            canStack: raw.canStack ?? raw.CanStack ?? false,
            statModifiers,
            keywordModifiers,
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
            canStack: false,
            statModifiers: [],
            keywordModifiers: [],
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
        const shouldRefreshNode = previousType !== nextType;

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
