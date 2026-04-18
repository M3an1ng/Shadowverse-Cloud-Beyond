const IOS_MANUAL_HINT = 'iPhone 或 iPad 请在 Safari 分享菜单中选择“添加到主屏幕”。';
const GENERIC_MANUAL_HINT = '可通过浏览器菜单中的“安装应用”或“添加到主屏幕”安装。';
const PROMPT_HINT = '安装后可用独立窗口启动，并减少浏览器界面干扰。';
const INSTALLED_HINT = '当前已处于安装态，可从桌面或开始菜单独立启动。';

export function isIosInstallEnvironment({
    userAgent = '',
    platform = '',
    maxTouchPoints = 0
} = {}) {
    const normalizedUserAgent = String(userAgent || '');
    const normalizedPlatform = String(platform || '');

    if (/iPad|iPhone|iPod/i.test(normalizedUserAgent)) {
        return true;
    }

    return normalizedPlatform === 'MacIntel' && Number(maxTouchPoints) > 1;
}

export function isStandaloneDisplayMode({
    standalone = false,
    matchMedia = null
} = {}) {
    if (standalone === true) {
        return true;
    }

    if (typeof matchMedia !== 'function') {
        return false;
    }

    return matchMedia('(display-mode: standalone)').matches
        || matchMedia('(display-mode: fullscreen)').matches;
}

export function getPwaInstallViewModel({
    installed = false,
    promptAvailable = false,
    iosManual = false
} = {}) {
    if (installed) {
        return {
            state: 'installed',
            buttonText: '已安装',
            statusText: INSTALLED_HINT,
            disabled: true
        };
    }

    if (promptAvailable) {
        return {
            state: 'prompt',
            buttonText: '安装应用',
            statusText: PROMPT_HINT,
            disabled: false
        };
    }

    return {
        state: iosManual ? 'ios-manual' : 'manual',
        buttonText: '查看安装方式',
        statusText: iosManual ? IOS_MANUAL_HINT : GENERIC_MANUAL_HINT,
        disabled: false
    };
}

export async function registerPwaServiceWorker({ productionMode = false } = {}) {
    if (!productionMode || !('serviceWorker' in navigator)) {
        return null;
    }

    try {
        return await navigator.serviceWorker.register('/sw.js');
    } catch (error) {
        console.warn('PWA: Failed to register service worker.', error);
        return null;
    }
}

export function setupPwaInstallUI() {
    const installButton = document.getElementById('pwa-install-btn');
    const statusEl = document.getElementById('pwa-install-status');

    if (!installButton || !statusEl) {
        return;
    }

    let deferredPrompt = null;
    const displayModeQuery = typeof window.matchMedia === 'function'
        ? window.matchMedia('(display-mode: standalone)')
        : null;

    const updateView = (overrideStatus = null) => {
        const viewModel = getPwaInstallViewModel({
            installed: isStandaloneDisplayMode({
                standalone: window.navigator?.standalone === true,
                matchMedia: typeof window.matchMedia === 'function' ? window.matchMedia.bind(window) : null
            }),
            promptAvailable: !!deferredPrompt,
            iosManual: isIosInstallEnvironment({
                userAgent: window.navigator?.userAgent,
                platform: window.navigator?.platform,
                maxTouchPoints: window.navigator?.maxTouchPoints
            })
        });

        installButton.textContent = viewModel.buttonText;
        installButton.disabled = viewModel.disabled;
        installButton.dataset.state = viewModel.state;
        statusEl.textContent = overrideStatus || viewModel.statusText;
    };

    const handleBeforeInstallPrompt = (event) => {
        event.preventDefault();
        deferredPrompt = event;
        updateView();
    };

    const handleInstalled = () => {
        deferredPrompt = null;
        updateView('安装完成，可从桌面或开始菜单打开。');
    };

    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt);
    window.addEventListener('appinstalled', handleInstalled);
    displayModeQuery?.addEventListener?.('change', () => updateView());

    installButton.addEventListener('click', async () => {
        if (installButton.disabled) {
            return;
        }

        if (!deferredPrompt) {
            updateView();
            return;
        }

        deferredPrompt.prompt();

        try {
            const result = await deferredPrompt.userChoice;
            const accepted = result?.outcome === 'accepted';
            deferredPrompt = null;
            updateView(accepted ? '安装请求已确认，浏览器会创建应用入口。' : '已取消安装，可稍后再次尝试。');
        } catch (error) {
            deferredPrompt = null;
            console.warn('PWA: Install prompt failed.', error);
            updateView('安装弹窗调用失败，请改用浏览器菜单安装。');
        }
    });

    updateView();
}
