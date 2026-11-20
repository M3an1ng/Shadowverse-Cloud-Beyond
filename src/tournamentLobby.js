import { formatBeijingTime } from './beijingTime.js';
import { getDeckClassDisplay, getDeckEnvironmentLabel, getDeckRuleLabel } from './deckPresentation.js';
import { closeDeckSelectionPicker, openDeckSelectionPicker } from './components/deckSelectionPickerModal.js';
import { getUserSession, USER_SESSION_CHANGED_EVENT } from './userSession.js';
import { fetchTournamentJson, isTournamentDeckSubmissionOpen, tournamentElement as item, tournamentFormats, tournamentMatchFormats, tournamentStages } from './tournamentUi.js';
import { renderTournamentSchedule } from './tournamentSchedule.js';

const endpoint = '/api/tournaments';
const stateLabels = { scheduled: '待报名', open: '报名中', closed: '报名截止', running: '比赛中', finished: '已结束' };
const emptyListMessages = {
    all: '目前没有赛事。', running: '目前没有进行中的赛事。', finished: '目前没有已结束的赛事。'
};
const timeLabel = value => formatBeijingTime(value, '待定', { includeSeconds: false });
const byId = id => document.getElementById(id);

let cleanupLobby;
let rememberedEventId;

export function cleanupTournamentLobby() {
    cleanupLobby?.();
    cleanupLobby = undefined;
}

function tournamentState(tournament) {
    return ['running', 'finished'].includes(tournament.status) ? tournament.status : tournament.registrationStatus;
}

function deckLabel(deck, rule) {
    return deck.name + ' · ' + getDeckClassDisplay(deck.class, rule, deck.secondaryClass);
}

function addDeckClasses(classes, deck) {
    classes.add(deck.class);
    if (deck.secondaryClass) classes.add(deck.secondaryClass);
}

function lineupStage(details) {
    if (details.tournament.registrationStatus !== 'closed') return 1;
    const now = Date.parse(details.serverTimeUtc);
    return details.tournament.stages.find(stage => isTournamentDeckSubmissionOpen(stage, now))?.number;
}

function registrationPath(eventId, stage) {
    const path = stage === 1 ? '/registration' : '/stages/' + stage + '/lineup';
    return endpoint + '/' + eventId + path;
}

function rememberEvent(id) {
    rememberedEventId = id;
    const url = new URL(window.location.href);
    if (id) url.searchParams.set('tournament', id);
    else url.searchParams.delete('tournament');
    window.history.replaceState(window.history.state, '', url);
}

