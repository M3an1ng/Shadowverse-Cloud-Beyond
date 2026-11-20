const assert = require('node:assert/strict');
const { base, eventsApi, adminApi, account, read, standardClasses, publish, enroll, setCutoff, waitFor, playNative } =
    require('./fixtures.cjs');
const { assertUi, launchBrowser, loginPage, screenshot } = require('./browser.cjs');

const series = [
    { format: 'bo1', decks: 1, winners: [0] },
    { format: 'bo3', decks: 2, winners: [0, 1, 0] },
    { format: 'bo5', decks: 3, winners: [0, 1, 0, 1, 0] },
    { format: 'bo3Ban1', decks: 3, winners: [0, 0] }
];

const matchPath = test => '/api/tournament-matches/' + test.matchId;
const ownSeat = room => room.one.isSelf ? room.one : room.two;
const otherSeat = room => room.one.isSelf ? room.two : room.one;
const loadRoom = (test, player) => read(matchPath(test), { player });
const put = (path, player, deckId, status = 200) => read(path, {
    method: 'PUT', player, status, payload: { deckId }
});

async function openPlayer(browser, player) {
    const state = { gameId: null, turns: 0 };
    const page = await loginPage(browser, player, loaded => {
        loaded.on('websocket', socket => {
            socket.on('framereceived', ({ payload }) => {
                if (typeof payload !== 'string') return;
                for (const frame of payload.split('\x1e').filter(Boolean)) {
                    const message = JSON.parse(frame);
                    if (message.target?.toLowerCase() === 'receivegamestate') state.gameId = message.arguments[0].GameId;
                    if (message.target?.toLowerCase() === 'turnstarted') state.turns++;
                }
            });
        });
    });
    return { page, state };
}

async function assertSymmetry(page) {
    const [left, right] = await page.locator('#tournament-match-seats .duel-room-seat').evaluateAll(seats =>
        seats.map(seat => [seat, ...seat.querySelectorAll('.duel-room-deck')].map(element => {
            const { top, width, height } = element.getBoundingClientRect();
            return { top, width, height };
        })));
    assert.equal(left.length, right.length);
    for (let i = 0; i < left.length; i++) {
        for (const key of ['top', 'width', 'height']) {
            assert.ok(Math.abs(left[i][key] - right[i][key]) <= 1,
                'Sides differ at row ' + i + ' on ' + key + ': ' + JSON.stringify({ left, right }));
        }
    }
    const spacing = await page.locator('#tournament-match-layout').evaluate(layout => {
        const person = layout.querySelector('.duel-room-person').getBoundingClientRect();
        const deck = layout.querySelector('.duel-room-deck').getBoundingClientRect();
        const bounds = layout.getBoundingClientRect();
        const scroller = layout.parentElement.getBoundingClientRect();
        const results = document.getElementById('tournament-match-results');
        return {
            headerGap: deck.top - person.bottom,
            top: bounds.top - scroller.top,
            bottom: scroller.bottom - (results.hidden ? bounds.bottom : results.getBoundingClientRect().bottom)
        };
    });
    assert.ok(spacing.headerGap >= 14, 'The first deck is too close to the player information.');
    if (spacing.top > 0 && spacing.bottom > 0) {
        assert.ok(Math.abs(spacing.top - spacing.bottom) <= 24, 'The room is too close to one edge of the content area.');
    }
}

