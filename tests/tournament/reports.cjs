const assert = require('node:assert/strict');
const { join } = require('node:path');
const { base, adminApi, eventsApi, account, publish, enroll, setCutoff, read, waitFor, standardClasses } =
    require('./fixtures.cjs');
const { assertUi, launchBrowser, loginPage, screenshot } = require('./browser.cjs');
const { playStage } = require('./scheduling.cjs');

function path(id) { return eventsApi + '/' + id; }
function classificationPath(id) { return adminApi + '/' + id + '/classifications'; }

function classification(row, formulaDeck) {
    if (row.deckId === formulaDeck) return '=1+1';
    return { forestcraft: '疾驰妖', swordcraft: '连击皇', runecraft: '控场法' }[row.class.toLowerCase()];
}

async function openAdmin(browser, admin, id) {
    const page = await loginPage(browser, admin);
    await page.locator('.nav-item[data-route="dev-tools"]').click();
    await page.getByRole('button', { name: '管理赛事', exact: true }).click();
    await page.locator('button[data-draft-id="' + id + '"]').click();
    await page.getByRole('button', { name: '卡组分类与导出' }).click();
    await assertUi(page.locator('#tournament-classification-dialog')).toBeVisible();
    return page;
}

async function classify(browser, admin, id, placements) {
    const response = await read(classificationPath(id), { player: admin });
    assert.equal(response.decks.length, 9);
    assert.ok(response.decks.every(deck => deck.stages.length === 1 && deck.stages[0] === 1));
    const formula = response.decks.find(deck => deck.entryId === placements.entryOneId && deck.class === 'forestcraft');
    assert.ok(formula);
    const page = await openAdmin(browser, admin, id);
    const dialog = page.locator('#tournament-classification-dialog');
    const save = dialog.getByRole('button', { name: '保存分类' });
    const status = dialog.locator('#tournament-classification-status');
    const download = dialog.getByRole('link', { name: '下载 Excel' });
    const inputs = dialog.locator('input[data-classification-deck]');
    const search = dialog.locator('#tournament-classification-search');
    const formulaInput = dialog.locator('[data-classification-deck="' + formula.deckId + '"]');
    const inspect = dialog.locator('button[data-preview-deck="' + formula.deckId + '"]');
    const preview = page.locator('#tournament-classification-preview-modal');
    await assertUi(inputs).toHaveCount(9);
    await assertUi(download).toBeHidden();
    await search.fill(formula.playerName);
    await formulaInput.fill('=1+1');
    await inspect.click();
    await assertUi(preview.locator('.deck-preview-card')).toHaveCount(14);
    await assertUi(preview.locator('.deck-preview-total')).toHaveText('40 / 40');
    await assertUi(preview.getByRole('heading', { name: formula.deckName })).toBeVisible();
    await screenshot(page, 'classification-preview.png');
    await preview.getByRole('button', { name: '关闭预览' }).click();
    await assertUi(preview).toBeHidden();
    await assertUi(inspect).toBeFocused();
    await page.setViewportSize({ width: 390, height: 844 });
    assert.equal(await dialog.locator('.tournament-report-dialog-body').evaluate(element => element.scrollWidth <= element.clientWidth + 1), true);
    await inspect.click();
    await assertUi(preview.getByRole('button', { name: '关闭预览' })).toBeFocused();
    await page.keyboard.press('Escape');
    await assertUi(preview).toBeHidden();
    await assertUi(dialog).toBeVisible();
    await assertUi(search).toHaveValue(formula.playerName);
    await assertUi(formulaInput).toHaveValue('=1+1');
    await search.fill('');
    await page.setViewportSize({ width: 1390, height: 880 });
    await save.click();
    await assertUi(status).toContainText('已分类 1 / 9');
    await assertUi(download).toBeHidden();
    await read(classificationPath(id), { method: 'PUT', status: 409, player: admin,
        payload: { changes: [{ deckId: formula.deckId, archetype: '过期编辑', previous: null }] } });
    assert.equal((await read(path(id) + '/report')).unclassified, 8);

    await search.fill(formula.playerName);
    await assertUi(inputs).toHaveCount(3);
    const previewRows = response.decks.filter(row => row.playerName === formula.playerName && row.deckId !== formula.deckId);
    for (const row of previewRows) {
        await dialog.locator('[data-classification-deck="' + row.deckId + '"]').fill(classification(row, formula.deckId));
    }
    await search.fill('');
    await assertUi(inputs).toHaveCount(9);
    for (const row of response.decks.filter(row => row.deckId !== formula.deckId)) {
        await dialog.locator('[data-classification-deck="' + row.deckId + '"]').fill(classification(row, formula.deckId));
    }
    await save.click();
    await assertUi(status).toContainText('已分类 9 / 9');
    await assertUi(download).toBeVisible();
    await page.close();
    return formula.deckId;
}

