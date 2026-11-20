import './styles/replay.css';
import { formatBeijingTime } from './beijingTime.js';
import { createReplayTimeline } from './replayTimeline.js';
import { readReplayTickets } from './replayTickets.js';
import { USER_SESSION_CHANGED_EVENT } from './userSession.js';

export function installReplayLibrary(recentGame) {
    const dialog = document.getElementById('replay-library');
    const toggle = document.getElementById('replay-toggle');
    if (!dialog || !toggle || window.frameElement?.hasAttribute('data-replay-board')) return;
    const control = name => dialog.querySelector(`[data-replay="${name}"]`);
    const listPanel = dialog.querySelector('.replay-list-panel');
    const list = dialog.querySelector('.replay-list');
    const player = dialog.querySelector('.replay-player');
    const stage = dialog.querySelector('.replay-stage');
    const status = dialog.querySelector('.replay-status');
    const title = dialog.querySelector('h2');
    const back = control('back'), more = control('more'), play = control('play'), turn = control('turn');
    let request, timer, refreshTimer, boardTimeout, nextOffset;
    let timeline, renderBoard, selected, viewingPlayer, index = 0;
    let playbackRun = 0, currentRender;

    function pause() {
        playbackRun++;
        clearTimeout(timer);
        timer = null;
        play.textContent = timeline && index === timeline.steps.length - 1 ? '重新播放' : '播放';
    }

    function stop() {
        request?.abort();
        clearTimeout(refreshTimer);
        clearTimeout(boardTimeout);
        pause();
        stage.replaceChildren();
        control('perspective').hidden = true;
        renderBoard = timeline = selected = currentRender = null;
    }

    function requestData() {
        request?.abort();
        request = new AbortController();
        return request.signal;
    }

    function playerError(error) {
        pause();
        status.textContent = error.message || '加载回放失败，请返回列表重试。';
        control('perspective').hidden = true;
        player.hidden = true;
    }

    function showStep(i, animate = false) {
        if (!timeline || !renderBoard) return;
        index = Math.max(0, Math.min(i, timeline.steps.length - 1));
        const step = timeline.steps[index];
        try {
            const completion = renderBoard(timeline.boardAt(index), step, viewingPlayer, animate);
            currentRender = completion;
            completion.catch(error => { if (currentRender === completion) playerError(error); });
        } catch (error) { playerError(error); return; }
        const ended = step.kind === 'GameEnd' || step.kind === 'ended';
        control('caption').textContent = ended ? step.label + ' · ' + matchResult(selected) : step.label;
        control('progress').textContent = `${index + 1} / ${timeline.steps.length}`;
        turn.value = String(step.turnIndex);
        control('prev').disabled = index === 0;
        control('next').disabled = index === timeline.steps.length - 1;
        if (control('next').disabled || !timer) pause();
    }

    function startPlaying() {
        if (!timeline || !renderBoard) return;
        if (index === timeline.steps.length - 1) showStep(0);
        if (timeline.steps.length < 2 || player.hidden) return;
        play.textContent = '暂停';
        const run = ++playbackRun;
        const tick = async () => {
            await currentRender?.catch(() => {});
            if (run !== playbackRun) return;
            const started = performance.now();
            showStep(index + 1, true);
            await currentRender?.catch(() => {});
            if (run !== playbackRun) return;
            const step = timeline.steps[index], next = timeline.steps[index + 1];
            const counterHit = step.kind === 'Damage' && step.cardId && step.cardId === next?.targetId && step.targetId === next.cardId;
            const atImpact = next?.kind === 'Damage' && (step.kind === 'Attack' || counterHit);
            timer = setTimeout(tick, atImpact ? 0 : Math.max(0, 1200 - (performance.now() - started)));
        };
        timer = setTimeout(tick, 1200);
    }

    async function watch(item) {
        stop();
        selected = item;
        viewingPlayer = item.viewerPlayerId;
        index = 0;
        const signal = requestData();
        title.textContent = item.hostName + ' 对 ' + item.guestName;
        listPanel.hidden = true;
        back.hidden = false;
        back.focus();
        dialog.classList.add('is-watching');
        status.textContent = '正在载入回放…';
        try {
            const receipt = readReplayTickets().find(row => row.gameId === item.gameId);
            const res = await fetch('/api/replays/' + encodeURIComponent(item.gameId) + '/playback', {
                signal, headers: receipt ? { 'X-Replay-Ticket': receipt.ticket } : {}
            });
            if (!res.ok) throw new Error(res.status === 404 ? '这场回放已过期或无法访问。' : '读取回放失败，请返回列表重试。');
            const data = await res.json();
            if (signal.aborted) return;
            timeline = createReplayTimeline(data);
            turn.replaceChildren(...timeline.turns.map(({ title }, i) => new Option(title, String(i))));
            const frame = document.createElement('iframe');
            frame.setAttribute('data-replay-board', '');
            frame.title = '对局回放战场';
            const url = new URL(window.location.pathname, window.location.origin);
            url.hash = 'replay-board';
            frame.src = url.href;
            frame.addEventListener('replay-board-ready', event => {
                if (signal.aborted) return;
                clearTimeout(boardTimeout);
                renderBoard = event.detail;
                status.textContent = '';
                player.hidden = false;
                control('perspective').hidden = false;
                showStep(0);
                if (!document.hidden) startPlaying();
            }, { once: true });
            boardTimeout = setTimeout(() => {
                if (!signal.aborted && !renderBoard) playerError(new Error('战场载入超时，请返回列表重试。'));
            }, 30000);
            stage.replaceChildren(frame);
        } catch (error) {
            if (!signal.aborted) playerError(error);
        }
    }

    function matchResult(item) {
        if (!item.winnerPlayerId) return '平局';
        return item.winnerPlayerId === item.viewerPlayerId ? '胜利' : '败北';
    }

    function matchRow(item) {
        const row = document.createElement('button');
        row.type = 'button';
        row.className = 'replay-row';
        const result = document.createElement('strong');
        result.className = 'replay-result';
        result.textContent = matchResult(item);
        result.dataset.result = result.textContent;
        const opponent = document.createElement('span');
        opponent.textContent = '对阵 ' + (item.hostPlayerId === item.viewerPlayerId ? item.guestName : item.hostName);
        const date = document.createElement('time');
        date.textContent = formatBeijingTime(item.endedAtUtc, '', { includeSeconds: true });
        const arrow = document.createElement('span');
        arrow.textContent = '▶';
        arrow.setAttribute('aria-hidden', 'true');
        row.append(result, opponent, date, arrow);
        row.addEventListener('click', () => { void watch(item); });
        return row;
    }

    async function load(offset = 0, attempts = 0) {
        const signal = requestData();
        clearTimeout(refreshTimer);
        status.textContent = '正在读取对局…';
        more.hidden = true;
        if (offset === 0) list.replaceChildren();
        try {
            const res = await fetch('/api/replays/library', {
                method: 'POST', signal, headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ tickets: readReplayTickets().map(r => r.ticket), offset })
            });
            if (!res.ok) throw new Error();
            const data = await res.json();
            if (signal.aborted) return;
            list.append(...data.items.map(matchRow));
            nextOffset = data.nextOffset;
            more.hidden = nextOffset == null;
            status.textContent = list.childElementCount ? '' : data.recordingEnabled
                ? '还没有可回看的对局。打完一场真人对局后，会出现在这里。' : '服务器已关闭新对局录制，当前没有已保存的回放。';
            const recent = recentGame();
            if (attempts > 0 && !offset && recent && data.recordingEnabled && !data.items.some(g => g.gameId === recent)) {
                refreshTimer = setTimeout(() => { void load(0, attempts - 1); }, 1000);
            }
        } catch {
            if (!signal.aborted) {
                status.textContent = '未能读取对局，点「刷新」可以重试。';
                more.hidden = offset === 0;
            }
        }
    }

    function showList() {
        stop();
        title.textContent = '对局回放';
        dialog.classList.remove('is-watching');
        listPanel.hidden = false;
        back.hidden = player.hidden = true;
        void load(0, 5);
    }

    toggle.addEventListener('click', () => {
        if (!dialog.open) dialog.showModal();
        showList();
    });
    control('close').addEventListener('click', () => dialog.close());
    dialog.addEventListener('close', () => { if (!dialog.open) stop(); });
    back.addEventListener('click', () => { showList(); control('refresh').focus(); });
    control('refresh').addEventListener('click', () => { void load(0, 5); });
    more.addEventListener('click', () => { if (nextOffset != null) void load(nextOffset); });
    control('next').addEventListener('click', () => { pause(); showStep(index + 1, true); });
    control('prev').addEventListener('click', () => { pause(); showStep(index - 1); });
    control('perspective').addEventListener('click', () => {
        const other = timeline?.boardAt(index).players.find(p => p.playerId !== viewingPlayer);
        if (other) { viewingPlayer = other.playerId; showStep(index); }
    });
    play.addEventListener('click', () => { if (timer) pause(); else startPlaying(); });
    turn.addEventListener('change', () => { pause(); showStep(timeline.turns[Number(turn.value)].index); });
    document.addEventListener('visibilitychange', () => { if (document.hidden) pause(); });
    document.addEventListener(USER_SESSION_CHANGED_EVENT, () => { if (dialog.open) showList(); });
}
