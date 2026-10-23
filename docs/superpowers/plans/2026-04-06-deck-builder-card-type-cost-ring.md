# Deck Builder Card Type Cost Ring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add follower/spell/amulet type styling to cost badges in deck builder library cards, selected deck mini cards, and deck preview modal cards without changing card borders.

**Architecture:** Introduce one tiny shared helper that normalizes card types into reusable root CSS classes, then apply that class in the three renderers and let `deck-builder.css` style each cost badge variant. Cover the helper and preview markup with targeted Jest tests so the behavior is verified before implementation.

**Tech Stack:** JavaScript, Jest + jsdom, existing deck builder CSS

---

### Task 1: Add shared card-type helper tests

**Files:**
- Create: `src/__tests__/deckCardTypeStyling.test.js`
- Create: `src/deckCardTypeStyling.js`

- [ ] **Step 1: Write the failing test**

```javascript
import { getDeckCardTypeClass } from "../deckCardTypeStyling.js";

describe("getDeckCardTypeClass", () => {
    test.each([
        ["Follower", "type-follower"],
        ["spell", "type-spell"],
        ["Amulet", "type-amulet"],
        ["crest", ""],
        ["", ""],
        [null, ""]
    ])("maps %p to %p", (value, expected) => {
        expect(getDeckCardTypeClass(value)).toBe(expected);
    });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/__tests__/deckCardTypeStyling.test.js --runInBand`

Expected: FAIL with module or export not found for `deckCardTypeStyling.js`

- [ ] **Step 3: Write minimal implementation**

```javascript
function normalizeDeckCardType(value) {
    return String(value ?? "").trim().toLowerCase();
}

export function getDeckCardTypeClass(value) {
    const normalized = normalizeDeckCardType(value);
    if (normalized === "follower" || normalized === "spell" || normalized === "amulet") {
        return `type-${normalized}`;
    }
    return "";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest src/__tests__/deckCardTypeStyling.test.js --runInBand`

Expected: PASS

### Task 2: Add failing preview markup regression test

**Files:**
- Modify: `src/components/deckPreviewModal.js`
- Modify: `src/__tests__/deckCardTypeStyling.test.js`

- [ ] **Step 1: Extend the failing test with preview markup expectations**

```javascript
import { buildDeckPreviewSnapshot, createDeckPreviewModal } from "../components/deckPreviewModal.js";

test("deck preview cards expose root type classes for cost ring styling", () => {
    document.body.innerHTML = `<div id="host"></div>`;
    const modal = createDeckPreviewModal({
        onRequestExport: jest.fn(),
        onCardClick: jest.fn()
    });

    modal.open({
        name: "Type Preview",
        environmentLabel: "标准",
        classLabel: "精灵",
        stats: {},
        snapshot: buildDeckPreviewSnapshot([
            { cardDefinitionId: 1, count: 1, cost: 2, name: "法术卡", type: "Spell", rarity: "bronze" },
            { cardDefinitionId: 2, count: 1, cost: 3, name: "护符卡", type: "Amulet", rarity: "silver" }
        ])
    });

    const cards = Array.from(document.querySelectorAll(".deck-preview-card"));
    expect(cards[0].className).toContain("type-spell");
    expect(cards[1].className).toContain("type-amulet");
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx jest src/__tests__/deckCardTypeStyling.test.js --runInBand -t "deck preview cards expose root type classes"`

Expected: FAIL because preview card class names do not yet include `type-spell` / `type-amulet`

- [ ] **Step 3: Implement the minimal preview wiring**

```javascript
import { getDeckCardTypeClass } from "../deckCardTypeStyling.js";

const typeClass = getDeckCardTypeClass(item.type);
const cardClassName = `deck-preview-card${previewUrl ? " has-art" : ""}${rarityClass ? ` ${rarityClass}` : ""}${typeClass ? ` ${typeClass}` : ""}`;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest src/__tests__/deckCardTypeStyling.test.js --runInBand -t "deck preview cards expose root type classes"`

Expected: PASS

