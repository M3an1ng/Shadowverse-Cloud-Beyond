# Enemy Hand Leader Attack Zone Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an enemy-hand leader attack zone during follower attack dragging so dragging into the opponent hand area can target the opposing leader with lightweight visual confirmation.

**Architecture:** Extend the existing attack-drag flow in `src/ui.js` with one new transient overlay element that is positioned from the opponent hand rect and resolves to the opponent leader UID when valid. Keep the implementation inside the current UI module and add one dedicated Jest test file plus CSS states in `src/styles/game.css`.

**Tech Stack:** Vanilla JS UI in `src/ui.js`, CSS in `src/styles/game.css`, Jest DOM-style frontend tests, Vite build.

---

## File Map

**Modify**
- `src/ui.js`
  Attack-drag transient state, overlay creation/cleanup, target resolution extension, and release confirmation timing.
- `src/styles/game.css`
  Enemy-hand leader zone visual states, avatar marker styling, and confirmation pulse animation.

**Create**
- `tests/ui_attack_drag_leader_zone.test.js`
  Focused regression tests for visibility, target resolution, validity, cleanup, and release behavior.

## Task 1: Add Failing Attack-Drag Regression Tests

**Files:**
- Create: `tests/ui_attack_drag_leader_zone.test.js`
- Modify: none
- Test: `tests/ui_attack_drag_leader_zone.test.js`

- [ ] **Step 1: Write the failing test file**

```js
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
}));

function setupAttackDom() {
    document.body.innerHTML = `
        <div id="game-page" class="page active">
            <div id="game-container">
                <div id="battlefield">
                    <div id="player2-field" class="field opponent-field"></div>
                    <div id="player1-field" class="field player-field"></div>
                </div>
                <div id="player2-hand" class="hand opponent-hand"></div>
                <div id="player1-hand" class="hand player-hand"></div>
                <div id="player1-avatar"></div>
                <div id="player2-avatar"></div>
            </div>
        </div>
    `;
}

test('shows enemy hand leader zone only while an attack drag is active', () => {
    const attacker = document.createElement('div');
    attacker.className = 'card field-card';
    attacker.dataset.uid = 'attacker-1';
    document.getElementById('player1-field').appendChild(attacker);
    attacker.getBoundingClientRect = () => ({ left: 200, top: 420, width: 100, height: 140, right: 300, bottom: 560 });
    document.getElementById('player2-hand').getBoundingClientRect = () => ({ left: 160, top: 60, width: 520, height: 110, right: 680, bottom: 170 });

    attacker.dispatchEvent(createPointerEvent('pointerdown', { clientX: 220, clientY: 440 }));
    window.dispatchEvent(createPointerEvent('pointermove', { clientX: 220, clientY: 360 }));

    expect(document.querySelector('.attack-drag-leader-zone')).not.toBeNull();

    window.dispatchEvent(createPointerEvent('pointerup', { clientX: 220, clientY: 360 }));
    expect(document.querySelector('.attack-drag-leader-zone')).toBeNull();
});

test('resolves enemy hand zone to opposing leader uid when leader is valid', () => {
    const attacker = document.createElement('div');
    attacker.className = 'card field-card';
    attacker.dataset.uid = 'attacker-1';
    document.getElementById('player1-field').appendChild(attacker);
    attacker.getBoundingClientRect = () => ({ left: 200, top: 420, width: 100, height: 140, right: 300, bottom: 560 });

    const opponentAvatar = document.getElementById('player2-avatar');
    opponentAvatar.classList.add('targetable');
    document.getElementById('player2-hand').getBoundingClientRect = () => ({ left: 160, top: 60, width: 520, height: 110, right: 680, bottom: 170 });
    document.elementFromPoint = jest.fn(() => document.getElementById('player2-hand'));

    attacker.dispatchEvent(createPointerEvent('pointerdown', { clientX: 220, clientY: 440 }));
    window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 110 }));

    expect(document.querySelector('.attack-drag-leader-zone')?.classList.contains('is-valid')).toBe(true);

    window.dispatchEvent(createPointerEvent('pointerup', { clientX: 260, clientY: 110 }));
    expect(gameManager.selectAttackTarget).toHaveBeenCalledWith('leader-2');
});

test('keeps enemy hand zone invalid when leader is not a legal target', () => {
    const attacker = document.createElement('div');
    attacker.className = 'card field-card';
    attacker.dataset.uid = 'attacker-1';
    document.getElementById('player1-field').appendChild(attacker);
    attacker.getBoundingClientRect = () => ({ left: 200, top: 420, width: 100, height: 140, right: 300, bottom: 560 });

    document.getElementById('player2-hand').getBoundingClientRect = () => ({ left: 160, top: 60, width: 520, height: 110, right: 680, bottom: 170 });
    document.elementFromPoint = jest.fn(() => document.getElementById('player2-hand'));

    attacker.dispatchEvent(createPointerEvent('pointerdown', { clientX: 220, clientY: 440 }));
    window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 110 }));

    expect(document.querySelector('.attack-drag-leader-zone')?.classList.contains('is-invalid')).toBe(true);

    window.dispatchEvent(createPointerEvent('pointerup', { clientX: 260, clientY: 110 }));
    expect(gameManager.selectAttackTarget).not.toHaveBeenCalled();
});
```

