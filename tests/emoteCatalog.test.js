const {
    findEmoteById,
    loadEmoteCatalog,
    normalizeEmoteCatalogResponse,
    resetEmoteCatalogCache
} = require('../src/emoteCatalog.js');

describe('emoteCatalog', () => {
    beforeEach(() => {
        resetEmoteCatalogCache();
        global.fetch = jest.fn();
    });

    afterEach(() => {
        delete global.fetch;
    });

    test('normalizes packs, filters empty packs and keeps safe item metadata', () => {
        const catalog = normalizeEmoteCatalogResponse({
            packs: [
                {
                    id: 'default',
                    name: 'default',
                    items: [
                        { id: 'default/smile.png', packId: 'default', name: 'Smile', url: '/assets/emotes/default/smile.png' },
                        { id: '', packId: 'default', name: 'bad', url: '/assets/emotes/default/bad.png' }
                    ]
                },
                { id: 'empty', name: 'empty', items: [] }
            ]
        });

        expect(catalog.packs).toHaveLength(1);
        expect(catalog.packs[0].id).toBe('default');
        expect(catalog.packs[0].items).toEqual([
            {
                id: 'default/smile.png',
                packId: 'default',
                name: 'Smile',
                url: '/assets/emotes/default/smile.png'
            }
        ]);
        expect(findEmoteById(catalog, 'default/smile.png')?.name).toBe('Smile');
        expect(findEmoteById(catalog, 'missing.png')).toBeNull();
    });

    test('loadEmoteCatalog caches successful results for the page lifecycle', async () => {
        global.fetch.mockResolvedValue({
            ok: true,
            json: jest.fn().mockResolvedValue({
                packs: [
                    {
                        id: 'default',
                        items: [
                            { id: 'default/smile.png', packId: 'default', name: 'smile', url: '/assets/emotes/default/smile.png' }
                        ]
                    }
                ]
            })
        });

        const first = await loadEmoteCatalog();
        const second = await loadEmoteCatalog();

        expect(first.ok).toBe(true);
        expect(second).toBe(first);
        expect(global.fetch).toHaveBeenCalledTimes(1);
    });

    test('loadEmoteCatalog returns failed state when the request fails', async () => {
        global.fetch.mockResolvedValue({ ok: false, status: 500 });

        const result = await loadEmoteCatalog();

        expect(result.ok).toBe(false);
        expect(result.catalog.packs).toEqual([]);
        expect(result.error).toContain('500');
    });
});
