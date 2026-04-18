import { getDataSourceOptions } from "../components/dsl_editor/options-provider.js";
import { getReadablePropertyChain } from "../components/dsl_editor/ui-nodes.js";

function createEditorStub() {
    const metadata = {
        dsl: {
            contextObjects: [
                {
                    name: "context",
                    displayName: "上下文",
                    typeName: "GameContext"
                },
                {
                    name: "db",
                    displayName: "卡牌定义数据库",
                    description: "按构筑环境拆分后的静态卡牌定义集合",
                    typeName: "CardDefinitionDatabaseRoot"
                }
            ],
            typeMetadata: {
                CardDefinitionDatabaseRoot: {
                    properties: [
                        {
                            name: "rotationCards",
                            displayName: "指定卡池",
                            typeName: "List<CardDefinition>"
                        },
                        {
                            name: "unlimitedCards",
                            displayName: "无限制卡池",
                            typeName: "List<CardDefinition>"
                        },
                        {
                            name: "openCards",
                            displayName: "开放卡池",
                            typeName: "List<CardDefinition>"
                        }
                    ]
                }
            }
        }
    };

    return {
        metadata,
        normalizeTypeName: (typeName) => typeName,
        getDslContextObject(name) {
            const normalized = String(name || "").trim().toLowerCase();
            return this.metadata.dsl.contextObjects.find(obj =>
                String(obj.name || "").toLowerCase() === normalized
            ) || null;
        },
        getTypeMetadata(typeName) {
            return this.metadata.dsl.typeMetadata[typeName] || null;
        },
        isDictionaryType: () => false
    };
}

describe("dsl db root metadata", () => {
    test("exposes db as a data source option", () => {
        const editor = createEditorStub();

        const options = getDataSourceOptions(editor);
        const dbOption = options.find(option => option.type === "context" && option.value === "db");

        expect(dbOption).toBeTruthy();
        expect(dbOption.label).toBe("卡牌定义数据库");
        expect(dbOption.returnType).toBe("CardDefinitionDatabaseRoot");
    });

    test("translates environment-scoped db paths using metadata display names", () => {
        const editor = createEditorStub();

        expect(getReadablePropertyChain(editor, "db.rotationCards")).toBe("卡牌定义数据库.指定卡池");
        expect(getReadablePropertyChain(editor, "db.unlimitedCards")).toBe("卡牌定义数据库.无限制卡池");
        expect(getReadablePropertyChain(editor, "db.openCards")).toBe("卡牌定义数据库.开放卡池");
    });
});
