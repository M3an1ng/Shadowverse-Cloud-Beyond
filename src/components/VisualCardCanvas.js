// src/components/VisualCardCanvas.js
// 可视化卡面直编组件（基于 MSE Beyond 渲染内核，适配星云沉浸式无痕原位交互）
import { openCardSetManagerModal } from '../devToolsCardSetEnvironmentMembership.js';

import {
    CLASSES,
    TYPES,
    RARITIES,
    SECTION_STYLES,
    descriptionFromSections
} from '../mse-editor-test/document.js';
import {
    WIDTH,
    HEIGHT,
    loadFonts,
    loadImage,
    drawCroppedArt,
    renderCard
} from '../mse-editor-test/renderer.js';

const META_STORAGE_PREFIX = 'svwb-mse-card-meta:';
const MAX_SECTIONS = 5;

const SECTION_STYLE_SHORT_LABELS = {
    '普通': '普通',
    '普通（分割线）': '分割线',
    '进化': '进化',
    '超进化': '超进化',
    '纹章': '纹章',
    '信仰': '信仰'
};

const FIELD_PICKER_TITLES = {
    class: '选择职业',
    rarity: '选择稀有度',
    type: '卡牌类型',
    cardSet: '选择卡包'
};

function escapeHtml(text) {
    return String(text ?? '').replace(/[&<>"']/g, ch => ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;'
    }[ch]));
}

function createEmptySection() {
    return { text: '', style: '普通', icon: '' };
}

/**
 * 将卡牌的纯文本 description 逆向拆解为 MSE 五段式规则结构
 */
export function sectionsFromDescription(description) {
    const emptySections = () => Array.from({ length: MAX_SECTIONS }, createEmptySection);
    const raw = String(description ?? '').trim();
    if (!raw) {
        return emptySections();
    }

    const blocks = raw
        .split(/\r?\n\s*\r?\n/)
        .map(b => b.trim())
        .filter(Boolean);

    const sections = emptySections();
    let idx = 0;

    for (const block of blocks) {
        if (idx >= MAX_SECTIONS) {
            sections[MAX_SECTIONS - 1].text += `\n${block}`;
            continue;
        }

        let style = '普通';
        let body = block.replace(/\r?\n──\r?\n/g, '==');

        const prefixMatch = body.match(/^【(进化|超进化|纹章|信仰)】\r?\n([\s\S]*)$/);
        if (prefixMatch) {
            style = prefixMatch[1];
            body = prefixMatch[2].trim();
        } else if (/^【进化时】|^进化后/.test(body)) {
            style = '进化';
        } else if (/^【超进化时】|^超进化后/.test(body)) {
            style = '超进化';
        } else if (/^【纹章】/.test(body)) {
            style = '纹章';
        } else if (/^【信仰】/.test(body)) {
            style = '信仰';
        }

        sections[idx] = { text: body, style, icon: '' };
        idx++;
    }

    return sections;
}

export class VisualCardCanvas {
    constructor(cardEditor) {
        this.cardEditor = cardEditor;
        this.host = null;
        this.settingsHost = null;
        this.mseCard = null;
        this.selectedField = '';
        this.activeSectionIndex = 0;
        this.inlineEditor = null;
        this.activeFloatingField = null; // 当前打开悬浮气泡面板的字段 key（select 或 number）
        this.rendered = null;
        this.renderGeneration = 0;
        this.scale = 1;
        this.showHotspots = true;
        this.artDirty = false;
        this.artDrag = null;
        this.resizeObserver = null;
        this.mountAbortController = null;
        this.fontsReadyPromise = loadFonts().catch(err => {
            console.warn('VisualCardCanvas: Fonts load warning:', err);
        });
    }

    dispose() {
        this.mountAbortController?.abort();
        this.mountAbortController = null;
        this.resizeObserver?.disconnect();
        this.resizeObserver = null;
        this.inlineEditor = null;
        this.activeFloatingField = null;
    }

    query(selector) {
        return this.host?.querySelector(selector)
            || this.settingsHost?.querySelector(selector)
            || null;
    }

    getActiveOmitTextField() {
        const key = this.inlineEditor?.dataset?.key || '';
        if (!key) return null;
        if (key === 'name' || key === 'title-name' || key === 'tribe' || key === 'artist' || key.startsWith('section.')) {
            return key;
        }
        return null;
    }

    getMetaStorageKey(cardId) {
        return cardId != null ? `${META_STORAGE_PREFIX}${cardId}` : null;
    }

    loadSavedPresentation(cardId) {
        const key = this.getMetaStorageKey(cardId);
        if (!key) return null;
        try {
            const raw = localStorage.getItem(key);
            return raw ? JSON.parse(raw) : null;
        } catch (_) {
            return null;
        }
    }

    saveLocalPresentationMeta(cardId) {
        const key = this.getMetaStorageKey(cardId);
        if (!key || !this.mseCard?.presentation) return;
        try {
            const meta = {
                language: this.mseCard.presentation.language,
                fontSize: this.mseCard.presentation.fontSize,
                autoFitFontSize: this.mseCard.presentation.autoFitFontSize,
                numberStyle: this.mseCard.presentation.numberStyle,
                alternateArt: this.mseCard.presentation.alternateArt,
                artist: this.mseCard.presentation.artist,
                showArtist: this.mseCard.presentation.showArtist,
                crop: this.mseCard.presentation.art?.crop || { zoom: 1, x: 0.5, y: 0.5 },
                sections: this.mseCard.presentation.sections
            };
            localStorage.setItem(key, JSON.stringify(meta));
        } catch (err) {
            console.warn('VisualCardCanvas: Failed to save presentation meta:', err);
        }
    }

    hasPendingArtChanges() {
        return Boolean(this.mseCard?.presentation?.art?.file || this.artDirty);
    }

    markArtSaved(newPreviewUrl) {
        const art = this.mseCard.presentation.art;
        art.file = null;
        art.src = newPreviewUrl || art.src;
        art.crop = { zoom: 1, x: 0.5, y: 0.5, flipX: false };
        this.artDirty = false;
        const zoomSlider = this.query('#vc-art-zoom');
        const zoomValue = this.query('#vc-zoom-val');
        if (zoomSlider) zoomSlider.value = '1';
        if (zoomValue) zoomValue.textContent = '100%';
    }

    getNonEmptySectionCount() {
        const sections = this.mseCard?.presentation?.sections;
        if (!Array.isArray(sections)) return 0;
        return sections.filter(sec => String(sec?.text ?? '').trim().length > 0).length;
    }

    compactEmptySections(keepIndex = -1) {
        const sections = this.mseCard?.presentation?.sections;
        if (!Array.isArray(sections)) return false;

        const preserved = [];
        sections.forEach((sec, idx) => {
            const text = String(sec?.text ?? '');
            if (text.trim().length > 0 || idx === keepIndex) {
                preserved.push({
                    text,
                    style: SECTION_STYLES.includes(sec?.style) ? sec.style : '普通',
                    icon: String(sec?.icon ?? '')
                });
            }
        });

        const nextSections = Array.from({ length: MAX_SECTIONS }, (_, i) => preserved[i] || createEmptySection());
        const changed = JSON.stringify(sections) !== JSON.stringify(nextSections);
        if (changed) {
            this.mseCard.presentation.sections = nextSections;
        }
        return changed;
    }

    syncFromEditorCard(editorCard) {
        if (!editorCard) return;

        const backendPres = editorCard.presentation && typeof editorCard.presentation === 'object' ? editorCard.presentation : null;
        const savedMeta = backendPres ?? (!editorCard.isNew && editorCard.id != null
            ? this.loadSavedPresentation(editorCard.id)
            : null);

        const normClass = (editorCard.cardClass && editorCard.cardClass in CLASSES)
            ? editorCard.cardClass
            : 'Neutral';
        const normType = (editorCard.cardType && editorCard.cardType in TYPES)
            ? editorCard.cardType
            : 'Follower';
        let normRarity = editorCard.rarity || 'Bronze';
        if (normRarity === 'Legendary') normRarity = 'Legend';
        if (normRarity === 'Common') normRarity = 'Bronze';
        if (!(normRarity in RARITIES)) normRarity = 'Bronze';

        const pendingArt = this.mseCard?.sourceCardId === (editorCard.id ?? null) && this.hasPendingArtChanges()
            ? this.mseCard.presentation.art
            : null;
        if (!pendingArt) this.artDirty = false;

        const previewArtUrl = editorCard.previewArtUrl || (editorCard.artPreview
            ? this.cardEditor.getArtAssetUrl(editorCard.artPreview)
            : '');

        const sections = savedMeta?.sections && Array.isArray(savedMeta.sections) && savedMeta.sections.length === MAX_SECTIONS
            ? savedMeta.sections.map(sec => ({
                text: String(sec?.text ?? ''),
                style: SECTION_STYLES.includes(sec?.style) ? sec.style : '普通',
                icon: String(sec?.icon ?? '')
            }))
            : sectionsFromDescription(editorCard.description);

        // 后端展示配置优先；旧卡只有本地缓存时，以数据库 description 解析为准。
        const reconstructedDesc = descriptionFromSections(sections);
        const finalSections = (!backendPres && editorCard.description && editorCard.description.trim() && reconstructedDesc.trim() !== editorCard.description.trim())
            ? sectionsFromDescription(editorCard.description)
            : sections;

        this.mseCard = {
            editorId: String(editorCard.id ?? 'new'),
            sourceCardId: editorCard.id ?? null,
            name: editorCard.name || '未命名卡牌',
            type: normType,
            class: normClass,
            rarity: normRarity,
            cardSet: editorCard.cardSet || '默认',
            cost: Number.isFinite(Number(editorCard.cost)) ? Number(editorCard.cost) : 0,
            attack: Number.isFinite(Number(editorCard.attack)) ? Number(editorCard.attack) : 0,
            health: Math.max(0, Number.isFinite(Number(editorCard.health)) ? Number(editorCard.health) : 1),
            keywords: Array.isArray(editorCard.keywords) ? [...editorCard.keywords] : [],
            tribe: Array.isArray(editorCard.tribe) ? [...editorCard.tribe] : [],
            countdown: editorCard.countdown ?? 0,
            isCollectible: editorCard.isCollectible ?? true,
            isImplemented: editorCard.isImplemented ?? (editorCard.isNew ? false : true),
            presentation: {
                template: 'shadowverse-beyond',
                version: 1,
                language: savedMeta?.language || 'zh-CN',
                fontSize: savedMeta?.fontSize || 16.8,
                autoFitFontSize: savedMeta?.autoFitFontSize ?? true,
                numberStyle: 'shadowverse-symd2',
                alternateArt: !!savedMeta?.alternateArt,
                artist: savedMeta?.artist ?? editorCard.artist ?? '',
                showArtist: savedMeta?.showArtist ?? true,
                // 服务器预览已裁切，只有未保存的本地原图继续使用当前裁切。
                art: pendingArt || {
                    src: previewArtUrl,
                    file: null,
                    crop: { zoom: 1, x: 0.5, y: 0.5, flipX: false, ...backendPres?.art?.crop }
                },
                background: savedMeta?.background ?? '',
                cardPackLogo: this.resolvePackLogoUrl(editorCard.cardSet),
                sections: finalSections
            }
        };
        this.compactEmptySections(-1);
    }

