#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path


KEYFRAMES = {
    "combat-attack": ["windup", "travel", "hit-stop", "impact", "recovery"],
    "ability-cast": ["anticipation", "charge", "release", "payload", "resolution"],
    "destroy-resolution": ["callout", "fatal-hit", "vanish", "settle"],
    "card-entry": ["spawn", "emphasis", "settle"],
}


def build_markdown(feature: str, pattern: str, assertions: list[str]) -> str:
    lines: list[str] = []
    lines.append("# Animation QA Checklist")
    lines.append("")
    lines.append("## Feature")
    lines.append("")
    lines.append(feature)
    lines.append("")
    lines.append("## Keyframes To Capture")
    lines.append("")
    for keyframe in KEYFRAMES[pattern]:
        lines.append(f"- {keyframe}")
    lines.append("")
    lines.append("## Functional Checks")
    lines.append("")
    lines.append("- Trigger starts on the correct game event.")
    lines.append("- Damage, destroy, or state resolution lands on the intended phase.")
    lines.append("- Final board state matches authoritative game state.")
    lines.append("- Repeated triggers do not overlap into broken states unless explicitly designed.")
    lines.append("")
    lines.append("## Visual Checks")
    lines.append("")
    lines.append("- The hit frame is obvious on first view.")
    lines.append("- Attacker, target, and outcome remain readable.")
    lines.append("- The strongest accent appears at the decisive frame, not before or after it.")
    lines.append("- Residual effects do not outlast the player's need for the board state.")
    lines.append("")
    lines.append("## Performance Checks")
    lines.append("")
    lines.append("- Repeated playback does not leak nodes, timers, or canvases.")
    lines.append("- No obvious frame drops occur during consecutive triggers.")
    lines.append("- Effect density remains safe for lower-end devices or constrained browser tabs.")
    lines.append("")
    lines.append("## Accessibility And Control")
    lines.append("")
    lines.append("- Reduced-motion handling exists or has been explicitly deferred.")
    lines.append("- The interaction still communicates outcome if flashes or shake are reduced.")
    lines.append("- Input lock or turn lock duration matches the visible motion.")
    lines.append("")
    lines.append("## Claims To Sign Off")
    lines.append("")
    if assertions:
        for item in assertions:
            lines.append(f"- {item}")
    else:
        lines.append("- Motion is readable, impactful, and correctly synchronized with state changes.")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a combat animation QA checklist.")
    parser.add_argument("--feature", required=True, help="Feature or interaction name.")
    parser.add_argument(
        "--pattern",
        required=True,
        choices=sorted(KEYFRAMES.keys()),
        help="Closest interaction pattern.",
    )
    parser.add_argument(
        "--assertion",
        action="append",
        default=[],
        help="Claim or acceptance statement to verify. Repeatable.",
    )
    parser.add_argument("--output", help="Optional output markdown file path.")
    args = parser.parse_args()

    markdown = build_markdown(
        feature=args.feature,
        pattern=args.pattern,
        assertions=args.assertion,
    )

    if args.output:
        output_path = Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(markdown, encoding="utf-8")
    else:
        print(markdown, end="")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