- [ ] **Step 2: Run the new test file to verify it fails**

Run: `npx jest tests/ui_attack_drag_leader_zone.test.js --runInBand`

Expected: FAIL because the attack-drag flow does not yet render an enemy-hand leader zone or resolve that region to the opponent leader UID.

- [ ] **Step 3: Expand the test file with concrete DOM expectations**

```js
expect(document.querySelector('.attack-drag-leader-zone')).not.toBeNull();
expect(document.querySelector('.attack-drag-leader-zone')?.classList.contains('is-valid')).toBe(true);
expect(document.querySelector('.attack-drag-line')?.style.transform).toContain('rotate(');
expect(gameManager.selectAttackTarget).toHaveBeenCalledWith('leader-2');
expect(document.querySelector('.attack-drag-leader-zone')).toBeNull();
```

- [ ] **Step 4: Re-run the file and confirm it still fails for the right reason**

Run: `npx jest tests/ui_attack_drag_leader_zone.test.js --runInBand`

Expected: FAIL with missing zone/behavior assertions, not with malformed test setup or import errors.

- [ ] **Step 5: Commit the red test scaffold**

```bash
git add tests/ui_attack_drag_leader_zone.test.js
git commit -m "test: cover enemy hand leader attack zone"
```

## Task 2: Implement Enemy-Hand Leader Zone State and Overlay Lifecycle

**Files:**
- Modify: `src/ui.js`
- Test: `tests/ui_attack_drag_leader_zone.test.js`

- [ ] **Step 1: Add the next failing test for drag lifecycle cleanup**

```js
test('cleans up the enemy hand leader zone on cancel and invalid end states', () => {
    const attacker = document.createElement('div');
    attacker.className = 'card field-card';
    attacker.dataset.uid = 'attacker-1';
    document.getElementById('player1-field').appendChild(attacker);
    attacker.getBoundingClientRect = () => ({ left: 200, top: 420, width: 100, height: 140, right: 300, bottom: 560 });
    document.getElementById('player2-hand').getBoundingClientRect = () => ({ left: 160, top: 60, width: 520, height: 110, right: 680, bottom: 170 });

    attacker.dispatchEvent(createPointerEvent('pointerdown', { clientX: 220, clientY: 440 }));
    window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 110 }));
    expect(document.querySelector('.attack-drag-leader-zone')).not.toBeNull();

    window.dispatchEvent(createPointerEvent('pointercancel', { clientX: 260, clientY: 110 }));
    expect(document.querySelector('.attack-drag-leader-zone')).toBeNull();
});
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `npx jest tests/ui_attack_drag_leader_zone.test.js --runInBand -t "cleans up the enemy hand leader zone"`

Expected: FAIL because `src/ui.js` does not yet track or remove a zone element.

- [ ] **Step 3: Add attack-drag zone constants, state, and lifecycle helpers in `src/ui.js`**

```js
const ATTACK_DRAG_LEADER_ZONE_ID = 'attack-drag-leader-zone';
const ATTACK_DRAG_LEADER_CONFIRM_MS = 180;

