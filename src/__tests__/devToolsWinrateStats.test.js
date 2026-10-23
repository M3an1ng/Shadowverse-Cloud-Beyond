import {
    copyWinrateDeckCode,
    getWinrateDeckCode
} from "../devToolsWinrateStats.js";

describe("winrate deck code export", () => {
    test("returns only a non-empty deck code from the composition response", () => {
        expect(getWinrateDeckCode({ deckCode: "  SVB2abc  " })).toBe("SVB2abc");
        expect(getWinrateDeckCode({ DeckCode: "SVB2xyz" })).toBe("SVB2xyz");
        expect(getWinrateDeckCode({ deckCode: "   " })).toBe("");
        expect(getWinrateDeckCode(null)).toBe("");
    });

    test("copies the deck code through the provided clipboard", async () => {
        const clipboard = {
            writeText: jest.fn().mockResolvedValue(undefined)
        };

        await copyWinrateDeckCode("SVB2abc", clipboard);

        expect(clipboard.writeText).toHaveBeenCalledWith("SVB2abc");
    });
});
