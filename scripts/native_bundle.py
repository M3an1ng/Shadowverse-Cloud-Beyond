"""Shared file selection for native application bundles and code updates."""

import hashlib
import json
import os
import re
import shutil
import tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
UI_DIRECTORIES = ("card-ui", "fonts", "reference-cost", "reference-icons")
OCR_FILES = ("server.py", "recognition.py", "config.json", "requirements.txt", "requirements.lock.txt")
OCR_MODELS = ("ch_PP-OCRv5_det_mobile.onnx", "ch_PP-OCRv5_rec_mobile.onnx")


def add_bundle_arguments(parser):
    parser.add_argument("name", nargs="?")
    parser.add_argument("--publish-dir", type=Path, default=ROOT / "Server/publish")
    parser.add_argument("--dist-dir", type=Path, default=ROOT / "dist")
    parser.add_argument("--model-dir", type=Path,
                        default=Path(os.environ.get("OCR_LAB_MODEL_DIR", ROOT / ".local/ocr-lab/models")))
    parser.add_argument("--stage-dir", type=Path)
    parser.add_argument("--output-dir", type=Path, default=ROOT / "exports")


def create_stage(args, default_name):
    name = args.name or default_name
    if not re.fullmatch(r"svnebula-native-[a-z0-9-]+", name):
        raise ValueError("Bundle name must match svnebula-native-[a-z0-9-]+")
    stage = args.stage_dir or ROOT / ".local-run" / name
    if stage.exists():
        raise FileExistsError(f"Staging directory already exists: {stage}")
    if (args.output_dir / f"{name}.tar.gz").exists():
        raise FileExistsError(f"Archive already exists: {name}.tar.gz")
    stage.mkdir(parents=True)
    return name, stage


def copy_backend(published, destination):
    for required in ("ShadowverseBeyond.Server.dll", "runtimes/linux-x64/native/libe_sqlite3.so"):
        if not (published / required).is_file():
            raise FileNotFoundError(f"Backend publish file missing: {published / required}")
    shutil.copytree(published, destination, ignore=shutil.ignore_patterns(
        "runtimes", "Logs", "App_Data", "Data", "appsettings.Development.json", "*.db", "*.db-*", "*.sqlite*", "*.env"))
    for runtime in ("linux-x64", "win-x64"):
        source = published / "runtimes" / runtime
        if source.is_dir():
            shutil.copytree(source, destination / "runtimes" / runtime)
    (destination / "appsettings.Production.json").write_text(json.dumps({
        "Logging": {"LogLevel": {"Default": "Warning", "Microsoft.AspNetCore": "Warning"}},
        "Serilog": {"MinimumLevel": {"Default": "Warning", "Override": {"Microsoft": "Warning"}}},
        "AllowedHosts": "*",
        "Replay": {"EnableJournalWrites": False, "PersistClientDtos": False}
    }, indent=2), encoding="utf-8")


def copy_frontend(dist, app):
    required = ["index.html", "mse-beyond/card_mask.png", "manifest.webmanifest", "sw.js", "favicon.jpg",
                "pwa-icon-192.png", "pwa-icon-512.png"]
    required.extend(f"mse-beyond/fonts/{name}.woff2" for name in
                    ("weibei-sc", "weibei-tc", "noto-sans-sc", "noto-sans-tc"))
    for relative in required:
        if not (dist / relative).is_file():
            raise FileNotFoundError(f"Frontend build file missing: {dist / relative}")
    shutil.copytree(dist, app, dirs_exist_ok=True)
    probes = ["/" + relative for relative in required if relative != "index.html"]
    for name in UI_DIRECTORIES:
        source = ROOT / "assets" / name
        if not source.is_dir():
            raise FileNotFoundError(f"Fixed UI resource directory missing: {source}")
        shutil.copytree(source, app / "assets" / name, dirs_exist_ok=True)
        probe = next((path for path in sorted(source.rglob("*")) if path.is_file()), None)
        if probe is None:
            raise ValueError(f"Fixed UI resource directory is empty: {source}")
        probes.append("/assets/" + name + "/" + probe.relative_to(source).as_posix())
    (app / "data").mkdir(exist_ok=True)
    shutil.copy2(ROOT / "data/about-changelog.json", app / "data/about-changelog.json")
    (app / "robots.txt").write_text("User-agent: *\nDisallow: /\n", encoding="utf-8")
    html = (app / "index.html").read_text(encoding="utf-8")
    match = re.search(r'<script\b[^>]*\bsrc="([^"]+)"', html)
    if match is None:
        raise ValueError("Frontend index.html has no script entry")
    return {"entryScript": match.group(1), "staticProbes": probes}


def copy_ocr(model_source, app):
    lab = app / "tools/ocr-lab"
    (lab / "models").mkdir(parents=True)
    for name in OCR_FILES:
        shutil.copy2(ROOT / "tools/ocr-lab" / name, lab / name)
    for name in OCR_MODELS:
        source = model_source / name
        if not source.is_file():
            raise FileNotFoundError(f"Required mobile OCR model missing: {source}")
        shutil.copy2(source, lab / "models" / name)


def copy_deployment_files(stage, entry_script):
    files = {entry_script: "install.sh" if entry_script == "install-native.sh" else "update.sh",
             "prepare-ocr.sh": "prepare-ocr.sh", "svnebula-native.service": "svnebula-native.service",
             "update-ai-model.sh": "update-ai-model.sh", "verify-native.py": "verify-native.py"}
    if entry_script == "install-native.sh":
        files["rollback-mini.sh"] = "rollback-mini.sh"
    for source, target in files.items():
        content = (ROOT / "deploy/ubuntu" / source).read_text(encoding="utf-8-sig")
        (stage / target).write_text(content, encoding="utf-8", newline="\n")


def file_hash(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_archive(stage, name, output):
    files = sorted(path for path in stage.rglob("*") if path.is_file())
    (stage / "SHA256SUMS").write_text("".join(
        f"{file_hash(path)}  {path.relative_to(stage).as_posix()}\n" for path in files),
        encoding="utf-8", newline="\n")
    output.mkdir(parents=True, exist_ok=True)
    archive = output / f"{name}.tar.gz"
    with tarfile.open(archive, "w:gz", compresslevel=5) as tar:
        tar.add(stage, arcname=name)
    archive.with_suffix(".tar.gz.sha256").write_text(f"{file_hash(archive)}  {archive.name}\n", encoding="utf-8")
    print(json.dumps({"archive": str(archive), "bytes": archive.stat().st_size}, ensure_ascii=False))
