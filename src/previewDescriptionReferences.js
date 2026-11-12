import './styles/alternate-reference.css';
import { ensureAbilityData, getActiveAbilityDataScope } from './abilityDataCache.js';
import { normalizeCardPoolScope } from './cardDefinitionsLoader.js';
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
    return encodeURI(String(value)).replace(/['"()\\]/g, char => `%${char.charCodeAt(0).toString(16).toUpperCase()}`);
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

export function buildReferenceCardHtml(card, displayName, options = {}) {
    const previewUrl = resolvePreviewUrl(card);
    const fallbackPreviewUrl = normalizePreviewUrl(options?.fallbackPreviewUrl);
    const alternateReference = /^(激奏|结晶|結晶)[:：]/.test(String(resolveField(card, ['name','Name']) || displayName || ''));
    const cost = resolveNumber(card, ['cost','Cost','currentCost','CurrentCost']);
    const referenceCost = cost === null ? null : Math.max(0, Math.trunc(cost));
    const resolvedPreviewUrl = alternateReference ? '' : previewUrl || fallbackPreviewUrl;
    const statsText = buildStatsText(card);
    const description = String(resolveField(card, ['description', 'Description']) ?? '').trim();
    const descriptionHtml = description ? renderDescriptionHtml(description) : EMPTY_DESCRIPTION_HTML;
    const titleText = resolveField(card, ['name', 'Name']) || displayName || '';
    const iconClass = resolvedPreviewUrl ? 'preview-ref-icon' : 'preview-ref-icon is-empty';
    const statsHtml = statsText
        ? `<div class="preview-ref-stats">${statsText}</div>`
        : '';
    const headerClass = statsText ? 'preview-ref-header has-stats' : 'preview-ref-header no-stats';
    const navigationAttrs = typeof options.openCardDetail === 'function'
        ? ` role="button" tabindex="0" aria-label="查看${escapeHtml(titleText)}详情" title="查看卡牌详情"`
        : '';
    const iconHtml = alternateReference
        ? `<div class="preview-ref-icon preview-ref-cost" aria-label="费用 ${referenceCost ?? '?'}"><img src="/assets/reference-cost/cost_${Math.min(10,referenceCost ?? 0)}.png" alt=""><span>${referenceCost !== null && referenceCost > 10 ? referenceCost : ''}</span></div>`
        : resolvedPreviewUrl
        ? `<div class="${iconClass}" style="background-image: url('${resolvedPreviewUrl}');"></div>`
        : `<div class="${iconClass}"><span>?</span></div>`;

    return `
        <div class="preview-ref-card">
            <div class="${headerClass}"${navigationAttrs}>
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

export function bindReferenceCardDetails(container, card, options = {}) {
    const header = container.querySelector(':scope > .preview-ref-card > .preview-ref-header');
    if (!header || typeof options.openCardDetail !== 'function') return;
    let opening = false;
    const open = async event => {
        event.preventDefault();
        event.stopPropagation();
        if (opening) return;
        opening = true;
        try {
            let detailCard = card;
            const id = resolveNumber(card, ['id','Id','CardId','cardDefinitionId','CardDefinitionId']);
            // Runtime reference DTOs omit class/type and other catalog detail fields.
            if (id && !resolveField(card, ['type','Type','cardType','CardType'])) {
                const local = options.cards?.find(candidate => Number(resolveField(candidate, ['id','Id','CardId'])) === id);
                if (local) detailCard = local;
                else {
                    const scope = normalizeCardPoolScope(options.scope || getActiveAbilityDataScope());
                    const response = await fetch(`/api/cards/${id}?scope=${encodeURIComponent(scope)}`);
                    if (!response.ok) throw new Error(`Card detail request failed: ${response.status}`);
                    detailCard = await response.json();
                }
            }
            await options.openCardDetail(detailCard);
        } catch (error) {
            console.warn('Reference card detail unavailable', error);
        } finally { opening = false; }
    };
    header.addEventListener('click', open);
    header.addEventListener('keydown', event => {
        if (event.key === 'Enter' || event.key === ' ') void open(event);
    });
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
    bindReferenceCardDetails(element, result.match, nestedOptions);
    await hydrateDescriptionReferences(element, nestedOptions);
}

export async function hydrateDescriptionReferences(container, options = {}) {
    if (!container) return;
    const elements = Array.from(container.querySelectorAll('.preview-description-reference'));
    if (elements.length === 0) return;
    await Promise.all(elements.map(element => hydrateReferenceElement(element, options)));
}

export function collectAlternateReferences(abilities) {
    const references = new Map();
    for (const ability of abilities || []) {
        let config;
        try { config = JSON.parse(ability.jsonConfig ?? ability.JsonConfig ?? '{}'); } catch { continue; }
        const alternate = config.alternatePlay ?? config.AlternatePlay;
        const keywords = config.abilityKeywords ?? config.AbilityKeywords ?? [];
        const effects = config.effects ?? config.Effects ?? [];
        const declarations = effects.filter(e => ['Accelerate','Crystalize',11,12].includes(e.type ?? e.Type));
        if (alternate) declarations.push({ type: keywords.some(k => ['Accelerate',118].includes(k.keyword ?? k.Keyword)) ? 'Accelerate' : 'Crystalize', id: alternate.playAsCardDefinitionId ?? alternate.PlayAsCardDefinitionId });
        for (const declaration of declarations) {
            const params = declaration.parameters ?? declaration.Parameters ?? {};
            const raw = params.CardDefinitionId ?? params.cardDefinitionId;
            const id = Number(declaration.id ?? raw?.Value ?? raw?.value ?? raw);
            if (Number.isInteger(id) && id > 0) references.set(id, { id, label: ['Accelerate',11].includes(declaration.type ?? declaration.Type) ? '激奏' : '结晶' });
        }
    }
    return [...references.values()];
}
export async function hydrateAlternatePlayReferences(container, cardId, options = {}) {
    if (!container || !cardId) return;
    const marker = String(cardId); container.dataset.alternateCardId = marker;
    try {
        const supplied = Array.isArray(options.references);
        const scope = normalizeCardPoolScope(options.scope || getActiveAbilityDataScope());
        const data = supplied ? null : await ensureAbilityData(scope);
        if (!container.isConnected || container.dataset.alternateCardId !== marker) return;
        const references = supplied ? options.references.map(card => ({ id: Number(card.Id ?? card.id), label: card.Label ?? card.label, card })) : collectAlternateReferences(data.abilitiesByCard.get(Number(cardId)) || []);
        const host = document.createElement('section'); host.className = 'alternate-play-references';
        for (const reference of references) {
            if (reference.id === Number(cardId)) continue;
            const card = reference.card ?? data?.cardIndex.get(reference.id); if (!card) continue;
            const referenceName = normalizeCardName(card.name ?? card.Name);
            const alreadyIncluded = [...container.querySelectorAll('.preview-description-reference')].some(element => {
                const name = String(element.dataset.previewReference || '').replace(/^\{/, '').replace(/\}$/, '').trim();
                return normalizeCardName(name.replace(/\s*[（(]\d+[）)]$/, '')) === referenceName;
            });
            if (alreadyIncluded) continue;
            const item = document.createElement('div'); item.className = 'alternate-play-reference';
            const heading = document.createElement('h4'); heading.textContent = reference.label;
            const preview = document.createElement('div'); preview.innerHTML = buildReferenceCardHtml(options.fallbackPreviewUrl ? {...card,artPreview:options.fallbackPreviewUrl,ArtPreview:options.fallbackPreviewUrl} : card, card.name ?? card.Name, options);
            bindReferenceCardDetails(preview, card, options);
            item.append(heading,preview); host.append(item);
        }
        container.querySelector(':scope > .alternate-play-references')?.remove();
        if (host.childElementCount) container.append(host);
    } catch (error) { console.warn('Alternate play preview unavailable',error); }
}
