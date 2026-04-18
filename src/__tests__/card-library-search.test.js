import { matchesCardLibrarySearch } from "../cardLibrarySearch.js";

describe("card library search matching", () => {
    test("returns true when search text is empty", () => {
        expect(matchesCardLibrarySearch({ name: "精灵弓箭手" }, "")).toBe(true);
        expect(matchesCardLibrarySearch({ name: "精灵弓箭手" }, "   ")).toBe(true);
    });

    test("matches against card name", () => {
        const card = {
            name: "虹辉的天使"
        };
        expect(matchesCardLibrarySearch(card, "天使")).toBe(true);
    });

    test("matches against card description", () => {
        const card = {
            name: "虹辉的天使",
            description: "入场曲：抽取1张卡牌。"
        };
        expect(matchesCardLibrarySearch(card, "抽取1张")).toBe(true);
    });

    test("matches against fallback Description field", () => {
        const card = {
            Name: "虹辉的天使",
            Description: "谢幕曲：回复自己的主战者2点体力。"
        };
        expect(matchesCardLibrarySearch(card, "回复自己的主战者")).toBe(true);
    });

    test("returns false when name and description do not include search text", () => {
        const card = {
            name: "虹辉的天使",
            description: "入场曲：抽取1张卡牌。"
        };
        expect(matchesCardLibrarySearch(card, "疾驰")).toBe(false);
    });
});
