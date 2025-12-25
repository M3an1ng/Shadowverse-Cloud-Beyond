import { DslEditor } from '../src/components/DslEditor.js';

describe('DslEditor reverse parse integration', () => {
  afterEach(() => {
    if (global.fetch && global.fetch.mockClear) {
      global.fetch.mockClear();
    }
    delete global.fetch;
  });

  test('loadExpressionWithParseApi maps parse result into AST', async () => {
    global.fetch = jest.fn(() =>
      Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve({
            success: true,
            root: { nodeType: 'Literal', dataType: 'number', value: 5 }
          })
      })
    );

    const editor = new DslEditor();
    editor.metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
    editor.isInitialized = true;

    await editor.loadExpressionWithParseApi('5');

    expect(editor.parseStatus.state).toBe('parsed');
    expect(editor.virtualRoot.main).toBeTruthy();
    const inner = editor.virtualRoot.main.metadata.innerAST;
    expect(inner.metadata.type).toBe('staticValue');
    expect(inner.value).toBe('5');
  });

  test('loadExpressionWithParseApi falls back to rawText on parse failure', async () => {
    global.fetch = jest.fn(() =>
      Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve({
            success: false,
            errors: ['parse failed']
          })
      })
    );

    const editor = new DslEditor();
    editor.metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
    editor.isInitialized = true;

    await editor.loadExpressionWithParseApi('bad(');

    expect(editor.parseStatus.state).toBe('fallback');
    const inner = editor.virtualRoot.main.metadata.innerAST;
    expect(inner.metadata.type).toBe('rawText');
    expect(inner.value).toContain('bad(');
  });
});
