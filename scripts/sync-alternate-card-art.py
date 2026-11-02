"""Copy only preview/detail art from original cards to alternate definitions.

Usage: python scripts/sync-alternate-card-art.py PATH_TO_DATABASE
Backs up the database and writes an audit report before completing.
"""
import json, re, sqlite3, sys
from datetime import datetime, timezone
from pathlib import Path

path = Path(sys.argv[1]).resolve()
if not path.is_file(): raise SystemExit('Database does not exist')
db = sqlite3.connect(path)
db.row_factory = sqlite3.Row
cards = {r['Id']: dict(r) for r in db.execute('SELECT Id,Name,ArtPreview,ArtDetail FROM CardDefinitions')}
links = {}
def bind(target, source):
    if target == source or target not in cards or source not in cards: return
    if target in links and links[target] != source: raise ValueError(f'Ambiguous original for {target}')
    links[target] = source
def scalar(value):
    if isinstance(value, dict): value = value.get('Value', value.get('value'))
    return value if isinstance(value, int) else None
for row in db.execute('SELECT CardId,JsonConfig FROM CardAbilities'):
    config = json.loads(row['JsonConfig'])
    if not any(k.get('keyword') in ('Accelerate','Crystalize') for k in config.get('abilityKeywords',[])): continue
    alternate = config.get('alternatePlay') or {}
    bind(scalar(alternate.get('playAsCardDefinitionId')), row['CardId'])
    for effect in config.get('effects',[]):
        params = effect.get('parameters') or {}
        if effect.get('type') in ('Accelerate','Crystalize'):
            bind(scalar(params.get('CardDefinitionId')),row['CardId'])
        if effect.get('type') == 'Transform':
            target = scalar(params.get('TargetCardDefinitionId'))
            if target in cards and re.match(r'^(激奏|结晶|結晶)[:：]', cards[target]['Name']): bind(target,row['CardId'])
for target, card in cards.items():
    name = re.sub(r'^(激奏|结晶|結晶)[:：]\s*','',card['Name'])
    if name == card['Name']: continue
    matches = [i for i,c in cards.items() if c['Name'] == name]
    if len(matches) == 1: bind(target,matches[0])
    elif target not in links: raise ValueError(f'Cannot resolve original for {target}')
stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
backup = path.with_name('before-alternate-art-'+stamp+'.db')
with sqlite3.connect(backup) as destination: db.backup(destination)
changes=[]
with db:
    for target,source in sorted(links.items()):
        original,alternate = cards[source],cards[target]
        changes.append({'originalId':source,'originalName':original['Name'],'alternateId':target,'alternateName':alternate['Name'],'before':{k:alternate[k] for k in ('ArtPreview','ArtDetail')},'after':{k:original[k] for k in ('ArtPreview','ArtDetail')}})
        db.execute('UPDATE CardDefinitions SET ArtPreview=?,ArtDetail=? WHERE Id=?',(original['ArtPreview'],original['ArtDetail'],target))
    assert db.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
    for target,source in links.items():
        saved=db.execute('SELECT ArtPreview,ArtDetail FROM CardDefinitions WHERE Id=?',(target,)).fetchone()
        assert tuple(saved)==(cards[source]['ArtPreview'],cards[source]['ArtDetail'])
db.close()
report=path.with_name('alternate-art-'+stamp+'.json')
report.write_text(json.dumps({'backup':str(backup),'changes':changes},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'updatedCards':len(changes),'backup':str(backup),'report':str(report)},ensure_ascii=False))
