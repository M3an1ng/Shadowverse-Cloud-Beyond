const assert = require('node:assert/strict');
const { base, eventsApi, account, read, standardClasses, publish, enroll, setCutoff, waitFor, playNative } =
    require('./fixtures.cjs');
const { assertUi, launchBrowser, loginPage, screenshot } = require('./browser.cjs');

const eventPath = id => eventsApi + '/' + id;
const matchPath = id => '/api/tournament-matches/' + id;
const stageInfo = (details, number) => details.tournament.stages.find(stage => stage.number === number);
const progress = (details, number) => details.progress.find(stage => stage.stageNumber === number);
const ownSeat = room => room.one.isSelf ? room.one : room.two;
const otherSeat = room => room.one.isSelf ? room.two : room.one;

function waitEvent(id, predicate, label) {
    return waitFor(async () => {
        const details = await read(eventPath(id));
        return predicate(details) && details;
    }, label, 20000);
}

async function createEvent(admin, pool, name, count, config, deckCount, automatic = false) {
    const id = await publish(admin, {
        name, registrationClosesAt: new Date(Date.now() + 3600000).toISOString(), ...config
    });
    const players = new Map();
    for (const player of pool.slice(0, count)) {
        const registration = await enroll(id, player, deckCount);
        players.set(registration.entryId, { ...player, registration });
    }
    const begin = performance.now();
    await setCutoff(id, admin, 1, automatic ? new Date(Date.now() + 2500).toISOString() : null);
    const details = await waitEvent(id, event => event.matches.length > 0, 'automatic opening for ' + name);
    assert.equal(details.tournament.status, 'running');
    assert.equal(details.entrants.length, count);
    console.log(name + ' first pairings after ' + Math.round(performance.now() - begin) + ' ms.');
    return { id, players };
}

async function playMatch(id, stage, pairing, players, winningSide) {
    const path = matchPath(pairing.id);
    const seats = [players.get(pairing.entryOneId), players.get(pairing.entryTwoId)];
    const load = () => Promise.all(seats.map(player => read(path, { player })));
    const initial = await load();
    if (initial[0].phase === 'ban') {
        await Promise.all(seats.map((player, index) => read(path + '/ban', {
            method: 'PUT', player, payload: { deckId: otherSeat(initial[index]).decks.at(-1).id }
        })));
    }
    for (let number = 1; number <= stage.winsRequired; number++) {
        const rooms = await load();
        assert.ok(rooms.every(room => room.phase === 'pick' && room.gameNumber === number));
        await Promise.all(seats.map((player, index) => {
            const deck = ownSeat(rooms[index]).decks.find(deck => !deck.banned && !deck.hasWon);
            return read(path + '/games/' + number + '/deck', {
                method: 'PUT', player, payload: { deckId: deck.id }
            });
        }));
        const { activeGameId } = await read(path, { player: seats[0] });
        const nativeSeats = await Promise.all(seats.map(player => read(path + '/enter', {
            method: 'POST', player, payload: { gameId: activeGameId }
        })));
        await playNative(nativeSeats, 1 - winningSide);
        const scoreboard = await waitEvent(id, event => {
            const recorded = event.matches.find(item => item.id === pairing.id);
            return recorded && (winningSide === 0 ? recorded.oneWins : recorded.twoWins) === number;
        }, 'engine result for game ' + number);
        assert.equal(scoreboard.matches.find(item => item.id === pairing.id).isComplete, number === stage.winsRequired);
    }
}

async function playStage(id, number, players, winner = () => 0) {
    while (true) {
        const details = await waitEvent(id, event => stageInfo(event, number).completedAtUtc
            || event.matches.some(match => match.stageNumber === number && !match.isComplete), 'next set of pairings');
        const phase = stageInfo(details, number);
        if (phase.completedAtUtc) return details;
        const open = details.matches.filter(match => match.stageNumber === number && !match.isComplete);
        for (let index = 0; index < open.length; index += 2) {
            await Promise.all(open.slice(index, index + 2)
                .map(pairing => playMatch(id, phase, pairing, players, winner(pairing))));
        }
    }
}

