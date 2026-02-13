import { getDataSourceOptions, getOptionsForCompleteNode } from "../components/dsl_editor/options-provider.js";
import { getOptionsForIncompleteNode } from "../components/dsl_editor/options-provider.js";

function createEditorStub(overrides = {}) {
    const base = {
        metadata: {
            events: [
                {
                    eventType: "Summon",
                    displayName: "Summon",
                    parameters: [
                        {
                            propertyName: "Damage",
                            displayName: "Damage",
                            typeName: "Int32",
                            scope: "input"
                        }
                    ]
                }
            ],
            dsl: {
                functions: [],
                contextObjects: [
                    {
                        name: "context",
                        displayName: "context",
                        description: "game context",
                        typeName: "GameContext"
                    }
                ]
            }
        },
        triggeringEventContext: {
            eventTypes: ["Summon"],
            timing: "On"
        },
        normalizeTypeName: (typeName) => typeName,
        getDisplayTypeName: (typeName) => typeName,
        findLastNodeInExpression: (node) => node,
        getNodeActualReturnType: (node) => node.returnType,
        isNumericType: () => false,
        isTextType: () => false,
        isCollectionType: () => false,
        isObjectType: () => true,
        isBusinessObjectType: () => false,
        getObjectProperties: () => [],
        getTextOperators: () => [],
        getTextFunctions: () => [],
        getCollectionFunctions: () => [],
        getSmartOperators: () => []
    };
    return { ...base, ...overrides };
}

