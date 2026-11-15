import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../src/liveEffectSummary.js', import.meta.url));
const { summarizeAttachedNumericEffects, numericEffectTitle } = await import(`data:text/javascript;base64,${source.toString('base64')}`);

const set = (field, value, extra = {}) => ({ Field: field, Layer: 'Set', Value: value, ...extra });

test('legacy hand updates show the last assignment per stat from the same source', () => {
    const groups = summarizeAttachedNumericEffects([{
        SourceCardName: '大型作战测试',
        NumericEffects: [set('Cost', 2), set('Attack', 3), set('Cost', 1), set('Defense', 8),
            set('Cost', 1), set('Health', 9), set('Attack', 5), set('Cost', 0)]
    }]);
    assert.deepEqual(groups.map(numericEffectTitle), ['费用 设为 0', '攻击 设为 5', '生命 设为 9']);
    assert.ok(groups.every(group => group.sources[0].name === '大型作战测试'));
});

test('server application order wins even when the obsolete source is listed last', () => {
    const groups = summarizeAttachedNumericEffects([
        { sourceCardName: '最新来源', numericEffects: [{ field: 'Cost', layer: 'Set', value: 2 }] },
        { sourceCardName: '较早来源', numericEffects: [{ field: 'Cost', layer: 'Set', value: 7, isSuperseded: true }] }
    ]);
    assert.deepEqual(groups.map(numericEffectTitle), ['费用 设为 2']);
    assert.equal(groups[0].sources[0].name, '最新来源');
});

test('additive modifiers still combine by policy and assignments to ability values stay separate', () => {
    const add = (value, policy) => ({ Field: 'Attack', Layer: 'Additive', Value: value, PolicyKey: policy });
    const groups = summarizeAttachedNumericEffects([
        { NumericEffects: [add(2, 'permanent'), add(4, 'endOfTurn'), set('Act', 2), set('Act', 1)] },
        { NumericEffects: [add(3, 'permanent')] }
    ]);
    assert.deepEqual(groups.map(numericEffectTitle), ['攻击 +5', '攻击 +4', '启动费用 设为 2', '启动费用 设为 1']);
});
