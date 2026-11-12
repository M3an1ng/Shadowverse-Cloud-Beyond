// src/battleExitConfirmPolicy.js - guards for confirming battle exit actions

const BATTLE_ROUTE = 'game';
const MAIN_MENU_ROUTE = 'navigation';
const BATTLE_EXIT_BUTTON_CLASS = 'back-nav-btn';
const PRE_BATTLE_PHASES = new Set(['', 'connecting', 'initialization', 'initializing', 'mulligan', 'gameover']);

export const BATTLE_EXIT_CONFIRM_MESSAGE = '对战已经开始，是否投降并返回主菜单？';
export const MIRAGE_8P_EXIT_CONFIRM_MESSAGE = '当前幻境对局仍在进行中，退出将按当前名次直接退赛淘汰（不影响其他选手继续对局），是否确认退赛？';
export const SPECTATOR_EXIT_CONFIRM_MESSAGE = '是否退出房间？';

export function isSpectatorBattle(battleState, handshake = null) {
    return battleState?.isSpectator === true
        || handshake?.role === 'spectator'
        || handshake?.role === 'eliminated-spectator';
}

export function battleExitConfirmMessage(battleState, handshake = null) {
    if (isSpectatorBattle(battleState, handshake)) {
        return SPECTATOR_EXIT_CONFIRM_MESSAGE;
    }
    if (handshake?.kind === 'mirage-8p') {
        return MIRAGE_8P_EXIT_CONFIRM_MESSAGE;
    }
    return BATTLE_EXIT_CONFIRM_MESSAGE;
}

export function isLeavingBattleRoute(currentRoute, nextRoute) {
    if (currentRoute !== BATTLE_ROUTE) return false;
    return nextRoute !== BATTLE_ROUTE;
}

export function isStartedBattleState(battleState) {
    if (!battleState || battleState.gameOver === true) return false;
    const phase = String(battleState.phase ?? battleState.gamePhase ?? '').trim().toLowerCase();
    return !PRE_BATTLE_PHASES.has(phase);
}

export function shouldConfirmBattleExit(currentRoute, nextRoute, routeButton, battleState = null, handshake = null) {
    if (!isLeavingBattleRoute(currentRoute, nextRoute)) return false;
    if (handshake?.kind === 'mirage-8p') {
        if (isSpectatorBattle(battleState, handshake)) return true;
        return isStartedBattleState(battleState);
    }
    if (nextRoute !== MAIN_MENU_ROUTE) return false;
    if (!routeButton?.classList || typeof routeButton.classList.contains !== 'function') return false;
    if (!routeButton.classList.contains(BATTLE_EXIT_BUTTON_CLASS)) return false;
    return isSpectatorBattle(battleState, handshake) || isStartedBattleState(battleState);
}
