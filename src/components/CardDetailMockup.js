import { renderDescriptionHtml } from '../descriptionParser.js';
import { hydrateDescriptionReferences } from '../previewDescriptionReferences.js';
import { ensureEnumLocalizations, getCachedEnumLabel } from '../enumLocalizationCache.js';
import { showPreviewDescriptionTooltip } from '../previewDescriptionTooltip.js';
import { getUserSession } from '../userSession.js';
import {
    buildActivityQuestMessage,
    extractQuestProgress,
    QUEST_PROGRESS_UPDATED_EVENT
} from '../questFeedback.js';
import cardAttackIconUrl from '../icon/utx_ic_card_attack.png';
import cardHealthIconUrl from '../icon/utx_ic_card_health.png';
import cardCostIconUrl from '../icon/utx_ic_card_cost.png';

const MODAL_ID = 'card-detail-modal';
const IMAGE_MODAL_ID = 'card-detail-image-modal';
const COMMENT_MAX_LENGTH = 500;
const COMMUNITY_TIME_ZONE = 'Etc/GMT-8';
const DETAIL_NAVIGATION_RESET = 'reset';
const DETAIL_NAVIGATION_PUSH = 'push';
const DETAIL_NAVIGATION_REPLACE = 'replace';
const COMMUNITY_TIME_FORMATTER = new Intl.DateTimeFormat('zh-CN', {
    timeZone: COMMUNITY_TIME_ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23'
});
let detailModalHistory = [];
let activeDetailModalCleanup = null;

const Icons = {
    atk: cardAttackIconUrl,
    def: cardHealthIconUrl,
    cost: cardCostIconUrl,
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

function renderStatIcon(iconUrl, className) {
    return `<img class="${className}" src="${iconUrl}" alt="" aria-hidden="true">`;
}

function removeExistingModal(id) {
    const existing = document.getElementById(id);
    if (existing) {
        existing.remove();
    }
}

function normalizeDetailNavigationMode(rawValue) {
    const normalized = String(rawValue ?? '').trim().toLowerCase();
    if (normalized === DETAIL_NAVIGATION_PUSH) return DETAIL_NAVIGATION_PUSH;
    if (normalized === DETAIL_NAVIGATION_REPLACE) return DETAIL_NAVIGATION_REPLACE;
    return DETAIL_NAVIGATION_RESET;
}

function cleanupActiveDetailModal() {
    if (typeof activeDetailModalCleanup === 'function') {
        activeDetailModalCleanup();
    }
    activeDetailModalCleanup = null;
}

function normalizeCardDefinitionId(rawValue) {
    const parsed = Number.parseInt(String(rawValue ?? '').trim(), 10);
    return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function normalizeVoteValue(rawValue) {
    const normalized = String(rawValue ?? '').trim().toLowerCase();
    if (normalized === 'up' || normalized === 'down') {
        return normalized;
    }
    return null;
}

function parseCommunityDate(rawValue) {
    if (!rawValue) return null;
    if (rawValue instanceof Date) {
        return Number.isNaN(rawValue.getTime()) ? null : rawValue;
    }

    const text = String(rawValue).trim();
    if (!text) return null;

    // If timezone information exists, rely on native parsing.
    const hasTimezone = /(?:z|[+-]\d{2}:?\d{2})$/i.test(text);
    if (hasTimezone) {
        const parsed = new Date(text);
        return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    // Backend may return UTC without timezone suffix, e.g. "2026-03-03 20:35:33.8062848".
    const normalized = text.replace(' ', 'T');
    const match = normalized.match(
        /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,9}))?)?$/
    );
    if (match) {
        const year = Number(match[1]);
        const month = Number(match[2]) - 1;
        const day = Number(match[3]);
        const hour = Number(match[4]);
        const minute = Number(match[5]);
        const second = Number(match[6] ?? '0');
        const fraction = String(match[7] ?? '').slice(0, 3).padEnd(3, '0');
        const millisecond = Number(fraction || '0');
        return new Date(Date.UTC(year, month, day, hour, minute, second, millisecond));
    }

    const fallback = new Date(normalized);
    return Number.isNaN(fallback.getTime()) ? null : fallback;
}

