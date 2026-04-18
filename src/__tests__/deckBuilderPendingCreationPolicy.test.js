import {
    attachPendingDeckId,
    createPendingDeckCreationRecord,
    markPendingDeckCreationAbandoned,
    shouldCleanupPendingDeckCreation
} from "../deckBuilderPendingCreationPolicy.js";

describe("deckBuilder pending creation policy", () => {
    test("new pending creation starts without deck id and without cleanup", () => {
        const record = createPendingDeckCreationRecord(7);

        expect(record).toEqual({
            requestId: 7,
            deckId: null,
            abandoned: false
        });
        expect(shouldCleanupPendingDeckCreation(record, "deck-builder")).toBe(false);
    });

    test("abandoned pending creation cleans up once deck id arrives", () => {
        const record = createPendingDeckCreationRecord(9);
        const abandoned = markPendingDeckCreationAbandoned(record);
        const created = attachPendingDeckId(abandoned, "deck-123");

        expect(shouldCleanupPendingDeckCreation(created, "deck-builder")).toBe(true);
    });

    test("route switch away from deck builder cleans up created shell", () => {
        const record = attachPendingDeckId(createPendingDeckCreationRecord(11), "deck-456");

        expect(shouldCleanupPendingDeckCreation(record, "navigation")).toBe(true);
    });
});