const attackDragState = {
    pointerId: null,
    isPending: false,
    isActive: false,
    attackerUid: null,
    sourceElement: null,
    sourceCenterX: 0,
    sourceCenterY: 0,
    startX: 0,
    startY: 0,
    currentX: 0,
    currentY: 0,
    hoverTargetUid: null,
    hoverElement: null,
    lineElement: null,
    leaderZoneElement: null
};

function ensureAttackDragLeaderZoneElement() {
    const transientRoot = ensureTransientVisualRoot();
    if (!transientRoot) return null;

    let zoneEl = attackDragState.leaderZoneElement ?? transientRoot.querySelector(`#${ATTACK_DRAG_LEADER_ZONE_ID}`);
    if (zoneEl) {
        attackDragState.leaderZoneElement = zoneEl;
        return zoneEl;
    }

    zoneEl = document.createElement('div');
    zoneEl.id = ATTACK_DRAG_LEADER_ZONE_ID;
    zoneEl.className = 'attack-drag-leader-zone';
    zoneEl.innerHTML = `
        <div class="attack-drag-leader-zone__label">攻击主战者</div>
        <div class="attack-drag-leader-zone__avatar" aria-hidden="true"></div>
    `;
    transientRoot.appendChild(zoneEl);
    attackDragState.leaderZoneElement = zoneEl;
    return zoneEl;
}

function removeAttackDragLeaderZoneElement() {
    attackDragState.leaderZoneElement?.remove();
    attackDragState.leaderZoneElement = null;
}
```

- [ ] **Step 4: Position and hydrate the zone from the opponent hand and opponent avatar data**

```js
function updateAttackDragLeaderZoneLayout() {
    const zoneEl = ensureAttackDragLeaderZoneElement();
    const handEl = player2HandEl ?? document.getElementById('player2-hand');
    if (!zoneEl || !handEl) return null;

    const rect = handEl.getBoundingClientRect();
    zoneEl.style.left = `${rect.left}px`;
    zoneEl.style.top = `${rect.top}px`;
    zoneEl.style.width = `${rect.width}px`;
    zoneEl.style.height = `${rect.height}px`;

    const avatarMarker = zoneEl.querySelector('.attack-drag-leader-zone__avatar');
    const opponentPlayer = gameState.players?.find(player => player?.id !== (gameState.localPlayerId ?? uiCurrentPlayerId)) ?? null;
    const avatarInfo = resolvePlayerAvatar(opponentPlayer, false, resolvePlayerDisplayName(opponentPlayer, false));
    avatarMarker?.classList.toggle('has-image', Boolean(avatarInfo.imageSrc));
    avatarMarker?.style.setProperty('--leader-zone-avatar-image', avatarInfo.imageSrc ? `url("${avatarInfo.imageSrc}")` : 'none');
    if (avatarMarker && !avatarInfo.imageSrc) {
        avatarMarker.textContent = avatarInfo.fallbackText;
    }
    return rect;
}
```

- [ ] **Step 5: Wire creation and cleanup into the drag lifecycle**

```js
function activateAttackDragIfNeeded() {
    initiateAttack(attackerUid);
    if (String(gameState.selectedAttackerUid ?? '') !== String(attackerUid)) {
        resetAttackDragState();
        return;
    }
    attackDragState.lineElement = ensureAttackDragLineElement();
    updateAttackDragLeaderZoneLayout();
    attackDragState.isPending = false;
    attackDragState.isActive = true;
    document.body.classList.add('attack-dragging');
    updateAttackDragLine();
}