async function playInBrowser(test, players, rooms) {
    const browser = await launchBrowser();
    try {
        const sessions = await Promise.all(players.map(player => openPlayer(browser, player)));
        const [alpha, beta] = sessions.map(session => session.page);
        await Promise.all(sessions.map(({ page }) =>
            page.goto(base + '/?tournament=' + test.id + '#tournaments')));
        const pairing = alpha.locator('.tournament-round .tournament-pairing').first();
        for (const player of players) {
            await pairing.getByRole('button', { name: player.username, exact: true }).click();
            const profile = alpha.locator('.deck-picker-modal[data-player-profile]');
            await assertUi(profile.getByRole('heading', { name: player.username, exact: true })).toBeVisible();
            await assertUi(profile.locator('.tournament-player-deck')).toHaveCount(3);
            await profile.locator('.deck-picker-close-btn').click();
        }
        await assertUi(alpha.getByRole('button', { name: '进入房间', exact: true })).toBeVisible();
        await screenshot(alpha, 'series-schedule.png');
        await Promise.all(sessions.map(({ page }) =>
            page.getByRole('button', { name: '进入房间', exact: true }).click()));
        const ban = page => page.locator('[data-tournament-action="ban"]');
        const pick = page => page.locator('[data-tournament-action="pick"]');
        await assertUi(ban(alpha)).toHaveCount(3);
        await assertUi(ban(beta)).toHaveCount(3);
        for (const page of [alpha, beta]) {
            const seats = page.locator('#tournament-match-seats .duel-room-seat');
            await assertUi(seats.first()).toHaveAttribute('data-self', 'true');
            await assertUi(seats.last()).toHaveAttribute('data-self', 'false');
        }
        await assertSymmetry(alpha);
        await screenshot(alpha, 'series-ban.png');
        await beta.setViewportSize({ width: 390, height: 844 });
        await assertUi(beta.locator('#battle-portrait-rotate-overlay')).toBeHidden();
        assert.equal(await beta.locator('.duel-room-scroll:visible').evaluate(
            element => element.scrollWidth > element.clientWidth + 1), false);
        await assertSymmetry(beta);
        await screenshot(beta, 'series-mobile.png');
        await beta.setViewportSize({ width: 1390, height: 880 });

        await ban(alpha).last().click();
        await assertUi(alpha.locator('#tournament-match-phase')).toHaveText('禁牌已锁定，等待对手。');
        const hidden = await loadRoom(test, players[1]);
        assert.equal(hidden.phase, 'ban');
        assert.equal(otherSeat(hidden).banLocked, true);
        assert.ok(ownSeat(hidden).decks.every(deck => !deck.banned));
        const publicLineupUrl = eventsApi + '/' + test.id + '/entries/' + test.entries[1];
        const beforeReveal = await read(publicLineupUrl);
        assert.ok(beforeReveal.decks.every(deck => deck.stats.bansReceived === 0));
        await ban(beta).last().click();
        await assertUi(pick(alpha)).toHaveCount(2);
        await assertUi(pick(beta)).toHaveCount(2);
        const beforeSettlement = await read(publicLineupUrl);
        assert.ok(beforeSettlement.decks.every(deck => deck.stats.bansReceived === 0));
        await assertSymmetry(alpha);
        await screenshot(alpha, 'series-pick.png');
        assert.match(await put(matchPath(test) + '/games/1/deck', players[0],
            ownSeat(rooms[0]).decks.at(-1).id, 409), /无法再次出场/);

        await pick(alpha).first().click();
        await assertUi(alpha.locator('#tournament-match-phase')).toHaveText('出战卡组已锁定，等待对手。');
        assert.equal((await loadRoom(test, players[1])).myPick, null);
        await pick(beta).first().click();
        const enter = page => page.locator('#tournament-match-enter');
        await Promise.all(sessions.map(({ page }) => enter(page).click()));
        await Promise.all(sessions.map(({ page }) => page.waitForURL(/#game$/)));
        await waitFor(() => sessions.every(({ state }) => state.gameId === sessions[0].state.gameId && state.gameId),
            'a shared native game ID inside both browsers');
        await Promise.all(sessions.map(({ page }) => page.locator('#confirm-mulligan-button').click()));
        await waitFor(() => sessions.every(({ state }) => state.turns > 0), 'both browsers starting the first turn');
        await screenshot(alpha, 'series-battle.png');
        await beta.getByRole('button', { name: 'Back to Home' }).click();
        await beta.getByRole('button', { name: '确认投降' }).click();
        const returnButton = alpha.getByRole('button', { name: '返回赛事房间' });
        await assertUi(returnButton).toBeVisible();
        await returnButton.click();
        await alpha.waitForURL(/#tournament-match$/);
        await assertUi(alpha.locator('#tournament-match-score')).toHaveText('1 : 0');
        await assertUi(pick(alpha)).toHaveCount(1);
        await assertSymmetry(alpha);
        const results = alpha.locator('#tournament-match-results');
        await assertUi(results).toBeVisible();
        const offset = await results.evaluate(element => {
            const card = element.getBoundingClientRect();
            const score = document.getElementById('tournament-match-score').getBoundingClientRect();
            return Math.abs((card.left + card.right) / 2 - (score.left + score.right) / 2);
        });
        assert.ok(offset <= 1, 'Game results must be centered below the score.');
        await screenshot(alpha, 'series-after-game.png');
        const lineup = await read(eventsApi + '/' + test.id + '/entries/' + test.entries[0]);
        assert.deepEqual(lineup.record, { wins: 0, losses: 0 });
        assert.equal(lineup.decks.reduce((sum, deck) => sum + deck.stats.totalGames, 0), 1);
    } finally {
        await browser.close();
    }
}

async function checkSeries(test, players, outside) {
    const path = matchPath(test);
    await read(path, { status: 401 });
    await read(path, { player: outside, status: 404 });
    const snapshot = await fetch(base + path, { headers: { Cookie: players[0].cookie } });
    assert.equal(snapshot.status, 200);
    const etag = snapshot.headers.get('etag');
    assert.ok(etag);
    const snapshotBytes = Buffer.byteLength(await snapshot.text());
    const idle = await fetch(base + path, { headers: { Cookie: players[0].cookie, 'If-None-Match': etag } });
    assert.equal(idle.status, 304);
    assert.equal((await idle.text()).length, 0);
    const unauthorized = await fetch(base + path, { headers: { Cookie: outside.cookie, 'If-None-Match': etag } });
    assert.equal(unauthorized.status, 404);
    if (test.format === 'bo3Ban1') console.log('Unchanged series polling uses 0 response body bytes vs ' + snapshotBytes + ' bytes.');
    const initial = await Promise.all(players.map(player => loadRoom(test, player)));
    assert.equal(initial[0].phase, test.format === 'bo3Ban1' ? 'ban' : 'pick');
    assert.equal(ownSeat(initial[0]).decks.length, test.decks);
    if (test.format === 'bo3Ban1') await playInBrowser(test, players, initial);

    // Each game is played through the same SignalR API as the browser clients.
    for (let i = test.format === 'bo3Ban1' ? 1 : 0; i < test.winners.length; i++) {
        const rooms = await Promise.all(players.map(player => loadRoom(test, player)));
        assert.ok(rooms.every(room => room.phase === 'pick' && room.gameNumber === i + 1));
        const pickPath = path + '/games/' + (i + 1) + '/deck';
        const options = rooms.map(room => ownSeat(room).decks);
        const locked = options[0].find(deck => deck.hasWon);
        if (locked) assert.match(await put(pickPath, players[0], locked.id, 409), /已经赢过/);
        await Promise.all(players.map((player, index) =>
            put(pickPath, player, options[index].find(deck => !deck.banned && !deck.hasWon).id)));
        const next = await loadRoom(test, players[0]);
        assert.equal(next.phase, 'battle');
        const seats = await Promise.all(players.map(player => read(path + '/enter', {
            method: 'POST', player, payload: { gameId: next.activeGameId }
        })));
        assert.equal(seats[0].gameId, seats[1].gameId);
        assert.notEqual(seats[0].token, seats[1].token);
        await read('/api/match/' + seats[0].gameId, { status: 404 });
        await playNative(seats, 1 - test.winners[i]);
        // Public schedule contains persisted scores and does not run room reconciliation.
        const expected = test.winners.slice(0, i + 1);
        await waitFor(async () => {
            const event = await read(eventsApi + '/' + test.id);
            const pair = event.matches.find(match => match.id === test.matchId);
            return pair.oneWins === expected.filter(winner => winner === 0).length
                && pair.twoWins === expected.filter(winner => winner === 1).length;
        }, 'the persisted engine result');
    }
    const final = await loadRoom(test, players[0]);
    assert.equal(final.phase, 'completed');
    assert.equal(final.winnerEntryId, test.entries[0]);
    assert.deepEqual(final.results.map(result => result.winnerEntryId),
        test.winners.map(winner => test.entries[winner]));
    assert.ok(final.results.every(result => test.entries.includes(result.firstPlayerEntryId)));
    assert.equal(new Set(final.results.filter(game => game.winnerEntryId === test.entries[0])
        .map(game => game.entryOneDeckId)).size, final.one.wins);
    for (const [index, entry] of test.entries.entries()) {
        const profile = await read(eventsApi + '/' + test.id + '/entries/' + entry);
        assert.deepEqual(profile.record, { wins: 1 - index, losses: index });
        const deckKey = index === 0 ? 'entryOneDeckId' : 'entryTwoDeckId';
        for (const deck of profile.decks) {
            const games = final.results.filter(game => game[deckKey] === deck.id);
            assert.deepEqual(deck.stats, {
                totalGames: games.length,
                totalWins: games.filter(game => game.winnerEntryId === entry).length,
                goingFirstGames: games.filter(game => game.firstPlayerEntryId === entry).length,
                goingFirstWins: games.filter(game => game.firstPlayerEntryId === entry && game.winnerEntryId === entry).length,
                goingSecondGames: games.filter(game => game.firstPlayerEntryId !== entry).length,
                goingSecondWins: games.filter(game => game.firstPlayerEntryId !== entry && game.winnerEntryId === entry).length,
                bansReceived: test.format === 'bo3Ban1' ? Number(deck.id === ownSeat(initial[index]).decks.at(-1).id) : null
            });
        }
    }
    console.log(test.format + ' finished ' + final.one.wins + ':' + final.two.wins);
}

async function main() {
    const admin = await account('series-admin', { admin: true });
    const one = await account('series-alpha', { classes: standardClasses });
    const two = await account('series-beta', { classes: standardClasses });
    const outsider = await account('series-outsider');
    const cutoff = new Date(Date.now() + 3600000);
    for (const test of series) {
        test.id = await publish(admin, {
            name: '征服赛验收 ' + test.format, format: 'major', registrationClosesAt: cutoff.toISOString(),
            stages: [{ kind: 'swiss', matchFormat: test.format, rounds: 1, allowDeckResubmission: false }]
        });
        const entries = await Promise.all([one, two].map(player => enroll(test.id, player, test.decks)));
        const closed = await setCutoff(test.id, admin);
        test.revision = closed.revision;
        const pairing = await waitFor(async () => (await read(eventsApi + '/' + test.id)).matches[0],
            'automatically scheduled ' + test.format);
        test.matchId = pairing.id;
        test.entries = [pairing.entryOneId, pairing.entryTwoId];
        const players = pairing.entryOneId === entries[0].entryId ? [one, two] : [two, one];
        await checkSeries(test, players, outsider);
    }
    const finished = series.at(-1);
    await read(adminApi + '/' + finished.id + '?revision=' + finished.revision, { method: 'DELETE', player: admin, status: 204 });
    await read(eventsApi + '/' + finished.id, { status: 404 });
    await read(matchPath(finished), { player: one, status: 404 });
    await read('/api/decks/' + one.decks[0].deckId + '?userId=' + one.userId, { player: one });
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
