import {
    BATTLE_CUSTOMIZATION_ASSET_KEYS,
    BATTLE_CUSTOMIZATION_LIMITS,
    battleCustomizationStorage,
    validateBattleCustomizationFile
} from './battleCustomizationStorage.js';

const BACKGROUND_LAYER_ID = 'battle-custom-background';
const ROOT_ID = 'battle-customization-root';
const TOGGLE_ID = 'battle-customization-toggle';
const CARD_ID = 'battle-customization-card';
const DEFAULT_OUTPUT_WIDTH = 1600;
const DEFAULT_OUTPUT_HEIGHT = 900;

const state = {
    initialized: false,
    getCurrentRoute: () => '',
    currentBackgroundUrl: '',
    backgroundRecord: null,
    backgroundImageInfo: null,
    restorePromise: null,
    supportError: '',
    resizeObserver: null,
    elements: {}
};

function syncBackgroundDecorState(enabled) {
    const active = Boolean(enabled);
    state.elements.backgroundLayer?.classList.toggle('is-active', active);
    state.elements.gamePage?.classList.toggle('has-battle-custom-background', active);
    state.elements.gameContainer?.classList.toggle('has-battle-custom-background', active);
}

function isStorageSupported() {
    return battleCustomizationStorage.isSupported()
        && typeof URL !== 'undefined'
        && typeof URL.createObjectURL === 'function'
        && typeof URL.revokeObjectURL === 'function';
}

function isGameRouteActive() {
    const route = String(state.getCurrentRoute?.() ?? '').trim();
    if (route === 'game') {
        return true;
    }
    const gamePage = document.getElementById('game-page');
    return !!gamePage && gamePage.classList.contains('active');
}

function syncTopNavHoverLock() {
    const topNav = document.querySelector('.top-nav-bar');
    if (!topNav) return;
    const hasPinnedCard = !!topNav.querySelector('.user-card-popup.active');
    document.body.classList.toggle('topnav-hover-locked', hasPinnedCard);
}

function closeOtherTopNavCards() {
    const activeCards = document.querySelectorAll('.top-nav-bar .user-card-popup.active');
    activeCards.forEach((card) => {
        if (!(card instanceof HTMLElement) || card.id === CARD_ID) {
            return;
        }
        card.classList.remove('active');
        card.setAttribute('aria-hidden', 'true');
    });
}

function setMessage(message, tone = 'neutral') {
    const messageEl = state.elements.message;
    if (!messageEl) return;
    messageEl.textContent = String(message ?? '').trim();
    messageEl.dataset.tone = tone;
}

function revokeBackgroundUrl() {
    if (state.currentBackgroundUrl) {
        URL.revokeObjectURL(state.currentBackgroundUrl);
    }
    state.currentBackgroundUrl = '';
}

function clearBackgroundLayer() {
    revokeBackgroundUrl();
    const layer = state.elements.backgroundLayer;
    if (layer) {
        layer.style.backgroundImage = '';
        layer.style.backgroundSize = '';
        layer.style.backgroundPosition = '';
    }
    state.backgroundRecord = null;
    state.backgroundImageInfo = null;
    syncBackgroundDecorState(false);
}

function positionBackgroundLayer() {
    const layer = state.elements.backgroundLayer;
    const gamePage = state.elements.gamePage || document.getElementById('game-page');
    if (!layer || !gamePage) {
        return null;
    }

    const pageRect = gamePage.getBoundingClientRect();
    const pageStyles = window.getComputedStyle(gamePage);
    const paddingLeft = Number.parseFloat(pageStyles.paddingLeft) || 0;
    const paddingRight = Number.parseFloat(pageStyles.paddingRight) || 0;
    const paddingBottom = Number.parseFloat(pageStyles.paddingBottom) || 0;
    const pageHeader = state.elements.pageHeader || gamePage.querySelector('.page-header');
    const topOffset = Math.max(
        0,
        (pageHeader instanceof HTMLElement)
            ? (pageHeader.offsetTop + pageHeader.offsetHeight)
            : 0
    );

    layer.style.top = roundCssPixel(topOffset);
    layer.style.left = roundCssPixel(-paddingLeft);
    layer.style.right = roundCssPixel(-paddingRight);
    layer.style.bottom = roundCssPixel(-paddingBottom);

    return layer.getBoundingClientRect();
}

