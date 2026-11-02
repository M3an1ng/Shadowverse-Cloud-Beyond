"""Package the published backend, Vite build, local media and a consistent SQLite snapshot."""
import hashlib
import json
import re
import shutil
import sqlite3
import tarfile
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import unquote, urlsplit
import uuid


def file_hash(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()

ROOT = Path(__file__).resolve().parent.parent
STAGE = ROOT / '.local-run/cloud-native-20261030'
APP = STAGE / 'app'
EXPORT = ROOT / 'exports'
DB = ROOT / '.local-run/shadowverse-original-live-20261028.db'
assert (APP / 'Server/ShadowverseBeyond.Server.dll').is_file(), 'Publish the backend first'
assert (ROOT / 'dist/index.html').is_file(), 'Build the frontend first'
assert (APP / 'Server/runtimes/linux-x64/native/libe_sqlite3.so').is_file(), 'Linux SQLite runtime missing'
APP.mkdir(parents=True, exist_ok=True)
shutil.copytree(ROOT / 'dist', APP, dirs_exist_ok=True)
shutil.copytree(ROOT / 'assets', APP / 'assets', dirs_exist_ok=True)
(APP / 'data').mkdir(exist_ok=True)
shutil.copy2(ROOT / 'data/about-changelog.json', APP / 'data/about-changelog.json')
# No stale source seeds: this release starts from the exact local database snapshot.
(APP / 'robots.txt').write_text('User-agent: *\nDisallow: /\n', encoding='utf-8')
(APP / 'Server/appsettings.Production.json').write_text(json.dumps({
    'Logging': {'LogLevel': {'Default': 'Warning', 'Microsoft.AspNetCore': 'Warning'}},
    'Serilog': {'MinimumLevel': {'Default': 'Warning', 'Override': {'Microsoft': 'Warning'}}},
    'AllowedHosts': '*',
    'Replay': {'EnableJournalWrites': False, 'PersistClientDtos': False}
}, indent=2), encoding='utf-8')
# Portable publish includes many native platforms; retain Linux x64 and Windows x64
# so the exact same publication can also be smoke-tested locally.
for platform in (APP / 'Server/runtimes').iterdir():
    if platform.name not in ('linux-x64', 'win-x64'):
        shutil.rmtree(platform)
development_config = APP / 'Server/appsettings.Development.json'
if development_config.exists():
    development_config.unlink()
seed = STAGE / 'seed'
seed.mkdir(exist_ok=True)
dest = seed / 'shadowverse.db'
assert not dest.exists(), 'A seed snapshot already exists; inspect it before rebuilding'
with sqlite3.connect(DB.resolve().as_uri() + '?mode=ro', uri=True) as source:
    with sqlite3.connect(dest) as snapshot:
        source.backup(snapshot)
with sqlite3.connect(dest) as db:
    assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    assert not db.execute('PRAGMA foreign_key_check').fetchall()
    db.execute('PRAGMA journal_mode=DELETE')
    tables = ['Users', 'UserDecks', 'UserDeckCards', 'CardDefinitions', 'CardAbilities',
              'CardSetDefinitions', 'FeatureDocuments']
    counts = {table: db.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0] for table in tables}
    media = {v for row in db.execute('SELECT ArtPreview,ArtDetail FROM CardDefinitions') for v in row if v}
    missing = sorted(v for v in media if v.startswith('/') and
                     not (APP / unquote(urlsplit(v).path).lstrip('/')).is_file())
    probe = db.execute("SELECT Id,ArtPreview FROM CardDefinitions WHERE ArtPreview IS NOT NULL ORDER BY Id").fetchall()
    probe = next((i, image) for i, image in probe if image.startswith('/assets/') and
                 (APP / unquote(urlsplit(image).path).lstrip('/')).is_file())
    user = db.execute('SELECT Id FROM Users WHERE Username=?', ('李嘉图',)).fetchone()
    probes = {}
    if user:
        owner = str(uuid.UUID(user[0]))
        prefix = f'take-two-saved:{owner}:'
        probes[owner] = [{'id': key[len(prefix):], 'config': json.loads(value)}
                         for key, value in db.execute('SELECT Id,Json FROM FeatureDocuments WHERE Id LIKE ?', (prefix+'%',))]
    assert len(probes.get(owner, [])) == 2, 'The two imported pools must be included'
html = (APP / 'index.html').read_text(encoding='utf-8')
entry = re.search(r'<script\b[^>]*\bsrc="([^"]+)"', html).group(1)
stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
info = {'format': 1, 'releaseId': 'native-20261030-' + stamp, 'builtAt': stamp,
        'databaseCounts': counts, 'probeCardId': probe[0], 'probeImage': probe[1],
        'entryScript': entry, 'savedPoolProbes': probes, 'missingLocalMedia': missing,
        'databasePolicy': 'First migration uses the bundled local snapshot. Later updates retain the native online database.',
        'oldMiniPolicy': 'Back up the old data directory and retain the old application, environment and AI files.'}
(STAGE / 'bundle.json').write_text(json.dumps(info, ensure_ascii=False, indent=2), encoding='utf-8')
for name, target in [('install-native.sh', 'install.sh'), ('rollback-mini.sh', 'rollback-mini.sh'),
                     ('svnebula-native.service', 'svnebula-native.service'), ('verify-native.py', 'verify-native.py')]:
    text = (ROOT / 'deploy/ubuntu' / name).read_text(encoding='utf-8-sig')
    (STAGE / target).write_text(text, encoding='utf-8', newline='\n')
checks = []
for path in sorted(STAGE.rglob('*')):
    if path.is_file() and path.name != 'SHA256SUMS':
        checks.append(f'{file_hash(path)}  {path.relative_to(STAGE).as_posix()}')
(STAGE / 'SHA256SUMS').write_text('\n'.join(checks)+'\n', encoding='utf-8', newline='\n')
EXPORT.mkdir(exist_ok=True)
archive = EXPORT / 'svnebula-native-20261030.tar.gz'
assert not archive.exists(), 'An archive already exists; preserve it before rebuilding'
with tarfile.open(archive, 'w:gz', compresslevel=5) as tar:
    for path in sorted(STAGE.rglob('*')):
        tar.add(path, arcname='svnebula-native-20261030/'+path.relative_to(STAGE).as_posix(), recursive=False)
checksum = file_hash(archive)
(EXPORT / 'svnebula-native-20261030.tar.gz.sha256').write_text(checksum+'  '+archive.name+'\n', encoding='utf-8')
print(json.dumps({'archive': str(archive), 'bytes': archive.stat().st_size,
                  'sha256': checksum, 'databaseCounts': counts, 'missingLocalMedia': missing}, ensure_ascii=False))