function formatCommunityTime(rawValue) {
    if (!rawValue) return '-';
    const parsed = parseCommunityDate(rawValue);
    if (!parsed) return '-';
    try {
        const parts = COMMUNITY_TIME_FORMATTER.formatToParts(parsed);
        const values = {};
        parts.forEach((part) => {
            if (part.type !== 'literal') {
                values[part.type] = part.value;
            }
        });
        return `${values.year}-${values.month}-${values.day} ${values.hour}:${values.minute}`;
    } catch (error) {
        const utc8Date = new Date(parsed.getTime() + (8 * 60 * 60 * 1000));
        const year = utc8Date.getUTCFullYear();
        const month = String(utc8Date.getUTCMonth() + 1).padStart(2, '0');
        const day = String(utc8Date.getUTCDate()).padStart(2, '0');
        const hour = String(utc8Date.getUTCHours()).padStart(2, '0');
        const minute = String(utc8Date.getUTCMinutes()).padStart(2, '0');
        return `${year}-${month}-${day} ${hour}:${minute}`;
    }
}

function resolveCommunityValue(source, keys, fallback = null) {
    if (!source || typeof source !== 'object') return fallback;
    for (const key of keys) {
        if (Object.prototype.hasOwnProperty.call(source, key) && source[key] !== undefined && source[key] !== null) {
            return source[key];
        }
    }
    return fallback;
}

function emitQuestProgressUpdated(userId, questProgress) {
    if (!userId || !questProgress || typeof document === 'undefined') {
        return;
    }

    document.dispatchEvent(new CustomEvent(QUEST_PROGRESS_UPDATED_EVENT, {
        detail: {
            userId: String(userId),
            questProgress
        }
    }));
}

async function fetchCardCommunity(cardId, userId) {
    const params = new URLSearchParams();
    params.set('userId', userId);
    params.set('page', '1');
    params.set('limit', '50');
    const response = await fetch(`/api/cards/${encodeURIComponent(cardId)}/community?${params.toString()}`);
    if (!response.ok) {
        const message = await response.text();
        throw new Error(message || `评论加载失败 (${response.status})`);
    }
    return response.json();
}

async function submitCardVote(cardId, userId, voteType) {
    const response = await fetch(`/api/cards/${encodeURIComponent(cardId)}/community/vote`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, voteType })
    });
    if (!response.ok) {
        const message = await response.text();
        throw new Error(message || `投票失败 (${response.status})`);
    }
    return response.json();
}

async function createCardComment(cardId, userId, content) {
    const response = await fetch(`/api/cards/${encodeURIComponent(cardId)}/community/comments`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, content })
    });
    if (!response.ok) {
        const message = await response.text();
        throw new Error(message || `评论提交失败 (${response.status})`);
    }
    return response.json();
}

