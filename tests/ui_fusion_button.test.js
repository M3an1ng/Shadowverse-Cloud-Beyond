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
    getPlayerState: jest.fn(() => ({
        id: 'player-1',
        ep: 1,
        sep: 0,
        canSuperEvolve: false,
        canEvolve: true,
        turnNumber: 1
    })),
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

const { getActionButtonsHtml } = require('../src/ui.js');

function buildHandCard(overrides = {}) {
    return {
        uid: 'card-1',
        zone: 'hand',
        ownerId: 'player-1',
        type: 'Spell',
        actAbilities: [],
        fusionAbilities: [],
        effectDescriptions: [],
        description: '',
        ...overrides
    };
}

function buildFieldCard(overrides = {}) {
    return {
        uid: 'field-card-1',
        zone: 'field',
        ownerId: 'player-1',
        type: 'Follower',
        canEvolve: false,
        isEvolved: false,
        keywords: [],
        actAbilities: [],
        fusionAbilities: [],
        effectDescriptions: [],
        description: '',
        ...overrides
    };
}

describe('fusion button rendering', () => {
    test('does not render fusion button for fusion trigger text without Fusion keyword', () => {
        const html = getActionButtonsHtml(buildHandCard({
            description: '当这张卡发动融合时，抽取1张牌。',
            effectDescriptions: ['当一张卡牌发动融合时触发的事件。'],
            fusionAbilities: []
        }));

        expect(html).not.toContain('fusion-button');
    });

    test('renders fusion button when explicit Fusion abilities exist', () => {
        const html = getActionButtonsHtml(buildHandCard({
            fusionAbilities: [{ runtimeId: 'fusion-ability-1' }]
        }));

        expect(html).toContain('fusion-button');
        expect(html).toContain('data-fusion-ability-id="fusion-ability-1"');
    });

    test('disables fusion button after reaching per-turn fusion limit', () => {
        const html = getActionButtonsHtml(buildHandCard({
            fusionAbilities: [{ runtimeId: 'fusion-ability-1' }],
            fusionsThisTurn: 1,
            maxFusionsPerTurn: 1
        }));

        expect(html).toContain('fusion-button');
        expect(html).toContain('disabled');
    });

    test('renders act button from explicit Act abilities without boolean flag', () => {
        const html = getActionButtonsHtml(buildFieldCard({
            actAbilities: [{ abilityRuntimeId: 'act-ability-1', isPlayable: true }]
        }));

        expect(html).toContain('act-button');
        expect(html).toContain('data-act-ability-id="act-ability-1"');
    });
});
