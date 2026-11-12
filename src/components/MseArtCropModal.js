// src/components/MseArtCropModal.js
// 原版 MSE 风格插画截取、无级缩放、旋转翻转与实时卡框遮挡预览工作台

import { loadImage, maskedFrame } from '../mse-editor-test/renderer.js';

const ART_WIDTH = 293;
const ART_HEIGHT = 363;
const ART_ASPECT = ART_WIDTH / ART_HEIGHT;
const PREVIEW_FRAME_W = 391;
const PREVIEW_FRAME_H = 513;
const MAX_ZOOM = 8;

function clamp(val, min, max) {
    return Math.max(min, Math.min(max, val));
}

function readFileAsDataUrl(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = () => reject(reader.error || new Error('读取图片文件失败'));
        reader.readAsDataURL(file);
    });
}

/**
 * 根据旋转角度与水平翻转状态生成变换后的工作原图 Canvas
 */
function buildTransformedSourceCanvas(img, rotation = 0, flipX = false) {
    const normRot = ((rotation % 360) + 360) % 360;
    const swapWh = normRot === 90 || normRot === 270;
    const w = swapWh ? img.naturalHeight || img.height : img.naturalWidth || img.width;
    const h = swapWh ? img.naturalWidth || img.width : img.naturalHeight || img.height;

    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, w);
    canvas.height = Math.max(1, h);
    const ctx = canvas.getContext('2d');

    ctx.save();
    ctx.translate(canvas.width / 2, canvas.height / 2);
    if (flipX) {
        ctx.scale(-1, 1);
    }
    if (normRot) {
        ctx.rotate((normRot * Math.PI) / 180);
    }
    const rawW = img.naturalWidth || img.width;
    const rawH = img.naturalHeight || img.height;
    ctx.drawImage(img, -rawW / 2, -rawH / 2, rawW, rawH);
    ctx.restore();

    return canvas;
}

/**
 * 计算图片内最大内接 293:363 选区尺寸（对应 100% 缩放基准）
 */
function getMaxInscribedSize(srcW, srcH) {
    let w = srcW;
    let h = w / ART_ASPECT;
    if (h > srcH) {
        h = srcH;
        w = h * ART_ASPECT;
    }
    return { width: w, height: h };
}

/**
 * 打开 MSE 插画截取与图像处理弹窗
 * @param {object} options
 * @param {string} [options.initialSrc] - 初始原图 URL 或 DataURL
 * @param {object} [options.initialSession] - 上次截取会话 { rawSrc, rotation, flipX, crop }
 * @param {object} [options.cardContext] - 当前卡牌信息 { type, class, rarity, alternateArt }
 * @param {boolean} [options.autoPickFile] - 打开后是否立即触发选图
 * @returns {Promise<{ croppedDataUrl: string, session: object } | null>}
 */
