import assert from 'node:assert/strict';
import test from 'node:test';
import { build } from 'esbuild';

test('a PP change keeps a cancelled card in hand and only that play can be retried immediately', async t => {
    const bundle = await build({
        entryPoints: ['src/gameManager.js'], bundle: true, format: 'esm',
        platform: 'node', write: false, loader: { '.css': 'empty' }
    });
    const manager = await import('data:text/javascript;base64,' + Buffer.from(bundle.outputFiles[0].contents).toString('base64'));
    t.mock.method(Date, 'now', () => 1000);
    const hand = ['first', 'second'].map(uid => ({ uid, ownerId: 'player', zone: 'Hand', type: 'Spell', cost: 1 }));
    const player = { id: 'player', pp: 3, hand, field: [] };
    Object.assign(manager.gameState, { players: [player], localPlayerId: player.id, currentPlayerId: player.id, phase: 'Main' });
    manager.bindGameView({ updatePpDisplay() {}, cancelCommittedHandPlayVisual() {} });
    const plays = [];
    let acceptChoice = false;
    manager.setSignalRConnection({
        state: 'Connected',
        async invoke(method, dto) {
            if (method === 'SubmitTargetChoice') return acceptChoice;
            assert.equal(method, 'PlayCard');
            plays.push(dto.CardUid);
        }
    });

    await manager.playCard('first');
    await manager.playCard('second');
    const choice = { PendingChoiceId: 'choice', PendingPlayCardUid: 'first', RequestingPlayerId: player.id };
    await manager.submitPlayerChoice({ ...choice, PendingChoiceId: 'ended-choice' }, [], true);
    await manager.playCard('first');
    assert.deepEqual(plays, ['first', 'second']);

    acceptChoice = true;
    await manager.submitPlayerChoice(choice, [], true);
    manager.handlePpUpdate({ SourcePlayerId: player.id, CurrentPp: 2, MaxPp: 3 });
    assert.equal(player.pp, 2);
    assert.deepEqual(player.hand.map(card => card.uid), ['first', 'second']);
    await manager.playCard('first');
    await manager.playCard('first');
    await manager.playCard('second');
    assert.deepEqual(plays, ['first', 'second', 'first']);
});
