import { mapDslAstDtoToUiAst } from "../components/dsl_editor/ast-mapping.js";
import { refreshValueNodeReturnTypes } from "../components/dsl_editor/ast-normalize.js";
import { createValueNode } from "../components/dsl_editor/core.js";
import { resolvePropertyChain } from "../components/dsl_editor/property-chain-resolver.js";

function createEditorStub(overrides = {}) {
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

    const base = {
        metadata,
        triggeringEventContext: {
            eventTypes: ["Damage"],
            timing: "On",
            outputVariables: [{ name: "damageAmount", typeName: "Int32" }]
        },
        normalizeTypeName: (typeName) => typeName,
        getDslContextObject(name) {
            const normalized = String(name || "").toLowerCase();
            if (this.contextOverride && String(this.contextOverride.name || "").toLowerCase() === normalized) {
                return {
                    name: this.contextOverride.name,
                    displayName: this.contextOverride.displayName,
                    typeName: this.contextOverride.typeName
                };
            }
            const contextObjects = this.metadata?.dsl?.contextObjects || [];
            return contextObjects.find(obj => String(obj.name || "").toLowerCase() === normalized) || null;
        },
        getTypeMetadata(typeName) {
            return this.metadata?.dsl?.typeMetadata?.[typeName] || null;
        },
        isEnumType: (typeName) => typeName === "EventReasonSource",
        getEnumValues: (typeName) => typeName === "EventReasonSource" ? ["System", "Ability"] : [],
        isDictionaryType: (typeName) => String(typeName || "").startsWith("Dictionary<"),
        getDictionaryValueType: (typeName) => {
            const match = String(typeName || "").match(/^Dictionary<[^,]+,\s*(.+)>$/);
            return match ? match[1].trim() : "Object";
        }
    };

    return { ...base, ...overrides };
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

    test("byType scoped self event parameter keeps concrete type for nested business object property", () => {
        const editor = createEditorStub({
            metadata: {
                events: [
                    {
                        eventType: "Destroy",
                        displayName: "Destroy",
                        parameters: [
                            { propertyName: "PlayedCard", displayName: "PlayedCard", typeName: "CardInstance" }
                        ]
                    }
                ],
                dsl: {
                    contextObjects: [
                        { name: "context", displayName: "context", typeName: "GameContext" }
                    ],
                    typeMetadata: {
                        GameContext: {
                            properties: [
                                { name: "eventHistory", displayName: "eventHistory", typeName: "EventHistoryReader" }
                            ]
                        },
                        EventHistoryReader: {
                            properties: [
                                { name: "events", displayName: "events", typeName: "List<IGameEvent>" }
                            ]
                        },
                        CardInstance: {
                            properties: [
                                { name: "currentAttack", displayName: "currentAttack", typeName: "Int32" }
                            ]
                        }
                    }
                }
            },
            normalizeTypeName: (typeName) => typeName,
            getCollectionInnerType: (typeName) => typeName === "List<IGameEvent>" ? "IGameEvent" : null,
            getSemanticNodeType: (node) => node?.returnType || null,
            getDslContextObject(name) {
                const normalized = String(name || "").toLowerCase();
                if (this.contextOverride && String(this.contextOverride.name || "").toLowerCase() === normalized) {
                    return {
                        name: this.contextOverride.name,
                        displayName: this.contextOverride.displayName,
                        typeName: this.contextOverride.typeName
                    };
                }
                const contextObjects = this.metadata?.dsl?.contextObjects || [];
                return contextObjects.find(obj => String(obj.name || "").toLowerCase() === normalized) || null;
            }
        });

        const queryNode = {
            type: "listQuery",
            source: {
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
                        value: "\"Destroy\"",
                        returnType: "EventType",
                        metadata: { type: "enumValue", enumValue: "Destroy" }
                    }
                ]
            },
            filterExpression: createValueNode("self.playedCard.currentAttack", "Object", {
                type: "property",
                fullPath: "self.playedCard.currentAttack"
            })
        };

        refreshValueNodeReturnTypes(editor, queryNode);
        expect(queryNode.filterExpression.returnType).toBe("Int32");
    });

    test("refresh keeps existing concrete type when fresh inference falls back to Object", () => {
        const editor = createEditorStub();
        const rawNode = createValueNode("context.unknownProperty", "CardInstance", {
            type: "property",
            fullPath: "context.unknownProperty"
        });

        refreshValueNodeReturnTypes(editor, rawNode);
        expect(rawNode.returnType).toBe("CardInstance");
    });
});