function getBattleContainerTargetSize() {
    const gameContainer = state.elements.gameContainer || document.getElementById('game-container');
    const rect = gameContainer?.getBoundingClientRect?.();
    const width = Math.max(
        1,
        Math.round(rect?.width || gameContainer?.clientWidth || DEFAULT_OUTPUT_WIDTH)
    );
    const height = Math.max(
        1,
        Math.round(rect?.height || gameContainer?.clientHeight || DEFAULT_OUTPUT_HEIGHT)
    );

    return {
        width,
        height,
        ratio: width / height
    };
}

function probeImageBlob(blob) {
    const objectUrl = URL.createObjectURL(blob);
    return new Promise((resolve, reject) => {
        const image = new Image();
        image.onload = () => {
            const result = {
                naturalWidth: image.naturalWidth,
                naturalHeight: image.naturalHeight
            };
            URL.revokeObjectURL(objectUrl);
            resolve(result);
        };
        image.onerror = () => {
            URL.revokeObjectURL(objectUrl);
            reject(new Error('图片加载失败。'));
        };
        image.src = objectUrl;
    });
}

function roundCssPixel(value) {
    return `${Math.round(value * 100) / 100}px`;
}

function getNormalizedBackgroundCrop(record) {
    const metadata = record?.metadata;
    const crop = metadata?.crop;
    if (!crop) {
        return { x: 0, y: 0, width: 1, height: 1 };
    }

    const x = Number(crop.x);
    const y = Number(crop.y);
    const width = Number(crop.width);
    const height = Number(crop.height);

    if (
        !Number.isFinite(x)
        || !Number.isFinite(y)
        || !Number.isFinite(width)
        || !Number.isFinite(height)
        || width <= 0
        || height <= 0
    ) {
        return { x: 0, y: 0, width: 1, height: 1 };
    }

    const normalizedWidth = Math.min(Math.max(width, 0.0001), 1);
    const normalizedHeight = Math.min(Math.max(height, 0.0001), 1);

    return {
        x: Math.min(Math.max(x, 0), 1 - normalizedWidth),
        y: Math.min(Math.max(y, 0), 1 - normalizedHeight),
        width: normalizedWidth,
        height: normalizedHeight
    };
}

function renderBackgroundLayer() {
    const layer = state.elements.backgroundLayer;
    const imageInfo = state.backgroundImageInfo;

    if (!layer || !state.currentBackgroundUrl || !imageInfo?.naturalWidth || !imageInfo?.naturalHeight) {
        return;
    }

    const rect = positionBackgroundLayer();
    const containerWidth = Math.max(1, rect?.width || layer.clientWidth || DEFAULT_OUTPUT_WIDTH);
    const containerHeight = Math.max(1, rect?.height || layer.clientHeight || DEFAULT_OUTPUT_HEIGHT);
    const crop = getNormalizedBackgroundCrop(state.backgroundRecord);
    const cropWidth = Math.max(1, imageInfo.naturalWidth * crop.width);
    const cropHeight = Math.max(1, imageInfo.naturalHeight * crop.height);
    const cropCenterX = imageInfo.naturalWidth * (crop.x + (crop.width / 2));
    const cropCenterY = imageInfo.naturalHeight * (crop.y + (crop.height / 2));
    const scale = Math.max(containerWidth / cropWidth, containerHeight / cropHeight);
    const imageWidth = imageInfo.naturalWidth * scale;
    const imageHeight = imageInfo.naturalHeight * scale;
    const offsetX = (containerWidth / 2) - (cropCenterX * scale);
    const offsetY = (containerHeight / 2) - (cropCenterY * scale);

    layer.style.backgroundImage = `url("${state.currentBackgroundUrl}")`;
    layer.style.backgroundSize = `${roundCssPixel(imageWidth)} ${roundCssPixel(imageHeight)}`;
    layer.style.backgroundPosition = `${roundCssPixel(offsetX)} ${roundCssPixel(offsetY)}`;
    syncBackgroundDecorState(true);
}

async function applyBackgroundRecord(record, options = {}) {
    if (!record?.blob) {
        clearBackgroundLayer();
        updateUi();
        return;
    }

    try {
        const imageInfo = await probeImageBlob(record.blob);
        const objectUrl = URL.createObjectURL(record.blob);
        revokeBackgroundUrl();
        state.currentBackgroundUrl = objectUrl;
        state.backgroundRecord = record;
        state.backgroundImageInfo = imageInfo;
        renderBackgroundLayer();
    } catch (error) {
        if (options.removeBroken !== false) {
            await battleCustomizationStorage.removeAsset(BATTLE_CUSTOMIZATION_ASSET_KEYS.background).catch(() => {});
        }
        clearBackgroundLayer();
        setMessage(`桌布恢复失败：${error?.message || '图片无法加载。'}`, 'warning');
    }

    updateUi();
}

