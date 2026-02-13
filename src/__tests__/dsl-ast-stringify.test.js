import {
    createValueNode,
    createOperatorNode,
    createListQueryNode,
    createListSortNode,
    createListTakeNode,
    createListDistinctNode,
    createListSelectNode
} from "../components/dsl_editor/core.js";

describe("DSL AST stringify", () => {
    test("keeps set-source grouped before query/take chain", () => {
        const union = createOperatorNode("|");
        union.setLeftOperand(createValueNode("context.owner.field", "List<CardInstance>", { type: "property" }));
        union.setRightOperand(createValueNode("context.owner.leader", "CardInstance", { type: "property" }));

        const queryNode = createListQueryNode(union);
        queryNode.setFilterExpression(
            createValueNode('((self.cardType) == "Follower")', "Boolean", { type: "rawText" })
        );

        const takeNode = createListTakeNode(queryNode);
        takeNode.setStrategy("random");
        takeNode.setCountExpression(createValueNode("(1)", "Int32", { type: "rawText" }));

        const minus = createOperatorNode("~");
        minus.setLeftOperand(takeNode);
        minus.setRightOperand(createValueNode("context.sourceCard", "CardInstance", { type: "property" }));

        const expr = minus.toExpression();
        expect(expr).toContain('(context.owner.field | context.owner.leader).query(((self.cardType) == "Follower")).take(random (1))');
        expect(expr).not.toContain('context.owner.field | context.owner.leader.query');
    });

    test("wraps operator source for all list pipeline nodes", () => {
        const source = createOperatorNode("|");
        source.setLeftOperand(createValueNode("context.owner.field", "List<CardInstance>", { type: "property" }));
        source.setRightOperand(createValueNode("context.opponent.field", "List<CardInstance>", { type: "property" }));

        const queryNode = createListQueryNode(source);
        queryNode.setFilterExpression(createValueNode("true", "Boolean", { type: "staticValue" }));

        const sortNode = createListSortNode(source);
        sortNode.setSortExpression(createValueNode("self.currentCost", "Int32", { type: "property" }));

        const takeNode = createListTakeNode(source);
        takeNode.setCountExpression(createValueNode("2", "Int32", { type: "staticValue" }));

        const distinctNode = createListDistinctNode(source);
        distinctNode.setKeyExpression(createValueNode("self.cardType", "String", { type: "property" }));

        const selectNode = createListSelectNode(source);
        selectNode.setSelectExpression(createValueNode("self.currentCost", "Int32", { type: "property" }));

        expect(queryNode.toExpression()).toMatch(/^\(context\.owner\.field \| context\.opponent\.field\)\.query\(/);
        expect(sortNode.toExpression()).toMatch(/^\(context\.owner\.field \| context\.opponent\.field\)\.orderBy\(/);
        expect(takeNode.toExpression()).toMatch(/^\(context\.owner\.field \| context\.opponent\.field\)\.take\(/);
        expect(distinctNode.toExpression()).toMatch(/^\(context\.owner\.field \| context\.opponent\.field\)\.distinct\(/);
        expect(selectNode.toExpression()).toMatch(/^\(context\.owner\.field \| context\.opponent\.field\)\.select\(/);
    });

    test("keeps right-side equal-precedence grouping for operators", () => {
        const root = createOperatorNode("-");
        root.setLeftOperand(createValueNode("a", "Int32", { type: "rawText" }));

        const right = createOperatorNode("-");
        right.setLeftOperand(createValueNode("b", "Int32", { type: "rawText" }));
        right.setRightOperand(createValueNode("c", "Int32", { type: "rawText" }));
        root.setRightOperand(right);

        expect(root.toExpression()).toBe("a - (b - c)");
    });
});
