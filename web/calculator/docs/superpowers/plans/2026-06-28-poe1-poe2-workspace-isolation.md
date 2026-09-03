# POE1 / POE2 Workspace Isolation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a top-level POE1 / POE2 switcher whose two workspaces persist all arbitrage configuration and prices independently.

**Architecture:** Keep the existing standalone `index.html` as the only runtime entry. Wrap the existing per-game state in a versioned application state containing `activeGame` and two normalized game states, while retaining `state` as the active workspace reference so existing calculations and editors remain unchanged.

**Tech Stack:** HTML, CSS, browser JavaScript, localStorage, Node.js built-in test runner

---

### Task 1: Add executable storage and isolation contract tests

**Files:**
- Create: `scripts/game-workspace-isolation.test.mjs`
- Modify: `package.json`

- [ ] **Step 1: Write a failing test that executes the inline application script**

Create a lightweight VM harness with fake `localStorage`, `document`, and `window`, then assert migration and independent persistence:

```js
test("migrates legacy state into POE2 and creates a separate POE1 workspace", () => {
  const legacy = { ...legacyGameState, activeTab: "targets" };
  const app = boot({ [LEGACY_STORAGE_KEY]: JSON.stringify(legacy) });
  assert.equal(app.applicationState.activeGame, "poe2");
  assert.equal(app.applicationState.games.poe2.activeTab, "targets");
  assert.equal(app.applicationState.games.poe1.activeTab, "items");
  assert.notStrictEqual(app.applicationState.games.poe1, app.applicationState.games.poe2);
});

test("switching games preserves independent data", () => {
  const app = boot();
  app.state.items[0].name = "POE2 专用";
  app.saveState();
  app.switchGame("poe1");
  app.state.items[0].name = "POE1 专用";
  app.saveState();
  app.switchGame("poe2");
  assert.equal(app.state.items[0].name, "POE2 专用");
  assert.equal(app.applicationState.games.poe1.items[0].name, "POE1 专用");
});
```

Also add static assertions for `data-action="switch-game"`, both game buttons, active styling, and current-game title text.

- [ ] **Step 2: Add the test command**

Add this script to `package.json`:

```json
"test:game-workspaces": "node scripts/game-workspace-isolation.test.mjs"
```

- [ ] **Step 3: Run the new test and verify it fails**

Run:

```powershell
npm run test:game-workspaces
```

Expected: FAIL because the dual-game storage constants, `applicationState`, `switchGame`, and game switcher markup do not exist.

### Task 2: Implement versioned dual-game storage and switching

**Files:**
- Modify: `index.html`
- Test: `scripts/game-workspace-isolation.test.mjs`

- [ ] **Step 1: Introduce separate legacy and current storage keys**

Replace the single storage constant with:

```js
const LEGACY_STORAGE_KEY = "poe2-arbitrage-tool-standalone-v3";
const STORAGE_KEY = "poe-arbitrage-tool-standalone-v4";
const GAME_IDS = ["poe1", "poe2"];
```

- [ ] **Step 2: Extract per-game normalization from the old loader**

Move the current field-by-field fallback logic into:

```js
function normalizeGameState(saved) {
  if (!saved || typeof saved !== "object") {
    return getDefaultState();
  }

  return {
    activeTab: normalizedActiveTab,
    showFormulaTab,
    selectedModeKey,
    items,
    targets,
    pairs,
    runeArbitrage,
    formulaArbitrage
  };
}
```

The returned properties must preserve the existing exact normalization rules for every field.

- [ ] **Step 3: Load or migrate the top-level application state**

Implement:

```js
function getDefaultApplicationState() {
  return {
    activeGame: "poe2",
    games: {
      poe1: getDefaultState(),
      poe2: getDefaultState()
    }
  };
}

function loadApplicationState() {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || "null");
    if (saved && typeof saved === "object" && saved.games) {
      return {
        activeGame: GAME_IDS.includes(saved.activeGame) ? saved.activeGame : "poe2",
        games: {
          poe1: normalizeGameState(saved.games.poe1),
          poe2: normalizeGameState(saved.games.poe2)
        }
      };
    }
  } catch {}

  let legacyPoe2 = null;
  try {
    legacyPoe2 = JSON.parse(localStorage.getItem(LEGACY_STORAGE_KEY) || "null");
  } catch {}

  return {
    activeGame: "poe2",
    games: {
      poe1: getDefaultState(),
      poe2: normalizeGameState(legacyPoe2)
    }
  };
}
```

