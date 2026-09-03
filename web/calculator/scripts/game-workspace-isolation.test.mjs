import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");
const inlineScript = html.match(/<script>([\s\S]*?)<\/script>/)?.[1] || "";

const LEGACY_STORAGE_KEY = "poe2-arbitrage-tool-standalone-v3";
const STORAGE_KEY = "poe-arbitrage-tool-standalone-v4";

function createStorage(seed = {}) {
  const values = new Map(Object.entries(seed));
  return {
    getItem(key) {
      return values.has(key) ? values.get(key) : null;
    },
    setItem(key, value) {
      values.set(key, String(value));
    },
    removeItem(key) {
      values.delete(key);
    },
    readJson(key) {
      const value = values.get(key);
      return value ? JSON.parse(value) : null;
    }
  };
}

function boot(seed = {}) {
  const localStorage = createStorage(seed);
  const app = { innerHTML: "" };
  const document = {
    activeElement: null,
    title: "",
    addEventListener() {},
    getElementById(id) {
      return id === "app" ? app : null;
    },
    querySelector() {
      return null;
    }
  };
  const context = {
    console,
    document,
    localStorage,
    window: { localStorage },
    FileReader: class {},
    alert() {},
    Intl,
    JSON,
    Math,
    Number,
    String,
    Array,
    Object,
    Map,
    Set
  };
  context.globalThis = context;

  const testApi = `
    globalThis.__testApi = typeof switchGame === "function"
      ? {
          get applicationState() { return applicationState; },
          get state() { return state; },
          saveState,
          switchGame
        }
      : null;
  `;
  vm.runInNewContext(inlineScript + testApi, context);

  return { api: context.__testApi, app, document, localStorage };
}

function createLegacyGameState(itemName = "旧 POE2 数据") {
  return {
    activeTab: "targets",
    showFormulaTab: true,
    selectedModeKey: "legacy-mode",
    items: [{ id: "legacy-item", name: itemName, category: "currency", goldCost: "25" }],
    targets: [{ id: "legacy-target", itemId: "", rates: {} }],
    pairs: [{ id: "legacy-pair", baseItemId: "", quoteItemId: "", rate: "" }]
  };
}

test("migrates legacy state into POE2 and creates an independent POE1 workspace", () => {
  const { api, localStorage } = boot({
    [LEGACY_STORAGE_KEY]: JSON.stringify(createLegacyGameState())
  });

  assert.ok(api, "dual-game workspace API should exist");
  assert.equal(api.applicationState.activeGame, "poe2");
  assert.equal(api.applicationState.games.poe2.activeTab, "targets");
  assert.equal(api.applicationState.games.poe2.items[0].name, "旧 POE2 数据");
  assert.equal(api.applicationState.games.poe1.activeTab, "items");
  assert.equal(api.applicationState.games.poe1.items[0].name, "");
  assert.notStrictEqual(api.applicationState.games.poe1, api.applicationState.games.poe2);
  assert.ok(localStorage.readJson(STORAGE_KEY), "migration should persist the v4 envelope");
});

test("switching games persists isolated item data and selected context", () => {
  const { api, localStorage } = boot();
  assert.ok(api, "dual-game workspace API should exist");

  api.state.items[0].name = "POE2 专用";
  api.state.activeTab = "runes";
  api.saveState();

  api.switchGame("poe1");
  api.state.items[0].name = "POE1 专用";
  api.state.activeTab = "pairs";
  api.saveState();

  api.switchGame("poe2");
  assert.equal(api.state.items[0].name, "POE2 专用");
  assert.equal(api.state.activeTab, "runes");
  assert.equal(api.applicationState.games.poe1.items[0].name, "POE1 专用");
  assert.equal(api.applicationState.games.poe1.activeTab, "pairs");

  const persisted = localStorage.readJson(STORAGE_KEY);
  assert.equal(persisted.activeGame, "poe2");
  assert.equal(persisted.games.poe1.items[0].name, "POE1 专用");
  assert.equal(persisted.games.poe2.items[0].name, "POE2 专用");
});

test("restores the last selected game from v4 storage", () => {
  const firstBoot = boot();
  assert.ok(firstBoot.api, "dual-game workspace API should exist");
  firstBoot.api.switchGame("poe1");

  const savedEnvelope = firstBoot.localStorage.readJson(STORAGE_KEY);
  const secondBoot = boot({ [STORAGE_KEY]: JSON.stringify(savedEnvelope) });

  assert.equal(secondBoot.api.applicationState.activeGame, "poe1");
  assert.strictEqual(secondBoot.api.state, secondBoot.api.applicationState.games.poe1);
});

test("renders a high-level game switcher and current game context", () => {
  assert.match(html, /class="game-switcher"/);
  assert.match(html, /data-action="switch-game" data-game="poe1"/);
  assert.match(html, /data-action="switch-game" data-game="poe2"/);
  assert.match(html, />POE 1<\/button>/);
  assert.match(html, />POE 2<\/button>/);
  assert.match(html, /\.game-switch-button\.active/);
  assert.match(html, /const currentGameLabel =/);
});

test("keeps wide tables contained inside panels on narrow screens", () => {
  assert.match(html, /\.panel\s*\{[^}]*min-width:\s*0;[^}]*\}/);
  assert.match(html, /\.table-wrap\s*\{[^}]*overflow-x:\s*auto;[^}]*\}/);
});
