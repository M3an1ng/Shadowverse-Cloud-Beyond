"""Read-only database and HTTP checks used before and after switching services."""
import argparse
import json
import sqlite3
import urllib.request
import urllib.error
from pathlib import Path


def check_database(path, manifest, exact=False):
    db = sqlite3.connect(Path(path).resolve().as_uri() + '?mode=ro', uri=True)
    try:
        assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok', 'Database integrity check failed'
        assert not db.execute('PRAGMA foreign_key_check').fetchall(), 'Database foreign key check failed'
        counts = {table: db.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0]
                  for table in manifest['databaseCounts']}
        if exact:
            assert counts == manifest['databaseCounts'], 'Seed database counts differ from manifest'
        assert counts['Users'] > 0 and counts['CardDefinitions'] > 0
        return counts
    finally:
        db.close()


HTTP_HOST = None


def request(base, path, *, post=False, headers=None):
    headers = dict(headers or {})
    if HTTP_HOST:
        headers['Host'] = HTTP_HOST
    req = urllib.request.Request(base + path, data=b'' if post else None,
                                 headers=headers or {}, method='POST' if post else 'GET')
    with urllib.request.urlopen(req, timeout=60) as response:
        assert response.status == 200, f'{path}: {response.status}'
        return response.read()


def check_http(base, manifest):
    page = request(base, '/').decode('utf-8')
    assert manifest['entryScript'] in page, 'The frontend does not match this release'
    request(base, manifest['entryScript'])
    request(base, manifest['probeImage'])
    try:
        request(base, '/Server/appsettings.json')
        raise AssertionError('Server configuration is publicly accessible')
    except urllib.error.HTTPError as error:
        assert error.code == 404
    sets = json.loads(request(base, '/api/cardsets'))
    assert isinstance(sets, list) and sets, 'Card sets unavailable'
    card = json.loads(request(base, f"/api/cards/{manifest['probeCardId']}"))
    assert card.get('id') == manifest['probeCardId'], 'Card data unavailable'
    request(base, '/api/take-two/settings/as2p')
    negotiation = json.loads(request(base, '/gamehub/negotiate?negotiateVersion=1', post=True))
    assert negotiation.get('connectionToken'), 'SignalR negotiation failed'
    for owner, expected in manifest.get('savedPoolProbes', {}).items():
        saved = json.loads(request(base, '/api/take-two/saved-pools', headers={'X-Editor-UserId': owner}))
        actual = {item['id']: item['config'] for item in saved}
        for item in expected:
            assert actual.get(item['id']) == item['config'], 'Saved pool configuration mismatch'
    print('HTTP verified: frontend, card image, card data, card sets, AS2P, SignalR and saved pools.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--manifest', required=True)
    parser.add_argument('--database')
    parser.add_argument('--exact', action='store_true')
    parser.add_argument('--url')
    parser.add_argument('--http-host')
    args = parser.parse_args()
    HTTP_HOST = args.http_host
    info = json.loads(Path(args.manifest).read_text(encoding='utf-8'))
    if args.database:
        print(json.dumps(check_database(args.database, info, args.exact)))
    if args.url:
        check_http(args.url.rstrip('/'), info)