    syncToEditorCard() {
        const editorCard = this.cardEditor.currentCard;
        if (!editorCard || !this.mseCard) return;

        editorCard.name = this.mseCard.name === '未命名卡牌' && editorCard.isNew ? '' : this.mseCard.name;
        editorCard.cardType = this.mseCard.type;
        editorCard.cardClass = this.mseCard.class;
        editorCard.rarity = this.mseCard.rarity;
        editorCard.cardSet = this.mseCard.cardSet || '特殊';
        editorCard.cost = Number(this.mseCard.cost) || 0;
        editorCard.attack = Number(this.mseCard.attack) || 0;
        editorCard.health = Number(this.mseCard.health);
        editorCard.countdown = Number(this.mseCard.countdown) || 0;
        editorCard.isCollectible = !!this.mseCard.isCollectible;
        editorCard.isImplemented = !!this.mseCard.isImplemented;
        const { art, ...presentation } = this.mseCard.presentation;
        editorCard.presentation = {
            ...structuredClone(presentation),
            art: { src: '', crop: { ...art.crop } }
        };
        editorCard.tribe = Array.isArray(this.mseCard.tribe) ? [...this.mseCard.tribe] : [];
        editorCard.keywords = Array.isArray(this.mseCard.keywords) ? [...this.mseCard.keywords] : [];
        editorCard.description = descriptionFromSections(this.mseCard.presentation.sections);

        this.cardEditor.validateForm();
        this.cardEditor.updateCardPreview();
    }

    renderRightSettingsHtml(keywordOptions) {
        const isMirage = Boolean(this.cardEditor?.isMirageScope?.());
        return `
            <div class="mse-right-inspector">
                <section class="mse-inspector-group tech-section">
                    <h4 class="tech-header-small">归属与分类</h4>
                    <div class="vc-dock-item">
                        <label class="form-label" for="vc-card-set">卡包</label>
                        ${isMirage ? `
                            <div class="vc-inline-controls">
                                <input type="text" id="vc-card-set" class="tech-input" value="${escapeHtml(this.mseCard.cardSet)}" readonly disabled>
                            </div>
                        ` : `
                            <div class="vc-inline-controls">
                                <select id="vc-card-set" class="tech-select">
                                    ${this.cardSetOptionsHtml()}
                                </select>
                                <button type="button" class="btn-tech btn-small" data-vc-action="open-pack-manager" title="打开卡包管理（检视卡包、上传 Logo、配置环境）">卡包管理</button>
                            </div>
                        `}
                    </div>
                    <div class="vc-dock-item">
                        <label class="form-label" for="vc-tribe-input">种类 / 种族（· 分隔）</label>
                        <input type="text" id="vc-tribe-input" class="tech-input" list="vc-tribe-suggestions" value="${escapeHtml(this.mseCard.tribe.join('·'))}" placeholder="如：学园·财宝">
                        <datalist id="vc-tribe-suggestions"></datalist>
                    </div>
                </section>

                <section class="mse-inspector-group tech-section">
                    <h4 class="tech-header-small">版式与插画</h4>
                    <div class="vc-dock-row-2">
                        <div class="vc-dock-item">
                            <label class="form-label" for="vc-lang">规则字体</label>
                            <select id="vc-lang" class="tech-select">
                                <option value="zh-CN" ${this.mseCard.presentation.language === 'zh-CN' ? 'selected' : ''}>简体魏碑</option>
                                <option value="zh-TW" ${this.mseCard.presentation.language === 'zh-TW' ? 'selected' : ''}>繁体魏碑</option>
                            </select>
                        </div>
                        <div class="vc-dock-item vc-dock-align-end">
                            <label class="vc-check-label">
                                <input type="checkbox" id="vc-alt-art" ${this.mseCard.presentation.alternateArt ? 'checked' : ''}> 异画框
                            </label>
                        </div>
                    </div>
                    <div class="vc-dock-item">
                        <div class="vc-inline-controls vc-space-between">
                            <label class="form-label" for="vc-fontsize">规则字号 (<span id="vc-fontsize-val">${this.mseCard.presentation.fontSize}</span>pt)</label>
                            <label class="vc-check-label"><input type="checkbox" id="vc-autofit-font" ${this.mseCard.presentation.autoFitFontSize !== false ? 'checked' : ''}> 自动缩字</label>
                        </div>
                        <input type="range" id="vc-fontsize" class="vc-range-input" min="11" max="32" step="0.2" value="${this.mseCard.presentation.fontSize}">
                    </div>
                    <div class="vc-dock-item">
                        <div class="vc-inline-controls vc-space-between">
                            <label class="form-label" for="vc-art-zoom">插画缩放 (<span id="vc-zoom-val">${Math.round(this.mseCard.presentation.art.crop.zoom * 100)}%</span>)</label>
                        </div>
                        <input type="range" id="vc-art-zoom" class="vc-range-input" min="1" max="4" step="0.02" value="${this.mseCard.presentation.art.crop.zoom}">
                    </div>
                </section>

                <section class="mse-inspector-group tech-section">
                    <h4 class="tech-header-small">机制与标识</h4>
                    <div class="vc-inline-controls vc-flags-row">
                        <label class="vc-check-label">
                            <input type="checkbox" id="vc-collectible" ${this.mseCard.isCollectible ? 'checked' : ''}> 可收集
                        </label>
                        <label class="vc-check-label" title="第一阶段：待实装（仅卡面与云构筑）；第二阶段：已实装（可执行对战逻辑）">
                            <input type="checkbox" id="vc-implemented" ${this.mseCard.isImplemented ? 'checked' : ''}> 已实装
                        </label>
                        <label class="vc-countdown-inline" for="vc-countdown" title="护符/纹章倒计时（需同时绑定倒数关键字）">
                            <span class="form-label">倒数</span>
                            <input type="number" id="vc-countdown" class="tech-input vc-mini-num" min="0" max="99" value="${this.mseCard.countdown ?? 0}">
                        </label>
                    </div>
                    <div class="vc-dock-item vc-keywords-section">
                        <label class="form-label" for="vc-keyword-select">绑定关键字</label>
                        <div class="tag-list" id="vc-keywords-list"></div>
                        <select id="vc-keyword-select" class="tech-select vc-kw-select">
                            <option value="">+ 添加关键字...</option>
                            ${Object.entries(keywordOptions).map(([k, label]) => `<option value="${escapeHtml(k)}">${escapeHtml(label)} (${escapeHtml(k)})</option>`).join('')}
                        </select>
                    </div>
                </section>
            </div>
        `;
    }

