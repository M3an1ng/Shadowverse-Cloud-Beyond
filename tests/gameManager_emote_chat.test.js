jest.mock('../src/ui.js', () => ({
    renderGame: jest.fn(),
    renderGameLog: jest.fn(),
    showDamageNumber: jest.fn(),
    showHealNumber: jest.fn(),
    updatePpDisplay: jest.fn(),
    updateEpDisplay: jest.fn(),
    updateSepDisplay: jest.fn(),
    updatePpBoostDisplay: jest.fn(),
    updateDeckCountDisplay: jest.fn(),
    updateGraveyardCountDisplay: jest.fn(),
    updateBloodPoolDisplay: jest.fn(),
    updateComboDisplay: jest.fn(),
    updateRallyDisplay: jest.fn(),
    updateHealthDisplay: jest.fn(),
    updateUnitCombatStats: jest.fn(),
    clearSelectionHighlights: jest.fn(),
    resetMulliganSelection: jest.fn(),
    updateHandDisplay: jest.fn(),
    refreshHandPlayabilityVisuals: jest.fn(),
    updateUnitDisplay: jest.fn(),
    updateFieldDisplay: jest.fn(),
    highlightAttacker: jest.fn(),
    highlightTargets: jest.fn(),
    showMulliganInterface: jest.fn(),
    updateMulliganStatus: jest.fn(),
    refreshBattleCardPreviewIfCurrent: jest.fn(),
    renderHighlights: jest.fn(),
    updateActButtonState: jest.fn(),
    updateCardActionButtons: jest.fn(),
    updateEmblemZone: jest.fn(),
    showTurnStartBanner: jest.fn(),
    updateTurnTimerDisplay: jest.fn(),
    refreshCardInstanceUi: jest.fn(),
    refreshCardActionControls: jest.fn(),
    showPlayerChoiceUI: jest.fn(),
    showRevealOverlay: jest.fn(),
    showFusionAnimation: jest.fn(),
    showAbilityTriggerToast: jest.fn(),
    replaceInMatchChatHistory: jest.fn(),
    replaceInMatchCardHistory: jest.fn(),
    appendInMatchChatMessage: jest.fn(),
    clearInMatchChatUiState: jest.fn(),
    appendInMatchUsedCard: jest.fn(),
    appendInMatchDestroyedCard: jest.fn(),
    clearInMatchUsedCardUiState: jest.fn(),
    cancelCommittedHandPlayVisual: jest.fn()
}));

jest.mock('../src/userSession.js', () => ({
    getUserSession: jest.fn(() => null)
}));

const ui = require('../src/ui.js');
const {
    gameState,
    handleChatHistory,
    handleChatMessage,
    initializeGame,
    sendEmoteMessage,
    setSignalRConnection
} = require('../src/gameManager.js');

describe('gameManager emote chat', () => {
    beforeEach(() => {
        initializeGame('game-1', 'player-1');
        gameState.chatMessages = [];
        jest.clearAllMocks();
    });

    test('sendEmoteMessage submits only the emote id through SendChatMessage', async () => {
        const connection = {
            state: 'Connected',
            invoke: jest.fn().mockResolvedValue(undefined)
        };
        setSignalRConnection(connection);

        const result = await sendEmoteMessage('default/smile.png');

        expect(result).toEqual({ ok: true });
        expect(connection.invoke).toHaveBeenCalledWith('SendChatMessage', expect.objectContaining({
            MessageType: 'emote',
            EmoteId: 'default/smile.png'
        }));
        const dto = connection.invoke.mock.calls[0][1];
        expect(dto.EmoteUrl).toBeUndefined();
        expect(dto.Content).toBeUndefined();
    });

    test('handleChatMessage preserves live emote fields', () => {
        handleChatMessage({
            MessageId: 'm1',
            SenderType: 'Player',
            SenderId: 'player-1',
            SenderName: '玩家1',
            MessageType: 'emote',
            Content: 'smile',
            EmoteId: 'default/smile.png',
            EmoteName: 'smile',
            EmoteUrl: '/assets/emotes/default/smile.png',
            SentAtUtc: '2026-07-07T00:00:00Z'
        });

        expect(gameState.chatMessages[0]).toMatchObject({
            messageType: 'emote',
            emoteId: 'default/smile.png',
            emoteName: 'smile',
            emoteUrl: '/assets/emotes/default/smile.png'
        });
        expect(ui.appendInMatchChatMessage).toHaveBeenCalledWith(expect.objectContaining({
            messageType: 'emote',
            emoteId: 'default/smile.png'
        }), true);
    });

    test('handleChatHistory preserves historical emote fields', () => {
        handleChatHistory({
            Messages: [
                {
                    MessageId: 'm1',
                    SenderType: 'Player',
                    SenderId: 'player-2',
                    SenderName: '玩家2',
                    MessageType: 'emote',
                    EmoteId: 'default/smile.png',
                    EmoteName: 'smile',
                    EmoteUrl: '/assets/emotes/default/smile.png',
                    SentAtUtc: '2026-07-07T00:00:00Z'
                }
            ]
        });

        expect(ui.replaceInMatchChatHistory).toHaveBeenCalledWith([
            expect.objectContaining({
                messageType: 'emote',
                emoteId: 'default/smile.png',
                content: 'smile'
            })
        ]);
    });
});
