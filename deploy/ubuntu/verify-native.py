"""Read-only database and HTTP checks used before and after switching services."""
import argparse
import json
import sqlite3
import os
import subprocess
import urllib.request
import urllib.error
from pathlib import Path


def check_app(app, manifest, ocr_python):
    app = Path(app).resolve()
    paths = [app / "index.html", app / "Server/ShadowverseBeyond.Server.dll"]
    paths.extend(app / relative.lstrip("/") for relative in manifest["staticProbes"])
    lab = app / "tools/ocr-lab"
    paths.extend(lab / name for name in ("server.py", "recognition.py", "config.json",
                                        "requirements.txt", "requirements.lock.txt"))
    expected_models = {"ch_PP-OCRv5_det_mobile.onnx", "ch_PP-OCRv5_rec_mobile.onnx"}
    paths.extend(lab / "models" / name for name in expected_models)
    missing = [str(path) for path in paths if not path.is_file()]
    if missing:
        raise FileNotFoundError("Application files missing: " + ", ".join(missing))
    if {path.name for path in (lab / "models").iterdir()} != expected_models:
        raise ValueError("Production OCR models must contain exactly the two mobile ONNX files")
    environment = os.environ.copy()
    environment["OCR_LAB_MODEL_DIR"] = str(lab / "models")
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    subprocess.run([ocr_python, "-c", """
import sys
sys.path.insert(0, sys.argv[1])
from recognition import MODEL_DIR, _ensure_templates
import onnxruntime
_ensure_templates()
options = onnxruntime.SessionOptions()
options.intra_op_num_threads = 2
options.inter_op_num_threads = 1
for name in ('ch_PP-OCRv5_det_mobile.onnx', 'ch_PP-OCRv5_rec_mobile.onnx'):
    session = onnxruntime.InferenceSession(str(MODEL_DIR / name), sess_options=options,
                                         providers=['CPUExecutionProvider'])
    del session
print('OCR imports, templates and both mobile ONNX models verified.')
""", str(lab)], env=environment, check=True)


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
    for path in manifest['staticProbes']:
        request(base, path)
    for path in ('/Server/appsettings.json', '/tools/ocr-lab/config.json',
                 '/tools/ocr-lab/requirements.txt', '/.local/ocr-lab/models/probe.onnx'):
        try:
            request(base, path)
            raise AssertionError(f'Internal path is publicly accessible: {path}')
        except urllib.error.HTTPError as error:
            assert error.code == 404, f'{path}: expected 404, received {error.code}'
    sets = json.loads(request(base, '/api/cardsets'))
    assert isinstance(sets, list) and sets, 'Card sets unavailable'
    if manifest['kind'] == 'full':
        request(base, manifest['probeImage'])
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
    print('HTTP verified: frontend, MSE/font/UI assets, internal path blocking, card sets, AS2P and SignalR.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--manifest', required=True)
    parser.add_argument('--database')
    parser.add_argument('--exact', action='store_true')
    parser.add_argument('--url')
    parser.add_argument('--http-host')
    parser.add_argument('--app')
    parser.add_argument('--ocr-python')
    args = parser.parse_args()
    HTTP_HOST = args.http_host
    info = json.loads(Path(args.manifest).read_text(encoding='utf-8'))
    if args.app:
        if not args.ocr_python:
            parser.error('--app requires --ocr-python')
        check_app(args.app, info, args.ocr_python)
    if args.database:
        print(json.dumps(check_database(args.database, info, args.exact)))
    if args.url:
        check_http(args.url.rstrip('/'), info)
