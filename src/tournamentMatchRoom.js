import { getDeckClassDisplay } from './deckPresentation.js';
import { createDuelSeatView } from './duelRoomView.js';
import { fetchTournamentResponse, tournamentBrackets, tournamentMatchFormats, tournamentRoundLabel, tournamentStages } from './tournamentUi.js';

let cleanup;

export function cleanupTournamentMatchRoom() {
    cleanup?.();
    cleanup = undefined;
}

export function initializeTournamentMatchRoom(enterBattle) {
    cleanupTournamentMatchRoom();
    const id = new URLSearchParams(window.location.search).get('tournamentMatch');
    const byId = suffix => document.getElementById('tournament-match-' + suffix);
    const layout = byId('layout');
    const seats = byId('seats');
    const enter = byId('enter');
    const message = byId('message');
    const results = byId('results');
    const games = byId('games');
    layout.hidden = true;
    results.hidden = true;
    showMessage(id ? '正在读取赛事房间…' : '请从赛程进入赛事房间。');
    if (!id) return;

    const endpoint = '/api/tournament-matches/' + encodeURIComponent(id);
    const controller = new AbortController();
    const { signal } = controller;
    const leftView = createDuelSeatView('left', '左席');
    const rightView = createDuelSeatView('right', '右席');
    seats.replaceChildren(leftView.element, rightView.element);

    let room;
    let timer;
    let pending = false;
    let reading = false;
    let version = 0;
    let roomTag;
    let snapshotAt = 0;
    let renderedResultsKey;
    let lastPhase = '';
    let unavailable = false;
    let deckRows = 1;

    cleanup = () => {
        controller.abort();
        window.clearTimeout(timer);
        seats.replaceChildren();
        games.replaceChildren();
    };

    function showMessage(text, error = false) {
        message.textContent = text;
        message.dataset.error = String(error);
    }

    function seatModel(seat) {
        const banned = !seat.isSelf && room.phase === 'ban' && !room.myBan;
        const picking = seat.isSelf && room.phase === 'pick' && !room.myPick;
        const confirmed = room.phase === 'ban' ? seat.banLocked : room.phase === 'pick' ? seat.pickLocked : false;
        return {
            name: seat.playerName,
            avatarUrl: seat.avatarUrl,
            self: seat.isSelf,
            state: confirmed ? 'ready' : 'waiting',
            status: confirmed ? '已确认' : '',
            decks: seat.decks.map(deck => {
                const myBan = room.phase === 'ban' && !seat.isSelf && room.myBan === deck.id;
                const myPick = room.phase === 'pick' && seat.isSelf && room.myPick === deck.id;
                const state = deck.banned ? 'banned' : deck.hasWon ? 'won' : myBan || myPick ? 'selected' : '';
                const label = deck.banned ? '已禁用' : deck.hasWon ? '胜出锁定'
                    : myBan ? '已选禁用' : myPick ? '已选出战' : '';
                const deckClass = getDeckClassDisplay(deck.class, room.deckRule, deck.secondaryClass);
                return {
                    id: deck.id,
                    name: deck.name,
                    class: deck.class,
                    classLabel: deckClass,
                    status: label,
                    state,
                    action: banned ? 'ban' : picking && !deck.banned && !deck.hasWon ? 'pick' : null
                };
            })
        };
    }

    function phaseText() {
        const winner = [room.one, room.two].find(seat => seat.entryId === room.winnerEntryId);
        switch (room.phase) {
            case 'ban': return room.myBan ? '禁牌已锁定，等待对手。' : '从对手阵容中禁用一套卡组。';
            case 'pick': return room.myPick ? '出战卡组已锁定，等待对手。' : '选择第 ' + room.gameNumber + ' 局的出战卡组。';
            case 'battle': return '第 ' + room.gameNumber + ' 局已就绪。';
            case 'completed': return winner ? winner.playerName + ' 赢得比赛。' : '本场比赛已结束。';
            default: return '等待本阶段的参赛卡组锁定。';
        }
    }

    function renderGameResults(oneOnLeft) {
        const key = `${room.results.length}:${oneOnLeft}`;
        if (key === renderedResultsKey) return;
        renderedResultsKey = key;
        results.hidden = room.results.length === 0;
        const name = id => [room.one, room.two].find(seat => seat.entryId === id)?.playerName;
        const deck = (seat, id) => seat.decks.find(item => item.id === id)?.name ?? '卡组';
        games.replaceChildren(...room.results.map(game => {
            const result = document.createElement('li');
            const heading = document.createElement('strong');
            heading.textContent = '第 ' + game.number + ' 局 · ' + (name(game.winnerEntryId) || '平局');
            const text = document.createElement('span');
            const oneDeck = deck(room.one, game.entryOneDeckId);
            const twoDeck = deck(room.two, game.entryTwoDeckId);
            const lineup = oneOnLeft ? [oneDeck, twoDeck] : [twoDeck, oneDeck];
            const firstPlayer = name(game.firstPlayerEntryId);
            text.textContent = [lineup.join(' 对 '), firstPlayer && firstPlayer + ' 先手'].filter(Boolean).join(' · ');
            result.append(heading, text);
            return result;
        }));
    }

    function setText(node, text) {
        if (node.textContent !== text) node.textContent = text;
    }

    function render() {
        layout.hidden = false;
        const oneOnLeft = room.one.isSelf;
        const [leftSeat, rightSeat] = oneOnLeft ? [room.one, room.two] : [room.two, room.one];
        const nextDeckRows = Math.max(1, room.one.decks.length, room.two.decks.length);
        if (nextDeckRows !== deckRows) {
            deckRows = nextDeckRows;
            layout.style.setProperty('--duel-deck-rows', String(deckRows));
        }
        setText(byId('event'), room.tournamentName);
        setText(byId('score'), leftSeat.wins + ' : ' + rightSeat.wins);
        const bracket = room.stage === 'doubleElimination' && room.bracket !== 'finals' ? tournamentBrackets[room.bracket] : '';
        setText(byId('round'), [tournamentStages[room.stage], bracket, tournamentRoundLabel(room.bracket, room.roundNumber),
            tournamentMatchFormats[room.matchFormat]].filter(Boolean).join(' · '));
        setText(byId('phase'), phaseText());
        enter.hidden = room.phase !== 'battle';
        enter.disabled = pending;
        leftView.render(seatModel(leftSeat));
        rightView.render(seatModel(rightSeat));
        renderGameResults(oneOnLeft);
        if (room.phase !== lastPhase) {
            showMessage('');
            lastPhase = room.phase;
        }
    }

    function schedule() {
        window.clearTimeout(timer);
        if (!signal.aborted && !unavailable && room?.phase !== 'completed') {
            timer = window.setTimeout(refresh, document.hidden ? 12000 : 1100);
        }
    }

    async function refresh() {
        if (reading || pending || unavailable || signal.aborted) return schedule();
        const stamp = version;
        reading = true;
        try {
            const response = await fetchTournamentResponse(endpoint, {
                signal, headers: roomTag && Date.now() - snapshotAt < 30000 ? { 'If-None-Match': roomTag } : undefined
            });
            if (response.status === 304 || signal.aborted || stamp !== version) return;
            const next = await response.json();
            if (signal.aborted || stamp !== version) return;
            roomTag = response.headers.get('etag');
            snapshotAt = Date.now();
            room = next;
            render();
        } catch (error) {
            if (!signal.aborted && stamp === version) {
                showMessage(error.message, true);
                unavailable = [401, 403, 404].includes(error.status);
            }
        } finally {
            reading = false;
            schedule();
        }
    }

    async function act(action, deckId) {
        if (!room || pending || unavailable || signal.aborted) return;
        pending = true;
        version++;
        enter.disabled = true;
        const url = action === 'ban' ? '/ban' : action === 'pick' ? '/games/' + room.gameNumber + '/deck' : '/enter';
        try {
            const response = await fetchTournamentResponse(endpoint + url, {
                method: action === 'enter' ? 'POST' : 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(action === 'enter' ? { gameId: room.activeGameId } : { deckId }),
                signal
            });
            const result = await response.json();
            if (signal.aborted) return;
            showMessage('');
            if (action === 'enter') {
                showMessage('正在进入对战…');
                await enterBattle(result);
            } else {
                roomTag = response.headers.get('etag');
                snapshotAt = Date.now();
                room = result;
                render();
            }
        } catch (error) {
            if (!signal.aborted) showMessage(error.message, true);
        } finally {
            pending = false;
            enter.disabled = false;
            schedule();
        }
    }

    seats.addEventListener('click', event => {
        const button = event.target.closest('button[data-tournament-action]');
        if (button) void act(button.dataset.tournamentAction, button.dataset.deckId);
    }, { signal });
    enter.addEventListener('click', () => void act('enter'), { signal });
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden && room?.phase !== 'completed') void refresh();
    }, { signal });
    void refresh();
}
