import { applyDeckBuilderFilterUiState } from "../deckBuilderFilterUiState.js";

describe("deck builder filter UI state", () => {
    test("restores persisted search text when the editor UI is rebuilt", () => {
        document.body.innerHTML = `
            <input type="text" id="card-search-input" value="">
            <button class="filter-btn cost-btn" data-cost="1">1</button>
            <button class="filter-btn cost-btn" data-cost="2">2</button>
        `;

        applyDeckBuilderFilterUiState(document, {
            search: "fairy",
            cost: 2
        });

        expect(document.getElementById("card-search-input").value).toBe("fairy");
        expect(document.querySelector('.cost-btn[data-cost="1"]').classList.contains("active")).toBe(false);
        expect(document.querySelector('.cost-btn[data-cost="2"]').classList.contains("active")).toBe(true);
    });
});
