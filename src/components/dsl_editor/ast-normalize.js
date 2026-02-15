import { getSemanticNodeType as getCoreSemanticNodeType } from "./core.js";
import { resolvePropertyChain } from "./property-chain-resolver.js";
import { resolveScopedSelfContextFromSource } from "./event-translation.js";

function withScopedSelfContext(editor, sourceNode, callback) {
    if (typeof callback !== "function") return;
    if (!editor || !sourceNode) {
        callback();
        return;
    }

    const sourceType = getCoreSemanticNodeType(sourceNode);
    const scopedContext = resolveScopedSelfContextFromSource(editor, sourceNode, sourceType);
    if (!scopedContext) {
        callback();
        return;
    }

    const previousOverride = editor.contextOverride;
    editor.contextOverride = {
        ...(previousOverride || {}),
        ...scopedContext
    };
    try {
        callback();
    } finally {
        editor.contextOverride = previousOverride;
    }
}

function resolveExpectedTypeForValueNode(editor, node) {
    if (!editor || !node) return null;

    const normalizedFallback = editor.normalizeTypeName(editor.slotExpectedType || editor.expectedType);
    const parent = node.parent;
    if (!parent || typeof editor.getExpectedTypeForSlot !== "function") {
        return normalizedFallback;
    }

    if (parent.type === "operator") {
        const slot = parent.leftOperand === node
            ? "left"
            : parent.rightOperand === node
                ? "right"
                : null;
        return slot ? editor.normalizeTypeName(editor.getExpectedTypeForSlot(parent, slot)) : normalizedFallback;
    }

    if (parent.type === "functionCall") {
        const index = (parent.args || []).indexOf(node);
        return editor.normalizeTypeName(editor.getExpectedTypeForSlot(parent, "arg", index));
    }

    if (parent.type === "listQuery" && parent.filterExpression === node) {
        return editor.normalizeTypeName(editor.getExpectedTypeForSlot(parent, "filterExpression"));
    }

    if (parent.type === "listSort" && parent.sortExpression === node) {
        return editor.normalizeTypeName(editor.getExpectedTypeForSlot(parent, "sortExpression"));
    }

    if (parent.type === "listTake" && parent.countExpression === node) {
        return editor.normalizeTypeName(editor.getExpectedTypeForSlot(parent, "countExpression"));
    }

    if (parent.type === "listDistinct" && parent.keyExpression === node) {
        return editor.normalizeTypeName(editor.getExpectedTypeForSlot(parent, "keyExpression"));
    }
    if (parent.type === "listSelect" && parent.selectExpression === node) {
        return editor.normalizeTypeName(editor.getExpectedTypeForSlot(parent, "selectExpression"));
    }

    return normalizedFallback;
}