async function deleteCardComment(cardId, commentId, userId) {
    const params = new URLSearchParams();
    params.set('userId', userId);
    const response = await fetch(`/api/cards/${encodeURIComponent(cardId)}/community/comments/${encodeURIComponent(commentId)}?${params.toString()}`, {
        method: 'DELETE'
    });
    if (!response.ok) {
        const message = await response.text();
        throw new Error(message || `评论删除失败 (${response.status})`);
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
    const navigationMode = normalizeDetailNavigationMode(options.navigationMode);
    const modalOptions = { ...options };
    delete modalOptions.navigationMode;
    const hadOpenModal = Boolean(document.getElementById(MODAL_ID));

    const historyEntry = { card, options: modalOptions };
    if (navigationMode === DETAIL_NAVIGATION_PUSH && detailModalHistory.length > 0) {
        detailModalHistory.push(historyEntry);
    } else if (navigationMode === DETAIL_NAVIGATION_REPLACE && detailModalHistory.length > 0) {
        detailModalHistory[detailModalHistory.length - 1] = historyEntry;
    } else {
        detailModalHistory = [historyEntry];
    }

    cleanupActiveDetailModal();
    removeExistingModal(MODAL_ID);

    const name = resolveField(card, ['name', 'Name'], 'UNTITLED');
    const canGoBack = detailModalHistory.length > 1;
    const currentCardId = resolveField(card, ['id', 'Id', 'CardId'], null);
    const cardDefinitionId = normalizeCardDefinitionId(currentCardId);
    const session = getUserSession();
    const userId = session?.mode === 'user' ? session.userId : null;
    const canUseCommunity = !!userId && !!cardDefinitionId;
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
    const costValue = resolveNumber(card, ['cost', 'Cost'], 0);
    const attackValue = resolveNumber(card, ['attack', 'Attack'], 0);
    const healthValue = resolveNumber(card, ['health', 'Health'], 0);
    const rarityClass = resolveRarityClass(card);
    const descriptionFallback = Array.isArray(card?.effectDescriptions)
        ? card.effectDescriptions.filter(Boolean).join('\n')
        : '';
    const description = resolveField(card, ['description', 'Description'], descriptionFallback);
    const normalizedPreviewUrl = modalOptions.previewUrl ? String(modalOptions.previewUrl).replace(/'/g, '%27') : '';
    const normalizedDetailUrl = modalOptions.detailUrl ? String(modalOptions.detailUrl).replace(/'/g, '%27') : '';
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
            <div class="card-stat-indicator stat-atk">
                ${renderStatIcon(Icons.atk, 'card-stat-icon')}
                <span class="card-stat-value">${attackValue}</span>
            </div>
            <div class="card-stat-indicator stat-def">
                ${renderStatIcon(Icons.def, 'card-stat-icon')}
                <span class="card-stat-value">${healthValue}</span>
            </div>
        `
        : '';
    const metaStatsHtml = isFollower
        ? `
            <div class="meta-stats">
                <div class="mini-stat attack">${renderStatIcon(Icons.atk, 'mini-stat-icon icon-atk')} ${attackValue}</div>
                <div class="mini-stat health">${renderStatIcon(Icons.def, 'mini-stat-icon icon-def')} ${healthValue}</div>
            </div>
        `
        : '';
    const communityDisabled = !canUseCommunity;
    const communityStatusText = cardDefinitionId
        ? (communityDisabled ? '登录后可评论和投票' : '')
        : '当前卡牌不支持评论';

    const overlay = document.createElement('div');
    overlay.id = MODAL_ID;
    overlay.className = `card-detail-modal-overlay${hadOpenModal ? ' is-reopen' : ''}`;
    overlay.innerHTML = `
        <div class="card-detail-modal-shell">
            <div class="card-detail-modal" role="dialog" aria-modal="true" aria-label="Card detail">
                <div class="modal-header-bar">
                    <button class="back-btn" type="button" aria-label="Back" ${canGoBack ? '' : 'disabled'}>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor">
                            <polyline points="15 18 9 12 15 6"></polyline>
                        </svg>
                    </button>
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

                                <div class="card-stat-indicator stat-cost">
                                    ${renderStatIcon(Icons.cost, 'card-stat-icon')}
                                    <span class="card-stat-value">${costValue}</span>
                                </div>
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
                                <button class="action-btn community-action-btn upvote-btn" type="button" data-community-action="up" ${communityDisabled ? 'disabled' : ''}>
                                    <div class="btn-label">赞</div>
                                    <div class="btn-cost">当前 <span class="community-action-count" data-vote-count="up">0</span></div>
                                </button>
                                <button class="action-btn community-action-btn downvote-btn" type="button" data-community-action="down" ${communityDisabled ? 'disabled' : ''}>
                                    <div class="btn-label">踩</div>
                                    <div class="btn-cost">当前 <span class="community-action-count" data-vote-count="down">0</span></div>
                                </button>
                                <button class="action-btn community-action-btn comment-toggle-btn" type="button" data-community-action="toggle-comments" ${communityDisabled ? 'disabled' : ''}>
                                    <div class="btn-label">评论</div>
                                    <div class="btn-cost">当前 <span class="community-action-count" data-comment-total>0</span></div>
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="card-community-flyout" aria-hidden="true">
                <div class="card-community-flyout-head">
                    <div class="card-community-flyout-title">评论</div>
                    <div class="card-community-status" data-community-status>${escapeHtml(communityStatusText)}</div>
                </div>
                <div class="card-community-comments" data-community-comments></div>
                <form class="card-community-form">
                    <textarea class="card-community-input" rows="2" maxlength="${COMMENT_MAX_LENGTH}" placeholder="写下你的评论..." ${communityDisabled ? 'disabled' : ''}></textarea>
                    <div class="card-community-form-footer">
                        <span class="card-community-char-counter" data-community-char-counter>0/${COMMENT_MAX_LENGTH}</span>
                        <button class="card-community-submit" type="submit" ${communityDisabled ? 'disabled' : ''}>发送</button>
                    </div>
                </form>
            </div>
            <div class="card-community-reward-layer" data-community-reward-layer aria-hidden="true"></div>
        </div>
    `;

    let cleanupFn = null;
    const closeModal = () => {
        detailModalHistory = [];
        cleanupFn?.();
        if (activeDetailModalCleanup === cleanupFn) {
            activeDetailModalCleanup = null;
        }
    };

    const onKeydown = (event) => {
        if (event.key === 'Escape') {
            closeModal();
        }
    };

    cleanupFn = () => {
        document.removeEventListener('keydown', onKeydown);
        if (overlay.isConnected) {
            overlay.remove();
        }
    };
    activeDetailModalCleanup = cleanupFn;

    const backBtn = overlay.querySelector('.back-btn');
    const closeBtn = overlay.querySelector('.close-btn');
    backBtn?.addEventListener('click', () => {
        if (detailModalHistory.length <= 1) {
            return;
        }
        detailModalHistory.pop();
        const previousEntry = detailModalHistory[detailModalHistory.length - 1];
        if (!previousEntry?.card) {
            return;
        }
        showCardDetailModal(previousEntry.card, {
            ...previousEntry.options,
            navigationMode: DETAIL_NAVIGATION_REPLACE
        });
    });
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
            openCardDetail: showCardDetailModal,
            cards: modalOptions.cards,
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

    const shellEl = overlay.querySelector('.card-detail-modal-shell');
    const sidebarEl = overlay.querySelector('.card-community-flyout');
    const rewardLayerEl = overlay.querySelector('[data-community-reward-layer]');
    const communityStatusEl = overlay.querySelector('[data-community-status]');
    const commentsContainer = overlay.querySelector('[data-community-comments]');
    const commentForm = overlay.querySelector('.card-community-form');
    const commentInput = overlay.querySelector('.card-community-input');
    const charCounter = overlay.querySelector('[data-community-char-counter]');
    const voteUpBtn = overlay.querySelector('[data-community-action="up"]');
    const voteDownBtn = overlay.querySelector('[data-community-action="down"]');
    const toggleCommentsBtn = overlay.querySelector('[data-community-action="toggle-comments"]');
    const upvoteCountEl = overlay.querySelector('[data-vote-count="up"]');
    const downvoteCountEl = overlay.querySelector('[data-vote-count="down"]');
    const commentTotalEl = overlay.querySelector('[data-comment-total]');
    const submitBtn = overlay.querySelector('.card-community-submit');
    let isCommunityBusy = false;
    let isSidebarOpen = false;

    const setSidebarOpen = (open) => {
        if (!shellEl || !sidebarEl) return;
        const nextOpen = !!open && !communityDisabled;
        isSidebarOpen = nextOpen;
        shellEl.classList.toggle('is-community-open', nextOpen);
        toggleCommentsBtn?.classList.toggle('is-active', nextOpen);
        sidebarEl.setAttribute('aria-hidden', nextOpen ? 'false' : 'true');
        if (nextOpen) {
            commentInput?.focus();
        }
    };

    const setCommunityStatus = (message, isError = false) => {
        if (!communityStatusEl) return;
        communityStatusEl.textContent = message || '';
        communityStatusEl.classList.toggle('is-error', !!isError);
    };

    const setCommunityBusy = (busy) => {
        isCommunityBusy = !!busy;
        if (canUseCommunity) {
            if (submitBtn) submitBtn.disabled = isCommunityBusy;
            if (voteUpBtn) voteUpBtn.disabled = isCommunityBusy;
            if (voteDownBtn) voteDownBtn.disabled = isCommunityBusy;
            if (commentInput) commentInput.disabled = isCommunityBusy;
        }
    };

    const updateCharCounter = () => {
        if (!commentInput || !charCounter) return;
        charCounter.textContent = `${commentInput.value.length}/${COMMENT_MAX_LENGTH}`;
    };

    const applyVoteState = (votePayload) => {
        const upvoteCount = Number(resolveCommunityValue(votePayload, ['upvoteCount', 'UpvoteCount'], 0)) || 0;
        const downvoteCount = Number(resolveCommunityValue(votePayload, ['downvoteCount', 'DownvoteCount'], 0)) || 0;
        const userVote = normalizeVoteValue(resolveCommunityValue(votePayload, ['userVote', 'UserVote'], null));
        if (upvoteCountEl) upvoteCountEl.textContent = String(upvoteCount);
        if (downvoteCountEl) downvoteCountEl.textContent = String(downvoteCount);
        voteUpBtn?.classList.toggle('is-active', userVote === 'up');
        voteDownBtn?.classList.toggle('is-active', userVote === 'down');
    };

    const renderCommentItem = (comment) => {
        const commentId = resolveCommunityValue(comment, ['id', 'Id'], '');
        const authorName = resolveCommunityValue(comment, ['username', 'Username'], '玩家');
        const avatarUrl = resolveCommunityValue(comment, ['avatarUrl', 'AvatarUrl'], '');
        const content = resolveCommunityValue(comment, ['content', 'Content'], '');
        const createdAt = resolveCommunityValue(comment, ['createdAt', 'CreatedAt'], null);
        const commentUserId = resolveCommunityValue(comment, ['userId', 'UserId'], null);
        const canDelete = Boolean(resolveCommunityValue(comment, ['canDelete', 'CanDelete'], false))
            || (!!userId && String(commentUserId || '') === String(userId));
        const avatar = avatarUrl
            ? `<img class="card-community-avatar-image" src="${escapeHtml(String(avatarUrl))}" alt="${escapeHtml(String(authorName))}">`
            : `<span class="card-community-avatar-text">${escapeHtml(String(authorName).slice(0, 1).toUpperCase() || '玩')}</span>`;
        const deleteButton = canDelete
            ? `<button class="card-community-delete" type="button" data-comment-id="${escapeHtml(String(commentId))}">删除</button>`
            : '';
        return `
            <div class="card-community-item">
                <div class="card-community-avatar">${avatar}</div>
                <div class="card-community-body">
                    <div class="card-community-meta">
                        <span class="card-community-author">${escapeHtml(String(authorName))}</span>
                        <span class="card-community-time">${escapeHtml(formatCommunityTime(createdAt))}</span>
                        ${deleteButton}
                    </div>
                    <div class="card-community-content">${escapeHtml(String(content))}</div>
                </div>
            </div>
        `;
    };

    const renderVoteActivityItem = (activity) => {
        const username = resolveCommunityValue(activity, ['username', 'Username'], '玩家');
        const rawVoteType = resolveCommunityValue(activity, ['voteType', 'VoteType'], '');
        const voteType = normalizeVoteValue(rawVoteType);
        if (!voteType) return '';
        const updatedAt = resolveCommunityValue(activity, ['updatedAt', 'UpdatedAt'], null);
        const actionText = voteType === 'up' ? '点了赞' : '点了踩';
        return `
            <div class="card-community-system-entry ${voteType === 'up' ? 'is-up' : 'is-down'}">
                <div class="card-community-system-meta">
                    <span class="card-community-system-time">${escapeHtml(formatCommunityTime(updatedAt))}</span>
                </div>
                <div class="card-community-system-text">${escapeHtml(String(username))} ${actionText}</div>
            </div>
        `;
    };

    const renderCommunityFeed = (comments, voteActivities) => {
        if (!commentsContainer) return;

        const safeComments = Array.isArray(comments) ? comments : [];
        const safeVoteActivities = Array.isArray(voteActivities) ? voteActivities : [];
        const timeline = [];

        safeComments.forEach((comment, index) => {
            const createdAt = resolveCommunityValue(comment, ['createdAt', 'CreatedAt'], null);
            const parsedTime = parseCommunityDate(createdAt);
            timeline.push({
                kind: 'comment',
                sortTime: parsedTime ? parsedTime.getTime() : 0,
                order: index,
                payload: comment
            });
        });

        safeVoteActivities.forEach((activity, index) => {
            const updatedAt = resolveCommunityValue(activity, ['updatedAt', 'UpdatedAt'], null);
            const parsedTime = parseCommunityDate(updatedAt);
            timeline.push({
                kind: 'vote',
                sortTime: parsedTime ? parsedTime.getTime() : 0,
                order: index,
                payload: activity
            });
        });

        if (timeline.length === 0) {
            commentsContainer.innerHTML = '<div class="card-community-empty">暂无评论和投票动态</div>';
            return;
        }

        timeline.sort((left, right) => {
            if (left.sortTime !== right.sortTime) {
                return right.sortTime - left.sortTime;
            }
            if (left.kind !== right.kind) {
                return left.kind === 'vote' ? 1 : -1;
            }
            return left.order - right.order;
        });

        commentsContainer.innerHTML = timeline.map((entry) => {
            if (entry.kind === 'vote') {
                return renderVoteActivityItem(entry.payload);
            }
            return renderCommentItem(entry.payload);
        }).join('');
    };

    const loadCommunity = async () => {
        if (!cardDefinitionId || !userId) {
            return false;
        }

        setCommunityBusy(true);
        if (isSidebarOpen) {
            setCommunityStatus('加载中...');
        }
        try {
            const payload = await fetchCardCommunity(cardDefinitionId, userId);
            applyVoteState(payload);
            const totalComments = Number(resolveCommunityValue(payload, ['totalComments', 'TotalComments'], 0)) || 0;
            if (commentTotalEl) {
                commentTotalEl.textContent = String(totalComments);
            }
            const comments = resolveCommunityValue(payload, ['comments', 'Comments'], []);
            const voteActivities = resolveCommunityValue(payload, ['voteActivities', 'VoteActivities'], []);
            renderCommunityFeed(comments, voteActivities);
            setCommunityStatus('');
            return true;
        } catch (error) {
            setCommunityStatus(`加载失败：${error.message}`, true);
            return false;
        } finally {
            setCommunityBusy(false);
            updateCharCounter();
        }
    };

    updateCharCounter();

    commentInput?.addEventListener('input', () => {
        updateCharCounter();
    });

    commentForm?.addEventListener('submit', async (event) => {
        event.preventDefault();
        if (!cardDefinitionId || !userId || !commentInput || isCommunityBusy) return;

        const content = commentInput.value.trim();
        if (!content) {
            setCommunityStatus('评论内容不能为空。', true);
            return;
        }

        let createPayload = null;
        setCommunityBusy(true);
        setCommunityStatus('提交中...');
        try {
            createPayload = await createCardComment(cardDefinitionId, userId, content);
            commentInput.value = '';
            updateCharCounter();
        } catch (error) {
            setCommunityStatus(`评论失败：${error.message}`, true);
        } finally {
            setCommunityBusy(false);
            updateCharCounter();
        }

        if (createPayload && overlay.isConnected) {
            const questProgress = extractQuestProgress(createPayload);
            emitQuestProgressUpdated(userId, questProgress);
            const reloaded = await loadCommunity();
            if (overlay.isConnected && reloaded) {
                setCommunityStatus(buildActivityQuestMessage('评论已发布。', questProgress));
            }
        }
    });

    const handleVoteClick = async (voteType) => {
        if (!cardDefinitionId || !userId || isCommunityBusy) return;
        let votePayload = null;
        setCommunityBusy(true);
        setCommunityStatus('提交中...');
        try {
            votePayload = await submitCardVote(cardDefinitionId, userId, voteType);
            applyVoteState(votePayload);
        } catch (error) {
            setCommunityStatus(`投票失败：${error.message}`, true);
        } finally {
            setCommunityBusy(false);
        }

        if (votePayload && overlay.isConnected) {
            const questProgress = extractQuestProgress(votePayload);
            emitQuestProgressUpdated(userId, questProgress);
            const successMessage = buildActivityQuestMessage('投票已更新。', questProgress);
            if (isSidebarOpen) {
                const reloaded = await loadCommunity();
                if (overlay.isConnected && reloaded) {
                    setCommunityStatus(successMessage);
                }
            } else {
                setCommunityStatus(successMessage);
            }
        }
    };

    voteUpBtn?.addEventListener('click', () => {
        void handleVoteClick('up');
    });
    voteDownBtn?.addEventListener('click', () => {
        void handleVoteClick('down');
    });
    toggleCommentsBtn?.addEventListener('click', () => {
        const nextOpen = !isSidebarOpen;
        setSidebarOpen(nextOpen);
        if (nextOpen && canUseCommunity) {
            void loadCommunity();
        }
    });

    commentsContainer?.addEventListener('click', (event) => {
        const button = event.target.closest('[data-comment-id]');
        if (!button) return;
        const commentId = String(button.getAttribute('data-comment-id') || '').trim();
        if (!commentId || !cardDefinitionId || !userId || isCommunityBusy) return;
        setCommunityBusy(true);
        setCommunityStatus('删除中...');
        void deleteCardComment(cardDefinitionId, commentId, userId)
            .then(async () => {
                setCommunityBusy(false);
                if (overlay.isConnected) {
                    await loadCommunity();
                }
            })
            .catch((error) => {
                setCommunityStatus(`删除失败：${error.message}`, true);
                setCommunityBusy(false);
            });
    });

    if (communityDisabled) {
        setCommunityStatus(communityStatusText, false);
    } else {
        setCommunityStatus('');
    }

    document.body.appendChild(overlay);
    ensureDetailEnumLocalization(overlay);
    void hydrateDescriptionReferences(overlay, {
        cards: modalOptions.cards,
        fallbackPreviewUrl: previewUrl,
        currentCardId,
        currentCardName: name
    });
    if (canUseCommunity) {
        void loadCommunity();
    }
}
