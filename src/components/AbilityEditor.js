// src/components/AbilityEditor.js - 主要的能力编辑器组件 (Wizard Mode)
//
// 相关文档:
// - DSL映射规范: 文档/06-UI设计/03-能力DSL与UI节点映射规范.md
// - 元数据驱动UI: 文档/06-UI设计/05-元数据驱动UI开发实践指南.md
//

import { MetadataDrivenComponent } from './MetadataDrivenComponent.js';
import { TriggerConfigEditor } from './TriggerConfigEditor.js';
import { ConditionConfigEditor } from './ConditionConfigEditor.js';
import { TargetConfigEditor } from './TargetConfigEditor.js';
import { EffectConfigEditor } from './EffectConfigEditor.js';
import { StatusConfigEditor } from './StatusConfigEditor.js';
import { validateDslExpression } from '../dslValidation.js';
import { getBlockingLoader } from './blockingLoader.js';
import { ensureEnumLocalization } from '../enumLocalizationCache.js';

/**
 * 主要的能力编辑器组件
 * 采用分步向导（Wizard）模式，引导用户完成能力配置
 */
export class AbilityEditor extends MetadataDrivenComponent {
    constructor() {
        super();
        this.ability = null;
        this.onChange = null;
        this.container = null;
        this.options = {};
        
        // 子编辑器实例
        this.triggerEditor = new TriggerConfigEditor();
        this.conditionEditor = new ConditionConfigEditor();
        this.targetEditor = new TargetConfigEditor();
        this.effectEditor = new EffectConfigEditor();
        this.statusEditor = new StatusConfigEditor();
        this.editorMode = 'effect';

        // 向导状态
        this.currentStepIndex = 0;
        this.blockingLoader = null;
        this.steps = [
            { id: 'basic', label: '基础信息', description: '定义名称与类型' },
            { id: 'trigger', label: '触发时机', description: '何时发动能力' },
            { id: 'targets', label: '目标选择', description: '选择卡牌目标' },
            { id: 'conditions', label: '发动条件', description: '额外的限制' },
            { id: 'effects', label: '效果配置', description: '具体产生的影响' }
        ];
    }

    /**
     * 渲染能力编辑器
     * @param {HTMLElement} container - 容器元素
     * @param {Object} ability - 能力配置对象
     * @param {Function} onChange - 配置变化回调
     */
    async render(container, ability = {}, onChange = () => {}, options = {}) {
        await this.initialize();
        
        this.container = container;
        this.ability = this.normalizeAbility(ability);
        this.onChange = onChange;
        this.options = options || {};
        this.applyLockedTrigger();
        this.editorMode = this.resolveEditorMode();
        this.configureStepsForMode();

        this.clearContainer(container);
        
        try {
            // 创建编辑器主体结构
            this.renderEditorStructure();

            await this.runWithBlockingLoader("正在加载编辑器...", async () => {
                // 渲染当前步骤
                await this.renderCurrentStep();
            });
        } catch (error) {
            console.error('AbilityEditor: Render failed:', error);
            this.showError(`渲染能力编辑器失败: ${error.message}`, container);
        }
    }

    /**
     * 标准化能力配置对象
     */
    normalizeAbility(ability) {
        const auraConfig = this.normalizeAuraConfig(ability);
        const rawTrigger = ability.trigger || ability.Trigger || {};
        const trigger = {
            ...rawTrigger,
            eventTypes: rawTrigger.eventTypes || rawTrigger.EventTypes || [],
            relationType: rawTrigger.relationType || rawTrigger.RelationType || 'Any',
            timing: rawTrigger.timing || rawTrigger.Timing || 'On',
            validZones: rawTrigger.validZones || rawTrigger.ValidZones || [],
            revalidateZoneBeforeExecution: rawTrigger.revalidateZoneBeforeExecution ?? rawTrigger.RevalidateZoneBeforeExecution ?? false
        };
        const legacyExecutionKind = ability.executionKind || ability.ExecutionKind || '';
        const normalizedType = ability.type || ability.Type
            || (String(legacyExecutionKind).toLowerCase() === 'aura' ? 'Continuous' : '');
        return {
            id: ability.id || '',
            name: ability.name || '',
            description: ability.description || '',
            abilityKeywords: this.normalizeAbilityKeywords(ability),
            abilityType: ability.abilityType || 1,
            priority: ability.priority || 0,
            isActive: ability.isActive !== false,
            cardName: ability.cardName || ability.CardName || ability.card?.name || '',
            abilitySequence: ability.abilitySequence ?? ability.sequence ?? null,
            type: normalizedType,
            executionKind: legacyExecutionKind,
            trigger,
            conditions: ability.conditions || [],
            targets: ability.targets || [],
            effects: (ability.effects && ability.effects.length > 0) ? ability.effects : [{ type: '', parameters: {} }],
            auraConfig,
            auras: ability.auras || ability.Auras || [],
            counterRegistrations: this.mergeCounterRegistrations(
                ability.counterRegistrations || ability.CounterRegistrations || [],
                ability.playerCounterRegistrations || ability.PlayerCounterRegistrations || []
            )
        };
    }

    mergeCounterRegistrations(cardRegistrations, playerRegistrations) {
        const cardItems = this.normalizeCounterRegistrations(cardRegistrations, 'Card');
        const playerItems = this.normalizeCounterRegistrations(playerRegistrations, 'Player');
        return [...cardItems, ...playerItems];
    }

    normalizeCounterRegistrations(registrations, defaultTargetType = 'Card') {
        if (!Array.isArray(registrations)) {
            return [];
        }

        return registrations.map((entry) => this.normalizeSingleCounterRegistration(entry, defaultTargetType));
    }

