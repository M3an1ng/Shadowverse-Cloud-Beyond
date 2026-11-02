// src/battleExitConfirmPolicy.js - guards for confirming battle exit actions

const BATTLE_ROUTE = 'game';
const MAIN_MENU_ROUTE = 'navigation';
const BATTLE_EXIT_BUTTON_CLASS = 'back-nav-btn';
const PRE_BATTLE_PHASES = new Set(['', 'connecting', 'initialization', 'initializing', 'mulligan', 'gameover']);

export const BATTLE_EXIT_CONFIRM_MESSAGE = '对战已经开始，是否投降并返回主菜单？';
export const SPECTATOR_EXIT_CONFIRM_MESSAGE = '是否退出房间？';

export function isSpectatorBattle(battleState, handshake = null) {
    return battleState?.isSpectator === true || handshake?.role === 'spectator';
}

export function battleExitConfirmMessage(battleState, handshake = null) {
    return isSpectatorBattle(battleState, handshake)
        ? SPECTATOR_EXIT_CONFIRM_MESSAGE
        : BATTLE_EXIT_CONFIRM_MESSAGE;
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
    if (nextRoute !== MAIN_MENU_ROUTE) return false;
    if (!routeButton?.classList || typeof routeButton.classList.contains !== 'function') return false;
    if (!routeButton.classList.contains(BATTLE_EXIT_BUTTON_CLASS)) return false;
    return isSpectatorBattle(battleState, handshake) || isStartedBattleState(battleState);
}
