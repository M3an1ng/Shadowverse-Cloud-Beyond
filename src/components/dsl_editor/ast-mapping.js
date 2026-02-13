import {
    createValueNode,
    createOperatorNode,
    createFunctionCallNode,
    createListQueryNode,
    createListSortNode,
    createListTakeNode,
    createListDistinctNode,
    createListSelectNode
} from "./core.js";
import { resolvePropertyChain } from "./property-chain-resolver.js";

export function mapDslAstDtoToUiAst(editor, dto) {
    if (!dto || !dto.nodeType && !dto.NodeType) return null;

    const nodeType = dto.nodeType || dto.NodeType;

    if (nodeType === "Literal") {
        return mapLiteralDtoToNode(editor, dto);
    }
    if (nodeType === "Identifier") {
        return mapIdentifierDtoToNode(editor, dto);
    }
    if (nodeType === "MemberAccess") {
        const path = flattenMemberAccessPath(editor, dto);
        if (!path || path.length === 0) return null;
        return createPropertyChainNode(editor, path);
    }
    if (nodeType === "ParenthesizedExpression") {
        const innerDto = dto.inner || dto.Inner;
        const innerNode = innerDto ? mapDslAstDtoToUiAst(editor, innerDto) : null;
        if (!innerNode) return null;
        if (typeof editor.createEncapsulatedFrom === "function") {
            return editor.createEncapsulatedFrom(innerNode);
        }
        const originalExpression = innerNode.toExpression ? innerNode.toExpression() : "";
        return createValueNode(`(${originalExpression})`, innerNode.returnType || "Object", {
            type: "encapsulated",
            innerAST: innerNode,
            displayValue: `(${originalExpression})`,
            originalExpression
        });
    }
    if (nodeType === "BinaryOperation") {
        return mapBinaryOperationDtoToNode(editor, dto);
    }
    if (nodeType === "UnaryOperation") {
        return mapUnaryOperationDtoToNode(editor, dto);
    }
    if (nodeType === "FunctionCall") {
        return mapFunctionCallDtoToNode(editor, dto);
    }
    if (nodeType === "ConditionalExpression" || nodeType === "ArrayAccess" || nodeType === "Block") {
        return null;
    }

    return null;
}

export function mapLiteralDtoToNode(editor, dto) {
    const value = dto.value ?? dto.Value;
    const dataType = (dto.dataType ?? dto.DataType ?? "").toString().toLowerCase();
    let returnType = "Object";
    const isListLiteral = Array.isArray(value) || dataType.includes("list") || dataType.includes("collection");
    const isIntList = dataType.includes("list<int") || dataType.includes("list`1[system.int") || (Array.isArray(value) && value.every(v => Number.isInteger(Number(v))));
    if (isListLiteral) {
        returnType = isIntList ? "List<Int32>" : "List<String>";
    } else if (dataType === "boolean") {
        returnType = "Boolean";
    } else if (dataType === "string") {
        returnType = "String";
    } else {
        const numericTypes = new Set([
            "number",
            "int",
            "int32",
            "int64",
            "long",
            "double",
            "single",
            "float",
            "decimal"
        ]);
        if (numericTypes.has(dataType)) {
            if (typeof value === "number") {
                returnType = Number.isInteger(value) ? "Int32" : "Double";
            } else {
                const raw = String(value ?? "");
                returnType = raw.includes(".") ? "Double" : "Int32";
            }
        }
    }

    const formattedValue = formatLiteralValue(editor, value, returnType);
    const displayValue = (returnType === "List<String>" || returnType === "List<Int32>")
        ? formatListLiteral(value, returnType)
        : (typeof value === "string" ? value : String(value ?? ""));

    return createValueNode(formattedValue, returnType, {
        type: "staticValue",
        staticValue: displayValue,
        isAwaitingInput: false
    });
}

function formatListLiteral(value, returnType) {
    if (!Array.isArray(value)) {
        const raw = String(value ?? "");
        if (raw.startsWith("[") && raw.endsWith("]")) return raw;
        return `[${raw}]`;
    }
    if (returnType === "List<Int32>") {
        return `[${value.map(v => String(parseInt(v, 10))).join(",")}]`;
    }
    return `[${value.map(v => String(v ?? "")).join(",")}]`;
}

export function mapIdentifierDtoToNode(editor, dto) {
    const path = dto.path || dto.Path;
    const name = dto.name || dto.Name;
    const segments = Array.isArray(path) && path.length > 0 ? path : (name ? [name] : []);
    if (segments.length === 0) return null;
    return createPropertyChainNode(editor, segments);
}

