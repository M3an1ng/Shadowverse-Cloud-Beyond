jest.mock('../src/gameManager.js', () => ({
    gameState: {
        localPlayerId: 'player-1',
        players: []
    },
    findCardInstanceByUid: jest.fn(),
    sendChatMessage: jest.fn(),
    sendEmoteMessage: jest.fn(() => Promise.resolve({ ok: true }))
}));

jest.mock('../src/emoteCatalog.js', () => ({
    loadEmoteCatalog: jest.fn(() => Promise.resolve({
        ok: true,
        catalog: {
            packs: [
                {
                    id: 'default',
                    name: 'default',
                    items: [
                        { id: 'default/smile.png', name: 'smile', url: '/assets/emotes/default/smile.png' }
                    ]
                },
                {
                    id: 'line',
                    name: 'line',
                    items: [
                        { id: 'line/hello.webp', name: 'hello', url: '/assets/emotes/line/hello.webp' }
                    ]
                }
            ]
        },
        error: ''
    })),
    hasUsableEmotes: jest.fn(catalog => catalog?.packs?.some(pack => pack.items?.length > 0))
}));

jest.mock('../src/abilityDataCache.js', () => ({
    ensureCardDefinitions: jest.fn(() => Promise.resolve()),
    getAbilityDataSnapshot: jest.fn(() => ({ cards: [] }))
}));

jest.mock('../src/userSession.js', () => ({
    getUserSession: jest.fn(() => null)
}));

const gameManager = require('../src/gameManager.js');

function openChatPanel(renderGameLog) {
    renderGameLog([]);
    document.getElementById('game-chat-toggle').click();
}

async function flushPromises() {
    await Promise.resolve();
    await Promise.resolve();
}

describe('log panel emotes', () => {
    beforeEach(() => {
        document.body.innerHTML = '<div id="game-container"></div>';
        try {
            require('../src/ui/logPanel.js').clearInMatchChatUiState();
        } catch {
            // Module may not be loaded before the first test.
        }
        jest.clearAllMocks();
    });

    test('renders pack tabs, switches packs, and sends clicked emote id', async () => {
        const { renderGameLog } = require('../src/ui/logPanel.js');
        openChatPanel(renderGameLog);

        document.querySelector('.chat-emote-btn').click();
        await flushPromises();

        expect([...document.querySelectorAll('.chat-emote-tab')].map(tab => tab.textContent)).toEqual(['default', 'line']);
        expect(document.querySelector('.chat-emote-item img').getAttribute('src')).toBe('/assets/emotes/default/smile.png');

        document.querySelectorAll('.chat-emote-tab')[1].click();
        expect(document.querySelector('.chat-emote-item img').getAttribute('src')).toBe('/assets/emotes/line/hello.webp');

        document.querySelector('.chat-emote-item').click();
        await flushPromises();

        expect(gameManager.sendEmoteMessage).toHaveBeenCalledWith('line/hello.webp');
    });

    test('renders emote message image fallback when image load fails', () => {
        const { renderGameLog, replaceInMatchChatHistory } = require('../src/ui/logPanel.js');
        openChatPanel(renderGameLog);

        replaceInMatchChatHistory([
            {
                messageId: 'm1',
                senderType: 'player',
                senderId: 'player-2',
                senderName: '玩家2',
                messageType: 'emote',
                content: 'smile',
                emoteId: 'default/smile.png',
                emoteName: 'smile',
                emoteUrl: '/assets/emotes/default/smile.png',
                sentAtUtc: '2026-07-07T00:00:00Z'
            }
        ]);

        const image = document.querySelector('.chat-message-emote-img');
        expect(image).not.toBeNull();
        image.onerror();

        const fallback = document.querySelector('.chat-message-emote-fallback');
        expect(fallback).not.toBeNull();
        expect(fallback.textContent).toContain('smile');
    });

    test('keeps text chat rendered as safe text after emote UI is introduced', () => {
        const { renderGameLog, replaceInMatchChatHistory } = require('../src/ui/logPanel.js');
        openChatPanel(renderGameLog);

        replaceInMatchChatHistory([
            {
                messageId: 'm2',
                senderType: 'player',
                senderId: 'player-2',
                senderName: '玩家2',
                messageType: 'text',
                content: '<img src=x onerror=alert(1)>',
                sentAtUtc: '2026-07-07T00:00:00Z'
            }
        ]);

        const text = document.querySelector('.chat-message-text');
        expect(text.textContent).toBe('<img src=x onerror=alert(1)>');
        expect(text.querySelector('img')).toBeNull();
    });
});
