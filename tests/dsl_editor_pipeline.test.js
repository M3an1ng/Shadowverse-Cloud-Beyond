import { createListQueryNode, createListSortNode, createListTakeNode, createValueNode } from '../src/components/dsl_editor/core.js';
import { extractListPipeline, rebuildListChainFromStages } from '../src/components/dsl_editor/pipeline.js';
import { DslEditor } from '../src/components/DslEditor.js';

describe('DslEditor Pipeline helpers', () => {
    test('extractListPipeline returns ordered stages and source', () => {
        const source = createValueNode('player.hand', 'List<CardInstance>', { type: 'property' });
        const query = createListQueryNode(source);
        query.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));
        const sort = createListSortNode(query);
        sort.setSortExpression(createValueNode('self.cost', 'Int32', { type: 'property' }));
        const take = createListTakeNode(sort);
        take.setCountExpression(createValueNode('2', 'Int32', { type: 'staticValue' }));

        const pipeline = extractListPipeline(take);
        expect(pipeline.source).toBe(source);
        expect(pipeline.stages.map(stage => stage.type)).toEqual(['filter', 'sort', 'select']);
    });

    test('rebuildListChainFromStages rewires chain in new order', () => {
        const source = createValueNode('player.hand', 'List<CardInstance>', { type: 'property' });
        const query = createListQueryNode(source);
        query.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));
        const sort = createListSortNode(query);
        sort.setSortExpression(createValueNode('self.cost', 'Int32', { type: 'property' }));
        const take = createListTakeNode(sort);
        take.setCountExpression(createValueNode('2', 'Int32', { type: 'staticValue' }));

        const pipeline = extractListPipeline(take);
        const stages = [pipeline.stages[1], pipeline.stages[0], pipeline.stages[2]];
        const newRoot = rebuildListChainFromStages(source, stages);

        expect(newRoot.toExpression()).toBe('player.hand.orderBy(self.cost).query(true).take(2)');
    });

    test('DslEditor.movePipelineStage rewires list stages', () => {
        const editor = new DslEditor();
        editor.maintainASTRelationships = jest.fn();
        editor.triggerStateUpdate = jest.fn();

        const source = createValueNode('player.hand', 'List<CardInstance>', { type: 'property' });
        const query = createListQueryNode(source);
        query.setFilterExpression(createValueNode('true', 'Boolean', { type: 'staticValue' }));
        const sort = createListSortNode(query);
        sort.setSortExpression(createValueNode('self.cost', 'Int32', { type: 'property' }));
        const take = createListTakeNode(sort);
        take.setCountExpression(createValueNode('2', 'Int32', { type: 'staticValue' }));

        editor.astRoot = take;
        editor.workspaceAST = take;
        editor.pipelineOpen = true;

        editor.movePipelineStage(0, 1);

        expect(editor.astRoot.toExpression()).toBe('player.hand.orderBy(self.cost).query(true).take(2)');
    });
});
