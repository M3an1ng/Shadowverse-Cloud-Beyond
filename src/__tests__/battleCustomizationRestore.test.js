import {
    BATTLE_CUSTOMIZATION_ASSET_KEYS
} from '../battleCustomizationStorage.js';

describe('battle customization restore', () => {
    beforeEach(() => {
        jest.resetModules();
        URL.createObjectURL = jest.fn(() => 'blob:test');
        URL.revokeObjectURL = jest.fn();
        global.Image = class {
            set src(_value) {
                setTimeout(() => {
                    this.onerror?.(new Error('decode failed'));
                }, 0);
            }
        };
        document.body.innerHTML = `
            <nav class="top-nav-bar">
                <div class="about-widget"></div>
            </nav>
            <section id="game-page" class="active">
                <header class="page-header"></header>
                <div id="game-container"></div>
            </section>
        `;
    });

    test('keeps stored background when restore image probing fails', async () => {
        const getAsset = jest.fn().mockResolvedValue({
            key: BATTLE_CUSTOMIZATION_ASSET_KEYS.background,
            blob: new Blob(['not-an-image'], { type: 'image/png' }),
            fileName: 'playmat.png'
        });
        const removeAsset = jest.fn();

        jest.doMock('../battleCustomizationStorage.js', () => ({
            BATTLE_CUSTOMIZATION_ASSET_KEYS: {
                background: 'background',
                bgm: 'bgm'
            },
            BATTLE_CUSTOMIZATION_LIMITS: {
                background: {
                    supportedTypes: ['image/png', 'image/jpeg', 'image/webp'],
                    maxBytes: 10 * 1024 * 1024
                }
            },
            battleCustomizationStorage: {
                isSupported: () => true,
                getAsset,
                removeAsset,
                saveAsset: jest.fn()
            },
            validateBattleCustomizationFile: jest.fn(() => ({ ok: true }))
        }));

        const { initializeBattleCustomization } = await import('../battleCustomization.js');

        await initializeBattleCustomization({
            getCurrentRoute: () => 'game'
        });

        expect(getAsset).toHaveBeenCalledWith(BATTLE_CUSTOMIZATION_ASSET_KEYS.background);
        expect(removeAsset).not.toHaveBeenCalled();
    });
});
