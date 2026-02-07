import { createListQueryNode, createListSortNode, createListTakeNode, createListDistinctNode, createListSelectNode } from "./core.js";

export const PIPELINE_STAGE_META = {
    source: { label: "来源", dslMethod: null, backendOp: "QueryConfig.SourcePoolExpression" },
    filter: { label: "查询", dslMethod: "query", backendOp: "FilterOperation" },
    sort: { label: "排序", dslMethod: "orderBy", backendOp: "SortOperation" },
    take: { label: "取样", dslMethod: "take", backendOp: "SelectionOperation" },
    distinct: { label: "去重", dslMethod: "distinct", backendOp: "DistinctOperation" },
    select: { label: "选择", dslMethod: "select", backendOp: "SelectOperation" },
    raw: { label: "原始片段", dslMethod: null, backendOp: null }
};

export function isListOperationNode(node) {
    return Boolean(node && (node.type === "listQuery" || node.type === "listSort" || node.type === "listTake" || node.type === "listDistinct" || node.type === "listSelect"));
}

export function getStageTypeFromNode(node) {
    if (!node) return null;
    if (node.type === "listQuery") return "filter";
    if (node.type === "listSort") return "sort";
    if (node.type === "listTake") return "take";
    if (node.type === "listDistinct") return "distinct";
    if (node.type === "listSelect") return "select";
    return null;
}

export function extractListPipeline(rootNode) {
    if (!isListOperationNode(rootNode)) return null;

    const stages = [];
    let current = rootNode;
    while (isListOperationNode(current)) {
        stages.push({
            type: getStageTypeFromNode(current),
            node: current
        });
        current = current.source;
    }

    stages.reverse();
    return {
        source: current,
        stages
    };
}

export function rebuildListChainFromStages(sourceNode, stages) {
    let current = sourceNode;
    (stages || []).forEach(stage => {
        if (!stage || !stage.node) return;
        const node = stage.node;
        node.source = current;
        node.returnType = current?.returnType ?? node.returnType;
        if (current) {
            current.parent = node;
        }
        current = node;
    });
    return current || sourceNode;
}

export function createStageNode(stageType, sourceNode) {
    if (stageType === "filter") return createListQueryNode(sourceNode);
    if (stageType === "sort") return createListSortNode(sourceNode);
    if (stageType === "take") return createListTakeNode(sourceNode);
    if (stageType === "distinct") return createListDistinctNode(sourceNode);
    if (stageType === "select") return createListSelectNode(sourceNode);
    return null;
}
