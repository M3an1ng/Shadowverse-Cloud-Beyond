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

const { gameState, selectAttackTarget, setSignalRConnection } = require('../src/gameManager.js');

describe('gameManager IgnoreWard attack targeting', () => {
    let invoke;

    beforeEach(() => {
        invoke = jest.fn().mockResolvedValue(undefined);
        setSignalRConnection({ state: 'Connected', invoke });
        global.alert = jest.fn();

        gameState.players = [
            {
                id: 'player-1',
                hand: [],
                field: [
                    {
                        uid: 'attacker-1',
                        InstanceId: 'attacker-1',
                        ownerId: 'player-1',
                        zone: 'Field',
                        type: 'Follower',
                        name: 'Ignore Ward Attacker',
                        canAttack: true,
                        zoneStayTurn: 1,
                        keywords: ['IgnoreWard']
                    }
                ],
                emblem: [],
                leaderCard: {
                    uid: 'leader-1',
                    InstanceId: 'leader-1',
                    ownerId: 'player-1',
                    zone: 'Leader',
                    type: 'Leader',
                    name: 'Player Leader'
                }
            },
            {
                id: 'player-2',
                hand: [],
                field: [
                    {
                        uid: 'ward-1',
                        InstanceId: 'ward-1',
                        ownerId: 'player-2',
                        zone: 'Field',
                        type: 'Follower',
                        name: 'Ward Defender',
                        keywords: ['Ward']
                    },
                    {
                        uid: 'target-1',
                        InstanceId: 'target-1',
                        ownerId: 'player-2',
                        zone: 'Field',
                        type: 'Follower',
                        name: 'Normal Defender',
                        keywords: []
                    }
                ],
                emblem: [],
                leaderCard: {
                    uid: 'leader-2',
                    InstanceId: 'leader-2',
                    ownerId: 'player-2',
                    zone: 'Leader',
                    type: 'Leader',
                    name: 'Opponent Leader'
                }
            }
        ];
        gameState.currentPlayerId = 'player-1';
        gameState.localPlayerId = 'player-1';
        gameState.selectedAttackerUid = 'attacker-1';
        gameState.isSpectator = false;
    });

    afterEach(() => {
        setSignalRConnection(null);
        delete global.alert;
        gameState.players = [];
        gameState.selectedAttackerUid = null;
    });

    test('allows an IgnoreWard attacker to attack a non-Ward follower while Ward exists', async () => {
        await selectAttackTarget('target-1');

        expect(global.alert).not.toHaveBeenCalledWith('必须先攻击带有【守护】的随从！');
        expect(invoke).toHaveBeenCalledWith('PerformAttack', {
            AttackerUid: 'attacker-1',
            TargetUid: 'target-1'
        });
    });
});
