// src/roomExitDecisionPolicy.js - policy helpers for deferred room leave decisions

export function isNonAiMultiplayerHandshake(handshake) {
    if (!handshake) return false;
    if (handshake.useAiFallback === true) return false;

    const gameId = String(handshake.gameId || "").trim();
    const playerId = String(handshake.playerId || "").trim();
    const token = String(handshake.token || "").trim();

    return gameId.length > 0 && playerId.length > 0 && token.length > 0;
}

export function isMatchToNavigationTransition(fromRoute, toRoute) {
    const from = String(fromRoute || "").trim();
    const to = String(toRoute || "").trim();
    return (from === "game" || from === "multiplayer") && to === "navigation";
}

export function shouldDeferRoomLeaveForDecision(fromRoute, toRoute, handshake) {
    return isMatchToNavigationTransition(fromRoute, toRoute)
        && isNonAiMultiplayerHandshake(handshake);
}