describe("DSL options provider", () => {
    test("does not expose event fields in global data source options", () => {
        const editor = createEditorStub();
        const options = getDataSourceOptions(editor);
        const hasEventProperty = options.some(option => option.type === "property" && option.value === "Damage");
        expect(hasEventProperty).toBe(false);
    });

    test("exposes event properties only from context.triggeringEvent", () => {
        const editor = createEditorStub();
        const node = {
            type: "value",
            value: "context.triggeringEvent",
            returnType: "IGameEvent",
            metadata: { fullPath: "context.triggeringEvent" }
        };
        const options = getOptionsForCompleteNode(editor, node);
        const eventProperty = options.find(option => option.type === "property" && option.value === "Damage");
        expect(eventProperty).toBeTruthy();
        expect(eventProperty.returnType).toBe("Int32");
    });

    test("does not expose event fields for other context nodes", () => {
        const editor = createEditorStub();
        const node = {
            type: "value",
            value: "context",
            returnType: "GameContext",
            metadata: { type: "context" }
        };
        const options = getOptionsForCompleteNode(editor, node);
        const hasEventProperty = options.some(option => option.type === "property" && option.value === "Damage");
        expect(hasEventProperty).toBe(false);
    });

    test("keeps filter option for sibling branch when another branch already has query", () => {
        const rightNode = {
            type: "value",
            value: "context.opponent.field",
            returnType: "List<CardInstance>",
            metadata: { type: "property", fullPath: "context.opponent.field" }
        };
        const editor = createEditorStub({
            isCollectionType: (typeName) => typeName === "List<CardInstance>",
            workspaceAST: {
                type: "operator",
                operator: "|",
                leftOperand: {
                    type: "listQuery",
                    returnType: "List<CardInstance>",
                    source: {
                        type: "value",
                        value: "context.owner.hand",
                        returnType: "List<CardInstance>",
                        metadata: { type: "property", fullPath: "context.owner.hand" }
                    },
                    filterExpression: null
                },
                rightOperand: rightNode
            }
        });

        const options = getOptionsForCompleteNode(editor, rightNode);
        expect(options.some(option => option.type === "listPipeline")).toBe(true);
    });

    test("keeps filter panel option when current scope is already a query pipeline", () => {
        const queryNode = {
            type: "listQuery",
            returnType: "List<CardInstance>",
            source: {
                type: "value",
                value: "context.owner.hand",
                returnType: "List<CardInstance>",
                metadata: { type: "property", fullPath: "context.owner.hand" }
            },
            filterExpression: null
        };
        const editor = createEditorStub({
            isCollectionType: (typeName) => typeName === "List<CardInstance>",
            workspaceAST: queryNode
        });

        const options = getOptionsForCompleteNode(editor, queryNode);
        expect(options.some(option => option.type === "listPipeline")).toBe(true);
    });

    test("complete operator expression options are based on root node type", () => {
        const rootNode = {
            type: "operator",
            operator: "==",
            returnType: "Boolean",
            leftOperand: {
                type: "value",
                value: "context.owner.field",
                returnType: "List<CardInstance>",
                metadata: { type: "property", fullPath: "context.owner.field" }
            },
            rightOperand: {
                type: "value",
                value: "1",
                returnType: "Int32",
                metadata: { type: "staticValue" }
            }
        };
        const editor = createEditorStub({
            isCollectionType: (typeName) => typeName === "List<CardInstance>"
        });

        const options = getOptionsForCompleteNode(editor, rootNode);
        expect(options.some(option => option.type === "listPipeline")).toBe(false);
    });

    test("business object node exposes set operators so entity can initiate set ops", () => {
        const editor = createEditorStub({
            isBusinessObjectType: (typeName) => typeName === "CardInstance",
            isObjectType: (typeName) => typeName !== "CardInstance"
        });
        const node = {
            type: "value",
            value: "context.targetCard",
            returnType: "CardInstance",
            metadata: { type: "property", fullPath: "context.targetCard" }
        };

        const options = getOptionsForCompleteNode(editor, node);
        const setOps = options
            .filter(option => option.type === "operator")
            .map(option => option.value);

        expect(setOps).toContain("|");
        expect(setOps).toContain("&");
        expect(setOps).toContain("~");
    });

    test("list query self context gets eventType from byType literal argument", () => {
        const editor = createEditorStub({
            getCollectionInnerType: (typeName) => typeName === "List<IGameEvent>" ? "IGameEvent" : null,
            getSemanticNodeType: (node) => node?.returnType || null,
            normalizeTypeName: (typeName) => typeName
        });
        const byTypeNode = {
            type: "functionCall",
            functionName: "byType",
            returnType: "List<IGameEvent>",
            args: [
                {
                    type: "value",
                    value: "context.eventHistory.events",
                    returnType: "List<IGameEvent>",
                    metadata: { type: "property", fullPath: "context.eventHistory.events" }
                },
                {
                    type: "value",
                    value: "\"Summon\"",
                    returnType: "EventType",
                    metadata: { type: "enumValue", enumValue: "Summon" }
                }
            ]
        };
        const queryNode = {
            type: "listQuery",
            source: byTypeNode,
            filterExpression: null
        };

        getOptionsForIncompleteNode(editor, queryNode);

        expect(editor.contextOverride).toBeTruthy();
        expect(editor.contextOverride.name).toBe("self");
        expect(editor.contextOverride.typeName).toBe("IGameEvent");
        expect(editor.contextOverride.eventType).toBe("Summon");
    });

    test("list query self context falls back when byType argument is non-deterministic", () => {
        const editor = createEditorStub({
            getCollectionInnerType: (typeName) => typeName === "List<IGameEvent>" ? "IGameEvent" : null,
            getSemanticNodeType: (node) => node?.returnType || null,
            normalizeTypeName: (typeName) => typeName
        });
        const byTypeNode = {
            type: "functionCall",
            functionName: "byType",
            returnType: "List<IGameEvent>",
            args: [
                {
                    type: "value",
                    value: "context.eventHistory.events",
                    returnType: "List<IGameEvent>",
                    metadata: { type: "property", fullPath: "context.eventHistory.events" }
                },
                {
                    type: "value",
                    value: "context.triggeringEvent.eventType",
                    returnType: "EventType",
                    metadata: { type: "property", fullPath: "context.triggeringEvent.eventType" }
                }
            ]
        };
        const queryNode = {
            type: "listQuery",
            source: byTypeNode,
            filterExpression: null
        };

        getOptionsForIncompleteNode(editor, queryNode);

        expect(editor.contextOverride).toBeTruthy();
        expect(editor.contextOverride.name).toBe("self");
        expect(editor.contextOverride.typeName).toBe("IGameEvent");
        expect(editor.contextOverride.eventType).toBeNull();
    });
});
