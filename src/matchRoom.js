import { getDeckClassDisplay, getDeckEnvironmentLabel, getDeckRuleLabel } from './deckPresentation.js';
import { copyRoomCode, createDuelSeatView } from './duelRoomView.js';
import { isRoomSeatAlreadyReleased } from './roomExitDecisionPolicy.js';

let cleanup;

export function cleanupMatchRoom() {
    cleanup?.();
    cleanup = undefined;
}

export function initializeMatchRoom(options) {
    cleanupMatchRoom();
    const handshake = options.handshake;
    const layout = document.getElementById('match-room-layout');
    const seats = document.getElementById('match-room-seats');
    const status = document.getElementById('match-room-message');
    const invite = document.getElementById('match-room-code');
    const format = document.getElementById('match-room-format');
    const ready = document.getElementById('match-room-ready');
    const readyLabel = ready.querySelector('.duel-room-ready-label');
    const copy = document.getElementById('match-room-copy');
    const copyToast = document.getElementById('match-room-copy-toast');
    layout.hidden = true;
    status.textContent = handshake ? '正在读取房间…' : '房间座位已失效，请重新创建或加入。';
    status.dataset.error = String(!handshake);
    if (!handshake) return;

    const controller = new AbortController();
    const { signal } = controller;
    const host = createDuelSeatView('host', '房主');
    const guest = createDuelSeatView('guest', '访客');
    const ownHost = handshake.role === 'host';
    const [ownSeat, opponentSeat] = ownHost ? [host, guest] : [guest, host];
    seats.replaceChildren(ownSeat.element, opponentSeat.element);
    const pick = document.createElement('button');
    pick.type = 'button';
    pick.className = 'duel-room-button';
    pick.textContent = '选择卡组';
    ownSeat.actions.append(pick);
    const kick = document.createElement('button');
    kick.type = 'button';
    kick.className = 'duel-room-button duel-room-danger';
    kick.textContent = '移出访客';
    if (ownHost) guest.actions.append(kick);

    const profiles = new Map();
    let profilesKey;
    let timer;
    let copyToastTimer;
    let room;
    let busy = false;
    let polling = false;
    let entering = false;
    let released = false;
    let triedPreferredDeck = false;
    let requestVersion = 0;

    function showMessage(text, error = false) {
        status.textContent = text;
        status.dataset.error = String(error);
    }

    cleanup = () => {
        controller.abort();
        window.clearTimeout(timer);
        window.clearTimeout(copyToastTimer);
        copyToast.textContent = '';
        options.closeDeckPicker();
        seats.replaceChildren();
    };

    function setControls(requestedReady = null) {
        const roomWaiting = room && (room.status === 'Waiting' || room.status === 'Ready');
        const ownReady = ownHost ? room?.hostReady : room?.guestReady;
        const locked = busy || entering || released;
        pick.disabled = locked || !roomWaiting || ownReady;
        ready.disabled = locked || !roomWaiting
            || (!ownReady && (!room.selectedDeckId || room.selectedDeckMatchesRoom === false));
        const submitting = requestedReady !== null;
        const label = submitting
            ? (requestedReady ? '准备中…' : '取消中…')
            : (ownReady ? '取消准备' : '准备就绪');
        if (readyLabel.textContent !== label) readyLabel.textContent = label;
        if (ready.getAttribute('aria-busy') !== String(submitting)) {
            ready.setAttribute('aria-busy', String(submitting));
        }
        ready.dataset.ready = String(Boolean(ownReady));
        kick.hidden = !roomWaiting || !room?.guestPlayerId;
        kick.disabled = locked;
    }

    function seatModel(side) {
        const isHost = side === 'host';
        const playerId = isHost ? room.hostPlayerId : room.guestPlayerId;
        const profile = profiles.get(playerId);
        const own = ownHost === isHost;
        const prepared = isHost ? room.hostReady : room.guestReady;
        const selected = (isHost ? room.hostDeckSelected : room.guestDeckSelected)
            ?? Boolean(own ? room.selectedDeckId : (prepared || (isHost ? room.hostDeckClass : room.guestDeckClass)));
        const wins = isHost ? room.hostWins : room.guestWins;
        let deck = { name: playerId ? (selected ? '已选卡组' : '未选卡组') : '', state: 'empty' };
        if (own && selected) {
            const deckClass = isHost ? room.hostDeckClass : room.guestDeckClass;
            const secondaryClass = isHost ? room.hostSecondaryClass : room.guestSecondaryClass;
            const classLabel = deckClass ? getDeckClassDisplay(deckClass, room.requiredRule, secondaryClass) : '';
            const deckName = room.selectedDeckName;
            deck = { name: deckName || classLabel || '已选卡组', class: deckClass, classLabel: deckName ? classLabel : '' };
        }
        return {
            name: profile?.displayName || (playerId ? (isHost ? '房主' : '访客') : '等待玩家'),
            avatarUrl: profile?.avatarUrl,
            score: wins > 0 ? '胜场 ' + wins : '',
            status: !playerId ? '' : prepared ? '已准备' : '未准备',
            state: !playerId ? 'empty' : prepared ? 'ready' : 'waiting',
            self: own,
            decks: [deck]
        };
    }

    function render() {
        if (layout.hidden) showMessage('');
        layout.hidden = false;
        if (invite.textContent !== room.joinCode) invite.textContent = room.joinCode;
        const label = room.requiredEnvironment
            ? getDeckEnvironmentLabel(room.requiredEnvironment) + ' · ' + getDeckRuleLabel(room.requiredRule)
            : '';
        if (format.textContent !== label) format.textContent = label;
        host.render(seatModel('host'));
        guest.render(seatModel('guest'));
        setControls();
    }

    function stop(message) {
        released = true;
        window.clearTimeout(timer);
        options.releaseHandshake();
        showMessage(message, true);
        setControls();
        copy.disabled = true;
    }

    function schedule(delay = document.hidden ? 12000 : 1050) {
        window.clearTimeout(timer);
        if (signal.aborted || released || entering) return;
        timer = window.setTimeout(refresh, delay);
    }

    async function startIfReady(state) {
        if (state.status !== 'Ready') return;
        try {
            await options.start();
        } catch (error) {
            if (error.status !== 409) throw error;
        }
    }

    async function refresh() {
        if (signal.aborted || released || entering) return;
        if (polling || busy) return schedule();
        polling = true;
        const stamp = requestVersion;
        try {
            const seatKey = room ? room.hostPlayerId + '/' + (room.guestPlayerId ?? '') : undefined;
            const includeProfiles = profilesKey === undefined || profilesKey !== seatKey;
            const next = await options.getRoom({ includeProfiles, signal });
            if (signal.aborted || stamp !== requestVersion) return;
            if (next.status === 'Closed') return stop(next.closedReason || '房间已关闭。');
            if (!next.isMirage && !next.useAiFallback) {
                if (next.seatProfiles) {
                    profiles.clear();
                    for (const profile of next.seatProfiles) profiles.set(profile.playerId, profile);
                    profilesKey = next.hostPlayerId + '/' + (next.guestPlayerId ?? '');
                }
                if (ownHost && room && !room.guestPlayerId && next.guestPlayerId) options.guestJoined();
                room = next;
            } else {
                return stop('这个房间由对应模式的入口管理。');
            }
            copy.disabled = !room.joinCode;
            render();
            if (room.status === 'Started') {
                const decision = options.getStartDecision(room);
                if (decision === 'wait-reset') showMessage('正在结算上一局…');
                else if (decision === 'ended-no-rematch') return stop('本房间的对局已结束。');
                else if (decision === 'enter-game') {
                    entering = true;
                    setControls();
                    showMessage('正在进入对战…');
                    if (!await options.enterGame(room)) entering = false;
                }
                return;
            }
            if (room.status === 'Ready') {
                await startIfReady(room);
                return;
            }
            if (!triedPreferredDeck && !room.selectedDeckId) {
                const preferred = options.preferredDeck(room);
                triedPreferredDeck = true;
                if (preferred) {
                    busy = true;
                    setControls();
                    try {
                        await options.setDeck(preferred);
                    } finally {
                        busy = false;
                        if (!signal.aborted) setControls();
                    }
                    return;
                }
            }
            showMessage(room.selectedDeckMatchesRoom === false && room.selectedDeckId
                ? '卡组不符合房间规则，请重新选择。'
                : '');
        } catch (error) {
            if (signal.aborted || stamp !== requestVersion) return;
            entering = false;
            if (isRoomSeatAlreadyReleased(error)) stop(error.status === 403 ? '你已离开房间或座位已失效。' : error.message);
            else showMessage(error.message || '连接房间失败，正在重试。', true);
        } finally {
            polling = false;
            schedule();
        }
    }

    async function act(action, requestedReady = null) {
        if (!room || busy || entering || released) return;
        const restoreFocus = requestedReady !== null && ready.matches(':focus-visible');
        requestVersion++;
        busy = true;
        setControls(requestedReady);
        let completed = false;
        try {
            await action();
            completed = true;
        } catch (error) {
            if (!signal.aborted) showMessage(error.message, true);
        } finally {
            busy = false;
            if (!signal.aborted) {
                setControls();
                if (restoreFocus && !ready.disabled && document.activeElement === document.body) {
                    ready.focus({ preventScroll: true });
                }
                schedule(completed ? 0 : undefined);
            }
        }
    }

    pick.addEventListener('click', () => act(async () => {
        const id = await options.pickDeck(room);
        if (id && !signal.aborted) await options.setDeck(id);
    }), { signal });
    ready.addEventListener('click', () => {
        const nextReady = !(ownHost ? room?.hostReady : room?.guestReady);
        void act(async () => {
            const updated = await options.setReady(nextReady);
            if (signal.aborted) return;
            // The ready response omits the private deck and invite details.
            room.hostReady = updated.hostReady;
            room.guestReady = updated.guestReady;
            room.status = updated.status;
            render();
            await startIfReady(updated);
        }, nextReady);
    }, { signal });
    kick.addEventListener('click', () => act(() => options.kick(room.guestPlayerId)), { signal });
    copy.addEventListener('click', async () => {
        if (!room?.joinCode) return;
        const copied = await copyRoomCode(room.joinCode);
        if (signal.aborted) return;
        window.clearTimeout(copyToastTimer);
        copyToast.dataset.error = String(!copied);
        copyToast.textContent = copied ? '已复制邀请码。' : '复制失败，可手动选择邀请码。';
        copyToastTimer = window.setTimeout(() => { copyToast.textContent = ''; }, copied ? 2200 : 4200);
    }, { signal });
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) {
            window.clearTimeout(timer);
            void refresh();
        }
    }, { signal });
    void refresh();
}