async function earlyDownload(id, room, player) {
    const own = room.one.isSelf ? room.two : room.one;
    assert.equal(room.phase, 'ban');
    await read('/api/tournament-matches/' + room.id + '/ban', {
        method: 'PUT', player, payload: { deckId: own.decks.at(-1).id }
    });
    const report = await read(path(id) + '/report');
    assert.equal(report.totals.completedMatches, 0);
    assert.equal(report.totals.pendingMatches, 1);
    assert.equal(report.classes.reduce((sum, row) => sum + row.bans, 0), 0);
    assert.equal(report.classes.reduce((sum, row) => sum + row.banChances, 0), 0);
    const file = await fetch(base + path(id) + '/report.xlsx');
    assert.equal(file.status, 200);
    const { writeFile } = require('node:fs/promises');
    const directory = process.env.TOURNAMENT_TEST_SCREENSHOTS;
    if (directory) await writeFile(join(directory, 'report-open-series.xlsx'), Buffer.from(await file.arrayBuffer()));
}

async function verifyNumbers(id, finished, players) {
    const report = await read(path(id) + '/report');
    const first = await read(path(id) + '/report?stage=1');
    const second = await read(path(id) + '/report?stage=2');
    assert.equal(report.status, 'finished');
    assert.deepEqual([first.totals.players, first.totals.lineupDecks, first.totals.completedMatches, first.totals.games], [3, 9, 1, 2]);
    assert.deepEqual([second.totals.players, second.totals.lineupDecks, second.totals.completedMatches, second.totals.games], [3, 3, 2, 2]);
    assert.deepEqual([report.totals.players, report.totals.lineupDecks, report.totals.completedMatches,
        report.totals.byes, report.totals.games, report.totals.draws], [3, 12, 3, 2, 4, 0]);
    for (const totals of [report.classes, report.archetypes]) {
        assert.deepEqual(['appearances','wins','banChances','bans','matchLineups'].map(key => totals.reduce((sum, row) => sum + row[key], 0)),
            [8, 4, 6, 2, 10]);
    }
    assert.equal(second.classes.every(row => row.banRate === null), true);
    assert.equal(report.advancement.filter(row => row.qualified && row.acceptedLineup).length, 3);
    assert.equal(report.classMatchups.reduce((sum, row) => sum + row.games, 0), 8);
    assert.equal(report.archetypeMatchups.reduce((sum, row) => sum + row.wins, 0), 4);
    const rooms = await Promise.all(finished.matches.filter(match => match.entryTwoId).map(match =>
        read('/api/tournament-matches/' + match.id, { player: players.get(match.entryOneId) })));
    const facts = rooms.flatMap(room => room.results);
    assert.equal(report.totals.gamesWithKnownOrder, facts.filter(fact => fact.firstPlayerEntryId).length);
    assert.equal(report.totals.firstPlayerWins, facts.filter(fact => fact.firstPlayerEntryId === fact.winnerEntryId).length);
    assert.equal(report.classes.reduce((sum, row) => sum + row.firstWins, 0), report.totals.firstPlayerWins);
}