function getAssetLabel(record) {
    return record?.fileName || '未设置';
}

function createWidgetMarkup() {
    const root = document.createElement('div');
    root.id = ROOT_ID;
    root.className = 'user-widget battle-customization-widget';
    root.innerHTML = `
        <button id="${TOGGLE_ID}" class="icon-btn battle-customization-toggle" type="button" aria-label="Battle Background" aria-expanded="false" title="本地桌布">
            <svg viewBox="0 0 24 24" class="icon-svg battle-customization-icon" aria-hidden="true">
                <path d="M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2zm0 2v14h14V5H5zm10.4 1.75a1.7 1.7 0 1 1 0 3.4 1.7 1.7 0 0 1 0-3.4zM5 17.7l4.45-5.02a.95.95 0 0 1 1.44.02l1.9 2.16 3.05-4.14a.95.95 0 0 1 1.54.03L19 13.16V19H5v-1.3z"></path>
            </svg>
        </button>
        <div id="${CARD_ID}" class="user-card-popup battle-customization-card" aria-hidden="true">
            <div class="battle-customization-card-header">
                <div class="battle-customization-card-title">对战桌布</div>
                <div class="battle-customization-card-kicker">仅本地生效</div>
            </div>
            <div class="battle-customization-card-body">
                <div class="battle-customization-preview-row">
                    <div class="battle-customization-preview" data-role="background-preview" aria-hidden="true"></div>
                    <div class="battle-customization-meta">
                        <div class="battle-customization-meta-label">当前资源</div>
                        <div class="battle-customization-meta-value" data-role="background-label">未设置</div>
                        <div class="battle-customization-meta-note">仅保存在当前浏览器</div>
                    </div>
                </div>
                <div class="battle-customization-actions">
                    <button type="button" class="btn" data-action="select-background">选择</button>
                    <button type="button" class="btn btn-danger" data-action="clear-background">清除</button>
                </div>
                <div class="battle-customization-message" data-role="message" aria-live="polite"></div>
            </div>
        </div>
    `;
    return root;
}

function setCardOpen(open) {
    const card = state.elements.card;
    if (!card) return;

    card.classList.toggle('active', open);
    card.setAttribute('aria-hidden', open ? 'false' : 'true');
    state.elements.toggle?.setAttribute('aria-expanded', open ? 'true' : 'false');

    if (open) {
        closeOtherTopNavCards();
    }
    syncTopNavHoverLock();
}

function ensureDom() {
    const gamePage = document.getElementById('game-page');
    const gameContainer = document.getElementById('game-container');
    const pageHeader = gamePage?.querySelector('.page-header');
    const topNav = document.querySelector('.top-nav-bar');
    if (!gamePage || !gameContainer || !pageHeader || !topNav) {
        return false;
    }

    let backgroundLayer = document.getElementById(BACKGROUND_LAYER_ID);
    if (!backgroundLayer) {
        backgroundLayer = document.createElement('div');
        backgroundLayer.id = BACKGROUND_LAYER_ID;
        backgroundLayer.className = 'battle-custom-background';
    }
    if (backgroundLayer.parentElement !== gamePage) {
        gamePage.insertBefore(backgroundLayer, gameContainer);
    }
    positionBackgroundLayer();

    let root = document.getElementById(ROOT_ID);
    if (!root) {
        root = createWidgetMarkup();
        const aboutWidget = topNav.querySelector('.about-widget');
        topNav.insertBefore(root, aboutWidget || null);
    }

    state.elements = {
        root,
        gamePage,
        pageHeader,
        gameContainer,
        backgroundLayer,
        toggle: root.querySelector(`#${TOGGLE_ID}`),
        card: root.querySelector(`#${CARD_ID}`),
        preview: root.querySelector('[data-role="background-preview"]'),
        backgroundLabel: root.querySelector('[data-role="background-label"]'),
        message: root.querySelector('[data-role="message"]'),
        selectBackground: root.querySelector('[data-action="select-background"]'),
        clearBackground: root.querySelector('[data-action="clear-background"]')
    };

    syncBackgroundDecorState(Boolean(state.currentBackgroundUrl || state.backgroundRecord));

    if (typeof ResizeObserver === 'function') {
        if (!state.resizeObserver) {
            state.resizeObserver = new ResizeObserver(() => {
                renderBackgroundLayer();
            });
        }
        state.resizeObserver.disconnect();
        state.resizeObserver.observe(gamePage);
        state.resizeObserver.observe(pageHeader);
        state.resizeObserver.observe(gameContainer);
    }

    return true;
}

