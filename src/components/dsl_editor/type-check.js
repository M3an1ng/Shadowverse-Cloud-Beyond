import { inferOperatorReturnType } from "./core.js";

export function calculateASTReturnType(editor, node) {
    if (!node) return 'Object';


    if (node.type === 'value') {
        if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
            const innerReturnType = calculateASTReturnType(editor, node.metadata.innerAST);
            return innerReturnType;
        }

        const returnType = node.returnType || 'Object';
        return returnType;
    }

    if (node.type === 'operator') {
        const leftType = node.leftOperand ? calculateASTReturnType(editor, node.leftOperand) : null;
        const rightType = node.rightOperand ? calculateASTReturnType(editor, node.rightOperand) : null;
        if (['|', '&', '~'].includes(node.operator)) {
            if (leftType && editor.isCollectionType(leftType)) {
                return leftType;
            }
            if (rightType && editor.isCollectionType(rightType)) {
                return rightType;
            }
        }
        const operatorReturnType = inferOperatorReturnType(node.operator);
        return operatorReturnType;
    } else if (node.type === 'functionCall') {
        const returnType = node.returnType || 'Object';
        return returnType;
    } else if (node.type === 'listQuery' || node.type === 'listSort' || node.type === 'listTake' || node.type === 'listDistinct') {
        if (node.source) {
            const sourceReturnType = calculateASTReturnType(editor, node.source);
            return sourceReturnType;
        }
    } else if (node.type === 'listSelect') {
        const sourceReturnType = node.source ? calculateASTReturnType(editor, node.source) : null;
        const selectedReturnType = node.selectExpression
            ? calculateASTReturnType(editor, node.selectExpression)
            : null;
        if (sourceReturnType && typeof editor.isCollectionType === 'function' && editor.isCollectionType(sourceReturnType)) {
            const normalized = editor.normalizeTypeName?.(selectedReturnType) || selectedReturnType || "Object";
            return `List<${normalized}>`;
        }
        if (selectedReturnType) return selectedReturnType;
        if (sourceReturnType) return sourceReturnType;
    }

    return 'Object';
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
