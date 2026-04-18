import {
    BATTLE_EXIT_CONFIRM_MESSAGE,
    isLeavingBattleRoute,
    shouldConfirmBattleExit
} from "../battleExitConfirmPolicy.js";

describe("battle exit confirm policy", () => {
    test("requires confirmation only for game page back-nav button to navigation", () => {
        const button = document.createElement("button");
        button.classList.add("back-nav-btn");

        expect(shouldConfirmBattleExit("game", "navigation", button)).toBe(true);
        expect(shouldConfirmBattleExit("game", "card-editor", button)).toBe(false);
        expect(shouldConfirmBattleExit("card-editor", "navigation", button)).toBe(false);
    });

    test("does not require confirmation for non back-nav route buttons", () => {
        const button = document.createElement("button");
        button.classList.add("tool-button");
        expect(shouldConfirmBattleExit("game", "navigation", button)).toBe(false);
    });

    test("treats any game -> non-game route as leaving battle", () => {
        expect(isLeavingBattleRoute("game", "navigation")).toBe(true);
        expect(isLeavingBattleRoute("game", "card-editor")).toBe(true);
        expect(isLeavingBattleRoute("game", "game")).toBe(false);
        expect(isLeavingBattleRoute("navigation", "game")).toBe(false);
    });

    test("exports non-empty confirmation message", () => {
        expect(typeof BATTLE_EXIT_CONFIRM_MESSAGE).toBe("string");
        expect(BATTLE_EXIT_CONFIRM_MESSAGE.length).toBeGreaterThan(0);
    });
});
