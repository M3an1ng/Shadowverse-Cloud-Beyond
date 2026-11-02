"""Build an application-only update; never include player databases or uploaded media."""
import hashlib
import re
import shutil
import sys
import tarfile
from pathlib import Path

root = Path(__file__).resolve().parent.parent
name = sys.argv[1] if len(sys.argv) > 1 else 'svnebula-native-pool-local-20261030'
assert re.fullmatch(r'svnebula-native-[a-z0-9-]+', name), 'Invalid update name'
stage = root / '.local-run' / name
assert not stage.exists(), 'Update staging directory already exists'
server = stage / 'app/Server'
server.mkdir(parents=True)
published = root / '.local-run/pool-local-publish'
for path in published.iterdir():
    if path.is_file() and (path.suffix in ('.dll', '.pdb') or path.name.endswith(('.deps.json', '.runtimeconfig.json'))):
        shutil.copy2(path, server / path.name)
for runtime in ('linux-x64', 'win-x64'):
    shutil.copytree(published / 'runtimes' / runtime, server / 'runtimes' / runtime)
assert (server / 'runtimes/linux-x64/native/libe_sqlite3.so').is_file()
shutil.copytree(root / 'dist/assets', stage / 'app/assets')
shutil.copy2(root / 'dist/index.html', stage / 'app/index.html')
(stage / 'app/data').mkdir()
shutil.copy2(root / 'data/about-changelog.json', stage / 'app/data/about-changelog.json')
entry = re.search(r'<script\b[^>]*\bsrc="([^"]+)"', (stage / 'app/index.html').read_text(encoding='utf-8')).group(1)
(stage / 'entry-script.txt').write_text(entry + '\n', encoding='utf-8')
(stage / 'update.sh').write_text((root / 'deploy/ubuntu/update-native.sh').read_text(encoding='utf-8'), encoding='utf-8', newline='\n')
def digest(path):
    sha = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            sha.update(chunk)
    return sha.hexdigest()
files = sorted(p for p in stage.rglob('*') if p.is_file())
assert not any(p.suffix in ('.db', '.sqlite') or p.name.startswith('appsettings') for p in files)
(stage / 'SHA256SUMS').write_text(''.join(f'{digest(p)}  {p.relative_to(stage).as_posix()}\n' for p in files), encoding='utf-8')
archive = root / 'exports' / (name + '.tar.gz')
assert not archive.exists(), 'Do not overwrite an existing release'
with tarfile.open(archive, 'w:gz') as tar:
    tar.add(stage, arcname=name)
checksum = digest(archive)
archive.with_suffix(archive.suffix + '.sha256').write_text(f'{checksum}  {archive.name}\n', encoding='utf-8')
print(f'{archive}\nBytes: {archive.stat().st_size}\nSHA256: {checksum}')