async function verifyReportUi(browser, id, player) {
    const page = await loginPage(browser, player);
    await page.goto(base + '/?tournament=' + id + '#tournaments');
    const started = performance.now();
    await page.getByRole('button', { name: '赛事战报', exact: true }).click();
    const dialog = page.locator('#tournament-report-dialog');
    await assertUi(dialog.locator('#tournament-report-totals dd')).toHaveCount(4);
    await assertUi(dialog.locator('#tournament-report-status')).toHaveText('赛事已结束');
    await assertUi(dialog.getByRole('columnheader', { name: '被 Ban / 可 Ban 大局' })).toBeVisible();
    console.log('Browser report opened and rendered in ' + Math.round(performance.now() - started) + ' ms.');
    await screenshot(page, 'report-overview.png');
    const view = dialog.locator('#tournament-report-view');
    await view.selectOption('archetypes');
    await assertUi(dialog.getByRole('cell', { name: '进化妖', exact: true })).toBeVisible();
    await view.selectOption('archetypeMatchups');
    await assertUi(dialog.locator('#tournament-report-subject')).toBeVisible();
    await assertUi(dialog.locator('#tournament-report-subject option')).not.toHaveCount(0);
    await view.selectOption('advancement');
    await assertUi(dialog.getByRole('cell', { name: '已晋级', exact: true })).toHaveCount(3);
    const stage = dialog.locator('#tournament-report-stage');
    await stage.selectOption('2');
    await assertUi(dialog.locator('#tournament-report-view')).toHaveValue('classes');
    await assertUi(dialog.locator('#tournament-report-totals dd').nth(1)).toHaveText('2');
    await assertUi(dialog.getByRole('columnheader', { name: /ban/i })).toHaveCount(0);
    await page.setViewportSize({ width: 390, height: 844 });
    assert.equal(await dialog.locator('.tournament-report-dialog-body').evaluate(element => element.scrollWidth <= element.clientWidth + 1), true);
    await screenshot(page, 'report-mobile.png');
    await page.setViewportSize({ width: 1390, height: 880 });
    const event = page.waitForEvent('download');
    await dialog.getByRole('link', { name: '下载 Excel' }).click();
    const downloaded = await event;
    assert.match(downloaded.suggestedFilename(), /星云战报\.xlsx$/);
    const directory = process.env.TOURNAMENT_TEST_SCREENSHOTS;
    if (directory) await downloaded.saveAs(join(directory, 'report-sample.xlsx'));
}

async function verifyBothBanStages(browser, admin, pool) {
    const id = await publish(admin, {
        name: '双阶段 BO3 Ban1 战报验收', format: 'major',
        stages: [{ kind: 'swiss', rounds: 1, matchFormat: 'bo3Ban1' },
            { kind: 'singleElimination', advancingPlayers: 2, matchFormat: 'bo3Ban1' }]
    });
    const players = new Map();
    for (const [index, player] of pool.slice(0, 2).entries()) {
        const lineup = index === 1 ? [player.decks[0], player.decks[2], player.decks[1]] : player.decks;
        const record = await enroll(id, { ...player, decks: lineup }, 3);
        players.set(record.entryId, player);
    }
    await setCutoff(id, admin);
    await playStage(id, 1, players, () => 1);
    const finished = await playStage(id, 2, players);
    const { decks } = await read(classificationPath(id), { player: admin });
    await read(classificationPath(id), { method: 'PUT', player: admin, status: 204,
        payload: { changes: decks.map(deck => ({ deckId: deck.deckId, archetype: classification(deck), previous: null })) } });
    const report = await read(path(id) + '/report');
    assert.ok(report.archetypes.every(row => row.banChances === row.matchLineups));

    const page = await loginPage(browser, pool[0]);
    try {
        await page.goto(base + '/?tournament=' + id + '#tournaments');
        await page.getByRole('button', { name: '赛事战报', exact: true }).click();
        const dialog = page.locator('#tournament-report-dialog');
        await assertUi(dialog.getByRole('columnheader', { name: '被 Ban', exact: true })).toBeVisible();
        await assertUi(dialog.getByRole('columnheader', { name: /可 Ban 大局/ })).toHaveCount(0);
    } finally {
        await page.context().close();
    }
    const response = await fetch(base + path(id) + '/report.xlsx');
    assert.equal(response.status, 200);
    const directory = process.env.TOURNAMENT_TEST_SCREENSHOTS;
    if (directory) {
        const { writeFile } = require('node:fs/promises');
        await writeFile(join(directory, 'report-both-ban.xlsx'), Buffer.from(await response.arrayBuffer()));
        const first = finished.matches.find(match => match.stageNumber === 1 && match.entryTwoId);
        assert.equal(first.winnerEntryId, first.entryTwoId);
        const room = await read('/api/tournament-matches/' + first.id, { player: players.get(first.entryOneId) });
        const labels = new Map(decks.map(deck => [deck.deckId, classification(deck)]));
        const one = labels.get(room.one.decks.find(deck => deck.banned).id);
        const two = labels.get(room.two.decks.find(deck => deck.banned).id);
        assert.notEqual(one, two);
        await writeFile(join(directory, 'report-matchup-expected.json'),
            JSON.stringify({ winner: first.playerTwo, loser: first.playerOne, winnerBan: two, loserBan: one }));
    }
}

