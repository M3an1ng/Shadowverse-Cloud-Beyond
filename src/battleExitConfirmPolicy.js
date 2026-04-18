// src/battleExitConfirmPolicy.js - guards for confirming battle exit actions

const BATTLE_ROUTE = 'game';
const MAIN_MENU_ROUTE = 'navigation';
const BATTLE_EXIT_BUTTON_CLASS = 'back-nav-btn';

export const BATTLE_EXIT_CONFIRM_MESSAGE = '确认退出当前对战并返回主菜单吗？';

export function isLeavingBattleRoute(currentRoute, nextRoute) {
    if (currentRoute !== BATTLE_ROUTE) return false;
    return nextRoute !== BATTLE_ROUTE;
}

export function shouldConfirmBattleExit(currentRoute, nextRoute, routeButton) {
    if (!isLeavingBattleRoute(currentRoute, nextRoute)) return false;
    if (nextRoute !== MAIN_MENU_ROUTE) return false;
    if (!routeButton?.classList || typeof routeButton.classList.contains !== 'function') return false;
    return routeButton.classList.contains(BATTLE_EXIT_BUTTON_CLASS);
}
