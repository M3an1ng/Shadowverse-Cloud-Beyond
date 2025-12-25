import { ValueConfigEditor } from '../src/components/ValueConfigEditor.js';

describe('ValueConfigEditor (type-driven)', () => {
  test('StaticValueConfig with parameterType=Boolean renders checkbox and hides data-type selector', () => {
    const editor = new ValueConfigEditor();
    editor.metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
    editor.isInitialized = true;

    const container = document.createElement('div');
    document.body.appendChild(container);

    editor.render(
      container,
      { $type: 'StaticValueConfig', Value: true },
      () => {},
      { parameterType: 'Boolean' }
    );

    expect(container.textContent.includes('数据类型')).toBe(false);
    const checkbox = container.querySelector('input[type="checkbox"]');
    expect(checkbox).toBeTruthy();
  });

  test('StaticValueConfig with parameterType=Int32 renders number input and hides data-type selector', () => {
    const editor = new ValueConfigEditor();
    editor.metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
    editor.isInitialized = true;

    const container = document.createElement('div');
    document.body.appendChild(container);

    editor.render(
      container,
      { $type: 'StaticValueConfig', Value: 3 },
      () => {},
      { parameterType: 'Int32' }
    );

    expect(container.textContent.includes('数据类型')).toBe(false);
    const numberInput = container.querySelector('input[type="number"]');
    expect(numberInput).toBeTruthy();
  });
});

