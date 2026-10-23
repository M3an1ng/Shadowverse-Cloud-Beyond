describe('preview description lookup', () => {
    beforeEach(() => {
        jest.resetModules();
        global.fetch = jest.fn(async (url) => {
            const enumName = String(url).split('/').pop();
            const payloads = {
                CardKeyword: {},
                AbilityKeyword: {},
                DeckKeyword: {
                    UnlimitedDeck: '无垠界域牌组'
                }
            };

            return {
                ok: true,
                json: async () => payloads[enumName] || {}
            };
        });
    });

    afterEach(() => {
        jest.resetModules();
        delete global.fetch;
    });

    test('resolves deck keywords from enum localization', async () => {
        const { resolveKeywordInfo } = require('../src/previewDescriptionLookup.js');

        const info = await resolveKeywordInfo('无垠界域牌组');

        expect(info).toEqual(expect.objectContaining({
            kind: 'deck',
            key: 'UnlimitedDeck',
            name: '无垠界域牌组'
        }));
        expect(info.description).toContain('11张不同类型的卡片');
    });

    test('keeps existing keyword resolution when deck keyword localization fails', async () => {
        global.fetch = jest.fn(async (url) => {
            const enumName = String(url).split('/').pop();
            if (enumName === 'DeckKeyword') {
                return {
                    ok: false,
                    status: 404,
                    statusText: 'Not Found',
                    json: async () => ({})
                };
            }

            const payloads = {
                CardKeyword: {
                    Storm: '疾驰'
                },
                AbilityKeyword: {}
            };

            return {
                ok: true,
                json: async () => payloads[enumName] || {}
            };
        });
        const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});

        try {
            const { resolveKeywordInfo } = require('../src/previewDescriptionLookup.js');

            const info = await resolveKeywordInfo('疾驰');

            expect(global.fetch).toHaveBeenCalledWith('/api/localization/enums/DeckKeyword');
            expect(info).toEqual(expect.objectContaining({
                kind: 'card',
                key: 'Storm',
                name: '疾驰'
            }));
        } finally {
            warnSpy.mockRestore();
        }
    });

    test.each([
        ['中文括号', '妖精（1002）'],
        ['英文括号', '妖精(1002)']
    ])('使用%s中的卡牌ID区分同名衍生物', async (_label, reference) => {
        const { resolveDerivedCardMatch } = require('../src/previewDescriptionLookup.js');
        const cards = [
            { id: 1001, name: '妖精' },
            { id: 1002, name: '妖精' }
        ];

        const result = await resolveDerivedCardMatch(reference, { cards });

        expect(result).toEqual(expect.objectContaining({
            match: cards[1],
            matches: cards,
            reason: null,
            requestedId: '1002'
        }));
    });
});