export function initializeTournamentLobby(openMatch) {
    cleanupTournamentLobby();
    const lifetime = new AbortController();
    const { signal } = lifetime;
    const list = byId('tournament-lobby-events');
    const lobbyStatus = byId('tournament-lobby-status');
    const refresh = byId('tournament-lobby-refresh');
    const filter = byId('tournament-lobby-filter');
    const viewReport = byId('tournament-view-report');
    const detail = byId('tournament-lobby-detail');
    const schedulePane = byId('tournament-lobby-schedule');
    const stageList = byId('tournament-event-stages');
    const pairings = byId('tournament-event-pairings');
    const registration = byId('tournament-registration');
    const registrationNote = byId('tournament-registration-note');
    const registrationStatus = byId('tournament-registration-status');
    const login = byId('tournament-registration-login');
    const submitted = byId('tournament-submitted-decks');
    const form = byId('tournament-registration-form');
    const fields = byId('tournament-registration-fields');
    const submit = byId('tournament-registration-submit');

    rememberedEventId = new URLSearchParams(window.location.search).get('tournament') ?? rememberedEventId;
    let tournaments = [];
    let currentTournament;
    let currentDetails;
    let selectedScheduleStage;
    let currentRegistration;
    let deckChoices = new Map();
    let selectedDeckIds = [];
    let detailRequest;
    let lineupRequest;
    let boundaryTimer;
    let registrationBusy = false;
    let listBusy = false;
    filter.value = 'all';

    function showMessage(node, message, error = false) {
        node.textContent = message;
        node.dataset.error = String(error);
    }

    function setDetailsVisible(visible) {
        detail.hidden = schedulePane.hidden = !visible;
    }

    cleanupLobby = () => {
        lifetime.abort();
        detailRequest?.abort();
        lineupRequest?.abort();
        closeDeckSelectionPicker();
        fields.replaceChildren();
        submitted.replaceChildren();
        window.clearTimeout(boundaryTimer);
    };

    function renderList(selectedId = rememberedEventId) {
        const buttons = tournaments.map(tournament => {
            const button = item('button', '', 'tournament-list-button');
            button.type = 'button';
            button.dataset.eventId = tournament.id;
            button.setAttribute('aria-current', String(tournament.id === selectedId));
            button.append(item('strong', tournament.name),
                item('span', [tournament.cardVersion, tournamentFormats[tournament.format],
                    stateLabels[tournamentState(tournament)]].join(' · '), 'tournament-muted'));
            return button;
        });
        list.replaceChildren(...buttons);
        const active = buttons.find(button => button.dataset.eventId === selectedId);
        if (active) {
            const itemBounds = active.getBoundingClientRect();
            const listBounds = list.getBoundingClientRect();
            if (itemBounds.left < listBounds.left) list.scrollLeft += itemBounds.left - listBounds.left;
            else if (itemBounds.right > listBounds.right) list.scrollLeft += itemBounds.right - listBounds.right;
        }
    }

    function showRegistration(record) {
        currentRegistration = record;
        const entryDecks = record.submittedDecks;
        deckChoices = new Map(record.availableDecks.map(deck => [deck.deckId, deck]));
        selectedDeckIds = Array.from({ length: record.requiredDeckCount }, (_, index) => {
            const deckId = entryDecks[index]?.deckId;
            return deckChoices.has(deckId) ? deckId : null;
        });
        submitted.replaceChildren(...entryDecks.map(deck => item('li', deckLabel(deck, currentTournament.deckRule))));
        submitted.hidden = entryDecks.length === 0;
        const resubmitting = lineupStage(currentDetails) > 1;
        showMessage(registrationStatus, record.blockedReason ?? (resubmitting ? '确认本阶段阵容后提交。'
            : record.entryId ? '已报名，可在截止前重新提交。' : ''));
        form.hidden = Boolean(record.blockedReason) || record.availableDecks.length === 0;
        if (form.hidden) {
            fields.replaceChildren();
            if (!record.blockedReason) {
                showMessage(registrationStatus, '此账号还没有符合赛事规则的完整卡组。');
            }
            return;
        }
        const slots = selectedDeckIds.map((_, index) => {
            const slot = item('div', '', 'tournament-deck-field');
            const actions = item('div', '', 'tournament-deck-controls');
            const choose = item('button', '', 'tournament-button tournament-deck-choice');
            choose.type = 'button';
            choose.dataset.chooseDeck = String(index);
            const clear = item('button', '清除', 'tournament-button');
            clear.type = 'button';
            clear.dataset.clearDeck = String(index);
            clear.setAttribute('aria-label', '清除第 ' + (index + 1) + ' 套卡组');
            actions.append(choose, clear);
            slot.append(item('span', '第 ' + (index + 1) + ' 套'), actions);
            return slot;
        });
        fields.replaceChildren(...slots);
        submit.textContent = resubmitting ? '提交阶段卡组' : record.entryId ? '重新提交卡组' : '确认报名';
        syncDeckSelections();
    }

    function syncDeckSelections() {
        if (!currentRegistration) return;
        const pickedClasses = new Set();
        let complete = true;
        for (const [index, slot] of [...fields.children].entries()) {
            const deck = deckChoices.get(selectedDeckIds[index]);
            if (!deck || pickedClasses.has(deck.class) || pickedClasses.has(deck.secondaryClass)) complete = false;
            if (deck) addDeckClasses(pickedClasses, deck);
            const choose = slot.querySelector('[data-choose-deck]');
            choose.textContent = deck ? deckLabel(deck, currentTournament.deckRule) : '选择卡组';
            choose.setAttribute('aria-label', '第 ' + (index + 1) + ' 套：' + choose.textContent);
            slot.querySelector('[data-clear-deck]').hidden = !deck;
        }
        submit.disabled = registrationBusy || !complete;
    }

    async function pickRegistrationDeck(index) {
        if (!currentRegistration || !currentTournament || registrationBusy) return;
        const requestSignal = detailRequest.signal;
        const { deckRule, deckEnvironment } = currentTournament;
        const otherClasses = new Set();
        for (const [slot, id] of selectedDeckIds.entries()) {
            if (slot !== index && id) addDeckClasses(otherClasses, deckChoices.get(id));
        }
        const decks = [...deckChoices.values()]
            .filter(deck => !otherClasses.has(deck.class) && !otherClasses.has(deck.secondaryClass))
            .map(deck => ({
                id: deck.deckId, name: deck.name,
                classLabel: getDeckClassDisplay(deck.class, deckRule, deck.secondaryClass),
                environment: deckEnvironment, rule: deckRule, isBattleReady: true, cardCount: 40
            }));
        const picked = await openDeckSelectionPicker({
            title: '选择第 ' + (index + 1) + ' 套卡组', decks,
            selectedDeckId: selectedDeckIds[index], emptyText: '没有可选的其他职业卡组。'
        });
        if (!requestSignal.aborted && picked) {
            selectedDeckIds[index] = picked;
            syncDeckSelections();
        }
    }

    function showEvent(details) {
        const tournament = details.tournament;
        currentTournament = tournament;
        currentDetails = details;
        const state = tournamentState(tournament);
        const listItem = list.querySelector('button[aria-current="true"] .tournament-muted');
        if (listItem) listItem.textContent = [tournament.cardVersion, tournamentFormats[tournament.format],
            stateLabels[state]].join(' · ');
        setDetailsVisible(true);
        const headline = byId('tournament-event-title');
        headline.textContent = tournament.name;
        const eventState = byId('tournament-event-state');
        eventState.textContent = stateLabels[state];
        eventState.dataset.state = state;
        viewReport.hidden = !tournament.stages.some(stage => stage.startedAtUtc);
        byId('tournament-event-meta').textContent = [
            tournament.cardVersion, tournamentFormats[tournament.format], getDeckEnvironmentLabel(tournament.deckEnvironment),
            getDeckRuleLabel(tournament.deckRule)
        ].join(' · ');
        byId('tournament-event-start').textContent = timeLabel(tournament.registrationOpensAtUtc);
        byId('tournament-event-end').textContent = tournament.registrationClosesAtUtc
            ? timeLabel(tournament.registrationClosesAtUtc) : '手动截止';
        byId('tournament-event-count').textContent = tournament.playerLimit
            ? tournament.registeredCount + ' / ' + tournament.playerLimit : tournament.registeredCount + ' 人';
        stageList.replaceChildren(...tournament.stages.map(stage => {
            const li = item('li', '', 'tournament-stage');
            li.append(item('strong', tournamentStages[stage.kind]),
                item('span', [tournamentMatchFormats[stage.matchFormat], stage.rounds && stage.rounds + ' 轮',
                    stage.advancingPlayers && stage.advancingPlayers + ' 人晋级',
                    stage.allowDeckResubmission && '允许重交卡组'].filter(Boolean).join(' · '), 'tournament-muted'));
            return li;
        }));
        renderTournamentSchedule(pairings, details, selectedScheduleStage);

        const submission = lineupStage(details);
        registration.hidden = !submission;
        if (submission === 1) {
            byId('tournament-registration-title').textContent = '报名';
            const count = tournament.requiredDeckCount;
            const first = tournament.stages[0];
            registrationNote.textContent = '按顺序提交 ' + count + ' 套卡组，职业不可重复。'
                + (count > first.decksPerPlayer ? ' 首阶段使用前 ' + first.decksPerPlayer + ' 套。' : '')
                + (first.allowDeckResubmission ? ' 截止前可重交。' : ' 提交后锁定。')
                + ' 卡组在报名截止后公开。';
        } else if (submission) {
            const stage = tournament.stages.find(item => item.number === submission);
            byId('tournament-registration-title').textContent = tournamentStages[stage.kind] + ' · 卡组重交';
            const cutoff = stage.deckSubmissionClosesAtUtc
                ? '北京时间 ' + timeLabel(stage.deckSubmissionClosesAtUtc) + ' 截止。' : '由管理员手动截止。';
            registrationNote.textContent = cutoff + '沿用的阵容须符合本阶段卡组数量。重交阵容在阶段开赛后公开。';
        }
    }

    function watchEvent(id, details, requestSignal) {
        const tournament = details.tournament;
        const state = tournament.registrationStatus;
        if (tournament.status === 'finished') return;

        const boundary = state === 'open' ? tournament.registrationClosesAtUtc : tournament.registrationOpensAtUtc;
        const remaining = boundary ? Date.parse(boundary) - Date.parse(details.serverTimeUtc) + 30 : 12000;
        const interval = state === 'closed' ? 4000 : Math.max(200, Math.min(12000, remaining));
        async function check() {
            if (requestSignal.aborted) return;
            if (registrationBusy || document.hidden) {
                boundaryTimer = window.setTimeout(check, 12000);
                return;
            }
            try {
                if (state === 'closed' && tournament.updatedAtUtc) {
                    const version = await fetchTournamentJson(endpoint + '/' + id + '/version', { signal: requestSignal });
                    if (requestSignal.aborted) return;
                    if (version.updatedAtUtc === tournament.updatedAtUtc) {
                        boundaryTimer = window.setTimeout(check, interval);
                        return;
                    }
                }
                const next = await fetchTournamentJson(endpoint + '/' + id, { signal: requestSignal });
                if (requestSignal.aborted) return;
                if (next.tournament.status !== tournament.status) {
                    void loadEvents();
                    return;
                }
                if (next.tournament.registrationStatus !== state || lineupStage(next) !== lineupStage(details)) {
                    void chooseEvent(id);
                    return;
                }
                if (next.tournament.registrationClosesAtUtc !== currentTournament.registrationClosesAtUtc
                    || next.tournament.registeredCount !== currentTournament.registeredCount
                    || next.tournament.updatedAtUtc !== tournament.updatedAtUtc) showEvent(next);
                watchEvent(id, next, requestSignal);
            } catch (error) {
                if (requestSignal.aborted) return;
                if (error.status === 404) {
                    rememberEvent();
                    void loadEvents();
                    return;
                }
                boundaryTimer = window.setTimeout(check, 12000);
            }
        }
        boundaryTimer = window.setTimeout(check, interval);
    }

    async function chooseEvent(id) {
        detailRequest?.abort();
        closeDeckSelectionPicker();
        window.clearTimeout(boundaryTimer);
        lineupRequest?.abort();
        if (rememberedEventId !== id) selectedScheduleStage = undefined;
        rememberEvent(id);
        currentTournament = undefined;
        currentDetails = undefined;
        currentRegistration = undefined;
        registrationBusy = false;
        fields.disabled = false;
        fields.replaceChildren();
        form.hidden = true;
        submitted.hidden = true;
        submitted.replaceChildren();
        login.hidden = true;
        showMessage(registrationStatus, '');
        setDetailsVisible(false);
        for (const button of list.querySelectorAll('button')) {
            button.setAttribute('aria-current', String(button.dataset.eventId === id));
        }
        if (!id) {
            showMessage(lobbyStatus, emptyListMessages[filter.value]);
            return;
        }
        detailRequest = new AbortController();
        const requestSignal = detailRequest.signal;
        showMessage(lobbyStatus, '正在读取赛事…');
        try {
            const path = endpoint + '/' + id;
            const details = await fetchTournamentJson(path, { signal: requestSignal });
            if (requestSignal.aborted) return;
            showEvent(details);
            showMessage(lobbyStatus, '');
            const state = details.tournament.registrationStatus;
            const submission = lineupStage(details);
            watchEvent(id, details, requestSignal);
            if (state === 'scheduled') showMessage(registrationStatus, '报名尚未开始。');
            if (!submission || state === 'scheduled') return;
            if (getUserSession().mode !== 'user') {
                login.hidden = false;
                showMessage(registrationStatus, submission === 1 ? '登录账号后即可报名。' : '登录后可查看晋级资格。');
                return;
            }
            showMessage(registrationStatus, '正在读取你的卡组…');
            try {
                const record = await fetchTournamentJson(registrationPath(id, submission), { signal: requestSignal });
                if (!requestSignal.aborted) showRegistration(record);
            } catch (error) {
                if (requestSignal.aborted) return;
                login.hidden = error.status !== 401 && error.status !== 403;
                showMessage(registrationStatus, error.message, true);
            }
        } catch (error) {
            if (requestSignal.aborted) return;
            if (error.status === 404) {
                rememberEvent();
                void loadEvents();
            } else {
                showMessage(lobbyStatus, error.message, true);
            }
        }
    }

    async function loadEvents() {
        if (listBusy) return;
        listBusy = true;
        refresh.disabled = true;
        filter.disabled = true;
        showMessage(lobbyStatus, '正在读取赛事列表…');
        try {
            tournaments = await fetchTournamentJson(endpoint + '?scope=' + filter.value, { signal });
            if (signal.aborted) return;
            const selected = tournaments.find(event => event.id === rememberedEventId) ?? tournaments[0];
            renderList(selected?.id);
            void chooseEvent(selected?.id);
        } catch (error) {
            if (!signal.aborted) showMessage(lobbyStatus, error.message, true);
        } finally {
            listBusy = false;
            refresh.disabled = false;
            filter.disabled = false;
        }
    }

    async function submitEntry(event) {
        event.preventDefault();
        if (!currentRegistration || !currentTournament || registrationBusy || submit.disabled) return;
        const tournament = currentTournament;
        const requestSignal = detailRequest.signal;
        const submission = lineupStage(currentDetails);
        const firstSubmission = !currentRegistration.entryId;
        const payload = {
            revision: currentRegistration.revision,
            deckIds: selectedDeckIds
        };
        registrationBusy = true;
        fields.disabled = true;
        syncDeckSelections();
        showMessage(registrationStatus, '正在保存卡组…');
        try {
            const record = await fetchTournamentJson(registrationPath(tournament.id, submission), {
                method: 'PUT', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload), signal: requestSignal
            });
            if (requestSignal.aborted) return;
            if (firstSubmission) {
                tournament.registeredCount++;
                byId('tournament-event-count').textContent = tournament.playerLimit
                    ? tournament.registeredCount + ' / ' + tournament.playerLimit : tournament.registeredCount + ' 人';
            }
            showRegistration(record);
            const message = firstSubmission ? '报名成功。' : '卡组已更新。';
            showMessage(registrationStatus, message + (record.blockedReason ? ' ' + record.blockedReason : ''));
        } catch (error) {
            if (!requestSignal.aborted) showMessage(registrationStatus, error.message, true);
        } finally {
            if (!requestSignal.aborted) {
                registrationBusy = false;
                fields.disabled = false;
                syncDeckSelections();
            }
        }
    }

    async function openPlayerProfile(player) {
        const tournament = currentTournament;
        if (!tournament) return;
        const { stageEntryId: entryId, lineupStage: stageNumber } = player.dataset;
        lineupRequest?.abort();
        lineupRequest = new AbortController();
        const requestSignal = lineupRequest.signal;
        player.setAttribute('aria-busy', 'true');
        try {
            const url = endpoint + '/' + tournament.id + '/entries/' + entryId + '?stage=' + stageNumber;
            const lineup = await fetchTournamentJson(url, { signal: requestSignal });
            if (requestSignal.aborted) return;
            const stageLabel = number => tournamentStages[tournament.stages.find(stage => stage.number === number)?.kind];
            const label = stageLabel(Number(stageNumber));
            const previous = lineup.previousLineup;
            const previousLabel = previous ? stageLabel(previous.stageNumber) : undefined;
            const describeDecks = (decks, label) => decks.map(deck => ({
                ...deck,
                classLabel: getDeckClassDisplay(deck.class, tournament.deckRule, deck.secondaryClass),
                environment: tournament.deckEnvironment, rule: tournament.deckRule, stageLabel: label
            }));
            showMessage(lobbyStatus, '');
            void openDeckSelectionPicker({
                title: lineup.playerName, emptyText: '该选手没有提交卡组。',
                playerProfile: {
                    record: lineup.record, avatarUrl: lineup.avatarUrl, stageLabel: label,
                    previousLineup: previous ? { stageLabel: previousLabel, decks: describeDecks(previous.decks, previousLabel) } : null
                },
                decks: describeDecks(lineup.decks, label)
            });
        } catch (error) {
            if (!requestSignal.aborted) showMessage(lobbyStatus, error.message, true);
        } finally {
            player.removeAttribute('aria-busy');
        }
    }

    setDetailsVisible(false);
    form.addEventListener('submit', submitEntry, { signal });
    fields.addEventListener('click', event => {
        const choose = event.target.closest('button[data-choose-deck]');
        if (choose) void pickRegistrationDeck(Number(choose.dataset.chooseDeck));
        const clear = event.target.closest('button[data-clear-deck]');
        if (clear && !registrationBusy) {
            selectedDeckIds[Number(clear.dataset.clearDeck)] = null;
            syncDeckSelections();
            clear.parentElement.querySelector('[data-choose-deck]').focus();
        }
    }, { signal });
    refresh.addEventListener('click', loadEvents, { signal });
    viewReport.addEventListener('click', async () => {
        const id = currentTournament?.id;
        const { openTournamentReport } = await import('./tournamentReports.js');
        if (!signal.aborted && id && id === currentTournament?.id) void openTournamentReport(id, signal);
    }, { signal });
    filter.addEventListener('change', loadEvents, { signal });
    login.addEventListener('click', () => byId('user-info-toggle')?.click(), { signal });
    list.addEventListener('click', event => {
        const id = event.target.closest('button[data-event-id]')?.dataset.eventId;
        if (id && (!currentTournament || currentTournament.id !== id)) void chooseEvent(id);
    }, { signal });
    pairings.addEventListener('click', event => {
        const source = event.target.closest('button[data-bracket-source]');
        if (source) {
            const match = pairings.querySelector(`[data-bracket-node="${source.dataset.bracketSource}"]`);
            match?.focus({ preventScroll: true });
            match?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
            return;
        }
        const id = event.target.closest('button[data-tournament-match-id]')?.dataset.tournamentMatchId;
        if (id && currentTournament) void openMatch(id, currentTournament.id);
        const stage = event.target.closest('button[data-schedule-stage]');
        if (stage && currentDetails) {
            selectedScheduleStage = Number(stage.dataset.scheduleStage);
            renderTournamentSchedule(pairings, currentDetails, selectedScheduleStage);
            pairings.querySelector('[data-schedule-stage="' + selectedScheduleStage + '"]')?.focus();
        }
        const player = event.target.closest('button[data-stage-entry-id]');
        if (player) void openPlayerProfile(player);
    }, { signal });
    document.addEventListener(USER_SESSION_CHANGED_EVENT, () => {
        if (rememberedEventId) void chooseEvent(rememberedEventId);
    }, { signal });
    void loadEvents();
}