async function main() {
    const admin = await account('report-admin', { admin: true });
    const pool = [];
    for (const seat of ['alpha', 'beta', 'gamma'])
        pool.push(await account('report-' + seat, { classes: standardClasses }));
    const name = '星云战报验收';
    const id = await publish(admin, {
        name, format: 'major', registrationClosesAt: new Date(Date.now() + 3600000).toISOString(),
        stages: [
            { kind: 'swiss', rounds: 1, matchFormat: 'bo3Ban1' },
            { kind: 'singleElimination', advancingPlayers: 3, matchFormat: 'bo1',
                allowDeckResubmission: true, deckSubmissionMinutes: 3 }
        ]
    });
    const players = new Map();
    for (const player of pool) {
        const registration = await enroll(id, player, 3);
        players.set(registration.entryId, player);
    }
    await read(path(id) + '/report', { status: 404 });
    await read(classificationPath(id), { player: admin, status: 404 });
    await setCutoff(id, admin);
    const firstMatch = await waitFor(async () => (await read(path(id))).matches.find(match => match.entryTwoId), 'report fixture match');
    await read(classificationPath(id), { status: 401 });
    await read(classificationPath(id), { player: pool[0], status: 403 });
    await read(path(id) + '/report.xlsx', { status: 409 });
    const initial = await read(path(id) + '/report');
    assert.equal(initial.unclassified, 9);
    assert.equal(initial.totals, null);

    const browser = await launchBrowser();
    try {
        const reclassified = await classify(browser, admin, id, firstMatch);
        const player = players.get(firstMatch.entryOneId);
        await earlyDownload(id, await read('/api/tournament-matches/' + firstMatch.id, { player }), player);
        await read(classificationPath(id), { method: 'PUT', status: 204, player: admin,
            payload: { changes: [{ deckId: reclassified, archetype: '进化妖', previous: '=1+1' }] } });
        const qualifier = await playStage(id, 1, players);
        const [first, second] = qualifier.tournament.stages;
        assert.equal(second.deckSubmissionPending, true);
        assert.equal(Date.parse(second.deckSubmissionClosesAtUtc) - Date.parse(first.completedAtUtc), 180000);
        await setCutoff(id, admin, 2, new Date(Date.now() + 2200).toISOString());
        await waitFor(async () => (await read(path(id))).tournament.stages[1].startedAtUtc, 'automatic lineup cutoff');
        const finished = await playStage(id, 2, players);
        await verifyNumbers(id, finished, players);
        const organizer = await openAdmin(browser, admin, id);
        await assertUi(organizer.locator('#tournament-classification-status')).toContainText('已分类 9 / 9');
        await screenshot(organizer, 'report-classification.png');
        await organizer.close();
        await verifyReportUi(browser, id, pool[0]);
        await verifyBothBanStages(browser, admin, pool);
        console.log('Reported series, deck once per phase, ban denominators, outcomes, first player, advancement and download passed.');
    } finally {
        await browser.close();
    }
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
