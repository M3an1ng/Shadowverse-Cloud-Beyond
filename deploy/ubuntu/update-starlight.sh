#!/usr/bin/env bash
set -Eeuo pipefail
[[ $EUID == 0 ]] || { echo 'Run with sudo bash update-starlight.sh'; exit 1; }
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
sha256sum -c SHA256SUMS >/dev/null
database=/opt/svnebula-native/shared/data/shadowverse.db
[[ -f $database ]] || { echo 'Native database not found'; exit 1; }
# Preflight is read-only; a conflicting online edit aborts without touching the service.
dotnet tool/CardPackMigration.dll "$database" starlight-patch.json
restart() { systemctl start svnebula; }
trap restart EXIT
systemctl stop svnebula
# The importer validates again under a transaction, makes an online SQLite backup,
# and adds native card-version history. It never replaces the player database.
dotnet tool/CardPackMigration.dll "$database" starlight-patch.json --apply
systemctl start svnebula
ready=false
for attempt in {1..60}; do
    if curl -fsS --max-time 2 http://127.0.0.1:5173/api/take-two/settings/as2p >/dev/null 2>&1; then ready=true; break; fi
    sleep 1
done
[[ $ready == true ]]
trap - EXIT
echo 'Starlight card data updated. Player data and other card packs retained.'
echo 'Previous configurations are available in card version history. Refresh with Ctrl+F5.'
