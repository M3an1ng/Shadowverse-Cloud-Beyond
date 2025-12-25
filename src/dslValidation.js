import { DslEditor } from "./components/DslEditor.js";
import { validateAST } from "./components/dsl_editor/core.js";

export function validateDslExpression(metadata, expression, options = {}) {
    const expectedType = options.expectedType || null;
    const allowEmpty = options.allowEmpty === true;
    const contextOverride = options.contextOverride || null;

    const trimmed = (expression ?? "").trim();
    if (!trimmed) {
        return {
            ast: null,
            issues: allowEmpty
                ? []
                : [{ severity: "error", message: "DSL 表达式不能为空" }]
        };
    }

    const editor = new DslEditor();
    editor.metadata = metadata;
    editor.isInitialized = true;
    editor.expectedType = expectedType;
    editor.contextOverride = contextOverride;

    try {
        editor.parseExpression(trimmed);
    } catch (e) {
        return {
            ast: null,
            issues: [{ severity: "error", message: `DSL 解析失败: ${e?.message || e}` }]
        };
    }

    const ast = editor.astRoot;
    if (!ast) {
        return {
            ast: null,
            issues: [{ severity: "error", message: "DSL 解析失败: AST 为空" }]
        };
    }

    const issues = [];

    const astValidation = validateAST(ast);
    if (!astValidation.isValid) {
        for (const err of astValidation.errors || []) {
            issues.push({ severity: "error", message: String(err) });
        }
    }

    if (typeof editor.validateExpressionSemantics === "function") {
        const semantic = editor.validateExpressionSemantics(ast);
        for (const err of semantic?.issues || []) {
            issues.push({ severity: "error", message: String(err) });
        }
    }

    if (expectedType && typeof editor.calculateNodeTypeMatch === "function") {
        const match = editor.calculateNodeTypeMatch(ast, expectedType);
        if (match && match.isTypeMatch === false) {
            issues.push({
                severity: "error",
                message: `类型不匹配：期望 ${match.expectedType || expectedType}，实际 ${match.actualType || "未知"}`
            });
        }
    }

    return { ast, issues };
}
