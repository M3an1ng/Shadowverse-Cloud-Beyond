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
    clearInMatchUsedCardUiState: jest.fn()
}));

jest.mock('../src/userSession.js', () => ({
    getUserSession: jest.fn(() => null)
}));

const {
    gameState,
    initializeGame,
    setSignalRConnection,
    updateFullGameState
} = require('../src/gameManager.js');
require('../src/gameManager.js').bindGameView(require('../src/ui.js'));


describe('gameManager playability resync', () => {
    let connection;

    beforeEach(() => {
        jest.useFakeTimers();
        connection = {
            state: 'Connected',
            invoke: jest.fn().mockResolvedValue(undefined)
        };

        initializeGame('game-1', 'player-1');
        setSignalRConnection(connection);
        gameState.phase = 'Main';
    });

    afterEach(() => {
        jest.useRealTimers();
    });

    test('requests playability refresh after full state hydration when caches are empty', async () => {
        const hydrated = updateFullGameState({
            GameId: 'game-1',
            CurrentPlayerId: 'player-1',
            Phase: 'Main',
            GameOver: false,
            Players: [
                {
                    PlayerId: 'player-1',
                    IsAI: false,
                    CurrentPP: 2,
                    MaxPP: 2,
                    CurrentEP: 0,
                    MaxEP: 0,
                    CurrentSEP: 0,
                    MaxSEP: 0,
                    TurnNumber: 1,
                    DeckCount: 1,
                    CanEvolve: false,
                    CanSuperEvolve: false,
                    HasCompletedMulligan: true,
                    LeaderCard: {
                        InstanceId: 'leader-1',
                        CardId: 900001,
                        Name: 'Leader 1',
                        Cost: 0,
                        Attack: 0,
                        Health: 20,
                        MaxHealth: 20,
                        Type: 'Leader',
                        CurrentHealth: 20
                    },
                    Hand: [
                        {
                            InstanceId: 'card-1',
                            CardId: 1001,
                            Name: 'Test Spell',
                            Cost: 2,
                            Attack: 0,
                            Health: 0,
                            Type: 'Spell',
                            CurrentCost: 2,
                            CurrentAttack: 0,
                            CurrentHealth: 0
                        }
                    ],
                    Field: []
                },
                {
                    PlayerId: 'player-2',
                    IsAI: false,
                    CurrentPP: 2,
                    MaxPP: 2,
                    CurrentEP: 0,
                    MaxEP: 0,
                    CurrentSEP: 0,
                    MaxSEP: 0,
                    TurnNumber: 1,
                    DeckCount: 1,
                    CanEvolve: false,
                    CanSuperEvolve: false,
                    HasCompletedMulligan: true,
                    LeaderCard: {
                        InstanceId: 'leader-2',
                        CardId: 900002,
                        Name: 'Leader 2',
                        Cost: 0,
                        Attack: 0,
                        Health: 20,
                        MaxHealth: 20,
                        Type: 'Leader',
                        CurrentHealth: 20
                    },
                    Hand: [],
                    Field: []
                }
            ]
        });

        expect(hydrated).toBe(true);

        jest.advanceTimersByTime(150);
        await Promise.resolve();

        expect(connection.invoke).toHaveBeenCalledWith('RequestPlayabilityUpdate');
    });
});
