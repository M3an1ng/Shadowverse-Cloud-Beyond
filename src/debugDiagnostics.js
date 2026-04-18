const connectionDebugIds = new WeakMap();
let nextConnectionDebugId = 1;

function debugDetailsReplacer(_key, value) {
    if (value instanceof Error) {
        return {
            name: value.name,
            message: value.message,
            stack: value.stack
        };
    }

    if (typeof value === 'bigint') {
        return String(value);
    }

    return value;
}

export function formatDebugDetails(details) {
    if (details === null || details === undefined) {
        return '';
    }

    if (typeof details === 'string') {
        return details;
    }

    try {
        return JSON.stringify(details, debugDetailsReplacer);
    } catch (error) {
        return `[unserializable:${error instanceof Error ? error.message : String(error)}]`;
    }
}

export function buildDebugLogLine(scope, message, details = null) {
    const base = `[PERF_DEBUG][${scope}] ${message}`;
    const formattedDetails = formatDebugDetails(details);
    return formattedDetails ? `${base} ${formattedDetails}` : base;
}

export function getConnectionDebugId(connection, kind = 'game') {
    if (!connection || (typeof connection !== 'object' && typeof connection !== 'function')) {
        return `${kind}-unknown`;
    }

    const existing = connectionDebugIds.get(connection);
    if (existing) {
        return existing;
    }

    const assigned = `${kind}-${nextConnectionDebugId++}`;
    connectionDebugIds.set(connection, assigned);
    return assigned;
}

export function createHandDragPerfSession(sessionId, startedAt) {
    return {
        sessionId: Number(sessionId) || 0,
        startedAt: Number(startedAt) || 0,
        pointerMoveCount: 0,
        lastPointerMoveAt: 0,
        frameCount: 0,
        rafDelaySum: 0,
        maxRafDelayMs: 0,
        lagSum: 0,
        maxLagPx: 0,
        slowFrameCount: 0,
        severeFrameCount: 0,
        maxPointerMoveAgeMs: 0
    };
}

export function recordHandDragPerfFrame(session, sample = {}) {
    const next = session ? { ...session } : createHandDragPerfSession(0, 0);
    const rafDelayMs = Number(sample.rafDelayMs) || 0;
    const lagPx = Number(sample.lagPx) || 0;
    const pointerMoveAgeMs = Number(sample.pointerMoveAgeMs) || 0;

    next.frameCount += 1;
    next.rafDelaySum += rafDelayMs;
    next.lagSum += lagPx;
    next.maxRafDelayMs = Math.max(next.maxRafDelayMs, rafDelayMs);
    next.maxLagPx = Math.max(next.maxLagPx, lagPx);
    next.maxPointerMoveAgeMs = Math.max(next.maxPointerMoveAgeMs, pointerMoveAgeMs);

    if (rafDelayMs >= 16.7) {
        next.slowFrameCount += 1;
    }
    if (rafDelayMs >= 33.4) {
        next.severeFrameCount += 1;
    }

    return next;
}

function roundToOneDecimal(value) {
    return Number((Number(value) || 0).toFixed(1));
}

export function summarizeHandDragPerfSession(session, endedAt) {
    const safeSession = session ? { ...session } : createHandDragPerfSession(0, 0);
    const frameCount = Math.max(0, Number(safeSession.frameCount) || 0);
    const durationMs = Math.max(0, (Number(endedAt) || 0) - (Number(safeSession.startedAt) || 0));
    const avgRafDelayMs = frameCount > 0 ? safeSession.rafDelaySum / frameCount : 0;
    const avgLagPx = frameCount > 0 ? safeSession.lagSum / frameCount : 0;

    return {
        sessionId: safeSession.sessionId,
        durationMs: roundToOneDecimal(durationMs),
        pointerMoveCount: Number(safeSession.pointerMoveCount) || 0,
        frameCount,
        avgRafDelayMs: roundToOneDecimal(avgRafDelayMs),
        maxRafDelayMs: roundToOneDecimal(safeSession.maxRafDelayMs),
        avgLagPx: roundToOneDecimal(avgLagPx),
        maxLagPx: roundToOneDecimal(safeSession.maxLagPx),
        slowFrameCount: Number(safeSession.slowFrameCount) || 0,
        severeFrameCount: Number(safeSession.severeFrameCount) || 0,
        maxPointerMoveAgeMs: roundToOneDecimal(safeSession.maxPointerMoveAgeMs)
    };
}
