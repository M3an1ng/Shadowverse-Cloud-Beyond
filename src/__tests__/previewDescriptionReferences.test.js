import { hydrateDescriptionReferences } from "../previewDescriptionReferences.js";

describe("preview description references", () => {
    test("stops hydrating direct self references", async () => {
        document.body.innerHTML = `
            <div id="root">
                <div class="preview-description-reference" data-preview-reference="诺顿星图手册">{诺顿星图手册}</div>
            </div>
        `;

        const container = document.getElementById("root");
        const cards = [
            {
                id: 1471907,
                name: "诺顿星图手册",
                description: "{诺顿星图手册}"
            }
        ];

        await hydrateDescriptionReferences(container, {
            cards,
            currentCardId: 1471907,
            currentCardName: "诺顿星图手册"
        });

        expect(container.textContent).toContain("检测到引用循环");
        expect(container.querySelectorAll(".preview-description-reference").length).toBe(1);
    });

    test("stops hydrating indirect reference cycles", async () => {
        document.body.innerHTML = `
            <div id="root">
                <div class="preview-description-reference" data-preview-reference="卡牌B">{卡牌B}</div>
            </div>
        `;

        const container = document.getElementById("root");
        const cards = [
            {
                id: 1,
                name: "卡牌A",
                description: "{卡牌B}"
            },
            {
                id: 2,
                name: "卡牌B",
                description: "{卡牌A}"
            }
        ];

        await hydrateDescriptionReferences(container, {
            cards,
            currentCardId: 1,
            currentCardName: "卡牌A"
        });

        expect(container.textContent).toContain("检测到引用循环");
        expect(container.querySelectorAll(".preview-description-reference").length).toBe(2);
    });
});
