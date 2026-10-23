import { renderDescriptionHtml } from './descriptionParser.js';
import { resolveKeywordInfo, resolveDerivedCardMatch } from './previewDescriptionLookup.js';
import { showCardDetailModal } from './components/CardDetailMockup.js';

let activeTooltip = null;
let activeRequestId = 0;
let detachHandlers = null;

function normalizeCardName(value) {
    return String(value ?? '').trim().toLowerCase();
}

function resolveCardId(card) {
    return card?.id ?? card?.CardId ?? card?.Id ?? null;
}

function isSameCard(match, options = {}) {
    if (!match) return false;
    const currentId = options.currentCardId;
    const matchId = resolveCardId(match);
    if (currentId !== null && currentId !== undefined && matchId !== null && matchId !== undefined) {
        return String(currentId) === String(matchId);
    }
    const currentName = normalizeCardName(options.currentCardName);
    const matchName = normalizeCardName(match?.name ?? match?.Name);
    return Boolean(currentName && matchName && currentName === matchName);
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function ensureTooltip() {
    if (activeTooltip) return activeTooltip;
    const tooltip = document.createElement('div');
    tooltip.className = 'preview-description-tooltip';
    tooltip.setAttribute('role', 'dialog');
    tooltip.setAttribute('aria-live', 'polite');
    tooltip._previewOptions = {};
    tooltip.addEventListener('click', (event) => {
        const tokenEl = event.target.closest('[data-preview-token]');
        if (!tokenEl || !tooltip.contains(tokenEl)) return;
        const tokenType = tokenEl.dataset.previewToken;
        const tokenValue = tokenEl.dataset.previewValue;
        if (!tokenType || !tokenValue) return;
        event.preventDefault();
        event.stopPropagation();
        void showPreviewDescriptionTooltip(tokenEl, tokenType, tokenValue, tooltip._previewOptions || {});
    });
    document.body.appendChild(tooltip);
    activeTooltip = tooltip;
    return tooltip;
}

function clearTooltip() {
    if (!activeTooltip) return;
    activeTooltip.remove();
    activeTooltip = null;
}

function detachGlobalHandlers() {
    if (detachHandlers) {
        detachHandlers();
        detachHandlers = null;
    }
}

function attachGlobalHandlers() {
    detachGlobalHandlers();
    const onDocumentClick = (event) => {
        const clickedTooltip = event.target.closest('.preview-description-tooltip');
        const clickedToken = event.target.closest('[data-preview-token]');
        if (clickedTooltip || clickedToken) return;
        hidePreviewDescriptionTooltip();
    };
    const onDocumentKeydown = (event) => {
        if (event.key === 'Escape') {
            hidePreviewDescriptionTooltip();
        }
    };
    document.addEventListener('click', onDocumentClick, true);
    document.addEventListener('keydown', onDocumentKeydown);
    detachHandlers = () => {
        document.removeEventListener('click', onDocumentClick, true);
        document.removeEventListener('keydown', onDocumentKeydown);
    };
}

function positionTooltip(tooltip, anchorEl) {
    if (!tooltip || !anchorEl) return;
    const anchorRect = anchorEl.getBoundingClientRect();
    const tooltipRect = tooltip.getBoundingClientRect();
    const padding = 12;
    const offset = 10;

    let left = anchorRect.left;
    let top = anchorRect.bottom + offset;

    if (left + tooltipRect.width > window.innerWidth - padding) {
        left = window.innerWidth - tooltipRect.width - padding;
    }
    if (top + tooltipRect.height > window.innerHeight - padding) {
        top = anchorRect.top - tooltipRect.height - offset;
    }

    left = Math.max(padding, left);
    top = Math.max(padding, top);

    tooltip.style.left = `${Math.round(left)}px`;
    tooltip.style.top = `${Math.round(top)}px`;
}

function buildLoadingHtml(label) {
    return `
        <div class="preview-tooltip-header">
            <span class="preview-tooltip-title">${escapeHtml(label)}</span>
        </div>
        <div class="preview-tooltip-body">加载中...</div>
    `;
}

const keywordKindLabels = {
    card: '卡牌关键字',
    ability: '能力关键字',
    deck: '牌组关键字'
};

function buildKeywordHtml(info, rawLabel) {
    if (!info) {
        return `
            <div class="preview-tooltip-header">
                <span class="preview-tooltip-title">${escapeHtml(rawLabel)}</span>
            </div>
            <div class="preview-tooltip-body">暂无关键词说明。</div>
        `;
    }
    const kindLabel = keywordKindLabels[info.kind] || '关键词';
    const description = info.description ? renderDescriptionHtml(info.description) : '暂无关键词说明。';
    return `
        <div class="preview-tooltip-header">
            <span class="preview-tooltip-title">${escapeHtml(info.name)}</span>
            <span class="preview-tooltip-tag">${escapeHtml(kindLabel)}</span>
        </div>
        <div class="preview-tooltip-body">${description}</div>
    `;
}

function buildDerivedMissingHtml(rawName) {
    return `
        <div class="preview-tooltip-header">
            <span class="preview-tooltip-title">${escapeHtml(rawName)}</span>
        </div>
        <div class="preview-tooltip-body">未找到对应的衍生卡详情图。</div>
    `;
}

function buildDerivedMultipleHtml(rawName, matches) {
    const ids = (matches || [])
        .map(card => card?.id ?? card?.CardId ?? card?.Id)
        .filter(id => id !== null && id !== undefined)
        .map(id => String(id));
    const idList = ids.length ? `可用ID：${ids.join(', ')}` : '请使用ID指定衍生物。';
    return `
        <div class="preview-tooltip-header">
            <span class="preview-tooltip-title">${escapeHtml(rawName)}</span>
        </div>
        <div class="preview-tooltip-body">检测到同名衍生物，请在名称后添加（ID）指定。${escapeHtml(idList)}</div>
    `;
}

function buildDerivedIdMissingHtml(rawName) {
    return `
        <div class="preview-tooltip-header">
            <span class="preview-tooltip-title">${escapeHtml(rawName)}</span>
        </div>
        <div class="preview-tooltip-body">指定的ID未找到，请检查名称与ID是否正确。</div>
    `;
}

function resolveDerivedArtUrls(card) {
    const previewPath = card?.artPreview ?? card?.ArtPreview ?? '';
    const detailPath = card?.artDetail ?? card?.ArtDetail ?? '';
    return {
        previewUrl: previewPath ? encodeURI(previewPath).replace(/'/g, '%27') : '',
        detailUrl: detailPath ? encodeURI(detailPath).replace(/'/g, '%27') : ''
    };
}

export async function showPreviewDescriptionTooltip(anchorEl, tokenType, tokenValue, options = {}) {
    if (!anchorEl || !tokenType || !tokenValue) return;
    const tooltip = ensureTooltip();
    tooltip._previewOptions = { ...options };
    const requestId = ++activeRequestId;

    let contentHtml = '';
    if (tokenType === 'keyword') {
        tooltip.innerHTML = buildLoadingHtml(tokenValue);
        tooltip.classList.add('visible');
        positionTooltip(tooltip, anchorEl);
        attachGlobalHandlers();
        const info = await resolveKeywordInfo(tokenValue);
        if (requestId !== activeRequestId) return;
        contentHtml = buildKeywordHtml(info, tokenValue);
    } else if (tokenType === 'derived') {
        tooltip.innerHTML = buildLoadingHtml(tokenValue);
        tooltip.classList.add('visible');
        positionTooltip(tooltip, anchorEl);
        attachGlobalHandlers();
        const result = await resolveDerivedCardMatch(tokenValue, options);
        if (requestId !== activeRequestId) return;
        if (result.reason === 'multiple') {
            contentHtml = buildDerivedMultipleHtml(tokenValue, result.matches);
        } else if (result.reason === 'id_not_found') {
            contentHtml = buildDerivedIdMissingHtml(tokenValue);
        } else if (!result.match) {
            contentHtml = buildDerivedMissingHtml(tokenValue);
        } else {
            if (isSameCard(result.match, options)) {
                hidePreviewDescriptionTooltip();
                return;
            }
            const { previewUrl, detailUrl } = resolveDerivedArtUrls(result.match);
            hidePreviewDescriptionTooltip();
            showCardDetailModal(result.match, {
                previewUrl,
                detailUrl,
                cards: options.cards,
                navigationMode: 'push'
            });
            return;
        }
    }

    if (contentHtml) {
        tooltip.innerHTML = contentHtml;
        positionTooltip(tooltip, anchorEl);
    }
}

export function hidePreviewDescriptionTooltip() {
    activeRequestId += 1;
    detachGlobalHandlers();
    clearTooltip();
}
