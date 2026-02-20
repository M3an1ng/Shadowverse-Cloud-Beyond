import {
    BATTLE_EXIT_CONFIRM_MESSAGE,
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

    test("exports non-empty confirmation message", () => {
        expect(typeof BATTLE_EXIT_CONFIRM_MESSAGE).toBe("string");
        expect(BATTLE_EXIT_CONFIRM_MESSAGE.length).toBeGreaterThan(0);
    });
});
