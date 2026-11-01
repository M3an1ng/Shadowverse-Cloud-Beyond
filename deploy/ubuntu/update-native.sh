#!/usr/bin/env bash
set -Eeuo pipefail
[[ $EUID == 0 ]] || { echo 'Run with sudo bash update.sh'; exit 1; }
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
sha256sum -c SHA256SUMS >/dev/null
base=/opt/svnebula-native
previous=$(readlink -f "$base/current")
[[ $previous == "$base/releases/"* && -f $previous/Server/ShadowverseBeyond.Server.dll ]] || { echo 'Native installation not found'; exit 1; }
stamp=$(date -u +%Y%m%dT%H%M%SZ)
release="$base/releases/pool-local-$stamp-$$"
backup="$base/backups/pool-local-$stamp-$$"
mkdir -p "$release" "$backup"
cp -a "$previous/." "$release/"
cp -a app/Server/. "$release/Server/"
cp -a app/assets/. "$base/shared/assets/"
cp -a app/index.html "$release/index.html"
chown -R svnebula:svnebula "$release"
printf '%s\n' "$previous" > "$backup/previous-release.txt"
restart_started=false
rollback() {
    status=$?
    trap - ERR INT TERM
    if [[ $restart_started == true ]]; then
        ln -s "$previous" "$base/rollback-$$"
        mv -Tf "$base/rollback-$$" "$base/current"
        systemctl restart svnebula || true
    fi
    echo "Update failed; previous application restored. Backup: $backup"
    exit "$status"
}
trap rollback ERR INT TERM
systemctl stop svnebula
restart_started=true
python3 - "$base/shared/data/shadowverse.db" "$backup/shadowverse.db" <<'PY'
import sqlite3, sys
from pathlib import Path
source = Path(sys.argv[1]).resolve()
with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as db:
    with sqlite3.connect(sys.argv[2]) as target:
        db.backup(target)
        assert target.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
PY
ln -s "$release" "$base/switch-$$"
mv -Tf "$base/switch-$$" "$base/current"
systemctl start svnebula
ready=false
for attempt in {1..60}; do
    if curl -fsS --max-time 2 http://127.0.0.1:5173/api/take-two/settings/as2p >/dev/null 2>&1; then ready=true; break; fi
    sleep 1
done
[[ $ready == true ]]
curl -fsS --max-time 10 http://127.0.0.1:5173/ | python3 -c 'import sys; from pathlib import Path; assert Path("entry-script.txt").read_text().strip() in sys.stdin.read(), "Frontend entry mismatch"'
trap - ERR INT TERM
echo "Updated successfully: $release"
echo "Database retained and backed up at: $backup/shadowverse.db"
echo 'Refresh the browser with Ctrl+F5. Active battles were ended by the restart.'
