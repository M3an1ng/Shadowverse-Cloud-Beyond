import { DslEditor } from "../components/DslEditor.js";
import { renderPipelineStageInlineEditor } from "../components/dsl_editor/pipeline-editor.js";
import { createListQueryNode, createValueNode } from "../components/dsl_editor/core.js";
import { removeFavoritesMenu, showFavoritesMenu } from "../components/dsl_editor/ui-favorites.js";

describe("DslEditor sub editor favorites", () => {
    test("pipeline stage inline editor keeps favorites toolbar enabled", () => {
        const editor = new DslEditor();
        const inlineEditor = {
            render: jest.fn().mockResolvedValue(undefined)
        };
        const source = createValueNode("self.cards", "List<Card>", {
            type: "property",
            fullPath: "self.cards"
        });
        const stageNode = createListQueryNode(source);
        const container = document.createElement("div");

        editor.pipelineEditState = {
            node: stageNode,
            slot: "filterExpression",
            editor: inlineEditor,
            draftExpression: "",
            draftCountValue: ""
        };
        editor.pipelineActiveContextKey = "root:none";
        editor.pipelineContextStore = new Map([
            ["root:none", {
                pipelineOpen: false,
                pipelineOpenSource: null,
                pipelineOpenMode: null,
                pipelineEditState: editor.pipelineEditState
            }]
        ]);
        editor.getExpectedTypeForSlot = jest.fn().mockReturnValue("Boolean");

        renderPipelineStageInlineEditor(editor, { node: stageNode }, container);

        expect(inlineEditor.hideFavoritesToolbar).toBe(false);
        expect(inlineEditor.render).toHaveBeenCalledWith(
            expect.any(HTMLElement),
            "",
            expect.any(Function),
            expect.objectContaining({
                expectedType: "Boolean",
                hideFavoritesToolbar: false
            })
        );
    });

    test("favorites menu renders above pipeline stage modal overlay", () => {
        const editor = {
            favoriteState: {
                query: "",
                items: [],
                recentItems: [],
                page: 1,
                total: 0,
                totalPages: 1,
                loading: false,
                error: null
            },
            favoriteSearchTimer: null,
            favoriteMenuOutsideHandler: null,
            favoriteMenuAnchor: null,
            userId: null
        };
        const overlay = document.createElement("div");
        overlay.className = "dsl-pipeline-stage-modal-overlay";
        overlay.style.zIndex = "1250";
        const anchor = document.createElement("button");
        anchor.getBoundingClientRect = () => ({
            left: 24,
            right: 124,
            top: 48,
            bottom: 76,
            width: 100,
            height: 28
        });
        overlay.appendChild(anchor);
        document.body.appendChild(overlay);

        showFavoritesMenu(editor, anchor);

        const menu = document.querySelector(".dsl-favorites-menu");
        expect(menu).not.toBeNull();
        expect(menu.style.zIndex).toBe("1260");

        removeFavoritesMenu(editor);
    });
});
