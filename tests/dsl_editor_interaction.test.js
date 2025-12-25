import { createOperatorNode, createValueNode } from '../src/components/dsl_editor/core.js';
import { DslEditor } from '../src/components/DslEditor.js';
import { validateDslExpression } from '../src/dslValidation.js';

describe('DslEditor / DSL Validation (minimal)', () => {
    test('handleElevateExpression wraps a value node in an encapsulated node', () => {
        const editor = new DslEditor();
        const container = document.createElement('div');
        document.body.appendChild(container);
        editor.container = container;

        const initialNode = createValueNode('5', 'Int32', { type: 'staticValue' });
        editor.workspaceAST = initialNode;
        editor.astRoot = initialNode;

        editor.handleElevateExpression(editor.workspaceAST);

        const newRoot = editor.workspaceAST;
        expect(newRoot.type).toBe('value');
        expect(newRoot.metadata.type).toBe('encapsulated');
        expect(newRoot.metadata.innerAST.value).toBe(initialNode.value);
        expect(newRoot.toExpression()).toBe('(5)');
    });

    test('validateDslExpression flags empty expression when allowEmpty=false', () => {
        const metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        const res = validateDslExpression(metadata, '', { expectedType: 'Boolean', allowEmpty: false });
        expect(res.issues.some(i => i.severity === 'error')).toBe(true);
    });

    test('validateDslExpression accepts numeric expression when expectedType is numeric', () => {
        const metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        const res = validateDslExpression(metadata, '(5)', { expectedType: 'Int32', allowEmpty: false });
        expect(res.issues.length).toBe(0);
    });

    test('validateDslExpression reports expectedType mismatch as error', () => {
        const metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        const res = validateDslExpression(metadata, '"hello"', { expectedType: 'Int32', allowEmpty: false });
        expect(res.issues.some(i => i.severity === 'error')).toBe(true);
        expect(res.issues.some(i => (i.message || '').includes('类型不匹配'))).toBe(true);
    });

    test('DslEditor.parseExpression does not introduce double parentheses for (5)', () => {
        const editor = new DslEditor();
        editor.metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        editor.isInitialized = true;
        editor.parseExpression('(5)');
        expect(editor.astRoot.toExpression()).toBe('(5)');
    });

    test('DslEditor initial options include context even when expectedType is Boolean', () => {
        const editor = new DslEditor();
        editor.metadata = {
            dsl: {
                typeMetadata: {},
                functions: [],
                contextObjects: [
                    { name: 'self', displayName: '当前项', description: '当前项', typeName: 'CardInstance' }
                ]
            }
        };
        editor.isInitialized = true;
        editor.slotExpectedType = 'Boolean';
        editor.astRoot = createValueNode('', 'Void', { type: 'empty' });

        const opts = editor.getCurrentEditingOptions();
        expect(opts.some(o => o.type === 'context')).toBe(true);
        expect(opts.some(o => o.type === 'staticValue')).toBe(true);
    });

    test('DslEditor exposes TriggeringEvent fields as eventField options without showing internal name in label', () => {
        const editor = new DslEditor();
        editor.metadata = {
            events: [
                {
                    eventType: 'damageDealt',
                    displayName: '造成伤害',
                    parameters: [
                        { propertyName: 'amount', displayName: '伤害量', description: '伤害数值', typeName: 'Int32', scope: 'input' },
                        { propertyName: 'isCritical', displayName: '暴击', description: '是否暴击', typeName: 'Boolean', scope: 'output' }
                    ]
                }
            ],
            dsl: { typeMetadata: {}, contextObjects: [], functions: [] }
        };
        editor.isInitialized = true;
        editor.triggeringEventContext = { eventTypes: ['damageDealt'], timing: 'On' };

        const opts = editor.getTriggeringEventFieldOptions();
        expect(opts.some(o => o.type === 'eventField')).toBe(true);
        expect(opts[0].label.includes('eventInt')).toBe(false);
        // On timing should not include output-only field
        expect(opts.some(o => (o.meta?.propertyName || '') === 'isCritical')).toBe(false);
    });

    test('clicking empty placeholder enters edit mode for the correct slot', () => {
        const editor = new DslEditor();
        const container = document.createElement('div');
        document.body.appendChild(container);
        editor.container = container;

        const left = createValueNode('true', 'Boolean', { type: 'staticValue', staticValue: 'true' });
        const right = createValueNode('', 'Void', { type: 'empty' });
        const opNode = createOperatorNode('==');
        opNode.setLeftOperand(left);
        opNode.setRightOperand(right);

        const placeholderContainer = document.createElement('div');
        editor.renderEmptySlotPlaceholder(opNode, 'right', placeholderContainer);
        const placeholder = placeholderContainer.querySelector('.dsl-placeholder-capsule');
        expect(placeholder).toBeTruthy();

        placeholder.click();

        const top = editor.peekEditingPath();
        expect(top?.slot).toBe('right');
        expect(top?.parentNode).toBe(opNode);
    });

    test('getEncapsulationStyle uses mismatch info to show error badge', () => {
        const editor = new DslEditor();
        const encapsulated = createValueNode('(5)', 'Int32', { type: 'encapsulated' });
        encapsulated._typeMatchInfo = {
            expectedType: 'Boolean',
            actualType: 'Int32',
            isTypeMatch: false,
            needsCorrection: true
        };

        const style = editor.getEncapsulationStyle(encapsulated);
        expect(style.badgeSymbol).toBe('×');
    });
});
