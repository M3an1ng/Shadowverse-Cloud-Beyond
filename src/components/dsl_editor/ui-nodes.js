import { getEventTypeHint, resolveEventParameterMeta } from "./event-translation.js";

export function renderFunctionCallNodeAST(editor, node, container, options = {}) {
    const args = node.args || [];
    const useMethodStyle = args.length > 0;
    const wrapper = document.createElement('span');
    wrapper.className = 'dsl-node dsl-function-call';
    const readableName = editor.getReadableFunctionName ? editor.getReadableFunctionName(node.functionName) : node.functionName;
    const clickableArgs = Boolean(options?.clickableArgs);
    const encapsulatedNode = options?.encapsulatedNode || null;
    const isParentSelected = options?.isParentSelected || false;
    const omitParensForSingleArg = Boolean(options?.omitParensForSingleArg);

    if (clickableArgs) {
        wrapper.addEventListener('click', (e) => {
            e.stopPropagation();
            if (typeof editor.editSubExpression === 'function') {
                editor.editSubExpression(node);
            }
        });
    }

    const renderArgNode = (arg, index) => {
        if (arg) {
            if (clickableArgs && typeof editor.renderClickableASTNode === 'function') {
                editor.renderClickableASTNode(arg, wrapper, encapsulatedNode, isParentSelected);
            } else {
                editor.renderASTNodeRecursive(arg, wrapper);
            }
        } else {
            editor.renderEmptySlotPlaceholder(node, 'arg', wrapper, index);
        }
    };

    if (useMethodStyle) {
        const receiver = args.length > 0 ? args[0] : null;
        if (receiver) {
            if (clickableArgs && typeof editor.renderClickableASTNode === 'function') {
                editor.renderClickableASTNode(receiver, wrapper, encapsulatedNode, isParentSelected);
            } else {
                editor.renderASTNodeRecursive(receiver, wrapper);
            }
        } else {
            editor.renderEmptySlotPlaceholder(node, 'arg', wrapper, 0);
        }

        const dot = document.createElement('span');
        dot.textContent = '.';
        wrapper.appendChild(dot);

        const name = document.createElement('span');
        name.textContent = readableName;
        wrapper.appendChild(name);

        const restCount = args.length - 1;
        if (restCount <= 0) {
            container.appendChild(wrapper);
            return;
        }

        if (omitParensForSingleArg && restCount === 1) {
            const spacer = document.createElement('span');
            spacer.textContent = ' ';
            wrapper.appendChild(spacer);
            renderArgNode(args[1], 1);
            container.appendChild(wrapper);
            return;
        }

        const open = document.createElement('span');
        open.textContent = '(';
        wrapper.appendChild(open);

        for (let i = 1; i < args.length; i++) {
            if (i > 1) {
                const comma = document.createElement('span');
                comma.textContent = ', ';
                wrapper.appendChild(comma);
            }
            renderArgNode(args[i], i);
        }

        const close = document.createElement('span');
        close.textContent = ')';
        wrapper.appendChild(close);

        container.appendChild(wrapper);
        return;
    }

    const name = document.createElement('span');
    name.textContent = readableName;
    wrapper.appendChild(name);

    if (args.length === 0) {
        container.appendChild(wrapper);
        return;
    }

    const open = document.createElement('span');
    open.textContent = '(';
    wrapper.appendChild(open);

    for (let i = 0; i < args.length; i++) {
        if (i > 0) {
            const comma = document.createElement('span');
            comma.textContent = ', ';
            wrapper.appendChild(comma);
        }

        renderArgNode(args[i], i);
    }

    const close = document.createElement('span');
    close.textContent = ')';
    wrapper.appendChild(close);

    container.appendChild(wrapper);
}

