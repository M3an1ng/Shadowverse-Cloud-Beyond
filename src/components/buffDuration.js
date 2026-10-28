export function normalizeDurationType(value) {
    if (value == null || value === '') return 'Permanent';
    if (typeof value === 'number') {
        return mapDurationTypeValue(value);
    }
    const raw = String(value).trim();
    if (raw === '') return 'Permanent';
    if (/^\d+$/.test(raw)) {
        return mapDurationTypeValue(Number(raw));
    }
    const lowered = raw.toLowerCase();
    if (lowered === 'permanent') return 'Permanent';
    if (lowered === 'endofturn') return 'EndOfTurn';
    if (lowered === 'startofturn') return 'StartOfTurn';
    if (lowered === 'ownerturnstart') return 'OwnerTurnStart';
    if (lowered === 'ownerturnend') return 'OwnerTurnEnd';
    if (lowered === 'opponentturnstart') return 'OpponentTurnStart';
    if (lowered === 'opponentturnend') return 'OpponentTurnEnd';
    if (lowered === 'conditionalremoval') return 'ConditionalRemoval';
    return raw;
}

function mapDurationTypeValue(value) {
    if (value === 1) return 'EndOfTurn';
    if (value === 2) return 'StartOfTurn';
    if (value === 3) return 'OwnerTurnStart';
    if (value === 4) return 'OwnerTurnEnd';
    if (value === 5) return 'OpponentTurnStart';
    if (value === 6) return 'OpponentTurnEnd';
    if (value === 7) return 'ConditionalRemoval';
    return 'Permanent';
}