    normalizeSingleCounterRegistration(entry, defaultTargetType = 'Card') {
        const name = String(entry?.name ?? entry?.Name ?? '').trim();
        const rawDisplayName = entry?.displayName ?? entry?.DisplayName;
        const displayName = String(rawDisplayName ?? '').trim() || name;
        const rawInitialValue = Number(entry?.initialValue ?? entry?.InitialValue ?? 0);
        const initialValue = Number.isFinite(rawInitialValue) ? Math.trunc(rawInitialValue) : 0;
        const targetTypeRaw = String(entry?.targetType ?? entry?.TargetType ?? defaultTargetType ?? 'Card').trim();
        const targetType = targetTypeRaw.toLowerCase() === 'player' ? 'Player' : 'Card';
        const playerScopeRaw = String(entry?.playerScope ?? entry?.PlayerScope ?? 'Self').trim();
        const playerScope = ['Self', 'Opponent', 'Both'].includes(playerScopeRaw) ? playerScopeRaw : 'Self';

        return {
            name,
            displayName,
            initialValue,
            targetType,
            playerScope
        };
    }

    applyLockedTrigger() {
        const locked = this.options?.lockedTrigger;
        if (!locked) {
            return;
        }

        const eventTypes = Array.isArray(locked.eventTypes) ? locked.eventTypes : [];
        this.ability.trigger = {
            ...this.ability.trigger,
            ...locked,
            eventTypes: eventTypes.length > 0 ? eventTypes : this.ability.trigger?.eventTypes || []
        };
    }

    normalizeAbilityKeywords(ability) {
        const rawKeywords = ability?.abilityKeywords ?? [];
        const normalized = [];

        const addKeyword = (keyword, value) => {
            if (!keyword || typeof keyword !== 'string') return;
            const existing = normalized.find(k => k.keyword.toLowerCase() === keyword.toLowerCase());
            if (existing) {
                if (value !== null && value !== undefined) {
                    existing.value = value;
                }
                return;
            }
            normalized.push({ keyword, value: value ?? null });
        };

        if (Array.isArray(rawKeywords)) {
            rawKeywords.forEach(entry => {
                if (!entry) return;
                if (typeof entry === 'string') {
                    addKeyword(entry);
                    return;
                }

                if (typeof entry === 'object') {
                    const keyword = entry.keyword || entry.tag || entry.Keyword || entry.Tag;
                    const value = entry.value ?? entry.amount ?? entry.cost ?? entry.Value ?? null;
                    addKeyword(keyword, value);
                }
            });
        } else if (typeof rawKeywords === 'string') {
            addKeyword(rawKeywords);
        }

        return normalized;
    }

    isCostKeyword(keyword) {
        if (!keyword || typeof keyword !== 'string') return false;
        const normalized = keyword.toLowerCase();
        return ['enhance', 'act', 'necromance', 'sacrifice', 'earthritual'].includes(normalized);
    }

    getDefaultAbilityName() {
        const rawName = (this.ability.cardName || '').trim();
        const seqRaw = this.ability.abilitySequence;
        const seq = Number.isFinite(seqRaw)
            ? seqRaw
            : parseInt(seqRaw, 10);
        const sequence = Number.isFinite(seq) && seq > 0 ? seq : 1;
        const baseName = rawName || 'Ability';
        return `${baseName}${sequence}`;
    }

    /**
     * 渲染编辑器主体结构 (Wizard Layout)
     */
    renderEditorStructure() {
        const editorHTML = `
            <div class="ability-editor wizard-mode">
                <div class="wizard-header">
                    <div class="wizard-title">
                        <h3>能力编辑器</h3>
                        <span class="wizard-subtitle">创建新的卡牌能力</span>
                    </div>
                    <div class="wizard-progress">
                        ${this.steps.map((step, index) => `
                            <div class="progress-step ${index === 0 ? 'active' : ''}" data-step="${index}">
                                <div class="step-indicator">${index + 1}</div>
                                <div class="step-label">
                                    <span class="step-name">${step.label}</span>
                                    <span class="step-desc">${step.description}</span>
                                </div>
                            </div>
                        `).join('')}
                    </div>
                </div>

                <div class="wizard-content custom-scrollbar" id="wizard-content-area">
                    <!-- 动态内容区域 -->
                </div>

                <div class="wizard-footer">
                    <div class="footer-left">
                        <button class="btn-secondary" id="wizard-cancel">取消</button>
                    </div>
                    <div class="footer-right">
                        <button class="btn-secondary" id="wizard-back" disabled>上一步</button>
                        <button class="btn-primary" id="wizard-next">下一步</button>
                        <button class="btn-success" id="wizard-finish" style="display: none;">完成</button>
                    </div>
                </div>
            </div>
        `;

        this.container.innerHTML = editorHTML;

        // 绑定全局导航事件
        this.bindNavigationEvents();
    }

    /**
     * 绑定导航事件
     */
    bindNavigationEvents() {
        const backBtn = this.container.querySelector('#wizard-back');
        const nextBtn = this.container.querySelector('#wizard-next');
        const finishBtn = this.container.querySelector('#wizard-finish');
        const cancelBtn = this.container.querySelector('#wizard-cancel');

        backBtn.addEventListener('click', () => this.handleBack());
        nextBtn.addEventListener('click', () => this.handleNext());
        finishBtn.addEventListener('click', () => this.handleFinish());
        cancelBtn.addEventListener('click', () => this.handleCancel());
    }

