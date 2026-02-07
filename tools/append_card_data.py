import json
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: append_card_data.py <output_path> [input_json]", file=sys.stderr)
        return 1

    output_path = Path(sys.argv[1])
    input_path = Path(sys.argv[2]) if len(sys.argv) > 2 else None
    try:
        if input_path:
            raw = input_path.read_text(encoding="utf-8")
        else:
            raw = sys.stdin.read()
    except OSError as exc:
        print(f"Failed to read card JSON: {exc}", file=sys.stderr)
        return 2

    raw = raw.lstrip("\ufeff").strip()
    if not raw:
        print("Failed to read card JSON: empty input.", file=sys.stderr)
        return 2

    try:
        card = json.loads(raw)
    except json.JSONDecodeError as exc:
        print(f"Failed to read card JSON: {exc}", file=sys.stderr)
        return 2

    if output_path.exists():
        with output_path.open("r", encoding="utf-8") as handle:
            try:
                data = json.load(handle)
            except json.JSONDecodeError as exc:
                print(f"Failed to read existing JSON: {exc}", file=sys.stderr)
                return 3
        if not isinstance(data, list):
            print("Output JSON must be a list.", file=sys.stderr)
            return 4
    else:
        data = []

    data.append(card)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        json.dumps(data, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
