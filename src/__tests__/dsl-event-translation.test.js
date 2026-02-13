import { resolveEventTypeHintFromSource } from "../components/dsl_editor/event-translation.js";

function createEditorStub() {
    return {
        metadata: {
            events: [
                { eventType: "Damage" },
                { eventType: "Destroy" }
            ]
        },
        getEnumValues: () => ["Damage", "Destroy"]
    };
}

describe("dsl event translation", () => {
    test("resolves byType hint from literal argument", () => {
        const editor = createEditorStub();
        const node = {
            type: "functionCall",
            functionName: "byType",
            args: [
                { type: "value", value: "context.eventHistory.events", returnType: "List<IGameEvent>" },
                { type: "value", value: "\"Damage\"", returnType: "EventType", metadata: { type: "staticValue", staticValue: "Damage" } }
            ]
        };

        expect(resolveEventTypeHintFromSource(editor, node)).toBe("Damage");
    });

    test("resolves byType hint through list pipeline wrappers", () => {
        const editor = createEditorStub();
        const byTypeNode = {
            type: "functionCall",
            functionName: "byType",
            args: [
                { type: "value", value: "context.eventHistory.events", returnType: "List<IGameEvent>" },
                { type: "value", value: "\"Destroy\"", returnType: "EventType", metadata: { type: "enumValue", enumValue: "Destroy" } }
            ]
        };
        const queryNode = {
            type: "listQuery",
            source: byTypeNode,
            filterExpression: null
        };
        const selectNode = {
            type: "listSelect",
            source: queryNode,
            selectExpression: null
        };

        expect(resolveEventTypeHintFromSource(editor, selectNode)).toBe("Destroy");
    });

    test("returns null for non-deterministic byType argument", () => {
        const editor = createEditorStub();
        const node = {
            type: "functionCall",
            functionName: "byType",
            args: [
                { type: "value", value: "context.eventHistory.events", returnType: "List<IGameEvent>" },
                { type: "value", value: "context.triggeringEvent.eventType", returnType: "EventType", metadata: { type: "property" } }
            ]
        };

        expect(resolveEventTypeHintFromSource(editor, node)).toBeNull();
    });
});
