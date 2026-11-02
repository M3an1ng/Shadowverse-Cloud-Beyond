"""Package the scoped Mini backup migration tool, not a replacement database."""
from pathlib import Path
import hashlib
import shutil
import tarfile
import json

root = Path(__file__).resolve().parent.parent
name = 'svnebula-starlight-data-20261030'
stage = root / '.local-run' / name
assert not stage.exists(), 'Do not overwrite an existing migration package'
stage.mkdir()
published = root / '.local-run/starlight-migration-tool'
tool = stage / 'tool'
tool.mkdir()
for path in published.iterdir():
    if path.is_file() and (path.suffix in ('.dll', '.pdb') or path.name.endswith(('.deps.json', '.runtimeconfig.json'))):
        shutil.copy2(path, tool / path.name)
shutil.copytree(published / 'runtimes/linux-x64', tool / 'runtimes/linux-x64')
assert (tool / 'runtimes/linux-x64/native/libe_sqlite3.so').is_file()
shutil.copy2(root / 'data/mini-starlight-migration-20261030.json', stage / 'starlight-patch.json')
(stage / 'update-starlight.sh').write_text((root / 'deploy/ubuntu/update-starlight.sh').read_text(encoding='utf-8'),encoding='utf-8',newline='\n')
data = json.loads((stage / 'starlight-patch.json').read_text(encoding='utf-8'))
summary = '来源：'+data['sourceBackup']+'\n已核对：260张\n修改：9张卡牌、9个能力\n\n'
for card in data['patches']:
    summary += str(card['cardId'])+' '+card['name']+'：'+'；'.join(card['notes'])+'\n'
summary += '\n等价差异，无需修改：\n'
for card in data['equivalentDifferences']:
    summary += str(card['cardId'])+' '+card['name']+'：'+card['reason']+'\n'
(stage / 'migration-summary.txt').write_text(summary,encoding='utf-8')
def digest(path):
    sha=hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda:stream.read(1024*1024),b''):sha.update(chunk)
    return sha.hexdigest()
files=sorted(p for p in stage.rglob('*') if p.is_file())
assert not any(p.suffix in ('.db','.sqlite') for p in files)
(stage/'SHA256SUMS').write_text(''.join(f'{digest(p)}  {p.relative_to(stage).as_posix()}\n' for p in files),encoding='utf-8')
archive=root/'exports'/(name+'.tar.gz')
assert not archive.exists()
with tarfile.open(archive,'w:gz') as tar:tar.add(stage,arcname=name)
archive.with_suffix(archive.suffix+'.sha256').write_text(f'{digest(archive)}  {archive.name}\n',encoding='utf-8')
print(f'{archive}\nBytes: {archive.stat().st_size}\nSHA256: {digest(archive)}')
