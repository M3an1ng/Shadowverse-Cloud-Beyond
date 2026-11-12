// src/mobileViewport.js - 移动端视口高度同步、安全区与全站横屏引导控制
import { isStandaloneDisplayMode } from './pwaInstall.js';

function getFullscreenElement(doc = document) {
    return doc.fullscreenElement || doc.webkitFullscreenElement || null;
}

function canRequestDocumentFullscreen(doc = document) {
    const root = doc?.documentElement;
    if (!root) return false;
    const fn = root.requestFullscreen || root.webkitRequestFullscreen;
    return typeof fn === 'function' && (doc.fullscreenEnabled !== false);
}

export async function requestLandscapeFullscreen() {
    const root = document.documentElement;
    const requestFn = root?.requestFullscreen || root?.webkitRequestFullscreen;
    if (typeof requestFn === 'function' && !getFullscreenElement()) {
        try {
            await requestFn.call(root, { navigationUI: 'hide' });
        } catch {
            // 浏览器可能拒绝非手势或受限环境下的全屏请求，静默降级
        }
    }

    const orientationApi = window.screen?.orientation;
    if (orientationApi && typeof orientationApi.lock === 'function') {
        try {
            await orientationApi.lock('landscape');
        } catch {
            // 部分浏览器不支持方向锁定，忽略异常
        }
    }
}

export function syncMobileViewportMetrics() {
    const root = document.documentElement;
    if (!root) return;

    const vv = window.visualViewport;
    const width = Math.round(vv?.width || window.innerWidth || 0);
    const height = Math.round(vv?.height || window.innerHeight || 0);

    if (width > 0) {
        root.style.setProperty('--app-vw', `${width}px`);
    }
    if (height > 0) {
        root.style.setProperty('--app-vh', `${height}px`);
    }

    const isCapacitor = Boolean(window.Capacitor?.isNativePlatform?.() || window.Capacitor?.platform && window.Capacitor.platform !== 'web');
    const isStandalone = isCapacitor || isStandaloneDisplayMode({
        standalone: window.navigator?.standalone === true,
        matchMedia: typeof window.matchMedia === 'function' ? window.matchMedia.bind(window) : null
    });
    const isFullscreen = Boolean(getFullscreenElement());

    root.dataset.displayShell = isCapacitor
        ? 'capacitor'
        : (isStandalone || isFullscreen ? 'immersive' : 'browser');

    const fullscreenBtn = document.getElementById('battle-portrait-fullscreen-btn');
    if (fullscreenBtn) {
        const showFullscreenAction = !isCapacitor && !isStandalone && !isFullscreen && canRequestDocumentFullscreen();
        fullscreenBtn.hidden = !showFullscreenAction;
    }
}

export function setupMobileViewport() {
    if (typeof window === 'undefined' || typeof document === 'undefined') {
        return;
    }

    let rafId = 0;
    const scheduleSync = () => {
        if (rafId) return;
        rafId = window.requestAnimationFrame(() => {
            rafId = 0;
            syncMobileViewportMetrics();
        });
    };

    syncMobileViewportMetrics();

    window.addEventListener('resize', scheduleSync, { passive: true });
    window.addEventListener('orientationchange', scheduleSync, { passive: true });
    document.addEventListener('fullscreenchange', scheduleSync);
    document.addEventListener('webkitfullscreenchange', scheduleSync);
    window.visualViewport?.addEventListener?.('resize', scheduleSync, { passive: true });

    document.addEventListener('click', (event) => {
        const dismissBtn = event.target?.closest?.('#battle-portrait-dismiss-btn');
        if (dismissBtn) {
            event.preventDefault();
            document.body?.classList.add('portrait-guard-dismissed');
            document.getElementById('game-page')?.classList.add('portrait-guard-dismissed');
            return;
        }

        const fullscreenBtn = event.target?.closest?.('#battle-portrait-fullscreen-btn');
        if (fullscreenBtn) {
            event.preventDefault();
            void requestLandscapeFullscreen().finally(() => {
                syncMobileViewportMetrics();
            });
        }
    });
}