Initialize the active reference:

```js
let applicationState = loadApplicationState();
let state = applicationState.games[applicationState.activeGame];
```

- [ ] **Step 4: Persist the envelope and implement switching**

Replace `saveState()` and add:

```js
function saveState() {
  applicationState.games[applicationState.activeGame] = state;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(applicationState));
}

function switchGame(gameId) {
  if (!GAME_IDS.includes(gameId) || gameId === applicationState.activeGame) {
    return;
  }

  saveState();
  applicationState.activeGame = gameId;
  state = applicationState.games[gameId];
  saveState();
  render();
}
```

- [ ] **Step 5: Run the storage tests**

Run:

```powershell
npm run test:game-workspaces
```

Expected: storage migration and workspace isolation tests PASS; UI assertions may still fail until Task 3.

### Task 3: Add the high-level game switcher and visual context

**Files:**
- Modify: `index.html`
- Test: `scripts/game-workspace-isolation.test.mjs`

- [ ] **Step 1: Add responsive switcher styles**

Add:

```css
.game-switcher {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin-bottom: 16px;
  padding: 8px;
  border: 1px solid var(--line-strong);
  border-radius: 14px;
  background: rgba(11, 9, 8, 0.78);
}

.game-switch-button {
  min-width: 0;
  padding: 13px 16px;
  border: 1px solid var(--line);
  border-radius: 10px;
  color: var(--muted);
  background: rgba(16, 12, 10, 0.88);
  cursor: pointer;
}

.game-switch-button.active {
  color: var(--ink);
  border-color: rgba(224, 173, 91, 0.72);
  background: linear-gradient(180deg, rgba(182, 69, 53, 0.96), rgba(106, 31, 24, 0.98));
}
```

- [ ] **Step 2: Render the switcher above the hero**

At the beginning of `app.innerHTML`, render:

```html
<nav class="game-switcher" aria-label="选择游戏">
  <button class="game-switch-button ${applicationState.activeGame === "poe1" ? "active" : ""}"
          data-action="switch-game" data-game="poe1" type="button">POE 1</button>
  <button class="game-switch-button ${applicationState.activeGame === "poe2" ? "active" : ""}"
          data-action="switch-game" data-game="poe2" type="button">POE 2</button>
</nav>
```

Derive:

```js
const currentGameLabel = applicationState.activeGame === "poe1" ? "POE 1" : "POE 2";
```

Use it in the eyebrow, title, and document title so the current context is visible.

- [ ] **Step 3: Wire the click handler**

Before tab handling, add:

```js
if (action === "switch-game") {
  switchGame(target.dataset.game);
  return;
}
```

- [ ] **Step 4: Run all standalone tests**

Run:

```powershell
npm run test:game-workspaces
node --test scripts/*.test.mjs
```

Expected: all tests PASS.

- [ ] **Step 5: Verify the production build remains healthy**

Run:

```powershell
npm run build
```

Expected: TypeScript and Vite build complete successfully. The build validates the separate React artifact only; the user-facing standalone behavior is covered by the Node tests and browser verification.

### Task 4: Browser verification

**Files:**
- Verify: `index.html`

- [ ] **Step 1: Open the actual standalone entry in a browser**

Open `index.html` through a local HTTP server and confirm the game switcher appears above the hero.

- [ ] **Step 2: Verify isolation manually**

Enter distinct item names and prices in POE1 and POE2, switch repeatedly, and confirm each workspace restores its own values, active tab, and selected mode.

- [ ] **Step 3: Verify persistence and migration**

Reload the page and confirm the last game remains active. Seed the legacy key in browser storage with a recognizable POE2 value, remove the v4 key, reload, and confirm the value appears only in POE2.

- [ ] **Step 4: Verify responsive layout**

At a narrow mobile viewport, confirm both game buttons remain equal width, readable, and free of horizontal overflow.

## Repository Note

This workspace has no `.git` directory, so the commit steps normally required by the planning workflow cannot be performed. All changes remain directly in the provided project directory.
