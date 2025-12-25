import { TriggerConfigEditor } from '../src/components/TriggerConfigEditor.js';

describe('TriggerConfigEditor (category grouping + system toggle)', () => {
  test('system events are hidden by default and shown after toggle', async () => {
    const editor = new TriggerConfigEditor();
    editor.metadata = {
      events: [
        {
          eventType: 'Damage',
          displayName: '造成伤害',
          description: '',
          categoryId: 'base-action',
          categoryLabel: '基础行动',
          visibility: 'visible',
          parameters: [],
        },
        {
          eventType: 'PlayCardRequested',
          displayName: '出牌请求',
          description: '',
          categoryId: 'system-request',
          categoryLabel: '系统请求',
          visibility: 'system',
          parameters: [],
        },
      ],
      effects: [],
      dsl: { contextObjects: [], functions: [] },
      queryOperations: [],
    };
    editor.isInitialized = true;

    const container = document.createElement('div');
    document.body.appendChild(container);

    await editor.render(container, { eventTypes: [] }, () => {});

    expect(container.textContent).toContain('造成伤害');
    expect(container.textContent).not.toContain('出牌请求');

    const toggle = container.querySelector('.event-system-toggle input[type="checkbox"]');
    toggle.checked = true;
    toggle.dispatchEvent(new Event('change'));

    expect(container.textContent).toContain('出牌请求');
  });
});
