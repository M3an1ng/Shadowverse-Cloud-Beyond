import {
    BATTLE_EXIT_CONFIRM_MESSAGE,
    isLeavingBattleRoute,
    isStartedBattleState,
    shouldConfirmBattleExit
} from "../battleExitConfirmPolicy.js";

describe("battle exit confirm policy", () => {
    test("requires confirmation only for game page back-nav button to navigation", () => {
        const button = document.createElement("button");
        button.classList.add("back-nav-btn");

        const activeBattle = { phase: "Main", gameOver: false };

        expect(shouldConfirmBattleExit("game", "navigation", button, activeBattle)).toBe(true);
        expect(shouldConfirmBattleExit("game", "card-editor", button, activeBattle)).toBe(false);
        expect(shouldConfirmBattleExit("card-editor", "navigation", button, activeBattle)).toBe(false);
    });

    test("does not require confirmation for non back-nav route buttons", () => {
        const button = document.createElement("button");
        button.classList.add("tool-button");
        expect(shouldConfirmBattleExit("game", "navigation", button, { phase: "Main", gameOver: false })).toBe(false);
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
        expect(BATTLE_EXIT_CONFIRM_MESSAGE).toContain("投降");
    });

    test("requires started and unfinished battle state", () => {
        const button = document.createElement("button");
        button.classList.add("back-nav-btn");

        expect(isStartedBattleState({ phase: "Mulligan", gameOver: false })).toBe(false);
        expect(isStartedBattleState({ phase: "Main", gameOver: false })).toBe(true);
        expect(isStartedBattleState({ phase: "GameOver", gameOver: true })).toBe(false);
        expect(shouldConfirmBattleExit("game", "navigation", button, { phase: "Mulligan", gameOver: false })).toBe(false);
    });
});