function getExpectedTypeForSlot(editor, parentNode, slot, index) {
    if (parentNode?.type === 'virtualRoot' && slot === 'main') {
        return editor.normalizeTypeName(editor.expectedType);
    }

    if (parentNode?.type === 'functionCall' && slot === 'arg') {
        const argIndex = typeof index === 'number' ? index : 0;
        const param = (parentNode.parameters || [])[argIndex];
        const paramType = editor.normalizeTypeName(param?.typeName);
        if (paramType && paramType !== 'Object') {
            return paramType;
        }
        if (argIndex > 0) {
            const receiver = (parentNode.args || [])[0];
            const receiverType = receiver ? editor.calculateASTReturnType?.(receiver) : null;
            if (receiverType) {
                if (editor.isCollectionType?.(receiverType)) {
                    return editor.normalizeTypeName(editor.getCollectionInnerType?.(receiverType)) || 'Object';
                }
                if (editor.isTextType?.(receiverType)) {
                    return 'String';
                }
            }
        }
        return paramType;
    }

    if (parentNode?.type === 'listQuery' && slot === 'filterExpression') {
        return 'Boolean';
    }

    if (parentNode?.type === 'listSort' && slot === 'sortExpression') {
        return 'Object';
    }

    if (parentNode?.type === 'listTake' && slot === 'countExpression') {
        return 'Int32';
    }

    if (parentNode?.type === 'listDistinct' && slot === 'keyExpression') {
        return 'Object';
    }

    if (parentNode?.type === 'listSelect' && slot === 'selectExpression') {
        return 'Object';
    }

    if (parentNode?.type === 'operator') {
        const op = parentNode.operator;
        const logicalOps = ['&&', '||'];
        const compareOps = ['==', '!=', '>', '<', '>=', '<=', 'contains', 'startsWith', 'endsWith'];
        const arithmeticOps = ['+', '-', '*', '/'];

        if (logicalOps.includes(op)) {
            return 'Boolean';
        }
        if (arithmeticOps.includes(op)) {
            return 'Number';
        }
        if (compareOps.includes(op)) {
            const sibling = slot === 'left' ? parentNode.rightOperand : parentNode.leftOperand;
            const siblingType = sibling?.returnType || editor.calculateASTReturnType?.(sibling);
            const normalizedSibling = editor.normalizeTypeName(siblingType);
            if (['contains', 'startsWith', 'endsWith'].includes(op)) {
                if (normalizedSibling && editor.isCollectionType?.(normalizedSibling)) {
                    const inner = editor.getCollectionInnerType?.(normalizedSibling);
                    return slot === 'right'
                        ? (editor.normalizeTypeName(inner) || normalizedSibling)
                        : normalizedSibling;
                }
                if (normalizedSibling && editor.isTextType?.(normalizedSibling)) {
                    return 'String';
                }
            }
            return normalizedSibling;
        }
    }

    return editor.normalizeTypeName(editor.slotExpectedType || editor.expectedType);
}

function getPlaceholderLabel(editor, expectedType) {
    if (!expectedType) return '待填写';
    if (String(expectedType).toLowerCase() === 'object') return '任意类型';
    const display = editor.getDisplayTypeName(expectedType);
    return display || '待填写';
}

export function renderValueNode(editor, node, container) {

    if (node.metadata?.type === 'empty') {
        const placeholderInfo = editor.resolveEmptySlot(node.parent, node);
        if (placeholderInfo.slot) {
            editor.renderEmptySlotPlaceholder(node.parent, placeholderInfo.slot, container, placeholderInfo.index);
        }
        return;
    }

    if (node.metadata && node.metadata.type === "encapsulated") {
        editor.renderEncapsulatedExpression(node, container);
        return;
    }

    const nodeElement = document.createElement("span");
    nodeElement.className = "dsl-node dsl-value-node";
    nodeElement.dataset.nodeType = node.metadata?.type || node.type;

    if (node.metadata && node.metadata.type === "context") {
        nodeElement.textContent = editor.getReadableValueLabel(node);
        nodeElement.classList.add("dsl-value-node--context");
    } else if (node.metadata.type === "property") {
        nodeElement.textContent = editor.getReadableValueLabel(node);
        nodeElement.classList.add("dsl-value-node--property");
    } else if (node.metadata.type === "staticValue") {
        if (node.metadata.isAwaitingInput) {
            editor.renderStaticValueInput(node, container);
            return;
        } else {
            nodeElement.textContent = editor.getReadableValueLabel(node);
            nodeElement.classList.add("dsl-value-node--static");
        }
    } else if (node.metadata.type === "enumValue") {
        nodeElement.textContent = editor.getReadableValueLabel(node);
        nodeElement.classList.add("dsl-value-node--static");
    } else if (node.metadata.type === "propertyInput") {
        editor.renderPropertyInput(node, container);
        return;
    } else if (node.metadata.type === "rawText") {
        editor.renderRawTextInput(node, container);
        return;
    } else if (node.metadata.type === "function") {
        nodeElement.textContent = editor.getReadableValueLabel(node);
        nodeElement.classList.add("dsl-value-node--function");
    } else if (node.metadata.type === "eventField") {
        nodeElement.textContent = editor.getReadableValueLabel(node);
        nodeElement.classList.add("dsl-value-node--event");
    }
    if (node._typeMatchInfo && !node._typeMatchInfo.isTypeMatch && node._typeMatchInfo.needsCorrection) {
        nodeElement.classList.add("dsl-value-node--error");
        nodeElement.title = `类型不匹配：期望 ${node._typeMatchInfo.expectedType}，实际 ${node._typeMatchInfo.actualType}`;
    }

    container.appendChild(nodeElement);
}

