import { resolveArtUrl } from "../cardArtCache.js";

describe("card art cache URL resolution", () => {
    test("normalizes relative card asset paths", () => {
        expect(resolveArtUrl("assets/cards/sample card.webp")).toBe("/assets/cards/sample%20card.webp");
        expect(resolveArtUrl("cards/sample.webp")).toBe("/cards/sample.webp");
    });

    test("keeps absolute and data image URLs", () => {
        expect(resolveArtUrl("/assets/cards/sample.webp")).toBe("/assets/cards/sample.webp");
        expect(resolveArtUrl("https://example.com/card.webp")).toBe("https://example.com/card.webp");
        expect(resolveArtUrl("data:image/png;base64,abc")).toBe("data:image/png;base64,abc");
    });
});