function updateUi() {
    const { toggle, card, preview, backgroundLabel, selectBackground, clearBackground, root } = state.elements;
    if (!toggle || !card || !preview || !backgroundLabel || !selectBackground || !clearBackground || !root) {
        return;
    }

    const supported = isStorageSupported();
    const inGameRoute = isGameRouteActive();
    const enabled = supported && inGameRoute;

    toggle.disabled = !enabled;
    toggle.title = !supported
        ? '当前浏览器不支持本地桌布'
        : (inGameRoute ? '本地桌布' : '仅在对战页面可用');
    backgroundLabel.textContent = getAssetLabel(state.backgroundRecord);
    preview.classList.toggle('has-image', Boolean(state.currentBackgroundUrl));
    preview.style.backgroundImage = state.currentBackgroundUrl ? `url("${state.currentBackgroundUrl}")` : '';
    selectBackground.disabled = !enabled;
    clearBackground.disabled = !enabled || !state.backgroundRecord;
    root.classList.toggle('is-route-disabled', !inGameRoute);

    if (!enabled) {
        setCardOpen(false);
    }
}

function getSourceValidationMessage(file) {
    const mimeType = String(file?.type ?? '').trim().toLowerCase();
    if (!mimeType || !BATTLE_CUSTOMIZATION_LIMITS.background.supportedTypes.includes(mimeType)) {
        return `不支持的图片类型。支持类型：${BATTLE_CUSTOMIZATION_LIMITS.background.supportedTypes.join(', ')}`;
    }
    return '';
}

function buildBackgroundCropMetadata(imageRect, cropRect, naturalWidth, naturalHeight) {
    const scaleX = naturalWidth / imageRect.width;
    const scaleY = naturalHeight / imageRect.height;
    const sourceX = Math.max(0, cropRect.x * scaleX);
    const sourceY = Math.max(0, cropRect.y * scaleY);
    const sourceWidth = Math.max(1, cropRect.width * scaleX);
    const sourceHeight = Math.max(1, cropRect.height * scaleY);

    return {
        naturalWidth,
        naturalHeight,
        crop: {
            x: sourceX / naturalWidth,
            y: sourceY / naturalHeight,
            width: sourceWidth / naturalWidth,
            height: sourceHeight / naturalHeight
        }
    };
}

function getMaxCropSize(imageRect, targetRatio) {
    if (!imageRect || !Number.isFinite(targetRatio) || targetRatio <= 0) {
        return { width: 0, height: 0 };
    }

    const imageRatio = imageRect.width / imageRect.height;
    if (imageRatio >= targetRatio) {
        const height = imageRect.height;
        return {
            width: height * targetRatio,
            height
        };
    }

    const width = imageRect.width;
    return {
        width,
        height: width / targetRatio
    };
}

function getMaxCropWidthForOrigin(imageRect, targetRatio, cropX, cropY) {
    if (!imageRect || !Number.isFinite(targetRatio) || targetRatio <= 0) {
        return 0;
    }

    const availableWidth = (imageRect.x + imageRect.width) - cropX;
    const availableHeight = (imageRect.y + imageRect.height) - cropY;
    return Math.max(0, Math.min(availableWidth, availableHeight * targetRatio));
}

async function saveBackgroundSelection(file, metadata) {
    const validation = validateBattleCustomizationFile(BATTLE_CUSTOMIZATION_ASSET_KEYS.background, file);
    if (!validation.ok) {
        setMessage(validation.message, 'warning');
        return;
    }

    const record = await battleCustomizationStorage.saveAsset(
        BATTLE_CUSTOMIZATION_ASSET_KEYS.background,
        file,
        { metadata }
    );
    await applyBackgroundRecord(record, { removeBroken: true });
    setMessage(`桌布已更新为 ${record.fileName || '本地图片'}。`, 'success');
}

