jest.mock('../src/gameManager.js', () => {
    const actual = jest.requireActual('../src/gameManager.js');
    return {
        ...actual,
        gameState: Object.assign(actual.gameState, {
            localPlayerId: 'player-1',
            currentPlayerId: 'player-1',
            phase: 'Main',
            isSpectator: false,
            players: []
        }),
        playCard: jest.fn(),
        findCardInstanceByUid: jest.fn(),
        getCurrentPlayerId: jest.fn(() => 'player-1'),
        getPlayerState: jest.fn(),
        initiateAttack: jest.fn(attackerUid => {
            const { gameState } = require('../src/gameManager.js');
            gameState.selectedAttackerUid = attackerUid;
        }),
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
    };
});

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
    ensureEnumLocalizations: jest.fn(() => Promise.resolve()),
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
    findLogCardInstanceByUid: jest.fn(() => null),
    buildMatchHistoryPreviewCard: jest.fn(() => null)
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

let gameManager;
let ui;

function setupAttackDom() {
    document.body.innerHTML = `
        <div id="game-page" class="page active">
            <div id="game-container">
                <div id="turn-info"></div>
                <button id="end-turn-button"><span class="end-turn-countdown"></span><span class="end-turn-indicator-light"></span></button>
                <button id="confirm-mulligan-button"></button>
                <button id="reconnect-button"></button>
                <div id="battle-card-preview"></div>
                <div id="player1-avatar"></div>
                <div id="player2-avatar"></div>
                <div id="player2-hand" class="hand opponent-hand"></div>
                <div id="battlefield">
                    <div id="player2-field" class="field opponent-field"></div>
                    <div id="player1-field" class="field player-field"></div>
                </div>
                <div id="player1-hand" class="hand player-hand"></div>
                <div id="player1-hp"></div>
                <div id="player1-pp-bar"></div>
                <div id="player1-ep-dots"></div>
                <div id="player1-sep-dots"></div>
                <div id="player1-deck-count"></div>
                <div id="player1-grave-count"></div>
                <div id="player2-hp"></div>
                <div id="player2-pp-bar"></div>
                <div id="player2-ep-dots"></div>
                <div id="player2-sep-dots"></div>
                <div id="player2-deck-count"></div>
                <div id="player2-grave-count"></div>
            </div>
        </div>
    `;
}

function rect(left, top, width, height) {
    return {
        left,
        top,
        width,
        height,
        right: left + width,
        bottom: top + height
    };
}

function attachRect(element, nextRect) {
    element.getBoundingClientRect = () => rect(nextRect.left, nextRect.top, nextRect.width, nextRect.height);
}

function createPointerEvent(type, { clientX = 0, clientY = 0, button = 0, pointerId = 1 } = {}) {
    const event = new window.MouseEvent(type, {
        bubbles: true,
        cancelable: true,
        clientX,
        clientY,
        button
    });
    Object.defineProperty(event, 'pointerId', { value: pointerId });
    return event;
}

function setupElementsFromPoint(mapping) {
    document.elementFromPoint = jest.fn((x, y) => {
        const key = `${Math.round(x)},${Math.round(y)}`;
        const values = mapping[key] ?? [];
        return values[0] ?? null;
    });
    document.elementsFromPoint = jest.fn((x, y) => {
        const key = `${Math.round(x)},${Math.round(y)}`;
        return mapping[key] ?? [];
    });
}

function createFieldCardElement(uid) {
    const card = document.createElement('div');
    card.className = 'card field-card';
    card.dataset.uid = uid;
    card.innerHTML = `<div class="card-art"></div><div class="name">Card ${uid}</div>`;
    return card;
}

