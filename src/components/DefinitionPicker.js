import { getBlockingLoader } from './blockingLoader.js';

function renderDefinitionCardSummary(entry, tags) {
    const cost = Number.isFinite(entry.cost) ? entry.cost : 0;
    return `
                <div class="ability-copy-card-main">
                    <span class="ability-copy-card-name tech-text-bold">${entry.name}</span>
                    <span class="ability-copy-card-id tech-text-dim">#${entry.id}</span>
                </div>
                <div class="ability-copy-card-meta">
                    <span class="tech-badge">COST ${cost}</span>
                    ${entry.cardType ? `<span class="tech-tag">${entry.cardType}</span>` : ''}
                    ${entry.cardClass ? `<span class="tech-tag">${entry.cardClass}</span>` : ''}
                    ${tags.map(tag => `<span class="tech-tag">${tag}</span>`).join('')}
                </div>
            `;
}

// 选择弹窗只拥有交互状态，复用调用方的目录，不承担效果或授予配置。
export class DefinitionPicker {
    #catalog;
    // 同类替换按取消结束旧选择；收尾只释放自己的弹窗。
    #activeAbilitySelection = null;
    #activeCardSelection = null;

    constructor(catalog) {
        this.#catalog = catalog;
    }

    async openAbilities(selectedIds = [], options = {}) {
        this.#activeAbilitySelection?.cancel();

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

            const modalLoader = getBlockingLoader(modal);

            const finish = (value) => {
                resolve(value);
                modalLoader?.hide();
                overlay.remove();
                if (this.#activeAbilitySelection?.overlay === overlay) {
                    this.#activeAbilitySelection = null;
                }
            };

            const close = () => finish([]);
            this.#activeAbilitySelection = { overlay, cancel: close };
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
                    const abilities = abilitiesByCard.get(entry.id);
                    return sum + (Array.isArray(abilities) ? abilities.length : 0);
                }, 0);

                listEl.innerHTML = pageEntries.map(entry => {
                    const abilities = abilitiesByCard.get(entry.id) || [];
                    const isExpanded = expandedCards.has(entry.id);
                    const abilityCount = abilities.length;
                    const costLabel = Number.isFinite(entry.cost) ? entry.cost : 0;
                    const bodyHtml = isExpanded
                        ? (abilityCount === 0
                            ? `<div class="abilities-placeholder"><p>暂无可用能力</p></div>`
                            : `<div class="ability-copy-abilities">${buildAbilityHtml(abilities)}</div>`)
                        : '';

                    return `
                            <div class="ability-copy-card${isExpanded ? ' expanded' : ''}" data-card-id="${entry.id}">
                                <button type="button" class="ability-copy-card-header" aria-expanded="${isExpanded}">
                                    ${renderDefinitionCardSummary(entry, [`ABILITIES ${abilityCount}`])}
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
                    filteredEntries = cardEntries.filter(entry => entry.searchText.includes(term));
                }
                currentPage = 1;
                renderList();
            };

            const init = async () => {
                modalLoader?.show('正在加载能力库...');
                try {
                    const abilities = await this.#catalog.loadAbilities();
                    const cards = Array.isArray(this.#catalog.getCards()) ? this.#catalog.getCards() : [];

                    abilitiesByCard.clear();
                    abilities.forEach(ability => {
                        const cardId = Number(ability.cardId);
                        if (!Number.isFinite(cardId) || cardId <= 0) return;
                        const list = abilitiesByCard.get(cardId) || [];
                        list.push(ability);
                        abilitiesByCard.set(cardId, list);
                    });

                    cardEntries = cards
                        .filter(card => abilitiesByCard.has(card.id))
                        .map(card => ({
                            ...card,
                            searchText: [card.name, card.id, card.cost, card.cardType, card.cardClass,
                                card.rarity, abilitiesByCard.get(card.id).map(ability => ability.name).join(' ')]
                                .join(' ').toLowerCase()
                        }))
                        .sort((a, b) => String(a.name).localeCompare(String(b.name), undefined, { numeric: true, sensitivity: 'base' }));

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

    async openCards(selectedIds = [], options = {}) {
        this.#activeCardSelection?.cancel();

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

            const modalLoader = getBlockingLoader(modal);

            const finish = (value) => {
                resolve(value);
                modalLoader?.hide();
                overlay.remove();
                if (this.#activeCardSelection?.overlay === overlay) {
                    this.#activeCardSelection = null;
                }
            };

            const close = () => finish([]);
            this.#activeCardSelection = { overlay, cancel: close };
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
                    return `
                            <div class="ability-copy-card${isSelected ? ' selected' : ''}" data-card-id="${entry.id}">
                                <button type="button" class="ability-copy-card-header" aria-pressed="${isSelected}">
                                    ${renderDefinitionCardSummary(entry, entry.rarity ? [entry.rarity] : [])}
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
                    const cards = await this.#catalog.loadCards();
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

}
