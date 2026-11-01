#!/usr/bin/env bash
set -Eeuo pipefail
umask 027
[[ $EUID -eq 0 ]] || { echo 'Run with sudo'; exit 1; }
base=/opt/svnebula-native
backup=$(cat "$base/bin/last-backup.txt")
[[ $backup == /opt/svnebula/backups/before-native-* && -f $backup/svnebula.service ]] || { echo 'Valid saved service configuration missing.'; exit 1; }
systemctl stop svnebula
# The old Mini application, data and environment remain in their original paths.
# Retain the newer database too, in case the operator switches back later.
cp -a "$backup/svnebula.service" /etc/systemd/system/svnebula.service
previous=$(cat "$backup/previous-native.txt")
if [[ -n $previous ]]; then
    [[ $previous == "$base/releases/"* && -d $previous ]] || { echo 'Invalid previous release.'; exit 1; }
    ln -s "$previous" "$base/rollback-$$"
    mv -Tf "$base/rollback-$$" "$base/current"
fi
systemctl daemon-reload
systemctl start svnebula
echo 'Previous service restored. Current native database has been retained.'
echo 'Check: sudo systemctl status svnebula --no-pager'
