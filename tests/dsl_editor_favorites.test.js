import { createValueNode } from '../src/components/dsl_editor/core.js';
import { DslEditor } from '../src/components/DslEditor.js';

describe('DslEditor favorites', () => {
  afterEach(() => {
    if (global.fetch && global.fetch.mockClear) {
      global.fetch.mockClear();
    }
    delete global.fetch;
    delete window.prompt;
  });

  test('refreshFavorites loads items for current user', async () => {
    global.fetch = jest.fn(() =>
      Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve({
            items: [
              { id: 'fav-1', label: '常用表达式', expression: '5', usageCount: 1 }
            ],
            total: 1
          })
      })
    );

    const editor = new DslEditor();
    editor.userId = 'user-1';
    await editor.refreshFavorites('');

    expect(editor.favoriteState.items.length).toBe(1);
    expect(editor.favoriteState.items[0].label).toBe('常用表达式');
  });

  test('createFavoriteFromCurrentExpression posts favorite and marks used', async () => {
    const calls = [];
    global.fetch = jest.fn((url, options = {}) => {
      calls.push({ url, options });
      if (url === '/api/dsl-favorites' && options.method === 'POST') {
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve({ id: 'fav-2' })
        });
      }
      if (String(url).startsWith('/api/dsl-favorites/fav-2/use')) {
        return Promise.resolve({ ok: true, json: () => Promise.resolve({}) });
      }
      if (String(url).startsWith('/api/dsl-favorites?')) {
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve({ items: [], total: 0 })
        });
      }
      return Promise.resolve({ ok: false, json: () => Promise.resolve({}) });
    });

    window.prompt = jest.fn(() => '测试收藏');

    const editor = new DslEditor();
    editor.userId = 'user-2';
    editor.expectedType = 'Int32';
    editor.astRoot = createValueNode('5', 'Int32', { type: 'staticValue', staticValue: '5' });

    await editor.createFavoriteFromCurrentExpression();

    expect(calls.some(call => call.url === '/api/dsl-favorites')).toBe(true);
    expect(calls.some(call => String(call.url).includes('/use'))).toBe(true);
  });
});