    async mount(containerEl, options = {}) {
        this.dispose();
        this.mountAbortController = new AbortController();

        this.host = containerEl;
        this.settingsHost = options.settingsHost || null;
        this.selectedField = '';
        this.syncFromEditorCard(this.cardEditor.currentCard);
        await this.fontsReadyPromise;

        const keywordOptions = options.keywordOptions || {};

        this.host.innerHTML = `
            <div class="visual-card-workspace">
                <!-- 顶部快捷工具栏 -->
                <div class="visual-card-toolbar tech-section">
                    <div class="visual-toolbar-group">
                        <button type="button" class="btn-tech btn-small btn-primary" data-vc-action="ocr-import" title="上传或 Ctrl+V 粘贴卡图自动识别填充">
                            识图录卡
                        </button>
                        <button type="button" class="btn-tech btn-small" data-vc-action="ai-dsl" title="将卡面规则文本自动编译为可执行 DSL 能力">
                            编译并实装
                        </button>
                        <button type="button" class="btn-tech btn-small" data-vc-action="upload-art" title="上传或更换卡面插画">
                            更换插画
                        </button>
                        <button type="button" class="btn-tech btn-small" data-vc-action="reset-crop" title="重置插画缩放与位置">
                            重置裁切
                        </button>
                        <button type="button" class="btn-tech btn-small" data-vc-action="flip-art" title="水平翻转插画（避开右侧文本框遮挡角色面部）">水平翻转</button>
                    </div>
                    <div class="visual-toolbar-group">
                        <button type="button" class="btn-tech btn-small" data-vc-action="toggle-hotspots" aria-pressed="${String(this.showHotspots)}" title="显示/隐藏可点击编辑边框">
                            编辑热区
                        </button>
                        <button type="button" class="btn-tech btn-small" data-vc-action="export-png" title="导出 1269×715 高清 PNG 卡图">
                            导出 PNG
                        </button>
                        <button type="button" class="btn-tech btn-small" data-vc-action="switch-classic-form" title="切换到传统表单视图">
                            传统表单
                        </button>
                    </div>
                </div>

                <!-- 中央交互式 MSE 卡面画布（沉浸式无痕原位编辑 + 全息毛玻璃气泡） -->
                <div class="visual-canvas-viewport" id="vc-viewport">
                    <div class="visual-canvas-frame" id="vc-frame">
                        <div class="visual-canvas-stage" id="vc-stage">
                            <canvas id="vc-canvas" width="${WIDTH}" height="${HEIGHT}"></canvas>
                            <div class="vc-field-overlays" id="vc-overlays"></div>
                            <div class="vc-inline-layer" id="vc-inline-layer"></div>
                        </div>
                    </div>
                    <div class="vc-section-popover vc-glass-hud" id="vc-section-popover" hidden></div>
                    <div class="vc-floating-popover vc-glass-hud" id="vc-floating-popover" hidden></div>
                    <div class="vc-drop-hint" id="vc-drop-hint" hidden>松开鼠标以更换插画</div>
                </div>

                <div class="vc-status-row">
                    <span class="card-stage-badge" id="vc-stage-badge"></span>
                    <span class="vc-status-text" id="vc-status" role="status">点击卡面各区域直接无痕编辑 · 点击规则区管理文本区块 · 拖拽平移插画</span>
                    <span class="vc-overflow-warning" id="vc-overflow" hidden>规则超出显示区，请调小字号或精简描述</span>
                </div>

                <input type="file" id="vc-art-file" accept="image/png,image/jpeg,image/webp,image/gif" hidden>
                <input type="file" id="vc-ocr-file" accept="image/png,image/jpeg,image/webp" hidden>
            </div>
        `;

        if (this.settingsHost) {
            this.settingsHost.innerHTML = this.renderRightSettingsHtml(keywordOptions);
            const tribeDatalist = this.settingsHost.querySelector('#vc-tribe-suggestions');
            if (tribeDatalist && typeof this.cardEditor?.populateTribeSuggestionOptions === 'function') {
                void this.cardEditor.populateTribeSuggestionOptions(tribeDatalist);
            }
        }

        this.bindEvents();
        this.renderKeywordsList();

        const viewport = this.host.querySelector('#vc-viewport');
        if (viewport) {
            this.resizeObserver = new ResizeObserver(() => {
                this.fitCanvas();
                this.positionSectionPopover();
                this.positionFloatingPopover();
            });
            this.resizeObserver.observe(viewport);
        }

        this.fitCanvas();
        await this.paint();
    }

    setStatus(msg, isError = false) {
        const el = this.host?.querySelector('#vc-status');
        if (!el) return;
        el.textContent = msg;
        el.classList.toggle('is-error', isError);
    }

    fitCanvas() {
        const viewport = this.host?.querySelector('#vc-viewport');
        const frame = this.host?.querySelector('#vc-frame');
        const stage = this.host?.querySelector('#vc-stage');
        if (!viewport || !frame || !stage) return;

        const availW = Math.max(320, viewport.clientWidth - 24);
        const availH = Math.max(220, viewport.clientHeight - 16);
        this.scale = Math.max(0.25, Math.min(1.0, availW / WIDTH, availH / HEIGHT));
        frame.style.width = `${Math.round(WIDTH * this.scale)}px`;
        frame.style.height = `${Math.round(HEIGHT * this.scale)}px`;
        stage.style.transform = `scale(${this.scale})`;
    }

    renderKeywordsList() {
        const container = this.query('#vc-keywords-list');
        if (!container || !this.mseCard) return;
        container.innerHTML = this.cardEditor.renderTagList(this.mseCard.keywords || [], 'keywords');
    }

    syncSettingsControlsFromCard() {
        if (!this.mseCard) return;
        const setSelect = this.query('#vc-card-set');
        if (setSelect && !setSelect.disabled) {
            setSelect.innerHTML = this.cardSetOptionsHtml();
        }
        const tribeIn = this.query('#vc-tribe-input');
        if (tribeIn && document.activeElement !== tribeIn) {
            tribeIn.value = (this.mseCard.tribe || []).join('·');
        }
        const zoomSlider = this.query('#vc-art-zoom');
        const zoomVal = this.query('#vc-zoom-val');
        const zoom = this.mseCard.presentation.art?.crop?.zoom ?? 1;
        if (zoomSlider) zoomSlider.value = String(zoom);
        if (zoomVal) zoomVal.textContent = `${Math.round(zoom * 100)}%`;

        const langSel = this.query('#vc-lang');
        if (langSel) langSel.value = this.mseCard.presentation.language || 'zh-CN';

        const altArtChk = this.query('#vc-alt-art');
        if (altArtChk) altArtChk.checked = Boolean(this.mseCard.presentation.alternateArt);

        const autoFitChk = this.query('#vc-autofit-font');
        if (autoFitChk) autoFitChk.checked = this.mseCard.presentation.autoFitFontSize !== false;

        const fontSlider = this.query('#vc-fontsize');
        if (fontSlider) fontSlider.value = String(this.mseCard.presentation.fontSize || 16.8);

        const collectibleChk = this.query('#vc-collectible');
        if (collectibleChk) collectibleChk.checked = Boolean(this.mseCard.isCollectible);

        const implementedChk = this.query('#vc-implemented');
        if (implementedChk) implementedChk.checked = Boolean(this.mseCard.isImplemented);

        const countdownIn = this.query('#vc-countdown');
        if (countdownIn && document.activeElement !== countdownIn) {
            countdownIn.value = String(this.mseCard.countdown ?? 0);
        }

        this.renderKeywordsList();
    }

    renderHotspotHtml(field, nonEmptyCount) {
        const activeInlineKey = this.inlineEditor?.dataset?.key || '';
        const activePopoverKey = this.activeFloatingField || '';
        const sections = this.mseCard.presentation.sections;

        if (field.key.startsWith('style.')) {
            const idx = Number(field.key.split('.')[1]);
            const sec = sections[idx];
            const isActiveSec = activeInlineKey === `section.${idx}`;
            if (!sec?.text?.trim() && !isActiveSec) {
                return '';
            }
            return `
                <button
                    type="button"
                    class="vc-hotspot vc-hotspot-style-badge ${isActiveSec ? 'is-selected' : ''}"
                    data-vc-field="${field.key}"
                    title="区块 ${idx + 1}（${escapeHtml(sec.style)}）· 点击管理样式与顺序"
                    style="left:490px;top:${Math.max(148, field.y - 4)}px;width:28px;height:24px">
                    <span>${idx + 1}</span>
                </button>
            `;
        }

        if (field.key.startsWith('section.')) {
            const idx = Number(field.key.split('.')[1]);
            const sec = sections[idx];
            const hasText = Boolean(sec?.text?.trim());
            const isEditingThis = activeInlineKey === field.key;

            if (isEditingThis) {
                return '';
            }

            if (!hasText) {
                if (field.y >= 552) {
                    return '';
                }
                const slotHeight = idx === 0 && nonEmptyCount === 0
                    ? 72
                    : Math.min(44, Math.max(28, 568 - field.y));
                const hintLabel = idx === 0 && nonEmptyCount === 0
                    ? '+ 点击输入规则文本（支持最多 5 个文本区块）'
                    : `+ 新增文本区块 (${idx + 1}/${MAX_SECTIONS})`;
                return `
                    <button
                        type="button"
                        class="vc-hotspot vc-hotspot-section is-add-section"
                        data-vc-field="${field.key}"
                        title="${escapeHtml(hintLabel)}"
                        style="left:${field.x}px;top:${field.y}px;width:${field.width}px;height:${slotHeight}px">
                        <span class="vc-add-sec-label">${escapeHtml(hintLabel)}</span>
                    </button>
                `;
            }

            return `
                <button
                    type="button"
                    class="vc-hotspot vc-hotspot-section has-content ${field.key === this.selectedField ? 'is-selected' : ''}"
                    data-vc-field="${field.key}"
                    title="点击直接编辑区块 ${idx + 1}（${escapeHtml(sec.style)}）"
                    style="left:${field.x}px;top:${field.y}px;width:${field.width}px;height:${field.height}px">
                    <span class="vc-sec-chip">${idx + 1} · ${escapeHtml(sec.style)}</span>
                </button>
            `;
        }

        // 正在原位透明编辑文本或数字聚光时，隐藏底层虚线热区，避免双层框重叠
        if (activeInlineKey === field.key) {
            return '';
        }

        const isBadgeNumber = field.kind === 'number';
        const isActiveFloating = activePopoverKey === field.key;
        return `
            <button
                type="button"
                class="vc-hotspot ${field.kind === 'art' ? 'is-art' : ''} ${isBadgeNumber ? 'is-badge-hotspot' : ''} ${isActiveFloating ? 'is-selected' : ''}"
                data-vc-field="${field.key}"
                title="${escapeHtml(field.label)}"
                style="left:${field.x}px;top:${field.y}px;width:${field.width}px;height:${field.height}px">
            </button>
        `;
    }

