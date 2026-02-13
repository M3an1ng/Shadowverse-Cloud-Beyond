import { renderDescriptionHtml } from '../descriptionParser.js';
import { hydrateDescriptionReferences } from '../previewDescriptionReferences.js';
import { ensureEnumLocalizations, getCachedEnumLabel } from '../enumLocalizationCache.js';
import { showPreviewDescriptionTooltip } from '../previewDescriptionTooltip.js';

const MODAL_ID = 'card-detail-modal';
const IMAGE_MODAL_ID = 'card-detail-image-modal';

const Icons = {
    atk: '⚔',
    def: '♥',
    vial: `<svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><path d="M12 2c1.1 0 2 .9 2 2v6.2c2.23 1.61 3.4 4.35 2.92 7.16-.4 2.34-2.26 4.2-4.6 4.6-3.83.65-7.32-2.3-7.32-6.16 0-2.61 1.44-4.87 3.6-6V4c0-1.1.9-2 2-2zm0 2v5h-1V4h1zm0 7c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z"/></svg>`,
    rupee: `<svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><circle cx="12" cy="12" r="10"></circle></svg>`,
    card: `<svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><path d="M20 4H4c-1.11 0-2 .89-2 2v12c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4V6h16v12z"/></svg>`
};

function normalizeEnumName(value) {
    if (typeof value !== 'string') return value ?? '';
    const trimmed = value.trim();
    if (!trimmed) return '';
    if (trimmed.includes('_')) {
        return trimmed
            .split('_')
            .map(part => part ? part[0].toUpperCase() + part.slice(1) : part)
            .join('_');
    }
    return trimmed[0].toUpperCase() + trimmed.slice(1);
}

function normalizeEnumValue(value) {
    if (value === null || value === undefined) return '';
    return String(value).trim().toLowerCase();
}

function getCardClassLabel(value) {
    const normalized = normalizeEnumValue(value);
    if (!normalized) return '-';
    const fallback = normalizeEnumName(String(value).trim());
    return getCachedEnumLabel('CardClass', value, fallback || '-');
}

let detailEnumLocalizationPromise = null;
function ensureDetailEnumLocalization(overlay) {
    if (!detailEnumLocalizationPromise) {
        detailEnumLocalizationPromise = ensureEnumLocalizations(['CardClass'])
            .catch(error => {
                console.warn('CardDetailMockup: Failed to load card enum labels.', error);
            });
    }

    detailEnumLocalizationPromise?.then(() => {
        if (!overlay?.isConnected) return;
        const classEl = overlay.querySelector('[data-card-class]');
        const rawClass = classEl?.dataset?.cardClass;
        if (!classEl || !rawClass) return;
        classEl.textContent = getCachedEnumLabel('CardClass', rawClass, classEl.textContent);
    });
}

