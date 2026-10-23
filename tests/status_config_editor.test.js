import { StatusConfigEditor } from '../src/components/StatusConfigEditor.js';

async function waitFor(predicate, attempts = 20) {
  for (let i = 0; i < attempts; i += 1) {
    const result = predicate();
    if (result) {
      return result;
    }
    await new Promise(resolve => setTimeout(resolve, 0));
  }
  return null;
}

describe('StatusConfigEditor (conditional removal visibility)', () => {
  beforeEach(() => {
    global.fetch = jest.fn((url) => {
      if (String(url).startsWith('/api/localization/enums/')) {
        const enumName = String(url).split('/').pop();
        const payload = enumName === 'DurationType'
          ? {
              Permanent: '永久',
              ConditionalRemoval: '条件移除',
            }
          : {
              Default: '默认',
            };

        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve(payload),
        });
      }

      if (url === '/api/AbilityEditorMetadata') {
        return Promise.resolve({
          ok: true,
          json: () =>
            Promise.resolve({
              effects: [],
              events: [],
              dsl: { contextObjects: [], functions: [], typeMetadata: {} },
              queryOperations: []
            }),
        });
      }

      return Promise.reject(new Error(`Unexpected fetch: ${url}`));
    });
  });

  afterEach(() => {
    global.fetch.mockClear();
    delete global.fetch;
  });

  test('shows remove condition and hides duration parameter for conditional removal buffs', async () => {
    const editor = new StatusConfigEditor();
    const container = document.createElement('div');
    document.body.appendChild(container);

    await editor.render(container, [
      {
        durationType: 'ConditionalRemoval',
        durationParameter: 3,
        removeCondition: { expression: 'test.remove', isEnabled: true },
        canStack: false,
        statModifiers: [{ stat: 'Attack', layer: 'Additive', value: 1 }],
      },
    ], () => {});

    const removeConditionLabel = await waitFor(() =>
      Array.from(container.querySelectorAll('label, .parameter-label'))
        .find((node) => node.textContent?.includes('移除条件'))
    );

    expect(removeConditionLabel).toBeTruthy();
    expect(container.textContent).not.toContain('持续参数');
  });
});