function resetAttackDragState() {
    clearAttackDragHover();
    removeAttackDragLeaderZoneElement();
    removeAttackDragLineElement();
    document.body.classList.remove('attack-dragging');
    // keep the rest of the existing reset fields
}
```

- [ ] **Step 6: Run the focused lifecycle test and then the whole file**

Run: `npx jest tests/ui_attack_drag_leader_zone.test.js --runInBand`

Expected: Some tests still FAIL, but lifecycle/visibility should now move from “missing element” toward target-resolution-specific failures.

- [ ] **Step 7: Commit the overlay lifecycle implementation**

```bash
git add src/ui.js tests/ui_attack_drag_leader_zone.test.js
git commit -m "feat: add enemy hand leader attack zone overlay"
```

## Task 3: Resolve the Zone to Opposing Leader UID and Add Release Confirmation

**Files:**
- Modify: `src/ui.js`
- Test: `tests/ui_attack_drag_leader_zone.test.js`

- [ ] **Step 1: Add the failing release-confirmation test**

```js
test('releasing inside a valid enemy hand leader zone targets the opponent leader and marks confirmation state', () => {
    jest.useFakeTimers();
    const attacker = document.createElement('div');
    attacker.className = 'card field-card';
    attacker.dataset.uid = 'attacker-1';
    document.getElementById('player1-field').appendChild(attacker);
    attacker.getBoundingClientRect = () => ({ left: 200, top: 420, width: 100, height: 140, right: 300, bottom: 560 });

    const opponentAvatar = document.getElementById('player2-avatar');
    opponentAvatar.classList.add('targetable');
    document.getElementById('player2-hand').getBoundingClientRect = () => ({ left: 160, top: 60, width: 520, height: 110, right: 680, bottom: 170 });
    document.elementFromPoint = jest.fn(() => document.getElementById('player2-hand'));

    attacker.dispatchEvent(createPointerEvent('pointerdown', { clientX: 220, clientY: 440 }));
    window.dispatchEvent(createPointerEvent('pointermove', { clientX: 260, clientY: 110 }));
    window.dispatchEvent(createPointerEvent('pointerup', { clientX: 260, clientY: 110 }));

    expect(gameManager.selectAttackTarget).toHaveBeenCalledWith('leader-2');
    expect(document.querySelector('.attack-drag-leader-zone')?.classList.contains('is-confirmed')).toBe(true);
});
```

- [ ] **Step 2: Run the confirmation test to verify it fails**

Run: `npx jest tests/ui_attack_drag_leader_zone.test.js --runInBand -t "releasing inside a valid enemy hand leader zone"`

Expected: FAIL because the drag resolver does not yet treat the enemy hand zone as a leader proxy.

- [ ] **Step 3: Extend attack target resolution to recognize the enemy-hand leader zone**

```js
function resolveAttackDragLeaderZoneTarget(clientX, clientY) {
    const zoneEl = attackDragState.leaderZoneElement;
    const rect = zoneEl?.getBoundingClientRect?.() ?? updateAttackDragLeaderZoneLayout();
    if (!rect || !isPointInsideRect(clientX, clientY, rect)) {
        return null;
    }

    const opponentAvatarEl = document.getElementById('player2-avatar');
    const leaderUid = resolveAvatarLeaderUid(opponentAvatarEl);
    const isValid = Boolean(leaderUid) && opponentAvatarEl?.classList.contains('targetable');

    return {
        uid: leaderUid ? String(leaderUid) : null,
        element: zoneEl,
        isValid
    };
}