function resolveRarityClass(card) {
    const rawRarity = resolveField(card, ['rarity', 'Rarity'], '');
    const normalized = normalizeEnumName(rawRarity) || '';
    const lower = String(normalized).toLowerCase();
    if (lower === 'legendary') return 'legendary';
    if (lower === 'legend') return 'legend';
    if (lower === 'gold' || lower === 'silver' || lower === 'bronze') return lower;
    return lower || 'common';
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function resolveField(card, keys, fallback = '') {
    for (const key of keys) {
        const value = card?.[key];
        if (value !== undefined && value !== null && value !== '') {
            return value;
        }
    }
    return fallback;
}

function resolveNumber(card, keys, fallback = 0) {
    const raw = resolveField(card, keys, fallback);
    const value = Number(raw);
    return Number.isFinite(value) ? value : fallback;
}

function resolveArtUrl(path) {
    if (!path) return '';
    return encodeURI(path).replace(/'/g, '%27');
}

function buildAbilityHtml(description) {
    const contentHtml = description
        ? renderDescriptionHtml(description)
        : '<span class="text-dim"><暂无描述></span>';
    return `<div class="ability-text">${contentHtml}</div>`;
}

function removeExistingModal(id) {
    const existing = document.getElementById(id);
    if (existing) {
        existing.remove();
    }
}

export function showCardDetailImageModal(detailUrl) {
    if (!detailUrl) return;
    removeExistingModal(IMAGE_MODAL_ID);

    const modal = document.createElement('div');
    modal.id = IMAGE_MODAL_ID;
    modal.className = 'card-detail-image-modal';
    modal.innerHTML = `
        <div class="card-detail-image-content">
            <img class="card-detail-image" src="${detailUrl}" alt="Card detail art">
        </div>
    `;
    const closeModal = () => {
        modal.remove();
        document.removeEventListener('keydown', onKeydown);
    };
    const onKeydown = (event) => {
        if (event.key === 'Escape') {
            closeModal();
        }
    };
    modal.addEventListener('click', (event) => {
        if (event.target === modal) {
            closeModal();
        }
    });
    document.addEventListener('keydown', onKeydown);

    document.body.appendChild(modal);
}

export function showCardDetailModal(card, options = {}) {
    if (!card) return;
    removeExistingModal(MODAL_ID);

    const name = resolveField(card, ['name', 'Name'], 'UNTITLED');
    const currentCardId = resolveField(card, ['id', 'Id', 'CardId'], null);
    const rawClass = resolveField(card, ['class', 'Class', 'cardClass', 'CardClass'], '-');
    const cardClass = getCardClassLabel(rawClass);
    const rawType = resolveField(card, ['type', 'Type', 'cardType', 'CardType'], '');
    const isFollower = normalizeEnumValue(rawType) === 'follower';
    const rawTribe = card?.tribe ?? card?.Tribe ?? card?.cardTribe ?? card?.CardTribe ?? [];
    const tribeList = Array.isArray(rawTribe) ? rawTribe : [rawTribe];
    const tribeText = tribeList
        .map(item => (item ?? '').toString().trim())
        .filter(Boolean)
        .join('·');
    const tribeDisplay = tribeText || '-';
    const costValue = resolveNumber(card, ['currentCost', 'CurrentCost', 'cost', 'Cost'], 0);
    const attackValue = resolveNumber(card, ['currentAttack', 'CurrentAttack', 'attack', 'Attack'], 0);
    const healthValue = resolveNumber(card, ['currentHealth', 'CurrentHealth', 'health', 'Health'], 0);
    const rarityClass = resolveRarityClass(card);
    const descriptionFallback = Array.isArray(card?.effectDescriptions)
        ? card.effectDescriptions.filter(Boolean).join('\n')
        : '';
    const description = resolveField(card, ['description', 'Description'], descriptionFallback);
    const normalizedPreviewUrl = options.previewUrl ? String(options.previewUrl).replace(/'/g, '%27') : '';
    const normalizedDetailUrl = options.detailUrl ? String(options.detailUrl).replace(/'/g, '%27') : '';
    const resolvedPreviewUrl = normalizedPreviewUrl || resolveArtUrl(resolveField(card, ['artPreview', 'ArtPreview'], ''));
    const resolvedDetailUrl = normalizedDetailUrl || resolveArtUrl(resolveField(card, ['artDetail', 'ArtDetail'], ''));
    const previewUrl = resolvedPreviewUrl || resolvedDetailUrl;
    const detailUrl = resolvedDetailUrl;
    const inspectDisabled = !detailUrl;
    const abilityHtml = buildAbilityHtml(description);
    const artHtml = previewUrl
        ? `<img class="card-preview-image" src="${previewUrl}" alt="${escapeHtml(name)}">`
        : `
            <div class="art-placeholder">
                <div class="art-text">ART</div>
            </div>
        `;
    const statIndicatorsHtml = isFollower
        ? `
            <div class="card-stat-indicator stat-atk">${attackValue}</div>
            <div class="card-stat-indicator stat-def">${healthValue}</div>
        `
        : '';
    const metaStatsHtml = isFollower
        ? `
            <div class="meta-stats">
                <div class="mini-stat attack"><span class="icon-atk">${Icons.atk}</span> ${attackValue}</div>
                <div class="mini-stat health"><span class="icon-def">${Icons.def}</span> ${healthValue}</div>
            </div>
        `
        : '';

    const overlay = document.createElement('div');
    overlay.id = MODAL_ID;
    overlay.className = 'card-detail-modal-overlay';
    overlay.innerHTML = `
        <div class="card-detail-modal" role="dialog" aria-modal="true" aria-label="Card detail">
            <div class="modal-header-bar">
                <div class="header-title">卡牌详情</div>
                <button class="close-btn" type="button" aria-label="Close">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor">
                        <line x1="18" y1="6" x2="6" y2="18"></line>
                        <line x1="6" y1="6" x2="18" y2="18"></line>
                    </svg>
                </button>
            </div>

            <div class="detail-content-frame">
                <div class="detail-content-grid">
                    <div class="visual-column">
                    <div class="card-preview-wrapper rarity-${rarityClass}">
                            <div class="card-preview-art${previewUrl ? ' has-image' : ''}">
                                ${artHtml}
                            </div>

                            <div class="card-stat-indicator stat-cost">${costValue}</div>
                            ${statIndicatorsHtml}

                            <button class="inspect-btn" type="button" ${inspectDisabled ? 'disabled' : ''} aria-label="Inspect detail art">
                                <span class="inspect-text">检视</span>
                            </button>
                        </div>
                    </div>

                    <div class="data-column">
                        <div class="info-row name-row">
                            <div class="name-box">
                                <span class="card-name">${escapeHtml(name)}</span>
                            </div>
                        </div>

                        <div class="info-row meta-row">
                            <div class="meta-box">
                                <div class="meta-text">
                                    <div>职业: <span class="meta-value" data-card-class="${escapeHtml(rawClass)}">${escapeHtml(cardClass)}</span></div>
                                    <div>种类: <span class="meta-value">${escapeHtml(tribeDisplay)}</span></div>
                                </div>
                                ${metaStatsHtml}
                            </div>
                        </div>

                        <div class="ability-container-row">
                            <div class="ability-box">
                                ${abilityHtml}
                            </div>
                            <div class="side-buttons" aria-hidden="true">
                                <button class="side-btn audio-btn" type="button" aria-label="Voice">
                                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z"/></svg>
                                </button>
                                <button class="side-btn lore-btn" type="button" aria-label="Lore">
                                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M18 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zM6 4h5v8l-2.5-1.5L6 12V4z"/></svg>
                                </button>
                            </div>
                        </div>

                        <div class="footer-stats-row" aria-hidden="true">
                            <div class="stat-bar">
                                <span class="stat-label">持有</span>
                                <span class="stat-icon normal-icon">${Icons.card}</span>
                                <span class="stat-value">0</span>
                            </div>
                            <div class="stat-bar">
                                <span class="stat-label">持有</span>
                                <span class="stat-icon animated-icon">${Icons.card}</span>
                                <span class="stat-value">0</span>
                            </div>
                        </div>

                        <div class="footer-actions-row">
                            <button class="action-btn create-animated-btn is-disabled" type="button" disabled>
                                <div class="btn-label">制作闪卡</div>
                                <div class="btn-cost">消费 <span class="cost-icon">${Icons.rupee}</span> 0</div>
                            </button>
                            <button class="action-btn liquefy-btn is-disabled" type="button" disabled>
                                <div class="btn-label">分解</div>
                                <div class="btn-cost">获得 <span class="cost-icon">${Icons.vial}</span> 0</div>
                            </button>
                            <button class="action-btn create-btn is-disabled" type="button" disabled>
                                <div class="btn-label">制作</div>
                                <div class="btn-cost">消费 <span class="cost-icon">${Icons.vial}</span> 0</div>
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;

    const closeModal = () => {
        overlay.remove();
        document.removeEventListener('keydown', onKeydown);
    };

    const onKeydown = (event) => {
        if (event.key === 'Escape') {
            closeModal();
        }
    };

    const closeBtn = overlay.querySelector('.close-btn');
    closeBtn?.addEventListener('click', closeModal);
    overlay.addEventListener('click', (event) => {
        if (event.target === overlay) {
            closeModal();
        }
    });
    overlay.addEventListener('click', (event) => {
        const tokenEl = event.target.closest('[data-preview-token]');
        if (!tokenEl) return;
        const tokenType = tokenEl.dataset.previewToken;
        const tokenValue = tokenEl.dataset.previewValue;
        if (!tokenType || !tokenValue) return;
        event.preventDefault();
        event.stopPropagation();
        const tooltipOptions = {
            cards: options.cards,
            currentCardId,
            currentCardName: name
        };
        void showPreviewDescriptionTooltip(tokenEl, tokenType, tokenValue, tooltipOptions);
    });
    document.addEventListener('keydown', onKeydown);

    const inspectBtn = overlay.querySelector('.inspect-btn');
    if (inspectBtn && !inspectDisabled) {
        inspectBtn.addEventListener('click', (event) => {
            event.stopPropagation();
            showCardDetailImageModal(detailUrl);
        });
    }

    document.body.appendChild(overlay);
    ensureDetailEnumLocalization(overlay);
    void hydrateDescriptionReferences(overlay, {
        cards: options.cards,
        fallbackPreviewUrl: previewUrl
    });
}