export function mapBinaryOperationDtoToNode(editor, dto) {
    const leftDto = dto.left || dto.Left;
    const rightDto = dto.right || dto.Right;
    const opToken = dto.operator || dto.Operator;
    const operator = mapOperatorTokenToSymbol(editor, opToken);
    if (!operator) return null;

    const leftNode = mapDslAstDtoToUiAst(editor, leftDto);
    const rightNode = mapDslAstDtoToUiAst(editor, rightDto);
    if (!leftNode || !rightNode) return null;

    const opNode = createOperatorNode(operator);
    opNode.setLeftOperand(leftNode);
    opNode.setRightOperand(rightNode);
    return opNode;
}

export function mapUnaryOperationDtoToNode(editor, dto) {
    const opToken = dto.operator || dto.Operator;
    const operandDto = dto.operand || dto.Operand;
    const operator = mapOperatorTokenToSymbol(editor, opToken);
    if (!operator || !operandDto) return null;

    if (operator === "-" && (operandDto.nodeType || operandDto.NodeType) === "Literal") {
        const literal = mapLiteralDtoToNode(editor, operandDto);
        if (!literal) return null;
        if (!editor.isNumericType(literal.returnType)) return null;
        const rawValue = literal.metadata?.staticValue;
        const negatedValue = rawValue ? `-${rawValue}` : null;
        if (!negatedValue) return null;
        return createValueNode(negatedValue, literal.returnType, {
            type: "staticValue",
            staticValue: negatedValue,
            isAwaitingInput: false
        });
    }

    return null;
}

export function mapFunctionCallDtoToNode(editor, dto) {
    const name = dto.name || dto.Name;
    if (!name) return null;

    const receiver = dto.receiver || dto.Receiver;
    const isMethodCall = dto.isMethodCall ?? dto.IsMethodCall;
    const lowerName = String(name).toLowerCase();
    if (receiver || isMethodCall) {
        if (['query', 'orderby', 'take', 'distinct', 'select'].includes(lowerName)) {
            return mapListOperationFromMethodCall(editor, lowerName, receiver, dto);
        }
    }

    const eventAccessor = tryMapEventFieldCall(editor, name, dto);
    if (eventAccessor) return eventAccessor;

    const argsDto = dto.arguments || dto.Arguments || [];
    if (!receiver && !isMethodCall && ['query', 'orderby', 'take', 'distinct', 'select'].includes(lowerName)) {
        if (Array.isArray(argsDto) && argsDto.length > 0) {
            const patchedArgs = argsDto.slice(1);
            const patchedDto = {
                ...dto,
                arguments: patchedArgs,
                Arguments: patchedArgs
            };
            return mapListOperationFromMethodCall(editor, lowerName, argsDto[0], patchedDto);
        }
    }

    const args = Array.isArray(argsDto) ? argsDto.map(arg => mapDslAstDtoToUiAst(editor, arg)) : [];
    if (args.some(arg => !arg)) return null;

    const signature = getFunctionSignature(editor, name);
    const returnType = editor.normalizeTypeName(signature?.returnType || signature?.returnTypeName) || "Object";
    const parameters = signature?.parameters || signature?.Parameters || [];
    const normalizedParameters = Array.isArray(parameters) ? [...parameters] : [];
    if (normalizedParameters.length < args.length) {
        for (let i = normalizedParameters.length; i < args.length; i++) {
            normalizedParameters.push({
                name: `arg${i + 1}`,
                typeName: "Object",
                isOptional: true
            });
        }
    }
    const fnNode = createFunctionCallNode(name, returnType, normalizedParameters);
    if (receiver) {
        const receiverNode = mapDslAstDtoToUiAst(editor, receiver);
        if (!receiverNode) return null;
        fnNode.setArg(0, receiverNode);
        args.forEach((arg, idx) => {
            if (arg) fnNode.setArg(idx + 1, arg);
        });
    } else {
        args.forEach((arg, idx) => {
            if (arg) fnNode.setArg(idx, arg);
        });
    }
    return fnNode;
}

