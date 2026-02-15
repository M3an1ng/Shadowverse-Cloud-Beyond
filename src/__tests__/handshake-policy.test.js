import { getRouteNameFromHash, shouldReuseCachedHandshake } from "../handshakePolicy.js";

describe("handshake reconnect policy", () => {
    test("reuses standard pvp handshake on game route refresh", () => {
        const cached = {
            gameId: "g1",
            playerId: "p1",
            token: "t1",
            useAiFallback: false,
            dualControl: false,
            role: "host"
        };

        const shouldReuse = shouldReuseCachedHandshake(cached, {
            routeName: "game",
            isInMultiplayerUi: false
        });

        expect(shouldReuse).toBe(true);
    });

    test("reuses cached handshake in multiplayer panel", () => {
        const cached = {
            gameId: "g1",
            useAiFallback: false,
            dualControl: false,
            role: "guest"
        };

        const shouldReuse = shouldReuseCachedHandshake(cached, {
            routeName: "multiplayer",
            isInMultiplayerUi: true
        });

        expect(shouldReuse).toBe(true);
    });

    test("does not reuse plain pvp handshake outside game route", () => {
        const cached = {
            gameId: "g1",
            useAiFallback: false,
            dualControl: false,
            role: "host"
        };

        const shouldReuse = shouldReuseCachedHandshake(cached, {
            routeName: "navigation",
            isInMultiplayerUi: false
        });

        expect(shouldReuse).toBe(false);
    });

    test("normalizes route from hash", () => {
        expect(getRouteNameFromHash("#game")).toBe("game");
        expect(getRouteNameFromHash("  #multiplayer  ")).toBe("multiplayer");
        expect(getRouteNameFromHash("")).toBe("");
    });
});