### Task 3: Wire deck builder library and selected-card markup

**Files:**
- Modify: `src/deckBuilder.js`
- Modify: `src/__tests__/deckCardTypeStyling.test.js`

- [ ] **Step 1: Add failing helper-driven markup tests**

```javascript
test("shared helper returns classes used by deck builder renderers", () => {
    expect(getDeckCardTypeClass("Follower")).toBe("type-follower");
    expect(getDeckCardTypeClass("Spell")).toBe("type-spell");
    expect(getDeckCardTypeClass("Amulet")).toBe("type-amulet");
});
```

- [ ] **Step 2: Run test to verify it fails if markup wiring is absent**

Run: `npx jest src/__tests__/deckCardTypeStyling.test.js --runInBand`

Expected: FAIL from at least one missing renderer integration assertion

- [ ] **Step 3: Implement minimal deck builder wiring**

```javascript
import { getDeckCardTypeClass } from "./deckCardTypeStyling.js";

const typeClass = getDeckCardTypeClass(card?.type ?? card?.Type ?? card?.cardType ?? card?.card_type);
const cardClassName = `library-card${previewUrl ? ' has-art' : ''}${rarityClass ? ` ${rarityClass}` : ''}${typeClass ? ` ${typeClass}` : ''}`;

const miniTypeClass = getDeckCardTypeClass(item.card?.type ?? item.card?.Type ?? item.card?.cardType ?? item.card?.card_type);
return `
    <div class="deck-card-mini${rarityClass ? ` ${rarityClass}` : ''}${miniTypeClass ? ` ${miniTypeClass}` : ''}" ...>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx jest src/__tests__/deckCardTypeStyling.test.js --runInBand`

Expected: PASS

### Task 4: Add CSS for the three cost badges

**Files:**
- Modify: `src/styles/deck-builder.css`

- [ ] **Step 1: Add minimal CSS variables for type colors**

```css
#deck-builder-page {
    --db-type-follower: #ef4444;
    --db-type-spell: #a78bfa;
    --db-type-amulet: #32ab5e;
}
```

- [ ] **Step 2: Add type badge rules for all three card contexts**

```css
.library-card.type-follower .card-cost,
.deck-card-mini.type-follower .mini-cost,
.deck-preview-card.type-follower .deck-preview-card-cost {
    border-color: var(--db-type-follower);
    color: var(--db-type-follower);
    box-shadow: 0 0 10px rgba(239, 68, 68, 0.18);
}
```

Repeat for `type-spell` and `type-amulet` using the agreed purple and green values.

- [ ] **Step 3: Keep hover-only enhancement lightweight**

```css
.library-card.type-spell:hover .card-cost,
.deck-card-mini.type-spell:hover .mini-cost,
.deck-preview-card.type-spell:hover .deck-preview-card-cost {
    box-shadow: 0 0 12px rgba(167, 139, 250, 0.28);
}
```

- [ ] **Step 4: Run targeted tests**

Run: `npx jest src/__tests__/deckCardTypeStyling.test.js --runInBand`

Expected: PASS

### Task 5: Verify the full slice

**Files:**
- Modify: `src/deckBuilder.js`
- Modify: `src/components/deckPreviewModal.js`
- Modify: `src/styles/deck-builder.css`
- Modify: `src/__tests__/deckCardTypeStyling.test.js`
- Create: `src/deckCardTypeStyling.js`

- [ ] **Step 1: Run the focused regression test**

Run: `npx jest src/__tests__/deckCardTypeStyling.test.js --runInBand`

Expected: PASS

- [ ] **Step 2: Run a broader related suite**

Run: `npx jest src/__tests__ --runInBand`

Expected: PASS with no new regressions in existing `src/__tests__` suites

- [ ] **Step 3: Refresh the code map checkpoint**

Run: `python ".codex/skills/checkpoint-code-map/scripts/code_map.py" build --root . --out ".codex/code-map.json" --summary ".codex/code-map.md" --changed-only`

Expected: map refreshed successfully
