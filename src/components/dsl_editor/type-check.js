import { getSemanticNodeType } from "./core.js";

export function calculateASTReturnType(editor, node) {
    const semanticType = getSemanticNodeType(node);
    return semanticType || 'Object';
}

export function validateExpressionSemantics(editor, node) {
    const result = {
        hasSemanticIssues: false,
        issues: [],
        fixSuggestions: []
    };

    const typeMatch = editor.calculateNodeTypeMatch(node);
    if (!typeMatch.isTypeMatch) {
        result.hasSemanticIssues = true;
        result.issues.push(`类型不匹配：期望 ${typeMatch.expectedType}，实际 ${typeMatch.actualType}`);
    }

    if (node.type === 'operator') {
        if (node.leftOperand) {
            const leftValidation = validateExpressionSemantics(editor, node.leftOperand);
            if (leftValidation.hasSemanticIssues) {
                result.hasSemanticIssues = true;
                result.issues.push(...leftValidation.issues);
            }
        }
        if (node.rightOperand) {
            const rightValidation = validateExpressionSemantics(editor, node.rightOperand);
            if (rightValidation.hasSemanticIssues) {
                result.hasSemanticIssues = true;
                result.issues.push(...rightValidation.issues);
            }
        }
    }

    if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
        const innerValidation = validateExpressionSemantics(editor, node.metadata.innerAST);
        if (innerValidation.hasSemanticIssues) {
            result.hasSemanticIssues = true;
            result.issues.push(...innerValidation.issues);
        }
    }

    return result;
}
