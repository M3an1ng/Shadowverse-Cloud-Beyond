import { loadCardDefinitions } from '../src/cardDefinitionsLoader.js';
import { MetadataManager } from '../src/metadataManager.js';

const originalFetch = global.fetch;

function response(payload) {
    return { ok: true, json: async () => payload };
}

function deferred() {
    let resolve;
    let reject;
    const promise = new Promise((accept, fail) => { resolve = accept; reject = fail; });
    return { promise, resolve, reject };
}

function metadata(name) {
    return { effects: [{ effectTypeName: name }], events: [], dsl: {}, queryOperations: [] };
}

afterEach(() => {
    global.fetch = originalFetch;
    jest.restoreAllMocks();
});

test('invalid card payload rejects without replacing a valid cache or publishing its revision', async () => {
    const cachedCards = [{ id: 17, name: 'Published card' }];
    let cached = cachedCards;
    const setCached = jest.fn(value => { cached = value; });
    const onResponse = jest.fn();
    global.fetch = jest.fn().mockResolvedValue(response({ message: 'Not a card list' }));

    await expect(loadCardDefinitions({
        force: true,
        getCached: () => cached,
        setCached,
        onResponse
    })).rejects.toThrow('Card definitions response must be an array.');

    expect(cached).toBe(cachedCards);
    expect(setCached).not.toHaveBeenCalled();
    expect(onResponse).not.toHaveBeenCalled();
});

test('a response from before reload cannot replace the newly loaded metadata', async () => {
    const manager = new MetadataManager();
    const oldResponse = deferred();
    const current = metadata('current');
    global.fetch = jest.fn()
        .mockReturnValueOnce(oldResponse.promise)
        .mockResolvedValueOnce(response(current));

    const oldRequest = manager.initialize();
    await expect(manager.reload()).resolves.toBe(current);
    oldResponse.resolve(response(metadata('old')));
    await oldRequest;

    expect(manager.getMetadata()).toBe(current);
    expect(manager.getEffects()).toEqual([{ effectTypeName: 'current' }]);
});

test('incomplete metadata rejects without marking the manager loaded', async () => {
    const manager = new MetadataManager();
    jest.spyOn(console, 'error').mockImplementation(() => {});
    global.fetch = jest.fn().mockResolvedValue(response({ effects: [] }));

    await expect(manager.initialize()).rejects.toThrow('元数据缺少字段: events');

    expect(manager.isMetadataLoaded()).toBe(false);
    expect(() => manager.getMetadata()).toThrow('元数据尚未加载');
});

test.each(['effects', 'events', 'queryOperations'])('invalid %s metadata collection rejects', async field => {
    const manager = new MetadataManager();
    jest.spyOn(console, 'error').mockImplementation(() => {});
    global.fetch = jest.fn().mockResolvedValue(response({ ...metadata('invalid'), [field]: {} }));

    await expect(manager.initialize()).rejects.toThrow(`元数据字段 ${field} 必须是数组`);
    expect(manager.isMetadataLoaded()).toBe(false);
});

test('an empty card list remains a valid cache hit', async () => {
    const cards = [];
    let cached = null;
    global.fetch = jest.fn().mockResolvedValue(response(cards));
    const options = { getCached: () => cached, setCached: value => { cached = value; } };

    await expect(loadCardDefinitions(options)).resolves.toBe(cards);
    await expect(loadCardDefinitions(options)).resolves.toBe(cards);
    expect(global.fetch).toHaveBeenCalledTimes(1);
});

test('an HTTP failure preserves the published card cache', async () => {
    const published = [{ id: 17 }];
    const setCached = jest.fn();
    const onResponse = jest.fn();
    global.fetch = jest.fn().mockResolvedValue({ ok: false, status: 503, json: async () => ({ message: 'Unavailable' }) });

    await expect(loadCardDefinitions({ force: true, getCached: () => published, setCached, onResponse }))
        .rejects.toThrow('Unavailable');
    expect(setCached).not.toHaveBeenCalled();
    expect(onResponse).not.toHaveBeenCalled();
});

