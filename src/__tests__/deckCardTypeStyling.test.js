import {
    buildDeckCardRootClassName,
    getDeckCardTypeClass
} from "../deckCardTypeStyling.js";
import {
    buildDeckPreviewSnapshot,
    createDeckPreviewModal
} from "../components/deckPreviewModal.js";

describe("deck card type styling helpers", () => {
    test.each([
        ["Follower", "type-follower"],
        ["spell", "type-spell"],
        ["Amulet", "type-amulet"],
        ["crest", ""],
        ["faith", ""],
        ["", ""],
        [null, ""]
    ])("maps %p to %p", (value, expected) => {
        expect(getDeckCardTypeClass(value)).toBe(expected);
    });

    test("builds a library card class list with art rarity and type classes", () => {
        expect(buildDeckCardRootClassName("library-card", {
            hasArt: true,
            rarityClass: "rarity-gold",
            type: "Spell"
        })).toBe("library-card has-art rarity-gold type-spell");
    });

    test("omits unknown type classes while keeping other classes", () => {
        expect(buildDeckCardRootClassName("deck-preview-card", {
            hasArt: false,
            rarityClass: "rarity-bronze",
            type: "Leader"
        })).toBe("deck-preview-card rarity-bronze");
    });

    test("deck preview cards expose type classes on the root card element", () => {
        document.body.innerHTML = `
            <div id="preview-modal" class="deck-preview-modal" aria-hidden="true">
                <div class="deck-preview-dialog">
                    <div id="preview-title"></div>
                    <div id="preview-meta"></div>
                    <div id="preview-curve"></div>
                    <div id="preview-toolbar-stats"></div>
                    <div id="preview-counts"></div>
                    <div id="preview-cards"></div>
                    <button id="preview-export-btn" type="button">Export</button>
                    <button id="preview-close-btn" type="button">Close</button>
                </div>
            </div>
        `;
        const modal = createDeckPreviewModal({
            idPrefix: "preview",
            onClose: jest.fn(),
            onExport: jest.fn(),
            onCardClick: jest.fn()
        });

        modal.open({
            titleText: "Type Preview",
            metaText: "Open Rune",
            cards: buildDeckPreviewSnapshot([
                { cardDefinitionId: 101, count: 1, cost: 2, name: "Mystic Burst", type: "Spell", rarity: "bronze" },
                { cardDefinitionId: 102, count: 1, cost: 4, name: "Holy Relic", type: "Amulet", rarity: "silver" }
            ]).items,
            stats: {}
        });

        const cards = Array.from(document.querySelectorAll(".deck-preview-card"));
        expect(cards).toHaveLength(2);
        expect(cards[0].className).toContain("type-spell");
        expect(cards[1].className).toContain("type-amulet");
    });
});
