// src/handshakePolicy.js - reconnect/cache handshake policy helpers

export function getRouteNameFromHash(hashValue) {
    const raw = String(hashValue || '').trim();
    if (!raw) return '';
    return raw.replace(/^#/, '').trim();
}

export function isClassicRoomHandshake(handshake) {
    return Boolean(handshake?.gameId && handshake.playerId && handshake.token
        && (handshake.role === 'host' || handshake.role === 'guest')
        && !handshake.kind && !handshake.isMirage && !handshake.isTakeTwo
        && !handshake.useAiFallback && !handshake.dualControl);
}

export function shouldReuseCachedHandshake(cachedHandshake, context = {}) {
    if (cachedHandshake?.kind === 'mirage-8p') {
        const hasRoom = Boolean(cachedHandshake.roomId);
        if (!hasRoom) return false;
        const routeName = String(context.routeName || '').trim();
        return routeName === 'game' || routeName === 'mirage-lobby' || cachedHandshake.role === 'spectator';
    }

    if (!cachedHandshake?.gameId) {
        return false;
    }

    const routeName = String(context.routeName || '').trim();
    const isInMultiplayerUi = context.isInMultiplayerUi === true;
    const isGameRoute = routeName === 'game';

    if (isGameRoute || isInMultiplayerUi) {
        return true;
    }

    return cachedHandshake.useAiFallback === true
        || cachedHandshake.dualControl === true
        || cachedHandshake.role === 'spectator';
}