async function clearBackgroundCustomization() {
    await battleCustomizationStorage.removeAsset(BATTLE_CUSTOMIZATION_ASSET_KEYS.background);
    clearBackgroundLayer();
    setMessage('已清除本地桌布。', 'neutral');
    updateUi();
}

function openBackgroundCropModal(file, outputSize) {
    return new Promise((resolve) => {
        if (document.querySelector('.battle-background-crop-modal')) {
            resolve(null);
            return;
        }

        const modal = document.createElement('div');
        modal.className = 'avatar-crop-modal battle-background-crop-modal';
        modal.innerHTML = `
            <div class="avatar-crop-card battle-background-crop-card">
                <div class="avatar-crop-header">
                    <div class="avatar-crop-title">裁剪对战桌布</div>
                    <button type="button" class="avatar-crop-close">×</button>
                </div>
                <div class="avatar-crop-stage battle-background-crop-stage">
                    <img class="avatar-crop-image" alt="Background preview">
                    <div class="battle-background-crop-boundary">
                        <div class="avatar-crop-box battle-background-crop-box">
                            <span class="avatar-crop-handle"></span>
                        </div>
                    </div>
                    <div class="avatar-crop-placeholder">
                        <div class="avatar-crop-placeholder-icon">BACKGROUND</div>
                        <div class="avatar-crop-placeholder-text">裁剪后将匹配当前对战界面比例</div>
                    </div>
                </div>
                <div class="battle-background-crop-hint">裁剪框会自动贴合当前对战界面比例，并保持在图片范围内。保存时将保留原图分辨率。</div>
                <div class="avatar-crop-actions">
                    <button type="button" class="btn avatar-crop-cancel">取消</button>
                    <button type="button" class="btn btn-primary avatar-crop-upload">保存桌布</button>
                </div>
                <div class="avatar-crop-status"></div>
            </div>
        `;

        document.body.appendChild(modal);

        const closeBtn = modal.querySelector('.avatar-crop-close');
        const cancelBtn = modal.querySelector('.avatar-crop-cancel');
        const saveBtn = modal.querySelector('.avatar-crop-upload');
        const stage = modal.querySelector('.avatar-crop-stage');
        const imageEl = modal.querySelector('.avatar-crop-image');
        const cropBoundary = modal.querySelector('.battle-background-crop-boundary');
        const cropBox = modal.querySelector('.avatar-crop-box');
        const resizeHandle = modal.querySelector('.avatar-crop-handle');
        const statusEl = modal.querySelector('.avatar-crop-status');
        const targetRatio = outputSize.ratio;

        const cropState = {
            objectUrl: '',
            imageRect: null,
            naturalWidth: 0,
            naturalHeight: 0,
            crop: { x: 0, y: 0, width: 0, height: 0 },
            dragging: false,
            resizing: false,
            startX: 0,
            startY: 0,
            startCropX: 0,
            startCropY: 0,
            startWidth: 0
        };
        const handlePointerMove = (event) => {
            if (!cropState.imageRect) return;
            if (cropState.dragging) {
                const dx = event.clientX - cropState.startX;
                const dy = event.clientY - cropState.startY;
                cropState.crop.x = cropState.startCropX + dx;
                cropState.crop.y = cropState.startCropY + dy;
                clampCrop();
                renderCrop();
                return;
            }

            if (cropState.resizing) {
                const dx = event.clientX - cropState.startX;
                const dy = event.clientY - cropState.startY;
                const dyAsWidth = dy * targetRatio;
                const delta = Math.abs(dx) >= Math.abs(dyAsWidth) ? dx : dyAsWidth;
                cropState.crop.width = cropState.startWidth + delta;
                clampCrop();
                renderCrop();
            }
        };

        const setStatus = (text, isError = false) => {
            if (!statusEl) return;
            statusEl.textContent = text || '';
            statusEl.style.color = isError ? 'rgba(255, 120, 120, 0.9)' : 'rgba(255, 255, 255, 0.7)';
        };

        const cleanup = (result = null) => {
            window.removeEventListener('pointermove', handlePointerMove);
            window.removeEventListener('pointerup', stopPointer);
            window.removeEventListener('pointercancel', stopPointer);
            if (cropState.objectUrl) {
                URL.revokeObjectURL(cropState.objectUrl);
            }
            modal.remove();
            resolve(result);
        };

        const clampCrop = () => {
            if (!cropState.imageRect) return;
            const localImageRect = {
                x: 0,
                y: 0,
                width: cropState.imageRect.width,
                height: cropState.imageRect.height
            };
            const maxSize = getMaxCropSize(localImageRect, targetRatio);
            const maxWidth = cropState.resizing
                ? getMaxCropWidthForOrigin(localImageRect, targetRatio, cropState.crop.x, cropState.crop.y)
                : maxSize.width;
            const minWidth = Math.min(160, maxWidth);
            cropState.crop.width = Math.min(Math.max(cropState.crop.width, minWidth), maxWidth);
            cropState.crop.height = cropState.crop.width / targetRatio;

            const maxX = cropState.imageRect.width - cropState.crop.width;
            const maxY = cropState.imageRect.height - cropState.crop.height;
            cropState.crop.x = Math.min(Math.max(cropState.crop.x, 0), maxX);
            cropState.crop.y = Math.min(Math.max(cropState.crop.y, 0), maxY);
        };

        const renderCrop = () => {
            if (!cropBox) return;
            cropBox.style.left = `${cropState.crop.x}px`;
            cropBox.style.top = `${cropState.crop.y}px`;
            cropBox.style.width = `${cropState.crop.width}px`;
            cropBox.style.height = `${cropState.crop.height}px`;
        };

        const updateImageRect = () => {
            if (!stage || !imageEl || !cropBoundary) return;
            const stageRect = stage.getBoundingClientRect();
            const imageRect = imageEl.getBoundingClientRect();
            cropState.imageRect = {
                x: imageRect.left - stageRect.left,
                y: imageRect.top - stageRect.top,
                width: imageRect.width,
                height: imageRect.height
            };
            cropBoundary.style.left = `${cropState.imageRect.x}px`;
            cropBoundary.style.top = `${cropState.imageRect.y}px`;
            cropBoundary.style.width = `${cropState.imageRect.width}px`;
            cropBoundary.style.height = `${cropState.imageRect.height}px`;
        };

        const initCrop = () => {
            if (!cropState.imageRect) return;
            const maxSize = getMaxCropSize(
                {
                    x: 0,
                    y: 0,
                    width: cropState.imageRect.width,
                    height: cropState.imageRect.height
                },
                targetRatio
            );
            cropState.crop.width = maxSize.width;
            cropState.crop.height = maxSize.height;
            cropState.crop.x = (cropState.imageRect.width - cropState.crop.width) / 2;
            cropState.crop.y = (cropState.imageRect.height - cropState.crop.height) / 2;
            clampCrop();
            renderCrop();
        };

        const stopPointer = (event) => {
            if (cropState.dragging && cropBox) {
                cropState.dragging = false;
                cropBox.releasePointerCapture(event.pointerId);
                cropBox.classList.remove('dragging');
            }
            if (cropState.resizing && resizeHandle) {
                cropState.resizing = false;
                resizeHandle.releasePointerCapture(event.pointerId);
                cropBox?.classList.remove('resizing');
            }
        };

        closeBtn?.addEventListener('click', () => cleanup(null));
        cancelBtn?.addEventListener('click', () => cleanup(null));
        modal.addEventListener('click', (event) => {
            if (event.target === modal) {
                cleanup(null);
            }
        });

        cropBox?.addEventListener('pointerdown', (event) => {
            if (event.target === resizeHandle) return;
            event.preventDefault();
            cropState.dragging = true;
            cropState.startX = event.clientX;
            cropState.startY = event.clientY;
            cropState.startCropX = cropState.crop.x;
            cropState.startCropY = cropState.crop.y;
            cropBox.setPointerCapture(event.pointerId);
            cropBox.classList.add('dragging');
        });

        resizeHandle?.addEventListener('pointerdown', (event) => {
            event.preventDefault();
            event.stopPropagation();
            cropState.resizing = true;
            cropState.startX = event.clientX;
            cropState.startY = event.clientY;
            cropState.startWidth = cropState.crop.width;
            resizeHandle.setPointerCapture(event.pointerId);
            cropBox?.classList.add('resizing');
        });

        window.addEventListener('pointermove', handlePointerMove);
        window.addEventListener('pointerup', stopPointer);
        window.addEventListener('pointercancel', stopPointer);

        saveBtn?.addEventListener('click', async () => {
            if (!imageEl?.src || !cropState.imageRect || cropState.crop.width <= 0 || cropState.crop.height <= 0) {
                setStatus('请先完成裁剪。', true);
                return;
            }

            setStatus('保存中...');
            saveBtn.disabled = true;
            try {
                const cropMetadata = buildBackgroundCropMetadata(
                    cropState.imageRect,
                    cropState.crop,
                    cropState.naturalWidth,
                    cropState.naturalHeight
                );
                cleanup(cropMetadata);
            } catch (error) {
                setStatus(`保存失败：${error?.message || '裁剪数据生成失败。'}`, true);
                saveBtn.disabled = false;
            }
        });

        if (!imageEl) {
            cleanup(null);
            return;
        }

        cropState.objectUrl = URL.createObjectURL(file);
        imageEl.onload = () => {
            cropState.naturalWidth = imageEl.naturalWidth;
            cropState.naturalHeight = imageEl.naturalHeight;
            stage?.classList.add('has-image');
            requestAnimationFrame(() => {
                updateImageRect();
                initCrop();
                setStatus('');
            });
        };
        imageEl.onerror = () => {
            setStatus('图片加载失败。', true);
        };
        imageEl.src = cropState.objectUrl;
    });
}

