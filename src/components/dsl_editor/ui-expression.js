import { isListOperationNode } from "./pipeline.js";

export function renderClickableMainExpression(editor, node, container) {
    if (!node) return;

    if (node.type === 'value') {
        const valueSpan = document.createElement("span");

        if (node.metadata && node.metadata.type === 'encapsulated') {
            editor.renderEncapsulatedExpression(node, container);
            return;
        }

        valueSpan.textContent = node.value;
        valueSpan.className = "dsl-value-chip dsl-value-chip--main";
        valueSpan.dataset.nodeType = node.metadata?.type || node.type;
        valueSpan.classList.toggle("is-selected", editor.selectedNode === node);

        container.appendChild(valueSpan);
    } else if (node.type === 'operator') {
        if (node.leftOperand) {
            renderClickableMainExpression(editor, node.leftOperand, container);
        } else {
            editor.renderEmptySlotPlaceholder(node, 'left', container);
        }

        const operatorSpan = document.createElement("span");
        operatorSpan.textContent = ` ${node.operator} `;
        operatorSpan.className = "dsl-operator-text";
        operatorSpan.dataset.operator = node.operator;
        container.appendChild(operatorSpan);

        if (node.rightOperand) {
            renderClickableMainExpression(editor, node.rightOperand, container);
        } else {
            editor.renderEmptySlotPlaceholder(node, 'right', container);
        }
    }
}

export function renderASTNodeRecursive(editor, node, container) {
    if (!node) return;

    console.log("DslEditor: renderASTNodeRecursive called for:", {
        type: node.type,
        value: node.value,
        metadataType: node.metadata?.type
    });

    if (node.type === 'value' && (node.metadata?.type === 'property' || (node.metadata?.type === 'context' && node.value.includes('.')))) {
        editor.renderPropertyChainUI(node, container);
        return;
    }

    if (node.isCompletedExpression) {
        const completedWrapper = document.createElement("span");
        completedWrapper.className = "dsl-completed-expression";
        const openParen = document.createElement("span");
        openParen.textContent = "(";
        openParen.className = "dsl-completed-paren dsl-completed-paren--open";
        completedWrapper.appendChild(openParen);
        const contentContainer = document.createElement("span");
        renderNodeContent(editor, node, contentContainer);
        completedWrapper.appendChild(contentContainer);
        const closeParen = document.createElement("span");
        closeParen.textContent = ")";
        closeParen.className = "dsl-completed-paren dsl-completed-paren--close";
        completedWrapper.appendChild(closeParen);
        const completedLabel = document.createElement("span");
        completedLabel.textContent = "✓";
        completedLabel.className = "dsl-completed-badge";
        completedWrapper.appendChild(completedLabel);
        container.appendChild(completedWrapper);
    } else {
        renderNodeContent(editor, node, container);
    }
}

export function renderNodeContent(editor, node, container) {
    if (node.type === 'value') {
        editor.renderValueNode(node, container);
    } else if (node.type === 'operator') {
        editor.renderOperatorNodeAST(node, container);
    } else if (node.type === 'functionCall') {
        editor.renderFunctionCallNodeAST(node, container);
    } else if (node.type === 'listQuery' || node.type === 'listSort' || node.type === 'listTake') {
        editor.renderListQueryNodeAST(node, container);
    }
}

export function renderClickableSubExpressions(editor, innerAST, container, encapsulatedNode, isParentSelected = false) {
    if (!innerAST) {
        const displayText = encapsulatedNode.metadata.displayValue || encapsulatedNode.value;
        container.textContent = editor.removeBrackets(displayText);
        return;
    }

    if (innerAST.type === 'value' && innerAST.metadata && innerAST.metadata.type === 'encapsulated') {
        editor.renderNestedEncapsulatedExpression(innerAST, container, encapsulatedNode);
    } else {
        renderClickableASTNode(editor, innerAST, container, encapsulatedNode, isParentSelected);
    }
}

export function renderClickableASTNode(editor, node, container, encapsulatedNode, isParentSelected = false) {
    if (!node) return;

    const highlightTarget = editor.getHighlightTargetNode ? editor.getHighlightTargetNode() : null;

    if (node.type === 'value') {
        if (node.metadata?.type === 'empty') {
            const placeholderInfo = editor.resolveEmptySlot(node.parent, node);
            if (placeholderInfo.slot) {
                editor.renderEmptySlotPlaceholder(node.parent, placeholderInfo.slot, container, placeholderInfo.index);
            }
            return;
        }

        if (node.metadata && node.metadata.type === 'encapsulated') {
            editor.renderNestedEncapsulatedExpression(node, container, encapsulatedNode);
            return;
        }

        const valueSpan = document.createElement("span");
        valueSpan.textContent = editor.getReadableValueLabel(node);

        valueSpan.className = "dsl-value-chip dsl-value-chip--sub";
        valueSpan.dataset.nodeType = node.metadata?.type || node.type;
        valueSpan.classList.toggle("is-highlighted", highlightTarget === node);
        valueSpan.classList.toggle("is-selected", editor.selectedNode === node);
        valueSpan.classList.toggle("is-parent-selected", editor.selectedNode !== node && isParentSelected);

        container.appendChild(valueSpan);
    } else if (node.type === 'operator') {
        if (node.leftOperand) {
            renderClickableASTNode(editor, node.leftOperand, container, encapsulatedNode);
        }

        const operatorSpan = document.createElement("span");
        operatorSpan.textContent = ` ${node.operator} `;
        operatorSpan.className = "dsl-operator-text";
        operatorSpan.dataset.operator = node.operator;
        operatorSpan.classList.toggle("is-selected", editor.selectedNode === node || isParentSelected);

        container.appendChild(operatorSpan);

        if (node.rightOperand) {
            renderClickableASTNode(editor, node.rightOperand, container, encapsulatedNode);
        } else {
            editor.renderEmptySlotPlaceholder(node, 'right', container);
        }
    } else if (node.type === 'listQuery' || node.type === 'listSort' || node.type === 'listTake') {
        renderClickableListNode(editor, node, container, encapsulatedNode);
    }
}

export function renderClickableListNode(editor, node, container, encapsulatedNode) {
    if (node.parent && isListOperationNode(node.parent)) {
        return;
    }
    const rendered = editor.renderPipelineBlock(node, container, (target, targetContainer) => {
        renderClickableASTNode(editor, target, targetContainer, encapsulatedNode);
    });
    if (rendered) return;

    if (node.source) {
        renderClickableASTNode(editor, node.source, container, encapsulatedNode);
    }

    const operatorSpan = document.createElement("span");
    let operatorName = node.type.replace('list', '').toLowerCase();
    if (node.type === 'listSort') {
        operatorName = 'orderBy';
    }
    operatorSpan.textContent = `.${operatorName}`;
    operatorSpan.className = "dsl-list-operator";
    operatorSpan.dataset.listOperator = operatorName;
    container.appendChild(operatorSpan);

    let expressionNode = null;
    let slotName = '';

    if (node.type === 'listQuery') {
        expressionNode = node.filterExpression;
        slotName = 'filterExpression';
    } else if (node.type === 'listSort') {
        expressionNode = node.sortExpression;
        slotName = 'sortExpression';
    } else if (node.type === 'listTake') {
        expressionNode = node.countExpression;
        slotName = 'countExpression';
    }

    if (expressionNode && expressionNode.metadata?.type !== 'empty') {
        renderClickableASTNode(editor, expressionNode, container, encapsulatedNode);
    } else {
        editor.renderEmptySlotPlaceholder(node, slotName, container);
    }
}