    /**
     * 渲染当前步骤内容
     */
    async renderCurrentStep() {
        const contentArea = this.container.querySelector('#wizard-content-area');
        const currentStepId = this.steps[this.currentStepIndex].id;
        
        // 添加过渡动画类
        contentArea.classList.add('fade-out');
        
        // 等待淡出动画
        await new Promise(resolve => setTimeout(resolve, 200));
        
        this.clearContainer(contentArea);
        contentArea.classList.remove('fade-out');
        contentArea.classList.add('fade-in');

        // 渲染具体内容
        switch (currentStepId) {
            case 'basic':
                await this.renderBasicInfoStep(contentArea);
                break;
            case 'trigger':
                await this.triggerEditor.render(
                    contentArea,
                    this.ability.trigger,
                    (config) => this.updateAbility({ trigger: config }),
                    {
                        ...this.getTriggerEditorOptions(),
                        lockedConfig: this.options?.lockedTrigger,
                        lockMessage: this.options?.lockedTriggerMessage,
                        lockValidZones: this.options?.lockValidZones
                    }
                );
                break;
            case 'targets':
                await this.targetEditor.render(
                    contentArea,
                    this.ability.targets,
                    (targets) => this.updateAbility({ targets: targets }),
                    {
                        trigger: this.ability.trigger,
                        triggeringEventOutputs: this.options?.triggeringEventOutputs || [],
                        ...this.getTargetEditorOptions()
                    }
                );
                break;
            case 'conditions':
                await this.conditionEditor.render(
                    contentArea,
                    this.ability.conditions,
                    (conditions) => this.updateAbility({ conditions: conditions }),
                    {
                        trigger: this.ability.trigger,
                        triggeringEventOutputs: this.options?.triggeringEventOutputs || [],
                        ...this.getConditionEditorOptions()
                    }
                );
                break;
            case 'effects':
                if (this.isAuraMode()) {
                    const auraConfig = this.ability.auraConfig || { buffConfigs: [] };
                    await this.statusEditor.render(
                        contentArea,
                        auraConfig.buffConfigs || [],
                        (buffConfigs) => {
                            this.updateAbility({
                                auraConfig: {
                                    ...auraConfig,
                                    buffConfigs: buffConfigs || []
                                }
                            });
                        },
                        this.getStatusEditorOptions()
                    );
                } else {
                    await this.effectEditor.render(
                        contentArea,
                        this.ability.effects,
                        (effects) => this.updateAbility({ effects: effects }),
                        {
                            trigger: this.ability.trigger,
                            targets: this.ability.targets,
                            triggeringEventOutputs: this.options?.triggeringEventOutputs || []
                        }
                    );
                }
                break;
        }

        // 更新UI状态（进度条、按钮）
        this.updateWizardUI();
        
        // 移除淡入类
        setTimeout(() => contentArea.classList.remove('fade-in'), 300);
    }

    /**
     * 更新向导UI状态
     */
    updateWizardUI() {
        // 更新进度条
        const progressSteps = this.container.querySelectorAll('.progress-step');
        progressSteps.forEach((step, index) => {
            step.classList.remove('active', 'completed');
            if (index === this.currentStepIndex) {
                step.classList.add('active');
            } else if (index < this.currentStepIndex) {
                step.classList.add('completed');
            }
        });

        // 更新按钮状态
        const backBtn = this.container.querySelector('#wizard-back');
        const nextBtn = this.container.querySelector('#wizard-next');
        const finishBtn = this.container.querySelector('#wizard-finish');

        backBtn.disabled = this.currentStepIndex === 0;
        
        if (this.currentStepIndex === this.steps.length - 1) {
            nextBtn.style.display = 'none';
            finishBtn.style.display = 'inline-block';
        } else {
            nextBtn.style.display = 'inline-block';
            finishBtn.style.display = 'none';
        }
    }

    /**
     * 处理下一步
     */
    async handleNext() {
        if (this.currentStepIndex < this.steps.length && this.steps[this.currentStepIndex].id === 'targets') {
            await this.flushTargetEditorsWithLoader();
        }

        // 验证当前步骤
        const errors = await this.validateCurrentStep();
        if (errors.length > 0) {
            alert('请修正以下问题后再继续:\n' + errors.join('\n'));
            return;
        }

        if (this.currentStepIndex < this.steps.length - 1) {
            this.currentStepIndex++;
            await this.renderCurrentStep();
        }
    }

    /**
     * 处理上一步
     */
    async handleBack() {
        if (this.currentStepIndex > 0) {
            this.currentStepIndex--;
            await this.renderCurrentStep();
        }
    }

    /**
     * 处理完成
     */
    async handleFinish() {
        await this.flushTargetEditorsWithLoader();

        const validation = await this.validateAbilityBeforeSave();
        if (validation.errors.length > 0) {
            alert('请修正以下问题后再完成:\n' + validation.errors.map(i => `- ${i.message}`).join('\n'));
            return;
        }
        if (validation.warns.length > 0) {
            const ok = confirm('存在警告，仍要继续保存吗？\n' + validation.warns.map(i => `- ${i.message}`).join('\n'));
            if (!ok) return;
        }

        try {
            const finalConfig = this.buildFinalConfig();
            if (this.onChange) {
                await this.runWithBlockingLoader("正在保存...", async () => {
                    await this.onChange(finalConfig);
                });
            }
        } catch (error) {
            console.error('AbilityEditor: Save failed:', error);
            alert(`保存失败: ${error.message}`);
        }
    }

    /**
     * 处理取消
     */
    handleCancel() {
        if (confirm('确定要取消编辑吗？未保存的更改将丢失。')) {
            if (this.onChange) {
                this.onChange(null);
            }
        }
    }

    /**
     * 验证当前步骤
     */
    async validateCurrentStep() {
        const currentStepId = this.steps[this.currentStepIndex].id;
        const errors = [];

        switch (currentStepId) {
            case 'basic':
                // 能力名称不再必填，如果为空会自动生成默认名称
                break;
            case 'trigger':
                errors.push(...this.triggerEditor.validateConfig());
                break;
            case 'targets':
                errors.push(...(await this.collectTargetDslIssues()).errors);
                break;
            case 'conditions':
                errors.push(...this.conditionEditor.validateConditions());
                errors.push(...(await this.collectConditionDslIssues()).errors);
                break;
            case 'effects':
                if (this.isAuraMode()) {
                    errors.push(...this.statusEditor.validateBuffConfigs());
                } else {
                    errors.push(...this.effectEditor.validateEffects());
                    errors.push(...(await this.collectEffectDslIssues()).errors);
                }
                break;
        }

        return errors;
    }

