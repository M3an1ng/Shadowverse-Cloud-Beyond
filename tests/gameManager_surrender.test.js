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

const {
    gameState,
    initializeGame,
    setSignalRConnection,
    surrenderGame
} = require('../src/gameManager.js');
require('../src/gameManager.js').bindGameView(require('../src/ui.js'));


describe('gameManager surrender', () => {
    beforeEach(() => {
        initializeGame('game-1', 'player-1');
        gameState.phase = 'Main';
        gameState.gameOver = false;
    });

    test('submits Surrender action through SignalR', async () => {
        const connection = {
            state: 'Connected',
            invoke: jest.fn().mockResolvedValue(undefined)
        };
        setSignalRConnection(connection);

        const result = await surrenderGame();

        expect(result).toEqual({ ok: true });
        expect(connection.invoke).toHaveBeenCalledTimes(1);
        expect(connection.invoke.mock.calls[0][0]).toBe('Surrender');
        expect(typeof connection.invoke.mock.calls[0][1]).toBe('string');
        expect(connection.invoke.mock.calls[0][1].length).toBeGreaterThan(0);
    });

    test('rejects surrender when connection is disconnected', async () => {
        setSignalRConnection({ state: 'Disconnected', invoke: jest.fn() });

        const result = await surrenderGame();

        expect(result.ok).toBe(false);
        expect(result.error).toContain('连接已断开');
    });
});
