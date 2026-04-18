import {
    buildDebugLogLine,
    createHandDragPerfSession,
    getConnectionDebugId,
    recordHandDragPerfFrame,
    summarizeHandDragPerfSession
} from '../src/debugDiagnostics.js';

describe('debugDiagnostics', () => {
    test('buildDebugLogLine flattens object details into a single exportable string', () => {
        const line = buildDebugLogLine('hand-drag', 'ghost update frame', {
            sessionId: 3,
            rafDelayMs: 24.5,
            menuStarrySkyAnimating: false
        });

        expect(line).toContain('[PERF_DEBUG][hand-drag] ghost update frame');
        expect(line).toContain('"sessionId":3');
        expect(line).toContain('"rafDelayMs":24.5');
        expect(line).not.toContain('[object Object]');
    });

    test('getConnectionDebugId stays stable per connection and unique across connections', () => {
        const gameConn = {};
        const otherGameConn = {};

        const first = getConnectionDebugId(gameConn, 'game');
        const second = getConnectionDebugId(gameConn, 'game');
        const third = getConnectionDebugId(otherGameConn, 'game');

        expect(first).toBe(second);
        expect(first).toMatch(/^game-\d+$/);
        expect(third).toMatch(/^game-\d+$/);
        expect(third).not.toBe(first);
    });

    test('hand drag perf session summarizes sustained lag instead of only the first frame', () => {
        let session = createHandDragPerfSession(9, 100);
        session.pointerMoveCount = 6;
        session.lastPointerMoveAt = 148;

        session = recordHandDragPerfFrame(session, {
            rafDelayMs: 12,
            lagPx: 10,
            pointerMoveAgeMs: 4
        });
        session = recordHandDragPerfFrame(session, {
            rafDelayMs: 29,
            lagPx: 42,
            pointerMoveAgeMs: 12
        });
        session = recordHandDragPerfFrame(session, {
            rafDelayMs: 18,
            lagPx: 22,
            pointerMoveAgeMs: 6
        });

        const summary = summarizeHandDragPerfSession(session, 160);

        expect(summary).toMatchObject({
            sessionId: 9,
            frameCount: 3,
            pointerMoveCount: 6,
            durationMs: 60,
            avgRafDelayMs: 19.7,
            maxRafDelayMs: 29,
            avgLagPx: 24.7,
            maxLagPx: 42,
            slowFrameCount: 2,
            severeFrameCount: 0,
            maxPointerMoveAgeMs: 12
        });
    });
});
