import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");

test("target page exposes drag handle for manual ordering", () => {
  assert.match(html, /<th>排序<\/th>/);
  assert.match(html, /class="drag-handle-button"/);
  assert.match(html, /data-action="drag-target"/);
  assert.match(html, /draggable="true"/);
});

test("target reorder logic persists custom order through state saving", () => {
  assert.match(html, /function moveTargetRow\(draggedId, targetId, placeAfter = false\)/);
  assert.match(html, /state\.targets = nextTargets;/);
  assert.match(html, /saveState\(\);/);
  assert.match(html, /render\(\);/);
});

test("target drag events are wired for row reorder interactions", () => {
  assert.match(html, /let draggingTargetId = "";/);
  assert.match(html, /document\.addEventListener\("dragstart", \(event\) => \{/);
  assert.match(html, /document\.addEventListener\("dragover", \(event\) => \{/);
  assert.match(html, /document\.addEventListener\("drop", \(event\) => \{/);
  assert.match(html, /data-target-row-id="\$\{targetRow\.id\}"/);
});
