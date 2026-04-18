jest.mock('../src/gameManager.js', () => ({
    gameState: {
        localPlayerId: 'player-1',
        currentPlayerId: 'player-1',
        phase: 'Main',
        isSpectator: false,
        players: []
    },
    playCard: jest.fn(),
    findCardInstanceByUid: jest.fn(),
    getCurrentPlayerId: jest.fn(() => 'player-1'),
    getPlayerState: jest.fn(),
    initiateAttack: jest.fn(),
    selectAttackTarget: jest.fn(),
    cancelSelection: jest.fn(),
    evolveCard: jest.fn(),
    superEvolveCard: jest.fn(),
    requestAct: jest.fn(),
    requestFusion: jest.fn(),
    requestPpBoost: jest.fn(),
    getActPlayabilityForCard: jest.fn(() => null),
    getPlayabilityForCard: jest.fn(() => ({ isPlayable: true })),
    submitMulligan: jest.fn(),
    endTurnRequest: jest.fn(),
    submitPlayerChoice: jest.fn(),
    cancelPlayerChoice: jest.fn()
}));

jest.mock('../src/previewSlate.js', () => ({
    buildPreviewSlateHtml: jest.fn(() => '')
}));

jest.mock('../src/previewDescriptionTooltip.js', () => ({
    showPreviewDescriptionTooltip: jest.fn(),
    hidePreviewDescriptionTooltip: jest.fn()
}));

jest.mock('../src/components/CardDetailMockup.js', () => ({
    showCardDetailModal: jest.fn()
}));

jest.mock('../src/previewDescriptionReferences.js', () => ({
    hydrateDescriptionReferences: jest.fn()
}));

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
    findLogCardInstanceByUid: jest.fn(() => null)
}));

jest.mock('../src/ui/cardAnimationSystem.js', () => ({
    createCardAnimationSystem: jest.fn(() => ({
        animateEnter: jest.fn(),
        animateLeaveOverlay: jest.fn(),
        renderCollection: jest.fn(),
        dispose: jest.fn()
    }))
}));

jest.mock('../src/userSession.js', () => ({
    getUserSession: jest.fn(() => null)
}));

jest.mock('../src/badge/Ward.png', () => 'ward-badge');
jest.mock('../src/badge/Bane.png', () => 'bane-badge');
jest.mock('../src/badge/Drain.png', () => 'drain-badge');
jest.mock('../src/badge/Ambush.png', () => 'ambush-badge');
jest.mock('../src/badge/Pressure.png', () => 'pressure-badge');
jest.mock('../src/badge/Aura.png', () => 'aura-badge');
jest.mock('../src/badge/Shield.png', () => 'shield-badge');
jest.mock('../src/badge/AbilityResist.png', () => 'ability-resist-badge');
jest.mock('../src/badge/Shackle.png', () => 'shackle-badge');

const gameManager = require('../src/gameManager.js');
const { resolveRenderableTargetElement } = require('../src/ui.js');

describe('choice target rendering', () => {
    beforeEach(() => {
        document.body.innerHTML = `
            <div id="game-container">
                <div id="player1-avatar"></div>
                <div id="player2-avatar"></div>
                <div class="card" data-uid="card-1"></div>
            </div>
        `;

        gameManager.gameState.players = [
            { id: 'player-1', leaderCard: { uid: 'leader-1', InstanceId: 'leader-1' } },
            { id: 'player-2', leaderCard: { uid: 'leader-2', InstanceId: 'leader-2' } }
        ];

        gameManager.getPlayerState.mockImplementation(playerId =>
            gameManager.gameState.players.find(player => player.id === playerId) ?? null
        );
    });

    test('resolves visible leader targets to avatar elements', () => {
        const targetElement = resolveRenderableTargetElement('leader-2');
        expect(targetElement).toBe(document.getElementById('player2-avatar'));
    });

    test('resolves visible card targets to card elements', () => {
        const targetElement = resolveRenderableTargetElement('card-1');
        expect(targetElement).toBe(document.querySelector('.card[data-uid="card-1"]'));
    });
});
