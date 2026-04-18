import {
    isMatchToNavigationTransition,
    isNonAiMultiplayerHandshake,
    shouldDeferRoomLeaveForDecision
} from "../roomExitDecisionPolicy.js";

describe("room exit decision policy", () => {
    test("detects match-to-navigation transition", () => {
        expect(isMatchToNavigationTransition("game", "navigation")).toBe(true);
        expect(isMatchToNavigationTransition("multiplayer", "navigation")).toBe(true);
        expect(isMatchToNavigationTransition("navigation", "game")).toBe(false);
    });

    test("accepts standard pvp handshake", () => {
        const handshake = {
            gameId: "g1",
            playerId: "p1",
            token: "t1",
            useAiFallback: false
        };
        expect(isNonAiMultiplayerHandshake(handshake)).toBe(true);
    });

    test("rejects ai handshake and invalid room handshake", () => {
        expect(isNonAiMultiplayerHandshake({
            gameId: "g1",
            playerId: "p1",
            token: "t1",
            useAiFallback: true
        })).toBe(false);

        expect(isNonAiMultiplayerHandshake({
            gameId: "g1",
            playerId: "",
            token: "t1",
            useAiFallback: false
        })).toBe(false);
    });

    test("defers room leave only for qualified transition and handshake", () => {
        const handshake = {
            gameId: "g1",
            playerId: "p1",
            token: "t1",
            useAiFallback: false
        };
        expect(shouldDeferRoomLeaveForDecision("game", "navigation", handshake)).toBe(true);
        expect(shouldDeferRoomLeaveForDecision("game", "card-editor", handshake)).toBe(false);
    });

    test("does not defer room leave after game over", () => {
        const handshake = {
            gameId: "g1",
            playerId: "p1",
            token: "t1",
            useAiFallback: false
        };
        expect(shouldDeferRoomLeaveForDecision("game", "navigation", handshake, { gameOver: true })).toBe(false);
        expect(shouldDeferRoomLeaveForDecision("game", "navigation", handshake, { gameOver: false })).toBe(true);
    });
});
