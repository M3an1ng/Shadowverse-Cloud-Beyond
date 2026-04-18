import {
    getPwaInstallViewModel,
    isIosInstallEnvironment,
    isStandaloneDisplayMode
} from '../pwaInstall.js';

describe('pwa install helpers', () => {
    test('returns installed view model when app already runs in standalone mode', () => {
        const viewModel = getPwaInstallViewModel({ installed: true, promptAvailable: true });

        expect(viewModel).toMatchObject({
            state: 'installed',
            buttonText: '已安装',
            disabled: true
        });
    });

    test('returns prompt view model when browser can raise install prompt', () => {
        const viewModel = getPwaInstallViewModel({ installed: false, promptAvailable: true });

        expect(viewModel).toMatchObject({
            state: 'prompt',
            buttonText: '安装应用',
            disabled: false
        });
    });

    test('returns ios manual guidance when automatic prompt is unavailable on ios', () => {
        const viewModel = getPwaInstallViewModel({ iosManual: true });

        expect(viewModel.state).toBe('ios-manual');
        expect(viewModel.buttonText).toBe('查看安装方式');
        expect(viewModel.statusText).toContain('添加到主屏幕');
    });

    test('detects ipad os safari style install environment', () => {
        expect(isIosInstallEnvironment({
            userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)',
            platform: 'MacIntel',
            maxTouchPoints: 5
        })).toBe(true);
    });

    test('detects standalone display mode from media query', () => {
        const matchMedia = jest.fn((query) => ({ matches: query === '(display-mode: standalone)' }));

        expect(isStandaloneDisplayMode({ matchMedia })).toBe(true);
    });
});