    async collectConditionDslIssues() {
        const issues = { errors: [], warns: [], infos: [] };
        const pushError = (message) => issues.errors.push(message);
        const triggeringEventContext = this.getTriggeringEventContextForDsl();

        const conditions = this.ability.conditions || [];
        for (let index = 0; index < conditions.length; index++) {
            const condition = conditions[index];
            if (condition?.isEnabled === false) continue;
            const expr = condition?.expression || '';
            const res = await validateDslExpression(this.metadata, expr, {
                expectedType: 'Boolean',
                allowEmpty: false,
                triggeringEventContext
            });
            for (const issue of res.issues) {
                pushError(`条件 #${index + 1}: ${issue.message}`);
            }
        }

        return issues;
    }

    async collectTargetDslIssues() {
        const issues = { errors: [], warns: [], infos: [] };
        const pushError = (message) => issues.errors.push(message);
        const pushWarn = (message) => issues.warns.push(message);
        const triggeringEventContext = this.getTriggeringEventContextForDsl();

        const targets = this.ability.targets || [];
        for (let index = 0; index < targets.length; index++) {
            const target = targets[index];
            if (target?.isEnabled === false) {
                continue;
            }
            const sourceExpr = target?.query?.sourcePoolExpression || '';
            if (sourceExpr.trim()) {
                const res = await validateDslExpression(this.metadata, sourceExpr, {
                    expectedType: 'List<CardInstance>',
                    allowEmpty: false,
                    triggeringEventContext
                });
                for (const issue of res.issues) {
                    pushError(`目标 #${index + 1} 起始卡池: ${issue.message}`);
                }
            } else if (target?.query) {
                pushWarn(`目标 #${index + 1}: 未配置起始卡池表达式`);
            }

            const operations = target?.query?.operations || [];
            for (let opIdx = 0; opIdx < operations.length; opIdx++) {
                const op = operations[opIdx];
                if (op?.$type !== 'FilterOperation') continue;
                const filters = op.filters || [];
                for (let filterIdx = 0; filterIdx < filters.length; filterIdx++) {
                    const filter = filters[filterIdx];
                    const filterExpr = filter?.valueSource?.formula || '';
                    const res = await validateDslExpression(this.metadata, filterExpr, {
                        expectedType: 'Boolean',
                        allowEmpty: false,
                        triggeringEventContext
                    });
                    for (const issue of res.issues) {
                        pushError(`目标 #${index + 1} 筛选 #${opIdx + 1}.${filterIdx + 1}: ${issue.message}`);
                    }
                }
            }
        }

        return issues;
    }

    async collectEffectDslIssues() {
        const issues = { errors: [], warns: [], infos: [] };
        const pushError = (message) => issues.errors.push(message);
        const triggeringEventContext = this.getTriggeringEventContextForDsl();

        const effects = this.ability.effects || [];
        for (let index = 0; index < effects.length; index++) {
            const effect = effects[index];
            if (effect?.isEnabled === false) continue;
            if (!effect?.type) continue;

            const effectMeta = (this.metadata?.effects || []).find(e => e.effectTypeName === effect.type);
            const paramsMeta = effectMeta?.parameters || [];
            const parameters = effect.parameters || {};

            for (const [paramName, valueConfig] of Object.entries(parameters)) {
                if (!valueConfig || typeof valueConfig !== 'object') continue;
                if (valueConfig.$type !== 'FormulaValueConfig') continue;
                const formula = valueConfig.Formula || '';

                const meta = paramsMeta.find(p => {
                    const name = p.propertyName || p.PropertyName || p.name;
                    return String(name) === String(paramName);
                });
                const expectedType = meta?.dslType || meta?.DslType || meta?.typeName || meta?.TypeName || null;

                const res = await validateDslExpression(this.metadata, formula, {
                    expectedType,
                    allowEmpty: false,
                    triggeringEventContext
                });
                for (const issue of res.issues) {
                    pushError(`效果 #${index + 1} 参数 ${paramName} 公式: ${issue.message}`);
                }
            }

            const outputs = effect.outputs && typeof effect.outputs === 'object'
                ? effect.outputs
                : ((effect.Outputs && typeof effect.Outputs === 'object') ? effect.Outputs : {});
            const outputKeysSeen = new Set();
            for (const [rawOutputName, rawFormula] of Object.entries(outputs)) {
                const outputName = String(rawOutputName || '').trim();
                if (!outputName) {
                    pushError(`效果 #${index + 1} 输出变量名不能为空`);
                    continue;
                }

                const outputKey = outputName.toLowerCase();
                if (outputKeysSeen.has(outputKey)) {
                    pushError(`效果 #${index + 1} 输出变量 ${outputName} 重复`);
                    continue;
                }
                outputKeysSeen.add(outputKey);

                const formula = String(rawFormula || '');
                const res = await validateDslExpression(this.metadata, formula, {
                    allowEmpty: false,
                    triggeringEventContext
                });
                for (const issue of res.issues) {
                    pushError(`效果 #${index + 1} 输出 ${outputName} 公式: ${issue.message}`);
                }
            }
        }

        return issues;
    }

    getTriggeringEventContextForDsl() {
        const trigger = this.ability?.trigger || null;
        const eventTypes = Array.isArray(trigger?.eventTypes) ? trigger.eventTypes : [];
        if (!eventTypes.length) {
            return null;
        }
        const outputVariables = Array.isArray(this.options?.triggeringEventOutputs)
            ? this.options.triggeringEventOutputs
            : [];
        return {
            eventTypes,
            timing: trigger?.timing || 'On',
            outputVariables
        };
    }

