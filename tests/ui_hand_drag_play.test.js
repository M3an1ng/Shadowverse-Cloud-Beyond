jest.mock('../src/gameManager.js', () => ({
    gameState: {
        localPlayerId: 'player-1',
        currentPlayerId: 'player-1',
        phase: 'Main',
        isSpectator: false,
        players: []
    },
    playCard: jest.fn(),
    findCardInstanceByUid: jest.fn(),
    getCurrentPlayerId: jest.fn(() => 'player-1'),
    getPlayerState: jest.fn(),
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

function setupDom() {
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

function createHandCardElement(uid, { withUseButton = false } = {}) {
    const card = document.createElement('div');
    card.className = 'card hand-card playable';
    card.dataset.uid = uid;
    card.innerHTML = `
        <div class="card-art"></div>
        <div class="name">Card ${uid}</div>
    `;
    if (withUseButton) {
        const button = document.createElement('button');
        button.className = 'use-button';
        button.dataset.useUid = uid;
        button.textContent = '使用';
        card.appendChild(button);
    }
    return card;
}

function setupElementsFromPoint(mapping) {
    document.elementsFromPoint = jest.fn((x, y) => {
        const key = `${x},${y}`;
        return mapping[key] ?? [];
    });
    document.elementFromPoint = jest.fn((x, y) => {
        const values = mapping[`${x},${y}`] ?? [];
        return values[0] ?? null;
    });
}

function buildHandCard(overrides = {}) {
    return {
        uid: 'hand-card-1',
        zone: 'Hand',
        ownerId: 'player-1',
        type: 'Spell',
        name: 'Test Card',
        Name: 'Test Card',
        ...overrides
    };
}

describe('hand drag play interaction', () => {
    let ui;
    let handCard;
    let opponentFollower;
    let directTargetCard;
    let handCardInstance;

    beforeEach(() => {
        jest.resetModules();
        jest.clearAllMocks();
        setupDom();
        gameManager = require('../src/gameManager.js');

        window.matchMedia = jest.fn().mockImplementation(query => ({
            matches: false,
            media: query,
            addListener: jest.fn(),
            removeListener: jest.fn(),
            addEventListener: jest.fn(),
            removeEventListener: jest.fn(),
            dispatchEvent: jest.fn()
        }));

        gameManager.gameState.localPlayerId = 'player-1';
        gameManager.gameState.currentPlayerId = 'player-1';
        gameManager.gameState.phase = 'Main';
        gameManager.gameState.isSpectator = false;
        gameManager.gameState.selectedCardUid = null;
        gameManager.gameState.players = [
            { id: 'player-1', leaderCard: { uid: 'leader-1', InstanceId: 'leader-1' }, hand: [], field: [] },
            { id: 'player-2', leaderCard: { uid: 'leader-2', InstanceId: 'leader-2' }, hand: [], field: [] }
        ];
        gameManager.gameState.activePlayer = gameManager.gameState.players[0];
        gameManager.gameState.opponentPlayer = gameManager.gameState.players[1];

        gameManager.getPlayerState.mockImplementation(playerId =>
            gameManager.gameState.players.find(player => player.id === playerId) ?? null
        );

        handCardInstance = buildHandCard();
        gameManager.findCardInstanceByUid.mockImplementation(uid => {
            if (uid === 'hand-card-1') return handCardInstance;
            if (uid === 'target-card-1') return { uid: 'target-card-1', zone: 'Field', ownerId: 'player-2', type: 'Follower', name: 'Target' };
            if (uid === 'leader-1') return { uid: 'leader-1', zone: 'Leader', ownerId: 'player-1', type: 'Leader', name: 'Leader 1' };
            if (uid === 'leader-2') return { uid: 'leader-2', zone: 'Leader', ownerId: 'player-2', type: 'Leader', name: 'Leader 2' };
            return null;
        });

        gameManager.getPlayabilityForCard.mockImplementation(uid => {
            if (uid !== 'hand-card-1') return null;
            return {
                isPlayable: true,
                requiresTargetSelection: false,
                hasValidTargets: false,
                canPlayWithoutTarget: true,
                validTargetIds: []
            };
        });

        handCard = createHandCardElement('hand-card-1', { withUseButton: true });
        document.getElementById('player1-hand').appendChild(handCard);

        directTargetCard = document.createElement('div');
        directTargetCard.className = 'card field-card';
        directTargetCard.dataset.uid = 'target-card-1';
        document.getElementById('player2-field').appendChild(directTargetCard);

        opponentFollower = directTargetCard;

        attachRect(handCard, { left: 20, top: 420, width: 100, height: 140 });
        attachRect(document.getElementById('battlefield'), { left: 180, top: 160, width: 520, height: 320 });
        attachRect(document.getElementById('player1-field'), { left: 180, top: 320, width: 520, height: 160 });
        attachRect(document.getElementById('player2-field'), { left: 180, top: 160, width: 520, height: 160 });
        attachRect(document.getElementById('player1-avatar'), { left: 40, top: 520, width: 80, height: 80 });
        attachRect(document.getElementById('player2-avatar'), { left: 760, top: 80, width: 80, height: 80 });
        attachRect(opponentFollower, { left: 260, top: 200, width: 100, height: 140 });

        setupElementsFromPoint({
            '260,260': [document.getElementById('battlefield'), document.getElementById('player1-field')],
            '300,240': [opponentFollower, document.getElementById('player2-field'), document.getElementById('battlefield')],
            '780,120': [document.getElementById('player2-avatar'), document.getElementById('battlefield')],
            '40,40': []
        });

        ui = require('../src/ui.js');
    });

    test('clicking a hand card previews without submitting play', () => {
        handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
        window.dispatchEvent(createPointerEvent('pointerup', { clientX: 34, clientY: 434 }));
        handCard.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true, clientX: 34, clientY: 434 }));

        expect(gameManager.playCard).not.toHaveBeenCalled();
        expect(gameManager.gameState.selectedCardUid).toBe('hand-card-1');
    });

    test('dragging to a valid play area submits exactly once and suppresses release click', () => {
        handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
        window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 260 }));

        expect(document.querySelector('.hand-drag-ghost')).not.toBeNull();
        expect(document.querySelector('.hand-play-drop-lane')?.parentElement?.id).toBe('game-container');
        expect(handCard.classList.contains('hand-drag-origin')).toBe(true);

        window.dispatchEvent(createPointerEvent('pointerup', { clientX: 260, clientY: 260 }));
        handCard.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true, clientX: 260, clientY: 260 }));

        expect(gameManager.playCard).toHaveBeenCalledTimes(1);
        expect(gameManager.playCard).toHaveBeenCalledWith('hand-card-1');
        expect(document.querySelector('.hand-drag-ghost')?.classList.contains('is-committed')).toBe(true);
        expect(handCard.classList.contains('hand-play-committed-source')).toBe(true);
    });

    test('drag ghost starts from the source card position before the next animation frame update', () => {
        const originalRaf = window.requestAnimationFrame;
        window.requestAnimationFrame = jest.fn(() => 1);
        try {
            handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
            window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 260 }));

            const ghost = document.querySelector('.hand-drag-ghost');
            expect(ghost).not.toBeNull();
            expect(ghost.style.transform).toContain('translate3d(20px, 420px, 0)');
        } finally {
            window.requestAnimationFrame = originalRaf;
        }
    });

    test('dragging to a valid direct target submits target uid', () => {
        gameManager.getPlayabilityForCard.mockImplementation(uid => {
            if (uid !== 'hand-card-1') return null;
            return {
                isPlayable: true,
                requiresTargetSelection: true,
                hasValidTargets: true,
                canPlayWithoutTarget: false,
                validTargetIds: ['target-card-1']
            };
        });

        handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
        window.dispatchEvent(createPointerEvent('pointermove', { clientX: 300, clientY: 240 }));
        window.dispatchEvent(createPointerEvent('pointerup', { clientX: 300, clientY: 240 }));

        expect(gameManager.playCard).toHaveBeenCalledTimes(1);
        expect(gameManager.playCard).toHaveBeenCalledWith('hand-card-1', 'target-card-1');
    });

    test('invalid drop snaps back without submitting play', () => {
        jest.useFakeTimers();
        try {
            handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
            window.dispatchEvent(createPointerEvent('pointermove', { clientX: 40, clientY: 40 }));

            const ghost = document.querySelector('.hand-drag-ghost');
            expect(ghost).not.toBeNull();

            window.dispatchEvent(createPointerEvent('pointerup', { clientX: 40, clientY: 40 }));

            expect(gameManager.playCard).not.toHaveBeenCalled();
            expect(document.querySelector('.hand-drag-ghost')).not.toBeNull();

            jest.advanceTimersByTime(260);
            expect(document.querySelector('.hand-drag-ghost')).toBeNull();
        } finally {
            jest.useRealTimers();
        }
    });

    test('committed hand play visual clears when the card leaves the local hand', () => {
        handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
        window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 260 }));
        window.dispatchEvent(createPointerEvent('pointerup', { clientX: 260, clientY: 260 }));

        expect(document.querySelector('.hand-drag-ghost')).not.toBeNull();
        expect(handCard.classList.contains('hand-play-committed-source')).toBe(true);

        ui.updateHandDisplay('player-1', []);

        expect(document.querySelector('.hand-drag-ghost')).toBeNull();
        expect(document.querySelector('.hand-card[data-uid="hand-card-1"]')?.classList.contains('hand-play-committed-source')).toBe(false);
    });

    test('cancelCommittedHandPlayVisual snaps the committed card back to hand', () => {
        jest.useFakeTimers();
        try {
            handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
            window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 260 }));
            window.dispatchEvent(createPointerEvent('pointerup', { clientX: 260, clientY: 260 }));

            expect(document.querySelector('.hand-drag-ghost')).not.toBeNull();
            expect(handCard.classList.contains('hand-play-committed-source')).toBe(true);

            ui.cancelCommittedHandPlayVisual('hand-card-1');

            const ghost = document.querySelector('.hand-drag-ghost');
            expect(ghost).not.toBeNull();
            expect(ghost?.classList.contains('is-snapping-back')).toBe(true);
            expect(handCard.classList.contains('hand-play-committed-source')).toBe(true);

            jest.advanceTimersByTime(260);

            expect(document.querySelector('.hand-drag-ghost')).toBeNull();
            expect(handCard.classList.contains('hand-play-committed-source')).toBe(false);
        } finally {
            jest.useRealTimers();
        }
    });

    test('cancelling player choice without SourceCardUid still snaps the committed card back', () => {
        jest.useFakeTimers();
        try {
            handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
            window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 260 }));
            window.dispatchEvent(createPointerEvent('pointerup', { clientX: 260, clientY: 260 }));
            gameManager.gameState.players[0].hand = [handCardInstance];

            ui.showPlayerChoiceUI({
                PendingChoiceId: 'choice-1',
                SourceAbilityRuntimeId: 'ability-1',
                SelectableTargetIds: ['target-card-1'],
                SelectableCardInstances: [],
                MinSelectionCount: 1,
                MaxSelectionCount: 1,
                IsCancellable: true,
                Message: '请选择目标'
            });

            const cancelButton = document.getElementById('choice-cancel-btn');
            expect(cancelButton).not.toBeNull();

            cancelButton.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true }));

            const ghost = document.querySelector('.hand-drag-ghost');
            expect(ghost).not.toBeNull();
            expect(ghost?.classList.contains('is-snapping-back')).toBe(true);
            expect(gameManager.cancelPlayerChoice).toHaveBeenCalledWith('choice-1', 'ability-1');

            jest.advanceTimersByTime(260);

            expect(document.querySelector('.hand-drag-ghost')).toBeNull();
            expect(handCard.classList.contains('hand-play-committed-source')).toBe(false);
        } finally {
            jest.useRealTimers();
        }
    });

    test('targeted card dropped on cast area falls back to untargeted play request', () => {
        gameManager.getPlayabilityForCard.mockImplementation(uid => {
            if (uid !== 'hand-card-1') return null;
            return {
                isPlayable: true,
                requiresTargetSelection: true,
                hasValidTargets: true,
                canPlayWithoutTarget: false,
                validTargetIds: ['leader-2']
            };
        });

        handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
        window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 260 }));
        window.dispatchEvent(createPointerEvent('pointerup', { clientX: 260, clientY: 260 }));

        expect(gameManager.playCard).toHaveBeenCalledTimes(1);
        expect(gameManager.playCard).toHaveBeenCalledWith('hand-card-1');
    });

    test('existing use button remains a fallback path', () => {
        const useButton = handCard.querySelector('.use-button');
        useButton.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true }));

        expect(gameManager.playCard).toHaveBeenCalledTimes(1);
        expect(gameManager.playCard).toHaveBeenCalledWith('hand-card-1');
    });

    test('reduced motion marks the drag ghost accordingly', () => {
        window.matchMedia = jest.fn().mockImplementation(query => ({
            matches: query === '(prefers-reduced-motion: reduce)',
            media: query,
            addListener: jest.fn(),
            removeListener: jest.fn(),
            addEventListener: jest.fn(),
            removeEventListener: jest.fn(),
            dispatchEvent: jest.fn()
        }));

        handCard.dispatchEvent(createPointerEvent('pointerdown', { clientX: 30, clientY: 430 }));
        window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 260 }));

        expect(document.querySelector('.hand-drag-ghost')?.classList.contains('is-reduced-motion')).toBe(true);
    });

    test('leaving mulligan clears mulligan state classes from local hand cards', () => {
        document.getElementById('game-container')?.classList.add('mulligan-active');
        handCard.classList.add('mulligan-selectable', 'selected-for-mulligan');
        gameManager.gameState.phase = 'Main';
        gameManager.gameState.players[0].hand = [handCardInstance];

        ui.renderGame(gameManager.gameState);

        expect(document.getElementById('game-container')?.classList.contains('mulligan-active')).toBe(false);
        expect(handCard.classList.contains('mulligan-selectable')).toBe(false);
        expect(handCard.classList.contains('selected-for-mulligan')).toBe(false);
    });
});
