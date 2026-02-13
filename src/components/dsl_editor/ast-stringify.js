const OPERATOR_PRECEDENCE = {
    "||": 1,
    "&&": 2,
    "==": 3,
    "!=": 3,
    ">": 4,
    "<": 4,
    ">=": 4,
    "<=": 4,
    "|": 5,
    "&": 6,
    "~": 6,
    "+": 7,
    "-": 7,
    "*": 8,
    "/": 8,
    "\\": 8,
    "%": 8
};

function getOperatorPrecedence(operator) {
    return OPERATOR_PRECEDENCE[String(operator || "")] || 0;
}

function isEmptyNode(node) {
    return !node || node.metadata?.type === "empty";
}

function shouldWrapOperatorChild(parentNode, childNode, side) {
    if (!parentNode || parentNode.type !== "operator") return false;
    if (!childNode || childNode.type !== "operator") return false;

    const parentPrec = getOperatorPrecedence(parentNode.operator);
    const childPrec = getOperatorPrecedence(childNode.operator);
    if (childPrec < parentPrec) return true;

    // Keep right-side equal-precedence grouping, e.g. a - (b - c)
    if (childPrec === parentPrec && side === "right") return true;

    return false;
}

function stringifyOperatorChild(childNode, parentNode, side) {
    if (isEmptyNode(childNode)) return "()";
    const childExpr = stringifyAstNode(childNode);
    if (shouldWrapOperatorChild(parentNode, childNode, side)) {
        return `(${childExpr})`;
    }
    return childExpr;
}

function stringifyPipelineSource(sourceNode) {
    if (!sourceNode) return "";
    const sourceExpr = stringifyAstNode(sourceNode);
    if (!sourceExpr) return "";

    if (sourceNode.type === "operator") {
        return `(${sourceExpr})`;
    }

    return sourceExpr;
}

function isEmptyEncapsulated(node) {
    return node?.metadata?.type === "encapsulated"
        && node?.metadata?.innerAST?.metadata?.type === "empty";
}

function isEmptyOptionalExpression(node, expression) {
    return isEmptyNode(node) || isEmptyEncapsulated(node) || !expression;
}

export function stringifyAstNode(node) {
    if (!node) return "";

    if (node.type === "value") {
        if (node.metadata?.type === "encapsulated") {
            return node.metadata.displayValue || node.value || "";
        }
        return node.value || "";
    }

    if (node.type === "functionCall") {
        const parts = (node.args || []).map((arg) => (arg ? stringifyAstNode(arg) : ""));
        return `${node.functionName}(${parts.join(", ")})`;
    }

    if (node.type === "operator") {
        const left = stringifyOperatorChild(node.leftOperand, node, "left");
        const right = stringifyOperatorChild(node.rightOperand, node, "right");
        return `${left} ${node.operator} ${right}`;
    }

    if (node.type === "listQuery") {
        const sourceExpr = stringifyPipelineSource(node.source);
        const filterExpr = node.filterExpression ? stringifyAstNode(node.filterExpression) : "";
        if (isEmptyOptionalExpression(node.filterExpression, filterExpr)) return sourceExpr;
        return `${sourceExpr}.query(${filterExpr})`;
    }

    if (node.type === "listSort") {
        const sourceExpr = stringifyPipelineSource(node.source);
        const sortExpr = node.sortExpression ? stringifyAstNode(node.sortExpression) : "";
        if (isEmptyOptionalExpression(node.sortExpression, sortExpr)) return sourceExpr;
        const dir = String(node.direction || "asc").toLowerCase();
        const suffix = dir === "desc" ? " desc" : "";
        return `${sourceExpr}.orderBy(${sortExpr}${suffix})`;
    }

    if (node.type === "listDistinct") {
        const sourceExpr = stringifyPipelineSource(node.source);
        const keyExpr = node.keyExpression ? stringifyAstNode(node.keyExpression) : "";
        return `${sourceExpr}.distinct(${keyExpr})`;
    }

    if (node.type === "listSelect") {
        const sourceExpr = stringifyPipelineSource(node.source);
        const selectExpr = node.selectExpression ? stringifyAstNode(node.selectExpression) : "";
        return `${sourceExpr}.select(${selectExpr})`;
    }

    if (node.type === "listTake") {
        const sourceExpr = stringifyPipelineSource(node.source);
        const strategy = String(node.strategy || "first").toLowerCase();
        if (strategy === "all") {
            return `${sourceExpr}.take(all)`;
        }

        const countExpr = node.countExpression ? stringifyAstNode(node.countExpression) : "";
        if (isEmptyOptionalExpression(node.countExpression, countExpr)) return sourceExpr;
        if (strategy && strategy !== "first") {
            return `${sourceExpr}.take(${strategy} ${countExpr})`;
        }
        return `${sourceExpr}.take(${countExpr})`;
    }

    return node.value || "";
}