export function mapListOperationFromMethodCall(editor, methodName, receiverDto, dto) {
    if (!receiverDto) return null;
    const sourceNode = mapDslAstDtoToUiAst(editor, receiverDto);
    if (!sourceNode) return null;

    const argsDto = dto.arguments || dto.Arguments || [];
    const argNode = Array.isArray(argsDto) && argsDto.length > 0 ? mapDslAstDtoToUiAst(editor, argsDto[0]) : null;
    const resolvedArgNode = argNode?.metadata?.type === "encapsulated"
        ? argNode
        : (argNode ? editor.createEncapsulatedFrom(argNode) : null);

    if (methodName === "query") {
        const queryNode = createListQueryNode(sourceNode);
        if (resolvedArgNode) {
            queryNode.setFilterExpression(resolvedArgNode);
        }
        return queryNode;
    }

    if (methodName === "orderby") {
        const orderings = dto.queryOrderings || dto.QueryOrderings || [];
        const firstOrdering = Array.isArray(orderings) && orderings.length > 0 ? orderings[0] : null;
        const orderingExpr = firstOrdering?.expression || firstOrdering?.Expression || null;
        const orderingDirection = String(firstOrdering?.direction || firstOrdering?.Direction || '').toLowerCase();
        const orderingNode = orderingExpr ? mapDslAstDtoToUiAst(editor, orderingExpr) : null;
        const resolvedOrderingNode = orderingNode?.metadata?.type === "encapsulated"
            ? orderingNode
            : (orderingNode ? editor.createEncapsulatedFrom(orderingNode) : null);
        const sortNode = createListSortNode(sourceNode);
        if (resolvedOrderingNode) {
            sortNode.setSortExpression(resolvedOrderingNode);
        } else if (resolvedArgNode) {
            sortNode.setSortExpression(resolvedArgNode);
        }
        if (orderingDirection) {
            sortNode.setDirection?.(orderingDirection);
            sortNode.direction = orderingDirection === 'desc' ? 'desc' : 'asc';
        }
        return sortNode;
    }

    if (methodName === "take") {
        const strategy = dto.queryTakeStrategy || dto.QueryTakeStrategy || null;
        const countDto = dto.queryTakeCount || dto.QueryTakeCount || null;
        const countNode = countDto ? mapDslAstDtoToUiAst(editor, countDto) : null;
        const resolvedCountNode = countNode?.metadata?.type === "encapsulated"
            ? countNode
            : (countNode ? editor.createEncapsulatedFrom(countNode) : null);
        const takeNode = createListTakeNode(sourceNode);
        if (strategy) {
            takeNode.setStrategy?.(strategy);
            takeNode.strategy = String(strategy || '').toLowerCase();
        }
        if (resolvedCountNode) {
            takeNode.setCountExpression(resolvedCountNode);
        } else if (resolvedArgNode) {
            takeNode.setCountExpression(resolvedArgNode);
        }
        return takeNode;
    }

    if (methodName === "distinct") {
        const distinctNode = createListDistinctNode(sourceNode);
        if (resolvedArgNode) {
            distinctNode.setKeyExpression(resolvedArgNode);
        }
        return distinctNode;
    }

    if (methodName === "select") {
        const selectNode = createListSelectNode(sourceNode);
        if (resolvedArgNode) {
            selectNode.setSelectExpression(resolvedArgNode);
        }
        return selectNode;
    }

    return null;
}

export function tryMapEventFieldCall(editor, name, dto) {
    const accessor = String(name || "");
    const mapReturnType = (fn) => {
        const lower = String(fn || "").toLowerCase();
        if (lower === "eventbool") return "Boolean";
        if (lower === "eventstring") return "String";
        if (lower === "eventguid") return "Guid";
        if (lower === "eventint") return "Int32";
        if (lower === "eventcard") return "CardInstance";
        if (lower === "eventcards") return "List<CardInstance>";
        if (lower === "eventguidlist") return "List<Guid>";
        return null;
    };
    const returnTypeHint = mapReturnType(accessor);
    if (!returnTypeHint) return null;

    const argsDto = dto.arguments || dto.Arguments || [];
    if (!Array.isArray(argsDto) || argsDto.length === 0) return null;

    let arg = argsDto[0];
    let argType = arg?.nodeType || arg?.NodeType;
    if (argType === "ParenthesizedExpression") {
        arg = arg?.inner || arg?.Inner;
        argType = arg?.nodeType || arg?.NodeType;
    }
    if (argType !== "Literal") return null;

    const propertyName = arg.value ?? arg.Value;
    if (typeof propertyName !== "string" || !propertyName) return null;

    const eventMeta = getEventFieldMeta(editor, propertyName, returnTypeHint);
    if (!eventMeta) return null;

    const returnType = editor.normalizeTypeName(eventMeta.returnType || returnTypeHint) || returnTypeHint;
    const displayValue = eventMeta.displayValue || propertyName;
    const fullPath = `context.triggeringEvent.${propertyName}`;

    return createValueNode(fullPath, returnType, {
        type: "property",
        fullPath,
        displayValue,
        eventType: eventMeta.eventType || null,
        propertyName,
        scope: eventMeta.scope || null
    });
}

