const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const { randomUUID } = require('node:crypto');
const { setTimeout: sleep } = require('node:timers/promises');
const signalR = require('@microsoft/signalr');
const cardSeed = require('../../data/card_data.json');

const base = process.env.TOURNAMENT_TEST_BASE_URL || 'http://127.0.0.1:53261';
const eventsApi = '/api/tournaments';
const adminApi = '/api/admin/tournaments';
const standardClasses = ['Forestcraft', 'Swordcraft', 'Runecraft'];
const password = randomUUID();
const suffix = randomUUID().slice(0, 6);

async function call(path, { method = 'GET', payload, player, status = 200 } = {}) {
    const response = await fetch(base + path, {
        method,
        headers: {
            ...(payload ? { 'Content-Type': 'application/json' } : {}),
            ...(player ? { Cookie: player.cookie } : {})
        },
        body: payload && JSON.stringify(payload)
    });
    const text = await response.text();
    assert.equal(response.status, status, method + ' ' + path + '\n' + text);
    const data = response.headers.get('content-type')?.includes('json') ? JSON.parse(text) : text;
    return { data, cookie: response.headers.get('set-cookie')?.split(';')[0] };
}

async function read(path, options) {
    return (await call(path, options)).data;
}

function sqlite(code, ...args) {
    assert.ok(process.env.TOURNAMENT_SMOKE_DB, 'Set the isolated test database path.');
    return execFileSync(process.env.TOURNAMENT_SMOKE_PYTHON || 'python',
        ['-c', code, process.env.TOURNAMENT_SMOKE_DB, ...args], { encoding: 'utf8' });
}

async function signIn(credentials) {
    const { cookie } = await call('/api/auth/login', { method: 'POST', payload: credentials });
    assert.ok(cookie, 'Expected an account session.');
    return { ...credentials, cookie };
}

async function account(label, { admin = false, classes = [] } = {}) {
    const username = label + '-' + suffix;
    const credentials = { username, password };
    const { userId } = await read('/api/auth/register', { method: 'POST', payload: credentials });
    if (admin) {
        sqlite("import sqlite3,sys\nwith sqlite3.connect(sys.argv[1]) as c:\n"
            + " assert c.execute('UPDATE Users SET Role=1 WHERE Username=?', (sys.argv[2],)).rowcount == 1", username);
    }
    const player = { ...await signIn(credentials), userId, decks: [] };
    for (const deckClass of classes) {
        const cards = cardSeed.filter(card => card.id > 0 && card.isCollectible !== false
            && (card.class === deckClass || card.class === 'Neutral')).slice(0, 14)
            .map((card, index) => ({ cardDefinitionId: card.id, count: index === 13 ? 1 : 3 }));
        assert.equal(cards.length, 14);
        const saved = await read('/api/decks?userId=' + userId, {
            method: 'POST', player, status: 201,
            payload: { name: label + ' · ' + deckClass, class: deckClass, environment: 'rotation', cards }
        });
        player.decks.push(saved);
    }
    return player;
}

async function publish(admin, config) {
    const id = randomUUID();
    const path = adminApi + '/' + id;
    const draft = await read(path, {
        method: 'PUT', player: admin,
        payload: {
            revision: 0, cardVersion: 'EOT', deckEnvironment: 'rotation', deckRule: 'standard',
            registrationOpensAt: new Date(Date.now() - 60000).toISOString(), ...config
        }
    });
    await read(path + '/publish', { method: 'PUT', player: admin, payload: { revision: draft.revision } });
    return id;
}

async function enroll(id, player, count) {
    return read(eventsApi + '/' + id + '/registration', {
        method: 'PUT', player,
        payload: { revision: 0, deckIds: player.decks.slice(0, count).map(deck => deck.deckId) }
    });
}

async function setCutoff(id, admin, stageNumber = 1, deadline = null) {
    const event = (await read(adminApi, { player: admin })).find(tournament => tournament.id === id);
    assert.ok(event, 'Expected a tournament in the admin list.');
    return read(adminApi + '/' + id + '/registration/cutoff', {
        method: 'PUT', player: admin,
        payload: { revision: event.revision, stageNumber, closeNow: !deadline, registrationClosesAt: deadline }
    });
}

async function waitFor(check, description, timeout = 12000) {
    const deadline = Date.now() + timeout;
    do {
        const result = await check();
        if (result) return result;
        await sleep(120);
    } while (Date.now() < deadline);
    throw new Error('Timed out waiting for ' + description);
}

async function connect(seat) {
    const query = new URLSearchParams({ gameId: seat.gameId, playerId: seat.playerId, token: seat.token });
    const hub = new signalR.HubConnectionBuilder().withUrl(base + '/gamehub?' + query, {
        transport: signalR.HttpTransportType.WebSockets
    }).configureLogging(signalR.LogLevel.Error).build();
    const client = { hub, state: null, started: false, error: null };
    hub.on('ReceiveGameState', state => { client.state = state; });
    hub.on('TurnStarted', () => { client.started = true; });
    hub.on('ReceiveError', error => { client.error = error; });
    await hub.start();
    return client;
}

async function playNative(seats, loserIndex) {
    const clients = [];
    try {
        // Both transport connections must be open before the game begins.
        clients.push(await connect(seats[0]));
        clients.push(await connect(seats[1]));
        await waitFor(() => {
            for (const client of clients) assert.equal(client.error, null);
            return clients.every(client => client.state?.GameId === seats[0].gameId);
        }, 'native game state on both seats');
        await Promise.all(clients.map(client => client.hub.invoke('SubmitMulligan', [])));
        await waitFor(() => clients.every(client => client.started || client.state?.Phase === 'Main'), 'both mulligans');
        await clients[loserIndex].hub.invoke('Surrender', randomUUID());
    } finally {
        await Promise.all(clients.map(client => client.hub.stop()));
    }
}

module.exports = { base, eventsApi, adminApi, account, signIn, read, sqlite, standardClasses, publish, enroll, setCutoff, waitFor, connect, playNative };
