import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");

test("imports the versioned scanner result while retaining the legacy price-table reader", () => {
  assert.match(html, /function normalizePriceScanV2Payload\(payload\)/);
  assert.match(html, /payload\.schemaVersion !== "poe-trade-scan\/v2"/);
  assert.match(html, /payload = normalizePriceScanV2Payload\(payload\);/);
});

test("uses explicit v2 ratio fields when raw OCR text is unavailable", () => {
  assert.match(html, /function formatPriceScanV2Ratio\(ratio\)/);
  assert.match(html, /const left = Number\(ratio && ratio\.left\);/);
  assert.match(html, /const right = Number\(ratio && ratio\.right\);/);
});

test("does not import failed scanner rows into calculator prices", () => {
  assert.match(html, /item\.status === "ok" \|\| item\.status === "imported-v1"/);
});
