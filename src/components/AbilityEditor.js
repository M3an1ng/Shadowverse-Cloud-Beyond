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
import { validateDslExpression } from '../dslValidation.js';

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
        
        // 子编辑器实例
        this.triggerEditor = new TriggerConfigEditor();
        this.conditionEditor = new ConditionConfigEditor();
        this.targetEditor = new TargetConfigEditor();
        this.effectEditor = new EffectConfigEditor();
        
        // 向导状态
        this.currentStepIndex = 0;
        this.steps = [
            { id: 'basic', label: '基础信息', description: '定义名称与类型' },
            { id: 'trigger', label: '触发时机', description: '何时发动能力' },
            { id: 'targets', label: '目标选择', description: '对谁生效' },
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
    async render(container, ability = {}, onChange = () => {}) {
        await this.initialize();
        
        this.container = container;
        this.ability = this.normalizeAbility(ability);
        this.onChange = onChange;

        this.clearContainer(container);
        
        try {
            // 创建编辑器主体结构
            this.renderEditorStructure();
            
            // 渲染当前步骤
            await this.renderCurrentStep();
            
        } catch (error) {
            console.error('AbilityEditor: Render failed:', error);
            this.showError(`渲染能力编辑器失败: ${error.message}`, container);
        }
    }

    /**
     * 标准化能力配置对象
     */
    normalizeAbility(ability) {
        return {
            id: ability.id || '',
            name: ability.name || '',
            description: ability.description || '',
            abilityTag: ability.abilityTag || '',
            abilityType: ability.abilityType || 1,
            priority: ability.priority || 0,
            isActive: ability.isActive !== false,
            trigger: ability.trigger || {
                eventTypes: [],
                relationType: '',
                timing: 'On',
                validZones: []
            },
            conditions: ability.conditions || [],
            targets: ability.targets || [],
            effects: ability.effects || []
        };
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
                    (config) => this.updateAbility({ trigger: config })
                );
                break;
            case 'targets':
                await this.targetEditor.render(
                    contentArea,
                    this.ability.targets,
                    (targets) => this.updateAbility({ targets: targets }),
                    { trigger: this.ability.trigger }
                );
                break;
            case 'conditions':
                await this.conditionEditor.render(
                    contentArea,
                    this.ability.conditions,
                    (conditions) => this.updateAbility({ conditions: conditions }),
                    { trigger: this.ability.trigger }
                );
                break;
            case 'effects':
                await this.effectEditor.render(
                    contentArea,
                    this.ability.effects,
                    (effects) => this.updateAbility({ effects: effects }),
                    { trigger: this.ability.trigger }
                );
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
        // 验证当前步骤
        const errors = this.validateCurrentStep();
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
        const validation = this.validateAbilityBeforeSave();
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
                await this.onChange(finalConfig);
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
    validateCurrentStep() {
        const currentStepId = this.steps[this.currentStepIndex].id;
        const errors = [];

        switch (currentStepId) {
            case 'basic':
                if (!this.ability.name || this.ability.name.trim() === '') {
                    errors.push('能力名称不能为空');
                }
                // 能力标签不再必填
                break;
            case 'trigger':
                errors.push(...this.triggerEditor.validateConfig());
                break;
            case 'targets':
                errors.push(...this.collectTargetDslIssues().errors);
                break;
            case 'conditions':
                errors.push(...this.conditionEditor.validateConditions());
                errors.push(...this.collectConditionDslIssues().errors);
                break;
            case 'effects':
                errors.push(...this.effectEditor.validateEffects());
                errors.push(...this.collectEffectDslIssues().errors);
                break;
        }

        return errors;
    }

    collectConditionDslIssues() {
        const issues = { errors: [], warns: [], infos: [] };
        const pushError = (message) => issues.errors.push(message);

        (this.ability.conditions || []).forEach((condition, index) => {
            if (condition?.isEnabled === false) return;
            const expr = condition?.expression || '';
            const res = validateDslExpression(this.metadata, expr, { expectedType: 'Boolean', allowEmpty: false });
            for (const issue of res.issues) {
                pushError(`条件 #${index + 1}: ${issue.message}`);
            }
        });

        return issues;
    }

    collectTargetDslIssues() {
        const issues = { errors: [], warns: [], infos: [] };
        const pushError = (message) => issues.errors.push(message);
        const pushWarn = (message) => issues.warns.push(message);

        (this.ability.targets || []).forEach((target, index) => {
            const sourceExpr = target?.query?.sourcePoolExpression || '';
            if (sourceExpr.trim()) {
                const res = validateDslExpression(this.metadata, sourceExpr, { expectedType: 'List<CardInstance>', allowEmpty: false });
                for (const issue of res.issues) {
                    pushError(`目标 #${index + 1} 起始卡池: ${issue.message}`);
                }
            } else if (target?.query) {
                pushWarn(`目标 #${index + 1}: 未配置起始卡池表达式`);
            }

            const operations = target?.query?.operations || [];
            operations.forEach((op, opIdx) => {
                if (op?.$type !== 'FilterOperation') return;
                (op.filters || []).forEach((filter, filterIdx) => {
                    const filterExpr = filter?.valueSource?.formula || '';
                    const res = validateDslExpression(this.metadata, filterExpr, { expectedType: 'Boolean', allowEmpty: false });
                    for (const issue of res.issues) {
                        pushError(`目标 #${index + 1} 筛选 #${opIdx + 1}.${filterIdx + 1}: ${issue.message}`);
                    }
                });
            });
        });

        return issues;
    }

    collectEffectDslIssues() {
        const issues = { errors: [], warns: [], infos: [] };
        const pushError = (message) => issues.errors.push(message);

        const effects = this.ability.effects || [];
        effects.forEach((effect, index) => {
            if (!effect?.type) return;

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
                const expectedType = meta?.typeName || meta?.TypeName || null;

                const res = validateDslExpression(this.metadata, formula, { expectedType, allowEmpty: false });
                for (const issue of res.issues) {
                    pushError(`效果 #${index + 1} 参数 ${paramName} 公式: ${issue.message}`);
                }
            }
        });

        return issues;
    }

    validateAbilityBeforeSave() {
        const issues = { errors: [], warns: [], infos: [] };
        const pushError = (message) => issues.errors.push({ severity: 'error', message });
        const pushWarn = (message) => issues.warns.push({ severity: 'warn', message });

        if (!this.ability.name || this.ability.name.trim() === '') {
            pushError('能力名称不能为空');
        }

        // Trigger: required
        for (const msg of this.triggerEditor.validateConfig()) {
            pushError(msg);
        }

        (this.ability.effects || []).forEach((effect, index) => {
            if (!effect?.type) {
                pushError(`效果 #${index + 1}: 必须选择效果类型`);
            }
        });

        const conditionIssues = this.collectConditionDslIssues();
        for (const err of conditionIssues.errors) pushError(err);

        const targetIssues = this.collectTargetDslIssues();
        for (const err of targetIssues.errors) pushError(err);
        for (const warn of targetIssues.warns) pushWarn(warn);

        const effectIssues = this.collectEffectDslIssues();
        for (const err of effectIssues.errors) pushError(err);

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
            <p class="step-intro">首先，让我们定义这个能力的基本属性。这些信息将帮助玩家在游戏中识别和理解这个能力。</p>
        `;
        wrapper.appendChild(header);

        // 表单区域
        const formGrid = document.createElement('div');
        formGrid.className = 'form-grid-layout';

        // 1. 能力名称 (全宽)
        const nameInput = this.createTextInput(
            this.ability.name,
            (value) => this.updateAbility({ name: value }),
            { placeholder: '例如：火球术、神圣治愈...' }
        );
        nameInput.className = 'form-input large-input';
        const nameLabel = document.createElement('span');
        nameLabel.innerHTML = '能力名称 <span class="required-mark">*</span>';
        const nameGroup = this.createFormGroup(nameLabel, nameInput, '给能力起一个响亮的名字');
        nameGroup.className += ' full-width';
        formGrid.appendChild(nameGroup);

        // 2. 能力标签 (半宽)
        const abilityTagOptions = await this.loadAbilityTagOptions();
        // 添加一个空选项作为默认值（如果允许不选）
        if (!abilityTagOptions.some(opt => opt.value === '')) {
            abilityTagOptions.unshift({ value: '', label: '无 / 默认' });
        }
        
        const tagDropdown = this.createDropdown(
            abilityTagOptions,
            this.ability.abilityTag,
            (value) => this.updateAbility({ abilityTag: value }),
            '选择分类...'
        );
        tagDropdown.className = 'form-select';
        const tagGroup = this.createFormGroup('能力类型', tagDropdown, '决定能力的触发机制（可选）');
        formGrid.appendChild(tagGroup);

        // 3. 优先级 (半宽)
        const priorityInput = this.createNumberInput(
            this.ability.priority,
            (value) => this.updateAbility({ priority: value }),
            { min: 0, max: 100, placeholder: '0' }
        );
        priorityInput.className = 'form-input';
        const priorityGroup = this.createFormGroup('优先级', priorityInput, '数值越高越先结算');
        formGrid.appendChild(priorityGroup);

        // 4. 描述 (全宽)
        const descInput = this.createTextInput(
            this.ability.description,
            (value) => this.updateAbility({ description: value }),
            { placeholder: '详细描述能力的效果...', multiline: true }
        );
        descInput.className = 'form-textarea';
        const descGroup = this.createFormGroup('能力描述', descInput, '这段文字将显示在卡牌详情中');
        descGroup.className += ' full-width';
        formGrid.appendChild(descGroup);

        // 5. 激活状态 (开关)
        const activeSwitch = this.createToggleSwitch(
            this.ability.isActive,
            (checked) => this.updateAbility({ isActive: checked }),
            '立即启用此能力'
        );
        const activeGroup = this.createFormGroup('状态', activeSwitch);
        activeGroup.className += ' full-width';
        formGrid.appendChild(activeGroup);

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

    async loadAbilityTagOptions() {
        // ... (保持原有逻辑)
        try {
            if (this._abilityTagOptionsCache) {
                return this._abilityTagOptionsCache;
            }
            const res = await fetch('/api/localization/enums/AbilityTag');
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
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
                { value: 'Choose', label: '抉择' }
            ];
        }
    }

    buildFinalConfig() {
        return {
            ...this.ability,
            jsonConfig: JSON.stringify({
                name: this.ability.name,
                description: this.ability.description,
                abilityTag: this.ability.abilityTag,
                type: "Triggered",
                trigger: this.ability.trigger,
                conditions: this.ability.conditions,
                targets: this.ability.targets,
                effects: this.ability.effects,
                isActive: this.ability.isActive,
                version: "1.0"
            })
        };
    }

    cleanup() {
        if (this.conditionEditor) this.conditionEditor.cleanup();
        // 其他清理...
    }
}
