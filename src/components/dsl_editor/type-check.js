import { inferOperatorReturnType } from "./core.js";

export function calculateASTReturnType(editor, node) {
    if (!node) return 'Object';

    console.log("DslEditor: Calculating return type for node:", node);

    if (node.type === 'value') {
        if (node.metadata && node.metadata.type === "encapsulated" && node.metadata.innerAST) {
            const innerReturnType = calculateASTReturnType(editor, node.metadata.innerAST);
            console.log("DslEditor: Encapsulated expression inner return type:", innerReturnType);
            return innerReturnType;
        }

        const returnType = node.returnType || 'Object';
        console.log("DslEditor: Value node return type:", returnType);
        return returnType;
    }

    if (node.type === 'operator') {
        const leftType = node.leftOperand ? calculateASTReturnType(editor, node.leftOperand) : null;
        const rightType = node.rightOperand ? calculateASTReturnType(editor, node.rightOperand) : null;
        if (['|', '&'].includes(node.operator) && leftType && editor.isCollectionType(leftType)) {
            console.log("DslEditor: Operator", node.operator, "return type:", leftType);
            return leftType;
        }
        if (node.operator === '-' && leftType && editor.isCollectionType(leftType) && (!rightType || editor.isCollectionType(rightType))) {
            console.log("DslEditor: Operator", node.operator, "return type:", leftType);
            return leftType;
        }
        const operatorReturnType = inferOperatorReturnType(node.operator);
        console.log("DslEditor: Operator", node.operator, "return type:", operatorReturnType);
        return operatorReturnType;
    } else if (node.type === 'functionCall') {
        const returnType = node.returnType || 'Object';
        return returnType;
    } else if (node.type === 'listQuery' || node.type === 'listSort' || node.type === 'listTake') {
        if (node.source) {
            const sourceReturnType = calculateASTReturnType(editor, node.source);
            console.log(`DslEditor: List operation node (${node.type}) return type is same as source:`, sourceReturnType);
            return sourceReturnType;
        }
    }

    console.log("DslEditor: Unknown node type, defaulting to Object");
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