    async validateAbilityBeforeSave() {
        const issues = { errors: [], warns: [], infos: [] };
        const pushError = (message) => issues.errors.push({ severity: 'error', message });
        const pushWarn = (message) => issues.warns.push({ severity: 'warn', message });

        // Trigger: required
        for (const msg of this.triggerEditor.validateConfig()) {
            pushError(msg);
        }

        if (this.isAuraMode()) {
            for (const msg of this.statusEditor.validateBuffConfigs()) {
                pushError(msg);
            }
        } else {
            (this.ability.effects || []).forEach((effect, index) => {
                if (effect?.isEnabled === false) {
                    return;
                }
                if (!effect?.type) {
                    pushError(`效果 #${index + 1}: 必须选择效果类型`);
                }
            });
        }

        const conditionIssues = await this.collectConditionDslIssues();
        for (const err of conditionIssues.errors) pushError(err);

        const targetIssues = await this.collectTargetDslIssues();
        for (const err of targetIssues.errors) pushError(err);
        for (const warn of targetIssues.warns) pushWarn(warn);

        if (!this.isAuraMode()) {
            const effectIssues = await this.collectEffectDslIssues();
            for (const err of effectIssues.errors) pushError(err);
        }

        return issues;
    }

    /**
     * 渲染基础信息步骤 (重构版)
     */
    async renderBasicInfoStep(container) {
        const wrapper = document.createElement('div');
        wrapper.className = 'step-content-wrapper basic-info-step';

        // 标题区域
        const header = document.createElement('div');
        header.className = 'step-header';
        header.innerHTML = `
            <h4>基础信息配置</h4>
            <p class="step-intro">定义这个能力的基本属性。该部分信息将用于游戏系统如何识别和处理这个能力。</p>
        `;
        wrapper.appendChild(header);

        // 表单区域
        const formGrid = document.createElement('div');
        formGrid.className = 'form-grid-layout';

        // 1. 能力名称 (全宽)
        const defaultName = this.getDefaultAbilityName();
        const nameInput = this.createTextInput(
            this.ability.name,
            (value) => this.updateAbility({ name: value }),
            { placeholder: `${defaultName}` }
        );
        nameInput.className = 'form-input large-input';
        const nameLabel = document.createElement('span');
        nameLabel.innerHTML = '能力名称';
        const nameGroup = this.createFormGroup(nameLabel, nameInput, '留空将自动生成');
        nameGroup.className += ' full-width';
        formGrid.appendChild(nameGroup);

        // 2. 激活状态 (开关) - 置于关键字上方
        const activeSwitch = this.createToggleSwitch(
            this.ability.isActive,
            (checked) => this.updateAbility({ isActive: checked }),
            '立即启用此能力'
        );
        const activeGroup = this.createFormGroup('状态', activeSwitch);
        // activeGroup.className += ' full-width'; // 不再强制全宽，使其适应网格布局
        formGrid.appendChild(activeGroup);

        // 3. 能力关键字 (全宽，横向排列)
        const abilityTagOptions = await this.loadAbilityTagOptions();
        const keywordOptions = abilityTagOptions.filter(opt => opt.value && String(opt.value).toLowerCase() !== 'none');
        const keywordContainer = document.createElement('div');
        keywordContainer.className = 'keyword-grid';

        const updateKeywordSelection = (keyword, checked) => {
            const current = Array.isArray(this.ability.abilityKeywords) ? [...this.ability.abilityKeywords] : [];
            const existingIndex = current.findIndex(k => (k.keyword || '').toLowerCase() === keyword.toLowerCase());
            if (checked) {
                if (existingIndex === -1) {
                    current.push({ keyword, value: this.isCostKeyword(keyword) ? 0 : null });
                }
            } else if (existingIndex >= 0) {
                current.splice(existingIndex, 1);
            }
            this.updateAbility({ abilityKeywords: current });
        };

        const updateKeywordValue = (keyword, value) => {
            const current = Array.isArray(this.ability.abilityKeywords) ? [...this.ability.abilityKeywords] : [];
            const existingIndex = current.findIndex(k => (k.keyword || '').toLowerCase() === keyword.toLowerCase());
            if (existingIndex >= 0) {
                current[existingIndex] = { ...current[existingIndex], value };
                this.updateAbility({ abilityKeywords: current });
            }
        };

        keywordOptions.forEach(option => {
            const keyword = option.value;
            const entry = (this.ability.abilityKeywords || []).find(k => (k.keyword || '').toLowerCase() === keyword.toLowerCase());
            const isChecked = Boolean(entry);

            const item = document.createElement('label');
            item.className = 'keyword-item';

            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.checked = isChecked;
            checkbox.addEventListener('change', () => {
                updateKeywordSelection(keyword, checkbox.checked);
                const showValue = this.isCostKeyword(keyword) && checkbox.checked;
                valueInput.disabled = !showValue;
                valueInput.classList.toggle('hidden', !showValue);
            });

            const label = document.createElement('span');
            label.textContent = option.label;

            const valueInput = document.createElement('input');
            valueInput.type = 'number';
            valueInput.className = 'keyword-value-input';
            valueInput.placeholder = this.isCostKeyword(keyword) ? '数值' : '';
            valueInput.disabled = !this.isCostKeyword(keyword) || !isChecked;
            valueInput.classList.toggle('hidden', !this.isCostKeyword(keyword) || !isChecked);
            if (entry && entry.value !== null && entry.value !== undefined) {
                valueInput.value = entry.value;
            }
            valueInput.addEventListener('change', () => {
                const raw = valueInput.value.trim();
                const parsed = raw === '' ? null : parseInt(raw, 10);
                updateKeywordValue(keyword, Number.isNaN(parsed) ? null : parsed);
            });

            item.appendChild(checkbox);
            item.appendChild(label);
            item.appendChild(valueInput);
            keywordContainer.appendChild(item);
        });

        const tagGroup = this.createFormGroup('能力关键字', keywordContainer, '可多选，部分关键字可填写数值参数');
        tagGroup.classList.add('full-width');
        formGrid.appendChild(tagGroup);

        // 4. 变量注册 (全宽)
        const counterGroup = document.createElement('div');
        counterGroup.className = 'counter-registration-group';

        const counterList = document.createElement('div');
        counterList.className = 'counter-registration-list';
        const counterTargetOptions = [
            { value: 'Card', label: '卡牌' },
            { value: 'Player', label: '玩家' }
        ];
        const playerScopeOptions = [
            { value: 'Self', label: '自己' },
            { value: 'Opponent', label: '对手' },
            { value: 'Both', label: '双方' }
        ];

        const updateCounters = (next) => {
            const normalized = this.normalizeCounterRegistrations(next);
            this.updateAbility({ counterRegistrations: normalized });
            renderCounters(normalized);
        };

        const renderCounters = (items) => {
            counterList.innerHTML = '';
            const list = Array.isArray(items) ? items : [];

            const headerRow = document.createElement('div');
            headerRow.className = 'counter-registration-row counter-registration-row--header';
            headerRow.innerHTML = '<span>变量名</span><span>目标</span><span>玩家范围</span><span>初始值</span><span>操作</span>';
            counterList.appendChild(headerRow);

            if (list.length === 0) {
                const empty = document.createElement('div');
                empty.className = 'counter-registration-empty';
                empty.textContent = '暂无注册变量';
                counterList.appendChild(empty);
                return;
            }

            list.forEach((entry, index) => {
                const row = document.createElement('div');
                row.className = 'counter-registration-row';

                const nameInput = this.createTextInput(entry?.name || '', (value) => {
                    const next = list.map((item, idx) => {
                        if (idx !== index) {
                            return item;
                        }

                        const previousName = String(item?.name ?? '').trim();
                        const previousDisplayName = String(item?.displayName ?? '').trim();
                        const normalizedName = String(value ?? '').trim();
                        const shouldFollowName = previousDisplayName.length === 0 || previousDisplayName === previousName;

                        return {
                            ...item,
                            name: normalizedName,
                            displayName: shouldFollowName ? normalizedName : previousDisplayName
                        };
                    });
                    updateCounters(next);
                }, { placeholder: '变量名称' });
                nameInput.classList.add('counter-registration-input');

                const targetTypeSelect = this.createDropdown(
                    counterTargetOptions,
                    entry?.targetType || 'Card',
                    (value) => {
                        const nextTargetType = value === 'Player' ? 'Player' : 'Card';
                        const next = list.map((item, idx) => idx === index
                            ? { ...item, targetType: nextTargetType, playerScope: item?.playerScope || 'Self' }
                            : item);
                        updateCounters(next);
                    },
                    ''
                );
                targetTypeSelect.classList.add('counter-registration-input');

                const playerScopeSelect = this.createDropdown(
                    playerScopeOptions,
                    entry?.playerScope || 'Self',
                    (value) => {
                        const next = list.map((item, idx) => idx === index
                            ? { ...item, playerScope: value || 'Self' }
                            : item);
                        updateCounters(next);
                    },
                    ''
                );
                playerScopeSelect.classList.add('counter-registration-input');
                const isPlayerTarget = (entry?.targetType || 'Card') === 'Player';
                playerScopeSelect.disabled = !isPlayerTarget;
                if (!isPlayerTarget) {
                    playerScopeSelect.classList.add('counter-registration-input--disabled');
                }

                const valueInput = this.createNumberInput(entry?.initialValue ?? 0, (value) => {
                    const normalized = Number.isFinite(value) ? Math.trunc(value) : 0;
                    const next = list.map((item, idx) => idx === index ? { ...item, initialValue: normalized } : item);
                    updateCounters(next);
                }, { step: 1, placeholder: '0' });
                valueInput.classList.add('counter-registration-input');

                const removeBtn = this.createButton('移除', () => {
                    const next = list.filter((_, idx) => idx !== index);
                    updateCounters(next);
                }, 'danger');
                removeBtn.classList.add('btn-small');

                row.appendChild(nameInput);
                row.appendChild(targetTypeSelect);
                row.appendChild(playerScopeSelect);
                row.appendChild(valueInput);
                row.appendChild(removeBtn);
                counterList.appendChild(row);
            });
        };

        const addButton = this.createButton('添加变量', () => {
            const next = Array.isArray(this.ability.counterRegistrations) ? [...this.ability.counterRegistrations] : [];
            next.push({ name: '', displayName: '', initialValue: 0, targetType: 'Card', playerScope: 'Self' });
            updateCounters(next);
        }, 'secondary');
        addButton.classList.add('btn-small');

        const counterActions = document.createElement('div');
        counterActions.className = 'counter-registration-actions';
        counterActions.appendChild(addButton);

        counterGroup.appendChild(counterList);
        counterGroup.appendChild(counterActions);
        renderCounters(this.ability.counterRegistrations || []);

        const counterForm = this.createFormGroup('变量注册', counterGroup, '单区块配置，保存时自动拆分为卡牌/玩家变量注册。');
        counterForm.classList.add('full-width');
        formGrid.appendChild(counterForm);

        // 5. 能力描述 (全宽)
        const descriptionInput = this.createTextInput(
            this.ability.description,
            (value) => this.updateAbility({ description: value }),
            { multiline: true, placeholder: '请输入能力描述...' }
        );
        descriptionInput.classList.add('form-textarea');
        const descriptionGroup = this.createFormGroup('能力描述', descriptionInput, '留空表示无描述');
        descriptionGroup.classList.add('full-width');
        formGrid.appendChild(descriptionGroup);

        wrapper.appendChild(formGrid);
        container.appendChild(wrapper);
    }

