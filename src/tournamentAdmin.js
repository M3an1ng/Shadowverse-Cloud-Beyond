import { generateGuid, getUserSession, USER_SESSION_CHANGED_EVENT } from './userSession.js';
import { ensureDeckEnvironmentOptions, fillDeckEnvironmentSelect } from './deckEnvironmentOptions.js';
import { confirmDialog, showDialog } from './siteDialog.js';
import { describeTournamentStage, fetchTournamentJson, isTournamentDeckSubmissionOpen, tournamentFormats } from './tournamentUi.js';

const endpoint = '/api/admin/tournaments';
const statusLabels = { draft: '草稿', published: '已发布', running: '进行中', finished: '已结束' };
const beijingInputFormat = new Intl.DateTimeFormat('sv-SE', {
    timeZone: 'Asia/Shanghai',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23'
});

const isAdmin = () => getUserSession().role === 'admin';
let entryLifetime;
let pageLifetime;

export function initializeTournamentAdminEntry() {
    cleanupTournamentAdminEntry();
    const entry = document.getElementById('tournament-admin-entry');
    if (!entry) return;
    entryLifetime = new AbortController();
    const syncEntry = () => { entry.hidden = !isAdmin(); };
    document.addEventListener(USER_SESSION_CHANGED_EVENT, syncEntry, { signal: entryLifetime.signal });
    syncEntry();
}

export function cleanupTournamentAdminEntry() {
    entryLifetime?.abort();
}

export function cleanupTournamentAdmin() {
    pageLifetime?.abort();
}

function toBeijingInput(value) {
    return value ? beijingInputFormat.format(new Date(value)).replace(' ', 'T') : '';
}