function checkPlaza(event, entrants) {
    const histories = new Map([...entrants.keys()].map(id => [id, '']));
    for (let round = 1; round <= 4; round++) {
        const playing = new Set();
        for (const pair of event.matches.filter(match => match.roundNumber === round)) {
            const oneBefore = histories.get(pair.entryOneId);
            assert.ok(!playing.has(pair.entryOneId));
            playing.add(pair.entryOneId);
            if (pair.entryTwoId) {
                assert.equal(histories.get(pair.entryTwoId), oneBefore, 'Plaza must pair identical ordered records.');
                assert.ok(!playing.has(pair.entryTwoId));
                playing.add(pair.entryTwoId);
                histories.set(pair.entryTwoId, oneBefore + (pair.winnerEntryId === pair.entryTwoId ? 'W' : 'L'));
            }
            histories.set(pair.entryOneId, oneBefore + (pair.winnerEntryId === pair.entryOneId ? 'W' : 'L'));
        }
        assert.equal(playing.size, entrants.size);
    }
    if (entrants.size === 16) assert.equal(new Set(histories.values()).size, 16);
    assert.ok(progress(event, 1).standings.every(row => row.record === histories.get(row.entryId)));
}

function checkSwiss(event) {
    const matches = event.matches.filter(match => match.stageNumber === 1);
    const seen = new Set();
    const byes = new Set();
    const ranking = progress(event, 1).standings;
    for (const match of matches) {
        if (match.entryTwoId) {
            const opponents = [match.entryOneId, match.entryTwoId].sort().join('|');
            assert.ok(!seen.has(opponents), 'Swiss repeats were avoidable in this test.');
            seen.add(opponents);
        } else {
            assert.ok(!byes.has(match.entryOneId), 'Swiss byes should go to different players.');
            byes.add(match.entryOneId);
        }
    }
    const wins = new Map(ranking.map(row => [row.entryId, matches.filter(match => match.winnerEntryId === row.entryId).length]));
    for (const row of ranking) {
        const foes = matches.map(match => match.entryOneId === row.entryId ? match.entryTwoId
            : match.entryTwoId === row.entryId ? match.entryOneId : null).filter(Boolean);
        assert.equal(row.wins, wins.get(row.entryId));
        assert.equal(row.opponentWins, foes.reduce((sum, id) => sum + wins.get(id), 0));
    }
    assert.equal(ranking.length, event.entrants.length);
}

async function checkPoolCardOrder(page, event) {
    const matches = event.matches.filter(match => match.stageNumber === 1);
    const standingByName = new Map(progress(event, 1).standings.map(entry => [entry.playerName, entry]));
    const columns = page.locator('.tournament-round-strip[data-bracket="pool"] .tournament-round');
    const numbers = [...new Set(matches.map(match => match.roundNumber))].sort((a, b) => a - b);
    await assertUi(columns).toHaveCount(numbers.length);
    const clippedNames = await columns.locator('.tournament-pairing-seat .tournament-player-name').evaluateAll(names =>
        names.filter(name => name.scrollWidth > name.clientWidth + 1).map(name => name.textContent));
    assert.deepEqual(clippedNames, [], 'Player names should fit in the desktop schedule.');
    const layout = await columns.evaluateAll(rounds => rounds.map(round => [...round.querySelectorAll('.tournament-pairing')].map(card => {
        const bounds = card.getBoundingClientRect();
        return {
            names: [...card.querySelectorAll('.tournament-pairing-seat .tournament-player-name')].map(name => name.textContent),
            top: bounds.top, height: bounds.height
        };
    })));

    for (const [index, cards] of layout.entries()) {
        const round = numbers[index];
        assert.equal(cards.length, matches.filter(match => match.roundNumber === round).length);
        let previous = [Infinity, Infinity];
        for (const [row, card] of cards.entries()) {
            const rank = card.names.map(name => (standingByName.get(name)?.record.slice(0, round - 1).match(/W/g) ?? []).length)
                .sort((a, b) => b - a);
            assert.ok(rank[0] < previous[0] || (rank[0] === previous[0] && rank[1] <= previous[1]),
                'Pairing order must be based on wins before its round.');
            previous = rank;
            const reference = layout[0][row];
            if (reference) {
                assert.ok(Math.abs(card.top - reference.top) <= 1, 'Pairing rows must align across columns.');
                assert.ok(Math.abs(card.height - reference.height) <= 1, 'Pairing cards must have a common height.');
            }
        }
    }
}