function isEnumLiteralValue(editor, expectedType, raw) {
    if (!expectedType || typeof editor?.isEnumType !== "function") return false;
    const normalized = editor.normalizeTypeName(expectedType);
    if (!normalized || !editor.isEnumType(normalized)) return false;
    const value = typeof editor.normalizeEnumToken === "function"
        ? editor.normalizeEnumToken(raw)
        : String(raw ?? "").trim().replace(/^['"]|['"]$/g, "");
    if (!value) return false;
    const values = typeof editor.getEnumValues === "function" ? editor.getEnumValues(normalized) : [];
    if (!Array.isArray(values) || values.length === 0) return false;
    if (values.some(v => String(v).toLowerCase() === value.toLowerCase())) return true;
    if (typeof editor.resolveEnumValueFromInput === "function") {
        const resolved = editor.resolveEnumValueFromInput(normalized, value);
        return values.some(v => String(v).toLowerCase() === String(resolved).toLowerCase());
    }
    return false;
}

export function prepareEditorForRecommendations(editor) {
    if (!editor?.workspaceAST) return;
    finalizeDraftInputs(editor, editor.workspaceAST);
    refreshValueNodeReturnTypes(editor, editor.workspaceAST);
    refreshCompleteness(editor.workspaceAST);
}

export function finalizeDraftInputs(editor, node) {
    if (!node) return;

    if (node.metadata?.type === "encapsulated" && node.metadata?.innerAST) {
        finalizeDraftInputs(editor, node.metadata.innerAST);
    }

    if (node.type === "value" && node.metadata) {
        if (node.metadata.type === "staticValue" && node.metadata.isAwaitingInput) {
            const raw = String(node.metadata.staticValue || "").trim();
            if (raw) {
                const expectedType = resolveExpectedTypeForValueNode(editor, node);
                const useEnumType = isEnumLiteralValue(editor, expectedType, raw);
                const normalizedEnumType = useEnumType ? editor.normalizeTypeName(expectedType) : null;
                const inferredType = useEnumType ? normalizedEnumType : editor.inferStaticValueType(raw);
                let formattedValue = null;
                if (useEnumType) {
                    const canonical = typeof editor.resolveEnumValueFromInput === "function"
                        ? editor.resolveEnumValueFromInput(normalizedEnumType, raw)
                        : raw;
                    formattedValue = typeof editor.formatEnumLiteralValue === "function"
                        ? editor.formatEnumLiteralValue(canonical)
                        : JSON.stringify(String(canonical));
                    node.metadata.enumType = normalizedEnumType;
                } else {
                    formattedValue = editor.formatStaticValue(raw, inferredType);
                }
                node.value = formattedValue;
                node.returnType = inferredType;
                node.metadata.isAwaitingInput = false;
                node.updateCompleteness();
                if (node.parent) {
                    node.parent.updateCompleteness();
                }
            }
        }

        if (node.metadata.type === "propertyInput" && node.metadata.isAwaitingInput) {
            const key = String(node.metadata.key || "").trim();
            if (key) {
                const basePath = node.metadata.basePath || "";
                node.value = basePath ? `${basePath}.${key}` : key;
                node.returnType = editor.normalizeTypeName(node.metadata.returnType) || node.returnType;
                node.metadata.isAwaitingInput = false;
                node.updateCompleteness();
                if (node.parent) {
                    node.parent.updateCompleteness();
                }
            }
        }
    }

    if (node.type === "operator") {
        finalizeDraftInputs(editor, node.leftOperand);
        finalizeDraftInputs(editor, node.rightOperand);
    } else if (node.type === "functionCall") {
        (node.args || []).forEach((arg) => finalizeDraftInputs(editor, arg));
    } else if (node.type === "listQuery") {
        finalizeDraftInputs(editor, node.source);
        finalizeDraftInputs(editor, node.filterExpression);
    } else if (node.type === "listSort") {
        finalizeDraftInputs(editor, node.source);
        finalizeDraftInputs(editor, node.sortExpression);
    } else if (node.type === "listTake") {
        finalizeDraftInputs(editor, node.source);
        finalizeDraftInputs(editor, node.countExpression);
    } else if (node.type === "listDistinct") {
        finalizeDraftInputs(editor, node.source);
        finalizeDraftInputs(editor, node.keyExpression);
    } else if (node.type === "listSelect") {
        finalizeDraftInputs(editor, node.source);
        finalizeDraftInputs(editor, node.selectExpression);
    }
}

export function refreshValueNodeReturnTypes(editor, node) {
    if (!node) return;

    if (node.metadata?.type === "encapsulated" && node.metadata?.innerAST) {
        refreshValueNodeReturnTypes(editor, node.metadata.innerAST);
        coerceEncapsulatedToEnum(editor, node);
    }

    if (node.type === "value") {
        const refreshed = inferValueNodeReturnType(editor, node);
        applyRefreshedValueType(editor, node, refreshed);
        if (node.metadata?.type === "staticValue") {
            coerceStaticValueToEnum(editor, node);
        }
    }

    if (node.type === "operator") {
        refreshValueNodeReturnTypes(editor, node.leftOperand);
        refreshValueNodeReturnTypes(editor, node.rightOperand);
    } else if (node.type === "functionCall") {
        (node.args || []).forEach((arg) => refreshValueNodeReturnTypes(editor, arg));
        const semanticType = getCoreSemanticNodeType(node);
        if (semanticType) {
            node.returnType = semanticType;
        }
    } else if (node.type === "listQuery") {
        refreshValueNodeReturnTypes(editor, node.source);
        withScopedSelfContext(editor, node.source, () => {
            refreshValueNodeReturnTypes(editor, node.filterExpression);
        });
        const sourceType = getCoreSemanticNodeType(node.source);
        if (sourceType) {
            node.returnType = sourceType;
        }
    } else if (node.type === "listSort") {
        refreshValueNodeReturnTypes(editor, node.source);
        withScopedSelfContext(editor, node.source, () => {
            refreshValueNodeReturnTypes(editor, node.sortExpression);
        });
        const sourceType = getCoreSemanticNodeType(node.source);
        if (sourceType) {
            node.returnType = sourceType;
        }
    } else if (node.type === "listTake") {
        refreshValueNodeReturnTypes(editor, node.source);
        refreshValueNodeReturnTypes(editor, node.countExpression);
        const sourceType = getCoreSemanticNodeType(node.source);
        if (sourceType) {
            node.returnType = sourceType;
        }
    } else if (node.type === "listDistinct") {
        refreshValueNodeReturnTypes(editor, node.source);
        withScopedSelfContext(editor, node.source, () => {
            refreshValueNodeReturnTypes(editor, node.keyExpression);
        });
        const sourceType = getCoreSemanticNodeType(node.source);
        if (sourceType) {
            node.returnType = sourceType;
        }
    } else if (node.type === "listSelect") {
        refreshValueNodeReturnTypes(editor, node.source);
        withScopedSelfContext(editor, node.source, () => {
            refreshValueNodeReturnTypes(editor, node.selectExpression);
        });
        const sourceType = getCoreSemanticNodeType(node.source);
        const selectedType = getCoreSemanticNodeType(node.selectExpression);
        if (sourceType && typeof editor.isCollectionType === "function" && editor.isCollectionType(sourceType)) {
            const inner = selectedType || "Object";
            node.returnType = `List<${inner}>`;
        } else if (selectedType) {
            node.returnType = selectedType;
        } else if (sourceType) {
            node.returnType = sourceType;
        }
    }
}

function inferValueNodeReturnType(editor, node) {
    if (!node || node.type !== "value") return null;
    const metaType = node.metadata?.type;

    if (metaType === "encapsulated" && node.metadata?.innerAST) {
        const inner = node.metadata.innerAST;
        return getCoreSemanticNodeType(inner);
    }

    if (metaType === "staticValue") {
        const raw = String(node.metadata?.staticValue || "").trim();
        if (raw) {
            const expectedType = resolveExpectedTypeForValueNode(editor, node);
            if (isEnumLiteralValue(editor, expectedType, raw)) {
                return editor.normalizeTypeName(expectedType);
            }
            return editor.inferStaticValueType(raw);
        }
    }

    if (metaType === "enumValue") {
        return editor.normalizeTypeName(node.metadata?.enumType) || node.returnType;
    }

    if (metaType === "propertyInput") {
        return editor.normalizeTypeName(node.metadata?.returnType) || node.returnType;
    }

    if (metaType === "context") {
        const ctxName = node.metadata?.name || node.value;
        const ctx = editor.getDslContextObject(ctxName);
        return editor.normalizeTypeName(ctx?.typeName) || node.returnType;
    }

    const path = node.metadata?.fullPath || node.value;
    if (typeof path === "string") {
        const inferredPathType = inferPropertyChainReturnType(editor, path);
        if (inferredPathType) {
            return inferredPathType;
        }
    }

    if (metaType === "eventField") {
        return editor.normalizeTypeName(node.metadata?.returnType) || node.returnType;
    }

    return node.returnType;
}

function coerceStaticValueToEnum(editor, node) {
    if (!editor || !node || node.metadata?.type !== "staticValue") return;
    const raw = String(node.metadata?.staticValue || "").trim();
    if (!raw) return;
    const expectedType = resolveExpectedTypeForValueNode(editor, node);
    if (!isEnumLiteralValue(editor, expectedType, raw)) return;

    const normalized = editor.normalizeTypeName(expectedType);
    const token = typeof editor.normalizeEnumToken === "function"
        ? editor.normalizeEnumToken(raw)
        : raw.replace(/^['"]|['"]$/g, "");
    const canonical = typeof editor.resolveEnumValueFromInput === "function"
        ? editor.resolveEnumValueFromInput(normalized, token)
        : token;
    const displayValue = typeof editor.getEnumLabelForValue === "function"
        ? editor.getEnumLabelForValue(normalized, canonical)
        : null;
    node.returnType = normalized || node.returnType;
    node.value = typeof editor.formatEnumLiteralValue === "function"
        ? editor.formatEnumLiteralValue(canonical)
        : JSON.stringify(String(canonical));
    node.metadata = {
        ...(node.metadata || {}),
        type: "enumValue",
        enumType: normalized,
        enumValue: canonical,
        displayValue: displayValue || token || canonical
    };
    delete node.metadata.staticValue;
    delete node.metadata.isAwaitingInput;
}

function coerceEncapsulatedToEnum(editor, node) {
    if (!editor || !node || node.metadata?.type !== "encapsulated") return;
    const inner = node.metadata?.innerAST;
    if (!inner || inner.type !== "value" || inner.metadata?.type !== "staticValue") return;

    const expectedType = resolveExpectedTypeForValueNode(editor, node);
    if (!isEnumLiteralValue(editor, expectedType, inner.metadata?.staticValue)) return;

    const normalized = editor.normalizeTypeName(expectedType);
    const raw = String(inner.metadata?.staticValue || "").trim();
    const token = typeof editor.normalizeEnumToken === "function"
        ? editor.normalizeEnumToken(raw)
        : raw.replace(/^['"]|['"]$/g, "");
    const canonical = typeof editor.resolveEnumValueFromInput === "function"
        ? editor.resolveEnumValueFromInput(normalized, token)
        : token;
    const displayValue = typeof editor.getEnumLabelForValue === "function"
        ? editor.getEnumLabelForValue(normalized, canonical)
        : null;
    inner.returnType = normalized || inner.returnType;
    inner.value = typeof editor.formatEnumLiteralValue === "function"
        ? editor.formatEnumLiteralValue(canonical)
        : JSON.stringify(String(canonical));
    inner.metadata = {
        ...(inner.metadata || {}),
        type: "enumValue",
        enumType: normalized,
        enumValue: canonical,
        displayValue: displayValue || token || canonical
    };
    delete inner.metadata.staticValue;
    delete inner.metadata.isAwaitingInput;
    node.returnType = normalized || node.returnType;
}

function inferPropertyChainReturnType(editor, path) {
    const resolved = resolvePropertyChain(editor, path);
    return resolved?.finalType || null;
}

function applyRefreshedValueType(editor, node, refreshedType) {
    const normalizedRefreshed = normalizeTypeName(editor, refreshedType);
    if (!normalizedRefreshed) {
        return;
    }

    const normalizedExisting = normalizeTypeName(editor, node?.returnType);
    if (isFallbackType(normalizedRefreshed)
        && normalizedExisting
        && !isFallbackType(normalizedExisting)) {
        return;
    }

    node.returnType = normalizedRefreshed;
}

function normalizeTypeName(editor, typeName) {
    if (!typeName) {
        return null;
    }

    if (typeof editor?.normalizeTypeName === "function") {
        return editor.normalizeTypeName(typeName) || String(typeName);
    }

    return String(typeName);
}

function isFallbackType(typeName) {
    if (!typeName) {
        return true;
    }

    const compact = String(typeName).replace(/\s+/g, "");
    return compact === "Object"
        || compact === "Void"
        || compact === "List<Object>"
        || compact === "IEnumerable<Object>"
        || compact === "IReadOnlyList<Object>"
        || compact === "IReadOnlyCollection<Object>"
        || compact === "ICollection<Object>";
}

export function refreshCompleteness(node) {
    if (!node) return;

    if (node.metadata?.type === "encapsulated" && node.metadata?.innerAST) {
        refreshCompleteness(node.metadata.innerAST);
    }

    if (node.type === "operator") {
        refreshCompleteness(node.leftOperand);
        refreshCompleteness(node.rightOperand);
    } else if (node.type === "functionCall") {
        (node.args || []).forEach((arg) => refreshCompleteness(arg));
    } else if (node.type === "listQuery") {
        refreshCompleteness(node.source);
        refreshCompleteness(node.filterExpression);
    } else if (node.type === "listSort") {
        refreshCompleteness(node.source);
        refreshCompleteness(node.sortExpression);
    } else if (node.type === "listTake") {
        refreshCompleteness(node.source);
        refreshCompleteness(node.countExpression);
    } else if (node.type === "listDistinct") {
        refreshCompleteness(node.source);
        refreshCompleteness(node.keyExpression);
    } else if (node.type === "listSelect") {
        refreshCompleteness(node.source);
        refreshCompleteness(node.selectExpression);
    }

    if (typeof node.updateCompleteness === "function") {
        node.updateCompleteness();
    }
}