export async function initializeTournamentAdmin() {
    cleanupTournamentAdmin();
    pageLifetime = new AbortController();
    const { signal } = pageLifetime;
    const workspace = document.getElementById('tournament-admin-workspace');
    const list = document.getElementById('tournament-admin-list');
    const form = document.getElementById('tournament-admin-form');
    const formTitle = document.getElementById('tournament-admin-form-title');
    const fields = document.getElementById('tournament-admin-fields');
    const stageFields = Object.fromEntries(['plaza', 'swiss', 'elimination'].map(name =>
        [name, document.getElementById('tournament-admin-' + name)]));
    const status = document.getElementById('tournament-admin-status');
    const refresh = document.getElementById('tournament-admin-refresh');
    const create = document.getElementById('tournament-admin-new');
    const retry = document.getElementById('tournament-admin-retry');
    const submitBar = document.getElementById('tournament-admin-submit');
    const publish = document.getElementById('tournament-admin-publish');
    const registration = document.getElementById('tournament-admin-registration-controls');
    const saveCutoff = document.getElementById('tournament-admin-save-cutoff');
    const closeRegistration = document.getElementById('tournament-admin-close-registration');
    const remove = document.getElementById('tournament-admin-delete');
    const classify = document.getElementById('tournament-admin-classify');
    const field = name => form.elements.namedItem(name);

    let drafts = [];
    let draftId;
    let revision;
    let selectedStatus = 'draft';
    let busy = false;
    let registrationTimer;
    let cutoffStage = 1;
    let cutoffValue;

    signal.addEventListener('abort', () => window.clearTimeout(registrationTimer), { once: true });

    function showStatus(text, error = false) {
        status.textContent = text;
        status.dataset.error = String(error);
    }

    function setBusy(value) {
        busy = value;
        fields.disabled = value || selectedStatus !== 'draft';
        refresh.disabled = value;
        create.disabled = value;
        retry.disabled = value;
        list.querySelectorAll('button').forEach(button => { button.disabled = value; });
        syncManagementControls();
    }

    function syncManagementControls() {
        window.clearTimeout(registrationTimer);
        const selected = drafts.find(draft => draft.id === draftId);
        remove.hidden = !selected;
        remove.disabled = busy;
        classify.hidden = !selected?.stages.some(stage => stage.startedAtUtc);
        classify.disabled = busy;
        const now = Date.now();
        const laterStage = selected?.stages.find(stage => isTournamentDeckSubmissionOpen(stage, now));
        cutoffStage = laterStage?.number ?? 1;
        const until = laterStage?.deckSubmissionClosesAtUtc
            ?? (selected?.status === 'published' ? selected.registrationClosesAtUtc : null);
        const opens = laterStage ? now : Date.parse(selected?.registrationOpensAtUtc);
        const closes = until ? Date.parse(until) : Infinity;
        registration.hidden = !(selected?.status === 'published' || laterStage) || !(now < closes);
        if (until !== cutoffValue) {
            field('registrationDeadline').value = toBeijingInput(until);
            cutoffValue = until;
        }
        document.getElementById('tournament-admin-cutoff-title').textContent = laterStage ? '阶段卡组重交' : '报名管理';
        document.getElementById('tournament-admin-cutoff-label').textContent = laterStage ? '重交截止（北京时间）' : '报名截止（北京时间）';
        closeRegistration.textContent = laterStage ? '立即截止重交' : '立即截止报名';
        field('registrationDeadline').disabled = busy;
        saveCutoff.disabled = busy;
        closeRegistration.disabled = busy || now < opens;
        const boundary = now < opens ? opens : closes;
        if (!registration.hidden && Number.isFinite(boundary)) {
            registrationTimer = window.setTimeout(syncManagementControls,
                Math.max(150, Math.min(2147483647, boundary - now + 30)));
        }
    }

    function syncFormat() {
        const major = field('format').value === 'major';
        const stageModeInput = field('stageMode');
        const stageMode = stageModeInput.value;
        stageModeInput.closest('label').hidden = !major;
        stageModeInput.disabled = !major;
        field('playerLimit').placeholder = major ? '留空不限' : '默认 16 人';
        const activeStages = {
            plaza: !major,
            swiss: major && stageMode !== 'elimination',
            elimination: major && stageMode !== 'swiss'
        };
        for (const [name, active] of Object.entries(activeStages)) {
            stageFields[name].hidden = !active;
            stageFields[name].disabled = !active;
        }
        field('swissRounds').required = activeStages.swiss;
        const finalReset = field('allowGrandFinalReset');
        finalReset.disabled = !activeStages.elimination || field('elimination').value !== 'doubleElimination';
        finalReset.closest('label').hidden = finalReset.disabled;
        const advancing = field('advancingPlayers');
        advancing.required = major && stageMode === 'both';
        advancing.disabled = !advancing.required;
        advancing.closest('label').hidden = advancing.disabled;
        const minutes = field('deckSubmissionMinutes');
        minutes.disabled = !advancing.required || !field('eliminationResubmission').checked;
        minutes.closest('label').hidden = minutes.disabled;
    }

    function renderList() {
        list.replaceChildren();
        if (drafts.length === 0) {
            const empty = document.createElement('li');
            empty.textContent = '还没有赛事。';
            empty.className = 'tournament-admin-note';
            list.append(empty);
            return;
        }
        for (const draft of drafts) {
            const item = document.createElement('li');
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'tool-button';
            button.dataset.draftId = draft.id;
            button.setAttribute('aria-current', String(draft.id === draftId));
            button.disabled = busy;
            const title = document.createElement('strong');
            title.textContent = draft.name;
            const details = document.createElement('span');
            details.className = 'tournament-admin-note';
            details.textContent = [draft.cardVersion, tournamentFormats[draft.format], statusLabels[draft.status],
                draft.stages.map(describeTournamentStage).join(' → ')].join(' · ');
            button.append(title, details);
            item.append(button);
            list.append(item);
        }
    }

    function selectDraft(draft) {
        form.reset();
        cutoffValue = undefined;
        draftId = draft?.id ?? generateGuid();
        revision = draft?.revision ?? 0;
        selectedStatus = draft?.status ?? 'draft';
        submitBar.hidden = selectedStatus !== 'draft';
        fields.disabled = busy || selectedStatus !== 'draft';
        formTitle.textContent = draft
            ? (selectedStatus === 'draft' ? '编辑赛事草稿' : '赛事设置 · ' + statusLabels[selectedStatus])
            : '新建赛事';
        if (draft) {
            for (const name of ['name', 'cardVersion', 'format', 'playerLimit', 'deckRule']) {
                field(name).value = draft[name] ?? '';
            }
            const environment = field('deckEnvironment');
            if (!Array.from(environment.options).some(option => option.value === draft.deckEnvironment)) {
                environment.add(new Option(draft.deckEnvironment, draft.deckEnvironment));
            }
            environment.value = draft.deckEnvironment;
            const swiss = draft.stages.find(stage => stage.kind === 'swiss');
            const elimination = draft.stages.find(stage =>
                stage.kind === 'singleElimination' || stage.kind === 'doubleElimination');
            if (draft.format === 'major') {
                field('stageMode').value = swiss && elimination ? 'both' : swiss ? 'swiss' : 'elimination';
            }
            field('swissRounds').value = swiss?.rounds ?? 4;
            field('elimination').value = elimination?.kind ?? 'singleElimination';
            field('allowGrandFinalReset').checked = elimination?.allowGrandFinalReset ?? false;
            field('advancingPlayers').value = elimination?.advancingPlayers ?? 8;
            field('deckSubmissionMinutes').value = elimination?.deckSubmissionMinutes ?? '';
            for (const stage of draft.stages) {
                const prefix = stage.kind === 'plaza' || stage.kind === 'swiss' ? stage.kind : 'elimination';
                field(prefix + 'MatchFormat').value = stage.matchFormat;
                field(prefix + 'Resubmission').checked = stage.allowDeckResubmission;
            }
            field('registrationOpensAt').value = toBeijingInput(draft.registrationOpensAtUtc);
            field('registrationClosesAt').value = toBeijingInput(draft.registrationClosesAtUtc);
        }
        syncFormat();
        renderList();
        syncManagementControls();
    }

    async function loadDrafts() {
        if (busy) return;
        if (!isAdmin()) {
            workspace.hidden = true;
            retry.hidden = true;
            showStatus('请以管理员身份登录。', true);
            return;
        }
        setBusy(true);
        showStatus('正在读取赛事…');
        retry.hidden = true;
        try {
            const [loaded] = await Promise.all([
                fetchTournamentJson(endpoint, { signal }),
                ensureDeckEnvironmentOptions()
            ]);
            if (signal.aborted || !isAdmin()) return;
            drafts = loaded;
            fillDeckEnvironmentSelect(field('deckEnvironment'), 'rotation');
            selectDraft(drafts.find(draft => draft.id === draftId));
            workspace.hidden = false;
            showStatus('');
        } catch (error) {
            if (signal.aborted) return;
            workspace.hidden = true;
            retry.hidden = error.status === 401 || error.status === 403;
            showStatus(error.message, true);
        } finally {
            if (!signal.aborted) setBusy(false);
        }
    }

    async function saveDraft(event) {
        event.preventDefault();
        if (busy || selectedStatus !== 'draft') return;
        const publishing = event.submitter === publish;
        const format = field('format').value;
        const major = format === 'major';
        const stageMode = field('stageMode').value;
        const optionalNumber = name => field(name).value === '' ? null : field(name).valueAsNumber;
        const beijingTime = name => field(name).value ? field(name).value + ':00+08:00' : null;
        const stageRules = prefix => ({
            matchFormat: field(prefix + 'MatchFormat').value,
            allowDeckResubmission: field(prefix + 'Resubmission').checked
        });
        const stages = [];
        if (!major) {
            stages.push({ kind: 'plaza', ...stageRules('plaza') });
        }
        if (major && stageMode !== 'elimination') {
            stages.push({ kind: 'swiss', rounds: optionalNumber('swissRounds'), ...stageRules('swiss') });
        }
        if (major && stageMode !== 'swiss') {
            stages.push({
                kind: field('elimination').value,
                allowGrandFinalReset: !field('allowGrandFinalReset').disabled && field('allowGrandFinalReset').checked,
                advancingPlayers: stageMode === 'both' ? optionalNumber('advancingPlayers') : null,
                deckSubmissionMinutes: field('deckSubmissionMinutes').disabled ? null : optionalNumber('deckSubmissionMinutes'),
                ...stageRules('elimination')
            });
        }
        const payload = {
            name: field('name').value.trim(),
            cardVersion: field('cardVersion').value.trim(),
            format,
            deckEnvironment: field('deckEnvironment').value,
            deckRule: field('deckRule').value,
            playerLimit: optionalNumber('playerLimit'),
            registrationOpensAt: beijingTime('registrationOpensAt'),
            registrationClosesAt: beijingTime('registrationClosesAt'),
            stages,
            revision
        };

        setBusy(true);
        try {
            if (publishing && !await confirmDialog('发布后玩家可以进入赛事页面，赛事设置将锁定。确认发布？')) return;
            if (signal.aborted || !isAdmin()) return;
            showStatus(publishing ? '正在发布赛事…' : '正在保存草稿…');
            const savePath = endpoint + '/' + draftId;
            let saved = await fetchTournamentJson(savePath, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
                signal
            });
            if (signal.aborted || !isAdmin()) return;
            drafts = [saved, ...drafts.filter(draft => draft.id !== saved.id)];
            selectDraft(saved);
            if (publishing) {
                saved = await fetchTournamentJson(savePath + '/publish', {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ revision: saved.revision }),
                    signal
                });
                if (signal.aborted || !isAdmin()) return;
                drafts[0] = saved;
                selectDraft(saved);
            }
            showStatus(publishing ? '赛事已发布。' : '草稿已保存。');
        } catch (error) {
            if (signal.aborted) return;
            if (error.status === 401 || error.status === 403) workspace.hidden = true;
            showStatus(error.message, true);
        } finally {
            if (!signal.aborted) setBusy(false);
        }
    }

    async function updateCutoff(closeNow) {
        if (busy || registration.hidden || !isAdmin()) return;
        const deadline = field('registrationDeadline');
        const action = cutoffStage === 1 ? '报名' : '重交';
        if (!closeNow && !deadline.value) {
            showStatus('请填写新的' + action + '截止时间。', true);
            deadline.focus();
            return;
        }
        const payload = {
            revision, closeNow, stageNumber: cutoffStage,
            registrationClosesAt: closeNow ? null : deadline.value + ':00+08:00'
        };
        setBusy(true);
        try {
            if (closeNow && !await showDialog({
                title: '立即截止' + action,
                message: cutoffStage === 1 ? '报名会立即关闭，已提交的参赛卡组随即公开。'
                    : '重交会立即关闭，符合卡组要求的晋级选手将进入赛程。',
                cancelText: '取消',
                confirmText: '确认截止',
                tone: 'danger'
            })) return;
            if (signal.aborted || !isAdmin()) return;
            const saved = await fetchTournamentJson(endpoint + '/' + draftId + '/registration/cutoff', {
                method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload), signal
            });
            if (signal.aborted || !isAdmin()) return;
            drafts = drafts.map(draft => draft.id === saved.id ? saved : draft);
            selectDraft(saved);
            showStatus(closeNow ? action + '已截止。' : action + '截止时间已更新。');
        } catch (error) {
            if (signal.aborted) return;
            if (error.status === 401 || error.status === 403) workspace.hidden = true;
            showStatus(error.message, true);
        } finally {
            if (!signal.aborted) setBusy(false);
        }
    }

    async function deleteTournament() {
        const selected = drafts.find(draft => draft.id === draftId);
        if (!selected || busy || !isAdmin()) return;
        setBusy(true);
        try {
            const confirmed = await showDialog({
                title: '删除赛事',
                message: '删除「' + selected.name + '」？报名、卡组快照及赛程记录会一并删除。',
                cancelText: '取消',
                confirmText: '确认删除',
                tone: 'danger'
            });
            if (!confirmed || signal.aborted || !isAdmin()) return;
            await fetchTournamentJson(endpoint + '/' + selected.id + '?revision=' + selected.revision,
                { method: 'DELETE', signal });
            if (signal.aborted || !isAdmin()) return;
            drafts = drafts.filter(draft => draft.id !== selected.id);
            selectDraft(drafts[0]);
            showStatus('赛事已删除。');
        } catch (error) {
            if (signal.aborted) return;
            if (error.status === 401 || error.status === 403) workspace.hidden = true;
            showStatus(error.message, true);
        } finally {
            if (!signal.aborted) setBusy(false);
        }
    }

    workspace.hidden = true;
    form.addEventListener('submit', saveDraft, { signal });
    field('format').addEventListener('change', syncFormat, { signal });
    field('stageMode').addEventListener('change', syncFormat, { signal });
    field('elimination').addEventListener('change', syncFormat, { signal });
    field('eliminationResubmission').addEventListener('change', syncFormat, { signal });
    list.addEventListener('click', event => {
        const id = event.target.closest('button[data-draft-id]')?.dataset.draftId;
        if (id && !busy) {
            selectDraft(drafts.find(draft => draft.id === id));
            showStatus('');
            formTitle.focus();
        }
    }, { signal });
    create.addEventListener('click', () => {
        selectDraft();
        showStatus('');
        field('name').focus();
    }, { signal });
    refresh.addEventListener('click', loadDrafts, { signal });
    retry.addEventListener('click', loadDrafts, { signal });
    remove.addEventListener('click', deleteTournament, { signal });
    classify.addEventListener('click', async () => {
        const id = draftId;
        const { openTournamentClassifications } = await import('./tournamentReports.js');
        if (!signal.aborted && id === draftId) void openTournamentClassifications(id, signal);
    }, { signal });
    saveCutoff.addEventListener('click', () => void updateCutoff(false), { signal });
    closeRegistration.addEventListener('click', () => void updateCutoff(true), { signal });
    document.addEventListener(USER_SESSION_CHANGED_EVENT, () => {
        if (!isAdmin()) {
            workspace.hidden = true;
            list.replaceChildren();
            retry.hidden = true;
            showStatus('请以管理员身份登录。', true);
        }
    }, { signal });
    await loadDrafts();
}
