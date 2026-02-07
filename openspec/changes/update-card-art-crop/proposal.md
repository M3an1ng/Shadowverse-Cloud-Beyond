# Change: Update Card Art Upload Cropping

## Why
Card art uploads currently accept raw images without editor-side cropping. We need a consistent preview ratio and a freeform crop flow aligned with the avatar upload UX.

## What Changes
- Add a crop modal for card art uploads in the card editor.
- Preview art uses a fixed 848x1024 output and a locked aspect ratio with a resize handle.
- Detail art allows freeform crop and exports the selected region at its original pixel size.
- Reuse existing card art upload endpoints; no backend API changes.

## Impact
- Affected specs: card-art (new)
- Affected code: src/cardEditor.js, src/styles/card-editor.css (crop modal styling), shared UI helpers if introduced