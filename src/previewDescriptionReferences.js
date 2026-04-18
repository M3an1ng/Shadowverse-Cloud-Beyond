import { renderDescriptionHtml } from './descriptionParser.js';
import { resolveDerivedCardMatch } from './previewDescriptionLookup.js';

const EMPTY_DESCRIPTION_HTML = '<span class="text-dim"><暂无描述></span>';

function normalizeCardName(value) {
    return String(value ?? '').trim().toLowerCase();
}

function resolveField(card, keys) {
    for (const key of keys) {
        const value = card?.[key];
        if (value !== undefined && value !== null && value !== '') {
            return value;
        }
    }
    return null;
}

function resolveNumber(card, keys) {
    const raw = resolveField(card, keys);
    if (raw === null) return null;
    const value = Number(raw);
    return Number.isFinite(value) ? value : null;
}

function resolvePreviewUrl(card) {
    const path = resolveField(card, ['artPreview', 'ArtPreview']);
    if (!path) return '';
    return normalizePreviewUrl(path);
}

function normalizePreviewUrl(value) {
    if (!value) return '';
    return encodeURI(String(value)).replace(/'/g, '%27');
}

function escapeHtml(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function buildStatsText(card) {
    const attack = resolveNumber(card, ['attack', 'Attack', 'currentAttack', 'CurrentAttack']);
    const health = resolveNumber(card, ['health', 'Health', 'currentHealth', 'CurrentHealth']);
    if (attack === null || health === null) return '';
    return `${attack} / ${health}`;
}

function buildReferenceKey(card, fallbackName = '') {
    const cardId = resolveField(card, ['id', 'Id', 'CardId', 'cardDefinitionId', 'CardDefinitionId']);
    if (cardId !== null && cardId !== undefined && String(cardId).trim() !== '') {
        return `id:${String(cardId).trim()}`;
    }

    const normalizedName = normalizeCardName(resolveField(card, ['name', 'Name']) || fallbackName);
    return normalizedName ? `name:${normalizedName}` : null;
}

function buildReferenceChain(options = {}) {
    const chain = Array.isArray(options.referenceChain) ? [...options.referenceChain] : [];
    const currentKey = buildReferenceKey({
        id: options.currentCardId,
        name: options.currentCardName
    });
    if (currentKey && !chain.includes(currentKey)) {
        chain.push(currentKey);
    }
    return chain;
}

function buildReferenceCardHtml(card, displayName, options) {
    const previewUrl = resolvePreviewUrl(card);
    const fallbackPreviewUrl = normalizePreviewUrl(options?.fallbackPreviewUrl);
    const resolvedPreviewUrl = previewUrl || fallbackPreviewUrl;
    const statsText = buildStatsText(card);
    const description = String(resolveField(card, ['description', 'Description']) ?? '').trim();
    const descriptionHtml = description ? renderDescriptionHtml(description) : EMPTY_DESCRIPTION_HTML;
    const titleText = displayName || resolveField(card, ['name', 'Name']) || '';
    const iconClass = resolvedPreviewUrl ? 'preview-ref-icon' : 'preview-ref-icon is-empty';
    const statsHtml = statsText
        ? `<div class="preview-ref-stats">${statsText}</div>`
        : '';
    const headerClass = statsText ? 'preview-ref-header has-stats' : 'preview-ref-header no-stats';
    const iconHtml = resolvedPreviewUrl
        ? `<div class="${iconClass}" style="background-image: url('${resolvedPreviewUrl}');"></div>`
        : `<div class="${iconClass}"><span>?</span></div>`;

    return `
        <div class="preview-ref-card">
            <div class="${headerClass}">
                ${iconHtml}
                <div class="preview-ref-title">${escapeHtml(titleText)}</div>
                ${statsHtml}
            </div>
            <div class="preview-ref-body">${descriptionHtml}</div>
        </div>
    `;
}

function buildReferenceWarningHtml(displayName, message) {
    return `
        <div class="preview-ref-card preview-ref-card-warning">
            <div class="preview-ref-header no-stats">
                <div class="preview-ref-icon is-empty"><span>!</span></div>
                <div class="preview-ref-title">${escapeHtml(displayName)}</div>
            </div>
            <div class="preview-ref-body">${escapeHtml(message)}</div>
        </div>
    `;
}

async function hydrateReferenceElement(element, options) {
    if (!element || element.dataset.previewReferenceHydrated === 'true') {
        return;
    }

    element.dataset.previewReferenceHydrated = 'true';
    const rawName = element.dataset.previewReference || element.textContent || '';
    const name = String(rawName).replace(/^\{/, '').replace(/\}$/, '').trim();
    if (!name) {
        return;
    }

    let result = null;
    try {
        result = await resolveDerivedCardMatch(name, options);
    } catch (error) {
        console.warn('PreviewDescription: Failed to resolve reference card.', error);
        return;
    }

    if (!result?.match) {
        return;
    }

    const referenceChain = buildReferenceChain(options);
    const matchKey = buildReferenceKey(result.match, name);
    if (matchKey && referenceChain.includes(matchKey)) {
        element.classList.add('is-resolved', 'has-reference-cycle');
        element.innerHTML = buildReferenceWarningHtml(name, '检测到引用循环，已停止展开。');
        return;
    }

    const nestedOptions = {
        ...options,
        referenceChain: matchKey ? [...referenceChain, matchKey] : referenceChain
    };

    element.classList.add('is-resolved');
    element.innerHTML = buildReferenceCardHtml(result.match, name, nestedOptions);
    await hydrateDescriptionReferences(element, nestedOptions);
}

export async function hydrateDescriptionReferences(container, options = {}) {
    if (!container) return;
    const elements = Array.from(container.querySelectorAll('.preview-description-reference'));
    if (elements.length === 0) return;
    await Promise.all(elements.map(element => hydrateReferenceElement(element, options)));
}