export function getEncapsulationStyle(editor, node) {
    const isMainExpression = editor?.virtualRoot?.main === node;
    const parseState = editor?.parseStatus?.state;
    if (isMainExpression && parseState === "fallback") {
        return {
            background: "linear-gradient(135deg, #ffebee 0%, #ffcdd2 100%)",
            borderColor: "#e74c3c",
            color: "#c62828",
            boxShadow: "0 2px 4px rgba(231, 76, 60, 0.2)",
            hoverBoxShadow: "0 4px 8px rgba(231, 76, 60, 0.3)",
            badgeSymbol: "×",
            badgeColor: "#e74c3c",
            badgeTitle: editor?.parseStatus?.message || "表达式解析失败，已降级为原始文本",
            hasIssue: true
        };
    }

    const typeMatch = node._typeMatchInfo || editor.calculateNodeTypeMatch(node);
    const hasTypeIssue = !typeMatch.isTypeMatch && typeMatch.needsCorrection;

    if (hasTypeIssue) {
        return {
            background: "linear-gradient(135deg, #ffebee 0%, #ffcdd2 100%)",
            borderColor: "#e74c3c",
            color: "#c62828",
            boxShadow: "0 2px 4px rgba(231, 76, 60, 0.2)",
            hoverBoxShadow: "0 4px 8px rgba(231, 76, 60, 0.3)",
            badgeSymbol: "×",
            badgeColor: "#e74c3c",
            badgeTitle: `类型不匹配：期望 ${typeMatch.expectedType}，实际 ${typeMatch.actualType}`,
            hasIssue: true
        };
    }

    return {
        background: "linear-gradient(135deg, #e8f5e8 0%, #f0f8f0 100%)",
        borderColor: "#27ae60",
        color: "#2c3e50",
        boxShadow: "0 2px 4px rgba(39, 174, 96, 0.2)",
        hoverBoxShadow: "0 4px 8px rgba(39, 174, 96, 0.3)",
        badgeSymbol: "✓",
        badgeColor: "#27ae60",
        badgeTitle: "表达式已完成",
        hasIssue: false
    };
}

export function createStatusBadge(style) {
    const badge = document.createElement("span");
    badge.textContent = style.badgeSymbol;
    badge.title = style.badgeTitle;

    badge.className = "dsl-encapsulated-badge dsl-encapsulated-badge--main";
    if (style.hasIssue) {
        badge.classList.add("has-issue");
    }
    return badge;
}

export function removeBrackets(text) {
    if (typeof text !== 'string') return text;
    if (text.startsWith('(') && text.endsWith(')')) {
        return text.slice(1, -1);
    }
    return text;
}

function unwrapNestedEncapsulated(node) {
    let current = node;
    while (current && current.metadata?.type === "encapsulated" && current.metadata?.innerAST) {
        current = current.metadata.innerAST;
    }
    return current;
}

