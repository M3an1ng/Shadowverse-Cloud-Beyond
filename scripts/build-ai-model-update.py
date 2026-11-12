"""Package a trained model alone. No database, decks or training records are included."""
from pathlib import Path
import hashlib, shutil, tarfile, sys, re
root=Path(__file__).resolve().parent.parent
name=sys.argv[1] if len(sys.argv)>1 else 'svnebula-ai-model-20261105'
assert re.fullmatch(r'svnebula-ai-model-[a-z0-9-]+',name)
source=Path(sys.argv[2]).resolve() if len(sys.argv)>2 else root/'data/ai-model/model.json'
stage=root/'.local-run'/name
archive=root/'exports'/(name+'.tar.gz')
assert not stage.exists() and not archive.exists(),'Choose a new package name'
stage.mkdir(parents=True)
shutil.copy2(source,stage/'model.json')
shutil.copy2(root/'deploy/ubuntu/update-ai-model.sh',stage/'update-ai-model.sh')
(stage/'install.sh').write_text('#!/usr/bin/env bash\nset -Eeuo pipefail\ncd -- "$(dirname -- "${BASH_SOURCE[0]}")"\nsha256sum -c SHA256SUMS\nbash update-ai-model.sh "$PWD/model.json"\n',encoding='utf-8',newline='\n')
def digest(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
 return h.hexdigest()
(stage/'SHA256SUMS').write_text(''.join(f'{digest(p)}  {p.name}\n' for p in sorted(stage.iterdir()) if p.is_file()),encoding='utf-8')
archive.parent.mkdir(exist_ok=True)
with tarfile.open(archive,'w:gz') as tar:tar.add(stage,arcname=name)
archive.with_suffix('.gz.sha256').write_text(f'{digest(archive)}  {archive.name}\n',encoding='utf-8')
print(f'{archive}\nBytes: {archive.stat().st_size}\nSHA256: {digest(archive)}')
