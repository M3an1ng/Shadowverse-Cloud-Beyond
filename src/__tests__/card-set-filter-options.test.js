import {
    CARD_SET_UNSPECIFIED_ENVIRONMENT,
    buildGroupedCardSetOptions,
    matchesCardSetEnvironment
} from '../cardSetFilterOptions.js';

function getCardSetInfo(card) {
    return {
        name: card.name,
        code: card.code,
        environments: card.environments
    };
}

function normalizeEnvironment(value) {
    return String(value ?? '').trim().toLowerCase();
}

function getEnvironmentLabel(value) {
    if (value === CARD_SET_UNSPECIFIED_ENVIRONMENT) {
        return '未指定环境';
    }

    return value === 'rotation' ? '指定' : '无限制';
}

describe('card set filter options', () => {
    test('builds grouped set options in environment order', () => {
        const cards = [
            { name: 'Beta', code: '102', environments: ['unlimited'] },
            { name: 'Alpha', code: '001', environments: ['rotation'] }
        ];

        const groups = buildGroupedCardSetOptions(cards, {
            getCardSetInfo,
            normalizeEnvironment,
            getEnvironmentLabel,
            knownEnvironmentOptions: [
                { value: 'rotation', label: '指定' },
                { value: 'unlimited', label: '无限制' }
            ]
        });

        expect(groups.map(group => group.label)).toEqual(['指定', '无限制']);
        expect(groups[0].options.map(option => option.label)).toEqual(['Alpha']);
        expect(groups[1].options.map(option => option.label)).toEqual(['Beta']);
    });

    test('keeps multi-environment sets only in the first matching group', () => {
        const cards = [
            { name: 'Beta', code: '102', environments: ['unlimited'] },
            { name: 'Alpha', code: '001', environments: ['rotation', 'unlimited'] }
        ];

        const groups = buildGroupedCardSetOptions(cards, {
            getCardSetInfo,
            normalizeEnvironment,
            getEnvironmentLabel,
            knownEnvironmentOptions: [
                { value: 'rotation', label: '指定' },
                { value: 'unlimited', label: '无限制' }
            ]
        });

        expect(groups[0].options.map(option => option.label)).toEqual(['Alpha']);
        expect(groups[1].options.map(option => option.label)).toEqual(['Beta']);
    });

    test('treats missing environments as unspecified bucket', () => {
        const info = {
            name: 'Legacy',
            environments: []
        };

        expect(matchesCardSetEnvironment(info, CARD_SET_UNSPECIFIED_ENVIRONMENT, normalizeEnvironment)).toBe(true);
        expect(matchesCardSetEnvironment(info, 'rotation', normalizeEnvironment)).toBe(false);
    });
});