    /**
     * 创建切换开关 (Toggle Switch)
     */
    createToggleSwitch(checked, onChange, labelText) {
        const wrapper = document.createElement('label');
        wrapper.className = 'toggle-switch-wrapper';
        
        const input = document.createElement('input');
        input.type = 'checkbox';
        input.checked = checked;
        input.addEventListener('change', (e) => onChange(e.target.checked));
        
        const slider = document.createElement('span');
        slider.className = 'toggle-slider';
        
        const label = document.createElement('span');
        label.className = 'toggle-label';
        label.textContent = labelText;
        
        wrapper.appendChild(input);
        wrapper.appendChild(slider);
        wrapper.appendChild(label);
        
        return wrapper;
    }

    // ... (保留原有的辅助方法: updateAbility, loadAbilityTagOptions, buildFinalConfig, cleanup) ...
    
    updateAbility(updates) {
        this.ability = { ...this.ability, ...updates };
    }

    resolveEditorMode() {
        const explicit = this.options?.mode || this.options?.editorMode;
        if (explicit) {
            return String(explicit).toLowerCase();
        }
        const abilityType = String(this.ability?.type || '').toLowerCase();
        if (abilityType === 'continuous') {
            return 'aura';
        }
        const executionKind = String(this.ability?.executionKind || '').toLowerCase();
        if (executionKind === 'aura') {
            return 'aura';
        }
        const hasAuras = Array.isArray(this.ability?.auras) && this.ability.auras.length > 0;
        const hasBuffs = Array.isArray(this.ability?.auraConfig?.buffConfigs) && this.ability.auraConfig.buffConfigs.length > 0;
        if (hasAuras || hasBuffs) {
            return 'aura';
        }
        return 'effect';
    }