export function renderEncapsulatedExpression(editor, node, container, options = {}) {
    if (node.metadata.innerAST && node.metadata.innerAST.metadata?.type === 'empty') {
        editor.renderPlaceholderCapsule(node, container);
        return;
    }

    const capsuleElement = document.createElement("span");
    capsuleElement.className = "dsl-encapsulated-expression";

    const style = getEncapsulationStyle(editor, node);
    const isSelected = editor.selectedNode === node;

    capsuleElement.classList.toggle("is-selected", isSelected);
    capsuleElement.classList.toggle("has-issue", style.hasIssue);
    capsuleElement.dataset.encapsulated = "true";
    capsuleElement.dataset.issue = style.hasIssue ? "true" : "false";

    const contentSpan = document.createElement("span");
    contentSpan.className = "dsl-encapsulated-content";
    const resolveDslNode = (start, boundary) => {
        if (!start || !start.closest) return null;
        const hit = start.closest('[data-dsl-node="true"]');
        if (!hit || !boundary.contains(hit)) return null;
        return hit.__dslNode || null;
    };
    const resolveDslNodeFromPoint = (event, boundary) => {
        if (typeof document.elementsFromPoint !== "function") {
            const target = event.target?.nodeType === Node.TEXT_NODE ? event.target.parentElement : event.target;
            return resolveDslNode(target, boundary);
        }
        const hits = document.elementsFromPoint(event.clientX, event.clientY);
        for (const hit of hits) {
            if (hit && hit.closest) {
                const nodeTarget = resolveDslNode(hit, boundary);
                if (nodeTarget) return nodeTarget;
            }
        }
        return null;
    };

    if (node.metadata.innerAST) {
        const innerAst = options.flattenNested
            ? unwrapNestedEncapsulated(node.metadata.innerAST)
            : node.metadata.innerAST;
        editor.renderClickableSubExpressions(innerAst, contentSpan, node, isSelected);
    } else {
        contentSpan.textContent = removeBrackets(node.value);
    }

    capsuleElement.appendChild(contentSpan);

    const badge = createStatusBadge(style);
    if (badge) {
        capsuleElement.appendChild(badge);
    }

    capsuleElement.addEventListener('click', (e) => {
        e.stopPropagation();
        if (typeof editor.editSubExpression !== 'function') {
            return;
        }

        const ignoreSelector = [
            '.dsl-static-input',
            '.dsl-rawtext-input',
            '.dsl-static-input-container',
            '.dsl-property-input-container',
            '.dsl-rawtext-input-container'
        ].join(', ');
        const rawTarget = e.target?.nodeType === Node.TEXT_NODE ? e.target.parentElement : e.target;
        if (rawTarget && rawTarget.closest && rawTarget.closest(ignoreSelector)) {
            return;
        }

        const nodeTarget = resolveDslNodeFromPoint(e, capsuleElement);
        const shouldDebug = Boolean(window.DEBUG_DSL);
        const debugHits = (shouldDebug && typeof document.elementsFromPoint === "function")
            ? document.elementsFromPoint(e.clientX, e.clientY).slice(0, 8).map(el => ({
                tag: el.tagName,
                cls: el.className,
                dslNode: el.dataset?.dslNode || null
            }))
            : [];
        if (shouldDebug) {
            console.log("DslEditor: capsule click", {
                targetTag: rawTarget?.tagName,
                targetClass: rawTarget?.className,
                targetDslNode: rawTarget?.dataset?.dslNode || null,
                resolvedNode: nodeTarget ? {
                    type: nodeTarget.type,
                    meta: nodeTarget.metadata?.type,
                    value: nodeTarget.value ?? null,
                    functionName: nodeTarget.functionName ?? null
                } : null,
                hasInnerAst: Boolean(node.metadata?.innerAST),
                hitStack: debugHits
            });
        }
        if (nodeTarget && nodeTarget !== node) {
            if (shouldDebug) {
                console.log("DslEditor: routing to inner node", {
                    type: nodeTarget.type,
                    meta: nodeTarget.metadata?.type,
                    value: nodeTarget.value ?? null
                });
            }
            editor.editSubExpression(nodeTarget);
            return;
        }

        if (node.metadata.innerAST) {
            if (shouldDebug) {
                console.log("DslEditor: fallback to innerAST root", {
                    type: node.metadata.innerAST?.type,
                    meta: node.metadata.innerAST?.metadata?.type,
                    value: node.metadata.innerAST?.value ?? null
                });
            }
            editor.editSubExpression(node.metadata.innerAST);
            return;
        }

        if (shouldDebug) {
            console.log("DslEditor: fallback to main encapsulated node");
        }
        editor.selectedNode = node;
        editor.handleEnterEditModeForSlot(editor.virtualRoot, 'main');
    });

    container.appendChild(capsuleElement);
}