async function handleSelectBackground() {
    const outputSize = getBattleContainerTargetSize();
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = BATTLE_CUSTOMIZATION_LIMITS.background.supportedTypes.join(',');
    input.style.display = 'none';
    document.body.appendChild(input);

    const cleanup = () => input.remove();

    input.addEventListener('change', async () => {
        const file = input.files?.[0];
        cleanup();
        if (!file) {
            return;
        }

        const sourceValidationMessage = getSourceValidationMessage(file);
        if (sourceValidationMessage) {
            setMessage(sourceValidationMessage, 'warning');
            return;
        }

        const cropMetadata = await openBackgroundCropModal(file, outputSize);
        if (!cropMetadata) {
            return;
        }

        await saveBackgroundSelection(file, cropMetadata);
    }, { once: true });

    input.click();
}

async function bindEvents() {
    const { toggle, card, root, selectBackground, clearBackground } = state.elements;
    if (!toggle || !card || !root || !selectBackground || !clearBackground) {
        return;
    }

    toggle.addEventListener('click', (event) => {
        event.stopPropagation();
        if (toggle.disabled) {
            return;
        }
        setCardOpen(!card.classList.contains('active'));
    });

    selectBackground.addEventListener('click', async (event) => {
        event.stopPropagation();
        await handleSelectBackground();
    });

    clearBackground.addEventListener('click', async (event) => {
        event.stopPropagation();
        await clearBackgroundCustomization();
    });

    document.addEventListener('click', (event) => {
        if (!card.classList.contains('active')) {
            return;
        }
        const target = event.target;
        if (target instanceof Element && (card.contains(target) || toggle.contains(target))) {
            return;
        }
        setCardOpen(false);
    });

    document.addEventListener('cleanupGame', () => {
        setCardOpen(false);
    });

    window.addEventListener('resize', () => {
        renderBackgroundLayer();
    });
}

