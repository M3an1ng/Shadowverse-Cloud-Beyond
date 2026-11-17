import { getDeckClassDisplay } from './deckPresentation.js';
import { createDeckPreviewModal, loadDeckPreviewCards } from './components/deckPreviewModal.js';
import { fetchTournamentJson, tournamentElement as item, tournamentStages } from './tournamentUi.js';

const byId = id => document.getElementById(id);
const stageLabel = phase => tournamentStages[phase.kind];
const reportPath = id => '/api/tournaments/' + encodeURIComponent(id) + '/report';
const rateFormat = new Intl.NumberFormat('zh-CN', { style: 'percent', maximumFractionDigits: 1 });
const pageLength = 40;

function dialogLifetime(dialog, parentSignal) {
    if (dialog.open || parentSignal.aborted) return null;
    const controller = new AbortController();
    const { signal } = controller;
    parentSignal.addEventListener('abort', () => dialog.close(), { once: true, signal });
    dialog.addEventListener('close', () => controller.abort(), { once: true, signal });
    dialog.showModal();
    return signal;
}

function setStatus(node, message, error = false) {
    node.textContent = message;
    node.dataset.error = String(error);
}

function setStageOptions(select, stages, label, current = '0') {
    select.replaceChildren(new Option(label, '0'), ...stages.map(phase => new Option(stageLabel(phase), phase.number)));
    select.value = Array.from(select.options).some(option => option.value === current) ? current : '0';
}