export function renderPlaceholderCapsule(editor, node, container) {
    const placeholderCapsule = document.createElement("span");
    placeholderCapsule.className = "dsl-placeholder-capsule";
    const isSelected = editor.selectedNode === node;
    placeholderCapsule.classList.add("dsl-placeholder-capsule--main");
    placeholderCapsule.classList.toggle("is-selected", isSelected);
    placeholderCapsule.dataset.emptySlot = "true";
    placeholderCapsule.dataset.placeholderScope = "main";
    placeholderCapsule.dataset.dslNode = "true";
    placeholderCapsule.__dslNode = node;

    const expectedType = getExpectedTypeForSlot(editor, editor.virtualRoot, 'main');
    const label = getPlaceholderLabel(editor, expectedType);
    const placeholderText = document.createElement("span");
    placeholderText.className = "dsl-placeholder-text";
    placeholderText.textContent = label;
    placeholderCapsule.appendChild(placeholderText);

    placeholderCapsule.addEventListener('click', (e) => {
        e.stopPropagation();
        editor.selectedNode = node;
        editor.handleEnterEditModeForSlot(editor.virtualRoot, 'main');
    });

    container.appendChild(placeholderCapsule);
}

export function renderNestedEncapsulatedExpression(editor, innerNode, container) {
    const nestedCapsule = document.createElement("span");
    nestedCapsule.className = "dsl-nested-encapsulated-expression";
    nestedCapsule.dataset.dslNode = "true";
    nestedCapsule.__dslNode = innerNode;

    const isSelected = editor.selectedNode === innerNode;
    nestedCapsule.classList.toggle("is-selected", isSelected);
    nestedCapsule.dataset.encapsulated = "true";

    nestedCapsule.addEventListener('click', (e) => {
        e.stopPropagation();
        editor.selectedNode = innerNode;
        editor.renderEditor();
        editor.editSubExpression(innerNode);
    });

    const nestedContent = document.createElement("span");
    nestedContent.className = "dsl-encapsulated-content";

    if (innerNode.metadata.innerAST) {
        editor.renderClickableSubExpressions(innerNode.metadata.innerAST, nestedContent, innerNode, isSelected);
    } else {
        const displayText = innerNode.metadata.displayValue || innerNode.value;
        nestedContent.textContent = removeBrackets(displayText);
    }

    nestedCapsule.appendChild(nestedContent);

    let hasTypeIssue = false;
    let typeIssueDetails = null;


    if (innerNode._typeMatchInfo) {
        const typeMatch = innerNode._typeMatchInfo;
        hasTypeIssue = !typeMatch.isTypeMatch && typeMatch.needsCorrection;
        if (hasTypeIssue) {
            typeIssueDetails = `类型不匹配：期望 ${typeMatch.expectedType}，实际 ${typeMatch.actualType}`;
        }
    } else {
    }
    nestedCapsule.dataset.issue = hasTypeIssue ? "true" : "false";

    const completedBadge = document.createElement("span");
    completedBadge.textContent = hasTypeIssue ? "×" : "✓";
    completedBadge.className = "dsl-encapsulated-badge dsl-encapsulated-badge--nested";
    if (hasTypeIssue) {
        completedBadge.classList.add("has-issue");
        completedBadge.title = typeIssueDetails;
    }

    nestedCapsule.appendChild(completedBadge);
    container.appendChild(nestedCapsule);
}

