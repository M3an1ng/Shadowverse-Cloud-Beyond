import { getUserSession } from './userSession.js';

function storageKey() {
    const session = getUserSession();
    return 'svb.replay.seats.' + (session.userId || session.guestId);
}

export function readReplayTickets() {
    try {
        const rows = JSON.parse(localStorage.getItem(storageKey()) || '[]');
        return Array.isArray(rows) ? rows.filter(r => typeof r?.ticket === 'string' && typeof r.gameId === 'string').slice(0, 256) : [];
    } catch { return []; }
}

export function rememberReplaySeat(state) {
    const ticket = state?.ReplayTicket ?? state?.replayTicket;
    const gameId = state?.GameId ?? state?.gameId;
    if (!ticket || !gameId) return;
    const rows = readReplayTickets().filter(row => row.gameId !== gameId);
    rows.unshift({ gameId, ticket });
    try { localStorage.setItem(storageKey(), JSON.stringify(rows.slice(0, 256))); } catch { /* Storage can be disabled. */ }
}
