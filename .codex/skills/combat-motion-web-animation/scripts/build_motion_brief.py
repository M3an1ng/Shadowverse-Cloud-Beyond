#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path


PATTERNS = {
    "combat-attack": {
        "feel": "fast, decisive, readable",
        "phases": [
            ("Windup", "80-180ms", "prepare force", "Keep it short and directional."),
            ("Travel", "120-220ms", "deliver force", "Use a single dominant path."),
            ("Hit-stop", "40-90ms", "sell contact", "Freeze or sharply slow on contact."),
            ("Impact", "120-220ms", "show damage", "Flash, accent shape, target reaction."),
            ("Recovery", "120-240ms", "restore board", "Return control quickly."),
        ],
        "layers": [
            "attacker movement",
            "impact flash",
            "target recoil or shrink",
            "optional screen shake",
        ],
    },
    "ability-cast": {
        "feel": "charged, controlled, escalating",
        "phases": [
            ("Anticipation", "100-220ms", "show intent", "Prime the player for a cast."),
            ("Charge", "180-420ms", "build energy", "Use a clear buildup cue."),
            ("Release", "60-140ms", "break tension", "Make the release frame obvious."),
            ("Payload", "120-320ms", "carry effect", "Travel or transfer energy."),
            ("Resolution", "120-260ms", "show outcome", "Apply impact and state change."),
        ],
        "layers": [
            "charge accumulation",
            "release burst",
            "payload trail",
            "target resolution",
        ],
    },
    "destroy-resolution": {
        "feel": "final, sharp, uncluttered",
        "phases": [
            ("Callout", "60-140ms", "mark the target", "Focus attention before removal."),
            ("Fatal Hit", "80-180ms", "sell the finishing blow", "Place the strongest accent here."),
            ("Vanish", "120-260ms", "remove the target", "Dissolve, collapse, or break apart."),
            ("Settle", "80-160ms", "stabilize the board", "Leave the final state readable."),
        ],
        "layers": [
            "target callout",
            "fatal impact",
            "vanish or collapse treatment",
            "board settle feedback",
        ],
    },
    "card-entry": {
        "feel": "clean, focused, quick",
        "phases": [
            ("Spawn", "80-160ms", "introduce the card", "Avoid too much distance."),
            ("Emphasis", "80-180ms", "draw the eye", "Use a single focus accent."),
            ("Settle", "120-200ms", "lock the board", "End in a stable state."),
        ],
        "layers": [
            "spawn motion",
            "focus accent",
            "settle easing",
        ],
    },
}


def build_markdown(
    feature: str,
    pattern: str,
    style: str | None,
    stack: str | None,
    references: list[str],
    constraints: list[str],
) -> str:
    config = PATTERNS[pattern]
    lines: list[str] = []
    lines.append("# Motion Brief")
    lines.append("")
    lines.append(f"## Feature")
    lines.append("")
    lines.append(feature)
    lines.append("")
    lines.append("## Pattern")
    lines.append("")
    lines.append(pattern)
    lines.append("")
    lines.append("## Desired Feel")
    lines.append("")
    lines.append(style or config["feel"])
    lines.append("")
    lines.append("## Sequence")
    lines.append("")
    lines.append("| Phase | Duration | Goal | Notes |")
    lines.append("| --- | --- | --- | --- |")
    for phase, duration, goal, notes in config["phases"]:
        lines.append(f"| {phase} | {duration} | {goal} | {notes} |")
    lines.append("")
    lines.append("## Essential Layers")
    lines.append("")
    for layer in config["layers"]:
        lines.append(f"- {layer}")
    lines.append("")
    lines.append("## References")
    lines.append("")
    if references:
        for item in references:
            lines.append(f"- {item}")
    else:
        lines.append("- Add 3-5 references covering gameplay VFX, motion direction, and implementation.")
    lines.append("")
    lines.append("## Implementation Stack")
    lines.append("")
    lines.append(stack or "Pick the smallest stack that can express the effect.")
    lines.append("")
    lines.append("## Constraints")
    lines.append("")
    if constraints:
        for item in constraints:
            lines.append(f"- {item}")
    else:
        lines.append("- Keep combat readability higher than visual density.")
        lines.append("- Keep the hit frame obvious on first viewing.")
        lines.append("- Integrate with authoritative state changes.")
    lines.append("")
    lines.append("## Risks")
    lines.append("")
    lines.append("- Weak impact if the hit frame lacks contrast.")
    lines.append("- Sluggish feel if windup or settle phases drag.")
    lines.append("- Board readability loss if particles or flashes hide the target.")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a combat motion brief.")
    parser.add_argument("--feature", required=True, help="Feature or interaction name.")
    parser.add_argument(
        "--pattern",
        required=True,
        choices=sorted(PATTERNS.keys()),
        help="Closest interaction pattern.",
    )
    parser.add_argument("--style", help="Desired feel override.")
    parser.add_argument("--stack", help="Planned implementation stack.")
    parser.add_argument(
        "--reference",
        action="append",
        default=[],
        help="Reference note or URL. Repeatable.",
    )
    parser.add_argument(
        "--constraint",
        action="append",
        default=[],
        help="Constraint or acceptance rule. Repeatable.",
    )
    parser.add_argument("--output", help="Optional output markdown file path.")
    args = parser.parse_args()

    markdown = build_markdown(
        feature=args.feature,
        pattern=args.pattern,
        style=args.style,
        stack=args.stack,
        references=args.reference,
        constraints=args.constraint,
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
