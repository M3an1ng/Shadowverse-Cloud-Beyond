#!/usr/bin/env bash
# Switch the existing Mini server to Cloud Beyond. Never opens a public backend port.
set -Eeuo pipefail
umask 027
[[ $EUID -eq 0 ]] || { echo 'Run with sudo bash install.sh'; exit 1; }
[[ $(uname -m) == x86_64 ]] || { echo 'This bundle requires an x86_64 server.'; exit 1; }
bundle=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
cd "$bundle"
sha256sum --quiet -c SHA256SUMS
release_id=$(python3 -c 'import json; print(json.load(open("bundle.json"))["releaseId"])')
[[ $release_id =~ ^[a-zA-Z0-9-]+$ ]] || { echo 'Invalid release ID'; exit 1; }
base=/opt/svnebula-native
mini=/opt/svnebula
unit=/etc/systemd/system/svnebula.service
[[ -f $unit && -d $mini/shared/data ]] || { echo 'The existing Mini installation was not found.'; exit 1; }
if ! command -v rsync >/dev/null || ! command -v dotnet >/dev/null || ! dotnet --list-runtimes | grep -q '^Microsoft.AspNetCore.App 8\.'; then
    apt-get update
    DEBIAN_FRONTEND=noninteractive apt-get install -y aspnetcore-runtime-8.0 rsync curl python3