    isAuraMode() {
        return this.editorMode === 'aura';
    }

    configureStepsForMode() {
        if (!this.isAuraMode()) {
            this.steps = [
                { id: 'basic', label: '基础信息', description: '定义名称与类型' },
                { id: 'trigger', label: '触发时机', description: '何时发动能力' },
                { id: 'targets', label: '目标选择', description: '选择卡牌目标' },
                { id: 'conditions', label: '发动条件', description: '额外的限制' },
                { id: 'effects', label: '效果配置', description: '具体产生的影响' }
            ];
            return;
        }

        this.steps = [
            { id: 'basic', label: '基础信息', description: '定义名称与类型' },
            { id: 'trigger', label: '刷新事件', description: '何时刷新光环状态' },
            { id: 'targets', label: '生效目标', description: '光环作用对象' },
            { id: 'conditions', label: '生效条件', description: '满足条件才生效' },
            { id: 'effects', label: '状态配置', description: '配置光环状态效果' }
        ];
    }

    getTriggerEditorOptions() {
        if (!this.isAuraMode()) return {};
        return {
            title: '刷新事件配置',
            intro: '选择会触发光环状态刷新的事件。',
            eventLabel: '刷新事件',
            eventHelp: '选择触发光环刷新的游戏事件（可多选）',
            hideTiming: true,
            hideRelationType: false,
            hideCountLimit: true
        };
    }

    getTargetEditorOptions() {
        if (!this.isAuraMode()) return {};
        return {
            title: '生效目标配置',
            intro: '设置光环会影响哪些卡牌实例。',
            hideValidation: true,
            hidePlayerChoice: true
        };
    }

    getConditionEditorOptions() {
        if (!this.isAuraMode()) return {};
        return {
            title: '生效条件配置',
            intro: '仅当满足条件时，光环状态才会生效。',
            hideHelp: false
        };
    }

    getStatusEditorOptions() {
        if (!this.isAuraMode()) return {};
        return {
            title: '状态配置',
            intro: '配置光环产生的状态（属性/关键词/能力）'
        };
    }

    normalizeAuraConfig(ability) {
        const rawAuras = ability.auras || ability.Auras || [];
        const rawAura = Array.isArray(rawAuras) && rawAuras.length > 0 ? rawAuras[0] : null;
        const rawBuffConfigs = rawAura?.buffConfigs ?? rawAura?.BuffConfigs ?? null;
        const rawTemplate = rawAura?.buffTemplate ?? rawAura?.BuffTemplate ?? null;
        const buffConfigs = Array.isArray(rawBuffConfigs) && rawBuffConfigs.length > 0
            ? rawBuffConfigs
            : this.convertBuffTemplateToConfigs(rawTemplate);

        return {
            name: rawAura?.name ?? rawAura?.Name ?? '',
            isEnabled: rawAura?.isEnabled ?? rawAura?.IsEnabled ?? true,
            buffConfigs: buffConfigs || []
        };
    }

    convertBuffTemplateToConfigs(template) {
        if (!template || typeof template !== 'object') {
            return [];
        }
        return [
            {
                key: '',
                durationType: template.durationType ?? template.DurationType ?? 'Permanent',
                durationParameter: template.durationParameter ?? template.DurationParameter ?? 0,
                canStack: template.canStack ?? template.CanStack ?? false,
                statModifiers: template.statModifiers ?? template.StatModifiers ?? [],
                keywordModifiers: template.keywordModifiers ?? template.KeywordModifiers ?? [],
                grantedAbilities: template.grantedAbilities ?? template.GrantedAbilities ?? []
            }
        ];
    }

