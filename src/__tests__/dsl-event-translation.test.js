import { resolveEventTypeHintFromSource, resolveScopedSelfContextFromSource } from "../components/dsl_editor/event-translation.js";

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

    test("resolves scoped self context with eventType for byType source", () => {
        const editor = {
            ...createEditorStub(),
            normalizeTypeName: (typeName) => typeName,
            getCollectionInnerType: (typeName) => typeName === "List<IGameEvent>" ? "IGameEvent" : null
        };
        const node = {
            type: "functionCall",
            functionName: "byType",
            returnType: "List<IGameEvent>",
            args: [
                { type: "value", value: "context.eventHistory.events", returnType: "List<IGameEvent>" },
                { type: "value", value: "\"Destroy\"", returnType: "EventType", metadata: { type: "enumValue", enumValue: "Destroy" } }
            ]
        };

        const scoped = resolveScopedSelfContextFromSource(editor, node, "List<IGameEvent>");
        expect(scoped).toEqual({
            name: "self",
            displayName: "当前项",
            typeName: "IGameEvent",
            eventType: "Destroy"
        });
    });

    test("returns null scoped self context for non-queryable source", () => {
        const editor = {
            ...createEditorStub(),
            normalizeTypeName: (typeName) => typeName,
            getCollectionInnerType: () => null,
            canOpenPipelineOnType: () => false
        };
        const node = { type: "value", value: "context.turn", returnType: "Int32" };
        expect(resolveScopedSelfContextFromSource(editor, node, "Int32")).toBeNull();
    });
});