    syncInlineControlGeometry() {
        if (!this.inlineEditor || !this.rendered) return;
        const key = this.inlineEditor.dataset.key;
        if (!key) return;

        const field = this.rendered.fields.find(f => f.key === key);
        if (!field) return;

        if (field.textStyle) {
            this.inlineEditor.style.fontFamily = field.textStyle.family;
            this.inlineEditor.style.fontSize = `${field.textStyle.size}px`;
        }

        if (key.startsWith('section.')) {
            const maxAvailableH = Math.max(76, 578 - field.y);
            const desiredH = Math.max(field.height + 14, 76);
            const finalH = Math.min(maxAvailableH, desiredH);
            // 水平留出 6px 透明呼吸内边距，使文字起点严格对齐 x=527、行宽严格等于 614px
            this.inlineEditor.style.left = `${field.x - 6}px`;
            this.inlineEditor.style.top = `${field.y + 1}px`;
            this.inlineEditor.style.width = `${field.width + 12}px`;
            this.inlineEditor.style.height = `${finalH}px`;
            return;
        }

        if (key === 'name') {
            this.inlineEditor.style.left = `${field.x}px`;
            this.inlineEditor.style.top = `${field.y}px`;
            this.inlineEditor.style.width = `${field.width}px`;
            this.inlineEditor.style.height = `${field.height}px`;
        }
    }

    renderSectionPopover() {
        const popover = this.host?.querySelector('#vc-section-popover');
        if (!popover) return;

        const key = this.inlineEditor?.dataset?.key || '';
        if (!key.startsWith('section.') || !this.mseCard) {
            popover.hidden = true;
            popover.innerHTML = '';
            return;
        }

        const idx = Number(key.split('.')[1]) || 0;
        const sections = this.mseCard.presentation.sections;
        const sec = sections[idx] || createEmptySection();
        const nonEmptyCount = this.getNonEmptySectionCount();
        const hasCurrentText = Boolean(sec.text.trim());
        const canMoveUp = idx > 0;
        const canMoveDown = idx + 1 < MAX_SECTIONS && Boolean(sections[idx + 1]?.text?.trim());
        const canAddNext = hasCurrentText && nonEmptyCount < MAX_SECTIONS;
        const isCrestOrFaith = ['纹章', '信仰'].includes(sec.style);

        const quickTags = this.mseCard.presentation.language === 'zh-TW'
            ? [
                ['入场曲', '入場曲'],
                ['谢幕曲', '謝幕曲'],
                ['进化时', '進化時'],
                ['超进化时', '超進化時']
            ]
            : [
                ['入场曲', '入场曲'],
                ['谢幕曲', '谢幕曲'],
                ['进化时', '进化时'],
                ['超进化时', '超进化时']
            ];

        popover.innerHTML = `
            <div class="vc-sec-pop-row">
                <span class="vc-sec-pop-badge">区块 ${idx + 1}/${MAX_SECTIONS}</span>
                <div class="vc-style-pills" role="group" aria-label="区块样式">
                    ${SECTION_STYLES.map(s => `
                        <button
                            type="button"
                            class="vc-hud-pill ${s === sec.style ? 'is-active' : ''}"
                            data-vc-sec-style-pill="${escapeHtml(s)}"
                            title="切换为「${escapeHtml(s)}」样式">
                            ${escapeHtml(SECTION_STYLE_SHORT_LABELS[s] || s)}
                        </button>
                    `).join('')}
                </div>
                ${isCrestOrFaith ? `
                    <button type="button" class="vc-hud-btn" data-vc-sec-action="icon" title="上传或更换纹章/信仰图标">图标</button>
                ` : ''}
            </div>
            <div class="vc-sec-pop-row vc-sec-pop-tags">
                ${quickTags.map(([keyTag, label]) => `
                    <button type="button" class="vc-hud-btn is-gold-tint" data-vc-insert-tag="${keyTag}">【${label}】</button>
                `).join('')}
                <button type="button" class="vc-hud-btn" data-vc-sec-action="emphasis" title="将选中文本包上【关键字】标金">【强调】</button>
                <button type="button" class="vc-hud-btn" data-vc-sec-action="separator" title="插入区块内部分隔线">分隔线</button>
                <span class="vc-sec-pop-divider" aria-hidden="true"></span>
                <button type="button" class="vc-hud-btn" data-vc-sec-action="up" ${canMoveUp ? '' : 'disabled'} title="上移此区块">↑</button>
                <button type="button" class="vc-hud-btn" data-vc-sec-action="down" ${canMoveDown ? '' : 'disabled'} title="下移此区块">↓</button>
                <button type="button" class="vc-hud-btn" data-vc-sec-action="add" ${canAddNext ? '' : 'disabled'} title="在下方新增一个文本区块">+ 新区块</button>
                <button type="button" class="vc-hud-btn is-danger" data-vc-sec-action="delete" title="删除或清空此文本区块">删除</button>
                <button type="button" class="vc-hud-btn is-done" data-vc-sec-action="done" title="完成当前区块编辑 (Esc)">完成</button>
            </div>
        `;
        popover.hidden = false;
        this.positionSectionPopover();
    }

    positionPopoverNearRect(popover, fieldRect, preferAbove = true) {
        const viewport = this.host?.querySelector('#vc-viewport');
        const frame = this.host?.querySelector('#vc-frame');
        if (!popover || popover.hidden || !viewport || !frame || !fieldRect) return;

        const rectLeft = frame.offsetLeft + fieldRect.x * this.scale;
        const rectTop = frame.offsetTop + fieldRect.y * this.scale;
        const rectWidth = fieldRect.width * this.scale;
        const rectBottom = rectTop + fieldRect.height * this.scale;

        const popW = popover.offsetWidth || 260;
        const popH = popover.offsetHeight || 64;

        let top;
        if (preferAbove) {
            top = rectTop - popH - 10;
            if (top < 8) {
                top = Math.min(Math.max(8, viewport.clientHeight - popH - 8), rectBottom + 10);
            }
        } else {
            top = rectBottom + 10;
            if (top + popH > viewport.clientHeight - 8) {
                top = Math.max(8, rectTop - popH - 10);
            }
        }

        const anchorCenterX = rectLeft + rectWidth / 2;
        const alignedLeft = fieldRect.width > 300 ? rectLeft : (anchorCenterX - popW / 2);
        const left = Math.max(8, Math.min(alignedLeft, viewport.clientWidth - popW - 8));

        popover.style.left = `${Math.round(left)}px`;
        popover.style.top = `${Math.round(top)}px`;
    }

    positionSectionPopover() {
        const popover = this.host?.querySelector('#vc-section-popover');
        if (!popover || popover.hidden || !this.inlineEditor) return;
        const x = parseFloat(this.inlineEditor.style.left) || 521;
        const y = parseFloat(this.inlineEditor.style.top) || 180;
        const width = parseFloat(this.inlineEditor.style.width) || 626;
        const height = parseFloat(this.inlineEditor.style.height) || 80;
        this.positionPopoverNearRect(popover, { x, y, width, height }, true);
    }

    renderFloatingPopover() {
        const popover = this.host?.querySelector('#vc-floating-popover');
        if (!popover) return;

        const key = this.activeFloatingField;
        if (!key || !this.mseCard || !this.rendered) {
            popover.hidden = true;
            popover.innerHTML = '';
            return;
        }

        const field = this.rendered.fields.find(f => f.key === key);
        if (!field) {
            popover.hidden = true;
            return;
        }

        // 数字微调胶囊 HUD（配合徽章聚光环）
        if (field.kind === 'number') {
            const val = Number(this.getCardField(key)) || 0;
            popover.innerHTML = `
                <div class="vc-num-stepper-hud">
                    <span class="vc-float-kicker">${escapeHtml(field.label)}</span>
                    <button type="button" class="vc-hud-step-btn" data-vc-num-step="-1" title="减少 1">−</button>
                    <span class="vc-hud-num-val" id="vc-hud-num-display">${val}</span>
                    <button type="button" class="vc-hud-step-btn" data-vc-num-step="1" title="增加 1">＋</button>
                </div>
            `;
            popover.hidden = false;
            this.positionFloatingPopover();
            return;
        }

        // 枚举选择悬浮全息面板
        const choices = this.selectChoicesFor(key);
        if (!choices) {
            popover.hidden = true;
            return;
        }
        const currentVal = String(this.getCardField(key));
        const title = FIELD_PICKER_TITLES[key] || field.label;

        if (key === 'class') {
            popover.innerHTML = `
                <div class="vc-float-header">
                    <span class="vc-float-kicker">${escapeHtml(title)}</span>
                </div>
                <div class="vc-picker-grid vc-picker-class-grid">
                    ${Object.entries(choices).map(([val, label]) => `
                        <button
                            type="button"
                            class="vc-picker-option ${val === currentVal ? 'is-active' : ''}"
                            data-vc-pick-field="${key}"
                            data-vc-pick-val="${escapeHtml(val)}">
                            <img class="vc-picker-emblem" src="/mse-beyond/Emblem_${escapeHtml(val)}.png" alt="">
                            <span>${escapeHtml(label)}</span>
                        </button>
                    `).join('')}
                </div>
            `;
        } else if (key === 'rarity') {
            popover.innerHTML = `
                <div class="vc-float-header">
                    <span class="vc-float-kicker">${escapeHtml(title)}</span>
                </div>
                <div class="vc-picker-row">
                    ${Object.entries(choices).map(([val, label]) => `
                        <button
                            type="button"
                            class="vc-picker-option ${val === currentVal ? 'is-active' : ''}"
                            data-vc-pick-field="${key}"
                            data-vc-pick-val="${escapeHtml(val)}">
                            <span class="vc-rarity-gem-dot is-${escapeHtml(val.toLowerCase())}"></span>
                            <span>${escapeHtml(label)}</span>
                        </button>
                    `).join('')}
                </div>
            `;
        } else if (key === 'cardSet') {
            const isMirage = Boolean(this.cardEditor?.isMirageScope?.());
            popover.innerHTML = `
                <div class="vc-float-header">
                    <span class="vc-float-kicker">${escapeHtml(title)}</span>
                    ${isMirage ? '' : '<button type="button" class="vc-hud-btn" data-vc-action="open-pack-manager">卡包管理</button>'}
                </div>
                ${isMirage ? `
                    <div class="vc-picker-locked-note">幻境卡牌固定归属「${escapeHtml(this.mseCard.cardSet)}」</div>
                ` : `
                    <div class="vc-picker-list custom-scrollbar">
                        ${Object.entries(choices).map(([val]) => {
                            const logo = this.resolvePackLogoUrl(val);
                            return `
                                <button
                                    type="button"
                                    class="vc-picker-list-item ${val === currentVal ? 'is-active' : ''}"
                                    data-vc-pick-field="${key}"
                                    data-vc-pick-val="${escapeHtml(val)}">
                                    <span class="vc-picker-pack-name">${escapeHtml(val)}</span>
                                    ${logo ? `<img class="vc-picker-pack-logo" src="${escapeHtml(logo)}" alt="">` : '<span class="vc-picker-pack-nolgo">无Logo</span>'}
                                </button>
                            `;
                        }).join('')}
                    </div>
                `}
            `;
        } else {
            popover.innerHTML = `
                <div class="vc-float-header">
                    <span class="vc-float-kicker">${escapeHtml(title)}</span>
                </div>
                <div class="vc-picker-row">
                    ${Object.entries(choices).map(([val, label]) => `
                        <button
                            type="button"
                            class="vc-picker-option ${val === currentVal ? 'is-active' : ''}"
                            data-vc-pick-field="${key}"
                            data-vc-pick-val="${escapeHtml(val)}">
                            <span>${escapeHtml(label)}</span>
                        </button>
                    `).join('')}
                </div>
            `;
        }

        popover.hidden = false;
        this.positionFloatingPopover();
    }