export function openMseArtCropModal(options = {}) {
    document.querySelector('.mse-crop-modal-backdrop')?.remove();

    return new Promise((resolve) => {
        const cardCtx = options.cardContext || {};
        const abortCtrl = new AbortController();
        const { signal } = abortCtrl;

        const backdrop = document.createElement('div');
        backdrop.className = 'mse-crop-modal-backdrop';
        backdrop.innerHTML = `
            <div class="mse-crop-dialog tech-panel" role="dialog" aria-modal="true" aria-label="MSE 插画截取与图像处理">
                <header class="mse-crop-header">
                    <div class="mse-crop-title-group">
                        <span class="mse-crop-title">插画截取与图像处理</span>
                        <span class="mse-crop-subtitle">拖动选区或四角手柄截取 · 滚轮无级放大缩小 · 支持拖入或 Ctrl+V 粘贴新图</span>
                    </div>
                    <button type="button" class="mse-crop-close-btn" data-crop-action="cancel" aria-label="关闭">×</button>
                </header>

                <div class="mse-crop-body">
                    <!-- 左侧：原图自由框选取景台 -->
                    <div class="mse-crop-stage-wrap" id="mse-crop-stage-wrap">
                        <div class="mse-crop-stage" id="mse-crop-stage">
                            <canvas class="mse-crop-source-canvas" id="mse-crop-source-canvas"></canvas>
                            <div class="mse-crop-selection" id="mse-crop-selection" hidden>
                                <div class="mse-crop-grid-lines" aria-hidden="true"></div>
                                <span class="mse-crop-handle is-nw" data-handle="nw"></span>
                                <span class="mse-crop-handle is-ne" data-handle="ne"></span>
                                <span class="mse-crop-handle is-sw" data-handle="sw"></span>
                                <span class="mse-crop-handle is-se" data-handle="se"></span>
                            </div>
                        </div>
                        <div class="mse-crop-empty-hint" id="mse-crop-empty-hint">
                            <button type="button" class="btn-tech btn-primary" data-crop-action="pick-file">选择插画图片…</button>
                            <span>或直接将图片拖入此处 / 按 Ctrl+V 粘贴</span>
                        </div>
                    </div>

                    <!-- 右侧：实时卡框遮挡预览与图像处理面板 -->
                    <aside class="mse-crop-sidebar">
                        <div class="mse-crop-preview-card tech-section">
                            <div class="mse-crop-preview-head">
                                <span class="form-label">卡面实装实时预览</span>
                                <label class="vc-check-label" title="叠加当前卡牌的卡框与宝石位置，方便避开文字与数值遮挡">
                                    <input type="checkbox" id="mse-crop-frame-overlay" checked> 卡框参考
                                </label>
                            </div>
                            <div class="mse-crop-preview-box">
                                <canvas id="mse-crop-preview-canvas" width="${PREVIEW_FRAME_W}" height="${PREVIEW_FRAME_H}"></canvas>
                            </div>
                        </div>

                        <div class="mse-crop-controls tech-section">
                            <div class="vc-dock-item">
                                <div class="vc-inline-controls vc-space-between">
                                    <label class="form-label" for="mse-crop-zoom-slider">放大倍率</label>
                                    <span class="mse-crop-zoom-readout" id="mse-crop-zoom-readout">100%</span>
                                </div>
                                <input type="range" id="mse-crop-zoom-slider" class="vc-range-input" min="1" max="${MAX_ZOOM}" step="0.02" value="1">
                            </div>

                            <div class="mse-crop-tool-grid">
                                <button type="button" class="btn-tech btn-small" data-crop-action="flip-x" title="水平镜像翻转插画">⇄ 水平翻转</button>
                                <button type="button" class="btn-tech btn-small" data-crop-action="rotate-cw" title="顺时针旋转 90°">↻ 旋转 90°</button>
                                <button type="button" class="btn-tech btn-small" data-crop-action="reset" title="最大化选区并居中重置">⊡ 居中重置</button>
                                <button type="button" class="btn-tech btn-small" data-crop-action="pick-file" title="从本地选择另一张原图">更换原图…</button>
                            </div>
                        </div>
                    </aside>
                </div>

                <footer class="mse-crop-footer">
                    <span class="mse-crop-meta" id="mse-crop-meta">等待载入图片…</span>
                    <div class="mse-crop-footer-actions">
                        <button type="button" class="btn-tech btn-small" data-crop-action="cancel">取消</button>
                        <button type="button" class="btn-tech btn-small btn-primary" id="mse-crop-apply-btn" data-crop-action="apply" disabled>应用插画</button>
                    </div>
                </footer>

                <input type="file" id="mse-crop-file-input" accept="image/png,image/jpeg,image/webp,image/gif" hidden>
            </div>
        `;

        document.body.appendChild(backdrop);

        const stageWrap = backdrop.querySelector('#mse-crop-stage-wrap');
        const stage = backdrop.querySelector('#mse-crop-stage');
        const sourceCanvas = backdrop.querySelector('#mse-crop-source-canvas');
        const selectionEl = backdrop.querySelector('#mse-crop-selection');
        const emptyHint = backdrop.querySelector('#mse-crop-empty-hint');
        const previewCanvas = backdrop.querySelector('#mse-crop-preview-canvas');
        const frameOverlayChk = backdrop.querySelector('#mse-crop-frame-overlay');
        const zoomSlider = backdrop.querySelector('#mse-crop-zoom-slider');
        const zoomReadout = backdrop.querySelector('#mse-crop-zoom-readout');
        const metaEl = backdrop.querySelector('#mse-crop-meta');
        const applyBtn = backdrop.querySelector('#mse-crop-apply-btn');
        const fileInput = backdrop.querySelector('#mse-crop-file-input');

        const state = {
            rawSrc: '',
            rawImage: null,
            workCanvas: null, // 经旋转/翻转后的原图画布
            rotation: 0,
            flipX: false,
            displayScale: 1, // sourceCanvas 显示尺寸 / workCanvas 像素尺寸
            // 原图坐标系下的选区 { x, y, width, height }
            crop: { x: 0, y: 0, width: ART_WIDTH, height: ART_HEIGHT },
            showFrameOverlay: true,
            frameAssets: null,
            drag: null
        };

        // 预加载当前卡面的卡框与宝石贴图供右侧实时遮挡参考使用
        void (async () => {
            try {
                const isFollower = cardCtx.type === 'Follower' || cardCtx.type === 'Leader';
                const frameType = isFollower ? 'minion' : cardCtx.type === 'Spell' ? 'spell' : 'amulet';
                const rarityKey = cardCtx.alternateArt
                    ? 'special'
                    : cardCtx.rarity === 'Legend'
                        ? 'legend'
                        : String(cardCtx.rarity || 'bronze').toLowerCase();
                const classKey = String(cardCtx.class || 'neutral').toLowerCase();
                const [frameImg, gemImg] = await Promise.all([
                    maskedFrame(`card_${frameType}_${rarityKey}.png`),
                    loadImage(`/mse-beyond/gem_${classKey}.png`)
                ]);
                state.frameAssets = { frameImg, gemImg };
                drawLivePreview();
            } catch (_) {
                // 贴图未就绪时退回纯插画预览
            }
        })();

        function close(result = null) {
            abortCtrl.abort();
            resizeObserver.disconnect();
            backdrop.remove();
            resolve(result);
        }

        function setMeta(text, isError = false) {
            if (!metaEl) return;
            metaEl.textContent = text;
            metaEl.classList.toggle('is-error', isError);
        }

        function clampSourceCrop() {
            if (!state.workCanvas) return;
            const srcW = state.workCanvas.width;
            const srcH = state.workCanvas.height;
            const maxBox = getMaxInscribedSize(srcW, srcH);
            const minW = Math.max(16, maxBox.width / MAX_ZOOM);

            const w = clamp(state.crop.width, minW, maxBox.width);
            const h = w / ART_ASPECT;

            state.crop.width = w;
            state.crop.height = h;
            state.crop.x = clamp(state.crop.x, 0, Math.max(0, srcW - w));
            state.crop.y = clamp(state.crop.y, 0, Math.max(0, srcH - h));
        }

        function resetCenteredCrop() {
            if (!state.workCanvas) return;
            const srcW = state.workCanvas.width;
            const srcH = state.workCanvas.height;
            const maxBox = getMaxInscribedSize(srcW, srcH);
            state.crop = {
                x: (srcW - maxBox.width) / 2,
                y: (srcH - maxBox.height) / 2,
                width: maxBox.width,
                height: maxBox.height
            };
        }

        function getCurrentZoom() {
            if (!state.workCanvas || state.crop.width <= 0) return 1;
            const maxBox = getMaxInscribedSize(state.workCanvas.width, state.workCanvas.height);
            return clamp(maxBox.width / state.crop.width, 1, MAX_ZOOM);
        }

        function setCropByZoom(targetZoom) {
            if (!state.workCanvas) return;
            const zoom = clamp(targetZoom, 1, MAX_ZOOM);
            const maxBox = getMaxInscribedSize(state.workCanvas.width, state.workCanvas.height);
            const centerX = state.crop.x + state.crop.width / 2;
            const centerY = state.crop.y + state.crop.height / 2;

            const nextW = maxBox.width / zoom;
            const nextH = nextW / ART_ASPECT;
            state.crop.width = nextW;
            state.crop.height = nextH;
            state.crop.x = centerX - nextW / 2;
            state.crop.y = centerY - nextH / 2;
            clampSourceCrop();
        }

        function layoutStage() {
            if (!state.workCanvas || !stageWrap || !stage || !sourceCanvas) return;
            const availW = Math.max(200, stageWrap.clientWidth - 32);
            const availH = Math.max(200, stageWrap.clientHeight - 32);
            const srcW = state.workCanvas.width;
            const srcH = state.workCanvas.height;

            state.displayScale = Math.min(1, availW / srcW, availH / srcH);
            const dispW = Math.max(1, Math.round(srcW * state.displayScale));
            const dispH = Math.max(1, Math.round(srcH * state.displayScale));

            stage.style.width = `${dispW}px`;
            stage.style.height = `${dispH}px`;
            sourceCanvas.style.width = `${dispW}px`;
            sourceCanvas.style.height = `${dispH}px`;

            renderSelectionAndPreview();
        }

        function drawLivePreview() {
            if (!previewCanvas) return;
            const ctx = previewCanvas.getContext('2d');
            ctx.clearRect(0, 0, PREVIEW_FRAME_W, PREVIEW_FRAME_H);

            if (!state.workCanvas) return;

            const { x, y, width, height } = state.crop;
            if (state.showFrameOverlay && state.frameAssets) {
                ctx.drawImage(
                    state.workCanvas,
                    x, y, width, height,
                    55, 99, ART_WIDTH, ART_HEIGHT
                );
                ctx.drawImage(state.frameAssets.frameImg, 0, 0, PREVIEW_FRAME_W, PREVIEW_FRAME_H);
                ctx.drawImage(state.frameAssets.gemImg, 187, 441, 25, 26);
            } else {
                // 纯净插画等比居中显示
                const padX = (PREVIEW_FRAME_W - ART_WIDTH * 1.24) / 2;
                const padY = (PREVIEW_FRAME_H - ART_HEIGHT * 1.24) / 2;
                ctx.drawImage(
                    state.workCanvas,
                    x, y, width, height,
                    padX, padY, ART_WIDTH * 1.24, ART_HEIGHT * 1.24
                );
            }
        }

        function renderSelectionAndPreview() {
            if (!state.workCanvas || !selectionEl) return;
            const s = state.displayScale;
            selectionEl.hidden = false;
            selectionEl.style.left = `${Math.round(state.crop.x * s)}px`;
            selectionEl.style.top = `${Math.round(state.crop.y * s)}px`;
            selectionEl.style.width = `${Math.round(state.crop.width * s)}px`;
            selectionEl.style.height = `${Math.round(state.crop.height * s)}px`;

            const zoom = getCurrentZoom();
            if (zoomSlider && document.activeElement !== zoomSlider) {
                zoomSlider.value = zoom.toFixed(2);
            }
            if (zoomReadout) {
                zoomReadout.textContent = `${Math.round(zoom * 100)}%`;
            }

            setMeta(
                `原图 ${state.workCanvas.width} × ${state.workCanvas.height} px · 截取区域 ${Math.round(state.crop.width)} × ${Math.round(state.crop.height)} px`
            );
            drawLivePreview();
        }

        function rebuildWorkCanvas(preserveCrop = false) {
            if (!state.rawImage) return;
            state.workCanvas = buildTransformedSourceCanvas(state.rawImage, state.rotation, state.flipX);
            sourceCanvas.width = state.workCanvas.width;
            sourceCanvas.height = state.workCanvas.height;
            const ctx = sourceCanvas.getContext('2d');
            ctx.clearRect(0, 0, sourceCanvas.width, sourceCanvas.height);
            ctx.drawImage(state.workCanvas, 0, 0);

            if (!preserveCrop) {
                resetCenteredCrop();
            } else {
                clampSourceCrop();
            }
            layoutStage();
        }

        async function loadSourceUrl(src, restoreSession = null) {
            if (!src) return;
            setMeta('正在载入插画…');
            try {
                const img = await loadImage(src);
                state.rawSrc = src;
                state.rawImage = img;
                state.rotation = restoreSession?.rotation ?? 0;
                state.flipX = Boolean(restoreSession?.flipX);

                if (emptyHint) emptyHint.hidden = true;
                if (applyBtn) applyBtn.disabled = false;

                rebuildWorkCanvas(false);
                if (restoreSession?.crop && restoreSession.crop.width > 0) {
                    state.crop = { ...restoreSession.crop };
                    clampSourceCrop();
                    renderSelectionAndPreview();
                }
            } catch (err) {
                setMeta(`图片载入失败：${err.message}`, true);
            }
        }

        async function loadNewFile(file) {
            if (!file || !file.type.startsWith('image/')) {
                setMeta('请选择 PNG / JPEG / WebP / GIF 格式的图片', true);
                return;
            }
            const dataUrl = await readFileAsDataUrl(file);
            await loadSourceUrl(dataUrl, null);
        }

        function exportSlicedArt() {
            if (!state.workCanvas) return null;
            const { x, y, width, height } = state.crop;
            // 保真输出：保持 293:363 比例，最高 879×1089 (3x 超采样)，避免高分原图模糊
            const outW = Math.round(clamp(width, ART_WIDTH * 2, ART_WIDTH * 3));
            const outH = Math.round(outW / ART_ASPECT);
            const outCanvas = document.createElement('canvas');
            outCanvas.width = outW;
            outCanvas.height = outH;
            const ctx = outCanvas.getContext('2d');
            ctx.imageSmoothingEnabled = true;
            ctx.imageSmoothingQuality = 'high';
            ctx.drawImage(state.workCanvas, x, y, width, height, 0, 0, outW, outH);

            return {
                croppedDataUrl: outCanvas.toDataURL('image/webp', 0.94),
                session: {
                    rawSrc: state.rawSrc,
                    rotation: state.rotation,
                    flipX: state.flipX,
                    crop: { ...state.crop }
                }
            };
        }

        // 事件监听绑定
        const resizeObserver = new ResizeObserver(() => layoutStage());
        if (stageWrap) resizeObserver.observe(stageWrap);

        backdrop.addEventListener('click', e => {
            if (e.target === backdrop) {
                close(null);
                return;
            }
            const btn = e.target.closest('[data-crop-action]');
            if (!btn) return;
            const action = btn.dataset.cropAction;
            if (action === 'cancel') {
                close(null);
            } else if (action === 'pick-file') {
                if (fileInput) {
                    fileInput.value = '';
                    fileInput.click();
                }
            } else if (action === 'flip-x' && state.rawImage) {
                const prevW = state.workCanvas.width;
                const mirroredX = prevW - (state.crop.x + state.crop.width);
                state.flipX = !state.flipX;
                state.crop.x = mirroredX;
                rebuildWorkCanvas(true);
            } else if (action === 'rotate-cw' && state.rawImage) {
                state.rotation = (state.rotation + 90) % 360;
                rebuildWorkCanvas(false);
            } else if (action === 'reset' && state.rawImage) {
                resetCenteredCrop();
                renderSelectionAndPreview();
            } else if (action === 'apply' && state.workCanvas) {
                const exported = exportSlicedArt();
                close(exported);
            }
        }, { signal });

        fileInput?.addEventListener('change', () => {
            const file = fileInput.files?.[0];
            if (file) void loadNewFile(file);
        }, { signal });

        frameOverlayChk?.addEventListener('change', () => {
            state.showFrameOverlay = frameOverlayChk.checked;
            drawLivePreview();
        }, { signal });

        zoomSlider?.addEventListener('input', () => {
            setCropByZoom(Number(zoomSlider.value));
            renderSelectionAndPreview();
        }, { signal });

        // 取景框拖拽平移与四角手柄等比缩放
        selectionEl?.addEventListener('pointerdown', e => {
            if (!state.workCanvas || e.button !== 0) return;
            e.preventDefault();
            e.stopPropagation();
            const handle = e.target.closest('[data-handle]')?.dataset?.handle || null;
            state.drag = {
                pointerId: e.pointerId,
                mode: handle || 'move',
                startX: e.clientX,
                startY: e.clientY,
                initialCrop: { ...state.crop }
            };
            selectionEl.setPointerCapture(e.pointerId);
        }, { signal });

        selectionEl?.addEventListener('pointermove', e => {
            if (!state.drag || !state.workCanvas) return;
            const dx = (e.clientX - state.drag.startX) / state.displayScale;
            const dy = (e.clientY - state.drag.startY) / state.displayScale;
            const init = state.drag.initialCrop;
            const srcW = state.workCanvas.width;
            const srcH = state.workCanvas.height;
            const maxBox = getMaxInscribedSize(srcW, srcH);
            const minW = Math.max(16, maxBox.width / MAX_ZOOM);

            if (state.drag.mode === 'move') {
                state.crop.x = init.x + dx;
                state.crop.y = init.y + dy;
                clampSourceCrop();
                renderSelectionAndPreview();
                return;
            }

            // 四角锚点等比缩放（锚定对角点不动）
            const mode = state.drag.mode;
            const anchorRight = init.x + init.width;
            const anchorBottom = init.y + init.height;

            let rawDeltaW = mode.endsWith('e') ? dx : -dx;
            let rawDeltaH = mode.startsWith('s') ? dy : -dy;
            const deltaW = Math.abs(rawDeltaW) >= Math.abs(rawDeltaH * ART_ASPECT)
                ? rawDeltaW
                : rawDeltaH * ART_ASPECT;

            let maxAllowedW = maxBox.width;
            if (mode === 'se') {
                maxAllowedW = Math.min(srcW - init.x, (srcH - init.y) * ART_ASPECT);
            } else if (mode === 'sw') {
                maxAllowedW = Math.min(anchorRight, (srcH - init.y) * ART_ASPECT);
            } else if (mode === 'ne') {
                maxAllowedW = Math.min(srcW - init.x, anchorBottom * ART_ASPECT);
            } else if (mode === 'nw') {
                maxAllowedW = Math.min(anchorRight, anchorBottom * ART_ASPECT);
            }

            const nextW = clamp(init.width + deltaW, minW, Math.max(minW, maxAllowedW));
            const nextH = nextW / ART_ASPECT;

            state.crop.width = nextW;
            state.crop.height = nextH;
            state.crop.x = mode.endsWith('w') ? anchorRight - nextW : init.x;
            state.crop.y = mode.startsWith('n') ? anchorBottom - nextH : init.y;
            clampSourceCrop();
            renderSelectionAndPreview();
        }, { signal });

        const endDrag = () => {
            state.drag = null;
        };
        selectionEl?.addEventListener('pointerup', endDrag, { signal });
        selectionEl?.addEventListener('pointercancel', endDrag, { signal });

        // 鼠标滚轮无级缩放选区
        stageWrap?.addEventListener('wheel', e => {
            if (!state.workCanvas) return;
            e.preventDefault();
            const curZoom = getCurrentZoom();
            const factor = e.deltaY < 0 ? 1.08 : 1 / 1.08;
            setCropByZoom(curZoom * factor);
            renderSelectionAndPreview();
        }, { passive: false, signal });

        // 支持拖入文件与 Ctrl+V 粘贴图片
        stageWrap?.addEventListener('dragover', e => {
            if ([...(e.dataTransfer?.types || [])].includes('Files')) {
                e.preventDefault();
            }
        }, { signal });
        stageWrap?.addEventListener('drop', e => {
            e.preventDefault();
            const file = e.dataTransfer?.files?.[0];
            if (file) void loadNewFile(file);
        }, { signal });

        document.addEventListener('paste', e => {
            const items = e.clipboardData?.items;
            if (!items) return;
            for (const item of items) {
                if (item.type.startsWith('image/')) {
                    const file = item.getAsFile();
                    if (file) {
                        e.preventDefault();
                        void loadNewFile(file);
                        break;
                    }
                }
            }
        }, { signal });

        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopImmediatePropagation();
                close(null);
            } else if (e.key === 'Enter' && state.workCanvas && document.activeElement?.tagName !== 'BUTTON') {
                e.preventDefault();
                close(exportSlicedArt());
            }
        }, { capture: true, signal });

        // 初始化图片加载
        const initSession = options.initialSession;
        const startSrc = initSession?.rawSrc || options.initialSrc || '';
        if (startSrc) {
            void loadSourceUrl(startSrc, initSession);
        } else if (options.autoPickFile) {
            setTimeout(() => fileInput?.click(), 30);
        }
    });
}
