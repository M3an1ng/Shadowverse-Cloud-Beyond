import {
    createValueNode,
    createOperatorNode,
    createFunctionCallNode,
    createListQueryNode,
    createListSortNode,
    createListTakeNode
} from "./core.js";

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
    if (dataType === "boolean") {
        returnType = "Boolean";
    } else if (dataType === "string") {
        returnType = "String";
    } else if (dataType === "number") {
        if (typeof value === "number" && Number.isInteger(value)) {
            returnType = "Int32";
        } else {
            returnType = "Double";
        }
    }

    const formattedValue = formatLiteralValue(editor, value, returnType);
    const displayValue = typeof value === "string" ? value : String(value ?? "");

    return createValueNode(formattedValue, returnType, {
        type: "staticValue",
        staticValue: displayValue,
        isAwaitingInput: false
    });
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
    if (receiver || isMethodCall) {
        const lowerName = String(name).toLowerCase();
        if (['query', 'orderby', 'take'].includes(lowerName)) {
            return mapListOperationFromMethodCall(editor, lowerName, receiver, dto);
        }
    }

    const eventAccessor = tryMapEventFieldCall(editor, name, dto);
    if (eventAccessor) return eventAccessor;

    const argsDto = dto.arguments || dto.Arguments || [];
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
    args.forEach((arg, idx) => {
        if (arg) fnNode.setArg(idx, arg);
    });
    return fnNode;
}

export function mapListOperationFromMethodCall(editor, methodName, receiverDto, dto) {
    if (!receiverDto) return null;
    const sourceNode = mapDslAstDtoToUiAst(editor, receiverDto);
    if (!sourceNode) return null;

    const argsDto = dto.arguments || dto.Arguments || [];
    const argNode = Array.isArray(argsDto) && argsDto.length > 0 ? mapDslAstDtoToUiAst(editor, argsDto[0]) : null;

    if (methodName === "query") {
        const queryNode = createListQueryNode(sourceNode);
        if (argNode) {
            queryNode.setFilterExpression(editor.createEncapsulatedFrom(argNode));
        }
        return queryNode;
    }

    if (methodName === "orderby") {
        const sortNode = createListSortNode(sourceNode);
        if (argNode) {
            sortNode.setSortExpression(editor.createEncapsulatedFrom(argNode));
        }
        return sortNode;
    }

    if (methodName === "take") {
        const takeNode = createListTakeNode(sourceNode);
        if (argNode) {
            takeNode.setCountExpression(editor.createEncapsulatedFrom(argNode));
        }
        return takeNode;
    }

    return null;
}

export function tryMapEventFieldCall(editor, name, dto) {
    const accessor = String(name || "");
    const mapReturnType = (fn) => {
        if (fn === "eventBool") return "Boolean";
        if (fn === "eventString") return "String";
        if (fn === "eventGuid") return "Guid";
        if (fn === "eventInt") return "Int32";
        return null;
    };
    const returnType = mapReturnType(accessor);
    if (!returnType) return null;

    const argsDto = dto.arguments || dto.Arguments || [];
    if (!Array.isArray(argsDto) || argsDto.length === 0) return null;

    const arg = argsDto[0];
    const argType = arg?.nodeType || arg?.NodeType;
    if (argType !== "Literal") return null;

    const propertyName = arg.value ?? arg.Value;
    if (typeof propertyName !== "string" || !propertyName) return null;

    const eventMeta = getEventFieldMeta(editor, propertyName, returnType);
    const displayValue = eventMeta?.displayValue || propertyName;
    const expression = `${accessor}(\"${propertyName}\")`;

    return createValueNode(expression, returnType, {
        type: "eventField",
        displayValue,
        eventType: eventMeta?.eventType || null,
        propertyName,
        scope: eventMeta?.scope || null
    });
}

export function getEventFieldMeta(editor, propertyName, returnType) {
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
        returnType
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
        Plus: "+",
        Minus: "-",
        Multiply: "*",
        Divide: "/"
    };
    return map[value] || null;
}

export function formatLiteralValue(editor, value, returnType) {
    if (value === null || value === undefined) {
        return "null";
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
    const ctx = editor.getDslContextObject(segments[0]);
    let currentType = editor.normalizeTypeName(ctx?.typeName) || "Object";
    let currentPath = segments[0];
    let node = createValueNode(currentPath, currentType, {
        type: "context",
        name: segments[0],
        displayName: ctx?.displayName || segments[0]
    });

    for (let i = 1; i < segments.length; i++) {
        const seg = segments[i];
        currentPath = `${currentPath}.${seg}`;
        const tm = editor.getTypeMetadata(currentType);
        const prop = (tm?.properties || []).find(p =>
            String(p.name || "").toLowerCase() === String(seg).toLowerCase()
        );
        currentType = editor.normalizeTypeName(prop?.typeName) || "Object";
        node = createValueNode(currentPath, currentType, {
            type: "property",
            displayName: prop?.displayName || seg,
            fullPath: currentPath
        });
    }

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