function resolveAttackDragDropTarget(clientX, clientY) {
    const hit = document.elementFromPoint(clientX, clientY);
    const targetCard = hit?.closest?.('.card[data-uid]');
    if (targetCard?.dataset?.uid) {
        const uid = String(targetCard.dataset.uid);
        const isValid = targetCard.classList.contains('targetable');
        return { uid, element: targetCard, isValid };
    }

    const avatarEl = hit?.closest?.('#player1-avatar, #player2-avatar');
    if (avatarEl) {
        const uid = resolveAvatarLeaderUid(avatarEl);
        const isValid = avatarEl.classList.contains('targetable');
        return { uid: uid ? String(uid) : null, element: avatarEl, isValid };
    }

    const leaderZoneTarget = resolveAttackDragLeaderZoneTarget(clientX, clientY);
    if (leaderZoneTarget) {
        return leaderZoneTarget;
    }

    return { uid: null, element: null, isValid: false };
}
```

- [ ] **Step 4: Update hover state handling for zone-specific valid and invalid states**

```js
function updateAttackDragHover(targetInfo) {
    const zoneEl = attackDragState.leaderZoneElement;
    zoneEl?.classList.remove('is-active', 'is-valid', 'is-invalid');

    const nextUid = targetInfo?.isValid && targetInfo.uid ? String(targetInfo.uid) : null;
    const nextElement = targetInfo?.element ?? null;

    if (zoneEl && nextElement === zoneEl) {
        zoneEl.classList.add('is-active');
        zoneEl.classList.add(targetInfo?.isValid ? 'is-valid' : 'is-invalid');
    }

    if (attackDragState.hoverTargetUid === nextUid && attackDragState.hoverElement === nextElement) {
        return;
    }

    clearAttackDragHover();
    if (nextUid && nextElement && nextElement !== zoneEl) {
        nextElement.classList.add('attack-drag-hover');
    }
    attackDragState.hoverTargetUid = nextUid;
    attackDragState.hoverElement = nextElement;
}
```

- [ ] **Step 5: Add light release confirmation before final cleanup**

```js
function markAttackDragLeaderZoneConfirmed() {
    const zoneEl = attackDragState.leaderZoneElement;
    if (!zoneEl) return;
    zoneEl.classList.remove('is-invalid');
    zoneEl.classList.add('is-confirmed');
}

function finalizeAttackDrag(event, { wasCancelled = false } = {}) {
    let dropTargetUid = null;
    if (attackDragState.isActive && !wasCancelled) {
        const clientX = Number.isFinite(event.clientX) ? event.clientX : attackDragState.currentX;
        const clientY = Number.isFinite(event.clientY) ? event.clientY : attackDragState.currentY;
        const targetInfo = resolveAttackDragDropTarget(clientX, clientY);
        if (targetInfo.isValid && targetInfo.uid) {
            dropTargetUid = String(targetInfo.uid);
        }
    }
    const committedToLeaderZone = dropTargetUid
        && attackDragState.leaderZoneElement
        && attackDragState.hoverElement === attackDragState.leaderZoneElement;

    if (committedToLeaderZone) {
        markAttackDragLeaderZoneConfirmed();
    }

    resetAttackDragState();

    if (wasActiveDrag) {
        suppressGameClickUntil = Date.now() + ATTACK_DRAG_CLICK_SUPPRESS_MS;
        event.preventDefault();
        if (dropTargetUid && gameState.selectedAttackerUid) {
            void selectAttackTarget(dropTargetUid);
        }
    }
}
```

- [ ] **Step 6: Run the attack-zone test file until all tests pass**

Run: `npx jest tests/ui_attack_drag_leader_zone.test.js --runInBand`

Expected: PASS

- [ ] **Step 7: Commit target resolution and confirmation behavior**

```bash
git add src/ui.js tests/ui_attack_drag_leader_zone.test.js
git commit -m "feat: resolve enemy hand zone as leader attack target"
```

## Task 4: Add CSS Styling and Run Full Verification

**Files:**
- Modify: `src/styles/game.css`
- Test: `tests/ui_attack_drag_leader_zone.test.js`

- [ ] **Step 1: Add the visual styling block for the zone and confirmation pulse**

```css
.attack-drag-leader-zone {
    position: fixed;
    z-index: 8900;
    pointer-events: none;
    border: 2px solid rgba(148, 163, 184, 0.26);
    border-radius: 0;
    background: linear-gradient(180deg, rgba(8, 15, 27, 0.08), rgba(8, 15, 27, 0.18));
    box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.02), 0 10px 24px rgba(2, 6, 23, 0.16);
    opacity: 0.72;
    transition: border-color 120ms ease, box-shadow 120ms ease, opacity 120ms ease, transform 140ms ease;
}

