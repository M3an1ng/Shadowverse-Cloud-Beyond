import { DslEditor } from "../components/DslEditor.js";
import { getReadableFunctionName } from "../components/dsl_editor/ui-labels.js";

describe("dsl frequency function visibility", () => {
    test("readable function name localizes frequency", () => {
        const editor = { metadata: { dsl: { functions: [] } } };
        expect(getReadableFunctionName(editor, "frequency")).toBe("频次");
    });

    test("collection functions include frequency from metadata", () => {
        const fakeEditor = {
            metadata: {
                dsl: {
                    functions: [
                        {
                            Name: "Frequency",
                            description: "统计集合中与目标值相等的元素个数。",
                            parameters: [{ typeName: "Object" }]
                        }
                    ]
                }
            },
            isFunctionCompatibleWithType: () => true,
            getReadableFunctionName: (name) => name
        };

        const options = DslEditor.prototype.getCollectionFunctions.call(fakeEditor, "List<Int32>");
        expect(options.some(option => String(option.value).toLowerCase() === "frequency")).toBe(true);
    });

    test("collection functions include find from metadata", () => {
        const fakeEditor = {
            metadata: {
                dsl: {
                    functions: [
                        {
                            Name: "Find",
                            description: "按索引获取集合中的目标（索引从1开始，越界返回默认值或null）。",
                            parameters: [{ typeName: "Object" }]
                        }
                    ]
                }
            },
            isFunctionCompatibleWithType: () => true,
            getReadableFunctionName: (name) => name
        };

        const options = DslEditor.prototype.getCollectionFunctions.call(fakeEditor, "List<Int32>");
        expect(options.some(option => String(option.value).toLowerCase() === "find")).toBe(true);
    });
});