function checkElimination(event, double) {
    const players = progress(event, 2).participants.filter(player => player.canPlay);
    const active = event.matches.filter(match => match.stageNumber === 2 && match.entryTwoId);
    const winner = stageInfo(event, 2).winnerEntryId;
    assert.equal(event.tournament.status, 'finished');
    assert.ok(winner);
    assert.equal(active.length, double ? players.length * 2 - 2 : players.length - 1);
    const finals = active.filter(match => match.bracket === 'finals');
    if (double) {
        assert.equal(stageInfo(event, 2).allowGrandFinalReset, false);
        assert.equal(finals.length, 1);
        assert.equal(finals[0].roundNumber, 1);
        assert.equal(finals[0].winnerEntryId, finals[0].entryTwoId);
        assert.equal(finals[0].winnerEntryId, winner);
        assert.equal(progress(event, 2).bracket.filter(node => node.position.bracket === 'finals').length, 1);
    }
    for (const player of players) {
        const losses = active.filter(match => (match.entryOneId === player.entryId || match.entryTwoId === player.entryId)
            && match.winnerEntryId !== player.entryId).length;
        const expected = double ? (player.entryId === winner || player.entryId === finals[0].entryOneId ? 1 : 2)
            : (player.entryId === winner ? 0 : 1);
        assert.equal(losses, expected);
    }
}

async function checkLineupWindow(id, admin, qualification, players) {
    const qualifiers = progress(qualification, 2).participants;
    const ranked = progress(qualification, 1).standings.slice(0, 6).map(row => row.entryId);
    assert.deepEqual(qualifiers.map(player => player.entryId), ranked);
    assert.equal(qualification.matches.some(match => match.stageNumber === 2), false);
    const route = eventPath(id) + '/stages/2/lineup';
    const first = qualifiers[0].entryId;
    await read(eventPath(id) + '/entries/' + first + '?stage=2', { status: 404 });
    const unqualified = [...players].find(([entryId]) => !ranked.includes(entryId))[1];
    assert.match((await read(route, { player: unqualified })).blockedReason, /晋级/);
    await read(route, {
        method: 'PUT', status: 409, player: unqualified,
        payload: { revision: unqualified.registration.revision, deckIds: unqualified.decks.map(deck => deck.deckId) }
    });

    const browser = await launchBrowser();
    try {
        const player = players.get(first);
        const [page, organizer] = await Promise.all([loginPage(browser, player), loginPage(browser, admin)]);
        await page.goto(base + '/?tournament=' + id + '#tournaments');
        await assertUi(page.getByRole('heading', { name: '双败淘汰 · 卡组重交' })).toBeVisible();
        await assertUi(page.locator('#tournament-registration-note')).toContainText('由管理员手动截止');
        await page.getByRole('button', { name: '瑞士轮', exact: true }).click();
        await checkPoolCardOrder(page, qualification);
        await page.getByRole('button', { name: '双败淘汰', exact: true }).click();
        await assertUi(page.locator('[data-choose-deck]')).toHaveCount(3);
        for (const slot of [1, 2]) {
            await page.locator('[data-choose-deck="' + slot + '"]').click();
            const card = page.locator('.deck-picker-modal.open .deck-picker-card[data-deck-id="' + player.decks[slot].deckId + '"]');
            await card.hover();
            await card.getByRole('button', { name: '选择', exact: true }).click();
        }
        await page.getByRole('button', { name: '提交阶段卡组' }).click();
        await assertUi(page.locator('#tournament-registration-status')).toHaveText('卡组已更新。');
        await screenshot(page, 'scheduler-resubmission.png');
        for (const qualifier of qualifiers.slice(1, -1)) {
            const other = players.get(qualifier.entryId);
            await read(route, {
                method: 'PUT', player: other,
                payload: { revision: other.registration.revision, deckIds: other.decks.map(deck => deck.deckId) }
            });
        }
        await read(eventPath(id) + '/entries/' + first + '?stage=2', { status: 404 });
        await organizer.locator('.nav-item[data-route="dev-tools"]').click();
        await organizer.getByRole('button', { name: '管理赛事', exact: true }).click();
        await organizer.getByRole('button', { name: /九人瑞士轮/ }).click();
        await organizer.getByRole('button', { name: '立即截止重交' }).click();
        await organizer.getByRole('button', { name: '确认截止', exact: true }).click();
        await assertUi(organizer.locator('#tournament-admin-status')).toHaveText('重交已截止。');
        const started = await waitEvent(id, data => stageInfo(data, 2).startedAtUtc, 'resubmission lock');
        assert.equal(stageInfo(started, 2).deckSubmissionPending, false);
        assert.equal(progress(started, 2).participants.filter(player => player.canPlay).length, 5);
        await read(eventPath(id) + '/entries/' + qualifiers.at(-1).entryId + '?stage=2', { status: 404 });
        const lineup = await read(eventPath(id) + '/entries/' + first + '?stage=2');
        assert.equal(lineup.decks.length, 3);
        assert.ok(lineup.decks.every(deck => deck.stageNumber === 2));
        assert.ok(lineup.decks.every(deck => deck.stats.totalGames === 0 && deck.stats.bansReceived === 0));
        const former = await read(eventPath(id) + '/entries/' + first + '?stage=1');
        assert.equal(former.decks.length, 1);
        assert.ok(former.decks[0].stats.totalGames > 0);
        assert.equal(former.decks[0].stats.bansReceived, null);
        assert.equal(former.previousLineup, null);
        assert.deepEqual(lineup.previousLineup, { stageNumber: 1, decks: former.decks });
        await page.bringToFront();
        await assertUi(page.locator('#tournament-registration-form')).toBeHidden();
        await assertUi(page.getByRole('heading', { name: '胜者组', exact: true })).toBeVisible();
        await screenshot(page, 'scheduler-double-bracket.png');
        await page.locator('.tournament-pairing-seat [data-stage-entry-id="' + first + '"]').first().click();
        const profile = page.locator('.deck-picker-modal[data-player-profile]');
        await assertUi(profile.getByRole('heading', { name: '后半程 · 双败淘汰卡组', exact: true })).toBeVisible();
        await assertUi(profile.locator('#deck-picker-grid .tournament-player-deck')).toHaveCount(3);
        const history = profile.locator('.deck-picker-profile-history');
        await assertUi(history.getByRole('heading', { name: '前半程 · 瑞士轮卡组', exact: true })).toBeVisible();
        await assertUi(history.locator('.tournament-player-deck')).toHaveCount(1);
        const previousPreview = history.getByRole('button', { name: '预览卡组', exact: true });
        await previousPreview.click();
        await assertUi(profile.locator('#deck-picker-preview-title')).toHaveText(former.decks[0].name);
        await profile.getByRole('button', { name: '关闭预览', exact: true }).click();
        await assertUi(previousPreview).toBeFocused();
    } finally {
        await browser.close();
    }
}

