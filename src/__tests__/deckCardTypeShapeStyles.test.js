import fs from "fs";
import path from "path";

function getDeckBuilderCss() {
    const cssPath = path.resolve(__dirname, "../styles/deck-builder.css");
    return fs.readFileSync(cssPath, "utf8");
}

function normalizeCss(css) {
    return css.replace(/\s+/g, " ").trim();
}

describe("deck card type shape styles", () => {
    test("uses shape differences instead of type colors for cost badges", () => {
        const css = normalizeCss(getDeckBuilderCss());

        expect(css).not.toContain("--db-type-follower");
        expect(css).not.toContain("--db-type-spell");
        expect(css).not.toContain("--db-type-amulet");

        expect(css).toContain(".library-card.type-follower .card-cost, .deck-card-mini.type-follower .mini-cost, .deck-preview-card.type-follower .deck-preview-card-cost { border-radius: 0;");
        expect(css).toContain(".library-card.type-spell .card-cost, .deck-card-mini.type-spell .mini-cost, .deck-preview-card.type-spell .deck-preview-card-cost { border-radius: 50%; clip-path: none;");
        expect(css).toContain(".library-card.type-amulet .card-cost, .deck-card-mini.type-amulet .mini-cost, .deck-preview-card.type-amulet .deck-preview-card-cost { border-radius: 0; clip-path: polygon(50% 0%, 100% 50%, 50% 100%, 0% 50%);");
    });
});
