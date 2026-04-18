import { EffectConfigEditor } from '../src/components/EffectConfigEditor.js';
import { AbilityEditor } from '../src/components/AbilityEditor.js';

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

describe('EffectConfigEditor (controlType rendering)', () => {
  beforeEach(() => {
    global.fetch = jest.fn((url) => {
      if (String(url).startsWith('/api/localization/enums/')) {
        const enumName = String(url).split('/').pop();
        const payload = enumName === 'CardKeyword'
          ? {
              None: '无',
              Storm: '疾走',
            }
          : {
              Default: '默认',
              Ignore: '忽略',
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
    const select = await waitFor(() =>
      Array.from(container.querySelectorAll('select.form-select'))
        .find((candidate) => candidate.querySelector('option[value="Storm"]'))
    );

    expect(select).toBeTruthy();
    expect(select.querySelectorAll('option').length).toBeGreaterThan(1);
  });
});

describe('EffectConfigEditor (decision trigger inheritance)', () => {
  test('synchronizeDecisionBranchTriggers overwrites stale branch trigger fields and preserves branch-only settings', () => {
    const editor = new EffectConfigEditor();
    const parentTrigger = {
      eventTypes: ['Summon'],
      relationType: 'Ally',
      timing: 'Before',
      validZones: ['Field'],
      revalidateZoneBeforeExecution: true,
    };
    const effects = [
      {
        type: 'Choose',
        parameters: {
          abilityConfigs: [
            {
              name: 'Branch A',
              trigger: {
                eventTypes: ['Destroy'],
                relationType: 'Enemy',
                timing: 'On',
                validZones: ['Hand'],
                revalidateZoneBeforeExecution: false,
                countLimit: { maxTriggers: 1 },
              },
            },
          ],
        },
      },
    ];

    const [normalized] = editor.synchronizeDecisionBranchTriggers(effects, parentTrigger);
    const [branch] = normalized.parameters.abilityConfigs;

    expect(branch.trigger.eventTypes).toEqual(['Summon']);
    expect(branch.trigger.relationType).toBe('Ally');
    expect(branch.trigger.timing).toBe('Before');
    expect(branch.trigger.validZones).toEqual(['Field']);
    expect(branch.trigger.revalidateZoneBeforeExecution).toBe(true);
    expect(branch.trigger.countLimit).toEqual({ maxTriggers: 1 });
  });

  test('buildEffectsForSave normalizes stale decision branch triggers before serialization', () => {
    const editor = new EffectConfigEditor();
    editor.options = {
      trigger: {
        eventTypes: ['CardMoved'],
        relationType: 'Any',
        timing: 'On',
        validZones: ['Deck'],
        revalidateZoneBeforeExecution: false,
      },
    };

    const [savedEffect] = editor.buildEffectsForSave([
      {
        type: 'Random',
        parameters: {
          abilityConfigs: [
            {
              name: 'Random Branch',
              trigger: {
                eventTypes: ['Destroy'],
                relationType: 'Enemy',
                timing: 'After',
                validZones: ['Hand'],
              },
            },
          ],
        },
      },
    ], { effects: [] });

    expect(savedEffect.parameters.abilityConfigs[0].trigger.eventTypes).toEqual(['CardMoved']);
    expect(savedEffect.parameters.abilityConfigs[0].trigger.validZones).toEqual(['Deck']);
  });
});

describe('AbilityEditor (decision trigger inheritance)', () => {
  test('buildFinalConfig uses the latest parent trigger for decision branches even if branches were saved with stale triggers', () => {
    const editor = new AbilityEditor();
    editor.metadata = { effects: [] };
    editor.ability = {
      name: 'Test Ability',
      description: '',
      abilityKeywords: [],
      abilityType: 1,
      priority: 0,
      isActive: true,
      broadcastOnTrigger: true,
      buffDisplay: true,
      type: 'Triggered',
      trigger: {
        eventTypes: ['Destroy'],
        relationType: 'Any',
        timing: 'On',
        validZones: ['Field'],
        revalidateZoneBeforeExecution: false,
      },
      conditions: [],
      targets: [],
      effects: [
        {
          type: 'Sequence',
          parameters: {
            abilityConfigs: [
              {
                name: 'Sequence Branch',
                trigger: {
                  eventTypes: ['Destroy'],
                  relationType: 'Enemy',
                  timing: 'After',
                  validZones: ['Hand'],
                },
              },
            ],
          },
        },
      ],
      auras: [],
      counterRegistrations: [],
      playerCounterRegistrations: [],
    };

    editor.updateAbility({
      trigger: {
        eventTypes: ['Summon'],
        relationType: 'Ally',
        timing: 'Before',
        validZones: ['Deck'],
        revalidateZoneBeforeExecution: true,
      },
    });

    const finalConfig = editor.buildFinalConfig();
    const savedConfig = JSON.parse(finalConfig.jsonConfig);
    const savedBranchTrigger = savedConfig.effects[0].parameters.abilityConfigs[0].trigger;

    expect(savedBranchTrigger.eventTypes).toEqual(['Summon']);
    expect(savedBranchTrigger.relationType).toBe('Ally');
    expect(savedBranchTrigger.timing).toBe('Before');
    expect(savedBranchTrigger.validZones).toEqual(['Deck']);
    expect(savedBranchTrigger.revalidateZoneBeforeExecution).toBe(true);
  });
});
