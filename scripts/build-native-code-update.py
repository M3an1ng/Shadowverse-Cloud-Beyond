"""Build an application-only update without player databases or uploaded media."""

import argparse
import json
from datetime import datetime, timezone

from native_bundle import (add_bundle_arguments, copy_backend, copy_deployment_files,
                           copy_frontend, copy_ocr, create_stage, write_archive)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    add_bundle_arguments(parser)
    args = parser.parse_args()
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    name, stage = create_stage(args, "svnebula-native-update-" + stamp.lower())
    app = stage / "app"
    copy_backend(args.publish_dir, app / "Server")
    manifest = copy_frontend(args.dist_dir, app)
    copy_ocr(args.model_dir, app)
    manifest.update(format=2, kind="code-update", releaseId=name, builtAt=stamp)
    (stage / "bundle.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    (stage / "entry-script.txt").write_text(manifest["entryScript"] + "\n", encoding="utf-8")
    copy_deployment_files(stage, "update-native.sh")
    write_archive(stage, name, args.output_dir)


if __name__ == "__main__":
    main()
