import { EffectConfigEditor } from '../src/components/EffectConfigEditor.js';

describe('EffectConfigEditor (controlType rendering)', () => {
  beforeEach(() => {
    global.fetch = jest.fn(() =>
      Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve({
            None: '无',
            Storm: '疾走',
          }),
      })
    );
  });

  afterEach(() => {
    global.fetch.mockClear();
    delete global.fetch;
  });

  test('Dropdown with EnumName loads enum options', async () => {
    const editor = new EffectConfigEditor();
    editor.metadata = {
      effects: [
        {
          effectTypeName: 'AddKeyword',
          displayName: '添加关键字',
          parameters: [
            {
              propertyName: 'Keyword',
              displayName: '关键字',
              description: '',
              controlType: 'Dropdown',
              typeName: 'CardKeyword',
              enumName: 'CardKeyword',
            },
          ],
        },
      ],
    };
    editor.isInitialized = true;

    const container = document.createElement('div');
    document.body.appendChild(container);

    await editor.render(container, [{ type: 'AddKeyword', parameters: { Keyword: 'None' } }], () => {});

    // allow populateEnumDropdown to resolve
    await Promise.resolve();

    const select = container.querySelector('select.form-select');
    expect(select).toBeTruthy();
    expect(select.querySelectorAll('option').length).toBeGreaterThan(1);
  });
});

