import {
  clearAbilityDataCache,
  ensureAbilityData,
  ensureAbilityDefinitions,
  ensureCardDefinitions,
  getAbilityDataSnapshot,
  markAbilityDataCacheDirty,
  removeAbilityDataCacheEntry,
  upsertAbilityDataCacheEntry,
} from '../src/abilityDataCache.js';
import { notifyCardCatalogInvalidated } from '../src/dataChangeEvents.js';

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((done, fail) => { resolve = done; reject = fail; });
  return { promise, resolve, reject };
}

function jsonResponse(body) {
  return { ok: true, json: async () => body };
}

beforeEach(() => {
  clearAbilityDataCache();
  global.fetch = jest.fn();
});

afterEach(() => {
  clearAbilityDataCache();
  delete global.fetch;
});

describe.each([
  ['cards', '/api/cards', ensureCardDefinitions],
  ['abilities', '/api/abilities', ensureAbilityDefinitions],
])('%s 目录的请求所有权', (key, endpoint, load) => {
  const original = [{ id: 1, cardId: 10, name: 'original' }];
  const latest = [{ id: 2, cardId: 20, name: 'latest' }];

  test('并发读取共用一次请求，并缓存成功结果', async () => {
    const request = deferred();
    global.fetch.mockReturnValue(request.promise);
    const first = load();
    const second = load();
    request.resolve(jsonResponse(original));
    expect(await first).toBe(original);
    expect(await second).toBe(original);
    expect(await load()).toBe(original);
    expect(global.fetch).toHaveBeenCalledTimes(1);
    expect(global.fetch.mock.calls[0][0]).toBe(endpoint);
  });

  test('失效前发出的响应不能覆盖已经刷新的目录', async () => {
    const oldRequest = deferred();
    global.fetch.mockReturnValueOnce(oldRequest.promise)
      .mockResolvedValueOnce(jsonResponse(latest));
    const oldLoad = load();
    clearAbilityDataCache();
    await load();
    oldRequest.resolve(jsonResponse(original));
    await oldLoad;
    expect(getAbilityDataSnapshot()[key]).toBe(latest);
  });

  test('旧请求结束不能清空新请求，也不能触发重复加载', async () => {
    const oldRequest = deferred();
    const newRequest = deferred();
    global.fetch.mockReturnValueOnce(oldRequest.promise)
      .mockReturnValue(newRequest.promise);
    const oldLoad = load();
    clearAbilityDataCache();
    const newLoad = load();
    oldRequest.resolve(jsonResponse(original));
    await oldLoad;
    const observedPending = getAbilityDataSnapshot()[key];
    const thirdLoad = load();
    const callsWhilePending = global.fetch.mock.calls.length;
    newRequest.resolve(jsonResponse(latest));
    await Promise.all([newLoad, thirdLoad]);
    expect(observedPending).toEqual([]);
    expect(callsWhilePending).toBe(2);
    expect(await thirdLoad).toBe(latest);
  });

  test('HTTP 失败明确上抛，下一次显式读取仍可加载', async () => {
    global.fetch.mockResolvedValueOnce({ ok: false, status: 503, statusText: 'Unavailable' })
      .mockResolvedValueOnce(jsonResponse(latest));
    await expect(load()).rejects.toThrow('HTTP 503: Unavailable');
    expect(await load()).toBe(latest);
    expect(global.fetch).toHaveBeenCalledTimes(2);
  });

  test('旧请求失败不能清除新目录，失败仍反馈给原调用者', async () => {
    const oldRequest = deferred();
    global.fetch.mockReturnValueOnce(oldRequest.promise)
      .mockResolvedValueOnce(jsonResponse(latest));
    const oldLoad = load();
    const oldOutcome = oldLoad.then(() => null, error => error);
    clearAbilityDataCache();
    await load();
    const error = new Error('connection lost');
    oldRequest.reject(error);
    expect(await oldOutcome).toBe(error);
    expect(getAbilityDataSnapshot()[key]).toBe(latest);
  });

  test('成功的空数组是有效缓存，不会重复请求', async () => {
    const empty = [];
    global.fetch.mockResolvedValueOnce(jsonResponse(empty));
    expect(await load()).toBe(empty);
    expect(await load()).toBe(empty);
    expect(global.fetch).toHaveBeenCalledTimes(1);
  });

  test('非数组响应显式失败，而不是伪装成空目录', async () => {
    global.fetch.mockResolvedValueOnce(jsonResponse({ unexpected: true }));
    await expect(load()).rejects.toThrow(TypeError);
  });
});

test('能力修改和删除共同维护列表、ID 索引与卡牌分组', async () => {
  global.fetch.mockResolvedValueOnce(jsonResponse([
    { Id: 1, CardId: 10 }, { id: 2, cardId: 10 },
  ]));
  await ensureAbilityDefinitions();
  const updated = { id: 1, cardId: 20, name: 'updated' };
  upsertAbilityDataCacheEntry(updated);
  let snapshot = getAbilityDataSnapshot();
  expect(snapshot.abilityIndex.get(1)).toBe(updated);
  expect(snapshot.abilitiesByCard.get(10)).toEqual([{ id: 2, cardId: 10 }]);
  expect(snapshot.abilitiesByCard.get(20)).toEqual([updated]);
  removeAbilityDataCacheEntry(1);
  snapshot = getAbilityDataSnapshot();
  expect(snapshot.abilityIndex.has(1)).toBe(false);
  expect(snapshot.abilitiesByCard.has(20)).toBe(false);
  expect(snapshot.abilities).toEqual([{ id: 2, cardId: 10 }]);
});

test('仅能力失效不会清除已加载的卡牌目录', async () => {
  const cards = [{ id: 10 }];
  global.fetch.mockImplementation(async url => jsonResponse(url === '/api/cards' ? cards : []));
  await ensureAbilityData();
  markAbilityDataCacheDirty();
  notifyCardCatalogInvalidated({ reason: 'ability-updated' });
  expect(await ensureCardDefinitions()).toBe(cards);
  expect(global.fetch).toHaveBeenCalledTimes(2);
});

test('卡牌变更通知会撤销尚未返回的卡牌请求发布权', async () => {
  const oldRequest = deferred();
  const latest = [{ id: 2 }];
  global.fetch.mockReturnValueOnce(oldRequest.promise)
    .mockResolvedValueOnce(jsonResponse(latest));
  const oldLoad = ensureCardDefinitions();
  notifyCardCatalogInvalidated({ reason: 'card-updated' });
  await ensureCardDefinitions();
  oldRequest.resolve(jsonResponse([{ id: 1 }]));
  await oldLoad;
  expect(getAbilityDataSnapshot().cards).toBe(latest);
});
