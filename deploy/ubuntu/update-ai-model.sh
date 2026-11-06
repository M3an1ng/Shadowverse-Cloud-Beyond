#!/usr/bin/env bash
set -Eeuo pipefail
[[ $EUID == 0 ]] || { echo 'Run with sudo bash update-ai-model.sh /home/ubuntu/svnebula-ai-model.json'; exit 1; }
source_model=$(readlink -f "${1:?Model file required}")
base=/opt/svnebula-native
[[ -f $source_model && -f $base/current/Server/ShadowverseBeyond.Server.dll ]]
# Validation runs against the same inference code, before stopping the website.
dotnet "$base/current/Server/ShadowverseBeyond.Server.dll" --validate-ai-model "$source_model"
stamp=$(date -u +%Y%m%dT%H%M%SZ)-$$
folder="$base/shared/data/ai-model"
backup="$base/backups/ai-model-$stamp"
mkdir -p "$folder" "$backup" /etc/systemd/system/svnebula.service.d
[[ ! -f $folder/model.json ]] || cp -a "$folder/model.json" "$backup/model.json"
override=/etc/systemd/system/svnebula.service.d/ai-model.conf
[[ ! -f $override ]] || cp -a "$override" "$backup/ai-model.conf"
cp "$source_model" "$folder/model.json.new"
chown -R svnebula:svnebula "$folder"
chmod 750 "$folder"
chmod 640 "$folder/model.json.new"
rollback() {
 trap - ERR INT TERM
 if [[ -f $backup/model.json ]]; then cp -a "$backup/model.json" "$folder/model.json"; else rm -f "$folder/model.json"; fi
 if [[ -f $backup/ai-model.conf ]]; then cp -a "$backup/ai-model.conf" "$override"; else rm -f "$override"; fi
 systemctl daemon-reload
 systemctl restart svnebula || true
 echo "Model update failed; previous model restored. Backup: $backup"
 exit 1
}
trap rollback ERR INT TERM
mv -f "$folder/model.json.new" "$folder/model.json"
printf '[Service]\nEnvironment=AiModel__Path=%s/model.json\nEnvironment=LocalAiTraining__Enabled=false\n' "$folder" > "$override"
systemctl daemon-reload
systemctl restart svnebula
ready=false
for attempt in {1..60}; do
 if curl -fsS --max-time 2 http://127.0.0.1:5173/api/take-two/settings/as2p >/dev/null; then ready=true; break; fi
 sleep 1
done
[[ $ready == true ]]
trap - ERR INT TERM
echo "AI model updated. Backup: $backup. No decks or player data imported."