export function renderOperatorNodeAST(editor, node, container) {
    const operatorContainer = document.createElement("div");
    operatorContainer.className = "dsl-operator-container";

    const leftSource = node.source || node.leftOperand;
    if (leftSource) {
        editor.renderASTNodeRecursive(leftSource, operatorContainer);
    }

    const operatorElement = document.createElement("span");
    operatorElement.className = "dsl-operator-pill";
    operatorElement.dataset.operatorKind = node.type === 'operator' ? 'binary' : 'list';
    if (node.type === 'operator') {
        operatorElement.textContent = node.operator;
        operatorElement.classList.add("dsl-operator-pill--binary");
    } else {
        let opName = node.type.replace('list', '').toLowerCase();
        if (node.type === 'listSort') {
            opName = 'orderBy';
        }
        operatorElement.textContent = `.${opName}`;
        operatorElement.classList.add("dsl-operator-pill--list");
    }
    operatorContainer.appendChild(operatorElement);

    const wrapWithParens = node.type !== 'operator';
    if (wrapWithParens) {
        const parenLeft = document.createElement("span");
        parenLeft.textContent = "(";
        operatorContainer.appendChild(parenLeft);
    }

    let rightOperand = null;
    let slotName = '';
    if (node.type === 'operator') {
        rightOperand = node.rightOperand;
        slotName = 'right';
    } else if (node.type === 'listQuery') {
        rightOperand = node.filterExpression;
        slotName = 'filterExpression';
    } else if (node.type === 'listSort') {
        rightOperand = node.sortExpression;
        slotName = 'sortExpression';
    } else if (node.type === 'listTake') {
        rightOperand = node.countExpression;
        slotName = 'countExpression';
    } else if (node.type === 'listDistinct') {
        rightOperand = node.keyExpression;
        slotName = 'keyExpression';
    } else if (node.type === 'listSelect') {
        rightOperand = node.selectExpression;
        slotName = 'selectExpression';
    }

    if (rightOperand && rightOperand.metadata?.type !== 'empty') {
        editor.renderASTNodeRecursive(rightOperand, operatorContainer);
    } else {
        editor.renderEmptySlotPlaceholder(node, slotName, operatorContainer);
    }

    if (wrapWithParens) {
        const parenRight = document.createElement("span");
        parenRight.textContent = ")";
        operatorContainer.appendChild(parenRight);
    }

    container.appendChild(operatorContainer);
}

export function renderStaticValueInput(editor, node, container) {
    const inputContainer = document.createElement("div");
    inputContainer.className = "dsl-static-input-container";

    const inputElement = document.createElement("input");
    inputElement.type = "text";
    inputElement.className = "dsl-static-input";
    inputElement.placeholder = "输入值";
    inputElement.value = node.metadata.staticValue || "";

    const applyDraftValue = (value) => {
        node.metadata.staticValue = value;
        if (value) {
            const inferredType = editor.inferStaticValueType(value);
            node.value = editor.formatStaticValue(value, inferredType);
            node.returnType = inferredType;
        } else {
            node.value = "[输入值]";
            node.returnType = "Object";
        }
        node.metadata.isAwaitingInput = true;
        node.updateCompleteness();
        if (node.parent) {
            node.parent.updateCompleteness();
        }
    };

    const commitValue = (value) => {
        const trimmed = value.trim();
        applyDraftValue(trimmed);
        node.metadata.isAwaitingInput = !trimmed;
        node.updateCompleteness();
        if (node.parent) {
            node.parent.updateCompleteness();
        }
        editor.updateExpressionFromAST();
        if (editor.onChange) {
            editor.onChange(editor.expression);
        }
    };

    inputElement.addEventListener("input", (e) => {
        applyDraftValue(e.target.value.trim());
        if (!editor.isLightWorkspace) {
            editor.updateExpressionFromAST({ skipRender: true });
        }
    });

    inputElement.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
            e.preventDefault();
            commitValue(e.target.value);
            inputElement.blur();
        }
    });

    inputElement.addEventListener("blur", (e) => {
        commitValue(e.target.value);
    });

    inputContainer.appendChild(inputElement);
    container.appendChild(inputContainer);

    setTimeout(() => inputElement.focus(), 100);
}

