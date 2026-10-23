const { buildCounterSummaryEntries } = require('../src/counterSummary.js');

describe('counter summary entries', () => {
    const resolveLabel = (labelMap, key) => {
        if (!labelMap || typeof labelMap !== 'object') {
            return '';
        }

        const normalizedKey = String(key ?? '').trim().toLowerCase();
        for (const [entryKey, entryValue] of Object.entries(labelMap)) {
            if (String(entryKey).trim().toLowerCase() !== normalizedKey) {
                continue;
            }

            return typeof entryValue === 'string' ? entryValue : '';
        }

        return '';
    };

    test('uses server ordered summary entries before object key sorting fallback', () => {
        const entries = buildCounterSummaryEntries({
            summaryEntries: [
                { key: 'water', label: '水', value: 1 },
                { key: 'zero', label: '零', value: 0 },
                { key: 'amber', label: '琥珀', value: 2 }
            ],
            localCounterMap: {
                amber: 2,
                water: 1
            },
            localLabelMap: {
                amber: '琥珀',
                water: '水'
            },
            resolveLabel
        });

        expect(entries).toEqual([
            { key: 'water', label: '水', value: 1 },
            { key: 'amber', label: '琥珀', value: 2 }
        ]);
    });

    test('shows referenced owner counters for ordinary cards and keeps zero values', () => {
        const entries = buildCounterSummaryEntries({
            localCounterMap: {
                spellboost: 2
            },
            localLabelMap: {
                spellboost: '魔力增幅数',
                evolution_count: '本局进化次数'
            },
            ownerCounterMap: {
                evolution_count: 0,
                unrelated_counter: 7
            },
            ownerLabelMap: {
                evolution_count: '本局进化次数',
                unrelated_counter: '无关计数'
            },
            ownerReferenceKeys: ['spellboost', 'evolution_count'],
            includeOwnerCounters: true,
            showZeroOwnerEntries: true,
            resolveLabel
        });

        expect(entries).toEqual([
            { key: 'evolution_count', label: '本局进化次数', value: 0 },
            { key: 'spellboost', label: '魔力增幅数', value: 2 }
        ]);
    });

    test('hides player system counters when leader preview is configured to suppress them', () => {
        const entries = buildCounterSummaryEntries({
            ownerCounterMap: {
                combo: 3,
                graveyard: 5,
                evolution_count: 1
            },
            ownerLabelMap: {
                combo: '连击数',
                graveyard: '墓场数',
                evolution_count: '本局进化次数'
            },
            ownerReferenceKeys: ['combo', 'graveyard', 'evolution_count'],
            includeOwnerCounters: true,
            hideOwnerSystemKeys: true,
            showZeroOwnerEntries: true,
            resolveLabel
        });

        expect(entries).toEqual([
            { key: 'evolution_count', label: '本局进化次数', value: 1 }
        ]);
    });
});
