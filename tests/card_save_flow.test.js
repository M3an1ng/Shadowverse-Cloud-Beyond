/**
 * 集成流测试：前端保存卡牌 -> 后端写库 -> 再次读取验证
 *
 * 依赖：
 * - 后端服务已在本地运行（默认 http://localhost:5000），或通过环境变量 API_BASE 指定。
 * - 新增的卡牌保存接口：POST /api/cards、PUT /api/cards/{id}、GET /api/cards/{id}。
 *
 * 运行方式（示例）：
 *   API_BASE=http://localhost:5000 npm test -- card_save_flow.test.js
 */

const BASE_URL = process.env.API_BASE || '';
const shouldRun = !!BASE_URL;

/**
 * 帮助方法：包装 fetch 并抛出详细错误，便于定位断线/跨域问题
 */
async function request(path, options = {}) {
  const res = await fetch(`${BASE_URL}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
    ...options,
  });
  let body = null;
  try {
    body = await res.json();
  } catch (_) {
    // ignore json parse error
  }
  if (!res.ok) {
    const msg = body?.error || res.statusText;
    throw new Error(`HTTP ${res.status}: ${msg}`);
  }
  return body;
}

const suite = shouldRun ? describe : describe.skip;

suite('Card save flow (frontend -> backend -> DB)', () => {
  jest.setTimeout(20000);

  beforeAll(async () => {
    if (!global.fetch) {
      throw new Error('This test suite requires global.fetch. Set API_BASE and run under a Node version with fetch support.');
    }
  });

  test('should create, update, and read back a card successfully', async () => {
    const unique = Date.now();
    const createPayload = {
      name: `E2E Test Card ${unique}`,
      type: 'Follower',
      rarity: 'Bronze',
      class: 'Neutral',
      card_set: 'Basic',
      cost: 2,
      attack: 1,
      health: 1,
      description: 'Created via automated test',
      abilityIds: [],
    };

    // 1) 创建
    const created = await request('/api/cards', {
      method: 'POST',
      body: JSON.stringify(createPayload),
    });
    expect(created).toBeTruthy();
    expect(created.cardId || created.id).toBeDefined();
    const cardId = created.cardId || created.id;

    // 2) 更新（修改描述/费用）
    const updatePayload = {
      ...createPayload,
      id: cardId,
      description: 'Updated via automated test',
      cost: 3,
    };
    const updated = await request(`/api/cards/${cardId}`, {
      method: 'PUT',
      body: JSON.stringify(updatePayload),
    });
    expect(updated).toBeTruthy();
    expect((updated.cardId || updated.id)).toEqual(cardId);

    // 3) 读取并校验字段
    const fetched = await request(`/api/cards/${cardId}`, { method: 'GET' });
    expect(fetched.name).toEqual(updatePayload.name);
    expect(fetched.description).toEqual(updatePayload.description);
    expect(fetched.cost).toEqual(updatePayload.cost);
    expect(fetched.attack).toEqual(updatePayload.attack);
    expect(fetched.health).toEqual(updatePayload.health);
  });
});
