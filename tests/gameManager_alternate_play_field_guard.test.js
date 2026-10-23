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
    handleHandPlayabilityUpdate,
    playCard
} = require('../src/gameManager.js');

describe('gameManager alternate play field guard', () => {
    let connection;

    beforeEach(() => {
        global.alert = jest.fn();
        jest.spyOn(console, 'warn').mockImplementation(() => {});
        connection = {
            state: 'Connected',
            invoke: jest.fn().mockResolvedValue(undefined)
        };
        initializeGame('game-1', 'player-1');
        setSignalRConnection(connection);
        gameState.currentPlayerId = 'player-1';
        gameState.phase = 'Main';
        gameState.players = [
            {
                id: 'player-1',
                pp: 2,
                ppBoostArmed: false,
                hand: [buildHandCard()],
                field: Array.from({ length: 5 }, (_, index) => ({
                    uid: `field-${index}`,
                    ownerId: 'player-1',
                    zone: 'Field',
                    type: 'Follower'
                }))
            },
            {
                id: 'player-2',
                hand: [],
                field: []
            }
        ];
    });

    afterEach(() => {
        console.warn.mockRestore();
    });

    test('allows Accelerate on a full field because selected play is a spell', async () => {
        pushPlayability('Accelerate');

        await playCard('hand-card-1');

        expect(connection.invoke).toHaveBeenCalledTimes(1);
        expect(connection.invoke.mock.calls[0][0]).toBe('PlayCard');
        expect(connection.invoke.mock.calls[0][1]).toMatchObject({
            CardUid: 'hand-card-1',
            PlayMode: 'Accelerate',
            PaidCost: 2
        });
        expect(global.alert).not.toHaveBeenCalledWith('战场已满！');
    });

    test('blocks Crystalize on a full field because selected play is an amulet', async () => {
        pushPlayability('Crystalize');

        await playCard('hand-card-1');

        expect(connection.invoke).not.toHaveBeenCalled();
        expect(global.alert).toHaveBeenCalledWith('战场已满！');
    });
});

function buildHandCard() {
    return {
        uid: 'hand-card-1',
        InstanceId: 'hand-card-1',
        ownerId: 'player-1',
        zone: 'Hand',
        type: 'Follower',
        name: 'Alternate Card',
        cost: 5,
        currentCost: 5
    };
}

function pushPlayability(selectedPlayMode) {
    handleHandPlayabilityUpdate({
        PlayerId: 'player-1',
        TimestampUtc: new Date().toISOString(),
        PlayabilityResults: {
            'hand-card-1': {
                IsPlayable: true,
                RequiresTargetSelection: false,
                HasValidTargets: true,
                CanPlayWithoutTarget: true,
                FailureReason: '',
                ValidTargetIds: [],
                PlayModes: [
                    {
                        Mode: selectedPlayMode,
                        EffectiveCost: 2,
                        IsAvailable: true
                    }
                ],
                SelectedPlayMode: selectedPlayMode,
                SelectedAbilityRuntimeId: 'ability-1'
            }
        }
    });
}
