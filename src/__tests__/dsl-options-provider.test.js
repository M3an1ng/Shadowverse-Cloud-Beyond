import { getDataSourceOptions, getOptionsForCompleteNode } from "../components/dsl_editor/options-provider.js";

function createEditorStub() {
    return {
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
});
