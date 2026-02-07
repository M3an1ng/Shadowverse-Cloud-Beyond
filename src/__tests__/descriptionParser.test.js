import { parseDescriptionTokens, renderDescriptionHtml } from "../descriptionParser.js";

describe("description parser", () => {
    test("parses keyword and derived segments in order", () => {
        const tokens = parseDescriptionTokens("获得【疾驰】并召唤『妖精』。");

        expect(tokens).toEqual([
            { type: "plain", text: "获得" },
            { type: "keyword", text: "疾驰" },
            { type: "plain", text: "并召唤" },
            { type: "derived", text: "妖精" },
            { type: "plain", text: "。" }
        ]);
    });

    test("unmatched markers remain plain text", () => {
        const text = "获得【疾驰";
        const tokens = parseDescriptionTokens(text);
        expect(tokens.every(token => token.type === "plain")).toBe(true);
        expect(tokens.map(token => token.text).join("")).toBe(text);

        const trailingText = "疾驰】";
        const trailingTokens = parseDescriptionTokens(trailingText);
        expect(trailingTokens.every(token => token.type === "plain")).toBe(true);
        expect(trailingTokens.map(token => token.text).join("")).toBe(trailingText);
    });

    test("renderDescriptionHtml wraps spans with token metadata", () => {
        const html = renderDescriptionHtml("【疾驰】");
        expect(html).toBe('<span class="preview-description-token preview-description-keyword">【<span class="preview-description-text" data-preview-token="keyword" data-preview-value="疾驰">疾驰</span>】</span>');
    });

    test("renderDescriptionHtml keeps numeric suffix while highlighting base", () => {
        const html = renderDescriptionHtml("【吟唱_2】");
        expect(html).toBe('<span class="preview-description-token preview-description-keyword">【<span class="preview-description-text" data-preview-token="keyword" data-preview-value="吟唱">吟唱</span>_2】</span>');
    });

    test("renderDescriptionHtml keeps non-numeric suffix while highlighting base", () => {
        const html = renderDescriptionHtml("【爆能强化_X】");
        expect(html).toBe('<span class="preview-description-token preview-description-keyword">【<span class="preview-description-text" data-preview-token="keyword" data-preview-value="爆能强化">爆能强化</span>_X】</span>');
    });

    test("divider tokens render as lines for repeated hyphens", () => {
        const html = renderDescriptionHtml("第一行---第二行");
        expect(html).toBe('第一行<div class="preview-description-divider" role="presentation"></div>第二行');
    });

    test("divider trims surrounding whitespace", () => {
        const html = renderDescriptionHtml("第一行 ---    第二行");
        expect(html).toBe('第一行<div class="preview-description-divider" role="presentation"></div>第二行');
    });
});
