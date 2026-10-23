jest.mock('../src/gameManager.js', () => ({
    gameState: {
        localPlayerId: 'player-1',
        players: []
    },
    findCardInstanceByUid: jest.fn(),
    sendChatMessage: jest.fn()
}));

jest.mock('../src/abilityDataCache.js', () => ({
    ensureCardDefinitions: jest.fn(() => Promise.resolve()),
    getAbilityDataSnapshot: jest.fn(() => ({ cards: [] }))
}));

jest.mock('../src/userSession.js', () => ({
    getUserSession: jest.fn(() => null)
}));

describe('log panel card tokens', () => {
    test('renders the card name snapshot from log tokens when current card name changed', () => {
        const gameManager = require('../src/gameManager.js');
        gameManager.findCardInstanceByUid.mockReturnValue({
            uid: 'card-1',
            name: '变形后的卡牌',
            ownerId: 'player-1',
            type: 'Follower'
        });

        const { renderGameLog } = require('../src/ui/logPanel.js');

        renderGameLog([
            '[Turn 1] > [[card:card-1|name:原本的卡牌|owner:player-1]]攻击。'
        ]);

        const logContent = document.getElementById('game-log-content');
        expect(logContent.textContent).toContain('原本的卡牌');
        expect(logContent.textContent).not.toContain('变形后的卡牌');
    });
});
