import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { setImmediate } from 'node:timers/promises';
import test from 'node:test';

async function loadBrowserModule(path) {
    const source = await readFile(new URL(path, import.meta.url));
    return import(`data:text/javascript;base64,${source.toString('base64')}`);
}

const { BattleConnectionRejected, retryBattleConnection, waitForBattleConnection } =
    await loadBrowserModule('../src/battleConnection.js');
const { createRevealPresentationGate } = await loadBrowserModule('../src/revealPresentationGate.js');

function deferred() {
    let resolve;
    const promise = new Promise(done => { resolve = done; });
    return { promise, resolve };
}

test('waits for the game snapshot after the SignalR handshake', async () => {
    const { promise, resolve } = deferred();
    const signal = new AbortController().signal;
    let synchronized = false;
    const ready = waitForBattleConnection({ start: async () => {} }, promise, { signal, timeoutMs: 1_000 })
        .then(() => { synchronized = true; });
    await setImmediate();
    assert.equal(synchronized, false);
    resolve();
    await ready;
    assert.equal(synchronized, true);
});

test('times out an incomplete connection and can cancel on page exit', async () => {
    const connection = { start: async () => {} };
    const snapshot = new Promise(() => {});
    const controller = new AbortController();
    await assert.rejects(
        waitForBattleConnection(connection, snapshot, { signal: controller.signal, timeoutMs: 20 }),
        /等待对局状态超时/
    );

    const cancelled = waitForBattleConnection(connection, snapshot, { signal: controller.signal, timeoutMs: 1_000 });
    controller.abort();
    await assert.rejects(cancelled, { name: 'AbortError' });
});

test('retries promptly when the browser is back online', async t => {
    const originalNavigator = Object.getOwnPropertyDescriptor(globalThis, 'navigator');
    const network = { onLine: false };
    globalThis.window = new EventTarget();
    globalThis.document = new EventTarget();
    Object.defineProperty(globalThis, 'navigator', { configurable: true, value: network });
    t.after(() => {
        delete globalThis.document;
        delete globalThis.window;
        if (originalNavigator) Object.defineProperty(globalThis, 'navigator', originalNavigator);
        else delete globalThis.navigator;
    });

    let attempts = 0;
    const signal = new AbortController().signal;
    const recovering = retryBattleConnection(() => ++attempts, { signal, budgetMs: 1_000 });
    assert.equal(attempts, 0);
    network.onLine = true;
    window.dispatchEvent(new Event('online'));
    assert.equal(await recovering, 1);
    assert.equal(attempts, 1);

    const rejected = new BattleConnectionRejected('room closed');
    await assert.rejects(retryBattleConnection(() => {
        attempts++;
        throw rejected;
    }, { signal, budgetMs: 1_000 }), rejected);
    assert.equal(attempts, 2);
});

test('a full state discards old animation work without unblocking newer effects', async () => {
    const gate = createRevealPresentationGate();
    const oldEffect = deferred();
    const newEffect = deferred();
    const events = [];

    gate.dispatch('CardRevealed', () => oldEffect.promise);
    gate.dispatch('CardMoved', () => events.push('stale move'));
    gate.dispatch('ReceiveGameState', () => events.push('snapshot'));
    gate.dispatch('CardRevealed', () => {
        events.push('new effect');
        return newEffect.promise;
    });
    gate.dispatch('CardMoved', () => events.push('new move'));

    oldEffect.resolve();
    await setImmediate();
    assert.deepEqual(events, ['snapshot', 'new effect']);
    newEffect.resolve();
    await setImmediate();
    assert.deepEqual(events, ['snapshot', 'new effect', 'new move']);
});
