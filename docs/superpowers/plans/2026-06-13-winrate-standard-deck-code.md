# Winrate Standard Deck Code Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable copying standard deck codes from the winrate dashboard preview.

**Architecture:** The devtools composition endpoint generates an `SVB2` code from its historical card snapshot through `IDeckCodeService`. The frontend enables its existing export button only when the response contains a code and copies it through a small testable clipboard helper.

**Tech Stack:** ASP.NET Core 8, EF Core, xUnit, JavaScript ES modules, Jest

---

### Task 1: Backend deck code response

**Files:**
- Modify: `Server.Tests/DevToolsWinrateStatsControllerTests.cs`
- Modify: `Server/Controllers/DevToolsWinrateStatsController.cs`

- [ ] Add a failing controller test asserting that a standard composition response contains a decodable `SVB2` code with matching class, environment, rule, and cards.
- [ ] Run the focused xUnit test and confirm it fails because `DeckCode` does not exist.
- [ ] Inject `IDeckCodeService`, add nullable `DeckCode` to `DeckCompositionResponse`, and encode only `DeckRule.Standard` snapshots.
- [ ] Add a test asserting a dual-class composition response has a null code.
- [ ] Run the controller test class and confirm both behaviors pass.

### Task 2: Frontend clipboard behavior

**Files:**
- Create: `src/__tests__/devToolsWinrateStats.test.js`
- Modify: `src/devToolsWinrateStats.js`

- [ ] Add failing Jest tests for extracting a non-empty response code and copying it with the Clipboard API.
- [ ] Run the focused Jest test and confirm the exported helpers are missing.
- [ ] Add the minimal helpers and store the active preview code in `DevToolsWinrateStats`.
- [ ] Enable the preview export button only when a standard deck code is available and report copy results through `setMessage`.
- [ ] Run the focused Jest test and confirm it passes.

### Task 3: Verification

**Files:**
- Refresh: `.codex/code-map.json`
- Refresh: `.codex/code-map.md`

- [ ] Run the focused backend test class.
- [ ] Run the focused frontend test.
- [ ] Run the frontend production build.
- [ ] Refresh the code map with `--changed-only`.
- [ ] Review `git diff` to confirm only intended files changed.

