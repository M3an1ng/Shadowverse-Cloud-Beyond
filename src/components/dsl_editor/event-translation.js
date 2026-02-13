export function getEventTypeHint(editor, segments) {
    if (!Array.isArray(segments) || segments.length < 2) {
        return null;
    }

    const first = String(segments[0] || '').toLowerCase();
    const second = String(segments[1] || '').toLowerCase();

    if (first === 'context' && second === 'triggeringevent') {
        const eventTypes = editor?.triggeringEventContext?.eventTypes || [];
        if (!Array.isArray(eventTypes) || eventTypes.length !== 1) {
            return null;
        }
        return eventTypes[0];
    }

    if (first === 'self' && editor?.contextOverride?.eventType) {
        return editor.contextOverride.eventType;
    }

    return null;
}

export function resolveEventTypeHintFromSource(editor, sourceNode) {
    return resolveByTypeHint(editor, sourceNode, new WeakSet());
}

function resolveByTypeHint(editor, node, visited) {
    if (!node || (typeof node !== "object" && typeof node !== "function")) {
        return null;
    }

    if (visited.has(node)) {
        return null;
    }
    visited.add(node);

    if (node.metadata?.type === "encapsulated" && node.metadata?.innerAST) {
        return resolveByTypeHint(editor, node.metadata.innerAST, visited);
    }

    if (node.type === "listQuery" || node.type === "listSort" || node.type === "listTake"
        || node.type === "listDistinct" || node.type === "listSelect") {
        return resolveByTypeHint(editor, node.source, visited);
    }

    if (node.type === "functionCall") {
        const name = String(node.functionName || "").toLowerCase();
        if (name === "bytype") {
            const arg = Array.isArray(node.args) ? node.args[1] : null;
            return resolveCanonicalEventType(editor, readDeterministicLiteral(arg));
        }
        return null;
    }

    return null;
}

function readDeterministicLiteral(node) {
    if (!node) {
        return null;
    }

    if (node.metadata?.type === "encapsulated" && node.metadata?.innerAST) {
        return readDeterministicLiteral(node.metadata.innerAST);
    }

    if (node.type !== "value") {
        return null;
    }

    if (node.metadata?.type === "enumValue") {
        return node.metadata.enumValue ?? node.metadata.staticValue ?? node.value ?? null;
    }

    if (node.metadata?.type === "staticValue") {
        return node.metadata.staticValue ?? node.value ?? null;
    }

    if (typeof node.value === "string") {
        return node.value;
    }

    return null;
}

function normalizeEventTypeToken(raw) {
    if (raw === null || raw === undefined) {
        return "";
    }

    let token = String(raw).trim();
    if (!token) {
        return "";
    }

    token = token.replace(/^['"]|['"]$/g, "");
    if (token.toLowerCase().startsWith("eventtype.")) {
        token = token.slice("eventType.".length);
    }

    return token.trim();
}

function resolveCanonicalEventType(editor, rawToken) {
    const token = normalizeEventTypeToken(rawToken);
    if (!token) {
        return null;
    }

    const eventTypes = Array.isArray(editor?.metadata?.events)
        ? editor.metadata.events
            .map(e => e?.eventType ?? e?.EventType ?? "")
            .filter(Boolean)
        : [];

    const findCanonical = (values) => values.find(v =>
        String(v || "").toLowerCase() === token.toLowerCase()
    ) || null;

    const fromEvents = findCanonical(eventTypes);
    if (fromEvents) {
        return fromEvents;
    }

    const enumValues = typeof editor?.getEnumValues === "function"
        ? editor.getEnumValues("EventType")
        : [];
    const fromEnum = findCanonical(Array.isArray(enumValues) ? enumValues : []);
    if (fromEnum) {
        return fromEnum;
    }

    return null;
}

export function resolveEventParameterMeta(editor, propertyName, eventType) {
    const normalizedProperty = String(propertyName || '').trim().toLowerCase();
    if (!normalizedProperty) {
        return null;
    }

    const events = editor?.metadata?.events || [];
    const normalizeEventType = (value) => String(value ?? '').trim().toLowerCase();
    const findEvent = (type) => events.find(e =>
        normalizeEventType(e.eventType ?? e.EventType ?? '') === normalizeEventType(type)
    );

    if (eventType) {
        const target = findEvent(eventType);
        const direct = findEventParameter(target, normalizedProperty);
        if (direct) {
            return direct;
        }
    }

    const ctxEventTypes = Array.isArray(editor?.triggeringEventContext?.eventTypes)
        ? editor.triggeringEventContext.eventTypes.filter(Boolean)
        : [];
    if (ctxEventTypes.length === 1) {
        const scopedMatches = [];
        const target = findEvent(ctxEventTypes[0]);
        const param = findEventParameter(target, normalizedProperty);
        if (param) {
            scopedMatches.push(param);
        }
        if (scopedMatches.length === 1) {
            return scopedMatches[0];
        }
        if (scopedMatches.length > 1) {
            const typeNames = new Set(scopedMatches.map(m => String(m.typeName || '').toLowerCase()));
            if (typeNames.size === 1) {
                return scopedMatches[0];
            }
            const displayNames = new Set(scopedMatches.map(m => String(m.displayName || '').toLowerCase()));
            if (displayNames.size === 1) {
                return scopedMatches[0];
            }
        }
    }

    const matches = [];
    for (const ev of events) {
        const param = findEventParameter(ev, normalizedProperty);
        if (param) {
            matches.push(param);
        }
    }

    if (matches.length === 1) {
        return matches[0];
    }

    if (matches.length > 1) {
        const uniqueNames = new Set(matches.map(m => String(m.displayName || '').toLowerCase()));
        if (uniqueNames.size === 1) {
            return matches[0];
        }
    }

    return null;
}

function findEventParameter(eventMeta, normalizedProperty) {
    if (!eventMeta) {
        return null;
    }

    const parameters = eventMeta.parameters || eventMeta.Parameters || [];
    const param = parameters.find(p =>
        String(p.propertyName || p.PropertyName || '').toLowerCase() === normalizedProperty
    );
    if (!param) {
        return null;
    }

    return {
        displayName: param.displayName || param.DisplayName || param.propertyName || param.PropertyName,
        typeName: param.typeName || param.TypeName || null
    };
}