export function renderPropertyInput(editor, node, container) {
    const inputContainer = document.createElement("span");
    inputContainer.className = "dsl-property-input-container";

    const basePath = node.metadata?.basePath || "";
    const readablePrefix = editor.getReadablePropertyChain(basePath) || basePath;
    if (readablePrefix) {
        const prefix = document.createElement("span");
        prefix.className = "dsl-property-input-prefix";
        prefix.textContent = `${readablePrefix}.`;
        inputContainer.appendChild(prefix);
    }

    const inputElement = document.createElement("input");
    inputElement.type = "text";
    inputElement.className = "dsl-static-input";
    inputElement.placeholder = "输入键名";
    inputElement.value = node.metadata?.key || "";

    const normalizeKey = (value) => {
        const trimmed = String(value || "").trim();
        if (!trimmed) return "";
        if ((trimmed.startsWith('"') && trimmed.endsWith('"')) ||
            (trimmed.startsWith("'") && trimmed.endsWith("'"))) {
            return trimmed.slice(1, -1).trim();
        }
        return trimmed;
    };

    const applyDraftKey = (value) => {
        const key = normalizeKey(value);
        node.metadata.key = key;
        node.metadata.isAwaitingInput = !key;
        node.value = key ? (basePath ? `${basePath}.${key}` : key) : basePath;
        node.updateCompleteness();
        if (node.parent) {
            node.parent.updateCompleteness();
        }
    };

    const commitKey = (value) => {
        const key = normalizeKey(value);
        if (!key) {
            editor.showExpressionError("键名不能为空。");
            return;
        }
        if (key.includes(".")) {
            editor.showExpressionError("键名不能包含 '.' 。");
            return;
        }

        const fullPath = basePath ? `${basePath}.${key}` : key;
        node.value = fullPath;
        node.returnType = node.metadata?.returnType || node.returnType || "Object";
        node.metadata = {
            type: "property",
            fullPath,
            displayName: key,
            isNested: true
        };
        node.updateCompleteness();
        if (node.parent) {
            node.parent.updateCompleteness();
        }
        editor.updateExpressionFromAST();
        if (editor.onChange) {
            editor.onChange(editor.expression);
        }
    };

    inputElement.addEventListener("input", (e) => {
        applyDraftKey(e.target.value);
        editor.updateExpressionFromAST({ skipRender: true });
    });

    inputElement.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
            e.preventDefault();
            commitKey(e.target.value);
            inputElement.blur();
        }
    });

    inputElement.addEventListener("blur", () => {
        commitKey(inputElement.value);
    });

    inputContainer.appendChild(inputElement);
    container.appendChild(inputContainer);

    setTimeout(() => inputElement.focus(), 100);
}

export function renderRawTextInput(editor, node, container) {
    const inputContainer = document.createElement("div");
    inputContainer.className = "dsl-rawtext-input-container";

    const inputElement = document.createElement("input");
    inputElement.type = "text";
    inputElement.className = "dsl-rawtext-input";
    inputElement.placeholder = "输入 DSL 表达式";
    inputElement.value = node.value || node.metadata?.rawText || "";

    const commit = async () => {
        const value = inputElement.value.trim();
        if (!value) {
            editor.showExpressionError("表达式不能为空。");
            return;
        }
        if (value === (node.value || "").trim()) {
            return;
        }
        await editor.applyRawTextEdit(value);
    };

    inputElement.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
            e.preventDefault();
            inputElement.blur();
        }
    });

    inputElement.addEventListener("blur", () => {
        commit();
    });

    inputContainer.appendChild(inputElement);
    container.appendChild(inputContainer);
}

export function renderEmptySlotPlaceholder(editor, parentNode, slot, container, index = null) {
    const placeholderCapsule = document.createElement("span");
    placeholderCapsule.className = "dsl-placeholder-capsule";
    placeholderCapsule.classList.add("dsl-placeholder-capsule--slot");
    placeholderCapsule.dataset.emptySlot = "true";
    placeholderCapsule.dataset.slotName = slot;
    const expectedType = getExpectedTypeForSlot(editor, parentNode, slot, index);
    const label = getPlaceholderLabel(editor, expectedType);
    const placeholderText = document.createElement("span");
    placeholderText.className = "dsl-placeholder-text";
    placeholderText.textContent = label;
    placeholderCapsule.appendChild(placeholderText);

    placeholderCapsule.addEventListener('click', (e) => {
        e.stopPropagation();
        if (editor.isLightWorkspace && typeof editor.setLightWorkspaceFocus === "function") {
            editor.setLightWorkspaceFocus(parentNode, slot, index);
            editor.renderEditor();
            return;
        }
        editor.handleEnterEditModeForSlot(parentNode, slot, index);
    });

    container.appendChild(placeholderCapsule);
}

