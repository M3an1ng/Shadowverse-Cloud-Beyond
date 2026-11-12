import { renderDescriptionHtml } from './descriptionParser.js';

const DEFAULT_DESCRIPTION_HTML = '<span class="text-dim"><暂无描述></span>';

export function buildPreviewSlateHtml(options) {
    if (!options) return '';

    const {
        rarityClass = 'common',
        rarityLabel = 'Common',
        classLabel = 'Neutral',
        typeLabel = 'Spell',
        name = 'UNTITLED',
        costValue = 0,
        enhanceActive = false,
        modeLabel = 'ENH',
        alternateActive = false,
        isFollower = false,
        isLeader = false,
        isMirageCard = false,
        mirageBuyCost = null,
        attackValue = 0,
        healthValue = 0,
        countdownValue = null,
        showCountdown = false,
        earthsigilValue = 0,
        showEarthsigil = false,
        evidenceValue = 0,
        showEvidence = false,
        description = '',
        hideEmptyDescriptionPlaceholder = false,
        previewUrl = '',
        detailUrl = '',
        enableDetail = false,
        tribeText = '',
        counterSummaryHtml = '',
        buffSummaryHtml = '',
        actionsHtml = ''
    } = options;

    const descriptionHtml = description
        ? renderDescriptionHtml(description, { isIntromezzoActive: options.isIntromezzoActive })
        : (hideEmptyDescriptionPlaceholder ? '' : DEFAULT_DESCRIPTION_HTML);

    const descriptionBlockHtml = descriptionHtml
        ? `<div class="slate-content-area"><div class="slate-description custom-scrollbar">${descriptionHtml}</div></div>`
        : '';

    const nameClasses = ['slate-name'];
    let nameDetailAttr = '';
    if (enableDetail) {
        nameClasses.push('preview-detail-name');
        if (detailUrl) {
            nameDetailAttr = `data-detail-src="${detailUrl}"`;
        }
    }

    const safeTribeText = typeof tribeText === 'string' ? tribeText : '';
    const tribeTitle = safeTribeText.replace(/"/g, '&quot;');
    const tribeMetaHtml = safeTribeText
        ? `<span class="meta-tag tribe-tag" title="${tribeTitle}">${safeTribeText}</span>`
        : '';

    const headerStatsHtml = isFollower
        ? `
                  <div class="header-stats">
                      <div class="stat-mini attack">
                          <span class="stat-icon">⚔</span>
                          <span class="stat-value">${attackValue}</span>
                    </div>
                    <div class="stat-mini health">
                        <span class="stat-icon">♥</span>
                        <span class="stat-value">${healthValue}</span>
                    </div>
                </div>
                `
        : (showCountdown || showEarthsigil || showEvidence)
            ? `
                <div class="header-stats">
                    ${showEvidence ? `<div class="stat-mini evidence" title="证据数"><span class="stat-icon evidence-icon" aria-hidden="true"></span><span class="stat-value">${evidenceValue}</span></div>` : ''}
                    ${showEarthsigil
            ? `
                    <div class="stat-mini earthsigil">
                        <span class="stat-icon earthsigil-icon" aria-hidden="true"></span>
                        <span class="stat-value">${earthsigilValue}</span>
                    </div>
                    `
            : ''}
                    ${showCountdown
            ? `
                    <div class="stat-mini countdown">
                        <span class="stat-icon">⧗</span>
                        <span class="stat-value">${countdownValue}</span>
                    </div>
                    `
            : ''}
                </div>
                `
            : '';

    const costClass = `slate-cost-badge-embedded${enhanceActive ? ' enhance-active' : ''}${alternateActive ? ' alternate-active' : ''}`;
    const nameClassAttr = nameClasses.join(' ');
    const nameAttr = nameDetailAttr ? ` ${nameDetailAttr}` : '';
    const artClass = previewUrl ? ' has-art' : '';
    const artHtml = previewUrl
        ? `<div class="slate-art-image" style="background-image: url('${previewUrl}');"></div>`
        : '';

    const showMirageStar = Boolean(isMirageCard) && !isLeader && Number(costValue) > 0;
    const tierNum = Math.max(1, Math.trunc(Number(costValue) || 1));
    const costBadgeInnerHtml = showMirageStar
        ? `<span class="cost-value mirage-slate-tier"><svg viewBox="0 0 12 12" width="9" height="9" style="vertical-align:-1px;margin-right:1px;color:#ecd290;" aria-hidden="true"><path d="M6 0.7L7.45 4.55L11.3 6L7.45 7.45L6 11.3L4.55 7.45L0.7 6L4.55 4.55Z" fill="currentColor"/></svg>${tierNum}</span>`
        : `<span class="cost-value">${costValue}</span>`;
    const mirageGoldChipHtml = (Boolean(isMirageCard) && !isLeader && mirageBuyCost !== null && mirageBuyCost !== undefined && Number.isFinite(Number(mirageBuyCost)))
        ? `<span class="meta-tag mirage-gold-tag" title="酒馆购买消耗金币" style="margin-left:auto;color:#fef3c7;border:1px solid rgba(251,191,36,0.75);background:rgba(92,57,11,0.72);font-weight:700;">${Number(mirageBuyCost)}G</span>`
        : '';

    return `
        <div class="preview-slate rarity-${rarityClass}">
              <div class="slate-header">
                      <div class="slate-identity">
                      <div class="identity-top">
                          ${(Boolean(isMirageCard) && isLeader) ? '' : `
                          <div class="${costClass}" data-play-mode-label="${['ACC','CRY','ENH'].includes(modeLabel) ? modeLabel : ''}">
                              ${costBadgeInnerHtml}
                          </div>
                          `}
                          <h2 class="${nameClassAttr}"${nameAttr}>${name}</h2>
                          ${mirageGoldChipHtml}
                      </div>
                      <div class="slate-meta">
                          <span class="meta-tag rarity-tag">${rarityLabel}</span>
                          <span class="meta-tag class-tag">${classLabel}</span>
                          <span class="meta-tag type-tag">${typeLabel}</span>
                          ${tribeMetaHtml}
                      </div>
                  </div>
                  ${headerStatsHtml}
            </div>

            <div class="slate-art-background${artClass}">
                ${artHtml}
            </div>

            <div class="slate-body">
                ${counterSummaryHtml || ''}
                ${descriptionBlockHtml}
                ${buffSummaryHtml || ''}
                ${actionsHtml || ''}
            </div>
            
            <div class="holo-scanline"></div>
            <div class="holo-grid-overlay"></div>
        </div>
    `;
}
