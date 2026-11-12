import test from 'node:test';
import assert from 'node:assert/strict';
import { build } from 'esbuild';

const result = await build({ entryPoints: ['src/discoverParameters.js'], bundle: true, format: 'esm', platform: 'node', write: false });
const { isActiveDiscoverParameter } = await import('data:text/javascript;base64,' + Buffer.from(result.outputFiles[0].text).toString('base64'));
for (const mode of ['Copy', 'copy', 1, 'Instance', 'instance', 2, { type: 'static', value: 'Instance' }]) {
    test(`instance list is used for ${JSON.stringify(mode)}`, () => {
        assert.equal(isActiveDiscoverParameter('Discover', { Mode: mode }, 'CopySourceTargets'), true);
        assert.equal(isActiveDiscoverParameter('Discover', { Mode: mode }, 'CardDefinitionIds'), false);
    });
}
test('definition mode and other effects keep their parameters', () => {
    assert.equal(isActiveDiscoverParameter('Discover', {}, 'CardDefinitionIds'), true);
    assert.equal(isActiveDiscoverParameter('Discover', { Mode: 0 }, 'CopySourceTargets'), false);
    assert.equal(isActiveDiscoverParameter('Discover', { mOdE: '2' }, 'copysourcetargets'), true);
    assert.equal(isActiveDiscoverParameter('Draw', { Mode: 'Instance' }, 'CardDefinitionIds'), true);
});
