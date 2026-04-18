import {
    createDefaultDeckBuilderFilters,
    resetDeckBuilderFilters
} from "../deckBuilderFilters.js";

describe("deck builder filters", () => {
    test("creates empty default filters", () => {
        expect(createDefaultDeckBuilderFilters()).toEqual({
            cost: null,
            class: "",
            type: "",
            rarity: "",
            set: "",
            search: ""
        });
    });

    test("resets persisted filters when leaving deck builder", () => {
        const filters = {
            cost: 3,
            class: "forestcraft",
            type: "spell",
            rarity: "gold",
            set: "heroes",
            search: "fairy"
        };

        resetDeckBuilderFilters(filters);

        expect(filters).toEqual(createDefaultDeckBuilderFilters());
    });
});