    positionFloatingPopover() {
        const popover = this.host?.querySelector('#vc-floating-popover');
        if (!popover || popover.hidden || !this.activeFloatingField || !this.rendered) return;
        const field = this.rendered.fields.find(f => f.key === this.activeFloatingField);
        if (!field) return;

        // 底部宝石/攻防优先向上弹出，顶部职业/卡包/费用优先向下弹出，侧边卡牌类型锚定在其中段旁
        const anchorRect = field.key === 'type'
            ? { x: field.x, y: field.y + 85, width: field.width, height: 80 }
            : field;
        const preferAbove = ['rarity', 'attack', 'health'].includes(field.key);
        this.positionPopoverNearRect(popover, anchorRect, preferAbove);
    }

    async paint() {
        if (!this.host || !this.mseCard) return;
        const generation = ++this.renderGeneration;
        try {
            const snapshot = structuredClone(this.mseCard);
            const omitTextField = this.getActiveOmitTextField();
            const result = await renderCard(snapshot, { omitTextField });
            if (generation !== this.renderGeneration) return;
            this.rendered = result;

            const fontSizeLabel = this.query('#vc-fontsize-val');
            if (fontSizeLabel) {
                const requested = this.mseCard.presentation.fontSize;
                fontSizeLabel.textContent = result.effectiveFontSize < requested
                    ? `${requested} → ${result.effectiveFontSize}`
                    : String(requested);
            }
            const stageBadge = this.host.querySelector('#vc-stage-badge');
            if (stageBadge) {
                stageBadge.textContent = this.mseCard.isImplemented ? '已实装 · 对战就绪' : '待实装 · 卡面阶段';
                stageBadge.classList.toggle('is-unimplemented', !this.mseCard.isImplemented);
            }
            const implementedInput = this.query('#vc-implemented');
            if (implementedInput) implementedInput.checked = this.mseCard.isImplemented;

            const canvas = this.host.querySelector('#vc-canvas');
            if (canvas) {
                const ctx = canvas.getContext('2d');
                ctx.clearRect(0, 0, WIDTH, HEIGHT);
                ctx.drawImage(result.canvas, 0, 0);
            }

            const nonEmptyCount = this.getNonEmptySectionCount();
            const overlays = this.host.querySelector('#vc-overlays');
            if (overlays) {
                overlays.innerHTML = result.fields
                    .map(field => this.renderHotspotHtml(field, nonEmptyCount))
                    .join('');
                overlays.classList.toggle('hide-hotspots', !this.showHotspots);
            }

            if (this.inlineEditor) {
                this.syncInlineControlGeometry();
                this.positionSectionPopover();
            }
            if (this.activeFloatingField) {
                this.positionFloatingPopover();
            }

            const overflowEl = this.host.querySelector('#vc-overflow');
            if (overflowEl) {
                overflowEl.hidden = !result.overflow;
            }
        } catch (err) {
            this.setStatus(`卡面渲染异常：${err.message}`, true);
        }
    }

    finishInline() {
        const hadInline = Boolean(this.inlineEditor);
        const hadFloating = Boolean(this.activeFloatingField);

        if (!hadInline && !hadFloating) {
            return true;
        }

        const wasSection = Boolean(this.inlineEditor?.dataset?.key?.startsWith('section.'));
        if (this.inlineEditor) {
            const ctrl = this.inlineEditor;
            this.inlineEditor = null;
            ctrl.remove();
        }
        this.activeFloatingField = null;
        this.selectedField = '';

        const secPop = this.host?.querySelector('#vc-section-popover');
        if (secPop) {
            secPop.hidden = true;
            secPop.innerHTML = '';
        }
        const floatPop = this.host?.querySelector('#vc-floating-popover');
        if (floatPop) {
            floatPop.hidden = true;
            floatPop.innerHTML = '';
        }

        if (wasSection) {
            this.compactEmptySections(-1);
        }

        if (hadInline) {
            this.syncToEditorCard();
        }
        void this.paint();
        return true;
    }

    setCardField(key, value) {
        if (!this.mseCard) return;
        if (key === 'title-name') key = 'name';
        const [kind, idxStr] = key.split('.');
        const idx = Number(idxStr);

        if (kind === 'section' && Number.isInteger(idx)) {
            this.mseCard.presentation.sections[idx].text = value;
            this.activeSectionIndex = idx;
        } else if (kind === 'style' && Number.isInteger(idx)) {
            this.mseCard.presentation.sections[idx].style = value;
            this.activeSectionIndex = idx;
        } else if (kind === 'icon' && Number.isInteger(idx)) {
            this.mseCard.presentation.sections[idx].icon = value;
        } else if (key === 'artist') {
            this.mseCard.presentation.artist = value;
        } else if (key === 'tribe') {
            this.mseCard.tribe = String(value).split(/[•·,，、\s]+/).map(s => s.trim()).filter(Boolean);
            const tribeIn = this.query('#vc-tribe-input');
            if (tribeIn && document.activeElement !== tribeIn) {
                tribeIn.value = this.mseCard.tribe.join('·');
            }
        } else if (key === 'cardSet') {
            this.mseCard.cardSet = String(value).trim() || '默认';
            this.mseCard.presentation.cardPackLogo = this.resolvePackLogoUrl(this.mseCard.cardSet);
            const setIn = this.query('#vc-card-set');
            if (setIn && document.activeElement !== setIn && !setIn.disabled) {
                setIn.value = this.mseCard.cardSet;
            }
        } else if (key === 'type') {
            this.mseCard.type = value;
            if (value === 'Follower') {
                this.mseCard.attack ??= 0;
                this.mseCard.health ??= 1;
            } else if (value === 'Leader') {
                this.mseCard.attack = 0;
                if (!this.mseCard.health || this.mseCard.health <= 1) {
                    this.mseCard.health = 20;
                }
            }
        } else if (['cost', 'attack', 'health', 'countdown'].includes(key)) {
            const minVal = key === 'health' && (this.mseCard.type === 'Follower' || this.mseCard.type === 'Leader') ? 1 : 0;
            const parsed = parseInt(value, 10);
            const num = Math.max(minVal, Math.min(99, Number.isFinite(parsed) ? parsed : minVal));
            this.mseCard[key] = num;
            const hudNum = this.host?.querySelector('#vc-hud-num-display');
            if (hudNum && this.activeFloatingField === key) {
                hudNum.textContent = String(num);
            }
        } else {
            this.mseCard[key] = value;
        }

        if (kind === 'section' || kind === 'style') {
            this.notifyRulesEdited();
            if (this.inlineEditor?.dataset?.key?.startsWith('section.')) {
                this.renderSectionPopover();
            }
        }
        this.syncToEditorCard();
        void this.paint();
    }

    notifyRulesEdited() {
        if (this.mseCard.isImplemented) {
            this.setStatus('规则文本已修改，请点击「编译并实装」更新对战能力（或取消勾选「已实装」存为卡面阶段）');
        }
    }

    resolvePackLogoUrl(cardSetName) {
        const name = String(cardSetName ?? '').trim().toLowerCase();
        const definition = this.cardEditor.cardSetDefinitions.find(set => String(set.name ?? set.Name).trim().toLowerCase() === name);
        return definition?.logo_url ?? definition?.logoUrl ?? '';
    }

    cardSetOptionsHtml() {
        return Object.entries(this.selectChoicesFor('cardSet')).map(([name, label]) =>
            `<option value="${escapeHtml(name)}" ${name === this.mseCard.cardSet ? 'selected' : ''}>${escapeHtml(label)}</option>`
        ).join('');
    }

