// src/roomExitDecisionPolicy.js - policy helpers for deferred room leave decisions

export function isRoomSeatAlreadyReleased(error) {
    if (error?.status === 504) return false;
    if (error?.status === 403 || error?.status === 404) return true;
    if (error instanceof TypeError) return false;
    return /房间(?:不存在|已不存在|已过期|已失效|已关闭|已解散|已结束)|不在(?:该)?房间|座位已失效|已被.*(?:踢出|移出)|无权限|token\s*无效|not found|already closed|(?:room|seat|token).*expired|^expired$|not.*participant|not in (?:the )?room|forbidden/i.test(error?.message || '');
}

export function isNonAiMultiplayerHandshake(handshake) {
    if (!handshake) return false;
    if (handshake.kind === 'mirage-8p') return false;
    if (handshake.role === 'spectator' || handshake.role === 'eliminated-spectator') return false;
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

export function shouldDeferRoomLeaveForDecision(fromRoute, toRoute, handshake, options = {}) {
    if (!isMatchToNavigationTransition(fromRoute, toRoute)) {
        return false;
    }

    if (!isNonAiMultiplayerHandshake(handshake)) {
        return false;
    }

    // Once a match is already over, leaving the game page should immediately
    // release the room seat instead of keeping a deferred reconnect decision.
    if (options.surrenderSubmitted === true) {
        return false;
    }

    return options.gameOver !== true;
}

export function canShowRematchRoomButton(gameState, handshake) {
    if (gameState?.gameOver !== true) return false;
    if (gameState?.isSpectator === true) return false;
    if (!isNonAiMultiplayerHandshake(handshake)) return false;
    if (handshake.dualControl === true) return false;
    if (handshake.isTakeTwo === true) return false;
    return handshake.canRematch === true;
}

export function resolveRematchPollStartedAction(room, rematchExitedActiveGameId) {
    if (!room || room.status !== "Started") {
        return "none";
    }

    const activeGameId = String(room.activeGameId || "").trim();
    const exitedGameId = String(rematchExitedActiveGameId || "").trim();
    if (exitedGameId && activeGameId && activeGameId === exitedGameId) {
        return room.canRematch === false ? "ended-no-rematch" : "wait-reset";
    }

    if (room.canRematch === false && exitedGameId) {
        return "ended-no-rematch";
    }

    return "enter-game";
}
