jest.mock('../src/gameManager.js', () => ({
    gameState: {
        localPlayerId: 'player-1',
        currentPlayerId: 'player-1',
        phase: 'Main',
        isSpectator: false
    },
    playCard: jest.fn(),
    findCardInstanceByUid: jest.fn(),
    getCurrentPlayerId: jest.fn(() => 'player-1'),
    getPlayerState: jest.fn(() => ({ id: 'player-1' })),
    initiateAttack: jest.fn(),
    selectAttackTarget: jest.fn(),
    cancelSelection: jest.fn(),
    evolveCard: jest.fn(),
    superEvolveCard: jest.fn(),
    requestAct: jest.fn(),
    requestFusion: jest.fn(),
    requestPpBoost: jest.fn(),
    getActPlayabilityForCard: jest.fn(() => null),
    getPlayabilityForCard: jest.fn(() => null),
    submitMulligan: jest.fn(),
    endTurnRequest: jest.fn(),
    submitPlayerChoice: jest.fn(),
    cancelPlayerChoice: jest.fn()
}));

jest.mock('../src/previewSlate.js', () => ({ buildPreviewSlateHtml: jest.fn(() => '') }));
jest.mock('../src/previewDescriptionTooltip.js', () => ({
    showPreviewDescriptionTooltip: jest.fn(),
    hidePreviewDescriptionTooltip: jest.fn()
}));
jest.mock('../src/components/CardDetailMockup.js', () => ({ showCardDetailModal: jest.fn() }));
jest.mock('../src/previewDescriptionReferences.js', () => ({ hydrateDescriptionReferences: jest.fn() }));
jest.mock('../src/enumLocalizationCache.js', () => ({
    ensureEnumLocalizations: jest.fn(),
    getCachedEnumLabel: jest.fn(() => '')
}));
jest.mock('../src/ui/logPanel.js', () => ({
    renderGameLog: jest.fn(),
    replaceInMatchChatHistory: jest.fn(),
    replaceInMatchCardHistory: jest.fn(),
    appendInMatchChatMessage: jest.fn(),
    clearInMatchChatUiState: jest.fn(),
    appendInMatchUsedCard: jest.fn(),
    appendInMatchDestroyedCard: jest.fn(),
    clearInMatchUsedCardUiState: jest.fn(),
    findLogCardInstanceByUid: jest.fn(() => null),
    buildMatchHistoryPreviewCard: jest.fn(() => '')
}));
jest.mock('../src/ui/cardAnimationSystem.js', () => ({
    createCardAnimationSystem: jest.fn(() => ({
        animateEnter: jest.fn(),
        animateLeaveOverlay: jest.fn(),
        renderCollection: jest.fn(),
        dispose: jest.fn()
    }))
}));
jest.mock('../src/userSession.js', () => ({ getUserSession: jest.fn(() => null) }));
jest.mock('../src/nativeGameDragGuard.js', () => ({ preventNativeGameDrag: jest.fn() }));
jest.mock('../src/badge/Ward.png', () => 'ward-badge');
jest.mock('../src/badge/Bane.png', () => 'bane-badge');
jest.mock('../src/badge/Drain.png', () => 'drain-badge');
jest.mock('../src/badge/Ambush.png', () => 'ambush-badge');
jest.mock('../src/badge/Pressure.png', () => 'pressure-badge');
jest.mock('../src/badge/Aura.png', () => 'aura-badge');
jest.mock('../src/badge/Shield.png', () => 'shield-badge');
jest.mock('../src/badge/AbilityResist.png', () => 'ability-resist-badge');
jest.mock('../src/badge/Shackle.png', () => 'shackle-badge');

describe('ability trigger toast lifecycle', () => {
    beforeEach(() => {
        jest.useFakeTimers();
    });

    afterEach(() => {
        jest.useRealTimers();
    });

    test('removes toast after its visible and fade durations', async () => {
        const { showAbilityTriggerToast } = require('../src/ui.js');
        document.body.innerHTML = '<div id="game-page" class="active"></div>';

        showAbilityTriggerToast({
            title: '测试能力',
            durationMs: 20
        });

        expect(document.querySelector('.ability-trigger-toast-item')).not.toBeNull();

        await jest.advanceTimersByTimeAsync(2040);
        expect(document.querySelector('.ability-trigger-toast-item')?.classList.contains('is-hiding')).toBe(true);

        await jest.advanceTimersByTimeAsync(360);
        expect(document.querySelector('.ability-trigger-toast-item')).toBeNull();
    });
});