    async refreshCardSets() {
        if (!this.mseCard) return;
        this.mseCard.presentation.cardPackLogo = this.resolvePackLogoUrl(this.mseCard.cardSet);
        const select = this.query('#vc-card-set');
        if (select && !select.disabled) select.innerHTML = this.cardSetOptionsHtml();
        if (this.activeFloatingField === 'cardSet') {
            this.renderFloatingPopover();
        }
        await this.paint();
    }

    getCardField(key) {
        if (!this.mseCard) return '';
        if (key === 'title-name') key = 'name';
        const [kind, idxStr] = key.split('.');
        const idx = Number(idxStr);
        if (kind === 'section') return this.mseCard.presentation.sections[idx]?.text ?? '';
        if (kind === 'style') return this.mseCard.presentation.sections[idx]?.style ?? '普通';
        if (key === 'artist') return this.mseCard.presentation.artist ?? '';
        if (key === 'tribe') return (this.mseCard.tribe || []).join('·');
        return this.mseCard[key] ?? '';
    }

    selectChoicesFor(key) {
        if (key === 'cardSet') {
            const choices = Object.fromEntries(this.cardEditor.cardSetDefinitions.map(set => {
                const name = String(set.name ?? set.Name);
                return [name, (set.logo_url ?? set.logoUrl) ? `${name} (已配Logo)` : name];
            }));
            choices[this.mseCard.cardSet] ??= this.mseCard.cardSet;
            return choices;
        }
        if (key.startsWith('style.')) {
            return Object.fromEntries(SECTION_STYLES.map(x => [x, x]));
        }
        if (key === 'type') {
            if (Boolean(this.cardEditor?.isMirageScope?.()) || this.mseCard?.type === 'Leader') {
                return TYPES;
            }
            return Object.fromEntries(Object.entries(TYPES).filter(([k]) => k !== 'Leader'));
        }
        return {
            class: CLASSES,
            rarity: RARITIES
        }[key] || null;
    }

    async openInlineFieldEditor(key) {
        // 再次点击同一个已展开的枚举面板时直接收起
        if (this.activeFloatingField === key && !this.inlineEditor) {
            this.finishInline();
            return;
        }

        if (!this.finishInline() || !this.rendered) return;

        // 点击段落左侧序号徽章时，直接打开该段落的原位透明编辑与全息工具条
        if (key.startsWith('style.')) {
            const secIdx = Number(key.split('.')[1]) || 0;
            key = `section.${secIdx}`;
        }

        const field = this.rendered.fields.find(f => f.key === key);
        if (!field) return;

        if (field.kind === 'art') {
            this.setStatus('拖拽平移插画 · Ctrl+滚轮缩放 · 双击或拖入图片更换插画');
            return;
        }
        if (field.kind === 'image') {
            this.triggerArtFilePicker(key);
            return;
        }

        this.selectedField = key;

        // 枚举选择字段（职业 / 稀有度 / 类型 / 卡包）：弹出星云毛玻璃胶囊面板，不在画布叠黑框
        const choices = this.selectChoicesFor(key);
        if (choices) {
            this.activeFloatingField = key;
            this.renderFloatingPopover();
            await this.paint();
            return;
        }

        const isSection = key.startsWith('section.');
        const isNumber = field.kind === 'number';
        if (isSection) {
            this.activeSectionIndex = Number(key.split('.')[1]) || 0;
        }

        const control = document.createElement(field.kind === 'multiline' ? 'textarea' : 'input');
        control.className = [
            'vc-inline-editor',
            `vc-inline-${field.kind}`,
            isSection ? 'vc-inline-section' : '',
            isNumber ? 'vc-inline-number-halo' : 'vc-inline-seamless'
        ].filter(Boolean).join(' ');
        control.dataset.key = key;
        control.setAttribute('aria-label', field.label);

        if (isSection) {
            control.placeholder = '输入此区块规则描述（【关键字】自动标金，== 插入内部分隔线）…';
        } else if (key === 'tribe') {
            control.placeholder = '输入种类（· 分隔）';
        } else if (key === 'artist') {
            control.placeholder = '输入画师署名…';
        } else if (key === 'name' || key === 'title-name') {
            control.placeholder = '输入卡牌名称';
        }

        control.style.cssText = `left:${field.x}px;top:${field.y}px;width:${Math.max(field.width, 64)}px;height:${Math.max(field.height, field.kind === 'multiline' ? 76 : 32)}px;`;

        if (isNumber) {
            control.type = 'text';
            control.inputMode = 'numeric';
            control.autocomplete = 'off';
        }

        if (field.textStyle) {
            control.style.fontFamily = field.textStyle.family;
            control.style.fontSize = `${field.textStyle.size}px`;
            if (!isNumber) {
                control.style.textAlign = field.textStyle.align;
            }
        }

        control.value = String(this.getCardField(key) ?? '');
        this.inlineEditor = control;
        this.host.querySelector('#vc-inline-layer')?.append(control);

        if (isSection) {
            this.syncInlineControlGeometry();
            this.renderSectionPopover();
        } else if (isNumber) {
            this.activeFloatingField = key;
            this.renderFloatingPopover();
        }

        // 触发一次带 omitTextField 的重绘，隐去底层静态字以呈现 100% 透明无重影的卡面肌理
        await this.paint();

        control.addEventListener('input', () => {
            const currentKey = control.dataset.key;
            if (isNumber) {
                const digits = control.value.replace(/\D+/g, '').slice(0, 2);
                if (control.value !== digits) {
                    control.value = digits;
                }
                this.setCardField(currentKey, digits === '' ? '0' : digits);
                return;
            }
            this.setCardField(currentKey, control.value);
        });

        control.addEventListener('keydown', e => {
            if (e.key === 'Escape') {
                e.preventDefault();
                this.finishInline();
                return;
            }
            if (e.key === 'Enter' && field.kind !== 'multiline') {
                e.preventDefault();
                this.finishInline();
                return;
            }
            if (isNumber && (e.key === 'ArrowUp' || e.key === 'ArrowDown')) {
                e.preventDefault();
                const currentKey = control.dataset.key;
                const cur = Number(this.getCardField(currentKey)) || 0;
                const next = cur + (e.key === 'ArrowUp' ? 1 : -1);
                this.setCardField(currentKey, next);
                control.value = String(this.getCardField(currentKey));
                control.select();
            }
        });

        if (isNumber) {
            control.addEventListener('wheel', e => {
                e.preventDefault();
                const currentKey = control.dataset.key;
                const cur = Number(this.getCardField(currentKey)) || 0;
                const next = cur + (e.deltaY < 0 ? 1 : -1);
                this.setCardField(currentKey, next);
                control.value = String(this.getCardField(currentKey));
                control.select();
            }, { passive: false });
        }

        control.addEventListener('blur', () => {
            if (this.inlineEditor !== control) return;
            if (!document.hasFocus()) return;
            this.finishInline();
        });

        control.focus();
        if (control.select && !isSection) {
            control.select();
        }
    }

    async handleSectionPopoverAction(action) {
        if (!this.mseCard) return;
        const sections = this.mseCard.presentation.sections;
        const idx = this.activeSectionIndex;

        if (action === 'done') {
            this.finishInline();
            return;
        }

        if (action === 'icon') {
            this.triggerArtFilePicker(`icon.${idx}`);
            return;
        }

        if (action === 'emphasis' || action === 'separator') {
            if (this.inlineEditor?.tagName === 'TEXTAREA') {
                const textArea = this.inlineEditor;
                const start = textArea.selectionStart ?? textArea.value.length;
                const end = textArea.selectionEnd ?? textArea.value.length;
                const insert = action === 'emphasis'
                    ? `【${textArea.value.slice(start, end) || '关键字'}】`
                    : '==';
                textArea.setRangeText(insert, start, end, 'end');
                textArea.dispatchEvent(new Event('input', { bubbles: true }));
                textArea.focus();
            }
            return;
        }

        if (action === 'up' || action === 'down') {
            const nextIdx = idx + (action === 'up' ? -1 : 1);
            if (nextIdx < 0 || nextIdx >= MAX_SECTIONS) return;
            if (action === 'down' && !sections[nextIdx]?.text?.trim()) return;

            [sections[idx], sections[nextIdx]] = [sections[nextIdx], sections[idx]];
            this.activeSectionIndex = nextIdx;
            this.selectedField = `section.${nextIdx}`;
            if (this.inlineEditor) {
                this.inlineEditor.dataset.key = `section.${nextIdx}`;
            }
            this.notifyRulesEdited();
            this.syncToEditorCard();
            await this.paint();
            this.renderSectionPopover();
            this.inlineEditor?.focus();
            return;
        }

        if (action === 'add') {
            this.compactEmptySections(idx);
            const nextEmptyIdx = this.mseCard.presentation.sections.findIndex(s => !s.text.trim());
            if (nextEmptyIdx === -1) return;
            await this.openInlineFieldEditor(`section.${nextEmptyIdx}`);
            return;
        }

        if (action === 'delete') {
            sections.splice(idx, 1);
            sections.push(createEmptySection());
            this.compactEmptySections(-1);
            this.notifyRulesEdited();
            this.finishInline();
        }
    }

    triggerArtFilePicker(targetKey = 'art') {
        this._uploadTargetKey = targetKey;
        const input = this.host?.querySelector('#vc-art-file');
        if (input) {
            input.value = '';
            input.click();
        }
    }

