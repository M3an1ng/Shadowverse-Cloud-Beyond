import { createListQueryNode, createListSortNode, createListTakeNode } from "./core.js";

export const PIPELINE_STAGE_META = {
    source: { label: "来源", dslMethod: null, backendOp: "QueryConfig.SourcePoolExpression" },
    filter: { label: "筛选", dslMethod: "query", backendOp: "FilterOperation" },
    sort: { label: "排序", dslMethod: "orderBy", backendOp: "SortOperation" },
    select: { label: "选择", dslMethod: "take", backendOp: "SelectionOperation" },
    postProcess: { label: "后处理", dslMethod: null, backendOp: "PostProcessingOperation" },
    raw: { label: "原始片段", dslMethod: null, backendOp: null }
};

export function isListOperationNode(node) {
    return Boolean(node && (node.type === "listQuery" || node.type === "listSort" || node.type === "listTake"));
}

export function getStageTypeFromNode(node) {
    if (!node) return null;
    if (node.type === "listQuery") return "filter";
    if (node.type === "listSort") return "sort";
    if (node.type === "listTake") return "select";
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
    if (stageType === "select") return createListTakeNode(sourceNode);
    return null;
}
