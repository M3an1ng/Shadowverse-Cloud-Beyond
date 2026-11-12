"""Package the native application, local media and a consistent SQLite snapshot."""

import argparse
import json
import shutil
import sqlite3
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import unquote, urlsplit

from native_bundle import (ROOT, add_bundle_arguments, copy_backend, copy_deployment_files,
                           copy_frontend, copy_ocr, create_stage, write_archive)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    add_bundle_arguments(parser)
    parser.add_argument("--database", type=Path, required=True,
                        help="Read-only source SQLite database for the initial snapshot")
    args = parser.parse_args()
    if not args.database.is_file():
        raise FileNotFoundError(f"Database snapshot source missing: {args.database}")
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    name, stage = create_stage(args, "svnebula-native-" + stamp.lower())
    app = stage / "app"
    copy_backend(args.publish_dir, app / "Server")
    manifest = copy_frontend(args.dist_dir, app)
    # Uploads belong only to the initial full bundle; code updates never copy this tree.
    shutil.copytree(ROOT / "assets", app / "assets", dirs_exist_ok=True)
    copy_ocr(args.model_dir, app)
    seed = stage / "seed/shadowverse.db"
    seed.parent.mkdir()
    with sqlite3.connect(args.database.resolve().as_uri() + "?mode=ro", uri=True) as source:
        with sqlite3.connect(seed) as target:
            source.backup(target)
    with sqlite3.connect(seed) as db:
        if db.execute("PRAGMA integrity_check").fetchone()[0] != "ok" or db.execute("PRAGMA foreign_key_check").fetchall():
            raise ValueError("Database snapshot integrity check failed")
        db.execute("PRAGMA journal_mode=DELETE")
        tables = ("Users", "UserDecks", "UserDeckCards", "CardDefinitions", "CardAbilities",
                  "CardSetDefinitions", "FeatureDocuments")
        counts = {table: db.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0] for table in tables}
        media = {value for row in db.execute("SELECT ArtPreview,ArtDetail FROM CardDefinitions") for value in row if value}
        missing = sorted(value for value in media if value.startswith("/") and
                         not (app / unquote(urlsplit(value).path).lstrip("/")).is_file())
        if missing:
            raise FileNotFoundError("Bundled card media missing: " + ", ".join(missing))
        probes = [(card_id, image) for card_id, image in db.execute(
            "SELECT Id,ArtPreview FROM CardDefinitions WHERE ArtPreview IS NOT NULL ORDER BY Id")
            if image.startswith("/assets/") and (app / unquote(urlsplit(image).path).lstrip("/")).is_file()]
        if not probes:
            raise ValueError("Database has no local card image to verify")
        saved_pools = {}
        for key, value in db.execute("SELECT Id,Json FROM FeatureDocuments WHERE Id LIKE 'take-two-saved:%'"):
            _, owner, pool_id = key.split(":", 2)
            saved_pools.setdefault(owner, []).append({"id": pool_id, "config": json.loads(value)})
    manifest.update(format=2, kind="full", releaseId=name, builtAt=stamp, databaseCounts=counts,
                    probeCardId=probes[0][0], probeImage=probes[0][1], savedPoolProbes=saved_pools,
                    missingLocalMedia=missing,
                    databasePolicy="Initial install uses this snapshot; updates retain the online database.")
    (stage / "bundle.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    copy_deployment_files(stage, "install-native.sh")
    write_archive(stage, name, args.output_dir)


if __name__ == "__main__":
    main()