export async function openTournamentClassifications(id, parentSignal) {
    const dialog = byId('tournament-classification-dialog');
    const signal = dialogLifetime(dialog, parentSignal);
    if (!signal) return;
    const path = '/api/admin/tournaments/' + encodeURIComponent(id) + '/classifications';
    const stage = byId('tournament-classification-stage');
    const search = byId('tournament-classification-search');
    const save = byId('tournament-classification-save');
    const download = byId('tournament-classification-download');
    const status = byId('tournament-classification-status');
    const suggestions = byId('tournament-classification-types');
    const grid = byId('tournament-classification-decks');
    const preview = createDeckPreviewModal({ idPrefix: 'tournament-classification-preview' });
    const pages = byId('tournament-classification-pages');
    const prev = byId('tournament-classification-prev');
    const next = byId('tournament-classification-next');
    const changes = new Map();
    let decks = [];
    let byDeck = new Map();
    let names = new Map();
    let missing = 0;
    let page = 0;
    let saving = false;
    let previewRequest;

    download.href = reportPath(id) + '.xlsx';
    download.hidden = true;
    pages.hidden = true;
    stage.disabled = true;
    search.disabled = true;
    search.value = '';
    save.disabled = true;
    grid.replaceChildren();
    suggestions.replaceChildren();
    setStatus(status, '正在读取锁定的参赛卡组…');

    function value(row) {
        return changes.get(row.deckId) ?? row.archetype ?? '';
    }

    function updateControls(message) {
        save.disabled = saving || changes.size === 0;
        search.disabled = saving;
        stage.disabled = saving;
        download.hidden = saving || missing > 0 || changes.size > 0;
        setStatus(status, '已分类 ' + (decks.length - missing) + ' / ' + decks.length + ' 套'
            + (changes.size ? ' · ' + changes.size + ' 项待保存' : '') + (message ? ' · ' + message : ''));
    }

    function updateSuggestions() {
        const labels = [...new Set(decks.map(row => value(row).trim()).filter(Boolean))];
        labels.sort((a, b) => a.localeCompare(b, 'zh-CN'));
        suggestions.replaceChildren(...labels.map(label => new Option(label, label)));
    }

    function renderRows() {
        const number = Number(stage.value);
        const query = search.value.trim().toLowerCase();
        const filtered = decks.filter(row => (!number || row.stages.includes(number))
            && (!query || (row.playerName + ' ' + row.deckName + ' ' + row.classLabel + ' ' + value(row)).toLowerCase().includes(query)));
        const pageCount = Math.max(1, Math.ceil(filtered.length / pageLength));
        page = Math.max(0, Math.min(page, pageCount - 1));
        grid.replaceChildren(...filtered.slice(page * pageLength, (page + 1) * pageLength).map(row => {
            const card = item('div', '', 'tournament-classification-row');
            const deck = item('div', '', 'tournament-classification-deck-name');
            deck.append(item('strong', row.deckName), item('span',
                [row.classLabel, ...row.stages.map(number => names.get(number))].join(' · '), 'tournament-muted'));
            const input = item('input');
            input.type = 'text';
            input.maxLength = 64;
            input.autocomplete = 'off';
            input.placeholder = '卡组类型';
            input.value = value(row);
            input.disabled = saving;
            input.dataset.classificationDeck = row.deckId;
            input.setAttribute('list', suggestions.id);
            input.setAttribute('aria-label', row.playerName + ' · ' + row.deckName + '的卡组类型');
            const inspect = item('button', '预览', 'tournament-button');
            inspect.type = 'button';
            inspect.dataset.previewDeck = row.deckId;
            inspect.setAttribute('aria-haspopup', 'dialog');
            card.append(item('span', row.playerName, 'tournament-classification-owner'), deck, input, inspect);
            return card;
        }));
        if (!filtered.length) grid.append(item('p', decks.length ? '没有找到匹配的卡组。' : '当前没有锁定的参赛卡组。', 'tournament-muted'));
        pages.hidden = pageCount <= 1;
        prev.disabled = page === 0;
        next.disabled = page + 1 >= pageCount;
        byId('tournament-classification-page').textContent = (page + 1) + ' / ' + pageCount;
    }

    function disposePreview() {
        previewRequest?.abort();
        previewRequest = null;
        if (preview.isOpen()) preview.close();
    }

    async function openPreview(row, button) {
        disposePreview();
        const request = new AbortController();
        previewRequest = request;
        const stageNumber = Number(stage.value) || row.stages[0];
        button.disabled = true;
        button.textContent = '读取中…';
        try {
            const result = await fetchTournamentJson('/api/tournaments/' + encodeURIComponent(id)
                + '/entries/' + encodeURIComponent(row.entryId) + '?stage=' + stageNumber, { signal: request.signal });
            const deck = result.decks.find(item => item.id === row.deckId);
            if (!deck) throw new Error('找不到这套参赛卡组。');
            const cards = await loadDeckPreviewCards(deck);
            if (request.signal.aborted || !dialog.open || !button.isConnected) return;
            button.disabled = false;
            button.focus({ preventScroll: true });
            preview.open({
                titleText: deck.name,
                metaText: [row.playerName, row.classLabel, names.get(stageNumber)].join(' · '),
                cards,
                stats: deck.stats
            });
        } catch (error) {
            if (!request.signal.aborted && dialog.open) setStatus(status, error.message, true);
        } finally {
            if (previewRequest === request) previewRequest = null;
            button.disabled = false;
            button.textContent = '预览';
        }
    }

    signal.addEventListener('abort', () => {
        disposePreview();
        preview.dispose();
    }, { once: true });

    try {
        const result = await fetchTournamentJson(path, { signal });
        if (signal.aborted) return;
        byId('tournament-classification-title').textContent = '卡组分类 · ' + result.name;
        names = new Map(result.stages.map(phase => [phase.number, stageLabel(phase)]));
        decks = result.decks.map(row => ({ ...row, classLabel: getDeckClassDisplay(row.class, result.deckRule, row.secondaryClass) }));
        byDeck = new Map(decks.map(row => [row.deckId, row]));
        missing = decks.filter(row => !row.archetype).length;
        setStageOptions(stage, result.stages, '全部阶段');
        renderRows();
        updateSuggestions();
        updateControls();
    } catch (error) {
        if (!signal.aborted) setStatus(status, error.message, true);
        return;
    }

    grid.addEventListener('input', event => {
        const row = byDeck.get(event.target.dataset.classificationDeck);
        if (!row) return;
        const label = event.target.value;
        if (label.trim() === (row.archetype ?? '')) changes.delete(row.deckId);
        else changes.set(row.deckId, label);
        updateControls();
    }, { signal });
    grid.addEventListener('change', updateSuggestions, { signal });
    grid.addEventListener('click', event => {
        const btn = event.target.closest('button[data-preview-deck]');
        const row = byDeck.get(btn?.dataset.previewDeck);
        if (row) void openPreview(row, btn);
    }, { signal });
    const filter = () => {
        page = 0;
        disposePreview();
        renderRows();
    };
    stage.addEventListener('change', filter, { signal });
    search.addEventListener('input', filter, { signal });
    prev.addEventListener('click', () => { page--; renderRows(); }, { signal });
    next.addEventListener('click', () => { page++; renderRows(); }, { signal });

    save.addEventListener('click', async () => {
        if (saving || changes.size === 0) return;
        saving = true;
        updateControls('正在保存');
        for (const input of grid.querySelectorAll('input')) input.disabled = true;
        const changesToSave = [...changes].map(([deckId, text]) => ({
            deckId, archetype: text.trim().normalize('NFC') || null, previous: byDeck.get(deckId).archetype
        }));
        try {
            await fetchTournamentJson(path, {
                method: 'PUT', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ changes: changesToSave }), signal
            });
            if (signal.aborted) return;
            for (const change of changesToSave) byDeck.get(change.deckId).archetype = change.archetype;
            changes.clear();
            missing = decks.filter(row => !row.archetype).length;
            updateSuggestions();
            saving = false;
            updateControls('分类已保存');
            renderRows();
        } catch (error) {
            if (signal.aborted) return;
            saving = false;
            updateControls();
            for (const input of grid.querySelectorAll('input')) input.disabled = false;
            setStatus(status, error.message, true);
        }
    }, { signal });
}

