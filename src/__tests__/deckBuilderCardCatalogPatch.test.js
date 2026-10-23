import {
    applyDeckBuilderCardCatalogPatch,
    canApplyDeckBuilderCardCatalogPatch,
    shouldSkipDeckBuilderCatalogReload
} from "../deckBuilderCardCatalogPatch.js";

describe("deck builder card catalog patching", () => {
    test("updates cached card definitions without replacing the full catalog", () => {
        const state = {
            rawCardsCache: [
                { id: 1, name: "Old One", isCollectible: true, rarity: "Bronze", type: "Follower" },
                { id: 2, name: "Keep Me", isCollectible: true, rarity: "Silver", type: "Spell" }
            ],
            rawCardIndex: new Map([[1, { id: 1, name: "Old One" }], [2, { id: 2, name: "Keep Me" }]]),
            availableCards: [
                { id: 1, name: "Old One", isCollectible: true, rarity: "Bronze", type: "Follower" },
                { id: 2, name: "Keep Me", isCollectible: true, rarity: "Silver", type: "Spell" }
            ],
            cardIndex: new Map([[1, { id: 1, name: "Old One" }], [2, { id: 2, name: "Keep Me" }]])
        };

        const result = applyDeckBuilderCardCatalogPatch(state, {
            changedCards: [
                { id: 1, name: "New One", isCollectible: true, rarity: "Gold", type: "Follower" }
            ]
        });

        expect(result.changedIds).toEqual([1]);
        expect(state.rawCardsCache.map(card => card.name)).toEqual(["New One", "Keep Me"]);
        expect(state.rawCardIndex.get(1).name).toBe("New One");
        expect(state.cardIndex.get(1).name).toBe("New One");
    });

    test("removes deleted cards from all local indexes", () => {
        const state = {
            rawCardsCache: [
                { id: 1, name: "Delete Me", isCollectible: true, rarity: "Bronze", type: "Follower" },
                { id: 2, name: "Keep Me", isCollectible: true, rarity: "Silver", type: "Spell" }
            ],
            rawCardIndex: new Map([[1, { id: 1, name: "Delete Me" }], [2, { id: 2, name: "Keep Me" }]]),
            availableCards: [
                { id: 1, name: "Delete Me", isCollectible: true, rarity: "Bronze", type: "Follower" },
                { id: 2, name: "Keep Me", isCollectible: true, rarity: "Silver", type: "Spell" }
            ],
            cardIndex: new Map([[1, { id: 1, name: "Delete Me" }], [2, { id: 2, name: "Keep Me" }]])
        };

        const result = applyDeckBuilderCardCatalogPatch(state, {
            deletedCardIds: [1]
        });

        expect(result.deletedIds).toEqual([1]);
        expect(state.rawCardsCache.map(card => card.id)).toEqual([2]);
        expect(state.rawCardIndex.has(1)).toBe(false);
        expect(state.cardIndex.has(1)).toBe(false);
    });

    test("allows incremental patches only when a local full catalog already exists", () => {
        expect(canApplyDeckBuilderCardCatalogPatch({
            rawCardsCache: [{ id: 1 }],
            rawCardIndex: new Map()
        }, {
            changedCards: [{ id: 1 }]
        })).toBe(true);

        expect(canApplyDeckBuilderCardCatalogPatch({
            rawCardsCache: null,
            rawCardIndex: new Map()
        }, {
            changedCards: [{ id: 1 }]
        })).toBe(false);
    });

    test("skips full catalog reload for ability-only card revision events", () => {
        expect(shouldSkipDeckBuilderCatalogReload({
            reason: "ability-updated",
            changedCardIds: [1]
        })).toBe(true);

        expect(shouldSkipDeckBuilderCatalogReload({
            reason: "card-updated",
            changedCardIds: [1]
        })).toBe(false);
    });
});