.attack-drag-leader-zone__label {
    position: absolute;
    top: 10px;
    left: 50%;
    transform: translateX(-50%);
}

.attack-drag-leader-zone__avatar {
    position: absolute;
    top: 42px;
    left: 50%;
    width: 56px;
    height: 56px;
    transform: translateX(-50%);
    border-radius: 999px;
    border: 2px solid rgba(125, 211, 252, 0.72);
    background: radial-gradient(circle at 35% 35%, #1d4ed8, #111827);
    background-image: var(--leader-zone-avatar-image, none);
    background-size: cover;
    background-position: center;
}

.attack-drag-leader-zone.is-valid {
    border-color: rgba(96, 165, 250, 0.82);
    box-shadow: inset 0 0 0 1px rgba(125, 211, 252, 0.14), 0 14px 36px rgba(8, 47, 73, 0.24);
    opacity: 0.92;
}

.attack-drag-leader-zone.is-invalid {
    border-color: rgba(248, 113, 113, 0.6);
    box-shadow: inset 0 0 0 1px rgba(248, 113, 113, 0.1), 0 10px 28px rgba(127, 29, 29, 0.18);
}

.attack-drag-leader-zone.is-confirmed .attack-drag-leader-zone__avatar {
    animation: attackLeaderZoneAvatarFlash 180ms ease-out;
}
```

- [ ] **Step 2: Add the keyframes used by the confirmation effect**

```css
@keyframes attackLeaderZoneAvatarFlash {
    0% { transform: translateX(-50%) scale(1); filter: brightness(1); }
    50% { transform: translateX(-50%) scale(1.06); filter: brightness(1.22); }
    100% { transform: translateX(-50%) scale(1); filter: brightness(1); }
}
```

- [ ] **Step 3: Run targeted tests, related drag regression, and build**

Run: `npx jest tests/ui_attack_drag_leader_zone.test.js --runInBand`
Expected: PASS

Run: `npx jest tests/ui_hand_drag_play.test.js --runInBand`
Expected: PASS

Run: `npm run build`
Expected: Vite build succeeds

- [ ] **Step 4: Commit the visual layer**

```bash
git add src/styles/game.css tests/ui_attack_drag_leader_zone.test.js src/ui.js
git commit -m "feat: style enemy hand leader attack zone"
```

## Self-Review

### Spec Coverage

- Drag-only visibility: Task 2
- Opponent hand overlay + avatar emblem: Task 2 and Task 4
- Free arrow behavior with no snapping: Task 1 and Task 3 tests
- Validity tied to existing leader legality: Task 3
- Light confirmation feedback on release: Task 3 and Task 4
- Cleanup on cancel and end states: Task 2 and Task 3

### Placeholder Scan

- No placeholder markers or shortcut references remain.
- All tasks list exact files and exact commands.
- Each implementation step includes concrete code to add or adapt.

### Type and Naming Consistency

- Zone element id/class names are consistently `ATTACK_DRAG_LEADER_ZONE_ID` / `.attack-drag-leader-zone`.
- Resolution helper naming is consistently `resolveAttackDragLeaderZoneTarget`.
- Confirmation helper naming is consistently `markAttackDragLeaderZoneConfirmed`.
