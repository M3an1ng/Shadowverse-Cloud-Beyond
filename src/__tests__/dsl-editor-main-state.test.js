import { DslEditor } from "../components/DslEditor.js";
import { createFunctionCallNode, createValueNode } from "../components/dsl_editor/core.js";

function createEditor() {
    const editor = new DslEditor();
    editor.renderEditor = jest.fn();
    editor.showExpressionError = jest.fn();
    return editor;
}

describe("DslEditor main expression state", () => {
    test("replaceMainExpression synchronizes main root, workspace draft, and editing path", () => {
        const editor = createEditor();
        const rootNode = editor.buildAstFromExpression("1");

        editor.replaceMainExpression(rootNode, {
            expression: "1",
            parseState: "parsed",
            parseMessage: "解析成功"
        });

        expect(editor.virtualRoot.main).toBeTruthy();
        expect(editor.virtualRoot.main.metadata?.innerAST?.toExpression?.()).toBe("1");
        expect(editor.workspaceAST?.toExpression?.()).toBe("1");
        expect(editor.expression).toBe("1");
        expect(editor.parseStatus.state).toBe("parsed");
        expect(editor.peekEditingPath()).toMatchObject({
            parentNode: editor.virtualRoot,
            slot: "main"
        });
        expect(editor.renderEditor).toHaveBeenCalled();
    });

    test("updateExpressionFromAST keeps virtualRoot.main in sync while editing main workspace", () => {
        const editor = createEditor();

        editor.replaceMainExpression(editor.buildAstFromExpression("1"), {
            expression: "1",
            parseState: "parsed",
            parseMessage: "解析成功"
        });

        const nextRoot = editor.buildAstFromExpression("2");
        editor.workspaceAST = nextRoot;
        editor.astRoot = nextRoot;

        editor.updateExpressionFromAST({ skipRender: true });

        expect(editor.expression).toBe("2");
        expect(editor.virtualRoot.main.metadata?.innerAST?.toExpression?.()).toBe("2");
    });

    test("insertFavoriteExpression loads favorite content through parse api instead of local draft parser", async () => {
        const editor = createEditor();

        editor.replaceMainExpression(editor.buildAstFromExpression("1"), {
            expression: "1",
            parseState: "parsed",
            parseMessage: "解析成功"
        });
        editor.editingPath = [{ parentNode: { type: "operator" }, slot: "left" }];
        editor.requestParseAst = jest.fn().mockResolvedValue({
            success: true,
            root: { nodeType: "Literal", dataType: "number", value: 3 }
        });

        const loaded = await editor.insertFavoriteExpression("context.owner.field.query(self.cost > 1)");

        expect(loaded).toBe(true);
        expect(editor.requestParseAst).toHaveBeenCalledWith("context.owner.field.query(self.cost > 1)");
        expect(editor.expression).toBe("3");
        expect(editor.virtualRoot.main.metadata?.innerAST?.toExpression?.()).toBe("3");
        expect(editor.workspaceAST?.toExpression?.()).toBe("3");
        expect(editor.peekEditingPath()).toMatchObject({
            parentNode: editor.virtualRoot,
            slot: "main"
        });
        expect(editor.renderEditor).toHaveBeenCalled();
    });

    test("insertFavoriteExpression falls back to raw text when parse api reports failure", async () => {
        const editor = createEditor();
        editor.requestParseAst = jest.fn().mockResolvedValue({
            success: false,
            message: "解析失败",
            errors: ["bad dsl"]
        });

        const loaded = await editor.insertFavoriteExpression("a + b");

        expect(loaded).toBe(true);
        expect(editor.parseStatus.state).toBe("fallback");
        expect(editor.expression).toBe("a + b");
        expect(editor.virtualRoot.main.metadata?.innerAST?.metadata?.type).toBe("rawText");
    });

    test("extendAST attaches property access to function result instead of rightmost leaf", () => {
        const editor = createEditor();

        const sourceNode = createValueNode("context.owner.field", "List<CardInstance>", {
            type: "property",
            fullPath: "context.owner.field",
            displayName: "战场"
        });
        const countNode = createValueNode("2", "Int32", {
            type: "staticValue",
            staticValue: "2"
        });
        const encapsulatedCount = editor.createEncapsulatedFrom(countNode);
        const findNode = createFunctionCallNode("find", "CardInstance", [
            { name: "source", typeName: "List<CardInstance>", isOptional: false },
            { name: "index", typeName: "Int32", isOptional: false }
        ]);
        findNode.setArg(0, sourceNode);
        findNode.setArg(1, encapsulatedCount);

        editor.replaceMainExpression(findNode, {
            expression: "find(context.owner.field, (2))",
            parseState: "parsed",
            parseMessage: "解析成功"
        });

        const changed = editor.extendAST({
            type: "property",
            value: "cardType",
            label: "卡牌类型",
            returnType: "CardType"
        });

        expect(changed).toBe(true);
        expect(editor.workspaceAST?.toExpression?.()).toBe("find(context.owner.field, (2)).cardType");
        expect(editor.astRoot?.metadata?.isDynamicMemberAccess).toBe(true);
        expect(editor.astRoot?.returnType).toBe("CardType");
    });
});
