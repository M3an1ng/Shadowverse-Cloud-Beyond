jest.mock('../src/components/CardDetailMockup.js', () => ({
  showCardDetailModal: jest.fn(),
}));

import { CardEditor } from '../src/cardEditor.js';

function createJsonResponse(body = {}, headers = {}) {
  return {
    ok: true,
    status: 200,
    statusText: 'OK',
    headers: {
      get: (name) => headers[name] ?? headers[String(name).toLowerCase()] ?? null,
    },
    json: () => Promise.resolve(body),
  };
}

describe('CardEditor ability save cache behavior', () => {
  beforeEach(() => {
    global.alert = jest.fn();
    document.body.innerHTML = '<div id="editor"><div id="abilities-list"></div></div>';
  });

  afterEach(() => {
    delete global.fetch;
    delete global.alert;
  });

  test('saving an ability avoids full card and ability catalog refreshes', async () => {
    const fetchCalls = [];
    global.fetch = jest.fn((url, options = {}) => {
      fetchCalls.push(String(url));
      if (String(url) === '/api/abilities/77' && options.method === 'PUT') {
        return Promise.resolve(createJsonResponse({
          id: 77,
          cardId: 42,
          name: 'Updated Ability',
          description: 'Updated',
        }));
      }

      if (String(url) === '/api/cards/42/abilities') {
        return Promise.resolve(createJsonResponse([
          {
            id: 77,
            cardId: 42,
            name: 'Updated Ability',
            description: 'Updated',
          },
        ]));
      }

      return Promise.reject(new Error(`Unexpected fetch: ${url}`));
    });

    const editor = new CardEditor({ containerId: 'editor' });
    editor.container = document.getElementById('editor');
    editor.currentCard = { id: 42, name: 'Card' };
    editor.getCurrentCardId = () => 42;
    editor.updateEditorHeader = jest.fn();
    editor.applyCardEditMetadata = jest.fn();
    editor.applyDataRevisionFromResponse = jest.fn();

    await editor.saveAbilityChanges({ id: 77, cardId: 42 }, {
      name: 'Updated Ability',
      description: 'Updated',
    });

    expect(fetchCalls).toContain('/api/abilities/77');
    expect(fetchCalls).toContain('/api/cards/42/abilities');
    expect(fetchCalls).not.toContain('/api/cards');
    expect(fetchCalls).not.toContain('/api/abilities');
  });
});
