// src/handshakePolicy.js - reconnect/cache handshake policy helpers

export function getRouteNameFromHash(hashValue) {
    const raw = String(hashValue || '').trim();
    if (!raw) return '';
    return raw.replace(/^#/, '').trim();
}

export function shouldReuseCachedHandshake(cachedHandshake, context = {}) {
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