describe('enemy hand leader attack zone', () => {
    beforeEach(() => {
        jest.resetModules();
        jest.clearAllMocks();
        setupAttackDom();
        window.matchMedia = jest.fn().mockImplementation(query => ({
            matches: false,
            media: query,
            addListener: jest.fn(),
            removeListener: jest.fn(),
            addEventListener: jest.fn(),
            removeEventListener: jest.fn(),
            dispatchEvent: jest.fn()
        }));

        gameManager = require('../src/gameManager.js');
        gameManager.gameState.localPlayerId = 'player-1';
        gameManager.gameState.currentPlayerId = 'player-1';
        gameManager.gameState.phase = 'Main';
        gameManager.gameState.isSpectator = false;
        gameManager.gameState.selectedAttackerUid = null;
        gameManager.gameState.players = [
            { id: 'player-1', leaderCard: { uid: 'leader-1', InstanceId: 'leader-1' }, hand: [], field: [] },
            { id: 'player-2', leaderCard: { uid: 'leader-2', InstanceId: 'leader-2' }, hand: [], field: [] }
        ];
        gameManager.gameState.activePlayer = gameManager.gameState.players[0];
        gameManager.gameState.opponentPlayer = gameManager.gameState.players[1];
        gameManager.getPlayerState.mockImplementation(playerId =>
            gameManager.gameState.players.find(player => player.id === playerId) ?? null
        );

        gameManager.findCardInstanceByUid.mockImplementation(uid => {
            if (uid === 'attacker-1') {
                return {
                    uid: 'attacker-1',
                    zone: 'Field',
                    ownerId: 'player-1',
                    type: 'Follower',
                    name: 'Attacker',
                    canAttack: true
                };
            }
            if (uid === 'leader-1') {
                return {
                    uid: 'leader-1',
                    zone: 'Leader',
                    ownerId: 'player-1',
                    type: 'Leader',
                    name: 'Leader 1'
                };
            }
            if (uid === 'leader-2') {
                return {
                    uid: 'leader-2',
                    zone: 'Leader',
                    ownerId: 'player-2',
                    type: 'Leader',
                    name: 'Leader 2'
                };
            }
            return null;
        });

        setupElementsFromPoint({});
        ui = require('../src/ui.js');
    });

    afterEach(() => {
        document.body.innerHTML = '';
        jest.useRealTimers();
    });

    function startAttackDrag(attacker, pointerY = 360, mapping = {}) {
        document.getElementById('player2-hand').getBoundingClientRect = () => rect(160, 60, 520, 110);
        document.getElementById('battlefield').getBoundingClientRect = () => rect(120, 160, 680, 320);
        attachRect(attacker, { left: 200, top: 420, width: 100, height: 140 });
        setupElementsFromPoint(mapping);
        attacker.dispatchEvent(createPointerEvent('pointerdown', { clientX: 220, clientY: 440 }));
        window.dispatchEvent(createPointerEvent('pointermove', { clientX: 220, clientY: pointerY }));
    }

    test('enemy hand leader zone exists only while an attack drag is active', () => {
        const attacker = createFieldCardElement('attacker-1');
        document.getElementById('player1-field').appendChild(attacker);
        expect(document.querySelector('.attack-drag-leader-zone')).toBeNull();
        startAttackDrag(attacker, 360, {
            '220,360': [document.getElementById('player2-hand')]
        });

        expect(document.querySelector('.attack-drag-leader-zone')).not.toBeNull();

        window.dispatchEvent(createPointerEvent('pointerup', { clientX: 220, clientY: 360 }));
        expect(document.querySelector('.attack-drag-leader-zone')).toBeNull();
    });

    test('zone uses battlefield width while masking the opponent hand area', () => {
        const attacker = createFieldCardElement('attacker-1');
        document.getElementById('player1-field').appendChild(attacker);
        const opponentAvatar = document.getElementById('player2-avatar');
        opponentAvatar.classList.add('targetable');

        startAttackDrag(attacker, 110, {
            '220,110': [document.getElementById('player2-hand')]
        });

        const zone = document.querySelector('.attack-drag-leader-zone');
        expect(zone?.classList.contains('is-valid')).toBe(true);
        expect(zone?.classList.contains('is-active')).toBe(true);
        expect(zone?.parentElement?.id).toBe('game-container');
        expect(zone?.style.position).toBe('fixed');
        expect(zone?.style.left).toBe('120px');
        expect(zone?.style.top).toBe('60px');
        expect(zone?.style.width).toBe('680px');
        expect(zone?.style.height).toBe('110px');
        expect(zone?.querySelector('.attack-drag-leader-zone-name')).toBeNull();
        expect(zone?.querySelector('.attack-drag-leader-zone-avatar')).not.toBeNull();

        window.dispatchEvent(createPointerEvent('pointerup', { clientX: 20, clientY: 20 }));
        expect(gameManager.selectAttackTarget).not.toHaveBeenCalled();
    });

    test('zone stays hidden when the opponent leader is not a legal target', () => {
        const attacker = createFieldCardElement('attacker-1');
        document.getElementById('player1-field').appendChild(attacker);

        startAttackDrag(attacker, 110, {
            '220,110': [document.getElementById('player2-hand')]
        });

        expect(document.querySelector('.attack-drag-leader-zone')?.style.display).toBe('none');

        window.dispatchEvent(createPointerEvent('pointerup', { clientX: 220, clientY: 110 }));
        expect(gameManager.selectAttackTarget).not.toHaveBeenCalled();
        expect(document.querySelector('.attack-drag-leader-zone')).toBeNull();
    });

    test('cleanup removes the enemy hand leader zone on cancel', () => {
        const attacker = createFieldCardElement('attacker-1');
        document.getElementById('player1-field').appendChild(attacker);

        startAttackDrag(attacker, 110, {
            '220,110': [document.getElementById('player2-hand')]
        });

        expect(document.querySelector('.attack-drag-leader-zone')).not.toBeNull();

        window.dispatchEvent(createPointerEvent('pointercancel', { clientX: 220, clientY: 110 }));
        expect(document.querySelector('.attack-drag-leader-zone')).toBeNull();
    });

    test('cleanupGame removes the enemy hand leader zone after drag activation', () => {
        const attacker = createFieldCardElement('attacker-1');
        document.getElementById('player1-field').appendChild(attacker);

        startAttackDrag(attacker, 110, {
            '220,110': [document.getElementById('player2-hand')]
        });

        expect(document.querySelector('.attack-drag-leader-zone')).not.toBeNull();

        document.dispatchEvent(new Event('cleanupGame'));
        expect(document.querySelector('.attack-drag-leader-zone')).toBeNull();
    });

    test('releasing inside a valid enemy hand leader zone targets the opponent leader and marks confirmation state', () => {
        jest.useFakeTimers();
        const attacker = createFieldCardElement('attacker-1');
        document.getElementById('player1-field').appendChild(attacker);
        document.getElementById('player2-avatar').classList.add('targetable');

        startAttackDrag(attacker, 110, {
            '220,110': [document.getElementById('player2-hand')]
        });

        window.dispatchEvent(createPointerEvent('pointerup', { clientX: 220, clientY: 110 }));

        expect(gameManager.selectAttackTarget).toHaveBeenCalledWith('leader-2');
        expect(document.querySelector('.attack-drag-leader-zone')?.classList.contains('is-confirmed')).toBe(true);
        jest.runOnlyPendingTimers();
        expect(document.querySelector('.attack-drag-leader-zone')).toBeNull();
    });
});
