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
        isFollower = false,
        attackValue = 0,
        healthValue = 0,
        countdownValue = null,
        showCountdown = false,
        earthsigilValue = 0,
        showEarthsigil = false,
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
        ? renderDescriptionHtml(description)
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
        : (showCountdown || showEarthsigil)
            ? `
                <div class="header-stats">
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

    const costClass = `slate-cost-badge-embedded${enhanceActive ? ' enhance-active' : ''}`;
    const nameClassAttr = nameClasses.join(' ');
    const nameAttr = nameDetailAttr ? ` ${nameDetailAttr}` : '';
    const artClass = previewUrl ? ' has-art' : '';
    const artHtml = previewUrl
        ? `<div class="slate-art-image" style="background-image: url('${previewUrl}');"></div>`
        : '';

    return `
        <div class="preview-slate rarity-${rarityClass}">
              <div class="slate-header">
                      <div class="slate-identity">
                      <div class="identity-top">
                          <div class="${costClass}">
                              <span class="cost-value">${costValue}</span>
                          </div>
                          <h2 class="${nameClassAttr}"${nameAttr}>${name}</h2>
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
