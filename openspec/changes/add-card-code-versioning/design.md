## Context
- Card types use GameEnum CardType and no longer distinguish evolved types.
- Card definitions require stable identity (Code) with versioned content.
- Player decks should always use latest versions; replays must remain deterministic.

## Goals / Non-Goals
- Goals:
  - Generate an 8-digit Code per card definition and track CardVersion.
  - Preserve old versions as immutable records.
  - Persist abilities and art only on card save, scoped per version.
  - Keep decks auto-updated to latest versions.
  - Save match/replay snapshots to prevent drift.
- Non-Goals:
  - Migrating historical replays that predate snapshot storage.
  - Supporting evolved card types in Code encoding.

## Decisions
- Code uses 8 digits (D8), where:
  - Digit 1: group
  - Digits 2-3: pack sequence (00-99)
  - Digit 4: craft
  - Digit 5: rarity
  - Digit 6: card type (GameEnum CardType)
  - Digit 7: serial (0-9)
  - Digit 8: reserved, fixed to 0
- CardVersion increments per Code; saving a new version creates a new CardDefinition row.
- Only the latest version per Code is editable; older versions are immutable.
- Decks store Code and resolve to the latest version at play time.
- Deck code v2 encodes Code; legacy deck code maps ids to Code and resolves latest.
- Match/replay snapshots store resolved versions (Code + CardVersion or CardDefinitionId) and are used for playback.
- Deleting a referenced version is blocked or converted to archive-only retention.

## Risks / Trade-offs
- Serial digit limits Code capacity to 10 per (group, pack, craft, rarity, type) bucket.
- Auto-generation must handle concurrency to avoid duplicate Code values.
- Deck auto-updates can change balance; UI should surface version changes.
- Draft storage for art/abilities adds complexity and cleanup requirements.

## Migration Plan
1) Backfill Code for existing cards using the new 8-digit rule; set CardVersion=1.
2) Update deck entries to use Code; migrate deck code to v2 while keeping legacy decode.
3) Introduce snapshot storage for new matches/replays.
4) Archive or block deletion of versions referenced by snapshots.

## Open Questions
- Mapping source for pack/craft/rarity/type when generating Code from existing data.
- Snapshot storage format (Code+CardVersion vs full JSON fallback).
- Deletion policy enforcement for versions referenced by replays.
