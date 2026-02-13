import { createFunctionCallNode, createOperatorNode, createValueNode, getSemanticNodeType } from '../src/components/dsl_editor/core.js';
import { DslEditor } from '../src/components/DslEditor.js';
import { validateDslExpression } from '../src/dslValidation.js';

describe('DslEditor / DSL Validation (minimal)', () => {
    afterEach(() => {
        if (global.fetch && global.fetch.mockClear) {
            global.fetch.mockClear();
        }
        delete global.fetch;
    });

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

    test('validateDslExpression flags empty expression when allowEmpty=false', async () => {
        const metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        const res = await validateDslExpression(metadata, '', { expectedType: 'Boolean', allowEmpty: false });
        expect(res.issues.some(i => i.severity === 'error')).toBe(true);
    });

    test('validateDslExpression accepts numeric expression when expectedType is numeric', async () => {
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
        const metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        const res = await validateDslExpression(metadata, '(5)', { expectedType: 'Int32', allowEmpty: false });
        expect(res.issues.length).toBe(0);
    });

    test('validateDslExpression reports expectedType mismatch as error', async () => {
        global.fetch = jest.fn(() =>
            Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve({
                        success: true,
                        root: { nodeType: 'Literal', dataType: 'string', value: 'hello' }
                    })
            })
        );
        const metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        const res = await validateDslExpression(metadata, '"hello"', { expectedType: 'Int32', allowEmpty: false });
        expect(res.issues.some(i => i.severity === 'error')).toBe(true);
        expect(res.issues.some(i => (i.message || '').includes('类型不匹配'))).toBe(true);
    });

    test('validateDslExpression accepts boolean property without == true', async () => {
        global.fetch = jest.fn(() =>
            Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve({
                        success: true,
                        root: {
                            nodeType: 'Identifier',
                            path: ['context', 'sourceCard', 'isEvolved']
                        }
                    })
            })
        );

        const metadata = {
            dsl: {
                functions: [],
                contextObjects: [
                    {
                        name: 'context',
                        displayName: '游戏上下文',
                        description: 'context',
                        typeName: 'GameContext'
                    }
                ],
                typeMetadata: {
                    GameContext: {
                        typeName: 'GameContext',
                        displayName: '游戏上下文',
                        properties: [
                            {
                                name: 'sourceCard',
                                displayName: '本卡牌',
                                description: '当前效果的持有卡牌',
                                typeName: 'CardInstance'
                            }
                        ]
                    },
                    CardInstance: {
                        typeName: 'CardInstance',
                        displayName: '卡牌实例',
                        properties: [
                            {
                                name: 'isEvolved',
                                displayName: '是否已进化',
                                description: '该单位是否已进化',
                                typeName: 'bool'
                            }
                        ]
                    }
                }
            }
        };

        const res = await validateDslExpression(
            metadata,
            'context.sourceCard.isEvolved',
            { expectedType: 'Boolean', allowEmpty: false }
        );
        expect(res.issues).toHaveLength(0);
    });

    test('validateDslExpression infers select projection type from collection self context', async () => {
        const identifier = (name) => ({ nodeType: 'Identifier', name });
        const member = (target, segment) => ({ nodeType: 'MemberAccess', target, member: segment });
        const receiver = member(
            member(identifier('context'), identifier('triggeringEvent')),
            identifier('ResolvedTargets')
        );
        const projection = member(
            member(
                member(identifier('self'), identifier('card')),
                identifier('currentDefinition')
            ),
            identifier('id')
        );

        global.fetch = jest.fn(() =>
            Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve({
                        success: true,
                        root: {
                            nodeType: 'FunctionCall',
                            name: 'select',
                            isMethodCall: true,
                            receiver,
                            arguments: [
                                {
                                    nodeType: 'ParenthesizedExpression',
                                    inner: projection
                                }
                            ]
                        }
                    })
            })
        );

        const metadata = {
            dsl: {
                functions: [],
                contextObjects: [
                    { name: 'context', displayName: '游戏上下文', typeName: 'GameContext' }
                ],
                typeMetadata: {
                    GameContext: {
                        typeName: 'GameContext',
                        properties: [
                            { name: 'triggeringEvent', displayName: '触发事件', typeName: 'IGameEvent' }
                        ]
                    },
                    GameEventBase: {
                        typeName: 'GameEventBase',
                        properties: [
                            { name: 'ResolvedTargets', displayName: '解析目标', typeName: 'List<TargetInfo>' }
                        ]
                    },
                    TargetInfo: {
                        typeName: 'TargetInfo',
                        properties: [
                            { name: 'card', displayName: '目标卡牌', typeName: 'CardInstance' }
                        ]
                    },
                    CardInstance: {
                        typeName: 'CardInstance',
                        properties: [
                            { name: 'currentDefinition', displayName: '当前定义', typeName: 'CardDefinition' }
                        ]
                    },
                    CardDefinition: {
                        typeName: 'CardDefinition',
                        properties: [
                            { name: 'id', displayName: '定义ID', typeName: 'Int32' }
                        ]
                    }
                }
            }
        };

        const expression = 'context.triggeringEvent.ResolvedTargets.select((self.card.currentDefinition.id))';
        const res = await validateDslExpression(metadata, expression, { expectedType: 'List<Int32>', allowEmpty: false });
        expect(res.issues).toHaveLength(0);
        expect(res.ast?.returnType).toBe('List<Int32>');
    });

    test('DslEditor.parseExpression does not introduce double parentheses for (5)', () => {
        const editor = new DslEditor();
        editor.metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        editor.isInitialized = true;
        editor.parseExpression('(5)');
        expect(editor.astRoot.toExpression()).toBe('5');
    });

    test('DslEditor.getSemanticNodeType infers find return type from source collection', () => {
        const editor = new DslEditor();
        editor.metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        editor.isInitialized = true;

        const findNode = createFunctionCallNode('find', 'Object', [
            { name: 'source', typeName: 'Object', isOptional: false },
            { name: 'index', typeName: 'Int32', isOptional: false }
        ]);
        findNode.setArg(0, createValueNode('context.owner.field', 'List<CardInstance>', {
            type: 'property',
            fullPath: 'context.owner.field'
        }));
        findNode.setArg(1, createValueNode('1', 'Int32', {
            type: 'staticValue',
            staticValue: '1'
        }));

        expect(editor.getSemanticNodeType(findNode)).toBe('CardInstance');
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

    test('set operator treats business object operand as list-compatible expectation', () => {
        const editor = new DslEditor();
        editor.metadata = { dsl: { typeMetadata: {}, contextObjects: [], functions: [] } };
        editor.isInitialized = true;

        const leftEntity = createValueNode('context.targetCard', 'CardInstance', {
            type: 'property',
            fullPath: 'context.targetCard'
        });
        const rightList = createValueNode('context.owner.field', 'List<CardInstance>', {
            type: 'property',
            fullPath: 'context.owner.field'
        });
        const union = createOperatorNode('|');
        union.setLeftOperand(leftEntity);
        union.setRightOperand(rightList);

        const expectedForRight = editor.getExpectedTypeForSlot(union, 'right');
        const match = editor.calculateNodeTypeMatch(rightList);

        expect(expectedForRight).toBe('List<CardInstance>');
        expect(match.expectedType).toBe('List<CardInstance>');
        expect(match.isTypeMatch).toBe(true);
    });

    test('validateDslExpression accepts singleton-singleton set operator and infers collection result', async () => {
        const identifier = (name) => ({ nodeType: 'Identifier', name });
        const member = (target, segment) => ({ nodeType: 'MemberAccess', target, member: segment });

        global.fetch = jest.fn(() =>
            Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve({
                        success: true,
                        root: {
                            nodeType: 'BinaryOperation',
                            operator: 'SetUnion',
                            left: member(member(identifier('context'), identifier('owner')), identifier('leader')),
                            right: member(member(identifier('context'), identifier('opponent')), identifier('leader'))
                        }
                    })
            })
        );

        const metadata = {
            dsl: {
                functions: [],
                contextObjects: [
                    { name: 'context', displayName: '游戏上下文', typeName: 'GameContext' }
                ],
                typeMetadata: {
                    GameContext: {
                        typeName: 'GameContext',
                        properties: [
                            { name: 'owner', displayName: '我方玩家', typeName: 'PlayerState' },
                            { name: 'opponent', displayName: '敌方玩家', typeName: 'PlayerState' }
                        ]
                    },
                    PlayerState: {
                        typeName: 'PlayerState',
                        properties: [
                            { name: 'leader', displayName: '主战者', typeName: 'CardInstance' }
                        ]
                    }
                }
            }
        };

        const res = await validateDslExpression(
            metadata,
            'context.owner.leader | context.opponent.leader',
            { expectedType: 'List<CardInstance>', allowEmpty: false }
        );

        expect(res.issues).toHaveLength(0);
        expect(getSemanticNodeType(res.ast)).toBe('List<CardInstance>');
    });

    test('DslEditor exposes TriggeringEvent fields as property options without showing internal name in label', () => {
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
        expect(opts.some(o => o.type === 'property')).toBe(true);
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

    test('getCollectionFunctions filters numeric aggregates by element type', () => {
        const editor = new DslEditor();
        editor.metadata = {
            dsl: {
                functions: [
                    { name: 'sum', parameters: [{ typeName: 'IEnumerable<Double>' }] },
                    { name: 'max', parameters: [{ typeName: 'IEnumerable<Double>' }] },
                    { name: 'min', parameters: [{ typeName: 'IEnumerable<Double>' }] },
                    { name: 'count', parameters: [{ typeName: 'Object' }] },
                    { name: 'isEmpty', parameters: [{ typeName: 'IEnumerable' }] }
                ]
            }
        };

        const numericListFns = editor.getCollectionFunctions('List<Int32>');
        expect(numericListFns.some(f => f.value === 'sum')).toBe(true);
        expect(numericListFns.some(f => f.value === 'max')).toBe(true);

        const objectListFns = editor.getCollectionFunctions('List<CardInstance>');
        expect(objectListFns.some(f => f.value === 'sum')).toBe(false);
        expect(objectListFns.some(f => f.value === 'max')).toBe(false);
        expect(objectListFns.some(f => f.value === 'count')).toBe(true);
    });

    test('normalizeTypeName canonicalizes bool aliases', () => {
        const editor = new DslEditor();
        expect(editor.normalizeTypeName('bool')).toBe('Boolean');
        expect(editor.normalizeTypeName('boolean')).toBe('Boolean');
        expect(editor.normalizeTypeName('System.Boolean')).toBe('Boolean');
        expect(editor.normalizeTypeName('List<bool>')).toBe('List<Boolean>');
        expect(editor.normalizeTypeName('IEnumerable<boolean>')).toBe('List<Boolean>');
    });

    test('calculateNodeTypeMatch accepts bool alias for boolean expected type', () => {
        const editor = new DslEditor();
        const node = createValueNode('context.sourceCard.isEvolved', 'bool', {
            type: 'property',
            fullPath: 'context.sourceCard.isEvolved'
        });
        const match = editor.calculateNodeTypeMatch(node, 'Boolean');
        expect(match.isTypeMatch).toBe(true);
    });
});
