export const INITIAL_BATTLE_CONNECT_MS = 20_000;
export const BATTLE_RECOVERY_MS = 120_000;
const ATTEMPT_TIMEOUT_MS = 12_000;

export class BattleConnectionRejected extends Error {}

function waitForRetry(delay, signal) {
    if (delay <= 0) return Promise.resolve();

    return new Promise((resolve, reject) => {
        const finish = () => {
            clearTimeout(timer);
            window.removeEventListener('online', finish);
            document.removeEventListener('visibilitychange', onVisible);
            signal.removeEventListener('abort', finish);
            if (signal.aborted) reject(signal.reason);
            else resolve();
        };
        const onVisible = () => {
            if (!document.hidden && navigator.onLine !== false) finish();
        };
        const timer = setTimeout(finish, delay);
        window.addEventListener('online', finish);
        document.addEventListener('visibilitychange', onVisible);
        signal.addEventListener('abort', finish, { once: true });
    });
}

export async function retryBattleConnection(connect, { signal, budgetMs }) {
    const deadline = Date.now() + budgetMs;
    let lastError;
    let failures = 0;
    let delay = 0;

    while (Date.now() < deadline) {
        signal.throwIfAborted();
        const remaining = deadline - Date.now();
        await waitForRetry(navigator.onLine === false ? remaining : Math.min(delay, remaining), signal);
        signal.throwIfAborted();

        const attemptTimeout = Math.min(ATTEMPT_TIMEOUT_MS, deadline - Date.now());
        if (attemptTimeout <= 0) break;
        if (navigator.onLine === false) continue;

        try {
            return await connect(attemptTimeout);
        } catch (error) {
            signal.throwIfAborted();
            if (error instanceof BattleConnectionRejected) throw error;
            lastError = error;
            const backoff = Math.min(8_000, 750 * 2 ** failures++);
            delay = backoff * (0.8 + Math.random() * 0.4);
        }
    }

    throw lastError ?? new Error('等待网络恢复超时');
}

// SignalR's handshake can finish before the server has accepted the seat and sent its board.
export async function waitForBattleConnection(connection, snapshot, { signal, timeoutMs }) {
    signal.throwIfAborted();
    let timer;
    let onAbort;
    const interrupted = new Promise((_, reject) => {
        onAbort = () => reject(signal.reason);
        signal.addEventListener('abort', onAbort, { once: true });
        timer = setTimeout(() => reject(new Error('等待对局状态超时')), timeoutMs);
    });

    try {
        await Promise.race([Promise.all([snapshot, connection.start()]), interrupted]);
    } finally {
        clearTimeout(timer);
        signal.removeEventListener('abort', onAbort);
    }
}
