import {
    CARD_CATALOG_INVALIDATED_EVENT,
    isAbilityOnlyInvalidation,
    syncDataRevisionFromResponse
} from "../dataChangeEvents.js";

function createResponseWithRevision(revision) {
    return {
        headers: {
            get(name) {
                return name === "X-Data-Revision" ? revision : "";
            }
        }
    };
}

describe("data change events", () => {
    test("identifies ability-only invalidations", () => {
        expect(isAbilityOnlyInvalidation({
            reason: "ability-updated",
            changedCardIds: [42]
        })).toBe(true);

        expect(isAbilityOnlyInvalidation({
            reason: "card-updated",
            changedCardIds: [42]
        })).toBe(false);
    });

    test("preserves incremental card patch detail when revision changes", () => {
        const listener = jest.fn();
        document.addEventListener(CARD_CATALOG_INVALIDATED_EVENT, listener);

        const changedCard = { id: 42, name: "Patched Card" };
        const synced = syncDataRevisionFromResponse(createResponseWithRevision("revision-a"), {
            source: "card-editor",
            changedCardIds: [42],
            changedCards: [changedCard]
        });

        expect(synced).toBe(true);
        expect(listener).toHaveBeenCalledTimes(1);
        expect(listener.mock.calls[0][0].detail).toEqual(expect.objectContaining({
            source: "card-editor",
            revision: "revision-a",
            changedCardIds: [42],
            changedCards: [changedCard]
        }));

        document.removeEventListener(CARD_CATALOG_INVALIDATED_EVENT, listener);
    });
});