const views = {
    classes: '职业统计', archetypes: '卡组统计', classMatchups: '职业对阵',
    archetypeMatchups: '卡组对阵', advancement: '晋级统计'
};
const rate = value => ({ rate: value });

function tableView(content, headings, rows, note = '') {
    content.replaceChildren();
    if (!rows.length) {
        content.append(item('p', '此范围暂无数据。', 'tournament-muted'));
        return;
    }
    const wrap = item('div', '', 'tournament-report-table-wrap');
    const table = item('table', '', 'tournament-report-table');
    const header = table.createTHead().insertRow();
    for (const label of headings) {
        const th = item('th', label);
        th.scope = 'col';
        header.append(th);
    }
    const body = table.createTBody();
    for (const values of rows) {
        const tr = body.insertRow();
        for (const value of values) {
            const ratio = typeof value === 'object';
            const td = item('td', String(ratio ? value.rate == null ? '—' : rateFormat.format(value.rate) : value));
            if (ratio && value.rate != null) {
                td.className = 'tournament-report-rate';
                td.style.setProperty('--rate', (value.rate * 100) + '%');
            }
            tr.append(td);
        }
    }
    wrap.append(table);
    content.append(wrap);
    if (note) content.append(item('p', note, 'tournament-muted'));
}

export async function openTournamentReport(id, parentSignal) {
    const dialog = byId('tournament-report-dialog');
    const signal = dialogLifetime(dialog, parentSignal);
    if (!signal) return;
    const stage = byId('tournament-report-stage');
    const view = byId('tournament-report-view');
    const summary = byId('tournament-report-totals');
    const status = byId('tournament-report-status');
    const content = byId('tournament-report-content');
    const subject = byId('tournament-report-subject');
    const subjectLabel = byId('tournament-report-subject-label');
    const refresh = byId('tournament-report-refresh');
    const download = byId('tournament-report-download');
    let report;
    let loading = false;

    stage.replaceChildren(new Option('全赛事', '0'));
    view.replaceChildren(...Object.entries(views).map(([key, label]) => new Option(label, key)));
    summary.replaceChildren();
    content.replaceChildren();
    subjectLabel.hidden = true;
    download.hidden = true;
    download.href = reportPath(id) + '.xlsx';

    function render() {
        subjectLabel.hidden = true;
        if (!report?.totals) return;
        const category = view.value;
        if (category === 'classMatchups' || category === 'archetypeMatchups') {
            const rows = report[category];
            const selected = subject.value;
            subject.replaceChildren(...Array.from(new Set(rows.map(row => row.name)), name => new Option(name, name)));
            if (selected && Array.from(subject.options).some(option => option.value === selected)) subject.value = selected;
            subjectLabel.hidden = rows.length === 0;
            tableView(content, ['对方类型', '出场', '胜场', '胜率'],
                rows.filter(row => row.name === subject.value).map(row => [row.opponent, row.games, row.wins, rate(row.winRate)]),
                '同类对局计入双方出场。');
        } else if (category === 'advancement') {
            tableView(content, ['名次', '选手', '胜负', '对手胜场', '小局', '晋级', '卡组类型'],
                report.advancement.map(row => [row.rank, row.playerName, row.wins + '–' + row.losses, row.opponentWins,
                    row.gameWins + '–' + row.gameLosses, row.qualified ? (row.acceptedLineup === false ? '卡组不足' : '已晋级') : '未晋级',
                    row.archetypes.join(' · ')]));
        } else {
            const formats = report.stages.filter(phase => !report.stage || report.stage === phase.number)
                .map(phase => phase.matchFormat);
            const hasBans = formats.includes('bo3Ban1');
            const showBanChances = hasBans && formats.some(format => format !== 'bo3Ban1');
            const headings = [views[category].replace('统计', ''), '阵容', '小局', '胜场', '胜率', '大局'];
            if (hasBans) headings.push(showBanChances ? '被 Ban / 可 Ban 大局' : '被 Ban', 'Ban 率');
            headings.push('先手胜率', '后手胜率');
            const rows = report[category].map(row => {
                const cells = [row.name, row.lineupDecks, row.appearances, row.wins, rate(row.winRate), row.matchLineups];
                if (hasBans) cells.push(showBanChances ? row.bans + ' / ' + row.banChances : row.bans, rate(row.banRate));
                cells.push(rate(row.firstWinRate), rate(row.secondWinRate));
                return cells;
            });
            tableView(content, headings, rows, '小局胜率按出场统计，包含平局。'
                + (hasBans ? 'Ban 率按进入可被 Ban 的已结算大局计数。' : ''));
        }
    }

    async function load() {
        if (loading) return;
        loading = true;
        stage.disabled = true;
        view.disabled = true;
        refresh.disabled = true;
        download.hidden = true;
        setStatus(status, '正在读取赛事数据…');
        try {
            report = await fetchTournamentJson(reportPath(id) + '?stage=' + Number(stage.value), { signal });
            if (signal.aborted) return;
            byId('tournament-report-title').textContent = '赛事战报 · ' + report.name;
            setStageOptions(stage, report.stages, '全赛事', String(report.stage));
            summary.replaceChildren();
            content.replaceChildren();
            subjectLabel.hidden = true;
            download.hidden = report.allUnclassified > 0;
            view.querySelector('[value="advancement"]').disabled = report.advancement.length === 0;
            if (!report.totals) {
                setStatus(status, '此范围还有 ' + report.unclassified + ' 套卡组待分类。');
                return;
            }
            if (view.selectedOptions[0]?.disabled) view.value = 'classes';
            const totals = report.totals;
            const known = totals.gamesWithKnownOrder;
            const displayed = [['选手', totals.players], ['大局', totals.completedMatches], ['小局', totals.games],
                ['先手胜率', known ? rateFormat.format(totals.firstPlayerWins / known) : '—']];
            for (const [label, value] of displayed) {
                const cell = item('div');
                cell.append(item('dt', label), item('dd', String(value)));
                summary.append(cell);
            }
            setStatus(status, report.status === 'finished' ? '赛事已结束' : '统计截至当前已结算的对局'
                + (totals.pendingMatches ? ' · ' + totals.pendingMatches + ' 场待结算' : ''));
            render();
        } catch (error) {
            if (!signal.aborted) setStatus(status, error.message, true);
        } finally {
            if (!signal.aborted) {
                loading = false;
                stage.disabled = !report;
                view.disabled = !report?.totals;
                refresh.disabled = false;
            }
        }
    }

    stage.addEventListener('change', load, { signal });
    view.addEventListener('change', render, { signal });
    subject.addEventListener('change', render, { signal });
    refresh.addEventListener('click', load, { signal });
    await load();
}
