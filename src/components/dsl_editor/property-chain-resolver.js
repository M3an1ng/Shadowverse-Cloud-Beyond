import { getEventTypeHint, resolveEventParameterMeta } from "./event-translation.js";

function normalizeTypeName(editor, typeName) {
    if (!typeName) return null;
    if (typeof editor?.normalizeTypeName === "function") {
        return editor.normalizeTypeName(typeName) || typeName;
    }
    return typeName;
}

function resolveDictionaryValueType(editor, typeName) {
    const normalized = normalizeTypeName(editor, typeName);
    if (!normalized) return null;

    if (typeof editor?.isDictionaryType === "function" && editor.isDictionaryType(normalized)) {
        if (typeof editor.getDictionaryValueType === "function") {
            const valueType = editor.getDictionaryValueType(normalized);
            return normalizeTypeName(editor, valueType) || valueType;
        }
        const match = String(normalized).match(/<(.*)>/);
        if (!match) return "Object";
        const parts = match[1].split(",").map(part => part.trim());
        return parts[1] || "Object";
    }

    return null;
}

function resolveTriggeringEventOutputType(editor, outputName) {
    if (!outputName) return null;
    const outputVariables = Array.isArray(editor?.triggeringEventContext?.outputVariables)
        ? editor.triggeringEventContext.outputVariables
        : [];
    if (outputVariables.length === 0) return null;

    const normalizedName = String(outputName).trim().toLowerCase();
    const matched = outputVariables.find(entry =>
        String(entry?.name || "").trim().toLowerCase() === normalizedName
    );
    if (!matched) return null;
    return normalizeTypeName(editor, matched.typeName || "Object") || "Object";
}

function isTriggeringEventOutputsPath(pathSegments, index) {
    if (!Array.isArray(pathSegments) || index < 3) return false;
    const s0 = String(pathSegments[0] || "").toLowerCase();
    const s1 = String(pathSegments[1] || "").toLowerCase();
    const s2 = String(pathSegments[2] || "").toLowerCase();
    return s0 === "context" && s1 === "triggeringevent" && s2 === "outputs";
}

function resolveEnumMeta(editor, typeName) {
    const normalized = normalizeTypeName(editor, typeName);
    if (!normalized) {
        return { enumType: null, enumValues: null };
    }

    const isEnumType = typeof editor?.isEnumType === "function" && editor.isEnumType(normalized);
    if (!isEnumType) {
        return { enumType: null, enumValues: null };
    }

    const enumValues = typeof editor?.getEnumValues === "function"
        ? editor.getEnumValues(normalized)
        : null;

    return {
        enumType: normalized,
        enumValues: Array.isArray(enumValues) ? enumValues : null
    };
}

export function toPropertyPathSegments(pathOrSegments) {
    if (Array.isArray(pathOrSegments)) {
        return pathOrSegments.filter(Boolean).map(seg => String(seg));
    }
    return String(pathOrSegments || "").split(".").filter(Boolean);
}

export function resolvePropertyChain(editor, pathOrSegments) {
    const segments = toPropertyPathSegments(pathOrSegments);
    if (segments.length === 0) return null;

    const rootName = segments[0];
    const contextMeta = typeof editor?.getDslContextObject === "function"
        ? editor.getDslContextObject(rootName)
        : null;
    let currentType = normalizeTypeName(editor, contextMeta?.typeName) || "Object";
    let currentPath = rootName;
    const eventTypeHint = getEventTypeHint(editor, segments);
    const steps = [];

    for (let i = 1; i < segments.length; i++) {
        const segment = segments[i];
        currentPath = `${currentPath}.${segment}`;

        let resolvedType = null;
        let displayName = segment;
        let source = "property";

        if (isTriggeringEventOutputsPath(segments, i)) {
            const outputType = resolveTriggeringEventOutputType(editor, segment);
            if (outputType) {
                resolvedType = outputType;
                source = "triggeringEventOutput";
            }
        }

        if (!resolvedType) {
            const dictValueType = resolveDictionaryValueType(editor, currentType);
            if (dictValueType) {
                resolvedType = normalizeTypeName(editor, dictValueType) || "Object";
                source = "dictionary";
            }
        }

        if (!resolvedType) {
            const normalizedCurrentType = normalizeTypeName(editor, currentType);
            const isEventType = normalizedCurrentType === "IGameEvent" || normalizedCurrentType === "GameEventBase";
            if (isEventType) {
                const eventParam = resolveEventParameterMeta(editor, segment, eventTypeHint);
                if (eventParam) {
                    resolvedType = normalizeTypeName(editor, eventParam.typeName) || "Object";
                    displayName = eventParam.displayName || segment;
                    source = "eventParam";
                }
            }
        }

        if (!resolvedType) {
            const typeMeta = typeof editor?.getTypeMetadata === "function"
                ? editor.getTypeMetadata(currentType)
                : null;
            const prop = (typeMeta?.properties || []).find(p =>
                String(p.name || "").toLowerCase() === String(segment || "").toLowerCase()
            );
            resolvedType = normalizeTypeName(editor, prop?.typeName) || "Object";
            displayName = prop?.displayName || segment;
            source = "property";
        }

        currentType = resolvedType || "Object";
        const enumMeta = resolveEnumMeta(editor, currentType);

        steps.push({
            index: i,
            segment,
            path: currentPath,
            typeName: currentType,
            displayName,
            source,
            enumType: enumMeta.enumType,
            enumValues: enumMeta.enumValues
        });
    }

    return {
        segments,
        context: {
            name: rootName,
            displayName: contextMeta?.displayName || rootName,
            typeName: normalizeTypeName(editor, contextMeta?.typeName) || "Object"
        },
        steps,
        finalType: currentType || null
    };
}
