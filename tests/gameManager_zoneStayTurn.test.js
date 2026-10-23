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

const { gameState, handleCardInstanceUpdated } = require('../src/gameManager.js');
require('../src/gameManager.js').bindGameView(require('../src/ui.js'));


describe('gameManager zoneStayTurn syncing', () => {
    beforeEach(() => {
        gameState.players = [
            {
                id: 'player-1',
                hand: [],
                field: [
                    {
                        uid: 'card-1',
                        InstanceId: 'card-1',
                        ownerId: 'player-1',
                        zone: 'Field',
                        name: 'Reanimated Follower',
                        canAttack: true,
                        currentAttack: 3,
                        currentHealth: 2,
                        isEvolved: false,
                        isSuperEvolved: false,
                        zoneStayTurn: 1,
                        keywords: []
                    }
                ],
                emblem: [],
                leaderCard: null
            }
        ];
    });

    test('preserves zoneStayTurn when partial CardInstanceUpdated payload omits it', () => {
        handleCardInstanceUpdated({
            UpdatedCards: [
                {
                    InstanceId: 'card-1',
                    CardId: 1001,
                    Name: 'Reanimated Follower',
                    CurrentAttack: 3,
                    CurrentHealth: 2
                }
            ]
        });

        expect(gameState.players[0].field[0].zoneStayTurn).toBe(1);
    });
});
