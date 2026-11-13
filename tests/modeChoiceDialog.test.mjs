import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const load = async path => import(`data:text/javascript;base64,${Buffer.from(await readFile(new URL(path, import.meta.url), 'utf8')).toString('base64')}`);
const { splitModeNumber, getModeOptionContent, toggleModeSelection } = await load('../src/ui/modeChoiceDialog.js');
const { shouldAutoConfirmChoice } = await load('../src/choiceSettingsPolicy.js');
const uiSource = await readFile(new URL('../src/ui.js', import.meta.url), 'utf8');
const functions = ['getChoiceMinMax', 'isChoiceSelectionValid', 'isFixedChoiceCount', 'isCurrentChoiceRequest', 'confirmPlayerChoice', 'handleChoiceTargetClick'];
const choiceCode = functions.map(name => {
    const start = uiSource.indexOf(`function ${name}(`);
    assert.ok(start >= 0, `${name} exists`);
    const end = uiSource.indexOf('\n}', start) + 2;
    return (uiSource.slice(start - 6, start) === 'async ' ? 'async ' : '') + uiSource.slice(start, end);
}).join('\n');

function harness({ mode = true, max = 2, min = max, auto = false, submit } = {}) {
    const submissions = [];
    const context = vm.createContext({
        Set, console, shouldAutoConfirmChoice, toggleModeSelection,
        isAwaitingPlayerChoice: true,
        currentChoiceSubmission: null,
        modeChoiceDialog: null,
        currentChoiceRequest: { PendingChoiceId: 'choice', SourceAbilityRuntimeId: 'ability', TargetKey: mode ? 'decision' : 'target', SelectableTargetIds: ['a', 'b', 'c'], MinSelectionCount: min, MaxSelectionCount: max },
        currentChoiceSelectedTargetIds: new Set(),
        gameState: {}, renderHighlights() {}, updateChoiceOverlayUI() {}, canSelectChoiceTarget: () => true,
        readOperationSettings: () => ({ autoConfirmModes: auto, autoConfirmTargets: auto }),
        submitPlayerChoice: async (...args) => { submissions.push(args); return submit ? submit(...args) : true; },
        endPlayerChoice() { context.isAwaitingPlayerChoice = false; context.currentChoiceRequest = null; context.currentChoiceSelectedTargetIds = new Set(); context.currentChoiceSubmission = null; }
    });
    vm.runInContext(choiceCode, context);
    return { context, submissions, click: key => context.handleChoiceTargetClick(key), confirm: () => context.confirmPlayerChoice(), selected: () => [...context.currentChoiceSelectedTargetIds] };
}

test('extracts leading bracket numbers and leaves effect values intact', () => {
    assert.deepEqual(splitModeNumber(' （０２）：抽取 2 张卡牌。', 0), { number: '（2）', text: '抽取 2 张卡牌。', explicit: true });
    assert.equal(splitModeNumber('(12) 效果', 0).number, '（12）');
    assert.deepEqual(splitModeNumber('抽取 2 张卡牌。', 2), { number: '（3）', text: '抽取 2 张卡牌。', explicit: false });
    assert.equal(splitModeNumber('（2张）卡牌', 1).text, '（2张）卡牌');
    assert.equal(splitModeNumber('效果（2）', 1).text, '效果（2）');
});

test('supports numbering in either field without repeating the same effect', () => {
    assert.deepEqual(getModeOptionContent({ Name: '（1）抽牌', Description: '(1)抽牌' }, 0), { number: '（1）', title: '抽牌', description: '' });
    assert.deepEqual(getModeOptionContent({ Name: 'None', Description: '（２）回复 3 点生命值。' }, 0), { number: '（2）', title: '', description: '回复 3 点生命值。' });
    assert.deepEqual(getModeOptionContent({ Name: '（3）', Description: '【守护】与 +2/+2。' }, 0), { number: '（3）', title: '', description: '【守护】与 +2/+2。' });
    assert.deepEqual(getModeOptionContent({ Name: '抽牌', Description: '抽取 2 张卡牌。' }, 2), { number: '（3）', title: '抽牌', description: '抽取 2 张卡牌。' });
});

test('single-choice switches directly and clicking the selected mode clears it', async () => {
    const h = harness({ max: 1 });
    await h.click('a'); await h.click('b');
    assert.deepEqual(h.selected(), ['b']);
    await h.click('b');
    assert.deepEqual(h.selected(), []);
    await h.confirm();
    assert.equal(h.submissions.length, 0);
});

test('full multi-choice preserves existing picks until one is explicitly deselected', async () => {
    const h = harness();
    for (const key of ['a', 'b', 'c']) await h.click(key);
    assert.deepEqual(h.selected(), ['a', 'b']);
    await h.click('a'); await h.click('c');
    assert.deepEqual(h.selected(), ['b', 'c']);
    await h.click('invalid');
    assert.deepEqual(h.selected(), ['b', 'c']);
    await h.confirm();
    assert.deepEqual(Array.from(h.submissions[0][1]), ['b', 'c']);
    assert.equal(h.context.isAwaitingPlayerChoice, false);
});

test('ordinary target choices retain their existing selection limit', async () => {
    const h = harness({ mode: false });
    for (const key of ['a', 'b', 'c']) await h.click(key);
    assert.deepEqual(h.selected(), ['a', 'b']);
});

test('auto-confirm respects settings, prevents duplicate submissions, and recovers after failure', async () => {
    let finish;
    const h = harness({ auto: true, submit: () => new Promise(resolve => { finish = resolve; }) });
    await h.click('a');
    assert.equal(h.submissions.length, 0);
    const pending = h.click('b');
    await h.click('c'); await h.confirm();
    assert.equal(h.submissions.length, 1);
    assert.deepEqual(h.selected(), ['a', 'b']);
    finish(false); await pending;
    assert.equal(h.context.currentChoiceSubmission, null);
    assert.equal(h.context.isAwaitingPlayerChoice, true);
    await h.click('a');
    const retry = h.click('c');
    assert.deepEqual(h.selected(), ['b', 'c']);
    finish(true); await retry;
    assert.equal(h.context.isAwaitingPlayerChoice, false);
});

test('an earlier submission cannot dismiss the next mode prompt', async () => {
    let finish;
    const h = harness({ max: 1, auto: true, submit: () => new Promise(resolve => { finish = resolve; }) });
    const pending = h.click('a');
    h.context.currentChoiceRequest = { ...h.context.currentChoiceRequest, PendingChoiceId: 'next' };
    h.context.currentChoiceSubmission = null;
    finish(true); await pending;
    assert.equal(h.context.currentChoiceRequest.PendingChoiceId, 'next');
    assert.equal(h.context.isAwaitingPlayerChoice, true);
});

test('variable counts allow explicit empty confirmation without auto-submitting', async () => {
    const h = harness({ min: 0, max: 2, auto: true });
    await h.click('a'); await h.click('a');
    assert.equal(h.submissions.length, 0);
    await h.confirm();
    assert.deepEqual(Array.from(h.submissions[0][1]), []);
});
