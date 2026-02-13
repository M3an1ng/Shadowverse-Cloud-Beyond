import { mapDslAstDtoToUiAst } from "../components/dsl_editor/ast-mapping.js";
import { refreshValueNodeReturnTypes } from "../components/dsl_editor/ast-normalize.js";
import { createValueNode } from "../components/dsl_editor/core.js";
import { resolvePropertyChain } from "../components/dsl_editor/property-chain-resolver.js";

function createEditorStub() {
    const metadata = {
        events: [
            {
                eventType: "Damage",
                displayName: "Damage",
                parameters: [
                    { propertyName: "Damage", displayName: "Damage", typeName: "Int32" },
                    { propertyName: "Reason", displayName: "Reason", typeName: "EventReasonSource" }
                ]
            }
        ],
        dsl: {
            contextObjects: [
                {
                    name: "context",
                    displayName: "context",
                    typeName: "GameContext"
                }
            ],
            typeMetadata: {
                GameContext: {
                    properties: [
                        { name: "triggeringEvent", displayName: "triggeringEvent", typeName: "IGameEvent" },
                        { name: "sourceCard", displayName: "sourceCard", typeName: "CardInstance" }
                    ]
                },
                IGameEvent: {
                    properties: [
                        { name: "outputs", displayName: "outputs", typeName: "Dictionary<String,Object>" }
                    ]
                },
                CardInstance: {
                    properties: [
                        { name: "counterMap", displayName: "counterMap", typeName: "Dictionary<String,Int32>" }
                    ]
                }
            }
        }
    };

    return {
        metadata,
        triggeringEventContext: {
            eventTypes: ["Damage"],
            timing: "On",
            outputVariables: [{ name: "damageAmount", typeName: "Int32" }]
        },
        normalizeTypeName: (typeName) => typeName,
        getDslContextObject: (name) =>
            metadata.dsl.contextObjects.find(obj => obj.name === name) || null,
        getTypeMetadata: (typeName) => metadata.dsl.typeMetadata[typeName] || null,
        isEnumType: (typeName) => typeName === "EventReasonSource",
        getEnumValues: (typeName) => typeName === "EventReasonSource" ? ["System", "Ability"] : [],
        isDictionaryType: (typeName) => String(typeName || "").startsWith("Dictionary<"),
        getDictionaryValueType: (typeName) => {
            const match = String(typeName || "").match(/^Dictionary<[^,]+,\s*(.+)>$/);
            return match ? match[1].trim() : "Object";
        }
    };
}

function buildMemberAccessDto(parts) {
    let root = { nodeType: "Identifier", name: parts[0] };
    for (let i = 1; i < parts.length; i++) {
        root = {
            nodeType: "MemberAccess",
            target: root,
            member: { nodeType: "Identifier", name: parts[i] }
        };
    }
    return root;
}

describe("property chain resolver", () => {
    test("mapping and normalize share same type resolution for triggeringEvent.outputs path", () => {
        const editor = createEditorStub();
        const parts = ["context", "triggeringEvent", "outputs", "damageAmount"];
        const dto = buildMemberAccessDto(parts);

        const mappedNode = mapDslAstDtoToUiAst(editor, dto);
        expect(mappedNode).toBeTruthy();
        expect(mappedNode.value).toBe("context.triggeringEvent.outputs.damageAmount");
        expect(mappedNode.returnType).toBe("Int32");

        const rawNode = createValueNode("context.triggeringEvent.outputs.damageAmount", "Object", {
            type: "property",
            fullPath: "context.triggeringEvent.outputs.damageAmount"
        });
        refreshValueNodeReturnTypes(editor, rawNode);
        expect(rawNode.returnType).toBe("Int32");

        const resolved = resolvePropertyChain(editor, parts);
        expect(resolved?.finalType).toBe("Int32");
    });

    test("mapping and normalize share same dictionary value type resolution", () => {
        const editor = createEditorStub();
        const parts = ["context", "sourceCard", "counterMap", "water"];
        const dto = buildMemberAccessDto(parts);

        const mappedNode = mapDslAstDtoToUiAst(editor, dto);
        expect(mappedNode).toBeTruthy();
        expect(mappedNode.value).toBe("context.sourceCard.counterMap.water");
        expect(mappedNode.returnType).toBe("Int32");

        const rawNode = createValueNode("context.sourceCard.counterMap.water", "Object", {
            type: "property",
            fullPath: "context.sourceCard.counterMap.water"
        });
        refreshValueNodeReturnTypes(editor, rawNode);
        expect(rawNode.returnType).toBe("Int32");

        const resolved = resolvePropertyChain(editor, parts);
        expect(resolved?.finalType).toBe("Int32");
    });

    test("triggeringEvent reason resolves to EventReasonSource enum metadata", () => {
        const editor = createEditorStub();
        const parts = ["context", "triggeringEvent", "reason"];

        const resolved = resolvePropertyChain(editor, parts);
        expect(resolved?.finalType).toBe("EventReasonSource");
        expect(resolved?.steps?.[1]?.enumType).toBe("EventReasonSource");
        expect(resolved?.steps?.[1]?.enumValues).toEqual(["System", "Ability"]);
    });
});