fi
[[ -x /usr/bin/dotnet ]] || { echo 'System .NET runtime missing.'; exit 1; }
dotnet --list-runtimes | grep -q '^Microsoft.AspNetCore.App 8\.'
python3 verify-native.py --manifest bundle.json --database seed/shadowverse.db --exact
id svnebula >/dev/null
install -d -o root -g svnebula -m 0750 "$base" "$base/releases" "$base/bin"
install -d -o svnebula -g svnebula -m 0750 "$base/shared" "$base/shared/data" "$base/shared/assets" "$base/shared/logs"
[[ ! -e $base/releases/$release_id ]] || { echo "Release already exists: $release_id. Use a new bundle or inspect the previous attempt."; exit 1; }
release="$base/releases/$release_id"
mkdir "$release"
for item in "$bundle"/app/*; do
    [[ $(basename "$item") == assets ]] && continue
    cp -a "$item" "$release/"
done
rsync -a "$bundle/app/assets/" "$base/shared/assets/"
ln -s "$base/shared/assets" "$release/assets"
ln -s "$base/shared/logs" "$release/Server/Logs"
chown -R svnebula:svnebula "$release" "$base/shared/assets"
chmod -R u=rwX,g=rX,o= "$release" "$base/shared/assets"
check_unit="svnebula-native-check-$$"
check_db="$base/shared/data/check-$$.db"
active_db="$base/shared/data/shadowverse.db"
previous_native=$(readlink -f "$base/current" 2>/dev/null || true)
backup="$mini/backups/before-native-$release_id-$(date -u +%Y%m%dT%H%M%SZ)-$$"
switched=false
was_active=false
systemctl is-active --quiet svnebula && was_active=true
cleanup_check() {
    systemctl stop "$check_unit" >/dev/null 2>&1 || true
    systemctl reset-failed "$check_unit" >/dev/null 2>&1 || true
    rm -f -- "$check_db" "$check_db-wal" "$check_db-shm"
}
recover() {
    trap - ERR INT TERM
    cleanup_check
    if [[ $switched == true ]]; then
        systemctl stop svnebula || true
        cp -a "$backup/svnebula.service" "$unit"
        if [[ -n $previous_native ]]; then
            ln -s "$previous_native" "$base/restore-$$"
            mv -Tf "$base/restore-$$" "$base/current"
        fi
        if [[ -f $backup/native.db ]]; then
            rm -f -- "$active_db-wal" "$active_db-shm"
            cp "$backup/native.db" "$active_db"
            chown svnebula:svnebula "$active_db"
        fi
        systemctl daemon-reload
        [[ $was_active == true ]] && systemctl start svnebula
        echo "Switch failed; the previous service configuration was restored. Backup: $backup"
    else
        echo 'Preflight failed. The currently running website has not been stopped.'
    fi
    exit 1
}
trap recover ERR INT TERM
source_db="$bundle/seed/shadowverse.db"
[[ -f $active_db ]] && source_db="$active_db"
python3 - "$source_db" "$check_db" <<'PY'
import sqlite3,sys
from pathlib import Path
with sqlite3.connect(Path(sys.argv[1]).resolve().as_uri()+'?mode=ro',uri=True) as source:
    with sqlite3.connect(sys.argv[2]) as target: source.backup(target)
PY
chown svnebula:svnebula "$check_db"
systemd-run --quiet --unit="$check_unit" --uid=svnebula --gid=svnebula --working-directory="$release/Server" \
    --property=PrivateTmp=true --property=NoNewPrivileges=true \
    /usr/bin/env ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://127.0.0.1:15175 \
    "ConnectionStrings__DefaultConnection=Data Source=$check_db" \
    /usr/bin/dotnet "$release/Server/ShadowverseBeyond.Server.dll"
ready=false
for attempt in {1..90}; do
    if curl -fsS --max-time 2 http://127.0.0.1:15175/api/take-two/settings/as2p >/dev/null 2>&1; then ready=true; break; fi
    systemctl is-active --quiet "$check_unit" || break
    sleep 1
done
[[ $ready == true ]] || { journalctl -u "$check_unit" -n 15 --no-pager; false; }
python3 verify-native.py --manifest bundle.json --url http://127.0.0.1:15175
cleanup_check
# Back up the old Mini data only after its writers have stopped.
install -d -m 0700 "$backup"
cp -a "$unit" "$backup/svnebula.service"
printf '%s\n' "$previous_native" > "$backup/previous-native.txt"
printf '%s\n' "$was_active" > "$backup/was-active.txt"
readlink -f "$mini/current" > "$backup/mini-release.txt" || true
switched=true
systemctl stop svnebula
cp -a "$mini/shared/data" "$backup/mini-data"
[[ ! -f $mini/shared/server.env ]] || cp -a "$mini/shared/server.env" "$backup/mini-server.env"
if [[ -f $active_db ]]; then
    python3 - "$active_db" "$backup/native.db" <<'PY'
import sqlite3,sys
with sqlite3.connect(sys.argv[1]) as source:
    with sqlite3.connect(sys.argv[2]) as target: source.backup(target)
PY
else
    install -o svnebula -g svnebula -m 0640 "$bundle/seed/shadowverse.db" "$active_db"
fi
install -m 0644 "$bundle/svnebula-native.service" "$unit"
install -m 0750 "$bundle/rollback-mini.sh" "$base/bin/rollback-mini.sh"
printf '%s\n' "$backup" > "$base/bin/last-backup.txt"
chmod 0600 "$base/bin/last-backup.txt"
ln -s "$release" "$base/switch-$$"
mv -Tf "$base/switch-$$" "$base/current"
systemctl daemon-reload
systemctl enable svnebula >/dev/null
systemctl start svnebula
ready=false
for attempt in {1..90}; do
    if curl -fsS --max-time 2 http://127.0.0.1:5173/api/take-two/settings/as2p >/dev/null 2>&1; then ready=true; break; fi
    sleep 1
done
[[ $ready == true ]] || { journalctl -u svnebula -n 15 --no-pager; false; }
python3 verify-native.py --manifest bundle.json --url http://127.0.0.1:5173
# Confirm the existing Nginx domain/IP route reaches the new application too.
python3 verify-native.py --manifest bundle.json --url http://127.0.0.1 --http-host 110.40.204.152
trap - ERR INT TERM
echo "Cloud Beyond is live: $release"
echo "Old Mini data backed up at: $backup/mini-data"
echo 'Nginx and the public URL are unchanged. Refresh the browser and log in with a local-new-site account.'
echo 'Rollback: sudo bash /opt/svnebula-native/bin/rollback-mini.sh'
