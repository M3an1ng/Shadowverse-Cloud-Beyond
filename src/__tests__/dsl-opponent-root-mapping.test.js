import { mapDslAstDtoToUiAst } from "../components/dsl_editor/ast-mapping.js";

function createEditorStub() {
    const metadata = {
        dsl: {
            contextObjects: [
                {
                    name: "context",
                    displayName: "context",
                    description: "game context",
                    typeName: "GameContext"
                }
            ],
            typeMetadata: {
                GameContext: {
                    properties: [
                        { name: "opponentPlayer", displayName: "Opponent", typeName: "PlayerState" }
                    ]
                },
                PlayerState: {
                    properties: [
                        { name: "field", displayName: "Field", typeName: "List<CardInstance>" }
                    ]
                }
            }
        }
    };

    return {
        metadata,
        normalizeTypeName: (typeName) => typeName,
        getDslContextObject: (name) =>
            metadata.dsl.contextObjects.find(obj => obj.name === name) || null,
        getTypeMetadata: (typeName) => metadata.dsl.typeMetadata[typeName] || null
    };
}

describe("DSL AST mapping - opponent root", () => {
    test("opponent.field maps to Object when opponent is not a declared context root", () => {
        const editor = createEditorStub();
        const dto = {
            nodeType: "MemberAccess",
            target: { nodeType: "Identifier", name: "opponent" },
            member: { nodeType: "Identifier", name: "field" }
        };

        const node = mapDslAstDtoToUiAst(editor, dto);

        expect(node).toBeTruthy();
        expect(node.value).toBe("opponent.field");
        expect(node.returnType).toBe("Object");
    });
});
