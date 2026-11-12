#!/usr/bin/env bash
# Sourced by install/update so both use the same dependency and activation rules.

ensure_ocr_python() {
    if ! command -v python3 >/dev/null || ! python3 -c 'import venv, ensurepip' >/dev/null 2>&1; then
        apt-get update
        DEBIAN_FRONTEND=noninteractive apt-get install -y python3 python3-venv
    fi
    python3 -c 'import sys; sys.exit("OCR requires Python >= 3.10") if sys.version_info < (3, 10) else None'
}

prepare_ocr_environment() {
    local lab=$1 shared=$2 fingerprint installed active
    fingerprint=$(python3 - "$lab" <<'PY'
import hashlib, platform, sys
from pathlib import Path
lab = Path(sys.argv[1])
digest = hashlib.sha256((sys.version + platform.machine()).encode())
for name in ('requirements.lock.txt', 'requirements.txt'):
    digest.update(name.encode())
    digest.update((lab / name).read_bytes())
print(digest.hexdigest())
PY
)
    ocr_venv="$shared/environments/$fingerprint"
    install -d -o svnebula -g svnebula -m 0750 "$shared" "$shared/environments"
    if [[ -e $shared/venv ]]; then
        active=$(readlink -f "$shared/venv")
        installed=$(cat "$active/dependency-fingerprint.txt" 2>/dev/null || true)
        if [[ $installed == "$fingerprint" ]] && "$active/bin/python" -c 'import rapidocr, onnxruntime, cv2, PIL, numpy'; then
            ocr_venv=$active
        elif [[ $active == "$ocr_venv" ]]; then
            ocr_venv="$shared/environments/$fingerprint-$$"
        fi
    fi
    installed=$(cat "$ocr_venv/dependency-fingerprint.txt" 2>/dev/null || true)
    if [[ $installed == "$fingerprint" ]] && "$ocr_venv/bin/python" -c 'import rapidocr, onnxruntime, cv2, PIL, numpy'; then
        echo "Reusing OCR environment: $ocr_venv"
    else
        # A failed preparation is never reused, and the active environment is untouched.
        [[ $ocr_venv == "$shared/environments/"* && -n $fingerprint ]]
        rm -rf -- "$ocr_venv"
        python3 -m venv "$ocr_venv"
        local requirements=requirements.txt
        if python3 -c 'import sys; sys.exit(0 if sys.version_info >= (3, 12) else 1)'; then
            if "$ocr_venv/bin/python" -m pip install --only-binary=:all: -r "$lab/requirements.lock.txt"; then
                requirements=requirements.lock.txt
            else
                echo 'Locked wheels are unavailable for this platform; installing Python-compatible requirements.txt.'
                "$ocr_venv/bin/python" -m pip install -r "$lab/requirements.txt"
            fi
        else
            echo 'Python 3.10/3.11 uses requirements.txt; the lock requires Python >= 3.12.'
            "$ocr_venv/bin/python" -m pip install -r "$lab/requirements.txt"
        fi
        "$ocr_venv/bin/python" -c 'import rapidocr, onnxruntime, cv2, PIL, numpy'
        "$ocr_venv/bin/python" -m pip check
        "$ocr_venv/bin/python" -m pip freeze > "$ocr_venv/installed-packages.txt"
        "$ocr_venv/bin/python" - "$ocr_venv" "$fingerprint" "$requirements" <<'PY'
import json, platform, sys
from pathlib import Path
root = Path(sys.argv[1])
(root / 'environment.json').write_text(json.dumps({
    'dependencyFingerprint': sys.argv[2], 'requirements': sys.argv[3],
    'python': sys.version, 'platform': platform.platform()
}, indent=2), encoding='utf-8')
(root / 'dependency-fingerprint.txt').write_text(sys.argv[2] + '\n', encoding='utf-8')
PY
    fi
    chown -R svnebula:svnebula "$shared"
    chmod -R u=rwX,g=rX,o= "$shared"
}

activate_ocr_environment() {
    local shared=$1
    previous_ocr_venv=''
    if [[ -e $shared/venv || -L $shared/venv ]]; then
        previous_ocr_venv=$(readlink -f "$shared/venv")
    fi
    if [[ -d $shared/venv && ! -L $shared/venv ]]; then
        previous_ocr_venv="$shared/environments/legacy-$(date -u +%Y%m%dT%H%M%SZ)-$$"
        mv -- "$shared/venv" "$previous_ocr_venv"
        if [[ $ocr_venv == "$shared/venv" ]]; then
            ocr_venv=$previous_ocr_venv
        fi
    fi
    ln -s "$ocr_venv" "$shared/activate-$$"
    mv -Tf "$shared/activate-$$" "$shared/venv"
    chown -h svnebula:svnebula "$shared/venv"
}

restore_ocr_environment() {
    local shared=$1
    if [[ -n $previous_ocr_venv ]]; then
        ln -s "$previous_ocr_venv" "$shared/restore-$$"
        mv -Tf "$shared/restore-$$" "$shared/venv"
        chown -h svnebula:svnebula "$shared/venv"
    else
        rm -f -- "$shared/venv"
    fi
}
