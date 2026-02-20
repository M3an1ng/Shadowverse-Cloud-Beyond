import { TargetConfigEditor } from "../components/TargetConfigEditor.js";

describe("TargetConfigEditor simplified scope", () => {
    test("normalizes legacy multi-select scope into single scope value", () => {
        const editor = new TargetConfigEditor();
        expect(editor.normalizeSimplifiedScope(["Self", "Opponent"])).toBe("Both");
        expect(editor.normalizeSimplifiedScope(["Opponent"])).toBe("Opponent");
        expect(editor.normalizeSimplifiedScope(["Self"])).toBe("Self");
    });

    test("builds both-side zone source from currentGameState aggregates", () => {
        const editor = new TargetConfigEditor();
        const built = editor.buildSimplifiedTargetQuery({
            _simpleMethod: "All",
            _simpleScope: "Both",
            _simpleZone: ["Battlefield", "Deck"],
            _simpleType: ["Unit"]
        });

        expect(built.query.sourcePoolExpression).toContain("context.currentGameState.allField");
        expect(built.query.sourcePoolExpression).toContain("context.currentGameState.allDeck");
        expect(built.query.sourcePoolExpression).not.toContain("context.owner.");
        expect(built.query.sourcePoolExpression).not.toContain("context.opponent.");
    });

    test("builds both-side leader source as allLeaders", () => {
        const editor = new TargetConfigEditor();
        const built = editor.buildSimplifiedTargetQuery({
            _simpleMethod: "All",
            _simpleScope: "Both",
            _simpleZone: ["Battlefield"],
            _simpleType: ["Leader"]
        });

        expect(built.query.sourcePoolExpression).toBe("context.currentGameState.allLeaders");
    });

    test("parses legacy owner/opponent union back to Both scope", () => {
        const editor = new TargetConfigEditor();
        const parsed = editor.parseDslToSimplifiedUI({
            query: {
                sourcePoolExpression: '(context.owner.field | context.opponent.field).query(self.type == "Follower")'
            }
        });

        expect(parsed).toBeTruthy();
        expect(parsed._simpleScope).toBe("Both");
        expect(parsed._simpleZone).toEqual(["Battlefield"]);
        expect(parsed._simpleType).toEqual(["Unit"]);
    });

    test("parses currentGameState aggregate source back to Both scope", () => {
        const editor = new TargetConfigEditor();
        const parsed = editor.parseDslToSimplifiedUI({
            query: {
                sourcePoolExpression: 'context.currentGameState.allLeaders'
            }
        });

        expect(parsed).toBeTruthy();
        expect(parsed._simpleScope).toBe("Both");
        expect(parsed._simpleType).toEqual(["Leader"]);
    });

    test("normalizes legacy requireValidTarget into block-player strategy", () => {
        const editor = new TargetConfigEditor();
        const normalized = editor.normalizeTargetConfig({
            requireValidTarget: true,
            query: { sourcePoolExpression: "context.owner.field" }
        });

        expect(normalized.targetHandlingStrategy).toBe(2);
        expect(normalized.requireValidTarget).toBe(true);
    });

    test("explicit targetHandlingStrategy syncs requireValidTarget compatibility flag", () => {
        const editor = new TargetConfigEditor();
        const normalized = editor.normalizeTargetConfig({
            targetHandlingStrategy: 1,
            requireValidTarget: true,
            query: { sourcePoolExpression: "context.owner.field" }
        });

        expect(normalized.targetHandlingStrategy).toBe(1);
        expect(normalized.requireValidTarget).toBe(false);
    });
});
