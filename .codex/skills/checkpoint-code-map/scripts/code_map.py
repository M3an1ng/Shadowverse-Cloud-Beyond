#!/usr/bin/env python3
"""Build and query a compact repository code map."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import pathlib
import re
import subprocess
from typing import Any

SKIP_DIRS = {
    ".git",
    ".hg",
    ".svn",
    "node_modules",
    "dist",
    "build",
    "bin",
    "obj",
    ".next",
    ".nuxt",
    ".cache",
    ".idea",
    ".vscode",
    "__pycache__",
    ".venv",
    "venv",
}

TEXT_EXTENSIONS = {
    ".cs",
    ".csproj",
    ".sln",
    ".py",
    ".js",
    ".ts",
    ".tsx",
    ".jsx",
    ".java",
    ".kt",
    ".go",
    ".rs",
    ".php",
    ".rb",
    ".swift",
    ".scala",
    ".c",
    ".h",
    ".cpp",
    ".hpp",
    ".m",
    ".mm",
    ".json",
    ".yml",
    ".yaml",
    ".toml",
    ".md",
    ".txt",
    ".xml",
    ".sql",
    ".vue",
    ".svelte",
}

CS_NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][\w\.]*)")
CS_TYPE_RE = re.compile(
    r"^\s*(?:public|private|protected|internal|abstract|sealed|partial|static|\s)+\s*"
    r"(class|interface|record|struct|enum)\s+([A-Za-z_]\w*)"
)
CS_METHOD_RE = re.compile(
    r"^\s*(?:public|private|protected|internal|virtual|override|abstract|static|async|sealed|new|\s)+"
    r"\s*[A-Za-z_<>\[\],\?\.\s]+\s+([A-Za-z_]\w*)\s*\([^;]*\)\s*(?:\{|=>)"
)

PY_CLASS_RE = re.compile(r"^\s*class\s+([A-Za-z_]\w*)")
PY_FUNC_RE = re.compile(r"^\s*def\s+([A-Za-z_]\w*)\s*\(")

JS_EXPORT_RE = re.compile(
    r"^\s*export\s+(?:default\s+)?(?:class|function|const|let|var|async function)\s+([A-Za-z_]\w*)"
)
JS_CLASS_RE = re.compile(r"^\s*class\s+([A-Za-z_]\w*)")
JS_FUNC_RE = re.compile(r"^\s*(?:function|async function)\s+([A-Za-z_]\w*)\s*\(")
JS_CONST_FUNC_RE = re.compile(
    r"^\s*(?:const|let|var)\s+([A-Za-z_]\w*)\s*=\s*(?:async\s+)?\([^)]*\)\s*=>"
)

GO_FUNC_RE = re.compile(r"^\s*func\s+(?:\([^)]+\)\s+)?([A-Za-z_]\w*)\s*\(")
JAVA_TYPE_RE = re.compile(r"^\s*(?:public\s+)?(?:class|interface|enum|record)\s+([A-Za-z_]\w*)")
JAVA_METHOD_RE = re.compile(
    r"^\s*(?:public|private|protected|static|final|synchronized|abstract|native|strictfp|\s)+"
    r"\s*[A-Za-z_<>\[\],\?\.\s]+\s+([A-Za-z_]\w*)\s*\([^;]*\)\s*\{"
)
RUST_ITEM_RE = re.compile(r"^\s*pub\s+(?:struct|enum|trait|fn)\s+([A-Za-z_]\w*)|^\s*(?:struct|enum|trait|fn)\s+([A-Za-z_]\w*)")

USING_RE = re.compile(r"^\s*using\s+([A-Za-z_][\w\.]+)\s*;")
IMPORT_RE = re.compile(
    r"^\s*import\s+(?:.+?\s+from\s+)?[\"']([^\"']+)[\"']|^\s*from\s+[\"']([^\"']+)[\"']\s+import"
)
PY_IMPORT_RE = re.compile(r"^\s*(?:from\s+([A-Za-z_][\w\.]*)\s+import|import\s+([A-Za-z_][\w\.]*))")


def run_git_ls_files(root: pathlib.Path) -> list[pathlib.Path]:
    try:
        proc = subprocess.run(
            ["git", "-C", str(root), "ls-files"],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        files = [line.strip() for line in proc.stdout.splitlines() if line.strip()]
        return [root / f for f in files]
    except Exception:
        return []


def walk_files(root: pathlib.Path) -> list[pathlib.Path]:
    results: list[pathlib.Path] = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS and not d.startswith(".")]
        base = pathlib.Path(dirpath)
        for name in filenames:
            p = base / name
            if p.suffix.lower() in TEXT_EXTENSIONS and not name.startswith("."):
                results.append(p)
    return results


def collect_files(root: pathlib.Path) -> list[pathlib.Path]:
    git_files = run_git_ls_files(root)
    if git_files:
        filtered = [
            p for p in git_files
            if p.suffix.lower() in TEXT_EXTENSIONS
            and all(part not in SKIP_DIRS for part in p.parts)
            and p.exists()
        ]
        return sorted(set(filtered))
    return sorted(set(walk_files(root)))


def relative_path(path: pathlib.Path, root: pathlib.Path) -> str:
    return str(path.resolve().relative_to(root.resolve())).replace("\\", "/")


def file_signature(path: pathlib.Path) -> dict[str, Any]:
    stat = path.stat()
    return {"size": stat.st_size, "mtime_ns": stat.st_mtime_ns}


def detect_language(path: pathlib.Path) -> str:
    ext = path.suffix.lower()
    return {
        ".cs": "csharp",
        ".py": "python",
        ".js": "javascript",
        ".jsx": "javascript",
        ".ts": "typescript",
        ".tsx": "typescript",
        ".go": "go",
        ".java": "java",
        ".rs": "rust",
    }.get(ext, ext.lstrip(".") or "text")


def parse_symbols(path: pathlib.Path, lines: list[str], language: str) -> tuple[list[dict[str, Any]], list[str]]:
    symbols: list[dict[str, Any]] = []
    imports: list[str] = []

    for idx, line in enumerate(lines, start=1):
        if language == "csharp":
            if m := CS_NAMESPACE_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "namespace", "line": idx})
            if m := CS_TYPE_RE.match(line):
                symbols.append({"name": m.group(2), "kind": m.group(1), "line": idx})
            if m := CS_METHOD_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "method", "line": idx})
            if m := USING_RE.match(line):
                imports.append(m.group(1))
        elif language == "python":
            if m := PY_CLASS_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "class", "line": idx})
            if m := PY_FUNC_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "function", "line": idx})
            if m := PY_IMPORT_RE.match(line):
                imports.append(m.group(1) or m.group(2))
        elif language in {"javascript", "typescript"}:
            if m := JS_EXPORT_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "export", "line": idx})
            if m := JS_CLASS_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "class", "line": idx})
            if m := JS_FUNC_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "function", "line": idx})
            if m := JS_CONST_FUNC_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "function", "line": idx})
            if m := IMPORT_RE.match(line):
                imports.append(m.group(1) or m.group(2))
        elif language == "go":
            if m := GO_FUNC_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "function", "line": idx})
        elif language == "java":
            if m := JAVA_TYPE_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "type", "line": idx})
            if m := JAVA_METHOD_RE.match(line):
                symbols.append({"name": m.group(1), "kind": "method", "line": idx})
        elif language == "rust":
            if m := RUST_ITEM_RE.match(line):
                name = m.group(1) or m.group(2)
                if name:
                    symbols.append({"name": name, "kind": "item", "line": idx})

    return symbols, sorted(set(imports))


def parse_file(path: pathlib.Path, root: pathlib.Path) -> dict[str, Any] | None:
    try:
        raw = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        try:
            raw = path.read_text(encoding="utf-8-sig")
        except Exception:
            return None
    except Exception:
        return None

    lines = raw.splitlines()
    language = detect_language(path)
    symbols, imports = parse_symbols(path, lines, language)

    return {
        "path": relative_path(path, root),
        "language": language,
        "line_count": len(lines),
        "signature": file_signature(path),
        "symbols": symbols,
        "imports": imports,
    }


def load_previous_map(path: pathlib.Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}


def build_map(root: pathlib.Path, out_path: pathlib.Path, summary_path: pathlib.Path | None, changed_only: bool) -> dict[str, Any]:
    files = collect_files(root)
    previous = load_previous_map(out_path) if changed_only else {}
    previous_entries = {
        e["path"]: e for e in previous.get("files", [])
        if isinstance(e, dict) and "path" in e and "signature" in e
    }

    entries: list[dict[str, Any]] = []
    scanned = 0
    reused = 0

    for file_path in files:
        if not file_path.exists():
            continue
        rel = relative_path(file_path, root)
        signature = file_signature(file_path)
        prev = previous_entries.get(rel)
        if prev and prev.get("signature") == signature:
            entries.append(prev)
            reused += 1
            continue

        parsed = parse_file(file_path, root)
        if parsed:
            entries.append(parsed)
            scanned += 1

    entries.sort(key=lambda e: e["path"])
    generated_at = dt.datetime.now(dt.timezone.utc).isoformat()

    map_data = {
        "version": 1,
        "root": str(root.resolve()),
        "generated_at": generated_at,
        "stats": {
            "file_count": len(entries),
            "scanned_count": scanned,
            "reused_count": reused,
        },
        "files": entries,
    }

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(map_data, ensure_ascii=False, indent=2), encoding="utf-8")

    if summary_path:
        summary_path.parent.mkdir(parents=True, exist_ok=True)
        summary_path.write_text(render_summary(map_data), encoding="utf-8")

    return map_data


def render_summary(map_data: dict[str, Any]) -> str:
    files = map_data.get("files", [])
    stats = map_data.get("stats", {})

    by_dir: dict[str, int] = {}
    symbol_heavy: list[tuple[str, int, str]] = []

    for entry in files:
        path = entry.get("path", "")
        dir_name = path.rsplit("/", 1)[0] if "/" in path else "."
        by_dir[dir_name] = by_dir.get(dir_name, 0) + 1
        symbol_heavy.append((path, len(entry.get("symbols", [])), entry.get("language", "text")))

    top_dirs = sorted(by_dir.items(), key=lambda x: x[1], reverse=True)[:15]
    top_symbols = sorted(symbol_heavy, key=lambda x: x[1], reverse=True)[:20]

    lines = [
        "# Code Map Summary",
        "",
        f"- generated_at: {map_data.get('generated_at', '')}",
        f"- file_count: {stats.get('file_count', 0)}",
        f"- scanned_count: {stats.get('scanned_count', 0)}",
        f"- reused_count: {stats.get('reused_count', 0)}",
        "",
        "## Top Directories",
    ]
    for d, count in top_dirs:
        lines.append(f"- {d}: {count}")

    lines.append("")
    lines.append("## Symbol-Heavy Files")
    for path, count, lang in top_symbols:
        lines.append(f"- {path} ({lang}): {count}")

    lines.append("")
    lines.append("## Entry Candidates")
    entry_candidates = [
        e for e in files
        if any(key in e.get("path", "").lower() for key in ("program", "startup", "app", "main", "index"))
    ][:20]
    if not entry_candidates:
        lines.append("- none")
    else:
        for e in entry_candidates:
            lines.append(f"- {e.get('path')}")

    return "\n".join(lines) + "\n"


def lookup(map_path: pathlib.Path, symbol: str | None, path_part: str | None, kind: str | None, limit: int) -> list[dict[str, Any]]:
    map_data = load_previous_map(map_path)
    results: list[dict[str, Any]] = []
    symbol_low = symbol.lower() if symbol else None
    path_low = path_part.lower() if path_part else None
    kind_low = kind.lower() if kind else None

    for entry in map_data.get("files", []):
        path_val = entry.get("path", "")

        if path_low and path_low not in path_val.lower():
            continue

        symbols = entry.get("symbols", [])
        if symbol_low or kind_low:
            matched = []
            for s in symbols:
                name = str(s.get("name", ""))
                skind = str(s.get("kind", ""))
                if symbol_low and symbol_low not in name.lower():
                    continue
                if kind_low and kind_low != skind.lower():
                    continue
                matched.append(s)
            if not matched:
                continue
        else:
            matched = symbols[:]

        results.append(
            {
                "path": path_val,
                "language": entry.get("language"),
                "line_count": entry.get("line_count"),
                "matches": matched[:50],
            }
        )

        if len(results) >= limit:
            break

    return results


def print_lookup(results: list[dict[str, Any]]) -> None:
    if not results:
        print("No matches.")
        return

    for entry in results:
        print(f"{entry['path']} [{entry.get('language', 'text')}, lines={entry.get('line_count', 0)}]")
        matches = entry.get("matches", [])
        if not matches:
            print("  (file matched; no symbol filter)")
            continue
        for m in matches:
            print(f"  - {m.get('kind', 'symbol')} {m.get('name', '')} @ line {m.get('line', '?')}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build and query a repository code map.")
    sub = parser.add_subparsers(dest="command", required=True)

    build_parser = sub.add_parser("build", help="Build or refresh code map.")
    build_parser.add_argument("--root", default=".", help="Repository root path.")
    build_parser.add_argument("--out", default=".codex/code-map.json", help="Output JSON map path.")
    build_parser.add_argument("--summary", default=".codex/code-map.md", help="Output markdown summary path.")
    build_parser.add_argument("--changed-only", action="store_true", help="Reuse unchanged file entries from previous map.")

    lookup_parser = sub.add_parser("lookup", help="Lookup files/symbols in map.")
    lookup_parser.add_argument("--map", default=".codex/code-map.json", help="Map JSON path.")
    lookup_parser.add_argument("--symbol", help="Symbol name contains filter.")
    lookup_parser.add_argument("--path", dest="path_part", help="Path contains filter.")
    lookup_parser.add_argument("--kind", help="Symbol kind exact filter (case-insensitive).")
    lookup_parser.add_argument("--limit", type=int, default=20, help="Max file matches.")

    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.command == "build":
        root = pathlib.Path(args.root).resolve()
        out_path = pathlib.Path(args.out)
        summary = pathlib.Path(args.summary) if args.summary else None
        map_data = build_map(root, out_path, summary, args.changed_only)
        stats = map_data.get("stats", {})
        print(
            "Built map:",
            f"files={stats.get('file_count', 0)}",
            f"scanned={stats.get('scanned_count', 0)}",
            f"reused={stats.get('reused_count', 0)}",
            f"out={out_path}",
        )
        if summary:
            print(f"Summary: {summary}")
        return 0

    if args.command == "lookup":
        map_path = pathlib.Path(args.map)
        results = lookup(
            map_path=map_path,
            symbol=args.symbol,
            path_part=args.path_part,
            kind=args.kind,
            limit=args.limit,
        )
        print_lookup(results)
        return 0

    return 1


if __name__ == "__main__":
    raise SystemExit(main())