async function main() {
    const admin = await account('schedule-admin', { admin: true });
    const pool = [];
    for (let index = 1; index <= 16; index++) {
        pool.push(await account('seed-' + index, { classes: standardClasses }));
    }
    for (const count of [16, 7]) {
        const { id, players } = await createEvent(admin, pool, count + ' 人广场赛', count, {
            format: 'plaza', playerLimit: count, stages: [{ kind: 'plaza', matchFormat: 'bo1' }]
        }, 1, count === 7);
        const ended = await playStage(id, 1, players);
        assert.equal(ended.tournament.status, 'finished');
        checkPlaza(ended, players);
        console.log(count + ' player plaza: all four ordered-record rounds passed.');
        if (count === 16) {
            const browser = await launchBrowser();
            try {
                const page = await loginPage(browser, pool[0]);
                await page.goto(base + '/?tournament=' + id + '#tournaments');
                await assertUi(page.getByRole('heading', { name: '第 4 轮' })).toBeVisible();
                const filter = page.getByRole('combobox', { name: '筛选赛事' });
                await assertUi(filter).toHaveValue('all');
                await filter.selectOption('finished');
                await assertUi(filter).toBeEnabled();
                await assertUi(page.locator('#tournament-lobby-events [aria-current="true"]')).toHaveAttribute('data-event-id', id);
                await assertUi(page.locator('#tournament-lobby-detail')).toBeVisible();
                const top = await page.locator('.tournament-lobby-grid').evaluate(grid => {
                    const list = grid.querySelector('.tournament-event-list-panel').getBoundingClientRect();
                    const summary = grid.querySelector('.tournament-event-summary').getBoundingClientRect();
                    return { aligned: Math.abs(list.left - summary.left) <= 1, below: summary.top >= list.bottom,
                        fullWidth: Math.abs(list.width - summary.width) <= 1 };
                });
                assert.deepEqual(top, { aligned: true, below: true, fullWidth: true });
                await assertUi(page.locator('.tournament-standings tbody tr')).toHaveCount(16);
                await checkPoolCardOrder(page, ended);
                await assertUi(page.getByRole('heading', { name: '选手与卡组', exact: true })).toHaveCount(0);
                const results = page.getByRole('button', { name: '查看赛果', exact: true });
                await assertUi(results).toHaveCount(4);
                assert.ok((await results.first().boundingBox()).width >= 68, 'The full results label needs a wide button.');
                await screenshot(page, 'scheduler-plaza.png');
                await page.setViewportSize({ width: 390, height: 844 });
                assert.equal(await page.locator('.tournament-round-strip').evaluate(
                    strip => strip.scrollWidth > strip.clientWidth + 1), false);
                await results.first().click();
                await assertUi(page.getByRole('heading', { name: '小局结果', exact: true })).toBeVisible();
            } finally { await browser.close(); }
        }
    }

    const single = await createEvent(admin, pool, '六人瑞士轮 · 单败', 6, {
        format: 'major', stages: [{ kind: 'swiss', rounds: 2, matchFormat: 'bo1' },
            { kind: 'singleElimination', advancingPlayers: 5, matchFormat: 'bo3' }]
    }, 2);
    const switched = await playStage(single.id, 1, single.players);
    checkSwiss(switched);
    assert.equal(progress(switched, 2).participants.length, 5);
    const inherited = switched.matches.find(match => match.stageNumber === 2 && match.entryTwoId);
    const reused = await read(eventPath(single.id) + '/entries/' + inherited.entryOneId + '?stage=2');
    assert.equal(reused.decks.length, 2);
    assert.ok(reused.decks.every(deck => deck.stageNumber === 1));
    assert.deepEqual(reused.previousLineup.decks.map(deck => deck.id), reused.decks.map(deck => deck.id));
    checkElimination(await playStage(single.id, 2, single.players), false);
    console.log('Swiss to five-player single elimination with inherited decks passed.');

    const double = await createEvent(admin, pool, '九人瑞士轮 · 双败', 9, {
        format: 'major', stages: [{ kind: 'swiss', rounds: 4, matchFormat: 'bo1' },
            { kind: 'doubleElimination', advancingPlayers: 6, matchFormat: 'bo3Ban1', allowDeckResubmission: true }]
    }, 1);
    const qualification = await playStage(double.id, 1, double.players);
    checkSwiss(qualification);
    const resubmitting = stageInfo(qualification, 2);
    assert.equal(resubmitting.deckSubmissionMinutes, null);
    assert.equal(resubmitting.deckSubmissionClosesAtUtc, null);
    assert.equal(resubmitting.deckSubmissionPending, true);
    await checkLineupWindow(double.id, admin, qualification, double.players);
    const champion = await playStage(double.id, 2, double.players,
        pairing => pairing.bracket === 'finals' && pairing.roundNumber === 1 ? 1 : 0);
    checkElimination(champion, true);
    const archive = await read(eventsApi + '?finished=true');
    assert.ok(archive.some(event => event.id === double.id));
    console.log('Nine-player Swiss and five-player BO3 Ban1 double elimination passed. The lower bracket winner took the title in one final.');

    const large = await createEvent(admin, pool, '十六人卡组分类验收', 16, {
        format: 'major', stages: [{ kind: 'swiss', rounds: 1, matchFormat: 'bo3Ban1' }]
    }, 3);
    const browser = await launchBrowser();
    try {
        const page = await loginPage(browser, admin);
        await page.locator('.nav-item[data-route="dev-tools"]').click();
        await page.getByRole('button', { name: '管理赛事', exact: true }).click();
        await page.getByRole('button', { name: /十六人卡组分类验收/ }).click();
        const started = performance.now();
        await page.getByRole('button', { name: '卡组分类与导出' }).click();
        const dialog = page.locator('#tournament-classification-dialog');
        const decks = dialog.locator('input[data-classification-deck]');
        await assertUi(decks).toHaveCount(40);
        console.log('Classification listed 48 decks in ' + Math.round(performance.now() - started) + ' ms with 40 rows rendered.');
        await dialog.getByRole('button', { name: '下一页' }).click();
        await assertUi(decks).toHaveCount(8);
        await dialog.locator('#tournament-classification-search').fill(pool[0].username);
        await assertUi(decks).toHaveCount(3);
        const event = (await read('/api/admin/tournaments', { player: admin })).find(row => row.id === large.id);
        await read('/api/admin/tournaments/' + large.id + '?revision=' + event.revision,
            { method: 'DELETE', player: admin, status: 204 });
    } finally {
        await browser.close();
    }
}

if (require.main === module) {
    main().catch(error => {
        console.error(error);
        process.exitCode = 1;
    });
}

module.exports = { playStage };
