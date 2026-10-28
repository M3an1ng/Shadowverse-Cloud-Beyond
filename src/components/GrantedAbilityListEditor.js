import { copyGrantedAbilityConfig, createGrantedAbilityEditorInput, extractGrantedAbilityEditorConfig, normalizeInheritedTriggerFields, mergeInheritedTriggerFields, isDecisionAbilityEffect } from './grantedAbilityConfig.js';
import { unwrapStaticValueConfig } from './staticValueConfig.js';

// 列表只拥有授予能力的交互；父触发上下文按使用时读取，不保存第二份选项。
export class GrantedAbilityListEditor {
    #readOptions;

    constructor(definitionPicker, readOptions) {
        this.definitionPicker = definitionPicker;
        this.#readOptions = readOptions;
        this.activeGrantAbilityEditorModal = null;
    }

    get #options() {
        return this.#readOptions();
    }

    render(container, paramValue, updateParam, context = {}) {
        const normalized = this.normalizeAbilityListValue(paramValue);
        const state = { items: normalized.items };
        const effectTypeName = context?.effectTypeName || '';
        const isDecisionAbility = isDecisionAbilityEffect(effectTypeName);
        const getDecisionEditorOptions = (ability) => {
            if (!isDecisionAbility) {
                return null;
            }
            return this.buildDecisionAbilityEditorOptions(ability);
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
            const mode = await this.openGrantAbilityTypeModal();
            if (!mode) return;
            const editorOptions = {
                ...(getDecisionEditorOptions(null) || {}),
                mode
            };
            const created = await this.openGrantAbilityEditorModal(null, editorOptions);
            if (!created) return;
            syncValue([...state.items, created]);
        };

        addBtn.addEventListener('click', () => {
            void handleAdd();
        });

        copyBtn.addEventListener('click', async () => {
            try {
                const copied = await this.openAbilityConfigPickerModal();
                if (!copied) return;
                syncValue([...state.items, copied]);
            } catch (error) {
                console.error('EffectConfigEditor: failed to copy ability', error);
                alert(`复制能力失败: ${error.message}`);
            }
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
                summary.textContent = this.formatAbilityTriggerName(
                    ability,
                    isDecisionAbility ? this.#options?.trigger : null
                );

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
        const unwrapped = unwrapStaticValueConfig(raw);
        let value = unwrapped.ok ? unwrapped.value : raw;
        let didNormalize = unwrapped.ok;
        if (value == null) return { items: [], didNormalize };
        if (typeof value === 'string') {
            value = JSON.parse(value);
            didNormalize = true;
        }
        if (!Array.isArray(value)) {
            throw new TypeError('能力列表必须是数组');
        }
        return { items: value.filter(Boolean), didNormalize };
    }

    async fetchAbilityDetailById(abilityId) {
        const response = await fetch(`/api/abilities/${abilityId}`);
        if (!response.ok) {
            throw new Error(`加载能力 #${abilityId} 失败 (HTTP ${response.status})`);
        }
        const detail = await response.json();
        if (!detail || typeof detail !== 'object' || Array.isArray(detail)) {
            throw new TypeError('能力详情必须是对象');
        }
        return detail;
    }

    async buildAbilityConfigFromAbilityId(abilityId) {
        const detail = await this.fetchAbilityDetailById(abilityId);
        return copyGrantedAbilityConfig(detail);
    }

    async openAbilityConfigPickerModal() {
        const selected = await this.definitionPicker.openAbilities([], {
            allowMultiple: false,
            title: '复制现有能力',
            hintText: '选择能力后确认复制',
            confirmText: '确认复制'
        });
        const abilityId = Array.isArray(selected) ? selected[0] : selected;
        if (!Number.isFinite(Number(abilityId))) return null;
        return await this.buildAbilityConfigFromAbilityId(Number(abilityId));
    }

    formatAbilityTriggerName(ability, fallbackTrigger = null) {
    const trigger = fallbackTrigger
        ? mergeInheritedTriggerFields(ability?.trigger ?? ability?.Trigger, fallbackTrigger)
        : (ability?.trigger ?? ability?.Trigger ?? null);
    const rawEvents = trigger?.eventTypes ?? trigger?.EventTypes ?? [];
    const events = Array.isArray(rawEvents)
        ? rawEvents.map(eventType => String(eventType)).filter(Boolean)
        : [];

        if (events.length === 0) {
            return '触发: 未配置';
        }
        return `触发: ${events.join(' / ')}`;
    }

    async openGrantAbilityTypeModal() {
        return await new Promise((resolve) => {
            const overlay = document.createElement('div');
            overlay.className = 'modal-overlay grant-ability-type-overlay';
            overlay.id = 'grant-ability-type-modal';

            const modal = document.createElement('div');
            modal.className = 'modal-container ability-edit-container ability-type-modal';

            const header = document.createElement('div');
            header.className = 'modal-header';
            header.innerHTML = '<h3>选择能力类型</h3>';

            const closeBtn = document.createElement('button');
            closeBtn.className = 'modal-close-btn';
            closeBtn.type = 'button';
            closeBtn.setAttribute('aria-label', '关闭');
            closeBtn.textContent = '×';
            header.appendChild(closeBtn);

            const content = document.createElement('div');
            content.className = 'modal-content';
            content.innerHTML = `
                    <div class="ability-mode-selector">
                        <div class="btn-group-row">
                            <button type="button" class="btn-option" data-mode="effect">触发式能力</button>
                            <button type="button" class="btn-option" data-mode="aura">持久式能力</button>
                        </div>
                    </div>
                `;

            modal.appendChild(header);
            modal.appendChild(content);
            overlay.appendChild(modal);
            document.body.appendChild(overlay);

            const finish = (mode) => {
                overlay.remove();
                resolve(mode);
            };

            closeBtn.addEventListener('click', () => finish(null));
            overlay.addEventListener('click', (event) => {
                if (event.target === overlay) {
                    finish(null);
                }
            });

            content.addEventListener('click', (event) => {
                const button = event.target.closest('button[data-mode]');
                if (!button) return;
                finish(button.dataset.mode || null);
            });
        });
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
                const editorInput = createGrantedAbilityEditorInput(baseAbility);

                const options = editorOptions || {};
                await abilityEditor.render(
                    content,
                    editorInput,
                    (updatedConfig) => {
                        if (updatedConfig === null) {
                            finish(null);
                            return;
                        }
                        const abilityConfig = extractGrantedAbilityEditorConfig(updatedConfig);
                        finish(abilityConfig);
                    },
                    options
                );
            };

            void renderEditor();
        });
    }

    getDecisionAbilityLockConfig(ability) {
        const parentTrigger = this.#options?.trigger;
        if (parentTrigger && typeof parentTrigger === 'object') {
            return normalizeInheritedTriggerFields(parentTrigger);
        }

        return normalizeInheritedTriggerFields(ability?.trigger ?? ability?.Trigger ?? null);
    }

    getInheritedTriggeringEventOutputs() {
        const outputs = this.#options?.triggeringEventOutputs;
        return Array.isArray(outputs) ? outputs.slice() : [];
    }

    buildDecisionAbilityEditorOptions(ability) {
        return {
            lockedTrigger: this.getDecisionAbilityLockConfig(ability),
            lockedTriggerMessage: '分支能力触发时机与有效区域已锁定，无法修改。',
            lockValidZones: true,
            triggeringEventOutputs: this.getInheritedTriggeringEventOutputs()
        };
    }

}
