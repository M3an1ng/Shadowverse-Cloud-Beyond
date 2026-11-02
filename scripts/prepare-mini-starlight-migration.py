"""Prepare targeted native patches from the verified cloud Mini backup export."""
import copy
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
source_path = Path('I:/Codex/mini-starlight-backup.json')
source = json.loads(source_path.read_text(encoding='utf-8-sig'))
assert '/before-native-' in source['sourceBackup'], 'Use the migration backup, not the old local Mini database'
assert len(source['cards']) == 260
cards = {int(c['id']): c for c in source['cards']}
raw = {int(c['id']): c for c in source['raw']}
abilities = {int(a['id']): a for a in source['abilities']}
patches = []

for card_id, index in [(1471951, 1), (1471952, 0), (1472298, 1), (1472406, 0),
                       (1472407, 1), (1472650, 0), (1472651, 0), (1472921, 0), (1474098, 1)]:
    card = cards[card_id]
    mini = card['abilities'][index]
    record = abilities[int(mini['id'])]
    before = json.loads(record['jsonConfig'])
    after = copy.deepcopy(before)
    notes = []
    if card_id in (1471951, 1471952):
        after['conditions'][0]['expression'] = mini['conditions'][0]['expression']
        notes.append('历史破坏计数同时限定当前玩家，避免双方相同回合编号混计。')
    elif card_id in (1472298, 1472406, 1472407):
        after['name'] = mini['name']
        after['trigger']['eventTypes'] = ['CardMoved']
        after['trigger']['relationType'] = {'otherTarget': 'TargetOther', 'any': 'Any'}[mini['trigger']['relation']]
        after['conditions'] = [dict(id=c['id'], isEnabled=c['enabled'], expression=c['expression']) for c in mini['conditions']]
        # Mini and native hand-movement events have different ownership scopes.
        # Preserve the original own-hand rule explicitly on native CardMoved.
        after['conditions'].append(dict(id='mini-starlight-own-hand', isEnabled=True,
            expression='context.triggeringEvent.MovedCard.ownerPlayerId == context.owner.playerId'))
        for target, updated in zip(after['targets'], mini['targetPools']):
            target['query']['sourcePoolExpression'] = updated['expression']
        notes.append('改为监听移动到己方手牌且非抽取的事件；显式保留己方归属限制。')
    elif card_id == 1472650:
        after['conditions'] = [dict(id=c['id'], isEnabled=c['enabled'], expression=c['expression']) for c in mini['conditions']]
        after['effects'][0]['outputs'] = dict(mini['effects'][0]['outputs'])
        after['effects'][0]['outputs']['心随从'] = 'context.triggeringEvent.SummonedCard.instanceId'
        for target, updated in zip(after['effects'][0]['chainedAbilities'], mini['effects'][0]['chainedAbilities']):
            target['broadcastOnTrigger'] = updated['broadcast']
            target['targets'][0]['query']['sourcePoolExpression'] = 'context.owner.field.query(self.instanceId == context.triggeringEvent.Outputs.心随从)'
        notes.append('限定随从进场，按召唤随从的实例ID绑定三个连锁能力，适配新版输出和目标池格式。')
    elif card_id == 1472651:
        after['effects'][0]['chainedAbilities'][0]['conditions'] = [
            dict(id=c['id'], isEnabled=c['enabled'], expression=c['expression'])
            for c in mini['effects'][0]['chainedAbilities'][0]['conditions']]
        notes.append('没有可用变身目标时跳过连锁。')
    elif card_id == 1472921:
        after['conditions'][1]['expression'] = mini['conditions'][1]['expression']
        notes.append('使用召唤原因识别因能力进场。')
    elif card_id == 1474098:
        assert before['targets'] == []
        after['targets'] = [dict(isEnabled=True, targetHandlingStrategy=0, requireValidTarget=False,
            query=dict(sourcePoolExpression='context.SourceCard', operations=[]), playerChoiceOptions=None)]
        notes.append('启动补上本卡牌目标，使倒数减少正确作用于自身。')
    fields = {}
    if card_id == 1472407:
        fields['Description'] = {'before': raw[card_id]['description'], 'after': card['description']}
    assert before != after
    patches.append(dict(cardId=card_id, name=card['name'], sourceUpdatedAt=card.get('updatedAt'),
        fields=fields, abilities=[dict(id=record['id'], nameBefore=record['name'], nameAfter=after['name'], before=before, after=after)], notes=notes))

payload = dict(format=1, pack='星光舞台', sourceBackup=source['sourceBackup'],
    sourceSha256=hashlib.sha256(source_path.read_bytes()).hexdigest(), reviewedCards=260,
    patches=patches, equivalentDifferences=[
        dict(cardId=1473372, name=cards[1473372]['name'], reason='新版原生支持单个卡牌ID转为ID列表；保留合法的原生DSL，不复制Mini的动态数组语法。'),
        dict(cardId=1473971, name=cards[1473971]['name'], reason='缺省的removeAfterUse与false等价。'),
        dict(cardId=1474035, name=cards[1474035]['name'], reason='缺省的removeAfterUse与false等价。')])
target = root / 'data/mini-starlight-migration-20261030.json'
target.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Reviewed {payload["reviewedCards"]} cards; prepared {len(patches)} card / {sum(len(p["abilities"]) for p in patches)} ability patches')
