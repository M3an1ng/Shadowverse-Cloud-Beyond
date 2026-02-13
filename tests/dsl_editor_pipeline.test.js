import { createListQueryNode, createListSortNode, createListTakeNode, createListSelectNode, createOperatorNode, createValueNode } from '../src/components/dsl_editor/core.js';
import { extractListPipeline, rebuildListChainFromStages } from '../src/components/dsl_editor/pipeline.js';
import { syncPipelineContext } from '../src/components/dsl_editor/pipeline-editor.js';
import { DslEditor } from '../src/components/DslEditor.js';

function buildModuloEqualityExpression() {
    const counter = createValueNode('context.sourceCard.counterMap.water', 'Int32', { type: 'property' });
    const plus = createOperatorNode('+');
    plus.setLeftOperand(counter);
    plus.setRightOperand(createValueNode('3', 'Int32', { type: 'staticValue' }));

    const modulo = createOperatorNode('%');
    modulo.setLeftOperand(plus);
    modulo.setRightOperand(createValueNode('3', 'Int32', { type: 'staticValue' }));

    const equals = createOperatorNode('==');
    equals.setLeftOperand(modulo);
    equals.setRightOperand(createValueNode('1', 'Int32', { type: 'staticValue' }));
    return equals;
}

describe('DslEditor Pipeline helpers', () => {
    test('ListQuery validate uses semantic type for encapsulated boolean filter', () => {
        const source = createValueNode('context.owner.field', 'List<CardInstance>', { type: 'property' });
        const query = createListQueryNode(source);
        const inner = createValueNode('self.isEvolved', 'Boolean', { type: 'property' });
        const wrapped = createValueNode('(self.isEvolved)', 'Object', {
            type: 'encapsulated',
            innerAST: inner
        });

        query.setFilterExpression(wrapped);
        const result = query.validate();
        expect(result.isValid).toBe(true);
    });

    test('ListTake validate uses semantic type for encapsulated numeric count', () => {
        const source = createValueNode('context.owner.field', 'List<CardInstance>', { type: 'property' });
        const take = createListTakeNode(source);
        const inner = createValueNode('2', 'Int32', { type: 'staticValue' });
        const wrapped = createValueNode('(2)', 'Object', {
            type: 'encapsulated',
            innerAST: inner
        });

        take.setCountExpression(wrapped);
        const result = take.validate();
        expect(result.isValid).toBe(true);
    });

    test('pipeline inline/light workspace still exposes pipeline entry for collection nodes', () => {
        const editor = new DslEditor();
        editor.isPipelineInline = true;
        editor.isLightWorkspace = true;

        const options = editor.getOptionsForType('List<CardInstance>');
        expect(options.some(option => option.type === 'listPipeline')).toBe(true);
    });

    test('extractListPipeline returns ordered stages and source', () => {
        const source = createValueNode('player.hand', 'List<CardInstance>', { type: 'property' });
        const query = createListQueryNode(source);
        query.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));
        const sort = createListSortNode(query);
        sort.setSortExpression(createValueNode('self.cost', 'Int32', { type: 'property' }));
        const take = createListTakeNode(sort);
        take.setCountExpression(createValueNode('2', 'Int32', { type: 'staticValue' }));
        const select = createListSelectNode(take);
        select.setSelectExpression(createValueNode('self.cost', 'Int32', { type: 'property' }));

        const pipeline = extractListPipeline(select);
        expect(pipeline.source).toBe(source);
        expect(pipeline.stages.map(stage => stage.type)).toEqual(['filter', 'sort', 'take', 'select']);
    });

    test('rebuildListChainFromStages rewires chain in new order', () => {
        const source = createValueNode('player.hand', 'List<CardInstance>', { type: 'property' });
        const query = createListQueryNode(source);
        query.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));
        const sort = createListSortNode(query);
        sort.setSortExpression(createValueNode('self.cost', 'Int32', { type: 'property' }));
        const take = createListTakeNode(sort);
        take.setCountExpression(createValueNode('2', 'Int32', { type: 'staticValue' }));
        const select = createListSelectNode(take);
        select.setSelectExpression(createValueNode('self.cost', 'Int32', { type: 'property' }));

        const pipeline = extractListPipeline(select);
        const stages = [pipeline.stages[1], pipeline.stages[0], pipeline.stages[2], pipeline.stages[3]];
        const newRoot = rebuildListChainFromStages(source, stages);

        expect(newRoot.toExpression()).toBe('player.hand.orderBy(self.cost).query(true).take(2).select(self.cost)');
    });

    test('DslEditor.movePipelineStage rewires list stages', () => {
        const editor = new DslEditor();
        editor.maintainASTRelationships = jest.fn();
        editor.triggerStateUpdate = jest.fn();
        editor.renderEditor = jest.fn();

        const source = createValueNode('player.hand', 'List<CardInstance>', { type: 'property' });
        const query = createListQueryNode(source);
        query.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));
        const sort = createListSortNode(query);
        sort.setSortExpression(createValueNode('self.cost', 'Int32', { type: 'property' }));
        const take = createListTakeNode(sort);
        take.setCountExpression(createValueNode('2', 'Int32', { type: 'staticValue' }));
        const select = createListSelectNode(take);
        select.setSelectExpression(createValueNode('self.cost', 'Int32', { type: 'property' }));

        editor.astRoot = select;
        editor.workspaceAST = select;
        editor.syncPipelineContext();
        editor.pipelineOpen = true;
        editor.pipelineOpenSource = source;

        editor.movePipelineStage(0, 1);

        expect(editor.astRoot.toExpression()).toBe('player.hand.orderBy(self.cost).query(true).take(2).select(self.cost)');
    });

    test('syncPipelineContext keeps current context state but collapses on context switch', () => {
        const parentA = { type: 'operator', leftOperand: null, rightOperand: null };
        const parentB = { type: 'operator', leftOperand: null, rightOperand: null };
        const editor = {
            isPipelineInline: false,
            pipelineContextStore: new Map(),
            pipelineNodeIds: new WeakMap(),
            pipelineNodeIdSeed: 1,
            pipelineActiveContextKey: null,
            pipelineOpen: true,
            pipelineOpenSource: { type: 'value' },
            pipelineEditState: { node: { type: 'listQuery' }, slot: 'filterExpression' },
            isLightWorkspace: false,
            lightWorkspaceFocus: null,
            virtualRoot: { main: null },
            workspaceAST: null,
            peekEditingPath: jest.fn(() => ({ parentNode: parentA, slot: 'left' }))
        };

        const firstKey = syncPipelineContext(editor);
        expect(firstKey).toBeTruthy();
        expect(editor.pipelineOpen).toBe(false);

        // 同一上下文内，保留用户手动打开状态
        editor.pipelineOpen = true;
        const sameKey = syncPipelineContext(editor);
        expect(sameKey).toBe(firstKey);
        expect(editor.pipelineOpen).toBe(true);

        // 切换上下文后，默认折叠
        editor.peekEditingPath.mockReturnValue({ parentNode: parentB, slot: 'right' });
        const secondKey = syncPipelineContext(editor);
        expect(secondKey).not.toBe(firstKey);
        expect(editor.pipelineOpen).toBe(false);
    });

    test('getPipelineContext resolves clicked pipeline source within operator workspace', () => {
        const editor = new DslEditor();

        const leftSource = createValueNode('context.owner.hand', 'List<CardInstance>', { type: 'property' });
        const leftQuery = createListQueryNode(leftSource);
        leftQuery.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));

        const rightSource = createValueNode('context.opponent.field', 'List<CardInstance>', { type: 'property' });
        const rightQuery = createListQueryNode(rightSource);
        rightQuery.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));

        const root = createOperatorNode('|');
        root.setLeftOperand(leftQuery);
        root.setRightOperand(rightQuery);

        editor.workspaceAST = root;
        editor.virtualRoot.main = root;
        editor.syncPipelineContext();
        editor.pipelineOpen = true;
        editor.pipelineOpenSource = rightSource;

        const pipeline = editor.getPipelineContext();
        expect(pipeline).toBeTruthy();
        expect(pipeline.source).toBe(rightSource);
        expect(pipeline.root).toBe(rightQuery);
    });

    test('getPipelineContext uses exact node identity when pipelines share the same source expression', () => {
        const editor = new DslEditor();

        const leftSource = createValueNode('context.owner.field', 'List<CardInstance>', { type: 'property' });
        const leftQuery = createListQueryNode(leftSource);
        leftQuery.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));

        const rightSource = createValueNode('context.owner.field', 'List<CardInstance>', { type: 'property' });
        const rightQuery = createListQueryNode(rightSource);
        rightQuery.setFilterExpression(createValueNode('false', 'Boolean', { type: 'staticValue' }));

        const root = createOperatorNode('|');
        root.setLeftOperand(leftQuery);
        root.setRightOperand(rightQuery);

        editor.workspaceAST = root;
        editor.astRoot = root;
        editor.virtualRoot.main = root;
        editor.syncPipelineContext();
        editor.pipelineOpen = true;
        editor.pipelineOpenSource = rightSource;

        const pipeline = editor.getPipelineContext();
        expect(pipeline).toBeTruthy();
        expect(pipeline.root).toBe(rightQuery);
        expect(pipeline.source).toBe(rightSource);
    });

    test('removePipelineStage only mutates the selected pipeline branch when sources look the same', () => {
        const editor = new DslEditor();
        editor.maintainASTRelationships = jest.fn();
        editor.updateExpressionFromAST = jest.fn();
        editor.renderEditor = jest.fn();

        const leftSource = createValueNode('context.owner.field', 'List<CardInstance>', { type: 'property' });
        const leftQuery = createListQueryNode(leftSource);
        leftQuery.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));

        const rightSource = createValueNode('context.owner.field', 'List<CardInstance>', { type: 'property' });
        const rightQuery = createListQueryNode(rightSource);
        rightQuery.setFilterExpression(createValueNode('false', 'Boolean', { type: 'staticValue' }));

        const root = createOperatorNode('|');
        root.setLeftOperand(leftQuery);
        root.setRightOperand(rightQuery);

        editor.workspaceAST = root;
        editor.astRoot = root;
        editor.virtualRoot.main = root;
        editor.syncPipelineContext();
        editor.pipelineOpen = true;
        editor.pipelineOpenSource = rightSource;

        editor.removePipelineStage(0);

        expect(editor.workspaceAST.leftOperand.type).toBe('listQuery');
        expect(editor.workspaceAST.rightOperand.type).toBe('value');
        expect(editor.workspaceAST.rightOperand.toExpression()).toBe('context.owner.field');
    });

    test('insertPipelineStage keeps operator root when editing selected pipeline branch', () => {
        const editor = new DslEditor();
        editor.maintainASTRelationships = jest.fn();
        editor.updateExpressionFromAST = jest.fn();
        editor.renderEditor = jest.fn();
        editor.showExpressionError = jest.fn();

        const leftSource = createValueNode('context.owner.hand', 'List<CardInstance>', { type: 'property' });
        const leftQuery = createListQueryNode(leftSource);
        leftQuery.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));

        const rightSource = createValueNode('context.opponent.field', 'List<CardInstance>', { type: 'property' });
        const rightQuery = createListQueryNode(rightSource);
        rightQuery.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));

        const root = createOperatorNode('|');
        root.setLeftOperand(leftQuery);
        root.setRightOperand(rightQuery);

        editor.workspaceAST = root;
        editor.astRoot = root;
        editor.virtualRoot.main = root;
        editor.syncPipelineContext();
        editor.pipelineOpen = true;
        editor.pipelineOpenSource = rightSource;

        editor.insertPipelineStage('sort');

        expect(editor.workspaceAST.type).toBe('operator');
        expect(editor.workspaceAST.leftOperand).toBe(leftQuery);
        expect(editor.workspaceAST.rightOperand.type).toBe('listSort');
        expect(editor.workspaceAST.rightOperand.source.type).toBe('listQuery');
    });

    test('openPipelineFromCurrent follows workspace-root semantics and ignores focused sub-slot', () => {
        const editor = new DslEditor();
        editor.maintainASTRelationships = jest.fn();
        editor.updateExpressionFromAST = jest.fn();
        editor.renderEditor = jest.fn();
        editor.showExpressionError = jest.fn();

        const left = createValueNode('self.currentCost', 'Int32', { type: 'property' });
        const right = createValueNode('context.triggeringEvent.GeneratedCards', 'List<CardInstance>', { type: 'property' });
        const root = createOperatorNode('>=');
        root.setLeftOperand(left);
        root.setRightOperand(right);

        editor.workspaceAST = root;
        editor.astRoot = root;
        editor.virtualRoot.main = root;
        editor.isLightWorkspace = true;
        editor.setLightWorkspaceFocus(root, 'right');

        editor.openPipelineFromCurrent();

        expect(editor.pipelineOpen).toBe(false);
        expect(editor.pipelineOpenSource).toBeNull();
        expect(editor.showExpressionError).toHaveBeenCalled();
    });

    test('openPipelineFromCurrent uses whole operator expression as pipeline source', () => {
        const editor = new DslEditor();
        editor.showExpressionError = jest.fn();

        const leftSource = createValueNode('context.owner.hand', 'List<CardInstance>', { type: 'property' });
        const leftQuery = createListQueryNode(leftSource);
        leftQuery.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));

        const rightSource = createValueNode('context.owner.field', 'List<CardInstance>', { type: 'property' });
        const rightQuery = createListQueryNode(rightSource);
        rightQuery.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));

        const root = createOperatorNode('~');
        root.setLeftOperand(leftQuery);
        root.setRightOperand(rightQuery);

        editor.workspaceAST = root;
        editor.astRoot = root;
        editor.virtualRoot.main = root;
        editor.isLightWorkspace = false;

        editor.openPipelineFromCurrent();

        expect(editor.pipelineOpen).toBe(true);
        expect(editor.pipelineOpenSource).toBe(root);
        expect(editor.showExpressionError).not.toHaveBeenCalled();
    });

    test('saveCurrentLayer returns to main path after sub-expression commit', () => {
        const editor = new DslEditor();
        editor.renderEditor = jest.fn();
        editor.syncPipelineContext = jest.fn();
        editor.showExpressionError = jest.fn();

        const equals = buildModuloEqualityExpression();
        editor.virtualRoot.main = editor.createEncapsulatedFrom(equals);

        const mainInner = editor.virtualRoot.main.metadata.innerAST;
        editor.handleEnterEditModeForSlot(mainInner, 'left');
        expect(editor.peekEditingPath()?.slot).toBe('left');

        editor.saveCurrentLayer();

        const top = editor.peekEditingPath();
        expect(top?.slot).toBe('main');
        expect(top?.parentNode).toBe(editor.virtualRoot);
        expect(editor.workspaceAST?.type).toBe('operator');
        expect(editor.workspaceAST?.operator).toBe('==');
    });

    test('submit updates DSL expression immediately after saveCurrentLayer', () => {
        const editor = new DslEditor();
        editor.renderEditor = jest.fn();
        editor.syncPipelineContext = jest.fn();
        editor.showExpressionError = jest.fn();

        const onChange = jest.fn();
        editor.onChange = onChange;

        const equals = buildModuloEqualityExpression();
        editor.virtualRoot.main = editor.createEncapsulatedFrom(equals);

        const mainInner = editor.virtualRoot.main.metadata.innerAST;
        editor.handleEnterEditModeForSlot(mainInner, 'left');
        editor.saveCurrentLayer();

        expect(editor.expression).toBeTruthy();
        expect(editor.expression).toBe(editor.workspaceAST?.toExpression?.());
        expect(onChange).toHaveBeenCalledWith(editor.expression);
    });

    test('submit updates DSL expression immediately in top-level workspace', () => {
        const editor = new DslEditor();
        editor.renderEditor = jest.fn();
        editor.syncPipelineContext = jest.fn();
        editor.showExpressionError = jest.fn();

        const onChange = jest.fn();
        editor.onChange = onChange;

        const equals = buildModuloEqualityExpression();
        editor.workspaceAST = equals;
        editor.astRoot = equals;
        editor.clearEditingPath();

        editor.saveWorkspace();

        expect(editor.expression).toBe(equals.toExpression());
        expect(onChange).toHaveBeenCalledWith(equals.toExpression());
    });

    test('computeCurrentExpression falls back to main expression when path is missing', () => {
        const editor = new DslEditor();
        const equals = buildModuloEqualityExpression();
        editor.virtualRoot.main = editor.createEncapsulatedFrom(equals);
        editor.workspaceAST = editor.cloneAST(equals.leftOperand);
        editor.astRoot = editor.workspaceAST;
        editor.clearEditingPath();

        const expression = editor.computeCurrentExpression();
        expect(expression).toBe(equals.toExpression());
    });

    test('ascend rebuilds from editing path instead of parent argument', () => {
        const editor = new DslEditor();
        editor.renderEditor = jest.fn();
        editor.syncPipelineContext = jest.fn();

        const equals = buildModuloEqualityExpression();
        editor.virtualRoot.main = editor.createEncapsulatedFrom(equals);
        const mainInner = editor.virtualRoot.main.metadata.innerAST;
        editor.handleEnterEditModeForSlot(mainInner, 'left');

        const beforeExpression = editor.workspaceAST?.toExpression?.();
        editor.ascend(editor.virtualRoot);

        expect(editor.peekEditingPath()?.slot).toBe('left');
        expect(editor.workspaceAST?.toExpression?.()).toBe(beforeExpression);
    });

    test('rebuildWorkspaceFromPath repairs stale path back to main', () => {
        const editor = new DslEditor();
        editor.renderEditor = jest.fn();
        editor.syncPipelineContext = jest.fn();

        const equals = buildModuloEqualityExpression();
        editor.virtualRoot.main = editor.createEncapsulatedFrom(equals);
        editor.workspaceAST = editor.cloneAST(equals.leftOperand);
        editor.astRoot = editor.workspaceAST;

        editor.clearEditingPath();
        editor.pushEditingPath({
            parentNode: { type: 'operator' },
            slot: 'left',
            descriptor: '编辑 left'
        });

        editor.rebuildWorkspaceFromPath();

        const top = editor.peekEditingPath();
        expect(top?.parentNode).toBe(editor.virtualRoot);
        expect(top?.slot).toBe('main');
        expect(editor.workspaceAST?.type).toBe('operator');
        expect(editor.workspaceAST?.operator).toBe('==');
    });

    test('goBack climbs to parent layer when current slot is emptied', () => {
        const editor = new DslEditor();
        editor.renderEditor = jest.fn();
        editor.showExpressionError = jest.fn();

        const source = createValueNode('context.owner.field', 'List<CardInstance>', { type: 'property' });
        const query = createListQueryNode(source);
        query.setFilterExpression(createValueNode('', 'Void', { type: 'empty' }));

        editor.virtualRoot.main = editor.createEncapsulatedFrom(query);
        const mainInner = editor.virtualRoot.main.metadata.innerAST;
        editor.handleEnterEditModeForSlot(mainInner, 'filterExpression');
        expect(editor.peekEditingPath()?.slot).toBe('filterExpression');

        editor.goBack();

        expect(editor.peekEditingPath()?.slot).toBe('main');
        expect(editor.peekEditingPath()?.parentNode).toBe(editor.virtualRoot);
        expect(editor.workspaceAST?.type).toBe('listQuery');
    });

    test('choosing listPipeline opens a fresh pipeline zone without mutating existing stages', () => {
        const editor = new DslEditor();
        editor.renderEditor = jest.fn();
        editor.showExpressionError = jest.fn();

        const source = createValueNode('context.owner.field', 'List<CardInstance>', { type: 'property' });
        const firstFilter = createListQueryNode(source);
        firstFilter.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));
        editor.pipelineEditState = { node: firstFilter, slot: 'filterExpression', editor: null, draftExpression: 'true', draftCountValue: '' };

        editor.workspaceAST = firstFilter;
        editor.astRoot = firstFilter;
        editor.virtualRoot.main = editor.createEncapsulatedFrom(firstFilter);
        editor.clearEditingPath();

        editor.addNode({ type: 'listPipeline', value: 'pipeline', label: '筛选' });

        const existingPipeline = extractListPipeline(editor.workspaceAST);
        expect(existingPipeline?.stages?.length).toBe(1);
        expect(editor.pipelineOpen).toBe(true);
        expect(editor.pipelineOpenMode).toBe('fresh');
        expect(editor.pipelineOpenSource?.toExpression?.()).toBe(firstFilter.toExpression());
        expect(editor.pipelineEditState?.node).toBeNull();
        expect(editor.pipelineEditState?.slot).toBeNull();

        const freshContext = editor.getPipelineContext();
        expect(freshContext?.stages?.length).toBe(0);
        expect(freshContext?.source?.toExpression?.()).toBe(firstFilter.toExpression());

        editor.insertPipelineStage('filter');
        const appended = extractListPipeline(editor.workspaceAST);
        expect(appended?.stages?.length).toBe(2);
        expect(editor.pipelineOpenMode).toBeNull();
    });

});
