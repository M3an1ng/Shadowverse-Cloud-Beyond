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
