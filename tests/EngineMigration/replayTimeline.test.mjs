import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../../src/replayTimeline.js', import.meta.url), 'utf8');
const { createReplayTimeline } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

const areas = () => ({ hand: [], deck: [], field: [], emblem: [], graveyard: [], extra: [], leaderCard: null });
const opening = {
    game: { gameId: 'battle', currentPlayerId: 'a' }, playerOrder: ['a', 'b'],
    players: { a: { playerId: 'a', turnNumber: 0, isFirst: true }, b: { playerId: 'b', turnNumber: 0 } },
    cards: { x: { instanceId: 'x', name: '夜莺' }, y: { instanceId: 'y' } },
    fusions: { x: ['y'], y: [] }, zones: { a: { ...areas(), hand: ['x'] }, b: areas() }
};
const set = (path, value) => ({ operation: 'set', path, value });

test('单步、倒退和跳回合可还原同一张卡以及双方各自的回合', () => {
    const replay = createReplayTimeline({ opening, steps: [
        { kind: 'TurnStart', owner: 'a', changes: [set(['players', 'a', 'turnNumber'], 1)] },
        { kind: 'PlayCard', owner: 'a', cardId: 'x', changes: [set(['zones', 'a', 'hand'], []), set(['zones', 'a', 'field'], ['x'])] },
        { kind: 'TurnStart', owner: 'b', changes: [set(['players', 'b', 'turnNumber'], 1), set(['game', 'currentPlayerId'], 'b')] }
    ] });
    assert.deepEqual(replay.turns.map(t => [t.index, t.title]), [[0,'起手调度'], [1,'第 1 回合 · 先手'], [3,'第 1 回合 · 后手']]);
    assert.equal(replay.steps[2].label, '使用卡牌 · 夜莺');
    assert.equal(replay.boardAt(3).players[0].field[0].fusedCards[0].instanceId, 'y');
    assert.equal(replay.boardAt(1).players[0].hand[0].instanceId, 'x');
    assert.equal(replay.boardAt(0).players[0].turnNumber, 0);
    assert.deepEqual(opening.zones.a.hand, ['x']);
});

test('旧版整帧录像可从终局跳回起手和换边后的回合', () => {
    const frames = [0, 1, 2].map((n) => ({ boundary: ['initial', 'completed_action', 'ended'][n],
        state: { currentPlayerId: n === 2 ? 'b' : 'a', players: [
            { playerId: 'a', turnNumber: Number(n > 0), isFirst: true }, { playerId: 'b', turnNumber: Number(n > 1) }
        ] } }));
    frames[1].effects = [{ method: 'HealApplied', payload: { targetUid: 'x', healAmount: 3 } }];
    const replay = createReplayTimeline({ frames });
    assert.equal(replay.steps.at(-1).label, '对局结束');
    assert.deepEqual(replay.turns.map(t => t.index), [0, 1, 2]);
    assert.equal(replay.boardAt(2).currentPlayerId, 'b');
    assert.equal(replay.boardAt(0).players[0].turnNumber, 0);
    assert.deepEqual(replay.steps[1].effects, frames[1].effects);
});

test('倒回出牌后再前进仍能按原本的费用与模式播放落场', () => {
    const cast = { playedCardInstance: { instanceId: 'x', type: 'Amulet' }, sourcePlayerId: 'a', paidCost: 2, playMode: 'Crystalize' };
    const move = { method: 'CardMoved', payload: { cardUid: 'x', toZone: 'Field' } };
    const replay = createReplayTimeline({ opening, steps: [
        { kind: 'PlayCard', changes: [], effects: [{ method: 'CardPlayed', payload: cast }] },
        { kind: 'Summon', changes: [], effects: [move] },
        { kind: 'Summon', changes: [], effects: [move] }
    ] });
    assert.equal(replay.steps[2].effects[0].cast, cast);
    assert.equal(replay.steps[3].effects[0].cast, undefined);
});
