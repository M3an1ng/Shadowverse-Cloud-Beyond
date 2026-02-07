import { mapDslAstDtoToUiAst } from "../components/dsl_editor/ast-mapping.js";
import { createValueNode } from "../components/dsl_editor/core.js";

function createEditorStub() {
    return {
        metadata: {
            events: [
                {
                    eventType: "Damage",
                    displayName: "Damage",
                    parameters: [
                        {
                            propertyName: "Damage",
                            displayName: "Damage",
                            typeName: "Int32",
                            scope: "input"
                        }
                    ]
                }
            ]
        },
        triggeringEventContext: {
            eventTypes: ["Damage"],
            timing: "On"
        },
        getDslContextObject: (name) => {
            if (String(name).toLowerCase() === "pool") {
                return { typeName: "List<CardInstance>", displayName: "pool" };
            }
            return null;
        },
        normalizeTypeName: (typeName) => typeName,
        createEncapsulatedFrom: (ast) => {
            const originalExpression = ast?.toExpression ? ast.toExpression() : "";
            return createValueNode(`(${originalExpression})`, ast?.returnType || "Object", {
                type: "encapsulated",
                innerAST: ast,
                displayValue: `(${originalExpression})`,
                originalExpression
            });
        }
    };
}

describe("DSL AST mapping", () => {
    test("maps eventInt literal to triggeringEvent property chain", () => {
        const editor = createEditorStub();
        const dto = {
            nodeType: "FunctionCall",
            name: "eventInt",
            arguments: [
                { nodeType: "Literal", dataType: "string", value: "Damage" }
            ]
        };

        const node = mapDslAstDtoToUiAst(editor, dto);

        expect(node).toBeTruthy();
        expect(node.type).toBe("value");
        expect(node.value).toBe("context.triggeringEvent.Damage");
        expect(node.returnType).toBe("Int32");
        expect(node.metadata.type).toBe("property");
        expect(node.metadata.fullPath).toBe("context.triggeringEvent.Damage");
    });

    test("maps ParenthesizedExpression to encapsulated node", () => {
        const editor = createEditorStub();
        const dto = {
            nodeType: "ParenthesizedExpression",
            inner: { nodeType: "Literal", dataType: "number", value: 1 }
        };

        const node = mapDslAstDtoToUiAst(editor, dto);

        expect(node).toBeTruthy();
        expect(node.type).toBe("value");
        expect(node.metadata.type).toBe("encapsulated");
        expect(node.metadata.innerAST).toBeTruthy();
    });

    test("maps distinct method call to listDistinct node", () => {
        const editor = createEditorStub();
        const dto = {
            nodeType: "FunctionCall",
            name: "distinct",
            isMethodCall: true,
            receiver: { nodeType: "Identifier", name: "pool" },
            arguments: [{ nodeType: "Literal", dataType: "string", value: "CardId" }]
        };

        const node = mapDslAstDtoToUiAst(editor, dto);

        expect(node).toBeTruthy();
        expect(node.type).toBe("listDistinct");
        expect(node.source).toBeTruthy();
        expect(node.keyExpression).toBeTruthy();
        expect(node.keyExpression.metadata.type).toBe("encapsulated");
        expect(node.keyExpression.metadata.innerAST.returnType).toBe("String");
    });
});
