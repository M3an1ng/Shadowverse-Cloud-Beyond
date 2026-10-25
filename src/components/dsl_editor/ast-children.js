const LIST_EXPRESSION_SLOTS = {
    listQuery: "filterExpression",
    listSort: "sortExpression",
    listTake: "countExpression",
    listDistinct: "keyExpression",
    listSelect: "selectExpression"
};

export function getAstChildSlots(node) {
    if (!node) return [];
    const children = [];
    const add = (child, slot, index = null) => {
        if (child) children.push({ node: child, slot, index });
    };

    if (node.type === "operator") {
        add(node.leftOperand, "left");
        add(node.rightOperand, "right");
    } else if (node.type === "functionCall") {
        (node.args || []).forEach((arg, index) => add(arg, "arg", index));
    } else if (Object.hasOwn(LIST_EXPRESSION_SLOTS, node.type)) {
        add(node.source, "source");
        const slot = LIST_EXPRESSION_SLOTS[node.type];
        add(node[slot], slot);
    }
    add(node.metadata?.innerAST, "innerAST");
    return children;
}