export function renderPropertyChainUI(editor, node, container) {
    const chainContainer = document.createElement("div");
    const finalReturnType = editor.calculateASTReturnType(node);
    const style = getStyleForValueType(editor, finalReturnType);
    chainContainer.className = "dsl-property-chain";
    chainContainer.classList.add(`dsl-value-style--${style.key}`);
    chainContainer.dataset.valueStyle = style.key;
    chainContainer.dataset.dslNode = "true";
    chainContainer.__dslNode = node;

    const chainParts = getReadablePropertyChainParts(editor, node.metadata?.fullPath || node.value);
    const parts = chainParts.length > 0 ? chainParts : (node.value || '').split('.');
    parts.forEach((part, index) => {
        const partElement = document.createElement("span");
        partElement.textContent = part;
        chainContainer.appendChild(partElement);

        if (index < parts.length - 1) {
            const separator = document.createElement("span");
            separator.textContent = ".";
            separator.className = "dsl-property-chain-separator";
            chainContainer.appendChild(separator);
        }
    });

    container.appendChild(chainContainer);
}

export function getStyleForValueType(editor, typeName) {
    let style = { key: 'default', background: '#f3e5f5', color: '#7b1fa2' };

    if (editor.isNumericType(typeName)) {
        style = { key: 'numeric', background: '#fff3e0', color: '#f57c00' };
    } else if (typeName === 'Boolean') {
        style = { key: 'boolean', background: '#e8f5e8', color: '#388e3c' };
    } else if (typeName === 'String' || editor.isTextType(typeName)) {
        style = { key: 'text', background: '#f3e5f5', color: '#7b1fa2' };
    } else if (typeName === 'context') {
        style = { key: 'context', background: '#e3f2fd', color: '#1976d2' };
    }

    return style;
}

export function getReadablePropertyChainParts(editor, path) {
    const safePath = String(path || '').trim();
    if (!safePath) return [];

    const segments = safePath.split('.').filter(Boolean);
    if (segments.length === 0) return [];

    const contextName = segments[0];
    const ctx = editor.getDslContextObject(contextName);
    let currentType = editor.normalizeTypeName(ctx?.typeName);
    const displayParts = [ctx?.displayName || contextName];
    const eventTypeHint = getEventTypeHint(editor, segments);

    for (let i = 1; i < segments.length; i++) {
        const seg = segments[i];
        const normalizedType = editor.normalizeTypeName(currentType);
        if (typeof editor.isDictionaryType === "function" && editor.isDictionaryType(normalizedType)) {
            displayParts.push(seg);
            const valueType = typeof editor.getDictionaryValueType === "function"
                ? editor.getDictionaryValueType(normalizedType)
                : null;
            currentType = editor.normalizeTypeName(valueType) || null;
            continue;
        }
        if (normalizedType === 'IGameEvent' || normalizedType === 'GameEventBase') {
            const eventParam = resolveEventParameterMeta(editor, seg, eventTypeHint);
            if (eventParam) {
                displayParts.push(eventParam.displayName || seg);
                currentType = editor.normalizeTypeName(eventParam.typeName) || null;
                continue;
            }
        }

        const tm = normalizedType ? editor.getTypeMetadata(normalizedType) : null;
        const prop = (tm?.properties || []).find(p =>
            String(p.name || '').toLowerCase() === String(seg).toLowerCase()
        );
        displayParts.push(prop?.displayName || seg);
        currentType = editor.normalizeTypeName(prop?.typeName) || null;
    }

    return displayParts;
}

export function getReadablePropertyChain(editor, path) {
    const parts = getReadablePropertyChainParts(editor, path);
    return parts.length > 0 ? parts.join('.') : path;
}
