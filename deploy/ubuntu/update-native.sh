#!/usr/bin/env bash
set -Eeuo pipefail
umask 027
[[ $EUID == 0 ]] || { echo 'Run with sudo bash update.sh'; exit 1; }
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
sha256sum -c SHA256SUMS >/dev/null
source ./prepare-ocr.sh
ensure_ocr_python
if ! command -v rsync >/dev/null || ! command -v curl >/dev/null; then
    apt-get update
    DEBIAN_FRONTEND=noninteractive apt-get install -y rsync curl
fi
base=/opt/svnebula-native
unit=/etc/systemd/system/svnebula.service
previous=$(readlink -f "$base/current")
[[ $previous == "$base/releases/"* && -f $previous/Server/ShadowverseBeyond.Server.dll ]] || { echo 'Native installation not found'; exit 1; }
stamp=$(date -u +%Y%m%dT%H%M%SZ)
release="$base/releases/update-$stamp-$$"
backup="$base/backups/update-$stamp-$$"
active_db="$base/shared/data/shadowverse.db"
mkdir -p "$release" "$backup"
rsync -a --exclude=/assets app/ "$release/"
# Production overrides belong to this server, not to the build machine.
if [[ -f $previous/Server/appsettings.Production.json ]]; then
    cp "$previous/Server/appsettings.Production.json" "$release/Server/appsettings.Production.json"
fi
rm -f -- "$release/index.prod.html"
ln -s "$base/shared/assets" "$release/assets"
ln -s "$base/shared/logs" "$release/Server/Logs"
chown -R svnebula:svnebula "$release"
chmod -R u=rwX,g=rX,o= "$release"
printf '%s\n' "$previous" > "$backup/previous-release.txt"
cp -a "$unit" "$backup/svnebula.service"
restart_started=false
ocr_activated=false
previous_ocr_venv=''
rollback() {
    status=$?
    [[ $status -ne 0 ]] || status=1
    trap - ERR INT TERM
    if [[ $restart_started == true ]]; then
        systemctl stop svnebula
        if [[ -f $backup/shadowverse.db ]]; then
            rm -f -- "$active_db-wal" "$active_db-shm"
            cp "$backup/shadowverse.db" "$active_db"
            chown svnebula:svnebula "$active_db"
            chmod 0640 "$active_db"
        fi
        [[ $ocr_activated != true ]] || restore_ocr_environment "$base/shared/ocr-lab"
        cp -a "$backup/svnebula.service" "$unit"
        ln -s "$previous" "$base/rollback-$$"
        mv -Tf "$base/rollback-$$" "$base/current"
        systemctl daemon-reload
        systemctl start svnebula
        echo "Update failed; previous application, database and OCR environment restored. Backup: $backup"
    else
        echo "Update preparation failed; the running application was not stopped. Backup: $backup"
    fi
    exit "$status"
}
trap rollback ERR INT TERM
prepare_ocr_environment "$release/tools/ocr-lab" "$base/shared/ocr-lab"
rsync -a app/assets/ "$base/shared/assets/"
chown -R svnebula:svnebula "$base/shared/assets" "$base/shared/ocr-lab"
chmod -R u=rwX,g=rX,o= "$base/shared/assets" "$base/shared/ocr-lab"
python3 verify-native.py --manifest bundle.json --app "$release" --ocr-python "$ocr_venv/bin/python"
runuser -u svnebula -- test -w "$base/shared/assets"
restart_started=true
systemctl stop svnebula
python3 - "$active_db" "$backup/shadowverse.db.pending" <<'PY'
import sqlite3, sys
from pathlib import Path
source = Path(sys.argv[1]).resolve()
with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as db:
    with sqlite3.connect(sys.argv[2]) as target:
        db.backup(target)
        if target.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
            raise RuntimeError('Database backup integrity check failed')
PY
mv -- "$backup/shadowverse.db.pending" "$backup/shadowverse.db"
ocr_activated=true
activate_ocr_environment "$base/shared/ocr-lab"
printf '%s\n' "$previous_ocr_venv" > "$backup/previous-ocr-venv.txt"
install -m 0644 svnebula-native.service "$unit"
ln -s "$release" "$base/switch-$$"
mv -Tf "$base/switch-$$" "$base/current"
systemctl daemon-reload
systemctl start svnebula
ready=false
for attempt in {1..60}; do
    if curl -fsS --max-time 2 http://127.0.0.1:5173/api/take-two/settings/as2p >/dev/null 2>&1; then ready=true; break; fi
    systemctl is-active --quiet svnebula || break
    sleep 1
done
[[ $ready == true ]] || { journalctl -u svnebula -n 30 --no-pager; false; }
python3 verify-native.py --manifest bundle.json --url http://127.0.0.1:5173
trap - ERR INT TERM
echo "Updated successfully: $release"
echo "Database retained and backed up at: $backup/shadowverse.db"
echo 'Refresh the browser with Ctrl+F5. Active battles were ended by the restart.'