async function restoreFromStorage() {
    if (!isStorageSupported()) {
        state.supportError = '当前浏览器不支持本地桌布功能。';
        updateUi();
        return;
    }

    if (state.restorePromise) {
        return await state.restorePromise;
    }

    state.restorePromise = (async () => {
        const backgroundRecord = await battleCustomizationStorage.getAsset(BATTLE_CUSTOMIZATION_ASSET_KEYS.background);
        await applyBackgroundRecord(backgroundRecord, { removeBroken: false });
    })();

    try {
        await state.restorePromise;
    } finally {
        state.restorePromise = null;
    }
}

async function handleRouteChange(newRoute) {
    if (newRoute === 'game') {
        await restoreFromStorage();
        updateUi();
        return;
    }

    setCardOpen(false);
    updateUi();
}

export async function initializeBattleCustomization(options = {}) {
    if (state.initialized) {
        updateUi();
        return;
    }

    if (!ensureDom()) {
        return;
    }

    state.initialized = true;
    state.getCurrentRoute = typeof options.getCurrentRoute === 'function' ? options.getCurrentRoute : (() => '');
    state.supportError = isStorageSupported() ? '' : '当前浏览器不支持本地桌布功能。';

    await bindEvents();
    updateUi();

    if (typeof options.appendAfterRouteChangeHandler === 'function') {
        options.appendAfterRouteChangeHandler('battle-customization', async (_oldRoute, newRoute) => {
            await handleRouteChange(newRoute);
        });
    }

    if (isGameRouteActive()) {
        await restoreFromStorage();
    }

    updateUi();
}
