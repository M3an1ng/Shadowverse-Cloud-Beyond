import { DslEditor } from "./components/DslEditor.js";
import { validateAST } from "./components/dsl_editor/core.js";
import { refreshCompleteness, refreshValueNodeReturnTypes } from "./components/dsl_editor/ast-normalize.js";
import { requestParseAstApi } from "./components/dsl_editor/services/parse.js";

export async function validateDslExpression(metadata, expression, options = {}) {
    const expectedType = options.expectedType || null;
    const allowEmpty = options.allowEmpty === true;
    const contextOverride = options.contextOverride || null;
    const triggeringEventContext = options.triggeringEventContext || null;

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
    editor.triggeringEventContext = triggeringEventContext;
    if (typeof editor.preloadEnumLocalizations === "function") {
        await editor.preloadEnumLocalizations();
    }

    const parseResult = await requestParseAstApi(trimmed);
    if (!parseResult.success || !parseResult.root) {
        const message = parseResult.message || (parseResult.errors || [])[0] || "DSL 解析失败";
        return {
            ast: null,
            issues: [{ severity: "error", message: `DSL 解析失败: ${message}` }]
        };
    }

    const mapped = editor.mapDslAstDtoToUiAst(parseResult.root);
    if (!mapped) {
        return {
            ast: null,
            issues: [{ severity: "error", message: "DSL 解析失败: 无法映射 AST" }]
        };
    }

    editor.workspaceAST = mapped;
    editor.astRoot = mapped;
    try {
        editor.maintainASTRelationships(mapped);
        refreshValueNodeReturnTypes(editor, mapped);
        refreshCompleteness(mapped);
        editor.maintainASTRelationships(mapped);
    } catch (e) {
        return {
            ast: null,
            issues: [{ severity: "error", message: `DSL 解析失败: ${e?.message || e}` }]
        };
    }

    const ast = editor.workspaceAST;

    const issues = [];
    const debugSummary = buildValidationDebug(editor, ast, expectedType);

    const astValidation = validateAST(ast);
    if (!astValidation.isValid) {
        for (const err of astValidation.errors || []) {
            issues.push({
                severity: "error",
                message: `${String(err)}\n    [debug] ${debugSummary}`
            });
        }
    }

    if (typeof editor.validateExpressionSemantics === "function") {
        const semantic = editor.validateExpressionSemantics(ast);
        for (const err of semantic?.issues || []) {
            issues.push({
                severity: "error",
                message: `${String(err)}\n    [debug] ${debugSummary}`
            });
        }
    }

    if (expectedType && typeof editor.calculateNodeTypeMatch === "function") {
        const match = editor.calculateNodeTypeMatch(ast, expectedType);
        if (match && match.isTypeMatch === false) {
            issues.push({
                severity: "error",
                message: `类型不匹配：期望 ${match.expectedType || expectedType}，实际 ${match.actualType || "未知"}\n    [debug] ${debugSummary}`
            });
        }
    }

    return { ast, issues };
}

function buildValidationDebug(editor, ast, expectedType) {
    if (!editor || !ast) return "ast=null";
    const expr = ast?.toExpression?.() || "";
    const rootType = editor.calculateASTReturnType(ast);
    const parts = [
        `expected=${expectedType || "null"}`,
        `rootType=${rootType || "null"}`,
        `expr=${expr}`
    ];

    if (ast.type === "operator") {
        const left = ast.leftOperand;
        const right = ast.rightOperand;
        parts.push(`op=${ast.operator}`);
        parts.push(`left=${summarizeNode(left)}`);
        parts.push(`right=${summarizeNode(right)}`);
    }

    return parts.join(" | ");
}

function summarizeNode(node) {
    if (!node) return "null";
    const meta = node.metadata || {};
    const metaType = meta.type || "none";
    const chunks = [
        `type=${node.type}`,
        `return=${node.returnType || "null"}`,
        `meta=${metaType}`,
        `value=${node.value || ""}`
    ];
    if (meta.enumType) chunks.push(`enumType=${meta.enumType}`);
    if (meta.enumValue) chunks.push(`enumValue=${meta.enumValue}`);
    if (meta.staticValue) chunks.push(`staticValue=${meta.staticValue}`);
    if (meta.enumValues && Array.isArray(meta.enumValues)) {
        chunks.push(`enumValues=${meta.enumValues.join(",")}`);
    }
    return chunks.join(",");
}