    async loadAbilityTagOptions() {
        // ... (保持原有逻辑)
        try {
            if (this._abilityTagOptionsCache) {
                return this._abilityTagOptionsCache;
            }
            const { raw } = await ensureEnumLocalization('AbilityKeyword');
            const data = raw || {};
            if (Object.keys(data).length === 0) {
                throw new Error('Empty enum localization');
            }
            const options = Object.entries(data).map(([value, label]) => ({ value, label }));
            options.sort((a, b) => a.label.localeCompare(b.label, 'zh-CN'));
            this._abilityTagOptionsCache = options;
            return options;
        } catch (error) {
            console.error('AbilityEditor: Failed to load tags, using defaults.', error);
            return [
                { value: 'Fanfare', label: '入场曲' },
                { value: 'Lastword', label: '谢幕曲' },
                { value: 'AttackEffect', label: '攻击时' },
                { value: 'FightEffect', label: '交战时' },
                { value: 'EvolveEffect', label: '进化时' },
                { value: 'Combo', label: '连击' },
                { value: 'Rally', label: '协作' },
                { value: 'SpellBoost', label: '魔力增幅' },
                { value: 'Awake', label: '觉醒' },
                { value: 'Necromance', label: '死灵术' },
                { value: 'Venge', label: '复仇' },
                { value: 'Reson', label: '共鸣' },
                { value: 'Eager', label: '渴望' },
                { value: 'EarthRitual', label: '土之秘术' },
                { value: 'Enhance', label: '爆能强化' },
                { value: 'Bury', label: '葬送' },
                { value: 'Reanimate', label: '亡者召还' },
                { value: 'Accelerate', label: '激奏' },
                { value: 'Crystalize', label: '结晶' },
                { value: 'Travel', label: '漫游' },
                { value: 'Intromezzo', label: '间奏' },
                { value: 'Wrath', label: '狂乱' },
                { value: 'Choose', label: '抉择' },
                { value: 'Act', label: '启动' },
                { value: 'Sacrifice', label: '血祭' }
            ];
        }
    }

    stripUiFields(value) {
        if (Array.isArray(value)) {
            return value
                .map((item) => this.stripUiFields(item))
                .filter((item) => item !== undefined);
        }
        if (!value || typeof value !== 'object') {
            return value;
        }

        const cleaned = {};
        for (const [key, val] of Object.entries(value)) {
            if (key.startsWith('_')) continue;
            const next = this.stripUiFields(val);
            if (next !== undefined) {
                cleaned[key] = next;
            }
        }
        return cleaned;
    }

    buildFinalConfig() {
        // 处理默认值
        const finalName = (this.ability.name && this.ability.name.trim() !== '')
            ? this.ability.name
            : this.getDefaultAbilityName();
        const normalizedCounters = this.normalizeCounterRegistrations(this.ability.counterRegistrations);
        const cardCounterRegistrations = normalizedCounters
            .filter(entry => entry.targetType !== 'Player')
            .map(({ targetType, playerScope, ...rest }) => ({ ...rest }));
        const playerCounterRegistrations = normalizedCounters
            .filter(entry => entry.targetType === 'Player')
            .map(({ targetType, ...rest }) => ({ ...rest, playerScope: rest.playerScope || 'Self' }));
            
        const { cardName, abilitySequence, ...baseAbility } = this.ability;
        const finalAbility = {
            ...baseAbility,
            name: finalName,
            priority: this.ability.priority || 0,
            description: this.ability.description || '',
            counterRegistrations: cardCounterRegistrations,
            playerCounterRegistrations
        };
        const cleanedAbility = this.stripUiFields(finalAbility);
        const finalizedEffects = this.effectEditor?.buildEffectsForSave
            ? this.effectEditor.buildEffectsForSave(cleanedAbility.effects, this.metadata)
            : cleanedAbility.effects;
        const isAuraMode = this.isAuraMode();
        const auraConfig = this.ability?.auraConfig || {};
        const finalizedBuffConfigs = this.statusEditor?.buildBuffConfigsForSave
            ? this.statusEditor.buildBuffConfigsForSave(auraConfig.buffConfigs || [])
            : (auraConfig.buffConfigs || []);
        const auraPayload = {
            name: auraConfig.name || cleanedAbility.name || '',
            isEnabled: auraConfig.isEnabled !== false,
            buffConfigs: finalizedBuffConfigs
        };

        const nextType = isAuraMode ? 'Continuous' : (cleanedAbility.type || 'Triggered');
        delete cleanedAbility.executionKind;
        cleanedAbility.type = nextType;
        return {
            ...cleanedAbility,
            jsonConfig: JSON.stringify({
                name: cleanedAbility.name,
                description: cleanedAbility.description,
                abilityKeywords: cleanedAbility.abilityKeywords,
                type: nextType,
                trigger: cleanedAbility.trigger,
                conditions: cleanedAbility.conditions,
                targets: cleanedAbility.targets,
                effects: isAuraMode ? [] : finalizedEffects,
                auras: isAuraMode ? [auraPayload] : (cleanedAbility.auras || []),
                counterRegistrations: cleanedAbility.counterRegistrations,
                playerCounterRegistrations: cleanedAbility.playerCounterRegistrations,
                isActive: cleanedAbility.isActive,
                version: "1.0"
            })
        };
    }

    cleanup() {
        this.blockingLoader?.cleanup();
        if (this.conditionEditor) this.conditionEditor.cleanup();
        // 其他清理...
    }

    async flushTargetEditorsWithLoader() {
        if (!this.targetEditor?.flushDslEditors) {
            return;
        }

        const shouldShowLoader = typeof this.targetEditor.hasPendingDslRenders === 'function'
            ? this.targetEditor.hasPendingDslRenders()
            : true;

        if (shouldShowLoader) {
            await this.runWithBlockingLoader("正在同步目标DSL...", async () => {
                await this.targetEditor.flushDslEditors();
            });
            return;
        }

        await this.targetEditor.flushDslEditors();
    }

    async runWithBlockingLoader(message, task) {
        const root = this.container?.querySelector('.ability-editor.wizard-mode');
        if (!root) {
            return task();
        }

        const minVisibleMs = 300;
        const startTime = Date.now();
        this.blockingLoader = this.blockingLoader || getBlockingLoader(root);
        this.blockingLoader?.show(message);

        try {
            return await task();
        } finally {
            const elapsed = Date.now() - startTime;
            const delayMs = elapsed + minVisibleMs;
            await new Promise(resolve => setTimeout(resolve, delayMs));
            this.blockingLoader?.hide();
        }
    }
}