    async loadArtFile(file, targetKey = 'art') {
        if (!file || !file.type.startsWith('image/')) {
            this.setStatus('请选择 PNG / JPEG / WebP / GIF 格式的图片', true);
            return;
        }
        const dataUrl = await new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => reject(reader.error);
            reader.readAsDataURL(file);
        });
        await loadImage(dataUrl);

        if (targetKey === 'art') {
            this.mseCard.presentation.art = {
                src: dataUrl,
                file,
                crop: { zoom: 1, x: 0.5, y: 0.5, flipX: false }
            };
            this.artDirty = true;
            const zoomSlider = this.query('#vc-art-zoom');
            const zoomVal = this.query('#vc-zoom-val');
            if (zoomSlider) zoomSlider.value = '1';
            if (zoomVal) zoomVal.textContent = '100%';
            this.setStatus('插画已载入 · 拖拽平移插画 · Ctrl+滚轮缩放 · 保存时上传插画与卡面');
        } else if (targetKey.startsWith('icon.')) {
            this.setCardField(targetKey, dataUrl);
        }
        await this.paint();
    }

    moveCrop(crop, initial, image, dx, dy) {
        const effDx = crop.flipX ? -dx : dx;
        const factor = Math.max(293 / image.width, 363 / image.height) * crop.zoom;
        const extraX = image.width * factor - 293;
        const extraY = image.height * factor - 363;
        crop.x = extraX > 0.01 ? Math.max(0, Math.min(1, initial.x - effDx / extraX)) : 0.5;
        crop.y = extraY > 0.01 ? Math.max(0, Math.min(1, initial.y - dy / extraY)) : 0.5;
    }

    /**
     * 导出裁切后的插画预览图 Blob (用于上传 /api/cards/{id}/art/preview)
     */
    async exportCroppedPreviewBlob() {
        const art = this.mseCard?.presentation?.art;
        if (!art?.src) return null;
        const image = await loadImage(art.src);
        const canvas = document.createElement('canvas');
        canvas.width = 586;
        canvas.height = 726;
        const ctx = canvas.getContext('2d');
        drawCroppedArt(ctx, image, art.crop, { x: 0, y: 0, width: 586, height: 726 });
        return new Promise(resolve => canvas.toBlob(resolve, 'image/webp', 0.92));
    }

    /**
     * 导出完整 1269×715 渲染卡面 Blob (用于上传 /api/cards/{id}/art/detail 或下载)
     */
    async exportRenderedCardBlob(mimeType = 'image/webp') {
        if (!this.mseCard) return null;
        const result = await renderCard(structuredClone(this.mseCard));
        return new Promise(resolve => result.canvas.toBlob(resolve, mimeType, 0.92));
    }

    /**
     * 处理 OCR 识别图像并直接灌入当前可视化卡面
     */
    async handleOcrImportFile(file) {
        if (!file || !file.type.startsWith('image/')) {
            this.setStatus('请选择有效的卡牌图片进行 OCR 识别', true);
            return;
        }

        this.setStatus('正在识别卡面文字、数值并提取插画…');
        try {
            const formData = new FormData();
            formData.append('file', file);

            const response = await fetch('/api/cards/ocr-import', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                const errText = await response.text();
                throw new Error(errText || `HTTP ${response.status}`);
            }

            const ocrData = await response.json();
            this.applyOcrResultToCard(ocrData);
            await this.paint();
            this.setStatus(`OCR 识别完成（${ocrData.recognitionSeconds ?? '-'}s），请核对数值与文本后保存。`);
        } catch (error) {
            console.error('OCR import failed:', error);
            this.setStatus(`OCR 识别失败：${error.message}`, true);
        }
    }

    applyOcrResultToCard(ocrData) {
        if (!ocrData || !this.mseCard) return;

        if (ocrData.name) this.mseCard.name = ocrData.name;
        if (ocrData.class && ocrData.class in CLASSES) this.mseCard.class = ocrData.class;
        if (ocrData.type && ocrData.type in TYPES) this.mseCard.type = ocrData.type;
        if (ocrData.rarity && ocrData.rarity in RARITIES) this.mseCard.rarity = ocrData.rarity;
        if (Number.isInteger(ocrData.cost)) this.mseCard.cost = ocrData.cost;
        if (Number.isInteger(ocrData.attack)) this.mseCard.attack = ocrData.attack;
        if (Number.isInteger(ocrData.health)) this.mseCard.health = Math.max(0, ocrData.health);
        if (Array.isArray(ocrData.tribe) && ocrData.tribe.length > 0) {
            this.mseCard.tribe = ocrData.tribe;
        }
        if (ocrData.artist) {
            this.mseCard.presentation.artist = ocrData.artist;
        }
        if (Array.isArray(ocrData.sections) && ocrData.sections.length > 0) {
            this.mseCard.presentation.sections = Array.from({ length: MAX_SECTIONS }, (_, i) => ({
                text: String(ocrData.sections[i]?.text ?? ''),
                style: SECTION_STYLES.includes(ocrData.sections[i]?.style) ? ocrData.sections[i].style : '普通',
                icon: String(ocrData.sections[i]?.icon ?? '')
            }));
        } else if (ocrData.rulesText) {
            this.mseCard.presentation.sections = sectionsFromDescription(ocrData.rulesText);
        }
        this.compactEmptySections(-1);

        if (ocrData.croppedArtDataUrl) {
            this.mseCard.presentation.art = {
                src: ocrData.croppedArtDataUrl,
                crop: { zoom: 1, x: 0.5, y: 0.5, flipX: false }
            };
            this.artDirty = true;
        }

        this.syncSettingsControlsFromCard();
        this.syncToEditorCard();
    }

    bindEvents() {
        if (!this.host) return;
        const signal = this.mountAbortController?.signal;
        const listen = (target, type, handler, opts = {}) => {
            target?.addEventListener(type, handler, { ...opts, signal });
        };

        // Escape 关闭悬浮选择面板；点击画布外部任意区域自动收起悬浮气泡
        listen(document, 'keydown', e => {
            if (e.key === 'Escape' && this.activeFloatingField && !this.inlineEditor) {
                e.preventDefault();
                this.finishInline();
            }
        });
        listen(document, 'pointerdown', e => {
            if (this.activeFloatingField && !this.inlineEditor && this.host && !this.host.contains(e.target)) {
                this.finishInline();
            }
        });

        // 点击悬浮全息面板内部时保持原位编辑框焦点与选区；点击画布空白处收起
        listen(this.host, 'pointerdown', e => {
            if (e.target.closest('.vc-section-popover, .vc-floating-popover')) {
                e.preventDefault();
                return;
            }
            if ((this.inlineEditor || this.activeFloatingField) && !e.target.closest('.vc-inline-editor, [data-vc-field]')) {
                this.finishInline();
            }
        });

        // 画布、全息悬浮条与工具栏点击
        listen(this.host, 'click', async e => {
            const pickBtn = e.target.closest('[data-vc-pick-field]');
            if (pickBtn) {
                const fieldKey = pickBtn.dataset.vcPickField;
                const val = pickBtn.dataset.vcPickVal;
                this.setCardField(fieldKey, val);
                this.finishInline();
                return;
            }

            const numStepBtn = e.target.closest('[data-vc-num-step]');
            if (numStepBtn && this.activeFloatingField) {
                const delta = Number(numStepBtn.dataset.vcNumStep) || 0;
                const key = this.activeFloatingField;
                const cur = Number(this.getCardField(key)) || 0;
                this.setCardField(key, cur + delta);
                if (this.inlineEditor) {
                    this.inlineEditor.value = String(this.getCardField(key));
                    this.inlineEditor.focus();
                    this.inlineEditor.select();
                }
                return;
            }

            const stylePill = e.target.closest('[data-vc-sec-style-pill]');
            if (stylePill) {
                this.setCardField(`style.${this.activeSectionIndex}`, stylePill.dataset.vcSecStylePill);
                await this.paint();
                this.renderSectionPopover();
                this.inlineEditor?.focus();
                return;
            }

            const tagButton = e.target.closest('[data-vc-insert-tag]');
            if (tagButton) {
                if (this.inlineEditor?.tagName === 'TEXTAREA') {
                    const textArea = this.inlineEditor;
                    const simplified = tagButton.dataset.vcInsertTag;
                    const traditional = { '入场曲': '入場曲', '谢幕曲': '謝幕曲', '进化时': '進化時', '超进化时': '超進化時' };
                    const tag = this.mseCard.presentation.language === 'zh-TW' ? traditional[simplified] : simplified;
                    textArea.setRangeText(`【${tag}】`, textArea.selectionStart, textArea.selectionEnd, 'end');
                    textArea.dispatchEvent(new Event('input', { bubbles: true }));
                    textArea.focus();
                }
                return;
            }

            const secActionBtn = e.target.closest('[data-vc-sec-action]');
            if (secActionBtn) {
                await this.handleSectionPopoverAction(secActionBtn.dataset.vcSecAction);
                return;
            }

            const actionBtn = e.target.closest('[data-vc-action]');
            if (actionBtn) {
                const action = actionBtn.dataset.vcAction;
                if (action === 'open-pack-manager') {
                    this.finishInline();
                    await openCardSetManagerModal({ highlightSetName: this.mseCard.cardSet });
                } else if (action === 'flip-art') {
                    const crop = this.mseCard.presentation.art.crop;
                    crop.flipX = !crop.flipX;
                    this.artDirty = true;
                    this.syncToEditorCard();
                    await this.paint();
                } else if (action === 'upload-art') {
                    this.triggerArtFilePicker('art');
                } else if (action === 'reset-crop') {
                    this.mseCard.presentation.art.crop = { zoom: 1, x: 0.5, y: 0.5, flipX: false };
                    this.artDirty = true;
                    const zoomSlider = this.query('#vc-art-zoom');
                    const zoomVal = this.query('#vc-zoom-val');
                    if (zoomSlider) zoomSlider.value = '1';
                    if (zoomVal) zoomVal.textContent = '100%';
                    this.syncToEditorCard();
                    void this.paint();
                } else if (action === 'toggle-hotspots') {
                    this.showHotspots = !this.showHotspots;
                    actionBtn.setAttribute('aria-pressed', String(this.showHotspots));
                    this.host.querySelector('#vc-overlays')?.classList.toggle('hide-hotspots', !this.showHotspots);
                } else if (action === 'export-png') {
                    this.finishInline();
                    const blob = await this.exportRenderedCardBlob('image/png');
                    if (blob) {
                        const url = URL.createObjectURL(blob);
                        const a = document.createElement('a');
                        a.href = url;
                        a.download = `${(this.mseCard.name || 'card').replace(/[\\/:*?"<>|]/g, '_')}.png`;
                        a.click();
                        setTimeout(() => URL.revokeObjectURL(url), 1000);
                        this.setStatus('已导出 1269 × 715 PNG 卡图。');
                    }
                } else if (action === 'ocr-import') {
                    this.finishInline();
                    const ocrInput = this.host.querySelector('#vc-ocr-file');
                    if (ocrInput) {
                        ocrInput.value = '';
                        ocrInput.click();
                    }
                } else if (action === 'ai-dsl') {
                    await this.cardEditor.generateAbilitiesFromVisualCard?.();
                } else if (action === 'switch-classic-form') {
                    this.finishInline();
                    this.cardEditor.useClassicBasicForm = true;
                    await this.cardEditor.renderBasicInfoForm();
                }
                return;
            }

            const hotspot = e.target.closest('[data-vc-field]');
            if (hotspot && !this._wasDraggingArt) {
                await this.openInlineFieldEditor(hotspot.dataset.vcField);
            }
        });

        // 右侧设置面板事件联动
        if (this.settingsHost) {
            listen(this.settingsHost, 'pointerdown', () => {
                if (this.inlineEditor || this.activeFloatingField) {
                    this.finishInline();
                }
            });

            listen(this.settingsHost, 'click', async e => {
                const packBtn = e.target.closest('[data-vc-action="open-pack-manager"]');
                if (packBtn) {
                    await openCardSetManagerModal({ highlightSetName: this.mseCard.cardSet });
                    return;
                }
                if (e.target.classList.contains('tag-remove-btn')) {
                    const tag = e.target.dataset.tag;
                    this.mseCard.keywords = (this.mseCard.keywords || []).filter(k => k !== tag);
                    this.syncToEditorCard();
                    this.renderKeywordsList();
                }
            });

            const cardSetIn = this.settingsHost.querySelector('#vc-card-set');
            listen(cardSetIn, 'change', () => {
                this.setCardField('cardSet', cardSetIn.value);
            });

            const tribeIn = this.settingsHost.querySelector('#vc-tribe-input');
            listen(tribeIn, 'input', () => {
                this.setCardField('tribe', tribeIn.value);
            });

            const autoFitChk = this.settingsHost.querySelector('#vc-autofit-font');
            listen(autoFitChk, 'change', e => {
                this.mseCard.presentation.autoFitFontSize = e.target.checked;
                this.syncToEditorCard();
                void this.paint();
            });

            const implementedChk = this.settingsHost.querySelector('#vc-implemented');
            listen(implementedChk, 'change', e => {
                this.mseCard.isImplemented = e.target.checked;
                this.syncToEditorCard();
                this.setStatus(e.target.checked ? '已实装 · 保存后可用于正式对战' : '待实装 · 保存后仅用于图鉴浏览或云构筑草稿');
                void this.paint();
            });

            const fontSizeSlider = this.settingsHost.querySelector('#vc-fontsize');
            listen(fontSizeSlider, 'input', () => {
                const val = Number(fontSizeSlider.value);
                this.mseCard.presentation.fontSize = val;
                const label = this.query('#vc-fontsize-val');
                if (label) label.textContent = String(val);
                this.syncToEditorCard();
                void this.paint();
            });

            const artZoomSlider = this.settingsHost.querySelector('#vc-art-zoom');
            listen(artZoomSlider, 'input', () => {
                const val = Number(artZoomSlider.value);
                this.mseCard.presentation.art.crop.zoom = val;
                this.artDirty = true;
                const label = this.query('#vc-zoom-val');
                if (label) label.textContent = `${Math.round(val * 100)}%`;
                this.syncToEditorCard();
                void this.paint();
            });

            const langSel = this.settingsHost.querySelector('#vc-lang');
            listen(langSel, 'change', () => {
                this.mseCard.presentation.language = langSel.value;
                this.syncToEditorCard();
                void this.paint();
            });

            const collectibleChk = this.settingsHost.querySelector('#vc-collectible');
            listen(collectibleChk, 'change', () => {
                this.mseCard.isCollectible = collectibleChk.checked;
                this.syncToEditorCard();
                void this.paint();
            });

            const altArtChk = this.settingsHost.querySelector('#vc-alt-art');
            listen(altArtChk, 'change', () => {
                this.mseCard.presentation.alternateArt = altArtChk.checked;
                this.syncToEditorCard();
                void this.paint();
            });

            const countdownIn = this.settingsHost.querySelector('#vc-countdown');
            listen(countdownIn, 'input', () => {
                this.setCardField('countdown', countdownIn.value);
            });

            const kwSelect = this.settingsHost.querySelector('#vc-keyword-select');
            listen(kwSelect, 'change', () => {
                const val = kwSelect.value.trim();
                if (val && !this.mseCard.keywords.includes(val)) {
                    this.mseCard.keywords.push(val);
                    this.syncToEditorCard();
                    this.renderKeywordsList();
                }
                kwSelect.value = '';
            });
        }

        // 文件上传与 OCR 导入
        const artInput = this.host.querySelector('#vc-art-file');
        listen(artInput, 'change', () => {
            const file = artInput.files?.[0];
            if (file) void this.loadArtFile(file, this._uploadTargetKey || 'art');
        });

        const ocrInput = this.host.querySelector('#vc-ocr-file');
        listen(ocrInput, 'change', () => {
            const file = ocrInput.files?.[0];
            if (file) void this.handleOcrImportFile(file);
        });

        // 画布插画双击、拖拽与滚轮缩放
        const viewport = this.host.querySelector('#vc-viewport');
        listen(viewport, 'dblclick', e => {
            if (e.target.closest('[data-vc-field="art"]')) {
                this.triggerArtFilePicker('art');
            }
        });

        listen(viewport, 'wheel', e => {
            if (!(e.ctrlKey || e.altKey || this.artDrag)) return;
            if (!e.target.closest('[data-vc-field="art"]') || !this.mseCard.presentation.art.src) return;
            e.preventDefault();
            const delta = e.deltaY < 0 ? 0.08 : -0.08;
            const nextZoom = Math.max(1, Math.min(4, Number((this.mseCard.presentation.art.crop.zoom + delta).toFixed(2))));
            if (nextZoom === this.mseCard.presentation.art.crop.zoom) return;
            this.mseCard.presentation.art.crop.zoom = nextZoom;
            this.artDirty = true;
            const zoomSlider = this.query('#vc-art-zoom');
            const zoomVal = this.query('#vc-zoom-val');
            if (zoomSlider) zoomSlider.value = String(nextZoom);
            if (zoomVal) zoomVal.textContent = `${Math.round(nextZoom * 100)}%`;
            this.syncToEditorCard();
            void this.paint();
        }, { passive: false });

        listen(viewport, 'pointerdown', e => {
            if (!e.target.closest('[data-vc-field="art"]') || e.button !== 0 || !this.mseCard.presentation.art.src) return;
            this.finishInline();
            this.artDrag = {
                pointerId: e.pointerId,
                startX: e.clientX,
                startY: e.clientY,
                crop: { ...this.mseCard.presentation.art.crop },
                image: null,
                moved: false
            };
            const dragRef = this.artDrag;
            loadImage(this.mseCard.presentation.art.src).then(img => {
                if (this.artDrag === dragRef) dragRef.image = img;
            }).catch(() => {});
        });

        listen(viewport, 'pointermove', e => {
            if (!this.artDrag?.image) return;
            const dx = (e.clientX - this.artDrag.startX) / this.scale;
            const dy = (e.clientY - this.artDrag.startY) / this.scale;
            if (Math.abs(dx) + Math.abs(dy) < 3) return;
            if (!this.artDrag.moved) {
                viewport.setPointerCapture(e.pointerId);
                this.artDrag.moved = true;
            }
            const crop = this.mseCard.presentation.art.crop;
            const { x, y } = crop;
            this.moveCrop(crop, this.artDrag.crop, this.artDrag.image, dx, dy);
            if (crop.x !== x || crop.y !== y) this.artDirty = true;
            void this.paint();
        });

        const endArtDrag = () => {
            if (!this.artDrag) return;
            this._wasDraggingArt = this.artDrag.moved;
            if (this.artDrag.moved) {
                this.syncToEditorCard();
            }
            this.artDrag = null;
            setTimeout(() => { this._wasDraggingArt = false; }, 30);
        };
        listen(viewport, 'pointerup', endArtDrag);
        listen(viewport, 'pointercancel', endArtDrag);

        // 拖拽图片到画布
        const dropHint = this.host.querySelector('#vc-drop-hint');
        listen(viewport, 'dragover', e => {
            if ([...(e.dataTransfer?.types || [])].includes('Files')) {
                e.preventDefault();
                if (dropHint) dropHint.hidden = false;
            }
        });
        listen(viewport, 'dragleave', e => {
            if (!viewport.contains(e.relatedTarget) && dropHint) {
                dropHint.hidden = true;
            }
        });
        listen(viewport, 'drop', e => {
            e.preventDefault();
            if (dropHint) dropHint.hidden = true;
            const file = e.dataTransfer?.files?.[0];
            if (file) {
                void this.loadArtFile(file, 'art');
            }
        });
    }
}