test('invalid card JSON cannot publish a successful revision', async () => {
    const onResponse = jest.fn();
    const setCached = jest.fn();
    global.fetch = jest.fn().mockResolvedValue({ ok: true, json: async () => { throw new SyntaxError('Invalid JSON'); } });

    await expect(loadCardDefinitions({ onResponse, setCached })).rejects.toThrow('Invalid JSON');
    expect(setCached).not.toHaveBeenCalled();
    expect(onResponse).not.toHaveBeenCalled();
});

test('cleanup revokes an unfinished metadata request', async () => {
    const manager = new MetadataManager();
    const pending = deferred();
    global.fetch = jest.fn().mockReturnValue(pending.promise);
    const request = manager.initialize();

    manager.cleanup();
    pending.resolve(response(metadata('obsolete')));
    await request;

    expect(manager.isMetadataLoaded()).toBe(false);
    expect(() => manager.getMetadata()).toThrow('元数据尚未加载');
});

test('an obsolete failure cannot release the current request for a duplicate fetch', async () => {
    const manager = new MetadataManager();
    const oldResponse = deferred();
    const newResponse = deferred();
    const current = metadata('current');
    jest.spyOn(console, 'error').mockImplementation(() => {});
    global.fetch = jest.fn().mockReturnValueOnce(oldResponse.promise).mockReturnValueOnce(newResponse.promise);
    const obsolete = manager.initialize().catch(error => error);
    const active = manager.reload();

    oldResponse.reject(new Error('Obsolete request failed'));
    await obsolete;
    const shared = manager.initialize();
    expect(global.fetch).toHaveBeenCalledTimes(2);
    newResponse.resolve(response(current));
    await expect(active).resolves.toBe(current);
    await expect(shared).resolves.toBe(current);
    expect(manager.getMetadata()).toBe(current);
});

test('a failed initialization can be explicitly started again without automatic retry', async () => {
    const manager = new MetadataManager();
    const current = metadata('recovered');
    jest.spyOn(console, 'error').mockImplementation(() => {});
    global.fetch = jest.fn().mockRejectedValueOnce(new Error('Network failure')).mockResolvedValueOnce(response(current));

    await expect(manager.initialize()).rejects.toThrow('Network failure');
    expect(global.fetch).toHaveBeenCalledTimes(1);
    expect(manager.isMetadataLoaded()).toBe(false);
    await expect(manager.initialize()).resolves.toBe(current);
    expect(global.fetch).toHaveBeenCalledTimes(2);
});

test('concurrent initialization shares one request and keeps valid empty metadata', async () => {
    const manager = new MetadataManager();
    const pending = deferred();
    const empty = { effects: [], events: [], dsl: {}, queryOperations: [] };
    global.fetch = jest.fn().mockReturnValue(pending.promise);
    const first = manager.initialize();
    const second = manager.initialize();
    expect(global.fetch).toHaveBeenCalledTimes(1);
    pending.resolve(response(empty));
    await expect(first).resolves.toBe(empty);
    await expect(second).resolves.toBe(empty);
    await expect(manager.initialize()).resolves.toBe(empty);
    expect(manager.getEffects()).toEqual([]);
    expect(global.fetch).toHaveBeenCalledTimes(1);
});

test.each([null, []])('invalid metadata root %p is never published', async payload => {
    const manager = new MetadataManager();
    jest.spyOn(console, 'error').mockImplementation(() => {});
    global.fetch = jest.fn().mockResolvedValue(response(payload));
    await expect(manager.initialize()).rejects.toThrow();
    expect(manager.isMetadataLoaded()).toBe(false);
});

test.each([null, []])('invalid DSL metadata %p is never published', async dsl => {
    const manager = new MetadataManager();
    jest.spyOn(console, 'error').mockImplementation(() => {});
    global.fetch = jest.fn().mockResolvedValue(response({ ...metadata('invalid'), dsl }));
    await expect(manager.initialize()).rejects.toThrow('元数据字段 dsl 必须是对象');
    expect(manager.isMetadataLoaded()).toBe(false);
});