export function getEventFieldMeta(editor, propertyName, returnTypeHint) {
    const ctx = editor.triggeringEventContext;
    const eventTypes = ctx?.eventTypes || [];
    if (!Array.isArray(eventTypes) || eventTypes.length === 0) {
        return null;
    }

    const selected = eventTypes[0];
    const ev = (editor.metadata?.events || []).find(e =>
        String(e.eventType ?? e.EventType ?? '').toLowerCase() === String(selected).toLowerCase()
    );
    if (!ev) return null;

    const parameters = ev.parameters || ev.Parameters || [];
    const param = parameters.find(p => {
        const name = p.propertyName || p.PropertyName || p.name;
        return String(name || "").toLowerCase() === String(propertyName).toLowerCase();
    });

    if (!param) return null;

    const eventDisplayName = ev.displayName || ev.DisplayName || String(selected);
    const fieldDisplayName = param.displayName || param.DisplayName || propertyName;
    return {
        eventType: selected,
        displayValue: `${eventDisplayName}.${fieldDisplayName}`,
        scope: param.scope || param.Scope || null,
        returnType: editor.normalizeTypeName(param.typeName || param.TypeName || param.type) || returnTypeHint
    };
}

export function getFunctionSignature(editor, name) {
    const functions = editor.metadata?.dsl?.functions || [];
    return functions.find(f => String(f.name ?? f.Name ?? '').toLowerCase() === String(name).toLowerCase());
}

export function mapOperatorTokenToSymbol(editor, token) {
    const value = String(token || "");
    const map = {
        Equal: "==",
        NotEqual: "!=",
        GreaterThan: ">",
        GreaterEqual: ">=",
        LessThan: "<",
        LessEqual: "<=",
        And: "&&",
        Or: "||",
        SetUnion: "|",
        SetIntersect: "&",
        SetDifference: "~",
        Plus: "+",
        Minus: "-",
        Multiply: "*",
        Divide: "/",
        DivideCeil: "\\",
        Modulo: "%"
    };
    return map[value] || null;
}

export function formatLiteralValue(editor, value, returnType) {
    if (value === null || value === undefined) {
        return "null";
    }
    if (returnType === "List<String>" || returnType === "List<Int32>") {
        return formatListLiteral(value, returnType);
    }
    if (returnType === "String") {
        return JSON.stringify(String(value));
    }
    if (returnType === "Boolean") {
        return String(value).toLowerCase();
    }
    if (returnType === "Int32" || returnType === "Double" || returnType === "Number") {
        return String(value);
    }
    return String(value);
}

export function createPropertyChainNode(editor, segments) {
    if (!Array.isArray(segments) || segments.length === 0) return null;
    const resolved = resolvePropertyChain(editor, segments);
    if (!resolved || !Array.isArray(resolved.segments) || resolved.segments.length === 0) return null;

    const rootName = resolved.segments[0];
    let node = createValueNode(rootName, resolved.context?.typeName || "Object", {
        type: "context",
        name: rootName,
        displayName: resolved.context?.displayName || rootName
    });

    (resolved.steps || []).forEach((step) => {
        node = createValueNode(step.path, step.typeName || "Object", {
            type: "property",
            displayName: step.displayName || step.segment,
            fullPath: step.path,
            enumType: step.enumType || null,
            enumValues: Array.isArray(step.enumValues) ? step.enumValues : null
        });
    });

    return node;
}

export function flattenMemberAccessPath(editor, dto) {
    const nodeType = dto?.nodeType || dto?.NodeType;
    if (nodeType === "MemberAccess") {
        const left = flattenMemberAccessPath(editor, dto.target || dto.Target);
        const right = flattenMemberAccessPath(editor, dto.member || dto.Member);
        if (!left || !right) return null;
        return [...left, ...right];
    }
    if (nodeType === "Identifier") {
        const path = dto.path || dto.Path;
        const name = dto.name || dto.Name;
        if (Array.isArray(path) && path.length > 0) return path;
        if (name) return [name];
    }
    return null;
}
