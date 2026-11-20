import test from 'node:test';
import assert from 'node:assert/strict';
import { getEffectSource, getEffectSourceDetail, renderAttachedNumericEffects, summarizeAttachedNumericEffects } from '../../src/liveEffectSummary.js';

test('combined stats link each distinct source appearance once', () => {
    const modifiers = ['Attack', 'Health'].map(field => ({ field, layer: 'Additive', value: 2 }));
    const first = {
        sourceCardDefinitionId: 1473441, sourceCardName: '夜莺', sourceAbilityName: '连锁能力1',
        sourceCardOwnerPlayerId: 'first', sourceCardArtPreview: '/first.webp', numericEffects: modifiers
    };
    const second = { ...first, sourceCardOwnerPlayerId: 'second', sourceCardArtPreview: '/second.webp' };
    const groups = summarizeAttachedNumericEffects([first, first, second]);
    assert.deepEqual(groups.map(group => group.value), [6, 6]);
    const html = renderAttachedNumericEffects(groups);
    assert.deepEqual([...html.matchAll(/data-source-index="(\d+)"/g)].map(match => match[1]), ['0', '2']);
    assert.match(html, /src="\/first.webp"/);
    assert.match(html, /src="\/second.webp"/);
});

test('source details keep the recorded owner and face, including deliberately empty style text', () => {
    const base = { Id: 1473441, Name: '刺血玫瑰·夜莺', Description: '原卡卡文', ArtPreview: '/base.webp', ArtDetail: '/base-detail.webp' };
    const source = getEffectSource({
        SourceCardName: '笼中囚羽·夜莺', SourceCardOwnerPlayerId: 'opponent',
        SourceCardDescription: '', SourceCardArtPreview: '/caged.webp', SourceCardArtDetail: '/caged-detail.webp'
    }, 0);
    const detail = getEffectSourceDetail(base, source);
    assert.equal(detail.OwnerPlayerId, 'opponent');
    assert.equal(detail.Name, source.name);
    assert.equal(detail.artPreview, source.art);
    assert.equal(detail.ArtDetail, source.detailArt);
    assert.equal(detail.description, '');
    assert.equal(detail.Description, '');
    assert.equal(detail.Id, base.Id);
    const legacy = getEffectSourceDetail(base, getEffectSource({ sourceCardName: base.Name, sourceCardArtPreview: '' }, 0));
    assert.equal(legacy.ArtPreview, base.ArtPreview);
    assert.equal(legacy.Description, base.Description);
});
